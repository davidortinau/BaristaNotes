using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Services;

public static class ThemePreference
{
    public const string Key = "AppThemeMode";

    public static ThemeMode Read(IPreferencesStore preferences) =>
        TryRead(preferences, out var mode) ? mode : ThemeMode.System;

    public static bool TryRead(IPreferencesStore preferences, out ThemeMode mode)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var saved = preferences.Get(Key, string.Empty);
        if (!string.IsNullOrEmpty(saved) && Enum.TryParse<ThemeMode>(saved, out var parsed))
        {
            mode = parsed;
            return true;
        }

        mode = ThemeMode.System;
        return false;
    }

    public static void Write(IPreferencesStore preferences, ThemeMode mode)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        preferences.Set(Key, mode.ToString());
    }
}
