using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Speech;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace BaristaNotes.AndroidApp.Services;

internal sealed class AndroidSpeechRecognitionService(
    Activity activity, VoiceUiDispatcher dispatcher, ILogger logger) : ISpeechRecognitionService, IDisposable
{
    private sealed class ActiveRound(SpeechRecognitionRound state, CancellationToken caller)
    {
        public SpeechRecognitionRound State { get; } = state;
        public CancellationToken Caller { get; } = caller;
        public CancellationTokenSource Lifetime { get; } = CancellationTokenSource.CreateLinkedTokenSource(caller);
        public CancellationTokenRegistration Registration { get; set; }
        public SpeechRecognizer? Recognizer { get; set; }
        public Listener? Listener { get; set; }
    }

    private ActiveRound? _round;
    private long _generation;
    private bool _disposed;
    public SpeechRecognitionState State { get; private set; } = SpeechRecognitionState.Idle;
    public event EventHandler<SpeechRecognitionState>? StateChanged;
    public event EventHandler<string>? PartialResultReceived;

    public Task<bool> IsAvailableAsync()
    {
        VoiceUiDispatcher.RequireMainThread();
        return Task.FromResult(!_disposed && SpeechRecognizer.IsRecognitionAvailable(activity));
    }

    public Task<bool> RequestPermissionsAsync()
    {
        VoiceUiDispatcher.RequireMainThread();
        // The pinned app checks Android permission status, then offers its
        // Permission Required/Settings dialog. It does not auto-request here.
        return Task.FromResult(!_disposed
            && activity.CheckSelfPermission(Android.Manifest.Permission.RecordAudio) == Permission.Granted);
    }

    public Task<SpeechRecognitionResultDto> StartListeningAsync(CancellationToken cancellationToken = default)
    {
        VoiceUiDispatcher.RequireMainThread();
        if (_disposed) return Task.FromResult(Failure("Cancelled"));
        if (_round is not null) return Task.FromResult(Failure("Already listening"));
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(Failure("Cancelled"));
        var round = new ActiveRound(new SpeechRecognitionRound(++_generation), cancellationToken);
        _round = round;
        ChangeState(SpeechRecognitionState.Listening);
        try
        {
            if (!SpeechRecognizer.IsRecognitionAvailable(activity))
                throw new NotSupportedException("Speech Recognition is not available on this device");
            round.Recognizer = SpeechRecognizer.CreateSpeechRecognizer(activity)
                ?? throw new NotSupportedException("Speech recognizer is not available on this device");
            round.Listener = new Listener(
                text => OnPartial(round, text),
                text => OnResult(round, text),
                error => OnError(round, error));
            round.Recognizer.SetRecognitionListener(round.Listener);
            round.Lifetime.CancelAfter(TimeSpan.FromSeconds(60));
            round.Registration = round.Lifetime.Token.Register(() => dispatcher.Post(() =>
            {
                if (!ReferenceEquals(_round, round)) return;
                Finish(round, Failure(round.Caller.IsCancellationRequested
                    ? "Cancelled" : "Listening timed out. Please try again."), SpeechRecognitionState.Idle);
            }));
            using var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
            intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
            intent.PutExtra(RecognizerIntent.ExtraCallingPackage, activity.PackageName);
            intent.PutExtra(RecognizerIntent.ExtraPartialResults, true);
            using var locale = Java.Util.Locale.ForLanguageTag(CultureInfo.CurrentCulture.Name);
            var language = locale?.ToLanguageTag() ?? CultureInfo.CurrentCulture.Name;
            intent.PutExtra(RecognizerIntent.ExtraLanguage, language);
            intent.PutExtra(RecognizerIntent.ExtraLanguagePreference, language);
            intent.PutExtra(RecognizerIntent.ExtraOnlyReturnLanguagePreference, language);
            // Source defaults use the ordinary online-capable recognizer. Its
            // caller owns the separate1.5s silence timer, not these intent extras.
            if (cancellationToken.IsCancellationRequested)
                Finish(round, Failure("Cancelled"), SpeechRecognitionState.Idle);
            else
                round.Recognizer.StartListening(intent);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Starting Android speech recognition failed");
            Finish(round, Failure(exception.Message), SpeechRecognitionState.Error);
        }
        return round.State.Completion;
    }

    public Task StopListeningAsync()
    {
        VoiceUiDispatcher.RequireMainThread();
        if (_round is { } round)
        {
            // Toolkit StopListening+Destroy can leave its wrapper's TCS pending.
            // Settle with the latest hypothesis; the caller's cancelled-token
            // branch separately processes its captured source transcript once.
            var text = round.State.LatestPartial;
            Finish(round, string.IsNullOrWhiteSpace(text) ? Failure("No speech recognized")
                : new SpeechRecognitionResultDto { Success = true, Transcript = text, Confidence = 1 },
                SpeechRecognitionState.Idle);
        }
        else ChangeState(SpeechRecognitionState.Idle);
        return Task.CompletedTask;
    }

    private void OnPartial(ActiveRound round, string text)
    {
        if (!ReferenceEquals(_round, round) || !round.State.Partial(text)) return;
        PartialResultReceived?.Invoke(this, text);
    }

    private void OnResult(ActiveRound round, string text)
    {
        if (!ReferenceEquals(_round, round)) return;
        ChangeState(SpeechRecognitionState.Processing);
        Finish(round, string.IsNullOrEmpty(text) ? Failure("No speech recognized")
            : new SpeechRecognitionResultDto { Success = true, Transcript = text, Confidence = 1 },
            string.IsNullOrEmpty(text) ? SpeechRecognitionState.Error : SpeechRecognitionState.Idle);
    }

    private void OnError(ActiveRound round, SpeechRecognizerError error)
    {
        if (!ReferenceEquals(_round, round)) return;
        ChangeState(SpeechRecognitionState.Processing);
        logger.LogWarning("Android speech recognizer failed with {SpeechError}", error);
        Finish(round, Failure($"Failure in speech engine - {error}"), SpeechRecognitionState.Error);
    }

    private void Finish(ActiveRound round, SpeechRecognitionResultDto result, SpeechRecognitionState state)
    {
        if (!ReferenceEquals(_round, round) || !round.State.Finish(result)) return;
        _round = null;
        round.Listener?.Detach();
        try { round.Recognizer?.StopListening(); }
        catch (Exception exception) { logger.LogWarning(exception, "Stopping Android recognizer failed"); }
        try { round.Recognizer?.SetRecognitionListener(null); }
        catch (Exception exception) { logger.LogWarning(exception, "Detaching Android recognition listener failed"); }
        try { round.Recognizer?.Destroy(); }
        catch (Exception exception) { logger.LogWarning(exception, "Destroying Android recognizer failed"); }
        try { round.Recognizer?.Dispose(); }
        catch (Exception exception) { logger.LogWarning(exception, "Disposing Android recognizer failed"); }
        try { round.Listener?.Dispose(); }
        catch (Exception exception) { logger.LogWarning(exception, "Disposing recognition listener failed"); }
        round.Registration.Dispose();
        round.Lifetime.Dispose();
        ChangeState(state);
    }

    private void ChangeState(SpeechRecognitionState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(this, state);
    }

    private static SpeechRecognitionResultDto Failure(string message) => new() { Success = false, ErrorMessage = message };

    public void Dispose()
    {
        if (_disposed) return;
        VoiceUiDispatcher.RequireMainThread();
        _disposed = true;
        if (_round is { } round) Finish(round, Failure("Cancelled"), SpeechRecognitionState.Idle);
        PartialResultReceived = null;
        StateChanged = null;
    }

    private sealed class Listener(Action<string> partial, Action<string> result, Action<SpeechRecognizerError> error)
        : Java.Lang.Object, IRecognitionListener
    {
        private Action<string>? _partial = partial;
        private Action<string>? _result = result;
        private Action<SpeechRecognizerError>? _error = error;
        public void Detach() { _partial = null; _result = null; _error = null; }
        public void OnBeginningOfSpeech() { }
        public void OnBufferReceived(byte[]? buffer) { }
        public void OnEndOfSpeech() { }
        public void OnError(SpeechRecognizerError error) => _error?.Invoke(error);
        public void OnEvent(int eventType, Bundle? parameters) { }
        public void OnReadyForSpeech(Bundle? parameters) { }
        public void OnRmsChanged(float rmsdB) { }
        public void OnPartialResults(Bundle? results) => Send(results, _partial);
        public void OnResults(Bundle? results) => Send(results, _result);
        private static void Send(Bundle? results, Action<string>? deliver)
        {
            var matches = results?.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
            if (matches is { Count: > 0 }) deliver?.Invoke(matches[0]);
        }
    }
}
