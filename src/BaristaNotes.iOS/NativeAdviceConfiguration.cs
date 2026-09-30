using BaristaNotes.Core.Hosting;
using Foundation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace BaristaNotes.Native.iOS;

internal static class NativeAdviceConfiguration
{
    public static void AddAdvice(IServiceCollection services, string dataDirectory, ILoggerFactory logging)
    {
        var bundlePath = NSBundle.MainBundle.BundlePath;
        var builder = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(bundlePath, "appsettings.json"), optional: false, reloadOnChange: false)
            .AddJsonFile(Path.Combine(bundlePath, "appsettings.Development.json"), optional: true, reloadOnChange: false);
#if DEBUG
        // An app-sandbox file can override the bundled development configuration during local debugging.
        builder.AddJsonFile(Path.Combine(dataDirectory, "appsettings.Development.json"), optional: true, reloadOnChange: false);
#endif
        services.AddSingleton<IConfiguration>(_ => builder.Build());
        if (OperatingSystem.IsIOSVersionAtLeast(26))
        {
            try
            {
#pragma warning disable MAUIAI0001
                var client = new AppleIntelligenceChatClient(logging);
                services.AddSingleton<IChatClient>(client);
#pragma warning restore MAUIAI0001
                logging.CreateLogger("NativeAdvice").LogInformation(
                    "Apple Intelligence client registered; registration does not establish model availability");
            }
            catch (Exception error)
            {
                logging.CreateLogger("NativeAdvice").LogWarning(error, "Apple Intelligence client registration failed");
            }
        }
        services.AddBaristaNotesAI();
    }
}
