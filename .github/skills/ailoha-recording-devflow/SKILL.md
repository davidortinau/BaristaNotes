---
name: ailoha-recording-devflow
description: >
  Use Ailoha recordings to capture, analyze, replay, and convert app journeys
  into repeatable UI/API test sequences. USE FOR: starting or stopping screen
  recordings, recording a user flow, pinpointing issues in recorded app
  journeys, extracting screenshots from a repro, replaying a flow, evaluating
  UX friction, generating UI tests or Ailoha action sequences from a recording.
  DO NOT USE FOR: adding Ailoha to a platform app (use that platform's DevFlow
  skill), implementing new platform agents (use ailoha-platform-agent), or
  generic video editing or standalone memory updates. INVOKES: Ailoha CLI/MCP
  recording, UI, screenshot, logs, network, and action tools.
license: LicenseRef-Ailoha-Binary-Distribution
---

# Ailoha Recording DevFlow

Use this skill when a screen recording is part of the debugging or automation
loop. Treat the video as evidence, not as the only source of truth: pair it with
Ailoha UI queries, screenshots, logs, network entries, profiler data, and a
short action transcript.

## When to Use This Skill

Use this skill when:

- starting, stopping, or checking an Ailoha screen recording
- recording a repro, login, setup, create/save/delete, or navigation flow
- finding the moment where a recorded journey goes wrong
- extracting a small set of screenshots or frames to explain an issue
- replaying a recorded flow with fewer, more deterministic steps
- turning a recorded journey into CLI commands, JSONL batch input, or tests
- evaluating UX friction, repeated clicks, brittle navigation, or slow flows
- deciding what reusable workflow knowledge from a recording/replay should be
  remembered for a codebase

## When to Stop

- The recording is stopped, saved, and its path is reported.
- The issue is pinned to the smallest useful evidence set: usually one video
  path plus a few screenshots, UI facts, logs, network entries, or profiler
  observations.
- Replay reaches the intended state with stable selectors, explicit
  preconditions, and assertions after meaningful state transitions.
- Test generation includes setup, actions, and final verification without
  embedding secrets, local-only paths, or one-off recording artifacts.
- You have captured reusable codebase knowledge only when it is stable and safe
  to remember. Do not store private video content, credentials, or screenshots.

## Workflow

### 1. Choose the recording goal before starting

Classify the goal first:

| Goal | Record | Also collect |
|---|---|---|
| Visual bug | Short clip around the target screen | `ui screenshot`, focused `ui query`/`ui element` |
| Repro flow | Full journey from clean precondition | action transcript, final tree/query, logs |
| Network/state bug | Steps that trigger the request/state change | `network list/detail`, logs, relevant storage |
| Performance/friction | User-visible wait points and detours | profiler samples, action count, timings |
| Test generation | Deterministic setup and core actions | selectors, route names, final assertions |

> Do not start a long blind recording when a shallow tree, targeted query, log,
> or network request can narrow the symptom first.

### 2. Capture with a transcript

Use a bounded recording and a named output path:

```bash
ailoha agent wait --timeout 60
# CLI default timeout is 30s; pass a longer bounded timeout when the repro needs it.
ailoha recording start --platform ios --output artifacts/login-repro-ios.mp4 --timeout 60
```

Drive the app with structured Ailoha commands whenever possible and keep a
compact transcript:

```text
1. Open login screen -> route /login
2. Fill EmailEntry with test user
3. Fill PasswordEntry with test password
4. Tap LoginButton
5. Verify HomeTitle is visible
```

For each step, preserve intent and stable identifiers (`AutomationId`,
`ValueKey`, `testID`, text, route, or selector). Re-query after navigation or a
major UI change because element ids may be ephemeral.

Stop promptly:

```bash
ailoha recording stop --platform ios
ailoha recording status
```

See [references/capture-workflows.md](references/capture-workflows.md) for
platform limits and capture patterns.

### 3. Analyze evidence, not just video

Start from the transcript and any known failure time. Correlate the clip with:

- focused screenshots or extracted frames for visual evidence
- UI tree/query/element output for exact state
- logs and network details for behavior that is not visible
- profiler samples when available, logs, or visible wait points for performance
  issues

When presenting findings, point to the smallest evidence set. Prefer "frame at
step 4 shows SaveButton disabled; `ui element` confirms IsEnabled=false" over a
large video dump.

See [references/analysis-and-evidence.md](references/analysis-and-evidence.md).

### 4. Optimize replay

Convert raw gestures into intentful, deterministic operations:

- "open settings", not "tap x=34 y=92"
- "tap SaveButton", not "tap the lower-right blue button"
- "wait until SuccessToast exists", not "sleep 3"

Use CLI batch mode or MCP `ui_*` tool calls as the primary replay surface. The
underlying protocol also supports batch actions when several actions are safe to
combine, then verify after a meaningful transition:

```http
POST /api/v1/ui/actions/batch
{
  "actions": [
    { "action": "fill", "elementId": "EmailEntry", "text": "user@example.com" },
    { "action": "fill", "elementId": "PasswordEntry", "text": "..." },
    { "action": "tap", "elementId": "LoginButton" }
  ],
  "include": ["screenshot", "tree"],
  "continueOnError": false
}
```

See [references/replay-optimization.md](references/replay-optimization.md).

### 5. Generate repeatable automation

Choose the narrowest useful output:

| Need | Output |
|---|---|
| Human repro | Preconditions, numbered steps, expected result, evidence links |
| Protocol replay | Ailoha CLI commands or JSONL batch input |
| App integration coverage | Ailoha.Driver/xUnit-style UI test |
| Pure logic defect | Unit test in the repo's existing test framework |

Always include setup, actions, and assertions. Assertions should prove the app
reached the intended state, not just that taps returned success.

See [references/test-generation.md](references/test-generation.md).

### 6. Learn safely from recording friction

After replay or analysis, look for reusable lessons:

- stable routes, selectors, test data, setup shortcuts, and verification points
- repeated detours caused by missing identifiers or unclear navigation
- opportunities to replace 10-click flows with route/deep-link/setup helpers
- brittle coordinate taps that should become stable automation ids

Record only durable, non-sensitive knowledge. See
[references/memory-and-learning.md](references/memory-and-learning.md).

## Critical Anti-Patterns

| Do not | Do instead |
|---|---|
| Treat a recording as a substitute for app state | Verify with UI tree/query, logs, network, or profiler data |
| Promise automatic video search or replay APIs that do not exist | Explain the available recording plus structured Ailoha signal workflow |
| Keep recording after the target evidence is captured | Stop promptly and preserve the saved path |
| Replay coordinates, sleeps, and incidental element ids | Use stable selectors, route intent, and condition-based waits |
| Commit recordings or extracted frames accidentally | Keep media in artifacts or ignored output folders |
| Store private frames, credentials, or request bodies as memory | Store only reusable, safe workflow facts |

## References

- **Capture workflows**:
  [references/capture-workflows.md](references/capture-workflows.md)
- **Analysis and evidence**:
  [references/analysis-and-evidence.md](references/analysis-and-evidence.md)
- **Replay optimization**:
  [references/replay-optimization.md](references/replay-optimization.md)
- **Test generation**:
  [references/test-generation.md](references/test-generation.md)
- **Memory and learning**:
  [references/memory-and-learning.md](references/memory-and-learning.md)
- **Limits and troubleshooting**:
  [references/limits-and-troubleshooting.md](references/limits-and-troubleshooting.md)
