using BaristaNotes.Core.Services;

namespace BaristaNotes.Tests.Mocks;

public class MockPreferencesStore : IPreferencesStore
{
    private readonly Dictionary<string, object> _storage = new();
    
    public string? Get(string key, string? defaultValue)
    {
        return _storage.TryGetValue(key, out var value) ? value as string : defaultValue;
    }
    
    public void Set(string key, string value)
    {
        _storage[key] = value;
    }
    
    public int Get(string key, int defaultValue)
    {
        return _storage.TryGetValue(key, out var value) ? (int)value : defaultValue;
    }
    
    public void Set(string key, int value)
    {
        _storage[key] = value;
    }
    
    public void Remove(string key)
    {
        _storage.Remove(key);
    }
    
    public void Clear()
    {
        _storage.Clear();
    }
}
