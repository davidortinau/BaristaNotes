using System.Diagnostics.CodeAnalysis;
using BaristaNotes.Core.Data;
using BaristaNotes.Core.Data.CompiledModels;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Grind;
using BaristaNotes.Core.Services.Recipes;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BaristaNotes.Core.Hosting;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers shared app services. The head supplies IPreferencesStore and
    /// IImageProcessingService before resolving services that use them.
    /// </summary>
    public static IServiceCollection AddBaristaNotesCore(
        this IServiceCollection services,
        string databasePath) =>
        services
            .AddBaristaNotesData(databasePath)
            .AddBaristaNotesDomain()
            .AddBaristaNotesRecipeSourcing();

    public static IServiceCollection AddBaristaNotesData(
        this IServiceCollection services,
        string databasePath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        }.ToString();

        services.AddLogging();
        services.AddDbContext<BaristaNotesContext>(options =>
            options
                .UseModel(BaristaNotesContextModel.Instance)
                .UseSqlite(connectionString));

        services.TryAddScoped<DatabaseInitializer>();
        services.TryAddSingleton<DatabaseInitializationService>();
        services.TryAddScoped<IEquipmentRepository, EquipmentRepository>();
        services.TryAddScoped<IBeanRepository, BeanRepository>();
        services.TryAddScoped<IBagRepository, BagRepository>();
        services.TryAddScoped<IUserProfileRepository, UserProfileRepository>();
        services.TryAddScoped<IShotRecordRepository, ShotRecordRepository>();
        services.TryAddScoped<IRecipeRepository, RecipeRepository>();
        services.TryAddScoped<IGrinderProfileRepository, GrinderProfileRepository>();
        services.TryAddScoped<IGrindTranslationCacheRepository, GrindTranslationCacheRepository>();
        return services;
    }

    public static IServiceCollection AddBaristaNotesDomain(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLogging();
        services.TryAddScoped<IShotService, ShotService>();
        services.TryAddScoped<IEquipmentService, EquipmentService>();
        services.TryAddScoped<IBeanService, BeanService>();
        services.TryAddScoped<IBagService, BagService>();
        services.TryAddScoped<IUserProfileService, UserProfileService>();
        services.TryAddScoped<IRatingService, RatingService>();
        services.TryAddScoped<IRecipeService, RecipeService>();
        services.TryAddScoped<IGrindTranslationService, GrindTranslationService>();
        services.TryAddScoped<DrinkWorkflow>();
        services.TryAddScoped<GrindPickerWorkflow>();
        services.TryAddScoped<EquipmentWorkflow>();
        services.TryAddScoped<ProfileWorkflow>();
        services.TryAddScoped<BeanWorkflow>();
        services.TryAddScoped<BagWorkflow>();
        services.TryAddScoped<BagDetailsWorkflow>();
        services.TryAddScoped<BeanCreationWorkflow>();
        services.TryAddScoped<AddCoffeeWorkflow>();
        services.TryAddSingleton<IPreferencesService, PreferencesService>();
        services.TryAddSingleton<IDrinkValueRangeService, DrinkValueRangeService>();
        services.TryAddSingleton<IDataChangeNotifier, DataChangeNotifier>();
        services.TryAddSingleton<INavigationRegistry, NavigationRegistry>();
        return services;
    }

    public static IServiceCollection AddBaristaNotesRecipeSourcing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient();
        services
            .AddRoasterAdapter<OnyxCoffeeLabAdapter>()
            .AddRoasterAdapter<CounterCultureAdapter>()
            .AddRoasterAdapter<BlueBottleAdapter>()
            .AddRoasterAdapter<IntelligentsiaAdapter>();

        services.TryAddSingleton<IRoasterRecipeAdapterRegistry, RoasterRecipeAdapterRegistry>();
        services.TryAddSingleton<IAIRecipeGenerator, NullAIRecipeGenerator>();
        services.TryAddScoped<IRecipeSourcingService, RecipeSourcingService>();
        return services;
    }

    public static IServiceCollection AddBaristaNotesAI(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddLogging();
        services.TryAddSingleton<IAIAdviceService, AIAdviceService>();
        services.TryAddSingleton<IGrindTranslationAI, GrindTranslationAI>();
        services.TryAddSingleton<IVisionService, VisionService>();
        return services;
    }

    public static IServiceCollection AddBaristaNotesVoice(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddLogging();
        services.TryAddScoped<BaristaNotes.Services.AI.NavigationTools>();
        services.TryAddScoped<BaristaNotes.Services.AI.ProfileContextTools>();
        services.TryAddScoped<BaristaNotes.Services.AI.PhotoQueryTools>();
        services.TryAddScoped<BaristaNotes.Services.VoiceCommandService>();
        services.TryAddScoped<IVoiceCommandService>(provider =>
            provider.GetRequiredService<BaristaNotes.Services.VoiceCommandService>());
        return services;
    }

    private static IServiceCollection AddRoasterAdapter<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAdapter>(
        this IServiceCollection services)
        where TAdapter : class, IRoasterRecipeAdapter
    {
        services.AddSingleton<IRoasterRecipeAdapter>(provider =>
            ActivatorUtilities.CreateInstance<TAdapter>(
                provider,
                provider.GetRequiredService<IHttpClientFactory>().CreateClient("recipes")));
        return services;
    }
}
