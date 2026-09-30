using BaristaNotes.Core.Services;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed record NativePhotoRequest(string Title, int? MaximumWidth = null, int? MaximumHeight = null, int CompressionQuality = 100)
{
    public static NativePhotoRequest General { get; } = new("Take a photo", 1024, 1024, 70);
    public static NativePhotoRequest LabelScan { get; } = new("Photograph bag label");
    public bool HasTransform => MaximumWidth.HasValue || MaximumHeight.HasValue || CompressionQuality < 100;
}

internal interface INativePhotoCapture
{
    bool CaptureSupported { get; }
    Task<VoicePhoto?> CaptureAsync(UIViewController presenter, NativePhotoRequest request, CancellationToken cancellationToken = default);
}

internal sealed class NativeCameraPermissionException : Exception
{
    public NativeCameraPermissionException() : base("Camera permission denied.") { }
}
