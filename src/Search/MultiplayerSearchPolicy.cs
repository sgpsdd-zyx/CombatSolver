namespace CombatSolver;

internal sealed record MultiplayerSearchPolicy(
    int Horizon = 7,
    IReadOnlyList<IReadOnlyList<PlanAction>>? PreviousRoutes = null)
{
    public SearchPolicySnapshot Apply(SearchPolicySnapshot policy) => policy with
    {
        Multiplayer = this,
        IncludeTurnSetup = false,
        IgnoreLongTermRewards = true,
        RelicTargets = [],
        TheftPolicy = null,
        Act3BossStrategy = false,
        StopAtAcceptableBattleHpLoss = false,
        UseNoveltyPortfolio = false,
        UseBeamWidthPortfolio = false,
        NoveltySearch = null,
    };
}
