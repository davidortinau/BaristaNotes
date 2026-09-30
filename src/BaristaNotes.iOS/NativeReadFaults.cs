#if DEBUG
using Microsoft.Extensions.Logging;
using Foundation;

namespace BaristaNotes.Native.iOS;

internal static class NativeReadFaults
{
    private static int _bagFailure;
    private static int _activityDelayUsed;
    private static int _launchFormUsed;

    public static string? TakeLaunchForm()
    {
        if (Interlocked.Exchange(ref _launchFormUsed, 1) != 0) return null;
        var form = Environment.GetEnvironmentVariable("BN_TEST_OPEN_FORM");
        if (form == null) return null;
        if (NSBundle.MainBundle.BundleIdentifier is not
            ("com.simplyprofound.baristanotes.nativetoastd3" or "com.simplyprofound.baristanotes.nativetoastd3b"))
            throw new InvalidOperationException("Controlled form launches are restricted to the existing D3 test identities.");
        if (form is not ("bean" or "profile"))
            throw new ArgumentException("BN_TEST_OPEN_FORM must be bean or profile.");
        return form;
    }

    public static void BeanCreated()
    {
        if (Environment.GetEnvironmentVariable("BN_TEST_BAG_FAILURE_AFTER_CREATE") == "1")
            Interlocked.Exchange(ref _bagFailure, 1);
    }

    public static void BeforeBagRead(ILogger logger)
    {
        if (Interlocked.Exchange(ref _bagFailure, 0) != 1) return;
        logger.LogWarning("Controlled one-shot bag read failure after committed creation");
        throw new IOException("Controlled bag refresh failure");
    }

    public static async Task AfterActivityReadAsync(ILogger logger)
    {
        var value = Environment.GetEnvironmentVariable("BN_TEST_ACTIVITY_DELAY_MS");
        if (value == null || Interlocked.Exchange(ref _activityDelayUsed, 1) != 0) return;
        if (!int.TryParse(value, out var milliseconds) || milliseconds is < 1 or > 30000)
            throw new ArgumentException("BN_TEST_ACTIVITY_DELAY_MS must be between 1 and 30000.");
        logger.LogWarning("Delaying the first Activity result by {Milliseconds} ms for controlled verification", milliseconds);
        await Task.Delay(milliseconds);
    }
}
#endif
