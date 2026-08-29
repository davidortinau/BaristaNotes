using BaristaNotes.Core.Models;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using Application = Microsoft.Maui.Controls.Application;

namespace BaristaNotes.Pages;

class ValueRangeSettingsPageProps
{
    public DrinkValueMetric Metric { get; set; } = DrinkValueMetric.DoseIn;
}

class ValueRangeSettingsPageState
{
    public ValueRangeMode Mode { get; set; }
    public int OverrideCount { get; set; }
    public string? LoadWarning { get; set; }
}

partial class ValueRangeSettingsPage : Component<ValueRangeSettingsPageState, ValueRangeSettingsPageProps>
{
    [Inject] IDrinkValueRangeService _rangeService;

    protected override void OnMounted()
    {
        base.OnMounted();
        _rangeService.SettingsChanged += OnRangeSettingsChanged;
        Reload();
    }

    protected override void OnWillUnmount()
    {
        _rangeService.SettingsChanged -= OnRangeSettingsChanged;
        base.OnWillUnmount();
    }

    void OnRangeSettingsChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(Reload);

    void Reload()
    {
        var snapshot = _rangeService.GetSettings();
        SetState(s =>
        {
            s.Mode = snapshot.Modes.GetValueOrDefault(Props.Metric, ValueRangeMode.Auto);
            s.OverrideCount = snapshot.Overrides.Count(item => item.Metric == Props.Metric);
            s.LoadWarning = snapshot.LoadWarning;
        });
    }

    void SelectMode(ValueRangeMode mode)
    {
        _rangeService.SetMode(Props.Metric, mode);
        Reload();
    }

    async Task OpenEditorAsync(BrewMethod method)
    {
        if (State.Mode != ValueRangeMode.Custom)
        {
            return;
        }

        await MauiControls.Shell.Current.GoToAsync<ValueRangeEditorPageProps>(
            "value-range-editor",
            props =>
            {
                props.Metric = Props.Metric;
                props.Method = method;
            });
    }

    async Task ResetAllAsync()
    {
        if (State.OverrideCount == 0)
        {
            return;
        }

        var confirmed = ContainerPage is not null
            && await ContainerPage.DisplayAlertAsync(
                "Reset custom ranges?",
                $"Remove all custom {DrinkValueRangeFormatting.MetricTitle(Props.Metric).ToLowerInvariant()} ranges?",
                "Reset",
                "Cancel");
        if (!confirmed)
        {
            return;
        }

        _rangeService.ResetOverrides(Props.Metric);
        Reload();
    }

    public override VisualNode Render()
    {
        var title = $"{DrinkValueRangeFormatting.MetricTitle(Props.Metric)} Ranges";

        return ContentPage(title,
            Grid(rows: "Auto,*,Auto", columns: "*",
                HeaderTile(title).GridRow(0),
                RenderBody().GridRow(1),
                BottomNavRow().GridRow(2)
            )
            .RowSpacing(1)
            .BackgroundColor(DividerColor())
            .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None))
        )
        .Set(MauiControls.Shell.NavBarIsVisibleProperty, false)
        .Set(MauiControls.Shell.TabBarIsVisibleProperty, false)
        .OniOS(_ => _.Set(
            MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty,
            LargeTitleDisplayMode.Never))
        .OnAppearing(Reload);
    }

    VisualNode HeaderTile(string title) =>
        Border(
            Grid(rows: "Auto,*", columns: "*",
                Label("VALUE RANGES")
                    .FontSize(AppFontSizes.Caption)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0),
                Label(title)
                    .FontSize(AppFontSizes.TitleLarge)
                    .FontFamily("ManropeSemibold")
                    .TextColor(TextPrimary())
                    .VEnd()
                    .GridRow(1)
            )
            .Padding(AppSpacing.M, 14, AppSpacing.M, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(120);

    VisualNode RenderBody() =>
        ScrollView(
            VStack(spacing: 1,
                ModeHelpTile(),
                ModePickerRow(),
                State.LoadWarning is not null
                    ? WarningTile(State.LoadWarning)
                    : Border().HeightRequest(0),
                BrewMethodExtensions.All.Select(MethodTile).ToArray(),
                State.OverrideCount > 0
                    ? ResetAllTile()
                    : Border().HeightRequest(AppSpacing.M)
            )
            .BackgroundColor(DividerColor())
        )
        .BackgroundColor(SurfaceColor());

    VisualNode ModeHelpTile() =>
        Border(
            VStack(spacing: AppSpacing.XS,
                Label("MODE")
                    .FontSize(AppFontSizes.Caption)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary()),
                Label(State.Mode == ValueRangeMode.Auto
                        ? "Uses recommended ranges for each drink method."
                        : "Edited methods use custom ranges. Other methods stay automatic.")
                    .FontSize(AppFontSizes.BodySmall)
                    .TextColor(TextPrimary())
            )
            .Padding(AppSpacing.M, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle());

    VisualNode ModePickerRow() =>
        Grid(rows: "Auto", columns: "*,*",
            ModeTile(ValueRangeMode.Auto, "AUTO").GridColumn(0),
            ModeTile(ValueRangeMode.Custom, "CUSTOM").GridColumn(1)
        )
        .ColumnSpacing(1)
        .BackgroundColor(DividerColor());

    VisualNode ModeTile(ValueRangeMode mode, string label)
    {
        var selected = State.Mode == mode;
        var background = selected ? TextPrimary() : SurfaceColor();
        var foreground = selected ? SurfaceColor() : TextPrimary();

        return Button(label)
        .FontSize(AppFontSizes.BodySmall)
        .CharacterSpacing(2)
        .FontFamily("ManropeSemibold")
        .TextColor(foreground)
        .BackgroundColor(background)
        .CornerRadius(0)
        .BorderWidth(0)
        .MinimumHeightRequest(56)
        .AutomationId($"RangeMode_{mode}")
        .OnClicked(() => SelectMode(mode));
    }

    VisualNode MethodTile(BrewMethod method)
    {
        var effective = _rangeService.Resolve(Props.Metric, method);
        var source = effective.Source switch
        {
            ValueRangeSource.Custom => "CUSTOM",
            ValueRangeSource.AutoFallback => "AUTO FALLBACK",
            _ => "AUTO",
        };

        var tile = new AdaptiveTwoLineTile(
            Label(method.DisplayName().ToUpperInvariant())
                .FontSize(AppFontSizes.Caption)
                .CharacterSpacing(1.5)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(TextSecondary())
                .LineBreakMode(LineBreakMode.TailTruncation)
                .MaxLines(1),
            Label(DrinkValueRangeFormatting.FormatRange(Props.Metric, effective.Range))
                .FontSize(AppFontSizes.BodyLarge)
                .FontFamily("ManropeSemibold")
                .TextColor(TextPrimary())
                .LineBreakMode(LineBreakMode.TailTruncation)
                .MaxLines(1))
        .Trailing(
            AdaptiveTwoLineTile.StatusWithChevron(
                source,
                effective.Source == ValueRangeSource.Custom
                    ? AccentColor()
                    : TextSecondary(),
                State.Mode == ValueRangeMode.Custom
                    ? TextPrimary()
                    : TextSecondary().WithAlpha(0.35f)))
        .BackgroundColor(SurfaceColor())
        .AutomationId($"RangeMethod_{method}");

        return State.Mode == ValueRangeMode.Custom
            ? tile.OnTapped(
                $"{method.DisplayName()}. {DrinkValueRangeFormatting.FormatRange(Props.Metric, effective.Range)}. {source}.",
                async () => await OpenEditorAsync(method))
            : tile;
    }

    VisualNode WarningTile(string warning) =>
        Border(
            Label(warning)
                .FontSize(AppFontSizes.BodySmall)
                .TextColor(SurfaceColor())
        )
        .BackgroundColor(AppColors.Warning)
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .Padding(AppSpacing.M, 12);

    VisualNode ResetAllTile() =>
        Button("RESET ALL CUSTOM RANGES")
        .FontSize(AppFontSizes.BodySmall)
        .CharacterSpacing(1)
        .FontFamily("ManropeSemibold")
        .TextColor(AppColors.Error)
        .BackgroundColor(SurfaceColor())
        .BorderWidth(0)
        .CornerRadius(0)
        .MinimumHeightRequest(56)
        .OnClicked(async () => await ResetAllAsync());

    VisualNode BottomNavRow() =>
        Button("BACK")
        .FontSize(AppFontSizes.BodyLarge)
        .FontFamily("ManropeSemibold")
        .CharacterSpacing(1)
        .TextColor(TextPrimary())
        .BackgroundColor(SurfaceColor())
        .BorderWidth(0)
        .CornerRadius(0)
        .MinimumHeightRequest(72)
        .Padding(AppSpacing.S, 18, AppSpacing.S, 30)
        .OnClicked(async () => await MauiControls.Shell.Current.GoToAsync(".."));

    static bool IsLight() => Application.Current?.RequestedTheme != AppTheme.Dark;
    static Color SurfaceColor() => IsLight() ? AppColors.Light.Surface : AppColors.Dark.Surface;
    static Color TextPrimary() => IsLight() ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
    static Color TextSecondary() => IsLight() ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
    static Color AccentColor() => IsLight() ? AppColors.Light.Primary : AppColors.Dark.Primary;
    static Color DividerColor() => IsLight() ? AppColors.Light.Outline : AppColors.Dark.Outline;
}
