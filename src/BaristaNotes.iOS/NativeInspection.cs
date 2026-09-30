using BaristaNotes.Native.iOS.Ailoha;
using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal static class NativeInspection
{
    public static async Task StartAsync(ILogger logger, UIViewController presenter)
    {
        try
        {
            var completion = new TaskCompletionSource<nint>(TaskCreationOptions.RunContinuationsAsynchronously);
            NativeAgentBridge.Start((port, error) =>
            {
                if (error != null) completion.TrySetException(new NSErrorException(error));
                else if (port > 0) completion.TrySetResult(port);
                else completion.TrySetException(new InvalidOperationException("Native agent returned no listening port."));
            });
            var port = await completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
            logger.LogInformation("Native inspection ready on {Port}", port);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Native inspection failed to start");
            var alert = UIAlertController.Create("Inspection unavailable", exception.Message, UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
            presenter.PresentViewController(alert, true, null);
        }
    }
}
