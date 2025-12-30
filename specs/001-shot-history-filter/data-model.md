# Data Model: Shot History Filter

**Feature**: 001-shot-history-filter  
**Date**: 2025-12-30

## Existing Entities (Reference Only)

These entities already exist and are used by the filter feature:

### ShotRecord
- **Id**: int (PK)
- **Timestamp**: DateTime
- **Rating**: int? (0-4 scale per constitution)
- **BagId**: int? (FK to Bag)
- **MadeById**: int? (FK to UserProfile)
- **MadeForId**: int? (FK to UserProfile)
- **IsDeleted**: bool (soft delete)

### Bean (via Bag)
- **Id**: int (PK)
- **Name**: string
- Shots linked via `ShotRecord.Bag.BeanId`

### UserProfile
- **Id**: int (PK)
- **Name**: string
- **AvatarPath**: string?
- Referenced by `ShotRecord.MadeForId`

## New Entities

### ShotFilterCriteria (Transient UI State)

Filter state model for the ActivityFeedPage. Not persisted to database.

| Field | Type | Description |
|-------|------|-------------|
| BeanIds | `List<int>` | Selected bean IDs to filter by |
| MadeForIds | `List<int>` | Selected person IDs to filter by |
| Ratings | `List<int>` | Selected rating values (0-4) to filter by |

**Validation Rules**:
- All lists default to empty (no filter applied)
- BeanIds must reference existing Bean.Id values
- MadeForIds must reference existing UserProfile.Id values
- Ratings must be in range 0-4

**State Transitions**:
```
[No Filters] --select--> [Filters Applied] --clear--> [No Filters]
                              |
                              +--modify--> [Filters Applied]
```

### ShotFilterCriteriaDto (Core Layer)

DTO for passing filter criteria to repository/service layer.

| Field | Type | Description |
|-------|------|-------------|
| BeanIds | `IReadOnlyList<int>?` | Bean IDs to filter (null = no filter) |
| MadeForIds | `IReadOnlyList<int>?` | Person IDs to filter (null = no filter) |
| Ratings | `IReadOnlyList<int>?` | Rating values to filter (null = no filter) |

**Design Notes**:
- Immutable record type for thread safety
- Nullable lists distinguish "no filter" from "empty selection"
- Passed to repository's `GetFilteredAsync` method

## Entity Relationships

```
┌─────────────────────────────────────────────────────────────┐
│                     Filter Flow                              │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────────┐     ┌──────────────────────────┐     │
│  │ ShotFilterCriteria│────>│ ShotFilterCriteriaDto    │     │
│  │ (UI State)        │     │ (Service/Repository)     │     │
│  └──────────────────┘     └───────────┬──────────────┘     │
│                                       │                     │
│                                       ▼                     │
│  ┌──────────────────────────────────────────────────────┐  │
│  │                   ShotRecord Query                    │  │
│  │  WHERE BeanId IN (criteria.BeanIds)                  │  │
│  │    AND MadeForId IN (criteria.MadeForIds)            │  │
│  │    AND Rating IN (criteria.Ratings)                  │  │
│  │    AND IsDeleted = false                             │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## Database Impact

**No schema changes required.** All existing tables and columns support the filter queries:
- `ShotRecord.Rating` - existing nullable int column
- `ShotRecord.MadeForId` - existing FK to UserProfile
- `ShotRecord.BagId` → `Bag.BeanId` - existing relationships

**Indexes**: Existing indexes on FK columns should provide adequate performance. Monitor after implementation.
