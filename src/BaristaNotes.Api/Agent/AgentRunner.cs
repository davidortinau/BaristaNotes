using System.Text.Json;
using BaristaNotes.Agent.Contracts;
using BaristaNotes.Api.Data;
using BaristaNotes.Api.Shop;
using Microsoft.EntityFrameworkCore;

namespace BaristaNotes.Api.Agent;

/// <summary>
/// One agent run: check what is finished, learn preferences, read catalogs,
/// filter in code, rank with the model, check the evidence, then act within
/// the autonomy level. Every external effect has a ledger entry before and
/// after it, so a stop or a crash shows what already happened.
/// </summary>
public sealed class AgentRunner(
    IDbContextFactory<AgentDb> dbs,
    LedgerWriter ledger,
    ShopClient shop,
    IReorderModel model,
    OrderRecorder orders,
    TimeProvider time,
    ILogger<AgentRunner> log)
{
    public const int MaxRoastAgeDays = 21;

    sealed class Progress
    {
        public int RoastersChecked;
        public int Skipped;
        public int Picks;
        public readonly List<string> Outcomes = [];
        public readonly List<string> Effects = [];
        public string? InFlight;
    }

    public async Task RunAsync(Guid runId, CancellationToken ct)
    {
        await using var db = await dbs.CreateDbContextAsync(CancellationToken.None);
        var run = await db.Runs.FindAsync([runId], CancellationToken.None)
                  ?? throw new InvalidOperationException($"Run {runId} not found.");
        var progress = new Progress();

        using var scope = RunContext.Enter(new RunScope(runId, ledger));
        try
        {
            await ExecuteAsync(db, run, progress, ct);
            run.Status = RunStatus.Completed;
            run.Summary = Summary(progress);
            await Write(run, LedgerKind.Completed, "Run finished.", run.Summary);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            run.Status = RunStatus.Stopped;
            run.Summary = "Stopped by you. " + Summary(progress);
            var already = progress.Effects.Count == 0
                ? "Nothing was changed in any shop."
                : "Already done: " + string.Join("; ", progress.Effects) + ".";
            if (progress.InFlight is not null)
            {
                already += $" {progress.InFlight} was in progress when you stopped. Check the shop.";
            }

            await Write(run, LedgerKind.Stopped, "Stopped by you.", already);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Run {RunId} failed", runId);
            run.Status = RunStatus.Failed;
            run.Summary = "Failed: " + ex.Message;
            var detail = progress.Effects.Count == 0
                ? ex.Message
                : ex.Message + " Already done: " + string.Join("; ", progress.Effects) + ".";
            await Write(run, LedgerKind.Failed, "Run failed.", detail);
        }
        finally
        {
            run.EndedAt = time.GetUtcNow().UtcDateTime;
            run.RoastersChecked = progress.RoastersChecked;
            run.Skipped = progress.Skipped;
            run.Picks = progress.Picks;
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    async Task ExecuteAsync(AgentDb db, RunRow run, Progress progress, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var settings = await AgentState.GetSettingsAsync(db, ct);
        var step = TimeSpan.FromMilliseconds(Math.Clamp(settings.StepDelayMs, 0, 10_000));
        var (_, budgetLeft) = await AgentState.BudgetAsync(db, settings, now, ct);

        await Write(run, LedgerKind.Started,
            $"Run started. Autonomy: {AgentState.Describe(settings.Autonomy)}.",
            $"{Money.Format(budgetLeft, settings.Currency)} of {Money.Format(settings.MonthlyBudget, settings.Currency)} left this month.");

        // 1. What needs replacing. Only bags you confirmed finished in a check-in count.
        var snapshotRow = await db.Snapshots.FindAsync([SnapshotRow.SingletonId], ct);
        var snapshot = AgentState.ReadSnapshot(snapshotRow);
        if (snapshotRow is null || snapshot is null)
        {
            await Write(run, LedgerKind.NeedsCheckIn, "Barista Notes has not shared any data yet.",
                "Open the app and run the agent from there.");
            progress.Outcomes.Add("no data");
            return;
        }

        var checkIns = await db.CheckIns.AsNoTracking().ToListAsync(ct);
        if (checkIns.Count == 0)
        {
            await Write(run, LedgerKind.NeedsCheckIn, "I don't know which bags you still have.",
                "Answer the check-in in the app, then run me again.");
            progress.Outcomes.Add("waiting for a check-in");
            return;
        }

        var beans = snapshot.Beans.ToDictionary(b => b.Id);
        var bags = snapshot.Bags.ToDictionary(b => b.Id);
        var forMe = snapshot.Shots.Where(s => s.ForMe).ToDictionary(s => s.Id);
        var finishedIds = checkIns.SelectMany(c => c.FinishedBagIds).ToHashSet();
        var orderedBags = (await db.Proposals.AsNoTracking()
                .Where(p => p.Status == ProposalStatus.Ordered)
                .Select(p => p.ForBagId)
                .ToListAsync(ct))
            .ToHashSet();

        var needs = finishedIds
            .Where(id => bags.ContainsKey(id) && !orderedBags.Contains(id))
            .Order()
            .Select(id => BuildNeed(bags[id], beans, snapshot.Shots))
            .ToList();

        if (needs.Count == 0)
        {
            var open = snapshot.Bags.Count(b => !b.Finished && !finishedIds.Contains(b.Id));
            await Write(run, LedgerKind.NothingToDo,
                $"You still have {open} open {(open == 1 ? "bag" : "bags")}. Nothing to order.",
                finishedIds.Count > 0 ? "Every finished bag already has an order." : null);
            progress.Outcomes.Add("nothing to order");
            return;
        }

        await Write(run, LedgerKind.Notice,
            $"Looking for a replacement for {string.Join(" and ", needs.Select(n => n.BeanName))}.",
            "You marked it finished in your check-in.");

        // 2. Learn preferences, or use what is remembered.
        var memory = await LearnAsync(db, run, snapshotRow, snapshot, forMe, ct);

        // 3. Read catalogs and filter in code.
        var shopRoasters = (await shop.GetRoastersAsync(ct)).ToDictionary(r => r.Handle);
        var neverRoasters = memory
            .Where(m => m.Kind == MemoryKind.NeverRoaster)
            .Select(m => m.Key.Split(':').Last())
            .ToHashSet();
        var openBags = snapshot.Bags.Where(b => !b.Finished && !finishedIds.Contains(b.Id)).ToList();
        var stillHave = openBags
            .Where(b => beans.ContainsKey(b.BeanId))
            .Select(b => ProductMatch.Key(beans[b.BeanId].Roaster, beans[b.BeanId].Name))
            .ToHashSet();
        var roasterHistory = snapshot.Beans
            .Where(b => !string.IsNullOrWhiteSpace(b.Roaster))
            .GroupBy(b => ProductMatch.Normalize(b.Roaster))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(b => b.Name).Distinct().ToList());
        var finishedLowRated = needs
            .Where(n => n.AverageRating < ReorderPolicy.FavoriteRating)
            .GroupBy(n => ProductMatch.Key(n.Roaster, n.BeanName))
            .ToDictionary(g => g.Key, g => g.First().AverageRating!.Value);
        var filter = new FilterContext(today, MaxRoastAgeDays, budgetLeft, MemoryRules.AvoidRules(memory), stillHave)
        {
            FinishedLowRated = finishedLowRated,
        };

        var candidates = new List<RankCandidate>();
        foreach (var handle in settings.WatchedRoasters)
        {
            await Task.Delay(step, time, ct);

            if (!shopRoasters.TryGetValue(handle, out var roaster))
            {
                await Write(run, LedgerKind.Notice, $"Roaster '{handle}' is not in the shop.", null, handle);
                continue;
            }

            if (neverRoasters.Contains(handle))
            {
                progress.Skipped++;
                await Write(run, LedgerKind.Skipped, roaster.Name, "You said never this roaster.", handle);
                continue;
            }

            var products = await shop.GetProductsAsync(handle, ct);
            var items = products.Select(p => ShopClient.ToCatalogItem(roaster, p)).OfType<CatalogItem>().ToList();
            progress.RoastersChecked++;
            await Write(run, LedgerKind.Checked, $"Checked {roaster.Name}: {items.Count} coffees.", null, handle);

            foreach (var item in items)
            {
                var reason = CandidateFilter.SkipReason(item, filter);
                if (reason is not null)
                {
                    progress.Skipped++;
                    await Write(run, LedgerKind.Skipped, item.Title, reason, handle);
                    continue;
                }

                var same = needs.FirstOrDefault(n => ProductMatch.Same(n.Roaster, n.BeanName, item.RoasterName, item.Title));
                candidates.Add(new RankCandidate(item, item.Describe(today), same?.BagId,
                    roasterHistory.GetValueOrDefault(ProductMatch.Normalize(item.RoasterName)) ?? []));
            }

            await SaveProgressAsync(db, run, progress);
        }

        // Something new: roasters you do not watch. Suggestion only.
        var discovery = new List<RankCandidate>();
        var unwatched = shopRoasters.Values
            .Where(r => !settings.WatchedRoasters.Contains(r.Handle) && !neverRoasters.Contains(r.Handle))
            .ToList();
        foreach (var roaster in unwatched)
        {
            ct.ThrowIfCancellationRequested();
            var products = await shop.GetProductsAsync(roaster.Handle, ct);
            discovery.AddRange(products
                .Select(p => ShopClient.ToCatalogItem(roaster, p))
                .OfType<CatalogItem>()
                .Where(item => CandidateFilter.SkipReason(item, filter) is null)
                .Select(item => new RankCandidate(item, item.Describe(today), null,
                    roasterHistory.GetValueOrDefault(ProductMatch.Normalize(item.RoasterName)) ?? [])));
        }

        if (unwatched.Count > 0)
        {
            await Write(run, LedgerKind.Notice,
                $"Also looked at {string.Join(" and ", unwatched.Select(r => r.Name))}, which you don't watch.",
                "Anything from there is a suggestion only.");
        }

        if (candidates.Count == 0)
        {
            await Write(run, LedgerKind.NothingToDo, "No coffee passed your filters.", "Nothing was changed in any shop.");
            progress.Outcomes.Add("nothing passed the filters");
            return;
        }

        // 4. Rank with the model, then check what it says.
        await Task.Delay(step, time, ct);
        await Write(run, LedgerKind.Ranking, $"Comparing {candidates.Count} coffees with what you like.",
            string.Join(", ", candidates.Select(c => c.Item.Title)) + ".");

        var citeable = CiteableShots(memory, needs, snapshot, forMe);
        var request = new RankRequest(
            memory.Where(m => m.Kind != MemoryKind.NeverRoaster)
                .OrderBy(m => m.Key, StringComparer.Ordinal)
                .Select(m => new RankMemory(m.Statement, m.Key, Citations(m.EvidenceJson).Select(c => c.ShotId).ToList()))
                .ToList(),
            needs,
            candidates,
            citeable)
        {
            Discovery = discovery,
        };

        var ranking = await model.RankAsync(request, ct);
        await ReportReplayAsync(run);
        var accepted = Accept(ranking.Picks, needs, candidates, forMe, out var ignoredPicks, out var cited, out var dropped);

        await Write(run, LedgerKind.EvidenceChecked,
            dropped == 0
                ? $"Checked {cited} citations against your notes. All found."
                : $"Dropped {dropped} of {cited} citations that do not match your notes.",
            ignoredPicks > 0 ? $"Ignored {ignoredPicks} picks that were not among the candidates." : null);

        foreach (var need in needs.Where(n => accepted.All(a => a.Need.BagId != n.BagId)))
        {
            await Write(run, LedgerKind.Notice, $"No good match for {need.BeanName}.", null);
        }

        // 5. Propose, then act within the autonomy level.
        foreach (var (need, candidate, pick, evidence) in accepted)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(step, time, ct);

            var item = candidate.Item;
            var sameBag = candidate.SameAsFinishedBagId == need.BagId;
            var proposal = new ProposalRow
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                ForBagId = need.BagId,
                ForBeanName = need.BeanName,
                Roaster = item.Roaster,
                RoasterName = item.RoasterName,
                ProductId = item.ProductId,
                VariantId = item.VariantId,
                ProductTitle = item.Title,
                ProductDetails = candidate.Description,
                Price = item.Price,
                Shipping = item.Shipping,
                Currency = item.Currency,
                Why = pick.Why.Trim(),
                EvidenceJson = AgentState.SerializeCitations(evidence),
                Unknowns = pick.Unknowns.Where(u => !string.IsNullOrWhiteSpace(u)).Take(3).ToList(),
                IsFavoriteReorder = sameBag && need.AverageRating >= ReorderPolicy.FavoriteRating,
                Status = ProposalStatus.Proposed,
                CreatedAt = time.GetUtcNow().UtcDateTime,
            };
            db.Proposals.Add(proposal);
            progress.Picks++;
            await SaveProgressAsync(db, run, progress);

            await Write(run, LedgerKind.Proposed,
                $"Pick for {need.BeanName}: {item.Title} from {item.RoasterName}.",
                $"{Money.Format(item.Price, item.Currency)} + {Money.Format(item.Shipping, item.Currency)} shipping. {proposal.Why}",
                item.Roaster, proposal.Id);

            (_, budgetLeft) = await AgentState.BudgetAsync(db, settings, time.GetUtcNow().UtcDateTime, ct);
            var decision = ReorderPolicy.Decide(new PolicyInput(
                settings.Autonomy,
                sameBag,
                need.AverageRating,
                candidate.KnownRoaster,
                item.Total,
                budgetLeft,
                orderedBags.Contains(need.BagId),
                settings.Currency));

            await Task.Delay(step, time, ct);
            switch (decision)
            {
                case PolicyDecision.Notify notify:
                    proposal.ActionNote = notify.Reason;
                    await db.SaveChangesAsync(CancellationToken.None);
                    await Write(run, LedgerKind.Notice, notify.Reason, "Open the pick to buy it.", item.Roaster, proposal.Id);
                    progress.Outcomes.Add("waiting for you");
                    break;

                case PolicyDecision.PrepareCart cart:
                    await PrepareCartAsync(db, run, proposal, cart.Reason, progress, ct);
                    progress.Outcomes.Add("cart ready");
                    break;

                case PolicyDecision.PlaceOrder:
                    if (await PlaceOrderAsync(db, run, proposal, budgetLeft, progress, ct))
                    {
                        progress.Outcomes.Add("ordered");
                        orderedBags.Add(need.BagId);
                    }
                    else
                    {
                        progress.Outcomes.Add("out of date");
                    }

                    break;
            }
        }

        // 6. A suggestion from a roaster you don't watch. Never a cart or an order.
        if (ranking.Suggestion is { } suggestion &&
            discovery.FirstOrDefault(d => d.Item.ProductId == suggestion.ProductId) is { } found)
        {
            await Task.Delay(step, time, ct);
            await Write(run, LedgerKind.Notice,
                $"Something new to try: {found.Item.Title} from {found.Item.RoasterName}.",
                $"{suggestion.Why.Trim()} Suggestion only: you don't watch {found.Item.RoasterName}, so I did not prepare a cart.",
                found.Item.Roaster);
        }
    }

    async Task<List<MemoryRow>> LearnAsync(
        AgentDb db, RunRow run, SnapshotRow snapshotRow, Snapshot snapshot,
        IReadOnlyDictionary<int, SnapshotShot> forMe, CancellationToken ct)
    {
        var rows = await db.Memory.ToListAsync(ct);
        var active = rows.Where(r => r.RemovedAt is null).OrderBy(r => r.Key, StringComparer.Ordinal).ToList();
        var guests = snapshot.Shots.Count - forMe.Count;

        if (snapshotRow.LearnedHash == snapshotRow.Hash && active.Any(r => r.Source == "learned"))
        {
            var remembered = active.Where(r => r.Kind != MemoryKind.NeverRoaster).ToList();
            await Write(run, LedgerKind.Learned, $"Using {remembered.Count} things I remember about you.",
                string.Join(" ", remembered.Select(r => r.Statement)));
            return active;
        }

        await Write(run, LedgerKind.Notice,
            $"Learning what you like from {forMe.Count} drinks.",
            guests > 0 ? $"{guests} drinks made for guests are left out." : null);

        var learned = await model.LearnAsync(new LearnRequest(
            snapshot.Beans,
            snapshot.Bags,
            forMe.Values.ToList(),
            rows.Where(r => r.RemovedAt is not null).Select(r => r.Key).Distinct().ToList()), ct);
        await ReportReplayAsync(run);

        var dropped = 0;
        var checkedItems = new List<LearnedPreference>();
        foreach (var item in learned)
        {
            var result = EvidenceValidator.ValidateClaim(item.Kind, item.Evidence, forMe);
            dropped += result.Dropped;
            checkedItems.Add(item with { Evidence = result.Valid });
        }

        var merge = MemoryRules.Merge(rows, checkedItems, time.GetUtcNow().UtcDateTime);
        db.Memory.RemoveRange(merge.ToDelete);
        db.Memory.AddRange(merge.ToAdd);
        snapshotRow.LearnedHash = snapshotRow.Hash;
        await db.SaveChangesAsync(CancellationToken.None);

        var notes = new List<string> { string.Join(" ", merge.ToAdd.Select(r => r.Statement)) };
        if (merge.Blocked > 0) notes.Add($"Left out {merge.Blocked} you removed earlier.");
        if (dropped > 0) notes.Add($"Dropped {dropped} citations that do not match your notes or ratings.");
        await Write(run, LedgerKind.Learned, $"Learned {merge.ToAdd.Count} preferences.", string.Join(" ", notes));

        return (await db.Memory.Where(r => r.RemovedAt == null).ToListAsync(ct))
            .OrderBy(r => r.Key, StringComparer.Ordinal)
            .ToList();
    }

    static RankNeed BuildNeed(SnapshotBag bag, IReadOnlyDictionary<int, SnapshotBean> beans, IReadOnlyList<SnapshotShot> shots)
    {
        beans.TryGetValue(bag.BeanId, out var bean);
        var rated = shots.Where(s => s.BagId == bag.Id && s.ForMe && s.Rating is not null).ToList();
        double? average = rated.Count == 0 ? null : rated.Average(s => s.Rating!.Value);
        var description = string.Join(". ", new[] { bean?.Origin, bean?.Notes }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new RankNeed(bag.Id, bean?.Name ?? $"Bag {bag.Id}", bean?.Roaster, description, average, rated.Count);
    }

    /// <summary>The drinks the ranker sees: memory evidence plus the finished bags' own drinks.</summary>
    static IReadOnlyList<RankShot> CiteableShots(
        IEnumerable<MemoryRow> memory, IEnumerable<RankNeed> needs, Snapshot snapshot, IReadOnlyDictionary<int, SnapshotShot> forMe)
    {
        var ids = memory.SelectMany(m => Citations(m.EvidenceJson)).Select(c => c.ShotId).ToHashSet();
        var needBags = needs.Select(n => n.BagId).ToHashSet();
        ids.UnionWith(forMe.Values.Where(s => needBags.Contains(s.BagId)).Select(s => s.Id));

        var bags = snapshot.Bags.ToDictionary(b => b.Id);
        var beans = snapshot.Beans.ToDictionary(b => b.Id);
        return ids
            .Where(forMe.ContainsKey)
            .Select(id => forMe[id])
            .Select(s => new RankShot(
                s.Id,
                bags.TryGetValue(s.BagId, out var bag) && beans.TryGetValue(bag.BeanId, out var bean) ? bean.Name : "unknown bean",
                s.DrinkType,
                s.Rating,
                s.TastingNotes,
                s.MadeAt))
            .ToList();
    }

    static List<(RankNeed Need, RankCandidate Candidate, ModelPick Pick, IReadOnlyList<CitedShot> Evidence)> Accept(
        IReadOnlyList<ModelPick> picks,
        IReadOnlyList<RankNeed> needs,
        IReadOnlyList<RankCandidate> candidates,
        IReadOnlyDictionary<int, SnapshotShot> forMe,
        out int ignored,
        out int cited,
        out int dropped)
    {
        var accepted = new List<(RankNeed, RankCandidate, ModelPick, IReadOnlyList<CitedShot>)>();
        var byBag = needs.ToDictionary(n => n.BagId);
        var byProduct = candidates.GroupBy(c => c.Item.ProductId).ToDictionary(g => g.Key, g => g.First());
        var usedProducts = new HashSet<long>();
        ignored = cited = dropped = 0;

        foreach (var pick in picks)
        {
            if (!byBag.TryGetValue(pick.ForBagId, out var need) ||
                !byProduct.TryGetValue(pick.ProductId, out var candidate) ||
                accepted.Any(a => a.Item1.BagId == pick.ForBagId) ||
                !usedProducts.Add(pick.ProductId))
            {
                ignored++;
                continue;
            }

            var check = EvidenceValidator.Validate(pick.Evidence, forMe);
            cited += check.Valid.Count + check.Dropped;
            dropped += check.Dropped;
            accepted.Add((need, candidate, pick, check.Valid));
        }

        // Keep the order of the needs, so a two-bag run is predictable on stage.
        return accepted.OrderBy(a => needs.ToList().FindIndex(n => n.BagId == a.Item1.BagId)).ToList();
    }

    async Task PrepareCartAsync(AgentDb db, RunRow run, ProposalRow proposal, string? reason, Progress progress, CancellationToken ct)
    {
        await Write(run, LedgerKind.CartCreating, $"Creating a cart at {proposal.RoasterName}.", null, proposal.Roaster, proposal.Id);

        progress.InFlight = $"A cart request to {proposal.RoasterName}";
        var cart = await shop.CreateCartAsync(proposal.Roaster, proposal.VariantId, proposal.Id, ct);
        progress.InFlight = null;

        proposal.CartId = cart.Id;
        proposal.CheckoutUrl = cart.CheckoutUrl;
        proposal.Status = ProposalStatus.CartReady;
        proposal.ActionNote = reason;
        await db.SaveChangesAsync(CancellationToken.None);
        progress.Effects.Add($"the cart at {proposal.RoasterName} is still open");

        await Write(run, LedgerKind.CartReady, $"Cart ready at {proposal.RoasterName}. Nothing charged.",
            reason ?? "Open the pick and tap Buy to check out.", proposal.Roaster, proposal.Id, cart.Id);
    }

    /// <summary>Returns false when the pick is out of date and nothing was ordered.</summary>
    async Task<bool> PlaceOrderAsync(AgentDb db, RunRow run, ProposalRow proposal, decimal budgetLeft, Progress progress, CancellationToken ct)
    {
        // The policy approved this exact price. Recheck it right before ordering.
        var now = await shop.GetVariantNowAsync(proposal.Roaster, proposal.ProductId, proposal.VariantId, ct);
        if (StaleCheck.Reason(proposal.Price, proposal.Currency, now) is { } stale)
        {
            proposal.Status = ProposalStatus.Stale;
            proposal.ActionNote = stale + " I did not order.";
            await db.SaveChangesAsync(CancellationToken.None);
            await Write(run, LedgerKind.Notice, "Did not order: the pick is out of date.", stale, proposal.Roaster, proposal.Id);
            return false;
        }

        var total = proposal.Price + proposal.Shipping;
        await Write(run, LedgerKind.Ordering, $"Ordering {proposal.ProductTitle} from {proposal.RoasterName}.",
            $"{Money.Format(total, proposal.Currency)} of {Money.Format(budgetLeft, proposal.Currency)} left. " +
            $"Idempotency-Key {proposal.Id.ToString()[..8]}.",
            proposal.Roaster, proposal.Id);

        progress.InFlight = $"An order request to {proposal.RoasterName}";
        var result = await shop.CreateOrderAsync(proposal.Roaster, proposal.VariantId, proposal.Id, ct);
        progress.InFlight = null;

        var record = await orders.RecordAsync(result.Order, reportedBy: "agent");
        await db.Entry(proposal).ReloadAsync(CancellationToken.None);
        progress.Effects.Add($"order {result.Order.OrderNumber} at {proposal.RoasterName}");

        if (result.Replayed)
        {
            await Write(run, LedgerKind.Ordered, $"The shop returned the existing order {result.Order.OrderNumber}.",
                "Same Idempotency-Key, so no second order. " +
                $"{Money.Format(record.BudgetLeft, proposal.Currency)} left this month.",
                proposal.Roaster, proposal.Id);
        }
        else if (!record.IsDuplicate)
        {
            await Write(run, LedgerKind.Ordered, $"Order {result.Order.OrderNumber} placed at {proposal.RoasterName}.",
                $"{Money.Format(result.Order.Total, result.Order.Currency)}. " +
                $"{Money.Format(record.BudgetLeft, proposal.Currency)} left this month.",
                proposal.Roaster, proposal.Id);
        }

        return true;
    }

    /// <summary>Says so in the ledger when the answer came from a rehearsal recording.</summary>
    async Task ReportReplayAsync(RunRow run)
    {
        if (RunContext.Value is { Flags.ReplayReason: { } reason } scope)
        {
            scope.Flags.ReplayReason = null;
            await Write(run, LedgerKind.Notice, "Used a rehearsal answer.",
                $"{reason} I used the answer recorded for this same request in rehearsal.");
        }
    }

    static IReadOnlyList<CitedShot> Citations(string json) =>
        JsonSerializer.Deserialize<List<CitedShot>>(json, AgentState.Json) ?? [];

    async Task SaveProgressAsync(AgentDb db, RunRow run, Progress progress)
    {
        run.RoastersChecked = progress.RoastersChecked;
        run.Skipped = progress.Skipped;
        run.Picks = progress.Picks;
        await db.SaveChangesAsync(CancellationToken.None);
    }

    Task Write(RunRow run, LedgerKind kind, string title, string? detail, string? roaster = null, Guid? proposalId = null, string? cartId = null) =>
        ledger.WriteAsync(run.Id, kind, title, detail, roaster, proposalId, cartId);

    static string Summary(Progress progress)
    {
        var picks = progress.Picks == 1 ? "1 pick" : $"{progress.Picks} picks";
        var outcome = progress.Outcomes.Count == 0 ? "" : ": " + string.Join(", ", progress.Outcomes.Distinct());
        return $"Checked {progress.RoastersChecked} roasters. Skipped {progress.Skipped}. {picks}{outcome}.";
    }
}
