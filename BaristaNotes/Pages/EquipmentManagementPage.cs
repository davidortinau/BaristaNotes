using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using MauiReactor;
using The49MauiBottomSheet = The49.Maui.BottomSheet;
using MauiControls = Microsoft.Maui.Controls;

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
        var page = ContainerPage;
        if (page?.Window == null) return;

        // Create form fields
        var nameEntry = new MauiControls.Entry
        {
            Placeholder = "Equipment name",
            Text = equipment?.Name ?? "",
            BackgroundColor = Colors.White
        };

        var typePicker = new MauiControls.Picker
        {
            Title = "Select type"
        };
        var equipmentTypes = Enum.GetValues<EquipmentType>().ToList();
        foreach (var type in equipmentTypes)
        {
            typePicker.Items.Add(type.ToString());
        }
        // Find the index of the current type in the list (not the enum value)
        typePicker.SelectedIndex = equipment != null ? equipmentTypes.IndexOf(equipment.Type) : 0;

        var notesEditor = new MauiControls.Editor
        {
            Placeholder = "Optional notes",
            Text = equipment?.Notes ?? "",
            HeightRequest = 100,
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
            BackgroundColor = Colors.Blue,
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
                _feedbackService.ShowError("Equipment name is required", "Please enter a name for your equipment");
                return;
            }

            _feedbackService.ShowLoading("Saving equipment...");
            saveButton.IsEnabled = false;

            try
            {
                // Get the actual enum value from the list, not by casting the index
                var selectedType = equipmentTypes[typePicker.SelectedIndex];

                if (equipment != null)
                {
                    await _equipmentService.UpdateEquipmentAsync(
                        equipment.Id,
                        new UpdateEquipmentDto
                        {
                            Name = nameEntry.Text,
                            Type = selectedType,
                            Notes = string.IsNullOrWhiteSpace(notesEditor.Text) ? null : notesEditor.Text
                        });
                    
                    _feedbackService.HideLoading();
                    _feedbackService.ShowSuccess($"{nameEntry.Text} updated successfully");
                }
                else
                {
                    await _equipmentService.CreateEquipmentAsync(
                        new CreateEquipmentDto
                        {
                            Name = nameEntry.Text,
                            Type = selectedType,
                            Notes = string.IsNullOrWhiteSpace(notesEditor.Text) ? null : notesEditor.Text
                        });
                    
                    _feedbackService.HideLoading();
                    _feedbackService.ShowSuccess($"{nameEntry.Text} added successfully");
                }

                await _currentSheet?.DismissAsync()!;
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _feedbackService.HideLoading();
                _feedbackService.ShowError("Failed to save equipment", "Please try again");
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
                    Text = equipment != null ? "Edit Equipment" : "Add Equipment",
                    FontSize = 20,
                    FontAttributes = MauiControls.FontAttributes.Bold
                },
                new MauiControls.Label { Text = "Name *", FontSize = 14 },
                nameEntry,
                new MauiControls.Label { Text = "Type *", FontSize = 14 },
                typePicker,
                new MauiControls.Label { Text = "Notes", FontSize = 14 },
                notesEditor,
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

    async Task ShowDeleteConfirmation(EquipmentDto equipment)
    {
        var page = ContainerPage;
        if (page?.Window == null) return;

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
            await DeleteEquipment(equipment);
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
                            Text = "Delete Equipment",
                            FontSize = 20,
                            FontAttributes = MauiControls.FontAttributes.Bold,
                            TextColor = Colors.Red
                        }
                    }
                },
                new MauiControls.Label
                {
                    Text = $"\"{equipment.Name}\"",
                    FontSize = 16,
                    FontAttributes = MauiControls.FontAttributes.Bold,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new MauiControls.Label
                {
                    Text = "Are you sure you want to delete this equipment? This action cannot be undone.",
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

    async Task DeleteEquipment(EquipmentDto equipment)
    {
        _feedbackService.ShowLoading("Deleting equipment...");
        
        try
        {
            await _equipmentService.DeleteEquipmentAsync(equipment.Id);
            _feedbackService.HideLoading();
            _feedbackService.ShowSuccess($"{equipment.Name} deleted successfully");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _feedbackService.HideLoading();
            _feedbackService.ShowError("Failed to delete equipment", "Please try again");
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
            ToolbarItem("+ Add")
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
        return Border(
            Grid("Auto", "*,Auto",
                VStack(spacing: 4,
                    Label(equipment.Name)
                        .FontSize(18)
                        .FontAttributes(MauiControls.FontAttributes.Bold),
                    Label(equipment.Type.ToString())
                        .FontSize(14)
                        .TextColor(Colors.Gray)
                )
                .GridColumn(0)
                .VCenter(),

                // Action buttons
                HStack(spacing: 8,
                    Button("✏️")
                        .BackgroundColor(Colors.Transparent)
                        .OnClicked(async () => await ShowEditEquipmentSheet(equipment)),
                    Button("🗑️")
                        .BackgroundColor(Colors.Transparent)
                        .OnClicked(async () => await ShowDeleteConfirmation(equipment))
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
