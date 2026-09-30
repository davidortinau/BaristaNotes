# Native Android first slice

This is a C# .NET 11 Android Views application, not a MAUI application.
Its package is `com.simplyprofound.baristanotes.nativeapp`, with its own sandbox.
The minimum supported Android version remains API 24.

The temporary proof screen has been replaced with the bounded manual-slice UI:
the drink grid, custom action row, manual bean form, bag/method/drink/rating
button lists, whole/tenth mass lists, Activity and the custom Filter Shots
overlay. The empty People tile now opens the source-supported name/context
profile form. Settings → Profiles → Add also reaches that form, so profiles
can be added after the first. The split BY/FOR RecyclerViews update `DrinkDraft` immediately;
both Close and Done retain selection. Native views reuse cells and update
existing controls.

The head consumes Core's shared registration, DrinkDraft/DrinkWorkflow,
BeanCreationWorkflow, EquipmentWorkflow, MassPickerState, NumericPickerState,
GrindPickerState/GrindPickerWorkflow, DrinkDisplay and ShotFilterCriteria.
Native preferences and image adapters satisfy the platform contracts; no
domain DTO, validation, numeric arithmetic or filter model is duplicated.

All selector and Activity lists use AndroidX RecyclerView, matching the pinned
MAUI CollectionView backend. Categorical rows retain wrapping and the selected
trailing dot; centering uses the actual measured row, including first/last
rows. Filter entry/exit uses the source bottom translation (300ms CubicOut,
400ms CubicIn), retaining input blocking until exit and result completion.

Successful bean creation leaves the ADD form before the reference-list read.
If that read fails, the returned editor reports the failure and BAG exposes a
read-only retry. It never retries creation or caches a returned-but-unread bag.

## Isolated UI fixture

For controlled UI failure/scroll checks only, build Debug with
`-p:NativeUiFixture=true` and its own artifacts directory. This uses package
`com.simplyprofound.baristanotes.nativefixture`, separate from the normal app.
It exposes a clearly labeled fixture-only route to the existing Bean Detail
form and a one-shot failure of the next bag-list read. Data is still created
through the real forms and Core workflows, never direct seeding.
Fixture controls are compiled out of ordinary Debug and all Release builds.
Passing the fixture property in Release fails explicitly.
Do not create profile fixtures through direct service calls or add broader
product screens just for fixture generation.

This is **not** feature/design parity. General camera/photo workflows and AI
configuration are implemented offline below. Live AI and grinder calibration
remain unverified or outside the implemented workstreams. No unavailable path
reports fake success. Photo adapter behavior has not been exercised on a device.

Profile creation now uses `ProfileDraft`/`ProfileWorkflow`, not a fixture seed.
The created identity is retained before notification/photo processing, so
staged-photo retry updates the same profile. Creation does not select the new
profile. Name/context errors remain inline; shared service validation enforces
the source create-name and context limits without input truncation.

The Settings prerequisite exposes the source MANAGE section and Profiles
entry. The profile list uses native recycled member rows and the source
four-action footer with inverted Add. Profile edits/photos/deletion are now
implemented offline as described below. Cancel and Android Back return
to the form's caller. A successful Add from the list refreshes that list;
failed reads expose Retry without reopening a successful creation form.
Recipient OR with bean/rating AND has now been exercised through real forms,
including the retained multi-bean sandbox with ordinary product code and no
fixture-menu controls.

## Value-range Settings

Normal Settings now opens Dose In, Yield, Grind Size and Time range pages.
Each uses the source Auto/Custom controls, per-method summaries, automatic
fallback labels and fixed Back action. Switching Auto retains saved overrides.
The 13-method source composition uses a bounded ScrollView, not an unbounded
data feed.

Editors use Core's `RangeEditorDraft` for text, canonical values, units, dirty
state and live validation. They do not reimplement parsing. Save uses
`IDrinkValueRangeService`; failures stay in the editor with an explicit error.
Settings change subscriptions belong to their visible native screen and are
removed when that screen closes. Returning to the drink leaves entered numbers
alone; a new-mode method change and newly opened mass selector resolve current
range settings through Core.

Dirty Cancel and Android Back use a native confirmation. Use Recommended
stages automatic values when no override exists; an existing override requires
the source removal confirmation. Reset All affects only the selected metric
and requires its own confirmation. UI tests must not confirm removal/reset
without permission for the named fixture overrides.

Grinder calibration remains outside the implemented workstreams. Appearance
switching is implemented offline below; runtime revalidation remains pending.
Corrupt-settings warning
UI uses the shared snapshot's `LoadWarning`; no hidden preference-corruption
fixture control is added to ordinary builds.

## Time and water temperature

Time and Water Temp now open single-column native RecyclerViews, not wheels or
sliders. They share the existing numeric row typography, measured centering and
half-viewport endpoint decoration. `NumericPickerState` owns selection, exact
maximum/off-step inclusion, preferred/full range state and unchanged Done.
Close discards staged values. Time opens ActualTime when present, otherwise
ExpectedTime; Done writes both together. New opens resolve current range settings.

Water Temp preserves the source caller's current/original semantics. Fahrenheit
uses 150–212 in steps of 1 with default 200; Celsius uses 65–100 in steps of 0.5
with default 93. Current converted values are included even off-step. Conversion
and duration labels come from `DrinkDisplay`. Canceling a null temperature
leaves it null; Done accepts the displayed/default value.

Normal Settings includes the source-shaped FAHRENHEIT/CELSIUS tiles, backed by
`IPreferencesService`. Returning to a drink refreshes the unit without replacing
its canonical Celsius value. The row does not add a second settings store.

Dedicated UI-created numeric fixtures have exercised cancellation, commit,
range toggling, converted off-step values, endpoint centering, custom Time
ranges, unit return, and real save/edit/restart persistence. Debug and Release
restore graphs contain no Controls/Reactor; Release excludes Ailoha. Runtime
evidence is separate from dependency evidence: this does not establish Release
workflow coverage, full accessibility/adaptive coverage, exact visual parity,
or final rebuild acceptance.

## Equipment and Grind (offline implementation)

Normal Settings → Equipment now has an active-item RecyclerView, loading,
empty and error/retry states, and a form with name, all five type chips, notes
and the source fixed action row. Machine/Grinder selectors open preset forms
when empty; creation refreshes references without selecting the new item.
Populated single selectors commit on tap and expose Clear. The accessory
multi-select branch preserves immediate working-draft updates and Close/Done/
Clear semantics, but the pinned drink grid has no rendered accessory entry.
No extra tile was invented to hide that source reachability gap.

Equipment validation, saves and archive notifications use the shared workflows.
DELETE means archive, not physical row deletion. The confirmation is a separate
centered fixed-dark SimpleAction composition with source horizontal SpringOut
entry and scale/left-exit sequence, not an alert or filter bottom sheet.
It blocks underlying input and disables its actions while animating.
Archive runs before success feedback, popup exit and navigation return.
Positive archive and clearing saved notes have not been exercised or authorized.

Grind uses its distinct shared state/workflow, not the mass/time clamp. It
retains exact current microns even outside the hard domain, variable outside
steps, preferred/full selection, row emphasis, centering and a sticky shared
badge. Existing microns precede bean/method history and the effective default.
The inherited DF64 seed refresh runs before loading configured anchors;
configured user anchors do not universally override seeds.

The no-grinder badge opens Grinder selection. The pinned uncalibrated
`equipmentDetail` route is unregistered and provides no calibration screen;
the native caller reports that route as unavailable instead of inventing one.
Shared load failures propagate into the source-equivalent logged fallback
picker; this is not a successful load or a new failure-state design.

Async result application is guarded by page/editor ownership and captured
input state. Cancel can leave an awaiting equipment form, but it cannot undo a
write already started, matching the source's nontransactional navigation.
Rows, event handlers, native lists and animations have explicit screen owners.

This group is implemented/build-checked only: emulator storage blocks
installation. Source/retained iOS regression evidence is not native Android
runtime evidence. New UI, popup motion/hit testing, persistence, error and
lifetime paths still require the documented finite replay after authorization.

The initial-bag persistence regression was traced to the pinned SQLite
provider's RETURNING/reset retry. The coordinator disabled RETURNING in the
shared model and regenerated it. Native UI revalidation confirmed a new
bean/today bag, explicit bag selection, real drink save/edit, populated
filtering and persistence after restart. Earlier data was preserved, not
repaired or reset. The temporary SQLite observer and narrow diagnostics
were removed after that verification.

The page grid now uses the source's full-window layout with edge overlap
consumed by native leaf containers, intrinsic auto rows, and the pinned MAUI
Android font/character-spacing mapping. Picker pages keep their inset host.
Feedback is a separate native top Toast presentation matching the source
UXDivers icon/title, dark rounded body, input blocking, fade/hold lifecycle and
error cooldown. Error/success feedback, populated edit/filter views and
restart persistence were checked. Exact visual acceptance and the remaining
adaptive/accessibility matrix are still pending; this is not full-app parity.

## Profiles and photos (offline implementation)

Profile management now supports list/loading/empty/error/retry, add/edit,
name and2000-character context counter, staged-image preview and partial-save
identity. Details and staged photos use `ProfileWorkflow`; failed image saves
retain the saved ID and staged bytes. Delete uses the same centered SimpleAction
family as Equipment with profile-specific content, not a second dialog design.

Saved-profile library changes/removal call the existing image service immediately
and are not coupled to form text Save/Cancel. They do not emit an extra global
ProfileUpdated event. The source selected-person DTO refresh limitation remains:
refreshing available profiles does not silently replace the cached maker/recipient
object. Reopening/reselecting can therefore matter after changes.

The native chooser uses the source-equivalent Photo Picker capability order:
API33+, API30+ R-extension2+, then an eligible system fallback picker. Only
unavailable-picker devices use GET_CONTENT. It buffers the selected URI with temporary access only,
attempts400/85 orientation-aware JPEG preprocessing, and closes owned streams.
No broad media/storage permission, persistent URI grant, camera or AI photo flow
was added. A staged-byte argument is available for a later source caller.

Form/list/People avatars load actual app-local images with generation guards and
explicit Bitmap disposal. The form preserves CircularAvatar120, margin8,
LightGray2dp stroke/0.3 fill and Gray person glyph. The staged card is160dp.
Native Bitmap decoding handles the eight source EXIF orientations and downsampling.

The shared image policy is unchanged: downsample before the12MiB size-only gate,
original-byte fallback on null decoding, unique filenames, and delete-old-before-
replacement-save. Small invalid fallback bytes can still be saved, and replacement
failure can leave the old DB filename without a file. No safer replacement or
stronger validation is claimed. Filename resolution is constrained to the supplied
app directory to avoid accidental external writes/deletes.

These additions have no current media/UI runtime evidence. Physical photos,
permission/cancellation, staged failure/retry, immediate replacement/removal,
source image parity, deletion and lifecycle checks still require authorized
fixtures and a storage-safe deployment.

## Beans, bags and read-only detail sections (offline implementation)

Settings → Beans now has native recycled rows, active counts and loading/empty/
error/retry states. The existing manual bean-create form also serves management
Add and still uses `BeanCreationWorkflow` with separate bean/initial-bag outcomes.
The empty-BAG path continues to return for explicit bag selection, never auto-select.
Saved bean fields use `BeanDraft`/`BeanWorkflow`; only URL trimming and the source
optional-value/clear rules are applied.

Saved detail includes source ratings, all bags including completed bags,
read-only recipe cards, and a bounded420dp shot RecyclerView using20-item pages
and threshold5. Rich history cards differ from Activity rows. Child Bag/Shot
routes preserve the parent draft and scroll/page state; return reloads bags and
recipes, not all ratings/history, matching the source refresh boundary.

Bag forms use the native date picker with max today and `BagDraft`/`BagWorkflow`.
Untouched loaded timestamps are retained; form date-only validation does not
replace the service's full-DateTime rule. Complete/reactivate calls persist
independently of Save, then source feedback/event/local-state ordering follows.
Deletes use the existing SimpleAction family and shared workflows.

Bag detail now loads the explicit shared associated-shot count alongside the
bag and rating aggregate. It does not substitute an unloaded navigation
collection's zero for the source warm-context count. Stats and delete wording
use that same value; all associated rows and rating filtering remain separate
semantics. Delete wording still promises associated-shot deletion while the
existing repository only marks the bag; no cascade change was added. Positive
deletion/status/old-content changes have not been exercised.

Recipes retain ROASTER/AI/CUSTOM/EDITED badges, source parameter/time text,
notes/source links and the read-only grind chip. The first active recipe grinder
is distinct from logging's selected grinder. Existing services/providers are used;
there is no invented Apply button, provider or AI opt-in. The source empty
FIND/REFRESH wording is retained.

Repository Debug/Release locks were reconciled using package-manager restore
for the coordinator's Core AI dependencies. Existing versions/content hashes
were unchanged, and normal locked builds work without isolated lock overrides.
This group remains offline-only: actual UI, status, recipes/provider requests,
history paging, persistence and shared-caller regressions still require the
permission/storage-aware replay.

## Saved-drink AI advice (offline implementation)

The edit-only Advice action now asks shared `IAIAdviceService` for the saved
shot ID, not unsaved form text. It prevents duplicate requests, checks
configuration first and passes the source ten-second cancellation token.
The four-dp gradient loading bar and request lifetime are stopped when the
editor is left. Late results cannot display on another page. Shared
local/cloud timeout and false-response behavior is not rewritten.

Successful responses use a native ActionModal composition, distinct from
the SimpleAction delete/archive family. It keeps the fixed-dark title/Close,
bounded inner scroll, adjustment rows, italic reasoning, source label and
optional prompt pill. Prompt toggles preserve the native tap owner and Close
controls instead of replacing their subtree. Entry/exit reuse the existing
bottom300ms CubicOut/400ms CubicIn motion. Runtime layout, gestures, focus,
animation, lifecycle and accessibility still need verification.

Configuration is standard `IConfiguration` with
Microsoft.Extensions.Configuration.Json11.0.0-preview.7.26381.103. Only the
source's empty base `appsettings.json` is always bundled. When the ignored MAUI
development file exists, the native build also bundles it as
`appsettings.Development.json` and loads it after the base settings in Debug
and Release. Debug may then override it with a file of the same name directly
under the running app's FilesDir.

The optional file uses the same schema (empty template only):

```json
{
  "AzureOpenAI": {
    "Endpoint": "",
    "ApiKey": ""
  }
}
```

Do not commit credentials. This personal Release configuration embeds the
development credential in the APK asset and must not be used for public
distribution. The native manifest declares the same normal INTERNET permission
as the pinned source for configured network features in Debug and Release.

The head now retains all three approved shared AI registrations: advice, vision,
and grind translation. Registration does not issue a request; explicit UI/tool
actions use the source configuration and unavailable behavior. No Apple/local
client is supplied. The advice singleton continues to acquire DTO context
through the shared service's fresh scopes.

Advice errors retain the existing D3 feedback presenter/queue and render only
the primary message. The pinned FeedbackService discards its recoveryAction
argument; that inherited limitation is preserved. No common feedback
implementation was changed.

## Appearance and complete Settings layout (offline implementation)

Settings now preserves the source order: APPEARANCE with LIGHT/DARK/AUTO tiles;
MANAGE; UNITS; VALUE RANGES; ABOUT; three-action bottom navigation. Header text,
selected dots/inversion, source About copy and existing management/unit/range
links remain. Native voice is now implemented offline as described below;
actual speech/provider/UI behavior remains unverified.

The platform service uses shared Core `ThemeMode` and `ThemePreference`, including
`AppThemeMode` and the source case-sensitive/numeric enum string behavior.
There is no duplicate native key or enum parser. API31+ uses only
`UiModeManager.SetApplicationNightMode`, matching the source and persisting the
mode for the next system splash. Pre31 uses a dedicated Activity configuration
context; it never writes the system NightMode setting. The pre31 system starting
window can follow OS resources before the app process applies its saved mode.

The Activity handles uiMode changes without recreation. NativeStyle changes the
five existing semantic page palette colors in place, retaining alpha, rounded
stroke widths and text spans. Existing EditText buffers are never replaced;
draft objects, filter criteria, active forms and page/history state remain
owned by their existing screens. Recycled row views are tracked weakly and
list layout-manager state is retained across rebinds. Actual focus, composition,
scroll and state survival still require UI verification.

Fixed-dark advice/filter/SimpleAction/feedback trees are explicitly excluded
from page palette refresh. The app's known palette is not replaced with Android
Force Dark; native force-dark transformation is disabled. The source status-bar
surface/icon behavior is applied; navigation-bar handling stays with the chosen
native theme/edge-to-edge policy, as it does in the pinned Activity.

This is crosscutting code, not runtime acceptance. Light/Dark/Auto, cold-start
splash, system configuration, forms, cached/recycled rows, popups, keyboard,
screen reader and Release all remain in the pending native/source matrix.

## Android voice (offline implementation)

Drink, Activity and Settings voice entrypoints now open the persistent native
window overlay. Activity/Settings return to New Drink before the source toggle.
Ready never auto-arms. Android uses tap-toggle, not iOS hold; the source's
“Hold to speak” text remains. Collapse/Expand preserves the conversation and
listening state; the window overlay is not rebuilt on page navigation.

The voice family is source gray/orange, not the coffee ActionModal palette.
Native Views/TextViews/Buttons provide its layout; small native Drawables render
the source microphone geometry. Panel420+Android34 assumption,80 mic with15
expanded hit area,56 FAB, and source logical-unit fonts/positions are retained.
This family intentionally does not reuse Advice's real-inset host. Landscape
clipping from the source's fixed panel geometry remains a runtime comparison
case, not a secretly redesigned adaptive sheet.

One voice conversation owns one DI scope with the actual shared command engine,
generated tools and typed platform actions. No duplicate command schemas/body
implementation or Apple branch was added. New Show clears conversation;
Collapse/Expand does not. Commands retain source CancellationToken.None while
recognition owns its separate cancellation. An ending scope drains committed/
in-flight command work before disposal, while obsolete UI/platform requests
are fenced; closing is not rollback.

The Android recognizer uses ordinary online-capable SpeechRecognizer with
CurrentCulture and partials. Its60-second bound, generation checks, listener
detach and Destroy/Dispose paths are explicit. Source's active1.5-second partial
silence timer is retained. Stop settles the pending result deterministically
with the latest hypothesis rather than leaving a destroyed recognizer's task
pending. The caller preserves source partial concatenation, stop-time capture,
and its recording/mic-latch distinctions after natural completion.

The pinned Android permission flow checks RECORD_AUDIO and uses the source
Permission Required/Open Settings/Cancel dialog on denial; it does not silently
auto-request a permission. No dialog or permission operation was executed here.
Empty cloud configuration returns the actual shared “Voice commands are
temporarily unavailable.” response when a transcript is processed.

Native platform actions queue known root/detail routes with typed IDs and bag
metadata, reject stale/busy owners, and preserve return views with typed native
snapshots. Navigation acknowledgements remain optimistic source tool responses,
not proof of actual navigation. Source history-query and unregistered-route
limitations remain. Data notifications refresh voice-created picker references
without selecting new entities.

The camera adapter delegates ACTION_IMAGE_CAPTURE through a non-exported,
temporary-grant FileProvider exposing only its own voice-capture cache directory.
It honors1024/quality70 source options, default no rotation and the source EXIF
metadata subset. No gallery/broad storage/permanent grant is added. Only this
capture's generated temporary files are cleaned up. Camera/browser APIs and
RECORD_AUDIO/query/provider declarations are code-only; no capture or browser
was invoked. General intent classification/photo workflows reuse this adapter as described below.

A partial startup hook has no ordinary implementation. A session-artifact-only,
Debug-guarded target supplies clearly scripted speech/provider interfaces for
future UI replay. Scripted types are absent from ordinary Debug/Release output.
No scripted fixture was installed and no fake response path ships normally.

All voice UI/audio/permission/camera/browser/provider/navigation-return and
heap/lifecycle checks still require authorized runtime/source comparison.

## General photo and Add Coffee (offline implementation)

New Drink's camera action now runs one owner-bound photo workflow. Edit Drink
still shows saved-drink advice. The existing camera adapter is reused for
general photos, voice photos and label scans. General/voice requests keep
1024/70 processing. Add Coffee's title-only scan keeps the original camera bytes
without a resize or recompression request. Each capture has a unique result
code, temporary URI and cancellation owner. Cleanup targets only that capture's
generated files; no old fixture, gallery or app data is cleared.

The source reviewing veil blocks input and hides the underlying editor from
accessibility. Shared photo rules choose automatic intent or the fixed-dark
Use This Photo ActionModal. Its four choices preserve Retake and Close/Cancel;
the choice is returned after exit. Coffee uses shared extraction/prefill rules,
Profile opens a new staged-photo form, and Room uses the source question and
native result alert. No existing-profile identification route is invented.
Late camera, classification, extraction and choice results cannot route a
replacement editor.

Add Coffee has recent-six Browse, Type, date/chip/fuzzy suggestions and
label-scanning content. The only pinned product entry is the general-photo
coffee route. It initializes Type with a prefill; Browse is reached through
the source Browse recent link when recents exist. No extra manual entry was
invented, and the manual empty-BAG workflow remains unchanged.

The shared workflow snapshots Type fields, trims bean values, places notes only
on the bag and retains separate bean/bag results. Existing-coffee choices make
a Today bag. Source partial writes remain possible; there is no rollback or
retry-idempotency claim. Success runs haptic, popup exit, OnCreated, then disposal.
Only this photo callback inserts/selects the returned bag in the captured drink
draft. Source feedback returns immediately while the D3 toast runs separately;
only the primary message is shown. Label extraction keeps the source None token,
but closed owners reject late UI updates.

If exit or OnCreated fails after a bag was saved, the original active owner
requests one error toast before popup/session release. Browse retains
"Couldn't create bag"; Type uses the exception message. Type's earlier save
failures remain inline. A detached form cannot show an inline completion error,
so that specific error uses the owner feedback path. Cancellation and lost
owners do not request an error. No failed exit invokes OnCreated, and no
completion failure retries creation or implies that the saved bag was rolled back.

ActionModal content is native Views, not a shared MAUI renderer or a native
AlertDialog substitute. It keeps fixed-dark chrome, real top/left/right insets,
separate design padding, bottom opt-out and the source 300/400ms motion. All
camera/URI/bitmap/haptic/keyboard/layout/accessibility/persistence and provider
checks remain NOT RUN. Offline controller/model checks and builds are not native
runtime or final rebuild acceptance.

## Package identity

The coordinator corrected the initial `.native` implementation choice to
`.nativeapp` on 2026-09-25. The .NET Android SDK rejects `native` as a reserved
Java keyword (XA4258). The new ID preserves separate installation and storage;
no SDK validation is bypassed.

The initial pre-agent Debug and Release builds were independently cold-launched on
`emulator-5554` (Android 14) after this correction. Both built without warnings
or errors. The Release app is not debuggable; its APK uses the development
signing certificate for local validation, not a production signing identity.
These are native-startup checks, not Ailoha inspection or workflow parity.
Subsequent clean binding generation has reviewed Kotlin synthetic-member parser
warnings; see the binding README rather than assuming it is warning-free.

## Debug-only native Ailoha

The official native Android agent **0.1.13** is integrated through the adjacent
app-internal `BaristaNotes.Android.Ailoha` binding in Debug only. Do not
substitute `Ailoha.Agent.Core`: its MAUI Controls dependency violates the
native boundary.

Authorized package access on 2026-09-25 established that the official stable
publication is available at lowercase `maven.pkg.github.com/redth/ailoha`.
Microsoft organization access is not needed for this publication. Native
inputs remain outside the repository and are pinned and build-verified by
the binding's `native-inputs.json`. See its README for exact dependency,
license and connection details.

Source evaluation at release tag `v0.1.13`, commit
`037a25bc8d0e6f88636039fa0cb4c94b0b662634`, confirms:

- `AilohaAndroidAgent.start(Application, AilohaAgentOptions)` returns its port.
- Options include port 9233, broker port 19323 and optional app name.
- Native Android Views resource IDs become automation IDs.
- Android minimum API is 24, Kotlin is 2.2.20 and NanoHTTPD is 2.3.1.
- The current Ailoha Binary Distribution License grants official binary use
  but reserves modification and source redistribution. It is not MIT, despite
  stale MIT metadata also present in the Gradle publishing configuration.

Native app/device identity, Android Views tree, stable-ID button operation,
counter transition and changed screenshot have been proved using Ailoha.
Release was rebuilt independently and checked for agent exclusion and startup.
This foundation proof does not establish any feature/design parity.

## Assets

Manrope regular/semibold, Material Symbols, coffee icons and the bean image
are copied unmodified from the fixed reference source at commit
`85beca0bb79e56720ff5dac31230958b5b0efa60`. Manrope's OFL is retained.
The Material/coffee fonts are retained for the upcoming source-derived UI,
not replaced with emoji. Native theme resources use the source surface,
primary and text colors; source window chrome is not replaced with tabs.

## Build

Use the installed .NET 11 Android workload and an isolated `--artifacts-path`
for concurrent work. Debug requires `-p:AilohaArtifactsDirectory=` pointing
to the directory with the two pinned official inputs; Release does not.
Restore precedes `--no-restore` builds. Configuration-specific lock files
separate the Debug binding graph from Release. Re-evaluate the applicable lock
when changing the restore RID set. Runtime identifier selection belongs to the
build command, not a device-specific project default.
When installing the APK directly rather than using the SDK deployment target,
pass `-p:EmbedAssembliesIntoApk=true`. A FastDev APK alone does not contain the
CoreCLR managed assemblies and cannot start on a device without its matching
deployment payload.
Keep build logs, screenshots and APK provenance in the session evidence folder,
not in source control. Never uninstall or clear the retained MAUI app.
