using MauiReactor;
using BaristaNotes.Pages;
using BaristaNotes.Styles;

namespace BaristaNotes;

public class AppShell : Component
{
    public override VisualNode Render()
    {
        return Shell(
            TabBar(
                // Shot Log remains as the second tab
                ShellContent("New Shot")
                    .Icon(AppIcons.CoffeeCup)
                    .Route("shots")
                    .RenderContent(() => new ShotLoggingPage()),

                // Activity Feed is now the primary tab
                ShellContent("Activity")
                    .Icon(AppIcons.Feed)
                    .Route("history")
                    .RenderContent(() => new ActivityFeedPage()),


ShellContent("Settings")
                    .Icon(AppIcons.Settings)
                    .Route("settings")
                    .RenderContent(() => new SettingsPage())
            )
        );
    }
}
