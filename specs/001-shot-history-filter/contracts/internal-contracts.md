# Internal Contracts: Shot History Filter

**Feature**: 001-shot-history-filter  
**Date**: 2025-12-30

## Service Layer Contract

### IShotService Extensions

New methods to add to `IShotService`:

```csharp
/// <summary>
/// Gets shot history filtered by specified criteria with pagination.
/// </summary>
/// <param name="criteria">Filter criteria (null or empty lists = no filter)</param>
/// <param name="pageIndex">Zero-based page index</param>
/// <param name="pageSize">Number of items per page</param>
/// <returns>Paged result with filtered shots</returns>
Task<PagedResult<ShotRecordDto>> GetFilteredShotHistoryAsync(
    ShotFilterCriteriaDto? criteria,
    int pageIndex,
    int pageSize);

/// <summary>
/// Gets beans that have at least one shot record.
/// Used to populate filter options.
/// </summary>
/// <returns>List of beans with shot history</returns>
Task<List<BeanSummaryDto>> GetBeansWithShotsAsync();

/// <summary>
/// Gets user profiles that have been marked as "Made For" on at least one shot.
/// Used to populate filter options.
/// </summary>
/// <returns>List of people with shot history</returns>
Task<List<UserProfileDto>> GetPeopleWithShotsAsync();
```

## Repository Layer Contract

### IShotRecordRepository Extensions

New methods to add to `IShotRecordRepository`:

```csharp
/// <summary>
/// Gets shot records filtered by specified criteria with pagination.
/// Applies AND logic for multiple criteria.
/// </summary>
Task<List<ShotRecord>> GetFilteredAsync(
    ShotFilterCriteriaDto? criteria,
    int pageIndex,
    int pageSize);

/// <summary>
/// Gets total count of shots matching filter criteria.
/// </summary>
Task<int> GetFilteredCountAsync(ShotFilterCriteriaDto? criteria);

/// <summary>
/// Gets distinct bean IDs that have associated shot records.
/// </summary>
Task<List<int>> GetBeanIdsWithShotsAsync();

/// <summary>
/// Gets distinct MadeFor user profile IDs from shot records.
/// </summary>
Task<List<int>> GetMadeForIdsWithShotsAsync();
```

## DTO Definitions

### ShotFilterCriteriaDto

```csharp
namespace BaristaNotes.Core.DTOs;

/// <summary>
/// Filter criteria for shot history queries.
/// Null or empty lists mean "no filter for this field".
/// </summary>
public record ShotFilterCriteriaDto
{
    /// <summary>
    /// Filter by bean IDs. Null/empty = include all beans.
    /// </summary>
    public IReadOnlyList<int>? BeanIds { get; init; }
    
    /// <summary>
    /// Filter by "Made For" user profile IDs. Null/empty = include all.
    /// </summary>
    public IReadOnlyList<int>? MadeForIds { get; init; }
    
    /// <summary>
    /// Filter by rating values (0-4). Null/empty = include all ratings.
    /// </summary>
    public IReadOnlyList<int>? Ratings { get; init; }
    
    /// <summary>
    /// Returns true if any filter criteria is specified.
    /// </summary>
    public bool HasFilters => 
        (BeanIds?.Count ?? 0) > 0 ||
        (MadeForIds?.Count ?? 0) > 0 ||
        (Ratings?.Count ?? 0) > 0;
}
```

## UI Layer Contract

### ShotFilterCriteria (UI Model)

```csharp
namespace BaristaNotes.Models;

/// <summary>
/// Mutable filter state for UI binding.
/// Converts to immutable ShotFilterCriteriaDto for service calls.
/// </summary>
public class ShotFilterCriteria
{
    public List<int> BeanIds { get; set; } = new();
    public List<int> MadeForIds { get; set; } = new();
    public List<int> Ratings { get; set; } = new();
    
    public bool HasFilters => 
        BeanIds.Count > 0 || 
        MadeForIds.Count > 0 || 
        Ratings.Count > 0;
    
    public int FilterCount => 
        BeanIds.Count + MadeForIds.Count + Ratings.Count;
    
    public ShotFilterCriteriaDto ToDto() => new()
    {
        BeanIds = BeanIds.Count > 0 ? BeanIds.ToList() : null,
        MadeForIds = MadeForIds.Count > 0 ? MadeForIds.ToList() : null,
        Ratings = Ratings.Count > 0 ? Ratings.ToList() : null
    };
    
    public void Clear()
    {
        BeanIds.Clear();
        MadeForIds.Clear();
        Ratings.Clear();
    }
    
    public ShotFilterCriteria Clone() => new()
    {
        BeanIds = new List<int>(BeanIds),
        MadeForIds = new List<int>(MadeForIds),
        Ratings = new List<int>(Ratings)
    };
}
```

### ShotFilterPopup Callback Contract

```csharp
namespace BaristaNotes.Integrations.Popups;

public class ShotFilterPopup : ActionModalPopup
{
    /// <summary>
    /// Current filter state to initialize popup with.
    /// </summary>
    public ShotFilterCriteria CurrentFilters { get; set; } = new();
    
    /// <summary>
    /// Available beans for filter selection.
    /// </summary>
    public List<BeanSummaryDto> AvailableBeans { get; set; } = new();
    
    /// <summary>
    /// Available people for "Made For" filter selection.
    /// </summary>
    public List<UserProfileDto> AvailablePeople { get; set; } = new();
    
    /// <summary>
    /// Callback invoked when user applies filters.
    /// </summary>
    public Action<ShotFilterCriteria>? OnFiltersApplied { get; set; }
    
    /// <summary>
    /// Callback invoked when user clears all filters.
    /// </summary>
    public Action? OnFiltersCleared { get; set; }
}
```

## ActivityFeedState Extensions

```csharp
partial class ActivityFeedState
{
    // New filter-related state
    public ShotFilterCriteria ActiveFilters { get; set; } = new();
    public int TotalShotCount { get; set; }
    public int FilteredShotCount { get; set; }
    
    // Computed properties
    public bool HasActiveFilters => ActiveFilters.HasFilters;
    public string ResultCountText => HasActiveFilters 
        ? $"Showing {FilteredShotCount} of {TotalShotCount} shots"
        : $"{TotalShotCount} shots";
}
```

## Method Signatures Summary

| Layer | Method | Input | Output |
|-------|--------|-------|--------|
| Service | `GetFilteredShotHistoryAsync` | `ShotFilterCriteriaDto?, int, int` | `PagedResult<ShotRecordDto>` |
| Service | `GetBeansWithShotsAsync` | - | `List<BeanSummaryDto>` |
| Service | `GetPeopleWithShotsAsync` | - | `List<UserProfileDto>` |
| Repository | `GetFilteredAsync` | `ShotFilterCriteriaDto?, int, int` | `List<ShotRecord>` |
| Repository | `GetFilteredCountAsync` | `ShotFilterCriteriaDto?` | `int` |
| Repository | `GetBeanIdsWithShotsAsync` | - | `List<int>` |
| Repository | `GetMadeForIdsWithShotsAsync` | - | `List<int>` |
