using System.Globalization;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.Workflows;
using CoreGraphics;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class NumericViewController : SliceViewController
{
    private readonly EffectiveDrinkValueRange _definition;
    private readonly Func<decimal, string> _format;
    private readonly Action<decimal> _commit;
    private readonly bool _useStagedValueAsOriginal;
    private readonly ChoiceList _values = new(numeric: true, scalesNumericText: true);
    private readonly string _heading;
    private readonly UILabel _title = new();
    private readonly UILabel _description = SliceUi.Label("", 12, secondary: true);
    private readonly UILabel _rangeLabel = SliceUi.Label("", 11, true);
    private readonly UIView _rangeBackground = new()
    {
        BackgroundColor = NativeTheme.TextPrimary.ColorWithAlpha(0.05f),
        UserInteractionEnabled = false
    };
    private NumericPickerState _state;
    private UIButton? _toggle;
    private UIButton? _close;
    private UIButton? _done;

    public NumericViewController(SliceNavigationController host, string heading,
        EffectiveDrinkValueRange definition, decimal original, Func<decimal, string> format,
        Action<decimal> commit, bool useStagedValueAsOriginal = false) : base(host)
    {
        _definition = definition;
        _state = new NumericPickerState(definition, original);
        _format = format;
        _commit = commit;
        _useStagedValueAsOriginal = useStagedValueAsOriginal;
        _heading = heading.ToUpperInvariant();
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        _description.IsAccessibilityElement = _rangeLabel.IsAccessibilityElement = false;
        _rangeLabel.TextAlignment = UITextAlignment.Right;
        _values.AccessibilityIdentifier = "picker.values";
        _close = SliceUi.PickerAction("Close", "picker.close", WeakUiCallback.Create(this, static owner =>
        {
            if (owner.Host.TopViewController == owner) owner.Host.PopViewController(false);
        }));
        _done = SliceUi.PickerAction("Done", "picker.done", WeakUiCallback.Create(this, static owner =>
        {
            if (owner.Host.TopViewController == owner) owner._commit(owner._state.DoneValue);
        }), primary: true);
        _toggle = SliceUi.Button("", "RangeScopeToggle", WeakUiCallback.Create(this, static owner =>
        {
            if (owner.Host.TopViewController != owner) return;
            owner._state.ToggleRange();
            owner.Refresh(center: true);
        }));
        Root.AddSubviews(_title, _close, _done, _rangeBackground, _toggle, _description, _rangeLabel, _values);
        if (OperatingSystem.IsIOSVersionAtLeast(17))
            RegisterForTraitChanges<UITraitPreferredContentSizeCategory>(static (environment, _) =>
                ((NumericViewController)environment).Refresh(center: true));
        Refresh(center: false);
    }

    public override void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        Refresh(center: true);
    }

    private void Select(decimal value)
    {
        if (Host.TopViewController != this) return;
        _state.Select(value);
        if (_useStagedValueAsOriginal)
        {
            // Source WaterTempPicker passes its staged value as NumericScroller's original on each render.
            _state = new NumericPickerState(_definition, _state.StagedValue, _state.StagedValue,
                _state.HasChanged, _state.ShowsFullRange);
        }
        // The source rebuilds its numeric CollectionView on selection and centers the new row.
        Refresh(center: true);
    }

    private void Refresh(bool center)
    {
        SourceScaledText.Tracked(_title, _heading, 12, 3, NativeTheme.Secondary, TraitCollection);
        _description.Font = SourceScaledText.Font(12, false, TraitCollection);
        if (_close != null) _close.TitleLabel.Font = SourceScaledText.Font(14, false, TraitCollection);
        if (_done != null) _done.TitleLabel.Font = SourceScaledText.Font(14, true, TraitCollection);
        _description.Text = _state.RangeDescription;
        _description.TextColor = _state.IsOutsidePreferredRange ? NativeTheme.Warning : NativeTheme.Secondary;
        SourceScaledText.Tracked(_rangeLabel, _state.RangeToggleText, 11, 1, NativeTheme.Primary, TraitCollection);
        if (_toggle != null)
            _toggle.AccessibilityLabel = $"{_state.RangeDescription} {(_state.ShowsFullRange ? "Show preferred range." : "Show full allowed range.")}";
        _values.SetRows(_state.Values.Select((value, index) => new ChoiceRow(
            // Preserve exact off-step values in IDs, even when the displayed label rounds them.
            $"NumericValue_{value.ToString("G29", CultureInfo.InvariantCulture)}",
            _format(value), index == _state.SelectedIndex,
            WeakUiCallback.Create(this, value, static (owner, selected) => owner.Select(selected)))).ToArray(), center, TraitCollection);
        Root.SetNeedsLayout();
    }

    // The deployment target includes iOS 15/16, before trait registrations were available.
#pragma warning disable CS0672, CA1422
    public override void TraitCollectionDidChange(UITraitCollection? previousTraitCollection)
    {
        base.TraitCollectionDidChange(previousTraitCollection);
        if (!OperatingSystem.IsIOSVersionAtLeast(17) && IsViewLoaded &&
            previousTraitCollection?.PreferredContentSizeCategory != TraitCollection.PreferredContentSizeCategory)
            Refresh(center: true);
    }
#pragma warning restore CS0672, CA1422

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var safe = Root.SafeAreaLayoutGuide.LayoutFrame;
        var actionHeight = (nfloat)Math.Max(44, Math.Max(
            _close?.SizeThatFits(CGSize.Empty).Height ?? 44, _done?.SizeThatFits(CGSize.Empty).Height ?? 44));
        var title = SliceUi.Measure(_title, safe.Width - 24);
        var headerHeight = (nfloat)Math.Max(actionHeight, title.Height);
        _title.Frame = new CGRect(safe.X + (safe.Width - title.Width) / 2,
            safe.Top + 8 + (headerHeight - title.Height) / 2, title.Width, title.Height);
        if (_close != null)
            _close.Frame = new CGRect(safe.X + 12, safe.Top + 8,
                (nfloat)Math.Ceiling(_close.SizeThatFits(CGSize.Empty).Width), headerHeight);
        if (_done != null)
        {
            var width = (nfloat)Math.Ceiling(_done.SizeThatFits(CGSize.Empty).Width);
            _done.Frame = new CGRect(safe.Right - 12 - width, safe.Top + 8, width, headerHeight);
        }
        var rangeTop = safe.Top + headerHeight + 16;
        var range = SliceUi.Measure(_rangeLabel, safe.Width);
        var descriptionWidth = (nfloat)Math.Max(1, safe.Width - 32 - range.Width);
        var rangeHeight = (nfloat)Math.Max(44, Math.Max(range.Height, SliceUi.Measure(_description, descriptionWidth).Height));
        _description.Frame = new CGRect(safe.X + 16, rangeTop + 10, descriptionWidth, rangeHeight);
        _rangeLabel.Frame = new CGRect(safe.Right - 16 - range.Width, rangeTop + 10, range.Width, rangeHeight);
        _rangeBackground.Frame = new CGRect(safe.X, rangeTop, safe.Width, rangeHeight + 20);
        if (_toggle != null) _toggle.Frame = new CGRect(safe.X + 16, rangeTop + 10, safe.Width - 32, rangeHeight);
        _values.Frame = SliceUi.PickerViewport(Root, rangeTop + rangeHeight + 20);
    }
}
