using System.Globalization;
using System.Text.RegularExpressions;
using BaristaNotes.Agent.Contracts;
using BaristaNotes.Api.Data;

namespace BaristaNotes.Api.Agent;

// The rules in this file are code, not model output. The model proposes;
// these decide what is allowed. Each one has tests in AgentRulesTests.

// ---------------------------------------------------------------------------
// Autonomy policy
// ---------------------------------------------------------------------------

public sealed record PolicyInput(
    Autonomy Autonomy,
    bool IsSameProductAsFinishedBag,
    double? FinishedBagAverageRating,
    bool IsKnownRoaster,
    decimal Total,
    decimal BudgetLeft,
    bool BagAlreadyOrdered,
    string Currency);

public abstract record PolicyDecision
{
    /// <summary>Tell you about the pick. Do nothing in the shop.</summary>
    public sealed record Notify(string Reason) : PolicyDecision;

    /// <summary>Prepare a cart. You place the order. <c>Reason</c> explains why it did not order.</summary>
    public sealed record PrepareCart(string? Reason) : PolicyDecision;

    /// <summary>Place the order with an idempotency key.</summary>
    public sealed record PlaceOrder : PolicyDecision;
}

public static class ReorderPolicy
{
    /// <summary>A favorite is rated 3 or higher on the 0-4 scale.</summary>
    public const double FavoriteRating = 3.0;

    public static PolicyDecision Decide(PolicyInput input)
    {
        if (input.BagAlreadyOrdered)
        {
            return new PolicyDecision.Notify("This bag already has an order.");
        }

        switch (input.Autonomy)
        {
            case Autonomy.Watch:
                return new PolicyDecision.Notify("Watch mode: I only tell you.");

            case Autonomy.PrepareCart:
                return new PolicyDecision.PrepareCart(null);
        }

        if (!input.IsSameProductAsFinishedBag)
        {
            return new PolicyDecision.PrepareCart(input.IsKnownRoaster
                ? "New coffee, so I prepared a cart instead."
                : "New roaster, so I prepared a cart instead.");
        }

        if (input.FinishedBagAverageRating is not double rating || rating < FavoriteRating)
        {
            var rated = input.FinishedBagAverageRating is double r
                ? $"You rated it {r.ToString("0.0", CultureInfo.InvariantCulture)} of 4. "
                : "You did not rate it. ";
            return new PolicyDecision.PrepareCart(rated + "I only reorder favorites rated 3 or higher.");
        }

        if (input.Total > input.BudgetLeft)
        {
            return new PolicyDecision.PrepareCart(
                $"Over budget: {Money.Format(input.Total, input.Currency)} with shipping, " +
                $"{Money.Format(input.BudgetLeft, input.Currency)} left this month. I prepared a cart instead.");
        }

        return new PolicyDecision.PlaceOrder();
    }
}

// ---------------------------------------------------------------------------
// Budget
// ---------------------------------------------------------------------------

public static class Budget
{
    /// <summary>All orders this calendar month (UTC), duplicates included: they cost real money.</summary>
    public static decimal SpentThisMonth(IEnumerable<OrderRow> orders, DateTime nowUtc) =>
        orders
            .Where(o => o.CreatedAt.Year == nowUtc.Year && o.CreatedAt.Month == nowUtc.Month)
            .Sum(o => o.Total);

    public static decimal Left(decimal monthlyBudget, decimal spent) => Math.Max(0m, monthlyBudget - spent);
}

public static class Money
{
    public static string Format(decimal amount, string currency) => currency switch
    {
        "EUR" => "€" + amount.ToString("0.00", CultureInfo.InvariantCulture),
        _ => currency + " " + amount.ToString("0.00", CultureInfo.InvariantCulture),
    };
}

// ---------------------------------------------------------------------------
// Catalog filters
// ---------------------------------------------------------------------------

/// <summary>One purchasable variant from a roaster's catalog. Tags look like <c>process:natural</c>.</summary>
public sealed record CatalogItem(
    string Roaster,
    string RoasterName,
    decimal Shipping,
    string Currency,
    long ProductId,
    long VariantId,
    string Title,
    decimal Price,
    bool Available,
    DateOnly? RoastedOn,
    IReadOnlyList<string> Tags)
{
    public decimal Total => Price + Shipping;

    public IEnumerable<string> TagValues(string name) =>
        Tags.Where(t => t.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase))
            .Select(t => t[(name.Length + 1)..].Trim().ToLowerInvariant());

    public string? Tag(string name) => TagValues(name).FirstOrDefault();

    /// <summary>A short description for the model and the proposal page.</summary>
    public string Describe(DateOnly today)
    {
        var parts = new List<string>();
        var origin = string.Join(", ", new[] { Tag("origin"), Tag("region") }.OfType<string>().Where(s => s.Length > 0).Select(Title_));
        if (origin.Length > 0) parts.Add(origin);
        if (Tag("process") is { } process) parts.Add(Title_(process));
        if (Tag("roast") is { } roast) parts.Add(Title_(roast) + " roast");
        if (Tag("decaf") == "yes") parts.Add("Decaf");
        if (RoastedOn is DateOnly roasted) parts.Add($"Roasted {today.DayNumber - roasted.DayNumber} days ago");
        var notes = TagValues("notes").ToList();
        if (notes.Count > 0) parts.Add(Title_(string.Join(", ", notes)));
        return string.Join(". ", parts) + ".";

        static string Title_(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
    }
}

/// <summary>Something to skip, from a disliked preference in memory.</summary>
public sealed record Avoid(string Attribute, string Value, string Reason);

public sealed record FilterContext(
    DateOnly Today,
    int MaxRoastAgeDays,
    decimal BudgetLeft,
    IReadOnlyCollection<Avoid> Avoid,
    IReadOnlySet<string> StillHave)
{
    /// <summary>Finished coffees rated below 3, by <see cref="ProductMatch.Key"/>, with the average rating.</summary>
    public IReadOnlyDictionary<string, double> FinishedLowRated { get; init; } = new Dictionary<string, double>();
}

public static class CandidateFilter
{
    /// <summary>Returns why the agent skips the item, or null to keep it.</summary>
    public static string? SkipReason(CatalogItem item, FilterContext context)
    {
        if (!item.Available)
        {
            return "Sold out.";
        }

        if (item.RoastedOn is not DateOnly roasted)
        {
            return "No roast date.";
        }

        var age = context.Today.DayNumber - roasted.DayNumber;
        if (age > context.MaxRoastAgeDays)
        {
            return $"Roasted {age} days ago. Your limit is {context.MaxRoastAgeDays} days.";
        }

        if (item.Total > context.BudgetLeft)
        {
            return $"{Money.Format(item.Price, item.Currency)} + {Money.Format(item.Shipping, item.Currency)} shipping " +
                   $"is over the {Money.Format(context.BudgetLeft, item.Currency)} left this month.";
        }

        foreach (var avoid in context.Avoid)
        {
            if (item.TagValues(avoid.Attribute).Contains(avoid.Value))
            {
                return avoid.Reason;
            }
        }

        var key = ProductMatch.Key(item.RoasterName, item.Title);
        if (context.StillHave.Contains(key))
        {
            return "You still have a bag of this.";
        }

        // The same coffee again is only a good idea if you liked it.
        if (context.FinishedLowRated.TryGetValue(key, out var rating))
        {
            return $"You rated it {rating.ToString("0.0", CultureInfo.InvariantCulture)} of 4. Looking for something you may like more.";
        }

        return null;
    }
}

// ---------------------------------------------------------------------------
// Stale picks: what you approved must still be what you get
// ---------------------------------------------------------------------------

/// <summary>The variant's price and availability in the shop right now.</summary>
public sealed record ShopVariantNow(decimal Price, bool Available);

public static class StaleCheck
{
    /// <summary>Returns why the pick is out of date, or null when it still holds.</summary>
    public static string? Reason(decimal pickedPrice, string currency, ShopVariantNow? now)
    {
        if (now is null)
        {
            return "It is no longer in the catalog.";
        }

        if (!now.Available)
        {
            return "It sold out since I picked it.";
        }

        if (now.Price != pickedPrice)
        {
            return $"The price changed from {Money.Format(pickedPrice, currency)} to {Money.Format(now.Price, currency)} since I picked it.";
        }

        return null;
    }
}

// ---------------------------------------------------------------------------
// Matching the app's beans to shop products
// ---------------------------------------------------------------------------

public static partial class ProductMatch
{
    public static string Normalize(string? value) =>
        Spaces().Replace((value ?? "").Trim().ToLowerInvariant(), " ");

    public static string Key(string? roasterName, string? title) => Normalize(roasterName) + "|" + Normalize(title);

    public static bool Same(string? beanRoaster, string? beanName, string roasterName, string title) =>
        !string.IsNullOrWhiteSpace(beanRoaster)
        && !string.IsNullOrWhiteSpace(beanName)
        && Key(beanRoaster, beanName) == Key(roasterName, title);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}

// ---------------------------------------------------------------------------
// Evidence
// ---------------------------------------------------------------------------

public sealed record EvidenceCheck(IReadOnlyList<CitedShot> Valid, int Dropped);

public static class EvidenceValidator
{
    /// <summary>
    /// Keeps citations of drinks that exist and were made for you. Drops invented
    /// IDs and guest drinks. Repeated IDs count once.
    /// </summary>
    public static EvidenceCheck Validate(IEnumerable<CitedShot> cited, IReadOnlyDictionary<int, SnapshotShot> shots)
    {
        var valid = new List<CitedShot>();
        var seen = new HashSet<int>();
        var dropped = 0;

        foreach (var citation in cited)
        {
            if (!seen.Add(citation.ShotId))
            {
                continue;
            }

            if (shots.TryGetValue(citation.ShotId, out var shot) && shot.ForMe)
            {
                valid.Add(citation);
            }
            else
            {
                dropped++;
            }
        }

        return new EvidenceCheck(valid, dropped);
    }

    /// <summary>
    /// As <see cref="Validate"/>, and the rating must agree with the claim:
    /// a like cites drinks rated 3 or 4, a dislike cites drinks rated 0 to 2.
    /// </summary>
    public static EvidenceCheck ValidateClaim(MemoryKind kind, IEnumerable<CitedShot> cited, IReadOnlyDictionary<int, SnapshotShot> shots)
    {
        var basic = Validate(cited, shots);
        var agreeing = basic.Valid.Where(c => shots[c.ShotId].Rating is int rating && kind switch
        {
            MemoryKind.Likes => rating >= 3,
            MemoryKind.Dislikes => rating <= 2,
            _ => true,
        }).ToList();

        return new EvidenceCheck(agreeing, basic.Dropped + (basic.Valid.Count - agreeing.Count));
    }
}

// ---------------------------------------------------------------------------
// Memory
// ---------------------------------------------------------------------------

/// <summary>A preference the model proposes, before validation.</summary>
public sealed record LearnedPreference(
    MemoryKind Kind,
    string Attribute,
    string Value,
    string Statement,
    IReadOnlyList<CitedShot> Evidence);

public sealed record MemoryMerge(IReadOnlyList<MemoryRow> ToAdd, IReadOnlyList<MemoryRow> ToDelete, int Blocked);

public static partial class MemoryRules
{
    /// <summary>Attributes that match shop tags, so a dislike can become a filter.</summary>
    public static readonly IReadOnlySet<string> TagAttributes = new HashSet<string> { "process", "roast", "origin" };

    public static string Key(MemoryKind kind, string attribute, string value) =>
        $"{kind.ToString().ToLowerInvariant()}:{Slug(attribute)}:{Slug(value)}";

    /// <summary>
    /// Replaces the previously learned items with newly learned ones. Keeps items
    /// you added. Never adds a key you removed. Drops items with no evidence.
    /// </summary>
    public static MemoryMerge Merge(IReadOnlyList<MemoryRow> existing, IEnumerable<LearnedPreference> learned, DateTime nowUtc)
    {
        var removedKeys = existing.Where(r => r.RemovedAt is not null).Select(r => r.Key).ToHashSet();
        var keptKeys = existing.Where(r => r.RemovedAt is null && r.Source != "learned").Select(r => r.Key).ToHashSet();
        var toDelete = existing.Where(r => r.RemovedAt is null && r.Source == "learned").ToList();

        var toAdd = new List<MemoryRow>();
        var blocked = 0;
        foreach (var item in learned)
        {
            if (item.Evidence.Count == 0)
            {
                continue;
            }

            var key = Key(item.Kind, item.Attribute, item.Value);
            if (removedKeys.Contains(key))
            {
                blocked++;
                continue;
            }

            if (keptKeys.Contains(key) || toAdd.Any(r => r.Key == key))
            {
                continue;
            }

            toAdd.Add(new MemoryRow
            {
                Id = Guid.NewGuid(),
                Kind = item.Kind,
                Key = key,
                Statement = item.Statement.Trim(),
                EvidenceJson = System.Text.Json.JsonSerializer.Serialize(item.Evidence),
                LearnedAt = nowUtc,
                Source = "learned",
            });
        }

        return new MemoryMerge(toAdd, toDelete, blocked);
    }

    /// <summary>Active dislikes of process, roast, or origin become catalog filters.</summary>
    public static IReadOnlyList<Avoid> AvoidRules(IEnumerable<MemoryRow> rows)
    {
        var rules = new List<Avoid>();
        foreach (var row in rows.Where(r => r.RemovedAt is null && r.Kind == MemoryKind.Dislikes))
        {
            var parts = row.Key.Split(':', 3);
            if (parts.Length == 3 && TagAttributes.Contains(parts[1]))
            {
                rules.Add(new Avoid(parts[1], parts[2], row.Statement));
            }
        }

        return rules;
    }

    static string Slug(string value) => NonWord().Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonWord();
}
