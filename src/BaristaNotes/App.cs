using BaristaNotes.Core.Data;
using BaristaNotes.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BaristaNotes;

public class BaristaAppState
{
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
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Theme initialization
            var savedMode = await _themeService.GetThemeModeAsync();
            await _themeService.SetThemeModeAsync(savedMode);
            _logger.LogDebug("[STARTUP-ASYNC] Theme initialized: {ElapsedMs}ms", sw.ElapsedMilliseconds);

            // Database migration. Single-user dev app; on a fresh install pages
            // mounted before this completes can race. The migration is fast on
            // an empty DB, so we just fire-and-forget here rather than gating
            // the entire Shell behind it (which creates a second Window and
            // breaks WindowOverlay-based services).
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
            SetState(s => s.InitializationError = ex.Message);
        }
    }

    public override VisualNode Render()
    {
        // Always render AppShell so only one Window is created. Window-level
        // services (e.g. IOverlayService / VoiceOverlay) are bound to that
        // single Window's handler mapping. Pages that query the DB during
        // OnMounted should tolerate an in-flight migration on a fresh install.
        return new AppShell();
    }
}
