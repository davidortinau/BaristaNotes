// The .NET 11 RC2 macios SDK expects Xcode 27.0; this Mac has 27.1. Skip the
// check. Aspire.Hosting.Maui builds the app in a child process that inherits
// this, and MSBuild reads environment variables as properties.
Environment.SetEnvironmentVariable("ValidateXcodeVersion", "false");

var builder = DistributedApplication.CreateBuilder(args);

// Foundry endpoint and key. Set in this project's user secrets:
// ConnectionStrings:foundry = "Endpoint=https://...;Key=..."
// Only the API reads it. The app never receives the key.
var foundry = builder.AddConnectionString("foundry");

// The shop signs its order webhooks with this secret, and the API checks the
// signature. Generated once and kept in user secrets.
var webhookSecret = builder.AddParameter("webhook-secret",
    new GenerateParameterDefault { MinLength = 32, Special = false }, secret: true, persist: true);

var shop = builder.AddProject<Projects.BaristaNotes_RoasterShop>("shop")
    .WithEnvironment("Webhooks__Secret", webhookSecret)
    .WithHttpCommand("/api/demo/faults", "Arm order timeout", commandName: "arm-timeout", commandOptions: Json(
        """{"order_timeout_armed":true}""",
        "The next order is created, then the shop waits longer than the API's timeout before answering.",
        "Timer"))
    .WithHttpCommand("/api/demo/faults", "Ignore idempotency keys", commandName: "ignore-keys", commandOptions: Json(
        """{"ignore_idempotency_keys":true}""",
        "Simulates a shop that ignores Idempotency-Key, so a retried order becomes a second order.",
        "Bug"))
    .WithHttpCommand("/api/demo/faults", "Honor idempotency keys", commandName: "honor-keys", commandOptions: Json(
        """{"ignore_idempotency_keys":false}""",
        "The shop returns the first order when the same Idempotency-Key arrives again.",
        "ShieldCheckmark"));

var api = builder.AddProject<Projects.BaristaNotes_Api>("api")
    .WithReference(foundry)
    .WithReference(shop)
    .WaitFor(shop)
    .WithEnvironment("Webhooks__Secret", webhookSecret)
    .WithHttpCommand("/runs", "Run agent", commandName: "run-agent", commandOptions: Json(
        """{"trigger":"dashboard"}""", "Starts an agent run, the same as Run now in the app.", "Play", highlighted: true))
    .WithHttpCommand("/runs/stop", "Stop run", commandName: "stop-run", commandOptions: Json(
        "{}", "Stops the run that is going. Effects that already happened stay in the ledger.", "Stop"))
    .WithHttpCommand("/demo/model-mode", "Use rehearsal answers", commandName: "model-replay", commandOptions: Json(
        """{"mode":"Only"}""", "For bad conference Wi-Fi: the agent uses answers recorded in rehearsal, and the ledger says so.", "PlugDisconnected"))
    .WithHttpCommand("/demo/model-mode", "Use live model", commandName: "model-live", commandOptions: Json(
        """{"mode":"Auto"}""", "Calls Foundry, records each answer, and falls back to a recording only if Foundry cannot be reached.", "PlugConnected"))
    .WithHttpCommand("/demo/reset", "Reset demo", commandName: "reset-demo", commandOptions: Json(
        "{}", "Clears runs, picks, orders, check-ins, and memory in the API and the shop.", "ArrowReset",
        confirm: "Reset the API and the shop? The app's demo data is reset from its Settings page."));

// The shop sends order webhooks to the API through service discovery.
shop.WithReference(api);

var app = builder.AddMauiProject("app", "../BaristaNotes/BaristaNotes.csproj");

app.AddMacCatalystDevice("app-maccatalyst")
    .WithReference(api)
    // Use a separate demo database so David's own data is not touched.
    .WithEnvironment("BARISTANOTES_DEMO", "1");

// Fallback stage head, and the target for UI automation of popups. Set the
// simulator per machine in user secrets: Demo:IosSimulatorUdid. The simulator
// must trust the ASP.NET Core dev certificate (xcrun simctl keychain <udid> add-root-cert).
var simulatorUdid = builder.Configuration["Demo:IosSimulatorUdid"];
(string.IsNullOrWhiteSpace(simulatorUdid)
        ? app.AddiOSSimulator("app-ios")
        : app.AddiOSSimulator("app-ios", simulatorUdid))
    .WithReference(api)
    .WithEnvironment("BARISTANOTES_DEMO", "1");

builder.Build().Run();

static HttpCommandOptions Json(string body, string description, string icon, bool highlighted = false, string? confirm = null) => new()
{
    Method = HttpMethod.Post,
    Description = description,
    IconName = icon,
    IsHighlighted = highlighted,
    ConfirmationMessage = confirm,
    PrepareRequest = context =>
    {
        context.Request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        return Task.CompletedTask;
    },
};
