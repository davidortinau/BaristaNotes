using BaristaNotes.Components;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;

namespace BaristaNotes.Pages;

class ProfileFormPageState
{
    public int? ProfileId { get; set; }
    public string Name { get; set; } = "";
    public bool IsSaving { get; set; }
    public string? ErrorMessage { get; set; }
}

class ProfileFormPageProps
{
    public int? ProfileId { get; set; }
}

partial class ProfileFormPage : Component<ProfileFormPageState, ProfileFormPageProps>
{
    [Inject]
    IUserProfileService _profileService;

    [Inject]
    IImagePickerService _imagePickerService;

    [Inject]
    IFeedbackService _feedbackService;

    protected override void OnMounted()
    {
        base.OnMounted();

        // If ProfileId is set via props, load the profile data
        if (Props.ProfileId.HasValue && Props.ProfileId.Value > 0)
        {
            SetState(s => s.ProfileId = Props.ProfileId);
            _ = LoadProfileAsync();
        }
    }

    async Task LoadProfileAsync()
    {
        if (!State.ProfileId.HasValue || State.ProfileId.Value <= 0) return;

        try
        {
            var profile = await _profileService.GetProfileByIdAsync(State.ProfileId.Value);

            SetState(s =>
            {
                s.ProfileId = profile.Id;
                s.Name = profile.Name;
            });
        }
        catch (Exception ex)
        {
            SetState(s => s.ErrorMessage = $"Failed to load profile: {ex.Message}");
        }
    }

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

            if (State.ProfileId.HasValue && State.ProfileId.Value > 0)
            {
                // Update existing profile
                await _profileService.UpdateProfileAsync(
                    State.ProfileId.Value,
                    new UpdateUserProfileDto { Name = State.Name });

                await _feedbackService.ShowSuccessAsync($"Profile '{State.Name}' updated successfully");
            }
            else
            {
                // Create new profile
                await _profileService.CreateProfileAsync(
                    new CreateUserProfileDto { Name = State.Name });

                await _feedbackService.ShowSuccessAsync($"Profile '{State.Name}' created successfully");
            }

            // Navigate back to profiles list
            await Microsoft.Maui.Controls.Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsSaving = false;
                s.ErrorMessage = $"Failed to save profile: {ex.Message}";
            });
        }
    }

    async Task DeleteProfile()
    {
        if (!State.ProfileId.HasValue || State.ProfileId.Value <= 0) return;

        if (Microsoft.Maui.Controls.Application.Current?.MainPage == null) return;

        var confirmed = await Microsoft.Maui.Controls.Application.Current.MainPage.DisplayAlert(
            "Delete Profile",
            $"Are you sure you want to delete '{State.Name}'? This action cannot be undone.",
            "Delete",
            "Cancel");

        if (!confirmed) return;

        try
        {
            await _profileService.DeleteProfileAsync(State.ProfileId.Value);
            await _feedbackService.ShowSuccessAsync($"Profile '{State.Name}' deleted successfully");

            // Navigate back to profiles list
            await Microsoft.Maui.Controls.Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            SetState(s => s.ErrorMessage = $"Failed to delete profile: {ex.Message}");
        }
    }

    public override VisualNode Render()
    {
        var isEditMode = State.ProfileId.HasValue && State.ProfileId.Value > 0;
        var title = isEditMode
            ? (string.IsNullOrEmpty(State.Name) ? "Edit Profile" : $"Edit {State.Name}")
            : "Add Profile";

        return ContentPage(
            isEditMode ?
                ToolbarItem()
                    .Text("Delete")
                    .IconImageSource(AppIcons.Delete)
                    .Order(Microsoft.Maui.Controls.ToolbarItemOrder.Secondary)
                    .OnClicked(DeleteProfile)
                    : null,
            ScrollView(
                VStack(spacing: 24,
                    // Header
                    Label(title)
                        .FontSize(24)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .Padding(16, 16, 16, 0),

                    // Profile image picker (only shown in edit mode since we need a profile ID)
                    isEditMode && State.ProfileId.HasValue
                        ? VStack(spacing: 16,
                            Label("Profile Picture")
                                .FontSize(16)
                                .FontAttributes(MauiControls.FontAttributes.Bold)
                                .Padding(16, 0),
                            new ProfileImagePicker(
                                State.ProfileId.Value,
                                _imagePickerService,
                                _profileService
                            )
                        )
                        : VStack(spacing: 12,
                            Label("Profile Picture")
                                .FontSize(16)
                                .FontAttributes(MauiControls.FontAttributes.Bold)
                                .Padding(16, 0),
                            Label("Save the profile first to add a photo")
                                .FontSize(14)
                                .TextColor(Colors.Gray)
                                .Padding(16, 0)
                        ),

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
                        Button(State.IsSaving ? "Saving..." : (isEditMode ? "Save Changes" : "Create Profile"))
                            .OnClicked(SaveProfile)
                            .IsEnabled(!State.IsSaving)
                            .Margin(16, 0)
                            .AutomationId("SaveProfileButton"),

                        Button("Cancel")
                            .OnClicked(async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync(".."))
                            .IsEnabled(!State.IsSaving)
                            .Margin(16, 0)
                            .BackgroundColor(Colors.Gray)
                    )
                )
            )
        ).Title(title);
    }
}
