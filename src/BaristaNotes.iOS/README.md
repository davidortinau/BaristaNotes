# Native iOS foundation

This is a genuine C# UIKit `net11.0-ios` head, not a MAUI application.
It uses `com.simplyprofound.baristanotes.native`, the display name
**BaristaNotes Native**, iPhone-only device family, and the source iOS 15 minimum.

## Beans map (coordinator validation pending)

### Round 30: mixed-country rendered-cluster test setup

Mapsui 5.1.0 derives `Navigator.ZoomBounds` when layers are added.
Disabling the tile layer does not clear those existing bounds, so the test's
`ZoomTo(1000000)` was clamped and left Ethiopia separate from Brazil/Colombia.
The test now sets `OverrideZoomBounds` only on its tile-disabled session,
asserts the actual 400-by-300 viewport and requested resolution, and measures
all projected place distances against the unchanged **64-DIP** threshold
before checking the single rendered feature. The original countries,
**Origins (3)** label, blend deduplication, country/location membership,
selected beans and repeat clearing remain asserted. No runtime zoom bounds,
coordinates, fixtures, data, compact labels or iOS queued-redraw code change.
This controlled test viewport does not require widening the live tile camera
or modifying saved samples to create a mixed-country cluster.

Only the coordinator runs these from the export root. Simulator builds use
the proven SDK ad-hoc signing configuration, not disabled signing:

```sh
dotnet test src/BaristaNotes.Tests/BaristaNotes.Tests.csproj --filter "FullyQualifiedName~BeanPlaceMapTests|FullyQualifiedName~BeanOriginMapTests|FullyQualifiedName~BeanMapSessionTests|FullyQualifiedName~BeanPageGeometryTests|FullyQualifiedName~NominatimOriginGeocoderTests" --artifacts-path <fresh-round30-native-tests>
dotnet build src/BaristaNotes.iOS/BaristaNotes.iOS.csproj -c Debug -r iossimulator-arm64 -p:NativeMapSlice=true -p:EnableCodeSigning=true -p:CodesignKey=- -p:AilohaPackageRoot=<existing-agent-package-root> --artifacts-path <fresh-round30-ios-maps-debug-artifacts>
dotnet build src/BaristaNotes.Android/BaristaNotes.Android.csproj -t:SignAndroidPackage -c Debug -r android-arm64 -p:NativeMapSlice=true -p:EmbedAssembliesIntoApk=true -p:AilohaArtifactsDirectory=<existing-agent-artifacts> --artifacts-path <fresh-round30-android-maps-debug-artifacts>
```

Runtime remains coordinator-owned: update the existing isolated app without
clearing data on the owned Duo simulator
**B48CED31-1B76-4CED-8FDF-47D91AF5BA33**, then exercise Settings -> Beans -> Back
at least 20 times during tile/animation/origin updates and after full map
scroll-out. Drain a queued redraw after disposal and check that retired
controls/canvases receive no `SetNeedsDisplay`; reopen and confirm rendering.
Retain the round-28 disposal/rotation scenarios and round-29 compact labels,
wide/split Guji pins, repeat selection, one-tap rows, Detail/Back camera,
accepted geometry and persisted-cache/data checks on owned iOS comparisons
and Android **emulator-5556** only. The old installed-package crash does not
verify the new lifecycle fix. No new build, test or runtime result is claimed.
NativeAOT stays separate; public-service checks remain serial across prototypes.

### Round 29: compact selected-origin header

Both native heads now display the Core `SelectionLabel` directly below
**BEANS**, using the existing marker-title rules once: **Guji (1)**,
**Ethiopia (1)**, same-country **Ethiopia (2)**, and mixed-country
**Origins (3)**. No precision, nearby-origin, country-list or archived-membership
prose appears in that header. Distinct-bean counts, the unfiltered count and
**All beans** action are unchanged; precision remains in place metadata and
map accessibility. No separate explanatory summary line existed in these
heads, so none is retained or allocated. Offscreen legacy-footer measurement
continues to preserve the accepted map edge; page geometry is untouched.
The round-28 marker tests and disposed-redraw guard remain, with focused
selection-label coverage for fallback, city/locality/region, same/mixed-country
clusters, archived blend deduplication, repeat clearing and cached restoration.

Only the coordinator runs these from the export root:

```sh
dotnet test src/BaristaNotes.Tests/BaristaNotes.Tests.csproj --filter "FullyQualifiedName~BeanPlaceMapTests|FullyQualifiedName~BeanOriginMapTests|FullyQualifiedName~BeanMapSessionTests|FullyQualifiedName~BeanPageGeometryTests|FullyQualifiedName~NominatimOriginGeocoderTests" --artifacts-path <fresh-round29-native-tests>
dotnet build src/BaristaNotes.iOS/BaristaNotes.iOS.csproj -c Debug -r iossimulator-arm64 -p:NativeMapSlice=true -p:EnableCodeSigning=true -p:CodesignKey=- -p:AilohaPackageRoot=<existing-agent-package-root> --artifacts-path <fresh-round29-ios-maps-debug-artifacts>
dotnet build src/BaristaNotes.Android/BaristaNotes.Android.csproj -t:SignAndroidPackage -c Debug -r android-arm64 -p:NativeMapSlice=true -p:EmbedAssembliesIntoApk=true -p:AilohaArtifactsDirectory=<existing-agent-artifacts> --artifacts-path <fresh-round29-android-maps-debug-artifacts>
```

Required scenarios on owned iOS comparison simulators and Android
**emulator-5556** only, updating the existing isolated apps without clearing data:

1. Compare marker and selected-header text at wide **Ethiopia (2)** and
   separated **Guji (1)**/**Ethiopia (1)** pins, plus Colombia/Brazil.
   A same-country cluster always uses its country name; a mixed-country
   cluster uses **Origins (count)**. Pan/zoom existing records to check this,
   without new fixtures. Confirm no explanatory line or gap below the header,
   including large text and both themes. VoiceOver/TalkBack still identify
   Guji as a region and country-only/fallback coordinates as approximate.
2. Select/repeat-clear each place/aggregate and use **All beans**. Check
   the correct rows/counts, cached Detail/Back/reopen/restart, camera/list
   anchor retention and single physical row activation on first visit,
   after scroll, after filter and after Back; retain keyboard/accessibility
   activation. Keep four mapped beans and one empty-origin unplotted,
   with all five original records/cache unchanged. Offline successful-cache
   reuse and explicit recoverable approximate fallback must remain usable.
3. Scroll the map fully out and reverse; verify the same camera, clipped
   20% parallax, real map behind status area, safe pinned compact title/first
   row, separator, attribution and unchanged bottom navigation/portrait
   policy. Repeat the round-28 iOS map retirement/queued-redraw scenarios
   below to retain that correction; no zoom buttons or orientation actions.

No builds, tests or runtime checks have been run by this file-only worker.
NativeAOT remains a separate gate. Public-service checks stay serial across
prototypes; the per-process throttle is not production-wide rate control.

### Round 28: compact markers and disposed-redraw guard

Core's `BeanOriginClusters.Title` now supplies compact marker titles in both
native heads: **Guji (1)**, **Ethiopia (1)**, same-country **Ethiopia (2)**,
and mixed-country **Origins (3)**. No "Nearby origins" or precision prose
appears on markers. `BeanOriginPlace.Label`, place metadata and map
accessibility retain region/approximate-country information; geographic
coordinates, counts, IDs, clustering distance, toggles and camera are unchanged.
`BeanPlaceMapTests` covers all precision kinds, same/mixed-country labels,
blend deduplication, actual rendered-feature label text and the complete
Guji split/select/cache journey.

The concrete iOS lifecycle defect is a queued redraw outliving its native
receiver: `BeanListViewController.ViewWillDisappear` -> `RetireMap` ->
`BeanMapView.Dispose` -> Mapsui 5.1.0 `MapControl.Dispose`. The published
`Mapsui.UI.iOS/MapControl.cs` dispatches unconditional `SetNeedsDisplay` calls;
it releases the native control/canvas before `SharedDispose` stops the render
controller. `Mapsui.UI.Shared/RenderController.cs` can also finish an iteration
after its non-awaiting `Dispose`. An already queued callback therefore reaches
a zero native handle. This fits the supplied main-queue `setNeedsDisplay`,
zero-receiver crash; it does not establish an orientation/keyboard cause or
prove that this is the simulator's only crash path.

`BeanMapView.cs` now selects the iOS-local `MapsuiLifecycle/MapControl.cs`
and `MapControl.Shared.cs`, adapted only from the supplied **Mapsui 5.1.0**
`Mapsui.UI.iOS/MapControl.cs` and the iOS branch of
`Mapsui.UI.Shared/MapControl.cs`. The nonvirtual invalidation callback prevents
a reliable subclass-only fix. The existing Mapsui.iOS **5.1.0** package,
public `RenderController`, Skia renderer, Metal canvas, gestures and map-info
pipeline remain in use; no package upgrade, new renderer, external source link,
GPU disablement or Android host change is introduced.

The local control marks retirement before cleanup, checks retirement and live
control/canvas handles **when the queued main-thread redraw executes**, and
serializes redraw/native release with a lifetime gate. It stops/unsubscribes
the renderer/map before disposing native views, detaches paint handlers and
clears canvas references. The render-loop map getter is gated against recreation
after disposal; late paint/layout/touch callbacks are guarded. Disposal overrides
the actual UIKit virtual method, protecting inherited `UIView`/`IDisposable`
paths as well as the typed control path. Browser registration uses a static
callback so it does not retain a retired control. The upstream MIT license is
kept in `MapsuiLifecycle/LICENSE` and bundled via the iOS project.
Controller retirement timing, Detail/Back reuse, accepted spacing, whole-page
scroll, clipped 20% parallax and bottom navigation are unchanged.

Only the coordinator runs the following from the export root:

```sh
dotnet test src/BaristaNotes.Tests/BaristaNotes.Tests.csproj --filter "FullyQualifiedName~BeanPlaceMapTests|FullyQualifiedName~BeanOriginMapTests|FullyQualifiedName~BeanMapSessionTests|FullyQualifiedName~BeanPageGeometryTests|FullyQualifiedName~NominatimOriginGeocoderTests" --artifacts-path <fresh-round28-native-tests>
dotnet build src/BaristaNotes.iOS/BaristaNotes.iOS.csproj -c Debug -r iossimulator-arm64 -p:NativeMapSlice=true -p:EnableCodeSigning=true -p:CodesignKey=- -p:AilohaPackageRoot=<existing-agent-package-root> --artifacts-path <fresh-round28-ios-maps-debug-artifacts>
dotnet build src/BaristaNotes.Android/BaristaNotes.Android.csproj -t:SignAndroidPackage -c Debug -r android-arm64 -p:NativeMapSlice=true -p:EmbedAssembliesIntoApk=true -p:AilohaArtifactsDirectory=<existing-agent-artifacts> --artifacts-path <fresh-round28-android-maps-debug-artifacts>
```

Required runtime checks (update the existing isolated app without clearing data):

1. On the owned Duo simulator **B48CED31-1B76-4CED-8FDF-47D91AF5BA33**,
   repeat Settings -> Beans -> Settings/back while tiles are loading, during
   map pan/fling/pinch/double-tap animation, during origin completion, and
   immediately after scrolling the map fully offscreen. Repeat at least 20
   times, then reopen Beans and verify the Metal map still renders/responds.
   Also replace the Beans stack through Activity/New Drink and reopen, and
   exercise scene/window teardown with Beans visible or beneath Bean Detail.
   Inspect native crash logs and retained controls/canvases for bounded cleanup.
2. With coordinator debugger/instrumentation on that owned simulator, queue
   `InvalidateCanvas`/`ForceUpdate` and dispose the same local control in the
   same main-queue turn before queued redraw execution. Drain the queue:
   neither retired control nor canvas may receive `SetNeedsDisplay`.
   Repeat through typed control, `UIView` and `IDisposable` disposal, with
   repeated disposal and a late invalidation. No runtime access-violation
   catch or retained-map workaround is acceptable.
3. Attempt simulator portrait -> landscape-left -> portrait and
   portrait -> landscape-right -> portrait while Beans is visible, while
   returning from Bean Detail, and immediately around Beans -> Settings/back.
   Use the OS/simulator rotation controls, not a new app map action.
   Beans must retain its existing portrait-only policy; confirm no Expand,
   Close, map-driven orientation request, native crash or layout regression.
   Repeat background/foreground during a tile/animation update.
4. On both owned iOS comparisons and Android **emulator-5556** only,
   verify wide **Ethiopia (2)** and separated **Guji (1)**/**Ethiopia (1)**,
   Colombia/Brazil one each, four mapped and one unplotted, with no precision
   prose on markers. VoiceOver/TalkBack must retain the Guji region and
   approximate-country descriptions. Mixed-country clusters say **Origins**;
   blends deduplicate IDs. Select/repeat-clear each place/aggregate; one
   unfocused-row tap opens the correct detail on first visit, after scroll,
   after filtering and after Back; retain keyboard/accessibility activation.
5. Explore the camera, scroll fully out/reverse, and Detail/Back without
   retiring the Beans owner: preserve camera, selection, list anchor, safe
   compact pinned title/first row, separator, hero lower edge, attribution
   and bottom navigation in both themes and large text. Reopen/restart offline
   using the persisted success cache; uncached failures retain explicit
   recoverable approximate fallback and a usable list. Preserve all five
   existing UI-created samples and existing cache/data.

No build, test or runtime verification is claimed by this file-only worker.
NativeAOT remains a separate gate. Public lookups are still a single-user
prototype: exercise one new geocoding client/prototype at a time;
per-process throttling is not production-wide service rate control.

Round 26 is limited to Core's explicit comma-bound country parsing and xUnit
isolation for the shared static Nominatim gate. See the Android README's
**Round 26** section for the correction, coordinator commands (fresh round26
artifact paths) and runtime scenarios. UIKit, Guji coordinates, one-tap
navigation, camera, geometry and saved samples are unchanged. The process-wide
serial >=1-second gate and exact timing assertion are unchanged; no new
build/test/runtime verification is claimed.

Round 25 makes only shared-Core validation-fallback and country-alias corrections.
See the Android README's **Round 25** section for the four diagnosed defects,
strengthened focused assertions and coordinator commands/scenarios (use fresh
round25 artifact paths). UIKit, one-tap navigation, geometry and camera are
unchanged. No new build/test/runtime verification is claimed.

Round 24 supersedes the historical country-only behavior below. Both native
heads now reuse Core's cached Nominatim resolver, separate place model,
64-screen-DIP zoom-dependent clusters and stable place-ID selection. Guji's
real administrative result is labelled **Guji region, Ethiopia**; country-only
Ethiopia retains its approximate country point. They aggregate to two beans at
wide zoom and separate to two individually selectable one-bean pins at sufficient
zoom. Repeat selection clears; Detail/Back retains camera, selection and anchor.
No saved bean data, schema or origin text is changed.

UIKit injects the same singleton geocoder and dispatches result/pin updates to
the main thread. Leaving Beans cancels pending resolution; late completions are
version/lifetime guarded. Cached successes are reused on return/restart.
Failures retain labelled approximate country fallback and explicit feedback,
with conditional **Retry origins**. Rich accessibility information is separate
from the old offscreen footer measurement, preserving accepted hero spacing.
Resolution refreshes header labels, not unchanged collection cells, so a live
row tap or keyboard focus is not interrupted by a geocoding completion.
The existing map/collection lifetime, single-tap row navigation, safe compact
pinned title, separator, clipped 20% parallax, portrait-only mode and bottom
navigation are unchanged. No permanent summary caption is restored.

See the Android README's **Round 24** section for shared implementation,
request validation/privacy, persisted failure/retry behavior, runtime-changeable
`origin-geocoding.json` in the existing iOS Library directory, prototype/public
policy limits, focused-test command and complete runtime scenarios. Exercise
one new geocoding client/prototype at a time; per-process throttling is not
production-wide rate control.

The coordinator's required Debug build is:

```sh
dotnet build src/BaristaNotes.iOS/BaristaNotes.iOS.csproj -c Debug -r iossimulator-arm64 -p:NativeMapSlice=true -p:EnableCodeSigning=true -p:CodesignKey=- -p:AilohaPackageRoot=<existing-agent-package-root> --artifacts-path <fresh-round24-ios-maps-debug-artifacts>
```

Use only owned comparison simulators and update only the existing isolated maps
app, preserving the five UI-created samples. Verify real public Guji lookup,
region versus approximate country labels, wide/split pin counts and repeated
place/aggregate toggles, one-tap Detail/Back, explored camera through full map
exit/reverse, safe pinned title and first row, unchanged navigation, cached
offline restart, uncached error/timeout/retry, endpoint changes, cancellation,
VoiceOver, large text and both themes. Source tests use controlled HTTP handlers;
the worker has run no builds/tests/device checks and claims no runtime result.
NativeAOT remains a separate gate.

Beans now includes Mapsui.iOS 5.1.0 first, above the existing heading, including while
the bean list is empty or loading. The native heads share Core's
`BeanMapSession`: one supported default OpenStreetMap tile layer, an identifying
User-Agent, explicit logged tile failures and unchanged default cartography.
Round 13 moves the hero's top into the former title space while approximately
preserving its lower edge. The existing `EquipmentRows` UICollectionView has an
opt-in Beans page: hero in section zero, original heading as section one's
supplementary header, virtualized original bean cells and a state/trailing-space
cell. `UICollectionViewFlowLayout.SectionHeadersPinToVisibleBounds` pins the
title at the usable top. Minimum trailing space also permits pinning/removing
the complete hero on empty, short and filtered lists. Collection scroll offsets
translate only the existing map surface by 20% within a clipped container.
The map is not recreated or resized per scroll frame. Reverse scroll releases
the title and restores the same map/camera. Other `EquipmentRows` users retain
their original single-section layout. Bottom navigation remains outside the
page scroller. No device-location permission is added.
Surrounding controls use existing app fonts/colors; the map itself is
not recolored into a new renderer for dark mode.

Round 16 is a spacing-only correction. The finite Beans collection viewport
starts at physical y=0 with automatic content-inset adjustment still disabled.
The map's own clipped container and hero cell also start at y=0: no inner
safe-area padding or permanent summary/control footer remains. The former
hero geometry minus the old summary and idle controls preserves the
approximate visible map lower edge. Removed-footer typography is measured
offscreen for width/font scaling, without retaining a summary view or reserving
footer space. Genuine origin-loading/error feedback
remains transient. The existing All beans action appears inside the map above
attribution only during selection, with no reserved footer. Core totals and
approximate-origin text remain in the map accessibility value.

Only Beans opts into the compact `EquipmentHeader`: existing fonts/palette,
natural label heights, 12-point vertical padding, 4-point label gap and
1-point Outline separator. A small Beans-only flow-layout inset adjustment
retains native section-header pinning but places its labels at the usable
status/notch edge. The existing top surface masks rows under system icons
only when pinned; it is hidden over the expanded map. Status icons are dark
over unchanged light map tiles and palette-appropriate over the pinned surface.
Other headers, collections and pages retain their existing layout/style.
The clipped 20% parallax transform, map/camera lifetime, scroll direction,
virtualization, filter and portrait-only paths are unchanged.
Shared `BeanPageGeometryTests` covers upward expansion/lower-edge geometry,
compact multi-line sizing and safe-edge pin/release thresholds.
Round 16 builds and device checks remain coordinator-owned and pending.

Expand, Close, the full-screen controller and map-driven scene geometry requests
are removed. Ordinary app supported orientations remain unchanged. The finite
collection viewport now reaches behind the status/safe area; its page pan recognizer
does not take touches starting on the map surface, preserving SDK pan/pinch.
Title/row/metadata drags scroll the one page. Attribution appears once as the SDK's
clickable copyright widget, with its default 12-DIP black text on an opaque white
background in both app themes. Map initialization/tile errors stay
local and do not replace the Beans list. Tile failure labels remain for the
current map session rather than being cleared by another tile's success.

Round 9 embeds the supplied 177-country Natural Earth label-position catalogue
in Core and reuses its resolver, distinct counts, pin layer, selection and
one-time camera-fit logic in both native heads. See the Android map section for
data provenance, normalization/boundary rules, public Mapsui API citations,
padding thresholds and the shared targeted-test command. Parsing uses
`JsonDocument`, not reflection-based serialization. Pins retain country/count
labels; the map accessibility value describes approximate country positions,
never farms, without a permanent visible summary.

The map's independent non-deleted bean read includes archived beans while the
ordinary Beans list stays active-only. Empty/unresolved origins have no invented
positions and contribute to the accessibility unmapped count. Blends can appear in
multiple country counts, but mapped totals and combined lists deduplicate IDs.
Initial data fits once (country-scale for one point, labelled world overview for
empty/unmapped data). The existing view/session survives refresh, Detail return
and scroll-away/reverse, so later reads update pins without recentering the camera.

A country-pin tap shows its count and matching existing bean rows below the map,
including ARCHIVED captions. Tapping the same selected country set again clears
it; another country replaces it. All beans clears only the filter.
Existing Bean Detail navigation is reused;
list reloads preserve the collection offset on Back. Only an intentional new
selection starts its rows at the top. Origin-read errors are logged and shown
locally without replacing the ordinary active list or hiding tile errors.

Shared Core recognizes Mapsui `DoubleTap` and calls the published
`Navigator.ZoomIn(args.ScreenPosition, duration: 200)`. Only `SingleTap` toggles
countries. The SDK immediately emits the first tap of a double tap as a single
tap (which may select/clear a pin); the recognized second tap never toggles it.
Two-finger double-tap zoom-out is explicitly omitted because the supplied 5.1.0
tracker exposes no such event. No custom gesture recognizer is added.
The SDK GPU setting, tile source and saved data remain unchanged.
The SDK layer's `HyperlinkWidget` retains the tile source copyright text/URL and
uses the platform browser action registered by the native `MapControl`; only the
duplicate UIKit copyright button is removed. See the Android map section for
the published attribution API citations. Use an actual OS/device frame for
GPU-backed map evidence, not a view-render screenshot.

Only the coordinator runs these commands from the export root, using matching
installed .NET 11/iOS/Xcode tooling and the existing official Debug agent input.
Angle-bracket values are coordinator-owned isolated paths. Do not deploy the
ordinary Release app ID; use `NativeMapSlice=true` in both configurations.

```sh
dotnet build src/BaristaNotes.iOS/BaristaNotes.iOS.csproj -c Debug -r iossimulator-arm64 -p:NativeMapSlice=true -p:EnableCodeSigning=true -p:CodesignKey=- -p:AilohaPackageRoot=<existing-agent-package-root> --artifacts-path <fresh-round16-ios-maps-debug-artifacts>
dotnet publish src/BaristaNotes.iOS/BaristaNotes.iOS.csproj -c Release -r iossimulator-arm64 -p:NativeMapSlice=true -p:PublishAot=true -p:EnableCodeSigning=true -p:CodesignKey=- -p:TrimmerSingleWarn=false --artifacts-path <ios-maps-release-simulator-artifacts>
dotnet publish src/BaristaNotes.iOS/BaristaNotes.iOS.csproj -c Release -r ios-arm64 -p:NativeMapSlice=true -p:PublishAot=true -p:EnableCodeSigning=false -p:TrimmerSingleWarn=false --artifacts-path <ios-maps-release-device-artifacts>
```

Round 16 requires the Debug build and shared focused-test command in the
Android map section; the existing Release commands are a separate NativeAOT
gate. On an owned maps simulator, compare expanded/intermediate/pinned/reversed
OS frames in light/dark mode: tiles reach physical y=0 behind status/notch icons,
no solid top band appears while expanded, and the initial map lower edge stays
within roughly one former caption line of its prior position. Measure the
compact labels/padding/gap and visible separator, including when pinned.
Pinned labels and tappable rows must stay below the usable safe edge.
Repeat with empty, short, long and filtered content, large text, explored
camera reversal and Detail/Back. Check Ethiopia two-bean toggle back to five,
another country, All beans, pan/pinch/double tap, attribution, genuine load/error
feedback, bottom navigation and home-indicator clearance. Preserve the
existing form-created samples and all user data; do not seed/reset.

The map build property isolates both configurations as
`com.simplyprofound.baristanotes.maps.native.ios`; Release enables NativeAOT
and full trimming; NativeAOT acceptance remains a separate open gate.
It cannot be combined with performance fixtures. Device
publishing without signing is a compatibility/package check, not a deployable
signed-device acceptance result. Release does not use the Debug agent.
See the Android head's map section for the targeted shared-Core test command.

On the dedicated BaristaNotes Maps iOS 27.0 and 27.1 simulators,
replay empty, short, five-bean, longer and filtered Beans in both light and dark
themes. Check map first/taller with its old lower edge approximately preserved;
intermediate clipped parallax; complete hero removal; title/count pinning at the
usable top; later rows scrolling and remaining tappable below it. Reverse must
release the title and restore the explored map/camera. Repeat at least five times.
Check Add, existing Detail/Back at a deeper row, background/foreground and fixed
bottom navigation. Map pan/pinch must not scroll the page; title/row/metadata
drags must. One-finger double tap must visibly zoom at its tap position without
a second country toggle. Two-finger double-tap zoom-out is not promised.
Uncached network failure must not block those list actions. Use only UI-created
records and preserve all existing data.
Replay the Android section's round-9 UI-created country samples, archived/deleted
and blend-count scenarios, initial empty/single/multiple-country fit, pin-selected
rows and Detail/Back preservation. Tap Ethiopia once to show its two beans;
after the SDK double-tap interval, tap it again to restore the five active
samples. Selecting Colombia/Brazil changes the filter without moving the camera.
Expected sample totals are Ethiopia = 2,
Colombia = 1, Brazil = 1, mapped = 4, unmapped = 1. Check region-only unknowns
remain unmapped and adding/editing origins does not repeatedly recenter.

Inspect every screen edge and fixed control before, during and after scrolling,
especially the status/safe area, single readable/clickable attribution widget,
pinned heading and bottom navigation. Verify no Expand, Close or full-screen
map exists and no map action forces rotation; normal app layout policy remains
usable. Repeat representative scenarios in the
NativeAOT Release simulator app; simulator/Debug success does not establish
signed physical-device Release behavior. Do not bulk-download tiles.

This file-only implementation pass produced no build, test, tile-network or device
results. The published `MapControl(CGRect)` constructor, tile layer, nullable
SDK `DataChangedEventArgs.Error`, map assignment and disposal remain unchanged.
Record all restore, build/publish and AOT/trimming diagnostics, plus bundled
Mapsui/Skia assets.
Unresolved always-throw or unsupported dynamic-code warnings block Release
acceptance; none are suppressed here. Compatibility and runtime acceptance
remain pending until the coordinator supplies its own results.

**Bounded first slice, not full parity:** startup calls the coordinator-owned
Core registration and awaits shared database initialization. UIKit presenters
use `DrinkWorkflow`, `DrinkDraft`, `BeanCreationWorkflow`, and existing history
services through separate per-operation scopes. The database is in this app's
own Library directory. No direct database seeding is performed.

New Drink, Bean and Profile create/cancel, category lists, Activity, and a working-copy
Filter Shots presenter are wired. Numeric selectors use Core `MassPickerState`,
formatting uses `DrinkDisplay`, and filtering uses a cloned Core
`ShotFilterCriteria`. UIKit owns only the reusable cells, layout, native input,
navigation, and scoped asynchronous calls.

Successful creation leaves the form before the parent refreshes its reference
lists. A failed refresh offers a read-only retry, not another ADD. Bean and
Profile validation use the source inline ERROR tile and preserve keyboard
focus. People opens the source two-column BY/FOR selector; both selections
update the draft immediately and Close/Done return without an extra save.
Profile photo/edit/delete were outside that prerequisite; their later bounded
implementation and verification limits are described below.

Activity serializes refreshes, snapshots each filter request, and discards an
older result when a later request is pending. No-match content has separate
title/body labels and a Clear Filters action. Error feedback retains the source
five-second cooldown and FIFO queue across navigation and early dismissal;
the approved two-second visible hold still applies to each admitted toast.

The simulator run exercised bean/today-bag creation without auto-selection,
explicit selection, 18.3g/36.5g save, modal Apply/Close/backdrop/Clear,
same-record update, restart, scrolling and combined rating filters. Release
startup was separately checked with the same preserved sandbox. Matched
pinned-reference comparisons, long-name/multi-person fixtures, full keyboard,
rotation/theme/accessibility matrices, and coordinator acceptance remain
separate requirements. The repair pass uses separately identified fresh native
fixtures without clearing the populated application sandbox; current P0
evidence is kept separate from the original foundation/partial-slice captures.
Other features display an explicit first-slice boundary message, never fake
success. Do not interpret a successful build as full slice delivery.

Fonts and the coffee image are bundled from the existing source resource files
without referencing the MAUI project. UIKit color mappings retain the source
coffee palette. The full icon/splash composition and feature layouts remain
part of later source-led implementation.

## Building

Use an installed, internally consistent .NET 11 SDK and iOS workload, with Xcode.
Keep outputs isolated using `--artifacts-path` on restore and build.

Debug also needs the official Ailoha package outside the repository. See the
binding project's README for its exact version, checksum, and layout. Pass
`AilohaPackageRoot` to both restore and build. The native RID is forwarded to
the binding as `AilohaBridgeRuntimeIdentifier`; ordinary binding projects do
not inherit the application's RID automatically.

This machine's RC1 binding generator requires host runtime
`11.0.0-rc.1.26426.105`, newer than its installed RC1 runtime
`11.0.0-rc.1.26425.128`. The foundation was built with the **already installed**
SDK `11.0.100-preview.7.26381.103` and iOS workload `26.5.11997-net11-p7`,
selected only in external evidence storage. No SDK/workload was installed or
modified, and no root SDK configuration was changed.

Release does not reference the binding, compile its inspection startup code, bundle
the native agent or its notices, or merge the Debug network-permission plist.
Build and restore Release without an `AilohaPackageRoot` property to verify
that this dependency is genuinely absent.

## Inspection

Debug listens on port **9243**, with no fallback. Startup errors are logged
through `ILogger<T>` and displayed instead of being reported as success.
The broker session ID includes `SIMULATOR_UDID` when running in a simulator.
Only use this development inspection surface in a trusted development setting.

Use the installed Ailoha CLI with explicit `-p ios -ah 127.0.0.1 -ap 9243`.
Inspect the tree before actions; the drink BAG control is `tid:drink.bag`,
the form fields use `tid:bean.*`, and bottom actions use `tid:nav.*`.
Use the current tree's **UIWindow ID** with `ui screenshot --id` for a full
app-view capture: the CLI's unscoped iOS screenshot shortcut can select another
booted simulator even when an agent endpoint is specified. An app-window
capture does not include system status-bar chrome.

The Swift agent's scroll implementation accepts deltas, not the CLI's
item-index options. A successful item-index acknowledgment does not prove
movement: read native bounds and realized rows. Activity cells expose native
accessibility activation as well as standard collection selection. All
source-backed lists use `UICollectionView`, matching MAUI's iOS backend.
Categorical rows and Activity rows are measured from their content; numeric
rows retain the source 72/96-point choices. Picker viewports consume the safe
area once, and centering waits for nonzero layout.

Feedback uses an app-window overlay, not a page-owned banner. Its shape,
semantic icons, theme, top inset, 500ms source easing animations and 2000ms
visible interval follow the approved toast-lifecycle correction: the hold
starts after appearance, not after `PushAsync` has waited for dismissal.
The window host survives form pop/navigation. Physical popup-input checks
through the approved native driver are recorded separately; keyboard/VoiceOver
focus equivalence remains under review. The retained-MAUI fixture rebuilt with
the approved toast correction has separate D3 comparison evidence. This is not
full focus or visual acceptance.

### Range settings

Settings exposes Dose In, Yield, Grind Size, and Time range pages. Each metric
has Auto/Custom modes and the source-ordered per-method list. Unedited methods
show automatic fallback in Custom mode; switching to Auto retains overrides.
The list subscribes only while visible and refreshes again on navigation return.

The native editor delegates parsing, canonical precision, dirty state, and
validation to Core `RangeEditorDraft`. It preserves the source guidance, fixed
Cancel/Save actions, scrolling, and native confirmation wording. Cancel and the
left-edge back gesture share the dirty-draft guard. Existing-override
Use Recommended and reset-all require their source confirmations; no-override
Use Recommended changes only the draft text. Persistence errors remain visible.

Range work does not add media, voice, other Settings features, or a second
range parser. New preference values are exercised only in the dedicated
`com.simplyprofound.baristanotes.nativeranges20260926` test app, not the
demonstrated final-pair sandboxes. Destructive confirmation outcomes and
corrupt-preference injection require a separately authorized test. Do not
infer physical back-gesture, keyboard, or screen-reader coverage from metadata.

### Time and temperature

Time and Water Temperature use one source-shaped numeric collection page backed
by Core `NumericPickerState` and the existing reusable `ChoiceList`. Initial
selection, subsequent selection, and endpoint centering use the established
safe-area viewport and content-inset mechanism. The range strip separates its
background from the source's inset 44-point button. Selection stages a value; Close discards it, and
range toggling alone preserves the original value on Done.

Time reads its effective per-method range at open, starts from ActualTime or
ExpectedTime, and commits both fields. Duration labels use `DrinkDisplay`.
Temperature stays nullable Celsius in the draft and stored record. Settings
exposes the normal Fahrenheit/Celsius tiles; the drink page rereads that
preference on return without converting the stored Celsius value. Fahrenheit
conversion on Done uses `DrinkDisplay.FahrenheitToCelsius`.

The temperature picker refreshes the shared state with its staged value as the
original, matching the source WaterTempPicker caller. Exact off-step values use
distinct automation IDs even when their visible Fahrenheit labels round to the
same integer. No Grind, media, voice, or theme functionality is added here.

Recovery evidence distinguishes behavioral checks from visual comparison:
earlier builds staged the correct values but did not recenter a tapped row.
Those historical captures are not current visual evidence. The recovered
fixture's existing saved drink and custom ranges are retained; checks use UI
actions and read-only storage corroboration, not database or preference
seeding. Full application acceptance, complete adaptive/accessibility coverage,
physical back-gesture testing, and formal shared evidence binding remain open.

### Equipment and Grind group

Equipment management now uses native recycled two-line rows and a UIKit form
backed by Core `EquipmentDraft`/`EquipmentWorkflow`. All five source types,
name/type/notes fields, inline validation, empty-selector presets and immediate
Machine/Grinder selection/Clear are wired. Creating equipment does not select it.
The DELETE action opens a separate centered SimpleAction-style confirmation;
its primary path calls Archive, never physical deletion. Current runtime
verification covers Cancel/backdrop, not positive archive or saved-note clearing.

Grind uses `GrindPickerWorkflow`, `GrindPickerState` and `DrinkDisplay.GrindBadge`.
It keeps source preferred/full ranges, 50-micron outside steps, exact/current
values, variable row emphasis, header and sticky badge. Source font scaling
applies to management captions, values and name entries too; only the adaptive
tile's decorative glyph opts out.
Callbacks use weak ownership, and operation guards serialize form saves.
Equipment saves await the existing native feedback lifecycle before returning.

The pinned editor implements an Accessories picker but has no rendered entry
tile. Native picker support is retained without adding a new product tile.
Its toggles/Close/Clear/Done were checked using a session-only diagnostic entry,
not represented as a completed production navigation route. Unknown-grinder
scale setup likewise preserves the documented source route gap rather than
inventing a calibration editor. Source-routing decisions and remaining
media/fault/physical/accessibility limits are recorded in the workstream report.

### Profile management and photo library

Profiles now opens the shared `ProfileDraft`/`ProfileWorkflow` form for add/edit.
The source name/context fields, 2000-character counter, service validation,
Save-first photo hint, staged preview, saved circular avatar, Change Photo,
conditional Remove, spinner and inline errors are implemented. Name entries,
management labels and ordinary labels/buttons use DefaultMetrics; decorative
chevrons retain the source's explicit scaling opt-out.
List/detail read failures expose retry, without treating an unloaded profile as
safe to save or delete.

Existing-profile image changes call the existing image service independently
of text Save/Cancel. New staged images are saved only after details assign the
profile ID. A failed image retains that ID and its bytes; retry updates the same
profile. Delete reuses the centered SimpleAction family with Profile-specific
text, and remains permission-limited in testing. New profiles still do not
auto-select a maker or recipient.

The photo-library implementation uses public `PHPickerViewController` bindings
with ImagesFilter and SelectionLimit1, not a MAUI control or a camera workflow.
It requests no new photo-library authorization. The source's UIKit-first400/85
JPEG processing and non-UI Graphics fallback are preserved, followed by the
existing12MB byte-only validation. The shared original-byte fallback and
delete-old-before-new policy were deliberately not changed.

Current evidence includes real app-scoped generated-image staging, a controlled
first-write failure/retry with one profile ID,400×300 JPEG output, and target
font/lifetime checks. The library picker was not exposed usefully by current
Ailoha capture/accessibility, so actual library selection and physical-device
photo behavior remain unverified. No user photo library was modified or existing
avatar replaced/removed during verification. Diagnostic entry/fault code lives
outside the repo and is absent from ordinary builds.

### Beans and Bags

Settings now opens native Beans management, Bean detail and Bag detail. Empty
BAG still enters manual creation, refreshes through the original read/retry
path and does not auto-select the new bag. Forms use Core `BeanDraft`,
`BeanWorkflow`, `BeanCreationWorkflow`, `BagDraft` and `BagWorkflow`, with
UI-thread input snapshots before scoped background work. Cancel remains
available while writes are pending; late results cannot navigate a replacement
page. Bean creation retains separate bean and initial-bag outcomes.

The detail view shows all bags, rating aggregates and recycled rich shot cards.
History uses20-record pages, threshold5 and the source420-point inner collection.
Bag child navigation preserves the parent draft, paging and inner scroll;
reappearance refreshes only bags and recipes. A shot opens its actual edit
route. Recipe reads, source badges, parameters and grind translation are
implemented without an invented Apply/editor. The four source roaster adapters
registered by Core remain available, including the working Onyx adapter. The
shared null AI recipe generator remains unavailable. No new recipe provider was
invented, and runtime checks have not fetched a live product guide.

Bag date entry uses native date wheels capped at today, with the source
date-only form validation and separate exact-timestamp service validation.
Notes enforce500characters. Status changes persist independently of Save.
Delete uses the reviewed centered confirmation and existing shared service
behavior, without a cascade-policy repair.

Bounded verification includes new synthetic bean/initial-bag and second-bag
creation, validation/Cancel/restart, no-auto-selection/read retry,22 UI-created
shots,20+1 paging, rich V60 history, ratings and delete Cancel/backdrop. The
source/retained forms were checked using unchanged isolated binaries.
Positive deletion/status changes and saved-content clears were not performed.
Actual date adjustment, populated-provider/recipe cases, Beans-specific
enlarged-font/lifetime fault matrices, physical back gestures and full visual
acceptance remain explicitly open; compilation and target observations are
not substitutes for those checks.

### Saved-drink advice and appearance

The edit-only AI action sends the saved shot ID to the shared `IAIAdviceService`,
not the editable in-memory draft. It retains the source ten-second cooperative
request token and response/error distinctions. Navigation invalidates late
callbacks, cancels/disposes the token and removes the four-point loading strip.
Only the source primary feedback message is displayed; recovery details are not
concatenated into new error copy.

AI Suggestions uses a separate fixed-dark ActionModal-style presenter, with
300ms bottom/CubicOut entry and400ms bottom/CubicIn exit, actual top/side safe
insets,24-point design padding and bottom opt-out. The body is capped at
max(240,60% of full logical display height). Close remains fixed while the stable
prompt toggle expands/collapses the inner scroll content. Underlying input and
accessibility are blocked until dismissal.

The native head supplies standard `IConfiguration` and calls shared
`AddBaristaNotesAI`. The bundled base JSON has the source `AzureOpenAI:Endpoint`
and `AzureOpenAI:ApiKey` keys, both empty. When the ignored MAUI development
file exists, the native build bundles it as `appsettings.Development.json` and
loads it after the base JSON in Debug and Release. Debug can then override it
with an optional file of the same name from the absolute
`NativeServices.DataDirectory` (this app's Library sandbox). This personal
Release configuration embeds the development credential in the application
bundle and must not be used for public distribution.

On iOS26+, the approved `Microsoft.Maui.Essentials.AI` public
`AppleIntelligenceChatClient` is registered directly. Construction is not a
model-availability check; the shared service retains its local-disable/cloud
fallback behavior. UI fixture tests are explicitly not real-model proof.
The later Voice group below supplies a separate scoped session/adapter; the
shared local-voice branch remains disabled.

Settings includes source-ordered Appearance, Manage, Units, Value Ranges and
About sections. Light/Dark/Auto uses shared `ThemePreference` and applies
`OverrideUserInterfaceStyle` to the existing app window only. Controllers,
drafts, filters, scroll and focus are not replaced. UIKit dynamic colors retain
semantic roles; individual layer owners resolve their named border colors
again during layout. Fixed-dark modal chrome does not follow page appearance.
Voice/camera UI remains outside this group.

The accompanying Beans review corrected four earlier issues: Bag detail now
uses an explicit associated-row count (including unrated and soft-deleted rows,
while ratings keep their live-only policy); failed Beans-list reads survive
font changes with Retry intact; empty recipe lookup uses Information feedback;
and the native head no longer removes the existing source recipe adapters.
Historical reports/captures are retained with a separate correction record,
not relabeled as passing.

### Native Voice

New Drink's mic opens the used source window-overlay flow in Ready, without
starting recognition. Settings/Activity route to New Drink before opening it.
Minimize/outside collapse to the56-point FAB; expanding preserves the current
conversation. The window-owned UIKit overlay survives navigation and does not
reset underlying forms. Expanded input/accessibility is isolated; collapsed
hit testing passes through everywhere except the FAB.

The native panel uses source gray/orange colors, a420-point content region plus
the actual window bottom safe inset,20-point corner/padding geometry,80-point
microphone with15-point hit expansion and fixed source text sizes. Small native
shape layers draw the microphones. Native CoreText-backed labels match the
actual pinned Graphics font-name resolution/alignment: that source backend
ignores its requested weight/italic flags for `Arial`. Voice text intentionally
does not adopt page Dynamic Type scaling.

iOS recording is press/hold/release via a zero-duration long-press recognizer,
not Android tap-toggle. Partials update inline on the main thread and dispatch
only when received off-main. Release captures the received partial text before
cancelling listening. Natural final completion retains the source recording
latch until release; final/partial/stop generations prevent double commands.
The source1.5-second silence stop and60-second recognition budget are retained.

`NativeSpeechRecognitionService` uses SFSpeechRecognizer/AVAudioEngine directly:
CurrentCulture, partials, Toolkit15 PlayAndRecord routing options, owned input
tap/request/task/engine, cancellation and audio-session deactivation. Permission
descriptions match source. Ready never requests permission; an actual user
press may request undetermined speech/microphone authorization. Automated tests
did not start real recognition or grant permissions.

Permission preflight preserves the source's precise ordering: both granted or
either unknown permits the start attempt, including mixed denied/unknown and
restricted/unknown statuses. Native status mapping and all16 combinations are
tested separately; preflight acceptance is not a permission grant.

Each Show creates a scoped shared `IVoiceCommandService`, clears its history and
keeps its35 generated tools unchanged. Close retires UI/listening; already-
dispatched processing uses the source `CancellationToken.None` and retains its
scope until completion, without claiming rollback. The shared40-message history
and cloud-only voice branch are preserved; registering the advice Apple client
does not enable local voice tools. Empty cloud configuration returns unavailable.

Scene disconnect synchronously detaches the overlay/gesture, restores
accessibility and removes the old coordinator's notifier subscriptions. The
disconnected controller waits for dispatched work before disposal, while the
same service provider may attach one new scene window. Attachment generations
reject callbacks from old controls; a late disposal cannot detach the new
overlay. App-scoped tests invoke the actual delegate handlers, not an OS scene
destruction/reconnection, which remains a separate hardware/lifecycle check.

Retirement guards old overlay replies, not inherited platform tool actions.
An already-dispatched tool may still navigate the currently attached window
after reconnection. No new session-bound cancellation policy is implied.

The platform adapter queues real generic/typed navigation independently of
speech-state dispatch, refreshes drink reference lists through the shared data
notifier, and exposes native camera/browser operations only to explicit tools.
Simulator capture is unavailable even when UIKit advertises a camera source.
Camera CODE supports source1024/70 options and one-shot temporary read-stream
ownership; real camera/browser and permissions remain unverified.

Runtime evidence separates ordinary Ready/collapse/Close/navigation from a
build-only scripted speech + real shared engine/Azure-SDK loopback fixture.
The latter exercised partial/final ordering, history40/reset, a new synthetic
profile through the actual generated tool, scoped retirement and navigation.
Those tests are not physical hold, SFSpeechRecognizer transcription or live
provider proof. No general photo-intent/AddCoffee workflow is added here.

### General photo intent and Add Coffee

New Drink's camera uses the existing native capture implementation with the
source general1024×1024/70 options. A single original-editor session owns
capture, classification, processing input/accessibility isolation and routing.
Source `PhotoWorkflowRules` chooses only obvious successful known intents
automatically; otherwise a fixed-dark Use This Photo ActionModal offers the
four source choices. Retake follows modal exit. Profile opens a NEW staged
form; Room retains its native source alerts and exact analysis question.
Canceled/retired owners cannot present on a replacement scene.

Add Coffee is a separate ActionModal, not a full-page editor. It retains
source Type/Browse/Scan doors, recent6 cards, one-time extraction prefill,
top-three chips,300ms fuzzy lookup and Today-limited date entry. General-photo
prefill always selects Type initially. Raw fields use the actual source
14-point system font, not Voice Arial or an assumed Manrope default.
The package's AvoidKeyboard=false default is preserved.

Type writes an input snapshot through shared `AddCoffeeWorkflow`: trimmed
name/roaster/origin, bag-only notes and explicit date, separate Bean/Bag
outcomes. Browse/Use it creates only a Today bag and ignores form notes/date.
Success is haptic → modal exit → original-owner bag insertion/selection →
release; other drink values are preserved. Active exit/callback failures are
handed to owner feedback before release. Fault teardown does not mark user
cancellation. True Cancel and scene retirement still suppress callback/error;
no automatic write retry or rollback is claimed. The IOS-PH01 repair checks
the actual exit catch by injecting a false result after the native animation
await, for both Type and Browse. It checks one error request before one release,
zero OnCreated calls, and cancellation, scene retirement and presenter faults
without new writes. This is not real OS animation-interruption delivery.
Earlier completion-exit evidence covers only a wrapper failure before exit.
The modal retains its original underlying view until accessibility is restored,
then releases that reference. Cleanup checks also cover managed collection;
they do not depend on a weak UIKit wrapper surviving until exit.

Label Scan is title-only “Photograph bag label,” not forced through the general
transform. Available original file bytes are retained; the source-style UIImage
fallback uses lossless PNG when no transform is requested. Scan extraction
keeps the source noncancelable provider token while stale UI results are ignored.
No second camera implementation or real-library data fixture was added.

Ordinary unavailable-camera checks and labeled scripted capture/vision/native
UI checks are recorded separately. New synthetic Bean/Bag writes used actual
forms/workflows; no DB seeding, existing data clearing or real media/provider
request was used. Initial source popup comparison used native bounds/font
inspection; Shell-scoped screenshots contained only the underlying page.
Later full-display source Intent and automatic-Coffee captures are available,
but their OS chrome and empty data differ from the populated app-only target
captures. These are not matched full-image pairs. Real camera output/permissions,
full keyboard/physical behavior, some fault branches and acceptance remain open.

### Controlled Debug checks

`BN_TEST_BAG_FAILURE_AFTER_CREATE=1` fails one bag read after successful
UI creation. `BN_TEST_ACTIVITY_DELAY_MS` (1–30000) delays the first completed
Activity read so a later filter request can be applied while it is pending.
Both hooks log their use and are excluded from Release.

For repeatable checks without clearing populated sandboxes,
`BN_TEST_OPEN_FORM=bean` or `profile` opens the actual form after startup,
without filling it or invoking any service write. This Debug-only route is
restricted to the already installed `nativetoastd3`/`nativetoastd3b` test IDs.
All synthetic data must still be entered and saved through the app UI.

Filter enters from below for 300ms with CubicOut and exits below for 400ms
with CubicIn. The travel distance follows UXDivers 0.9.4's
`(popup height + content height) / 2` rule. Popup controls and callbacks are
disabled during both phases while the full-screen host continues to consume
input. Filter results are applied only after exit completes. Colors remain
stable during interaction gating, as the source disables input rather than
restyling the popup.

Do not use `UIButton(UIButtonType)` from a UIButton subclass. That convenience
constructor wraps a factory-created base instance and bypasses native
subclass overrides. The custom form/navigation/chip controls use the default
subclass constructor.

The official **v0.1.13** artifact reports its internal agent version as **0.1.0**.
Broker metadata reports the hostname as device ID, so correlate the simulator
UDID in the session ID, explicit install/launch target, OS, and app ID rather
than treating the hostname as a simulator identifier.
