using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Grind;
using BaristaNotes.Core.Services.Workflows;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SafariServices;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class BeanDetailViewController : BeanBagPage
{
    private readonly BeanDraft _draft;
    private readonly BeanField _name;
    private readonly BeanField _roaster;
    private readonly BeanField _origin;
    private readonly BeanField _url;
    private readonly BeanNotes _notes;
    private readonly BeanSection _ratings;
    private readonly BeanSection _recipes;
    private readonly BeanSection _bags;
    private readonly BeanSection _historySection;
    private readonly BeanHistoryList _history;
    private readonly BeanMiniAction _recipeAction;
    private readonly BeanMiniAction _bagAction;
    private EquipmentActionButton? _save;
    private RatingAggregateDto? _rating;
    private List<BagSummaryDto> _bagItems = [];
    private List<RecipeDto> _recipeItems = [];
    private List<ShotRecordDto> _shots = [];
    private Dictionary<int, GrindTranslationResult> _translations = [];
    private bool _loaded, _initializing, _saving, _committed, _hasMore, _loadingShots, _loadingBags, _loadingRecipes, _refreshingRecipes, _translating;
    private bool _uncertainCreate, _deleted;
    private int _pageIndex, _bagVersion, _recipeVersion, _shotVersion;
    private int? _grinderId;
    private string? _shotError, _recipeError;
    private CancellationTokenSource? _translation;
    private const int PageSize = 20;

    public BeanDetailViewController(SliceNavigationController host, int? beanId = null) : base(host, "bean")
    {
        _draft = new BeanDraft { BeanId = beanId };
        var weak = new WeakReference<BeanDetailViewController>(this);
        _name = new BeanField("NAME", "Bean name (required)", "bean.name", 22, 100,
            value => { if (weak.TryGetTarget(out var owner)) { owner._draft.Name = value; owner.UpdateHeader(); } });
        _roaster = new BeanField("ROASTER", "Roaster name", "bean.roaster", 18, 90,
            value => { if (weak.TryGetTarget(out var owner)) owner._draft.Roaster = value; });
        _origin = new BeanField("ORIGIN", "Country or region", "bean.origin", 18, 90,
            value => { if (weak.TryGetTarget(out var owner)) owner._draft.Origin = value; });
        _url = new BeanField("ROASTER URL", "https://… (where to reorder these beans)", "bean.url", 18, 90,
            value => { if (weak.TryGetTarget(out var owner)) owner._draft.RoasterUrl = value; });
        _url.Entry.KeyboardType = UIKeyboardType.Url;
        _url.Entry.AutocapitalizationType = UITextAutocapitalizationType.None;
        _notes = new BeanNotes("Tasting notes, processing method…", "bean.notes",
            value => { if (weak.TryGetTarget(out var owner)) owner._draft.Notes = value; });
        _recipeAction = new BeanMiniAction("FIND", "bean.recipes.refresh", WeakUiCallback.Create(this, static owner => _ = owner.RefreshRecipesAsync()));
        _bagAction = new BeanMiniAction("+ BAG", "bean.bags.add", WeakUiCallback.Create(this, static owner => owner.OpenBag(null)));
        _ratings = new BeanSection("RATINGS", "bean.ratings", minimum: 80);
        _recipes = new BeanSection("RECIPES", "bean.recipes", action: _recipeAction);
        _bags = new BeanSection("BAGS", "bean.bags", action: _bagAction);
        _historySection = new BeanSection("SHOT HISTORY", "bean.history.section");
        _history = new BeanHistoryList(id =>
        {
            if (weak.TryGetTarget(out var owner) && owner.Current(owner.Generation)) owner.Host.Edit(id);
        }, WeakUiCallback.Create(this, static owner =>
        {
            if (owner._hasMore && !owner._loadingShots && owner.Current(owner.Generation)) _ = owner.LoadShotsAsync(true);
        }));
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        foreach (var field in new UIView[] { _name, _roaster, _origin, _url, _notes }) AddSection(field);
        if (_draft.IsEditing)
            foreach (var section in new[] { _ratings, _recipes, _bags, _historySection }) AddSection(section);
        AddSection(new BeanSpacer(24) { BackgroundColor = NativeTheme.Surface });
        AddAction(new EquipmentActionButton("CANCEL", "bean.cancel", WeakUiCallback.Create(this, static owner =>
        {
            if (owner.Current(owner.Generation) && owner.Host.PresentedViewController == null) owner.Host.RequestBack();
        })));
        if (_draft.IsEditing)
            AddAction(new EquipmentActionButton("DELETE", "bean.delete", WeakUiCallback.Create(this, static owner => owner.ConfirmDelete()), danger: true));
        _save = new EquipmentActionButton(_draft.IsEditing ? "SAVE" : "ADD", "bean.add",
            WeakUiCallback.Create(this, static owner => _ = owner.SaveAsync()), inverted: true);
        AddAction(_save);
        UpdateHeader();
        RefreshFonts();
    }
    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        if (!_initializing && !_loaded)
        {
            _initializing = true;
            if (_draft.IsEditing) _ = LoadAsync(); else _loaded = true;
        }
        else if (_loaded && _draft.IsEditing)
        {
            // Source reappearance deliberately refreshes only bags and recipes,
            // not editable fields, ratings, history paging or parent scroll.
            _ = LoadBagsAsync();
            _ = LoadRecipesAsync();
        }
    }
    private void UpdateHeader()
    {
        Header.Update(_draft.IsEditing ? "EDIT BEAN" : "NEW BEAN",
            _draft.IsEditing ? string.IsNullOrEmpty(_draft.Name) ? "Loading…" : _draft.Name : "Add bean");
        if (_save != null) _save.AccessibilityIdentifier = _draft.IsEditing ? "bean.save" : "bean.add";
        Relayout();
    }
    private async Task LoadAsync()
    {
        var generation = Generation;
        SetLoading(true);
        try
        {
            var bean = await Services.RunAsync(provider => provider.GetRequiredService<IBeanService>().GetBeanWithRatingsAsync(_draft.BeanId!.Value));
            if (!Alive(generation)) return;
            if (bean == null) { SetError("Bean not found"); return; }
            _draft.ApplyLoadedData(bean);
            _name.Text = _draft.Name;
            _roaster.Text = _draft.Roaster;
            _origin.Text = _draft.Origin;
            _url.Text = _draft.RoasterUrl;
            _notes.Text = _draft.Notes;
            _rating = bean.RatingAggregate;
            _loaded = true;
            UpdateHeader();
            RenderRatings();
            _ = LoadBagsAsync();
            _ = LoadRecipesAsync();
            _ = LoadShotsAsync(false);
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Bean detail load failed for {BeanId}", _draft.BeanId);
            if (Current(generation)) SetError($"Failed to load bean: {error.Message}");
        }
        finally { if (Current(generation)) SetLoading(false); }
    }
    private BeanDraft Snapshot() => new()
    {
        BeanId = _draft.BeanId, Name = _draft.Name, Roaster = _draft.Roaster,
        Origin = _draft.Origin, Notes = _draft.Notes, RoasterUrl = _draft.RoasterUrl
    };
    private async Task SaveAsync()
    {
        if (!_loaded || _saving || _committed || _uncertainCreate || !Current(Generation) || Host.PresentedViewController != null) return;
        var snapshot = Snapshot();
        if (snapshot.ValidationError is { } validation) { SetError(validation); return; }
        var generation = Generation;
        _saving = true;
        _save?.SetText("SAVING…");
        SetError(null);
        try
        {
            string message;
            bool partial = false;
            if (snapshot.IsEditing)
            {
                await Services.RunAsync(provider => provider.GetRequiredService<BeanWorkflow>().UpdateAsync(snapshot));
                _committed = true;
                message = $"Bean '{snapshot.Name}' updated";
            }
            else
            {
                var input = snapshot.ToCreateDto();
                var result = await Services.RunAsync(provider => provider.GetRequiredService<BeanCreationWorkflow>().CreateWithInitialBagAsync(input));
                if (!Current(generation)) return;
                if (!result.Bean.Success) { SetError(result.Bean.ErrorMessage ?? "Failed to create bean"); return; }
                if (result.Bean.Data == null) { SetError("Bean creation did not return the new bean"); return; }
                _committed = true;
#if DEBUG
                NativeReadFaults.BeanCreated();
#endif
                partial = result.InitialBag?.Success != true;
                message = partial ? $"Bean saved, but initial bag failed: {result.InitialBag?.ErrorMessage ?? "Unknown error"}"
                    : $"Bean '{snapshot.Name}' created";
            }
            if (!Current(generation)) return;
            Root.EndEditing(true);
            if (partial) ShowFeedback(message, isError: true);
            else await Host.FeedbackHost.ShowAndWaitAsync(message);
            if (Current(generation)) Host.RequestBack();
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Bean save failed for {BeanId}", snapshot.BeanId);
            if (!snapshot.IsEditing) _uncertainCreate = true; // No result can establish whether BeanCreated already committed.
            if (Current(generation)) SetError(_committed || _uncertainCreate
                ? $"Save status requires checking the bean list before retrying: {error.Message}" : $"Failed to save: {error.Message}");
        }
        finally
        {
            _saving = false;
            if (Current(generation))
            {
                _save?.SetText(_draft.IsEditing ? "SAVE" : "ADD");
                if (_save != null) _save.Enabled = !_committed && !_uncertainCreate;
            }
        }
    }
    private void OpenBag(int? id)
    {
        if (!_draft.IsEditing || !Current(Generation) || Host.PresentedViewController != null) return;
        Host.PushHierarchy(new BagDetailViewController(Host, _draft.BeanId!.Value, _draft.Name, id));
    }
    private void ConfirmDelete()
    {
        if (!_loaded || !_draft.IsEditing || !Current(Generation) || Host.PresentedViewController != null) return;
        var id = _draft.BeanId!.Value;
        var name = _draft.Name;
        var generation = Generation;
        var weak = new WeakReference<BeanDetailViewController>(this);
        Host.PresentViewController(new EquipmentConfirmationViewController(Host, "Delete Bean?",
            $"Are you sure you want to delete '{name}'? This action cannot be undone.", "Delete",
            async () =>
            {
                if (!weak.TryGetTarget(out var owner)) return;
                if (!owner._deleted)
                {
                    await owner.Services.RunAsync(async provider => { await provider.GetRequiredService<BeanWorkflow>().DeleteAsync(id); return true; });
                    owner._deleted = true;
                }
                if (owner.Alive(generation)) await owner.Host.FeedbackHost.ShowAndWaitAsync($"Bean '{name}' deleted");
            }, () => { if (weak.TryGetTarget(out var owner) && owner.Current(generation)) owner.Host.RequestBack(); },
            operation: "delete bean"), false, null);
    }
    private async Task LoadBagsAsync()
    {
        if (!_draft.IsEditing || _loadingBags || Removed) return;
        var generation = Generation;
        var version = ++_bagVersion;
        _loadingBags = true;
        RenderBags();
        try
        {
            var bags = await Services.RunAsync(provider => provider.GetRequiredService<IBagService>().GetBagSummariesForBeanAsync(_draft.BeanId!.Value, true));
            if (!Alive(generation) || version != _bagVersion) return;
            _bagItems = bags;
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Bean bags load failed");
            if (Current(generation)) SetError($"Failed to load bags: {error.Message}");
        }
        finally { _loadingBags = false; if (Alive(generation)) RenderBags(); }
    }
    private async Task LoadShotsAsync(bool more)
    {
        if (!_draft.IsEditing || _loadingShots || Removed || more && !_hasMore) return;
        var generation = Generation;
        var version = ++_shotVersion;
        var page = more ? _pageIndex : 0;
        _loadingShots = true;
        _shotError = null;
        RenderHistory();
        try
        {
            var result = await Services.RunAsync(provider => provider.GetRequiredService<IShotService>()
                .GetShotHistoryByBeanAsync(_draft.BeanId!.Value, page, PageSize));
            if (!Alive(generation) || version != _shotVersion) return;
            _shots = more ? _shots.Concat(result.Items).ToList() : result.Items.ToList();
            _hasMore = result.HasNextPage;
            _pageIndex = page + 1;
            var offset = _history.ContentOffset;
            _history.Update(_shots, TraitCollection, more);
            if (more) _history.SetContentOffset(offset, false);
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Bean shot history page {Page} failed", page);
            if (Alive(generation)) _shotError = $"Failed to load {(more ? "more " : "")}shots: {error.Message}";
        }
        finally { _loadingShots = false; if (Alive(generation)) RenderHistory(); }
    }
    private async Task LoadRecipesAsync()
    {
        if (!_draft.IsEditing || _loadingRecipes || Removed) return;
        var generation = Generation;
        var version = ++_recipeVersion;
        _loadingRecipes = true;
        _recipeError = null;
        RenderRecipes();
        try
        {
            var recipes = await Services.RunAsync(provider => provider.GetRequiredService<IRecipeService>().GetRecipesForBeanAsync(_draft.BeanId!.Value));
            if (!Alive(generation) || version != _recipeVersion) return;
            _recipeItems = recipes.ToList();
            _ = TranslateAsync();
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Bean recipes read failed");
            if (Alive(generation)) _recipeError = $"Failed to load recipes: {error.Message}";
        }
        finally { _loadingRecipes = false; if (Alive(generation)) RenderRecipes(); }
    }
    private async Task RefreshRecipesAsync()
    {
        if (!_draft.IsEditing || _refreshingRecipes || !Current(Generation)) return;
        var generation = Generation;
        _refreshingRecipes = true;
        _recipeError = null;
        RenderRecipes();
        try
        {
            var recipes = await Services.RunAsync(provider => provider.GetRequiredService<IBeanService>().RefreshRecipesAsync(_draft.BeanId!.Value));
            if (!Current(generation)) return;
            _recipeItems = recipes.ToList();
            _ = TranslateAsync();
            Host.FeedbackHost.Show(
                recipes.Count == 0 ? "No recipes found for this bean yet." : $"Refreshed {recipes.Count} recipe(s).",
                recipes.Count == 0 ? NativeFeedbackKind.Information : NativeFeedbackKind.Success);
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Bean recipe refresh failed");
            if (Current(generation)) { _recipeError = $"Failed to refresh recipes: {error.Message}"; ShowFeedback("Recipe refresh failed.", isError: true); }
        }
        finally { _refreshingRecipes = false; if (Alive(generation)) RenderRecipes(); }
    }
    private async Task TranslateAsync()
    {
        _translation?.Cancel();
        _translation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _translation = cancellation;
        var token = cancellation.Token;
        var generation = Generation;
        var recipes = _recipeItems.ToArray();
        _translating = true;
        try
        {
            var result = await Services.RunAsync(async provider =>
            {
                var grinders = await provider.GetRequiredService<IEquipmentRepository>().GetByTypeAsync(EquipmentType.Grinder);
                token.ThrowIfCancellationRequested();
                var grinder = grinders.FirstOrDefault(item => item.IsActive && !item.IsDeleted);
                var translations = new Dictionary<int, GrindTranslationResult>();
                if (grinder == null) return (Id: (int?)null, Values: translations);
                await provider.GetRequiredService<IGrinderProfileRepository>().GetOrCreateForEquipmentAsync(grinder);
                token.ThrowIfCancellationRequested();
                foreach (var recipe in recipes.Where(recipe => !string.IsNullOrWhiteSpace(recipe.GrindHint)))
                {
                    try
                    {
                        translations[recipe.Id] = await provider.GetRequiredService<IGrindTranslationService>().TranslateAsync(
                            new GrindTranslationRequest(EquipmentId: grinder.Id, GrinderModel: grinder.Name,
                                GrindHint: recipe.GrindHint!, Method: recipe.BrewMethod, BeanId: _draft.BeanId), token);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception error) { Logger.LogWarning(error, "Recipe {RecipeId} grind translation unavailable", recipe.Id); }
                }
                return (Id: (int?)grinder.Id, Values: translations);
            });
            if (!Alive(generation) || token.IsCancellationRequested) return;
            _grinderId = result.Id;
            _translations = result.Values;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { Logger.LogWarning(error, "Recipe grinder resolution failed"); }
        finally
        {
            if (Alive(generation) && !token.IsCancellationRequested) { _translating = false; RenderRecipes(); }
        }
    }
    private void RenderRatings() => _ratings.SetItems(_rating is { HasRatings: true }
        ? [new BeanRatingView(_rating, TraitCollection)] : [BeanBagUi.Text("No ratings yet", 16, false, NativeTheme.Secondary, TraitCollection)]);
    private UIActivityIndicatorView Spinner()
    {
        var spinner = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Medium) { Color = NativeTheme.Primary };
        spinner.StartAnimating();
        return spinner;
    }
    private void RenderBags()
    {
        var lines = new List<UIView>();
        var cards = new List<UIView>();
        if (_loadingBags) lines.Add(Spinner());
        if (!_loadingBags && _bagItems.Count == 0) lines.Add(BeanBagUi.Text("No bags added yet", 14, false, NativeTheme.TextPrimary, TraitCollection));
        foreach (var bag in _bagItems)
        {
            var details = new List<UIView>();
            if (bag.Notes != null) details.Add(BeanBagUi.Text(bag.Notes, 13, false, NativeTheme.Secondary, TraitCollection));
            details.Add(new BagStatsLine(bag, TraitCollection));
            cards.Add(new BeanReadCard($"Roasted {bag.FormattedRoastDate}", bag.StatusBadge.ToUpperInvariant(), details,
                TraitCollection, $"bean.bag.{bag.Id}", WeakUiCallback.Create(this, bag.Id, static (owner, id) => owner.OpenBag(id))));
        }
        if (cards.Count > 0) lines.Add(new BeanVerticalGroup(8, cards.ToArray()));
        _bags.SetItems(lines);
        Relayout();
    }
    private void RenderHistory()
    {
        var lines = new List<UIView>();
        if (_shotError != null)
        {
            lines.Add(new BeanVerticalGroup(6, BeanBagUi.Text(_shotError, 13, false, NativeTheme.Error, TraitCollection),
                new BeanMiniAction("RETRY", "bean.history.retry", WeakUiCallback.Create(this, static owner => _ = owner.LoadShotsAsync(false)))));
        }
        if (_loadingShots && _shots.Count == 0) lines.Add(Spinner());
        if (!_loadingShots && _shots.Count == 0 && _shotError == null)
            lines.Add(BeanBagUi.Text("No shots recorded with this bean yet", 14, false, NativeTheme.TextPrimary, TraitCollection));
        if (_shots.Count > 0) lines.Add(_history);
        if (_loadingShots && _shots.Count > 0) lines.Add(Spinner());
        _historySection.SetItems(lines);
        Relayout();
    }
    private void RenderRecipes()
    {
        _recipeAction.Update(_refreshingRecipes ? "…" : _recipeItems.Count == 0 ? "FIND" : "REFRESH", TraitCollection);
        _recipeAction.Enabled = !_refreshingRecipes;
        _recipes.SetBusy(_refreshingRecipes);
        var lines = new List<UIView>();
        if (_recipeError != null) lines.Add(BeanBagUi.Text(_recipeError, 13, false, NativeTheme.Error, TraitCollection));
        if (_loadingRecipes && _recipeItems.Count == 0) lines.Add(Spinner());
        if (!_loadingRecipes && !_refreshingRecipes && _recipeItems.Count == 0 && _recipeError == null)
        {
            lines.Add(new BeanVerticalGroup(4,
                BeanBagUi.Text("No recipes yet.", 14, false, NativeTheme.TextPrimary, TraitCollection),
                BeanBagUi.Text("Tap REFRESH to look up brew guides.", 12, false, NativeTheme.Secondary, TraitCollection)));
        }
        var cards = new List<UIView>();
        foreach (var recipe in _recipeItems)
        {
            var details = new List<UIView>();
            if (!string.IsNullOrWhiteSpace(recipe.Title)) details.Add(BeanBagUi.Text(recipe.Title, 12, false, NativeTheme.Secondary, TraitCollection));
            var hasParameters = BeanDisplay.HasRecipeParameters(recipe);
            details.Add(BeanBagUi.Text(BeanDisplay.RecipeParameters(recipe), hasParameters ? 13 : 12, hasParameters,
                hasParameters ? NativeTheme.TextPrimary : NativeTheme.Secondary, TraitCollection));
            if (!string.IsNullOrWhiteSpace(recipe.GrindHint))
            {
                details.Add(BeanBagUi.Text("Grind: " + recipe.GrindHint, 12, false, NativeTheme.Secondary, TraitCollection));
                if (_grinderId.HasValue)
                {
                    var translation = BeanRecipePresentation.Translation(_translations.GetValueOrDefault(recipe.Id),
                        _translating && !_translations.ContainsKey(recipe.Id), TraitCollection);
                    if (translation != null) details.Add(translation);
                }
            }
            if (!string.IsNullOrWhiteSpace(recipe.Notes)) details.Add(BeanBagUi.Text(recipe.Notes, 12, false, NativeTheme.Secondary, TraitCollection));
            if (!string.IsNullOrWhiteSpace(recipe.SourceUrl))
                details.Add(new BeanRecipePresentation.BeanSourceLink($"bean.recipe.{recipe.Id}.source", TraitCollection,
                    WeakUiCallback.Create(this, recipe.SourceUrl, static (owner, url) => owner.OpenSource(url))));
            cards.Add(new BeanReadCard(recipe.BrewMethod.DisplayName(), BeanDisplay.RecipeSourceText(recipe.Source), details,
                TraitCollection, $"bean.recipe.{recipe.Id}", extraBadge: recipe.IsEditedByUser ? "EDITED" : null, headingSpacing: 6));
        }
        if (cards.Count > 0) lines.Add(new BeanVerticalGroup(8, cards.ToArray()));
        _recipes.SetItems(lines);
        Relayout();
    }
    private void OpenSource(string url)
    {
        if (!Current(Generation) || Host.PresentedViewController != null) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            ShowFeedback("Could not open recipe source.", isError: true);
            return;
        }
        Host.PresentViewController(new SFSafariViewController(new NSUrl(uri.AbsoluteUri)), true, null);
    }
    protected override void RefreshFonts()
    {
        base.RefreshFonts();
        foreach (var field in new[] { _name, _roaster, _origin, _url }) field.UpdateFonts(TraitCollection);
        _notes.UpdateFonts(TraitCollection);
        foreach (var section in new[] { _ratings, _recipes, _bags, _historySection }) section.UpdateFonts(TraitCollection);
        _bagAction.Update("+ BAG", TraitCollection);
        var offset = _history.ContentOffset;
        _history.Update(_shots, TraitCollection, false);
        _history.SetContentOffset(offset, false);
        RenderRatings(); RenderBags(); RenderRecipes(); RenderHistory();
    }
    protected override void OnRemoved()
    {
        _translation?.Cancel();
        _translation?.Dispose();
        _translation = null;
    }
}
