# Data Model: Edit and Delete Shots Feature

**Feature**: Edit and Delete Shots from Activity Page  
**Date**: 2025-12-03  
**Status**: Complete

## Overview

This feature extends existing data models and adds new DTOs for update operations. No database schema changes required - leveraging existing `ShotRecord` model and soft delete capability.

## Entities

### Existing: ShotRecord (No Changes)

**Location**: `BaristaNotes.Core/Models/ShotRecord.cs`

The existing model already supports all required fields. No modifications needed.

**Relevant Fields for Edit Operations**:
- `Id` (int, PK) - Identifies shot to edit
- `ActualTime` (decimal?, nullable) - Editable
- `ActualOutput` (decimal?, nullable) - Editable  
- `Rating` (int?, nullable) - Editable
- `DrinkType` (string) - Editable
- `LastModifiedAt` (DateTimeOffset) - Updated on edit
- `IsDeleted` (bool) - Set to true on delete

**Immutable Fields** (readonly in edit UI):
- `Timestamp` - Creation time, never changes
- `BeanId` - Recipe parameter, preserved
- `GrindSetting` - Recipe parameter, preserved
- `DoseIn` - Recipe parameter, preserved
- `ExpectedTime` - Original expectation, preserved
- `ExpectedOutput` - Original expectation, preserved
- `MachineId`, `GrinderId` - Equipment setup, preserved
- `MadeById`, `MadeForId` - User context, preserved

**Validation Rules**:
- ActualTime: 0 < value < 999 seconds (if provided)
- ActualOutput: 0 < value < 200 grams (if provided)
- Rating: 1 <= value <= 5 (if provided, nullable allowed)
- DrinkType: Cannot be null or empty
- IsDeleted: Must be false for visible shots

**State Transitions**:
```
[Active Shot] --Edit--> [Update ActualTime/Output/Rating/DrinkType] --> [Active Shot (modified)]
[Active Shot] --Delete--> [Soft Deleted (IsDeleted=true)]
```

---

## Data Transfer Objects (DTOs)

### NEW: UpdateShotDto

**Location**: `BaristaNotes.Core/Services/DTOs/UpdateShotDto.cs`

**Purpose**: Encapsulates editable fields for shot update operations

**Fields**:
```csharp
public class UpdateShotDto
{
    public decimal? ActualTime { get; set; }
    public decimal? ActualOutput { get; set; }
    public int? Rating { get; set; }
    public string DrinkType { get; set; } = string.Empty;
}
```

**Validation**:
- ActualTime: Optional, but if provided must be > 0 and < 999
- ActualOutput: Optional, but if provided must be > 0 and < 200
- Rating: Optional, but if provided must be 1-5
- DrinkType: Required, cannot be empty

**Usage**:
```csharp
var dto = new UpdateShotDto
{
    ActualTime = 28.5m,
    ActualOutput = 42.0m,
    Rating = 4,
    DrinkType = "Latte"
};

var updated = await shotService.UpdateShotAsync(shotId, dto);
```

---

### Existing: ShotRecordDto (No Changes)

**Location**: `BaristaNotes.Core/Services/DTOs/ShotRecordDto.cs`

Already used for reading shot data. No modifications needed. Returned by `GetShotByIdAsync()` and `UpdateShotAsync()`.

---

## Service Contracts

### Updated: IShotService

**Location**: `BaristaNotes.Core/Services/IShotService.cs`

**Existing Methods** (no changes):
```csharp
Task<ShotRecordDto?> GetMostRecentShotAsync();
Task<ShotRecordDto> CreateShotAsync(CreateShotDto dto);
Task<PagedResult<ShotRecordDto>> GetShotHistoryAsync(int pageIndex, int pageSize);
Task<ShotRecordDto?> GetShotByIdAsync(int id);
```

**New Methods**:
```csharp
Task<ShotRecordDto> UpdateShotAsync(int id, UpdateShotDto dto);
Task DeleteShotAsync(int id);
```

**Method Details**:

#### UpdateShotAsync
```csharp
/// <summary>
/// Updates editable fields of an existing shot record
/// </summary>
/// <param name="id">Shot record ID</param>
/// <param name="dto">Fields to update</param>
/// <returns>Updated shot record DTO</returns>
/// <exception cref="NotFoundException">Shot not found</exception>
/// <exception cref="ValidationException">Invalid field values</exception>
Task<ShotRecordDto> UpdateShotAsync(int id, UpdateShotDto dto);
```

**Behavior**:
- Validates shot exists (throws NotFoundException if not)
- Validates DTO fields (throws ValidationException if invalid)
- Updates only provided fields on ShotRecord entity
- Sets LastModifiedAt to current time
- Persists changes via repository
- Returns updated ShotRecordDto

#### DeleteShotAsync
```csharp
/// <summary>
/// Soft deletes a shot record (sets IsDeleted flag)
/// </summary>
/// <param name="id">Shot record ID</param>
/// <exception cref="NotFoundException">Shot not found</exception>
Task DeleteShotAsync(int id);
```

**Behavior**:
- Validates shot exists (throws NotFoundException if not)
- Sets IsDeleted = true
- Sets LastModifiedAt to current time
- Persists changes via repository
- Does NOT physically delete from database (soft delete for sync)

---

## Repository Layer (No Changes Required)

**Existing**: `IShotRecordRepository` already has:
- `GetByIdAsync(int id)` - Used by edit/delete operations
- `UpdateAsync(ShotRecord shot)` - Used for both edit and delete
- `GetPagedAsync(...)` - Already filters IsDeleted=false

No new repository methods needed.

---

## Component State Models (MVU)

### EditShotPage State

**Location**: `BaristaNotes/Pages/EditShotPage.cs` (NEW)

```csharp
class EditShotPageState
{
    public int ShotId { get; set; }
    
    // Original (immutable) values for display
    public DateTimeOffset Timestamp { get; set; }
    public string BeanName { get; set; }
    public string GrindSetting { get; set; }
    public decimal DoseIn { get; set; }
    
    // Editable fields
    public decimal ActualTime { get; set; }
    public decimal ActualOutput { get; set; }
    public int? Rating { get; set; }
    public string DrinkType { get; set; }
    
    // UI state
    public bool IsLoading { get; set; }
    public bool IsSaving { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
}

enum EditShotMessage
{
    Load,
    Loaded,
    ActualTimeChanged,
    ActualOutputChanged,
    RatingChanged,
    DrinkTypeChanged,
    Save,
    Saved,
    Cancel,
    ValidationFailed
}
```

### ActivityFeedPage State Extension

**Location**: `BaristaNotes/Pages/ActivityFeedPage.cs` (MODIFY)

Add to existing state:
```csharp
public int? ShotToDelete { get; set; }
public bool ShowDeleteConfirmation { get; set; }
public int? ShotToEdit { get; set; }
```

Add to existing message enum:
```csharp
DeleteRequested,
DeleteConfirmed,
DeleteCanceled,
EditRequested,
RefreshAfterDelete,
RefreshAfterEdit
```

---

## Validation Rules Summary

| Field | Required | Type | Range | Error Message |
|-------|----------|------|-------|---------------|
| ActualTime | No | decimal | 0 < x < 999 | "Shot time must be between 0 and 999 seconds" |
| ActualOutput | No | decimal | 0 < x < 200 | "Output weight must be between 0 and 200 grams" |
| Rating | No | int | 1 <= x <= 5 | "Rating must be between 1 and 5 stars" |
| DrinkType | Yes | string | Not empty | "Drink type is required" |

---

## Data Flow Diagrams

### Edit Flow
```
[ActivityFeed] --Swipe Edit--> [EditShotPage]
     |                              |
     |                         Load shot data
     |                              |
     |                         User edits fields
     |                              |
     |                         Validate + Save
     |                              |
     |<--Navigate back + Refresh----+
```

### Delete Flow
```
[ActivityFeed] --Swipe Delete--> [Show Confirmation Popup]
     |                                    |
     |                              User confirms
     |                                    |
     |                         Soft delete (IsDeleted=true)
     |                                    |
     |<--Refresh list (shot removed)------+
```

---

## Summary

- **No database migrations required** - all needed fields exist
- **One new DTO**: UpdateShotDto for edit operations
- **Two new service methods**: UpdateShotAsync, DeleteShotAsync
- **Soft delete pattern** using existing IsDeleted flag
- **Immutable creation data** preserved during edits
- **Client-side validation** before service calls
- **MVU state management** for edit form and delete confirmation

Ready to proceed to contracts generation and quickstart guide.
