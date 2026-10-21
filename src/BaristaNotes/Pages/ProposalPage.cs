using BaristaNotes.Agent.Contracts;
using BaristaNotes.Services.Agent;
using static BaristaNotes.Components.AgentTiles;

namespace BaristaNotes.Pages;

class ProposalPageProps
{
    public Guid ProposalId { get; set; }
}

class ProposalPageState
{
    public bool IsLoading { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public Proposal? Proposal { get; set; }
    public string? BusyAction { get; set; }
}

/// <summary>
/// One pick: what it is, why, the evidence from your own notes, what the
/// agent does not know, and the budget. Buy opens the prepared cart in the
/// system browser. The order arrives later by webhook, so the page checks
/// again while a cart is open.
/// </summary>
partial class ProposalPage : Component<ProposalPageState, ProposalPageProps>
{
    const string Buy = "BUY";
    const string NotNow = "NOT NOW";
    const string Never = "NEVER THIS ROASTER";

    static readonly TimeSpan OrderCheckInterval = TimeSpan.FromSeconds(2);
    static readonly TimeSpan OrderCheckLimit = TimeSpan.FromMinutes(15);

    [Inject] AgentApiClient _agentApi;
    [Inject] IFeedbackService _feedbackService;
    [Inject] ILogger<ProposalPage> _logger;

    CancellationTokenSource? _orderWatchCts;

    protected override void OnMounted()
    {
        base.OnMounted();
        _ = LoadAsync();
    }

    protected override void OnWillUnmount()
    {
        StopOrderWatch();
        base.OnWillUnmount();
    }

    // ============================================================
    // Loading
    // ============================================================

    async Task LoadAsync()
    {
        if (!_agentApi.IsConnected)
        {
            SetState(s => s.IsLoading = false);
            return;
        }

        try
        {
            var proposal = await _agentApi.GetProposalAsync(Props.ProposalId);
            ShowProposal(proposal);
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = null;
            });

            if (proposal.Status == ProposalStatus.CartReady)
            {
                StartOrderWatch();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load proposal {ProposalId}", Props.ProposalId);
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = AgentText.Unreachable;
            });
        }
    }

    /// <summary>Shows the proposal and announces an order the webhook just delivered.</summary>
    void ShowProposal(Proposal proposal)
    {
        var previous = State.Proposal?.Status;
        SetState(s => s.Proposal = proposal);

        if (previous is not null and not ProposalStatus.Ordered && proposal.Status == ProposalStatus.Ordered)
        {
            _ = _feedbackService.ShowSuccessAsync(AgentText.ProposalStatusText(proposal));
        }
    }

    void StartOrderWatch()
    {
        if (_orderWatchCts is not null)
        {
            return;
        }

        _orderWatchCts = new CancellationTokenSource(OrderCheckLimit);
        _ = WatchForOrderAsync(_orderWatchCts.Token);
    }

    void StopOrderWatch()
    {
        // Cancel only. The watch loop may still read the token, so it is not disposed.
        _orderWatchCts?.Cancel();
        _orderWatchCts = null;
    }

    async Task WatchForOrderAsync(CancellationToken ct)
    {
        try
        {
            while (true)
            {
                await Task.Delay(OrderCheckInterval, ct);
                try
                {
                    var proposal = await _agentApi.GetProposalAsync(Props.ProposalId, ct);
                    ShowProposal(proposal);
                    if (proposal.Status != ProposalStatus.CartReady)
                    {
                        break;
                    }
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogDebug(ex, "Order check for proposal {ProposalId} failed", Props.ProposalId);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        // Ended by itself (order arrived, or the time limit): allow a new watch later.
        if (_orderWatchCts?.Token == ct)
        {
            _orderWatchCts = null;
        }
    }

    // ============================================================
    // Actions
    // ============================================================

    async Task BuyAsync()
    {
        if (State.BusyAction is not null)
        {
            return;
        }

        SetState(s => s.BusyAction = Buy);
        try
        {
            var proposal = await _agentApi.PrepareCartAsync(Props.ProposalId);
            ShowProposal(proposal);

            if (!Uri.TryCreate(proposal.CheckoutUrl, UriKind.Absolute, out var checkout))
            {
                await _feedbackService.ShowErrorAsync("The shop did not return a checkout link.", "Try again.");
                return;
            }

            StartOrderWatch();
            await Browser.Default.OpenAsync(checkout, BrowserLaunchMode.External);
        }
        catch (AgentConflictException ex)
        {
            // For example, the price changed after the pick. Show why, then the new status.
            await _feedbackService.ShowWarningAsync(ex.Message, 5000);
            _ = LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not prepare the cart for proposal {ProposalId}", Props.ProposalId);
            await _feedbackService.ShowErrorAsync("Could not open the cart.", "Check that the API and the shop are running.");
        }
        finally
        {
            SetState(s => s.BusyAction = null);
        }
    }

    async Task NotNowAsync()
    {
        if (State.BusyAction is not null)
        {
            return;
        }

        SetState(s => s.BusyAction = NotNow);
        try
        {
            ShowProposal(await _agentApi.DismissAsync(Props.ProposalId));
            StopOrderWatch();
            await _feedbackService.ShowInfoAsync("Dismissed. Nothing was ordered.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not dismiss proposal {ProposalId}", Props.ProposalId);
            await _feedbackService.ShowErrorAsync("Could not dismiss this pick.", "Try again.");
        }
        finally
        {
            SetState(s => s.BusyAction = null);
        }
    }

    Task ConfirmNeverAsync()
    {
        var roaster = State.Proposal?.RoasterName ?? "this roaster";
        return ConfirmAsync(
            $"Never {roaster}?",
            $"The agent will not suggest coffee from {roaster} again. You can remove this from Memory.",
            "Never",
            NeverAsync);
    }

    async Task NeverAsync()
    {
        if (State.BusyAction is not null)
        {
            return;
        }

        SetState(s => s.BusyAction = Never);
        try
        {
            var proposal = await _agentApi.NeverThisRoasterAsync(Props.ProposalId);
            ShowProposal(proposal);
            StopOrderWatch();
            await _feedbackService.ShowSuccessAsync($"The agent will not suggest {proposal.RoasterName} again.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not block the roaster for proposal {ProposalId}", Props.ProposalId);
            await _feedbackService.ShowErrorAsync("Could not save that.", "Try again.");
        }
        finally
        {
            SetState(s => s.BusyAction = null);
        }
    }

    async Task OpenShotAsync(int shotId) =>
        await MauiControls.Shell.Current.GoToAsync<ShotLoggingGridPageProps>(
            "shot-logging",
            props => props.ShotId = shotId);

    // ============================================================
    // Rendering
    // ============================================================

    public override VisualNode Render()
    {
        return PageFrame("Pick", Header(), Body(), Footer())
            .OnAppearing(() => _ = LoadAsync());
    }

    VisualNode Header()
    {
        if (State.Proposal is not { } proposal)
        {
            return HeaderTile("PICK", _agentApi.IsConnected ? "Loading" : "Not connected");
        }

        return HeaderTile(AgentText.ProposalKind(proposal), proposal.ProductTitle,
            $"{proposal.RoasterName}{AgentText.Separator}replaces {proposal.ForBeanName}");
    }

    VisualNode Body()
    {
        if (!_agentApi.IsConnected)
        {
            return MessageBody("NOT CONNECTED", AgentText.NotConnected);
        }

        if (State.Proposal is not { } proposal)
        {
            return State.ErrorMessage is null
                ? LoadingBody()
                : MessageBody("PICK UNAVAILABLE", State.ErrorMessage, "Retry", () =>
                {
                    SetState(s => s.ErrorMessage = null);
                    _ = LoadAsync();
                });
        }

        // The reasons come first: on stage this page answers "why this one".
        var tiles = new List<VisualNode?>
        {
            InfoTile("STATUS", AgentText.ProposalStatusText(proposal), glyph: StatusGlyph(proposal.Status), glyphColor: AccentColor()),
            string.IsNullOrWhiteSpace(proposal.ActionNote)
                ? null
                : TextTile(proposal.ActionNote, glyph: MaterialSymbolsFont.Info),
            ActionRow(
                InfoTile("PRICE", AgentText.PriceLine(proposal)),
                InfoTile("BUDGET LEFT", AgentText.Money(proposal.BudgetLeft, proposal.Currency))),

            SectionLabel("WHY THIS ONE"),
            TextTile(proposal.Why),

            SectionLabel("EVIDENCE FROM YOUR NOTES"),
        };

        if (proposal.Evidence.Count == 0)
        {
            tiles.Add(TextTile("No notes cited.", secondary: true));
        }
        else
        {
            tiles.AddRange(proposal.Evidence.Select(EvidenceTile));
        }

        if (proposal.Unknowns.Count > 0)
        {
            tiles.Add(SectionLabel("WHAT I DON'T KNOW"));
            tiles.AddRange(proposal.Unknowns.Select(unknown =>
                TextTile(unknown, secondary: true, glyph: MaterialSymbolsFont.Info, glyphColor: TextSecondary())));
        }

        if (!string.IsNullOrWhiteSpace(proposal.ProductDetails))
        {
            tiles.Add(SectionLabel("THE COFFEE"));
            tiles.Add(TextTile(proposal.ProductDetails, secondary: true));
        }

        return ScrollBody(tiles);
    }

    VisualNode EvidenceTile(Evidence evidence)
    {
        var heading = AgentText.EvidenceHeading(evidence);
        var quote = AgentText.Quote(evidence.Note);

        var visual = Border(
            Grid(rows: "Auto,Auto,Auto", columns: "Auto,*,Auto",
                Glyph(AppIcons.GetRatingIcon(evidence.Rating ?? 2),
                        evidence.Rating is null ? TextSecondary() : TextPrimary())
                    .VStart()
                    .GridRow(0).GridRowSpan(3).GridColumn(0),
                MicroLabel(heading.ToUpperInvariant()).GridRow(0).GridColumn(1),
                Label(quote)
                    .FontSize(16)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .LineBreakMode(LineBreakMode.WordWrap)
                    .GridRow(1).GridColumn(1),
                Label(evidence.Point)
                    .FontSize(13)
                    .TextColor(TextSecondary())
                    .LineBreakMode(LineBreakMode.WordWrap)
                    .GridRow(2).GridColumn(1),
                Glyph(MaterialSymbolsFont.Chevron_right, TextPrimary())
                    .VCenter()
                    .GridRow(0).GridRowSpan(3).GridColumn(2)
            )
            .ColumnSpacing(12)
            .RowSpacing(4)
            .Padding(16, 12, 16, 12)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .Set(MauiControls.AutomationProperties.ExcludedWithChildrenProperty, true);

        return Grid(
            visual,
            TapTarget($"{heading}. {quote}. {evidence.Point}. Open the drink.", () => _ = OpenShotAsync(evidence.ShotId)));
    }

    VisualNode? Footer()
    {
        if (State.Proposal is not { } proposal || !_agentApi.IsConnected)
        {
            return null;
        }

        var busy = State.BusyAction;
        var canBuy = proposal.Status is ProposalStatus.Proposed or ProposalStatus.CartReady or ProposalStatus.CartRemoved;
        var canDismiss = proposal.Status is ProposalStatus.Proposed or ProposalStatus.CartReady;
        var canNever = proposal.Status is ProposalStatus.Proposed or ProposalStatus.CartReady or ProposalStatus.CartRemoved;

        if (!canBuy && !canDismiss && !canNever)
        {
            return null;
        }

        return ActionRow(
            canBuy
                ? ActionTile(Buy, MaterialSymbolsFont.Shopping_cart_checkout, () => _ = BuyAsync(),
                    primary: true, enabled: busy is null, busy: busy == Buy,
                    accessibilityName: "Buy. Opens the prepared cart in your browser.")
                : null,
            canDismiss
                ? ActionTile(NotNow, MaterialSymbolsFont.Schedule, () => _ = NotNowAsync(),
                    enabled: busy is null, busy: busy == NotNow)
                : null,
            canNever
                ? ActionTile(Never, MaterialSymbolsFont.Block, () => _ = ConfirmNeverAsync(),
                    enabled: busy is null, busy: busy == Never)
                : null);
    }

    static string StatusGlyph(ProposalStatus status) => status switch
    {
        ProposalStatus.CartReady => MaterialSymbolsFont.Shopping_cart,
        ProposalStatus.Ordered => MaterialSymbolsFont.Receipt_long,
        ProposalStatus.Dismissed => MaterialSymbolsFont.Schedule,
        ProposalStatus.CartRemoved => MaterialSymbolsFont.Remove_shopping_cart,
        ProposalStatus.Stale => MaterialSymbolsFont.Price_change,
        _ => MaterialSymbolsFont.Recommend,
    };
}
