using BaristaNotes.Agent.Contracts;
using BaristaNotes.Services.Agent;

namespace BaristaNotes.Tests.Unit.Agent;

public sealed class AgentHomeCardTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 30, 0, TimeSpan.FromHours(2));
    static readonly Guid RunId = Guid.Parse("6f9b0c1e-2a3d-4e5f-8a9b-0c1d2e3f4a5b");

    [Fact]
    public void DueCheckInWithOpenBags_AsksToCheckIn()
    {
        var card = AgentHomeCard.Decide(Status(checkInDue: true, Run(RunStatus.Completed)), openBagCount: 3, lastSeenRunId: null);

        Assert.NotNull(card);
        Assert.Equal(AgentHomeCardKind.CheckIn, card.Kind);
        Assert.Equal("STILL HAVE THESE?", card.Label);
        Assert.Equal("3 open bags", card.Title);
    }

    [Fact]
    public void DueCheckInWithoutOpenBags_FallsBackToTheUnseenRun()
    {
        var card = AgentHomeCard.Decide(Status(checkInDue: true, Run(RunStatus.Completed)), openBagCount: 0, lastSeenRunId: null);

        Assert.NotNull(card);
        Assert.Equal(AgentHomeCardKind.WhileAway, card.Kind);
        Assert.Equal("Checked 5 roasters · skipped 9 · 1 pick", card.Title);
    }

    [Fact]
    public void SeenRun_ShowsNoCard()
    {
        var card = AgentHomeCard.Decide(Status(checkInDue: false, Run(RunStatus.Completed)), 3, RunId.ToString().ToUpperInvariant());

        Assert.Null(card);
    }

    [Fact]
    public void RunningRun_ShowsTheAgentAtWork()
    {
        var card = AgentHomeCard.Decide(Status(checkInDue: false, Run(RunStatus.Running)), 3, null);

        Assert.NotNull(card);
        Assert.Equal(AgentHomeCardKind.Working, card.Kind);
        Assert.Equal("AGENT AT WORK", card.Label);
        Assert.Equal("WATCH", card.Action);
    }

    [Fact]
    public void NoRun_ShowsNoCard()
    {
        Assert.Null(AgentHomeCard.Decide(Status(checkInDue: false, run: null), 3, null));
    }

    [Fact]
    public void StoppedRun_CountsAsEnded()
    {
        var card = AgentHomeCard.Decide(Status(checkInDue: false, Run(RunStatus.Stopped)), 3, null);

        Assert.Equal(AgentHomeCardKind.WhileAway, card?.Kind);
    }

    static AgentStatus Status(bool checkInDue, RunInfo? run) =>
        new(
            new AgentSettings(Autonomy.PrepareCart, 80m, "EUR", ["copperhaze"], 0),
            SpentThisMonth: 22.50m,
            BudgetLeft: 57.50m,
            LatestRun: run,
            CheckIn: new CheckInStatus(null, checkInDue, []),
            LastSnapshot: null);

    static RunInfo Run(RunStatus status) =>
        new(RunId, Now.AddMinutes(-10), status == RunStatus.Running ? null : Now.AddMinutes(-2), status,
            Autonomy.PrepareCart, "app", null, RoastersChecked: 5, Skipped: 9, Picks: 1, ProposalIds: []);
}
