namespace BaristaNotes.Core.Services.DTOs;

/// <summary>
/// Data transfer object containing aggregate rating statistics.
/// Used for displaying rating summaries in UI (bean-level or bag-level).
/// </summary>
public class RatingAggregateDto
{
    /// <summary>
    /// Average rating across all rated shots.
    /// 0.00 if no rated shots exist.
    /// Range: 0.00 - 5.00
    /// </summary>
    public double AverageRating { get; set; }
    
    /// <summary>
    /// Total number of shots (including those without ratings).
    /// </summary>
    public int TotalShots { get; set; }
    
    /// <summary>
    /// Number of shots with non-null ratings.
    /// RatedShots <= TotalShots always.
    /// </summary>
    public int RatedShots { get; set; }
    
    /// <summary>
    /// Rating distribution: Rating value (1-5) → Count of shots with that rating.
    /// Example: { 5: 10, 4: 5, 3: 2, 2: 0, 1: 1 }
    /// </summary>
    public Dictionary<int, int> Distribution { get; set; } = new();
    
    /// <summary>
    /// Convenience property: True if at least one shot has a rating.
    /// </summary>
    public bool HasRatings => RatedShots > 0;
    
    /// <summary>
    /// Formatted average rating for display (e.g., "4.25").
    /// Returns "N/A" if no ratings exist.
    /// </summary>
    public string FormattedAverage => HasRatings ? AverageRating.ToString("F2") : "N/A";
    
    /// <summary>
    /// Gets count for a specific rating level (1-5).
    /// Returns 0 if rating level has no shots.
    /// </summary>
    public int GetCountForRating(int rating)
    {
        return Distribution.TryGetValue(rating, out var count) ? count : 0;
    }
    
    /// <summary>
    /// Gets percentage of shots at a specific rating level (0-100).
    /// Returns 0.0 if no rated shots exist.
    /// </summary>
    public double GetPercentageForRating(int rating)
    {
        if (RatedShots == 0) return 0.0;
        return (GetCountForRating(rating) / (double)RatedShots) * 100.0;
    }
}
