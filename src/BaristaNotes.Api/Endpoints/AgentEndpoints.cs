using System.Text.Json;
using BaristaNotes.Agent.Contracts;
using BaristaNotes.Api.Agent;
using BaristaNotes.Api.Data;
using BaristaNotes.Api.Shop;
using Microsoft.EntityFrameworkCore;

namespace BaristaNotes.Api.Endpoints;

public static class AgentEndpoints
{
    public static WebApplication MapAgentEndpoints(this WebApplication app)
    {
        MapSettings(app);
        MapSnapshot(app);
        MapMemory(app);
        MapCheckIns(app);
        MapRuns(app);
        MapProposals(app);
        MapDemo(app);
        return app;
    }

    // -----------------------------------------------------------------------
    // Settings and status
    // -----------------------------------------------------------------------

    static void MapSettings(WebApplication app)
    {
        app.MapGet("/agent/status", async (AgentDb db, TimeProvider time, CancellationToken ct) =>
        {
            var now = time.GetUtcNow().UtcDateTime;
            var settings = await AgentState.GetSettingsAsync(db, ct);
            var (spent, left) = await AgentState.BudgetAsync(db, settings, now, ct);
            var latest = await db.Runs.AsNoTracking().OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(ct);
            var snapshot = await db.Snapshots.AsNoTracking().FirstOrDefaultAsync(ct);
            return new AgentStatus(
                settings.ToContract(),
                spent,
                left,
                await AgentState.RunInfoAsync(db, latest, ct),
                await AgentState.CheckInStatusAsync(db, now, ct),
                AgentState.Summarize(snapshot));
        });

        app.MapGet("/agent/settings", async (AgentDb db, CancellationToken ct) =>
            (await AgentState.GetSettingsAsync(db, ct)).ToContract());

        app.MapPut("/agent/settings", async (AgentSettings update, AgentDb db, CancellationToken ct) =>
        {
            if (!Enum.IsDefined(update.Autonomy))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["autonomy"] = ["Autonomy must be Watch, PrepareCart, or ReorderFavorites."],
                });
            }

            if (update.MonthlyBudget is < 0 or > 1000)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["monthlyBudget"] = ["The monthly budget must be between 0 and 1000."],
                });
            }

            var settings = await AgentState.GetSettingsAsync(db, ct);
            settings.Autonomy = update.Autonomy;
            settings.MonthlyBudget = decimal.Round(update.MonthlyBudget, 2);
            settings.Currency = string.IsNullOrWhiteSpace(update.Currency) ? settings.Currency : update.Currency.Trim().ToUpperInvariant();
            settings.WatchedRoasters = update.WatchedRoasters
                .Select(r => r.Trim().ToLowerInvariant())
                .Where(r => r.Length > 0)
                .Distinct()
                .ToList();
            settings.StepDelayMs = Math.Clamp(update.StepDelayMs, 0, 10_000);
            await db.SaveChangesAsync(ct);
            return Results.Ok(settings.ToContract());
        });

        app.MapGet("/roasters", async (AgentDb db, ShopClient shop, CancellationToken ct) =>
        {
            var settings = await AgentState.GetSettingsAsync(db, ct);
            var roasters = await shop.GetRoastersAsync(ct);
            IReadOnlyList<Roaster> result = roasters
                .Select(r => new Roaster(r.Handle, r.Name, r.City, r.Shipping, r.Currency, settings.WatchedRoasters.Contains(r.Handle)))
                .ToList();
            return result;
        });
    }

    // -----------------------------------------------------------------------
    // Snapshot: what the app shares
    // -----------------------------------------------------------------------

    static void MapSnapshot(WebApplication app)
    {
        app.MapPost("/snapshot", async (Snapshot snapshot, AgentDb db, TimeProvider time, CancellationToken ct) =>
        {
            var row = await db.Snapshots.FindAsync([SnapshotRow.SingletonId], ct);
            if (row is null)
            {
                row = new SnapshotRow();
                db.Snapshots.Add(row);
            }

            row.ReceivedAt = time.GetUtcNow().UtcDateTime;
            row.Json = JsonSerializer.Serialize(snapshot, AgentJsonContext.Default.Snapshot);
            row.Hash = AgentState.LearningHash(snapshot);
            await db.SaveChangesAsync(ct);
            return AgentState.Summarize(row);
        });

        // Deletes the shared data and what was learned from it. Items you
        // removed stay removed, and items you added (Never this roaster) stay.
        app.MapDelete("/snapshot", async (AgentDb db, CancellationToken ct) =>
        {
            await db.Snapshots.ExecuteDeleteAsync(ct);
            await db.Memory.Where(m => m.Source == "learned" && m.RemovedAt == null).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
    }

    // -----------------------------------------------------------------------
    // Memory
    // -----------------------------------------------------------------------

    static void MapMemory(WebApplication app)
    {
        app.MapGet("/memory", async (AgentDb db, CancellationToken ct) =>
        {
            var snapshotRow = await db.Snapshots.AsNoTracking().FirstOrDefaultAsync(ct);
            var snapshot = AgentState.ReadSnapshot(snapshotRow);
            var rows = await db.Memory.AsNoTracking().OrderBy(m => m.Kind).ThenBy(m => m.LearnedAt).ToListAsync(ct);
            return new MemoryView(
                rows.Where(r => r.RemovedAt is null).Select(r => AgentState.ToContract(r, snapshot)).ToList(),
                rows.Where(r => r.RemovedAt is not null).Select(r => AgentState.ToContract(r, snapshot)).ToList(),
                AgentState.Summarize(snapshotRow));
        });

        app.MapDelete("/memory/{id:guid}", async (Guid id, AgentDb db, TimeProvider time, CancellationToken ct) =>
        {
            var row = await db.Memory.FindAsync([id], ct);
            if (row is null)
            {
                return Results.NotFound();
            }

            row.RemovedAt ??= time.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    // -----------------------------------------------------------------------
    // Check-ins
    // -----------------------------------------------------------------------

    static void MapCheckIns(WebApplication app)
    {
        app.MapPost("/checkins", async (CheckIn checkIn, AgentDb db, TimeProvider time, CancellationToken ct) =>
        {
            var now = time.GetUtcNow().UtcDateTime;
            db.CheckIns.Add(new CheckInRow
            {
                At = now,
                FinishedBagIds = checkIn.FinishedBagIds.Distinct().ToList(),
                StillHaveBagIds = checkIn.StillHaveBagIds.Distinct().ToList(),
            });
            await db.SaveChangesAsync(ct);
            return await AgentState.CheckInStatusAsync(db, now, ct);
        });

        app.MapGet("/checkins/latest", async (AgentDb db, TimeProvider time, CancellationToken ct) =>
            await AgentState.CheckInStatusAsync(db, time.GetUtcNow().UtcDateTime, ct));
    }

    // -----------------------------------------------------------------------
    // Runs and the ledger
    // -----------------------------------------------------------------------

    static void MapRuns(WebApplication app)
    {
        app.MapPost("/runs", async (StartRun? start, RunCoordinator runs, AgentDb db, CancellationToken ct) =>
        {
            var (run, started) = await runs.StartAsync(start?.Trigger ?? "app");
            var info = await AgentState.RunInfoAsync(db, run, ct);
            return started
                ? Results.Accepted($"/runs/{run.Id}", info)
                : Results.Conflict(info);
        });

        app.MapGet("/runs/latest", async (AgentDb db, CancellationToken ct) =>
        {
            var latest = await db.Runs.AsNoTracking().OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(ct);
            return latest is null ? Results.NoContent() : Results.Ok(await AgentState.RunInfoAsync(db, latest, ct));
        });

        app.MapGet("/runs/{id:guid}", async (Guid id, AgentDb db, CancellationToken ct) =>
        {
            var run = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
            return run is null ? Results.NotFound() : Results.Ok(await AgentState.RunInfoAsync(db, run, ct));
        });

        app.MapPost("/runs/{id:guid}/stop", async (Guid id, RunCoordinator runs, AgentDb db, CancellationToken ct) =>
        {
            var run = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
            if (run is null)
            {
                return Results.NotFound();
            }

            runs.Stop(id);

            // Give the run a moment to write its Stopped entry.
            for (var i = 0; i < 20 && run.Status == RunStatus.Running; i++)
            {
                await Task.Delay(100, ct);
                run = await db.Runs.AsNoTracking().FirstAsync(r => r.Id == id, ct);
            }

            return Results.Ok(await AgentState.RunInfoAsync(db, run, ct));
        });

        // For the dashboard's Stop command, which does not know the run ID.
        app.MapPost("/runs/stop", async (RunCoordinator runs, AgentDb db, CancellationToken ct) =>
        {
            var running = await db.Runs.AsNoTracking().Where(r => r.Status == RunStatus.Running)
                .OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(ct);
            if (running is null || !runs.Stop(running.Id))
            {
                return Results.Ok(new { message = "No run is going." });
            }

            return Results.Ok(new { message = "Stopping the run." });
        });

        app.MapGet("/runs/{id:guid}/ledger",async (Guid id, int? after, AgentDb db, CancellationToken ct) =>
            await AgentState.LedgerAsync(db, id, after ?? 0, ct));

        app.MapPost("/ledger/{entryId:long}/remove-cart", async (
            long entryId, AgentDb db, ShopClient shop, LedgerWriter ledger, CancellationToken ct) =>
        {
            var entry = await db.Ledger.AsNoTracking().FirstOrDefaultAsync(l => l.Id == entryId, ct);
            if (entry?.ProposalId is not Guid proposalId || entry.CartId is null)
            {
                return Results.NotFound();
            }

            var proposal = await db.Proposals.FindAsync([proposalId], ct);
            if (proposal is null || proposal.CartId != entry.CartId)
            {
                return Results.NotFound();
            }

            if (proposal.Status == ProposalStatus.CartReady)
            {
                if (!await shop.DeleteCartAsync(entry.CartId, ct))
                {
                    return Results.Conflict(new { error = "That cart was already ordered." });
                }

                proposal.Status = ProposalStatus.CartRemoved;
                await db.SaveChangesAsync(ct);
                await ledger.WriteAsync(proposal.RunId, LedgerKind.CartRemoved,
                    $"Removed the cart at {proposal.RoasterName}.", "You asked me to.", proposal.Roaster, proposal.Id, entry.CartId);
            }

            var refreshed = await db.Ledger.AsNoTracking().FirstAsync(l => l.Id == entryId, ct);
            return Results.Ok(AgentState.ToContract(refreshed, cartOpen: false));
        });
    }

    // -----------------------------------------------------------------------
    // Proposals
    // -----------------------------------------------------------------------

    static void MapProposals(WebApplication app)
    {
        app.MapGet("/proposals", async (Guid? runId, AgentDb db, TimeProvider time, CancellationToken ct) =>
        {
            var query = db.Proposals.AsNoTracking();
            if (runId is Guid id)
            {
                query = query.Where(p => p.RunId == id);
            }

            var rows = await query.OrderByDescending(p => p.CreatedAt).Take(50).ToListAsync(ct);
            var (snapshot, left) = await ProposalContextAsync(db, time, ct);
            IReadOnlyList<Proposal> result = rows.Select(r => AgentState.ToContract(r, snapshot, left)).ToList();
            return result;
        });

        app.MapGet("/proposals/{id:guid}", async (Guid id, AgentDb db, TimeProvider time, CancellationToken ct) =>
        {
            var row = await db.Proposals.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
            if (row is null)
            {
                return Results.NotFound();
            }

            var (snapshot, left) = await ProposalContextAsync(db, time, ct);
            return Results.Ok(AgentState.ToContract(row, snapshot, left));
        });

        // Buy: make sure there is a cart, then the app opens its checkout page.
        app.MapPost("/proposals/{id:guid}/cart", async (
            Guid id, AgentDb db, ShopClient shop, LedgerWriter ledger, TimeProvider time, CancellationToken ct) =>
        {
            var row = await db.Proposals.FindAsync([id], ct);
            if (row is null)
            {
                return Results.NotFound();
            }

            if (row.Status is ProposalStatus.Ordered or ProposalStatus.Dismissed or ProposalStatus.Stale)
            {
                return Results.Conflict(new { error = row.Status == ProposalStatus.Stale
                    ? row.ActionNote ?? "This pick is out of date. Run the agent again."
                    : $"This pick is {row.Status.ToString().ToLowerInvariant()}." });
            }

            // What you approve must still be what you get: recheck before checkout.
            var now = await shop.GetVariantNowAsync(row.Roaster, row.ProductId, row.VariantId, ct);
            if (StaleCheck.Reason(row.Price, row.Currency, now) is { } stale)
            {
                var removed = row.Status == ProposalStatus.CartReady && row.CartId is not null
                              && await shop.DeleteCartAsync(row.CartId, ct)
                    ? $" Removed the cart at {row.RoasterName}."
                    : "";
                row.Status = ProposalStatus.Stale;
                row.ActionNote = stale + " Run the agent again for a fresh pick.";
                await db.SaveChangesAsync(ct);
                await ledger.WriteAsync(row.RunId, LedgerKind.Notice, "This pick is out of date.",
                    stale + " I did not open checkout." + removed, row.Roaster, row.Id);
                return Results.Conflict(new { error = row.ActionNote });
            }

            if (row.Status != ProposalStatus.CartReady || row.CheckoutUrl is null)
            {
                await ledger.WriteAsync(row.RunId, LedgerKind.CartCreating, $"Creating a cart at {row.RoasterName}.",
                    "You tapped Buy.", row.Roaster, row.Id);
                var cart = await shop.CreateCartAsync(row.Roaster, row.VariantId, row.Id, ct);
                row.CartId = cart.Id;
                row.CheckoutUrl = cart.CheckoutUrl;
                row.Status = ProposalStatus.CartReady;
                await db.SaveChangesAsync(ct);
                await ledger.WriteAsync(row.RunId, LedgerKind.CartReady, $"Cart ready at {row.RoasterName}. Nothing charged.",
                    "Opening checkout.", row.Roaster, row.Id, cart.Id);
            }

            var (snapshot, left) = await ProposalContextAsync(db, time, ct);
            return Results.Ok(AgentState.ToContract(row, snapshot, left));
        });

        app.MapPost("/proposals/{id:guid}/dismiss", async (
            Guid id, AgentDb db, ShopClient shop, LedgerWriter ledger, TimeProvider time, CancellationToken ct) =>
        {
            var row = await db.Proposals.FindAsync([id], ct);
            if (row is null)
            {
                return Results.NotFound();
            }

            await DismissAsync(row, db, shop, ledger, "You said not now.", ct);
            var (snapshot, left) = await ProposalContextAsync(db, time, ct);
            return Results.Ok(AgentState.ToContract(row, snapshot, left));
        });

        app.MapPost("/proposals/{id:guid}/never-roaster", async (
            Guid id, AgentDb db, ShopClient shop, LedgerWriter ledger, TimeProvider time, CancellationToken ct) =>
        {
            var row = await db.Proposals.FindAsync([id], ct);
            if (row is null)
            {
                return Results.NotFound();
            }

            var key = MemoryRules.Key(MemoryKind.NeverRoaster, "roaster", row.Roaster);
            if (!await db.Memory.AnyAsync(m => m.Key == key && m.RemovedAt == null, ct))
            {
                db.Memory.Add(new MemoryRow
                {
                    Id = Guid.NewGuid(),
                    Kind = MemoryKind.NeverRoaster,
                    Key = key,
                    Statement = $"Never buy from {row.RoasterName}.",
                    EvidenceJson = "[]",
                    LearnedAt = time.GetUtcNow().UtcDateTime,
                    Source = "you",
                });
            }

            await DismissAsync(row, db, shop, ledger, $"You said never {row.RoasterName}. I will skip it from now on.", ct);
            var (snapshot, left) = await ProposalContextAsync(db, time, ct);
            return Results.Ok(AgentState.ToContract(row, snapshot, left));
        });
    }

    static async Task DismissAsync(ProposalRow row, AgentDb db, ShopClient shop, LedgerWriter ledger, string reason, CancellationToken ct)
    {
        if (row.Status == ProposalStatus.Ordered)
        {
            return;
        }

        var detail = (string?)null;
        if (row.Status == ProposalStatus.CartReady && row.CartId is not null)
        {
            detail = await shop.DeleteCartAsync(row.CartId, ct)
                ? $"Removed the cart at {row.RoasterName}."
                : "The cart was already ordered.";
        }

        row.Status = ProposalStatus.Dismissed;
        await db.SaveChangesAsync(ct);
        await ledger.WriteAsync(row.RunId, LedgerKind.Notice, reason, detail, row.Roaster, row.Id);
    }

    static async Task<(Snapshot? Snapshot, decimal Left)> ProposalContextAsync(AgentDb db, TimeProvider time, CancellationToken ct)
    {
        var settings = await AgentState.GetSettingsAsync(db, ct);
        var (_, left) = await AgentState.BudgetAsync(db, settings, time.GetUtcNow().UtcDateTime, ct);
        var snapshot = AgentState.ReadSnapshot(await db.Snapshots.AsNoTracking().FirstOrDefaultAsync(ct));
        return (snapshot, left);
    }

    // -----------------------------------------------------------------------
    // Demo reset
    // -----------------------------------------------------------------------

    static void MapDemo(WebApplication app)
    {
        // Conference Wi-Fi switch: Only = use rehearsal answers, Auto = live model with replay as backup.
        app.MapPost("/demo/model-mode", (ModelModeRequest request, ModelReplay replay) =>
        {
            replay.Mode = request.Mode;
            return Results.Ok(new { mode = replay.Mode.ToString() });
        });

        app.MapGet("/demo/model-mode", (ModelReplay replay) => Results.Ok(new { mode = replay.Mode.ToString() }));

        app.MapPost("/demo/reset", async (RunCoordinator runs, AgentDb db, ShopClient shop, ILoggerFactory logs, CancellationToken ct) =>
        {
            await runs.StopAllAsync();
            await db.Ledger.ExecuteDeleteAsync(ct);
            await db.Proposals.ExecuteDeleteAsync(ct);
            await db.Orders.ExecuteDeleteAsync(ct);
            await db.Runs.ExecuteDeleteAsync(ct);
            await db.CheckIns.ExecuteDeleteAsync(ct);
            await db.Memory.ExecuteDeleteAsync(ct);
            await db.Snapshots.ExecuteDeleteAsync(ct);
            await db.Settings.ExecuteDeleteAsync(ct);
            await AgentState.GetSettingsAsync(db, ct);

            try
            {
                await shop.ResetAsync(ct);
            }
            catch (Exception ex)
            {
                logs.CreateLogger("Demo").LogWarning(ex, "Could not reset the shop");
            }

            return Results.NoContent();
        });
    }
}

public sealed record ModelModeRequest([property: System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ReplayMode>))] ReplayMode Mode);
