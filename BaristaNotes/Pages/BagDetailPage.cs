using BaristaNotes.Components;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using Fonts;
using MauiReactor;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;

namespace BaristaNotes.Pages;

class BagDetailPageProps
{
    public int? BagId { get; set; }  // If null/0, we're creating; otherwise editing
    public int BeanId { get; set; }
    public string BeanName { get; set; } = "";
}

class BagDetailPageState
{
    // Form fields
    public int? BagId { get; set; }
    public int BeanId { get; set; }
    public string BeanName { get; set; } = "";
    public DateTime RoastDate { get; set; } = DateTime.Now;
    public string Notes { get; set; } = "";
    public bool IsComplete { get; set; }

    // Read-only info
    public int ShotCount { get; set; }
    public RatingAggregateDto? RatingAggregate { get; set; }

    // Form state
    public bool IsLoading { get; set; }
    public bool IsSaving { get; set; }
    public string? ErrorMessage { get; set; }
}

partial class BagDetailPage : Component<BagDetailPageState, BagDetailPageProps>
{
    [Inject] IBagService _bagService;
    [Inject] IRatingService _ratingService;
    [Inject] IFeedbackService _feedbackService;

    protected override void OnMounted()
    {
        base.OnMounted();

        var isEditMode = Props.BagId.HasValue && Props.BagId.Value > 0;

        SetState(s =>
        {
            s.BagId = Props.BagId;
            s.BeanId = Props.BeanId;
            s.BeanName = Props.BeanName;
            s.IsLoading = isEditMode;
        });

        if (isEditMode)
        {
            _ = LoadBagAsync();
        }
    }

    async Task LoadBagAsync()
    {
        if (!State.BagId.HasValue || State.BagId.Value <= 0) return;

        try
        {
            var bag = await _bagService.GetBagByIdAsync(State.BagId.Value);

            if (bag == null)
            {
                SetState(s =>
                {
                    s.IsLoading = false;
                    s.ErrorMessage = "Bag not found";
                });
                return;
            }

            var rating = await _ratingService.GetBagRatingAsync(State.BagId.Value);

            SetState(s =>
            {
                s.BeanId = bag.BeanId;
                s.BeanName = bag.Bean?.Name ?? Props.BeanName;
                s.RoastDate = bag.RoastDate;
                s.Notes = bag.Notes ?? "";
                s.IsComplete = bag.IsComplete;
                s.ShotCount = bag.ShotRecords?.Count ?? 0;
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

    bool ValidateForm()
    {
        if (State.RoastDate.Date > DateTime.Now.Date)
        {
            SetState(s => s.ErrorMessage = "Roast date cannot be in the future");
            return false;
        }

        if (!string.IsNullOrEmpty(State.Notes) && State.Notes.Length > 500)
        {
            SetState(s => s.ErrorMessage = "Notes cannot exceed 500 characters");
            return false;
        }

        SetState(s => s.ErrorMessage = null);
        return true;
    }

    async Task SaveBagAsync()
    {
        if (!ValidateForm()) return;

        SetState(s =>
        {
            s.IsSaving = true;
            s.ErrorMessage = null;
        });

        try
        {
            var bag = new Core.Models.Bag
            {
                Id = State.BagId ?? 0,
                BeanId = State.BeanId,
                RoastDate = State.RoastDate,
                Notes = string.IsNullOrWhiteSpace(State.Notes) ? null : State.Notes,
                IsComplete = State.IsComplete
            };

            var isEditMode = State.BagId.HasValue && State.BagId.Value > 0;
            var result = isEditMode
                ? await _bagService.UpdateBagAsync(bag)
                : await _bagService.CreateBagAsync(bag);

            if (!result.Success)
            {
                SetState(s =>
                {
                    s.IsSaving = false;
                    s.ErrorMessage = result.ErrorMessage ?? "Failed to save bag";
                });
                return;
            }

            await _feedbackService.ShowSuccessAsync(isEditMode ? "Bag updated" : $"Bag added for {State.BeanName}");
            await Microsoft.Maui.Controls.Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsSaving = false;
                s.ErrorMessage = $"Failed to save bag: {ex.Message}";
            });
        }
    }

    async Task DeleteBagAsync()
    {
        if (!State.BagId.HasValue || State.BagId.Value <= 0) return;
        if (Application.Current?.MainPage == null) return;

        var popup = new SimpleActionPopup
        {
            Title = $"Delete Bag?",
            Text = "Are you sure you want to delete this bag? This will also delete all {State.ShotCount} associated shot records. This action cannot be undone.",
            ActionButtonText = "Delete",
            SecondaryActionButtonText = "Cancel",
            ActionButtonCommand = new Command(async () =>
            {
                await _bagService.DeleteBagAsync(State.BagId.Value);
                await _feedbackService.ShowSuccessAsync("Bag deleted");
                await IPopupService.Current.PopAsync();
                await MauiControls.Shell.Current.GoToAsync("..");

            })
        };

        await IPopupService.Current.PushAsync(popup);
    }

    async Task ToggleBagStatus()
    {
        if (!State.BagId.HasValue) return;

        try
        {
            if (State.IsComplete)
            {
                await _bagService.ReactivateBagAsync(State.BagId.Value);
                await _feedbackService.ShowSuccessAsync("Bag reactivated");
            }
            else
            {
                await _bagService.MarkBagCompleteAsync(State.BagId.Value);
                await _feedbackService.ShowSuccessAsync("Bag marked as complete");
            }

            SetState(s => s.IsComplete = !s.IsComplete);
        }
        catch (Exception ex)
        {
            SetState(s => s.ErrorMessage = $"Failed to update status: {ex.Message}");
        }
    }

    public override VisualNode Render()
    {
        var isEditMode = State.BagId.HasValue && State.BagId.Value > 0;
        var title = isEditMode ? "Edit Bag" : "Add Bag";

        if (State.IsLoading)
        {
            return ContentPage(
                VStack(
                    ActivityIndicator().IsRunning(true)
                )
                .Center()
            )
            .Title(title);
        }

        return ContentPage(
            isEditMode
                ? ToolbarItem().Text("Delete").IconImageSource(AppIcons.Delete).Order(ToolbarItemOrder.Secondary).OnClicked(async () => await DeleteBagAsync())
                : null,
            ScrollView(
                VStack(spacing: 16,
                    // Form section
                    RenderForm(),

                    // Status section (edit mode only)
                    isEditMode ? RenderStatusSection() : null,

                    // Stats section (edit mode only)
                    isEditMode ? RenderStatsSection() : null,

                    // Rating section (edit mode only)
                    isEditMode ? RenderRatings() : null
                )
                .Padding(16)
            )
        )
        .Title(title);
    }

    VisualNode RenderForm()
    {
        return VStack(spacing: 16,
            // Bean name (read-only display)
            VStack(spacing: 4,
                Label("Bean")
                    .ThemeKey(ThemeKeys.SecondaryText),
                Label(State.BeanName)
                    .ThemeKey(ThemeKeys.CardTitle)
            ),

            // Roast Date picker
            VStack(spacing: 8,
                Label("Roast Date")
                    .ThemeKey(ThemeKeys.SecondaryText),
                Border(
                    DatePicker()
                        .Date(State.RoastDate)
                        .MaximumDate(DateTime.Now)
                        .OnDateSelected((s, e) => SetState(state => state.RoastDate = e.NewDate ?? DateTime.Now))
                )
                .Padding(8)
                .ThemeKey(ThemeKeys.CardBorder)
            ),

            // Notes field
            VStack(spacing: 8,
                Label("Notes (optional)")
                    .ThemeKey(ThemeKeys.SecondaryText),
                Border(
                    Editor()
                        .Text(State.Notes)
                        .OnTextChanged((s, e) => SetState(state => state.Notes = e.NewTextValue))
                        .Placeholder("e.g., From Trader Joe's, Gift from friend")
                        .PlaceholderColor(Colors.Gray)
                        .HeightRequest(100)
                        .BackgroundColor(Colors.Transparent)
                )
                .Padding(8)
                .ThemeKey(ThemeKeys.CardBorder)
            ),

            // Error message
            !string.IsNullOrEmpty(State.ErrorMessage)
                ? Border(
                    Label(State.ErrorMessage).TextColor(Colors.Red).Padding(12)
                )
                .BackgroundColor(Colors.Red.WithAlpha(0.1f))
                .StrokeThickness(1)
                .Stroke(Colors.Red)
                : null,

            // Save button
            Button(State.IsSaving ? "Saving..." : (State.BagId.HasValue && State.BagId.Value > 0 ? "Save Changes" : "Add Bag"))
                .OnClicked(async () => await SaveBagAsync())
                .IsEnabled(!State.IsSaving)
                .HeightRequest(48)
        );
    }

    VisualNode RenderStatusSection()
    {
        return VStack(spacing: 12,
            BoxView().HeightRequest(1).Color(Colors.Gray.WithAlpha(0.3f)),

            Label("Status")
                .ThemeKey(ThemeKeys.SubHeadline),

            HStack(spacing: 12,
                Label(State.IsComplete ? "Complete" : "Active")
                    .ThemeKey(ThemeKeys.PrimaryText)
                    .VCenter(),

                Button(State.IsComplete ? "Reactivate" : "Mark Complete")
                    .OnClicked(async () => await ToggleBagStatus())
                    .ThemeKey(ThemeKeys.SecondaryButton)
                    .HEnd()
            )
        );
    }

    VisualNode RenderStatsSection()
    {
        return VStack(spacing: 12,
            BoxView().HeightRequest(1).Color(Colors.Gray.WithAlpha(0.3f)),

            HStack(spacing: 24,
                VStack(spacing: 4,
                    Label("Shots Logged")
                        .ThemeKey(ThemeKeys.SecondaryText),
                    Label(State.ShotCount.ToString())
                        .ThemeKey(ThemeKeys.CardTitle)
                )
            )
        );
    }

    VisualNode RenderRatings()
    {
        if (State.RatingAggregate == null || !State.RatingAggregate.HasRatings)
        {
            return VStack(spacing: 12,
                BoxView().HeightRequest(1).Color(Colors.Gray.WithAlpha(0.3f)),
                Label("No ratings yet")
                    .ThemeKey(ThemeKeys.SecondaryText)
                    .HCenter()
            );
        }

        return VStack(spacing: 12,
            BoxView().HeightRequest(1).Color(Colors.Gray.WithAlpha(0.3f)),

            Label("Bag Ratings")
                .ThemeKey(ThemeKeys.SubHeadline),

            new RatingDisplayComponent()
                .RatingAggregate(State.RatingAggregate)
        );
    }
}
