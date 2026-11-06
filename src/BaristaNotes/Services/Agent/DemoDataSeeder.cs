using Microsoft.Extensions.DependencyInjection;

namespace BaristaNotes.Services.Agent;

/// <summary>
/// Fills the separate demo database (<c>BARISTANOTES_DEMO=1</c>) from
/// <c>Resources/Raw/demo/demo-data.json</c>. Every method refuses to run
/// outside demo mode, so it can never touch real data.
/// </summary>
public sealed class DemoDataSeeder(
    IServiceScopeFactory scopeFactory,
    IServiceProvider rootServices,
    IDataChangeNotifier dataChangeNotifier,
    ILogger<DemoDataSeeder> logger)
{
    const string DemoDataAsset = "demo/demo-data.json";
    const string DemoDatabaseFile = "barista_notes_demo.db";

    /// <summary>Seeds when the demo database has no beans. Logs and returns on failure.</summary>
    public async Task SeedIfEmptyAsync()
    {
        if (!AgentApiAddress.IsDemo)
        {
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BaristaNotesContext>();
            EnsureDemoDatabase(db);

            if (await db.Beans.AnyAsync())
            {
                return;
            }

            var count = await SeedAsync(db);
            logger.LogInformation("Seeded the demo database with {ShotCount} drinks", count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not seed the demo database");
        }
    }

    /// <summary>
    /// Deletes all profiles, beans, bags, and drinks in the demo database and
    /// seeds them again. Equipment and settings stay.
    /// </summary>
    public async Task ResetAsync()
    {
        if (!AgentApiAddress.IsDemo)
        {
            throw new InvalidOperationException("Demo data can only be reset in demo mode.");
        }

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaristaNotesContext>();
            EnsureDemoDatabase(db);

            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.ShotEquipments.ExecuteDeleteAsync();
            await db.ShotRecords.ExecuteDeleteAsync();
            await db.Recipes.ExecuteDeleteAsync();
            await db.Bags.ExecuteDeleteAsync();
            await db.Beans.ExecuteDeleteAsync();
            await db.UserProfiles.ExecuteDeleteAsync();

            // The tables use AUTOINCREMENT. Restart the counters so a reset gives
            // the same ids every time; rehearsal recordings depend on them.
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM sqlite_sequence WHERE name IN ('ShotEquipments', 'ShotRecords', 'Recipes', 'Bags', 'Beans', 'UserProfiles');");
            await SeedAsync(db);
            await transaction.CommitAsync();
        }

        // Pages share one context resolved from the root provider. Ids are
        // reused after the reset, so forget what that context tracked.
        rootServices.GetService<BaristaNotesContext>()?.ChangeTracker.Clear();

        dataChangeNotifier.NotifyDataChanged(DataChangeType.BeanUpdated);
        dataChangeNotifier.NotifyDataChanged(DataChangeType.BagUpdated);
        dataChangeNotifier.NotifyDataChanged(DataChangeType.ShotUpdated);
        dataChangeNotifier.NotifyDataChanged(DataChangeType.ProfileUpdated);
        logger.LogInformation("Reset the demo database");
    }

    static async Task<int> SeedAsync(BaristaNotesContext db)
    {
        DemoData data;
        await using (var stream = await FileSystem.OpenAppPackageFileAsync(DemoDataAsset))
        {
            data = DemoSeedPlan.Parse(stream);
        }

        var entities = DemoSeedPlan.Build(data, DateTime.Now);
        db.UserProfiles.AddRange(entities.Profiles);
        db.Beans.AddRange(entities.Beans);
        db.Bags.AddRange(entities.Bags);
        db.ShotRecords.AddRange(entities.Shots);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return entities.Shots.Count;
    }

    /// <summary>Second guard: the open connection must be the demo database file.</summary>
    static void EnsureDemoDatabase(BaristaNotesContext db)
    {
        var dataSource = db.Database.GetDbConnection().DataSource;
        if (!string.Equals(Path.GetFileName(dataSource), DemoDatabaseFile, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to change '{Path.GetFileName(dataSource)}'. Only the demo database can be seeded.");
        }
    }
}
