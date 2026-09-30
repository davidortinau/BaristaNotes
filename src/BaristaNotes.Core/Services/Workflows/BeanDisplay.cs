using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Grind;

namespace BaristaNotes.Core.Services.Workflows;

public enum GrindTranslationBadgeKind
{
    UserHistory,
    Calculated,
    KnownMatch,
    AI,
    Estimate
}

public sealed record GrindTranslationDisplay(
    string? Headline,
    string Badge,
    GrindTranslationBadgeKind BadgeKind,
    string? Explanation);

public static class BeanDisplay
{
    private static readonly string[] RatingGlyphs = ["\ue814", "\ue811", "\ue812", "\ue0ed", "\ue815"];

    public static string RatingGlyph(int rating) =>
        RatingGlyphs[rating is >= 0 and <= 4 ? rating : 2];

    public static string Subtitle(BeanDto bean) =>
        !string.IsNullOrWhiteSpace(bean.Roaster) ? bean.Roaster.ToUpperInvariant()
        : !string.IsNullOrWhiteSpace(bean.Origin) ? bean.Origin.ToUpperInvariant()
        : "BEAN";

    public static bool HasRecipeParameters(RecipeDto recipe) =>
        recipe.DoseIn.HasValue || recipe.OutputAmount.HasValue
        || recipe.TotalTimeSeconds.HasValue || recipe.BrewTempC.HasValue;

    public static string RecipeSourceText(RecipeSource source) => source switch
    {
        RecipeSource.RoasterSite => "ROASTER",
        RecipeSource.AIGenerated => "AI",
        RecipeSource.Manual => "CUSTOM",
        _ => source.ToString().ToUpperInvariant()
    };

    public static string RecipeParameters(RecipeDto recipe)
    {
        var parts = new List<string>();
        if (recipe.DoseIn.HasValue) parts.Add($"{recipe.DoseIn:0.#}g in");
        if (recipe.OutputAmount.HasValue) parts.Add($"{recipe.OutputAmount:0.#}g out");
        if (recipe.TotalTimeSeconds is decimal seconds)
            parts.Add(seconds >= 60
                ? $"{(int)(seconds / 60)}:{(int)(seconds % 60):D2}"
                : $"{seconds:0}s");
        if (recipe.BrewTempC.HasValue) parts.Add($"{recipe.BrewTempC:0.#}°C");
        return parts.Count == 0 ? "No parameters captured." : string.Join(" · ", parts);
    }

    public static GrindTranslationDisplay Translation(GrindTranslationResult result)
    {
        string? headline;
        if (result.SuggestedSetting.HasValue)
        {
            headline = result.MinSetting.HasValue && result.MaxSetting.HasValue
                && result.MinSetting != result.MaxSetting
                    ? $"On {result.GrinderModel}: {result.MinSetting:0.#}–{result.MaxSetting:0.#} (try {result.SuggestedSetting:0.#})"
                    : $"On {result.GrinderModel}: {result.SuggestedSetting:0.#}";
        }
        else
        {
            headline = result.MinSetting.HasValue && result.MaxSetting.HasValue
                ? $"On {result.GrinderModel}: {result.MinSetting:0.#}–{result.MaxSetting:0.#}"
                : null;
        }

        var (badge, kind) = result.Source switch
        {
            GrindTranslationSource.UserHistory => ("From your history", GrindTranslationBadgeKind.UserHistory),
            GrindTranslationSource.Deterministic => ("Calculated", GrindTranslationBadgeKind.Calculated),
            GrindTranslationSource.Cache => ("Known match", GrindTranslationBadgeKind.KnownMatch),
            GrindTranslationSource.AI => ("AI", GrindTranslationBadgeKind.AI),
            _ => ("Estimate", GrindTranslationBadgeKind.Estimate)
        };
        return new(headline, badge, kind, result.Explanation);
    }

    public static string ShotRecipe(ShotRecordDto shot) =>
        $"{shot.DoseIn:F1}g in → {(shot.ActualOutput ?? shot.ExpectedOutput):F1}g out " +
        $"({(shot.ActualTime ?? shot.ExpectedTime):F1}s)";

    public static string? ShotPeople(ShotRecordDto shot, DateTimeOffset now)
    {
        var people = new List<string>();
        if (shot.MadeBy is not null) people.Add($"By: {shot.MadeBy.Name}");
        if (shot.MadeFor is not null) people.Add($"For: {shot.MadeFor.Name}");
        return people.Count == 0 ? null : $"{ShotAge(shot.Timestamp, now)} • {string.Join(" • ", people)}";
    }

    public static string ShotAge(DateTimeOffset timestamp, DateTimeOffset now)
    {
        var difference = now - timestamp;
        if (difference.TotalMinutes < 1) return "Just now";
        if (difference.TotalMinutes < 60) return $"{(int)difference.TotalMinutes}m ago";
        if (difference.TotalHours < 24) return timestamp.ToString("h:mm tt");
        if (difference.TotalDays < 7) return timestamp.ToString("ddd h:mm tt");
        return timestamp.ToString("MMM d, h:mm tt");
    }
}
