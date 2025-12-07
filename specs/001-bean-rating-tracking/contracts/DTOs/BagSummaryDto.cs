namespace BaristaNotes.Core.Services.DTOs;

/// <summary>
/// Lightweight data transfer object for bag list display.
/// Contains basic bag info plus aggregated shot statistics.
/// </summary>
public class BagSummaryDto
{
    /// <summary>
    /// Bag unique identifier.
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Parent bean identifier.
    /// </summary>
    public int BeanId { get; set; }
    
    /// <summary>
    /// Date the coffee was roasted.
    /// </summary>
    public DateTimeOffset RoastDate { get; set; }
    
    /// <summary>
    /// Optional user notes about this bag (e.g., "From Trader Joe's", "Gift").
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Total number of shots logged for this bag.
    /// </summary>
    public int ShotCount { get; set; }
    
    /// <summary>
    /// Average rating for shots from this bag.
    /// Null if no rated shots exist.
    /// </summary>
    public double? AverageRating { get; set; }
    
    /// <summary>
    /// Formatted roast date for display (e.g., "Dec 05, 2025").
    /// </summary>
    public string FormattedRoastDate => RoastDate.ToString("MMM dd, yyyy");
    
    /// <summary>
    /// User-friendly label for bag selection (e.g., "Roasted Dec 05, 2025 - From Trader Joe's").
    /// Used in picker controls and list views.
    /// </summary>
    public string DisplayLabel => 
        $"Roasted {FormattedRoastDate}" + 
        (Notes != null ? $" - {Notes}" : "");
    
    /// <summary>
    /// Formatted average rating for display (e.g., "4.5" or "No ratings").
    /// </summary>
    public string FormattedRating => 
        AverageRating.HasValue ? AverageRating.Value.ToString("F1") : "No ratings";
}
