using Android.App;
using Android.Content;
using Android.Content.Res;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp.Services;

internal sealed class NativeThemeService
{
    private readonly Application _application;
    private readonly ILogger _logger;
    private readonly NativeThemeState _state;

    public NativeThemeService(Application application, IPreferencesStore preferences, ILogger logger)
    {
        _application = application;
        _logger = logger;
        _state = new NativeThemeState(preferences,
            (application.Resources?.Configuration?.UiMode & UiMode.NightMask) == UiMode.NightYes);
    }

    public ThemeMode CurrentMode => _state.CurrentMode;
    public bool IsDark => _state.IsDark;
    public string Label => _state.Label;
    public event EventHandler? Changed
    {
        add => _state.Changed += value;
        remove => _state.Changed -= value;
    }

    public void Select(ThemeMode mode)
    {
        _logger.LogDebug("Selecting native app theme {ThemeMode}", mode);
        // Refresh the fallback before returning to AUTO even when Android does
        // not emit a new configuration event because effective night is equal.
        _state.UpdateSystem((_application.Resources?.Configuration?.UiMode & UiMode.NightMask) == UiMode.NightYes);
        _state.Select(mode, ApplyPlatform);
    }

    public void ApplyCurrentMode() => ApplyPlatform(CurrentMode);

    public void UpdateSystem(Configuration configuration)
    {
        var dark = (configuration.UiMode & UiMode.NightMask) == UiMode.NightYes;
        _logger.LogDebug("Native appearance configuration changed: Night={Night}, AppMode={AppMode}", dark, CurrentMode);
        _state.UpdateSystem(dark);
    }

    private void ApplyPlatform(ThemeMode mode)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            using var manager = _application.GetSystemService(Context.UiModeService) as UiModeManager
                ?? throw new InvalidOperationException("Android app UI mode service is unavailable.");
            manager.SetApplicationNightMode((int)(mode switch
            {
                ThemeMode.Light => UiNightMode.No,
                ThemeMode.Dark => UiNightMode.Yes,
                _ => UiNightMode.Auto
            }));
        }
        // Before API31, MainActivity's dedicated configuration context handles
        // the app-local resource mode. Never call the system NightMode setter.
    }
}
