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
            var results = await _mediaPicker.PickPhotosAsync(new MediaPickerOptions
            {
                SelectionLimit = 1,
                MaximumWidth = 400,
                MaximumHeight = 400,
                CompressionQuality = 85,
                RotateImage = true,
                PreserveMetaData = false
            });
            
            if (results?.Count > 0)
            {
                return await results.First().OpenReadAsync();
            }
            
            return null; // User cancelled
        }
        catch (PermissionException)
        {
            throw; // Re-throw permission exceptions for UI handling
        }
        catch (Exception ex)
        {
            // Log error and return null
            Console.WriteLine($"Error picking image: {ex.Message}");
            return null;
        }
    }
}
