using CombatSolver.Engine.InCombat.Simulation;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private async Task AssertMultiplayerRelicExtraTurnSourceAsync(CombatState combat)
    {
        Player local = LocalContext.GetMe(combat) ?? throw new InvalidOperationException("Missing local player.");
        Player peer = combat.Players.Single(player => player != local);
        foreach (Player player in combat.Players)
        {
            foreach (RelicModel relic in player.Relics.ToArray()) await RelicCmd.Remove(relic);
            foreach (PowerModel power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            await ClearPlayerPilesAsync(player);
            await RelicCmd.Obtain(ModelDb.Relic<PaelsEye>().ToMutable(), player);
        }
        await PowerCmd.Apply<AmbergrisPower>(new ThrowingPlayerChoiceContext(), local.Creature, 1, local.Creature, null);
        await InjectCardAsync(combat, local, new() { CardId = "DEFEND_IRONCLAD", Pile = "Hand" });
        CardModel card = local.PlayerCombatState!.Hand.Cards.Single();
        (int energy, int stars) = await card.SpendResources();
        await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(), null, isAutoPlay: false,
            new ResourceInfo { EnergySpent = energy, EnergyValue = energy, StarsSpent = stars, StarValue = stars },
            skipCardPileVisuals: true);
        PaelsEye localEye = local.GetRelic<PaelsEye>()!;
        if (localEye.ShouldTakeExtraTurn(local) || !Hook.ShouldTakeExtraTurn(combat, local))
            throw new InvalidOperationException("Extra-turn fixture did not isolate the alternate turn source.");
        CombatRootSnapshot root = CombatRootSnapshot.Capture(combat, multiplayerAdvisor: true);
        CombatPredictionSimulator predicted = root.ForkSimulator();
        SimulatedCombatState shadow = (SimulatedCombatState)predicted.State.CombatState;
        shadow.ConsumeExtraTurnSources(local);
        ContinuationStamp expected = ContinuationStamp.CapturePredicted(local, predicted, root.StartTurnNumber,
            root.Forecast, root.StartTurnNumber);
        await Hook.AfterTakingExtraTurn(combat, local);
        ContinuationStamp actual = ContinuationStamp.CaptureLive(combat, multiplayerAdvisor: true);
        bool rootFrozen = ContinuationStamp.CapturePredicted(local, root.ForkSimulator(), root.StartTurnNumber,
            root.Forecast, root.StartTurnNumber).StateText == root.ContinuationStamp.StateText;
        bool ownConsumed = !shadow.IsPaelsEyeUnused(shadow.RelicsOf(local).OfType<PaelsEye>().Single());
        bool peerUntouched = shadow.IsPaelsEyeUnused(shadow.RelicsOf(peer).OfType<PaelsEye>().Single())
            && !peer.GetRelic<PaelsEye>()!._usedThisCombat;
        bool equal = expected.StateText == actual.StateText;
        _writer.WriteGeneratedArtifact("multiplayer-relic-extra-turn-source.json", new
        {
            equal, rootFrozen, ownConsumed, peerUntouched, localEye._usedThisCombat,
            differences = expected.DescribeDifferences(actual, maximumDifferences: 12),
            expected = expected.StateText, actual = actual.StateText,
        });
        if (!equal || !rootFrozen || !ownConsumed || !peerUntouched)
            throw new InvalidOperationException("Alternate extra-turn source did not consume only the owner's Pael's Eye: "
                + string.Join("; ", expected.DescribeDifferences(actual, maximumDifferences: 12)));
        _completedChecks.Add("MultiplayerRelicExtraTurnSource:NativeFullState:OwnConsumed:PeerUntouched:FrozenRoot");
    }

    private async Task AssertMultiplayerRelicOwnershipAsync(CombatState combat)
    {
        if (combat.Players.Count != 2)
            throw new InvalidOperationException("Relic ownership fixture requires two native players.");
        // The isolated test has no network transport; preserve the multiplayer ready quorum.
        var ready = AccessTools.Method(typeof(CombatManager), "AllPlayersReadyToEndTurn", [typeof(CombatTurnState)]);
        var prefix = AccessTools.Method(typeof(UnattendedTestRunner), nameof(MultiplayerRelicReadyPrefix));
        var harmony = new Harmony("CombatSolver.Tests.MultiplayerRelicOwnership");
        harmony.Patch(ready, prefix: new HarmonyMethod(prefix));
        ulong? originalLocal = LocalContext.NetId;
        List<object> evidence = [];
        List<string> failures = [];
        void SaveEvidence(string stage)
        {
            if (!string.IsNullOrWhiteSpace(_request.EvidenceDirectory))
                _writer.WriteGeneratedArtifact("multiplayer-relic-ownership.json", new
                {
                    stage, evidence, failures, combat.RoundNumber, combat.CurrentSide,
                    players = combat.Players.Select(player => new { player.NetId,
                        player.PlayerCombatState!.TurnNumber, player.PlayerCombatState.Phase }),
                });
        }
        void Check(string name, bool passed, object facts)
        {
            evidence.Add(new { name, passed, facts });
            if (!passed) failures.Add(name);
            SaveEvidence(name);
        }
        try
        {
            foreach (var (name, localIndex, localOwns, peerOwns, peerPlays) in new[]
            {
                ("peer_only", 1, false, true, false),
                ("both_local_extra", 1, true, true, true),
                ("both_extra", 1, true, true, false),
            })
            {
                Player local = combat.Players[localIndex];
                Player peer = combat.Players[1 - localIndex];
                SaveEvidence(name + ":prepare");
                LocalContext.NetId = local.NetId;
                foreach (Player player in combat.Players)
                {
                    foreach (RelicModel relic in player.Relics.ToArray()) await RelicCmd.Remove(relic);
                    foreach (PowerModel power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
                    foreach (PotionModel? potion in player.PotionSlots.ToArray()) potion?.Discard();
                    await ClearPlayerPilesAsync(player);
                    player.Creature.SetMaxHpInternal(500);
                    player.Creature.SetCurrentHpInternal(500);
                    await PowerCmd.Apply<BufferPower>(new ThrowingPlayerChoiceContext(), player.Creature,
                        32, player.Creature, null);
                    await InjectCardAsync(combat, player, new() { CardId = "STRIKE_IRONCLAD", Pile = "Hand" });
                    // Avoid the first-shuffle tutorial; this fixture only needs the extra-turn boundary.
                    for (int cardIndex = 0; cardIndex < 12; cardIndex++)
                        await InjectCardAsync(combat, player, new() { CardId = "DEFEND_IRONCLAD", Pile = "Draw" });
                    if ((player == local && localOwns) || (player == peer && peerOwns))
                        await RelicCmd.Obtain(ModelDb.Relic<PaelsEye>().ToMutable(), player);
                }
                if (peerPlays)
                {
                    SaveEvidence(name + ":peer_play");
                    CardModel attack = peer.PlayerCombatState!.Hand.Cards.Single();
                    if (!attack.CanPlayTargeting(combat.Enemies[0]))
                        throw new InvalidOperationException("Peer fixture attack was not legal.");
                    // Exercise native manual-play hooks without the singleplayer queue's peer-card visuals.
                    (int energy, int stars) = await attack.SpendResources();
                    Task played = attack.OnPlayWrapper(new ThrowingPlayerChoiceContext(), combat.Enemies[0],
                        isAutoPlay: false, new ResourceInfo
                        { EnergySpent = energy, EnergyValue = energy, StarsSpent = stars, StarValue = stars },
                        skipCardPileVisuals: true);
                    while (!played.IsCompleted) { EnsureWithinDeadline(); await NextFrameAsync(); }
                    await played;
                }
                CombatRootSnapshot root = CombatRootSnapshot.Capture(combat, multiplayerAdvisor: true);
                int originalPeerTurn = peer.PlayerCombatState!.TurnNumber;
                SolverDisplayNames names = SolverDisplayNames.Capture(combat);
                SearchPolicySnapshot policy = new MultiplayerSearchPolicy(Horizon: 2,
                    PreviousRoutes: [[new(PlanActionKind.EndTurn, root.StartTurnNumber)]]).Apply(
                    SolverController.CaptureSearchPolicy(SolverSettings.Capture(), combat, false, null)) with
                {
                    Profile = new SolverSearchProfile(4, 100, 12, 4, 4, 1000),
                    FixedBudget = true,
                    MaxDegreeOfParallelism = 1,
                };
                CombatBeamSolver driver = new(root, names, BattleDamageTracker.Observe(combat), policy,
                    searchProfile: policy.Profile);
                CombatPredictionSimulator predicted = root.ForkSimulator();
                SimulatedCombatState shadow = (SimulatedCombatState)predicted.State.CombatState;
                var recorder = new ActionRelicTriggerRecorder();
                recorder.BeginAction(0);
                predicted.ActionRelicTriggers = recorder;
                HashSet<uint> deaths = [];
                TurnStartChoiceCursor choices = new(null);
                shadow.BeginActionChoices(choices);
                shadow.SetActionChoiceTiming(PlanChoiceTiming.PlayerTurnEnd);
                if (InvokeForcedTerminalMethod(driver, "EndMultiplayerPlayerTurn", [predicted, shadow, deaths, false]) is not true)
                    throw new InvalidOperationException("Relic ownership end phase requested an unexpected choice.");
                ulong[] expectedOwners = combat.Players.Where(player => player == local ? localOwns : peerOwns && !peerPlays)
                    .Select(player => player.NetId).ToArray();
                Check(name + ":extra_owners", shadow.AdvisorExtraTurnPlayers.Select(player => player.NetId).SequenceEqual(expectedOwners),
                    new { expectedOwners, actualOwners = shadow.AdvisorExtraTurnPlayers.Select(player => player.NetId).ToArray() });
                var started = (SearchBoundaryReason)InvokeForcedTerminalMethod(driver, "StartMultiplayerPlayerTurn",
                    [predicted, shadow, deaths, 0, choices, true])!;
                if (started != SearchBoundaryReason.None || !CombatBeamSolver.SettleReplayActionBoundary(predicted, shadow))
                    throw new InvalidOperationException("Relic ownership start phase did not settle.");
                predicted.ActionRelicTriggers = null;
                shadow.EndActionChoices();
                Check(name + ":record_count", recorder.ForAction(0).Count == expectedOwners.Length,
                    new { expected = expectedOwners.Length, recorded = recorder.ForAction(0) });
                Check(name + ":record_owners", recorder.ForAction(0).Select(trigger => trigger.OwnerNetId)
                        .SequenceEqual(expectedOwners),
                    new { expectedOwners, actualOwners = recorder.ForAction(0).Select(trigger => trigger.OwnerNetId).ToArray() });
                ContinuationStamp expected = ContinuationStamp.CapturePredicted(local, predicted,
                    shadow.GetPlayerTurnNumber(local), root.Forecast, root.StartTurnNumber);
                CombatPredictionSimulator sibling = predicted.Fork();
                Check(name + ":fork", ContinuationStamp.CapturePredicted(local, sibling,
                    shadow.GetPlayerTurnNumber(local), root.Forecast, root.StartTurnNumber).StateText == expected.StateText,
                    new { localIndex });
                if (name == "both_local_extra")
                {
                    SimulatedCombatState siblingCombat = (SimulatedCombatState)sibling.State.CombatState;
                    if (!siblingCombat.TriggerRelicsAfterSideTurnStart(sibling, CombatSide.Player,
                        combat.Players.Select(player => player.Creature).ToArray()))
                        throw new InvalidOperationException("Fork side-start unexpectedly paused.");
                    Check(name + ":fork_participation_isolation", siblingCombat.ShouldTriggerPaelsEye(
                            siblingCombat.RelicsOf(peer).OfType<PaelsEye>().Single())
                        && !shadow.ShouldTriggerPaelsEye(shadow.RelicsOf(peer).OfType<PaelsEye>().Single())
                        && ContinuationStamp.CapturePredicted(local, predicted, shadow.GetPlayerTurnNumber(local),
                            root.Forecast, root.StartTurnNumber).StateText == expected.StateText, new { localIndex });
                }
                string rootBefore = root.ContinuationStamp.StateText;
                if (name == "peer_only")
                {
                    Task<SolverResult> search = Task.Run(driver.Solve);
                    while (!search.IsCompleted) { EnsureWithinDeadline(); await NextFrameAsync(); }
                    SolverResult result = await search;
                    string[] labels = result.BestNode.Actions.SelectMany(action =>
                        SolverOverlaySnapshot.CaptureAction(action, []).RelicLabels).ToArray();
                    string ownerLabel = SolverText.Format($"队友 {1}：{ModelDb.Relic<PaelsEye>().Title.GetFormattedText()}");
                    Check(name + ":display_owner", labels.Any(label => label.Contains(ownerLabel)
                            && label.Contains(ModelDb.Relic<PaelsEye>().Title.GetFormattedText())), new { labels, ownerLabel });
                    SimulationSnapshot route = driver.ReplayMultiplayerForTesting([new(PlanActionKind.EndTurn, root.StartTurnNumber)]);
                    try
                    {
                        SimulatedCombatState routeCombat = (SimulatedCombatState)route.Simulator.State.CombatState;
                        Check(name + ":no_borrowed_turn", route.AdvisoryEnemyCycles == 1
                            && routeCombat.GetPlayerTurnNumber(local) == root.StartTurnNumber + 1
                            && routeCombat.GetPlayerTurnNumber(peer) == originalPeerTurn + 2,
                            new { route.AdvisoryEnemyCycles, localTurn = routeCombat.GetPlayerTurnNumber(local),
                                peerTurn = routeCombat.GetPlayerTurnNumber(peer) });
                    }
                    finally { route.ReleaseSimulator(); }
                }
                foreach (Player player in combat.Players) CombatManager.Instance.SetReadyToEndTurn(player, false);
                SaveEvidence(name + ":wait_native_extra");
                await WaitForRelicExtraTurnAsync(combat, expectedOwners, root, local, peer, originalPeerTurn);
                ContinuationStamp actual = ContinuationStamp.CaptureLive(combat, multiplayerAdvisor: true);
                Check(name + ":native_state", expected.StateText == actual.StateText,
                    new { differences = expected.DescribeDifferences(actual, maximumDifferences: 12),
                        expected = expected.StateText, actual = actual.StateText });
                foreach (Player player in combat.Players)
                {
                    if (player.GetRelic<PaelsEye>() is not { } liveEye) continue;
                    PaelsEye eye = shadow.RelicsOf(player).OfType<PaelsEye>().Single();
                    Check(name + ":eligibility:" + player.NetId,
                        shadow.ShouldTriggerPaelsEye(eye) == liveEye.ShouldTakeExtraTurn(player),
                        new { owner = player.NetId, predicted = shadow.ShouldTriggerPaelsEye(eye),
                            actual = liveEye.ShouldTakeExtraTurn(player), liveEye._usedThisCombat,
                            liveEye._wasOwnerPartOfLastPlayerTurn });
                }
                Check(name + ":frozen_root", ContinuationStamp.CapturePredicted(local, root.ForkSimulator(),
                        root.StartTurnNumber, root.Forecast, root.StartTurnNumber).StateText == rootBefore,
                    new { localIndex });
                if (name == "both_extra") break;
                int round = combat.RoundNumber;
                foreach (Player player in CombatManager.Instance.PlayersTakingExtraTurn)
                    CombatManager.Instance.SetReadyToEndTurn(player, false);
                SaveEvidence(name + ":wait_normal_turn");
                while (combat.RoundNumber == round || combat.CurrentSide != CombatSide.Player
                    || combat.Players.Any(player => player.PlayerCombatState!.Phase != PlayerTurnPhase.Play))
                {
                    EnsureWithinDeadline();
                    await NextFrameAsync();
                }
                SaveEvidence(name + ":normal_turn");
            }
            if (failures.Count > 0)
                throw new InvalidOperationException("Multiplayer relic ownership failures: " + string.Join(", ", failures));
            _completedChecks.Add("MultiplayerRelicOwnership:PeerOnly:LocalExtra:BothExtra:NativeFullState:Fork:DisplayOwner");
        }
        finally
        {
            LocalContext.NetId = originalLocal;
            harmony.Unpatch(ready, prefix);
            SaveEvidence("finished");
        }
    }

    private async Task WaitForRelicExtraTurnAsync(CombatState combat, ulong[] owners,
        CombatRootSnapshot root, Player local, Player peer, int originalPeerTurn)
    {
        int expectedLocalTurn = root.StartTurnNumber + (owners.Contains(local.NetId) ? 1 : 0);
        int expectedPeerTurn = originalPeerTurn + (owners.Contains(peer.NetId) ? 1 : 0);
        while (!CombatManager.Instance.PlayersTakingExtraTurn.Select(player => player.NetId).SequenceEqual(owners)
            || local.PlayerCombatState!.TurnNumber != expectedLocalTurn
            || peer.PlayerCombatState!.TurnNumber != expectedPeerTurn
            || combat.Players.Where(player => owners.Contains(player.NetId))
                .Any(player => player.PlayerCombatState!.Phase != PlayerTurnPhase.Play))
        {
            EnsureWithinDeadline();
            await NextFrameAsync();
        }
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
    }

    private static bool MultiplayerRelicReadyPrefix(CombatTurnState __0, ref bool __result)
    {
        __result = __0.PlayersReadyToEndTurn.Count == __0.State.Players.Count
            && __0.State.CurrentSide == CombatSide.Player;
        return false;
    }
}
