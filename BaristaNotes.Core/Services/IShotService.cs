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
}
