using BaristaNotes.Components.Forms;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using BaristaNotes.Utilities;

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

    async Task ShowAddProfileSheet()
    {
        await ShowProfileFormSheet(null);
    }

    async Task ShowEditProfileSheet(UserProfileDto profile)
    {
        await ShowProfileFormSheet(profile);
    }

    async Task ShowProfileFormSheet(UserProfileDto? profile)
    {
        await BottomSheetManager.ShowAsync(
            () => new UserProfileFormComponent(
                profile,
                _profileService,
                _feedbackService,
                () => _ = LoadDataAsync()),
            sheet => sheet.HasBackdrop = true);
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

        await BottomSheetManager.ShowAsync(
            () => new DeleteProfileConfirmationComponent(
                profile,
                () => _ = DeleteProfile(profile)),
            sheet => sheet.HasBackdrop = true);
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
                HStack(spacing: 8,
                    Button("✏️")
                        .BackgroundColor(Colors.Transparent)
                        .OnClicked(async () => await ShowEditProfileSheet(profile)),
                    Button("🗑️")
                        .BackgroundColor(Colors.Transparent)
                        .OnClicked(async () => await ShowDeleteConfirmation(profile))
                )
                .GridColumn(1)
                .VCenter()
            )
            .Padding(12)
        )
        .Margin(0, 4)
        .ThemeKey(ThemeKeys.CardBorder);
    }
}
