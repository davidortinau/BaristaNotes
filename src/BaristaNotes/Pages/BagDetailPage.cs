using BaristaNotes.Components;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using Fonts;
using MauiReactor;
using MauiReactor.Shapes;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;
using Application = Microsoft.Maui.Controls.Application;

namespace BaristaNotes.Pages;

class BagDetailPageProps
{
    public int? BagId { get; set; }
    public int BeanId { get; set; }
    public string BeanName { get; set; } = "";
}

class BagDetailPageState
{
    public int? BagId { get; set; }
    public int BeanId { get; set; }
    public string BeanName { get; set; } = "";
    public DateTime RoastDate { get; set; } = DateTime.Now;
    public string Notes { get; set; } = "";
    public bool IsComplete { get; set; }

    public int ShotCount { get; set; }
    public RatingAggregateDto? RatingAggregate { get; set; }

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

        var popup = new SimpleActionPopup
        {
            Title = "Delete Bag?",
            Text = $"Are you sure you want to delete this bag? This will also delete all {State.ShotCount} associated shot records. This action cannot be undone.",
            ActionButtonText = "Delete",
            SecondaryActionButtonText = "Cancel",
            ActionButtonCommand = new Command(async () =>
            {
                await _bagService.DeleteBagAsync(State.BagId!.Value);
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

    async Task CancelAsync()
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync("..");
    }

    // ============================================================
    // Rendering
    // ============================================================

    public override VisualNode Render()
    {
        return ContentPage("Bag",
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
        .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Never));
    }

    // ------------------------------------------------------------
    // Theme helpers
    // ------------------------------------------------------------

    static bool IsLight() => Application.Current?.RequestedTheme != AppTheme.Dark;
    static Color SurfaceColor() => IsLight() ? AppColors.Light.Surface : AppColors.Dark.Surface;
    static Color SurfaceVariantColor() => IsLight() ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;
    static Color TextPrimary() => IsLight() ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
    static Color TextSecondary() => IsLight() ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
    static Color DividerColor() => IsLight() ? AppColors.Light.Outline : AppColors.Dark.Outline;
    static Color ErrorColor() => AppColors.Error;

    // ------------------------------------------------------------
    // Header tile
    // ------------------------------------------------------------

    VisualNode HeaderTile()
    {
        var isEditMode = State.BagId.HasValue && State.BagId.Value > 0;
        var label = isEditMode ? "EDIT BAG" : "NEW BAG";
        var title = State.BeanName.Length > 0 ? State.BeanName : "Bag";

        var len = title.Length;
        double valueFontSize = len switch
        {
            <= 12 => 28,
            <= 20 => 22,
            <= 28 => 18,
            _ => 16
        };

        return Border(
            Grid(rows: "Auto,*", columns: "*",
                Label(label)
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0),
                Label(title)
                    .FontSize(valueFontSize)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .LineBreakMode(LineBreakMode.WordWrap)
                    .MaxLines(2)
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
    // Body
    // ------------------------------------------------------------

    VisualNode RenderBody()
    {
        if (State.IsLoading)
        {
            return Border(
                ActivityIndicator()
                    .IsRunning(true)
                    .VCenter()
                    .HCenter()
            )
            .BackgroundColor(SurfaceColor())
            .StrokeThickness(0)
            .StrokeShape(new Rectangle());
        }

        var isEditMode = State.BagId.HasValue && State.BagId.Value > 0;

        return ScrollView(
            Grid(
                rows: "Auto,Auto,Auto,Auto,Auto,Auto,Auto",
                columns: "*",
                RoastDateTile().GridRow(0),
                NotesTile().GridRow(1),
                isEditMode ? StatusTile().GridRow(2) : EmptyTile().GridRow(2),
                isEditMode ? StatsTile().GridRow(3) : EmptyTile().GridRow(3),
                isEditMode ? RatingsTile().GridRow(4) : EmptyTile().GridRow(4),
                State.ErrorMessage != null
                    ? ErrorTile(State.ErrorMessage).GridRow(5)
                    : EmptyTile().GridRow(5),
                Border()
                    .BackgroundColor(SurfaceColor())
                    .StrokeThickness(0)
                    .StrokeShape(new Rectangle())
                    .MinimumHeightRequest(24)
                    .GridRow(6)
            )
            .RowSpacing(1)
            .BackgroundColor(DividerColor())
        )
        .BackgroundColor(SurfaceColor());
    }

    VisualNode EmptyTile() =>
        Border()
            .BackgroundColor(SurfaceColor())
            .StrokeThickness(0)
            .StrokeShape(new Rectangle())
            .MinimumHeightRequest(0);

    VisualNode RoastDateTile()
    {
        return Border(
            VStack(spacing: 8,
                Label("ROAST DATE")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary()),
                DatePicker()
                    .Date(State.RoastDate)
                    .MaximumDate(DateTime.Now)
                    .FontSize(20)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .BackgroundColor(Colors.Transparent)
                    .OnDateSelected((s, e) => SetState(state => state.RoastDate = e.NewDate ?? DateTime.Now))
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(90);
    }

    VisualNode NotesTile()
    {
        return Border(
            Grid(rows: "Auto,Auto", columns: "*",
                Label("NOTES")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0),
                Editor()
                    .Text(State.Notes)
                    .Placeholder("From Trader Joe's, gift from friend…")
                    .PlaceholderColor(TextSecondary().WithAlpha(0.5f))
                    .TextColor(TextPrimary())
                    .FontSize(16)
                    .BackgroundColor(Colors.Transparent)
                    .AutoSize(EditorAutoSizeOption.TextChanges)
                    .HeightRequest(100)
                    .OnTextChanged((s, e) => SetState(state => state.Notes = e.NewTextValue))
                    .GridRow(1)
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(150);
    }

    VisualNode StatusTile()
    {
        var statusLabel = State.IsComplete ? "COMPLETE" : "ACTIVE";
        var actionLabel = State.IsComplete ? "REACTIVATE" : "MARK COMPLETE";

        return Border(
            Grid(rows: "Auto,Auto", columns: "*,Auto",
                Label("STATUS")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0).GridColumn(0).GridColumnSpan(2),
                Label(statusLabel)
                    .FontSize(22)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .VCenter()
                    .GridRow(1).GridColumn(0),
                Border(
                    Label(actionLabel)
                        .FontSize(11)
                        .CharacterSpacing(1.5)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(SurfaceColor())
                        .HCenter()
                        .VCenter()
                )
                .BackgroundColor(TextPrimary())
                .StrokeThickness(0)
                .StrokeShape(new Rectangle())
                .Padding(14, 10)
                .MinimumHeightRequest(40)
                .OnTapped(async () => await ToggleBagStatus())
                .VCenter()
                .GridRow(1).GridColumn(1)
            )
            .RowSpacing(8)
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(100);
    }

    VisualNode StatsTile()
    {
        return Border(
            Grid(rows: "Auto,Auto", columns: "*",
                Label("SHOTS LOGGED")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0),
                Label(State.ShotCount.ToString())
                    .FontSize(28)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .GridRow(1)
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(100);
    }

    VisualNode RatingsTile()
    {
        var hasRatings = State.RatingAggregate != null && State.RatingAggregate.HasRatings;

        return Border(
            VStack(spacing: 8,
                Label("RATINGS")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary()),
                hasRatings
                    ? (VisualNode)new RatingDisplayComponent().RatingAggregate(State.RatingAggregate)
                    : Label("No ratings yet")
                        .FontSize(16)
                        .TextColor(TextSecondary())
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(90);
    }

    VisualNode ErrorTile(string message)
    {
        return Border(
            Grid(rows: "Auto,Auto", columns: "*",
                Label("ERROR")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(SurfaceColor().WithAlpha(0.8f))
                    .GridRow(0),
                Label(message)
                    .FontSize(16)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(SurfaceColor())
                    .GridRow(1)
            )
            .Padding(16, 12, 16, 12)
        )
        .BackgroundColor(ErrorColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(60);
    }

    // ------------------------------------------------------------
    // Bottom nav row
    // ------------------------------------------------------------

    VisualNode BottomNavRow()
    {
        var isEditMode = State.BagId.HasValue && State.BagId.Value > 0;

        if (isEditMode)
        {
            return Grid(rows: "Auto", columns: "*,*,*",
                ActionTile("CANCEL", inverted: false,
                    onTap: async () => await CancelAsync()).GridColumn(0),
                ActionTile("DELETE", inverted: false, danger: true,
                    onTap: async () => await DeleteBagAsync()).GridColumn(1),
                ActionTile(State.IsSaving ? "SAVING…" : "SAVE", inverted: true,
                    onTap: async () => { if (!State.IsSaving) await SaveBagAsync(); }).GridColumn(2)
            )
            .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None))
            .ColumnSpacing(1)
            .BackgroundColor(DividerColor());
        }

        return Grid(rows: "Auto", columns: "*,*",
            ActionTile("CANCEL", inverted: false,
                onTap: async () => await CancelAsync()).GridColumn(0),
            ActionTile(State.IsSaving ? "SAVING…" : "ADD", inverted: true,
                onTap: async () => { if (!State.IsSaving) await SaveBagAsync(); }).GridColumn(1)
        )
        .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None))
        .ColumnSpacing(1)
        .BackgroundColor(DividerColor());
    }

    VisualNode ActionTile(string label, bool inverted, Action onTap, bool danger = false)
    {
        Color bg;
        Color fg;
        if (danger)
        {
            bg = ErrorColor();
            fg = SurfaceColor();
        }
        else if (inverted)
        {
            bg = TextPrimary();
            fg = SurfaceColor();
        }
        else
        {
            bg = SurfaceColor();
            fg = TextPrimary();
        }

        return Border(
            Label(label)
                .FontSize(13)
                .CharacterSpacing(2)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(fg)
                .HCenter()
                .VCenter()
        )
        .BackgroundColor(bg)
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(72)
        .Padding(8, 18, 8, 30)
        .OnTapped(onTap);
    }
}
