using Foundation;

namespace BaristaNotes;

// UIKit 27 requires the scene lifecycle. Without a scene delegate the Mac
// Catalyst app never shows a window. This matches Platforms/iOS/SceneDelegate.cs.
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
