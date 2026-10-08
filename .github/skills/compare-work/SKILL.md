---
name: compare-work
description: Run a repeatable BaristaNotes feature-effort comparison between MAUI on iOS/Android and native .NET for iOS/Android. Use for compare-work, compare implementation cost, start a measured feature comparison, record comparison feedback, accept a comparison, or generate its final report. Uses scripts/compare_work.py for independent source exports, restricted Copilot workers, token/cost records, code snapshots, feedback rounds, and explicit user acceptance. Do not use for an ordinary feature request without a comparison request.
---

# Compare work

Use `python3 scripts/compare_work.py` from the BaristaNotes repository.
Read [the operating rules](references/operations.md) before starting workers.
Do not run a factory, fork this conversation, or create ordinary app sessions
as a substitute for the restricted launcher.

## Prepare the approved scope

1. Ask the user for the feature scope. Resolve material behavior/design doubts.
2. Copy `assets/scope-template.json` to private run storage. Fill the requirements,
   exclusions, acceptance criteria, target devices, model, and reasoning setting.
   Ask for approval of those settings. Never invent `approved_by`.
3. Include iOS and Android for both architectures. Keep runtime performance
   out of scope unless requested.
4. Check the chosen baseline and build tools. Resolve baseline blockers before
   implementation. Record preparation separately from feature effort.
5. Run `prepare scope.json --repo . --baseline COMMIT`. Run `doctor RUN`.
   Stop if either command fails. Do not weaken isolation to keep working.

## Execute independent work

Run `work RUN --group maui` and `work RUN --group native`. These start fresh,
file-tool-only Copilot processes in different source exports, not this session.
Each group has one active worker. Parallelize the two groups when practical.
Do not provide one group's fixes, code, reviews, or discoveries to the other.

Use `check RUN --group GROUP -- COMMAND ARGS` for source-local build/test checks.
Build iOS and Android for each group. Inspect failure logs in that group's
worker directory. Provide only that group's results when it needs a correction.
Use `feedback` with a file containing the approved product feedback; then run
`work --phase correction`. Use `work --phase review` for an independent review.
Review workers have the same source boundary and are instructed not to edit.

Workers cannot inspect devices or fetch public documentation. The coordinator
performs those actions and supplies only the appropriate group's evidence.
Use DevFlow for the MAUI app and the approved native inspection path for native
apps. Create data through app forms. Never install over the personal application.
Check the evaluated application identity before every deployment.

## Feedback and acceptance

Configure `track-coordinator RUN --session SESSION_ID --start UTC_TIME` before
the first work prompt. Use the current coordinator session ID and the approved
measurement start, not an invented identity. It reads the local usage database
read-only; its cost stays separate from the two worker totals.

Run `snapshot RUN --label pre-ux-N` before admitting a UX round. This one command
saves metrics, coordinator cost, ledger, clean source archives, receipts, and an
interim comparison without accepting the work. Use `--compare-to pre-ux-N` on a
later snapshot to retain incremental worker cost. Do not replace frozen snapshots.

Run `checkpoint` before/after a feedback boundary. Use `feedback --group both`
for scope additions and product clarifications. Keep one-group defects local.
Never include a technical fix from the competing implementation.
Use `--kind technical` for compiler/runtime corrections and `--kind ux` for
the user's design/interaction feedback. Do not present technical round numbers
as counts of user UX rounds. The latest feedback and current scope take
precedence over historical exclusions; earlier messages remain in the ledger.

Record `evidence` for each criterion, platform, group, current round, and source
snapshot. Capture the actual states and transitions, not just build success.
Only record PASS after exercising the requirement. Retain the built package
hash alongside screenshots/logs in the evidence file.

Ask the user for explicit acceptance. Save their actual signoff text and run
`accept` for each accepted group with its author. Do not accept for the user.
Generate `report` only after both groups are accepted. If source changes after
acceptance, stop; do not silently regenerate evidence or signoff.

## Measurement and closeout

The launcher registers each model/check attempt before starting it. It retains
real CLI receipts and imports each receipt once. Failed or interrupted attempts
remain in the ledger. Never reset accrued cost for a restart or feedback round.
Use `status` for current totals. Missing usage is unavailable, never zero.

Keep the report and raw records private. Publish sanitized reports only on
request. Keep setup, coordinator, human review, and later integration separate:
the pilot does not automatically meter those activities. Report that limit.
Do not call model duration human effort or internal usage units dollars.
Do not treat lines of code as a productivity score.

After acceptance, reconcile Core changes separately. Do not merge or transfer
code between groups before acceptance. Do not commit/push without the user's
request. The next comparison starts from the integrated, approved baseline.
