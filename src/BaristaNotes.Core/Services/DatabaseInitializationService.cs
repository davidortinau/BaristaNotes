using BaristaNotes.Core.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services;

public sealed class DatabaseInitializationService(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseInitializationService> logger)
{
    private readonly object _sync = new();
    private Task? _initialization;

    public Task InitializeAsync()
    {
        lock (_sync)
        {
            if (_initialization is null || (_initialization.IsCompleted && !_initialization.IsCompletedSuccessfully))
            {
                _initialization = Task.Run(() =>
                {
                    try
                    {
                        logger.LogDebug("Initializing the app database");
                        using var scope = scopeFactory.CreateScope();
                        scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().Initialize();
                        logger.LogInformation("App database initialization completed");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "App database initialization failed");
                        throw;
                    }
                });
            }

            return _initialization;
        }
    }
}
