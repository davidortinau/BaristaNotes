using Android.App;
using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private static readonly DrinkValueMetric[] RangeMetrics =
        [DrinkValueMetric.DoseIn, DrinkValueMetric.Yield, DrinkValueMetric.GrindMicrons, DrinkValueMetric.Time];
    private RangeEditorDraft? _rangeDraft;
    private AlertDialog? _rangeConfirmation;

    private IDrinkValueRangeService Ranges => _app.Services.GetRequiredService<IDrinkValueRangeService>();

    private void ObserveRangeChanges(NativeScreen screen, Action refresh)
    {
        EventHandler changed = (_, _) => RunOnUiThread(() =>
        {
            if (_destroyed || !ReferenceEquals(_transient, screen))
                return;
            try { refresh(); }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to refresh range presentation on {Page}", _page);
                ShowFeedback(ErrorMessage(exception), isError: true);
            }
        });
        Ranges.SettingsChanged += changed;
        screen.OnDispose(() => Ranges.SettingsChanged -= changed);
    }

    private RangeTile NewRangeTile(NativeScreen screen, string id, Action action)
    {
        var tile = new RangeTile(_style);
        NativeStyle.Identify(tile, id + "Row");
        NativeStyle.Identify(tile.ActionButton, id);
        Bind(screen, tile.ActionButton, action);
        EventHandler<View.FocusChangeEventArgs> focus = (_, args) =>
            tile.Background = _style.Rounded(_style.Surface, 0, args.HasFocus ? _style.Primary : _style.Surface);
        tile.ActionButton.FocusChange += focus;
        screen.OnDispose(() => tile.ActionButton.FocusChange -= focus);
        return tile;
    }

    private void AddSettingsRanges(NativeScreen screen, LinearLayout body)
    {
        AddSettingsSection(body, "VALUE RANGES");
        var tiles = new Dictionary<DrinkValueMetric, RangeTile>();
        foreach (var metric in RangeMetrics)
        {
            var tile = NewRangeTile(screen, $"ValueRange_{metric}", () => ShowRangeSettings(metric));
            tile.Caption.Text = DrinkValueRangeFormatting.MetricTitle(metric).ToUpperInvariant();
            tile.Caption.SetTextSize(Android.Util.ComplexUnitType.Sp, 10);
            tile.Caption.LetterSpacing = 2 * .0624f;
            tile.Status.Visibility = ViewStates.Visible;
            body.AddView(tile, new LinearLayout.LayoutParams(-1, -2));
            var divider = new View(this);
            divider.SetBackgroundColor(_style.Outline);
            body.AddView(divider, new LinearLayout.LayoutParams(-1, _style.Dp(1)));
            tiles.Add(metric, tile);
        }
        void Refresh()
        {
            var snapshot = Ranges.GetSettings();
            foreach (var (metric, tile) in tiles)
            {
                var mode = snapshot.Modes.GetValueOrDefault(metric, ValueRangeMode.Auto);
                var count = snapshot.Overrides.Count(item => item.Metric == metric);
                var subtitle = mode == ValueRangeMode.Auto ? "Automatic by drink method"
                    : count == 1 ? "Custom - 1 drink method" : $"Custom - {count} drink methods";
                tile.Value.Text = subtitle;
                tile.Status.Text = mode == ValueRangeMode.Auto ? "AUTO" : "CUSTOM";
                tile.Status.SetTextColor(mode == ValueRangeMode.Custom ? _style.Primary : _style.Secondary);
                tile.SetInteraction(true, $"{DrinkValueRangeFormatting.MetricTitle(metric)}. {subtitle}. {mode}.");
            }
        }
        Refresh();
        ObserveRangeChanges(screen, Refresh);
    }

    private View RangeHeader(string caption, string title)
    {
        var header = (ViewGroup)BuildHeader(caption, title);
        var label = (TextView)header.GetChildAt(0)!;
        label.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
        var value = (TextView)header.GetChildAt(1)!;
        value.Typeface = _style.Semibold;
        NativeStyle.Identify(value, "RangePageTitle");
        return header;
    }

    private Button RangeAction(string label, string id, bool inverted = false)
    {
        var button = _style.Button(label, id, inverted ? _style.Surface : _style.Text);
        ConfigureBeanAction(button);
        button.SetBackgroundColor(inverted ? _style.Text : _style.Surface);
        return button;
    }

    private void ShowRangeSettings(DrinkValueMetric metric)
    {
        ClearTransient();
        _rangeDraft = null;
        _page = "ranges";
        var column = _style.Column();
        column.SetBackgroundColor(_style.Outline);
        var screen = new NativeScreen(column);
        column.AddView(RangeHeader("VALUE RANGES", $"{DrinkValueRangeFormatting.MetricTitle(metric)} Ranges"),
            new LinearLayout.LayoutParams(-1, -2) { BottomMargin = _style.Dp(1) });
        var scroll = new ScrollView(this) { FillViewport = true };
        NativeStyle.Identify(scroll, "RangeSettingsScroll");
        var content = _style.Column();
        content.SetBackgroundColor(_style.Outline);
        scroll.SetBackgroundColor(_style.Surface);
        scroll.AddView(content);
        void Add(View child) => content.AddView(child,
            new LinearLayout.LayoutParams(-1, -2) { BottomMargin = _style.Dp(1) });

        var help = _style.Column();
        help.SetBackgroundColor(_style.Surface);
        help.SetPadding(_style.Dp(16), _style.Dp(14), _style.Dp(16), _style.Dp(14));
        var modeCaption = _style.Label("MODE", 12, true, _style.Secondary);
        modeCaption.LetterSpacing = 2 * .0624f;
        help.AddView(modeCaption);
        var helpText = _style.Label("", 14);
        NativeStyle.Identify(helpText, "RangeModeHelp");
        help.AddView(helpText, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(4) });
        Add(help);
        var modes = _style.Row();
        var modeButtons = new Dictionary<ValueRangeMode, Button>();
        foreach (var mode in new[] { ValueRangeMode.Auto, ValueRangeMode.Custom })
        {
            var button = _style.Button(mode.ToString().ToUpperInvariant(), $"RangeMode_{mode}");
            button.Typeface = _style.Semibold;
            button.LetterSpacing = 2 * .0624f;
            button.SetMinHeight(_style.Dp(56));
            button.SetMinimumHeight(_style.Dp(56));
            Bind(screen, button, () => RunOperation(async () =>
            {
                await InScopeAsync(services =>
                {
                    services.GetRequiredService<IDrinkValueRangeService>().SetMode(metric, mode);
                    return Task.FromResult(true);
                });
            }));
            modes.AddView(button, new LinearLayout.LayoutParams(0, -2, 1)
                { RightMargin = mode == ValueRangeMode.Auto ? _style.Dp(1) : 0 });
            modeButtons.Add(mode, button);
        }
        Add(modes);
        var warning = _style.Label("", 14, color: _style.Surface);
        warning.SetBackgroundColor(NativeStyle.Warning);
        warning.SetPadding(_style.Dp(16), _style.Dp(12), _style.Dp(16), _style.Dp(12));
        warning.AccessibilityLiveRegion = AccessibilityLiveRegion.Polite;
        NativeStyle.Identify(warning, "RangeLoadWarning");
        Add(warning);
        var methodTiles = new Dictionary<BrewMethod, RangeTile>();
        foreach (var method in BrewMethodExtensions.All)
        {
            var tile = NewRangeTile(screen, $"RangeMethod_{method}", () =>
            {
                if (Ranges.GetMode(metric) == ValueRangeMode.Custom)
                    ShowRangeEditor(metric, method);
            });
            tile.Caption.Text = method.DisplayName().ToUpperInvariant();
            tile.Status.Visibility = ViewStates.Visible;
            Add(tile);
            methodTiles.Add(method, tile);
        }
        var reset = _style.Button("RESET ALL CUSTOM RANGES", "RangeResetAll", _style.Error);
        reset.Typeface = _style.Semibold;
        reset.LetterSpacing = .0624f;
        reset.SetBackgroundColor(_style.Surface);
        reset.SetMinHeight(_style.Dp(56));
        reset.SetMinimumHeight(_style.Dp(56));
        Bind(screen, reset, () => RunOperation(async () =>
        {
            if (await ConfirmRangeAsync("Reset custom ranges?",
                    $"Remove all custom {DrinkValueRangeFormatting.MetricTitle(metric).ToLowerInvariant()} ranges?",
                    "Reset", "Cancel"))
            {
                await InScopeAsync(services =>
                {
                    services.GetRequiredService<IDrinkValueRangeService>().ResetOverrides(metric);
                    return Task.FromResult(true);
                });
            }
        }));
        Add(reset);
        var spacer = new View(this);
        spacer.SetBackgroundColor(_style.Outline);
        content.AddView(spacer, new LinearLayout.LayoutParams(-1, _style.Dp(16)));
        column.AddView(scroll, _style.Fill(weight: 1));
        var back = RangeAction("BACK", "RangeSettingsBack");
        Bind(screen, back, ShowSettings);
        column.AddView(back);
        void Refresh()
        {
            var settings = Ranges.GetSettings();
            var mode = settings.Modes.GetValueOrDefault(metric, ValueRangeMode.Auto);
            helpText.Text = mode == ValueRangeMode.Auto
                ? "Uses recommended ranges for each drink method."
                : "Edited methods use custom ranges. Other methods stay automatic.";
            foreach (var (value, button) in modeButtons)
            {
                var selected = value == mode;
                button.Selected = selected;
                button.SetBackgroundColor(selected ? _style.Text : _style.Surface);
                button.SetTextColor(selected ? _style.Surface : _style.Text);
            }
            warning.Text = settings.LoadWarning;
            // Keep the source's zero-height placeholder in the stack so its
            // adjacent divider spacing does not change when no warning exists.
            warning.Visibility = settings.LoadWarning is null ? ViewStates.Invisible : ViewStates.Visible;
            warning.LayoutParameters = new LinearLayout.LayoutParams(-1, settings.LoadWarning is null ? 0 : -2)
                { BottomMargin = _style.Dp(1) };
            var hasOverrides = settings.Overrides.Any(item => item.Metric == metric);
            reset.Visibility = hasOverrides ? ViewStates.Visible : ViewStates.Gone;
            spacer.Visibility = hasOverrides ? ViewStates.Gone : ViewStates.Visible;
            foreach (var (method, tile) in methodTiles)
            {
                var effective = Ranges.Resolve(metric, method);
                var source = effective.Source switch
                {
                    ValueRangeSource.Custom => "CUSTOM",
                    ValueRangeSource.AutoFallback => "AUTO FALLBACK",
                    _ => "AUTO"
                };
                var rangeText = DrinkValueRangeFormatting.FormatRange(metric, effective.Range);
                tile.Value.Text = rangeText;
                tile.Status.Text = source;
                tile.Status.SetTextColor(effective.Source == ValueRangeSource.Custom ? _style.Primary : _style.Secondary);
                tile.Glyph.SetTextColor(mode == ValueRangeMode.Custom ? _style.Text
                    : Color.Argb(89, _style.Secondary.R, _style.Secondary.G, _style.Secondary.B));
                tile.SetInteraction(mode == ValueRangeMode.Custom, $"{method.DisplayName()}. {rangeText}. {source}.");
            }
        }
        Refresh();
        ObserveRangeChanges(screen, Refresh);
        _transient = screen;
        Present(column, edgeToEdge: true);
    }

    private void ShowRangeEditor(DrinkValueMetric metric, BrewMethod method)
    {
        var draft = new RangeEditorDraft(metric, method, Ranges.GetSettings());
        ClearTransient();
        _rangeDraft = draft;
        _page = "rangeEditor";
        var column = _style.Column();
        column.SetBackgroundColor(_style.Outline);
        var screen = new NativeScreen(column);
        column.AddView(RangeHeader($"CUSTOM {DrinkValueRangeFormatting.MetricTitle(metric).ToUpperInvariant()}", method.DisplayName()),
            new LinearLayout.LayoutParams(-1, -2) { BottomMargin = _style.Dp(1) });
        var scroll = new ScrollView(this) { FillViewport = true };
        NativeStyle.Identify(scroll, "RangeEditorScroll");
        scroll.SetBackgroundColor(_style.Surface);
        var content = _style.Column();
        content.SetBackgroundColor(_style.Outline);
        scroll.AddView(content);
        void Add(View view) => content.AddView(view,
            new LinearLayout.LayoutParams(-1, -2) { BottomMargin = _style.Dp(1) });
        var guidance = _style.Column();
        guidance.SetBackgroundColor(_style.Surface);
        guidance.SetPadding(_style.Dp(16), _style.Dp(14), _style.Dp(16), _style.Dp(14));
        var unit = _style.Label($"ENTER VALUES IN {draft.EditorUnit.Label.ToUpperInvariant()}", 12, true, _style.Secondary);
        unit.LetterSpacing = 2 * .0624f;
        guidance.AddView(unit);
        foreach (var (text, secondary) in new[]
                 {
                     ($"Recommended: {DrinkValueRangeFormatting.FormatRange(metric, draft.Definition.AutoRange)}", false),
                     ($"Allowed: {DrinkValueRangeFormatting.FormatRange(metric, draft.Definition.HardRange)}", true)
                 })
            guidance.AddView(_style.Label(text, 14, color: secondary ? _style.Secondary : _style.Text),
                new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(8) });
        NativeStyle.Identify(guidance, "RangeGuidance");
        Add(guidance);
        EditText Field(string caption, string value, string id)
        {
            var row = _style.Row();
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetBackgroundColor(_style.Surface);
            row.SetMinimumHeight(_style.Dp(96));
            row.SetPadding(_style.Dp(16), _style.Dp(16), _style.Dp(16), _style.Dp(16));
            var text = _style.Column();
            var label = _style.Label(caption, 12, true, _style.Secondary);
            label.LetterSpacing = 2 * .0624f;
            text.AddView(label);
            var entry = new EditText(this)
            {
                Text = value, Hint = caption == "MINIMUM" ? "Minimum value" : "Maximum value",
                Typeface = _style.Semibold,
                InputType = InputTypes.ClassNumber | InputTypes.NumberFlagDecimal | InputTypes.NumberFlagSigned,
                ContentDescription = $"{caption}. {draft.EditorUnit.Label}"
            };
            entry.SetSingleLine(true);
            entry.SetTextSize(Android.Util.ComplexUnitType.Sp, 22);
            entry.SetTextColor(_style.Text);
            entry.SetBackgroundColor(Color.Transparent);
            NativeStyle.Identify(entry, id);
            text.AddView(entry, new LinearLayout.LayoutParams(-1, -2));
            row.AddView(text, new LinearLayout.LayoutParams(0, -2, 1));
            row.AddView(_style.Label(draft.EditorUnit.Label, 14, color: _style.Secondary),
                new LinearLayout.LayoutParams(-2, -2) { LeftMargin = _style.Dp(8) });
            Add(row);
            return entry;
        }
        var minimum = Field("MINIMUM", draft.MinimumText, "RangeMinimum");
        var maximum = Field("MAXIMUM", draft.MaximumText, "RangeMaximum");
        var error = _style.Label("", 14, color: _style.Surface);
        error.Typeface = _style.Semibold;
        error.SetBackgroundColor(_style.Error);
        error.SetPadding(_style.Dp(16), _style.Dp(12), _style.Dp(16), _style.Dp(12));
        error.SetMinimumHeight(_style.Dp(56));
        error.AccessibilityLiveRegion = AccessibilityLiveRegion.Assertive;
        NativeStyle.Identify(error, "RangeEditorError");
        Add(error);
        void ShowError(string? message)
        {
            if (_destroyed || !ReferenceEquals(_transient, screen))
                return;
            error.Text = message;
            error.Visibility = message is null ? ViewStates.Invisible : ViewStates.Visible;
            error.LayoutParameters = new LinearLayout.LayoutParams(-1, message is null ? 0 : -2)
                { BottomMargin = _style.Dp(1) };
        }
        EventHandler<TextChangedEventArgs> minimumChanged = (_, _) =>
        {
            draft.MinimumText = minimum.Text ?? "";
            ShowError(draft.ValidationError);
        };
        EventHandler<TextChangedEventArgs> maximumChanged = (_, _) =>
        {
            draft.MaximumText = maximum.Text ?? "";
            ShowError(draft.ValidationError);
        };
        minimum.TextChanged += minimumChanged;
        maximum.TextChanged += maximumChanged;
        screen.OnDispose(() => minimum.TextChanged -= minimumChanged);
        screen.OnDispose(() => maximum.TextChanged -= maximumChanged);
        var recommended = NewRangeTile(screen, "UseRecommendedRange", () => RunOperation(async () =>
        {
            if (!draft.HasOverride)
            {
                draft.UseRecommended();
                minimum.Text = draft.MinimumText;
                maximum.Text = draft.MaximumText;
                ShowError(null);
                return;
            }
            if (!await ConfirmRangeAsync("Use recommended range?",
                    $"Remove the custom {DrinkValueRangeFormatting.MetricTitle(metric).ToLowerInvariant()} range for {method.DisplayName()}?",
                    "Use Recommended", "Cancel"))
                return;
            try
            {
                await InScopeAsync(services =>
                {
                    services.GetRequiredService<IDrinkValueRangeService>().RemoveOverride(metric, method);
                    return Task.FromResult(true);
                });
                HideKeyboard();
                ShowRangeSettings(metric);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to remove range override for {Metric} {Method}", metric, method);
                ShowError(ErrorMessage(exception));
            }
        }));
        recommended.Caption.Text = "USE RECOMMENDED RANGE";
        recommended.Value.Text = DrinkValueRangeFormatting.FormatRange(metric, draft.Definition.AutoRange);
        recommended.Glyph.Text = "\uf053";
        recommended.Glyph.SetTextColor(_style.Primary);
        recommended.SetInteraction(true, $"Use recommended range. {recommended.Value.Text}.");
        Add(recommended);
        var spacer = new View(this);
        spacer.SetBackgroundColor(_style.Outline);
        content.AddView(spacer, new LinearLayout.LayoutParams(-1, _style.Dp(16)));
        column.AddView(scroll, _style.Fill(weight: 1));
        var actions = _style.Row();
        var cancel = RangeAction("CANCEL", "RangeEditorCancel");
        Bind(screen, cancel, () => RunOperation(LeaveRangeEditorAsync));
        actions.AddView(cancel, new LinearLayout.LayoutParams(0, -2, 1) { RightMargin = _style.Dp(1) });
        var save = RangeAction("SAVE", "RangeEditorSave", inverted: true);
        Bind(screen, save, () => RunOperation(async () =>
        {
            if (!draft.TryGetRange(out var range, out var message))
            {
                ShowError(message);
                return;
            }
            save.Text = "SAVING";
            minimum.Enabled = maximum.Enabled = false;
            ShowError(null);
            try
            {
                await InScopeAsync(services =>
                {
                    services.GetRequiredService<IDrinkValueRangeService>().SaveOverride(metric, method, range.Minimum, range.Maximum);
                    return Task.FromResult(true);
                });
                HideKeyboard();
                ShowRangeSettings(metric);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to save range override for {Metric} {Method}", metric, method);
                ShowError(ErrorMessage(exception));
            }
            finally
            {
                if (!_destroyed && ReferenceEquals(_transient, screen))
                {
                    save.Text = "SAVE";
                    minimum.Enabled = maximum.Enabled = true;
                }
            }
        }));
        actions.AddView(save, new LinearLayout.LayoutParams(0, -2, 1));
        column.AddView(actions);
        _transient = screen;
        ShowError(null);
        Present(column, edgeToEdge: true);
    }

    private async Task LeaveRangeEditorAsync()
    {
        if (_rangeDraft is not { } draft)
            return;
        if (draft.IsDirty && !await ConfirmRangeAsync(
                "Discard changes?", "Your range changes have not been saved.", "Discard", "Keep Editing"))
            return;
        HideKeyboard();
        ShowRangeSettings(draft.Metric);
    }

    private async Task<bool> ConfirmRangeAsync(string title, string message, string accept, string cancel)
    {
        var cancellation = _lifetime.Token;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var builder = new AlertDialog.Builder(this);
        using var dialog = builder.SetTitle(title)!.SetMessage(message)!
            .SetPositiveButton(accept, (_, _) => completion.TrySetResult(true))!
            .SetNegativeButton(cancel, (_, _) => completion.TrySetResult(false))!
            .Create() ?? throw new InvalidOperationException("The native range confirmation could not be created.");
        EventHandler dismissed = (_, _) => completion.TrySetResult(false);
        dialog.DismissEvent += dismissed;
        _rangeConfirmation = dialog;
        try
        {
            dialog.Show();
            dialog.GetButton((int)Android.Content.DialogButtonType.Positive)?.SetAllCaps(false);
            dialog.GetButton((int)Android.Content.DialogButtonType.Negative)?.SetAllCaps(false);
            var result = await completion.Task;
            cancellation.ThrowIfCancellationRequested();
            return result;
        }
        finally
        {
            dialog.DismissEvent -= dismissed;
            if (ReferenceEquals(_rangeConfirmation, dialog))
                _rangeConfirmation = null;
        }
    }
}
