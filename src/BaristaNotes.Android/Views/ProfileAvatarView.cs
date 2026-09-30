using Android.Graphics;
using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Services;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class ProfileAvatarView : FrameLayout
{
    private readonly NativeStyle _style;
    private readonly ILogger _logger;
    private readonly ImageView _image;
    private readonly TextView _placeholder;
    private readonly ImageFileStore _files;
    private Bitmap? _bitmap;
    private int _generation;
    private int _size;
    private int _loadedSize;
    private string? _path;
    private bool _disposed;

    public ProfileAvatarView(NativeStyle style, ILogger logger, int size) : base(style.Context)
    {
        _style = style;
        _logger = logger;
        _files = new ImageFileStore(
            style.Context.FilesDir?.AbsolutePath
                ?? throw new InvalidOperationException("App image storage is unavailable."),
            logger);
        _image = new ImageView(style.Context);
        _image.SetScaleType(ImageView.ScaleType.CenterCrop);
        _image.ImportantForAccessibility = ImportantForAccessibility.No;
        AddView(_image, new LayoutParams(-1, -1));
        _placeholder = style.Label("\ue7fd", size * .5f, color: Color.Gray);
        _placeholder.Typeface = style.Symbols;
        _placeholder.Gravity = GravityFlags.Center;
        _placeholder.ImportantForAccessibility = ImportantForAccessibility.No;
        AddView(_placeholder, new LayoutParams(-1, -1));
        SetAutomationPrefix("ProfileAvatar");
        ClipToOutline = true;
        ConfigureCircular(size);
        SetRequestedImage(false);
    }

    public void SetAutomationPrefix(string prefix)
    {
        NativeStyle.Identify(this, prefix + "Frame");
        NativeStyle.Identify(_image, prefix + "Image");
        NativeStyle.Identify(_placeholder, prefix + "Placeholder");
    }

    public void ConfigureCircular(int size)
    {
        Configure(size, Color.LightGray, 2, Color.Argb(77, 211, 211, 211), Color.Gray, size * .5f);
    }

    public void ConfigurePeople(int size, bool selected)
    {
        var outline = selected ? _style.Primary
            : Color.Argb(102, _style.Secondary.R, _style.Secondary.G, _style.Secondary.B);
        Configure(size, outline, selected ? 3 : 1,
            Color.Argb(31, _style.Secondary.R, _style.Secondary.G, _style.Secondary.B),
            _style.Secondary, size * .55f);
    }

    private void Configure(int size, Color outline, int stroke, Color fill, Color glyphColor, float glyphSize)
    {
        _size = size;
        Background = _style.Rounded(fill, size / 2f);
        Foreground = _style.Rounded(Color.Transparent, size / 2f, outline, stroke);
        _placeholder.SetTextColor(glyphColor);
        _placeholder.SetTextSize(Android.Util.ComplexUnitType.Sp, glyphSize);
    }

    public Task LoadFileAsync(string? path)
    {
        if (_disposed)
            return Task.CompletedTask;
        if (path == _path && _loadedSize == _size && _bitmap is not null)
            return Task.CompletedTask;
        _path = path;
        var generation = ++_generation;
        SetRequestedImage(!string.IsNullOrEmpty(path));
        ReleaseBitmap();
        if (string.IsNullOrEmpty(path))
            return Task.CompletedTask;
        return LoadAsync(async () =>
        {
            var owned = _files.RequireOwnedPath(path);
            return await File.ReadAllBytesAsync(owned);
        }, generation);
    }

    private async Task LoadAsync(Func<Task<byte[]>> readBytes, int generation)
    {
        Bitmap? decoded = null;
        try
        {
            var maximum = Math.Max(1, _style.Dp(_size));
            var bytes = await readBytes();
            decoded = await Task.Run(() => AndroidBitmapCodec.DecodeOwned(bytes, maximum, _logger));
            if (_disposed || generation != _generation)
                return;
            _bitmap = decoded;
            decoded = null;
            _loadedSize = _size;
            _image.SetImageBitmap(_bitmap);
            // A nonempty stored path with undecodable bytes stays a blank image,
            // just like the source. It is not proof that validation was repaired.
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Loading an app profile avatar failed");
        }
        finally
        {
            decoded?.Dispose();
        }
    }

    private void SetRequestedImage(bool hasImage)
    {
        _image.Visibility = hasImage ? ViewStates.Visible : ViewStates.Gone;
        _placeholder.Visibility = hasImage ? ViewStates.Gone : ViewStates.Visible;
    }

    private void ReleaseBitmap()
    {
        _image.SetImageDrawable(null);
        _bitmap?.Dispose();
        _bitmap = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _generation++;
            ReleaseBitmap();
        }
        base.Dispose(disposing);
    }
}
