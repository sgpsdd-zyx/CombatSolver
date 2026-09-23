using CombatSolver.Engine.InCombat.Simulation;

namespace CombatSolver;

internal sealed partial class CombatBeamSolver
{
    private sealed record MultiplayerPlanOrdering(int EnemyCycles, Comparison<SearchNode> Compare);
    private int _maximumObservedEnemyCycles;
    private double MultiplayerScoredProgress(int enemyHp, long local, long total, long shared)
        => policy.Multiplayer!.CreditSharedDamage
            ? _contributionObjective!.SharedProgress(enemyHp, local, total, shared)
            : ContributionProgress(enemyHp, local, total);

    private void CaptureMultiplayerCycle(CombatPredictionSimulator simulator, SimulatedCombatState combat)
    {
        int observed = Volatile.Read(ref _maximumObservedEnemyCycles);
        while (combat.AdvisorEnemyCycles > observed)
        {
            int previous = Interlocked.CompareExchange(ref _maximumObservedEnemyCycles, combat.AdvisorEnemyCycles, observed);
            if (previous == observed) break;
            observed = previous;
        }
        var player = simulator.State.GetCreature(_player.Creature);
        combat.AdvisorLastEnemyCycle = new MultiplayerCycleCheckpoint(
            combat.AdvisorEnemyCycles, player.CurrentHp, combat.GetCumulativeHpLost(_player.Creature),
            combat.DeathSaveUseCount,
            combat.KnownEnemies.Sum(enemy => combat.EffectiveEnemyHp(enemy, simulator.State.GetCreature(enemy))),
            combat.Players.Count(peer => simulator.State.GetCreature(peer.Creature).IsAlive),
            combat.PotionUses.Count, combat.AdvisorLastEnemyCycle)
        {
            LocalDamage = combat.AdvisorLocalDamage, TotalDamage = combat.AdvisorTotalDamage,
            UnattributedDamage = combat.AdvisorUnattributedDamage,
            AliveEnemies = combat.KnownEnemies.Count(enemy => combat.ContainsCreature(enemy)
                && combat.EffectiveEnemyHp(enemy, simulator.State.GetCreature(enemy)) > 0),
            PotionCost = combat.PotionUses.Sum(use => use.StrategicHpCost), MaxHp = player.MaxHp,
        };
    }

    private MultiplayerPlanOrdering CreateMultiplayerOrdering(IReadOnlyList<SearchNode> nodes)
    {
        int deadline = _contributionObjective!.RemainingCycles;
        int depth = nodes.Select(node => node.Snapshot.AllEnemiesDead ? deadline
                : Math.Min(deadline, node.Snapshot.AdvisoryLastEnemyCycle?.Cycle ?? 0))
            .DefaultIfEmpty(0).Max();
        // One frozen deadline for the batch. A missing witness is unknown, never infeasible.
        return new(depth, (left, right) => CompareMultiplayerAtCycle(left, right, depth));
    }

    private MultiplayerPlanValue MultiplayerFactsAt(SearchNode node, int depth)
    {
        SimulationSnapshot snapshot = node.Snapshot;
        MultiplayerCycleCheckpoint? checkpoint = snapshot.AdvisoryLastEnemyCycle;
        while (checkpoint != null && checkpoint.Cycle > depth) checkpoint = checkpoint.Previous;
        bool earlyTerminal = (snapshot.AllEnemiesDead || snapshot.PlayerDead)
            && snapshot.AdvisoryEnemyCycles < depth;
        if (depth > 0 && checkpoint?.Cycle == depth && !earlyTerminal)
            return new(true, false, checkpoint.Hp <= 0, checkpoint.Hp, checkpoint.MaxHp,
                checkpoint.HpLost, checkpoint.DeathSaves, checkpoint.EnemyHp, checkpoint.AliveEnemies,
                checkpoint.TeamSurvivors, checkpoint.Potions, checkpoint.PotionCost,
                ContributionProgress(checkpoint.EnemyHp, checkpoint.LocalDamage, checkpoint.TotalDamage), depth)
            {
                LocalDamage = checkpoint.LocalDamage, TotalDamage = checkpoint.TotalDamage,
                UnattributedDamage = checkpoint.UnattributedDamage,
            };
        return new(snapshot.AllEnemiesDead || snapshot.PlayerDead, snapshot.AllEnemiesDead, snapshot.PlayerDead,
            snapshot.PlayerHp, snapshot.PlayerMaxHp, snapshot.CumulativePlayerHpLost,
            snapshot.DeathSaveUseCount, snapshot.EnemyHp, snapshot.AliveEnemyCount,
            snapshot.TeamSurvivors, snapshot.PotionUseCount, snapshot.PotionStrategicCost,
            snapshot.AdvisoryContribution, snapshot.AdvisoryEnemyCycles)
        {
            LocalDamage = snapshot.AdvisoryLocalDamage, TotalDamage = snapshot.AdvisoryTotalDamage,
            UnattributedDamage = snapshot.AdvisoryUnattributedDamage,
        };
    }

    private int ContributionProgress(int enemyHp, long localDamage, long totalDamage)
        => _contributionObjective!.Progress(enemyHp, localDamage, totalDamage);

    private int FirstCycleRisk(SearchNode node)
    {
        var first = node.Snapshot.AdvisoryLastEnemyCycle;
        while (first?.Previous != null) first = first.Previous;
        if (first != null) return first.Hp <= 0 ? 2 : 0;
        if (node.Snapshot.PlayerDead) return 2;
        return node.Snapshot.AllEnemiesDead ? 0 : 1;
    }

    private double MultiplayerTailValue(SearchNode node, MultiplayerPlanValue stage)
    {
        var tail = node.Snapshot.AdvisoryLastEnemyCycle;
        if (tail == null) return 0;
        if (tail.Cycle <= stage.Cycle)
            return node.Snapshot.PlayerDead && !stage.Dead ? -root.InitialPlayerHp : 0;
        double progress = MultiplayerScoredProgress(tail.EnemyHp, tail.LocalDamage, tail.TotalDamage, tail.UnattributedDamage);
        double stageProgress = MultiplayerScoredProgress(stage.EnemyHp, stage.LocalDamage, stage.TotalDamage, stage.UnattributedDamage);
        double gain = root.InitialPlayerHp * Math.Clamp((progress - stageProgress)
            / Math.Max(1, _contributionObjective!.TargetDamage), 0, 1);
        double cost = Math.Max(0, stage.Hp - tail.Hp) + Math.Max(0, stage.MaxHp - tail.MaxHp)
            + Math.Max(0, tail.PotionCost - stage.PotionCost)
            + Math.Max(0, tail.DeathSaves - stage.DeathSaves) * (double)root.InitialPlayerHp
            + Math.Max(0, stage.TeamSurvivors - tail.TeamSurvivors) * (double)root.InitialPlayerHp;
        if (node.Snapshot.PlayerDead) cost += root.InitialPlayerHp;
        // Discount uncertainty with distance, not average damage over time: averaging
        // erased already-realized payback from long-lived setup cards.
        return (gain - cost) * Math.Pow(0.95, tail.Cycle - stage.Cycle - 1);
    }

    private double MultiplayerObjectiveCost(SearchNode node, MultiplayerPlanValue facts)
    {
        double cost = MultiplayerQuotaSelection.Cost(facts, _contributionObjective!, root.InitialPlayerHp,
            root.InitialPlayerMaxHp);
        if (policy.Multiplayer!.CreditSharedDamage && !facts.Won)
            cost += _contributionObjective!.DeficitCost(MultiplayerScoredProgress(facts.EnemyHp,
                facts.LocalDamage, facts.TotalDamage, facts.UnattributedDamage), root.InitialPlayerHp)
                - _contributionObjective.DeficitCost(facts.Progress, root.InitialPlayerHp);
        return cost - 0.5 * MultiplayerTailValue(node, facts)
            + 0.25 * root.InitialPlayerHp * facts.EnemyHp / Math.Max(1d, root.MultiplayerObservation!.EnemyHp);
    }

    private int CompareMultiplayerAtCycle(SearchNode left, SearchNode right, int depth)
    {
        int comparison = CompareMultiplayerQualityAtCycle(left, right, depth);
        if (comparison == 0 && MultiplayerFactsAt(left, depth).Won && MultiplayerFactsAt(right, depth).Won)
            comparison = Nullable.Compare(left.Snapshot.CombatEndedTurn, right.Snapshot.CombatEndedTurn);
        return comparison != 0 ? comparison : MultiplayerActionCountAt(left, depth)
            .CompareTo(MultiplayerActionCountAt(right, depth));
    }

    private int CompareMultiplayerQualityAtCycle(SearchNode left, SearchNode right, int depth)
    {
        bool forcedA = _potionStrategy.EvaluateForcedUses(left.Actions, root.HasRenewablePotionShapedRock).AllForcedUsesSatisfied;
        bool forcedB = _potionStrategy.EvaluateForcedUses(right.Actions, root.HasRenewablePotionShapedRock).AllForcedUsesSatisfied;
        int comparison = forcedB.CompareTo(forcedA);
        if (comparison != 0) return comparison;
        comparison = left.HasPredictionRisk.CompareTo(right.HasPredictionRisk);
        if (comparison != 0) return comparison;
        comparison = FirstCycleRisk(left).CompareTo(FirstCycleRisk(right));
        if (comparison != 0) return comparison;
        MultiplayerPlanValue a = MultiplayerFactsAt(left, depth), b = MultiplayerFactsAt(right, depth);
        comparison = a.Dead.CompareTo(b.Dead);
        if (comparison != 0) return comparison;
        comparison = b.Comparable.CompareTo(a.Comparable);
        if (comparison != 0) return comparison;
        comparison = b.Won.CompareTo(a.Won);
        if (comparison != 0) return comparison;
        if (!a.Comparable) return right.Score.CompareTo(left.Score);
        comparison = MultiplayerObjectiveCost(left, a).CompareTo(MultiplayerObjectiveCost(right, b));
        if (comparison != 0) return comparison;
        comparison = a.AliveEnemies.CompareTo(b.AliveEnemies);
        if (comparison != 0) return comparison;
        comparison = a.EnemyHp.CompareTo(b.EnemyHp);
        if (comparison != 0) return comparison;
        comparison = b.Hp.CompareTo(a.Hp);
        if (comparison != 0) return comparison;
        comparison = a.HpLost.CompareTo(b.HpLost);
        if (comparison != 0) return comparison;
        return a.Potions.CompareTo(b.Potions);
    }

    private static int MultiplayerActionCountAt(SearchNode node, int depth)
    {
        SearchNode cursor = node;
        while (cursor.Parent != null && cursor.Parent.Snapshot.AdvisoryEnemyCycles >= depth)
            cursor = cursor.Parent;
        return cursor.ActionCount;
    }
}
