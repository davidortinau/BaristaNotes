using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using BaristaNotes.Components.FormFields;
using MauiReactor;

namespace BaristaNotes.Pages;

class EquipmentDetailPageProps
{
    public int? EquipmentId { get; set; }
}

class EquipmentDetailPageState
{
    // Form fields
    public int? EquipmentId { get; set; }
    public string Name { get; set; } = "";
    public EquipmentType SelectedType { get; set; } = EquipmentType.Machine;
    public int SelectedTypeIndex { get; set; } = 0;
    public string Notes { get; set; } = "";

    // Form state
    public bool IsSaving { get; set; }
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }

    // Type options
    public List<string> TypeOptions { get; set; } = new()
    {
        "Machine",
        "Grinder",
        "Tamper",
        "Puck Screen",
        "Other"
    };
}

partial class EquipmentDetailPage : Component<EquipmentDetailPageState, EquipmentDetailPageProps>
{
    [Inject] IEquipmentService _equipmentService;
    [Inject] IFeedbackService _feedbackService;

    // Map display names to enum values
    private static readonly Dictionary<int, EquipmentType> IndexToType = new()
    {
        { 0, EquipmentType.Machine },
        { 1, EquipmentType.Grinder },
        { 2, EquipmentType.Tamper },
        { 3, EquipmentType.PuckScreen },
        { 4, EquipmentType.Other }
    };

    private static readonly Dictionary<EquipmentType, int> TypeToIndex = new()
    {
        { EquipmentType.Machine, 0 },
        { EquipmentType.Grinder, 1 },
        { EquipmentType.Tamper, 2 },
        { EquipmentType.PuckScreen, 3 },
        { EquipmentType.Other, 4 }
    };

    protected override void OnMounted()
    {
        base.OnMounted();

        if (Props.EquipmentId.HasValue && Props.EquipmentId.Value > 0)
        {
            SetState(s =>
            {
                s.EquipmentId = Props.EquipmentId;
                s.IsLoading = true;
            });
            _ = LoadEquipmentAsync();
        }
    }

    async Task LoadEquipmentAsync()
    {
        if (!State.EquipmentId.HasValue || State.EquipmentId.Value <= 0) return;

        try
        {
            var equipment = await _equipmentService.GetEquipmentByIdAsync(State.EquipmentId.Value);

            if (equipment == null)
            {
                SetState(s =>
                {
                    s.IsLoading = false;
                    s.ErrorMessage = "Equipment not found";
                });
                return;
            }

            SetState(s =>
            {
                s.Name = equipment.Name;
                s.SelectedType = equipment.Type;
                s.SelectedTypeIndex = TypeToIndex.GetValueOrDefault(equipment.Type, 0);
                s.Notes = equipment.Notes ?? "";
                s.IsLoading = false;
            });
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = $"Failed to load equipment: {ex.Message}";
            });
        }
    }

    bool ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(State.Name))
        {
            SetState(s => s.ErrorMessage = "Equipment name is required");
            return false;
        }

        SetState(s => s.ErrorMessage = null);
        return true;
    }

    async Task SaveEquipmentAsync()
    {
        if (!ValidateForm()) return;

        SetState(s =>
        {
            s.IsSaving = true;
            s.ErrorMessage = null;
        });

        try
        {
            if (State.EquipmentId.HasValue && State.EquipmentId.Value > 0)
            {
                // Update existing
                var updateDto = new UpdateEquipmentDto
                {
                    Name = State.Name,
                    Type = State.SelectedType,
                    Notes = string.IsNullOrWhiteSpace(State.Notes) ? null : State.Notes
                };

                await _equipmentService.UpdateEquipmentAsync(State.EquipmentId.Value, updateDto);
                await _feedbackService.ShowSuccessAsync($"'{State.Name}' updated");
            }
            else
            {
                // Create new
                var createDto = new CreateEquipmentDto
                {
                    Name = State.Name,
                    Type = State.SelectedType,
                    Notes = string.IsNullOrWhiteSpace(State.Notes) ? null : State.Notes
                };

                await _equipmentService.CreateEquipmentAsync(createDto);
                await _feedbackService.ShowSuccessAsync($"'{State.Name}' created");
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

    async Task ArchiveEquipmentAsync()
    {
        if (!State.EquipmentId.HasValue || State.EquipmentId.Value <= 0) return;

        if (Application.Current?.MainPage == null) return;

        var confirmed = await Application.Current.MainPage.DisplayAlert(
            "Archive Equipment",
            $"Are you sure you want to archive '{State.Name}'? It will no longer appear in your equipment list.",
            "Archive",
            "Cancel");

        if (!confirmed) return;

        try
        {
            await _equipmentService.ArchiveEquipmentAsync(State.EquipmentId.Value);
            await _feedbackService.ShowSuccessAsync($"'{State.Name}' archived");
            await Microsoft.Maui.Controls.Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            SetState(s => s.ErrorMessage = $"Failed to archive: {ex.Message}");
        }
    }

    public override VisualNode Render()
    {
        var isEditMode = State.EquipmentId.HasValue && State.EquipmentId.Value > 0;
        var title = isEditMode
            ? (string.IsNullOrEmpty(State.Name) ? "Edit Equipment" : $"Edit {State.Name}")
            : "Add Equipment";

        if (State.IsLoading)
        {
            return ContentPage(
                VStack(
                    ActivityIndicator().IsRunning(true)
                )
                .VCenter()
                .HCenter()
            ).Title(title);
        }

        return ContentPage(
            isEditMode ?
                ToolbarItem()
                    .Text("Delete")
                    .IconImageSource(AppIcons.Delete)
                    .Order(Microsoft.Maui.Controls.ToolbarItemOrder.Secondary)
                    .OnClicked(async () => await ArchiveEquipmentAsync())
                    : null,
            ScrollView(
                VStack(spacing: 16,
                    RenderForm()
                )
                .Padding(16)
            )
        ).Title(title);
    }

    VisualNode RenderForm()
    {
        var isEditMode = State.EquipmentId.HasValue && State.EquipmentId.Value > 0;

        return VStack(spacing: 16,
            // Name field
            new FormEntryField()
                .Label("Name *")
                .Placeholder("Equipment name (required)")
                .Text(State.Name)
                .OnTextChanged(text => SetState(s => s.Name = text)),

            // Type picker
            new FormPickerField()
                .Label("Type")
                .Title("Select Type")
                .ItemsSource(State.TypeOptions)
                .SelectedIndex(State.SelectedTypeIndex)
                .OnSelectedIndexChanged(idx =>
                {
                    if (idx >= 0 && idx < State.TypeOptions.Count)
                    {
                        SetState(s =>
                        {
                            s.SelectedTypeIndex = idx;
                            s.SelectedType = IndexToType.GetValueOrDefault(idx, EquipmentType.Machine);
                        });
                    }
                }),

            // Notes field
            new FormEditorField()
                .Label("Notes")
                .Placeholder("Additional details about this equipment")
                .Text(State.Notes)
                .HeightRequest(100)
                .OnTextChanged(text => SetState(s => s.Notes = text)),

            // Error message
            State.ErrorMessage != null
                ? Border(
                    Label(State.ErrorMessage).TextColor(Colors.Red).Padding(12)
                )
                .BackgroundColor(Colors.Red.WithAlpha(0.1f))
                .StrokeThickness(1)
                .Stroke(Colors.Red)
                : null,

            Button(State.IsSaving ? "Saving..." : (isEditMode ? "Save Changes" : "Add Equipment"))
                .OnClicked(async () => await SaveEquipmentAsync())
                .IsEnabled(!State.IsSaving)

        );
    }
}
