using BaristaNotes.Agent.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace BaristaNotes.Services.Agent;

/// <summary>A bag that is open on this device: active, not finished, not deleted.</summary>
public sealed record OpenBag(int Id, string BeanName, string? Roaster, DateTime RoastDate);

/// <summary>
/// Reads the local database for the agent. Each call uses its own scope and
/// a no-tracking query, so it sees what is saved and never changes the
/// context the pages share.
/// </summary>
public sealed class AgentSnapshotBuilder(
    IServiceScopeFactory scopeFactory,
    DatabaseInitializationService databaseInitialization)
{
    /// <summary>Beans, bags, and drinks that are not deleted, mapped by <see cref="AgentSnapshotMapper"/>.</summary>
    public async Task<Snapshot> BuildAsync(CancellationToken ct = default)
    {
        await databaseInitialization.InitializeAsync();

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaristaNotesContext>();

        var beans = await db.Beans.AsNoTracking().Where(bean => !bean.IsDeleted).ToListAsync(ct);
        var bags = await db.Bags.AsNoTracking().Where(bag => !bag.IsDeleted).ToListAsync(ct);
        var shots = await db.ShotRecords.AsNoTracking().Where(shot => !shot.IsDeleted).ToListAsync(ct);

        return AgentSnapshotMapper.Build(beans, bags, shots, DateTimeOffset.Now);
    }

    /// <summary>Bags the check-in asks about, newest roast first.</summary>
    public async Task<IReadOnlyList<OpenBag>> GetOpenBagsAsync(CancellationToken ct = default)
    {
        await databaseInitialization.InitializeAsync();

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaristaNotesContext>();

        return await db.Bags
            .AsNoTracking()
            .Where(bag => bag.IsActive && !bag.IsComplete && !bag.IsDeleted && !bag.Bean.IsDeleted)
            .OrderByDescending(bag => bag.RoastDate)
            .Select(bag => new OpenBag(bag.Id, bag.Bean.Name, bag.Bean.Roaster, bag.RoastDate))
            .ToListAsync(ct);
    }
}
