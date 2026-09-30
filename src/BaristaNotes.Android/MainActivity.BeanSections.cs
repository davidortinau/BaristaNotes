using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using BaristaNotes.AndroidApp.Services;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private NativeScreen ReplaceBeanSection(BeanEditor editor, string key, FrameLayout host, View content)
    {
        host.RemoveAllViews();
        if (editor.Sections.Remove(key, out var old)) old.Dispose();
        var screen = new NativeScreen(content);
        editor.Sections.Add(key, screen);
        host.AddView(content, new FrameLayout.LayoutParams(-1, -2));
        return screen;
    }

    private void RenderBeanRatings(BeanEditor editor)
    {
        if (!IsCurrentBean(editor)) return;
        var section = BeanSection("RATINGS", 80);
        View content = editor.State.Ratings is { HasRatings: true } ratings
            ? new RatingSummaryView(_style, ratings)
            : _style.Label("No ratings yet", 16, color: _style.Secondary);
        section.AddView(content, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(10) });
        ReplaceBeanSection(editor, "ratings", editor.RatingsHost, section);
    }

    private async Task LoadBeanBagsAsync(BeanEditor editor)
    {
        if (!IsCurrentBean(editor) || editor.LoadingBags || editor.State.Draft.BeanId is not int id) return;
        editor.LoadingBags = true;
        RenderBeanBags(editor);
        try
        {
            var bags = await InScopeAsync(services =>
                services.GetRequiredService<IBagService>().GetBagSummariesForBeanAsync(id, includeCompleted: true));
            if (IsCurrentBean(editor)) editor.State.Bags = bags;
        }
        catch (OperationCanceledException) when (editor.Cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Loading bags for bean {BeanId} failed", id);
            SetBeanError(editor, $"Failed to load bags: {ErrorMessage(exception)}");
        }
        finally
        {
            editor.LoadingBags = false;
            if (IsCurrentBean(editor)) RenderBeanBags(editor);
        }
    }

    private void RenderBeanBags(BeanEditor editor)
    {
        if (!IsCurrentBean(editor)) return;
        var section = BeanSection("BAGS");
        var header = _style.Row();
        var caption = section.GetChildAt(0)!;
        section.RemoveView(caption);
        header.SetGravity(GravityFlags.CenterVertical);
        header.AddView(caption, new LinearLayout.LayoutParams(0, -2, 1));
        var add = BeanMiniAction("+ BAG", "BeanAddBag");
        header.AddView(add);
        section.AddView(header);
        var screen = ReplaceBeanSection(editor, "bags", editor.BagsHost, section);
        Bind(screen, add, () =>
        {
            CaptureBeanScroll(editor);
            ObserveBeanTask(() => ShowBagDetailAsync(editor.State, null));
        });
        if (editor.LoadingBags)
            section.AddView(new ProgressBar(this), new LinearLayout.LayoutParams(-2, -2)
            {
                Gravity = GravityFlags.CenterHorizontal, TopMargin = _style.Dp(10)
            });
        else if (editor.State.Bags.Count == 0)
            section.AddView(_style.Label("No bags added yet", 14, color: _style.Secondary),
                new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(10) });
        foreach (var bag in editor.State.Bags)
        {
            var card = _style.Column();
            card.SetBackgroundColor(_style.SurfaceVariant);
            card.SetPadding(_style.Dp(12), _style.Dp(12), _style.Dp(12), _style.Dp(12));
            var titleRow = _style.Row();
            titleRow.SetGravity(GravityFlags.CenterVertical);
            titleRow.AddView(_style.Label($"Roasted {bag.FormattedRoastDate}", 16, true),
                new LinearLayout.LayoutParams(0, -2, 1));
            var status = _style.Label(bag.StatusBadge.ToUpperInvariant(), 9, true, _style.Secondary);
            status.LetterSpacing = 1.5f * .0624f;
            status.SetPadding(_style.Dp(6), _style.Dp(2), _style.Dp(6), _style.Dp(2));
            titleRow.AddView(status);
            card.AddView(titleRow);
            if (bag.Notes is not null)
                card.AddView(_style.Label(bag.Notes, 13, color: _style.Secondary),
                    new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(6) });
            var stats = _style.Row();
            stats.SetGravity(GravityFlags.CenterVertical);
            stats.AddView(_style.Label($"{bag.ShotCount} shots", 12, color: _style.Secondary));
            if (bag.AverageRating is double average)
            {
                stats.AddView(_style.Label(bag.FormattedRating, 12, true),
                    new LinearLayout.LayoutParams(-2, -2) { LeftMargin = _style.Dp(16) });
                var icon = _style.Label(BeanDisplay.RatingGlyph((int)Math.Round(average)), 14);
                icon.Typeface = _style.Symbols;
                stats.AddView(icon, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = _style.Dp(4) });
            }
            else
                stats.AddView(_style.Label("no ratings", 12, color: _style.Secondary),
                    new LinearLayout.LayoutParams(-2, -2) { LeftMargin = _style.Dp(16) });
            card.AddView(stats, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(6) });
            NativeStyle.Identify(card, $"BeanBag_{bag.Id}");
            card.Focusable = true;
            Bind(screen, card, () =>
            {
                CaptureBeanScroll(editor);
                ObserveBeanTask(() => ShowBagDetailAsync(editor.State, bag.Id));
            });
            section.AddView(card, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(8) });
        }
    }

    private void BuildBeanHistory(BeanEditor editor)
    {
        var section = BeanSection("SHOT HISTORY");
        var screen = ReplaceBeanSection(editor, "history", editor.HistoryHost, section);
        editor.HistoryError = _style.Label("", 13, color: _style.Error);
        section.AddView(editor.HistoryError, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(10) });
        editor.HistoryRetry = BeanMiniAction("RETRY", "BeanHistoryRetry");
        Bind(screen, editor.HistoryRetry, () => ObserveBeanTask(() => LoadBeanShotsAsync(editor, more: false)));
        section.AddView(editor.HistoryRetry, new LinearLayout.LayoutParams(-2, -2) { TopMargin = _style.Dp(6) });
        editor.HistoryEmpty = _style.Label("No shots recorded with this bean yet", 14, color: _style.Secondary);
        section.AddView(editor.HistoryEmpty, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(10) });
        editor.HistoryInitialLoading = new ProgressBar(this);
        section.AddView(editor.HistoryInitialLoading, new LinearLayout.LayoutParams(-2, -2)
        {
            Gravity = GravityFlags.CenterHorizontal, TopMargin = _style.Dp(10)
        });
        var adapter = new BeanHistoryAdapter(_style, id => Choice(() =>
        {
            if (!IsCurrentBean(editor)) return;
            CaptureBeanScroll(editor);
            ObserveBeanTask(() => OpenBeanHistoryShotAsync(editor, id));
        })());
        var list = new SelectorRecyclerView(this);
        list.SetAdapter(adapter);
        NativeStyle.Identify(list, "BeanShotHistory");
        var listener = new BeanHistoryScrollListener(() =>
        {
            if (IsCurrentBean(editor) && editor.State.HasMoreShots && !editor.LoadingShots)
                ObserveBeanTask(() => LoadBeanShotsAsync(editor, more: true));
        });
        list.AddOnScrollListener(listener);
        screen.OnDispose(() => { list.RemoveOnScrollListener(listener); listener.Dispose(); });
        screen.Own(list);
        screen.Own(adapter);
        editor.HistoryList = list;
        editor.HistoryAdapter = adapter;
        section.AddView(list, new LinearLayout.LayoutParams(-1, _style.Dp(420)) { TopMargin = _style.Dp(10) });
        editor.HistoryMoreLoading = new ProgressBar(this);
        section.AddView(editor.HistoryMoreLoading, new LinearLayout.LayoutParams(-2, -2)
        {
            Gravity = GravityFlags.CenterHorizontal, TopMargin = _style.Dp(10)
        });
        RefreshBeanHistory(editor);
    }

    private async Task LoadBeanShotsAsync(BeanEditor editor, bool more)
    {
        if (!IsCurrentBean(editor) || editor.LoadingShots || (more && !editor.State.HasMoreShots)
            || editor.State.Draft.BeanId is not int id) return;
        var page = more ? editor.State.NextShotPage : 0;
        editor.LoadingShots = true;
        if (!more) editor.State.ShotError = null;
        RefreshBeanHistory(editor);
        try
        {
            var result = await InScopeAsync(services =>
                services.GetRequiredService<IShotService>().GetShotHistoryByBeanAsync(id, page, 20));
            if (!IsCurrentBean(editor)) return;
            editor.State.Shots = more ? editor.State.Shots.Concat(result.Items).ToList() : result.Items.ToList();
            editor.State.HasMoreShots = result.HasNextPage;
            editor.State.NextShotPage = page + 1;
            editor.State.HistoryLoaded = true;
        }
        catch (OperationCanceledException) when (editor.Cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Bean {BeanId} history page {Page} failed", id, page);
            if (IsCurrentBean(editor))
                editor.State.ShotError = $"Failed to load {(more ? "more shots" : "shots")}: {ErrorMessage(exception)}";
        }
        finally
        {
            editor.LoadingShots = false;
            if (IsCurrentBean(editor)) RefreshBeanHistory(editor);
        }
    }

    private void RefreshBeanHistory(BeanEditor editor)
    {
        if (!IsCurrentBean(editor) || editor.HistoryAdapter is null) return;
        editor.HistoryAdapter.SetItems(editor.State.Shots);
        editor.HistoryError!.Text = editor.State.ShotError ?? "";
        editor.HistoryError.Visibility = editor.HistoryRetry!.Visibility =
            editor.State.ShotError is null ? ViewStates.Gone : ViewStates.Visible;
        editor.HistoryList!.Visibility = editor.State.Shots.Count > 0 ? ViewStates.Visible : ViewStates.Gone;
        editor.HistoryEmpty!.Visibility = !editor.LoadingShots && editor.State.Shots.Count == 0
            && editor.State.ShotError is null ? ViewStates.Visible : ViewStates.Gone;
        editor.HistoryInitialLoading!.Visibility = editor.LoadingShots && editor.State.Shots.Count == 0
            ? ViewStates.Visible : ViewStates.Gone;
        editor.HistoryMoreLoading!.Visibility = editor.LoadingShots && editor.State.Shots.Count > 0
            ? ViewStates.Visible : ViewStates.Gone;
    }

    private async Task OpenBeanHistoryShotAsync(BeanEditor editor, int shotId)
    {
        var revision = _presentationRevision;
        var draft = new BaristaNotes.Core.Services.Workflows.DrinkDraft();
        bool IsCurrentRequest() => IsCurrentBean(editor) && revision == _presentationRevision;
        var loaded = await BeanHistoryLoad.RunAsync(
            () => LoadDraftAsync(draft, shotId),
            IsCurrentRequest,
            exception => _logger.LogError(exception, "Opening bean history shot {ShotId} failed", shotId),
            exception => ShowFeedback(ErrorMessage(exception), isError: true),
            () => _logger.LogDebug("Opening bean history shot {ShotId} was canceled", shotId));
        if (!loaded || !IsCurrentRequest()) return;
        CaptureBeanScroll(editor);
        HideKeyboard();
        _beanShotReturn = editor.State;
        _editDraft = draft;
        _editingShotId = shotId;
        ShowDrink();
    }

    private async Task ReturnToBeanFromShotAsync()
    {
        if (_beanShotReturn is not { } state) return;
        _beanShotReturn = null;
        if (_editEditor is { } editor)
        {
            _host.RemoveView(editor.Screen.Root);
            editor.Screen.Dispose();
            _editEditor = null;
        }
        _editDraft = null;
        _editingShotId = null;
        await ShowBeanDetailAsync(state, loadBean: false);
    }

    private async Task ConfirmBeanBagDeleteAsync(NativeScreen owner, SimpleActionContent content, string success,
        Func<Task> delete, Func<Task> returnToParent, Action<string> showError)
    {
        HideKeyboard();
        var committed = false;
        using var confirmation = new NativeArchiveConfirmation(this, _style, "",
            async cancellation =>
            {
                await delete();
                committed = true;
                cancellation.ThrowIfCancellationRequested();
                if (!_destroyed && ReferenceEquals(_transient, owner))
                    await _feedback.ShowSuccessAsync(success);
                cancellation.ThrowIfCancellationRequested();
            }, () => !_destroyed && !_feedback.IsVisible, _logger, _lifetime.Token, content);
        _equipmentConfirmation = confirmation;
        _host.Enabled = false;
        _host.ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants;
        try
        {
            var confirmed = await confirmation.ShowAsync();
            if (confirmed && !_destroyed && ReferenceEquals(_transient, owner))
                await returnToParent();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Bean/bag confirmation failed after commit={Committed}", committed);
            if (committed && !_destroyed && ReferenceEquals(_transient, owner))
                await returnToParent();
            else if (!_destroyed && ReferenceEquals(_transient, owner))
                showError($"Failed to delete: {ErrorMessage(exception)}");
        }
        finally
        {
            if (ReferenceEquals(_equipmentConfirmation, confirmation)) _equipmentConfirmation = null;
            if (!_destroyed)
            {
                var blocked = _feedback.IsVisible || _filterScreen is not null;
                _host.Enabled = !blocked;
                _host.ImportantForAccessibility = blocked
                    ? ImportantForAccessibility.NoHideDescendants : ImportantForAccessibility.Auto;
            }
        }
    }

    private sealed class BeanHistoryScrollListener(Action loadMore) : RecyclerView.OnScrollListener
    {
        public override void OnScrolled(RecyclerView recyclerView, int dx, int dy)
        {
            if (recyclerView.GetLayoutManager() is LinearLayoutManager manager
                && recyclerView.GetAdapter() is { ItemCount: > 0 } adapter
                && manager.FindLastVisibleItemPosition() >= adapter.ItemCount - 6)
                loadMore();
        }
    }
}
