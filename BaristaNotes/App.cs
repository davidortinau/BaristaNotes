using BaristaNotes.Core.Data;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using MauiReactor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes;

public partial class BaristaApp : Component
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
            await Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<BaristaNotesContext>();
                await context.Database.MigrateAsync();
            });
            _logger.LogDebug("[STARTUP-ASYNC] Database migrated: {ElapsedMs}ms", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during async initialization");
        }
    }

    public override VisualNode Render()
    {
        // Shell is the root - overlay is handled natively via IOverlayService
        return new AppShell();
    }
}
