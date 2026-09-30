namespace BaristaNotes.AndroidApp.Views;

// Only RequestCancellation runs off-thread. All state transitions and native
// access are serialized on the animation's original UI thread.
internal sealed class UiAnimationLifetime(
    SynchronizationContext uiContext,
    CancellationToken cancellation,
    Action cancelNative,
    Action releaseNative) : IDisposable
{
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenRegistration _registration;
    private Action? _cancelNative = cancelNative;
    private Action? _releaseNative = releaseNative;
    private bool _registered;
    private bool _settled;
    private bool _disposed;

    public Task Completion => _completion.Task;
    public bool IsActive => !_settled && !_disposed && !cancellation.IsCancellationRequested;

    public void RegisterCancellation()
    {
        VerifyOwnerThread();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_registered)
            throw new InvalidOperationException("Animation cancellation is already registered.");
        _registered = true;
        _registration = cancellation.Register(static state =>
            ((UiAnimationLifetime)state!).RequestCancellation(), this);
    }

    public void Complete()
    {
        VerifyOwnerThread();
        if (_settled || _disposed)
            return;
        _settled = true;
        if (cancellation.IsCancellationRequested)
            _completion.TrySetCanceled(cancellation);
        else
            _completion.TrySetResult();
    }

    private void RequestCancellation() =>
        uiContext.Post(static state => ((UiAnimationLifetime)state!).CancelOnOwnerThread(), this);

    private void CancelOnOwnerThread()
    {
        VerifyOwnerThread();
        if (_settled || _disposed)
            return;

        // Cancel can synchronously raise AnimationEnd. Claim the outcome first.
        _settled = true;
        try
        {
            _cancelNative?.Invoke();
            _completion.TrySetCanceled(cancellation);
        }
        catch (Exception exception)
        {
            _completion.TrySetException(exception);
        }
    }

    public void Dispose()
    {
        VerifyOwnerThread();
        if (_disposed)
            return;

        _disposed = true;
        _settled = true;
        _cancelNative = null;
        var release = _releaseNative;
        _releaseNative = null;
        _registration.Dispose();
        try
        {
            release?.Invoke();
        }
        finally
        {
            _completion.TrySetCanceled(cancellation);
        }
    }

    private void VerifyOwnerThread()
    {
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
            throw new InvalidOperationException("Native animation lifetime must stay on its owning UI thread.");
    }
}
