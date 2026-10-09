using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services.Origins;

public enum OriginPrecision { ApproximateCountry, Region, City, Locality }

public sealed record OriginQuery(string Key, string Text, string Detail, OriginCountry Country)
{
    public string[] DetailNames { get; init; } = [];
    public bool NeedsLookup => Detail.Length > 0;
}

public sealed record OriginPlace(
    string Name, string CountryCode, double Longitude, double Latitude, OriginPrecision Precision)
{
    public string AddressHierarchy { get; init; } = "";
}

public sealed record OriginLookup(OriginPlace? Place, string? Error);

public sealed record OriginLocation(
    string Key, OriginCountry Country, string Name, double Longitude, double Latitude,
    OriginPrecision Precision, IReadOnlyList<BeanDto> Beans);

public sealed record OriginSelection(string Key, string Label, IReadOnlyList<string> LocationKeys)
{
    public IReadOnlyList<BeanDto> BeansFor(BeanOriginSnapshot snapshot) =>
        snapshot.Locations.Where(location => LocationKeys.Contains(location.Key))
            .SelectMany(location => location.Beans).DistinctBy(bean => bean.Id)
            .OrderBy(bean => bean.Name, StringComparer.OrdinalIgnoreCase).ToArray();
}

public sealed record OriginCluster(IReadOnlyList<OriginLocation> Locations)
{
    public IReadOnlyList<BeanDto> Beans => Locations.SelectMany(location => location.Beans)
        .DistinctBy(bean => bean.Id).ToArray();
    public string Name => Locations.Count == 1 ? Locations[0].Name :
        string.Join(" / ", Locations.Select(location => location.Country.Name).Distinct());
    public string MarkerLabel
    {
        get
        {
            var name = Locations.Select(location => location.Country.Name).Distinct().Count() > 1
                ? "Origins" : Name;
            return $"{name} ({Beans.Count})";
        }
    }
    public string PrecisionLabel => Locations.Count == 1
        ? Locations[0].Precision switch
        {
            OriginPrecision.Region => "Region",
            OriginPrecision.City => "City",
            OriginPrecision.Locality => "Locality",
            _ => "Approx. country"
        }
        : Locations.Any(location => location.Precision == OriginPrecision.ApproximateCountry)
            ? "Nearby origins; includes approx. country" : "Nearby places";
    public OriginSelection Selection => new(
        string.Join("\n", Locations.Select(location => location.Key).Order(StringComparer.Ordinal)),
        MarkerLabel,
        Locations.Select(location => location.Key).ToArray());
}

public static class OriginClusterer
{
    public const double SeparationDistance = 72;

    public static IReadOnlyList<OriginCluster> Build(IReadOnlyList<OriginLocation> locations,
        Func<OriginLocation, (double X, double Y)> projectToScreen)
    {
        var points = locations.Select(projectToScreen).ToArray();
        if (points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
            throw new ArgumentException("Origin screen coordinates must be finite.", nameof(projectToScreen));
        var remaining = Enumerable.Range(0, locations.Count).ToHashSet();
        var clusters = new List<OriginCluster>();
        while (remaining.Count > 0)
        {
            var members = new List<int> { remaining.Min() };
            remaining.Remove(members[0]);
            for (var i = 0; i < members.Count; i++)
            {
                var point = points[members[i]];
                foreach (var candidate in remaining.ToArray())
                {
                    var dx = point.X - points[candidate].X;
                    var dy = point.Y - points[candidate].Y;
                    if (dx * dx + dy * dy > SeparationDistance * SeparationDistance)
                        continue;
                    remaining.Remove(candidate);
                    members.Add(candidate);
                }
            }
            clusters.Add(new OriginCluster(members.Select(index => locations[index]).ToArray()));
        }
        return clusters;
    }
}
