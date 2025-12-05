using MauiReactor;
using BaristaNotes.Services;
using BaristaNotes.Styles;

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

                    // Theme selection
                    Border(
                        VStack(spacing: 12,
                            RenderThemeOption(ThemeMode.Light, "☀️", "Light", "Always use light theme"),
                            RenderThemeOption(ThemeMode.Dark, "🌙", "Dark", "Always use dark theme"),
                            RenderThemeOption(ThemeMode.System, "⚙️", "System", "Follow device theme")
                        )
                        .Padding(16)
                    )
                    .ThemeKey(ThemeKeys.CardBorder)
                    .Margin(16, 0),

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

    VisualNode RenderThemeOption(ThemeMode mode, string emoji, string title, string description)
    {
        var isSelected = State.CurrentThemeMode == mode;

        return Border(
            Grid("*", "Auto,*,Auto",
                Label(emoji)
                    .FontSize(24)
                    .VCenter(),
                VStack(spacing: 4,
                    Label(title)
                        .FontSize(16)
                        .FontAttributes(isSelected ? Microsoft.Maui.Controls.FontAttributes.Bold : Microsoft.Maui.Controls.FontAttributes.None),
                    Label(description)
                        .ThemeKey(ThemeKeys.TextSecondary)
                )
                .VCenter()
                .GridColumn(1),
                Label(isSelected ? "✓" : "")
                    .FontSize(20)
                    .ThemeKey(ThemeKeys.PrimaryText)
                    .GridColumn(2)
                    .VCenter()
            )
            .ColumnSpacing(AppSpacing.L)
            .Padding(12)
        )
        .ThemeKey(isSelected ? ThemeKeys.SelectedCard : ThemeKeys.Card)
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
