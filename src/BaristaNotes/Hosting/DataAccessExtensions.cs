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

        builder.Services.AddDbContext<BaristaNotesContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        builder.Services
            .AddScoped<IEquipmentRepository, EquipmentRepository>()
            .AddScoped<IBeanRepository, BeanRepository>()
            .AddScoped<IBagRepository, BagRepository>()
            .AddScoped<IUserProfileRepository, UserProfileRepository>()
            .AddScoped<IShotRecordRepository, ShotRecordRepository>()
            .AddScoped<IRecipeRepository, RecipeRepository>()
            .AddScoped<IGrinderProfileRepository, GrinderProfileRepository>()
            .AddScoped<IGrindTranslationCacheRepository, GrindTranslationCacheRepository>()
            .AddSingleton<IPreferencesStore, MauiPreferencesStore>();

        return builder;
    }
}
