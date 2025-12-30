# Quickstart: Shot History Filter

**Feature**: 001-shot-history-filter  
**Date**: 2025-12-30

## Overview

This feature adds filtering to the ActivityFeedPage (shot history). Users can filter by Bean, Made For (person), and Rating using a popup accessible via toolbar.

## Key Components

```
┌─────────────────────────────────────────────────────────────────┐
│                     ActivityFeedPage                             │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ Toolbar: [Filter Icon] ─────────────────────────────────┐│  │
│  │                                                          ││  │
│  │  ┌────────────────────────────────────────────────────┐ ││  │
│  │  │ "Showing 12 of 45 shots"  (when filtered)          │ ││  │
│  │  └────────────────────────────────────────────────────┘ ││  │
│  │                                                          ││  │
│  │  ┌────────────────────────────────────────────────────┐ ││  │
│  │  │ CollectionView with ShotRecordDto items            │ ││  │
│  │  │ (filtered or unfiltered based on state)            │ ││  │
│  │  └────────────────────────────────────────────────────┘ ││  │
│  └──────────────────────────────────────────────────────────┘│  │
│                                                               │  │
│  Opens ShotFilterPopup ◄──────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────────┐
│                     ShotFilterPopup                              │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ BEANS                                              [Clear]│  │
│  │ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐                      │  │
│  │ │Bean A│ │Bean B│ │Bean C│ │Bean D│  ◄── Toggle chips    │  │
│  │ └──────┘ └──────┘ └──────┘ └──────┘                      │  │
│  ├───────────────────────────────────────────────────────────┤  │
│  │ MADE FOR                                                  │  │
│  │ ┌──────┐ ┌──────┐ ┌──────┐                               │  │
│  │ │ Me   │ │Partner│ │Guest │  ◄── Toggle chips            │  │
│  │ └──────┘ └──────┘ └──────┘                               │  │
│  ├───────────────────────────────────────────────────────────┤  │
│  │ RATING                                                    │  │
│  │ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐                            │  │
│  │ │ 0 │ │ 1 │ │ 2 │ │ 3 │ │ 4 │  ◄── Rating toggles       │  │
│  │ └───┘ └───┘ └───┘ └───┘ └───┘      (sentiment icons)     │  │
│  ├───────────────────────────────────────────────────────────┤  │
│  │        [Clear All]        [Apply Filters]                 │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

## Data Flow

```
User taps Filter ──► Open ShotFilterPopup
                            │
                            ▼
            ┌───────────────────────────────┐
            │ Load filter options from:     │
            │ - GetBeansWithShotsAsync()    │
            │ - GetPeopleWithShotsAsync()   │
            │ - Static ratings (0-4)        │
            └───────────────────────────────┘
                            │
                            ▼
            User selects filters (multi-select)
                            │
                            ▼
            User taps "Apply Filters"
                            │
                            ▼
            ┌───────────────────────────────┐
            │ Callback: OnFiltersApplied    │
            │ with ShotFilterCriteria       │
            └───────────────────────────────┘
                            │
                            ▼
            ┌───────────────────────────────┐
            │ ActivityFeedPage:             │
            │ 1. SetState(ActiveFilters)    │
            │ 2. Reset PageIndex = 0        │
            │ 3. Clear ShotRecords          │
            │ 4. Load filtered data         │
            └───────────────────────────────┘
                            │
                            ▼
            ┌───────────────────────────────┐
            │ ShotService calls Repository: │
            │ GetFilteredShotHistoryAsync() │
            └───────────────────────────────┘
                            │
                            ▼
            ┌───────────────────────────────┐
            │ Repository builds query:      │
            │ WHERE BeanId IN (...)         │
            │   AND MadeForId IN (...)      │
            │   AND Rating IN (...)         │
            │   AND IsDeleted = false       │
            └───────────────────────────────┘
```

## File Locations

| Component | Path |
|-----------|------|
| Filter Popup | `BaristaNotes/Integrations/Popups/ShotFilterPopup.cs` |
| Filter Model (UI) | `BaristaNotes/Models/ShotFilterCriteria.cs` |
| Filter DTO (Core) | `BaristaNotes.Core/DTOs/ShotFilterCriteriaDto.cs` |
| Activity Page | `BaristaNotes/Pages/ActivityFeedPage.cs` (modify) |
| Shot Service | `BaristaNotes/Services/IShotService.cs` (modify) |
| Shot Repository | `BaristaNotes.Core/Data/Repositories/ShotRecordRepository.cs` (modify) |

## Implementation Order

1. **Core Layer First** (no UI dependencies)
   - `ShotFilterCriteriaDto.cs` - DTO record
   - `IShotRecordRepository.cs` - Add interface methods
   - `ShotRecordRepository.cs` - Implement filter query

2. **Service Layer**
   - `IShotService.cs` - Add interface methods
   - `ShotService.cs` - Implement with DTO mapping

3. **UI Layer**
   - `ShotFilterCriteria.cs` - UI model
   - `ShotFilterPopup.cs` - Popup component
   - `ActivityFeedPage.cs` - Integration

4. **Tests**
   - Repository filter tests (integration)
   - Service filter tests (unit)
   - Filter criteria model tests (unit)

## Code Patterns

### Filter Query (Repository)

```csharp
public async Task<List<ShotRecord>> GetFilteredAsync(
    ShotFilterCriteriaDto? criteria,
    int pageIndex, int pageSize)
{
    var query = _dbSet.AsNoTracking()
        .Include(s => s.Bag).ThenInclude(b => b!.Bean)
        .Include(s => s.MadeFor)
        .Where(s => !s.IsDeleted);

    if (criteria?.BeanIds?.Count > 0)
        query = query.Where(s => s.Bag != null && 
            criteria.BeanIds.Contains(s.Bag.BeanId));

    if (criteria?.MadeForIds?.Count > 0)
        query = query.Where(s => s.MadeForId != null && 
            criteria.MadeForIds.Contains(s.MadeForId.Value));

    if (criteria?.Ratings?.Count > 0)
        query = query.Where(s => s.Rating != null && 
            criteria.Ratings.Contains(s.Rating.Value));

    return await query
        .OrderByDescending(s => s.Timestamp)
        .Skip(pageIndex * pageSize)
        .Take(pageSize)
        .ToListAsync();
}
```

### Toolbar Filter Button (MauiReactor)

```csharp
ToolbarItem("Filter")
    .IconImageSource(new FontImageSource
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Filter_list,
        Color = State.HasActiveFilters 
            ? AppColors.Primary 
            : AppColors.OnSurface
    })
    .OnClicked(async () => await OpenFilterPopup())
```

### Filter Chip Component

```csharp
Border FilterChip(string label, bool isSelected, Action onTap) =>
    Border()
        .Padding(8, 4)
        .StrokeShape(new RoundRectangle { CornerRadius = 16 })
        .BackgroundColor(isSelected ? AppColors.Primary : AppColors.Surface)
        .StrokeThickness(1)
        .Stroke(isSelected ? AppColors.Primary : AppColors.Outline)
        .GestureRecognizers(new TapGestureRecognizer().OnTapped(onTap))
        .Content(
            Label(label)
                .TextColor(isSelected ? AppColors.OnPrimary : AppColors.OnSurface)
                .FontSize(AppFontSizes.Small)
        );
```

## Testing Strategy

### Repository Tests (Integration)

```csharp
[Fact]
public async Task GetFilteredAsync_ByBean_ReturnsOnlyMatchingShots()
{
    // Arrange: Create shots with different beans
    // Act: Call GetFilteredAsync with single bean ID
    // Assert: All returned shots have the filtered bean
}

[Fact]
public async Task GetFilteredAsync_MultipleFilters_AppliesAndLogic()
{
    // Arrange: Create shots with various combinations
    // Act: Call with bean + rating filter
    // Assert: Only shots matching BOTH criteria returned
}
```

### Service Tests (Unit)

```csharp
[Fact]
public async Task GetFilteredShotHistoryAsync_MapsToDto()
{
    // Arrange: Mock repository to return shot records
    // Act: Call service method
    // Assert: Returns PagedResult<ShotRecordDto> with correct mapping
}
```

## Accessibility Checklist

- [ ] Filter button has accessible name ("Filter shots")
- [ ] Filter chips announce selected/unselected state
- [ ] Result count announced when filters applied
- [ ] Touch targets ≥ 44x44px
- [ ] Color contrast meets WCAG AA
