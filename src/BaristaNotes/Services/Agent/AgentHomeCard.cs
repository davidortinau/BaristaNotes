using BaristaNotes.Agent.Contracts;

namespace BaristaNotes.Services.Agent;

public enum AgentHomeCardKind
{
    CheckIn,
    WhileAway,
}

/// <summary>
/// The single agent card at the top of the Activity feed. A due check-in
/// wins over an unseen run, because the agent cannot order without it.
/// </summary>
public sealed record AgentHomeCard(AgentHomeCardKind Kind, string Label, string Title, string Action)
{
    /// <summary>Preference key holding the id of the last finished run the user opened.</summary>
    public const string LastSeenRunIdKey = "agent.lastSeenRunId";

    public static AgentHomeCard? Decide(AgentStatus status, int openBagCount, string? lastSeenRunId)
    {
        if (status.CheckIn.Due && openBagCount > 0)
        {
            return new AgentHomeCard(
                AgentHomeCardKind.CheckIn,
                "STILL HAVE THESE?",
                AgentText.Count(openBagCount, "open bag", "open bags"),
                "CHECK IN");
        }

        if (status.LatestRun is { Status: not RunStatus.Running } run
            && !string.Equals(run.Id.ToString(), lastSeenRunId, StringComparison.OrdinalIgnoreCase))
        {
            return new AgentHomeCard(
                AgentHomeCardKind.WhileAway,
                "WHILE YOU WERE AWAY",
                AgentText.RunSummary(run),
                "VIEW");
        }

        return null;
    }
}
