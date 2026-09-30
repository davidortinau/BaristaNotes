# Debug-only native Ailoha binding

This app-internal .NET Android binding consumes the **unmodified official**
`com.ailoha:ailoha-android-agent:0.1.13` AAR published by Redth. It is not a
reimplementation, a source rebuild, or a distributable Ailoha package.
`IsPackable` is false; building this project as Release fails explicitly.
If adding it to a solution, exclude this binding project from the solution's
Release Build configuration; the app's conditional ProjectReference already
handles project-to-project builds.

`Transforms/Metadata.xml` exposes only startup/shutdown and options in C#.
It does not modify the Java payload. API 24 remains the minimum. The native
minimum dependencies are NanoHTTPD 2.3.1 and Kotlin stdlib 2.2.20; the latter uses
the Microsoft `Xamarin.Kotlin.StdLib` binding and its JetBrains annotations
dependency. The host's RecyclerView graph currently resolves Kotlin 2.4.0.1;
the actual merged versions are recorded in the host configuration lock files.
There is no MAUI Controls dependency.

## Official inputs

`native-inputs.json` pins URLs, coordinates and hashes. Download the official
AAR and NanoHTTPD JAR into an external local directory, then supply that
directory with the `AilohaArtifactsDirectory` MSBuild property. The build
checks both SHA256 hashes before compilation. Do not put credentials in
MSBuild properties, URLs, NuGet configuration, project files or build logs.

The agent's published Gradle module independently supplies matching SHA256,
SHA512 and byte length. The release-tag `LICENSE` is the **Ailoha Binary
Distribution License v1.0**: consume the official binary under its application
grant. The POM has both MIT and custom-license entries; do not infer permission
to modify or redistribute source from the stale MIT entry. NanoHTTPD is
BSD-3-Clause; the Kotlin binding uses MIT AND Apache-2.0.

## Connection

The app starts the agent only under `#if DEBUG`, before Activity creation.
It uses loopback port 9233 (the official server can fall back through 9238).
Broker registration and optional network/log/profiler/WebView adapters are
disabled. This avoids the native broker's synchronous HTTP registration in
`Application.OnCreate` and needs no app-wide cleartext network override.

Use the port reported by `ILogger<NativeApplication>` and explicitly select
both the emulator and forwarded endpoint. Do not trust broker discovery to
select Android when another platform is running.

```sh
adb -s emulator-5554 forward --no-rebind tcp:19233 tcp:9233
ailoha agent status --platform android --agent-host 127.0.0.1 --agent-port 19233
ailoha ui tree --platform android --agent-host 127.0.0.1 --agent-port 19233
```

Resource IDs become stable native automation IDs. The foundation uses
`proof_increment` and `proof_count`; these are temporary proof controls, not
application workflow or source parity.

## Verified limitations

The installed .NET 11 RC binding parser emits BG8605/BG8606 for Kotlin
synthetic/internal methods containing `$` before applying metadata transforms.
`java-resolution-report.log` enumerates only those discarded methods. The
generated options constructor and static Start/Stop signatures are present.
No warnings are suppressed. Startup, native tree, button callback and changed
screenshot have been proved against the resulting binding.

The official 0.1.13 server reports a hardcoded protocol agent version `0.1.0`
and omits port in its status JSON; CLI 0.1.10 renders the missing port as zero.
Artifact version must come from the pinned official binary metadata/hash, and
the actual port from startup logs and the explicit tested endpoint.

Release exclusion must be checked in both the NuGet graph and actual APK:
no binding assembly, NanoHTTPD or Ailoha Java classes. Kotlin/annotations can
legitimately remain in Release as dependencies of the native RecyclerView.
Do not replace this with a release no-op artifact—the app requires no agent
dependency at all in Release.
