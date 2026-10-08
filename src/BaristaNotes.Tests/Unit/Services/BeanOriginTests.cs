using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Origins;
using OriginLocation = BaristaNotes.Core.Services.Origins.OriginLocation;
using OriginLookup = BaristaNotes.Core.Services.Origins.OriginLookup;
using OriginPrecision = BaristaNotes.Core.Services.Origins.OriginPrecision;
using Moq;

namespace BaristaNotes.Tests.Unit.Services;

public sealed class BeanOriginTests
{
    private readonly BeanOriginResolver _resolver = new();

    [Fact]
    public void Catalog_HasAllPublicCountriesAndRepresentativeCoordinates()
    {
        Assert.Equal(177, _resolver.Countries.Count);
        Assert.Equal(177, _resolver.Countries.Select(country => country.Name).Distinct().Count());
        Assert.Equal(174, _resolver.Countries.Count(country => country.CountryCode is not null));
        Assert.Equal("no", Assert.Single(_resolver.Resolve("Norway")).CountryCode);
        Assert.Equal("fr", Assert.Single(_resolver.Resolve("France")).CountryCode);
        Assert.Equal("tw", Assert.Single(_resolver.Resolve("Taiwan")).CountryCode);
        var ethiopia = Assert.Single(_resolver.Resolve("Ethiopia"));
        Assert.Equal(39.0886, ethiopia.Longitude);
        Assert.Equal(8.032795, ethiopia.Latitude);
        var colombia = Assert.Single(_resolver.Resolve("Colombia"));
        Assert.Equal(-73.174347, colombia.Longitude);
        Assert.Equal(3.373111, colombia.Latitude);
        var brazil = Assert.Single(_resolver.Resolve("Brazil"));
        Assert.Equal(-49.55945, brazil.Longitude);
        Assert.Equal(-12.098687, brazil.Latitude);
        foreach (var country in _resolver.Countries)
            foreach (var alias in country.Aliases)
                Assert.Equal(country.Name, Assert.Single(_resolver.Resolve(alias)).Name);
    }

    [Theory]
    [InlineData("Guji, Ethiopia", "Ethiopia")]
    [InlineData("Guji/Ethiopia", "Ethiopia")]
    [InlineData("  ethiopia  ", "Ethiopia")]
    [InlineData("Federal Democratic Republic of Ethiopia", "Ethiopia")]
    [InlineData("C\u00f4te d'Ivoire", "Ivory Coast")]
    [InlineData("Cote d Ivoire", "Ivory Coast")]
    [InlineData("Papua New Guinea", "Papua New Guinea")]
    [InlineData("Equatorial Guinea", "Equatorial Guinea")]
    [InlineData("Guinea-Bissau", "Guinea-Bissau")]
    [InlineData("Democratic Republic of the Congo", "Democratic Republic of the Congo")]
    [InlineData("South Sudan", "South Sudan")]
    [InlineData("Nigeria", "Nigeria")]
    public void Resolve_UsesCountryNamesWithoutNestedOrSubstringMatches(string origin, string country)
    {
        Assert.Equal(country, Assert.Single(_resolver.Resolve(origin)).Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Guji")]
    [InlineData("Unknown")]
    [InlineData("Ethiopian")]
    [InlineData("Brazilian")]
    [InlineData("Tropical blend")]
    public void Resolve_LeavesUnknownOriginsUnmapped(string? origin)
    {
        Assert.Empty(_resolver.Resolve(origin));
    }

    [Fact]
    public void Build_ApprovedSamplesCountBeansNotDistinctOriginStrings()
    {
        var beans = new[]
        {
            Bean(1, "Ethiopia"),
            Bean(2, "Colombia"),
            Bean(3, "Brazil"),
            Bean(4, "Guji, Ethiopia", active: false),
            Bean(5, "")
        };
        var snapshot = _resolver.Build(beans.Concat(new[] { beans[0] }));

        Assert.Equal(3, snapshot.Countries.Count);
        Assert.Equal(4, snapshot.MappedBeans.Count);
        Assert.Equal(5, Assert.Single(snapshot.UnmappedBeans).Id);
        var ethiopia = snapshot.Countries.Single(group => group.Country.Name == "Ethiopia");
        Assert.Equal(new[] { 1, 4 }, ethiopia.Beans.Select(bean => bean.Id).Order().ToArray());
        Assert.False(ethiopia.Beans.Single(bean => bean.Id == 4).IsActive);
    }

    [Fact]
    public void Build_BlendsAppearAtEachCountryButCombinedBeansAreDistinct()
    {
        var blend = Bean(1, "Ethiopia / Colombia / Ethiopia");
        var snapshot = _resolver.Build(new[] { blend, blend, Bean(2, "Colombia"), Bean(3, null) });

        Assert.Equal(2, snapshot.Countries.Count);
        Assert.Equal(new[] { 1, 2 }, snapshot.MappedBeans.Select(bean => bean.Id).Order().ToArray());
        Assert.Single(snapshot.Countries.Single(group => group.Country.Name == "Ethiopia").Beans);
        Assert.Equal(2, snapshot.Countries.Single(group => group.Country.Name == "Colombia").Beans.Count);
        Assert.Single(snapshot.UnmappedBeans);
        Assert.Equal(new[] { "Guinea", "Papua New Guinea" },
            _resolver.Resolve("Guinea / Papua New Guinea").Select(country => country.Name).ToArray());
    }

    [Fact]
    public void Build_EmptyAndUnmappedSnapshotsHaveNoInventedCountries()
    {
        var empty = _resolver.Build(Array.Empty<BeanDto>());
        Assert.Empty(empty.Countries);
        Assert.Empty(empty.MappedBeans);
        Assert.Empty(empty.UnmappedBeans);
        var unknown = _resolver.Build(new[] { Bean(1, ""), Bean(2, "Guji") });
        Assert.Empty(unknown.Countries);
        Assert.Empty(unknown.MappedBeans);
        Assert.Equal(2, unknown.UnmappedBeans.Count);
    }

    [Fact]
    public void Queries_KeepCountryAliasesLocalAndDetailedBlendComponentsSeparate()
    {
        foreach (var country in _resolver.Countries)
            foreach (var alias in country.Aliases.Append(country.Name))
                Assert.False(Assert.Single(_resolver.Queries(alias)).NeedsLookup);
        Assert.Equal("et", Assert.Single(_resolver.Queries("Guji, Ethiopia")).Country.CountryCode);
        Assert.Equal("guji", Assert.Single(_resolver.Queries("Guji, Ethiopia")).Detail);
        Assert.False(Assert.Single(_resolver.Queries("Ethiopia / Ethiopia")).NeedsLookup);
        var blend = _resolver.Queries("Guji, Ethiopia / Colombia / Ethiopia");
        Assert.Equal(3, blend.Count);
        Assert.Single(blend.Where(query => query.NeedsLookup));
        Assert.All(_resolver.Queries("Ethiopia, Colombia"), query => Assert.False(query.NeedsLookup));
        var sameCountryBlend = _resolver.Queries("Guji, Ethiopia / Yirgacheffe, Ethiopia");
        Assert.Equal(2, sameCountryBlend.Count);
        Assert.All(sameCountryBlend, query => Assert.True(query.NeedsLookup));
        Assert.Equal("guji", Assert.Single(_resolver.Queries("Guji/Ethiopia")).Detail);
        Assert.Empty(_resolver.Queries(""));
    }

    [Fact]
    public void DetailedLocations_SplitWithZoomWithoutChangingCoordinatesOrBeanData()
    {
        var beans = new[] { Bean(1, "Ethiopia"), Bean(2, "Guji, Ethiopia", false),
            Bean(3, "Colombia"), Bean(4, "Brazil"), Bean(5, "") };
        var query = Assert.Single(_resolver.Queries(beans[1].Origin));
        var lookups = new Dictionary<string, OriginLookup>
        {
            [query.Key] = new(new OriginPlace("Guji", "et", 39.2030529, 5.5501401, OriginPrecision.Region), null)
        };
        var snapshot = _resolver.Build(beans, lookups);
        Assert.Equal(4, snapshot.MappedBeans.Count);
        Assert.Equal(5, Assert.Single(snapshot.UnmappedBeans).Id);
        Assert.Equal(4, snapshot.Locations.Count);
        var locations = snapshot.Locations.Where(location => location.Country.Name == "Ethiopia").ToArray();
        var originalCoordinates = locations.Select(location => (location.Longitude, location.Latitude)).ToArray();
        var wide = Assert.Single(Clusters(locations, OriginCameraFit.CountryResolution));
        Assert.Equal(2, wide.Beans.Count);
        Assert.Equal("Ethiopia (2)", wide.MarkerLabel);
        Assert.Equal("Ethiopia (2)", wide.Selection.Label);
        Assert.Contains("approx.", wide.PrecisionLabel);
        Assert.Equal(new[] { 1, 2 }, wide.Selection.BeansFor(snapshot).Select(bean => bean.Id).Order().ToArray());
        var zoomed = Clusters(locations, OriginCameraFit.CountryResolution / 4);
        Assert.Equal(2, zoomed.Count);
        Assert.All(zoomed, cluster => Assert.Single(cluster.Beans));
        var guji = zoomed.Single(cluster => cluster.Name == "Guji");
        Assert.Equal("Guji (1)", guji.MarkerLabel);
        Assert.Equal("Guji (1)", guji.Selection.Label);
        Assert.Equal("Region", guji.PrecisionLabel);
        Assert.Equal(2, Assert.Single(guji.Selection.BeansFor(snapshot)).Id);
        var country = zoomed.Single(cluster => cluster.Name == "Ethiopia");
        Assert.Equal("Ethiopia (1)", country.MarkerLabel);
        Assert.Equal("Ethiopia (1)", country.Selection.Label);
        Assert.Equal("Approx. country", country.PrecisionLabel);
        Assert.Equal(1, Assert.Single(country.Selection.BeansFor(snapshot)).Id);
        Assert.Equal(originalCoordinates, locations.Select(location => (location.Longitude, location.Latitude)).ToArray());
        Assert.Equal(beans, _resolver.Build(beans).MappedBeans.Concat(snapshot.UnmappedBeans).OrderBy(bean => bean.Id).ToArray());
        Assert.Equal(wide.Selection.Key, Assert.Single(Clusters(locations.Reverse().ToArray(),
            OriginCameraFit.CountryResolution)).Selection.Key);
    }

    [Fact]
    public void MarkerLabel_MixedCountryClusterUsesNeutralNameAndDistinctBeanCount()
    {
        var snapshot = _resolver.Build(new[] { Bean(1, "Ethiopia / Colombia"),
            Bean(2, "Ethiopia"), Bean(3, "Brazil") });
        var cluster = Assert.Single(OriginClusterer.Build(snapshot.Locations, _ => (0d, 0d)));

        Assert.Equal("Origins (3)", cluster.MarkerLabel);
        Assert.Equal(new[] { 1, 2, 3 },
            cluster.Selection.BeansFor(snapshot).Select(bean => bean.Id).Order().ToArray());
        Assert.Equal("Origins (3)", cluster.Selection.Label);
        var reversed = Assert.Single(OriginClusterer.Build(snapshot.Locations.Reverse().ToArray(), _ => (0d, 0d)));
        Assert.Equal(cluster.MarkerLabel, reversed.MarkerLabel);
        Assert.Equal(cluster.Selection.Label, reversed.Selection.Label);
        Assert.Equal(cluster.Selection.Key, reversed.Selection.Key);
    }

    [Fact]
    public void SelectionLabel_SameCountryBlendCountsArchivedBeansOnceWithoutExplanatoryLine()
    {
        var blend = Bean(1, "Guji, Ethiopia / Ethiopia", active: false);
        var snapshot = _resolver.Build(new[] { blend, blend, Bean(2, "Ethiopia") });
        var cluster = Assert.Single(OriginClusterer.Build(snapshot.Locations, _ => (0d, 0d)));

        Assert.Equal(2, cluster.Locations.Count);
        Assert.Equal("Ethiopia (2)", cluster.Selection.Label);
        Assert.Equal(cluster.MarkerLabel, cluster.Selection.Label);
        Assert.DoesNotContain("\n", cluster.Selection.Label);
        Assert.Equal(new[] { 1, 2 },
            cluster.Selection.BeansFor(snapshot).Select(bean => bean.Id).Order().ToArray());
        Assert.False(cluster.Selection.BeansFor(snapshot).Single(bean => bean.Id == 1).IsActive);
    }

    [Fact]
    public void ClusterThreshold_IsInclusiveAndDeduplicatesBlendIds()
    {
        var snapshot = _resolver.Build(new[] { Bean(1, "Guji, Ethiopia / Ethiopia / Colombia"),
            Bean(2, "Ethiopia"), Bean(3, "") });
        Assert.Equal(3, snapshot.Locations.Count);
        var combined = Assert.Single(OriginClusterer.Build(snapshot.Locations, _ => (0d, 0d)));
        Assert.Equal(2, combined.Beans.Count);
        Assert.Equal(2, combined.Selection.BeansFor(snapshot).Count);
        var locations = snapshot.Locations.Take(2).ToArray();
        Assert.Single(OriginClusterer.Build(locations, location =>
            (location == locations[0] ? 0 : OriginClusterer.SeparationDistance, 0d)));
        Assert.Equal(2, OriginClusterer.Build(locations, location =>
            (location == locations[0] ? 0 : OriginClusterer.SeparationDistance + 0.01, 0d)).Count);
        Assert.Throws<ArgumentException>(() => OriginClusterer.Build(locations, _ => (double.NaN, 0d)));
    }

    [Fact]
    public void UnresolvedQueries_KeepApproximateFallbackAndStableSelectionAfterResolution()
    {
        var beans = new[] { Bean(1, "Ethiopia"), Bean(2, "Guji, Ethiopia") };
        var query = Assert.Single(_resolver.Queries(beans[1].Origin));
        var pending = _resolver.Build(beans);
        Assert.All(pending.Locations, location => Assert.Equal(OriginPrecision.ApproximateCountry, location.Precision));
        var selected = Assert.Single(Clusters(pending.Locations, OriginCameraFit.CountryResolution)).Selection;
        Assert.Equal("Ethiopia (2)", selected.Label);
        var resolved = _resolver.Build(beans, new Dictionary<string, OriginLookup>
        {
            [query.Key] = new(new OriginPlace("Guji", "et", 39.2030529, 5.5501401, OriginPrecision.Region), null)
        });
        Assert.Equal(2, selected.BeansFor(resolved).Count);
        var resolvedSelection = Assert.Single(Clusters(resolved.Locations, OriginCameraFit.CountryResolution)).Selection;
        Assert.Equal(selected.Key, resolvedSelection.Key);
        Assert.Equal(selected.Label, resolvedSelection.Label);
        var fallback = _resolver.Build(beans, new Dictionary<string, OriginLookup>
        {
            [query.Key] = new(null, "No result")
        });
        Assert.Equal(pending.Locations.Select(location => location.Latitude),
            fallback.Locations.Select(location => location.Latitude));
        Assert.Throws<InvalidDataException>(() => _resolver.Build(beans, new Dictionary<string, OriginLookup>
        {
            [query.Key] = new(new OriginPlace("Guji", "co", 39.2030529, 5.5501401, OriginPrecision.Region), null)
        }));
    }

    private static IReadOnlyList<OriginCluster> Clusters(IReadOnlyList<OriginLocation> locations, double resolution) =>
        OriginClusterer.Build(locations, location =>
        {
            const double radius = 6378137;
            var x = radius * location.Longitude * Math.PI / 180;
            var y = radius * Math.Log(Math.Tan(Math.PI / 4 + location.Latitude * Math.PI / 360));
            return (x / resolution, y / resolution);
        });

    [Fact]
    public async Task SavedBeanQuery_IncludesArchivedExcludesDeletedAndDeduplicatesIds()
    {
        var repository = new Mock<IBeanRepository>();
        var active = new Bean { Id = 1, Name = "Active", Origin = "Ethiopia", IsActive = true };
        repository.Setup(repo => repo.GetNonDeletedBeansAsync(null)).ReturnsAsync(new List<Bean>
        {
            active,
            new() { Id = 2, Name = "Archived", Origin = "Brazil", IsActive = false },
            new() { Id = 3, Name = "Deleted", Origin = "Colombia", IsDeleted = true },
            active
        });
        var service = new BeanService(repository.Object, Mock.Of<IRatingService>());

        var beans = await service.GetAllSavedBeansAsync();

        Assert.Equal(new[] { 1, 2 }, beans.Select(bean => bean.Id).Order().ToArray());
        Assert.False(beans.Single(bean => bean.Id == 2).IsActive);
        repository.Verify(repo => repo.GetNonDeletedBeansAsync(null), Times.Once);
        repository.Verify(repo => repo.GetActiveBeansAsync(), Times.Never);
        var snapshot = _resolver.Build(beans);
        Assert.Equal(2, snapshot.MappedBeans.Count);
        Assert.DoesNotContain(snapshot.Countries, group => group.Country.Name == "Colombia");
    }

    [Theory]
    [InlineData(402, 208)]
    [InlineData(874, 96)]
    [InlineData(874, 280)]
    public void CameraFit_AllProjectedLocationsHaveRequiredPadding(double width, double height)
    {
        var points = new[] { (-8145790d, 375000d), (-5516900d, -1356900d), (4351350d, 897000d) };
        var fit = OriginCameraFit.Calculate(points, width, height);

        Assert.True(fit.Resolution >= OriginCameraFit.CountryResolution);
        foreach (var point in points)
        {
            var screenX = width / 2 + (point.Item1 - fit.CenterX) / fit.Resolution;
            var screenY = height / 2 - (point.Item2 - fit.CenterY) / fit.Resolution;
            Assert.InRange(screenX, Math.Min(48, width / 4) - 0.001, width - Math.Min(48, width / 4) + 0.001);
            Assert.InRange(screenY, Math.Min(48, height / 4) - 0.001, height - Math.Min(48, height / 4) + 0.001);
        }
    }

    [Fact]
    public void CameraFit_WorldOverviewCanExceedTileLevelZeroWithoutCropping()
    {
        var fit = OriginCameraFit.Calculate(
            new[] { (-20037508d, -15000000d), (20037508d, 15000000d) }, 402, 150);
        Assert.Equal(0d, fit.CenterX);
        Assert.Equal(0d, fit.CenterY);
        Assert.Equal(400000d, fit.Resolution);
        Assert.True(fit.Resolution > 156543.033928);
    }

    [Fact]
    public void CameraFit_OneCountryHasCountryScaleNotStreetScale()
    {
        var fit = OriginCameraFit.Calculate(new[] { (4351350d, 897000d) }, 402, 208);
        Assert.Equal(4351350d, fit.CenterX);
        Assert.Equal(897000d, fit.CenterY);
        Assert.Equal(OriginCameraFit.CountryResolution, fit.Resolution);
    }

    [Fact]
    public void CameraFit_RejectsInvalidInput()
    {
        Assert.Throws<ArgumentException>(() => OriginCameraFit.Calculate(Array.Empty<(double, double)>(), 402, 208));
        Assert.Throws<ArgumentOutOfRangeException>(() => OriginCameraFit.Calculate(new[] { (0d, 0d) }, 0, 208));
        Assert.Throws<ArgumentException>(() => OriginCameraFit.Calculate(new[] { (double.NaN, 0d) }, 402, 208));
    }

    private static BeanDto Bean(int id, string? origin, bool active = true) =>
        new() { Id = id, Name = $"Map sample {id}", Origin = origin, IsActive = active };
}
