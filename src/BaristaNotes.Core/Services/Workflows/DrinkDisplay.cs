using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Grind;

namespace BaristaNotes.Core.Services.Workflows;

public sealed record ShotRowText(string Title, string Ratio, string Subtitle, string Result);

public static class DrinkDisplay
{
    public static string GrindBadge(
        int? grinderId, string? grinderName, IReadOnlyList<GrindAnchor>? anchors, int microns)
    {
        if (!grinderId.HasValue)
            return "Select grinder for dial setting";
        if (anchors is null || anchors.Count < 2)
            return $"{grinderName ?? "Grinder"} \u00B7 Set up scale";

        var result = DeterministicGrindInterpolator.Interpolate(anchors, microns);
        return result?.Suggested is decimal setting
            ? $"{grinderName ?? "Grinder"} \u00B7 {Math.Round(setting, 1):0.#}"
            : $"{grinderName ?? "Grinder"} \u00B7 \u2014";
    }

    public static string RatingText(int rating)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rating);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rating, 4);
        return new string('\u2605', rating + 1) + new string('\u2606', 4 - rating);
    }

    public static string TimeValue(decimal seconds)
    {
        var rounded = (int)Math.Round((double)seconds);
        if (rounded < 60)
            return rounded.ToString("0");
        if (rounded < 3600)
            return $"{rounded / 60}:{rounded % 60:00}";

        var hours = rounded / 3600;
        var minutes = rounded % 3600 / 60;
        return minutes == 0 ? $"{hours}h" : $"{hours}h {minutes}m";
    }

    public static string? TimeUnit(decimal seconds) =>
        (int)Math.Round((double)seconds) < 60 ? "s" : null;

    public static double CelsiusToFahrenheit(decimal value) => (double)value * 9.0 / 5.0 + 32.0;

    public static decimal FahrenheitToCelsius(double value) =>
        Math.Round((decimal)((value - 32.0) * 5.0 / 9.0), 2);

    public static string WaterTemperatureValue(decimal? celsius, TemperatureUnit unit)
    {
        if (!celsius.HasValue)
            return "\u2014";
        return unit == TemperatureUnit.Fahrenheit
            ? $"{CelsiusToFahrenheit(celsius.Value):0}"
            : $"{celsius.Value:0.#}";
    }

    public static string? WaterTemperatureUnit(decimal? celsius, TemperatureUnit unit) =>
        !celsius.HasValue ? null : unit == TemperatureUnit.Fahrenheit ? "\u00B0F" : "\u00B0C";

    public static string? WaterTemperatureSecondary(decimal? celsius, TemperatureUnit unit)
    {
        if (!celsius.HasValue)
            return null;
        return unit == TemperatureUnit.Fahrenheit
            ? $"{celsius.Value:0.#} \u00B0C"
            : $"{CelsiusToFahrenheit(celsius.Value):0} \u00B0F";
    }

    public static string FormatTimestamp(DateTime timestamp, DateTime? localToday = null)
    {
        var local = timestamp.ToLocalTime();
        var today = (localToday ?? DateTime.Today).Date;
        if (local.Date == today)
            return $"Today {local:h:mm tt}";
        if (local.Date == today.AddDays(-1))
            return $"Yesterday {local:h:mm tt}";
        if (local.Date > today.AddDays(-7))
            return local.ToString("ddd h:mm tt");
        return local.ToString("MMM d, yyyy");
    }

    public static ShotRowText ActivityRow(ShotRecordDto shot, DateTime? localToday = null)
    {
        ArgumentNullException.ThrowIfNull(shot);
        var rating = shot.Rating.HasValue ? RatingText(Math.Clamp(shot.Rating.Value, 0, 4)) : "";
        var bean = shot.Bean?.Name ?? shot.Bag?.BeanName ?? "\u2014";
        var time = shot.ActualTime ?? shot.ExpectedTime;
        return new ShotRowText(
            shot.BrewMethod.DisplayName(),
            $"{shot.DoseIn:0.#}g \u2192 {shot.ActualOutput ?? shot.ExpectedOutput:0.#}g",
            $"{bean}  \u00B7  {FormatTimestamp(shot.Timestamp, localToday)}",
            $"{time:0}s   {rating}".Trim());
    }

    public static string RangeDescription(
        EffectiveDrinkValueRange range,
        decimal original,
        bool showsFullRange) =>
        !range.Range.Contains(original)
            ? "Current value is outside your preferred range."
            : showsFullRange
                ? "Showing the full allowed range."
                : "Showing your preferred range.";
}
