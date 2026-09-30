using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services.Workflows;

public sealed record BagDetails(Bag? Bag, RatingAggregateDto? Rating, int ShotCount);

public sealed class BagDetailsWorkflow(IBagService bags, IRatingService ratings)
{
    public async Task<BagDetails> LoadAsync(int bagId)
    {
        var bag = await bags.GetBagByIdAsync(bagId);
        if (bag is null)
            return new(null, null, 0);

        // These services can share a scoped DbContext, so keep the queries sequential.
        var rating = await ratings.GetBagRatingAsync(bagId);
        var count = await bags.GetShotCountAsync(bagId);
        return new(bag, rating, count);
    }
}
