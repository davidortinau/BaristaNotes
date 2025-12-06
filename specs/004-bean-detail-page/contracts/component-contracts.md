# Component Contracts: Bean Detail Page

**Feature**: 004-bean-detail-page  
**Date**: December 5, 2025

## Overview

This document defines the contracts for UI components involved in the Bean Detail Page feature.

---

## BeanDetailPage Component

**Purpose**: Full-page form for creating/editing beans with shot history display for existing beans

**Location**: `BaristaNotes/Pages/BeanDetailPage.cs`

### Props Contract

```csharp
class BeanDetailPageProps
{
    /// <summary>
    /// Bean ID for edit mode. Null/0 for create mode.
    /// </summary>
    public int? BeanId { get; set; }
}
```

### State Contract

```csharp
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
```

### Component Interface

```csharp
partial class BeanDetailPage : Component<BeanDetailPageState, BeanDetailPageProps>
{
    [Inject] IBeanService _beanService;
    [Inject] IShotService _shotService;
    [Inject] IFeedbackService _feedbackService;
    
    // Lifecycle
    protected override void OnMounted();
    
    // Data loading
    private async Task LoadBeanAsync();
    private async Task LoadShotsAsync();
    private async Task LoadMoreShotsAsync();
    
    // Form actions
    private bool ValidateForm();
    private async Task SaveBeanAsync();
    private async Task DeleteBeanAsync();
    
    // Navigation
    private async Task NavigateToShotAsync(int shotId);
    
    // Rendering
    public override VisualNode Render();
    private VisualNode RenderForm();
    private VisualNode RenderShotHistory();
    private VisualNode RenderEmptyShots();
    private VisualNode RenderShotItem(ShotRecordDto shot);
}
```

### Form Fields

| Field | Control | Required | Notes |
|-------|---------|----------|-------|
| Name | Entry | Yes | Placeholder: "Bean name (required)" |
| Roaster | Entry | No | Placeholder: "Roaster name" |
| Origin | Entry | No | Placeholder: "Country or region of origin" |
| TrackRoastDate | Switch | No | Toggles DatePicker visibility |
| RoastDate | DatePicker | No* | MaximumDate: DateTime.Now; *Only when TrackRoastDate=true |
| Notes | Editor | No | Placeholder: "Tasting notes, processing method, etc." |

### Action Buttons

| Button | Condition | Action |
|--------|-----------|--------|
| Save/Create | Always visible | SaveBeanAsync() |
| Cancel | Always visible | GoToAsync("..") |
| Delete | Edit mode only (BeanId > 0) | DeleteBeanAsync() with confirmation |

---

## Modified Component: BeanManagementPage

**Purpose**: List beans and provide navigation to BeanDetailPage (replacing bottom sheet)

**Location**: `BaristaNotes/Pages/BeanManagementPage.cs`

### Changes Required

1. **Remove** bottom sheet methods:
   - `ShowAddBeanSheet()`
   - `ShowEditBeanSheet(BeanDto)`
   - `OnBeanSaved(BeanDto)`

2. **Add** navigation methods:
   ```csharp
   private async Task NavigateToAddBean()
   {
       await Shell.Current.GoToAsync("bean-detail");
   }
   
   private async Task NavigateToEditBean(BeanDto bean)
   {
       await Shell.Current.GoToAsync<BeanDetailPageProps>("bean-detail", props =>
       {
           props.BeanId = bean.Id;
       });
   }
   ```

3. **Update** toolbar button to call `NavigateToAddBean()`

4. **Update** edit button in `RenderBeanItem()` to call `NavigateToEditBean(bean)`

5. **Remove** delete button from list item (delete moved to detail page)

---

## Route Registration

**Location**: `BaristaNotes/MauiProgram.cs`

```csharp
// Add after existing route registrations
MauiReactor.Routing.RegisterRoute<Pages.BeanDetailPage>("bean-detail");
```

---

## Reused Component: ShotRecordCard

**Purpose**: Display shot information in the shot history list

**Location**: `BaristaNotes/Components/ShotRecordCard.cs`

**No changes required** - existing component provides all needed functionality:
- Displays drink type, rating, bean name, recipe details, equipment, timestamp
- Accepts `ShotRecordDto` via `[Prop]`

### Usage in BeanDetailPage

```csharp
// In RenderShotItem
Border(
    new ShotRecordCard().Shot(shot)
)
.GestureRecognizers(
    TapGestureRecognizer()
        .OnTapped(async () => await NavigateToShotAsync(shot.Id))
)
.Margin(0, 4)
```

---

## Service Contracts (Existing - No Changes)

### IBeanService

```csharp
public interface IBeanService
{
    Task<List<BeanDto>> GetAllActiveBeansAsync();
    Task<BeanDto?> GetBeanByIdAsync(int id);
    Task<OperationResult<BeanDto>> CreateBeanAsync(CreateBeanDto dto);
    Task<BeanDto> UpdateBeanAsync(int id, UpdateBeanDto dto);
    Task ArchiveBeanAsync(int id);
    Task DeleteBeanAsync(int id);
}
```

### IShotService (relevant method)

```csharp
public interface IShotService
{
    // ... other methods
    Task<PagedResult<ShotRecordDto>> GetShotHistoryByBeanAsync(
        int beanId, 
        int pageIndex, 
        int pageSize);
}
```

### IFeedbackService

```csharp
public interface IFeedbackService
{
    Task ShowSuccessAsync(string message, int durationMs = 2000);
    Task ShowErrorAsync(string message, string? recoveryAction = null, int durationMs = 5000);
    // ... other methods
}
```
