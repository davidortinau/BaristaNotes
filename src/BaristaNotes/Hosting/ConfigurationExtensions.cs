namespace BaristaNotes.Hosting;

internal static class ConfigurationExtensions
{
    /// <summary>
    /// Loads <c>appsettings.json</c> via Shiny's platform bundle support
    /// (Android Assets / iOS-Mac Bundle Resources / Windows embedded) and, in
    /// DEBUG, layers <c>appsettings.Development.json</c> on top for local API
    /// keys. Also registers the Syncfusion license if a key is present.
    /// </summary>
    public static MauiAppBuilder AddAppConfiguration(this MauiAppBuilder builder)
    {
#if DEBUG
        builder.Configuration.AddJsonPlatformBundle("Development");
#else
        builder.Configuration.AddJsonPlatformBundle();
#endif

        var sfKey = builder.Configuration["Syncfusion:Key"];
        if (!string.IsNullOrEmpty(sfKey))
        {
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(sfKey);
        }

        return builder;
    }
}
