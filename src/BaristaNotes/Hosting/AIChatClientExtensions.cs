#if IOS
using BaristaNotes.Platforms.iOS;
#endif

namespace BaristaNotes.Hosting;

internal static class AIChatClientExtensions
{
    public static MauiAppBuilder AddAIChatClients(this MauiAppBuilder builder)
    {
#if IOS
#pragma warning disable CA1416 // Validate platform compatibility
        // Apple Intelligence requires iOS 26.0+. VoiceCommandService falls back
        // to OpenAI when local AI isn't available or fails.
        try
        {
            var appleIntelligenceClient = new AppleIntelligenceChatClient();
            builder.Services.AddSingleton<Microsoft.Extensions.AI.IChatClient>(appleIntelligenceClient);
            Console.WriteLine("Apple Intelligence chat client registered successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Apple Intelligence not available, will use OpenAI: {ex.Message}");
        }
#pragma warning restore CA1416
#endif

        builder.Services
            .AddSingleton<IAIAdviceService, AIAdviceService>()
            .AddSingleton<IGrindTranslationAI, GrindTranslationAI>()
            .AddScoped<IGrindTranslationService, GrindTranslationService>();

        return builder;
    }
}
