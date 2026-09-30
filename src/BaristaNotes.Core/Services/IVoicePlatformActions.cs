namespace BaristaNotes.Core.Services;

public sealed record VoiceNavigationRequest(
    string Route, int? EntityId = null, int? BeanId = null, string? BeanName = null);

public sealed record VoiceCaptureOptions(string Title, int MaximumWidth, int MaximumHeight, int CompressionQuality);

public sealed class VoicePhoto(string fileName, Func<Task<Stream>> openRead)
{
    public string FileName { get; } = fileName;
    public Task<Stream> OpenReadAsync() => openRead();
}

public interface IVoicePlatformActions
{
    void Navigate(VoiceNavigationRequest request);
    bool IsCaptureSupported { get; }
    Task<VoicePhoto?> CapturePhotoAsync(VoiceCaptureOptions options);
    Task OpenBrowserAsync(Uri uri);
}
