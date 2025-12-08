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
    public int BeanId { get; set; }
    public string BeanName { get; set; } = "";
}

class BagFormPageState
{
    public int BeanId { get; set; }
    public string BeanName { get; set; } = "";
    public DateTime RoastDate { get; set; } = DateTime.Now;
    public string Notes { get; set; } = "";
    
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
            s.BeanId = Props.BeanId;
            s.BeanName = Props.BeanName;
        });
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
                BeanId = State.BeanId,
                RoastDate = State.RoastDate,
                Notes = string.IsNullOrWhiteSpace(State.Notes) ? null : State.Notes
            };
            
            var result = await _bagService.CreateBagAsync(bag);
            
            if (!result.Success)
            {
                SetState(s =>
                {
                    s.IsSaving = false;
                    s.ErrorMessage = result.ErrorMessage ?? "Failed to add bag";
                });
                return;
            }
            
            await _feedbackService.ShowSuccessAsync($"Bag added for {State.BeanName}");
            
            // Navigate back to bean detail
            await Microsoft.Maui.Controls.Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsSaving = false;
                s.ErrorMessage = $"Failed to add bag: {ex.Message}";
            });
        }
    }
    
    public override VisualNode Render()
    {
        return ContentPage(
            ScrollView(
                VStack(spacing: 24,
                    
                    // Header
                    Label($"Add Bag for {State.BeanName}")
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
                    Button(State.IsSaving ? "Saving..." : "Add Bag")
                        .OnClicked(SaveBag)
                        .IsEnabled(!State.IsSaving)
                        .Margin(16, 0, 16, 16)
                        .HeightRequest(48)
                )
            )
        )
        .Title("Add Bag");
    }
}
