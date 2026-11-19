import argparse
import csv
import hashlib
import json
import math
import os
import re
import statistics
import subprocess
import time
import urllib.request
import urllib.parse
import zipfile
from datetime import datetime
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("app", choices=("maui", "native"))
parser.add_argument("configuration", choices=("fullr2r", "partialr2r", "aot"))
parser.add_argument("--output", required=True, type=Path)
parser.add_argument("--serial", required=True)
parser.add_argument("--aapt", default="aapt")
parser.add_argument("--smoke", action="store_true")
args = parser.parse_args()

root = Path(__file__).resolve().parent
source = root / "source-devflow-ga"
build = root / "builds" / f"df3-android-{args.app}-{args.configuration}"
variant = {"fullr2r": "dffull", "partialr2r": "dfpartial", "aot": "dfaot"}[args.configuration]
package = (
    f"com.simplyprofound.baristanotes.perf.ga.maui.android.{variant}"
    if args.app == "maui"
    else f"com.simplyprofound.baristanotes.perf.nativeviews1000.{variant}"
)
project = "BaristaNotes" if args.app == "maui" else "BaristaNotes.Android"
app_name = "BaristaNotes MAUI Performance" if args.app == "maui" else "BaristaNotes Native"
prefix = "Maui" if args.app == "maui" else "Native"
apk = next(p for p in (build / "bin").glob("**/*-Signed.apk") if "publish" not in p.parts)
args.output.mkdir(parents=True, exist_ok=False)
log = (args.output / "commands.jsonl").open("x", buffering=1)
pids = set()
lease = "baristanotes-devflow-benchmark"
active_port = None
serial_env = os.environ.copy()
serial_env["ANDROID_SERIAL"] = args.serial


def run(argv, timeout=60, env=None):
    result = subprocess.run(argv, capture_output=True, text=True, timeout=timeout, env=env)
    log.write(json.dumps({"argv": argv, "exit": result.returncode, "stdout": result.stdout,
                          "stderr": result.stderr, "time": time.time()}) + "\n")
    if result.returncode:
        raise RuntimeError(f"Command failed: {argv}: {result.stderr[-2000:]}")
    return result.stdout


def adb(*argv):
    return run(["adb", "-s", args.serial, *argv])


def request(port, path, payload, with_lease=False):
    headers = {"Content-Type": "application/json"}
    if with_lease:
        headers["X-DevFlow-Lease"] = lease
    data = json.dumps(payload).encode()
    req = urllib.request.Request(f"http://127.0.0.1:{port}/api/v1{path}",
                                 data=data, headers=headers, method="POST")
    with urllib.request.urlopen(req, timeout=15) as response:
        result = json.load(response)
    log.write(json.dumps({"devflow_port": port, "path": path, "payload": payload,
                          "response": result, "time": time.time()}) + "\n")
    return result


def action(port, *parameters):
    keys = ("command", "route", "sample")
    query = urllib.parse.urlencode(dict(zip(keys, parameters)))
    req = urllib.request.Request(
        f"http://127.0.0.1:{port}/api/v1/ext/com.baristanotes.benchmark/command?{query}",
        data=b"{}", headers={"X-DevFlow-Lease": lease, "Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=15) as response:
        result = response.read().decode()
    log.write(json.dumps({"devflow_port": port, "command": parameters,
                          "response": result, "time": time.time()}) + "\n")
    return dict(item.split("=", 1) for item in result.split(";") if "=" in item)


def claim(port):
    global active_port
    result = request(port, "/agent/lease", {
        "action": "claim", "leaseId": lease, "holderKind": "benchmark",
        "label": "BaristaNotes benchmark", "force": False,
    })
    if not result.get("allowed") or result.get("heldByOther"):
        raise RuntimeError(f"DevFlow lease unavailable: {result}")
    active_port = port


def release(port):
    global active_port
    request(port, "/agent/lease", {"action": "release", "leaseId": lease})
    if active_port == port:
        active_port = None


def status_until(port, page=None, state=None):
    deadline = time.monotonic() + 40
    while time.monotonic() < deadline:
        status = action(port, "status")
        if status.get("failure") or status.get("state") == "rejected":
            raise RuntimeError(status)
        if status.get("dataset") != "1000":
            raise RuntimeError(f"Wrong fixture: {status}")
        if (page is None or status.get("page") == page) and (
            state is None or status.get("state") == state
        ) and (page != "history" or status.get("activityItems") == "50"):
            return status
        time.sleep(0.1)
    raise TimeoutError(f"Probe did not reach page={page} state={state}: {status}")


def wait_agent(start):
    deadline = time.monotonic() + 60
    while time.monotonic() < deadline:
        result = subprocess.run(
            ["maui", "devflow", "--json", "--device", args.serial, "--platform", "android",
             "agent", "list"], capture_output=True, text=True, timeout=15)
        try:
            agents = json.loads(result.stdout)
        except json.JSONDecodeError:
            agents = []
        candidates = [
            item for item in agents
            if item.get("appName") == app_name and item.get("platform") == "Android"
            and datetime.fromisoformat(item["connectedAt"].replace("Z", "+00:00")).timestamp() >= start
        ]
        if candidates:
            port = int(max(candidates, key=lambda a: a["connectedAt"])["port"])
            run(["maui", "devflow", "wait", "--device", args.serial,
                 "--wait-platform", "Android", "--agent-port", str(port), "--timeout", "20"], timeout=25)
            response = run(["maui", "devflow", "--json", "--device", args.serial,
                            "--platform", "android", "--agent-port", str(port), "agent", "status"])
            agent = json.loads(response)
            if agent["app"]["packageId"] == package:
                return port
        time.sleep(0.3)
    raise TimeoutError(f"No new DevFlow agent for {package}")


def display_time(start):
    deadline = time.monotonic() + 15
    pattern = re.compile(
        rf"^\s*(\d+\.\d+).*Displayed {re.escape(package)}/.*?:\s*\+((?:\d+m)?(?:\d+s)?\d+ms)"
    )
    while time.monotonic() < deadline:
        result = subprocess.run(
            ["adb", "-s", args.serial, "logcat", "-d", "-b", "all", "-v", "epoch",
             "-t", "20000", "-s", "ActivityTaskManager:I"],
            capture_output=True, text=True, timeout=30,
        )
        if result.returncode:
            raise RuntimeError(f"Unable to read display events: {result.stderr}")
        matches = []
        for line in result.stdout.splitlines():
            match = pattern.search(line)
            if match and float(match.group(1)) >= start:
                token = match.group(2)
                minutes = re.search(r"(\d+)m(?!s)", token)
                seconds = re.search(r"(\d+)s", token)
                millis = re.search(r"(\d+)ms", token)
                elapsed = (int(minutes.group(1)) * 60000 if minutes else 0)
                elapsed += (int(seconds.group(1)) * 1000 if seconds else 0)
                elapsed += int(millis.group(1))
                matches.append((float(match.group(1)), elapsed))
        if matches:
            log.write(json.dumps({"display_events": matches, "time": time.time()}) + "\n")
            return min(matches)[1]
        time.sleep(0.2)
    raise TimeoutError(f"No Android Displayed event for {package}")


def launch():
    argv = [
        "dotnet", "build", f"src/{project}/{project}.csproj", "-t:Run", "-c", "Release",
        "-f", "net11.0-android", "-r", "android-arm64", f"-p:AdbTarget=-s {args.serial}",
        f"-p:BenchmarkOutputRoot={build}", f"-p:BenchmarkVariant={variant}",
        "-p:BenchmarkDevFlow=true", "-p:MauiDevFlowVersion=0.1.0-preview.14.26567.2",
        f"-p:{prefix}Performance=true", f"-p:{prefix}PerformanceDataset=1000",
        f"-p:{prefix}PerformanceDatabase={root / 'fixture/barista_notes.db'}",
        f"-p:{prefix}PerformanceMetadata={root / 'fixture/preferences.txt'}",
        "-p:UseMonoRuntime=false", "-p:AndroidPackageFormats=apk", "--no-restore",
        "--nologo", "-v:quiet",
    ]
    if args.app == "maui":
        argv.append(f"-p:MauiPerformanceVariant={args.configuration}")
    if args.configuration == "aot":
        argv += ["-p:EnableNativeAot=true", "-p:PublishAot=true",
                 "-p:PublishAotUsingRuntimePack=true", "-p:PublishReadyToRun=false"]
    else:
        argv += ["-p:PublishReadyToRun=true", "-p:PublishReadyToRunComposite=true",
                 f"-p:MauiEnableFullReadyToRun={'true' if args.configuration == 'fullr2r' else 'false'}"]
        if args.app == "native" and args.configuration == "partialr2r":
            argv.append("-p:PublishReadyToRunCrossgen2ExtraArgs=--partial")
    started = time.time()
    result = subprocess.run(argv, cwd=source, env=serial_env, capture_output=True, text=True, timeout=180)
    log.write(json.dumps({"argv": argv, "exit": result.returncode, "stdout": result.stdout,
                          "stderr": result.stderr, "time": time.time()}) + "\n")
    if result.returncode:
        raise RuntimeError(f"Launch failed: {result.stdout[-3000:]} {result.stderr[-1000:]}")
    port = wait_agent(started)
    claim(port)
    status = status_until(port, page="drink", state="ready")
    if args.configuration == "aot" and status.get("dynamicCodeSupported") != "False":
        raise RuntimeError(f"NativeAOT not active: {status}")
    pid = int(status["pid"])
    if pid in pids:
        raise RuntimeError(f"Process {pid} was not restarted")
    pids.add(pid)
    return display_time(started), port, pid


def distribution(values):
    values = sorted(values)
    return {"n": len(values), "p50": statistics.median(values),
            "p90": values[math.ceil(0.9 * len(values)) - 1],
            "min": values[0], "max": values[-1]}


summary = {"app": args.app, "configuration": args.configuration, "package": package}
try:
    badging = run([args.aapt, "dump", "badging", str(apk)])
    if f"package: name='{package}'" not in badging:
        raise RuntimeError(f"Wrong package: {badging.splitlines()[0]}")
    with zipfile.ZipFile(apk) as archive:
        fixture = hashlib.sha256(archive.read("assets/performance/barista_notes.db")).hexdigest()
        abis = sorted({n.split("/")[1] for n in archive.namelist() if n.startswith("lib/")})
    if fixture != "7e23a022af101dd46bd596e6a377f7597b103edb0d735896dfcc6c07e6ae8fbf" or abis != ["arm64-v8a"]:
        raise RuntimeError(f"Unexpected fixture or ABI: {fixture} {abis}")
    summary.update(bytes=apk.stat().st_size, sha256=hashlib.sha256(apk.read_bytes()).hexdigest(),
                   fixture_sha256=fixture, abis=abis)
    for setting in ("window_animation_scale", "transition_animation_scale", "animator_duration_scale"):
        if float(adb("shell", "settings", "get", "global", setting).strip()) != 0:
            raise RuntimeError(f"{setting} must be zero")
    summary["thermal_before"] = adb("shell", "dumpsys", "thermalservice")
    summary["battery_before"] = adb("shell", "dumpsys", "battery")
    values = []
    with (args.output / "startup.csv").open("x", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=("phase", "sample", "pid", "displayed_ms"))
        writer.writeheader()
        for sample in range(1 if args.smoke else 36):
            elapsed, port, pid = launch()
            release(port)
            phase = "pilot" if sample == 0 else "conditioning" if sample <= 5 else "measured"
            writer.writerow(dict(phase=phase, sample=sample, pid=pid, displayed_ms=elapsed))
            stream.flush()
            if phase == "measured":
                values.append(elapsed)
    print(f"{args.app}-{args.configuration}: startup complete", flush=True)
    transitions = {}
    with (args.output / "transitions.csv").open("x", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=("route", "sample", "pid", "duration_ms"))
        writer.writeheader()
        for route in ("new-drink-to-activity", "activity-to-settings"):
            samples = []
            for sample in range(1 if args.smoke else 10):
                _, port, pid = launch()
                prepared = action(port, "prepare", route, sample)
                if prepared.get("route") != route or prepared.get("sample") != str(sample):
                    raise RuntimeError(prepared)
                if route == "activity-to-settings":
                    action(port, "activity")
                    status_until(port, page="history")
                action(port, "activity" if route == "new-drink-to-activity" else "settings")
                status = status_until(port, state="complete")
                elapsed = float(status["durationMs"])
                destination = "activity" if route == "new-drink-to-activity" else "settings"
                if (elapsed <= 0 or int(status["pid"]) != pid
                        or status.get("destination") != destination
                        or int(status["endNanos"]) <= int(status["startNanos"])):
                    raise RuntimeError(status)
                release(port)
                samples.append(elapsed)
                writer.writerow(dict(route=route, sample=sample, pid=pid, duration_ms=elapsed))
                stream.flush()
            transitions[route] = distribution(samples)
            print(f"{args.app}-{args.configuration}: {route} complete", flush=True)
    summary["startup"] = distribution(values) if values else None
    summary["transitions"] = transitions
    summary["thermal_after"] = adb("shell", "dumpsys", "thermalservice")
    summary["battery_after"] = adb("shell", "dumpsys", "battery")
    summary["process_count"] = len(pids)
    if hashlib.sha256(apk.read_bytes()).hexdigest() != summary["sha256"]:
        raise RuntimeError("The Android APK changed during collection")
    summary["status"] = "smoke-passed" if args.smoke else "complete"
except Exception as error:
    summary["status"] = "failed"
    summary["error"] = str(error)
    raise
finally:
    if active_port is not None:
        try:
            release(active_port)
        except Exception as error:
            log.write(json.dumps({"lease_release_error": str(error), "time": time.time()}) + "\n")
    (args.output / "summary.json").write_text(json.dumps(summary, indent=2))
    log.close()
print(json.dumps({key: summary[key] for key in ("app", "configuration", "status", "bytes", "startup", "transitions")}),
      flush=True)
