using Foundation;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal static class NativeUiThread
{
    public static void Send(Action action)
    {
        if (NSThread.IsMain) action();
        else UIApplication.SharedApplication.BeginInvokeOnMainThread(action);
    }

    public static Task<T> InvokeAsync<T>(Func<T> action)
    {
        if (NSThread.IsMain) return Task.FromResult(action());
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Send(() =>
        {
            try { completion.TrySetResult(action()); }
            catch (Exception error) { completion.TrySetException(error); }
        });
        return completion.Task;
    }

    public static Task<T> InvokeAsync<T>(Func<Task<T>> action)
    {
        if (NSThread.IsMain) return action();
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Send(async () =>
        {
            try { completion.TrySetResult(await action()); }
            catch (Exception error) { completion.TrySetException(error); }
        });
        return completion.Task;
    }
}
