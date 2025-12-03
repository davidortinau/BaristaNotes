# Service Contracts: IShotService Extensions

**Feature**: Edit and Delete Shots from Activity Page  
**Date**: 2025-12-03

## Overview

This document defines the service contract extensions for shot edit and delete operations. These extend the existing `IShotService` interface.

---

## IShotService Interface Extensions

**Namespace**: `BaristaNotes.Core.Services`  
**File**: `BaristaNotes.Core/Services/IShotService.cs`

### New Methods

```csharp
namespace BaristaNotes.Core.Services;

public interface IShotService
{
    // ... existing methods ...
    
    /// <summary>
    /// Updates editable fields of an existing shot record.
    /// Only ActualTime, ActualOutput, Rating, and DrinkType can be modified.
    /// Immutable fields (Timestamp, Bean, GrindSetting, DoseIn, etc.) are preserved.
    /// </summary>
    /// <param name="id">The shot record ID to update</param>
    /// <param name="dto">DTO containing fields to update</param>
    /// <returns>Updated shot record DTO with all fields</returns>
    /// <exception cref="NotFoundException">Thrown when shot with given ID does not exist</exception>
    /// <exception cref="ValidationException">Thrown when DTO contains invalid values</exception>
    /// <remarks>
    /// Performance target: <2 seconds including validation and database update.
    /// Updates LastModifiedAt timestamp automatically for sync compatibility.
    /// </remarks>
    Task<ShotRecordDto> UpdateShotAsync(int id, UpdateShotDto dto);
    
    /// <summary>
    /// Soft deletes a shot record by setting IsDeleted flag to true.
    /// Physical deletion does not occur - record is retained for sync purposes.
    /// Deleted shots are filtered from all query results automatically.
    /// </summary>
    /// <param name="id">The shot record ID to delete</param>
    /// <exception cref="NotFoundException">Thrown when shot with given ID does not exist</exception>
    /// <remarks>
    /// Performance target: <1 second including database update.
    /// Updates LastModifiedAt timestamp automatically for sync compatibility.
    /// This is a soft delete - record remains in database with IsDeleted=true.
    /// </remarks>
    Task DeleteShotAsync(int id);
}
```

---

## Data Transfer Objects

### UpdateShotDto

**Namespace**: `BaristaNotes.Core.Services.DTOs`  
**File**: `BaristaNotes.Core/Services/DTOs/UpdateShotDto.cs`

```csharp
namespace BaristaNotes.Core.Services.DTOs;

/// <summary>
/// DTO for updating editable fields of a shot record.
/// Only mutable fields included - immutable fields (timestamp, bean, grind settings, etc.) 
/// are not included and will be preserved during update.
/// </summary>
public class UpdateShotDto
{
    /// <summary>
    /// Actual shot extraction time in seconds.
    /// Optional - null means no change to existing value.
    /// If provided, must be greater than 0 and less than 999.
    /// </summary>
    /// <example>28.5</example>
    public decimal? ActualTime { get; set; }
    
    /// <summary>
    /// Actual output weight in grams.
    /// Optional - null means no change to existing value.
    /// If provided, must be greater than 0 and less than 200.
    /// </summary>
    /// <example>42.0</example>
    public decimal? ActualOutput { get; set; }
    
    /// <summary>
    /// Taste rating on 1-5 scale (1=dislike, 5=excellent).
    /// Optional - null means no change to existing value.
    /// If provided, must be between 1 and 5 inclusive.
    /// </summary>
    /// <example>4</example>
    public int? Rating { get; set; }
    
    /// <summary>
    /// Type of drink made (e.g., "Espresso", "Latte", "Americano").
    /// Required - cannot be null or empty.
    /// </summary>
    /// <example>Latte</example>
    public string DrinkType { get; set; } = string.Empty;
}
```

---

## Validation Rules

### UpdateShotDto Validation

**When**: Before calling `UpdateShotAsync`  
**Where**: `ShotService.ValidateUpdateShot(UpdateShotDto dto)` private method

| Field | Rule | Error Message |
|-------|------|---------------|
| ActualTime | If not null: must be > 0 and < 999 | "Shot time must be between 0 and 999 seconds" |
| ActualOutput | If not null: must be > 0 and < 200 | "Output weight must be between 0 and 200 grams" |
| Rating | If not null: must be >= 1 and <= 5 | "Rating must be between 1 and 5 stars" |
| DrinkType | Cannot be null or whitespace | "Drink type is required" |

**Exception Type**: `ValidationException` with list of all validation errors

---

## Exception Contracts

### NotFoundException

**Namespace**: `BaristaNotes.Core.Services.Exceptions`  
**File**: Already exists

```csharp
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
```

**When Thrown**:
- `UpdateShotAsync`: Shot with given ID not found in database
- `DeleteShotAsync`: Shot with given ID not found in database

**Message Format**: `"Shot with ID {id} not found"`

### ValidationException

**Namespace**: `BaristaNotes.Core.Services.Exceptions`  
**File**: Already exists

```csharp
public class ValidationException : Exception
{
    public List<string> Errors { get; }
    
    public ValidationException(List<string> errors) 
        : base(string.Join("; ", errors))
    {
        Errors = errors;
    }
}
```

**When Thrown**:
- `UpdateShotAsync`: One or more fields in DTO fail validation

**Error List**: Contains all validation error messages from table above

---

## Usage Examples

### Update Shot Example

```csharp
// Get the shot service
var shotService = serviceProvider.GetRequiredService<IShotService>();

// Create update DTO with new values
var updateDto = new UpdateShotDto
{
    ActualTime = 28.5m,           // Updated extraction time
    ActualOutput = 42.0m,         // Updated output weight
    Rating = 4,                   // Updated rating
    DrinkType = "Latte"           // Updated drink type
};

try
{
    // Update the shot
    var updated = await shotService.UpdateShotAsync(shotId: 123, updateDto);
    
    // Success - show feedback to user
    await feedbackService.ShowSuccessAsync("Shot updated successfully");
    
    // Updated DTO contains all fields (including unchanged ones)
    Console.WriteLine($"Updated shot: {updated.Timestamp}, Rating: {updated.Rating}");
}
catch (NotFoundException ex)
{
    // Shot doesn't exist
    await feedbackService.ShowErrorAsync($"Shot not found: {ex.Message}");
}
catch (ValidationException ex)
{
    // Invalid values provided
    var errors = string.Join("\n", ex.Errors);
    await feedbackService.ShowErrorAsync($"Validation errors:\n{errors}");
}
```

### Delete Shot Example

```csharp
// Get the shot service
var shotService = serviceProvider.GetRequiredService<IShotService>();

try
{
    // Soft delete the shot
    await shotService.DeleteShotAsync(shotId: 123);
    
    // Success - show feedback to user
    await feedbackService.ShowSuccessAsync("Shot deleted");
    
    // Refresh the activity feed (shot will no longer appear)
    await RefreshActivityFeedAsync();
}
catch (NotFoundException ex)
{
    // Shot doesn't exist (maybe already deleted)
    await feedbackService.ShowErrorAsync($"Shot not found: {ex.Message}");
}
```

### Partial Update Example

```csharp
// Only update rating - leave other fields unchanged
var updateDto = new UpdateShotDto
{
    ActualTime = null,      // No change
    ActualOutput = null,    // No change
    Rating = 5,             // Update rating to excellent
    DrinkType = "Espresso"  // Required field (must provide current value if not changing)
};

var updated = await shotService.UpdateShotAsync(shotId: 123, updateDto);
```

---

## Performance Guarantees

| Operation | Target | Measured By |
|-----------|--------|-------------|
| UpdateShotAsync | <2 seconds | Time from method call to return |
| DeleteShotAsync | <1 second | Time from method call to return |
| Validation | <100ms | Time to validate DTO before database call |

**Note**: Performance targets include database operations but exclude UI rendering time.

---

## Sync Compatibility

Both operations maintain CoreSync.Sqlite compatibility:

1. **UpdateShotAsync**: Updates `LastModifiedAt` timestamp to trigger sync
2. **DeleteShotAsync**: Sets `IsDeleted=true` and updates `LastModifiedAt` for soft delete pattern
3. **No physical deletes**: Ensures sync can track deletion across devices
4. **Query filtering**: Repository automatically filters `IsDeleted=true` records from results

---

## Testing Checklist

### Unit Tests (ShotServiceTests.cs)

- [ ] UpdateShotAsync with valid DTO updates all fields
- [ ] UpdateShotAsync with partial DTO updates only provided fields
- [ ] UpdateShotAsync throws NotFoundException for invalid ID
- [ ] UpdateShotAsync throws ValidationException for invalid ActualTime
- [ ] UpdateShotAsync throws ValidationException for invalid ActualOutput
- [ ] UpdateShotAsync throws ValidationException for invalid Rating
- [ ] UpdateShotAsync throws ValidationException for empty DrinkType
- [ ] UpdateShotAsync updates LastModifiedAt timestamp
- [ ] DeleteShotAsync sets IsDeleted flag and LastModifiedAt
- [ ] DeleteShotAsync throws NotFoundException for invalid ID

### Integration Tests (ShotDatabaseTests.cs)

- [ ] UpdateShotAsync persists changes to database
- [ ] DeleteShotAsync soft deletes and filters from queries
- [ ] Updated shots appear in GetShotHistoryAsync results
- [ ] Deleted shots do NOT appear in GetShotHistoryAsync results
- [ ] UpdateShotAsync preserves immutable fields
- [ ] Multiple updates maintain data integrity

---

## Summary

- **Two new methods** added to IShotService interface
- **One new DTO** for update operations (UpdateShotDto)
- **Existing exceptions** reused (NotFoundException, ValidationException)
- **Soft delete pattern** for sync compatibility
- **Performance targets** defined and measurable
- **Comprehensive validation** with clear error messages
- **100% backward compatible** - existing methods unchanged
