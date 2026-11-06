using System.Security.Cryptography;
using System.Text.Json;
using BaristaNotes.Api.Agent;

namespace BaristaNotes.Api.Shop;

/// <summary>
/// Receives the shop's orders/create webhook. The shop signs the raw body with
/// HMAC-SHA256 and a secret the AppHost generates, like Shopify does. Unsigned
/// or wrongly signed requests are rejected. The same order is recorded once.
/// </summary>
public static class ShopWebhook
{
    public const string SignatureHeader = "X-Shop-Hmac-Sha256";
    public const int MaxBodyBytes = 64 * 1024;

    public static WebApplication MapShopWebhook(this WebApplication app)
    {
        app.MapPost("/webhooks/orders", async (HttpRequest request, OrderRecorder orders, IConfiguration config, ILoggerFactory logs) =>
        {
            var log = logs.CreateLogger(typeof(ShopWebhook));
            var secret = config["Webhooks:Secret"];
            if (string.IsNullOrEmpty(secret))
            {
                log.LogWarning("Webhooks:Secret is not set, so shop webhooks are rejected");
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer);
            if (buffer.Length > MaxBodyBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var body = buffer.ToArray();
            if (!IsValid(body, request.Headers[SignatureHeader].ToString(), secret))
            {
                log.LogWarning("Rejected a shop webhook with a missing or wrong signature");
                return Results.Unauthorized();
            }

            var order = JsonSerializer.Deserialize<ShopOrder>(body, ShopClient.Json);
            if (order is null || string.IsNullOrEmpty(order.Id))
            {
                return Results.BadRequest();
            }

            await orders.RecordAsync(order, reportedBy: "webhook");
            return Results.Ok();
        });

        return app;
    }

    public static string Sign(byte[] body, string secret) =>
        Convert.ToBase64String(HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret), body));

    public static bool IsValid(byte[] body, string? signature, string secret)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        byte[] given;
        try
        {
            given = Convert.FromBase64String(signature);
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret), body);
        return CryptographicOperations.FixedTimeEquals(given, expected);
    }
}
