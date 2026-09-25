using CombatSolver.Engine.InCombat.Mirrors.Hooks.TurnStart;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching.Models;

namespace CombatSolver;

// Observe the native pause; searches and cancellation remain owned by SolverController.
internal static class MultiplayerTurnSetupCoordinator
{
    internal sealed record PendingChoice(CombatState Combat, Player Player,
        HookPlayerChoiceContext Context, NativeChoiceSession Choices,
        ToastyMittens Source, int Turn, int RelicIndex)
    {
        public Task? Completion { get; set; }
    }

    private static PendingChoice? _pending;

    internal static PendingChoice? Begin(ToastyMittens source, Player player, PlayerChoiceContext context)
    {
        if (!SolverController.IsMultiplayerSession || !LocalContext.IsMe(player)
            || source.Owner != player || player.PlayerCombatState?.Phase != PlayerTurnPhase.Start
            || context is not HookPlayerChoiceContext hook)
            return null;
        Reset();
        CombatState combat = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Turn-start choice has no combat.");
        int index = player.Relics.ToList().IndexOf(source);
        if (index < 0) throw new InvalidOperationException("Turn-start choice source is not owned by the player.");
        return _pending = new(combat, player, hook,
            NativeChoiceRuntime.Begin(combat, player, "multiplayer_turn_setup"),
            source, player.PlayerCombatState.TurnNumber, index);
    }

    internal static bool IsStablePendingChoice(CombatState state)
    {
        PendingChoice? pending = _pending;
        if (pending == null || !ReferenceEquals(pending.Combat, state)
            || pending.Completion?.IsCompleted != false
            || pending.Player.PlayerCombatState?.Phase != PlayerTurnPhase.Start
            || pending.Player.PlayerCombatState.TurnNumber != pending.Turn
            || !pending.Choices.IsVisibleChoicePending
            || pending.Choices.LatestVisibleSequence != pending.Choices.FirstVisibleSequence)
            return false;
        var executor = RunManager.Instance.ActionExecutor;
        // Completing a teammate action clears CurrentlyRunningAction while this
        // choice remains paused. Any executing action still needs its barrier.
        return pending.Context.GameAction is
                { State: GameActionState.GatheringPlayerChoice, CompletionTask.IsCompleted: false } action
            && (executor.CurrentlyRunningAction == null || ReferenceEquals(executor.CurrentlyRunningAction, action))
            && executor.FinishedExecutingActions().IsCompletedSuccessfully;
    }

    internal static MultiplayerTurnSetup? Capture(CombatState state)
    {
        if (!IsStablePendingChoice(state)) return null;
        PendingChoice pending = _pending!;
        if (pending.RelicIndex >= pending.Player.Relics.Count
            || !ReferenceEquals(pending.Player.Relics[pending.RelicIndex], pending.Source)
            || pending.Source.IsMelted)
            throw new InvalidOperationException("Pending turn-start relic changed before root capture.");
        // Arbitrary callbacks can retain private progress across the native await.
        // Their mid-hook state is not covered by this vanilla continuation contract.
        if (AfterPlayerTurnStartMirrors.HasExternalRegistrations
            || state.IterateHookListeners().Any(model => model.GetType().Assembly != typeof(ToastyMittens).Assembly
                && AfterPlayerTurnStartMirrors.HasOverride(model)))
            throw new NotSupportedException("Pending multiplayer turn-start choices with custom turn-start hooks are not supported.");
        return new(pending.RelicIndex);
    }

    internal static void Observe()
    {
        if (_pending is { } pending
            && (pending.Completion?.IsCompleted == true
                || !ReferenceEquals(pending.Combat, CombatManager.Instance.DebugOnlyGetState())))
            Reset();
    }

    internal static void PrepareForSceneExit()
    {
        if (_pending != null) NativeChoiceRuntime.CancelActiveHandSelectionForSceneExit();
        Reset();
    }

    internal static void Reset()
    {
        _pending?.Choices.Dispose();
        _pending = null;
    }
}

internal sealed class MultiplayerToastyMittensChoicePatch : IPatchMethod
{
    public static string PatchId => "combat_solver_multiplayer_toasty_choice";
    public static string Description => "Observe the local multiplayer turn-start exhaust choice";
    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(ToastyMittens), nameof(ToastyMittens.AfterPlayerTurnStart), [typeof(PlayerChoiceContext), typeof(Player)])];

    [HarmonyPriority(Priority.First)]
    public static void Prefix(ToastyMittens __instance, PlayerChoiceContext choiceContext, Player player,
        out MultiplayerTurnSetupCoordinator.PendingChoice? __state)
        => __state = MultiplayerTurnSetupCoordinator.Begin(__instance, player, choiceContext);

    public static void Postfix(Task __result, MultiplayerTurnSetupCoordinator.PendingChoice? __state)
    {
        if (__state != null) __state.Completion = __result;
    }
}
