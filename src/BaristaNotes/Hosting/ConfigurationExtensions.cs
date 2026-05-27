namespace BaristaNotes.Hosting;

internal static class ConfigurationExtensions
{
    /// <summary>
    /// Loads <c>appsettings.json</c> via Shiny's platform bundle support
    /// (Android Assets / iOS-Mac Bundle Resources / Windows embedded) and, in
    /// DEBUG, layers <c>appsettings.Development.json</c> on top for local API
    /// keys. 
    /// </summary>
    public static MauiAppBuilder AddAppConfiguration(this MauiAppBuilder builder)
    {
#if DEBUG
        builder.Configuration.AddJsonPlatformBundle("Development");
#else
        builder.Configuration.AddJsonPlatformBundle();
#endif

        return builder;
    }
}
