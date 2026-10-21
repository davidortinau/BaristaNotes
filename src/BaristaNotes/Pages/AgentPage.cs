using BaristaNotes.Agent.Contracts;
using BaristaNotes.Services.Agent;
using static BaristaNotes.Components.AgentTiles;

namespace BaristaNotes.Pages;

class AgentPageState
{
    public bool IsLoading { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public bool ConnectionLost { get; set; }
    public AgentStatus? Status { get; set; }
    public RunInfo? Run { get; set; }
    public List<LedgerEntry> Ledger { get; set; } = [];
    public List<Proposal> Proposals { get; set; } = [];
    public bool SkippedExpanded { get; set; }
    public string? StartingStep { get; set; }
    public bool IsStopping { get; set; }
    public HashSet<long> RemovingCartIds { get; set; } = [];
}

/// <summary>
/// The agent's latest run as a timeline. While the run is going the page
/// polls the ledger about once a second, so the steps appear one by one.
/// </summary>
partial class AgentPage : Component<AgentPageState>
{
    const int MaxPollFailures = 5;
    static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    static readonly TimeSpan IdleCheckInterval = TimeSpan.FromSeconds(3);

    [Inject] AgentApiClient _agentApi;
    [Inject] AgentSnapshotBuilder _snapshotBuilder;
    [Inject] IPreferencesStore _preferences;
    [Inject] IFeedbackService _feedbackService;
    [Inject] ILogger<AgentPage> _logger;

    CancellationTokenSource? _watchCts;
    MauiControls.ScrollView? _scrollView;

    protected override void OnMounted()
    {
        base.OnMounted();
        _ = LoadAsync();
    }

    protected override void OnWillUnmount()
    {
        StopWatching();
        base.OnWillUnmount();
    }

    // ============================================================
    // Loading and polling
    // ============================================================

    CancellationToken RestartWatching()
    {
        StopWatching();
        _watchCts = new CancellationTokenSource();
        return _watchCts.Token;
    }

    void StopWatching()
    {
        // Cancel only. Calls still finishing may read the token, so it is not disposed.
        _watchCts?.Cancel();
        _watchCts = null;
    }

    async Task LoadAsync()
    {
        if (!_agentApi.IsConnected)
        {
            SetState(s => s.IsLoading = false);
            return;
        }

        var ct = RestartWatching();
        try
        {
            var status = await _agentApi.GetStatusAsync(ct);
            var run = status.LatestRun;
            IReadOnlyList<LedgerEntry> ledger = [];
            IReadOnlyList<Proposal> proposals = [];
            if (run is not null)
            {
                ledger = await _agentApi.GetLedgerAsync(run.Id, -1, ct);
                proposals = await _agentApi.GetProposalsAsync(run.Id, ct);
            }

            SetState(s =>
            {
                s.Status = status;
                s.Run = run;
                s.Ledger = ledger.OrderBy(entry => entry.Seq).ToList();
                s.Proposals = proposals.ToList();
                s.IsLoading = false;
                s.ErrorMessage = null;
                s.ConnectionLost = false;
            });

            await WatchAsync(run, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load the agent run");
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = AgentText.Unreachable;
                // With a run already on screen, keep it and offer a retry tile.
                s.ConnectionLost = s.Status is not null;
            });
        }
    }

    /// <summary>
    /// While the page is visible: follow a running run step by step, mark a
    /// finished run as seen, and pick up a run started elsewhere (for example
    /// from the Aspire dashboard). Ends when the page disappears.
    /// </summary>
    async Task WatchAsync(RunInfo? run, CancellationToken ct)
    {
        while (true)
        {
            if (run is { Status: RunStatus.Running })
            {
                await PollAsync(run.Id, ct);
                if (State.ConnectionLost)
                {
                    return;
                }
            }
            else if (run is not null)
            {
                MarkSeen(run);
            }

            run = await WaitForNewRunAsync(State.Run?.Id, ct);
            await AttachAsync(run, ct);
        }
    }

    /// <summary>
    /// Waits for a different run. Meanwhile keeps the shown run's ledger
    /// current, so steps added after it ended (an order arriving by webhook,
    /// a removed cart) appear too.
    /// </summary>
    async Task<RunInfo> WaitForNewRunAsync(Guid? currentRunId, CancellationToken ct)
    {
        while (true)
        {
            await Task.Delay(IdleCheckInterval, ct);
            try
            {
                var latest = await _agentApi.GetLatestRunAsync(ct);
                if (latest is not null && latest.Id != currentRunId)
                {
                    return latest;
                }

                if (latest is not null)
                {
                    await CatchUpAsync(latest, ct);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "Checking for agent run changes failed");
            }
        }
    }

    async Task CatchUpAsync(RunInfo run, CancellationToken ct)
    {
        var entries = await _agentApi.GetLedgerAsync(run.Id, LastSeq(), ct);
        if (entries.Count == 0)
        {
            return;
        }

        var proposals = await _agentApi.GetProposalsAsync(run.Id, ct);
        var status = await _agentApi.GetStatusAsync(ct);
        AppendLedger(entries, run);
        SetState(s =>
        {
            s.Proposals = proposals.ToList();
            s.Status = status;
        });
        _ = ScrollToEndAsync();
    }

    /// <summary>Shows a run that started elsewhere.</summary>
    async Task AttachAsync(RunInfo run, CancellationToken ct)
    {
        var ledger = await _agentApi.GetLedgerAsync(run.Id, -1, ct);
        var proposals = await _agentApi.GetProposalsAsync(run.Id, ct);
        var status = await _agentApi.GetStatusAsync(ct);
        SetState(s =>
        {
            s.Run = run;
            s.Ledger = ledger.OrderBy(entry => entry.Seq).ToList();
            s.Proposals = proposals.ToList();
            s.Status = status;
            s.SkippedExpanded = false;
        });
        _ = ScrollToEndAsync();
    }

    async Task PollAsync(Guid runId, CancellationToken ct)
    {
        var failures = 0;
        while (true)
        {
            await Task.Delay(PollInterval, ct);
            try
            {
                var entries = await _agentApi.GetLedgerAsync(runId, LastSeq(), ct);
                var run = await _agentApi.GetRunAsync(runId, ct);
                failures = 0;

                if (State.Run?.Id != runId)
                {
                    return;
                }

                AppendLedger(entries, run);
                if (entries.Any(entry => entry.ProposalId is not null))
                {
                    var proposals = await _agentApi.GetProposalsAsync(runId, ct);
                    SetState(s => s.Proposals = proposals.ToList());
                }

                if (entries.Count > 0)
                {
                    _ = ScrollToEndAsync();
                }

                if (run.Status != RunStatus.Running)
                {
                    await FinishAsync(runId, ct);
                    return;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Polling agent run {RunId} failed", runId);
                if (++failures >= MaxPollFailures)
                {
                    SetState(s => s.ConnectionLost = true);
                    return;
                }
            }
        }
    }

    /// <summary>Last catch-up after a run ends: remaining steps, picks, budget.</summary>
    async Task FinishAsync(Guid runId, CancellationToken ct)
    {
        var entries = await _agentApi.GetLedgerAsync(runId, LastSeq(), ct);
        var run = await _agentApi.GetRunAsync(runId, ct);
        var proposals = await _agentApi.GetProposalsAsync(runId, ct);
        var status = await _agentApi.GetStatusAsync(ct);

        AppendLedger(entries, run);
        SetState(s =>
        {
            s.Proposals = proposals.ToList();
            s.Status = status;
        });
        MarkSeen(run);
    }

    /// <summary>Reloads the current run after an action changed it (remove cart).</summary>
    async Task RefreshRunAsync()
    {
        if (State.Run is not { } current)
        {
            return;
        }

        var run = await _agentApi.GetRunAsync(current.Id);
        var ledger = await _agentApi.GetLedgerAsync(current.Id, -1);
        var proposals = await _agentApi.GetProposalsAsync(current.Id);
        var status = await _agentApi.GetStatusAsync();
        SetState(s =>
        {
            s.Run = run;
            s.Ledger = ledger.OrderBy(entry => entry.Seq).ToList();
            s.Proposals = proposals.ToList();
            s.Status = status;
        });
    }

    int LastSeq() => State.Ledger.Count == 0 ? -1 : State.Ledger.Max(entry => entry.Seq);

    void AppendLedger(IReadOnlyList<LedgerEntry> entries, RunInfo run)
    {
        SetState(s =>
        {
            var known = s.Ledger.Select(entry => entry.Seq).ToHashSet();
            s.Ledger = s.Ledger
                .Concat(entries.Where(entry => known.Add(entry.Seq)))
                .OrderBy(entry => entry.Seq)
                .ToList();
            s.Run = run;
        });
    }

    void MarkSeen(RunInfo run)
    {
        if (run.Status != RunStatus.Running)
        {
            _preferences.Set(AgentHomeCard.LastSeenRunIdKey, run.Id.ToString());
        }
    }

    async Task ScrollToEndAsync()
    {
        if (_scrollView is null)
        {
            return;
        }

        try
        {
            // Let the new tiles lay out first.
            await Task.Delay(100);
            await _scrollView.ScrollToAsync(0, _scrollView.ContentSize.Height, animated: true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not scroll the ledger");
        }
    }

    // ============================================================
    // Actions
    // ============================================================

    async Task RunNowAsync()
    {
        if (State.StartingStep is not null || State.Run?.Status == RunStatus.Running)
        {
            return;
        }

        var ct = RestartWatching();
        SetState(s =>
        {
            s.StartingStep = "SHARING YOUR NOTES";
            s.ConnectionLost = false;
        });

        StartRunResult result;
        try
        {
            var snapshot = await _snapshotBuilder.BuildAsync(ct);
            await _agentApi.SendSnapshotAsync(snapshot, ct);
            SetState(s => s.StartingStep = "STARTING");

            result = await _agentApi.StartRunAsync("app", ct);
            var ledger = await _agentApi.GetLedgerAsync(result.Run.Id, -1, ct);
            SetState(s =>
            {
                s.Run = result.Run;
                s.Ledger = ledger.OrderBy(entry => entry.Seq).ToList();
                s.Proposals = [];
                s.SkippedExpanded = false;
                s.StartingStep = null;
                s.ErrorMessage = null;
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            SetState(s => s.StartingStep = null);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not start an agent run");
            SetState(s => s.StartingStep = null);
            await _feedbackService.ShowErrorAsync("Could not start the agent.", "Check that the API is running, then try again.");
            return;
        }

        if (result.AlreadyRunning)
        {
            _ = _feedbackService.ShowInfoAsync("A run is already going. Showing it.");
        }

        try
        {
            if (result.Run.Status != RunStatus.Running)
            {
                await FinishAsync(result.Run.Id, ct);
            }

            await WatchAsync(result.Run, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lost the agent run");
            SetState(s => s.ConnectionLost = true);
        }
    }

    async Task StopAsync()
    {
        if (State.Run is not { Status: RunStatus.Running } run || State.IsStopping)
        {
            return;
        }

        SetState(s => s.IsStopping = true);
        try
        {
            var stopped = await _agentApi.StopRunAsync(run.Id);
            SetState(s =>
            {
                s.Run = stopped;
                s.IsStopping = false;
            });
            _ = _feedbackService.ShowInfoAsync("Stopped. Anything already done is listed in the ledger.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not stop agent run {RunId}", run.Id);
            SetState(s => s.IsStopping = false);
            await _feedbackService.ShowErrorAsync("Could not stop the run.", "Try again.");
        }
    }

    async Task RemoveCartAsync(LedgerEntry entry)
    {
        if (State.RemovingCartIds.Contains(entry.Id))
        {
            return;
        }

        SetState(s => s.RemovingCartIds = [.. s.RemovingCartIds, entry.Id]);
        try
        {
            await _agentApi.RemoveCartAsync(entry.Id);
            await RefreshRunAsync();
            await _feedbackService.ShowSuccessAsync("Cart removed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove the cart for ledger entry {EntryId}", entry.Id);
            await _feedbackService.ShowErrorAsync("Could not remove the cart.", "Try again.");
        }
        finally
        {
            SetState(s => s.RemovingCartIds = s.RemovingCartIds.Where(id => id != entry.Id).ToHashSet());
        }
    }

    async Task OpenProposalAsync(Proposal proposal) =>
        await MauiControls.Shell.Current.GoToAsync<ProposalPageProps>(
            "proposal",
            props => props.ProposalId = proposal.Id);

    // ============================================================
    // Rendering
    // ============================================================

    public override VisualNode Render()
    {
        return PageFrame("Agent", Header(), Body())
            .OnAppearing(() => _ = LoadAsync())
            .OnDisappearing(StopWatching);
    }

    VisualNode Header()
    {
        if (!_agentApi.IsConnected)
        {
            return HeaderTile("AGENT", "Not connected");
        }

        var title = State.IsLoading && State.Status is null
            ? "Loading"
            : AgentText.RunTitle(State.Run, DateTimeOffset.Now);
        var subtitle = State.Status is null ? null : AgentText.BudgetLine(State.Status);
        return HeaderTile("AGENT", title, subtitle);
    }

    VisualNode Body()
    {
        if (!_agentApi.IsConnected)
        {
            return MessageBody("NOT CONNECTED", AgentText.NotConnected);
        }

        if (State.IsLoading && State.Status is null)
        {
            return LoadingBody();
        }

        if (State.ErrorMessage is not null && State.Status is null)
        {
            return MessageBody("AGENT UNAVAILABLE", State.ErrorMessage, "Retry", () =>
            {
                SetState(s => s.IsLoading = true);
                _ = LoadAsync();
            });
        }

        var tiles = new List<VisualNode?> { ActionButtons() };

        if (State.ConnectionLost)
        {
            tiles.Add(InfoTile("LOST CONTACT", "Tap to try again", () => _ = LoadAsync(),
                MaterialSymbolsFont.Warning, ErrorColor()));
        }

        if (State.Run is not { } run)
        {
            tiles.Add(TextTile("No runs yet. Run now to check the roasters you watch.", secondary: true));
            return ScrollBody(tiles, scrollView => _scrollView = scrollView);
        }

        tiles.Add(SectionLabel($"LEDGER{AgentText.Separator}STARTED {AgentText.Clock(run.StartedAt)}"));
        tiles.AddRange(LedgerTiles());

        if (run.Status == RunStatus.Running && State.Ledger.Count == 0)
        {
            tiles.Add(TextTile("Waiting for the first step…", secondary: true));
        }

        if (State.Proposals.Count > 0)
        {
            tiles.Add(SectionLabel(State.Proposals.Count == 1 ? "PICK" : "PICKS"));
            tiles.AddRange(State.Proposals.Select(ProposalTile));
        }

        return ScrollBody(tiles, scrollView => _scrollView = scrollView);
    }

    VisualNode ActionButtons()
    {
        if (State.StartingStep is { } step)
        {
            return ActionRow(
                ActionTile(step, MaterialSymbolsFont.Play_arrow, () => { }, primary: true, busy: true),
                ActionTile("MEMORY", MaterialSymbolsFont.Psychology,
                    async () => await MauiControls.Shell.Current.GoToAsync("memory")));
        }

        if (State.Run?.Status == RunStatus.Running)
        {
            return ActionRow(
                ActionTile("RUNNING", MaterialSymbolsFont.Play_arrow, () => { }, primary: true, busy: true),
                ActionTile("STOP", MaterialSymbolsFont.Stop_circle, () => _ = StopAsync(), busy: State.IsStopping,
                    accessibilityName: "Stop the run"));
        }

        return ActionRow(
            ActionTile("RUN NOW", MaterialSymbolsFont.Play_arrow, () => _ = RunNowAsync(), primary: true,
                accessibilityName: "Run the agent now"),
            ActionTile("MEMORY", MaterialSymbolsFont.Psychology,
                async () => await MauiControls.Shell.Current.GoToAsync("memory"),
                accessibilityName: "What the agent learned"));
    }

    // ------------------------------------------------------------
    // Ledger
    // ------------------------------------------------------------

    /// <summary>
    /// Steps in order. Skipped steps collapse into one tile where the latest one
    /// happened, so the roasters checked come first and the timeline stays in order.
    /// </summary>
    IEnumerable<VisualNode> LedgerTiles()
    {
        var skipped = State.Ledger.Where(entry => entry.Kind == LedgerKind.Skipped).ToList();
        var lastSkippedSeq = skipped.Count == 0 ? -1 : skipped[^1].Seq;

        foreach (var entry in State.Ledger)
        {
            if (entry.Kind != LedgerKind.Skipped)
            {
                yield return LedgerTile(entry);
                continue;
            }

            if (entry.Seq != lastSkippedSeq)
            {
                continue;
            }

            yield return SkippedGroupTile(skipped);
            if (State.SkippedExpanded)
            {
                foreach (var skip in skipped)
                {
                    yield return SkippedReasonTile(skip);
                }
            }
        }
    }

    VisualNode LedgerTile(LedgerEntry entry)
    {
        var (glyph, color) = LedgerGlyph(entry.Kind);

        return Border(
            Grid(rows: "Auto,Auto,Auto", columns: "Auto,Auto,*",
                ClockLabel(entry.At).GridRow(0).GridColumn(0),
                Glyph(glyph, color, 20).VStart().GridRow(0).GridRowSpan(3).GridColumn(1),
                Label(entry.Title)
                    .FontSize(16)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .LineBreakMode(LineBreakMode.WordWrap)
                    .GridRow(0).GridColumn(2),
                string.IsNullOrWhiteSpace(entry.Detail)
                    ? null
                    : Label(entry.Detail)
                        .FontSize(13)
                        .TextColor(TextSecondary())
                        .LineBreakMode(LineBreakMode.WordWrap)
                        .GridRow(1).GridColumn(2),
                entry.CartOpen
                    ? RemoveCartButton(entry).GridRow(2).GridColumn(2)
                    : null
            )
            .ColumnSpacing(12)
            .RowSpacing(4)
            .Padding(16, 12, 16, 12)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle());
    }

    VisualNode RemoveCartButton(LedgerEntry entry)
    {
        if (State.RemovingCartIds.Contains(entry.Id))
        {
            return ActivityIndicator()
                .IsRunning(true)
                .Color(TextPrimary())
                .HStart()
                .Margin(0, 6, 0, 0);
        }

        return Button("REMOVE CART")
            .FontSize(11)
            .CharacterSpacing(1)
            .FontAttributes(MauiControls.FontAttributes.Bold)
            .TextColor(TextPrimary())
            .BackgroundColor(Colors.Transparent)
            .BorderColor(TextPrimary())
            .BorderWidth(1)
            .CornerRadius(0)
            .Padding(12, 6)
            .MinimumHeightRequest(44)
            .HStart()
            .Margin(0, 6, 0, 0)
            .Set(MauiControls.SemanticProperties.DescriptionProperty, $"Remove cart. {entry.Title}")
            .OnClicked(() => _ = RemoveCartAsync(entry));
    }

    VisualNode SkippedGroupTile(IReadOnlyList<LedgerEntry> skipped)
    {
        var expanded = State.SkippedExpanded;
        var label = $"SKIPPED {skipped.Count}";
        var hint = expanded ? "Tap to hide reasons" : "Tap for reasons";

        var visual = Border(
            Grid(rows: "Auto,Auto", columns: "Auto,Auto,*,Auto",
                ClockLabel(skipped[0].At).GridRow(0).GridColumn(0),
                Glyph(MaterialSymbolsFont.Skip_next, TextSecondary(), 20).VStart().GridRow(0).GridRowSpan(2).GridColumn(1),
                MicroLabel(label).GridRow(0).GridColumn(2).Margin(0, 3, 0, 0),
                Label(hint)
                    .FontSize(16)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .GridRow(1).GridColumn(2),
                Glyph(expanded ? MaterialSymbolsFont.Expand_less : MaterialSymbolsFont.Expand_more, TextPrimary())
                    .VCenter().GridRow(0).GridRowSpan(2).GridColumn(3)
            )
            .ColumnSpacing(12)
            .RowSpacing(2)
            .Padding(16, 12, 16, 12)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .Set(MauiControls.AutomationProperties.ExcludedWithChildrenProperty, true);

        return Grid(
            visual,
            TapTarget($"{skipped.Count} skipped. {hint}.", () => SetState(s => s.SkippedExpanded = !s.SkippedExpanded)));
    }

    VisualNode SkippedReasonTile(LedgerEntry entry)
    {
        return Border(
            Grid(rows: "Auto,Auto", columns: "Auto,*",
                ClockLabel(entry.At).GridRow(0).GridColumn(0),
                Label(entry.Title)
                    .FontSize(14)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .LineBreakMode(LineBreakMode.WordWrap)
                    .GridRow(0).GridColumn(1),
                Label(string.IsNullOrWhiteSpace(entry.Detail) ? "No reason recorded" : entry.Detail)
                    .FontSize(13)
                    .TextColor(TextSecondary())
                    .LineBreakMode(LineBreakMode.WordWrap)
                    .GridRow(1).GridColumn(1)
            )
            .ColumnSpacing(12)
            .RowSpacing(2)
            .Padding(48, 10, 16, 10)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle());
    }

    static MauiReactor.Label ClockLabel(DateTimeOffset at) =>
        Label(AgentText.Clock(at))
            .FontSize(12)
            .TextColor(TextSecondary())
            .VStart()
            .Margin(0, 2, 0, 0);

    static (string Glyph, Color Color) LedgerGlyph(LedgerKind kind) => kind switch
    {
        LedgerKind.Started => (MaterialSymbolsFont.Play_arrow, TextPrimary()),
        LedgerKind.Learned => (MaterialSymbolsFont.Psychology, TextPrimary()),
        LedgerKind.Checked => (MaterialSymbolsFont.Storefront, TextPrimary()),
        LedgerKind.Skipped => (MaterialSymbolsFont.Skip_next, TextSecondary()),
        LedgerKind.Ranking => (MaterialSymbolsFont.Leaderboard, TextPrimary()),
        LedgerKind.Proposed => (MaterialSymbolsFont.Recommend, AccentColor()),
        LedgerKind.EvidenceChecked => (MaterialSymbolsFont.Fact_check, TextPrimary()),
        LedgerKind.CartCreating => (MaterialSymbolsFont.Add_shopping_cart, TextPrimary()),
        LedgerKind.CartReady => (MaterialSymbolsFont.Shopping_cart, AccentColor()),
        LedgerKind.CartRemoved => (MaterialSymbolsFont.Remove_shopping_cart, TextSecondary()),
        LedgerKind.Ordering => (MaterialSymbolsFont.Shopping_cart_checkout, TextPrimary()),
        LedgerKind.Retrying => (MaterialSymbolsFont.Replay, AppColors.Warning),
        LedgerKind.Ordered => (MaterialSymbolsFont.Receipt_long, AccentColor()),
        LedgerKind.OrderReceived => (MaterialSymbolsFont.Inventory_2, AccentColor()),
        LedgerKind.DuplicateOrder => (MaterialSymbolsFont.Warning, ErrorColor()),
        LedgerKind.Notice => (MaterialSymbolsFont.Info, TextPrimary()),
        LedgerKind.NothingToDo => (MaterialSymbolsFont.Task_alt, TextPrimary()),
        LedgerKind.NeedsCheckIn => (MaterialSymbolsFont.Checklist, AppColors.Warning),
        LedgerKind.Stopped => (MaterialSymbolsFont.Stop_circle, TextPrimary()),
        LedgerKind.Completed => (MaterialSymbolsFont.Done_all, TextPrimary()),
        LedgerKind.Failed => (MaterialSymbolsFont.Error, ErrorColor()),
        _ => (MaterialSymbolsFont.Info, TextPrimary()),
    };

    // ------------------------------------------------------------
    // Picks
    // ------------------------------------------------------------

    VisualNode ProposalTile(Proposal proposal)
    {
        var label = $"{AgentText.ProposalKind(proposal)}{AgentText.Separator}{proposal.RoasterName.ToUpperInvariant()}";
        var status = $"{AgentText.Money(proposal.Price, proposal.Currency)}{AgentText.Separator}"
            + AgentText.ProposalStatusText(proposal).ToUpperInvariant();

        return InfoTile(
            label,
            proposal.ProductTitle,
            () => _ = OpenProposalAsync(proposal),
            proposal.IsFavoriteReorder ? MaterialSymbolsFont.Quick_reorder : MaterialSymbolsFont.Recommend,
            AccentColor(),
            status);
    }
}
