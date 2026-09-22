using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Players;

namespace CombatSolver;

internal static class MultiplayerContributionCapture
{
    // Native history carries Dealer separately from Actor/Receiver. Only read it at the root.
    public static MultiplayerRootObservation Capture(CombatState live, CombatPredictionSimulator simulator,
        Player player)
    {
        var combat = (SimulatedCombatState)simulator.State.CombatState;
        long local = 0, total = 0;
        foreach (var entry in CombatManager.Instance.History.Entries.OfType<DamageReceivedEntry>())
        {
            if (entry.Receiver.Side != CombatSide.Enemy) continue;
            int damage = MultiplayerDamageAttribution.HpDamage(entry.Result);
            total = checked(total + damage);
            if (MultiplayerDamageAttribution.IsLocal(entry.Dealer, player)) local = checked(local + damage);
        }
        return new(live.RoundNumber, combat.KnownEnemies.Sum(enemy =>
                combat.EffectiveEnemyHp(enemy, simulator.State.GetCreature(enemy))),
            player.Creature.CurrentHp, player.Creature.MaxHp,
            Array.AsReadOnly(live.Players.Where(peer => peer.Creature.IsAlive)
                .Select(peer => peer.NetId).Order().ToArray()), local, total);
    }
}
