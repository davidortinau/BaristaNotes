using BaristaNotes.Core.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BaristaNotes.AndroidApp.Services;

internal static class NativeAiConfiguration
{
    public static ConfigurationManager Load(Stream baseSettings, params Stream?[] overrides)
    {
        var configuration = new ConfigurationManager();
        try
        {
            configuration.AddJsonStream(baseSettings);
            foreach (var settings in overrides)
                if (settings is not null)
                    configuration.AddJsonStream(settings);
            return configuration;
        }
        catch
        {
            configuration.Dispose();
            throw;
        }
    }

    public static void AddAI(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.AddBaristaNotesAI();
    }
}
