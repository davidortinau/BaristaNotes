# Android setup

Use this reference when a native Android app is not wired for Ailoha or when the
startup path looks unsafe.

## 1. Add Gradle dependencies

Use the real agent only in debug builds:

```kotlin
dependencies {
    debugImplementation("com.ailoha:ailoha-android-agent:$ailohaVersion")
    releaseImplementation("com.ailoha:ailoha-android-agent-noop:$ailohaVersion")
}
```

Add optional artifacts only when the app needs them:

```kotlin
debugImplementation("com.ailoha:ailoha-android-agent-compose:$ailohaVersion")
debugImplementation("com.ailoha:ailoha-android-agent-okhttp:$ailohaVersion")
debugImplementation("com.ailoha:ailoha-android-agent-timber:$ailohaVersion")
```

If the team is consuming a local checkout or CI build, use the package source
described in `android/README.md` instead of inventing alternate coordinates.

## 2. Start in debug-only app startup

```kotlin
class App : Application() {
    override fun onCreate() {
        super.onCreate()
        if (BuildConfig.DEBUG) {
            AilohaAndroidAgent.start(
                application = this,
                options = AilohaAgentOptions(appName = "My Android App")
            )
        }
    }
}
```

Setup rules:

- Prefer a debug source-set `Application` class when possible.
- Keep startup behind `BuildConfig.DEBUG` if shared source must reference it.
- Leave the port at `9233` unless a test matrix explicitly needs another port.
- Keep broker registration enabled by default.

## 3. Add Compose support only for Compose apps

```kotlin
if (BuildConfig.DEBUG) {
    AilohaAndroidAgent.start(this)
    AilohaComposeAgent.install()
}
```

Compose support maps semantics nodes and `Modifier.testTag("...")` to protocol
elements. Do not add the Compose artifact to a Views-only app unless needed.

## 4. Add stable identifiers

Views:

```kotlin
binding.emailInput.setAilohaAutomationId("EmailInput")
```

Resource IDs also become automation IDs when available:

```xml
<EditText
    android:id="@+id/EmailInput" />
```

Compose:

```kotlin
TextField(
    value = email,
    onValueChange = { email = it },
    modifier = Modifier.testTag("EmailInput")
)
```

## 5. Verify the integration

After launching the app in Debug mode:

```bash
adb forward tcp:9233 tcp:9233
ailoha diagnose
ailoha agent wait --timeout 60
ailoha agent status
ailoha ui tree --depth 2
```

Use direct curl only for fallback-port validation:

```bash
curl http://localhost:9233/api/v1/agent/status
```
