namespace CombatSolver;

internal readonly record struct MultiplayerPlanValue(bool Comparable, bool Won, bool Dead,
    int Hp, int MaxHp, int HpLost, int DeathSaves, int EnemyHp, int AliveEnemies,
    int TeamSurvivors, int Potions, int PotionCost, int Progress, int Cycle)
{
    public int HealthCost(int rootHp, int rootMaxHp)
        => Math.Max(0, rootHp - Hp) + Math.Max(0, rootMaxHp - MaxHp);
}

internal static class MultiplayerQuotaSelection
{
    public static double Cost(MultiplayerPlanValue value, MultiplayerContributionObjective objective,
        int rootHp, int rootMaxHp)
        => value.HealthCost(rootHp, rootMaxHp) + value.PotionCost
            + value.DeathSaves * Math.Max(1, rootHp)
            + Math.Max(0, objective.ParticipantCount - value.TeamSurvivors) * Math.Max(1, rootHp)
            + (value.Won ? 0 : objective.DeficitCost(value.Progress, rootHp));

    public static bool Dominates(MultiplayerPlanValue left, MultiplayerPlanValue right,
        int rootHp, int rootMaxHp)
        => left.Comparable && right.Comparable && left.Cycle == right.Cycle
            && left.Won == right.Won && left.Dead == right.Dead
            && left.Progress >= right.Progress && left.EnemyHp <= right.EnemyHp
            && left.AliveEnemies <= right.AliveEnemies && left.Hp >= right.Hp
            && left.MaxHp >= right.MaxHp && left.HpLost <= right.HpLost
            && left.DeathSaves <= right.DeathSaves && left.TeamSurvivors >= right.TeamSurvivors
            && left.Potions <= right.Potions && left.PotionCost <= right.PotionCost
            && (left.Progress > right.Progress || left.EnemyHp < right.EnemyHp
                || left.HealthCost(rootHp, rootMaxHp) < right.HealthCost(rootHp, rootMaxHp)
                || left.PotionCost < right.PotionCost || left.TeamSurvivors > right.TeamSurvivors);
}
