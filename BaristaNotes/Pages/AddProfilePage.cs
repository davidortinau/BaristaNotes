using BaristaNotes.Components;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using Fonts;

namespace BaristaNotes.Pages;

class AddProfilePageState
{
    public string Name { get; set; } = "";
    public bool IsSaving { get; set; }
    public string? ErrorMessage { get; set; }
}

partial class AddProfilePage : Component<AddProfilePageState>
{
    [Inject]
    IUserProfileService _profileService;
    
    [Inject]
    IImagePickerService _imagePickerService;
    
    [Inject]
    IFeedbackService _feedbackService;
    
    async Task SaveProfile()
    {
        try
        {
            // Validate name
            if (string.IsNullOrWhiteSpace(State.Name))
            {
                SetState(s => s.ErrorMessage = "Please enter a profile name");
                return;
            }
            
            SetState(s => 
            {
                s.IsSaving = true;
                s.ErrorMessage = null;
            });
            
            // Create profile
            await _profileService.CreateProfileAsync(new CreateUserProfileDto
            {
                Name = State.Name
            });
            
            await _feedbackService.ShowSuccessAsync($"Profile '{State.Name}' created successfully");
            
            // Navigate back to profiles list
            await Microsoft.Maui.Controls.Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsSaving = false;
                s.ErrorMessage = $"Failed to create profile: {ex.Message}";
            });
        }
    }
    
    public override VisualNode Render()
    {
        return ContentPage(
            ScrollView(
                VStack(spacing: 24,
                    // Header
                    Label("Create New Profile")
                        .FontSize(24)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .Padding(16, 16, 16, 0),
                    
                    // Name input
                    VStack(spacing: 12,
                        Label("Profile Name")
                            .FontSize(16)
                            .FontAttributes(MauiControls.FontAttributes.Bold)
                            .Padding(16, 0),
                        
                        Border(
                            Entry()
                                .Placeholder("Enter profile name")
                                .Text(State.Name)
                                .OnTextChanged((s, e) => SetState(state => state.Name = e.NewTextValue))
                                .FontSize(16)
                                .AutomationId("ProfileNameEntry")
                        )
                        .Padding(8)
                        .Margin(16, 0)
                        .ThemeKey(ThemeKeys.CardBorder)
                    ),
                    
                    // Error message
                    State.ErrorMessage != null
                        ? Border(
                            Label(State.ErrorMessage)
                                .TextColor(Colors.Red)
                                .Padding(12)
                        )
                        .Margin(16, 0)
                        .BackgroundColor(Colors.Red.WithAlpha(0.1f))
                        .StrokeThickness(1)
                        .Stroke(Colors.Red)
                        : null,
                    
                    // Action buttons
                    VStack(spacing: 12,
                        Button(State.IsSaving ? "Saving..." : "Create Profile")
                            .OnClicked(SaveProfile)
                            .IsEnabled(!State.IsSaving)
                            .Margin(16, 0)
                            .AutomationId("CreateProfileButton"),
                        
                        Button("Cancel")
                            .OnClicked(async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync(".."))
                            .IsEnabled(!State.IsSaving)
                            .Margin(16, 0)
                            .BackgroundColor(Colors.Gray)
                    )
                    .Margin(0, 16)
                )
            )
        ).Title("Add Profile");
    }
}
