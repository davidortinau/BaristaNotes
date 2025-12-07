# Data Model: Bean Rating Tracking and Bag Management

**Feature**: 001-bean-rating-tracking  
**Phase**: 1 - Design & Contracts  
**Date**: 2025-12-07

## Entity Relationship Diagram

```
┌─────────────────────┐
│       Bean          │
│─────────────────────│
│ Id (PK)             │
│ Name                │◄──────┐
│ Roaster             │       │
│ Origin              │       │ 1:N
│ Notes               │       │
│ IsActive            │       │
│ CreatedAt           │       │
│ SyncId (unique)     │       │
│ LastModifiedAt      │       │
│ IsDeleted           │       │
└─────────────────────┘       │
                              │
                    ┌─────────┴──────────┐
                    │       Bag          │
                    │────────────────────│
                    │ Id (PK)            │
                    │ BeanId (FK) ───────┘
                    │ RoastDate          │◄──────┐
                    │ Notes              │       │
                    │ IsActive           │       │ 1:N
                    │ CreatedAt          │       │
                    │ SyncId (unique)    │       │
                    │ LastModifiedAt     │       │
                    │ IsDeleted          │       │
                    └────────────────────┘       │
                                                 │
                              ┌──────────────────┴─────────────┐
                              │      ShotRecord                │
                              │────────────────────────────────│
                              │ Id (PK)                        │
                              │ BagId (FK) ────────────────────┘
                              │ Timestamp                      │
                              │ MachineId (FK)                 │
                              │ GrinderId (FK)                 │
                              │ MadeById (FK)                  │
                              │ MadeForId (FK)                 │
                              │ DoseIn                         │
                              │ GrindSetting                   │
                              │ ExpectedTime                   │
                              │ ExpectedOutput                 │
                              │ DrinkType                      │
                              │ ActualTime                     │
                              │ ActualOutput                   │
                              │ PreinfusionTime                │
                              │ Rating (1-5, nullable)         │
                              │ SyncId (unique)                │
                              │ LastModifiedAt                 │
                              │ IsDeleted                      │
                              └────────────────────────────────┘
```

## Entity Definitions

### Bean (Modified)

**Purpose**: Represents a coffee bean variety (brand, origin, roaster). Multiple physical bags of the same bean variety share this record.

**Changes from Current**:
- **Removed**: `RoastDate` property (moved to Bag entity)
- **Removed**: Direct navigation property to `ShotRecords` (now goes through Bags)
- **Added**: Navigation property `Bags` (one-to-many)

**Schema**:
```csharp
public class Bean
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Roaster { get; set; }
    public string? Origin { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    
    // CoreSync metadata
    public Guid SyncId { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    // Navigation properties
    public virtual ICollection<Bag> Bags { get; set; } = new List<Bag>();
}
```

**Validation Rules** (from FR-001, FR-007):
- `Name`: Required, max 100 chars
- `Roaster`: Optional, max 100 chars
- `Origin`: Optional, max 100 chars
- `Notes`: Optional, max 500 chars
- `IsActive`: Required, default true
- `SyncId`: Required, unique (for future CoreSync)

**Indexes**:
- Primary key: `Id`
- Unique: `SyncId`
- Composite: `Name`, `Roaster` (for duplicate detection)

---

### Bag (New Entity)

**Purpose**: Represents a physical bag of a specific bean, distinguished by roasting date. Links beans to shots, enabling bag-level and bean-level rating aggregation.

**Schema**:
```csharp
public class Bag
{
    public int Id { get; set; }
    public int BeanId { get; set; }
    public DateTimeOffset RoastDate { get; set; }
    public string? Notes { get; set; } // e.g., "From Trader Joe's", "Gift from friend"
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    
    // CoreSync metadata
    public Guid SyncId { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    // Navigation properties
    public virtual Bean Bean { get; set; } = null!;
    public virtual ICollection<ShotRecord> ShotRecords { get; set; } = new List<ShotRecord>();
}
```

**Validation Rules** (from FR-003, FR-008):
- `BeanId`: Required, FK to Bean.Id
- `RoastDate`: Required (business rule: cannot be future date)
- `Notes`: Optional, max 500 chars
- `IsActive`: Required, default true
- `SyncId`: Required, unique

**Business Rules**:
- Multiple bags can have same RoastDate (edge case from spec: distinguish by Notes or creation timestamp)
- RoastDate cannot be in the future (validation in service layer)
- Deleting a Bag soft-deletes associated ShotRecords (cascade delete via service, not DB FK)

**Indexes**:
- Primary key: `Id`
- Foreign key: `BeanId` (for bean→bags queries)
- Unique: `SyncId`
- Composite: `BeanId`, `RoastDate DESC` (for "most recent bag" queries)

---

### ShotRecord (Modified)

**Purpose**: Records a single espresso shot with parameters and rating. Links to Bag (instead of directly to Bean).

**Changes from Current**:
- **Removed**: `BeanId` FK property
- **Removed**: `Bean` navigation property
- **Added**: `BagId` FK property
- **Added**: `Bag` navigation property

**Schema**:
```csharp
public class ShotRecord
{
    public int Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    
    // Foreign keys
    public int BagId { get; set; } // CHANGED: was BeanId
    public int? MachineId { get; set; }
    public int? GrinderId { get; set; }
    public int? MadeById { get; set; }
    public int? MadeForId { get; set; }
    
    // Recipe parameters
    public decimal DoseIn { get; set; }
    public string GrindSetting { get; set; } = string.Empty;
    public decimal ExpectedTime { get; set; }
    public decimal ExpectedOutput { get; set; }
    public string DrinkType { get; set; } = string.Empty;
    
    // Actual results
    public decimal? ActualTime { get; set; }
    public decimal? ActualOutput { get; set; }
    public decimal? PreinfusionTime { get; set; }
    
    // Rating (1-5 scale)
    public int? Rating { get; set; }
    
    // CoreSync metadata
    public Guid SyncId { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    // Navigation properties
    public virtual Bag Bag { get; set; } = null!; // CHANGED: was Bean
    public virtual Equipment? Machine { get; set; }
    public virtual Equipment? Grinder { get; set; }
    public virtual UserProfile? MadeBy { get; set; }
    public virtual UserProfile? MadeFor { get; set; }
    public virtual ICollection<ShotEquipment> ShotEquipments { get; set; } = new List<ShotEquipment>();
}
```

**Validation Rules** (from FR-004, FR-009):
- `BagId`: Required, FK to Bag.Id
- `Rating`: Optional, range 1-5 (validated in service layer)
- All other fields: Keep existing validation from current schema

**Indexes**:
- Primary key: `Id`
- **NEW** Composite: `BagId`, `Rating` (for rating aggregate queries - CRITICAL for performance)
- Foreign key: `BagId` (for bag→shots queries)
- Unique: `SyncId`

---

## Computed/Virtual Entities (DTOs)

These are **not** stored in the database. Computed on-demand from ShotRecord queries.

### RatingAggregateDto

**Purpose**: Encapsulates aggregate rating data (average + distribution) for display in UI.

**Schema**:
```csharp
public class RatingAggregateDto
{
    public double AverageRating { get; set; } // 0.00 if no ratings
    public int TotalShots { get; set; }
    public int RatedShots { get; set; } // Count of shots with non-null Rating
    public Dictionary<int, int> Distribution { get; set; } = new(); // Rating (1-5) → Count
    
    // Convenience properties
    public bool HasRatings => RatedShots > 0;
    public string FormattedAverage => HasRatings ? AverageRating.ToString("F2") : "N/A";
}
```

**Calculation Logic** (in RatingService):
```csharp
public async Task<RatingAggregateDto> GetBeanRatingAsync(int beanId)
{
    var shots = await _context.ShotRecords
        .Where(s => s.Bag.BeanId == beanId && !s.IsDeleted)
        .Select(s => s.Rating)
        .ToListAsync();
    
    var ratedShots = shots.Where(r => r.HasValue).ToList();
    
    return new RatingAggregateDto
    {
        TotalShots = shots.Count,
        RatedShots = ratedShots.Count,
        AverageRating = ratedShots.Any() ? ratedShots.Average(r => r.Value) : 0,
        Distribution = ratedShots
            .GroupBy(r => r.Value)
            .ToDictionary(g => g.Key, g => g.Count())
    };
}
```

**Used By**:
- BeanDetailPage (bean-level aggregate)
- BagDetailPage (bag-level aggregate - same logic, filtered by BagId)

---

### BagSummaryDto

**Purpose**: Lightweight DTO for listing bags with basic stats (used in BeanDetailPage bag list).

**Schema**:
```csharp
public class BagSummaryDto
{
    public int Id { get; set; }
    public int BeanId { get; set; }
    public DateTimeOffset RoastDate { get; set; }
    public string? Notes { get; set; }
    public int ShotCount { get; set; }
    public double? AverageRating { get; set; } // Null if no rated shots
    
    // Convenience properties
    public string FormattedRoastDate => RoastDate.ToString("MMM dd, yyyy");
    public string DisplayLabel => $"Roasted {FormattedRoastDate}" + 
                                  (Notes != null ? $" - {Notes}" : "");
}
```

**Query** (in BagRepository):
```csharp
public async Task<List<BagSummaryDto>> GetBagSummariesForBeanAsync(int beanId)
{
    return await _context.Bags
        .Where(b => b.BeanId == beanId && !b.IsDeleted)
        .Select(b => new BagSummaryDto
        {
            Id = b.Id,
            BeanId = b.BeanId,
            RoastDate = b.RoastDate,
            Notes = b.Notes,
            ShotCount = b.ShotRecords.Count(s => !s.IsDeleted),
            AverageRating = b.ShotRecords
                .Where(s => !s.IsDeleted && s.Rating != null)
                .Average(s => (double?)s.Rating)
        })
        .OrderByDescending(b => b.RoastDate)
        .ToListAsync();
}
```

---

## Migration Plan

### Migration File: `[timestamp]_AddBagEntity.cs`

**Generated via**: `dotnet ef migrations add AddBagEntity --project BaristaNotes.Core`

**Up() Method** (detailed in research.md, summarized here):
1. Create `Bags` table with schema above
2. Add nullable `BagId` column to `ShotRecords` table
3. Seed `Bags` table: For each existing Bean with RoastDate, create corresponding Bag
4. Update `ShotRecords.BagId` to link to newly created Bags
5. Make `ShotRecords.BagId` required (not null)
6. Add FK constraint `FK_ShotRecords_Bags_BagId`
7. Drop old `FK_ShotRecords_Beans_BeanId` constraint
8. Drop `BeanId` column from `ShotRecords`
9. Drop `RoastDate` column from `Beans`
10. Add indexes: `IX_ShotRecords_BagId_Rating`, `IX_Bags_BeanId`

**Down() Method** (rollback):
1. Add `RoastDate` column back to `Beans` (nullable)
2. Add `BeanId` column back to `ShotRecords` (nullable)
3. Copy `Bag.RoastDate` → `Bean.RoastDate` (via SQL)
4. Update `ShotRecords.BeanId` from `Bag.BeanId` (via SQL)
5. Make `ShotRecords.BeanId` required
6. Add FK constraint `FK_ShotRecords_Beans_BeanId`
7. Drop FK `FK_ShotRecords_Bags_BagId`
8. Drop `BagId` column from `ShotRecords`
9. Drop `Bags` table
10. Drop indexes added in Up()

**Testing**:
- Unit test: Verify Up() creates Bags from existing Beans
- Unit test: Verify Down() restores original schema
- Integration test: Run Up() + Down() cycle, assert data integrity

---

## State Transitions

### Bag Lifecycle

```
[Created] ──(user adds bag)──> [Active]
                                   │
                                   │ (user logs shots)
                                   ▼
                              [Active, Has Shots]
                                   │
                                   │ (user marks inactive)
                                   ▼
                              [Inactive]
                                   │
                                   │ (user deletes)
                                   ▼
                              [Soft Deleted] (IsDeleted = true)
```

**Rules**:
- Cannot delete Bag if it has ShotRecords (must soft-delete instead)
- Soft-deleted Bags excluded from all queries (WHERE !IsDeleted)
- Bags with IsActive=false still appear in detail views, but not in bag picker

### Rating Calculation Triggers

```
ShotRecord CRUD Event
    │
    ├─> Create with Rating ──> Recalculate Bean & Bag aggregates
    ├─> Update Rating ──────> Recalculate Bean & Bag aggregates
    ├─> Delete ─────────────> Recalculate Bean & Bag aggregates
    └─> Create without Rating ─> No rating recalculation (TotalShots increments)
```

**Implementation**: 
- Not event-driven (no DB triggers per constitution)
- Calculation happens on-demand when UI requests `RatingAggregateDto`
- Service methods return fresh calculations every time

---

## Indexing Strategy

### Required Indexes (for <500ms performance target)

```sql
-- Bag lookups by Bean
CREATE INDEX IX_Bags_BeanId ON Bags (BeanId);

-- Bag selection (newest first)
CREATE INDEX IX_Bags_BeanId_RoastDate ON Bags (BeanId, RoastDate DESC);

-- Rating aggregation (CRITICAL for performance)
CREATE INDEX IX_ShotRecords_BagId_Rating ON ShotRecords (BagId, Rating);

-- CoreSync queries
CREATE UNIQUE INDEX IX_Bags_SyncId ON Bags (SyncId);
```

### Query Patterns Enabled

1. **Get bags for bean** (O(log n) via index):
   ```sql
   SELECT * FROM Bags WHERE BeanId = ? AND IsDeleted = 0 ORDER BY RoastDate DESC
   ```

2. **Get newest bag for bean** (O(log n) via index):
   ```sql
   SELECT * FROM Bags WHERE BeanId = ? AND IsDeleted = 0 ORDER BY RoastDate DESC LIMIT 1
   ```

3. **Bean-level rating aggregate** (O(n) scan with index):
   ```sql
   SELECT Rating, COUNT(*) as Count
   FROM ShotRecords sr
   INNER JOIN Bags b ON sr.BagId = b.Id
   WHERE b.BeanId = ? AND sr.IsDeleted = 0 AND sr.Rating IS NOT NULL
   GROUP BY Rating
   ```

4. **Bag-level rating aggregate** (O(n) scan with index):
   ```sql
   SELECT Rating, COUNT(*) as Count
   FROM ShotRecords
   WHERE BagId = ? AND IsDeleted = 0 AND Rating IS NOT NULL
   GROUP BY Rating
   ```

---

## Validation Summary

### Service-Layer Validation (not DB constraints)

**BagService**:
- `CreateBagAsync(Bag bag)`: 
  - RoastDate cannot be in future
  - BeanId must reference existing Bean
  - Notes max 500 chars

**ShotService**:
- `CreateShotAsync(ShotRecord shot)`:
  - BagId must reference existing, active Bag
  - Rating must be 1-5 or null
  - All existing shot validations maintained

**RatingService**:
- `GetBeanRatingAsync(int beanId)`:
  - BeanId must exist
  - Returns empty aggregate if no shots
- `GetBagRatingAsync(int bagId)`:
  - BagId must exist
  - Returns empty aggregate if no shots

---

## Summary

**New Entities**: Bag  
**Modified Entities**: Bean (removed RoastDate), ShotRecord (BagId replaces BeanId)  
**Virtual Entities**: RatingAggregateDto, BagSummaryDto  
**Indexes Added**: 4 (Bags.BeanId, Bags.BeanId+RoastDate, ShotRecords.BagId+Rating, Bags.SyncId)  
**Migration Complexity**: Medium (data migration + schema change, fully automated via EF Core)  
**Performance Impact**: Positive (indexed queries enable <500ms rating calculations)

**Next Phase**: Generate service contracts (Phase 1b).
