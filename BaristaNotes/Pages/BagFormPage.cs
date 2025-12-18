using BaristaNotes.Components;
using BaristaNotes.Components.FormFields;
using BaristaNotes.Core.Services;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using Fonts;
using MauiReactor;

namespace BaristaNotes.Pages;

class BagFormPageProps
{
    public int? BagId { get; set; }  // If set, we're editing; otherwise creating
    public int BeanId { get; set; }
    public string BeanName { get; set; } = "";
}

class BagFormPageState
{
    public int? BagId { get; set; }
    public int BeanId { get; set; }
    public string BeanName { get; set; } = "";
    public DateTime RoastDate { get; set; } = DateTime.Now;
    public string Notes { get; set; } = "";

    public bool IsLoading { get; set; }
    public bool IsSaving { get; set; }
    public string? ErrorMessage { get; set; }
}

partial class BagFormPage : Component<BagFormPageState, BagFormPageProps>
{
    [Inject] IBagService _bagService;
    [Inject] IFeedbackService _feedbackService;

    protected override void OnMounted()
    {
        base.OnMounted();

        SetState(s =>
        {
            s.BagId = Props.BagId;
            s.BeanId = Props.BeanId;
            s.BeanName = Props.BeanName;
            s.IsLoading = Props.BagId.HasValue;
        });

        if (Props.BagId.HasValue)
        {
            _ = LoadBagAsync();
        }
    }

    async Task LoadBagAsync()
    {
        if (!State.BagId.HasValue) return;

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

            SetState(s =>
            {
                s.BeanId = bag.BeanId;
                s.BeanName = bag.Bean?.Name ?? Props.BeanName;
                s.RoastDate = bag.RoastDate;
                s.Notes = bag.Notes ?? "";
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

    async Task SaveBag()
    {
        try
        {
            // Validate roast date not in future
            if (State.RoastDate.Date > DateTime.Now.Date)
            {
                SetState(s => s.ErrorMessage = "Roast date cannot be in the future");
                return;
            }

            // Validate notes length
            if (!string.IsNullOrEmpty(State.Notes) && State.Notes.Length > 500)
            {
                SetState(s => s.ErrorMessage = "Notes cannot exceed 500 characters");
                return;
            }

            SetState(s =>
            {
                s.IsSaving = true;
                s.ErrorMessage = null;
            });

            var bag = new Core.Models.Bag
            {
                Id = State.BagId ?? 0,
                BeanId = State.BeanId,
                RoastDate = State.RoastDate,
                Notes = string.IsNullOrWhiteSpace(State.Notes) ? null : State.Notes
            };

            var isEditing = State.BagId.HasValue;
            var result = isEditing
                ? await _bagService.UpdateBagAsync(bag)
                : await _bagService.CreateBagAsync(bag);

            if (!result.Success)
            {
                SetState(s =>
                {
                    s.IsSaving = false;
                    s.ErrorMessage = result.ErrorMessage ?? (isEditing ? "Failed to update bag" : "Failed to add bag");
                });
                return;
            }

            await _feedbackService.ShowSuccessAsync(isEditing
                ? "Bag updated successfully"
                : $"Bag added for {State.BeanName}");

            // Navigate back
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

    public override VisualNode Render()
    {
        var isEditing = State.BagId.HasValue;
        var title = isEditing ? "Edit Bag" : "Add Bag";

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
            ScrollView(
                VStack(spacing: 24,

                    // Header
                    Label(isEditing ? $"Edit Bag for {State.BeanName}" : $"Add Bag for {State.BeanName}")
                        .FontSize(24)
                        .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold)
                        .Padding(16, 16, 16, 0),

                    // Roast Date picker
                    VStack(spacing: 12,
                        Label("Roast Date")
                            .FontSize(16)
                            .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold)
                            .Padding(16, 0),

                        Border(
                            DatePicker()
                                .Date(State.RoastDate)
                                .MaximumDate(DateTime.Now)
                                .OnDateSelected((s, e) => SetState(state => state.RoastDate = e.NewDate ?? DateTime.Now))
                        )
                        .Padding(8)
                        .Margin(16, 0)
                        .ThemeKey(ThemeKeys.CardBorder)
                    ),

                    // Notes field
                    VStack(spacing: 12,
                        Label("Notes (optional)")
                            .FontSize(16)
                            .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold)
                            .Padding(16, 0),

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
                        .Margin(16, 0)
                        .ThemeKey(ThemeKeys.CardBorder)
                    ),

                    // Error message
                    !string.IsNullOrEmpty(State.ErrorMessage)
                        ? Label(State.ErrorMessage)
                            .TextColor(Colors.Red)
                            .FontSize(14)
                            .Padding(16, 0)
                        : null,

                    // Save button
                    Button(State.IsSaving ? "Saving..." : (isEditing ? "Save Changes" : "Add Bag"))
                        .OnClicked(SaveBag)
                        .IsEnabled(!State.IsSaving)
                        .Margin(16, 0, 16, 16)
                        .HeightRequest(48)
                )
            )
        )
        .Title(title);
    }
}
