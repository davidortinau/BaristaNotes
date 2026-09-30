using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Services;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private PhotoSession? _photoSession;
    private bool PhotoBlocksInput => _photoSession?.BlocksInput == true;

    private async void RequestPhoto(DrinkEditor editor)
    {
        if (_destroyed || _photoSession is not null || _page != "drink"
            || _editingShotId.HasValue || !ReferenceEquals(_newEditor, editor)) return;
        PhotoSession? session = null;
        try
        {
            session = new PhotoSession(this, editor);
            _photoSession = session;
            await session.RunAsync();
        }
        catch (OperationCanceledException) when (session?.IsCurrent != true || _destroyed) { }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Native photo workflow host failed");
            if (!_destroyed && (session is null || session.IsCurrent)) ShowFeedback(ErrorMessage(exception), isError: true);
        }
        finally
        {
            if (session is { HasCoffeePopup: false }) ReleasePhotoSession(session);
        }
    }

    private void CancelPhotoWhenLeaving(View next)
    {
        if (_photoSession is { } session && !ReferenceEquals(next, session.Editor.Screen.Root))
            ReleasePhotoSession(session);
    }

    private void ReleasePhotoSession(PhotoSession session)
    {
        if (ReferenceEquals(_photoSession, session)) _photoSession = null;
        session.Dispose();
        UpdateVoiceInputGate();
    }

    private sealed class PhotoSession : IPhotoWorkflowHost, IDisposable
    {
        private readonly MainActivity _activity;
        private readonly CancellationTokenSource _lifetime;
        private readonly CancellationToken _cancellation;
        private readonly VoiceUiDispatcher _dispatcher;
        private readonly AndroidVoiceCapture _capture;
        private readonly IVisionService _vision;
        private readonly PhotoWorkflow _workflow;
        private NativeScreen? _processing;
        private NativePhotoIntentPopup? _intent;
        private NativeAddCoffeePopup? _coffee;
        private AlertDialog? _alert;
        private bool _disposed;
        public DrinkEditor Editor { get; }
        public bool HasCoffeePopup => _coffee is not null;
        public bool BlocksInput => _processing is not null || _intent is not null || _coffee is not null;
        public bool IsCurrent => !_disposed && !_cancellation.IsCancellationRequested && !_activity._destroyed
            && ReferenceEquals(_activity._photoSession, this) && _activity._page == "drink"
            && !_activity._editingShotId.HasValue && ReferenceEquals(_activity._newEditor, Editor)
            && ReferenceEquals(_activity._host.GetChildAt(0), Editor.Screen.Root);
        public bool IsCaptureSupported => IsCurrent && _capture.IsSupported;

        public PhotoSession(MainActivity activity, DrinkEditor editor)
        {
            (_activity, Editor) = (activity, editor);
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(activity._lifetime.Token);
            _cancellation = _lifetime.Token;
            _dispatcher = new VoiceUiDispatcher();
            _capture = new AndroidVoiceCapture(activity, _dispatcher, activity._logger, _cancellation,
                activity.NextVoiceCaptureRequestCode);
            _vision = activity._app.Services.GetRequiredService<IVisionService>();
            _workflow = new PhotoWorkflow(this, _vision, activity._logger);
        }

        public Task RunAsync() => _workflow.RunAsync(_cancellation);
        public Task<VoicePhoto?> CaptureAsync(CancellationToken cancellation) =>
            _capture.CaptureAsync(AndroidCaptureRequest.GeneralPhoto, cancellation);
        public bool HandleResult(int request, Result result) => _capture.HandleResult(request, result);

        public void SetProcessing(bool processing)
        {
            if (_processing is not null && (!processing || !IsCurrent))
            {
                (_processing.Root.Parent as ViewGroup)?.RemoveView(_processing.Root);
                _processing.Dispose();
                _processing = null;
            }
            if (processing && IsCurrent && _processing is null)
            {
                var style = _activity._style;
                var overlay = new FrameLayout(_activity) { Clickable = true, Focusable = true };
                overlay.SetFitsSystemWindows(false);
                var dark = NativeStyle.ModalSurface;
                overlay.SetBackgroundColor(Color.Argb((int)(255 * .72), dark.R, dark.G, dark.B));
                NativeStyle.Identify(overlay, "PhotoProcessingOverlay");
                var card = style.Column();
                card.SetGravity(GravityFlags.Center);
                card.SetPadding(style.Dp(16), style.Dp(16), style.Dp(16), style.Dp(16));
                card.Background = style.Rounded(style.Surface, 12);
                var spinner = new ProgressBar(_activity);
                NativeStyle.Identify(spinner, "PhotoProcessingIndicator");
                card.AddView(spinner, new LinearLayout.LayoutParams(-2, -2));
                var title = style.Label("Reviewing photo", 16, true);
                title.Gravity = GravityFlags.Center;
                card.AddView(title, new LinearLayout.LayoutParams(-2, -2) { TopMargin = style.Dp(8) });
                var detail = style.Label("Finding the best next step…", 14, color: style.Secondary);
                detail.Gravity = GravityFlags.Center;
                card.AddView(detail, new LinearLayout.LayoutParams(-2, -2) { TopMargin = style.Dp(8) });
                overlay.AddView(card, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Center)
                {
                    LeftMargin = style.Dp(24), RightMargin = style.Dp(24),
                    TopMargin = style.Dp(24), BottomMargin = style.Dp(24)
                });
                _processing = new NativeScreen(overlay);
                var window = _activity.Window?.DecorView as ViewGroup
                    ?? throw new InvalidOperationException("Photo processing window is unavailable.");
                window.AddView(overlay, new FrameLayout.LayoutParams(-1, -1));
#pragma warning disable CS0618, CA1422
                overlay.AnnounceForAccessibility("Reviewing photo. Please wait.");
#pragma warning restore CS0618, CA1422
            }
            _activity.UpdateVoiceInputGate();
        }

        public async Task<PhotoIntentChoice> ChooseIntentAsync(CancellationToken cancellation)
        {
            using var popup = new NativePhotoIntentPopup(_activity, _activity._style, () => IsCurrent,
                _activity.UpdateVoiceInputGate, _activity._logger, cancellation);
            _intent = popup;
            try { return await popup.ShowAsync(cancellation); }
            finally
            {
                if (ReferenceEquals(_intent, popup)) _intent = null;
                _activity.UpdateVoiceInputGate();
            }
        }

        public async Task OpenCoffeeAsync(BeanLabelExtraction prefill, CancellationToken cancellation)
        {
            var popup = new NativeAddCoffeePopup(_activity, _activity._style,
                _activity._app.Services.GetRequiredService<IServiceScopeFactory>(), _vision,
                token => _capture.CaptureAsync(AndroidCaptureRequest.LabelPhoto, token), () => IsCaptureSupported,
                ShowCoffeeFeedback, Haptic, () => IsCurrent, _activity.UpdateVoiceInputGate,
                () =>
                {
                    _coffee = null;
                    if (!_workflow.IsActive) _activity.ReleasePhotoSession(this);
                }, _activity._logger, _cancellation);
            _coffee = popup;
            try
            {
                await popup.InitializeAsync(prefill);
                cancellation.ThrowIfCancellationRequested();
                if (!IsCurrent) throw new OperationCanceledException(cancellation);
                popup.OnCreated = bag =>
                {
                    if (!IsCurrent) return;
                    PhotoDrinkSelection.Apply(_activity._newDraft, bag);
                    _activity.UpdateDrink(Editor);
                };
                SetProcessing(false);
                _activity.HideKeyboard();
                await popup.ShowAsync();
            }
            catch { popup.Dispose(); throw; }
        }

        private void ShowCoffeeFeedback(string message, bool error)
        {
            if (!IsCurrent) return;
            // Source FeedbackService returns immediately; its toast runs separately.
            if (error) _activity._feedback.Show(message, isError: true);
            else _ = _activity._feedback.ShowInfoAsync(message);
        }

        private void Haptic()
        {
            if (!IsCurrent) return;
            try { _activity.Window?.DecorView.PerformHapticFeedback(FeedbackConstants.ContextClick); }
            catch (Exception exception) { _activity._logger.LogDebug(exception, "Photo success haptic is unavailable"); }
        }

        public Task OpenProfileAsync(byte[] image) => IsCurrent
            ? _activity.ShowProfileEditorAsync(false, null, image)
            : Task.FromCanceled(new CancellationToken(true));

        public async Task AlertAsync(string title, string message, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!IsCurrent) return;
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var builder = new AlertDialog.Builder(_activity);
            builder.SetTitle(title);
            builder.SetMessage(message);
            builder.SetPositiveButton("OK", (_, _) => { });
            using var dialog = builder.Create() ?? throw new InvalidOperationException("Photo result alert is unavailable.");
            _alert = dialog;
            EventHandler dismissed = (_, _) => completed.TrySetResult();
            dialog.DismissEvent += dismissed;
            using var registration = cancellation.Register(() => completed.TrySetCanceled(cancellation));
            try
            {
                dialog.Show();
                await completed.Task;
            }
            finally
            {
                dialog.DismissEvent -= dismissed;
                dialog.Dismiss();
                if (ReferenceEquals(_alert, dialog)) _alert = null;
            }
        }

        public void RequestBack()
        {
            if (_intent is not null) _intent.Modal.RequestCancel();
            else if (_coffee is not null) _coffee.Modal.RequestCancel();
            // A processing veil has no dismiss affordance in the source.
        }

        public void SetCovered(bool covered)
        {
            _intent?.Modal.SetCovered(covered);
            _coffee?.Modal.SetCovered(covered);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _lifetime.Cancel();
            _workflow.Dispose();
            _capture.Dispose();
            _intent?.Dispose(); _intent = null;
            _coffee?.Dispose(); _coffee = null;
            _alert?.Dismiss();
            SetProcessing(false);
            _lifetime.Dispose();
        }
    }
}
