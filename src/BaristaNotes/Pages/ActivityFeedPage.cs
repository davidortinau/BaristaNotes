using BaristaNotes.Integrations.Popups;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using Application = Microsoft.Maui.Controls.Application;

namespace BaristaNotes.Pages;

class ActivityFeedState
{
    public List<ShotRecordDto> ShotRecords { get; set; } = new();
    public bool IsLoading { get; set; }
    public bool IsRefreshing { get; set; }
    public string? ErrorMessage { get; set; }
    public int PageIndex { get; set; } = 0;
    public int PageSize { get; set; } = 50;
    public bool HasMore { get; set; } = true;

    // Filter state
    public ShotFilterCriteria ActiveFilters { get; set; } = new();
    public int TotalShotCount { get; set; }
    public int FilteredShotCount { get; set; }

    public bool HasActiveFilters => ActiveFilters.HasFilters;
}

partial class ActivityFeedPage : Component<ActivityFeedState>
{
    [Inject] IShotService _shotService;

    protected override void OnMounted()
    {
        base.OnMounted();
        SetState(s => s.IsLoading = true);
        _ = LoadShotsAsync();
    }

    async Task LoadShotsAsync(bool isRefresh = false)
    {
        try
        {
            if (isRefresh)
            {
                SetState(s =>
                {
                    s.IsRefreshing = true;
                    s.PageIndex = 0;
                });
            }
            else if (!State.IsLoading)
            {
                SetState(s => s.IsLoading = true);
            }

            PagedResult<ShotRecordDto> pagedResult;
            if (State.HasActiveFilters)
            {
                pagedResult = await _shotService.GetFilteredShotHistoryAsync(
                    State.ActiveFilters.ToDto(),
                    pageIndex: isRefresh ? 0 : State.PageIndex,
                    pageSize: State.PageSize);
            }
            else
            {
                pagedResult = await _shotService.GetShotHistoryAsync(
                    pageIndex: isRefresh ? 0 : State.PageIndex,
                    pageSize: State.PageSize);
            }

            var shots = pagedResult.Items.ToList();
            var hasMore = pagedResult.HasNextPage;

            if (isRefresh)
            {
                SetState(s =>
                {
                    s.ShotRecords = new List<ShotRecordDto>(shots);
                    s.IsRefreshing = false;
                    s.IsLoading = false;
                    s.HasMore = hasMore;
                    s.PageIndex = 0;
                    s.FilteredShotCount = pagedResult.TotalCount;
                    s.ErrorMessage = null;
                });
            }
            else
            {
                SetState(s =>
                {
                    s.ShotRecords = new List<ShotRecordDto>(s.ShotRecords.Concat(shots));
                    s.IsLoading = false;
                    s.HasMore = hasMore;
                    s.FilteredShotCount = pagedResult.TotalCount;
                    s.ErrorMessage = null;
                });
            }

            if (isRefresh || State.TotalShotCount == 0)
            {
                var totalResult = await _shotService.GetShotHistoryAsync(0, 1);
                SetState(s => s.TotalShotCount = totalResult.TotalCount);
            }
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsLoading = false;
                s.IsRefreshing = false;
                s.ErrorMessage = ex.Message;
            });
        }
    }

    async void NavigateToDetail(int shotId)
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync<ShotLoggingGridPageProps>("shot-logging", props => props.ShotId = shotId);
    }

    async Task OpenFilterPopup()
    {
        var beans = await _shotService.GetBeansWithShotsAsync();
        var people = await _shotService.GetPeopleWithShotsAsync();

        var popup = new ShotFilterPopup
        {
            CurrentFilters = State.ActiveFilters.Clone(),
            AvailableBeans = beans,
            AvailablePeople = people,
            OnFiltersApplied = OnFiltersApplied,
            OnFiltersCleared = OnFiltersCleared
        };
        popup.Build();

        await IPopupService.Current.PushAsync(popup);
    }

    void OnFiltersApplied(ShotFilterCriteria filters)
    {
        SetState(s =>
        {
            s.ActiveFilters = filters;
            s.PageIndex = 0;
            s.ShotRecords = new List<ShotRecordDto>();
        });
        _ = LoadShotsAsync(isRefresh: true);
    }

    void OnFiltersCleared()
    {
        SetState(s =>
        {
            s.ActiveFilters = new ShotFilterCriteria();
            s.PageIndex = 0;
            s.ShotRecords = new List<ShotRecordDto>();
        });
        _ = LoadShotsAsync(isRefresh: true);
    }

    async Task OpenVoiceFromActivityAsync()
    {
        // Voice flow lives on the Drink page (recording, command dispatch).
        // From Activity we signal the grid page to auto-open the overlay on
        // its next mount, then navigate to it.
        ShotLoggingGridPage.OpenVoiceOnNextMount = true;
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//shots");
    }

    // ============================================================
    // Rendering
    // ============================================================

    public override VisualNode Render()
    {
        return ContentPage("Activity",
            Grid(rows: "Auto,*,Auto", columns: "*",
                HeaderTile().GridRow(0),
                RenderBody().GridRow(1),
                BottomNavRow().GridRow(2)
            )
            .RowSpacing(1)
            .BackgroundColor(DividerColor())
            .Padding(1)
            .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None))
        )
        .Set(MauiControls.Shell.NavBarIsVisibleProperty, false)
        .Set(MauiControls.Shell.TabBarIsVisibleProperty, false)
        .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Never))
        .OnAppearing(() => _ = LoadShotsAsync(isRefresh: true));
    }

    // ------------------------------------------------------------
    // Theme helpers (mirror ShotLoggingGridPage)
    // ------------------------------------------------------------

    static bool IsLight() => Application.Current?.RequestedTheme != AppTheme.Dark;
    static Color SurfaceColor() => IsLight() ? AppColors.Light.Surface : AppColors.Dark.Surface;
    static Color TextPrimary() => IsLight() ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
    static Color TextSecondary() => IsLight() ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
    static Color AccentColor() => IsLight() ? AppColors.Light.Primary : AppColors.Dark.Primary;
    static Color DividerColor() => IsLight() ? AppColors.Light.Outline : AppColors.Dark.Outline;

    // ------------------------------------------------------------
    // Header — single tile spanning full width, sits in safe area
    // ------------------------------------------------------------

    VisualNode HeaderTile()
    {
        var count = State.HasActiveFilters ? State.FilteredShotCount : State.TotalShotCount;
        var countText = State.HasActiveFilters
            ? $"{State.FilteredShotCount} of {State.TotalShotCount} shots"
            : (count == 1 ? "1 shot" : $"{count} shots");

        return Border(
            Grid(rows: "Auto,*", columns: "*",
                Label("ACTIVITY")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0),
                Label(countText)
                    .FontSize(28)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .VEnd()
                    .GridRow(1)
            )
            .Padding(16, 56, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(120);
    }

    // ------------------------------------------------------------
    // Body — list / empty / loading / error
    // ------------------------------------------------------------

    VisualNode RenderBody()
    {
        if (State.IsLoading && !State.ShotRecords.Any())
        {
            return Border(
                VStack(spacing: 8,
                    ActivityIndicator().IsRunning(true),
                    Label("Loading…").FontSize(14).TextColor(TextSecondary()).HCenter()
                ).VCenter().HCenter()
            )
            .BackgroundColor(SurfaceColor())
            .StrokeThickness(0)
            .StrokeShape(new Rectangle());
        }

        if (State.ErrorMessage != null)
        {
            return Border(
                VStack(spacing: 12,
                    Label("ERROR")
                        .FontSize(10).CharacterSpacing(2)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(TextSecondary())
                        .HCenter(),
                    Label(State.ErrorMessage ?? "Unknown error")
                        .FontSize(18)
                        .TextColor(TextPrimary())
                        .HCenter(),
                    Button("Retry")
                        .OnClicked(async () => await LoadShotsAsync(isRefresh: true))
                        .BackgroundColor(AccentColor())
                        .TextColor(SurfaceColor())
                ).VCenter().HCenter().Padding(24)
            )
            .BackgroundColor(SurfaceColor())
            .StrokeThickness(0)
            .StrokeShape(new Rectangle());
        }

        if (!State.ShotRecords.Any())
        {
            var emptyTitle = State.HasActiveFilters ? "NO MATCHES" : "NO SHOTS YET";
            var emptyBody = State.HasActiveFilters
                ? "Adjust or clear filters to see results."
                : "Log a drink to see it here.";
            return Border(
                VStack(spacing: 12,
                    Label(emptyTitle)
                        .FontSize(12).CharacterSpacing(3)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(TextSecondary())
                        .HCenter(),
                    Label(emptyBody)
                        .FontSize(18)
                        .TextColor(TextPrimary())
                        .HCenter(),
                    State.HasActiveFilters
                        ? Button("Clear Filters")
                            .OnClicked(() => OnFiltersCleared())
                            .BackgroundColor(AccentColor())
                            .TextColor(SurfaceColor())
                            .Margin(0, 8, 0, 0)
                        : null
                ).VCenter().HCenter().Padding(24)
            )
            .BackgroundColor(SurfaceColor())
            .StrokeThickness(0)
            .StrokeShape(new Rectangle());
        }

        return
            CollectionView()
                .ItemsSource(State.ShotRecords, ShotRow)
                .BackgroundColor(SurfaceColor());
    }

    // ------------------------------------------------------------
    // Shot row — flat, no card, 1px hairline divider via container padding
    // ------------------------------------------------------------

    VisualNode ShotRow(ShotRecordDto shot)
    {
        var primary = shot.BrewMethod.DisplayName();
        var ratio = $"{shot.DoseIn:0.#}g → {shot.ActualOutput ?? shot.ExpectedOutput:0.#}g";
        var timeSec = shot.ActualTime ?? shot.ExpectedTime;
        var timeText = $"{timeSec:0}s";
        var ratingText = shot.Rating.HasValue
            ? new string('★', shot.Rating.Value + 1) + new string('☆', 4 - shot.Rating.Value)
            : "";
        var bean = shot.Bean?.Name ?? shot.Bag?.BeanName ?? "—";
        var when = FormatTimestamp(shot.Timestamp);

        return Border(
            Grid(rows: "Auto,Auto", columns: "*,Auto",
                Label(primary)
                    .FontSize(22)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .LineBreakMode(LineBreakMode.TailTruncation)
                    .MaxLines(1)
                    .GridRow(0).GridColumn(0),
                Label(ratio)
                    .FontSize(18)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .HEnd()
                    .GridRow(0).GridColumn(1),
                Label($"{bean}  ·  {when}")
                    .FontSize(13)
                    .TextColor(TextSecondary())
                    .LineBreakMode(LineBreakMode.TailTruncation)
                    .MaxLines(1)
                    .GridRow(1).GridColumn(0)
                    .Margin(0, 4, 0, 0),
                Label($"{timeText}   {ratingText}".Trim())
                    .FontSize(13)
                    .TextColor(TextSecondary())
                    .HEnd()
                    .GridRow(1).GridColumn(1)
                    .Margin(0, 4, 0, 0)
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .Margin(0, 0, 0, 1) // 1px gap reveals DividerColor underneath
        .OnTapped(() => NavigateToDetail(shot.Id));
    }

    static string FormatTimestamp(DateTime ts)
    {
        var local = ts.ToLocalTime();
        var today = DateTime.Today;
        if (local.Date == today) return $"Today {local:h:mm tt}";
        if (local.Date == today.AddDays(-1)) return $"Yesterday {local:h:mm tt}";
        if (local.Date > today.AddDays(-7)) return local.ToString("ddd h:mm tt");
        return local.ToString("MMM d, yyyy");
    }

    // ------------------------------------------------------------
    // Bottom nav row — mirrors ShotLoggingGridPage
    // ------------------------------------------------------------

    VisualNode BottomNavRow()
    {
        return Grid(rows: "Auto", columns: "*,*,*,*",
            NavTile(AppIcons.CoffeeCup,
                async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//shots"))
                .GridColumn(0),
            NavTile(AppIcons.Settings,
                async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//settings"))
                .GridColumn(1),
            NavTile(AppIcons.GetFilterIcon(State.HasActiveFilters),
                async () => await OpenFilterPopup())
                .GridColumn(2),
            NavTile(AppIcons.Voice,
                async () => await OpenVoiceFromActivityAsync())
                .GridColumn(3)
        )
        .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None))
        .ColumnSpacing(1)
        .BackgroundColor(DividerColor());
    }

    VisualNode NavTile(FontImageSource imageSource, Action onTap)
    {
        return Border(
            Image()
                .Source(imageSource)
                .HCenter()
                .VCenter()
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(72)
        .Padding(16, 18, 16, 30)
        .OnTapped(onTap);
    }
}
