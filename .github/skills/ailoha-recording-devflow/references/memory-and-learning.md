# Memory and learning from recordings

Recordings can reveal friction and reusable workflow knowledge. Preserve only
facts that will help future agents work in the codebase, and avoid preserving
private recording content.

## What is worth remembering

Safe, useful memories:

- route names and deep links for common screens
- stable automation ids, test ids, keys, and selectors
- app-specific setup shortcuts and seed data patterns
- known test accounts described by fixture name, not credentials
- reliable verification points for common flows
- common failure signatures and where to look first
- "avoid this detour" lessons, such as using a route instead of five taps

Do not remember:

- screenshots or frame contents
- credentials, tokens, request bodies, or personal data
- exact private user text from a recording
- one-off element ids from a transient visual tree
- local machine paths unless they are repo-relative and intentional

## Friction rubric

After a recording or replay, ask:

1. How many meaningful user actions were required?
2. Which actions were detours or retries?
3. Did the agent rely on screenshots before trying structured state?
4. Were any coordinate taps needed because stable ids were missing?
5. Could setup state, a route, or a batch action remove steps?
6. Did waits have explicit conditions?
7. What should future agents do differently in this codebase?

This rubric is about improving future work, not criticizing the user. Focus on
repeatable process improvements and app testability.

## Memory wording pattern

Use concise, durable phrasing:

```text
In this app, use route /settings/profile to reach the profile editor directly.
The SaveButton automation id is stable; verify ProfileSavedToast after saving.
```

Avoid storing recording-specific content:

```text
Do not store: "At 00:14 the user's email address was visible in the profile
screen screenshot."
```

## Self-healing opportunities

When a recording shows repeated friction, consider codebase changes:

- add missing `AutomationId`, `ValueKey`, or `testID`
- add a test fixture or seed-data helper
- expose a debug-only route/deep link for common setup
- add clearer screen titles or accessible labels
- make success/failure states observable in UI or logs
- add integration tests for flows that users repeatedly record

Only make code changes when the user asked for implementation or the current
task includes fixing the friction. Otherwise, report the opportunity clearly.

## Memory stop signals

- The lesson applies beyond this one recording.
- The lesson is safe to persist.
- The memory is phrased as reusable workflow guidance.
- The source artifact does not need to be retained to use the lesson.
