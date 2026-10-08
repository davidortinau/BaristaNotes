#!/usr/bin/env python3
"""Local, independent MAUI/native work records. Python standard library only."""

import argparse
import datetime as dt
import difflib
import hashlib
import io
import json
import os
from pathlib import Path
import platform
import re
import shutil
import sqlite3
import subprocess
import sys
import tarfile
import time
import uuid
import weakref
import zipfile
import xml.etree.ElementTree as ET


GROUPS = ("maui", "native")
PLATFORMS = ("ios", "android")
TOOLS = ("view", "create", "edit", "apply_patch", "grep", "rg", "glob")
READ_TOOLS = ("view", "grep", "rg", "glob")
IGNORED = {".git", "bin", "obj", "__pycache__", ".copilot", "packages"}
TEXT_SUFFIXES = {".cs", ".csproj", ".props", ".targets", ".xml", ".json", ".md",
                 ".xaml", ".plist", ".txt", ".svg", ".sln", ".slnx", ".sh", ".py"}
SCHEMA = """
CREATE TABLE metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE snapshots (id INTEGER PRIMARY KEY, work_group TEXT NOT NULL,
  created_at TEXT NOT NULL, digest TEXT NOT NULL, files TEXT NOT NULL);
CREATE TABLE attempts (id TEXT PRIMARY KEY, work_group TEXT NOT NULL,
  round INTEGER NOT NULL, phase TEXT NOT NULL, session_id TEXT NOT NULL UNIQUE,
  started_at TEXT NOT NULL, ended_at TEXT, duration_ms INTEGER, exit_code INTEGER,
  status TEXT NOT NULL, usage TEXT, usage_hash TEXT);
CREATE TABLE processes (attempt_id TEXT PRIMARY KEY, launcher_pid INTEGER NOT NULL, worker_pid INTEGER);
CREATE TABLE feedback (id INTEGER PRIMARY KEY, target TEXT NOT NULL,
  kind TEXT NOT NULL, round INTEGER NOT NULL, created_at TEXT NOT NULL, text TEXT NOT NULL);
CREATE TABLE evidence (id INTEGER PRIMARY KEY, work_group TEXT NOT NULL,
  platform TEXT NOT NULL, criterion TEXT NOT NULL, snapshot_id INTEGER NOT NULL,
  round INTEGER NOT NULL, created_at TEXT NOT NULL, result TEXT NOT NULL,
  file TEXT NOT NULL, digest TEXT NOT NULL);
CREATE TABLE acceptance (work_group TEXT PRIMARY KEY, snapshot_id INTEGER NOT NULL,
  round INTEGER NOT NULL, created_at TEXT NOT NULL, signed_by TEXT NOT NULL,
  signoff TEXT NOT NULL);
"""


def now():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def git(repo, *args):
    return subprocess.run(["git", "-C", str(repo), *args], check=True,
                          capture_output=True).stdout


def validate_scope(scope):
    require(scope.get("schema_version") == 1, "Unsupported scope schema.")
    require(scope.get("purpose", "comparison") in ("comparison", "self-test"),
            "Invalid purpose.")
    for key in ("title", "scope", "approved_by", "model", "reasoning_effort"):
        require(isinstance(scope.get(key), str) and scope[key].strip(),
                f"Scope requires {key}.")
    require(scope["model"] != "auto", "Use an explicit model for a controlled comparison.")
    require(scope["reasoning_effort"] in ("none", "minimal", "low", "medium", "high", "xhigh", "max"),
            "Invalid reasoning effort.")
    criteria = scope.get("criteria")
    require(isinstance(criteria, list) and criteria, "At least one criterion is required.")
    ids = []
    for criterion in criteria:
        require(isinstance(criterion, dict) and criterion.get("id") and criterion.get("text"),
                "Each criterion needs an id and text.")
        require(criterion.get("platforms") == list(PLATFORMS),
                "The pilot requires iOS and Android for every criterion.")
        ids.append(criterion["id"])
    require(len(ids) == len(set(ids)), "Criterion ids must be unique.")


def source_allowed(name, group):
    common = ("src/BaristaNotes.Core/", "src/BaristaNotes.Tests/")
    if name.startswith(common):
        if group == "maui" and Path(name).name in (
                "UiAnimationLifetimeTests.cs", "NativeSpeechPermissionTests.cs"):
            return False
        return True
    if group == "maui":
        return name.startswith("src/BaristaNotes/")
    if name.startswith("src/BaristaNotes/Resources/") and name.endswith(".cs"):
        return name == "src/BaristaNotes/Resources/Styles/AppColors.cs"
    return (name.startswith(("src/BaristaNotes.iOS/", "src/BaristaNotes.Android/",
                             "src/BaristaNotes.iOS.Ailoha/", "src/BaristaNotes.Android.Ailoha/",
                             "src/BaristaNotes/Resources/", "src/BaristaNotes/Platforms/Android/Resources/"))
            or name == "src/BaristaNotes/appsettings.json")


def export_source(repo, revision, destination, group):
    archive = git(repo, "archive", "--format=tar", revision)
    count = 0
    with tarfile.open(fileobj=io.BytesIO(archive)) as tar:
        for entry in tar:
            name = entry.name
            if not source_allowed(name, group):
                continue
            require(not entry.issym() and not entry.islnk(), f"Source link refused: {name}")
            if not entry.isfile():
                continue
            parts = Path(name).parts
            require(not Path(name).is_absolute() and ".." not in parts, "Unsafe archive path.")
            require(not any(part in IGNORED for part in parts), f"Generated output tracked: {name}")
            require("appsettings.Development.json" not in parts, "Development credentials tracked.")
            path = destination / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(tar.extractfile(entry).read())
            path.chmod(0o700 if entry.mode & 0o111 else 0o600)
            count += 1
    require(count, f"No {group} source at {revision}.")
    if group == "maui":
        project = destination / "src/BaristaNotes.Tests/BaristaNotes.Tests.csproj"
        if project.exists():
            tree = ET.parse(project)
            for item_group in tree.getroot().findall("ItemGroup"):
                for node in list(item_group):
                    include = node.get("Include", "").replace("\\", "/")
                    if "../BaristaNotes.Android/" in include or "../BaristaNotes.iOS/" in include:
                        item_group.remove(node)
            tree.write(project, encoding="utf-8", xml_declaration=False)
    return count


def category(name):
    path = Path(name)
    if (path.name.endswith((".g.cs", ".generated.cs", ".Designer.cs"))
            or "CompiledModels" in path.parts or "Generated" in path.parts):
        return "generated"
    if "BaristaNotes.Tests" in path.parts:
        return "tests"
    if path.suffix == ".cs":
        return "product"
    if path.suffix in {".csproj", ".props", ".targets", ".json", ".plist", ".xml"}:
        return "configuration"
    if path.suffix in {".md", ".txt"}:
        return "documentation"
    return "resources"


def capture_tree(workspace, blobs):
    files = {}
    for path in sorted(workspace.rglob("*")):
        relative = path.relative_to(workspace)
        if any(part in IGNORED for part in relative.parts):
            continue
        require(not path.is_symlink(), f"Source symlink refused: {relative}")
        if not path.is_file():
            continue
        data = path.read_bytes()
        sha = digest(data)
        blob = blobs / sha
        if not blob.exists():
            blob.write_bytes(data)
        files[relative.as_posix()] = {
            "sha256": sha, "bytes": len(data), "category": category(relative),
            "text": path.suffix in TEXT_SUFFIXES and b"\x00" not in data,
        }
    sha = digest(json.dumps(files, sort_keys=True).encode())
    return sha, files


def changes(old, new, blobs):
    totals = {}
    for name in sorted(old.keys() | new.keys()):
        before, after = old.get(name), new.get(name)
        if before == after:
            continue
        kind = (after or before)["category"]
        bucket = totals.setdefault(kind, {"added": 0, "removed": 0, "files": 0, "binary_files": 0})
        bucket["files"] += 1
        if not all(item["text"] for item in (before, after) if item):
            bucket["binary_files"] += 1
            continue
        left = (blobs / before["sha256"]).read_bytes().decode("utf-8", errors="replace").splitlines() if before else []
        right = (blobs / after["sha256"]).read_bytes().decode("utf-8", errors="replace").splitlines() if after else []
        for operation, a, b, c, d in difflib.SequenceMatcher(None, left, right, autojunk=False).get_opcodes():
            if operation != "equal":
                bucket["removed"] += b - a
                bucket["added"] += d - c
    return totals


class Run:
    def __init__(self, path):
        self.path = Path(path).resolve()
        require((self.path / "manifest.json").is_file(), "Run not found.")
        self.manifest = json.loads((self.path / "manifest.json").read_text())
        self.db = sqlite3.connect(self.path / "measurements.sqlite3", timeout=20)
        self.db.row_factory = sqlite3.Row
        self._finalizer = weakref.finalize(self, self.db.close)
        current = self.db.execute("SELECT value FROM metadata WHERE key='scope.current'").fetchone()
        if current:
            self.manifest["original_scope"] = self.manifest["scope"]
            self.manifest["scope"] = json.loads(current[0])
        self.manifest["scope_version"] = 1 + self.db.execute(
            "SELECT COUNT(*) FROM feedback WHERE kind='scope-addition'").fetchone()[0]

    def close(self):
        self._finalizer()

    def workspace(self, group):
        return self.path / "workers" / group / "workspace"

    def home(self, group):
        return self.path / "workers" / group

    def round(self, group):
        return int(self.db.execute("SELECT value FROM metadata WHERE key=?", ("round." + group,)).fetchone()[0])

    def latest(self, group):
        return self.db.execute("SELECT * FROM snapshots WHERE work_group=? ORDER BY id DESC LIMIT 1",
                               (group,)).fetchone()

    def mutable(self, group):
        require(not self.db.execute("SELECT 1 FROM acceptance WHERE work_group=?", (group,)).fetchone(),
                f"{group} is accepted. Use a new run for further work.")
        require(not self.db.execute("SELECT 1 FROM attempts WHERE work_group=? AND status='running'",
                                    (group,)).fetchone(), f"{group} has an active command.")

    def snapshot(self, group):
        sha, files = capture_tree(self.workspace(group), self.path / "blobs")
        last = self.latest(group)
        if last and last["digest"] == sha:
            return last["id"]
        own_transaction = not self.db.in_transaction
        cursor = self.db.execute(
            "INSERT INTO snapshots(work_group,created_at,digest,files) VALUES(?,?,?,?)",
            (group, now(), sha, json.dumps(files, sort_keys=True)))
        if own_transaction:
            self.db.commit()
        return cursor.lastrowid


def prepare(args):
    repo = Path(args.repo).resolve()
    scope = json.loads(Path(args.scope).read_text())
    validate_scope(scope)
    modified = git(repo, "diff", "--name-only", "-z", "HEAD").decode().split("\0")
    untracked = git(repo, "ls-files", "--others", "--exclude-standard", "-z").decode().split("\0")
    dirty_source = [name for name in modified + untracked
                    if name and any(source_allowed(name, group) for group in GROUPS)]
    require(not dirty_source,
            "Resolve application-source changes before freezing a baseline: " + ", ".join(dirty_source))
    revision = git(repo, "rev-parse", "--verify", args.baseline + "^{commit}").decode().strip()
    root = Path(args.root).expanduser().resolve()
    require(not root.is_relative_to(repo), "Private run storage must be outside the repository.")
    root.mkdir(parents=True, exist_ok=True, mode=0o700)
    run_id = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ-") + uuid.uuid4().hex[:8]
    path = root / run_id
    path.mkdir(mode=0o700)
    manifest = {"schema_version": 1, "id": run_id, "created_at": now(),
                "baseline": revision, "repository": str(repo), "scope": scope,
                "isolation": "macOS Seatbelt; file tools only; independent CLI homes",
                "worker_tools": list(TOOLS), "concurrency_per_group": 1,
                "test_export_adjustments": "MAUI removes the two native-head compile links and their tests."}
    (path / "blobs").mkdir()
    (path / "attempts").mkdir()
    (path / "evidence").mkdir()
    db = sqlite3.connect(path / "measurements.sqlite3")
    db.executescript(SCHEMA)
    for group in GROUPS:
        home = path / "workers" / group
        workspace = home / "workspace"
        workspace.mkdir(parents=True)
        (home / "tmp").mkdir()
        (home / "cli").mkdir()
        write_json(home / "cli/config.json", {"memory": False, "autoUpdate": False})
        write_json(home / "cli/settings.json", {"sandbox": {"enabled": False, "allowBypass": False}})
        export_source(repo, revision, workspace, group)
        db.execute("INSERT INTO metadata VALUES(?,?)", ("round." + group, "1"))
    db.commit()
    db.close()
    write_json(path / "manifest.json", manifest)
    run = Run(path)
    for group in GROUPS:
        run.snapshot(group)
    run.close()
    print(path)


def seatbelt(home, source_repo, read_only=(), tool_cache_prefixes=()):
    home = Path(home).resolve()
    sources = {Path(source_repo).resolve(), Path.home().resolve(),
               Path("/Users"), Path("/Volumes"), Path("/private/tmp"), Path("/private/var/folders")}
    quote = lambda value: json.dumps(str(value))
    exceptions = f"(require-not (subpath {quote(home)}))"
    for path in read_only:
        path = Path(path).resolve()
        require(not home.is_relative_to(path) and not Path(source_repo).resolve().is_relative_to(path),
                "Read-only grant is too broad.")
        require(not path.is_relative_to(Path(source_repo).resolve()), "Cannot grant source-repository access.")
        exceptions += f" (require-not (subpath {quote(path)}))"
    cache_exceptions = ""
    for prefix in tool_cache_prefixes:
        cache_exceptions += f" (require-not (regex {quote('^' + re.escape(str(prefix)) + '(?:-[^/]+)?$')}))"
    rules = ["(version 1)", "(allow default)"]
    for source in sources:
        rules.append(f"(deny file-read-data (require-all (subpath {quote(source)}) {exceptions}{cache_exceptions}))")
    rules.append(f"(deny file-write* (require-all (require-not (subpath {quote(home)})) "
                 f'(require-not (literal "/dev/null")){cache_exceptions}))')
    return "\n".join(rules) + "\n"


def isolation_check(run, group):
    require(platform.system() == "Darwin" and shutil.which("sandbox-exec"),
            "Strict worker execution currently requires macOS sandbox-exec.")
    home = run.home(group)
    policy = home / "worker.sb"
    policy.write_text(seatbelt(home, run.manifest["repository"]))
    peer = run.workspace("native" if group == "maui" else "maui")
    peer_file = next(p for p in peer.rglob("*") if p.is_file())
    canary = run.path / "coordinator-canary.txt"
    if not canary.exists():
        canary.write_text("Coordinator records must not enter a worker context.\n")
    targets = [peer_file, canary, Path(run.manifest["repository"]) / ".git/HEAD",
               run.path / "measurements.sqlite3"]
    own = home / "isolation-canary.txt"
    own.write_text("Own source is readable.\n")
    for target in [own, *targets]:
        result = subprocess.run(["sandbox-exec", "-f", str(policy), "/bin/cat", str(target)],
                                cwd=home, capture_output=True)
        if target == own:
            require(result.returncode == 0, "Sandbox cannot read its own files.")
        else:
            require(result.returncode != 0 and b"Operation not permitted" in result.stderr,
                    f"Isolation denial failed: {target}")
    print(f"{group}: own read passed; peer, coordinator, history, and ledger reads denied.")
    return policy


def worker_env(home):
    node = shutil.which("node")
    require(node is not None, "Copilot's node launcher is missing.")
    env = {"PATH": str(Path(node).parent) + ":/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin",
           "HOME": str(home), "COPILOT_HOME": str(home / "cli"), "TMPDIR": str(home / "tmp"),
           "COPILOT_AUTO_UPDATE": "false", "NO_COLOR": "1"}
    token = os.environ.get("COPILOT_GITHUB_TOKEN") or os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")
    if not token:
        require(shutil.which("gh"), "Supply COPILOT_GITHUB_TOKEN or authenticate gh.")
        result = subprocess.run(["gh", "auth", "token"], capture_output=True, text=True)
        require(result.returncode == 0 and result.stdout.strip(), "No GitHub authentication available.")
        token = result.stdout.strip()
    env["COPILOT_GITHUB_TOKEN"] = token
    return env


def usage_import(run, attempt_id, usage_path):
    data = json.loads(Path(usage_path).read_text())
    require(isinstance(data.get("totalNanoAiu"), int) and data["totalNanoAiu"] >= 0,
            "CLI usage has no recorded internal cost.")
    require(isinstance(data.get("totalApiDurationMs"), (int, float)), "CLI usage has no model duration.")
    metrics = data.get("modelMetrics")
    require(isinstance(metrics, dict) and metrics, "CLI usage has no model metrics.")
    for value in metrics.values():
        require(isinstance(value.get("usage"), dict), "Model token record missing.")
        for field in ("inputTokens", "outputTokens", "cacheReadTokens", "cacheWriteTokens"):
            require(isinstance(value["usage"].get(field), (int, float)) and value["usage"][field] >= 0,
                    f"Missing/invalid token field: {field}")
    require(abs(sum(round(value.get("totalNanoAiu", -1)) for value in metrics.values()) -
                data["totalNanoAiu"]) <= len(metrics),
            "Model costs do not reconcile with CLI total.")
    sha = digest(json.dumps(data, sort_keys=True).encode())
    attempt = run.db.execute("SELECT * FROM attempts WHERE id=?", (attempt_id,)).fetchone()
    require(attempt and attempt["phase"] != "check", "Unknown model attempt.")
    require(not attempt["usage_hash"] or attempt["usage_hash"] == sha, "Usage receipt changed after import.")
    with run.db:
        run.db.execute("UPDATE attempts SET usage=?, usage_hash=? WHERE id=?",
                       (json.dumps(data, sort_keys=True), sha, attempt_id))


def audit_tools(path, allowed=TOOLS):
    observed = set()
    confirmed = False
    for line in path.read_text().splitlines():
        event = json.loads(line)
        data = event.get("data", {})
        if event["type"] == "tool.execution_start":
            observed.add(data.get("toolName"))
        if event["type"] == "session.usage_checkpoint":
            for conversation in data.get("promptCacheBreakState", []):
                for state in conversation.get("models", {}).values():
                    require(state.get("tools_truncated", 0) == 0, "Tool catalog is incomplete.")
                    tools = {item["name"] for item in state.get("tools", [])}
                    require(tools and tools <= set(allowed), "Worker has unauthorized tools.")
                    confirmed = True
    require(observed <= set(allowed), "An unauthorized tool ran.")
    require(confirmed, "CLI did not export its tool catalog. Cannot confirm restrictions.")


def prompt_for(run, group, phase):
    feedback = [dict(row) for row in run.db.execute(
        "SELECT kind, text FROM feedback WHERE target IN (?, 'both') ORDER BY id", (group,))]
    feedback = feedback[-1:]
    return (
        "Work only in this independent BaristaNotes source export. Do not read other "
        "implementations, use session history, run shell commands, start agents, commit, "
        "push, or install apps. File tools are the only permitted tools. "
        "The coordinator performs builds, tests, and device checks and supplies your own "
        "results. Do not claim runtime verification. Preserve user data and existing behavior. "
        "Use the source design; do not redesign it. Record concise implementation decisions "
        "in workspace notes only when needed. Report required build commands and runtime "
        "scenarios. For review, report bugs only; do not edit files.\n"
        f"Group: {group}. Phase: {phase}. Round: {run.round(group)}.\n"
        f"Approved scope:\n{json.dumps(run.manifest['scope'], indent=2)}\n"
        f"Latest applicable feedback (the current scope supersedes older exclusions):\n{json.dumps(feedback, indent=2)}\n"
        "Public SDK reference source, if supplied, is at ../inputs/sdk-reference and is "
        "an allowed read directory. Inspect the published API there before editing. "
        "MAUI must deliver on iOS and Android; native must deliver in both native heads. "
        "Within native, reuse its Core logic once. Both groups have independent Core copies."
    )


def execute(args):
    run = Run(args.run)
    group = args.group
    policy = isolation_check(run, group)
    require(args.command == "check" or shutil.which("copilot"), "Copilot CLI is missing.")
    run.db.execute("BEGIN IMMEDIATE")
    try:
        run.mutable(group)
        attempt_id, session_id = str(uuid.uuid4()), str(uuid.uuid4())
        run.db.execute("INSERT INTO attempts VALUES(?,?,?,?,?,?,?,?,?,?,?,?)",
                       (attempt_id, group, run.round(group), args.phase if args.command == "work" else "check",
                        session_id, now(), None, None, None, "running", None, None))
        run.db.execute("INSERT INTO processes VALUES(?,?,NULL)", (attempt_id, os.getpid()))
        run.db.commit()
    except Exception:
        run.db.rollback()
        raise
    directory = run.path / "attempts" / attempt_id
    directory.mkdir()
    home = run.home(group)
    usage = home / f"usage-{attempt_id}.json"
    output = home / f"output-{attempt_id}.jsonl"
    stderr = home / f"stderr-{attempt_id}.log"
    cli = args.command == "work"
    host_toolchain = not cli and getattr(args, "host_toolchain", False)
    exit_code = -1
    started = time.monotonic()
    try:
        if cli:
            environment = worker_env(home)
            allowed = READ_TOOLS if args.phase == "review" else TOOLS
            command = [shutil.which("copilot"), "--no-auto-update", "--no-custom-instructions",
                       "--disable-builtin-mcps", "--no-ask-user", "--no-remote", "--no-remote-export",
                       "--available-tools=" + ",".join(allowed), "--allow-all-tools", "--disallow-temp-dir",
                       "--secret-env-vars=COPILOT_GITHUB_TOKEN", "--output-format=json",
                       "--usage-output-file", str(usage), "--session-id", session_id,
                       "--model", run.manifest["scope"]["model"],
                       "--reasoning-effort", run.manifest["scope"]["reasoning_effort"],
                       "-p", prompt_for(run, group, args.phase)]
            sdk_reference = home / "inputs/sdk-reference"
            if sdk_reference.is_dir():
                command[1:1] = ["--add-dir", str(sdk_reference)]
        else:
            command = args.argv
            if command and command[0] == "--":
                command = command[1:]
            require(command, "Supply a check command after --.")
            environment = {"PATH": os.environ.get("PATH", "/usr/bin:/bin"),
                           "HOME": str(home), "TMPDIR": str(home / "tmp"),
                           "DOTNET_CLI_HOME": str(home), "NUGET_PACKAGES": str(home / "packages"),
                           "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_NOLOGO": "1",
                           "DOTNET_GENERATE_ASPNET_CERTIFICATE": "false",
                           "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE": "true",
                           "DOTNET_CLI_USE_MSBUILD_SERVER": "0",
                           "xcrun_nocache": "1"}
            read_only = [Path(shutil.which("dotnet")).resolve().parent] if shutil.which("dotnet") else []
            android_sdk = Path.home() / "Library/Android/sdk"
            if android_sdk.is_dir():
                read_only.append(android_sdk)
            policy = home / "check.sb"
            temp = Path(subprocess.check_output(["getconf", "DARWIN_USER_TEMP_DIR"], text=True).strip()).resolve()
            policy.write_text(seatbelt(home, run.manifest["repository"], read_only,
                                      tool_cache_prefixes=(temp / "xcrun_db",)))
        write_json(directory / "execution.json", {
            "mode": "isolated-ai-worker" if cli else "coordinator-host-build" if host_toolchain else "isolated-check",
            "work_group": group,
            "session_id": session_id,
            "started_at": now(),
            "worker_tools": list(allowed) if cli else [],
        })
        with output.open("w") as out, stderr.open("w") as err:
            invocation = command if host_toolchain else ["sandbox-exec", "-f", str(policy), *command]
            with subprocess.Popen(invocation, text=True,
                                  cwd=run.workspace(group), env=environment,
                                  stdout=subprocess.PIPE if cli else out, stderr=err) as process:
                with run.db:
                    run.db.execute("UPDATE processes SET worker_pid=? WHERE attempt_id=?",
                                   (process.pid, attempt_id))
                # Do not kill only the wrapper: it can leave an active child worker.
                # Copilot's own completion is the boundary; the caller can stop a stuck tree.
                print(f"{group}: {args.phase if cli else 'check'} started; attempt {attempt_id}", flush=True)
                if cli:
                    for line in process.stdout:
                        out.write(line)
                        out.flush()
                        try:
                            event = json.loads(line)
                        except json.JSONDecodeError:
                            print(f"{group}: unrecognized CLI output retained for audit", flush=True)
                            continue
                        if event.get("type") == "tool.execution_start":
                            print(f"{group}: {event.get('data', {}).get('toolName')} tool running", flush=True)
                exit_code = process.wait()
        if cli:
            if usage.exists():
                usage_import(run, attempt_id, usage)
            require(exit_code == 0, f"Worker failed ({exit_code}); see {stderr}.")
            audit_tools(output, allowed)
        else:
            require(exit_code == 0, f"Check failed ({exit_code}); see {stderr} and {output}.")
    finally:
        for file in (usage, output, stderr):
            if file.exists():
                shutil.copy2(file, directory / file.name)
        with run.db:
            run.db.execute("BEGIN IMMEDIATE")
            run.snapshot(group)
            run.db.execute("UPDATE attempts SET ended_at=?,duration_ms=?,exit_code=?,status=? WHERE id=?",
                           (now(), round((time.monotonic() - started) * 1000), exit_code,
                            "complete" if exit_code == 0 and sys.exc_info()[0] is None else "failed", attempt_id))
        print(f"Attempt: {attempt_id}. Records: {directory}")
        run.close()


def feedback(args):
    run = Run(args.run)
    text = Path(args.file).read_text().strip()
    require(text, "Feedback is empty.")
    targets = GROUPS if args.group == "both" else (args.group,)
    require(args.kind in ("defect", "technical", "ux") or args.group == "both",
            "Scope changes must go to both groups.")
    updated_scope = None
    if args.kind == "scope-addition":
        updated_scope = json.loads(text)
        validate_scope(updated_scope)
        for key in ("model", "reasoning_effort"):
            require(updated_scope[key] == run.manifest["scope"][key], "Keep execution settings unchanged.")
    with run.db:
        run.db.execute("BEGIN IMMEDIATE")
        for group in targets:
            run.mutable(group)
        round_number = max(run.round(group) for group in targets) + 1
        run.db.execute("INSERT INTO feedback(target,kind,round,created_at,text) VALUES(?,?,?,?,?)",
                       (args.group, args.kind, round_number, now(), text))
        if updated_scope is not None:
            run.db.execute("INSERT OR REPLACE INTO metadata VALUES('scope.current',?)",
                           (json.dumps(updated_scope, sort_keys=True),))
        for group in targets:
            run.db.execute("UPDATE metadata SET value=? WHERE key=?", (str(round_number), "round." + group))
    print(f"Feedback recorded for {args.group}, round {round_number}. Counters retained.")
    run.close()


def evidence(args):
    run = Run(args.run)
    ids = {item["id"] for item in run.manifest["scope"]["criteria"]}
    require(args.criterion in ids, "Unknown acceptance criterion.")
    source = Path(args.file).resolve()
    require(source.is_file(), "Evidence file does not exist.")
    require(source.stat().st_size > 0, "Evidence file is empty.")
    data = source.read_bytes()
    sha = digest(data)
    destination = run.path / "evidence" / (sha + source.suffix)
    if not destination.exists():
        destination.write_bytes(data)
    with run.db:
        run.db.execute("BEGIN IMMEDIATE")
        run.mutable(args.group)
        snapshot = run.snapshot(args.group)
        run.db.execute(
            "INSERT INTO evidence(work_group,platform,criterion,snapshot_id,round,created_at,result,file,digest) "
            "VALUES(?,?,?,?,?,?,?,?,?)",
            (args.group, args.platform, args.criterion, snapshot, run.round(args.group),
             now(), args.result, str(destination.relative_to(run.path)), sha))
    print(f"Evidence recorded: {args.group}/{args.platform}/{args.criterion} {args.result}.")
    run.close()


def accept(args):
    run = Run(args.run)
    run.db.execute("BEGIN IMMEDIATE")
    run.mutable(args.group)
    snapshot = run.snapshot(args.group)
    round_number = run.round(args.group)
    require(args.signed_by.strip(), "Signoff author is required.")
    signoff = Path(args.file).read_text().strip()
    require(signoff, "Explicit signoff text is required.")
    require(not run.db.execute("SELECT 1 FROM attempts WHERE status='running'").fetchone(),
            "All workers must be idle before acceptance.")
    for criterion in run.manifest["scope"]["criteria"]:
        for target in PLATFORMS:
            item = run.db.execute(
                "SELECT * FROM evidence WHERE work_group=? AND platform=? AND criterion=? AND snapshot_id=? "
                "AND round=? ORDER BY id DESC LIMIT 1",
                (args.group, target, criterion["id"], snapshot, round_number)).fetchone()
            require(item and item["result"] == "pass", f"Missing current PASS: {target}/{criterion['id']}")
            require(digest((run.path / item["file"]).read_bytes()) == item["digest"], "Evidence changed.")
    with run.db:
        run.db.execute("INSERT INTO acceptance VALUES(?,?,?,?,?,?)",
                       (args.group, snapshot, round_number, now(), args.signed_by, signoff))
    print(f"{args.group} accepted at source snapshot {snapshot}.")
    run.close()


def aggregate(run, group):
    attempts = list(run.db.execute("SELECT * FROM attempts WHERE work_group=? ORDER BY started_at", (group,)))
    rows = list(run.db.execute("SELECT * FROM snapshots WHERE work_group=? ORDER BY id", (group,)))
    final = changes(json.loads(rows[0]["files"]), json.loads(rows[-1]["files"]), run.path / "blobs")
    churn = {}
    for old, new in zip(rows, rows[1:]):
        for kind, values in changes(json.loads(old["files"]), json.loads(new["files"]), run.path / "blobs").items():
            bucket = churn.setdefault(kind, {"added": 0, "removed": 0})
            for key in bucket:
                bucket[key] += values[key]
    metrics = {"model_events": 0, "internal_nano_aiu": 0, "model_ms": 0,
               "input_tokens": 0, "output_tokens": 0, "cache_read_tokens": 0,
               "cache_write_tokens": 0, "reasoning_tokens": 0}
    unknown, unavailable_metrics, by_phase, by_model = [], set(), {}, {}
    by_scope = {}
    scope_changes = [row[0] for row in run.db.execute(
        "SELECT created_at FROM feedback WHERE kind='scope-addition' ORDER BY id")]
    for attempt in attempts:
        if attempt["phase"] == "check":
            continue
        if not attempt["usage"]:
            unknown.append(attempt["id"])
            unavailable_metrics.update(metrics)
            continue
        receipt = json.loads(attempt["usage"])
        nano = receipt["totalNanoAiu"]
        metrics["internal_nano_aiu"] += nano
        metrics["model_ms"] += receipt["totalApiDurationMs"]
        by_phase[attempt["phase"]] = by_phase.get(attempt["phase"], 0) + nano
        version = str(1 + sum(timestamp <= attempt["started_at"] for timestamp in scope_changes))
        by_scope[version] = by_scope.get(version, 0) + nano
        for model, value in receipt["modelMetrics"].items():
            by_model[model] = by_model.get(model, 0) + round(value["totalNanoAiu"])
            metrics["model_events"] += value["requests"]["count"]
            for source, target in (("inputTokens", "input_tokens"), ("outputTokens", "output_tokens"),
                                   ("cacheReadTokens", "cache_read_tokens"), ("cacheWriteTokens", "cache_write_tokens"),
                                   ("reasoningTokens", "reasoning_tokens")):
                count = value["usage"].get(source)
                if count is None:
                    unavailable_metrics.add(target)
                else:
                    metrics[target] += count
    baseline_files, final_files = json.loads(rows[0]["files"]), json.loads(rows[-1]["files"])
    paths = {"shared": "src/BaristaNotes.Core/", "ios": "src/BaristaNotes.iOS/",
             "android": "src/BaristaNotes.Android/", "maui": "src/BaristaNotes/"}
    breakdown = {}
    for label, prefix in paths.items():
        old = {k: v for k, v in baseline_files.items() if k.startswith(prefix) and v["category"] == "product"}
        new = {k: v for k, v in final_files.items() if k.startswith(prefix) and v["category"] == "product"}
        breakdown[label] = changes(old, new, run.path / "blobs").get("product", {"added": 0, "removed": 0, "files": 0})
    acceptance = run.db.execute("SELECT * FROM acceptance WHERE work_group=?", (group,)).fetchone()
    elapsed_ms = None
    first_evidence = {}
    if acceptance and attempts:
        elapsed_ms = round((dt.datetime.fromisoformat(acceptance["created_at"]) -
                            dt.datetime.fromisoformat(attempts[0]["started_at"])).total_seconds() * 1000)
    if attempts:
        for target in PLATFORMS:
            first = run.db.execute("SELECT MIN(created_at) FROM evidence WHERE work_group=? AND platform=? "
                                   "AND result='pass'", (group, target)).fetchone()[0]
            first_evidence[target] = None if first is None else round(
                (dt.datetime.fromisoformat(first) - dt.datetime.fromisoformat(attempts[0]["started_at"]))
                .total_seconds() * 1000)
    return {"metrics": metrics, "unavailable_attempts": unknown, "by_phase_nano_aiu": by_phase,
            "unavailable_metrics": sorted(unavailable_metrics), "by_scope_version_nano_aiu": by_scope,
            "by_model_nano_aiu": by_model, "final_change": final, "snapshot_churn": churn,
            "product_change_by_path": breakdown, "elapsed_to_acceptance_ms": elapsed_ms,
            "source_digest": rows[-1]["digest"],
            "first_pass_evidence_ms_by_platform": first_evidence,
            "unavailable_command_durations": [row["id"] for row in attempts if row["duration_ms"] is None],
            "recorded_command_ms": sum(row["duration_ms"] or 0 for row in attempts),
            "rounds": run.round(group), "attempts": len(attempts),
            "accepted": bool(run.db.execute("SELECT 1 FROM acceptance WHERE work_group=?", (group,)).fetchone())}


def coordinator_cost(run, cutoff):
    row = run.db.execute("SELECT value FROM metadata WHERE key='coordinator'").fetchone()
    if not row:
        return {"status": "unavailable", "reason": "Coordinator tracking is not configured."}
    config = json.loads(row[0])
    source = Path(config["database"])
    require(source.is_file(), "Configured coordinator usage database is missing.")
    with sqlite3.connect(source.as_uri() + "?mode=ro", uri=True) as db:
        db.row_factory = sqlite3.Row
        rows = [dict(value) for value in db.execute(
            "SELECT model, reasoning_effort, COUNT(*) AS model_events, "
            "SUM(total_nano_aiu) AS internal_nano_aiu, SUM(duration_ms) AS model_ms, "
            "SUM(input_tokens) AS input_tokens, SUM(output_tokens) AS output_tokens, "
            "SUM(cache_read_tokens) AS cache_read_tokens, SUM(cache_write_tokens) AS cache_write_tokens, "
            "SUM(reasoning_tokens) AS reasoning_tokens, MAX(id) AS maximum_event_id, "
            "SUM(CASE WHEN total_nano_aiu IS NULL THEN 1 ELSE 0 END) AS missing_cost_events "
            "FROM assistant_usage_events WHERE session_id=? AND agent_id IS NULL "
            "AND julianday(created_at)>=julianday(?) AND julianday(created_at)<julianday(?) "
            "GROUP BY model,reasoning_effort",
            (config["session"], config["start"], cutoff))]
    return {"status": "recorded" if rows else "no-records", "start": config["start"], "cutoff": cutoff,
            "models": rows, "internal_nano_aiu": sum(r["internal_nano_aiu"] or 0 for r in rows),
            "missing_cost_events": sum(r["missing_cost_events"] for r in rows)}


def track_coordinator(args):
    run = Run(args.run)
    config = {"database": str(Path(args.database).expanduser().resolve()),
              "session": args.session, "start": args.start}
    require(dt.datetime.fromisoformat(args.start.replace("Z", "+00:00")).utcoffset() is not None,
            "Coordinator start requires a timezone.")
    with run.db:
        run.db.execute("INSERT OR REPLACE INTO metadata VALUES('coordinator',?)",
                       (json.dumps(config),))
        coordinator_cost(run, now())
    run.close()
    print("Read-only coordinator tracking configured; worker totals remain separate.")


def snapshot(args):
    run = Run(args.run)
    require(re.fullmatch(r"[a-z0-9][a-z0-9-]*", args.label), "Use a simple lowercase checkpoint label.")
    directory = run.path / "checkpoints" / args.label
    require(not directory.exists(), "Checkpoint already exists; immutable snapshots cannot be overwritten.")
    cutoff = now()
    with run.db:
        run.db.execute("BEGIN IMMEDIATE")
        require(not run.db.execute("SELECT 1 FROM attempts WHERE status='running'").fetchone(),
                "Wait for all measured attempts to finish before taking a snapshot.")
        for group in GROUPS:
            run.snapshot(group)
        data = {"cutoff": cutoff, "status": "interim; no acceptance implied",
                "run": run.manifest, "groups": {g: aggregate(run, g) for g in GROUPS},
                "coordinator": coordinator_cost(run, cutoff),
                "feedback": [dict(r) for r in run.db.execute(
                    "SELECT target,kind,round,created_at FROM feedback ORDER BY id")]}
    if args.compare_to:
        require(re.fullmatch(r"[a-z0-9][a-z0-9-]*", args.compare_to), "Invalid previous checkpoint label.")
        previous = json.loads((run.path / "checkpoints" / args.compare_to / "totals.json").read_text())
        data["increment_since"] = args.compare_to
        data["increment"] = {g: {k: data["groups"][g]["metrics"][k] -
                                previous["groups"][g]["metrics"][k]
                                for k in data["groups"][g]["metrics"]} for g in GROUPS}
    directory.mkdir(parents=True, mode=0o700)
    write_json(directory / "totals.json", data)
    with sqlite3.connect(directory / "measurements.sqlite3") as copy:
        run.db.backup(copy)
    for group in GROUPS:
        row = run.latest(group)
        index = json.loads(row["files"])
        write_json(directory / (group + "-source-index.json"), {"id": row["id"], "digest": row["digest"], "files": index})
        with zipfile.ZipFile(directory / (group + "-source.zip"), "w", zipfile.ZIP_DEFLATED) as archive:
            for name, value in sorted(index.items()):
                path = Path(name)
                require(not path.is_absolute() and ".." not in path.parts, "Unsafe source path.")
                if path.suffix.lower() in {".keystore", ".p12", ".pfx", ".mobileprovision"} or path.name == "appsettings.Development.json":
                    continue
                content = (run.path / "blobs" / value["sha256"]).read_bytes()
                require(digest(content) == value["sha256"], "Source blob checksum mismatch.")
                archive.writestr(name, content)
    receipts = directory / "usage-receipts"
    receipts.mkdir()
    for row in run.db.execute("SELECT id,usage FROM attempts WHERE phase!='check'"):
        if row["usage"]:
            write_json(receipts / (row["id"] + ".json"), json.loads(row["usage"]))
    lines = ["# Interim work comparison", "", f"Checkpoint: {args.label}. Cutoff: {cutoff}.",
             "", "No acceptance is implied. Native includes both platform heads and shared work once.", "",
             "| Measure | MAUI | Native |", "|---|---:|---:|"]
    for label, metric, divisor in [
        ("Recorded internal AI units", "internal_nano_aiu", 1e9),
        ("Model calls", "model_events", 1),
        ("Model minutes (not human labor)", "model_ms", 60000),
        ("Gross input tokens", "input_tokens", 1),
        ("Output tokens", "output_tokens", 1)]:
        values = [data["groups"][g]["metrics"][metric] / divisor for g in GROUPS]
        lines.append(f"| {label} | {values[0]:,.3f} | {values[1]:,.3f} |")
    for kind in ("product", "tests"):
        for key in ("added", "removed"):
            values = [data["groups"][g]["final_change"].get(kind, {}).get(key, 0) for g in GROUPS]
            lines.append(f"| {kind.title()} lines {key} | {values[0]} | {values[1]} |")
    coord = data["coordinator"]
    lines += ["", "## Separate overhead", ""]
    lines.append(f"Coordinator model units: {coord['internal_nano_aiu']/1e9:,.6f}."
                 if coord["status"] == "recorded" else "Coordinator cost: unavailable; not zero.")
    lines += ["", "## Measurement limits", "",
              "- Internal units are not dollars. Model minutes are not human effort.",
              "- Cache categories are included in gross input; do not add them again.",
              "- Technical corrections and UX feedback have separate ledger kinds. Legacy defect entries are technical.",
              "- External UI work and human time remain unavailable unless recorded separately.",
              "- Line totals include comments/blanks; JSON/reference data and generated lock files are not product code.",
              "- Missing receipts remain unavailable. Source archives exclude signing keys and development credentials.",
              "- Final acceptance/report remains gated; this command does not reset counters."]
    if args.compare_to:
        lines += ["", f"Incremental worker metrics since `{args.compare_to}` are saved in totals.json."]
    (directory / "snapshot.md").write_text("\n".join(lines) + "\n")
    write_json(directory / "checksums.json", {str(p.relative_to(directory)): digest(p.read_bytes())
                                            for p in sorted(directory.rglob("*")) if p.is_file()})
    run.close()
    print(directory / "snapshot.md")


def report(args):
    run = Run(args.run)
    for group in GROUPS:
        acceptance = run.db.execute("SELECT * FROM acceptance WHERE work_group=?", (group,)).fetchone()
        require(acceptance, f"{group} has not been accepted.")
        sha, _ = capture_tree(run.workspace(group), run.path / "blobs")
        require(sha == run.latest(group)["digest"], f"{group} source changed after acceptance.")
        require(acceptance["snapshot_id"] == run.latest(group)["id"], "Accepted source is no longer current.")
        for item in run.db.execute("SELECT * FROM evidence WHERE work_group=? AND snapshot_id=?",
                                   (group, acceptance["snapshot_id"])):
            require(digest((run.path / item["file"]).read_bytes()) == item["digest"], "Evidence changed.")
    data = {"schema_version": 1, "run": run.manifest, "groups": {g: aggregate(run, g) for g in GROUPS},
            "acceptance": [dict(row) for row in run.db.execute("SELECT * FROM acceptance")],
            "coordinator": coordinator_cost(run, now()),
            "overhead": "Coordinator model cost is separate when configured. Human review, external device time and integration are not automatically measured.",
            "limitations": [
                "Recorded command time excludes user wait; concurrent groups must not be summed as elapsed time.",
                "Model duration is not human effort; internal usage units are not dollars.",
                "Cache read/write are components of gross input, not additional input.",
                "Snapshot churn is a lower bound; intermediate edits between snapshots are not observed.",
                "Missing cost or tokens are unavailable, not zero.",
                "Native Core is counted once. This pilot reports code by path, not per-platform token guesses.",
                "Worker file tools are isolated. Coordinator feedback and external device checks require protocol discipline."
            ]}
    directory = run.path / "report"
    directory.mkdir(exist_ok=True)
    write_json(directory / "totals.json", data)
    a, b = (data["groups"][g] for g in GROUPS)
    lines = [f"# Work comparison: {run.manifest['scope']['title']}", "",
             f"Purpose: {run.manifest['scope'].get('purpose', 'comparison')}.",
             f"Baseline: `{run.manifest['baseline']}`.", "",
             "## Approved scope", "", run.manifest["scope"]["scope"], "",
             f"Scope version: {run.manifest['scope_version']}.",
             f"Configured model: `{run.manifest['scope']['model']}`; "
             f"reasoning effort: `{run.manifest['scope']['reasoning_effort']}`.", "",
             "| Measure | MAUI (iOS + Android) | Native (iOS + Android) |",
             "|---|---:|---:|"]
    def metric(value, key):
        known = value["metrics"][key]
        return f"Unavailable (known: {known:,})" if key in value["unavailable_metrics"] else f"{known:,}"
    for label, key in (("Model calls", "model_events"), ("Gross input tokens", "input_tokens"),
                       ("Output tokens", "output_tokens"), ("Cache-read tokens", "cache_read_tokens"),
                       ("Cache-write tokens", "cache_write_tokens"), ("Reasoning tokens", "reasoning_tokens"),
                       ("Model execution milliseconds", "model_ms")):
        lines.append(f"| {label} | {metric(a, key)} | {metric(b, key)} |")
    def cost(value):
        number = value["metrics"]["internal_nano_aiu"] / 1_000_000_000
        return f"{number:.6f}" + (" (partial; records missing)" if value["unavailable_attempts"] else "")
    def command_time(value):
        known = f"{value['recorded_command_ms']:,}"
        return f"Unavailable (known: {known})" if value["unavailable_command_durations"] else known
    lines += [f"| Recorded internal AI usage units | {cost(a)} | {cost(b)} |",
              f"| Recorded command milliseconds | {command_time(a)} | {command_time(b)} |",
              f"| Elapsed milliseconds to acceptance | {a['elapsed_to_acceptance_ms']} | {b['elapsed_to_acceptance_ms']} |",
              f"| Feedback round | {a['rounds']} | {b['rounds']} |",
              "", "![Recorded model cost](cost.svg)", "", "## Source changes", "",
              "Line counts include blank/comment lines. Generated code and binary files are separate.",
              "", "| Group | Category | Final added | Final removed | Churn added | Churn removed |",
              "|---|---|---:|---:|---:|---:|"]
    for group, value in data["groups"].items():
        for kind in sorted(value["final_change"].keys() | value["snapshot_churn"].keys()):
            net = value["final_change"].get(kind, {})
            churn = value["snapshot_churn"].get(kind, {})
            lines.append(f"| {group} | {kind} | {net.get('added',0)} | {net.get('removed',0)} | "
                         f"{churn.get('added',0)} | {churn.get('removed',0)} |")
    lines += ["", "## Cost attribution", "", "| Group | Activity | Internal AI units |",
              "|---|---|---:|"]
    for group, value in data["groups"].items():
        for phase, nano in sorted(value["by_phase_nano_aiu"].items()):
            lines.append(f"| {group} | {phase} | {nano / 1_000_000_000:.6f} |")
    lines += ["", "## Scope versions", "", "| Group | Scope version | Recorded internal AI units |",
              "|---|---:|---:|"]
    for group, value in data["groups"].items():
        for version, nano in value["by_scope_version_nano_aiu"].items():
            lines.append(f"| {group} | {version} | {nano / 1_000_000_000:.6f} |")
    lines += ["", "## Recorded models", "", "| Group | Model | Recorded internal AI units |",
              "|---|---|---:|"]
    for group, value in data["groups"].items():
        for model, nano in value["by_model_nano_aiu"].items():
            lines.append(f"| {group} | {model} | {nano / 1_000_000_000:.6f} |")
    lines += ["", "## Product-code ownership", "",
              "| Group | Source area | Added | Removed |", "|---|---|---:|---:|"]
    for group, value in data["groups"].items():
        for label, counts in value["product_change_by_path"].items():
            lines.append(f"| {group} | {label} | {counts['added']} | {counts['removed']} |")
    lines += ["", "## Limits", "", data["overhead"], ""]
    if data["coordinator"]["status"] == "recorded":
        lines += [f"Separate coordinator internal AI units: {data['coordinator']['internal_nano_aiu']/1e9:.6f}.", ""]
    lines += ["First verified evidence is recorded per platform in `totals.json`.",
              "Its timestamp is the recording time, not an inferred earlier demonstration.", ""]
    lines.extend("- " + limit for limit in data["limitations"])
    lines += ["", "Accepted snapshot ids: " + ", ".join(
        f"{item['work_group']}={item['snapshot_id']}" for item in data["acceptance"]), "",
        "Machine-readable totals and the private run ledger retain the source receipts.", ""]
    for group, value in data["groups"].items():
        lines += [f"Accepted {group} source SHA-256: `{value['source_digest']}`.", ""]
    (directory / "report.md").write_text("\n".join(lines))
    top = max(a["metrics"]["internal_nano_aiu"], b["metrics"]["internal_nano_aiu"], 1)
    svg = ['<svg xmlns="http://www.w3.org/2000/svg" width="720" height="180" role="img">',
           '<title>Recorded model cost in internal AI usage units, not dollars</title>',
           '<rect width="720" height="180" fill="#fff"/>']
    for index, (group, value) in enumerate(data["groups"].items()):
        y = 35 + 70 * index
        width = 420 * value["metrics"]["internal_nano_aiu"] / top
        svg += [f'<text x="20" y="{y+22}" font-family="sans-serif">{group}</text>',
                f'<rect x="90" y="{y}" width="{width:.2f}" height="30" fill="#6f4e37"/>',
                f'<text x="530" y="{y+22}" font-family="sans-serif">{cost(value)}</text>']
    svg.append("</svg>")
    (directory / "cost.svg").write_text("\n".join(svg))
    print(directory / "report.md")
    run.close()


def recover(args):
    run = Run(args.run)
    row = run.db.execute("SELECT a.*,p.launcher_pid,p.worker_pid FROM attempts a "
                         "JOIN processes p ON p.attempt_id=a.id WHERE a.id=?", (args.attempt,)).fetchone()
    require(row and row["status"] == "running", "No interrupted running attempt with that id.")
    for pid in (row["launcher_pid"], row["worker_pid"]):
        if pid is None:
            continue
        try:
            os.kill(pid, 0)
        except ProcessLookupError:
            continue
        raise ValueError(f"Recorded process {pid} is still alive. Do not recover or duplicate this worker.")
    processes = subprocess.run(["ps", "-axo", "command="], capture_output=True, text=True, check=True)
    require(row["session_id"] not in processes.stdout, "A child session is still running.")
    receipt_path = run.home(row["work_group"]) / f"usage-{row['id']}.json"
    if row["phase"] != "check" and receipt_path.exists():
        usage_import(run, row["id"], receipt_path)
    with run.db:
        run.db.execute("UPDATE attempts SET status='interrupted',ended_at=? WHERE id=?", (now(), row["id"]))
    run.snapshot(row["work_group"])
    print("Interrupted attempt retained. Missing duration or usage remains unavailable.")
    run.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    p = commands.add_parser("prepare", help="Freeze an approved scope and independent source exports.")
    p.add_argument("scope")
    p.add_argument("--repo", default=".")
    p.add_argument("--baseline", default="HEAD")
    p.add_argument("--root", default=str(Path.home() / ".baristanotes-comparisons"))
    for name in ("doctor", "checkpoint", "snapshot", "track-coordinator", "status", "report", "feedback", "evidence", "accept", "work", "check", "import-usage", "recover"):
        p = commands.add_parser(name)
        p.add_argument("run")
        if name == "snapshot":
            p.add_argument("--label", required=True)
            p.add_argument("--compare-to")
        if name == "track-coordinator":
            p.add_argument("--database", default=str(Path.home() / ".copilot/session-store.db"))
            p.add_argument("--session", required=True)
            p.add_argument("--start", required=True)
        if name in ("work", "check", "feedback", "evidence", "accept"):
            p.add_argument("--group", choices=GROUPS + (("both",) if name == "feedback" else ()), required=True)
        if name == "work":
            p.add_argument("--phase", choices=("implementation", "correction", "review"), default="implementation")
        if name == "check":
            p.add_argument("--host-toolchain", action="store_true",
                           help="Run a trusted coordinator check without the OS build sandbox; workers stay isolated.")
        if name == "feedback":
            p.add_argument("--kind", choices=("defect", "technical", "ux", "clarification", "scope-addition"), default="technical")
        if name in ("feedback", "evidence", "accept", "import-usage"):
            p.add_argument("--file", required=True)
        if name == "evidence":
            p.add_argument("--platform", choices=PLATFORMS, required=True)
            p.add_argument("--criterion", required=True)
            p.add_argument("--result", choices=("pass", "fail"), required=True)
        if name == "accept":
            p.add_argument("--signed-by", required=True)
        if name in ("import-usage", "recover"):
            p.add_argument("--attempt", required=True)
    raw_args = sys.argv[1:]
    check_argv = []
    if raw_args and raw_args[0] == "check" and "--" in raw_args:
        separator = raw_args.index("--")
        check_argv, raw_args = raw_args[separator + 1:], raw_args[:separator]
    args = parser.parse_args(raw_args)
    if args.command == "check":
        args.argv = check_argv
    try:
        if args.command == "prepare":
            prepare(args)
        elif args.command in ("work", "check"):
            execute(args)
        elif args.command == "feedback":
            feedback(args)
        elif args.command == "evidence":
            evidence(args)
        elif args.command == "accept":
            accept(args)
        elif args.command == "report":
            report(args)
        elif args.command == "snapshot":
            snapshot(args)
        elif args.command == "track-coordinator":
            track_coordinator(args)
        elif args.command == "recover":
            recover(args)
        else:
            run = Run(args.run)
            if args.command == "doctor":
                for group in GROUPS:
                    isolation_check(run, group)
            elif args.command == "checkpoint":
                with run.db:
                    run.db.execute("BEGIN IMMEDIATE")
                    for group in GROUPS:
                        run.mutable(group)
                        print(group, run.snapshot(group))
            elif args.command == "import-usage":
                usage_import(run, args.attempt, args.file)
                print("Usage imported once; repeated identical receipts do not add cost.")
            else:
                print(json.dumps({g: aggregate(run, g) for g in GROUPS}, indent=2))
            run.close()
    except (ValueError, OSError, subprocess.SubprocessError, sqlite3.Error, ET.ParseError) as error:
        print(f"compare-work: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
