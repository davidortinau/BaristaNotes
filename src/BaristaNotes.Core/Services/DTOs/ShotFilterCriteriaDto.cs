namespace BaristaNotes.Core.Services.DTOs;

/// <summary>
/// Filter criteria for shot history queries.
/// Null or empty lists mean "no filter for this field".
/// </summary>
public record ShotFilterCriteriaDto
{
    /// <summary>
    /// Filter by bean IDs. Null/empty = include all beans.
    /// </summary>
    public IReadOnlyList<int>? BeanIds { get; init; }
    
    /// <summary>
    /// Filter by "Made For" user profile IDs. Null/empty = include all.
    /// </summary>
    public IReadOnlyList<int>? MadeForIds { get; init; }
    
    /// <summary>
    /// Filter by rating values (0-4). Null/empty = include all ratings.
    /// </summary>
    public IReadOnlyList<int>? Ratings { get; init; }
    
    /// <summary>
    /// Returns true if any filter criteria is specified.
    /// </summary>
    public bool HasFilters => 
        (BeanIds?.Count ?? 0) > 0 ||
        (MadeForIds?.Count ?? 0) > 0 ||
        (Ratings?.Count ?? 0) > 0;
}
