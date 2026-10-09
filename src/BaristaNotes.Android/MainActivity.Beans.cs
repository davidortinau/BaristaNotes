using Android.Graphics;
using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private bool _beanCreateReturnToList;
    private BeanDetailState? _beanShotReturn;

    private async void ObserveBeanTask(Func<Task> operation)
    {
        if (_destroyed)
            return;
        try { await operation(); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Native bean/bag operation failed");
            if (!_destroyed)
                ShowFeedback(ErrorMessage(exception), isError: true);
        }
    }

    private async Task ShowBeansAsync()
    {
        ClearTransient();
        _page = "beans";
        _beanTitlePinned = false;
        var root = new EdgeAwareColumn(this, () => Window?.DecorView)
        {
            Orientation = Orientation.Vertical, ExtendBehindStatusBar = true
        };
        root.SetContentPadding(_style.Dp(1), 0, _style.Dp(1), _style.Dp(1));
        root.SetBackgroundColor(_style.Outline);
        var screen = new NativeScreen(root);
        var header = (ViewGroup)BuildHeader("BEANS", "0 beans", safeArea: false, compact: true);
        var count = (TextView)((ViewGroup)header.GetChildAt(0)!).GetChildAt(1)!;
        NativeStyle.Identify(count, "BeanCount");
        var map = AddBeanMap(screen);
        var status = _style.Column();
        status.SetGravity(GravityFlags.Center);
        status.SetPadding(_style.Dp(24), _style.Dp(24), _style.Dp(24), _style.Dp(24));
        var progress = new ProgressBar(this);
        status.AddView(progress);
        var title = _style.Label("", 10, true, _style.Secondary);
        title.LetterSpacing = 2 * .0624f;
        title.Gravity = GravityFlags.Center;
        title.Visibility = ViewStates.Gone;
        status.AddView(title);
        var message = _style.Label("", 16);
        message.Gravity = GravityFlags.Center;
        message.Visibility = ViewStates.Gone;
        NativeStyle.Identify(message, "BeanListMessage");
        status.AddView(message, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(12) });
        var retry = _style.Button("Retry", "BeanListRetry", _style.Surface);
        retry.Background = _style.Rounded(_style.Primary, 8);
        retry.Visibility = ViewStates.Gone;
        Bind(screen, retry, () => ObserveBeanTask(ShowBeansAsync));
        status.AddView(retry, new LinearLayout.LayoutParams(-2, -2) { TopMargin = _style.Dp(12) });
        var page = new BeanPageView(_style, map, header, status, id => Choice(() =>
            ObserveBeanTask(() => ShowBeanDetailAsync(new BeanDetailState(id), loadBean: true)))(),
            pinned =>
            {
                if (_destroyed || !ReferenceEquals(_transient, screen)) return;
                _beanTitlePinned = pinned;
                ApplyNativeWindowTheme();
            });
        var list = page.List;
        root.AddView(page, _style.Fill(weight: 1));
        screen.OnDispose(() =>
        {
            _beanListReturnState?.Dispose();
            _beanListReturnState = list.GetLayoutManager()?.OnSaveInstanceState();
        });
        screen.Own(page);
        var navigation = BuildNavigation(screen,
            ("\uefef", "New Drink", "NavDrink", () => { _editingShotId = null; ShowDrink(); }),
            ("\uf009", "Activity", "NavActivity", () => RunOperation(ShowHistoryAsync)),
            ("\ue8b8", "Settings", "BeansSettings", ShowSettings),
            ("\ue145", "Add bean", "BeansAdd", () => ShowBeanForm(returnToBeans: true)));
        var add = (Button)((LinearLayout)navigation).GetChildAt(3)!;
        add.SetTextColor(_style.Surface);
        add.SetBackgroundColor(_style.Text);
        root.AddView(navigation, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(1) });
        _transient = screen;
        Present(root, edgeToEdge: true);
        IReadOnlyList<BeanDto> activeBeans = [];
        var listLoaded = false;
        string? listError = null;

        void UpdateCount(IReadOnlyList<BeanDto> beans)
        {
            var beanCount = beans.Count == 1 ? "1 bean" : $"{beans.Count} beans";
            count.Text = map.HasSelection ? map.SelectionLabel : beanCount;
        }

        void RenderRows()
        {
            if (_destroyed || !ReferenceEquals(_transient, screen)) return;
            var display = BeanListReads.Display(listLoaded, map.WaitingForSelection, map.HasSelection, listError);
            if (display != BeanListReadState.Content)
            {
                count.Text = "0 beans";
                page.SetItems([]);
                status.Visibility = ViewStates.Visible;
                var error = listError;
                progress.Visibility = display == BeanListReadState.Loading ? ViewStates.Visible : ViewStates.Gone;
                title.Text = "ERROR";
                message.Text = error;
                title.Visibility = message.Visibility = retry.Visibility =
                    error == null ? ViewStates.Gone : ViewStates.Visible;
                page.RequestLayout();
                return;
            }
            var beans = map.HasSelection ? map.SelectedBeans : activeBeans;
            UpdateCount(beans);
            progress.Visibility = ViewStates.Gone;
            retry.Visibility = ViewStates.Gone;
            if (beans.Count == 0)
            {
                status.Visibility = ViewStates.Visible;
                page.SetItems([]);
                title.Text = "NO BEANS";
                message.Text = "Add your favorite coffee beans";
                title.Visibility = message.Visibility = ViewStates.Visible;
            }
            else
            {
                page.SetItems(beans);
                status.Visibility = ViewStates.Gone;
            }
            page.RequestLayout();
            RestoreBeanListPosition(list, screen);
        }

        EventHandler selection = (_, _) =>
        {
            if (_destroyed || !ReferenceEquals(_transient, screen)) return;
            _beanListReturnState?.Dispose();
            _beanListReturnState = null;
            var manager = list.GetLayoutManager() as AndroidX.RecyclerView.Widget.LinearLayoutManager;
            var first = manager?.FindFirstVisibleItemPosition() ?? 0;
            var heroOffset = manager?.FindViewByPosition(0)?.Top ?? 0;
            RenderRows();
            if (first == 0) manager?.ScrollToPositionWithOffset(0, heroOffset);
            else manager?.ScrollToPositionWithOffset(1, 0);
        };
        map.SelectionChanged += selection;
        screen.OnDispose(() => map.SelectionChanged -= selection);
        EventHandler origins = (_, _) =>
        {
            if (_destroyed || !ReferenceEquals(_transient, screen) || !map.HasSelection) return;
            UpdateCount(map.SelectedBeans);
            page.RequestLayout();
        };
        map.OriginsChanged += origins;
        screen.OnDispose(() => map.OriginsChanged -= origins);
        _ = LoadBeanOriginsAsync(map, screen, RenderRows);
        try
        {
            var beans = await InScopeAsync(services =>
                services.GetRequiredService<IBeanService>().GetAllActiveBeansAsync());
            if (_destroyed || !ReferenceEquals(_transient, screen))
                return;
            activeBeans = beans;
            listLoaded = true;
            if (beans.Count == 0)
                status.SetPadding(_style.Dp(32), _style.Dp(32), _style.Dp(32), _style.Dp(32));
            RenderRows();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Loading native beans failed");
            if (_destroyed || !ReferenceEquals(_transient, screen))
                return;
            listError = ErrorMessage(exception);
            RenderRows();
        }
    }

    private async Task LoadBeanOriginsAsync(BeanMapPanel map, NativeScreen screen, Action loaded)
    {
        try
        {
            var beans = await InScopeAsync(services =>
                services.GetRequiredService<IBeanService>().GetAllSavedBeansAsync());
            if (_destroyed || !ReferenceEquals(_transient, screen)) return;
            map.SetBeans(beans);
            loaded();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error)
        {
            _logger.LogError(error, "Loading native saved bean origins failed");
            if (!_destroyed && ReferenceEquals(_transient, screen))
            {
                map.ReportOriginError();
                loaded();
            }
        }
    }

    private void CancelBeanCreate()
    {
        HideKeyboard();
        if (TryReturnVoiceNavigation()) return;
        if (_beanCreateReturnToList)
            ObserveBeanTask(ShowBeansAsync);
        else
            ShowDrink();
    }

    private Button BeanAction(string text, string id, bool inverted = false, bool danger = false)
    {
        var button = _style.Button(text, id, inverted || danger ? _style.Surface : _style.Text);
        ConfigureBeanAction(button);
        button.SetBackgroundColor(danger ? _style.Error : inverted ? _style.Text : _style.Surface);
        return button;
    }

    private TextView BeanSectionCaption(string text)
    {
        var label = _style.Label(text, 10, true, _style.Secondary);
        label.LetterSpacing = 2 * .0624f;
        return label;
    }

    private LinearLayout BeanSection(string caption, int minimum = 0)
    {
        var section = _style.Column();
        section.SetBackgroundColor(_style.Surface);
        section.SetMinimumHeight(_style.Dp(minimum));
        section.SetPadding(_style.Dp(16), _style.Dp(14), _style.Dp(16), _style.Dp(14));
        section.AddView(BeanSectionCaption(caption));
        return section;
    }

    private Button BeanMiniAction(string text, string id)
    {
        var button = _style.Button(text, id, _style.Surface);
        button.SetBackgroundColor(_style.Text);
        button.Typeface = _style.Bold;
        button.SetTextSize(Android.Util.ComplexUnitType.Sp, 11);
        button.LetterSpacing = 1.5f * .0624f;
        button.SetPadding(_style.Dp(12), _style.Dp(8), _style.Dp(12), _style.Dp(8));
        button.SetMinWidth(0);
        button.SetMinimumWidth(0);
        button.SetMinHeight(_style.Dp(32));
        button.SetMinimumHeight(_style.Dp(32));
        return button;
    }

    private (LinearLayout Tile, TextView Message) BeanErrorTile()
    {
        var tile = _style.Column();
        tile.SetBackgroundColor(_style.Error);
        tile.SetMinimumHeight(_style.Dp(60));
        tile.SetPadding(_style.Dp(16), _style.Dp(12), _style.Dp(16), _style.Dp(12));
        tile.AccessibilityLiveRegion = AccessibilityLiveRegion.Assertive;
        var caption = _style.Label("ERROR", 10, true,
            Color.Argb(204, _style.Surface.R, _style.Surface.G, _style.Surface.B));
        caption.LetterSpacing = 2 * .0624f;
        tile.AddView(caption);
        var message = _style.Label("", 16, true, _style.Surface);
        tile.AddView(message);
        tile.Visibility = ViewStates.Gone;
        return (tile, message);
    }
}
