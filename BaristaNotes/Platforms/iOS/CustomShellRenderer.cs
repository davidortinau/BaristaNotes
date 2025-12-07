using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using ShellSection = Microsoft.Maui.Controls.ShellSection;

namespace BaristaNotes.Platforms.iOS;

/// <summary>
/// Custom ShellRenderer that uses CustomShellSectionRenderer with PrefersLargeTitles support. 
/// </summary>
public class CustomShellRenderer : ShellRenderer
{
    /// <summary>
    /// Set this to true to enable large titles for the entire Shell.
    /// </summary>
    public static bool PrefersLargeTitles { get; set; } = true;

    protected override IShellSectionRenderer CreateShellSectionRenderer(ShellSection shellSection)
    {
        return new CustomShellSectionRenderer(this)
        {
            ShellSection = shellSection,
            PrefersLargeTitles = PrefersLargeTitles
        };
    }
}

