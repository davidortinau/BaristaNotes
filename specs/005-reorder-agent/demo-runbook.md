# Demo runbook: reorder agent

> [!summary]
> One `aspire start`, then everything on stage runs from the app, the shop page, and the Aspire dashboard. Reset between rehearsals with **Reset demo** (dashboard) and **Reset demo data** (app Settings). If the Wi-Fi fails, switch the dashboard to **Use rehearsal answers**. The ledger says so out loud.

## Before the talk

| When | Do | Check |
|---|---|---|
| Day before | Rehearse the full script once on the presentation machine with the live model | `src/BaristaNotes.Api/App_Data/replay/` has recordings. Each model request is recorded once |
| 30 min before | `aspire start --apphost src/BaristaNotes.AppHost/BaristaNotes.AppHost.csproj` | Dashboard opens. `api` and `shop` are Healthy |
| 30 min before | Start `app-maccatalyst` from the dashboard (Start button), or `aspire resource app-maccatalyst start --apphost src/BaristaNotes.AppHost/BaristaNotes.AppHost.csproj`. If the app is already open from an earlier build, quit it first: a resource restart does not relaunch a running app | The app window opens with the demo data (Kochere, Huila, Nyeri open) |
| 20 min before | Dashboard `api` → **Reset demo**. App → Settings → **Reset demo data** | Activity tab shows "Still have these?" |
| 20 min before | Dashboard `shop` → **Honor idempotency keys**. Open `https://localhost:7032/orders` in the browser | No orders |
| 10 min before | On the venue network: dashboard `api` → **Run agent** once, then **Reset demo** again | Run reaches "Run finished" in about 25 s. If it fails, switch to **Use rehearsal answers** |

## Windows on screen

1. The app (Mac Catalyst).
2. The browser with two tabs: the shop (`https://localhost:7032/orders`) and the Aspire dashboard.

## Script

| Beat | On stage | What the audience sees | Dashboard or shop |
|---|---|---|---|
| 1 Check-in | Activity tab → **Still have these?** → mark Kochere Natural **Finished** → Save | The app asks for what it cannot see | |
| 2 Run | Settings → Agent activity → **Run now** | The ledger fills step by step: checked, skipped with reasons | Optional: show the trace in the dashboard (Traces → `invoke_agent`) |
| 3 While you were away | Back to Activity: the card "While you were away · checked 5 roasters · skipped 12 · 1 pick" | Doing nothing, with a reason, is also a ledger entry | |
| 4 Why this one | Tap the pick (Guji Natural) | Why, evidence from your own notes with ratings, what it does not know, budget left. "Checked 4 citations against your notes. All found." | |
| 5 Buy | **Buy** opens the shop checkout. Place the order | "Prepared by the Barista Notes agent". Back in the app: Ordered #1001, budget €57.50 left | Shop orders page shows #1001 |
| 6 Autonomy and retry | Settings → autonomy **Reorder**. Check in Huila Washed as finished. Dashboard `shop` → **Arm order timeout**. **Run now** | Ledger: "Ordering...", "Retrying POST /api/orders ... same Idempotency-Key", "The shop returned the existing order #1002". One order | Shop orders page: one #1002 |
| 6b The bug (optional) | Reset first (see below). Dashboard `shop` → **Ignore idempotency keys** + **Arm order timeout**. Run | Ledger: "Second order for the same pick: #1002 ... charged twice" | Shop orders page: two orders marked Duplicate |
| 7 Stop | Settings → autonomy **Cart**. Check in Nyeri Washed as finished. **Run now**, then **Stop** right after "Cart ready at Tidewell Roasting" (set the step delay to 2500 ms if the timing is tight) | "Stopped by you. Already done: the cart at Tidewell Roasting is still open." **Remove cart** on that entry | Shop cart status is "removed" |
| 8 Memory | Settings → Memory: show what it learned, with your drinks as evidence. Forget one item: it moves to Removed and is never relearned. Then on the Guji pick tap **Never this roaster** and run again | The next run skips Copperhaze Coffee ("You said never this roaster") and picks Rwanda Natural instead. Recommended: forgetting "likes naturals" alone does not change the pick, because other preferences and the finished bag's own notes still point to Guji | |

## Dashboard commands

| Resource | Command | Effect |
|---|---|---|
| api | Run agent | Same as Run now in the app |
| api | Stop run | Stops the run that is going |
| api | Use rehearsal answers | No model calls. Recorded answers only. The ledger says "Used a rehearsal answer" |
| api | Use live model | Back to Foundry. Replays only if Foundry cannot be reached |
| api | Reset demo | Clears runs, picks, orders, check-ins, and memory in the API and the shop |
| shop | Arm order timeout | The next order is created, then the shop waits 6 s. The API gives up after 3 s and retries |
| shop | Ignore idempotency keys | The shop creates a new order for every request, like a buggy store |
| shop | Honor idempotency keys | The fix |

## Timing

- Measured on 21 October with the default step delay of 900 ms: a first run (with learning) takes about 20 s, a later run (remembered preferences) about 15 s. Learning is about 6-10 s, ranking about 5 s.
- Change the pacing with `stepDelayMs` in the agent settings (`PUT /agent/settings`).

## If something goes wrong

| Problem | Do |
|---|---|
| The run fails at "Learning" or "Comparing" | Dashboard `api` → **Use rehearsal answers**, then Run again. Say: "The Wi-Fi lost, so it is using the answer it gave in rehearsal. The ledger tells you that." |
| The app shows "Agent not connected" | The app was started outside the AppHost. Quit it and start `app-maccatalyst` from the dashboard |
| A cart or order is left from rehearsal | Dashboard `api` → **Reset demo**, app → **Reset demo data** |
| The AppHost was restarted | The shop keeps orders in memory, so it starts empty while the API remembers. Dashboard `api` → **Reset demo** |
| The Mac Catalyst app will not start | Dashboard → start `app-ios` instead (simulator "BaristaNotes Agent Demo"). Same API, same demo data |
| Nothing works | Play the backup video |

## Fallback head: iOS simulator

- `app-ios` runs on the simulator "BaristaNotes Agent Demo" (iOS 27.0, iPhone 17 Pro). Its UDID is in the AppHost user secrets as `Demo:IosSimulatorUdid`.
- The simulator trusts the ASP.NET Core dev certificate (`xcrun simctl keychain <udid> add-root-cert`). Do it again if the dev certificate is renewed.
- On another Mac: create a simulator, add the root certificate, and set `Demo:IosSimulatorUdid`.
