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

MAUI registration wrappers are under `src/BaristaNotes/Hosting/`. Shared data,
domain, and recipe registrations now live in
`src/BaristaNotes.Core/Hosting/ServiceCollectionExtensions.cs`. The native heads
register platform `IPreferencesStore` and `IImageProcessingService` adapters,
then call `AddBaristaNotesCore(databasePath)`. No native head references the
MAUI application or `Microsoft.Maui.Controls`.

## Lifetimes

| Lifetime | Current use |
|---|---|
| Scoped | EF context, repositories, data-backed domain services, voice command tools, recipe sourcing |
| Singleton | Preferences, value ranges, feedback, theme, image services, AI advice, speech recognition, navigation registry |
| Transient | Popup instances |

The EF context is scoped. It must not be changed to a singleton.

`AddBaristaNotesAI()` is an opt-in shared registration for `IAIAdviceService`,
`IGrindTranslationAI`, and `IVisionService`. The head supplies `IConfiguration` and can register
a platform `IChatClient`. The manual native composition does not enable AI
implicitly. The advice service retains its session-level local-provider state
but reads each shot/bean context in a fresh scope instead of capturing a
scoped data service in a singleton.
Bean recommendation context uses the existing eager-loading bean-history query
so it does not depend on earlier EF tracking to populate bags or equipment.
All matching shots are loaded before the existing rating-first top-ten ranking;
the latest shot still supplies the equipment names.

## Shared Application Workflows

`BaristaNotes.Core/Services/Workflows/` contains app logic used by both native
heads and the retained MAUI pages:

| Type | Responsibility |
|---|---|
| `DrinkDraft` / `DrinkLoadResult` | Editable values and loaded reference data without native control types |
| `DrinkWorkflow` | Load new/edit state, apply effective method defaults, save, and remember selections |
| `BeanCreationWorkflow` | Create a bean and initial bag, preserving separate write outcomes and notifications |
| `AddCoffeeDraft` / `AddCoffeeWorkflow` | Photo/Browse coffee creation, trimmed fields, explicit roast date and bag notes without extra global notifications |
| `PhotoWorkflowRules` | Source photo intent choice and coffee-extraction/prefill decisions |
| `BeanDraft` / `BeanWorkflow` | Bean form mapping, URL normalization, explicit optional-field clearing and update/delete notifications |
| `BagDraft` / `BagWorkflow` | Bag form validation, create/update mapping and source notification payloads |
| `MassPickerState` | Whole/tenth selection, preferred/full range, staged value and unchanged Done semantics |
| `NumericPickerState` | Time/temperature lists, exact current-value inclusion, range toggles and staged Done semantics |
| `GrindPickerState` / `GrindPickerWorkflow` | Variable-step micron lists, current/history/default selection and configured/seeded grinder anchors |
| `EquipmentDraft` / `EquipmentWorkflow` | Equipment form values, blank-name check, explicit note clearing, save/archive and source notifications |
| `ProfileDraft` / `ProfileWorkflow` | Profile details, staged-photo save boundary, partial failure and source notifications |
| `RangeEditorDraft` | Range text, dirty state, source validation, recommended-value staging and canonical precision |
| `DrinkDisplay` / `DrinkValueRangeFormatting` | Shared value, unit, rating, timestamp and range text |

Native controls own measurement, scrolling, focus, view reuse and UI-thread
updates. They do not duplicate these workflows or their arithmetic.

Grind selection is distinct from time and mass selection: the current micron
value stays in the list even outside the allowed domain. Full-range rows use
50-micron steps outside the preferred interval. Opening the selector uses the
current value, then the bean/method history, then the effective default.
Configured grinder anchors and the existing DF64 seed refresh remain shared.
`DrinkDisplay.GrindBadge` formats the live native dial-setting label; each head
owns the badge actions and navigation.

Equipment archive remains separate from deletion. The source form's DELETE
action confirms an archive, which hides equipment from active lists without
deleting its row. `EquipmentWorkflow` retains that operation and the source
notification payloads; platform UI owns the confirmation, feedback and return.

Bean creation still uses `BeanCreationWorkflow` so its initial-bag result
remains separate. Bag completion/reactivation still persists immediately
through `IBagService`, independently of the form Save action. Confirmation
wording and the existing delete behavior remain platform-owned source behavior.

The Add Coffee modal uses a different source flow from manual Bean Detail.
Its notes belong to the bag, it trims form text, and its successful callback
can select the returned bag after dismissal. `AddCoffeeWorkflow` preserves
that distinction; the manual initial-bag flow does not auto-select.

Profile details and a staged photo are separate saves. The draft receives its
saved profile ID before photo work, so a failed photo save leaves an editable
profile rather than retrying creation. Failed photos retain staged bytes;
successful photo saves clear them. The workflow does not change the existing
image validation or replacement order. Platform UI still owns photo selection,
immediate existing-profile image changes, partial-success messages and navigation.

`ShotFilterCriteria` is now in Core's `Services/DTOs/` namespace. A filter modal
edits a clone and applies it explicitly. Dismissing the modal does not apply
its working copy.

`NavigationRegistry` also lives in Core. It retains the source route
descriptions, ordering and voice aliases. Platform heads still perform actual
navigation; sharing the registry does not add a missing native route or
implement the voice UI.

The retained MAUI filter updates chip selection and colors in place. It does
not rebuild the popup host during a tap. An explicit content rebuild detaches
the reused layouts first, so a native view is not attached to two parents.

For selected optional update fields, `FieldUpdate<T>` separates omission from
explicit assignment: default means unchanged, `Set(value)` stores the value,
and `Set(null)` clears an optional field. Required-field validation remains.

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

`RangeEditorDraft` loads a method's saved override even when its mode is Auto.
It parses values in the current culture and keeps unchanged canonical values
when minutes or hours have rounded display text. It does not save or remove
preferences. Each UI owns confirmation and navigation, then uses the shared
range service to persist an explicit Save or confirmed removal.

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

The coordinator is in `BaristaNotes.Core/Services/`. Failed initialization can
be retried; concurrent callers share the same active initialization task.

See [Data Layer](DATA_LAYER.md) for schema rules.

## Feedback and Theme Services

Theme mode values and the `AppThemeMode` preference format live in Core's
`ThemeMode` and `ThemePreference`. Each head still owns effective system-theme
resolution, platform appearance, resource updates and native view refresh.
The retained MAUI theme service uses the same preference helper.

`IFeedbackService` wraps the application feedback system:

```csharp
await _feedbackService.ShowSuccessAsync("Shot saved");
await _feedbackService.ShowErrorAsync(
    "The shot could not be saved",
    "Check the values and try again");
```

It also exposes feedback and loading-state observables and supports haptic
feedback.

MAUI toast presentation awaits `PushAsync(..., waitUntilClosed: false)` so its
two-second hold starts after appearance. It then closes that specific toast,
not the topmost unrelated popup. Native presenters reproduce this approved
lifecycle correction while retaining the source feedback design.

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

`VoiceCommandService`, its tool classes, and the checked-in `VoiceTools.g.cs`
now live in `BaristaNotes.Core/Services/Voice`. Their existing namespaces remain
unchanged to preserve the generated type references and tool schema identity.
The generated file is byte-identical to its source version.

`AddBaristaNotesVoice()` registers the scoped engine and tools. A head supplies
`IVoicePlatformActions` for queued navigation, camera capture and browser launch;
the MAUI head uses `MauiVoicePlatformActions`. Speech recognition and window
overlays remain platform-owned. This shared registration does not create those
native controls or request microphone/camera permission.

The Attributes generator is disabled for this checked-in tool context, as in
the original MAUI project. Core also pins the original application's
`NuGet.CommandLine` 7.9.0 override because the recognizer otherwise resolves
the older 5.11.5 transitive build tool. Neither adds a Controls dependency.

AI provider behavior:

1. Supported non-NativeAOT iOS builds register Apple Intelligence.
2. Advice and grind services try the local client when it is available.
3. Services can fall back to Azure OpenAI.
4. NativeAOT builds exclude the Apple Intelligence package and local client.

The source voice-command engine explicitly disables its local-client branch.
The extraction preserves that setting: voice tool calling currently requires
Azure configuration. It does not silently enable Apple voice tool calls.

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
