namespace CombatSolver;

// Immutable raw observations only; no simulator/model references or search policy. The chain
// is bounded by the advisory horizon and shared by forks until another enemy cycle completes.
internal sealed record MultiplayerCycleCheckpoint(int Cycle, int Hp, int HpLost, int DeathSaves,
    int EnemyHp, int TeamSurvivors, int Potions, MultiplayerCycleCheckpoint? Previous);
