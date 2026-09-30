using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services.Workflows;

public sealed record VoiceSessionMessage(string Text, bool IsUser, bool IsError);

public static class VoiceStateDispatch
{
    public static void Invoke(bool isMainThread, Action<Action> post, Action action)
    {
        if (isMainThread)
            action();
        else
            post(action);
    }
}

public sealed class VoiceSessionWorkflow : IDisposable
{
    public static readonly TimeSpan SilenceTimeout = TimeSpan.FromMilliseconds(1500);

    private readonly ISpeechRecognitionService _speech;
    private readonly IVoiceCommandService _engine;
    private readonly IOverlayService _overlay;
    private readonly Func<ValueTask> _disposeScope;
    private readonly Action<Action> _post;
    private readonly Func<CancellationToken, Task> _permissionDenied;
    private readonly Action _endOwner;
    private readonly Action<string>? _responseChanged;
    private readonly ILogger _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TaskCompletionSource _ended =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<VoiceSessionMessage> _messages = [];
    private CancellationTokenSource? _listenCancellation;
    private CancellationTokenSource? _silence;
    private EventHandler<string>? _partial;
    private Task _startTask = Task.CompletedTask;
    private Task _listenTask = Task.CompletedTask;
    private Task _commandTask = Task.CompletedTask;
    private Task _stopTask = Task.CompletedTask;
    private Task? _closeTask;
    private long _generation;
    private long _claimedGeneration = -1;
    private bool _held;
    private bool _starting;
    private bool _ending;
    private bool _speechPaused;

    public VoiceSessionWorkflow(
        long id,
        ISpeechRecognitionService speech,
        IVoiceCommandService engine,
        IOverlayService overlay,
        Func<ValueTask> disposeScope,
        Action<Action> post,
        Func<CancellationToken, Task> permissionDenied,
        Action endOwner,
        ILogger logger,
        string initialResponse = "",
        Action<string>? responseChanged = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        Id = id;
        _speech = speech;
        _engine = engine;
        _overlay = overlay;
        _disposeScope = disposeScope;
        _post = post;
        _permissionDenied = permissionDenied;
        _endOwner = endOwner;
        _logger = logger;
        LastResponse = initialResponse;
        _responseChanged = responseChanged;
        _delay = delay ?? Task.Delay;
        _overlay.CloseRequested += OnClose;
        _overlay.ExpandRequested += OnExpand;
        _overlay.MicPressStarted += OnMicStart;
        _overlay.MicPressEnded += OnMicEnd;
        _engine.PauseSpeechRequested += OnPause;
    }

    public long Id { get; }
    public IVoiceCommandService Commands => _engine;
    public bool IsOpen { get; private set; }
    public bool IsRecording { get; private set; }
    public bool IsStarting => _starting;
    public bool IsProcessing { get; private set; }
    public bool IsRetired => _ending;
    public string Transcript { get; private set; } = "";
    public string LastResponse { get; private set; }
    public IReadOnlyList<VoiceSessionMessage> Messages => _messages;
    public Task Completion => _ended.Task;

    public void Open()
    {
        if (IsOpen || _ending)
            return;
        _engine.ClearConversationHistory();
        _messages.Clear();
        IsOpen = true;
        _overlay.Show();
        Ready();
    }

    private void OnMicStart(object? sender, EventArgs args)
    {
        _held = true;
        if (!IsOpen || _ending || IsRecording || IsProcessing || _starting
            || !_listenTask.IsCompleted || !_stopTask.IsCompleted)
        {
            return;
        }
        _startTask = StartRecordingAsync();
        Observe(_startTask);
    }

    private void OnMicEnd(object? sender, EventArgs args)
    {
        _held = false;
        if (!IsOpen || _ending || IsProcessing)
            return;
        if (_starting && !IsRecording)
        {
            _generation++;
            CancelListening();
            return;
        }
        if (IsRecording)
            Observe(StopRecordingAsync());
    }

    private void OnClose(object? sender, EventArgs args) => Observe(CloseAsync());

    private void OnExpand(object? sender, EventArgs args)
    {
        if (IsOpen && _overlay.IsCollapsed)
            _overlay.Expand();
    }

    private void OnPause(object? sender, EventArgs args) =>
        _post(() => Observe(PauseAsync()));

    private async Task StartRecordingAsync()
    {
        _starting = true;
        _listenCancellation?.Dispose();
        _listenCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var cancellation = _listenCancellation.Token;
        var generation = ++_generation;
        try
        {
            if (!await _speech.RequestPermissionsAsync())
            {
                if (!IsOpen || _ending)
                    return;
                _messages.Add(new(
                    "Microphone permission is required. Please enable it in Settings.",
                    false,
                    true));
                await _permissionDenied(cancellation);
                return;
            }
            if (!IsOpen || _ending || !_held || generation != _generation
                || cancellation.IsCancellationRequested)
            {
                return;
            }

            IsRecording = true;
            _speechPaused = false;
            Transcript = "";
            _overlay.UpdateContent(new("Listening...", "", true, false));
            _partial = (_, text) => _post(() =>
            {
                if (!IsOpen || _ending || generation != _generation
                    || cancellation.IsCancellationRequested)
                {
                    return;
                }
                LastResponse = "";
                Transcript = !string.IsNullOrEmpty(Transcript)
                    && !string.IsNullOrEmpty(text)
                    ? Transcript + " " + text
                    : text;
                ResetSilence(generation);
                _overlay.UpdateContent(
                    new("Listening...", Transcript, true, false));
            });
            _speech.PartialResultReceived += _partial;
            _listenTask = ListenAsync(generation, cancellation, _partial);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Starting voice session {SessionId} recording failed",
                Id);
            if (IsOpen)
            {
                _messages.Add(new(
                    "Failed to start recording. Please try again.",
                    false,
                    true));
            }
        }
        finally
        {
            _starting = false;
        }
    }

    private async Task ListenAsync(
        long generation,
        CancellationToken cancellation,
        EventHandler<string> partial)
    {
        try
        {
            var result = await _speech.StartListeningAsync(cancellation);
            if (!IsOpen || _ending || generation != _generation
                || cancellation.IsCancellationRequested)
            {
                return;
            }
            var transcript = result.Success
                && !string.IsNullOrWhiteSpace(result.Transcript)
                ? result.Transcript
                : Transcript;
            if (!string.IsNullOrWhiteSpace(transcript))
            {
                Transcript = "";
                await ProcessOnceAsync(generation, transcript);
            }
            else
            {
                IsRecording = false;
                Ready();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _logger.LogDebug("Voice session {SessionId} listen canceled", Id);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Voice session {SessionId} listen failed",
                Id);
        }
        finally
        {
            StopSilence();
            _speech.PartialResultReceived -= partial;
            if (ReferenceEquals(_partial, partial))
                _partial = null;
        }
    }

    private async Task StopRecordingAsync()
    {
        if (!_stopTask.IsCompleted)
        {
            await _stopTask;
            return;
        }
        _stopTask = StopRecordingCoreAsync();
        await _stopTask;
    }

    private async Task StopRecordingCoreAsync()
    {
        StopSilence();
        var transcript = Transcript;
        var generation = _generation;
        CancelListening();
        try
        {
            await _speech.StopListeningAsync();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Voice session {SessionId} stop failed",
                Id);
        }
        if (_partial is not null)
            _speech.PartialResultReceived -= _partial;
        _partial = null;
        IsRecording = false;
        Transcript = "";
        if (!IsOpen || _ending)
            return;
        if (!string.IsNullOrWhiteSpace(transcript))
            await ProcessOnceAsync(generation, transcript);
        else if (!IsProcessing)
            Ready();
    }

    private Task ProcessOnceAsync(long generation, string transcript)
    {
        if (_claimedGeneration == generation || IsProcessing || !IsOpen || _ending)
            return Task.CompletedTask;
        _claimedGeneration = generation;
        _commandTask = ProcessAsync(transcript);
        return _commandTask;
    }

    private async Task ProcessAsync(string transcript)
    {
        IsProcessing = true;
        _messages.Add(new(transcript, true, false));
        _overlay.UpdateContent(new("Processing...", transcript, false, true));
        try
        {
            var result = await _engine.ProcessCommandAsync(
                new VoiceCommandRequestDto(transcript, 1),
                CancellationToken.None);
            if (!IsOpen || _ending)
                return;
            LastResponse = result.Message;
            _responseChanged?.Invoke(LastResponse);
            Transcript = "";
            _messages.Add(new(result.Message, false, !result.Success));
            _overlay.UpdateContent(new(
                result.Success ? "Done" : "Error",
                "",
                false,
                false,
                IsReady: true,
                AIResponse: result.Message));
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Processing voice session {SessionId} command failed",
                Id);
            if (!IsOpen || _ending)
                return;
            LastResponse = "Sorry, something went wrong. Please try again.";
            _responseChanged?.Invoke(LastResponse);
            _messages.Add(new(LastResponse, false, true));
            _overlay.UpdateContent(new(
                "Error",
                "",
                false,
                false,
                IsReady: true,
                ErrorMessage: "Something went wrong",
                AIResponse: LastResponse));
        }
        finally
        {
            IsProcessing = false;
            _speechPaused = false;
        }
    }

    private async Task PauseAsync()
    {
        if (!IsOpen || _ending)
            return;
        _speechPaused = true;
        if (!IsRecording)
            return;
        try
        {
            await _speech.StopListeningAsync();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Voice session {SessionId} pause failed",
                Id);
        }
        if (IsOpen && !_ending)
        {
            _overlay.UpdateContent(new(
                "Taking photo...",
                Transcript,
                false,
                true,
                AIResponse: LastResponse));
        }
    }

    private void Ready()
    {
        if (IsOpen && !_ending)
        {
            _overlay.UpdateContent(new(
                "Ready",
                "",
                false,
                false,
                IsReady: true,
                AIResponse: LastResponse));
        }
    }

    private void ResetSilence(long generation)
    {
        StopSilence();
        _silence = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        Observe(WaitForSilenceAsync(generation, _silence.Token));
    }

    private async Task WaitForSilenceAsync(
        long generation,
        CancellationToken cancellation)
    {
        try
        {
            await _delay(SilenceTimeout, cancellation);
            _post(() =>
            {
                if (!cancellation.IsCancellationRequested && IsOpen && !_ending
                    && generation == _generation && IsRecording && !_speechPaused
                    && !string.IsNullOrWhiteSpace(Transcript))
                {
                    Observe(_speech.StopListeningAsync());
                }
            });
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
    }

    private void StopSilence()
    {
        _silence?.Cancel();
        _silence?.Dispose();
        _silence = null;
    }

    public Task CloseAsync() => _closeTask ??= CloseCoreAsync();

    private async Task CloseCoreAsync()
    {
        _held = false;
        if (!IsOpen)
        {
            End();
            await Completion;
            return;
        }
        if (_starting && !IsRecording)
        {
            _generation++;
            CancelListening();
        }
        if (IsRecording)
            await StopRecordingAsync();
        End();
        await Completion;
    }

    private void End()
    {
        if (_ending)
            return;
        _ending = true;
        IsOpen = false;
        IsRecording = false;
        _lifetime.Cancel();
        CancelListening();
        StopSilence();
        _overlay.Hide();
        _overlay.CloseRequested -= OnClose;
        _overlay.ExpandRequested -= OnExpand;
        _overlay.MicPressStarted -= OnMicStart;
        _overlay.MicPressEnded -= OnMicEnd;
        _engine.PauseSpeechRequested -= OnPause;
        if (_partial is not null)
            _speech.PartialResultReceived -= _partial;
        _partial = null;
        try
        {
            _endOwner();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Stopping voice session {SessionId} platform owner failed",
                Id);
        }
        _ = DrainAsync();
    }

    private void CancelListening()
    {
        try
        {
            _listenCancellation?.Cancel();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Voice session {SessionId} cancellation callback failed",
                Id);
        }
    }

    private async Task DrainAsync()
    {
        try
        {
            await Task.WhenAll(
                _startTask,
                _listenTask,
                _commandTask,
                _stopTask);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Voice session {SessionId} retired with pending operation failure",
                Id);
        }
        finally
        {
            try
            {
                _listenCancellation?.Dispose();
                await _disposeScope();
                _logger.LogInformation(
                    "Voice session {SessionId} scope disposed after dispatched work drained",
                    Id);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Disposing voice session {SessionId} scope failed",
                    Id);
                _ended.TrySetException(exception);
            }
            finally
            {
                _lifetime.Dispose();
                _ended.TrySetResult();
            }
        }
    }

    private async void Observe(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Voice session {SessionId} operation failed",
                Id);
        }
    }

    public void Dispose()
    {
        if (_ending)
            return;
        End();
        Observe(Completion);
    }
}
