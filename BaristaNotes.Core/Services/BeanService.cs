using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;

namespace BaristaNotes.Core.Services;

public class BeanService : IBeanService
{
    private readonly IBeanRepository _beanRepository;
    
    public BeanService(IBeanRepository beanRepository)
    {
        _beanRepository = beanRepository;
    }
    
    public async Task<List<BeanDto>> GetAllActiveBeansAsync()
    {
        var beans = await _beanRepository.GetActiveBeansAsync();
        return beans.Select(MapToDto).ToList();
    }
    
    public async Task<BeanDto?> GetBeanByIdAsync(int id)
    {
        var bean = await _beanRepository.GetByIdAsync(id);
        return bean == null ? null : MapToDto(bean);
    }
    
    public async Task<BeanDto> CreateBeanAsync(CreateBeanDto dto)
    {
        ValidateCreateBean(dto);
        
        var bean = new Bean
        {
            Name = dto.Name,
            Roaster = dto.Roaster,
            RoastDate = dto.RoastDate,
            Origin = dto.Origin,
            Notes = dto.Notes,
            IsActive = true,
            CreatedAt = DateTimeOffset.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        var created = await _beanRepository.AddAsync(bean);
        return MapToDto(created);
    }
    
    public async Task<BeanDto> UpdateBeanAsync(int id, UpdateBeanDto dto)
    {
        var bean = await _beanRepository.GetByIdAsync(id);
        if (bean == null)
            throw new EntityNotFoundException(nameof(Bean), id);
        
        if (dto.Name != null)
            bean.Name = dto.Name;
        if (dto.Roaster != null)
            bean.Roaster = dto.Roaster;
        if (dto.RoastDate.HasValue)
            bean.RoastDate = dto.RoastDate;
        if (dto.Origin != null)
            bean.Origin = dto.Origin;
        if (dto.Notes != null)
            bean.Notes = dto.Notes;
        if (dto.IsActive.HasValue)
            bean.IsActive = dto.IsActive.Value;
        
        bean.LastModifiedAt = DateTimeOffset.Now;
        
        var updated = await _beanRepository.UpdateAsync(bean);
        return MapToDto(updated);
    }
    
    public async Task ArchiveBeanAsync(int id)
    {
        var bean = await _beanRepository.GetByIdAsync(id);
        if (bean == null)
            throw new EntityNotFoundException(nameof(Bean), id);
        
        bean.IsActive = false;
        bean.LastModifiedAt = DateTimeOffset.Now;
        await _beanRepository.UpdateAsync(bean);
    }
    
    public async Task DeleteBeanAsync(int id)
    {
        var bean = await _beanRepository.GetByIdAsync(id);
        if (bean == null)
            throw new EntityNotFoundException(nameof(Bean), id);
        
        bean.IsDeleted = true;
        bean.LastModifiedAt = DateTimeOffset.Now;
        await _beanRepository.UpdateAsync(bean);
    }
    
    private BeanDto MapToDto(Bean bean) => new()
    {
        Id = bean.Id,
        Name = bean.Name,
        Roaster = bean.Roaster,
        RoastDate = bean.RoastDate,
        Origin = bean.Origin,
        Notes = bean.Notes,
        IsActive = bean.IsActive,
        CreatedAt = bean.CreatedAt
    };
    
    private void ValidateCreateBean(CreateBeanDto dto)
    {
        var errors = new Dictionary<string, List<string>>();
        
        if (string.IsNullOrWhiteSpace(dto.Name))
            errors.Add(nameof(dto.Name), new List<string> { "Name is required" });
        else if (dto.Name.Length > 100)
            errors.Add(nameof(dto.Name), new List<string> { "Name must be 100 characters or less" });
        
        if (dto.Roaster?.Length > 100)
            errors.Add(nameof(dto.Roaster), new List<string> { "Roaster must be 100 characters or less" });
        
        if (dto.Origin?.Length > 100)
            errors.Add(nameof(dto.Origin), new List<string> { "Origin must be 100 characters or less" });
        
        if (dto.Notes?.Length > 500)
            errors.Add(nameof(dto.Notes), new List<string> { "Notes must be 500 characters or less" });
        
        if (dto.RoastDate.HasValue && dto.RoastDate.Value > DateTimeOffset.Now)
            errors.Add(nameof(dto.RoastDate), new List<string> { "Roast date cannot be in the future" });
        
        if (errors.Any())
            throw new ValidationException(errors);
    }
}
