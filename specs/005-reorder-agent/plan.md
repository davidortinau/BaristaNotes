# Implementation Plan: Reorder agent for the Trust Fall demo

**Branch:** `005-reorder-agent`, from `main` at `8b7c138` | **Date:** 2026-10-20 | **Talk:** Techorama NL, Tuesday 27 October, 16:00 | **Status:** Gates 1-3 done on 21 October. Gate 4 partly done. See [the runbook](demo-runbook.md).

> [!summary]
> Add an Aspire AppHost, a Web API that runs a coffee reorder agent, and a fictional roaster shop. Everything runs on this Mac. The agent checks the roasters you watch, skips what does not fit, explains its pick with your own tasting notes, and prepares a cart. Within a fixed monthly budget it can reorder a favorite on its own. The Foundry key moves out of the app into AppHost user secrets. The API forwards the app's existing AI calls, so no key ships in the app.

## Progress

| Gate | Status, 2026-10-21 |
|---|---|
| 1 | **Done.** The app's own AI call goes through the forwarder: the voice harness asked "How many shots have I pulled?" and got "You have pulled 22 shots in total" (`POST /openai/deployments/gpt-4.1-mini/chat/completions`, 200) |
| 2 | **Done.** Beats 2-5 run end to end in the Mac Catalyst app: run, ledger, pick with evidence, Buy, checkout in the shop, signed webhook, Ordered in the app |
| 3 | **Done.** All 8 beats work in the app, driven with DevFlow. Not automated: the three confirmation popups (Forget, Never this roaster, Reset demo data). Their API calls are tested |
| 4 | **Partly done.** Demo data, pacing, dashboard commands, rehearsal replay, and the [runbook](demo-runbook.md) are done. Left for David: the rehearsal on the presentation machine, the backup video, and the [open decisions](#needs-davids-input) |

Tests: 49 API, 36 shop, 1005 app (52 new). Commits on the branch: `c1bfd0d` to `0f717d0`.

### Added beyond the plan

| What | Why |
|---|---|
| Rehearsal replay (`ReplayChatClient`): records each model answer, replays it when Foundry cannot be reached, or always via the dashboard command **Use rehearsal answers** | Conference Wi-Fi. The ledger says "Used a rehearsal answer", so the stage never pretends |
| Suggestion from a roaster you don't watch (Fennick Roastery). Ledger only, no cart | David's original "broaden out to other roasters" idea, kept at suggestion level |
| More code checks on model output: a like must cite drinks rated 3-4 and a dislike 0-2; a finished coffee rated below 3 is never re-picked; the ranker is told what you had from each roaster | Each came from a real model mistake in testing (see the [findings log](#findings-log)) |
| `app-ios` resource on a dedicated simulator, "BaristaNotes Agent Demo" (iOS 27.0, iPhone 17 Pro, trusts the dev certificate) | The fallback stage head from the plan. Tested: it connects to the API and loads the demo data |
| Automation IDs on all agent tiles, from their accessible names | DevFlow and future UI tests |

### What Mac Catalyst needed

| Problem | Fix |
|---|---|
| No window: UIKit 27 requires the scene lifecycle | `Platforms/MacCatalyst/SceneDelegate.cs` and a scene manifest in its `Info.plist`, copied from the iOS head |
| Crash: the first page resolved `IOverlayService` before the overlay existed | `VoiceOverlay` is now created before the window content is mapped |
| Restore failed, and the app was killed at launch after incremental builds | This Mac had an internal RC2 SDK. Now on the public SDK `11.0.100-rc.2.26504.105` and workload set `11.0.100-rc.2.26513.2`. The repo `nuget.config` uses nuget.org and `../LocalNuGets` only. The whole solution restores |
| Build fails: the macios SDK expects Xcode 27.0, this Mac has 27.1 | `ValidateXcodeVersion=false`, set by the AppHost as an environment variable |

Debug Mac Catalyst data is in `~/Library/` (not sandboxed). The demo uses `~/Library/barista_notes_demo.db`. David's `barista_notes.db` was checked on 21 October: unchanged since 28 August. Backups: `~/Library/barista_notes.db.bak-pre-005-2026-10-20` and the container copy.

## Needs David's input

| # | Decision | Recommendation |
|---|---|---|
| 1 | Beat 8 (memory). Removing "You like natural-process coffees" works and is never relearned, but the next pick stays Guji Natural: the other preferences and the finished bag's own notes still point there | Show the Memory page and remove an item, then use **Never this roaster** on the pick for the visible change: the next run skips Copperhaze and picks Rwanda Natural |
| 2 | Beat 6b, the duplicate order with **Ignore idempotency keys**. It needs a **Reset demo** before beat 6 | Show the bug first, reset, then the fix. Or show only the fix and describe the bug |
| 3 | The dashboard shows the prompts and answers in Development (`EnableSensitiveData`). The data is fictional | Keep it: "see what the model saw" is a strong trust moment |
| 4 | Step delay is 900 ms. A first run takes about 20 s, a run with remembered preferences about 15 s | Keep, or set 600 ms for a faster stage |
| 5 | The Foundry key is still in the git-ignored `src/BaristaNotes/appsettings.Development.json`, for the iOS head. Mac Catalyst no longer bundles it | Remove it after the talk, or route iOS through the API too |
| 6 | From 20 October: rotate the internal feed PAT that was shown in tool output | Rotate |
| 7 | Optional: give the terminal Accessibility permission | Lets DevFlow and UI scripting reach the confirmation popups on Mac Catalyst |

## Decisions (David, 2026-10-20)

| Topic | Decision |
|---|---|
| Demo app | Barista Notes replaces DeliveryDesk |
| Top autonomy step | Reorder known favorites on its own, within a fixed monthly budget |
| Learned preferences | Settings → Memory page shows what the agent learned, with evidence, and lets you manage it |
| Humidity and grind advice | Not in this talk |
| Inventory | Do not infer stock from logged drinks. Many drinks are never logged. Ask David to confirm which bags he still has |
| Hosting | Aspire on this Mac. No Azure hosting |
| Monthly budget | €80 |
| Test-first exception | Approved: tests first for critical paths only (see Constitution check) |
| Stage head | Mac Catalyst, if it builds and runs. Otherwise the iOS simulator |

## Demo beats this plan must support

| # | Beat | Pattern |
|---|---|---|
| 1 | Check-in: "Still have these?" Mark Kochere finished | Human-in-the-loop: ask for what the app cannot observe |
| 2 | Run the agent from the dashboard or the app. The ledger fills step by step | Progress ledger |
| 3 | "While you were away": checked 5 roasters, skipped 9 with reasons, 1 pick | Progress ledger |
| 4 | "Why this one": evidence from your notes, what it does not know, budget left | Confidence signals |
| 5 | Buy: opens the prepared cart in the shop. You place the order. The ledger and budget update | Sandbox preview, human commit |
| 6 | Autonomy set to Reorder favorites. Arm "time out after order." Run. One order, not two | Autonomy gradient, retry |
| 7 | Stop mid-run. Ledger: the cart at roaster B is still open, with Remove | Stopped is not undone |
| 8 | Memory: remove "likes naturals." The next run reflects it | Inspectable memory |

## Architecture

```text
aspire run (AppHost)
├── api    ASP.NET Core minimal API: agent, ledger, settings, memory, AI forwarder   SQLite agent.db
├── shop   Blazor Web App: 5 fictional roasters, Shopify-shaped catalog, cart, checkout, orders
├── app    MAUI app on Mac Catalyst (Aspire.Hosting.Maui, preview), references api
└── secret ConnectionStrings:foundry in AppHost user secrets

app  → api   snapshot, check-ins, runs, ledger, proposals, settings, memory, /openai/* forwarder
api  → Foundry
api  → shop  catalog, carts, orders (Idempotency-Key)
shop → api   order webhook
```

The shop covers Blazor from the abstract. The Aspire dashboard is the demo control panel: custom resource commands for Run agent, Arm order timeout, Ignore idempotency keys, and Reset demo.

### Model work and code work

This table is TF-12, "AI proposes. Apps enforce permission."

| Step | Done by |
|---|---|
| Read catalogs. Filter: in stock, roasted 21 days ago or less, price, avoid list | Code |
| Learn preferences from ratings and free-text notes, with shot IDs as evidence | Model, structured output |
| Rank the candidates, explain the pick, cite shot IDs, list unknowns | Model, Agent Framework `ChatClientAgent` |
| Check that every cited ID exists. Drop invented evidence | Code |
| Autonomy level, monthly budget, one order per finished bag, idempotency | Code |

### Secrets

- **AppHost:** `builder.AddConnectionString("foundry")`. Value in user secrets: `ConnectionStrings:foundry = Endpoint=...;Key=...`.
- **api, agent:** an Azure OpenAI client from that connection string, wrapped with MEAI `UseOpenTelemetry()` so model calls show in the dashboard.
- **api, forwarder:** a YARP route `/openai/{**catch-all}` to the Foundry endpoint. The transform removes the incoming `api-key` and `Authorization` headers and adds the real key. It listens on localhost only.
- **app:** `AzureOpenAI:Endpoint` = the api URL. `ApiKey` = a placeholder. The five client sites stay as they are: `AIAdviceService`, `VisionService` (2), `VoiceCommandService`, `GrindTranslationAI`. Remove the key from `appsettings.Development.json`.
- **Limit:** the forwarder has no user sign-in. That is acceptable for a local demo only. Production needs sign-in and per-feature endpoints. Say so in the talk.
- Apple Intelligence on iOS does not change.
- This supersedes the unmerged `op-fix-dev-config-in-release` fix for AI keys.

### Data the app shares

`POST /snapshot` sends beans (name, roaster, origin, notes), bags (roast date, finished), and shots (rating 0-4, tasting notes, made for me or a guest, date). It does not send photos, people's names, equipment, or recipes.

- Guest drinks (`MadeForId` is not the user) are excluded from learning by default. This is the Nest lesson: an exception is not a preference.
- The Memory page shows when the last snapshot was sent and what it contained, with **Delete shared data**.

### Agent run

1. Load settings, memory, and the latest check-in. If no bag is confirmed finished, record "You still have 2 bags. Nothing to order" and end. Doing nothing, with the reason, is a ledger entry.
2. Get each watched roaster's catalog. Record "checked" for each one. A configurable step delay makes the run visible on stage.
3. Apply the code filters. Record each skip with its reason.
4. Ask the model to rank the candidates and give evidence. Validate the cited IDs.
5. Save the proposal, then apply the autonomy level:
   - **Watch:** notify only.
   - **Prepare cart:** create a cart in the shop. Record "Cart ready. Nothing charged."
   - **Reorder favorites:** place the order only if all of these are true: it is the same product as a finished bag rated 3 or higher, the price plus shipping fits the budget left this month, and that bag has no order yet. Otherwise prepare a cart and say why, for example "New roaster, so I prepared a cart instead."
6. Write a ledger entry before and after every external effect. A stop or crash then shows what already happened.

### Stop and retry

- **Stop:** `POST /runs/{id}/stop` cancels the run. The ledger lists effects that already happened, for example the open cart at roaster B, with **Remove cart**.
- **Retry:** Aspire service defaults add the standard resilience handler to every `HttpClient`. [By default it retries all HTTP methods, including POST.](https://learn.microsoft.com/dotnet/core/resilience/http-resilience#remove-standard-resilience-handlers) A timed-out order is therefore sent again with no retry code written. That is the TF-15 demonstration:
  1. With "Ignore idempotency keys" armed, the timeout produces 2 orders.
  2. With keys honored (`Idempotency-Key` = proposal ID), the shop returns the first order.

## Changes by project

| Project | New or changed | Work |
|---|---|---|
| `BaristaNotes.AppHost` | New, net10.0 | api, shop, MAUI app resource, connection string, dashboard commands, demo env vars |
| `BaristaNotes.ServiceDefaults` | New, from template | OpenTelemetry, resilience, health checks for api and shop |
| `BaristaNotes.MauiServiceDefaults` | New, from `maui-aspire-servicedefaults` | Service discovery and telemetry for the MAUI app |
| `BaristaNotes.Agent.Contracts` | New, net10.0 | DTO records shared by api and app. Records only, so the contract does not drift |
| `BaristaNotes.Api` | New, net10.0 | Endpoints below, agent, policy, EF Core SQLite `agent.db`, YARP forwarder, Agent Framework 1.25 |
| `BaristaNotes.RoasterShop` | New, net10.0, Blazor Web App | `/{roaster}/products.json`, `/{roaster}/cart/{variant}:{qty}`, checkout page, `POST /orders`, delete cart, orders page, fault toggles, webhook |
| `BaristaNotes` (MAUI) | Changed | Config, agent API client, new pages, demo database switch |
| `BaristaNotes.Api.Tests` | New, xUnit | Critical-path tests (see Constitution check) |
| Native iOS and Android heads | Not changed | Out of scope for this talk |

### API endpoints

```text
GET      /agent/status                 settings, budget spent and left, latest run, check-in, last snapshot
GET/PUT  /agent/settings               autonomy, monthly budget, watched roasters, step delay
GET      /roasters                     the shop's roasters, with Watched
POST     /snapshot   DELETE /snapshot
GET      /memory     DELETE /memory/{id}     removed items stay removed
POST     /checkins   GET /checkins/latest
POST     /runs       POST /runs/{id}/stop    POST /runs/stop (dashboard)
GET      /runs/latest  GET /runs/{id}  GET /runs/{id}/ledger?after={seq}
POST     /ledger/{entryId}/remove-cart
GET      /proposals?runId=  GET /proposals/{id}
POST     /proposals/{id}/cart  POST /proposals/{id}/dismiss  POST /proposals/{id}/never-roaster
POST     /webhooks/orders              from the shop, HMAC-SHA256 signed
POST     /demo/reset                   API and shop
GET/POST /demo/model-mode              Auto (live, replay as backup) or Only (rehearsal answers)
*        /openai/{**catch-all}         forwarder to Foundry, loopback only
```

### MAUI app screens

| Screen | Change |
|---|---|
| New Drink or Activity tab | "While you were away" card when the latest run has new entries |
| Ledger page, new | Run timeline: checked, skipped (expand for reasons), proposed, cart, ordered, stopped |
| Proposal page, new | Why this one, evidence chips that open the shot, unknowns, price and budget left. **Buy**, **Not now**, **Never this roaster** |
| Check-in sheet, new | Active bags with a finished switch. Sets `Bag.IsComplete`, which already exists |
| Settings → Agent, new section | Autonomy (Watch, Prepare cart, Reorder favorites), monthly budget, watched roasters, **Run now**, **Stop** |
| Settings → Memory, new page | Learned items with evidence, remove, what was shared, delete shared data |

There is no local schema change. `Bag.IsComplete`, `Bean.Roaster`, and `Bean.RoasterUrl` already exist. Agent state lives in the API's `agent.db`.

**Demo data:** when the AppHost sets `BARISTANOTES_DEMO=1`, the app uses a separate `barista_notes_demo.db`, seeded from a JSON fixture: fictional roasters, beans, bags, and about 25 shots with notes. David's real data is not touched. Check roaster names against real businesses before use.

The existing voice tool `open_roaster_url` does not change and is not part of the demo.

## Constitution check

| Principle | Status |
|---|---|
| I. Code quality | Pass. Analyzers on new projects, no warnings |
| II. Test-first | **Exception approved by David, 2026-10-20.** Tests first for critical paths only: budget, autonomy policy, one order per finished bag, idempotent order, evidence validation, removed memory stays removed, forwarder header handling. UI and full contract tests are deferred. Approved by David |
| III. UX consistency | Pass. MauiReactor, `MaterialSymbolsFont`, `IFeedbackService`, loading states, no emoji |
| V. Rating scale | Pass. 0-4 scale. A favorite is rated 3 or higher |
| VI. Architecture constraints | Pass. MauiReactor only |
| Schema management | Pass. No app schema change. `agent.db` uses its own EF migrations |
| Data preservation | Pass. Separate demo database |

## Schedule

| Day | Goal | Gate | Status |
|---|---|---|---|
| Tue 20 Oct, evening | Repo `nuget.config` and clean restore. AppHost, api, and shop skeletons. MAUI app launched from the AppHost on Mac Catalyst. AI through the forwarder | **1** | Done |
| Wed 21 Oct | Catalogs, filters, ledger, model pick with evidence, cart, checkout, webhook. App ledger and proposal pages | **2** | Done |
| Thu 22 Oct | Settings, auto-reorder, idempotency and fault toggles, Stop, check-in, Memory page. Policy tests | **3** | Done on 21 Oct |
| Fri 23 Oct | Cologne MAUI Day keynote. No build work planned | | |
| Sat 24 Oct | Demo data, step pacing, dashboard commands, polish. Record a full backup video. **Feature freeze** | **4** | Demo data, pacing, commands, replay, runbook done. Backup video left |
| Sun 25 Oct | Update outline TF-06 to TF-15 and the deck for this demo. Rehearse | | |
| Mon 26 Oct | Code freeze. Full run on the presentation machine, including a run on phone hotspot | **5** | |

## Cut line

- **Must:** beats 1-7, forwarder, demo database, backup video.
- **Should:** beat 8 affecting the next run, one discovery suggestion (a new roaster, suggestion only), dashboard commands (otherwise admin buttons on the shop page).
- **Not for this talk:** iOS simulator or device, native heads, push notifications, keyless Entra auth, CoreSync, real roasters, live web search.

## Risks

| Risk | Mitigation |
|---|---|
| NuGet: the user config replaces nuget.org with a stale proxy, and the internal dnceng feed returns 401 | Resolved for this repo: `nuget.config` uses nuget.org and `../LocalNuGets` (DevFlow, HotReload) only |
| `Aspire.Hosting.Maui` is preview. It may not work with .NET 11 RC2 on Mac Catalyst | Gate 1 tests it first. Fallback: run the app separately with a fixed api port in `appsettings.Development.json` |
| Conference Wi-Fi. Foundry needs internet | Phone hotspot. Optional replay client (MEAI `DelegatingChatClient`) that serves rehearsal responses. If used, say so on stage. The backup video is the last resort |
| Model variance changes the pick on stage | Demo data with one clear best match. Low temperature. The replay client |
| Mac Catalyst layout looks like a stretched phone app | Check at Gate 1. Use a fixed phone-sized window |
| Fictional names match real businesses | Check before seeding |

## Findings log

### Fixed on 21 October

| Found | Fix |
|---|---|
| The model cited a drink rated 2 ("Sour") as evidence for "You prefer light roasts" | Code check: a like cites drinks rated 3-4, a dislike 0-2 |
| The model re-picked Nyeri Washed (rated 2.3) against its instructions | Code skips a finished coffee rated below 3: "You rated it 2.3 of 4. Looking for something you may like more." |
| The model said "You have not tried this roaster" for roasters you had | The prompt lists what you had from each roaster |
| Replay missed: memory items reached the prompt in a different order each run | Stable order by key, and by date then ID for drinks |
| Demo reset continued the ids (SQLite AUTOINCREMENT), so recordings would not match | Reset clears `sqlite_sequence` for the demo tables |
| Skipped steps grouped at the first skip broke the timeline | Grouped at the latest skip |
| The pick page showed the evidence below the fold | Why and evidence first; price and budget side by side |

### Open

| Severity | Finding |
|---|---|
| Low | Two runs for the same finished bag can leave two open carts. The agent never orders twice, but you could buy both by hand |
| Low | Cart creation is a POST, so the resilience handler retries it too. On a timeout the shop could get a second cart. Nothing is charged |
| Runbook | Restarting the AppHost resets the shop (in memory) but not the API. Order numbers restart at #1001. Always **Reset demo** after a restart |
| Dev loop | Restarting the `app-maccatalyst` resource does not quit a running app, so a new build is not launched. Quit the app first (Aspire.Hosting.Maui preview) |
| Tooling | DevFlow on Mac Catalyst cannot see or tap the UXDivers confirmation popups. `ui alert` auto-detected a booted iOS simulator instead of the Catalyst app and reported its "NEW PROFILE" dialog; pass `--pid` |
| Tooling | On the iOS simulator the popup buttons appear in `ui alert tree`, but `ui drag` and Ailoha `ui swipe` both return `success: false` with no detail, so the popup still cannot be tapped |
| Tooling | `screencapture -l` and AppleScript UI scripting fail from this shell (no Screen Recording or Accessibility permission). DevFlow screenshots work |
| Cosmetic | The DEBUG AI tool harness expects an empty database. With demo data it logs "LLM FAIL ... You have pulled 22 shots". Harmless |
| Cosmetic | The model's "why" sometimes stretches a preference, for example "light roast Ethiopian" for the Rwanda Natural |
| Docs | The `maui-aspire` skill in maui-skills still says MAUI cannot be orchestrated by Aspire. `Aspire.Hosting.Maui` does it |
