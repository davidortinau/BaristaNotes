using BaristaNotes.Core.Models;
using BaristaNotes.Services.Agent;

namespace BaristaNotes.Tests.Unit.Agent;

public sealed class AgentSnapshotMapperTests
{
    static readonly DateTimeOffset CreatedAt = new(2026, 10, 21, 9, 12, 0, TimeSpan.FromHours(2));

    [Theory]
    [InlineData(null, null, true)]   // nobody named: my drink
    [InlineData(1, null, true)]      // made by me, no recipient
    [InlineData(1, 1, true)]         // made by me for me
    [InlineData(1, 2, false)]        // made for a guest
    [InlineData(null, 2, false)]     // only a recipient named
    public void IsForMe_OnlyFalseWhenSomeoneElseIsTheRecipient(int? madeById, int? madeForId, bool expected)
    {
        Assert.Equal(expected, AgentSnapshotMapper.IsForMe(madeById, madeForId));
    }

    [Fact]
    public void Build_LeavesOutDeletedRowsAndTheirChildren()
    {
        var beans = new[]
        {
            new Bean { Id = 1, Name = "Kochere" },
            new Bean { Id = 2, Name = "Gone", IsDeleted = true },
        };
        var bags = new[]
        {
            new Bag { Id = 10, BeanId = 1 },
            new Bag { Id = 11, BeanId = 1, IsDeleted = true },
            new Bag { Id = 12, BeanId = 2 },                     // bean deleted
        };
        var shots = new[]
        {
            Shot(100, bagId: 10),
            Shot(101, bagId: 10, deleted: true),
            Shot(102, bagId: 11),                                // bag deleted
            Shot(103, bagId: 12),                                // bean deleted
        };

        var snapshot = AgentSnapshotMapper.Build(beans, bags, shots, CreatedAt);

        Assert.Equal(new[] { 1 }, snapshot.Beans.Select(bean => bean.Id));
        Assert.Equal(new[] { 10 }, snapshot.Bags.Select(bag => bag.Id));
        Assert.Equal(new[] { 100 }, snapshot.Shots.Select(shot => shot.Id));
        Assert.Equal(CreatedAt, snapshot.CreatedAt);
    }

    [Fact]
    public void Build_MapsOnlyTheSharedFields()
    {
        var roastDate = new DateTime(2026, 9, 24);
        var madeAt = new DateTime(2026, 9, 27, 7, 30, 0);
        var beans = new[]
        {
            new Bean
            {
                Id = 1, Name = "Kochere Natural", Roaster = " Lanternfish Coffee ", Origin = "Ethiopia",
                Notes = "  ", RoasterUrl = "https://example.invalid/private",
            },
        };
        var bags = new[] { new Bag { Id = 10, BeanId = 1, RoastDate = roastDate, IsComplete = true, Notes = "Gift from Sam" } };
        var shots = new[]
        {
            new ShotRecord
            {
                Id = 100, BagId = 10, Timestamp = madeAt, DrinkType = "Flat White", Rating = 4,
                TastingNotes = "Blueberry jam", MadeById = 1, MadeForId = 1, MachineId = 5, GrinderId = 6,
            },
        };

        var snapshot = AgentSnapshotMapper.Build(beans, bags, shots, CreatedAt);

        var bean = Assert.Single(snapshot.Beans);
        Assert.Equal("Kochere Natural", bean.Name);
        Assert.Equal("Lanternfish Coffee", bean.Roaster);
        Assert.Equal("Ethiopia", bean.Origin);
        Assert.Null(bean.Notes);

        var bag = Assert.Single(snapshot.Bags);
        Assert.Equal(1, bag.BeanId);
        Assert.Equal(roastDate, bag.RoastDate);
        Assert.True(bag.Finished);

        var shot = Assert.Single(snapshot.Shots);
        Assert.Equal(10, shot.BagId);
        Assert.Equal(madeAt, shot.MadeAt);
        Assert.Equal("Flat White", shot.DrinkType);
        Assert.Equal(4, shot.Rating);
        Assert.Equal("Blueberry jam", shot.TastingNotes);
        Assert.True(shot.ForMe);
    }

    [Fact]
    public void Build_MarksGuestDrinksAndOrdersDrinksByTime()
    {
        var beans = new[] { new Bean { Id = 1, Name = "Santos" } };
        var bags = new[] { new Bag { Id = 10, BeanId = 1 } };
        var shots = new[]
        {
            Shot(2, bagId: 10, at: new DateTime(2026, 8, 3), madeById: 1, madeForId: 2),
            Shot(1, bagId: 10, at: new DateTime(2026, 8, 1), madeById: 1, madeForId: 1),
        };

        var snapshot = AgentSnapshotMapper.Build(beans, bags, shots, CreatedAt);

        Assert.Equal(new[] { 1, 2 }, snapshot.Shots.Select(shot => shot.Id));
        Assert.True(snapshot.Shots[0].ForMe);
        Assert.False(snapshot.Shots[1].ForMe);
    }

    static ShotRecord Shot(
        int id,
        int bagId,
        bool deleted = false,
        DateTime? at = null,
        int? madeById = null,
        int? madeForId = null) =>
        new()
        {
            Id = id,
            BagId = bagId,
            IsDeleted = deleted,
            Timestamp = at ?? new DateTime(2026, 10, 1),
            DrinkType = "Espresso",
            MadeById = madeById,
            MadeForId = madeForId,
        };
}
