# Service Contracts: Espresso Shot Tracking & Management

**Feature**: 001-espresso-tracking  
**Phase**: 1 - Design & Contracts  
**Date**: 2025-12-02

## Purpose

This document defines the service layer contracts (interfaces) for BaristaNotes. Services provide business logic abstraction between ViewModels and data repositories. All interfaces use DTOs (Data Transfer Objects) to decouple UI from persistence layer.

---

## IShotService

Manages shot logging, retrieval, and history.

```csharp
public interface IShotService
{
    /// <summary>
    /// Gets the most recent shot record for pre-populating the shot form.
    /// Returns null if no shots exist.
    /// </summary>
    Task<ShotRecordDto?> GetMostRecentShotAsync();
    
    /// <summary>
    /// Creates a new shot record with recipe and optional actuals/rating.
    /// </summary>
    /// <param name="dto">Shot data to save</param>
    /// <returns>Created shot with generated ID</returns>
    Task<ShotRecordDto> CreateShotAsync(CreateShotDto dto);
    
    /// <summary>
    /// Updates an existing shot record (for editing actuals/rating after creation).
    /// </summary>
    Task<ShotRecordDto> UpdateShotAsync(int id, UpdateShotDto dto);
    
    /// <summary>
    /// Soft-deletes a shot record (sets IsDeleted flag).
    /// </summary>
    Task DeleteShotAsync(int id);
    
    /// <summary>
    /// Gets paginated shot history ordered by timestamp descending.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index</param>
    /// <param name="pageSize">Number of records per page</param>
    /// <returns>Page of shot records</returns>
    Task<PagedResult<ShotRecordDto>> GetShotHistoryAsync(int pageIndex, int pageSize);
    
    /// <summary>
    /// Gets shot history filtered by user profile (as barista or consumer).
    /// </summary>
    Task<PagedResult<ShotRecordDto>> GetShotHistoryByUserAsync(
        int userProfileId, 
        int pageIndex, 
        int pageSize);
    
    /// <summary>
    /// Gets shot history filtered by bean.
    /// </summary>
    Task<PagedResult<ShotRecordDto>> GetShotHistoryByBeanAsync(
        int beanId, 
        int pageIndex, 
        int pageSize);
    
    /// <summary>
    /// Gets shot history filtered by equipment (machine, grinder, or accessory).
    /// </summary>
    Task<PagedResult<ShotRecordDto>> GetShotHistoryByEquipmentAsync(
        int equipmentId, 
        int pageIndex, 
        int pageSize);
    
    /// <summary>
    /// Gets a single shot record by ID.
    /// </summary>
    Task<ShotRecordDto?> GetShotByIdAsync(int id);
}
```

---

## IEquipmentService

Manages equipment (machines, grinders, accessories).

```csharp
public interface IEquipmentService
{
    /// <summary>
    /// Gets all active equipment items.
    /// </summary>
    Task<List<EquipmentDto>> GetAllActiveEquipmentAsync();
    
    /// <summary>
    /// Gets active equipment filtered by type.
    /// </summary>
    Task<List<EquipmentDto>> GetEquipmentByTypeAsync(EquipmentType type);
    
    /// <summary>
    /// Gets a single equipment item by ID.
    /// </summary>
    Task<EquipmentDto?> GetEquipmentByIdAsync(int id);
    
    /// <summary>
    /// Creates a new equipment item.
    /// </summary>
    Task<EquipmentDto> CreateEquipmentAsync(CreateEquipmentDto dto);
    
    /// <summary>
    /// Updates an existing equipment item.
    /// </summary>
    Task<EquipmentDto> UpdateEquipmentAsync(int id, UpdateEquipmentDto dto);
    
    /// <summary>
    /// Archives an equipment item (sets IsActive = false).
    /// Preserves historical shot data.
    /// </summary>
    Task ArchiveEquipmentAsync(int id);
    
    /// <summary>
    /// Permanently deletes an equipment item (soft delete: IsDeleted = true).
    /// Should only be used if never referenced in shots.
    /// </summary>
    Task DeleteEquipmentAsync(int id);
}
```

---

## IBeanService

Manages coffee bean records.

```csharp
public interface IBeanService
{
    /// <summary>
    /// Gets all active bean records.
    /// </summary>
    Task<List<BeanDto>> GetAllActiveBeansAsync();
    
    /// <summary>
    /// Gets a single bean record by ID.
    /// </summary>
    Task<BeanDto?> GetBeanByIdAsync(int id);
    
    /// <summary>
    /// Creates a new bean record.
    /// </summary>
    Task<BeanDto> CreateBeanAsync(CreateBeanDto dto);
    
    /// <summary>
    /// Updates an existing bean record.
    /// </summary>
    Task<BeanDto> UpdateBeanAsync(int id, UpdateBeanDto dto);
    
    /// <summary>
    /// Archives a bean record (sets IsActive = false).
    /// Preserves historical shot data.
    /// </summary>
    Task ArchiveBeanAsync(int id);
    
    /// <summary>
    /// Permanently deletes a bean record (soft delete: IsDeleted = true).
    /// Should only be used if never referenced in shots.
    /// </summary>
    Task DeleteBeanAsync(int id);
}
```

---

## IUserProfileService

Manages user profiles (baristas and consumers).

```csharp
public interface IUserProfileService
{
    /// <summary>
    /// Gets all active user profiles.
    /// </summary>
    Task<List<UserProfileDto>> GetAllProfilesAsync();
    
    /// <summary>
    /// Gets a single user profile by ID.
    /// </summary>
    Task<UserProfileDto?> GetProfileByIdAsync(int id);
    
    /// <summary>
    /// Creates a new user profile.
    /// </summary>
    Task<UserProfileDto> CreateProfileAsync(CreateUserProfileDto dto);
    
    /// <summary>
    /// Updates an existing user profile.
    /// </summary>
    Task<UserProfileDto> UpdateProfileAsync(int id, UpdateUserProfileDto dto);
    
    /// <summary>
    /// Permanently deletes a user profile (soft delete: IsDeleted = true).
    /// Preserves historical shot data.
    /// </summary>
    Task DeleteProfileAsync(int id);
}
```

---

## IPreferencesService

Manages app preferences and "remember last selection" functionality.

```csharp
public interface IPreferencesService
{
    /// <summary>
    /// Gets the last selected drink type.
    /// </summary>
    string? GetLastDrinkType();
    
    /// <summary>
    /// Sets the last selected drink type.
    /// </summary>
    void SetLastDrinkType(string drinkType);
    
    /// <summary>
    /// Gets the last selected bean ID.
    /// </summary>
    int? GetLastBeanId();
    
    /// <summary>
    /// Sets the last selected bean ID.
    /// </summary>
    void SetLastBeanId(int? beanId);
    
    /// <summary>
    /// Gets the last selected machine ID.
    /// </summary>
    int? GetLastMachineId();
    
    /// <summary>
    /// Sets the last selected machine ID.
    /// </summary>
    void SetLastMachineId(int? machineId);
    
    /// <summary>
    /// Gets the last selected grinder ID.
    /// </summary>
    int? GetLastGrinderId();
    
    /// <summary>
    /// Sets the last selected grinder ID.
    /// </summary>
    void SetLastGrinderId(int? grinderId);
    
    /// <summary>
    /// Gets the last selected accessory IDs (comma-separated).
    /// </summary>
    List<int> GetLastAccessoryIds();
    
    /// <summary>
    /// Sets the last selected accessory IDs.
    /// </summary>
    void SetLastAccessoryIds(List<int> accessoryIds);
    
    /// <summary>
    /// Gets the last selected "made by" user profile ID.
    /// </summary>
    int? GetLastMadeById();
    
    /// <summary>
    /// Sets the last selected "made by" user profile ID.
    /// </summary>
    void SetLastMadeById(int? madeById);
    
    /// <summary>
    /// Gets the last selected "made for" user profile ID.
    /// </summary>
    int? GetLastMadeForId();
    
    /// <summary>
    /// Sets the last selected "made for" user profile ID.
    /// </summary>
    void SetLastMadeForId(int? madeForId);
    
    /// <summary>
    /// Clears all stored preferences (for testing or reset functionality).
    /// </summary>
    void ClearAll();
}
```

**Implementation Note**: Uses .NET MAUI `Preferences` API for local key-value storage.

---

## Data Transfer Objects (DTOs)

### ShotRecordDto

```csharp
public record ShotRecordDto
{
    public int Id { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    
    // Related entities (can be null if archived/deleted)
    public BeanDto? Bean { get; init; }
    public EquipmentDto? Machine { get; init; }
    public EquipmentDto? Grinder { get; init; }
    public List<EquipmentDto> Accessories { get; init; } = new();
    public UserProfileDto? MadeBy { get; init; }
    public UserProfileDto? MadeFor { get; init; }
    
    // Recipe
    public decimal DoseIn { get; init; }
    public string GrindSetting { get; init; } = string.Empty;
    public decimal ExpectedTime { get; init; }
    public decimal ExpectedOutput { get; init; }
    public string DrinkType { get; init; } = string.Empty;
    
    // Actuals
    public decimal? ActualTime { get; init; }
    public decimal? ActualOutput { get; init; }
    
    // Rating
    public int? Rating { get; init; }
}
```

### CreateShotDto

```csharp
public record CreateShotDto
{
    public DateTimeOffset? Timestamp { get; init; } // Defaults to now if null
    
    public int? BeanId { get; init; }
    public int? MachineId { get; init; }
    public int? GrinderId { get; init; }
    public List<int> AccessoryIds { get; init; } = new();
    public int? MadeById { get; init; }
    public int? MadeForId { get; init; }
    
    public decimal DoseIn { get; init; }
    public string GrindSetting { get; init; } = string.Empty;
    public decimal ExpectedTime { get; init; }
    public decimal ExpectedOutput { get; init; }
    public string DrinkType { get; init; } = string.Empty;
    
    public decimal? ActualTime { get; init; }
    public decimal? ActualOutput { get; init; }
    
    public int? Rating { get; init; }
}
```

### UpdateShotDto

```csharp
public record UpdateShotDto
{
    public decimal? ActualTime { get; init; }
    public decimal? ActualOutput { get; init; }
    public int? Rating { get; init; }
    
    // Optional: Allow editing recipe after creation
    public decimal? DoseIn { get; init; }
    public string? GrindSetting { get; init; }
    public decimal? ExpectedTime { get; init; }
    public decimal? ExpectedOutput { get; init; }
    public string? DrinkType { get; init; }
}
```

### EquipmentDto

```csharp
public record EquipmentDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public EquipmentType Type { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
```

### CreateEquipmentDto

```csharp
public record CreateEquipmentDto
{
    public string Name { get; init; } = string.Empty;
    public EquipmentType Type { get; init; }
    public string? Notes { get; init; }
}
```

### UpdateEquipmentDto

```csharp
public record UpdateEquipmentDto
{
    public string? Name { get; init; }
    public EquipmentType? Type { get; init; }
    public string? Notes { get; init; }
    public bool? IsActive { get; init; }
}
```

### BeanDto

```csharp
public record BeanDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Roaster { get; init; }
    public DateTimeOffset? RoastDate { get; init; }
    public string? Origin { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
```

### CreateBeanDto

```csharp
public record CreateBeanDto
{
    public string Name { get; init; } = string.Empty;
    public string? Roaster { get; init; }
    public DateTimeOffset? RoastDate { get; init; }
    public string? Origin { get; init; }
    public string? Notes { get; init; }
}
```

### UpdateBeanDto

```csharp
public record UpdateBeanDto
{
    public string? Name { get; init; }
    public string? Roaster { get; init; }
    public DateTimeOffset? RoastDate { get; init; }
    public string? Origin { get; init; }
    public string? Notes { get; init; }
    public bool? IsActive { get; init; }
}
```

### UserProfileDto

```csharp
public record UserProfileDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? AvatarPath { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
```

### CreateUserProfileDto

```csharp
public record CreateUserProfileDto
{
    public string Name { get; init; } = string.Empty;
    public string? AvatarPath { get; init; }
}
```

### UpdateUserProfileDto

```csharp
public record UpdateUserProfileDto
{
    public string? Name { get; init; }
    public string? AvatarPath { get; init; }
}
```

### PagedResult<T>

```csharp
public record PagedResult<T>
{
    public List<T> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int PageIndex { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageIndex > 0;
    public bool HasNextPage => PageIndex < TotalPages - 1;
}
```

---

## Validation Rules

All DTOs should be validated in service implementations:

### CreateShotDto Validation
- `DoseIn`: Range 5-30 grams
- `GrindSetting`: Required, max 50 characters
- `ExpectedTime`: Range 10-60 seconds
- `ExpectedOutput`: Range 10-80 grams
- `DrinkType`: Required, max 50 characters
- `ActualTime`: Optional, range 5-120 seconds if provided
- `ActualOutput`: Optional, range 5-150 grams if provided
- `Rating`: Optional, range 1-5 if provided

### CreateEquipmentDto Validation
- `Name`: Required, 1-100 characters
- `Type`: Required enum value
- `Notes`: Optional, max 500 characters

### CreateBeanDto Validation
- `Name`: Required, 1-100 characters
- `Roaster`: Optional, max 100 characters
- `RoastDate`: Optional, must be in past if provided
- `Origin`: Optional, max 100 characters
- `Notes`: Optional, max 500 characters

### CreateUserProfileDto Validation
- `Name`: Required, 1-50 characters
- `AvatarPath`: Optional, max 500 characters

---

## Error Handling

Services should throw specific exceptions:

```csharp
public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string entityType, int id) 
        : base($"{entityType} with ID {id} not found") { }
}

public class ValidationException : Exception
{
    public Dictionary<string, List<string>> Errors { get; }
    
    public ValidationException(Dictionary<string, List<string>> errors) 
        : base("Validation failed")
    {
        Errors = errors;
    }
}
```

ViewModels catch these exceptions and display user-friendly messages.

---

## Summary

Service contracts defined for Shot, Equipment, Bean, UserProfile, and Preferences management. All interfaces use DTOs to decouple UI from data layer. Validation rules documented. PagedResult pattern for efficient list queries. Ready for service implementation and ViewModel integration.
