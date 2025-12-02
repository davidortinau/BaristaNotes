using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using MauiReactor;

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
                        .HCenter()
                )
                .VCenter()
                .HCenter()
                .Spacing(16)
            );
        }

        return ContentPage("Profiles",
            VStack(spacing: 16,
                Label("User Profiles")
                    .FontSize(24)
                    .HCenter(),
                Label("Coming soon: Add and manage user profiles")
                    .HCenter(),
                CollectionView()
                    .ItemsSource(State.Profiles, RenderProfileItem)
            )
            .Padding(16)
        );
    }

    VisualNode RenderProfileItem(UserProfileDto profile)
    {
        return Border(
            VStack(spacing: 8,
                Label(profile.Name)
                    .FontSize(18)
                    .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold),
                Label($"Created: {profile.CreatedAt:d}")
                    .FontSize(12)
            )
            .Padding(12)
        )
        .Margin(0, 4)
        .StrokeThickness(1)
        .Stroke(new SolidColorBrush(Colors.Gray));
    }
}
