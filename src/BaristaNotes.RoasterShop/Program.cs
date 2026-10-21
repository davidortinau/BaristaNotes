using BaristaNotes.RoasterShop.Components;
using BaristaNotes.RoasterShop.Shop;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents();

// Shopify-shaped JSON: snake_case, money as strings.
builder.Services.ConfigureHttpJsonOptions(options => ShopJson.Configure(options.SerializerOptions));

builder.Services.Configure<WebhookOptions>(builder.Configuration.GetSection(WebhookOptions.Section));
builder.Services.Configure<FaultOptions>(builder.Configuration.GetSection(FaultOptions.Section));

// Everything lives in memory. Restarting the shop resets it.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<Catalog>();
builder.Services.AddSingleton<WebhookQueue>();
builder.Services.AddSingleton<IOrderNotifier>(sp => sp.GetRequiredService<WebhookQueue>());
builder.Services.AddSingleton<ShopStore>();

// Service defaults add service discovery and the standard resilience handler to this client.
builder.Services.AddHttpClient(WebhookSender.ClientName);
builder.Services.AddHostedService<WebhookSender>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseApiStatusCodes();
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapDefaultEndpoints();
app.MapStaticAssets();

// Cross-site protection for the JSON API: a web page cannot send a custom
// header to another site without a CORS preflight, and this shop answers none.
// The checkout and admin pages use antiforgery tokens instead.
app.Use(async (context, next) =>
{
    var method = context.Request.Method;
    if (context.Request.Path.StartsWithSegments("/api")
        && !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method)
        && string.IsNullOrEmpty(context.Request.Headers["X-Requested-By"]))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { error = "Missing the X-Requested-By header." });
        return;
    }

    await next();
});

// API before Razor components.
app.MapShopApi();
app.MapRazorComponents<App>();

app.Run();
