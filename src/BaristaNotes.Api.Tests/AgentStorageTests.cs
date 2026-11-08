using BaristaNotes.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BaristaNotes.Api.Tests;

public class AgentStorageTests
{
    [Theory]
    [InlineData(null, "Microsoft.EntityFrameworkCore.Sqlite")]
    [InlineData("sqlite", "Microsoft.EntityFrameworkCore.Sqlite")]
    [InlineData("POSTGRES", "Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void Selects_the_requested_provider(string? provider, string expected)
    {
        var config = Configuration(provider, provider?.Equals("POSTGRES", StringComparison.OrdinalIgnoreCase) == true
            ? "Host=localhost;Database=agentdb;Username=postgres;Password=test"
            : null);
        var options = new DbContextOptionsBuilder<AgentDb>();
        AgentStorage.Configure(options, config, "/demo");
        using var db = new AgentDb(options.Options);

        Assert.Equal(expected, db.Database.ProviderName);
    }

    [Fact]
    public void Sqlite_keeps_the_existing_local_database_path()
    {
        var options = new DbContextOptionsBuilder<AgentDb>();
        AgentStorage.Configure(options, Configuration(null), "/demo");
        using var db = new AgentDb(options.Options);

        Assert.Equal("Data Source=/demo/agent.db", db.Database.GetConnectionString());
    }

    [Fact]
    public void Postgres_never_falls_back_to_sqlite_when_the_connection_is_missing()
    {
        var options = new DbContextOptionsBuilder<AgentDb>();
        Assert.Throws<InvalidOperationException>(() =>
            AgentStorage.Configure(options, Configuration("postgres"), "/demo"));
    }

    [Fact]
    public void Unknown_provider_is_an_error()
    {
        Assert.Throws<InvalidOperationException>(() => AgentStorage.Provider(Configuration("postgress")));
    }

    static IConfiguration Configuration(string? provider, string? connection = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Agent:Storage"] = provider,
            ["ConnectionStrings:agentdb"] = connection,
        }).Build();
}
