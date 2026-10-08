# MAUI CLI workflows

This reference maps the Ailoha CLI to the most common MAUI debugging loops.
Treat it as a workflow chooser, not a complete command catalog.

## Connection and discovery

| Command | Use it when | What it tells you |
|---|---|---|
| `ailoha diagnose` | You want the fastest overall health check | Whether broker, agent resolution, and project integration look healthy |
| `ailoha agent wait --timeout 60` | The app is starting or reconnecting | Blocks until a matching agent is available |
| `ailoha agent status` | You want to confirm the currently resolved target | Resolved host, port, and capabilities |
| `ailoha agent list` | More than one app may be running | All connected agents |
| `ailoha broker status` | Discovery feels suspicious | Broker health and whether discovery is available |

## Inspection and actions

| Command | Use it when | Why it is a good next step |
|---|---|---|
| `ailoha ui query --automationId SubmitButton` | You know the control id | Cheapest focused lookup |
| `ailoha ui tree --depth 3` | You need local hierarchy context | Small enough to reason over quickly |
| `ailoha ui element <id>` | You need one element's details | Avoids another full tree dump |
| `ailoha ui tap <id>` | You already resolved the target | Keeps the interaction loop tight |
| `ailoha ui fill <id> "text"` | You are testing entries or editors | Faster than ad hoc navigation and screenshots |

## Diagnostics

| Command | Use it when | Notes |
|---|---|---|
| `ailoha logs --limit 50 --minLevel Warning` | App behavior is wrong but the tree looks fine | Often cheaper than a screenshot-first loop |
| `ailoha network list --limit 20` | You suspect API or request state problems | Good for login, load, and retry issues |
| `ailoha ui screenshot` | Layout or visual styling is the question | Use once you already know where to look |

## Blazor Hybrid

Use these only when the app actually contains `BlazorWebView` content:

```bash
ailoha webview list
ailoha webview Runtime evaluate "document.title"
ailoha webview DOM querySelector "h1"
```

If the app is MAUI-only, webview commands are the wrong branch.

## Typical MAUI loops

### Connect and inspect

```bash
ailoha diagnose
ailoha agent wait --timeout 60
ailoha ui tree --depth 3
```

### Inspect a known control

```bash
ailoha ui query --automationId SubmitButton
```

If the query succeeds, prefer `ui element`, `ui tap`, or `ui fill` over a larger
tree.

### Check a Blazor page

```bash
ailoha webview list
ailoha webview Runtime evaluate "document.title"
```

### Validate a suspected visual bug

```bash
ailoha ui query --automationId SubmitButton
ailoha ui screenshot
```

Take the screenshot after you already know the relevant screen is loaded.
