using MauiReactor;
using MauiReactor.Shapes;

namespace BaristaNotes.Styles;

class ApplicationTheme : Theme
{
    // Cached brush objects for performance (T021)
    private static Brush? _primaryBrush;
    private static Brush? _surfaceBrush;
    private static Brush? _backgroundBrush;
    
    public static Brush PrimaryBrush => _primaryBrush ??= new SolidColorBrush(
        IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary);
    
    public static Brush SurfaceBrush => _surfaceBrush ??= new SolidColorBrush(
        IsLightTheme ? AppColors.Light.Surface : AppColors.Dark.Surface);
    
    public static Brush BackgroundBrush => _backgroundBrush ??= new SolidColorBrush(
        IsLightTheme ? AppColors.Light.Background : AppColors.Dark.Background);

    protected override void OnApply()
    {
        // Reset cached brushes when theme changes
        _primaryBrush = null;
        _surfaceBrush = null;
        _backgroundBrush = null;
        
        ActivityIndicatorStyles.Default = _ =>
            _.Color(IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary);

        IndicatorViewStyles.Default = _ => _
            .IndicatorColor(IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted)
            .SelectedIndicatorColor(IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary);

        BorderStyles.Default = _ => _
            .Stroke(IsLightTheme ? AppColors.Light.Outline : AppColors.Dark.Outline)
            .StrokeShape(new Rectangle())
            .StrokeThickness(1);

        BoxViewStyles.Default = _ => _
            .BackgroundColor(IsLightTheme ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant);

        ButtonStyles.Default = _ => _
            .TextColor(IsLightTheme ? AppColors.Light.OnPrimary : AppColors.Dark.OnPrimary)
            .BackgroundColor(IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary)
            .FontFamily("Manrope")
            .FontSize(14)
            .BorderWidth(0)
            .CornerRadius(8)
            .Padding(14, 10)
            .MinimumHeightRequest(44)
            .MinimumWidthRequest(44)
            .VisualState("CommonStates", "Disable", MauiControls.Button.TextColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted)
            .VisualState("CommonStates", "Disable", MauiControls.Button.BackgroundColorProperty, IsLightTheme ? AppColors.Light.Outline : AppColors.Dark.Outline);

        CheckBoxStyles.Default = _ => _
            .Color(IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary)
            .MinimumHeightRequest(44)
            .MinimumWidthRequest(44)
            .VisualState("CommonStates", "Disable", MauiControls.CheckBox.ColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted);

        DatePickerStyles.Default = _ => _
            .TextColor(IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary)
            .BackgroundColor(Colors.Transparent)
            .FontFamily("Manrope")
            .FontSize(14)
            .MinimumHeightRequest(44)
            .MinimumWidthRequest(44)
            .VisualState("CommonStates", "Disable", MauiControls.DatePicker.TextColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted);

        EditorStyles.Default = _ => _
            .TextColor(IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary)
            .BackgroundColor(Colors.Transparent)
            .FontFamily("Manrope")
            .FontSize(14)
            .PlaceholderColor(IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted)
            .MinimumHeightRequest(44)
            .MinimumWidthRequest(44)
            .VisualState("CommonStates", "Disable", MauiControls.Editor.TextColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted);

        EntryStyles.Default = _ => _
            .TextColor(IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary)
            .BackgroundColor(Colors.Transparent)
            .FontFamily("Manrope")
            .FontSize(14)
            .PlaceholderColor(IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted)
            .MinimumHeightRequest(44)
            .MinimumWidthRequest(44)
            .VisualState("CommonStates", "Disable", MauiControls.Entry.TextColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted);

        ImageButtonStyles.Default = _ => _
            .Opacity(1)
            .BorderColor(Colors.Transparent)
            .BorderWidth(0)
            .CornerRadius(0)
            .MinimumHeightRequest(44)
            .MinimumWidthRequest(44)
            .VisualState("CommonStates", "Disable", MauiControls.ImageButton.OpacityProperty, 0.5);

        LabelStyles.Default = _ => _
            .TextColor(IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary)
            .BackgroundColor(Colors.Transparent)
            .FontFamily("Manrope")
            .FontSize(14)
            .VisualState("CommonStates", "Disable", MauiControls.Label.TextColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted);

        LabelStyles.Themes["Headline"] = _ => _
            .TextColor(IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary)
            .FontSize(32)
            .HorizontalOptions(LayoutOptions.Center)
            .HorizontalTextAlignment(TextAlignment.Center);

        LabelStyles.Themes["SubHeadline"] = _ => _
            .TextColor(IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary)
            .FontSize(24)
            .HorizontalOptions(LayoutOptions.Center)
            .HorizontalTextAlignment(TextAlignment.Center);

        PickerStyles.Default = _ => _
            .TextColor(IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary)
            .TitleColor(IsLightTheme ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary)
            .BackgroundColor(Colors.Transparent)
            .FontFamily("Manrope")
            .FontSize(14)
            .MinimumHeightRequest(44)
            .MinimumWidthRequest(44)
            .VisualState("CommonStates", "Disable", MauiControls.Picker.TextColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted)
            .VisualState("CommonStates", "Disable", MauiControls.Picker.TitleColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted);

        ProgressBarStyles.Default = _ => _
            .ProgressColor(IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary)
            .VisualState("CommonStates", "Disable", MauiControls.ProgressBar.ProgressColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted);

        RadioButtonStyles.Default = _ => _
            .BackgroundColor(Colors.Transparent)
            .TextColor(IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary)
            .FontFamily("Manrope")
            .FontSize(14)
            .MinimumHeightRequest(44)
            .MinimumWidthRequest(44)
            .VisualState("CommonStates", "Disable", MauiControls.RadioButton.TextColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted);

        RefreshViewStyles.Default = _ => _
            .RefreshColor(IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary);

        SearchBarStyles.Default = _ => _
            .TextColor(IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary)
            .PlaceholderColor(IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted)
            .CancelButtonColor(IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted)
            .BackgroundColor(Colors.Transparent)
            .FontFamily("Manrope")
            .FontSize(14)
            .MinimumHeightRequest(44)
            .MinimumWidthRequest(44)
            .VisualState("CommonStates", "Disable", MauiControls.SearchBar.TextColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted)
            .VisualState("CommonStates", "Disable", MauiControls.SearchBar.PlaceholderColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted);

        ShadowStyles.Default = _ => _
            .Radius(15)
            .Opacity(0.5f)
            .Brush(IsLightTheme ? AppColors.Light.Outline : AppColors.Dark.Outline)
            .Offset(new Point(10, 10));

        SliderStyles.Default = _ => _
            .MinimumTrackColor(IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary)
            .MaximumTrackColor(IsLightTheme ? AppColors.Light.Outline : AppColors.Dark.Outline)
            .ThumbColor(IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary)
            .VisualState("CommonStates", "Disable", MauiControls.Slider.MinimumTrackColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted)
            .VisualState("CommonStates", "Disable", MauiControls.Slider.MaximumTrackColorProperty, IsLightTheme ? AppColors.Light.Outline : AppColors.Dark.Outline)
            .VisualState("CommonStates", "Disable", MauiControls.Slider.ThumbColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted);

        SwipeItemStyles.Default = _ => _
            .BackgroundColor(IsLightTheme ? AppColors.Light.Surface : AppColors.Dark.Surface);

        SwitchStyles.Default = _ => _
            .OnColor(IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary)
            .ThumbColor(IsLightTheme ? AppColors.Light.OnPrimary : AppColors.Dark.OnPrimary)
            .VisualState("CommonStates", "Disable", MauiControls.Switch.OnColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted)
            .VisualState("CommonStates", "Disable", MauiControls.Switch.ThumbColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted)
            .VisualState("CommonStates", "On", MauiControls.Switch.OnColorProperty, IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary)
            .VisualState("CommonStates", "On", MauiControls.Switch.ThumbColorProperty, IsLightTheme ? AppColors.Light.OnPrimary : AppColors.Dark.OnPrimary)
            .VisualState("CommonStates", "Off", MauiControls.Switch.ThumbColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted);


        TimePickerStyles.Default = _ => _
            .TextColor(IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary)
            .BackgroundColor(Colors.Transparent)
            .FontFamily("Manrope")
            .FontSize(14)
            .MinimumHeightRequest(44)
            .MinimumWidthRequest(44)
            .VisualState("CommonStates", "Disable", MauiControls.TimePicker.TextColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted);

        TitleBarStyles.Default = _ => _
            .MinimumHeightRequest(32)
            .VisualState("TitleActiveStates", "TitleBarTitleActive", MauiControls.TitleBar.BackgroundColorProperty, Colors.Transparent)
            .VisualState("TitleActiveStates", "TitleBarTitleActive", MauiControls.TitleBar.ForegroundColorProperty, IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary)
            .VisualState("TitleActiveStates", "TitleBarTitleInactive", MauiControls.TitleBar.BackgroundColorProperty, IsLightTheme ? AppColors.Light.Surface : AppColors.Dark.Surface)
            .VisualState("TitleActiveStates", "TitleBarTitleInactive", MauiControls.TitleBar.ForegroundColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted);

        PageStyles.Default = _ => _
            .Padding(0)
            .BackgroundColor(IsLightTheme ? AppColors.Light.Background : AppColors.Dark.Background);

        ShellStyles.Default = _ => _
            .Set(MauiControls.Shell.BackgroundColorProperty, IsLightTheme ? AppColors.Light.Background : AppColors.Dark.Background)
            .Set(MauiControls.Shell.ForegroundColorProperty, IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary)
            .Set(MauiControls.Shell.TitleColorProperty, IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary)
            .Set(MauiControls.Shell.DisabledColorProperty, IsLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted)
            .Set(MauiControls.Shell.UnselectedColorProperty, IsLightTheme ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary)
            .Set(MauiControls.Shell.NavBarHasShadowProperty, false)
            .Set(MauiControls.Shell.TabBarBackgroundColorProperty, IsLightTheme ? AppColors.Light.Surface : AppColors.Dark.Surface)
            .Set(MauiControls.Shell.TabBarForegroundColorProperty, IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary)
            .Set(MauiControls.Shell.TabBarTitleColorProperty, IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary)
            .Set(MauiControls.Shell.TabBarUnselectedColorProperty, IsLightTheme ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary);

        NavigationPageStyles.Default = _ => _
            .Set(MauiControls.NavigationPage.BarBackgroundColorProperty, IsLightTheme ? AppColors.Light.Surface : AppColors.Dark.Surface)
            .Set(MauiControls.NavigationPage.BarTextColorProperty, IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary)
            .Set(MauiControls.NavigationPage.IconColorProperty, IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary);

        TabbedPageStyles.Default = _ => _
            .Set(MauiControls.TabbedPage.BarBackgroundColorProperty, IsLightTheme ? AppColors.Light.Surface : AppColors.Dark.Surface)
            .Set(MauiControls.TabbedPage.BarTextColorProperty, IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary)
            .Set(MauiControls.TabbedPage.UnselectedTabColorProperty, IsLightTheme ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary)
            .Set(MauiControls.TabbedPage.SelectedTabColorProperty, IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary);
    }
}
