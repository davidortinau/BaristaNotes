using BaristaNotes.Agent.Contracts;
using BaristaNotes.Core.Models;

namespace BaristaNotes.Services.Agent;

/// <summary>
/// Turns local beans, bags, and shots into the <see cref="Snapshot"/> the
/// agent receives. Pure, so the sharing rules are unit tested.
/// Shares only ids, bean name/roaster/origin/notes, roast dates, finished
/// flags, drink type, rating, tasting notes, and whether a drink was for the
/// user. No photos, people's names, equipment, or recipes.
/// </summary>
public static class AgentSnapshotMapper
{
    /// <summary>
    /// A drink is "for me" when nobody else is named as the recipient.
    /// A drink made for someone else is a guest drink, and the agent does
    /// not learn from it.
    /// </summary>
    public static bool IsForMe(int? madeById, int? madeForId) =>
        madeForId is null || madeForId == madeById;

    public static Snapshot Build(
        IEnumerable<Bean> beans,
        IEnumerable<Bag> bags,
        IEnumerable<ShotRecord> shots,
        DateTimeOffset createdAt)
    {
        var sharedBeans = beans
            .Where(bean => !bean.IsDeleted)
            .OrderBy(bean => bean.Id)
            .Select(bean => new SnapshotBean(
                bean.Id,
                bean.Name,
                Clean(bean.Roaster),
                Clean(bean.Origin),
                Clean(bean.Notes)))
            .ToList();
        var beanIds = sharedBeans.Select(bean => bean.Id).ToHashSet();

        var sharedBags = bags
            .Where(bag => !bag.IsDeleted && beanIds.Contains(bag.BeanId))
            .OrderBy(bag => bag.Id)
            .Select(bag => new SnapshotBag(bag.Id, bag.BeanId, bag.RoastDate, bag.IsComplete))
            .ToList();
        var bagIds = sharedBags.Select(bag => bag.Id).ToHashSet();

        var sharedShots = shots
            .Where(shot => !shot.IsDeleted && bagIds.Contains(shot.BagId))
            .OrderBy(shot => shot.Timestamp)
            .ThenBy(shot => shot.Id)
            .Select(shot => new SnapshotShot(
                shot.Id,
                shot.BagId,
                shot.Timestamp,
                Clean(shot.DrinkType),
                shot.Rating,
                Clean(shot.TastingNotes),
                IsForMe(shot.MadeById, shot.MadeForId)))
            .ToList();

        return new Snapshot(createdAt, sharedBeans, sharedBags, sharedShots);
    }

    static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
