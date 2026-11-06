namespace BaristaNotes.Services.Agent;

/// <summary>
/// Finds the BaristaNotes API address. The Aspire AppHost passes it as an
/// environment variable when it starts the app. Returns null when the app runs
/// on its own.
/// </summary>
public static class AgentApiAddress
{
    public const string DemoVariable = "BARISTANOTES_DEMO";

    public static Uri? Resolve()
    {
        foreach (var name in new[] { "services__api__https__0", "services__api__http__0" })
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                return uri;
            }
        }

        return null;
    }

    /// <summary>True when the AppHost asks for the separate demo database.</summary>
    public static bool IsDemo => Environment.GetEnvironmentVariable(DemoVariable) == "1";
}
