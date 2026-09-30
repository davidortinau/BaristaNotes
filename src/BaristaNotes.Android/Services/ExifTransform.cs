namespace BaristaNotes.AndroidApp.Services;

internal readonly record struct ExifTransform(float ScaleX, float ScaleY, int Rotation)
{
    public bool IsIdentity => ScaleX == 1 && ScaleY == 1 && Rotation == 0;

    // Pre-scale then post-rotate matches the pinned Android PlatformImage path.
    public static ExifTransform FromOrientation(int orientation) => orientation switch
    {
        2 => new(-1, 1, 0),
        3 => new(1, 1, 180),
        4 => new(1, -1, 0),
        5 => new(1, -1, 90),
        6 => new(1, 1, 90),
        7 => new(1, -1, 270),
        8 => new(1, 1, 270),
        _ => new(1, 1, 0)
    };
}
