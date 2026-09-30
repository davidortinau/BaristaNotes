using BaristaNotes.Core.Hosting;

namespace BaristaNotes.Hosting;

internal static class VoiceServiceExtensions
{
    public static MauiAppBuilder AddVoiceServices(this MauiAppBuilder builder)
    {
        // Online SpeechToText for better accuracy (uses Apple's cloud services like Notes).
        builder.Services
            .AddSingleton<ISpeechToText>(SpeechToText.Default)
            .AddSingleton<ISpeechRecognitionService, SpeechRecognitionService>()
            .AddSingleton<IVoicePlatformActions, MauiVoicePlatformActions>()
            .AddBaristaNotesVoice();

        // Cross-platform voice overlay via WindowOverlay pattern.
        builder.UseVoiceOverlay();

        return builder;
    }
}
