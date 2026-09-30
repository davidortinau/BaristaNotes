using CoreGraphics;
using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

// Source ActionModal template: actual safe top/sides, bottom opt-out,24 design
// padding, fixed action row. AvoidKeyboard remains the package default false.
internal abstract class PhotoActionModal : SliceViewController
{
    private readonly UIView _panel = new();
    protected PhotoBodyScroll Scroll { get; } = new() { ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never };
    private readonly UIButton _backdrop = new(UIButtonType.Custom);
    private readonly UIButton _close = new(UIButtonType.Custom);
    protected UIButton PrimaryAction { get; } = new(UIButtonType.Custom);
    private readonly UILabel _title = new() { TextAlignment = UITextAlignment.Center };
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private UIView? _underlying;
    private NSObject? _keyboardObserver;
    private NSObject? _fieldStartedObserver;
    private NSObject? _editorStartedObserver;
    private nfloat _keyboardOverlap;
    private bool _priorAccessibility, _appearing = true, _closing, _ended;
    private UIView? _content;
    protected bool IsOpen => !_ended && !_closing;
    protected bool UserCancelled { get; private set; }
    public Task Closed => _closed.Task;
    public Task Opened => _opened.Task;

    protected PhotoActionModal(SliceNavigationController host, string title, string id, bool dismissBackdrop = true) : base(host)
    {
        ModalPresentationStyle = UIModalPresentationStyle.OverFullScreen;
        ModalInPresentation = true;
        _title.Text = title;
        _panel.AccessibilityIdentifier = id + ".panel";
        Scroll.AccessibilityIdentifier = id + ".scroll";
        _close.AccessibilityIdentifier = id + ".close";
        PrimaryAction.AccessibilityIdentifier = id + ".action";
        _backdrop.AccessibilityIdentifier = id + ".backdrop";
        var close = WeakUiCallback.Create(this, static modal => _ = modal.CancelAsync());
        _close.TouchUpInside += (_, _) => close();
        if (dismissBackdrop) _backdrop.TouchUpInside += (_, _) => close();
        var activate = WeakUiCallback.Create(this, static modal => modal.OnAction());
        PrimaryAction.TouchUpInside += (_, _) => activate();
    }
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Root.BackgroundColor = UIColor.Clear;
        Root.AccessibilityViewIsModal = true;
        _backdrop.BackgroundColor = NativeTheme.Backdrop;
        _backdrop.AccessibilityLabel = "Photo modal background";
        _panel.BackgroundColor = NativeTheme.DarkSurface;
        _panel.Layer.CornerRadius = 24;
        _panel.ClipsToBounds = true;
        _panel.Hidden = true;
        _title.Font = SourceScaledText.Font(18, true, TraitCollection);
        _title.TextColor = NativeTheme.OnPrimary;
        _close.SetTitle("\ue14c", UIControlState.Normal);
        _close.AccessibilityLabel = "Close";
        _close.TitleLabel.Font = UIFontMetrics.DefaultMetrics.GetScaledFont(NativeTheme.Icons(24), TraitCollection);
        _close.SetTitleColor(NativeTheme.OnPrimary, UIControlState.Normal);
        PrimaryAction.TitleLabel.Font = PhotoUi.Font(16, TraitCollection);
        PrimaryAction.SetTitleColor(NativeTheme.OnPrimary, UIControlState.Normal);
        PrimaryAction.BackgroundColor = NativeTheme.Primary;
        PrimaryAction.Layer.CornerRadius = 20;
        PrimaryAction.Hidden = true;
        Scroll.KeyboardDismissMode = UIScrollViewKeyboardDismissMode.OnDrag;
        _panel.AddSubviews(_title, _close, Scroll, PrimaryAction);
        Root.AddSubviews(_backdrop, _panel);
        if (_content != null) Scroll.AddSubview(_content);
        SetInteractive(false);
    }
    protected void SetContent(UIView content)
    {
        _content?.RemoveFromSuperview();
        _content = content;
        if (IsViewLoaded) Scroll.AddSubview(content);
        Relayout();
    }
    protected void SetPrimary(string text, bool visible, bool enabled)
    {
        PrimaryAction.SetTitle(text, UIControlState.Normal);
        PrimaryAction.AccessibilityLabel = text;
        PrimaryAction.Hidden = !visible;
        PrimaryAction.Enabled = enabled;
        Relayout();
    }
    protected void Relayout() { if (IsViewLoaded && !_ended) Root.SetNeedsLayout(); }
    protected virtual void OnAction() { }
    protected virtual void OnCancelled() { }
    protected virtual void OnClosed() { }
    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        RegisterKeyboardObservers();
        if (_underlying == null && Host.View is { } view)
        {
            _priorAccessibility = view.AccessibilityElementsHidden;
            _underlying = view;
            view.AccessibilityElementsHidden = true;
        }
    }
    public override void ViewDidDisappear(bool animated)
    {
        DisposeKeyboardObservers();
        _keyboardOverlap = 0;
        base.ViewDidDisappear(animated);
        // A camera presented above this modal does not close it.
        if (PresentingViewController == null) FinishClosed();
    }
    public override void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        if (_appearing) _ = AppearAsync();
    }
    private async Task AppearAsync()
    {
        try
        {
            Root.LayoutIfNeeded();
            _appearing = false;
            await AnimateAsync(true);
            if (_ended) return;
            SetInteractive(true);
            _opened.TrySetResult();
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Photo ActionModal appearance failed");
            _opened.TrySetException(error);
            Retire();
        }
    }
    public Task CancelAsync()
    {
        if (!_closing && !_ended) { UserCancelled = true; OnCancelled(); }
        return CloseAsync();
    }
    public async Task CloseAsync()
    {
        if (_closing || _ended) { await _closed.Task; return; }
        _closing = true;
        SetInteractive(false);
        Root.EndEditing(true);
        try
        {
            await AnimateAsync(false);
            if (!_ended)
            {
                if (PresentingViewController == null) FinishClosed();
                else DismissViewController(false, FinishClosed);
            }
            await _closed.Task;
        }
        catch
        {
            TearDown();
            throw;
        }
    }
    public void Retire()
    {
        if (_ended) return;
        UserCancelled = true;
        TearDown();
    }
    private void TearDown()
    {
        if (_ended) return;
        // Fault cleanup must not suppress the active owner's completion error.
        OnCancelled();
        if (IsViewLoaded) _panel.Layer.RemoveAllAnimations();
        if (PresentingViewController != null) DismissViewController(false, null);
        FinishClosed();
    }
    private void SetInteractive(bool enabled)
    {
        _panel.UserInteractionEnabled = enabled;
        _backdrop.Enabled = enabled;
        _close.Enabled = enabled;
        Scroll.ScrollEnabled = enabled;
    }
    private void FinishClosed()
    {
        if (_ended) return;
        _ended = true;
        if (_underlying != null) _underlying.AccessibilityElementsHidden = _priorAccessibility;
        _underlying = null;
        _opened.TrySetCanceled();
        try { OnClosed(); }
        finally { _closed.TrySetResult(); }
    }

    private void RegisterKeyboardObservers()
    {
        DisposeKeyboardObservers();
        var weak = new WeakReference<PhotoActionModal>(this);
        _keyboardObserver = UIKeyboard.Notifications.ObserveWillChangeFrame((_, args) =>
        {
            if (!weak.TryGetTarget(out var modal) || modal._ended) return;
            var frame = modal.Root.ConvertRectFromView(args.FrameEnd, null);
            modal._keyboardOverlap = frame.Bottom >= modal.Root.Bounds.Bottom - 1
                ? (nfloat)Math.Max(0, modal.Root.Bounds.Bottom - frame.Y)
                : 0;
            modal.Root.SetNeedsLayout();
            modal.Root.LayoutIfNeeded();
            modal.ScrollActiveControlIntoView();
        });
        _fieldStartedObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            UITextField.TextDidBeginEditingNotification,
            notification =>
            {
                if (weak.TryGetTarget(out var modal) && notification.Object is UIView view)
                    modal.ScrollControlIntoView(view);
            });
        _editorStartedObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            UITextView.TextDidBeginEditingNotification,
            notification =>
            {
                if (weak.TryGetTarget(out var modal) && notification.Object is UIView view)
                    modal.ScrollControlIntoView(view);
            });
    }

    private void DisposeKeyboardObservers()
    {
        _keyboardObserver?.Dispose();
        _fieldStartedObserver?.Dispose();
        _editorStartedObserver?.Dispose();
        _keyboardObserver = _fieldStartedObserver = _editorStartedObserver = null;
    }

    private void ScrollActiveControlIntoView()
    {
        if (FindFirstResponder(Scroll) is { } active)
            ScrollControlIntoView(active);
    }

    private void ScrollControlIntoView(UIView control)
    {
        if (_ended || !IsInsideScroll(control)) return;
        BeginInvokeOnMainThread(() =>
        {
            if (_ended || !IsInsideScroll(control)) return;
            Root.LayoutIfNeeded();
            var rect = control.ConvertRectToCoordinateSpace(control.Bounds, Scroll);
            nfloat padding = 12;
            var visibleTop = Scroll.ContentOffset.Y;
            var visibleBottom = visibleTop + Scroll.Bounds.Height;
            nfloat target = visibleTop;
            if (rect.Y - padding < visibleTop)
                target = rect.Y - padding;
            else if (rect.Bottom + padding > visibleBottom)
                target = rect.Bottom + padding - Scroll.Bounds.Height;
            var maximum = (nfloat)Math.Max(0, Scroll.ContentSize.Height - Scroll.Bounds.Height);
            target = (nfloat)Math.Clamp(target, 0, maximum);
            if (Math.Abs(target - Scroll.ContentOffset.Y) > .5)
                Scroll.SetContentOffset(new CGPoint(Scroll.ContentOffset.X, target), true);
        });
    }

    private bool IsInsideScroll(UIView view)
    {
        for (UIView? current = view; current != null; current = current.Superview)
            if (ReferenceEquals(current, Scroll)) return true;
        return false;
    }

    private static UIView? FindFirstResponder(UIView view)
    {
        if (view.IsFirstResponder) return view;
        foreach (var child in view.Subviews)
            if (FindFirstResponder(child) is { } responder) return responder;
        return null;
    }

    internal sealed class PhotoBodyScroll : UIScrollView
    {
        public bool AllowsHorizontalBleed { get; set; }
        public override bool PointInside(CGPoint point, UIEvent? uievent) =>
            AllowsHorizontalBleed ? Bounds.Inset(-20, 0).Contains(point) : base.PointInside(point, uievent);
    }
    private async Task AnimateAsync(bool entering)
    {
        var duration = entering ? .3 : .4;
        var steps = (int)Math.Ceiling(duration / .016);
        var distance = (Root.Bounds.Height + _panel.Bounds.Height) / 2;
        var complete = await UIView.AnimateKeyframesAsync(duration, 0, UIViewKeyframeAnimationOptions.CalculationModeLinear, () =>
        {
            for (var i = 1; i <= steps; i++)
            {
                var t = i / (double)steps;
                var fraction = entering ? 1 - (Math.Pow(t - 1, 3) + 1) : t * t * t;
                var offset = distance * (nfloat)fraction;
                UIView.AddKeyframeWithRelativeStartTime((i - 1) / (double)steps, 1d / steps,
                    () => _panel.Transform = CGAffineTransform.MakeTranslation(0, offset));
            }
        });
        if (!complete && !_ended) throw new InvalidOperationException("Photo modal animation interrupted.");
    }
    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var safe = Root.SafeAreaInsets;
        var width = Root.Bounds.Width - safe.Left - safe.Right;
        var height = (nfloat)Math.Max(1, Root.Bounds.Height - safe.Top - _keyboardOverlap);
        _backdrop.Frame = Root.Bounds;
        _panel.Bounds = new CGRect(0, 0, width, height);
        _panel.Center = new CGPoint(safe.Left + width / 2, safe.Top + height / 2);
        var title = SliceUi.Measure(_title, width - 48);
        var close = SliceUi.Measure(_close.TitleLabel, 100);
        _title.Frame = new CGRect(24, 24, width - 48, title.Height);
        _close.Frame = new CGRect(width - 34 - close.Width, 20 - (44 - close.Height) / 2, close.Width + 20, 44);
        var actionHeight = PrimaryAction.Hidden ? 0 : (nfloat)Math.Max(54, PrimaryAction.TitleLabel.Font.LineHeight + 8);
        PrimaryAction.Frame = new CGRect(24, height - 24 - actionHeight, width - 48, actionHeight);
        var top = 24 + (nfloat)Math.Max(title.Height, close.Height - 4) + 16;
        Scroll.Frame = new CGRect(24, top, width - 48, (nfloat)Math.Max(0, height - 24 - actionHeight - 16 - top));
        if (_content != null)
        {
            var size = _content.SizeThatFits(new CGSize(Scroll.Bounds.Width, nfloat.MaxValue));
            _content.Frame = new CGRect(0, 0, Scroll.Bounds.Width, size.Height);
            Scroll.ContentSize = _content.Bounds.Size;
        }
        if (_appearing)
        {
            _panel.Transform = CGAffineTransform.MakeTranslation(0, (Root.Bounds.Height + height) / 2);
            _panel.Hidden = false;
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!_ended) Retire();
            DisposeKeyboardObservers();
            _panel.Layer.RemoveAllAnimations();
        }
        base.Dispose(disposing);
    }
}
