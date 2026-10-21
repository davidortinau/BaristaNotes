using BaristaNotes.Agent.Contracts;
using BaristaNotes.Api.Agent;
using BaristaNotes.Api.Data;

namespace BaristaNotes.Api.Tests;

// Critical-path tests for the code that limits what the agent may do.
// The model proposes. These rules decide.

public class ReorderPolicyTests
{
    static PolicyInput Favorite(Autonomy autonomy = Autonomy.ReorderFavorites) => new(
        Autonomy: autonomy,
        IsSameProductAsFinishedBag: true,
        FinishedBagAverageRating: 3.4,
        IsKnownRoaster: true,
        Total: 21.00m,
        BudgetLeft: 57.50m,
        BagAlreadyOrdered: false,
        Currency: "EUR");

    [Fact]
    public void Watch_only_notifies()
    {
        Assert.IsType<PolicyDecision.Notify>(ReorderPolicy.Decide(Favorite(Autonomy.Watch)));
    }

    [Fact]
    public void PrepareCart_never_orders()
    {
        Assert.IsType<PolicyDecision.PrepareCart>(ReorderPolicy.Decide(Favorite(Autonomy.PrepareCart)));
    }

    [Fact]
    public void ReorderFavorites_orders_a_finished_favorite_within_budget()
    {
        Assert.IsType<PolicyDecision.PlaceOrder>(ReorderPolicy.Decide(Favorite()));
    }

    [Fact]
    public void ReorderFavorites_orders_when_total_equals_budget_left()
    {
        var input = Favorite() with { Total = 57.50m };
        Assert.IsType<PolicyDecision.PlaceOrder>(ReorderPolicy.Decide(input));
    }

    [Fact]
    public void ReorderFavorites_prepares_a_cart_when_over_budget()
    {
        var input = Favorite() with { Total = 57.51m };
        var cart = Assert.IsType<PolicyDecision.PrepareCart>(ReorderPolicy.Decide(input));
        Assert.Contains("budget", cart.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReorderFavorites_prepares_a_cart_for_a_new_roaster()
    {
        var input = Favorite() with { IsSameProductAsFinishedBag = false, IsKnownRoaster = false };
        var cart = Assert.IsType<PolicyDecision.PrepareCart>(ReorderPolicy.Decide(input));
        Assert.Equal("New roaster, so I prepared a cart instead.", cart.Reason);
    }

    [Fact]
    public void ReorderFavorites_prepares_a_cart_for_a_new_coffee_from_a_known_roaster()
    {
        var input = Favorite() with { IsSameProductAsFinishedBag = false };
        var cart = Assert.IsType<PolicyDecision.PrepareCart>(ReorderPolicy.Decide(input));
        Assert.Equal("New coffee, so I prepared a cart instead.", cart.Reason);
    }

    [Theory]
    [InlineData(2.9)]
    [InlineData(null)]
    public void ReorderFavorites_only_orders_coffees_rated_3_or_higher(double? rating)
    {
        var input = Favorite() with { FinishedBagAverageRating = rating };
        Assert.IsType<PolicyDecision.PrepareCart>(ReorderPolicy.Decide(input));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(-1)]
    public void An_unknown_autonomy_level_never_orders(int level)
    {
        // Fail closed: only an explicit Reorder favorites setting may order.
        Assert.IsType<PolicyDecision.Notify>(ReorderPolicy.Decide(Favorite((Autonomy)level)));
    }

    [Theory]
    [InlineData(Autonomy.Watch)]
    [InlineData(Autonomy.PrepareCart)]
    [InlineData(Autonomy.ReorderFavorites)]
    public void One_order_per_finished_bag(Autonomy autonomy)
    {
        var input = Favorite(autonomy) with { BagAlreadyOrdered = true };
        Assert.IsType<PolicyDecision.Notify>(ReorderPolicy.Decide(input));
    }
}

public class BudgetTests
{
    static OrderRow Order(decimal total, DateTime at) => new() { OrderId = Guid.NewGuid().ToString(), Total = total, CreatedAt = at };

    [Fact]
    public void Counts_only_this_month_and_counts_duplicates()
    {
        var now = new DateTime(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc);
        var orders = new[]
        {
            Order(22.50m, new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc)),
            Order(21.00m, new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)),
            Order(21.00m, new DateTime(2026, 10, 20, 0, 0, 5, DateTimeKind.Utc)),
            Order(99.00m, new DateTime(2026, 9, 30, 23, 0, 0, DateTimeKind.Utc)),
        };

        Assert.Equal(64.50m, Budget.SpentThisMonth(orders, now));
    }

    [Fact]
    public void Left_never_goes_below_zero()
    {
        Assert.Equal(0m, Budget.Left(80m, 95m));
        Assert.Equal(15.50m, Budget.Left(80m, 64.50m));
    }
}

public class CandidateFilterTests
{
    static readonly DateOnly Today = new(2026, 10, 21);

    static CatalogItem Item(
        bool available = true,
        int? roastedDaysAgo = 5,
        decimal price = 17.50m,
        params string[] tags) => new(
            Roaster: "copperhaze",
            RoasterName: "Copperhaze Coffee",
            Shipping: 5.00m,
            Currency: "EUR",
            ProductId: 501,
            VariantId: 5011,
            Title: "Guji Natural",
            Price: price,
            Available: available,
            RoastedOn: roastedDaysAgo is int d ? Today.AddDays(-d) : null,
            Tags: tags.Length == 0 ? ["process:natural", "roast:light"] : tags);

    static FilterContext Context(
        decimal budgetLeft = 80m, Avoid[]? avoid = null, string[]? stillHave = null, Dictionary<string, double>? finishedLowRated = null) => new(
        Today,
        MaxRoastAgeDays: 21,
        BudgetLeft: budgetLeft,
        Avoid: avoid ?? [],
        StillHave: (stillHave ?? []).ToHashSet())
    {
        FinishedLowRated = finishedLowRated ?? [],
    };

    [Fact]
    public void Keeps_a_fresh_coffee_in_stock_within_budget()
    {
        Assert.Null(CandidateFilter.SkipReason(Item(), Context()));
    }

    [Fact]
    public void Skips_sold_out()
    {
        Assert.Equal("Sold out.", CandidateFilter.SkipReason(Item(available: false), Context()));
    }

    [Fact]
    public void Skips_old_roasts_but_keeps_the_limit_day()
    {
        Assert.Equal("Roasted 30 days ago. Your limit is 21 days.", CandidateFilter.SkipReason(Item(roastedDaysAgo: 30), Context()));
        Assert.Null(CandidateFilter.SkipReason(Item(roastedDaysAgo: 21), Context()));
    }

    [Fact]
    public void Skips_when_the_roast_date_is_unknown()
    {
        Assert.Equal("No roast date.", CandidateFilter.SkipReason(Item(roastedDaysAgo: null), Context()));
    }

    [Fact]
    public void Skips_when_price_plus_shipping_is_over_budget()
    {
        var reason = CandidateFilter.SkipReason(Item(price: 89m), Context(budgetLeft: 80m));
        Assert.Equal("€89.00 + €5.00 shipping is over the €80.00 left this month.", reason);
    }

    [Fact]
    public void Skips_what_you_dislike()
    {
        var avoid = new Avoid("roast", "dark", "You don't like dark roasts.");
        var reason = CandidateFilter.SkipReason(Item(tags: ["process:natural", "roast:dark"]), Context(avoid: [avoid]));
        Assert.Equal("You don't like dark roasts.", reason);
    }

    [Fact]
    public void Skips_a_finished_coffee_you_rated_below_3()
    {
        var key = ProductMatch.Key("Copperhaze Coffee", "Guji Natural");
        var reason = CandidateFilter.SkipReason(Item(), Context(finishedLowRated: new() { [key] = 2.33 }));
        Assert.Equal("You rated it 2.3 of 4. Looking for something you may like more.", reason);
    }

    [Fact]
    public void Skips_a_coffee_you_still_have()
    {
        var key = ProductMatch.Key("Copperhaze Coffee", "Guji Natural");
        Assert.Equal("You still have a bag of this.", CandidateFilter.SkipReason(Item(), Context(stillHave: [key])));
    }
}

public class EvidenceValidatorTests
{
    static readonly Dictionary<int, SnapshotShot> Shots = new()
    {
        [1] = new(1, 10, DateTime.UtcNow, "Espresso", 4, "Blueberry jam.", ForMe: true),
        [2] = new(2, 10, DateTime.UtcNow, "Latte", 1, "Sam: too fruity.", ForMe: false),
        [3] = new(3, 11, DateTime.UtcNow, "Espresso", 4, "Strawberry.", ForMe: true),
    };

    [Fact]
    public void Keeps_real_citations()
    {
        var result = EvidenceValidator.Validate([new(1, "a"), new(3, "b")], Shots);
        Assert.Equal([1, 3], result.Valid.Select(c => c.ShotId));
        Assert.Equal(0, result.Dropped);
    }

    [Fact]
    public void Drops_invented_ids()
    {
        var result = EvidenceValidator.Validate([new(1, "a"), new(99, "made up")], Shots);
        Assert.Equal([1], result.Valid.Select(c => c.ShotId));
        Assert.Equal(1, result.Dropped);
    }

    [Fact]
    public void Drops_guest_drinks()
    {
        var result = EvidenceValidator.Validate([new(2, "guest")], Shots);
        Assert.Empty(result.Valid);
        Assert.Equal(1, result.Dropped);
    }

    [Fact]
    public void A_like_cites_only_drinks_rated_3_or_4()
    {
        var shots = new Dictionary<int, SnapshotShot>(Shots)
        {
            [4] = new(4, 12, DateTime.UtcNow, "Espresso", 2, "Sour.", ForMe: true),
            [5] = new(5, 12, DateTime.UtcNow, "Espresso", null, "No rating.", ForMe: true),
        };

        var result = EvidenceValidator.ValidateClaim(MemoryKind.Likes, [new(1, "a"), new(4, "b"), new(5, "c")], shots);

        Assert.Equal([1], result.Valid.Select(c => c.ShotId));
        Assert.Equal(2, result.Dropped);
    }

    [Fact]
    public void A_dislike_cites_only_drinks_rated_0_to_2()
    {
        var shots = new Dictionary<int, SnapshotShot>(Shots)
        {
            [4] = new(4, 12, DateTime.UtcNow, "Espresso", 2, "Sour.", ForMe: true),
        };

        var result = EvidenceValidator.ValidateClaim(MemoryKind.Dislikes, [new(1, "a"), new(4, "b")], shots);

        Assert.Equal([4], result.Valid.Select(c => c.ShotId));
        Assert.Equal(1, result.Dropped);
    }

    [Fact]
    public void Collapses_repeated_ids()
    {
        var result = EvidenceValidator.Validate([new(1, "a"), new(1, "again")], Shots);
        Assert.Single(result.Valid);
        Assert.Equal(0, result.Dropped);
    }
}

public class MemoryRulesTests
{
    static readonly DateTime Now = new(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc);

    static MemoryRow Row(string key, string source = "learned", DateTime? removed = null) => new()
    {
        Id = Guid.NewGuid(),
        Kind = key.StartsWith("dislikes") ? MemoryKind.Dislikes : MemoryKind.Likes,
        Key = key,
        Statement = key,
        Source = source,
        LearnedAt = Now.AddDays(-1),
        RemovedAt = removed,
    };

    static LearnedPreference Learned(string attribute, string value, MemoryKind kind = MemoryKind.Likes) =>
        new(kind, attribute, value, $"{kind} {value}", [new CitedShot(1, "x"), new CitedShot(3, "y")]);

    [Fact]
    public void Key_is_normalized()
    {
        Assert.Equal("likes:process:natural", MemoryRules.Key(MemoryKind.Likes, " Process ", "Natural"));
        Assert.Equal("dislikes:roast:dark", MemoryRules.Key(MemoryKind.Dislikes, "roast", "DARK "));
        Assert.Equal("likes:flavor:red-berries", MemoryRules.Key(MemoryKind.Likes, "flavor", "red berries"));
    }

    [Fact]
    public void Removed_items_stay_removed_after_relearning()
    {
        var existing = new List<MemoryRow> { Row("likes:process:natural", removed: Now.AddHours(-1)) };

        var merge = MemoryRules.Merge(existing, [Learned("process", "natural"), Learned("flavor", "berry")], Now);

        Assert.Equal(["likes:flavor:berry"], merge.ToAdd.Select(r => r.Key));
        Assert.Equal(1, merge.Blocked);
        Assert.Empty(merge.ToDelete);
    }

    [Fact]
    public void Relearning_replaces_learned_items_and_keeps_yours()
    {
        var oldLearned = Row("likes:flavor:chocolate");
        var yours = Row("neverroaster:roaster:ninefold", source: "you");
        var existing = new List<MemoryRow> { oldLearned, yours };

        var merge = MemoryRules.Merge(existing, [Learned("process", "natural")], Now);

        Assert.Equal([oldLearned.Id], merge.ToDelete.Select(r => r.Id));
        Assert.Equal(["likes:process:natural"], merge.ToAdd.Select(r => r.Key));
    }

    [Fact]
    public void Items_without_evidence_are_not_learned()
    {
        var noEvidence = new LearnedPreference(MemoryKind.Likes, "flavor", "citrus", "Likes citrus", []);
        var merge = MemoryRules.Merge([], [noEvidence], Now);
        Assert.Empty(merge.ToAdd);
    }

    [Fact]
    public void Dislikes_of_tag_attributes_become_avoid_rules()
    {
        var rows = new[]
        {
            Row("dislikes:roast:dark"),
            Row("dislikes:flavor:sour"),
            Row("dislikes:process:wet-hulled", removed: Now),
            Row("likes:process:natural"),
        };

        var avoid = MemoryRules.AvoidRules(rows);

        var rule = Assert.Single(avoid);
        Assert.Equal(("roast", "dark"), (rule.Attribute, rule.Value));
    }
}

public class ProductMatchTests
{
    [Fact]
    public void Matches_ignoring_case_and_spacing()
    {
        Assert.True(ProductMatch.Same("Wickerline Roasters", "Huila Washed", "wickerline roasters", "  Huila  washed "));
        Assert.False(ProductMatch.Same("Wickerline Roasters", "Huila Washed", "Ninefold Coffee", "Huila Washed"));
        Assert.False(ProductMatch.Same(null, "Huila Washed", "Wickerline Roasters", "Huila Washed"));
    }
}

public class StaleCheckTests
{
    static ShopVariantNow Now(decimal price = 17.50m, bool available = true) => new(price, available);

    [Fact]
    public void Same_price_and_in_stock_is_current()
    {
        Assert.Null(StaleCheck.Reason(17.50m, "EUR", Now()));
    }

    [Fact]
    public void A_price_change_makes_the_pick_stale()
    {
        Assert.Equal("The price changed from €17.50 to €19.25 since I picked it.", StaleCheck.Reason(17.50m, "EUR", Now(19.25m)));
    }

    [Fact]
    public void Sold_out_or_gone_makes_the_pick_stale()
    {
        Assert.Equal("It sold out since I picked it.", StaleCheck.Reason(17.50m, "EUR", Now(available: false)));
        Assert.Equal("It is no longer in the catalog.", StaleCheck.Reason(17.50m, "EUR", null));
    }
}

