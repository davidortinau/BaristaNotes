using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class ProfileAvatarView : UIView
{
    private nfloat _size;
    private bool _compact;
    private bool _selected;
    private readonly UIView _circle = new();
    private readonly UIImageView _image = new() { ContentMode = UIViewContentMode.ScaleAspectFill, ClipsToBounds = true };
    private readonly UILabel _placeholder = new() { Text = "\ue7fd", TextAlignment = UITextAlignment.Center };
    private UIImage? _ownedImage;
    private string? _path;
    private DateTime? _lastWrite;
    private long? _fileLength;

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
        => _placeholder.Font = _compact
            ? NativeTheme.Icons(_size * 0.55f)
            : UIFontMetrics.DefaultMetrics.GetScaledFont(NativeTheme.Icons(_size / 2), traits);

    public void ConfigurePeople(nfloat size, bool selected)
    {
        _size = size;
        _compact = true;
        _selected = selected;
        _circle.BackgroundColor = NativeTheme.Secondary.ColorWithAlpha(0.12f);
        _circle.Layer.BorderWidth = selected ? 3 : 1;
        _circle.Layer.CornerRadius = size / 2;
        _placeholder.TextColor = NativeTheme.Secondary;
        UserInteractionEnabled = false;
        IsAccessibilityElement = false;
        UpdateFonts(TraitCollection);
        SetNeedsLayout();
    }

    public void SetProfile(UserProfileDto? profile, IImageProcessingService images, ILogger logger)
    {
        try
        {
            var filename = profile?.AvatarPath;
            var path = string.IsNullOrWhiteSpace(filename) ? null :
                Path.IsPathRooted(filename) ? File.Exists(filename) ? filename : null :
                images.ImageExists(filename) ? images.GetImagePath(filename) : null;
            SetPath(path, logger);
        }
        catch (Exception error)
        {
            logger.LogError(error, "Could not load people avatar for {ProfileId}", profile?.Id);
            SetPath(null);
        }
    }

    public void SetPath(string? path, ILogger? logger = null)
    {
        var file = string.IsNullOrEmpty(path) ? null : new FileInfo(path);
        var lastWrite = file?.LastWriteTimeUtc;
        var length = file?.Length;
        // Profile photo changes can replace a file without changing AvatarPath.
        if (_path == path && _lastWrite == lastWrite && _fileLength == length) return;
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
        _lastWrite = lastWrite;
        _fileLength = length;
        // An existing undecodable file is not silently converted into "no photo".
        _image.Hidden = string.IsNullOrEmpty(path);
        _placeholder.Hidden = !string.IsNullOrEmpty(path);
    }

    public override CGSize SizeThatFits(CGSize size) =>
        _compact ? new(_size, _size) : new(_size + 16, _size + 16);
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        if (_compact)
            _circle.Layer.BorderColor = (_selected ? NativeTheme.Primary : NativeTheme.Secondary.ColorWithAlpha(0.4f))
                .GetResolvedColor(TraitCollection).CGColor;
        _circle.Frame = new CGRect((Bounds.Width - _size) / 2, _compact ? (Bounds.Height - _size) / 2 : 8, _size, _size);
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
