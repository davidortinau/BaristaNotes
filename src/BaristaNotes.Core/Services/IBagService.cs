using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services;

/// <summary>
/// Service interface for managing Bag entities (physical bags of coffee beans).
/// A Bag represents a specific roast batch of a Bean, distinguished by roasting date.
/// Bags can be marked as "complete" when finished to hide them from shot logging workflows.
/// </summary>
public interface IBagService
{
    /// <summary>
    /// Creates a new bag for an existing bean.
    /// </summary>
    /// <param name="bag">Bag to create (Id will be auto-generated)</param>
    /// <returns>Created bag with generated Id</returns>
    /// <exception cref="ValidationException">
    /// - If BeanId references non-existent bean
    /// - If RoastDate is in the future
    /// - If Notes exceed 500 characters
    /// </exception>
    Task<OperationResult<Bag>> CreateBagAsync(Bag bag);
    
    /// <summary>
    /// Gets a bag by its unique identifier.
    /// </summary>
    /// <param name="id">Bag ID</param>
    /// <returns>Bag if found, null otherwise</returns>
    Task<Bag?> GetBagByIdAsync(int id);
    
    /// <summary>
    /// Gets all bags for a specific bean, ordered by roast date descending (newest first).
    /// </summary>
    /// <param name="beanId">Bean ID</param>
    /// <param name="includeCompleted">If true, includes bags marked as complete</param>
    /// <returns>List of bags (empty if none found)</returns>
    Task<List<Bag>> GetBagsForBeanAsync(int beanId, bool includeCompleted = true);
    
    /// <summary>
    /// Gets active (incomplete) bags for shot logging, ordered by roast date descending.
    /// Used in shot logging page bag picker.
    /// </summary>
    /// <returns>List of active bag summaries with bean names</returns>
    Task<List<BagSummaryDto>> GetActiveBagsForShotLoggingAsync();
    
    /// <summary>
    /// Gets bag summaries (lightweight DTOs) for a bean, including shot count and average rating.
    /// Used in bean detail page to show bag history.
    /// </summary>
    /// <param name="beanId">Bean ID</param>
    /// <param name="includeCompleted">If true, includes completed bags</param>
    /// <returns>List of bag summaries ordered by roast date descending</returns>
    Task<List<BagSummaryDto>> GetBagSummariesForBeanAsync(int beanId, bool includeCompleted = true);
    
    /// <summary>
    /// Gets the most recent active (incomplete) bag for a bean (by roast date).
    /// Used for auto-selecting bag when logging shots from bean context.
    /// </summary>
    /// <param name="beanId">Bean ID</param>
    /// <returns>Most recent active bag, or null if bean has no active bags</returns>
    Task<Bag?> GetMostRecentActiveBagForBeanAsync(int beanId);
    
    /// <summary>
    /// Updates an existing bag's properties.
    /// </summary>
    /// <param name="bag">Bag with updated properties</param>
    /// <returns>Updated bag</returns>
    /// <exception cref="NotFoundException">If bag with given Id doesn't exist</exception>
    /// <exception cref="ValidationException">If validation fails (same rules as Create)</exception>
    Task<OperationResult<Bag>> UpdateBagAsync(Bag bag);
    
    /// <summary>
    /// Marks a bag as complete (finished/empty).
    /// Completed bags are excluded from shot logging bag picker but remain in history views.
    /// </summary>
    /// <param name="id">Bag ID</param>
    /// <exception cref="NotFoundException">If bag not found</exception>
    Task MarkBagCompleteAsync(int id);
    
    /// <summary>
    /// Reactivates a completed bag (unmarks as complete).
    /// Allows user to log more shots to a previously completed bag.
    /// </summary>
    /// <param name="id">Bag ID</param>
    /// <exception cref="NotFoundException">If bag not found</exception>
    Task ReactivateBagAsync(int id);
    
    /// <summary>
    /// Soft-deletes a bag (sets IsDeleted=true).
    /// Also soft-deletes all associated shot records.
    /// </summary>
    /// <param name="id">Bag ID to delete</param>
    /// <exception cref="NotFoundException">If bag not found</exception>
    Task DeleteBagAsync(int id);
}
