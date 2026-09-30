using Android.Graphics;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp.Services;

public sealed class AndroidImageProcessingService(
    string dataDirectory,
    ILogger<AndroidImageProcessingService> logger) : IImageProcessingService
{
    private readonly ImageFileStore _files = new(dataDirectory, logger);

    public Task<ImageValidationResult> ValidateImageAsync(Stream imageStream) => _files.ValidateAsync(imageStream);
    public Task<string> SaveImageAsync(Stream imageStream, string filename) => _files.SaveAsync(imageStream, filename);
    public Task<bool> DeleteImageAsync(string filename) => _files.DeleteAsync(filename);
    public string GetImagePath(string filename) => _files.Resolve(filename);
    public bool ImageExists(string filename) => _files.Exists(filename);

    public async Task<MemoryStream?> DownsampleAsync(Stream imageStream, int maxDimension, int quality)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDimension, 1);
        if (quality is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(quality));

        using var source = new MemoryStream();
        if (imageStream.CanSeek)
            imageStream.Position = 0;
        await imageStream.CopyToAsync(source);
        var bytes = source.ToArray();
        return await Task.Run(() => DecodeAndResize(bytes, maxDimension, quality));
    }

    private MemoryStream? DecodeAndResize(byte[] bytes, int maxDimension, int quality)
    {
        try
        {
            using var decoded = AndroidBitmapCodec.DecodeOwned(bytes, maxDimension, logger);
            if (decoded is null)
            {
                logger.LogWarning("Native image decoding returned no bitmap");
                return null;
            }

            var output = new MemoryStream();
            try
            {
                if (!decoded.Compress(Bitmap.CompressFormat.Jpeg!, quality, output))
                {
                    output.Dispose();
                    logger.LogWarning("Native JPEG encoding failed; caller retains source fallback");
                    return null;
                }
                output.Position = 0;
                return output;
            }
            catch
            {
                output.Dispose();
                throw;
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Native downsample failed; caller retains original-byte fallback");
            return null;
        }
    }
}
