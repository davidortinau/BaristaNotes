namespace BaristaNotes.Core.Models.Enums;

/// <summary>
/// User-selectable unit for displaying and entering water temperature.
/// Storage is always canonical Celsius (<see cref="ShotRecord.WaterTempC"/>);
/// this only affects display and the picker scale.
/// </summary>
public enum TemperatureUnit
{
    Fahrenheit = 0,
    Celsius = 1
}
