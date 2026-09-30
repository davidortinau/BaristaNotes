namespace BaristaNotes.Core.Models.Enums;

/// <summary>
/// Represents the user's theme mode preference.
/// </summary>
public enum ThemeMode
{
    /// <summary>
    /// Use light theme regardless of system setting.
    /// </summary>
    Light,
    
    /// <summary>
    /// Use dark theme regardless of system setting.
    /// </summary>
    Dark,
    
    /// <summary>
    /// Follow the device's system theme setting.
    /// </summary>
    System
}
