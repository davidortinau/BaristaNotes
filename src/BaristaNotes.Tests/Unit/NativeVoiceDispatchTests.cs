using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BaristaNotes.Tests.Unit;

public sealed class NativeVoiceDispatchTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task MainThreadPartial_BeforeWaitingStopOrClose_IsProcessedOnce(
        bool hasPriorPartial, bool close)
    {
        var commands = await RunSessionAsync(hasPriorPartial, close, offMainThread: false);

        Assert.Equal(hasPriorPartial ? "first last" : "last", Assert.Single(commands));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OffMainThreadPartial_QueuedAfterStop_DoesNotEnterStoppedGeneration(
        bool hasPriorPartial)
    {
        var commands = await RunSessionAsync(hasPriorPartial, close: false, offMainThread: true);

        if (hasPriorPartial)
            Assert.Equal("first", Assert.Single(commands));
        else
            Assert.Empty(commands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AlwaysQueuedControl_LosesAlreadyReceivedMainThreadPartial(bool hasPriorPartial)
    {
        var commands = await RunSessionAsync(
            hasPriorPartial, close: false, offMainThread: false, alwaysQueue: true);

        if (hasPriorPartial)
            Assert.Equal("first", Assert.Single(commands));
        else
            Assert.Empty(commands);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Dispatch_OnlyQueuesOffMainThread(bool isMainThread)
    {
        var queued = new Queue<Action>();
        var calls = 0;

        VoiceStateDispatch.Invoke(isMainThread, queued.Enqueue, () => calls++);

        Assert.Equal(isMainThread ? 1 : 0, calls);
        Assert.Equal(isMainThread ? 0 : 1, queued.Count);
        while (queued.TryDequeue(out var action))
            action();
        Assert.Equal(1, calls);
    }

    private static async Task<IReadOnlyList<string>> RunSessionAsync(
        bool hasPriorPartial, bool close, bool offMainThread, bool alwaysQueue = false)
    {
        var loop = new MainLoop();
        var speech = new Mock<ISpeechRecognitionService>(MockBehavior.Strict);
        var engine = new Mock<IVoiceCommandService>(MockBehavior.Strict);
        var overlay = new Mock<IOverlayService>(MockBehavior.Strict);
        var scope = new Mock<IDisposable>(MockBehavior.Strict);
        var commands = new List<string>();
        var listening = new TaskCompletionSource<SpeechRecognitionResultDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration cancellation = default;
        var ownerReleases = 0;

        speech.Setup(service => service.RequestPermissionsAsync()).ReturnsAsync(true);
        speech.Setup(service => service.StartListeningAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) =>
            {
                cancellation = token.Register(() =>
                    listening.TrySetResult(new() { ErrorMessage = "Cancelled" }));
                return listening.Task;
            });
        speech.Setup(service => service.StopListeningAsync()).Returns(() =>
        {
            listening.TrySetResult(new() { Success = true, Transcript = "late final" });
            return Task.CompletedTask;
        });
        engine.Setup(service => service.ClearConversationHistory());
        engine.Setup(service => service.ProcessCommandAsync(
                It.IsAny<VoiceCommandRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<VoiceCommandRequestDto, CancellationToken>((request, token) =>
            {
                Assert.Equal(CancellationToken.None, token);
                commands.Add(request.Transcript);
            })
            .ReturnsAsync(new VoiceToolResultDto { Success = true, Message = "Test response" });
        overlay.Setup(service => service.Show());
        overlay.Setup(service => service.Hide());
        overlay.Setup(service => service.UpdateContent(It.IsAny<OverlayContent>()));
        scope.Setup(service => service.Dispose());
        Action<Action> dispatch = alwaysQueue ? loop.Post : loop.Dispatch;
        using var session = new VoiceSessionWorkflow(
            1,
            speech.Object,
            engine.Object,
            overlay.Object,
            () =>
            {
                scope.Object.Dispose();
                return ValueTask.CompletedTask;
            },
            dispatch,
            _ => Task.FromException(
                new InvalidOperationException("Unexpected permission denial")),
            () => ownerReleases++,
            NullLogger.Instance,
            delay: (_, token) => Task.Delay(Timeout.InfiniteTimeSpan, token));
        try
        {
            loop.Main(() =>
            {
                session.Open();
                overlay.Raise(service => service.MicPressStarted += null, EventArgs.Empty);
            });
            Assert.True(session.IsRecording);
            if (hasPriorPartial)
            {
                loop.Main(() => speech.Raise(
                    service => service.PartialResultReceived += null, speech.Object, "first"));
                loop.Drain();
            }

            loop.Post(() =>
            {
                if (close)
                    overlay.Raise(service => service.CloseRequested += null, EventArgs.Empty);
                else
                    overlay.Raise(service => service.MicPressEnded += null, EventArgs.Empty);
            });
            void LastPartial() => speech.Raise(
                service => service.PartialResultReceived += null, speech.Object, "last");
            if (offMainThread)
                LastPartial();
            else
                loop.Main(LastPartial);

            if (!alwaysQueue && !offMainThread)
                Assert.Equal(hasPriorPartial ? "first last" : "last", session.Transcript);
            loop.Drain();
            Task closeTask = Task.CompletedTask;
            loop.Main(() => closeTask = session.CloseAsync());
            loop.Drain();
            await closeTask.WaitAsync(TimeSpan.FromSeconds(5));
            await session.Completion.WaitAsync(TimeSpan.FromSeconds(5));

            var processed = commands.ToArray();
            speech.Raise(service => service.PartialResultReceived += null, speech.Object, "stale partial");
            listening.TrySetResult(new() { Success = true, Transcript = "duplicate final" });
            loop.Drain();
            session.Dispose();

            Assert.Equal(processed, commands);
            Assert.False(session.IsOpen);
            Assert.Equal(1, ownerReleases);
            scope.Verify(service => service.Dispose(), Times.Once);
            engine.Verify(service => service.ClearConversationHistory(), Times.Once);
            speech.Verify(service => service.StartListeningAsync(It.IsAny<CancellationToken>()), Times.Once);
            return commands;
        }
        finally
        {
            session.Dispose();
            cancellation.Dispose();
            await session.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    // This models dispatch order, not Android input delivery or audio.
    private sealed class MainLoop
    {
        private readonly Queue<Action> _queue = new();
        private bool _isMainThread;

        public void Post(Action action) => _queue.Enqueue(action);
        public void Dispatch(Action action) => VoiceStateDispatch.Invoke(_isMainThread, Post, action);

        public void Main(Action action)
        {
            var previous = _isMainThread;
            _isMainThread = true;
            try { action(); }
            finally { _isMainThread = previous; }
        }

        public void Drain()
        {
            while (_queue.TryDequeue(out var action))
                Main(action);
        }
    }
}
