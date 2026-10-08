---
name: ailoha-android-devflow
description: >
  End-to-end workflow for onboarding, connecting, inspecting, and debugging
  Ailoha in native Android apps. USE FOR: adding com.ailoha Android Gradle
  artifacts, wiring AilohaAndroidAgent in debug-only Kotlin/Java app startup,
  enabling Jetpack Compose semantics, adding OkHttp/Timber diagnostics,
  connecting the ailoha CLI through adb forwarding or broker discovery,
  inspecting Android Views by resource IDs or Ailoha automation IDs, inspecting
  Compose by Modifier.testTag, and troubleshooting WebView, storage, network,
  logs, and profiler behavior. DO NOT USE FOR: implementing a new Ailoha
  platform agent (use ailoha-platform-agent), generic Android architecture
  advice, or MAUI/Flutter/React Native setup. INVOKES: repo docs, Ailoha CLI
  commands, adb, and Android Gradle tooling.
license: LicenseRef-Ailoha-Binary-Distribution
---

# Ailoha Android DevFlow

Use this skill when a native Android app needs to become connected, queryable,
and debuggable with Ailoha.

## When to Use This Skill

Use this skill when:

- adding Ailoha to an Android app or sample
- wiring `AilohaAndroidAgent.start(...)` in debug-only startup
- adding the no-op release artifact so release builds do not ship the server
- enabling Jetpack Compose semantics through `AilohaComposeAgent.install()`
- adding stable IDs with resource IDs, `setAilohaAutomationId(...)`, or
  `Modifier.testTag(...)`
- diagnosing `adb forward`, broker discovery, or CLI connection failures
- using logs, network capture, profiler, storage, WebView, or screenshots to
  debug an Android issue

## When to Stop

- The Gradle dependency is present, startup is debug-only, and release builds
  reference only the no-op artifact or no agent code.
- `ailoha diagnose`, `ailoha agent wait`, or `ailoha agent status` confirms the
  host can reach the running app.
- A focused `ui query`, `logs`, `network`, `profiler`, or `webview` command
  explains the issue.
- One screenshot confirms visual state. Do not keep capturing equivalent frames.

## Workflow

### 1. Confirm integration from source

Look for the real Android integration points:

- `com.ailoha:ailoha-android-agent` in a debug dependency configuration
- `com.ailoha:ailoha-android-agent-noop` in release if shared code references
  the API
- `AilohaAndroidAgent.start(...)`
- a debug-only guard or debug source set
- optional `AilohaComposeAgent.install()` for Compose apps
- stable element IDs on the screen being automated

Do not infer missing integration purely from runtime symptoms.

### 2. Onboard correctly if Ailoha is missing

Follow [references/setup.md](references/setup.md):

- add debug dependencies and the no-op release artifact
- start the agent from debug-only `Application` startup
- keep the default port unless the environment requires an override
- add stable IDs for important Views or Compose nodes
- forward `adb` only when broker discovery cannot reach the device directly

### 3. Connect with broker-first CLI flow

Use the cheap connection loop first:

```bash
ailoha diagnose
ailoha agent wait --timeout 60
ailoha agent status
```

For emulators or physical devices, add port forwarding before retrying:

```bash
adb forward tcp:9233 tcp:9233
ailoha agent status
```

### 4. Inspect cheap before expensive

For most Android issues:

1. Query by stable `automationId`.
2. Use a shallow tree before a full tree.
3. Check logs, network, or profiler before screenshots when the issue is stateful.
4. Use WebView routes only after the tree confirms the target WebView is present.

### 5. Escalate by symptom

| Symptom | Best next move |
|---|---|
| Agent not found | `diagnose` -> `adb forward` -> `agent wait` -> re-check startup |
| View missing | `ui query --automationId ...` -> shallow `ui tree` |
| Compose node missing | verify `AilohaComposeAgent.install()` and `Modifier.testTag(...)` |
| Network missing | verify OkHttp interceptor or direct `recordNetworkRequest(...)` |
| Logs missing | verify Timber tree or direct `recordLog(...)` |
| WebView issue | `webview list` -> `webview evaluate` -> screenshot once |

### 6. Keep Android anti-patterns in mind

- Do not run the agent in release builds.
- Do not depend on coordinates when resource IDs or `testTag` are available.
- Do not expect generic network/log capture without an adapter or explicit event
  recording.
- Do not advertise secure storage, BLE, jobs, or WebSocket streams unless the app
  has a supported adapter and the agent capabilities show it.

## References

- **Onboarding and integration**:
  [references/setup.md](references/setup.md)
- **Command selection and workflows**:
  [references/cli-workflows.md](references/cli-workflows.md)
- **Troubleshooting**:
  [references/troubleshooting.md](references/troubleshooting.md)
- **Token-aware debugging strategy**:
  [references/debugging-strategies.md](references/debugging-strategies.md)
