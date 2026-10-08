# Android troubleshooting

## Agent not found

1. Confirm the app is a debug build.
2. Confirm `AilohaAndroidAgent.start(...)` runs in the `Application`.
3. Run `adb forward tcp:9233 tcp:9233`.
4. Run `ailoha agent wait --timeout 60`.
5. If the port is busy, inspect `ailoha diagnose` and the app logs for the
   selected fallback port.

## Release build contains agent code

Use debug source sets or `BuildConfig.DEBUG`. If shared code references Ailoha
APIs, add `com.ailoha:ailoha-android-agent-noop` to release builds.

## Element cannot be found

- Views: add a resource ID or `setAilohaAutomationId("...")`.
- Compose: call `AilohaComposeAgent.install()` and add `Modifier.testTag("...")`.
- Query shallow first: `ailoha ui query --automationId ...`.

## Network or logs are empty

Android does not automatically intercept every HTTP or logging stack. Add
`AilohaOkHttpInterceptor`, plant `AilohaTimberTree`, or call
`AilohaAndroidAgent.recordNetworkRequest(...)` / `recordLog(...)` directly.

## WebView commands fail

Confirm the screen containing the WebView is visible, then run:

```bash
ailoha ui query --automationId MainWebView
ailoha webview list
```

Only run DOM/evaluate commands after a WebView context is listed.

## Unsupported responses

The Android agent intentionally returns explicit unsupported responses for secure
storage, BLE, generic device jobs, and WebSocket streams until those surfaces have
real adapters. Do not work around this by pretending a missing capability exists.
