using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class NativeArchiveConfirmation : IDisposable
{
    private readonly Activity _activity;
    private readonly NativeScreen _screen;
    private readonly FrameLayout _overlay;
    private readonly LinearLayout _card;
    private readonly Button _cancel;
    private readonly Button _confirm;
    private readonly CancellationTokenSource _lifetime;
    private readonly CancellationToken _cancellation;
    private readonly Func<CancellationToken, Task> _archiveAndNotify;
    private readonly Func<bool> _canInteract;
    private readonly ILogger _logger;
    private readonly string _actionName;
    private readonly TaskCompletionSource<bool> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ViewGroup? _window;
    private bool _ready;
    private bool _closing;
    private bool _disposed;

    public NativeArchiveConfirmation(Activity activity, NativeStyle style, string equipmentName,
        Func<CancellationToken, Task> archiveAndNotify, Func<bool> canInteract,
        ILogger logger, CancellationToken cancellation, SimpleActionContent? content = null)
    {
        _activity = activity;
        _archiveAndNotify = archiveAndNotify;
        _canInteract = canInteract;
        _logger = logger;
        content ??= new SimpleActionContent("Archive Equipment?",
            $"Are you sure you want to archive '{equipmentName}'? This action cannot be undone.",
            "Archive", "EquipmentArchive");
        _actionName = content.ConfirmText;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        _cancellation = _lifetime.Token;
        _overlay = new FrameLayout(activity) { Clickable = true };
        _overlay.SetBackgroundColor(Color.Argb(170, 0, 0, 0));
        NativeStyle.Identify(_overlay, content.AutomationPrefix + "Backdrop");
        _screen = new NativeScreen(_overlay);
        _screen.Click(_overlay, RequestCancel);
        var safeHost = new PopupSafeHost(activity);
        _overlay.AddView(safeHost, new FrameLayout.LayoutParams(-1, -1));
        _card = style.Column();
        _card.Clickable = true;
        _card.Alpha = 0;
        _card.Background = style.Rounded(NativeStyle.ModalSurface, 20);
        _card.SetPadding(style.Dp(24), style.Dp(24), style.Dp(24), style.Dp(24));
        NativeStyle.Identify(_card, content.AutomationPrefix + "Card");
        var tablet = (activity.Resources?.Configuration?.SmallestScreenWidthDp ?? 0) >= 600;
        safeHost.AddView(_card, new FrameLayout.LayoutParams(tablet ? style.Dp(400) : -1, -2, GravityFlags.Center)
        {
            LeftMargin = style.Dp(30), RightMargin = style.Dp(30),
            TopMargin = style.Dp(30), BottomMargin = style.Dp(30)
        });
        var text = style.Column();
        var title = style.Label(content.Title, tablet ? 32 : 24, color: NativeStyle.ModalText);
        title.Typeface = style.Semibold;
        title.Gravity = GravityFlags.Center;
        NativeStyle.Identify(title, content.AutomationPrefix + "Title");
        text.AddView(title, new LinearLayout.LayoutParams(-1, -2));
        var message = style.Label(content.Message,
            tablet ? 16 : 12, color: NativeStyle.ModalSecondary);
        message.Gravity = GravityFlags.Center;
        NativeStyle.Identify(message, content.AutomationPrefix + "Message");
        text.AddView(message, new LinearLayout.LayoutParams(-1, -2) { TopMargin = style.Dp(4) });
        _card.AddView(text, new LinearLayout.LayoutParams(-1, -2)
        {
            LeftMargin = style.Dp(30), RightMargin = style.Dp(30)
        });
        var actions = style.Row();
        Button Action(string label, string id, bool primary)
        {
            var button = style.Button(label, id, primary ? NativeStyle.ModalText : style.Primary);
            // The pinned popup action styles do not set a custom FontFamily.
            button.Typeface = Typeface.Default;
            button.SetTextSize(Android.Util.ComplexUnitType.Sp, tablet ? 18 : 16);
            button.SetPadding(style.Dp(20), style.Dp(4), style.Dp(20), style.Dp(4));
            button.Background = style.Rounded(primary ? style.Primary : NativeStyle.ModalElevated, 20);
            return button;
        }
        _cancel = Action("Cancel", content.AutomationPrefix + "Cancel", primary: false);
        _confirm = Action(content.ConfirmText, content.AutomationPrefix + "Confirm", primary: true);
        _screen.Click(_cancel, RequestCancel);
        _screen.Click(_confirm, () => { _ = ConfirmAsync(); });
        _cancel.Enabled = _confirm.Enabled = false;
        actions.AddView(_cancel, new LinearLayout.LayoutParams(0, style.Dp(54), 1) { RightMargin = style.Dp(16) });
        actions.AddView(_confirm, new LinearLayout.LayoutParams(0, style.Dp(54), 1));
        _card.AddView(actions, new LinearLayout.LayoutParams(-1, -2) { TopMargin = style.Dp(12) });
        style.FixTheme(_overlay);
    }

    public async Task<bool> ShowAsync()
    {
        _cancellation.ThrowIfCancellationRequested();
        _window = _activity.Window?.DecorView as ViewGroup
            ?? throw new InvalidOperationException("The archive confirmation window is unavailable.");
        using var registration = _cancellation.Register(() => _result.TrySetCanceled(_cancellation));
        try
        {
            _window.AddView(_overlay, new FrameLayout.LayoutParams(-1, -1));
            await SimpleActionMotion.AppearAsync(_card, _window, _cancellation);
            _cancellation.ThrowIfCancellationRequested();
            SetReady(true);
            return await _result.Task;
        }
        finally
        {
            Dispose();
        }
    }

    public void RequestCancel()
    {
        if (!_ready || _disposed || !_canInteract())
            return;
        _ = CloseAsync(confirmed: false);
    }

    private async Task ConfirmAsync()
    {
        if (!_ready || _disposed || !_canInteract())
            return;
        SetReady(false);
        try
        {
            // Source order: archive -> success feedback -> popup exit -> caller return.
            await _archiveAndNotify(_cancellation);
            _cancellation.ThrowIfCancellationRequested();
            await CloseAsync(confirmed: true);
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            _result.TrySetCanceled(_cancellation);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Native {ActionName} confirmation action failed", _actionName);
            _result.TrySetException(exception);
        }
    }

    private async Task CloseAsync(bool confirmed)
    {
        if (_closing || _disposed || _window is null)
            return;
        _closing = true;
        SetReady(false);
        try
        {
            await SimpleActionMotion.DisappearAsync(_card, _window, _cancellation);
            _result.TrySetResult(confirmed);
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            _result.TrySetCanceled(_cancellation);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Native {ActionName} confirmation exit failed", _actionName);
            _result.TrySetException(exception);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _ready = false;
        _lifetime.Cancel();
        _result.TrySetCanceled(_cancellation);
        (_overlay.Parent as ViewGroup)?.RemoveView(_overlay);
        _screen.Dispose();
        _lifetime.Dispose();
    }

    private void SetReady(bool ready)
    {
        _ready = ready;
        _cancel.Enabled = _confirm.Enabled = ready;
    }

    private sealed class PopupSafeHost(Activity activity) : FrameLayout(activity)
    {
        protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
        {
            var window = activity.Window?.DecorView;
            if (window is not null)
            {
                var insets = WindowEdgeInsets.Read(window);
                if (PaddingLeft != insets.Left || PaddingTop != insets.Top || PaddingRight != insets.Right)
                    SetPadding(insets.Left, insets.Top, insets.Right, 0);
            }
            base.OnLayout(changed, left, top, right, bottom);
        }
    }
}
