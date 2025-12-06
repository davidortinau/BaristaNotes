# Research: Bean Detail Page

**Feature**: 004-bean-detail-page  
**Date**: December 5, 2025

## Overview

This document captures research findings to resolve technical unknowns before implementation. All NEEDS CLARIFICATION items from Technical Context have been investigated.

---

## 1. MauiReactor Full-Page Form Pattern

**Question**: What is the established pattern for full-page forms with props-based navigation?

**Decision**: Follow `ProfileFormPage` pattern exactly

**Rationale**:
- ProfileFormPage already implements the full-page form pattern with optional ID props
- Uses `Component<TState, TProps>` for parameter passing via Shell navigation
- Consistent with codebase conventions

**Implementation Pattern**:
```csharp
// Props class for navigation parameters
class BeanDetailPageProps
{
    public int? BeanId { get; set; }
}

// State class for form data
class BeanDetailPageState
{
    public int? BeanId { get; set; }
    public string Name { get; set; } = "";
    public string Roaster { get; set; } = "";
    public string Origin { get; set; } = "";
    public bool TrackRoastDate { get; set; }
    public DateTime RoastDate { get; set; } = DateTime.Now;
    public string Notes { get; set; } = "";
    public bool IsSaving { get; set; }
    public string? ErrorMessage { get; set; }
    // Shot history
    public List<ShotRecordDto> Shots { get; set; } = new();
    public bool IsLoadingShots { get; set; }
    public bool HasMoreShots { get; set; }
    public int ShotPageIndex { get; set; }
}

// Page component
partial class BeanDetailPage : Component<BeanDetailPageState, BeanDetailPageProps>
{
    [Inject] IBeanService _beanService;
    [Inject] IShotService _shotService;
    [Inject] IFeedbackService _feedbackService;
    
    protected override void OnMounted()
    {
        if (Props.BeanId.HasValue && Props.BeanId.Value > 0)
        {
            SetState(s => s.BeanId = Props.BeanId);
            _ = LoadBeanAsync();
            _ = LoadShotsAsync();
        }
    }
}
```

**Alternatives Considered**:
- Bottom sheet (current): Cramped on small screens, doesn't show shot history well
- Modal dialog: Same issues as bottom sheet

---

## 2. Shot History Pagination Pattern

**Question**: How to implement paginated shot history with infinite scroll?

**Decision**: Use `CollectionView` with `RemainingItemsThreshold` for pagination

**Rationale**:
- `IShotService.GetShotHistoryByBeanAsync(beanId, pageIndex, pageSize)` already exists
- Returns `PagedResult<ShotRecordDto>` with `HasNextPage` property
- CollectionView supports `RemainingItemsThresholdReached` for lazy loading

**Implementation Pattern**:
```csharp
// In state
public List<ShotRecordDto> Shots { get; set; } = new();
public bool IsLoadingShots { get; set; }
public bool HasMoreShots { get; set; }
public int ShotPageIndex { get; set; }

// Load method
async Task LoadShotsAsync()
{
    if (State.IsLoadingShots || !State.BeanId.HasValue) return;
    
    SetState(s => s.IsLoadingShots = true);
    
    var result = await _shotService.GetShotHistoryByBeanAsync(
        State.BeanId.Value, 
        State.ShotPageIndex, 
        pageSize: 20);
    
    SetState(s =>
    {
        s.Shots = new List<ShotRecordDto>(s.Shots.Concat(result.Items));
        s.HasMoreShots = result.HasNextPage;
        s.ShotPageIndex = result.PageIndex + 1;
        s.IsLoadingShots = false;
    });
}

// In Render
CollectionView()
    .ItemsSource(State.Shots, shot => new ShotRecordCard().Shot(shot))
    .RemainingItemsThreshold(5)
    .RemainingItemsThresholdReached(async () => await LoadMoreShotsAsync())
```

**Alternatives Considered**:
- Load all shots at once: Performance issue for beans with many shots
- Manual "Load More" button: Worse UX than automatic pagination

---

## 3. Shell Navigation with Props

**Question**: How to navigate to BeanDetailPage with optional BeanId?

**Decision**: Use MauiReactor's typed navigation with Props

**Rationale**:
- Matches existing ProfileFormPage navigation pattern
- Type-safe parameter passing
- Works with Shell back navigation

**Implementation Pattern**:
```csharp
// Register route in MauiProgram.cs
MauiReactor.Routing.RegisterRoute<Pages.BeanDetailPage>("bean-detail");

// Navigate from BeanManagementPage
// For new bean (no ID)
await Shell.Current.GoToAsync("bean-detail");

// For existing bean (with ID)
await Shell.Current.GoToAsync<BeanDetailPageProps>("bean-detail", props => 
{
    props.BeanId = bean.Id;
});
```

**Alternatives Considered**:
- Query string parameters: Not type-safe, MauiReactor Props pattern preferred
- Global state: Unnecessary complexity

---

## 4. Validation Pattern

**Question**: How to implement inline form validation?

**Decision**: Client-side validation with inline error display, service-level validation as backup

**Rationale**:
- Matches BeanFormSheet validation pattern (name required)
- Service already validates (BeanService.ValidateCreateBean)
- Immediate feedback improves UX

**Validation Rules**:
| Field | Rule | Error Message |
|-------|------|---------------|
| Name | Required | "Bean name is required" |
| RoastDate | ≤ Today (if provided) | "Roast date cannot be in the future" |

**Implementation Pattern**:
```csharp
bool ValidateForm()
{
    if (string.IsNullOrWhiteSpace(State.Name))
    {
        SetState(s => s.ErrorMessage = "Bean name is required");
        return false;
    }
    
    if (State.TrackRoastDate && State.RoastDate > DateTime.Now)
    {
        SetState(s => s.ErrorMessage = "Roast date cannot be in the future");
        return false;
    }
    
    SetState(s => s.ErrorMessage = null);
    return true;
}
```

---

## 5. Delete Confirmation Pattern

**Question**: How to show delete confirmation dialog?

**Decision**: Use `DisplayAlert` (consistent with ProfileFormPage)

**Rationale**:
- ProfileFormPage uses this exact pattern
- Native platform dialog, accessible
- Simple to implement

**Implementation Pattern**:
```csharp
async Task DeleteBeanAsync()
{
    if (!State.BeanId.HasValue) return;
    
    var confirmed = await Application.Current!.MainPage!.DisplayAlert(
        "Delete Bean",
        $"Are you sure you want to delete '{State.Name}'? This action cannot be undone.",
        "Delete",
        "Cancel");
    
    if (!confirmed) return;
    
    await _beanService.DeleteBeanAsync(State.BeanId.Value);
    await _feedbackService.ShowSuccessAsync($"Bean '{State.Name}' deleted");
    await Shell.Current.GoToAsync("..");
}
```

---

## 6. Shot Item Tap Navigation

**Question**: How to navigate to shot detail when tapping a shot in the history?

**Decision**: Use TapGestureRecognizer on ShotRecordCard wrapper with navigation to shot-logging page

**Rationale**:
- ShotLoggingPage exists and accepts shot ID via props
- Existing pattern in ActivityFeedPage for shot editing

**Implementation Pattern**:
```csharp
// Wrap ShotRecordCard with tappable container
Border(
    new ShotRecordCard().Shot(shot)
)
.GestureRecognizers(
    TapGestureRecognizer()
        .OnTapped(async () => await NavigateToShotAsync(shot.Id))
)

async Task NavigateToShotAsync(int shotId)
{
    await Shell.Current.GoToAsync<ShotLoggingPageProps>("shot-logging", props =>
    {
        props.ShotId = shotId;
    });
}
```

---

## 7. Empty State Pattern

**Question**: What to show when a bean has no shots?

**Decision**: Simple centered message, consistent with BeanManagementPage empty state

**Rationale**:
- Matches existing empty state patterns in the app
- Clear, non-blocking message

**Implementation Pattern**:
```csharp
VisualNode RenderEmptyShots()
{
    return VStack(spacing: 12,
        Label("📋")
            .FontSize(48)
            .HCenter(),
        Label("No shots recorded with this bean yet")
            .ThemeKey(ThemeKeys.SecondaryText)
            .HCenter()
    )
    .Padding(24);
}
```

---

## Unknowns Resolved

✅ **MauiReactor full-page form pattern**: Use ProfileFormPage as template  
✅ **Pagination implementation**: CollectionView with RemainingItemsThreshold  
✅ **Shell navigation with props**: Typed navigation pattern  
✅ **Validation approach**: Client-side with inline errors  
✅ **Delete confirmation**: Native DisplayAlert  
✅ **Shot tap navigation**: TapGestureRecognizer to shot-logging  
✅ **Empty state**: Centered message with icon  

## Next Steps

1. Create `data-model.md` documenting page state structure
2. Create `contracts/` with component interface definitions
3. Create `quickstart.md` with implementation guide
