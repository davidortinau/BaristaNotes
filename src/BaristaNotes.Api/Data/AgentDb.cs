using BaristaNotes.Agent.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BaristaNotes.Api.Data;

/// <summary>
/// The agent's own state. The app's data stays in the app; the API keeps only
/// the latest snapshot the app shared, plus what the agent did.
/// Times are stored as UTC <see cref="DateTime"/> because SQLite cannot sort
/// <see cref="DateTimeOffset"/>.
/// </summary>
public sealed class AgentDb(DbContextOptions<AgentDb> options) : DbContext(options)
{
    public DbSet<SettingsRow> Settings => Set<SettingsRow>();
    public DbSet<SnapshotRow> Snapshots => Set<SnapshotRow>();
    public DbSet<MemoryRow> Memory => Set<MemoryRow>();
    public DbSet<CheckInRow> CheckIns => Set<CheckInRow>();
    public DbSet<RunRow> Runs => Set<RunRow>();
    public DbSet<LedgerRow> Ledger => Set<LedgerRow>();
    public DbSet<ProposalRow> Proposals => Set<ProposalRow>();
    public DbSet<OrderRow> Orders => Set<OrderRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<SettingsRow>().Property(s => s.Id).ValueGeneratedNever();
        model.Entity<SnapshotRow>().Property(s => s.Id).ValueGeneratedNever();
        model.Entity<MemoryRow>().HasIndex(m => m.Key);
        model.Entity<LedgerRow>().HasIndex(l => new { l.RunId, l.Seq });
        model.Entity<ProposalRow>().HasIndex(p => p.RunId);
        model.Entity<OrderRow>().HasKey(o => o.OrderId);

        // Enums as text, so the database is readable during the demo.
        model.Entity<SettingsRow>().Property(s => s.Autonomy).HasConversion<string>();
        model.Entity<MemoryRow>().Property(m => m.Kind).HasConversion<string>();
        model.Entity<RunRow>().Property(r => r.Status).HasConversion<string>();
        model.Entity<RunRow>().Property(r => r.Autonomy).HasConversion<string>();
        model.Entity<LedgerRow>().Property(l => l.Kind).HasConversion<string>();
        model.Entity<ProposalRow>().Property(p => p.Status).HasConversion<string>();
    }
}

public sealed class SettingsRow
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public Autonomy Autonomy { get; set; }
    public decimal MonthlyBudget { get; set; }
    public string Currency { get; set; } = "EUR";
    public List<string> WatchedRoasters { get; set; } = [];
    public int StepDelayMs { get; set; }

    public static SettingsRow Defaults() => new()
    {
        Autonomy = Autonomy.PrepareCart,
        MonthlyBudget = 80m,
        Currency = "EUR",
        WatchedRoasters = ["lanternfish", "wickerline", "ninefold", "tidewell", "copperhaze"],
        StepDelayMs = 900,
    };

    public AgentSettings ToContract() =>
        new(Autonomy, MonthlyBudget, Currency, WatchedRoasters.ToList(), StepDelayMs);
}

/// <summary>The latest snapshot only. <c>LearnedHash</c> is the snapshot the memory was learned from.</summary>
public sealed class SnapshotRow
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public DateTime ReceivedAt { get; set; }
    public string Json { get; set; } = "";
    public string Hash { get; set; } = "";
    public string? LearnedHash { get; set; }
}

public sealed class MemoryRow
{
    public Guid Id { get; set; }
    public MemoryKind Kind { get; set; }
    public string Key { get; set; } = "";
    public string Statement { get; set; } = "";

    /// <summary>JSON list of <see cref="CitedShot"/>. Notes and ratings are read from the snapshot when shown.</summary>
    public string EvidenceJson { get; set; } = "[]";

    public DateTime LearnedAt { get; set; }

    /// <summary>"learned" (by the model) or "you" (for example Never this roaster).</summary>
    public string Source { get; set; } = "learned";

    /// <summary>Set when you remove the item. The key is then never learned again.</summary>
    public DateTime? RemovedAt { get; set; }
}

public sealed class CheckInRow
{
    public long Id { get; set; }
    public DateTime At { get; set; }
    public List<int> FinishedBagIds { get; set; } = [];
    public List<int> StillHaveBagIds { get; set; } = [];
}

public sealed class RunRow
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public RunStatus Status { get; set; }
    public Autonomy Autonomy { get; set; }
    public string Trigger { get; set; } = "app";
    public string? Summary { get; set; }
    public int RoastersChecked { get; set; }
    public int Skipped { get; set; }
    public int Picks { get; set; }
}

public sealed class LedgerRow
{
    public long Id { get; set; }
    public Guid? RunId { get; set; }
    public int Seq { get; set; }
    public DateTime At { get; set; }
    public LedgerKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string? Detail { get; set; }
    public string? Roaster { get; set; }
    public Guid? ProposalId { get; set; }
    public string? CartId { get; set; }
}

public sealed class ProposalRow
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public int ForBagId { get; set; }
    public string ForBeanName { get; set; } = "";
    public string Roaster { get; set; } = "";
    public string RoasterName { get; set; } = "";
    public long ProductId { get; set; }
    public long VariantId { get; set; }
    public string ProductTitle { get; set; } = "";
    public string? ProductDetails { get; set; }
    public decimal Price { get; set; }
    public decimal Shipping { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Why { get; set; } = "";
    public string EvidenceJson { get; set; } = "[]";
    public List<string> Unknowns { get; set; } = [];
    public bool IsFavoriteReorder { get; set; }
    public ProposalStatus Status { get; set; }
    public string? ActionNote { get; set; }
    public string? CartId { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Every order the shop reported, duplicates included. The budget counts all of them.</summary>
public sealed class OrderRow
{
    public string OrderId { get; set; } = "";
    public string OrderNumber { get; set; } = "";
    public string Roaster { get; set; } = "";
    public Guid? ProposalId { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateTime CreatedAt { get; set; }

    /// <summary>"webhook" or "agent": who told the API about the order first.</summary>
    public string ReportedBy { get; set; } = "webhook";
}

/// <summary>A shot the model cited, with its reason. Stored as JSON.</summary>
public sealed record CitedShot(int ShotId, string Point);
