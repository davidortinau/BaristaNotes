using Android.Content;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp.Services;

public sealed class AndroidPreferencesStore(
    ISharedPreferences preferences,
    ILogger<AndroidPreferencesStore> logger) : IPreferencesStore
{
    public string? Get(string key, string? defaultValue) => preferences.GetString(key, defaultValue);
    public int Get(string key, int defaultValue) => preferences.GetInt(key, defaultValue);
    public double Get(string key, double defaultValue) =>
        BitConverter.Int64BitsToDouble(preferences.GetLong(key, BitConverter.DoubleToInt64Bits(defaultValue)));

    public void Set(string key, string value) => Commit(editor => editor.PutString(key, value));
    public void Set(string key, int value) => Commit(editor => editor.PutInt(key, value));
    public void Set(string key, double value) =>
        Commit(editor => editor.PutLong(key, BitConverter.DoubleToInt64Bits(value)));
    public void Remove(string key) => Commit(editor => editor.Remove(key));

    private void Commit(Action<ISharedPreferencesEditor> change)
    {
        using var editor = preferences.Edit()
            ?? throw new InvalidOperationException("Native preferences cannot be edited.");
        change(editor);
        // Core's synchronous contract must not report a successful durable save
        // before the platform store has accepted it.
        if (!editor.Commit())
        {
            logger.LogError("Android preferences could not be persisted");
            throw new IOException("Could not save app preferences.");
        }
    }
}
