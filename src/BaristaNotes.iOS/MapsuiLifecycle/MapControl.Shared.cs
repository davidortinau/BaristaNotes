// iOS-only adaptation of Mapsui 5.1.0 Mapsui.UI.Shared/MapControl.cs; see LICENSE.
using Mapsui;
using Mapsui.Disposing;
using Mapsui.Extensions;
using Mapsui.Fetcher;
using Mapsui.Layers;
using Mapsui.Logging;
using Mapsui.Manipulations;
using Mapsui.Rendering;
using Mapsui.Utilities;
using Mapsui.Widgets;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using Layers = Mapsui.Layers;
using LogLevel = Mapsui.Logging.LogLevel;

namespace BaristaNotes.Native.iOS.MapsuiLifecycle;

public partial class MapControl : INotifyPropertyChanged, IDisposable
{
    private readonly TapGestureTracker _tapGestureTracker = new();
    private readonly FlingTracker _flingTracker = new();
    private ScreenSize _mapControlScreenSize = new(0, 0);
    private RenderController? _renderController;
    private DisposableWrapper<Map>? _map;

    public int MaxTapGestureMovement { get; set; } = 8;
    public bool UseFling { get; set; } = true;
    public event EventHandler<MapInfoEventArgs>? Info;
    public event EventHandler<MapEventArgs>? MapTapped;
    public event EventHandler<MapEventArgs>? MapPointerPressed;
    public event EventHandler<MapEventArgs>? MapPointerMoved;
    public event EventHandler<MapEventArgs>? MapPointerReleased;

    private void SharedConstructor()
    {
        PlatformUtilities.SetOpenInBrowserFunc(OpenUrlInBrowser);
        Map = new Map();
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        // A final render-loop iteration must not recreate the map after SharedDispose.
        _renderController = new(() =>
        {
            lock (_lifetimeGate)
                return _disposed ? null : Map;
        }, InvalidateCanvas);
    }

    private void SharedOnSizeChanged(double width, double height)
    {
        _mapControlScreenSize = new ScreenSize(width, height);
        TryUpdateViewportSize();
    }

    public void SetMapRenderer(IMapRenderer mapRenderer)
    {
        if (_renderController is null)
            return;
        _renderController.SetMapRenderer(mapRenderer);
    }

    public void ForceUpdate() => InvalidateCanvas();
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Unsubscribe() => UnsubscribeFromMapEvents(Map);

    private void SubscribeToMapEvents(Map map)
    {
        map.DataChanged += Map_DataChanged;
        map.PropertyChanged += Map_PropertyChanged;
        map.RefreshGraphicsRequest += Map_RefreshGraphicsRequest;
    }

    private void Map_RefreshGraphicsRequest(object? sender, EventArgs e)
    {
        var request = (e as RefreshGraphicsEventArgs)?.Request;
        _renderController?.RefreshGraphics(request);
    }

    private void UnsubscribeFromMapEvents(Map map)
    {
        var localMap = map;
        localMap.DataChanged -= Map_DataChanged;
        localMap.PropertyChanged -= Map_PropertyChanged;
        localMap.RefreshGraphicsRequest -= Map_RefreshGraphicsRequest;
        localMap.AbortFetch();
    }

    public void Refresh(ChangeType changeType = ChangeType.Discrete) => Map.Refresh(changeType);
    public void RefreshGraphics() => _renderController?.RefreshGraphics();

    private void Map_DataChanged(object? sender, DataChangedEventArgs? e)
    {
        if (_disposed) return;
        try
        {
            if (sender is ILayer layer)
                _renderController?.UpdateDrawables(Map.Navigator.Viewport, layer, Map.RenderService);

            if (e == null)
                Logger.Log(LogLevel.Warning, "Unexpected error: DataChangedEventArgs can not be null");
            else if (e.Error is WebException)
                Logger.Log(LogLevel.Warning, $"A WebException occurred. Do you have internet? Exception: {e.Error?.Message}", e.Error);
            else if (e.Error != null)
                Logger.Log(LogLevel.Warning, $"An error occurred while fetching data. Exception: {e.Error?.Message}", e.Error);
            else
                RefreshGraphics();
        }
        catch (Exception exception)
        {
            Logger.Log(LogLevel.Warning, $"Unexpected exception in {nameof(Map_DataChanged)}", exception);
        }
    }

    private void Map_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed) return;
        if (e.PropertyName == nameof(Layers.Layer.Enabled))
            RefreshGraphics();
        else if (e.PropertyName == nameof(Layers.Layer.Opacity))
            RefreshGraphics();
        else if (e.PropertyName == nameof(Map.BackColor))
            RefreshGraphics();
        else if (e.PropertyName == nameof(Layers.Layer.DataSource))
            Refresh();
        else if (e.PropertyName == nameof(Map.Extent))
            Refresh();
        else if (e.PropertyName == nameof(Map.Layers))
            Refresh();
    }

    public Map Map
    {
        get
        {
            if (_map == null)
            {
                _map = new DisposableWrapper<Map>(new Map(), true);
                AfterSetMap(_map.WrappedObject);
                OnPropertyChanged();
            }
            return _map.WrappedObject;
        }
        set
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            BeforeSetMap();
            _map?.Dispose();
            _map = new DisposableWrapper<Map>(value, false);
            AfterSetMap(value);
            OnPropertyChanged();
        }
    }

    private void BeforeSetMap()
    {
        if (Map is null) return;
        UnsubscribeFromMapEvents(Map);
    }

    private void AfterSetMap(Map? map)
    {
        if (map is null) return;
        TryUpdateViewportSize();
        SubscribeToMapEvents(map);
        _renderController?.SetupDrawableFactory(map.RenderService);
        Refresh();
    }

    public void RefreshData(ChangeType changeType = ChangeType.Discrete) => Map.RefreshData(changeType);

    protected void OnMapInfo(MapInfoEventArgs mapInfoEventArgs)
    {
        Map?.OnMapInfo(mapInfoEventArgs);
        Info?.Invoke(this, mapInfoEventArgs);
    }

    public byte[] GetSnapshot(IEnumerable<ILayer>? layers = null, RenderFormat renderFormat = RenderFormat.Png, int quality = 100)
    {
        if (GetPixelDensity() is not float pixelDensity)
            throw new Exception("PixelDensity is not initialized");

        using var stream = _renderController?.RenderToBitmapStream(Map.Navigator.Viewport, layers ?? Map.Layers ?? [],
            Map.RenderService, pixelDensity: pixelDensity, renderFormat: renderFormat, quality: quality)
            ?? throw new ArgumentNullException(nameof(_renderController));
        return stream.ToArray();
    }

    private MapInfoEventArgs CreateMapInfoEventArgs(ScreenPosition screenPosition, MPoint worldPosition, GestureType gestureType) =>
        new(screenPosition, worldPosition, gestureType, Map, GetMapInfo, GetRemoteMapInfoAsync);

    public MapInfo GetMapInfo(ScreenPosition screenPosition, IEnumerable<ILayer> layers) =>
        _renderController?.GetMapInfo(screenPosition, Map.Navigator.Viewport, layers, Map.RenderService)
            ?? throw new ArgumentNullException(nameof(_renderController));

    protected Task<MapInfo> GetRemoteMapInfoAsync(ScreenPosition screenPosition, Viewport viewport, IEnumerable<ILayer> layers) =>
        RemoteMapInfoFetcher.GetRemoteMapInfoAsync(screenPosition, viewport, layers);

    private void TryUpdateViewportSize()
    {
        if (_mapControlScreenSize.Width <= 0 || _mapControlScreenSize.Height <= 0) return;
        if (Map is Map map)
        {
            var hadSize = map.Navigator.Viewport.HasSize();
            map.Navigator.SetSize(_mapControlScreenSize.Width, _mapControlScreenSize.Height);
            if (!hadSize && map.Navigator.Viewport.HasSize()) map.OnViewportSizeInitialized();
        }
    }

    private void SharedDispose(bool disposing)
    {
        if (disposing)
        {
            _renderController?.Dispose();
            _renderController = null;
            Unsubscribe();
            _map?.Dispose();
            _map = null;
        }
    }

    private bool OnWidgetTapped(ScreenPosition screenPosition, MPoint worldPosition, GestureType gestureType, bool shiftPressed)
    {
        var eventArgs = new WidgetEventArgs(screenPosition, worldPosition, gestureType, Map, shiftPressed, GetMapInfo, GetRemoteMapInfoAsync);
        var touchedWidgets = WidgetInput.GetWidgetsAtPosition(screenPosition, Map);
        foreach (var widget in touchedWidgets)
        {
            if (Logger.Settings.LogWidgetEvents)
                Logger.Log(LogLevel.Information, $"{nameof(OnWidgetTapped)} - {widget.GetType().Name} {nameof(GestureType)}: {gestureType} KeyState: {shiftPressed}");
            widget.OnTapped(eventArgs);
            if (eventArgs.Handled) return true;
        }
        return false;
    }

    private bool OnWidgetPointerPressed(ScreenPosition screenPosition, MPoint worldPosition, bool shiftPressed)
    {
        var eventArgs = new WidgetEventArgs(screenPosition, worldPosition, GestureType.Press, Map, shiftPressed, GetMapInfo, GetRemoteMapInfoAsync);
        foreach (var widget in WidgetInput.GetWidgetsAtPosition(screenPosition, Map))
        {
            if (Logger.Settings.LogWidgetEvents)
                Logger.Log(LogLevel.Information, $"{nameof(OnWidgetPointerPressed)} - {widget.GetType().Name}");
            widget.OnPointerPressed(eventArgs);
            if (eventArgs.Handled) return true;
        }
        return false;
    }

    private bool OnWidgetPointerMoved(ScreenPosition screenPosition, MPoint worldPosition, GestureType gestureType, bool shiftPressed)
    {
        var eventArgs = new WidgetEventArgs(screenPosition, worldPosition, gestureType, Map, shiftPressed, GetMapInfo, GetRemoteMapInfoAsync);
        foreach (var widget in WidgetInput.GetWidgetsAtPosition(screenPosition, Map))
        {
            if (Logger.Settings.LogWidgetEvents)
                Logger.Log(LogLevel.Information, $"{nameof(OnWidgetPointerMoved)} - {widget.GetType().Name}");
            widget.OnPointerMoved(eventArgs);
            if (eventArgs.Handled) return true;
        }
        return false;
    }

    private bool OnWidgetPointerReleased(ScreenPosition screenPosition, MPoint worldPosition, bool shiftPressed)
    {
        var eventArgs = new WidgetEventArgs(screenPosition, worldPosition, GestureType.Release, Map, shiftPressed, GetMapInfo, GetRemoteMapInfoAsync);
        foreach (var widget in WidgetInput.GetWidgetsAtPosition(screenPosition, Map))
        {
            if (Logger.Settings.LogWidgetEvents)
                Logger.Log(LogLevel.Information, $"{nameof(OnWidgetPointerReleased)} - {widget.GetType().Name}");
            widget.OnPointerReleased(eventArgs);
            if (eventArgs.Handled) return true;
        }
        return false;
    }

    private bool OnTapped(ScreenPosition screenPosition, GestureType gestureType)
    {
        var worldPosition = Map.Navigator.Viewport.ScreenToWorld(screenPosition);
        if (OnWidgetTapped(screenPosition, worldPosition, gestureType, GetShiftPressed())) return true;
        if (Map is null) return false;
        if (OnMapTapped(screenPosition, worldPosition, gestureType)) return true;
        OnMapInfo(CreateMapInfoEventArgs(screenPosition, worldPosition, gestureType));
        return false;
    }

    private bool OnPointerPressed(ReadOnlySpan<ScreenPosition> positions)
    {
        if (positions.Length != 1) return false;
        _flingTracker.Restart();
        _tapGestureTracker.Restart(positions[0]);
        var screenPosition = positions[0];
        var worldPosition = Map.Navigator.Viewport.ScreenToWorld(screenPosition);
        if (OnWidgetPointerPressed(screenPosition, worldPosition, GetShiftPressed())) return true;
        return OnMapPointerPressed(screenPosition, worldPosition);
    }

    private bool OnPointerMoved(ReadOnlySpan<ScreenPosition> screenPositions, bool isHovering)
    {
        if (screenPositions.Length != 1) return false;
        var gestureType = isHovering ? GestureType.Hover : GestureType.Drag;
        var screenPosition = screenPositions[0];
        var worldPosition = Map.Navigator.Viewport.ScreenToWorld(screenPosition);
        if (OnWidgetPointerMoved(screenPosition, worldPosition, gestureType, GetShiftPressed())) return true;
        if (OnMapPointerMoved(screenPosition, worldPosition, gestureType)) return true;
        if (!isHovering) _flingTracker.AddEvent(screenPosition, DateTime.Now.Ticks);
        return false;
    }

    private bool OnPointerReleased(ReadOnlySpan<ScreenPosition> screenPositions)
    {
        if (screenPositions.Length != 1) return false;
        if (GetPixelDensity() is not float pixelDensity) return false;
        var handled = false;
        var screenPosition = screenPositions[0];
        var worldPosition = Map.Navigator.Viewport.ScreenToWorld(screenPosition);
        if (OnWidgetPointerReleased(screenPosition, worldPosition, GetShiftPressed())) handled = true;
        if (!handled && OnMapPointerReleased(screenPosition, worldPosition)) handled = true;
        if (_tapGestureTracker.TapIfNeeded(screenPositions[0], MaxTapGestureMovement * pixelDensity, OnTapped)) handled = true;
        if (UseFling) _flingTracker.FlingIfNeeded((vX, vY) => Map.Navigator.Fling(vX, vY, 1000));
        if (!handled) Refresh();
        return handled;
    }

    protected virtual bool OnMapTapped(ScreenPosition screenPosition, MPoint worldPosition, GestureType gestureType)
    {
        if (Logger.Settings.LogMapEvents)
            Logger.Log(LogLevel.Information, $"{nameof(OnMapTapped)} - {nameof(GestureType)}: {gestureType}");
        var eventArgs = new MapEventArgs(screenPosition, worldPosition, gestureType, Map, GetMapInfo, GetRemoteMapInfoAsync);
        Map.OnTapped(eventArgs);
        if (!eventArgs.Handled) MapTapped?.Invoke(this, eventArgs);
        return eventArgs.Handled;
    }

    protected virtual bool OnMapPointerPressed(ScreenPosition screenPosition, MPoint worldPosition)
    {
        if (Logger.Settings.LogMapEvents)
            Logger.Log(LogLevel.Information, $"{nameof(OnMapPointerPressed)}");
        var eventArgs = new MapEventArgs(screenPosition, worldPosition, GestureType.Press, Map, GetMapInfo, GetRemoteMapInfoAsync);
        Map.OnPointerPressed(eventArgs);
        if (!eventArgs.Handled) MapPointerPressed?.Invoke(this, eventArgs);
        return eventArgs.Handled;
    }

    protected virtual bool OnMapPointerMoved(ScreenPosition screenPosition, MPoint worldPosition, GestureType gestureType)
    {
        if (Logger.Settings.LogMapEvents)
            Logger.Log(LogLevel.Information, $"{nameof(OnMapPointerMoved)} - {nameof(GestureType)}: {gestureType}");
        var eventArgs = new MapEventArgs(screenPosition, worldPosition, gestureType, Map, GetMapInfo, GetRemoteMapInfoAsync);
        Map.OnPointerMoved(eventArgs);
        if (!eventArgs.Handled) MapPointerMoved?.Invoke(this, eventArgs);
        return eventArgs.Handled;
    }

    protected virtual bool OnMapPointerReleased(ScreenPosition screenPosition, MPoint worldPosition)
    {
        if (Logger.Settings.LogMapEvents)
            Logger.Log(LogLevel.Information, $"{nameof(OnMapPointerReleased)}");
        var eventArgs = new MapEventArgs(screenPosition, worldPosition, GestureType.Release, Map, GetMapInfo, GetRemoteMapInfoAsync);
        Map.OnPointerReleased(eventArgs);
        if (!eventArgs.Handled) MapPointerReleased?.Invoke(this, eventArgs);
        return eventArgs.Handled;
    }

    private record ScreenSize(double Width, double Height);
}
