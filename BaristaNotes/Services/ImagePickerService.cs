using Microsoft.Maui.Media;
using BaristaNotes.Core.Services;

namespace BaristaNotes.Services;

public class ImagePickerService : IImagePickerService
{
    private readonly IMediaPicker _mediaPicker;

    public ImagePickerService(IMediaPicker mediaPicker)
    {
        _mediaPicker = mediaPicker;
    }

    public bool IsPickerSupported => true; // Always supported on iOS/Android

    public async Task<Stream?> PickImageAsync()
    {
        try
        {
            Console.WriteLine("ImagePickerService: Starting photo pick...");

            var results = await _mediaPicker.PickPhotosAsync(new MediaPickerOptions
            {
                SelectionLimit = 1,
                MaximumWidth = 400,
                MaximumHeight = 400,
                CompressionQuality = 85,
                RotateImage = true,
                PreserveMetaData = false
            });

            Console.WriteLine($"ImagePickerService: PickPhotosAsync returned, results count: {results?.Count ?? 0}");

            if (results?.Count > 0)
            {
                var fileResult = results.First();
                Console.WriteLine($"ImagePickerService: Opening stream for {fileResult.FileName}");

                var stream = await fileResult.OpenReadAsync();
                Console.WriteLine($"ImagePickerService: Stream opened, CanRead: {stream.CanRead}, CanSeek: {stream.CanSeek}, Length: {(stream.CanSeek ? stream.Length : -1)}");

                return stream;
            }

            Console.WriteLine("ImagePickerService: No results, user cancelled");
            return null; // User cancelled
        }
        catch (PermissionException ex)
        {
            Console.WriteLine($"ImagePickerService: Permission denied - {ex.Message}");
            throw; // Re-throw permission exceptions for UI handling
        }
        catch (Exception ex)
        {
            // Log error and return null
            Console.WriteLine($"ImagePickerService: Error picking image - {ex.Message}");
            Console.WriteLine($"ImagePickerService: Stack trace - {ex.StackTrace}");
            return null;
        }
    }
}
