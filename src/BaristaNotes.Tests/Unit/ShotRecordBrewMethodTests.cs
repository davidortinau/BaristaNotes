using BaristaNotes.Core.Data;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BaristaNotes.Tests.Unit;

/// <summary>
/// Verifies the Phase D1 additive schema: BrewMethod + ParametersJson columns
/// on ShotRecord persist, default correctly, and the EquipmentType enum has
/// the new per-method values wired to CompatibleMethods.
/// </summary>
public class ShotRecordBrewMethodTests : IDisposable
{
    private readonly BaristaNotesContext _context;
    private readonly ShotRecordRepository _repo;
    private readonly Bag _bag;

    public ShotRecordBrewMethodTests()
    {
        var options = new DbContextOptionsBuilder<BaristaNotesContext>()
            .UseInMemoryDatabase(databaseName: $"ShotBrewMethodTest_{Guid.NewGuid()}")
            .Options;

        _context = new BaristaNotesContext(options);
        _repo = new ShotRecordRepository(_context);

        var bean = new Bean
        {
            Name = "Test Bean",
            Roaster = "Test Roaster",
            IsActive = true,
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        _context.Beans.Add(bean);
        _context.SaveChanges();

        _bag = new Bag
        {
            BeanId = bean.Id,
            RoastDate = DateTime.Now.AddDays(-7),
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        _context.Bags.Add(_bag);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task ShotRecord_DefaultsToEspresso_WhenBrewMethodNotSet()
    {
        var shot = new ShotRecord
        {
            Timestamp = DateTime.Now,
            BagId = _bag.Id,
            DoseIn = 18m,
            GrindMicrons = 270,
            ExpectedTime = 28m,
            ExpectedOutput = 36m,
            DrinkType = "Espresso",
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now,
        };
        await _repo.AddAsync(shot);

        var fetched = await _repo.GetByIdAsync(shot.Id);
        Assert.NotNull(fetched);
        Assert.Equal(BrewMethod.Espresso, fetched!.BrewMethod);
        Assert.Null(fetched.ParametersJson);
    }

    [Theory]
    [InlineData(BrewMethod.PourOver, "{\"bloomTime\":30,\"waterG\":300}")]
    [InlineData(BrewMethod.Moka, "{\"stovePower\":\"medium\"}")]
    [InlineData(BrewMethod.Aeropress, null)]
    [InlineData(BrewMethod.FrenchPress, "{\"steepMinutes\":4}")]
    [InlineData(BrewMethod.Drip, "{\"ratio\":\"1:16\"}")]
    public async Task ShotRecord_PersistsBrewMethodAndParametersJson(BrewMethod method, string? parametersJson)
    {
        var shot = new ShotRecord
        {
            Timestamp = DateTime.Now,
            BagId = _bag.Id,
            BrewMethod = method,
            ParametersJson = parametersJson,
            DoseIn = 20m,
            GrindMicrons = 270,
            ExpectedTime = 240m,
            ExpectedOutput = 300m,
            DrinkType = method.ToString(),
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now,
        };
        await _repo.AddAsync(shot);

        var fetched = await _repo.GetByIdAsync(shot.Id);
        Assert.NotNull(fetched);
        Assert.Equal(method, fetched!.BrewMethod);
        Assert.Equal(parametersJson, fetched.ParametersJson);
    }

    [Fact]
    public void EquipmentType_NewValuesMapToBrewMethods()
    {
        Assert.Contains(BrewMethod.Espresso, EquipmentType.Machine.CompatibleMethods());
        Assert.Contains(BrewMethod.PourOver, EquipmentType.PourOverDripper.CompatibleMethods());
        Assert.Contains(BrewMethod.Moka, EquipmentType.MokaPot.CompatibleMethods());
        Assert.Contains(BrewMethod.Drip, EquipmentType.DripMachine.CompatibleMethods());
        Assert.Contains(BrewMethod.Aeropress, EquipmentType.Aeropress.CompatibleMethods());
        Assert.Contains(BrewMethod.FrenchPress, EquipmentType.FrenchPress.CompatibleMethods());
    }

    [Fact]
    public void EquipmentType_AccessoriesMatchAllBrewMethods()
    {
        // Grinder, Tamper, PuckScreen, Other should be compatible with every method
        foreach (var accessory in new[] { EquipmentType.Grinder, EquipmentType.Tamper, EquipmentType.PuckScreen, EquipmentType.Other })
        {
            var methods = accessory.CompatibleMethods();
            foreach (var brewMethod in BrewMethodExtensions.All)
            {
                Assert.Contains(brewMethod, methods);
            }
        }
    }
}
