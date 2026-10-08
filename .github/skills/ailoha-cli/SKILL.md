# Ailoha CLI — Agent Skill File

> **What:** CLI + MCP server for inspecting and interacting with running mobile/desktop apps.
> **When to use:** UI automation, visual testing, app diagnostics, screenshot capture, network inspection.
> **Install:** see [docs/install.md](../../../docs/install.md) — `curl -fsSL https://ailoha.dev/install.sh | bash` on Unix, `iex "& { $(irm https://ailoha.dev/install.ps1) }"` on Windows. Then `ailoha update --self` to upgrade.
> **MCP mode:** `ailoha mcp-serve` (stdio transport — wire into any MCP-compatible AI tool)

## Global Options

Every command accepts:

| Flag | Purpose |
|------|---------|
| `--agent-port <port>` | Connect to a specific agent port |
| `--agent-host <host>` | Override the agent host (defaults to `127.0.0.1`) |
| `--platform <platform>` | Select a platform context; platform aliases like `ailoha maui ...`, `ailoha android ...`, `ailoha rn ...`, `ailoha expo ...`, and `ailoha flutter ...` set this automatically |
| `--json` | Machine-readable JSON output |
| `--no-json` | Force human-readable output |

If you omit `--agent-host` / `--agent-port`, resolution is:
**explicit args** → **`AILOHA_AGENT_HOST` / `AILOHA_AGENT_PORT` or local `.devflow`** → **broker discovery** → **`127.0.0.1:9233`**.
Ailoha's default direct agent port is `9233`; its broker listens on `19323`.

## Onboarding & Skill Management

Use `ailoha init` before editing a target app with an AI agent. It installs the
version-matched Ailoha CLI skill plus the platform skill for detected MAUI,
Android, WinUI, WPF, Flutter, React Native, or Expo projects.

```sh
ailoha init                              # Detect app platforms and install matching bundled skills
ailoha init --platforms maui,android,flutter # Explicit non-interactive platform selection
ailoha init --platforms android          # Install native Android onboarding guidance
ailoha init --target github              # Install into .github/skills instead of auto target
ailoha init --scope user                 # Install into user-level skill folder
```

If no supported project is detected, interactive runs prompt for the expected
platforms. Non-interactive or JSON runs should pass `--platforms`.

```sh
ailoha skills list                       # Show skill install status
ailoha skills check                      # Compare installed files with this CLI bundle
ailoha skills update                     # Reconcile installed bundled skills
ailoha skills doctor                     # State, detection, and CLI drift diagnostics
ailoha skills remove ailoha-maui-devflow # Remove a managed skill
```

Skill targets are `auto`, `claude` (`.claude/skills`), `github`
(`.github/skills`), `agent` (`.agent/skills`), `agents` (`.agents/skills`), or a
custom relative `--path`.

## Connection

```sh
ailoha agent list                          # Show connected agents
ailoha agent status                        # Check the resolved/default agent
ailoha agent wait --timeout 30             # Block until an agent connects
ailoha agent wait --project path/to/MyApp.csproj  # Wait for a specific project
ailoha agent select <id>                   # Persist a default agent for this project
```

MCP tools: `agent_list`, `agent_status`, `agent_wait`, `agent_select`

## UI Inspection

```sh
ailoha ui tree                             # Full visual tree
ailoha ui tree --depth 3                   # Limit depth
ailoha ui tree --filter "Button"           # Filter by type
ailoha ui query --automationId loginBtn    # Find by automation ID
ailoha ui query --text "Submit"            # Find by visible text
ailoha ui query --selector "Button.primary" # CSS-like selector
ailoha ui element <id>                     # Details for one element
ailoha ui hittest 120 300                  # Element at screen coordinates
```

MCP tools: `ui_tree`, `ui_query`, `ui_element`, `ui_hittest`

## UI Actions

```sh
ailoha ui tap <id>                         # Tap / click element
ailoha ui fill <id> "hello@example.com"    # Type into a text field (clears first)
ailoha ui fill <id> "more text" --append   # Append text without clearing
ailoha ui clear <id>                       # Clear text field
ailoha ui scroll --dy -200                 # Scroll down (auto-finds scrollable)
ailoha ui scroll --element <id> --dy -200  # Scroll within specific element
ailoha ui scroll --item-index 5            # Scroll to item index
ailoha ui focus <id>                       # Focus element
ailoha ui navigate "/settings"             # Navigate to app route
ailoha ui navigate --back                  # Go back (pop stack / dismiss modal)
ailoha ui navigate --reset "Home"          # Reset navigation stack to route
ailoha ui swipe 200 700 200 200            # Swipe gesture (startX startY endX endY)
ailoha ui resize 375 812                   # Resize window (w h)
```

MCP tools: `ui_tap`, `ui_fill`, `ui_clear`, `ui_scroll`, `ui_focus`, `ui_navigate`, `ui_swipe`, `ui_resize`

## Properties

```sh
ailoha ui prop get <id> IsEnabled          # Read a property
ailoha ui prop set <id> Text "new value"   # Write a property
ailoha ui assert --id <id> IsVisible true
```

MCP tools: `ui_prop_get`, `ui_prop_set`, `ui_assert`

## Screenshots

```sh
ailoha ui screenshot                       # Full window screenshot
ailoha ui screenshot --element-id <id>     # Single element
ailoha ui screenshot --output shot.png     # Save to file
```

MCP tool: `ui_screenshot`

## WebView

```sh
ailoha webview list                        # List WebView contexts
ailoha webview Runtime evaluate "document.title"  # Run JS
ailoha webview DOM querySelector "h1"      # Query DOM
ailoha webview source                      # Get page HTML
ailoha webview screenshot                  # WebView screenshot
```

MCP tools: `webview_list`, `webview_eval`, `webview_query`, `webview_source`, `webview_screenshot`

## Device Info

```sh
ailoha device info                         # OS, model, runtime
ailoha device app                          # App name, version, ID
ailoha device display                      # Screen size, density
ailoha device battery                      # Battery level, charging
ailoha device connectivity                 # Network type, status
ailoha device geolocation                  # GPS coordinates
ailoha device sensor list                  # Available sensors
ailoha device sensor start accelerometer   # Start streaming
ailoha device sensor stop accelerometer    # Stop streaming
```

MCP tools: `device_info`, `device_app`, `device_display`, `device_battery`, `device_connectivity`, `device_geolocation`, `device_sensor_list`, `device_sensor_start`, `device_sensor_stop`

## Storage

```sh
ailoha storage pref list                   # All preferences
ailoha storage pref get theme              # Read one preference
ailoha storage pref set theme dark         # Write preference
ailoha storage pref delete theme           # Delete preference
ailoha storage pref clear                  # Clear all preferences
ailoha storage secure get api_token        # Read encrypted value
ailoha storage secure set api_token "xyz"  # Write encrypted value
ailoha storage secure delete api_token     # Delete encrypted value
ailoha storage secure clear                # Clear secure storage
```

MCP tools: `storage_pref_*`, `storage_secure_*`

## Network Traffic

```sh
ailoha network list                        # Recent HTTP requests
ailoha network list --limit 50 --host api.example.com --method POST
ailoha network detail <id>                 # Headers, body, timing
ailoha network clear                       # Clear captured requests
```

MCP tools: `network_list`, `network_detail`, `network_clear`

## Logs & Recording

```sh
ailoha logs                                # App log stream
ailoha logs --limit 100 --minLevel Warning # Filter logs
ailoha recording start                     # Start screen recording
ailoha recording stop                      # Stop and save
ailoha recording status                    # Is recording active?
```

MCP tools: `logs_get`, `recording_start`, `recording_stop`, `recording_status`

## Broker & Diagnostics

```sh
ailoha broker start                        # Start local broker
ailoha broker stop                         # Stop broker
ailoha broker status                       # Broker health
ailoha diagnose                            # Full health check
```

## Batch Mode

Pipe JSONL commands via stdin for scripted automation:

```sh
printf 'ui tree --depth 3\nui screenshot --output shot.png\n' | ailoha batch
```

## Common Workflows

**Wait for app, then inspect UI:**
```sh
ailoha agent wait --timeout 60 && ailoha ui tree --depth 4
```

**Tap a button by automation ID:**
```sh
ID=$(ailoha ui query --automationId submitBtn --json | jq -r '.[0].id')
ailoha ui tap "$ID"
```

**Screenshot after navigation:**
```sh
ailoha ui navigate "/profile" && sleep 1 && ailoha ui screenshot --output profile.png
```
