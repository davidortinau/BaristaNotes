using BaristaNotes.RoasterShop.Shop;

namespace BaristaNotes.RoasterShop.Tests;

public class ShopStoreTests
{
    [Fact]
    public void Same_idempotency_key_twice_creates_one_order_and_replays_the_second()
    {
        var shop = new TestShop();
        var key = Guid.NewGuid().ToString();

        var first = shop.Store.PlaceOrder(TestShop.GujiLines(key), key, OrderSource.Api);
        var second = shop.Store.PlaceOrder(TestShop.GujiLines(key), key, OrderSource.Api);

        Assert.Equal(OrderOutcome.Created, first.Outcome);
        Assert.Equal(OrderOutcome.Replayed, second.Outcome);
        Assert.Equal(first.Order!.Id, second.Order!.Id);
        Assert.Single(shop.Store.GetOrders());
        Assert.Single(shop.Notifier.Orders);
    }

    [Fact]
    public void Ignore_keys_fault_creates_two_orders_for_the_same_key()
    {
        var shop = new TestShop();
        shop.Store.UpdateFaults(ignoreIdempotencyKeys: true);
        var proposal = Guid.NewGuid().ToString();

        var first = shop.Store.PlaceOrder(TestShop.GujiLines(proposal), proposal, OrderSource.Api);
        var second = shop.Store.PlaceOrder(TestShop.GujiLines(proposal), proposal, OrderSource.Api);

        Assert.Equal(OrderOutcome.Created, first.Outcome);
        Assert.Equal(OrderOutcome.Created, second.Outcome);
        Assert.NotEqual(first.Order!.Id, second.Order!.Id);
        Assert.Equal(2, shop.Store.GetOrders().Count);
        Assert.All(shop.Store.GetOrders(), o => Assert.Equal(proposal, o.IdempotencyKey));
        // A webhook goes out for the duplicate too.
        Assert.Equal(2, shop.Notifier.Orders.Count);
    }

    [Fact]
    public void Ignore_keys_fault_also_lets_the_same_cart_be_ordered_twice()
    {
        var shop = new TestShop();
        var cart = shop.CreateGujiCart(proposalId: "p-1");
        shop.Store.UpdateFaults(ignoreIdempotencyKeys: true);

        var first = shop.Store.PlaceOrder(new OrderRequest(CartId: cart.Id), "p-1", OrderSource.Api);
        var second = shop.Store.PlaceOrder(new OrderRequest(CartId: cart.Id), "p-1", OrderSource.Api);

        Assert.Equal(OrderOutcome.Created, second.Outcome);
        Assert.Equal(2, shop.Store.GetOrders().Count);
        Assert.Equal(first.Order!.Id, shop.Store.GetCart(cart.Id)!.OrderId);
    }

    [Fact]
    public void Honored_keys_refuse_to_order_the_same_cart_twice_with_a_new_key()
    {
        var shop = new TestShop();
        var cart = shop.CreateGujiCart();

        shop.Store.PlaceOrder(new OrderRequest(CartId: cart.Id), "k1", OrderSource.Api);
        var second = shop.Store.PlaceOrder(new OrderRequest(CartId: cart.Id), "k2", OrderSource.Api);

        Assert.Equal(OrderOutcome.Conflict, second.Outcome);
        Assert.Single(shop.Store.GetOrders());
    }

    [Fact]
    public void Ordering_a_cart_marks_it_ordered_and_copies_its_attributes()
    {
        var shop = new TestShop();
        var cart = shop.CreateGujiCart(proposalId: "p-42");

        var result = shop.Store.PlaceOrder(new OrderRequest(CartId: cart.Id), cart.Id, OrderSource.Checkout);

        Assert.Equal(OrderOutcome.Created, result.Outcome);
        var order = result.Order!;
        Assert.Equal(cart.Id, order.CartId);
        Assert.Equal("p-42", order.ProposalId);
        Assert.Equal(OrderSource.Checkout, order.Source);
        Assert.Equal(17.50m, order.Subtotal);
        Assert.Equal(5.00m, order.Shipping);
        Assert.Equal(22.50m, order.Total);
        var stored = shop.Store.GetCart(cart.Id)!;
        Assert.Equal(CartStatus.Ordered, stored.Status);
        Assert.Equal(order.Id, stored.OrderId);
        Assert.Same(order, shop.Store.FindOrderForCart(cart.Id));
    }

    [Fact]
    public void Ordering_a_removed_cart_fails()
    {
        var shop = new TestShop();
        var cart = shop.CreateGujiCart();

        Assert.Equal(DeleteCartOutcome.Removed, shop.Store.DeleteCart(cart.Id));
        var result = shop.Store.PlaceOrder(new OrderRequest(CartId: cart.Id), null, OrderSource.Api);

        Assert.Equal(OrderOutcome.Conflict, result.Outcome);
        Assert.Contains("removed", result.Error);
        Assert.Empty(shop.Store.GetOrders());
        Assert.Equal(CartStatus.Removed, shop.Store.GetCart(cart.Id)!.Status);
    }

    [Fact]
    public void Deleting_an_ordered_cart_fails()
    {
        var shop = new TestShop();
        var cart = shop.CreateGujiCart();
        shop.Store.PlaceOrder(new OrderRequest(CartId: cart.Id), null, OrderSource.Api);

        Assert.Equal(DeleteCartOutcome.AlreadyOrdered, shop.Store.DeleteCart(cart.Id));
        Assert.Equal(CartStatus.Ordered, shop.Store.GetCart(cart.Id)!.Status);
    }

    [Fact]
    public void Deleting_an_unknown_cart_reports_not_found_and_ordering_it_too()
    {
        var shop = new TestShop();

        Assert.Equal(DeleteCartOutcome.NotFound, shop.Store.DeleteCart("c_nope"));
        Assert.Equal(OrderOutcome.NotFound, shop.Store.PlaceOrder(new OrderRequest(CartId: "c_nope"), null, OrderSource.Api).Outcome);
    }

    [Fact]
    public void Order_numbers_increment_from_1001_and_reset_restarts_them()
    {
        var shop = new TestShop();

        var a = shop.Store.PlaceOrder(TestShop.GujiLines(), null, OrderSource.Api).Order!;
        var b = shop.Store.PlaceOrder(TestShop.GujiLines(), null, OrderSource.Api).Order!;
        var c = shop.Store.PlaceOrder(TestShop.GujiLines(), null, OrderSource.Api).Order!;

        Assert.Equal(["#1001", "#1002", "#1003"], new[] { a.OrderNumber, b.OrderNumber, c.OrderNumber });
        // Newest first.
        Assert.Equal(["#1003", "#1002", "#1001"], shop.Store.GetOrders().Select(o => o.OrderNumber));

        var cart = shop.CreateGujiCart();
        shop.Store.UpdateFaults(orderTimeoutArmed: true, ignoreIdempotencyKeys: true);
        shop.Store.Reset();

        Assert.Empty(shop.Store.GetOrders());
        Assert.Null(shop.Store.GetCart(cart.Id));
        Assert.Equal(Faults.Off, shop.Store.GetFaults());
        Assert.Equal("#1001", shop.Store.PlaceOrder(TestShop.GujiLines(), null, OrderSource.Api).Order!.OrderNumber);
    }

    [Fact]
    public void Reset_forgets_idempotency_keys()
    {
        var shop = new TestShop();
        shop.Store.PlaceOrder(TestShop.GujiLines(), "k", OrderSource.Api);
        shop.Store.Reset();

        Assert.Equal(OrderOutcome.Created, shop.Store.PlaceOrder(TestShop.GujiLines(), "k", OrderSource.Api).Outcome);
    }

    [Fact]
    public void Unavailable_variant_in_a_cart_is_rejected()
    {
        var shop = new TestShop();

        var result = shop.Store.CreateCart("copperhaze", [new LineRequest(TestShop.SidamoVariant, 1)], null);

        Assert.Equal(CartOutcome.Invalid, result.Outcome);
        Assert.Contains("sold out", result.Error);
        Assert.Equal(OrderOutcome.Invalid, shop.Store.PlaceOrder(
            new OrderRequest(Roaster: "copperhaze", Lines: [new LineRequest(TestShop.SidamoVariant, 1)]), null, OrderSource.Api).Outcome);
    }

    [Theory]
    [InlineData("nope", TestShop.GujiVariant, 1)]   // unknown roaster
    [InlineData("tidewell", TestShop.GujiVariant, 1)] // variant from another roaster
    [InlineData("copperhaze", 9999, 1)]             // unknown variant
    [InlineData("copperhaze", TestShop.GujiVariant, 0)]
    [InlineData("copperhaze", TestShop.GujiVariant, 21)]
    public void Invalid_cart_is_rejected(string roaster, long variant, int quantity)
    {
        var shop = new TestShop();

        var result = shop.Store.CreateCart(roaster, [new LineRequest(variant, quantity)], null);

        Assert.Equal(CartOutcome.Invalid, result.Outcome);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public void Cart_totals_add_shipping_once_per_roaster()
    {
        var shop = new TestShop();

        var cart = shop.Store.CreateCart("lanternfish", [new LineRequest(1021, 2), new LineRequest(1031, 1)], null).Cart!;

        Assert.StartsWith("c_", cart.Id);
        Assert.Equal(CartStatus.Open, cart.Status);
        Assert.Equal(2 * 14.00m + 13.00m, cart.Subtotal);
        Assert.Equal(4.90m, cart.Shipping);
        Assert.Equal(45.90m, cart.Total);
        Assert.Equal("EUR", cart.Currency);
    }

    [Fact]
    public void Order_timeout_fault_is_one_shot_and_only_for_api_orders()
    {
        var shop = new TestShop();
        shop.Store.UpdateFaults(orderTimeoutArmed: true);

        var checkout = shop.Store.PlaceOrder(TestShop.GujiLines(), null, OrderSource.Checkout);
        Assert.False(checkout.HoldResponse);
        Assert.True(shop.Store.GetFaults().OrderTimeoutArmed);

        var first = shop.Store.PlaceOrder(TestShop.GujiLines("p"), "p", OrderSource.Api);
        Assert.Equal(OrderOutcome.Created, first.Outcome);
        Assert.True(first.HoldResponse);
        Assert.False(shop.Store.GetFaults().OrderTimeoutArmed);

        // The retry arrives while the first reply is held: answered at once with the same order.
        var retry = shop.Store.PlaceOrder(TestShop.GujiLines("p"), "p", OrderSource.Api);
        Assert.Equal(OrderOutcome.Replayed, retry.Outcome);
        Assert.False(retry.HoldResponse);
        Assert.Equal(first.Order!.Id, retry.Order!.Id);
    }

    [Fact]
    public async Task Concurrent_requests_with_one_key_create_exactly_one_order()
    {
        var shop = new TestShop();
        var key = Guid.NewGuid().ToString();

        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ =>
            Task.Run(() => shop.Store.PlaceOrder(TestShop.GujiLines(key), key, OrderSource.Api))));

        Assert.Single(results, r => r.Outcome == OrderOutcome.Created);
        Assert.Equal(31, results.Count(r => r.Outcome == OrderOutcome.Replayed));
        Assert.Single(results.Select(r => r.Order!.Id).Distinct());
        Assert.Single(shop.Store.GetOrders());
    }

    [Fact]
    public void Update_faults_changes_only_the_fields_given()
    {
        var shop = new TestShop();

        Assert.Equal(new Faults(true, false), shop.Store.UpdateFaults(orderTimeoutArmed: true));
        Assert.Equal(new Faults(true, true), shop.Store.UpdateFaults(ignoreIdempotencyKeys: true));
        Assert.Equal(new Faults(false, true), shop.Store.UpdateFaults(orderTimeoutArmed: false));
    }
}
