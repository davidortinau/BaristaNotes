using BaristaNotes.Agent.Contracts;
using BaristaNotes.Services.Agent;
using static BaristaNotes.Components.AgentTiles;

namespace BaristaNotes.Pages;

class CheckInPageState
{
    public bool IsLoading { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public List<OpenBag> Bags { get; set; } = [];
    public HashSet<int> Finished { get; set; } = [];
    public bool IsSaving { get; set; }
}

/// <summary>
/// The app cannot see the cupboard, so it asks. Each open bag is either
/// still here or finished. Finished bags are marked complete on this device,
/// then the agent gets the check-in and a fresh snapshot.
/// </summary>
partial class CheckInPage : Component<CheckInPageState>
{
    [Inject] AgentApiClient _agentApi;
    [Inject] AgentSnapshotBuilder _localData;
    [Inject] IBagService _bagService;
    [Inject] IDataChangeNotifier _dataChangeNotifier;
    [Inject] IFeedbackService _feedbackService;
    [Inject] ILogger<CheckInPage> _logger;

    protected override void OnMounted()
    {
        base.OnMounted();
        _ = LoadAsync();
    }

    async Task LoadAsync()
    {
        try
        {
            var bags = await _localData.GetOpenBagsAsync();
            SetState(s =>
            {
                s.Bags = bags.ToList();
                s.Finished = [];
                s.IsLoading = false;
                s.ErrorMessage = null;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not read open bags for the check-in");
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = "Could not read your bags.";
            });
        }
    }

    void SetFinished(int bagId, bool finished)
    {
        SetState(s => s.Finished = finished
            ? [.. s.Finished, bagId]
            : s.Finished.Where(id => id != bagId).ToHashSet());
    }

    async Task SaveAsync()
    {
        if (State.IsSaving)
        {
            return;
        }

        var finished = State.Bags.Where(bag => State.Finished.Contains(bag.Id)).Select(bag => bag.Id).ToList();
        var stillHave = State.Bags.Where(bag => !State.Finished.Contains(bag.Id)).Select(bag => bag.Id).ToList();
        SetState(s => s.IsSaving = true);

        try
        {
            foreach (var bagId in finished)
            {
                await _bagService.MarkBagCompleteAsync(bagId);
                _dataChangeNotifier.NotifyDataChanged(DataChangeType.BagUpdated, bagId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not mark finished bags complete");
            SetState(s => s.IsSaving = false);
            await _feedbackService.ShowErrorAsync("Could not save your bags.", "Try again.");
            return;
        }

        try
        {
            await _agentApi.SendCheckInAsync(new CheckIn(finished, stillHave));
            await _agentApi.SendSnapshotAsync(await _localData.BuildAsync());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Saved the check-in locally but could not send it to the agent");
            SetState(s => s.IsSaving = false);
            await _feedbackService.ShowErrorAsync(
                "Saved on this device, but the agent could not be reached.",
                "Check that the API is running, then save again.");
            return;
        }

        await _feedbackService.ShowSuccessAsync(AgentText.CheckInThanks(finished.Count));
        await MauiControls.Shell.Current.GoToAsync("..");
    }

    // ============================================================
    // Rendering
    // ============================================================

    public override VisualNode Render()
    {
        return PageFrame("Check-in", Header(), Body(), Footer());
    }

    VisualNode Header()
    {
        var title = State.IsLoading
            ? "Loading"
            : AgentText.Count(State.Bags.Count, "open bag", "open bags");
        return HeaderTile("STILL HAVE THESE?", title, "Mark what you finished. The agent only replaces finished bags.");
    }

    VisualNode Body()
    {
        if (!_agentApi.IsConnected)
        {
            return MessageBody("NOT CONNECTED", AgentText.NotConnected);
        }

        if (State.IsLoading)
        {
            return LoadingBody();
        }

        if (State.ErrorMessage is not null)
        {
            return MessageBody("ERROR", State.ErrorMessage, "Retry", () =>
            {
                SetState(s => s.IsLoading = true);
                _ = LoadAsync();
            });
        }

        if (State.Bags.Count == 0)
        {
            return MessageBody("NO OPEN BAGS", "Nothing to check. Add a bag when you buy coffee.");
        }

        var today = DateTime.Today;
        return ScrollBody(State.Bags.SelectMany(bag => BagTiles(bag, today)));
    }

    IEnumerable<VisualNode?> BagTiles(OpenBag bag, DateTime today)
    {
        var finished = State.Finished.Contains(bag.Id);
        var detail = string.IsNullOrWhiteSpace(bag.Roaster)
            ? AgentText.RoastedAgo(bag.RoastDate, today)
            : $"{bag.Roaster}{AgentText.Separator}{AgentText.RoastedAgo(bag.RoastDate, today)}";

        yield return Border(
            VStack(spacing: 4,
                Label(bag.BeanName)
                    .FontSize(20)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .LineBreakMode(LineBreakMode.WordWrap),
                Label(detail)
                    .FontSize(13)
                    .TextColor(TextSecondary())
                    .LineBreakMode(LineBreakMode.WordWrap)
            )
            .Padding(16, 14, 16, 12)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle());

        yield return ActionRow(
            ActionTile("STILL HAVE", MaterialSymbolsFont.Inventory_2, () => SetFinished(bag.Id, false),
                primary: !finished,
                enabled: !State.IsSaving,
                accessibilityName: $"{bag.BeanName}: still have{(finished ? "" : ", selected")}"),
            ActionTile("FINISHED", MaterialSymbolsFont.Task_alt, () => SetFinished(bag.Id, true),
                primary: finished,
                enabled: !State.IsSaving,
                accessibilityName: $"{bag.BeanName}: finished{(finished ? ", selected" : "")}"));
    }

    VisualNode? Footer()
    {
        if (!_agentApi.IsConnected || State.IsLoading || State.Bags.Count == 0)
        {
            return null;
        }

        return ActionTile("SAVE", MaterialSymbolsFont.Done_all, () => _ = SaveAsync(),
            primary: true,
            busy: State.IsSaving,
            accessibilityName: "Save the check-in");
    }
}
