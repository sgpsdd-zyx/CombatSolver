using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private const string MultiplayerTurnSetupScenario = "MULTIPLAYER-TOASTY-CHOICE";
    private const string MultiplayerTurnSetupControlsScenario = "MULTIPLAYER-TOASTY-CONTROLS";
    private Harmony? _multiplayerTurnSetupPatches;
    private static UnattendedTestRunner? _multiplayerTurnSetupRunner;
    private NativeChoiceSession? _multiplayerTestChoice;
    private NativeChoiceRequest? _multiplayerTestRequest;
    private ManualResetEventSlim? _multiplayerChoiceWorkerGate;
    private ManualResetEventSlim? _multiplayerChoiceWorkerEntered;

    private void InitializeMultiplayerTurnSetupTest()
    {
        if (_request.ScenarioId is not (MultiplayerTurnSetupScenario or MultiplayerTurnSetupControlsScenario)) return;
        _multiplayerTurnSetupRunner = this;
        _multiplayerTurnSetupPatches = new Harmony("CombatSolver.Tests.MultiplayerTurnSetup");
        _multiplayerTurnSetupPatches.Patch(
            AccessTools.PropertyGetter(typeof(SolverController), nameof(SolverController.IsMultiplayerSession)),
            prefix: new HarmonyMethod(typeof(UnattendedTestRunner), nameof(MultiplayerTurnSetupModePrefix)));
        _multiplayerTurnSetupPatches.Patch(
            AccessTools.Method(typeof(CardSelectCmd), nameof(CardSelectCmd.FromHand),
                [typeof(PlayerChoiceContext), typeof(Player), typeof(CardSelectorPrefs), typeof(Func<CardModel, bool>), typeof(AbstractModel)]),
            postfix: new HarmonyMethod(typeof(UnattendedTestRunner), nameof(MultiplayerTurnSetupChoicePostfix)));
        _multiplayerTurnSetupPatches.Patch(
            AccessTools.Method(typeof(CombatBeamSolver), nameof(CombatBeamSolver.Solve)),
            prefix: new HarmonyMethod(typeof(UnattendedTestRunner), nameof(MultiplayerChoiceWorkerPrefix)));
        _multiplayerTurnSetupPatches.Patch(
            AccessTools.Method(typeof(NCardPlayQueue), "OnActionEnqueued"),
            prefix: new HarmonyMethod(typeof(UnattendedTestRunner), nameof(MultiplayerChoicePeerVisualPrefix)));
    }

    private static bool MultiplayerTurnSetupModePrefix(ref bool __result)
    {
        __result = RunManager.Instance.IsInProgress
            && RunManager.Instance.DebugOnlyGetState()?.Players.Count > 1;
        return false;
    }

    private static bool MultiplayerChoicePeerVisualPrefix(GameAction __0)
        => __0 is not PlayCardAction play || LocalContext.IsMe(play.Player);

    private static void MultiplayerTurnSetupChoicePostfix(Player player, CardSelectorPrefs prefs,
        AbstractModel source, Task __result)
    {
        if (source is not ToastyMittens || !LocalContext.IsMe(player)) return;
        UnattendedTestRunner runner = _multiplayerTurnSetupRunner
            ?? throw new InvalidOperationException("Missing multiplayer choice test runner.");
        CombatState state = CombatManager.Instance.DebugOnlyGetState()!;
        runner._multiplayerTestRequest = new NativeChoiceRequest(1, NativeChoiceSurfaceKind.Hand,
            player, player.PlayerCombatState!.Hand.Cards.ToArray(), null,
            prefs.MinSelect, prefs.MaxSelect, true, false, prefs.RequireManualConfirmation, source.Id.Entry)
        { Completion = __result };
        runner._multiplayerTestChoice = new NativeChoiceSession(state, player, "multiplayer_choice_test");
        runner._multiplayerTestChoice.Enqueue(runner._multiplayerTestRequest);
    }

    private void ReleaseMultiplayerTurnSetupTest()
    {
        if (_multiplayerTurnSetupPatches == null) return;
        _multiplayerChoiceWorkerGate?.Set();
        _multiplayerTurnSetupPatches.UnpatchAll(_multiplayerTurnSetupPatches.Id);
        _multiplayerTurnSetupPatches = null;
        _multiplayerTurnSetupRunner = null;
    }

    private static void MultiplayerChoiceWorkerPrefix()
    {
        UnattendedTestRunner? runner = _multiplayerTurnSetupRunner;
        if (runner?._multiplayerChoiceWorkerGate is not { IsSet: false } gate) return;
        runner._multiplayerChoiceWorkerEntered!.Set();
        gate.Wait();
    }

    private async Task<SolverResult> WaitForMultiplayerChoiceResultAsync()
    {
        while (SolverController.IsSearching)
        {
            EnsureWithinDeadline();
            await NextFrameAsync();
        }
        if (SolverController.LastSearchFailureForTesting is { } failure)
            throw new InvalidOperationException("Multiplayer pending-choice search failed.", failure);
        return SolverController.LastCompletedResultForTesting
            ?? throw new InvalidOperationException("Pending-choice search returned no result.");
    }

    private async Task VerifyMultiplayerChoiceControlsAsync(CombatState state, Player local)
    {
        _multiplayerChoiceWorkerGate = new(false);
        _multiplayerChoiceWorkerEntered = new(false);
        SolverController.RequestSearch(_host, state, SearchReason.Manual);
        while (!_multiplayerChoiceWorkerEntered.IsSet)
        {
            EnsureWithinDeadline();
            await NextFrameAsync();
        }
        SolverController.StopSearchByUser(_host);
        _multiplayerChoiceWorkerGate.Set();
        while (SolverController.IsSearching)
        {
            EnsureWithinDeadline();
            await NextFrameAsync();
        }
        if (!_multiplayerTestRequest!.IsPending || !_multiplayerTestChoice!.IsVisibleChoicePending)
            throw new InvalidOperationException("Stopping the search canceled the native choice.");
        _completedChecks.Add("MultiplayerTurnSetup:StopDrainsWorker:NativeInputPreserved");

        SolverController.RequestSearch(_host, state, SearchReason.Manual);
        SolverResult result = await WaitForMultiplayerChoiceResultAsync();
        if (SolverOverlaySnapshot.Capture(result, unexpectedReplan: false).Turns[0].TurnStartChoices.Count == 0)
            throw new InvalidOperationException("Multiplayer result did not display its suggested exhaust.");
        Player peer = state.Players.Single(player => player != local);
        ActionExecutor executor = RunManager.Instance.ActionExecutor;
        GameAction paused = executor.CurrentlyRunningAction
            ?? throw new InvalidOperationException("Native choice action was not observed before the peer played.");
        CardModel defend = peer.PlayerCombatState!.Hand.Cards.First(card =>
            card.Id.Entry == "DEFEND_IRONCLAD" && card.CanPlayTargeting(null));
        using CancellationTokenSource peerDeadline = new(TimeSpan.FromSeconds(12));
        GameAction played = await SolverController.EnqueueAndCaptureActionAsync(
            candidate => candidate is PlayCardAction action && action.Player == peer
                && ReferenceEquals(action.NetCombatCard.ToCardModelOrNull(), defend),
            () =>
            {
                if (!defend.TryManualPlay(null)) throw new InvalidOperationException("Native peer card enqueue failed.");
            }, peerDeadline.Token);
        await played.CompletionTask.WaitAsync(peerDeadline.Token);
        await executor.FinishedExecutingActions().WaitAsync(peerDeadline.Token);
        if (executor.CurrentlyRunningAction != null || paused.State != GameActionState.GatheringPlayerChoice
            || !_multiplayerTestRequest.IsPending || peer.Creature.Block <= 0)
            throw new InvalidOperationException("Peer action did not finish while preserving the local native choice.");
        long changedAt = Environment.TickCount64;
        while (!ProtocolHost.PublishedAdviceIsStale() && Environment.TickCount64 - changedAt < 2000)
        {
            EnsureWithinDeadline();
            await NextFrameAsync();
        }
        if (!ProtocolHost.PublishedAdviceIsStale())
            throw new InvalidOperationException("Teammate changes did not mark the pending-choice advice stale.");
        SolverController.RequestSearch(_host, state, SearchReason.Manual);
        result = await WaitForMultiplayerChoiceResultAsync();
        if (ProtocolHost.PublishedAdviceIsStale())
            throw new InvalidOperationException("Manual recalculation did not capture the changed teammate.");
        _completedChecks.Add("MultiplayerTurnSetup:ChoiceDisplayed:NativePeerActionClearsCurrent:AdviceStale:ManualFreshRoot");

        _multiplayerChoiceWorkerEntered.Reset();
        _multiplayerChoiceWorkerGate.Reset();
        SolverController.RequestSearch(_host, state, SearchReason.Manual);
        while (!_multiplayerChoiceWorkerEntered.IsSet)
        {
            EnsureWithinDeadline();
            await NextFrameAsync();
        }
        await _multiplayerTestChoice.SelectPlannedOrDifferentVisibleCardsForTesting(
            _host, result.TurnSetupChoices[0], different: true, CancellationToken.None);
        while (local.PlayerCombatState!.Phase != PlayerTurnPhase.Play)
        {
            EnsureWithinDeadline();
            await NextFrameAsync();
        }
        _multiplayerChoiceWorkerGate.Set();
        _ = await WaitForMultiplayerChoiceResultAsync();
        if (!ProtocolHost.PublishedAdviceIsStale() || SolverController.SearchesStartedForTesting != 4
            || SolverController.FullAutoEnabled || SolverController.IsDeploying)
            throw new InvalidOperationException("Manual choice during search was applied as fresh advice or started automation.");
        _completedChecks.Add("MultiplayerTurnSetup:ManualDifferentChoiceDuringWorker:ResultStale:NoAutomation");
        _multiplayerChoiceWorkerGate.Dispose();
        _multiplayerChoiceWorkerEntered.Dispose();
        _multiplayerChoiceWorkerGate = null;
        _multiplayerChoiceWorkerEntered = null;
    }

    private async Task<CombatState> VerifyMultiplayerTurnSetupAsync()
    {
        while (_multiplayerTestRequest is not { IsPending: true }
            || NPlayerHand.Instance?.IsInCardSelection != true
            || CombatManager.Instance.DebugOnlyGetState()!.Players.Any(player => !LocalContext.IsMe(player)
                && player.PlayerCombatState?.Phase != PlayerTurnPhase.Play))
        {
            EnsureWithinDeadline();
            await NextFrameAsync();
        }
        CombatState state = CombatManager.Instance.DebugOnlyGetState()!;
        Player local = LocalContext.GetMe(state)!;
        if (SolverController.SearchesStartedForTesting != 0 || SolverController.IsSearching)
            throw new InvalidOperationException("Multiplayer turn setup started an automatic search.");
        if (_request.ScenarioId == MultiplayerTurnSetupControlsScenario)
        {
            await VerifyMultiplayerChoiceControlsAsync(state, local);
            return state;
        }
        ContinuationStamp before = ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true);
        SetStage("multiplayer_pending_choice_manual_search");
        SolverController.RequestSearch(_host, state, SearchReason.Manual);
        long requestedAt = Environment.TickCount64;
        while (SolverController.SearchesStartedForTesting == 0 && Environment.TickCount64 - requestedAt < 3000)
        {
            EnsureWithinDeadline();
            await NextFrameAsync();
        }
        if (SolverController.SearchesStartedForTesting != 1)
        {
            _writer.WriteGeneratedArtifact("multiplayer-pending-choice.json", new
            {
                local.PlayerCombatState!.Phase, nativeChoicePending = _multiplayerTestRequest.IsPending,
                searches = SolverController.SearchesStartedForTesting,
                currentAction = RunManager.Instance.ActionExecutor.CurrentlyRunningAction?.GetType().FullName,
                queueIdle = RunManager.Instance.ActionExecutor.FinishedExecutingActions().IsCompleted,
            });
            NativeChoiceRuntime.CancelActiveHandSelection();
            throw new InvalidOperationException("Manual multiplayer search did not start while Toasty Mittens was awaiting a card.");
        }
        SolverResult result = await WaitForMultiplayerChoiceResultAsync();
        if (result.TurnSetupChoices.FirstOrDefault() is not { SourceId: "TOASTY_MITTENS", Effect: PlanChoiceEffect.Exhaust }
            || result.TurnSetupPlayState == null || !_multiplayerTestRequest.IsPending
            || local.PlayerCombatState!.Phase != PlayerTurnPhase.Start
            || SolverController.IsDeploying || SolverController.FullAutoEnabled
            || before.StateText != ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true).StateText)
            throw new InvalidOperationException("Pending-choice advice lost its choice or changed native state.");
        _completedChecks.Add("MultiplayerTurnSetup:ManualRequest:NativeChoiceRemainsOpen:FrozenFullPartyRoot");
        Task? nativeAction = RunManager.Instance.ActionExecutor.CurrentlyRunningAction?.CompletionTask;
        await _multiplayerTestChoice!.SelectPlannedOrDifferentVisibleCardsForTesting(
            _host, result.TurnSetupChoices[0], different: false, CancellationToken.None);
        while (local.PlayerCombatState.Phase != PlayerTurnPhase.Play
            || nativeAction?.IsCompleted == false
            || !RunManager.Instance.ActionExecutor.FinishedExecutingActions().IsCompleted)
        {
            EnsureWithinDeadline();
            await NextFrameAsync();
        }
        ContinuationStamp actual = ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true);
        bool equal = result.TurnSetupPlayState.StateText == actual.StateText;
        _writer.WriteGeneratedArtifact("multiplayer-pending-choice.json", new
        {
            equal, result.TurnSetupChoices, expected = result.TurnSetupPlayState.StateText,
            actual = actual.StateText, differences = result.TurnSetupPlayState.DescribeDifferences(actual, maximumDifferences: 12),
        });
        if (!equal)
            throw new InvalidOperationException("Pending-choice result differs from native setup: "
                + result.TurnSetupPlayState.DescribeFirstDifference(actual));
        if (SolverController.SearchesStartedForTesting != 1 || SolverController.IsDeploying || SolverController.FullAutoEnabled)
            throw new InvalidOperationException("Manual choice triggered an automatic multiplayer operation.");
        _completedChecks.Add("MultiplayerTurnSetup:ManualNativeSelection:FullStateEquivalent:NoAutomaticRecalculation");
        return state;
    }
}
