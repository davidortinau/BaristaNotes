# Data Model: Espresso Shot Tracking & Management

**Feature**: 001-espresso-tracking  
**Phase**: 1 - Design & Contracts  
**Date**: 2025-12-02

## Purpose

This document defines the data model for BaristaNotes espresso tracking application, including entities, relationships, validation rules, and Entity Framework Core configuration.

## Entity Definitions

### 1. Equipment

Represents espresso-making tools (machines, grinders, accessories).

**Properties**:
```csharp
public class Equipment
{
    public int Id { get; set; }                          // Primary key
    public string Name { get; set; }                     // Required, max 100 chars
    public EquipmentType Type { get; set; }              // Enum: Machine, Grinder, Tamper, PuckScreen, Other
    public string? Notes { get; set; }                   // Optional, max 500 chars
    public bool IsActive { get; set; } = true;           // Archiving flag
    public DateTimeOffset CreatedAt { get; set; }        // Timestamp
    
    // CoreSync metadata (preparation for future sync)
    public Guid SyncId { get; set; }                     // Unique sync identifier
    public DateTimeOffset LastModifiedAt { get; set; }   // Last modification timestamp
    public bool IsDeleted { get; set; } = false;         // Soft delete flag
    
    // Navigation properties
    public virtual ICollection<ShotEquipment> ShotEquipments { get; set; } = new List<ShotEquipment>();
}
```

**Validation Rules**:
- Name: Required, 1-100 characters
- Type: Required enum value
- Notes: Optional, max 500 characters
- IsActive: Defaults to true
- CreatedAt: Auto-set on creation
- SyncId: Auto-generated GUID
- LastModifiedAt: Auto-updated on save

**Indexes**:
- `Name, Type` (composite for quick lookup by name and type)
- `IsActive` (filter active equipment)
- `SyncId` (unique, for future sync)

---

### 2. Bean

Represents coffee beans used for shots.

**Properties**:
```csharp
public class Bean
{
    public int Id { get; set; }                          // Primary key
    public string Name { get; set; }                     // Required, max 100 chars
    public string? Roaster { get; set; }                 // Optional, max 100 chars
    public DateTimeOffset? RoastDate { get; set; }       // Optional
    public string? Origin { get; set; }                  // Optional, max 100 chars
    public string? Notes { get; set; }                   // Optional, max 500 chars
    public bool IsActive { get; set; } = true;           // Archiving flag
    public DateTimeOffset CreatedAt { get; set; }        // Timestamp
    
    // CoreSync metadata
    public Guid SyncId { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    // Navigation properties
    public virtual ICollection<ShotRecord> ShotRecords { get; set; } = new List<ShotRecord>();
}
```

**Validation Rules**:
- Name: Required, 1-100 characters
- Roaster: Optional, max 100 characters
- RoastDate: Optional, must be in past if provided
- Origin: Optional, max 100 characters
- Notes: Optional, max 500 characters
- IsActive: Defaults to true
- CreatedAt: Auto-set on creation

**Indexes**:
- `Name, Roaster` (composite for quick lookup)
- `IsActive` (filter active beans)
- `SyncId` (unique)

---

### 3. UserProfile

Represents people in the household (baristas and consumers).

**Properties**:
```csharp
public class UserProfile
{
    public int Id { get; set; }                          // Primary key
    public string Name { get; set; }                     // Required, max 50 chars
    public string? AvatarPath { get; set; }              // Optional, future: local file path
    public DateTimeOffset CreatedAt { get; set; }        // Timestamp
    
    // CoreSync metadata
    public Guid SyncId { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    // Navigation properties
    public virtual ICollection<ShotRecord> ShotsMadeBy { get; set; } = new List<ShotRecord>();
    public virtual ICollection<ShotRecord> ShotsMadeFor { get; set; } = new List<ShotRecord>();
}
```

**Validation Rules**:
- Name: Required, 1-50 characters
- AvatarPath: Optional, future feature (out of scope for MVP)
- CreatedAt: Auto-set on creation

**Indexes**:
- `Name` (quick lookup by name)
- `SyncId` (unique)

---

### 4. ShotRecord

Represents a logged espresso shot with recipe, actuals, and ratings.

**Properties**:
```csharp
public class ShotRecord
{
    public int Id { get; set; }                          // Primary key
    public DateTimeOffset Timestamp { get; set; }        // Shot pull timestamp
    
    // Foreign keys
    public int? BeanId { get; set; }                     // Optional (can be null if bean deleted)
    public int? MachineId { get; set; }                  // Optional (can be null)
    public int? GrinderId { get; set; }                  // Optional (can be null)
    public int? MadeById { get; set; }                   // Optional (barista profile)
    public int? MadeForId { get; set; }                  // Optional (consumer profile)
    
    // Recipe parameters
    public decimal DoseIn { get; set; }                  // Grams, required
    public string GrindSetting { get; set; }             // String (flexible for all grinder types)
    public decimal ExpectedTime { get; set; }            // Seconds, required
    public decimal ExpectedOutput { get; set; }          // Grams, required
    public string DrinkType { get; set; }                // Required, max 50 chars
    
    // Actual results
    public decimal? ActualTime { get; set; }             // Seconds, optional (can record later)
    public decimal? ActualOutput { get; set; }           // Grams, optional (can record later)
    
    // Rating
    public int? Rating { get; set; }                     // 1-5 scale, optional
    
    // CoreSync metadata
    public Guid SyncId { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    // Navigation properties
    public virtual Bean? Bean { get; set; }
    public virtual Equipment? Machine { get; set; }
    public virtual Equipment? Grinder { get; set; }
    public virtual UserProfile? MadeBy { get; set; }
    public virtual UserProfile? MadeFor { get; set; }
    public virtual ICollection<ShotEquipment> ShotEquipments { get; set; } = new List<ShotEquipment>();
}
```

**Validation Rules**:
- Timestamp: Required, auto-set on creation, can be modified
- DoseIn: Required, range 5-30 grams (typical espresso range)
- GrindSetting: Required, max 50 characters
- ExpectedTime: Required, range 10-60 seconds
- ExpectedOutput: Required, range 10-80 grams
- DrinkType: Required, max 50 characters
- ActualTime: Optional, range 5-120 seconds if provided
- ActualOutput: Optional, range 5-150 grams if provided
- Rating: Optional, range 1-5 if provided

**Indexes**:
- `Timestamp DESC` (primary query for activity feed)
- `BeanId` (filter by bean)
- `MadeById, MadeForId` (filter by user)
- `SyncId` (unique)

**Business Rules**:
- Can save with only expected values (actuals optional)
- Can edit actuals and rating after initial save
- Foreign keys nullable to handle archived/deleted equipment/beans/profiles
- Display "Unknown Equipment" or "Unknown Bean" in UI if FK is null

---

### 5. ShotEquipment (Junction Table)

Represents the many-to-many relationship between shots and equipment accessories.

**Properties**:
```csharp
public class ShotEquipment
{
    public int ShotRecordId { get; set; }                // FK to ShotRecord
    public int EquipmentId { get; set; }                 // FK to Equipment
    
    // Navigation properties
    public virtual ShotRecord ShotRecord { get; set; } = null!;
    public virtual Equipment Equipment { get; set; } = null!;
}
```

**Purpose**: 
- Links shots to multiple accessories (tamper, puck screen, distribution tool, etc.)
- Separate from Machine and Grinder (which are 1:1 relationships on ShotRecord)

**Indexes**:
- Primary Key: Composite `(ShotRecordId, EquipmentId)`
- `EquipmentId` (reverse lookup)

---

## Enumerations

### EquipmentType

```csharp
public enum EquipmentType
{
    Machine = 1,
    Grinder = 2,
    Tamper = 3,
    PuckScreen = 4,
    Other = 99
}
```

**Usage**: Categorizes equipment for filtering and validation.

---

## Entity Relationships

```
UserProfile (1) ----< (M) ShotRecord.MadeBy
UserProfile (1) ----< (M) ShotRecord.MadeFor
Bean (1) ----< (M) ShotRecord
Equipment (1) ----< (M) ShotRecord.Machine
Equipment (1) ----< (M) ShotRecord.Grinder
ShotRecord (M) >----< (M) Equipment (via ShotEquipment junction)
```

**Relationship Details**:

1. **UserProfile → ShotRecord (MadeBy)**: One-to-Many
   - One user profile can be the barista for many shots
   - Foreign key: `ShotRecord.MadeById`
   - Delete behavior: SetNull (preserve shot history if profile deleted)

2. **UserProfile → ShotRecord (MadeFor)**: One-to-Many
   - One user profile can be the consumer for many shots
   - Foreign key: `ShotRecord.MadeForId`
   - Delete behavior: SetNull

3. **Bean → ShotRecord**: One-to-Many
   - One bean can be used in many shots
   - Foreign key: `ShotRecord.BeanId`
   - Delete behavior: SetNull (preserve shot history if bean archived)

4. **Equipment → ShotRecord (Machine)**: One-to-Many
   - One machine can be used in many shots
   - Foreign key: `ShotRecord.MachineId`
   - Delete behavior: SetNull

5. **Equipment → ShotRecord (Grinder)**: One-to-Many
   - One grinder can be used in many shots
   - Foreign key: `ShotRecord.GrinderId`
   - Delete behavior: SetNull

6. **ShotRecord ↔ Equipment (Accessories)**: Many-to-Many
   - One shot can use multiple accessories
   - One accessory can be used in many shots
   - Junction table: `ShotEquipment`
   - Delete behavior: Cascade (remove junction records if shot or equipment deleted)

---

## Database Configuration

### Entity Framework Core Context

```csharp
public class BaristaNotesContext : DbContext
{
    public DbSet<Equipment> Equipment { get; set; } = null!;
    public DbSet<Bean> Beans { get; set; } = null!;
    public DbSet<UserProfile> UserProfiles { get; set; } = null!;
    public DbSet<ShotRecord> ShotRecords { get; set; } = null!;
    public DbSet<ShotEquipment> ShotEquipments { get; set; } = null!;
    
    public BaristaNotesContext(DbContextOptions<BaristaNotesContext> options) 
        : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Equipment configuration
        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.SyncId).IsRequired();
            entity.Property(e => e.LastModifiedAt).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
            
            entity.HasIndex(e => new { e.Name, e.Type });
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.SyncId).IsUnique();
        });
        
        // Bean configuration
        modelBuilder.Entity<Bean>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Roaster).HasMaxLength(100);
            entity.Property(e => e.Origin).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.SyncId).IsRequired();
            entity.Property(e => e.LastModifiedAt).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
            
            entity.HasIndex(e => new { e.Name, e.Roaster });
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.SyncId).IsUnique();
        });
        
        // UserProfile configuration
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AvatarPath).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.SyncId).IsRequired();
            entity.Property(e => e.LastModifiedAt).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
            
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.SyncId).IsUnique();
        });
        
        // ShotRecord configuration
        modelBuilder.Entity<ShotRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.DoseIn).IsRequired().HasPrecision(5, 2);
            entity.Property(e => e.GrindSetting).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ExpectedTime).IsRequired().HasPrecision(5, 2);
            entity.Property(e => e.ExpectedOutput).IsRequired().HasPrecision(5, 2);
            entity.Property(e => e.DrinkType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ActualTime).HasPrecision(5, 2);
            entity.Property(e => e.ActualOutput).HasPrecision(5, 2);
            entity.Property(e => e.SyncId).IsRequired();
            entity.Property(e => e.LastModifiedAt).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
            
            entity.HasIndex(e => e.Timestamp).IsDescending();
            entity.HasIndex(e => e.BeanId);
            entity.HasIndex(e => e.MadeById);
            entity.HasIndex(e => e.MadeForId);
            entity.HasIndex(e => e.SyncId).IsUnique();
            
            // Relationships
            entity.HasOne(e => e.Bean)
                .WithMany(b => b.ShotRecords)
                .HasForeignKey(e => e.BeanId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Machine)
                .WithMany()
                .HasForeignKey(e => e.MachineId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Grinder)
                .WithMany()
                .HasForeignKey(e => e.GrinderId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.MadeBy)
                .WithMany(u => u.ShotsMadeBy)
                .HasForeignKey(e => e.MadeById)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.MadeFor)
                .WithMany(u => u.ShotsMadeFor)
                .HasForeignKey(e => e.MadeForId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        
        // ShotEquipment (junction) configuration
        modelBuilder.Entity<ShotEquipment>(entity =>
        {
            entity.HasKey(e => new { e.ShotRecordId, e.EquipmentId });
            
            entity.HasOne(e => e.ShotRecord)
                .WithMany(s => s.ShotEquipments)
                .HasForeignKey(e => e.ShotRecordId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Equipment)
                .WithMany(eq => eq.ShotEquipments)
                .HasForeignKey(e => e.EquipmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

---

## Migration Strategy

### Initial Migration

```bash
dotnet ef migrations add InitialCreate --project BaristaNotes --startup-project BaristaNotes
dotnet ef database update --project BaristaNotes --startup-project BaristaNotes
```

### Future Migrations

When adding/modifying entities:
1. Update model classes
2. Run `dotnet ef migrations add <MigrationName>`
3. Review generated migration for correctness
4. Test migration on dev database
5. Apply to production (when cloud sync implemented)

---

## Seeding Strategy

### Default Data (Optional)

For development/testing, seed with:
- 1-2 default user profiles ("Me", "Guest")
- 1-2 sample equipment items (generic machine, generic grinder)
- 1 sample bean (for first-run experience)

**Implementation**: Use `OnModelCreating` with `modelBuilder.Entity<T>().HasData()` or seed in `MauiProgram.cs` on first launch.

---

## Query Patterns

### Most Recent Shot (for pre-population)

```csharp
var lastShot = await context.ShotRecords
    .AsNoTracking()
    .Include(s => s.Bean)
    .Include(s => s.Machine)
    .Include(s => s.Grinder)
    .Include(s => s.MadeBy)
    .Include(s => s.MadeFor)
    .Include(s => s.ShotEquipments)
        .ThenInclude(se => se.Equipment)
    .OrderByDescending(s => s.Timestamp)
    .FirstOrDefaultAsync();
```

### Activity Feed (paginated)

```csharp
var shots = await context.ShotRecords
    .AsNoTracking()
    .Include(s => s.Bean)
    .Include(s => s.Machine)
    .Include(s => s.Grinder)
    .Include(s => s.MadeBy)
    .Include(s => s.MadeFor)
    .Where(s => !s.IsDeleted)
    .OrderByDescending(s => s.Timestamp)
    .Skip(pageIndex * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

### Filter by User

```csharp
var userShots = await context.ShotRecords
    .AsNoTracking()
    .Where(s => s.MadeById == userId || s.MadeForId == userId)
    .Where(s => !s.IsDeleted)
    .OrderByDescending(s => s.Timestamp)
    .ToListAsync();
```

---

## Summary

Data model complete with 5 entities (Equipment, Bean, UserProfile, ShotRecord, ShotEquipment), validation rules, indexes, and EF Core configuration. Supports all functional requirements FR-001 through FR-015. CoreSync metadata included for future sync capability. Soft deletes implemented for data preservation. Ready for Phase 1 contracts generation.
