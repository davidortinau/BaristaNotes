using System.Text.Json;
using System.Text.Json.Serialization;

namespace BaristaNotes.Agent.Contracts;

// Records shared by the API and the app. No logic here, so the two sides
// cannot drift apart.

// ---------------------------------------------------------------------------
// Settings
// ---------------------------------------------------------------------------

/// <summary>How much the agent may do on its own.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Autonomy>))]
public enum Autonomy
{
    /// <summary>Find a pick and tell you. Do nothing else.</summary>
    Watch,

    /// <summary>Find a pick and prepare a cart. You place the order.</summary>
    PrepareCart,

    /// <summary>Reorder a finished favorite within the monthly budget. Anything else gets a cart.</summary>
    ReorderFavorites,
}

public sealed record AgentSettings(
    Autonomy Autonomy,
    decimal MonthlyBudget,
    string Currency,
    IReadOnlyList<string> WatchedRoasters,
    int StepDelayMs);

/// <summary>A roaster the shop sells from. <c>Handle</c> is the id in shop URLs.</summary>
public sealed record Roaster(
    string Handle,
    string Name,
    string City,
    decimal Shipping,
    string Currency,
    bool Watched);

// ---------------------------------------------------------------------------
// Snapshot: the data the app shares with the agent
// ---------------------------------------------------------------------------

/// <summary>
/// What the app sends. No photos, people's names, equipment, or recipes.
/// </summary>
public sealed record Snapshot(
    DateTimeOffset CreatedAt,
    IReadOnlyList<SnapshotBean> Beans,
    IReadOnlyList<SnapshotBag> Bags,
    IReadOnlyList<SnapshotShot> Shots);

public sealed record SnapshotBean(
    int Id,
    string Name,
    string? Roaster,
    string? Origin,
    string? Notes);

public sealed record SnapshotBag(
    int Id,
    int BeanId,
    DateTime RoastDate,
    bool Finished);

/// <summary>
/// One drink. <c>ForMe</c> is false when the drink was made for someone else.
/// The agent does not learn from those.
/// </summary>
public sealed record SnapshotShot(
    int Id,
    int BagId,
    DateTime MadeAt,
    string? DrinkType,
    int? Rating,
    string? TastingNotes,
    bool ForMe);

public sealed record SnapshotSummary(
    DateTimeOffset ReceivedAt,
    int Beans,
    int Bags,
    int Shots,
    int GuestShots);

// ---------------------------------------------------------------------------
// Check-in: the app cannot see what is in the cupboard, so it asks
// ---------------------------------------------------------------------------

public sealed record CheckIn(
    IReadOnlyList<int> FinishedBagIds,
    IReadOnlyList<int> StillHaveBagIds);

public sealed record CheckInStatus(
    DateTimeOffset? LastCheckInAt,
    bool Due,
    IReadOnlyList<int> FinishedBagIds);

// ---------------------------------------------------------------------------
// Runs and the ledger
// ---------------------------------------------------------------------------

[JsonConverter(typeof(JsonStringEnumConverter<RunStatus>))]
public enum RunStatus
{
    Running,
    Completed,
    Stopped,
    Failed,
}

public sealed record StartRun(string Trigger);

public sealed record RunInfo(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    RunStatus Status,
    Autonomy Autonomy,
    string Trigger,
    string? Summary,
    int RoastersChecked,
    int Skipped,
    int Picks,
    IReadOnlyList<Guid> ProposalIds);

[JsonConverter(typeof(JsonStringEnumConverter<LedgerKind>))]
public enum LedgerKind
{
    Started,
    Learned,
    Checked,
    Skipped,
    Ranking,
    Proposed,
    EvidenceChecked,
    CartCreating,
    CartReady,
    CartRemoved,
    Ordering,
    Retrying,
    Ordered,
    OrderReceived,
    DuplicateOrder,
    Notice,
    NothingToDo,
    NeedsCheckIn,
    Stopped,
    Completed,
    Failed,
}

/// <summary>
/// One step of a run. <c>CartOpen</c> is true when this step created a cart
/// that is still open, so the app can offer to remove it.
/// </summary>
public sealed record LedgerEntry(
    long Id,
    Guid? RunId,
    int Seq,
    DateTimeOffset At,
    LedgerKind Kind,
    string Title,
    string? Detail,
    string? Roaster,
    Guid? ProposalId,
    string? CartId,
    bool CartOpen);

// ---------------------------------------------------------------------------
// Proposals
// ---------------------------------------------------------------------------

[JsonConverter(typeof(JsonStringEnumConverter<ProposalStatus>))]
public enum ProposalStatus
{
    Proposed,
    CartReady,
    Ordered,
    Dismissed,
    CartRemoved,
}

/// <summary>
/// A drink the agent cites. <c>Point</c> is the agent's reason. <c>Note</c>,
/// <c>Rating</c>, and <c>MadeAt</c> come from your data, not from the model.
/// </summary>
public sealed record Evidence(
    int ShotId,
    string Point,
    string? Note,
    int? Rating,
    DateTime? MadeAt,
    string? BeanName);

public sealed record Proposal(
    Guid Id,
    Guid RunId,
    int ForBagId,
    string ForBeanName,
    string Roaster,
    string RoasterName,
    long ProductId,
    long VariantId,
    string ProductTitle,
    string? ProductDetails,
    decimal Price,
    decimal Shipping,
    string Currency,
    string Why,
    IReadOnlyList<Evidence> Evidence,
    IReadOnlyList<string> Unknowns,
    bool IsFavoriteReorder,
    decimal BudgetLeft,
    ProposalStatus Status,
    string? ActionNote,
    string? CartId,
    string? CheckoutUrl,
    string? OrderNumber,
    DateTimeOffset CreatedAt);

// ---------------------------------------------------------------------------
// Memory: what the agent learned, with evidence, and what you removed
// ---------------------------------------------------------------------------

[JsonConverter(typeof(JsonStringEnumConverter<MemoryKind>))]
public enum MemoryKind
{
    Likes,
    Dislikes,
    NeverRoaster,
}

/// <summary>
/// <c>Key</c> identifies the preference, for example <c>likes:process:natural</c>.
/// A removed key is never learned again. <c>Source</c> is "learned" or "you".
/// </summary>
public sealed record MemoryItem(
    Guid Id,
    MemoryKind Kind,
    string Statement,
    string Key,
    IReadOnlyList<Evidence> Evidence,
    DateTimeOffset LearnedAt,
    string Source);

public sealed record MemoryView(
    IReadOnlyList<MemoryItem> Items,
    IReadOnlyList<MemoryItem> Removed,
    SnapshotSummary? LastSnapshot);

// ---------------------------------------------------------------------------
// Status for the app's home card and settings
// ---------------------------------------------------------------------------

public sealed record AgentStatus(
    AgentSettings Settings,
    decimal SpentThisMonth,
    decimal BudgetLeft,
    RunInfo? LatestRun,
    CheckInStatus CheckIn,
    SnapshotSummary? LastSnapshot);

// ---------------------------------------------------------------------------
// Source-generated JSON, so the app works when trimmed
// ---------------------------------------------------------------------------

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(AgentSettings))]
[JsonSerializable(typeof(AgentStatus))]
[JsonSerializable(typeof(Snapshot))]
[JsonSerializable(typeof(SnapshotSummary))]
[JsonSerializable(typeof(CheckIn))]
[JsonSerializable(typeof(CheckInStatus))]
[JsonSerializable(typeof(StartRun))]
[JsonSerializable(typeof(RunInfo))]
[JsonSerializable(typeof(LedgerEntry))]
[JsonSerializable(typeof(IReadOnlyList<LedgerEntry>))]
[JsonSerializable(typeof(Proposal))]
[JsonSerializable(typeof(IReadOnlyList<Proposal>))]
[JsonSerializable(typeof(MemoryItem))]
[JsonSerializable(typeof(MemoryView))]
[JsonSerializable(typeof(Roaster))]
[JsonSerializable(typeof(IReadOnlyList<Roaster>))]
public partial class AgentJsonContext : JsonSerializerContext;
