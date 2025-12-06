using BaristaNotes.Components.Forms;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using BaristaNotes.Utilities;
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

    async Task ShowAddEquipmentSheet()
    {
        await ShowEquipmentFormSheet(null);
    }

    async Task ShowEditEquipmentSheet(EquipmentDto equipment)
    {
        await ShowEquipmentFormSheet(equipment);
    }

    async Task ShowEquipmentFormSheet(EquipmentDto? equipment)
    {
        await BottomSheetManager.ShowAsync(
            () => new EquipmentFormComponent(
                equipment,
                _equipmentService,
                _feedbackService,
                () => _ = LoadDataAsync()),
            sheet => sheet.HasBackdrop = true);
    }

    async Task ShowDeleteConfirmation(EquipmentDto equipment)
    {
        var popup = new SimpleActionPopup
        {
            Title = $"Delete \"{equipment.Name}\"?",
            Text = "This action cannot be undone.",
            ActionButtonText = "Delete",
            SecondaryActionButtonText = "Cancel",
            ActionButtonCommand = new Command(async () =>
            {
                // Delete logic here
                await DeleteEquipment(equipment);
                await IPopupService.Current.PopAsync();
            })
        };

        await IPopupService.Current.PushAsync(popup);
    }

    async Task DeleteEquipment(EquipmentDto equipment)
    {
        try
        {
            await _equipmentService.DeleteEquipmentAsync(equipment.Id);

            await _feedbackService.ShowSuccessAsync($"{equipment.Name} deleted successfully");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {

            await _feedbackService.ShowErrorAsync("Failed to delete equipment", "Please try again");
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
            );
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
            );
        }

        return ContentPage("Equipment",
            ToolbarItem()
                .IconImageSource(AppIcons.Add)
                .Order(MauiControls.ToolbarItemOrder.Primary)
                .Priority(0)
                .OnClicked(async () => await ShowAddEquipmentSheet()),
            Grid("Auto,*", "*",
                // Header with Add button
                Label("Equipment")
                    .FontSize(24)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .Padding(16, 8)
                    .GridRow(0),

                // Equipment list
                State.Equipment.Count == 0
                    ? RenderEmptyState().GridRow(1)
                    : CollectionView()
                        .ItemsSource(State.Equipment, RenderEquipmentItem)
                        .Margin(16, 0)
                        .GridRow(1)
            )
        );
    }

    VisualNode RenderEmptyState()
    {
        return VStack(spacing: 12,
            Label("🛠️")
                .FontSize(64)
                .HCenter(),
            Label("No Equipment Yet")
                .FontSize(20)
                .HCenter(),
            Label("Add your coffee machines, grinders, and accessories")
                .FontSize(16)
                .TextColor(Colors.Gray)
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
            .OnTapped(async () => await ShowEditEquipmentSheet(equipment))
        )
        .LeftItems(
        [
            SwipeItem()
                .BackgroundColor(Colors.Transparent)
                .IconImageSource(AppIcons.Delete)
                .OnInvoked(async () => await ShowDeleteConfirmation(equipment))
        ])
        .Margin(0, 4);
    }
}
