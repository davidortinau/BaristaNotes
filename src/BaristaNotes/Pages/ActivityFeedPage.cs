using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Components;
using BaristaNotes.Styles;
using BaristaNotes.Models;
using BaristaNotes.Integrations.Popups;
using Fonts;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using UXDivers.Popups.Services;

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
    public int? ShotToDelete { get; set; }

    // Filter state
    public ShotFilterCriteria ActiveFilters { get; set; } = new();
    public int TotalShotCount { get; set; }
    public int FilteredShotCount { get; set; }

    public bool HasActiveFilters => ActiveFilters.HasFilters;
    public string ResultCountText => HasActiveFilters
        ? $"Showing {FilteredShotCount} of {TotalShotCount} shots"
        : $"{TotalShotCount} shots";
}

partial class ActivityFeedPage : Component<ActivityFeedState>
{
    [Inject]
    IShotService _shotService;

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

            // Use filtered query when filters are active
            PagedResult<ShotRecordDto> pagedResult;
            if (State.HasActiveFilters)
            {
                pagedResult = await _shotService.GetFilteredShotHistoryAsync(
                    State.ActiveFilters.ToDto(),
                    pageIndex: isRefresh ? 0 : State.PageIndex,
                    pageSize: State.PageSize
                );
            }
            else
            {
                pagedResult = await _shotService.GetShotHistoryAsync(
                    pageIndex: isRefresh ? 0 : State.PageIndex,
                    pageSize: State.PageSize
                );
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

            // Get total count for display
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
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync<ShotLoggingPageProps>("shot-logging", props => props.ShotId = shotId);
    }

    async Task OpenFilterPopup()
    {
        // Load filter options
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

    public override VisualNode Render()
    {
        return ContentPage("Shot History",
            // Filter toolbar item
            ToolbarItem()
                .IconImageSource(AppIcons.GetFilterIcon(State.HasActiveFilters))
                .Order(MauiControls.ToolbarItemOrder.Primary)
                .OnClicked(async () => await OpenFilterPopup()),

            RenderContent()
        )
        .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Always))
        .OnAppearing(() => OnPageAppearing());
    }

    void OnPageAppearing()
    {
        // Refresh data when returning from edit/delete
        _ = LoadShotsAsync(isRefresh: true);
    }

    VisualNode RenderContent()
    {
        if (State.IsLoading && !State.ShotRecords.Any())
        {
            return VStack(
                ActivityIndicator()
                    .IsRunning(true),

                Label("Loading shot history...")
                    .FontSize(16)
                    .Margin(0, 8, 0, 0)
            )
            .VCenter()
            .HCenter();
        }

        if (State.ErrorMessage != null)
        {
            return VStack(spacing: 12,
                Label("Error Loading History")
                    .FontSize(18)
                    .HCenter(),

                Label(State.ErrorMessage)
                    .FontSize(14)
                    .TextColor(Colors.Red)
                    .HCenter(),

                Button("Retry")
                    .OnClicked(async () => await LoadShotsAsync(isRefresh: true))
                    .HeightRequest(48)
            )
            .VCenter()
            .HCenter()
            .Padding(16);
        }

        if (!State.ShotRecords.Any())
        {
            // Different empty state for filtered vs unfiltered
            if (State.HasActiveFilters)
            {
                return VStack(spacing: 12,
                    Label(MaterialSymbolsFont.Filter_list_off)
                        .FontFamily(MaterialSymbolsFont.FontFamily)
                        .FontSize(64)
                        .HCenter()
                        .TextColor(AppColors.Dark.TextSecondary),

                    Label("No Matching Shots")
                        .ThemeKey(ThemeKeys.CardTitle)
                        .HCenter(),

                    Label("Try adjusting or clearing your filters")
                        .ThemeKey(ThemeKeys.CardSubtitle)
                        .HCenter(),

                    Button("Clear Filters")
                        .OnClicked(() => OnFiltersCleared())
                        .HeightRequest(48)
                        .Margin(0, 16, 0, 0)
                )
                .VCenter()
                .HCenter()
                .Padding(24);
            }

            return VStack(spacing: 12,
                Label(MaterialSymbolsFont.Coffee)
                    .FontFamily(MaterialSymbolsFont.FontFamily)
                    .FontSize(64)
                    .HCenter(),

                Label("No Shots Yet")
                    .ThemeKey(ThemeKeys.CardTitle)
                    .HCenter(),

                Label("Start logging your espresso shots to see them here")
                    .ThemeKey(ThemeKeys.CardSubtitle)
                    .HCenter()
            )
            .VCenter()
            .HCenter()
            .Padding(24);
        }

        return CollectionView()
            .ItemsSource(State.ShotRecords, shot =>
                Border(
                    new ShotRecordCard()
                        .Shot(shot)
                )
                .StrokeThickness(0)
                .OnTapped(() => NavigateToDetail(shot.Id))
                .Margin(16, 4)
            )
            .Header(
                VStack(
                    ContentView().HeightRequest(DeviceInfo.Platform == DevicePlatform.iOS ? 160 : 0),
                    State.HasActiveFilters
                        ? Label(State.ResultCountText)
                            .FontSize(14)
                            .TextColor(AppColors.Dark.TextSecondary)
                            .HCenter()
                            .Margin(0, 8, 0, 8)
                        : null
                )
            )
            .Footer(
                ContentView().HeightRequest(80)
            );
    }
}
