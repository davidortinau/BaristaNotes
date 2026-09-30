using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using BaristaNotes.AndroidApp.Views;

namespace BaristaNotes.Tests.Unit;

public sealed class UiAnimationLifetimeTests
{
    [Fact]
    public void EndThenCancellation_DoesNotUseAnimatorAfterQueuedCleanup()
    {
        for (var iteration = 0; iteration < 100; iteration++)
        {
            using var ui = new QueuedUiContext();
            using var cancellation = new CancellationTokenSource();
            var native = new FakeAnimator();
            using var lifetime = new UiAnimationLifetime(ui, cancellation.Token, native.Cancel, native.Release);
            lifetime.RegisterCancellation();
            var observed = ObserveAsync(lifetime, cancellation.Token);

            lifetime.Complete();
            Assert.Equal(1, ui.Count);
            cancellation.Cancel();
            Assert.Equal(2, ui.Count);
            ui.RunNext();
            Assert.Equal(1, native.ReleaseCount);
            AssertCanceled(observed);
            ui.RunNext();

            Assert.Equal(0, native.CancelCount);
            Assert.Equal(0, native.AccessAfterDispose);
            Assert.Equal(0, ui.Count);
        }
    }

    [Fact]
    public void CancellationBeforeEnd_HandlesSynchronousReentrantEnd()
    {
        using var ui = new QueuedUiContext();
        using var cancellation = new CancellationTokenSource();
        var native = new FakeAnimator();
        using var lifetime = new UiAnimationLifetime(ui, cancellation.Token, native.Cancel, native.Release);
        native.OnCancel = lifetime.Complete;
        lifetime.RegisterCancellation();
        var observed = ObserveAsync(lifetime, cancellation.Token);

        cancellation.Cancel();
        Assert.False(lifetime.IsActive);
        ui.RunNext();
        ui.RunNext();
        AssertCanceled(observed);
        lifetime.Complete();
        lifetime.Dispose();

        Assert.Equal(1, native.CancelCount);
        Assert.Equal(1, native.ReleaseCount);
        Assert.Equal(0, ui.Count);
    }

    [Fact]
    public void WorkerCancellation_PostsNativeAccessToTheOwner()
    {
        using var ui = new QueuedUiContext();
        using var cancellation = new CancellationTokenSource();
        var native = new FakeAnimator();
        using var lifetime = new UiAnimationLifetime(ui, cancellation.Token, native.Cancel, native.Release);
        lifetime.RegisterCancellation();
        var observed = ObserveAsync(lifetime, cancellation.Token);

        RunOnOtherThread(cancellation.Cancel);
        Assert.Equal(0, native.CancelCount);
        ui.RunNext();
        ui.RunNext();

        AssertCanceled(observed);
        Assert.Equal(1, native.CancelCount);
        Assert.Equal(1, native.ReleaseCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DisposeBeforePostedCancellation_MakesTheCallbackInert(bool alreadyCanceled)
    {
        using var ui = new QueuedUiContext();
        using var cancellation = new CancellationTokenSource();
        if (alreadyCanceled)
            cancellation.Cancel();
        var native = new FakeAnimator();
        using var lifetime = new UiAnimationLifetime(ui, cancellation.Token, native.Cancel, native.Release);
        lifetime.RegisterCancellation();
        if (!alreadyCanceled)
            cancellation.Cancel();

        lifetime.Dispose();
        ui.RunNext();
        lifetime.Complete();

        AssertCanceled(lifetime.Completion);
        Assert.Equal(0, native.CancelCount);
        Assert.Equal(1, native.ReleaseCount);
        Assert.Equal(0, native.AccessAfterDispose);
    }

    [Fact]
    public void NativeCancelFailure_IsAVisibleFaultAndStillReleasesOnce()
    {
        using var ui = new QueuedUiContext();
        using var cancellation = new CancellationTokenSource();
        var native = new FakeAnimator();
        using var lifetime = new UiAnimationLifetime(ui, cancellation.Token, () =>
        {
            native.Cancel();
            throw new InvalidOperationException("Controlled native cancellation failure");
        }, native.Release);
        lifetime.RegisterCancellation();
        var observed = ObserveAsync(lifetime, cancellation.Token);

        cancellation.Cancel();
        ui.RunNext();
        ui.RunNext();

        Assert.True(observed.IsFaulted);
        Assert.IsType<InvalidOperationException>(observed.Exception!.InnerException);
        Assert.Equal(1, native.ReleaseCount);
    }

    [Fact]
    public void CompletionAndDisposal_AreIdempotent()
    {
        using var ui = new QueuedUiContext();
        using var cancellation = new CancellationTokenSource();
        var native = new FakeAnimator();
        using var lifetime = new UiAnimationLifetime(ui, cancellation.Token, native.Cancel, native.Release);
        lifetime.RegisterCancellation();
        var observed = ObserveAsync(lifetime, cancellation.Token);

        lifetime.Complete();
        lifetime.Complete();
        ui.RunNext();
        Assert.True(observed.IsCompletedSuccessfully);
        lifetime.Dispose();
        cancellation.Cancel();
        lifetime.Complete();

        Assert.Equal(0, native.CancelCount);
        Assert.Equal(1, native.ReleaseCount);
        Assert.Equal(0, ui.Count);
    }

    [Fact]
    public void ForeignThreadMutations_AreRejected()
    {
        using var ui = new QueuedUiContext();
        var native = new FakeAnimator();
        using var lifetime = new UiAnimationLifetime(ui, CancellationToken.None, native.Cancel, native.Release);

        RunOnOtherThread(() =>
        {
            Assert.Throws<InvalidOperationException>(lifetime.RegisterCancellation);
            Assert.Throws<InvalidOperationException>(lifetime.Complete);
            Assert.Throws<InvalidOperationException>(lifetime.Dispose);
        });

        Assert.Equal(0, native.ReleaseCount);
        lifetime.Dispose();
        Assert.Equal(1, native.ReleaseCount);
    }

    private static async Task ObserveAsync(UiAnimationLifetime lifetime, CancellationToken token)
    {
        try
        {
            await lifetime.Completion;
            token.ThrowIfCancellationRequested();
        }
        finally
        {
            lifetime.Dispose();
        }
    }

    private static void AssertCanceled(Task task)
    {
        Assert.True(task.IsCanceled);
        Assert.True(task.IsCompleted);
    }

    private static void RunOnOtherThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        })
        {
            IsBackground = true
        };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "The worker-thread probe did not complete.");
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class QueuedUiContext : SynchronizationContext, IDisposable
    {
        private readonly SynchronizationContext? _previous = Current;
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();

        public QueuedUiContext() => SetSynchronizationContext(this);

        public int Count => _queue.Count;

        public override void Post(SendOrPostCallback callback, object? state) => _queue.Add((callback, state));

        public void RunNext()
        {
            Assert.True(_queue.TryTake(out var work, TimeSpan.FromSeconds(5)), "Expected a queued UI callback.");
            work.Callback(work.State);
        }

        public void Dispose()
        {
            SetSynchronizationContext(_previous);
            _queue.Dispose();
        }
    }

    private sealed class FakeAnimator
    {
        private readonly int _ownerThread = Environment.CurrentManagedThreadId;
        private bool _disposed;
        public int CancelCount { get; private set; }
        public int ReleaseCount { get; private set; }
        public int AccessAfterDispose { get; private set; }
        public Action? OnCancel { get; set; }

        public void Cancel()
        {
            Assert.Equal(_ownerThread, Environment.CurrentManagedThreadId);
            if (_disposed)
            {
                AccessAfterDispose++;
                throw new ObjectDisposedException(nameof(FakeAnimator));
            }
            CancelCount++;
            OnCancel?.Invoke();
        }

        public void Release()
        {
            Assert.Equal(_ownerThread, Environment.CurrentManagedThreadId);
            Assert.False(_disposed, "The native animator must only be released once.");
            _disposed = true;
            ReleaseCount++;
        }
    }
}
