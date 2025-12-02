using BaristaNotes.Core.Models;

namespace BaristaNotes.Core.Data.Repositories;

public interface IShotRecordRepository : IRepository<ShotRecord>
{
    Task<ShotRecord?> GetMostRecentAsync();
    Task<List<ShotRecord>> GetHistoryAsync(int pageIndex, int pageSize);
    Task<List<ShotRecord>> GetByUserAsync(int userProfileId, int pageIndex, int pageSize);
    Task<List<ShotRecord>> GetByBeanAsync(int beanId, int pageIndex, int pageSize);
    Task<List<ShotRecord>> GetByEquipmentAsync(int equipmentId, int pageIndex, int pageSize);
    Task<int> GetTotalCountAsync();
}
