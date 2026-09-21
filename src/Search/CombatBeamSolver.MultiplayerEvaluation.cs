using CombatSolver.Engine.InCombat.Simulation;

namespace CombatSolver;

internal sealed partial class CombatBeamSolver
{
    private sealed record MultiplayerPlanOrdering(int EnemyCycles, Comparison<SearchNode> Compare);

    private void CaptureMultiplayerCycle(CombatPredictionSimulator simulator, SimulatedCombatState combat)
    {
        var player = simulator.State.GetCreature(_player.Creature);
        combat.AdvisorLastEnemyCycle = new MultiplayerCycleCheckpoint(
            combat.AdvisorEnemyCycles, player.CurrentHp, combat.GetCumulativeHpLost(_player.Creature),
            combat.DeathSaveUseCount,
            combat.KnownEnemies.Sum(enemy => combat.EffectiveEnemyHp(enemy, simulator.State.GetCreature(enemy))),
            combat.Players.Count(peer => simulator.State.GetCreature(peer.Creature).IsAlive),
            combat.PotionUses.Count, combat.AdvisorLastEnemyCycle);
    }

    private MultiplayerPlanOrdering CreateMultiplayerOrdering(IReadOnlyList<SearchNode> nodes)
    {
        // Freeze one depth for the whole batch; pairwise minimum depths are not transitive.
        int depth = int.MaxValue;
        foreach (SearchNode node in nodes)
        {
            if (node.Snapshot.AllEnemiesDead || node.Snapshot.PlayerDead
                || node.BoundaryReason is not (SearchBoundaryReason.None or SearchBoundaryReason.AdvisoryHorizon))
                continue;
            if (node.Snapshot.AdvisoryLastEnemyCycle is { } checkpoint)
                depth = Math.Min(depth, checkpoint.Cycle);
        }
        if (depth == int.MaxValue)
        {
            // External choices do not hold back other candidates. If every route is blocked,
            // their last observed checkpoint still describes a conditional, unfinished result.
            depth = nodes.Where(node => !node.Snapshot.AllEnemiesDead && !node.Snapshot.PlayerDead)
                .Select(node => node.Snapshot.AdvisoryLastEnemyCycle?.Cycle ?? 0)
                .Where(cycle => cycle > 0).DefaultIfEmpty(0).Min();
            if (depth == 0 && nodes.Count > 0 && nodes.All(node => node.Snapshot.AllEnemiesDead || node.Snapshot.PlayerDead))
                depth = policy.Multiplayer!.Horizon;
        }
        int commonDepth = depth;
        return new(commonDepth, (left, right) => CompareMultiplayerAtCycle(left, right, commonDepth));
    }

    private readonly record struct MultiplayerPlanFacts(bool Comparable, bool Won, int Hp, int HpLost,
        int DeathSaves, int ExcessHpLost, int EnemyHp, int TeamSurvivors, int Potions);

    private MultiplayerPlanFacts MultiplayerFactsAt(SearchNode node, int depth)
    {
        SimulationSnapshot snapshot = node.Snapshot;
        bool terminal = snapshot.AllEnemiesDead || snapshot.PlayerDead;
        MultiplayerCycleCheckpoint? checkpoint = snapshot.AdvisoryLastEnemyCycle;
        while (checkpoint != null && checkpoint.Cycle > depth) checkpoint = checkpoint.Previous;
        if (!terminal && depth > 0 && checkpoint?.Cycle == depth)
        {
            int excess = 0;
            for (MultiplayerCycleCheckpoint? cycle = checkpoint; cycle != null; cycle = cycle.Previous)
            {
                int loss = cycle.HpLost - (cycle.Previous?.HpLost ?? -snapshot.AdvisoryRootHpLost);
                excess = checked(excess + Math.Max(0, loss - policy.Multiplayer!.AcceptableHpLossPerTurn));
            }
            return new(true, false, checkpoint.Hp, checkpoint.HpLost, checkpoint.DeathSaves,
                excess, checkpoint.EnemyHp, checkpoint.TeamSurvivors, checkpoint.Potions);
        }
        return new(terminal, snapshot.AllEnemiesDead, snapshot.PlayerHp, snapshot.CumulativePlayerHpLost,
            snapshot.DeathSaveUseCount, node.AdvisoryHpLoss.ExcessHpLost(policy.Multiplayer!.AcceptableHpLossPerTurn),
            snapshot.EnemyHp, snapshot.TeamSurvivors, node.PotionCount);
    }

    private int CompareMultiplayerAtCycle(SearchNode left, SearchNode right, int depth)
    {
        int comparison = CompareMultiplayerQualityAtCycle(left, right, depth);
        return comparison != 0 ? comparison : left.ActionCount.CompareTo(right.ActionCount);
    }

    private int CompareMultiplayerQualityAtCycle(SearchNode left, SearchNode right, int depth)
    {
        bool forcedA = _potionStrategy.EvaluateForcedUses(left.Actions, root.HasRenewablePotionShapedRock).AllForcedUsesSatisfied;
        bool forcedB = _potionStrategy.EvaluateForcedUses(right.Actions, root.HasRenewablePotionShapedRock).AllForcedUsesSatisfied;
        int comparison = forcedB.CompareTo(forcedA);
        if (comparison != 0) return comparison;
        comparison = left.Snapshot.PlayerDead.CompareTo(right.Snapshot.PlayerDead);
        if (comparison != 0) return comparison;
        MultiplayerPlanFacts a = MultiplayerFactsAt(left, depth), b = MultiplayerFactsAt(right, depth);
        comparison = b.Comparable.CompareTo(a.Comparable);
        if (comparison != 0) return comparison;
        // Later observations may refute this particular continuation. Never hide that risk
        // behind its earlier checkpoint, or transfer it to every route with the same first card.
        comparison = Math.Max(a.DeathSaves, left.Snapshot.DeathSaveUseCount)
            .CompareTo(Math.Max(b.DeathSaves, right.Snapshot.DeathSaveUseCount));
        if (comparison != 0) return comparison;
        int allowance = policy.Multiplayer!.AcceptableHpLossPerTurn;
        comparison = Math.Max(a.ExcessHpLost, left.AdvisoryHpLoss.ExcessHpLost(allowance))
            .CompareTo(Math.Max(b.ExcessHpLost, right.AdvisoryHpLoss.ExcessHpLost(allowance)));
        if (comparison != 0) return comparison;
        comparison = b.Won.CompareTo(a.Won);
        if (comparison != 0) return comparison;
        if (!a.Comparable)
            return right.Score.CompareTo(left.Score);
        comparison = Math.Min(b.TeamSurvivors, right.Snapshot.TeamSurvivors)
            .CompareTo(Math.Min(a.TeamSurvivors, left.Snapshot.TeamSurvivors));
        if (comparison != 0) return comparison;
        comparison = a.EnemyHp.CompareTo(b.EnemyHp);
        if (comparison != 0) return comparison;
        comparison = a.HpLost.CompareTo(b.HpLost);
        if (comparison != 0) return comparison;
        comparison = b.Hp.CompareTo(a.Hp);
        if (comparison != 0) return comparison;
        comparison = a.Potions.CompareTo(b.Potions);
        if (comparison != 0) return comparison;
        if (a.Won && b.Won)
        {
            comparison = Nullable.Compare(left.Snapshot.CombatEndedTurn, right.Snapshot.CombatEndedTurn);
            if (comparison != 0) return comparison;
        }
        // Exploration estimates never replace an observed enemy-cycle result.
        return 0;
    }
}
