using System.Text.Json;
using System.Text.Json.Serialization;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Services.Agent;

// The shape of Resources/Raw/demo/demo-data.json. Dates in the file are
// relative ("roastedDaysAgo", "daysAgo") so the demo always looks fresh.

public sealed class DemoData
{
    public List<DemoProfile> Profiles { get; set; } = [];
    public List<DemoBean> Beans { get; set; } = [];
    public List<DemoBag> Bags { get; set; } = [];
    public List<DemoShot> Shots { get; set; } = [];
}

public sealed class DemoProfile
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class DemoBean
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Roaster { get; set; }
    public string? Origin { get; set; }
    public string? Notes { get; set; }
}

public sealed class DemoBag
{
    public string Key { get; set; } = "";
    public string Bean { get; set; } = "";
    public int RoastedDaysAgo { get; set; }
    public bool Finished { get; set; }
}

public sealed class DemoShot
{
    public string Bag { get; set; } = "";
    public int DaysAgo { get; set; }
    public string MadeBy { get; set; } = "";
    public string? MadeFor { get; set; }
    public string DrinkType { get; set; } = "Espresso";
    public decimal DoseIn { get; set; }
    public decimal Output { get; set; }
    public decimal Time { get; set; }
    public int? Rating { get; set; }
    public string? Notes { get; set; }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(DemoData))]
internal sealed partial class DemoDataJsonContext : JsonSerializerContext;

/// <summary>Entities ready to add to the database. Navigation properties link them.</summary>
public sealed record DemoSeedEntities(
    IReadOnlyList<UserProfile> Profiles,
    IReadOnlyList<Bean> Beans,
    IReadOnlyList<Bag> Bags,
    IReadOnlyList<ShotRecord> Shots);

/// <summary>
/// Parses the demo fixture and builds entities for a given "now". Pure, so
/// the date and person mapping is unit tested without a device.
/// </summary>
public static class DemoSeedPlan
{
    public static DemoData Parse(Stream json) =>
        JsonSerializer.Deserialize(json, DemoDataJsonContext.Default.DemoData)
        ?? throw new InvalidDataException("The demo data file is empty.");

    public static DemoSeedEntities Build(DemoData data, DateTime now)
    {
        var today = now.Date;

        var profiles = data.Profiles.ToDictionary(
            profile => profile.Key,
            profile => new UserProfile
            {
                Name = profile.Name,
                CreatedAt = now,
                SyncId = Guid.NewGuid(),
                LastModifiedAt = now,
            });

        var beans = data.Beans.ToDictionary(
            bean => bean.Key,
            bean => new Bean
            {
                Name = bean.Name,
                Roaster = bean.Roaster,
                Origin = bean.Origin,
                Notes = bean.Notes,
                IsActive = true,
                CreatedAt = now,
                SyncId = Guid.NewGuid(),
                LastModifiedAt = now,
            });

        var bags = data.Bags.ToDictionary(
            bag => bag.Key,
            bag =>
            {
                var bean = Lookup(beans, bag.Bean, "bean");
                var entity = new Bag
                {
                    Bean = bean,
                    RoastDate = today.AddDays(-bag.RoastedDaysAgo),
                    IsComplete = bag.Finished,
                    IsActive = true,
                    CreatedAt = now,
                    SyncId = Guid.NewGuid(),
                    LastModifiedAt = now,
                };
                bean.Bags.Add(entity);
                return entity;
            });

        // Oldest first, so shot ids grow with time.
        var shots = data.Shots
            .Select((shot, index) => (shot, index))
            .OrderByDescending(item => item.shot.DaysAgo)
            .ThenBy(item => item.index)
            .Select(item => BuildShot(item.shot, item.index, now, profiles, bags))
            .ToList();

        return new DemoSeedEntities(
            profiles.Values.ToList(),
            beans.Values.ToList(),
            bags.Values.ToList(),
            shots);
    }

    /// <summary>A morning time on the given day, varied per shot, never in the future.</summary>
    public static DateTime MorningTime(DateTime now, int daysAgo, int index)
    {
        var minutesAfterSeven = 10 + (index * 17 % 140);
        var timestamp = now.Date.AddDays(-daysAgo).AddHours(7).AddMinutes(minutesAfterSeven);
        return timestamp > now ? now.AddMinutes(-5) : timestamp;
    }

    public static BrewMethod BrewMethodFor(string drinkType) =>
        string.Equals(drinkType, "Pour Over", StringComparison.OrdinalIgnoreCase)
            ? BrewMethod.PourOver
            : BrewMethod.Espresso;

    static ShotRecord BuildShot(
        DemoShot shot,
        int index,
        DateTime now,
        Dictionary<string, UserProfile> profiles,
        Dictionary<string, Bag> bags)
    {
        var bag = Lookup(bags, shot.Bag, "bag");
        var madeBy = Lookup(profiles, shot.MadeBy, "profile");
        var madeFor = string.IsNullOrEmpty(shot.MadeFor) ? null : Lookup(profiles, shot.MadeFor, "profile");

        var entity = new ShotRecord
        {
            Bag = bag,
            Timestamp = MorningTime(now, shot.DaysAgo, index),
            BrewMethod = BrewMethodFor(shot.DrinkType),
            DrinkType = shot.DrinkType,
            DoseIn = shot.DoseIn,
            ExpectedOutput = shot.Output,
            ActualOutput = shot.Output,
            ExpectedTime = shot.Time,
            ActualTime = shot.Time,
            Rating = shot.Rating,
            TastingNotes = shot.Notes,
            MadeBy = madeBy,
            MadeFor = madeFor,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = now,
        };
        bag.ShotRecords.Add(entity);
        return entity;
    }

    static T Lookup<T>(Dictionary<string, T> items, string key, string kind) =>
        items.TryGetValue(key, out var item)
            ? item
            : throw new InvalidDataException($"The demo data refers to an unknown {kind} '{key}'.");
}
