using BaristaNotes.Core.Services;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;

namespace BaristaNotes.Services;

public class ImageProcessingService : IImageProcessingService
{
    private readonly ILogger<ImageProcessingService> _logger;
    
    private const int MaxFileSize = 1_048_576; // 1MB
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

        // Buffer to memory so PlatformImage.FromStream can do random access (and so we
        // can fall back to the original if decoding fails).
        using var source = new MemoryStream();
        if (imageStream.CanSeek) imageStream.Position = 0;
        await imageStream.CopyToAsync(source);
        source.Position = 0;

        try
        {
            using var image = PlatformImage.FromStream(source);
            if (image is null)
            {
                _logger.LogWarning("PlatformImage.FromStream returned null; cannot downsample");
                return null;
            }

            var w = image.Width;
            var h = image.Height;
            _logger.LogDebug("Source image: {Width}x{Height} ({Bytes} bytes)", w, h, source.Length);

            // Only downsize if larger than target on either axis.
            Microsoft.Maui.Graphics.IImage scaled;
            if (w > maxDimension || h > maxDimension)
            {
                scaled = image.Downsize(maxDimension, true);
            }
            else
            {
                scaled = image;
            }

            var output = new MemoryStream();
            await scaled.SaveAsync(output, ImageFormat.Jpeg, quality / 100f);
            output.Position = 0;
            _logger.LogDebug(
                "Downsampled image: {Width}x{Height} → {Bytes} bytes (q={Quality})",
                scaled.Width, scaled.Height, output.Length, quality);

            return output;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Image downsample failed; caller should fall back to original");
            return null;
        }
    }
}
