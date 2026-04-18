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
    /// Range: 0.00 - 4.00 (5-level scale: 0=Terrible, 1=Bad, 2=Average, 3=Good, 4=Excellent)
    /// </summary>
    public double AverageRating { get; set; }
    
    /// <summary>
    /// Total number of shots (all shots have ratings - this equals RatedShots).
    /// </summary>
    public int TotalShots { get; set; }
    
    /// <summary>
    /// Number of shots with ratings (equals TotalShots since all shots are rated).
    /// </summary>
    public int RatedShots { get; set; }
    
    /// <summary>
    /// Rating distribution: Rating value (0-4) → Count of shots with that rating.
    /// Example: { 4: 10, 3: 5, 2: 2, 1: 1, 0: 0 }
    /// </summary>
    public Dictionary<int, int> Distribution { get; set; } = new();
    
    /// <summary>
    /// Convenience property: True if at least one shot has a rating.
    /// </summary>
    public bool HasRatings => RatedShots > 0;
    
    /// <summary>
    /// Formatted average rating for display (e.g., "2.9 / 4").
    /// Returns "N/A" if no ratings exist.
    /// </summary>
    public string FormattedAverage => HasRatings ? $"{AverageRating:F1} / 4" : "N/A";
    
    /// <summary>
    /// Gets count for a specific rating level (0-4).
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
