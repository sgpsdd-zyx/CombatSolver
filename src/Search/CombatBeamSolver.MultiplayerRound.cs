using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CombatSolver;

internal sealed partial class CombatBeamSolver
{
    private bool EndMultiplayerPlayerTurn(CombatPredictionSimulator simulator,
        SimulatedCombatState combat, ISet<uint> deaths, out bool extraTurn)
    {
        extraTurn = false;
        IReadOnlyList<Player> players = combat.AdvisorExtraTurnPlayers.Count > 0
            ? combat.AdvisorExtraTurnPlayers : combat.Players;
        Creature[] participants = players.Select(player => player.Creature).ToArray();
        List<Player> extra = [];
        foreach (Player player in players)
        {
            if (!combat.TryPrepareExtraPlayerTurn(simulator, player, out bool takingExtra, out _))
                return false;
            if (takingExtra) extra.Add(player);
        }
        combat.AdvisorEtherealCounts = players.ToDictionary(player => player,
            player => combat.CountEtherealCardsInHand(simulator, player));
        if (!PlayerTurnEndLifecycle.RunPhaseOne(simulator, combat, _player, participants)) return false;
        foreach (Player player in players) combat.CommitHistoryCourseTurn(player);
        combat.NormalizeAeonglassWithers(simulator);
        combat.NormalizeCardAfflictions(simulator);
        if (!CorePowerSupport.ApplyEnemyDeathPowers(simulator, combat, combat.KnownEnemies, deaths)) return false;
        if (!simulator.IsInProgress) return true;
        foreach (Player player in players)
        {
            if (simulator.State.GetCreature(player.Creature).IsDead) continue;
            CorePowerSupport.FlushPlayerHandAtTurnEnd(simulator, combat, player);
            if (combat.HasPendingChoice) return false;
        }
        if (!PlayerTurnEndLifecycle.RunPhaseTwo(simulator, combat, participants)) return false;
        combat.AdvisorEtherealCounts = null;
        if (!CorePowerSupport.ApplyEnemyDeathPowers(simulator, combat, combat.KnownEnemies, deaths)) return false;
        combat.AdvisorExtraTurnPlayers = extra.ToArray();
        foreach (Player player in extra) combat.ConsumeExtraTurnSources(player);
        extraTurn = extra.Count > 0;
        return true;
    }

    private SearchBoundaryReason StartMultiplayerPlayerTurn(CombatPredictionSimulator simulator,
        SimulatedCombatState combat, ISet<uint> deaths, ref int shufflesCrossed,
        TurnStartChoiceCursor choices, bool extraTurn)
    {
        combat.SetActionChoiceTiming(PlanChoiceTiming.PlayerTurnStart);
        combat.CurrentSide = CombatSide.Player;
        if (!extraTurn) combat.RoundNumber++;
        IReadOnlyList<Player> players = extraTurn ? combat.AdvisorExtraTurnPlayers : combat.Players;
        Creature[] participants = extraTurn
            ? players.Select(player => player.Creature).ToArray() : combat.Allies.ToArray();
        foreach (Player player in combat.Players)
            simulator.State.GetPlayerCombatState(player).Phase = PlayerTurnPhase.None;
        foreach (Player player in players) combat.AdvancePlayerTurn(player);
        combat.ResetMultiplayerHistoryWindow();
        foreach (Creature creature in participants) combat.BeginSideTurn(creature);
        combat.SnapshotPowerAmountsAtTurnStart(participants);
        if (!TurnStartRelicSupport.TriggerBeforeSideTurnStart(simulator, combat, participants)
            || TurnStartPowerSupport.TriggerBeforeSideTurnStart(simulator, combat, participants))
            return SearchBoundaryReason.PendingChoice;
        foreach (Player player in combat.Players)
            simulator.State.GetPlayerCombatState(player).Phase = PlayerTurnPhase.Start;
        foreach (Creature creature in participants)
        {
            var state = simulator.State.GetCreature(creature);
            if (state.Block > 0)
            {
                if (combat.ShouldClearBlock(creature, out AbstractModel? preventer))
                    state.DamageBlock(state.Block, ValueProp.Move);
                else PersistentRelicSupport.TriggerAfterPreventingBlockClear(simulator, preventer, creature);
            }
        }
        foreach (Creature creature in participants)
            if (!CorePowerSupport.TriggerAfterBlockCleared(simulator, combat, creature))
                return SearchBoundaryReason.PendingChoice;
        int beforeShuffles = simulator.ShuffleEventCount;
        bool PrepareHand(Player player)
        {
            if (simulator.State.GetCreature(player.Creature).IsDead) return true;
            var state = simulator.State.GetPlayerCombatState(player);
            if (PersistentRelicSupport.ShouldPlayerResetEnergy(combat, player)) state.LoseEnergy(state.Energy);
            state.GainEnergy(PersistentPowerSupport.GetModifiedMaxEnergy(combat, player)
                + combat.ConsumeEnergyNextTurn(player));
            if (!PersistentPowerSupport.TriggerAfterEnergyReset(simulator, combat, player))
                return false;
            TurnStartRelicSupport.TriggerAfterEnergyReset(simulator, combat, player);
            if (combat.HasPendingChoice) return false;
            TurnStartRelicSupport.TriggerAfterEnergyResetLate(simulator, combat, player);
            if (combat.HasPendingChoice || combat.PrepareBeforeHandDraw(simulator, player, choices))
                return false;
            int draw = PersistentPowerSupport.ConsumeModifiedHandDraw(combat, player, CombatManager.baseHandDrawCount);
            int history = simulator.History.Entries.Count;
            simulator.Draw(player, draw, fromHandDraw: true);
            if (combat.HasPendingChoice) return false;
            TriggeredPowerSupport.CompensateHistorySince(simulator, combat, history);
            if (combat.HasPendingChoice || combat.TriggerAfterPlayerTurnStart(simulator, player.Creature, choices))
                return false;
            return true;
        }
        int nextPlayer = 0;
        bool sideStarted = false;
        HashSet<Player> autoStarted = [];
        bool StartAuto(Player player)
        {
            if (!autoStarted.Add(player) || simulator.State.GetCreature(player.Creature).IsDead) return true;
            combat.TriggerAutoPrePlayEarly(simulator, player, combat.GetPlayerTurnNumber(player), choices, deaths);
            return !combat.HasPendingChoice;
        }
        bool CompleteSideStart(bool localChoicePaused)
        {
            if (sideStarted) return true;
            while (nextPlayer < players.Count)
                if (!PrepareHand(players[nextPlayer++])) return false;
            sideStarted = true;
            if (!combat.TriggerSideTurnStart(simulator, CombatSide.Player, participants,
                decrementPlating: combat.RoundNumber != 1, extraTurn)
                || !CorePowerSupport.ApplyEnemyDeathPowers(simulator, combat, combat.KnownEnemies, deaths))
                return false;
            foreach (Player player in players)
            {
                EnchantmentLifecycleSupport.TriggerAfterTurnStartOrbs(simulator, player);
                if (combat.HasPendingChoice) return false;
            }
            if (localChoicePaused)
                foreach (Player player in players)
                    if (player != _player && !StartAuto(player)) return false;
            return true;
        }
        while (nextPlayer < players.Count)
        {
            Player player = players[nextPlayer++];
            // Native setup pauses for local choices while other players, side hooks and
            // orbs finish starting. Resume that choice only after the same shared work.
            using var beforeChoice = player == _player && !sideStarted
                ? choices.BeforeNextTake(() => CompleteSideStart(localChoicePaused: true)) : null;
            if (!PrepareHand(player)) return SearchBoundaryReason.PendingChoice;
        }
        if (!CompleteSideStart(localChoicePaused: false)) return SearchBoundaryReason.PendingChoice;
        foreach (Player player in players)
            if (!StartAuto(player)) return SearchBoundaryReason.PendingChoice;
        shufflesCrossed += simulator.ShuffleEventCount - beforeShuffles;
        combat.NormalizeAeonglassWithers(simulator);
        combat.NormalizeCardAfflictions(simulator);
        combat.SetPredictedEnemyIntents(combat.CurrentMonsterMoves()
            .Where(move => move.AttackHits.Count > 0).Select(move => move.Owner));
        simulator.CheckWinCondition(combat.GetPlayerTurnNumber(_player));
        return SearchBoundaryReason.None;
    }
}
