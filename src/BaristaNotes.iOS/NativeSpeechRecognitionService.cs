using System.Globalization;
using AVFoundation;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using Foundation;
using Microsoft.Extensions.Logging;
using Speech;

namespace BaristaNotes.Native.iOS;

internal sealed class NativeSpeechRecognitionService(ILogger<NativeSpeechRecognitionService> logger)
    : ISpeechRecognitionService, IDisposable, IAsyncDisposable
{
    private Recognition? _active;
    private CancellationTokenSource? _startCancellation;
    private bool _starting, _disposed;
    private int _generation;
    public SpeechRecognitionState State { get; private set; }
    public event EventHandler<SpeechRecognitionState>? StateChanged;
    public event EventHandler<string>? PartialResultReceived;

    public Task<bool> IsAvailableAsync() => NativeUiThread.InvokeAsync(() =>
    {
        using var recognizer = new SFSpeechRecognizer(NSLocale.FromLocaleIdentifier(CultureInfo.CurrentCulture.Name));
        return recognizer.Available;
    });

    public Task<bool> RequestPermissionsAsync() => NativeUiThread.InvokeAsync(() =>
    {
        var speech = SFSpeechRecognizer.AuthorizationStatus;
        var microphone = AVCaptureDevice.GetAuthorizationStatus(AVAuthorizationMediaType.Audio);
        logger.LogInformation("Speech permission status {Speech}; microphone {Microphone}", speech, microphone);
        // Match the source status-only check. Only an explicit subsequent start
        // can request undetermined permissions; opening Ready never gets here.
        return NativeSpeechPermissions.AllowsStart(speech, microphone);
    });

    public Task<SpeechRecognitionResultDto> StartListeningAsync(CancellationToken cancellationToken = default) =>
        NativeUiThread.InvokeAsync(() => StartOnUiAsync(cancellationToken));

    private async Task<SpeechRecognitionResultDto> StartOnUiAsync(CancellationToken token)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(NativeSpeechRecognitionService));
        if (_starting || _active != null) return Failure("Already listening");
        var generation = ++_generation;
        _starting = true;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(stop.Token, budget.Token);
        _startCancellation = stop;
        var operationToken = combined.Token;
        try
        {
            operationToken.ThrowIfCancellationRequested();
            if (!await EnsureAuthorizationAsync(operationToken))
            {
                SetState(SpeechRecognitionState.Error);
                return Failure("Speech recognition permission denied");
            }
            if (_disposed || generation != _generation) return Failure("Cancelled");
            operationToken.ThrowIfCancellationRequested();
            var session = new Recognition();
            _active = session;
            SetState(SpeechRecognitionState.Listening);
            try
            {
                session.Recognizer = new SFSpeechRecognizer(NSLocale.FromLocaleIdentifier(CultureInfo.CurrentCulture.Name));
                if (!session.Recognizer.Available) throw new InvalidOperationException("Speech recognizer is not available");
                session.Request = new SFSpeechAudioBufferRecognitionRequest { ShouldReportPartialResults = true };
                session.AudioSession = AVAudioSession.SharedInstance();
                if (!session.AudioSession.SetSupportsMultichannelContent(true, out var channelError))
                    throw new NSErrorException(channelError ?? new NSError(new NSString("NativeSpeech"), 1));
#pragma warning disable CA1422
                var options = AVAudioSessionCategoryOptions.DefaultToSpeaker | AVAudioSessionCategoryOptions.AllowBluetooth
                    | AVAudioSessionCategoryOptions.AllowAirPlay | AVAudioSessionCategoryOptions.AllowBluetoothA2DP;
#pragma warning restore CA1422
                var category = AVAudioSessionCategory.PlayAndRecord.GetConstant()?.ToString()
                    ?? throw new InvalidOperationException("No PlayAndRecord category.");
                if (!session.AudioSession.SetCategory(category, options, out var categoryError))
                    throw new NSErrorException(categoryError ?? new NSError(new NSString("NativeSpeech"), 2));
                session.Engine = new AVAudioEngine();
                var input = session.Engine.InputNode;
                using var format = input.GetBusOutputFormat(0);
                var weakSession = new WeakReference<Recognition>(session);
                AVAudioNodeTapBlock tapBlock = (buffer, _) =>
                {
                    if (weakSession.TryGetTarget(out var current))
                    {
                        lock (current.AudioGate)
                            if (!current.Ended) current.Request?.Append(buffer);
                    }
                };
                if (OperatingSystem.IsIOSVersionAtLeast(27))
                {
                    if (!input.InstallTapOnBus(0, 1024, format, out var tapError, tapBlock))
                        throw new NSErrorException(tapError ?? new NSError(new NSString("NativeSpeech"), 4));
                }
                else
                {
#pragma warning disable CA1422
                    input.InstallTapOnBus(0, 1024, format, tapBlock);
#pragma warning restore CA1422
                }
                session.TapInstalled = true;
                session.Engine.Prepare();
                if (!session.Engine.StartAndReturnError(out var engineError))
                    throw new NSErrorException(engineError ?? new NSError(new NSString("NativeSpeech"), 3));
                session.EngineStarted = true;
                var weakOwner = new WeakReference<NativeSpeechRecognitionService>(this);
                session.Task = session.Recognizer.GetRecognitionTask(session.Request, (result, error) =>
                {
                    if (!weakSession.TryGetTarget(out var current)) return;
                    // Copy native callback values before dispatching; their native
                    // lifetime is not extended by a queued UI callback.
                    var text = result?.BestTranscription.FormattedString;
                    var final = result?.Final ?? false;
                    var message = error?.LocalizedDescription;
                    NativeUiThread.Send(() =>
                    {
                        if (!weakOwner.TryGetTarget(out var owner) || !ReferenceEquals(owner._active, current)) return;
                        if (message != null) owner.Complete(current, Failure(message), SpeechRecognitionState.Error);
                        else if (final)
                        {
                            owner.SetState(SpeechRecognitionState.Processing);
                            owner.Complete(current, string.IsNullOrEmpty(text) ? Failure("No speech recognized")
                                : new SpeechRecognitionResultDto { Success = true, Transcript = text, Confidence = 1 },
                                string.IsNullOrEmpty(text) ? SpeechRecognitionState.Error : SpeechRecognitionState.Idle);
                        }
                        else if (text != null) owner.RaisePartial(text);
                    });
                });
                session.Cancellation = operationToken.Register(() => NativeUiThread.Send(() =>
                {
                    if (weakOwner.TryGetTarget(out var owner))
                        owner.Complete(session, Failure(budget.IsCancellationRequested
                            ? "Listening timed out. Please try again." : "Cancelled"), SpeechRecognitionState.Idle);
                }));
                return await session.Completion.Task;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Native speech start failed");
                Complete(session, Failure(error.Message), SpeechRecognitionState.Error);
                return await session.Completion.Task;
            }
        }
        catch (OperationCanceledException)
        {
            SetState(SpeechRecognitionState.Idle);
            return Failure(budget.IsCancellationRequested ? "Listening timed out. Please try again." : "Cancelled");
        }
        catch (Exception error)
        {
            logger.LogError(error, "Speech authorization/start failed");
            SetState(SpeechRecognitionState.Error);
            return Failure(error.Message);
        }
        finally { _starting = false; _startCancellation = null; }
    }

    private static async Task<bool> EnsureAuthorizationAsync(CancellationToken token)
    {
        var speech = SFSpeechRecognizer.AuthorizationStatus;
        if (speech == SFSpeechRecognizerAuthorizationStatus.NotDetermined)
        {
            var result = new TaskCompletionSource<SFSpeechRecognizerAuthorizationStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
            SFSpeechRecognizer.RequestAuthorization(status => result.TrySetResult(status));
            speech = await result.Task.WaitAsync(token);
        }
        if (speech != SFSpeechRecognizerAuthorizationStatus.Authorized) return false;
        var microphone = AVCaptureDevice.GetAuthorizationStatus(AVAuthorizationMediaType.Audio);
        if (microphone == AVAuthorizationStatus.NotDetermined)
        {
            var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
#pragma warning disable CA1422
            AVAudioSession.SharedInstance().RequestRecordPermission(granted => result.TrySetResult(granted));
#pragma warning restore CA1422
            return await result.Task.WaitAsync(token);
        }
        return microphone == AVAuthorizationStatus.Authorized;
    }

    public async Task StopListeningAsync() => await NativeUiThread.InvokeAsync(() =>
    {
        _generation++;
        if (_active is { } session) Complete(session, Failure("Stopped"), SpeechRecognitionState.Idle);
        else SetState(SpeechRecognitionState.Idle);
        _startCancellation?.Cancel();
        return true;
    });

    private void Complete(Recognition session, SpeechRecognitionResultDto result, SpeechRecognitionState state)
    {
        if (!ReferenceEquals(_active, session)) return;
        _active = null;
        lock (session.AudioGate) session.Ended = true;
        try
        {
            session.Engine?.Stop();
            if (session.TapInstalled)
            {
                session.Engine?.InputNode.RemoveTapOnBus(0);
                session.TapInstalled = false;
            }
            session.Request?.EndAudio();
            session.Task?.Finish();
            session.Task?.Cancel();
        }
        catch (Exception error) { logger.LogWarning(error, "Speech stop cleanup failed"); }
        finally
        {
            session.Cancellation.Dispose();
            session.Task?.Dispose();
            session.Request?.Dispose();
            session.Recognizer?.Dispose();
            session.Engine?.Dispose();
            if (session.EngineStarted && session.AudioSession is { } audio)
            {
                if (!audio.SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out var error))
                    logger.LogWarning("Could not deactivate speech audio session: {Error}", error?.LocalizedDescription);
            }
            SetState(state);
            session.Completion.TrySetResult(result);
        }
    }
    private void SetState(SpeechRecognitionState state)
    {
        if (State == state) return;
        State = state;
        try { StateChanged?.Invoke(this, state); }
        catch (Exception error) { logger.LogError(error, "Speech state listener failed"); }
    }
    private void RaisePartial(string text)
    {
        try { PartialResultReceived?.Invoke(this, text); }
        catch (Exception error) { logger.LogError(error, "Speech partial-result listener failed"); }
    }
    private static SpeechRecognitionResultDto Failure(string message) => new() { Success = false, ErrorMessage = message };
    private void DisposeOnUi()
    {
        if (_disposed) return;
        _disposed = true;
        _generation++;
        if (_active is { } session) Complete(session, Failure("Cancelled"), SpeechRecognitionState.Idle);
        _startCancellation?.Cancel();
    }
    public void Dispose() => NativeUiThread.Send(DisposeOnUi);
    public async ValueTask DisposeAsync() => await NativeUiThread.InvokeAsync(() => { DisposeOnUi(); return true; });
    private sealed class Recognition
    {
        public object AudioGate { get; } = new();
        public bool Ended, TapInstalled, EngineStarted;
        public AVAudioEngine? Engine;
        public AVAudioSession? AudioSession;
        public SFSpeechRecognizer? Recognizer;
        public SFSpeechAudioBufferRecognitionRequest? Request;
        public SFSpeechRecognitionTask? Task;
        public CancellationTokenRegistration Cancellation;
        public TaskCompletionSource<SpeechRecognitionResultDto> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
