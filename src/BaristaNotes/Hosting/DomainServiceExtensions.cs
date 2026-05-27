namespace BaristaNotes.Hosting;

internal static class DomainServiceExtensions
{
    public static MauiAppBuilder AddDomainServices(this MauiAppBuilder builder)
    {
        builder.Services
            .AddScoped<IShotService, ShotService>()
            .AddScoped<IEquipmentService, EquipmentService>()
            .AddScoped<IBeanService, BeanService>()
            .AddScoped<IBagService, BagService>()
            .AddScoped<IUserProfileService, UserProfileService>()
            .AddScoped<IRatingService, RatingService>()
            .AddScoped<IRecipeService, RecipeService>();

        builder.Services
            .AddSingleton<IPreferencesService, PreferencesService>()
            .AddSingleton<IFeedbackService, FeedbackService>()
            .AddSingleton<IThemeService, ThemeService>();

        // Popups
        builder.Services.AddTransient<Integrations.Popups.AddCoffeePopup>();

        return builder;
    }
}
