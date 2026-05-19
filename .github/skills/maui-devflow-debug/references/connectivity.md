# Connectivity Recovery

Use this when a project already has DevFlow package references and `builder.AddMauiDevFlowAgent()` registered, but the `maui` CLI cannot reach a running app.

## Quick workflow

1. Run the built-in diagnostic:

   ```bash
   maui devflow diagnose
   ```

   Use the result to separate broker startup, missing project integration, no running app, and target-device networking issues.

2. Verify integration before chasing ports:

   ```bash
   grep -R --include="*.csproj" "Microsoft.Maui.DevFlow.Agent" .
   grep -R "AddMauiDevFlowAgent" .
   ```

   If package references or `AddMauiDevFlowAgent()` are missing, stop and switch to `maui-devflow-onboard`.

3. Check broker health:

   ```bash
   maui devflow broker status
   maui devflow broker start
   ```

4. List and wait for agents:

   ```bash
   maui devflow list
   maui devflow wait
   maui devflow ui tree --depth 1
   ```

   A successful `ui tree` means connectivity is fixed; continue the main debug loop.

## Platform notes

### Android emulator

Android emulators run in a separate network namespace. Broker registration and CLI-to-agent traffic need opposite forwarding directions:

```bash
maui devflow list                 # note the assigned agent port
adb reverse tcp:19223 tcp:19223   # app in emulator -> host broker
adb forward tcp:<port> tcp:<port> # host CLI -> app agent
adb reverse --list
adb forward --list
```

If no broker is available and the app uses a direct `.mauidevflow` port, forward that port instead:

```bash
adb forward tcp:9223 tcp:9223
```

### iOS simulator

No forwarding is normally needed because simulators share host networking:

```bash
xcrun simctl list devices booted
```

### Mac Catalyst and macOS

Mac Catalyst needs Debug entitlements that allow the agent and CDP servers to bind ports:

```xml
<key>com.apple.security.network.server</key>
<true/>
```

macOS AppKit and Mac Catalyst otherwise use direct localhost access. Check for explicit port conflicts only after confirming broker discovery:

```bash
lsof -i :9223
```

### Windows and Linux/GTK

These use direct localhost access. On Linux, verify the app process started:

```bash
pgrep -f "YourApp"
```

## Common symptoms

| Symptom | Fix |
|---------|-----|
| `maui devflow list` shows no agents | Verify app is running in Debug, package references exist, and `AddMauiDevFlowAgent()` executes |
| Android agent never registers | Run `adb reverse tcp:19223 tcp:19223` for broker registration |
| Android connection refused after registration | Run `adb forward tcp:<port> tcp:<port>` using the port from `maui devflow list` |
| Broker unavailable | `maui devflow broker start`; retry the command |
| Port already in use | Identify the owner with `lsof -i :<port>` before killing a PID |
| Multiple agents connected | Ask which app/device/agent port to target; use `--agent-port` |

## Stop signals

- Stop and switch to `maui-devflow-onboard` if package references or `AddMauiDevFlowAgent()` are missing.
- Stop connectivity recovery once `maui devflow wait` succeeds and `maui devflow ui tree --depth 1` returns a tree.
- Stop and ask which target to use if multiple connected agents or devices match the app.
- Stop after confirming a generic build/deploy failure; the app must launch before broker connectivity can work.

## Anti-patterns

- Do not treat an empty `maui devflow list` as proof the project is not integrated. It only means no runtime agent is connected.
- Do not use `adb reverse` for the agent HTTP port when the host CLI must connect into the emulator. Use `adb forward tcp:<port> tcp:<port>` for that direction.
- Do not kill random processes by name. Identify the owning PID first.

## The 8-rung smoke-test troubleshooting ladder

When `maui devflow ui status` / `ui tree` / `screenshot` fails, climb this ladder in order. **Do not abandon UI smoke testing until you reach the bottom rung.** The most common false-failure (a healthy agent + a buggy CLI) is exposed at Rung 3.

### Rung 1 — Is the app actually running?

```bash
ps -ef | grep "<AppName>.app/Contents/MacOS\|<AppName>\.exe\|<package>" | grep -v grep
lsof -i :<port>   # is the agent listening?
```

If no process: rebuild and relaunch. Don't go further.

### Rung 2 — Is the broker seeing the agent?

```bash
maui devflow list
maui devflow broker status
maui devflow broker start   # if status was unhealthy
```

### Rung 3 — Is the agent itself responding? (bypass the CLI)

The agent exposes an HTTP API. Curl it directly:

```bash
curl -s -m 3 http://localhost:<port>/api/status | head -30
```

- **Returns agent JSON** (version, app info, window dims): the agent is HEALTHY. The CLI is the broken layer. Skip to Rung 5.
- **Returns `{"error":"Route not found"}`**: agent is serving but the route schema differs from what the CLI expects. Try `/api/status`, `/devflow/v1/status`, `/status` to find the schema version.
- **Times out or refuses**: real connectivity issue. Go to Rung 4.

### Rung 4 — Connectivity recovery (platform-specific)

- **Android emulator:** `adb reverse tcp:19223 tcp:19223` + `adb forward tcp:<port> tcp:<port>`.
- **iOS simulator:** shares host network — no forwarding; check `xcrun simctl list devices booted`.
- **Mac Catalyst:** verify Debug entitlement `com.apple.security.network.server=true` and that the bound port isn't owned by a stale process (`lsof -i :<port>` → `kill <PID>`).
- **Windows / Linux GTK:** localhost direct; check firewall.

### Rung 5 — CLI / Agent version skew (most common false-failure)

If Rung 3 showed the agent responding to curl but the CLI's `ui status` reports "Cannot connect":

```bash
maui --version
curl -s http://localhost:<port>/api/status | grep -o '"version":"[^"]*"'
```

Align them:

1. **Preferred:** bump the agent NuGet. Check `~/work/LocalNuGets/` for available `Microsoft.Maui.DevFlow.Agent.<ver>.nupkg`, pick the highest, update `<PackageReference>` in the MAUI project's csproj, `dotnet restore`, rebuild, relaunch.
2. **Fallback:** roll the CLI to match the installed agent: `dotnet tool update -g maui --version <matching-ver>`.

### Rung 6 — CLI fallback to direct HTTP

If alignment isn't immediately possible, the agent's `/api/*` surface is enough to verify the smoke test:

```bash
curl -s http://localhost:<port>/api/status | jq .
for route in /api/ui/tree /api/visualtree /devflow/v1/ui/tree; do
  echo "=== $route ==="
  curl -s -m 3 "http://localhost:<port>$route?depth=3" | head -20
done
curl -s "http://localhost:<port>/api/screenshot" -o /tmp/devflow.png
```

Use whatever route returns real data.

### Rung 7 — Fix the tool itself (we own the source)

DevFlow source lives at `~/work/maui-labs` (origin `dotnet/maui-labs`). When the CLI or agent has a real bug, fix it instead of routing around it.

```bash
# 1. Reproduce against source. Pin the bug to a file/line from the stack trace.
#    Example: maui devflow diagnose stack trace pointed to
#    src/Cli/Microsoft.Maui.Cli/DevFlow/CliJson.cs:67

# 2. Branch off main (NOT a dirty WIP squad branch).
cd ~/work/maui-labs
git fetch origin
git checkout -b fix-devflow-<short-symptom> origin/main

# 3. Make the surgical fix. Stay narrow — no adjacent refactors.

# 4. Pack into LocalNuGets.
dotnet pack src/Cli/Microsoft.Maui.Cli.csproj -c Release -o ~/work/LocalNuGets/
# or for an agent fix:
dotnet pack src/Agent/Microsoft.Maui.DevFlow.Agent.csproj -c Release \
  -o ~/work/LocalNuGets/

# 5. Consume locally.
# CLI fix:
dotnet tool update -g maui --add-source ~/work/LocalNuGets/ --version <new-version>
maui --version
# Agent fix: bump <PackageReference Include="Microsoft.Maui.DevFlow.Agent" Version="..."/>
#           in the consuming MAUI project's csproj, restore, rebuild, relaunch.

# 6. Re-run the failing command; confirm fixed; continue the original smoke test.

# 7. Draft PR upstream.
git add -A
git commit -m "Fix: <symptom> → <root-cause>" \
  -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
git push -u origin fix-devflow-<short-symptom>
gh pr create --draft --repo dotnet/maui-labs \
  --title "..." \
  --body "## Repro\n...\n## Root cause\n...\n## Fix\n...\n## Risk\n..."

# 8. Record the PR number in the session plan.
```

**Boundaries on this rung:**

- Stay surgical. Land the smallest correct change; broader cleanup is a separate PR.
- For agent route bugs, prefer **adding** missing routes over **removing/renaming** new ones — keeps older agents compatible with new CLIs.
- If a fix requires a breaking schema change, stop and ask the user. That's a release-coordination decision.
- If the working branch in `~/work/maui-labs` has WIP, branch off `main` so the fix isn't entangled with unrelated work.

### Rung 8 — Last-resort observability fallbacks

ONLY after Rungs 1–7 have been tried and a real source-level blocker exists (e.g., fix is too invasive for one session):

- **Logs:** `maui devflow logs --follow --source native` — usually a different code path than `ui *` and often still works.
- **Manual:** ask the user to confirm a specific visible behaviour. Document the open upstream issue / draft PR alongside the manual hand-off.

## Hard rules

1. Never declare smoke testing "blocked" without running `curl http://localhost:<port>/api/status` first.
2. Never trust the CLI's "Cannot connect" wrapper — it masks JSON-schema and route-not-found errors. Rerun with `--verbose` and probe the raw HTTP layer.
3. Never uninstall, reset, or wipe the app to "fix" DevFlow. The agent is in-process; uninstalling wipes user data.
4. Never stop at "the tool is broken." We own the source — Rung 7 is in scope every time the bug is reproducible.
5. Always document which rung resolved, which routes the agent responds to, and (if Rung 7) the draft PR number.
