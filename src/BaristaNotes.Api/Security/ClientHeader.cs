using BaristaNotes.Api.Forwarding;
using Microsoft.Extensions.Primitives;

namespace BaristaNotes.Api.Security;

/// <summary>
/// Blocks cross-site requests from web pages open on this Mac. Loopback is not
/// authentication: a page can send a simple POST to localhost without a CORS
/// preflight. A custom header cannot be sent cross-site without a preflight,
/// and this API answers no preflights, so every state-changing request must
/// carry one.
/// </summary>
public static class ClientHeader
{
    public const string Name = "X-Requested-By";

    /// <summary>True when the request may continue.</summary>
    public static bool IsAllowed(string method, PathString path, IHeaderDictionary headers)
    {
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        {
            return true;
        }

        // The shop's webhook is checked by its HMAC signature instead.
        if (path.StartsWithSegments("/webhooks"))
        {
            return true;
        }

        // The app's AI client always sends api-key, which is also a custom header.
        if (path.StartsWithSegments(FoundryForwarder.RoutePrefix))
        {
            return HasValue(headers, FoundryForwarder.KeyHeader) || HasValue(headers, "Authorization");
        }

        return HasValue(headers, Name);
    }

    public static WebApplication UseClientHeaderCheck(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (!IsAllowed(context.Request.Method, context.Request.Path, context.Request.Headers))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = $"Missing the {Name} header." });
                return;
            }

            await next();
        });

        return app;
    }

    static bool HasValue(IHeaderDictionary headers, string name) =>
        headers.TryGetValue(name, out var value) && !StringValues.IsNullOrEmpty(value);
}
