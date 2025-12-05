using BaristaNotes.Core.Services;

namespace BaristaNotes.Services;

public class ThemeService : IThemeService
{
    private readonly IPreferencesStore _preferencesStore;
    private const string ThemeModeKey = "AppThemeMode";
    
    private ThemeMode _currentMode = ThemeMode.System;

    public ThemeMode CurrentMode => _currentMode;

    public AppTheme CurrentTheme =>
        _currentMode == ThemeMode.System
            ? Application.Current?.RequestedTheme ?? AppTheme.Light
            : _currentMode == ThemeMode.Dark ? AppTheme.Dark : AppTheme.Light;

    public ThemeService(IPreferencesStore preferencesStore)
    {
        _preferencesStore = preferencesStore;
        
        // Subscribe to system theme changes
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged += OnSystemThemeChanged;
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
        _currentMode = mode;
        _preferencesStore.Set(ThemeModeKey, mode.ToString());
        ApplyTheme();
        return Task.CompletedTask;
    }

    public void ApplyTheme()
    {
        if (Application.Current == null) return;

        var targetTheme = _currentMode switch
        {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark => AppTheme.Dark,
            ThemeMode.System => Application.Current.RequestedTheme,
            _ => AppTheme.Light
        };

        Application.Current.UserAppTheme = targetTheme;
    }

    private void OnSystemThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        // Only react to system theme changes if we're in System mode
        if (_currentMode == ThemeMode.System)
        {
            ApplyTheme();
        }
    }
}
