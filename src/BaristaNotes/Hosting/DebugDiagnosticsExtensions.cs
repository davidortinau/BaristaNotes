namespace BaristaNotes.Hosting;

internal static class DebugDiagnosticsExtensions
{
    /// <summary>
    /// Wires up the Debug-only diagnostic stack: ILogger Debug provider and the
    /// MAUI DevFlow agent for broker and visual-tree inspection.
    /// </summary>
    public static MauiAppBuilder AddDebugDiagnostics(this MauiAppBuilder builder)
    {
#if DEBUG
        builder.Logging.AddDebug();
        Microsoft.Maui.DevFlow.Agent.AgentServiceExtensions.AddMauiDevFlowAgent(builder);
#endif
        return builder;
    }
}
