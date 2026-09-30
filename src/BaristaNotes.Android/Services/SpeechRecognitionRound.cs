using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.AndroidApp.Services;

internal sealed class SpeechRecognitionRound(long generation)
{
    private readonly TaskCompletionSource<SpeechRecognitionResultDto> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _finished;

    public long Generation { get; } = generation;
    public string LatestPartial { get; private set; } = "";
    public Task<SpeechRecognitionResultDto> Completion => _completion.Task;
    public bool IsFinished => Volatile.Read(ref _finished) != 0;

    public bool Partial(string text)
    {
        if (IsFinished) return false;
        LatestPartial = text;
        return true;
    }

    public bool Finish(SpeechRecognitionResultDto result)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0) return false;
        _completion.TrySetResult(result);
        return true;
    }
}
