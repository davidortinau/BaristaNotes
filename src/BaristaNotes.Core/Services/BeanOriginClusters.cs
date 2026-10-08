using Mapsui;
using Mapsui.Extensions;
using Mapsui.Projections;

namespace BaristaNotes.Core.Services;

public sealed record BeanOriginCluster(IReadOnlyList<BeanOriginPlace> Places, MPoint Position)
{
    public int BeanCount => Places.SelectMany(place => place.Beans).DistinctBy(bean => bean.Id).Count();
    public string Title => BeanOriginClusters.GetTitle(Places);
}

public static class BeanOriginClusters
{
    public const double Distance = 64;

    internal static string GetTitle(IReadOnlyList<BeanOriginPlace> places) => places.Count == 0 ? ""
        : places.Count == 1
            ? places[0].Location.Precision == OriginPrecision.ApproximateCountry
                ? places[0].Country.Name : places[0].Location.Name
            : places.Select(place => place.Country.Name).Distinct(StringComparer.Ordinal).Count() == 1
                ? places[0].Country.Name : "Origins";

    public static IReadOnlyList<BeanOriginCluster> Create(IReadOnlyList<BeanOriginPlace> places, Viewport viewport)
    {
        var positions = places.Select(place => SphericalMercator.FromLonLat(
            place.Location.Longitude, place.Location.Latitude)).Select(point => new MPoint(point.x, point.y)).ToArray();
        var resolution = viewport.Resolution > 0 && double.IsFinite(viewport.Resolution)
            ? viewport.Resolution : 156543.033928;
        var screen = positions.Select(position => (viewport with { Resolution = resolution }).WorldToScreen(position)).ToArray();
        var remaining = Enumerable.Range(0, places.Count).ToHashSet();
        var clusters = new List<BeanOriginCluster>();
        while (remaining.Count > 0)
        {
            var members = new List<int> { remaining.Min() };
            remaining.Remove(members[0]);
            for (var index = 0; index < members.Count; index++)
            {
                foreach (var candidate in remaining.ToArray())
                {
                    var dx = screen[members[index]].X - screen[candidate].X;
                    var dy = screen[members[index]].Y - screen[candidate].Y;
                    if (dx * dx + dy * dy > Distance * Distance) continue;
                    members.Add(candidate);
                    remaining.Remove(candidate);
                }
            }
            // Only the aggregate marker moves; the stored geographic positions never change with zoom.
            clusters.Add(new BeanOriginCluster(members.Select(index => places[index]).ToArray(),
                new MPoint(members.Average(index => positions[index].X), members.Average(index => positions[index].Y))));
        }
        return clusters;
    }
}
