using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Models;
using BaristaNotes.Styles;
using Fonts;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;
using Controls = Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace BaristaNotes.Integrations.Popups;

/// <summary>
/// Modal popup for filtering shot history by Bean, Made For, and Rating.
/// Supports multi-select for each filter category with AND logic.
/// </summary>
public class ShotFilterPopup : ActionModalPopup
{
    private const int ChipHeight = 40;
    private const int ChipCornerRadius = 20;
    private const int HorizontalPadding = 16;
    private const int ChipSpacing = 8;

    private readonly Controls.FlexLayout _beanChipsLayout;
    private readonly Controls.FlexLayout _peopleChipsLayout;
    private readonly Controls.FlexLayout _ratingChipsLayout;
    private readonly Controls.Button _clearButton;
    
    private ShotFilterCriteria _workingFilters;
    private bool _isInitialized;
    
    /// <summary>
    /// Current filter state to initialize popup with.
    /// </summary>
    public ShotFilterCriteria CurrentFilters { get; set; } = new();
    
    /// <summary>
    /// Available beans for filter selection.
    /// </summary>
    public List<BeanFilterOptionDto> AvailableBeans { get; set; } = new();
    
    /// <summary>
    /// Available people for "Made For" filter selection.
    /// </summary>
    public List<UserProfileDto> AvailablePeople { get; set; } = new();
    
    /// <summary>
    /// Callback invoked when user applies filters.
    /// </summary>
    public Action<ShotFilterCriteria>? OnFiltersApplied { get; set; }
    
    /// <summary>
    /// Callback invoked when user clears all filters.
    /// </summary>
    public Action? OnFiltersCleared { get; set; }

    public ShotFilterPopup()
    {
        Title = "Filter Shots";
        ActionButtonText = "Apply";
        ShowActionButton = true;
        
        _workingFilters = new ShotFilterCriteria();
        
        // Create layouts for chips
        _beanChipsLayout = new Controls.FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
            AlignContent = Microsoft.Maui.Layouts.FlexAlignContent.Start
        };
        
        _peopleChipsLayout = new Controls.FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
            AlignContent = Microsoft.Maui.Layouts.FlexAlignContent.Start
        };
        
        _ratingChipsLayout = new Controls.FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
            AlignContent = Microsoft.Maui.Layouts.FlexAlignContent.Start
        };
        
        _clearButton = new Controls.Button
        {
            Text = "Clear All",
            BackgroundColor = Colors.Transparent,
            TextColor = AppColors.Dark.Primary,
            FontSize = 14,
            HeightRequest = 44,
            HorizontalOptions = Controls.LayoutOptions.Center
        };
        _clearButton.Clicked += OnClearClicked;
        
        ActionButtonCommand = new Controls.Command(() => ApplyFilters());
    }

    /// <summary>
    /// Build the popup content. Must be called after setting all properties.
    /// </summary>
    public void Build()
    {
        // Only clone on first build, preserve working state on rebuilds
        if (_workingFilters == null || !_isInitialized)
        {
            _workingFilters = CurrentFilters.Clone();
            _isInitialized = true;
        }
        
        // Build bean chips
        _beanChipsLayout.Children.Clear();
        foreach (var bean in AvailableBeans)
        {
            _beanChipsLayout.Children.Add(CreateChip(
                bean.Name,
                _workingFilters.BeanIds.Contains(bean.Id),
                () => ToggleBeanSelection(bean.Id)
            ));
        }
        
        // Build people chips
        _peopleChipsLayout.Children.Clear();
        foreach (var person in AvailablePeople)
        {
            _peopleChipsLayout.Children.Add(CreateChip(
                person.Name,
                _workingFilters.MadeForIds.Contains(person.Id),
                () => ToggleMadeForSelection(person.Id)
            ));
        }
        
        // Build rating chips (0-4 scale per constitution)
        _ratingChipsLayout.Children.Clear();
        var ratingLabels = new[] { "Terrible (0)", "Bad (1)", "Average (2)", "Good (3)", "Excellent (4)" };
        var ratingIcons = new[] 
        { 
            MaterialSymbolsFont.Sentiment_very_dissatisfied,
            MaterialSymbolsFont.Sentiment_dissatisfied,
            MaterialSymbolsFont.Sentiment_neutral,
            MaterialSymbolsFont.Sentiment_satisfied,
            MaterialSymbolsFont.Sentiment_very_satisfied
        };
        
        for (int i = 0; i <= 4; i++)
        {
            var rating = i;
            _ratingChipsLayout.Children.Add(CreateRatingChip(
                ratingIcons[i],
                rating,
                _workingFilters.Ratings.Contains(rating),
                () => ToggleRatingSelection(rating)
            ));
        }
        
        UpdateClearButtonState();
        
        PopupContent = new Controls.ScrollView
        {
            Content = new Controls.VerticalStackLayout
            {
                Spacing = 16,
                Padding = new Thickness(HorizontalPadding, 0),
                Children =
                {
                    CreateSectionLabel("Beans"),
                    AvailableBeans.Any() 
                        ? _beanChipsLayout 
                        : CreateEmptyLabel("No beans with shots"),
                    
                    CreateSectionLabel("Made For"),
                    AvailablePeople.Any() 
                        ? _peopleChipsLayout 
                        : CreateEmptyLabel("No people with shots"),
                    
                    CreateSectionLabel("Rating"),
                    _ratingChipsLayout,
                    
                    new Controls.BoxView { HeightRequest = 8 },
                    _clearButton
                }
            }
        };
    }
    
    private Controls.Label CreateSectionLabel(string text)
    {
        return new Controls.Label
        {
            Text = text,
            FontSize = 14,
            FontAttributes = Controls.FontAttributes.Bold,
            TextColor = AppColors.Dark.TextPrimary,
            Margin = new Thickness(0, 8, 0, 4)
        };
    }
    
    private Controls.Label CreateEmptyLabel(string text)
    {
        return new Controls.Label
        {
            Text = text,
            FontSize = 14,
            TextColor = AppColors.Dark.TextSecondary,
            FontAttributes = Controls.FontAttributes.Italic
        };
    }
    
    private Controls.View CreateChip(string label, bool isSelected, Action onTap)
    {
        var backgroundColor = isSelected ? AppColors.Dark.Primary : AppColors.Dark.SurfaceVariant;
        var textColor = isSelected ? AppColors.Dark.OnPrimary : AppColors.Dark.TextPrimary;
        
        var border = new Controls.Border
        {
            BackgroundColor = backgroundColor,
            StrokeThickness = 1,
            Stroke = isSelected ? AppColors.Dark.Primary : AppColors.Dark.Outline,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = ChipCornerRadius },
            Padding = new Thickness(12, 0),
            HeightRequest = ChipHeight,
            MinimumWidthRequest = 60,
            Margin = new Thickness(0, 0, ChipSpacing, ChipSpacing),
            Content = new Controls.Label
            {
                Text = label,
                TextColor = textColor,
                FontSize = 14,
                VerticalOptions = Controls.LayoutOptions.Center,
                HorizontalOptions = Controls.LayoutOptions.Center
            }
        };
        
        var tapGesture = new Controls.TapGestureRecognizer();
        tapGesture.Tapped += (s, e) => onTap();
        border.GestureRecognizers.Add(tapGesture);
        
        return border;
    }
    
    private Controls.View CreateRatingChip(string iconGlyph, int rating, bool isSelected, Action onTap)
    {
        var backgroundColor = isSelected ? AppColors.Dark.Primary : AppColors.Dark.SurfaceVariant;
        var iconColor = isSelected ? AppColors.Dark.OnPrimary : AppColors.Dark.TextPrimary;
        
        var border = new Controls.Border
        {
            BackgroundColor = backgroundColor,
            StrokeThickness = 1,
            Stroke = isSelected ? AppColors.Dark.Primary : AppColors.Dark.Outline,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = ChipCornerRadius },
            Padding = new Thickness(8, 0),
            HeightRequest = ChipHeight,
            WidthRequest = ChipHeight + 16,
            Margin = new Thickness(0, 0, ChipSpacing, ChipSpacing),
            Content = new Controls.Label
            {
                Text = iconGlyph,
                FontFamily = MaterialSymbolsFont.FontFamily,
                TextColor = iconColor,
                FontSize = 24,
                VerticalOptions = Controls.LayoutOptions.Center,
                HorizontalOptions = Controls.LayoutOptions.Center
            }
        };
        
        // Add accessible name
        Controls.AutomationProperties.SetName(border, $"Rating {rating}");
        
        var tapGesture = new Controls.TapGestureRecognizer();
        tapGesture.Tapped += (s, e) => onTap();
        border.GestureRecognizers.Add(tapGesture);
        
        return border;
    }
    
    private void ToggleBeanSelection(int beanId)
    {
        if (_workingFilters.BeanIds.Contains(beanId))
            _workingFilters.BeanIds.Remove(beanId);
        else
            _workingFilters.BeanIds.Add(beanId);
        
        Build(); // Rebuild to update chip states
    }
    
    private void ToggleMadeForSelection(int personId)
    {
        if (_workingFilters.MadeForIds.Contains(personId))
            _workingFilters.MadeForIds.Remove(personId);
        else
            _workingFilters.MadeForIds.Add(personId);
        
        Build();
    }
    
    private void ToggleRatingSelection(int rating)
    {
        if (_workingFilters.Ratings.Contains(rating))
            _workingFilters.Ratings.Remove(rating);
        else
            _workingFilters.Ratings.Add(rating);
        
        Build();
    }
    
    private void UpdateClearButtonState()
    {
        _clearButton.IsEnabled = _workingFilters.HasFilters;
        _clearButton.TextColor = _workingFilters.HasFilters 
            ? AppColors.Dark.Primary 
            : AppColors.Dark.TextSecondary;
    }
    
    private async void OnClearClicked(object? sender, EventArgs e)
    {
        _workingFilters.Clear();
        await IPopupService.Current.PopAsync();
        OnFiltersCleared?.Invoke();
    }
    
    private async void ApplyFilters()
    {
        await IPopupService.Current.PopAsync();
        OnFiltersApplied?.Invoke(_workingFilters);
    }
}
