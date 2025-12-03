using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using MauiReactor;
using The49MauiBottomSheet = The49.Maui.BottomSheet;
using MauiControls = Microsoft.Maui.Controls;

namespace BaristaNotes.Pages;

class BeanManagementState
{
    public List<BeanDto> Beans { get; set; } = new();
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }
}

partial class BeanManagementPage : Component<BeanManagementState>
{
    [Inject]
    IBeanService _beanService;

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
            var beans = await _beanService.GetAllActiveBeansAsync();
            SetState(s =>
            {
                s.Beans = beans.ToList();
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

    async Task ShowAddBeanSheet()
    {
        await ShowBeanFormSheet(null);
    }

    async Task ShowEditBeanSheet(BeanDto bean)
    {
        await ShowBeanFormSheet(bean);
    }

    async Task ShowBeanFormSheet(BeanDto? bean)
    {
        var page = ContainerPage;
        if (page?.Window == null) return;

        // Create form fields
        var nameEntry = new MauiControls.Entry
        {
            Placeholder = "Bean name (required)",
            Text = bean?.Name ?? "",
            BackgroundColor = Colors.White
        };

        var roasterEntry = new MauiControls.Entry
        {
            Placeholder = "Roaster name",
            Text = bean?.Roaster ?? "",
            BackgroundColor = Colors.White
        };

        var originEntry = new MauiControls.Entry
        {
            Placeholder = "Country or region of origin",
            Text = bean?.Origin ?? "",
            BackgroundColor = Colors.White
        };

        var roastDatePicker = new MauiControls.DatePicker
        {
            MaximumDate = DateTime.Now,
            Date = bean?.RoastDate?.DateTime ?? DateTime.Now,
            BackgroundColor = Colors.White
        };

        var useRoastDate = new MauiControls.Switch
        {
            IsToggled = bean?.RoastDate != null
        };

        var notesEditor = new MauiControls.Editor
        {
            Placeholder = "Tasting notes, processing method, etc.",
            Text = bean?.Notes ?? "",
            HeightRequest = 80,
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
            BackgroundColor = Colors.Brown,
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
                _feedbackService.ShowError("Bean name is required", "Please enter a name for your coffee bean");
                return;
            }

            _feedbackService.ShowLoading("Saving bean...");
            saveButton.IsEnabled = false;

            try
            {
                DateTimeOffset? roastDate = useRoastDate.IsToggled && roastDatePicker.Date.HasValue
                    ? new DateTimeOffset(roastDatePicker.Date.Value)
                    : null;

                if (bean != null)
                {
                    await _beanService.UpdateBeanAsync(
                        bean.Id,
                        new UpdateBeanDto
                        {
                            Name = nameEntry.Text,
                            Roaster = string.IsNullOrWhiteSpace(roasterEntry.Text) ? null : roasterEntry.Text,
                            Origin = string.IsNullOrWhiteSpace(originEntry.Text) ? null : originEntry.Text,
                            RoastDate = roastDate,
                            Notes = string.IsNullOrWhiteSpace(notesEditor.Text) ? null : notesEditor.Text
                        });
                    
                    _feedbackService.HideLoading();
                    _feedbackService.ShowSuccess($"{nameEntry.Text} updated successfully");
                }
                else
                {
                    var result = await _beanService.CreateBeanAsync(
                        new CreateBeanDto
                        {
                            Name = nameEntry.Text,
                            Roaster = string.IsNullOrWhiteSpace(roasterEntry.Text) ? null : roasterEntry.Text,
                            Origin = string.IsNullOrWhiteSpace(originEntry.Text) ? null : originEntry.Text,
                            RoastDate = roastDate,
                            Notes = string.IsNullOrWhiteSpace(notesEditor.Text) ? null : notesEditor.Text
                        });
                    
                    _feedbackService.HideLoading();
                    
                    if (result.Success)
                    {
                        _feedbackService.ShowSuccess(result.Message);
                    }
                    else
                    {
                        _feedbackService.ShowError(result.ErrorMessage!, result.RecoveryAction);
                        saveButton.IsEnabled = true;
                        return;
                    }
                }

                await _currentSheet?.DismissAsync()!;
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _feedbackService.HideLoading();
                _feedbackService.ShowError("Failed to save bean", "Please try again");
                saveButton.IsEnabled = true;
            }
        };

        var formContent = new MauiControls.ScrollView
        {
            Content = new MauiControls.VerticalStackLayout
            {
                Spacing = 12,
                Padding = new Thickness(20),
                BackgroundColor = Colors.White,
                Children =
                {
                    new MauiControls.Label
                    {
                        Text = bean != null ? "Edit Bean" : "Add Bean",
                        FontSize = 20,
                        FontAttributes = MauiControls.FontAttributes.Bold
                    },
                    new MauiControls.Label { Text = "Name *", FontSize = 14 },
                    nameEntry,
                    new MauiControls.Label { Text = "Roaster", FontSize = 14 },
                    roasterEntry,
                    new MauiControls.Label { Text = "Origin", FontSize = 14 },
                    originEntry,
                    new MauiControls.HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            new MauiControls.Label { Text = "Track Roast Date", FontSize = 14, VerticalOptions = MauiControls.LayoutOptions.Center },
                            useRoastDate
                        }
                    },
                    roastDatePicker,
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

    async Task ShowDeleteConfirmation(BeanDto bean)
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
            await DeleteBean(bean);
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
                            Text = "Delete Bean",
                            FontSize = 20,
                            FontAttributes = MauiControls.FontAttributes.Bold,
                            TextColor = Colors.Red
                        }
                    }
                },
                new MauiControls.Label
                {
                    Text = $"\"{bean.Name}\"",
                    FontSize = 16,
                    FontAttributes = MauiControls.FontAttributes.Bold,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new MauiControls.Label
                {
                    Text = "Are you sure you want to delete this bean? Shot records using this bean will retain the historical reference.",
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

    async Task DeleteBean(BeanDto bean)
    {
        try
        {
            await _beanService.DeleteBeanAsync(bean.Id);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            SetState(s => s.ErrorMessage = ex.Message);
        }
    }

    public override VisualNode Render()
    {
        if (State.IsLoading)
        {
            return ContentPage(
                VStack(
                    ActivityIndicator()
                        .IsRunning(true)
                        .VCenter()
                        .HCenter()
                )
                .VCenter()
                .HCenter()
            ).Title("Beans");
        }

        if (!string.IsNullOrEmpty(State.ErrorMessage))
        {
            return ContentPage(
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
            ).Title("Beans");
        }

        return ContentPage(
            ToolbarItem("+ Add")
                .Order(MauiControls.ToolbarItemOrder.Primary)
                .Priority(0)
                .OnClicked(async () => await ShowAddBeanSheet()),
            Grid("Auto,*", "*",
                Label("Beans")
                    .FontSize(24)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .Padding(16, 8)
                    .GridRow(0),

                // Bean list
                State.Beans.Count == 0
                    ? RenderEmptyState().GridRow(1)
                    : CollectionView()
                        .ItemsSource(State.Beans, RenderBeanItem)
                        .Margin(16, 0)
                        .GridRow(1)
            )
        ).Title("Beans");
    }

    VisualNode RenderEmptyState()
    {
        return VStack(spacing: 12,
            Label("☕")
                .FontSize(64)
                .HCenter(),
            Label("No Beans Yet")
                .FontSize(20)
                .HCenter(),
            Label("Add your favorite coffee beans to track freshness and tasting notes")
                .FontSize(16)
                .TextColor(Colors.Gray)
                .HCenter()
        )
        .VCenter()
        .HCenter()
        .Padding(24);
    }

    VisualNode RenderBeanItem(BeanDto bean)
    {
        return Border(
            Grid("Auto", "*,Auto",
                VStack(spacing: 4,
                    Label(bean.Name)
                        .FontSize(18)
                        .FontAttributes(MauiControls.FontAttributes.Bold),
                    bean.Roaster != null
                        ? Label($"🏭 {bean.Roaster}")
                            .FontSize(14)
                            .TextColor(Colors.Gray)
                        : null,
                    bean.Origin != null
                        ? Label($"🌍 {bean.Origin}")
                            .FontSize(14)
                            .TextColor(Colors.Gray)
                        : null,
                    bean.RoastDate.HasValue
                        ? Label($"📅 Roasted: {bean.RoastDate.Value:MMM d, yyyy}")
                            .FontSize(12)
                            .TextColor(Colors.DarkGray)
                        : null
                )
                .GridColumn(0)
                .VCenter(),

                // Action buttons
                HStack(spacing: 8,
                    Button("✏️")
                        .BackgroundColor(Colors.Transparent)
                        .OnClicked(async () => await ShowEditBeanSheet(bean)),
                    Button("🗑️")
                        .BackgroundColor(Colors.Transparent)
                        .OnClicked(async () => await ShowDeleteConfirmation(bean))
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
