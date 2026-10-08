# Capture workflows

Recording works best when the agent knows why it is recording and keeps a
structured transcript while the app is being driven.

## Before recording

1. Confirm the target app is available with `ailoha agent wait` or the
   equivalent MCP agent status flow.
2. Check `ailoha recording status` if a prior run may still be active.
3. Pick a scenario name and output path before starting.
4. Decide the evidence budget: video only, video plus final screenshot, or video
   plus logs/network/profiler.

Use short recordings by default. Longer videos are harder to review, harder to
share, and more likely to capture sensitive or irrelevant content.

## Naming outputs

Prefer paths that encode scenario, platform, and date-free intent:

```text
artifacts/recordings/login-failure-ios.mp4
artifacts/recordings/create-item-android.mp4
artifacts/recordings/settings-friction-maccatalyst.mov
```

Do not put recordings in source folders unless the repo intentionally tracks
media fixtures. If the recording is only evidence for this session, keep it in
the session artifacts directory or another ignored output directory.

## Capture loop

```bash
ailoha agent wait --timeout 60
# CLI default timeout is 30s; this example uses 60s for a bounded repro flow.
ailoha recording start --platform ios --output artifacts/recordings/login-failure-ios.mp4 --timeout 60
# Drive the app with Ailoha UI tools and write down intentful steps.
ailoha recording stop --platform ios
ailoha ui screenshot --output artifacts/recordings/login-failure-final.png
ailoha logs --limit 100 --minLevel Warning
ailoha network list --limit 20
```

Use screenshots, logs, and network only when they add value. A visual layout bug
may only need a final screenshot and focused tree data. A failed save probably
needs network details and logs.

## Transcript format

Keep a small transcript outside the repo unless it is intentionally becoming a
test or issue artifact:

```text
Scenario: Create a todo item
Platform: android
Precondition: sample app launched on Todo screen, empty list
Recording: artifacts/recordings/create-todo-android.mp4

1. Query AddTodoButton -> found automationId AddTodoButton
2. Tap AddTodoButton -> New item form appears
3. Fill TitleEntry = "Buy milk"
4. Tap SaveButton
5. Verify item text "Buy milk" appears
```

Each line should preserve intent and the stable selector used. Avoid transcript
lines that only say "clicked button" or "waited".

## Platform limits

| Platform | Recorder | Notes |
|---|---|---|
| iOS simulator | `xcrun simctl io recordVideo` | Saves video from the simulator; stop to finalize the file |
| Mac Catalyst/macOS | `screencapture` | May save `.mov`; attempts window capture when possible |
| Android | `adb shell screenrecord` | Maximum is 180 seconds; output is pulled from device on stop |
| Windows | `ffmpeg` | Requires ffmpeg; captures desktop/window input depending on driver support |
| Linux | `ffmpeg`/x11grab | Requires ffmpeg and a valid `DISPLAY` |

Recording state is persisted in `~/.ailoha/recording-state.json`. If stop/status
reports a different platform than expected, there may be an active or stale
recording from a previous run.

## Capture stop signals

- Stop once the target state or failure is visible.
- Stop before doing unrelated cleanup or exploration.
- Stop before entering secrets or personal data. Use fixtures, masked values, or
  non-recorded setup for sensitive steps.
- Stop and restart with a narrower goal when the original run becomes a long
  exploratory session.
