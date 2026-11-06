using BaristaNotes.Services.Agent;

namespace BaristaNotes.Hosting;

internal static class AgentServiceExtensions
{
    /// <summary>
    /// Agent API client, snapshot builder, and demo seeder. The client gets
    /// its own HttpClient so Aspire service defaults (and their retry
    /// handlers) never reach the app's other clients. Without the AppHost the
    /// client has no base address and reports <c>IsConnected == false</c>.
    /// </summary>
    public static MauiAppBuilder AddAgentServices(this MauiAppBuilder builder)
    {
        var api = AgentApiAddress.Resolve();

        builder.Services.AddHttpClient<AgentApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);

            // The API refuses state changes without this header, so a web page
            // open on the same machine cannot trigger them.
            client.DefaultRequestHeaders.Add("X-Requested-By", "BaristaNotes");
            if (api is not null)
            {
                // Relative request paths keep any base path only with a trailing slash.
                client.BaseAddress = api.AbsoluteUri.EndsWith('/') ? api : new Uri(api.AbsoluteUri + "/");
            }
        });

        builder.Services.AddSingleton<AgentSnapshotBuilder>();
        builder.Services.AddSingleton<DemoDataSeeder>();

        return builder;
    }
}
