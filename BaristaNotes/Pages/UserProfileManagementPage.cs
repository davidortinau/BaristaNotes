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
}

partial class UserProfileManagementPage : Component<UserProfileManagementState>
{
    [Inject]
    IUserProfileService _profileService;

    [Inject]
    IFeedbackService _feedbackService;

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

    void OnPageAppearing()
    {
        // Refresh data when returning from detail page
        _ = LoadDataAsync();
    }

    async Task ShowAddProfileSheet()
    {
        // Navigate to profile form page (no profileId = add mode)
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync<ProfileFormPageProps>("profile-form", props => props.ProfileId = null);
    }

    async Task ShowEditProfileSheet(UserProfileDto profile)
    {
        // Navigate to profile form page with profileId (edit mode)
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync<ProfileFormPageProps>("profile-form", props => props.ProfileId = profile.Id);
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
            ).OnAppearing(() => OnPageAppearing());
        }

        if (!string.IsNullOrEmpty(State.ErrorMessage))
        {
            return ContentPage("Profiles",
                VStack(
                    Label(MaterialSymbolsFont.Warning)
                        .FontFamily(MaterialSymbolsFont.FontFamily)
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
            ).OnAppearing(() => OnPageAppearing());
        }

        return ContentPage(
            ToolbarItem("+ Add")
                .Order(MauiControls.ToolbarItemOrder.Primary)
                .Priority(0)
                .OnClicked(async () => await ShowAddProfileSheet()),
            // Profile list
            State.Profiles.Count == 0
                ? RenderEmptyState()
                : CollectionView()
                    .ItemsSource(State.Profiles, RenderProfileItem)
                    .Margin(16, 16, 16, 32)
        ).Title("Profiles")
        .OnAppearing(() => OnPageAppearing());
    }

    VisualNode RenderEmptyState()
    {
        return VStack(spacing: 12,
            Label(MaterialSymbolsFont.Person)
                .FontFamily(MaterialSymbolsFont.FontFamily)
                .FontSize(64)
                .HCenter(),
            Label("No Profiles Yet")
                .ThemeKey(ThemeKeys.CardTitle)
                .HCenter(),
            Label("Create profiles for different users or coffee preferences")
                .ThemeKey(ThemeKeys.CardSubtitle)
                .HCenter()
        )
        .VCenter()
        .HCenter()
        .Padding(24);
    }

    VisualNode RenderProfileItem(UserProfileDto profile)
    {
        return SwipeView(
            Border(
                VStack(spacing: 4,
                    Label(profile.Name)
                        .ThemeKey(ThemeKeys.CardTitle),
                    Label($"Created: {profile.CreatedAt:MMM d, yyyy}")
                        .ThemeKey(ThemeKeys.CardSubtitle)
                )
                .Padding(12)
            )
            .ThemeKey(ThemeKeys.CardBorder)
            .OnTapped(async () => await ShowEditProfileSheet(profile))
        )
        .LeftItems(
        [
            SwipeItem()
                .BackgroundColor(Colors.Transparent)
                .IconImageSource(AppIcons.Delete)
                .OnInvoked(async () => await ShowDeleteConfirmation(profile))
        ])
        .Margin(0, 4);
    }
}
