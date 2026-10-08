# Android CLI workflows

Use broker-first commands unless you are validating a device-forwarding fallback.

## Connection loop

```bash
ailoha diagnose
adb forward tcp:9233 tcp:9233
ailoha agent wait --timeout 60
ailoha agent status
```

The direct agent port is `9233`; the broker default is `19323`. The Android agent
falls back through `9238` if the preferred port is busy.

## Inspect and act

```bash
ailoha ui query --automationId EmailInput
ailoha ui tree --depth 3
ailoha ui fill <element-id> "user@example.com"
ailoha ui tap <button-id>
ailoha ui screenshot --output android.png
```

Prefer stable IDs over text and coordinates. For Compose, stable IDs come from
`Modifier.testTag`.

## Diagnostics

```bash
ailoha logs --limit 50
ailoha network list
ailoha network inspect <request-id>
ailoha profiler capabilities
ailoha storage roots
ailoha storage preferences
```

Network and log output only appears if the app uses the OkHttp/Timber adapters or
directly records events through `AilohaAndroidAgent`.

## WebView

```bash
ailoha webview list
ailoha webview Runtime evaluate "document.title"
ailoha webview Runtime query "#login"
```

If no WebView appears, query the UI tree for the expected `MainWebView` or app
specific automation ID before trying DOM commands.
