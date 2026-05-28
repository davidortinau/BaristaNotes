using BaristaNotes.Core.Services;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;

namespace BaristaNotes.Services;

public class ImageProcessingService : IImageProcessingService
{
    private readonly ILogger<ImageProcessingService> _logger;

    // 12MB cap: we downsample before validation now, so this is just a hard
    // ceiling against truly absurd inputs (raw RAW files etc.). The previous
    // 1MB cap was rejecting full-resolution HEIC/JPEG photos from the iOS
    // photo library when DownsampleAsync hadn't been wired in.
    private const int MaxFileSize = 12 * 1024 * 1024;
    private const int MaxDimension = 400;
    private const int MinDimension = 100;

    public ImageProcessingService(ILogger<ImageProcessingService> logger)
    {
        _logger = logger;
    }

    public async Task<ImageValidationResult> ValidateImageAsync(Stream imageStream)
    {
        try
        {
            // Copy to memory stream for multiple reads
            using var ms = new MemoryStream();
            await imageStream.CopyToAsync(ms);
            ms.Position = 0;

            // Check file size
            if (ms.Length > MaxFileSize)
            {
                return ImageValidationResult.TooLarge;
            }

            // Note: For full validation, we would load image to check dimensions
            // using Microsoft.Maui.Graphics.Platform.PlatformImage.FromStream(ms)
            // For now, we'll rely on MediaPicker's built-in resizing

            // Reset stream position
            imageStream.Position = 0;

            return ImageValidationResult.Valid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image validation error");
            return ImageValidationResult.ProcessingFailed;
        }
    }

    public async Task<string> SaveImageAsync(Stream imageStream, string filename)
    {
        var path = GetImagePath(filename);

        // Copy stream to memory first to ensure we have all the data
        // This helps with streams that may not support seeking
        using var memoryStream = new MemoryStream();

        // Reset position if possible
        if (imageStream.CanSeek)
        {
            imageStream.Position = 0;
        }

        await imageStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        // Write to file
        using var fileStream = File.Create(path);
        await memoryStream.CopyToAsync(fileStream);

        _logger.LogDebug("Image saved to: {Path}, size: {SizeBytes} bytes", path, memoryStream.Length);

        return path;
    }

    public async Task<bool> DeleteImageAsync(string filename)
    {
        await Task.CompletedTask; // Make async consistent

        var path = GetImagePath(filename);

        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    public string GetImagePath(string filename)
    {
        return Path.Combine(FileSystem.AppDataDirectory, filename);
    }

    public bool ImageExists(string filename)
    {
        return File.Exists(GetImagePath(filename));
    }

    public async Task<MemoryStream?> DownsampleAsync(Stream imageStream, int maxDimension, int quality)
    {
        if (maxDimension < 1) throw new ArgumentOutOfRangeException(nameof(maxDimension));
        if (quality < 1 || quality > 100) throw new ArgumentOutOfRangeException(nameof(quality));

        // Buffer to memory so decoders can do random access and so we can
        // try multiple decode strategies on the same bytes.
        using var source = new MemoryStream();
        if (imageStream.CanSeek) imageStream.Position = 0;
        await imageStream.CopyToAsync(source);
        var sourceBytes = source.ToArray();
        _logger.LogInformation("Downsample: source bytes = {Bytes}", sourceBytes.Length);

#if IOS || MACCATALYST
        // Try iOS-native UIKit path first — UIImage handles HEIC/HEIF, ProRAW,
        // and other formats from the iOS photo library reliably. The
        // Microsoft.Maui.Graphics PlatformImage.FromStream path has been
        // observed to silently fail on some iOS images (returns null or
        // throws inside SaveAsync), which then dropped us through to the
        // 1MB-cap ValidateImage gate and looked like a UI bug.
        var iosResult = TryDownsampleWithUIKit(sourceBytes, maxDimension, quality);
        if (iosResult != null)
            return iosResult;
        _logger.LogWarning("UIKit downsample returned null; falling back to PlatformImage");
#endif

        try
        {
            source.Position = 0;
            using var image = PlatformImage.FromStream(source);
            if (image is null)
            {
                _logger.LogWarning("PlatformImage.FromStream returned null; cannot downsample");
                return null;
            }

            var w = image.Width;
            var h = image.Height;
            _logger.LogDebug("Source image (PlatformImage): {Width}x{Height} ({Bytes} bytes)", w, h, sourceBytes.Length);

            Microsoft.Maui.Graphics.IImage scaled = (w > maxDimension || h > maxDimension)
                ? image.Downsize(maxDimension, true)
                : image;

            var output = new MemoryStream();
            await scaled.SaveAsync(output, ImageFormat.Jpeg, quality / 100f);
            output.Position = 0;
            _logger.LogInformation(
                "Downsampled via PlatformImage: {Width}x{Height} → {Bytes} bytes (q={Quality})",
                scaled.Width, scaled.Height, output.Length, quality);
            return output;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PlatformImage downsample failed; caller will fall back to original");
            return null;
        }
    }

#if IOS || MACCATALYST
    private MemoryStream? TryDownsampleWithUIKit(byte[] bytes, int maxDimension, int quality)
    {
        try
        {
            using var data = Foundation.NSData.FromArray(bytes);
            using var srcImage = UIKit.UIImage.LoadFromData(data);
            if (srcImage is null)
            {
                _logger.LogWarning("UIImage.LoadFromData returned null ({Bytes} bytes)", bytes.Length);
                return null;
            }

            var w = (double)srcImage.Size.Width;
            var h = (double)srcImage.Size.Height;
            _logger.LogDebug("UIKit source: {Width}x{Height}", w, h);

            UIKit.UIImage? scaled = null;
            try
            {
                if (w > maxDimension || h > maxDimension)
                {
                    var scale = Math.Min(maxDimension / w, maxDimension / h);
                    var newSize = new CoreGraphics.CGSize(w * scale, h * scale);
                    var format = new UIKit.UIGraphicsImageRendererFormat
                    {
                        Scale = 1, // pixel-for-pixel; we already computed the target size
                        Opaque = true,
                    };
                    using var renderer = new UIKit.UIGraphicsImageRenderer(newSize, format);
                    scaled = renderer.CreateImage(ctx =>
                    {
                        srcImage.Draw(new CoreGraphics.CGRect(CoreGraphics.CGPoint.Empty, newSize));
                    });
                }
                else
                {
                    scaled = srcImage;
                }

                using var jpeg = scaled.AsJPEG((nfloat)(quality / 100.0));
                if (jpeg is null)
                {
                    _logger.LogWarning("UIImage.AsJPEG returned null");
                    return null;
                }

                var output = new MemoryStream((int)jpeg.Length);
                output.Write(jpeg.ToArray(), 0, (int)jpeg.Length);
                output.Position = 0;

                _logger.LogInformation(
                    "Downsampled via UIKit: {SourceW}x{SourceH} → {Bytes} bytes (q={Quality})",
                    (int)w, (int)h, output.Length, quality);

                return output;
            }
            finally
            {
                if (scaled != null && !ReferenceEquals(scaled, srcImage))
                    scaled.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UIKit downsample threw");
            return null;
        }
    }
#endif
}
