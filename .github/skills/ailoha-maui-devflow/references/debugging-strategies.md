# MAUI debugging strategies

This reference is about reducing wasted tool calls and token spend while still
finding the bug quickly.

## Cost ladder

| Cost | Preferred tools | Use them for |
|---|---|---|
| Low | `diagnose`, `agent status`, `agent wait`, `ui query`, `logs`, `network list` | Fast connection checks and focused state checks |
| Medium | `ui tree --depth 3/4`, `ui element`, `webview list`, `Runtime evaluate` | Local context or one subsystem deeper |
| High | full tree dumps, repeated screenshots | Only when cheaper tools cannot answer the question |

## Default loop

1. **Confirm integration** from source files.
2. **Connect** with `diagnose` -> `agent wait` -> `agent status`.
3. **Target the smallest thing first** with `ui query --automationId ...`.
4. **Open the scope slightly** with a shallow tree if the query is not enough.
5. **Switch subsystem instead of expanding output**:
   - logs for behavior
   - network for API state
   - webview for Blazor content
6. **Use one screenshot** to confirm a visual hypothesis.

## When screenshots are worth it

Screenshots are useful when:

- the issue is visual styling or layout
- a query found the element, but you need to confirm placement or clipping
- you need proof that the UI reached the expected screen

Screenshots are a poor first move when:

- the question is "did the agent connect?"
- the problem is request state or business logic
- a stable `AutomationId` can answer the question

## MAUI-specific heuristics

- Prefer `AutomationId` over text queries when both exist.
- Re-query after navigation or after large state changes.
- Use WebView commands only when the app actually contains Blazor content.
- If the bug is "the page never loaded," check connection and logs before taking
  screenshots.
