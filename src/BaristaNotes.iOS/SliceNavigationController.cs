using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.Exceptions;
using CoreGraphics;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class SliceNavigationController : UINavigationController
{
    public NativeServices Services { get; }
    public ILoggerFactory Logging { get; }
    public WindowFeedbackHost FeedbackHost { get; }
    private DrinkViewController? _newDrink;
    private ActivityViewController? _activity;
    private SettingsViewController? _settings;
    internal NativeVoiceCoordinator? Voice { get; private set; }
    private bool _sceneDetached;
    private Task _voiceRetirement = Task.CompletedTask;
    internal bool IsSceneAttached => !_sceneDetached;

    public SliceNavigationController(NativeServices services, ILoggerFactory logging)
    {
        Services = services;
        Logging = logging;
        FeedbackHost = new WindowFeedbackHost(() => View?.Window, logging.CreateLogger<WindowFeedbackHost>());
        SetNavigationBarHidden(true, false);
        _newDrink = new DrinkViewController(this);
        SetViewControllers([_newDrink], false);
    }

    public void NewDrink()
    {
        if (FeedbackHost.IsShowing || !CanNavigate(NewDrink)) return;
        _newDrink ??= new DrinkViewController(this);
        SetViewControllers([_newDrink], false);
    }

    public void Activity()
    {
        if (FeedbackHost.IsShowing || !CanNavigate(Activity)) return;
        _activity ??= new ActivityViewController(this);
        SetViewControllers([_activity], false);
    }

    public void Edit(int id)
    {
        if (!FeedbackHost.IsShowing && CanNavigate(() => Edit(id)))
            PushViewController(new DrinkViewController(this, id), true);
    }

    public void Settings()
    {
        if (FeedbackHost.IsShowing || !CanNavigate(Settings)) return;
        _settings ??= new SettingsViewController(this);
        SetViewControllers([_settings], false);
    }

    public void Profiles()
    {
        if (FeedbackHost.IsShowing || TopViewController is ProfilesViewController || !CanNavigate(Profiles)) return;
        PushHierarchy(new ProfilesViewController(this));
    }

    public void Equipment()
    {
        if (FeedbackHost.IsShowing || TopViewController is EquipmentListViewController || !CanNavigate(Equipment)) return;
        PushHierarchy(new EquipmentListViewController(this));
    }

    public void Beans()
    {
        if (FeedbackHost.IsShowing || TopViewController is BeanListViewController || !CanNavigate(Beans)) return;
        PushHierarchy(new BeanListViewController(this));
    }

    public void Ranges(DrinkValueMetric metric)
    {
        if (FeedbackHost.IsShowing || TopViewController is RangeSettingsViewController ||
            !CanNavigate(() => Ranges(metric))) return;
        PushHierarchy(new RangeSettingsViewController(this, metric));
    }

    public void RequestBack()
    {
        if (CanNavigate(RequestBack)) PopViewController(true);
    }

    internal void PushHierarchy(UIViewController controller) => PushViewController(controller, true);

    internal void AttachVoiceWindow(UIWindow window)
    {
        var overlay = Services.Singleton<NativeVoiceOverlay>();
        overlay.Attach(window, this);
        Services.Singleton<NativeVoicePlatformActions>().Attach(this);
        Voice = new NativeVoiceCoordinator(this, overlay);
    }

    internal Task DetachVoiceWindowAsync()
    {
        if (_sceneDetached) return _voiceRetirement;
        var photoOwners = (ViewControllers ?? []).OfType<DrinkViewController>().ToList();
        if (_newDrink != null && !photoOwners.Contains(_newDrink)) photoOwners.Add(_newDrink);
        foreach (var owner in photoOwners) owner.RetirePhoto();
        _sceneDetached = true;
        var coordinator = Voice;
        Voice = null;
        // DisposeAsync unsubscribes and retires presentation synchronously before
        // its first await; dispatched domain work retains its scope until drained.
        _voiceRetirement = coordinator?.DisposeAsync().AsTask() ?? Task.CompletedTask;
        Services.Singleton<NativeVoiceOverlay>().Detach(this);
        Services.Singleton<NativeVoicePlatformActions>().Detach(this);
        FeedbackHost.Dispose();
        return _voiceRetirement;
    }

    internal async Task ToggleVoiceAsync(bool navigateToDrink = false)
    {
        var coordinator = Voice;
        if (_sceneDetached || coordinator == null || FeedbackHost.IsShowing || PresentedViewController != null) return;
        if (navigateToDrink) NewDrink();
        var current = TopViewController;
        if (current is DrinkViewController drink) await drink.WaitForVoiceReadinessAsync();
        // Readiness can outlive scene teardown. Validate managed ownership before
        // touching UIKit properties on a detached or disposed navigation host.
        if (_sceneDetached || !ReferenceEquals(Voice, coordinator)) return;
        if (TopViewController == current && PresentedViewController == null) await coordinator.ToggleAsync();
    }

    internal void PushVoicePage(Func<UIViewController> create)
    {
        if (!FeedbackHost.IsShowing && CanNavigate(() => PushVoicePage(create))) PushViewController(create(), false);
    }

    internal void RefreshVoiceReferences(DataChangeType change)
    {
        if (_sceneDetached) return;
        var pages = (ViewControllers ?? []).OfType<DrinkViewController>().ToList();
        if (_newDrink != null && !pages.Contains(_newDrink)) pages.Add(_newDrink);
        foreach (var page in pages) _ = page.RefreshVoiceReferencesAsync(change);
    }

    private bool CanNavigate(Action navigate) =>
        !_sceneDetached && PresentedViewController == null &&
        (TopViewController is not RangeEditorViewController editor || editor.CanNavigate(navigate));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ = ObserveVoiceRetirementAsync(DetachVoiceWindowAsync());
        }
        base.Dispose(disposing);
    }

    private async Task ObserveVoiceRetirementAsync(Task retirement)
    {
        try { await retirement; }
        catch (Exception error) { Logging.CreateLogger<SliceNavigationController>().LogError(error, "Scene voice retirement failed"); }
    }
}

internal abstract class SliceViewController : UIViewController
{
    protected SliceNavigationController Host { get; }
    protected NativeServices Services => Host.Services;
    protected ILogger Logger { get; }
    protected UIView Root => View ?? throw new InvalidOperationException("Native view is unavailable.");

    protected SliceViewController(SliceNavigationController host)
    {
        Host = host;
        Logger = host.Logging.CreateLogger(GetType().FullName ?? GetType().Name);
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Root.BackgroundColor = NativeTheme.Surface;
    }

    protected async Task GuardAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Native operation failed on {Screen}", GetType().Name);
            var message = exception is ValidationException validation
                ? validation.Errors.SelectMany(pair => pair.Value).FirstOrDefault() ?? "Invalid input"
                : exception.Message;
            ShowFeedback(message, isError: true);
        }
    }

    public void ShowFeedback(string message, bool isError = false) =>
        Host.FeedbackHost.Show(message, isError ? NativeFeedbackKind.Error : NativeFeedbackKind.Success);

    protected void Pending(string feature) =>
        Host.FeedbackHost.Show($"{feature} is outside this first slice; it is not connected yet.", NativeFeedbackKind.Information);
}
