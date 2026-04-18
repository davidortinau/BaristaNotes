using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;
using Controls = Microsoft.Maui.Controls;

namespace BaristaNotes.Integrations.Popups;

/// <summary>
/// "Three Doors to Coffee" popup — unified entry point for adding a coffee.
/// Browse mode (default when recent beans exist): tap a recent bean to instantly
/// create a new bag dated today. Type mode: full bean + bag form with
/// fuzzy-match hint, roaster/origin chip suggestions, and a Today-chip date field.
/// </summary>
public class AddCoffeePopup : ActionModalPopup, IDisposable
{
    private enum Mode
    {
        Browse,
        Type
    }

    private const int FieldHeight = 50;
    private const int CornerRadius = 25;
    private const int HorizontalPadding = 16;
    private const int MultilineFieldHeight = 80;
    private const int MultilineCornerRadius = 16;
    private const int ChipCornerRadius = 14;
    private const int FuzzyDebounceMs = 300;

    private readonly IBeanService _beanService;
    private readonly IBagService _bagService;
    private readonly IFeedbackService _feedbackService;
    private readonly ILogger<AddCoffeePopup> _logger;

    private Mode _mode = Mode.Type;
    private IReadOnlyList<BeanDto> _recentBeans = Array.Empty<BeanDto>();
    private IReadOnlyList<string> _knownRoasters = Array.Empty<string>();
    private IReadOnlyList<string> _knownOrigins = Array.Empty<string>();
    private bool _isSaving;
    private bool _initialized;

    // Type-mode fields (recreated each time we render Type mode)
    private Controls.Entry? _nameEntry;
    private Controls.Entry? _roasterEntry;
    private Controls.Entry? _originEntry;
    private Controls.Editor? _notesEditor;
    private Controls.Label? _errorLabel;
    private Controls.Label? _fuzzyHintLabel;
    private Controls.Button? _fuzzyUseButton;
    private Controls.HorizontalStackLayout? _roasterChipsHost;
    private Controls.HorizontalStackLayout? _originChipsHost;
    private Controls.Label? _dateChipLabel;
    private Controls.Border? _dateChipBorder;
    private Controls.DatePicker? _hiddenDatePicker;

    private DateTime _roastDate = DateTime.Today;
    private BeanDto? _fuzzyMatch;
    private CancellationTokenSource? _fuzzyCts;

    /// <summary>
    /// Callback invoked when a bag is successfully created (either via Browse tap
    /// or Type-mode Save). Returns the created bag summary for auto-selection.
    /// </summary>
    public Action<BagSummaryDto>? OnCreated { get; set; }

    public AddCoffeePopup(
        IBeanService beanService,
        IBagService bagService,
        IFeedbackService feedbackService,
        ILogger<AddCoffeePopup> logger)
    {
        _beanService = beanService;
        _bagService = bagService;
        _feedbackService = feedbackService;
        _logger = logger;

        Title = "Add Coffee";
        ActionButtonText = "Create";
        ShowActionButton = false;

        ActionButtonCommand = new Command(async () => await SaveTypeModeAsync(), () => !_isSaving);

        // Render an initial placeholder; InitializeAsync swaps to real content.
        PopupContent = BuildLoadingContent();
    }

    /// <summary>
    /// Loads recent beans and distinct roaster/origin values, then renders the
    /// appropriate initial mode. Call before pushing the popup.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        try
        {
            var recentTask = _beanService.GetRecentBeansAsync(limit: 6);
            var roastersTask = _beanService.GetDistinctRoastersAsync();
            var originsTask = _beanService.GetDistinctOriginsAsync();

            await Task.WhenAll(recentTask, roastersTask, originsTask);

            _recentBeans = recentTask.Result ?? Array.Empty<BeanDto>();
            _knownRoasters = roastersTask.Result ?? Array.Empty<string>();
            _knownOrigins = originsTask.Result ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load initial data for AddCoffeePopup");
            _recentBeans = Array.Empty<BeanDto>();
            _knownRoasters = Array.Empty<string>();
            _knownOrigins = Array.Empty<string>();
        }

        _mode = _recentBeans.Count > 0 ? Mode.Browse : Mode.Type;
        RenderCurrentMode();
    }

    private void RenderCurrentMode()
    {
        ShowActionButton = _mode == Mode.Type;

        PopupContent = _mode switch
        {
            Mode.Browse => BuildBrowseContent(),
            Mode.Type => BuildTypeContent(),
            _ => BuildTypeContent()
        };
    }

    // ---------- Browse mode ----------

    private Controls.View BuildLoadingContent()
    {
        return new Controls.VerticalStackLayout
        {
            Padding = new Thickness(HorizontalPadding, 16),
            Children =
            {
                new Controls.ActivityIndicator
                {
                    IsRunning = true,
                    HorizontalOptions = LayoutOptions.Center
                }
            }
        };
    }

    private Controls.View BuildBrowseContent()
    {
        var carouselStack = new Controls.HorizontalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(HorizontalPadding, 0)
        };

        foreach (var bean in _recentBeans)
        {
            carouselStack.Children.Add(BuildBeanCard(bean));
        }

        var carousel = new Controls.ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = carouselStack
        };

        var newCoffeeButton = new Controls.Button
        {
            Text = "New coffee…",
            BackgroundColor = Colors.Transparent,
            TextColor = AppColors.Dark.Primary,
            BorderColor = AppColors.Dark.Primary,
            BorderWidth = 1,
            CornerRadius = 18,
            HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Center,
            Padding = new Thickness(20, 0)
        };
        newCoffeeButton.Clicked += (_, _) =>
        {
            _mode = Mode.Type;
            RenderCurrentMode();
        };

        return new Controls.VerticalStackLayout
        {
            Spacing = 16,
            Padding = new Thickness(0, 4, 0, 8),
            Children =
            {
                new Controls.Label
                {
                    Text = "Tap a recent coffee to log a new bag today",
                    FontSize = 14,
                    TextColor = AppColors.Dark.TextSecondary,
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(HorizontalPadding, 0)
                },
                carousel,
                newCoffeeButton
            }
        };
    }

    private Controls.View BuildBeanCard(BeanDto bean)
    {
        var nameLabel = new Controls.Label
        {
            Text = bean.Name,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = AppColors.Dark.TextPrimary,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1
        };

        var roasterLabel = new Controls.Label
        {
            Text = string.IsNullOrWhiteSpace(bean.Roaster) ? "—" : bean.Roaster,
            FontSize = 11,
            TextColor = AppColors.Dark.TextSecondary,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1
        };

        var card = new Controls.Border
        {
            BackgroundColor = AppColors.Dark.SurfaceVariant,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            WidthRequest = 140,
            HeightRequest = 80,
            Padding = new Thickness(12, 10),
            Content = new Controls.VerticalStackLayout
            {
                Spacing = 4,
                VerticalOptions = LayoutOptions.Center,
                Children = { nameLabel, roasterLabel }
            }
        };

        var tap = new Controls.TapGestureRecognizer();
        tap.Tapped += async (_, _) => await HandleBeanTappedAsync(bean);
        card.GestureRecognizers.Add(tap);

        return card;
    }

    private async Task HandleBeanTappedAsync(BeanDto bean)
    {
        if (_isSaving)
        {
            return;
        }

        _isSaving = true;
        _logger.LogDebug("Browse-mode bean tapped: {BeanId} {BeanName}", bean.Id, bean.Name);

        try
        {
            var result = await _bagService.CreateNewBagForBeanAsync(bean.Id, DateTime.Today);

            if (!result.Success || result.Data == null)
            {
                _logger.LogError("Failed to create bag for bean {BeanId}: {Error}", bean.Id, result.ErrorMessage);
                await _feedbackService.ShowErrorAsync(result.ErrorMessage ?? "Couldn't create bag");
                _isSaving = false;
                return;
            }

            _feedbackService.TriggerSuccessHaptic();
            await IPopupService.Current.PopAsync();
            OnCreated?.Invoke(result.Data);
            Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception creating bag for bean {BeanId}", bean.Id);
            await _feedbackService.ShowErrorAsync("Couldn't create bag");
            _isSaving = false;
        }
    }

    // ---------- Type mode ----------

    private Controls.View BuildTypeContent()
    {
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
        _notesEditor = new Controls.Editor
        {
            Placeholder = "Tasting notes…",
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

        _fuzzyHintLabel = new Controls.Label
        {
            FontSize = 12,
            TextColor = AppColors.Dark.TextSecondary,
            IsVisible = false,
            Margin = new Thickness(HorizontalPadding, 0, 0, 0)
        };
        _fuzzyUseButton = new Controls.Button
        {
            Text = "Use it",
            BackgroundColor = Colors.Transparent,
            TextColor = AppColors.Dark.Primary,
            BorderWidth = 0,
            Padding = new Thickness(8, 0),
            HeightRequest = 28,
            IsVisible = false,
            HorizontalOptions = LayoutOptions.Start
        };
        _fuzzyUseButton.Clicked += async (_, _) =>
        {
            if (_fuzzyMatch != null)
            {
                var match = _fuzzyMatch;
                await HandleBeanTappedAsync(match);
            }
        };

        _nameEntry.TextChanged += (_, _) =>
        {
            ClearError();
            ScheduleFuzzyCheck();
        };
        _roasterEntry.TextChanged += (_, _) =>
        {
            ScheduleFuzzyCheck();
            UpdateChipSuggestions(_roasterChipsHost, _knownRoasters, _roasterEntry!.Text);
        };
        _originEntry.TextChanged += (_, _) =>
        {
            UpdateChipSuggestions(_originChipsHost, _knownOrigins, _originEntry!.Text);
        };

        _roasterChipsHost = new Controls.HorizontalStackLayout
        {
            Spacing = 6,
            Padding = new Thickness(HorizontalPadding, 0, HorizontalPadding, 0)
        };
        _originChipsHost = new Controls.HorizontalStackLayout
        {
            Spacing = 6,
            Padding = new Thickness(HorizontalPadding, 0, HorizontalPadding, 0)
        };
        UpdateChipSuggestions(_roasterChipsHost, _knownRoasters, null);
        UpdateChipSuggestions(_originChipsHost, _knownOrigins, null);

        // Back-to-Browse link (only if we have recents)
        Controls.View? backLink = null;
        if (_recentBeans.Count > 0)
        {
            var back = new Controls.Button
            {
                Text = "← Browse recent",
                BackgroundColor = Colors.Transparent,
                TextColor = AppColors.Dark.Primary,
                BorderWidth = 0,
                Padding = new Thickness(0),
                HeightRequest = 28,
                HorizontalOptions = LayoutOptions.Start
            };
            back.Clicked += (_, _) =>
            {
                _mode = Mode.Browse;
                RenderCurrentMode();
            };
            backLink = back;
        }

        var content = new Controls.VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(HorizontalPadding, 0),
            HorizontalOptions = LayoutOptions.Fill
        };

        if (backLink != null)
        {
            content.Children.Add(backLink);
        }

        content.Children.Add(new Controls.Label
        {
            Text = "Add a new coffee to your collection",
            FontSize = 14,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = AppColors.Dark.TextSecondary
        });
        content.Children.Add(_errorLabel);

        content.Children.Add(CreateFormField("Name *", _nameEntry, false));
        content.Children.Add(_fuzzyHintLabel);
        content.Children.Add(_fuzzyUseButton);

        content.Children.Add(CreateFormField("Roaster", _roasterEntry, false));
        content.Children.Add(_roasterChipsHost);

        content.Children.Add(CreateFormField("Origin", _originEntry, false));
        content.Children.Add(_originChipsHost);

        content.Children.Add(BuildRoastDateField());
        content.Children.Add(CreateFormField("Notes", _notesEditor, true));

        return new Controls.ScrollView { Content = content };
    }

    private Controls.View BuildRoastDateField()
    {
        var isToday = _roastDate.Date == DateTime.Today;
        _dateChipLabel = new Controls.Label
        {
            Text = isToday ? "Today" : _roastDate.ToString("MMM d, yyyy"),
            TextColor = isToday ? Colors.White : AppColors.Dark.TextPrimary,
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };

        _dateChipBorder = new Controls.Border
        {
            BackgroundColor = isToday ? AppColors.Dark.Primary : AppColors.Dark.SurfaceVariant,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = ChipCornerRadius },
            HeightRequest = 32,
            Padding = new Thickness(16, 0),
            HorizontalOptions = LayoutOptions.Start,
            Content = _dateChipLabel
        };

        _hiddenDatePicker = new Controls.DatePicker
        {
            Date = _roastDate,
            MaximumDate = DateTime.Today,
            BackgroundColor = Colors.Transparent,
            Opacity = 0.01,
            HorizontalOptions = LayoutOptions.Start,
            WidthRequest = 140,
            HeightRequest = 32
        };
        _hiddenDatePicker.DateSelected += (_, e) =>
        {
            _roastDate = e.NewDate ?? _roastDate;
            UpdateDateChip();
        };

        var grid = new Controls.Grid
        {
            RowDefinitions = { new RowDefinition { Height = GridLength.Auto } },
            ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto } },
            HorizontalOptions = LayoutOptions.Start
        };
        grid.Children.Add(_dateChipBorder);
        grid.Children.Add(_hiddenDatePicker);

        return new Controls.VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Controls.Label
                {
                    Text = "Roast Date *",
                    FontSize = 12,
                    TextColor = AppColors.Dark.TextSecondary,
                    Margin = new Thickness(HorizontalPadding, 0, 0, 0)
                },
                grid
            }
        };
    }

    private void UpdateDateChip()
    {
        if (_dateChipLabel == null || _dateChipBorder == null)
        {
            return;
        }
        var isToday = _roastDate.Date == DateTime.Today;
        _dateChipLabel.Text = isToday ? "Today" : _roastDate.ToString("MMM d, yyyy");
        _dateChipLabel.TextColor = isToday ? Colors.White : AppColors.Dark.TextPrimary;
        _dateChipBorder.BackgroundColor = isToday ? AppColors.Dark.Primary : AppColors.Dark.SurfaceVariant;
    }

    private Controls.View CreateFormField(string label, Controls.View input, bool isMultiline)
    {
        var fieldHeight = isMultiline ? MultilineFieldHeight : FieldHeight;
        var cornerRadius = isMultiline ? MultilineCornerRadius : CornerRadius;

        var background = new Controls.Border
        {
            BackgroundColor = AppColors.Dark.SurfaceVariant,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = cornerRadius },
            HeightRequest = fieldHeight
        };

        var fieldContainer = new Controls.Grid
        {
            HeightRequest = fieldHeight,
            Children = { background }
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

    private void UpdateChipSuggestions(Controls.HorizontalStackLayout? host, IReadOnlyList<string> pool, string? filter)
    {
        if (host == null)
        {
            return;
        }
        host.Children.Clear();

        if (pool.Count == 0)
        {
            return;
        }

        IEnumerable<string> matches = pool;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim();
            matches = pool.Where(p => p.Contains(f, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var value in matches.Take(3))
        {
            host.Children.Add(BuildSuggestionChip(value, host));
        }
    }

    private Controls.View BuildSuggestionChip(string value, Controls.HorizontalStackLayout host)
    {
        var label = new Controls.Label
        {
            Text = value,
            FontSize = 12,
            TextColor = AppColors.Dark.TextPrimary,
            VerticalOptions = LayoutOptions.Center
        };

        var chip = new Controls.Border
        {
            BackgroundColor = AppColors.Dark.SurfaceVariant,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = ChipCornerRadius },
            Padding = new Thickness(12, 4),
            HeightRequest = 28,
            Content = label
        };

        var tap = new Controls.TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (host == _roasterChipsHost && _roasterEntry != null)
            {
                _roasterEntry.Text = value;
            }
            else if (host == _originChipsHost && _originEntry != null)
            {
                _originEntry.Text = value;
            }
        };
        chip.GestureRecognizers.Add(tap);

        return chip;
    }

    private void ScheduleFuzzyCheck()
    {
        _fuzzyCts?.Cancel();
        _fuzzyCts?.Dispose();
        _fuzzyCts = new CancellationTokenSource();
        var token = _fuzzyCts.Token;

        var name = _nameEntry?.Text;
        var roaster = _roasterEntry?.Text;

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2)
        {
            _fuzzyMatch = null;
            SetFuzzyHintVisible(false, null);
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(FuzzyDebounceMs, token);
                if (token.IsCancellationRequested) return;

                var match = await _beanService.FuzzyFindByNameRoasterAsync(
                    name!.Trim(),
                    string.IsNullOrWhiteSpace(roaster) ? null : roaster!.Trim());

                if (token.IsCancellationRequested) return;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _fuzzyMatch = match;
                    SetFuzzyHintVisible(match != null, match);
                });
            }
            catch (TaskCanceledException)
            {
                // expected
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fuzzy lookup failed for name {Name}", name);
            }
        }, token);
    }

    private void SetFuzzyHintVisible(bool visible, BeanDto? match)
    {
        if (_fuzzyHintLabel == null || _fuzzyUseButton == null)
        {
            return;
        }
        if (visible && match != null)
        {
            _fuzzyHintLabel.Text = $"Looks like \"{match.Name}\" — use that?";
            _fuzzyHintLabel.IsVisible = true;
            _fuzzyUseButton.IsVisible = true;
        }
        else
        {
            _fuzzyHintLabel.IsVisible = false;
            _fuzzyUseButton.IsVisible = false;
        }
    }

    private void ClearError()
    {
        if (_errorLabel == null) return;
        _errorLabel.IsVisible = false;
        _errorLabel.Text = string.Empty;
    }

    private void ShowError(string message)
    {
        if (_errorLabel == null) return;
        _errorLabel.Text = message;
        _errorLabel.IsVisible = true;
    }

    private void SetSaving(bool saving)
    {
        _isSaving = saving;
        if (_nameEntry != null) _nameEntry.IsEnabled = !saving;
        if (_roasterEntry != null) _roasterEntry.IsEnabled = !saving;
        if (_originEntry != null) _originEntry.IsEnabled = !saving;
        if (_notesEditor != null) _notesEditor.IsEnabled = !saving;
        if (_hiddenDatePicker != null) _hiddenDatePicker.IsEnabled = !saving;
        ActionButtonText = saving ? "Creating..." : "Create";
        if (ActionButtonCommand is Command cmd)
        {
            cmd.ChangeCanExecute();
        }
    }

    private async Task SaveTypeModeAsync()
    {
        var name = _nameEntry?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Bean name is required");
            return;
        }
        if (_roastDate.Date > DateTime.Today)
        {
            ShowError("Roast date cannot be in the future");
            return;
        }

        SetSaving(true);
        ClearError();

        try
        {
            var createBeanDto = new CreateBeanDto
            {
                Name = name!,
                Roaster = string.IsNullOrWhiteSpace(_roasterEntry?.Text) ? null : _roasterEntry!.Text.Trim(),
                Origin = string.IsNullOrWhiteSpace(_originEntry?.Text) ? null : _originEntry!.Text.Trim(),
                Notes = null
            };

            var beanResult = await _beanService.CreateBeanAsync(createBeanDto);
            if (!beanResult.Success || beanResult.Data == null)
            {
                _logger.LogError("CreateBeanAsync failed: {Error}", beanResult.ErrorMessage);
                ShowError(beanResult.ErrorMessage ?? "Failed to create bean");
                SetSaving(false);
                return;
            }

            var notes = string.IsNullOrWhiteSpace(_notesEditor?.Text) ? null : _notesEditor!.Text.Trim();
            var bagResult = await _bagService.CreateNewBagForBeanAsync(beanResult.Data.Id, _roastDate, notes);
            if (!bagResult.Success || bagResult.Data == null)
            {
                _logger.LogError("CreateNewBagForBeanAsync failed for bean {BeanId}: {Error}", beanResult.Data.Id, bagResult.ErrorMessage);
                ShowError(bagResult.ErrorMessage ?? "Failed to create bag");
                SetSaving(false);
                return;
            }

            _logger.LogDebug("Created bean {BeanId} and bag {BagId}", beanResult.Data.Id, bagResult.Data.Id);
            _feedbackService.TriggerSuccessHaptic();
            await IPopupService.Current.PopAsync();
            OnCreated?.Invoke(bagResult.Data);
            Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception saving new coffee");
            ShowError(ex.Message);
            SetSaving(false);
        }
    }

    public void Dispose()
    {
        _fuzzyCts?.Cancel();
        _fuzzyCts?.Dispose();
        _fuzzyCts = null;
        GC.SuppressFinalize(this);
    }
}
