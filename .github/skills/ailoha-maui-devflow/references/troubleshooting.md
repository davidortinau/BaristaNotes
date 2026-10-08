# MAUI troubleshooting

## `ailoha agent wait` times out

Check these in order:

1. The app is running in a Debug build.
2. `Ailoha.Agent.Maui` is referenced by the app project.
3. `AddMauiDevFlowAgent(...)` is present in `MauiProgram.cs`.
4. The registration is inside `#if DEBUG`, but the current build actually defines
   `DEBUG`.
5. `ailoha diagnose` does not show a broker or resolution problem.

Do not jump straight to port overrides unless the first four checks are already
clean.

## `agent status` resolves the wrong target

This usually means more than one compatible app is running, or the project has a
persisted selection/config override.

Use:

```bash
ailoha agent list
ailoha agent status
```

If discovery is correct but the selected app is not the one you want, use the
appropriate project selection flow before retrying other commands.

## `ui query` returns nothing

Common causes:

- the control does not have an `AutomationId`
- the `AutomationId` value changed
- the current page is not the expected one
- the UI changed after navigation and you are using a stale element id

Best recovery:

```bash
ailoha ui tree --depth 3
```

Then re-query using the actual `AutomationId` or text now visible on screen.

## Blazor commands do not help

Check whether the app actually uses `BlazorWebView` and whether
`AddMauiBlazorDevFlowTools()` is registered. If not, treat the problem as a
native MAUI issue and stay on `ui`, `logs`, or `network` commands.

## You are tempted to hardcode `--agent-port 9233`

Pause and verify whether you actually need it. The normal Ailoha flow is:

`explicit args -> env/config override -> broker discovery -> localhost:9233`

If broker discovery is working, forcing `9233` can point you at the wrong app or
wrong fallback path.
