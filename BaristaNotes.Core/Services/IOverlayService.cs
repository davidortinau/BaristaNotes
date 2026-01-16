namespace BaristaNotes.Core.Services;

/// <summary>
/// Service for managing a persistent overlay that appears above all app content,
/// including Shell navigation. Used for voice command UI.
/// </summary>
public interface IOverlayService
{
    /// <summary>
    /// Gets whether the overlay is currently visible (either expanded or collapsed).
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// Gets whether the overlay is in collapsed FAB mode.
    /// </summary>
    bool IsCollapsed { get; }

    /// <summary>
    /// Shows the overlay in expanded mode.
    /// </summary>
    void Show();

    /// <summary>
    /// Hides the overlay completely.
    /// </summary>
    void Hide();

    /// <summary>
    /// Collapses the overlay to a small FAB button.
    /// </summary>
    void Collapse();

    /// <summary>
    /// Expands the overlay from FAB to full panel.
    /// </summary>
    void Expand();

    /// <summary>
    /// Updates the overlay content (e.g., transcript text, state indicator).
    /// </summary>
    void UpdateContent(OverlayContent content);

    /// <summary>
    /// Event raised when the overlay visibility changes.
    /// </summary>
    event EventHandler<bool>? VisibilityChanged;

    /// <summary>
    /// Event raised when user requests to close the overlay.
    /// </summary>
    event EventHandler? CloseRequested;

    /// <summary>
    /// Event raised when user taps the collapsed FAB to expand.
    /// </summary>
    event EventHandler? ExpandRequested;
}

/// <summary>
/// Content to display in the voice overlay.
/// </summary>
public record OverlayContent(
    string StateText,
    string Transcript,
    bool IsListening,
    bool IsProcessing,
    string? ErrorMessage = null,
    string? AIResponse = null
);
