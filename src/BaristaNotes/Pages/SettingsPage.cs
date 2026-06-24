using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using Application = Microsoft.Maui.Controls.Application;

namespace BaristaNotes.Pages;

class SettingsPageState
{
    public bool IsLoading { get; set; }
    public ThemeMode CurrentThemeMode { get; set; } = ThemeMode.System;
    public BaristaNotes.Core.Models.Enums.TemperatureUnit TemperatureUnit { get; set; }
        = BaristaNotes.Core.Models.Enums.TemperatureUnit.Fahrenheit;
}

partial class SettingsPage : Component<SettingsPageState>
{
    [Inject]
    IThemeService _themeService;

    [Inject]
    BaristaNotes.Core.Services.IPreferencesService _preferencesService;

    protected override void OnMounted()
    {
        base.OnMounted();
        _ = LoadCurrentTheme();
        SetState(s => s.TemperatureUnit = _preferencesService.GetTemperatureUnit());
    }

    async Task LoadCurrentTheme()
    {
        var mode = await _themeService.GetThemeModeAsync();
        SetState(s => s.CurrentThemeMode = mode);
    }

    async Task OnThemeSelected(ThemeMode mode)
    {
        await _themeService.SetThemeModeAsync(mode);
        SetState(s => s.CurrentThemeMode = mode);
    }

    void OnTempUnitSelected(BaristaNotes.Core.Models.Enums.TemperatureUnit unit)
    {
        _preferencesService.SetTemperatureUnit(unit);
        SetState(s => s.TemperatureUnit = unit);
    }

    async Task OpenVoiceFromSettingsAsync()
    {
        ShotLoggingGridPage.OpenVoiceOnNextMount = true;
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//shots");
    }

    // ============================================================
    // Rendering
    // ============================================================

    public override VisualNode Render()
    {
        return ContentPage("Settings",
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
        .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Never))
        .OnAppearing(() => _ = LoadCurrentTheme());
    }

    // ------------------------------------------------------------
    // Theme helpers (mirror ShotLoggingGridPage / ActivityFeedPage)
    // ------------------------------------------------------------

    static bool IsLight() => Application.Current?.RequestedTheme != AppTheme.Dark;
    static Color SurfaceColor() => IsLight() ? AppColors.Light.Surface : AppColors.Dark.Surface;
    static Color SurfaceVariantColor() => IsLight() ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;
    static Color TextPrimary() => IsLight() ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
    static Color TextSecondary() => IsLight() ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
    static Color AccentColor() => IsLight() ? AppColors.Light.Primary : AppColors.Dark.Primary;
    static Color DividerColor() => IsLight() ? AppColors.Light.Outline : AppColors.Dark.Outline;

    // ------------------------------------------------------------
    // Header tile
    // ------------------------------------------------------------

    VisualNode HeaderTile()
    {
        var modeLabel = State.CurrentThemeMode switch
        {
            ThemeMode.Light => "Light theme",
            ThemeMode.Dark => "Dark theme",
            _ => "System theme"
        };

        return Border(
            Grid(rows: "Auto,*", columns: "*",
                Label("SETTINGS")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0),
                Label(modeLabel)
                    .FontSize(28)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
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
    // Body — scrolling stack of section tiles
    // ------------------------------------------------------------

    VisualNode RenderBody()
    {
        return Grid("*", "*",
            ScrollView(
                Grid(
                    rows: "Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,*",
                    columns: "*",
                    SectionLabel("APPEARANCE").GridRow(0),
                    ThemePickerRow().GridRow(1),
                    SectionLabel("UNITS").GridRow(2),
                    TempUnitPickerRow().GridRow(3),
                    SectionLabel("MANAGE").GridRow(4),
                    ManageTile("EQUIPMENT", "Machines, grinders, accessories",
                        async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("equipment")).GridRow(5),
                    ManageTile("BEANS", "Coffee beans and roasters",
                        async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("beans")).GridRow(6),
                    ManageTile("PROFILES", "Coffee lovers",
                        async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("profiles")).GridRow(7),
                    SectionLabel("ABOUT").GridRow(8),
                    AboutTile().GridRow(9),
                    Border()
                        .BackgroundColor(SurfaceColor())
                        .StrokeThickness(0)
                        .StrokeShape(new Rectangle())
                        .MinimumHeightRequest(16)
                        .VerticalOptions(LayoutOptions.Fill)
                        .GridRow(10)
                )
                .RowSpacing(1)
                .BackgroundColor(DividerColor())
            )
        )
        .BackgroundColor(SurfaceColor());
    }

    VisualNode SectionLabel(string text)
    {
        return Border(
            Label(text)
                .FontSize(10)
                .CharacterSpacing(2)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(TextSecondary())
                .VEnd()
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .Padding(16, 24, 16, 10);
    }

    VisualNode ThemePickerRow()
    {
        return Grid(rows: "Auto", columns: "*,*,*",
            ThemeOptionTile(ThemeMode.Light, "LIGHT").GridColumn(0),
            ThemeOptionTile(ThemeMode.Dark, "DARK").GridColumn(1),
            ThemeOptionTile(ThemeMode.System, "AUTO").GridColumn(2)
        )
        .ColumnSpacing(1)
        .BackgroundColor(DividerColor());
    }

    VisualNode ThemeOptionTile(ThemeMode mode, string label)
    {
        var isSelected = State.CurrentThemeMode == mode;
        var bg = isSelected ? TextPrimary() : SurfaceColor();
        var fg = isSelected ? SurfaceColor() : TextPrimary();

        return Border(
            Grid(rows: "Auto,*", columns: "*",
                Label(label)
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(fg.WithAlpha(0.7f))
                    .GridRow(0),
                Label(isSelected ? "●" : "○")
                    .FontSize(28)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(fg)
                    .VEnd()
                    .GridRow(1)
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(bg)
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(96)
        .OnTapped(async () => await OnThemeSelected(mode));
    }

    VisualNode TempUnitPickerRow()
    {
        return Grid(rows: "Auto", columns: "*,*",
            TempUnitOptionTile(BaristaNotes.Core.Models.Enums.TemperatureUnit.Fahrenheit, "FAHRENHEIT", "°F").GridColumn(0),
            TempUnitOptionTile(BaristaNotes.Core.Models.Enums.TemperatureUnit.Celsius, "CELSIUS", "°C").GridColumn(1)
        )
        .ColumnSpacing(1)
        .BackgroundColor(DividerColor());
    }

    VisualNode TempUnitOptionTile(BaristaNotes.Core.Models.Enums.TemperatureUnit unit, string label, string glyph)
    {
        var isSelected = State.TemperatureUnit == unit;
        var bg = isSelected ? TextPrimary() : SurfaceColor();
        var fg = isSelected ? SurfaceColor() : TextPrimary();

        return Border(
            Grid(rows: "Auto,*", columns: "*",
                Label(label)
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(fg.WithAlpha(0.7f))
                    .GridRow(0),
                Label(glyph)
                    .FontSize(28)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(fg)
                    .VEnd()
                    .GridRow(1)
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(bg)
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(96)
        .OnTapped(() => OnTempUnitSelected(unit));
    }

    VisualNode ManageTile(string label, string subtitle, Action onTapped)
    {
        return Border(
            Grid(rows: "Auto,Auto", columns: "*,Auto",
                Label(label)
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0).GridColumn(0),
                Label(subtitle)
                    .FontSize(20)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .LineBreakMode(LineBreakMode.TailTruncation)
                    .GridRow(1).GridColumn(0),
                Label("→")
                    .FontSize(24)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .VCenter()
                    .GridRow(0).GridRowSpan(2).GridColumn(1)
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(80)
        .OnTapped(onTapped);
    }

    VisualNode AboutTile()
    {
        return Border(
            Grid(rows: "Auto,Auto,Auto", columns: "*",
                Label("BARISTANOTES")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0),
                Label("Version 1.0")
                    .FontSize(18)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .GridRow(1),
                Label("Track your espresso journey")
                    .FontSize(13)
                    .TextColor(TextSecondary())
                    .GridRow(2)
            )
            .Padding(16, 14, 16, 18)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(96);
    }

    // ------------------------------------------------------------
    // Bottom nav row
    // ------------------------------------------------------------

    VisualNode BottomNavRow()
    {
        return Grid(rows: "Auto", columns: "*,*,*",
            NavTile(AppIcons.CoffeeCup,
                async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//shots"))
                .GridColumn(0),
            NavTile(AppIcons.Feed,
                async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//history"))
                .GridColumn(1),
            NavTile(AppIcons.Voice,
                async () => await OpenVoiceFromSettingsAsync())
                .GridColumn(2)
        )
        .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None))
        .ColumnSpacing(1)
        .BackgroundColor(DividerColor());
    }

    VisualNode NavTile(FontImageSource imageSource, Action onTap)
    {
        return Border(
            Image()
                .Source(imageSource)
                .HCenter()
                .VCenter()
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(72)
        .Padding(16, 18, 16, 30)
        .OnTapped(onTap);
    }
}
