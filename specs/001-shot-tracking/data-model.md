# Data Model: Shot Maker, Recipient, and Preinfusion Tracking

**Feature**: 001-shot-tracking  
**Date**: 2025-12-03  
**Phase**: 1 - Design & Contracts

## Entity Relationship Diagram

```
┌─────────────────────────┐
│     UserProfile         │
│─────────────────────────│
│ Id (PK)                 │
│ Name                    │
│ AvatarPath              │
│ IsDeleted               │
│ CreatedAt               │
│ LastModifiedAt          │
│ SyncId                  │
└─────────────────────────┘
           ▲
           │ 0..1
           │
           │ MadeBy (FK)
           │
┌──────────┴──────────────┐         ┌─────────────────────────┐
│     ShotRecord          │         │        Bean             │
│─────────────────────────│         │─────────────────────────│
│ Id (PK)                 │◄────────│ Id (PK)                 │
│ MadeById (FK, NULL)     │  0..*   │ Name                    │
│ MadeForId (FK, NULL)    │  BeanId │ Roaster                 │
│ PreinfusionTime (NULL)  │         │ Origin                  │
│ BeanId (FK)             │         │ ...                     │
│ GrinderId (FK)          │         └─────────────────────────┘
│ MachineId (FK)          │
│ DoseIn                  │         ┌─────────────────────────┐
│ ActualTime              │         │      Equipment          │
│ ActualOutput            │◄────────│─────────────────────────│
│ ExpectedTime            │  0..*   │ Id (PK)                 │
│ ExpectedOutput          │ Grinder │ Name                    │
│ GrindSetting            │ Machine │ Type                    │
│ DrinkType               │         │ ...                     │
│ Rating                  │         └─────────────────────────┘
│ Timestamp               │
│ IsDeleted               │
│ LastModifiedAt          │
│ SyncId                  │
└─────────────────────────┘
           │ MadeFor (FK)
           │
           │ 0..1
           ▼
┌─────────────────────────┐
│     UserProfile         │
│  (same entity)          │
└─────────────────────────┘
```

## Entity Definitions

### ShotRecord (Modified)

**Purpose**: Represents a single espresso shot logging event with all associated metadata.

**Fields**:

| Field Name | Type | Nullable | Constraints | Description |
|------------|------|----------|-------------|-------------|
| Id | int | No | PK, Identity | Unique identifier |
| **MadeById** | **int** | **Yes** | **FK → UserProfile.Id** | **NEW: User who pulled the shot** |
| **MadeForId** | **int** | **Yes** | **FK → UserProfile.Id** | **NEW: User the shot was made for** |
| **PreinfusionTime** | **decimal(5,2)** | **Yes** | **Range: 0-60** | **NEW: Preinfusion duration in seconds** |
| BeanId | int | Yes | FK → Bean.Id | Coffee bean used |
| GrinderId | int | Yes | FK → Equipment.Id | Grinder used |
| MachineId | int | Yes | FK → Equipment.Id | Espresso machine used |
| DoseIn | decimal(5,2) | No | > 0 | Coffee dose in grams |
| ActualTime | decimal(5,2) | No | > 0 | Actual extraction time in seconds |
| ActualOutput | decimal(5,2) | No | > 0 | Actual output in grams |
| ExpectedTime | decimal(5,2) | Yes | > 0 | Target extraction time |
| ExpectedOutput | decimal(5,2) | Yes | > 0 | Target output weight |
| GrindSetting | string(50) | Yes | | Grinder setting used |
| DrinkType | string(50) | No | Enum | Type of drink (Espresso, Americano, etc) |
| Rating | int | Yes | 1-5 | Quality rating |
| Timestamp | DateTimeOffset | No | | When shot was pulled |
| IsDeleted | bool | No | Default: false | Soft delete flag |
| LastModifiedAt | DateTimeOffset | No | | Last update timestamp |
| SyncId | Guid | No | Unique | Cross-device sync identifier |

**Navigation Properties**:
```csharp
public UserProfile? MadeBy { get; set; }      // NEW
public UserProfile? MadeFor { get; set; }     // NEW
public Bean? Bean { get; set; }
public Equipment? Grinder { get; set; }
public Equipment? Machine { get; set; }
```

**Indexes**:
```sql
-- Existing indexes
IX_ShotRecords_BeanId
IX_ShotRecords_GrinderId
IX_ShotRecords_MachineId
IX_ShotRecords_Timestamp

-- NEW indexes for filtering/analytics
IX_ShotRecords_MadeById
IX_ShotRecords_MadeForId
```

**Validation Rules**:
- PreinfusionTime: Must be between 0 and 60 seconds if provided
- MadeById and MadeForId can reference the same user (valid scenario: making shot for yourself)
- MadeById and MadeForId can both be null (optional tracking)
- Foreign key relationships use `ReferentialAction.Restrict` to prevent cascading deletes

**State Transitions**: N/A (CRUD entity, no state machine)

---

### UserProfile (Existing - No Changes)

**Purpose**: Represents a user of the app (barista or customer).

**Fields**: (Existing structure maintained)

| Field Name | Type | Nullable | Description |
|------------|------|----------|-------------|
| Id | int | No | Primary key |
| Name | string | No | Display name |
| AvatarPath | string | Yes | Path to avatar image |
| IsDeleted | bool | No | Soft delete flag |
| CreatedAt | DateTimeOffset | No | Creation timestamp |
| LastModifiedAt | DateTimeOffset | No | Last update timestamp |
| SyncId | Guid | No | Cross-device sync identifier |

**Navigation Properties**:
```csharp
// NEW: Reverse navigation for analytics/filtering
public ICollection<ShotRecord> ShotsMadeBy { get; set; }
public ICollection<ShotRecord> ShotsMadeFor { get; set; }
```

**Note**: Soft delete pattern (`IsDeleted = true`) ensures historical shot records maintain valid references even after user deletion.

---

## Database Migration

### Migration: AddShotMakerRecipientPreinfusion

**Up Migration**:

```sql
-- Add new columns
ALTER TABLE ShotRecords 
ADD COLUMN MadeById INTEGER NULL;

ALTER TABLE ShotRecords 
ADD COLUMN MadeForId INTEGER NULL;

ALTER TABLE ShotRecords 
ADD COLUMN PreinfusionTime DECIMAL(5,2) NULL;

-- Add indexes
CREATE INDEX IX_ShotRecords_MadeById 
ON ShotRecords(MadeById);

CREATE INDEX IX_ShotRecords_MadeForId 
ON ShotRecords(MadeForId);

-- Add foreign key constraints
ALTER TABLE ShotRecords 
ADD CONSTRAINT FK_ShotRecords_UserProfiles_MadeById 
FOREIGN KEY (MadeById) 
REFERENCES UserProfiles(Id) 
ON DELETE RESTRICT;

ALTER TABLE ShotRecords 
ADD CONSTRAINT FK_ShotRecords_UserProfiles_MadeForId 
FOREIGN KEY (MadeForId) 
REFERENCES UserProfiles(Id) 
ON DELETE RESTRICT;
```

**Down Migration**:

```sql
-- Drop foreign key constraints
ALTER TABLE ShotRecords 
DROP CONSTRAINT FK_ShotRecords_UserProfiles_MadeById;

ALTER TABLE ShotRecords 
DROP CONSTRAINT FK_ShotRecords_UserProfiles_MadeForId;

-- Drop indexes
DROP INDEX IX_ShotRecords_MadeById;
DROP INDEX IX_ShotRecords_MadeForId;

-- Drop columns
ALTER TABLE ShotRecords DROP COLUMN MadeById;
ALTER TABLE ShotRecords DROP COLUMN MadeForId;
ALTER TABLE ShotRecords DROP COLUMN PreinfusionTime;
```

**Data Impact**: 
- Existing shot records remain valid with NULL values for new fields
- No data loss or transformation required
- Backward compatible

---

## Preferences Storage Schema

**Storage Mechanism**: .NET MAUI Preferences API (key-value store)

**Keys and Values**:

| Preference Key | Value Type | Description | Example |
|----------------|------------|-------------|---------|
| `LastUsedBeanId` | int | Last selected bean ID | `5` |
| `LastUsedMadeById` | int | Last selected maker user ID | `12` |
| `LastUsedMadeForId` | int | Last selected recipient user ID | `7` |
| `LastUsedGrinderId` | int | Last selected grinder ID | `3` |
| `LastUsedMachineId` | int | Last selected machine ID | `1` |
| `LastUsedDoseIn` | decimal | Last entered dose in grams | `18.5` |
| `LastUsedExpectedTime` | decimal | Last entered expected time | `28.0` |
| `LastUsedExpectedOutput` | decimal | Last entered expected output | `36.0` |
| `LastUsedGrindSetting` | string | Last entered grind setting | `"2.5"` |
| `LastUsedDrinkType` | string | Last selected drink type | `"Espresso"` |

**Data Lifecycle**:
- **Write**: On successful shot creation/update
- **Read**: On ShotLoggingPage initialization (new shot only, not edit mode)
- **Clear**: On user logout or "Clear Defaults" action (future enhancement)
- **Persistence**: Survives app restarts, preserved across updates

**Validation**:
- IDs validated against database (ensure referenced entity exists before applying)
- Numeric values validated against reasonable ranges
- Invalid preferences ignored (fall back to empty/default)

---

## Validation Summary

### Client-Side Validation (ShotLoggingPage)

```csharp
// Preinfusion time range check
if (State.PreinfusionTime.HasValue && 
    (State.PreinfusionTime < 0 || State.PreinfusionTime > 60))
{
    await FeedbackService.ShowError(
        "Preinfusion time must be between 0 and 60 seconds");
    return;
}

// Optional: Warn if maker/recipient both empty
if (!State.SelectedMakerId.HasValue && !State.SelectedRecipientId.HasValue)
{
    // Allow but potentially show informational message
}
```

### Service-Side Validation (CreateShotDto/UpdateShotDto)

```csharp
[Range(0, 60, ErrorMessage = "Preinfusion time must be between 0 and 60 seconds")]
public decimal? PreinfusionTime { get; set; }

// Foreign key validation (EF Core automatic)
// - MadeById must reference existing UserProfile or be null
// - MadeForId must reference existing UserProfile or be null
```

---

## Query Patterns

### Load Shot with Maker/Recipient for Display

```csharp
var shot = await context.ShotRecords
    .Include(s => s.MadeBy)
    .Include(s => s.MadeFor)
    .Include(s => s.Bean)
    .Include(s => s.Grinder)
    .Include(s => s.Machine)
    .Where(s => !s.IsDeleted)
    .FirstOrDefaultAsync(s => s.Id == shotId);
```

### Filter Shots by Maker

```csharp
var shotsByMaker = await context.ShotRecords
    .Include(s => s.MadeBy)
    .Include(s => s.MadeFor)
    .Where(s => !s.IsDeleted && s.MadeById == userId)
    .OrderByDescending(s => s.Timestamp)
    .ToListAsync();
```

### Filter Shots by Recipient

```csharp
var shotsForUser = await context.ShotRecords
    .Include(s => s.MadeBy)
    .Include(s => s.MadeFor)
    .Where(s => !s.IsDeleted && s.MadeForId == userId)
    .OrderByDescending(s => s.Timestamp)
    .ToListAsync();
```

### Load All Users for Picker

```csharp
var users = await context.UserProfiles
    .Where(u => !u.IsDeleted)
    .OrderBy(u => u.Name)
    .ToListAsync();
```

---

**Phase 1 Data Model Complete**: Entity structure defined, migrations planned, validation rules established. Ready for contract definition.
