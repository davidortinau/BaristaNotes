using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Data.Repositories;

/// <summary>
/// Repository interface for Bag entity data access.
/// Handles bag CRUD operations and specialized queries for shot logging workflows.
/// </summary>
public interface IBagRepository
{
    /// <summary>
    /// Creates a new bag in the database.
    /// </summary>
    Task<Bag> CreateAsync(Bag bag);

    /// <summary>
    /// Gets a bag by its unique identifier, including Bean navigation property.
    /// </summary>
    Task<Bag?> GetByIdAsync(int id);

    /// <summary>
    /// Gets all bags for a specific bean.
    /// </summary>
    /// <param name="beanId">Bean ID</param>
    /// <param name="includeCompleted">If true, includes completed bags</param>
    /// <returns>List of bags ordered by roast date descending</returns>
    Task<List<Bag>> GetBagsForBeanAsync(int beanId, bool includeCompleted = true);

    /// <summary>
    /// Gets bag summaries (lightweight DTOs) for a bean.
    /// Includes bean name via navigation property and aggregated shot statistics.
    /// </summary>
    /// <param name="beanId">Bean ID</param>
    /// <param name="includeCompleted">If true, includes completed bags</param>
    /// <returns>List of bag summaries ordered by roast date descending</returns>
    Task<List<BagSummaryDto>> GetBagSummariesForBeanAsync(int beanId, bool includeCompleted = true);

    /// <summary>
    /// Gets active (incomplete) bags for shot logging, ordered by roast date descending.
    /// Includes bean name and shot count.
    /// Uses composite index (BeanId, IsComplete, RoastDate) for performance.
    /// </summary>
    /// <returns>List of active bag summaries</returns>
    Task<List<BagSummaryDto>> GetActiveBagsForShotLoggingAsync();

    /// <summary>
    /// Gets the most recent active (incomplete) bag for a bean (by roast date).
    /// </summary>
    Task<Bag?> GetMostRecentActiveBagForBeanAsync(int beanId);

    /// <summary>
    /// Updates an existing bag.
    /// </summary>
    Task<Bag> UpdateAsync(Bag bag);

    /// <summary>
    /// Soft-deletes a bag (sets IsDeleted=true).
    /// </summary>
    Task DeleteAsync(int id);
}
