using BaristaNotes.Core.Models;

namespace BaristaNotes.Components;

internal static class DrinkValueRangeFormatting
{
    public static string MetricTitle(DrinkValueMetric metric) => metric switch
    {
        DrinkValueMetric.DoseIn => "Dose In",
        DrinkValueMetric.Yield => "Yield",
        DrinkValueMetric.GrindMicrons => "Grind Size",
        DrinkValueMetric.Time => "Time",
        _ => metric.ToString(),
    };

    public static string FormatRange(
        DrinkValueMetric metric,
        DrinkValueRange range) =>
        $"{FormatValue(metric, range.Minimum)} - {FormatValue(metric, range.Maximum)}";

    public static string FormatValue(DrinkValueMetric metric, decimal value) => metric switch
    {
        DrinkValueMetric.DoseIn or DrinkValueMetric.Yield => $"{value:0.#} g",
        DrinkValueMetric.GrindMicrons => $"{value:0} \u00B5m",
        DrinkValueMetric.Time => FormatDuration(value),
        _ => value.ToString("0.##"),
    };

    public static RangeEditorUnit GetEditorUnit(
        DrinkValueMetric metric,
        BrewMethod method) => metric switch
    {
        DrinkValueMetric.DoseIn or DrinkValueMetric.Yield => new(1, "grams", "0.#"),
        DrinkValueMetric.GrindMicrons => new(1, "microns", "0"),
        DrinkValueMetric.Time when method is BrewMethod.ColdBrew or BrewMethod.ColdDrip
            => new(3600, "hours", "0.##"),
        DrinkValueMetric.Time when method == BrewMethod.Espresso
            => new(1, "seconds", "0"),
        DrinkValueMetric.Time => new(60, "minutes", "0.##"),
        _ => new(1, string.Empty, "0.##"),
    };

    public static string FormatEditorValue(decimal canonicalValue, RangeEditorUnit editorUnit)
        => (canonicalValue / editorUnit.Scale).ToString(editorUnit.Format);

    private static string FormatDuration(decimal seconds)
    {
        var totalSeconds = (int)Math.Round(seconds);
        if (totalSeconds < 60)
        {
            return $"{totalSeconds} s";
        }

        if (totalSeconds < 3600)
        {
            var minutes = totalSeconds / 60;
            var remainder = totalSeconds % 60;
            return remainder == 0 ? $"{minutes} min" : $"{minutes}:{remainder:00}";
        }

        var hours = totalSeconds / 3600;
        var remainingMinutes = totalSeconds % 3600 / 60;
        return remainingMinutes == 0 ? $"{hours} h" : $"{hours} h {remainingMinutes} min";
    }
}

internal sealed record RangeEditorUnit(decimal Scale, string Label, string Format);
