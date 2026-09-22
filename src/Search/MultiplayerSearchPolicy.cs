namespace CombatSolver;

internal sealed record MultiplayerSearchPolicy(
    int Horizon = 14,
    IReadOnlyList<IReadOnlyList<PlanAction>>? PreviousRoutes = null)
{
    internal const int LongHorizonBudgetMultiplier = 2;

    public MultiplayerContributionObjective? Objective { get; init; }

    public SolverSearchProfile ResolveSearchProfile(SearchPolicySnapshot policy)
    {
        SolverSearchProfile profile = policy.Profile;
        if (policy.BudgetOverrideMilliseconds is { } budget)
            return profile with { SoftTimeBudgetMilliseconds = budget };
        if (policy.FixedBudget) return profile;
        // Spend the extra work once per manual request; explicit test budgets stay exact.
        return profile with
        {
            MaxExpandedNodes = (int)Math.Min(int.MaxValue, (long)profile.MaxExpandedNodes * LongHorizonBudgetMultiplier),
            SoftTimeBudgetMilliseconds = (int)Math.Min(int.MaxValue,
                (long)profile.SoftTimeBudgetMilliseconds * LongHorizonBudgetMultiplier),
        };
    }

    public int RemainingCycleLayers(int completedEnemyCycles) => Math.Max(1, Horizon - completedEnemyCycles);

    public SearchPolicySnapshot Apply(SearchPolicySnapshot policy)
    {
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
