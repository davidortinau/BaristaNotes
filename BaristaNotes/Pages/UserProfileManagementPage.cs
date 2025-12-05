using BaristaNotes.Components;
using BaristaNotes.Components.Forms;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using BaristaNotes.Utilities;
using Fonts;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;

namespace BaristaNotes.Pages;

class UserProfileManagementState
{
    public List<UserProfileDto> Profiles { get; set; } = new();
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }
    public int? SelectedProfileId { get; set; }
}

partial class UserProfileManagementPage : Component<UserProfileManagementState>
{
    [Inject]
    IUserProfileService _profileService;

    [Inject]
    IFeedbackService _feedbackService;
    
    [Inject]
    IImagePickerService _imagePickerService;
    
    [Inject]
    IImageProcessingService _imageProcessingService;

    protected override void OnMounted()
    {
        base.OnMounted();
        SetState(s => s.IsLoading = true);
        _ = LoadDataAsync();
    }

    async Task LoadDataAsync()
    {
        try
        {
            var profiles = await _profileService.GetAllProfilesAsync();
            SetState(s =>
            {
                s.Profiles = profiles.ToList();
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

    async Task ShowAddProfileSheet()
    {
        await ShowProfileFormSheet(null);
    }

    async Task ShowEditProfileSheet(UserProfileDto profile)
    {
        // Show profile detail with image picker
        SetState(s => s.SelectedProfileId = profile.Id);
    }
    
    async Task ShowProfileFormSheet(UserProfileDto? profile)
    {
        await ShowProfileFormSheetLegacy(profile);
    }

    async Task ShowProfileFormSheetLegacy(UserProfileDto? profile)
    {
        var fields = new List<FormField>
        {
            new FormField
            {
                Placeholder = "Name",
                Value = profile?.Name,
                Icon = MaterialSymbolsFont.Person,
                IconColor = AppColors.Dark.TextPrimary
            },
            new FormField
            {
                Placeholder = "Avatar URL",
                Value = profile?.AvatarPath,
                Icon = MaterialSymbolsFont.Photo,
                IconColor = AppColors.Dark.TextPrimary
            }
        };

        var popup = new FormPopup
        {
            Title = profile == null ? "Add Profile" : "Edit Profile",
            // Text = "",
            Items = fields,
            ActionButtonText = "Save",
            // SecondaryActionText = "Secondary Action Text",
            SecondaryActionLinkText = "Cancel"

        };

        List<string?>? result = await IPopupService.Current.PushAsync(popup);

        if (result != null && result.Count >= 2)
        {
            string? name = result[0];
            string? avatar = result[1];
            // Process login

            if (profile != null)
            {
                await _profileService.UpdateProfileAsync(
                    profile.Id,
                    new UpdateUserProfileDto
                    {
                        Name = name,
                        AvatarPath = string.IsNullOrWhiteSpace(avatar) ? null : avatar
                    });

                await _feedbackService.ShowSuccessAsync($"{name} updated successfully");
            }
            else
            {
                await _profileService.CreateProfileAsync(
                    new CreateUserProfileDto
                    {
                        Name = name,
                        AvatarPath = string.IsNullOrWhiteSpace(avatar) ? null : avatar
                    });

                await _feedbackService.ShowSuccessAsync($"{name} created successfully");
            }
            await LoadDataAsync();
        }
    }

    async Task ShowDeleteConfirmation(UserProfileDto profile)
    {
        // Check if this is the last profile - prevent deletion
        var isLastProfile = State.Profiles.Count <= 1;

        if (isLastProfile)
        {
            await ShowLastProfileWarning();
            return;
        }

        var popup = new SimpleActionPopup
        {
            Title = $"Delete \"{profile.Name}\"?",
            Text = "This action cannot be undone.",
            ActionButtonText = "Delete",
            SecondaryActionButtonText = "Cancel",
            ActionButtonCommand = new Command(async () =>
            {
                // Delete logic here
                DeleteProfile(profile);
                await IPopupService.Current.PopAsync();
            })
        };

        await IPopupService.Current.PushAsync(popup);
    }

    async Task ShowLastProfileWarning()
    {
        await BottomSheetManager.ShowAsync(
            () => new LastProfileWarningComponent(),
            sheet => sheet.HasBackdrop = true);
    }

    async Task DeleteProfile(UserProfileDto profile)
    {
        try
        {
            await _profileService.DeleteProfileAsync(profile.Id);

            await _feedbackService.ShowSuccessAsync($"{profile.Name} deleted successfully");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {

            await _feedbackService.ShowErrorAsync("Failed to delete profile", "Please try again");
            SetState(s => s.ErrorMessage = ex.Message);
        }
    }

    public override VisualNode Render()
    {
        // If a profile is selected, show detail view
        if (State.SelectedProfileId.HasValue)
        {
            var profile = State.Profiles.FirstOrDefault(p => p.Id == State.SelectedProfileId.Value);
            if (profile != null)
            {
                return RenderProfileDetailPage(profile);
            }
        }
        
        if (State.IsLoading)
        {
            return ContentPage("Profiles",
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
            return ContentPage("Profiles",
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
                            await LoadDataAsync();
                        })
                        .Margin(0, 16, 0, 0)
                )
                .VCenter()
                .HCenter()
                .Spacing(16)
            );
        }

        return ContentPage(
            ToolbarItem("+ Add")
                .Order(MauiControls.ToolbarItemOrder.Primary)
                .Priority(0)
                .OnClicked(async () => await ShowAddProfileSheet()),
            Grid("Auto,*", "*",
                // Header with Add button
                Label("Profiles")
                    .FontSize(24)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .Padding(16, 8)
                    .GridRow(0),

                // Profile list
                State.Profiles.Count == 0
                    ? RenderEmptyState().GridRow(1)
                    : CollectionView()
                        .ItemsSource(State.Profiles, RenderProfileItem)
                        .Margin(16, 0)
                        .GridRow(1)
            )
        ).Title("Profiles");
    }

    VisualNode RenderEmptyState()
    {
        return VStack(spacing: 12,
            Label("👤")
                .FontSize(64)
                .HCenter(),
            Label("No Profiles Yet")
                .FontSize(20)
                .HCenter(),
            Label("Create profiles for different users or coffee preferences")
                .FontSize(16)
                .TextColor(Colors.Gray)
                .HCenter()
        )
        .VCenter()
        .HCenter()
        .Padding(24);
    }

    VisualNode RenderProfileItem(UserProfileDto profile)
    {
        return Border(
            Grid("Auto", "*,Auto",
                VStack(spacing: 4,
                    Label(profile.Name)
                        .ThemeKey(ThemeKeys.CardTitle),
                    Label($"Created: {profile.CreatedAt:MMM d, yyyy}")
                        .ThemeKey(ThemeKeys.CardSubtitle)
                )
                .GridColumn(0)
                .VCenter(),

                // Action buttons
                HStack(
                    ImageButton()
                        .Source(AppIcons.Edit)
                        .Aspect(Aspect.Center)
                        .BackgroundColor(Colors.Transparent)
                        .OnClicked(async () => await ShowEditProfileSheet(profile)),
                    ImageButton()
                        .Source(AppIcons.Delete)
                        .Aspect(Aspect.Center)
                        .BackgroundColor(Colors.Transparent)
                        .OnClicked(async () => await ShowDeleteConfirmation(profile))
                )
                .Spacing(AppSpacing.XS)
                .GridColumn(1)
                .VCenter()
                .HEnd()
            )
            .Padding(12)
        )
        .Margin(0, 4)
        .ThemeKey(ThemeKeys.CardBorder);
    }
    
    VisualNode RenderProfileDetailPage(UserProfileDto profile)
    {
        return ContentPage(
            ToolbarItem("Back")
                .Order(MauiControls.ToolbarItemOrder.Primary)
                .Priority(0)
                .OnClicked(() => SetState(s => s.SelectedProfileId = null)),
            ScrollView(
                VStack(spacing: 24,
                    // Profile image picker section
                    VStack(spacing: 16,
                        Label("Profile Picture")
                            .FontSize(20)
                            .FontAttributes(MauiControls.FontAttributes.Bold)
                            .Padding(16, 16, 16, 0),
                        new ProfileImagePicker(
                            profile.Id,
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
                                    Label(profile.Name)
                                ),
                                HStack(spacing: 12,
                                    Label("Created:")
                                        .FontAttributes(MauiControls.FontAttributes.Bold)
                                        .WidthRequest(80),
                                    Label(profile.CreatedAt.ToString("MMM d, yyyy"))
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
                            .OnClicked(async () => await ShowProfileFormSheetLegacy(profile))
                            .Margin(16, 0),
                        Button("Delete Profile")
                            .OnClicked(async () => 
                            {
                                await ShowDeleteConfirmation(profile);
                                SetState(s => s.SelectedProfileId = null);
                            })
                            .Margin(16, 0)
                            .BackgroundColor(Colors.Red)
                    )
                )
                .Padding(0, 16)
            )
        ).Title($"{profile.Name}'s Profile");
    }
}
