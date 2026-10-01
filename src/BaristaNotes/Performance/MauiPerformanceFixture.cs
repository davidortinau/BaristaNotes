#if MAUI_PERFORMANCE
using System.Globalization;
#if IOS
using Foundation;
#endif
using Microsoft.Extensions.DependencyInjection;

namespace BaristaNotes;

internal static class MauiPerformanceFixture
{
    public static int Dataset { get; private set; }

    public static string PrepareDatabase()
    {
        var values = ReadMetadata();
        Dataset = int.Parse(values["dataset"], CultureInfo.InvariantCulture);
        var destination = Path.Combine(
            FileSystem.AppDataDirectory,
            $"barista_notes_maui_perf_{Dataset}.db");
        if (!File.Exists(destination))
        {
            using var source = OpenBundledFile("barista_notes.db");
            using var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            source.CopyTo(output);
            output.Flush(true);
        }
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
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        using var stream = OpenBundledFile("preferences.txt");
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            var separator = line.IndexOf('=');
            if (separator > 0)
                values.Add(line[..separator], line[(separator + 1)..]);
        }
        return values;
    }

    private static Stream OpenBundledFile(string fileName)
    {
#if IOS
        var path = NSBundle.MainBundle.PathForResource(
            Path.GetFileNameWithoutExtension(fileName),
            Path.GetExtension(fileName).TrimStart('.'),
            "performance")
            ?? throw new FileNotFoundException(
                $"The MAUI performance resource is not bundled: {fileName}");
        return File.OpenRead(path);
#elif ANDROID
        return Android.App.Application.Context.Assets?.Open($"performance/{fileName}")
            ?? throw new FileNotFoundException(
                $"The MAUI performance resource is not bundled: {fileName}");
#else
        throw new PlatformNotSupportedException();
#endif
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

internal sealed class MauiPerformancePreferencesStore : IPreferencesStore
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
