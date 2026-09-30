# Pixel 5 native Android performance comparison

Collection started `2026-09-28T03:39:18.338712+00:00` and finished `2026-09-28T04:01:13.546552+00:00`.

This report compares complete BaristaNotes application stacks on one physical Pixel 5. It reports controlled observations for these builds and datasets. It does not claim general framework performance.

## Result

- Native Android had 43.4-43.7% lower median startup TTID than MAUI and 74.1-74.3% lower median startup TTID than Comet.
- Native Android had 81.2-89.0% lower median page-transition time than MAUI and 86.3-88.4% lower median page-transition time than Comet.
- Native Android used 25.8-31.4% less median PSS than MAUI and 6.0-12.6% less median PSS than Comet across the six measured states.
- The clean Native Android APK was 41.6% smaller than MAUI and 17.8% smaller than Comet.
- Native private-dirty memory was not lower than Comet in all states. The PSS and RSS results are more consistently favorable.

## Test identity

| Item | Value |
|---|---|
| Device | Pixel 5 `redfin`, Android 14 / API 34, serial `13041FDD4007MT` |
| Display | 1080 x 2340, density 440 |
| Build | Release, Android arm64, .NET 11 Native AOT |
| SDK | `11.0.100-rc.1.26425.128` |
| Android workload | `37.0.0-rc.1.2257` |
| Native AOT runtime | `11.0.0-rc.1.26428.117` |
| 100-drink semantic SHA-256 | `6d378da25645b1ab4231e8889ba829e0416b391886a3aafe68ff9f26d6708cd8` |
| 1,000-drink semantic SHA-256 | `eb97125e964e9504ce498e1f1bd85d297a1234d56eb2db00e7b5c14da68f092f` |
| 100-drink installed APK SHA-256 | `3dc966c3d589f79aa61a68d196c5fe145e897a45c2c2b5e8b0260afd99af1fbc` |
| 1,000-drink installed APK SHA-256 | `458f8446b27e9bd4c4e930ea370e5c86f127be73b432ecea3498147b1fe3818c` |
| Clean native APK SHA-256 | `62e11ad47c753ae25155e073c03d06df538c9957d4b38fd38a8700515b5bbcef` |
| Native ART status | `verify/install`, same mode as the saved MAUI result |
| Battery and thermal | USB powered; 99% to 100%; battery 22.5 C to 22.9 C; thermal status 0 |
| Wake state after collection | Screen on, awake, `mStayOn=true`, keyguard not showing |
| System animation scales | Window 0, transition 0, animator 0; unchanged |

## Process-cold startup TTID

Lower is better. Each native result has 30 measured launches after one pilot and five conditioning launches. p90 uses nearest rank.

| Drinks | App | n | p50 | p90 | Native p50 difference |
|---:|---|---:|---:|---:|---:|
| 100 | MAUI | 30 | 317.0 ms | 693.5 ms | - |
| 100 | Comet | 30 | 693.5 ms | 709.3 ms | - |
| 100 | Native Android | 30 | 179.5 ms | 197.0 ms | -43.4% vs MAUI; -74.1% vs Comet |
| 1,000 | MAUI | 30 | 321.5 ms | 979.2 ms | - |
| 1,000 | Comet | 30 | 704.5 ms | 771.2 ms | - |
| 1,000 | Native Android | 30 | 181.0 ms | 204.0 ms | -43.7% vs MAUI; -74.3% vs Comet |

## Page transitions with 1,000 drinks

Lower is better. The timer starts at the existing navigation action-handler entry and ends at Android's accepted frame-commit callback. Each result has ten fresh-process samples. p90 uses nearest rank.

| Route | App | n | p50 | p90 | Native p50 difference |
|---|---|---:|---:|---:|---:|
| New Drink -> Activity | MAUI | 10 | 1854.5 ms | 1873.3 ms | - |
| New Drink -> Activity | Comet | 10 | 1490.6 ms | 1624.9 ms | - |
| New Drink -> Activity | Native Android | 10 | 203.8 ms | 213.2 ms | -89.0% vs MAUI; -86.3% vs Comet |
| Activity -> Settings | MAUI | 10 | 247.8 ms | 253.2 ms | - |
| Activity -> Settings | Comet | 10 | 399.9 ms | 450.2 ms | - |
| Activity -> Settings | Native Android | 10 | 46.5 ms | 64.5 ms | -81.2% vs MAUI; -88.4% vs Comet |

## Memory

Lower is better. Each value is the median of ten independent process runs. Each run value is the median of three `dumpsys meminfo --local` snapshots after a ten-second settle.

### PSS

| Drinks | State | MAUI | Comet | Native Android | Native vs MAUI | Native vs Comet |
|---:|---|---:|---:|---:|---:|---:|
| 100 | Initial New Drink | 169.27 MiB | 132.95 MiB | 116.19 MiB | -31.4% | -12.6% |
| 100 | First Activity | 173.05 MiB | 137.36 MiB | 120.01 MiB | -30.7% | -12.6% |
| 100 | Repeated navigation | 183.62 MiB | 145.78 MiB | 136.23 MiB | -25.8% | -6.6% |
| 1,000 | Initial New Drink | 176.91 MiB | 135.21 MiB | 127.11 MiB | -28.1% | -6.0% |
| 1,000 | First Activity | 182.53 MiB | 139.66 MiB | 129.49 MiB | -29.1% | -7.3% |
| 1,000 | Repeated navigation | 190.85 MiB | 150.04 MiB | 139.04 MiB | -27.1% | -7.3% |

### Supporting counters

| Drinks | State | App | Private dirty | Main-process RSS |
|---:|---|---|---:|---:|
| 100 | Initial New Drink | MAUI | 106.85 MiB | 281.76 MiB |
| 100 | Initial New Drink | Comet | 76.06 MiB | 246.14 MiB |
| 100 | Initial New Drink | Native Android | 74.50 MiB | 217.96 MiB |
| 100 | First Activity | MAUI | 109.93 MiB | 286.23 MiB |
| 100 | First Activity | Comet | 79.05 MiB | 251.61 MiB |
| 100 | First Activity | Native Android | 76.75 MiB | 222.62 MiB |
| 100 | Repeated navigation | MAUI | 120.11 MiB | 297.00 MiB |
| 100 | Repeated navigation | Comet | 87.38 MiB | 260.45 MiB |
| 100 | Repeated navigation | Native Android | 92.46 MiB | 239.09 MiB |
| 1,000 | Initial New Drink | MAUI | 113.22 MiB | 289.01 MiB |
| 1,000 | Initial New Drink | Comet | 78.04 MiB | 248.30 MiB |
| 1,000 | Initial New Drink | Native Android | 84.63 MiB | 229.24 MiB |
| 1,000 | First Activity | MAUI | 119.25 MiB | 295.50 MiB |
| 1,000 | First Activity | Comet | 81.06 MiB | 253.58 MiB |
| 1,000 | First Activity | Native Android | 85.94 MiB | 232.35 MiB |
| 1,000 | Repeated navigation | MAUI | 126.99 MiB | 304.20 MiB |
| 1,000 | Repeated navigation | Comet | 90.96 MiB | 264.38 MiB |
| 1,000 | Repeated navigation | Native Android | 94.86 MiB | 242.22 MiB |

## Signed APK size

The native result uses the clean product APK. It has no fixture, performance receiver, Ailoha, MAUI Controls, or MauiReactor payload.

| App | Signed APK | Native difference |
|---|---:|---:|
| MAUI | 37,023,552 bytes | - |
| Comet | 26,311,173 bytes | - |
| Native Android | 21,636,512 bytes | -41.6% vs MAUI; -17.8% vs Comet |

## Method and limits

- Startup uses Android `am start -W` `TotalTime` for process-cold TTID. A fresh process does not imply cold filesystem, database, or OS caches.
- Page timing excludes screenshots, USB transfer, host waits, and the 15-second quiet period. It includes app work from action-handler entry through accepted frame commit.
- Memory uses PSS as the primary measure. RSS includes shared resident pages. The three snapshots in one run are not independent observations.
- Repeated navigation is five Activity -> Settings -> New Drink -> Activity cycles after the first Activity visit. Retained memory does not by itself establish a leak.
- The native fixtures were created through public `BaristaNotes.Core` services. The 1,000-drink semantic hash matches the accepted MAUI and Comet study.
- Native AOT on Android is experimental (`XA1040`). Existing EF Core, SQLite, and Microsoft.Recognizers trimming/AOT warnings were retained and not suppressed.
- MAUI and Comet values are the saved accepted Pixel 5 results. They were not repeated. This is a nonconcurrent comparison.
- Native Android and MAUI had Android ART status `verify/install`. The accepted Comet result used `speed-profile/install-dm`.
- The native Activity acceptance check required the correct total count, 50 adapter items, visible content, hidden loading UI, pre-draw, and frame commit. The saved MAUI and Comet probes also inspected expected visible row content. Instrumentation overhead is not isolated.

## Evidence

- `results-20260927/startup/measured.csv`
- `results-20260927/memory/run-medians.csv`
- `results-20260927/pages/samples.csv`
- Raw `am start -W`, broadcast, and `dumpsys meminfo --local` output under `results-20260927/`
- Native fixture APKs and clean product APK under `apks/`
- Publish logs: `publish-native100.log`, `publish-native1000.log`, and `publish-native-clean.log`

## Native raw samples

### Startup TTID

| Drinks | Sample | TotalTime | PID |
|---:|---:|---:|---:|
| 100 | 1 | 184 ms | 30052 |
| 100 | 2 | 241 ms | 30102 |
| 100 | 3 | 174 ms | 30151 |
| 100 | 4 | 177 ms | 30201 |
| 100 | 5 | 166 ms | 30249 |
| 100 | 6 | 204 ms | 30298 |
| 100 | 7 | 172 ms | 30347 |
| 100 | 8 | 161 ms | 30398 |
| 100 | 9 | 168 ms | 30453 |
| 100 | 10 | 158 ms | 30502 |
| 100 | 11 | 184 ms | 30559 |
| 100 | 12 | 179 ms | 30621 |
| 100 | 13 | 206 ms | 30670 |
| 100 | 14 | 189 ms | 30723 |
| 100 | 15 | 197 ms | 30772 |
| 100 | 16 | 180 ms | 30825 |
| 100 | 17 | 164 ms | 30879 |
| 100 | 18 | 180 ms | 30933 |
| 100 | 19 | 181 ms | 30987 |
| 100 | 20 | 181 ms | 31037 |
| 100 | 21 | 177 ms | 31094 |
| 100 | 22 | 189 ms | 31149 |
| 100 | 23 | 189 ms | 31203 |
| 100 | 24 | 178 ms | 31252 |
| 100 | 25 | 185 ms | 31301 |
| 100 | 26 | 162 ms | 31356 |
| 100 | 27 | 179 ms | 31410 |
| 100 | 28 | 179 ms | 31464 |
| 100 | 29 | 190 ms | 31519 |
| 100 | 30 | 172 ms | 31573 |
| 1,000 | 1 | 196 ms | 31973 |
| 1,000 | 2 | 180 ms | 32028 |
| 1,000 | 3 | 217 ms | 32084 |
| 1,000 | 4 | 187 ms | 32141 |
| 1,000 | 5 | 203 ms | 32197 |
| 1,000 | 6 | 172 ms | 32254 |
| 1,000 | 7 | 204 ms | 32311 |
| 1,000 | 8 | 210 ms | 32368 |
| 1,000 | 9 | 204 ms | 32424 |
| 1,000 | 10 | 193 ms | 32509 |
| 1,000 | 11 | 186 ms | 32581 |
| 1,000 | 12 | 173 ms | 32672 |
| 1,000 | 13 | 185 ms | 32727 |
| 1,000 | 14 | 171 ms | 347 |
| 1,000 | 15 | 177 ms | 604 |
| 1,000 | 16 | 181 ms | 778 |
| 1,000 | 17 | 171 ms | 835 |
| 1,000 | 18 | 177 ms | 903 |
| 1,000 | 19 | 181 ms | 1239 |
| 1,000 | 20 | 198 ms | 1339 |
| 1,000 | 21 | 188 ms | 1463 |
| 1,000 | 22 | 182 ms | 1568 |
| 1,000 | 23 | 176 ms | 1624 |
| 1,000 | 24 | 174 ms | 1694 |
| 1,000 | 25 | 208 ms | 1803 |
| 1,000 | 26 | 170 ms | 1860 |
| 1,000 | 27 | 167 ms | 1917 |
| 1,000 | 28 | 163 ms | 1984 |
| 1,000 | 29 | 171 ms | 2082 |
| 1,000 | 30 | 159 ms | 2206 |

### Page transitions

| Route | Sample | Duration | PID |
|---|---:|---:|---:|
| New Drink -> Activity | 1 | 201.006 ms | 9292 |
| New Drink -> Activity | 2 | 206.024 ms | 9369 |
| New Drink -> Activity | 3 | 209.066 ms | 9442 |
| New Drink -> Activity | 4 | 234.623 ms | 9548 |
| New Drink -> Activity | 5 | 213.169 ms | 9878 |
| New Drink -> Activity | 6 | 178.513 ms | 9950 |
| New Drink -> Activity | 7 | 185.773 ms | 10025 |
| New Drink -> Activity | 8 | 179.099 ms | 10132 |
| New Drink -> Activity | 9 | 207.857 ms | 10214 |
| New Drink -> Activity | 10 | 201.651 ms | 10287 |
| Activity -> Settings | 1 | 71.297 ms | 10361 |
| Activity -> Settings | 2 | 38.191 ms | 10449 |
| Activity -> Settings | 3 | 45.246 ms | 10541 |
| Activity -> Settings | 4 | 56.580 ms | 10639 |
| Activity -> Settings | 5 | 41.216 ms | 10728 |
| Activity -> Settings | 6 | 38.691 ms | 10817 |
| Activity -> Settings | 7 | 47.833 ms | 10933 |
| Activity -> Settings | 8 | 44.794 ms | 11081 |
| Activity -> Settings | 9 | 64.467 ms | 11168 |
| Activity -> Settings | 10 | 48.657 ms | 11260 |

### Memory run medians

| Drinks | Run | State | PSS | Private dirty | RSS | PID |
|---:|---:|---|---:|---:|---:|---:|
| 100 | 1 | Initial New Drink | 116.17 MiB | 74.55 MiB | 217.95 MiB | 2300 |
| 100 | 1 | First Activity | 119.90 MiB | 76.75 MiB | 222.44 MiB | 2300 |
| 100 | 1 | Repeated navigation | 136.13 MiB | 92.46 MiB | 238.95 MiB | 2300 |
| 100 | 2 | Initial New Drink | 116.17 MiB | 74.46 MiB | 217.95 MiB | 3088 |
| 100 | 2 | First Activity | 120.02 MiB | 76.67 MiB | 222.61 MiB | 3088 |
| 100 | 2 | Repeated navigation | 136.18 MiB | 92.36 MiB | 239.06 MiB | 3088 |
| 100 | 3 | Initial New Drink | 116.17 MiB | 74.46 MiB | 217.95 MiB | 3525 |
| 100 | 3 | First Activity | 119.99 MiB | 76.76 MiB | 222.59 MiB | 3525 |
| 100 | 3 | Repeated navigation | 136.01 MiB | 92.50 MiB | 238.84 MiB | 3525 |
| 100 | 4 | Initial New Drink | 116.27 MiB | 74.57 MiB | 218.05 MiB | 3826 |
| 100 | 4 | First Activity | 119.95 MiB | 76.80 MiB | 222.54 MiB | 3826 |
| 100 | 4 | Repeated navigation | 136.31 MiB | 92.50 MiB | 239.12 MiB | 3826 |
| 100 | 5 | Initial New Drink | 115.69 MiB | 74.43 MiB | 217.47 MiB | 4232 |
| 100 | 5 | First Activity | 119.96 MiB | 76.73 MiB | 222.55 MiB | 4232 |
| 100 | 5 | Repeated navigation | 136.07 MiB | 92.45 MiB | 238.95 MiB | 4232 |
| 100 | 6 | Initial New Drink | 116.21 MiB | 74.49 MiB | 217.97 MiB | 4548 |
| 100 | 6 | First Activity | 120.04 MiB | 76.69 MiB | 222.63 MiB | 4548 |
| 100 | 6 | Repeated navigation | 136.27 MiB | 92.46 MiB | 239.15 MiB | 4548 |
| 100 | 7 | Initial New Drink | 116.21 MiB | 74.49 MiB | 217.98 MiB | 4871 |
| 100 | 7 | First Activity | 120.19 MiB | 76.77 MiB | 222.78 MiB | 4871 |
| 100 | 7 | Repeated navigation | 136.48 MiB | 92.47 MiB | 239.30 MiB | 4871 |
| 100 | 8 | Initial New Drink | 117.91 MiB | 75.94 MiB | 219.80 MiB | 5171 |
| 100 | 8 | First Activity | 122.43 MiB | 78.77 MiB | 225.09 MiB | 5171 |
| 100 | 8 | Repeated navigation | 136.46 MiB | 92.46 MiB | 239.41 MiB | 5171 |
| 100 | 9 | Initial New Drink | 116.27 MiB | 74.54 MiB | 218.22 MiB | 5461 |
| 100 | 9 | First Activity | 119.95 MiB | 76.65 MiB | 222.73 MiB | 5461 |
| 100 | 9 | Repeated navigation | 136.31 MiB | 92.30 MiB | 239.31 MiB | 5461 |
| 100 | 10 | Initial New Drink | 115.71 MiB | 74.50 MiB | 217.56 MiB | 5767 |
| 100 | 10 | First Activity | 120.10 MiB | 76.74 MiB | 222.76 MiB | 5767 |
| 100 | 10 | Repeated navigation | 135.89 MiB | 92.47 MiB | 239.04 MiB | 5767 |
| 1,000 | 1 | Initial New Drink | 127.72 MiB | 84.62 MiB | 229.76 MiB | 6171 |
| 1,000 | 1 | First Activity | 130.06 MiB | 85.96 MiB | 232.87 MiB | 6171 |
| 1,000 | 1 | Repeated navigation | 139.54 MiB | 94.86 MiB | 242.64 MiB | 6171 |
| 1,000 | 2 | Initial New Drink | 127.11 MiB | 84.57 MiB | 229.16 MiB | 6468 |
| 1,000 | 2 | First Activity | 129.53 MiB | 85.93 MiB | 232.35 MiB | 6468 |
| 1,000 | 2 | Repeated navigation | 140.61 MiB | 96.36 MiB | 243.77 MiB | 6468 |
| 1,000 | 3 | Initial New Drink | 127.23 MiB | 84.68 MiB | 229.36 MiB | 6819 |
| 1,000 | 3 | First Activity | 129.78 MiB | 85.93 MiB | 232.69 MiB | 6819 |
| 1,000 | 3 | Repeated navigation | 139.07 MiB | 94.82 MiB | 242.25 MiB | 6819 |
| 1,000 | 4 | Initial New Drink | 127.11 MiB | 84.62 MiB | 229.25 MiB | 7153 |
| 1,000 | 4 | First Activity | 129.45 MiB | 85.96 MiB | 232.35 MiB | 7153 |
| 1,000 | 4 | Repeated navigation | 138.68 MiB | 94.78 MiB | 242.05 MiB | 7153 |
| 1,000 | 5 | Initial New Drink | 127.70 MiB | 84.66 MiB | 229.96 MiB | 7468 |
| 1,000 | 5 | First Activity | 129.79 MiB | 85.94 MiB | 232.81 MiB | 7468 |
| 1,000 | 5 | Repeated navigation | 140.46 MiB | 96.27 MiB | 243.70 MiB | 7468 |
| 1,000 | 6 | Initial New Drink | 126.88 MiB | 84.64 MiB | 229.07 MiB | 7767 |
| 1,000 | 6 | First Activity | 129.31 MiB | 85.95 MiB | 232.26 MiB | 7767 |
| 1,000 | 6 | Repeated navigation | 138.80 MiB | 94.86 MiB | 242.04 MiB | 7767 |
| 1,000 | 7 | Initial New Drink | 127.32 MiB | 84.64 MiB | 229.51 MiB | 8062 |
| 1,000 | 7 | First Activity | 129.59 MiB | 85.98 MiB | 232.55 MiB | 8062 |
| 1,000 | 7 | Repeated navigation | 138.74 MiB | 94.79 MiB | 241.98 MiB | 8062 |
| 1,000 | 8 | Initial New Drink | 127.07 MiB | 84.64 MiB | 229.21 MiB | 8357 |
| 1,000 | 8 | First Activity | 129.41 MiB | 85.98 MiB | 232.31 MiB | 8357 |
| 1,000 | 8 | Repeated navigation | 139.00 MiB | 94.87 MiB | 242.19 MiB | 8357 |
| 1,000 | 9 | Initial New Drink | 126.93 MiB | 84.57 MiB | 229.06 MiB | 8684 |
| 1,000 | 9 | First Activity | 129.16 MiB | 85.92 MiB | 232.06 MiB | 8684 |
| 1,000 | 9 | Repeated navigation | 140.81 MiB | 97.18 MiB | 244.00 MiB | 8684 |
| 1,000 | 10 | Initial New Drink | 127.10 MiB | 84.56 MiB | 229.23 MiB | 8987 |
| 1,000 | 10 | First Activity | 129.41 MiB | 85.87 MiB | 232.30 MiB | 8987 |
| 1,000 | 10 | Repeated navigation | 138.65 MiB | 94.77 MiB | 241.77 MiB | 8987 |
