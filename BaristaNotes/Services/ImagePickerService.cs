using Microsoft.Maui.Media;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Services;

public class ImagePickerService : IImagePickerService
{
    private readonly IMediaPicker _mediaPicker;
    private readonly ILogger<ImagePickerService> _logger;

    public ImagePickerService(IMediaPicker mediaPicker, ILogger<ImagePickerService> logger)
    {
        _mediaPicker = mediaPicker;
        _logger = logger;
    }

    public bool IsPickerSupported => true; // Always supported on iOS/Android

    public async Task<Stream?> PickImageAsync()
    {
        try
        {
            _logger.LogDebug("Starting photo pick");

            var results = await _mediaPicker.PickPhotosAsync(new MediaPickerOptions
            {
                SelectionLimit = 1,
                MaximumWidth = 400,
                MaximumHeight = 400,
                CompressionQuality = 85,
                RotateImage = true,
                PreserveMetaData = false
            });

            _logger.LogDebug("PickPhotosAsync returned, results count: {ResultCount}", results?.Count ?? 0);

            if (results?.Count > 0)
            {
                var fileResult = results.First();
                _logger.LogDebug("Opening stream for file {FileName}", fileResult.FileName);

                var stream = await fileResult.OpenReadAsync();
                _logger.LogDebug("Stream opened, CanRead: {CanRead}, CanSeek: {CanSeek}, Length: {Length}", 
                    stream.CanRead, stream.CanSeek, stream.CanSeek ? stream.Length : -1);

                return stream;
            }

            _logger.LogDebug("No results, user cancelled");
            return null; // User cancelled
        }
        catch (PermissionException ex)
        {
            _logger.LogWarning(ex, "Permission denied");
            throw; // Re-throw permission exceptions for UI handling
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error picking image");
            return null;
        }
    }
}
