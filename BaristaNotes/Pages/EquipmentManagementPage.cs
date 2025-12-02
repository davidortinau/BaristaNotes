using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using MauiReactor;

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
                        .HCenter()
                )
                .VCenter()
                .HCenter()
                .Spacing(16)
            );
        }

        return ContentPage("Equipment",
            VStack(spacing: 16,
                Label("Equipment Management")
                    .FontSize(24)
                    .HCenter(),
                Label("Coming soon: Add, edit, and manage your equipment")
                    .HCenter(),
                CollectionView()
                    .ItemsSource(State.Equipment, RenderEquipmentItem)
            )
            .Padding(16)
        );
    }

    VisualNode RenderEquipmentItem(EquipmentDto equipment)
    {
        return Border(
            VStack(spacing: 8,
                Label(equipment.Name)
                    .FontSize(18)
                    .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold),
                Label($"Type: {equipment.Type}"),
                equipment.Notes != null 
                    ? Label(equipment.Notes)
                        .FontSize(12)
                    : null
            )
            .Padding(12)
        )
        .Margin(0, 4)
        .StrokeThickness(1)
        .Stroke(new SolidColorBrush(Colors.Gray));
    }
}
