using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;

namespace BaristaNotes.AndroidApp.Services;

internal sealed class NativeThemeState
{
    private readonly IPreferencesStore _preferences;
    private bool _systemIsDark;

    public NativeThemeState(IPreferencesStore preferences, bool systemIsDark)
    {
        _preferences = preferences;
        _systemIsDark = systemIsDark;
        CurrentMode = ThemePreference.Read(preferences);
    }

    public ThemeMode CurrentMode { get; private set; }
    public bool IsDark => CurrentMode switch
    {
        ThemeMode.Light => false,
        ThemeMode.Dark => true,
        _ => _systemIsDark
    };
    public string Label => CurrentMode switch
    {
        ThemeMode.Light => "Light theme",
        ThemeMode.Dark => "Dark theme",
        _ => "System theme"
    };

    public event EventHandler? Changed;

    public void Select(ThemeMode mode, Action<ThemeMode> applyPlatform)
    {
        // Preserve the source order and shared case-sensitive/numeric enum
        // semantics instead of inventing another preference representation.
        CurrentMode = mode;
        ThemePreference.Write(_preferences, mode);
        applyPlatform(mode);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateSystem(bool isDark)
    {
        var before = IsDark;
        _systemIsDark = isDark;
        if (before != IsDark) Changed?.Invoke(this, EventArgs.Empty);
    }
}
