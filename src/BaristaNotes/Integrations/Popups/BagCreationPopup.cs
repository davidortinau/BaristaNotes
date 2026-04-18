using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Styles;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;
using Controls = Microsoft.Maui.Controls;

namespace BaristaNotes.Integrations.Popups;

/// <summary>
/// Modal popup for creating a new bag during shot logging.
/// Supports two modes:
/// - Preset BeanId (from bean creation flow - US1)
/// - Bean picker (when beans exist but no bags - US2)
/// Part of inline creation flow (T004-T010, T020-T021).
/// </summary>
public class BagCreationPopup : ActionModalPopup
{
    // Styling constants matching FormEntryField pattern
    private const int FieldHeight = 50;
    private const int CornerRadius = 25;
    private const int HorizontalPadding = 16;
    private const int MultilineFieldHeight = 100;
    private const int MultilineCornerRadius = 16;

    private readonly IBagService _bagService;
    private readonly Controls.DatePicker _roastDatePicker;
    private readonly Controls.Editor _notesEditor;
    private readonly Controls.Label _errorLabel;
    private readonly Controls.Picker? _beanPicker;
    private readonly Controls.Label? _beanDisplayLabel;
    private bool _isSaving;

    /// <summary>
    /// Pre-set bean ID when chaining from bean creation (US1).
    /// </summary>
    public int? BeanId { get; set; }

    /// <summary>
    /// Bean name to display when BeanId is pre-set.
    /// </summary>
    public string? BeanName { get; set; }

    /// <summary>
    /// Available beans for picker mode (US2, T020).
    /// Only used when BeanId is not pre-set.
    /// </summary>
    public List<BeanDto>? AvailableBeans { get; set; }

    /// <summary>
    /// Callback invoked when bag is successfully created (T006).
    /// Used by ShotLoggingPage to auto-select the new bag.
    /// </summary>
    public Action<BagSummaryDto>? OnBagCreated { get; set; }

    public BagCreationPopup(IBagService bagService)
    {
        _bagService = bagService;

        Title = "Create Bag";
        ActionButtonText = "Create";
        ShowActionButton = true;

        // Create form fields with styling
        _roastDatePicker = new Controls.DatePicker
        {
            Date = DateTime.Today,
            MaximumDate = DateTime.Today,  // T010: Can't be in future
            BackgroundColor = Colors.Transparent,
            TextColor = AppColors.Dark.TextPrimary
        };
        _notesEditor = new Controls.Editor
        {
            Placeholder = "e.g., From local roaster...",
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

        // Create bean picker for US2 mode (will be shown/hidden based on BeanId)
        _beanPicker = new Controls.Picker
        {
            Title = "Select Bean",
            BackgroundColor = Colors.Transparent,
            TextColor = AppColors.Dark.TextPrimary,
            TitleColor = AppColors.Dark.TextSecondary
        };
        _beanDisplayLabel = new Controls.Label
        {
            FontSize = 16,
            TextColor = AppColors.Dark.TextPrimary
        };

        // Wire up action button
        ActionButtonCommand = new Command(async () => await SaveBagAsync(), () => !_isSaving);
    }

    /// <summary>
    /// Build the popup content based on mode (preset BeanId vs picker).
    /// Must be called after setting BeanId, BeanName, and AvailableBeans.
    /// </summary>
    public void Build()
    {
        var showPicker = !BeanId.HasValue && AvailableBeans?.Any() == true;

        Controls.View beanControl;
        if (showPicker)
        {
            // US2 mode: Show bean picker
            _beanPicker!.ItemsSource = AvailableBeans!.Select(b => b.Name).ToList();
            _beanPicker.SelectedIndex = 0;
            beanControl = CreateFormField("Bean *", _beanPicker, false);
        }
        else
        {
            // US1 mode: Show bean name display
            _beanDisplayLabel!.Text = BeanName ?? "Unknown";
            beanControl = CreateFormField("Bean", _beanDisplayLabel, false);
        }

        var subtitle = showPicker
            ? "Create a new bag for an existing bean"
            : $"Create a bag for {BeanName}";

        PopupContent = new Controls.VerticalStackLayout
        {
            Spacing = 12,
            Padding = new Thickness(HorizontalPadding, 0),
            Children =
            {
                new Controls.Label
                {
                    Text = subtitle,
                    FontSize = 14,
                    HorizontalOptions = LayoutOptions.Center,
                    TextColor = AppColors.Dark.TextSecondary
                },
                _errorLabel,
                beanControl,
                CreateFormField("Roast Date *", _roastDatePicker, false),
                CreateFormField("Notes", _notesEditor, true)
            }
        };
    }

    /// <summary>
    /// Creates a styled form field with bordered background matching app design.
    /// Uses Grid+BoxView pattern from FormEntryField component.
    /// </summary>
    private Controls.View CreateFormField(string label, Controls.View input, bool isMultiline)
    {
        var fieldHeight = isMultiline ? MultilineFieldHeight : FieldHeight;
        var cornerRadius = isMultiline ? MultilineCornerRadius : CornerRadius;

        // Background box with rounded corners
        var backgroundBox = new Controls.BoxView
        {
            BackgroundColor = AppColors.Dark.SurfaceVariant,
            CornerRadius = cornerRadius,
            HeightRequest = fieldHeight
        };

        // Container grid with input overlay on background
        var fieldContainer = new Controls.Grid
        {
            HeightRequest = fieldHeight,
            Children = { backgroundBox }
        };

        // Position input with padding inside the container
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
        else if (input is Controls.Picker picker)
        {
            picker.VerticalOptions = LayoutOptions.Center;
            picker.Margin = new Thickness(HorizontalPadding, 0);
        }
        else if (input is Controls.DatePicker datePicker)
        {
            datePicker.VerticalOptions = LayoutOptions.Center;
            datePicker.Margin = new Thickness(HorizontalPadding, 0);
        }
        else if (input is Controls.Label displayLabel)
        {
            displayLabel.VerticalOptions = LayoutOptions.Center;
            displayLabel.Margin = new Thickness(HorizontalPadding, 0);
        }

        fieldContainer.Children.Add(input);

        return new Controls.VerticalStackLayout
        {
            Spacing = 6,
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

    private void ShowError(string message)
    {
        _errorLabel.Text = message;
        _errorLabel.IsVisible = true;
    }

    private void ClearError()
    {
        _errorLabel.IsVisible = false;
        _errorLabel.Text = string.Empty;
    }

    private void SetSaving(bool saving)
    {
        _isSaving = saving;
        _roastDatePicker.IsEnabled = !saving;
        _notesEditor.IsEnabled = !saving;
        if (_beanPicker != null) _beanPicker.IsEnabled = !saving;
        ActionButtonText = saving ? "Creating..." : "Create";
        ((Command)ActionButtonCommand).ChangeCanExecute();
    }

    /// <summary>
    /// Gets the selected bean ID either from props (US1) or picker (US2).
    /// </summary>
    private int? GetSelectedBeanId()
    {
        if (BeanId.HasValue)
            return BeanId.Value;

        if (AvailableBeans?.Any() == true && _beanPicker != null &&
            _beanPicker.SelectedIndex >= 0 && _beanPicker.SelectedIndex < AvailableBeans.Count)
            return AvailableBeans[_beanPicker.SelectedIndex].Id;

        return null;
    }

    /// <summary>
    /// Gets the selected bean name for display.
    /// </summary>
    private string GetSelectedBeanName()
    {
        if (!string.IsNullOrEmpty(BeanName))
            return BeanName;

        if (AvailableBeans?.Any() == true && _beanPicker != null &&
            _beanPicker.SelectedIndex >= 0 && _beanPicker.SelectedIndex < AvailableBeans.Count)
            return AvailableBeans[_beanPicker.SelectedIndex].Name;

        return "Unknown";
    }

    /// <summary>
    /// Save bag using IBagService (T008).
    /// Validates roast date (T010) and invokes callback on success (T006).
    /// </summary>
    private async Task SaveBagAsync()
    {
        var beanId = GetSelectedBeanId();

        // Validate bean is selected
        if (!beanId.HasValue)
        {
            ShowError("Please select a bean");
            return;
        }

        // Validate roast date not in future (T010)
        if (_roastDatePicker.Date.HasValue && _roastDatePicker.Date.Value > DateTime.Today)
        {
            ShowError("Roast date cannot be in the future");
            return;
        }

        SetSaving(true);
        ClearError();

        try
        {
            var bag = new Bag
            {
                BeanId = beanId.Value,
                RoastDate = _roastDatePicker.Date ?? DateTime.Today,
                Notes = string.IsNullOrWhiteSpace(_notesEditor.Text) ? null : _notesEditor.Text.Trim(),
                IsComplete = false,
                IsActive = true
            };

            var result = await _bagService.CreateBagAsync(bag);

            if (result.Success && result.Data != null)
            {
                // Create summary DTO for callback
                var summary = new BagSummaryDto
                {
                    Id = result.Data.Id,
                    BeanId = result.Data.BeanId,
                    BeanName = GetSelectedBeanName(),
                    RoastDate = result.Data.RoastDate,
                    Notes = result.Data.Notes,
                    IsComplete = false,
                    ShotCount = 0,
                    AverageRating = null
                };

                // Dismiss popup and invoke callback (T006)
                await IPopupService.Current.PopAsync();
                OnBagCreated?.Invoke(summary);
            }
            else
            {
                SetSaving(false);
                ShowError(result.ErrorMessage ?? "Failed to create bag");
            }
        }
        catch (Exception ex)
        {
            SetSaving(false);
            ShowError(ex.Message);
        }
    }
}
