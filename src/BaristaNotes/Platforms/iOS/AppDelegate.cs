using Foundation;

namespace BaristaNotes;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	[Export("application:supportedInterfaceOrientationsForWindow:")]
	public UIKit.UIInterfaceOrientationMask GetSupportedInterfaceOrientations(
		UIKit.UIApplication application, UIKit.UIWindow? forWindow)
		=> UIKit.UIDevice.CurrentDevice.UserInterfaceIdiom == UIKit.UIUserInterfaceIdiom.Pad
			? UIKit.UIInterfaceOrientationMask.All
			: UIKit.UIInterfaceOrientationMask.AllButUpsideDown;

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
