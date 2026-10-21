using BaristaNotes.Agent.Contracts;
using BaristaNotes.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BaristaNotes.Api.Agent;

/// <summary>
/// Writes ledger entries one at a time, each in its own save, so the app sees
/// progress while a run is going. Writes are not cancelled by Stop: the entry
/// that says what already happened must still be written.
/// </summary>
public sealed class LedgerWriter(IDbContextFactory<AgentDb> dbs, TimeProvider time, ILogger<LedgerWriter> log)
{
    static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<LedgerRow> WriteAsync(
        Guid? runId,
        LedgerKind kind,
        string title,
        string? detail = null,
        string? roaster = null,
        Guid? proposalId = null,
        string? cartId = null)
    {
        await Gate.WaitAsync(CancellationToken.None);
        try
        {
            await using var db = await dbs.CreateDbContextAsync(CancellationToken.None);
            var last = await db.Ledger.Where(l => l.RunId == runId).MaxAsync(l => (int?)l.Seq, CancellationToken.None) ?? 0;
            var row = new LedgerRow
            {
                RunId = runId,
                Seq = last + 1,
                At = time.GetUtcNow().UtcDateTime,
                Kind = kind,
                Title = title,
                Detail = detail,
                Roaster = roaster,
                ProposalId = proposalId,
                CartId = cartId,
            };
            db.Ledger.Add(row);
            await db.SaveChangesAsync(CancellationToken.None);
            log.LogInformation("Ledger {Kind}: {Title} {Detail}", kind, title, detail);
            return row;
        }
        finally
        {
            Gate.Release();
        }
    }
}

/// <summary>
/// The run a piece of code is working for. HTTP retry callbacks use it to
/// write "retrying" entries to the right ledger.
/// </summary>
public sealed record RunScope(Guid RunId, LedgerWriter Ledger);

public static class RunContext
{
    static readonly AsyncLocal<RunScope?> Current = new();

    public static RunScope? Value => Current.Value;

    public static IDisposable Enter(RunScope scope)
    {
        var previous = Current.Value;
        Current.Value = scope;
        return new Restore(previous);
    }

    sealed class Restore(RunScope? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
