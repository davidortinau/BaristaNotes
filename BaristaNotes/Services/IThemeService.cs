namespace BaristaNotes.Services;

/// <summary>
/// Encapsulates theme mode management, preference persistence, and theme application logic.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets the current theme mode (Light, Dark, or System).
    /// </summary>
    ThemeMode CurrentMode { get; }
    
    /// <summary>
    /// Gets the current effective theme (Light or Dark), resolving System mode to actual OS theme.
    /// </summary>
    AppTheme CurrentTheme { get; }
    
    /// <summary>
    /// Asynchronously retrieves the user's saved theme mode preference.
    /// Returns ThemeMode.System if no preference is saved.
    /// </summary>
    Task<ThemeMode> GetThemeModeAsync();
    
    /// <summary>
    /// Asynchronously saves the user's theme mode preference and applies the new theme.
    /// </summary>
    /// <param name="mode">The theme mode to save and apply.</param>
    Task SetThemeModeAsync(ThemeMode mode);
    
    /// <summary>
    /// Forces re-application of the current theme.
    /// Useful when responding to system theme changes in System mode.
    /// </summary>
    void ApplyTheme();
}
