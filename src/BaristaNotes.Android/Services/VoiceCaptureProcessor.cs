using Android.Graphics;
using Android.Media;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp.Services;

internal static class VoiceCaptureProcessor
{
    public static async Task<byte[]> ReadAsync(string source, string processed, VoiceCaptureOptions options,
        ILogger logger, CancellationToken cancellation)
    {
        return await Task.Run(() =>
        {
            cancellation.ThrowIfCancellationRequested();
            using var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
            using var ignored = BitmapFactory.DecodeFile(source, bounds);
            if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0)
                return File.ReadAllBytes(source);
            var sample = 1;
            while (bounds.OutWidth / (sample * 2) >= options.MaximumWidth
                && bounds.OutHeight / (sample * 2) >= options.MaximumHeight) sample *= 2;
            using var decode = new BitmapFactory.Options { InSampleSize = sample };
            using var original = BitmapFactory.DecodeFile(source, decode);
            if (original is null) return File.ReadAllBytes(source);
            var ratio = Math.Min(1d, Math.Min(options.MaximumWidth / (double)original.Width,
                options.MaximumHeight / (double)original.Height));
            Bitmap? resized = null;
            try
            {
                var bitmap = original;
                if (ratio < 1)
                    bitmap = resized = Bitmap.CreateScaledBitmap(original, Math.Max(1, (int)(original.Width * ratio)),
                        Math.Max(1, (int)(original.Height * ratio)), true)
                        ?? throw new IOException("The captured photo could not be resized.");
                using (var output = File.Create(processed))
                    if (!bitmap.Compress(Bitmap.CompressFormat.Jpeg!, options.CompressionQuality, output))
                        throw new IOException("The captured photo could not be encoded.");
                // VoiceCaptureOptions maps to source MediaPicker defaults:
                // RotateImage=false, PreserveMetaData=true. Do not route this
                // through the avatar processor, which applies orientation.
                try
                {
                    using var from = new ExifInterface(source);
                    using var to = new ExifInterface(processed);
#pragma warning disable CS0618, CA1422 // Preserve the pinned source's legacy ISO EXIF tag.
                    string[] tags = [ExifInterface.TagArtist, ExifInterface.TagCopyright, ExifInterface.TagDatetime,
                        ExifInterface.TagImageDescription, ExifInterface.TagMake, ExifInterface.TagModel,
                        ExifInterface.TagOrientation, ExifInterface.TagSoftware, ExifInterface.TagGpsLatitude,
                        ExifInterface.TagGpsLongitude, ExifInterface.TagGpsAltitude, ExifInterface.TagExposureTime,
                        ExifInterface.TagFNumber, ExifInterface.TagIso, ExifInterface.TagWhiteBalance,
                        ExifInterface.TagFlash, ExifInterface.TagFocalLength];
#pragma warning restore CS0618, CA1422
                    foreach (var tag in tags)
                    {
                        var value = from.GetAttribute(tag);
                        if (string.IsNullOrEmpty(value)) continue;
                        try { to.SetAttribute(tag, value); }
                        catch (Exception exception) { logger.LogDebug(exception, "Captured photo EXIF attribute could not be copied"); }
                    }
                    to.SaveAttributes();
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Captured photo metadata could not be preserved; using processed image");
                }
                cancellation.ThrowIfCancellationRequested();
                return File.ReadAllBytes(processed);
            }
            finally { if (!ReferenceEquals(resized, original)) resized?.Dispose(); }
        }, cancellation);
    }
}
