namespace BaristaNotes.AndroidApp.Views;

internal readonly record struct VoiceRect(double X, double Y, double Width, double Height);

internal sealed record VoiceGeometry(VoiceRect Panel, VoiceRect Close, VoiceRect Minimize, VoiceRect Indicator,
    VoiceRect State, VoiceRect Transcript, VoiceRect Response, VoiceRect ReadyHint, VoiceRect MicHit,
    VoiceRect SpeakHint, VoiceRect Fab)
{
    public static VoiceGeometry Calculate(double width, double height, bool hasTranscript)
    {
        // Android's pinned renderer intentionally assumes34 logical safe-bottom,
        // including in landscape. Do not replace this family's contract with
        // the separately corrected Advice popup's real-inset policy.
        const double panelHeight = 420, safeBottom = 34, padding = 20;
        var contentY = 70d;
        const double micTop = 290, contentMaxY = micTop - 15;
        var transcriptHeight = Math.Min(60, (contentMaxY - contentY) / 2);
        var transcript = new VoiceRect(padding, contentY, width - 40, transcriptHeight);
        if (hasTranscript) contentY += transcriptHeight + 10;
        return new(
            new(0, height - panelHeight - safeBottom, width, panelHeight + safeBottom),
            new(width - 60, 20, 40, 40),
            new(width - 104, 20, 40, 40),
            new(20, 20, 24, 30),
            new(44, 20, width - 148, 30),
            transcript,
            new(20, contentY, width - 40, Math.Max(40, contentMaxY - contentY)),
            new(20, contentY, width - 40, 40),
            new(width / 2 - 55, 275, 110, 110),
            new(0, 376, width, 20),
            new(width - 72, height - 166, 56, 56));
    }
}
