using BaristaNotes.Agent.Contracts;
using BaristaNotes.Services.Agent;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using Application = Microsoft.Maui.Controls.Application;

namespace BaristaNotes.Pages;

class SettingsPageState
{
    public bool IsLoading { get; set; }
    public ThemeMode CurrentThemeMode { get; set; } = ThemeMode.System;
    public BaristaNotes.Core.Models.Enums.TemperatureUnit TemperatureUnit { get; set; }
        = BaristaNotes.Core.Models.Enums.TemperatureUnit.Fahrenheit;
    public Dictionary<DrinkValueMetric, ValueRangeMode> RangeModes { get; set; } = [];
    public Dictionary<DrinkValueMetric, int> RangeOverrideCounts { get; set; } = [];

    // Reorder agent (only when the Aspire AppHost started the app)
    public AgentStatus? Agent { get; set; }
    public int? AgentMemoryCount { get; set; }
    public bool AgentFailed { get; set; }
    public bool AgentSaving { get; set; }
    public bool ResettingDemo { get; set; }
}

partial class SettingsPage : Component<SettingsPageState>
{
    [Inject]
    IThemeService _themeService;

    [Inject]
    BaristaNotes.Core.Services.IPreferencesService _preferencesService;

    [Inject]
    IDrinkValueRangeService _rangeService;

    [Inject] AgentApiClient _agentApi;
    [Inject] DemoDataSeeder _demoDataSeeder;
    [Inject] IFeedbackService _feedbackService;
    [Inject] ILogger<SettingsPage> _logger;

#if IOS || ANDROID
    [Inject] BaristaNotes.Core.Services.Origins.NominatimOriginGeocoder _geocoder;
#endif
#if MAUI_PERFORMANCE
    [Inject] MauiPerformanceProbe _performanceProbe;
#endif

    protected override void OnMounted()
    {
        base.OnMounted();
        LoadPreferences();
    }

    void LoadPreferences()
    {
        _ = LoadCurrentTheme();
        _ = LoadAgentAsync();
        var snapshot = _rangeService.GetSettings();
        SetState(s =>
        {
            s.TemperatureUnit = _preferencesService.GetTemperatureUnit();
            s.RangeModes = Enum.GetValues<DrinkValueMetric>()
                .ToDictionary(
                    metric => metric,
                    metric => snapshot.Modes.GetValueOrDefault(metric, ValueRangeMode.Auto));
            s.RangeOverrideCounts = Enum.GetValues<DrinkValueMetric>()
                .ToDictionary(
                    metric => metric,
                    metric => snapshot.Overrides.Count(item => item.Metric == metric));
        });
    }

    async Task LoadCurrentTheme()
    {
        var mode = await _themeService.GetThemeModeAsync();
        SetState(s => s.CurrentThemeMode = mode);
#if MAUI_PERFORMANCE
        _performanceProbe.SettingsReady();
#endif
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

    async Task OpenValueRangesAsync(DrinkValueMetric metric)
    {
        await MauiControls.Shell.Current.GoToAsync<ValueRangeSettingsPageProps>(
            "value-ranges",
            props => props.Metric = metric);
    }

#if IOS || ANDROID
    async Task ChangeGeocoderEndpointAsync()
    {
        var page = ContainerPage ?? throw new InvalidOperationException("Settings page is unavailable.");
        try
        {
            var endpoint = await page.DisplayPromptAsync("Map geocoder",
                "HTTPS Nominatim-compatible search endpoint. Only saved origin text is sent; successful lookups are cached per endpoint.",
                "Save", "Cancel", initialValue: _geocoder.Endpoint);
            if (endpoint is null)
                return;
            _geocoder.Endpoint = endpoint;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not change the origin geocoder endpoint");
            await page.DisplayAlertAsync("Map geocoder", ex.Message, "OK");
        }
    }
#endif

    // ============================================================
    // Reorder agent
    // ============================================================

    async Task LoadAgentAsync()
    {
        if (!_agentApi.IsConnected)
        {
            return;
        }

        try
        {
            var memoryCount = LoadAgentMemoryCountAsync();
            var status = await _agentApi.GetStatusAsync();
            var count = await memoryCount;
            SetState(s =>
            {
                s.Agent = status;
                s.AgentMemoryCount = count;
                s.AgentFailed = false;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load the agent status");
            SetState(s => s.AgentFailed = true);
        }
    }

    async Task<int?> LoadAgentMemoryCountAsync()
    {
        try
        {
            return (await _agentApi.GetMemoryAsync()).Items.Count;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not load the agent memory count");
            return null;
        }
    }

    async Task OnAutonomySelected(Autonomy autonomy)
    {
        if (State.Agent is not { } status || status.Settings.Autonomy == autonomy || State.AgentSaving)
        {
            return;
        }

        await SaveAgentSettingsAsync(status.Settings with { Autonomy = autonomy });
    }

    async Task ChangeBudgetAsync()
    {
        if (State.Agent is not { } status || ContainerPage is not { } page || State.AgentSaving)
        {
            return;
        }

        var input = await page.DisplayPromptAsync(
            "Monthly budget",
            $"How much may the agent spend each month, in {status.Settings.Currency}?",
            accept: "Save",
            cancel: "Cancel",
            maxLength: 8,
            keyboard: Keyboard.Numeric,
            initialValue: status.Settings.MonthlyBudget.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
        if (input is null)
        {
            return;
        }

        if (!AgentText.TryParseBudget(input, out var amount))
        {
            await _feedbackService.ShowErrorAsync(
                $"Enter an amount from 0 to {AgentText.MaxMonthlyBudget:0}.",
                "Use digits only, for example 80 or 57.50.");
            return;
        }

        await SaveAgentSettingsAsync(status.Settings with { MonthlyBudget = amount });
    }

    async Task SaveAgentSettingsAsync(AgentSettings settings)
    {
        var before = State.Agent;
        if (before is null)
        {
            return;
        }

        // Show the change right away. Put it back if the save fails.
        SetState(s =>
        {
            s.Agent = before with { Settings = settings };
            s.AgentSaving = true;
        });

        try
        {
            await _agentApi.SaveSettingsAsync(settings);
            SetState(s => s.AgentSaving = false);
            await LoadAgentAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save the agent settings");
            SetState(s =>
            {
                s.Agent = before;
                s.AgentSaving = false;
            });
            await _feedbackService.ShowErrorAsync("Could not save the agent settings.", "Check that the API is running.");
        }
    }

    Task ConfirmResetDemoAsync() =>
        AgentTiles.ConfirmAsync(
            "Reset demo data?",
            "This replaces the demo beans, bags, and drinks, and clears the agent's runs, picks, orders, check-ins, and memory.",
            "Reset",
            ResetDemoAsync);

    async Task ResetDemoAsync()
    {
        if (State.ResettingDemo)
        {
            return;
        }

        SetState(s => s.ResettingDemo = true);
        try
        {
            await _demoDataSeeder.ResetAsync();
            if (_agentApi.IsConnected)
            {
                await _agentApi.ResetDemoAsync();
            }

            await _feedbackService.ShowSuccessAsync("Demo data reset.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not reset the demo data");
            await _feedbackService.ShowErrorAsync("Could not reset the demo data.", "Check that the API is running, then try again.");
        }
        finally
        {
            SetState(s => s.ResettingDemo = false);
        }

        await LoadAgentAsync();
    }

    static Task GoToAsync(string route) => MauiControls.Shell.Current.GoToAsync(route);

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
        .OnAppearing(LoadPreferences);
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
            .Padding(16, 14, 16, 14)
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
        // Rows are numbered from this list, so sections can be added without
        // renumbering. The last row is the star-sized footer.
        var rows = new List<VisualNode>
        {
            SectionLabel("APPEARANCE"),
            ThemePickerRow(),
        };
        rows.AddRange(AgentRows());
        rows.AddRange(
        [
            SectionLabel("MANAGE"),
            ManageTile("EQUIPMENT", "Machines, grinders, accessories",
                async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("equipment")),
            ManageTile("BEANS", "Coffee beans and roasters",
                async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("beans")),
            ManageTile("PROFILES", "Coffee lovers",
                async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("profiles")),
            SectionLabel("UNITS"),
            TempUnitPickerRow(),
            SectionLabel("VALUE RANGES"),
            ValueRangeTile(DrinkValueMetric.DoseIn),
            ValueRangeTile(DrinkValueMetric.Yield),
            ValueRangeTile(DrinkValueMetric.GrindMicrons),
            ValueRangeTile(DrinkValueMetric.Time),
            SectionLabel("ABOUT"),
            AboutTile(),
        ]);
#if IOS || ANDROID
        rows.Add(ManageTile("MAP GEOCODER", "Change search service endpoint",
            async () => await ChangeGeocoderEndpointAsync()));
#endif
        rows.Add(
            Border()
                .BackgroundColor(SurfaceColor())
                .StrokeThickness(0)
                .StrokeShape(new Rectangle())
                .MinimumHeightRequest(16)
                .VerticalOptions(LayoutOptions.Fill));

        var rowDefinitions = string.Join(",", Enumerable.Repeat("Auto", rows.Count - 1).Append("*"));

        return Grid("*", "*",
            ScrollView(
                Grid(
                    rows: rowDefinitions,
                    columns: "*",
                    rows.Select((row, index) => row.GridRow(index)).ToArray()
                )
                .RowSpacing(1)
                .BackgroundColor(DividerColor())
            )
        )
        .BackgroundColor(SurfaceColor());
    }

    // ------------------------------------------------------------
    // Agent section
    // ------------------------------------------------------------

    IEnumerable<VisualNode> AgentRows()
    {
        yield return SectionLabel("AGENT");

        if (!_agentApi.IsConnected)
        {
            yield return AgentTiles.InfoTile(
                "AGENT NOT CONNECTED",
                "Start the app from the Aspire AppHost.",
                glyph: MaterialSymbolsFont.Info);
        }
        else if (State.Agent is not { } status)
        {
            yield return State.AgentFailed
                ? ManageTile("AGENT UNAVAILABLE", "Tap to try again", async () => await LoadAgentAsync())
                : AgentTiles.InfoTile("AGENT", "Loading\u2026");
        }
        else
        {
            var now = DateTimeOffset.Now;
            var autonomy = status.Settings.Autonomy;

            yield return AutonomyPickerRow(autonomy);
            yield return AgentTiles.TextTile(AgentText.AutonomyDescription(autonomy), secondary: true);
            yield return ManageTile("MONTHLY BUDGET", AgentText.BudgetTile(status),
                async () => await ChangeBudgetAsync());
            yield return ManageTile("ROASTERS", $"{status.Settings.WatchedRoasters.Count} watched",
                async () => await GoToAsync("agent-roasters"));
            yield return ManageTile("AGENT ACTIVITY", AgentText.RunTitle(status.LatestRun, now),
                async () => await GoToAsync("agent"));
            yield return ManageTile("MEMORY",
                State.AgentMemoryCount is { } learned ? AgentText.LearnedTitle(learned) : "What the agent learned",
                async () => await GoToAsync("memory"));
            yield return ManageTile("CHECK-IN", AgentText.CheckInLine(status.CheckIn, now),
                async () => await GoToAsync("checkin"));
        }

        if (AgentApiAddress.IsDemo)
        {
            yield return ManageTile("RESET DEMO DATA",
                State.ResettingDemo ? "Resetting\u2026" : "Start the demo again",
                async () => await ConfirmResetDemoAsync());
        }
    }

    VisualNode AutonomyPickerRow(Autonomy selected)
    {
        return AgentTiles.ActionRow(
            AutonomyOptionTile(Autonomy.Watch, MaterialSymbolsFont.Visibility, selected),
            AutonomyOptionTile(Autonomy.PrepareCart, MaterialSymbolsFont.Shopping_cart, selected),
            AutonomyOptionTile(Autonomy.ReorderFavorites, MaterialSymbolsFont.Quick_reorder, selected));
    }

    VisualNode AutonomyOptionTile(Autonomy autonomy, string glyph, Autonomy selected)
    {
        var isSelected = autonomy == selected;
        return AgentTiles.ActionTile(
            AgentText.AutonomyLabel(autonomy),
            glyph,
            async () => await OnAutonomySelected(autonomy),
            primary: isSelected,
            enabled: !State.AgentSaving || isSelected,
            accessibilityName: $"Autonomy {AgentText.AutonomyLabel(autonomy)}. {AgentText.AutonomyDescription(autonomy)}{(isSelected ? " Selected." : "")}");
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
        return new AdaptiveTwoLineTile(
            Label(label)
                .FontSize(10)
                .CharacterSpacing(2)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(TextSecondary())
                .LineBreakMode(LineBreakMode.TailTruncation)
                .MaxLines(1),
            Label(subtitle)
                .FontSize(20)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(TextPrimary())
                .LineBreakMode(LineBreakMode.TailTruncation)
                .MaxLines(1))
        .Trailing(
            AdaptiveTwoLineTile.DecorativeGlyph(
                MaterialSymbolsFont.Chevron_right,
                TextPrimary()))
        .BackgroundColor(SurfaceColor())
        .AutomationId(AgentTiles.AutomationIdFor(label))
        .OnTapped($"{label}: {subtitle}", onTapped);
    }

    VisualNode ValueRangeTile(DrinkValueMetric metric)
    {
        var mode = State.RangeModes.GetValueOrDefault(metric, ValueRangeMode.Auto);
        var overrideCount = State.RangeOverrideCounts.GetValueOrDefault(metric);
        var subtitle = mode == ValueRangeMode.Auto
            ? "Automatic by drink method"
            : overrideCount == 1
                ? "Custom - 1 drink method"
                : $"Custom - {overrideCount} drink methods";

        return new AdaptiveTwoLineTile(
            Label(DrinkValueRangeFormatting.MetricTitle(metric).ToUpperInvariant())
                .FontSize(10)
                .CharacterSpacing(2)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(TextSecondary())
                .LineBreakMode(LineBreakMode.TailTruncation)
                .MaxLines(1),
            Label(subtitle)
                .FontSize(AppFontSizes.BodyLarge)
                .FontFamily("ManropeSemibold")
                .TextColor(TextPrimary())
                .LineBreakMode(LineBreakMode.TailTruncation)
                .MaxLines(1))
        .Trailing(
            AdaptiveTwoLineTile.StatusWithChevron(
                mode == ValueRangeMode.Auto ? "AUTO" : "CUSTOM",
                mode == ValueRangeMode.Custom ? AccentColor() : TextSecondary(),
                TextPrimary()))
        .BackgroundColor(SurfaceColor())
        .AutomationId($"ValueRange_{metric}")
        .OnTapped(
            $"{DrinkValueRangeFormatting.MetricTitle(metric)}. {subtitle}. {mode}.",
            async () => await OpenValueRangesAsync(metric));
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
