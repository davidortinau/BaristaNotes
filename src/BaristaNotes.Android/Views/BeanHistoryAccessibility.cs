namespace BaristaNotes.AndroidApp.Views;

internal static class BeanHistoryAccessibility
{
    public static string Describe(string drink, string? visibleMethod, int renderedRating,
        string bean, string recipe, string? visibleFooter)
    {
        // These meanings follow the source's0–4 sentiment glyphs. Its invalid
        // glyph fallback is Average; null records render glyph0 at the caller.
        var rating = renderedRating switch
        {
            0 => "Terrible", 1 => "Bad", 2 => "Average", 3 => "Good", 4 => "Excellent",
            _ => "Average"
        };
        var parts = new List<string> { drink };
        if (!string.IsNullOrEmpty(visibleMethod)) parts.Add(visibleMethod);
        parts.Add($"Rating: {rating}");
        parts.Add(bean);
        parts.Add(recipe);
        if (!string.IsNullOrEmpty(visibleFooter)) parts.Add(visibleFooter);
        return string.Join(", ", parts);
    }
}
