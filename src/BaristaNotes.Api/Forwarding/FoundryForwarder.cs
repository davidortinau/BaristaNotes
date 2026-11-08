using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using Azure.Core;
using Azure.Identity;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace BaristaNotes.Api.Forwarding;

/// <summary>
/// Forwards the app's existing Azure OpenAI calls to Foundry and authenticates
/// server-side. This is a local demo forwarder with no user
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
        builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());

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
                ApplyAuthenticationAsync(
                    transform.ProxyRequest.Headers,
                    connection,
                    transform.HttpContext.RequestServices.GetRequiredService<TokenCredential>(),
                    transform.HttpContext.RequestAborted)));

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

    public static async ValueTask ApplyAuthenticationAsync(
        HttpRequestHeaders headers, FoundryConnection connection, TokenCredential credential, CancellationToken ct)
    {
        if (connection.Key is { } key)
        {
            ApplyKey(headers, key);
            return;
        }

        headers.Remove(KeyHeader);
        headers.Remove("Authorization");
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]), ct);
        headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }

    public static bool IsLoopback(IPAddress? address) =>
        address is not null && IPAddress.IsLoopback(address);
}

/// <summary>Foundry connection; an omitted key selects Entra ID authentication.</summary>
public sealed class FoundryConnection(Uri endpoint, string? key)
{
    public Uri Endpoint { get; } = endpoint;

    public string? Key { get; } = key;

    public static FoundryConnection FromConfiguration(IConfiguration configuration) =>
        Parse(configuration.GetConnectionString("foundry")
            ?? throw new InvalidOperationException(
                "Missing connection string 'foundry'. Start through the AppHost or set ConnectionStrings:foundry."));

    public static FoundryConnection Parse(string connectionString)
    {
        var parts = new DbConnectionStringBuilder { ConnectionString = connectionString };
        if (!parts.TryGetValue("Endpoint", out var endpoint) || string.IsNullOrWhiteSpace(endpoint as string))
        {
            throw new InvalidOperationException("Connection string 'foundry' must contain Endpoint.");
        }

        var hasKey = parts.TryGetValue("Key", out var key);
        if (hasKey && string.IsNullOrWhiteSpace(key as string))
        {
            throw new InvalidOperationException("Connection string 'foundry' must omit Key for Entra ID or supply a non-empty Key.");
        }

        return new FoundryConnection(new Uri((string)endpoint), hasKey ? (string)key! : null);
    }

    // Never write the key to logs.
    public override string ToString() => $"FoundryConnection {{ Endpoint = {Endpoint} }}";
}
