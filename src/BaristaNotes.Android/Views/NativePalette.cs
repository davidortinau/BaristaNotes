namespace BaristaNotes.AndroidApp.Views;

internal readonly record struct NativePalette(int Surface, int Variant, int Text, int Secondary, int Outline)
{
    public static NativePalette Light => new(
        unchecked((int)0xFFFCEFE1), unchecked((int)0xFFECDAC4), unchecked((int)0xFF352B23),
        unchecked((int)0xFF7C7067), unchecked((int)0xFFD7C5B2));
    public static NativePalette Dark => new(
        unchecked((int)0xFF48362E), unchecked((int)0xFF7D5A45), unchecked((int)0xFFF8F6F4),
        unchecked((int)0xFFC5BFBB), unchecked((int)0xFF5A463B));

    public int TranslateTo(int color, NativePalette target)
    {
        const int rgbMask = 0x00FFFFFF;
        var alpha = color & unchecked((int)0xFF000000);
        if (alpha == 0) return color;
        var rgb = color & rgbMask;
        var replacement = rgb == (Surface & rgbMask) ? target.Surface
            : rgb == (Variant & rgbMask) ? target.Variant
            : rgb == (Text & rgbMask) ? target.Text
            : rgb == (Secondary & rgbMask) ? target.Secondary
            : rgb == (Outline & rgbMask) ? target.Outline : color;
        return alpha | (replacement & rgbMask);
    }
}
