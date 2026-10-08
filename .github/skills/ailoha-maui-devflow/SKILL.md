---
name: ailoha-maui-devflow
description: >
  End-to-end workflow for onboarding, connecting, inspecting, and debugging
  Ailoha in .NET MAUI and MAUI Blazor Hybrid apps. USE FOR: setting up Ailoha
  in a MAUI app, adding Ailoha.Agent.Maui or Ailoha.Blazor packages, wiring
  AddMauiDevFlowAgent, connecting the ailoha CLI to a running MAUI app,
  debugging broker or agent discovery issues, inspecting MAUI visual trees, and
  troubleshooting BlazorWebView behavior. DO NOT USE FOR: implementing a new
  Ailoha platform agent (use ailoha-platform-agent), generic .NET build tuning,
  or non-MAUI desktop automation. INVOKES: repo docs, Ailoha CLI commands, and
  platform build tools.
license: LicenseRef-Ailoha-Binary-Distribution
---

# Ailoha MAUI DevFlow

Use this skill to get a MAUI app from "not integrated" to "connected and
inspectable" with the Ailoha CLI. Keep the loop broker-first, debug-only, and
focused on stable identifiers.

## When to Use This Skill

Use this skill when:

- adding Ailoha to a new or existing MAUI app
- wiring `AddMauiDevFlowAgent(...)` in `MauiProgram.cs`
- enabling Ailoha for a Blazor Hybrid app with `AddMauiBlazorDevFlowTools()`
- figuring out why `ailoha agent status` or `ailoha agent wait` cannot see the app
- inspecting MAUI controls through `AutomationId`
- deciding whether to use tree/query, logs/network, screenshots, or WebView tools

## When to Stop

- The project has the right packages, the registration is inside `#if DEBUG`, and
  one of `ailoha diagnose`, `ailoha agent wait`, or `ailoha agent status`
  confirms connectivity.
- A focused `ui query`, `logs`, `network`, or `webview` command explains the
  issue without needing a larger dump.
- One screenshot confirms the visual hypothesis. Do not keep capturing nearly
  identical frames.

## Workflow

### 1. Confirm project integration from source, not runtime symptoms

Check project files for the real integration points:

- `Ailoha.Agent.Maui`
- `Ailoha.Blazor` for Blazor Hybrid
- `AddMauiDevFlowAgent(...)`
- `AddMauiBlazorDevFlowTools()`

An empty `ailoha agent list` is only runtime state. It does **not** prove the
project is missing Ailoha.

> ❌ Do not equate "no connected agent" with "Ailoha is not installed."

### 2. Onboard correctly when integration is missing

Follow [references/setup.md](references/setup.md):

- add the MAUI package
- add the Blazor package only if the app uses `BlazorWebView`
- keep all registration inside `#if DEBUG`
- prefer stable `AutomationId` values on important controls

### 3. Connect with the broker-first CLI flow

Prefer the cheapest connection loop:

```bash
ailoha diagnose
ailoha agent wait --timeout 60
ailoha agent status
```

Only override host or port when the environment/config requires it or when you
are proving a fallback path.

> ❌ Do not assume `localhost:9233` is the active port just because it is the
> fallback. Ailoha resolves explicit args, then env/config, then broker
> discovery, then `localhost:9233`.

### 4. Inspect cheap before expensive

For most MAUI issues:

1. Query by `AutomationId` first.
2. Use `ui tree --depth 3` or `--depth 4` before a full tree dump.
3. Use `ui element <id>` when you need one element's details.
4. Use logs, network, or webview commands before screenshots if the issue is not
   primarily visual.

### 5. Escalate by symptom

| Symptom | Best next move |
|---|---|
| Agent not found | `diagnose` -> `agent wait` -> re-check source integration |
| Wrong element or missing control | `ui query --automationId ...` -> shallow `ui tree` |
| Visual/layout issue | one screenshot after you have the right element |
| State or behavior issue | `ailoha logs` or `ailoha network list` |
| Blazor page issue | `ailoha webview list` then `Runtime evaluate` |

### 6. Keep MAUI-specific anti-patterns in mind

- ❌ Do not wire the agent in Release builds.
- ❌ Do not start with screenshots when `AutomationId`-based queries can answer
  the question faster and more cheaply.
- ❌ Do not skip `AddMauiBlazorDevFlowTools()` when the app uses
  `BlazorWebView`.
- ❌ Do not rely on ephemeral element ids after navigation; re-query when the UI
  changed.

## References

- **Onboarding and integration**:
  [references/setup.md](references/setup.md)
- **Command selection and workflows**:
  [references/cli-workflows.md](references/cli-workflows.md)
- **Troubleshooting**:
  [references/troubleshooting.md](references/troubleshooting.md)
- **Token-aware debugging strategy**:
  [references/debugging-strategies.md](references/debugging-strategies.md)
