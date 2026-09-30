using CoreGraphics;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class EquipmentConfirmationViewController : SliceViewController
{
    private enum Phase { Preparing, Appearing, Open, Executing, Disappearing, Closed }
    private readonly string _heading;
    private readonly string _bodyText;
    private readonly string _actionText;
    private readonly Func<Task> _primary;
    private readonly Action _afterPrimary;
    private readonly string _operation;
    private readonly UIView _panel = new();
    private readonly UILabel _title = new() { Lines = 0, TextAlignment = UITextAlignment.Center };
    private readonly UILabel _body = new() { Lines = 0, TextAlignment = UITextAlignment.Center };
    private UIButton? _backdrop;
    private UIButton? _cancel;
    private UIButton? _confirm;
    private Phase _phase;

    public EquipmentConfirmationViewController(SliceNavigationController host, string heading, string body,
        string actionText, Func<Task> primary, Action afterPrimary, string operation = "archive equipment") : base(host)
    {
        _heading = heading;
        _bodyText = body;
        _actionText = actionText;
        _primary = primary;
        _afterPrimary = afterPrimary;
        _operation = operation;
        ModalPresentationStyle = UIModalPresentationStyle.OverFullScreen;
        ModalInPresentation = true;
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Root.AccessibilityIdentifier = "equipment.confirmation";
        Root.AccessibilityViewIsModal = true;
        Root.BackgroundColor = UIColor.Clear;
        _backdrop = SliceUi.Button("", "equipment.confirm.backdrop",
            WeakUiCallback.Create(this, static owner => _ = owner.CloseAsync(false)));
        _backdrop.BackgroundColor = NativeTheme.Backdrop;
        _backdrop.AccessibilityLabel = $"Cancel {_actionText.ToLowerInvariant()}";
        _panel.BackgroundColor = NativeTheme.DarkSurface;
        _panel.Layer.CornerRadius = 20;
        _panel.AccessibilityIdentifier = "equipment.confirm.card";
        _panel.Hidden = true;
        _cancel = CreateAction("Cancel", "equipment.confirm.cancel", false,
            WeakUiCallback.Create(this, static owner => _ = owner.CloseAsync(false)));
        _confirm = CreateAction(_actionText, "equipment.confirm.primary", true,
            WeakUiCallback.Create(this, static owner => _ = owner.CloseAsync(true)));
        _title.Text = _heading;
        _body.Text = _bodyText;
        _title.TextColor = NativeTheme.OnPrimary;
        _body.TextColor = NativeTheme.DarkSecondary;
        _panel.AddSubviews(_title, _body, _cancel, _confirm);
        Root.AddSubviews(_backdrop, _panel);
        if (OperatingSystem.IsIOSVersionAtLeast(17))
            RegisterForTraitChanges<UITraitPreferredContentSizeCategory>(static (environment, _) =>
                ((EquipmentConfirmationViewController)environment).UpdateFonts());
        UpdateFonts();
        SetInteractions(false);
    }

    private static UIButton CreateAction(string title, string id, bool primary, Action action)
    {
        var button = new UIButton(UIButtonType.Custom) { AccessibilityIdentifier = id };
        button.SetTitle(title, UIControlState.Normal);
        var foreground = primary ? NativeTheme.OnPrimary : NativeTheme.Primary;
        button.SetTitleColor(foreground, UIControlState.Normal);
        button.SetTitleColor(foreground, UIControlState.Disabled);
        button.BackgroundColor = primary ? NativeTheme.Primary : EquipmentColors.DarkElevated;
        button.Layer.CornerRadius = 20;
        button.TouchUpInside += (_, _) => action();
        return button;
    }

    private void UpdateFonts()
    {
        _title.Font = SourceScaledText.Font(24, true, TraitCollection);
        _body.Font = SourceScaledText.Font(12, false, TraitCollection);
        if (_cancel != null) _cancel.TitleLabel.Font = SourceScaledText.Font(16, false, TraitCollection);
        if (_confirm != null) _confirm.TitleLabel.Font = SourceScaledText.Font(16, false, TraitCollection);
        Root.SetNeedsLayout();
    }

    public override void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        if (_phase == Phase.Preparing) _ = AppearAsync();
    }

    private async Task AppearAsync()
    {
        _phase = Phase.Appearing;
        try
        {
            Root.LayoutIfNeeded();
            var distance = (Root.Bounds.Width + _panel.Bounds.Width) / 2;
            _panel.Transform = CGAffineTransform.MakeTranslation(distance, 0);
            _panel.Hidden = false;
            Logger.LogDebug("SimpleAction appearing: Right SpringOut {DurationMs}ms", 400);
            await AnimateAsync(0.4, t =>
                _panel.Transform = CGAffineTransform.MakeTranslation(distance * (nfloat)(1 - SpringOut(t)), 0));
            _phase = Phase.Open;
            SetInteractions(true);
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Equipment confirmation appearance failed");
            _phase = Phase.Closed;
            DismissViewController(false, () => ShowFeedback("Could not open equipment confirmation.", isError: true));
        }
    }

    private async Task CloseAsync(bool confirmed)
    {
        if (_phase != Phase.Open) return;
        SetInteractions(false);
        if (confirmed)
        {
            _phase = Phase.Executing;
            try { await _primary(); }
            catch (Exception error)
            {
                Logger.LogError(error, "Confirmation operation {Operation} failed", _operation);
                _phase = Phase.Open;
                SetInteractions(true);
                ShowFeedback($"Failed to {_operation}: {error.Message}", isError: true);
                return;
            }
        }
        _phase = Phase.Disappearing;
        try
        {
            Logger.LogDebug("SimpleAction disappearing: Scale1.2 {FirstMs}ms, Scale0.5 {SecondMs}ms, Left SpringOut {ExitMs}ms", 150, 150, 500);
            await AnimateAsync(0.15, t => _panel.Transform = CGAffineTransform.MakeScale((nfloat)(1 + .2 * t), (nfloat)(1 + .2 * t)));
            await AnimateAsync(0.15, t => _panel.Transform = CGAffineTransform.MakeScale((nfloat)(1.2 - .7 * t), (nfloat)(1.2 - .7 * t)));
            var distance = (Root.Bounds.Width + _panel.Bounds.Width) / 2;
            await AnimateAsync(0.5, t => _panel.Transform = new CGAffineTransform(.5f, 0, 0, .5f, -distance * (nfloat)SpringOut(t), 0));
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Equipment confirmation disappearance was interrupted");
        }
        finally
        {
            _phase = Phase.Closed;
            DismissViewController(false, () => { if (confirmed) _afterPrimary(); });
        }
    }

    private static double SpringOut(double t)
    {
        var x = t - 1;
        return x * x * ((1.70158 + 1) * x + 1.70158) + 1;
    }

    private static async Task AnimateAsync(double duration, Action<double> frame)
    {
        var steps = (int)Math.Ceiling(duration / .016);
        var finished = await UIView.AnimateKeyframesAsync(duration, 0, UIViewKeyframeAnimationOptions.CalculationModeLinear, () =>
        {
            for (var index = 1; index <= steps; index++)
            {
                var t = index / (double)steps;
                UIView.AddKeyframeWithRelativeStartTime((index - 1) / (double)steps, 1d / steps, () => frame(t));
            }
        });
        if (!finished) throw new InvalidOperationException("Native SimpleAction animation was interrupted.");
    }

    private void SetInteractions(bool enabled)
    {
        _panel.UserInteractionEnabled = enabled;
        if (_backdrop != null) _backdrop.Enabled = enabled;
        if (_cancel != null) _cancel.Enabled = enabled;
        if (_confirm != null) _confirm.Enabled = enabled;
    }

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        if (_backdrop != null) _backdrop.Frame = Root.Bounds;
        var safe = Root.SafeAreaInsets;
        var width = (nfloat)Math.Max(1, Root.Bounds.Width - safe.Left - safe.Right - 60);
        var contentWidth = (nfloat)Math.Max(1, width - 108);
        var title = SliceUi.Measure(_title, contentWidth);
        var body = SliceUi.Measure(_body, contentWidth);
        var height = 48 + title.Height + 4 + body.Height + 12 + 54;
        // PopupPage respects Top|Left|Right, not bottom: no duplicated bottom safe inset.
        _panel.Bounds = new CGRect(0, 0, width, height);
        _panel.Center = new CGPoint(safe.Left + (Root.Bounds.Width - safe.Left - safe.Right) / 2,
            safe.Top + (Root.Bounds.Height - safe.Top) / 2);
        _title.Frame = new CGRect(54, 24, contentWidth, title.Height);
        _body.Frame = new CGRect(54, 28 + title.Height, contentWidth, body.Height);
        var buttonWidth = (width - 64) / 2;
        var buttonTop = height - 78;
        if (_cancel != null) _cancel.Frame = new CGRect(24, buttonTop, buttonWidth, 54);
        if (_confirm != null) _confirm.Frame = new CGRect(40 + buttonWidth, buttonTop, buttonWidth, 54);
    }
}
