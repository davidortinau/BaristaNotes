using Android.Content;
using Android.Content.Res;
using Android.Graphics.Drawables;
using Android.Views;
using AndroidX.Core.View;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private Context? _appearanceContext;
    private Action? _refreshSettingsAppearance;

    protected override void AttachBaseContext(Context? @base)
    {
        ArgumentNullException.ThrowIfNull(@base);
        var application = @base.ApplicationContext as NativeApplication
            ?? throw new InvalidOperationException("Native theme host is unavailable.");
        using var configuration = new Configuration(@base.Resources!.Configuration!);
        configuration.UiMode = (configuration.UiMode & ~UiMode.NightMask)
            | (application.ThemeService.IsDark ? UiMode.NightYes : UiMode.NightNo);
        // A dedicated resource context makes the pre31 override local to this
        // Activity; no global resources or OS NightMode setting are written.
        _appearanceContext = @base.CreateConfigurationContext(configuration)
            ?? throw new InvalidOperationException("The app appearance context could not be created.");
        base.AttachBaseContext(_appearanceContext);
    }

    private void OnNativeThemeChanged(object? sender, EventArgs args) => ApplyNativeAppearance();

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        if (_destroyed || _style is null) return;
        try
        {
            _app.ThemeService.UpdateSystem(newConfig);
            ApplyNativeAppearance();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Native appearance configuration update failed");
            ShowFeedback(ErrorMessage(exception), isError: true);
        }
    }

    private void ApplyNativeAppearance()
    {
        if (_destroyed || _style is null) return;
        var resources = Resources ?? throw new InvalidOperationException("Activity resources are unavailable.");
        using var configuration = new Configuration(resources.Configuration!);
        configuration.UiMode = (configuration.UiMode & ~UiMode.NightMask)
            | (_app.ThemeService.IsDark ? UiMode.NightYes : UiMode.NightNo);
#pragma warning disable CS0618, CA1422
        resources.UpdateConfiguration(configuration, resources.DisplayMetrics);
#pragma warning restore CS0618, CA1422
        SetTheme(_app.ThemeService.IsDark ? Resource.Style.Barista_DarkTheme : Resource.Style.Barista_MainTheme);
        _style.RefreshAppearance(_app.ThemeService.IsDark, _root, _host, _newEditor?.Screen.Root,
            _editEditor?.Screen.Root, _historyScreen?.Root, _transient?.Root);
        _refreshSettingsAppearance?.Invoke();
        ApplyNativeWindowTheme();
        _logger.LogDebug("Applied app appearance {Mode}, Dark={Dark} without replacing page state",
            _app.ThemeService.CurrentMode, _style.IsDark);
    }

    private void ApplyNativeWindowTheme()
    {
        if (Window is not { } window) return;
        using var background = new ColorDrawable(_style.Surface);
        window.SetBackgroundDrawable(background);
        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
        {
#pragma warning disable CS0618, CA1422
            window.SetStatusBarColor(_style.Surface);
#pragma warning restore CS0618, CA1422
        }
        var controller = WindowCompat.GetInsetsController(window, window.DecorView)
            ?? throw new InvalidOperationException("Window appearance controller is unavailable.");
        controller.AppearanceLightStatusBars = !_style.IsDark;
        // As in the pinned Activity, navigation-bar treatment remains with the
        // selected platform theme/edge-to-edge policy, not a new app override.
    }
}
