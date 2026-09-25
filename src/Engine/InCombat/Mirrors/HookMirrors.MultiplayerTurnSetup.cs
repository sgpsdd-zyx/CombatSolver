using CombatSolver.Engine.InCombat.Mirrors.Hooks.TurnStart;
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace CombatSolver.Engine.InCombat.Mirrors;

internal static partial class HookMirrors
{
    internal static bool ResumeMultiplayerToastyMittens(CombatPredictionSimulator simulator,
        SimulatedCombatState combat, Player player, TurnStartChoiceCursor choices, int relicIndex)
    {
        IReadOnlyList<RelicModel> relics = combat.RelicsOf(player);
        if (combat.AdvisorPlayer != player || (uint)relicIndex >= relics.Count
            || relics[relicIndex] is not ToastyMittens || relics[relicIndex].IsMelted)
            throw new InvalidOperationException("Invalid multiplayer turn-start choice root.");
        var context = new AfterPlayerTurnStartMirrorContext { Simulator = simulator, Player = player, Choices = choices };
        // Vanilla normal hooks affecting this player have already completed their Power
        // prefix. Only the current relic and the remaining owned relics are unfinished.
        for (int index = relicIndex; index < relics.Count; index++)
        {
            if (relics[index].IsMelted) continue;
            AfterPlayerTurnStartMirrors.Invoke(relics[index], context, phase: 1);
            if (simulator.HasPendingChoice) return false;
        }
        // Late listeners are enumerated after the resumed normal phase, as in vanilla.
        foreach (AbstractModel listener in IterateCombatHookListeners(simulator))
        {
            AfterPlayerTurnStartMirrors.Invoke(listener, context, phase: 2);
            if (simulator.HasPendingChoice) return false;
        }
        return true;
    }
}
