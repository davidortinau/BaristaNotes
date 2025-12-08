using BaristaNotes.Components;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using Fonts;
using MauiReactor;

namespace BaristaNotes.Pages;

class BagDetailPageProps
{
    public int BagId { get; set; }
}

class BagDetailPageState
{
    public int BagId { get; set; }
    public string BeanName { get; set; } = "";
    public DateTime RoastDate { get; set; }
    public string? Notes { get; set; }
    public bool IsComplete { get; set; }
    public int ShotCount { get; set; }
    
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsTogglingStatus { get; set; }
    
    public RatingAggregateDto? RatingAggregate { get; set; }
}

partial class BagDetailPage : Component<BagDetailPageState, BagDetailPageProps>
{
    [Inject] IBagService _bagService;
    [Inject] IRatingService _ratingService;
    [Inject] IFeedbackService _feedbackService;
    
    protected override void OnMounted()
    {
        base.OnMounted();
        
        SetState(s =>
        {
            s.BagId = Props.BagId;
            s.IsLoading = true;
        });
        
        _ = LoadBagAsync();
    }
    
    async Task LoadBagAsync()
    {
        try
        {
            var bags = await _bagService.GetBagSummariesForBeanAsync(State.BagId, includeCompleted: true);
            var bag = bags.FirstOrDefault(b => b.Id == State.BagId);
            
            if (bag == null)
            {
                SetState(s =>
                {
                    s.IsLoading = false;
                    s.ErrorMessage = "Bag not found";
                });
                return;
            }
            
            var rating = await _ratingService.GetBagRatingAsync(State.BagId);
            
            SetState(s =>
            {
                s.BeanName = bag.BeanName;
                s.RoastDate = bag.RoastDate;
                s.Notes = bag.Notes;
                s.IsComplete = bag.IsComplete;
                s.ShotCount = bag.ShotCount;
                s.RatingAggregate = rating;
                s.IsLoading = false;
            });
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = $"Failed to load bag: {ex.Message}";
            });
        }
    }
    
    async Task ToggleBagStatus()
    {
        try
        {
            SetState(s => s.IsTogglingStatus = true);
            
            if (State.IsComplete)
            {
                await _bagService.ReactivateBagAsync(State.BagId);
                await _feedbackService.ShowSuccessAsync("Bag reactivated");
            }
            else
            {
                await _bagService.MarkBagCompleteAsync(State.BagId);
                await _feedbackService.ShowSuccessAsync("Bag marked as complete");
            }
            
            SetState(s =>
            {
                s.IsComplete = !s.IsComplete;
                s.IsTogglingStatus = false;
            });
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsTogglingStatus = false;
                s.ErrorMessage = $"Failed to update bag status: {ex.Message}";
            });
        }
    }
    
    public override VisualNode Render()
    {
        if (State.IsLoading)
        {
            return ContentPage(
                VStack(
                    ActivityIndicator()
                        .IsRunning(true)
                )
                .Center()
            )
            .Title("Loading...");
        }
        
        if (!string.IsNullOrEmpty(State.ErrorMessage))
        {
            return ContentPage(
                VStack(spacing: 16,
                    Label(State.ErrorMessage)
                        .HCenter(),
                    Button("Go Back")
                        .OnClicked(async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync(".."))
                        .ThemeKey(ThemeKeys.SecondaryButton)
                )
                .Padding(16)
                .Center()
            )
            .Title("Error");
        }
        
        return ContentPage(
            ScrollView(
                VStack(spacing: 24,
                    
                    // Header with bean name and roast date
                    VStack(spacing: 8,
                        Label(State.BeanName)
                            .ThemeKey(ThemeKeys.Headline),
                        Label($"Roasted {State.RoastDate:MMM dd, yyyy}")
                            .ThemeKey(ThemeKeys.SecondaryText)
                    ),
                    
                    // Status badge
                    HStack(spacing: 8,
                        Label(State.IsComplete ? "Complete" : "Active")
                            .ThemeKey(ThemeKeys.SecondaryText)
                            .Padding(8, 4)
                    ),
                    
                    // Notes if present
                    !string.IsNullOrEmpty(State.Notes)
                        ? VStack(spacing: 8,
                            Label("Notes")
                                .ThemeKey(ThemeKeys.SecondaryText),
                            Label(State.Notes ?? "")
                                .ThemeKey(ThemeKeys.SecondaryText)
                        )
                        : null,
                    
                    // Shot count
                    VStack(spacing: 8,
                        Label("Shots Logged")
                            .ThemeKey(ThemeKeys.SecondaryText),
                        Label(State.ShotCount.ToString())
                            .ThemeKey(ThemeKeys.CardTitle)
                    ),
                    
                    // Rating aggregate
                    State.RatingAggregate != null
                        ? VStack(spacing: 16,
                            Label("Bag Ratings")
                                .ThemeKey(ThemeKeys.SubHeadline),
                            new RatingDisplayComponent()
                                .RatingAggregate(State.RatingAggregate)
                        )
                        : null,
                    
                    // Empty state for no ratings
                    (State.RatingAggregate == null || !State.RatingAggregate.HasRatings)
                        ? Label("No ratings yet")
                            .ThemeKey(ThemeKeys.SecondaryText)
                            .HCenter()
                        : null,
                    
                    // Toggle status button
                    Button(
                        State.IsTogglingStatus 
                            ? "Updating..." 
                            : (State.IsComplete ? "Reactivate Bag" : "Mark as Complete")
                    )
                        .OnClicked(ToggleBagStatus)
                        .IsEnabled(!State.IsTogglingStatus)
                        .ThemeKey(State.IsComplete ? ThemeKeys.PrimaryButton : ThemeKeys.SecondaryButton)
                )
                .Padding(16)
            )
        )
        .Title("Bag Details");
    }
}
