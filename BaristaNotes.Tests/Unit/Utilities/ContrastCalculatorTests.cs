using Microsoft.Maui.Graphics;
using Xunit;

namespace BaristaNotes.Tests.Unit.Utilities;

public class ContrastCalculatorTests
{
    [Fact]
    public void CalculateContrast_BlackOnWhite_Returns21to1()
    {
        // Arrange
        var black = Colors.Black;
        var white = Colors.White;
        
        // Act
        var contrast = BaristaNotes.Tests.Utilities.ContrastCalculator.CalculateContrast(black, white);
        
        // Assert
        Assert.True(contrast >= 20.9, $"Expected contrast >= 20.9, got {contrast}");
    }
    
    [Fact]
    public void CalculateContrast_WhiteOnBlack_Returns21to1()
    {
        // Arrange
        var white = Colors.White;
        var black = Colors.Black;
        
        // Act
        var contrast = BaristaNotes.Tests.Utilities.ContrastCalculator.CalculateContrast(white, black);
        
        // Assert
        Assert.True(contrast >= 20.9, $"Expected contrast >= 20.9, got {contrast}");
    }
    
    [Fact]
    public void CalculateContrast_SameColor_Returns1to1()
    {
        // Arrange
        var color = Colors.Gray;
        
        // Act
        var contrast = BaristaNotes.Tests.Utilities.ContrastCalculator.CalculateContrast(color, color);
        
        // Assert
        Assert.True(contrast >= 0.99 && contrast <= 1.01, $"Expected contrast ≈ 1.0, got {contrast}");
    }
    
    [Fact]
    public void CalculateContrast_MeetsWCAGAA_NormalText()
    {
        // Arrange - WCAG AA requires 4.5:1 for normal text
        var darkGray = Color.FromArgb("#595959");
        var white = Colors.White;
        
        // Act
        var contrast = BaristaNotes.Tests.Utilities.ContrastCalculator.CalculateContrast(darkGray, white);
        
        // Assert
        Assert.True(contrast >= 4.5, $"Expected contrast >= 4.5:1 for WCAG AA normal text, got {contrast}");
    }
    
    [Fact]
    public void CalculateContrast_MeetsWCAGAA_LargeText()
    {
        // Arrange - WCAG AA requires 3:1 for large text
        var lightGray = Color.FromArgb("#767676");
        var white = Colors.White;
        
        // Act
        var contrast = BaristaNotes.Tests.Utilities.ContrastCalculator.CalculateContrast(lightGray, white);
        
        // Assert
        Assert.True(contrast >= 3.0, $"Expected contrast >= 3.0:1 for WCAG AA large text, got {contrast}");
    }
}
