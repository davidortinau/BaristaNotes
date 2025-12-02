using BaristaNotes.Core.Services;

namespace BaristaNotes.Infrastructure;

public class MauiPreferencesStore : IPreferencesStore
{
    public string? Get(string key, string? defaultValue)
        => Preferences.Get(key, defaultValue);
    
    public void Set(string key, string value)
        => Preferences.Set(key, value);
    
    public int Get(string key, int defaultValue)
        => Preferences.Get(key, defaultValue);
    
    public void Set(string key, int value)
        => Preferences.Set(key, value);
    
    public void Remove(string key)
        => Preferences.Remove(key);
}
