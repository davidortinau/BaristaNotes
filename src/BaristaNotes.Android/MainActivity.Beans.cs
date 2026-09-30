using Android.Graphics;
using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Services;
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
        var root = _style.Column();
        root.SetPadding(_style.Dp(1), _style.Dp(1), _style.Dp(1), _style.Dp(1));
        root.SetBackgroundColor(_style.Outline);
        var screen = new NativeScreen(root);
        var header = (ViewGroup)BuildHeader("BEANS", "0 beans");
        var count = (TextView)header.GetChildAt(1)!;
        NativeStyle.Identify(count, "BeanCount");
        root.AddView(header, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = _style.Dp(1) });
        var body = new FrameLayout(this);
        body.SetBackgroundColor(_style.Surface);
        var adapter = new BeanListAdapter(_style, id => Choice(() =>
            ObserveBeanTask(() => ShowBeanDetailAsync(new BeanDetailState(id), loadBean: true)))());
        var list = new SelectorRecyclerView(this);
        list.SetAdapter(adapter);
        list.SetBackgroundColor(_style.Outline);
        list.Visibility = ViewStates.Gone;
        NativeStyle.Identify(list, "BeanList");
        screen.Own(list);
        screen.Own(adapter);
        body.AddView(list, new FrameLayout.LayoutParams(-1, -1));
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
        body.AddView(status, new FrameLayout.LayoutParams(-1, -1));
        root.AddView(body, _style.Fill(weight: 1));
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
        try
        {
            var beans = await InScopeAsync(services =>
                services.GetRequiredService<IBeanService>().GetAllActiveBeansAsync());
            if (_destroyed || !ReferenceEquals(_transient, screen))
                return;
            count.Text = beans.Count == 1 ? "1 bean" : $"{beans.Count} beans";
            progress.Visibility = ViewStates.Gone;
            if (beans.Count == 0)
            {
                status.SetPadding(_style.Dp(32), _style.Dp(32), _style.Dp(32), _style.Dp(32));
                title.Text = "NO BEANS";
                message.Text = "Add your favorite coffee beans";
                title.Visibility = message.Visibility = ViewStates.Visible;
            }
            else
            {
                adapter.SetItems(beans);
                status.Visibility = ViewStates.Gone;
                list.Visibility = ViewStates.Visible;
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Loading native beans failed");
            if (_destroyed || !ReferenceEquals(_transient, screen))
                return;
            progress.Visibility = ViewStates.Gone;
            title.Text = "ERROR";
            message.Text = ErrorMessage(exception);
            title.Visibility = message.Visibility = retry.Visibility = ViewStates.Visible;
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
