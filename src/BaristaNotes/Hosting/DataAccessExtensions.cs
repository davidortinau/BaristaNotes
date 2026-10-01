using BaristaNotes.Core.Hosting;

namespace BaristaNotes.Hosting;

internal static class DataAccessExtensions
{
    public static MauiAppBuilder AddDataAccess(this MauiAppBuilder builder)
    {
#if MAUI_PERFORMANCE
        var dbPath = MauiPerformanceFixture.PrepareDatabase();
#else
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "barista_notes.db");
#endif

        // Bootstrap-only Console.WriteLine: ILogger<T> isn't resolvable during
        // the DI container build phase (circular dependency). This is the only
        // acceptable use of Console.WriteLine in the application.
        Console.WriteLine($"Database path: {dbPath}");

        builder.Services.AddBaristaNotesData(dbPath);
#if MAUI_PERFORMANCE
        builder.Services.AddSingleton<IPreferencesStore, MauiPerformancePreferencesStore>();
#else
        builder.Services.AddSingleton<IPreferencesStore, MauiPreferencesStore>();
#endif

        return builder;
    }
}
