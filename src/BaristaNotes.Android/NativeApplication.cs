using Android.App;
using Android.Runtime;
using BaristaNotes.AndroidApp.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using BaristaNotes.Core.Hosting;
using BaristaNotes.Core.Services;
using BaristaNotes.AndroidApp.Services;
using Microsoft.Extensions.Configuration;

namespace BaristaNotes.AndroidApp;

[Application]
public sealed partial class NativeApplication(IntPtr handle, JniHandleOwnership ownership)
    : Application(handle, ownership)
{
    private ILoggerFactory? _loggerFactory;
    private ServiceProvider? _services;
    private ConfigurationManager? _configuration;
    internal NativeThemeService ThemeService { get; private set; } = null!;

    public IServiceProvider Services => _services
        ?? throw new InvalidOperationException("Native application services are not initialized.");

    public ILoggerFactory LoggerFactory => _loggerFactory
        ?? throw new InvalidOperationException("Native application logging has not been initialized.");

    public override void OnCreate()
    {
        base.OnCreate();
        _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
#if DEBUG
            builder.SetMinimumLevel(LogLevel.Debug);
#else
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
#endif
            builder.AddProvider(new AndroidLogProvider());
        });

        LoggerFactory.CreateLogger<NativeApplication>().LogInformation(
            "Starting native Android application {PackageId}", PackageName);
        var dataDirectory = FilesDir?.AbsolutePath
            ?? throw new InvalidOperationException("The Android app-data directory is unavailable.");
        Directory.CreateDirectory(dataDirectory);
        var databasePath = Path.Combine(dataDirectory, "barista_notes.db");
        ConfigurePerformanceDatabase(dataDirectory, ref databasePath);
        var services = new ServiceCollection();
        services.AddSingleton(LoggerFactory);
        services.AddSingleton<IPreferencesStore>(provider => new AndroidPreferencesStore(
            preferences: GetSharedPreferences("barista_notes", Android.Content.FileCreationMode.Private)
                ?? throw new InvalidOperationException("The native preferences store is unavailable."),
            logger: provider.GetRequiredService<ILogger<AndroidPreferencesStore>>()));
        services.AddScoped<IImageProcessingService>(provider => new AndroidImageProcessingService(
            dataDirectory: dataDirectory,
            logger: provider.GetRequiredService<ILogger<AndroidImageProcessingService>>()));
        services.AddBaristaNotesCore(databasePath);
        services.AddBaristaNotesOriginGeocoding(dataDirectory);
        using var baseSettings = Assets!.Open("appsettings.json");
        using var bundledDevelopmentSettings = Assets.List("")?.Contains(
            "appsettings.Development.json", StringComparer.Ordinal) == true
            ? Assets.Open("appsettings.Development.json")
            : null;
#if DEBUG
        var developmentPath = Path.Combine(dataDirectory, "appsettings.Development.json");
        using var sandboxDevelopmentSettings = File.Exists(developmentPath) ? File.OpenRead(developmentPath) : null;
        _configuration = NativeAiConfiguration.Load(
            baseSettings,
            bundledDevelopmentSettings,
            sandboxDevelopmentSettings);
#else
        _configuration = NativeAiConfiguration.Load(baseSettings, bundledDevelopmentSettings);
#endif
        NativeAiConfiguration.AddAI(services, _configuration);
        services.AddScoped<NativeVoicePlatformActions>();
        services.AddScoped<IVoicePlatformActions>(provider => provider.GetRequiredService<NativeVoicePlatformActions>());
        services.AddScoped<IOverlayService>(provider => provider.GetRequiredService<NativeVoicePlatformActions>().Overlay);
        services.AddScoped<ISpeechRecognitionService>(provider => provider.GetRequiredService<NativeVoicePlatformActions>().Speech);
        services.AddBaristaNotesVoice();
        ConfigureVoiceTestServices(services);
        _services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        ConfigurePerformancePreferences();
        ThemeService = new NativeThemeService(this, _services.GetRequiredService<IPreferencesStore>(),
            LoggerFactory.CreateLogger<NativeThemeService>());
#if DEBUG
        StartInspectionAgent();
#endif
    }

    // No implementation is compiled into ordinary builds. A reviewed external,
    // Debug-only fixture can provide scripted interfaces without production UI paths.
    partial void ConfigureVoiceTestServices(IServiceCollection services);
    partial void ConfigurePerformanceDatabase(string dataDirectory, ref string databasePath);
    partial void ConfigurePerformancePreferences();

#if DEBUG
    private void StartInspectionAgent()
    {
        var logger = LoggerFactory.CreateLogger<NativeApplication>();
        try
        {
            using var options = new AndroidAiloha.AilohaAgentOptions(
                port: 9233,
                brokerPort: 19323,
                enableBrokerRegistration: false,
                enableNetworkCapture: false,
                enableLogCapture: false,
                enableProfiler: false,
                enableWebView: false,
                appName: "BaristaNotes Native");
            // Direct forwarded-port inspection avoids broker HTTP registration on
            // Android's main thread and needs no cleartext network-policy override.
            var port = AndroidAiloha.AilohaAndroidAgent.Start(application: this, options: options);
            logger.LogInformation("Native Ailoha 0.1.13 started on loopback port {AgentPort}", port);
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Native Ailoha startup failed for {PackageId}", PackageName);
            throw;
        }
    }
#endif

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _services?.Dispose();
            _services = null;
            _configuration?.Dispose();
            _configuration = null;
            _loggerFactory?.Dispose();
            _loggerFactory = null;
        }

        base.Dispose(disposing);
    }
}
