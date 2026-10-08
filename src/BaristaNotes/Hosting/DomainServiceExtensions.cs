using BaristaNotes.Core.Hosting;

namespace BaristaNotes.Hosting;

internal static class DomainServiceExtensions
{
    public static MauiAppBuilder AddDomainServices(this MauiAppBuilder builder)
    {
        builder.Services
            .AddBaristaNotesDomain()
            .AddSingleton<IFeedbackService, FeedbackService>()
            .AddSingleton<IThemeService, ThemeService>();
#if IOS || ANDROID
        builder.Services.AddHttpClient("origin-geocoder")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        builder.Services.AddSingleton(provider => new BaristaNotes.Core.Services.Origins.NominatimOriginGeocoder(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("origin-geocoder"),
            provider.GetRequiredService<IPreferencesStore>(),
            provider.GetRequiredService<ILogger<BaristaNotes.Core.Services.Origins.NominatimOriginGeocoder>>()));
#endif

        // Popups
        builder.Services.AddTransient<Integrations.Popups.AddCoffeePopup>();

        return builder;
    }
}
