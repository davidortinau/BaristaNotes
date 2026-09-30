using Android.App;
using Android.Content;
using Android.Runtime;

namespace BaristaNotes.AndroidApp.Services;

[ContentProvider(new[] { "${applicationId}.voice.capture" },
    Name = "com.simplyprofound.baristanotes.nativeapp.VoiceFileProvider",
    Exported = false, GrantUriPermissions = true)]
[MetaData("android.support.FILE_PROVIDER_PATHS", Resource = "@xml/voice_capture_paths")]
public sealed class VoiceFileProvider : AndroidX.Core.Content.FileProvider
{
    public VoiceFileProvider() { }
    public VoiceFileProvider(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership) { }
}
