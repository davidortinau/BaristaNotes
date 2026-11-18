namespace BaristaNotes.Components;

internal static class NavigationIcon
{
    public static MauiReactor.Image Create(FontImageSource source)
    {
        var image = MauiReactor.Component.Image()
            .Source(source)
            .HCenter()
            .VCenter();
#if WINDOWS
        image.WidthRequest(source.Size).HeightRequest(source.Size);
#endif
        return image;
    }
}
