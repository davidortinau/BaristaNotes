using MauiReactor;
using BaristaNotes.Pages;
using MauiControls = Microsoft.Maui.Controls;

namespace BaristaNotes;

public class AppShell : Component
{
    public override VisualNode Render()
    {
        return Shell(
            TabBar(
                // Activity Feed is now the primary tab
                ShellContent("Activity")
                    .Icon("list.png")
                    .Route("history")
                    .RenderContent(() => new ActivityFeedPage()),

                // Shot Log remains as the second tab
                ShellContent("New Shot")
                    .Icon("coffee.png")
                    .Route("shots")
                    .RenderContent(() => new ShotLoggingPage())
            )
        );
    }
}
