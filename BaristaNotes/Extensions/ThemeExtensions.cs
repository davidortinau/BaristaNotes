using BaristaNotes.Styles;

namespace BaristaNotes.Extensions;

/// <summary>
/// Helper methods for getting theme-aware colors.
/// </summary>
public static class ThemeColors
{
    public static Color Surface => 
        ApplicationTheme.IsLightTheme 
            ? AppColors.Light.Surface 
            : AppColors.Dark.Surface;
    
    public static Color SurfaceVariant => 
        ApplicationTheme.IsLightTheme 
            ? AppColors.Light.SurfaceVariant 
            : AppColors.Dark.SurfaceVariant;
    
    public static Color TextPrimary => 
        ApplicationTheme.IsLightTheme 
            ? AppColors.Light.TextPrimary 
            : AppColors.Dark.TextPrimary;
    
    public static Color TextSecondary => 
        ApplicationTheme.IsLightTheme 
            ? AppColors.Light.TextSecondary 
            : AppColors.Dark.TextSecondary;
    
    public static Color TextMuted => 
        ApplicationTheme.IsLightTheme 
            ? AppColors.Light.TextMuted 
            : AppColors.Dark.TextMuted;
    
    public static Color Outline => 
        ApplicationTheme.IsLightTheme 
            ? AppColors.Light.Outline 
            : AppColors.Dark.Outline;
    
    public static Color Primary => 
        ApplicationTheme.IsLightTheme 
            ? AppColors.Light.Primary 
            : AppColors.Dark.Primary;
}
