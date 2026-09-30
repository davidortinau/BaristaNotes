using BaristaNotes.Core.Services;

namespace BaristaNotes.AndroidApp.Services;

internal sealed record AndroidCaptureRequest(string Title, VoiceCaptureOptions? Processing = null)
{
    public static AndroidCaptureRequest GeneralPhoto => FromVoice(new("Take a photo", 1024, 1024, 70));
    public static AndroidCaptureRequest LabelPhoto => new("Photograph bag label");

    public static AndroidCaptureRequest FromVoice(VoiceCaptureOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaximumWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaximumHeight, 1);
        if (options.CompressionQuality is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(options));
        return new(options.Title, options);
    }
}
