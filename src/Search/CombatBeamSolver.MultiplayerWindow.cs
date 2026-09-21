using System.Diagnostics;
using System.Text;

namespace CombatSolver;

internal sealed partial class CombatBeamSolver
{
    private sealed record MultiplayerWindowDecision(string Reason, int BaselineCycles, int ComparisonCycles,
        int RequiredRepresentatives, int CoveredRepresentatives, int PendingEligibility,
        int SuppressedAncestors, int MetadataWork, double ElapsedMilliseconds);

    private sealed class MultiplayerWindowWork(long remainingMilliseconds, CancellationToken cancellation)
    {
        private readonly long _started = Stopwatch.GetTimestamp();
        // A metadata ceiling, not additional search nodes or a per-representative allowance.
        private const int MaximumWork = 65_536;
        public int Used { get; private set; }
        public bool Exhausted { get; private set; }
        public double ElapsedMilliseconds => Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
        public double RemainingMilliseconds => Math.Max(0, remainingMilliseconds - ElapsedMilliseconds);

        public bool Spend(int amount = 1)
        {
            cancellation.ThrowIfCancellationRequested();
            if (amount > MaximumWork - Used || RemainingMilliseconds <= 0)
            { Exhausted = true; return false; }
            Used += amount;
            return true;
        }
    }

    private sealed record MultiplayerWindowPath(int Id, int FirstTurnId);

    private MultiplayerFinalBatch PrepareMultiplayerPublicationCandidates(IEnumerable<SearchNode> nodes,
        IReadOnlyList<SearchNode>? retainedScope, long elapsedMilliseconds)
    {
        var work = new MultiplayerWindowWork(
            Math.Max(0, (long)_profile.SoftTimeBudgetMilliseconds - elapsedMilliseconds), cancellationToken);
        SearchNode[] raw = nodes.ToArray();
        MultiplayerFinalBatch baseline = PrepareMultiplayerFinalCandidates(raw);
        int required = 0, coveredCount = 0, pending = 0, suppressed = 0;
        MultiplayerWindowDecision Decision(string reason, int depth) => new(reason,
            baseline.Ordering.EnemyCycles, depth, required, coveredCount, pending,
            suppressed, work.Used, work.ElapsedMilliseconds);
        MultiplayerFinalBatch KeepBaseline(string reason) => baseline with
        { Window = Decision(reason, baseline.Ordering.EnemyCycles) };

        if (policy.Multiplayer?.UseCoveredWindowSelection != true) return KeepBaseline("disabled");
        if (baseline.Candidates.Count == 0) return KeepBaseline("no_eligible_route");
        if (baseline.Ordering.EnemyCycles < 1) return KeepBaseline("first_cycle_incomplete");
        if (!work.Spend()) return KeepBaseline("metadata_budget");

        List<SearchNode> eligible = [];
        HashSet<SearchNode> seen = new(ReferenceEqualityComparer.Instance);
        Dictionary<SearchNode, bool> eligibility = new(ReferenceEqualityComparer.Instance);
        bool Eligible(SearchNode node)
        {
            if (!eligibility.TryGetValue(node, out bool value))
                eligibility.Add(node, value = IsEligibleMultiplayerFinal(node));
            return value;
        }
        foreach (SearchNode node in raw)
        {
            if (!work.Spend()) return KeepBaseline("metadata_budget");
            if (seen.Add(node) && Eligible(node)) eligible.Add(node);
        }
        if (eligible.All(IsMultiplayerTerminal)) return KeepBaseline("all_terminal");
        if (eligible.Max(node => node.Snapshot.AdvisoryLastEnemyCycle?.Cycle ?? 0)
            <= baseline.Ordering.EnemyCycles) return KeepBaseline("no_deeper_evidence");

        // The scope is frozen before the 4B cut. Still-retained prefixes cannot disappear
        // just because they were absent from the published (for example, end-turn) pool.
        List<SearchNode> scope = [];
        seen.Clear();
        foreach (SearchNode node in raw.Concat(retainedScope ?? []))
        {
            if (!work.Spend()) return KeepBaseline("metadata_budget");
            if (!seen.Add(node)) continue;
            scope.Add(node);
            if (!Eligible(node) && !node.IsTerminal) pending++;
            if (!IsMultiplayerTerminal(node)
                && node.BoundaryReason is not (SearchBoundaryReason.None or SearchBoundaryReason.AdvisoryHorizon))
                return KeepBaseline("blocked_continuation");
        }
        if (pending > 0) return KeepBaseline("pending_eligibility");

        Dictionary<SearchNode, MultiplayerWindowPath> paths = new(ReferenceEqualityComparer.Instance);
        Dictionary<(int Parent, string Action), int> interned = [];
        List<int> pathParents = [-1];
        SearchNode? requestRoot = null;
        string? rootChoices = null;
        string pathFailure = "metadata_budget";

        bool ReadPath(SearchNode node)
        {
            List<SearchNode> missing = [];
            for (SearchNode? cursor = node; cursor != null && !paths.ContainsKey(cursor); cursor = cursor.Parent)
            {
                if (!work.Spend()) return false;
                missing.Add(cursor);
            }
            for (int index = missing.Count - 1; index >= 0; index--)
            {
                if (!work.Spend()) return false;
                SearchNode current = missing[index];
                if (current.Parent == null)
                {
                    string? choices = MultiplayerWindowActionKey(null, current.TurnSetupChoices, work);
                    if (choices == null) return false;
                    if (current.Action != null || current.ActionCount != 0
                        || current.Snapshot.AdvisoryEnemyCycles != 0 || current.Turn != _startTurnNumber
                        || requestRoot != null && (!ReferenceEquals(requestRoot.Snapshot, current.Snapshot)
                            || rootChoices != choices))
                    { pathFailure = "root_or_path_mismatch"; return false; }
                    requestRoot = current;
                    rootChoices = choices;
                    paths.Add(current, new(0, 0));
                    continue;
                }
                MultiplayerWindowPath parent = paths[current.Parent];
                if (current.Action is not { } action || current.ActionCount != current.Parent.ActionCount + 1)
                { pathFailure = "root_or_path_mismatch"; return false; }
                string? key = MultiplayerWindowActionKey(action, current.TurnSetupChoices, work);
                if (key == null) return false;
                if (!interned.TryGetValue((parent.Id, key), out int id))
                {
                    id = pathParents.Count;
                    interned.Add((parent.Id, key), id);
                    pathParents.Add(parent.Id);
                }
                int firstTurn = parent.FirstTurnId;
                if (firstTurn == 0)
                {
                    if (action.Turn != _startTurnNumber)
                    { pathFailure = "first_turn_incomplete"; return false; }
                    bool ended = action.Kind == PlanActionKind.EndTurn || action.EndsPlayerTurn
                        || current.Turn > _startTurnNumber || IsMultiplayerTerminal(current);
                    bool observed = current.Outcome?.Turn == _startTurnNumber
                        || current.Snapshot.Turn > _startTurnNumber || IsMultiplayerTerminal(current)
                        || current.Snapshot.AdvisoryEnemyCycles > 0;
                    if (ended && observed) firstTurn = id;
                }
                paths.Add(current, new(id, firstTurn));
            }
            return true;
        }

        HashSet<int> representatives = [];
        foreach (SearchNode node in scope.Where(Eligible))
        {
            if (!ReadPath(node)) return KeepBaseline(pathFailure);
            MultiplayerWindowPath path = paths[node];
            if (path.FirstTurnId == 0) return KeepBaseline("first_turn_incomplete");
            if (!IsMultiplayerTerminal(node) && node.Snapshot.AdvisoryLastEnemyCycle == null)
                return KeepBaseline("first_cycle_incomplete");
            representatives.Add(path.FirstTurnId);
            required = representatives.Count;
            if (required > _profile.BeamWidth) return KeepBaseline("representative_limit");
        }
        if (required < 2) return KeepBaseline("one_representative");

        HashSet<int> extended = [];
        foreach (SearchNode node in scope.Where(Eligible))
        {
            for (int parent = pathParents[paths[node].Id]; parent > 0 && extended.Add(parent);
                parent = pathParents[parent])
                if (!work.Spend()) return KeepBaseline("metadata_budget");
        }
        List<SearchNode> maximal = eligible.Where(node => !extended.Contains(paths[node].Id)).ToList();
        suppressed = eligible.Count - maximal.Count;
        Dictionary<int, int> depths = [];
        foreach (SearchNode node in maximal)
        {
            if (!work.Spend()) return KeepBaseline("metadata_budget");
            int id = paths[node].FirstTurnId;
            int depth = IsMultiplayerTerminal(node) ? policy.Multiplayer.Horizon
                : node.Snapshot.AdvisoryLastEnemyCycle?.Cycle ?? 0;
            depths[id] = Math.Max(depths.GetValueOrDefault(id), depth);
        }
        coveredCount = representatives.Count(id => depths.GetValueOrDefault(id) > 0);
        if (coveredCount != required) return KeepBaseline("missing_representative");
        int commonDepth = Math.Min(policy.Multiplayer.Horizon, representatives.Min(id => depths[id]));
        if (commonDepth <= baseline.Ordering.EnemyCycles) return KeepBaseline("coverage_not_deeper");
        List<SearchNode> candidates = maximal.Where(node => IsMultiplayerTerminal(node)
            || node.Snapshot.AdvisoryLastEnemyCycle?.Cycle >= commonDepth).ToList();
        Dictionary<SearchNode, int> costs = new(ReferenceEqualityComparer.Instance);
        foreach (SearchNode node in candidates)
        {
            if (!TryMultiplayerWindowActionCost(node, commonDepth, work, out int cost))
                return KeepBaseline(work.Exhausted ? "metadata_budget" : "cycle_boundary_missing");
            costs.Add(node, cost);
        }
        // Charge the sort before entering its comparer; cancellation must not be wrapped by List.Sort.
        if (!work.Spend(checked(candidates.Count * (1 + (int)Math.Log2(Math.Max(1, candidates.Count))))))
            return KeepBaseline("metadata_budget");
        var ordering = new MultiplayerPlanOrdering(commonDepth, (left, right) =>
        {
            int comparison = CompareMultiplayerQualityAtCycle(left, right, commonDepth);
            return comparison != 0 ? comparison : costs[left].CompareTo(costs[right]);
        });
        candidates.Sort(ordering.Compare);
        if (!work.Spend()) return KeepBaseline("metadata_budget");
        SearchNode best = candidates[0], incumbent = baseline.Candidates[0];
        if (CompareMultiplayerKnownRisk(best, incumbent) > 0) return KeepBaseline("known_risk_regression");

        // Materialization replays the route and its continuation prefixes. Account for both
        // from remaining request time; observed average cost is an estimate, not a hard deadline.
        long replayActions = 2L * best.ActionCount;
        HashSet<int> futureTurns = [];
        for (SearchNode? cursor = best; cursor?.Action is { } action; cursor = cursor.Parent)
        {
            if (!work.Spend()) return KeepBaseline("metadata_budget");
            if ((action.Kind == PlanActionKind.EndTurn || action.EndsPlayerTurn)
                && !IsMultiplayerTerminal(cursor) && cursor.Snapshot.BoundaryReason == SearchBoundaryReason.None
                && futureTurns.Contains(cursor.Turn)) replayActions += cursor.ActionCount;
            futureTurns.Add(action.Turn);
        }
        double replayReserve = replayActions * Math.Max(1d,
            (double)elapsedMilliseconds / Math.Max(1, _run.TransitionCount)) * 2;
        if (work.RemainingMilliseconds < replayReserve) return KeepBaseline("replay_budget");
        int limit = _profile.BeamWidth * 4;
        if (candidates.Count > limit) candidates.RemoveRange(limit, candidates.Count - limit);
        return new(candidates, ordering, Decision("covered", commonDepth));
    }

    private static bool IsMultiplayerTerminal(SearchNode node)
        => node.Snapshot.AllEnemiesDead || node.Snapshot.PlayerDead;

    private int CompareMultiplayerKnownRisk(SearchNode left, SearchNode right)
    {
        int comparison = left.Snapshot.PlayerDead.CompareTo(right.Snapshot.PlayerDead);
        if (comparison != 0) return comparison;
        comparison = left.Snapshot.DeathSaveUseCount.CompareTo(right.Snapshot.DeathSaveUseCount);
        if (comparison != 0) return comparison;
        int allowance = policy.Multiplayer!.AcceptableHpLossPerTurn;
        return left.AdvisoryHpLoss.ExcessHpLost(allowance).CompareTo(right.AdvisoryHpLoss.ExcessHpLost(allowance));
    }

    private static bool TryMultiplayerWindowActionCost(SearchNode node, int depth,
        MultiplayerWindowWork work, out int cost)
    {
        cost = node.ActionCount;
        if (IsMultiplayerTerminal(node)) return work.Spend();
        SearchNode boundary = node;
        while (boundary.Parent is { } parent && parent.Snapshot.AdvisoryEnemyCycles >= depth)
        {
            if (!work.Spend()) return false;
            boundary = parent;
        }
        if (!work.Spend() || depth < 1 || boundary.Snapshot.AdvisoryLastEnemyCycle?.Cycle != depth
            || boundary.Parent?.Snapshot.AdvisoryEnemyCycles != depth - 1) return false;
        cost = boundary.ActionCount;
        return true;
    }

    private static string? MultiplayerWindowActionKey(PlanAction? action,
        IReadOnlyList<PlanCardChoice>? setup, MultiplayerWindowWork work)
    {
        StringBuilder key = new();
        bool valid = true;
        void Number(int value) { if (valid && (valid = work.Spend())) key.Append(value).Append(';'); }
        void Text(string value)
        {
            if (valid && (valid = work.Spend(value.Length + 1)))
                key.Append(value.Length).Append(':').Append(value).Append(';');
        }
        void Choice(PlanCardChoice choice)
        {
            Number((int)choice.Effect); Number((int)choice.SourcePile); Number((int)choice.Timing);
            Text(choice.SourceId); Text(choice.ContextId); Number(choice.Cards.Count);
            foreach (PlanCardToken card in choice.Cards)
            {
                if (!valid) break;
                Text(card.CardId); Number(card.UpgradeLevel); Text(card.StateKey);
                Number(card.SourceOccurrence); Number(card.OptionOccurrence);
            }
        }
        void Choices(IReadOnlyList<PlanCardChoice>? choices)
        {
            Number(choices?.Count ?? 0);
            if (choices == null) return;
            foreach (PlanCardChoice choice in choices) { if (!valid) break; Choice(choice); }
        }
        Choices(setup);
        if (action != null)
        {
            Number((int)action.Kind); Number(action.Turn); Text(action.CardId);
            Number(action.CardOccurrence); Number(action.TargetIndex); Text(action.TargetCombatId?.ToString() ?? "");
            Text(action.CardStateKey); Number(action.CardStateOccurrence); Number(action.CardUpgradeLevel);
            Text(action.CardEnchantmentId); Number(action.PotionSlot); Text(action.PotionId);
            Number(action.EndsPlayerTurn ? 1 : 0); Number(action.ReplayCount);
            Number(action.NestedChoicesBeforePrimary); Number(action.Choice == null ? 0 : 1);
            if (action.Choice != null) Choice(action.Choice);
            Choices(action.NestedChoices); Choices(action.TurnStartChoices);
        }
        return valid ? key.ToString() : null;
    }
}
