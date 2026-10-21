using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BaristaNotes.RoasterShop.Shop;

namespace BaristaNotes.RoasterShop.Tests;

public class WebhookTests
{
    private const string Secret = "test-webhook-secret";

    private static Order PlaceOrder()
    {
        var shop = new TestShop();
        return shop.Store.PlaceOrder(TestShop.GujiLines("proposal-1"), "proposal-1", OrderSource.Api).Order!;
    }

    [Fact]
    public async Task Signature_is_the_hmac_of_the_exact_body_bytes()
    {
        var order = PlaceOrder();
        var webhookId = Guid.NewGuid();

        using var request = WebhookSigner.CreateRequest(order, new Uri("https://api.example/webhooks/orders"), Secret, webhookId);
        var body = await request.Content!.ReadAsByteArrayAsync();

        // Independent computation with the classic HMACSHA256 API.
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var expected = Convert.ToBase64String(hmac.ComputeHash(body));

        Assert.Equal(expected, Assert.Single(request.Headers.GetValues(WebhookSigner.HmacHeader)));
        Assert.Equal("orders/create", Assert.Single(request.Headers.GetValues("X-Shop-Topic")));
        Assert.Equal(webhookId.ToString(), Assert.Single(request.Headers.GetValues("X-Shop-Webhook-Id")));
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("application/json", request.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public void A_changed_body_or_secret_does_not_verify()
    {
        var body = WebhookSigner.Serialize(PlaceOrder());
        var signature = WebhookSigner.Sign(body, Secret);

        var tampered = (byte[])body.Clone();
        tampered[^2] ^= 0x01;

        Assert.NotEqual(signature, WebhookSigner.Sign(tampered, Secret));
        Assert.NotEqual(signature, WebhookSigner.Sign(body, "other-secret"));
    }

    [Fact]
    public void Body_is_the_snake_case_order()
    {
        var order = PlaceOrder();

        using var doc = JsonDocument.Parse(WebhookSigner.Serialize(order));
        var o = doc.RootElement;

        Assert.Equal(order.Id, o.GetProperty("id").GetString());
        Assert.StartsWith("ord_", o.GetProperty("id").GetString());
        Assert.Equal("#1001", o.GetProperty("order_number").GetString());
        Assert.Equal("copperhaze", o.GetProperty("roaster").GetString());
        Assert.Equal(JsonValueKind.Null, o.GetProperty("cart_id").ValueKind);
        Assert.Equal(5011, o.GetProperty("lines")[0].GetProperty("variant_id").GetInt64());
        Assert.Equal("17.50", o.GetProperty("subtotal").GetString());
        Assert.Equal("5.00", o.GetProperty("shipping").GetString());
        Assert.Equal("22.50", o.GetProperty("total").GetString());
        Assert.Equal("EUR", o.GetProperty("currency").GetString());
        Assert.Equal("proposal-1", o.GetProperty("attributes").GetProperty("proposal_id").GetString());
        Assert.Equal("proposal-1", o.GetProperty("idempotency_key").GetString());
        Assert.Equal("api", o.GetProperty("source").GetString());
        Assert.Equal(TestShop.Now, o.GetProperty("created_at").GetDateTimeOffset());
        Assert.False(o.TryGetProperty("proposal_id", out _));
    }
}
