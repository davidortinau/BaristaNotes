using Foundation;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

[Register("NativeSceneDelegate")]
public sealed class SceneDelegate : UIResponder, IUIWindowSceneDelegate
{
    [Export("window")]
    public UIWindow? Window { get; set; }
    internal Task SceneRetirement { get; private set; } = Task.CompletedTask;
    private ILogger? _logger;

    [Export("scene:willConnectToSession:options:")]
    public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        if (scene is not UIWindowScene windowScene)
            throw new InvalidOperationException("The native phone head requires a UIWindowScene.");
        if (UIApplication.SharedApplication.Delegate is not AppDelegate app)
            throw new InvalidOperationException("The native application delegate was not registered.");

        var window = ConnectWindow(windowScene,
            app.Services ?? throw new InvalidOperationException("Native services were not initialized."), app.Logging);
#if DEBUG
        _ = NativeInspection.StartAsync(app.Logging.CreateLogger<SceneDelegate>(), window.RootViewController!);
#endif
        app.Logging.CreateLogger<SceneDelegate>().LogInformation("Native window connected for scene {SceneId}", session.PersistentIdentifier);
    }

    internal UIWindow ConnectWindow(UIWindowScene scene, NativeServices services, ILoggerFactory logging)
    {
        if (Window != null) throw new InvalidOperationException("Scene window is already connected.");
        _logger = logging.CreateLogger<SceneDelegate>();
        var controller = new SliceNavigationController(services, logging);
        var window = new AppearanceWindow(scene) { RootViewController = controller };
        Window = window;
        try
        {
            NativeAppearance.Apply(window, ThemePreference.Read(services.Singleton<IPreferencesStore>()));
            controller.AttachVoiceWindow(window);
            window.MakeKeyAndVisible();
            return window;
        }
        catch
        {
            DidDisconnect(scene);
            throw;
        }
    }

    [Export("sceneDidDisconnect:")]
    public void DidDisconnect(UIScene scene)
    {
        var window = Window;
        if (window == null || window.WindowScene?.Session.PersistentIdentifier != scene.Session.PersistentIdentifier) return;
        Window = null;
        var controller = window.RootViewController as SliceNavigationController;
        var retirement = controller?.DetachVoiceWindowAsync() ?? Task.CompletedTask;
        window.Hidden = true;
        window.RootViewController = null;
        window.Dispose();
        var cleanup = RetireControllerAsync(controller, retirement, _logger);
        SceneRetirement = SceneRetirement.IsCompleted ? cleanup : Task.WhenAll(SceneRetirement, cleanup);
        _logger?.LogInformation("Scene {SceneId} disconnected; voice UI detached, dispatched work draining", scene.Session.PersistentIdentifier);
    }

    private static async Task RetireControllerAsync(SliceNavigationController? controller, Task retirement, ILogger? logger)
    {
        try { await retirement; }
        catch (Exception error) { logger?.LogError(error, "Disconnected scene voice scope did not retire cleanly"); }
        finally { controller?.Dispose(); }
    }
}
