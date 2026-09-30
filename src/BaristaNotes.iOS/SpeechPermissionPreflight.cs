namespace BaristaNotes.Native.iOS;

internal enum SpeechPermissionStatus { Unknown, Denied, Restricted, Granted }

internal static class SpeechPermissionPreflight
{
    public static bool AllowsStart(SpeechPermissionStatus speech, SpeechPermissionStatus microphone) =>
        speech == SpeechPermissionStatus.Granted && microphone == SpeechPermissionStatus.Granted
        // The source intentionally checks either Unknown before its final denial,
        // including mixed Unknown/Denied or Unknown/Restricted combinations.
        || speech == SpeechPermissionStatus.Unknown || microphone == SpeechPermissionStatus.Unknown;
}
