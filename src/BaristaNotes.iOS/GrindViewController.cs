using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.Workflows;
using CoreGraphics;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class GrindViewController : SliceViewController
{
    private readonly GrindPickerState _state;
    private readonly GrindPickerLoadResult _loaded;
    private readonly int? _grinderId;
    private readonly Action<int> _commit;
    private readonly Action _selectGrinder;
    private readonly EquipmentRows _rows = new(EquipmentRowStyle.Grind, "grind.values");
    private readonly UILabel _title = new();
    private readonly UILabel _description = new() { Lines = 0 };
    private readonly UILabel _scopeLabel = new();
    private readonly UIView _scopeBackground = new() { BackgroundColor = NativeTheme.TextPrimary.ColorWithAlpha(0.05f), UserInteractionEnabled = false };
    private readonly UIView _badge = new() { BackgroundColor = NativeTheme.TextPrimary.ColorWithAlpha(0.05f) };
    private readonly UILabel _badgeText = new() { Lines = 0 };
    private readonly UILabel _badgeArrow = new();
    private UIButton? _close;
    private UIButton? _done;
    private UIButton? _scope;
    private UIButton? _badgeAction;

    public GrindViewController(SliceNavigationController host, EffectiveDrinkValueRange definition,
        int? original, GrindPickerLoadResult loaded, int? grinderId,
        Action<int> commit, Action selectGrinder) : base(host)
    {
        _state = new GrindPickerState(definition, original, loaded.Microns);
        _loaded = loaded;
        _grinderId = grinderId;
        _commit = commit;
        _selectGrinder = selectGrinder;
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        _close = SliceUi.PickerAction("Close", "grind.close", WeakUiCallback.Create(this, static owner =>
        {
            if (owner.Host.TopViewController == owner) owner.Host.PopViewController(false);
        }));
        _done = SliceUi.PickerAction("Done", "grind.done", WeakUiCallback.Create(this, static owner =>
        {
            if (owner.Host.TopViewController == owner) owner._commit(owner._state.DoneValue);
        }), primary: true);
        _scope = SliceUi.Button("", "GrindRangeScopeToggle", WeakUiCallback.Create(this, static owner =>
        {
            if (owner.Host.TopViewController != owner) return;
            owner._state.ToggleRange();
            owner.Render(true);
        }));
        _description.IsAccessibilityElement = _scopeLabel.IsAccessibilityElement = false;
        _badgeText.TextColor = NativeTheme.TextPrimary;
        _badgeArrow.Text = "→";
        _badgeArrow.TextColor = NativeTheme.Primary;
        _badgeAction = SliceUi.Button("", "grind.badge", WeakUiCallback.Create(this, static owner => owner.ActivateBadge()));
        _badge.AddSubviews(_badgeText, _badgeArrow, _badgeAction);
        Root.AddSubviews(_title, _close, _done, _scopeBackground, _description, _scopeLabel, _scope, _rows, _badge);
        if (OperatingSystem.IsIOSVersionAtLeast(17))
            RegisterForTraitChanges<UITraitPreferredContentSizeCategory>(static (environment, _) =>
                ((GrindViewController)environment).Render(true));
        Render(false);
    }

    public override void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        Render(true);
    }

    private void ActivateBadge()
    {
        if (Host.TopViewController != this) return;
        if (!_grinderId.HasValue)
        {
            Host.PopViewController(false);
            _selectGrinder();
        }
        else if (_loaded.Anchors is null || _loaded.Anchors.Count < 2)
        {
            // The source calls an unregistered equipmentDetail route here. Do not
            // turn that gap into an invented calibration editor or fake success.
            Logger.LogWarning("Scale setup unavailable: source equipmentDetail route is not registered");
            Host.PopViewController(false);
            ShowFeedback("Scale setup is not available in the source workflow.");
        }
    }

    private void Select(int value)
    {
        if (Host.TopViewController != this) return;
        _state.Select(value);
        Render(true);
    }

    private void Render(bool center)
    {
        SourceScaledText.Tracked(_title, "GRIND", 12, 3, NativeTheme.Secondary, TraitCollection);
        _description.Font = SourceScaledText.Font(12, false, TraitCollection);
        _description.Text = _state.RangeDescription;
        _description.TextColor = _state.IsOutsidePreferredRange ? NativeTheme.Warning : NativeTheme.Secondary;
        SourceScaledText.Tracked(_scopeLabel, _state.RangeToggleText, 11, 1, NativeTheme.Primary, TraitCollection);
        if (_scope != null)
            _scope.AccessibilityLabel = $"{_state.RangeDescription} {(_state.ShowsFullRange ? "Show preferred range." : "Show full allowed range.")}";
        if (_close != null) _close.TitleLabel.Font = SourceScaledText.Font(14, false, TraitCollection);
        if (_done != null) _done.TitleLabel.Font = SourceScaledText.Font(14, true, TraitCollection);
        _badgeText.Font = SourceScaledText.Font(16, true, TraitCollection);
        _badgeArrow.Font = SourceScaledText.Font(20, false, TraitCollection);
        _badgeText.Text = DrinkDisplay.GrindBadge(_grinderId, _loaded.GrinderName, _loaded.Anchors, _state.StagedValue);
        var actionable = !_grinderId.HasValue || _loaded.Anchors is null || _loaded.Anchors.Count < 2;
        _badgeArrow.Hidden = !actionable;
        if (_badgeAction != null)
        {
            _badgeAction.Hidden = !actionable;
            _badgeAction.AccessibilityLabel = _badgeText.Text;
        }
        _badgeText.IsAccessibilityElement = !actionable;
        _rows.Update(_state.Values.Select(value => new EquipmentRow($"GrindValue_{value}", $"{value} µm",
            WeakUiCallback.Create(this, value, static (owner, selected) => owner.Select(selected)),
            Selected: value == _state.StagedValue, Preferred: _state.IsPreferred(value))).ToArray(), TraitCollection, center);
        Root.SetNeedsLayout();
    }

#pragma warning disable CS0672, CA1422
    public override void TraitCollectionDidChange(UITraitCollection? previousTraitCollection)
    {
        base.TraitCollectionDidChange(previousTraitCollection);
        if (!OperatingSystem.IsIOSVersionAtLeast(17) && IsViewLoaded) Render(true);
    }
#pragma warning restore CS0672, CA1422

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var safe = Root.SafeAreaLayoutGuide.LayoutFrame;
        var actionHeight = (nfloat)Math.Max(44, Math.Max(_close?.SizeThatFits(CGSize.Empty).Height ?? 44,
            _done?.SizeThatFits(CGSize.Empty).Height ?? 44));
        var title = SliceUi.Measure(_title, safe.Width - 24);
        _title.Frame = new CGRect(safe.X + (safe.Width - title.Width) / 2, safe.Y + 8 + (actionHeight - title.Height) / 2, title.Width, title.Height);
        if (_close != null) _close.Frame = new CGRect(safe.X + 12, safe.Top + 8, (nfloat)Math.Ceiling(_close.SizeThatFits(CGSize.Empty).Width), actionHeight);
        if (_done != null)
        {
            var width = (nfloat)Math.Ceiling(_done.SizeThatFits(CGSize.Empty).Width);
            _done.Frame = new CGRect(safe.Right - width - 12, safe.Top + 8, width, actionHeight);
        }
        var scopeTop = safe.Top + actionHeight + 16;
        var scope = SliceUi.Measure(_scopeLabel, safe.Width - 32);
        var descriptionWidth = (nfloat)Math.Max(1, safe.Width - scope.Width - 32);
        var scopeHeight = (nfloat)Math.Max(44, Math.Max(scope.Height, SliceUi.Measure(_description, descriptionWidth).Height));
        _scopeBackground.Frame = new CGRect(safe.X, scopeTop, safe.Width, scopeHeight + 20);
        _description.Frame = new CGRect(safe.X + 16, scopeTop + 10, descriptionWidth, scopeHeight);
        _scopeLabel.Frame = new CGRect(safe.Right - 16 - scope.Width, scopeTop + 10, scope.Width, scopeHeight);
        if (_scope != null) _scope.Frame = new CGRect(safe.X + 16, scopeTop + 10, safe.Width - 32, scopeHeight);
        var arrow = _badgeArrow.Hidden ? CGSize.Empty : SliceUi.Measure(_badgeArrow, safe.Width);
        var badgeWidth = safe.Width - 40 - arrow.Width;
        var badgeText = SliceUi.Measure(_badgeText, badgeWidth);
        var badgeHeight = (nfloat)Math.Max(arrow.Height, badgeText.Height) + 36;
        _badge.Frame = new CGRect(safe.X, safe.Bottom - badgeHeight, safe.Width, badgeHeight);
        _badgeText.Frame = new CGRect(20, (badgeHeight - badgeText.Height) / 2, badgeWidth, badgeText.Height);
        _badgeArrow.Frame = new CGRect(safe.Width - 20 - arrow.Width, (badgeHeight - arrow.Height) / 2, arrow.Width, arrow.Height);
        if (_badgeAction != null) _badgeAction.Frame = _badge.Bounds;
        var listTop = scopeTop + scopeHeight + 20;
        _rows.Frame = new CGRect(safe.X, listTop, safe.Width, (nfloat)Math.Max(0, _badge.Frame.Top - listTop));
    }
}
