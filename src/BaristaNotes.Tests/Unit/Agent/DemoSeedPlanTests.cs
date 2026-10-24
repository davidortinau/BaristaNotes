using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Services.Agent;

namespace BaristaNotes.Tests.Unit.Agent;

public sealed class DemoSeedPlanTests
{
    static readonly DateTime Now = new(2026, 10, 27, 15, 30, 0);

    static DemoData LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Agent", "demo-data.json");
        using var stream = File.OpenRead(path);
        return DemoSeedPlan.Parse(stream);
    }

    [Fact]
    public void Parse_ReadsTheShippedFixture()
    {
        var data = LoadFixture();

        Assert.Equal(2, data.Profiles.Count);
        Assert.Equal(6, data.Beans.Count);
        Assert.Equal(6, data.Bags.Count);
        Assert.Equal(26, data.Shots.Count);
    }

    [Fact]
    public void Build_UsesRelativeRoastDatesAndFinishedFlags()
    {
        var entities = DemoSeedPlan.Build(LoadFixture(), Now);

        var kochere = entities.Bags.Single(bag => bag.Bean.Name == "Kochere Natural");
        Assert.Equal(Now.Date.AddDays(-27), kochere.RoastDate);
        Assert.False(kochere.IsComplete);
        Assert.True(kochere.IsActive);

        var santos = entities.Bags.Single(bag => bag.Bean.Name == "Santos Dark");
        Assert.Equal(Now.Date.AddDays(-75), santos.RoastDate);
        Assert.True(santos.IsComplete);
    }

    [Fact]
    public void Build_MapsPeopleSoGuestDrinksAreNotForMe()
    {
        var entities = DemoSeedPlan.Build(LoadFixture(), Now);
        var me = entities.Profiles.Single(profile => profile.Name == "David");
        var guest = entities.Profiles.Single(profile => profile.Name == "Sam");

        Assert.All(entities.Shots, shot => Assert.Same(me, shot.MadeBy));
        Assert.Equal(3, entities.Shots.Count(shot => ReferenceEquals(shot.MadeFor, guest)));
        Assert.Equal(23, entities.Shots.Count(shot => ReferenceEquals(shot.MadeFor, me)));
    }

    [Fact]
    public void Build_PlacesDrinksOnMorningsInThePastOldestFirst()
    {
        var entities = DemoSeedPlan.Build(LoadFixture(), Now);

        Assert.All(entities.Shots, shot =>
        {
            Assert.True(shot.Timestamp < Now);
            Assert.InRange(shot.Timestamp.Hour, 7, 9);
        });
        Assert.Equal(
            entities.Shots.Select(shot => shot.Timestamp.Date).OrderBy(date => date),
            entities.Shots.Select(shot => shot.Timestamp.Date));
    }

    [Fact]
    public void Build_CopiesRecipeValuesAndSetsSyncMetadata()
    {
        var entities = DemoSeedPlan.Build(LoadFixture(), Now);
        var best = entities.Shots.Single(shot => shot.TastingNotes == "Blueberry jam and jasmine. Best shot this month.");

        Assert.Equal(18m, best.DoseIn);
        Assert.Equal(40m, best.ActualOutput);
        Assert.Equal(40m, best.ExpectedOutput);
        Assert.Equal(29m, best.ActualTime);
        Assert.Equal(29m, best.ExpectedTime);
        Assert.Equal(4, best.Rating);
        Assert.Equal(BrewMethod.Espresso, best.BrewMethod);
        Assert.Equal(Now.Date.AddDays(-24), best.Timestamp.Date);

        var syncIds = entities.Shots.Select(shot => shot.SyncId)
            .Concat(entities.Bags.Select(bag => bag.SyncId))
            .Concat(entities.Beans.Select(bean => bean.SyncId))
            .Concat(entities.Profiles.Select(profile => profile.SyncId))
            .ToList();
        Assert.DoesNotContain(Guid.Empty, syncIds);
        Assert.Equal(syncIds.Count, syncIds.Distinct().Count());
    }

    [Fact]
    public void Build_SharesNoGuestDrinkAsMine()
    {
        // End to end with the snapshot rule: the agent learns only from my drinks.
        var entities = DemoSeedPlan.Build(LoadFixture(), Now);
        var profileIds = entities.Profiles.Select((profile, index) => (profile, id: index + 1)).ToList();
        foreach (var (profile, id) in profileIds)
        {
            profile.Id = id;
        }

        var forMe = entities.Shots.Count(shot => AgentSnapshotMapper.IsForMe(shot.MadeBy?.Id, shot.MadeFor?.Id));
        Assert.Equal(23, forMe);
    }

    [Theory]
    [InlineData("Pour Over", BrewMethod.PourOver)]
    [InlineData("pour over", BrewMethod.PourOver)]
    [InlineData("Latte", BrewMethod.Espresso)]
    [InlineData("Espresso", BrewMethod.Espresso)]
    public void BrewMethodFor_IsEspressoExceptPourOver(string drinkType, BrewMethod expected)
    {
        Assert.Equal(expected, DemoSeedPlan.BrewMethodFor(drinkType));
    }

    [Fact]
    public void MorningTime_NeverReturnsAFutureTime()
    {
        var earlyMorning = new DateTime(2026, 10, 27, 6, 0, 0);
        Assert.True(DemoSeedPlan.MorningTime(earlyMorning, daysAgo: 0, index: 3) < earlyMorning);
    }

    [Fact]
    public void Build_RejectsAnUnknownBagKey()
    {
        var data = new DemoData
        {
            Profiles = [new DemoProfile { Key = "me", Name = "Me" }],
            Shots = [new DemoShot { Bag = "missing", MadeBy = "me" }],
        };

        Assert.Throws<InvalidDataException>(() => DemoSeedPlan.Build(data, Now));
    }
}
