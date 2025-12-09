# Research: Inline Bean Creation During Shot Logging

**Feature**: 001-inline-bean-creation  
**Created**: 2025-12-09  
**Status**: Complete

## Research Questions

### 1. UXDivers.Popups.Maui FormPopup Usage

**Decision**: Use `FormPopup` from UXDivers.Popups.Maui for bean and bag creation forms.

**Rationale**: 
- Already installed in project (version 0.9.0)
- Consistent with existing popup patterns (SimpleActionPopup, ListActionPopup, Toast)
- Provides built-in form field support with consistent styling
- Handles modal overlay, animations, and dismissal automatically

**Alternatives considered**:
- Custom popup component: Rejected - would duplicate UXDivers functionality
- Full-page navigation: Rejected - spec requires modal overlay without navigation
- Bottom sheet (Plugin.Maui.BottomSheet): Rejected - would add unnecessary dependency when UXDivers already available

**Implementation pattern** (from existing codebase):
```csharp
// Push popup
await IPopupService.Current.PushAsync(popup);

// Dismiss from within popup
await IPopupService.Current.PopAsync();
```

### 2. Empty State Detection

**Decision**: Detect empty state by checking both `AvailableBags.Count == 0` AND active beans count.

**Rationale**:
- Current ShotLoggingPage already loads bags via `_bagService.GetActiveBagsForShotLoggingAsync()`
- Need additional check for beans to differentiate "no beans at all" vs "beans exist but no active bags"
- Use existing `IBeanService.GetAllActiveBeansAsync()` (already injected in ShotLoggingPage)

**Implementation approach**:
```csharp
// In ShotLoggingState - add beans tracking
public List<BeanDto> AvailableBeans { get; set; } = new();

// In LoadDataAsync - load beans alongside bags
var beans = await _beanService.GetAllActiveBeansAsync();
SetState(s => {
    s.AvailableBeans = beans;
    s.AvailableBags = bags;
});

// Empty state logic:
// - No beans: Show "Create Bean" CTA
// - Beans but no bags: Show "Add New Bag" with bean picker (existing pattern, enhanced)
// - Bags exist: Show normal bag picker
```

### 3. Flow Chaining (Bean → Bag)

**Decision**: Chain bean creation popup dismissal to bag creation popup display.

**Rationale**:
- Spec requires automatic bag prompt after bean creation
- UXDivers PopAsync returns control, allowing immediate PushAsync of next popup
- Pass newly created BeanId to bag creation popup

**Implementation pattern**:
```csharp
// In BeanCreationPopup OnSave:
var result = await _beanService.CreateBeanAsync(createDto);
if (result.Success)
{
    await IPopupService.Current.PopAsync();
    OnBeanCreated?.Invoke(result.Data);  // Callback to parent
}

// In ShotLoggingPage handler:
private async void HandleBeanCreated(BeanDto newBean)
{
    // Immediately show bag creation popup
    var bagPopup = new BagCreationPopup { BeanId = newBean.Id, BeanName = newBean.Name };
    bagPopup.OnBagCreated = HandleBagCreated;
    await IPopupService.Current.PushAsync(bagPopup);
}
```

### 4. Auto-Selection After Creation

**Decision**: Store created bag ID in state and refresh bag list, then auto-select.

**Rationale**:
- State refresh is necessary to show new bag in picker
- Can set `SelectedBagId` and `SelectedBagIndex` in same SetState call

**Implementation pattern**:
```csharp
private async void HandleBagCreated(BagSummaryDto newBag)
{
    await LoadDataAsync();  // Refresh bag list
    SetState(s => {
        s.SelectedBagId = newBag.Id;
        s.SelectedBagIndex = s.AvailableBags.FindIndex(b => b.Id == newBag.Id);
    });
    // Bag is now auto-selected, user can proceed with shot logging
}
```

### 5. Existing Service Capabilities

**Decision**: No new service methods needed - existing APIs are sufficient.

**BeanService (existing)**:
- `CreateBeanAsync(CreateBeanDto dto)` → `OperationResult<BeanDto>` ✓
- Returns structured error handling via OperationResult

**BagService (existing)**:
- `CreateBagAsync(Bag bag)` → `OperationResult<Bag>` ✓
- Validates roast date not in future
- Validates bean exists

**Required DTO fields for bean creation**:
- `Name` (required, max 100 chars)
- `Roaster` (optional, max 100 chars)
- `Origin` (optional, max 100 chars)
- `Notes` (optional, max 500 chars)

**Required fields for bag creation**:
- `BeanId` (required)
- `RoastDate` (required, not in future)
- `Notes` (optional, max 500 chars)

### 6. FormPopup Field Configuration

**Decision**: Use UXDivers.Popups.Maui FormPopup with custom form content.

**Research findings** (from UXDivers.Popups documentation):
- FormPopup provides container with title, scrollable content area, and action buttons
- Form fields should use standard MAUI Entry, DatePicker, Editor controls
- Can customize via `ContentTemplate` property

**Bean creation form fields**:
1. Name (Entry, required)
2. Roaster (Entry, optional)
3. Origin (Entry, optional)
4. Notes (Editor, optional)

**Bag creation form fields**:
1. BeanId (hidden, passed via props)
2. RoastDate (DatePicker, required, max=today)
3. Notes (Editor, optional)

## Technical Decisions Summary

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Popup library | UXDivers.Popups.Maui FormPopup | Already in project, consistent styling |
| Empty state detection | Check beans AND bags count | Two different user states |
| Flow chaining | Callback + immediate push | Seamless UX |
| Auto-selection | SetState after LoadDataAsync | Consistent with existing patterns |
| New service methods | None needed | Existing APIs sufficient |
| Form validation | Service-level (existing) | Leverage existing ValidationException |

## Dependencies Verified

- [x] UXDivers.Popups.Maui 0.9.0 installed
- [x] IBeanService.CreateBeanAsync exists and returns OperationResult
- [x] IBagService.CreateBagAsync exists and returns OperationResult
- [x] ThemeKeys and styling infrastructure exists
