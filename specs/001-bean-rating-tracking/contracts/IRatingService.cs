using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services;

/// <summary>
/// Service interface for calculating rating aggregates (averages and distributions).
/// Performs on-demand calculations from ShotRecord data.
/// </summary>
public interface IRatingService
{
    /// <summary>
    /// Calculates aggregate rating for a bean (across all bags).
    /// </summary>
    /// <param name="beanId">Bean ID</param>
    /// <returns>
    /// Aggregate with average rating, total shots, rated shots, and distribution.
    /// Returns empty aggregate (AverageRating=0, TotalShots=0) if bean has no shots.
    /// </returns>
    Task<RatingAggregateDto> GetBeanRatingAsync(int beanId);
    
    /// <summary>
    /// Calculates aggregate rating for a specific bag.
    /// </summary>
    /// <param name="bagId">Bag ID</param>
    /// <returns>
    /// Aggregate with average rating, total shots, rated shots, and distribution.
    /// Returns empty aggregate if bag has no shots.
    /// </returns>
    Task<RatingAggregateDto> GetBagRatingAsync(int bagId);
    
    /// <summary>
    /// Gets rating aggregates for multiple bags (batch query optimization).
    /// Used when displaying list of bags with their individual ratings.
    /// </summary>
    /// <param name="bagIds">Collection of Bag IDs</param>
    /// <returns>Dictionary mapping BagId → RatingAggregateDto</returns>
    Task<Dictionary<int, RatingAggregateDto>> GetBagRatingsBatchAsync(IEnumerable<int> bagIds);
}
