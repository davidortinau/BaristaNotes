using CoreGraphics;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class PhotoProcessingView : UIView
{
    private readonly UIView _card = new() { BackgroundColor = NativeTheme.Surface };
    private readonly UIActivityIndicatorView _spinner = new(UIActivityIndicatorViewStyle.Medium) { Color = NativeTheme.Primary };
    private readonly UILabel _title = new() { Text = "Reviewing photo", TextAlignment = UITextAlignment.Center, Lines = 0 };
    private readonly UILabel _detail = new() { Text = "Finding the best next step…", TextAlignment = UITextAlignment.Center, Lines = 0 };
    public PhotoProcessingView()
    {
        AccessibilityIdentifier = "PhotoProcessingOverlay";
        AccessibilityViewIsModal = true;
        BackgroundColor = NativeTheme.DarkSurface.ColorWithAlpha(.72f);
        _card.Layer.CornerRadius = 12;
        _spinner.AccessibilityIdentifier = "PhotoProcessingIndicator";
        _title.Font = SourceScaledText.Font(16, true, TraitCollection);
        _title.TextColor = NativeTheme.TextPrimary;
        _detail.Font = SourceScaledText.Font(14, false, TraitCollection);
        _detail.TextColor = NativeTheme.Secondary;
        _card.AddSubviews(_spinner, _title, _detail);
        AddSubview(_card);
        _spinner.StartAnimating();
    }
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var maximum = (nfloat)Math.Max(1, Bounds.Width - 80);
        var title = SliceUi.Measure(_title, maximum);
        var detail = SliceUi.Measure(_detail, maximum);
        var width = (nfloat)Math.Min(Bounds.Width - 48, Math.Max(title.Width, detail.Width) + 32);
        var height = title.Height + detail.Height + 20 + 16 + 32;
        _card.Frame = new CGRect((Bounds.Width - width) / 2, (Bounds.Height - height) / 2, width, height);
        _spinner.Frame = new CGRect((width - 20) / 2, 16, 20, 20);
        _title.Frame = new CGRect(16, 44, width - 32, title.Height);
        _detail.Frame = new CGRect(16, 52 + title.Height, width - 32, detail.Height);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) _spinner.StopAnimating();
        base.Dispose(disposing);
    }
}
