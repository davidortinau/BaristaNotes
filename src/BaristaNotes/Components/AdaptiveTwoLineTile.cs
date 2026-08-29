using Application = Microsoft.Maui.Controls.Application;

namespace BaristaNotes.Components;

internal sealed class AdaptiveTwoLineTileState
{
    public bool IsFocused { get; set; }
}

internal sealed class AdaptiveTwoLineTile : Component<AdaptiveTwoLineTileState>
{
    private readonly VisualNode _label;
    private readonly VisualNode _value;
    private VisualNode? _leading;
    private VisualNode? _trailing;
    private Color? _backgroundColor;
    private double _minimumHeight = 80;
    private double _columnSpacing = AppSpacing.S;
    private string? _automationId;
    private Action? _onTapped;
    private string? _accessibilityName;
    private Thickness? _margin;

    public AdaptiveTwoLineTile(VisualNode label, VisualNode value)
    {
        _label = label;
        _value = value;
    }

    public static MauiReactor.Label DecorativeGlyph(
        string glyph,
        Color color,
        double fontSize = 24) =>
        Label(glyph)
            .FontFamily(MaterialSymbolsFont.FontFamily)
            .FontSize(fontSize)
            .VerticalTextAlignment(TextAlignment.Center)
            .Set(MauiControls.Label.FontAutoScalingEnabledProperty, false)
            .Set(MauiControls.AutomationProperties.ExcludedWithChildrenProperty, true)
            .TextColor(color);

    public static MauiReactor.Grid StatusWithChevron(
        string status,
        Color statusColor,
        Color chevronColor) =>
        Grid(rows: "Auto", columns: "Auto,Auto",
            Label(status)
                .FontSize(10)
                .CharacterSpacing(1)
                .VerticalTextAlignment(TextAlignment.Center)
                .TextColor(statusColor)
                .VCenter()
                .GridColumn(0),
            DecorativeGlyph(MaterialSymbolsFont.Chevron_right, chevronColor)
                .VCenter()
                .GridColumn(1)
        )
        .ColumnSpacing(AppSpacing.S)
        .VCenter();

    public AdaptiveTwoLineTile Leading(VisualNode leading)
    {
        _leading = leading;
        return this;
    }

    public AdaptiveTwoLineTile Trailing(VisualNode trailing)
    {
        _trailing = trailing;
        return this;
    }

    public AdaptiveTwoLineTile BackgroundColor(Color color)
    {
        _backgroundColor = color;
        return this;
    }

    public AdaptiveTwoLineTile MinimumHeight(double height)
    {
        _minimumHeight = height;
        return this;
    }

    public AdaptiveTwoLineTile ColumnSpacing(double spacing)
    {
        _columnSpacing = spacing;
        return this;
    }

    public AdaptiveTwoLineTile Margin(Thickness margin)
    {
        _margin = margin;
        return this;
    }

    public AdaptiveTwoLineTile AutomationId(string automationId)
    {
        _automationId = automationId;
        return this;
    }

    public AdaptiveTwoLineTile OnTapped(string accessibilityName, Action onTapped)
    {
        _accessibilityName = accessibilityName;
        _onTapped = onTapped;
        return this;
    }

    public override VisualNode Render()
    {
        var hasLeading = _leading is not null;
        var hasTrailing = _trailing is not null;
        var columns = (hasLeading, hasTrailing) switch
        {
            (true, true) => "Auto,*,Auto",
            (true, false) => "Auto,*",
            (false, true) => "*,Auto",
            _ => "*",
        };
        var contentColumn = hasLeading ? 1 : 0;
        var trailingColumn = contentColumn + 1;
        var children = new List<VisualNode>
        {
            _label.GridRow(1).GridColumn(contentColumn),
            _value.GridRow(2).GridColumn(contentColumn),
        };

        if (_leading is not null)
        {
            children.Add(
                Grid(_leading)
                    .VCenter()
                    .GridRow(0)
                    .GridRowSpan(4)
                    .GridColumn(0));
        }

        if (_trailing is not null)
        {
            children.Add(
                Grid(_trailing)
                    .VCenter()
                    .GridRow(0)
                    .GridRowSpan(4)
                    .GridColumn(trailingColumn));
        }

        var tile = Border(
            Grid(
                rows: "*,Auto,Auto,*",
                columns: columns,
                children.ToArray())
            .ColumnSpacing(_columnSpacing)
            .Padding(AppSpacing.M)
        )
        .BackgroundColor(_backgroundColor ?? SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(_minimumHeight);

        if (_onTapped is null)
        {
            if (_margin.HasValue)
            {
                tile.Margin(_margin.Value);
            }

            if (_automationId is not null)
            {
                tile.AutomationId(_automationId);
            }

            return tile;
        }

        tile
            .StrokeThickness(1)
            .Stroke(State.IsFocused ? FocusColor() : (_backgroundColor ?? SurfaceColor()))
            .Set(MauiControls.AutomationProperties.ExcludedWithChildrenProperty, true);

        var activator = Button("")
            .Padding(0)
            .BorderWidth(0)
            .CornerRadius(0)
            .BackgroundColor(Colors.Transparent)
            .TextColor(Colors.Transparent)
            .Set(
                MauiControls.SemanticProperties.DescriptionProperty,
                _accessibilityName ?? string.Empty)
            .OnClicked(_onTapped)
            .OnFocused(() => SetState(s => s.IsFocused = true))
            .OnUnfocused(() => SetState(s => s.IsFocused = false));

        if (_automationId is not null)
        {
            activator.AutomationId(_automationId);
        }

        var interactiveTile = Grid(tile, activator);
        if (_margin.HasValue)
        {
            interactiveTile.Margin(_margin.Value);
        }

        return interactiveTile;
    }

    private static Color SurfaceColor() =>
        Application.Current?.RequestedTheme != AppTheme.Dark
            ? AppColors.Light.Surface
            : AppColors.Dark.Surface;

    private static Color FocusColor() =>
        Application.Current?.RequestedTheme != AppTheme.Dark
            ? AppColors.Light.Primary
            : AppColors.Dark.OnPrimary;
}
