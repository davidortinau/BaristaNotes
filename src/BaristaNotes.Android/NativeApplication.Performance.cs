#if NATIVE_PERFORMANCE
using System.Globalization;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BaristaNotes.AndroidApp;

public sealed partial class NativeApplication
{
    internal static int PerformanceDataset { get; private set; }

    partial void ConfigurePerformanceDatabase(string dataDirectory, ref string databasePath)
    {
        var values = ReadPerformancePreferences();
        PerformanceDataset = int.Parse(values["dataset"], CultureInfo.InvariantCulture);
        var performancePath = Path.Combine(dataDirectory, $"barista_notes_perf_{PerformanceDataset}.db");
        if (!File.Exists(performancePath))
        {
            using var source = Assets!.Open("performance/barista_notes.db");
            using var destination = new FileStream(
                performancePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            source.CopyTo(destination);
            destination.Flush(true);
        }

        databasePath = performancePath;
    }

    partial void ConfigurePerformancePreferences()
    {
        var values = ReadPerformancePreferences();
        var services = _services
            ?? throw new InvalidOperationException("Performance preferences require initialized services.");
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

    private Dictionary<string, string> ReadPerformancePreferences()
    {
        using var stream = Assets!.Open("performance/preferences.txt");
        using var reader = new StreamReader(stream);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
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
#endif
