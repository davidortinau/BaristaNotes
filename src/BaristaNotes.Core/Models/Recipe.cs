using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Models;

/// <summary>
/// A brewing recipe for a specific <see cref="Bean"/> and <see cref="BrewMethod"/>.
/// A bean may have multiple recipes (one per method, or user-authored variants).
/// </summary>
public class Recipe
{
    public int Id { get; set; }

    public int BeanId { get; set; }

    public BrewMethod BrewMethod { get; set; }

    public RecipeSource Source { get; set; }

    /// <summary>URL the recipe was scraped from (for attribution). Null for AI/Manual.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>Optional display title (e.g., "Onyx Espresso Recipe"). Falls back to BrewMethod.</summary>
    public string? Title { get; set; }

    // Shared core parameters (nullable — not every method uses each).
    public decimal? DoseIn { get; set; }
    public decimal? OutputAmount { get; set; }
    public string? GrindHint { get; set; }
    public decimal? BrewTempC { get; set; }
    public decimal? TotalTimeSeconds { get; set; }

    /// <summary>
    /// JSON blob with method-specific parameters that don't fit the core columns
    /// (e.g., bloom time, pour schedule, preinfusion for espresso, agitation).
    /// </summary>
    public string? ParametersJson { get; set; }

    /// <summary>Free-form recipe notes / instructions.</summary>
    public string? Notes { get; set; }

    public DateTime FetchedAt { get; set; }

    /// <summary>True once the user has edited this recipe locally.</summary>
    public bool IsEditedByUser { get; set; }

    // CoreSync metadata
    public Guid SyncId { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Bean Bean { get; set; } = null!;
}
