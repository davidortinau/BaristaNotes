using MauiReactor;
using BaristaNotes.Components.Feedback;

namespace BaristaNotes;

public class App : Component
{
    public override VisualNode Render()
    {
        return Grid(
            new AppShell(),
            new FeedbackOverlay()
        );
    }
}
