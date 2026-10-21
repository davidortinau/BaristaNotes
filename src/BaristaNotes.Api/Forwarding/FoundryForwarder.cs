using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace BaristaNotes.Api.Forwarding;

/// <summary>
/// Forwards the app's existing Azure OpenAI calls to Foundry and adds the key,
/// so the app does not hold one. This is a local demo forwarder with no user
/// sign-in, so it accepts loopback callers only. Production needs sign-in and
/// per-feature endpoints instead.
/// </summary>
public static class FoundryForwarder
{
    public const string RoutePrefix = "/openai";
    public const string KeyHeader = "api-key";

    public static WebApplicationBuilder AddFoundryForwarder(this WebApplicationBuilder builder)
    {
        var connection = FoundryConnection.FromConfiguration(builder.Configuration);
        builder.Services.AddSingleton(connection);

        builder.Services.AddReverseProxy()
            .LoadFromMemory(
                [
                    new RouteConfig
                    {
                        RouteId = "foundry",
                        ClusterId = "foundry",
                        Match = new RouteMatch { Path = RoutePrefix + "/{**catch-all}" },
                    },
                ],
                [
                    new ClusterConfig
                    {
                        ClusterId = "foundry",
                        Destinations = new Dictionary<string, DestinationConfig>
                        {
                            ["foundry"] = new() { Address = connection.Endpoint.ToString() },
                        },
                    },
                ])
            .AddTransforms(context => context.AddRequestTransform(transform =>
            {
                ApplyKey(transform.ProxyRequest.Headers, connection.Key);
                return ValueTask.CompletedTask;
            }));

        return builder;
    }

    public static WebApplication MapFoundryForwarder(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(RoutePrefix) &&
                !IsLoopback(context.Connection.RemoteIpAddress))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next();
        });

        app.MapReverseProxy();
        return app;
    }

    /// <summary>Drops any credential the caller sent and adds the Foundry key.</summary>
    public static void ApplyKey(HttpRequestHeaders headers, string key)
    {
        headers.Remove(KeyHeader);
        headers.Remove("Authorization");
        headers.TryAddWithoutValidation(KeyHeader, key);
    }

    public static bool IsLoopback(IPAddress? address) =>
        address is not null && IPAddress.IsLoopback(address);
}

/// <summary>Foundry endpoint and key from the "foundry" connection string.</summary>
public sealed class FoundryConnection(Uri endpoint, string key)
{
    public Uri Endpoint { get; } = endpoint;

    public string Key { get; } = key;

    public static FoundryConnection FromConfiguration(IConfiguration configuration) =>
        Parse(configuration.GetConnectionString("foundry")
            ?? throw new InvalidOperationException(
                "Missing connection string 'foundry'. Set ConnectionStrings:foundry in the AppHost user secrets."));

    public static FoundryConnection Parse(string connectionString)
    {
        var parts = new DbConnectionStringBuilder { ConnectionString = connectionString };
        if (!parts.TryGetValue("Endpoint", out var endpoint) || !parts.TryGetValue("Key", out var key) ||
            string.IsNullOrWhiteSpace(endpoint as string) || string.IsNullOrWhiteSpace(key as string))
        {
            throw new InvalidOperationException("Connection string 'foundry' must contain Endpoint and Key.");
        }

        return new FoundryConnection(new Uri((string)endpoint), (string)key);
    }

    // Never write the key to logs.
    public override string ToString() => $"FoundryConnection {{ Endpoint = {Endpoint} }}";
}
