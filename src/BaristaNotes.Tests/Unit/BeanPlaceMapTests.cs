using System.Collections.Concurrent;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Manipulations;
using Mapsui.Rendering;
using Mapsui.Styles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BaristaNotes.Tests.Unit;

[Collection(nameof(NominatimRequestGateCollection))]
public sealed class BeanPlaceMapTests
{
    private static BeanDto Bean(int id, string? origin) =>
        new() { Id = id, Name = $"Unchanged saved bean {id}", Origin = origin, IsActive = true };
    private static BeanDto[] Samples => [
        Bean(1, "Ethiopia"), Bean(2, "Colombia"), Bean(3, "Brazil"), Bean(4, "Guji, Ethiopia"), Bean(5, "")
    ];

    [Fact]
    public void CountryOnlyAliasesAndEmptyOriginsNeverGenerateDetailedQueries()
    {
        foreach (var country in BeanOriginMapData.CountryCatalogue)
        foreach (var alias in country.Aliases.Append(country.Name))
        {
            var data = new BeanOriginMapData([Bean(1, alias)]);
            Assert.Empty(data.Lookups);
            Assert.Equal(OriginPrecision.ApproximateCountry, Assert.Single(data.Places).Location.Precision);
            Assert.Empty(new BeanOriginMapData([Bean(1, $"{alias} and Ethiopia")]).Lookups);
        }
        Assert.Empty(new BeanOriginMapData([Bean(1, ""), Bean(2, null), Bean(3, "Guji")]).Lookups);
        Assert.Equal("et", Assert.Single(BeanOriginMapData.Resolve("Ethiopia")).CountryCode);
        Assert.Equal("co", Assert.Single(BeanOriginMapData.Resolve("Colombia")).CountryCode);
        Assert.Equal("br", Assert.Single(BeanOriginMapData.Resolve("Brazil")).CountryCode);
        Assert.Equal(174, BeanOriginMapData.CountryCatalogue.Count(country => country.CountryCode?.Length == 2));
    }

    [Theory]
    [InlineData("Yorkshire, United Kingdom of Great Britain and Northern Ireland", "United Kingdom", "yorkshire", 1)]
    [InlineData("Yorkshire, United Kingdom of Great Britain and Northern Ireland and Ethiopia", "United Kingdom", "yorkshire", 2)]
    [InlineData("Sarajevo, Bosnia and Herzegovina and Ethiopia", "Bosnia and Herzegovina", "sarajevo", 2)]
    [InlineData("Port of Spain, Trinidad and Tobago", "Trinidad and Tobago", "port of spain", 1)]
    [InlineData("Port of Spain, Trinidad and Tobago and Ethiopia", "Trinidad and Tobago", "port of spain", 2)]
    [InlineData("Port of Spain, Trinidad and Tobago / Ethiopia", "Trinidad and Tobago", "port of spain", 2)]
    [InlineData("Ethiopia and Port of Spain, Trinidad and Tobago", "Trinidad and Tobago", "port of spain", 2)]
    public void CountryNameConjunctionsStayIntactWhileExplicitBlendConjunctionsSeparatePairs(
        string origin, string country, string detail, int countryCount)
    {
        var data = new BeanOriginMapData([Bean(1, origin)]);
        Assert.False(data.HasAmbiguousBlend);
        Assert.Equal(countryCount, data.Countries.Count);
        Assert.All(data.Countries, group => Assert.Contains(group.Country.Name, new[] { country, "Ethiopia" }));
        var lookup = Assert.Single(data.Lookups);
        Assert.Equal(country, lookup.Country.Name);
        Assert.Equal(detail, lookup.Detail);
        Assert.Equal($"{country}|{detail}", lookup.Id);
        Assert.Equal(new[] { $"{country}|{detail}" }, data.Places
            .Where(place => place.Country.Name == country).Select(place => place.Id));
    }

    [Theory]
    [InlineData("Guji, Ethiopia")]
    [InlineData("GUJI / eTHiOPia")]
    [InlineData("Guji; Ethiopia")]
    public void DetailedOriginKeepsSeparateStableLocationEvenWhileUsingCountryFallback(string origin)
    {
        var data = new BeanOriginMapData([Bean(1, "Ethiopia"), Bean(4, origin)]);
        var lookup = Assert.Single(data.Lookups);
        Assert.Equal("Ethiopia|guji", lookup.Id);
        Assert.Equal("guji", lookup.Detail);
        Assert.Equal(2, data.Places.Count);
        Assert.All(data.Places, place => Assert.Equal(OriginPrecision.ApproximateCountry, place.Location.Precision));
        Assert.Equal(4, Assert.Single(data.BeansForPlaces([lookup.Id])).Id);
        Assert.Equal(1, Assert.Single(data.BeansForPlaces(["Ethiopia"])).Id);
    }

    [Fact]
    public void ExplicitBlendPlacePairsDeduplicateBeanIdsWithinAndAcrossClusters()
    {
        var beans = new[] { Bean(1, "Guji, Ethiopia / Ethiopia / Guji, Ethiopia"),
            Bean(2, "Guji, Ethiopia; Huila, Colombia") };
        var fallback = new BeanOriginMapData(beans);
        Assert.False(fallback.HasAmbiguousBlend);
        Assert.Equal(new[] { "Colombia|huila", "Ethiopia|guji" }, fallback.Lookups.Select(lookup => lookup.Id));
        var locations = new Dictionary<string, OriginLocation>
        {
            ["Ethiopia|guji"] = NominatimOriginGeocoder.ParseResponse(
                NominatimOriginGeocoderTests.GujiResponse, fallback.Lookups.Single(lookup => lookup.Country.Name == "Ethiopia"))
        };
        var data = new BeanOriginMapData(beans, locations);
        Assert.Equal(new[] { 1, 2 }, data.BeansForPlaces(data.Places.Select(place => place.Id)).Select(bean => bean.Id));
        var clusters = BeanOriginClusters.Create(data.Places, new Viewport(0, 0, 20000, 0, 400, 300));
        var ethiopia = clusters.Single(cluster => cluster.Places.All(place => place.Country.Name == "Ethiopia"));
        Assert.Equal(2, ethiopia.Places.Count);
        Assert.Equal(2, ethiopia.BeanCount);
        Assert.Equal(2, data.MappedBeans.Count);
        Assert.Equal(1, data.Places.Single(place => place.Id == "Ethiopia").Beans.Count);
        Assert.Equal(2, data.Places.Single(place => place.Id == "Ethiopia|guji").Beans.Count);
    }

    [Fact]
    public void ExplicitCommaSeparatedCountriesRemainACountryOnlyBlend()
    {
        var data = new BeanOriginMapData([Bean(1, "Spain, Trinidad and Tobago")]);
        Assert.False(data.HasAmbiguousBlend);
        Assert.Empty(data.Lookups);
        Assert.Equal(new[] { "Spain", "Trinidad and Tobago" }, data.Countries.Select(group => group.Country.Name));
        Assert.All(data.Places, place => Assert.Equal(OriginPrecision.ApproximateCountry, place.Location.Precision));
    }

    [Theory]
    [InlineData("Guji, Ethiopia, Huila, Colombia")]
    [InlineData("Guji, Ethiopia, Huila Colombia")]
    [InlineData("Port of Spain, Trinidad and Tobago, Huila, Colombia")]
    [InlineData("Port of Spain, Trinidad and Tobago, Huila Colombia")]
    public void AmbiguousDetailedBlendRetainsExplicitCountryFallbackInsteadOfGuessingAssociations(string origin)
    {
        var data = new BeanOriginMapData([Bean(1, origin)]);
        Assert.True(data.HasAmbiguousBlend);
        Assert.DoesNotContain(data.Countries, group => group.Country.Name == "Spain");
        Assert.Empty(data.Lookups);
        Assert.Equal(2, data.Places.Count);
        Assert.All(data.Places, place => Assert.Equal(OriginPrecision.ApproximateCountry, place.Location.Precision));
        using var session = new BeanMapSession(NullLogger.Instance);
        session.SetBeans([Bean(1, origin)]);
        Assert.Contains("could not be paired", session.Error);
        Assert.Equal(1, Assert.Single(session.OriginData!.MappedBeans).Id);
    }

    [Fact]
    public void ClusteringUsesTheExactScreenDistanceThresholdAndNeverChangesStoredCoordinates()
    {
        var country = Assert.Single(BeanOriginMapData.Resolve("Ethiopia"));
        var first = new BeanOriginPlace("first", country,
            new OriginLocation("First", 0, 0, OriginPrecision.Region), [Bean(1, "Ethiopia")]);
        var second = new BeanOriginPlace("second", country,
            new OriginLocation("Second", 1, 0, OriginPrecision.Region), [Bean(2, "Ethiopia")]);
        var projected = Mapsui.Projections.SphericalMercator.FromLonLat(1, 0);
        var boundaryResolution = projected.x / BeanOriginClusters.Distance;
        var viewport = new Viewport(0, 0, boundaryResolution, 0, 400, 300);
        Assert.Single(BeanOriginClusters.Create([first, second], viewport));
        Assert.Equal(2, BeanOriginClusters.Create([first, second],
            viewport with { Resolution = boundaryResolution * .999 }).Count);
        Assert.Single(BeanOriginClusters.Create([first, second], viewport with { Rotation = 90 }));
        Assert.Equal(0, first.Location.Longitude);
        Assert.Equal(1, second.Location.Longitude);
    }

    [Theory]
    [InlineData(OriginPrecision.ApproximateCountry, "Ethiopia")]
    [InlineData(OriginPrecision.City, "Place")]
    [InlineData(OriginPrecision.Locality, "Place")]
    [InlineData(OriginPrecision.Region, "Place")]
    public void SinglePlaceMarkerTitleIsCompactWithoutChangingItsPrecisionDescription(
        OriginPrecision precision, string expected)
    {
        var country = Assert.Single(BeanOriginMapData.Resolve("Ethiopia"));
        var place = new BeanOriginPlace("Ethiopia|place", country,
            new OriginLocation("Place", 0, 0, precision), [Bean(4, "Place, Ethiopia")]);
        var cluster = Assert.Single(BeanOriginClusters.Create([place], new Viewport(0, 0, 1000, 0, 400, 300)));

        Assert.Equal(expected, cluster.Title);
        Assert.Equal($"{expected} (1)", $"{cluster.Title} ({cluster.BeanCount})");
        Assert.Equal(precision, place.Location.Precision);
        if (precision == OriginPrecision.Region) Assert.Equal("Place region, Ethiopia", place.Label);
        if (precision == OriginPrecision.ApproximateCountry) Assert.Equal("Ethiopia (approximate country)", place.Label);
    }

    [Fact]
    public void ClusterMarkerTitlesUseCountryOrNeutralOriginsWithDeduplicatedCounts()
    {
        var ethiopia = Assert.Single(BeanOriginMapData.Resolve("Ethiopia"));
        var colombia = Assert.Single(BeanOriginMapData.Resolve("Colombia"));
        var blend = Bean(1, "Guji, Ethiopia / Colombia");
        var country = new BeanOriginPlace("Ethiopia", ethiopia,
            new OriginLocation("Ethiopia", 0, 0, OriginPrecision.ApproximateCountry), [blend, Bean(2, "Ethiopia")]);
        var region = new BeanOriginPlace("Ethiopia|guji", ethiopia,
            new OriginLocation("Guji", .01, 0, OriginPrecision.Region), [blend]);
        var other = new BeanOriginPlace("Colombia", colombia,
            new OriginLocation("Colombia", .02, 0, OriginPrecision.ApproximateCountry), [blend, Bean(3, "Colombia")]);
        var viewport = new Viewport(0, 0, 1000, 0, 400, 300);

        var sameCountry = Assert.Single(BeanOriginClusters.Create([country, region], viewport));
        Assert.Equal("Ethiopia (2)", $"{sameCountry.Title} ({sameCountry.BeanCount})");
        var mixedCountries = Assert.Single(BeanOriginClusters.Create([country, region, other], viewport));
        Assert.Equal("Origins (3)", $"{mixedCountries.Title} ({mixedCountries.BeanCount})");
        var reordered = Assert.Single(BeanOriginClusters.Create([other, region, country], viewport));
        Assert.Equal("Origins (3)", $"{reordered.Title} ({reordered.BeanCount})");
        Assert.Equal(0, country.Location.Longitude);
        Assert.Equal(.01, region.Location.Longitude);
        Assert.Equal(.02, other.Location.Longitude);
    }

    [Fact]
    public void SelectedOriginLabelUsesCompactCountryAndClusterCountsIncludingFallbackAndArchivedBlends()
    {
        using var session = new BeanMapSession(NullLogger.Instance);
        session.Map.Layers.First().Enabled = false;
        Assert.Empty(session.SelectionLabel);
        session.SetBeans(Samples);
        Assert.Empty(session.SelectionLabel);

        session.SelectLocations(["Ethiopia"]);
        Assert.Equal("Ethiopia (1)", session.SelectionLabel);
        Assert.Equal(1, Assert.Single(session.SelectedBeans).Id);
        session.SelectLocations(["Ethiopia|guji"]);
        Assert.Equal("Ethiopia (1)", session.SelectionLabel);
        Assert.Equal(4, Assert.Single(session.SelectedBeans).Id);
        session.SelectLocations(["Ethiopia", "Ethiopia|guji", "Ethiopia"]);
        Assert.Equal("Ethiopia (2)", session.SelectionLabel);
        Assert.Equal(new[] { 1, 4 }, session.SelectedBeans.Select(bean => bean.Id));

        var archived = Bean(3, "Colombia") with { IsActive = false };
        session.SetBeans([Bean(1, "Guji, Ethiopia / Ethiopia / Colombia"), Bean(2, "Ethiopia"), archived]);
        session.SelectLocations(["Ethiopia", "Ethiopia|guji"]);
        Assert.Equal("Ethiopia (2)", session.SelectionLabel);
        session.SelectLocations(["Colombia", "Ethiopia|guji", "Ethiopia", "Colombia"]);
        Assert.Equal("Origins (3)", session.SelectionLabel);
        Assert.Equal(new[] { 1, 2, 3 }, session.SelectedBeans.Select(bean => bean.Id));
        Assert.False(session.SelectedBeans.Single(bean => bean.Id == 3).IsActive);
        Assert.Contains("approximate country", session.OriginStatus);

        session.SelectCountries([]);
        Assert.Empty(session.SelectionTitle);
        Assert.Empty(session.SelectionLabel);
        session.SelectCountries(["Ethiopia"]);
        session.SetBeans([Bean(5, "")]);
        Assert.False(session.HasSelection);
        Assert.Empty(session.SelectionLabel);
    }

    [Theory]
    [InlineData(OriginPrecision.City)]
    [InlineData(OriginPrecision.Locality)]
    [InlineData(OriginPrecision.Region)]
    public async Task SelectedOriginLabelUsesOnlyTheResolvedPlaceNameAndCount(OriginPrecision precision)
    {
        var geocoder = new Mock<IOriginGeocoder>();
        geocoder.Setup(service => service.ResolveAsync(It.IsAny<OriginLookup>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OriginLookupResult(new OriginLocation("Place", 0, 0, precision), null));
        using var session = new BeanMapSession(NullLogger.Instance, geocoder: geocoder.Object, dispatch: action => action());
        session.Map.Layers.First().Enabled = false;
        var resolved = NextResolution(session);
        session.SetBeans([Bean(1, "Place, Ethiopia")]);
        await resolved.WaitAsync(TimeSpan.FromSeconds(30));
        session.SelectLocations(["Ethiopia|place"]);

        Assert.Equal("Place", session.SelectionTitle);
        Assert.Equal("Place (1)", session.SelectionLabel);
        Assert.Equal(1, Assert.Single(session.SelectedBeans).Id);
        var place = Assert.Single(session.OriginData!.Places);
        Assert.Equal(precision, place.Location.Precision);
        Assert.Equal(precision == OriginPrecision.Region ? "Place region, Ethiopia" : "Place, Ethiopia", place.Label);
        Assert.Contains(place.Label, session.OriginStatus);
        session.SelectLocations([]);
        Assert.Empty(session.SelectionLabel);
    }

    [Fact]
    public void MixedCountryMarkerUsesNeutralLabelAndRetainsCountrySelectionAndBlendDeduplication()
    {
        using var session = new BeanMapSession(NullLogger.Instance);
        session.Map.Layers.First().Enabled = false;
        session.Map.Navigator.SetSize(400, 300);
        session.SetBeans([Bean(1, "Ethiopia / Colombia"), Bean(2, "Ethiopia"), Bean(3, "Brazil")]);
        const double clusterResolution = 1000000;
        var navigator = session.Map.Navigator;
        // Disabling tiles leaves their existing zoom bounds; this in-memory test needs a wider viewport.
        navigator.OverrideZoomBounds = new MMinMax(1, clusterResolution);
        navigator.ZoomTo(clusterResolution, duration: 0);
        var viewport = navigator.Viewport;
        Assert.Equal(clusterResolution, viewport.Resolution);
        Assert.Equal(400d, viewport.Width);
        Assert.Equal(300d, viewport.Height);
        var positions = session.OriginData!.Places.Select(place =>
        {
            var projected = Mapsui.Projections.SphericalMercator.FromLonLat(
                place.Location.Longitude, place.Location.Latitude);
            return viewport.WorldToScreen(projected.x, projected.y);
        }).ToArray();
        Assert.All(positions, first => Assert.All(positions, second =>
        {
            var dx = first.X - second.X;
            var dy = first.Y - second.Y;
            Assert.InRange(Math.Sqrt(dx * dx + dy * dy), 0, BeanOriginClusters.Distance);
        }));
        var layer = Assert.IsType<MemoryLayer>(session.Map.Layers.Last());
        var feature = Assert.Single(layer.Features);

        Assert.Equal("Origins (3)", Label(feature));
        Assert.Equal("Brazil, Colombia, Ethiopia", feature["Country"]);
        Assert.Equal(new[] { "Brazil", "Colombia", "Ethiopia" },
            Assert.IsType<string[]>(feature["Locations"]).Order(StringComparer.Ordinal));
        Tap(session, feature);
        Assert.Equal(new[] { 1, 2, 3 }, session.SelectedBeans.Select(bean => bean.Id));
        Assert.Equal("Origins", session.SelectionTitle);
        Assert.Equal(Label(feature), session.SelectionLabel);
        Tap(session, feature);
        Assert.False(session.HasSelection);
        Assert.Empty(session.SelectionLabel);
        Assert.Contains("Ethiopia (approximate country)", session.OriginStatus);
    }

    [Fact]
    public async Task CompletePlaceJourneyCachesRegionSplitsPinsTogglesCorrectBeansAndRestoresCameraAndSelection()
    {
        var directory = Path.Combine(Path.GetTempPath(), "BaristaNotes-PlaceJourney-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            using var client = NominatimOriginGeocoderTests.Client(async (_, token) =>
            {
                calls++;
                started.TrySetResult();
                await release.Task.WaitAsync(token);
                return NominatimOriginGeocoderTests.Response(NominatimOriginGeocoderTests.GujiResponse);
            });
            var geocoder = new NominatimOriginGeocoder(client, directory, NullLogger.Instance);
            var beans = Samples;
            var original = beans.Select(bean => (bean.Id, bean.Name, bean.Origin, bean.IsActive)).ToArray();
            BeanMapState saved;
            using (var session = new BeanMapSession(NullLogger.Instance, geocoder: geocoder, dispatch: action => action()))
            {
                session.Map.Layers.First().Enabled = false;
                session.Map.Navigator.SetSize(400, 300);
                var resolved = NextResolution(session);
                session.SetBeans(beans);
                Assert.Equal(4, session.OriginData!.MappedBeans.Count);
                Assert.Equal(5, Assert.Single(session.OriginData.UnmappedBeans).Id);
                await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
                session.Map.Navigator.CenterOnAndZoomTo(new MPoint(1000000, 2000000), 20000, duration: 0);
                var before = session.CaptureState()!;
                release.SetResult();
                await resolved.WaitAsync(TimeSpan.FromSeconds(30));
                AssertCamera(before, session.CaptureState()!);
                var layer = Assert.IsType<MemoryLayer>(session.Map.Layers.Last());
                var aggregate = layer.Features.Single(feature => Equals(feature["Country"], "Ethiopia"));
                Assert.Equal(2, Assert.IsType<string[]>(aggregate["Locations"]).Length);
                Assert.Equal("Ethiopia (2)", Label(aggregate));
                Tap(session, aggregate);
                Assert.Equal(new[] { 1, 4 }, session.SelectedBeans.Select(bean => bean.Id));
                Assert.Equal(Label(aggregate), session.SelectionLabel);
                Tap(session, aggregate);
                Assert.False(session.HasSelection);
                Assert.Empty(session.SelectionLabel);

                session.Map.Navigator.ZoomTo(1000, duration: 0);
                Assert.Equal(4, layer.Features.Count());
                var precise = layer.Features.Single(feature => Assert.IsType<string[]>(feature["Locations"]).Contains("Ethiopia|guji"));
                var approximate = layer.Features.Single(feature => Assert.IsType<string[]>(feature["Locations"]).Contains("Ethiopia"));
                Assert.Equal("Guji (1)", Label(precise));
                Assert.Equal("Ethiopia (1)", Label(approximate));
                Assert.Contains("Guji region, Ethiopia: 1", session.OriginStatus);
                Assert.Contains("Ethiopia (approximate country): 1", session.OriginStatus);
                Tap(session, precise);
                Assert.Equal(4, Assert.Single(session.SelectedBeans).Id);
                Assert.Equal("Guji", session.SelectionTitle);
                Assert.Equal(Label(precise), session.SelectionLabel);
                Tap(session, precise);
                Assert.False(session.HasSelection);
                Assert.Empty(session.SelectionLabel);
                Tap(session, approximate);
                Assert.Equal(1, Assert.Single(session.SelectedBeans).Id);
                Assert.Equal(Label(approximate), session.SelectionLabel);
                Tap(session, approximate);
                Assert.False(session.HasSelection);
                Tap(session, precise);
                saved = session.CaptureState()!;
                Assert.Equal(new[] { "Ethiopia|guji" }, saved.SelectedLocations);
                Assert.Equal(original, beans.Select(bean => (bean.Id, bean.Name, bean.Origin, bean.IsActive)).ToArray());
                Assert.Contains("Approximate country locations", session.OriginStatus);
                Assert.Equal("4 mapped beans; 1 unmapped. Approximate country locations.", session.FormerFooterStatus);
            }

            using var restored = new BeanMapSession(NullLogger.Instance, saved,
                new NominatimOriginGeocoder(client, directory, NullLogger.Instance), action => action());
            restored.Map.Layers.First().Enabled = false;
            var cached = NextResolution(restored);
            restored.SetBeans(beans);
            restored.Map.Navigator.SetSize(400, 300);
            await cached.WaitAsync(TimeSpan.FromSeconds(30));
            AssertCamera(saved, restored.CaptureState()!);
            Assert.Equal(4, Assert.Single(restored.SelectedBeans).Id);
            Assert.Equal("Guji", restored.SelectionTitle);
            Assert.Equal("Guji (1)", restored.SelectionLabel);
            Assert.Equal(1, calls);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DisposedSessionRejectsQueuedGeocoderCompletion()
    {
        var callbacks = new ConcurrentQueue<Action>();
        var scheduled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var geocoder = new Mock<IOriginGeocoder>();
        geocoder.Setup(service => service.ResolveAsync(It.IsAny<OriginLookup>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OriginLookupResult(new OriginLocation("Guji", 39.2030529, 5.5501401, OriginPrecision.Region), null));
        var session = new BeanMapSession(NullLogger.Instance, geocoder: geocoder.Object, dispatch: action =>
        {
            callbacks.Enqueue(action);
            scheduled.TrySetResult();
        });
        session.SetBeans(Samples);
        await scheduled.Task.WaitAsync(TimeSpan.FromSeconds(30));
        session.Dispose();
        var notifications = 0;
        session.OriginsChanged += (_, _) => notifications++;
        Assert.True(callbacks.TryDequeue(out var completion));
        Assert.NotNull(completion);
        completion();
        Assert.Equal(0, notifications);
        Assert.Equal(OriginPrecision.ApproximateCountry,
            session.OriginData!.Places.Single(place => place.Id == "Ethiopia|guji").Location.Precision);
    }

    private static Task NextResolution(BeanMapSession session)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            session.OriginsChanged -= handler;
            completed.TrySetResult();
        };
        session.OriginsChanged += handler;
        return completed.Task;
    }

    private static string Label(IFeature feature) =>
        Assert.IsType<string>(Assert.Single(feature.Styles.OfType<LabelStyle>()).GetLabelText(feature));
    private static void AssertCamera(BeanMapState expected, BeanMapState actual)
    {
        Assert.Equal(expected.CenterX, actual.CenterX);
        Assert.Equal(expected.CenterY, actual.CenterY);
        Assert.Equal(expected.Resolution, actual.Resolution);
        Assert.Equal(expected.Rotation, actual.Rotation);
        Assert.Equal(expected.InitialFitDone, actual.InitialFitDone);
    }

    private static void Tap(BeanMapSession session, IFeature feature)
    {
        var layer = Assert.IsType<MemoryLayer>(session.Map.Layers.Last());
        var position = new ScreenPosition(200, 150);
        var world = new MPoint(0, 0);
        var info = new MapInfo(position, world, session.Map.Navigator.Viewport.Resolution,
            feature.Styles.Select(style => new MapInfoRecord(feature, style, layer)));
        var args = new MapEventArgs(position, world, GestureType.SingleTap, session.Map,
            (_, _) => info, (_, _, _) => Task.FromResult(info));
        session.Map.OnTapped(args);
        Assert.True(args.Handled);
    }
}
