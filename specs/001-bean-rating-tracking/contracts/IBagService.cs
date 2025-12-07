using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services;

/// <summary>
/// Service interface for managing Bag entities (physical bags of coffee beans).
/// A Bag represents a specific roast batch of a Bean, distinguished by roasting date.
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
    /// <param name="includeInactive">If true, includes bags with IsActive=false</param>
    /// <returns>List of bags (empty if none found)</returns>
    Task<List<Bag>> GetBagsForBeanAsync(int beanId, bool includeInactive = false);
    
    /// <summary>
    /// Gets bag summaries (lightweight DTOs) for a bean, including shot count and average rating.
    /// </summary>
    /// <param name="beanId">Bean ID</param>
    /// <returns>List of bag summaries ordered by roast date descending</returns>
    Task<List<BagSummaryDto>> GetBagSummariesForBeanAsync(int beanId);
    
    /// <summary>
    /// Gets the most recent bag for a bean (by roast date).
    /// Used for auto-selecting bag when logging shots.
    /// </summary>
    /// <param name="beanId">Bean ID</param>
    /// <returns>Most recent active bag, or null if bean has no bags</returns>
    Task<Bag?> GetMostRecentBagForBeanAsync(int beanId);
    
    /// <summary>
    /// Updates an existing bag's properties.
    /// </summary>
    /// <param name="bag">Bag with updated properties</param>
    /// <returns>Updated bag</returns>
    /// <exception cref="NotFoundException">If bag with given Id doesn't exist</exception>
    /// <exception cref="ValidationException">If validation fails (same rules as Create)</exception>
    Task<OperationResult<Bag>> UpdateBagAsync(Bag bag);
    
    /// <summary>
    /// Soft-deletes a bag (sets IsDeleted=true).
    /// Also soft-deletes all associated shot records.
    /// </summary>
    /// <param name="id">Bag ID to delete</param>
    /// <returns>Success if deleted, failure if not found</returns>
    Task<OperationResult> DeleteBagAsync(int id);
    
    /// <summary>
    /// Marks a bag as inactive (IsActive=false).
    /// Bag remains in database but excluded from bag picker UI.
    /// </summary>
    /// <param name="id">Bag ID</param>
    /// <returns>Success if deactivated, failure if not found</returns>
    Task<OperationResult> DeactivateBagAsync(int id);
}
