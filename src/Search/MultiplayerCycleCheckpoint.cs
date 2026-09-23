namespace CombatSolver;

// Immutable raw observations only; no simulator/model references or search policy. The chain
// is bounded by the advisory horizon and shared by forks until another enemy cycle completes.
internal sealed record MultiplayerCycleCheckpoint(int Cycle, int Hp, int HpLost, int DeathSaves,
    int EnemyHp, int TeamSurvivors, int Potions, MultiplayerCycleCheckpoint? Previous)
{
    public long LocalDamage { get; init; }
    public long TotalDamage { get; init; }
    public long UnattributedDamage { get; init; }
    public int AliveEnemies { get; init; }
    public int PotionCost { get; init; }
    public int MaxHp { get; init; }
}
