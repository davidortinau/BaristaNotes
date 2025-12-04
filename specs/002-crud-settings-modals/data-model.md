# Data Model: CRUD Settings with Modal Bottom Sheets

**Feature**: 002-crud-settings-modals  
**Date**: December 2, 2025

## Overview

This feature does not introduce new entities. It leverages existing data models and DTOs for Equipment, Bean, and UserProfile. This document describes how existing models are used in the CRUD forms.

---

## Existing Entities (No Changes Required)

### Equipment

**Purpose**: Represents espresso-making equipment (machines, grinders, accessories)

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| Id | int | Auto | Primary key |
| Name | string | Yes | Max 100 chars |
| Type | EquipmentType (enum) | Yes | Machine, Grinder, Accessory |
| Notes | string? | No | Max 500 chars |
| IsActive | bool | Yes | Default true |
| CreatedAt | DateTimeOffset | Auto | Set on create |

**Relationships**:
- One-to-many with ShotRecord (as Machine, Grinder, or Accessory)

**State Transitions**:
- Active → Archived (soft delete when referenced by shots)
- Active → Deleted (hard delete when no references)

---

### Bean

**Purpose**: Represents coffee beans used for espresso shots

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| Id | int | Auto | Primary key |
| Name | string | Yes | Max 100 chars |
| Roaster | string? | No | Max 100 chars |
| Origin | string? | No | Max 100 chars |
| RoastDate | DateTimeOffset? | No | Must be ≤ today |
| Notes | string? | No | Max 500 chars |
| IsActive | bool | Yes | Default true |
| CreatedAt | DateTimeOffset | Auto | Set on create |

**Relationships**:
- One-to-many with ShotRecord

**State Transitions**:
- Active → Archived (soft delete when referenced by shots)
- Active → Deleted (hard delete when no references)

---

### UserProfile

**Purpose**: Represents individual users for multi-user shot tracking

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| Id | int | Auto | Primary key |
| Name | string | Yes | Max 50 chars |
| AvatarPath | string? | No | File path (future) |
| CreatedAt | DateTimeOffset | Auto | Set on create |

**Relationships**:
- One-to-many with ShotRecord (as MadeBy or MadeFor)

**State Transitions**:
- Cannot delete last profile (validation rule)
- Delete removes profile if not referenced by shots

---

## DTOs Used in CRUD Forms

### Create DTOs

```text
CreateEquipmentDto
├── Name: string (required)
├── Type: EquipmentType (required)
└── Notes: string? (optional)

CreateBeanDto
├── Name: string (required)
├── Roaster: string? (optional)
├── Origin: string? (optional)
├── RoastDate: DateTimeOffset? (optional)
└── Notes: string? (optional)

CreateUserProfileDto
├── Name: string (required)
└── AvatarPath: string? (optional, future use)
```

### Update DTOs

```text
UpdateEquipmentDto
├── Name: string? (optional update)
├── Type: EquipmentType? (optional update)
├── Notes: string? (optional update)
└── IsActive: bool? (optional update)

UpdateBeanDto
├── Name: string? (optional update)
├── Roaster: string? (optional update)
├── Origin: string? (optional update)
├── RoastDate: DateTimeOffset? (optional update)
├── Notes: string? (optional update)
└── IsActive: bool? (optional update)

UpdateUserProfileDto
├── Name: string? (optional update)
└── AvatarPath: string? (optional update)
```

### Read DTOs

```text
EquipmentDto (returned from service)
├── Id: int
├── Name: string
├── Type: EquipmentType
├── Notes: string?
├── IsActive: bool
└── CreatedAt: DateTimeOffset

BeanDto (returned from service)
├── Id: int
├── Name: string
├── Roaster: string?
├── Origin: string?
├── RoastDate: DateTimeOffset?
├── Notes: string?
├── IsActive: bool
└── CreatedAt: DateTimeOffset

UserProfileDto (returned from service)
├── Id: int
├── Name: string
├── AvatarPath: string?
└── CreatedAt: DateTimeOffset
```

---

## Form State Models (New - UI Layer Only)

These state classes manage form data within MauiReactor components:

### EquipmentFormState

```text
EquipmentFormState
├── Id: int? (null for create, set for edit)
├── Name: string
├── Type: EquipmentType
├── Notes: string
├── IsSaving: bool
├── ValidationErrors: Dictionary<string, string>
└── HasChanges: bool
```

### BeanFormState

```text
BeanFormState
├── Id: int? (null for create, set for edit)
├── Name: string
├── Roaster: string
├── Origin: string
├── RoastDate: DateTimeOffset?
├── Notes: string
├── IsSaving: bool
├── ValidationErrors: Dictionary<string, string>
└── HasChanges: bool
```

### UserProfileFormState

```text
UserProfileFormState
├── Id: int? (null for create, set for edit)
├── Name: string
├── IsSaving: bool
├── ValidationErrors: Dictionary<string, string>
└── HasChanges: bool
```

---

## Validation Rules Summary

| Entity | Field | Rule | Error Message |
|--------|-------|------|---------------|
| Equipment | Name | Required | "Equipment name is required" |
| Equipment | Name | Max 100 chars | "Name must be 100 characters or less" |
| Equipment | Type | Required | "Equipment type is required" |
| Bean | Name | Required | "Bean name is required" |
| Bean | Name | Max 100 chars | "Name must be 100 characters or less" |
| Bean | RoastDate | ≤ Today | "Roast date cannot be in the future" |
| Profile | Name | Required | "Profile name is required" |
| Profile | Name | Max 50 chars | "Name must be 50 characters or less" |

---

## Business Rules

### Equipment Delete Rules
1. If equipment has associated shot records → Archive (set IsActive = false)
2. If equipment has no shot records → Hard delete

### Bean Delete Rules
1. If bean has associated shot records → Archive (set IsActive = false)
2. If bean has no shot records → Hard delete

### UserProfile Delete Rules
1. If profile is the only profile → Prevent delete (show error)
2. If profile has associated shot records → Prevent delete (show error with option to reassign)
3. If profile has no shot records and not last → Hard delete
