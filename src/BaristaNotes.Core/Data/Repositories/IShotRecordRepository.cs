using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Models.Enums;

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

    /// <summary>
    /// Returns the most recent (non-deleted) shot record for a given grinder
    /// that has a non-null <c>GrindMicrons</c>. Optionally filter by bean and
    /// brew method for higher-relevance history lookups. Used by the grind
    /// translator to seed a suggested default setting from past micron values.
    /// </summary>
    Task<ShotRecord?> GetMostRecentWithGrindAsync(int grinderId, int? beanId = null, BrewMethod? method = null);

    /// <summary>
    /// Returns the most recent (non-deleted) shot record for a given bean and
    /// brew method that has a non-null <c>GrindMicrons</c>. Grinder-agnostic —
    /// microns transfer across grinders, so this is the canonical lookup
    /// when defaulting the grind picker for a freshly opened brew of a
    /// bean+method combination.
    /// </summary>
    Task<int?> GetMostRecentMicronsByBeanAsync(int beanId, BrewMethod method);
}
