using BaristaNotes.Core.Services;

namespace BaristaNotes.Infrastructure;

/// <summary>
/// App preferences. <paramref name="sharedName"/> keeps a separate set, so
/// demo mode never changes the real app's last-used bag, people, or settings.
/// </summary>
public class MauiPreferencesStore(string? sharedName = null) : IPreferencesStore
{
    public string? Get(string key, string? defaultValue)
        => Preferences.Get(key, defaultValue, sharedName);

    public void Set(string key, string value)
        => Preferences.Set(key, value, sharedName);

    public int Get(string key, int defaultValue)
        => Preferences.Get(key, defaultValue, sharedName);

    public void Set(string key, int value)
        => Preferences.Set(key, value, sharedName);

    public double Get(string key, double defaultValue)
        => Preferences.Get(key, defaultValue, sharedName);

    public void Set(string key, double value)
        => Preferences.Set(key, value, sharedName);

    public void Remove(string key)
        => Preferences.Remove(key, sharedName);
}
