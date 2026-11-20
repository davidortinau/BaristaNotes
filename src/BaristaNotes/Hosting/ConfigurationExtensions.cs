using BaristaNotes.Services.Agent;

namespace BaristaNotes.Hosting;

internal static class ConfigurationExtensions
{
    /// <summary>
    /// Loads <c>appsettings.json</c> via Shiny's platform bundle support
    /// (Android Assets / iOS-Mac Bundle Resources / Windows embedded) and, in
    /// DEBUG, layers <c>appsettings.Development.json</c> on top for local API
    /// keys. When the Aspire AppHost starts the app, AI calls go through the
    /// API instead (see <see cref="UseAgentApiForAI"/>).
    /// </summary>
    public static MauiAppBuilder AddAppConfiguration(this MauiAppBuilder builder)
    {
#if DEBUG
        builder.Configuration.AddJsonPlatformBundle("Development");
#else
        builder.Configuration.AddJsonPlatformBundle();
#endif

        builder.UseAgentApiForAI();
        return builder;
    }

    /// <summary>
    /// Under the Aspire AppHost, send the existing Azure OpenAI calls to the
    /// API's forwarder. The API authenticates with Foundry, so the app holds no credentials.
    /// The AI services read these values and do not change.
    /// </summary>
    static void UseAgentApiForAI(this MauiAppBuilder builder)
    {
        var api = AgentApiAddress.Resolve();
        if (api is null)
        {
            return;
        }

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureOpenAI:Endpoint"] = api.ToString(),
            // The client library requires a value. The API replaces it with server-side authentication.
            ["AzureOpenAI:ApiKey"] = "added-by-api",
        });
    }
}
