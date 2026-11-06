using BaristaNotes.Agent.Contracts;
using BaristaNotes.Services.Agent;
using static BaristaNotes.Components.AgentTiles;

namespace BaristaNotes.Pages;

class MemoryPageState
{
    public bool IsLoading { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public MemoryView? View { get; set; }
    public HashSet<Guid> Expanded { get; set; } = [];
    public HashSet<Guid> Removing { get; set; } = [];
    public bool IsDeletingShared { get; set; }
}

/// <summary>
/// What the agent learned, with the drinks it learned from. Removing an
/// item means it is never learned again. Also shows what the app shared and
/// lets you delete it.
/// </summary>
partial class MemoryPage : Component<MemoryPageState>
{
    [Inject] AgentApiClient _agentApi;
    [Inject] IFeedbackService _feedbackService;
    [Inject] ILogger<MemoryPage> _logger;

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
            var view = await _agentApi.GetMemoryAsync();
            SetState(s =>
            {
                s.View = view;
                s.IsLoading = false;
                s.ErrorMessage = null;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load the agent memory");
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = AgentText.Unreachable;
            });
        }
    }

    void ToggleExpanded(Guid id)
    {
        SetState(s => s.Expanded = s.Expanded.Contains(id)
            ? s.Expanded.Where(item => item != id).ToHashSet()
            : [.. s.Expanded, id]);
    }

    Task ConfirmForgetAsync(MemoryItem item) =>
        ConfirmAsync(
            "Forget this?",
            "The agent will forget this and not learn it again.",
            "Forget",
            () => ForgetAsync(item));

    async Task ForgetAsync(MemoryItem item)
    {
        SetState(s => s.Removing = [.. s.Removing, item.Id]);
        try
        {
            await _agentApi.ForgetMemoryAsync(item.Id);
            await LoadAsync();
            await _feedbackService.ShowSuccessAsync("Forgotten. The agent will not learn this again.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove memory item {MemoryId}", item.Id);
            await _feedbackService.ShowErrorAsync("Could not remove this.", "Try again.");
        }
        finally
        {
            SetState(s => s.Removing = s.Removing.Where(id => id != item.Id).ToHashSet());
        }
    }

    Task ConfirmDeleteSharedAsync() =>
        ConfirmAsync(
            "Delete shared data?",
            "The agent deletes the notes this app shared and everything it learned from them.",
            "Delete",
            DeleteSharedAsync);

    async Task DeleteSharedAsync()
    {
        SetState(s => s.IsDeletingShared = true);
        try
        {
            await _agentApi.DeleteSnapshotAsync();
            await LoadAsync();
            await _feedbackService.ShowSuccessAsync("Shared data deleted.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete the shared snapshot");
            await _feedbackService.ShowErrorAsync("Could not delete shared data.", "Try again.");
        }
        finally
        {
            SetState(s => s.IsDeletingShared = false);
        }
    }

    // ============================================================
    // Rendering
    // ============================================================

    public override VisualNode Render()
    {
        return PageFrame("Memory", Header(), Body())
            .OnAppearing(() => _ = LoadAsync());
    }

    VisualNode Header()
    {
        var title = !_agentApi.IsConnected
            ? "Not connected"
            : State.View is { } view
                ? AgentText.LearnedTitle(view.Items.Count)
                : "Loading";
        return HeaderTile("MEMORY", title);
    }

    VisualNode Body()
    {
        if (!_agentApi.IsConnected)
        {
            return MessageBody("NOT CONNECTED", AgentText.NotConnected);
        }

        if (State.View is not { } view)
        {
            return State.ErrorMessage is null
                ? LoadingBody()
                : MessageBody("MEMORY UNAVAILABLE", State.ErrorMessage, "Retry", () =>
                {
                    SetState(s => s.ErrorMessage = null);
                    _ = LoadAsync();
                });
        }

        var tiles = new List<VisualNode?>();

        if (view.Items.Count == 0)
        {
            tiles.Add(TextTile("Nothing learned yet. Run the agent to start.", secondary: true));
        }

        AddSection(tiles, "LIKES", view.Items.Where(item => item.Kind == MemoryKind.Likes));
        AddSection(tiles, "DISLIKES", view.Items.Where(item => item.Kind == MemoryKind.Dislikes));
        AddSection(tiles, "NEVER", view.Items.Where(item => item.Kind == MemoryKind.NeverRoaster));

        if (view.Removed.Count > 0)
        {
            tiles.Add(SectionLabel("REMOVED"));
            tiles.AddRange(view.Removed.Select(RemovedTile));
        }

        tiles.Add(SectionLabel("WHAT WAS SHARED"));
        tiles.Add(view.LastSnapshot is { } snapshot
            ? TextTile(AgentText.SnapshotLine(snapshot), glyph: MaterialSymbolsFont.Cloud_upload)
            : TextTile("Nothing shared yet.", secondary: true));
        tiles.Add(TextTile(AgentText.NothingShared, secondary: true, glyph: MaterialSymbolsFont.Shield, glyphColor: TextSecondary()));
        if (view.LastSnapshot is not null)
        {
            tiles.Add(ActionRow(
                ActionTile("DELETE SHARED DATA", MaterialSymbolsFont.Delete_forever, () => _ = ConfirmDeleteSharedAsync(),
                    busy: State.IsDeletingShared)));
        }

        return ScrollBody(tiles);
    }

    void AddSection(List<VisualNode?> tiles, string label, IEnumerable<MemoryItem> items)
    {
        var list = items.ToList();
        if (list.Count == 0)
        {
            return;
        }

        tiles.Add(SectionLabel(label));
        foreach (var item in list)
        {
            tiles.AddRange(ItemTiles(item));
        }
    }

    IEnumerable<VisualNode?> ItemTiles(MemoryItem item)
    {
        var expanded = State.Expanded.Contains(item.Id);
        var basis = AgentText.MemoryBasis(item);

        var visual = Border(
            Grid(rows: "Auto,Auto", columns: "Auto,*,Auto",
                Glyph(KindGlyph(item.Kind), TextPrimary())
                    .VStart()
                    .GridRow(0).GridRowSpan(2).GridColumn(0),
                Label(item.Statement)
                    .FontSize(18)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .LineBreakMode(LineBreakMode.WordWrap)
                    .GridRow(0).GridColumn(1),
                Label(basis)
                    .FontSize(13)
                    .TextColor(TextSecondary())
                    .GridRow(1).GridColumn(1),
                Glyph(expanded ? MaterialSymbolsFont.Expand_less : MaterialSymbolsFont.Expand_more, TextPrimary())
                    .VCenter()
                    .GridRow(0).GridRowSpan(2).GridColumn(2)
            )
            .ColumnSpacing(12)
            .RowSpacing(4)
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .Set(MauiControls.AutomationProperties.ExcludedWithChildrenProperty, true);

        yield return Grid(
            visual,
            TapTarget($"{item.Statement}. {basis}. {(expanded ? "Hide" : "Show")} evidence.", () => ToggleExpanded(item.Id)));

        if (!expanded)
        {
            yield break;
        }

        foreach (var evidence in item.Evidence)
        {
            yield return EvidenceTile(evidence);
        }

        yield return ActionRow(
            ActionTile("FORGET THIS", MaterialSymbolsFont.Delete, () => _ = ConfirmForgetAsync(item),
                busy: State.Removing.Contains(item.Id),
                accessibilityName: $"Forget: {item.Statement}"));
    }

    static VisualNode EvidenceTile(Evidence evidence)
    {
        var parts = new List<string>();
        if (evidence.Rating is { } rating)
            parts.Add($"{rating}/4");
        if (evidence.MadeAt is { } madeAt)
            parts.Add(AgentText.ShortDate(madeAt));
        if (!string.IsNullOrWhiteSpace(evidence.BeanName))
            parts.Add(evidence.BeanName!);
        parts.Add($"Shot {evidence.ShotId}");

        return Border(
            VStack(spacing: 4,
                Label(AgentText.Quote(evidence.Note))
                    .FontSize(15)
                    .TextColor(TextPrimary())
                    .LineBreakMode(LineBreakMode.WordWrap),
                Label(string.Join(AgentText.Separator, parts))
                    .FontSize(12)
                    .TextColor(TextSecondary())
                    .LineBreakMode(LineBreakMode.WordWrap)
            )
            .Padding(52, 10, 16, 10)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle());
    }

    static VisualNode RemovedTile(MemoryItem item)
    {
        return Border(
            VStack(spacing: 4,
                Label(item.Statement)
                    .FontSize(16)
                    .TextColor(TextSecondary())
                    .TextDecorations(TextDecorations.Strikethrough)
                    .LineBreakMode(LineBreakMode.WordWrap),
                Label("Will not be learned again")
                    .FontSize(12)
                    .TextColor(TextSecondary())
            )
            .Padding(16, 12, 16, 12)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle());
    }

    static string KindGlyph(MemoryKind kind) => kind switch
    {
        MemoryKind.Likes => MaterialSymbolsFont.Thumb_up,
        MemoryKind.Dislikes => MaterialSymbolsFont.Thumb_down,
        MemoryKind.NeverRoaster => MaterialSymbolsFont.Block,
        _ => MaterialSymbolsFont.Psychology,
    };
}
