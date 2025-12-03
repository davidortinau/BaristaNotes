# Service Contracts: Shot Maker, Recipient, and Preinfusion Tracking

**Feature**: 001-shot-tracking  
**Date**: 2025-12-03  
**Phase**: 1 - Design & Contracts

## Overview

This document defines the service layer contracts (DTOs and interfaces) for the shot tracking feature. Since BaristaNotes is a client-side mobile application with local SQLite storage, these are internal service contracts rather than REST API endpoints.

---

## DTOs (Data Transfer Objects)

### CreateShotDto (Modified)

**Purpose**: Data contract for creating a new shot record.

```csharp
namespace BaristaNotes.Core.DTOs;

public class CreateShotDto
{
    // Existing fields
    public int? BeanId { get; set; }
    public int? GrinderId { get; set; }
    public int? MachineId { get; set; }
    
    [Required]
    [Range(0.1, 100, ErrorMessage = "Dose must be between 0.1 and 100 grams")]
    public decimal DoseIn { get; set; }
    
    [Required]
    [Range(0.1, 300, ErrorMessage = "Extraction time must be between 0.1 and 300 seconds")]
    public decimal ActualTime { get; set; }
    
    [Required]
    [Range(0.1, 500, ErrorMessage = "Output must be between 0.1 and 500 grams")]
    public decimal ActualOutput { get; set; }
    
    public decimal? ExpectedTime { get; set; }
    public decimal? ExpectedOutput { get; set; }
    
    [StringLength(50)]
    public string? GrindSetting { get; set; }
    
    [Required]
    [StringLength(50)]
    public string DrinkType { get; set; }
    
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
    public int? Rating { get; set; }
    
    [Required]
    public DateTimeOffset Timestamp { get; set; }
    
    // NEW fields
    /// <summary>
    /// User ID of the person who pulled/made the shot
    /// </summary>
    public int? MadeById { get; set; }
    
    /// <summary>
    /// User ID of the person the shot was made for
    /// </summary>
    public int? MadeForId { get; set; }
    
    /// <summary>
    /// Preinfusion duration in seconds (0-60 range)
    /// </summary>
    [Range(0, 60, ErrorMessage = "Preinfusion time must be between 0 and 60 seconds")]
    public decimal? PreinfusionTime { get; set; }
}
```

**Validation Notes**:
- MadeById and MadeForId are optional (nullable)
- MadeById and MadeForId can reference the same user ID (valid scenario)
- PreinfusionTime validated to reasonable espresso range (0-60 seconds)
- All validation attributes enforced at service layer

---

### UpdateShotDto (Modified)

**Purpose**: Data contract for updating an existing shot record.

```csharp
namespace BaristaNotes.Core.DTOs;

public class UpdateShotDto
{
    [Required]
    public int Id { get; set; }
    
    // Existing fields (all updatable)
    public int? BeanId { get; set; }
    public int? GrinderId { get; set; }
    public int? MachineId { get; set; }
    
    [Required]
    [Range(0.1, 100, ErrorMessage = "Dose must be between 0.1 and 100 grams")]
    public decimal DoseIn { get; set; }
    
    [Required]
    [Range(0.1, 300, ErrorMessage = "Extraction time must be between 0.1 and 300 seconds")]
    public decimal ActualTime { get; set; }
    
    [Required]
    [Range(0.1, 500, ErrorMessage = "Output must be between 0.1 and 500 grams")]
    public decimal ActualOutput { get; set; }
    
    public decimal? ExpectedTime { get; set; }
    public decimal? ExpectedOutput { get; set; }
    
    [StringLength(50)]
    public string? GrindSetting { get; set; }
    
    [Required]
    [StringLength(50)]
    public string DrinkType { get; set; }
    
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
    public int? Rating { get; set; }
    
    [Required]
    public DateTimeOffset Timestamp { get; set; }
    
    // NEW fields (updatable)
    /// <summary>
    /// User ID of the person who pulled/made the shot
    /// </summary>
    public int? MadeById { get; set; }
    
    /// <summary>
    /// User ID of the person the shot was made for
    /// </summary>
    public int? MadeForId { get; set; }
    
    /// <summary>
    /// Preinfusion duration in seconds (0-60 range)
    /// </summary>
    [Range(0, 60, ErrorMessage = "Preinfusion time must be between 0 and 60 seconds")]
    public decimal? PreinfusionTime { get; set; }
}
```

**Design Notes**:
- Timestamp is updatable (user may correct time after logging)
- All new fields (MadeById, MadeForId, PreinfusionTime) are fully updatable
- Validation identical to CreateShotDto for consistency

---

### ShotRecordDto (Modified - Read Model)

**Purpose**: Data contract for displaying shot record with related entities.

```csharp
namespace BaristaNotes.Core.DTOs;

public class ShotRecordDto
{
    public int Id { get; set; }
    
    // Core shot data
    public decimal DoseIn { get; set; }
    public decimal ActualTime { get; set; }
    public decimal ActualOutput { get; set; }
    public decimal? ExpectedTime { get; set; }
    public decimal? ExpectedOutput { get; set; }
    public string? GrindSetting { get; set; }
    public string DrinkType { get; set; }
    public int? Rating { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    
    // Related entities (existing)
    public SimpleBeanDto? Bean { get; set; }
    public SimpleEquipmentDto? Grinder { get; set; }
    public SimpleEquipmentDto? Machine { get; set; }
    
    // NEW: Related user entities
    /// <summary>
    /// User who pulled/made the shot
    /// </summary>
    public SimpleUserDto? MadeBy { get; set; }
    
    /// <summary>
    /// User the shot was made for
    /// </summary>
    public SimpleUserDto? MadeFor { get; set; }
    
    /// <summary>
    /// Preinfusion duration in seconds
    /// </summary>
    public decimal? PreinfusionTime { get; set; }
    
    // Metadata
    public DateTimeOffset LastModifiedAt { get; set; }
}
```

---

### SimpleUserDto (New)

**Purpose**: Lightweight user representation for display in shot records.

```csharp
namespace BaristaNotes.Core.DTOs;

public class SimpleUserDto
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; }
    
    public string? AvatarPath { get; set; }
}
```

**Usage**: 
- Displayed in ShotRecordCard for maker/recipient
- Used in picker controls for user selection
- Minimal data to reduce memory footprint in lists

---

## Service Interfaces

### IShotService (Modified)

**Purpose**: Service layer interface for shot record operations.

```csharp
namespace BaristaNotes.Core.Interfaces;

public interface IShotService
{
    /// <summary>
    /// Creates a new shot record
    /// </summary>
    /// <param name="dto">Shot creation data including maker, recipient, and preinfusion time</param>
    /// <returns>Created shot record ID</returns>
    /// <exception cref="ValidationException">When validation fails</exception>
    /// <exception cref="EntityNotFoundException">When MadeById or MadeForId references non-existent user</exception>
    Task<int> CreateShotAsync(CreateShotDto dto);
    
    /// <summary>
    /// Updates an existing shot record
    /// </summary>
    /// <param name="dto">Shot update data including maker, recipient, and preinfusion time</param>
    /// <exception cref="ValidationException">When validation fails</exception>
    /// <exception cref="EntityNotFoundException">When shot ID or user IDs don't exist</exception>
    Task UpdateShotAsync(UpdateShotDto dto);
    
    /// <summary>
    /// Retrieves a single shot record with all related entities
    /// </summary>
    /// <param name="id">Shot record ID</param>
    /// <returns>Shot record with maker, recipient, bean, equipment</returns>
    /// <exception cref="EntityNotFoundException">When shot ID doesn't exist</exception>
    Task<ShotRecordDto> GetShotByIdAsync(int id);
    
    /// <summary>
    /// Retrieves all non-deleted shots with related entities
    /// </summary>
    /// <returns>List of shots ordered by timestamp descending</returns>
    Task<List<ShotRecordDto>> GetAllShotsAsync();
    
    /// <summary>
    /// Soft-deletes a shot record
    /// </summary>
    /// <param name="id">Shot record ID</param>
    /// <exception cref="EntityNotFoundException">When shot ID doesn't exist</exception>
    Task DeleteShotAsync(int id);
    
    // NEW: Filter methods for analytics (P3 priority - future)
    /// <summary>
    /// Retrieves shots made by a specific user
    /// </summary>
    /// <param name="userId">User ID of the maker</param>
    /// <returns>List of shots made by the user</returns>
    Task<List<ShotRecordDto>> GetShotsByMakerAsync(int userId);
    
    /// <summary>
    /// Retrieves shots made for a specific user
    /// </summary>
    /// <param name="userId">User ID of the recipient</param>
    /// <returns>List of shots made for the user</returns>
    Task<List<ShotRecordDto>> GetShotsByRecipientAsync(int userId);
}
```

**Implementation Notes**:
- All async methods for consistency
- Validation exceptions provide user-friendly messages
- EntityNotFoundException thrown for invalid foreign keys
- Soft delete (IsDeleted flag) preserves historical data

---

### IUserService (Existing - No Changes to Interface)

**Purpose**: Service layer interface for user profile operations.

```csharp
namespace BaristaNotes.Core.Interfaces;

public interface IUserService
{
    /// <summary>
    /// Retrieves all active (non-deleted) users
    /// </summary>
    /// <returns>List of users sorted alphabetically by name</returns>
    Task<List<SimpleUserDto>> GetAllUsersAsync();
    
    /// <summary>
    /// Retrieves a single user by ID
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>User profile</returns>
    /// <exception cref="EntityNotFoundException">When user doesn't exist</exception>
    Task<SimpleUserDto> GetUserByIdAsync(int id);
    
    // Additional methods as needed...
}
```

**Note**: Existing interface already supports the needs of this feature. No modifications required.

---

### IPreferencesService (New)

**Purpose**: Service layer interface for managing last-used values via Preferences API.

```csharp
namespace BaristaNotes.Core.Interfaces;

public interface IPreferencesService
{
    /// <summary>
    /// Saves last-used shot logging form values
    /// </summary>
    /// <param name="dto">Shot data to remember for next time</param>
    void SaveLastUsedShotValues(CreateShotDto dto);
    
    /// <summary>
    /// Loads last-used shot logging form values
    /// </summary>
    /// <returns>DTO pre-populated with last-used values, or defaults if none saved</returns>
    CreateShotDto LoadLastUsedShotValues();
    
    /// <summary>
    /// Clears all saved preferences (logout/reset scenario)
    /// </summary>
    void ClearAllPreferences();
    
    /// <summary>
    /// Checks if a specific preference key exists
    /// </summary>
    /// <param name="key">Preference key name</param>
    /// <returns>True if key exists</returns>
    bool HasPreference(string key);
}
```

**Implementation Details**:
```csharp
public class PreferencesService : IPreferencesService
{
    // Constants for preference keys
    private const string KEY_LAST_BEAN_ID = "LastUsedBeanId";
    private const string KEY_LAST_MADE_BY_ID = "LastUsedMadeById";
    private const string KEY_LAST_MADE_FOR_ID = "LastUsedMadeForId";
    private const string KEY_LAST_GRINDER_ID = "LastUsedGrinderId";
    private const string KEY_LAST_MACHINE_ID = "LastUsedMachineId";
    private const string KEY_LAST_DOSE_IN = "LastUsedDoseIn";
    private const string KEY_LAST_EXPECTED_TIME = "LastUsedExpectedTime";
    private const string KEY_LAST_EXPECTED_OUTPUT = "LastUsedExpectedOutput";
    private const string KEY_LAST_GRIND_SETTING = "LastUsedGrindSetting";
    private const string KEY_LAST_DRINK_TYPE = "LastUsedDrinkType";
    
    public void SaveLastUsedShotValues(CreateShotDto dto)
    {
        if (dto.BeanId.HasValue)
            Preferences.Set(KEY_LAST_BEAN_ID, dto.BeanId.Value);
        
        if (dto.MadeById.HasValue)
            Preferences.Set(KEY_LAST_MADE_BY_ID, dto.MadeById.Value);
        
        if (dto.MadeForId.HasValue)
            Preferences.Set(KEY_LAST_MADE_FOR_ID, dto.MadeForId.Value);
        
        // ... save other values ...
    }
    
    public CreateShotDto LoadLastUsedShotValues()
    {
        return new CreateShotDto
        {
            BeanId = Preferences.ContainsKey(KEY_LAST_BEAN_ID) 
                ? Preferences.Get(KEY_LAST_BEAN_ID, 0) 
                : null,
            MadeById = Preferences.ContainsKey(KEY_LAST_MADE_BY_ID)
                ? Preferences.Get(KEY_LAST_MADE_BY_ID, 0)
                : null,
            MadeForId = Preferences.ContainsKey(KEY_LAST_MADE_FOR_ID)
                ? Preferences.Get(KEY_LAST_MADE_FOR_ID, 0)
                : null,
            // ... load other values ...
            Timestamp = DateTimeOffset.Now
        };
    }
    
    public void ClearAllPreferences()
    {
        Preferences.Clear();
    }
    
    public bool HasPreference(string key)
    {
        return Preferences.ContainsKey(key);
    }
}
```

---

## Error Handling

### Exception Types

```csharp
public class ValidationException : Exception
{
    public Dictionary<string, string[]> Errors { get; }
    
    public ValidationException(string message, Dictionary<string, string[]> errors) 
        : base(message)
    {
        Errors = errors;
    }
}

public class EntityNotFoundException : Exception
{
    public string EntityName { get; }
    public object EntityId { get; }
    
    public EntityNotFoundException(string entityName, object entityId)
        : base($"{entityName} with ID {entityId} was not found")
    {
        EntityName = entityName;
        EntityId = entityId;
    }
}
```

### Error Scenarios

| Scenario | Exception | HTTP Status (if REST) | User Message |
|----------|-----------|----------------------|--------------|
| Invalid preinfusion time (<0 or >60) | ValidationException | 400 | "Preinfusion time must be between 0 and 60 seconds" |
| MadeById references non-existent user | EntityNotFoundException | 404 | "Selected maker not found. Please refresh the user list." |
| MadeForId references non-existent user | EntityNotFoundException | 404 | "Selected recipient not found. Please refresh the user list." |
| Shot ID doesn't exist (update/delete) | EntityNotFoundException | 404 | "Shot record not found" |

---

## Mapping Strategy

### Entity to DTO Mapping

```csharp
// In ShotService
private ShotRecordDto MapToDto(ShotRecord entity)
{
    return new ShotRecordDto
    {
        Id = entity.Id,
        DoseIn = entity.DoseIn,
        ActualTime = entity.ActualTime,
        ActualOutput = entity.ActualOutput,
        ExpectedTime = entity.ExpectedTime,
        ExpectedOutput = entity.ExpectedOutput,
        GrindSetting = entity.GrindSetting,
        DrinkType = entity.DrinkType,
        Rating = entity.Rating,
        Timestamp = entity.Timestamp,
        PreinfusionTime = entity.PreinfusionTime,
        LastModifiedAt = entity.LastModifiedAt,
        
        // Map related entities
        Bean = entity.Bean != null ? new SimpleBeanDto 
        { 
            Id = entity.Bean.Id, 
            Name = entity.Bean.Name 
        } : null,
        
        Grinder = entity.Grinder != null ? new SimpleEquipmentDto
        {
            Id = entity.Grinder.Id,
            Name = entity.Grinder.Name
        } : null,
        
        Machine = entity.Machine != null ? new SimpleEquipmentDto
        {
            Id = entity.Machine.Id,
            Name = entity.Machine.Name
        } : null,
        
        // NEW: Map maker and recipient
        MadeBy = entity.MadeBy != null ? new SimpleUserDto
        {
            Id = entity.MadeBy.Id,
            Name = entity.MadeBy.Name,
            AvatarPath = entity.MadeBy.AvatarPath
        } : null,
        
        MadeFor = entity.MadeFor != null ? new SimpleUserDto
        {
            Id = entity.MadeFor.Id,
            Name = entity.MadeFor.Name,
            AvatarPath = entity.MadeFor.AvatarPath
        } : null
    };
}
```

---

## Performance Considerations

### Query Optimization

- Use `.Include()` for related entities to avoid N+1 queries
- Index foreign keys (MadeById, MadeForId) for filter performance
- Project to DTOs in service layer (not in UI) for consistent memory usage

### Preferences API Performance

- Synchronous read/write operations (<1ms latency)
- No serialization overhead (stores primitives directly)
- Platform-optimized storage (UserDefaults, SharedPreferences)

---

**Phase 1 Contracts Complete**: Service contracts defined, DTOs specified, error handling documented. Ready for Phase 2 (task breakdown).
