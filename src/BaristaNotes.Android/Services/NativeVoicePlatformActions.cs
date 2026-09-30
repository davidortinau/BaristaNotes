using Android.Content;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp.Services;

internal sealed class NativeVoicePlatformActions(ILogger<NativeVoicePlatformActions> logger) : IVoicePlatformActions, IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private WeakReference<MainActivity>? _activity;
    private VoiceUiDispatcher? _dispatcher;
    private Func<bool>? _isCurrent;
    private IOverlayService? _overlay;
    private AndroidSpeechRecognitionService? _speech;
    private AndroidVoiceCapture? _capture;
    private bool _disposed;
    public IOverlayService Overlay => _overlay ?? throw new InvalidOperationException("Voice platform scope is not attached.");
    public ISpeechRecognitionService Speech => _speech ?? throw new InvalidOperationException("Voice speech scope is not attached.");

    public void Attach(MainActivity activity, VoiceUiDispatcher dispatcher, IOverlayService overlay, Func<bool> isCurrent)
    {
        VoiceUiDispatcher.RequireMainThread();
        if (_activity is not null) throw new InvalidOperationException("A voice platform scope cannot be rebound.");
        _activity = new(activity);
        _dispatcher = dispatcher;
        _overlay = new ScopedVoiceOverlay(overlay, () => IsCurrent);
        _isCurrent = isCurrent;
        _speech = new AndroidSpeechRecognitionService(activity, dispatcher, logger);
        _capture = new AndroidVoiceCapture(activity, dispatcher, logger, _lifetime.Token, activity.NextVoiceCaptureRequestCode);
        activity.AttachVoiceCapture(_capture);
    }

    private bool IsCurrent => !_disposed && !_lifetime.IsCancellationRequested && _isCurrent?.Invoke() == true;
    public void Navigate(VoiceNavigationRequest request)
    {
        var dispatcher = _dispatcher ?? throw new InvalidOperationException("Voice platform scope is not attached.");
        dispatcher.Post(async () =>
        {
            if (!IsCurrent || _activity is null || !_activity.TryGetTarget(out var activity)) return;
            try { await activity.NavigateFromVoiceAsync(VoiceRoutePlan.From(request), () => IsCurrent); }
            catch (Exception exception) { logger.LogError(exception, "Queued native voice navigation failed for {Route}", request.Route); }
        });
    }
    public bool IsCaptureSupported => IsCurrent && _capture?.IsSupported == true;
    public Task<VoicePhoto?> CapturePhotoAsync(VoiceCaptureOptions options)
    {
        var dispatcher = _dispatcher ?? throw new InvalidOperationException("Voice platform scope is not attached.");
        return dispatcher.InvokeAsync(() =>
        {
            if (!IsCurrent || _capture is null) throw new OperationCanceledException("The voice session has ended.");
            return _capture.CaptureAsync(options);
        });
    }
    public async Task OpenBrowserAsync(Uri uri)
    {
        var dispatcher = _dispatcher ?? throw new InvalidOperationException("Voice platform scope is not attached.");
        await dispatcher.InvokeAsync(() =>
        {
            if (!IsCurrent || _activity is null || !_activity.TryGetTarget(out var activity))
                throw new OperationCanceledException("The voice session has ended.");
            if (!uri.IsAbsoluteUri || uri.Scheme is not ("https" or "http"))
                throw new ArgumentException("Only web URLs can be opened by voice.", nameof(uri));
            using var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(uri.AbsoluteUri));
            intent.AddCategory(Intent.CategoryBrowsable!);
            activity.StartActivity(intent);
            return Task.FromResult(true);
        });
    }
    public void EndOwner()
    {
        if (_disposed || _lifetime.IsCancellationRequested) return;
        _lifetime.Cancel();
        (_overlay as IDisposable)?.Dispose();
        _speech?.Dispose();
        _capture?.Dispose();
    }
    public void Dispose()
    {
        if (_disposed) return;
        EndOwner();
        _disposed = true;
        if (_capture is not null && _activity?.TryGetTarget(out var activity) == true)
            activity.DetachVoiceCapture(_capture);
        _overlay = null;
        _isCurrent = null;
        _activity = null;
        _dispatcher = null;
        _lifetime.Dispose();
    }
}
