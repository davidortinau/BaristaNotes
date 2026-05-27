namespace BaristaNotes.Hosting;

internal static class DebugDiagnosticsExtensions
{
    /// <summary>
    /// Wires up the Debug-only diagnostic stack: ILogger Debug provider, MAUI
    /// DevFlow agent (for the broker + visual-tree inspection), and the
    /// HotReload Sentinel hooks. No-op in Release builds.
    /// </summary>
    public static MauiAppBuilder AddDebugDiagnostics(this MauiAppBuilder builder)
    {
#if DEBUG
        builder.Logging.AddDebug();
        Microsoft.Maui.DevFlow.Agent.AgentServiceExtensions.AddMauiDevFlowAgent(builder);
        HotReloadSentinel.Diagnostics.HotReloadDiagnosticsExtensions.UseHotReloadDiagnostics(builder);
        // HotReloadSentinel.Diagnostics.Maui.MauiAppBuilderExtensions.UseHotReloadOverlay(builder);
#endif
        return builder;
    }
}
