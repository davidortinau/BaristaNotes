# Data Model: Bean Detail Page

**Feature**: 004-bean-detail-page  
**Date**: December 5, 2025

## Overview

This feature does not introduce new database entities. It leverages existing entities (Bean, ShotRecord) and their DTOs. This document defines the UI state models for the new BeanDetailPage component.

---

## Existing Entities (No Changes Required)

### Bean Entity
Already defined in `BaristaNotes.Core/Models/Bean.cs`:
- Id, Name, Roaster, RoastDate, Origin, Notes, IsActive, IsDeleted, CreatedAt, LastModifiedAt

### ShotRecord Entity
Already defined in `BaristaNotes.Core/Models/ShotRecord.cs`:
- Linked to Bean via `BeanId` foreign key
- Contains: Timestamp, DoseIn, GrindSetting, ExpectedTime, ExpectedOutput, ActualTime, ActualOutput, Rating, DrinkType

---

## Existing DTOs (No Changes Required)

### BeanDto
```text
BeanDto
├── Id: int
├── Name: string
├── Roaster: string?
├── RoastDate: DateTimeOffset?
├── Origin: string?
├── Notes: string?
├── IsActive: bool
└── CreatedAt: DateTimeOffset
```

### CreateBeanDto
```text
CreateBeanDto
├── Name: string (required)
├── Roaster: string?
├── RoastDate: DateTimeOffset?
├── Origin: string?
└── Notes: string?
```

### UpdateBeanDto
```text
UpdateBeanDto
├── Name: string?
├── Roaster: string?
├── RoastDate: DateTimeOffset?
├── Origin: string?
├── Notes: string?
└── IsActive: bool?
```

### ShotRecordDto
```text
ShotRecordDto
├── Id: int
├── Timestamp: DateTimeOffset
├── Bean: BeanDto?
├── Machine: EquipmentDto?
├── Grinder: EquipmentDto?
├── Accessories: List<EquipmentDto>
├── MadeBy: UserProfileDto?
├── MadeFor: UserProfileDto?
├── DoseIn: decimal
├── GrindSetting: string
├── ExpectedTime: decimal
├── ExpectedOutput: decimal
├── DrinkType: string
├── ActualTime: decimal?
├── ActualOutput: decimal?
├── PreinfusionTime: decimal?
└── Rating: int?
```

### PagedResult<T>
```text
PagedResult<T>
├── Items: List<T>
├── TotalCount: int
├── PageIndex: int
├── PageSize: int
├── TotalPages: int (computed)
├── HasPreviousPage: bool (computed)
└── HasNextPage: bool (computed)
```

---

## New UI State Models (Component Layer Only)

### BeanDetailPageProps

Props passed during Shell navigation:

```text
BeanDetailPageProps
└── BeanId: int? (null for create, set for edit/view)
```

### BeanDetailPageState

UI state managed by the BeanDetailPage component:

```text
BeanDetailPageState
├── Form Fields
│   ├── BeanId: int? (null for new, set for existing)
│   ├── Name: string
│   ├── Roaster: string
│   ├── Origin: string
│   ├── TrackRoastDate: bool (toggle for date picker visibility)
│   ├── RoastDate: DateTime
│   └── Notes: string
├── Form State
│   ├── IsSaving: bool
│   ├── IsLoading: bool
│   └── ErrorMessage: string?
└── Shot History
    ├── Shots: List<ShotRecordDto>
    ├── IsLoadingShots: bool
    ├── HasMoreShots: bool
    ├── ShotPageIndex: int
    └── ShotLoadError: string?
```

---

## State Transitions

### Initial Load (Edit Mode)
```
Start → IsLoading=true → LoadBeanAsync() → 
  Success: IsLoading=false, populate form fields
  Failure: IsLoading=false, ErrorMessage=error
  Then: LoadShotsAsync() → IsLoadingShots=true →
    Success: Shots=items, HasMoreShots=hasNext, IsLoadingShots=false
    Failure: ShotLoadError=error, IsLoadingShots=false
```

### Initial Load (Create Mode)
```
Start → Form fields empty → No shot history section shown
```

### Save Flow
```
User clicks Save → ValidateForm() →
  Invalid: ErrorMessage=validation error
  Valid: IsSaving=true → CreateOrUpdate service call →
    Success: ShowSuccessAsync() → GoToAsync("..")
    Failure: IsSaving=false, ErrorMessage=error
```

### Delete Flow
```
User clicks Delete → DisplayAlert confirmation →
  Cancel: No change
  Confirm: DeleteBeanAsync() →
    Success: ShowSuccessAsync() → GoToAsync("..")
    Failure: ErrorMessage=error
```

### Shot Pagination
```
Scroll reaches threshold → HasMoreShots=true? →
  No: Do nothing
  Yes: IsLoadingShots=true → LoadMoreShotsAsync() →
    Success: Shots.AddRange(items), ShotPageIndex++, HasMoreShots=hasNext
    Failure: ShotLoadError=error, IsLoadingShots=false
```

---

## Validation Rules

| Field | Rule | Error Message |
|-------|------|---------------|
| Name | Required (non-empty) | "Bean name is required" |
| RoastDate | ≤ Today when TrackRoastDate=true | "Roast date cannot be in the future" |

---

## Service Dependencies

| Service | Method | Usage |
|---------|--------|-------|
| IBeanService | GetBeanByIdAsync(id) | Load existing bean for edit |
| IBeanService | CreateBeanAsync(dto) | Create new bean |
| IBeanService | UpdateBeanAsync(id, dto) | Update existing bean |
| IBeanService | DeleteBeanAsync(id) | Delete bean |
| IShotService | GetShotHistoryByBeanAsync(beanId, pageIndex, pageSize) | Load paginated shot history |
| IFeedbackService | ShowSuccessAsync(message) | Success notifications |
| IFeedbackService | ShowErrorAsync(message) | Error notifications |
