using System.Globalization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace BaristaNotes.RoasterShop.Shop;

/// <summary>
/// Shopify-shaped HTTP API. Every route has a literal segment (products.json,
/// cart, api) so it never collides with Blazor pages or static files.
/// </summary>
public static class ShopApi
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotent-Replayed";

    public static IEndpointRouteBuilder MapShopApi(this IEndpointRouteBuilder app)
    {
        // Catalog
        app.MapGet("/roasters.json", (Catalog catalog) =>
            Results.Ok(new { roasters = catalog.Roasters.Select(Catalog.ToJson) }));

        app.MapGet("/{handle}/products.json", (string handle, Catalog catalog, ShopStore store) =>
        {
            var roaster = catalog.FindRoaster(handle);
            if (roaster is null) return NotFound($"Unknown roaster '{handle}'.");
            var today = catalog.Today;
            return Results.Ok(new { products = catalog.ProductsFor(roaster).Select(p => Catalog.ToJson(p, today, store.PriceOf(p))) });
        });

        // Cart permalink, like Shopify: /{handle}/cart/{variant}:{qty}[,{variant}:{qty}]
        app.MapGet("/{handle}/cart/{spec}", (string handle, string spec, Catalog catalog, ShopStore store) =>
        {
            if (catalog.FindRoaster(handle) is null) return NotFound($"Unknown roaster '{handle}'.");
            if (!TryParsePermalink(spec, out var lines))
                return Unprocessable("Use /{roaster}/cart/{variant_id}:{quantity}.");

            var result = store.CreateCart(handle, lines, attributes: null);
            return result.Outcome == CartOutcome.Created
                ? Results.Redirect($"/checkout/{result.Cart!.Id}")
                : Unprocessable(result.Error!);
        });

        var api = app.MapGroup("/api");

        // Carts
        api.MapPost("/carts", (CartRequest body, HttpRequest request, ShopStore store) =>
        {
            var result = store.CreateCart(body.Roaster, body.Lines, body.Attributes);
            if (result.Outcome != CartOutcome.Created) return Unprocessable(result.Error!);
            var cart = result.Cart!;
            return Results.Created($"/api/carts/{cart.Id}", CartResponse.From(cart, CheckoutUrl(request, cart.Id)));
        });

        api.MapGet("/carts/{id}", (string id, HttpRequest request, ShopStore store) =>
            store.GetCart(id) is { } cart
                ? Results.Ok(CartResponse.From(cart, CheckoutUrl(request, cart.Id)))
                : NotFound($"Cart {id} not found."));

        api.MapDelete("/carts/{id}", (string id, ShopStore store) => store.DeleteCart(id) switch
        {
            DeleteCartOutcome.Removed or DeleteCartOutcome.AlreadyRemoved => Results.NoContent(),
            DeleteCartOutcome.AlreadyOrdered => Conflict($"Cart {id} was already ordered and cannot be removed."),
            _ => NotFound($"Cart {id} not found."),
        });

        // Orders
        api.MapPost("/orders", async (
            OrderRequest body,
            HttpContext context,
            ShopStore store,
            IOptionsMonitor<FaultOptions> faultOptions,
            ILoggerFactory loggerFactory) =>
        {
            string? key = context.Request.Headers[IdempotencyKeyHeader];
            var result = store.PlaceOrder(body, key, OrderSource.Api);

            switch (result.Outcome)
            {
                case OrderOutcome.Replayed:
                    context.Response.Headers[ReplayedHeader] = "true";
                    return Results.Ok(result.Order);
                case OrderOutcome.NotFound:
                    return NotFound(result.Error!);
                case OrderOutcome.Conflict:
                    return Conflict(result.Error!);
                case OrderOutcome.Invalid:
                    return Unprocessable(result.Error!);
            }

            var order = result.Order!;
            if (result.HoldResponse)
            {
                // The order exists and its webhook is queued. Hold the reply so
                // the caller's attempt timeout fires and its retry arrives now.
                var delay = faultOptions.CurrentValue.OrderTimeoutDelay;
                loggerFactory.CreateLogger("BaristaNotes.RoasterShop.Faults").LogWarning(
                    "Order timeout fault: holding the response for {OrderNumber} for {Seconds}s",
                    order.OrderNumber, delay.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture));
                try
                {
                    await Task.Delay(delay, context.RequestAborted);
                }
                catch (OperationCanceledException)
                {
                    // The caller gave up. The order stays placed.
                    return Results.Empty;
                }
            }

            return Results.Created($"/api/orders/{order.Id}", order);
        });

        api.MapGet("/orders", (ShopStore store) => Results.Ok(new { orders = store.GetOrders() }));

        api.MapGet("/orders/{id}", (string id, ShopStore store) =>
            store.GetOrder(id) is { } order ? Results.Ok(order) : NotFound($"Order {id} not found."));

        // Demo controls
        api.MapGet("/demo/faults", (ShopStore store) => Results.Ok(store.GetFaults()));

        api.MapPost("/demo/faults", (FaultsUpdate body, ShopStore store) =>
            Results.Ok(store.UpdateFaults(body.OrderTimeoutArmed, body.IgnoreIdempotencyKeys, body.PriceIncreasePercent)));

        api.MapPost("/demo/reset", (ShopStore store) =>
        {
            store.Reset();
            return Results.NoContent();
        });

        return app;
    }

    /// <summary>
    /// API responses carry their own JSON bodies. Keep the Blazor status code
    /// page (HTML) away from them, including the framework's own 400s.
    /// </summary>
    public static IApplicationBuilder UseApiStatusCodes(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (IsApiPath(context.Request.Path) && context.Features.Get<IStatusCodePagesFeature>() is { } feature)
                feature.Enabled = false;
            await next(context);
        });

    public static bool IsApiPath(PathString path)
    {
        var value = path.Value ?? "";
        if (path.StartsWithSegments("/api") || value.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return true;
        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 3 && segments[1] == "cart";
    }

    public static bool TryParsePermalink(string spec, out List<LineRequest> lines)
    {
        lines = [];
        foreach (var part in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pieces = part.Split(':');
            if (pieces.Length != 2
                || !long.TryParse(pieces[0], NumberStyles.None, CultureInfo.InvariantCulture, out var variant)
                || !int.TryParse(pieces[1], NumberStyles.None, CultureInfo.InvariantCulture, out var quantity))
            {
                return false;
            }
            lines.Add(new LineRequest(variant, quantity));
        }
        return lines.Count > 0;
    }

    private static string CheckoutUrl(HttpRequest request, string cartId) =>
        $"{request.Scheme}://{request.Host}{request.PathBase}/checkout/{cartId}";

    private static IResult NotFound(string error) => Results.NotFound(new ErrorResponse(error));

    private static IResult Conflict(string error) => Results.Conflict(new ErrorResponse(error));

    private static IResult Unprocessable(string error) => Results.UnprocessableEntity(new ErrorResponse(error));
}
