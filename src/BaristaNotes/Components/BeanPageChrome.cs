#if IOS || ANDROID
namespace BaristaNotes.Components;

internal sealed class BeanPageChrome : IDisposable
{
    private readonly BeanMapView _map;
    private readonly MauiControls.Grid _hero = new()
    {
        IsClippedToBounds = true,
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None)
    };
    private readonly MauiControls.BoxView _titleSpace = new() { InputTransparent = true };
    private readonly MauiControls.BoxView _separator = new()
    {
        HeightRequest = 1,
        InputTransparent = true,
        AutomationId = "BeanPageTitleSeparator"
    };
    private readonly MauiControls.Label _count = new()
    {
        FontFamily = "Manrope",
        FontSize = 28,
        FontAttributes = MauiControls.FontAttributes.Bold
    };
    private readonly MauiControls.Label _heading = new()
    {
        FontFamily = "Manrope",
        Text = "BEANS",
        FontSize = 10,
        CharacterSpacing = 2,
        FontAttributes = MauiControls.FontAttributes.Bold
    };
    private readonly MauiControls.Label _country = new()
    {
        FontFamily = "Manrope",
        FontSize = 12,
        VerticalOptions = LayoutOptions.Center
    };
    private readonly MauiControls.Button _clear = new()
    {
        Text = "All beans",
        AutomationId = "BeanMapClearCountry",
        FontFamily = "Manrope",
        FontSize = 14,
        MinimumHeightRequest = 44,
        MinimumWidthRequest = 44,
        CornerRadius = 8,
        BorderWidth = 0,
        Padding = new Thickness(14, 10)
    };
    private readonly MauiControls.Grid _filter = new() { Padding = new Thickness(12, 4) };
    private readonly MauiControls.ContentView _status = new();
    private readonly MauiControls.Label _message = new()
    {
        FontFamily = "Manrope",
        FontSize = 16,
        HorizontalTextAlignment = TextAlignment.Center
    };
    private readonly MauiControls.Label _statusHeading = new()
    {
        FontFamily = "Manrope",
        FontSize = 10,
        CharacterSpacing = 2,
        FontAttributes = MauiControls.FontAttributes.Bold
    };
    private readonly MauiControls.ActivityIndicator _loading = new();
    private readonly MauiControls.Button _retry = new()
    {
        Text = "Retry",
        FontFamily = "Manrope",
        FontSize = 14,
        MinimumHeightRequest = 44,
        MinimumWidthRequest = 44,
        CornerRadius = 8,
        BorderWidth = 0,
        Padding = new Thickness(14, 10)
    };
    private readonly MauiControls.VerticalStackLayout _statusContent = new()
    {
        Spacing = 12,
        Padding = 24,
        MinimumHeightRequest = 160,
        VerticalOptions = LayoutOptions.Center,
        HorizontalOptions = LayoutOptions.Center
    };
    private MauiControls.CollectionView? _list;
    private MauiControls.ContentView? _titleHost;
    private object? _items;
    private string? _statusKey;
    private double _offset;
    private double _heroHeight = BeanPageScrollGeometry.PortraitHeroHeight;
    private double _titleHeight = BeanPageScrollGeometry.MinimumTitleHeight;
    private double _topInset;
    private bool _landscape;
    private bool _queued;
    private bool _disposed;
#if ANDROID
    private MainActivity? _activity;
    private AndroidX.RecyclerView.Widget.RecyclerView? _native;
    private ScrollListener? _listener;
#else
    // StructuredItemsViewController2 tags MAUI's global supplementary cells.
    private const int MauiHeaderTag = 111;
    private const int MauiFooterTag = 222;
    private UIKit.UICollectionView? _native;
    private IDisposable? _offsetObserver;
    private IDisposable? _sizeObserver;
    private double? _bodyHeight;
#endif

    internal MauiControls.Grid Header { get; } = new()
    {
        RowSpacing = 0,
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None)
    };
    internal MauiControls.BoxView StatusBarTint { get; } = new()
    {
        HeightRequest = 0,
        VerticalOptions = LayoutOptions.Start,
        InputTransparent = true
    };
    internal MauiControls.BoxView Footer { get; } = new()
    {
        HeightRequest = 1,
        InputTransparent = true
    };
    internal MauiControls.Grid Title { get; } = new()
    {
        RowSpacing = 0,
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None),
        VerticalOptions = LayoutOptions.Start,
        AutomationId = "BeanPageTitle"
    };

    internal BeanPageChrome(BeanMapView map, Func<Task> clear, Func<Task> retry)
    {
        _map = map;
        _hero.HeightRequest = _heroHeight;
        _hero.Add(map);
        _titleSpace.HeightRequest = _titleHeight;
        for (var i = 0; i < 4; i++)
            Header.RowDefinitions.Add(new MauiControls.RowDefinition { Height = GridLength.Auto });
        Header.Add(_hero);
        Header.Add(_titleSpace, 0, 1);
        Header.Add(_filter, 0, 2);
        Header.Add(_status, 0, 3);
        Title.RowDefinitions.Add(new MauiControls.RowDefinition { Height = GridLength.Auto });
        Title.RowDefinitions.Add(new MauiControls.RowDefinition { Height = GridLength.Auto });
        var labels = new MauiControls.VerticalStackLayout
        {
            Spacing = 4,
            Padding = new Thickness(16, 10),
            SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None)
        };
        labels.Add(_heading);
        labels.Add(_count);
        Title.Add(labels);
        Title.Add(_separator, 0, 1);
        Title.SizeChanged += TitleSizeChanged;
        Header.SizeChanged += LayoutChanged;
        Footer.SizeChanged += LayoutChanged;
        _filter.ColumnDefinitions.Add(new MauiControls.ColumnDefinition { Width = GridLength.Star });
        _filter.ColumnDefinitions.Add(new MauiControls.ColumnDefinition { Width = GridLength.Auto });
        _filter.Add(_country);
        _filter.Add(_clear, 1);
        _clear.Clicked += async (_, _) => await clear();
        _retry.Clicked += async (_, _) => await retry();
        _statusContent.Add(_loading);
        _statusContent.Add(_statusHeading);
        _statusContent.Add(_message);
        _statusContent.Add(_retry);
        _status.Content = _statusContent;
        if (MauiControls.Application.Current is { } app)
            app.RequestedThemeChanged += ThemeChanged;
        ApplyTheme();
        UpdateOffset(0);
    }

    internal void Update(
        object items, int totalCount, string? country, int displayedCount,
        bool loading, string? error)
    {
        _count.Text = totalCount == 1 ? "1 bean" : $"{totalCount} beans";
        _filter.IsVisible = country is not null;
        _country.Text = country;
        _status.IsVisible = loading || error is not null || displayedCount == 0;
        _loading.IsVisible = _loading.IsRunning = loading;
        _statusHeading.IsVisible = _message.IsVisible = !loading;
        _retry.IsVisible = !loading && error is not null;
        _statusHeading.Text = error is not null ? "ERROR" : "NO BEANS";
        _message.Text = error ?? (country is not null
            ? "No saved beans for this origin"
            : "Add your favorite coffee beans");
        ApplyTheme();

        var statusKey = $"{loading}:{error}:{country}:{displayedCount}";
        if (!ReferenceEquals(_items, items) || _statusKey != statusKey)
        {
            _items = items;
            _statusKey = statusKey;
#if ANDROID
            if (_offset <= 0)
                Footer.HeightRequest = 1;
#else
            _bodyHeight = null;
#endif
            QueueLayout();
        }
    }

    private void ThemeChanged(object? sender, AppThemeChangedEventArgs args) => ApplyTheme();

    private void ApplyTheme()
    {
        var light = MauiControls.Application.Current?.RequestedTheme != AppTheme.Dark;
        var surface = light ? AppColors.Light.Surface : AppColors.Dark.Surface;
        var text = light ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var secondary = light ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
        Header.BackgroundColor = Title.BackgroundColor = Footer.BackgroundColor = surface;
        StatusBarTint.Color = surface;
        _separator.Color = light ? AppColors.Light.Outline : AppColors.Dark.Outline;
        if (_titleHost is not null)
            _titleHost.BackgroundColor = surface;
        Footer.Color = _titleSpace.Color = surface;
        _count.TextColor = _country.TextColor = _message.TextColor = text;
        _heading.TextColor = _statusHeading.TextColor = secondary;
        _clear.BackgroundColor = surface;
        _clear.TextColor = text;
        _retry.BackgroundColor = light ? AppColors.Light.Primary : AppColors.Dark.Primary;
        _retry.TextColor = surface;
        _loading.Color = light ? AppColors.Light.Primary : AppColors.Dark.Primary;
    }

    internal void SetLandscape(bool landscape)
    {
        _landscape = landscape;
        UpdateHeroHeight();
    }

    private void UpdateHeroHeight()
    {
        var height = BeanPageScrollGeometry.HeroHeight(_landscape);
        if (_heroHeight == height)
            return;
        _hero.HeightRequest = _heroHeight = height;
        UpdateOffset(_offset);
        QueueLayout();
    }

    internal void Attach(MauiControls.CollectionView list)
    {
        if (_disposed)
            return;
        if (!ReferenceEquals(_list, list))
        {
            Detach();
            _list = list;
            list.SizeChanged += LayoutChanged;
            list.Unloaded += ListUnloaded;
        }
        _map.BeanList = list;
        for (MauiControls.Element? parent = list.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is MauiControls.Page page && page.Width > 0 && page.Height > 0)
            {
                SetLandscape(page.Width > page.Height);
                break;
            }
        }
        if (_native is not null)
            return;
#if ANDROID
        _native = list.Handler?.PlatformView as AndroidX.RecyclerView.Widget.RecyclerView
            ?? throw new InvalidOperationException("The Beans page collection is unavailable.");
        _activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity as MainActivity
            ?? throw new InvalidOperationException("The Beans page activity is unavailable.");
        _activity.SetBeanMapVisible(true);
        _listener = new ScrollListener(this);
        _native.AddOnScrollListener(_listener);
        _native.LayoutChange += NativeLayoutChanged;
#else
        _native = list.Handler?.PlatformView is UIKit.UIView platformView
            ? BeanMapView.FindCollectionView(platformView)
            : null;
        if (_native is null)
            throw new InvalidOperationException("The Beans page collection is unavailable.");
        _native.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
        _native.LayoutIfNeeded();
        _offsetObserver = _native.AddObserver("contentOffset",
            Foundation.NSKeyValueObservingOptions.New, _ =>
            {
                ReadOffset();
                QueueLayout();
            });
        _sizeObserver = _native.AddObserver("contentSize",
            Foundation.NSKeyValueObservingOptions.New, _ => QueueLayout());
#endif
        QueueLayout();
    }

    internal void AttachTitleHost(MauiControls.ContentView host)
    {
        _titleHost = host;
        host.BackgroundColor = Title.BackgroundColor;
        host.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
#if ANDROID
        if (host.Handler?.PlatformView is Android.Views.View nativeHost)
        {
            // Consume touches over covered rows without adding an accessibility action.
            nativeHost.Clickable = true;
            nativeHost.Focusable = false;
            nativeHost.ImportantForAccessibility = Android.Views.ImportantForAccessibility.No;
        }
#endif
        UpdateOffset(_offset);
    }

    internal double TitleOffset => BeanPageScrollGeometry.TitleOffset(_heroHeight, _offset);

    private void TitleSizeChanged(object? sender, EventArgs args)
    {
        var height = Math.Max(BeanPageScrollGeometry.MinimumTitleHeight, Title.Height);
        if (Math.Abs(height - _titleHeight) < 0.5)
            return;
        _titleHeight = height;
        UpdateOffset(_offset);
        QueueLayout();
    }

    private void LayoutChanged(object? sender, EventArgs args)
    {
#if IOS
        if (ReferenceEquals(sender, _list))
            _bodyHeight = null;
#endif
        QueueLayout();
    }

    private void QueueLayout()
    {
        if (_queued || _disposed || _list is null)
            return;
        _queued = true;
        _list.Dispatcher.Dispatch(() =>
        {
            _queued = false;
            if (!_disposed)
                ReadLayout();
        });
    }

    private void UpdateOffset(double offset)
    {
        _offset = offset;
        if (_titleHost is not null)
        {
            _titleHost.TranslationY = TitleOffset;
            _titleHost.Padding = new Thickness(0,
                BeanPageScrollGeometry.TitleSafePadding(_heroHeight, offset, _topInset), 0, 0);
        }
#if ANDROID
        _titleSpace.HeightRequest = BeanPageScrollGeometry.TitleSpaceHeight(
            _heroHeight, _titleHeight, offset, _topInset);
#endif
        StatusBarTint.HeightRequest = _topInset;
        StatusBarTint.Opacity = offset >= _heroHeight ? 1 : 0.65;
        _map.TranslationY = BeanPageScrollGeometry.ParallaxOffset(_heroHeight, offset);
    }

    private void ReadLayout(double delta = 0)
    {
        if (_native is null || _list is null)
            return;
#if ANDROID
        if (Header.Height <= 0)
            return;
        double? tail = null;
        if (_native.GetLayoutManager() is not AndroidX.RecyclerView.Widget.LinearLayoutManager manager)
            throw new InvalidOperationException("The Beans page requires its vertical collection layout.");
        var density = _native.Resources?.DisplayMetrics?.Density ?? 1;
        var insets = AndroidX.Core.View.ViewCompat.GetRootWindowInsets(_native);
        _topInset = (insets?.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars() |
                AndroidX.Core.View.WindowInsetsCompat.Type.DisplayCutout()).Top ?? 0) / density;
        UpdateHeroHeight();
        var belowTitle = Math.Max(0, Header.Height - _heroHeight - _titleSpace.Height);
        var header = manager.FindViewByPosition(0);
        UpdateOffset(header is not null
            ? (_native.PaddingTop - manager.GetDecoratedTop(header)) / density
            : _offset + delta / density);
        var itemCount = _native.GetAdapter()?.ItemCount ?? 0;
        var footer = itemCount > 0 ? manager.FindViewByPosition(itemCount - 1) : null;
        if (footer is not null)
        {
            var rows = Math.Max(0,
                (manager.GetDecoratedTop(footer) - _native.PaddingTop) / density + _offset - Header.Height);
            tail = BeanPageScrollGeometry.TailHeight(_list.Height, _titleSpace.HeightRequest, belowTitle + rows);
        }
        if (tail is { } height && Math.Abs(Footer.HeightRequest - height) > 0.5)
            Footer.HeightRequest = height;
#else
        _native.LayoutIfNeeded();
        ReadOffset();
        UpdateHeroHeight();
        var space = BeanPageScrollGeometry.TitleSpaceHeight(_heroHeight, _titleHeight, _offset, _topInset);
        // Reserve the measured title before waiting for a settled native body.
        if (_bodyHeight is null && Math.Abs(_titleSpace.HeightRequest - space) > 0.5)
            _titleSpace.HeightRequest = space;
        var header = _native.ViewWithTag(MauiHeaderTag);
        var footer = _native.ViewWithTag(MauiFooterTag);
        if (header is not null && footer is not null
            && Math.Abs(_titleSpace.Height - _titleSpace.HeightRequest) <= 0.5
            && BeanPageScrollGeometry.TryGetNativeBodyHeight(
                (double)_native.ContentSize.Height, (double)header.Frame.Height,
                (double)footer.Frame.Y, (double)footer.Frame.Height,
                Header.Height, Footer.HeightRequest, _heroHeight, _titleSpace.Height, out var bodyHeight))
            _bodyHeight = bodyHeight;

        if (_bodyHeight is not { } body)
            return;
        // Resize the spacer and its compensating tail from one settled native layout.
        var tail = BeanPageScrollGeometry.TailHeight((double)_native.Bounds.Height, space, body);
        // Grow before shrinking so an intermediate layout cannot clamp the live offset.
        if (tail > Footer.HeightRequest + 0.5)
            Footer.HeightRequest = tail;
        if (Math.Abs(_titleSpace.HeightRequest - space) > 0.5)
            _titleSpace.HeightRequest = space;
        if (Footer.HeightRequest > tail + 0.5)
            Footer.HeightRequest = tail;
#endif
    }

#if IOS
    private void ReadOffset()
    {
        if (_native is null)
            return;
        _topInset = (double)(_native.Window?.SafeAreaInsets.Top ?? 0);
        // Offset observation must not remeasure the collection's header/footer.
        UpdateOffset((double)(_native.ContentOffset.Y + _native.AdjustedContentInset.Top));
    }
#endif

#if ANDROID
    internal void SetPageVisible(bool visible) => _activity?.SetBeanMapVisible(visible);

    private void NativeLayoutChanged(object? sender, Android.Views.View.LayoutChangeEventArgs args) =>
        QueueLayout();

    private sealed class ScrollListener(BeanPageChrome owner) : AndroidX.RecyclerView.Widget.RecyclerView.OnScrollListener
    {
        public override void OnScrolled(AndroidX.RecyclerView.Widget.RecyclerView recyclerView, int dx, int dy) =>
            owner.ReadLayout(dy);
    }
#endif

    private void ListUnloaded(object? sender, EventArgs args) => DetachNative();

    private void DetachNative()
    {
#if ANDROID
        _activity?.SetBeanMapVisible(false);
        _activity = null;
        if (_native is not null)
        {
            if (_listener is not null)
                _native.RemoveOnScrollListener(_listener);
            _native.LayoutChange -= NativeLayoutChanged;
        }
        _listener?.Dispose();
        _listener = null;
#else
        _offsetObserver?.Dispose();
        _sizeObserver?.Dispose();
        _offsetObserver = _sizeObserver = null;
        _bodyHeight = null;
#endif
        _native = null;
    }

    private void Detach()
    {
        DetachNative();
        if (_list is not null)
        {
            _list.SizeChanged -= LayoutChanged;
            _list.Unloaded -= ListUnloaded;
        }
        _list = null;
        _map.BeanList = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (MauiControls.Application.Current is { } app)
            app.RequestedThemeChanged -= ThemeChanged;
        Detach();
        Title.SizeChanged -= TitleSizeChanged;
        Header.SizeChanged -= LayoutChanged;
        Footer.SizeChanged -= LayoutChanged;
        _titleHost = null;
    }
}
#endif
