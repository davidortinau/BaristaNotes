using System.Reflection;
using BaristaNotes.Agent.Contracts;
using BaristaNotes.Services.Agent;

namespace BaristaNotes.Tests.Unit.Agent;

public sealed class AgentTextTests
{
    static readonly DateTimeOffset Now = new(new DateTime(2026, 10, 21, 9, 30, 0, DateTimeKind.Local));

    [Theory]
    [InlineData(80, "EUR", "€80")]
    [InlineData(57.5, "EUR", "€57.50")]
    [InlineData(12, "usd", "$12")]
    [InlineData(9.9, "CHF", "CHF 9.90")]
    public void Money_UsesTheCurrencySignOrCode(double amount, string currency, string expected)
    {
        Assert.Equal(expected, AgentText.Money((decimal)amount, currency));
    }

    [Theory]
    [InlineData("80", 80)]
    [InlineData("57.50", 57.50)]
    [InlineData("57,50", 57.50)]
    [InlineData("€ 80", 80)]
    [InlineData("0", 0)]
    public void TryParseBudget_AcceptsCommonInput(string input, double expected)
    {
        Assert.True(AgentText.TryParseBudget(input, out var amount));
        Assert.Equal((decimal)expected, amount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("eighty")]
    [InlineData("2000")]
    [InlineData("1.2.3")]
    public void TryParseBudget_RejectsInvalidInput(string input)
    {
        Assert.False(AgentText.TryParseBudget(input, out _));
    }

    [Fact]
    public void RunTitle_DescribesTheLatestRun()
    {
        Assert.Equal("No runs yet", AgentText.RunTitle(null, Now));
        Assert.Equal("Running", AgentText.RunTitle(Run(RunStatus.Running, endedAt: null), Now));
        Assert.Equal("Stopped", AgentText.RunTitle(Run(RunStatus.Stopped, Now.AddMinutes(-1)), Now));
        Assert.Equal("Done 2 min ago", AgentText.RunTitle(Run(RunStatus.Completed, Now.AddMinutes(-2)), Now));
    }

    [Fact]
    public void RunSummary_CountsRoastersSkipsAndPicks()
    {
        Assert.Equal(
            "Checked 5 roasters · skipped 9 · 1 pick",
            AgentText.RunSummary(Run(RunStatus.Completed, Now, roasters: 5, skipped: 9, picks: 1)));
    }

    [Fact]
    public void SnapshotLine_SaysWhatWasSharedAndWhatWasNotUsed()
    {
        var summary = new SnapshotSummary(Now.AddMinutes(-18), Beans: 5, Bags: 5, Shots: 22, GuestShots: 3);

        Assert.Equal(
            "Sent 21 Oct 09:12 · 5 beans · 5 bags · 22 drinks · 3 guest drinks not used for learning",
            AgentText.SnapshotLine(summary));
    }

    [Fact]
    public void EvidenceHeading_CombinesShotBeanRatingAndDate()
    {
        var evidence = new Evidence(12, "Rated 4 for blueberry", "Blueberry jam", 4, new DateTime(2026, 9, 24), "Kochere Natural");

        Assert.Equal("Shot 12 · Kochere Natural · 4/4 · 24 Sep", AgentText.EvidenceHeading(evidence));
        Assert.Equal("“Blueberry jam”", AgentText.Quote(evidence.Note));
    }

    [Theory]
    [InlineData(0, "Roasted today")]
    [InlineData(1, "Roasted yesterday")]
    [InlineData(27, "Roasted 27 days ago")]
    public void RoastedAgo_CountsWholeDays(int days, string expected)
    {
        var today = new DateTime(2026, 10, 21);
        Assert.Equal(expected, AgentText.RoastedAgo(today.AddDays(-days), today));
    }

    [Theory]
    [InlineData(0, "Thanks. Nothing is finished, so the agent will not order.")]
    [InlineData(1, "Thanks. The agent will look for a replacement for 1 bag.")]
    [InlineData(2, "Thanks. The agent will look for a replacement for 2 bags.")]
    public void CheckInThanks_MatchesTheFinishedCount(int finished, string expected)
    {
        Assert.Equal(expected, AgentText.CheckInThanks(finished));
    }

    [Fact]
    public void CheckInLine_HandlesNoCheckIn()
    {
        Assert.Equal("No check-in yet", AgentText.CheckInLine(new CheckInStatus(null, true, []), Now));
        Assert.Equal("Last check-in 3 days ago", AgentText.CheckInLine(new CheckInStatus(Now.AddDays(-3), false, []), Now));
    }

    [Fact]
    public void ProposalStatusText_IncludesTheOrderNumber()
    {
        Assert.Equal("Ordered #1003", AgentText.ProposalStatusText(ProposalWith(ProposalStatus.Ordered, "1003")));
        Assert.Equal("Ordered #1003", AgentText.ProposalStatusText(ProposalWith(ProposalStatus.Ordered, "#1003")));
        Assert.Equal("Cart ready", AgentText.ProposalStatusText(ProposalWith(ProposalStatus.CartReady, null)));
    }

    [Fact]
    public void FixedText_ContainsNoEmoji()
    {
        var fixedText = typeof(AgentText)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Concat(Enum.GetValues<Autonomy>().Select(AgentText.AutonomyDescription))
            .Concat([AgentText.Money(1, "EUR"), AgentText.Quote("x"), AgentText.RunSummary(Run(RunStatus.Completed, Now))]);

        Assert.All(fixedText, text => Assert.DoesNotContain(text, IsEmoji));
    }

    static bool IsEmoji(char c) =>
        char.IsSurrogate(c) || c is >= '☀' and <= '➿' || c == '️';

    static RunInfo Run(RunStatus status, DateTimeOffset? endedAt, int roasters = 5, int skipped = 9, int picks = 1) =>
        new(Guid.NewGuid(), Now.AddMinutes(-5), endedAt, status, Autonomy.PrepareCart, "app", null,
            roasters, skipped, picks, []);

    static Proposal ProposalWith(ProposalStatus status, string? orderNumber) =>
        new(Guid.NewGuid(), Guid.NewGuid(), 1, "Kochere Natural", "copperhaze", "Copperhaze Coffee", 1, 1,
            "Guji Natural", null, 17.50m, 4.90m, "EUR", "Why", [], [], false, 57.50m, status, null, null, null,
            orderNumber, Now);
}
