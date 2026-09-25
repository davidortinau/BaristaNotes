# Project Structure

BaristaNotes has one MAUI app, one shared core library, and one test project.
All projects are under `src/`.

```text
BaristaNotes/
  src/
    BaristaNotes/          # .NET MAUI application
    BaristaNotes.Core/     # Domain, data, and service layer
    BaristaNotes.Tests/    # xUnit tests
    BaristaNotes.sln
  docs/                    # Current developer documentation
  specs/                   # Historical feature specifications
  .specify/                # Project governance and templates
```

## MAUI Application

`src/BaristaNotes` contains the application host and all MAUI-specific code.

```text
BaristaNotes/
  Components/              # Shared MauiReactor components
  Hosting/                 # MauiAppBuilder registration extensions
  Integrations/            # External service and popup adapters
  Pages/                   # Application screens
  Platforms/               # Android, iOS, Mac Catalyst, and Windows code
  Resources/
    AppIcon/
    Fonts/
    Images/
    Raw/
    Splash/
    Styles/                # Theme keys, colors, spacing, icons, and styles
  Services/
    AI/                    # Tool functions and checked-in generated tool code
  App.cs                   # Root MauiReactor component
  AppShell.cs              # Main Shell and root routes
  MauiProgram.cs           # Application composition root
  BaristaNotes.csproj
```

### Application Composition

`MauiProgram.CreateMauiApp()` composes the application through focused
extensions:

| Extension | Responsibility |
|---|---|
| `ConfigureBaristaApp` | MauiReactor, themes, resources, fonts, handlers, and popup support |
| `AddAppConfiguration` | Platform JSON configuration and Debug overrides |
| `AddDataAccess` | EF context, repositories, initialization, and preference store |
| `AddDomainServices` | Drink, bean, bag, equipment, profile, rating, recipe, range, theme, and feedback services |
| `AddRecipeSourcing` | Roaster recipe adapters and recipe sourcing |
| `AddImageServices` | Media picker, image processing, and vision |
| `AddVoiceServices` | Speech recognition, navigation tools, and voice overlay |
| `AddAIChatClients` | Local Apple Intelligence when supported, Azure OpenAI services, advice, and grind translation |
| `AddDebugDiagnostics` | Debug logging, DevFlow, and other Debug-only tools |

Route registration is in `Hosting/RouteRegistration.cs`.

### Main Pages

The root Shell exposes three destinations:

- `ShotLoggingGridPage`: create or edit a drink.
- `ActivityFeedPage`: browse and filter history.
- `SettingsPage`: manage preferences and app data.

Registered detail and management routes include:

- bean, bag, equipment, and profile pages;
- `ValueRangeSettingsPage`; and
- `ValueRangeEditorPage`.

### Shared Components

Important shared components include:

- `AdaptiveTwoLineTile` for aligned, adaptive settings and management rows;
- `FormFields` for consistent form controls;
- `CircularAvatar` and `ProfileImagePicker`;
- `DrinkValueRangeFormatting`;
- `GrindTranslationChip`;
- `RatingDisplayComponent`; and
- `ShotRecordCard`.

Theme definitions are under `Resources/Styles/`. UI code should use
`ThemeKeys`, `AppColors`, `AppFontSizes`, `AppSpacing`, and `AppIcons`.

## Core Library

`src/BaristaNotes.Core` targets plain `net11.0`. It does not depend on a MAUI
target framework.

```text
BaristaNotes.Core/
  Data/
    CompiledModels/        # Checked-in EF NativeAOT model and interceptors
    Repositories/          # Repository interfaces and implementations
    BaristaNotesContext.cs
    BaristaNotesContextFactory.cs
    DatabaseInitializer.cs
  Migrations/              # EF migration history and model snapshot
  Models/
    Enums/
  Services/
    DTOs/
    Exceptions/
    Grind/
    Recipes/
  BaristaNotes.Core.csproj
```

### Domain Models

The current EF model contains:

- `Bean`
- `Bag`
- `Equipment`
- `UserProfile`
- `ShotRecord`
- `ShotEquipment`
- `Recipe`
- `GrinderProfile`
- `GrindTranslationCache`

`BrewMethodValueRangeCatalog` contains the automatic input ranges and hard
limits for every supported brew method.

### Data Access

`BaristaNotesContext` is registered as a scoped EF context. Scoped
repositories and domain services use it. `DatabaseInitializer` creates or
upgrades the local SQLite schema without deleting existing data.

NativeAOT builds use the generated code under `Data/CompiledModels`. These
files are source artifacts and are intentionally committed.

## Test Project

`src/BaristaNotes.Tests` targets `net11.0`.

```text
BaristaNotes.Tests/
  Helpers/
  Integration/
  Mocks/
  TestInfrastructure/
  Unit/
  Utilities/
  BaristaNotes.Tests.csproj
```

Tests use xUnit, Moq, and SQLite in-memory databases. Integration tests cover
database initialization and data-preserving schema upgrades.

## Generated Source

Two generated areas are intentionally checked in:

- `BaristaNotes.Core/Data/CompiledModels/` contains EF compiled models,
  unsafe accessors, and precompiled query interceptors.
- `BaristaNotes/Services/AI/Generated/VoiceTools.g.cs` contains the NativeAOT
  compatible tool registration and invocation code.

Regenerate these files only with package and tool versions that match the
project. Review the complete generated diff and rerun the NativeAOT publish.

## Documentation Boundaries

- `docs/` contains current developer documentation.
- `docs/archive/` contains superseded implementation notes.
- `specs/` contains feature-specific requirements, plans, and task records.
  These records can name old frameworks because they describe work at the time.
- `.specify/memory/constitution.md` and
  `.specify/ARCHITECTURE_CONSTRAINTS.md` contain current project rules.

## Build Outputs

The repository ignores normal build and test outputs:

```text
bin/
obj/
TestResults/
*.binlog
publish/
```

Do not commit local configuration files, databases, logs, or API keys.
