using BaristaNotes.Core.Services;
using Microsoft.Maui.Handlers;

namespace BaristaNotes.Services;

/// <summary>
/// Voice command overlay that displays above all app content.
/// Uses WindowOverlay + IWindowOverlayElement pattern for cross-platform support.
/// Based on Plugin.Maui.DebugOverlay approach.
/// </summary>
public class VoiceOverlay : WindowOverlay, IOverlayService
{
    private readonly VoiceOverlayPanel _panel;
    private bool _isOverlayVisible;
    private bool _isCollapsed;

    public new bool IsVisible => _isOverlayVisible;
    public bool IsCollapsed => _isCollapsed;

    public event EventHandler<bool>? VisibilityChanged;
    public event EventHandler? CloseRequested;
    public event EventHandler? ExpandRequested;

    public VoiceOverlay(Microsoft.Maui.IWindow window) : base(window)
    {
        _panel = new VoiceOverlayPanel(this);
        this.AddWindowElement(_panel);
        this.Tapped += VoiceOverlay_Tapped;
    }

    private void VoiceOverlay_Tapped(object? sender, WindowOverlayTappedEventArgs e)
    {
        if (!_isOverlayVisible) return;

        // If collapsed, check if FAB was tapped
        if (_isCollapsed)
        {
            if (_panel.IsFabTapped(e.Point))
            {
                ExpandRequested?.Invoke(this, EventArgs.Empty);
            }
            // When collapsed, taps outside FAB pass through (DisableUITouchEventPassthrough = false)
            return;
        }

        // Check if user tapped the close (X) button - collapse to FAB
        if (_panel.IsCloseButtonTapped(e.Point))
        {
            Collapse();
            return;
        }

        // Check if user tapped the cancel button - end session
        if (_panel.IsCancelButtonTapped(e.Point))
        {
            // Delay the close to prevent tap from passing through to underlying UI
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(100);
                CloseRequested?.Invoke(this, EventArgs.Empty);
            });
            return;
        }

        // If tapped on background (outside content panel), collapse
        if (!_panel.IsContentAreaTapped(e.Point))
        {
            Collapse();
        }
    }

    public void Show()
    {
        _isOverlayVisible = true;
        _isCollapsed = false;
        _panel.Show(collapsed: false);
        this.DisableUITouchEventPassthrough = true;
        this.Invalidate();
        VisibilityChanged?.Invoke(this, true);
    }

    public void Hide()
    {
        if (!_isOverlayVisible) return;

        _isOverlayVisible = false;
        _isCollapsed = false;
        _panel.Hide();
        this.DisableUITouchEventPassthrough = false;
        this.Invalidate();
        VisibilityChanged?.Invoke(this, false);
    }

    public void Collapse()
    {
        if (!_isOverlayVisible || _isCollapsed) return;

        _isCollapsed = true;
        _panel.Show(collapsed: true);
        this.DisableUITouchEventPassthrough = false;
        this.Invalidate();
    }

    public void Expand()
    {
        if (!_isOverlayVisible || !_isCollapsed) return;

        _isCollapsed = false;
        _panel.Show(collapsed: false);
        this.DisableUITouchEventPassthrough = true;
        this.Invalidate();
    }

    public void UpdateContent(OverlayContent content)
    {
        _panel.UpdateContent(content);
        this.Invalidate();
    }
}

/// <summary>
/// The visual panel element for the voice overlay.
/// Draws using ICanvas for cross-platform rendering.
/// </summary>
public class VoiceOverlayPanel : IWindowOverlayElement
{
    private readonly VoiceOverlay _overlay;
    private bool _isVisible;
    private bool _isCollapsed;
    private string _stateText = "Listening...";
    private string _transcript = "";
    private string _aiResponse = "";
    private bool _isListening;
    private bool _isProcessing;

    // Layout rectangles for hit testing
    private RectF _panelRect;
    private RectF _closeButtonRect;
    private RectF _cancelButtonRect;
    private RectF _contentAreaRect;
    private RectF _fabRect;

    // Colors
    private readonly Color _backgroundColor = Colors.Transparent; // Transparent - only panel is opaque
    private readonly Color _panelColor = Color.FromArgb("#FF1E1E1E"); // Dark gray
    private readonly Color _accentColor = Color.FromArgb("#FFFF9500"); // Orange
    private readonly Color _textColor = Colors.White;
    private readonly Color _secondaryTextColor = Color.FromArgb("#FFCCCCCC");
    private readonly Color _aiResponseColor = Color.FromArgb("#FF90EE90"); // Light green for AI response

    // Dimensions
    private const float PanelHeight = 380f; // Increased to fit longer AI responses
    private const float CornerRadius = 20f;
    private const float Padding = 20f;
    private const float CloseButtonSize = 40f;
    private const float FabSize = 56f;
    private const float FabMargin = 16f;

    public VoiceOverlayPanel(VoiceOverlay overlay)
    {
        _overlay = overlay;
    }

    public void Show(bool collapsed)
    {
        _isVisible = true;
        _isCollapsed = collapsed;
    }

    public void Hide()
    {
        _isVisible = false;
        _isCollapsed = false;
    }

    public void UpdateContent(OverlayContent content)
    {
        _stateText = content.StateText;
        _transcript = content.Transcript;
        _aiResponse = content.AIResponse ?? "";
        _isListening = content.IsListening;
        _isProcessing = content.IsProcessing;
    }

    public bool Contains(Point point)
    {
        if (!_isVisible)
            return false;

        // When collapsed, only consume taps on FAB
        if (_isCollapsed)
        {
            return _fabRect.Contains(point);
        }
        
        // When expanded, consume taps on the panel area
        return _panelRect.Contains(point);
    }

    public bool IsCloseButtonTapped(Point point)
    {
        return _isVisible && !_isCollapsed && _closeButtonRect.Contains(point);
    }

    public bool IsCancelButtonTapped(Point point)
    {
        return _isVisible && !_isCollapsed && _cancelButtonRect.Contains(point);
    }

    public bool IsContentAreaTapped(Point point)
    {
        return _isVisible && !_isCollapsed && _contentAreaRect.Contains(point);
    }

    public bool IsFabTapped(Point point)
    {
        return _isVisible && _isCollapsed && _fabRect.Contains(point);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (!_isVisible) return;

        try
        {
            // Get safe area (approximate for cross-platform)
            float safeBottom = 34f; // Home indicator area
            float safeTop = 50f;

#if IOS
            try
            {
                var app = UIKit.UIApplication.SharedApplication;
                if (app?.ConnectedScenes != null)
                {
                    foreach (var scene in app.ConnectedScenes)
                    {
                        if (scene is UIKit.UIWindowScene ws)
                        {
                            foreach (var w in ws.Windows)
                            {
                                if (w.IsKeyWindow)
                                {
                                    safeBottom = (float)w.SafeAreaInsets.Bottom;
                                    safeTop = (float)w.SafeAreaInsets.Top;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
#endif

            // If collapsed, draw only the FAB
            if (_isCollapsed)
            {
                DrawCollapsedFab(canvas, dirtyRect, safeBottom);
                return;
            }

            // Draw semi-transparent background
            canvas.FillColor = _backgroundColor;
            canvas.FillRectangle(dirtyRect);

            // Calculate panel position (bottom sheet)
            float panelY = dirtyRect.Height - PanelHeight - safeBottom;
            _panelRect = new RectF(0, panelY, dirtyRect.Width, PanelHeight + safeBottom);
            _contentAreaRect = _panelRect;

            // Draw panel background with rounded top corners
            canvas.FillColor = _panelColor;
            var panelPath = new PathF();
            panelPath.MoveTo(0, panelY + CornerRadius);
            panelPath.QuadTo(0, panelY, CornerRadius, panelY);
            panelPath.LineTo(dirtyRect.Width - CornerRadius, panelY);
            panelPath.QuadTo(dirtyRect.Width, panelY, dirtyRect.Width, panelY + CornerRadius);
            panelPath.LineTo(dirtyRect.Width, dirtyRect.Height);
            panelPath.LineTo(0, dirtyRect.Height);
            panelPath.Close();
            canvas.FillPath(panelPath);

            // Draw close button (X) in top right
            float closeX = dirtyRect.Width - Padding - CloseButtonSize;
            float closeY = panelY + Padding;
            _closeButtonRect = new RectF(closeX, closeY, CloseButtonSize, CloseButtonSize);
            
            canvas.FontColor = _textColor;
            canvas.FontSize = 24;
            canvas.DrawString("✕", _closeButtonRect, HorizontalAlignment.Center, VerticalAlignment.Center);

            // Draw state text with activity indicator simulation
            float stateY = panelY + Padding;
            canvas.FontColor = _textColor;
            canvas.FontSize = 20;
            canvas.Font = new Microsoft.Maui.Graphics.Font("Arial", 700, FontStyleType.Normal); // Bold
            
            var indicatorText = (_isListening || _isProcessing) ? "● " : "";
            if (_isListening || _isProcessing)
            {
                canvas.FontColor = _accentColor;
                canvas.DrawString(indicatorText, new RectF(Padding, stateY, 24, 30), HorizontalAlignment.Left, VerticalAlignment.Center);
            }
            
            canvas.FontColor = _textColor;
            canvas.DrawString(_stateText, new RectF(Padding + 24, stateY, dirtyRect.Width - Padding * 2 - CloseButtonSize - 24, 30), 
                HorizontalAlignment.Left, VerticalAlignment.Center);

            // Draw transcript (user's speech)
            float transcriptY = stateY + 50;
            canvas.FontColor = _secondaryTextColor;
            canvas.FontSize = 16;
            canvas.Font = new Microsoft.Maui.Graphics.Font("Arial", 400, FontStyleType.Normal); // Regular
            
            float transcriptHeight = 60f; // Reduced height for transcript
            var transcriptRect = new RectF(Padding, transcriptY, dirtyRect.Width - Padding * 2, transcriptHeight);
            
            // Simple text wrapping (draw what fits)
            if (!string.IsNullOrEmpty(_transcript))
            {
                canvas.DrawString(_transcript, transcriptRect, HorizontalAlignment.Left, VerticalAlignment.Top);
            }
            else if (string.IsNullOrEmpty(_aiResponse))
            {
                canvas.FontColor = Color.FromArgb("#FF888888");
                canvas.DrawString("Say something like \"Log shot 18 in, 36 out, 28 seconds\"", 
                    transcriptRect, HorizontalAlignment.Left, VerticalAlignment.Top);
            }

            // Draw AI response if available
            if (!string.IsNullOrEmpty(_aiResponse))
            {
                float responseY = transcriptY + transcriptHeight + 10;
                canvas.FontColor = _aiResponseColor;
                canvas.FontSize = 15;
                canvas.Font = new Microsoft.Maui.Graphics.Font("Arial", 400, FontStyleType.Italic);
                
                // More space for response text (panel height - header - transcript - cancel button area)
                var responseRect = new RectF(Padding, responseY, dirtyRect.Width - Padding * 2, PanelHeight - 200);
                canvas.DrawString(_aiResponse, responseRect, HorizontalAlignment.Left, VerticalAlignment.Top);
            }

            // Draw cancel button at bottom
            float cancelButtonWidth = 100f;
            float cancelButtonHeight = 40f;
            float cancelX = (dirtyRect.Width - cancelButtonWidth) / 2;
            float cancelY = panelY + PanelHeight - cancelButtonHeight - Padding;
            _cancelButtonRect = new RectF(cancelX, cancelY, cancelButtonWidth, cancelButtonHeight);
            
            canvas.FontColor = _accentColor;
            canvas.FontSize = 16;
            canvas.DrawString("Cancel", _cancelButtonRect,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }
        catch (Exception)
        {
            // Silently ignore drawing errors
        }
    }

    private void DrawCollapsedFab(ICanvas canvas, RectF dirtyRect, float safeBottom)
    {
        // Position FAB in bottom right corner, above tab bar (add extra 60px for tab bar height)
        float tabBarHeight = 60f;
        float fabX = dirtyRect.Width - FabSize - FabMargin;
        float fabY = dirtyRect.Height - FabSize - FabMargin - safeBottom - tabBarHeight;
        _fabRect = new RectF(fabX, fabY, FabSize, FabSize);

        // Draw FAB circle background
        canvas.FillColor = _accentColor;
        canvas.FillCircle(fabX + FabSize / 2, fabY + FabSize / 2, FabSize / 2);

        // Draw microphone icon (simple circle + stem representation)
        canvas.FillColor = _textColor;
        float iconCenterX = fabX + FabSize / 2;
        float iconCenterY = fabY + FabSize / 2;
        
        // Draw mic body (rounded rectangle approximation with circles)
        float micWidth = 12f;
        float micHeight = 18f;
        float micTop = iconCenterY - micHeight / 2 - 2;
        
        // Mic body using rounded rect path
        var micPath = new PathF();
        float micLeft = iconCenterX - micWidth / 2;
        float micRadius = micWidth / 2;
        micPath.MoveTo(micLeft, micTop + micRadius);
        micPath.LineTo(micLeft, micTop + micHeight - micRadius);
        micPath.QuadTo(micLeft, micTop + micHeight, micLeft + micRadius, micTop + micHeight);
        micPath.LineTo(micLeft + micWidth - micRadius, micTop + micHeight);
        micPath.QuadTo(micLeft + micWidth, micTop + micHeight, micLeft + micWidth, micTop + micHeight - micRadius);
        micPath.LineTo(micLeft + micWidth, micTop + micRadius);
        micPath.QuadTo(micLeft + micWidth, micTop, micLeft + micRadius, micTop);
        micPath.LineTo(micLeft + micRadius, micTop);
        micPath.QuadTo(micLeft, micTop, micLeft, micTop + micRadius);
        micPath.Close();
        canvas.FillPath(micPath);

        // Draw mic stand (U shape at bottom)
        canvas.StrokeColor = _textColor;
        canvas.StrokeSize = 2f;
        float standTop = micTop + micHeight + 2;
        float standWidth = micWidth + 6;
        float standLeft = iconCenterX - standWidth / 2;
        
        var standPath = new PathF();
        standPath.MoveTo(standLeft, micTop + micHeight / 2);
        standPath.LineTo(standLeft, standTop);
        standPath.QuadTo(standLeft, standTop + 6, iconCenterX, standTop + 6);
        standPath.QuadTo(standLeft + standWidth, standTop + 6, standLeft + standWidth, standTop);
        standPath.LineTo(standLeft + standWidth, micTop + micHeight / 2);
        canvas.DrawPath(standPath);

        // Draw stem
        canvas.DrawLine(iconCenterX, standTop + 6, iconCenterX, standTop + 12);

        // Draw pulsing indicator if listening/processing
        if (_isListening || _isProcessing)
        {
            canvas.StrokeColor = _textColor;
            canvas.StrokeSize = 2f;
            canvas.DrawCircle(fabX + FabSize / 2, fabY + FabSize / 2, FabSize / 2 + 4);
        }
    }
}

/// <summary>
/// Extension methods for adding VoiceOverlay to the app.
/// </summary>
public static class VoiceOverlayExtensions
{
    private static VoiceOverlay? _overlay;

    /// <summary>
    /// Adds voice overlay support to the app.
    /// Call this in MauiProgram.cs ConfigureMauiHandlers.
    /// </summary>
    public static MauiAppBuilder UseVoiceOverlay(this MauiAppBuilder builder)
    {
        builder.ConfigureMauiHandlers(handlers =>
        {
            WindowHandler.Mapper.AppendToMapping("AddVoiceOverlay", (handler, view) =>
            {
                _overlay = new VoiceOverlay(handler.VirtualView);
                handler.VirtualView.AddOverlay(_overlay);
            });
        });

        // Register as singleton
        builder.Services.AddSingleton<IOverlayService>(sp =>
        {
            // Return the overlay instance once it's created
            // This is a workaround since the overlay is created during Window initialization
            return _overlay ?? throw new InvalidOperationException("VoiceOverlay not yet initialized. Ensure UseVoiceOverlay is called before services are resolved.");
        });

        return builder;
    }

    /// <summary>
    /// Gets the current VoiceOverlay instance.
    /// </summary>
    public static VoiceOverlay? GetVoiceOverlay() => _overlay;
}
