using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using BaristaNotes.Utilities;

namespace BaristaNotes.Components.Forms;

class EquipmentFormState
{
    public string Name { get; set; } = string.Empty;
    public EquipmentType SelectedType { get; set; } = EquipmentType.Machine;
    public string Notes { get; set; } = string.Empty;
    public bool IsSaving { get; set; }
    public int SelectedTypeIndex { get; set; } = 0;
}

partial class EquipmentFormComponent : Component<EquipmentFormState>
{
    private readonly EquipmentDto? _equipment;
    private readonly IEquipmentService _equipmentService;
    private readonly IFeedbackService _feedbackService;
    private readonly Action _onSaved;

    public EquipmentFormComponent(
        EquipmentDto? equipment,
        IEquipmentService equipmentService,
        IFeedbackService feedbackService,
        Action onSaved)
    {
        _equipment = equipment;
        _equipmentService = equipmentService;
        _feedbackService = feedbackService;
        _onSaved = onSaved;
    }

    protected override void OnMounted()
    {
        base.OnMounted();
        
        if (_equipment != null)
        {
            var types = Enum.GetValues<EquipmentType>().ToList();
            SetState(s =>
            {
                s.Name = _equipment.Name;
                s.SelectedType = _equipment.Type;
                s.SelectedTypeIndex = types.IndexOf(_equipment.Type);
                s.Notes = _equipment.Notes ?? string.Empty;
            });
        }
    }

    async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(State.Name))
        {
            await _feedbackService.ShowErrorAsync("Equipment name is required", "Please enter a name for your equipment");
            return;
        }

        SetState(s => s.IsSaving = true);

        try
        {
            if (_equipment != null)
            {
                await _equipmentService.UpdateEquipmentAsync(
                    _equipment.Id,
                    new UpdateEquipmentDto
                    {
                        Name = State.Name,
                        Type = State.SelectedType,
                        Notes = string.IsNullOrWhiteSpace(State.Notes) ? null : State.Notes
                    });

                await _feedbackService.ShowSuccessAsync($"{State.Name} updated successfully");
            }
            else
            {
                await _equipmentService.CreateEquipmentAsync(
                    new CreateEquipmentDto
                    {
                        Name = State.Name,
                        Type = State.SelectedType,
                        Notes = string.IsNullOrWhiteSpace(State.Notes) ? null : State.Notes
                    });

                await _feedbackService.ShowSuccessAsync($"{State.Name} added successfully");
            }

            await BottomSheetManager.DismissAsync();
            _onSaved();
        }
        catch (Exception)
        {
            await _feedbackService.ShowErrorAsync("Failed to save equipment", "Please try again");
            SetState(s => s.IsSaving = false);
        }
    }

    async Task CancelAsync()
    {
        await BottomSheetManager.DismissAsync();
    }

    public override VisualNode Render()
    {
        var equipmentTypes = Enum.GetValues<EquipmentType>().ToList();
        
        return VStack(spacing: 16,
            Label(_equipment != null ? "Edit Equipment" : "Add Equipment")
                .ThemeKey(ThemeKeys.FormTitle),

            Label("Name *")
                .ThemeKey(ThemeKeys.FormLabel),
            Entry()
                .Text(State.Name)
                .OnTextChanged(text => SetState(s => s.Name = text))
                .Placeholder("Equipment name")
                .ThemeKey(ThemeKeys.Entry),

            Label("Type *")
                .ThemeKey(ThemeKeys.FormLabel),
            Picker()
                .ItemsSource(equipmentTypes.Cast<object>().ToList())
                .SelectedIndex(State.SelectedTypeIndex)
                .OnSelectedIndexChanged(index => SetState(s =>
                {
                    s.SelectedTypeIndex = index;
                    s.SelectedType = equipmentTypes[index];
                }))
                .ThemeKey(ThemeKeys.Entry),

            Label("Notes")
                .ThemeKey(ThemeKeys.FormLabel),
            Editor()
                .Text(State.Notes)
                .OnTextChanged(text => SetState(s => s.Notes = text))
                .Placeholder("Optional notes")
                .HeightRequest(100)
                .ThemeKey(ThemeKeys.Entry),

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
}
