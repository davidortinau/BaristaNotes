using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Foundry;

// The .NET 11 RC2 macios SDK expects Xcode 27.0; this Mac has 27.1. Skip the
// check. Aspire.Hosting.Maui builds the app in a child process that inherits
// this, and MSBuild reads environment variables as properties.
Environment.SetEnvironmentVariable("ValidateXcodeVersion", "false");

var builder = DistributedApplication.CreateBuilder(args);

var storage = (builder.Configuration["Parameters:storage"] ?? builder.Configuration["Demo:Storage"] ?? "sqlite").ToLowerInvariant();
if (storage is not ("sqlite" or "postgres"))
{
    throw new InvalidOperationException("The storage parameter must be 'sqlite' or 'postgres'.");
}

var storageParameter = builder.AddParameter("storage", storage)
    .WithDescription("Agent API storage. Choose SQLite or PostgreSQL, save to user secrets, then restart the AppHost to apply. Existing data is not copied between stores.")
    .WithCustomInput(parameter => new()
    {
        Name = parameter.Name,
        Label = "Agent storage",
        InputType = InputType.Choice,
        Required = true,
        Value = storage,
        Options = [
            new("sqlite", "SQLite"),
            new("postgres", "PostgreSQL"),
        ],
        Description = parameter.Description,
        EnableDescriptionMarkdown = parameter.EnableDescriptionMarkdown,
    });

IResourceBuilder<PostgresDatabaseResource>? agentDatabase = null;
if (storage == "postgres")
{
    agentDatabase = builder.AddPostgres("postgres")
        .WithDataVolume()
        .AddDatabase("agentdb");
#pragma warning disable ASPIREPOSTGRES001
    agentDatabase.WithPostgresMcp(mcp => mcp.WithArgs(args =>
    {
        args.Args.Remove("--access-mode=unrestricted");
        args.Args.Add("--access-mode=restricted");
    }));
#pragma warning restore ASPIREPOSTGRES001
}

// An explicit connection string reuses an existing account; otherwise Aspire
// provisions Foundry and the models used by the agent and the app.
IResourceBuilder<IResourceWithConnectionString> foundry;
if (!string.IsNullOrWhiteSpace(builder.Configuration["ConnectionStrings:foundry"]))
{
    foundry = builder.AddConnectionString("foundry");
}
else
{
    var account = builder.AddFoundry("foundry");
    account.AddDeployment("chat", FoundryModel.OpenAI.Gpt41Mini)
        .WithProperties(d => { d.DeploymentName = "gpt-4.1-mini"; d.SkuName = "GlobalStandard"; d.SkuCapacity = 10; });
    // Keep the app's deployment names while using models available for new accounts.
    account.AddDeployment("vision", FoundryModel.OpenAI.Gpt41)
        .WithProperties(d => { d.DeploymentName = "gpt-4o"; d.SkuName = "GlobalStandard"; d.SkuCapacity = 10; });
    account.AddDeployment("extraction", FoundryModel.OpenAI.Gpt41Mini)
        .WithProperties(d => { d.DeploymentName = "gpt-4o-mini"; d.SkuName = "GlobalStandard"; d.SkuCapacity = 10; });
    foundry = account;
}

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
    .WithHttpCommand("/api/demo/faults", "Disarm order timeout", commandName: "disarm-timeout", commandOptions: Json(
        """{"order_timeout_armed":false}""",
        "Turns the order timeout off if no order used it.",
        "TimerOff"))
    .WithHttpCommand("/api/demo/faults", "Ignore idempotency keys", commandName: "ignore-keys", commandOptions: Json(
        """{"ignore_idempotency_keys":true}""",
        "Simulates a shop that ignores Idempotency-Key, so a retried order becomes a second order.",
        "Bug"))
    .WithHttpCommand("/api/demo/faults", "Honor idempotency keys", commandName: "honor-keys", commandOptions: Json(
        """{"ignore_idempotency_keys":false}""",
        "The shop returns the first order when the same Idempotency-Key arrives again.",
        "ShieldCheckmark"))
    .WithHttpCommand("/api/demo/faults", "Raise prices 10%", commandName: "raise-prices", commandOptions: Json(
        """{"price_increase_percent":10}""",
        "Every price goes up 10%. A pick made before this is out of date, and Buy refuses it.",
        "ArrowTrendingLines"))
    .WithHttpCommand("/api/demo/faults", "Normal prices", commandName: "normal-prices", commandOptions: Json(
        """{"price_increase_percent":0}""",
        "Prices go back to the catalog prices.",
        "ArrowUndo"));

var api = builder.AddProject<Projects.BaristaNotes_Api>("api")
    .WithEnvironment("Agent__Storage", storageParameter)
    .WithReference(foundry)
    .WaitFor(foundry)
    .WithReference(shop)
    .WaitFor(shop)
    .WithEnvironment("Webhooks__Secret", webhookSecret)
    .WithHttpCommand("/runs", "Run agent", commandName: "run-agent", commandOptions: Json(
        """{"trigger":"dashboard"}""", "Starts an agent run, the same as Run now in the app.", "Play", highlighted: true))
    .WithHttpCommand("/runs/stop", "Stop run", commandName: "stop-run", commandOptions: Json(
        "{}", "Stops the run that is going. Effects that already happened stay in the ledger.", "Stop"))
    .WithHttpCommand("/demo/pace", "Stop window on (3 s)", commandName: "pace-slow", commandOptions: Json(
        """{"effectPauseMs":3000}""", "The run pauses 3 s after each cart or order, so there is time to tap Stop (TF-14). Other steps keep their pace.", "Pause"))
    .WithHttpCommand("/demo/pace", "Stop window off", commandName: "pace-normal", commandOptions: Json(
        """{"effectPauseMs":0,"stepDelayMs":900}""", "Back to the normal stage pace (900 ms steps, no pause after effects).", "Play"))
    .WithHttpCommand("/demo/model-mode", "Use rehearsal answers", commandName: "model-replay", commandOptions: Json(
        """{"mode":"Only"}""", "For bad conference Wi-Fi: the agent uses answers recorded in rehearsal, and the ledger says so.", "PlugDisconnected"))
    .WithHttpCommand("/demo/model-mode", "Use live model", commandName: "model-live", commandOptions: Json(
        """{"mode":"Auto"}""", "Calls Foundry, records each answer, and falls back to a recording only if Foundry cannot be reached.", "PlugConnected"))
    .WithHttpCommand("/demo/reset", "Reset demo", commandName: "reset-demo", commandOptions: Json(
        "{}", "Clears runs, picks, orders, check-ins, and memory in the API and the shop.", "ArrowReset",
        confirm: "Reset the API and the shop? The app's demo data is reset from its Settings page."));

if (agentDatabase is not null)
{
    api.WithReference(agentDatabase).WaitFor(agentDatabase);
}

// The shop sends order webhooks to the API through service discovery.
shop.WithReference(api);

#pragma warning disable ASPIREBROWSERLOGS001
builder.AddViteApp("web", "../BaristaNotes.Web")
    .WithReference(api)
    .WaitFor(api)
    .WithBrowserLogs();
#pragma warning restore ASPIREBROWSERLOGS001

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
        // The API and the shop refuse state changes without this header (cross-site protection).
        context.Request.Headers.Add("X-Requested-By", "Aspire dashboard");
        return Task.CompletedTask;
    },
};
