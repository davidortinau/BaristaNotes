using MauiReactor;
using BaristaNotes.Pages;
using BaristaNotes.Styles;

namespace BaristaNotes;

public class AppShell : Component
{
    public override VisualNode Render()
    {
        // TabBar wrapper is REQUIRED on iOS — without it, Shell with multiple
        // bare ShellContents doesn't pick a default route on iOS published
        // builds (blank black screen on device). The tab bar itself is hidden
        // by each page setting Shell.TabBarIsVisibleProperty=false; this
        // wrapper exists purely to give Shell the structure it needs to
        // resolve "//shots", "//history", and "//settings".
        return Shell(
            TabBar(
                ShellContent("New Drink")
                    .Icon(AppIcons.CoffeeCup)
                    .Route("shots")
                    .RenderContent(() => new ShotLoggingGridPage()),

                ShellContent("Activity")
                    .Icon(AppIcons.Feed)
                    .Route("history")
                    .RenderContent(() => new ActivityFeedPage()),

                ShellContent("Settings")
                    .Icon(AppIcons.Settings)
                    .Route("settings")
                    .RenderContent(() => new SettingsPage())
            )
        )
        .BackgroundColor(Colors.Transparent)
        .FlyoutBehavior(FlyoutBehavior.Disabled);
    }
}
