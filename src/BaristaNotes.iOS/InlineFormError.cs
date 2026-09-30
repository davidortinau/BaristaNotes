using CoreGraphics;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class InlineFormError : UIView
{
    private readonly UILabel _caption = new();
    private readonly UILabel _message = SliceUi.Label("", 16, true);

    public InlineFormError(string identifier)
    {
        AccessibilityIdentifier = identifier;
        BackgroundColor = NativeTheme.Error;
        SliceUi.TrackedText(_caption, "ERROR", 10, 2, NativeTheme.Surface.ColorWithAlpha(0.8f));
        _message.TextColor = NativeTheme.Surface;
        AddSubviews(_caption, _message);
        Hidden = true;
    }

    public void SetMessage(string? message)
    {
        _message.Text = message;
        Hidden = message == null;
        SetNeedsLayout();
    }

    public override CGSize SizeThatFits(CGSize size) => new(size.Width,
        (nfloat)Math.Max(60, 24 + SliceUi.Measure(_caption, size.Width - 32).Height
            + SliceUi.Measure(_message, size.Width - 32).Height));

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var caption = SliceUi.Measure(_caption, Bounds.Width - 32);
        _caption.Frame = new CGRect(16, 12, Bounds.Width - 32, caption.Height);
        var message = SliceUi.Measure(_message, Bounds.Width - 32);
        _message.Frame = new CGRect(16, 12 + caption.Height, Bounds.Width - 32, message.Height);
    }
}
