using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using CoreGraphics;
using MapControl = BaristaNotes.Native.iOS.MapsuiLifecycle.MapControl;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class BeanMapView : UIView
{
    private readonly UILabel _error = new() { Lines = 0 };
    private readonly UIView _mapSurface = new() { ClipsToBounds = true };
    private readonly UIButton _reset;
    private readonly UIButton _retry;
    private Action? _selectionChanged;
    private Action? _originsChanged;
    private BeanMapSession? _session;
    private MapControl? _map;
    private string? _controlError;
    private string? _originError;
    private bool _disposed;
    private bool _loadingOrigins = true;
    public bool HasSelection => _session?.HasSelection == true;
    public IReadOnlyList<BeanDto> SelectedBeans => _session?.SelectedBeans ?? [];
    public string SelectionLabel => _session?.SelectionLabel ?? "";

    public BeanMapView(ILogger logger, IOriginGeocoder geocoder, Action selectionChanged, Action originsChanged)
    {
        AccessibilityIdentifier = "beans.map";
        BackgroundColor = NativeTheme.Surface;
        _selectionChanged = selectionChanged;
        _originsChanged = originsChanged;
        _reset = SliceUi.PickerAction("All beans", "beans.map.all",
            WeakUiCallback.Create(this, static owner => owner._session?.SelectCountries([])));
        _reset.Hidden = true;
        _retry = SliceUi.PickerAction("Retry origins", "beans.map.retry",
            WeakUiCallback.Create(this, static owner => owner._session?.RetryOrigins()));
        _retry.Hidden = true;
        _error.TextColor = NativeTheme.Error;
        _error.AccessibilityIdentifier = "beans.map.error";
        _error.Hidden = true;
        try
        {
            _session = new BeanMapSession(logger, geocoder: geocoder, dispatch: NativeUiThread.Send);
            _session.Changed += OnMapChanged;
            _session.SelectionChanged += OnSelectionChanged;
            _session.OriginsChanged += OnOriginsChanged;
            _map = new MapControl(CGRect.Empty)
            {
                Map = _session.Map, IsAccessibilityElement = true, AccessibilityLabel = "Bean origins"
            };
            _mapSurface.AddSubview(_map);
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
        _mapSurface.AddSubview(_reset);
        _mapSurface.AddSubview(_retry);
        AddSubviews(_mapSurface, _error);
        RenderError();
    }

    public void CancelOriginResolution() => _session?.CancelOriginResolution();

    public void SetBeans(IEnumerable<BeanDto> beans)
    {
        _originError = null;
        _loadingOrigins = false;
        _session?.SetBeans(beans);
        RenderError();
    }

    public nfloat MeasureFormerFooter(nfloat width)
    {
        // No summary view is retained or laid out; this only preserves the old map edge.
        using var summary = new UILabel
        {
            Text = _session?.FormerFooterStatus, Lines = 0, Font = SourceScaledText.Font(12, false, TraitCollection)
        };
        using var action = new UILabel { Text = "All beans", Font = SourceScaledText.Font(14, false, TraitCollection) };
        return (nfloat)Math.Max(44, SliceUi.Measure(action, width / 2).Height + 16) +
            (_session == null ? 0 : SliceUi.Measure(summary, (nfloat)Math.Max(0, width - 24)).Height + 4);
    }

    public void ReportOriginError()
    {
        _originError = "Saved origins could not load. The bean list is still available.";
        _loadingOrigins = false;
        RenderError();
    }

    public bool BlocksPageScrollAt(CGPoint point) => _map != null && _mapSurface.Frame.Contains(point);

    public void SetParallax(nfloat offset)
    {
        if (_map != null) _map.Transform = CGAffineTransform.MakeTranslation(0,
            (nfloat)Math.Clamp(offset, 0, Bounds.Height) * .2f);
    }

    private void OnMapChanged(object? sender, EventArgs args) => NativeUiThread.Send(() =>
    {
        if (!_disposed && ReferenceEquals(sender, _session)) RenderError();
    });

    private void OnSelectionChanged(object? sender, EventArgs args) => NativeUiThread.Send(() =>
    {
        if (!_disposed && ReferenceEquals(sender, _session)) _selectionChanged?.Invoke();
    });

    private void OnOriginsChanged(object? sender, EventArgs args) => NativeUiThread.Send(() =>
    {
        if (!_disposed && ReferenceEquals(sender, _session)) _originsChanged?.Invoke();
    });

    private void RenderError()
    {
        _reset.Hidden = !HasSelection;
        _retry.Hidden = _session?.HasGeocodeFailure != true;
        var error = string.Join(" ", new[] { _controlError, _originError, _session?.Error }.OfType<string>());
        _error.Text = error.Length > 0 ? error : _loadingOrigins ? "Loading saved origins..." : "";
        _error.TextColor = error.Length > 0 ? NativeTheme.Error : NativeTheme.Secondary;
        if (_map != null) _map.AccessibilityValue = _session?.OriginStatus;
        _error.Hidden = string.IsNullOrEmpty(_error.Text);
        SetNeedsLayout();
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        _reset.TitleLabel.Font = SourceScaledText.Font(14, false, TraitCollection);
        _reset.SetTitleColor(NativeTheme.TextPrimary, UIControlState.Normal);
        _reset.BackgroundColor = NativeTheme.Surface;
        _retry.TitleLabel.Font = SourceScaledText.Font(14, false, TraitCollection);
        _retry.SetTitleColor(NativeTheme.TextPrimary, UIControlState.Normal);
        _retry.BackgroundColor = NativeTheme.Surface;
        _error.Font = SourceScaledText.Font(12, false, TraitCollection);
        var controlsHeight = (nfloat)Math.Max(44, SliceUi.Measure(_reset.TitleLabel, Bounds.Width / 2).Height + 16);
        var errorHeight = _error.Hidden ? 0 : SliceUi.Measure(_error, (nfloat)Math.Max(0, Bounds.Width - 24)).Height + 8;
        var mapHeight = (nfloat)Math.Max(0, Bounds.Height - errorHeight);
        _mapSurface.Frame = new CGRect(0, 0, Bounds.Width, mapHeight);
        if (_map != null && _map.Bounds.Size != _mapSurface.Bounds.Size)
        {
            var transform = _map.Transform;
            _map.Transform = CGAffineTransform.MakeIdentity();
            _map.Frame = _mapSurface.Bounds;
            _map.Transform = transform;
        }
        _error.Frame = new CGRect(12, mapHeight + 4, (nfloat)Math.Max(0, Bounds.Width - 24), (nfloat)Math.Max(0, errorHeight - 8));
        var resetWidth = (nfloat)Math.Max(88, SliceUi.Measure(_reset.TitleLabel, Bounds.Width / 2).Width + 24);
        _reset.Frame = new CGRect(12, (nfloat)Math.Max(0, mapHeight - controlsHeight - 24), resetWidth, controlsHeight);
        var retryWidth = SliceUi.Measure(_retry.TitleLabel, Bounds.Width / 2).Width + 24;
        var retryHeight = (nfloat)Math.Max(44, SliceUi.Measure(_retry.TitleLabel, Bounds.Width / 2).Height + 16);
        _retry.Frame = new CGRect(12, (nfloat)Math.Max(0,
            mapHeight - 24 - retryHeight - (_reset.Hidden ? 0 : controlsHeight + 8)), retryWidth, retryHeight);
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
            _selectionChanged = null;
            _originsChanged = null;
            _map?.RemoveFromSuperview();
            _map?.Dispose();
            _session?.Dispose();
            _map = null;
            _session = null;
        }
        base.Dispose(disposing);
    }
}
