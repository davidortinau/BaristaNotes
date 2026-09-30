using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Provider;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.Logging;
using AndroidUri = Android.Net.Uri;

namespace BaristaNotes.AndroidApp.Services;

internal sealed class AndroidVoiceCapture(Activity activity, VoiceUiDispatcher dispatcher,
    ILogger logger, CancellationToken owner, Func<int> nextRequestCode) : IDisposable
{
    private sealed class Pending(string raw, string processed, AndroidUri uri, AndroidCaptureRequest options,
        int requestCode, CancellationTokenSource lifetime)
    {
        public string Raw { get; } = raw;
        public string Processed { get; } = processed;
        public AndroidUri Uri { get; } = uri;
        public AndroidCaptureRequest Options { get; } = options;
        public CancellationTokenSource Lifetime { get; } = lifetime;
        public CancellationToken Cancellation { get; } = lifetime.Token;
        public int RequestCode { get; } = requestCode;
        public TaskCompletionSource<VoicePhoto?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationTokenRegistration Registration { get; set; }
        public bool Reading { get; set; }
        public bool Cleaned { get; set; }
    }
    private Pending? _pending;
    private bool _disposed;
    public bool IsSupported
    {
        get
        {
            if (_disposed || owner.IsCancellationRequested
                || activity.PackageManager?.HasSystemFeature(PackageManager.FeatureCameraAny!) != true) return false;
            using var intent = new Intent(MediaStore.ActionImageCapture);
#pragma warning disable CS0618, CA1422
            return activity.PackageManager.ResolveActivity(intent, PackageInfoFlags.MatchDefaultOnly) is not null;
#pragma warning restore CS0618, CA1422
        }
    }

    public Task<VoicePhoto?> CaptureAsync(VoiceCaptureOptions options) =>
        CaptureAsync(AndroidCaptureRequest.FromVoice(options), owner);

    public Task<VoicePhoto?> CaptureAsync(AndroidCaptureRequest options, CancellationToken cancellation)
    {
        VoiceUiDispatcher.RequireMainThread();
        owner.ThrowIfCancellationRequested();
        cancellation.ThrowIfCancellationRequested();
        if (_disposed || !IsSupported) throw new NotSupportedException("Camera is not available on this device.");
        if (_pending is not null) throw new InvalidOperationException("A camera capture is already active.");
        if (options.Processing is { } processing) AndroidCaptureRequest.FromVoice(processing);
        var requestCode = nextRequestCode();
        var cache = activity.CacheDir?.AbsolutePath ?? throw new IOException("App capture cache is unavailable.");
        var directory = Path.Combine(cache, "voice-capture");
        Directory.CreateDirectory(directory);
        var name = Guid.NewGuid().ToString("N");
        var raw = Path.Combine(directory, name + ".jpg");
        var processed = Path.Combine(directory, name + "-processed.jpg");
        using (File.Open(raw, FileMode.CreateNew, FileAccess.Write)) { }
        Pending? pending = null;
        try
        {
            using var file = new Java.IO.File(raw);
            var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(activity, activity.PackageName + ".voice.capture", file)
                ?? throw new IOException("The temporary capture URI could not be created.");
            pending = new Pending(raw, processed, uri, options, requestCode,
                CancellationTokenSource.CreateLinkedTokenSource(owner, cancellation));
            _pending = pending;
            pending.Registration = pending.Cancellation.Register(() => dispatcher.Post(() => Cancel(pending)));
            using var intent = new Intent(MediaStore.ActionImageCapture);
            intent.PutExtra(MediaStore.ExtraOutput, uri);
            intent.PutExtra(Intent.ExtraTitle, options.Title);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
            intent.ClipData = ClipData.NewRawUri("Photo capture", uri);
#pragma warning disable CS0618, CA1422
            pending.Cancellation.ThrowIfCancellationRequested();
            activity.StartActivityForResult(intent, pending.RequestCode);
#pragma warning restore CS0618, CA1422
            return pending.Completion.Task;
        }
        catch
        {
            if (pending is not null) Cleanup(pending);
            else DeleteOwned(raw);
            throw;
        }
    }

    public bool HandleResult(int requestCode, Result result)
    {
        var pending = _pending;
        if (pending is null || requestCode != pending.RequestCode) return false;
        if (pending.Reading) { logger.LogDebug("Ignored duplicate camera result"); return true; }
        if (result != Result.Ok)
        {
            pending.Completion.TrySetResult(null);
            Cleanup(pending);
            return true;
        }
        pending.Reading = true;
        _ = ReadAsync(pending);
        return true;
    }

    private async Task ReadAsync(Pending pending)
    {
        try
        {
            if (!File.Exists(pending.Raw) || new FileInfo(pending.Raw).Length == 0)
                throw new IOException("The camera did not return a photo.");
            // Title-only label capture keeps MediaPicker's no-processing defaults.
            var bytes = pending.Options.Processing is { } processing
                ? await VoiceCaptureProcessor.ReadAsync(pending.Raw, pending.Processed, processing, logger, pending.Cancellation)
                : await File.ReadAllBytesAsync(pending.Raw, pending.Cancellation);
            pending.Cancellation.ThrowIfCancellationRequested();
            pending.Completion.TrySetResult(new VoicePhoto(Path.GetFileName(pending.Raw),
                () => Task.FromResult<Stream>(new MemoryStream(bytes, writable: false))));
        }
        catch (OperationCanceledException) { pending.Completion.TrySetCanceled(pending.Cancellation); }
        catch (Exception exception)
        {
            logger.LogError(exception, "Reading camera capture failed");
            pending.Completion.TrySetException(exception);
        }
        finally { Cleanup(pending); }
    }

    private void Cancel(Pending pending)
    {
        if (!ReferenceEquals(_pending, pending) || pending.Reading) return;
        pending.Completion.TrySetCanceled(pending.Cancellation);
        Cleanup(pending);
    }
    private void Cleanup(Pending pending)
    {
        if (pending.Cleaned) return;
        pending.Cleaned = true;
        if (ReferenceEquals(_pending, pending)) _pending = null;
        pending.Registration.Dispose();
        pending.Lifetime.Dispose();
        try { activity.RevokeUriPermission(pending.Uri, ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission); }
        catch (Exception exception) { logger.LogWarning(exception, "Revoking temporary voice capture URI access failed"); }
        pending.Uri.Dispose();
        DeleteOwned(pending.Raw);
        DeleteOwned(pending.Processed);
    }
    private void DeleteOwned(string path)
    {
        try { File.Delete(path); }
        catch (Exception exception) { logger.LogWarning(exception, "Removing this capture's own temporary file failed"); }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_pending is { } pending)
        {
            pending.Lifetime.Cancel();
            pending.Completion.TrySetCanceled(pending.Cancellation);
            if (!pending.Reading) Cleanup(pending);
        }
    }
}
