using MegaCrit.Sts2.Core.Entities.Creatures;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors;

namespace CombatSolver.Engine.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    // Mirrors CreatureCmd.Heal's state mutation and HP-change hook without mutating real Creature state.
    // VFX/SFX, map-point history and waits are intentionally omitted.
    public void Heal(Creature creature, decimal amount)
    {
        if (IsEnding && !creature.IsPlayer)
        {
            return;
        }

        var creatureState = State.GetCreature(creature);
        int hpBeforeHeal = creatureState.CurrentHp;
        creatureState.Heal(amount);
        if (hpBeforeHeal <= 0 && creatureState.IsAlive && creature.Player is { } revived
            && State.CombatState is SimulatedCombatState multiplayer && multiplayer.AdvisorPlayer != null)
            multiplayer.SetPlayerActiveForHooks(revived, active: true);
        int restoredHp = creatureState.CurrentHp - hpBeforeHeal;
        ActionRelicTriggers?.RecordHealth("heal", creature.CombatId, ResolveDamageSource(null),
            amount, restoredHp, hpBeforeHeal, creatureState.CurrentHp);
        if (restoredHp > 0 && State.CombatState is ICombatPredictionCardEventSink eventSink)
        {
            eventSink.RecordHpRecovered(creature, restoredHp);
        }

        if (amount > 0m)
        {
            HookMirrors.AfterCurrentHpChanged(this, creature, amount);
        }
    }
}
