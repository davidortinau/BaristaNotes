import argparse
import csv
import hashlib
import json
import math
import os
import plistlib
import statistics
import subprocess
import time
import zipfile
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("app", choices=("maui", "native"))
parser.add_argument("configuration", choices=("fullr2r", "aot"))
parser.add_argument("--output", required=True, type=Path)
parser.add_argument("--device", required=True)
parser.add_argument("--port", type=int, default=29223)
parser.add_argument("--codesign-key", required=True)
parser.add_argument("--codesign-provision", required=True)
parser.add_argument("--smoke", action="store_true")
args = parser.parse_args()

root = Path(__file__).resolve().parent
source = root / "source-devflow-ios-ga"
build = root / "builds" / f"df2-ios-{args.app}-{args.configuration}"
variant = "dffull" if args.configuration == "fullr2r" else "dfaot"
project = "BaristaNotes" if args.app == "maui" else "BaristaNotes.iOS"
prefix = "Maui" if args.app == "maui" else "Native"
flavor = "maui" if args.app == "maui" else "nativeviews"
package = f"com.simplyprofound.baristanotes.perf.ga.{flavor}.ios.{variant}"
ipa = next(build.glob("bin/**/publish/*.ipa"))
args.output.mkdir(parents=True, exist_ok=False)
log = (args.output / "commands.jsonl").open("x", buffering=1)
seen_pids = set()
last_pid = None
sequence = 0


def run(argv, timeout=60, check=True, env=None, cwd=None):
    result = subprocess.run(argv, capture_output=True, text=True, timeout=timeout, env=env, cwd=cwd)
    log.write(json.dumps({"argv": argv, "exit": result.returncode, "stdout": result.stdout,
                          "stderr": result.stderr, "time": time.time()}) + "\n")
    if check and result.returncode:
        raise RuntimeError(f"Command failed: {argv}: {result.stderr[-2000:]}")
    return result


def copy_status(destination):
    return run([
        "xcrun", "devicectl", "device", "copy", "from", "--device", args.device,
        "--domain-type", "appDataContainer", "--domain-identifier", package,
        "--source", "Library/performance/status.txt", "--destination", str(destination),
    ], check=False, timeout=25)


def parse_status(path):
    return dict(line.split("=", 1) for line in path.read_text().splitlines() if "=" in line)


def stop():
    global last_pid
    if last_pid is not None:
        run(["xcrun", "devicectl", "device", "process", "terminate", "--device", args.device,
             "--pid", str(last_pid), "--kill"], check=False)
        last_pid = None


def agent_status():
    result = subprocess.run([
        "maui", "devflow", "--platform", "ios", "--agent-host", "127.0.0.1",
        "--agent-port", str(args.port), "--json", "agent", "status",
    ], capture_output=True, text=True, timeout=15)
    if result.returncode:
        return None
    try:
        value = json.loads(result.stdout)
    except json.JSONDecodeError:
        return None
    if value.get("app", {}).get("packageId") != package:
        return None
    if value.get("device", {}).get("platform") != "iOS":
        return None
    return value


def sample(mode, number, route=None):
    global sequence, last_pid
    stop()
    sequence += 1
    label = f"{sequence:03}-{mode}-{route or 'startup'}-{number:02}"
    extra = ["--perf-mode", mode, "--perf-sample", str(number)]
    if route:
        extra += ["--perf-route", route]
    env = os.environ.copy()
    env["MlaunchAdditionalArgumentsProperty"] = ";".join("--argument=" + item for item in extra)
    argv = [
        "dotnet", "build", f"src/{project}/{project}.csproj", "-t:Run", "-c", "Release",
        "-f", "net11.0-ios", "-r", "ios-arm64", f"-p:Device={args.device}",
        f"-p:BenchmarkOutputRoot={build}", f"-p:BenchmarkVariant={variant}",
        "-p:BenchmarkDevFlow=true", "-p:MauiDevFlowVersion=0.1.0-preview.14.26567.2",
        f"-p:{prefix}Performance=true", f"-p:{prefix}PerformanceDataset=1000",
        f"-p:{prefix}PerformanceDatabase={root / 'fixture/barista_notes.db'}",
        f"-p:{prefix}PerformanceMetadata={root / 'fixture/preferences.txt'}",
        "-p:UseMonoRuntime=false", "-p:MtouchInterpreter=",
        f"-p:CodesignKey={args.codesign_key}",
        f"-p:CodesignProvision={args.codesign_provision}",
        "-p:WaitForExit=false", "--no-restore", "--nologo", "-v:quiet",
    ]
    if args.configuration == "aot":
        argv += ["-p:EnableNativeAot=true", "-p:PublishAot=true",
                 "-p:PublishAotUsingRuntimePack=true", "-p:PublishReadyToRun=false",
                 "-p:TrimMode=full", "-p:MtouchLink=Full"]
    else:
        argv += ["-p:PublishAot=false", "-p:PublishReadyToRun=true",
                 "-p:PublishReadyToRunComposite=true"]
    run(argv, timeout=180, env=env, cwd=source)

    deadline = time.monotonic() + 60
    agent = None
    while time.monotonic() < deadline:
        agent = agent_status()
        if agent:
            break
        time.sleep(0.3)
    if agent is None:
        raise TimeoutError(f"DevFlow agent did not become reachable for {package}")
    log.write(json.dumps({"agent": agent, "time": time.time()}) + "\n")

    attempt = 0
    status = {}
    while time.monotonic() < deadline:
        path = args.output / f"{label}-status-{attempt:02}.txt"
        result = copy_status(path)
        attempt += 1
        if result.returncode == 0 and path.is_file():
            status = parse_status(path)
            pid = int(status.get("pid", "0"))
            if (pid not in seen_pids and pid > 0 and status.get("bundle") == package
                    and status.get("mode") == mode and status.get("sample") == str(number)
                    and status.get("route", "") == (route or "")):
                last_pid = pid
                if status.get("state") == "failed" or status.get("failure"):
                    raise RuntimeError(f"Probe failed: {status}")
                if status.get("state") == "complete":
                    if status.get("dataset") != "1000" or float(status["durationMs"]) <= 0:
                        raise RuntimeError(f"Invalid probe result: {status}")
                    if args.configuration == "aot" and status.get("dynamicCodeSupported") != "False":
                        raise RuntimeError(f"NativeAOT not active: {status}")
                    seen_pids.add(pid)
                    return status
        time.sleep(0.2)
    raise TimeoutError(f"Probe did not complete for {label}: {status}")


def distribution(values):
    values = sorted(values)
    return {"n": len(values), "p50": statistics.median(values),
            "p90": values[math.ceil(0.9 * len(values)) - 1],
            "min": values[0], "max": values[-1]}


summary = {"app": args.app, "configuration": args.configuration, "package": package}
try:
    with zipfile.ZipFile(ipa) as archive:
        plist = next(n for n in archive.namelist()
                     if n.startswith("Payload/") and n.count("/") == 2 and n.endswith("/Info.plist"))
        info = plistlib.loads(archive.read(plist))
        if info["CFBundleIdentifier"] != package:
            raise RuntimeError(f"Wrong bundle: {info['CFBundleIdentifier']}")
        fixture_path = next(n for n in archive.namelist() if n.endswith("/performance/barista_notes.db"))
        fixture = hashlib.sha256(archive.read(fixture_path)).hexdigest()
        if fixture != "7e23a022af101dd46bd596e6a377f7597b103edb0d735896dfcc6c07e6ae8fbf":
            raise RuntimeError(f"Wrong fixture: {fixture}")
        code = plist.rsplit("/", 1)[0] + "/" + info["CFBundleExecutable"]
        code_hash = hashlib.sha256(archive.read(code)).hexdigest()
    bundle_name = Path(plist).parts[1]
    bundle = next(p for p in build.glob(f"bin/**/{bundle_name}") if "publish" not in p.parts)
    summary.update(bytes=ipa.stat().st_size, sha256=hashlib.sha256(ipa.read_bytes()).hexdigest(),
                   fixture_sha256=fixture, executable_sha256=code_hash)
    old_status = args.output / "prior-status.txt"
    if copy_status(old_status).returncode == 0 and old_status.is_file():
        old_pid = int(parse_status(old_status).get("pid", "0"))
        if old_pid > 0:
            seen_pids.add(old_pid)
    wait = run(["maui", "devflow", "--platform", "ios", "--agent-host", "127.0.0.1",
                "--agent-port", str(args.port), "wait", "--wait-platform", "iOS",
                "--timeout", "2"], check=False, timeout=10)
    summary["broker_wait"] = (
        "timed out" if "Timeout" in wait.stdout + wait.stderr else "connected"
    )
    values = []
    with (args.output / "startup.csv").open("x", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=("phase", "sample", "pid", "duration_ms"))
        writer.writeheader()
        for number in range(1 if args.smoke else 36):
            status = sample("startup", number)
            elapsed = float(status["durationMs"])
            phase = "pilot" if number == 0 else "conditioning" if number <= 5 else "measured"
            writer.writerow(dict(phase=phase, sample=number, pid=status["pid"], duration_ms=elapsed))
            stream.flush()
            if phase == "measured":
                values.append(elapsed)
    summary["startup"] = distribution(values) if values else None
    print(f"{args.app}-{args.configuration}: startup complete", flush=True)
    transitions = {}
    with (args.output / "transitions.csv").open("x", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=("route", "sample", "pid", "duration_ms"))
        writer.writeheader()
        for route in ("new-drink-to-activity", "activity-to-settings"):
            values = []
            for number in range(1 if args.smoke else 10):
                status = sample("transition", number, route)
                elapsed = float(status["durationMs"])
                values.append(elapsed)
                writer.writerow(dict(route=route, sample=number, pid=status["pid"], duration_ms=elapsed))
                stream.flush()
            transitions[route] = distribution(values)
            print(f"{args.app}-{args.configuration}: {route} complete", flush=True)
    summary["transitions"] = transitions
    deployed_hash = hashlib.sha256((bundle / info["CFBundleExecutable"]).read_bytes()).hexdigest()
    if deployed_hash != code_hash:
        raise RuntimeError(f"Deployed executable differs from IPA: {deployed_hash} != {code_hash}")
    summary["status"] = "smoke-passed" if args.smoke else "complete"
except Exception as error:
    summary["status"] = "failed"
    summary["error"] = str(error)
    raise
finally:
    (args.output / "summary.json").write_text(json.dumps(summary, indent=2))
    stop()
    log.close()
print(json.dumps({key: summary[key] for key in ("app", "configuration", "status", "bytes",
                                               "startup", "transitions")}), flush=True)
