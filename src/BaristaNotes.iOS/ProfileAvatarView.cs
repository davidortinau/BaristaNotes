using CoreGraphics;
using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class ProfileAvatarView : UIView
{
    private readonly nfloat _size;
    private readonly UIView _circle = new();
    private readonly UIImageView _image = new() { ContentMode = UIViewContentMode.ScaleAspectFill, ClipsToBounds = true };
    private readonly UILabel _placeholder = new() { Text = "\ue7fd", TextAlignment = UITextAlignment.Center };
    private UIImage? _ownedImage;
    private string? _path;

    public ProfileAvatarView(nfloat size)
    {
        _size = size;
        AccessibilityIdentifier = "ProfileAvatarFrame";
        // MAUI Colors.LightGray/Gray are fixed RGB211/RGB128, not UIKit's named grays.
        var borderColor = UIColor.FromRGB(211, 211, 211);
        _circle.BackgroundColor = borderColor.ColorWithAlpha(0.3f);
        _circle.Layer.BorderWidth = 2;
        _circle.Layer.BorderColor = borderColor.CGColor;
        _circle.Layer.CornerRadius = size / 2;
        _circle.ClipsToBounds = true;
        _placeholder.AccessibilityIdentifier = "ProfileAvatarPlaceholder";
        _placeholder.TextColor = UIColor.FromRGB(128, 128, 128);
        _image.AccessibilityIdentifier = "ProfileAvatarImage";
        _image.Hidden = true;
        _circle.AddSubviews(_image, _placeholder);
        AddSubview(_circle);
        UpdateFonts(TraitCollection);
    }

    public void UpdateFonts(UITraitCollection traits)
        => _placeholder.Font = UIFontMetrics.DefaultMetrics.GetScaledFont(NativeTheme.Icons(_size / 2), traits);

    public void SetPath(string? path, ILogger? logger = null)
    {
        if (_path == path) return;
        UIImage? loaded = null;
        if (!string.IsNullOrEmpty(path))
        {
            using var stream = File.OpenRead(path);
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            using var data = NSData.FromArray(bytes.ToArray());
            loaded = UIImage.LoadFromData(data);
            if (loaded == null) logger?.LogWarning("Saved avatar could not be decoded; retaining the source blank-image behavior");
        }
        _image.Image = loaded;
        _ownedImage?.Dispose();
        _ownedImage = loaded;
        _path = path;
        // An existing undecodable file is not silently converted into "no photo".
        _image.Hidden = string.IsNullOrEmpty(path);
        _placeholder.Hidden = !string.IsNullOrEmpty(path);
    }

    public override CGSize SizeThatFits(CGSize size) => new(_size + 16, _size + 16);
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        _circle.Frame = new CGRect((Bounds.Width - _size) / 2, 8, _size, _size);
        _image.Frame = _circle.Bounds;
        _placeholder.Frame = _circle.Bounds;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _image.Image = null;
            _ownedImage?.Dispose();
            _ownedImage = null;
        }
        base.Dispose(disposing);
    }
}
