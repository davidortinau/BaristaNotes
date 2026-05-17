using MauiReactor;
using BaristaNotes.Pages;
using BaristaNotes.Styles;
using Microsoft.Maui.Storage;

namespace BaristaNotes;

public class AppShell : Component
{
    /// <summary>
    /// Preferences key for opting into the experimental grid-style logging layout.
    /// Default false → existing ShotLoggingPage. True → ShotLoggingGridPage.
    /// </summary>
    public const string GridLayoutPreferenceKey = "Pref:UseGridLoggingLayout";

    public static bool UseGridLoggingLayout
        => Preferences.Default.Get(GridLayoutPreferenceKey, false);

    public override VisualNode Render()
    {
        return Shell(
            TabBar(
                // Drink log remains as the second tab
                ShellContent("New Drink")
                    .Icon(AppIcons.CoffeeCup)
                    .Route("shots")
                    .RenderContent(() => UseGridLoggingLayout
                        ? (Component)new ShotLoggingGridPage()
                        : new ShotLoggingPage()),

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
        )
        .BackgroundColor(Colors.Transparent)
        .FlyoutBehavior(FlyoutBehavior.Disabled);
    }
}
