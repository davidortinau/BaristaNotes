using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using Fonts;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;

namespace BaristaNotes.Pages;

class EquipmentManagementState
{
    public List<EquipmentDto> Equipment { get; set; } = new();
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }
}

partial class EquipmentManagementPage : Component<EquipmentManagementState>
{
    [Inject]
    IEquipmentService _equipmentService;

    [Inject]
    IFeedbackService _feedbackService;

    protected override void OnMounted()
    {
        base.OnMounted();
        SetState(s => s.IsLoading = true);
        _ = LoadDataAsync();
    }

    protected override void OnPropsChanged()
    {
        base.OnPropsChanged();
        _ = LoadDataAsync();
    }

    void OnPageAppearing()
    {
        // Reload data when returning from detail page
        _ = LoadDataAsync();
    }

    async Task LoadDataAsync()
    {
        try
        {
            var equipment = await _equipmentService.GetAllActiveEquipmentAsync();
            SetState(s =>
            {
                s.Equipment = equipment.ToList();
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

    async Task NavigateToAddEquipment()
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync("equipment-detail");
    }

    async void NavigateToEditEquipment(EquipmentDto equipment)
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync<EquipmentDetailPageProps>("equipment-detail", props =>
        {
            props.EquipmentId = equipment.Id;
        });
    }

    async Task ShowArchiveConfirmation(EquipmentDto equipment)
    {
        var popup = new SimpleActionPopup
        {
            Title = $"Archive \"{equipment.Name}\"?",
            Text = "It will no longer appear in your equipment list.",
            ActionButtonText = "Archive",
            SecondaryActionButtonText = "Cancel",
            ActionButtonCommand = new Command(async () =>
            {
                await ArchiveEquipment(equipment);
                await IPopupService.Current.PopAsync();
            })
        };

        await IPopupService.Current.PushAsync(popup);
    }

    async Task ArchiveEquipment(EquipmentDto equipment)
    {
        try
        {
            await _equipmentService.ArchiveEquipmentAsync(equipment.Id);
            await _feedbackService.ShowSuccessAsync($"{equipment.Name} archived");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await _feedbackService.ShowErrorAsync("Failed to archive equipment", "Please try again");
            SetState(s => s.ErrorMessage = ex.Message);
        }
    }

    public override VisualNode Render()
    {
        if (State.IsLoading)
        {
            return ContentPage("Equipment",
                VStack(
                    ActivityIndicator()
                        .IsRunning(true)
                        .VCenter()
                        .HCenter()
                )
                .VCenter()
                .HCenter()
            )
            .OnAppearing(() => OnPageAppearing());
        }

        if (!string.IsNullOrEmpty(State.ErrorMessage))
        {
            return ContentPage("Equipment",
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
            )
            .OnAppearing(() => OnPageAppearing());
        }

        return ContentPage("Equipment",
            ToolbarItem("+ Add")
                .Order(MauiControls.ToolbarItemOrder.Primary)
                .Priority(0)
                .OnClicked(async () => await NavigateToAddEquipment()),
            State.Equipment.Count == 0
                ? RenderEmptyState()
                : CollectionView()
                    .ItemsSource(State.Equipment, RenderEquipmentItem)
                    .Margin(16, 16, 16, 32)
        )
        .OnAppearing(() => OnPageAppearing());
    }

    VisualNode RenderEmptyState()
    {
        return VStack(spacing: 12,
            Image()
                .Source(AppIcons.EspressoMachine)
                .HCenter(),
            Label("No Equipment Yet")
                .ThemeKey(ThemeKeys.CardTitle)
                .HCenter(),
            Label("Add your coffee machines, grinders, and accessories")
                .ThemeKey(ThemeKeys.CardSubtitle)
                .HCenter()
        )
        .VCenter()
        .HCenter()
        .Padding(24);
    }

    VisualNode RenderEquipmentItem(EquipmentDto equipment)
    {
        return SwipeView(
            Border(
                VStack(spacing: 4,
                    Label(equipment.Name)
                        .ThemeKey(ThemeKeys.CardTitle),
                    Label(equipment.Type.ToString())
                        .ThemeKey(ThemeKeys.CardSubtitle)
                )
                .Padding(12)
            )
            .ThemeKey(ThemeKeys.CardBorder)
            .OnTapped(() => NavigateToEditEquipment(equipment))
        )
        .LeftItems(
        [
            SwipeItem()
                .BackgroundColor(Colors.Transparent)
                .IconImageSource(AppIcons.Delete)
                .OnInvoked(async () => await ShowArchiveConfirmation(equipment))
        ])
        .Margin(0, 4);
    }
}
