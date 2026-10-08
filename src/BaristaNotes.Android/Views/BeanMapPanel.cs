using Android.Views;
using Android.Widget;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using Mapsui.UI.Android;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class BeanMapPanel : LinearLayout
{
    private readonly NativeStyle _style;
    private readonly TextView _error;
    private readonly FrameLayout _mapSurface;
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private BeanMapSession? _session;
    private MapControl? _map;
    private bool _disposed;
    private string? _controlError;
    private string? _originError;
    private bool _loadingOrigins = true;
    public Button ResetButton { get; }
    public Button RetryButton { get; }
    public bool HasSelection => _session?.HasSelection == true;
    public bool WaitingForSelection => _originError == null && _session?.HasPendingSelection == true;
    public IReadOnlyList<BeanDto> SelectedBeans => _session?.SelectedBeans ?? [];
    public string SelectionLabel => _session?.SelectionLabel ?? "";
    public event EventHandler? SelectionChanged;
    public event EventHandler? OriginsChanged;

    public BeanMapPanel(NativeStyle style, ILogger logger, IOriginGeocoder geocoder, BeanMapState? state = null) : base(style.Context)
    {
        _style = style;
        Orientation = Orientation.Vertical;
        SetBackgroundColor(style.Surface);
        NativeStyle.Identify(this, "BeanMap");
        _error = style.Label("", 12, color: style.Error);
        _error.SetPadding(style.Dp(12), style.Dp(4), style.Dp(12), style.Dp(4));
        _error.Visibility = ViewStates.Gone;
        _error.AccessibilityLiveRegion = AccessibilityLiveRegion.Polite;
        NativeStyle.Identify(_error, "BeanMapError");
        _mapSurface = new FrameLayout(style.Context);
        _mapSurface.SetClipChildren(true);
        AddView(_mapSurface, new LayoutParams(-1, 0, 1));
        try
        {
            if (_uiContext == null) throw new InvalidOperationException("The map requires the Android UI synchronization context.");
            _session = new BeanMapSession(logger, state, geocoder, action => _uiContext.Post(_ => action(), null));
            _session.Changed += OnMapChanged;
            _session.SelectionChanged += OnSelectionChanged;
            _session.OriginsChanged += OnOriginsChanged;
            _map = new MapControl(style.Context, null!) { Map = _session.Map };
            style.FixTheme(_map);
            _mapSurface.AddView(_map, new FrameLayout.LayoutParams(-1, -1));
        }
        catch (Exception error)
        {
            logger.LogError(error, "Native bean map initialization failed");
            _map?.Dispose();
            _map = null;
            if (_session != null)
            {
                _session.Changed -= OnMapChanged;
                _session.SelectionChanged -= OnSelectionChanged;
                _session.OriginsChanged -= OnOriginsChanged;
                _session.Dispose();
                _session = null;
            }
            _controlError = "Map could not start. The bean list is still available.";
        }
        AddView(_error, new LayoutParams(-1, -2));
        ResetButton = style.Button("All beans", "BeanMapAllBeans", style.Text);
        ResetButton.SetBackgroundColor(style.Surface);
        ResetButton.Visibility = ViewStates.Gone;
        RetryButton = style.Button("Retry origins", "BeanMapRetryOrigins", style.Text);
        RetryButton.SetBackgroundColor(style.Surface);
        RetryButton.Visibility = ViewStates.Gone;
        var actions = new LinearLayout(style.Context) { Orientation = Orientation.Vertical };
        actions.AddView(RetryButton, new LayoutParams(-2, -2));
        actions.AddView(ResetButton, new LayoutParams(-2, -2));
        _mapSurface.AddView(actions, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Bottom | GravityFlags.Start)
        {
            LeftMargin = style.Dp(12), BottomMargin = style.Dp(24)
        });
        RenderStatus();
    }

    public BeanMapState? CaptureState() => _session?.CaptureState();
    public void ClearSelection() => _session?.SelectCountries([]);
    public void RetryOrigins() => _session?.RetryOrigins();

    public int MeasureFormerFooter(int width)
    {
        // Measure the removed footer offscreen only to preserve the old map edge.
        using var summary = new TextView(_style.Context) { Text = _session?.FormerFooterStatus, Typeface = _style.Regular };
        summary.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
        summary.SetIncludeFontPadding(true);
        summary.Measure(MeasureSpec.MakeMeasureSpec(Math.Max(0, width - _style.Dp(24)), MeasureSpecMode.Exactly),
            MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
        ResetButton.Measure(MeasureSpec.MakeMeasureSpec(width, MeasureSpecMode.AtMost),
            MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
        return Math.Max(_style.Dp(44), ResetButton.MeasuredHeight) +
            (_session == null ? 0 : summary.MeasuredHeight + _style.Dp(4));
    }

    public void SetBeans(IEnumerable<BeanDto> beans)
    {
        _originError = null;
        _loadingOrigins = false;
        _session?.SetBeans(beans);
        RenderStatus();
    }

    public void ReportOriginError()
    {
        _originError = "Saved origins could not load. The bean list is still available.";
        _loadingOrigins = false;
        RenderStatus();
    }

    public void SetParallax(int offset)
    {
        if (_map != null) _map.TranslationY = Math.Clamp(offset, 0, Height) * .2f;
    }

    public override bool DispatchTouchEvent(MotionEvent? e)
    {
        if (e?.ActionMasked == MotionEventActions.Down && _map != null && e.GetY() < _mapSurface.Bottom)
            Parent?.RequestDisallowInterceptTouchEvent(true);
        var handled = base.DispatchTouchEvent(e);
        if (e?.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel)
            Parent?.RequestDisallowInterceptTouchEvent(false);
        return handled;
    }

    private void OnMapChanged(object? sender, EventArgs args) => _uiContext?.Post(_ =>
    {
        if (_disposed || !ReferenceEquals(sender, _session)) return;
        RenderStatus();
    }, null);

    private void OnSelectionChanged(object? sender, EventArgs args) => _uiContext?.Post(_ =>
    {
        if (!_disposed && ReferenceEquals(sender, _session)) SelectionChanged?.Invoke(this, EventArgs.Empty);
    }, null);

    private void OnOriginsChanged(object? sender, EventArgs args) => _uiContext?.Post(_ =>
    {
        if (!_disposed && ReferenceEquals(sender, _session)) OriginsChanged?.Invoke(this, EventArgs.Empty);
    }, null);

    private void RenderStatus()
    {
        if (_disposed) return;
        ResetButton.Visibility = HasSelection ? ViewStates.Visible : ViewStates.Gone;
        RetryButton.Visibility = _session?.HasGeocodeFailure == true ? ViewStates.Visible : ViewStates.Gone;
        var error = string.Join(" ", new[] { _controlError, _originError, _session?.Error }.OfType<string>());
        _error.Text = error.Length > 0 ? error : _loadingOrigins ? "Loading saved origins..." : "";
        _error.SetTextColor(error.Length > 0 ? _style.Error : _style.Secondary);
        if (_map != null) _map.ContentDescription = _session?.OriginStatus;
        _error.Visibility = string.IsNullOrEmpty(_error.Text) ? ViewStates.Gone : ViewStates.Visible;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            if (_session != null)
            {
                _session.Changed -= OnMapChanged;
                _session.SelectionChanged -= OnSelectionChanged;
                _session.OriginsChanged -= OnOriginsChanged;
            }
            SelectionChanged = null;
            OriginsChanged = null;
            _map?.Dispose();
            _session?.Dispose();
            _map = null;
            _session = null;
        }
        base.Dispose(disposing);
    }
}
