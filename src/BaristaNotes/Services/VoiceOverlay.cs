using BaristaNotes.Core.Services;
#if IOS
using UIKit;
using Foundation;
#endif

namespace BaristaNotes.Services;

/// <summary>
/// Voice command overlay that displays above all app content.
/// Uses WindowOverlay + IWindowOverlayElement pattern for cross-platform support.
/// Supports push-to-talk: user holds the mic button to record, releases to process.
/// </summary>
public class VoiceOverlay : WindowOverlay, IOverlayService
{
    private readonly VoiceOverlayPanel _panel;
    private bool _isOverlayVisible;
    private bool _isCollapsed;
    private bool _isMicActive;
    private bool _isProcessing;

    public new bool IsVisible => _isOverlayVisible;
    public bool IsCollapsed => _isCollapsed;

    public event EventHandler<bool>? VisibilityChanged;
    public event EventHandler? CloseRequested;
    public event EventHandler? ExpandRequested;
    public event EventHandler? MicPressStarted;
    public event EventHandler? MicPressEnded;

    public VoiceOverlay(Microsoft.Maui.IWindow window) : base(window)
    {
        _panel = new VoiceOverlayPanel(this);
        this.AddWindowElement(_panel);
        this.Tapped += VoiceOverlay_Tapped;
    }

    /// <summary>
    /// Sets up platform-specific touch handling for push-to-talk.
    /// On iOS, uses UILongPressGestureRecognizer to detect press/release on the mic button.
    /// Must be called after the overlay is added to the window.
    /// </summary>
    public void SetupPlatformTouchHandling()
    {
#if IOS
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Window?.Handler?.PlatformView is UIWindow uiWindow)
            {
                var gesture = new UILongPressGestureRecognizer(HandleiOSTouchGesture);
                gesture.MinimumPressDuration = 0;
                gesture.CancelsTouchesInView = false;
                gesture.ShouldReceiveTouch = (recognizer, touch) =>
                {
                    if (!_isOverlayVisible || _isCollapsed || _isProcessing) return false;
                    var point = touch.LocationInView(uiWindow);
                    return _panel.IsMicButtonArea(new Point(point.X, point.Y));
                };
                uiWindow.AddGestureRecognizer(gesture);
            }
        });
#endif
    }

#if IOS
    private void HandleiOSTouchGesture(UILongPressGestureRecognizer gesture)
    {
        if (!_isOverlayVisible || _isCollapsed) return;

        switch (gesture.State)
        {
            case UIGestureRecognizerState.Began:
                _isMicActive = true;
                _panel.SetMicPressed(true);
                this.Invalidate();
                MicPressStarted?.Invoke(this, EventArgs.Empty);
                break;

            case UIGestureRecognizerState.Ended:
            case UIGestureRecognizerState.Cancelled:
            case UIGestureRecognizerState.Failed:
                if (_isMicActive)
                {
                    _isMicActive = false;
                    _panel.SetMicPressed(false);
                    this.Invalidate();
                    MicPressEnded?.Invoke(this, EventArgs.Empty);
                }
                break;
        }
    }
#endif

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
            return;
        }

        // Mic button area: on iOS handled by push-to-talk gesture, on other platforms use tap-toggle
        if (_panel.IsMicButtonArea(e.Point))
        {
#if !IOS
            if (_isProcessing) return;
            if (!_isMicActive)
            {
                _isMicActive = true;
                _panel.SetMicPressed(true);
                this.Invalidate();
                MicPressStarted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _isMicActive = false;
                _panel.SetMicPressed(false);
                this.Invalidate();
                MicPressEnded?.Invoke(this, EventArgs.Empty);
            }
#endif
            return;
        }

        // Check if user tapped the minimize button - collapse to FAB
        if (_panel.IsMinimizeButtonTapped(e.Point))
        {
            Collapse();
            return;
        }

        // Check if user tapped the close (X) button - end session
        if (_panel.IsCloseButtonTapped(e.Point))
        {
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
        _isMicActive = false;
        _isProcessing = false;
        _panel.SetMicPressed(false);
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
        _isMicActive = false;
        _isProcessing = false;
        _panel.SetMicPressed(false);
        _panel.Hide();
        this.DisableUITouchEventPassthrough = false;
        this.Invalidate();
        VisibilityChanged?.Invoke(this, false);
    }

    public void Collapse()
    {
        if (!_isOverlayVisible || _isCollapsed) return;

        _isCollapsed = true;
        _isMicActive = false;
        _panel.SetMicPressed(false);
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
        _isProcessing = content.IsProcessing;
        _panel.UpdateContent(content);
        this.Invalidate();
    }
}

/// <summary>
/// The visual panel element for the voice overlay.
/// Draws using ICanvas for cross-platform rendering.
/// Includes a push-to-talk mic button that the user holds to record.
/// </summary>
public class VoiceOverlayPanel : IWindowOverlayElement
{
    private readonly VoiceOverlay _overlay;
    private bool _isVisible;
    private bool _isCollapsed;
    private string _stateText = "Ready";
    private string _transcript = "";
    private string _aiResponse = "";
    private bool _isListening;
    private bool _isProcessing;
    private bool _isReady;
    private bool _isMicPressed;

    // Layout rectangles for hit testing
    private RectF _panelRect;
    private RectF _closeButtonRect;
    private RectF _minimizeButtonRect;
    private RectF _contentAreaRect;
    private RectF _fabRect;
    private RectF _micButtonRect;

    // Colors
    private readonly Color _backgroundColor = Colors.Transparent;
    private readonly Color _panelColor = Color.FromArgb("#FF1E1E1E"); // Dark gray
    private readonly Color _accentColor = Color.FromArgb("#FFFF9500"); // Orange
    private readonly Color _textColor = Colors.White;
    private readonly Color _secondaryTextColor = Color.FromArgb("#FFCCCCCC");
    private readonly Color _aiResponseColor = Color.FromArgb("#FF90EE90"); // Light green
    private readonly Color _micIdleColor = Color.FromArgb("#FF333333"); // Dark gray fill for idle mic
    private readonly Color _micIdleStrokeColor = Color.FromArgb("#FF888888"); // Gray outline for idle mic

    // Dimensions
    private const float PanelHeight = 420f;
    private const float CornerRadius = 20f;
    private const float Padding = 20f;
    private const float CloseButtonSize = 40f;
    private const float FabSize = 56f;
    private const float FabMargin = 16f;
    private const float MicButtonSize = 80f; // Push-to-talk button diameter

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

    public void SetMicPressed(bool pressed)
    {
        _isMicPressed = pressed;
    }

    public void UpdateContent(OverlayContent content)
    {
        _stateText = content.StateText;
        _transcript = content.Transcript;
        _aiResponse = content.AIResponse ?? "";
        _isListening = content.IsListening;
        _isProcessing = content.IsProcessing;
        _isReady = content.IsReady;
    }

    public bool Contains(Point point)
    {
        if (!_isVisible)
            return false;

        if (_isCollapsed)
            return _fabRect.Contains(point);

        return _panelRect.Contains(point);
    }

    public bool IsCloseButtonTapped(Point point) =>
        _isVisible && !_isCollapsed && _closeButtonRect.Contains(point);

    public bool IsMinimizeButtonTapped(Point point) =>
        _isVisible && !_isCollapsed && _minimizeButtonRect.Contains(point);

    public bool IsContentAreaTapped(Point point) =>
        _isVisible && !_isCollapsed && _contentAreaRect.Contains(point);

    public bool IsFabTapped(Point point) =>
        _isVisible && _isCollapsed && _fabRect.Contains(point);

    /// <summary>
    /// Checks if a point is within the push-to-talk mic button area.
    /// Uses an expanded hit area for easier pressing.
    /// </summary>
    public bool IsMicButtonArea(Point point)
    {
        if (!_isVisible || _isCollapsed) return false;
        var expandedRect = new RectF(
            _micButtonRect.X - 15,
            _micButtonRect.Y - 15,
            _micButtonRect.Width + 30,
            _micButtonRect.Height + 30);
        return expandedRect.Contains(point);
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

            // Draw close (X) button in top right — ends session
            float closeX = dirtyRect.Width - Padding - CloseButtonSize;
            float closeY = panelY + Padding;
            _closeButtonRect = new RectF(closeX, closeY, CloseButtonSize, CloseButtonSize);

            canvas.FontColor = _textColor;
            canvas.FontSize = 24;
            canvas.DrawString("✕", _closeButtonRect, HorizontalAlignment.Center, VerticalAlignment.Center);

            // Draw minimize (−) button to the left of close — collapses to FAB
            float minimizeX = closeX - CloseButtonSize - 4;
            _minimizeButtonRect = new RectF(minimizeX, closeY, CloseButtonSize, CloseButtonSize);

            canvas.FontColor = _secondaryTextColor;
            canvas.FontSize = 22;
            canvas.DrawString("−", _minimizeButtonRect, HorizontalAlignment.Center, VerticalAlignment.Center);

            // Draw state text with activity indicator
            float stateY = panelY + Padding;
            canvas.FontSize = 20;
            canvas.Font = new Microsoft.Maui.Graphics.Font("Arial", 700, FontStyleType.Normal); // Bold

            if (_isListening || _isProcessing)
            {
                canvas.FontColor = _accentColor;
                canvas.DrawString("● ", new RectF(Padding, stateY, 24, 30), HorizontalAlignment.Left, VerticalAlignment.Center);
            }

            canvas.FontColor = _textColor;
            canvas.DrawString(_stateText, new RectF(Padding + 24, stateY, dirtyRect.Width - Padding * 2 - CloseButtonSize * 2 - 28, 30),
                HorizontalAlignment.Left, VerticalAlignment.Center);

            // Content area starts below state text
            float contentY = stateY + 50;
            float contentWidth = dirtyRect.Width - Padding * 2;

            // Calculate mic button position (centered, near bottom of panel)
            float micButtonCenterX = dirtyRect.Width / 2;
            float micButtonCenterY = panelY + PanelHeight - Padding - MicButtonSize / 2 - 30;
            _micButtonRect = new RectF(
                micButtonCenterX - MicButtonSize / 2,
                micButtonCenterY - MicButtonSize / 2,
                MicButtonSize,
                MicButtonSize);

            // Content area ends above mic button
            float contentMaxY = micButtonCenterY - MicButtonSize / 2 - 15;

            canvas.FontSize = 16;
            canvas.Font = new Microsoft.Maui.Graphics.Font("Arial", 400, FontStyleType.Normal);

            // Draw transcript (user's speech) if available
            if (!string.IsNullOrEmpty(_transcript))
            {
                canvas.FontColor = _secondaryTextColor;
                float transcriptHeight = Math.Min(60f, (contentMaxY - contentY) / 2);
                var transcriptRect = new RectF(Padding, contentY, contentWidth, transcriptHeight);
                canvas.DrawString(_transcript, transcriptRect, HorizontalAlignment.Left, VerticalAlignment.Top);
                contentY += transcriptHeight + 10f;
            }

            // Draw AI response if available
            if (!string.IsNullOrEmpty(_aiResponse))
            {
                canvas.FontColor = _aiResponseColor;
                canvas.FontSize = 15;
                canvas.Font = new Microsoft.Maui.Graphics.Font("Arial", 400, FontStyleType.Italic);

                float responseHeight = Math.Max(40f, contentMaxY - contentY);
                var responseRect = new RectF(Padding, contentY, contentWidth, responseHeight);
                canvas.DrawString(_aiResponse, responseRect, HorizontalAlignment.Left, VerticalAlignment.Top);
            }
            else if (string.IsNullOrEmpty(_transcript) && _isReady)
            {
                // Show hint text when ready and nothing to display
                canvas.FontColor = Color.FromArgb("#FF888888");
                canvas.FontSize = 14;
                canvas.Font = new Microsoft.Maui.Graphics.Font("Arial", 400, FontStyleType.Normal);
                var hintRect = new RectF(Padding, contentY, contentWidth, 40f);
                canvas.DrawString("Say something like \"Log shot 18 in, 36 out, 28 seconds\"",
                    hintRect, HorizontalAlignment.Center, VerticalAlignment.Top);
            }

            // Draw push-to-talk mic button
            DrawMicButton(canvas, micButtonCenterX, micButtonCenterY);

            // Draw "Hold to speak" hint below mic button when ready
            if (_isReady && !_isListening && !_isProcessing)
            {
                canvas.FontColor = Color.FromArgb("#FF888888");
                canvas.FontSize = 13;
                canvas.Font = new Microsoft.Maui.Graphics.Font("Arial", 400, FontStyleType.Normal);
                float hintY = micButtonCenterY + MicButtonSize / 2 + 6;
                canvas.DrawString("Hold to speak", new RectF(0, hintY, dirtyRect.Width, 20),
                    HorizontalAlignment.Center, VerticalAlignment.Center);
            }

        }
        catch (Exception)
        {
            // Silently ignore drawing errors
        }
    }

    /// <summary>
    /// Draws the push-to-talk mic button with visual state feedback.
    /// </summary>
    private void DrawMicButton(ICanvas canvas, float centerX, float centerY)
    {
        float radius = MicButtonSize / 2;

        if (_isMicPressed || _isListening)
        {
            // Active/pressed: filled orange circle with pulse ring
            canvas.FillColor = _accentColor;
            canvas.FillCircle(centerX, centerY, radius);

            canvas.StrokeColor = _accentColor;
            canvas.StrokeSize = 2f;
            canvas.DrawCircle(centerX, centerY, radius + 6);
        }
        else if (_isProcessing)
        {
            // Processing: filled orange, no pulse
            canvas.FillColor = _accentColor;
            canvas.FillCircle(centerX, centerY, radius);
        }
        else
        {
            // Idle/Ready: dark filled circle with gray outline
            canvas.FillColor = _micIdleColor;
            canvas.FillCircle(centerX, centerY, radius);
            canvas.StrokeColor = _micIdleStrokeColor;
            canvas.StrokeSize = 2.5f;
            canvas.DrawCircle(centerX, centerY, radius);
        }

        // Draw mic icon inside button
        canvas.FillColor = _textColor;
        float micWidth = 16f;
        float micHeight = 24f;
        float micTop = centerY - micHeight / 2 - 3;
        float micLeft = centerX - micWidth / 2;
        float micRadius = micWidth / 2;

        // Mic body (rounded rect)
        var micPath = new PathF();
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

        // Mic stand (U shape)
        canvas.StrokeColor = _textColor;
        canvas.StrokeSize = 2.5f;
        float standTop = micTop + micHeight + 3;
        float standWidth = micWidth + 8;
        float standLeft = centerX - standWidth / 2;

        var standPath = new PathF();
        standPath.MoveTo(standLeft, micTop + micHeight / 2);
        standPath.LineTo(standLeft, standTop);
        standPath.QuadTo(standLeft, standTop + 8, centerX, standTop + 8);
        standPath.QuadTo(standLeft + standWidth, standTop + 8, standLeft + standWidth, standTop);
        standPath.LineTo(standLeft + standWidth, micTop + micHeight / 2);
        canvas.DrawPath(standPath);

        // Stem
        canvas.DrawLine(centerX, standTop + 8, centerX, standTop + 14);
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
                _overlay.SetupPlatformTouchHandling();
            });
        });

        // Register as singleton. App.cs always renders AppShell directly so
        // exactly one Window/handler/overlay is created; no proxy needed.
        builder.Services.AddSingleton<IOverlayService>(sp =>
            _overlay ?? throw new InvalidOperationException(
                "VoiceOverlay not yet initialized. Ensure UseVoiceOverlay is called before services are resolved."));

        return builder;
    }

    /// <summary>
    /// Gets the current VoiceOverlay instance.
    /// </summary>
    public static VoiceOverlay? GetVoiceOverlay() => _overlay;
}
