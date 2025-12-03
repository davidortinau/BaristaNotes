using MauiReactor;
using BaristaNotes.Models;
using BaristaNotes.Services;
using System.Reactive.Linq;

namespace BaristaNotes.Components.Feedback;

class FeedbackOverlayState
{
    public List<FeedbackMessage> ActiveMessages { get; set; } = new();
    public bool IsLoading { get; set; }
    public string? LoadingMessage { get; set; }
}

partial class FeedbackOverlay : Component<FeedbackOverlayState>
{
    [Inject]
    IFeedbackService? _feedbackService;

    IDisposable? _feedbackSubscription;
    IDisposable? _loadingSubscription;

    protected override void OnMounted()
    {
        if (_feedbackService == null) return;

        // Subscribe to feedback messages
        _feedbackSubscription = _feedbackService.FeedbackMessages
            .ObserveOn(SynchronizationContext.Current!)
            .Subscribe(message =>
            {
                SetState(s =>
                {
                    s.ActiveMessages.Add(message);
                    
                    // Limit to max 3 messages
                    if (s.ActiveMessages.Count > 3)
                    {
                        s.ActiveMessages.RemoveAt(0);
                    }
                });
            });

        // Subscribe to loading state
        _loadingSubscription = _feedbackService.LoadingState
            .ObserveOn(SynchronizationContext.Current!)
            .Subscribe(state =>
            {
                SetState(s =>
                {
                    s.IsLoading = state.IsLoading;
                    s.LoadingMessage = state.Message;
                });
            });

        base.OnMounted();
    }

    protected override void OnWillUnmount()
    {
        _feedbackSubscription?.Dispose();
        _loadingSubscription?.Dispose();
        base.OnWillUnmount();
    }

    void OnToastDismiss(Guid messageId)
    {
        SetState(s =>
        {
            var message = s.ActiveMessages.FirstOrDefault(m => m.Id == messageId);
            if (message != null)
            {
                s.ActiveMessages.Remove(message);
            }
        });
    }

    public override VisualNode Render()
    {
        return new Grid
        {
            // Toast messages at top
            new VStack(spacing: 8)
            {
                State.ActiveMessages.Select(msg =>
                    new ToastComponent()
                        .Message(msg)
                        .OnDismiss(OnToastDismiss)
                ).ToArray()
            }
            .VStart()
            .HCenter(),

            // Loading overlay (fullscreen)
            State.IsLoading
                ? new LoadingOverlay()
                    .Message(State.LoadingMessage ?? "Loading...")
                : null
        };
    }
}
