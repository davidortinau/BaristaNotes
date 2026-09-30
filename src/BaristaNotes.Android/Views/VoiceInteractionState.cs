namespace BaristaNotes.AndroidApp.Views;

internal enum VoiceMicAction { None, Start, Stop }
internal sealed class VoiceInteractionState
{
    public bool IsVisible { get; private set; }
    public bool IsCollapsed { get; private set; }
    public bool MicActive { get; private set; }
    public bool BlocksInput => IsVisible && !IsCollapsed;
    public void Show() { IsVisible = true; IsCollapsed = false; MicActive = false; }
    public void Hide() { IsVisible = false; IsCollapsed = false; MicActive = false; }
    public void Collapse()
    {
        if (!IsVisible || IsCollapsed) return;
        IsCollapsed = true;
        MicActive = false;
    }
    public void Expand() { if (IsVisible && IsCollapsed) IsCollapsed = false; }
    public VoiceMicAction TapMic(bool processing)
    {
        if (!IsVisible || IsCollapsed || processing) return VoiceMicAction.None;
        MicActive = !MicActive;
        return MicActive ? VoiceMicAction.Start : VoiceMicAction.Stop;
    }
}
