# BaristaNotes Android runtime comparison on .NET 11 GA

November 18, 2026.

## Result

The Android GA rerun compares the retained MAUI application with the
.NET for Android application on the same physical Pixel 5. It uses the
original application source and accepted 1,000-drink fixture, not the
subsequent application on `main`.

With full composite ReadyToRun on both, the platform-UI app had 50.1% lower
median process-cold startup time and a 30.5% smaller signed APK.

The defaults tell a different package-size story. MAUI defaults to partial
ReadyToRun, while .NET for Android defaults to full composite ReadyToRun.
Comparing those defaults, platform UI had 48.4% lower median startup time,
but its APK was 10.0% larger.

These are results for the two BaristaNotes implementations. They do not
isolate framework overhead or establish that a framework is generally faster.

The original iPhone 15 Pro is also measured in the
[iOS GA report](native-runtime-comparison-ios-net11-ga.md). Its startup and
navigation cohorts are complete, but MAUI NativeAOT memory collection is
incomplete after a readiness timeout. The
[RC2 report](native-aot-runtime-comparison.md) retains the historical results.

## Test identity

| Item | Value |
|---|---|
| Device | Physical Pixel 5, `redfin` |
| Operating system | Android 14 |
| SDK | `11.0.100`, pinned without prerelease roll-forward |
| Workload set | `11.0.100.1` |
| Android workload | `37.2.31` |
| Runtime and compilation packs | `11.0.0` |
| MAUI controls | `11.0.0` |
| Entity Framework Core and aligned Microsoft.Extensions packages | `11.0.0` |
| Product baseline | `883448adea5148669f0e6009e9e16c7f54ec9e55` |
| Baseline plus committed performance probes | `6fc8c22f6ceedb277cf41ee44db14b0ab699ced6` |
| Fixture | The same accepted 1,000-drink database and preference metadata |
| Fixture SHA-256 | `7e23a022af101dd46bd596e6a377f7597b103edb0d735896dfcc6c07e6ae8fbf` |
| Package format | Signed, single-ABI `arm64-v8a` APK |
| Configuration | Release, not debuggable |
| Window, transition, and animator scales | All 0 |
| Thermal status | 0 before and after each primary runtime cell |

The original six Android benchmark APKs were recovered read-only from Pixel 5.
Each APK matched its SHA-256 in the RC2 report, and all six contained the same
fixture and metadata hashes. This also establishes that the original Android
package-size results were arm64-only APK bytes.

The rerun uses new isolated GA package identities. No personal application
was replaced, no application was uninstalled, and no existing app data was
cleared.

## Startup

Each cell has one pilot, five conditioning launches, and 30 measured
process-cold launches. The app process is stopped and its absence checked
before each launch. Each accepted launch validates a new process identity,
the sample and route arguments, app readiness, and fixture identity.

The Android clock is `am start -W` `TotalTime`. App readiness is checked
separately. This is not a time-to-fully-interactive measurement and does not
clear operating-system or filesystem caches.

| Runtime | MAUI p50 | Platform UI p50 | MAUI p90 | Platform UI p90 |
|---|---:|---:|---:|---:|
| Full composite R2R | 1,233.0 ms | 615.0 ms | 1,251 ms | 653 ms |
| Explicit partial R2R | 1,192.0 ms | 965.5 ms | 1,234 ms | 991 ms |
| NativeAOT | 725.5 ms | 203.5 ms | 771 ms | 223 ms |

The median is the usual sample median. The p90 uses nearest rank. Quantile
definitions must be retained when reproducing these values.

Full R2R was not faster than partial R2R in the MAUI startup samples here.
Do not assume that compiling more methods always improves this app's startup.

## Package size

These are exact signed APK file sizes, including benchmark instrumentation
and the fixture. They are not installed footprint or Google Play download
sizes. No bundletool or Play Console download-size result is claimed.

| Runtime | MAUI bytes | Platform UI bytes | MAUI MiB | Platform UI MiB |
|---|---:|---:|---:|---:|
| Full composite R2R | 47,859,935 | 33,280,267 | 45.64 | 31.74 |
| Explicit partial R2R | 30,243,039 | 17,072,395 | 28.84 | 16.28 |
| NativeAOT | 31,892,995 | 19,439,217 | 30.42 | 18.54 |

Archive inspection confirmed `arm64-v8a` as the only native-library ABI.
The four R2R packages contain CoreCLR/JIT runtime libraries. The two
NativeAOT packages do not.

## Screen navigation

Each route has ten fresh-process samples per cell. Timing begins at the
measured navigation action and ends at a content-qualified frame commit.
The history page must bind the expected 50 list items from the
1,000-drink fixture.

Both apps perform these routes within one Activity. MAUI uses Shell;
the platform implementation swaps view trees in a `FrameLayout`. It
does not launch a new Activity for each screen or use Fragments.
The app's screen named "Activity" means drink history.

| Runtime | Route | MAUI p50 | Platform UI p50 |
|---|---|---:|---:|
| Full composite R2R | New Drink to history | 826.802 ms | 380.963 ms |
| Full composite R2R | History to Settings | 102.661 ms | 62.107 ms |
| Explicit partial R2R | New Drink to history | 1,037.358 ms | 589.009 ms |
| Explicit partial R2R | History to Settings | 105.256 ms | 77.959 ms |
| NativeAOT | New Drink to history | 361.463 ms | 227.137 ms |
| NativeAOT | History to Settings | 42.714 ms | 46.683 ms |

Platform UI has the lower median in five of these six comparisons. MAUI
has the lower median for the short NativeAOT Settings route. The
distributions for that short route overlap; this is not a claim of
statistically established superiority.

The measurements include application data loading, view construction,
navigation, and frame scheduling. They do not isolate navigation-framework
overhead.

## Memory

For each cell and state, ten independent processes were measured. Each
process settled for ten seconds before three consecutive `dumpsys meminfo -s`
snapshots. The per-process statistic is their median; the table gives the
median of those ten process medians.

Repeated navigation starts on history and performs five
Settings to New Drink to history cycles before settling.

| Runtime | State | MAUI PSS | Platform UI PSS |
|---|---|---:|---:|
| Full composite R2R | Initial New Drink | 355.19 MiB | 279.39 MiB |
| Full composite R2R | First history | 366.90 MiB | 281.52 MiB |
| Full composite R2R | Repeated navigation | 400.66 MiB | 304.51 MiB |
| Explicit partial R2R | Initial New Drink | 200.78 MiB | 141.69 MiB |
| Explicit partial R2R | First history | 212.28 MiB | 141.21 MiB |
| Explicit partial R2R | Repeated navigation | 231.41 MiB | 159.04 MiB |
| NativeAOT | Initial New Drink | 143.15 MiB | 107.87 MiB |
| NativeAOT | First history | 148.83 MiB | 113.58 MiB |
| NativeAOT | Repeated navigation | 171.06 MiB | 124.25 MiB |

The raw `TOTAL PSS` output was checked against the parsed samples. These
Android values must not be compared directly with iOS physical footprint.

## Runtime configuration verification

The actual compiler invocations were inspected in the per-cell binlogs.

- Full R2R: Crossgen2 `11.0.0`, Android arm64, `--composite`, no `--partial`.
- MAUI partial R2R: `--partial`, MAUI's shipped framework MIBC profiles,
  and the workload-generated main-assembly profile.
- Native partial R2R: explicit `--partial`, with the workload-generated
  main-assembly profile. It does not import the MAUI framework profiles.
- NativeAOT: the resolved NativeAOT runtime and ILCompiler packs are
  `11.0.0`; archive inspection confirms absence of CoreCLR/JIT libraries.

The two partial rows therefore do not mean identical profile coverage.
The native partial row is an explicitly configured diagnostic comparison,
not the default for plain .NET for Android. The headline controlled
comparison uses full R2R on both.

## Source changes and warnings

The product source was exported into a session-only build tree. The
main checkout was not switched to an old commit.

Build-only changes pin the GA toolchain, align the listed Microsoft packages
to GA, isolate intermediate/output directories, and use distinct benchmark
package IDs. A read-only native-probe status field exposes existing
history-readiness criteria so the collector does not confuse entering a
page with finishing its data load. No product navigation or timing boundary
was rewritten.

The original external collector scripts were no longer available. The
collector was reconstructed from the committed probe contracts. Failed
smoke attempts and the `full01` argument-validation failure were retained
separately and excluded from the primary measurements. The accepted primary
run is `android-ga-full02`.

NativeAOT builds retain trimming and AOT diagnostics in EF Core and other
dependencies, including a `DependencyContext` single-file warning. Neither
Android NativeAOT build emitted a "will always throw" diagnostic. David
explicitly accepted isolated benchmark measurement with the warnings.
This is not acceptance for a personal or production release.

MAUI's dependency graph also reports a MessagePack `NU1902` advisory.
No general dependency-security approval is implied.

## Evidence and limits

[Machine-readable summary](performance/net11-ga-2026-11-18/summary.json)
contains exact values, APK hashes, toolchain identities, fixture hash, and
CSV hashes. Each cell has `startup.csv`, `transitions.csv`, and `memory.csv`
under the [evidence directory](performance/net11-ga-2026-11-18).

The [blog chart renderer](performance/net11-ga-2026-11-18/render_blog_charts.py)
and its sanitized primary inputs are retained with the samples. With
Matplotlib installed, run:

```bash
python3 docs/performance/net11-ga-2026-11-18/render_blog_charts.py \
  docs/performance/net11-ga-2026-11-18 \
  /tmp/baristanotes-ga-charts
```

The renderer now reads both platforms' GA primary inputs. Its iOS panels
are no longer the historical RC2 results. The iOS report records the
incomplete memory cell; the charts show startup, navigation, and package size.

The primary run contains 86 distinct processes per cell: 36 startup runs,
20 transition runs, and 30 memory runs. The order was MAUI full, platform
full, MAUI partial, platform partial, MAUI NativeAOT, platform NativeAOT.
This sequential order can introduce bias.

Startup and navigation were repeated in a separate reverse-order confirmation.
Each cell has another 30 measured startups and ten samples per route.
All directional comparisons reproduced, although the medians varied.
The confirmation observations are kept separate from the primary run;
they are not pooled with it or substituted for less favorable results.

| Runtime | MAUI startup p50, confirmation | Platform UI startup p50, confirmation |
|---|---:|---:|
| Full composite R2R | 1,210.5 ms | 593.5 ms |
| Explicit partial R2R | 1,163.5 ms | 943.5 ms |
| NativeAOT | 688.0 ms | 182.5 ms |

The confirmation has 56 processes per cell and thermal status 0 before
and after every cell. Its raw files are named `confirmation-startup.csv`
and `confirmation-transitions.csv`. The summary JSON retains both sets of
statistics and their CSV hashes.

Do not interpret differences from the old RC2 run as an isolated SDK effect.
The runs occurred on different dates, GA dependencies were aligned, and
the collector was reconstructed. This comparison concerns the two
implementations within the documented GA run.
