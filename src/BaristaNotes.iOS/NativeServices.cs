using BaristaNotes.Core.Hosting;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Foundation;

namespace BaristaNotes.Native.iOS;

internal sealed class NativeServices : IDisposable
{
    private readonly ServiceProvider _provider;
    public string DataDirectory { get; }

    public NativeServices(ILoggerFactory logging)
    {
        DataDirectory = NSFileManager.DefaultManager.GetUrls(
            NSSearchPathDirectory.LibraryDirectory, NSSearchPathDomain.User)[0].Path
            ?? throw new IOException("iOS did not provide the application Library directory.");
        Directory.CreateDirectory(DataDirectory);
        var services = new ServiceCollection();
        services.AddSingleton(logging);
#if NATIVE_PERFORMANCE
        var performancePreferences = new NativePerformancePreferencesStore();
        services.AddSingleton<IPreferencesStore>(performancePreferences);
        var databasePath = NativePerformanceFixture.PrepareDatabase(DataDirectory);
#else
        services.AddSingleton<IPreferencesStore, NativePreferencesStore>();
        var databasePath = Path.Combine(DataDirectory, "barista_notes.db");
#endif
        services.AddSingleton<IImageProcessingService>(provider =>
            new NativeImageProcessingService(DataDirectory, provider.GetRequiredService<ILogger<NativeImageProcessingService>>()));
        services.AddBaristaNotesCore(databasePath);
        services.AddBaristaNotesOriginGeocoding(DataDirectory);
        NativeAdviceConfiguration.AddAdvice(services, DataDirectory, logging);
        services.AddSingleton<NativeVoiceOverlay>();
        services.AddSingleton<IOverlayService>(provider => provider.GetRequiredService<NativeVoiceOverlay>());
        services.AddSingleton<NativeCameraCapture>();
        services.AddSingleton<INativePhotoCapture>(provider => provider.GetRequiredService<NativeCameraCapture>());
        services.AddSingleton<NativeVoicePlatformActions>();
        services.AddSingleton<IVoicePlatformActions>(provider => provider.GetRequiredService<NativeVoicePlatformActions>());
        services.AddScoped<ISpeechRecognitionService, NativeSpeechRecognitionService>();
        services.AddBaristaNotesVoice();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
#if NATIVE_PERFORMANCE
        NativePerformanceFixture.ConfigurePreferences(_provider);
#endif
    }

    public T Singleton<T>() where T : notnull => _provider.GetRequiredService<T>();
    internal AsyncServiceScope CreateVoiceScope() => _provider.CreateAsyncScope();
    public void OnUi(Action<IServiceProvider> operation)
    {
        using var scope = _provider.CreateScope();
        operation(scope.ServiceProvider);
    }

    // Each native operation owns a fresh context scope; EF work never shares a UI-lived context.
    public Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> operation) => Task.Run(async () =>
    {
        await using var scope = _provider.CreateAsyncScope();
        return await operation(scope.ServiceProvider);
    });

    public Task InitializeAsync() => Singleton<DatabaseInitializationService>().InitializeAsync();
    public void Dispose() => _provider.Dispose();
}

internal sealed class NativePreferencesStore : IPreferencesStore
{
    private static NSUserDefaults Store => NSUserDefaults.StandardUserDefaults;
    public string? Get(string key, string? defaultValue) => Store.StringForKey(key) ?? defaultValue;
    public int Get(string key, int defaultValue) => Store[key] == null ? defaultValue : (int)Store.IntForKey(key);
    public double Get(string key, double defaultValue) => Store[key] == null ? defaultValue : Store.DoubleForKey(key);
    public void Set(string key, string value) => Store.SetString(value, key);
    public void Set(string key, int value) => Store.SetInt(value, key);
    public void Set(string key, double value) => Store.SetDouble(value, key);
    public void Remove(string key) => Store.RemoveObject(key);
}
