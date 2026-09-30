using Android.Graphics;
using Android.Media;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp.Services;

internal static class AndroidBitmapCodec
{
    // The returned bitmap is owned by the caller. Null is not image validation:
    // the shared profile service deliberately permits original-byte fallback.
    public static Bitmap? DecodeOwned(byte[] bytes, int maxDimension, ILogger logger)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDimension, 1);
        using var options = new BitmapFactory.Options { InJustDecodeBounds = true };
        using var bounds = BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, options);
        if (options.OutWidth < 1 || options.OutHeight < 1)
        {
            logger.LogWarning("Android image decoder did not return valid dimensions");
            return null;
        }
        var sample = 1;
        var largest = Math.Max(options.OutWidth, options.OutHeight);
        while (sample <= int.MaxValue / 2 && largest / (sample * 2) >= maxDimension)
            sample *= 2;
        options.InJustDecodeBounds = false;
        options.InSampleSize = sample;
        Bitmap? bitmap = BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, options);
        if (bitmap is null)
            return null;
        try
        {
            var transform = ExifTransform.FromOrientation(ReadOrientation(bytes, logger));
            if (!transform.IsIdentity)
            {
                using var matrix = new Matrix();
                matrix.PreScale(transform.ScaleX, transform.ScaleY);
                matrix.PostRotate(transform.Rotation);
                var rotated = Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true);
                if (rotated is not null && !ReferenceEquals(rotated, bitmap))
                {
                    bitmap.Dispose();
                    bitmap = rotated;
                }
            }
            var ratio = Math.Min(1d, (double)maxDimension / Math.Max(bitmap.Width, bitmap.Height));
            if (ratio < 1)
            {
                var resized = Bitmap.CreateScaledBitmap(bitmap,
                    Math.Max(1, (int)(bitmap.Width * ratio)), Math.Max(1, (int)(bitmap.Height * ratio)), true);
                if (!ReferenceEquals(resized, bitmap))
                {
                    bitmap.Dispose();
                    bitmap = resized;
                }
            }
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static int ReadOrientation(byte[] bytes, ILogger logger)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var exif = new ExifInterface(stream);
            return exif.GetAttributeInt(ExifInterface.TagOrientation!, 1);
        }
        catch (Exception exception)
        {
            // The pinned decoder falls back to ordinary decoding if EXIF cannot
            // be read; do not turn this into a stronger image-validity check.
            logger.LogDebug(exception, "EXIF orientation unavailable; decoding without an orientation transform");
            return 1;
        }
    }
}
