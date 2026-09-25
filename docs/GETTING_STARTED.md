# Getting Started

This guide sets up BaristaNotes for local development.

## Prerequisites

Install:

1. .NET SDK `11.0.100-preview.7.26381.103` or a compatible .NET 11 SDK.
2. The .NET MAUI workloads for that SDK.
3. Git.
4. An editor:
   - Visual Studio 2022 on Windows, or
   - Visual Studio Code with C# Dev Kit and the .NET MAUI extension.

Platform tools:

- iOS and Mac Catalyst require macOS and the Xcode version supported by the
  installed MAUI workload.
- Android requires the Android SDK, API level 24 or later, and a compatible
  JDK.
- Windows requires Windows 10 version 1809 or later. Development tools usually
  require a newer supported Windows SDK.

Check the installed SDK:

```bash
dotnet --version
dotnet workload list
```

## Clone and Restore

```bash
git clone https://github.com/davidortinau/BaristaNotes.git
cd BaristaNotes

dotnet workload restore src/BaristaNotes/BaristaNotes.csproj
dotnet restore src/BaristaNotes.sln
```

The solution and projects are under `src/`. Commands that omit this path will
not find the solution from the repository root.

## Build

Choose one target framework:

```bash
# iOS
dotnet build src/BaristaNotes -f net11.0-ios

# Android
dotnet build src/BaristaNotes -f net11.0-android

# Mac Catalyst
dotnet build src/BaristaNotes -f net11.0-maccatalyst

# Windows
dotnet build src/BaristaNotes -f net11.0-windows10.0.19041.0
```

## Run and Inspect the App

The Debug configuration includes MAUI DevFlow. Use it for application logs,
visual-tree inspection, screenshots, and UI interaction.

In the first terminal, run the application:

```bash
dotnet build src/BaristaNotes -t:Run -f net11.0-ios
```

In a second terminal, wait for the agent and inspect the UI:

```bash
maui devflow wait
maui devflow device app-info
maui devflow ui tree --depth 3 --fields "id,type,text,automationId"
maui devflow logs --limit 200
```

Before starting a simulator, check for an existing target:

```bash
xcrun simctl list devices booted
maui devflow broker status
```

Use the selected target for the complete test. Do not create application state
by editing SQLite directly. Use the app forms or supported voice commands.

## Run Tests

```bash
# All tests
dotnet test src/BaristaNotes.Tests

# One test class or name fragment
dotnet test src/BaristaNotes.Tests \
  --filter "FullyQualifiedName~ShotService"

# Coverage
dotnet test src/BaristaNotes.Tests \
  --collect:"XPlat Code Coverage"
```

Tests use xUnit, Moq, and SQLite in-memory databases. They reference
`BaristaNotes.Core`; they do not need a MAUI target framework.

## Configure Optional AI Features

Create a local development file for each platform that you run.

For iOS and Mac Catalyst:

```text
src/BaristaNotes/appsettings.Development.json
```

For Android:

```text
src/BaristaNotes/Platforms/Android/Assets/appsettings.Development.json
```

Use this structure:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "ApiKey": "YOUR-LOCAL-DEVELOPMENT-KEY"
  }
}
```

The repository ignores all `appsettings.Development.json` files. Confirm this
before you add a real key:

```bash
git check-ignore src/BaristaNotes/appsettings.Development.json
git status --short
```

If an API key enters Git history, revoke it immediately. Removing the text from
a later commit does not make the exposed key safe.

Do not put an Azure OpenAI key in a production mobile package. Use an
authenticated backend as the API boundary. The backend keeps the key and sends
only the result to the app.

## Database

The app creates and upgrades:

```text
FileSystem.AppDataDirectory/barista_notes.db
```

`DatabaseInitializer`:

- creates the initial schema when no application tables exist;
- applies known data-preserving schema steps;
- records migration identifiers;
- repairs supported legacy relationships; and
- validates the final schema.

If initialization fails, read the application logs. Do not delete the database,
clear application data, or uninstall the app. These actions can permanently
remove user records. Back up the database before any manual diagnostic action.

See [Data Layer](DATA_LAYER.md) for schema development rules.

## Logging

Application services use `Microsoft.Extensions.Logging` and structured message
templates.

```csharp
_logger.LogInformation(
    "Saved shot {ShotId} for bag {BagId}",
    shotId,
    bagId);
```

Use DevFlow for current application logs:

```bash
maui devflow logs --source native --limit 300
```

Do not add `Debug.WriteLine` or `Console.WriteLine` to services. A bootstrap
message before dependency injection is available is the only current
`Console.WriteLine` exception.

## iOS NativeAOT Release

Use the command in the
[README NativeAOT section](../README.md#ios-nativeaot-release).

The published app is under:

```text
src/BaristaNotes/bin/Release/net11.0-ios/ios-arm64/publish/
```

Check signing before installation:

```bash
codesign --verify --deep --strict \
  src/BaristaNotes/bin/Release/net11.0-ios/ios-arm64/BaristaNotes.app
```

Install over the existing application:

```bash
xcrun devicectl device install app \
  --device <device-id> \
  src/BaristaNotes/bin/Release/net11.0-ios/ios-arm64/BaristaNotes.app
```

Do not uninstall first. An uninstall deletes the application sandbox.

## Troubleshooting

### The SDK cannot find the project

Run commands from the repository root and include the `src/` path shown in this
guide.

### A MAUI workload is missing

```bash
dotnet workload restore src/BaristaNotes/BaristaNotes.csproj
```

Use a workload version that matches the installed SDK.

### DevFlow does not connect

Confirm that the app uses a Debug build, that the app is running, and that the
broker is active:

```bash
maui devflow broker status
maui devflow list
```

The DevFlow package is not included in Release builds.

### AI features are unavailable

Check that:

- the development file is in the correct platform location;
- the section name is `AzureOpenAI`;
- both `Endpoint` and `ApiKey` have values; and
- the application uses a Debug build when it must load the development file.

An older `OpenAI` section is not read. Rename it to `AzureOpenAI` and add the
resource endpoint.

## Next Documents

- [Project Structure](PROJECT_STRUCTURE.md)
- [Data Layer](DATA_LAYER.md)
- [Service Architecture](SERVICES.md)
- [MauiReactor Patterns](MAUIREACTOR_PATTERNS.md)
- [Contributing](CONTRIBUTING.md)
