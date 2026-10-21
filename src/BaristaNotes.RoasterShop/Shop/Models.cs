using System.Text.Json.Serialization;

namespace BaristaNotes.RoasterShop.Shop;

public enum CartStatus { Open, Ordered, Removed }

public enum OrderSource { Api, Checkout }

public sealed record LineItem(long VariantId, long ProductId, string Title, int Quantity, decimal Price);

public sealed record Cart(
    string Id,
    string Roaster,
    CartStatus Status,
    IReadOnlyList<LineItem> Lines,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    string Currency,
    IReadOnlyDictionary<string, string> Attributes,
    DateTimeOffset CreatedAt,
    string? OrderId);

public sealed record Order(
    string Id,
    string OrderNumber,
    string Roaster,
    string? CartId,
    IReadOnlyList<LineItem> Lines,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    string Currency,
    IReadOnlyDictionary<string, string> Attributes,
    string? IdempotencyKey,
    OrderSource Source,
    DateTimeOffset CreatedAt)
{
    [JsonIgnore]
    public string? ProposalId =>Attributes.TryGetValue(ShopStore.ProposalIdAttribute, out var id) ? id : null;
}

/// <summary>Fault toggles for the talk. Both off by default.</summary>
public sealed record Faults(bool OrderTimeoutArmed, bool IgnoreIdempotencyKeys)
{
    public static readonly Faults Off = new(false, false);
}

// Request bodies (snake_case on the wire).
public sealed record LineRequest(long VariantId, int Quantity);

public sealed record CartRequest(
    string? Roaster,
    List<LineRequest>? Lines,
    Dictionary<string, string>? Attributes);

public sealed record OrderRequest(
    string? CartId = null,
    string? Roaster = null,
    List<LineRequest>? Lines = null,
    Dictionary<string, string>? Attributes = null);

public sealed record FaultsUpdate(bool? OrderTimeoutArmed, bool? IgnoreIdempotencyKeys);

/// <summary>Cart as the API returns it: the stored cart plus an absolute checkout URL.</summary>
public sealed record CartResponse(
    string Id,
    string Roaster,
    CartStatus Status,
    IReadOnlyList<LineItem> Lines,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    string Currency,
    string CheckoutUrl,
    IReadOnlyDictionary<string, string> Attributes,
    string? OrderId)
{
    public static CartResponse From(Cart c, string checkoutUrl) => new(
        c.Id, c.Roaster, c.Status, c.Lines, c.Subtotal, c.Shipping, c.Total, c.Currency,
        checkoutUrl, c.Attributes, c.OrderId);
}

public sealed record ErrorResponse(string Error);

public static class Ids
{
    /// <summary>First characters of a long id, for tables. The full id goes in a title attribute.</summary>
    public static string Short(string? value, int length = 8) =>
        string.IsNullOrEmpty(value) ? "" : value.Length <= length ? value : value[..length];
}

// Store results.
public enum CartOutcome { Created, Invalid }

public sealed record CartResult(CartOutcome Outcome, Cart? Cart, string? Error);

public enum DeleteCartOutcome { Removed, AlreadyRemoved, AlreadyOrdered, NotFound }

public enum OrderOutcome { Created, Replayed, NotFound, Conflict, Invalid }

/// <param name="HoldResponse">
/// True when this request consumed the one-shot "order timeout" fault. The
/// order already exists; the caller should delay the response.
/// </param>
public sealed record OrderResult(OrderOutcome Outcome, Order? Order, string? Error, bool HoldResponse = false);
