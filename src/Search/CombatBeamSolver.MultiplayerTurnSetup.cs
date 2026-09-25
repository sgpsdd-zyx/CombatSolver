using CombatSolver.Engine.InCombat.Mirrors;
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;

namespace CombatSolver;

internal sealed partial class CombatBeamSolver
{
    private SearchBoundaryReason ResumeMultiplayerTurnSetup(CombatPredictionSimulator simulator,
        SimulatedCombatState combat, TurnStartChoiceCursor choices, ISet<uint> deaths,
        MultiplayerTurnSetup setup)
    {
        if (root.PlayerPhase != PlayerTurnPhase.Start || !root.IsMultiplayerAdvisor)
            throw new InvalidOperationException("Multiplayer setup requires a captured native choice pause.");
        if (!HookMirrors.ResumeMultiplayerToastyMittens(simulator, combat, _player, choices, setup.RelicIndex))
            return SearchBoundaryReason.PendingChoice;
        // The paused native action is enqueued only after shared side hooks and every
        // player's orbs have started. Resume only this player's remaining local setup.
        if (!CorePowerSupport.ApplyEnemyDeathPowers(simulator, combat, combat.KnownEnemies, deaths))
            return SearchBoundaryReason.PendingChoice;
        combat.TriggerAutoPrePlayEarly(simulator, _player, _startTurnNumber, choices, deaths);
        if (combat.HasPendingChoice) return SearchBoundaryReason.PendingChoice;
        choices.AssertConsumed();
        combat.NormalizeAeonglassWithers(simulator);
        combat.NormalizeCardAfflictions(simulator);
        combat.SetPredictedEnemyIntents(combat.CurrentMonsterMoves()
            .Where(move => move.AttackHits.Count > 0).Select(move => move.Owner));
        simulator.CheckWinCondition(_startTurnNumber);
        return SearchBoundaryReason.None;
    }
}
