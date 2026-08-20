using BaristaNotes.Core.Data;

namespace BaristaNotes;

public class BaristaAppState
{
    public string? InitializationError { get; set; }
}

#if DEBUG
// MauiReactor's source generator emits the parameterless ctor for Component<T>,
// so HotReloadInitialize() is called from OnMounted instead. The MUH0001
// analyzer only inspects constructors, so suppress it here.
#pragma warning disable MUH0001
#endif
public partial class BaristaApp : Component<BaristaAppState>
#if DEBUG
    , Microsoft.Maui.Labs.HotReload.IHotReloadAware
#endif
{
    [Inject] IThemeService _themeService;
    [Inject] ILogger<BaristaApp> _logger;

#if DEBUG

    public void OnHotReload(Type[]? updatedTypes)
    {
        var names = updatedTypes is null
            ? "<null>"
            : string.Join(", ", updatedTypes.Select(t => t.FullName));
        _logger?.LogInformation("🔥 BaristaApp.OnHotReload fired. Updated types: {Types}", names);
        // Force a re-render of the MauiReactor component tree so view-level
        // edits are reflected immediately, even when no state changed.
        Invalidate();
    }
#endif

    protected override async void OnMounted()
    {
        base.OnMounted();
#if DEBUG
        // Register with the HotReload registry (source-gen emitted method).
        HotReloadInitialize();
#endif
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
#if DEBUG
#pragma warning restore MUH0001
#endif
