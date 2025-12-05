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
        return new Frame
        {
            new Image()
                .Source(_imagePath ?? "default_avatar.png")
                .Aspect(Aspect.AspectFill)
                .WidthRequest(_size)
                .HeightRequest(_size)
                .AutomationId("ProfileAvatarImage")
        }
        .WidthRequest(_size)
        .HeightRequest(_size)
        .CornerRadius(_size / 2)
        .IsClippedToBounds(true)
        .HasShadow(false)
        .Padding(0)
        .AutomationId("ProfileAvatarFrame");
    }
}
