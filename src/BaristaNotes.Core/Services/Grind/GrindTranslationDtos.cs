using System.ComponentModel;
using System.Text.Json.Serialization;

namespace BaristaNotes.Core.Services.Grind;

/// <summary>
/// Source of the final translation result — useful for the UI badge.
/// </summary>
public enum GrindTranslationSource
{
    UserHistory,
    Deterministic,
    Cache,
    AI,
    Default,
}

/// <summary>Input to <see cref="IGrindTranslationService.TranslateAsync"/>.</summary>
public sealed record GrindTranslationRequest(
    int? EquipmentId,
    string GrinderModel,
    string GrindHint,
    Models.Enums.BrewMethod Method,
    int? BeanId);

/// <summary>Final translated grind-setting recommendation.</summary>
public sealed record GrindTranslationResult(
    decimal? MinSetting,
    decimal? MaxSetting,
    decimal? SuggestedSetting,
    GrindHintKind ParsedKind,
    GrindTranslationSource Source,
    string ConfidenceLabel,
    string? Explanation,
    string GrinderModel);

/// <summary>Structured JSON response schema requested from the AI provider.</summary>
public sealed class GrindTranslationAIResponse
{
    [JsonPropertyName("min_setting")]
    [Description("Minimum grinder setting on the grinder's native scale for the requested grind hint and brew method.")]
    public decimal? MinSetting { get; set; }

    [JsonPropertyName("max_setting")]
    [Description("Maximum grinder setting on the grinder's native scale.")]
    public decimal? MaxSetting { get; set; }

    [JsonPropertyName("suggested_setting")]
    [Description("Best single recommended starting setting, sitting between min_setting and max_setting.")]
    public decimal? SuggestedSetting { get; set; }

    [JsonPropertyName("confidence")]
    [Description("Your confidence in this recommendation: low, medium, or high.")]
    public string Confidence { get; set; } = "low";

    [JsonPropertyName("explanation")]
    [Description("One-sentence plain-English explanation suitable for a tooltip.")]
    public string? Explanation { get; set; }

    [JsonPropertyName("micron_anchors")]
    [Description("Optional list of (micron, setting) calibration anchors for this grinder, if you are confident about them. These feed future calculations.")]
    public List<GrindTranslationAnchorDto>? MicronAnchors { get; set; }
}

public sealed class GrindTranslationAnchorDto
{
    [JsonPropertyName("micron")]
    public decimal Micron { get; set; }

    [JsonPropertyName("setting")]
    public decimal Setting { get; set; }
}
