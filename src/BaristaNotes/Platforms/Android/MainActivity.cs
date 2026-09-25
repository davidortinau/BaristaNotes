using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;
using Microsoft.Maui.Platform;

namespace BaristaNotes;

[Activity(Theme = "@style/Barista.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private MauiControls.Application? _application;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Resolve the saved mode before Reactor applies styles and creates its first Window.
        IPlatformApplication.Current!.Services.GetRequiredService<IThemeService>().ApplyTheme();
        SetTheme(Resource.Style.Barista_MainTheme);
        ApplyWindowTheme();
        base.OnCreate(savedInstanceState);

        _application = MauiControls.Application.Current;
        if (_application is not null)
            _application.RequestedThemeChanged += OnRequestedThemeChanged;

        ApplyWindowTheme();
    }

    protected override void OnDestroy()
    {
        if (_application is not null)
            _application.RequestedThemeChanged -= OnRequestedThemeChanged;

        base.OnDestroy();
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
        => ApplyWindowTheme();

    private void ApplyWindowTheme()
    {
        if (Window is not { } window)
            return;

        var isLight = MauiControls.Application.Current?.RequestedTheme != AppTheme.Dark;
        var background = (isLight ? AppColors.Light.Surface : AppColors.Dark.Surface).ToPlatform();
        window.SetBackgroundDrawable(new Android.Graphics.Drawables.ColorDrawable(background));

        // Android 15+ draws the window behind a transparent status bar.
        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
        {
#pragma warning disable CS0618
            window.SetStatusBarColor(background);
#pragma warning restore CS0618
        }

        var insetsController = WindowCompat.GetInsetsController(window, window.DecorView)
            ?? throw new InvalidOperationException("Android window insets controller is unavailable.");
        insetsController.AppearanceLightStatusBars = isLight;
    }
}
