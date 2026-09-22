using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace CombatSolver;

internal static class MultiplayerDamageAttribution
{
    internal static bool IsLocal(Creature? dealer, Player owner)
        => ReferenceEquals(dealer, owner.Creature) || ReferenceEquals(dealer?.PetOwner, owner);

    internal static int HpDamage(DamageResult result)
        // Native UnblockedDamage already excludes OverkillDamage.
        => Math.Max(0, result.UnblockedDamage);

}
