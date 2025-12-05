using Fonts;

namespace BaristaNotes.Styles;

public static class AppIcons
{
    public static readonly FontImageSource CoffeeCup = new()
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Coffee,
        Size = 32
    };

    public static readonly FontImageSource Feed = new()
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Feed,
        Size = 32
    };

    public static readonly FontImageSource Edit = new()
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Edit,
        Size = 18
    };

    public static readonly FontImageSource Delete = new()
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Delete,
        Size = 18
    };

    public static readonly FontImageSource Add = new()
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Add,
        Size = 24
    };

}