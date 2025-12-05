using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using BaristaNotes.Utilities;

namespace BaristaNotes.Components.Forms;

class UserProfileFormState
{
    public string Name { get; set; } = string.Empty;
    public string AvatarPath { get; set; } = string.Empty;
    public bool IsSaving { get; set; }
}

partial class UserProfileFormComponent : Component<UserProfileFormState>
{
    private readonly UserProfileDto? _profile;
    private readonly IUserProfileService _profileService;
    private readonly IFeedbackService _feedbackService;
    private readonly Action _onSaved;

    public UserProfileFormComponent(
        UserProfileDto? profile,
        IUserProfileService profileService,
        IFeedbackService feedbackService,
        Action onSaved)
    {
        _profile = profile;
        _profileService = profileService;
        _feedbackService = feedbackService;
        _onSaved = onSaved;
    }

    protected override void OnMounted()
    {
        base.OnMounted();
        
        if (_profile != null)
        {
            SetState(s =>
            {
                s.Name = _profile.Name;
                s.AvatarPath = _profile.AvatarPath ?? string.Empty;
            });
        }
    }

    async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(State.Name))
        {
            await _feedbackService.ShowErrorAsync("Profile name is required", "Please enter a name for the profile");
            return;
        }

        SetState(s => s.IsSaving = true);

        try
        {
            if (_profile != null)
            {
                await _profileService.UpdateProfileAsync(
                    _profile.Id,
                    new UpdateUserProfileDto
                    {
                        Name = State.Name,
                        AvatarPath = string.IsNullOrWhiteSpace(State.AvatarPath) ? null : State.AvatarPath
                    });

                await _feedbackService.ShowSuccessAsync($"{State.Name} updated successfully");
            }
            else
            {
                await _profileService.CreateProfileAsync(
                    new CreateUserProfileDto
                    {
                        Name = State.Name,
                        AvatarPath = string.IsNullOrWhiteSpace(State.AvatarPath) ? null : State.AvatarPath
                    });

                await _feedbackService.ShowSuccessAsync($"{State.Name} created successfully");
            }

            await BottomSheetManager.DismissAsync();
            _onSaved();
        }
        catch (Exception)
        {
            await _feedbackService.ShowErrorAsync("Failed to save profile", "Please try again");
            SetState(s => s.IsSaving = false);
        }
    }

    async Task CancelAsync()
    {
        await BottomSheetManager.DismissAsync();
    }

    public override VisualNode Render()
        => VStack(spacing: 16,
            Label(_profile != null ? "Edit Profile" : "Add Profile")
                .ThemeKey(ThemeKeys.FormTitle),

            Label("Name *")
                .ThemeKey(ThemeKeys.FormLabel),
            Entry()
                .Text(State.Name)
                .OnTextChanged(text => SetState(s => s.Name = text))
                .Placeholder("Profile name")
                .ThemeKey(ThemeKeys.Entry),

            Label("Avatar")
                .ThemeKey(ThemeKeys.FormLabel),
            Entry()
                .Text(State.AvatarPath)
                .OnTextChanged(text => SetState(s => s.AvatarPath = text))
                .Placeholder("Avatar path or URL (optional)")
                .ThemeKey(ThemeKeys.Entry),

            Label("💡 Profiles let you track shots for different users or coffee preferences")
                .ThemeKey(ThemeKeys.Caption),

            HStack(spacing: 12,
                Button("Cancel")
                    .OnClicked(CancelAsync)
                    .ThemeKey(ThemeKeys.SecondaryButton),
                Button("Save")
                    .OnClicked(SaveAsync)
                    .IsEnabled(!State.IsSaving)
                    .ThemeKey(ThemeKeys.PrimaryButton)
            )
            .HEnd()
        )
        .Padding(20)
        .ThemeKey(ThemeKeys.BottomSheet);
}
