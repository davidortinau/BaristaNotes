using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AndroidX.RecyclerView.Widget;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private TextView _historyCount = null!;
    private TextView _historyMessage = null!;
    private TextView _historyStatusTitle = null!;
    private ProgressBar _historyLoading = null!;
    private Button _historyRetry = null!;
    private Button _historyClear = null!;
    private Button _historyFilter = null!;
    private RecyclerView _historyList = null!;
    private ShotAdapter _shotAdapter = null!;

    private NativeScreen BuildHistory()
    {
        var column = _style.Column();
        column.SetBackgroundColor(_style.Outline);
        column.SetPadding(_style.Dp(1), _style.Dp(1), _style.Dp(1), _style.Dp(1));
        var screen = new NativeScreen(column);
        var header = new EdgeAwareColumn(this, () => Window?.DecorView) { Orientation = Orientation.Vertical };
        header.SetMinimumHeight(_style.Dp(120));
        header.SetBackgroundColor(_style.Surface);
        header.SetContentPadding(_style.Dp(16), _style.Dp(14), _style.Dp(16), _style.Dp(14));
        var caption = _style.Label("ACTIVITY", 10, true, _style.Secondary);
        caption.LetterSpacing = 2 * .0624f;
        header.AddView(caption);
        _historyCount = _style.Label("0 shots", 28, true);
        _historyCount.Gravity = GravityFlags.Bottom;
        NativeStyle.Identify(_historyCount, "ActivityCount");
        header.AddView(_historyCount, _style.Fill(weight: 1));
        column.AddView(header, new LinearLayout.LayoutParams(-1, ViewGroup.LayoutParams.WrapContent)
        {
            BottomMargin = _style.Dp(1)
        });
        var body = new FrameLayout(this);
        body.SetBackgroundColor(_style.Surface);
        _shotAdapter = new ShotAdapter(_style, id =>
        {
            if (_filterScreen is null && !_feedback.IsVisible)
                RunOperation(() => OpenEditAsync(id));
        });
        screen.Own(_shotAdapter);
        _historyList = new RecyclerView(this);
        var historyLayout = new LinearLayoutManager(this);
        screen.Own(historyLayout);
        _historyList.SetLayoutManager(historyLayout);
        _historyList.SetItemAnimator(null);
        _historyList.SetAdapter(_shotAdapter);
        _historyList.SetBackgroundColor(_style.Outline);
        NativeStyle.Identify(_historyList, "ActivityShots");
        body.AddView(_historyList, new FrameLayout.LayoutParams(-1, -1));
        var status = _style.Column();
        status.SetGravity(GravityFlags.Center);
        status.SetPadding(_style.Dp(24), _style.Dp(24), _style.Dp(24), _style.Dp(24));
        NativeStyle.Identify(status, "ActivityMessage");
        _historyLoading = new ProgressBar(this);
        status.AddView(_historyLoading);
        _historyStatusTitle = _style.Label("", 12, true, _style.Secondary);
        _historyStatusTitle.Gravity = GravityFlags.Center;
        status.AddView(_historyStatusTitle, new LinearLayout.LayoutParams(-1, -2)
        {
            BottomMargin = _style.Dp(12)
        });
        _historyMessage = _style.Label("", 18);
        _historyMessage.Gravity = GravityFlags.Center;
        status.AddView(_historyMessage);
        _historyRetry = _style.Button("Retry", "ActivityRetry");
        _historyRetry.SetTextColor(_style.Surface);
        _historyRetry.Background = _style.Rounded(_style.Primary, 8);
        Bind(screen, _historyRetry, () => RunOperation(ShowHistoryAsync));
        status.AddView(_historyRetry, new LinearLayout.LayoutParams(-2, -2)
        {
            TopMargin = _style.Dp(12)
        });
        _historyClear = _style.Button("Clear Filters", "ActivityClearFilters");
        _historyClear.SetTextColor(_style.Surface);
        _historyClear.Background = _style.Rounded(_style.Primary, 8);
        Bind(screen, _historyClear, () =>
        {
            _filters.Clear();
            RunOperation(ShowHistoryAsync);
        });
        status.AddView(_historyClear, new LinearLayout.LayoutParams(-2, -2)
        {
            TopMargin = _style.Dp(20)
        });
        body.AddView(status, new FrameLayout.LayoutParams(-1, -1));
        column.AddView(body, new LinearLayout.LayoutParams(-1, 0, 1) { BottomMargin = _style.Dp(1) });
        var navigation = BuildNavigation(screen,
            ("\uefef", "New Drink", "NavDrink", () => { _editingShotId = null; ShowDrink(); }),
            ("\ue8b8", "Settings", "NavSettings", OpenSettings),
            ("\ue152", "Filter Shots", "NavFilter", () => RunOperation(OpenFilterAsync)),
            ("\ue029", "Voice", "NavVoice", ToggleVoiceFromPage));
        _historyFilter = (Button)((LinearLayout)navigation).GetChildAt(2)!;
        column.AddView(navigation, new LinearLayout.LayoutParams(-1, ViewGroup.LayoutParams.WrapContent));
        return screen;
    }

    private async Task ShowHistoryAsync()
    {
        _beanShotReturn = null;
        ClearTransient();
        _editingShotId = null;
        _editDraft = null;
        if (_editEditor is not null)
        {
            _host.RemoveView(_editEditor.Screen.Root);
            _editEditor.Screen.Dispose();
            _editEditor = null;
        }
        _page = "history";
        _historyScreen ??= BuildHistory();
        Present(_historyScreen.Root, edgeToEdge: true);
        _historyList.Visibility = ViewStates.Gone;
        SetHistoryStatus(null, "Loading…");
        _historyLoading.Visibility = ViewStates.Visible;
        _historyRetry.Visibility = ViewStates.Gone;
        _historyClear.Visibility = ViewStates.Gone;
        _historyFilter.SetTextColor(_filters.HasFilters ? _style.Primary : _style.Text);
        try
        {
            var criteria = _filters.ToDto();
            var result = await InScopeAsync(async services =>
            {
                var shots = services.GetRequiredService<IShotService>();
                var page = criteria.HasFilters
                    ? await shots.GetFilteredShotHistoryAsync(criteria, pageIndex: 0, pageSize: 50)
                    : await shots.GetShotHistoryAsync(pageIndex: 0, pageSize: 50);
                var total = await shots.GetShotHistoryAsync(pageIndex: 0, pageSize: 1);
                return (Page: page, Total: total.TotalCount);
            });
            _historyCount.Text = _filters.HasFilters
                ? $"{result.Page.TotalCount} of {result.Total} shots"
                : result.Total == 1 ? "1 shot" : $"{result.Total} shots";
            _shotAdapter.SetItems(result.Page.Items.ToArray());
            _historyLoading.Visibility = ViewStates.Gone;
            if (result.Page.Items.Count == 0)
            {
                SetHistoryStatus(_filters.HasFilters ? "NO MATCHES" : "NO SHOTS YET",
                    _filters.HasFilters ? "Adjust or clear filters to see results." : "Log a drink to see it here.");
                _historyClear.Visibility = _filters.HasFilters ? ViewStates.Visible : ViewStates.Gone;
            }
            else
            {
                _historyMessage.Visibility = ViewStates.Gone;
                _historyStatusTitle.Visibility = ViewStates.Gone;
                _historyList.Visibility = ViewStates.Visible;
            }
            NotifyPerformanceHistoryReady(result.Total);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load native Activity");
            _historyLoading.Visibility = ViewStates.Gone;
            SetHistoryStatus("ERROR", ErrorMessage(exception), isError: true);
            _historyRetry.Visibility = ViewStates.Visible;
        }
    }

    partial void NotifyPerformanceHistoryReady(int totalCount);

    private void SetHistoryStatus(string? title, string message, bool isError = false)
    {
        _historyStatusTitle.Text = title ?? "";
        _historyStatusTitle.Visibility = title is null ? ViewStates.Gone : ViewStates.Visible;
        _historyStatusTitle.SetTextSize(Android.Util.ComplexUnitType.Sp, isError ? 10 : 12);
        _historyStatusTitle.LetterSpacing = (isError ? 2 : 3) * .0624f;
        _historyMessage.Text = message;
        _historyMessage.SetTextSize(Android.Util.ComplexUnitType.Sp, title is null ? 14 : 18);
        _historyMessage.SetTextColor(title is null ? _style.Secondary : _style.Text);
        _historyMessage.Visibility = ViewStates.Visible;
    }

    private async Task OpenEditAsync(int shotId)
    {
        var draft = new DrinkDraft();
        await LoadDraftAsync(draft, editingShotId: shotId);
        _editDraft = draft;
        _editingShotId = shotId;
        ShowDrink();
    }
}
