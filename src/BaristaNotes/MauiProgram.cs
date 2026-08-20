namespace BaristaNotes;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .ConfigureBaristaApp()        // MauiReactor + theme + resources + UXDivers + CT + fonts + handlers
            .AddAppConfiguration()        // Shiny json bundle (+ Development) 
            .AddDataAccess()              // DbContext + repositories + preferences store
            .AddDomainServices()          // shot/equipment/bean/bag/profile/rating/recipe + popups
            .AddRecipeSourcing()          // 4 roaster adapters + registry + AI generator
            .AddImageServices()           // media picker + image picker/processing + vision
            .AddVoiceServices()           // speech-to-text + voice command + navigation + overlay
            .AddAIChatClients()           // Apple Intelligence (iOS) + advice + grind translation
            .AddDebugDiagnostics();       // #if DEBUG: Debug logger + DevFlow + HotReload Sentinel

        RouteRegistration.RegisterAll();

        var app = builder.Build();
        var databaseInitialization =
            app.Services.GetRequiredService<DatabaseInitializationService>();
        _ = databaseInitialization.InitializeAsync();

#if DEBUG
        // AI tool harness: drives the source-generated VoiceTools.Default.Tools
        // surface end-to-end through the live DI graph. Read with:
        //   maui devflow logs --limit 300 | grep AI-HARNESS
        _ = Task.Run(async () =>
        {
            await databaseInitialization.InitializeAsync();
            await BaristaNotes.Services.AI.AIToolHarness.RunAsync(app.Services);
        });
#endif

        // Theme initialization runs from BaristaApp.OnMounted().
        return app;
    }
}
