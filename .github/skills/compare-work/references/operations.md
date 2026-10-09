# Operating rules

## Requirements

Use macOS, Python 3.10 or later, `git`, `sandbox-exec`, Copilot CLI, its Node
launcher, and GitHub authentication through an environment token or `gh`.
The tested CLI exports `totalNanoAiu`, `modelMetrics`, and a tool catalog in
its JSON event stream. If a CLI update omits those fields, stop and inspect
the contract. Do not substitute estimated usage.

This pilot uses one fresh worker per invocation, with an independent home per
architecture. It does not resume old sessions. A correction sees its group's
current code and approved feedback. The resulting context-read cost is part
of that group's cost. Both groups have the same worker policy.

## Commands

`RUN` is the private directory printed by `prepare`. Store input files outside
the repository. Do not place credentials in the scope or feedback.

```bash
python3 scripts/compare_work.py prepare /PRIVATE/scope.json --repo . --baseline COMMIT
python3 scripts/compare_work.py doctor RUN
python3 scripts/compare_work.py status RUN
python3 scripts/compare_work.py track-coordinator RUN --session SESSION_ID --start UTC_TIME
python3 scripts/compare_work.py snapshot RUN --label pre-ux-1
python3 scripts/compare_work.py snapshot RUN --label post-ux-1 --compare-to pre-ux-1

python3 scripts/compare_work.py work RUN --group maui
python3 scripts/compare_work.py work RUN --group native
python3 scripts/compare_work.py check RUN --group maui -- dotnet test src/BaristaNotes.Tests

python3 scripts/compare_work.py checkpoint RUN
python3 scripts/compare_work.py feedback RUN --group native --kind defect --file /PRIVATE/feedback.txt
python3 scripts/compare_work.py work RUN --group native --phase correction
python3 scripts/compare_work.py work RUN --group native --phase review

python3 scripts/compare_work.py evidence RUN --group maui --platform ios \
  --criterion journey-1 --result pass --file /PRIVATE/ios-evidence.txt
python3 scripts/compare_work.py accept RUN --group maui \
  --signed-by USER --file /PRIVATE/user-signoff.txt
python3 scripts/compare_work.py report RUN
```

For a scope addition, use `feedback --group both --kind scope-addition --file`
with a complete, newly approved scope JSON file, including the updated criteria.
Keep the model and reasoning settings unchanged. The runner retains the original
scope, increases its version, and requires fresh evidence from both groups.

Supply evidence for both platforms and both groups. Feedback invalidates the
previous round's evidence even when no source changes. Changed source requires
new evidence. The final report requires both groups' acceptance.

## Source isolation

`prepare` exports the committed baseline through Git archive; it never copies
the live worktree, development configuration, Git history, or old conversations.
It refuses modified or untracked application source. Unrelated documentation
and tool changes are not exported. Commit only with user approval.

MAUI receives its head, Core, and relevant tests. Native receives both native
heads, Core, tests, linked resources, and the debug binding projects. Each has
its own Core copy. The MAUI test export removes two native-head compile links
and their associated tests. The native test export removes the MAUI
`BeanPageScrollGeometry.cs` compile link and `BeanPageScrollGeometryTests.cs`.
It does not receive the MAUI components directory. Both exports retain the
shared `AppColors.cs` link. These preparation differences are in the manifest.
The combined repository test project and its links remain unchanged.

The full worker process runs under Seatbelt. It cannot read the other group's
files, the coordinator ledger, the original source repository, or user-home
history. Metadata needed for path resolution is not a secret-content boundary.
The OS policy applies to built-in file tools, not only shell commands.

Only file tools are exposed: `view`, `create`, `edit`, `apply_patch`, `grep`,
`rg`, and `glob` where supported by the selected model. Shell, delegation,
history, MCP, external retrieval, and messaging tools are not exposed. An
isolated Copilot home has no plugins or user instructions. The launcher clears
the inherited environment and passes the GitHub credential only to the CLI.
It does not write that credential to a file or command argument.

`doctor` checks actual reads, including the original `.git/HEAD`. Every worker
launch repeats the checks. The launch also verifies the tool catalog in the
event stream. Missing or unexpected tools make the attempt fail, not pass.

Disable the CLI's inner command sandbox in the private home. The complete
worker process already runs inside the outer Seatbelt policy; nesting the
CLI sandbox can prevent its file-search subprocess from starting. This does
not permit reads outside the outer policy or expose shell tools.

The scope and feedback are human-approved product inputs. The coordinator can
see both groups, so it must not transfer technical findings. The boundary does
not prevent a human from deliberately copying information. It does not claim
to separate pretrained model knowledge or disable provider-side prompt caching.

## Builds and device checks

`check` records a source-local command and runs it under the file boundary.
It grants read-only access to the installed .NET directory, uses separate
NuGet caches, and records its output in the group's worker directory. It does
not inherit signing credentials or general user configuration.

If a build needs another input, do not widen access to the original repository
or to the other worker. Supply the same approved, baseline-only input to both
groups. Record the environment difference. Native debug bindings and signing
may need this preparation before a feature begins.

Do not use `check` for application installation or UI interaction. The
coordinator selects an isolated app identity, confirms the final evaluated ID,
then deploys and inspects through the approved platform tools. Keep device
checks exclusive. Save evidence with exact package hashes and target records.
Record external verification duration in the evidence; the pilot does not
automatically count that duration as worker command time.

## Records and reports

If the user approves trusted coordinator builds, use `check --host-toolchain`
for build/test commands only. This bypasses the OS build sandbox, not worker
isolation. Keep the independent source copies, private build output, and
measurement receipts. Record this execution mode in the comparison limits.
Do not present host builds as fully sandboxed execution.

Every attempt gets a unique session id and receipt. `import-usage` can
re-import a recovered final receipt using its attempt id; an identical receipt
does not increase cost, and a changed receipt is rejected. It does not merge
cumulative receipts from resumed sessions.

Reports count recorded internal cost and gross input from `modelMetrics`.
Do not add cache categories to gross input. The cost unit is nano-AIU divided
by one billion, matching the prior cost report. Do not use premium requests
as a dollar amount. Missing receipts are reported explicitly.
Tables and charts label known subtotals as partial when receipts or metrics
are missing. A missing command duration is unavailable, not an inferred zero.

Round floating-point per-model cost fields to the nearest nano-AIU for
reconciliation. The CLI's integer total is authoritative; allow at most one
nano-AIU of rounding difference per model, not an arbitrary cost tolerance.

Code snapshots retain content-addressed blobs. Final changes compare the
prepared baseline to acceptance. Churn compares snapshot boundaries. Line
counts include blank and comment lines; generated files, tests, configuration,
documentation, and binary assets are separate.

Workers must be idle before feedback, evidence, or acceptance. A process killed
outside the launcher can leave a running marker. Use `recover RUN --attempt ID`
only after its recorded parent and worker processes have exited. Recovery refuses
live processes or a remaining child session, retains the interrupted attempt,
and imports a final receipt if available. Missing usage/duration is unavailable.
It never kills a process or deletes a measurement.

The report includes limits: coordinator cost, human review, setup, external
device time, and integration are not automatically metered. Do not claim total
exercise cost from the two implementation totals alone.

Coordinator cost is now read automatically by snapshots after `track-coordinator`
is configured. Missing configuration/data is unavailable, not zero. This adapter
selects numeric usage fields only, never conversation text, and opens SQLite
read-only. Human time and external device-check time are still not automatically
measured. Configure the session/start boundary before a new comparison starts.
An event with a null cost makes coordinator cost partial. If no event has a
known cost, the cost total is null and the report says unavailable. Known
coordinator cost stays separate from each architecture's subtotal.

`snapshot` is an interim report, not the accepted final report. It refuses active
attempts and existing labels, preserves a ledger backup/source archives/receipts,
and hashes the files. It does not reset counters or create a signoff.
Use technical and ux feedback kinds separately; legacy defect rows remain technical.
Worker launch output now shows actual file-tool activity rather than remaining
silent until completion. An emulator service is not an active implementation.

New snapshots record attempt IDs, IDs with imported model receipts, and a cutoff.
With `--compare-to`, `increment` contains only new attempts in the cutoff interval.
`receipt_reconciliation` contains receipts imported for older attempts since the
earlier snapshot. Those receipts increase the cumulative known subtotal, but are
not new interval effort. Both fields retain per-group missing-metric flags.
Earlier snapshots and receipts are not rewritten. If an older snapshot lacks
attempt IDs, receipt state, or a usable cutoff, interval attribution is
unavailable (null values with an explicit reason), not a cumulative subtraction
or an invented zero. Cumulative current totals remain available with their limits.

Keep raw records private. Acceptance is not permission to publish, commit,
push, overwrite a personal app, or discard the independently produced source.
