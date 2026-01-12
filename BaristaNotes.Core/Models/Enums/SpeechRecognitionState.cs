namespace BaristaNotes.Core.Models.Enums;

/// <summary>
/// State of the speech recognition service.
/// </summary>
public enum SpeechRecognitionState
{
    Idle = 0,
    Listening = 1,
    Processing = 2,
    Error = 3
}
