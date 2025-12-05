namespace BaristaNotes.Core.Services;

public interface IImagePickerService
{
    /// <summary>
    /// Opens native photo picker and returns selected image as stream.
    /// </summary>
    /// <returns>
    /// Image stream if user selected photo, null if cancelled or error.
    /// </returns>
    /// <exception cref="PermissionException">
    /// Thrown when photo library permission denied.
    /// </exception>
    Task<Stream?> PickImageAsync();
    
    /// <summary>
    /// Checks if photo picker is available on current platform.
    /// </summary>
    bool IsPickerSupported { get; }
}
