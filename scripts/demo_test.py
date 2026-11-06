#!/usr/bin/env python3
"""Run the BaristaNotes MAUI Android demo through DevFlow UI controls."""

import argparse
from datetime import datetime
import json
import os
from pathlib import Path
import re
import selectors
import shlex
import shutil
import subprocess
import sys
import time
import uuid


ROOT = Path(__file__).resolve().parents[1]
PROJECT = "src/BaristaNotes/BaristaNotes.csproj"
PACKAGE = "com.simplyprofound.baristanotes"
CLI = ["dotnet", "maui", "devflow"]


def decode(output):
    # Batch output can include a target diagnostic after the JSON document.
    start = re.search(r"(?m)^[\[{]", output)
    if start is None:
        raise ValueError(f"DevFlow did not return JSON: {output[:500]}")
    return json.JSONDecoder().raw_decode(output[start.start():])[0]


def walk(node, parents=()):
    yield node, parents
    for child in node.get("children", []):
        yield from walk(child, (*parents, node))


def active_page(tree, route):
    section_name = {
        "shots": "New Drink", "history": "Activity", "settings": "Settings"
    }[route.lstrip("/").split("/")[0]]
    sections = [
        node for root in tree for node, _ in walk(root)
        if node.get("type") == "ShellSection" and node.get("text") == section_name
    ]
    if len(sections) != 1:
        raise RuntimeError(f"Cannot identify active section for {route}")
    pages = [
        node for node, _ in walk(sections[0])
        if "ContentPage" in node.get("type", "")
    ]
    if not pages:
        raise RuntimeError(f"No page for {route}")
    return pages[-1]


def run_command(args, output=None, timeout=60):
    result = subprocess.run(
        args, cwd=ROOT, text=True, stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT, timeout=timeout, check=False,
    )
    if output:
        output.write_text(result.stdout)
    if result.returncode:
        raise RuntimeError(f"{shlex.join(args)} failed:\n{result.stdout[-6000:]}")
    return result.stdout


def connect(device, output, deploy):
    adb = shutil.which("adb") or str(
        Path(os.environ.get("ANDROID_HOME", Path.home() / "Library/Android/sdk"))
        / "platform-tools/adb"
    )
    run_command([adb, "-s", device, "wait-for-device"], timeout=30)
    run_command([*CLI, "broker", "start"])
    run_command([adb, "-s", device, "reverse", "tcp:19223", "tcp:19223"])
    if deploy:
        run_command([
            "dotnet", "build", PROJECT, "-f", "net11.0-android", "-c", "Debug",
            "-t:Run", f"-p:AdbTarget=-s {device}", "-v:q",
        ], output / "build.log", timeout=600)
    run_command([
        *CLI, "wait", "--wait-platform", "Android", "--timeout", "30",
        "--device", device,
    ], output / "wait.json", timeout=40)
    agents = decode(run_command([*CLI, "list"]))
    candidates = [
        agent for agent in agents
        if agent.get("platform") == "Android"
        and agent.get("appName") == "BaristaNotes"
        and Path(agent.get("project", "")).name == "BaristaNotes.csproj"
    ]
    if len(candidates) != 1:
        raise RuntimeError(f"Expected one BaristaNotes MAUI Android agent: {candidates}")
    port = str(candidates[0]["port"])
    run_command([adb, "-s", device, "forward", f"tcp:{port}", f"tcp:{port}"])
    target = ["--agent-port", port, "--device", device, "-p", "android"]
    status = decode(run_command([*CLI, "ui", "status", *target]))
    if status["app"]["packageId"] != PACKAGE:
        raise RuntimeError(f"Wrong application on {device}: {status['app']}")
    (output / "target.json").write_text(json.dumps(status, indent=2))
    return target


class Demo:
    def __init__(self, target, output, pace):
        self.output = output
        self.pace = pace
        self.transcript = (output / f"actions-{time.time_ns()}.jsonl").open("w")
        self.stderr = (output / f"devflow-{time.time_ns()}.log").open("w")
        self.process = subprocess.Popen(
            [*CLI, "batch", *target, "--delay", "0"], cwd=ROOT,
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=self.stderr,
            text=True, bufsize=1,
        )
        self.reader = selectors.DefaultSelector()
        self.reader.register(self.process.stdout, selectors.EVENT_READ)

    def close(self):
        self.process.stdin.close()
        try:
            self.process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            self.process.terminate()
            self.process.wait(timeout=10)
        self.reader.close()
        self.stderr.close()
        self.transcript.close()

    def command(self, *args):
        command = "MAUI " + " ".join(json.dumps(str(arg)) for arg in args)
        self.process.stdin.write(command + "\n")
        self.process.stdin.flush()
        if not self.reader.select(timeout=30):
            raise TimeoutError(f"DevFlow response timed out: {command}")
        line = self.process.stdout.readline()
        if not line:
            raise RuntimeError(f"DevFlow batch stopped: {command}")
        response = json.loads(line)
        self.transcript.write(json.dumps({
            "at": datetime.now().isoformat(), **response
        }) + "\n")
        self.transcript.flush()
        if response["exit_code"]:
            raise RuntimeError(f"{command}: {response['output']}")
        result = decode(response["output"])
        if isinstance(result, dict) and result.get("success") is False:
            raise RuntimeError(f"{command}: {result}")
        return result

    def page(self):
        route = self.command("status")["route"]
        tree = self.command("tree", "--depth", "30", "--format", "compact")
        return active_page(tree, route)

    def find(self, *, text=None, aid=None, kind=None, contains=False, timeout=15):
        deadline = time.monotonic() + timeout
        while True:
            matches = [
                (node, parents) for node, parents in walk(self.page())
                if (kind is None or node.get("type") == kind)
                and (aid is None or node.get("automationId") == aid)
                and (text is None or (
                    text in node.get("text", "") if contains
                    else node.get("text") == text
                ))
            ]
            if matches:
                return matches
            if time.monotonic() >= deadline:
                raise RuntimeError(f"UI element not found: {text or aid or kind}")
            time.sleep(0.2)

    def tap(self, node):
        self.command("tap", node["id"])
        time.sleep(self.pace)

    def tap_id(self, aid):
        self.tap(self.find(aid=aid)[0][0])

    def tap_text(self, text, parent_type=None, index=0, contains=False):
        node, parents = self.find(text=text, contains=contains)[index]
        if parent_type == "Adaptive":
            container = next(
                p for p in reversed(parents)
                if any(c["type"] == "Button" for c in p.get("children", []))
            )
            node = next(c for c in container["children"] if c["type"] == "Button")
        elif parent_type:
            node = next(p for p in reversed(parents) if p["type"] == parent_type)
        self.tap(node)

    def find_in_list(self, text, aid=None, contains=False):
        deadline = time.monotonic() + 30
        index = 0
        previous = None
        while time.monotonic() < deadline:
            page = self.page()
            collection = next(
                n for n, _ in walk(page)
                if n["type"] == "CollectionView"
                and (aid is None or n.get("automationId") == aid)
            )
            matches = [
                (n, parents) for n, parents in walk(collection)
                if (text in n.get("text", "") if contains else n.get("text") == text)
            ]
            if matches:
                return matches[0]
            fingerprint = tuple(n.get("text") for n, _ in walk(collection) if "text" in n)
            if index > 4 and fingerprint == previous:
                break
            previous = fingerprint
            self.command(
                "scroll", "--element", collection["id"],
                "--item-index", index, "--position", "Start",
            )
            index += 4
            time.sleep(0.2)
        raise RuntimeError(f"Item not found in the UI list: {text}")

    def fill_field(self, label, value):
        _, parents = self.find(text=label)[0]
        tile = next(p for p in reversed(parents) if p["type"] == "Border")
        entry = next(n for n, _ in walk(tile) if n["type"] == "Entry")
        self.command("fill", entry["id"], value)
        self.find(text=value, kind="Entry")
        time.sleep(self.pace)

    def checkpoint(self, name):
        self.command("screenshot", "--output", self.output / f"{name}.png")
        (self.output / f"{name}.json").write_text(
            json.dumps(self.page(), indent=2)
        )
        print(name, flush=True)

    def assert_tile(self, label, expected):
        _, parents = self.find(text=label)[0]
        tile = next(p for p in reversed(parents) if p["type"] == "Border")
        texts = [n.get("text") for n, _ in walk(tile)]
        if expected not in texts:
            raise AssertionError(f"{label}: expected {expected!r}, found {texts}")

    def select_mass(self, label, whole):
        self.tap_text(label, "Border")
        self.find(text="Done", kind="Button")
        self.checkpoint(f"{label.lower().replace(' ', '-')}-before")
        self.select_number("MassWhole", whole)
        self.tap_id("MassTenth_0")
        self.tap_text("Done")
        self.assert_tile(label, str(whole))

    def select_number(self, prefix, value, step=1):
        page = self.page()
        collection = next(
            node for node, _ in walk(page) if node["type"] == "CollectionView"
            and any(
                child.get("automationId", "").startswith(prefix + "_")
                for child, _ in walk(node)
            )
        )
        self.command(
            "scroll", "--element", collection["id"],
            "--item-index", "0", "--position", "Center",
        )
        first = min(
            float(node["automationId"].split("_", 1)[1])
            for node, _ in walk(self.page())
            if node.get("automationId", "").startswith(prefix + "_")
        )
        index = (value - first) / step
        if index < 0 or not index.is_integer():
            raise RuntimeError(f"{prefix}: {value} is outside the picker range")
        self.command(
            "scroll", "--element", collection["id"],
            "--item-index", int(index), "--position", "Center", "--animated",
        )
        self.tap_id(f"{prefix}_{value}")

    def verify_drink(self, bean, profile):
        for label, expected in (
            ("METHOD", "Espresso"), ("DRINK TYPE", "Cortado"),
            ("DOSE IN", "19"), ("YIELD", "42"), ("TIME", "31"),
        ):
            self.assert_tile(label, expected)
        self.find(text=bean, contains=True)
        self.find(text=profile)
        # The temperature tile displays both units.
        self.assert_tile("WATER TEMP", self.temperature)

    def journey(self, bean, profile):
        self.find(aid="DrinkSettingsButton")
        self.checkpoint("01-start")
        self.tap_id("DrinkSettingsButton")
        self.tap_id("beans")
        self.tap_id("AddBeanButton")
        self.find(text="NEW BEAN")
        for label, value in (
            ("NAME", bean), ("ROASTER", "Demo Roasters"), ("ORIGIN", "Ethiopia"),
        ):
            self.fill_field(label, value)
        self.checkpoint("02-bean-form")
        self.tap_text("ADD", "Border")
        self.find(aid="AddBeanButton")
        self.find_in_list(bean)
        self.checkpoint("03-bean-saved")
        self.tap_text(bean, "Adaptive")
        self.find(text="EDIT BEAN")
        self.find(text="Demo Roasters", kind="Entry")
        self.find(text="Ethiopia", kind="Entry")
        self.checkpoint("04-bean-reopened")
        self.tap_text("CANCEL", "Border")
        self.tap_id("BeansSettingsButton")
        self.tap_id("profiles")
        self.tap_id("AddProfileButton")
        self.find(aid="ProfileNameEntry")
        self.command("fill", "ProfileNameEntry", profile)
        self.command("fill", "ProfileContextEditor", "Prefers milk drinks.")
        self.checkpoint("05-profile-form")
        self.tap_text("ADD", "Border")
        self.find(aid="AddProfileButton")
        self.find_in_list(profile)
        self.checkpoint("06-profile-saved")
        self.tap_text(profile, "Adaptive")
        self.find(text="EDIT PROFILE")
        self.find(text=profile, kind="Entry")
        self.find(text="Prefers milk drinks.", kind="Editor")
        self.tap_text("CANCEL", "Border")
        self.tap_id("ProfilesDrinkButton")
        self.tap_text("METHOD", "Border")
        self.find(text="BREW METHOD")
        self.checkpoint("07-method-choices")
        self.tap_text("V60", "Grid", index=-1)
        self.assert_tile("METHOD", "V60")
        self.checkpoint("07a-v60-selected")
        self.tap_text("METHOD", "Border")
        self.tap_text("Espresso", "Grid", index=-1)
        self.tap_text("BAG", "Border")
        self.find_in_list(bean, contains=True)
        self.checkpoint("08-bag-choices")
        self.tap_text(bean, "Grid", contains=True, index=-1)
        self.tap_text("DRINK TYPE", "Border")
        self.find(text="Cortado")
        self.checkpoint("09-drink-choices")
        self.tap_text("Cortado", "Grid")
        self.select_mass("DOSE IN", 19)
        self.select_mass("YIELD", 42)
        self.tap_text("TIME", "Border")
        self.select_number("NumericValue", 31)
        self.tap_text("Done")
        self.assert_tile("TIME", "31")
        self.tap_text("WATER TEMP", "Border")
        self.find(text="WATER TEMP")
        # Preserve the current unit preference. 199 F is about 93 C.
        texts = [n.get("text", "") for n, _ in walk(self.page())]
        self.temperature = "199" if any("°F" in text for text in texts) else "93"
        self.select_number(
            "NumericValue", int(self.temperature),
            step=1 if self.temperature == "199" else 0.5,
        )
        self.tap_text("Done")
        self.tap_text("MADE BY / FOR", "Border")
        self.find(text="Done", kind="Button")
        self.checkpoint("10-people-choices")
        _, parents = self.find_in_list(profile, aid="DrinkMadeForPicker")
        self.tap(next(p for p in reversed(parents) if p["type"] == "Grid"))
        self.tap_text("Done")
        self.verify_drink(bean, profile)
        self.checkpoint("11-configured-drink")
        self.tap_text("Log Drink", "Border")
        self.find(aid="DrinkHistoryButton")
        self.tap_id("DrinkHistoryButton")
        self.find_in_list(bean, contains=True)
        self.checkpoint("12-saved-in-history")
        self.tap_text(bean, "Border", contains=True)
        self.verify_drink(bean, profile)
        self.checkpoint("13-saved-drink-reopened")

    def verify_after_restart(self, bean, profile, temperature):
        self.temperature = temperature
        self.tap_id("DrinkHistoryButton")
        self.find_in_list(bean, contains=True)
        self.tap_text(bean, "Border", contains=True)
        self.verify_drink(bean, profile)
        self.checkpoint("14-persisted-after-restart")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--device", required=True, help="Booted emulator serial from the mobile canvas")
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--skip-deploy", action="store_true", help="Development only")
    parser.add_argument("--pace", type=float, default=0.35, help="Pause after UI actions for viewing")
    args = parser.parse_args()
    if not args.device.startswith("emulator-"):
        parser.error("This demo only deploys to Android emulators.")
    if args.pace < 0:
        parser.error("--pace must not be negative.")
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S-%f")
    output = (args.output_dir or ROOT / ".artifacts/demo-test" / stamp).resolve()
    output.mkdir(parents=True, exist_ok=False)
    suffix = uuid.uuid4().hex[:6].upper()
    bean = f"Ethiopia {suffix}"
    profile = f"Guest {suffix}"
    result = {"success": False, "bean": bean, "profile": profile, "device": args.device}
    demo = None
    try:
        target = connect(args.device, output, not args.skip_deploy)
        demo = Demo(target, output, args.pace)
        started = time.monotonic()
        demo.journey(bean, profile)
        temperature = demo.temperature
        demo.close()
        demo = None
        target = connect(args.device, output, deploy=True)
        demo = Demo(target, output, args.pace)
        demo.verify_after_restart(bean, profile, temperature)
        result["durationSeconds"] = round(time.monotonic() - started, 2)
        result["success"] = True
    except (RuntimeError, AssertionError, TimeoutError, subprocess.SubprocessError, OSError, ValueError, KeyError, StopIteration) as error:
        result["error"] = str(error)
        if demo:
            try:
                demo.checkpoint("failure")
            except (RuntimeError, TimeoutError, OSError, ValueError, BrokenPipeError) as capture_error:
                result["captureError"] = str(capture_error)
        print(f"FAILED: {error}", file=sys.stderr)
    finally:
        if demo:
            demo.close()
        (output / "result.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps({**result, "output": str(output)}, indent=2))
    return 0 if result["success"] else 1


if __name__ == "__main__":
    sys.exit(main())
