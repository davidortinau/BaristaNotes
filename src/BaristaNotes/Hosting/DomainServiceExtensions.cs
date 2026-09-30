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

        // Popups
        builder.Services.AddTransient<Integrations.Popups.AddCoffeePopup>();

        return builder;
    }
}
