using BaristaNotes.Core.Services.Workflows;
using CoreGraphics;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class MassViewController(
    SliceNavigationController host,
    string heading,
    MassPickerState state,
    Action<decimal> commit) : SliceViewController(host)
{
    private readonly ChoiceList _whole = new(numeric: true);
    private readonly ChoiceList _tenth = new(numeric: true);
    private readonly UILabel _title = SliceUi.Label(heading, 12, true, true);
    private readonly UILabel _description = SliceUi.Label("", 12, secondary: true);
    private readonly UILabel _rangeLabel = SliceUi.Label("", 11, true);
    private readonly UILabel _decimal = SliceUi.Label(".", 48, secondary: true);
    private readonly UILabel _unit = SliceUi.Label("g", 20, secondary: true);
    private UIButton? _toggle;
    private UIButton? _close;
    private UIButton? _done;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        SliceUi.TrackedText(_title, heading, 12, 3, NativeTheme.Secondary);
        _title.TextAlignment = UITextAlignment.Left;
        _rangeLabel.TextColor = NativeTheme.Primary;
        _rangeLabel.TextAlignment = UITextAlignment.Right;
        _description.IsAccessibilityElement = false;
        _rangeLabel.IsAccessibilityElement = false;
        _whole.AccessibilityIdentifier = "picker.whole";
        _tenth.AccessibilityIdentifier = "picker.tenth";
        _close = SliceUi.PickerAction("Close", "picker.close", () => Host.PopViewController(false));
        _done = SliceUi.PickerAction("Done", "picker.done", () => commit(state.DoneValue), primary: true);
        _toggle = SliceUi.Button("", "RangeScopeToggle", () =>
        {
            state.ToggleRange();
            Refresh(center: true);
        });
        _toggle.BackgroundColor = NativeTheme.TextPrimary.ColorWithAlpha(0.05f);
        Root.AddSubviews(_title, _close, _done, _description, _rangeLabel, _toggle, _whole, _tenth, _decimal, _unit);
        Refresh(center: false);
    }

    public override void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        Refresh(center: true);
    }

    private void Refresh(bool center)
    {
        _description.Text = state.RangeDescription;
        SliceUi.TrackedText(_rangeLabel, state.RangeToggleText, 11, 1, NativeTheme.Primary);
        if (_toggle != null)
            _toggle.AccessibilityLabel = $"{state.RangeDescription} {(state.ShowsFullRange ? "Show preferred range." : "Show full allowed range.")}";
        _whole.SetRows(state.WholeValues.Select(value => new ChoiceRow($"MassWhole_{value}", value.ToString(),
            value == state.SelectedWhole, () => { state.SelectWhole(value); Refresh(center: false); })).ToArray(), center);
        _tenth.SetRows(state.TenthValues.Select(value => new ChoiceRow($"MassTenth_{value}", value.ToString(),
            value == state.SelectedTenth, () => { state.SelectTenth(value); Refresh(center: false); })).ToArray(), center);
    }

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var safe = Root.SafeAreaLayoutGuide.LayoutFrame;
        var width = safe.Width;
        var top = safe.Top;
        var title = SliceUi.Measure(_title, width - 24);
        _title.Frame = new CGRect(safe.X + (width - title.Width) / 2, top + 8 + (44 - title.Height) / 2, title.Width, title.Height);
        if (_close != null) _close.Frame = new CGRect(safe.X + 12, top + 8, _close.SizeThatFits(CGSize.Empty).Width, 44);
        if (_done != null)
        {
            var doneWidth = _done.SizeThatFits(CGSize.Empty).Width;
            _done.Frame = new CGRect(safe.Right - 12 - doneWidth, top + 8, doneWidth, 44);
        }
        var rangeSize = SliceUi.Measure(_rangeLabel, width);
        _description.Frame = new CGRect(safe.X + 16, top + 60, width - 32 - rangeSize.Width, 64);
        _rangeLabel.Frame = new CGRect(safe.Right - 16 - rangeSize.Width, top + 60, rangeSize.Width, 64);
        if (_toggle != null) _toggle.Frame = new CGRect(safe.X, top + 60, width, 64);
        var bodyTop = top + 124;
        var body = SliceUi.PickerViewport(Root, bodyTop);
        var dot = SliceUi.Measure(_decimal, body.Width);
        var unit = SliceUi.Measure(_unit, body.Width);
        var columnWidth = (body.Width - dot.Width - unit.Width) / 2;
        _whole.Frame = new CGRect(body.X, body.Y, columnWidth, body.Height);
        _decimal.Frame = new CGRect(body.X + columnWidth, body.Y + (body.Height - dot.Height) / 2, dot.Width, dot.Height);
        _tenth.Frame = new CGRect(body.X + columnWidth + dot.Width, body.Y, columnWidth, body.Height);
        _unit.Frame = new CGRect(body.Right - unit.Width, body.Y + (body.Height - unit.Height) / 2, unit.Width, unit.Height);
    }
}
