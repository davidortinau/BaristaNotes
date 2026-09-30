namespace BaristaNotes.Native.iOS;

internal static class WeakUiCallback
{
    // Native children can keep managed delegates alive after their parent is popped.
    // Build the closure here so a caller's display class cannot also capture its owner.
    public static Action Create<T>(T owner, Action<T> callback) where T : class
    {
        var weak = new WeakReference<T>(owner);
        return () =>
        {
            if (weak.TryGetTarget(out var target)) callback(target);
        };
    }

    public static Action Create<T, TValue>(T owner, TValue value, Action<T, TValue> callback) where T : class
    {
        var weak = new WeakReference<T>(owner);
        return () =>
        {
            if (weak.TryGetTarget(out var target)) callback(target, value);
        };
    }
}
