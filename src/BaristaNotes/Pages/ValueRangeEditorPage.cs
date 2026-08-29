using System.Globalization;
using BaristaNotes.Core.Models;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using Application = Microsoft.Maui.Controls.Application;

namespace BaristaNotes.Pages;

class ValueRangeEditorPageProps
{
    public DrinkValueMetric Metric { get; set; } = DrinkValueMetric.DoseIn;
    public BrewMethod Method { get; set; } = BrewMethod.Espresso;
}

class ValueRangeEditorPageState
{
    public string MinimumText { get; set; } = "";
    public string MaximumText { get; set; } = "";
    public string OriginalMinimumText { get; set; } = "";
    public string OriginalMaximumText { get; set; } = "";
    public decimal OriginalMinimumCanonical { get; set; }
    public decimal OriginalMaximumCanonical { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasOverride { get; set; }
    public bool IsSaving { get; set; }
}

partial class ValueRangeEditorPage : Component<ValueRangeEditorPageState, ValueRangeEditorPageProps>
{
    [Inject] IDrinkValueRangeService _rangeService;
    [Inject] ILogger<ValueRangeEditorPage> _logger;

    bool _allowNavigation;
    bool _discardPromptOpen;

    DrinkValueRangeDefinition Definition =>
        BrewMethodValueRangeCatalog.GetDefinition(Props.Method, Props.Metric);

    RangeEditorUnit EditorUnit =>
        DrinkValueRangeFormatting.GetEditorUnit(Props.Metric, Props.Method);

    bool IsDirty =>
        State.MinimumText != State.OriginalMinimumText
        || State.MaximumText != State.OriginalMaximumText;

    protected override void OnMounted()
    {
        base.OnMounted();
        MauiControls.Shell.Current.Navigating += OnShellNavigating;
        LoadValues();
    }

    protected override void OnWillUnmount()
    {
        MauiControls.Shell.Current.Navigating -= OnShellNavigating;
        base.OnWillUnmount();
    }

    async void OnShellNavigating(
        object? sender,
        MauiControls.ShellNavigatingEventArgs e)
    {
        if (_allowNavigation
            || _discardPromptOpen
            || !IsDirty
            || !e.CanCancel)
        {
            return;
        }

        e.Cancel();
        _discardPromptOpen = true;
        try
        {
            if (await ConfirmDiscardAsync())
            {
                _allowNavigation = true;
                await MauiControls.Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            _allowNavigation = false;
            _logger.LogError(ex, "Failed to handle range editor back navigation");
        }
        finally
        {
            _discardPromptOpen = false;
        }
    }

    void LoadValues()
    {
        var custom = _rangeService.GetSettings().Overrides.LastOrDefault(
            item => item.Metric == Props.Metric && item.Method == Props.Method);
        var range = custom is null
            ? Definition.AutoRange
            : new DrinkValueRange(custom.Minimum, custom.Maximum);
        var minimum = DrinkValueRangeFormatting.FormatEditorValue(range.Minimum, EditorUnit);
        var maximum = DrinkValueRangeFormatting.FormatEditorValue(range.Maximum, EditorUnit);

        SetState(s =>
        {
            s.MinimumText = minimum;
            s.MaximumText = maximum;
            s.OriginalMinimumText = minimum;
            s.OriginalMaximumText = maximum;
            s.OriginalMinimumCanonical = range.Minimum;
            s.OriginalMaximumCanonical = range.Maximum;
            s.HasOverride = custom is not null;
            s.ErrorMessage = null;
        });
    }

    void UpdateMinimum(string value)
    {
        SetState(s => s.MinimumText = value);
        ValidateIfComplete(value, State.MaximumText);
    }

    void UpdateMaximum(string value)
    {
        SetState(s => s.MaximumText = value);
        ValidateIfComplete(State.MinimumText, value);
    }

    void ValidateIfComplete(string minimumText, string maximumText)
    {
        if (string.IsNullOrWhiteSpace(minimumText) || string.IsNullOrWhiteSpace(maximumText))
        {
            SetState(s => s.ErrorMessage = null);
            return;
        }

        TryGetCanonicalRange(minimumText, maximumText, out _, out var error);
        SetState(s => s.ErrorMessage = error);
    }

    async Task SaveAsync()
    {
        if (!TryGetCanonicalRange(
                State.MinimumText,
                State.MaximumText,
                out var range,
                out var error))
        {
            SetState(s => s.ErrorMessage = error);
            return;
        }

        SetState(s => s.IsSaving = true);
        try
        {
            _rangeService.SaveOverride(
                Props.Metric,
                Props.Method,
                range.Minimum,
                range.Maximum);
            _allowNavigation = true;
            await MauiControls.Shell.Current.GoToAsync("..");
        }
        catch (ArgumentException ex)
        {
            SetState(s =>
            {
                s.IsSaving = false;
                s.ErrorMessage = ex.Message;
            });
        }
    }

    async Task CancelAsync()
    {
        if (IsDirty && !await ConfirmDiscardAsync())
        {
            return;
        }

        _allowNavigation = true;
        await MauiControls.Shell.Current.GoToAsync("..");
    }

    async Task<bool> ConfirmDiscardAsync()
    {
        if (ContainerPage is null)
        {
            return false;
        }

        return await ContainerPage.DisplayAlertAsync(
            "Discard changes?",
            "Your range changes have not been saved.",
            "Discard",
            "Keep Editing");
    }

    async Task UseRecommendedAsync()
    {
        if (!State.HasOverride)
        {
            var minimum = DrinkValueRangeFormatting.FormatEditorValue(
                Definition.AutoRange.Minimum,
                EditorUnit);
            var maximum = DrinkValueRangeFormatting.FormatEditorValue(
                Definition.AutoRange.Maximum,
                EditorUnit);
            SetState(s =>
            {
                s.MinimumText = minimum;
                s.MaximumText = maximum;
                s.ErrorMessage = null;
            });
            return;
        }

        var confirmed = ContainerPage is not null
            && await ContainerPage.DisplayAlertAsync(
                "Use recommended range?",
                $"Remove the custom {DrinkValueRangeFormatting.MetricTitle(Props.Metric).ToLowerInvariant()} range for {Props.Method.DisplayName()}?",
                "Use Recommended",
                "Cancel");
        if (!confirmed)
        {
            return;
        }

        _rangeService.RemoveOverride(Props.Metric, Props.Method);
        _allowNavigation = true;
        await MauiControls.Shell.Current.GoToAsync("..");
    }

    bool TryGetCanonicalRange(
        string minimumText,
        string maximumText,
        out DrinkValueRange range,
        out string? error)
    {
        range = Definition.AutoRange;

        if (!TryParseDisplayValue(minimumText, out var displayMinimum)
            || !TryParseDisplayValue(maximumText, out var displayMaximum))
        {
            error = $"Enter valid values in {EditorUnit.Label}.";
            return false;
        }

        var minimum = minimumText == State.OriginalMinimumText
            ? State.OriginalMinimumCanonical
            : displayMinimum * EditorUnit.Scale;
        var maximum = maximumText == State.OriginalMaximumText
            ? State.OriginalMaximumCanonical
            : displayMaximum * EditorUnit.Scale;

        if (minimum >= maximum)
        {
            error = "Minimum must be less than maximum.";
            return false;
        }

        if (!Definition.HardRange.Contains(minimum)
            || !Definition.HardRange.Contains(maximum))
        {
            error = $"Use values from {DrinkValueRangeFormatting.FormatRange(Props.Metric, Definition.HardRange)}.";
            return false;
        }

        if (Props.Metric is DrinkValueMetric.DoseIn or DrinkValueMetric.Yield
            && (decimal.Round(minimum, 1) != minimum || decimal.Round(maximum, 1) != maximum))
        {
            error = "Use no more than one decimal place.";
            return false;
        }

        if (Props.Metric is DrinkValueMetric.GrindMicrons or DrinkValueMetric.Time
            && (decimal.Truncate(minimum) != minimum || decimal.Truncate(maximum) != maximum))
        {
            error = "Use values that convert to whole units.";
            return false;
        }

        range = new(minimum, maximum);
        error = null;
        return true;
    }

    static bool TryParseDisplayValue(string text, out decimal value) =>
        decimal.TryParse(
            text,
            NumberStyles.Number,
            CultureInfo.CurrentCulture,
            out value);

    public override VisualNode Render()
    {
        var metric = DrinkValueRangeFormatting.MetricTitle(Props.Metric);

        return ContentPage($"Edit {metric} Range",
            Grid(rows: "Auto,*,Auto", columns: "*",
                HeaderTile(metric).GridRow(0),
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
            LargeTitleDisplayMode.Never));
    }

    VisualNode HeaderTile(string metric) =>
        Border(
            Grid(rows: "Auto,*", columns: "*",
                Label($"CUSTOM {metric.ToUpperInvariant()}")
                    .FontSize(AppFontSizes.Caption)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0),
                Label(Props.Method.DisplayName())
                    .FontSize(AppFontSizes.TitleLarge)
                    .FontFamily("ManropeSemibold")
                    .TextColor(TextPrimary())
                    .VEnd()
                    .GridRow(1)
            )
            .Padding(AppSpacing.M, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(120);

    VisualNode RenderBody() =>
        ScrollView(
            VStack(spacing: 1,
                GuidanceTile(),
                ValueFieldTile(
                    "MINIMUM",
                    State.MinimumText,
                    "Minimum value",
                    UpdateMinimum,
                    "RangeMinimum"),
                ValueFieldTile(
                    "MAXIMUM",
                    State.MaximumText,
                    "Maximum value",
                    UpdateMaximum,
                    "RangeMaximum"),
                State.ErrorMessage is not null
                    ? ErrorTile(State.ErrorMessage)
                    : Border().HeightRequest(0),
                RecommendedTile(),
                Border().HeightRequest(AppSpacing.M)
            )
            .BackgroundColor(DividerColor())
        )
        .BackgroundColor(SurfaceColor());

    VisualNode GuidanceTile() =>
        Border(
            VStack(spacing: AppSpacing.S,
                Label($"ENTER VALUES IN {EditorUnit.Label.ToUpperInvariant()}")
                    .FontSize(AppFontSizes.Caption)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary()),
                Label($"Recommended: {DrinkValueRangeFormatting.FormatRange(Props.Metric, Definition.AutoRange)}")
                    .FontSize(AppFontSizes.BodySmall)
                    .TextColor(TextPrimary()),
                Label($"Allowed: {DrinkValueRangeFormatting.FormatRange(Props.Metric, Definition.HardRange)}")
                    .FontSize(AppFontSizes.BodySmall)
                    .TextColor(TextSecondary())
            )
            .Padding(AppSpacing.M, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle());

    VisualNode ValueFieldTile(
        string label,
        string value,
        string placeholder,
        Action<string> onChanged,
        string automationId) =>
        new AdaptiveTwoLineTile(
            Label(label)
                .FontSize(AppFontSizes.Caption)
                .CharacterSpacing(2)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(TextSecondary()),
            Entry()
                .Text(value)
                .Placeholder(placeholder)
                .Keyboard(Keyboard.Numeric)
                .FontSize(AppFontSizes.TitleMedium)
                .FontFamily("ManropeSemibold")
                .TextColor(TextPrimary())
                .BackgroundColor(Colors.Transparent)
                .AutomationId(automationId)
                .OnTextChanged(onChanged))
        .Trailing(
            Label(EditorUnit.Label)
                .FontSize(AppFontSizes.BodySmall)
                .TextColor(TextSecondary()))
        .BackgroundColor(SurfaceColor())
        .MinimumHeight(96);

    VisualNode ErrorTile(string message) =>
        Border(
            Label(message)
                .FontSize(AppFontSizes.BodySmall)
                .FontFamily("ManropeSemibold")
                .TextColor(SurfaceColor())
        )
        .BackgroundColor(AppColors.Error)
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .Padding(AppSpacing.M, 12)
        .MinimumHeightRequest(56);

    VisualNode RecommendedTile() =>
        new AdaptiveTwoLineTile(
            Label("USE RECOMMENDED RANGE")
                .FontSize(AppFontSizes.Caption)
                .CharacterSpacing(1.5)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(TextSecondary()),
            Label(DrinkValueRangeFormatting.FormatRange(Props.Metric, Definition.AutoRange))
                .FontSize(AppFontSizes.BodyLarge)
                .FontFamily("ManropeSemibold")
                .TextColor(TextPrimary()))
        .Trailing(
            AdaptiveTwoLineTile.DecorativeGlyph(
                MaterialSymbolsFont.Restart_alt,
                AccentColor()))
        .BackgroundColor(SurfaceColor())
        .AutomationId("UseRecommendedRange")
        .OnTapped(
            $"Use recommended range. {DrinkValueRangeFormatting.FormatRange(Props.Metric, Definition.AutoRange)}.",
            async () => await UseRecommendedAsync());

    VisualNode BottomNavRow() =>
        Grid(rows: "Auto", columns: "*,*",
            ActionTile("CANCEL", false, "RangeEditorCancel", async () => await CancelAsync()).GridColumn(0),
            ActionTile(
                State.IsSaving ? "SAVING" : "SAVE",
                true,
                "RangeEditorSave",
                async () =>
            {
                if (!State.IsSaving)
                {
                    await SaveAsync();
                }
            }).GridColumn(1)
        )
        .ColumnSpacing(1)
        .BackgroundColor(DividerColor())
        .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None));

    VisualNode ActionTile(
        string label,
        bool inverted,
        string automationId,
        Action onTap)
    {
        var background = inverted ? TextPrimary() : SurfaceColor();
        var foreground = inverted ? SurfaceColor() : TextPrimary();

        return Button(label)
        .FontSize(AppFontSizes.BodyLarge)
        .FontFamily("ManropeSemibold")
        .CharacterSpacing(1)
        .TextColor(foreground)
        .BackgroundColor(background)
        .CornerRadius(0)
        .BorderWidth(0)
        .MinimumHeightRequest(72)
        .Padding(AppSpacing.S, 18, AppSpacing.S, 30)
        .AutomationId(automationId)
        .OnClicked(onTap);
    }

    static bool IsLight() => Application.Current?.RequestedTheme != AppTheme.Dark;
    static Color SurfaceColor() => IsLight() ? AppColors.Light.Surface : AppColors.Dark.Surface;
    static Color TextPrimary() => IsLight() ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
    static Color TextSecondary() => IsLight() ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
    static Color AccentColor() => IsLight() ? AppColors.Light.Primary : AppColors.Dark.Primary;
    static Color DividerColor() => IsLight() ? AppColors.Light.Outline : AppColors.Dark.Outline;
}
