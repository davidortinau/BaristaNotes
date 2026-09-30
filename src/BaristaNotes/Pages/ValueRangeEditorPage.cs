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
    public RangeEditorDraft? Draft { get; set; }
    public string? ErrorMessage { get; set; }
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

    RangeEditorDraft Draft =>
        State.Draft ?? throw new InvalidOperationException("The range editor has not loaded.");

    bool IsDirty => State.Draft?.IsDirty == true;

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
        var draft = new RangeEditorDraft(Props.Metric, Props.Method, _rangeService.GetSettings());
        SetState(s =>
        {
            s.Draft = draft;
            s.ErrorMessage = null;
        });
    }

    void UpdateMinimum(string value)
    {
        var draft = Draft;
        SetState(s =>
        {
            draft.MinimumText = value;
            s.ErrorMessage = draft.ValidationError;
        });
    }

    void UpdateMaximum(string value)
    {
        var draft = Draft;
        SetState(s =>
        {
            draft.MaximumText = value;
            s.ErrorMessage = draft.ValidationError;
        });
    }

    async Task SaveAsync()
    {
        if (!Draft.TryGetRange(out var range, out var error))
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
        if (!Draft.HasOverride)
        {
            var draft = Draft;
            SetState(s =>
            {
                draft.UseRecommended();
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
                    State.Draft?.MinimumText ?? "",
                    "Minimum value",
                    UpdateMinimum,
                    "RangeMinimum"),
                ValueFieldTile(
                    "MAXIMUM",
                    State.Draft?.MaximumText ?? "",
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
