using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using Android.Util;
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
    private sealed class DrinkEditor(NativeScreen screen)
    {
        public NativeScreen Screen { get; } = screen;
        public Dictionary<string, TextView> Values { get; } = [];
        public TextView Maker { get; set; } = null!;
        public TextView Recipient { get; set; } = null!;
        public ProfileAvatarView MakerAvatar { get; set; } = null!;
        public ProfileAvatarView RecipientAvatar { get; set; } = null!;
        public View People { get; set; } = null!;
        public AdviceLoadingBar? AdviceBar { get; set; }
    }

    private void ShowDrink()
    {
        ClearTransient();
        _page = "drink";
        // Like the retained source page's OnAppearing, refresh presentation
        // preferences on return without replacing canonical draft values.
        Draft.TempUnit = _app.Services.GetRequiredService<IPreferencesService>().GetTemperatureUnit();
        // Bean history can open a second edit draft while a recipe route retains
        // the original editor. Do not reuse or dispose that retained native owner.
        if (_editingShotId.HasValue && _editEditor is { } retained &&
            _voiceNavigation.Any(frame => ReferenceEquals(frame.Previous.EditEditor, retained)
                && !ReferenceEquals(frame.Previous.EditDraft, _editDraft)))
            _editEditor = null;
        var editor = _editingShotId.HasValue
            ? _editEditor ??= BuildDrinkEditor(isEditing: true)
            : _newEditor ??= BuildDrinkEditor(isEditing: false);
        UpdateDrink(editor);
        Present(editor.Screen.Root, edgeToEdge: true);
    }

    private DrinkEditor BuildDrinkEditor(bool isEditing)
    {
        var column = _style.Column();
        column.SetBackgroundColor(_style.Outline);
        column.SetPadding(_style.Dp(1), _style.Dp(1), _style.Dp(1), _style.Dp(1));
        var screen = new NativeScreen(column);
        var editor = new DrinkEditor(screen);
        screen.OnDispose(() =>
        {
            if (ReferenceEquals(_adviceOwner, editor)) DisposeAdvice();
            if (_photoSession is { } photo && ReferenceEquals(photo.Editor, editor)) ReleasePhotoSession(photo);
        });
        var rows = new[]
        {
            new[] { ("METHOD", "Method"), ("BAG", "Bag") },
            new[] { ("DRINK TYPE", "DrinkType"), ("RATING", "Rating") },
            new[] { ("DOSE IN", "DoseIn"), ("YIELD", "Yield") },
            new[] { ("TIME", "Time"), ("GRIND", "Grind") },
            new[] { ("WATER TEMP", "Temperature"), ("MADE BY / FOR", "People") },
            new[] { ("MACHINE", "Machine"), ("GRINDER", "Grinder") }
        };
        for (var index = 0; index < rows.Length; index++)
        {
            var row = _style.Row();
            for (var cellIndex = 0; cellIndex < 2; cellIndex++)
            {
                var (caption, key) = rows[index][cellIndex];
                View tile = key == "People"
                    ? BuildPeopleTile(editor)
                    : BuildDrinkTile(editor, caption, key, inverted: false);
                var layout = _style.Share();
                if (cellIndex == 0)
                    layout.RightMargin = _style.Dp(1);
                row.AddView(tile, layout);
            }
            var parameters = index == 0
                ? new LinearLayout.LayoutParams(-1, ViewGroup.LayoutParams.WrapContent)
                : new LinearLayout.LayoutParams(-1, 0, 1);
            parameters.BottomMargin = _style.Dp(1);
            column.AddView(row, parameters);
        }
        column.AddView(BuildDrinkTile(editor, isEditing ? "UPDATE" : "SAVE", "Save", inverted: true),
            new LinearLayout.LayoutParams(-1, 0, 1) { BottomMargin = _style.Dp(1) });
        var navigation = new FrameLayout(this);
        navigation.SetClipChildren(false);
        navigation.SetClipToPadding(false);
        navigation.AddView(BuildNavigation(screen,
            ("\uf009", "Activity", "NavActivity", () => RunOperation(ShowHistoryAsync)),
            ("\ue8b8", "Settings", "NavSettings", OpenSettings),
            ("\ue029", "Voice", "NavVoice", ToggleVoiceFromPage),
            (isEditing ? "\uf136" : "\ue3b0", isEditing ? "Advice" : "Camera", "NavCameraAdvice",
                isEditing ? () => RequestAdvice(editor) : () => RequestPhoto(editor))),
            new FrameLayout.LayoutParams(-1, ViewGroup.LayoutParams.WrapContent));
        if (isEditing)
        {
            editor.AdviceBar = new AdviceLoadingBar(_style);
            screen.Own(editor.AdviceBar);
            navigation.AddView(editor.AdviceBar, new FrameLayout.LayoutParams(-1, _style.Dp(4), GravityFlags.Top));
        }
        column.AddView(navigation, new LinearLayout.LayoutParams(-1, ViewGroup.LayoutParams.WrapContent));
        return editor;
    }

    private View BuildDrinkTile(DrinkEditor editor, string caption, string key, bool inverted)
    {
        var tile = new EdgeAwareColumn(this, () => Window?.DecorView) { Orientation = Orientation.Vertical };
        tile.SetMinimumHeight(_style.Dp(120));
        tile.SetBackgroundColor(inverted ? _style.Text : _style.Surface);
        tile.SetContentPadding(_style.Dp(16), _style.Dp(14), _style.Dp(16), _style.Dp(14));
        var captionColor = inverted
            ? Color.Argb(179, _style.Surface.R, _style.Surface.G, _style.Surface.B)
            : _style.Secondary;
        var label = _style.Label(caption, 10, true, captionColor);
        label.LetterSpacing = 2 * .0624f;
        tile.AddView(label);
        var value = _style.Label("", 28, true, inverted ? _style.Surface : _style.Text);
        value.Gravity = GravityFlags.Bottom | GravityFlags.Start;
        value.SetMaxLines(2);
        tile.AddView(value, _style.Fill(weight: 1));
        editor.Values[key] = value;
        NativeStyle.Identify(tile, $"ShotTile_{key}");
        Action? select = key switch
        {
            "Method" => OpenMethod,
            "Bag" => () => RunOperation(OpenBagAsync),
            "DrinkType" => OpenDrinkType,
            "Rating" => OpenRating,
            "DoseIn" => () => OpenMassPicker(isYield: false),
            "Yield" => () => OpenMassPicker(isYield: true),
            "Time" => OpenTimePicker,
            "Temperature" => OpenWaterTemperaturePicker,
            "Grind" => () => RunOperation(OpenGrindPickerAsync),
            "Machine" => () => RunOperation(() => OpenEquipmentSelectorAsync(EquipmentPickerKind.Machine)),
            "Grinder" => () => RunOperation(() => OpenEquipmentSelectorAsync(EquipmentPickerKind.Grinder)),
            // Pinned source has this selector kind but no rendered grid entry.
            "Accessories" => () => RunOperation(() => OpenEquipmentSelectorAsync(EquipmentPickerKind.Accessories)),
            "Save" => () => RunOperation(SaveDrinkAsync),
            _ => null
        };
        if (select is not null)
        {
            tile.Focusable = true;
            Bind(editor.Screen, tile, select, key == "Bag"
                ? () => RunOperation(() => OpenRecipeForSelectedBagAsync(editor)) : null);
        }
        else
            tile.ContentDescription = $"{caption}. Editing this field is outside the current first slice.";
        return tile;
    }

    private async Task OpenRecipeForSelectedBagAsync(DrinkEditor editor)
    {
        var draft = Draft;
        var revision = _presentationRevision;
        bool IsOwner() => !_destroyed && _page == "drink" && ReferenceEquals(Draft, draft)
            && ReferenceEquals(_host.GetChildAt(0), editor.Screen.Root);
        if (!IsOwner()) return;
        var bag = draft.AvailableBags.FirstOrDefault(item => item.Id == draft.SelectedBagId);
        if (bag == null)
        {
            ShowFeedback("Select a bag first to view its recipe.");
            return;
        }
        var method = draft.BrewMethod;
        try
        {
            var recipe = await InScopeAsync(provider =>
                provider.GetRequiredService<IRecipeService>().GetRecipeForBeanAndMethodAsync(bag.BeanId, method));
            if (!IsOwner() || revision != _presentationRevision) return;
            if (recipe == null)
            {
                ShowFeedback($"No {method.DisplayName()} recipe for {bag.BeanName} yet.");
                return;
            }
            var plan = new VoiceRoutePlan(VoiceRouteKind.Bean, false, bag.BeanId, null, null, null);
            PushNavigationFrame(plan);
            HideKeyboard();
            try { await ShowBeanDetailAsync(new BeanDetailState(bag.BeanId), loadBean: true); }
            catch
            {
                if (!_destroyed && _voiceNavigation.TryPeek(out var frame) && ReferenceEquals(frame.Destination, plan))
                    RestoreVoicePage(_voiceNavigation.Pop().Previous);
                throw;
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            _logger.LogError(error, "Failed to open recipe for {BagId}", bag.Id);
            if (IsOwner()) ShowFeedback("Couldn't open the recipe.", isError: true);
        }
    }

    private View BuildPeopleTile(DrinkEditor editor)
    {
        var tile = new EdgeAwareColumn(this, () => Window?.DecorView) { Orientation = Orientation.Vertical };
        tile.SetClipChildren(false);
        tile.SetClipToPadding(false);
        tile.SetMinimumHeight(_style.Dp(120));
        tile.SetBackgroundColor(_style.Surface);
        tile.SetContentPadding(_style.Dp(16), _style.Dp(14), _style.Dp(16), _style.Dp(14));
        var caption = _style.Label("MADE BY / FOR", 10, true, _style.Secondary);
        caption.LetterSpacing = 2 * .0624f;
        tile.AddView(caption);
        var row = new PeopleSummaryRow(this);
        TextView Person(bool maker)
        {
            var column = _style.Column();
            column.SetGravity(GravityFlags.Center);
            var icon = new ProfileAvatarView(_style, _logger, 40);
            icon.ConfigurePeople(40, selected: false);
            icon.SetAutomationPrefix(maker ? "DrinkMakerAvatar" : "DrinkRecipientAvatar");
            editor.Screen.Own(icon);
            if (maker)
                editor.MakerAvatar = icon;
            else
                editor.RecipientAvatar = icon;
            column.AddView(icon, new LinearLayout.LayoutParams(_style.Dp(40), _style.Dp(40)));
            var name = _style.Label("—", 11, color: _style.Secondary);
            name.SetSingleLine(true);
            name.Ellipsize = TextUtils.TruncateAt.End;
            name.Gravity = GravityFlags.Center;
            column.AddView(name, new LinearLayout.LayoutParams(-2, -2) { TopMargin = _style.Dp(3) });
            row.AddView(column, new ViewGroup.LayoutParams(-2, -2));
            return name;
        }
        editor.Maker = Person(maker: true);
        var arrow = _style.Label("\ue941", 22, color: _style.Primary);
        arrow.Typeface = _style.Symbols;
        arrow.SetPadding(_style.Dp(8), 0, _style.Dp(8), 0);
        row.AddView(arrow);
        editor.Recipient = Person(maker: false);
        tile.AddView(row, _style.Fill(weight: 1));
        tile.Focusable = true;
        NativeStyle.Identify(tile, "ShotTile_People");
        Bind(editor.Screen, tile, () => RunOperation(OpenPeopleAsync));
        editor.People = tile;
        return tile;
    }

    private View BuildNavigation(NativeScreen screen,
        params (string Glyph, string Name, string Id, Action? Action)[] items)
    {
        var row = _style.Row();
        row.SetMinimumHeight(_style.Dp(72));
        row.SetBackgroundColor(_style.Outline);
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
#if NATIVE_UI_FIXTURE
            if (item.Id == "NavSettings")
                item = (item.Glyph, "UI fixture routes", "FixtureMenu", OpenFixtureMenu);
#endif
            var button = _style.Button(item.Glyph, item.Id, _style.Text);
            button.Typeface = _style.Symbols;
            button.SetTextSize(ComplexUnitType.Sp, 32);
            button.SetBackgroundColor(_style.Surface);
            button.SetPadding(_style.Dp(16), _style.Dp(18), _style.Dp(16), _style.Dp(30));
            button.ContentDescription = item.Action is null ? $"{item.Name}. Not included in this first slice." : item.Name;
            if (item.Action is not null)
            {
                void Navigate()
                {
                    if (item.Name is "New Drink" or "Activity" or "Settings") ClearVoiceNavigationHistory();
                    NotifyPerformanceNavigationAction(item.Id);
                    item.Action();
                }
                Bind(screen, button, Navigate);
                RegisterPerformanceNavigation(item.Id, Navigate);
            }
            else
                button.Enabled = false;
            row.AddView(button, new LinearLayout.LayoutParams(0, -1, 1)
            {
                RightMargin = index < items.Length - 1 ? _style.Dp(1) : 0
            });
        }
        return row;
    }

    partial void NotifyPerformanceNavigationAction(string id);
    partial void RegisterPerformanceNavigation(string id, Action action);

    private void UpdateDrink(DrinkEditor editor)
    {
        var draft = Draft;
        SetTileValue(editor, "Method", draft.BrewMethod.DisplayName());
        SetTileValue(editor, "Bag", draft.AvailableBags.FirstOrDefault(b => b.Id == draft.SelectedBagId)?.BeanName
            ?? draft.BeanName ?? "—");
        SetTileValue(editor, "DrinkType", draft.DrinkType);
        SetTileValue(editor, "Rating", DrinkDisplay.RatingText(draft.Rating));
        SetTileValue(editor, "DoseIn", $"{draft.DoseIn:0.#}", "g");
        SetTileValue(editor, "Yield", $"{draft.ExpectedOutput:0.#}", "g");
        SetTileValue(editor, "Time", DrinkDisplay.TimeValue(draft.ActualTime ?? draft.ExpectedTime),
            DrinkDisplay.TimeUnit(draft.ActualTime ?? draft.ExpectedTime));
        SetTileValue(editor, "Grind", draft.GrindMicrons?.ToString() ?? "—", draft.GrindMicrons.HasValue ? "µm" : null);
        SetTileValue(editor, "Temperature", DrinkDisplay.WaterTemperatureValue(draft.WaterTempC, draft.TempUnit),
            DrinkDisplay.WaterTemperatureUnit(draft.WaterTempC, draft.TempUnit));
        SetTileValue(editor, "Machine", draft.AvailableEquipment.FirstOrDefault(e => e.Id == draft.SelectedMachineId)?.Name ?? "—");
        SetTileValue(editor, "Grinder", draft.AvailableEquipment.FirstOrDefault(e => e.Id == draft.SelectedGrinderId)?.Name ?? "—");
        SetTileValue(editor, "Save", _editingShotId.HasValue ? "Update" : "Log Drink");
        editor.Maker.Text = draft.SelectedMaker?.Name ?? "—";
        editor.Recipient.Text = draft.SelectedRecipient?.Name ?? "—";
        editor.Maker.SetTextColor(draft.SelectedMaker is null ? _style.Secondary : _style.Text);
        editor.Recipient.SetTextColor(draft.SelectedRecipient is null ? _style.Secondary : _style.Text);
        _ = editor.MakerAvatar.LoadFileAsync(ResolveProfileAvatarPath(draft.SelectedMaker?.AvatarPath));
        _ = editor.RecipientAvatar.LoadFileAsync(ResolveProfileAvatarPath(draft.SelectedRecipient?.AvatarPath));
        editor.People.ContentDescription =
            $"Made by {draft.SelectedMaker?.Name ?? "not selected"}. Made for {draft.SelectedRecipient?.Name ?? "not selected"}. Choose people.";
    }

    private void SetTileValue(DrinkEditor editor, string key, string text, string? unit = null)
    {
        var size = (text.Length, unit is not null) switch
        {
            (<= 3, _) => 44, (<= 6, true) => 36, (<= 6, false) => 38,
            (<= 10, _) => 28, (<= 14, _) => 22, (<= 20, _) => 18, _ => 16
        };
        var label = editor.Values[key];
        label.SetTextSize(ComplexUnitType.Sp, size);
        if (unit is null)
            label.Text = text;
        else
        {
            using var span = new SpannableString($"{text} {unit}");
            using var scale = new RelativeSizeSpan(Math.Max(12, size * .45f) / size);
            using var color = new ForegroundColorSpan(Color.Argb(153, _style.Text.R, _style.Text.G, _style.Text.B));
            var paint = label.Paint ?? throw new InvalidOperationException("Native text paint is unavailable.");
            using var gap = new ScaleXSpan(_style.Dp(4) / Math.Max(1, paint.MeasureText(" ")));
            // Java's Spannable retains this managed-callback span for the text
            // lifetime; disposing its peer here would break later measure/draw.
            var font = new NativeTypefaceSpan(_style.Regular);
            span.SetSpan(scale, text.Length + 1, span.Length(), SpanTypes.ExclusiveExclusive);
            span.SetSpan(gap, text.Length, text.Length + 1, SpanTypes.ExclusiveExclusive);
            span.SetSpan(color, text.Length + 1, span.Length(), SpanTypes.ExclusiveExclusive);
            span.SetSpan(font, text.Length + 1, span.Length(), SpanTypes.ExclusiveExclusive);
            label.TextFormatted = span;
        }
    }

    private async Task SaveDrinkAsync()
    {
        var draft = Draft;
        var editing = _editingShotId;
        await InScopeAsync(services =>
            services.GetRequiredService<DrinkWorkflow>().SaveAsync(draft: draft, editingShotId: editing));
        ShowFeedback(editing.HasValue ? "Drink updated" : $"{draft.DrinkType} logged");
        if (!editing.HasValue)
            await LoadDraftAsync(draft, editingShotId: null);
        ShowDrink();
    }
}
