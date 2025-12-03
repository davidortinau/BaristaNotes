using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using MauiReactor;
using The49MauiBottomSheet = The49.Maui.BottomSheet;
using MauiControls = Microsoft.Maui.Controls;
using UXDivers.Popups.Services;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups;

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

    private The49MauiBottomSheet.BottomSheet? _currentSheet;

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
        var page = ContainerPage;
        if (page?.Window == null) return;

        // Create form fields
        var nameEntry = new MauiControls.Entry
        {
            Placeholder = "Profile name (required)",
            Text = profile?.Name ?? "",
            BackgroundColor = Colors.White
        };

        var avatarEntry = new MauiControls.Entry
        {
            Placeholder = "Avatar path or URL (optional)",
            Text = profile?.AvatarPath ?? "",
            BackgroundColor = Colors.White
        };

        var errorLabel = new MauiControls.Label
        {
            TextColor = Colors.Red,
            FontSize = 12,
            IsVisible = false
        };

        var saveButton = new MauiControls.Button
        {
            Text = "Save",
            BackgroundColor = Colors.Purple,
            TextColor = Colors.White
        };

        var cancelButton = new MauiControls.Button
        {
            Text = "Cancel",
            BackgroundColor = Colors.LightGray,
            TextColor = Colors.Black
        };

        cancelButton.Clicked += async (s, e) =>
        {
            await _currentSheet?.DismissAsync()!;
        };

        saveButton.Clicked += async (s, e) =>
        {
            // Validate
            if (string.IsNullOrWhiteSpace(nameEntry.Text))
            {
                await _feedbackService.ShowErrorAsync("Profile name is required", "Please enter a name for the profile");
                return;
            }

            saveButton.IsEnabled = false;

            try
            {
                if (profile != null)
                {
                    await _profileService.UpdateProfileAsync(
                        profile.Id,
                        new UpdateUserProfileDto
                        {
                            Name = nameEntry.Text,
                            AvatarPath = string.IsNullOrWhiteSpace(avatarEntry.Text) ? null : avatarEntry.Text
                        });

                    await _feedbackService.ShowSuccessAsync($"{nameEntry.Text} updated successfully");
                }
                else
                {
                    await _profileService.CreateProfileAsync(
                        new CreateUserProfileDto
                        {
                            Name = nameEntry.Text,
                            AvatarPath = string.IsNullOrWhiteSpace(avatarEntry.Text) ? null : avatarEntry.Text
                        });

                    await _feedbackService.ShowSuccessAsync($"{nameEntry.Text} created successfully");

                }

                await _currentSheet?.DismissAsync()!;
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                await _feedbackService.ShowErrorAsync("Failed to save profile", "Please try again");
                saveButton.IsEnabled = true;
            }
        };

        var formContent = new MauiControls.VerticalStackLayout
        {
            Spacing = 16,
            Padding = new Thickness(20),
            BackgroundColor = Colors.White,
            Children =
            {
                new MauiControls.Label
                {
                    Text = profile != null ? "Edit Profile" : "Add Profile",
                    FontSize = 20,
                    FontAttributes = MauiControls.FontAttributes.Bold
                },
                new MauiControls.Label { Text = "Name *", FontSize = 14 },
                nameEntry,
                new MauiControls.Label { Text = "Avatar", FontSize = 14 },
                avatarEntry,
                new MauiControls.Label
                {
                    Text = "💡 Profiles let you track shots for different users or coffee preferences",
                    FontSize = 12,
                    TextColor = Colors.Gray
                },
                errorLabel,
                new MauiControls.HorizontalStackLayout
                {
                    Spacing = 12,
                    HorizontalOptions = MauiControls.LayoutOptions.End,
                    Children = { cancelButton, saveButton }
                }
            }
        };

        _currentSheet = new The49MauiBottomSheet.BottomSheet
        {
            HasHandle = true,
            IsCancelable = true,
            Content = formContent
        };

        await _currentSheet.ShowAsync(page.Window);
    }

    async Task ShowDeleteConfirmation(UserProfileDto profile)
    {
        var page = ContainerPage;
        if (page?.Window == null) return;

        // Check if this is the last profile - prevent deletion
        var isLastProfile = State.Profiles.Count <= 1;

        if (isLastProfile)
        {
            await ShowLastProfileWarning();
            return;
        }

        var confirmButton = new MauiControls.Button
        {
            Text = "Delete",
            BackgroundColor = Colors.Red,
            TextColor = Colors.White
        };

        var cancelButton = new MauiControls.Button
        {
            Text = "Cancel",
            BackgroundColor = Colors.LightGray,
            TextColor = Colors.Black
        };

        cancelButton.Clicked += async (s, e) =>
        {
            await _currentSheet?.DismissAsync()!;
        };

        confirmButton.Clicked += async (s, e) =>
        {
            await _currentSheet?.DismissAsync()!;
            await DeleteProfile(profile);
        };

        var confirmContent = new MauiControls.VerticalStackLayout
        {
            Spacing = 16,
            Padding = new Thickness(24),
            BackgroundColor = Colors.White,
            Children =
            {
                new MauiControls.HorizontalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new MauiControls.Label { Text = "⚠️", FontSize = 24 },
                        new MauiControls.Label
                        {
                            Text = "Delete Profile",
                            FontSize = 20,
                            FontAttributes = MauiControls.FontAttributes.Bold,
                            TextColor = Colors.Red
                        }
                    }
                },
                new MauiControls.Label
                {
                    Text = $"\"{profile.Name}\"",
                    FontSize = 16,
                    FontAttributes = MauiControls.FontAttributes.Bold,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new MauiControls.Label
                {
                    Text = "Are you sure you want to delete this profile? Shot records associated with this profile will retain the historical reference.",
                    FontSize = 14,
                    TextColor = Colors.Gray,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new MauiControls.HorizontalStackLayout
                {
                    Spacing = 12,
                    HorizontalOptions = MauiControls.LayoutOptions.Center,
                    Children = { cancelButton, confirmButton }
                }
            }
        };

        _currentSheet = new The49MauiBottomSheet.BottomSheet
        {
            HasHandle = true,
            IsCancelable = true,
            Content = confirmContent
        };

        await _currentSheet.ShowAsync(page.Window);
    }

    async Task ShowLastProfileWarning()
    {
        var page = ContainerPage;
        if (page?.Window == null) return;

        var okButton = new MauiControls.Button
        {
            Text = "OK",
            BackgroundColor = Colors.Purple,
            TextColor = Colors.White
        };

        okButton.Clicked += async (s, e) =>
        {
            await _currentSheet?.DismissAsync()!;
        };

        var warningContent = new MauiControls.VerticalStackLayout
        {
            Spacing = 16,
            Padding = new Thickness(24),
            BackgroundColor = Colors.White,
            Children =
            {
                new MauiControls.HorizontalStackLayout
                {
                    Spacing = 8,
                    HorizontalOptions = MauiControls.LayoutOptions.Center,
                    Children =
                    {
                        new MauiControls.Label { Text = "🚫", FontSize = 24 },
                        new MauiControls.Label
                        {
                            Text = "Cannot Delete",
                            FontSize = 20,
                            FontAttributes = MauiControls.FontAttributes.Bold
                        }
                    }
                },
                new MauiControls.Label
                {
                    Text = "This is your last profile. You must have at least one profile to log shots.",
                    FontSize = 14,
                    TextColor = Colors.Gray,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new MauiControls.Label
                {
                    Text = "💡 Create another profile first if you want to delete this one.",
                    FontSize = 12,
                    TextColor = Colors.DarkGray,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                okButton
            }
        };

        _currentSheet = new The49MauiBottomSheet.BottomSheet
        {
            HasHandle = true,
            IsCancelable = true,
            Content = warningContent
        };

        await _currentSheet.ShowAsync(page.Window);
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
                        .FontSize(18)
                        .FontAttributes(MauiControls.FontAttributes.Bold),
                    Label($"Created: {profile.CreatedAt:MMM d, yyyy}")
                        .FontSize(12)
                        .TextColor(Colors.Gray)
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
        .Stroke(Colors.LightGray)
        .BackgroundColor(Colors.White);
    }
}
