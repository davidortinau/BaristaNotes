# BaristaNotes native architecture refactor

**Date:** September 28, 2026
**Status:** Feature implementation is substantially complete. Final platform acceptance is still open.

## The short version

BaristaNotes changed from one shared [MAUI](file:///Users/davidortinau/work/davidos/context/glossary.md#maui) and [MauiReactor](file:///Users/davidortinau/work/davidos/context/glossary.md#mauireactor) application to three product projects:

1. `BaristaNotes.Core`, which contains shared data, services, validation, workflows, artificial intelligence, voice dispatch, photo rules, theme preferences, and persistence.
2. `BaristaNotes.iOS`, which uses Apple's UIKit controls and layouts.
3. `BaristaNotes.Android`, which uses Android Views and `RecyclerView`.

This reduced the cross-platform source-share ratio from **99.2% to 40.4%**. The new Android app gets **56.4%** of its hand-authored C# from the shared Core project. The iOS app gets **58.7%** from Core.

The refactor created **164 native-head C# files** and **23,456 nonblank lines of native platform code**. Core grew by **5,717 lines**, or **56.3%**, as product behavior moved out of the MAUI user interface. The target product code is now **26.1% larger** than the original MAUI mobile code base.

That added code bought direct platform ownership. In the controlled Pixel 5 study, native Android had 43.4% to 43.7% lower median startup time than MAUI, 81.2% to 89.0% lower median page-transition time, 25.8% to 31.4% lower median proportional set size memory, and a 41.6% smaller signed application package. See the [Pixel 5 performance report](pixel5-native-performance/pixel5-native-comparison.md).

## What changed

| Area | MAUI architecture | Native architecture |
|---|---|---|
| Application heads | One multi-target MAUI project | Separate .NET for iOS and .NET for Android projects |
| User interface | Shared MauiReactor component tree | UIKit on iOS, Android Views and `RecyclerView` on Android |
| Shared code | User interface, navigation, services, data, and most platform behavior | Application logic, workflows, validation, services, data, and persistence |
| Platform code | Small MAUI platform bootstrap and overrides | Complete screen, layout, navigation, lifecycle, accessibility, permission, media, and input implementations |
| Navigation and modal behavior | MAUI navigation and popup abstractions | Native controllers, activities, views, overlays, and platform lifecycle rules |
| Platform services | MAUI abstractions and Essentials | Shared contracts with native iOS and Android implementations |
| Inspection | DevFlow in the retained MAUI app | Debug-only Ailoha native bindings; no Ailoha in Release |
| Release dependencies | `Microsoft.Maui.Controls`, MauiReactor, MAUI toolkits and popup libraries | No `Microsoft.Maui.Controls` or MauiReactor in either native head |
| Application identity | One MAUI application identity | Separate native application identities and separate data sandboxes |
| Android publishing | MAUI application package | Signed arm64 [NativeAOT](file:///Users/davidortinau/work/davidos/context/glossary.md#nativeaot) package |

The retained MAUI app is still the source of truth for features, behavior, and design. It remains in the repository for comparison and regression checks. It is not part of the new native product-code calculation.

### Shared behavior moved into Core

The Core project now owns behavior that was previously attached to MAUI pages, components, or services. Highlights include:

- Drink create, edit, validation, and persistence workflows.
- Bean, Bag, Equipment, Profile, Grind, and Add Coffee workflows.
- Explicit optional-value semantics through `FieldUpdate<T>`.
- Shared photo-intent rules and vision result parsing.
- Shared Photo capture, classification, retake, Coffee, Profile, and Room workflow decisions.
- Shared 35-tool voice-command dispatch and platform action contracts.
- Shared Voice recording, silence timeout, exact-once dispatch, photo pause, and retirement lifecycle.
- Shared advice request, timeout, cancellation, and presentation text.
- Shared Bag detail loading, image file ownership, Coffee completion, and Voice route parsing.
- Shared navigation registry, theme preference, advice service, and database initialization.
- Shared presentation and picker state used by both native heads.

This change created 44 new Core files. The Core project increased from **10,152** to **15,869** nonblank C# lines.

### Each platform now owns its complete user interface

The Android head contains **99 C# files and 12,273 nonblank lines**. The iOS head contains **65 C# files and 11,183 nonblank lines**.

This code includes much more than screen layout. Each head owns:

- Native navigation and back behavior.
- Modal size, dismissal, focus, and keyboard behavior.
- List virtualization and item presentation.
- Safe-area and edge layout.
- Themes, fonts, accessibility, and dynamic state.
- Permissions, speech, camera, photo selection, and platform lifecycle.
- Native feedback, loading, error, and cancellation states.

## How code sharing changed

### Measurement method

The comparison uses the pinned MAUI source at commit `85beca0bb79e56720ff5dac31230958b5b0efa60` and the current native working tree.

It counts nonblank, hand-authored C# lines. It excludes:

- Tests.
- Generated source, including Entity Framework compiled models and `*.g.cs`.
- `bin` and `obj` output.
- Debug-only Ailoha binding projects.
- Documentation, project files, resources, images, fonts, and configuration.
- The retained MAUI project from the new native total.

The portfolio share ratio counts shared code once and each platform head once:

`shared / (shared + Android-specific + iOS-specific)`

### Results

| Architecture | Shared C# | Android-specific | iOS-specific | Total | Shared across both platforms |
|---|---:|---:|---:|---:|---:|
| MAUI baseline | 30,924 | 61 | 192 | 31,177 | **99.2%** |
| Native target | 15,869 | 12,273 | 11,183 | 39,325 | **40.4%** |

The shared ratio decreased by **58.8 percentage points**. Platform-specific code now represents **59.6%** of the native source portfolio.

There is another useful view. For each shipped application, Core is still more than half of the hand-authored C#:

| Native application | Shared Core | Platform head | Shared portion of that application |
|---|---:|---:|---:|
| Android | 15,869 | 12,273 | **56.4%** |
| iOS | 15,869 | 11,183 | **58.7%** |

The difference between 40.4% and 56.4% to 58.7% is important. The first number measures source reuse across the full two-platform portfolio. The second measures how much of each individual app comes from Core.

## What the refactor cost

### Source-code scale

| Measure | Result |
|---|---:|
| New native-head C# files | 164 |
| New native-head nonblank C# lines | 23,456 |
| New Core files | 44 |
| Core growth | 5,717 lines, 56.3% |
| New test files | 25 |
| New test-file lines | 4,301 |
| Product-code growth | 8,148 lines, 26.1% |

The main cost is not the first rewrite. It is the new ownership model:

- Shared domain, data, workflow, and service changes are implemented once.
- Screen layout, interaction, navigation, lifecycle, accessibility, and platform integration changes usually need one iOS implementation and one Android implementation.
- Shared unit tests run once, but important user journeys need runtime checks on both platforms.

For user-interface-heavy features, there are now two implementation and verification surfaces. This does not mean that every feature costs exactly twice as much. It means the old assumption of one shared user-interface change no longer applies.

### Product-contract scale

The rebuild was managed as a source-led compatibility project, not as a screen rewrite. The approved scope contains:

- **116 required application states.**
- **30 behavior and platform contracts.**
- **15 implementation workstreams.**
- Separate reference and native application identities.
- Runtime checks for persistence, restart, loading, error, cancellation, theme, permission, modal, accessibility, and platform lifecycle behavior.

The current plan has **74 completed tasks, 8 pending tasks, and 17 blocked tasks**. No task is marked in progress. The blocked items are mainly final runtime and acceptance evidence, not missing basic feature implementation.

### Verification scale

The latest complete repository test run had **699 passing tests** with no failures or skips.

The work also included:

- Debug and Release builds for both native heads.
- Independent source-first review.
- Device and simulator runtime checks.
- Data-preservation checks before and after native workflows.
- Three signed arm64 NativeAOT Android packages.
- A Pixel 5 study with 60 startup samples, 20 independent memory processes, 180 memory snapshots, and 20 page-transition samples.

The refactor exposed defects that ordinary visual conversion would not have found. Examples include:

- A SQLite `RETURNING` and reset interaction that could report success but lose a write.
- Optional edit fields that needed to distinguish “not supplied” from “clear this value.”
- Android measurement and parent-reuse defects.
- Android Bag completion-state presentation drift.
- iOS scene ownership, permission-state, and readiness continuation defects.
- iOS Photo exit-failure feedback and accessibility restoration defects.

These fixes are part of the refactor cost. They also improved the shared product behavior.

### Time and compute scale

The current session covers about **72.3 elapsed hours**, from September 25 to September 28. Work continued in parallel while the user was away.

The local session ledger records:

- **13 coordinator or subagent identities.**
- **58.0 aggregate model-execution hours.**
- A coordinator, separate platform implementers, and independent review roles.

Aggregate model time is not human labor and is not a billing estimate. Parallel work means it can exceed or overlap wall-clock time. It is included only to show the scale of the automated engineering effort.

No credible dollar estimate is available from this record. A dollar estimate would require an agreed engineering rate, an interpretation of model billing, and a decision about whether to include investigation, verification, and acceptance work.

## What the investment produced

The native Android measurements show the intended performance direction:

| Result | Native Android compared with MAUI |
|---|---:|
| Median startup | 43.4% to 43.7% lower |
| Median page-transition time | 81.2% to 89.0% lower |
| Median proportional set size memory | 25.8% to 31.4% lower |
| Signed application package | 41.6% smaller |

These are controlled observations for the tested BaristaNotes builds on one Pixel 5. They are not general claims about MAUI, UIKit, or Android Views.

The larger architectural result is clear:

- The app now has direct native control and lifecycle ownership.
- Shared application behavior has a cleaner boundary in Core.
- The cost moved from framework abstraction to duplicated native user-interface implementation and platform verification.
- Performance improved substantially in the measured Android scenarios.

## What remains

This is a cost-to-date report, not final acceptance. The remaining work includes:

- iOS coordinate-input replay and final interaction evidence.
- Android Photo completion-failure diagnostics that need a narrow test seam.
- Real media, speech, permission, and provider matrices.
- A refreshed current-tree evidence set.
- Broad Release acceptance on both platforms.
- Final source-first discrepancy and regression reviews.

The native feature set is substantially implemented. The remaining cost is concentrated in confidence, coverage, and final acceptance rather than broad feature construction.

## Sources

- Pinned MAUI source: commit `85beca0bb79e56720ff5dac31230958b5b0efa60`.
- Current repository working tree in `/Users/davidortinau/work/BaristaNotes`.
- Session checkpoints 1 through 8, September 25 to September 28, 2026.
- `src/BaristaNotes.Core/BaristaNotes.Core.csproj`.
- `src/BaristaNotes.Android/BaristaNotes.Android.csproj`.
- `src/BaristaNotes.iOS/BaristaNotes.iOS.csproj`.
- [Pixel 5 native performance comparison](pixel5-native-performance/pixel5-native-comparison.md).
- Session rebuild record: `rebuild.json`.
- Session implementation handoff: `implementation-handoff.json`.
