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

class EquipmentDetailPageProps
{
    public int? EquipmentId { get; set; }
}

class EquipmentDetailPageState
{
    public int? EquipmentId { get; set; }
    public string Name { get; set; } = "";
    public EquipmentType SelectedType { get; set; } = EquipmentType.Machine;
    public string Notes { get; set; } = "";

    public bool IsSaving { get; set; }
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }
}

partial class EquipmentDetailPage : Component<EquipmentDetailPageState, EquipmentDetailPageProps>
{
    [Inject] IEquipmentService _equipmentService;
    [Inject] IFeedbackService _feedbackService;
    [Inject] IDataChangeNotifier _dataChangeNotifier;

    static readonly (EquipmentType type, string label)[] TypeChoices = new[]
    {
        (EquipmentType.Machine,    "MACHINE"),
        (EquipmentType.Grinder,    "GRINDER"),
        (EquipmentType.Tamper,     "TAMPER"),
        (EquipmentType.PuckScreen, "PUCK SCREEN"),
        (EquipmentType.Other,      "OTHER"),
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
                var updateDto = new UpdateEquipmentDto
                {
                    Name = State.Name,
                    Type = State.SelectedType,
                    Notes = string.IsNullOrWhiteSpace(State.Notes) ? null : State.Notes
                };

                await _equipmentService.UpdateEquipmentAsync(State.EquipmentId.Value, updateDto);
                _dataChangeNotifier.NotifyDataChanged(DataChangeType.EquipmentUpdated, State.EquipmentId.Value);
                await _feedbackService.ShowSuccessAsync($"'{State.Name}' updated");
            }
            else
            {
                var createDto = new CreateEquipmentDto
                {
                    Name = State.Name,
                    Type = State.SelectedType,
                    Notes = string.IsNullOrWhiteSpace(State.Notes) ? null : State.Notes
                };

                var createdEquipment = await _equipmentService.CreateEquipmentAsync(createDto);
                _dataChangeNotifier.NotifyDataChanged(DataChangeType.EquipmentCreated, createdEquipment);
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

        var popup = new SimpleActionPopup
        {
            Title = "Archive Equipment?",
            Text = $"Are you sure you want to archive '{State.Name}'? This action cannot be undone.",
            ActionButtonText = "Archive",
            SecondaryActionButtonText = "Cancel",
            ActionButtonCommand = new Command(async () =>
            {
                await _equipmentService.ArchiveEquipmentAsync(State.EquipmentId!.Value);
                _dataChangeNotifier.NotifyDataChanged(DataChangeType.EquipmentUpdated, State.EquipmentId!.Value);
                await _feedbackService.ShowSuccessAsync($"'{State.Name}' archived");
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
        return ContentPage("Equipment",
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
    static Color SurfaceVariantColor() => IsLight() ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;
    static Color TextPrimary() => IsLight() ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
    static Color TextSecondary() => IsLight() ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
    static Color AccentColor() => IsLight() ? AppColors.Light.Primary : AppColors.Dark.Primary;
    static Color DividerColor() => IsLight() ? AppColors.Light.Outline : AppColors.Dark.Outline;
    static Color ErrorColor() => AppColors.Error;

    // ------------------------------------------------------------
    // Header tile
    // ------------------------------------------------------------

    VisualNode HeaderTile()
    {
        var isEditMode = State.EquipmentId.HasValue && State.EquipmentId.Value > 0;
        var label = isEditMode ? "EDIT EQUIPMENT" : "NEW EQUIPMENT";
        var title = isEditMode
            ? (string.IsNullOrEmpty(State.Name) ? "Loading…" : State.Name)
            : "Add equipment";

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
    // Body — form fields
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

        return ScrollView(
            Grid(
                rows: "Auto,Auto,Auto,Auto,*",
                columns: "*",
                NameFieldTile().GridRow(0),
                TypePickerTile().GridRow(1),
                NotesFieldTile().GridRow(2),
                State.ErrorMessage != null
                    ? ErrorTile(State.ErrorMessage).GridRow(3)
                    : Border()
                        .BackgroundColor(SurfaceColor())
                        .StrokeThickness(0)
                        .StrokeShape(new Rectangle())
                        .MinimumHeightRequest(0)
                        .GridRow(3),
                // Bottom spacer
                Border()
                    .BackgroundColor(SurfaceColor())
                    .StrokeThickness(0)
                    .StrokeShape(new Rectangle())
                    .MinimumHeightRequest(24)
                    .VerticalOptions(LayoutOptions.Fill)
                    .GridRow(4)
            )
            .RowSpacing(1)
            .BackgroundColor(DividerColor())
        );
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
                    .Placeholder("Equipment name")
                    .PlaceholderColor(TextSecondary().WithAlpha(0.5f))
                    .TextColor(TextPrimary())
                    .FontSize(22)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .BackgroundColor(Colors.Transparent)
                    .OnTextChanged(text => SetState(s => s.Name = text))
                    .GridRow(1)
            )
            .Padding(16, 14, 16, 10)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(100);
    }

    VisualNode TypePickerTile()
    {
        return Border(
            VStack(spacing: 10,
                Label("TYPE")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary()),
                Grid(rows: "Auto,Auto", columns: "*,*,*",
                    TypeChip(TypeChoices[0].type, TypeChoices[0].label).GridRow(0).GridColumn(0),
                    TypeChip(TypeChoices[1].type, TypeChoices[1].label).GridRow(0).GridColumn(1),
                    TypeChip(TypeChoices[2].type, TypeChoices[2].label).GridRow(0).GridColumn(2),
                    TypeChip(TypeChoices[3].type, TypeChoices[3].label).GridRow(1).GridColumn(0).GridColumnSpan(1),
                    TypeChip(TypeChoices[4].type, TypeChoices[4].label).GridRow(1).GridColumn(1).GridColumnSpan(1)
                )
                .RowSpacing(8)
                .ColumnSpacing(8)
            )
            .Padding(16, 14, 16, 16)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle());
    }

    VisualNode TypeChip(EquipmentType type, string label)
    {
        var isSelected = State.SelectedType == type;
        var bg = isSelected ? TextPrimary() : SurfaceVariantColor();
        var fg = isSelected ? SurfaceColor() : TextPrimary();

        return Border(
            Label(label)
                .FontSize(11)
                .CharacterSpacing(1.5)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(fg)
                .HCenter()
                .VCenter()
        )
        .BackgroundColor(bg)
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(44)
        .Padding(8, 0)
        .OnTapped(() => SetState(s => s.SelectedType = type));
    }

    VisualNode NotesFieldTile()
    {
        return Border(
            Grid(rows: "Auto,Auto", columns: "*",
                Label("NOTES")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0),
                Editor()
                    .Text(State.Notes)
                    .Placeholder("Additional details")
                    .PlaceholderColor(TextSecondary().WithAlpha(0.5f))
                    .TextColor(TextPrimary())
                    .FontSize(16)
                    .BackgroundColor(Colors.Transparent)
                    .AutoSize(EditorAutoSizeOption.TextChanges)
                    .HeightRequest(120)
                    .OnTextChanged(text => SetState(s => s.Notes = text))
                    .GridRow(1)
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(160);
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
    // Bottom nav row — context actions: Cancel / (Delete) / Save
    // ------------------------------------------------------------

    VisualNode BottomNavRow()
    {
        var isEditMode = State.EquipmentId.HasValue && State.EquipmentId.Value > 0;

        if (isEditMode)
        {
            return Grid(rows: "Auto", columns: "*,*,*",
                ActionTile("CANCEL", inverted: false,
                    onTap: async () => await CancelAsync()).GridColumn(0),
                ActionTile("DELETE", inverted: false, danger: true,
                    onTap: async () => await ArchiveEquipmentAsync()).GridColumn(1),
                ActionTile(State.IsSaving ? "SAVING…" : "SAVE", inverted: true,
                    onTap: async () => { if (!State.IsSaving) await SaveEquipmentAsync(); }).GridColumn(2)
            )
            .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None))
            .ColumnSpacing(1)
            .BackgroundColor(DividerColor());
        }

        return Grid(rows: "Auto", columns: "*,*",
            ActionTile("CANCEL", inverted: false,
                onTap: async () => await CancelAsync()).GridColumn(0),
            ActionTile(State.IsSaving ? "SAVING…" : "ADD", inverted: true,
                onTap: async () => { if (!State.IsSaving) await SaveEquipmentAsync(); }).GridColumn(1)
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
