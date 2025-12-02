using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;

namespace BaristaNotes.Core.Services;

public class ShotService : IShotService
{
    private readonly IShotRecordRepository _shotRepository;
    private readonly IPreferencesService _preferences;
    
    public ShotService(
        IShotRecordRepository shotRepository,
        IPreferencesService preferences)
    {
        _shotRepository = shotRepository;
        _preferences = preferences;
    }
    
    public async Task<ShotRecordDto?> GetMostRecentShotAsync()
    {
        var shot = await _shotRepository.GetMostRecentAsync();
        return shot == null ? null : MapToDto(shot);
    }
    
    public async Task<ShotRecordDto> CreateShotAsync(CreateShotDto dto)
    {
        ValidateCreateShot(dto);
        
        var shot = new ShotRecord
        {
            Timestamp = dto.Timestamp ?? DateTimeOffset.Now,
            BeanId = dto.BeanId,
            MachineId = dto.MachineId,
            GrinderId = dto.GrinderId,
            MadeById = dto.MadeById,
            MadeForId = dto.MadeForId,
            DoseIn = dto.DoseIn,
            GrindSetting = dto.GrindSetting,
            ExpectedTime = dto.ExpectedTime,
            ExpectedOutput = dto.ExpectedOutput,
            DrinkType = dto.DrinkType,
            ActualTime = dto.ActualTime,
            ActualOutput = dto.ActualOutput,
            Rating = dto.Rating,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        var created = await _shotRepository.AddAsync(shot);
        
        // Save accessories if provided
        if (dto.AccessoryIds.Any())
        {
            foreach (var accessoryId in dto.AccessoryIds)
            {
                created.ShotEquipments.Add(new ShotEquipment
                {
                    ShotRecordId = created.Id,
                    EquipmentId = accessoryId
                });
            }
            await _shotRepository.UpdateAsync(created);
        }
        
        // Remember selections
        _preferences.SetLastDrinkType(dto.DrinkType);
        if (dto.BeanId.HasValue)
            _preferences.SetLastBeanId(dto.BeanId.Value);
        if (dto.MachineId.HasValue)
            _preferences.SetLastMachineId(dto.MachineId.Value);
        if (dto.GrinderId.HasValue)
            _preferences.SetLastGrinderId(dto.GrinderId.Value);
        if (dto.AccessoryIds.Any())
            _preferences.SetLastAccessoryIds(dto.AccessoryIds);
        if (dto.MadeById.HasValue)
            _preferences.SetLastMadeById(dto.MadeById.Value);
        if (dto.MadeForId.HasValue)
            _preferences.SetLastMadeForId(dto.MadeForId.Value);
        
        // Reload with relationships
        return MapToDto(await _shotRepository.GetByIdAsync(created.Id) ?? created);
    }
    
    public async Task<ShotRecordDto> UpdateShotAsync(int id, UpdateShotDto dto)
    {
        var shot = await _shotRepository.GetByIdAsync(id);
        if (shot == null)
            throw new EntityNotFoundException(nameof(ShotRecord), id);
        
        if (dto.ActualTime.HasValue)
            shot.ActualTime = dto.ActualTime.Value;
        if (dto.ActualOutput.HasValue)
            shot.ActualOutput = dto.ActualOutput.Value;
        if (dto.Rating.HasValue)
            shot.Rating = dto.Rating.Value;
        
        if (dto.DoseIn.HasValue)
            shot.DoseIn = dto.DoseIn.Value;
        if (dto.GrindSetting != null)
            shot.GrindSetting = dto.GrindSetting;
        if (dto.ExpectedTime.HasValue)
            shot.ExpectedTime = dto.ExpectedTime.Value;
        if (dto.ExpectedOutput.HasValue)
            shot.ExpectedOutput = dto.ExpectedOutput.Value;
        if (dto.DrinkType != null)
            shot.DrinkType = dto.DrinkType;
        
        shot.LastModifiedAt = DateTimeOffset.Now;
        
        var updated = await _shotRepository.UpdateAsync(shot);
        return MapToDto(updated);
    }
    
    public async Task DeleteShotAsync(int id)
    {
        var shot = await _shotRepository.GetByIdAsync(id);
        if (shot == null)
            throw new EntityNotFoundException(nameof(ShotRecord), id);
        
        shot.IsDeleted = true;
        shot.LastModifiedAt = DateTimeOffset.Now;
        await _shotRepository.UpdateAsync(shot);
    }
    
    public async Task<PagedResult<ShotRecordDto>> GetShotHistoryAsync(int pageIndex, int pageSize)
    {
        var shots = await _shotRepository.GetHistoryAsync(pageIndex, pageSize);
        var totalCount = await _shotRepository.GetTotalCountAsync();
        
        return new PagedResult<ShotRecordDto>
        {
            Items = shots.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }
    
    public async Task<PagedResult<ShotRecordDto>> GetShotHistoryByUserAsync(int userProfileId, int pageIndex, int pageSize)
    {
        var shots = await _shotRepository.GetByUserAsync(userProfileId, pageIndex, pageSize);
        var totalCount = await _shotRepository.GetTotalCountAsync();
        
        return new PagedResult<ShotRecordDto>
        {
            Items = shots.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }
    
    public async Task<PagedResult<ShotRecordDto>> GetShotHistoryByBeanAsync(int beanId, int pageIndex, int pageSize)
    {
        var shots = await _shotRepository.GetByBeanAsync(beanId, pageIndex, pageSize);
        var totalCount = await _shotRepository.GetTotalCountAsync();
        
        return new PagedResult<ShotRecordDto>
        {
            Items = shots.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }
    
    public async Task<PagedResult<ShotRecordDto>> GetShotHistoryByEquipmentAsync(int equipmentId, int pageIndex, int pageSize)
    {
        var shots = await _shotRepository.GetByEquipmentAsync(equipmentId, pageIndex, pageSize);
        var totalCount = await _shotRepository.GetTotalCountAsync();
        
        return new PagedResult<ShotRecordDto>
        {
            Items = shots.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }
    
    public async Task<ShotRecordDto?> GetShotByIdAsync(int id)
    {
        var shot = await _shotRepository.GetByIdAsync(id);
        return shot == null ? null : MapToDto(shot);
    }
    
    private ShotRecordDto MapToDto(ShotRecord shot) => new()
    {
        Id = shot.Id,
        Timestamp = shot.Timestamp,
        Bean = shot.Bean == null ? null : new BeanDto
        {
            Id = shot.Bean.Id,
            Name = shot.Bean.Name,
            Roaster = shot.Bean.Roaster,
            RoastDate = shot.Bean.RoastDate,
            Origin = shot.Bean.Origin,
            Notes = shot.Bean.Notes,
            IsActive = shot.Bean.IsActive,
            CreatedAt = shot.Bean.CreatedAt
        },
        Machine = shot.Machine == null ? null : new EquipmentDto
        {
            Id = shot.Machine.Id,
            Name = shot.Machine.Name,
            Type = shot.Machine.Type,
            Notes = shot.Machine.Notes,
            IsActive = shot.Machine.IsActive,
            CreatedAt = shot.Machine.CreatedAt
        },
        Grinder = shot.Grinder == null ? null : new EquipmentDto
        {
            Id = shot.Grinder.Id,
            Name = shot.Grinder.Name,
            Type = shot.Grinder.Type,
            Notes = shot.Grinder.Notes,
            IsActive = shot.Grinder.IsActive,
            CreatedAt = shot.Grinder.CreatedAt
        },
        Accessories = shot.ShotEquipments.Select(se => new EquipmentDto
        {
            Id = se.Equipment.Id,
            Name = se.Equipment.Name,
            Type = se.Equipment.Type,
            Notes = se.Equipment.Notes,
            IsActive = se.Equipment.IsActive,
            CreatedAt = se.Equipment.CreatedAt
        }).ToList(),
        MadeBy = shot.MadeBy == null ? null : new UserProfileDto
        {
            Id = shot.MadeBy.Id,
            Name = shot.MadeBy.Name,
            AvatarPath = shot.MadeBy.AvatarPath,
            CreatedAt = shot.MadeBy.CreatedAt
        },
        MadeFor = shot.MadeFor == null ? null : new UserProfileDto
        {
            Id = shot.MadeFor.Id,
            Name = shot.MadeFor.Name,
            AvatarPath = shot.MadeFor.AvatarPath,
            CreatedAt = shot.MadeFor.CreatedAt
        },
        DoseIn = shot.DoseIn,
        GrindSetting = shot.GrindSetting,
        ExpectedTime = shot.ExpectedTime,
        ExpectedOutput = shot.ExpectedOutput,
        DrinkType = shot.DrinkType,
        ActualTime = shot.ActualTime,
        ActualOutput = shot.ActualOutput,
        Rating = shot.Rating
    };
    
    private void ValidateCreateShot(CreateShotDto dto)
    {
        var errors = new Dictionary<string, List<string>>();
        
        if (dto.DoseIn < 5 || dto.DoseIn > 30)
            errors.Add(nameof(dto.DoseIn), new List<string> { "Dose must be between 5 and 30 grams" });
        
        if (dto.ExpectedTime < 10 || dto.ExpectedTime > 60)
            errors.Add(nameof(dto.ExpectedTime), new List<string> { "Expected time must be between 10 and 60 seconds" });
        
        if (dto.ExpectedOutput < 10 || dto.ExpectedOutput > 100)
            errors.Add(nameof(dto.ExpectedOutput), new List<string> { "Expected output must be between 10 and 100 grams" });
        
        if (string.IsNullOrWhiteSpace(dto.GrindSetting))
            errors.Add(nameof(dto.GrindSetting), new List<string> { "Grind setting is required" });
        
        if (string.IsNullOrWhiteSpace(dto.DrinkType))
            errors.Add(nameof(dto.DrinkType), new List<string> { "Drink type is required" });
        
        if (dto.Rating.HasValue && (dto.Rating < 1 || dto.Rating > 5))
            errors.Add(nameof(dto.Rating), new List<string> { "Rating must be between 1 and 5" });
        
        if (errors.Any())
            throw new ValidationException(errors);
    }
}
