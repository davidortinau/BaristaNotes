# Repeatable work comparisons

Use the repository skill **compare-work**, or ask Copilot to compare a new
feature across the MAUI and native applications. Provide the feature scope
first. Copilot will resolve behavior and design questions, confirm execution
settings, and ask you to approve the scope.

The local command is `python3 scripts/compare_work.py`. It prepares independent
source copies, starts restricted Copilot workers, retains usage receipts and
code snapshots, records feedback, and produces a report after explicit
acceptance. It does not change the application in this repository.

Both groups must deliver on iOS and Android. Native iOS and Android can reuse
their own Core changes; those changes do not pass to the MAUI group. Workers
receive no previous conversation and cannot use shell, session-history,
messaging, or delegation tools. The macOS OS sandbox also blocks reads of the
other group, original source, and private measurement records.

Use the skill's [operating rules](../.github/skills/compare-work/references/operations.md)
for commands, access restrictions, build preparation, and acceptance rules.
Private runs are stored outside the repository under
`~/.baristanotes-comparisons/` by default. No existing usage records are reset.

The report distinguishes measured model cost, command duration, final source
change, and snapshot churn. Cached tokens are not added to gross input.
Internal AI usage units are not dollars, and model duration is not human labor.
Missing records remain unavailable.

AI-worker isolation and build-tool isolation are separate. If Apple or Android
tools cannot build inside the OS sandbox, user-approved coordinator builds can
use `check --host-toolchain` on the independent source copies. Worker source/tool
restrictions remain enforced. Execution receipts record the host-build mode;
the comparison must not claim that those build tools were sandboxed.

Human review, external device inspection, setup, and final integration are
not automatically measured. Its report states these limits. Those costs must
not be described as zero or included in one architecture's total only.

Configure `track-coordinator` with the coordinator session and start time to
include its recorded model cost separately. `snapshot --label LABEL` now saves
an interim comparison, ledger, source archives and receipts before acceptance.
Later snapshots can use `--compare-to LABEL` for incremental worker metrics.
Technical corrections and UX feedback have separate kinds. Human effort and
external device time remain unmeasured unless separately recorded.

No benchmark feature starts until you supply and approve its scope. Application
runtime checks remain required for acceptance; a successful build is not enough.

The user ended the first map comparison's isolation and approved integrating
both app versions as work in progress on October 8, 2026. The frozen private
records remain unchanged; this is not final comparison or release acceptance.
See the [native port feature review](native-port-review.md) for source coverage,
missing experiences, and the documented app/tooling backlog. The export and
incomplete-receipt findings must be addressed before the next affected comparison.
