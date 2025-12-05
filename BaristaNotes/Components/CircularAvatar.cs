using MauiReactor;

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
        return Border(
            new MauiReactor.Image()
                .Source(_imagePath ?? "default_avatar.png")
                .Aspect(Aspect.AspectFill)
                .WidthRequest(_size)
                .HeightRequest(_size)
                .AutomationId("ProfileAvatarImage")
        )
        .WidthRequest(_size)
        .HeightRequest(_size)
        .StrokeThickness(2)
        .Stroke(Colors.LightGray)
        .BackgroundColor(Colors.Transparent)
        .Padding(0)
        .Margin(8)
        .AutomationId("ProfileAvatarFrame");
    }
}
