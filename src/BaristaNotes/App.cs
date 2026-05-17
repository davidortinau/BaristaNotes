using BaristaNotes.Core.Data;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using MauiReactor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes;

public class BaristaAppState
{
    public bool IsInitialized { get; set; }
    public string? InitializationError { get; set; }
}

public partial class BaristaApp : Component<BaristaAppState>
{
    [Inject] IThemeService _themeService;
    [Inject] IServiceProvider _serviceProvider;
    [Inject] ILogger<BaristaApp> _logger;

    protected override async void OnMounted()
    {
        base.OnMounted();
        
        // Run deferred initialization asynchronously to avoid blocking UI thread
        // This allows iOS to consider the app "responsive" while we complete setup
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Theme initialization (was blocking in MauiProgram)
            var savedMode = await _themeService.GetThemeModeAsync();
            await _themeService.SetThemeModeAsync(savedMode);
            _logger.LogDebug("[STARTUP-ASYNC] Theme initialized: {ElapsedMs}ms", sw.ElapsedMilliseconds);
            
            // Database migration (was blocking in MauiProgram)
            // Must complete before any page queries the DB, otherwise queries
            // race migrations and fail with "no such column"/"no such table".
            await Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<BaristaNotesContext>();
                await context.Database.MigrateAsync();
            });
            _logger.LogDebug("[STARTUP-ASYNC] Database migrated: {ElapsedMs}ms", sw.ElapsedMilliseconds);

            SetState(s => s.IsInitialized = true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during async initialization");
            SetState(s =>
            {
                s.InitializationError = ex.Message;
                s.IsInitialized = true; // allow Shell to render even on error so user isn't stuck
            });
        }
    }

    public override VisualNode Render()
    {
        // Gate Shell mounting behind migration completion. Pages inside Shell
        // (e.g. ShotLoggingPage) query the DB in OnMounted, so mounting them
        // before migrations complete races EF and throws "no such column".
        if (!State.IsInitialized)
        {
            return new MauiReactor.ContentPage(
                new MauiReactor.Grid(
                    new MauiReactor.VerticalStackLayout(
                        new MauiReactor.ActivityIndicator()
                            .IsRunning(true)
                            .HeightRequest(40),
                        new MauiReactor.Label("Starting BaristaNotes…")
                            .FontSize(16)
                            .HorizontalOptions(LayoutOptions.Center)
                    )
                    .Spacing(16)
                    .VerticalOptions(LayoutOptions.Center)
                    .HorizontalOptions(LayoutOptions.Center)
                )
            );
        }

        // Shell is the root - overlay is handled natively via IOverlayService
        return new AppShell();
    }
}
