using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using static MauiReactor.Component;
using Application = Microsoft.Maui.Controls.Application;

namespace BaristaNotes.Components;

/// <summary>
/// Tiles shared by the reorder agent pages (Agent, Proposal, Check-in,
/// Memory, Roasters). Same flat-tile language as SettingsPage: 1px divider
/// lines, a header tile with a micro label and a 28pt title, a scrolling
/// body of tiles, and a bottom row of icon tiles. Shared here so five
/// pages do not repeat the same 150 lines.
/// </summary>
static partial class AgentTiles
{
    // ------------------------------------------------------------
    // Theme helpers (same tokens as SettingsPage)
    // ------------------------------------------------------------

    public static bool IsLight() => Application.Current?.RequestedTheme != AppTheme.Dark;
    public static Color SurfaceColor() => IsLight() ? AppColors.Light.Surface : AppColors.Dark.Surface;
    public static Color TextPrimary() => IsLight() ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
    public static Color TextSecondary() => IsLight() ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
    public static Color AccentColor() => IsLight() ? AppColors.Light.Primary : AppColors.Dark.Primary;
    public static Color DividerColor() => IsLight() ? AppColors.Light.Outline : AppColors.Dark.Outline;
    public static Color ErrorColor() => AppColors.Error;

    static readonly SafeAreaEdges NoSafeArea =
        new(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None);

    // ------------------------------------------------------------
    // Page frame: header, body, optional footer, bottom nav
    // ------------------------------------------------------------

    public static MauiReactor.ContentPage PageFrame(
        string title,
        VisualNode header,
        VisualNode body,
        VisualNode? footer = null)
    {
        var children = new List<VisualNode> { header.GridRow(0), body.GridRow(1) };
        if (footer is not null)
        {
            children.Add(footer.GridRow(2));
        }
        children.Add(BottomNavRow().GridRow(footer is null ? 2 : 3));

        return ContentPage(title,
                Grid(rows: footer is null ? "Auto,*,Auto" : "Auto,*,Auto,Auto", columns: "*", children.ToArray())
                    .RowSpacing(1)
                    .BackgroundColor(DividerColor())
                    .Padding(1)
                    .SafeAreaEdges(NoSafeArea))
            .Set(MauiControls.Shell.NavBarIsVisibleProperty, false)
            .Set(MauiControls.Shell.TabBarIsVisibleProperty, false)
            .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Never));
    }

    public static VisualNode HeaderTile(string micro, string title, string? subtitle = null)
    {
        return Border(
            Grid(rows: "Auto,*,Auto", columns: "*",
                MicroLabel(micro).GridRow(0),
                Label(title)
                    .FontSize(28)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .LineBreakMode(LineBreakMode.WordWrap)
                    .MaxLines(2)
                    .VEnd()
                    .GridRow(1),
                string.IsNullOrEmpty(subtitle)
                    ? null
                    : Label(subtitle)
                        .FontSize(14)
                        .TextColor(TextSecondary())
                        .Margin(0, 4, 0, 0)
                        .GridRow(2)
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(120);
    }

    /// <summary>
    /// Scrolling body. Tiles sit on the divider color with 1px gaps. Null
    /// tiles are skipped.
    /// </summary>
    public static VisualNode ScrollBody(
        IEnumerable<VisualNode?> tiles,
        Action<MauiControls.ScrollView?>? scrollViewRef = null)
    {
        var items = tiles.OfType<VisualNode>().ToList();
        items.Add(
            Border()
                .BackgroundColor(SurfaceColor())
                .StrokeThickness(0)
                .StrokeShape(new Rectangle())
                .MinimumHeightRequest(16));

        var content = VStack(spacing: 1, items.ToArray())
            .BackgroundColor(DividerColor());

        return Grid("*", "*",
            scrollViewRef is null
                ? ScrollView(content)
                : ScrollView(scrollViewRef, content)
        )
        .BackgroundColor(SurfaceColor());
    }

    // ------------------------------------------------------------
    // Labels and tiles
    // ------------------------------------------------------------

    public static MauiReactor.Label MicroLabel(string text, Color? color = null) =>
        Label(text)
            .FontSize(10)
            .CharacterSpacing(2)
            .FontAttributes(MauiControls.FontAttributes.Bold)
            .TextColor(color ?? TextSecondary())
            .LineBreakMode(LineBreakMode.TailTruncation)
            .MaxLines(1);

    public static MauiReactor.Label Glyph(string glyph, Color color, double size = 24) =>
        AdaptiveTwoLineTile.DecorativeGlyph(glyph, color, size);

    public static VisualNode SectionLabel(string text)
    {
        return Border(
            MicroLabel(text).VEnd()
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .Padding(16, 24, 16, 10);
    }

    /// <summary>Micro label over a bold value. Tappable when <paramref name="onTap"/> is set.</summary>
    public static VisualNode InfoTile(
        string label,
        string value,
        Action? onTap = null,
        string? glyph = null,
        Color? glyphColor = null,
        string? status = null)
    {
        var tile = new AdaptiveTwoLineTile(
                MicroLabel(label),
                Label(value)
                    .FontSize(18)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .LineBreakMode(LineBreakMode.WordWrap))
            .BackgroundColor(SurfaceColor());

        if (glyph is not null)
        {
            tile.Leading(Glyph(glyph, glyphColor ?? TextPrimary()))
                .ColumnSpacing(12);
        }

        if (onTap is not null)
        {
            tile.Trailing(status is null
                    ? (VisualNode)Glyph(MaterialSymbolsFont.Chevron_right, TextPrimary())
                    : AdaptiveTwoLineTile.StatusWithChevron(status, AccentColor(), TextPrimary()))
                .OnTapped($"{label}: {value}", onTap)
                .AutomationId(AutomationIdFor(label));
        }
        else if (status is not null)
        {
            tile.Trailing(MicroLabel(status, AccentColor()).VCenter());
        }

        return tile;
    }

    /// <summary>A paragraph tile. Wraps. Optional leading glyph.</summary>
    public static VisualNode TextTile(string text, bool secondary = false, string? glyph = null, Color? glyphColor = null)
    {
        return Border(
            Grid(rows: "Auto", columns: glyph is null ? "*" : "Auto,*",
                glyph is null
                    ? null
                    : Glyph(glyph, glyphColor ?? TextPrimary(), 20).VStart().GridColumn(0),
                Label(text)
                    .FontSize(secondary ? 14 : 16)
                    .TextColor(secondary ? TextSecondary() : TextPrimary())
                    .LineBreakMode(LineBreakMode.WordWrap)
                    .GridColumn(glyph is null ? 0 : 1)
            )
            .ColumnSpacing(12)
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle());
    }

    /// <summary>
    /// A square action tile like the theme picker options: micro label over a
    /// large glyph. <paramref name="primary"/> inverts the colors. A busy tile
    /// shows a spinner and ignores taps.
    /// </summary>
    public static VisualNode ActionTile(
        string label,
        string glyph,
        Action onTap,
        bool primary = false,
        bool enabled = true,
        bool busy = false,
        string? accessibilityName = null)
    {
        var bg = primary ? TextPrimary() : SurfaceColor();
        var fg = primary ? SurfaceColor() : TextPrimary();

        var visual = Border(
            Grid(rows: "Auto,*", columns: "*",
                MicroLabel(label, fg.WithAlpha(0.7f))
                    .LineBreakMode(LineBreakMode.WordWrap)
                    .MaxLines(2)
                    .GridRow(0),
                busy
                    ? (VisualNode)ActivityIndicator()
                        .IsRunning(true)
                        .Color(fg)
                        .HStart()
                        .VEnd()
                        .GridRow(1)
                    : Glyph(glyph, fg, 28)
                        .VEnd()
                        .GridRow(1)
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(bg)
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(88)
        .Opacity(enabled ? 1 : 0.4)
        .Set(MauiControls.AutomationProperties.ExcludedWithChildrenProperty, true);

        return Grid(
            visual,
            TapTarget(accessibilityName ?? label, onTap, enabled && !busy));
    }

    /// <summary>Lays action tiles side by side with 1px dividers.</summary>
    public static VisualNode ActionRow(params VisualNode?[] tiles)
    {
        var items = tiles.OfType<VisualNode>().ToList();
        var columns = string.Join(",", Enumerable.Repeat("*", Math.Max(1, items.Count)));
        return Grid(rows: "Auto", columns: columns,
                items.Select((tile, index) => tile.GridColumn(index)).ToArray())
            .ColumnSpacing(1)
            .BackgroundColor(DividerColor());
    }

    /// <summary>Fills the body with a centered message, optional button.</summary>
    public static VisualNode MessageBody(string micro, string message, string? buttonText = null, Action? onButton = null)
    {
        return Border(
            VStack(spacing: 12,
                MicroLabel(micro).HCenter(),
                Label(message)
                    .FontSize(18)
                    .TextColor(TextPrimary())
                    .HorizontalTextAlignment(TextAlignment.Center)
                    .LineBreakMode(LineBreakMode.WordWrap)
                    .HCenter(),
                buttonText is null || onButton is null
                    ? null
                    : Button(buttonText)
                        .OnClicked(onButton)
                        .BackgroundColor(AccentColor())
                        .TextColor(SurfaceColor())
                        .Margin(0, 8, 0, 0)
                        .HCenter()
            ).VCenter().HCenter().Padding(24)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle());
    }

    public static VisualNode LoadingBody(string message = "Loading…")
    {
        return Border(
            VStack(spacing: 8,
                ActivityIndicator().IsRunning(true).Color(TextPrimary()),
                Label(message).FontSize(14).TextColor(TextSecondary()).HCenter()
            ).VCenter().HCenter()
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle());
    }

    /// <summary>
    /// Invisible full-tile button: keyboard, screen reader, and pointer activation.
    /// Its AutomationId comes from the accessible name, for example
    /// "kochere-natural-finished", so UI automation can find it.
    /// </summary>
    public static MauiReactor.Button TapTarget(string accessibilityName, Action onTap, bool enabled = true) =>
        Button("")
            .Padding(0)
            .BorderWidth(0)
            .CornerRadius(0)
            .BackgroundColor(Colors.Transparent)
            .TextColor(Colors.Transparent)
            .IsEnabled(enabled)
            .AutomationId(AutomationIdFor(accessibilityName))
            .Set(MauiControls.SemanticProperties.DescriptionProperty, accessibilityName)
            .OnClicked(onTap);

    [System.Text.RegularExpressions.GeneratedRegex(@"[\s,.;:]*selected[\s.]*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex SelectedSuffix();

    /// <summary>Lowercase words joined by dashes, without the "selected" state suffix.</summary>
    public static string AutomationIdFor(string accessibilityName)
    {
        var name = SelectedSuffix().Replace(accessibilityName, "");
        var chars = name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = string.Join("-", new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return slug.Length > 60 ? slug[..60].TrimEnd('-') : slug;
    }

    // ------------------------------------------------------------
    // Bottom nav: drinks, activity, back
    // ------------------------------------------------------------

    public static VisualNode BottomNavRow()
    {
        return Grid(rows: "Auto", columns: "*,*,*",
            NavTile(AppIcons.CoffeeCup, "New drink",
                async () => await MauiControls.Shell.Current.GoToAsync("//shots"))
                .GridColumn(0),
            NavTile(AppIcons.Feed, "Activity",
                async () => await MauiControls.Shell.Current.GoToAsync("//history"))
                .GridColumn(1),
            NavTile(BackIcon(), "Back",
                async () => await MauiControls.Shell.Current.GoToAsync(".."))
                .GridColumn(2)
        )
        .SafeAreaEdges(NoSafeArea)
        .ColumnSpacing(1)
        .BackgroundColor(DividerColor());
    }

    static FontImageSource BackIcon() => new()
    {
        FontFamily = MaterialSymbolsFont.FontFamily,
        Glyph = MaterialSymbolsFont.Arrow_back,
        Size = 32,
        Color = TextPrimary(),
    };

    static VisualNode NavTile(FontImageSource imageSource, string accessibilityName, Action onTap)
    {
        return Grid(
            Border(
                Image()
                    .Source(imageSource)
                    .HCenter()
                    .VCenter()
            )
            .BackgroundColor(SurfaceColor())
            .StrokeThickness(0)
            .StrokeShape(new Rectangle())
            .MinimumHeightRequest(72)
            .Padding(16, 18, 16, 30)
            .Set(MauiControls.AutomationProperties.ExcludedWithChildrenProperty, true),
            TapTarget(accessibilityName, onTap));
    }

    // ------------------------------------------------------------
    // Confirmation (UXDivers popup, same as the delete confirmations)
    // ------------------------------------------------------------

    /// <summary>Shows a confirm popup. Runs <paramref name="onConfirm"/> after it closes.</summary>
    public static Task ConfirmAsync(string title, string text, string actionText, Func<Task> onConfirm)
    {
        var popup = new SimpleActionPopup
        {
            Title = title,
            Text = text,
            ActionButtonText = actionText,
            SecondaryActionButtonText = "Cancel",
            ActionButtonCommand = new Command(async () =>
            {
                await IPopupService.Current.PopAsync();
                await onConfirm();
            }),
        };

        return IPopupService.Current.PushAsync(popup);
    }
}
