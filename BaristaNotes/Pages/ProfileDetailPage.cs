using BaristaNotes.Components;
using BaristaNotes.Components.Forms;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using Fonts;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;

namespace BaristaNotes.Pages;

class ProfileDetailPageState
{
    public UserProfileDto? Profile { get; set; }
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }
}

[QueryProperty(nameof(ProfileId), "profileId")]
partial class ProfileDetailPage : Component<ProfileDetailPageState>
{
    public int ProfileId { get; set; }
    
    [Inject]
    IUserProfileService _profileService;
    
    [Inject]
    IImagePickerService _imagePickerService;
    
    [Inject]
    IFeedbackService _feedbackService;
    
    protected override void OnMounted()
    {
        base.OnMounted();
        _ = LoadProfileAsync();
    }
    
    async Task LoadProfileAsync()
    {
        try
        {
            SetState(s => s.IsLoading = true);
            
            var profile = await _profileService.GetProfileByIdAsync(ProfileId);
            
            SetState(s =>
            {
                s.Profile = profile;
                s.IsLoading = false;
            });
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = ex.Message;
            });
        }
    }
    
    async Task ShowEditNamePopup()
    {
        if (State.Profile == null) return;
        
        var fields = new List<FormField>
        {
            new FormField
            {
                Placeholder = "Name",
                Value = State.Profile.Name,
                Icon = MaterialSymbolsFont.Person,
                IconColor = AppColors.Dark.TextPrimary
            }
        };
        
        var popup = new FormPopup
        {
            Title = "Edit Profile Name",
            Items = fields,
            ActionButtonText = "Save",
            SecondaryActionLinkText = "Cancel"
        };
        
        var result = await IPopupService.Current.PushAsync(popup);
        
        if (result != null && result.Count >= 1 && !string.IsNullOrWhiteSpace(result[0]))
        {
            string? newName = result[0];
            
            await _profileService.UpdateProfileAsync(
                State.Profile.Id,
                new UpdateUserProfileDto { Name = newName });
            
            await _feedbackService.ShowSuccessAsync("Profile updated successfully");
            await LoadProfileAsync(); // Reload profile data
        }
    }
    
    async Task DeleteProfile()
    {
        if (State.Profile == null) return;
        
        var popup = new SimpleActionPopup
        {
            Title = $"Delete \"{State.Profile.Name}\"?",
            Text = "This action cannot be undone.",
            ActionButtonText = "Delete",
            SecondaryActionButtonText = "Cancel",
            ActionButtonCommand = new Command(async () =>
            {
                try
                {
                    await _profileService.DeleteProfileAsync(State.Profile.Id);
                    await _feedbackService.ShowSuccessAsync($"{State.Profile.Name} deleted successfully");
                    await IPopupService.Current.PopAsync();
                    
                    // Navigate back to profiles list
                    Navigation.PopAsync();
                }
                catch (Exception ex)
                {
                    await _feedbackService.ShowErrorAsync("Failed to delete profile", "Please try again");
                    Console.WriteLine($"Error: {ex.Message}");
                }
            })
        };
        
        await IPopupService.Current.PushAsync(popup);
    }
    
    public override VisualNode Render()
    {
        if (State.IsLoading || State.Profile == null)
        {
            return ContentPage("Profile",
                VStack(
                    ActivityIndicator()
                        .IsRunning(true)
                        .VCenter()
                        .HCenter()
                )
                .VCenter()
                .HCenter()
            );
        }
        
        if (!string.IsNullOrEmpty(State.ErrorMessage))
        {
            return ContentPage("Profile",
                VStack(
                    Label("⚠️")
                        .FontSize(48)
                        .HCenter(),
                    Label(State.ErrorMessage)
                        .HCenter(),
                    Button("Retry")
                        .OnClicked(async () =>
                        {
                            SetState(s => s.ErrorMessage = null);
                            await LoadProfileAsync();
                        })
                        .Margin(0, 16, 0, 0)
                )
                .VCenter()
                .HCenter()
                .Spacing(16)
            );
        }
        
        return ContentPage(
            ScrollView(
                VStack(spacing: 24,
                    // Profile image picker section
                    VStack(spacing: 16,
                        Label("Profile Picture")
                            .FontSize(20)
                            .FontAttributes(MauiControls.FontAttributes.Bold)
                            .Padding(16, 16, 16, 0),
                        new ProfileImagePicker(
                            State.Profile.Id,
                            _imagePickerService,
                            _profileService
                        )
                    )
                    .Padding(16, 0),
                    
                    // Profile details section
                    VStack(spacing: 16,
                        Label("Profile Details")
                            .FontSize(20)
                            .FontAttributes(MauiControls.FontAttributes.Bold)
                            .Padding(16, 0),
                        
                        Border(
                            VStack(spacing: 12,
                                HStack(spacing: 12,
                                    Label("Name:")
                                        .FontAttributes(MauiControls.FontAttributes.Bold)
                                        .WidthRequest(80),
                                    Label(State.Profile.Name)
                                ),
                                HStack(spacing: 12,
                                    Label("Created:")
                                        .FontAttributes(MauiControls.FontAttributes.Bold)
                                        .WidthRequest(80),
                                    Label(State.Profile.CreatedAt.ToString("MMM d, yyyy"))
                                )
                            )
                            .Padding(16)
                        )
                        .ThemeKey(ThemeKeys.CardBorder)
                    )
                    .Padding(16, 0),
                    
                    // Action buttons
                    VStack(spacing: 12,
                        Button("Edit Name")
                            .OnClicked(ShowEditNamePopup)
                            .Margin(16, 0),
                        Button("Delete Profile")
                            .OnClicked(DeleteProfile)
                            .Margin(16, 0)
                            .BackgroundColor(Colors.Red)
                    )
                )
                .Padding(0, 16)
            )
        ).Title($"{State.Profile.Name}'s Profile");
    }
}
