using BaristaNotes.Core.Services;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class NativeImageProcessingService(string directory, ILogger<NativeImageProcessingService> logger)
    : IImageProcessingService
{
    private readonly ImageFileStore _files = new(directory, logger);

    public Task<ImageValidationResult> ValidateImageAsync(Stream imageStream) => _files.ValidateAsync(imageStream);
    public Task<string> SaveImageAsync(Stream imageStream, string filename) => _files.SaveAsync(imageStream, filename);
    public Task<bool> DeleteImageAsync(string filename) => _files.DeleteAsync(filename);
    public string GetImagePath(string filename) => _files.Resolve(filename);
    public bool ImageExists(string filename) => _files.Exists(filename);

    public async Task<MemoryStream?> DownsampleAsync(Stream imageStream, int maxDimension, int quality)
    {
        if (maxDimension < 1) throw new ArgumentOutOfRangeException(nameof(maxDimension));
        if (quality < 1 || quality > 100) throw new ArgumentOutOfRangeException(nameof(quality));
        using var buffer = new MemoryStream();
        if (imageStream.CanSeek) imageStream.Position = 0;
        await imageStream.CopyToAsync(buffer);
        var native = TryDownsampleWithUIKit(buffer.ToArray(), maxDimension, quality);
        if (native != null) return native;

        // The pinned adapter tries the non-UI Graphics decoder after UIKit, then
        // lets UserProfileService retain its original-byte fallback policy.
        try
        {
            buffer.Position = 0;
            using var image = Microsoft.Maui.Graphics.Platform.PlatformImage.FromStream(buffer);
            if (image == null)
            {
                logger.LogWarning("PlatformImage could not decode the profile image");
                return null;
            }
            var scaled = image.Width > maxDimension || image.Height > maxDimension
                ? image.Downsize(maxDimension, true) : image;
            try
            {
                var output = new MemoryStream();
                try
                {
                    await scaled.SaveAsync(output, Microsoft.Maui.Graphics.ImageFormat.Jpeg, quality / 100f);
                    output.Position = 0;
                    return output;
                }
                catch { output.Dispose(); throw; }
            }
            finally { if (!ReferenceEquals(scaled, image)) scaled.Dispose(); }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "PlatformImage downsample failed; caller retains original-byte fallback");
            return null;
        }
    }

    private MemoryStream? TryDownsampleWithUIKit(byte[] bytes, int maxDimension, int quality)
    {
        try
        {
            using var data = NSData.FromArray(bytes);
            using var source = UIImage.LoadFromData(data);
            if (source == null)
            {
                logger.LogWarning("UIKit could not decode image; caller retains source fallback semantics");
                return null;
            }
            UIImage? scaled = null;
            try
            {
                var width = (double)source.Size.Width;
                var height = (double)source.Size.Height;
                if (width > maxDimension || height > maxDimension)
                {
                    var scale = Math.Min(maxDimension / width, maxDimension / height);
                    var size = new CGSize(width * scale, height * scale);
                    using var format = new UIGraphicsImageRendererFormat { Scale = 1, Opaque = true };
                    using var renderer = new UIGraphicsImageRenderer(size, format);
                    scaled = renderer.CreateImage(_ => source.Draw(new CGRect(CGPoint.Empty, size)));
                }
                using var jpeg = (scaled ?? source).AsJPEG((nfloat)(quality / 100.0));
                if (jpeg == null)
                {
                    logger.LogWarning("UIKit could not encode JPEG; returning decode failure");
                    return null;
                }
                var output = new MemoryStream(jpeg.ToArray());
                logger.LogInformation("Downsampled profile image with UIKit to {Bytes} bytes at quality {Quality}", output.Length, quality);
                return output;
            }
            finally { scaled?.Dispose(); }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Native downsampling failed; caller may fall back to original bytes");
            return null;
        }
    }
}
