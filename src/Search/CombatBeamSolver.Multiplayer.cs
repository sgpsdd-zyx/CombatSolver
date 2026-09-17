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
    {
        SimulationSnapshot a = left.Snapshot, b = right.Snapshot;
        bool forcedA = _potionStrategy.EvaluateForcedUses(left.Actions, root.HasRenewablePotionShapedRock).AllForcedUsesSatisfied;
        bool forcedB = _potionStrategy.EvaluateForcedUses(right.Actions, root.HasRenewablePotionShapedRock).AllForcedUsesSatisfied;
        int comparison = forcedB.CompareTo(forcedA);
        if (comparison != 0) return comparison;
        comparison = a.PlayerDead.CompareTo(b.PlayerDead);
        if (comparison != 0) return comparison;
        // A short unfinished route has not established safety through the same window.
        bool completeA = a.AllEnemiesDead || a.BoundaryReason == SearchBoundaryReason.AdvisoryHorizon;
        bool completeB = b.AllEnemiesDead || b.BoundaryReason == SearchBoundaryReason.AdvisoryHorizon;
        comparison = completeB.CompareTo(completeA);
        if (comparison != 0) return comparison;
        if (!completeA)
        {
            comparison = b.AdvisoryEnemyCycles.CompareTo(a.AdvisoryEnemyCycles);
            if (comparison != 0) return comparison;
        }
        comparison = a.DeathSaveUseCount.CompareTo(b.DeathSaveUseCount);
        if (comparison != 0) return comparison;
        int allowance = policy.Multiplayer!.AcceptableHpLossPerTurn;
        comparison = left.AdvisoryHpLoss.ExcessHpLost(allowance)
            .CompareTo(right.AdvisoryHpLoss.ExcessHpLost(allowance));
        if (comparison != 0) return comparison;
        comparison = b.AllEnemiesDead.CompareTo(a.AllEnemiesDead);
        if (comparison != 0) return comparison;
        comparison = a.EnemyHp.CompareTo(b.EnemyHp);
        if (comparison != 0) return comparison;
        comparison = a.CumulativePlayerHpLost.CompareTo(b.CumulativePlayerHpLost);
        if (comparison != 0) return comparison;
        comparison = b.TeamSurvivors.CompareTo(a.TeamSurvivors);
        if (comparison != 0) return comparison;
        if (a.AllEnemiesDead && b.AllEnemiesDead)
        {
            comparison = Nullable.Compare(a.CombatEndedTurn, b.CombatEndedTurn);
            if (comparison != 0) return comparison;
        }
        comparison = left.PotionCount.CompareTo(right.PotionCount);
        if (comparison != 0) return comparison;
        comparison = right.Score.CompareTo(left.Score);
        return comparison != 0 ? comparison : left.ActionCount.CompareTo(right.ActionCount);
    }

    private sealed partial class BeamRetentionPolicy
    {
        private List<SearchNode> RankMultiplayer(IEnumerable<SearchNode> nodes, int limit, bool finalQualityFirst)
        {
            if (finalQualityFirst)
            {
                List<SearchNode> final = nodes.Order(Comparer<SearchNode>.Create(_advisoryComparison!)).Take(limit).ToList();
                for (int index = 0; index < final.Count; index++) final[index].RetentionRank = index;
                return final;
            }
            var ranked = nodes.GroupBy(node => (node.StateKey,
                    node.AdvisoryHpLoss.CompletedExcessHpLost, node.AdvisoryHpLoss.CurrentCycleHpLost))
                .Select(group => group.OrderByDescending(node => node.Score)
                    .ThenBy(node => node.Snapshot.CumulativePlayerHpLost).ThenBy(node => node.ActionCount).First())
                .ToList();
            SortByBeamRank(ranked);
            List<SearchNode> retained = [];
            HashSet<SearchNode> selected = new(ReferenceEqualityComparer.Instance);
            void Take(IEnumerable<SearchNode> lane, int count)
            {
                foreach (SearchNode node in lane)
                {
                    if (count == 0 || retained.Count == limit) break;
                    if (!selected.Add(node)) continue;
                    retained.Add(node);
                    count--;
                }
            }
            Take(ranked, 1);
            // Reserve bounded alternatives before filling ordinary score slots. All lanes
            // share the same beam width and request budget, and retain complete party states.
            IEnumerable<SearchNode> living = ranked.Where(node => !node.Snapshot.PlayerDead);
            int quota = Math.Max(1, limit / 4);
            Take(living.OrderBy(node => node.AdvisoryHpLoss.ExcessHpLost(node.Snapshot.AdvisoryHpLossAllowance))
                .ThenByDescending(node => node.Snapshot.PlayerHp).ThenByDescending(node => node.Snapshot.PlayerBlock)
                .ThenByDescending(node => node.Score), quota);
            Take(living.OrderBy(node => node.Snapshot.EnemyHp).ThenByDescending(node => node.Score), quota);
            Take(living.OrderByDescending(node => node.Snapshot.PersistentBuffValue)
                .ThenByDescending(node => node.Snapshot.LatentSetupValue)
                .ThenByDescending(node => node.Snapshot.ReachableHandValue).ThenByDescending(node => node.Score), quota);
            Take(ranked, limit);
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
