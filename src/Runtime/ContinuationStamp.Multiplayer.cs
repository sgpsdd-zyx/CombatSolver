using System.Text;
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;

namespace CombatSolver;

internal sealed partial record ContinuationStamp
{
    private static void AppendMultiplayerLive(StringBuilder text, CombatState state, Player local)
    {
        text.Append(";advisor=").Append(local.NetId).Append(':').Append(state.RoundNumber)
            .Append(':').Append(state.CurrentSide);
        text.Append(";extra=").AppendJoin(',', CombatManager.Instance.PlayersTakingExtraTurn.Select(player => player.NetId));
        foreach (Player player in state.Players)
        {
            PlayerCombatState pcs = player.PlayerCombatState
                ?? throw new InvalidOperationException("Multiplayer player has no combat state.");
            text.Append(";peer=").Append(player.NetId).Append(':').Append(player.Creature.CombatId)
                .Append(':').Append(pcs.TurnNumber).Append(':').Append(pcs.Phase)
                .Append(':').Append(player.Creature.CurrentHp).Append(':').Append(player.Creature.MaxHp)
                .Append(':').Append(player.Creature.Block).Append(':').Append(pcs.Energy)
                .Append(':').Append(pcs.Stars).Append(':').Append(player.Gold)
                .Append(':').Append(player.IsActiveForHooks);
            AppendOsty(text, player.Osty, player.Osty?.CurrentHp ?? 0, player.Osty?.MaxHp ?? 0);
            text.Append(";osty_block=").Append(player.Osty?.Block ?? 0);
            AppendLivePile(text, pcs.Hand, 'H');
            AppendLivePile(text, pcs.DrawPile, 'D');
            AppendLivePile(text, pcs.DiscardPile, 'C');
            AppendLivePile(text, pcs.ExhaustPile, 'X');
            AppendLivePile(text, pcs.PlayPile, 'P');
            AppendOrbs(text, pcs.OrbQueue.Capacity, pcs.OrbQueue.Orbs);
            AppendPotions(text, player, player.GetPotionAtSlotIndex);
            SimulatedCombatState.AppendLiveTurnCardHistory(text, state, player);
            SimulatedCombatState.AppendLiveStatefulRelics(text, player);
            RelicPredictionStateSupport.AppendLiveContinuation(text, player);
        }
    }

    private static void AppendMultiplayerPredicted(StringBuilder text,
        CombatPredictionSimulator simulator, SimulatedCombatState combat, Player local)
    {
        text.Append(";advisor=").Append(local.NetId).Append(':').Append(combat.RoundNumber)
            .Append(':').Append(combat.CurrentSide);
        text.Append(";extra=").AppendJoin(',', combat.AdvisorExtraTurnPlayers.Select(player => player.NetId));
        foreach (Player player in combat.Players)
        {
            SimPlayerCombatState pcs = simulator.State.GetPlayerCombatState(player);
            SimCreatureState creature = simulator.State.GetCreature(player.Creature);
            text.Append(";peer=").Append(player.NetId).Append(':').Append(player.Creature.CombatId)
                .Append(':').Append(combat.GetPlayerTurnNumber(player)).Append(':').Append(pcs.Phase)
                .Append(':').Append(creature.CurrentHp).Append(':').Append(creature.MaxHp)
                .Append(':').Append(creature.Block).Append(':').Append(pcs.Energy)
                .Append(':').Append(pcs.Stars).Append(':').Append(combat.GetPlayerGold(player))
                .Append(':').Append(combat.IsPlayerActiveForHooks(player));
            var osty = combat.GetOsty(player);
            AppendOsty(text, osty, osty == null ? 0 : simulator.State.GetCreature(osty).CurrentHp,
                combat.GetOstyMaxHp(simulator, player));
            text.Append(";osty_block=").Append(osty == null ? 0 : simulator.State.GetCreature(osty).Block);
            AppendPredictedPile(text, pcs.Hand, 'H');
            AppendPredictedPile(text, pcs.DrawPile, 'D');
            AppendPredictedPile(text, pcs.DiscardPile, 'C');
            AppendPredictedPile(text, pcs.ExhaustPile, 'X');
            AppendPredictedPile(text, pcs.PlayPile, 'P');
            AppendPredictedOrbs(text, simulator, pcs.OrbQueue.Capacity, pcs.OrbQueue.Orbs);
            AppendPotions(text, player, slot => combat.GetPotionAtSlot(player, slot), combat.PotionSlotCount(player));
            combat.AppendPredictedTurnCardHistory(text, player);
            combat.AppendPredictedStatefulRelics(text, player);
            RelicPredictionStateSupport.AppendPredictedContinuation(text, simulator, combat.RelicsOf(player));
        }
    }
}
