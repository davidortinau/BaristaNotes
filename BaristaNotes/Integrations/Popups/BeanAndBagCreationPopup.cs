using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Styles;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;
using Controls = Microsoft.Maui.Controls;

namespace BaristaNotes.Integrations.Popups;

/// <summary>
/// Combined modal popup for creating a new bean AND bag together.
/// This eliminates the need for two sequential popups when a new user
/// needs to create their first bean and bag to start logging shots.
/// </summary>
public class BeanAndBagCreationPopup : ActionModalPopup
{
    // Styling constants matching FormEntryField pattern
    private const int FieldHeight = 50;
    private const int CornerRadius = 25;
    private const int HorizontalPadding = 16;
    private const int MultilineFieldHeight = 80;
    private const int MultilineCornerRadius = 16;

    private readonly IBeanService _beanService;
    private readonly IBagService _bagService;

    // Bean fields
    private readonly Controls.Entry _nameEntry;
    private readonly Controls.Entry _roasterEntry;
    private readonly Controls.Entry _originEntry;

    // Bag fields
    private readonly Controls.DatePicker _roastDatePicker;
    private readonly Controls.Editor _notesEditor;

    private readonly Controls.Label _errorLabel;
    private bool _isSaving;

    /// <summary>
    /// Callback invoked when both bean and bag are successfully created.
    /// Returns the created bag summary for auto-selection in shot logging.
    /// </summary>
    public Action<BagSummaryDto>? OnCreated { get; set; }

    public BeanAndBagCreationPopup(IBeanService beanService, IBagService bagService)
    {
        _beanService = beanService;
        _bagService = bagService;

        Title = "New Coffee";
        ActionButtonText = "Create";
        ShowActionButton = true;

        // Bean fields
        _nameEntry = new Controls.Entry
        {
            Placeholder = "Ethiopian Yirgacheffe",
            BackgroundColor = Colors.Transparent,
            TextColor = AppColors.Dark.TextPrimary,
            PlaceholderColor = AppColors.Dark.TextSecondary
        };
        _roasterEntry = new Controls.Entry
        {
            Placeholder = "Blue Bottle",
            BackgroundColor = Colors.Transparent,
            TextColor = AppColors.Dark.TextPrimary,
            PlaceholderColor = AppColors.Dark.TextSecondary
        };
        _originEntry = new Controls.Entry
        {
            Placeholder = "Ethiopia",
            BackgroundColor = Colors.Transparent,
            TextColor = AppColors.Dark.TextPrimary,
            PlaceholderColor = AppColors.Dark.TextSecondary
        };

        // Bag fields
        _roastDatePicker = new Controls.DatePicker
        {
            Date = DateTime.Today,
            MaximumDate = DateTime.Today,
            BackgroundColor = Colors.Transparent,
            TextColor = AppColors.Dark.TextPrimary
        };
        _notesEditor = new Controls.Editor
        {
            Placeholder = "Tasting notes, purchase info...",
            BackgroundColor = Colors.Transparent,
            TextColor = AppColors.Dark.TextPrimary,
            PlaceholderColor = AppColors.Dark.TextSecondary
        };

        _errorLabel = new Controls.Label
        {
            TextColor = Colors.Red,
            FontSize = 12,
            IsVisible = false,
            HorizontalOptions = LayoutOptions.Center
        };

        // Clear validation on text change
        _nameEntry.TextChanged += (s, e) => ClearError();

        // Build form content
        PopupContent = new Controls.ScrollView
        {
            Content = new Controls.VerticalStackLayout
            {
                Spacing = 10,
                Padding = new Thickness(HorizontalPadding, 0),
                HorizontalOptions = LayoutOptions.Fill,
                Children =
                {
                    new Controls.Label
                    {
                        Text = "Add a new coffee to your collection",
                        FontSize = 14,
                        HorizontalOptions = LayoutOptions.Center,
                        TextColor = AppColors.Dark.TextSecondary
                    },
                    _errorLabel,
                    
                    // Bean section header
                    CreateSectionHeader("Bean Details"),
                    CreateFormField("Name *", _nameEntry, false),
                    CreateFormField("Roaster", _roasterEntry, false),
                    CreateFormField("Origin", _originEntry, false),
                    
                    // Bag section header
                    CreateSectionHeader("Bag Details"),
                    CreateFormField("Roast Date *", _roastDatePicker, false),
                    CreateFormField("Notes", _notesEditor, true)
                }
            }
        };

        // Wire up action button
        ActionButtonCommand = new Command(async () => await SaveAsync(), () => !_isSaving);
    }

    private Controls.View CreateSectionHeader(string title)
    {
        return new Controls.Label
        {
            Text = title,
            FontSize = 16,
            FontAttributes = Microsoft.Maui.Controls.FontAttributes.Bold,
            TextColor = AppColors.Dark.TextPrimary,
            Margin = new Thickness(0, 8, 0, 0)
        };
    }

    private Controls.View CreateFormField(string label, Controls.View input, bool isMultiline)
    {
        var fieldHeight = isMultiline ? MultilineFieldHeight : FieldHeight;
        var cornerRadius = isMultiline ? MultilineCornerRadius : CornerRadius;

        var backgroundBox = new Controls.BoxView
        {
            BackgroundColor = AppColors.Dark.SurfaceVariant,
            CornerRadius = cornerRadius,
            HeightRequest = fieldHeight
        };

        var fieldContainer = new Controls.Grid
        {
            HeightRequest = fieldHeight,
            Children = { backgroundBox }
        };

        if (input is Controls.Entry entry)
        {
            entry.VerticalOptions = LayoutOptions.Center;
            entry.Margin = new Thickness(HorizontalPadding, 0);
        }
        else if (input is Controls.Editor editor)
        {
            editor.VerticalOptions = LayoutOptions.Fill;
            editor.Margin = new Thickness(HorizontalPadding, 8);
        }
        else if (input is Controls.DatePicker datePicker)
        {
            datePicker.VerticalOptions = LayoutOptions.Center;
            datePicker.Margin = new Thickness(HorizontalPadding, 0);
        }

        fieldContainer.Children.Add(input);

        return new Controls.VerticalStackLayout
        {
            Spacing = 4,
            HorizontalOptions = LayoutOptions.Fill,
            Children =
            {
                new Controls.Label
                {
                    Text = label,
                    FontSize = 12,
                    TextColor = AppColors.Dark.TextSecondary,
                    Margin = new Thickness(HorizontalPadding, 0, 0, 0)
                },
                fieldContainer
            }
        };
    }

    private void ClearError()
    {
        _errorLabel.IsVisible = false;
        _errorLabel.Text = string.Empty;
    }

    private void ShowError(string message)
    {
        _errorLabel.Text = message;
        _errorLabel.IsVisible = true;
    }

    private void SetSaving(bool saving)
    {
        _isSaving = saving;
        _nameEntry.IsEnabled = !saving;
        _roasterEntry.IsEnabled = !saving;
        _originEntry.IsEnabled = !saving;
        _roastDatePicker.IsEnabled = !saving;
        _notesEditor.IsEnabled = !saving;
        ActionButtonText = saving ? "Creating..." : "Create";
        ((Command)ActionButtonCommand).ChangeCanExecute();
    }

    private async Task SaveAsync()
    {
        // Validate bean name
        var name = _nameEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Bean name is required");
            return;
        }

        // Validate roast date not in future
        if (_roastDatePicker.Date.HasValue && _roastDatePicker.Date.Value > DateTime.Today)
        {
            ShowError("Roast date cannot be in the future");
            return;
        }

        SetSaving(true);
        ClearError();

        try
        {
            // Step 1: Create the bean
            var createBeanDto = new CreateBeanDto
            {
                Name = name,
                Roaster = string.IsNullOrWhiteSpace(_roasterEntry.Text) ? null : _roasterEntry.Text.Trim(),
                Origin = string.IsNullOrWhiteSpace(_originEntry.Text) ? null : _originEntry.Text.Trim(),
                Notes = null // Bean notes not collected in combined form
            };

            var beanResult = await _beanService.CreateBeanAsync(createBeanDto);

            if (!beanResult.Success || beanResult.Data == null)
            {
                SetSaving(false);
                ShowError(beanResult.ErrorMessage ?? "Failed to create bean");
                return;
            }

            // Step 2: Create the bag for this bean
            var bag = new Bag
            {
                BeanId = beanResult.Data.Id,
                RoastDate = _roastDatePicker.Date ?? DateTime.Today,
                Notes = string.IsNullOrWhiteSpace(_notesEditor.Text) ? null : _notesEditor.Text.Trim(),
                IsComplete = false,
                IsActive = true
            };

            var bagResult = await _bagService.CreateBagAsync(bag);

            if (!bagResult.Success || bagResult.Data == null)
            {
                SetSaving(false);
                ShowError(bagResult.ErrorMessage ?? "Failed to create bag");
                return;
            }

            // Create summary for callback
            var summary = new BagSummaryDto
            {
                Id = bagResult.Data.Id,
                BeanId = bagResult.Data.BeanId,
                BeanName = beanResult.Data.Name,
                RoastDate = bagResult.Data.RoastDate,
                Notes = bagResult.Data.Notes,
                IsComplete = false,
                ShotCount = 0,
                AverageRating = null
            };

            // Dismiss and notify
            await IPopupService.Current.PopAsync();
            OnCreated?.Invoke(summary);
        }
        catch (Exception ex)
        {
            SetSaving(false);
            ShowError(ex.Message);
        }
    }
}
