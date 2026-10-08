# Replay optimization

Replay should preserve the user's intent while removing accidental friction.
The goal is not to mimic every gesture; it is to reach the same meaningful app
state deterministically.

## Convert raw actions to intent

| Raw recording step | Replay step |
|---|---|
| Tap x=41 y=94 | Navigate back or tap BackButton |
| Tap third row after scrolling | Query item by text, then tap its element |
| Wait 5 seconds | Wait until target element exists or request completes |
| Tap blue button | Tap SaveButton by automation id/test id/text |
| Repeat screen traversal | Use route/deep link/setup helper when available |

Keep gestures only when the gesture itself is the behavior under test, such as
pinch, drag, swipe, or hit testing.

## Stable target hierarchy

Prefer targets in this order:

1. Developer-provided stable identifiers: `AutomationId`, `ValueKey`, `testID`.
2. Routes, screen names, and semantic selectors from the app code.
3. Visible text that is stable and not localized/variable for the scenario.
4. Structural queries from a shallow tree.
5. Coordinates only as a last resort, with display size and platform noted.

Re-query after navigation, list virtualization, page reloads, or modal changes.
Element ids may be valid only for the current tree.

## Waits and verification

Replace sleeps with observable conditions:

- element exists or disappears
- property equals expected value
- screen title or route is visible
- expected network request appears
- log line, performance marker, or visible wait point appears
- storage/preference value changed

Verify after meaningful transitions, not after every low-level action. For
example, a login flow can fill two fields and tap the login button in one batch,
then verify that the home screen appears.

## Batch actions

For CLI replay, `ailoha batch` reads CLI command lines from stdin and emits JSONL
responses. It does not read JSON command objects:

```bash
cat <<'EOF' | ailoha batch
ui fill <resolved-email-element-id> "test@example.com"
ui fill <resolved-password-element-id> "$TEST_PASSWORD"
ui tap <resolved-login-button-id>
ui assert --id <resolved-home-title-id> IsVisible true
EOF
```

Resolve dependent element ids before constructing the batch. If ids must be
looked up during the flow, use a shell script or driver test instead of batch
input so each lookup can feed the next action.

Use protocol-level batch actions when:

- every action targets elements already resolved in the current tree
- the actions are a single form interaction or stable local sequence
- no intermediate branch decision is needed
- `continueOnError` should be false for deterministic replay

Do not batch across navigation or state transitions unless the result will
include enough tree/screenshot data to verify the next state.

## Replay artifact pattern

```text
Scenario: Create item
Preconditions:
- App launched on Todo screen
- Test data cleared
- Network available

Actions:
1. Query AddTodoButton
2. Tap AddTodoButton
3. Query TitleEntry and SaveButton
4. Fill TitleEntry = "Buy milk"
5. Tap SaveButton

Assertions:
- Item text "Buy milk" appears
- Save request succeeds or no error log appears
```

## Friction review

After replay works, count meaningful steps and identify detours:

- repeated failed queries
- blind full-tree dumps
- coordinate taps
- unnecessary back-and-forth navigation
- waits without conditions
- repeated login/setup that could be fixture state

Recommend app improvements when replay reveals testability issues: stable ids,
deep links, seeded test state, clearer navigation, or fewer screens for a common
task.

## Replay stop signals

- The replay reaches the target state on a clean run.
- Preconditions and data setup are explicit.
- Selectors are stable enough for the app's framework.
- Assertions prove the intended result.
- Remaining optimization would change product behavior rather than replay
  quality.
