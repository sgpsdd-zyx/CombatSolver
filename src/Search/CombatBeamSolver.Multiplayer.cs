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
        comparison = b.AllEnemiesDead.CompareTo(a.AllEnemiesDead);
        if (comparison != 0) return comparison;
        if (a.AllEnemiesDead && b.AllEnemiesDead)
        {
            comparison = Nullable.Compare(a.CombatEndedTurn, b.CombatEndedTurn);
            if (comparison != 0) return comparison;
        }
        // A partially searched first turn cannot outrank defense verified through the horizon.
        comparison = (b.BoundaryReason == SearchBoundaryReason.AdvisoryHorizon)
            .CompareTo(a.BoundaryReason == SearchBoundaryReason.AdvisoryHorizon);
        if (comparison != 0) return comparison;
        comparison = a.CumulativePlayerHpLost.CompareTo(b.CumulativePlayerHpLost);
        if (comparison != 0) return comparison;
        comparison = a.EnemyHp.CompareTo(b.EnemyHp);
        if (comparison != 0) return comparison;
        comparison = b.TeamSurvivors.CompareTo(a.TeamSurvivors);
        if (comparison != 0) return comparison;
        comparison = left.PotionCount.CompareTo(right.PotionCount);
        if (comparison != 0) return comparison;
        comparison = right.Score.CompareTo(left.Score);
        return comparison != 0 ? comparison : left.ActionCount.CompareTo(right.ActionCount);
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
