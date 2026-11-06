using BaristaNotes.Agent.Contracts;
using BaristaNotes.Api.Data;
using BaristaNotes.Api.Shop;
using Microsoft.EntityFrameworkCore;

namespace BaristaNotes.Api.Agent;

public sealed record OrderRecord(bool IsNew, bool IsDuplicate, decimal BudgetLeft);

/// <summary>
/// Records orders reported by the shop webhook or by the agent's own order
/// call, whichever comes first. The same order ID is recorded once. A second
/// order for the same proposal is a duplicate charge, and the ledger says so.
/// </summary>
public sealed class OrderRecorder(IDbContextFactory<AgentDb> dbs, LedgerWriter ledger, TimeProvider time)
{
    static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<OrderRecord> RecordAsync(ShopOrder order, string reportedBy)
    {
        await Gate.WaitAsync(CancellationToken.None);
        try
        {
            await using var db = await dbs.CreateDbContextAsync(CancellationToken.None);
            var settings = await AgentState.GetSettingsAsync(db);
            var now = time.GetUtcNow().UtcDateTime;

            if (await db.Orders.FindAsync(order.Id) is not null)
            {
                // Already recorded by the other reporter (webhook or agent). It is a
                // duplicate if the pick's first order is a different one.
                var known = order.ProposalId is Guid pid ? await db.Proposals.FindAsync(pid) : null;
                var (_, leftNow) = await AgentState.BudgetAsync(db, settings, now);
                return new OrderRecord(
                    IsNew: false,
                    IsDuplicate: known?.OrderId is not null && known.OrderId != order.Id,
                    leftNow);
            }

            db.Orders.Add(new OrderRow
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                Roaster = order.Roaster,
                ProposalId = order.ProposalId,
                Total = order.Total,
                Currency = order.Currency,
                CreatedAt = order.CreatedAt.UtcDateTime,
                ReportedBy = reportedBy,
            });

            var proposal = order.ProposalId is Guid id ? await db.Proposals.FindAsync(id) : null;
            string? firstOrderNumber = null;
            if (proposal is not null)
            {
                if (proposal.OrderId is not null && proposal.OrderId != order.Id)
                {
                    firstOrderNumber = proposal.OrderNumber;
                }
                else
                {
                    proposal.OrderId = order.Id;
                    proposal.OrderNumber = order.OrderNumber;
                    proposal.Status = ProposalStatus.Ordered;
                }
            }

            await db.SaveChangesAsync();
            var (_, left) = await AgentState.BudgetAsync(db, settings, now);
            var money = $"{Money.Format(order.Total, order.Currency)}. {Money.Format(left, settings.Currency)} left this month.";

            if (firstOrderNumber is not null)
            {
                await ledger.WriteAsync(proposal!.RunId, LedgerKind.DuplicateOrder,
                    $"Second order for the same pick: {order.OrderNumber}.",
                    $"{firstOrderNumber} already exists, so you are charged twice. {money}",
                    order.Roaster, proposal.Id);
                return new OrderRecord(IsNew: true, IsDuplicate: true, left);
            }

            // When the agent placed the order it writes its own entry.
            if (reportedBy != "agent")
            {
                var title = order.Source == "checkout"
                    ? $"You placed order {order.OrderNumber} at {proposal?.RoasterName ?? order.Roaster}."
                    : $"Shop webhook: order {order.OrderNumber} created.";
                await ledger.WriteAsync(proposal?.RunId, LedgerKind.OrderReceived, title, money, order.Roaster, proposal?.Id);
            }

            return new OrderRecord(IsNew: true, IsDuplicate: false, left);
        }
        finally
        {
            Gate.Release();
        }
    }
}
