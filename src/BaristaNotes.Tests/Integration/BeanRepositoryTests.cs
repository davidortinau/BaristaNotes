using BaristaNotes.Core.Data;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BaristaNotes.Tests.Integration;

public sealed class BeanRepositoryTests : IDisposable
{
    private readonly BaristaNotesContext _context = SqliteTestContextFactory.Create();

    [Fact]
    public async Task GetActiveBeansWithActivityAsync_LoadsShotsOnlyForActiveBeans()
    {
        var activeBean = CreateBean(1, "Active", isActive: true);
        var inactiveBean = CreateBean(2, "Inactive", isActive: false);
        var activeBag = CreateBag(1, activeBean.Id);
        var inactiveBag = CreateBag(2, inactiveBean.Id);
        var activeShot = CreateShot(1, activeBag.Id);
        var inactiveShot = CreateShot(2, inactiveBag.Id);

        _context.AddRange(
            activeBean,
            inactiveBean,
            activeBag,
            inactiveBag,
            activeShot,
            inactiveShot);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var repository = new BeanRepository(_context);

        var beans = await repository.GetActiveBeansWithActivityAsync();

        var bean = Assert.Single(beans);
        Assert.Equal(activeBean.Id, bean.Id);
        var bag = Assert.Single(bean.Bags);
        Assert.Equal(activeBag.Id, bag.Id);
        Assert.Equal(activeShot.Id, Assert.Single(bag.ShotRecords).Id);
        Assert.DoesNotContain(
            _context.ChangeTracker.Entries<ShotRecord>(),
            entry => entry.Entity.Id == inactiveShot.Id);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private static Bean CreateBean(int id, string name, bool isActive) =>
        new()
        {
            Id = id,
            Name = name,
            IsActive = isActive,
            SyncId = Guid.NewGuid()
        };

    private static Bag CreateBag(int id, int beanId) =>
        new()
        {
            Id = id,
            BeanId = beanId,
            RoastDate = DateTime.UtcNow,
            SyncId = Guid.NewGuid()
        };

    private static ShotRecord CreateShot(int id, int bagId) =>
        new()
        {
            Id = id,
            BagId = bagId,
            DoseIn = 18,
            ExpectedTime = 30,
            ExpectedOutput = 36,
            DrinkType = "Espresso",
            SyncId = Guid.NewGuid()
        };
}
