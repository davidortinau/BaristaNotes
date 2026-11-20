using BaristaNotes.Core.Data;

namespace BaristaNotes;

public class BaristaAppState
{
    public string? InitializationError { get; set; }
}

public partial class BaristaApp : Component<BaristaAppState>
{
    [Inject] IThemeService _themeService;
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
        // single Window's handler mapping.
        return new AppShell();
    }
}
