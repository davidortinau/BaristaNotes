using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Components;
using BaristaNotes.Styles;
using Fonts;

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

            var pagedResult = await _shotService.GetShotHistoryAsync(
                pageIndex: isRefresh ? 0 : State.PageIndex,
                pageSize: State.PageSize
            );

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
                    s.ErrorMessage = null;
                });
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

    public override VisualNode Render()
    {
        return ContentPage("Shot History",
            Grid(
                RenderContent()
            )
            .SafeAreaEdges(SafeAreaEdges.None)
        )
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
            // .RemainingItemsThreshold(5)
            // .OnRemainingItemsThresholdReached(() =>
            // {
            //     if (State.HasMore && !State.IsLoading)
            //     {
            //         _ = LoadMoreShotsAsync();
            //     }
            // })
            .Header(
                ContentView().HeightRequest(16)
            )
            .Footer(
                ContentView().HeightRequest(80)
            );
    }
}
