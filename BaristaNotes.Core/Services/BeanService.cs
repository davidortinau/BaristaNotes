using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BaristaNotes.Core.Services;

public class BeanService : IBeanService
{
    private readonly IBeanRepository _beanRepository;
    private readonly IRatingService _ratingService;
    
    public BeanService(IBeanRepository beanRepository, IRatingService ratingService)
    {
        _beanRepository = beanRepository;
        _ratingService = ratingService;
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
    
    public async Task<BeanDto?> GetBeanWithRatingsAsync(int id)
    {
        var bean = await _beanRepository.GetByIdAsync(id);
        if (bean == null) return null;
        
        var ratings = await _ratingService.GetBeanRatingAsync(id);
        
        return MapToDto(bean) with { RatingAggregate = ratings };
    }
    
    public async Task<OperationResult<BeanDto>> CreateBeanAsync(CreateBeanDto dto)
    {
        try
        {
            ValidateCreateBean(dto);
        }
        catch (ValidationException ex)
        {
            var firstError = ex.Errors.FirstOrDefault();
            var errorMessage = firstError.Value?.FirstOrDefault() ?? "Validation failed";
            return OperationResult<BeanDto>.Fail(
                errorMessage,
                "Please correct the errors and try again"
            );
        }
        
        try
        {
            var bean = new Bean
            {
                Name = dto.Name,
                Roaster = dto.Roaster,
                // TODO: RoastDate moved to Bag entity - will be handled in T041 (BagFormPage)
                // Temporarily removed to allow migration generation
                Origin = dto.Origin,
                Notes = dto.Notes,
                IsActive = true,
                CreatedAt = DateTimeOffset.Now,
                SyncId = Guid.NewGuid(),
                LastModifiedAt = DateTimeOffset.Now
            };
            
            var created = await _beanRepository.AddAsync(bean);
            return OperationResult<BeanDto>.Ok(MapToDto(created), $"{dto.Name} saved successfully");
        }
        catch (DbUpdateException)
        {
            return OperationResult<BeanDto>.Fail(
                "Failed to save bean to database",
                "Check your connection and try again",
                "DB_UPDATE_FAILED"
            );
        }
        catch (Exception)
        {
            return OperationResult<BeanDto>.Fail(
                "An unexpected error occurred",
                "Please try again later",
                "UNKNOWN_ERROR"
            );
        }
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
        // TODO: RoastDate moved to Bag entity - dto.RoastDate temporarily ignored
        // if (dto.RoastDate.HasValue)
        //     bean.RoastDate = dto.RoastDate;
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
        RoastDate = null, // TODO: RoastDate moved to Bag - will populate from first Bag in later tasks
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
