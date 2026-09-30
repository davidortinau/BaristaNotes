using BaristaNotes.Core.Services.DTOs;
using Foundation;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed partial class DrinkViewController
{
    private PhotoWorkflowSession? _photoSession;
    private PhotoProcessingView? _photoProcessing;
    private readonly List<(UIView View, bool Interactive, bool AccessibilityHidden)> _photoInputState = [];

    private void OpenPhoto()
    {
        if (_editingId.HasValue || !_loaded || _busy || _photoSession != null ||
            !Host.IsSceneAttached || Host.TopViewController != this || Host.PresentedViewController != null || Host.FeedbackHost.IsShowing) return;
        var session = new PhotoWorkflowSession(this, Host);
        _photoSession = session;
        _ = session.RunAsync();
    }
    internal bool OwnsPhotoSession(PhotoWorkflowSession session) => ReferenceEquals(_photoSession, session);
    internal void PhotoSessionFinished(PhotoWorkflowSession session)
    {
        if (!OwnsPhotoSession(session)) return;
        SetPhotoProcessing(session, false);
        _photoSession = null;
    }
    internal void SelectPhotoBag(PhotoWorkflowSession session, BagSummaryDto bag)
    {
        if (!OwnsPhotoSession(session) || !session.IsCurrent) return;
        _draft.AvailableBags.RemoveAll(existing => existing.Id == bag.Id);
        _draft.AvailableBags.Insert(0, bag);
        _draft.SelectedBagId = bag.Id;
        _draft.BeanName = bag.BeanName;
        UpdateTiles();
    }
    internal void SetPhotoProcessing(PhotoWorkflowSession session, bool processing)
    {
        if (!OwnsPhotoSession(session)) return;
        if (!processing)
        {
            foreach (var state in _photoInputState)
            {
                state.View.UserInteractionEnabled = state.Interactive;
                state.View.AccessibilityElementsHidden = state.AccessibilityHidden;
            }
            _photoInputState.Clear();
            _photoProcessing?.RemoveFromSuperview();
            _photoProcessing?.Dispose();
            _photoProcessing = null;
            return;
        }
        if (_photoProcessing != null || !Host.IsSceneAttached || !IsViewLoaded) return;
        foreach (var view in Root.Subviews)
        {
            _photoInputState.Add((view, view.UserInteractionEnabled, view.AccessibilityElementsHidden));
            view.UserInteractionEnabled = false;
            view.AccessibilityElementsHidden = true;
        }
        _photoProcessing = new PhotoProcessingView { Frame = Root.Bounds,
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight };
        Root.AddSubview(_photoProcessing);
        UIAccessibility.PostNotification(UIAccessibilityPostNotification.Announcement, new NSString("Reviewing photo. Please wait."));
    }
    internal void RetirePhoto()
    {
        var session = _photoSession;
        if (session == null) return;
        session.Retire();
        SetPhotoProcessing(session, false);
        _photoSession = null;
    }
    public override void DidMoveToParentViewController(UIViewController? parent)
    {
        base.DidMoveToParentViewController(parent);
        if (parent == null) RetirePhoto();
    }
}
