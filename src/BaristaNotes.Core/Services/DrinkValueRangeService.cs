using System.Text.Json;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services;

public sealed class DrinkValueRangeService : IDrinkValueRangeService
{
    private readonly IPreferencesService _preferences;
    private readonly ILogger<DrinkValueRangeService> _logger;
    private readonly object _sync = new();
    private DrinkValueRangeSettings? _settings;
    private string? _loadWarning;

    public event EventHandler? SettingsChanged;

    public DrinkValueRangeService(
        IPreferencesService preferences,
        ILogger<DrinkValueRangeService> logger)
    {
        _preferences = preferences;
        _logger = logger;
    }

    public DrinkValueRangeSettingsSnapshot GetSettings()
    {
        lock (_sync)
        {
            var settings = GetOrLoadSettings();
            return new(
                new Dictionary<DrinkValueMetric, ValueRangeMode>(settings.Modes),
                settings.Overrides.ToArray(),
                _loadWarning);
        }
    }

    public ValueRangeMode GetMode(DrinkValueMetric metric)
    {
        lock (_sync)
        {
            var settings = GetOrLoadSettings();
            return settings.Modes.GetValueOrDefault(metric, ValueRangeMode.Auto);
        }
    }

    public EffectiveDrinkValueRange Resolve(DrinkValueMetric metric, BrewMethod method)
    {
        lock (_sync)
        {
            var definition = BrewMethodValueRangeCatalog.GetDefinition(method, metric);
            var settings = GetOrLoadSettings();
            var mode = settings.Modes.GetValueOrDefault(metric, ValueRangeMode.Auto);
            var custom = mode == ValueRangeMode.Custom
                ? settings.Overrides.LastOrDefault(item => item.Metric == metric && item.Method == method)
                : null;

            var range = custom is null
                ? definition.AutoRange
                : new DrinkValueRange(custom.Minimum, custom.Maximum);
            var source = custom is not null
                ? ValueRangeSource.Custom
                : mode == ValueRangeMode.Custom
                    ? ValueRangeSource.AutoFallback
                    : ValueRangeSource.Auto;

            return new(
                range,
                definition.HardRange,
                range.Clamp(definition.Default),
                definition.Step,
                definition.CanonicalUnit,
                source);
        }
    }

    public void SetMode(DrinkValueMetric metric, ValueRangeMode mode)
    {
        lock (_sync)
        {
            var settings = GetOrLoadSettings();
            settings.Modes[metric] = mode;
            Save(settings);
        }
    }

    public void SaveOverride(
        DrinkValueMetric metric,
        BrewMethod method,
        decimal minimum,
        decimal maximum)
    {
        ValidateOverride(metric, method, minimum, maximum);

        lock (_sync)
        {
            var settings = GetOrLoadSettings();
            settings.Overrides.RemoveAll(item => item.Metric == metric && item.Method == method);
            settings.Overrides.Add(new(metric, method, minimum, maximum));
            settings.Modes[metric] = ValueRangeMode.Custom;
            Save(settings);
        }
    }

    public void RemoveOverride(DrinkValueMetric metric, BrewMethod method)
    {
        lock (_sync)
        {
            var settings = GetOrLoadSettings();
            settings.Overrides.RemoveAll(item => item.Metric == metric && item.Method == method);
            Save(settings);
        }
    }

    public void ResetOverrides(DrinkValueMetric metric)
    {
        lock (_sync)
        {
            var settings = GetOrLoadSettings();
            settings.Overrides.RemoveAll(item => item.Metric == metric);
            Save(settings);
        }
    }

    private DrinkValueRangeSettings GetOrLoadSettings()
    {
        if (_settings is not null)
        {
            return _settings;
        }

        var json = _preferences.GetDrinkValueRangeSettingsJson();
        if (string.IsNullOrWhiteSpace(json))
        {
            return _settings = new();
        }

        try
        {
            var settings = JsonSerializer.Deserialize(
                    json,
                    DrinkValueRangeJsonContext.Default.DrinkValueRangeSettings)
                ?? throw new JsonException("The range settings document was empty.");

            if (settings.SchemaVersion != DrinkValueRangeSettings.CurrentSchemaVersion)
            {
                _loadWarning = "Custom ranges use an unsupported format. Automatic ranges are active.";
                _logger.LogWarning(
                    "Unsupported drink value range settings schema version {SchemaVersion}",
                    settings.SchemaVersion);
                return _settings = new();
            }

            if (settings.Modes is null
                || settings.Overrides is null
                || settings.Overrides.Any(item => item is null))
            {
                throw new JsonException("The range settings document has invalid collections.");
            }

            foreach (var item in settings.Overrides)
            {
                ValidateOverride(item.Metric, item.Method, item.Minimum, item.Maximum);
            }

            return _settings = settings;
        }
        catch (JsonException ex)
        {
            return UseAutomaticRanges(
                ex,
                "Custom range settings could not be read. Automatic ranges are active.");
        }
        catch (ArgumentException ex)
        {
            return UseAutomaticRanges(
                ex,
                "Custom range settings are invalid. Automatic ranges are active.");
        }
    }

    private DrinkValueRangeSettings UseAutomaticRanges(Exception exception, string warning)
    {
        _loadWarning = warning;
        _logger.LogWarning(exception, "Failed to load drink value range settings");
        return _settings = new();
    }

    private void Save(DrinkValueRangeSettings settings)
    {
        var json = JsonSerializer.Serialize(
            settings,
            DrinkValueRangeJsonContext.Default.DrinkValueRangeSettings);
        _preferences.SetDrinkValueRangeSettingsJson(json);
        _settings = settings;
        _loadWarning = null;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void ValidateOverride(
        DrinkValueMetric metric,
        BrewMethod method,
        decimal minimum,
        decimal maximum)
    {
        var definition = BrewMethodValueRangeCatalog.GetDefinition(method, metric);
        if (minimum >= maximum)
        {
            throw new ArgumentException("Minimum must be less than maximum.");
        }

        if (!definition.HardRange.Contains(minimum) || !definition.HardRange.Contains(maximum))
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimum),
                $"Range must stay between {definition.HardRange.Minimum} and {definition.HardRange.Maximum}.");
        }

        var usesTenths = metric is DrinkValueMetric.DoseIn or DrinkValueMetric.Yield;
        if (usesTenths)
        {
            if (decimal.Round(minimum, 1) != minimum || decimal.Round(maximum, 1) != maximum)
            {
                throw new ArgumentException("Dose and yield ranges support one decimal place.");
            }
        }
        else if (decimal.Truncate(minimum) != minimum || decimal.Truncate(maximum) != maximum)
        {
            throw new ArgumentException("Grind and time ranges use whole numbers.");
        }
    }
}
