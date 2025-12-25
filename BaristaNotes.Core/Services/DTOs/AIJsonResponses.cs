using System.ComponentModel;
using System.Text.Json.Serialization;

namespace BaristaNotes.Core.Services.DTOs;

/// <summary>
/// JSON schema DTO for AI shot advice responses.
/// Used with IChatClient.GetResponseAsync{T}() for structured output.
/// </summary>
public sealed record ShotAdviceJson
{
    /// <summary>
    /// List of specific parameter adjustments to try.
    /// </summary>
    [JsonPropertyName("adjustments")]
    [Description("List of 1-3 specific parameter changes to improve the shot")]
    public List<ShotAdjustment> Adjustments { get; init; } = [];

    /// <summary>
    /// Brief explanation of why these adjustments are recommended.
    /// </summary>
    [JsonPropertyName("reasoning")]
    [Description("Single sentence explaining the root cause or reasoning behind the adjustments")]
    public string Reasoning { get; init; } = string.Empty;
}

/// <summary>
/// A specific shot parameter adjustment recommendation.
/// </summary>
public sealed record ShotAdjustment
{
    /// <summary>
    /// The parameter to adjust (e.g., "dose", "grind", "yield", "time").
    /// </summary>
    [JsonPropertyName("parameter")]
    [Description("The shot parameter to change: dose, grind, yield, or time")]
    public string Parameter { get; init; } = string.Empty;

    /// <summary>
    /// The direction of change (e.g., "increase", "decrease", "finer", "coarser").
    /// </summary>
    [JsonPropertyName("direction")]
    [Description("Direction of change: increase, decrease, finer, coarser")]
    public string Direction { get; init; } = string.Empty;

    /// <summary>
    /// The specific amount to change (e.g., "0.5g", "2 clicks", "3 seconds").
    /// </summary>
    [JsonPropertyName("amount")]
    [Description("Specific amount to change (e.g., '0.5g', '2 clicks', '3 seconds')")]
    public string Amount { get; init; } = string.Empty;
}

/// <summary>
/// JSON schema DTO for AI bean recommendation responses.
/// Used with IChatClient.GetResponseAsync{T}() for structured output.
/// </summary>
public sealed record BeanRecommendationJson
{
    /// <summary>
    /// Recommended dose in grams.
    /// </summary>
    [JsonPropertyName("dose")]
    [Description("Recommended dose in grams, typically 16-20g")]
    public decimal Dose { get; init; }

    /// <summary>
    /// Recommended grind setting description.
    /// </summary>
    [JsonPropertyName("grind")]
    [Description("Grind setting recommendation (e.g., 'medium-fine', 'finer than medium')")]
    public string Grind { get; init; } = string.Empty;

    /// <summary>
    /// Target yield/output in grams.
    /// </summary>
    [JsonPropertyName("output")]
    [Description("Target yield in grams, typically 1:2 to 1:2.5 ratio")]
    public decimal Output { get; init; }

    /// <summary>
    /// Target extraction time in seconds.
    /// </summary>
    [JsonPropertyName("duration")]
    [Description("Target extraction time in seconds, typically 25-35")]
    public decimal Duration { get; init; }
}
