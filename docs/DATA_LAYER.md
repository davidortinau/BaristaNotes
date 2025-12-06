# Data Layer Architecture

This document describes the data persistence layer in BaristaNotes, including Entity Framework Core configuration, database schema, and data access patterns.

## Table of Contents

- [Overview](#overview)
- [Database Schema](#database-schema)
- [Entity Models](#entity-models)
- [DbContext Configuration](#dbcontext-configuration)
- [Service Layer](#service-layer)
- [Migrations](#migrations)
- [Best Practices](#best-practices)

## Overview

BaristaNotes uses Entity Framework Core with SQLite for local data persistence. The architecture follows a layered approach:

```
┌──────────────────┐
│   UI Layer       │
│  (Pages)         │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│  Service Layer   │
│  (Business Logic)│
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│   Data Layer     │
│  (EF Core)       │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│   SQLite DB      │
└──────────────────┘
```

### Key Principles

- **Separation of Concerns**: UI doesn't directly access DbContext
- **DTOs**: Services return Data Transfer Objects, not entities
- **Async Operations**: All database operations are asynchronous
- **Single DbContext**: One context instance per application lifetime (singleton)

## Database Schema

### Entity Relationship Diagram

```
┌─────────────┐       ┌─────────────┐
│ UserProfile │       │    Bean     │
└──────┬──────┘       └──────┬──────┘
       │                     │
       │ Made By             │
       │                     │
┌──────▼──────────────────────▼────┐
│           ShotRecord             │
│  ─────────────────────────────  │
│  + Id                            │
│  + BeanId (FK)                   │
│  + MadeById (FK)                 │
│  + MadeForId (FK)                │
│  + Dose                          │
│  + GrindSetting                  │
│  + OutputWeight                  │
│  + ExtractionTime                │
│  + WaterTemperature              │
│  + Rating                        │
│  + Notes                         │
│  + LoggedAt                      │
└──────┬───────────────┬───────────┘
       │               │
       │               │ Made For
       │               │
       ▼               ▼
┌─────────────┐  ┌─────────────┐
│   Machine   │  │   Grinder   │
└─────────────┘  └─────────────┘
```

### Tables

#### ShotRecord
Primary entity for espresso shot tracking.

| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER | Primary key |
| BeanId | INTEGER | Foreign key to Bean |
| MadeById | INTEGER | Foreign key to UserProfile (maker) |
| MadeForId | INTEGER | Foreign key to UserProfile (recipient) |
| MachineId | INTEGER | Foreign key to Equipment (machine) |
| GrinderId | INTEGER | Foreign key to Equipment (grinder) |
| Dose | REAL | Coffee dose in grams |
| GrindSetting | REAL | Grinder setting |
| OutputWeight | REAL | Output weight in grams |
| ExtractionTime | INTEGER | Extraction time in seconds |
| WaterTemperature | REAL | Water temperature in Celsius |
| Rating | INTEGER | Rating 1-5 |
| Notes | TEXT | User notes |
| LoggedAt | TEXT | Timestamp (ISO 8601) |

#### Bean
Coffee bean information.

| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER | Primary key |
| Name | TEXT | Bean name/label |
| Roaster | TEXT | Roaster name |
| Origin | TEXT | Coffee origin country |
| RoastDate | TEXT | Roast date (ISO 8601) |
| RoastLevel | INTEGER | Enum: Light, Medium, Dark |
| Notes | TEXT | Tasting notes, description |
| PurchaseDate | TEXT | Purchase date |
| PurchaseLocation | TEXT | Where purchased |
| CurrentStock | REAL | Current stock in grams |

#### Equipment
Espresso machines and grinders.

| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER | Primary key |
| Name | TEXT | Equipment name |
| Type | INTEGER | Enum: Machine, Grinder, Accessory |
| Manufacturer | TEXT | Manufacturer name |
| Model | TEXT | Model number |
| PurchaseDate | TEXT | Purchase date |
| Notes | TEXT | Equipment notes |

#### UserProfile
User profiles for maker/recipient tracking.

| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER | Primary key |
| Name | TEXT | Display name |
| AvatarPath | TEXT | Path to avatar image |
| IsActive | BOOLEAN | Active status |
| CreatedAt | TEXT | Creation timestamp |

## Entity Models

### ShotRecord Entity

```csharp
public class ShotRecord
{
    public int Id { get; set; }
    
    // Foreign Keys
    public int? BeanId { get; set; }
    public int? MadeById { get; set; }
    public int? MadeForId { get; set; }
    public int? MachineId { get; set; }
    public int? GrinderId { get; set; }
    
    // Shot Parameters
    public double Dose { get; set; }
    public double GrindSetting { get; set; }
    public double OutputWeight { get; set; }
    public int ExtractionTime { get; set; }
    public double? WaterTemperature { get; set; }
    
    // Evaluation
    public int Rating { get; set; }
    public string? Notes { get; set; }
    
    // Metadata
    public DateTime LoggedAt { get; set; }
    
    // Navigation Properties
    public Bean? Bean { get; set; }
    public UserProfile? MadeBy { get; set; }
    public UserProfile? MadeFor { get; set; }
    public Equipment? Machine { get; set; }
    public Equipment? Grinder { get; set; }
}
```

### Bean Entity

```csharp
public class Bean
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Roaster { get; set; }
    public string? Origin { get; set; }
    public DateTime? RoastDate { get; set; }
    public RoastLevel RoastLevel { get; set; }
    public string? Notes { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? PurchaseLocation { get; set; }
    public double CurrentStock { get; set; }
    
    // Navigation
    public ICollection<ShotRecord> ShotRecords { get; set; } = new List<ShotRecord>();
}

public enum RoastLevel
{
    Light,
    Medium,
    Dark
}
```

### UserProfile Entity

```csharp
public class UserProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AvatarPath { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    
    // Navigation
    public ICollection<ShotRecord> ShotsMadeBy { get; set; } = new List<ShotRecord>();
    public ICollection<ShotRecord> ShotsMadeFor { get; set; } = new List<ShotRecord>();
}
```

## DbContext Configuration

### BaristasDbContext

```csharp
public class BaristasDbContext : DbContext
{
    public DbSet<ShotRecord> ShotRecords { get; set; }
    public DbSet<Bean> Beans { get; set; }
    public DbSet<Equipment> Equipment { get; set; }
    public DbSet<UserProfile> UserProfiles { get; set; }
    
    public BaristasDbContext()
    {
        // Ensure database is created
        Database.EnsureCreated();
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dbPath = Path.Combine(
                FileSystem.AppDataDirectory, 
                "baristas.db"
            );
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure relationships
        modelBuilder.Entity<ShotRecord>()
            .HasOne(s => s.Bean)
            .WithMany(b => b.ShotRecords)
            .HasForeignKey(s => s.BeanId)
            .OnDelete(DeleteBehavior.SetNull);
            
        modelBuilder.Entity<ShotRecord>()
            .HasOne(s => s.MadeBy)
            .WithMany(u => u.ShotsMadeBy)
            .HasForeignKey(s => s.MadeById)
            .OnDelete(DeleteBehavior.SetNull);
            
        modelBuilder.Entity<ShotRecord>()
            .HasOne(s => s.MadeFor)
            .WithMany(u => u.ShotsMadeFor)
            .HasForeignKey(s => s.MadeForId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
```

### Database Location

The SQLite database file is stored in the application's data directory:

- **iOS**: `/var/mobile/Containers/Data/Application/{GUID}/Library/baristas.db`
- **Android**: `/data/data/com.yourcompany.baristanotes/files/baristas.db`

## Service Layer

Services provide a clean API for data operations and return DTOs instead of entities.

### Service Interface Example

```csharp
public interface IShotService
{
    Task<ShotDto?> GetShotByIdAsync(int id);
    Task<List<ShotDto>> GetAllShotsAsync();
    Task<List<ShotDto>> GetShotsByBeanIdAsync(int beanId);
    Task<ShotDto> CreateShotAsync(CreateShotRequest request);
    Task<ShotDto> UpdateShotAsync(int id, UpdateShotRequest request);
    Task DeleteShotAsync(int id);
}
```

### Service Implementation

```csharp
public class ShotService : IShotService
{
    private readonly BaristasDbContext _context;
    
    public ShotService(BaristasDbContext context)
    {
        _context = context;
    }
    
    public async Task<ShotDto?> GetShotByIdAsync(int id)
    {
        var shot = await _context.ShotRecords
            .Include(s => s.Bean)
            .Include(s => s.MadeBy)
            .Include(s => s.MadeFor)
            .Include(s => s.Machine)
            .Include(s => s.Grinder)
            .FirstOrDefaultAsync(s => s.Id == id);
            
        return shot == null ? null : MapToDto(shot);
    }
    
    public async Task<ShotDto> CreateShotAsync(CreateShotRequest request)
    {
        var shot = new ShotRecord
        {
            BeanId = request.BeanId,
            MadeById = request.MadeById,
            MadeForId = request.MadeForId,
            Dose = request.Dose,
            GrindSetting = request.GrindSetting,
            OutputWeight = request.OutputWeight,
            ExtractionTime = request.ExtractionTime,
            Rating = request.Rating,
            Notes = request.Notes,
            LoggedAt = DateTime.UtcNow
        };
        
        _context.ShotRecords.Add(shot);
        await _context.SaveChangesAsync();
        
        return MapToDto(shot);
    }
    
    private static ShotDto MapToDto(ShotRecord shot)
    {
        return new ShotDto
        {
            Id = shot.Id,
            BeanId = shot.BeanId,
            BeanName = shot.Bean?.Name,
            MadeById = shot.MadeById,
            MadeByName = shot.MadeBy?.Name,
            MadeForId = shot.MadeForId,
            MadeForName = shot.MadeFor?.Name,
            Dose = shot.Dose,
            GrindSetting = shot.GrindSetting,
            OutputWeight = shot.OutputWeight,
            ExtractionTime = shot.ExtractionTime,
            Rating = shot.Rating,
            Notes = shot.Notes,
            LoggedAt = shot.LoggedAt
        };
    }
}
```

### DTOs

DTOs decouple the database schema from the API surface:

```csharp
public record ShotDto
{
    public int Id { get; init; }
    public int? BeanId { get; init; }
    public string? BeanName { get; init; }
    public int? MadeById { get; init; }
    public string? MadeByName { get; init; }
    public int? MadeForId { get; init; }
    public string? MadeForName { get; init; }
    public double Dose { get; init; }
    public double GrindSetting { get; init; }
    public double OutputWeight { get; init; }
    public int ExtractionTime { get; init; }
    public int Rating { get; init; }
    public string? Notes { get; init; }
    public DateTime LoggedAt { get; init; }
    
    // Computed properties
    public double Ratio => Dose > 0 ? OutputWeight / Dose : 0;
}
```

## Migrations

Entity Framework Core migrations track database schema changes over time.

### Creating a Migration

When you modify entity models:

```bash
cd BaristaNotes.Core
dotnet ef migrations add MigrationName
```

This generates migration files in `Migrations/`:
- `{timestamp}_MigrationName.cs` - Migration code
- `{timestamp}_MigrationName.Designer.cs` - Metadata
- `BaristasDbContextModelSnapshot.cs` - Current schema snapshot

### Applying Migrations

Migrations are applied automatically on app startup via `Database.EnsureCreated()` or explicitly:

```bash
dotnet ef database update
```

### Example Migration

```csharp
public partial class AddUserProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UserProfiles",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(nullable: false),
                AvatarPath = table.Column<string>(nullable: true),
                IsActive = table.Column<bool>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserProfiles", x => x.Id);
            });
    }
    
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "UserProfiles");
    }
}
```

## Best Practices

### 1. Always Use Async

```csharp
// Good
var shot = await _context.ShotRecords.FirstOrDefaultAsync(s => s.Id == id);

// Avoid
var shot = _context.ShotRecords.FirstOrDefault(s => s.Id == id);
```

### 2. Include Related Data

Use `Include()` to load navigation properties:

```csharp
var shot = await _context.ShotRecords
    .Include(s => s.Bean)
    .Include(s => s.MadeBy)
    .FirstOrDefaultAsync(s => s.Id == id);
```

### 3. Project to DTOs

Don't expose entities to the UI layer:

```csharp
// Good
public async Task<ShotDto> GetShotAsync(int id)
{
    var shot = await _context.ShotRecords.FindAsync(id);
    return MapToDto(shot);
}

// Avoid
public async Task<ShotRecord> GetShotAsync(int id)
{
    return await _context.ShotRecords.FindAsync(id);
}
```

### 4. Handle Nulls

Always check for null when querying by ID:

```csharp
var shot = await _context.ShotRecords.FindAsync(id);
if (shot == null)
    throw new NotFoundException($"Shot {id} not found");
```

### 5. Use Transactions for Complex Operations

```csharp
using var transaction = await _context.Database.BeginTransactionAsync();
try
{
    // Multiple operations
    _context.Beans.Add(bean);
    await _context.SaveChangesAsync();
    
    var shot = new ShotRecord { BeanId = bean.Id };
    _context.ShotRecords.Add(shot);
    await _context.SaveChangesAsync();
    
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### 6. Optimize Queries

```csharp
// Good: Single query with projection
var shots = await _context.ShotRecords
    .Where(s => s.BeanId == beanId)
    .Select(s => new ShotDto 
    { 
        Id = s.Id, 
        BeanName = s.Bean.Name,
        // ...
    })
    .ToListAsync();

// Avoid: N+1 queries
var shots = await _context.ShotRecords
    .Where(s => s.BeanId == beanId)
    .ToListAsync();
foreach (var shot in shots)
{
    var bean = await _context.Beans.FindAsync(shot.BeanId); // N+1!
}
```

### 7. Separate Read and Write Models

Consider using different DTOs for create, update, and read operations:

```csharp
public record CreateShotRequest(
    int? BeanId,
    double Dose,
    double GrindSetting,
    // ...
);

public record UpdateShotRequest(
    double? Dose,
    int? Rating,
    string? Notes
    // Partial updates
);

public record ShotDto
{
    // Full read model with computed properties
}
```

## Troubleshooting

### Database Locked

SQLite doesn't support concurrent writes well. Use a singleton DbContext and ensure all operations are async:

```csharp
builder.Services.AddSingleton<BaristasDbContext>();
```

### Migration Not Applied

Delete the database file and restart the app to recreate from scratch:

```bash
# iOS Simulator
rm ~/Library/Developer/CoreSimulator/Devices/*/data/Containers/Data/Application/*/Library/baristas.db

# Android Emulator
adb shell
cd /data/data/com.yourcompany.baristanotes/files
rm baristas.db
```

### Slow Queries

Add indexes to frequently queried columns:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<ShotRecord>()
        .HasIndex(s => s.BeanId);
        
    modelBuilder.Entity<ShotRecord>()
        .HasIndex(s => s.LoggedAt);
}
```

## Additional Resources

- [Entity Framework Core Documentation](https://learn.microsoft.com/ef/core/)
- [SQLite Provider](https://learn.microsoft.com/ef/core/providers/sqlite/)
- [EF Core Performance Tips](https://learn.microsoft.com/ef/core/performance/)
