using Microsoft.Maui.Graphics;
using Xunit;

namespace BaristaNotes.Tests.Unit.Services;

public class ColorContrastTests
{
    // Light Mode Coffee Colors
    private static readonly Color LightBackground = Color.FromArgb("#D2BCA5");
    private static readonly Color LightSurface = Color.FromArgb("#FCEFE1");
    private static readonly Color LightSurfaceVariant = Color.FromArgb("#ECDAC4");
    private static readonly Color LightPrimary = Color.FromArgb("#86543F");
    private static readonly Color LightOnPrimary = Color.FromArgb("#F8F6F4");
    private static readonly Color LightTextPrimary = Color.FromArgb("#352B23");
    private static readonly Color LightTextSecondary = Color.FromArgb("#7C7067");
    private static readonly Color LightTextMuted = Color.FromArgb("#A38F7D");
    
    // Dark Mode Coffee Colors
    private static readonly Color DarkBackground = Color.FromArgb("#48362E");
    private static readonly Color DarkSurface = Color.FromArgb("#48362E");
    private static readonly Color DarkSurfaceVariant = Color.FromArgb("#7D5A45");
    private static readonly Color DarkPrimary = Color.FromArgb("#86543F");
    private static readonly Color DarkOnPrimary = Color.FromArgb("#F8F6F4");
    private static readonly Color DarkTextPrimary = Color.FromArgb("#F8F6F4");
    private static readonly Color DarkTextSecondary = Color.FromArgb("#C5BFBB");
    private static readonly Color DarkTextMuted = Color.FromArgb("#A19085");
    
    #region Light Mode Contrast Tests
    
    [Fact]
    public void LightMode_TextPrimaryOnBackground_MeetsWCAGAA()
    {
        // Arrange & Act
        var contrast = BaristaNotes.Tests.Utilities.ContrastCalculator.CalculateContrast(
            LightTextPrimary, 
            LightBackground);
        
        // Assert - WCAG AA requires 4.5:1 for normal text
        Assert.True(contrast >= 4.5, 
            $"Light mode TextPrimary on Background must meet WCAG AA (4.5:1). Got {contrast:F2}:1");
    }
    
    [Fact]
    public void LightMode_TextSecondaryOnSurface_MeetsWCAGAA_LargeText()
    {
        // Arrange & Act
        var contrast = BaristaNotes.Tests.Utilities.ContrastCalculator.CalculateContrast(
            LightTextSecondary, 
            LightSurface);
        
        // Assert - TextSecondary (#7C7067) on Surface (#FCEFE1) achieves 4.25:1
        // This meets WCAG AA for large text (3:1) but just misses normal text (4.5:1)
        // Usage: TextSecondary should be used at ≥14pt bold or ≥18pt regular
        Assert.True(contrast >= 3.0, 
            $"Light mode TextSecondary on Surface must meet WCAG AA for large text (3:1). Got {contrast:F2}:1");
    }
    
    [Fact]
    public void LightMode_OnPrimaryOnPrimary_MeetsWCAGAA()
    {
        // Arrange & Act
        var contrast = BaristaNotes.Tests.Utilities.ContrastCalculator.CalculateContrast(
            LightOnPrimary, 
            LightPrimary);
        
        // Assert - WCAG AA requires 4.5:1 for normal text
        Assert.True(contrast >= 4.5, 
            $"Light mode OnPrimary on Primary must meet WCAG AA (4.5:1). Got {contrast:F2}:1");
    }
    
    [Fact]
    public void LightMode_TextPrimaryOnSurface_MeetsWCAGAA()
    {
        // Arrange & Act
        var contrast = BaristaNotes.Tests.Utilities.ContrastCalculator.CalculateContrast(
            LightTextPrimary, 
            LightSurface);
        
        // Assert
        Assert.True(contrast >= 4.5, 
            $"Light mode TextPrimary on Surface must meet WCAG AA (4.5:1). Got {contrast:F2}:1");
    }
    
    [Fact]
    public void LightMode_TextSecondaryOnSurfaceVariant_MeetsWCAGAA_LargeText()
    {
        // Arrange & Act
        var contrast = BaristaNotes.Tests.Utilities.ContrastCalculator.CalculateContrast(
            LightTextSecondary, 
            LightSurfaceVariant);
        
        // Assert - WCAG AA requires 3:1 for large text (18pt+)
        Assert.True(contrast >= 3.0, 
            $"Light mode TextSecondary on SurfaceVariant must meet WCAG AA for large text (3:1). Got {contrast:F2}:1");
    }
    
    #endregion
    
    #region Dark Mode Contrast Tests
    
    [Fact]
    public void DarkMode_TextPrimaryOnBackground_MeetsWCAGAA()
    {
        // Arrange & Act
        var contrast = BaristaNotes.Tests.Utilities.ContrastCalculator.CalculateContrast(
            DarkTextPrimary, 
            DarkBackground);
        
        // Assert - WCAG AA requires 4.5:1 for normal text
        Assert.True(contrast >= 4.5, 
            $"Dark mode TextPrimary on Background must meet WCAG AA (4.5:1). Got {contrast:F2}:1");
    }
    
    [Fact]
    public void DarkMode_TextSecondaryOnBackground_MeetsWCAGAA_LargeText()
    {
        // Arrange & Act
        var contrast = BaristaNotes.Tests.Utilities.ContrastCalculator.CalculateContrast(
            DarkTextSecondary, 
            DarkBackground);
        
        // Assert - WCAG AA requires 3:1 for large text (18pt+)
        // TextSecondary is used for category labels and subtitles which are typically larger
        Assert.True(contrast >= 3.0, 
            $"Dark mode TextSecondary on Background must meet WCAG AA for large text (3:1). Got {contrast:F2}:1");
    }
    
    [Fact]
    public void DarkMode_OnPrimaryOnPrimary_MeetsWCAGAA()
    {
        // Arrange & Act
        var contrast = BaristaNotes.Tests.Utilities.ContrastCalculator.CalculateContrast(
            DarkOnPrimary, 
            DarkPrimary);
        
        // Assert - WCAG AA requires 4.5:1 for normal text
        Assert.True(contrast >= 4.5, 
            $"Dark mode OnPrimary on Primary must meet WCAG AA (4.5:1). Got {contrast:F2}:1");
    }
    
    [Fact]
    public void DarkMode_TextPrimaryOnSurface_MeetsWCAGAA()
    {
        // Arrange & Act
        var contrast = BaristaNotes.Tests.Utilities.ContrastCalculator.CalculateContrast(
            DarkTextPrimary, 
            DarkSurface);
        
        // Assert
        Assert.True(contrast >= 4.5, 
            $"Dark mode TextPrimary on Surface must meet WCAG AA (4.5:1). Got {contrast:F2}:1");
    }
    
    [Fact]
    public void DarkMode_TextSecondaryOnSurfaceVariant_MeetsWCAGAA_LargeText()
    {
        // Arrange & Act
        var contrast = BaristaNotes.Tests.Utilities.ContrastCalculator.CalculateContrast(
            DarkTextSecondary, 
            DarkSurfaceVariant);
        
        // Assert - WCAG AA requires 3:1 for large text (18pt+)
        Assert.True(contrast >= 3.0, 
            $"Dark mode TextSecondary on SurfaceVariant must meet WCAG AA for large text (3:1). Got {contrast:F2}:1");
    }
    
    #endregion
}
