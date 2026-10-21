using BaristaNotes.RoasterShop.Shop;

namespace BaristaNotes.RoasterShop.Tests;

/// <summary>A store with a fixed clock and a notifier that records every created order.</summary>
internal sealed class TestShop
{
    public static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 30, 0, TimeSpan.FromHours(2));

    public TestShop()
    {
        Time = new FixedTimeProvider(Now);
        Catalog = new Catalog(Time);
        Store = new ShopStore(Catalog, Notifier, Time);
    }

    public FixedTimeProvider Time { get; }
    public Catalog Catalog { get; }
    public RecordingNotifier Notifier { get; } = new();
    public ShopStore Store { get; }

    /// <summary>Copperhaze Guji Natural, available, 17.50.</summary>
    public const long GujiVariant = 5011;

    /// <summary>Copperhaze Sidamo Natural, sold out.</summary>
    public const long SidamoVariant = 5021;

    public Cart CreateGujiCart(string? proposalId = null)
    {
        var attributes = proposalId is null ? null : new Dictionary<string, string> { ["proposal_id"] = proposalId };
        var result = Store.CreateCart("copperhaze", [new LineRequest(GujiVariant, 1)], attributes);
        Assert.Equal(CartOutcome.Created, result.Outcome);
        return result.Cart!;
    }

    public static OrderRequest GujiLines(string? proposalId = null) => new(
        Roaster: "copperhaze",
        Lines: [new LineRequest(GujiVariant, 1)],
        Attributes: proposalId is null ? null : new() { ["proposal_id"] = proposalId });
}

internal sealed class RecordingNotifier : IOrderNotifier
{
    private readonly List<Order> _orders = [];

    public IReadOnlyList<Order> Orders
    {
        get { lock (_orders) return _orders.ToList(); }
    }

    public void OrderCreated(Order order)
    {
        lock (_orders) _orders.Add(order);
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone { get; } =
        TimeZoneInfo.CreateCustomTimeZone("Test+2", now.Offset, "Test+2", "Test+2");
}
