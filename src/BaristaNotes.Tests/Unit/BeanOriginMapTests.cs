using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Manipulations;
using Mapsui.Projections;
using Mapsui.Rendering;
using Mapsui.Styles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BaristaNotes.Tests.Unit;

public sealed class BeanOriginMapTests
{
    private static BeanDto Bean(int id, string? origin, bool active = true) =>
        new() { Id = id, Name = $"Map sample {id}", Origin = origin, IsActive = active };

    private static void AssertCamera(BeanMapState expected, BeanMapState? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.CenterX, actual.CenterX);
        Assert.Equal(expected.CenterY, actual.CenterY);
        Assert.Equal(expected.Resolution, actual.Resolution);
        Assert.Equal(expected.Rotation, actual.Rotation);
        Assert.Equal(expected.InitialFitDone, actual.InitialFitDone);
        Assert.Equal(expected.SelectedCountries.ToArray(), actual.SelectedCountries.ToArray());
    }

    [Fact]
    public void PublicCatalogueRetainsAll177NamesAliasesAndValidPositions()
    {
        Assert.Equal(177, BeanOriginMapData.CountryCatalogue.Count);
        Assert.Equal(177, BeanOriginMapData.CountryCatalogue.Select(country => country.Name).Distinct().Count());
        foreach (var country in BeanOriginMapData.CountryCatalogue)
        {
            Assert.InRange(country.Longitude, -180, 180);
            Assert.InRange(country.Latitude, -85, 85);
            foreach (var name in country.Aliases.Append(country.Name))
                Assert.Equal(country.Name, Assert.Single(BeanOriginMapData.Resolve(name)).Name);
        }
        var ethiopia = Assert.Single(BeanOriginMapData.Resolve("Ethiopia"));
        Assert.Equal(39.0886, ethiopia.Longitude);
        Assert.Equal(8.032795, ethiopia.Latitude);
        var colombia = Assert.Single(BeanOriginMapData.Resolve("Colombia"));
        Assert.Equal(-73.174347, colombia.Longitude);
        Assert.Equal(3.373111, colombia.Latitude);
        var brazil = Assert.Single(BeanOriginMapData.Resolve("Brazil"));
        Assert.Equal(-49.55945, brazil.Longitude);
        Assert.Equal(-12.098687, brazil.Latitude);
    }

    [Theory]
    [InlineData("Guji, Ethiopia", "Ethiopia")]
    [InlineData("Guji/Ethiopia", "Ethiopia")]
    [InlineData("  eTHiOPia  ", "Ethiopia")]
    [InlineData("Cote d'Ivoire", "Ivory Coast")]
    [InlineData("Papua New Guinea", "Papua New Guinea")]
    [InlineData("Equatorial Guinea", "Equatorial Guinea")]
    [InlineData("Democratic Republic of the Congo", "Democratic Republic of the Congo")]
    [InlineData("South Sudan", "South Sudan")]
    [InlineData("Nigeria", "Nigeria")]
    public void RecognizedOriginsUseOnlyTheNamedCountry(string origin, string expected) =>
        Assert.Equal(expected, Assert.Single(BeanOriginMapData.Resolve(origin)).Name);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Guji")]
    [InlineData("Unknown farm")]
    [InlineData("Ethiopian")]
    public void UnresolvedOriginsHaveNoInventedPosition(string? origin) =>
        Assert.Empty(BeanOriginMapData.Resolve(origin));

    [Fact]
    public void ApprovedSamplesIncludeArchivedAndDeduplicateSavedBeanIds()
    {
        var data = new BeanOriginMapData([
            Bean(1, "Ethiopia"), Bean(2, "Colombia"), Bean(3, "Brazil", active: false),
            Bean(4, "Guji, Ethiopia"), Bean(5, ""), Bean(1, "Ethiopia")
        ]);

        Assert.Equal(3, data.Countries.Count);
        Assert.Equal(4, data.MappedBeans.Count);
        Assert.Equal(5, Assert.Single(data.UnmappedBeans).Id);
        Assert.Equal(new[] { 1, 4 }, data.BeansForCountries(["Ethiopia"]).Select(bean => bean.Id));
        Assert.False(Assert.Single(data.BeansForCountries(["Brazil"])).IsActive);
    }

    [Fact]
    public void BlendsCountAtEachCountryButCombinedRowsAndTotalsAreDistinct()
    {
        var data = new BeanOriginMapData([
            Bean(1, "Brazil / Colombia / Brazil"), Bean(2, "Colombia and Ethiopia"), Bean(3, null)
        ]);

        Assert.Equal(2, data.MappedBeans.Count);
        Assert.Equal(1, data.UnmappedBeans.Count);
        Assert.Equal(2, data.Countries.Single(group => group.Country.Name == "Colombia").Beans.Count);
        Assert.Equal(1, data.Countries.Single(group => group.Country.Name == "Brazil").Beans.Count);
        Assert.Equal(new[] { 1, 2 }, data.BeansForCountries(["Brazil", "Colombia", "Ethiopia", "Brazil"]).Select(bean => bean.Id));
    }

    [Fact]
    public async Task MapReadIncludesArchivedButExcludesDeletedWithoutChangingActiveListRead()
    {
        var repository = new Mock<IBeanRepository>();
        var active = new Bean { Id = 1, Name = "Active", Origin = "Ethiopia", IsActive = true };
        var archived = new Bean { Id = 2, Name = "Archived", Origin = "Brazil", IsActive = false };
        var deleted = new Bean { Id = 3, Name = "Deleted", Origin = "Colombia", IsActive = true, IsDeleted = true };
        repository.Setup(repo => repo.GetNonDeletedBeansAsync(null)).ReturnsAsync([active, archived, deleted, active]);
        repository.Setup(repo => repo.GetActiveBeansAsync()).ReturnsAsync([active]);
        var service = new BeanService(repository.Object, Mock.Of<IRatingService>());

        Assert.Equal(new[] { 1, 2 }, (await service.GetAllSavedBeansAsync()).Select(bean => bean.Id));
        Assert.Equal(1, Assert.Single(await service.GetAllActiveBeansAsync()).Id);
    }

    [Fact]
    public void InitialBoundsHavePaddingAndCountryScaleForSingleOrigin()
    {
        var single = new BeanOriginMapData([Bean(1, "Ethiopia")]);
        var bounds = BeanMapSession.InitialBounds(single);
        Assert.Equal(1500000, bounds.Width, precision: 5);
        Assert.Equal(1500000, bounds.Height, precision: 5);
        var (x, y) = SphericalMercator.FromLonLat(39.0886, 8.032795);
        Assert.Equal(x, bounds.Centroid.X, precision: 5);
        Assert.Equal(y, bounds.Centroid.Y, precision: 5);

        var multiple = new BeanOriginMapData([Bean(1, "Ethiopia"), Bean(2, "Colombia"), Bean(3, "Brazil")]);
        bounds = BeanMapSession.InitialBounds(multiple);
        foreach (var country in multiple.Countries)
        {
            var point = SphericalMercator.FromLonLat(country.Country.Longitude, country.Country.Latitude);
            Assert.InRange(point.x, bounds.MinX + 749999, bounds.MaxX - 749999);
            Assert.InRange(point.y, bounds.MinY + 749999, bounds.MaxY - 749999);
        }
    }

    [Fact]
    public void InitialFitWaitsForViewportThenRefreshAndRecreationKeepExploredCamera()
    {
        BeanMapState state;
        using (var session = new BeanMapSession(NullLogger.Instance))
        {
            session.Map.Layers.First().Enabled = false;
            session.SetBeans([Bean(1, "Ethiopia")]);
            session.Map.Navigator.SetSize(400, 180);
            var country = SphericalMercator.FromLonLat(39.0886, 8.032795);
            Assert.Equal(country.x, session.Map.Navigator.Viewport.CenterX, precision: 5);
            Assert.Equal(country.y, session.Map.Navigator.Viewport.CenterY, precision: 5);
            Assert.InRange(session.Map.Navigator.Viewport.Resolution, 3750, 10000);
            session.Map.Navigator.CenterOnAndZoomTo(new MPoint(1000000, 2000000), 4000, duration: 0);
            session.SelectCountries(["Ethiopia"]);
            state = Assert.IsType<BeanMapState>(session.CaptureState());
            session.SetBeans([Bean(1, "Ethiopia"), Bean(2, "Colombia")]);
            AssertCamera(state, session.CaptureState());
        }

        using var restored = new BeanMapSession(NullLogger.Instance, state);
        restored.Map.Layers.First().Enabled = false;
        restored.SetBeans([Bean(1, "Ethiopia"), Bean(2, "Brazil")]);
        restored.Map.Navigator.SetSize(400, 180);
        var camera = restored.CaptureState();
        Assert.NotNull(camera);
        Assert.Equal(state.CenterX, camera.CenterX);
        Assert.Equal(state.CenterY, camera.CenterY);
        Assert.Equal(state.Resolution, camera.Resolution);
        Assert.Equal(state.Rotation, camera.Rotation);
        Assert.True(restored.HasSelection);
        Assert.Equal(1, Assert.Single(restored.SelectedBeans).Id);
    }

    [Fact]
    public void EmptyOrUnmappedDataHasExplicitWorldOverviewAndDoesNotRecenterOnRefresh()
    {
        using var session = new BeanMapSession(NullLogger.Instance);
        session.Map.Layers.First().Enabled = false;
        session.Map.Navigator.SetSize(400, 180);
        session.SetBeans([Bean(1, null)]);
        Assert.Contains("World overview", session.OriginStatus);
        Assert.Contains("1 unmapped", session.OriginStatus);
        Assert.Empty(Assert.IsType<MemoryLayer>(session.Map.Layers.Last()).Features);
        session.Map.Navigator.CenterOnAndZoomTo(new MPoint(1000000, 2000000), 4000, duration: 0);
        var camera = session.CaptureState();
        session.SetBeans([Bean(1, "Ethiopia")]);
        AssertCamera(Assert.IsType<BeanMapState>(camera), session.CaptureState());
    }

    [Fact]
    public void ExploringBeforeTheFirstOriginReadKeepsTheUserCamera()
    {
        using var session = new BeanMapSession(NullLogger.Instance);
        session.Map.Layers.First().Enabled = false;
        session.Map.Navigator.SetSize(400, 180);
        var position = new ScreenPosition(0, 0);
        var world = new MPoint(0, 0);
        var info = new MapInfo(position, world, 1000);
        session.Map.OnPointerPressed(new MapEventArgs(position, world, GestureType.Press, session.Map,
            (_, _) => info, (_, _, _) => Task.FromResult(info)));
        session.Map.Navigator.CenterOnAndZoomTo(new MPoint(1000000, 2000000), 4000, duration: 0);
        var state = Assert.IsType<BeanMapState>(session.CaptureState());

        session.SetBeans([Bean(1, "Ethiopia")]);

        AssertCamera(state, session.CaptureState());
    }

    [Fact]
    public void PinsAndSelectionsExposeDistinctCountsAndClearRemovedCountries()
    {
        using var session = new BeanMapSession(NullLogger.Instance);
        session.SetBeans([Bean(1, "Ethiopia"), Bean(2, "Guji, Ethiopia", active: false), Bean(3, "")]);
        var feature = Assert.Single(Assert.IsType<MemoryLayer>(session.Map.Layers.Last()).Features);
        Assert.Equal("Ethiopia", feature["Country"]);
        Assert.Equal("Ethiopia (2)", Assert.Single(feature.Styles.OfType<LabelStyle>()).GetLabelText(feature));
        Assert.Contains("2 mapped beans; 1 unmapped", session.OriginStatus);
        Assert.Contains("Approximate country locations", session.OriginStatus);
        var notifications = 0;
        session.SelectionChanged += (_, _) => notifications++;
        session.SelectCountries(["Ethiopia", "Ethiopia"]);
        Assert.Equal(2, session.SelectedBeans.Count);
        Assert.Equal(1, notifications);
        session.SelectCountries([]);
        Assert.False(session.HasSelection);
        session.SelectCountries(["Ethiopia"]);
        session.SetBeans([Bean(3, "")]);
        Assert.False(session.HasSelection);
        Assert.Empty(session.SelectedBeans);
    }

    [Fact]
    public void RepeatedPinTapClearsSelectionAndAnotherCountryReplacesItWithoutMovingCamera()
    {
        using var session = new BeanMapSession(NullLogger.Instance);
        session.Map.Layers.First().Enabled = false;
        session.Map.Navigator.SetSize(400, 300);
        session.SetBeans([Bean(1, "Ethiopia"), Bean(2, "Colombia"), Bean(3, "Brazil"),
            Bean(4, "Guji/Ethiopia"), Bean(5, "")]);
        session.Map.Navigator.CenterOnAndZoomTo(new MPoint(1000000, 2000000), 4000, duration: 0);
        var viewport = session.Map.Navigator.Viewport;
        var layer = Assert.IsType<MemoryLayer>(session.Map.Layers.Last());
        var notifications = 0;
        session.SelectionChanged += (_, _) => notifications++;

        void Tap(string country)
        {
            var feature = layer.Features.Single(feature => Equals(feature["Country"], country));
            var position = new ScreenPosition(200, 150);
            var info = new MapInfo(position, new MPoint(0, 0), 4000,
                feature.Styles.Select(style => new MapInfoRecord(feature, style, layer)));
            var args = new MapEventArgs(position, info.WorldPosition, GestureType.SingleTap, session.Map,
                (_, _) => info, (_, _, _) => Task.FromResult(info));
            session.Map.OnTapped(args);
            Assert.True(args.Handled);
            Assert.Equal(viewport, session.Map.Navigator.Viewport);
        }

        Tap("Ethiopia");
        Assert.Equal(new[] { 1, 4 }, session.SelectedBeans.Select(bean => bean.Id));
        Tap("Ethiopia");
        Assert.False(session.HasSelection);
        Assert.Equal(5, session.OriginData!.MappedBeans.Count + session.OriginData.UnmappedBeans.Count);
        Tap("Ethiopia");
        Tap("Colombia");
        Assert.Equal("Colombia", session.SelectionTitle);
        Assert.Equal(2, Assert.Single(session.SelectedBeans).Id);
        Tap("Colombia");
        Assert.False(session.HasSelection);
        Assert.Equal(5, notifications);
    }

    [Fact]
    public async Task RecognizedDoubleTapZoomsAtTheTapPositionWithoutASecondSelectionToggle()
    {
        using var session = new BeanMapSession(NullLogger.Instance);
        session.Map.Layers.First().Enabled = false;
        session.Map.Navigator.SetSize(400, 300);
        session.SetBeans([Bean(1, "Ethiopia"), Bean(2, "Guji/Ethiopia")]);
        session.Map.Navigator.CenterOnAndZoomTo(new MPoint(1000000, 2000000), 4000, duration: 0);
        var layer = Assert.IsType<MemoryLayer>(session.Map.Layers.Last());
        var feature = Assert.Single(layer.Features);
        var position = new ScreenPosition(120, 80);
        var before = session.Map.Navigator.Viewport;
        var world = before.ScreenToWorld(position);
        var info = new MapInfo(position, world, before.Resolution,
            feature.Styles.Select(style => new MapInfoRecord(feature, style, layer)));
        var selections = 0;
        session.SelectionChanged += (_, _) => selections++;
        session.Map.OnTapped(new MapEventArgs(position, world, GestureType.SingleTap, session.Map,
            (_, _) => info, (_, _, _) => Task.FromResult(info)));
        Assert.Equal("Ethiopia", session.SelectionTitle);

        var doubleTap = new MapEventArgs(position, world, GestureType.DoubleTap, session.Map,
            (_, _) => throw new InvalidOperationException("Double tap must not perform country hit testing."),
            (_, _, _) => Task.FromResult(info));
        session.Map.OnTapped(doubleTap);
        await Task.Delay(250);
        session.Map.Navigator.UpdateAnimations();

        Assert.True(doubleTap.Handled);
        Assert.Equal(1, selections);
        Assert.Equal(2, session.SelectedBeans.Count);
        var after = session.Map.Navigator.Viewport;
        Assert.Equal(Mapsui.Utilities.ZoomHelper.GetResolutionToZoomIn(session.Map.Navigator.Resolutions, before.Resolution),
            after.Resolution);
        var anchored = after.ScreenToWorld(position);
        Assert.Equal(world.X, anchored.X, precision: 5);
        Assert.Equal(world.Y, anchored.Y, precision: 5);
    }

    [Fact]
    public void OnlySingleTapSelectsHitCountriesAndDeduplicatesOverlappingPinAndLabelHits()
    {
        using var session = new BeanMapSession(NullLogger.Instance);
        session.SetBeans([Bean(1, "Brazil / Colombia"), Bean(2, "Colombia")]);
        var layer = Assert.IsType<MemoryLayer>(session.Map.Layers.Last());
        var records = layer.Features.SelectMany(feature => feature.Styles
            .Select(style => new MapInfoRecord(feature, style, layer))).ToArray();
        var position = new ScreenPosition(0, 0);
        var world = new MPoint(0, 0);
        var info = new MapInfo(position, world, 1000, records);
        MapEventArgs Tap(GestureType gesture) => new(position, world, gesture, session.Map,
            (_, layers) =>
            {
                Assert.Same(layer, Assert.Single(layers));
                return info;
            }, (_, _, _) => Task.FromResult(info));

        session.Map.OnTapped(Tap(GestureType.DoubleTap));
        session.Map.OnTapped(Tap(GestureType.Drag));
        Assert.False(session.HasSelection);
        var tap = Tap(GestureType.SingleTap);
        session.Map.OnTapped(tap);
        Assert.True(tap.Handled);
        Assert.Equal("Origins", session.SelectionTitle);
        Assert.Equal("Origins (2)", session.SelectionLabel);
        Assert.Equal(new[] { 1, 2 }, session.SelectedBeans.Select(bean => bean.Id));
    }
}
