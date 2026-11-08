using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Core;
using BaristaNotes.Agent.Contracts;
using BaristaNotes.Api.Agent;
using BaristaNotes.Api.Data;
using BaristaNotes.Api.Endpoints;
using BaristaNotes.Api.Forwarding;
using BaristaNotes.Api.Security;
using BaristaNotes.Api.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Polly;
using Polly.Retry;
using Polly.Timeout;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, AgentJsonContext.Default));
builder.Services.AddSingleton(TimeProvider.System);

// The app's existing AI calls go through here; credentials stay in the API.
builder.AddFoundryForwarder();

// Agent state. Demo only: the schema is created on start, and Reset demo empties it.
var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDirectory);
var storage = AgentStorage.Provider(builder.Configuration);
builder.Services.AddDbContextFactory<AgentDb>(o => AgentStorage.Configure(o, builder.Configuration, dataDirectory));
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AgentDb>>().CreateDbContext());

// The shop. Aspire service defaults give every HttpClient the standard
// resilience handler, which retries POST requests too. That is the point of
// the TF-15 demo, so the shop client keeps it. The only changes are a short
// attempt timeout for stage pacing, and a ledger entry for each retry.
var shopAttemptTimeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Shop:AttemptTimeoutSeconds", 3));
#pragma warning disable EXTEXP0001 // Experimental, and the documented way to replace the default handler for one client.
builder.Services.AddHttpClient<ShopClient>(c =>
    {
        c.BaseAddress = new Uri(builder.Configuration["Shop:Url"] ?? "https+http://shop");
        c.DefaultRequestHeaders.Add(ClientHeader.Name, "BaristaNotes.Api");
    })
    .RemoveAllResilienceHandlers()
#pragma warning restore EXTEXP0001
    .AddStandardResilienceHandler(o =>
    {
        o.AttemptTimeout.Timeout = shopAttemptTimeout;
        o.Retry.OnRetry = args => LogRetryAsync(args, shopAttemptTimeout);
    });

// Provisioned Foundry uses Entra ID; existing endpoint/key connections still work.
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var foundry = sp.GetRequiredService<FoundryConnection>();
    var deployment = builder.Configuration["Agent:Model"] ?? "gpt-4.1-mini";
    var client = foundry.Key is { } key
        ? new AzureOpenAIClient(foundry.Endpoint, new ApiKeyCredential(key))
        : new AzureOpenAIClient(foundry.Endpoint, sp.GetRequiredService<TokenCredential>());
    return client
        .GetChatClient(deployment)
        .AsIChatClient()
        .AsBuilder()
        .UseOpenTelemetry(sp.GetRequiredService<ILoggerFactory>(), FoundryReorderModel.TelemetrySource,
            c => c.EnableSensitiveData = builder.Environment.IsDevelopment())
        .Use(inner => new ReplayChatClient(inner, sp.GetRequiredService<ModelReplay>(), sp.GetRequiredService<ILogger<ReplayChatClient>>()))
        .Build(sp);
});
builder.Services.AddSingleton(new ModelReplay(
    Path.Combine(dataDirectory, "replay"),
    builder.Configuration.GetValue("Agent:Replay", ReplayMode.Auto)));
builder.Services.AddSingleton<IReorderModel, FoundryReorderModel>();

builder.Services.AddSingleton<LedgerWriter>();
builder.Services.AddSingleton<OrderRecorder>();
builder.Services.AddSingleton<RunCoordinator>();
builder.Services.AddSingleton<StagePace>();
builder.Services.AddScoped<AgentRunner>();

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(FoundryReorderModel.TelemetrySource))
    .WithMetrics(m => m.AddMeter(FoundryReorderModel.TelemetrySource));

var app = builder.Build();
app.Logger.LogInformation("Agent storage provider: {StorageProvider}", storage);

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AgentDb>();
    await db.Database.EnsureCreatedAsync();
    await AgentState.GetSettingsAsync(db);
}

await app.Services.GetRequiredService<RunCoordinator>().RecoverAsync();

app.UseClientHeaderCheck();
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Ok(new { name = "BaristaNotes API" }));
app.MapGet("/demo/storage", () => Results.Ok(new { provider = storage }));
app.MapAgentEndpoints();
app.MapShopWebhook();
app.MapFoundryForwarder();

app.Run();

static async ValueTask LogRetryAsync(OnRetryArguments<HttpResponseMessage> args, TimeSpan attemptTimeout)
{
    if (RunContext.Value is not { } run)
    {
        return;
    }

    var request = args.Context.GetRequestMessage();
    var why = args.Outcome.Exception switch
    {
        TimeoutRejectedException => $"No answer from the shop within {attemptTimeout.TotalSeconds:0} seconds.",
        { } ex => $"The request failed: {ex.Message}",
        null => $"The shop answered {(int?)args.Outcome.Result?.StatusCode}.",
    };
    var key = request is not null && request.Headers.TryGetValues(ShopClient.IdempotencyHeader, out var values)
        ? $" Same Idempotency-Key {values.First()[..8]}."
        : "";

    await run.Ledger.WriteAsync(run.RunId, LedgerKind.Retrying,
        $"Retrying {request?.Method} {request?.RequestUri?.AbsolutePath}.",
        $"{why} The HTTP client sends the same request again (attempt {args.AttemptNumber + 2}).{key}");
}
