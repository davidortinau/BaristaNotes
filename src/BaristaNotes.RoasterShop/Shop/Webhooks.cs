using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace BaristaNotes.RoasterShop.Shop;

public sealed class WebhookOptions
{
    public const string Section = "Webhooks";

    /// <summary>Resolved through Aspire service discovery.</summary>
    public string Url { get; set; } = "https+http://api/webhooks/orders";

    /// <summary>HMAC key shared with the API. When missing, webhooks are not sent.</summary>
    public string? Secret { get; set; }
}

public sealed class FaultOptions
{
    public const string Section = "Faults";

    /// <summary>How long the armed "order timeout" fault holds the response.</summary>
    public TimeSpan OrderTimeoutDelay { get; set; } = TimeSpan.FromSeconds(6);
}

/// <summary>Hands created orders to <see cref="WebhookSender"/> without blocking the order request.</summary>
public sealed class WebhookQueue : IOrderNotifier
{
    private readonly Channel<Order> _channel =
        Channel.CreateUnbounded<Order>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<Order> Reader => _channel.Reader;

    public void OrderCreated(Order order) => _channel.Writer.TryWrite(order);
}

/// <summary>Builds and signs an orders/create webhook, like Shopify.</summary>
public static class WebhookSigner
{
    public const string Topic = "orders/create";
    public const string TopicHeader = "X-Shop-Topic";
    public const string WebhookIdHeader = "X-Shop-Webhook-Id";
    public const string HmacHeader = "X-Shop-Hmac-Sha256";

    /// <summary>Base64 HMAC-SHA256 of the exact body bytes.</summary>
    public static string Sign(ReadOnlySpan<byte> body, string secret) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));

    public static byte[] Serialize(Order order) => JsonSerializer.SerializeToUtf8Bytes(order, ShopJson.Options);

    public static HttpRequestMessage CreateRequest(Order order, Uri url, string secret, Guid webhookId)
    {
        var body = Serialize(order);
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Add(TopicHeader, Topic);
        request.Headers.Add(WebhookIdHeader, webhookId.ToString());
        request.Headers.Add(HmacHeader, Sign(body, secret));
        return request;
    }
}

/// <summary>
/// Sends one webhook per created order, in the background. The standard
/// resilience handler (from service defaults) retries; the API dedupes by
/// order id. Failures are logged and never stop the shop.
/// </summary>
public sealed class WebhookSender(
    WebhookQueue queue,
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<WebhookSender> logger) : BackgroundService
{
    public const string ClientName = "webhooks";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var order in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await SendAsync(order, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task SendAsync(Order order, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        if (string.IsNullOrWhiteSpace(settings.Secret))
        {
            logger.LogWarning(
                "Webhooks:Secret is not set. Not sending the orders/create webhook for {OrderNumber} ({OrderId})",
                order.OrderNumber, order.Id);
            return;
        }

        if (!Uri.TryCreate(settings.Url, UriKind.Absolute, out var url))
        {
            logger.LogWarning("Webhooks:Url '{Url}' is not an absolute URL. Webhook for {OrderNumber} not sent", settings.Url, order.OrderNumber);
            return;
        }

        var webhookId = Guid.NewGuid();
        try
        {
            using var request = WebhookSigner.CreateRequest(order, url, settings.Secret, webhookId);
            using var response = await httpClientFactory.CreateClient(ClientName).SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Webhook orders/create {WebhookId} for {OrderNumber} delivered: {Status}",
                    webhookId, order.OrderNumber, (int)response.StatusCode);
            }
            else
            {
                logger.LogWarning(
                    "Webhook orders/create {WebhookId} for {OrderNumber} rejected: {Status}",
                    webhookId, order.OrderNumber, (int)response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Webhook orders/create {WebhookId} for {OrderNumber} failed", webhookId, order.OrderNumber);
        }
    }
}
