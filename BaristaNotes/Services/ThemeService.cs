using BaristaNotes.Core.Services;

namespace BaristaNotes.Services;

public class ThemeService : IThemeService
{
    private readonly IPreferencesStore _preferencesStore;
    private const string ThemeModeKey = "AppThemeMode";

    private ThemeMode _currentMode = ThemeMode.System;

    public ThemeMode CurrentMode => _currentMode;

    public AppTheme CurrentTheme
    {
        get
        {
            return _currentMode switch
            {
                ThemeMode.Light => AppTheme.Light,
                ThemeMode.Dark => AppTheme.Dark,
                ThemeMode.System => Application.Current?.RequestedTheme ?? AppTheme.Light,
                _ => AppTheme.Light
            };
        }
    }

    public ThemeService(IPreferencesStore preferencesStore)
    {
        _preferencesStore = preferencesStore;

        // Initialize _currentMode from stored preferences
        var saved = _preferencesStore.Get(ThemeModeKey, string.Empty);
        if (!string.IsNullOrEmpty(saved) && Enum.TryParse<ThemeMode>(saved, out var mode))
        {
            _currentMode = mode;
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Loaded saved theme mode: {_currentMode}");
        }

        // Subscribe to system theme changes
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged += OnSystemThemeChanged;
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Subscribed to RequestedThemeChanged event");
        }
    }

    public Task<ThemeMode> GetThemeModeAsync()
    {
        var saved = _preferencesStore.Get(ThemeModeKey, string.Empty);
        if (string.IsNullOrEmpty(saved) || !Enum.TryParse<ThemeMode>(saved, out var mode))
        {
            return Task.FromResult(ThemeMode.System);
        }
        return Task.FromResult(mode);
    }

    public Task SetThemeModeAsync(ThemeMode mode)
    {
        System.Diagnostics.Debug.WriteLine($"[ThemeService] SetThemeModeAsync called with mode: {mode}");
        _currentMode = mode;
        _preferencesStore.Set(ThemeModeKey, mode.ToString());
        ApplyTheme();
        return Task.CompletedTask;
    }

    public void ApplyTheme()
    {
        if (Application.Current == null) return;

        // When System mode, use Unspecified to let system control the theme
        // This allows RequestedThemeChanged events to fire properly
        var targetTheme = _currentMode switch
        {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark => AppTheme.Dark,
            ThemeMode.System => AppTheme.Unspecified, // Let system control theme
            _ => AppTheme.Unspecified
        };

        System.Diagnostics.Debug.WriteLine($"[ThemeService] ApplyTheme: CurrentMode={_currentMode}, TargetTheme={targetTheme}, SystemTheme={Application.Current.RequestedTheme}");
        Application.Current.UserAppTheme = targetTheme;
    }

    private void OnSystemThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[ThemeService] OnSystemThemeChanged fired: NewTheme={e.RequestedTheme}, CurrentMode={_currentMode}");

        // Only react to system theme changes if we're in System mode
        if (_currentMode == ThemeMode.System)
        {
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Applying theme because CurrentMode is System");
            ApplyTheme();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Ignoring system theme change because CurrentMode is {_currentMode}");
        }
    }
}
