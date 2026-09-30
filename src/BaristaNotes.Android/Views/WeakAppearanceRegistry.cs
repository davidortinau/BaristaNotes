using System.Runtime.CompilerServices;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class WeakAppearanceRegistry<T> : IDisposable where T : class
{
    private readonly ConditionalWeakTable<T, object> _entries = new();
    private bool _disposed;

    public void Add(T target)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _entries.GetValue(target, static _ => new object());
    }

    public IEnumerable<T> Targets
    {
        get
        {
            foreach (var entry in _entries) yield return entry.Key;
        }
    }

    public int Count => Targets.Count();
    public bool Remove(T target) => _entries.Remove(target);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _entries.Clear();
    }
}
