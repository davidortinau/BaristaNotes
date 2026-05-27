namespace BaristaNotes.Hosting;

internal static class RecipeSourcingExtensions
{
    public static MauiAppBuilder AddRecipeSourcing(this MauiAppBuilder builder)
    {
        builder.Services.AddHttpClient();

        builder.Services
            .AddRoasterAdapter<OnyxCoffeeLabAdapter>()
            .AddRoasterAdapter<CounterCultureAdapter>()
            .AddRoasterAdapter<BlueBottleAdapter>()
            .AddRoasterAdapter<IntelligentsiaAdapter>();

        builder.Services
            .AddSingleton<IRoasterRecipeAdapterRegistry, RoasterRecipeAdapterRegistry>()
            .AddSingleton<IAIRecipeGenerator, NullAIRecipeGenerator>()
            .AddScoped<IRecipeSourcingService, RecipeSourcingService>();

        return builder;
    }

    /// <summary>
    /// Registers a <see cref="IRoasterRecipeAdapter"/> implementation that
    /// expects <c>(HttpClient, ILogger&lt;TAdapter&gt;)</c> via the named
    /// <c>"recipes"</c> HttpClient.
    /// </summary>
    private static IServiceCollection AddRoasterAdapter<TAdapter>(this IServiceCollection services)
        where TAdapter : class, IRoasterRecipeAdapter
        => services.AddSingleton<IRoasterRecipeAdapter>(sp =>
            ActivatorUtilities.CreateInstance<TAdapter>(
                sp,
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("recipes")));
}
