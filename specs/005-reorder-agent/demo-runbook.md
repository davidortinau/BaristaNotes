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

Talk order, with the 21 October decisions. Runs take about 15-20 s at 900 ms steps.

### TF-10 Main demo (14:15-26:00)

| Beat | On stage | What the audience sees |
|---|---|---|
| 1 Check-in | Activity tab → **Still have these?** → mark **Kochere Natural** and **Nyeri Washed** finished → Save | The app asks for what it cannot see |
| 2 Run, unattended | Stay on Activity. Dashboard `api` → **Run agent** (the agent works on its own). Talk over the card "Agent at work" | It is working without you watching |
| 3 While you were away | The card turns into "While you were away · checked 5 roasters · skipped … · 2 picks" → **View** | The ledger: checked, skipped with reasons (sold out, roasted too long ago, over budget, you dislike dark roasts, you still have a bag), evidence checked, two picks, two carts, nothing charged |
| 4 Why this one | Open the **Guji Natural** pick (for Kochere) | Why, evidence from your own notes with ratings and dates, what it does not know, budget left €100 |
| 5 Buy | **Buy** → shop checkout ("Prepared by the Barista Notes agent") → **Place order** → back to the app | Ordered #1001, €77.50 left |

### TF-11 Inspectable memory (26:00-29:00)

| Step | On stage | What the audience sees |
|---|---|---|
| 1 | Settings → Memory | Each preference cites your drinks. "3 guest drinks not used for learning": Sam's dark-roast lattes. An exception is not a preference |
| 2 | Expand "You like natural-process coffees" → **Forget this** → Forget | It moves to Removed: never learned again |
| 3 | Agent activity → open the **Rwanda Natural** pick (for Nyeri, Tidewell Roasting) → **Never this roaster** → Never | You decide which roasters it may use |
| 4 | **Run now** | "Tidewell Roasting: you said never this roaster." Nyeri's pick changes, for example to Guji Natural |

### TF-12 Architecture (29:00-33:00)

Slide, then the dashboard: Traces → `api: POST /runs` → two `invoke_agent` spans (preference-learner, reorder-ranker), each with a `chat gpt-4.1-mini` call to Foundry, the shop calls in between. Click `invoke_agent reorder-ranker` → `gen_ai.input.messages`: exactly what the model saw. Say: Development only; in production this is personal data, so prompt capture is off.

### TF-14 Stopped is not undone, stale approval (36:00-42:00)

| Step | On stage | What the audience sees |
|---|---|---|
| 1 | Dashboard `api` → **Stop window on (3 s)** | (Each cart or order is followed by a 3 s pause) |
| 2 | Agent activity → **Run now**. When "Cart ready at …" appears → **Stop** | "Stopped by you. Already done: the cart at … is still open." **Remove cart** on that entry |
| 3 | Open the earlier Guji pick for Nyeri (cart ready). Dashboard `shop` → **Raise prices 10%** → tap **Buy** | "The price changed from €17.50 to €19.25 since I picked it." No checkout opens; the pick is Out of date |
| 4 | Dashboard `shop` → **Normal prices**; `api` → **Stop window off** | |

### TF-15 Retry without duplicate effects (42:00-45:00)

| Step | On stage | What the audience sees |
|---|---|---|
| 1 | Settings → autonomy **Reorder**. Check-in: **Huila Washed** finished | Reorder favorites: it may order a finished favorite within budget |
| 2 | Dashboard `shop` → **Ignore idempotency keys** and **Arm order timeout**. **Run now** | "Ordering Huila Washed…", "Retrying POST /api/orders … same Idempotency-Key", "Second order for the same pick: #1003 … charged twice". Shop Orders page: two orders marked Duplicate. The run also prepares a cart for Nyeri: "New coffee, so I prepared a cart instead" |
| 3 | Dashboard `shop` → **Honor idempotency keys** and **Arm order timeout**. Check-in: **Huehuetenango Washed** finished. **Run now** | "Retrying …", "The shop returned the existing order #1004." One order. €15.00 left of €100 |

No retry code was written: Aspire service defaults add the standard resilience handler, which retries POST requests too.

## Dashboard commands

| Resource | Command | Effect |
|---|---|---|
| api | Run agent | Same as Run now in the app |
| api | Stop window on (3 s) / Stop window off | Pause 3 s after each cart or order, so there is time to tap Stop (TF-14) |
| api | Stop run | Stops the run that is going |
| api | Use rehearsal answers | No model calls. Recorded answers only. The ledger says "Used a rehearsal answer" |
| api | Use live model | Back to Foundry. Replays only if Foundry cannot be reached |
| api | Reset demo | Clears runs, picks, orders, check-ins, and memory in the API and the shop |
| shop | Arm order timeout | The next order is created, then the shop waits 6 s. The API gives up after 3 s and retries |
| shop | Disarm order timeout | Turns the timeout off if no order used it (for example after a Cart-mode run) |
| shop | Ignore idempotency keys | The shop creates a new order for every request, like a buggy store. Turn it off with **Honor idempotency keys** right after beat 6b: while it is on, refreshing a checkout page also creates a second order |
| shop | Honor idempotency keys | The fix |
| shop | Raise prices 10% | Every price goes up 10%. Picks made before are out of date, and Buy refuses them |
| shop | Normal prices | Back to the catalog prices. **Reset demo** also does this |

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
