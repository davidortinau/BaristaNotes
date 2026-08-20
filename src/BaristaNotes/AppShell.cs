using MauiReactor;

namespace BaristaNotes;

public class AppShellState
{
    public bool IsDatabaseReady { get; set; }
    public string? InitializationError { get; set; }
}

public partial class AppShell : Component<AppShellState>
{
    [Inject] DatabaseInitializationService _databaseInitialization;
    [Inject] ILogger<AppShell> _logger;

    protected override void OnMounted()
    {
        base.OnMounted();
        _ = InitializeDatabaseAsync();
    }

    private async Task InitializeDatabaseAsync()
    {
        SetState(s => s.InitializationError = null);

        try
        {
            await _databaseInitialization.InitializeAsync();
            SetState(s => s.IsDatabaseReady = true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database initialization failed");
            SetState(s => s.InitializationError = ex.Message);
        }
    }

    public override VisualNode Render()
    {
        if (!State.IsDatabaseReady)
        {
            return RenderInitializationShell();
        }

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

    private VisualNode RenderInitializationShell()
    {
        var content = State.InitializationError is null
            ? (VisualNode)VStack(
                    ActivityIndicator().IsRunning(true),
                    Label("Preparing your data").ThemeKey(ThemeKeys.SecondaryText)
                )
                .Spacing(AppSpacing.S)
                .VCenter()
                .HCenter()
            : VStack(
                    Label("Database unavailable").ThemeKey(ThemeKeys.Headline),
                    Label(State.InitializationError).ThemeKey(ThemeKeys.SecondaryText),
                    Button("Try Again").OnClicked(InitializeDatabaseAsync)
                )
                .Spacing(AppSpacing.S)
                .Padding(AppSpacing.M)
                .VCenter()
                .HCenter();

        return Shell(
                ShellContent("Starting")
                    .Route("starting")
                    .RenderContent(() => ContentPage(content))
            )
            .BackgroundColor(Colors.Transparent)
            .FlyoutBehavior(FlyoutBehavior.Disabled);
    }
}
