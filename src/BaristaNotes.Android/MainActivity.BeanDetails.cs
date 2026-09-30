using Android.Text;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Grind;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private sealed class BeanDetailState(int beanId)
    {
        public BeanDraft Draft { get; } = new() { BeanId = beanId };
        public RatingAggregateDto? Ratings { get; set; }
        public List<BagSummaryDto> Bags { get; set; } = [];
        public List<ShotRecordDto> Shots { get; set; } = [];
        public List<RecipeDto> Recipes { get; set; } = [];
        public Dictionary<int, GrindTranslationResult> Translations { get; set; } = [];
        public int? RecipeGrinderId { get; set; }
        public string? RecipeGrinderName { get; set; }
        public bool HasMoreShots { get; set; }
        public bool HistoryLoaded { get; set; }
        public int NextShotPage { get; set; }
        public int ScrollY { get; set; }
        public int HistoryPosition { get; set; }
        public int HistoryOffset { get; set; }
        public string? Error { get; set; }
        public string? ShotError { get; set; }
        public string? RecipeError { get; set; }
    }

    private sealed class BeanEditor(NativeScreen screen, BeanDetailState state, CancellationToken parent)
    {
        public NativeScreen Screen { get; } = screen;
        public BeanDetailState State { get; } = state;
        public CancellationTokenSource Lifetime { get; } = CancellationTokenSource.CreateLinkedTokenSource(parent);
        public CancellationToken Cancellation { get; set; }
        public required ScrollView Scroll { get; init; }
        public required TextView Title { get; init; }
        public required Dictionary<string, EditText> Fields { get; init; }
        public required LinearLayout ErrorTile { get; init; }
        public required TextView ErrorText { get; init; }
        public required Button Save { get; init; }
        public required FrameLayout RatingsHost { get; init; }
        public required FrameLayout RecipesHost { get; init; }
        public required FrameLayout BagsHost { get; init; }
        public required FrameLayout HistoryHost { get; init; }
        public Dictionary<string, NativeScreen> Sections { get; } = [];
        public SelectorRecyclerView? HistoryList { get; set; }
        public BeanHistoryAdapter? HistoryAdapter { get; set; }
        public TextView? HistoryError { get; set; }
        public Button? HistoryRetry { get; set; }
        public TextView? HistoryEmpty { get; set; }
        public ProgressBar? HistoryInitialLoading { get; set; }
        public ProgressBar? HistoryMoreLoading { get; set; }
        public CancellationTokenSource? Translation { get; set; }
        public bool Saving { get; set; }
        public bool LoadingBags { get; set; }
        public bool LoadingShots { get; set; }
        public bool LoadingRecipes { get; set; }
        public bool RefreshingRecipes { get; set; }
        public bool Translating { get; set; }
    }

    private BeanEditor? _beanEditor;
    private bool IsCurrentBean(BeanEditor editor) =>
        !_destroyed && ReferenceEquals(_beanEditor, editor) && ReferenceEquals(_transient, editor.Screen)
        && !editor.Cancellation.IsCancellationRequested;

    private async Task ShowBeanDetailAsync(BeanDetailState state, bool loadBean)
    {
        ClearTransient();
        _page = "beanDetail";
        var root = _style.Column();
        root.SetBackgroundColor(_style.Outline);
        var screen = new NativeScreen(root);
        var header = (ViewGroup)BuildHeader("EDIT BEAN", string.IsNullOrEmpty(state.Draft.Name) ? "Loading…" : state.Draft.Name);
        var title = (TextView)header.GetChildAt(1)!;
        title.SetMaxLines(2);
        NativeStyle.Identify(title, "BeanDetailTitle");
        root.AddView(header, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = _style.Dp(1) });
        var body = new FrameLayout(this);
        body.SetBackgroundColor(_style.Surface);
        var scroll = new ScrollView(this) { FillViewport = true };
        NativeStyle.Identify(scroll, "BeanDetailScroll");
        var fields = _style.Column();
        fields.SetBackgroundColor(_style.Outline);
        scroll.AddView(fields);
        body.AddView(scroll, new FrameLayout.LayoutParams(-1, -1));
        var loading = new ProgressBar(this);
        body.AddView(loading, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Center));
        loading.Visibility = loadBean ? ViewStates.Visible : ViewStates.Gone;
        scroll.Visibility = loadBean ? ViewStates.Gone : ViewStates.Visible;
        root.AddView(body, _style.Fill(weight: 1));
        var entries = new Dictionary<string, EditText>();
        void Entry(string caption, string value, string placeholder, string key, bool large = false, bool multiline = false)
        {
            var tile = BeanSection(caption, multiline ? 150 : large ? 100 : 90);
            if (!multiline)
            {
                tile.SetPadding(_style.Dp(16), _style.Dp(16), _style.Dp(16), _style.Dp(16));
                tile.SetGravity(GravityFlags.CenterVertical);
            }
            var input = new EditText(this)
            {
                Text = value, Hint = placeholder, Typeface = multiline ? _style.Regular : _style.Bold,
                InputType = InputTypes.ClassText | (multiline ? InputTypes.TextFlagMultiLine : 0),
                Gravity = GravityFlags.Top | GravityFlags.Start
            };
            input.SetSingleLine(!multiline);
            input.SetTextSize(Android.Util.ComplexUnitType.Sp, multiline ? 16 : large ? 22 : 18);
            input.SetTextColor(_style.Text);
            input.SetHintTextColor(Android.Graphics.Color.Argb(128, _style.Secondary.R, _style.Secondary.G, _style.Secondary.B));
            input.SetBackgroundColor(Android.Graphics.Color.Transparent);
            input.SetPadding(0, _style.Dp(4), 0, 0);
            input.SetMinHeight(_style.Dp(44));
            if (key == "RoasterUrl") input.InputType = InputTypes.ClassText | InputTypes.TextVariationUri;
            NativeStyle.Identify(input, "BeanDetail" + key);
            tile.AddView(input, new LinearLayout.LayoutParams(-1, multiline ? _style.Dp(100) : -2));
            entries.Add(key, input);
            fields.AddView(tile, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = _style.Dp(1) });
        }
        Entry("NAME", state.Draft.Name, "Bean name (required)", "Name", large: true);
        Entry("ROASTER", state.Draft.Roaster, "Roaster name", "Roaster");
        Entry("ORIGIN", state.Draft.Origin, "Country or region", "Origin");
        Entry("ROASTER URL", state.Draft.RoasterUrl, "https://… (where to reorder these beans)", "RoasterUrl");
        Entry("NOTES", state.Draft.Notes, "Tasting notes, processing method…", "Notes", multiline: true);
        FrameLayout Host(string id)
        {
            var host = new FrameLayout(this);
            NativeStyle.Identify(host, id);
            fields.AddView(host, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = _style.Dp(1) });
            return host;
        }
        var ratings = Host("BeanRatings");
        var recipes = Host("BeanRecipes");
        var bags = Host("BeanBags");
        var history = Host("BeanHistory");
        var (errorTile, errorText) = BeanErrorTile();
        NativeStyle.Identify(errorText, "BeanDetailError");
        fields.AddView(errorTile);
        var spacer = new View(this);
        spacer.SetBackgroundColor(_style.Surface);
        fields.AddView(spacer, new LinearLayout.LayoutParams(-1, _style.Dp(24)));
        var actions = _style.Row();
        var cancel = BeanAction("CANCEL", "BeanDetailCancel");
        var delete = BeanAction("DELETE", "BeanDelete", danger: true);
        var save = BeanAction("SAVE", "BeanDetailSave", inverted: true);
        actions.AddView(cancel, new LinearLayout.LayoutParams(0, -1, 1) { RightMargin = _style.Dp(1) });
        actions.AddView(delete, new LinearLayout.LayoutParams(0, -1, 1) { RightMargin = _style.Dp(1) });
        actions.AddView(save, new LinearLayout.LayoutParams(0, -1, 1));
        root.AddView(actions);
        var editor = new BeanEditor(screen, state, _lifetime.Token)
        {
            Scroll = scroll, Title = title, Fields = entries, ErrorTile = errorTile,
            ErrorText = errorText, Save = save, RatingsHost = ratings, RecipesHost = recipes,
            BagsHost = bags, HistoryHost = history
        };
        editor.Cancellation = editor.Lifetime.Token;
        Bind(screen, cancel, () => { HideKeyboard(); ObserveBeanTask(ReturnFromBeanDetailAsync); });
        Bind(screen, save, () => ObserveBeanTask(() => SaveBeanDetailsAsync(editor)));
        Bind(screen, delete, () => RunOperation(() => DeleteBeanDetailsAsync(editor)));
        foreach (var (key, input) in entries)
        {
            EventHandler<TextChangedEventArgs> changed = (_, _) =>
            {
                if (!IsCurrentBean(editor)) return;
                var value = input.Text ?? "";
                switch (key)
                {
                    case "Name": state.Draft.Name = value; break;
                    case "Roaster": state.Draft.Roaster = value; break;
                    case "Origin": state.Draft.Origin = value; break;
                    case "RoasterUrl": state.Draft.RoasterUrl = value; break;
                    case "Notes": state.Draft.Notes = value; break;
                }
                UpdateBeanTitle(editor);
            };
            input.TextChanged += changed;
            screen.OnDispose(() => input.TextChanged -= changed);
        }
        screen.OnDispose(() =>
        {
            editor.Lifetime.Cancel();
            editor.Translation?.Cancel();
            editor.Translation?.Dispose();
            foreach (var section in editor.Sections.Values) section.Dispose();
            editor.Sections.Clear();
            editor.Lifetime.Dispose();
            if (ReferenceEquals(_beanEditor, editor)) _beanEditor = null;
        });
        _beanEditor = editor;
        _transient = screen;
        RenderBeanRatings(editor);
        RenderBeanBags(editor);
        RenderBeanRecipes(editor);
        BuildBeanHistory(editor);
        SetBeanError(editor, state.Error);
        UpdateBeanTitle(editor);
        Present(root, edgeToEdge: true);
        if (loadBean)
        {
            try
            {
                var id = state.Draft.BeanId ?? throw new InvalidOperationException("A saved bean is required.");
                var bean = await InScopeAsync(services => services.GetRequiredService<IBeanService>().GetBeanWithRatingsAsync(id));
                if (!IsCurrentBean(editor)) return;
                if (bean is null)
                {
                    SetBeanError(editor, "Bean not found");
                    return;
                }
                state.Draft.ApplyLoadedData(bean);
                state.Ratings = bean.RatingAggregate;
                entries["Name"].Text = state.Draft.Name;
                entries["Roaster"].Text = state.Draft.Roaster;
                entries["Origin"].Text = state.Draft.Origin;
                entries["RoasterUrl"].Text = state.Draft.RoasterUrl;
                entries["Notes"].Text = state.Draft.Notes;
                UpdateBeanTitle(editor);
                RenderBeanRatings(editor);
            }
            catch (OperationCanceledException) when (editor.Cancellation.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Bean detail load failed");
                SetBeanError(editor, $"Failed to load bean: {ErrorMessage(exception)}");
                return;
            }
            finally
            {
                if (IsCurrentBean(editor))
                {
                    loading.Visibility = ViewStates.Gone;
                    scroll.Visibility = ViewStates.Visible;
                }
            }
        }
        RestoreBeanScroll(editor);
        ObserveBeanTask(() => LoadBeanBagsAsync(editor));
        ObserveBeanTask(() => LoadBeanRecipesAsync(editor));
        if (!state.HistoryLoaded) ObserveBeanTask(() => LoadBeanShotsAsync(editor, more: false));
    }

    private void SetBeanError(BeanEditor editor, string? message)
    {
        if (!IsCurrentBean(editor)) return;
        editor.State.Error = message;
        editor.ErrorText.Text = message ?? "";
        editor.ErrorTile.Visibility = message is null ? ViewStates.Gone : ViewStates.Visible;
    }

    private void UpdateBeanTitle(BeanEditor editor)
    {
        var text = string.IsNullOrEmpty(editor.State.Draft.Name) ? "Loading…" : editor.State.Draft.Name;
        editor.Title.Text = text;
        editor.Title.SetTextSize(Android.Util.ComplexUnitType.Sp,
            text.Length <= 12 ? 28 : text.Length <= 20 ? 22 : text.Length <= 28 ? 18 : 16);
    }

    private void CaptureBeanScroll(BeanEditor editor)
    {
        editor.State.ScrollY = editor.Scroll.ScrollY;
        if (editor.HistoryList?.GetLayoutManager() is LinearLayoutManager manager)
        {
            var position = manager.FindFirstVisibleItemPosition();
            editor.State.HistoryPosition = Math.Max(0, position);
            editor.State.HistoryOffset = manager.FindViewByPosition(position)?.Top ?? 0;
        }
    }

    private void RestoreBeanScroll(BeanEditor editor)
    {
        EventHandler<View.LayoutChangeEventArgs>? restore = null;
        restore = (_, _) =>
        {
            if (!IsCurrentBean(editor) || editor.Scroll.Height <= 0) return;
            editor.Scroll.LayoutChange -= restore;
            editor.Scroll.ScrollTo(0, editor.State.ScrollY);
        };
        editor.Scroll.LayoutChange += restore;
        editor.Screen.OnDispose(() => editor.Scroll.LayoutChange -= restore);
        if (editor.HistoryList?.GetLayoutManager() is LinearLayoutManager manager)
            manager.ScrollToPositionWithOffset(editor.State.HistoryPosition, editor.State.HistoryOffset);
    }

    private async Task SaveBeanDetailsAsync(BeanEditor editor)
    {
        if (!IsCurrentBean(editor) || editor.Saving) return;
        var draft = editor.State.Draft;
        SetBeanError(editor, draft.ValidationError);
        if (draft.ValidationError is not null) return;
        var input = new BeanDraft
        {
            BeanId = draft.BeanId, Name = draft.Name, Roaster = draft.Roaster,
            Origin = draft.Origin, Notes = draft.Notes, RoasterUrl = draft.RoasterUrl
        };
        editor.Saving = true;
        editor.Save.Text = "SAVING…";
        HideKeyboard();
        try
        {
            await InScopeAsync(services => services.GetRequiredService<BeanWorkflow>().UpdateAsync(input));
            if (!IsCurrentBean(editor)) return;
            await _feedback.ShowSuccessAsync($"Bean '{input.Name}' updated");
            if (IsCurrentBean(editor)) await ReturnFromBeanDetailAsync();
        }
        catch (OperationCanceledException) when (editor.Cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Saving bean {BeanId} failed", input.BeanId);
            SetBeanError(editor, $"Failed to save: {ErrorMessage(exception)}");
        }
        finally
        {
            editor.Saving = false;
            if (IsCurrentBean(editor)) editor.Save.Text = "SAVE";
        }
    }

    private async Task DeleteBeanDetailsAsync(BeanEditor editor)
    {
        if (!IsCurrentBean(editor) || editor.State.Draft.BeanId is not int id) return;
        var name = editor.State.Draft.Name;
        await ConfirmBeanBagDeleteAsync(editor.Screen,
            new SimpleActionContent("Delete Bean?", $"Are you sure you want to delete '{name}'? This action cannot be undone.",
                "Delete", "BeanDelete"), $"Bean '{name}' deleted",
            async () => { await InScopeAsync(async services =>
            {
                await services.GetRequiredService<BeanWorkflow>().DeleteAsync(id);
                return true;
            }); }, ReturnFromBeanDetailAsync, message => SetBeanError(editor, message));
    }
}
