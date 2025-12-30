# Research: Shot History Filter

**Feature**: 001-shot-history-filter  
**Date**: 2025-12-30  
**Status**: Complete

## Research Tasks

### 1. UXDivers Popup Pattern for Filter UI

**Decision**: Use `ActionModalPopup` base class with custom content for filter selection.

**Rationale**: The existing codebase uses `ActionModalPopup` from UXDivers.Popups.Maui for modal dialogs (see `BagCreationPopup.cs`, `BeanAndBagCreationPopup.cs`). This provides:
- Consistent modal appearance with app theme
- Built-in animations (FadeIn/FadeOut)
- `IPopupService.Current.PushAsync/PopAsync` for navigation
- Callback pattern for returning results (`OnBagCreated` pattern)

**Alternatives considered**:
- Bottom sheet (`Plugin.Maui.BottomSheet`): Not currently in project, would add dependency
- Custom overlay: Inconsistent with existing patterns, more code
- Full page navigation: Breaks user flow, overkill for filter selection

**Implementation pattern**:
```csharp
public class ShotFilterPopup : ActionModalPopup
{
    public Action<ShotFilterCriteria>? OnFiltersApplied { get; set; }
    public Action? OnFiltersCleared { get; set; }
    public ShotFilterCriteria CurrentFilters { get; set; }
}
```

---

### 2. Multi-Select Filter UI Component

**Decision**: Use scrollable `VerticalStackLayout` with toggle-style selection for each filter category.

**Rationale**: MauiReactor components work best with simple layout primitives. Each filter category (Bean, Made For, Rating) gets a collapsible section with chip-style toggleable items.

**Alternatives considered**:
- Picker controls: Only support single selection, not multi-select
- CollectionView with SelectionMode.Multiple: More complex state management
- CheckBox list: Less visually appealing, takes more vertical space

**Implementation pattern**:
```csharp
// Filter section with chips
VStack(
    Label("Beans").ThemeKey(ThemeKeys.LabelSubtitle),
    FlexLayout(
        _availableBeans.Select(bean => 
            FilterChip(bean.Name, _selectedBeans.Contains(bean.Id), 
                () => ToggleBeanSelection(bean.Id)))
    ).Wrap(FlexWrap.Wrap)
)
```

---

### 3. Repository Combined Filter Query

**Decision**: Add single `GetFilteredAsync` method that accepts `ShotFilterCriteriaDto` with optional filter parameters.

**Rationale**: Existing repository has separate `GetByUserAsync`, `GetByBeanAsync`, etc. but no combined filter. Single method is cleaner than chaining multiple queries.

**Alternatives considered**:
- Chain existing methods: Would require in-memory filtering, poor performance
- Specification pattern: Over-engineered for 3 filter types
- Dynamic LINQ: Adds complexity without significant benefit

**Query approach** (EF Core):
```csharp
public async Task<List<ShotRecord>> GetFilteredAsync(
    ShotFilterCriteriaDto? criteria,
    int pageIndex, int pageSize)
{
    var query = _dbSet.AsNoTracking()
        .Include(s => s.Bag).ThenInclude(b => b.Bean)
        .Include(s => s.MadeFor)
        .Where(s => !s.IsDeleted);

    if (criteria?.BeanIds?.Any() == true)
        query = query.Where(s => s.Bag != null && 
            criteria.BeanIds.Contains(s.Bag.BeanId));

    if (criteria?.MadeForIds?.Any() == true)
        query = query.Where(s => s.MadeForId != null && 
            criteria.MadeForIds.Contains(s.MadeForId.Value));

    if (criteria?.Ratings?.Any() == true)
        query = query.Where(s => s.Rating != null && 
            criteria.Ratings.Contains(s.Rating.Value));

    return await query
        .OrderByDescending(s => s.Timestamp)
        .Skip(pageIndex * pageSize)
        .Take(pageSize)
        .ToListAsync();
}
```

---

### 4. Filter State Management in ActivityFeedPage

**Decision**: Add `ShotFilterCriteria` to `ActivityFeedState` and reset pagination when filters change.

**Rationale**: MauiReactor uses immutable state pattern. Filter state belongs with page state for single source of truth.

**State changes**:
```csharp
class ActivityFeedState
{
    // Existing...
    public ShotFilterCriteria? ActiveFilters { get; set; }
    public bool HasActiveFilters => ActiveFilters != null && 
        (ActiveFilters.BeanIds.Any() || 
         ActiveFilters.MadeForIds.Any() || 
         ActiveFilters.Ratings.Any());
    public int TotalCount { get; set; }  // For "Showing X of Y"
}
```

**Filter application flow**:
1. User opens popup → populate with available beans/people/ratings
2. User selects filters → update popup local state
3. User taps "Apply" → callback returns `ShotFilterCriteria`
4. ActivityFeedPage receives callback → `SetState()` with new filters
5. Page resets `PageIndex = 0`, `ShotRecords = []`, reloads data

---

### 5. Loading Available Filter Options

**Decision**: Load filter options from existing data (beans with shots, people with shots) when popup opens.

**Rationale**: Only show filter options that would return results. Prevents confusing empty results.

**Service methods needed**:
```csharp
// IShotService additions
Task<List<BeanSummaryDto>> GetBeansWithShotsAsync();
Task<List<UserProfileDto>> GetPeopleWithShotsAsync();
// Ratings are static 0-4, no query needed
```

**Alternatives considered**:
- Show all beans/people regardless of shots: Could confuse users with empty results
- Cache filter options: Premature optimization, popup opens infrequently

---

### 6. Visual Indicator for Active Filters

**Decision**: Add badge count to toolbar button and highlighted state when filters active.

**Rationale**: FR-008 requires visual indication. Badge is standard mobile pattern.

**Implementation**:
```csharp
// Toolbar button with badge
ToolbarItem("Filter")
    .IconImageSource(new FontImageSource
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Filter_list,
        Color = State.HasActiveFilters ? AppColors.Primary : AppColors.OnSurface
    })
    .OnClicked(OpenFilterPopup)
```

For badge count, use a custom view if needed (MauiReactor doesn't have built-in badge support on ToolbarItem).

---

### 7. Empty State Handling

**Decision**: Show contextual empty state when filters return no results with option to clear filters.

**Rationale**: NFR-UX2 requires helpful empty state message.

**Pattern**:
```csharp
if (State.ShotRecords.Count == 0 && State.HasActiveFilters)
{
    VStack(
        Image().Source(MaterialSymbolsFont.Filter_list_off),
        Label("No shots match your filters").ThemeKey(ThemeKeys.LabelTitle),
        Label("Try adjusting or clearing your filters"),
        Button("Clear Filters").OnClicked(ClearFilters)
    )
}
```

---

## Performance Considerations

1. **Filter query optimization**: Use indexed columns (BeanId via Bag, MadeForId, Rating, IsDeleted)
2. **Pagination preserved**: Filter query uses same Skip/Take pattern as existing
3. **Popup loading**: Load filter options async with loading indicator
4. **Memory**: Filter criteria is small (lists of ints), no memory concerns

## Dependencies Confirmed

- UXDivers.Popups.Maui 0.9.1 (existing)
- MauiReactor 4.0.9-beta (existing)
- Entity Framework Core 8.0 (existing)
- MaterialSymbolsFont (existing)

No new dependencies required.
