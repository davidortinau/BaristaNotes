using Microsoft.EntityFrameworkCore;

namespace BaristaNotes.Api.Data;

public static class AgentStorage
{
    public static string Provider(IConfiguration configuration) =>
        (configuration["Agent:Storage"] ?? "sqlite").ToLowerInvariant() switch
        {
            "sqlite" => "sqlite",
            "postgres" => "postgres",
            _ => throw new InvalidOperationException("Agent:Storage must be 'sqlite' or 'postgres'."),
        };

    public static void Configure(DbContextOptionsBuilder options, IConfiguration configuration, string dataDirectory)
    {
        var connection = configuration.GetConnectionString("agentdb");
        if (Provider(configuration) == "postgres")
        {
            if (string.IsNullOrWhiteSpace(connection))
            {
                throw new InvalidOperationException("Postgres storage requires ConnectionStrings:agentdb from the AppHost.");
            }

            options.UseNpgsql(connection);
        }
        else
        {
            options.UseSqlite(connection ?? $"Data Source={Path.Combine(dataDirectory, "agent.db")}");
        }
    }
}
