# BaristaNotes iOS runtime comparison on .NET 11 GA

November 18, 2026.

## Result

On the original physical iPhone 15 Pro, platform UI had a 35.5% lower
process-cold startup median and a 16.6% smaller signed IPA with full
composite ReadyToRun on both implementations. Platform UI also had the
lower median on both measured navigation routes in both runtime modes.
A separate reverse-order timing run reproduced those directions.

These are measurements of two BaristaNotes implementations, not isolated
framework overhead. The source is the original application baseline plus
the recorded GA compatibility changes, not current `main`.

**MAUI NativeAOT memory collection is incomplete.** A transport timeout
interrupted its first memory pass. A retry later failed to reach the
probe's readiness condition within 65 seconds. Its startup and navigation
cohorts had already completed, but no MAUI NativeAOT memory medians are
published. The cause of the readiness timeout has not been isolated.

See the [Android GA report](native-runtime-comparison-net11-ga.md) for
Pixel 5 and the [original RC2 report](native-aot-runtime-comparison.md) for
historical results. Do not interpret differences from RC2 as an SDK-only
effect: the iPhone OS, dependencies, collector, and MAUI scene wiring changed.

## Test identity

| Item | Value |
|---|---|
| Device | Original physical iPhone 15 Pro, `iPhone16,1`, wired |
| Operating system | iOS 26.7.2, build 23H128; the RC2 run used 26.7.1 |
| SDK and workload set | `11.0.100` and `11.0.100.1` |
| iOS workload | `27.2.11039` |
| Xcode | 27.2, build 27B52 |
| Runtime and compiler packs | `11.0.0` |
| MAUI Controls | `11.0.0` |
| Aligned Entity Framework Core and Microsoft.Extensions packages | `11.0.0` |
| Product baseline | `883448adea5148669f0e6009e9e16c7f54ec9e55` |
| Baseline plus committed probes | `6fc8c22f6ceedb277cf41ee44db14b0ab699ced6` |
| Fixture | The same accepted 1,000-drink database and preference metadata |
| Database SHA-256 | `7e23a022af101dd46bd596e6a377f7597b103edb0d735896dfcc6c07e6ae8fbf` |
| Package | Signed Release IPA, `ios-arm64`, including fixture and probes |
| Battery and thermal state | Not collected through the original probe |

Third-party packages stayed at the original versions. This is not a claim
that every dependency is GA. New isolated benchmark bundle identities
protect the personal application. No app was uninstalled or had its data
cleared.

## Startup and package size

Each timing cohort has one pilot, five conditioning launches, and 30
measured process-cold launches. Each accepted observation validates a new
process identity, bundle, sample, mode, fixture, and completed probe.

The iOS startup clock runs from the kernel process-start timestamp to
New Drink readiness plus two `CADisplayLink` frames. Operating-system caches
were not cleared. This clock is not Android's `am start -W` `TotalTime`.

| Runtime | MAUI p50 | Platform UI p50 | MAUI p90 | Platform UI p90 |
|---|---:|---:|---:|---:|
| Full composite ReadyToRun | 568.591 ms | 366.744 ms | 585.886 ms | 425.473 ms |
| NativeAOT | 349.150 ms | 200.610 ms | 395.094 ms | 210.678 ms |

The p50 is the sample median. The p90 uses nearest rank. Exact unrounded
statistics, ranges, and observations are retained in the evidence.

| Runtime | MAUI bytes | Platform UI bytes | MAUI MiB | Platform UI MiB |
|---|---:|---:|---:|---:|
| Full composite ReadyToRun | 61,732,540 | 51,473,502 | 58.87 | 49.09 |
| NativeAOT | 44,137,678 | 33,688,931 | 42.09 | 32.13 |

IPA bytes are not installed footprint or an App Store download estimate.
The apps have different dependency graphs, including MauiReactor and
additional UI libraries in the MAUI implementation.

## Screen navigation

Each route has ten fresh-process samples. Timing begins at the navigation
action handler and ends at destination readiness plus two display frames.
History readiness requires the expected 1,000 total drinks and 50
items in the bound history list. The measurements include application data loading,
view construction, navigation, and frame scheduling.

| Runtime | Route | MAUI p50 | Platform UI p50 |
|---|---|---:|---:|
| Full composite ReadyToRun | New Drink to history | 660.472 ms | 114.965 ms |
| Full composite ReadyToRun | History to Settings | 283.348 ms | 43.907 ms |
| NativeAOT | New Drink to history | 412.515 ms | 65.307 ms |
| NativeAOT | History to Settings | 150.022 ms | 54.642 ms |

## Memory and the incomplete cell

For each completed state, ten fresh processes settled for ten seconds
before three snapshots one second apart. The reported statistic is the
median of the ten per-process medians. Repeated navigation performs five
Settings to New Drink to history cycles before settling.

The metric is `proc_pid_rusage` physical footprint, not resident bytes,
managed heap size, or Android PSS. Raw resident, footprint, and managed
snapshots are retained separately in the CSV.

| Runtime | State | MAUI footprint | Platform UI footprint |
|---|---|---:|---:|
| Full composite ReadyToRun | Initial New Drink | 81.40 MiB | 60.70 MiB |
| Full composite ReadyToRun | First history | 98.38 MiB | 60.97 MiB |
| Full composite ReadyToRun | Repeated navigation | 127.40 MiB | 64.21 MiB |
| NativeAOT | Initial New Drink | Incomplete | 51.66 MiB |
| NativeAOT | First history | Incomplete | 52.25 MiB |
| NativeAOT | Repeated navigation | Incomplete | 50.74 MiB |

The first MAUI NativeAOT pass completed all timing observations, ten initial
memory processes, and eight first-history memory processes. Its next
status-file copy timed out after 25 seconds. The device connection was
re-established during that interval. A later retry of the same endpoint
found that the app probe had completed.

A fresh full-cell retry again completed all timing observations, ten
initial memory processes, and five first-history memory processes. The
next process stayed at `starting` for 65 seconds. All 167 status-file reads
succeeded, but none reported readiness or completion. A subsequent device
query reported no passcode requirement. That distinguishes this observation
from the earlier transport timeout; it does not establish an application,
framework, runtime, or collector root cause.

Rather than keep rerunning until a wholly successful cell appeared, the
charts retain each configuration's first complete timing cohort. This is
an explicit change from the original whole-cell acceptance plan after
the interrupted collection. No timing values were used to choose cohorts.
The interrupted retry's timing and partial memory CSVs are retained too.
No MAUI NativeAOT memory medians are published, even for its completed
initial state. The readiness failure is not silently discarded.

## Configuration and compatibility

The actual full-ReadyToRun compiler invocations use Crossgen2 `11.0.0`,
`--targetos:ios`, `--targetarch:arm64`, `--obj-format:macho`, and
`--composite`, without `--partial`. No supported iOS partial/profile
configuration was established, so no partial row was invented.

NativeAOT uses the GA `11.0.0` runtime and compiler packs with
`PublishAotUsingRuntimePack=true`. Probe observations report
`IsDynamicCodeSupported=false`. Package signing and exact fixture hashes
were checked for all four builds.

The frozen MAUI app did not declare UIKit scenes. The GA iOS toolchain
rejected that manifest with MT7199. The isolated benchmark snapshot adds
the required scene manifest and a registered `SceneDelegate` derived from
`MauiUISceneDelegate`, following the
[MAUI scene-lifecycle guidance](https://learn.microsoft.com/dotnet/maui/ios/uikit-scene-lifecycle).
The native app already declared scenes. Obsolete Mono interpreter settings
were cleared for CoreCLR. No product navigation or timing boundary was
rewritten.

The old RC2 MAUI NativeAOT "will always throw" diagnostic did not appear in
any of the four GA iOS publish logs. Other trimming and AOT warnings remain,
including EF Core expressions and Apple Intelligence JSON paths.
MAUI's dependency graph also reports MessagePack `NU1902`. David accepted
the recorded warnings for isolated measurement only, not a production
release. Completed routes do not establish whole-app NativeAOT safety.

## Reverse-order confirmation and evidence

Startup and both routes were repeated separately in reverse order:
platform NativeAOT, MAUI NativeAOT, platform full ReadyToRun, then MAUI full
ReadyToRun. Each confirmation cell has 56 processes: one pilot, five
conditioning, 30 measured startups, and ten samples per route. All four
confirmation cells completed.

| Runtime | MAUI startup p50 | Platform UI startup p50 | MAUI history / Settings p50 | Platform UI history / Settings p50 |
|---|---:|---:|---:|---:|
| Full composite ReadyToRun | 530.811 ms | 330.370 ms | 575.054 / 166.598 ms | 114.956 / 47.856 ms |
| NativeAOT | 504.557 ms | 204.869 ms | 360.496 / 149.996 ms | 65.163 / 47.861 ms |

All directional timing comparisons repeated, but several medians moved
substantially. In particular, MAUI NativeAOT startup changed from about
349 to 505 ms. Sequential order, uncontrolled thermal conditions, and
small navigation cohorts limit precision. The confirmation is not pooled
with the primary data or substituted for less favorable observations.

The [sanitized iOS summary](performance/net11-ga-2026-11-18/ios-summary.json)
contains exact statistics, package hashes, build properties, CSV hashes,
the selection-policy change, and both interruptions.
The [evidence directory](performance/net11-ga-2026-11-18) retains primary,
confirmation, interrupted-retry, and readiness-timeout CSVs. Partial memory
files are labeled explicitly. The three complete primary cells have 86
processes each; the MAUI NativeAOT timing cohort has 56.

The [chart renderer](performance/net11-ga-2026-11-18/render_blog_charts.py)
reads the retained Android and iOS GA primary summaries. With Matplotlib
installed, run:

```bash
python3 docs/performance/net11-ga-2026-11-18/render_blog_charts.py \
  docs/performance/net11-ga-2026-11-18 \
  /tmp/baristanotes-ga-charts
```

Private device identifiers, signing material, app archives, fixture contents,
and raw device-command logs are not included in the published evidence.
