using MauiReactor;
using MauiReactor.Shapes;
using Fonts;

namespace BaristaNotes.Components;

public class CircularAvatar : Component
{
    private readonly string? _imagePath;
    private readonly double _size;

    public CircularAvatar(string? imagePath, double size = 100)
    {
        _imagePath = imagePath;
        _size = size;
    }

    public override VisualNode Render()
    {
        // Use a Border with rounded corners to clip the image in a circle
        var cornerRadius = _size / 2;

        return Border(
            string.IsNullOrEmpty(_imagePath)
                // Show a person icon as placeholder when no image
                ? Label(MaterialSymbolsFont.Person)
                    .FontFamily("MaterialSymbolsOutlined")
                    .FontSize(_size * 0.5)
                    .TextColor(Colors.Gray)
                    .HCenter()
                    .VCenter()
                    .AutomationId("ProfileAvatarPlaceholder")
                // Show the actual image when available
                : (VisualNode)new MauiReactor.Image()
                    .Source(_imagePath)
                    .Aspect(Aspect.AspectFill)
                    .WidthRequest(_size)
                    .HeightRequest(_size)
                    .AutomationId("ProfileAvatarImage")
        )
        .WidthRequest(_size)
        .HeightRequest(_size)
        .StrokeShape(new RoundRectangle().CornerRadius(cornerRadius))
        .StrokeThickness(2)
        .Stroke(Colors.LightGray)
        .BackgroundColor(Colors.LightGray.WithAlpha(0.3f))
        .Padding(0)
        .Margin(8)
        .AutomationId("ProfileAvatarFrame");
    }
}
