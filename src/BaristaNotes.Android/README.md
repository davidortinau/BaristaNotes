# Native Android first slice

This is a C# .NET 11 Android Views application, not a MAUI application.
Its package is `com.simplyprofound.baristanotes.nativeapp`, with its own sandbox.
The minimum supported Android version remains API 24.

## Beans map (coordinator validation pending)

### Round 30: mixed-country test viewport and simulator signing

The mixed-country rendered-feature test now explicitly overrides its
tile-derived zoom bounds only in the tile-disabled test session. It checks
the effective viewport and unchanged 64-DIP distances before asserting
**Origins (3)**, country/location membership, deduplicated selection and
repeat clearing. Runtime Core/Android behavior and the iOS lifecycle fix
are unchanged. The iOS README's **Round 30** section provides coordinator-only
focused tests, both Debug builds and the outstanding Duo lifecycle scenarios.
All simulator build recipes now use SDK signing
`EnableCodeSigning=true` and `CodesignKey=-`.
Keep Android checks on **emulator-5556** only and preserve all five samples,
caches, compact labels, one-tap navigation, camera and accepted geometry.
No new build, test or runtime verification is claimed by this file-only worker.

### Round 29: compact selected-origin header

The selected count below **BEANS** now uses the same Core title rules as pins:
**Guji (1)**, **Ethiopia (1)**, same-country **Ethiopia (2)**, or mixed-country
**Origins (3)**. Counts still deduplicate bean IDs, including blends and archived
members. The unfiltered count and **All beans** action are unchanged. Precision
and region information remain in place metadata/map accessibility, not the
visible header. There was no separate explanatory summary row to remove;
no empty line or extra summary space is added. Offscreen legacy-footer
measurements are retained solely to preserve the accepted hero edge/parallax.

Focused tests now assert the exact shared visible selection label, fallback,
all resolved precision kinds, same/mixed-country counts, archived blend
deduplication, repeat clearing and cached restored selection. Existing compact
marker assertions remain. Coordinates, identities, clustering threshold,
filtering, gestures, camera, attribution, portrait layout and row navigation
are unchanged; the iOS disposed-redraw guard is retained.

The iOS README's **Round 29** section supplies coordinator-only focused-test
and both Debug build commands. On **emulator-5556** only, compare marker/header
text for the existing Ethiopia aggregate and separate Guji/country pins,
repeat-clear each and use **All beans**. Check a mixed-country cluster without
adding records, cached Detail/Back/reopen, one physical row tap, keyboard/
TalkBack, safe pinned title/first row and full scroll/reverse in both themes
and large text. Retain all five samples and caches; no resets or new fixtures.
Public-service checks remain serial across prototypes; per-process throttling
is not production-wide rate control. This file-only worker has not run builds,
tests or runtime checks.

### Round 28: compact native marker labels

Shared Core now uses country/place plus count for visible pins: **Ethiopia (2)**
at wide zoom, **Guji (1)** and **Ethiopia (1)** when separated. Same-country
clusters use the country name; mixed-country clusters use **Origins (count)**.
Precision/region prose stays in accessibility and place metadata,
not visible labels. Coordinates, deduplicated counts, selection toggles, geocoding
cache/privacy/rate limits and camera are unchanged. Focused `BeanPlaceMapTests`
cover compact labels for each precision, clusters/blends and actual feature text.
The stable Android map host, single-tap row handling and accepted page geometry
are untouched.

The native iOS README's **Round 28** section records the independent UIKit
queued-redraw correction, exact upstream 5.1.0 source, fresh focused-test/both
Debug build commands and required navigation/rotation scenarios. For Android,
use only **emulator-5556**, preserve the five existing UI-created samples,
and check compact labels, place/aggregate repeat toggles, one physical row tap
on first visit/after scroll/after filter/after Back, keyboard/TalkBack,
camera/selection retention, parallax/safe pinned title, attribution, both themes,
large text and cached/error fallback. No reset or new public-query fixtures.
No builds, tests or runtime checks have been run by this file-only worker.
Public-service checks remain serial across prototypes; per-process throttling
is not production-wide rate control.

### Round 26: explicit country components and throttle-test isolation

Comma-bound country components now take precedence over country words inside
place names. `Port of Spain, Trinidad and Tobago and Ethiopia` retains the
complete `port of spain` detail, recognizes only Trinidad and Tobago/Ethiopia,
and does not fabricate a Spain origin. Explicit country-only comma blends and
ambiguous detailed blends retain their prior fallback behavior. No geocoding
coordinates, navigation, camera, geometry or saved data are changed.

The two test classes using the static Nominatim request gate share one xUnit
nonparallel collection. This isolates handler/clock observations from the
formerly parallel place journey and unrelated tests without resetting static
state, changing the process-wide gate or weakening the >=1-second assertion.
Full-suite timing results remain coordinator-owned and pending.

Run the focused-test and both Debug build commands in Round 24 below with fresh
**round26** artifact paths. Repeat the entire focused suite, not only the timing
test, to check interference. Runtime: retain the five existing samples and
recheck real Guji region/country pins, zoom separation, repeat toggles and
one-tap Detail/Back with camera/geometry unchanged on emulator-5556 and owned
iOS comparison simulators only. Port of Spain parsing, exact country counts
and ambiguous-blend fallback are covered by source tests without public queries
or additional device records. Unresolved lookups must still show recoverable
approximate fallback. Do not change existing samples or reset caches/data;
exercise public lookups serially.
This file-only worker has not run builds, tests or runtime checks.

### Round 25: bounded validation-fallback and country-alias correction

The four reported failures are implementation defects, not invalid fixtures.
`InvalidDataException` is explicitly recoverable: unsupported country restrictions,
invalid endpoint settings and rejected cached results now reach the existing
logged, clearly approximate fallback paths without weakening parser/settings
validation. Corrupt cache still suppresses automatic queries until explicit retry.
Country matching now shares its existing longest non-overlapping spans with blend
splitting, so `and` inside a catalogue name/alias cannot create a fake detailed
query or break an otherwise explicit place/country pair.

Focused assertions retain all four original cases and add country-name/blend
conjunction coverage, exact zero requests for unsupported restrictions, separate
endpoint cache files/reuse and query/fragment/credential rejection. Navigation,
geometry, camera, data and SDK integrations are unchanged. Only source inspection
was performed; builds, focused tests and runtime checks remain coordinator-owned.

Run the Round 24 focused-test and both Debug build commands below with fresh
**round25** artifact paths. Runtime checks on the same owned maps targets: country
aliases generate no search; unsupported restrictions and invalid settings show
recoverable approximate fallback without a request; corrupt cached country results
show fallback without automatic querying, then recover on explicit retry; changing
between valid HTTPS endpoints uses distinct caches and returning reuses success.
Retain the existing five samples and recheck one-tap Detail/Back, wide/split pin
selection, camera retention and accepted map/header geometry. No reset or migration.

### Round 24: precise places and zoom clusters

This supersedes the historical country-only behavior below. Both heads now use
one Core geocoder, place model and cluster/selection implementation. The
Round 23 `RangeTile` first-tap fix is retained. No database records, schema,
origin text, names or accrued usage are rewritten.

Country-only origins retain the original approximate Natural Earth coordinates
and make no search request. Detailed origins query Nominatim with only detailed
geographic text and the recognized country, restricted by ISO country code.
Bean names, IDs and device location are never sent. Empty origins and details
without a recognized country remain unplotted. Catalogue names, aliases and
coordinates are unchanged; ISO codes come from the supplied public catalogue,
with standard `no`, `fr`, `tw` replacing its non-ISO Norway/France/Taiwan values.
Northern Cyprus, Somaliland and Kosovo have no supported restriction here;
their detailed lookups report explicit country fallback instead of accepting
unvalidated results.

jsonv2 results must have finite coordinates, matching `address.country_code`,
a matching named place, sub-country `place_rank` and city/locality or region
precision. The public Guji `state_district` result is **Guji region, Ethiopia**,
not a city. Its reference coordinates occur only in parser/HTTP tests, never
production code. Explicit blends separate place/country pairs with `/`, `;`,
`|`, `+` or `and`; ambiguous detailed associations show approximate-country
fallback and a corrective error rather than guessing.

Versioned cache keys include endpoint, ISO code and normalized geographic
detail. Successful raw results are validated again on cold reads. Atomic
app-local files in `origin-geocode-cache` persist successes and failures.
Failures suppress automatic uncached retries across view/restart.
**Retry origins** appears only for recoverable lookup/cache failures and
does not re-query persisted successes. Cache-write failures are logged and
displayed; same-process repair reuses a successful response without re-querying.
All lookups run off the UI thread, one serial request at a time across this
process, >=1 second between starts, with a 12-second throttle/network/body
timeout, 128-KiB response limit, cancellation, `Retry-After` handling and no
redirects. No polling, autocomplete, geographic dataset download or proxy.

Default search: `https://nominatim.openstreetmap.org/search`; identifying
User-Agent: `BaristaNotes-Native-OriginPrototype/0.1`. The existing clickable
OSM copyright widget also identifies Nominatim search, without a duplicate
copyright control. See [public policy](https://operations.osmfoundation.org/policies/nominatim/)
and [Search API](https://nominatim.org/release-docs/latest/api/Search/).
This is a **single-user, low-volume prototype**: per-process throttling is not
production-wide rate control. The coordinator exercises only one new
geocoding client/prototype at a time.

For a runtime service change, atomically replace `origin-geocoding.json` in
Android's existing `FilesDir` (iOS: existing Library directory):

```json
{"endpoint":"https://nominatim.openstreetmap.org/search","userAgent":"BaristaNotes-Native-OriginPrototype/0.1"}
```

Settings are reread on every lookup, including cache hits; no rebuild/restart.
Endpoint must be an absolute HTTPS search URL without query, fragment or
credentials. Optional User-Agent must remain a valid identifying BaristaNotes
header and may include coordinator-supplied contact information. Endpoint
changes use a separate cache namespace. Correct settings and Retry origins,
or reopen Beans, to update already displayed places. Invalid settings send no
request and show recoverable feedback.

Clustering is app-defined connected-component grouping at **64 screen DIPs**,
using verified Mapsui 5.1.0 `Navigator.Viewport`, `Viewport.Resolution`,
`Viewport.WorldToScreen` and `Navigator.ViewportChanged`, not a built-in switch.
Only aggregate marker centroids change; actual geographic coordinates do not.
Wide Ethiopia/Guji shows two beans; sufficient zoom separates two one-bean pins.
Stable location IDs drive place-specific selection, repeat-to-clear, screen
recreation and Detail/Back. Clusters, totals and filtered rows deduplicate bean
IDs, including blends. Resolution/zoom never refits the explored camera.
Resolution completion updates selected header labels without rebinding unchanged
bean rows in either head, avoiding interference with a live tap or keyboard focus.

Accepted map-first spacing, clipped 20% parallax, permanent Android GPU host,
safe compact pinned header/separator, portrait-only mode and bottom navigation
are unchanged. Rich accessibility text is deliberately separate from the old
offscreen footer measurement so hero geometry stays unchanged. No permanent
summary caption is restored. Only genuine errors and conditional actions show.

Only the coordinator executes Round 24 validation from the export root:

```sh
dotnet test src/BaristaNotes.Tests/BaristaNotes.Tests.csproj --filter "FullyQualifiedName~NominatimOriginGeocoderTests|FullyQualifiedName~BeanPlaceMapTests|FullyQualifiedName~BeanOriginMapTests|FullyQualifiedName~BeanMapSessionTests|FullyQualifiedName~BeanPageGeometryTests" --artifacts-path <fresh-round24-native-tests>
dotnet build src/BaristaNotes.Android/BaristaNotes.Android.csproj -t:SignAndroidPackage -c Debug -r android-arm64 -p:NativeMapSlice=true -p:EmbedAssembliesIntoApk=true -p:AilohaArtifactsDirectory=<existing-agent-artifacts> --artifacts-path <fresh-round24-android-maps-debug-artifacts>
dotnet build src/BaristaNotes.iOS/BaristaNotes.iOS.csproj -c Debug -r iossimulator-arm64 -p:NativeMapSlice=true -p:EnableCodeSigning=true -p:CodesignKey=- -p:AilohaPackageRoot=<existing-agent-package-root> --artifacts-path <fresh-round24-ios-maps-debug-artifacts>
```

Focused tests use controlled HTTP handlers, not public-service requests. They
cover Guji parsing and rejected precision/country/coordinate mismatches,
request privacy, durable success/failure cache, endpoint changes, corrupt
cache, retry/cache-write repair, cancellation, serial requests and measured
one-second spacing. The complete place journey covers five unchanged samples,
low/high zoom, place/country/aggregate toggles, cached restart, screen recreation
and camera retention. Existing map/session/geometry regressions are included.
Builds, tests and device checks have **not** been run by this file-only worker.
NativeAOT remains a separate gate.

Install over only existing maps comparison apps without clearing data.
Android: BaristaNotesMaps20261002 (`emulator-5556`), never Pixel 5 or
`emulator-5554`. iOS: owned comparison simulators only. Runtime scenarios:

1. Cold-open with real public search and the five existing UI-created samples:
   four mapped, one empty-origin unplotted, Colombia one, Brazil one, Ethiopia/
   Guji two in aggregate. Zoom to distinct **Guji region** and **approximate
   Ethiopia** one-bean pins; coordinates stay unchanged by zoom.
2. Select each precise/country/aggregate pin and repeat to clear. One physical
   tap on an unfocused bean row must open the correct detail on first visit,
   after scrolling, after each filter and after Back. Check Android keyboard/
   D-pad/TalkBack and unchanged iOS single-tap/VoiceOver activation. Recheck
   equipment/range controls sharing the retained RangeTile fix.
3. Explore camera, scroll the map fully offscreen and reverse, then Detail/Back
   and reopen/restart. Preserve camera, selected place, list anchor, initial map
   edge, safe pinned header/separator and bottom navigation in both themes,
   including short/filtered content and large text.
4. Reopen/restart offline after success: precise pins reuse persisted lookups.
   For an uncached detailed origin exercise unavailable service, timeout,
   invalid JSON/country/coordinates and cache-write failure. Clearly approximate
   fallback, explicit errors and list/detail stay usable. Restore service and
   explicitly Retry origins without resetting camera; cached failure must not
   silently retry on reopen/restart.
5. Change sandbox endpoint while running, including invalid settings/recovery.
   Check identifying header, country restriction, no bean names/location,
   one in-flight query, >=1-second starts, timeout/cancellation when leaving,
   no late UI update and readable clickable attribution.

Beans has a Mapsui.Android 5.1.0 map first, above its existing heading, including
list loading, empty and error states. Core's `BeanMapSession` configures one
default OpenStreetMap tile layer and the identifying
`BaristaNotes-Native-Maps/0.1` User-Agent for both native heads. Mapsui handles
visible-tile fetching, cartographic rendering and pan/zoom. Round 13 moves the
hero's top into the former title space while approximately preserving its lower
edge. The existing RecyclerView owns a height-only hero placeholder, title,
virtualized bean rows and state/trailing-space row as one scroll sequence.
Round 15 keeps the hardware map attached to a permanent page-owned host, outside
recyclable holders. The native scroll listener translates that host upward by
the placeholder's scroll offset, clipped at the page edges. The existing map
surface translates downward by 20% of that offset inside its own clipped
container, preserving the 80%-speed parallax without recreating, re-fitting or
resizing the map per frame. Collapse never hides or detaches the hardware view.
Unhandled non-map drags are forwarded to RecyclerView in page coordinates;
the real map and All beans control retain their normal touch handling.
The title uses its original adapter-row placeholder and moves into a native
pinned title host only after reaching the usable top. Reverse scroll releases
it and reveals the same map. Minimum trailing space lets empty/short and filtered
lists remove the entire hero and pin the title too. The source fonts, palette and row design are unchanged, and bottom navigation
stays outside the scroller.

Round 16 is a spacing-only correction. Beans opts out of the existing
`EdgeAwareColumn` top avoidance; its side/bottom insets are unchanged.
The actual map starts at physical y=0 behind transparent status-bar chrome.
The old hero geometry, minus the former summary and idle control row,
preserves the approximate visible map lower edge. Removed-footer typography
is measured offscreen for width/font scaling; no summary view or idle control
row is retained in the hierarchy. Genuine origin-loading and error feedback
still appears transiently. The existing All beans action is retained inside
the map, above attribution, only while a country is selected; it reserves no
footer space. Mapped/unmapped/approximate-origin information remains in Core
and the map's accessibility description rather than a permanent caption.

The Beans-only header uses natural label heights, 12-DIP vertical padding,
a 4-DIP label gap and a 1-DIP Outline separator carried with the pinned header.
Its existing host now pins at the status/cutout inset, filling that inset only
when pinned, so labels and row input remain below system icons. Expanded map
icons are dark over the unchanged light cartography; pinned icons follow the
app palette. Leaving Beans restores the existing window treatment.
The attached hardware surface, 20% clipped parallax, scroll direction,
virtualization, filter/camera state and portrait-only paths are unchanged.
Shared `BeanPageGeometryTests` covers upward expansion/lower-edge geometry,
compact multi-line label sizing and safe-edge pin/release thresholds.
Round 16 builds and device checks are coordinator-owned and pending.

Round 20 corrects Android's collapsed first-row overlap. As the title placeholder
travels from the safe top to y=0, its reserved height grows by that overlap,
up to the status/cutout inset. At full hero collapse the first bean starts at
the pinned separator's lower edge; further scrolling can move rows beneath it.
The reservation returns to zero on release, so the expanded header has no
extra safe-area gap. Short/empty-list trailing space shrinks by the same amount,
preserving the full-collapse scroll range. Existing holder parameters are
updated in place. The permanent GPU host, hero height/parallax, camera/session,
footer/navigation and iOS source are unchanged. `BeanPageGeometryTests` adds
full-collapse row/separator and continuous inset/release regressions.
Builds and real-frame checks remain coordinator-owned; no runtime verification
is claimed for this correction.

Round 23 corrects Android's first-tap row activation in the shared `RangeTile`.
Its overlay Button stays focusable for keyboard/D-pad navigation, but is no
longer focusable in touch mode: Android's normal touch dispatch can click an
unfocused row instead of consuming the tap to focus it. Bean and equipment
adapters and range-setting rows use this same control; their existing click
callbacks, focus indicators, enabled states and accessibility descriptions
are unchanged. No touch handler, synthetic click or second navigation call is
added. iOS, map/geocoding logic and accepted scroll geometry are untouched.

Only the coordinator builds and checks this correction:

```sh
dotnet build src/BaristaNotes.Android/BaristaNotes.Android.csproj -t:SignAndroidPackage -c Debug -r android-arm64 -p:NativeMapSlice=true -p:EmbedAssembliesIntoApk=true -p:AilohaArtifactsDirectory=<existing-agent-artifacts> --artifacts-path <fresh-round23-android-maps-debug-artifacts>
```

Install over only the existing maps comparison app without clearing data.
On BaristaNotesMaps20261002 (`emulator-5556`, never the physical Pixel 5 or
`emulator-5554`), use physical-style touch input, not automation `PerformClick`,
to tap an unfocused bean row once on first visit, after scrolling, after map
filtering (especially Guji in the Ethiopia-filtered list), and after Detail/Back.
Each tap must open the correct detail exactly once. Repeat with the distinct
Guji and approximate Ethiopia selections and their two-bean aggregate; pin
toggles, list anchor and explored camera must remain unchanged. Check D-pad/
keyboard focus and activation, TalkBack activation, and scroll drags without
accidental navigation. Check equipment and value-range rows using the same
control, including enabled/disabled range actions. Preserve the five existing
UI-created samples, accepted pinned-title/parallax behavior and bottom navigation.
iOS single-tap navigation remains a coordinator regression scenario, not an iOS
source change. Build and runtime verification remain pending.

Round 14's holder-metadata fix is retained: each page holder's root wrapper stays
owned by RecyclerView. Round 15 moves only inner title/state content when binding
a replacement holder; the hero wrapper remains empty.
Viewport sizing updates existing RecyclerView layout parameters in place,
preserving holder metadata, and requests layout without a data-set notification
from the page layout pass. Bean-row layout parameters are initialized only at
holder creation, not replaced during rebinding. The coordinator confirmed
Round 14 launch, intermediate parallax and pinned-title states, but reverse
scroll crashed in Skia's hardware renderer after map detachment/reattachment.
Round 15 addresses that lifetime path; its build/device checks remain pending.

Expand, Close, full-screen presentation and map-driven orientation requests are
removed. Ordinary app orientation policy remains unchanged. Side/bottom safe-area
insets and pinned-title top avoidance remain; map pan/pinch touches are kept out of RecyclerView
interception, while title/row/metadata drags scroll the page. Attribution
appears once, as the SDK's clickable on-map copyright widget, with its default
12-DIP black text on an opaque white background in both app themes.
Initialization and tile-load errors are logged and shown locally, not treated
as a failed bean read. A tile failure remains labelled for the current map
session rather than being hidden by a different tile's successful request.
Reopening Beans creates a fresh Android map control. Round 9 restores its
camera, consumed initial fit, country selection and list anchor from the
Activity-owned snapshot instead of returning to the initial overview.

Round 9 uses the shared Core `BeanOriginMapData` and `BeanMapSession` in both
native heads. `GetSavedBeansForMapAsync` reuses the existing non-deleted repository
read, includes archived beans, and does not change the active-only default list.
The embedded `Data/origin-countries.json` is a compact derivative of the supplied
177-country public Natural Earth catalogue: canonical names, all name aliases
and original label coordinates are retained; unused ISO fields and duplicate
canonical aliases are omitted. `reference_sha256` identifies the supplied input,
not the transformed file's checksum. Provenance, public-domain terms and
approximate-position precision remain in the resource. `JsonDocument` parsing
does not introduce reflection-based serialization for NativeAOT.

Matching is case/diacritic/punctuation insensitive at whole-word boundaries,
with longer names claiming their spans before shorter names. Guji/Ethiopia and
Guji, Ethiopia resolve to Ethiopia; Guji alone and empty origins remain unmapped.
Every pin shows the country and distinct saved-bean count. The map accessibility
description labels positions as approximate country locations and reports distinct
mapped/unmapped totals without a visible summary. Blends can contribute to multiple pins, but combined totals
and selection rows deduplicate bean IDs. No farm coordinates are inferred.

The first successful origin read fits projected positions once, with 18% extent
padding and at least 750 km per side in projected units. A single country gets a
1,500 km country-scale box; empty/unmapped data gets a labelled world overview.
Mapsui defers navigation until viewport size is available. Refreshes do not fit
again, including adding the first resolved origin after an empty initial read.
An interaction while the initial read is pending also keeps the explored camera.
Android snapshots center/resolution/rotation before retiring a Beans screen and
restores them through the SDK navigator when the replacement map initializes.

A single country-pin tap filters the existing rows, shows its country/count in
the heading, and labels archived rows. Tapping the same selected country set
again clears it; another country replaces it. Overlapping pin/label hit records
produce a deduplicated combined list. All beans also clears the selection
without moving the camera and restores active-only list behavior.
Selecting a row opens existing Bean Detail; Back reloads saved data but restores
the selected list anchor and explored camera. Origin reads fail locally with
logged, explicit feedback and do not block the ordinary active list.

Shared Core handles Mapsui's recognized `GestureType.DoubleTap` with
`Navigator.ZoomIn(args.ScreenPosition, duration: 200)`. Only `SingleTap` hit-tests
and toggles countries: the SDK immediately emits the first tap of a double tap
as a single tap, so that first tap may select/clear a pin, but the second tap
never toggles it again. Normal SDK pan/pinch remains unchanged. Supplied 5.1.0
`TapGestureTracker` and shared `OnPointerPressed` expose no two-finger double
tap; two-finger zoom-out is explicitly omitted, with no custom recognizer.

Published 5.1.0 API checks for this slice: `Layers/MemoryLayer.cs` exposes
`Features`, `FeaturesWereModified` and inherited `DataHasChanged`; `PointFeature`
accepts the tuple returned by `SphericalMercator.FromLonLat`; `Map.Tapped` and
`BaseEventArgs.GetMapInfo` accept an explicit layer list (no removed
`IsMapInfoLayer` property). `Navigator.ZoomToBox`, `CenterOnAndZoomTo` and
`RotateTo` postpone calls until initialization. The empty origin-layer attribution
widget is disabled; the single required OpenStreetMap widget is unchanged.

The SDK default Hardware rendering path remains unchanged. Initialization,
origin-read and tile-load errors retain logged, explicit local feedback.

Published 5.1.0 source citations: `Mapsui.UI.Android/MapControl.cs` initializes
`_renderMode` to Hardware and creates `SKGLSurfaceView`; the app no longer sets
`RenderMode`. `Mapsui.Tiling/OpenStreetMap.cs` supplies the copyright text/URL,
and `Mapsui.Tiling/Layers/TileLayer.cs` assigns them to the layer's attribution.
`Mapsui/Map.cs` includes enabled layer attribution widgets.
`Widgets/ButtonWidgets/HyperlinkWidget.cs` handles taps using the browser action
registered by each native control in `Mapsui.UI.Shared/MapControl.cs`.
The duplicate native copyright buttons are removed, not the SDK attribution.
`TextBoxWidget`/`BoxWidget` expose the text size, background and opacity used here;
no custom renderer or browser framework is added.

For the final Hardware check, use an actual OS/device frame, not Ailoha's
unsupported view-render screenshot of a GPU-backed map. Inspect portrait
pixels, all screen edges and the single readable/clickable copyright
widget in both themes, alongside the interaction scenarios below.

Only the coordinator runs the following commands, from the export root, with
an installed matching .NET 11 Android workload and the already supplied
official Debug-agent artifacts. Angle-bracket values are placeholders for
coordinator-owned, isolated paths. Do not use historical fixture/performance
deployment instructions for this map run.

```sh
dotnet build src/BaristaNotes.Android/BaristaNotes.Android.csproj -t:SignAndroidPackage -c Debug -r android-arm64 -p:NativeMapSlice=true -p:EmbedAssembliesIntoApk=true -p:AilohaArtifactsDirectory=<existing-agent-artifacts> --artifacts-path <fresh-round20-android-maps-debug-artifacts>
dotnet publish src/BaristaNotes.Android/BaristaNotes.Android.csproj -c Release -r android-arm64 -p:NativeMapSlice=true -p:PublishAot=true -p:TrimmerSingleWarn=false --artifacts-path <android-maps-release-artifacts>
dotnet test src/BaristaNotes.Tests/BaristaNotes.Tests.csproj --filter "FullyQualifiedName~BeanPageGeometryTests" --artifacts-path <fresh-round20-native-maps-test-artifacts>
```

Round 20 needs the Debug build and focused geometry tests above; the existing Release
command remains a separate NativeAOT gate. On the owned maps emulator, compare
expanded/intermediate/pinned/reversed OS frames in light/dark mode: real tiles
must reach y=0, the initial map lower edge should remain within roughly one
former caption line of its prior position, and the compact header must have
only its normal padding/gap plus a visible separator. Verify pinned label
bounds start below the status/cutout inset and the first visible row can be
tapped. At full collapse, record the pinned separator bottom and first Brazil
row top in the same real frame: the row must begin at or below that edge, with
its complete caption/name visible and tappable. Verify no added safe-area gap
in the expanded header and continuous row placement through the final collapse
segment and reverse release. Repeat with empty, short, long and filtered content; retain the
explored camera through reverse scrolling and Detail/Back. Check Ethiopia
two-bean toggle back to five, another country, All beans, pan/pinch/double tap,
attribution, genuine loading/error feedback and unchanged bottom navigation.
Use the existing form-created samples and preserve all data; do not seed/reset.

The final Hardware check requires a newly signed, embedded-assembly APK, not a
build-only or Run-only target that may reuse an old package. Use a fresh
artifacts directory, record the exact signed APK path/hash/time, install it over
only `com.simplyprofound.baristanotes.maps.nativeandroid`, then cold-start.
Do not uninstall, clear data, or deploy over another task's or personal app.

The map property isolates both configurations as
`com.simplyprofound.baristanotes.maps.nativeandroid`; in Release it enables
NativeAOT and full trimming; Release acceptance remains a separate open gate.
Fixture/performance property combinations fail
explicitly. Restore must regenerate the configuration lock files for the new
Mapsui references; use `dotnet restore` with `--force-evaluate`, matching
configuration/RID/properties/artifacts path, if the coordinator uses a locked
restore. Release must not include the Debug inspection binding.

Required runtime scenarios, on the dedicated BaristaNotesMaps20261002 ARM64
emulator (never another task's emulator), apply
to light and dark app themes:

First cold-open and repeatedly reopen Beans in the newly signed Android build.
Confirm the activity stays on this native Beans page without a UI-thread
RecyclerView holder crash or a return to a previous activity. Exercise filter
rebinding and viewport changes as well as the scroll scenarios below; a
pre-crash metadata tree alone does not establish that the page remains usable.

1. Open Beans empty, short, five-bean and longer lists, including country-filtered
   lists. Verify map first/taller and approximately unchanged initial lower edge.
   At intermediate offsets verify map image moves at 80% of page speed within
   its clip while the title moves with the page. Scroll until the complete map
   and its metadata leave view; title/count must pin at the usable top. Continue
   scrolling and tap rows below it: no overlap or lost input.
2. Reverse scroll to release the title and recover the same explored map/camera.
   Repeat fully collapsed-to-visible transitions at least five times and confirm
   the process survives without GL errors or `SKGLSurfaceViewRenderer.OnDrawFrame`
   native crashes. Confirm the same hardware view stays attached during scrolling.
   Open Detail/Back at a deeper row, background/foreground,
   and check the fixed bottom navigation, status/cutout edges and normal app
   orientation layout. No Expand, Close or full-screen map may be present.
3. Pan/pinch the map without moving the page; drag title/rows/metadata to scroll
   the page. One-finger double tap must visibly zoom at its tap position. On a
   pin, its first SDK single tap may toggle selection but the recognized second
   tap must only zoom. Two-finger double-tap zoom-out is not supported/promised.
4. With uncached visible tiles and networking unavailable, verify a local map
   error while list/add/detail/navigation remain usable; reopen Beans after
   restoring networking. Do not bulk-download tiles or clear app data.
5. Repeat representative interaction/round-trip/error scenarios using the
   isolated NativeAOT Release APK; Debug evidence is not Release evidence.
6. Create the approved Map sample records through Add: Ethiopia, Colombia,
   Brazil, Guji/Ethiopia (also check Guji, Ethiopia) and empty Origin. Verify
   Ethiopia = 2, Colombia = 1, Brazil = 1, distinct mapped = 4, unmapped = 1.
   Check zero saved beans, only unmapped beans and one mapped country separately
   using owned UI records; no direct database fixtures or personal-data copies.
7. Archive a sample through its detail: its pin and selected row must remain,
   with ARCHIVED visible. Delete an owned sample through the UI: its pin count,
   selection rows and totals must remove it. For Brazil / Colombia blends,
   each country counts the bean once; a combined selection counts/lists it once.
   Check long aliases, country-boundary collisions and unmapped region-only data.
8. Verify initial padded fit, then pan/pinch, select a pin, scroll its rows,
   open Detail and Back. Camera and selected list anchor must survive refresh
   and repeated scroll-away/reverse; adding/editing origins must not repeatedly
   recenter an explored map. Ethiopia single tap shows two rows; after the SDK
   double-tap interval, tapping Ethiopia again restores all five active samples.
   Selecting Colombia/Brazil changes the filter. All beans resets only the
   filter, and approximate/unmapped labels remain
   visible. Inject an origin-read failure in a controlled build if needed;
   active-list loading and map-data loading must report independent errors.

No build, test, tile-network or device result was produced by this file-only
implementation pass. Regression coverage retains error-free events, persistent
failure feedback, exception logging, disposal and the single SDK attribution,
and adds catalogue/alias resolution, distinct blend counts, archived/deleted
semantics, padded fit, deferred initialization, camera restoration, repeated-pin
clearing/country replacement and recognized double-tap zoom at the tap position
without a second selection toggle. These tests have not been run by the
file-only worker.
Camera tests disable the tile layer's fetching; they do not establish tile
networking, GPU rendering or device gestures. Those require the coordinator's
owned-app/device-frame scenarios above.

The coordinator reports an Android NativeAOT package was published without a
Mapsui/Skia AOT warning, but existing EF Core/SQLite/Recognizers analysis warnings
remain. Their full diagnostics must be expanded before any Release installation;
package production alone is not Release acceptance. Record all restore/build/
publish warnings and bundled Mapsui/Skia native assets. Unresolved always-throw
or unsupported dynamic-code warnings remain blockers; no warnings are suppressed
here. Final Hardware, iOS and Release runtime confirmation remain pending.

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
