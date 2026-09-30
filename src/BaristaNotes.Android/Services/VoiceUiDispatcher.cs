using Android.OS;
using BaristaNotes.Core.Services.Workflows;

namespace BaristaNotes.AndroidApp.Services;

internal sealed class VoiceUiDispatcher
{
    private readonly SynchronizationContext _context;

    public VoiceUiDispatcher()
    {
        RequireMainThread();
        _context = SynchronizationContext.Current
            ?? throw new InvalidOperationException("The Android UI synchronization context is unavailable.");
    }

    public static void RequireMainThread()
    {
        if (Looper.MyLooper() != Looper.MainLooper)
            throw new InvalidOperationException("Native voice platform operations require the Android UI thread.");
    }

    public void Post(Action action) => _context.Post(_ => action(), null);

    public void DispatchState(Action action) =>
        VoiceStateDispatch.Invoke(Looper.MyLooper() == Looper.MainLooper, Post, action);

    public Task<T> InvokeAsync<T>(Func<Task<T>> action)
    {
        if (Looper.MyLooper() == Looper.MainLooper) return action();
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(async () =>
        {
            try { completion.TrySetResult(await action()); }
            catch (System.OperationCanceledException) { completion.TrySetCanceled(); }
            catch (Exception exception) { completion.TrySetException(exception); }
        });
        return completion.Task;
    }
}
