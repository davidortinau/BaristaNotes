using BaristaNotes.Core.Hosting;

namespace BaristaNotes.Hosting;

internal static class DataAccessExtensions
{
    public static MauiAppBuilder AddDataAccess(this MauiAppBuilder builder)
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "barista_notes.db");

        // Bootstrap-only Console.WriteLine: ILogger<T> isn't resolvable during
        // the DI container build phase (circular dependency). This is the only
        // acceptable use of Console.WriteLine in the application.
        Console.WriteLine($"Database path: {dbPath}");

        builder.Services
            .AddBaristaNotesData(dbPath)
            .AddSingleton<IPreferencesStore, MauiPreferencesStore>();

        return builder;
    }
}
