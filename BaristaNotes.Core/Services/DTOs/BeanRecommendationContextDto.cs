namespace BaristaNotes.Core.Services.DTOs;

/// <summary>
/// Context data for AI bean recommendations.
/// </summary>
public record BeanRecommendationContextDto
{
    /// <summary>
    /// Bean identifier.
    /// </summary>
    public int BeanId { get; init; }

    /// <summary>
    /// Bean name.
    /// </summary>
    public string BeanName { get; init; } = string.Empty;

    /// <summary>
    /// Roaster name.
    /// </summary>
    public string? Roaster { get; init; }

    /// <summary>
    /// Origin country/region.
    /// </summary>
    public string? Origin { get; init; }

    /// <summary>
    /// Flavor profile notes.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Most recent bag's roast date.
    /// </summary>
    public DateTime? RoastDate { get; init; }

    /// <summary>
    /// Days since roast (calculated).
    /// </summary>
    public int? DaysFromRoast { get; init; }

    /// <summary>
    /// Whether shots exist for this bean.
    /// </summary>
    public bool HasHistory { get; init; }

    /// <summary>
    /// Up to 10 best-rated historical shots (if HasHistory is true).
    /// </summary>
    public List<ShotContextDto>? HistoricalShots { get; init; }

    /// <summary>
    /// Current machine/grinder equipment.
    /// </summary>
    public EquipmentContextDto? Equipment { get; init; }
}
