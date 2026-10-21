using System.Globalization;
using BaristaNotes.Agent.Contracts;

namespace BaristaNotes.Services.Agent;

/// <summary>
/// The words and numbers the agent screens show. Pure, so the wording is
/// unit tested and the pages stay about layout.
/// </summary>
public static class AgentText
{
    public const string Separator = " · ";

    public const string NotConnected = "Agent not connected. Start the app from the Aspire AppHost.";
    public const string Unreachable = "Could not reach the agent. Check that the API is running.";
    public const string NothingShared = "No photos, names, equipment, or recipes are shared.";

    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>"€80", "€57.50", "$12", "CHF 9.90".</summary>
    public static string Money(decimal amount, string? currency)
    {
        var number = amount == decimal.Truncate(amount)
            ? amount.ToString("0", Invariant)
            : amount.ToString("0.00", Invariant);
        var sign = amount < 0 ? "-" : "";
        number = number.TrimStart('-');

        return (currency ?? "").ToUpperInvariant() switch
        {
            "EUR" => $"{sign}€{number}",
            "USD" => $"{sign}${number}",
            "GBP" => $"{sign}£{number}",
            "" => $"{sign}{number}",
            var code => $"{sign}{code} {number}",
        };
    }

    public const decimal MaxMonthlyBudget = 1000m;

    /// <summary>
    /// Reads a typed budget such as "80", "57.50", "57,50", or "€ 80".
    /// Accepts 0 to <see cref="MaxMonthlyBudget"/>, rounded to cents.
    /// </summary>
    public static bool TryParseBudget(string? input, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var cleaned = new string(input.Where(c => char.IsDigit(c) || c is '.' or ',').ToArray())
            .Replace(',', '.');
        if (!decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint, Invariant, out var value)
            || value < 0
            || value > MaxMonthlyBudget)
            return false;

        amount = decimal.Round(value, 2);
        return true;
    }

    public static string Count(int count, string singular, string plural) =>
        count == 1 ? $"1 {singular}" : $"{count} {plural}";

    /// <summary>"just now", "2 min ago", "3 h ago", "yesterday", "3 days ago".</summary>
    public static string Ago(DateTimeOffset at, DateTimeOffset now)
    {
        var elapsed = now - at;
        if (elapsed < TimeSpan.FromMinutes(1))
            return "just now";
        if (elapsed < TimeSpan.FromHours(1))
            return $"{(int)elapsed.TotalMinutes} min ago";
        if (elapsed < TimeSpan.FromDays(1))
            return $"{(int)elapsed.TotalHours} h ago";

        var days = (now.ToLocalTime().Date - at.ToLocalTime().Date).Days;
        return days <= 1 ? "yesterday" : $"{days} days ago";
    }

    /// <summary>Page title for the latest run: "Running", "Done 2 min ago", "No runs yet".</summary>
    public static string RunTitle(RunInfo? run, DateTimeOffset now) => run?.Status switch
    {
        null => "No runs yet",
        RunStatus.Running => "Running",
        RunStatus.Stopped => "Stopped",
        RunStatus.Failed => "Failed",
        _ => $"Done {Ago(run.EndedAt ?? run.StartedAt, now)}",
    };

    /// <summary>"Checked 5 roasters · skipped 9 · 1 pick".</summary>
    public static string RunSummary(RunInfo run) =>
        $"Checked {Count(run.RoastersChecked, "roaster", "roasters")}"
        + $"{Separator}skipped {run.Skipped}"
        + $"{Separator}{Count(run.Picks, "pick", "picks")}";

    public static string BudgetLine(AgentStatus status) =>
        $"{Money(status.BudgetLeft, status.Settings.Currency)} left of "
        + $"{Money(status.Settings.MonthlyBudget, status.Settings.Currency)} this month";

    /// <summary>"€80 · €57.50 left this month".</summary>
    public static string BudgetTile(AgentStatus status) =>
        $"{Money(status.Settings.MonthlyBudget, status.Settings.Currency)}{Separator}"
        + $"{Money(status.BudgetLeft, status.Settings.Currency)} left this month";

    public static string AutonomyLabel(Autonomy autonomy) => autonomy switch
    {
        Autonomy.Watch => "WATCH",
        Autonomy.PrepareCart => "CART",
        Autonomy.ReorderFavorites => "REORDER",
        _ => autonomy.ToString().ToUpperInvariant(),
    };

    public static string AutonomyDescription(Autonomy autonomy) => autonomy switch
    {
        Autonomy.Watch => "Finds a pick and tells you.",
        Autonomy.PrepareCart => "Prepares a cart. You place the order.",
        Autonomy.ReorderFavorites => "Reorders a finished favorite within your budget. Anything else gets a cart.",
        _ => "",
    };

    public static string CheckInLine(CheckInStatus? checkIn, DateTimeOffset now) =>
        checkIn?.LastCheckInAt is { } at
            ? $"Last check-in {Ago(at, now)}"
            : "No check-in yet";

    /// <summary>"Roasted today", "Roasted yesterday", "Roasted 27 days ago".</summary>
    public static string RoastedAgo(DateTime roastDate, DateTime today)
    {
        var days = (today.Date - roastDate.Date).Days;
        return days switch
        {
            <= 0 => "Roasted today",
            1 => "Roasted yesterday",
            _ => $"Roasted {days} days ago",
        };
    }

    public static string ProposalStatusText(Proposal proposal) => proposal.Status switch
    {
        ProposalStatus.Proposed => "Proposed",
        ProposalStatus.CartReady => "Cart ready",
        ProposalStatus.Ordered => string.IsNullOrWhiteSpace(proposal.OrderNumber)
            ? "Ordered"
            : $"Ordered #{proposal.OrderNumber.TrimStart('#')}",
        ProposalStatus.Dismissed => "Dismissed",
        ProposalStatus.CartRemoved => "Cart removed",
        ProposalStatus.Stale => "Out of date",
        _ => proposal.Status.ToString(),
    };

    public static string ProposalKind(Proposal proposal) =>
        proposal.IsFavoriteReorder ? "REORDER" : "PICK";

    /// <summary>"€17.50 + €4.90 shipping".</summary>
    public static string PriceLine(Proposal proposal) =>
        proposal.Shipping > 0
            ? $"{Money(proposal.Price, proposal.Currency)} + {Money(proposal.Shipping, proposal.Currency)} shipping"
            : $"{Money(proposal.Price, proposal.Currency)}, free shipping";

    /// <summary>"Shot 12 · Kochere Natural · 4/4 · 24 Sep".</summary>
    public static string EvidenceHeading(Evidence evidence)
    {
        var parts = new List<string> { $"Shot {evidence.ShotId}" };
        if (!string.IsNullOrWhiteSpace(evidence.BeanName))
            parts.Add(evidence.BeanName!);
        if (evidence.Rating is { } rating)
            parts.Add($"{rating}/4");
        if (evidence.MadeAt is { } madeAt)
            parts.Add(ShortDate(madeAt));
        return string.Join(Separator, parts);
    }

    public static string Quote(string? note) =>
        string.IsNullOrWhiteSpace(note) ? "No note" : $"“{note.Trim()}”";

    public static string ShortDate(DateTime date) => date.ToString("d MMM", Invariant);

    public static string Clock(DateTimeOffset at) => at.ToLocalTime().ToString("HH:mm:ss", Invariant);

    /// <summary>"Sent 21 Oct 09:12 · 5 beans · 5 bags · 22 drinks · 3 guest drinks not used for learning".</summary>
    public static string SnapshotLine(SnapshotSummary summary)
    {
        var line = $"Sent {summary.ReceivedAt.ToLocalTime().ToString("d MMM HH:mm", Invariant)}"
            + $"{Separator}{Count(summary.Beans, "bean", "beans")}"
            + $"{Separator}{Count(summary.Bags, "bag", "bags")}"
            + $"{Separator}{Count(summary.Shots, "drink", "drinks")}";
        if (summary.GuestShots > 0)
            line += $"{Separator}{Count(summary.GuestShots, "guest drink", "guest drinks")} not used for learning";
        return line;
    }

    public static string LearnedTitle(int count) => count switch
    {
        0 => "Nothing learned yet",
        1 => "1 thing learned",
        _ => $"{count} things learned",
    };

    public static string MemoryBasis(MemoryItem item) =>
        string.Equals(item.Source, "you", StringComparison.OrdinalIgnoreCase)
            ? "Added by you"
            : $"Based on {Count(item.Evidence.Count, "drink", "drinks")}";

    public static string CheckInThanks(int finishedCount) => finishedCount switch
    {
        0 => "Thanks. Nothing is finished, so the agent will not order.",
        1 => "Thanks. The agent will look for a replacement for 1 bag.",
        _ => $"Thanks. The agent will look for a replacement for {finishedCount} bags.",
    };
}
