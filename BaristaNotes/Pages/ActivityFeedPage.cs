using MauiReactor;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using BaristaNotes.Components;
using BaristaNotes.Services;

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
    
    [Inject]
    IFeedbackService _feedbackService;

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
                    s.ShotRecords = shots;
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
                    s.ShotRecords = s.ShotRecords.Concat(shots).ToList();
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

    async Task LoadMoreShotsAsync()
    {
        if (State.IsLoading || !State.HasMore)
            return;

        SetState(s => s.PageIndex = s.PageIndex + 1);
        await LoadShotsAsync();
    }

    async Task NavigateToSettings()
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync("settings");
    }
    
    async void NavigateToEdit(int shotId)
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync<EditShotPageProps>("edit-shot", props => props.ShotId = shotId);
    }
    
    async Task ShowDeleteConfirmation(int shotId)
    {
        SetState(s => s.ShotToDelete = shotId);
        
        var result = await Microsoft.Maui.Controls.Application.Current!.MainPage!.DisplayAlert(
            "Delete Shot?",
            "This action cannot be undone. Are you sure you want to delete this shot?",
            "Delete",
            "Cancel"
        );
        
        if (result && State.ShotToDelete.HasValue)
        {
            await DeleteShot(State.ShotToDelete.Value);
        }
        else
        {
            SetState(s => s.ShotToDelete = null);
        }
    }
    
    async Task DeleteShot(int shotId)
    {
        try
        {
            await _shotService.DeleteShotAsync(shotId);
            _feedbackService.ShowSuccess("Shot deleted");
            
            // Refresh the list
            await LoadShotsAsync(isRefresh: true);
        }
        catch (EntityNotFoundException)
        {
            _feedbackService.ShowError("Shot not found");
        }
        catch (Exception ex)
        {
            _feedbackService.ShowError($"Error deleting shot: {ex.Message}");
        }
        finally
        {
            SetState(s => s.ShotToDelete = null);
        }
    }

    public override VisualNode Render()
    {
        return ContentPage("Shot History",
            ToolbarItem("Settings")
                .Order(Microsoft.Maui.Controls.ToolbarItemOrder.Primary)
                .Priority(0)
                .OnClicked(async () => await NavigateToSettings()),
            Grid("Auto,*", "*",
                Label("Shot History")
                    .FontSize(24)
                    .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold)
                    .Padding(16, 8)
                    .GridRow(0),

                RefreshView(
                    RenderContent()
                )
                .IsRefreshing(State.IsRefreshing)
                .OnRefreshing(async () => await LoadShotsAsync(isRefresh: true))
                .GridRow(1)
            )
        );
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
                Label("☕")
                    .FontSize(64)
                    .HCenter(),

                Label("No Shots Yet")
                    .FontSize(20)
                    .HCenter(),

                Label("Start logging your espresso shots to see them here")
                    .FontSize(16)
                    .TextColor(Colors.Gray)
                    .HCenter()
            )
            .VCenter()
            .HCenter()
            .Padding(24);
        }

        return CollectionView()
            .ItemsSource(State.ShotRecords, shot =>
                SwipeView(
                    new ShotRecordCard()
                        .Shot(shot)
                )
                .LeftItems(
                [
                    SwipeItem()
                        .Text("Edit")
                        .BackgroundColor(Colors.Blue)
                        .OnInvoked(() => NavigateToEdit(shot.Id)),
                    SwipeItem()
                        .Text("Delete")
                        .BackgroundColor(Colors.Red)
                        .OnInvoked(async () => await ShowDeleteConfirmation(shot.Id))
                ])
            )
            .RemainingItemsThreshold(5)
            .OnRemainingItemsThresholdReached(() =>
            {
                if (State.HasMore && !State.IsLoading)
                {
                    _ = LoadMoreShotsAsync();
                }
            });
    }
}
