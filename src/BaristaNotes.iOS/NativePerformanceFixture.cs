#if NATIVE_PERFORMANCE
using System.Globalization;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using Foundation;
using Microsoft.Extensions.DependencyInjection;

namespace BaristaNotes.Native.iOS;

internal static class NativePerformanceFixture
{
    public static int Dataset { get; private set; }

    public static string PrepareDatabase(string dataDirectory)
    {
        var values = ReadMetadata();
        Dataset = int.Parse(values["dataset"], CultureInfo.InvariantCulture);
        var source = NSBundle.MainBundle.PathForResource("barista_notes", "db", "performance")
            ?? throw new FileNotFoundException("The iOS performance database is not bundled.");
        var destination = Path.Combine(dataDirectory, $"barista_notes_perf_{Dataset}.db");
        if (!File.Exists(destination))
            File.Copy(source, destination);
        return destination;
    }

    public static void ConfigurePreferences(IServiceProvider services)
    {
        var values = ReadMetadata();
        var preferences = services.GetRequiredService<IPreferencesService>();
        preferences.SetLastDrinkType(values["drinkType"]);
        preferences.SetLastBeanId(Integer(values, "beanId"));
        preferences.SetLastBagId(Integer(values, "bagId"));
        preferences.SetLastMachineId(Integer(values, "machineId"));
        preferences.SetLastGrinderId(Integer(values, "grinderId"));
        preferences.SetLastAccessoryIds([]);
        preferences.SetLastMadeById(Integer(values, "madeById"));
        preferences.SetLastMadeForId(Integer(values, "madeForId"));
        preferences.SetLastDoseIn(Decimal(values, "doseIn"));
        preferences.SetLastGrindMicrons(Integer(values, "grindMicrons"));
        preferences.SetLastExpectedTime(Decimal(values, "expectedTime"));
        preferences.SetLastExpectedOutput(Decimal(values, "expectedOutput"));
        preferences.SetLastPreinfusionTime(Decimal(values, "preinfusionTime"));
        preferences.SetTemperatureUnit(TemperatureUnit.Celsius);
        ThemePreference.Write(services.GetRequiredService<IPreferencesStore>(), ThemeMode.Light);
    }

    private static Dictionary<string, string> ReadMetadata()
    {
        var path = NSBundle.MainBundle.PathForResource("preferences", "txt", "performance")
            ?? throw new FileNotFoundException("The iOS performance metadata is not bundled.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path))
        {
            var separator = line.IndexOf('=');
            if (separator > 0)
                values.Add(line[..separator], line[(separator + 1)..]);
        }
        return values;
    }

    private static int? Integer(IReadOnlyDictionary<string, string> values, string key) =>
        string.IsNullOrEmpty(values[key])
            ? null
            : int.Parse(values[key], CultureInfo.InvariantCulture);

    private static decimal? Decimal(IReadOnlyDictionary<string, string> values, string key) =>
        string.IsNullOrEmpty(values[key])
            ? null
            : decimal.Parse(values[key], CultureInfo.InvariantCulture);
}

internal sealed class NativePerformancePreferencesStore : IPreferencesStore
{
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

    public string? Get(string key, string? defaultValue) =>
        _values.TryGetValue(key, out var value) ? (string)value : defaultValue;

    public int Get(string key, int defaultValue) =>
        _values.TryGetValue(key, out var value) ? (int)value : defaultValue;

    public double Get(string key, double defaultValue) =>
        _values.TryGetValue(key, out var value) ? (double)value : defaultValue;

    public void Set(string key, string value) => _values[key] = value;
    public void Set(string key, int value) => _values[key] = value;
    public void Set(string key, double value) => _values[key] = value;
    public void Remove(string key) => _values.Remove(key);
}
#endif
