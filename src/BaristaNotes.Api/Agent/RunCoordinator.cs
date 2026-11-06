using BaristaNotes.Agent.Contracts;
using BaristaNotes.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BaristaNotes.Api.Agent;

/// <summary>
/// Starts runs in the background, one at a time, and stops them on request.
/// </summary>
public sealed class RunCoordinator(
    IServiceScopeFactory scopes,
    IDbContextFactory<AgentDb> dbs,
    LedgerWriter ledger,
    TimeProvider time,
    IHostApplicationLifetime lifetime,
    ILogger<RunCoordinator> log)
{
    readonly SemaphoreSlim _gate = new(1, 1);
    ActiveRun? _active;

    sealed record ActiveRun(Guid Id, CancellationTokenSource Cancel, Task Task);

    /// <summary>Starts a run, or returns the one already going (<c>Started</c> is false).</summary>
    public async Task<(RunRow Run, bool Started)> StartAsync(string trigger)
    {
        await _gate.WaitAsync();
        try
        {
            await using var db = await dbs.CreateDbContextAsync();
            if (_active is { Task.IsCompleted: false } active)
            {
                return ((await db.Runs.FindAsync(active.Id))!, false);
            }

            var settings = await AgentState.GetSettingsAsync(db);
            var run = new RunRow
            {
                Id = Guid.NewGuid(),
                StartedAt = time.GetUtcNow().UtcDateTime,
                Status = RunStatus.Running,
                Autonomy = settings.Autonomy,
                Trigger = string.IsNullOrWhiteSpace(trigger) ? "app" : trigger.Trim(),
            };
            db.Runs.Add(run);
            await db.SaveChangesAsync();

            var cancel = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
            var task = Task.Run(async () =>
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<AgentRunner>().RunAsync(run.Id, cancel.Token);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Run {RunId} crashed", run.Id);
                }
                finally
                {
                    cancel.Dispose();
                }
            });

            _active = new ActiveRun(run.Id, cancel, task);
            return (run, true);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Asks the run to stop. Returns false when it is not running.</summary>
    public bool Stop(Guid runId)
    {
        var active = _active;
        if (active is null || active.Id != runId || active.Task.IsCompleted)
        {
            return false;
        }

        try
        {
            active.Cancel.Cancel();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        return true;
    }

    /// <summary>Stops the active run and waits for it, for a demo reset.</summary>
    public async Task StopAllAsync()
    {
        var active = _active;
        if (active is null || active.Task.IsCompleted)
        {
            return;
        }

        Stop(active.Id);
        await Task.WhenAny(active.Task, Task.Delay(TimeSpan.FromSeconds(10)));
    }

    /// <summary>Marks runs that were going when the API stopped as failed.</summary>
    public async Task RecoverAsync()
    {
        await using var db = await dbs.CreateDbContextAsync();
        var interrupted = await db.Runs.Where(r => r.Status == RunStatus.Running).ToListAsync();
        foreach (var run in interrupted)
        {
            run.Status = RunStatus.Failed;
            run.EndedAt = time.GetUtcNow().UtcDateTime;
            run.Summary = "The API stopped during this run.";
            await ledger.WriteAsync(run.Id, LedgerKind.Failed, "The API stopped during this run.",
                "Check the ledger above for what already happened.");
        }

        await db.SaveChangesAsync();
    }
}
