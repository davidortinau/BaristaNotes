# Service Architecture

BaristaNotes separates UI, domain behavior, persistence, and platform
integration through dependency injection.

## Registration

`MauiProgram.CreateMauiApp()` calls focused registration extensions instead of
putting all registrations in one method.

| Extension | Services |
|---|---|
| `AddDataAccess` | EF context, database initializer, repositories, and preferences store |
| `AddDomainServices` | Shots, beans, bags, equipment, profiles, ratings, recipes, value ranges, feedback, and themes |
| `AddRecipeSourcing` | Roaster adapters, adapter registry, and recipe sourcing |
| `AddImageServices` | Media picker, image processing, and image analysis |
| `AddVoiceServices` | Speech recognition, data notifications, navigation tools, voice tools, and overlay |
| `AddAIChatClients` | AI advice, grind translation, and optional local chat client |

Registration files are under `src/BaristaNotes/Hosting/`.

## Lifetimes

| Lifetime | Current use |
|---|---|
| Scoped | EF context, repositories, data-backed domain services, voice command tools, recipe sourcing |
| Singleton | Preferences, value ranges, feedback, theme, image services, AI advice, speech recognition, navigation registry |
| Transient | Popup instances |

The EF context is scoped. It must not be changed to a singleton.

## Domain Services

Domain interfaces and most implementations are under
`src/BaristaNotes.Core/Services/`.

| Interface | Responsibility |
|---|---|
| `IShotService` | Create, update, query, filter, and enrich logged drinks |
| `IBeanService` | Manage beans, search names, list roasters and origins, and refresh recipes |
| `IBagService` | Manage physical bags and completion state |
| `IEquipmentService` | Manage machines, grinders, and accessories |
| `IUserProfileService` | Manage profiles and profile images |
| `IRatingService` | Calculate bean and bag rating aggregates |
| `IRecipeService` | Query, create, update, and import recipes |
| `IDrinkValueRangeService` | Resolve and persist Auto or Custom value ranges |
| `IPreferencesService` | Store application preferences |

Data-backed services are scoped because they use scoped repositories or
`BaristaNotesContext`.

## Value Range Service

`IDrinkValueRangeService` supports four metrics:

- dose;
- yield;
- grind size in microns; and
- time.

Each metric has an app-wide mode:

- `Auto` resolves the recommended range from
  `BrewMethodValueRangeCatalog`.
- `Custom` uses a saved range for the selected brew method.
- A method with no custom override uses the Auto fallback.

The service stores only user overrides. It does not copy the full default
catalog into Preferences. `SettingsChanged` lets active UI update after a
setting changes.

The drink logging page uses the effective range for controls and preserves an
existing historical value even when it is outside the preferred range.

## Data Access Services

Repositories are under `BaristaNotes.Core/Data/Repositories/`.

Current repositories cover:

- equipment;
- beans;
- bags;
- user profiles;
- shots;
- recipes;
- grinder profiles; and
- grind translation cache entries.

`DatabaseInitializer` is scoped because it uses the scoped EF context.
`DatabaseInitializationService` is a singleton coordinator that creates a
scope, runs initialization once, and shares the result with application
startup.

See [Data Layer](DATA_LAYER.md) for schema rules.

## Feedback and Theme Services

`IFeedbackService` wraps the application feedback system:

```csharp
await _feedbackService.ShowSuccessAsync("Shot saved");
await _feedbackService.ShowErrorAsync(
    "The shot could not be saved",
    "Check the values and try again");
```

It also exposes feedback and loading-state observables and supports haptic
feedback.

`IThemeService` stores and applies light, dark, or system theme mode. UI code
uses the shared theme keys and design constants.

## Image Services

| Interface | Responsibility |
|---|---|
| `IImagePickerService` | Select or capture images through MAUI media APIs |
| `IImageProcessingService` | Validate, downsample, save, and delete app images |
| `IVisionService` | Classify photos and extract bean or person information |

Library photo picks can contain full-resolution HEIC or JPEG data. The
processing service downsamples before size validation. UI code loads saved
absolute paths through streams and uses cache-busting when a file path is
reused.

## Voice and AI Services

The voice pipeline combines:

- MAUI speech-to-text;
- `VoiceCommandService`;
- source-defined navigation and data tools;
- `Microsoft.Extensions.AI` function invocation; and
- a window-level voice overlay.

`VoiceTools.g.cs` is checked in because its NativeAOT-safe schema and invocation
code must not depend on runtime reflection.

AI provider behavior:

1. Supported non-NativeAOT iOS builds register Apple Intelligence.
2. Services try the local client when it is available.
3. Services can fall back to Azure OpenAI.
4. NativeAOT builds exclude the Apple Intelligence package and local client.

Azure OpenAI configuration uses:

```text
AzureOpenAI:Endpoint
AzureOpenAI:ApiKey
```

Main model assignments:

- advice, voice commands, and grind translation: `gpt-4.1-mini`;
- image analysis: `gpt-4o`; and
- image field extraction: `gpt-4o-mini`.

`Microsoft.Extensions.AI` abstractions keep service code separate from the
provider client and make tests possible without a network request.

## Error Handling

Use the error contract already established by the service:

- `OperationResult<T>` for expected domain failures in services that use it;
- specific exceptions for unexpected or invalid operations;
- `IFeedbackService` for clear user-facing messages; and
- `ILogger<T>` for technical details.

Do not catch `Exception` only to return a successful result or an empty
collection. Log the failure and preserve the error signal.

## Logging

Services use constructor-injected `ILogger<T>` with structured templates:

```csharp
_logger.LogInformation(
    "Updated bag {BagId} with {ShotCount} shots",
    bagId,
    shotCount);
```

Do not use interpolated log messages, `Debug.WriteLine`, or
`Console.WriteLine` in services.

## Adding a Service

1. Search for an existing service or helper that owns the behavior.
2. Add or extend an interface in `BaristaNotes.Core` when the behavior is
   platform-independent.
3. Inject repositories or other interfaces through the constructor.
4. Register the service in the matching `Hosting/*Extensions.cs` file.
5. Choose a lifetime that is compatible with every dependency.
6. Add success, failure, and cancellation tests.
7. Use `ILogger<T>` and the established feedback or result contracts.

Do not resolve application services through a global service locator.
