using CoreGraphics;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal static class PhotoUi
{
    // Raw MAUI Controls in these popup bodies use the platform font unless a
    // FontFamily is explicitly assigned; popup titles/intent labels use Manrope.
    public static UIFont Font(nfloat size, UITraitCollection traits, bool bold = false) =>
        UIFontMetrics.DefaultMetrics.GetScaledFont(NativeTheme.SystemFont(size, bold), traits);
    public static UILabel Label(string text, nfloat size, UITraitCollection traits, bool bold = false, UIColor? color = null) =>
        new() { Text = text, Lines = 0, Font = Font(size, traits, bold), TextColor = color ?? NativeTheme.DarkSecondary };
    public static UIButton Button(string text, string id, Action action, UITraitCollection traits,
        bool filled = false, bool outlined = false)
    {
        var button = new UIButton(UIButtonType.Custom) { AccessibilityIdentifier = id };
        button.SetTitle(text, UIControlState.Normal);
        button.TitleLabel.Font = Font(UIFont.SystemFontSize, traits);
        button.SetTitleColor(filled ? NativeTheme.OnPrimary : NativeTheme.Primary, UIControlState.Normal);
        button.BackgroundColor = filled ? NativeTheme.Primary : UIColor.Clear;
        button.Layer.CornerRadius = 18;
        button.Layer.BorderWidth = outlined ? 1 : 0;
        button.Layer.BorderColor = NativeTheme.Primary.CGColor;
        button.TouchUpInside += (_, _) => action();
        return button;
    }
    public static void Haptic(ILogger logger)
    {
        try
        {
            using var view = new UIView();
            using var generator = OperatingSystem.IsIOSVersionAtLeast(17, 5)
                ? UIImpactFeedbackGenerator.GetFeedbackGenerator(UIImpactFeedbackStyle.Light, view)
                : new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Light);
            generator.Prepare();
            generator.ImpactOccurred();
        }
        catch (Exception error) { logger.LogDebug(error, "Photo success haptic unavailable"); }
    }
}

internal sealed class PhotoStack : UIView
{
    private readonly UIView[] _items;
    private readonly nfloat _spacing;
    private readonly UIEdgeInsets _padding;
    public PhotoStack(int spacing, UIEdgeInsets padding, params UIView[] items)
    {
        _items = items;
        _spacing = spacing;
        _padding = padding;
        AddSubviews(items);
    }
    public override CGSize SizeThatFits(CGSize size)
    {
        var visible = _items.Where(view => !view.Hidden).ToArray();
        return new(size.Width, _padding.Top + _padding.Bottom
            + visible.Sum(view => (double)view.SizeThatFits(new CGSize(size.Width - _padding.Left - _padding.Right, nfloat.MaxValue)).Height)
            + Math.Max(0, visible.Length - 1) * _spacing);
    }
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var width = Bounds.Width - _padding.Left - _padding.Right;
        var y = _padding.Top;
        foreach (var item in _items.Where(view => !view.Hidden))
        {
            var height = item.SizeThatFits(new CGSize(width, nfloat.MaxValue)).Height;
            item.Frame = new CGRect(_padding.Left, y, width, height);
            y += height + _spacing;
        }
    }
}

internal sealed class PhotoFixedBox(UIView content, nfloat height, int inset = 0) : UIView
{
    public override void MovedToSuperview()
    {
        base.MovedToSuperview();
        if (content.Superview != this) AddSubview(content);
    }

    public override CGSize SizeThatFits(CGSize size) => new(size.Width, height);
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        content.Frame = Bounds.Inset(inset, 0);
    }
}

internal sealed class PhotoInsetView(UIView content, UIEdgeInsets padding) : UIView
{
    public override void MovedToSuperview()
    {
        base.MovedToSuperview();
        if (content.Superview != this) AddSubview(content);
    }
    public override CGSize SizeThatFits(CGSize size) => new(size.Width,
        padding.Top + content.SizeThatFits(new CGSize(size.Width - padding.Left - padding.Right, nfloat.MaxValue)).Height + padding.Bottom);
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        content.Frame = new CGRect(padding.Left, padding.Top, Bounds.Width - padding.Left - padding.Right,
            Bounds.Height - padding.Top - padding.Bottom);
    }
}
