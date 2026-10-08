#if IOS || ANDROID
using BaristaNotes.Core.Services.Origins;
using NominatimOriginGeocoder = BaristaNotes.Core.Services.Origins.NominatimOriginGeocoder;
using OriginLookup = BaristaNotes.Core.Services.Origins.OriginLookup;
using Mapsui.Extensions;
using Mapsui.Tiling;
using Microsoft.Maui.ApplicationModel;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using MapControl = Mapsui.UI.Maui.MapControl;
using SdkAttribution = Mapsui.Widgets.ButtonWidgets.HyperlinkWidget;
using SdkDataChangedEventArgs = Mapsui.Fetcher.DataChangedEventArgs;
using SdkLogger = Mapsui.Logging.Logger;
using SdkLogLevel = Mapsui.Logging.LogLevel;
using SdkWidgetEventArgs = Mapsui.Widgets.WidgetEventArgs;

namespace BaristaNotes.Components;

internal sealed class BeanMapView : MauiControls.ContentView, IDisposable
{
    private readonly ILogger _logger;
    private readonly MauiControls.ContentView _mapHost = new()
    {
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None)
    };
    private readonly MauiControls.Grid _surface = new()
    {
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None),
        RowSpacing = 0
    };
    private readonly MauiControls.Label _error = new() { FontFamily = "Manrope", FontSize = 12 };
    private readonly MauiControls.Button _retry;
    private SdkAttribution? _attribution;
    private readonly MauiControls.Grid _errorRow = new();
    private readonly Action<SdkLogLevel, string, Exception?>? _previousSdkLogger;
    private readonly Action<SdkLogLevel, string, Exception?> _sdkLogger;
    private MapControl? _control;
    private Mapsui.Layers.MemoryLayer? _originLayer;
    private BeanOriginResolver? _originResolver;
    private readonly NominatimOriginGeocoder _geocoder;
    private IReadOnlyList<BeanDto> _savedBeans = [];
    private Dictionary<string, OriginLookup> _lookups = new();
    private CancellationTokenSource? _originCancellation;
    private string? _lookupEndpoint;
    private string? _geocodeStatus;
    private bool _geocodeFailed;
    private bool _beansLoaded;
    private bool _initialPlaceFitApplied;
    private bool _initialFitApplied;
    private bool _userExplored;
    private bool _originLoadFailed;
    private readonly HashSet<long> _touches = new();
    private bool _disposed;

    public MauiControls.CollectionView? BeanList { get; set; }
    public BeanOriginSnapshot? Origins { get; private set; }
    public Func<OriginSelection, Task>? OriginSelected { get; set; }
    public Action? OriginsChanged { get; set; }
    public Func<Task>? ReloadOrigins { get; set; }

    public BeanMapView(ILogger logger, NominatimOriginGeocoder geocoder)
    {
        _logger = logger;
        _geocoder = geocoder;
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
        _retry = MakeButton("Retry map", "BeanMapRetry");
        _retry.Clicked += RetryClicked;

        _errorRow.ColumnDefinitions.Add(new MauiControls.ColumnDefinition { Width = GridLength.Star });
        _errorRow.ColumnDefinitions.Add(new MauiControls.ColumnDefinition { Width = GridLength.Auto });
        _errorRow.Padding = new Thickness(12, 0);
        _errorRow.Add(_error);
        _errorRow.Add(_retry, 1);
        _errorRow.IsVisible = false;

        _surface.RowDefinitions.Add(new MauiControls.RowDefinition { Height = GridLength.Star });
        _surface.RowDefinitions.Add(new MauiControls.RowDefinition { Height = GridLength.Auto });
        _surface.Add(_mapHost);
        _surface.Add(_errorRow, 0, 1);
        Content = _surface;

        _previousSdkLogger = SdkLogger.LogDelegate;
        _sdkLogger = (level, message, exception) =>
        {
            _previousSdkLogger?.Invoke(level, message, exception);
            _logger.Log(ToLogLevel(level), exception, "Mapsui: {Message}", message);
            if (level == SdkLogLevel.Error || exception is not null)
                MainThread.BeginInvokeOnMainThread(() => ShowError("Map could not load. Check your connection and retry."));
        };
        SdkLogger.LogDelegate = _sdkLogger;
        if (MauiControls.Application.Current is { } app)
            app.RequestedThemeChanged += ThemeChanged;
        ApplyTheme();
        InitializeMap();
    }

    private static MauiControls.Button MakeButton(string text, string id) => new()
    {
        Text = text,
        AutomationId = id,
        FontFamily = "ManropeSemibold",
        FontSize = 14,
        CornerRadius = 0,
        MinimumHeightRequest = 44,
        Padding = new Thickness(12, 6),
        Margin = new Thickness(4, 0)
    };

    private void InitializeMap()
    {
        try
        {
            _control = new MapControl
            {
                AutomationId = "BeanMap",
                SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None)
            };
            if (_control.Content is SKGLView glView)
                glView.Touch += MapTouchChanged;
            else if (_control.Content is SKCanvasView canvasView)
                canvasView.Touch += MapTouchChanged;
            _control.Unloaded += MapUnloaded;
            _control.Map.DataChanged += MapDataChanged;
            _control.Map.Tapped += OriginTapped;
            _control.Map.PointerPressed += MapPointerPressed;
            _control.Map.Navigator.ViewportChanged += ViewportChanged;
            var layer = OpenStreetMap.CreateTileLayer(
                "BaristaNotes-MAUI-Maps/1.0 (bean map comparison)");
            _attribution = layer.Attribution;
            _attribution.BackColor = Mapsui.Styles.Color.White;
            _attribution.Opacity = 1;
            _attribution.Tapped += AttributionTapped;
            _control.Map.Layers.Add(layer);
            _originLayer = new Mapsui.Layers.MemoryLayer("Saved bean origins")
            {
                Style = null
            };
            _control.Map.Layers.Add(_originLayer);
            UpdateOriginFeatures();
            _mapHost.Content = _control;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not initialize the Beans map");
            ShowError("Map could not start. Retry map; the Beans list is still available.");
            ReleaseMap();
        }
    }

    public void SetBeans(IReadOnlyList<BeanDto> savedBeans)
    {
        if (_disposed)
            return;
        try
        {
            _originResolver ??= new BeanOriginResolver();
            var endpoint = _geocoder.Endpoint;
            var changed = !_beansLoaded || !_savedBeans.SequenceEqual(savedBeans) || _lookupEndpoint != endpoint;
            if (_lookupEndpoint != endpoint)
                _lookups = new();
            _lookupEndpoint = endpoint;
            _savedBeans = savedBeans.ToArray();
            _beansLoaded = true;
            Origins = _originResolver.Build(_savedBeans, _lookups);
            _originLoadFailed = false;
            UpdateOriginFeatures();
            TryInitialFit();
            if (changed)
                StartPlaceResolution(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not plot saved bean origins");
            ShowOriginLoadError();
        }
    }

    private void StartPlaceResolution(bool retryUnresolved)
    {
        if (_disposed || _originResolver is null)
            return;
        _originCancellation?.Cancel();
        _originCancellation?.Dispose();
        _originCancellation = new CancellationTokenSource();
        var token = _originCancellation.Token;
        var queries = _savedBeans.SelectMany(bean => _originResolver.Queries(bean.Origin))
            .Where(query => query.NeedsLookup).DistinctBy(query => query.Key).ToArray();
        if (queries.Length == 0)
        {
            _geocodeFailed = false;
            ClearGeocodeStatus();
            _retry.IsEnabled = true;
            return;
        }
        _geocodeStatus = "Resolving detailed origins; country locations remain approximate until resolved.";
        ShowError(_geocodeStatus);
        _retry.IsEnabled = false;
        var savedBeans = _savedBeans;
        _ = Task.Run(() => ResolvePlacesAsync(queries, savedBeans, retryUnresolved, token), token);
    }

    private async Task ResolvePlacesAsync(
        IReadOnlyList<OriginQuery> queries, IReadOnlyList<BeanDto> savedBeans,
        bool retryUnresolved, CancellationToken token)
    {
        try
        {
            var lookups = new Dictionary<string, OriginLookup>();
            foreach (var query in queries)
                lookups[query.Key] = await _geocoder.ResolveAsync(query, retryUnresolved, token).ConfigureAwait(false);
            var snapshot = _originResolver!.Build(savedBeans, lookups);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (_disposed || token.IsCancellationRequested)
                    return;
                _lookups = lookups;
                Origins = snapshot;
                UpdateOriginFeatures();
                if (!_initialPlaceFitApplied && !_userExplored)
                    _initialFitApplied = false;
                _initialPlaceFitApplied = true;
                TryInitialFit();
                OriginsChanged?.Invoke();
                ClearGeocodeStatus();
                _geocodeFailed = lookups.Values.Any(lookup => lookup.Place is null);
                if (_geocodeFailed)
                {
                    _geocodeStatus = lookups.Values.First(lookup => lookup.Place is null).Error
                        ?? "Detailed origin unresolved. Country locations remain approximate; Retry map to retry.";
                    ShowError(_geocodeStatus);
                }
                _retry.IsEnabled = true;
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not resolve detailed bean origins");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (_disposed || token.IsCancellationRequested)
                    return;
                _geocodeFailed = true;
                _geocodeStatus = "Could not resolve detailed origins. Country locations remain approximate; Retry map to retry.";
                ShowError(_geocodeStatus);
                _retry.IsEnabled = true;
            });
        }
    }

    private void ClearGeocodeStatus()
    {
        if (_geocodeStatus is not null && _error.Text == _geocodeStatus)
            _errorRow.IsVisible = false;
        _geocodeStatus = null;
    }

    public void ShowOriginLoadError()
    {
        _originLoadFailed = true;
        ShowError("Could not load saved origins. Retry map to reload.");
    }

    private void UpdateOriginFeatures()
    {
        if (_originLayer is null || _control is null || Origins is null)
            return;
        var light = MauiControls.Application.Current?.RequestedTheme != AppTheme.Dark;
        var surface = ToSdkColor(light ? AppColors.Light.Surface : AppColors.Dark.Surface);
        var text = ToSdkColor(light ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary);
        var accent = ToSdkColor(light ? AppColors.Light.Primary : AppColors.Dark.Primary);
        var viewport = _control.Map.Navigator.Viewport;
        if (!double.IsFinite(viewport.Resolution) || viewport.Resolution <= 0)
            return;
        var clusters = OriginClusterer.Build(Origins.Locations, location =>
        {
            var point = Mapsui.Projections.SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
            var screen = viewport.WorldToScreen(point.Item1, point.Item2);
            return (screen.X, screen.Y);
        });
        _originLayer.Features = clusters.Select(cluster =>
        {
            var points = cluster.Locations.Select(location =>
                Mapsui.Projections.SphericalMercator.FromLonLat(location.Longitude, location.Latitude)).ToArray();
            var feature = new Mapsui.Layers.PointFeature(
                points.Average(point => point.Item1), points.Average(point => point.Item2));
            feature["OriginSelection"] = cluster.Selection;
            feature.Styles.Add(new Mapsui.Styles.SymbolStyle
            {
                SymbolType = Mapsui.Styles.SymbolType.Ellipse,
                SymbolScale = 0.75,
                Fill = new Mapsui.Styles.Brush(accent),
                Outline = new Mapsui.Styles.Pen(surface, 2)
            });
            feature.Styles.Add(new Mapsui.Styles.LabelStyle
            {
                Text = cluster.MarkerLabel,
                Font = new Mapsui.Styles.Font { Size = 12 },
                ForeColor = text,
                BackColor = new Mapsui.Styles.Brush(surface),
                Offset = new Mapsui.Styles.Offset { Y = 32 },
                CollisionDetection = false
            });
            return feature;
        }).ToArray();
        _control.Map.RefreshGraphics();
    }

    private static Mapsui.Styles.Color ToSdkColor(Color color) =>
        new((int)(color.Red * 255), (int)(color.Green * 255),
            (int)(color.Blue * 255), (int)(color.Alpha * 255));

    private void MapPointerPressed(object? sender, Mapsui.MapEventArgs args) => _userExplored = true;

    private void MapTouchChanged(object? sender, SKTouchEventArgs args)
    {
        if (args.ActionType == SKTouchAction.Pressed)
        {
            _userExplored = true;
            _touches.Add(args.Id);
        }
        else if (args.ActionType == SKTouchAction.Released || args.ActionType == SKTouchAction.Exited)
            _touches.Remove(args.Id);
        else if (args.ActionType == SKTouchAction.Cancelled)
            _touches.Clear();
        SetPageScrollEnabled(_touches.Count == 0);
    }

    private void MapUnloaded(object? sender, EventArgs args)
    {
        _touches.Clear();
        SetPageScrollEnabled(true);
    }

    private void SetPageScrollEnabled(bool enabled)
    {
#if ANDROID
        if (_control?.Handler?.PlatformView is Android.Views.View view)
            view.Parent?.RequestDisallowInterceptTouchEvent(!enabled);
#else
        if (BeanList?.Handler?.PlatformView is UIKit.UIView platformView &&
            FindCollectionView(platformView) is { } list)
            list.ScrollEnabled = enabled;
#endif
    }

    private void ViewportChanged(object sender, Mapsui.ViewportChangedEventArgs args)
    {
        if (!_userExplored &&
            (args.PreviousViewport.Width != args.Viewport.Width ||
             args.PreviousViewport.Height != args.Viewport.Height))
            _initialFitApplied = false;
        TryInitialFit();
        UpdateOriginFeatures();
    }

    private void TryInitialFit()
    {
        if (_initialFitApplied || _userExplored || Origins is null || _control is null ||
            !_control.Map.Navigator.HasExecutedPostponedCalls)
            return;
        var navigator = _control.Map.Navigator;
        var viewport = navigator.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
            return;

        var points = Origins.Locations.Select(location =>
            Mapsui.Projections.SphericalMercator.FromLonLat(
                location.Longitude, location.Latitude)).ToArray();
        // Mark before navigating: navigation synchronously raises ViewportChanged.
        _initialFitApplied = true;
        var fit = OriginCameraFit.Calculate(
            points.Length == 0
                ? new[] { (-20037508d, -15000000d), (20037508d, 15000000d) }
                : points,
            viewport.Width, viewport.Height);
        // A compact world/data fit may need to zoom out beyond the tile layer's level-zero bound.
        if (navigator.ZoomBounds is { } bounds && fit.Resolution > bounds.Max)
            navigator.OverrideZoomBounds = new Mapsui.MMinMax(bounds.Min, fit.Resolution);
        navigator.CenterOnAndZoomTo(
            new Mapsui.MPoint(fit.CenterX, fit.CenterY), fit.Resolution);
    }

    private async void OriginTapped(object? sender, Mapsui.MapEventArgs args)
    {
        if (_disposed || _control is null)
            return;
        try
        {
            if (args.GestureType == Mapsui.Manipulations.GestureType.DoubleTap)
            {
                args.Handled = true;
                _userExplored = true;
                _control.Map.Navigator.ZoomIn(args.ScreenPosition, 250);
                return;
            }
            if (args.GestureType != Mapsui.Manipulations.GestureType.SingleTap || _originLayer is null)
                return;
            if (args.GetMapInfo(new[] { _originLayer }).Feature?["OriginSelection"] is not OriginSelection selection)
                return;
            args.Handled = true;
            if (OriginSelected is not { } select)
                throw new InvalidOperationException("Origin bean selection is unavailable.");
            await select(selection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not handle the Beans map tap");
            ShowError(args.GestureType == Mapsui.Manipulations.GestureType.DoubleTap
                ? "Could not zoom the map. Double tap to retry."
                : "Could not show origin beans. Tap the pin to retry.");
        }
    }

    private void MapDataChanged(object sender, SdkDataChangedEventArgs args)
    {
        if (args.Error is null)
            return;
        _logger.LogWarning(args.Error, "Beans map tile request failed");
        MainThread.BeginInvokeOnMainThread(() =>
            ShowError("Map tiles could not load. Check your connection and retry."));
    }

    internal void ShowError(string message)
    {
        if (_disposed)
            return;
        _error.Text = message;
        _errorRow.IsVisible = true;
    }

    private async void RetryClicked(object? sender, EventArgs args)
    {
        _errorRow.IsVisible = false;
        try
        {
            if (_control is null)
                InitializeMap();
            else
                _control.Refresh();
            if (_originLoadFailed && ReloadOrigins is { } reload)
                await reload();
            if (_geocodeFailed && _retry.IsEnabled)
                StartPlaceResolution(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not retry the Beans map");
            ShowError("Map retry failed. The Beans list is still available.");
        }
    }

    private async void AttributionTapped(object? sender, SdkWidgetEventArgs args)
    {
        args.Handled = true;
        try
        {
            if (!await Launcher.OpenAsync(new Uri("https://www.openstreetmap.org/copyright")))
                ShowError("Could not open OpenStreetMap attribution.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not open map attribution");
            ShowError("Could not open OpenStreetMap attribution.");
        }
    }

    private Task NextLayoutAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.Dispatch(() => Dispatcher.Dispatch(() => completion.TrySetResult()));
        return completion.Task;
    }

    private Action? CaptureListPosition()
    {
        if (BeanList is null)
            return null;
#if ANDROID
        if (BeanList.Handler?.PlatformView is not AndroidX.RecyclerView.Widget.RecyclerView list ||
            list.GetLayoutManager() is not AndroidX.RecyclerView.Widget.LinearLayoutManager manager)
            throw new InvalidOperationException("Could not save the Beans list scroll position.");
        var index = manager.FindFirstVisibleItemPosition();
        var row = manager.FindViewByPosition(index)
            ?? throw new InvalidOperationException("The Beans list has not finished laying out.");
        var offset = manager.GetDecoratedTop(row) - list.PaddingTop;
        return () =>
        {
            if (BeanList?.Handler?.PlatformView is not AndroidX.RecyclerView.Widget.RecyclerView current ||
                current.GetLayoutManager() is not AndroidX.RecyclerView.Widget.LinearLayoutManager currentManager)
                throw new InvalidOperationException("The Beans list is unavailable for scroll restoration.");
            currentManager.ScrollToPositionWithOffset(index, offset);
        };
#else
        if (BeanList.Handler?.PlatformView is not UIKit.UIView platformView ||
            FindCollectionView(platformView) is not { } list)
            throw new InvalidOperationException("Could not save the Beans list scroll position.");
        var offset = list.ContentOffset;
        return () =>
        {
            if (BeanList?.Handler?.PlatformView is not UIKit.UIView currentPlatformView ||
                FindCollectionView(currentPlatformView) is not { } current)
                throw new InvalidOperationException("The Beans list is unavailable for scroll restoration.");
            current.Window?.LayoutIfNeeded();
            current.LayoutIfNeeded();
            current.SetContentOffset(offset, animated: false);
        };
#endif
    }

#if IOS
    internal static UIKit.UICollectionView? FindCollectionView(UIKit.UIView view)
    {
        if (view is UIKit.UICollectionView collectionView)
            return collectionView;
        foreach (var child in view.Subviews)
            if (FindCollectionView(child) is { } contained)
                return contained;
        return null;
    }
#endif

    public Action? SaveListPosition()
    {
        try
        {
            return CaptureListPosition();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save the Beans list position");
            ShowError("Could not save the list position. The Beans list remains available.");
            return null;
        }
    }

    public async Task RestoreListPositionAsync(Action? restore)
    {
        if (restore is null || _disposed)
            return;
        try
        {
            await NextLayoutAsync();
            if (BeanList is not null)
                restore();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not restore the Beans list position");
            ShowError("Could not restore the list position. The Beans list remains available.");
        }
    }

    private void ThemeChanged(object? sender, AppThemeChangedEventArgs args) => ApplyTheme();

    private void ApplyTheme()
    {
        var light = MauiControls.Application.Current?.RequestedTheme != AppTheme.Dark;
        var surface = light ? AppColors.Light.Surface : AppColors.Dark.Surface;
        var text = light ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        BackgroundColor = _surface.BackgroundColor = _mapHost.BackgroundColor = surface;
        _error.TextColor = text;
        _retry.BackgroundColor = surface;
        _retry.TextColor = text;
        UpdateOriginFeatures();
    }

    private static LogLevel ToLogLevel(SdkLogLevel level) => level switch
    {
        SdkLogLevel.Error => LogLevel.Error,
        SdkLogLevel.Warning => LogLevel.Warning,
        SdkLogLevel.Information => LogLevel.Information,
        SdkLogLevel.Debug => LogLevel.Debug,
        _ => LogLevel.Trace
    };

    private void ReleaseMap()
    {
        if (_control is not { } control)
            return;
        MapUnloaded(this, EventArgs.Empty);
        control.Unloaded -= MapUnloaded;
        if (control.Content is SKGLView glView)
            glView.Touch -= MapTouchChanged;
        else if (control.Content is SKCanvasView canvasView)
            canvasView.Touch -= MapTouchChanged;
        _mapHost.Content = null;
        if (_attribution is { } attribution)
            attribution.Tapped -= AttributionTapped;
        _attribution = null;
        control.Map.DataChanged -= MapDataChanged;
        control.Map.Tapped -= OriginTapped;
        control.Map.PointerPressed -= MapPointerPressed;
        control.Map.Navigator.ViewportChanged -= ViewportChanged;
        control.Dispose();
        _control = null;
        _originLayer = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _originCancellation?.Cancel();
        _originCancellation?.Dispose();
        _originCancellation = null;
        if (MauiControls.Application.Current is { } app)
            app.RequestedThemeChanged -= ThemeChanged;
        if (SdkLogger.LogDelegate == _sdkLogger)
            SdkLogger.LogDelegate = _previousSdkLogger;
        _retry.Clicked -= RetryClicked;
        OriginSelected = null;
        OriginsChanged = null;
        ReloadOrigins = null;
        ReleaseMap();
    }

}
#endif
