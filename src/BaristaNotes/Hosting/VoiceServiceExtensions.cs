namespace BaristaNotes.Hosting;

internal static class VoiceServiceExtensions
{
    public static MauiAppBuilder AddVoiceServices(this MauiAppBuilder builder)
    {
        // Online SpeechToText for better accuracy (uses Apple's cloud services like Notes).
        builder.Services
            .AddSingleton<ISpeechToText>(SpeechToText.Default)
            .AddSingleton<ISpeechRecognitionService, SpeechRecognitionService>()
            .AddSingleton<IDataChangeNotifier, DataChangeNotifier>()
            .AddSingleton<INavigationRegistry, NavigationRegistry>()
            .AddScoped<BaristaNotes.Services.AI.NavigationTools>()
            .AddScoped<BaristaNotes.Services.AI.ProfileContextTools>()
            .AddScoped<BaristaNotes.Services.AI.PhotoQueryTools>()
            .AddScoped<VoiceCommandService>()
            .AddScoped<IVoiceCommandService>(sp => sp.GetRequiredService<VoiceCommandService>());

        // Cross-platform voice overlay via WindowOverlay pattern.
        builder.UseVoiceOverlay();

        return builder;
    }
}
