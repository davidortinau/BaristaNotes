using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using BaristaNotes.Agent.Contracts;

namespace BaristaNotes.Services.Agent;

/// <summary>The run that is going after a start request, and whether it was already going.</summary>
public sealed record StartRunResult(RunInfo Run, bool AlreadyRunning);

/// <summary>
/// Typed client for the BaristaNotes agent API. The base address comes from
/// <see cref="AgentApiAddress.Resolve"/>. When the app runs without the Aspire
/// AppHost there is no address, <see cref="IsConnected"/> is false, and pages
/// show <see cref="AgentText.NotConnected"/> instead of calling.
/// All JSON goes through the source-generated <see cref="AgentJsonContext"/>.
/// </summary>
public sealed class AgentApiClient(HttpClient http)
{
    static AgentJsonContext Json => AgentJsonContext.Default;

    public bool IsConnected => http.BaseAddress is not null;

    // ---------------------------------------------------------------- status, settings

    public Task<AgentStatus> GetStatusAsync(CancellationToken ct = default) =>
        GetAsync("agent/status", Json.AgentStatus, ct);

    public Task<AgentSettings> GetSettingsAsync(CancellationToken ct = default) =>
        GetAsync("agent/settings", Json.AgentSettings, ct);

    public async Task<AgentSettings> SaveSettingsAsync(AgentSettings settings, CancellationToken ct = default)
    {
        EnsureConnected();
        using var response = await http.PutAsJsonAsync("agent/settings", settings, Json.AgentSettings, ct);
        return await ReadAsync(response, Json.AgentSettings, ct);
    }

    public Task<IReadOnlyList<Roaster>> GetRoastersAsync(CancellationToken ct = default) =>
        GetAsync("roasters", Json.IReadOnlyListRoaster, ct);

    // ---------------------------------------------------------------- shared data, memory

    public Task<SnapshotSummary> SendSnapshotAsync(Snapshot snapshot, CancellationToken ct = default) =>
        PostAsync("snapshot", snapshot, Json.Snapshot, Json.SnapshotSummary, ct);

    public Task DeleteSnapshotAsync(CancellationToken ct = default) =>
        DeleteAsync("snapshot", ct);

    public Task<MemoryView> GetMemoryAsync(CancellationToken ct = default) =>
        GetAsync("memory", Json.MemoryView, ct);

    public Task ForgetMemoryAsync(Guid id, CancellationToken ct = default) =>
        DeleteAsync($"memory/{id}", ct);

    // ---------------------------------------------------------------- check-ins

    public Task<CheckInStatus> SendCheckInAsync(CheckIn checkIn, CancellationToken ct = default) =>
        PostAsync("checkins", checkIn, Json.CheckIn, Json.CheckInStatus, ct);

    public Task<CheckInStatus> GetLatestCheckInAsync(CancellationToken ct = default) =>
        GetAsync("checkins/latest", Json.CheckInStatus, ct);

    // ---------------------------------------------------------------- runs and ledger

    /// <summary>Starts a run. A 409 means one is already going; the result attaches to it.</summary>
    public async Task<StartRunResult> StartRunAsync(string trigger, CancellationToken ct = default)
    {
        EnsureConnected();
        using var response = await http.PostAsJsonAsync("runs", new StartRun(trigger), Json.StartRun, ct);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var running = await response.Content.ReadFromJsonAsync(Json.RunInfo, ct)
                ?? throw new HttpRequestException("The agent API returned an empty run.");
            return new StartRunResult(running, AlreadyRunning: true);
        }

        return new StartRunResult(await ReadAsync(response, Json.RunInfo, ct), AlreadyRunning: false);
    }

    /// <summary>The latest run, or null when there has never been one (204).</summary>
    public async Task<RunInfo?> GetLatestRunAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        using var response = await http.GetAsync("runs/latest", ct);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        return await ReadAsync(response, Json.RunInfo, ct);
    }

    public Task<RunInfo> GetRunAsync(Guid runId, CancellationToken ct = default) =>
        GetAsync($"runs/{runId}", Json.RunInfo, ct);

    public Task<RunInfo> StopRunAsync(Guid runId, CancellationToken ct = default) =>
        PostEmptyAsync($"runs/{runId}/stop", Json.RunInfo, ct);

    /// <summary>Ledger entries with <c>Seq</c> greater than <paramref name="afterSeq"/>. Pass -1 for all.</summary>
    public Task<IReadOnlyList<LedgerEntry>> GetLedgerAsync(Guid runId, int afterSeq, CancellationToken ct = default) =>
        GetAsync(
            $"runs/{runId}/ledger?after={afterSeq.ToString(CultureInfo.InvariantCulture)}",
            Json.IReadOnlyListLedgerEntry,
            ct);

    public Task<LedgerEntry> RemoveCartAsync(long ledgerEntryId, CancellationToken ct = default) =>
        PostEmptyAsync($"ledger/{ledgerEntryId.ToString(CultureInfo.InvariantCulture)}/remove-cart", Json.LedgerEntry, ct);

    // ---------------------------------------------------------------- proposals

    public Task<IReadOnlyList<Proposal>> GetProposalsAsync(Guid runId, CancellationToken ct = default) =>
        GetAsync($"proposals?runId={runId}", Json.IReadOnlyListProposal, ct);

    public Task<Proposal> GetProposalAsync(Guid proposalId, CancellationToken ct = default) =>
        GetAsync($"proposals/{proposalId}", Json.Proposal, ct);

    /// <summary>Creates the shop cart if needed. The result has the checkout URL.</summary>
    public Task<Proposal> PrepareCartAsync(Guid proposalId, CancellationToken ct = default) =>
        PostEmptyAsync($"proposals/{proposalId}/cart", Json.Proposal, ct);

    public Task<Proposal> DismissAsync(Guid proposalId, CancellationToken ct = default) =>
        PostEmptyAsync($"proposals/{proposalId}/dismiss", Json.Proposal, ct);

    public Task<Proposal> NeverThisRoasterAsync(Guid proposalId, CancellationToken ct = default) =>
        PostEmptyAsync($"proposals/{proposalId}/never-roaster", Json.Proposal, ct);

    // ---------------------------------------------------------------- demo

    /// <summary>Clears runs, proposals, orders, check-ins, and memory on the API.</summary>
    public async Task ResetDemoAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        using var response = await http.PostAsync("demo/reset", content: null, ct);
        response.EnsureSuccessStatusCode();
    }

    // ---------------------------------------------------------------- helpers

    void EnsureConnected()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException(AgentText.NotConnected);
        }
    }

    async Task<T> GetAsync<T>(string path, JsonTypeInfo<T> type, CancellationToken ct)
    {
        EnsureConnected();
        using var response = await http.GetAsync(path, ct);
        return await ReadAsync(response, type, ct);
    }

    async Task<TResult> PostAsync<TBody, TResult>(
        string path,
        TBody body,
        JsonTypeInfo<TBody> bodyType,
        JsonTypeInfo<TResult> resultType,
        CancellationToken ct)
    {
        EnsureConnected();
        using var response = await http.PostAsJsonAsync(path, body, bodyType, ct);
        return await ReadAsync(response, resultType, ct);
    }

    async Task<T> PostEmptyAsync<T>(string path, JsonTypeInfo<T> type, CancellationToken ct)
    {
        EnsureConnected();
        using var response = await http.PostAsync(path, content: null, ct);
        return await ReadAsync(response, type, ct);
    }

    async Task DeleteAsync(string path, CancellationToken ct)
    {
        EnsureConnected();
        using var response = await http.DeleteAsync(path, ct);
        response.EnsureSuccessStatusCode();
    }

    static async Task<T> ReadAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> type, CancellationToken ct)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(type, ct)
            ?? throw new HttpRequestException($"The agent API returned an empty {typeof(T).Name}.");
    }
}
