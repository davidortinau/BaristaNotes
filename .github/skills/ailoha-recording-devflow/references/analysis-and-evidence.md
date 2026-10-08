# Analysis and evidence

Analyze recordings by correlating the clip with structured Ailoha data. Video
shows what happened visually; Ailoha data explains what the app thought was
happening.

## Evidence order

1. Start with the user's symptom and the action transcript.
2. Locate the smallest likely time window or step number.
3. Use structured data to verify state: `ui query`, `ui element`, shallow tree,
   logs, network details, storage, or profiler samples.
4. Capture or extract only the screenshots/frames needed to explain the finding.
5. Present the finding with the video path and exact corroborating facts.

Avoid random scrubbing through long videos. First narrow the window with the
transcript, logs, network request timestamps, or visible step boundaries.

## What to collect by symptom

| Symptom | Good evidence |
|---|---|
| Wrong screen | screenshot/frame, route/title element, navigation transcript |
| Missing or disabled control | focused `ui query`/`ui element`, screenshot around the control |
| Failed save/load | network list/detail, warnings/errors, final UI assertion |
| Slow interaction | recording timestamp range, profiler samples, repeated waits |
| Flaky replay | selector lookup failures, tree before/after navigation, stable id gaps |
| UX friction | action count, detours, repeated backtracking, missing shortcuts |

## Screenshots and frames

Use screenshots from Ailoha when the app is still in the target state. Extract
video frames only when the moment is no longer reproducible or when the video is
the artifact the user provided.

When extracting frames, put them in artifacts, not source:

```text
artifacts/recordings/login-failure-step-04.png
artifacts/recordings/settings-delay-00m12s.png
```

Name frames by step or timestamp. Do not produce dozens of nearly identical
frames. Usually one "before", one "failure", and one "after" frame is the upper
bound.

## User-facing summary pattern

Use concise evidence-backed language:

```text
The failure happens after step 4. The recording shows the Save button stays
disabled, and `ui element SaveButton` confirms IsEnabled=false while TitleEntry
has text. The app never sends the expected POST /items request, so this looks
like client-side validation or binding state rather than an API failure.
Evidence: artifacts/recordings/create-item-ios.mp4, step 4 frame,
network list with no POST /items.
```

## Privacy and safety

- Do not share or attach frames that expose credentials, tokens, private user
  data, request bodies, or personal information.
- Redact or omit sensitive details from summaries.
- Do not store frame contents in memory. If a durable lesson is needed, store
  the safe abstraction: "Use route /settings to reach preferences" rather than
  text copied from a private screenshot.
- If a recording includes sensitive material, say so and limit analysis to local
  facts needed for the task.

## Analysis stop signals

- You can explain the issue with a specific step/time and corroborating state.
- Additional frames would be duplicates.
- The remaining uncertainty is in app code, not in the recording; switch to code
  investigation or test generation.
