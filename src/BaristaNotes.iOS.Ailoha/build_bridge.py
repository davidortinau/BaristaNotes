"""Compile the application's small bridge; never rebuild or modify Ailoha itself."""
import hashlib
import json
from pathlib import Path
import stat
import subprocess
import sys
import zipfile


ARCHIVE_SHA256 = "7c61cc0705afd160910c962d56afa576550b60de464a4d79722514d622ec710f"


def verify_framework(package, slice_name):
    archive = package / "AilohaAgent.xcframework.zip"
    root = package / "ailoha-v0.1.13" / "AilohaAgent.xcframework"
    selected = root / slice_name / "AilohaAgent.framework"
    for path in (package, archive, root.parent, root, selected.parent, selected):
        if path.is_symlink():
            raise ValueError(f"Ailoha input must not be a symbolic link: {path}")
    if not archive.is_file():
        raise FileNotFoundError(archive)
    if not selected.is_dir():
        raise FileNotFoundError(selected)

    expected = {}
    with archive.open("rb") as stream:
        if hashlib.file_digest(stream, "sha256").hexdigest() != ARCHIVE_SHA256:
            raise ValueError("Ailoha archive does not match the official v0.1.13 checksum.")
        stream.seek(0)
        with zipfile.ZipFile(stream) as package_zip:
            prefix = f"AilohaAgent.xcframework/{slice_name}/AilohaAgent.framework/"
            for entry in package_zip.infolist():
                if entry.filename != "AilohaAgent.xcframework/Info.plist" and not entry.filename.startswith(prefix):
                    continue
                if entry.is_dir():
                    continue
                if not stat.S_ISREG(entry.external_attr >> 16):
                    raise ValueError(f"Unsupported Ailoha archive entry: {entry.filename}")
                relative = entry.filename.removeprefix("AilohaAgent.xcframework/")
                if relative in expected:
                    raise ValueError(f"Duplicate Ailoha archive entry: {entry.filename}")
                with package_zip.open(entry) as content:
                    expected[relative] = {
                        "archive_entry": entry.filename,
                        "size_bytes": entry.file_size,
                        "sha256": hashlib.file_digest(content, "sha256").hexdigest(),
                    }
    if "Info.plist" not in expected or len(expected) < 2:
        raise ValueError("The pinned archive is missing root metadata or the selected framework slice.")

    actual = {"Info.plist"}
    for path in selected.rglob("*"):
        if path.is_symlink():
            raise ValueError(f"Ailoha input must not be a symbolic link: {path}")
        if path.is_file():
            actual.add(path.relative_to(root).as_posix())
        elif not path.is_dir():
            raise ValueError(f"Ailoha input must be a regular file or directory: {path}")
    missing = expected.keys() - actual
    extra = actual - expected.keys()
    if missing:
        raise ValueError(f"Ailoha framework is missing archive files: {', '.join(sorted(missing))}")
    if extra:
        raise ValueError(f"Ailoha framework has unexpected files: {', '.join(sorted(extra))}")

    verified = []
    for relative, record in sorted(expected.items()):
        path = root / relative
        if path.is_symlink() or not path.is_file():
            raise ValueError(f"Ailoha input must be a regular file: {path}")
        with path.open("rb") as content:
            digest = hashlib.file_digest(content, "sha256").hexdigest()
        if digest != record["sha256"] or path.stat().st_size != record["size_bytes"]:
            raise ValueError(f"Ailoha extracted input differs from the pinned archive: {path}")
        verified.append({"path": str(path), **record})

    return {
        "release": "v0.1.13",
        "archive_path": str(archive),
        "archive_sha256": ARCHIVE_SHA256,
        "selected_slice": slice_name,
        "verified_inputs": verified,
    }


def run(arguments):
    subprocess.run(arguments, check=True, timeout=120)


def main():
    if len(sys.argv) != 4:
        raise ValueError("Expected absolute package root, absolute output directory, and iOS runtime identifier.")
    package, output = (Path(value) for value in sys.argv[1:3])
    if not package.is_absolute() or not output.is_absolute() or not package.is_dir():
        raise ValueError("Package and output paths must be absolute; package must exist.")
    targets = {
        "iossimulator-arm64": ("iphonesimulator", "arm64-apple-ios15.0-simulator", "ios-arm64_x86_64-simulator"),
        "iossimulator-x64": ("iphonesimulator", "x86_64-apple-ios15.0-simulator", "ios-arm64_x86_64-simulator"),
        "ios-arm64": ("iphoneos", "arm64-apple-ios15.0", "ios-arm64"),
    }
    if sys.argv[3] not in targets:
        raise ValueError("Specify iossimulator-arm64, iossimulator-x64, or ios-arm64 explicitly.")
    sdk_name, triple, slice_name = targets[sys.argv[3]]
    verified = verify_framework(package, slice_name)
    framework = package / "ailoha-v0.1.13" / "AilohaAgent.xcframework" / slice_name
    sdk = subprocess.run(["xcrun", "--sdk", sdk_name, "--show-sdk-path"],
                         check=True, capture_output=True, text=True, timeout=30).stdout.strip()
    output.mkdir(parents=True, exist_ok=True)
    with (output / "verified-native-inputs.json").open("w", encoding="utf-8") as manifest:
        json.dump(verified, manifest, indent=2)
        manifest.write("\n")
    source = Path(__file__).resolve().parent / "NativeAgentBridge.swift"
    object_file = output / "NativeAgentBridge.o"
    run(["xcrun", "swiftc", "-parse-as-library", "-emit-object", "-D", "DEBUG",
         "-target", triple, "-sdk", sdk, "-F", str(framework),
         "-module-name", "BaristaNotesAilohaBridge", "-module-cache-path", str(output / "module-cache"),
         "-emit-objc-header", "-emit-objc-header-path", str(output / "BaristaNotesAilohaBridge-Swift.h"),
         str(source), "-o", str(object_file)])
    run(["xcrun", "ar", "-rcs", str(output / "libBaristaNotesAilohaBridge.a"), str(object_file)])


if __name__ == "__main__":
    main()
