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
        => CompareMultiplayerAtCycle(left, right, int.MaxValue);

    private sealed record MultiplayerFinalBatch(
        List<SearchNode> Candidates, MultiplayerPlanOrdering Ordering,
        MultiplayerWindowDecision? Window = null);

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
        eligible.Sort(ordering.Compare);
        int limit = _profile.BeamWidth * 4;
        if (eligible.Count > limit) eligible.RemoveRange(limit, eligible.Count - limit);
        return new(eligible, ordering);
    }

    private FinalPlanSelection SelectMultiplayerFinal(MultiplayerFinalBatch batch)
    {
        if (batch.Candidates.Count == 0)
            throw new PotionPolicyUnsatisfiedException("No advisory route satisfies the selected potion directives.");
        if (batch.Window is { } window)
            policy.Diagnostics.Debug($"[CombatSolver/Test] MULTIPLAYER_WINDOW reason={window.Reason} "
                + $"baseline={window.BaselineCycles} comparison={window.ComparisonCycles} "
                + $"required={window.RequiredRepresentatives} covered={window.CoveredRepresentatives} "
                + $"pending={window.PendingEligibility} ancestors={window.SuppressedAncestors} "
                + $"metadata_work={window.MetadataWork} elapsed_ms={window.ElapsedMilliseconds:0.###}");
        SearchNode best = batch.Candidates[0];
        return new FinalPlanSelection(new FinalPlanCandidate(best, best.Snapshot,
            SearchFeatures.Capture(best), best.FutureSoldHp,
            battleDamage.SoldHpCommitted + best.FutureSoldHp, best.PotionCount, best.Score), 0, 0, 0,
            batch.Ordering.EnemyCycles);
    }

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
                    node.AdvisoryHpLoss.CompletedExcessHpLost, node.AdvisoryHpLoss.CurrentCycleHpLost,
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
                living.OrderBy(node => node.AdvisoryHpLoss.ExcessHpLost(node.Snapshot.AdvisoryHpLossAllowance))
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
