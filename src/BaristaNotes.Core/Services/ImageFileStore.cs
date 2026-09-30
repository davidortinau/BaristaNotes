using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services;

public sealed class ImageFileStore
{
    public const int MaximumFileBytes = 12 * 1024 * 1024;

    private readonly string _root;
    private readonly ILogger _logger;

    public ImageFileStore(string appDataDirectory, ILogger logger)
    {
        if (!Path.IsPathFullyQualified(appDataDirectory))
            throw new ArgumentException(
                "App image storage requires an absolute app-data directory.", nameof(appDataDirectory));
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(appDataDirectory));
        _logger = logger;
    }

    public async Task<ImageValidationResult> ValidateAsync(Stream imageStream)
    {
        try
        {
            using var copy = new MemoryStream();
            await imageStream.CopyToAsync(copy);
            if (copy.Length > MaximumFileBytes)
                return ImageValidationResult.TooLarge;
            if (imageStream.CanSeek)
                imageStream.Position = 0;
            return ImageValidationResult.Valid;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Native image validation failed");
            return ImageValidationResult.ProcessingFailed;
        }
    }

    public async Task<string> SaveAsync(Stream imageStream, string filename)
    {
        if (imageStream.CanSeek)
            imageStream.Position = 0;
        using var copy = new MemoryStream();
        await imageStream.CopyToAsync(copy);
        copy.Position = 0;
        var path = Resolve(filename);
        await using var file = File.Create(path);
        await copy.CopyToAsync(file);
        _logger.LogDebug("Saved native image {Filename} with {SizeBytes} bytes", filename, copy.Length);
        return path;
    }

    public Task<bool> DeleteAsync(string filename)
    {
        var path = Resolve(filename);
        if (!File.Exists(path))
            return Task.FromResult(false);
        File.Delete(path);
        return Task.FromResult(true);
    }

    public bool Exists(string filename)
    {
        try
        {
            return File.Exists(Resolve(filename));
        }
        catch (ArgumentException exception)
        {
            _logger.LogWarning(exception, "Rejected an image filename outside app storage");
            return false;
        }
    }

    public string Resolve(string filename)
    {
        if (string.IsNullOrEmpty(filename) || filename is "." or ".."
            || Path.IsPathRooted(filename) || filename.IndexOfAny(['/', '\\', '\0']) >= 0)
            throw new ArgumentException(
                "An app image must use a filename, not an external path.", nameof(filename));
        return Path.Combine(_root, filename);
    }

    public string RequireOwnedPath(string path)
    {
        if (!Path.IsPathFullyQualified(path))
            throw new ArgumentException(
                "An image preview requires an absolute app image path.", nameof(path));
        var full = Path.GetFullPath(path);
        if (!string.Equals(Path.GetDirectoryName(full), _root, StringComparison.Ordinal))
            throw new ArgumentException("The image path is outside app image storage.", nameof(path));
        return full;
    }
}
