using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BaristaNotes.Agent.Contracts;
using BaristaNotes.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BaristaNotes.Api.Agent;

/// <summary>Reads and writes agent state, and maps rows to the shared contracts.</summary>
public static class AgentState
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public const int CheckInDueAfterDays = 7;

    public static async Task<SettingsRow> GetSettingsAsync(AgentDb db, CancellationToken ct = default)
    {
        var settings = await db.Settings.FindAsync([SettingsRow.SingletonId], ct);
        if (settings is null)
        {
            settings = SettingsRow.Defaults();
            db.Settings.Add(settings);
            await db.SaveChangesAsync(ct);
        }

        return settings;
    }

    public static Snapshot? ReadSnapshot(SnapshotRow? row) =>
        row is null ? null : JsonSerializer.Deserialize(row.Json, AgentJsonContext.Default.Snapshot);

    /// <summary>
    /// Hash of what the model learns from: beans and drinks. A check-in changes
    /// bag state but not this hash, so it does not trigger relearning.
    /// </summary>
    public static string LearningHash(Snapshot snapshot)
    {
        var material = JsonSerializer.Serialize(new { snapshot.Beans, snapshot.Shots }, Json);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    public static SnapshotSummary? Summarize(SnapshotRow? row)
    {
        var snapshot = ReadSnapshot(row);
        if (row is null || snapshot is null)
        {
            return null;
        }

        return new SnapshotSummary(
            new DateTimeOffset(DateTime.SpecifyKind(row.ReceivedAt, DateTimeKind.Utc)),
            snapshot.Beans.Count,
            snapshot.Bags.Count,
            snapshot.Shots.Count,
            snapshot.Shots.Count(s => !s.ForMe));
    }

    public static async Task<(decimal Spent, decimal Left)> BudgetAsync(AgentDb db, SettingsRow settings, DateTime nowUtc, CancellationToken ct = default)
    {
        var orders = await db.Orders.AsNoTracking().ToListAsync(ct);
        var spent = Budget.SpentThisMonth(orders, nowUtc);
        return (spent, Budget.Left(settings.MonthlyBudget, spent));
    }

    public static async Task<CheckInStatus> CheckInStatusAsync(AgentDb db, DateTime nowUtc, CancellationToken ct = default)
    {
        var checkIns = await db.CheckIns.AsNoTracking().ToListAsync(ct);
        var last = checkIns.OrderByDescending(c => c.At).FirstOrDefault();
        var due = last is null || (nowUtc - last.At).TotalDays >= CheckInDueAfterDays;
        var finished = checkIns.SelectMany(c => c.FinishedBagIds).Distinct().Order().ToList();
        return new CheckInStatus(last is null ? null : Utc(last.At), due, finished);
    }

    public static async Task<RunInfo?> RunInfoAsync(AgentDb db, RunRow? run, CancellationToken ct = default)
    {
        if (run is null)
        {
            return null;
        }

        var proposalIds = await db.Proposals.AsNoTracking()
            .Where(p => p.RunId == run.Id)
            .OrderBy(p => p.CreatedAt)
            .Select(p => p.Id)
            .ToListAsync(ct);

        return new RunInfo(
            run.Id,
            Utc(run.StartedAt),
            run.EndedAt is DateTime ended ? Utc(ended) : null,
            run.Status,
            run.Autonomy,
            run.Trigger,
            run.Summary,
            run.RoastersChecked,
            run.Skipped,
            run.Picks,
            proposalIds);
    }

    public static async Task<IReadOnlyList<LedgerEntry>> LedgerAsync(AgentDb db, Guid runId, int after, CancellationToken ct = default)
    {
        var rows = await db.Ledger.AsNoTracking()
            .Where(l => l.RunId == runId && l.Seq > after)
            .OrderBy(l => l.Seq)
            .ToListAsync(ct);

        var proposalIds = rows.Where(r => r.ProposalId is not null).Select(r => r.ProposalId!.Value).Distinct().ToList();
        var openCarts = await db.Proposals.AsNoTracking()
            .Where(p => proposalIds.Contains(p.Id) && p.Status == ProposalStatus.CartReady && p.CartId != null)
            .Select(p => p.CartId!)
            .ToListAsync(ct);

        return rows.Select(r => ToContract(r, r.Kind == LedgerKind.CartReady && r.CartId is not null && openCarts.Contains(r.CartId))).ToList();
    }

    public static LedgerEntry ToContract(LedgerRow row, bool cartOpen) => new(
        row.Id, row.RunId, row.Seq, Utc(row.At), row.Kind, row.Title, row.Detail, row.Roaster, row.ProposalId, row.CartId, cartOpen);

    public static Proposal ToContract(ProposalRow row, Snapshot? snapshot, decimal budgetLeft) => new(
        row.Id,
        row.RunId,
        row.ForBagId,
        row.ForBeanName,
        row.Roaster,
        row.RoasterName,
        row.ProductId,
        row.VariantId,
        row.ProductTitle,
        row.ProductDetails,
        row.Price,
        row.Shipping,
        row.Currency,
        row.Why,
        Evidence(row.EvidenceJson, snapshot),
        row.Unknowns,
        row.IsFavoriteReorder,
        budgetLeft,
        row.Status,
        row.ActionNote,
        row.CartId,
        row.CheckoutUrl,
        row.OrderNumber,
        Utc(row.CreatedAt));

    public static MemoryItem ToContract(MemoryRow row, Snapshot? snapshot) => new(
        row.Id, row.Kind, row.Statement, row.Key, Evidence(row.EvidenceJson, snapshot), Utc(row.LearnedAt), row.Source);

    /// <summary>
    /// Builds evidence from stored citations. The note, rating, and date come
    /// from the shared snapshot, never from the model.
    /// </summary>
    public static IReadOnlyList<Evidence> Evidence(string citationsJson, Snapshot? snapshot)
    {
        var citations = JsonSerializer.Deserialize<List<CitedShot>>(citationsJson, Json) ?? [];
        var shots = snapshot?.Shots.ToDictionary(s => s.Id) ?? [];
        var bags = snapshot?.Bags.ToDictionary(b => b.Id) ?? [];
        var beans = snapshot?.Beans.ToDictionary(b => b.Id) ?? [];

        return citations.Select(c =>
        {
            shots.TryGetValue(c.ShotId, out var shot);
            string? bean = null;
            if (shot is not null && bags.TryGetValue(shot.BagId, out var bag) && beans.TryGetValue(bag.BeanId, out var b))
            {
                bean = b.Name;
            }

            return new Evidence(c.ShotId, c.Point, shot?.TastingNotes, shot?.Rating, shot?.MadeAt, bean);
        }).ToList();
    }

    public static string SerializeCitations(IEnumerable<CitedShot> citations) => JsonSerializer.Serialize(citations.ToList(), Json);

    public static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static string Describe(Autonomy autonomy) => autonomy switch
    {
        Autonomy.Watch => "Watch",
        Autonomy.PrepareCart => "Prepare cart",
        Autonomy.ReorderFavorites => "Reorder favorites",
        _ => autonomy.ToString(),
    };
}
