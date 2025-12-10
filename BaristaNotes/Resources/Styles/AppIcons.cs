using Fonts;

namespace BaristaNotes.Styles;

public static class AppIcons
{
    public static FontImageSource EspressoMachine => new()
    {
        FontFamily = "coffee-icons",
        Glyph = "s",// Machine icon from coffee-icons font
        Size = 64,
        Color = IconColor
    };
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

    public static FontImageSource Ai => new()
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Magic_button,
        Size = 24,
        Color = IconColor
    };

    public static FontImageSource Add => new()
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Add,
        Size = 24,
        Color = IconColor
    };

    public static FontImageSource Increment => new()
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Add,
        Size = 12,
        Color = ApplicationTheme.IsLightTheme ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary
    };

    public static FontImageSource Decrement => new()
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Remove,
        Size = 12,
        Color = ApplicationTheme.IsLightTheme ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary
    };

    // Rating icons (0-4 scale: Terrible to Excellent)
    // Used consistently across ShotLoggingPage, ShotRecordCard, RatingDisplayComponent
    public static readonly string[] RatingIcons = new[]
    {
        MaterialSymbolsFont.Sentiment_very_dissatisfied, // 0 - Terrible
        MaterialSymbolsFont.Sentiment_dissatisfied,      // 1 - Bad
        MaterialSymbolsFont.Sentiment_neutral,           // 2 - Average
        MaterialSymbolsFont.Sentiment_satisfied,         // 3 - Good
        MaterialSymbolsFont.Sentiment_very_satisfied     // 4 - Excellent
    };

    public static string GetRatingIcon(int rating)
    {
        if (rating < 0 || rating >= RatingIcons.Length)
            return RatingIcons[2]; // Default to neutral
        return RatingIcons[rating];
    }

}