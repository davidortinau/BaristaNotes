using BaristaNotes.Core.Services;

namespace BaristaNotes.AndroidApp.Services;

// Disposing a conversation removes its event forwarding, never the persistent window.
internal sealed class ScopedVoiceOverlay : IOverlayService, IDisposable
{
    private readonly IOverlayService _inner;
    private readonly Func<bool> _active;
    private bool _disposed;
    public ScopedVoiceOverlay(IOverlayService inner, Func<bool> active)
    {
        (_inner, _active) = (inner, active);
        inner.VisibilityChanged += Visibility;
        inner.CloseRequested += Close;
        inner.ExpandRequested += ExpandEvent;
        inner.MicPressStarted += Start;
        inner.MicPressEnded += End;
    }
    private bool Active => !_disposed && _active();
    public bool IsVisible => Active && _inner.IsVisible;
    public bool IsCollapsed => Active && _inner.IsCollapsed;
    public void Show() { if (Active) _inner.Show(); }
    public void Hide() { if (Active) _inner.Hide(); }
    public void Collapse() { if (Active) _inner.Collapse(); }
    public void Expand() { if (Active) _inner.Expand(); }
    public void UpdateContent(OverlayContent content) { if (Active) _inner.UpdateContent(content); }
    public event EventHandler<bool>? VisibilityChanged;
    public event EventHandler? CloseRequested;
    public event EventHandler? ExpandRequested;
    public event EventHandler? MicPressStarted;
    public event EventHandler? MicPressEnded;
    private void Visibility(object? sender, bool visible) { if (Active) VisibilityChanged?.Invoke(this, visible); }
    private void Close(object? sender, EventArgs args) { if (Active) CloseRequested?.Invoke(this, args); }
    private void ExpandEvent(object? sender, EventArgs args) { if (Active) ExpandRequested?.Invoke(this, args); }
    private void Start(object? sender, EventArgs args) { if (Active) MicPressStarted?.Invoke(this, args); }
    private void End(object? sender, EventArgs args) { if (Active) MicPressEnded?.Invoke(this, args); }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _inner.VisibilityChanged -= Visibility;
        _inner.CloseRequested -= Close;
        _inner.ExpandRequested -= ExpandEvent;
        _inner.MicPressStarted -= Start;
        _inner.MicPressEnded -= End;
        VisibilityChanged = null; CloseRequested = null; ExpandRequested = null;
        MicPressStarted = null; MicPressEnded = null;
    }
}
