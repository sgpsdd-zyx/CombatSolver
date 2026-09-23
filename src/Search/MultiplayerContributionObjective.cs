namespace CombatSolver;

internal sealed record MultiplayerRootObservation(int Round, int EnemyHp, int LocalHp,
    int LocalMaxHp, IReadOnlyList<ulong> Participants, long LocalDamage, long TotalDamage)
{
    public long UnattributedDamage { get; init; }
}

internal sealed record MultiplayerContributionObjective(
    int Stage, int StartedRound, int DeadlineRound, int TargetDamage, int PaidDamage,
    int RemainingCycles, int ParticipantCount, string RenewalReason,
    int? PreviousTarget = null, int? PreviousProgress = null)
{
    internal const int StageCycles = 3;
    public int StageEnemyHp { get; init; }
    public long ObservedLocalDamage { get; init; }
    public long ObservedTotalDamage { get; init; }
    public long ObservedUnattributedDamage { get; init; }

    public static MultiplayerContributionObjective Start(MultiplayerRootObservation root, int horizon)
        => new(1, root.Round, checked(root.Round + Math.Min(StageCycles, horizon) - 1),
            Share(root.EnemyHp, root.Participants.Count), 0, Math.Min(StageCycles, horizon),
            root.Participants.Count, "initial") { StageEnemyHp = root.EnemyHp };

    public int Progress(int enemyHp, long localDamage, long totalDamage)
        => NetProgress(StageEnemyHp, enemyHp, checked(ObservedLocalDamage + localDamage),
            checked(ObservedTotalDamage + totalDamage));

    internal static int Share(int enemyHp, int participants)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(participants, 1);
        return (int)Math.Min(int.MaxValue, ((long)Math.Max(0, enemyHp) + participants - 1) / participants);
    }

    internal static int NetProgress(int initialEnemyHp, int currentEnemyHp, long localDamage, long totalDamage)
    {
        if (localDamage < 0 || totalDamage < localDamage)
            throw new InvalidOperationException("Multiplayer contribution history moved backwards.");
        // Foreign and unattributed damage cannot pay the local quota. Healing, revival and
        // new enemy HP can undo progress; overkill has already been removed at the source.
        long net = Math.Min(localDamage, (long)initialEnemyHp - currentEnemyHp - (totalDamage - localDamage));
        return (int)Math.Clamp(net, int.MinValue, int.MaxValue);
    }

    public double DeficitCost(int progress, int localHp)
        => DeficitCost((double)progress, localHp);

    internal double SharedProgress(int enemyHp, long localDamage, long totalDamage, long unattributedDamage)
    {
        long local = checked(ObservedLocalDamage + localDamage);
        long total = checked(ObservedTotalDamage + totalDamage);
        long shared = checked(ObservedUnattributedDamage + unattributedDamage);
        if (shared < 0 || local < 0 || total < local || shared > total - local)
            throw new InvalidOperationException("Invalid multiplayer unattributed damage ledger.");
        // Shared damage reduces everyone's outstanding work without acquiring a fictional dealer.
        double credit = local + shared / (double)ParticipantCount;
        return credit + Math.Min(0, StageEnemyHp - (double)enemyHp - total);
    }

    internal double DeficitCost(double progress, int localHp)
    {
        double deficit = Math.Clamp((TargetDamage - (double)progress) / Math.Max(1, TargetDamage), 0, 2);
        return Math.Max(1, localHp) * deficit * deficit;
    }
}
