using BaristaNotes.Core.Services.Grind;
using static MauiReactor.Component;

namespace BaristaNotes.Components;

/// <summary>
/// Compact read-only display of a <see cref="GrindTranslationResult"/>: suggested
/// setting with a min–max range and a source badge (e.g. "from your last shot",
/// "AI", "calculated"). Stateless — the hosting page owns translation state.
/// </summary>
static class GrindTranslationChip
{
    public static VisualNode Render(GrindTranslationResult? result, bool loading = false)
    {
        if (loading)
        {
            return Label("Translating grind…")
                .ThemeKey(ThemeKeys.SecondaryText)
                .FontSize(11);
        }

        if (result == null) return null!;

        var (badgeText, badgeColor) = result.Source switch
        {
            GrindTranslationSource.UserHistory => ("From your history", Colors.SeaGreen),
            GrindTranslationSource.Deterministic => ("Calculated", Colors.SteelBlue),
            GrindTranslationSource.Cache => ("Known match", Colors.SteelBlue),
            GrindTranslationSource.AI => ("AI", Colors.MediumPurple),
            _ => ("Estimate", Colors.Gray),
        };

        string headline;
        if (result.SuggestedSetting.HasValue)
        {
            if (result.MinSetting.HasValue && result.MaxSetting.HasValue
                && result.MinSetting.Value != result.MaxSetting.Value)
            {
                headline = $"On {result.GrinderModel}: {result.MinSetting:0.#}–{result.MaxSetting:0.#} (try {result.SuggestedSetting:0.#})";
            }
            else
            {
                headline = $"On {result.GrinderModel}: {result.SuggestedSetting:0.#}";
            }
        }
        else if (result.MinSetting.HasValue && result.MaxSetting.HasValue)
        {
            headline = $"On {result.GrinderModel}: {result.MinSetting:0.#}–{result.MaxSetting:0.#}";
        }
        else
        {
            // No numeric translation available — fall back to explanation only.
            if (string.IsNullOrWhiteSpace(result.Explanation)) return null!;
            return Label(result.Explanation!)
                .ThemeKey(ThemeKeys.SecondaryText)
                .FontSize(11);
        }

        return VStack(spacing: 2,
            HStack(spacing: 6,
                Label(headline)
                    .FontSize(12)
                    .FontAttributes(FontAttributes.Bold),
                Label(badgeText)
                    .FontSize(10)
                    .TextColor(Colors.White)
                    .BackgroundColor(badgeColor)
                    .Padding(6, 2)
                    .VCenter()
            ),
            !string.IsNullOrWhiteSpace(result.Explanation)
                ? (VisualNode)Label(result.Explanation!)
                    .ThemeKey(ThemeKeys.SecondaryText)
                    .FontSize(11)
                    .LineBreakMode(Microsoft.Maui.LineBreakMode.WordWrap)
                : null
        );
    }
}
