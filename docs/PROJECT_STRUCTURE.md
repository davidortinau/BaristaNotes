# Project Structure

BaristaNotes retains its MAUI app and adds native UIKit and Android Views
variations. The apps share one Core library and test project. Native parity is
under development; the MAUI app remains the feature and design reference.
All projects are under `src/`.

```text
BaristaNotes/
  src/
    BaristaNotes/          # .NET MAUI application
    BaristaNotes.Core/     # Domain, data, and service layer
    BaristaNotes.iOS/      # C# UIKit application
    BaristaNotes.Android/  # C# Android Views application
    BaristaNotes.iOS.Ailoha/      # Debug-only Swift agent binding
    BaristaNotes.Android.Ailoha/  # Debug-only Android agent binding
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
  Services/                # MAUI media, speech, theme and overlay adapters
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
- `GrindTranslationChip`;
- `RatingDisplayComponent`; and
- `ShotRecordCard`.

Theme definitions are under `Resources/Styles/`. UI code should use
`ThemeKeys`, `AppColors`, `AppFontSizes`, `AppSpacing`, and `AppIcons`.

## Native Applications

The native heads own platform controls, layout, navigation, lifecycle, and
device-service adapters. They consume Core workflows without a MAUI Controls
dependency and install with separate app IDs and storage.

The Ailoha binding projects consume official native binaries for Debug
inspection only. They are excluded from solution Release builds and are not
standalone published packages. See each head's README for the exact SDK,
native-input, and build requirements. These projects are not a replacement
cross-platform UI framework.

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
  Hosting/                 # Shared IServiceCollection registrations
  Models/
    Enums/
  Services/
    DTOs/
    Exceptions/
    Grind/
    Recipes/
    Voice/                 # Shared command engine, tools and generated dispatch
    Workflows/             # Shared drafts, operations, selector state and text
    DatabaseInitializationService.cs
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

`DrinkValueRangeFormatting`, `ShotFilterCriteria`, database initialization and
the disabled AI recipe generator have moved out of the MAUI head into Core.
The retained MAUI pages and native heads use the same shared implementation.

`Services/Workflows` also contains `RangeEditorDraft`, `NumericPickerState`,
`GrindPickerState`, `GrindPickerWorkflow`, `EquipmentDraft`, and
`EquipmentWorkflow`, plus `ProfileDraft` and `ProfileWorkflow`. These types
contain no platform views. The platform heads
own selection presentation, navigation, confirmation and feedback; the shared
types preserve the source's values, parsing, persistence and notification rules.
`BeanDraft`/`BeanWorkflow` and `BagDraft`/`BagWorkflow` share detail-form mapping
and saves while leaving navigation, immediate bag-status UI and confirmation
presentation in each head.

AI advice, vision analysis, and the chat-backed grind provider now live in Core. Their shared
registration is opt-in with `AddBaristaNotesAI`; platform hosts still supply
configuration and any on-device chat client. The MAUI advice popup and voice
UI remain in the MAUI head.

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

Tests use xUnit, Moq, and isolated in-memory/file-backed SQLite databases.
Integration tests cover initialization, persisted updates, shared workflows,
and concurrent-reader write contention.
Host tests also compile selected platform-independent native source files
directly. They cover animation cleanup and voice callback ordering without
starting Android, plus Add Coffee completion-error feedback before owner
release. The iOS speech preflight tests preserve the source permission status
table without reading or changing OS permissions. These tests do not replace
native input, audio, or UI checks.

## Generated Source

Two generated areas are intentionally checked in:

- `BaristaNotes.Core/Data/CompiledModels/` contains EF compiled models,
  unsafe accessors, and precompiled query interceptors.
- `BaristaNotes.Core/Services/Voice/Generated/VoiceTools.g.cs` contains the NativeAOT
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
