# Android debugging strategies

## Token-aware order

1. Check `ailoha agent status` and a focused `ui query`.
2. Use `ui tree --depth 2` or `--depth 3` before a full tree.
3. Use `logs`, `network`, `storage`, or `profiler` for stateful failures.
4. Capture one screenshot only after you know the screen is correct.

## Views vs Compose

For Views, automation IDs usually come from resource IDs or
`setAilohaAutomationId`. For Compose, they come from `Modifier.testTag` and require
the Compose artifact/provider. Do not debug a Compose missing-element issue by
changing the native host view first.

## Device and emulator issues

Use `adb devices` to confirm the target exists, then `adb forward tcp:9233
tcp:9233` for local host access. If multiple agents are running, prefer broker
selection through the CLI instead of hardcoding alternate ports in app code.

## Capabilities first

Read `/api/v1/agent/capabilities` before using optional surfaces. Respect missing
or unsupported secure storage, BLE, jobs, and WebSocket streams.
