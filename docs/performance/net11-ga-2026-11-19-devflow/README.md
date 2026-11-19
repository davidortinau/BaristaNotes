# BaristaNotes .NET 11 GA runtime measurements with DevFlow

November 19, 2026.

These are measurements of two BaristaNotes implementations on the same
physical device within each platform. They do not isolate framework
overhead. The Release benchmark builds include a DevFlow agent and
measurement probes in both implementations. Their timing and size
overhead was not separately measured, so these numbers are not estimates
of uninstrumented production builds.

## Results

Each pair is MAUI / platform UI. Times are medians in milliseconds;
package sizes are MiB. Full ReadyToRun is the matched comparison.

| Platform and runtime | Startup | Package | New Drink to history | History to Settings |
|---|---:|---:|---:|---:|
| iOS full R2R | 533.7 / 334.3 | 73.77 / 61.31 | 556.4 / 98.3 | 141.7 / 31.4 |
| iOS NativeAOT | 371.1 / 201.5 | 56.81 / 43.51 | 350.1 / 65.4 | 141.7 / 51.9 |
| Android full R2R | 1,518.0 / 830.5 | 56.94 / 41.62 | 875.2 / 508.1 | 121.4 / 62.4 |
| Android configured partial R2R | 1,769.0 / 1,773.5 | 36.29 / 23.04 | 1,024.2 / 472.7 | 211.4 / 72.6 |
| Android NativeAOT | 773.0 / 225.0 | 41.38 / 27.66 | 483.3 / 266.6 | 78.6 / 66.4 |

The Android defaults compare MAUI partial R2R with platform full R2R:
1,769.0 ms versus 830.5 ms, and 36.29 MiB versus 41.62 MiB. The
explicitly configured partial-R2R startup medians were close, with
overlapping sample ranges and different profile coverage.

## Test identity

| Item | Value |
|---|---|
| Devices | Pixel 5, Android 14; iPhone 15 Pro, iOS 26.7.2 |
| Product source | `9dcf58e1b2307f6507788dc8e2edb01787826739`, plus the platform-specific benchmark patches in this directory |
| SDK / workload set | `11.0.100` / `11.0.100.1` |
| Runtime and MAUI Controls | `11.0.0` |
| Entity Framework Core and aligned Microsoft.Extensions packages | `11.0.0` |
| DevFlow agent | `0.1.0-preview.14.26567.2`; CLI reports `0.1.0-dev` |
| Fixture | The same accepted 1,000-drink database and preference metadata for both implementations |
| Database SHA-256 | `7e23a022af101dd46bd596e6a377f7597b103edb0d735896dfcc6c07e6ae8fbf` |
| Preference metadata SHA-256 | `2f4a4711d5ddea7251d26e78a8113d806f4664eaaf8a6890c17e1c301493f4f3` |
| Package formats | Signed arm64-only Android APK and signed iOS `ios-arm64` IPA |

The database and preference contents, signed packages, device identifiers,
signing details, and raw command logs are not published. Repeating the
exact fixture requires access to those inputs; the hashes permit identity
checks but do not reconstruct them.

## Collection

Each cell has one pilot, five conditioning launches, 30 measured
process-cold startups, and ten fresh-process samples for each navigation
route. The first six startup samples are excluded from the median. Processes
are restarted; operating-system and filesystem caches are not cleared.
The sample median is p50, and p90 is nearest rank.

Android startup is the OS `ActivityTaskManager` `Displayed` duration. It
was checked against `am start -W` `TotalTime` and is not time to fully
interactive. Each launch also checks app readiness and the fixture. The
iOS timer runs from process start until the New Drink screen is ready and
two display frames complete. The startup clocks differ by OS: compare
the two implementations on the same platform, not Android versus iOS.

Navigation starts at the route action and ends at the instrumented
destination frame. History readiness requires 1,000 total drinks and 50
items in the bound list. Each observation includes data loading, view
construction, navigation, and frame scheduling.

Both Android implementations use one Activity. MAUI uses Shell; platform
UI swaps view trees within `MainActivity`. The app's "Activity" screen
is drink history, not an Android Activity launch. Android system animation
scales were zero for all measurements.

Full composite ReadyToRun is the matched comparison. [Android MAUI Release
defaults to partial composite ReadyToRun](https://learn.microsoft.com/dotnet/android/building-apps/build-properties#mauienablefullreadytorun);
plain [.NET for Android defaults to full composite ReadyToRun](https://learn.microsoft.com/dotnet/android/building-apps/build-properties#publishreadytorun).
The native partial row is explicitly configured. The two partial builds do
not have identical profile
coverage: the MAUI build includes its framework MIBC profiles while the
platform build uses its workload-generated profile. Compiler invocations
were checked for `--composite` and the presence or absence of `--partial`.
Probe status reports dynamic code support disabled for each NativeAOT
cohort. Android package inspection found CoreCLR and JIT libraries in the
R2R packages and not in the NativeAOT packages.

The paired builds include the agent with file logging, network monitoring,
and high-level UI hooks disabled. Android uses the same AOT-safe direct
DevFlow extension and mutation lease in both apps. On the physical iPhone,
both builds include a benchmark-only loopback broker rejector to avoid a
five-second registration wait when the device cannot reach the host's
loopback broker. The collector uses USB forwarding and verifies the
specific bundle through agent status after broker discovery times out.
Neither the agent nor the broker workaround is intended for a shipping
Release build; their costs have not been isolated.

[Android NativeAOT is experimental](https://learn.microsoft.com/dotnet/android/messages/xa1040).
The NativeAOT builds retain trimming and AOT warnings, accepted for isolated
measurement only. A successful measured route does not establish
whole-app NativeAOT safety.

## Evidence and limits

`results/` contains the ten primary cohorts, with raw timing CSVs, accepted
probe status records, exact distributions, signed-package hashes, and
fixture hashes. The CSV row values are unchanged; line endings were
normalized to LF. Android summaries retain thermal status and battery
percentage before and after each cohort rather than full device dumps.
Those are endpoint checks, not continuous thermal monitoring. The iPhone's
thermal state was not collected. Android reported thermal status 0 and
100% battery at both endpoints for all six primary cohorts.

The Android MAUI NativeAOT startup cohort contains two unusually low
observations. Each had exactly one corresponding OS `Displayed` event;
both remain in the data. Their cause was not isolated.

The first Android full-R2R cohorts used action discovery. NativeAOT action
discovery failed with `NoMetadataTokenAvailable`, so both Android apps
were changed to the same direct-extension control path before the final
six cohorts. The earlier completed action-based cohorts are retained
under `excluded-action-based/` and are not pooled or selected by result.

Within each device, primary cells ran sequentially rather than in
randomized order; the two devices were sometimes measured concurrently.
On iOS the order was platform full, MAUI full, MAUI NativeAOT, platform
NativeAOT. On Android it was MAUI NativeAOT, platform NativeAOT, platform
full, MAUI full, platform partial, MAUI partial.
There is no reverse-order confirmation or new memory cohort for these
instrumented measurements. The older [Android GA](../../native-runtime-comparison-net11-ga.md)
and [iOS GA](../../native-runtime-comparison-ios-net11-ga.md) reports
describe a different source snapshot and collector. Their confirmation
and memory runs do not validate this new cohort. Differences from those
reports or the RC2 report are not SDK-only effects.

To regenerate the three blog charts with Matplotlib installed:

```bash
python3 docs/performance/net11-ga-2026-11-19-devflow/render_devflow_blog_charts.py \
  docs/performance/net11-ga-2026-11-19-devflow/results \
  /tmp/baristanotes-devflow-charts
```

The platform-specific source patches apply separately to the recorded
commit, not to one another. The collector scripts expect those patched
source trees, the fixture, matching prebuilt signed packages, and local
device and signing arguments. They use isolated benchmark application
identities and do not uninstall or clear applications. The collector
command logs may contain local identifiers and signing arguments and
should not be published without review.

For another collection, place both collector scripts in a scratch directory
alongside `source-devflow-ga/`, `source-devflow-ios-ga/`,
`fixture/barista_notes.db`, `fixture/preferences.txt`, and the `builds/`
directories named in the scripts. Apply the Android and iOS patches to
separate checkouts of the recorded commit. Publish the chosen signed
configuration first using the matching build properties in the collector;
the collectors expect the package to exist before launch. The iOS collector
also requires a USB forward to its specified agent port.

For example, after that setup:

```bash
python3 collect_devflow_android_public.py maui fullr2r \
  --serial "$ANDROID_SERIAL" --aapt "$AAPT" --output results/android-maui-full
python3 collect_devflow_ios_public.py maui fullr2r \
  --device "$IOS_DEVICE" --codesign-key "$CODESIGN_KEY" \
  --codesign-provision "$CODESIGN_PROVISION" --output results/ios-maui-full
```

Add `--smoke` for one pilot and one sample per route rather than a full
cohort. Exact reproduction still requires the unpublished fixture and valid
local device signing.
