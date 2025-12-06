using Fonts;

namespace BaristaNotes.Styles;

public static class AppIcons
{
    private static Color IconColor => ApplicationTheme.IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;

    public static FontImageSource CoffeeCup => new()
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Coffee,
        Size = 32,
        Color = IconColor
    };

    public static FontImageSource Feed => new()
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Feed,
        Size = 32,
        Color = IconColor
    };

    public static FontImageSource Settings => new()
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Settings,
        Size = 32,
        Color = IconColor
    };

    public static FontImageSource Edit => new()
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Edit,
        Size = 18,
        Color = IconColor
    };

    public static FontImageSource Delete => new()
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Delete,
        Size = 18,
        Color = IconColor
    };

    public static FontImageSource Add => new()
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Add,
        Size = 24,
        Color = IconColor
    };

}