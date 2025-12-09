# Quickstart: Inline Bean Creation During Shot Logging

**Feature**: 001-inline-bean-creation  
**Created**: 2025-12-09

## Overview

This feature enables users to create beans and bags directly from the shot logging page via modal popups, eliminating the "cold start" problem for new users.

## User Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│                        ShotLoggingPage                              │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  IF no beans exist:                                                 │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │  [Coffee Icon] No beans configured                          │   │
│  │  "Create your first bean to start logging shots"            │   │
│  │                                                              │   │
│  │  [Create Bean]  ─────────────────┐                          │   │
│  └──────────────────────────────────│──────────────────────────┘   │
│                                     │                               │
│                                     ▼                               │
│                        ┌────────────────────────┐                   │
│                        │  BeanCreationPopup     │                   │
│                        │  ───────────────────   │                   │
│                        │  Name: [__________]    │                   │
│                        │  Roaster: [________]   │                   │
│                        │  Origin: [_________]   │                   │
│                        │  Notes: [__________]   │                   │
│                        │                        │                   │
│                        │  [Cancel] [Create]     │                   │
│                        └──────────│─────────────┘                   │
│                                   │ on success                      │
│                                   ▼                                 │
│                        ┌────────────────────────┐                   │
│                        │  BagCreationPopup      │                   │
│                        │  ───────────────────   │                   │
│                        │  Bean: [Bean Name]     │                   │
│                        │  Roast Date: [📅]      │                   │
│                        │  Notes: [__________]   │                   │
│                        │                        │                   │
│                        │  [Cancel] [Create]     │                   │
│                        └──────────│─────────────┘                   │
│                                   │ on success                      │
│                                   ▼                                 │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │  Normal shot logging form (bag auto-selected)               │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                                                                     │
│  ELSE IF beans exist but no active bags:                           │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │  Existing "No active bags" state + [Add New Bag] button     │   │
│  │  (Bag popup will show bean picker since beans exist)        │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## Key Components

### 1. ShotLoggingPage (Modified)

**New state fields**:
```csharp
public List<BeanDto> AvailableBeans { get; set; } = new();
```

**New empty state detection**:
- No beans + No bags → Show "Create Bean" prompt
- Beans exist + No bags → Show existing "Add New Bag" button (enhanced)
- Bags exist → Show normal bag picker

### 2. BeanCreationPopup (New)

**Purpose**: Modal form for creating a new bean  
**Type**: UXDivers.Popups.Maui FormPopup  
**Fields**:
- Name (required)
- Roaster (optional)
- Origin (optional)
- Notes (optional)

**Callback**: `Action<BeanDto> OnBeanCreated`

### 3. BagCreationPopup (New)

**Purpose**: Modal form for creating a new bag  
**Type**: UXDivers.Popups.Maui FormPopup  
**Props**:
- `int BeanId` (required)
- `string BeanName` (for display)

**Fields**:
- Roast Date (required, max today)
- Notes (optional)

**Callback**: `Action<BagSummaryDto> OnBagCreated`

## Service Integration

All existing services are reused:

```csharp
// Bean creation (existing)
IBeanService.CreateBeanAsync(CreateBeanDto) → OperationResult<BeanDto>

// Bag creation (existing)  
IBagService.CreateBagAsync(Bag) → OperationResult<Bag>

// Popups (existing)
IPopupService.Current.PushAsync(popup)  // Show popup
IPopupService.Current.PopAsync()        // Dismiss popup
```

## Testing Scenarios

### Unit Tests
- Bean validation (name required, field lengths)
- Bag validation (roast date not in future)

### Integration Tests
- Empty state detection with no beans
- Bean creation popup appears on button tap
- Bag creation popup chains after bean creation
- Bag auto-selected after creation flow

### UI Tests (Manual)
- Modal overlays correctly on shot logging page
- Cancel dismisses without creating
- Keyboard navigable
- Loading states during save

## Implementation Order

1. **T001**: Add `AvailableBeans` state and load beans in `LoadDataAsync`
2. **T002**: Create empty state UI when no beans exist
3. **T003**: Implement `BeanCreationPopup` component
4. **T004**: Implement `BagCreationPopup` component  
5. **T005**: Wire up flow chaining (bean → bag)
6. **T006**: Implement auto-selection after creation
7. **T007**: Write integration tests
