# Contributing to BaristaNotes

BaristaNotes is an educational .NET MAUI project. Contributions must keep the
application reliable, accessible, and useful as a current reference.

## Set Up the Repository

```bash
git clone https://github.com/davidortinau/BaristaNotes.git
cd BaristaNotes

dotnet workload restore src/BaristaNotes/BaristaNotes.csproj
dotnet restore src/BaristaNotes.sln
dotnet test src/BaristaNotes.Tests
```

See [Getting Started](GETTING_STARTED.md) for platform setup and run commands.

If you contribute through a fork, configure the main repository as `upstream`:

```bash
git remote add upstream \
  https://github.com/davidortinau/BaristaNotes.git
```

## Before You Change Code

1. Read `.specify/memory/constitution.md`.
2. Read `.specify/ARCHITECTURE_CONSTRAINTS.md`.
3. Search the current source for an existing service, component, style, or
   helper that owns the behavior.
4. Check the relevant feature record under `specs/` when one exists.
5. Preserve unrelated local changes in a dirty worktree.

Do not create a second implementation of an existing pattern.

## Branches and Commits

Use a short branch name that describes the change:

```text
feature/custom-ranges
fix/ios-startup
docs/current-onboarding
```

Use Conventional Commits:

```text
feat(settings): add custom drink ranges
fix(ios): harden NativeAOT release
docs: update developer setup
```

Stage explicit paths. Do not use `git add .` when the worktree contains
unrelated changes.

Do not commit generated build output, logs, databases, local configuration, or
secrets.

## Code Standards

- Target the frameworks declared in the project files. The current projects
  target .NET 11.
- Enable nullable reference types.
- Use file-scoped namespaces.
- Use records for immutable DTOs when appropriate.
- Add XML documentation to public contracts where it adds useful behavior or
  constraint information.
- Use async database and network APIs.
- Do not use `.Result` or `.Wait()`.
- Keep errors visible. Do not convert failures into successful empty results.

### Logging

Services and components use `Microsoft.Extensions.Logging`.

```csharp
_logger.LogInformation(
    "Saved shot {ShotId} for bag {BagId}",
    shotId,
    bagId);
```

Use named message-template values. Do not use interpolated log strings,
`Debug.WriteLine`, or `Console.WriteLine` in services.

## MauiReactor UI

- Build screens with MauiReactor C# components.
- Reuse `ThemeKeys`, `AppColors`, `AppFontSizes`, `AppSpacing`, and `AppIcons`.
- Use `AdaptiveTwoLineTile` for the shared two-line row pattern.
- Use shared form components before adding a page-specific control.
- Use Material Symbols or image assets, not emoji icons.
- Use `Border`, not `Frame`.
- Use `CollectionView`, not `ListView` or `TableView`.
- Keep touch targets at least 44 by 44 units.
- Support large text, narrow screens, tablets, landscape, keyboard focus, and
  screen readers.

See [MauiReactor Patterns](MAUIREACTOR_PATTERNS.md).

## Data Preservation

User data must survive development, upgrades, and deployment.

Do not:

- delete `barista_notes.db`;
- clear application data;
- uninstall the application as a reset;
- remove or rewrite applied migration history;
- drop a user table without an approved data-preserving transform; or
- create sample data through direct SQLite inserts.

Treat a physical device as a production environment. Back up data before any
manual database diagnostic.

For a schema change:

1. update the model and EF migration;
2. preserve existing rows in `Up` and provide a valid `Down`;
3. update the idempotent step in `DatabaseInitializer`;
4. regenerate the EF NativeAOT artifacts;
5. add database initialization tests; and
6. publish and run the NativeAOT app.

See [Data Layer](DATA_LAYER.md).

## Secrets and AI Configuration

Local AI configuration belongs in an ignored
`appsettings.Development.json` file.

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "ApiKey": "YOUR-LOCAL-DEVELOPMENT-KEY"
  }
}
```

Before a commit:

```bash
git status --short
git diff --cached
git check-ignore src/BaristaNotes/appsettings.Development.json
```

Never commit a real endpoint credential, token, certificate, connection
string, or private user data.

If a credential enters Git history:

1. revoke it immediately;
2. create a replacement;
3. remove the exposed value from the working tree; and
4. coordinate any required history cleanup.

A production mobile app must call an authenticated backend. It must not contain
or download the provider API key.

Tests must use mocked configuration and clients. They must not call a paid AI
endpoint unless the test is explicitly an authorized integration test.

## Tests

The project uses xUnit, Moq, and SQLite in-memory connections.

```bash
# Full test project
dotnet test src/BaristaNotes.Tests

# Targeted tests
dotnet test src/BaristaNotes.Tests \
  --filter "FullyQualifiedName~DrinkValueRange"
```

Add tests for:

- normal behavior;
- invalid input;
- boundary values;
- cancellation;
- service failures;
- repeated operations; and
- data preservation when a schema changes.

Do not use the EF in-memory provider as a substitute for SQLite behavior.

## Validation

Run the smallest checks that cover the change, then run the required
end-to-end path.

### Core or Service Change

```bash
dotnet test src/BaristaNotes.Tests \
  --filter "FullyQualifiedName~RelevantType"
dotnet test src/BaristaNotes.Tests
```

### MAUI UI Change

```bash
dotnet build src/BaristaNotes -f net11.0-ios
dotnet build src/BaristaNotes -t:Run -f net11.0-ios
maui devflow wait
```

Use DevFlow to inspect and exercise every changed UI state. A build and unit
tests are prerequisites, not UI verification.

### NativeAOT Change

Use the command in the
[README NativeAOT section](../README.md#ios-nativeaot-release).
Review all direct and dependency trim or AOT warnings before installation.

## Generated Files

The repository intentionally commits:

- EF compiled models and query interceptors under
  `BaristaNotes.Core/Data/CompiledModels`; and
- NativeAOT-safe AI tool code under `BaristaNotes/Services/AI/Generated`.

When source changes require regeneration:

- use tool versions that match the project;
- remove stale generated inputs when the generator requires it;
- review the complete generated diff; and
- rerun tests and NativeAOT publishing.

Do not hide generator warnings globally to make a release appear clean.

## Pull Requests

A pull request must include:

- a clear summary and reason;
- the meaningful implementation choices;
- tests and end-to-end checks that ran;
- screenshots or recordings for UI changes;
- data migration details when applicable;
- accepted warnings or known limitations; and
- a statement that no secrets or user data are included.

Before push:

1. inspect the staged diff;
2. run a high-signal code review of the staged change set;
3. fix all confirmed findings;
4. confirm unrelated files are not staged; and
5. push without rewriting shared history.

## Documentation

Update current documentation when behavior, setup, architecture, or developer
workflow changes.

- Update files under `docs/` for current behavior.
- Do not rewrite historical records under `specs/` or `docs/archive/` to make
  them look current.
- Mark a historical document as superseded when its status is unclear.
- Test commands and links before submission.
