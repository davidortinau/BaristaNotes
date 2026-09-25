---
name: maui-devflow-debug
description: >-
  Run build, deploy, inspect, and fix loops for .NET MAUI apps that already have
  MAUI DevFlow integrated. USE FOR: launching MAUI apps, selecting devices or
  emulators, waiting for or recovering agent connections, broker/port/adb
  connectivity issues, visual tree inspection, screenshots, UI interaction,
  Blazor WebView CDP debugging, reading DevFlow logs, and iterative app
  debugging. DO NOT USE FOR: first-time DevFlow package setup (use
  maui-devflow-onboard), or generic desktop automation unrelated to MAUI. INVOKES:
  maui devflow CLI, dotnet CLI, Android adb/android tools, and Apple simctl
  tools.
---

# DevFlow Debug

Use this skill for the active debugging loop after a MAUI app has DevFlow
packages and `builder.AddMauiDevFlowAgent()` registered.

## When to Use

- Build and run a MAUI app on Android, iOS, Mac Catalyst, macOS, Windows, or GTK.
- Choose or create a simulator/emulator for a project.
- Wait for a DevFlow agent, inspect the visual tree, tap/fill UI, or capture screenshots.
- Recover from DevFlow connection failures after the app is integrated, including broker, port, and Android adb forwarding issues.
- Debug Blazor Hybrid content through DevFlow WebView/CDP commands.
- Read app logs, network captures, preferences, device info, or recordings through DevFlow.
- Iterate on an app bug with a build -> deploy -> inspect -> fix loop.

## Route Elsewhere

- If DevFlow packages or `MauiProgram.cs` registration are missing, use `maui-devflow-onboard`.
- If the failure is a generic build or SDK issue with no DevFlow angle, use normal .NET/MAUI diagnostics.

## Core Loop

1. Confirm the app is already integrated:

   ```bash
   grep -rl "Microsoft.Maui.DevFlow" --include="*.csproj" .
   ```

   If no project has DevFlow package references, stop and switch to `maui-devflow-onboard`.

2. Pick the target framework and launch target. Do not assume `net10.0`; inspect the project first.

   ```bash
   grep -i "TargetFramework" *.csproj Directory.Build.props 2>/dev/null
   ```

3. Select the target before launch.

   - Honor a device or simulator named by the user.
   - Check `maui devflow broker status` and the platform device list before starting another simulator or emulator.
   - Prefer an already connected target that has the required app state. If several targets match, stop and ask which one to use.
   - Use Apple and Android platform tools only for target management, deployment, and launch. Use DevFlow for app UI inspection and interaction.

4. Launch the app and keep the launch process alive when required.

   - iOS, Android, and Mac Catalyst `dotnet build -t:Run` usually block for the app lifetime.
   - GTK `dotnet run` blocks for the app lifetime.
   - macOS AppKit builds can exit after compiling; launch the `.app` separately.

5. Wait for the DevFlow agent before inspecting UI:

   ```bash
   maui devflow wait
   maui devflow ui tree --depth 3 --fields "id,type,text,automationId"
   ```

   If `wait`, `list`, or `ui tree` cannot connect after the app is running, load `references/connectivity.md` and recover the broker/agent connection before continuing.

6. Verify the changed behavior before editing again or reporting success.

   1. List every changed state and transition, including loading/content, show/hide, open/close, success, error, and cancellation paths.
   2. Capture the initial full-screen state and inspect the relevant visual-tree nodes.
   3. Use DevFlow interaction to exercise each transition. Capture or inspect both sides of every transition.
   4. Check the top and bottom edges, fixed controls, sheet or overlay bounds, focus, and internal scrolling after layout changes.
   5. For a blocking overlay, attempt an underlying action while the overlay is visible and confirm that the action does not occur. A screenshot cannot prove input blocking.
   6. Confirm that dismissed or completed UI is removed and that normal interaction resumes.

   Repeat from launch after each code change.

## Critical Anti-patterns

- Do not treat an empty `maui devflow list` as proof the project is not integrated. `list` is runtime state; project files are source of truth.
- Do not use arbitrary sleeps after launch. Use `maui devflow wait` to gate on the actual agent connection.
- Do not kill an async `dotnet build -t:Run` or `dotnet run` shell while you still need the app; that often kills the app.
- Do not reuse a busy simulator/emulator when multiple MAUI apps or agents may be running.
- Do not substitute a simulator when the user named a physical device.
- Do not use AppleScript, coordinate shell tools, `adb shell`, or other desktop automation to inspect or operate app UI when DevFlow can perform the action.
- Do not debug Blazor WebView DOM issues through the native visual tree alone; use the WebView/CDP commands.
- Do not treat `ui status` / `ui tree` / `screenshot` failures as connectivity issues without first running `curl http://localhost:<port>/api/status`. The CLI's "Cannot connect" error masks JSON serialization errors and CLI↔agent route-version skew. See `references/connectivity.md` for the 8-rung troubleshooting ladder.
- Do not declare a DevFlow CLI or agent bug "blocked." The source lives at `~/work/maui-labs` (origin `dotnet/maui-labs`). Reproduce against source, fix locally (`dotnet pack` into `~/work/LocalNuGets/`), and open a draft PR upstream before falling back to manual smoke tests.

## Stop Signals

- Stop and switch to `maui-devflow-onboard` when package references or `AddMauiDevFlowAgent()` are absent.
- Stop and ask which project, device, or agent to target when multiple candidates match.
- Stop rebuilding after two identical failures until you inspect the first meaningful build/runtime error.
- Stop using screenshots for exact property values; query the visual tree or properties instead.
- Stop before reporting success when any changed state or transition has not been exercised.

## Reference Map

Load these only when needed:

- `references/setup.md` - detailed integration, package, entitlement, and update notes.
- `references/connectivity.md` - broker, agent, port, Android forwarding, and "no agents connected" recovery.
- `references/android.md` - Android SDK, emulator, adb, build, deploy, and port forwarding details.
- `references/ios-and-mac.md` - iOS simulator, Mac Catalyst, permissions, entitlements, and Apple tooling.
- `references/macos.md` - macOS AppKit project shape, launch model, and troubleshooting.
- `references/linux.md` - GTK/Linux launch, packages, and WebKitGTK notes.
- `references/batch.md` - batching multiple DevFlow UI/WebView operations.
- `references/troubleshooting.md` - build, connection, CDP, and platform-specific failure recovery.
