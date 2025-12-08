using BaristaNotes.Services;
using MauiReactor;

namespace BaristaNotes;

public partial class BaristaApp : Component
{
    [Inject] IThemeService _themeService;

    protected override void OnMounted()
    {
        base.OnMounted();
        // Apply the saved theme now that Application.Current is available
        _themeService.ApplyTheme();
    }

    public override VisualNode Render()
    {
        return new AppShell();
    }
}
