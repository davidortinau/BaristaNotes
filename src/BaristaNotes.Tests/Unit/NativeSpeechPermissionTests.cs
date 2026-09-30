using BaristaNotes.Native.iOS;

namespace BaristaNotes.Tests.Unit;

public sealed class NativeSpeechPermissionTests
{
    // The source accepts either Unknown before its final denial check,
    // even if the other permission is Denied or Restricted.
    [Theory]
    [InlineData("Unknown", "Unknown", true)]
    [InlineData("Unknown", "Denied", true)]
    [InlineData("Unknown", "Restricted", true)]
    [InlineData("Unknown", "Granted", true)]
    [InlineData("Denied", "Unknown", true)]
    [InlineData("Denied", "Denied", false)]
    [InlineData("Denied", "Restricted", false)]
    [InlineData("Denied", "Granted", false)]
    [InlineData("Restricted", "Unknown", true)]
    [InlineData("Restricted", "Denied", false)]
    [InlineData("Restricted", "Restricted", false)]
    [InlineData("Restricted", "Granted", false)]
    [InlineData("Granted", "Unknown", true)]
    [InlineData("Granted", "Denied", false)]
    [InlineData("Granted", "Restricted", false)]
    [InlineData("Granted", "Granted", true)]
    public void Preflight_MatchesSourcePermissionTable(
        string speechStatus, string microphoneStatus, bool expected)
    {
        var speech = Enum.Parse<SpeechPermissionStatus>(speechStatus);
        var microphone = Enum.Parse<SpeechPermissionStatus>(microphoneStatus);

        Assert.Equal(expected, SpeechPermissionPreflight.AllowsStart(speech, microphone));
    }
}
