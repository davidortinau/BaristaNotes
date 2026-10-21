// The .NET 11 RC2 macios SDK expects Xcode 27.0; this Mac has 27.1. Skip the
// check. Aspire.Hosting.Maui builds the app in a child process that inherits
// this, and MSBuild reads environment variables as properties.
Environment.SetEnvironmentVariable("ValidateXcodeVersion", "false");

var builder = DistributedApplication.CreateBuilder(args);

// Foundry endpoint and key. Set in this project's user secrets:
// ConnectionStrings:foundry = "Endpoint=https://...;Key=..."
// Only the API reads it. The app never receives the key.
var foundry = builder.AddConnectionString("foundry");

var shop = builder.AddProject<Projects.BaristaNotes_RoasterShop>("shop");

var api = builder.AddProject<Projects.BaristaNotes_Api>("api")
    .WithReference(foundry)
    .WithReference(shop)
    .WaitFor(shop);

var app = builder.AddMauiProject("app", "../BaristaNotes/BaristaNotes.csproj");

app.AddMacCatalystDevice("app-maccatalyst")
    .WithReference(api)
    // Use a separate demo database so David's own data is not touched.
    .WithEnvironment("BARISTANOTES_DEMO", "1");

builder.Build().Run();
