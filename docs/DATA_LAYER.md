# Data Layer

Native Bag Detail uses `IBagService.GetShotCountAsync` instead of counting an
unloaded `Bag.ShotRecords` navigation collection. The query counts all associated
rows, as the source detail page does when its collection is loaded. Rating and
active-bag summaries keep their separate soft-delete filtering. This adds no
schema, cascade, or eager-load change to ordinary bag reads.

Checked-in EF query interceptors contain source-file checksums. A repository
query edit requires regeneration with the matching EF tools, not manual edits
to encoded locations. Generate outside the source tree, then install the
affected generated file and run a normal build with all interceptors enabled.
Keep any bootstrap exclusions local to the generation command; do not leave
them in the project. Preserve the generated files' nullable annotation context
and review unchanged SQL as well as the new query.

BaristaNotes uses Entity Framework Core with SQLite. The application keeps all
current user data on the device.

## Data Flow

```text
MauiReactor page
    |
    v
Domain service
    |
    v
Repository or BaristaNotesContext
    |
    v
SQLite: barista_notes.db
```

Pages use injected services. They do not create database connections or change
SQLite tables directly.

## Database Location

`Hosting/DataAccessExtensions.cs` creates the database path:

```csharp
var dbPath = Path.Combine(
    FileSystem.AppDataDirectory,
    "barista_notes.db");
```

The full platform path can change when an application is installed again. Code
must use `FileSystem.AppDataDirectory`; it must not depend on a copied absolute
path.

## EF Context and Lifetime

The context type is `BaristaNotesContext`.

```csharp
builder.Services.AddDbContext<BaristaNotesContext>(options =>
    options
        .UseModel(BaristaNotesContextModel.Instance)
        .UseSqlite($"Data Source={dbPath}"));
```

`AddDbContext` registers a scoped context. Repositories and domain services that
use it are also scoped. Do not register the context as a singleton.

## Current Model

| Entity | Purpose |
|---|---|
| `Bean` | Bean identity, roaster, origin, notes, and roaster URL |
| `Bag` | A physical bag with a bean, roast date, notes, and completion state |
| `Equipment` | Machines, grinders, and accessories |
| `UserProfile` | People, avatars, and optional AI context |
| `ShotRecord` | Logged drink inputs, outputs, rating, method, and tasting notes |
| `ShotEquipment` | Additional equipment linked to a shot |
| `Recipe` | A recipe for a bean and brew method |
| `GrinderProfile` | Grinder adjustment profile |
| `GrindTranslationCache` | Cached translations from grinder settings to microns |

Important relationships:

- A bean has many bags.
- A bag has many shot records.
- A shot has one required bag.
- A shot can reference a machine, grinder, maker, and recipient.
- A shot can have additional equipment through `ShotEquipment`.
- A bean can have recipes for different brew methods.

Most user-managed records use soft-delete and synchronization metadata such as
`SyncId`, `LastModifiedAt`, and `IsDeleted`.

## Drink Values

`ShotRecord` stores canonical values:

- `DoseIn`
- `ActualOutput`
- `GrindMicrons`
- `ActualTime`
- `WaterTempC`
- `BrewMethod`
- `DrinkType`
- `Rating`
- `TastingNotes`

Expected values and method-specific parameters are stored separately on the
record when applicable.

The custom input-range feature does not add database columns.
`DrinkValueRangeService` stores its versioned, sparse override document through
the MAUI Preferences API. Auto values and hard limits come from
`BrewMethodValueRangeCatalog`.

## Schema Initialization and Upgrades

The app does not call `Database.EnsureCreated()` or `Database.Migrate()` during
normal startup.

`DatabaseInitializationService` runs `DatabaseInitializer` once. The
initializer:

1. opens the configured EF connection;
2. creates `__EFMigrationsHistory` when needed;
3. creates the initial application schema only when no application tables
   exist;
4. checks and applies each known schema step;
5. records the associated migration identifier;
6. repairs supported legacy relationships; and
7. validates the final schema.

The checked-in EF migration files remain the schema history and design-time
model source. The initializer contains equivalent static SQL for app startup.
This explicit startup path is used because NativeAOT cannot safely use all
dynamic EF migration paths.

An unknown partial schema causes a visible initialization error. The app does
not replace it with an empty database.

## NativeAOT Data Access

`BaristaNotes.Core/Data/CompiledModels/` contains:

- the compiled EF model;
- generated entity metadata;
- unsafe accessors; and
- precompiled query interceptors.

`DataAccessExtensions` supplies `BaristaNotesContextModel.Instance` through
`UseModel`. The checked-in model, interceptors, EF packages, and entity model
must remain synchronized.

After an EF model or query change:

1. use an EF tool version that matches the project packages;
2. update the EF migration history if the database schema changes;
3. update the equivalent `DatabaseInitializer` step only for a schema change;
4. regenerate the NativeAOT compiled model and precompiled queries;
5. review all generated changes;
6. run database integration tests; and
7. publish the iOS NativeAOT app and review all trim and AOT warnings.

Do not hand-edit generated EF logic except for a documented tool-output
compatibility correction that cannot be generated correctly.

The checked-in `Create` methods for `EquipmentEntityType`,
`GrindTranslationCacheEntityType`, `RecipeEntityType`, and `ShotRecordEntityType`
retain narrow `IL3050` suppressions for their concrete, statically rooted enum
converters. The EF generator does not emit these existing compatibility
annotations. Preserve them when reviewing regenerated output; do not replace
them with project-wide warning suppression.

## SQLite Write Contention

The pinned `Microsoft.Data.Sqlite` 11 Preview 7 provider can report a successful
`RETURNING` write after SQLite has rolled it back under a concurrent reader.
Its reset retry calls `sqlite3_reset` again, but that does not retry the failed
statement. SQLite documents this case in its
[`sqlite3_reset` contract](https://www.sqlite.org/c3ref/reset.html).

`BaristaNotesContext` disables the SQL `RETURNING` clause for every mapped
table through EF's supported `UseSqlReturningClause(false)` setting. The
compiled model contains the same setting. This uses EF's insert/select and
affected-row query strategy; it does not change the database schema, journaling
mode, or the separate bean/bag write boundary. No migration or data reset is
needed. Inserts, updates, and deletes must all persist before reporting success.

`SqliteWriteContentionTests` holds a real read transaction in an isolated
file-backed database using the app's rollback-journal mode. It exercises both
the design-time and compiled models, then checks the persisted write through
a new context. Remove the opt-out only after a provider update passes these
tests and the native create/reload/restart flow.

## Adding a Schema Change

Create a normal EF migration from the repository root:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/BaristaNotes.Core \
  --startup-project src/BaristaNotes.Core \
  --context BaristaNotesContext
```

Then:

- inspect `Up`, `Down`, and the model snapshot;
- preserve all existing rows during transforms;
- add the corresponding idempotent startup step to `DatabaseInitializer`;
- update compiled NativeAOT artifacts; and
- add tests for a new database and each supported older schema.

Never modify an applied migration to change history silently.

## Data Preservation

Do not use any of these actions as a repair:

- delete `barista_notes.db`;
- clear application data;
- uninstall the application;
- drop user tables; or
- replace an unknown database with a new empty database.

Before a manual database diagnostic:

1. stop writes;
2. make a database backup, including WAL data when present;
3. reproduce the issue against the backup;
4. make the repair idempotent; and
5. add an integration test.

Physical devices contain user data and must be treated as production
environments.

## Query and Repository Rules

- Use async EF operations for database I/O.
- Use `AsNoTracking` for read-only queries.
- Project only the values that a caller needs.
- Apply pagination to long history queries.
- Keep write transactions short.
- Use the existing repositories before adding direct context access.
- Use parameterized values for SQL that must exist in
  `DatabaseInitializer`.
- Log failures with `ILogger<T>` and rethrow or return the established
  `OperationResult<T>` error.

## Tests

Data tests use SQLite in-memory connections so they exercise the SQLite
provider rather than the EF in-memory provider.

Run:

```bash
dotnet test src/BaristaNotes.Tests \
  --filter "FullyQualifiedName~DatabaseInitializer"
```

Tests for a schema change must cover:

- a new empty database;
- each affected legacy schema;
- row preservation;
- repeated initialization; and
- the expected failure for an unsupported partial schema.
