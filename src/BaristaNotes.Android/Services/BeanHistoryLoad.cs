namespace BaristaNotes.AndroidApp.Services;

internal static class BeanHistoryLoad
{
    public static async Task<bool> RunAsync(Func<Task> load, Func<bool> isCurrent,
        Action<Exception> logFailure, Action<Exception> notifyFailure, Action logCancellation)
    {
        if (!isCurrent()) return false;
        try
        {
            await load();
            return isCurrent();
        }
        catch (OperationCanceledException)
        {
            logCancellation();
            return false;
        }
        catch (Exception exception)
        {
            logFailure(exception);
            // Logging is unconditional; presentation belongs to the same owner
            // and revision that initiated the read, including on failure.
            if (isCurrent()) notifyFailure(exception);
            return false;
        }
    }
}
