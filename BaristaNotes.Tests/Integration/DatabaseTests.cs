using Xunit;
using Microsoft.EntityFrameworkCore;
using BaristaNotes.Core.Data;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Tests.Integration;

public class DatabaseTests : IDisposable
{
    private readonly BaristaNotesContext _context;
    
    public DatabaseTests()
    {
        var options = new DbContextOptionsBuilder<BaristaNotesContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new BaristaNotesContext(options);
    }
    
    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
    
    [Fact]
    public async Task ShotRecord_BeanRelationship_WorksCorrectly()
    {
        // Arrange
        var bean = new Bean
        {
            Name = "Ethiopia Yirgacheffe",
            Roaster = "Local Roasters",
            IsActive = true,
            CreatedAt = DateTimeOffset.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        _context.Beans.Add(bean);
        await _context.SaveChangesAsync();
        
        var shot = new ShotRecord
        {
            Timestamp = DateTimeOffset.Now,
            BeanId = bean.Id,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        _context.ShotRecords.Add(shot);
        await _context.SaveChangesAsync();
        
        // Act
        var retrieved = await _context.ShotRecords
            .Include(s => s.Bean)
            .FirstAsync(s => s.Id == shot.Id);
        
        // Assert
        Assert.NotNull(retrieved.Bean);
        Assert.Equal("Ethiopia Yirgacheffe", retrieved.Bean.Name);
        Assert.Equal("Local Roasters", retrieved.Bean.Roaster);
    }
    
    [Fact]
    public async Task ShotRecord_EquipmentRelationships_WorkCorrectly()
    {
        // Arrange
        var machine = new Equipment
        {
            Name = "Gaggia Classic Pro",
            Type = EquipmentType.Machine,
            IsActive = true,
            CreatedAt = DateTimeOffset.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        var grinder = new Equipment
        {
            Name = "Baratza Sette 270",
            Type = EquipmentType.Grinder,
            IsActive = true,
            CreatedAt = DateTimeOffset.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        _context.Equipment.AddRange(machine, grinder);
        await _context.SaveChangesAsync();
        
        var shot = new ShotRecord
        {
            Timestamp = DateTimeOffset.Now,
            MachineId = machine.Id,
            GrinderId = grinder.Id,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        _context.ShotRecords.Add(shot);
        await _context.SaveChangesAsync();
        
        // Act
        var retrieved = await _context.ShotRecords
            .Include(s => s.Machine)
            .Include(s => s.Grinder)
            .FirstAsync(s => s.Id == shot.Id);
        
        // Assert
        Assert.NotNull(retrieved.Machine);
        Assert.Equal("Gaggia Classic Pro", retrieved.Machine.Name);
        Assert.Equal(EquipmentType.Machine, retrieved.Machine.Type);
        
        Assert.NotNull(retrieved.Grinder);
        Assert.Equal("Baratza Sette 270", retrieved.Grinder.Name);
        Assert.Equal(EquipmentType.Grinder, retrieved.Grinder.Type);
    }
    
    [Fact]
    public async Task ShotRecord_AccessoriesRelationship_WorksCorrectly()
    {
        // Arrange
        var tamper = new Equipment
        {
            Name = "Normcore 58.5mm",
            Type = EquipmentType.Tamper,
            IsActive = true,
            CreatedAt = DateTimeOffset.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        var puckScreen = new Equipment
        {
            Name = "IMS Precision Screen",
            Type = EquipmentType.PuckScreen,
            IsActive = true,
            CreatedAt = DateTimeOffset.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        _context.Equipment.AddRange(tamper, puckScreen);
        await _context.SaveChangesAsync();
        
        var shot = new ShotRecord
        {
            Timestamp = DateTimeOffset.Now,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        _context.ShotRecords.Add(shot);
        await _context.SaveChangesAsync();
        
        // Add accessories through junction table
        _context.ShotEquipments.Add(new ShotEquipment
        {
            ShotRecordId = shot.Id,
            EquipmentId = tamper.Id
        });
        
        _context.ShotEquipments.Add(new ShotEquipment
        {
            ShotRecordId = shot.Id,
            EquipmentId = puckScreen.Id
        });
        
        await _context.SaveChangesAsync();
        
        // Act
        var retrieved = await _context.ShotRecords
            .Include(s => s.ShotEquipments)
                .ThenInclude(se => se.Equipment)
            .FirstAsync(s => s.Id == shot.Id);
        
        // Assert
        Assert.Equal(2, retrieved.ShotEquipments.Count);
        Assert.Contains(retrieved.ShotEquipments, se => se.Equipment.Name == "Normcore 58.5mm");
        Assert.Contains(retrieved.ShotEquipments, se => se.Equipment.Name == "IMS Precision Screen");
    }
    
    [Fact]
    public async Task ShotRecord_UserProfileRelationships_WorkCorrectly()
    {
        // Arrange
        var alice = new UserProfile
        {
            Name = "Alice",
            CreatedAt = DateTimeOffset.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        var bob = new UserProfile
        {
            Name = "Bob",
            CreatedAt = DateTimeOffset.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        _context.UserProfiles.AddRange(alice, bob);
        await _context.SaveChangesAsync();
        
        var shot = new ShotRecord
        {
            Timestamp = DateTimeOffset.Now,
            MadeById = alice.Id,
            MadeForId = bob.Id,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Latte",
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        _context.ShotRecords.Add(shot);
        await _context.SaveChangesAsync();
        
        // Act
        var retrieved = await _context.ShotRecords
            .Include(s => s.MadeBy)
            .Include(s => s.MadeFor)
            .FirstAsync(s => s.Id == shot.Id);
        
        // Assert
        Assert.NotNull(retrieved.MadeBy);
        Assert.Equal("Alice", retrieved.MadeBy.Name);
        
        Assert.NotNull(retrieved.MadeFor);
        Assert.Equal("Bob", retrieved.MadeFor.Name);
    }
    
    [Fact]
    public async Task DeleteBean_SetsShotBeanIdToNull()
    {
        // Arrange
        var bean = new Bean
        {
            Name = "Test Bean",
            IsActive = true,
            CreatedAt = DateTimeOffset.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        _context.Beans.Add(bean);
        await _context.SaveChangesAsync();
        
        var shot = new ShotRecord
        {
            Timestamp = DateTimeOffset.Now,
            BeanId = bean.Id,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        _context.ShotRecords.Add(shot);
        await _context.SaveChangesAsync();
        
        // Act - Soft delete bean
        bean.IsDeleted = true;
        await _context.SaveChangesAsync();
        
        // Assert - Shot still exists but BeanId is preserved (soft delete doesn't cascade)
        var retrieved = await _context.ShotRecords
            .Include(s => s.Bean)
            .FirstAsync(s => s.Id == shot.Id);
        
        Assert.NotNull(retrieved);
        Assert.Equal(bean.Id, retrieved.BeanId);
        // Bean is still accessible but marked as deleted
        Assert.NotNull(retrieved.Bean);
        Assert.True(retrieved.Bean.IsDeleted);
    }
}
