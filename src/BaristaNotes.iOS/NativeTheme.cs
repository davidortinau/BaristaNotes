using UIKit;

namespace BaristaNotes.Native.iOS;

internal static class NativeTheme
{
    // UIKit mappings of the existing AppColors tokens, not a new shared UI abstraction.
    public static UIColor TextPrimary { get; } = UIColor.FromDynamicProvider(traits =>
        traits.UserInterfaceStyle == UIUserInterfaceStyle.Dark
            ? UIColor.FromRGB(0xF8, 0xF6, 0xF4) : UIColor.FromRGB(0x35, 0x2B, 0x23));
    public static UIColor Primary { get; } = UIColor.FromRGB(0x86, 0x54, 0x3F);
    public static UIColor Error { get; } = UIColor.FromRGB(0xEF, 0x53, 0x50);
    public static UIColor Warning { get; } = UIColor.FromRGB(0xFF, 0xA7, 0x26);
    public static UIColor OnPrimary { get; } = UIColor.FromRGB(0xF8, 0xF6, 0xF4);
    public static UIColor DarkSurface { get; } = UIColor.FromRGB(0x48, 0x36, 0x2E);
    public static UIColor Surface { get; } = UIColor.FromDynamicProvider(traits =>
        traits.UserInterfaceStyle == UIUserInterfaceStyle.Dark ? DarkSurface : UIColor.FromRGB(0xFC, 0xEF, 0xE1));
    public static UIColor Secondary { get; } = UIColor.FromDynamicProvider(traits =>
        traits.UserInterfaceStyle == UIUserInterfaceStyle.Dark ? UIColor.FromRGB(0xC5, 0xBF, 0xBB) : UIColor.FromRGB(0x7C, 0x70, 0x67));
    public static UIColor Outline { get; } = UIColor.FromDynamicProvider(traits =>
        traits.UserInterfaceStyle == UIUserInterfaceStyle.Dark ? UIColor.FromRGB(0x5A, 0x46, 0x3B) : UIColor.FromRGB(0xD7, 0xC5, 0xB2));
    public static UIColor DarkVariant { get; } = UIColor.FromRGB(0x7D, 0x5A, 0x45);
    public static UIColor DarkOutline { get; } = UIColor.FromRGB(0x5A, 0x46, 0x3B);
    public static UIColor DarkSecondary { get; } = UIColor.FromRGB(0xC5, 0xBF, 0xBB);
    public static UIColor Backdrop { get; } = UIColor.Black.ColorWithAlpha(170f / 255);
    public static UIFont Font(nfloat size, bool bold = false) =>
        UIFont.FromName(bold ? "Manrope-SemiBold" : "Manrope-Regular", size)
        ?? throw new InvalidOperationException("Required Manrope font is unavailable.");
    public static UIFont Icons(nfloat size) => UIFont.FromName("MaterialSymbolsOutlined-Regular", size)
        ?? UIFont.FromName("MaterialSymbolsOutlined", size)
        ?? throw new InvalidOperationException("Required Material Symbols font is unavailable.");

    public static UIFont SystemFont(nfloat size, bool bold = false, bool italic = false) =>
        (italic ? UIFont.ItalicSystemFontOfSize(size) :
            bold ? UIFont.BoldSystemFontOfSize(size) : UIFont.SystemFontOfSize(size))
        ?? throw new InvalidOperationException("The requested UIKit system font is unavailable.");

}
