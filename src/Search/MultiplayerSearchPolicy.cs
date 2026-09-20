namespace CombatSolver;

internal sealed record MultiplayerSearchPolicy(
    int Horizon = 7,
    IReadOnlyList<IReadOnlyList<PlanAction>>? PreviousRoutes = null,
    int AcceptableHpLossPerTurn = 3)
{
    public SearchPolicySnapshot Apply(SearchPolicySnapshot policy)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(AcceptableHpLossPerTurn);
        ArgumentOutOfRangeException.ThrowIfLessThan(Horizon, 1);
        return policy with
        {
            Multiplayer = this,
            IncludeTurnSetup = false,
            IgnoreLongTermRewards = true,
            RelicTargets = [],
            TheftPolicy = null,
            Act3BossStrategy = false,
            StopAtAcceptableBattleHpLoss = false,
            PredictPotionReward = false,
            UseNoveltyPortfolio = false,
            UseBeamWidthPortfolio = false,
            PortfolioExperiment = null,
            NoveltySearch = null,
        };
    }
}

// Search history, not combat state: unused allowance never carries into another enemy cycle.
internal readonly record struct MultiplayerHpLossBudget(
    int CompletedExcessHpLost,
    int CurrentCycleHpLost,
    int MaximumCycleHpLost)
{
    public int ExcessHpLost(int allowance)
        => checked(CompletedExcessHpLost + Math.Max(0, CurrentCycleHpLost - allowance));

    public static MultiplayerHpLossBudget Capture(SearchNode? parent, SimulationSnapshot snapshot)
    {
        if (snapshot.AdvisoryHpLossAllowance < 0) return default;
        if (parent == null)
        {
            int initial = checked(snapshot.AdvisoryRootHpLost + snapshot.CumulativePlayerHpLost);
            return new(0, initial, initial);
        }
        MultiplayerHpLossBudget prior = parent.AdvisoryHpLoss;
        int loss = snapshot.CumulativePlayerHpLost - parent.Snapshot.CumulativePlayerHpLost;
        if (loss < 0 || snapshot.AdvisoryEnemyCycles < parent.Snapshot.AdvisoryEnemyCycles)
            throw new InvalidOperationException("Multiplayer damage history moved backwards.");
        int allowance = snapshot.AdvisoryHpLossAllowance;
        if (snapshot.AdvisoryEnemyCycles == parent.Snapshot.AdvisoryEnemyCycles)
            return prior.Advance(loss, enemyCycleEnded: false, allowance);
        int cycleEndLoss = snapshot.AdvisoryLastEnemyCycleHpLost - parent.Snapshot.CumulativePlayerHpLost;
        if (snapshot.AdvisoryEnemyCycles != parent.Snapshot.AdvisoryEnemyCycles + 1
            || cycleEndLoss < 0 || cycleEndLoss > loss)
            throw new InvalidOperationException("Multiplayer route skipped an enemy-cycle damage checkpoint.");
        return prior.Advance(cycleEndLoss, enemyCycleEnded: true, allowance)
            .Advance(loss - cycleEndLoss, enemyCycleEnded: false, allowance);
    }

    internal MultiplayerHpLossBudget Advance(int hpLost, bool enemyCycleEnded, int allowance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hpLost);
        ArgumentOutOfRangeException.ThrowIfNegative(allowance);
        int current = checked(CurrentCycleHpLost + hpLost);
        int maximum = Math.Max(MaximumCycleHpLost, current);
        return enemyCycleEnded
            ? new(checked(CompletedExcessHpLost + Math.Max(0, current - allowance)), 0, maximum)
            : new(CompletedExcessHpLost, current, maximum);
    }

    public static double ApplyScore(double score, SearchNode? parent, SimulationSnapshot snapshot)
        => snapshot.AdvisoryHpLossAllowance < 0 ? score
            : score - Capture(parent, snapshot).ExcessHpLost(snapshot.AdvisoryHpLossAllowance) * 100000d;
}
