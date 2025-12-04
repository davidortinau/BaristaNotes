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
                // Activity Feed is now the primary tab
                ShellContent("Activity")
                    .Icon(AppIcons.Feed)
                    .Route("history")
                    .RenderContent(() => new ActivityFeedPage()),

                // Shot Log remains as the second tab
                ShellContent("New Shot")
                    .Icon(AppIcons.CoffeeCup)
                    .Route("shots")
                    .RenderContent(() => new ShotLoggingPage())
            )
        );
    }
}
