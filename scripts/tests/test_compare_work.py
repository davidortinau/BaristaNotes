import argparse
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import platform
import sqlite3
import subprocess
import tempfile
import unittest
from unittest.mock import patch


spec = importlib.util.spec_from_file_location("compare_work", Path(__file__).parents[1] / "compare_work.py")
cw = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cw)


def args(**values):
    return argparse.Namespace(**values)


def receipt(nano=1000000000):
    return {
        "totalNanoAiu": nano, "totalApiDurationMs": 100,
        "modelMetrics": {"test-model": {"totalNanoAiu": nano, "requests": {"count": 1},
                                      "usage": {"inputTokens": 100, "outputTokens": 10,
                                                "cacheReadTokens": 60, "cacheWriteTokens": 30,
                                                "reasoningTokens": 2}}}
    }


class ComparisonTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.repo = self.root / "repo"
        self.repo.mkdir()
        files = {
            "src/BaristaNotes/Screen.cs": "class MauiScreen {}\n",
            "src/BaristaNotes.iOS/Screen.cs": "class IosScreen {}\n",
            "src/BaristaNotes.Android/Screen.cs": "class AndroidScreen {}\n",
            "src/BaristaNotes.Core/Logic.cs": "class Logic {}\n",
            "src/BaristaNotes.Tests/Tests.cs": "class Tests {}\n",
            "src/BaristaNotes/Resources/Fonts/test.txt": "font fixture\n",
            "src/BaristaNotes/appsettings.json": "{}\n",
        }
        for name, text in files.items():
            p = self.repo / name
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_text(text)
        subprocess.run(["git", "init", "-q", str(self.repo)], check=True)
        subprocess.run(["git", "-C", str(self.repo), "add", "."], check=True)
        subprocess.run(["git", "-C", str(self.repo), "-c", "user.name=Self test",
                        "-c", "user.email=self-test@example.invalid", "-c", "commit.gpgsign=false",
                        "commit", "-qm", "Fixture baseline"], check=True)
        self.scope = {
            "schema_version": 1, "purpose": "self-test", "title": "Tool fixture",
            "scope": "Exercise the measurement tool only.", "approved_by": "self-test",
            "model": "test-model", "reasoning_effort": "high",
            "criteria": [{"id": "journey", "text": "Fixture exercised",
                          "platforms": ["ios", "android"]}]
        }
        self.scope_file = self.root / "scope.json"
        cw.write_json(self.scope_file, self.scope)
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            cw.prepare(args(repo=str(self.repo), scope=str(self.scope_file),
                            baseline="HEAD", root=str(self.root / "runs")))
        self.run = cw.Run(output.getvalue().strip())
        self.addCleanup(self.run.db.close)
        self.addCleanup(self.temp.cleanup)

    def attempt(self, group="maui", attempt_id="test-attempt"):
        with self.run.db:
            self.run.db.execute("INSERT INTO attempts VALUES(?,?,?,?,?,?,?,?,?,?,?,?)",
                                (attempt_id, group, 1, "implementation", attempt_id, cw.now(), cw.now(),
                                 200, 0, "complete", None, None))
        return attempt_id

    def proof(self, group, result="pass"):
        file = self.root / "proof.txt"
        file.write_text("Self-test evidence, not application acceptance.\n")
        for target in cw.PLATFORMS:
            cw.evidence(args(run=str(self.run.path), group=group, platform=target, criterion="journey",
                             result=result, file=str(file)))

    def signoff(self, group):
        file = self.root / "signoff.txt"
        file.write_text("Self-test signoff only, not user acceptance of an application.\n")
        cw.accept(args(run=str(self.run.path), group=group, signed_by="self-test", file=str(file)))

    def test_exports_exclude_other_implementation_and_history(self):
        maui = self.run.workspace("maui")
        native = self.run.workspace("native")
        self.assertTrue((maui / "src/BaristaNotes/Screen.cs").exists())
        self.assertFalse((maui / "src/BaristaNotes.iOS").exists())
        self.assertTrue((native / "src/BaristaNotes.iOS/Screen.cs").exists())
        self.assertFalse((native / "src/BaristaNotes/Screen.cs").exists())
        self.assertTrue((native / "src/BaristaNotes/Resources/Fonts/test.txt").exists())
        self.assertFalse((maui / ".git").exists())
        self.assertEqual(cw.aggregate(self.run, "maui")["metrics"]["internal_nano_aiu"], 0)

    def test_usage_reimport_does_not_double_count_cache(self):
        attempt = self.attempt()
        file = self.root / "usage.json"
        cw.write_json(file, receipt())
        cw.usage_import(self.run, attempt, file)
        cw.usage_import(self.run, attempt, file)
        values = cw.aggregate(self.run, "maui")["metrics"]
        self.assertEqual(values["internal_nano_aiu"], 1000000000)
        self.assertEqual(values["input_tokens"], 100)
        self.assertEqual(values["cache_read_tokens"], 60)
        cw.write_json(file, receipt(2000000000))
        with self.assertRaisesRegex(ValueError, "changed"):
            cw.usage_import(self.run, attempt, file)

    def test_missing_receipt_is_unavailable(self):
        self.attempt()
        self.assertEqual(cw.aggregate(self.run, "maui")["unavailable_attempts"], ["test-attempt"])
        self.assertIn("input_tokens", cw.aggregate(self.run, "maui")["unavailable_metrics"])

    def test_float_model_cost_rounds_to_recorded_nano_units(self):
        identifier = self.attempt()
        value = receipt(2081470000)
        value["modelMetrics"]["test-model"]["totalNanoAiu"] = 2081469999.9999998
        file = self.root / "floating-cost.json"
        cw.write_json(file, value)
        cw.usage_import(self.run, identifier, file)
        totals = cw.aggregate(self.run, "maui")
        self.assertEqual(totals["metrics"]["internal_nano_aiu"], 2081470000)
        self.assertEqual(totals["by_model_nano_aiu"]["test-model"], 2081470000)

    def test_native_export_excludes_maui_style_implementation(self):
        self.assertFalse(cw.source_allowed("src/BaristaNotes/Resources/Styles/ApplicationTheme.cs", "native"))
        self.assertTrue(cw.source_allowed("src/BaristaNotes/Resources/Styles/AppColors.cs", "native"))

    def test_missing_reasoning_does_not_mark_known_cost_missing(self):
        identifier = self.attempt()
        data = receipt()
        del data["modelMetrics"]["test-model"]["usage"]["reasoningTokens"]
        file = self.root / "usage.json"
        cw.write_json(file, data)
        cw.usage_import(self.run, identifier, file)
        value = cw.aggregate(self.run, "maui")
        self.assertEqual(value["unavailable_attempts"], [])
        self.assertEqual(value["unavailable_metrics"], ["reasoning_tokens"])

    def test_scope_addition_updates_criteria_for_both_groups(self):
        scope = dict(self.scope)
        scope["criteria"] = self.scope["criteria"] + [
            {"id": "added", "text": "Added acceptance", "platforms": ["ios", "android"]}]
        file = self.root / "addition.json"
        cw.write_json(file, scope)
        cw.feedback(args(run=str(self.run.path), group="both", kind="scope-addition", file=str(file)))
        updated = cw.Run(self.run.path)
        self.addCleanup(updated.close)
        self.assertEqual(updated.manifest["scope_version"], 2)
        self.assertEqual(len(updated.manifest["scope"]["criteria"]), 2)
        self.proof("maui")
        with self.assertRaisesRegex(ValueError, "added"):
            self.signoff("maui")

    def test_recovery_refuses_a_live_launcher(self):
        import os
        self.attempt()
        with self.run.db:
            self.run.db.execute("UPDATE attempts SET status='running'")
            self.run.db.execute("INSERT INTO processes VALUES(?,?,NULL)", ("test-attempt", os.getpid()))
        with self.assertRaisesRegex(ValueError, "still alive"):
            cw.recover(args(run=str(self.run.path), attempt="test-attempt"))

    def test_recovery_retains_unknown_cost_and_attempt(self):
        self.attempt()
        with self.run.db:
            self.run.db.execute("UPDATE attempts SET status='running'")
            self.run.db.execute("INSERT INTO processes VALUES(?,?,NULL)", ("test-attempt", 2147483647))
        cw.recover(args(run=str(self.run.path), attempt="test-attempt"))
        self.assertEqual(self.run.db.execute("SELECT status FROM attempts").fetchone()[0], "interrupted")
        self.assertEqual(cw.aggregate(self.run, "maui")["unavailable_attempts"], ["test-attempt"])

    def test_read_only_review_rejects_write_tools(self):
        file = self.root / "events.jsonl"
        file.write_text(json.dumps({"type": "session.usage_checkpoint", "data": {
            "promptCacheBreakState": [{"models": {"m": {"tools": [{"name": "edit"}]}}}]}}) + "\n")
        with self.assertRaisesRegex(ValueError, "unauthorized"):
            cw.audit_tools(file, cw.READ_TOOLS)

    def test_feedback_retains_cost_and_requires_current_round_evidence(self):
        attempt = self.attempt()
        file = self.root / "usage.json"
        cw.write_json(file, receipt())
        cw.usage_import(self.run, attempt, file)
        self.proof("maui")
        feedback = self.root / "feedback.txt"
        feedback.write_text("Approved fixture feedback.")
        cw.feedback(args(run=str(self.run.path), group="maui", kind="defect", file=str(feedback)))
        self.assertEqual(self.run.round("maui"), 2)
        self.assertEqual(cw.aggregate(self.run, "maui")["metrics"]["internal_nano_aiu"], 1000000000)
        with self.assertRaisesRegex(ValueError, "Missing current PASS"):
            self.signoff("maui")

    def test_scope_additions_must_go_to_both_groups(self):
        file = self.root / "feedback.txt"
        file.write_text("Another requirement.")
        with self.assertRaisesRegex(ValueError, "both groups"):
            cw.feedback(args(run=str(self.run.path), group="native", kind="scope-addition", file=str(file)))

    def test_changed_source_invalidates_evidence(self):
        self.proof("maui")
        (self.run.workspace("maui") / "src/BaristaNotes.Core/Logic.cs").write_text("class Changed {}\n")
        with self.assertRaisesRegex(ValueError, "Missing current PASS"):
            self.signoff("maui")

    def test_failed_latest_evidence_prevents_acceptance(self):
        self.proof("maui", "pass")
        self.proof("maui", "fail")
        with self.assertRaisesRegex(ValueError, "Missing current PASS"):
            self.signoff("maui")

    def test_report_requires_both_signoffs_and_rejects_later_edits(self):
        with self.assertRaisesRegex(ValueError, "not been accepted"):
            cw.report(args(run=str(self.run.path)))
        for group in cw.GROUPS:
            self.proof(group)
            self.signoff(group)
        cw.report(args(run=str(self.run.path)))
        self.assertTrue((self.run.path / "report/cost.svg").exists())
        totals = json.loads((self.run.path / "report/totals.json").read_text())
        self.assertEqual(totals["run"]["scope"]["purpose"], "self-test")
        (self.run.workspace("native") / "src/BaristaNotes.iOS/Screen.cs").write_text("class Changed {}\n")
        with self.assertRaisesRegex(ValueError, "changed after acceptance"):
            cw.report(args(run=str(self.run.path)))

    def test_snapshot_churn_is_separate_from_final_change(self):
        p = self.run.workspace("maui") / "src/BaristaNotes.Core/Logic.cs"
        before = p.read_text()
        p.write_text("class Changed {}\n")
        self.run.snapshot("maui")
        p.write_text(before)
        self.run.snapshot("maui")
        value = cw.aggregate(self.run, "maui")
        self.assertEqual(value["final_change"], {})
        self.assertEqual(value["snapshot_churn"]["product"]["added"], 2)
        self.assertEqual(value["snapshot_churn"]["product"]["removed"], 2)

    def test_running_marker_blocks_duplicate_workers(self):
        self.attempt()
        self.run.db.execute("UPDATE attempts SET status='running'")
        self.run.db.commit()
        with self.assertRaisesRegex(ValueError, "active command"):
            self.run.mutable("maui")

    def test_symlink_refused(self):
        (self.run.workspace("maui") / "leak.cs").symlink_to(self.repo / "src/BaristaNotes.iOS/Screen.cs")
        with self.assertRaisesRegex(ValueError, "symlink"):
            self.run.snapshot("maui")

    def test_prepare_refuses_dirty_application_source(self):
        (self.repo / "src/BaristaNotes/Screen.cs").write_text("Uncommitted application work.\n")
        with self.assertRaisesRegex(ValueError, "application-source changes"):
            cw.prepare(args(repo=str(self.repo), scope=str(self.scope_file),
                            baseline="HEAD", root=str(self.root / "second-runs")))

    def test_prepare_ignores_unrelated_documentation(self):
        (self.repo / "README.md").write_text("Untracked documentation, not exported.\n")
        with contextlib.redirect_stdout(io.StringIO()):
            cw.prepare(args(repo=str(self.repo), scope=str(self.scope_file),
                            baseline="HEAD", root=str(self.root / "second-runs")))

    def test_tool_catalog_fails_closed(self):
        file = self.root / "events.jsonl"
        file.write_text(json.dumps({"type": "session.usage_checkpoint", "data": {
            "promptCacheBreakState": [{"models": {"m": {"tools": [{"name": "bash"}]}}}]}}) + "\n")
        with self.assertRaisesRegex(ValueError, "unauthorized"):
            cw.audit_tools(file)
        file.write_text("{}\n")
        with self.assertRaises((ValueError, KeyError)):
            cw.audit_tools(file)

    @unittest.skipUnless(platform.system() == "Darwin", "macOS isolation check")
    def test_actual_os_denies_peer_and_coordinator_reads(self):
        for group in cw.GROUPS:
            cw.isolation_check(self.run, group)

    def test_check_command_is_registered_and_executes(self):
        with patch.object(cw, "isolation_check", return_value=self.run.home("maui") / "worker.sb"):
            # On macOS use the real profile; the mock avoids printing its doctor receipt.
            policy = self.run.home("maui") / "worker.sb"
            policy.write_text(cw.seatbelt(self.run.home("maui"), self.repo))
            if platform.system() != "Darwin":
                self.skipTest("macOS check command")
            cw.execute(args(command="check", run=str(self.run.path), group="maui", timeout=20,
                            argv=["/usr/bin/true"]))
        row = self.run.db.execute("SELECT * FROM attempts").fetchone()
        self.assertEqual(row["status"], "complete")
        self.assertEqual(row["phase"], "check")

    @unittest.skipUnless(platform.system() == "Darwin", "macOS coordinator check")
    def test_host_check_keeps_worker_isolation_and_records_mode(self):
        cw.execute(args(command="check", run=str(self.run.path), group="maui",
                        argv=["/usr/bin/true"], host_toolchain=True))
        row = self.run.db.execute("SELECT * FROM attempts").fetchone()
        mode = json.loads((self.run.path / "attempts" / row["id"] / "execution.json").read_text())
        self.assertEqual(mode["mode"], "coordinator-host-build")
        self.assertEqual(row["status"], "complete")
        cw.isolation_check(self.run, "maui")

    def test_interim_snapshot_is_immutable_and_does_not_accept(self):
        cw.snapshot(args(run=str(self.run.path), label="pre-ux", compare_to=None))
        folder = self.run.path / "checkpoints/pre-ux"
        self.assertTrue((folder / "snapshot.md").exists())
        self.assertTrue((folder / "maui-source.zip").exists())
        self.assertEqual(self.run.db.execute("SELECT COUNT(*) FROM acceptance").fetchone()[0], 0)
        with self.assertRaisesRegex(ValueError, "immutable"):
            cw.snapshot(args(run=str(self.run.path), label="pre-ux", compare_to=None))

    def test_snapshot_rejects_running_attempt(self):
        self.attempt()
        with self.run.db:
            self.run.db.execute("UPDATE attempts SET status='running'")
        with self.assertRaisesRegex(ValueError, "finish"):
            cw.snapshot(args(run=str(self.run.path), label="pre-ux", compare_to=None))
        self.assertFalse((self.run.path / "checkpoints/pre-ux").exists())

    def test_incremental_snapshot_uses_frozen_worker_totals(self):
        cw.snapshot(args(run=str(self.run.path), label="before", compare_to=None))
        identifier = self.attempt()
        file = self.root / "receipt.json"
        cw.write_json(file, receipt())
        cw.usage_import(self.run, identifier, file)
        cw.snapshot(args(run=str(self.run.path), label="after", compare_to="before"))
        value = json.loads((self.run.path / "checkpoints/after/totals.json").read_text())
        self.assertEqual(value["increment"]["maui"]["internal_nano_aiu"], 1000000000)
        self.assertEqual(value["increment"]["native"]["internal_nano_aiu"], 0)

    def test_ux_and_technical_feedback_have_distinct_kinds(self):
        file = self.root / "feedback.txt"
        file.write_text("Test feedback.")
        for kind in ("ux", "technical"):
            cw.feedback(args(run=str(self.run.path), group="maui", kind=kind, file=str(file)))
        self.assertEqual([r[0] for r in self.run.db.execute("SELECT kind FROM feedback ORDER BY id")],
                         ["ux", "technical"])

    def test_coordinator_adapter_reads_numeric_totals_without_mutation(self):
        file = self.root / "usage.sqlite3"
        with sqlite3.connect(file) as db:
            db.execute("CREATE TABLE assistant_usage_events (id INTEGER PRIMARY KEY, session_id TEXT, "
                       "agent_id TEXT, created_at TEXT, model TEXT, reasoning_effort TEXT, total_nano_aiu INTEGER, "
                       "duration_ms INTEGER,input_tokens INTEGER,output_tokens INTEGER,cache_read_tokens INTEGER,"
                       "cache_write_tokens INTEGER,reasoning_tokens INTEGER)")
            db.execute("INSERT INTO assistant_usage_events VALUES(1,'coordinator',NULL,"
                       "'2020-01-02T00:00:00Z','test','high',2000000000,100,10,2,5,3,0)")
        before = file.read_bytes()
        cw.track_coordinator(args(run=str(self.run.path), database=str(file),
                                  session="coordinator", start="2020-01-01T00:00:00Z"))
        value = cw.coordinator_cost(self.run, "2020-01-03T00:00:00Z")
        self.assertEqual(value["internal_nano_aiu"], 2000000000)
        self.assertEqual(file.read_bytes(), before)
        self.assertEqual(cw.coordinator_cost(self.run, "2020-01-01T01:00:00Z")["status"], "no-records")


if __name__ == "__main__":
    unittest.main()
