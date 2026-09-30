using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class NativePhotoModal : IDisposable
{
    private readonly Activity _activity;
    private readonly NativeStyle _style;
    private readonly NativeScreen _screen;
    private readonly FrameLayout _backdrop;
    private readonly LinearLayout _panel;
    private readonly FrameLayout _contentHost;
    private readonly Button _action;
    private readonly TextView _close;
    private readonly bool _dismissBackdrop;
    private readonly Func<bool> _ownerActive;
    private readonly Action _inputChanged;
    private readonly CancellationTokenSource _lifetime;
    private readonly CancellationToken _cancellation;
    private NativeScreen? _content;
    private Task? _closing;
    private bool _ready;
    private bool _covered;
    private bool _disposed;
    public bool IsVisible { get; private set; }
    public bool IsAlive => !_disposed && !_cancellation.IsCancellationRequested && _ownerActive();
    public bool CanInteract => IsAlive && _ready && !_covered;
    public CancellationToken Cancellation => _cancellation;
    public Action? ActionRequested { get; set; }
    public Action? CancelRequested { get; set; }

    public NativePhotoModal(Activity activity, NativeStyle style, string title, string prefix,
        bool dismissBackdrop, Func<bool> ownerActive, Action inputChanged, CancellationToken cancellation)
    {
        (_activity, _style, _dismissBackdrop, _ownerActive, _inputChanged) =
            (activity, style, dismissBackdrop, ownerActive, inputChanged);
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        _cancellation = _lifetime.Token;
        _backdrop = new FrameLayout(activity) { Clickable = true };
        _backdrop.SetFitsSystemWindows(false);
        _backdrop.SetBackgroundColor(Color.Argb(170, 0, 0, 0));
        NativeStyle.Identify(_backdrop, prefix + "Backdrop");
        _screen = new NativeScreen(_backdrop);
        _screen.Click(_backdrop, () => { if (_dismissBackdrop) RequestCancel(); });
        var safe = new AdviceSafeHost(activity, activity.Window?.DecorView
            ?? throw new InvalidOperationException("Photo modal window is unavailable."));
        safe.SetFitsSystemWindows(false);
        NativeStyle.Identify(safe, prefix + "SafeHost");
        _screen.Own(safe);
        _backdrop.AddView(safe, new FrameLayout.LayoutParams(-1, -1));
        _panel = style.Column();
        _panel.Clickable = true;
        _panel.SetFitsSystemWindows(false);
        _panel.SetClipChildren(false);
        _panel.SetClipToPadding(false);
        _panel.Background = style.Rounded(NativeStyle.ModalSurface, 24);
        _panel.SetPadding(style.Dp(24), style.Dp(24), style.Dp(24), style.Dp(24));
        NativeStyle.Identify(_panel, prefix + "Panel");
        safe.AddView(_panel, new FrameLayout.LayoutParams(-1, -1));
        var header = new FrameLayout(activity);
        var titleLabel = style.Label(title, 18, color: NativeStyle.ModalText);
        titleLabel.Typeface = style.Semibold;
        titleLabel.Gravity = GravityFlags.Center;
        header.AddView(titleLabel, new FrameLayout.LayoutParams(-1, -2));
        _close = style.Label("\ue14c", 24, color: NativeStyle.ModalText);
        _close.Typeface = style.Symbols;
        _close.Gravity = GravityFlags.Top | GravityFlags.End;
        _close.Focusable = true;
        _close.ContentDescription = "Close " + title;
        NativeStyle.Identify(_close, prefix + "Close");
        _screen.Click(_close, RequestCancel);
        header.AddView(_close, new FrameLayout.LayoutParams(style.Dp(44), -2, GravityFlags.End)
        {
            TopMargin = -style.Dp(4)
        });
        _panel.AddView(header);
        _contentHost = new FrameLayout(activity);
        _contentHost.SetClipChildren(false);
        _contentHost.SetClipToPadding(false);
        _panel.AddView(_contentHost, new LinearLayout.LayoutParams(-1, 0, 1) { TopMargin = style.Dp(16) });
        _action = style.Button("Create", prefix + "Action", NativeStyle.ModalText);
        _action.Typeface = Typeface.Default;
        _action.SetTextSize(Android.Util.ComplexUnitType.Sp, 16);
        _action.SetPadding(style.Dp(20), style.Dp(4), style.Dp(20), style.Dp(4));
        _action.Background = style.Rounded(style.Primary, 20);
        _screen.Click(_action, () => { if (CanInteract && _action.Enabled) ActionRequested?.Invoke(); });
        _panel.AddView(_action, new LinearLayout.LayoutParams(-1, style.Dp(54)) { TopMargin = style.Dp(16) });
        _action.Visibility = ViewStates.Gone;
        style.FixTheme(_backdrop);
    }

    public void SetContent(NativeScreen content)
    {
        if (!IsAlive) { content.Dispose(); return; }
        _contentHost.RemoveAllViews();
        _content?.Dispose();
        _content = content;
        _style.FixTheme(content.Root);
        _contentHost.AddView(content.Root, new FrameLayout.LayoutParams(-1, -1));
    }
    public void SetAction(bool visible, string text = "Create", bool enabled = true)
    {
        if (!IsAlive) return;
        _action.Visibility = visible ? ViewStates.Visible : ViewStates.Gone;
        _action.Text = text;
        _action.Enabled = enabled;
    }
    public void SetCovered(bool covered)
    {
        if (_disposed) return;
        _covered = covered;
        _backdrop.Enabled = !covered;
        _backdrop.ImportantForAccessibility = covered
            ? ImportantForAccessibility.NoHideDescendants : ImportantForAccessibility.Auto;
    }
    public async Task ShowAsync()
    {
        _cancellation.ThrowIfCancellationRequested();
        var window = _activity.Window?.DecorView as ViewGroup
            ?? throw new InvalidOperationException("Photo modal window is unavailable.");
        IsVisible = true;
        _panel.TranslationY = window.Height;
        window.AddView(_backdrop, new FrameLayout.LayoutParams(-1, -1));
        _inputChanged();
        try
        {
            await NativeModalMotion.RunAsync(_panel, _backdrop, true, _cancellation);
            _cancellation.ThrowIfCancellationRequested();
            _ready = true;
        }
        catch { Dispose(); throw; }
    }
    public void RequestCancel()
    {
        if (CanInteract) CancelRequested?.Invoke();
    }
    public Task CloseAsync() => _closing ??= CloseCoreAsync();
    private async Task CloseCoreAsync()
    {
        _ready = false;
        try
        {
            if (IsVisible) await NativeModalMotion.RunAsync(_panel, _backdrop, false, _cancellation);
            _cancellation.ThrowIfCancellationRequested();
            Detach();
        }
        catch
        {
            Detach();
            throw;
        }
    }
    private void Detach()
    {
        IsVisible = false;
        (_backdrop.Parent as ViewGroup)?.RemoveView(_backdrop);
        _inputChanged();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ready = false;
        _lifetime.Cancel();
        Detach();
        _content?.Dispose();
        _content = null;
        ActionRequested = null;
        CancelRequested = null;
        _screen.Dispose();
        _lifetime.Dispose();
    }
}
