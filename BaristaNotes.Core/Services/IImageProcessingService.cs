namespace BaristaNotes.Core.Services;

public interface IImageProcessingService
{
    /// <summary>
    /// Validates image dimensions and file size.
    /// </summary>
    /// <param name="imageStream">Image stream to validate (will be read and reset).</param>
    /// <returns>Validation result indicating success or specific failure.</returns>
    Task<ImageValidationResult> ValidateImageAsync(Stream imageStream);
    
    /// <summary>
    /// Saves image stream to app data directory with specified filename.
    /// </summary>
    /// <param name="imageStream">Image stream to save.</param>
    /// <param name="filename">Target filename (e.g., "profile_avatar_123.jpg").</param>
    /// <returns>Full path to saved file.</returns>
    /// <exception cref="IOException">Failed to save file.</exception>
    Task<string> SaveImageAsync(Stream imageStream, string filename);
    
    /// <summary>
    /// Deletes image file from app data directory.
    /// </summary>
    /// <param name="filename">Filename to delete (not full path).</param>
    /// <returns>True if deleted, false if file doesn't exist.</returns>
    Task<bool> DeleteImageAsync(string filename);
    
    /// <summary>
    /// Constructs full path to image file in app data directory.
    /// </summary>
    /// <param name="filename">Filename (e.g., "profile_avatar_123.jpg").</param>
    /// <returns>Full path to file.</returns>
    string GetImagePath(string filename);
    
    /// <summary>
    /// Checks if image file exists in app data directory.
    /// </summary>
    /// <param name="filename">Filename to check.</param>
    /// <returns>True if file exists.</returns>
    bool ImageExists(string filename);
}
