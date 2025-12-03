using MauiReactor;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using BaristaNotes.Services;
using Microsoft.Maui.Controls;

namespace BaristaNotes.Pages;

class EditShotPageState
{
    public int ShotId { get; set; }
    public bool IsLoading { get; set; } = true;
    public bool IsSaving { get; set; }
    
    // Original (readonly) fields for display
    public DateTimeOffset Timestamp { get; set; }
    public string BeanName { get; set; } = string.Empty;
    public string GrindSetting { get; set; } = string.Empty;
    public decimal DoseIn { get; set; }
    public decimal ExpectedTime { get; set; }
    public decimal ExpectedOutput { get; set; }
    
    // Editable fields
    public string ActualTimeText { get; set; } = string.Empty;
    public string ActualOutputText { get; set; } = string.Empty;
    public int? Rating { get; set; }
    public string DrinkType { get; set; } = string.Empty;
    
    public List<string> ValidationErrors { get; set; } = new();
}

[QueryProperty(nameof(ShotId), "shotId")]
partial class EditShotPage : Component<EditShotPageState>
{
    public int _shotId;
    
    [Inject]
    IShotService _shotService;
    
    [Inject]
    IFeedbackService _feedbackService;
    
    public int ShotId
    {
        get => _shotId;
        set
        {
            _shotId = value;
            System.Diagnostics.Debug.WriteLine($"[EditShotPage] ShotId property set to: {value}");
        }
    }
    
    protected override void OnMounted()
    {
        System.Diagnostics.Debug.WriteLine($"[EditShotPage] OnMounted called, _shotId = {_shotId}");
        
        if (_shotId > 0)
        {
            State.ShotId = _shotId;
            System.Diagnostics.Debug.WriteLine($"[EditShotPage] Starting LoadShotData for ID: {_shotId}");
            _ = LoadShotData();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[EditShotPage] Invalid shotId: {_shotId}");
            _feedbackService.ShowError("Invalid shot ID");
            _ = Microsoft.Maui.Controls.Application.Current?.MainPage?.Navigation.PopAsync();
        }
        
        base.OnMounted();
    }
    
    async Task LoadShotData()
    {
        try
        {
            SetState(s => s.IsLoading = true);
            
            System.Diagnostics.Debug.WriteLine($"[EditShotPage] Loading shot ID: {State.ShotId}");
            
            var shot = await _shotService.GetShotByIdAsync(State.ShotId);
            
            System.Diagnostics.Debug.WriteLine($"[EditShotPage] Shot loaded: {shot != null}");
            
            if (shot == null)
            {
                System.Diagnostics.Debug.WriteLine($"[EditShotPage] Shot is null");
                SetState(s => s.IsLoading = false);
                _feedbackService.ShowError("Shot not found");
                await Microsoft.Maui.Controls.Application.Current?.MainPage?.Navigation.PopAsync();
                return;
            }
            
            SetState(s =>
            {
                s.IsLoading = false;
                s.Timestamp = shot.Timestamp;
                s.BeanName = shot.Bean?.Name ?? "Unknown Bean";
                s.GrindSetting = shot.GrindSetting;
                s.DoseIn = shot.DoseIn;
                s.ExpectedTime = shot.ExpectedTime;
                s.ExpectedOutput = shot.ExpectedOutput;
                s.ActualTimeText = (shot.ActualTime ?? shot.ExpectedTime).ToString();
                s.ActualOutputText = (shot.ActualOutput ?? shot.ExpectedOutput).ToString();
                s.Rating = shot.Rating;
                s.DrinkType = shot.DrinkType;
            });
            
            System.Diagnostics.Debug.WriteLine($"[EditShotPage] State updated successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EditShotPage] Error loading shot: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[EditShotPage] Stack trace: {ex.StackTrace}");
            SetState(s => s.IsLoading = false);
            _feedbackService.ShowError($"Error loading shot: {ex.Message}");
            await Microsoft.Maui.Controls.Application.Current?.MainPage?.Navigation.PopAsync();
        }
    }
    
    async Task SaveChanges()
    {
        try
        {
            SetState(s =>
            {
                s.IsSaving = true;
                s.ValidationErrors.Clear();
            });
            
            // Parse editable fields
            if (!decimal.TryParse(State.ActualTimeText, out var actualTime))
            {
                SetState(s => s.ValidationErrors.Add("Actual time must be a valid number"));
                SetState(s => s.IsSaving = false);
                return;
            }
            
            if (!decimal.TryParse(State.ActualOutputText, out var actualOutput))
            {
                SetState(s => s.ValidationErrors.Add("Actual output must be a valid number"));
                SetState(s => s.IsSaving = false);
                return;
            }
            
            var dto = new UpdateShotDto
            {
                ActualTime = actualTime,
                ActualOutput = actualOutput,
                Rating = State.Rating,
                DrinkType = State.DrinkType
            };
            
            await _shotService.UpdateShotAsync(State.ShotId, dto);
            _feedbackService.ShowSuccess("Shot updated successfully");
            await Navigation.PopAsync();
        }
        catch (ValidationException ex)
        {
            SetState(s =>
            {
                s.IsSaving = false;
                s.ValidationErrors = ex.Errors.SelectMany(kvp => kvp.Value).ToList();
            });
        }
        catch (Exception ex)
        {
            SetState(s => s.IsSaving = false);
            _feedbackService.ShowError($"Error saving: {ex.Message}");
        }
    }
    
    public override VisualNode Render()
    {
        if (State.IsLoading)
        {
            return ContentPage("Edit Shot",
                VStack(
                    ActivityIndicator()
                        .IsRunning(true),
                    Label("Loading shot data...")
                        .FontSize(16)
                        .Margin(0, 8, 0, 0)
                )
                .VCenter()
                .HCenter()
            );
        }
        
        return ContentPage("Edit Shot",
            ScrollView(
                VStack(spacing: 16,
                    // Readonly section
                    VStack(spacing: 8,
                        Label("Shot Information")
                            .FontSize(20)
                            .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold),
                        Label($"Date: {State.Timestamp:MMM d, yyyy h:mm tt}")
                            .FontSize(14)
                            .TextColor(Colors.Gray),
                        Label($"Bean: {State.BeanName}")
                            .FontSize(14)
                            .TextColor(Colors.Gray),
                        Label($"Grind: {State.GrindSetting}")
                            .FontSize(14)
                            .TextColor(Colors.Gray),
                        Label($"Dose: {State.DoseIn}g")
                            .FontSize(14)
                            .TextColor(Colors.Gray),
                        Label($"Expected: {State.ExpectedTime}s → {State.ExpectedOutput}g")
                            .FontSize(14)
                            .TextColor(Colors.Gray)
                    )
                    .Padding(16)
                    .BackgroundColor(Colors.LightGray.WithAlpha(0.1f)),
                    
                    // Editable section
                    Label("Edit Details")
                        .FontSize(20)
                        .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold),
                    
                    VStack(spacing: 4,
                        Label("Actual Time (seconds)")
                            .FontSize(14),
                        Entry(State.ActualTimeText)
                            .Keyboard(Microsoft.Maui.Keyboard.Numeric)
                            .OnTextChanged((s, e) => SetState(state => state.ActualTimeText = e.NewTextValue))
                    ),
                    
                    VStack(spacing: 4,
                        Label("Actual Output (grams)")
                            .FontSize(14),
                        Entry(State.ActualOutputText)
                            .Keyboard(Microsoft.Maui.Keyboard.Numeric)
                            .OnTextChanged((s, e) => SetState(state => state.ActualOutputText = e.NewTextValue))
                    ),
                    
                    VStack(spacing: 4,
                        Label("Rating (1-5 stars)")
                            .FontSize(14),
                        Picker()
                            .Title("Select rating")
                            .ItemsSource(new[] { "1", "2", "3", "4", "5" })
                            .SelectedIndex(State.Rating.HasValue ? State.Rating.Value - 1 : -1)
                            .OnSelectedIndexChanged((s, e) =>
                            {
                                var args = e as Microsoft.Maui.Controls.SelectedItemChangedEventArgs;
                                if (args?.SelectedItemIndex >= 0)
                                    SetState(state => state.Rating = args.SelectedItemIndex + 1);
                            })
                    ),
                    
                    VStack(spacing: 4,
                        Label("Drink Type")
                            .FontSize(14),
                        Entry(State.DrinkType)
                            .Placeholder("e.g., Espresso, Latte, Americano")
                            .OnTextChanged((s, e) => SetState(state => state.DrinkType = e.NewTextValue))
                    ),
                    
                    // Validation errors
                    RenderValidationErrors(),
                    
                    // Action buttons
                    HStack(spacing: 16,
                        Button("Cancel")
                            .HFill()
                            .OnClicked(async () => await Navigation.PopAsync()),
                        
                        Button("Save")
                            .HFill()
                            .IsEnabled(!State.IsSaving)
                            .OnClicked(async () => await SaveChanges())
                    )
                    .Margin(0, 16, 0, 0)
                )
                .Padding(16)
            )
        );
    }
    
    VisualNode? RenderValidationErrors()
    {
        if (!State.ValidationErrors.Any())
            return null;
        
        return VStack(spacing: 4,
            Label("Please fix the following errors:")
                .FontSize(14)
                .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold)
                .TextColor(Colors.Red),
            
            VStack(spacing: 2,
                State.ValidationErrors.Select(error =>
                    Label($"• {error}")
                        .FontSize(12)
                        .TextColor(Colors.Red)
                ).ToArray()
            )
        )
        .Padding(12)
        .BackgroundColor(Colors.Red.WithAlpha(0.1f))
        .Margin(0, 8, 0, 0);
    }
}
