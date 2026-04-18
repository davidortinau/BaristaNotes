using Microsoft.Maui.Graphics;

namespace BaristaNotes.Tests.Utilities;

/// <summary>
/// Utility class for calculating WCAG 2.1 contrast ratios between colors.
/// </summary>
public static class ContrastCalculator
{
    /// <summary>
    /// Calculates the contrast ratio between two colors using WCAG 2.1 formula.
    /// </summary>
    /// <param name="foreground">The foreground color (typically text).</param>
    /// <param name="background">The background color.</param>
    /// <returns>The contrast ratio (1.0 to 21.0), where higher is better contrast.</returns>
    public static double CalculateContrast(Color foreground, Color background)
    {
        double l1 = GetRelativeLuminance(foreground);
        double l2 = GetRelativeLuminance(background);
        
        double lighter = Math.Max(l1, l2);
        double darker = Math.Min(l1, l2);
        
        return (lighter + 0.05) / (darker + 0.05);
    }
    
    /// <summary>
    /// Gets the relative luminance of a color according to WCAG 2.1 specification.
    /// </summary>
    private static double GetRelativeLuminance(Color color)
    {
        double r = GetLuminanceComponent(color.Red);
        double g = GetLuminanceComponent(color.Green);
        double b = GetLuminanceComponent(color.Blue);
        
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }
    
    /// <summary>
    /// Applies gamma correction to a color component.
    /// </summary>
    private static double GetLuminanceComponent(float component)
    {
        double c = component;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
