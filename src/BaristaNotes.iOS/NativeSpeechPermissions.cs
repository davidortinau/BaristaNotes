using AVFoundation;
using Speech;

namespace BaristaNotes.Native.iOS;

internal static class NativeSpeechPermissions
{
    public static SpeechPermissionStatus Map(SFSpeechRecognizerAuthorizationStatus status) => status switch
    {
        SFSpeechRecognizerAuthorizationStatus.Authorized => SpeechPermissionStatus.Granted,
        SFSpeechRecognizerAuthorizationStatus.Denied => SpeechPermissionStatus.Denied,
        SFSpeechRecognizerAuthorizationStatus.Restricted => SpeechPermissionStatus.Restricted,
        _ => SpeechPermissionStatus.Unknown
    };

    public static SpeechPermissionStatus Map(AVAuthorizationStatus status) => status switch
    {
        AVAuthorizationStatus.Authorized => SpeechPermissionStatus.Granted,
        AVAuthorizationStatus.Denied => SpeechPermissionStatus.Denied,
        AVAuthorizationStatus.Restricted => SpeechPermissionStatus.Restricted,
        _ => SpeechPermissionStatus.Unknown
    };

    public static bool AllowsStart(SFSpeechRecognizerAuthorizationStatus speech, AVAuthorizationStatus microphone) =>
        SpeechPermissionPreflight.AllowsStart(Map(speech), Map(microphone));
}
