using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;

namespace BaristaNotes.Core.Services;

public class ShotService : IShotService
{
    private readonly IShotRecordRepository _shotRepository;
    private readonly IPreferencesService _preferences;
    private readonly IBagRepository _bagRepository;

    public ShotService(
        IShotRecordRepository shotRepository,
        IPreferencesService preferences,
        IBagRepository bagRepository)
    {
        _shotRepository = shotRepository;
        _preferences = preferences;
        _bagRepository = bagRepository;
    }

    public async Task<ShotRecordDto?> GetMostRecentShotAsync()
    {
        var shot = await _shotRepository.GetMostRecentAsync();
        return shot == null ? null : MapToDto(shot);
    }

    public async Task<ShotRecordDto> CreateShotAsync(CreateShotDto dto)
    {
        ValidateCreateShot(dto);

        // T039: Validate bag exists and is active (IsComplete=false)
        if (!dto.BagId.HasValue)
        {
            throw new ValidationException(new Dictionary<string, List<string>>
            {
                { nameof(dto.BagId), new List<string> { "Bag is required" } }
            });
        }

        var bag = await _bagRepository.GetByIdAsync(dto.BagId.Value);
        if (bag == null)
        {
            throw new ValidationException(new Dictionary<string, List<string>>
            {
                { nameof(dto.BagId), new List<string> { "Bag not found" } }
            });
        }

        if (bag.IsComplete)
        {
            throw new ValidationException(new Dictionary<string, List<string>>
            {
                { nameof(dto.BagId), new List<string> { "Cannot log shot to a completed bag. Please reactivate the bag or select an active bag." } }
            });
        }

        var shot = new ShotRecord
        {
            Timestamp = dto.Timestamp ?? DateTime.Now,
            BagId = dto.BagId.Value,
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
            TastingNotes = dto.TastingNotes,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
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
        if (dto.BagId.HasValue)
            _preferences.SetLastBagId(dto.BagId.Value);
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
        ValidateUpdateShot(dto);

        var shot = await _shotRepository.GetByIdAsync(id);
        if (shot == null || shot.IsDeleted)
            throw new EntityNotFoundException(nameof(ShotRecord), id);

        // Update bag if provided
        if (dto.BagId.HasValue)
            shot.BagId = dto.BagId.Value;

        // Update maker/recipient if provided
        if (dto.MadeById.HasValue)
            shot.MadeById = dto.MadeById.Value;

        if (dto.MadeForId.HasValue)
            shot.MadeForId = dto.MadeForId.Value;

        // Update only editable fields
        if (dto.ActualTime.HasValue)
            shot.ActualTime = dto.ActualTime.Value;

        if (dto.ActualOutput.HasValue)
            shot.ActualOutput = dto.ActualOutput.Value;

        shot.Rating = dto.Rating; // Can be null
        shot.DrinkType = dto.DrinkType;
        shot.TastingNotes = dto.TastingNotes; // Can be null
        shot.LastModifiedAt = DateTime.Now;

        var updated = await _shotRepository.UpdateAsync(shot);
        return MapToDto(updated);
    }

    public async Task DeleteShotAsync(int id)
    {
        var shot = await _shotRepository.GetByIdAsync(id);
        if (shot == null || shot.IsDeleted)
            throw new EntityNotFoundException(nameof(ShotRecord), id);

        shot.IsDeleted = true;
        shot.LastModifiedAt = DateTime.Now;
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

    public async Task<ShotRecordDto?> GetBestRatedShotByBeanAsync(int beanId)
    {
        var shots = await _shotRepository.GetByBeanAsync(beanId, 0, 100);
        var bestShot = shots
            .Where(s => s.Rating.HasValue)
            .OrderByDescending(s => s.Rating)
            .ThenByDescending(s => s.Timestamp)
            .FirstOrDefault();
        return bestShot == null ? null : MapToDto(bestShot);
    }

    public async Task<ShotRecordDto?> GetBestRatedShotByBagAsync(int bagId)
    {
        var shots = await _shotRepository.GetAllAsync();
        var bestShot = shots
            .Where(s => s.BagId == bagId && s.Rating.HasValue && !s.IsDeleted)
            .OrderByDescending(s => s.Rating)
            .ThenByDescending(s => s.Timestamp)
            .FirstOrDefault();
        return bestShot == null ? null : MapToDto(bestShot);
    }

    public async Task<AIAdviceRequestDto?> GetShotContextForAIAsync(int shotId)
    {
        var shot = await _shotRepository.GetByIdAsync(shotId);
        if (shot == null || shot.IsDeleted)
            return null;

        // Build current shot context
        var currentShot = new ShotContextDto
        {
            DoseIn = shot.DoseIn,
            ActualOutput = shot.ActualOutput,
            ActualTime = shot.ActualTime,
            GrindSetting = shot.GrindSetting,
            Rating = shot.Rating,
            TastingNotes = null, // TastingNotes field will be added in US4
            Timestamp = shot.Timestamp
        };

        // Build bean context from bag
        var bean = shot.Bag?.Bean;
        var roastDate = shot.Bag?.RoastDate ?? DateTime.Now;
        var beanContext = new BeanContextDto
        {
            Name = bean?.Name ?? "Unknown Bean",
            Roaster = bean?.Roaster,
            Origin = bean?.Origin,
            RoastDate = roastDate,
            DaysFromRoast = (int)(DateTime.Now - roastDate).TotalDays,
            Notes = bean?.Notes
        };

        // Build equipment context
        EquipmentContextDto? equipmentContext = null;
        if (shot.Machine != null || shot.Grinder != null)
        {
            equipmentContext = new EquipmentContextDto
            {
                MachineName = shot.Machine?.Name,
                GrinderName = shot.Grinder?.Name
            };
        }

        // Get historical shots for same bag (up to 10, sorted by rating desc)
        var allShots = await _shotRepository.GetAllAsync();
        var historicalShots = allShots
            .Where(s => s.BagId == shot.BagId && s.Id != shotId && !s.IsDeleted)
            .OrderByDescending(s => s.Rating ?? -1)
            .ThenByDescending(s => s.Timestamp)
            .Take(10)
            .Select(s => new ShotContextDto
            {
                DoseIn = s.DoseIn,
                ActualOutput = s.ActualOutput,
                ActualTime = s.ActualTime,
                GrindSetting = s.GrindSetting,
                Rating = s.Rating,
                TastingNotes = null,
                Timestamp = s.Timestamp
            })
            .ToList();

        return new AIAdviceRequestDto
        {
            ShotId = shotId,
            CurrentShot = currentShot,
            HistoricalShots = historicalShots,
            BeanInfo = beanContext,
            Equipment = equipmentContext
        };
    }

    private ShotRecordDto MapToDto(ShotRecord shot) => new()
    {
        Id = shot.Id,
        Timestamp = shot.Timestamp,
        Bean = shot.Bag?.Bean == null ? null : new BeanDto // Kept for backward compatibility
        {
            Id = shot.Bag.Bean.Id,
            Name = shot.Bag.Bean.Name,
            Roaster = shot.Bag.Bean.Roaster,
            RoastDate = shot.Bag.RoastDate, // Get from Bag
            Origin = shot.Bag.Bean.Origin,
            Notes = shot.Bag.Bean.Notes,
            IsActive = shot.Bag.Bean.IsActive,
            CreatedAt = shot.Bag.Bean.CreatedAt
        },
        Bag = shot.Bag == null ? null : new BagSummaryDto // NEW: Include bag reference
        {
            Id = shot.Bag.Id,
            BeanId = shot.Bag.BeanId,
            BeanName = shot.Bag.Bean?.Name ?? "",
            RoastDate = shot.Bag.RoastDate,
            Notes = shot.Bag.Notes,
            IsComplete = shot.Bag.IsComplete,
            ShotCount = 0, // Not needed in this context
            AverageRating = null // Not needed in this context
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
        Rating = shot.Rating,
        TastingNotes = shot.TastingNotes
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

    private void ValidateUpdateShot(UpdateShotDto dto)
    {
        var errors = new Dictionary<string, List<string>>();

        if (dto.ActualTime.HasValue && (dto.ActualTime <= 0 || dto.ActualTime > 999))
            errors.Add(nameof(dto.ActualTime), new List<string> { "Shot time must be between 0 and 999 seconds" });

        if (dto.ActualOutput.HasValue && (dto.ActualOutput <= 0 || dto.ActualOutput > 200))
            errors.Add(nameof(dto.ActualOutput), new List<string> { "Output weight must be between 0 and 200 grams" });

        if (dto.Rating.HasValue && (dto.Rating < 1 || dto.Rating > 5))
            errors.Add(nameof(dto.Rating), new List<string> { "Rating must be between 1 and 5 stars" });

        if (string.IsNullOrWhiteSpace(dto.DrinkType))
            errors.Add(nameof(dto.DrinkType), new List<string> { "Drink type is required" });

        if (errors.Any())
            throw new ValidationException(errors);
    }
}
