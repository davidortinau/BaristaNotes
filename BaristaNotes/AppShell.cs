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
                    .RenderContent(() => new EquipmentManagementPage()),
                
                ShellContent("Beans")
                    .Icon("bean.png")
                    .Route("beans")
                    .RenderContent(() => new BeanManagementPage()),
                
                ShellContent("Profiles")
                    .Icon("person.png")
                    .Route("profiles")
                    .RenderContent(() => new UserProfileManagementPage())
            )
        );
    }
}

