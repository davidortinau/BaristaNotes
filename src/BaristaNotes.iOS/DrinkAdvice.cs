using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed partial class DrinkViewController
{
    private AdviceWorkflow? _adviceRequest;
    private AdviceLoadingBar? _adviceBar;
    private bool _adviceVisible;
    private int _adviceVersion;

    private bool IsCurrentAdvice(int version) => _adviceVisible && version == _adviceVersion
        && _adviceRequest is not null
        && Host.TopViewController == this && Host.PresentedViewController == null;

    private async Task RequestAdviceAsync()
    {
        if (!_editingId.HasValue || !_loaded || _adviceRequest is not null
            || !_adviceVisible || Host.TopViewController != this
            || Host.FeedbackHost.IsShowing) return;
        var id = _editingId.Value;
        var version = ++_adviceVersion;
        var workflowHost = new NativeAdviceWorkflowHost(this, version);
        using var request = new AdviceWorkflow(
            workflowHost,
            Services.Singleton<IAIAdviceService>(),
            Logger);
        _adviceRequest = request;
        try
        {
            await request.RunAsync(id, CancellationToken.None);
        }
        finally
        {
            if (ReferenceEquals(_adviceRequest, request))
            {
                _adviceRequest = null;
                RemoveAdviceBar();
            }
        }
    }
    private void LayoutAdviceBar(nfloat y)
    {
        if (_adviceBar != null) _adviceBar.Frame = new CGRect(1, y, Root.Bounds.Width - 2, 4);
    }
    private void RemoveAdviceBar()
    {
        var bar = _adviceBar;
        _adviceBar = null;
        if (bar == null) return;
        bar.Stop();
        bar.RemoveFromSuperview();
        bar.Dispose();
    }
    private void CancelAdvice()
    {
        _adviceVisible = false;
        _adviceVersion++;
        var request = _adviceRequest;
        _adviceRequest = null;
        try { request?.Dispose(); }
        catch (Exception error) { Logger.LogError(error, "Advice cancellation callback failed during page departure"); }
        finally
        {
            RemoveAdviceBar();
        }
    }
    public override void ViewWillDisappear(bool animated)
    {
        _recipeVersion++;
        CancelAdvice();
        base.ViewWillDisappear(animated);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _recipeVersion++;
            CancelAdvice();
            foreach (var tile in _tiles) tile.Dispose();
            _tiles.Clear();
            _people.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class NativeAdviceWorkflowHost(
        DrinkViewController owner,
        int version) : IAdviceWorkflowHost
    {
        public bool IsCurrent => owner.IsCurrentAdvice(version);

        public void SetLoading(bool loading)
        {
            if (!loading)
            {
                owner.RemoveAdviceBar();
                return;
            }
            if (!IsCurrent || owner._adviceBar is not null)
                return;
            owner._adviceBar = new AdviceLoadingBar();
            owner.Root.AddSubview(owner._adviceBar);
            owner.Root.SetNeedsLayout();
        }

        public Task PresentAsync(
            AIAdviceResponseDto response,
            CancellationToken cancellation)
        {
            if (IsCurrent)
            {
                owner.Host.PresentViewController(
                    new AdviceViewController(owner.Host, response),
                    false,
                    null);
            }
            return Task.CompletedTask;
        }

        public void ShowError(string title, string detail)
        {
            if (IsCurrent)
                owner.ShowFeedback(title, isError: true);
        }
    }
}

internal sealed class AdviceLoadingBar : UIView
{
    private readonly CAGradientLayer _gradient = new();
    public AdviceLoadingBar()
    {
        AccessibilityIdentifier = "advice.loading";
        AccessibilityLabel = "Loading AI advice";
        UserInteractionEnabled = false;
        ClipsToBounds = true;
        _gradient.Colors = [UIColor.Clear.CGColor, NativeTheme.Primary.CGColor, UIColor.Clear.CGColor];
        _gradient.Locations = [NSNumber.FromDouble(0), NSNumber.FromDouble(.5), NSNumber.FromDouble(1)];
        _gradient.StartPoint = new CGPoint(0, .5);
        _gradient.EndPoint = new CGPoint(1, .5);
        _gradient.Frame = new CGRect(-120, 0, 120, 4);
        Layer.AddSublayer(_gradient);
        using var animation = CABasicAnimation.FromKeyPath("transform.translation.x");
        animation.From = NSNumber.FromDouble(0);
        animation.To = NSNumber.FromDouble(520);
        animation.Duration = 1;
        animation.RepeatCount = float.PositiveInfinity;
        animation.TimingFunction = CAMediaTimingFunction.FromName(CAMediaTimingFunction.Linear);
        _gradient.AddAnimation(animation, "source-advice-sweep");
    }
    public void Stop() => _gradient.RemoveAllAnimations();
    protected override void Dispose(bool disposing)
    {
        if (disposing) { Stop(); _gradient.RemoveFromSuperLayer(); _gradient.Dispose(); }
        base.Dispose(disposing);
    }
}
