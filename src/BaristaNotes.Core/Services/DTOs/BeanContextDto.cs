namespace BaristaNotes.Core.Services.DTOs;

/// <summary>
/// Bean information for AI context.
/// </summary>
public record BeanContextDto
{
    /// <summary>
    /// Bean name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Roaster name.
    /// </summary>
    public string? Roaster { get; init; }

    /// <summary>
    /// Coffee origin (country/region).
    /// </summary>
    public string? Origin { get; init; }

    /// <summary>
    /// When beans were roasted.
    /// </summary>
    public DateTime RoastDate { get; init; }

    /// <summary>
    /// Calculated: days since roast.
    /// </summary>
    public int DaysFromRoast { get; init; }

    /// <summary>
    /// Bean notes (flavor profile, etc.).
    /// </summary>
    public string? Notes { get; init; }
}
