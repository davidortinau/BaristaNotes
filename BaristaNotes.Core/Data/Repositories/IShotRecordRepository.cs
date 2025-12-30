using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Data.Repositories;

public interface IShotRecordRepository : IRepository<ShotRecord>
{
    Task<ShotRecord?> GetMostRecentAsync();
    Task<List<ShotRecord>> GetHistoryAsync(int pageIndex, int pageSize);
    Task<List<ShotRecord>> GetByUserAsync(int userProfileId, int pageIndex, int pageSize);
    Task<List<ShotRecord>> GetByBeanAsync(int beanId, int pageIndex, int pageSize);
    Task<List<ShotRecord>> GetByEquipmentAsync(int equipmentId, int pageIndex, int pageSize);
    Task<int> GetTotalCountAsync();
    
    /// <summary>
    /// Gets shot records filtered by specified criteria with pagination.
    /// Applies AND logic for multiple criteria.
    /// </summary>
    Task<List<ShotRecord>> GetFilteredAsync(ShotFilterCriteriaDto? criteria, int pageIndex, int pageSize);
    
    /// <summary>
    /// Gets total count of shots matching filter criteria.
    /// </summary>
    Task<int> GetFilteredCountAsync(ShotFilterCriteriaDto? criteria);
    
    /// <summary>
    /// Gets distinct bean IDs that have associated shot records.
    /// </summary>
    Task<List<int>> GetBeanIdsWithShotsAsync();
    
    /// <summary>
    /// Gets distinct MadeFor user profile IDs from shot records.
    /// </summary>
    Task<List<int>> GetMadeForIdsWithShotsAsync();
}
