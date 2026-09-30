using Foundation;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal static class SourceScaledText
{
    // The pinned MAUI FontManager uses DefaultMetrics with no maximum point-size cap.
    // Opt in at the specific source surfaces; NativeTheme.Font remains unscaled.
    public static UIFont Font(nfloat size, bool bold, UITraitCollection traits) =>
        UIFontMetrics.DefaultMetrics.GetScaledFont(NativeTheme.Font(size, bold), traits);

    public static void Tracked(UILabel label, string text, nfloat size, nfloat spacing,
        UIColor color, UITraitCollection traits)
    {
        label.Font = Font(size, true, traits);
        label.TextColor = color;
        using var textWithSpacing = new NSAttributedString(text, new UIStringAttributes
        {
            Font = label.Font, ForegroundColor = color, KerningAdjustment = (float)spacing
        });
        label.AttributedText = textWithSpacing;
    }
}
