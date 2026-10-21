# UI automation: DevFlow and Ailoha

> [!summary]
> Use **DevFlow** for the MAUI app (`src/BaristaNotes`) on every target: Mac Catalyst, the iOS simulator, Android. MauiReactor renders real MAUI controls, so DevFlow sees and drives them. Use **Ailoha** only for the native heads (`BaristaNotes.iOS` UIKit and `BaristaNotes.Android`), which embed Ailoha's native agents. Run DevFlow from this repo as `dotnet maui devflow ...`, not the global `maui`.

## Which tool for which app

| App | Agent inside the app | Drive it with | Port |
|---|---|---|---|
| `src/BaristaNotes` (MAUI, MauiReactor), any target | `Microsoft.Maui.DevFlow.Agent` (Debug only) | `dotnet maui devflow ...` | Assigned by the DevFlow broker (often 10224 or 9223). `dotnet maui devflow diagnose` lists it |
| `src/BaristaNotes.iOS` (native UIKit) | Ailoha Swift agent 0.1.13 (`AilohaAgent.xcframework`) | `ailoha --agent-port 9243 ...` | 9243, fixed |
| `src/BaristaNotes.Android` (native Views) | Ailoha Android agent 0.1.13 (`.aar`) | `ailoha --agent-port 9233 ...` (forward the port with adb) | 9233 |

Why not one tool: an agent lives inside the app and reports that app's UI tree. DevFlow's agent understands MAUI elements (pages, layouts, handlers, Shell, AutomationId). The native heads have no MAUI, so they need a native agent. DevFlow is adding an agent for plain .NET native apps (`Microsoft.Maui.DevFlow.Agent.Native`, dotnet/maui-labs#435, open), which could later cover the native heads too.

## Versions: the CLI and the agent must match

| Piece | Where it is pinned | Now |
|---|---|---|
| DevFlow CLI | `dotnet-tools.json` (local tool, run as `dotnet maui`) | `0.1.0-dev.20261021.2` |
| DevFlow agent | `MauiDevFlowVersion` in `src/BaristaNotes/BaristaNotes.csproj` | `0.1.0-dev.20261021.2` |

`0.1.0-dev.20261021.2` is a local build from maui-labs branch `davidortinau/devflow-cli-lease-and-overlay-popups` (worktree `~/work/maui-labs-wt/devflow-fixes`), copied to `~/work/LocalNuGets`. It is the published `0.1.0-preview.13.26512.5` plus two fixes:

1. Back-to-back CLI commands no longer refuse each other for 10 s ("Another session is driving this app").
2. Popups that a library attaches to the native window (UXDivers.Popups here) are visible to tree, query, and tap.

The fixes are in review as dotnet/maui-labs#726 (lease) and dotnet/maui-labs#727 (popups). When they ship in a published preview, set both pins to that version.

## Everyday commands

```bash
cd ~/work/BaristaNotes
dotnet maui devflow diagnose                       # broker, agents, versions, ports
dotnet maui devflow ui navigate "//settings" --agent-port 10224
dotnet maui devflow ui tap --automationId reset-demo-data --agent-port 10224
dotnet maui devflow ui query --text "Reset" --type Button --agent-port 10224
dotnet maui devflow ui tap --text "Reset" --type Button --agent-port 10224   # the popup's button
dotnet maui devflow ui screenshot --output shot.png --overwrite --agent-port 10224
dotnet maui devflow logs --limit 100 --agent-port 10224
```

- Pass `--agent-port` whenever more than one app is connected; the CLI refuses to guess.
- The CLI prints a `→ target:` line before JSON. Send stderr to `/dev/null` when you parse the JSON.
- Agent tiles have automation ids from their accessible names, for example `kochere-natural-finished`, `run-the-agent-now`, `buy-opens-the-prepared-cart-in-your-browser`.
- When the same page is pushed more than once, the lower copies keep their elements. Tap the last match (`--index`).

## Gotchas on this Mac

| Gotcha | What to do |
|---|---|
| The global `maui` (`~/.dotnet/tools`) is a local build of the maui-labs Comet branch `davidortinau/comet-next`. Its `ui drag` verb only works with Comet's agent | In this repo always use `dotnet maui` |
| One DevFlow broker serves all apps for the user (port 19223, `~/.mauidevflow/broker.json`). Whichever CLI starts it owns it | After switching CLI versions: `dotnet maui devflow broker stop`, then `broker start` |
| `ui alert` handles OS-level dialogs (permission prompts, UIAlertController). On Mac Catalyst it reads the macOS accessibility tree and needs `--pid` | Pass `--pid $(pgrep -f "maccatalyst-arm64/BaristaNotes.app/Contents/MacOS/BaristaNotes")`. In-app popups (UXDivers) are MAUI pages: use `ui query` and `ui tap` |
| Restarting the Aspire `app-maccatalyst` resource does not quit a running app, so a new build is not launched | Quit the app first |
| `ailoha` (`~/.ailoha/bin`, 0.1.13) can also talk to a DevFlow agent (same protocol), but it is not version-matched to this agent | Use it for the native heads only |
