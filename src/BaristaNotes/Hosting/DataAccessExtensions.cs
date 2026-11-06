using BaristaNotes.Core.Hosting;

namespace BaristaNotes.Hosting;

internal static class DataAccessExtensions
{
    public static MauiAppBuilder AddDataAccess(this MauiAppBuilder builder)
    {
#if MAUI_PERFORMANCE
        var dbPath = MauiPerformanceFixture.PrepareDatabase();
#else
        // The AppHost sets BARISTANOTES_DEMO=1 so the demo never touches real data.
        var dbName = BaristaNotes.Services.Agent.AgentApiAddress.IsDemo ? "barista_notes_demo.db" : "barista_notes.db";
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, dbName);
#endif

        // Bootstrap-only Console.WriteLine: ILogger<T> isn't resolvable during
        // the DI container build phase (circular dependency). This is the only
        // acceptable use of Console.WriteLine in the application.
        Console.WriteLine($"Database path: {dbPath}");

        builder.Services.AddBaristaNotesData(dbPath);
#if MAUI_PERFORMANCE
        builder.Services.AddSingleton<IPreferencesStore, MauiPerformancePreferencesStore>();
#else
        // Demo mode keeps its own preferences, like its own database.
        builder.Services.AddSingleton<IPreferencesStore>(_ => new MauiPreferencesStore(
            BaristaNotes.Services.Agent.AgentApiAddress.IsDemo ? "com.simplyprofound.baristanotes.demo" : null));
#endif

        return builder;
    }
}
