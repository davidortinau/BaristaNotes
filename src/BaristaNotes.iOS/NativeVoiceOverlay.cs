using BaristaNotes.Core.Services;
using CoreAnimation;
using CoreGraphics;
using CoreText;
using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class NativeVoiceOverlay(ILogger<NativeVoiceOverlay> logger) : IOverlayService, IDisposable
{
    private VoiceOverlayView? _view;
    private WeakReference<UIWindow>? _window;
    private WeakReference<SliceNavigationController>? _host;
    private bool _micActive, _processing, _savedAccessibility, _accessibilityHidden;
    private int _displayGeneration;
    private int _attachmentGeneration;
    private UILongPressGestureRecognizer? _hold;
    public bool IsVisible { get; private set; }
    public bool IsCollapsed { get; private set; }
    public bool IsMicActive => _micActive;
    public event EventHandler<bool>? VisibilityChanged;
    public event EventHandler? CloseRequested;
    public event EventHandler? ExpandRequested;
    public event EventHandler? MicPressStarted;
    public event EventHandler? MicPressEnded;

    public void Attach(UIWindow window, SliceNavigationController host)
    {
        if (_view != null) throw new InvalidOperationException("Voice overlay already attached.");
        var attachment = ++_attachmentGeneration;
        _window = new(window);
        _host = new(host);
        _view = new VoiceOverlayView(
            WeakUiCallback.Create(this, attachment, static (owner, version) =>
            {
                if (owner._attachmentGeneration == version) owner.Collapse();
            }),
            WeakUiCallback.Create(this, attachment, static (owner, version) =>
            {
                if (owner._attachmentGeneration == version) owner.RequestClose();
            }),
            WeakUiCallback.Create(this, attachment, static (owner, version) =>
            {
                if (owner._attachmentGeneration == version) owner.ExpandRequested?.Invoke(owner, EventArgs.Empty);
            }))
        {
            Frame = window.Bounds, AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
            Hidden = true
        };
        var weak = new WeakReference<NativeVoiceOverlay>(this);
        _hold = new UILongPressGestureRecognizer(gesture =>
        {
            if (!weak.TryGetTarget(out var owner) || owner._attachmentGeneration != attachment || !owner.IsVisible || owner.IsCollapsed) return;
            if (gesture.State == UIGestureRecognizerState.Began) owner.HandleMicPhase(true);
            else if (gesture.State is UIGestureRecognizerState.Ended or UIGestureRecognizerState.Cancelled or UIGestureRecognizerState.Failed)
                owner.HandleMicPhase(false);
        })
        {
            MinimumPressDuration = 0,
            CancelsTouchesInView = false,
            ShouldReceiveTouch = (_, touch) =>
            {
                if (!weak.TryGetTarget(out var owner) || owner._attachmentGeneration != attachment ||
                    !owner.IsVisible || owner.IsCollapsed || owner._processing || owner._view == null) return false;
                return owner._view.MicHitFrame.Contains(touch.LocationInView(owner._view));
            }
        };
        _view.AddGestureRecognizer(_hold);
        window.AddSubview(_view);
    }

    public void Show() => NativeUiThread.Send(() =>
    {
        var view = RequireView();
        _displayGeneration++;
        IsVisible = true;
        IsCollapsed = _micActive = _processing = false;
        view.Hidden = false;
        view.Collapsed = false;
        view.SetPressed(false);
        SetAccessibility(true);
        BringForward();
        VisibilityChanged?.Invoke(this, true);
        logger.LogInformation("Voice overlay shown Ready without arming audio");
    });
    public void Hide() => NativeUiThread.Send(() =>
    {
        if (!IsVisible) return;
        _displayGeneration++;
        IsVisible = IsCollapsed = _micActive = _processing = false;
        RequireView().Hidden = true;
        RequireView().SetPressed(false);
        SetAccessibility(false);
        VisibilityChanged?.Invoke(this, false);
    });
    public void Collapse() => NativeUiThread.Send(() =>
    {
        if (!IsVisible || IsCollapsed) return;
        IsCollapsed = true;
        _micActive = false;
        RequireView().Collapsed = true;
        RequireView().SetPressed(false);
        SetAccessibility(false);
    });
    public void Expand() => NativeUiThread.Send(() =>
    {
        if (!IsVisible || !IsCollapsed) return;
        IsCollapsed = false;
        RequireView().Collapsed = false;
        SetAccessibility(true);
        BringForward();
    });
    public void UpdateContent(OverlayContent content) => NativeUiThread.Send(() =>
    {
        _processing = content.IsProcessing;
        RequireView().Update(content);
    });

    // Shared by the real iOS recognizer and explicitly labeled handler-level tests.
    internal void HandleMicPhase(bool pressed)
    {
        if (!IsVisible || IsCollapsed) return;
        if (pressed)
        {
            if (_processing || _micActive) return;
            _micActive = true;
            RequireView().SetPressed(true);
            MicPressStarted?.Invoke(this, EventArgs.Empty);
        }
        else if (_micActive)
        {
            _micActive = false;
            RequireView().SetPressed(false);
            MicPressEnded?.Invoke(this, EventArgs.Empty);
        }
    }
    private void RequestClose()
    {
        var weak = new WeakReference<NativeVoiceOverlay>(this);
        var generation = _displayGeneration;
        _ = DelayedCloseAsync(weak, generation);
    }
    private static async Task DelayedCloseAsync(WeakReference<NativeVoiceOverlay> weak, int generation)
    {
        await Task.Delay(100);
        NativeUiThread.Send(() =>
        {
            if (weak.TryGetTarget(out var owner) && owner.IsVisible && owner._displayGeneration == generation)
                owner.CloseRequested?.Invoke(owner, EventArgs.Empty);
        });
    }
    private VoiceOverlayView RequireView() => _view ?? throw new InvalidOperationException("Voice window is not attached.");
    private void BringForward()
    {
        if (_window?.TryGetTarget(out var window) == true) window.BringSubviewToFront(RequireView());
    }
    private void SetAccessibility(bool expanded)
    {
        RequireView().AccessibilityViewIsModal = expanded;
        if (_host == null || !_host.TryGetTarget(out var host) || host.View is not { } view) return;
        if (expanded && !_accessibilityHidden)
        {
            _savedAccessibility = view.AccessibilityElementsHidden;
            view.AccessibilityElementsHidden = true;
            _accessibilityHidden = true;
        }
        else if (!expanded && _accessibilityHidden)
        {
            view.AccessibilityElementsHidden = _savedAccessibility;
            _accessibilityHidden = false;
        }
    }
    internal void Detach(SliceNavigationController host)
    {
        if (_host == null || !_host.TryGetTarget(out var attached) || !ReferenceEquals(attached, host)) return;
        DetachCore();
    }
    private void DetachCore()
    {
        if (_view == null) return;
        Hide();
        _attachmentGeneration++;
        _displayGeneration++;
        if (_hold != null) { _view.RemoveGestureRecognizer(_hold); _hold.Dispose(); _hold = null; }
        _view.RemoveFromSuperview();
        _view.Dispose();
        _view = null;
        _host = null;
        _window = null;
        logger.LogInformation("Voice overlay detached from scene window");
    }
    public void Dispose() => NativeUiThread.Send(DetachCore);
}

internal static class VoiceColors
{
    public static UIColor Panel { get; } = UIColor.FromRGB(30, 30, 30);
    public static UIColor Accent { get; } = UIColor.FromRGB(255, 149, 0);
    public static UIColor Secondary { get; } = UIColor.FromRGB(204, 204, 204);
    public static UIColor Response { get; } = UIColor.FromRGB(144, 238, 144);
    public static UIColor Mic { get; } = UIColor.FromRGB(51, 51, 51);
    public static UIColor Muted { get; } = UIColor.FromRGB(136, 136, 136);
    // The pinned Graphics ToCTFont resolves font.Name only; its weight/style
    // parameters do not select bold or italic faces on this source backend.
    public static UIFont Font(nfloat size) =>
        UIFont.FromName("ArialMT", size)
        ?? throw new InvalidOperationException("Source Arial font is unavailable.");
}

internal sealed class VoiceOverlayView : UIView
{
    private readonly UIButton _outside = new(UIButtonType.Custom);
    private readonly UIView _panel = new();
    private readonly UIButton _close = new(UIButtonType.Custom);
    private readonly UIButton _minimize = new(UIButtonType.Custom);
    private readonly VoiceTextLabel _state = new();
    private readonly VoiceTextLabel _activity = new() { Text = "●", TextColor = VoiceColors.Accent, Font = VoiceColors.Font(20) };
    private readonly VoiceTextLabel _transcript = new(true);
    private readonly VoiceTextLabel _response = new(true);
    private readonly VoiceTextLabel _example = new(true) { Text = "Say something like \"Log shot 18 in, 36 out, 28 seconds\"", TextAlignment = UITextAlignment.Center };
    private readonly VoiceTextLabel _hint = new() { Text = "Hold to speak", TextAlignment = UITextAlignment.Center };
    private readonly VoiceTextLabel _closeGlyph = new() { Text = "\u2715", TextAlignment = UITextAlignment.Center, TextColor = UIColor.White, Font = VoiceColors.Font(24) };
    private readonly VoiceTextLabel _minimizeGlyph = new() { Text = "−", TextAlignment = UITextAlignment.Center, TextColor = VoiceColors.Secondary, Font = VoiceColors.Font(22) };
    private readonly UIControl _micHit = new();
    private readonly VoiceMicGraphic _mic = new(false);
    private readonly UIButton _fab = new(UIButtonType.Custom);
    private readonly VoiceMicGraphic _fabGraphic = new(true);
    private OverlayContent _content = new("Ready", "", false, false, true);
    private bool _collapsed;
    public CGRect MicHitFrame => _micHit.Frame;
    public bool Collapsed
    {
        get => _collapsed;
        set { _collapsed = value; ApplyContent(); SetNeedsLayout(); }
    }
    public VoiceOverlayView(Action collapse, Action close, Action expand)
    {
        AccessibilityIdentifier = "voice.overlay";
        BackgroundColor = UIColor.Clear;
        _outside.AccessibilityIdentifier = "voice.outside";
        _outside.AccessibilityLabel = "Collapse voice controls";
        _outside.TouchUpInside += (_, _) => collapse();
        _panel.BackgroundColor = VoiceColors.Panel;
        _panel.AccessibilityIdentifier = "voice.panel";
        _panel.Layer.CornerRadius = 20;
        _panel.Layer.MaskedCorners = CACornerMask.MinXMinYCorner | CACornerMask.MaxXMinYCorner;
        _close.AddSubview(_closeGlyph);
        _close.AccessibilityIdentifier = "voice.close";
        _close.AccessibilityLabel = "End voice session";
        _close.TouchUpInside += (_, _) => close();
        _minimize.AddSubview(_minimizeGlyph);
        _minimize.AccessibilityIdentifier = "voice.minimize";
        _minimize.AccessibilityLabel = "Minimize voice controls";
        _minimize.TouchUpInside += (_, _) => collapse();
        _state.AccessibilityIdentifier = "voice.state";
        _state.Font = VoiceColors.Font(20);
        _state.TextColor = UIColor.White;
        _transcript.AccessibilityIdentifier = "voice.transcript";
        _transcript.Font = VoiceColors.Font(16);
        _transcript.TextColor = VoiceColors.Secondary;
        _response.AccessibilityIdentifier = "voice.response";
        _response.Font = VoiceColors.Font(15);
        _response.TextColor = VoiceColors.Response;
        _example.Font = VoiceColors.Font(14);
        _hint.Font = VoiceColors.Font(13);
        _example.TextColor = _hint.TextColor = VoiceColors.Muted;
        _micHit.AccessibilityIdentifier = "voice.mic";
        _micHit.AccessibilityLabel = "Hold to speak";
        _micHit.AccessibilityTraits = UIAccessibilityTrait.Button;
        _micHit.IsAccessibilityElement = true;
        _micHit.AddSubview(_mic);
        _fab.AccessibilityIdentifier = "voice.fab";
        _fab.AccessibilityLabel = "Expand voice controls";
        _fab.TouchUpInside += (_, _) => expand();
        _fab.AddSubview(_fabGraphic);
        _panel.AddSubviews(_close, _minimize, _state, _activity, _transcript, _response, _example, _hint);
        AddSubviews(_outside, _panel, _micHit, _fab);
        ApplyContent();
    }
    public void Update(OverlayContent content) { _content = content; ApplyContent(); SetNeedsLayout(); }
    public void SetPressed(bool pressed) { _mic.Pressed = pressed; _mic.SetNeedsLayout(); }
    private void ApplyContent()
    {
        _panel.Hidden = _micHit.Hidden = _outside.Hidden = _collapsed;
        _fab.Hidden = !_collapsed;
        _state.Text = _content.StateText;
        _activity.Hidden = !_content.IsListening && !_content.IsProcessing;
        _transcript.Text = _content.Transcript;
        _transcript.Hidden = string.IsNullOrEmpty(_content.Transcript);
        _response.Text = _content.AIResponse;
        _response.Hidden = string.IsNullOrEmpty(_content.AIResponse);
        _example.Hidden = !_transcript.Hidden || !_response.Hidden || !_content.IsReady;
        _hint.Hidden = !_content.IsReady || _content.IsListening || _content.IsProcessing;
        _mic.Listening = _fabGraphic.Listening = _content.IsListening;
        _mic.Processing = _fabGraphic.Processing = _content.IsProcessing;
        _mic.SetNeedsLayout();
        _fabGraphic.SetNeedsLayout();
    }
    public override bool PointInside(CGPoint point, UIEvent? uievent) =>
        !Hidden && (_collapsed ? _fab.Frame.Contains(point) : Bounds.Contains(point));
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var width = Bounds.Width;
        var safeBottom = Window?.SafeAreaInsets.Bottom ?? 0;
        var panelY = Bounds.Height - 420 - safeBottom;
        _outside.Frame = Bounds;
        _panel.Frame = new CGRect(0, panelY, width, 420 + safeBottom);
        _close.Frame = new CGRect(width - 60, 20, 40, 40);
        _minimize.Frame = new CGRect(width - 104, 20, 40, 40);
        _closeGlyph.Frame = _close.Bounds;
        _minimizeGlyph.Frame = _minimize.Bounds;
        _activity.Frame = new CGRect(20, 20, 24, 30);
        _state.Frame = new CGRect(44, 20, width - 148, 30);
        nfloat contentY = 70;
        if (!_transcript.Hidden)
        {
            _transcript.Frame = new CGRect(20, contentY, width - 40, 60);
            contentY += 70;
        }
        _response.Frame = new CGRect(20, contentY, width - 40, (nfloat)Math.Max(40, 275 - contentY));
        _example.Frame = new CGRect(20, contentY, width - 40, 40);
        _micHit.Frame = new CGRect(width / 2 - 55, panelY + 275, 110, 110);
        _mic.Frame = new CGRect(15, 15, 80, 80);
        _hint.Frame = new CGRect(0, 376, width, 20);
        _fab.Frame = new CGRect(width - 72, Bounds.Height - 56 - 16 - safeBottom - 60, 56, 56);
        _fabGraphic.Frame = _fab.Bounds;
    }
}

internal sealed class VoiceTextLabel(bool topAligned = false) : UILabel
{
    public override void DrawText(CGRect rect)
    {
        if (string.IsNullOrEmpty(Text) || rect.Width <= 0 || rect.Height <= 0) return;
        // Native CoreText label drawing, matching the pinned MaciOS text metrics
        // without sharing its canvas renderer or applying Dynamic Type scaling.
        using var font = new CTFont(Font.Name, Font.PointSize);
        using var paragraph = new CTParagraphStyle(new CTParagraphStyleSettings
        {
            Alignment = TextAlignment switch
            {
                UITextAlignment.Center => CTTextAlignment.Center,
                UITextAlignment.Right => CTTextAlignment.Right,
                _ => CTTextAlignment.Left
            }
        });
        using var text = new NSAttributedString(Text, new CTStringAttributes
        {
            Font = font, ForegroundColor = (TextColor ?? UIColor.White).CGColor, ParagraphStyle = paragraph
        });
        using var path = new CGPath();
        path.AddRect(new CGRect(0, 0, rect.Width, rect.Height));
        using var setter = new CTFramesetter(text);
        using var frame = setter.GetFrame(new NSRange(0, 0), path, null)
            ?? throw new InvalidOperationException("CoreText could not lay out voice text.");
        var context = UIGraphics.GetCurrentContext()
            ?? throw new InvalidOperationException("Voice text requires an active drawing context.");
        context.SaveState();
        context.ClipToRect(rect);
        context.TranslateCTM(rect.X, rect.Y + rect.Height);
        context.ScaleCTM(1, -1);
        context.TextMatrix = CGAffineTransform.MakeIdentity();
        if (!topAligned)
        {
            var lines = frame.GetLines();
            var origins = new CGPoint[lines.Length];
            frame.GetLineOrigins(new NSRange(0, 0), origins);
            nfloat minimum = nfloat.MaxValue, maximum = nfloat.MinValue;
            for (var i = 0; i < lines.Length; i++)
            {
                lines[i].GetTypographicBounds(out var ascent, out var descent, out _);
                minimum = (nfloat)Math.Min(minimum, origins[i].Y - ascent);
                maximum = (nfloat)Math.Max(maximum, origins[i].Y + descent);
                lines[i].Dispose();
            }
            if (lines.Length > 0)
                context.TranslateCTM(0, -((rect.Height - maximum + minimum) / 2 - font.DescentMetric / 2));
        }
        frame.Draw(context);
        context.RestoreState();
    }
}

internal sealed class VoiceMicGraphic(bool fab) : UIView
{
    private readonly CAShapeLayer _circle = new(), _ring = new(), _body = new(), _stand = new();
    public bool Pressed, Listening, Processing;
    public override void MovedToSuperview()
    {
        base.MovedToSuperview();
        UserInteractionEnabled = false;
        if (_circle.SuperLayer is null)
            foreach (var layer in new[] { _circle, _ring, _body, _stand }) Layer.AddSublayer(layer);
    }
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        foreach (var layer in new[] { _circle, _ring, _body, _stand }) layer.Frame = Bounds;
        var radius = Bounds.Width / 2;
        var active = Pressed || Listening;
        using var circle = UIBezierPath.FromOval(Bounds);
        _circle.Path = circle.CGPath;
        _circle.FillColor = (fab || active || Processing ? VoiceColors.Accent : VoiceColors.Mic).CGColor;
        _circle.StrokeColor = !fab && !active && !Processing ? VoiceColors.Muted.CGColor : UIColor.Clear.CGColor;
        _circle.LineWidth = 2.5f;
        var ringVisible = fab ? Listening || Processing : active;
        using var ring = UIBezierPath.FromOval(Bounds.Inset(fab ? -4 : -6, fab ? -4 : -6));
        _ring.Path = ring.CGPath;
        _ring.FillColor = UIColor.Clear.CGColor;
        _ring.StrokeColor = (fab ? UIColor.White : VoiceColors.Accent).CGColor;
        _ring.LineWidth = 2;
        _ring.Hidden = !ringVisible;
        nfloat micWidth = fab ? 12 : 16, micHeight = fab ? 18 : 24;
        var top = radius - micHeight / 2 - (fab ? 2 : 3);
        var left = radius - micWidth / 2;
        var corner = micWidth / 2;
        using var body = new CGPath();
        body.MoveToPoint(left, top + corner);
        body.AddLineToPoint(left, top + micHeight - corner);
        body.AddQuadCurveToPoint(left, top + micHeight, left + corner, top + micHeight);
        body.AddLineToPoint(left + micWidth - corner, top + micHeight);
        body.AddQuadCurveToPoint(left + micWidth, top + micHeight, left + micWidth, top + micHeight - corner);
        body.AddLineToPoint(left + micWidth, top + corner);
        body.AddQuadCurveToPoint(left + micWidth, top, left + corner, top);
        body.AddLineToPoint(left + corner, top);
        body.AddQuadCurveToPoint(left, top, left, top + corner);
        body.CloseSubpath();
        _body.Path = body;
        _body.FillColor = UIColor.White.CGColor;
        var standTop = top + micHeight + (fab ? 2 : 3);
        var standWidth = micWidth + (fab ? 6 : 8);
        var standLeft = radius - standWidth / 2;
        var curve = fab ? 6 : 8;
        using var stand = new CGPath();
        stand.MoveToPoint(standLeft, top + micHeight / 2);
        stand.AddLineToPoint(standLeft, standTop);
        stand.AddQuadCurveToPoint(standLeft, standTop + curve, radius, standTop + curve);
        stand.AddQuadCurveToPoint(standLeft + standWidth, standTop + curve, standLeft + standWidth, standTop);
        stand.AddLineToPoint(standLeft + standWidth, top + micHeight / 2);
        stand.MoveToPoint(radius, standTop + curve);
        stand.AddLineToPoint(radius, standTop + curve + 6);
        _stand.Path = stand;
        _stand.FillColor = UIColor.Clear.CGColor;
        _stand.StrokeColor = UIColor.White.CGColor;
        _stand.LineWidth = fab ? 2 : 2.5f;
    }
}
