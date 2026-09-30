# Debug-only native Ailoha binding

The official native Agent API is a Swift actor, not directly Objective-C
bindable. `NativeAgentBridge.swift` is an application-owned adapter with one
Objective-C-visible completion API. The .NET binding maps only that API; it
does not copy or modify Ailoha implementation code. It invokes the throwing
startup method and verifies the listening state. The upstream convenience
helper is intentionally unused because it swallows startup errors.

This is not a standalone Ailoha package and must not be published as one.
`IsPackable=false`; building the binding in Release fails explicitly.

## Required external official package

- Release: `https://github.com/Redth/ailoha-releases/releases/tag/v0.1.13`
- Archive: `https://github.com/Redth/ailoha-releases/releases/download/v0.1.13/AilohaAgent.xcframework.zip`
- SHA-256: `7c61cc0705afd160910c962d56afa576550b60de464a4d79722514d622ec710f`
- Public package manifest: `https://github.com/Redth/ailoha-releases/blob/v0.1.13/Package.swift`
- License: `https://github.com/Redth/ailoha-releases/blob/v0.1.13/LICENSE`
- Dependency notice: `https://github.com/Redth/ailoha-releases/blob/v0.1.13/DEPENDENCIES`

Pass an absolute `AilohaPackageRoot` containing:

```text
AilohaAgent.xcframework.zip
ailoha-v0.1.13/
    AilohaAgent.xcframework/
ailoha-v0.1.13-LICENSE
ailoha-v0.1.13-DEPENDENCIES
```

Download the unmodified official archive, verify the checksum before extraction,
and preserve the license/dependency notices. Builds do not download packages or
silently fetch a different version. Native binaries are not stored in Git.
The coordinator must fingerprint this external input; this binding does not
change the central rebuild record or approval digests.

Before invoking Xcode, `build_bridge.py` checks the pinned ZIP checksum and
compares the extracted root XCFramework `Info.plist` and every file in the
selected iOS framework slice against that same archive. This includes the
framework executable, slice plist, Swift interfaces, module metadata and any
signature resources. Missing, changed, unexpected or symbolic-link inputs
stop the build before compilation and before the MSBuild target registers
native references. No archive is extracted and no external input is repaired
or overwritten automatically.

The generated `verified-native-inputs.json` in the bridge's intermediate
output lists the exact absolute paths, ZIP entries, sizes and SHA-256 values
checked for the requested runtime identifier. The device slice is
`ios-arm64`; both simulator architectures use `ios-arm64_x86_64-simulator`.
The archive and bundled license/dependency notices remain separate external
inputs; the notices are not inside this XCFramework archive. The check is
build-time input validation, not a lock against later concurrent edits.

The v0.1.13 package's custom **Ailoha Binary Distribution License 1.0** is not an
open-source license. It permits official binary consumption in applications,
including internal/evaluation use. It restricts modified versions, source
redistribution, and standalone repackaging. This integration uses the official
binary and bundles its notices in Debug only. The package says it bundles no
third-party binary libraries and links system Apple frameworks.

Both iOS and iOS Simulator slices declare iOS 15.0. The bridge supports
`iossimulator-arm64`, `iossimulator-x64`, and `ios-arm64` at build time; only the
specified arm64 simulator foundation has been runtime-checked. Device builds,
older OS versions, and other architectures are not claimed verified.

The compiler and archiver come from installed Xcode. `build_bridge.py` compiles
only this app's Swift adapter under isolated MSBuild intermediate output. The
official XCFramework is linked as a framework and is not rebuilt.
