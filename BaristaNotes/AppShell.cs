using MauiReactor;
using BaristaNotes.Pages;

namespace BaristaNotes;

public class AppShell : Component
{
    public override VisualNode Render()
    {
        return Shell(
            TabBar(
                ShellContent("Shot Log")
                    .Icon("coffee.png")
                    .Route("shots")
                    .RenderContent(() => new ShotLoggingPage()),
                
                ShellContent("History")
                    .Icon("list.png")
                    .Route("history")
                    .RenderContent(() => new ActivityFeedPage()),
                
                ShellContent("Equipment")
                    .Icon("gear.png")
                    .Route("equipment")
                    .RenderContent(() => new PlaceholderPage().Title("Equipment Management")),
                
                ShellContent("Beans")
                    .Icon("bean.png")
                    .Route("beans")
                    .RenderContent(() => new PlaceholderPage().Title("Bean Management"))
            )
        );
    }
}

partial class PlaceholderPage : Component
{
    [Prop]
    string _title = string.Empty;

    public override VisualNode Render()
    {
        return ContentPage(_title,
            VStack(spacing: 16,
                Label($"{_title} Page")
                    .FontSize(24)
                    .HCenter(),
                Label("Coming soon...")
                    .FontSize(16)
                    .HCenter()
            )
            .VCenter()
            .HCenter()
        );
    }
}
