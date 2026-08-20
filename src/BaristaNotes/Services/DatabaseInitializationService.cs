namespace BaristaNotes.Services;

public sealed class DatabaseInitializationService(IServiceScopeFactory scopeFactory)
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
                    using var scope = scopeFactory.CreateScope();
                    scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().Initialize();
                });
            }

            return _initialization;
        }
    }
}
