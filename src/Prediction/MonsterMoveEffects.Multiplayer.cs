using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace CombatSolver;

internal static partial class MonsterMoveEffects
{
    private static void ApplyToPlayers<T>(SimulatedCombatState combat, Creature single,
        int amount, Creature source) where T : PowerModel
    {
        if (combat.AdvisorPlayer == null) combat.Apply<T>(single, amount, source);
        else foreach (var player in combat.Players) combat.Apply<T>(player.Creature, amount, source);
    }

    private static void AddStatus<T>(CombatPredictionSimulator simulator, SimulatedCombatState combat,
        Creature single, PileType pile, int count, Player? source,
        CardPilePosition position = CardPilePosition.Bottom) where T : CardModel
    {
        if (combat.AdvisorPlayer == null) simulator.AddToCombat<T>(single, pile, count, source, position);
        else foreach (var player in combat.Players)
            simulator.AddToCombat<T>(player.Creature, pile, count, source, position);
    }

    private static void AddStatusPair<T>(CombatPredictionSimulator simulator, SimulatedCombatState combat,
        Creature single, PileType first, CardPilePosition firstPosition,
        PileType second, CardPilePosition secondPosition, int count) where T : CardModel
    {
        if (combat.AdvisorPlayer == null)
        {
            simulator.AddToCombat<T>(single, first, count, null, firstPosition);
            simulator.AddToCombat<T>(single, second, count, null, secondPosition);
            return;
        }
        foreach (var player in combat.Players)
        {
            simulator.AddToCombat<T>(player.Creature, first, count, null, firstPosition);
            simulator.AddToCombat<T>(player.Creature, second, count, null, secondPosition);
        }
    }
}
