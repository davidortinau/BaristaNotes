using BaristaNotes.Core.Data;
using BaristaNotes.Core.Data.CompiledModels;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BaristaNotes.Tests.Integration;

public sealed class UpdatePersistenceTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(), $"BaristaNotes-update-tests-{Guid.NewGuid():N}.db");

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();

        var bean = new Bean
        {
            Id = 1,
            Name = "Synthetic bean",
            Roaster = "Original roaster",
            Origin = "Original origin",
            Notes = "Original bean notes",
            RoasterUrl = "https://example.com/coffee",
            IsActive = true,
            CreatedAt = DateTime.Now,
            LastModifiedAt = DateTime.Now,
            SyncId = Guid.NewGuid()
        };
        var bag = new Bag
        {
            Id = 101,
            Bean = bean,
            RoastDate = DateTime.Today,
            CreatedAt = DateTime.Now,
            LastModifiedAt = DateTime.Now,
            SyncId = Guid.NewGuid()
        };
        var machine = CreateEquipment(201, EquipmentType.Machine);
        var grinder = CreateEquipment(202, EquipmentType.Grinder);
        var shot = new ShotRecord
        {
            Id = 301,
            Bag = bag,
            Machine = machine,
            Grinder = grinder,
            Timestamp = DateTime.Now,
            BrewMethod = BrewMethod.Espresso,
            DrinkType = "Espresso",
            DoseIn = 18,
            ExpectedTime = 28,
            ExpectedOutput = 36,
            Rating = 3,
            TastingNotes = "Original tasting notes",
            LastModifiedAt = DateTime.Now,
            SyncId = Guid.NewGuid()
        };

        context.AddRange(bean, bag, machine, grinder, shot);
        await context.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        File.Delete(_databasePath);
        File.Delete(_databasePath + "-wal");
        File.Delete(_databasePath + "-shm");
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task RatingOnlyUpdate_PreservesTastingNotes(int rating)
    {
        await UpdateShotAsync(new UpdateShotDto { DrinkType = "Espresso", Rating = rating });

        await using var verification = CreateContext();
        var stored = await verification.ShotRecords.SingleAsync(shot => shot.Id == 301);
        Assert.Equal(rating, stored.Rating);
        Assert.Equal("Original tasting notes", stored.TastingNotes);
        Assert.Equal(201, stored.MachineId);
        Assert.Equal(202, stored.GrinderId);
    }

    [Fact]
    public async Task NotesOnlyUpdate_PreservesRating()
    {
        await UpdateShotAsync(new UpdateShotDto
        {
            DrinkType = "Espresso",
            TastingNotes = "Updated tasting notes"
        });

        await using var verification = CreateContext();
        var stored = await verification.ShotRecords.SingleAsync(shot => shot.Id == 301);
        Assert.Equal(3, stored.Rating);
        Assert.Equal("Updated tasting notes", stored.TastingNotes);
    }

    [Fact]
    public async Task ExplicitNulls_ClearRatingAndNotes()
    {
        await UpdateShotAsync(new UpdateShotDto
        {
            DrinkType = "Espresso",
            Rating = FieldUpdate<int?>.Set(null),
            TastingNotes = FieldUpdate<string?>.Set(null)
        });

        await using var verification = CreateContext();
        var stored = await verification.ShotRecords.SingleAsync(shot => shot.Id == 301);
        Assert.Null(stored.Rating);
        Assert.Null(stored.TastingNotes);
        Assert.Equal(201, stored.MachineId);
    }

    [Fact]
    public async Task ExplicitEquipmentClears_PersistWithoutChangingOtherFields()
    {
        await UpdateShotAsync(new UpdateShotDto
        {
            DrinkType = "Espresso",
            MachineId = FieldUpdate<int?>.Set(null),
            GrinderId = FieldUpdate<int?>.Set(null)
        });

        await using var verification = CreateContext();
        var stored = await verification.ShotRecords.SingleAsync(shot => shot.Id == 301);
        Assert.Null(stored.MachineId);
        Assert.Null(stored.GrinderId);
        Assert.Equal(101, stored.BagId);
        Assert.Equal(3, stored.Rating);
        Assert.Equal("Original tasting notes", stored.TastingNotes);
    }

    [Fact]
    public async Task OmittedShotFields_PreserveExistingValues()
    {
        await UpdateShotAsync(new UpdateShotDto { DrinkType = "Latte" });

        await using var verification = CreateContext();
        var stored = await verification.ShotRecords.SingleAsync(shot => shot.Id == 301);
        Assert.Equal("Latte", stored.DrinkType);
        Assert.Equal(201, stored.MachineId);
        Assert.Equal(202, stored.GrinderId);
        Assert.Equal(3, stored.Rating);
        Assert.Equal("Original tasting notes", stored.TastingNotes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public async Task InvalidRating_DoesNotMutateStoredFields(int rating)
    {
        await Assert.ThrowsAsync<ValidationException>(() => UpdateShotAsync(
            new UpdateShotDto { DrinkType = "Espresso", Rating = rating }));

        await using var verification = CreateContext();
        var stored = await verification.ShotRecords.SingleAsync(shot => shot.Id == 301);
        Assert.Equal(3, stored.Rating);
        Assert.Equal("Original tasting notes", stored.TastingNotes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BeanOptionalText_DistinguishesClearFromOmission(bool clear)
    {
        await using (var context = CreateContext())
        {
            var service = new BeanService(
                new BeanRepository(context),
                new RatingService(context),
                NullLogger<BeanService>.Instance);
            var update = clear
                ? new UpdateBeanDto
                {
                    Roaster = FieldUpdate<string?>.Set(null),
                    Origin = FieldUpdate<string?>.Set(null),
                    Notes = FieldUpdate<string?>.Set(null)
                }
                : new UpdateBeanDto { Name = "Renamed bean" };
            await service.UpdateBeanAsync(1, update);
        }

        await using var verification = CreateContext();
        var stored = await verification.Beans.SingleAsync(bean => bean.Id == 1);
        Assert.Equal(clear ? null : "Original roaster", stored.Roaster);
        Assert.Equal(clear ? null : "Original origin", stored.Origin);
        Assert.Equal(clear ? null : "Original bean notes", stored.Notes);
        Assert.Equal("https://example.com/coffee", stored.RoasterUrl);
    }

    [Fact]
    public async Task EmptyBeanUrl_StillClearsWithoutChangingOtherText()
    {
        await using (var context = CreateContext())
        {
            var service = new BeanService(
                new BeanRepository(context),
                new RatingService(context),
                NullLogger<BeanService>.Instance);
            await service.UpdateBeanAsync(1, new UpdateBeanDto { RoasterUrl = string.Empty });
        }

        await using var verification = CreateContext();
        var stored = await verification.Beans.SingleAsync(bean => bean.Id == 1);
        Assert.Null(stored.RoasterUrl);
        Assert.Equal("Original roaster", stored.Roaster);
        Assert.Equal("Original origin", stored.Origin);
        Assert.Equal("Original bean notes", stored.Notes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EquipmentNotes_DistinguishClearFromOmission(bool clear)
    {
        await using (var context = CreateContext())
        {
            var service = new EquipmentService(new EquipmentRepository(context));
            await service.UpdateEquipmentAsync(201, clear
                ? new UpdateEquipmentDto { Notes = FieldUpdate<string?>.Set(null) }
                : new UpdateEquipmentDto { Name = "Renamed machine" });
        }

        await using var verification = CreateContext();
        var stored = await verification.Equipment.SingleAsync(equipment => equipment.Id == 201);
        Assert.Equal(clear ? null : "Original equipment notes", stored.Notes);
        Assert.Equal(EquipmentType.Machine, stored.Type);
    }

    private BaristaNotesContext CreateContext()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Pooling = false
        }.ToString();
        var options = new DbContextOptionsBuilder<BaristaNotesContext>()
            .UseModel(BaristaNotesContextModel.Instance)
            .UseSqlite(connectionString)
            .Options;
        return new BaristaNotesContext(options);
    }

    private async Task UpdateShotAsync(UpdateShotDto update)
    {
        await using var context = CreateContext();
        var service = new ShotService(
            new ShotRecordRepository(context),
            Mock.Of<IPreferencesService>(),
            new BagRepository(context),
            new BeanRepository(context),
            new UserProfileRepository(context));
        await service.UpdateShotAsync(301, update);
    }

    private static Equipment CreateEquipment(int id, EquipmentType type) => new()
    {
        Id = id,
        Name = $"Synthetic {type}",
        Type = type,
        Notes = "Original equipment notes",
        IsActive = true,
        CreatedAt = DateTime.Now,
        LastModifiedAt = DateTime.Now,
        SyncId = Guid.NewGuid()
    };
}
