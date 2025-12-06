# Quickstart: Bean Detail Page

**Feature**: 004-bean-detail-page  
**Date**: December 5, 2025

## Overview

This guide provides step-by-step implementation instructions for replacing the bean bottom sheet form with a dedicated BeanDetailPage that includes shot history.

---

## Prerequisites

Before starting implementation:

1. ✅ Existing services work correctly:
   - `IBeanService.GetBeanByIdAsync()`
   - `IBeanService.CreateBeanAsync()`
   - `IBeanService.UpdateBeanAsync()`
   - `IBeanService.DeleteBeanAsync()`
   - `IShotService.GetShotHistoryByBeanAsync()`

2. ✅ Existing components available:
   - `ShotRecordCard` component
   - `ThemeKeys` styling constants
   - `IFeedbackService` for notifications

3. ✅ Reference implementation:
   - `ProfileFormPage.cs` as pattern template

---

## Implementation Order

### Phase 1: Create BeanDetailPage
1. Create `BeanDetailPageProps` and `BeanDetailPageState` classes
2. Create `BeanDetailPage` component skeleton
3. Implement form rendering (copy from BeanFormSheet, adapt to full page)
4. Implement save/validation logic
5. Implement delete with confirmation

### Phase 2: Add Shot History
1. Add shot history state properties
2. Implement `LoadShotsAsync()` method
3. Implement `LoadMoreShotsAsync()` for pagination
4. Render shot history section with CollectionView
5. Add tap gesture for shot navigation

### Phase 3: Update BeanManagementPage
1. Remove bottom sheet methods and imports
2. Add navigation methods
3. Update toolbar and list item actions
4. Remove inline delete (moved to detail page)

### Phase 4: Route Registration
1. Register "bean-detail" route in MauiProgram.cs

---

## Code Patterns

### 1. BeanDetailPage Structure

```csharp
// File: BaristaNotes/Pages/BeanDetailPage.cs
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using BaristaNotes.Components;
using MauiReactor;

namespace BaristaNotes.Pages;

class BeanDetailPageProps
{
    public int? BeanId { get; set; }
}

class BeanDetailPageState
{
    // Form fields
    public int? BeanId { get; set; }
    public string Name { get; set; } = "";
    public string Roaster { get; set; } = "";
    public string Origin { get; set; } = "";
    public bool TrackRoastDate { get; set; }
    public DateTime RoastDate { get; set; } = DateTime.Now;
    public string Notes { get; set; } = "";
    
    // Form state
    public bool IsSaving { get; set; }
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }
    
    // Shot history
    public List<ShotRecordDto> Shots { get; set; } = new();
    public bool IsLoadingShots { get; set; }
    public bool HasMoreShots { get; set; }
    public int ShotPageIndex { get; set; }
    public string? ShotLoadError { get; set; }
}

partial class BeanDetailPage : Component<BeanDetailPageState, BeanDetailPageProps>
{
    [Inject] IBeanService _beanService;
    [Inject] IShotService _shotService;
    [Inject] IFeedbackService _feedbackService;
    
    const int PageSize = 20;
    
    protected override void OnMounted()
    {
        base.OnMounted();
        
        if (Props.BeanId.HasValue && Props.BeanId.Value > 0)
        {
            SetState(s => 
            {
                s.BeanId = Props.BeanId;
                s.IsLoading = true;
            });
            _ = LoadBeanAsync();
        }
    }
    
    // ... implementation continues
}
```

### 2. Load Bean Data

```csharp
async Task LoadBeanAsync()
{
    if (!State.BeanId.HasValue || State.BeanId.Value <= 0) return;
    
    try
    {
        var bean = await _beanService.GetBeanByIdAsync(State.BeanId.Value);
        
        if (bean == null)
        {
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = "Bean not found";
            });
            return;
        }
        
        SetState(s =>
        {
            s.Name = bean.Name;
            s.Roaster = bean.Roaster ?? "";
            s.Origin = bean.Origin ?? "";
            s.TrackRoastDate = bean.RoastDate.HasValue;
            s.RoastDate = bean.RoastDate?.DateTime ?? DateTime.Now;
            s.Notes = bean.Notes ?? "";
            s.IsLoading = false;
        });
        
        // Load shot history after bean loads
        _ = LoadShotsAsync();
    }
    catch (Exception ex)
    {
        SetState(s =>
        {
            s.IsLoading = false;
            s.ErrorMessage = $"Failed to load bean: {ex.Message}";
        });
    }
}
```

### 3. Load Shot History with Pagination

```csharp
async Task LoadShotsAsync()
{
    if (!State.BeanId.HasValue || State.IsLoadingShots) return;
    
    SetState(s =>
    {
        s.IsLoadingShots = true;
        s.ShotLoadError = null;
    });
    
    try
    {
        var result = await _shotService.GetShotHistoryByBeanAsync(
            State.BeanId.Value,
            0, // First page
            PageSize);
        
        SetState(s =>
        {
            s.Shots = result.Items.ToList();
            s.HasMoreShots = result.HasNextPage;
            s.ShotPageIndex = 1;
            s.IsLoadingShots = false;
        });
    }
    catch (Exception ex)
    {
        SetState(s =>
        {
            s.IsLoadingShots = false;
            s.ShotLoadError = $"Failed to load shots: {ex.Message}";
        });
    }
}

async Task LoadMoreShotsAsync()
{
    if (!State.BeanId.HasValue || State.IsLoadingShots || !State.HasMoreShots) return;
    
    SetState(s => s.IsLoadingShots = true);
    
    try
    {
        var result = await _shotService.GetShotHistoryByBeanAsync(
            State.BeanId.Value,
            State.ShotPageIndex,
            PageSize);
        
        SetState(s =>
        {
            s.Shots = new List<ShotRecordDto>(s.Shots.Concat(result.Items));
            s.HasMoreShots = result.HasNextPage;
            s.ShotPageIndex++;
            s.IsLoadingShots = false;
        });
    }
    catch (Exception ex)
    {
        SetState(s =>
        {
            s.IsLoadingShots = false;
            s.ShotLoadError = $"Failed to load more shots: {ex.Message}";
        });
    }
}
```

### 4. Form Validation

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

### 5. Save Bean

```csharp
async Task SaveBeanAsync()
{
    if (!ValidateForm()) return;
    
    SetState(s =>
    {
        s.IsSaving = true;
        s.ErrorMessage = null;
    });
    
    try
    {
        if (State.BeanId.HasValue && State.BeanId.Value > 0)
        {
            // Update existing
            var updateDto = new UpdateBeanDto
            {
                Name = State.Name,
                Roaster = string.IsNullOrWhiteSpace(State.Roaster) ? null : State.Roaster,
                Origin = string.IsNullOrWhiteSpace(State.Origin) ? null : State.Origin,
                RoastDate = State.TrackRoastDate ? new DateTimeOffset(State.RoastDate) : null,
                Notes = string.IsNullOrWhiteSpace(State.Notes) ? null : State.Notes
            };
            
            await _beanService.UpdateBeanAsync(State.BeanId.Value, updateDto);
            await _feedbackService.ShowSuccessAsync($"Bean '{State.Name}' updated");
        }
        else
        {
            // Create new
            var createDto = new CreateBeanDto
            {
                Name = State.Name,
                Roaster = string.IsNullOrWhiteSpace(State.Roaster) ? null : State.Roaster,
                Origin = string.IsNullOrWhiteSpace(State.Origin) ? null : State.Origin,
                RoastDate = State.TrackRoastDate ? new DateTimeOffset(State.RoastDate) : null,
                Notes = string.IsNullOrWhiteSpace(State.Notes) ? null : State.Notes
            };
            
            var result = await _beanService.CreateBeanAsync(createDto);
            if (!result.Success)
            {
                SetState(s =>
                {
                    s.IsSaving = false;
                    s.ErrorMessage = result.ErrorMessage ?? "Failed to create bean";
                });
                return;
            }
            
            await _feedbackService.ShowSuccessAsync($"Bean '{State.Name}' created");
        }
        
        await Shell.Current.GoToAsync("..");
    }
    catch (Exception ex)
    {
        SetState(s =>
        {
            s.IsSaving = false;
            s.ErrorMessage = $"Failed to save: {ex.Message}";
        });
    }
}
```

### 6. Delete Bean

```csharp
async Task DeleteBeanAsync()
{
    if (!State.BeanId.HasValue || State.BeanId.Value <= 0) return;
    
    if (Application.Current?.MainPage == null) return;
    
    var confirmed = await Application.Current.MainPage.DisplayAlert(
        "Delete Bean",
        $"Are you sure you want to delete '{State.Name}'? This action cannot be undone.",
        "Delete",
        "Cancel");
    
    if (!confirmed) return;
    
    try
    {
        await _beanService.DeleteBeanAsync(State.BeanId.Value);
        await _feedbackService.ShowSuccessAsync($"Bean '{State.Name}' deleted");
        await Shell.Current.GoToAsync("..");
    }
    catch (Exception ex)
    {
        SetState(s => s.ErrorMessage = $"Failed to delete: {ex.Message}");
    }
}
```

### 7. Render Method

```csharp
public override VisualNode Render()
{
    var isEditMode = State.BeanId.HasValue && State.BeanId.Value > 0;
    var title = isEditMode
        ? (string.IsNullOrEmpty(State.Name) ? "Edit Bean" : $"Edit {State.Name}")
        : "Add Bean";
    
    if (State.IsLoading)
    {
        return ContentPage(
            VStack(
                ActivityIndicator().IsRunning(true)
            )
            .VCenter()
            .HCenter()
        ).Title(title);
    }
    
    return ContentPage(
        ScrollView(
            VStack(spacing: 16,
                // Form section
                RenderForm(),
                
                // Shot history section (edit mode only)
                isEditMode ? RenderShotHistory() : null
            )
            .Padding(16)
        )
    ).Title(title);
}

VisualNode RenderForm()
{
    var isEditMode = State.BeanId.HasValue && State.BeanId.Value > 0;
    
    return VStack(spacing: 16,
        // Name field
        Label("Name *").ThemeKey(ThemeKeys.SecondaryText),
        Entry()
            .Placeholder("Bean name (required)")
            .Text(State.Name)
            .OnTextChanged(text => SetState(s => s.Name = text)),
        
        // Roaster field
        Label("Roaster").ThemeKey(ThemeKeys.SecondaryText),
        Entry()
            .Placeholder("Roaster name")
            .Text(State.Roaster)
            .OnTextChanged(text => SetState(s => s.Roaster = text)),
        
        // Origin field
        Label("Origin").ThemeKey(ThemeKeys.SecondaryText),
        Entry()
            .Placeholder("Country or region of origin")
            .Text(State.Origin)
            .OnTextChanged(text => SetState(s => s.Origin = text)),
        
        // Roast date toggle
        HStack(spacing: 8,
            Label("Track Roast Date").ThemeKey(ThemeKeys.SecondaryText).VCenter(),
            Switch()
                .IsToggled(State.TrackRoastDate)
                .OnToggled(args => SetState(s => s.TrackRoastDate = args.Value))
        ),
        
        // Date picker (conditional)
        State.TrackRoastDate
            ? DatePicker()
                .MaximumDate(DateTime.Now)
                .Date(State.RoastDate)
                .OnDateSelected(date => SetState(s => s.RoastDate = date ?? DateTime.Now))
            : null,
        
        // Notes field
        Label("Notes").ThemeKey(ThemeKeys.SecondaryText),
        Editor()
            .Placeholder("Tasting notes, processing method, etc.")
            .Text(State.Notes)
            .HeightRequest(100)
            .OnTextChanged(text => SetState(s => s.Notes = text)),
        
        // Error message
        State.ErrorMessage != null
            ? Border(
                Label(State.ErrorMessage).TextColor(Colors.Red).Padding(12)
            )
            .BackgroundColor(Colors.Red.WithAlpha(0.1f))
            .StrokeThickness(1)
            .Stroke(Colors.Red)
            : null,
        
        // Action buttons
        VStack(spacing: 12,
            Button(State.IsSaving ? "Saving..." : (isEditMode ? "Save Changes" : "Create Bean"))
                .OnClicked(async () => await SaveBeanAsync())
                .IsEnabled(!State.IsSaving),
            
            Button("Cancel")
                .OnClicked(async () => await Shell.Current.GoToAsync(".."))
                .IsEnabled(!State.IsSaving)
                .BackgroundColor(Colors.Gray),
            
            isEditMode
                ? Button("Delete Bean")
                    .OnClicked(async () => await DeleteBeanAsync())
                    .IsEnabled(!State.IsSaving)
                    .BackgroundColor(Colors.Red)
                : null
        )
    );
}
```

### 8. Shot History Section

```csharp
VisualNode RenderShotHistory()
{
    return VStack(spacing: 12,
        // Section header
        BoxView().HeightRequest(1).Color(Colors.Gray.WithAlpha(0.3f)),
        
        Label("Shot History")
            .ThemeKey(ThemeKeys.SubHeadline),
        
        // Error state
        State.ShotLoadError != null
            ? VStack(spacing: 8,
                Label(State.ShotLoadError).TextColor(Colors.Red),
                Button("Retry").OnClicked(async () => await LoadShotsAsync())
            )
            : null,
        
        // Loading state (initial)
        State.IsLoadingShots && State.Shots.Count == 0
            ? ActivityIndicator().IsRunning(true)
            : null,
        
        // Empty state
        !State.IsLoadingShots && State.Shots.Count == 0 && State.ShotLoadError == null
            ? RenderEmptyShots()
            : null,
        
        // Shot list
        State.Shots.Count > 0
            ? CollectionView()
                .ItemsSource(State.Shots, RenderShotItem)
                .RemainingItemsThreshold(5)
                .RemainingItemsThresholdReached(async () => await LoadMoreShotsAsync())
                .HeightRequest(400) // Constrain height within ScrollView
            : null,
        
        // Loading more indicator
        State.IsLoadingShots && State.Shots.Count > 0
            ? ActivityIndicator().IsRunning(true).HCenter()
            : null
    );
}

VisualNode RenderEmptyShots()
{
    return VStack(spacing: 12,
        Label("📋").FontSize(48).HCenter(),
        Label("No shots recorded with this bean yet")
            .ThemeKey(ThemeKeys.SecondaryText)
            .HCenter()
    )
    .Padding(24);
}

VisualNode RenderShotItem(ShotRecordDto shot)
{
    return Border(
        new ShotRecordCard().Shot(shot)
    )
    .GestureRecognizers(
        TapGestureRecognizer()
            .OnTapped(async () => await NavigateToShotAsync(shot.Id))
    )
    .Margin(0, 4);
}

async Task NavigateToShotAsync(int shotId)
{
    await Shell.Current.GoToAsync<ShotLoggingPageProps>("shot-logging", props =>
    {
        props.ShotId = shotId;
    });
}
```

### 9. Route Registration

```csharp
// File: BaristaNotes/MauiProgram.cs
// Add after existing route registrations (around line 197)
MauiReactor.Routing.RegisterRoute<Pages.BeanDetailPage>("bean-detail");
```

### 10. Update BeanManagementPage

```csharp
// Remove these methods:
// - ShowAddBeanSheet()
// - ShowEditBeanSheet(BeanDto)
// - OnBeanSaved(BeanDto)

// Add these methods:
async Task NavigateToAddBean()
{
    await Shell.Current.GoToAsync("bean-detail");
}

async Task NavigateToEditBean(BeanDto bean)
{
    await Shell.Current.GoToAsync<BeanDetailPageProps>("bean-detail", props =>
    {
        props.BeanId = bean.Id;
    });
}

// Update toolbar item OnClicked:
ToolbarItem("+ Add")
    .OnClicked(async () => await NavigateToAddBean())

// Update RenderBeanItem to navigate on tap:
// Replace entire card with tappable version
Border(...)
    .GestureRecognizers(
        TapGestureRecognizer()
            .OnTapped(async () => await NavigateToEditBean(bean))
    )

// Remove edit/delete buttons from list item (handled on detail page)
```

---

## Testing Checklist

- [ ] Create new bean from empty form
- [ ] Edit existing bean
- [ ] Validate name required
- [ ] Validate roast date not in future
- [ ] Delete bean with confirmation
- [ ] View shot history for bean with shots
- [ ] Verify empty state for bean without shots
- [ ] Test pagination (bean with >20 shots)
- [ ] Navigate to shot from history
- [ ] Cancel returns to list without saving
- [ ] Back navigation works correctly

---

## Files to Create/Modify

| File | Action | Description |
|------|--------|-------------|
| `Pages/BeanDetailPage.cs` | CREATE | New full-page component |
| `Pages/BeanManagementPage.cs` | MODIFY | Replace bottom sheet with navigation |
| `MauiProgram.cs` | MODIFY | Register "bean-detail" route |
| `Components/BottomSheet/BeanFormSheet.cs` | DELETE | No longer needed (optional - can keep for reference) |
