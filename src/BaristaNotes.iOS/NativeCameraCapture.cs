using BaristaNotes.Core.Services;
using AVFoundation;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class NativeCameraCapture(ILogger<NativeCameraCapture> logger) : INativePhotoCapture
{
    internal static bool IsSupported => ObjCRuntime.Runtime.Arch != ObjCRuntime.Arch.SIMULATOR
        && UIImagePickerController.IsSourceTypeAvailable(UIImagePickerControllerSourceType.Camera);
    private UIImagePickerController? _picker;
    private CameraDelegate? _delegate;
    private TaskCompletionSource<VoicePhoto?>? _completion;
    private NativePhotoRequest? _options;
    private CancellationTokenRegistration _cancellation;
    private CancellationToken _token;
    private TaskCompletionSource? _dismissed;
    private bool _ending;
    public bool CaptureSupported => IsSupported;
    public Task<VoicePhoto?> CaptureAsync(UIViewController presenter, VoiceCaptureOptions options) =>
        CaptureAsync(presenter, new NativePhotoRequest(options.Title, options.MaximumWidth, options.MaximumHeight, options.CompressionQuality));

    public Task<VoicePhoto?> CaptureAsync(UIViewController presenter, NativePhotoRequest options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported)
            throw new NotSupportedException("Camera capture is not supported on this device.");
        if (_picker != null || presenter.PresentedViewController != null)
            throw new InvalidOperationException("Another modal is already open.");
        if (options.MaximumWidth is <= 0 || options.MaximumHeight is <= 0 || options.CompressionQuality is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(options));
        if (AVCaptureDevice.GetAuthorizationStatus(AVAuthorizationMediaType.Video) is AVAuthorizationStatus.Denied or AVAuthorizationStatus.Restricted)
            throw new NativeCameraPermissionException();
        _options = options;
        _token = cancellationToken;
        _ending = false;
        _completion = new TaskCompletionSource<VoicePhoto?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _delegate = new CameraDelegate(this);
        _picker = new UIImagePickerController
        {
            SourceType = UIImagePickerControllerSourceType.Camera,
            CameraCaptureMode = UIImagePickerControllerCameraCaptureMode.Photo,
            MediaTypes = ["public.image"],
            AllowsEditing = false,
            Title = options.Title,
            Delegate = _delegate
        };
        try
        {
            var picker = _picker;
            var completion = _completion;
            _cancellation = cancellationToken.Register(() => NativeUiThread.Send(() =>
            {
                if (!ReferenceEquals(_picker, picker)) return;
                if (_ending)
                {
                    picker.DismissViewController(false, null);
                    _dismissed?.TrySetResult();
                }
                else _ = CompleteAsync(null, null, true);
            }));
            if (!cancellationToken.IsCancellationRequested) presenter.PresentViewController(_picker, true, null);
            return completion.Task;
        }
        catch
        {
            _picker.Delegate = null;
            _picker.Dispose();
            _delegate.Dispose();
            _picker = null;
            _delegate = null;
            _completion = null;
            _options = null;
            _cancellation.Dispose();
            throw;
        }
    }
    private async Task CompleteAsync(UIImage? original, NSUrl? originalUrl, bool cancelled)
    {
        var completion = _completion;
        var picker = _picker;
        if (completion == null || picker == null || _ending) return;
        _ending = true;
        string? path = null;
        try
        {
            if (!cancelled)
            {
                var options = _options ?? throw new InvalidOperationException("Capture options were lost.");
                var cache = NSFileManager.DefaultManager.GetUrls(NSSearchPathDirectory.CachesDirectory, NSSearchPathDomain.User)[0].Path
                    ?? throw new IOException("No app cache directory.");
                Directory.CreateDirectory(cache);
                if (!options.HasTransform && originalUrl?.IsFileUrl == true && originalUrl.Path is { } rawPath)
                {
                    // Title-only Scan preserves available original file bytes.
                    path = Path.Combine(cache, "photo-" + Guid.NewGuid().ToString("N") + Path.GetExtension(rawPath));
                    File.Copy(rawPath, path, overwrite: false);
                }
                else
                {
                    if (original == null) throw new InvalidOperationException("The camera did not return an image.");
                    var size = original.Size;
                    if (size.Width <= 0 || size.Height <= 0) throw new InvalidOperationException("The camera image has no dimensions.");
                    var scale = (nfloat)Math.Min(1, Math.Min(options.MaximumWidth is int w ? w / size.Width : nfloat.MaxValue,
                        options.MaximumHeight is int h ? h / size.Height : nfloat.MaxValue));
                    var target = new CGSize(Math.Max(1, Math.Round(size.Width * scale)), Math.Max(1, Math.Round(size.Height * scale)));
                    using var format = new UIGraphicsImageRendererFormat { Scale = 1, Opaque = options.HasTransform };
                    using var renderer = new UIGraphicsImageRenderer(target, format);
                    using var normalized = renderer.CreateImage(_ => original.Draw(new CGRect(CGPoint.Empty, target)));
                    var png = !options.HasTransform;
                    using var data = (png ? normalized.AsPNG() : normalized.AsJPEG(options.CompressionQuality / 100f))
                        ?? throw new InvalidOperationException("Could not encode the camera image.");
                    path = Path.Combine(cache, "photo-" + Guid.NewGuid().ToString("N") + (png ? ".png" : ".jpg"));
                    using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    using var input = data.AsStream();
                    input.CopyTo(output);
                }
            }
            var dismissed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _dismissed = dismissed;
            if (picker.PresentingViewController == null) dismissed.TrySetResult();
            else picker.DismissViewController(true, () => dismissed.TrySetResult());
            if (_token.IsCancellationRequested) dismissed.TrySetResult();
            await dismissed.Task;
            if (_token.IsCancellationRequested)
            {
                if (path != null) File.Delete(path);
                completion.TrySetCanceled(_token);
            }
            else if (path == null) completion.TrySetResult(null);
            else
            {
                var savedPath = path;
                // The voice consumers each open once. Their using/await using
                // owns the stream and removes only this new temporary capture.
                completion.TrySetResult(new VoicePhoto(Path.GetFileName(savedPath), () =>
                    Task.FromResult<Stream>(new FileStream(savedPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                        4096, FileOptions.DeleteOnClose))));
            }
        }
        catch (Exception error)
        {
            logger.LogError(error, "Native camera capture failed");
            try { if (path != null && File.Exists(path)) File.Delete(path); }
            catch (Exception cleanupError) { logger.LogWarning(cleanupError, "Could not remove failed temporary capture"); }
            completion.TrySetException(error);
            picker.DismissViewController(false, null);
        }
        finally
        {
            picker.Delegate = null;
            picker.Dispose();
            _delegate?.Dispose();
            _delegate = null;
            _picker = null;
            _options = null;
            _completion = null;
            _dismissed = null;
            _cancellation.Dispose();
            _cancellation = default;
            _token = default;
            _ending = false;
        }
    }
    private sealed class CameraDelegate(NativeCameraCapture owner) : UIImagePickerControllerDelegate
    {
        private readonly WeakReference<NativeCameraCapture> _owner = new(owner);
        public override void Canceled(UIImagePickerController picker)
        {
            if (_owner.TryGetTarget(out var target)) _ = target.CompleteAsync(null, null, true);
        }
        public override void FinishedPickingMedia(UIImagePickerController picker, NSDictionary info)
        {
            if (_owner.TryGetTarget(out var target))
                _ = target.CompleteAsync(info[UIImagePickerController.OriginalImage] as UIImage,
                    info[UIImagePickerController.ImageUrl] as NSUrl ?? info[UIImagePickerController.MediaURL] as NSUrl, false);
        }
    }
}
