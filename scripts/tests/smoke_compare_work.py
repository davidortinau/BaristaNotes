#!/usr/bin/env python3
"""Exercise the actual comparison commands, optionally with live Copilot workers.

All source, evidence, and signoff in this exercise are explicitly self-test data.
No real application is built, deployed, modified, or accepted.
"""

import argparse
import json
from pathlib import Path
import subprocess
import sys
import uuid

from test_compare_work import ComparisonTests


COMMAND = Path(__file__).parents[1] / "compare_work.py"


def cli(*args):
    process = subprocess.run([sys.executable, str(COMMAND), *map(str, args)],
                             capture_output=True, text=True)
    if process.returncode:
        raise RuntimeError(process.stderr + process.stdout)
    print(process.stdout.strip())
    return process.stdout.strip()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True)
    parser.add_argument("--model", required=True)
    parser.add_argument("--reasoning-effort", required=True)
    parser.add_argument("--live", action="store_true")
    args = parser.parse_args()
    fixture = ComparisonTests()
    fixture.setUp()
    try:
        destination = Path(args.output).resolve() / ("self-test-" + uuid.uuid4().hex[:8])
        destination.mkdir(parents=True)
        # Keep a durable fixture copy; never overwrite an earlier test.
        import shutil
        shutil.copytree(fixture.repo, destination / "baseline")
        scope = fixture.scope
        scope["model"] = args.model
        scope["reasoning_effort"] = args.reasoning_effort
        scope["scope"] = ("Infrastructure self-test only. Create comparison-probe.txt containing "
                          "the word READY. Do not change any other file. Do not build, deploy, "
                          "or operate devices. This is not a product feature.")
        scope_file = destination / "scope.json"
        scope_file.write_text(json.dumps(scope, indent=2))
        run = Path(cli("prepare", scope_file, "--repo", destination / "baseline",
                       "--root", destination / "runs"))
        cli("doctor", run)
        status = json.loads(cli("status", run))
        assert all(item["metrics"]["internal_nano_aiu"] == 0 for item in status.values())
        for group in ("maui", "native"):
            if args.live:
                cli("work", run, "--group", group)
                created = run / "workers" / group / "workspace/comparison-probe.txt"
                assert created.read_text().strip() == "READY", created
            cli("check", run, "--group", group, "--", "/usr/bin/true")
        feedback = destination / "feedback.txt"
        feedback.write_text("Self-test feedback: keep READY; no additional feature work.")
        cli("feedback", run, "--group", "both", "--file", feedback, "--kind", "clarification")
        if args.live:
            cli("work", run, "--group", "native", "--phase", "correction")
            cli("work", run, "--group", "maui", "--phase", "review")
        cli("checkpoint", run)
        proof = destination / "proof.txt"
        proof.write_text("Self-test command exercise only. No application UI acceptance claimed.")
        signoff = destination / "signoff.txt"
        signoff.write_text("Self-test signoff only. This is not David's product acceptance.")
        for group in ("maui", "native"):
            for target in ("ios", "android"):
                cli("evidence", run, "--group", group, "--platform", target,
                    "--criterion", "journey", "--result", "pass", "--file", proof)
            cli("accept", run, "--group", group, "--signed-by", "self-test", "--file", signoff)
        cli("report", run)
        totals = json.loads((run / "report/totals.json").read_text())
        assert totals["run"]["scope"]["purpose"] == "self-test"
        import xml.etree.ElementTree as ET
        chart = ET.parse(run / "report/cost.svg")
        assert chart.getroot().tag.endswith("svg")
        if args.live:
            assert all(item["metrics"]["internal_nano_aiu"] > 0 for item in totals["groups"].values())
            assert all(not item["unavailable_attempts"] for item in totals["groups"].values())
        print("Actual command lifecycle: PASS. Records:", run)
    finally:
        fixture.doCleanups()


if __name__ == "__main__":
    main()
