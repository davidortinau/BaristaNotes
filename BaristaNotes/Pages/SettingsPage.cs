using MauiReactor;
using MauiReactor.Shapes;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using Fonts;

namespace BaristaNotes.Pages;

class SettingsPageState
{
    public bool IsLoading { get; set; }
    public ThemeMode CurrentThemeMode { get; set; } = ThemeMode.System;
}

partial class SettingsPage : Component<SettingsPageState>
{
    [Inject]
    IThemeService _themeService;

    protected override void OnMounted()
    {
        base.OnMounted();
        _ = LoadCurrentTheme();
    }

    async Task LoadCurrentTheme()
    {
        var mode = await _themeService.GetThemeModeAsync();
        SetState(s => s.CurrentThemeMode = mode);
    }

    public override VisualNode Render()
    {
        return ContentPage("Settings",
            ScrollView(
                VStack(spacing: 16,
                    // Appearance section
                    Label("Appearance")
                        .ThemeKey(ThemeKeys.TextSecondary)
                        .Padding(16, 16, 16, 8),

                    // Theme selection - horizontal compact
                    HStack(spacing: 8,
                        RenderThemeOption(ThemeMode.Light, MaterialSymbolsFont.Light_mode, "Light"),
                        RenderThemeOption(ThemeMode.Dark, MaterialSymbolsFont.Dark_mode, "Dark"),
                        RenderThemeOption(ThemeMode.System, MaterialSymbolsFont.Brightness_auto, "Auto")
                    )
                    .Padding(16, 0),

                    // Manage section header
                    Label("Manage")
                        .ThemeKey(ThemeKeys.TextSecondary)
                        .Padding(16, 24, 16, 8),

                    // Equipment management option
                    RenderSettingsItem(
                        "Equipment",
                        "Manage machines, grinders, and accessories",
                        async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("equipment")),

                    // Bean management option
                    RenderSettingsItem(
                        "Beans",
                        "Manage coffee beans and roasters",
                        async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("beans")),

                    // User Profile management option
                    RenderSettingsItem(
                        "User Profiles",
                        "Manage household members",
                        async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("profiles")),

                    // About section
                    Label("About")
                        .ThemeKey(ThemeKeys.TextSecondary)
                        .Padding(16, 24, 16, 8),

                    Border(
                        VStack(spacing: 8,
                            Label("BaristaNotes")
                                .FontSize(16)
                                .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold),
                            Label("Version 1.0")
                                .ThemeKey(ThemeKeys.TextSecondary),
                            Label("Track your espresso journey")
                                .ThemeKey(ThemeKeys.TextSecondary)
                        )
                        .Padding(16)
                    )
                    .ThemeKey(ThemeKeys.CardBorder)
                    .Margin(16, 0, 16, 16)
                )
            )
        );
    }

    VisualNode RenderThemeOption(ThemeMode mode, string icon, string title)
    {
        var isSelected = State.CurrentThemeMode == mode;
        var isLightTheme = ApplicationTheme.IsLightTheme;
        var primaryColor = isLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary;
        var surfaceColor = isLightTheme ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;
        var textColor = isLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;

        return Border(
            VStack(spacing: 4,
                Label(icon)
                    .FontFamily(MaterialSymbolsFont.FontFamily)
                    .FontSize(24)
                    .TextColor(isSelected ? primaryColor : textColor)
                    .HCenter(),
                Label(title)
                    .FontSize(12)
                    .TextColor(isSelected ? primaryColor : textColor)
                    .HCenter()
            )
            .Padding(16, 8)
        )
        .StrokeShape(new RoundRectangle().CornerRadius(8))
        .BackgroundColor(isSelected ? primaryColor.WithAlpha(0.15f) : surfaceColor)
        .Stroke(isSelected ? primaryColor : Colors.Transparent)
        .StrokeThickness(isSelected ? 2 : 0)
        .OnTapped(async () => await OnThemeSelected(mode));
    }

    async Task OnThemeSelected(ThemeMode mode)
    {
        await _themeService.SetThemeModeAsync(mode);
        SetState(s => s.CurrentThemeMode = mode);
    }

    VisualNode RenderSettingsItem(string title, string description, Action onTapped)
    {
        return Border(
            Grid("*", "*,Auto",
                VStack(spacing: 4,
                    Label(title)
                        .FontSize(16)
                        .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold),
                    Label(description)
                        .ThemeKey(ThemeKeys.TextSecondary)
                )
                .VCenter(),
                Label("›")
                    .FontSize(20)
                    .ThemeKey(ThemeKeys.TextSecondary)
                    .GridColumn(1)
                    .VCenter()
            )
            .Padding(16)
        )
        .ThemeKey(ThemeKeys.CardBorder)
        .Margin(16, 0)
        .OnTapped(onTapped);
    }
}
