using BaristaNotes.Agent.Contracts;
using BaristaNotes.Services.Agent;
using static BaristaNotes.Components.AgentTiles;

namespace BaristaNotes.Pages;

class AgentRoastersPageState
{
    public bool IsLoading { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public AgentSettings? Settings { get; set; }
    public List<Roaster> Roasters { get; set; } = [];
    public string? SavingHandle { get; set; }
}

/// <summary>The roasters the shop sells from. The agent only checks watched ones.</summary>
partial class AgentRoastersPage : Component<AgentRoastersPageState>
{
    [Inject] AgentApiClient _agentApi;
    [Inject] IFeedbackService _feedbackService;
    [Inject] ILogger<AgentRoastersPage> _logger;

    protected override void OnMounted()
    {
        base.OnMounted();
        _ = LoadAsync();
    }

    async Task LoadAsync()
    {
        if (!_agentApi.IsConnected)
        {
            SetState(s => s.IsLoading = false);
            return;
        }

        try
        {
            var settings = await _agentApi.GetSettingsAsync();
            var roasters = await _agentApi.GetRoastersAsync();
            SetState(s =>
            {
                s.Settings = settings;
                s.Roasters = roasters.ToList();
                s.IsLoading = false;
                s.ErrorMessage = null;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load the roasters");
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = AgentText.Unreachable;
            });
        }
    }

    async Task ToggleAsync(Roaster roaster)
    {
        if (State.Settings is not { } settings || State.SavingHandle is not null)
        {
            return;
        }

        var before = State.Roasters;
        var after = before
            .Select(item => item.Handle == roaster.Handle ? item with { Watched = !item.Watched } : item)
            .ToList();
        var watched = after.Where(item => item.Watched).Select(item => item.Handle).ToList();

        // Show the change right away. Put it back if the save fails.
        SetState(s =>
        {
            s.Roasters = after;
            s.SavingHandle = roaster.Handle;
        });

        try
        {
            var saved = await _agentApi.SaveSettingsAsync(settings with { WatchedRoasters = watched });
            var savedSet = saved.WatchedRoasters.ToHashSet(StringComparer.OrdinalIgnoreCase);
            SetState(s =>
            {
                s.Settings = saved;
                s.Roasters = s.Roasters.Select(item => item with { Watched = savedSet.Contains(item.Handle) }).ToList();
                s.SavingHandle = null;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save watched roasters");
            SetState(s =>
            {
                s.Roasters = before;
                s.SavingHandle = null;
            });
            await _feedbackService.ShowErrorAsync("Could not save your roasters.", "Try again.");
        }
    }

    // ============================================================
    // Rendering
    // ============================================================

    public override VisualNode Render()
    {
        return PageFrame("Roasters", Header(), Body());
    }

    VisualNode Header()
    {
        var title = !_agentApi.IsConnected
            ? "Not connected"
            : State.IsLoading
                ? "Loading"
                : $"{State.Roasters.Count(roaster => roaster.Watched)} watched";
        return HeaderTile("ROASTERS", title, "The agent only checks roasters you watch.");
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
            return MessageBody("ROASTERS UNAVAILABLE", State.ErrorMessage, "Retry", () =>
            {
                SetState(s => s.IsLoading = true);
                _ = LoadAsync();
            });
        }

        if (State.Roasters.Count == 0)
        {
            return MessageBody("NO ROASTERS", "The shop has no roasters yet.");
        }

        return ScrollBody(State.Roasters.Select(RoasterTile));
    }

    VisualNode RoasterTile(Roaster roaster)
    {
        var label = $"{roaster.City.ToUpperInvariant()}{AgentText.Separator}"
            + $"{AgentText.Money(roaster.Shipping, roaster.Currency)} SHIPPING";
        var saving = State.SavingHandle == roaster.Handle;

        var trailing = saving
            ? (VisualNode)ActivityIndicator().IsRunning(true).Color(TextPrimary()).VCenter()
            : Glyph(
                roaster.Watched ? MaterialSymbolsFont.Check_box : MaterialSymbolsFont.Check_box_outline_blank,
                roaster.Watched ? AccentColor() : TextSecondary());

        return new AdaptiveTwoLineTile(
                MicroLabel(label),
                Label(roaster.Name)
                    .FontSize(18)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .LineBreakMode(LineBreakMode.TailTruncation)
                    .MaxLines(1))
            .Trailing(trailing)
            .BackgroundColor(SurfaceColor())
            .AutomationId($"Roaster_{roaster.Handle}")
            .OnTapped(
                $"{roaster.Name}, {roaster.City}. {(roaster.Watched ? "Watched" : "Not watched")}. Tap to change.",
                () => _ = ToggleAsync(roaster));
    }
}
