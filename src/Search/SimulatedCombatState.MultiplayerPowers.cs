using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace CombatSolver;

internal sealed partial class SimulatedCombatState
{
    private bool UsesMultiplayerPerApplierPower(PowerModel power)
        => AdvisorPlayer != null && power.InstanceType == PowerInstanceType.InstancedPerApplier;

    private T? GetMultiplayerPowerForApplier<T>(Creature owner, Creature? applier) where T : PowerModel
        => EffectivePowers().OfType<T>().FirstOrDefault(power => power.Owner == owner && power.Applier == applier);

    private PowerModel GetOrCreateMultiplayerPowerForApplier<T>(Creature owner, T prototype, Creature? applier)
        where T : PowerModel
    {
        if (GetMultiplayerPowerForApplier<T>(owner, applier) is { } existing)
            return GetMutablePowerInstance(existing);

        // Reuse the instance stores and Fork remapping; owner/type slots cannot distinguish appliers.
        T created = CreatePowerForApplication(owner, prototype.Target, applier, prototype);
        created.AmountOnTurnStart = 0;
        (_addedPowerInstances ??= []).Add(created);
        InvalidateHookListeners();
        return created;
    }
}
