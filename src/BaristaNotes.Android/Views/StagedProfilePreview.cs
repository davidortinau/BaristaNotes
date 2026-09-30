using Android.Graphics;
using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Services;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class StagedProfilePreview(NativeStyle style, ILogger logger) : FrameLayout(style.Context)
{
    private Bitmap? _bitmap;
    private ImageView? _image;
    private bool _disposed;
    private int _generation;

    public async Task LoadAsync(byte[] bytes)
    {
        var generation = ++_generation;
        if (_image is null)
        {
            Background = style.Rounded(style.Surface, 12);
            SetPadding(style.Dp(16), style.Dp(16), style.Dp(16), style.Dp(16));
            ClipToOutline = true;
            _image = new ImageView(style.Context);
            _image.SetScaleType(ImageView.ScaleType.CenterCrop);
            NativeStyle.Identify(_image, "StagedProfilePhoto");
            AddView(_image, new LayoutParams(-1, -1));
        }
        Bitmap? decoded = null;
        try
        {
            var maximum = Math.Max(style.Dp(160), style.Context.Resources!.DisplayMetrics!.WidthPixels);
            decoded = await Task.Run(() => AndroidBitmapCodec.DecodeOwned(bytes, maximum, logger));
            if (_disposed || generation != _generation)
                return;
            _image.SetImageDrawable(null);
            _bitmap?.Dispose();
            _bitmap = decoded;
            decoded = null;
            _image.SetImageBitmap(_bitmap);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Loading a staged profile preview failed");
        }
        finally
        {
            decoded?.Dispose();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _generation++;
            _image?.SetImageDrawable(null);
            _bitmap?.Dispose();
            _bitmap = null;
        }
        base.Dispose(disposing);
    }
}
