namespace BaristaNotes.Core.Services.Origins;

public readonly record struct OriginCameraFit(double CenterX, double CenterY, double Resolution)
{
    public const double CountryResolution = 9783.9396205;

    public static OriginCameraFit Calculate(
        IReadOnlyList<(double x, double y)> projectedLocations, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(projectedLocations);
        if (projectedLocations.Count == 0)
            throw new ArgumentException("At least one projected location is required.", nameof(projectedLocations));
        if (!double.IsFinite(width) || width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (!double.IsFinite(height) || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (projectedLocations.Any(point => !double.IsFinite(point.x) || !double.IsFinite(point.y)))
            throw new ArgumentException("Projected locations must be finite.", nameof(projectedLocations));

        var minX = projectedLocations.Min(point => point.x);
        var maxX = projectedLocations.Max(point => point.x);
        var minY = projectedLocations.Min(point => point.y);
        var maxY = projectedLocations.Max(point => point.y);
        // Reduce the 48-DIP padding only on a compact surface, retaining room for the viewport.
        var horizontalPadding = Math.Min(48, width / 4);
        var verticalPadding = Math.Min(48, height / 4);
        var resolution = Math.Max(CountryResolution, Math.Max(
            (maxX - minX) / (width - horizontalPadding * 2),
            (maxY - minY) / (height - verticalPadding * 2)));
        return new OriginCameraFit((minX + maxX) / 2, (minY + maxY) / 2, resolution);
    }
}
