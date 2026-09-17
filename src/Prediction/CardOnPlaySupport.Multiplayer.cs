using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

namespace CombatSolver;

internal static partial class CardOnPlaySupport
{
    internal static bool HasMultiplayerCompensation(CardModel card)
        => card is BelieveInYou or Flanking;

    private static void ApplyMultiplayer(CombatPredictionSimulator simulator,
        SimulatedCombatState combat, CardModel card, Creature? target)
    {
        switch (card)
        {
            case BelieveInYou:
                simulator.GainEnergy(target?.Player
                    ?? throw new InvalidOperationException("BelieveInYou requires a teammate."),
                    card.DynamicVars.Energy.IntValue);
                break;
            case Flanking:
                combat.Apply<FlankingPower>(target
                    ?? throw new InvalidOperationException("Flanking requires an enemy."), 2, card.Owner.Creature);
                break;
        }
    }
}
