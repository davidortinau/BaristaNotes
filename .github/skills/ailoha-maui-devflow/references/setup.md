# MAUI setup

Use this reference when the project is missing Ailoha or when the existing
registration is incomplete.

## 1. Add the packages

For a standard MAUI app, add the core package reference to the app project:

```xml
<ItemGroup>
  <PackageReference Include="Ailoha.Agent.Maui" Version="x.y.z" />
</ItemGroup>
```

Only add the Blazor package if the app uses `BlazorWebView`:

```xml
<ItemGroup>
  <PackageReference Include="Ailoha.Blazor" Version="x.y.z" />
</ItemGroup>
```

If the team is consuming CI builds instead of the public package feed, follow
the repo's `maui/README.md` guidance for GitHub Packages credentials rather than
inventing a new feed layout.

## 2. Register in `MauiProgram.cs`

```csharp
using Ailoha.Agent;

public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    builder.UseMauiApp<App>();

#if DEBUG
    builder.AddMauiDevFlowAgent(options =>
    {
        options.EnableProfiler = true;
    });
#endif

    return builder.Build();
}
```

If the app uses `BlazorWebView` and references `Ailoha.Blazor`, also add:

```csharp
using Ailoha.Blazor;

#if DEBUG
builder.AddMauiBlazorDevFlowTools();
#endif
```

### Setup rules

- Keep the whole block inside `#if DEBUG`.
- Prefer leaving the port at its default unless you need a fixed override.
- Turn on the profiler intentionally; it is useful, but not required for basic
  inspection.

## 3. Add stable identifiers

Prefer `AutomationId` for important controls:

```xml
<Entry AutomationId="EmailInput" Placeholder="Email" />
<Button AutomationId="SubmitButton" Text="Submit" />
```

This is the cheapest way to make `ui query` reliable.

## 4. Optional port overrides

The CLI resolves the agent in this order:

`explicit args -> env/config override -> broker discovery -> localhost:9233`

Only reach for overrides when you need deterministic behavior:

- MSBuild override: `-p:AilohaPort=9233`
- project-level agent config: `.ailoha`

Example `.ailoha`:

```json
{ "port": 9233 }
```

Prefer broker discovery for normal development.

## 5. Verify the integration

After launching the app in Debug mode:

```bash
ailoha diagnose
ailoha agent wait --timeout 60
ailoha agent status
ailoha ui tree --depth 2
```

For a direct smoke test against the fallback port:

```bash
curl http://localhost:9233/api/v1/agent/status
```

Only use the direct HTTP check when you are intentionally validating the fallback
path or debugging port resolution.
