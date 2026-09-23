using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace CombatSolver;

internal sealed partial class CombatBeamSolver
{
    internal SimulationSnapshot ReplayMultiplayerForTesting(IReadOnlyList<PlanAction> actions)
        => IsMultiplayerAdvice ? Replay(actions)
            : throw new InvalidOperationException("Multiplayer contract requires an advisory policy.");

    private int CompareMultiplayerPlans(SearchNode left, SearchNode right)
        => CompareMultiplayerAtCycle(left, right, _contributionObjective!.RemainingCycles);

    private sealed record MultiplayerFinalBatch(
        List<SearchNode> Candidates, MultiplayerPlanOrdering Ordering);

    private List<SearchNode> _contributionWitnesses = [];

    private bool IsEligibleMultiplayerFinal(SearchNode node)
        => (!_enforcePotionDirectives || _potionStrategy.EvaluateForcedUses(
                node.Actions, root.HasRenewablePotionShapedRock, _run.PotionStrategicCosts).AllForcedUsesSatisfied)
            && ExplicitPotionUseCount(node) >= _minimumPotionUses
            && (_potionPolicy != SolverPotionPolicy.RequireAtLeastOne || ExplicitPotionUseCount(node) > 0);

    private MultiplayerFinalBatch PrepareMultiplayerFinalCandidates(IEnumerable<SearchNode> nodes)
    {
        // Final eligibility must precede both the common-cycle decision and the 4B cut.
        // Expandable prefixes still use the unfiltered intermediate retention policy.
        List<SearchNode> eligible = nodes
            .Distinct((IEqualityComparer<SearchNode>)ReferenceEqualityComparer.Instance)
            .Where(IsEligibleMultiplayerFinal)
            .ToList();
        MultiplayerPlanOrdering ordering = CreateMultiplayerOrdering(eligible);
        int limit = _profile.BeamWidth * 4;
        eligible = RetainContributionFrontier(eligible, ordering, limit);
        return new(eligible, ordering);
    }

    private List<SearchNode> RetainContributionFrontier(List<SearchNode> nodes,
        MultiplayerPlanOrdering ordering, int limit)
    {
        nodes.Sort(ordering.Compare);
        List<SearchNode> frontier = [];
        foreach (SearchNode candidate in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MultiplayerPlanValue value = MultiplayerFactsAt(candidate, ordering.EnemyCycles);
            if (frontier.Any(other => (!other.HasPredictionRisk || candidate.HasPredictionRisk)
                && FirstCycleRisk(other) <= FirstCycleRisk(candidate)
                && (!policy.Multiplayer!.CreditSharedDamage
                    || MultiplayerObjectiveCost(other, MultiplayerFactsAt(other, ordering.EnemyCycles))
                        <= MultiplayerObjectiveCost(candidate, value))
                && MultiplayerTailValue(other, MultiplayerFactsAt(other, ordering.EnemyCycles))
                    >= MultiplayerTailValue(candidate, value)
                && MultiplayerQuotaSelection.Dominates(
                MultiplayerFactsAt(other, ordering.EnemyCycles), value, root.InitialPlayerHp, root.InitialPlayerMaxHp)))
                continue;
            frontier.RemoveAll(other => (!candidate.HasPredictionRisk || other.HasPredictionRisk)
                && FirstCycleRisk(candidate) <= FirstCycleRisk(other)
                && (!policy.Multiplayer!.CreditSharedDamage
                    || MultiplayerObjectiveCost(candidate, value)
                        <= MultiplayerObjectiveCost(other, MultiplayerFactsAt(other, ordering.EnemyCycles)))
                && MultiplayerTailValue(candidate, value)
                    >= MultiplayerTailValue(other, MultiplayerFactsAt(other, ordering.EnemyCycles))
                && MultiplayerQuotaSelection.Dominates(value,
                MultiplayerFactsAt(other, ordering.EnemyCycles), root.InitialPlayerHp, root.InitialPlayerMaxHp));
            frontier.Add(candidate);
            if (frontier.Count > limit)
            {
                var cheapest = frontier.OrderBy(node => MultiplayerFactsAt(node, ordering.EnemyCycles)
                    .HealthCost(root.InitialPlayerHp, root.InitialPlayerMaxHp)).First();
                var damage = frontier.MaxBy(node => MultiplayerFactsAt(node, ordering.EnemyCycles).Progress)!;
                var witness = frontier.FirstOrDefault(node =>
                    MultiplayerFactsAt(node, ordering.EnemyCycles).Progress >= _contributionObjective!.TargetDamage);
                var retained = frontier.Take(Math.Max(1, limit - 3)).ToHashSet(ReferenceEqualityComparer.Instance);
                retained.Add(cheapest); retained.Add(damage);
                if (witness != null) retained.Add(witness);
                frontier = frontier.Where(retained.Contains).Take(limit).ToList();
            }
        }
        frontier.Sort(ordering.Compare);
        return frontier;
    }

    private void PreserveContributionWitnesses(IEnumerable<SearchNode> nodes)
    {
        var candidates = nodes.Where(node => node.Snapshot.AdvisoryLastEnemyCycle != null
            || node.Snapshot.AllEnemiesDead).Concat(_contributionWitnesses)
            .Distinct((IEqualityComparer<SearchNode>)ReferenceEqualityComparer.Instance).Where(IsEligibleMultiplayerFinal).ToList();
        _contributionWitnesses = RetainContributionFrontier(candidates,
            CreateMultiplayerOrdering(candidates), _profile.BeamWidth * 4)
            .Select(node => node.Snapshot.HasSimulator
                ? node with { Snapshot = node.Snapshot.DetachForMultiplayerWitness() } : node).ToList();
    }

    private FinalPlanSelection SelectMultiplayerFinal(MultiplayerFinalBatch batch)
    {
        if (batch.Candidates.Count == 0)
            throw new PotionPolicyUnsatisfiedException("No advisory route satisfies the selected potion directives.");
        SearchNode best = batch.Candidates[0];
        _selectedContribution = MultiplayerFactsAt(best, batch.Ordering.EnemyCycles);
        _selectedContributionWitness = _selectedContribution.Comparable && !_selectedContribution.Dead
            && (_selectedContribution.Won || batch.Ordering.EnemyCycles == _contributionObjective!.RemainingCycles
                && _selectedContribution.Progress >= _contributionObjective.TargetDamage);
        _selectedQuotaFrontierCount = batch.Candidates.Count;
        _selectedSearchCycles = Volatile.Read(ref _maximumObservedEnemyCycles);
        if (batch.Ordering.EnemyCycles > 0 && !_selectedContribution.Won)
        {
            // The fixed deadline is the actionable output; later no-active-help risk is conditional.
            while (best.Parent is { } parent && parent.Snapshot.AdvisoryEnemyCycles >= batch.Ordering.EnemyCycles
                && IsEligibleMultiplayerFinal(parent))
                best = parent;
        }
        return new FinalPlanSelection(new FinalPlanCandidate(best, best.Snapshot,
            SearchFeatures.Capture(best), best.FutureSoldHp,
            battleDamage.SoldHpCommitted + best.FutureSoldHp, best.PotionCount, best.Score), 0, 0, 0,
            Math.Min(batch.Ordering.EnemyCycles, _selectedContribution.Won
                ? batch.Ordering.EnemyCycles : best.Snapshot.AdvisoryLastEnemyCycle?.Cycle ?? 0));
    }

    private MultiplayerPlanValue _selectedContribution;
    private bool _selectedContributionWitness;
    private int _selectedQuotaFrontierCount;
    private int _selectedSearchCycles;

    private sealed partial class BeamRetentionPolicy
    {
        private List<SearchNode> RankMultiplayerFinal(List<SearchNode> nodes, int limit)
        {
            MultiplayerPlanOrdering ordering = _advisoryOrdering!(nodes);
            nodes.Sort(ordering.Compare);
            return nodes.Take(limit).ToList();
        }

        private List<SearchNode> RankMultiplayer(IEnumerable<SearchNode> nodes, int limit, bool finalQualityFirst)
        {
            if (finalQualityFirst)
            {
                List<SearchNode> final = RankMultiplayerFinal(nodes.ToList(), limit);
                for (int index = 0; index < final.Count; index++) final[index].RetentionRank = index;
                return final;
            }
            var ranked = nodes.GroupBy(node => (node.StateKey,
                    node.Snapshot.AdvisoryLocalDamage, node.Snapshot.AdvisoryTotalDamage,
                    node.Snapshot.AdvisoryUnattributedDamage,
                    node.Snapshot.AdvisoryLastEnemyCycle))
                .Select(group => group.OrderByDescending(node => node.Score)
                    .ThenBy(node => node.Snapshot.CumulativePlayerHpLost).ThenBy(node => node.ActionCount).First())
                .ToList();
            SortByBeamRank(ranked);
            List<SearchNode> retained = [];
            HashSet<SearchNode> selected = new(ReferenceEqualityComparer.Instance);
            void Take(SearchNode? node)
            {
                if (node != null && retained.Count < limit && selected.Add(node)) retained.Add(node);
            }
            IEnumerable<SearchNode> living = ranked.Where(node => !node.Snapshot.PlayerDead);
            List<SearchNode>[] lanes =
            [
                living.OrderBy(node => node.Snapshot.CumulativePlayerHpLost)
                    .ThenByDescending(node => node.Snapshot.PlayerHp).ThenByDescending(node => node.Snapshot.PlayerBlock)
                    .ThenByDescending(node => node.Score).ToList(),
                living.OrderBy(node => node.Snapshot.EnemyHp).ThenByDescending(node => node.Score).ToList(),
                living.OrderByDescending(node => node.Snapshot.PersistentBuffValue)
                    .ThenByDescending(node => node.Snapshot.LatentSetupValue)
                    .ThenByDescending(node => node.Snapshot.ReachableHandValue).ThenByDescending(node => node.Score).ToList(),
            ];
            // One representative can cover several lanes. Do not replace that overlap with
            // a runner-up before the other lanes have had a seat in the same bounded beam.
            if (limit != 3) Take(ranked.FirstOrDefault());
            int offset = limit == 2 && ranked.Count > 0 ? ranked.Max(node => node.ActionCount) % lanes.Length : 0;
            for (int lane = 0; lane < lanes.Length; lane++)
                Take(lanes[(offset + lane) % lanes.Length].FirstOrDefault());
            int quota = Math.Max(1, limit / 4);
            for (int rank = 1; rank < quota && retained.Count < limit; rank++)
                foreach (List<SearchNode> lane in lanes) Take(lane.ElementAtOrDefault(rank));
            foreach (SearchNode node in ranked) Take(node);
            SortByBeamRank(retained);
            for (int index = 0; index < retained.Count; index++) retained[index].RetentionRank = index;
            return retained;
        }
    }

    private void SeedMultiplayerRoutes(List<SearchNode> frontier)
    {
        if (policy.Multiplayer?.PreviousRoutes is not { Count: > 0 } routes) return;
        SearchNode rootNode = frontier[0];
        long started = System.Environment.TickCount64;
        // Keep only pure action data across requests. Every reused edge pays for a new
        // simulation and contributes to this request's normal transition accounting.
        foreach (IReadOnlyList<PlanAction> route in routes.Take(4))
        {
            SearchNode node = rootNode;
            foreach (PlanAction action in route.Where(action => action.Turn >= _startTurnNumber).Take(32))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_run.Expanded >= _profile.MaxExpandedNodes
                    || System.Environment.TickCount64 - started >= _profile.SoftTimeBudgetMilliseconds)
                    break;
                if (action.Turn != _startTurnNumber || action.Kind == PlanActionKind.EndTurn
                    || action.EndsPlayerTurn || action.Choice != null
                    || action.NestedChoices is { Count: > 0 } || action.TurnStartChoices is { Count: > 0 })
                    break;
                if (!CanReplayMultiplayerAction(node, action)) break;
                _run.Expanded++;
                SimulationSnapshot snapshot = ReplayAction(node, action);
                var next = new SearchNode(action, node.ActionCount + 1, snapshot.PotionUseCount,
                    snapshot.PotionStrategicCost, snapshot.Turn, node.Traits, 0, snapshot.Score,
                    snapshot.StateKey, snapshot.HasRisk, snapshot.BoundaryReason,
                    snapshot.PlayerDead || snapshot.AllEnemiesDead || snapshot.BoundaryReason != SearchBoundaryReason.None,
                    node, snapshot, CombatProgressState.Capture(snapshot))
                { CumulativeEnemyHpLost = AccumulateEnemyHpLost(node, snapshot) };
                _run.ReplayedAdviceActions++;
                if (!ReferenceEquals(node, rootNode)) node.Snapshot.ReleaseSimulator();
                node = next;
                if (node.IsTerminal) break;
            }
            if (!ReferenceEquals(node, rootNode)) frontier.Add(node);
        }
    }

    private bool CanReplayMultiplayerAction(SearchNode node, PlanAction action)
    {
        var simulator = node.Snapshot.Simulator;
        var combat = (SimulatedCombatState)simulator.State.CombatState;
        Creature? target = combat.GetCreature(action.TargetCombatId);
        if (action.TargetCombatId != null && (target == null || !simulator.State.IsHittable(target)))
            return false;
        if (action.Kind == PlanActionKind.UsePotion)
        {
            var potion = combat.GetPotionAtSlot(_player, action.PotionSlot);
            return potion != null && potion.Id.Entry == action.PotionId
                && AllowsPotionUse(action.PotionSlot, potion.Id.Entry)
                && TargetsForPotion(potion, simulator).Any(item => ReferenceEquals(item.Target, target));
        }
        var card = FindCardForReplay(simulator.State.GetPlayerCombatState(_player).Hand.Cards, action);
        return card != null && combat.CanPlayCard(simulator, card)
            && TargetsFor(card, simulator).Any(item => ReferenceEquals(item.Target, target));
    }
}
