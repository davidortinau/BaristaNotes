using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using CoreGraphics;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class AdviceViewController : SliceViewController
{
    private readonly UIView _panel = new();
    private readonly UIScrollView _scroll = new() { ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never };
    private readonly AdviceBody _body;
    private readonly UILabel _title = new() { Text = AdvicePresentation.Title, TextAlignment = UITextAlignment.Center };
    private readonly UIButton _close = new(UIButtonType.Custom);
    private readonly UIButton _action = new(UIButtonType.Custom);
    private readonly UIButton _backdrop = new(UIButtonType.Custom);
    private bool _appearing = true, _open, _ended, _underlyingHidden;
    private bool _savedUnderlyingHidden;
    private Task? _closing;

    public AdviceViewController(SliceNavigationController host, AIAdviceResponseDto advice) : base(host)
    {
        ModalPresentationStyle = UIModalPresentationStyle.OverFullScreen;
        ModalInPresentation = true;
        _body = new AdviceBody(advice, WeakUiCallback.Create(this, static owner => owner.Root.SetNeedsLayout()));
    }
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Root.AccessibilityIdentifier = "advice.overlay";
        Root.AccessibilityViewIsModal = true;
        Root.BackgroundColor = UIColor.Clear;
        _backdrop.AccessibilityIdentifier = "advice.backdrop";
        _backdrop.AccessibilityLabel = "Dismiss AI Suggestions";
        _backdrop.BackgroundColor = NativeTheme.Backdrop;
        _panel.AccessibilityIdentifier = "advice.panel";
        _panel.BackgroundColor = NativeTheme.DarkSurface;
        _panel.Layer.CornerRadius = 24;
        _panel.ClipsToBounds = true;
        _panel.Hidden = true;
        _title.TextColor = NativeTheme.OnPrimary;
        _close.AccessibilityIdentifier = "advice.close";
        _close.AccessibilityLabel = "Close";
        _close.SetTitle("\ue14c", UIControlState.Normal);
        _close.SetTitleColor(NativeTheme.OnPrimary, UIControlState.Normal);
        _close.SetTitleColor(NativeTheme.OnPrimary, UIControlState.Disabled);
        _action.AccessibilityIdentifier = "advice.action";
        _action.SetTitle("Close", UIControlState.Normal);
        _action.SetTitleColor(NativeTheme.OnPrimary, UIControlState.Normal);
        _action.SetTitleColor(NativeTheme.OnPrimary, UIControlState.Disabled);
        _action.BackgroundColor = NativeTheme.Primary;
        _action.Layer.CornerRadius = 20;
        var close = WeakUiCallback.Create(this, static owner => _ = owner.CloseAsync());
        _action.TouchUpInside += (_, _) => close();
        _close.TouchUpInside += (_, _) => close();
        _backdrop.TouchUpInside += (_, _) => close();
        _scroll.AccessibilityIdentifier = "advice.scroll";
        _scroll.AddSubview(_body);
        _panel.AddSubviews(_title, _close, _scroll, _action);
        Root.AddSubviews(_backdrop, _panel);
        RefreshFonts();
        SetInteractions(false);
        if (OperatingSystem.IsIOSVersionAtLeast(17))
            RegisterForTraitChanges<UITraitPreferredContentSizeCategory>(static (environment, _) =>
                ((AdviceViewController)environment).RefreshFonts());
    }
    private void RefreshFonts()
    {
        _title.Font = SourceScaledText.Font(18, true, TraitCollection);
        _close.TitleLabel.Font = UIFontMetrics.DefaultMetrics.GetScaledFont(NativeTheme.Icons(24), TraitCollection);
        _action.TitleLabel.Font = UIFontMetrics.DefaultMetrics.GetScaledFont(NativeTheme.SystemFont(16), TraitCollection);
        _body.UpdateFonts(TraitCollection);
        Root.SetNeedsLayout();
    }
    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        if (!_underlyingHidden && Host.View is { } underlying)
        {
            _savedUnderlyingHidden = underlying.AccessibilityElementsHidden;
            underlying.AccessibilityElementsHidden = true;
            _underlyingHidden = true;
        }
    }
    public override void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        if (_appearing) _ = AppearAsync();
    }
    private async Task AppearAsync()
    {
        try
        {
            Root.LayoutIfNeeded();
            _appearing = false;
            Logger.LogInformation("AI Suggestions appearing from bottom,300ms CubicOut");
            await AnimatePanelAsync(true);
            if (_ended) return;
            _open = true;
            SetInteractions(true);
            UIAccessibility.PostNotification(UIAccessibilityPostNotification.ScreenChanged, _title);
        }
        catch (Exception error)
        {
            Logger.LogError(error, "AI Suggestions appearance failed");
            if (!_ended) DismissViewController(false, null);
        }
    }
    private Task CloseAsync()
    {
        if (_closing != null) return _closing;
        if (!_open || _ended) return Task.CompletedTask;
        _open = false;
        _closing = DismissAsync();
        return _closing;
    }
    private async Task DismissAsync()
    {
        SetInteractions(false);
        try
        {
            Logger.LogInformation("AI Suggestions disappearing to bottom,400ms CubicIn");
            await AnimatePanelAsync(false);
        }
        catch (Exception error) { Logger.LogError(error, "AI Suggestions exit animation interrupted"); }
        finally
        {
            if (!_ended)
            {
                var removed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                DismissViewController(false, () => removed.TrySetResult());
                await removed.Task;
            }
            RestoreUnderlyingAccessibility();
        }
    }
    private async Task AnimatePanelAsync(bool entering)
    {
        var duration = entering ? .3 : .4;
        var steps = (int)Math.Ceiling(duration / .016);
        var distance = (Root.Bounds.Height + _panel.Bounds.Height) / 2;
        var completed = await UIView.AnimateKeyframesAsync(duration, 0, UIViewKeyframeAnimationOptions.CalculationModeLinear, () =>
        {
            for (var i = 1; i <= steps; i++)
            {
                var t = i / (double)steps;
                var fraction = entering ? 1 - (Math.Pow(t - 1, 3) + 1) : t * t * t;
                var offset = distance * (nfloat)fraction;
                UIView.AddKeyframeWithRelativeStartTime((i - 1) / (double)steps, 1.0 / steps,
                    () => _panel.Transform = CGAffineTransform.MakeTranslation(0, offset));
            }
        });
        if (!completed && !_ended) throw new InvalidOperationException("AI Suggestions animation did not complete.");
    }
    private void SetInteractions(bool enabled)
    {
        _panel.UserInteractionEnabled = _scroll.ScrollEnabled = enabled;
        _backdrop.Enabled = _action.Enabled = _close.Enabled = enabled;
    }
    private void RestoreUnderlyingAccessibility()
    {
        if (!_underlyingHidden) return;
        if (Host.View is { } underlying) underlying.AccessibilityElementsHidden = _savedUnderlyingHidden;
        _underlyingHidden = false;
    }
    public override void ViewDidDisappear(bool animated)
    {
        _ended = true;
        _panel.Layer.RemoveAllAnimations();
        RestoreUnderlyingAccessibility();
        base.ViewDidDisappear(animated);
    }
    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var safe = Root.SafeAreaInsets;
        var width = Root.Bounds.Width - safe.Left - safe.Right;
        var height = Root.Bounds.Height - safe.Top;
        _backdrop.Frame = Root.Bounds;
        _panel.Bounds = new CGRect(0, 0, width, height);
        _panel.Center = new CGPoint(safe.Left + width / 2, safe.Top + height / 2);
        var titleHeight = SliceUi.Measure(_title, width - 48).Height;
        var closeSize = _close.TitleLabel.SizeThatFits(new CGSize(100, 100));
        var headingHeight = (nfloat)Math.Max(titleHeight, closeSize.Height - 4);
        _title.Frame = new CGRect(24, 24, width - 48, titleHeight);
        _close.Frame = new CGRect(width - 34 - closeSize.Width, 20 - (44 - closeSize.Height) / 2, closeSize.Width + 20, 44);
        var buttonHeight = (nfloat)Math.Max(54, _action.TitleLabel.Font.LineHeight + 28);
        _action.Frame = new CGRect(24, height - 24 - buttonHeight, width - 48, buttonHeight);
        var top = 24 + headingHeight + 16;
        var available = (nfloat)Math.Max(0, _action.Frame.Top - 16 - top);
        var displayHeight = Root.Window?.WindowScene?.Screen.Bounds.Height ?? Root.Bounds.Height;
        var bodyHeight = (nfloat)Math.Min(available, Math.Max(240, displayHeight * .6));
        _scroll.Frame = new CGRect(24, top + (available - bodyHeight) / 2, width - 48, bodyHeight);
        var desired = _body.SizeThatFits(new CGSize(_scroll.Bounds.Width, nfloat.MaxValue));
        _body.Frame = new CGRect(0, 0, _scroll.Bounds.Width, desired.Height);
        _scroll.ContentSize = _body.Bounds.Size;
        if (_appearing)
        {
            _panel.Transform = CGAffineTransform.MakeTranslation(0, (Root.Bounds.Height + height) / 2);
            _panel.Hidden = false;
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ended = true;
            _panel.Layer.RemoveAllAnimations();
            RestoreUnderlyingAccessibility();
        }
        base.Dispose(disposing);
    }
}

internal sealed class AdviceBody : UIView
{
    private readonly List<(UILabel Bullet, UILabel Text)> _adjustments = [];
    private readonly UILabel _empty = new() { Text = AdvicePresentation.EmptyAdjustments, Lines = 0 };
    private readonly UILabel _reason = new() { Lines = 0 };
    private readonly UILabel _source = new() { TextAlignment = UITextAlignment.Right, Lines = 0 };
    private readonly UILabel _prompt = new() { Lines = 0, AccessibilityIdentifier = "advice.prompt" };
    private readonly UIButton _toggle = new(UIButtonType.Custom);
    private readonly UILabel _toggleGlyph = new() { Text = "▸" };
    private readonly UILabel _toggleLabel = new() { Text = "Show prompt" };
    private readonly Action _relayout;
    private bool _expanded;

    public AdviceBody(AIAdviceResponseDto advice, Action relayout)
    {
        _relayout = relayout;
        foreach (var adjustment in advice.Adjustments ?? [])
        {
            var bullet = new UILabel { Text = "•" };
            var text = new UILabel { Text = AdvicePresentation.Adjustment(adjustment), Lines = 0 };
            _adjustments.Add((bullet, text));
            AddSubviews(bullet, text);
        }
        _empty.Hidden = _adjustments.Count > 0;
        _reason.Text = advice.Reasoning;
        _reason.Hidden = string.IsNullOrWhiteSpace(advice.Reasoning);
        _source.Text = advice.Source ?? "";
        _toggle.Hidden = string.IsNullOrWhiteSpace(advice.PromptSent);
        _prompt.Text = string.IsNullOrWhiteSpace(advice.PromptSent)
            ? string.Empty
            : AdvicePresentation.Prompt(advice.PromptSent, advice.HistoricalShotsCount);
        _prompt.Hidden = true;
        _toggle.AccessibilityIdentifier = "advice.prompt.toggle";
        _toggle.AccessibilityLabel = "Show prompt";
        _toggle.BackgroundColor = NativeTheme.DarkVariant;
        _toggle.Layer.CornerRadius = 12;
        _toggle.AddSubviews(_toggleGlyph, _toggleLabel);
        var toggle = WeakUiCallback.Create(this, static owner => owner.Toggle());
        _toggle.TouchUpInside += (_, _) => toggle();
        AddSubviews(_empty, _reason, _source, _toggle, _prompt);
    }
    private void Toggle()
    {
        _expanded = !_expanded;
        _prompt.Hidden = !_expanded;
        _toggleGlyph.Text = _expanded ? "▾" : "▸";
        _toggleLabel.Text = _expanded ? "Hide prompt" : "Show prompt";
        _toggle.AccessibilityLabel = _toggleLabel.Text;
        SetNeedsLayout();
        _relayout();
    }
    public void UpdateFonts(UITraitCollection traits)
    {
        UIFont Font(nfloat size, bool bold = false, bool italic = false) =>
            UIFontMetrics.DefaultMetrics.GetScaledFont(NativeTheme.SystemFont(size, bold, italic), traits);
        foreach (var (bullet, text) in _adjustments)
        {
            bullet.Font = text.Font = Font(14);
            bullet.TextColor = NativeTheme.DarkSecondary;
            text.TextColor = NativeTheme.OnPrimary;
        }
        _empty.Font = Font(14);
        _reason.Font = Font(13, italic: true);
        _source.Font = Font(12);
        _prompt.Font = SourceScaledText.Font(11, false, traits);
        _toggleGlyph.Font = Font(12);
        _toggleLabel.Font = Font(12, true);
        foreach (var label in new[] { _empty, _reason, _source, _prompt }) label.TextColor = NativeTheme.DarkSecondary;
        _toggleGlyph.TextColor = _toggleLabel.TextColor = NativeTheme.OnPrimary;
    }
    public override CGSize SizeThatFits(CGSize size) => new(size.Width, Layout(size.Width, false));
    public override void LayoutSubviews() { base.LayoutSubviews(); Layout(Bounds.Width, true); }
    private nfloat Layout(nfloat width, bool apply)
    {
        var contentWidth = (nfloat)Math.Max(1, width - 32);
        nfloat y = 0;
        void Label(UILabel label, int extraTop = 0)
        {
            if (label.Hidden) return;
            if (y > 0) y += 12;
            y += extraTop;
            var height = SliceUi.Measure(label, contentWidth).Height;
            if (apply) label.Frame = new CGRect(16, y, contentWidth, height);
            y += height;
        }
        foreach (var (bullet, text) in _adjustments)
        {
            if (y > 0) y += 12;
            var bulletSize = SliceUi.Measure(bullet, contentWidth);
            var textHeight = SliceUi.Measure(text, contentWidth - bulletSize.Width - 8).Height;
            if (apply)
            {
                bullet.Frame = new CGRect(16, y, bulletSize.Width, bulletSize.Height);
                text.Frame = new CGRect(24 + bulletSize.Width, y, contentWidth - bulletSize.Width - 8, textHeight);
            }
            y += (nfloat)Math.Max(bulletSize.Height, textHeight);
        }
        Label(_empty);
        Label(_reason, 4);
        if (y > 0) y += 12;
        y += 8;
        var glyph = SliceUi.Measure(_toggleGlyph, contentWidth);
        var title = SliceUi.Measure(_toggleLabel, contentWidth);
        var toggleWidth = _toggle.Hidden ? 0 : glyph.Width + 6 + title.Width + 20;
        var toggleHeight = _toggle.Hidden ? 0 : (nfloat)Math.Max(glyph.Height, title.Height) + 12;
        var sourceWidth = (nfloat)Math.Max(1, contentWidth - toggleWidth - 8);
        var sourceHeight = SliceUi.Measure(_source, sourceWidth).Height;
        var rowHeight = (nfloat)Math.Max(toggleHeight, sourceHeight);
        if (apply)
        {
            _toggle.Frame = new CGRect(16, y + (rowHeight - toggleHeight) / 2, toggleWidth, toggleHeight);
            _toggleGlyph.Frame = new CGRect(10, (toggleHeight - glyph.Height) / 2, glyph.Width, glyph.Height);
            _toggleLabel.Frame = new CGRect(16 + glyph.Width, (toggleHeight - title.Height) / 2, title.Width, title.Height);
            _source.Frame = new CGRect(16 + toggleWidth + 8, y + (rowHeight - sourceHeight) / 2, sourceWidth, sourceHeight);
        }
        y += rowHeight;
        Label(_prompt, 4);
        return y;
    }
}
