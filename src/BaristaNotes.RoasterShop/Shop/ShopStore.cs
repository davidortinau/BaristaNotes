using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.RoasterShop.Shop;

/// <summary>Called once for every order the store creates, duplicates included.</summary>
public interface IOrderNotifier
{
    void OrderCreated(Order order);
}

/// <summary>
/// All shop state, in memory. One lock guards carts, orders, idempotency keys,
/// and faults, so a retry that arrives while the first request is still being
/// held sees the first order. Records are immutable; reads return snapshots.
/// </summary>
public sealed class ShopStore
{
    public const int FirstOrderNumber = 1001;
    public const int MaxQuantity = 20;
    public const string ProposalIdAttribute = "proposal_id";

    private readonly Lock _gate = new();
    private readonly Dictionary<string, Cart> _carts = new(StringComparer.Ordinal);
    private readonly List<Order> _orders = [];
    private readonly Dictionary<string, Order> _ordersByKey = new(StringComparer.Ordinal);
    private int _nextOrderNumber = FirstOrderNumber;
    private Faults _faults = Faults.Off;

    private readonly Catalog _catalog;
    private readonly IOrderNotifier _notifier;
    private readonly TimeProvider _time;
    private readonly ILogger<ShopStore> _logger;

    public ShopStore(Catalog catalog, IOrderNotifier notifier, TimeProvider time, ILogger<ShopStore>? logger = null)
    {
        _catalog = catalog;
        _notifier = notifier;
        _time = time;
        _logger = logger ?? NullLogger<ShopStore>.Instance;
    }

    // ---------------------------------------------------------------- carts

    public CartResult CreateCart(string? roaster, IReadOnlyList<LineRequest>? lines, IReadOnlyDictionary<string, string>? attributes)
    {
        if (!TryPrice(roaster, lines, out var r, out var items, out var error))
            return new CartResult(CartOutcome.Invalid, null, error);

        var subtotal = items.Sum(l => l.Price * l.Quantity);
        var cart = new Cart(
            Id: "c_" + RandomNumberGenerator.GetHexString(16, lowercase: true),
            Roaster: r.Handle,
            Status: CartStatus.Open,
            Lines: items,
            Subtotal: subtotal,
            Shipping: r.Shipping,
            Total: subtotal + r.Shipping,
            Currency: Money.Currency,
            Attributes: Copy(attributes),
            CreatedAt: _time.GetLocalNow(),
            OrderId: null);

        lock (_gate)
        {
            _carts[cart.Id] = cart;
        }

        _logger.LogInformation("Cart {CartId} created at {Roaster} for {Total}", cart.Id, cart.Roaster, Money.Display(cart.Total));
        return new CartResult(CartOutcome.Created, cart, null);
    }

    public Cart? GetCart(string id)
    {
        lock (_gate)
        {
            return _carts.GetValueOrDefault(id);
        }
    }

    public DeleteCartOutcome DeleteCart(string id)
    {
        lock (_gate)
        {
            if (!_carts.TryGetValue(id, out var cart)) return DeleteCartOutcome.NotFound;
            switch (cart.Status)
            {
                case CartStatus.Ordered:
                    return DeleteCartOutcome.AlreadyOrdered;
                case CartStatus.Removed:
                    return DeleteCartOutcome.AlreadyRemoved;
                default:
                    _carts[id] = cart with { Status = CartStatus.Removed };
                    _logger.LogInformation("Cart {CartId} removed", id);
                    return DeleteCartOutcome.Removed;
            }
        }
    }

    // --------------------------------------------------------------- orders

    /// <summary>
    /// Places an order from a cart (<see cref="OrderRequest.CartId"/>) or from
    /// lines. A known idempotency key returns the original order unless the
    /// "ignore idempotency keys" fault is on.
    /// </summary>
    public OrderResult PlaceOrder(OrderRequest request, string? idempotencyKey, OrderSource source)
    {
        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        Order order;
        bool hold = false;

        lock (_gate)
        {
            var honorKeys = !_faults.IgnoreIdempotencyKeys;

            if (key is not null && honorKeys && _ordersByKey.TryGetValue(key, out var original))
            {
                _logger.LogInformation("Idempotency key {Key} already used. Replaying order {OrderNumber}", key, original.OrderNumber);
                return new OrderResult(OrderOutcome.Replayed, original, null);
            }

            string roaster;
            string? cartId = null;
            IReadOnlyList<LineItem> lines;
            decimal shipping;
            var attributes = new Dictionary<string, string>(StringComparer.Ordinal);

            if (!string.IsNullOrWhiteSpace(request.CartId))
            {
                if (!_carts.TryGetValue(request.CartId, out var cart))
                    return new OrderResult(OrderOutcome.NotFound, null, $"Cart {request.CartId} not found.");
                if (cart.Status == CartStatus.Removed)
                    return new OrderResult(OrderOutcome.Conflict, null, $"Cart {cart.Id} was removed.");
                // A correct store refuses to order the same cart twice. The
                // "ignore idempotency keys" fault simulates a store that does not.
                if (cart.Status == CartStatus.Ordered && honorKeys)
                    return new OrderResult(OrderOutcome.Conflict, null, $"Cart {cart.Id} was already ordered.");

                roaster = cart.Roaster;
                cartId = cart.Id;
                lines = cart.Lines;
                shipping = cart.Shipping;
                foreach (var (k, v) in cart.Attributes) attributes[k] = v;
            }
            else
            {
                if (!TryPrice(request.Roaster, request.Lines, out var r, out var items, out var error))
                    return new OrderResult(OrderOutcome.Invalid, null, error);
                roaster = r.Handle;
                lines = items;
                shipping = r.Shipping;
            }

            if (request.Attributes is not null)
                foreach (var (k, v) in request.Attributes) attributes[k] = v;

            var subtotal = lines.Sum(l => l.Price * l.Quantity);
            order = new Order(
                Id: "ord_" + RandomNumberGenerator.GetHexString(12, lowercase: true),
                OrderNumber: "#" + _nextOrderNumber++,
                Roaster: roaster,
                CartId: cartId,
                Lines: lines,
                Subtotal: subtotal,
                Shipping: shipping,
                Total: subtotal + shipping,
                Currency: Money.Currency,
                Attributes: attributes,
                IdempotencyKey: key,
                Source: source,
                CreatedAt: _time.GetLocalNow());

            _orders.Add(order);
            if (key is not null && honorKeys) _ordersByKey[key] = order;

            if (cartId is not null && _carts[cartId].Status == CartStatus.Open)
                _carts[cartId] = _carts[cartId] with { Status = CartStatus.Ordered, OrderId = order.Id };

            // One shot: the next API order consumes the fault, so a retry that
            // arrives during the delay is answered at once.
            if (source == OrderSource.Api && _faults.OrderTimeoutArmed)
            {
                _faults = _faults with { OrderTimeoutArmed = false };
                hold = true;
            }
        }

        _logger.LogInformation(
            "Order {OrderNumber} created at {Roaster} for {Total} (source {Source}, key {Key}, proposal {ProposalId})",
            order.OrderNumber, order.Roaster, Money.Display(order.Total), source, key ?? "none", order.ProposalId ?? "none");

        _notifier.OrderCreated(order);
        return new OrderResult(OrderOutcome.Created, order, null, hold);
    }

    /// <summary>Newest first.</summary>
    public IReadOnlyList<Order> GetOrders()
    {
        lock (_gate)
        {
            return Enumerable.Reverse(_orders).ToList();
        }
    }

    public Order? GetOrder(string id)
    {
        lock (_gate)
        {
            return _orders.Find(o => o.Id == id);
        }
    }

    /// <summary>The first order placed from this cart, if any.</summary>
    public Order? FindOrderForCart(string cartId)
    {
        lock (_gate)
        {
            return _orders.Find(o => o.CartId == cartId);
        }
    }

    public int OpenCartCount
    {
        get
        {
            lock (_gate)
            {
                return _carts.Values.Count(c => c.Status == CartStatus.Open);
            }
        }
    }

    // --------------------------------------------------------------- faults

    public Faults GetFaults()
    {
        lock (_gate)
        {
            return _faults;
        }
    }

    public Faults UpdateFaults(bool? orderTimeoutArmed = null, bool? ignoreIdempotencyKeys = null, int? priceIncreasePercent = null)
    {
        lock (_gate)
        {
            _faults = new Faults(
                orderTimeoutArmed ?? _faults.OrderTimeoutArmed,
                ignoreIdempotencyKeys ?? _faults.IgnoreIdempotencyKeys,
                Math.Clamp(priceIncreasePercent ?? _faults.PriceIncreasePercent, 0, 100));
            _logger.LogInformation(
                "Faults: order timeout armed {Armed}, ignore idempotency keys {Ignore}, price increase {Percent}%",
                _faults.OrderTimeoutArmed, _faults.IgnoreIdempotencyKeys, _faults.PriceIncreasePercent);
            return _faults;
        }
    }

    /// <summary>The current price, with the price-increase fault applied.</summary>
    public decimal PriceOf(CatalogProduct product)
    {
        lock (_gate)
        {
            return _faults.PriceIncreasePercent == 0
                ? product.Price
                : Math.Round(product.Price * (100 + _faults.PriceIncreasePercent) / 100m, 2, MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>Clears carts and orders, restarts at #1001, turns both faults off.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _carts.Clear();
            _orders.Clear();
            _ordersByKey.Clear();
            _nextOrderNumber = FirstOrderNumber;
            _faults = Faults.Off;
        }
        _logger.LogInformation("Demo reset: carts and orders cleared, faults off");
    }

    // -------------------------------------------------------------- helpers

    private bool TryPrice(
        string? roasterHandle,
        IReadOnlyList<LineRequest>? lines,
        out Roaster roaster,
        out IReadOnlyList<LineItem> items,
        out string error)
    {
        roaster = null!;
        items = [];
        error = "";

        var r = _catalog.FindRoaster(roasterHandle);
        if (r is null)
        {
            error = string.IsNullOrWhiteSpace(roasterHandle) ? "roaster is required." : $"Unknown roaster '{roasterHandle}'.";
            return false;
        }
        if (lines is null || lines.Count == 0)
        {
            error = "At least one line is required.";
            return false;
        }

        var result = new List<LineItem>(lines.Count);
        foreach (var line in lines)
        {
            var p = _catalog.FindVariant(line.VariantId);
            if (p is null || p.Roaster.Number != r.Number)
            {
                error = $"Variant {line.VariantId} is not sold by {r.Name}.";
                return false;
            }
            if (!p.Available)
            {
                error = $"{p.Title} (variant {p.VariantId}) is sold out.";
                return false;
            }
            if (line.Quantity is < 1 or > MaxQuantity)
            {
                error = $"Quantity for variant {line.VariantId} must be between 1 and {MaxQuantity}.";
                return false;
            }
            result.Add(new LineItem(p.VariantId, p.Id, p.Title, line.Quantity, PriceOf(p)));
        }

        roaster = r;
        items = result;
        return true;
    }

    private static Dictionary<string, string> Copy(IReadOnlyDictionary<string, string>? source) =>
        source is null ? new(StringComparer.Ordinal) : new(source, StringComparer.Ordinal);
}
