using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services;

public interface IBeanService
{
    Task<List<BeanDto>> GetAllActiveBeansAsync();
    Task<BeanDto?> GetBeanByIdAsync(int id);
    Task<OperationResult<BeanDto>> CreateBeanAsync(CreateBeanDto dto);
    Task<BeanDto> UpdateBeanAsync(int id, UpdateBeanDto dto);
    Task ArchiveBeanAsync(int id);
    Task DeleteBeanAsync(int id);
}
