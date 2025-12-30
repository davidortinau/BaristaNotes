using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services;

public interface IShotService
{
    Task<ShotRecordDto?> GetMostRecentShotAsync();
    Task<ShotRecordDto> CreateShotAsync(CreateShotDto dto);
    Task<ShotRecordDto> UpdateShotAsync(int id, UpdateShotDto dto);
    Task DeleteShotAsync(int id);
    Task<PagedResult<ShotRecordDto>> GetShotHistoryAsync(int pageIndex, int pageSize);
    Task<PagedResult<ShotRecordDto>> GetShotHistoryByUserAsync(int userProfileId, int pageIndex, int pageSize);
    Task<PagedResult<ShotRecordDto>> GetShotHistoryByBeanAsync(int beanId, int pageIndex, int pageSize);
    Task<PagedResult<ShotRecordDto>> GetShotHistoryByEquipmentAsync(int equipmentId, int pageIndex, int pageSize);
    Task<ShotRecordDto?> GetShotByIdAsync(int id);
    Task<ShotRecordDto?> GetBestRatedShotByBeanAsync(int beanId);
    Task<ShotRecordDto?> GetBestRatedShotByBagAsync(int bagId);
    
    /// <summary>
    /// Gets shot history filtered by specified criteria with pagination.
    /// </summary>
    /// <param name="criteria">Filter criteria (null or empty lists = no filter)</param>
    /// <param name="pageIndex">Zero-based page index</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paged result with filtered shots</returns>
    Task<PagedResult<ShotRecordDto>> GetFilteredShotHistoryAsync(
        ShotFilterCriteriaDto? criteria,
        int pageIndex,
        int pageSize);
    
    /// <summary>
    /// Gets beans that have at least one shot record.
    /// Used to populate filter options.
    /// </summary>
    /// <returns>List of beans with shot history</returns>
    Task<List<BeanFilterOptionDto>> GetBeansWithShotsAsync();
    
    /// <summary>
    /// Gets user profiles that have been marked as "Made For" on at least one shot.
    /// Used to populate filter options.
    /// </summary>
    /// <returns>List of people with shot history</returns>
    Task<List<UserProfileDto>> GetPeopleWithShotsAsync();
    
    /// <summary>
    /// Gets shot context data formatted for AI analysis.
    /// Includes current shot, historical shots for same bag, and bean info.
    /// </summary>
    /// <param name="shotId">The shot to get context for.</param>
    /// <returns>AI advice request context, or null if shot not found.</returns>
    Task<AIAdviceRequestDto?> GetShotContextForAIAsync(int shotId);

    /// <summary>
    /// Gets the bean ID of the most recently logged shot.
    /// </summary>
    /// <returns>Bean ID of most recent shot, or null if no shots exist.</returns>
    Task<int?> GetMostRecentBeanIdAsync();

    /// <summary>
    /// Checks if a bean has any shot history.
    /// </summary>
    /// <param name="beanId">The bean ID to check.</param>
    /// <returns>True if the bean has at least one logged shot.</returns>
    Task<bool> BeanHasHistoryAsync(int beanId);

    /// <summary>
    /// Builds the context required for AI bean recommendations.
    /// </summary>
    /// <param name="beanId">The ID of the bean.</param>
    /// <returns>Bean recommendation context, or null if bean not found.</returns>
    Task<BeanRecommendationContextDto?> GetBeanRecommendationContextAsync(int beanId);
}
