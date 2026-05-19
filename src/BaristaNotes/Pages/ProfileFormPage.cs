using BaristaNotes.Components;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using Fonts;
using MauiReactor;
using MauiReactor.Shapes;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;
using Application = Microsoft.Maui.Controls.Application;

namespace BaristaNotes.Pages;

class ProfileFormPageState
{
    public int? ProfileId { get; set; }
    public string Name { get; set; } = "";
    public bool IsSaving { get; set; }
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }
}

class ProfileFormPageProps
{
    public int? ProfileId { get; set; }
}

partial class ProfileFormPage : Component<ProfileFormPageState, ProfileFormPageProps>
{
    [Inject] IUserProfileService _profileService;
    [Inject] IImagePickerService _imagePickerService;
    [Inject] IFeedbackService _feedbackService;
    [Inject] IDataChangeNotifier _dataChangeNotifier;

    protected override void OnMounted()
    {
        base.OnMounted();

        if (Props.ProfileId.HasValue && Props.ProfileId.Value > 0)
        {
            SetState(s =>
            {
                s.ProfileId = Props.ProfileId;
                s.IsLoading = true;
            });
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
                s.IsLoading = false;
            });
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = $"Failed to load profile: {ex.Message}";
            });
        }
    }

    async Task SaveProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(State.Name))
        {
            SetState(s => s.ErrorMessage = "Profile name is required");
            return;
        }

        SetState(s =>
        {
            s.IsSaving = true;
            s.ErrorMessage = null;
        });

        try
        {
            if (State.ProfileId.HasValue && State.ProfileId.Value > 0)
            {
                await _profileService.UpdateProfileAsync(
                    State.ProfileId.Value,
                    new UpdateUserProfileDto { Name = State.Name });

                _dataChangeNotifier.NotifyDataChanged(DataChangeType.ProfileUpdated, State.ProfileId.Value);
                await _feedbackService.ShowSuccessAsync($"Profile '{State.Name}' updated");
            }
            else
            {
                var created = await _profileService.CreateProfileAsync(
                    new CreateUserProfileDto { Name = State.Name });

                _dataChangeNotifier.NotifyDataChanged(DataChangeType.ProfileCreated, created);
                await _feedbackService.ShowSuccessAsync($"Profile '{State.Name}' created");
            }

            await Microsoft.Maui.Controls.Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsSaving = false;
                s.ErrorMessage = $"Failed to save: {ex.Message}";
            });
        }
    }

    async Task DeleteProfileAsync()
    {
        if (!State.ProfileId.HasValue || State.ProfileId.Value <= 0) return;

        var popup = new SimpleActionPopup
        {
            Title = "Delete Profile?",
            Text = $"Are you sure you want to delete '{State.Name}'? This action cannot be undone.",
            ActionButtonText = "Delete",
            SecondaryActionButtonText = "Cancel",
            ActionButtonCommand = new Command(async () =>
            {
                await _profileService.DeleteProfileAsync(State.ProfileId!.Value);
                _dataChangeNotifier.NotifyDataChanged(DataChangeType.ProfileUpdated, State.ProfileId!.Value);
                await _feedbackService.ShowSuccessAsync($"Profile '{State.Name}' deleted");
                await IPopupService.Current.PopAsync();
                await MauiControls.Shell.Current.GoToAsync("..");
            })
        };

        await IPopupService.Current.PushAsync(popup);
    }

    async Task CancelAsync()
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync("..");
    }

    // ============================================================
    // Rendering
    // ============================================================

    public override VisualNode Render()
    {
        return ContentPage("Profile",
            Grid(rows: "Auto,*,Auto", columns: "*",
                HeaderTile().GridRow(0),
                RenderBody().GridRow(1),
                BottomNavRow().GridRow(2)
            )
            .RowSpacing(1)
            .BackgroundColor(DividerColor())
            .Padding(1)
            .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None))
        )
        .Set(MauiControls.Shell.NavBarIsVisibleProperty, false)
        .Set(MauiControls.Shell.TabBarIsVisibleProperty, false)
        .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Never));
    }

    // ------------------------------------------------------------
    // Theme helpers
    // ------------------------------------------------------------

    static bool IsLight() => Application.Current?.RequestedTheme != AppTheme.Dark;
    static Color SurfaceColor() => IsLight() ? AppColors.Light.Surface : AppColors.Dark.Surface;
    static Color TextPrimary() => IsLight() ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
    static Color TextSecondary() => IsLight() ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
    static Color DividerColor() => IsLight() ? AppColors.Light.Outline : AppColors.Dark.Outline;
    static Color ErrorColor() => AppColors.Error;

    // ------------------------------------------------------------
    // Header tile
    // ------------------------------------------------------------

    VisualNode HeaderTile()
    {
        var isEditMode = State.ProfileId.HasValue && State.ProfileId.Value > 0;
        var label = isEditMode ? "EDIT PROFILE" : "NEW PROFILE";
        var title = isEditMode
            ? (string.IsNullOrEmpty(State.Name) ? "Loading…" : State.Name)
            : "Add profile";

        var len = title.Length;
        double valueFontSize = len switch
        {
            <= 12 => 28,
            <= 20 => 22,
            <= 28 => 18,
            _ => 16
        };

        return Border(
            Grid(rows: "Auto,*", columns: "*",
                Label(label)
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0),
                Label(title)
                    .FontSize(valueFontSize)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .LineBreakMode(LineBreakMode.WordWrap)
                    .MaxLines(2)
                    .VEnd()
                    .GridRow(1)
            )
            .Padding(16, 56, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(120);
    }

    // ------------------------------------------------------------
    // Body
    // ------------------------------------------------------------

    VisualNode RenderBody()
    {
        if (State.IsLoading)
        {
            return Border(
                ActivityIndicator()
                    .IsRunning(true)
                    .VCenter()
                    .HCenter()
            )
            .BackgroundColor(SurfaceColor())
            .StrokeThickness(0)
            .StrokeShape(new Rectangle());
        }

        var isEditMode = State.ProfileId.HasValue && State.ProfileId.Value > 0;

        return ScrollView(
            Grid(
                rows: "Auto,Auto,Auto,*",
                columns: "*",
                NameFieldTile().GridRow(0),
                PhotoTile(isEditMode).GridRow(1),
                State.ErrorMessage != null
                    ? ErrorTile(State.ErrorMessage).GridRow(2)
                    : Border()
                        .BackgroundColor(SurfaceColor())
                        .StrokeThickness(0)
                        .StrokeShape(new Rectangle())
                        .MinimumHeightRequest(0)
                        .GridRow(2),
                Border()
                    .BackgroundColor(SurfaceColor())
                    .StrokeThickness(0)
                    .StrokeShape(new Rectangle())
                    .MinimumHeightRequest(24)
                    .VerticalOptions(LayoutOptions.Fill)
                    .GridRow(3)
            )
            .RowSpacing(1)
            .BackgroundColor(DividerColor())
        )
        .BackgroundColor(SurfaceColor());
    }

    VisualNode NameFieldTile()
    {
        return Border(
            Grid(rows: "Auto,Auto", columns: "*",
                Label("NAME")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0),
                Entry()
                    .Text(State.Name)
                    .Placeholder("Profile name")
                    .PlaceholderColor(TextSecondary().WithAlpha(0.5f))
                    .TextColor(TextPrimary())
                    .FontSize(22)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .BackgroundColor(Colors.Transparent)
                    .OnTextChanged(text => SetState(s => s.Name = text))
                    .AutomationId("ProfileNameEntry")
                    .GridRow(1)
            )
            .Padding(16, 14, 16, 10)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(100);
    }

    VisualNode PhotoTile(bool isEditMode)
    {
        return Border(
            VStack(spacing: 10,
                Label("PHOTO")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary()),
                isEditMode && State.ProfileId.HasValue
                    ? (VisualNode)new ProfileImagePicker(
                            State.ProfileId.Value,
                            _imagePickerService,
                            _profileService)
                    : Label("Save the profile first to add a photo")
                        .FontSize(14)
                        .TextColor(TextSecondary())
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle());
    }

    VisualNode ErrorTile(string message)
    {
        return Border(
            Grid(rows: "Auto,Auto", columns: "*",
                Label("ERROR")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(SurfaceColor().WithAlpha(0.8f))
                    .GridRow(0),
                Label(message)
                    .FontSize(16)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(SurfaceColor())
                    .GridRow(1)
            )
            .Padding(16, 12, 16, 12)
        )
        .BackgroundColor(ErrorColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(60);
    }

    // ------------------------------------------------------------
    // Bottom nav row
    // ------------------------------------------------------------

    VisualNode BottomNavRow()
    {
        var isEditMode = State.ProfileId.HasValue && State.ProfileId.Value > 0;

        if (isEditMode)
        {
            return Grid(rows: "Auto", columns: "*,*,*",
                ActionTile("CANCEL", inverted: false,
                    onTap: async () => await CancelAsync()).GridColumn(0),
                ActionTile("DELETE", inverted: false, danger: true,
                    onTap: async () => await DeleteProfileAsync()).GridColumn(1),
                ActionTile(State.IsSaving ? "SAVING…" : "SAVE", inverted: true,
                    onTap: async () => { if (!State.IsSaving) await SaveProfileAsync(); }).GridColumn(2)
            )
            .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None))
            .ColumnSpacing(1)
            .BackgroundColor(DividerColor());
        }

        return Grid(rows: "Auto", columns: "*,*",
            ActionTile("CANCEL", inverted: false,
                onTap: async () => await CancelAsync()).GridColumn(0),
            ActionTile(State.IsSaving ? "SAVING…" : "ADD", inverted: true,
                onTap: async () => { if (!State.IsSaving) await SaveProfileAsync(); }).GridColumn(1)
        )
        .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None))
        .ColumnSpacing(1)
        .BackgroundColor(DividerColor());
    }

    VisualNode ActionTile(string label, bool inverted, Action onTap, bool danger = false)
    {
        Color bg;
        Color fg;
        if (danger)
        {
            bg = ErrorColor();
            fg = SurfaceColor();
        }
        else if (inverted)
        {
            bg = TextPrimary();
            fg = SurfaceColor();
        }
        else
        {
            bg = SurfaceColor();
            fg = TextPrimary();
        }

        return Border(
            Label(label)
                .FontSize(13)
                .CharacterSpacing(2)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(fg)
                .HCenter()
                .VCenter()
        )
        .BackgroundColor(bg)
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(72)
        .Padding(8, 18, 8, 30)
        .OnTapped(onTap);
    }
}
