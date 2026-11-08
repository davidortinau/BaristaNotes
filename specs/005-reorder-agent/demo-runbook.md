# Demo runbook: reorder agent

> [!summary]
> One `aspire start`, then everything on stage runs from the app, the shop page, and the Aspire dashboard. Reset between rehearsals with **Reset demo** (dashboard) and **Reset demo data** (app Settings). If the Wi-Fi fails, switch the dashboard to **Use rehearsal answers**. The ledger says so out loud.

## AI development-loop demo (10-12 minutes)

The story is not just "I added an AI feature to David's app." It is "I made
the running app observable and operable by a coding agent, so it can close
the loop from a request to verified behavior." Keep Copilot, the Mac app,
and the live web companion visible; use the Aspire dashboard for the
resource graph and trace waterfall.

Start in PostgreSQL mode for the database-tool beat. Before a fresh
rehearsal, use the dashboard's **Reset demo** and the app's **Reset demo
data**, then check in through the app. Do not reset in the middle of the
walkthrough. If using the current populated demo instead, show its existing
run and use **Run now** without promising the original two-pick scenario.

| Beat | Show / ask the coding agent | What the augmentation enables |
|---|---|---|
| 1. Teach it the environment (1 min) | Show `.mcp.json`, the project-local `.agents/skills/`, and the matching MAUI CLI/agent versions. Explain that `aspire agent init` supplied the Aspire guidance. | Checked-in workflows and tools rather than relying on a chat remembering machine-specific instructions. Public package sources and an SDK pin make the setup repeatable. |
| 2. Discover the running system (1 min) | "Use Aspire to show this AppHost's resources, endpoints, and health." Show the native heads, API, shop, Foundry, web, and PostgreSQL on the graph. | One orchestration model supplies dependencies, service discovery, startup, and resource commands. The coding agent can inspect it through Aspire MCP. |
| 3. Give it native eyes and hands (2 min) | "Use DevFlow to capture the Mac app, inspect its visual tree, and open Agent activity." Let it identify controls by automation ID, not guessed screen coordinates. | Debug-only DevFlow exposes screenshots, the live UI tree, interaction, and app diagnostics. This works against the real MAUI app, not a browser imitation. |
| 4. Follow a real action across surfaces (2 min) | From the native app, save a cupboard check-in if needed and press **Run now**. Watch the companion's journal counts, ledger, picks, and memory update without a page refresh. | The native app and read-only React companion share the same API. The web polls every two seconds; it is not replaying a scripted animation. Foundry credentials stay server-side. |
| 5. Inspect, don't guess (2 min) | "Find the latest run's trace and explain where the time went." Show API/shop spans and the learner/ranker model calls. Then ask for the tracked browser's activity or error logs through Aspire. | Service-default telemetry and AI spans expose backend/model behavior; `WithBrowserLogs()` brings the tracked Edge browser's diagnostics into the same tooling. Prompt capture is development-only. |
| 6. Verify persisted reality (1 min) | "Through the Aspire MCP gateway, list the agent tables and show the latest run's status, picks, and roasters checked. Verify the connection is read-only." | PostgreSQL's MCP tools are discovered through Aspire, with restricted access. Query `Runs`, then `SHOW transaction_read_only`; this is the agent backend, not the native app's local journal database. |
| 7. Close the edit/verify loop (2 min) | "Change the web heading to 'From cupboard to coffee', build and lint it, then use Playwright to confirm the new heading and that the live ledger still appears." Show the Vite update and the browser evidence. | The coding agent can change code and verify rendered behavior with browser automation. For a native change, use the separate build/deploy/DevFlow loop; do not claim the removed private MAUI hot-reload callbacks still work. |
| 8. Bring the control room into Copilot (1 min) | Open the Aspire AppHosts canvas, show its Resources and Graph tabs, then choose **Run agent** on the API card and watch the app/web update. | The project-local canvas exposes the same AppHost-defined commands, endpoints, health, and diagnostics inside Copilot. No duplicate command wiring. |

Finish with: **"The coding agent can see the UI, operate the app, inspect
the services and database, and prove its change against the running system."**

The canvas is installed in `.github/extensions/aspire-apphosts` from
[`microsoft/aspire-skills`](https://github.com/microsoft/aspire-skills/tree/main/extensions/aspire-apphosts),
with its upstream MIT license. Open it with `open_aspire_apphosts` targeting
`src/BaristaNotes.AppHost/BaristaNotes.AppHost.csproj`. Custom commands live
on their owning resource cards; the Graph tab is read-only. The **Stop
window off** command was exercised successfully through the canvas command
handler without resetting data or creating orders.

Optional extensions:

- **Failure visibility:** stop only the `api` resource, show the web's
  stale-data warning and tracked-browser errors, then start `api` again and
  show automatic recovery. Do not stop the whole AppHost for this beat.
- **Repeatable native verification:** show the `run-demo-test` skill and
  `scripts/demo_test.py`: create a bean, profile, and drink through Android
  UI, reopen the saved data, and verify it after a data-preserving deployment.
  Run this as a separate prepared Android demo, not an unannounced target
  switch from the Mac app.
- **Dependency evidence:** show the `dotnet-inspect` skill for inspecting
  actual package APIs/source rather than inventing preview API signatures.
- **Storage choice:** show the **storage** parameter's **Set parameter** command,
  choose SQLite or PostgreSQL, and save to user secrets. Prefer explaining this
  after the live flow: restarting changes web ports, and the shop's
  in-memory carts disappear even though API state persists.
- **Rehearsal controls:** show the existing replay, reset, timeout,
  idempotency, and price-change resource commands as repeatable scenarios,
  not ad-hoc database edits. Rehearsal model answers are explicitly labeled
  in the ledger.

## Before the talk

### First run on another Mac

The repository pins the .NET 11 RC3 SDK and uses Aspire CLI 13.6.4. The
backend and AppHost target .NET 10: install the .NET 10 and ASP.NET Core 10
runtimes in the same dotnet installation as the SDK. The MAUI
heads additionally require MAUI workloads and
full Xcode selected with `xcode-select` (Command Line Tools alone are not enough).
Starting the dashboard does not build or launch a MAUI head; those resources
start explicitly from the dashboard.

On a Mac where Xcode is installed but not selected globally, prefix the start
command with `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer`.
When using the ignored repo-local SDK, also set `DOTNET_ROOT="$PWD/.dotnet"`
and prepend it to `PATH`; this lets the child MAUI build find its workloads.
Podman is supported: start its existing machine and set
`ASPIRE_CONTAINER_RUNTIME=podman` when starting the AppHost.

By default, the AppHost provisions Azure AI Foundry with three deployments
(GlobalStandard, capacity 10 each): `gpt-4.1-mini` uses GPT-4.1 mini,
`gpt-4o` uses GPT-4.1 for vision, and `gpt-4o-mini` uses GPT-4.1 mini for
extraction. The last two retain the app's existing deployment names, not the
underlying model names: Azure no longer permits new GPT-4o mini deployments.
Provisioning and model calls can incur Azure charges. Configure your own
subscription and a region with quota for all three models:

```bash
az login
dotnet user-secrets set "Azure:SubscriptionId" "<subscription-id>" --project src/BaristaNotes.AppHost
dotnet user-secrets set "Azure:Location" "swedencentral" --project src/BaristaNotes.AppHost
aspire start --apphost src/BaristaNotes.AppHost/BaristaNotes.AppHost.csproj
```

Aspire creates a development resource group and assigns the developer the
Foundry roles. The API uses `DefaultAzureCredential` for both agent calls and
forwarded app requests. Credentials are never sent to the app. Provisioned
Azure resources remain after stopping the AppHost.

To reuse an existing account instead, set `ConnectionStrings:foundry` in the
AppHost user secrets to `Endpoint=https://<account>.cognitiveservices.azure.com/`
for Entra ID, or include `;Key=<key>` for key authentication. This skips
provisioning; the account must already have the three deployments and the
signed-in developer needs permission to invoke them. Remove that connection
string to return to Aspire provisioning.

NuGet restore uses the public feed, not a required machine-specific sibling
folder. The MAUI Debug head and local `maui` tool use the matching public
DevFlow preview `0.1.0-preview.13.26526.5`. Run `dotnet tool restore`, then
`dotnet maui devflow --help`. The private Labs HotReload integration is
removed; its custom app/theme hot-reload callbacks are not part of this demo.
MauiReactor's own hot-reload feature switch remains enabled in Debug.

### SQLite or PostgreSQL

The startup option switches the **agent API's** storage, not the MAUI app's
on-device journal. SQLite is the default and needs no container engine.

In the resource workbench or dashboard, **storage → Set parameter** offers
SQLite and PostgreSQL. Enable **Save to user secrets**, then restart the
entire AppHost to apply the resource-graph change. The saved `Parameters:storage`
value takes precedence over the legacy `Demo:Storage` setting shown below.
For a command-line override, use `--Parameters:storage=sqlite` or
`--Parameters:storage=postgres` instead.

The legacy option still works when no storage parameter has been saved:

```bash
aspire start --non-interactive --apphost src/BaristaNotes.AppHost/BaristaNotes.AppHost.csproj -- --Demo:Storage=sqlite
```

For PostgreSQL and the MCP gateway demo, stop this AppHost and start it again:

```bash
aspire stop --non-interactive --apphost src/BaristaNotes.AppHost/BaristaNotes.AppHost.csproj
aspire start --non-interactive --apphost src/BaristaNotes.AppHost/BaristaNotes.AppHost.csproj -- --Demo:Storage=postgres
aspire wait agentdb-mcp --apphost src/BaristaNotes.AppHost/BaristaNotes.AppHost.csproj --timeout 60
```

SQLite keeps `src/BaristaNotes.Api/App_Data/agent.db`; PostgreSQL keeps a
named data volume. Settings, check-ins, runs, memory, and proposals remain
independent in each store. Switching does not copy or delete them. A new
store needs its own check-in: mark a currently open bag finished in the app,
save, then run the agent. Previously finished bags are no longer in the
app's open-bag check-in list.

Postgres mode adds `postgres`, `agentdb`, and `agentdb-mcp`. Its MCP server
uses **restricted/read-only access**, including read-only SQL transactions.
Discover and call it through Aspire:

```bash
aspire mcp tools --apphost src/BaristaNotes.AppHost/BaristaNotes.AppHost.csproj --format Json
aspire mcp call agentdb-mcp list_objects --input '{"schema_name":"public"}' --apphost src/BaristaNotes.AppHost/BaristaNotes.AppHost.csproj
aspire mcp call agentdb-mcp execute_sql --input '{"sql":"SELECT \"Status\", \"Picks\" FROM \"Runs\" ORDER BY \"StartedAt\" DESC LIMIT 5"}' --apphost src/BaristaNotes.AppHost/BaristaNotes.AppHost.csproj
```

The same nine database tools are proxied into the Aspire MCP server used by
the coding agent. If they are not visible after switching, list AppHosts,
select this exact AppHost, and refresh the MCP tools.

### Mac app to live web companion

The `web` resource is a React / TypeScript / Vite dashboard. Use its URL from
the Aspire dashboard or `aspire describe`; the web port is allocated at startup.
The storage badge reports the API's actual provider. The page polls every two
seconds for shared journal counts, budget, latest-run ledger, picks, and memory.
It is read-only and shows no fabricated activity.

1. Start `app-maccatalyst` from Aspire. In the app, open Settings → Check-in,
   mark an open bag finished, and save. The web check-in and journal counts update.
2. In the app, open Agent activity → **Run now**. Watch steps and recommendations
   appear on the web without refreshing it.
3. Use **Open tracked browser** on `web-browser-logs`. Its console log stream
   includes actual activity changes, manual refreshes, fetches, and errors:
   `aspire logs web-browser-logs --apphost src/BaristaNotes.AppHost/BaristaNotes.AppHost.csproj`.

To select Edge Beta on this Mac without hardcoding it into the AppHost:

```bash
dotnet user-secrets set "Aspire:Hosting:BrowserLogs:web:Browser" "/Applications/Microsoft Edge Beta.app/Contents/MacOS/Microsoft Edge Beta" --project src/BaristaNotes.AppHost
```

The web page retains its last successful data during an API outage, labels it
with a refresh error, and reconnects automatically. The tracked browser uses an
Aspire-managed profile, not the user's normal browser profile.

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
2. The browser with the live `web` companion, the shop (`https://localhost:7032/orders`),
   and the Aspire dashboard. Open the companion through `web-browser-logs` to show
   browser telemetry.

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
