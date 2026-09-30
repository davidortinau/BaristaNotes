using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Workflows;
using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class RangeSettingsViewController : RangePageViewController
{
    private readonly DrinkValueMetric _metric;
    private readonly IDrinkValueRangeService _ranges;
    private readonly UILabel _help = SliceUi.Label("", 14);
    private readonly UILabel _warning = SliceUi.Label("", 14);
    private readonly Dictionary<BrewMethod, RangeLinkTile> _methods = [];
    private RangeTextTile? _warningTile;
    private ModeRow? _modeRow;
    private UIButton? _reset;
    private bool _visible;
    private bool _promptOpen;

    public RangeSettingsViewController(SliceNavigationController host, DrinkValueMetric metric)
        : base(host, "VALUE RANGES", $"{DrinkValueRangeFormatting.MetricTitle(metric)} Ranges", "range.methods")
    {
        _metric = metric;
        _ranges = Services.Singleton<IDrinkValueRangeService>();
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        var modeCaption = new UILabel();
        SliceUi.TrackedText(modeCaption, "MODE", 12, 2, NativeTheme.Secondary);
        AddTile(new RangeTextTile(4, 14, 0, modeCaption, _help));
        _modeRow = new ModeRow(SelectMode);
        AddTile(_modeRow);
        _warning.TextColor = NativeTheme.Surface;
        _warningTile = new RangeTextTile(0, 12, 0, _warning)
        {
            BackgroundColor = UIColor.FromRGB(0xFF, 0xA7, 0x26),
            AccessibilityIdentifier = "range.warning",
            Hidden = true
        };
        AddTile(_warningTile);
        foreach (var method in BrewMethodExtensions.All)
        {
            var tile = new RangeLinkTile($"RangeMethod_{method}", () =>
            {
                if (_ranges.GetMode(_metric) == ValueRangeMode.Custom && Host.TopViewController == this)
                    Host.PushViewController(new RangeEditorViewController(Host, _metric, method), true);
            });
            _methods.Add(method, tile);
            AddTile(tile);
        }
        _reset = new ResetButton(() => ConfirmReset());
        AddTile(_reset);
        AddTile(new RangeTextTile(0, 8, 16) { BackgroundColor = NativeTheme.Outline });
        AddAction(SliceUi.FormAction("BACK", "range.back", () =>
        {
            if (!_promptOpen) Host.RequestBack();
        }));
    }

    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        _visible = true;
        _ranges.SettingsChanged += OnSettingsChanged;
        Reload();
    }

    public override void ViewDidDisappear(bool animated)
    {
        _visible = false;
        _ranges.SettingsChanged -= OnSettingsChanged;
        base.ViewDidDisappear(animated);
    }

    private void OnSettingsChanged(object? sender, EventArgs args) => BeginInvokeOnMainThread(() =>
    {
        if (_visible) Reload();
    });

    private void Reload()
    {
        try
        {
            var snapshot = _ranges.GetSettings();
            var mode = snapshot.Modes.GetValueOrDefault(_metric, ValueRangeMode.Auto);
            _help.Text = mode == ValueRangeMode.Auto
                ? "Uses recommended ranges for each drink method."
                : "Edited methods use custom ranges. Other methods stay automatic.";
            _modeRow?.Select(mode);
            _warning.Text = snapshot.LoadWarning;
            if (_warningTile != null) _warningTile.Hidden = snapshot.LoadWarning == null;
            if (_reset != null) _reset.Hidden = !snapshot.Overrides.Any(item => item.Metric == _metric);
            foreach (var (method, tile) in _methods)
            {
                var effective = _ranges.Resolve(_metric, method);
                var source = effective.Source switch
                {
                    ValueRangeSource.Custom => "CUSTOM",
                    ValueRangeSource.AutoFallback => "AUTO FALLBACK",
                    _ => "AUTO"
                };
                var range = DrinkValueRangeFormatting.FormatRange(_metric, effective.Range);
                tile.Update(method.DisplayName().ToUpperInvariant(), range, source,
                    mode == ValueRangeMode.Custom, effective.Source == ValueRangeSource.Custom,
                    $"{method.DisplayName()}. {range}. {source}.");
            }
            Root.SetNeedsLayout();
        }
        catch (Exception exception) { ShowFailure(exception); }
    }

    private void SelectMode(ValueRangeMode mode)
    {
        if (_promptOpen) return;
        try { _ranges.SetMode(_metric, mode); }
        catch (Exception exception) { ShowFailure(exception); }
    }

    private void ConfirmReset()
    {
        if (_promptOpen || !_ranges.GetSettings().Overrides.Any(item => item.Metric == _metric)) return;
        _promptOpen = true;
        var title = DrinkValueRangeFormatting.MetricTitle(_metric).ToLowerInvariant();
        var alert = UIAlertController.Create("Reset custom ranges?",
            $"Remove all custom {title} ranges?", UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, _ => _promptOpen = false));
        alert.AddAction(UIAlertAction.Create("Reset", UIAlertActionStyle.Default, _ =>
        {
            _promptOpen = false;
            try { _ranges.ResetOverrides(_metric); }
            catch (Exception exception) { ShowFailure(exception); }
        }));
        PresentViewController(alert, true, null);
    }

    private void ShowFailure(Exception exception)
    {
        Logger.LogError(exception, "Failed to read or update {Metric} range settings", _metric);
        _warning.Text = exception.Message;
        if (_warningTile != null) _warningTile.Hidden = false;
        Root.SetNeedsLayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _visible = false;
            _ranges.SettingsChanged -= OnSettingsChanged;
        }
        base.Dispose(disposing);
    }

    private sealed class ResetButton : UIButton
    {
        public ResetButton(Action action)
        {
            AccessibilityIdentifier = "range.reset-all";
            SetTitle("RESET ALL CUSTOM RANGES", UIControlState.Normal);
            TitleLabel.Font = NativeTheme.Font(14, true);
            SetTitleColor(NativeTheme.Error, UIControlState.Normal);
            SetAttributedTitle(new NSAttributedString("RESET ALL CUSTOM RANGES", new UIStringAttributes
            {
                Font = TitleLabel.Font, ForegroundColor = NativeTheme.Error, KerningAdjustment = 1
            }), UIControlState.Normal);
            BackgroundColor = NativeTheme.Surface;
            TouchUpInside += (_, _) => action();
        }
        public override CoreGraphics.CGSize SizeThatFits(CoreGraphics.CGSize size) => new(size.Width, 56);
    }

    private sealed class ModeRow : UIView
    {
        private readonly UIButton _auto;
        private readonly UIButton _custom;
        public ModeRow(Action<ValueRangeMode> select)
        {
            BackgroundColor = NativeTheme.Outline;
            _auto = SliceUi.Button("AUTO", "RangeMode_Auto", () => select(ValueRangeMode.Auto));
            _custom = SliceUi.Button("CUSTOM", "RangeMode_Custom", () => select(ValueRangeMode.Custom));
            _auto.TitleLabel.Font = _custom.TitleLabel.Font = NativeTheme.Font(14, true);
            AddSubviews(_auto, _custom);
        }
        public void Select(ValueRangeMode mode)
        {
            foreach (var (button, selected) in new[] { (_auto, mode == ValueRangeMode.Auto), (_custom, mode == ValueRangeMode.Custom) })
            {
                button.BackgroundColor = selected ? NativeTheme.TextPrimary : NativeTheme.Surface;
                var foreground = selected ? NativeTheme.Surface : NativeTheme.TextPrimary;
                button.SetTitleColor(foreground, UIControlState.Normal);
                button.SetAttributedTitle(new NSAttributedString(button.CurrentTitle ?? "", new UIStringAttributes
                {
                    Font = button.TitleLabel.Font, ForegroundColor = foreground, KerningAdjustment = 2
                }), UIControlState.Normal);
                button.AccessibilityTraits = UIAccessibilityTrait.Button |
                    (selected ? UIAccessibilityTrait.Selected : UIAccessibilityTrait.None);
            }
        }
        public override CoreGraphics.CGSize SizeThatFits(CoreGraphics.CGSize size) => new(size.Width, 56);
        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            var width = (Bounds.Width - 1) / 2;
            _auto.Frame = new CoreGraphics.CGRect(0, 0, width, Bounds.Height);
            _custom.Frame = new CoreGraphics.CGRect(width + 1, 0, width, Bounds.Height);
        }
    }
}
