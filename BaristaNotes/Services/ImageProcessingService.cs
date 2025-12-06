using BaristaNotes.Core.Services;

namespace BaristaNotes.Services;

public class ImageProcessingService : IImageProcessingService
{
    private const int MaxFileSize = 1_048_576; // 1MB
    private const int MaxDimension = 400;
    private const int MinDimension = 100;

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
            Console.WriteLine($"Image validation error: {ex.Message}");
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

        Console.WriteLine($"Image saved to: {path}, size: {memoryStream.Length} bytes");

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
}
