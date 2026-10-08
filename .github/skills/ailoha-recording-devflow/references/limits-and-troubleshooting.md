# Limits and troubleshooting

The recording skill should be honest about what Ailoha currently exposes.
Recording captures screen video. Search, replay, and test generation are
workflow capabilities built by combining recordings with structured Ailoha
tools, not separate magic APIs.

## Current recording surface

CLI commands:

```bash
ailoha recording start --platform ios --output path/to/file --timeout 60
ailoha recording stop --platform ios
ailoha recording status
```

MCP tools:

```text
recording_start
recording_stop
recording_status
```

Related structured signals:

- `ui_tree`, `ui_query`, `ui_element`, `ui_hittest`
- `ui_tap`, `ui_fill`, `ui_clear`, `ui_scroll`, `ui_navigate`
- `ui_screenshot`
- `logs_get`
- `network_list`, `network_detail`, `network_clear`
- profiler data when available through the target app or surrounding tools

## What not to promise

- Do not claim there is a built-in searchable recording index unless one has
  been added.
- Do not claim Ailoha can automatically infer every action from a video.
- Do not claim replay can be generated reliably from pixels alone.
- Do not claim recordings are safe to share without checking for sensitive
  content.

## Common problems

| Symptom | Likely cause | Response |
|---|---|---|
| `recording status` says no active recording | Recorder exited or state was cleaned up | Start a new bounded recording |
| Stop reports another platform | Persisted state belongs to a different active recording | Stop from the matching platform context or inspect stale state |
| CLI start records the wrong target | `--platform` was omitted and the CLI defaulted to Mac Catalyst | Pass `--platform ios`, `--platform android`, `--platform windows`, or the matching platform context |
| Android recording stops early | `adb screenrecord` max is 180 seconds | Use shorter scenario clips |
| Windows/Linux start fails | `ffmpeg` missing or display capture unavailable | Install/configure ffmpeg or use screenshots |
| macOS output is `.mov` | `screencapture` uses mov container | Keep path as reported or convert outside Ailoha if needed |
| Large video is hard to analyze | Goal was too broad | Split into shorter scenario recordings |

## Fallbacks

If recording is unsupported or unavailable:

- use `ui screenshot` at key states
- collect tree/query/log/network evidence
- create an explicit manual transcript while reproducing the flow
- generate replay/test code from structured steps instead of video

If frame extraction is required and local tools are available, extract only the
few frames needed into artifacts. Do not add a script to the skill until this is
a repeated workflow and the script can emit structured data without doing the
agent's reasoning.

## Troubleshooting stop signals

- You know whether the problem is recording availability, platform tooling,
  stale state, or overly broad capture scope.
- A fallback evidence path can answer the user's question.
- Further work requires changing Ailoha implementation rather than using the
  recording workflow skill.
