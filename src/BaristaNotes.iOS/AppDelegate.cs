using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

[Register("NativeAppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public ILoggerFactory Logging { get; } = LoggerFactory.Create(builder => builder.AddDebug());
    internal NativeServices? Services { get; private set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        Logging.CreateLogger<AppDelegate>().LogInformation(
            "Native UIKit startup for {AppId}, build {BuildNumber}",
            NSBundle.MainBundle.BundleIdentifier, NSBundle.MainBundle.ObjectForInfoDictionary("CFBundleVersion"));
        Services = new NativeServices(Logging);
        return true;
    }

    public override UISceneConfiguration GetConfiguration(
        UIApplication application,
        UISceneSession connectingSceneSession,
        UISceneConnectionOptions options)
    {
        return new UISceneConfiguration("Native", connectingSceneSession.Role)
        {
            DelegateType = typeof(SceneDelegate)
        };
    }

    public override void WillTerminate(UIApplication application)
    {
        Services?.Dispose();
        Logging.Dispose();
    }
}
