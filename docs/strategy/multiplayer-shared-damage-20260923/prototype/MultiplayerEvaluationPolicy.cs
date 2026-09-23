namespace CombatSolver;

[Flags]
internal enum MultiplayerEvaluationFeatures
{
    None = 0,
    SharedDamage = 1,
    DiscountPeerRisk = 2,
    ThreatPressure = 4,
}

internal sealed class MultiplayerEnemyHealth(EnemyDurabilityVector values) : IEquatable<MultiplayerEnemyHealth>
{
    public int Count => values.Count;
    public EnemyDurabilityEntry this[int index] => values[index];

    public bool Equals(MultiplayerEnemyHealth? other)
    {
        if (other == null || Count != other.Count) return false;
        for (int index = 0; index < Count; index++)
            if (this[index] != other[index]) return false;
        return true;
    }

    public override bool Equals(object? other) => other is MultiplayerEnemyHealth health && Equals(health);
    public override int GetHashCode()
    {
        HashCode hash = new();
        for (int index = 0; index < Count; index++) hash.Add(this[index]);
        return hash.ToHashCode();
    }
}

internal sealed class MultiplayerThreatPressure
{
    private readonly Dictionary<uint, double> _weights = [];
    private readonly double _totalThreat;
    private readonly double _unknownWeight;

    public MultiplayerThreatPressure(CombatRootSnapshot root)
    {
        var health = root.MultiplayerObservation!.Enemies!;
        int rounds = Math.Max(1, root.Forecast.Rounds.Count);
        for (int index = 0; index < health.Count; index++)
        {
            var enemy = health[index];
            double threat = 1 + root.Forecast.Rounds.Sum(round => round
                .Where(move => move.Owner.CombatId == enemy.CombatId)
                .Sum(move => move.AttackHits.Sum(hit => Math.Max(0, hit.BaseDamage)))) / (double)rounds;
            _weights.Add(enemy.CombatId, threat / Math.Max(1, enemy.Durability));
            _totalThreat += threat;
        }
        _unknownWeight = _totalThreat / Math.Max(1, root.MultiplayerObservation.EnemyHp);
    }

    public double Fraction(MultiplayerEnemyHealth? health, int fallbackHp, int initialHp)
    {
        if (health == null) return fallbackHp / Math.Max(1d, initialHp);
        double remaining = 0;
        for (int index = 0; index < health.Count; index++)
        {
            var enemy = health[index];
            remaining += Math.Max(0, enemy.Durability) * _weights.GetValueOrDefault(enemy.CombatId, _unknownWeight);
        }
        return remaining / Math.Max(1, _totalThreat);
    }
}
