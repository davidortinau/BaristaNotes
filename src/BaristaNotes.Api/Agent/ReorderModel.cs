using System.ComponentModel;
using System.Globalization;
using System.Text;
using BaristaNotes.Agent.Contracts;
using BaristaNotes.Api.Data;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace BaristaNotes.Api.Agent;

// The two jobs that need a model: learning preferences from free-text notes,
// and ranking candidates against those preferences. Everything the model
// returns is checked by code afterwards (see AgentRules.cs).

public sealed record LearnRequest(
    IReadOnlyList<SnapshotBean> Beans,
    IReadOnlyList<SnapshotBag> Bags,
    IReadOnlyList<SnapshotShot> ForMeShots,
    IReadOnlyList<string> RemovedKeys);

public sealed record RankMemory(string Statement, string Key, IReadOnlyList<int> ShotIds);

public sealed record RankNeed(int BagId, string BeanName, string? Roaster, string? Description, double? AverageRating, int Drinks);

/// <summary><c>PastBeans</c> lists what the person already had from this roaster.</summary>
public sealed record RankCandidate(CatalogItem Item, string Description, int? SameAsFinishedBagId, IReadOnlyList<string> PastBeans)
{
    public bool KnownRoaster => PastBeans.Count > 0;
}

public sealed record RankShot(int Id, string BeanName, string? DrinkType, int? Rating, string? Note, DateTime MadeAt);

public sealed record RankRequest(
    IReadOnlyList<RankMemory> Memory,
    IReadOnlyList<RankNeed> Needs,
    IReadOnlyList<RankCandidate> Candidates,
    IReadOnlyList<RankShot> Shots);

public sealed record ModelPick(
    int ForBagId,
    long ProductId,
    string Why,
    IReadOnlyList<CitedShot> Evidence,
    IReadOnlyList<string> Unknowns);

public interface IReorderModel
{
    Task<IReadOnlyList<LearnedPreference>> LearnAsync(LearnRequest request, CancellationToken ct);

    Task<IReadOnlyList<ModelPick>> RankAsync(RankRequest request, CancellationToken ct);
}

/// <summary>
/// Agent Framework agents on a Foundry chat model. Temperature 0 keeps the
/// demo repeatable. Spans go to the Aspire dashboard.
/// </summary>
public sealed class FoundryReorderModel : IReorderModel
{
    public const string TelemetrySource = "BaristaNotes.Agent";

    readonly AIAgent _learner;
    readonly AIAgent _ranker;

    public FoundryReorderModel(IChatClient chatClient, ILoggerFactory loggerFactory, IHostEnvironment environment)
    {
        _learner = CreateAgent(chatClient, loggerFactory, environment,
            "preference-learner",
            "Learns coffee preferences from a person's tasting notes and ratings.",
            Prompts.LearnerInstructions);

        _ranker = CreateAgent(chatClient, loggerFactory, environment,
            "reorder-ranker",
            "Chooses replacement coffee for finished bags and explains the choice with evidence.",
            Prompts.RankerInstructions);
    }

    static AIAgent CreateAgent(IChatClient chatClient, ILoggerFactory loggerFactory, IHostEnvironment environment,
        string name, string description, string instructions)
    {
        var agent = chatClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = name,
                Description = description,
                ChatOptions = new ChatOptions
                {
                    Instructions = instructions,
                    Temperature = 0,
                },
            },
            loggerFactory);

        // Prompts and answers appear in the dashboard in Development only.
        // The demo data is fictional.
        return agent.AsBuilder()
            .UseOpenTelemetry(TelemetrySource, a => a.EnableSensitiveData = environment.IsDevelopment())
            .Build();
    }

    public async Task<IReadOnlyList<LearnedPreference>> LearnAsync(LearnRequest request, CancellationToken ct)
    {
        var response = await _learner.RunAsync<LearnOutput>(Prompts.Learn(request), cancellationToken: ct);
        var output = response.Result;

        var result = new List<LearnedPreference>();
        foreach (var item in output.Preferences)
        {
            var kind = item.Kind.Trim().ToLowerInvariant() switch
            {
                "likes" => MemoryKind.Likes,
                "dislikes" => MemoryKind.Dislikes,
                _ => (MemoryKind?)null,
            };

            if (kind is null || string.IsNullOrWhiteSpace(item.Attribute) || string.IsNullOrWhiteSpace(item.Value))
            {
                continue;
            }

            result.Add(new LearnedPreference(
                kind.Value,
                item.Attribute,
                item.Value,
                item.Statement,
                item.Evidence.Select(e => new CitedShot(e.DrinkId, e.Point)).ToList()));
        }

        return result;
    }

    public async Task<IReadOnlyList<ModelPick>> RankAsync(RankRequest request, CancellationToken ct)
    {
        var response = await _ranker.RunAsync<RankOutput>(Prompts.Rank(request), cancellationToken: ct);
        return response.Result.Picks
            .Select(p => new ModelPick(
                p.FinishedBagId,
                p.ProductId,
                p.Why,
                p.Evidence.Select(e => new CitedShot(e.DrinkId, e.Point)).ToList(),
                p.Unknowns))
            .ToList();
    }

    // Structured output shapes. Descriptions become part of the JSON schema.

    sealed class LearnOutput
    {
        [Description("3 to 6 preferences, strongest first.")]
        public List<LearnedItem> Preferences { get; set; } = [];
    }

    sealed class LearnedItem
    {
        [Description("likes or dislikes")]
        public string Kind { get; set; } = "";

        [Description("One of: process, roast, origin, flavor, drink")]
        public string Attribute { get; set; } = "";

        [Description("Lowercase value. process: natural, washed, honey, wet-hulled. roast: light, medium, dark. origin: a country, for example ethiopia. flavor: one tasting-note family, for example berry, chocolate, citrus, floral, caramel. drink: for example espresso, flat white.")]
        public string Value { get; set; } = "";

        [Description("One short sentence in second person, for example: You like natural-process coffees.")]
        public string Statement { get; set; } = "";

        [Description("2 to 4 drinks from the list that show this preference.")]
        public List<Citation> Evidence { get; set; } = [];
    }

    sealed class Citation
    {
        [Description("A drink ID from the list. Never invent one.")]
        public int DrinkId { get; set; }

        [Description("Why this drink supports the point, at most 12 words.")]
        public string Point { get; set; } = "";
    }

    sealed class RankOutput
    {
        [Description("Exactly one pick per finished bag.")]
        public List<Pick> Picks { get; set; } = [];
    }

    sealed class Pick
    {
        [Description("The finished bag this pick replaces.")]
        public int FinishedBagId { get; set; }

        [Description("A product ID from the candidates.")]
        public long ProductId { get; set; }

        [Description("One or two short sentences in second person. Plain words, no marketing language.")]
        public string Why { get; set; } = "";

        [Description("2 to 4 drinks from the list that support the pick.")]
        public List<Citation> Evidence { get; set; } = [];

        [Description("1 to 3 short things you cannot know from this data that could make the pick wrong.")]
        public List<string> Unknowns { get; set; } = [];
    }
}

/// <summary>Prompt text. Kept apart so it is easy to read on a slide.</summary>
public static class Prompts
{
    public const string LearnerInstructions = """
        You learn the coffee preferences of one person from their espresso journal.
        Ratings use a 0 to 4 scale: 4 means loved it, 0 means undrinkable.

        Rules:
        - Use only the drinks listed. Every preference cites at least 2 drink IDs from the list. Never invent an ID.
        - Look for patterns across several drinks and beans, not single drinks.
        - Return 3 to 6 preferences, strongest first.
        - A preference key is kind:attribute:value, for example likes:process:natural.
        - Never return a preference whose key is in the removed list. The person removed those on purpose.
        """;

    public const string RankerInstructions = """
        You choose replacement coffee for one person. For each finished bag, pick exactly one coffee from the candidates.

        Rules:
        - Use only the candidates listed, by product ID. Use each candidate at most once.
        - If a candidate is the same coffee as a finished bag that the person rated 3 or higher on average, pick it for that bag. Reordering a favorite is what they want.
        - Otherwise pick the candidate that best matches what you remember about the person. What you remember is everything you know about their taste. Do not assume other preferences.
        - why: name the notes or drinks that connect the pick to this person, in one or two short sentences. Write to the person ("you").
        - Cite 2 to 4 drinks from the list as evidence. Never invent an ID.
        - unknowns: concrete things this data cannot tell you about this pick, written to the person. For example: "You have not had a coffee from Guji." "This is a lighter roast than Huila." "You have never ordered from this roaster." No vague doubts.
        """;

    public static string Learn(LearnRequest request)
    {
        var beans = request.Beans.ToDictionary(b => b.Id);
        var bags = request.Bags.ToDictionary(b => b.Id);
        var text = new StringBuilder();

        text.AppendLine("Beans:");
        foreach (var bean in request.Beans)
        {
            text.AppendLine($"- {bean.Name} from {bean.Roaster ?? "an unknown roaster"}. {bean.Origin}. {bean.Notes}");
        }

        text.AppendLine();
        text.AppendLine("Drinks made for this person:");
        foreach (var shot in request.ForMeShots.OrderBy(s => s.MadeAt).ThenBy(s => s.Id))
        {
            var bean = bags.TryGetValue(shot.BagId, out var bag) && beans.TryGetValue(bag.BeanId, out var b) ? b.Name : "unknown bean";
            text.AppendLine($"- drink {shot.Id}: {bean}, {shot.DrinkType}, {Rating(shot.Rating)}, {shot.MadeAt:yyyy-MM-dd}: \"{shot.TastingNotes}\"");
        }

        text.AppendLine();
        text.AppendLine("Removed preferences (never return these keys):");
        if (request.RemovedKeys.Count == 0)
        {
            text.AppendLine("- none");
        }

        foreach (var key in request.RemovedKeys)
        {
            text.AppendLine($"- {key}");
        }

        return text.ToString();
    }

    public static string Rank(RankRequest request)
    {
        var text = new StringBuilder();

        text.AppendLine("What you remember about this person:");
        if (request.Memory.Count == 0)
        {
            text.AppendLine("- nothing yet");
        }

        foreach (var item in request.Memory)
        {
            text.AppendLine($"- {item.Statement} (drinks {string.Join(", ", item.ShotIds)})");
        }

        text.AppendLine();
        text.AppendLine("Finished bags to replace:");
        foreach (var need in request.Needs)
        {
            var average = need.AverageRating is double a
                ? $"average rating {a.ToString("0.0", CultureInfo.InvariantCulture)} of 4 over {need.Drinks} drinks"
                : "not rated";
            text.AppendLine($"- bag {need.BagId}: {need.BeanName} from {need.Roaster ?? "an unknown roaster"}. {need.Description} {average}.");
        }

        text.AppendLine();
        text.AppendLine("Drinks you may cite:");
        foreach (var shot in request.Shots.OrderBy(s => s.MadeAt).ThenBy(s => s.Id))
        {
            text.AppendLine($"- drink {shot.Id}: {shot.BeanName}, {shot.DrinkType}, {Rating(shot.Rating)}: \"{shot.Note}\"");
        }

        text.AppendLine();
        text.AppendLine("Candidates:");
        foreach (var candidate in request.Candidates)
        {
            var item = candidate.Item;
            var line = new StringBuilder($"- product {item.ProductId}: {item.Title} from {item.RoasterName}. {candidate.Description} ");
            line.Append($"{Money.Format(item.Price, item.Currency)} plus {Money.Format(item.Shipping, item.Currency)} shipping.");
            if (candidate.SameAsFinishedBagId is int bagId)
            {
                line.Append($" Same coffee as finished bag {bagId}.");
            }

            line.Append(candidate.KnownRoaster
                ? $" The person has had {string.Join(" and ", candidate.PastBeans)} from this roaster."
                : " The person has never had coffee from this roaster.");

            text.AppendLine(line.ToString());
        }

        return text.ToString();
    }

    static string Rating(int? rating) => rating is int r ? $"rated {r}/4" : "not rated";
}
