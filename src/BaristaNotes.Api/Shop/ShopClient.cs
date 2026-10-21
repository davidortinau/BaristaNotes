using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BaristaNotes.Api.Agent;

namespace BaristaNotes.Api.Shop;

/// <summary>
/// Talks to the roaster shop. The shop is Shopify-shaped: snake_case JSON and
/// prices as strings. Its base address comes from Aspire service discovery.
/// </summary>
public sealed class ShopClient(HttpClient http)
{
    public const string IdempotencyHeader = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotent-Replayed";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<IReadOnlyList<ShopRoaster>> GetRoastersAsync(CancellationToken ct)
    {
        var list = await http.GetFromJsonAsync<ShopRoasterList>("/roasters.json", Json, ct);
        return list?.Roasters ?? [];
    }

    public async Task<IReadOnlyList<ShopProduct>> GetProductsAsync(string roaster, CancellationToken ct)
    {
        var list = await http.GetFromJsonAsync<ShopProductList>($"/{Uri.EscapeDataString(roaster)}/products.json", Json, ct);
        return list?.Products ?? [];
    }

    public async Task<ShopCart> CreateCartAsync(string roaster, long variantId, Guid proposalId, CancellationToken ct)
    {
        var request = new ShopCartRequest(roaster, [new ShopLine(variantId, 1)], new() { ["proposal_id"] = proposalId.ToString() });
        using var response = await http.PostAsJsonAsync("/api/carts", request, Json, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<ShopCart>(Json, ct))!;
    }

    /// <summary>Returns false when the cart was already ordered.</summary>
    public async Task<bool> DeleteCartAsync(string cartId, CancellationToken ct)
    {
        using var response = await http.DeleteAsync($"/api/carts/{Uri.EscapeDataString(cartId)}", ct);
        if (response.StatusCode is HttpStatusCode.Conflict)
        {
            return false;
        }

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return true;
        }

        await EnsureSuccessAsync(response, ct);
        return true;
    }

    /// <summary>
    /// Places an order. The idempotency key is the proposal ID, so a retried
    /// request returns the first order instead of creating a second one.
    /// </summary>
    public async Task<ShopOrderResult> CreateOrderAsync(string roaster, long variantId, Guid proposalId, CancellationToken ct)
    {
        var body = new ShopOrderRequest(roaster, [new ShopLine(variantId, 1)], new() { ["proposal_id"] = proposalId.ToString() });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(body, options: Json),
        };
        request.Headers.Add(IdempotencyHeader, proposalId.ToString());

        using var response = await http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        var order = (await response.Content.ReadFromJsonAsync<ShopOrder>(Json, ct))!;
        var replayed = response.Headers.TryGetValues(ReplayedHeader, out var values)
                       && values.Any(v => v.Equals("true", StringComparison.OrdinalIgnoreCase));
        return new ShopOrderResult(order, replayed);
    }

    /// <summary>Demo only: clears carts and orders and turns the fault toggles off.</summary>
    public async Task ResetAsync(CancellationToken ct)
    {
        using var response = await http.PostAsync("/api/demo/reset", content: null, ct);
        await EnsureSuccessAsync(response, ct);
    }

    static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var text = await response.Content.ReadAsStringAsync(ct);
        throw new ShopException($"The shop answered {(int)response.StatusCode}: {Trim(text)}", response.StatusCode);

        static string Trim(string s) => s.Length > 200 ? s[..200] : s;
    }

    /// <summary>Turns a shop product into the variant the agent considers (the first one).</summary>
    public static CatalogItem? ToCatalogItem(ShopRoaster roaster, ShopProduct product)
    {
        var variant = product.Variants.FirstOrDefault();
        if (variant is null)
        {
            return null;
        }

        DateOnly? roasted = null;
        var roastedTag = product.Tags.FirstOrDefault(t => t.StartsWith("roasted:", StringComparison.OrdinalIgnoreCase));
        if (roastedTag is not null &&
            DateOnly.TryParseExact(roastedTag["roasted:".Length..], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            roasted = d;
        }

        return new CatalogItem(
            Roaster: roaster.Handle,
            RoasterName: roaster.Name,
            Shipping: roaster.Shipping,
            Currency: roaster.Currency,
            ProductId: product.Id,
            VariantId: variant.Id,
            Title: product.Title,
            Price: variant.Price,
            Available: variant.Available,
            RoastedOn: roasted,
            Tags: product.Tags);
    }
}

public sealed class ShopException(string message, HttpStatusCode status) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

public sealed record ShopRoasterList(IReadOnlyList<ShopRoaster> Roasters);
public sealed record ShopRoaster(string Handle, string Name, string City, decimal Shipping, string Currency);

public sealed record ShopProductList(IReadOnlyList<ShopProduct> Products);
public sealed record ShopProduct(long Id, string Title, string Handle, string Vendor, IReadOnlyList<string> Tags, IReadOnlyList<ShopVariant> Variants);
public sealed record ShopVariant(long Id, string Title, decimal Price, bool Available, string? Sku);

public sealed record ShopLine(long VariantId, int Quantity);
public sealed record ShopCartRequest(string Roaster, IReadOnlyList<ShopLine> Lines, Dictionary<string, string> Attributes);
public sealed record ShopOrderRequest(string Roaster, IReadOnlyList<ShopLine> Lines, Dictionary<string, string> Attributes);

public sealed record ShopCart(string Id, string Roaster, string Status, decimal Total, string Currency, string CheckoutUrl);

public sealed record ShopOrder(
    string Id,
    string OrderNumber,
    string Roaster,
    string? CartId,
    decimal Total,
    string Currency,
    Dictionary<string, string>? Attributes,
    string? IdempotencyKey,
    string? Source,
    DateTimeOffset CreatedAt)
{
    public Guid? ProposalId =>
        Attributes is not null && Attributes.TryGetValue("proposal_id", out var value) && Guid.TryParse(value, out var id)
            ? id
            : null;
}

public sealed record ShopOrderResult(ShopOrder Order, bool Replayed);
