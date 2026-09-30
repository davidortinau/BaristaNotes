using BaristaNotes.Core.Services;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.Logging;
using PhotosUI;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class NativePhotoLibraryPicker(ILogger logger, IImageProcessingService processing)
{
    public async Task<MemoryStream?> PickAsync(UIViewController presenter, CancellationToken cancellationToken)
    {
        if (!NSThread.IsMain) throw new InvalidOperationException("Present the photo library on the UI thread.");
        if (presenter.PresentedViewController != null)
            throw new InvalidOperationException("Another modal is already presented.");
        using var configuration = new PHPickerConfiguration
        {
            SelectionLimit = 1, Filter = PHPickerFilter.ImagesFilter
        };
        using var picker = new PHPickerViewController(configuration);
        var completion = new TaskCompletionSource<PHPickerResult[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var selection = new SelectionDelegate(completion);
        using var dismissal = new DismissalDelegate(completion);
        picker.Delegate = selection;
        if (picker.PresentationController != null) picker.PresentationController.Delegate = dismissal;
        PHPickerResult[] results = [];
        try
        {
            await presenter.PresentViewControllerAsync(picker, true);
            if (picker.PresentationController != null) picker.PresentationController.Delegate = dismissal;
            using var cancelled = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            results = await completion.Task;
            if (results.Length == 0)
            {
                logger.LogDebug("Profile photo library selection cancelled");
                return null;
            }
            var provider = results[0].ItemProvider;
            var identifiers = provider.RegisteredTypeIdentifiers;
            var identifier = identifiers.Any(id => id.StartsWith("com.apple.live-photo", StringComparison.Ordinal))
                && identifiers.Contains("public.jpeg") ? "public.jpeg" : identifiers.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(identifier))
                throw new InvalidOperationException("The selected photo did not provide a data representation.");
            var pending = provider.LoadDataRepresentationAsync(identifier, out var progress);
            byte[] bytes;
            using (progress)
            {
                try
                {
                    using var data = await pending.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
                    if (data == null) throw new InvalidOperationException("The photo provider returned no data.");
                    bytes = data.ToArray();
                }
                catch
                {
                    progress.Cancel();
                    _ = DisposeLateDataAsync(pending);
                    throw;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            var oriented = NormalizeOrientation(bytes);
            using var raw = new MemoryStream(oriented, writable: false);
            // Match the source pick options before the shared service's own
            // authoritative 400/85 processing and 12MB validation.
            var prepared = await processing.DownsampleAsync(raw, 400, 85);
            if (cancellationToken.IsCancellationRequested)
            {
                prepared?.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
            }
            return prepared ?? new MemoryStream(oriented, writable: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Profile photo operation cancelled after its form was removed");
            return null;
        }
        catch (Exception error)
        {
            // ImagePickerService logs non-permission failures and returns no selection.
            logger.LogError(error, "Profile photo library failed");
            return null;
        }
        finally
        {
            if (picker.PresentingViewController != null)
                await picker.DismissViewControllerAsync(true);
            picker.WeakDelegate = null;
            if (picker.PresentationController != null) picker.PresentationController.WeakDelegate = null;
            foreach (var result in results) result.Dispose();
        }
    }

    private async Task DisposeLateDataAsync(Task<NSData> pending)
    {
        try { (await pending)?.Dispose(); }
        catch (Exception error) { logger.LogDebug(error, "Cancelled photo provider finished without usable data"); }
    }

    private byte[] NormalizeOrientation(byte[] bytes)
    {
        try
        {
            using var data = NSData.FromArray(bytes);
            using var image = UIImage.LoadFromData(data);
            if (image == null || image.Orientation == UIImageOrientation.Up) return bytes;
            using var format = new UIGraphicsImageRendererFormat { Scale = 1, Opaque = true };
            using var renderer = new UIGraphicsImageRenderer(image.Size, format);
            using var upright = renderer.CreateImage(_ => image.Draw(new CGRect(CGPoint.Empty, image.Size)));
            using var jpeg = upright.AsJPEG(.85f);
            return jpeg?.ToArray() ?? bytes;
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "Picker orientation normalization failed; retaining original data as the source does");
            return bytes;
        }
    }

    private sealed class SelectionDelegate(TaskCompletionSource<PHPickerResult[]> completion) : PHPickerViewControllerDelegate
    {
        public override void DidFinishPicking(PHPickerViewController picker, PHPickerResult[] results)
        {
            // Disable the interactive-dismiss callback before processing a selection.
            // Otherwise it can race the successful data-provider operation.
            if (picker.PresentationController?.Delegate is DismissalDelegate dismissal) dismissal.SelectionFinished = true;
            picker.DismissViewController(true, () => completion.TrySetResult(results));
        }
    }

    private sealed class DismissalDelegate(TaskCompletionSource<PHPickerResult[]> completion) : UIAdaptivePresentationControllerDelegate
    {
        public bool SelectionFinished { get; set; }
        public override void DidDismiss(UIPresentationController presentationController)
        {
            if (!SelectionFinished) completion.TrySetResult([]);
        }
    }
}
