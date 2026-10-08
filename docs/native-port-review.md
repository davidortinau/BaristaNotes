# Native port: progress review and feature comparison

Review date: October 8, 2026.

**Status: work in progress, not full feature parity or release approval.**
The user approved importing both map implementations from their independent
comparison copies and committing progress before further UI changes.
Frozen source archives, usage receipts, and comparison reports remain private
and unchanged. This import does not give either group final comparison acceptance.

## Review result

The native heads have connected implementations for the main drink, history,
bean, bag, equipment, profile, settings, voice, advice, and photo journeys.
The review found three missing or changed experiences relative to the original
MAUI source. It also found specific map and comparison-tool defects.

The user chose to fix integration build conflicts and preserve the remaining
findings as a named backlog for this checkpoint. These findings are not
reported as fixed. There was no confirmed new data-loss or crash finding in the
bounded static review; that is not a guarantee that every state is safe.

The import uses one shared saved-bean query, `IBeanService.GetAllSavedBeansAsync`.
The two map implementations retain their existing namespaces and catalog
formats. Distinct resource names and explicit consumer type aliases keep the
combined source compatible without redesigning the prototypes.

## Reference and evidence limits

Original reference: MAUI source at commit
`54dd24ebc0a02f5c2d20409566539b5558100252`.
Native comparison: the integrated working tree for this progress checkpoint.
Map additions are compared with the later approved map scope, not treated as
features that were present in the original list-only Beans page.

**Source-present** below means a reachable implementation was inspected.
It does not mean all runtime, accessibility, visual, or failure states passed.
**Missing** means the reference has a reachable user outcome with no native
equivalent found in the bounded source search. **Changed** means the native
path has different behavior. An **inherited limit** is not a port omission.

Citation roots in the tables:

- M: `src/BaristaNotes`
- I: `src/BaristaNotes.iOS`
- A: `src/BaristaNotes.Android`
- C: `src/BaristaNotes.Core`

MAUI citations refer to the pinned original source unless marked as a map
addition. Native citations refer to this checkpoint.

## Missing or changed experiences

| ID | Priority | Experience | Source evidence and effect |
|---|---|---|---|
| PORT-01 | Medium | BAG long-press recipe shortcut, both native heads | M/Pages/ShotLoggingGridPage.cs:304-306,665-696 looks up the selected bag's recipe and opens bean detail, with no-bag/no-recipe/error feedback. I/DrinkViewController.cs:35,291-307 and A/MainActivity.Drink.cs:127-129 only open bag selection. Recipes remain available through Settings, Beans, and bean detail. |
| PORT-02 | Medium | Saved profile photos in iOS drink attribution and People picker | M/Pages/ShotLoggingGridPage.cs:743-775,1069-1088 uses AvatarPath. I/PeopleViewController.cs:144-179 and I/SliceControls.cs:252-280 use fixed symbols and names. iOS profile storage/list photos are present. Android attribution uses photos in A/MainActivity.Drink.cs:264-265 and A/Views/PeopleAdapter.cs:56-71. |
| PORT-03 | High for parity | Manual same-photo recovery after AI classification failure, both native heads | M/Pages/ShotLoggingGridPage.cs:2210-2229,2294-2302 offers manual intent and Retake, including Profile creation from the captured image. C/Services/Workflows/PhotoWorkflow.cs:73-84 alerts and returns before manual choice. Its test explicitly expects that policy. Missing configuration, timeout, or provider failure can trigger it. This is not established as an accepted product difference. |

PORT-03 is a confirmed behavior difference with a priority disagreement:
the Skeptic rates the lost recovery path High; the other roles rate it Medium
for a progress checkpoint. It remains outstanding parity work until the user
accepts fail-fast behavior or requests restoration of manual recovery.
The checkpoint request did not approve that behavior change.

## Feature comparison

| User experience | Original MAUI evidence | Native iOS evidence | Native Android evidence | Result |
|---|---|---|---|---|
| Create/edit a drink and save values | M/Pages/ShotLoggingGridPage.cs:233-258,299-346 | I/DrinkViewController.cs:30-80,378-390 | A/MainActivity.Drink.cs:43-102,299-309 | Source-present; shared DrinkWorkflow validates and saves. |
| Method, type, rating, bag, dose, yield, time, grind, temperature, people, and equipment selection | M/Pages/ShotLoggingGridPage.cs:302-332,1004-1088 | I/DrinkViewController.cs:187-376; I/PeopleViewController.cs:21-47 | A/MainActivity.Pickers.cs:95-247; A/MainActivity.Numeric.cs:13-42; A/MainActivity.People.cs:28-95 | Representative selection, clear, and empty/create paths are present. PORT-01/02 remain. |
| Activity history and filters | M/Pages/ActivityFeedPage.cs:40-164,248-321; M/Integrations/Popups/ShotFilterPopup.cs:332-342 | I/ActivityViewController.cs:89-166; I/FilterViewController.cs:39-95,125-154 | A/MainActivity.History.cs:109-174; A/MainActivity.Filter.cs:16-166 | Source-present: bean, Made For, rating, apply/clear/cancel, empty/error/retry. Activity's first-50 limit is inherited. |
| Saved-drink AI advice | M/Pages/ShotLoggingGridPage.cs:2455-2515 | I/DrinkAdvice.cs:23-45; I/AdviceViewController.cs:235-263 | A/MainActivity.Advice.cs:21-80; A/Views/NativeAdvicePopup.cs:124-164 | Source-present: saved ID, loading, configuration/timeout/error, result, close/re-request. Live provider behavior is not certified. |
| Bean create/edit/delete and initial bag | M/Pages/BeanDetailPage.cs:84-119,375-446 | I/BeanDetailViewController.cs:76-91,118-232 | A/MainActivity.BeanDetails.cs:76-168,293-337 | Source-present: fields, validation, cancel, confirmation, feedback. A separate initial-bag failure is surfaced. |
| Bag dates, notes, status, ratings, and delete | M/Pages/BagDetailPage.cs:51-189 | I/BagDetailViewController.cs:100-247 | A/MainActivity.Bags.cs:34-139,254-348 | Source-present, including complete/reactivate. Status changes save separately from form Save, as in the reference. |
| Bean ratings and paged rich history | M/Pages/BeanDetailPage.cs:149-211,992-1059 | I/BeanDetailViewController.cs:256-282,375-414; I/BeanHistoryViews.cs:10-33 | A/MainActivity.BeanSections.cs:131-251 | Source-present: 20-item pages, later-page loading, rich cards, edit navigation, empty/error/retry. |
| Recipe discovery, read, grind translation, and source links | M/Pages/BeanDetailPage.cs:215-355,736-889 | I/BeanDetailViewController.cs:284-373,417-468 | A/MainActivity.BeanRecipes.cs:18-118,124-255 | Source-present. PORT-01 is a shortcut gap, not missing recipes. The reference has no recipe Apply/editor action. |
| Equipment management and drink selection | M/Pages/EquipmentDetailPage.cs:94-153,410-422 | I/EquipmentFormViewController.cs:161-252; I/DrinkViewController.cs:187-226 | A/MainActivity.Equipment.cs:405-546; A/MainActivity.Drink.cs:133-140 | Source-present: create/edit/type/notes/validation/archive confirmation/select/clear. |
| Profiles, preference context, and delete | M/Pages/ProfileFormPage.cs:27-152 | I/ProfileCreateViewController.cs:203-296,377-405 | A/MainActivity.ProfileForm.cs:69-244,301-378 | Source-present. iOS read-error/retry prevents save before the form loads. |
| Profile photo change/remove and staged camera photo | M/Components/ProfileImagePicker.cs:64-165; M/Pages/ProfileFormPage.cs:94-112 | I/ProfileCreateViewController.cs:249-373; I/PhotoWorkflowSession.cs:126-132 | A/MainActivity.ProfileImages.cs:151-250; A/MainActivity.PhotoWorkflow.cs:199-201 | Source-present; cancel, image error, saved-image changes, and staged-save ordering exist. Real physical-device media paths remain outside this review. |
| Voice capture, transcript, command/query engine, and navigation | M/Pages/ShotLoggingGridPage.cs:1832-2160; C/Services/Voice/VoiceCommandService.cs | I/NativeVoiceCoordinator.cs:39-87,145-185; I/NativeVoicePlatformActions.cs:20-93 | A/MainActivity.Voice.cs:27-132; A/Services/NativeVoicePlatformActions.cs:22-62 | Source-present in inspected paths; permission, overlay, and navigation handling exist. Full voice/OS parity is not certified. |
| Camera/photo assistant and label extraction | M/Pages/ShotLoggingGridPage.cs:2210-2302 | I/PhotoWorkflowSession.cs; I/AddCoffeeViewController.cs:293-338 | A/MainActivity.PhotoWorkflow.cs:79-91; A/Views/NativeAddCoffeePopup.cs:498-566 | Source-present for successful routing and label-to-Type fallback; general classification failure has PORT-03. |
| Theme, temperature units, and numeric ranges | M/Pages/SettingsPage.cs; M/Pages/ValueRangeSettingsPage.cs; M/Pages/ValueRangeEditorPage.cs | I/SettingsViewController.cs:50-89; I/RangeSettingsViewController.cs:117-136; I/RangeEditorViewController.cs:94-117 | A/MainActivity.SettingsAppearance.cs:27-120; A/MainActivity.TemperatureUnits.cs:13-64; A/MainActivity.Ranges.cs:138-193,438-447 | Source-present: Light/Dark/Auto, Fahrenheit/Celsius, four ranges, reset, and dirty-exit handling. |
| Origin map, precise places, clusters, filtering, parallax, and short labels | Later approved map addition; original Beans page was list-only | I/BeanMapView.cs; I/BeanListViewController.cs | A/Views/BeanMapPanel.cs; A/Views/BeanPageView.cs; A/MainActivity.Beans.cs | Implemented and previously exercised in isolated builds; integrated verification is recorded separately. Map defects below remain. |
| User-editable geocoder endpoint | New M/Pages/SettingsPage.cs map setting, not the original reference | File-based origin-geocoding.json | File-based origin-geocoding.json | Added-feature difference: native has runtime file configuration, not the new MAUI Settings prompt. Not an original-port omission. |

No rendered direct drink-delete or bean-archive entry was found in the pinned
reference paths. Service methods alone are not proof of a missed UI feature.
Do not add those actions under the label of parity without separate scope.

## Other confirmed review findings

| ID | Priority | Finding | Disposition |
|---|---|---|---|
| INTEGRATION-01 | High | Two Core namespaces declare OriginLookup/OriginLocation/OriginPrecision. MAUI consumer imports and combined tests had ambiguous types. | Fixed with consumer aliases. Combined builds/tests are required; isolated results cannot prove integration. |
| MAP-01 | High | MAUI scans country names inside place names. Port of Spain, Trinidad and Tobago can map to Spain as well as the correct country. C/Services/Origins/BeanOrigins.cs:62-80,100-108,141-147. | Open. Protect explicit place/country pairs while preserving real blends. |
| MAP-02 | High | Guji, Oromia, Ethiopia can reject a valid Guji result because MAUI compares the complete remaining query only with the place name. C/Services/Origins/NominatimOriginGeocoder.cs:168-190. | Open. Preserve qualifier evidence through parse, cache, and snapshot validation. |
| MAP-03 | High | Native Android's restored pending selection can hide list Retry after both independent bean reads fail in a particular order. A/MainActivity.Beans.cs:107-119,164-167,182-188. | Open. Render all origin-read completion outcomes and retain completed active-read errors. |
| MAP-04 | Medium | MAUI honors minimum request spacing but ignores the server's Retry-After cooldown. C/Services/Origins/NominatimOriginGeocoder.cs:86-126. | Open. Keep cancellation and apply server cooldown in the existing gate. |
| TOOL-01 | High for next comparison | Native export omits the newly linked MAUI BeanPageScrollGeometry helper while retaining its tests. scripts/compare_work.py:98-145. | Open before the next measured comparison. Export the specific helper or remove its link/test from that export; do not broaden source access. |
| TOOL-02 | Medium | Incomplete snapshot totals and late receipt import can lose uncertainty or assign old cost to a later interval. scripts/compare_work.py:645-810. | Open. Mark incomplete values and report late-receipt reconciliation separately. No existing frozen report is asserted to be affected. |

## Review stages and confidence

Three independent roles reviewed the integrated source: Advocate, Skeptic,
and Architect. All three persisted source-backed reports. A second, bounded
counterargument stage ran because the first stage found High issues and
coverage was limited. It confirmed the concrete findings without a new broad
audit.

No further code probe ran: the remaining photo-failure question is a product
decision, not an unresolved source trace. The report keeps that question open.
Initial report metadata used nonstandard status/coverage strings; those fields
were normalized after file fingerprints were checked. Report bodies were
not changed.

Confidence is high for the named source paths; coverage is medium.
This is not an exhaustive line-by-line audit, a full visual comparison, or
verification of every OS, error, offline, accessibility, and cancellation state.
Release trimming/NativeAOT acceptance and the separately tracked intermittent
Android startup ANR remain open.

Further UI fixes, parity-gap implementation, and the map/tooling backlog
require their own work. The user explicitly approved this documented
work-in-progress checkpoint instead of expanding those fixes into this commit.

## Integrated checkpoint checks

After the import and type-alias corrections, the combined checkout passed
936 C# tests and 29 comparison-tool tests. Both MAUI mobile heads and both
native heads compiled in Debug. Native Android Debug and Release dependency
locks passed locked-mode restore. Release app execution was not checked.

Fresh packages were installed over the existing isolated map app IDs.
DevFlow and Ailoha inspected the real saved five-bean data. Real map taps
selected Ethiopia and repeat taps returned to the full list in the inspected
journeys. Native detail/cancel and iOS Beans/back/reopen paths were also
exercised. Native iOS map/header captures were scoped to the actual views:
its full-window capture returned black and was not used as visual proof.

The restored dependency graphs for both native heads contain no
Microsoft.Maui.Controls package. No personal app was replaced and no data
was reset. These checks establish the integrated progress baseline, not
closure of the review backlog or the full screen-state parity matrix.
