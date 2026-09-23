using CombatSolver.Engine.InCombat.Simulation;
using CombatSolver.Engine.InCombat.Mirrors;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private static List<string>? _multiplayerTurnStartEvents;

    private static void ObserveMultiplayerSideStart(CombatSide __1)
        => _multiplayerTurnStartEvents!.Add("side:" + __1);

    private static void ObserveMultiplayerPlayerStart(Player __1)
        => _multiplayerTurnStartEvents!.Add("player:" + __1.NetId);

    private async Task AssertMultiplayerSharedDamageAsync(CombatState combat)
    {
        Player local = LocalContext.GetMe(combat) ?? throw new InvalidOperationException("Missing local player.");
        Player peer = combat.Players.Single(player => player != local);
        var enemy = combat.Enemies.Single();
        List<string> checks = [];
        void Check(bool passed, string name)
        {
            if (!passed) throw new InvalidOperationException("Shared damage contract: " + name);
            checks.Add(name);
        }
        foreach (Player player in combat.Players)
        {
            foreach (var relic in player.Relics.ToArray()) await RelicCmd.Remove(relic);
            foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            foreach (var potion in player.PotionSlots.ToArray()) potion?.Discard();
            await ClearPlayerPilesAsync(player);
            player.Creature.SetMaxHpInternal(80);
            player.Creature.SetCurrentHpInternal(80);
            for (int index = 0; index < 12; index++)
                await InjectCardAsync(combat, player, new() { CardId = "DEFEND_IRONCLAD", Pile = "Draw" });
        }
        enemy.SetMaxHpInternal(200);
        enemy.SetCurrentHpInternal(200);
        await PowerCmd.Apply<PoisonPower>(new ThrowingPlayerChoiceContext(), enemy, 5, local.Creature, null);
        await PowerCmd.Apply<PoisonPower>(new ThrowingPlayerChoiceContext(), enemy, 3, peer.Creature, null);
        await InjectCardAsync(combat, local, new() { CardId = "DEADLY_POISON", Pile = "Hand" });
        await InjectCardAsync(combat, local, new() { CardId = "STRIKE_IRONCLAD", Pile = "Hand" });
        await InjectCardAsync(combat, local, new() { CardId = "DEFEND_IRONCLAD", Pile = "Hand" });
        var root = CombatRootSnapshot.Capture(combat, multiplayerAdvisor: true);
        var session = new MultiplayerContributionSession();
        var initial = session.Observe(root.MultiplayerObservation!);
        var prediction = root.ForkSimulator();
        var shadow = (SimulatedCombatState)prediction.State.CombatState;
        var sibling = prediction.Fork();
        Check(CorePowerSupport.TriggerPoison(prediction, shadow, [enemy]), "predicted_poison_settles");
        await enemy.GetPower<PoisonPower>()!.Trigger();
        var expected = ContinuationStamp.CapturePredicted(local, prediction, root.StartTurnNumber, root.Forecast, root.StartTurnNumber);
        var actual = ContinuationStamp.CaptureLive(combat, multiplayerAdvisor: true);
        Check(expected.StateText == actual.StateText, "mixed_poison_full_native_state");
        Check(shadow.AdvisorLocalDamage == 0 && shadow.AdvisorTotalDamage == 8 && shadow.AdvisorUnattributedDamage == 8,
            "poison_stays_unattributed");
        Check(((SimulatedCombatState)sibling.State.CombatState).AdvisorUnattributedDamage == 0,
            "sibling_shared_ledger_isolated");
        Check(ContinuationStamp.CapturePredicted(local, root.ForkSimulator(), root.StartTurnNumber,
            root.Forecast, root.StartTurnNumber).StateText == root.ContinuationStamp.StateText, "root_stays_frozen");
        var observed = CombatRootSnapshot.Capture(combat, multiplayerAdvisor: true);
        var goal = session.Observe(observed.MultiplayerObservation!);
        Check(goal.PaidDamage == 0 && goal.ObservedUnattributedDamage == 8
            && goal.SharedProgress(enemy.CurrentHp, 0, 0, 0) == 4, "native_observation_shared_not_personal");
        Check(goal.DeadlineRound == initial.DeadlineRound, "poison_recalculation_does_not_extend_deadline");
        await CreatureCmd.Heal(enemy, 3);
        observed = CombatRootSnapshot.Capture(combat, multiplayerAdvisor: true);
        goal = session.Observe(observed.MultiplayerObservation!);
        Check(goal.SharedProgress(enemy.CurrentHp, 0, 0, 0) == 1, "healing_undoes_shared_progress_once");
        Check(session.Observe(observed.MultiplayerObservation!) == goal, "unchanged_recalculation_is_idempotent");
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), new[] { enemy }, 6, ValueProp.Unpowered, peer.Creature);
        observed = CombatRootSnapshot.Capture(combat, multiplayerAdvisor: true);
        goal = session.Observe(observed.MultiplayerObservation!);
        Check(goal.SharedProgress(enemy.CurrentHp, 0, 0, 0) == 1 && goal.ObservedUnattributedDamage == 8,
            "known_peer_damage_never_becomes_shared");
        var child = prediction.Fork();
        var childState = (SimulatedCombatState)child.State.CombatState;
        Check(CorePowerSupport.TriggerPoison(child, childState, [enemy]), "child_poison_settles");
        Check(childState.AdvisorUnattributedDamage == 15 && shadow.AdvisorUnattributedDamage == 8,
            "child_accumulates_without_mutating_parent");
        MultiplayerRootObservation Synthetic(int hp, long localDamage, long totalDamage, long shared, ulong[] players)
            => new(1, hp, 80, 80, players, localDamage, totalDamage) { UnattributedDamage = shared };
        var three = MultiplayerContributionObjective.Start(Synthetic(300, 0, 0, 0, [1, 2, 3]), 14);
        Check(three.SharedProgress(291, 0, 9, 9) == 3, "three_players_share_without_rounding_each_tick");
        Check(three.SharedProgress(299, 0, 1, 1) == 1d / 3, "fractional_shared_progress_preserved");
        var roster = new MultiplayerContributionSession();
        roster.Observe(Synthetic(300, 0, 0, 0, [1, 2, 3]));
        roster.Observe(Synthetic(291, 0, 9, 9, [1, 2, 3]));
        var changed = roster.Observe(Synthetic(291, 0, 9, 9, [1, 2]));
        Check(changed.ObservedUnattributedDamage == 0 && changed.DeadlineRound == 3,
            "roster_change_rebases_shared_history_without_extension");

        var readyMethod = AccessTools.Method(typeof(CombatManager), "AllPlayersReadyToEndTurn", [typeof(CombatTurnState)]);
        var prefix = AccessTools.Method(typeof(UnattendedTestRunner), nameof(MultiplayerRelicReadyPrefix));
        var harmony = new Harmony("CombatSolver.Tests.SharedDamage");
        harmony.Patch(readyMethod, prefix: new HarmonyMethod(prefix));
        try
        {
            CombatManager.Instance.SetReadyToEndTurn(peer, true);
            Check(CombatManager.Instance.IsPlayerReadyToEndTurn(peer), "ready_observed");
            CombatManager.Instance.UndoReadyToEndTurn(peer);
            Check(!CombatManager.Instance.IsPlayerReadyToEndTurn(peer), "ready_can_be_undone");
            root = CombatRootSnapshot.Capture(combat, multiplayerAdvisor: true);
            var policy = new MultiplayerSearchPolicy(Horizon: 3)
                .Apply(SolverController.CaptureSearchPolicy(SolverSettings.Capture(), combat, false, null)) with
            {
                FixedBudget = true, Profile = new(6, 100, 12, 4, 4, 1000), MaxDegreeOfParallelism = 1,
                VerifyIncrementalSearch = true, PotionPolicy = SolverPotionPolicy.Disabled,
                PotionStrategy = new(SolverPotionPolicy.Disabled, []),
            };
            var solver = new CombatBeamSolver(root, SolverDisplayNames.Capture(combat), BattleDamageTracker.Observe(combat),
                policy, searchProfile: policy.Profile);
            var search = Task.Run(solver.Solve);
            while (!search.IsCompleted) { EnsureWithinDeadline(); await NextFrameAsync(); }
            var result = await search;
            Check(result.ExpandedNodes <= 100 && result.AdvisoryComparisonCycles > 0,
                "shared_scoring_incremental_replay_and_fixed_budget");
            var sideStart = AccessTools.Method(typeof(HookMirrors), nameof(HookMirrors.BeforeSideTurnStart));
            var playerStart = AccessTools.Method(typeof(HookMirrors), nameof(HookMirrors.AfterPlayerTurnStart));
            var sideProbe = AccessTools.Method(typeof(UnattendedTestRunner), nameof(ObserveMultiplayerSideStart));
            var playerProbe = AccessTools.Method(typeof(UnattendedTestRunner), nameof(ObserveMultiplayerPlayerStart));
            SimulationSnapshot next;
            _multiplayerTurnStartEvents = [];
            try
            {
                // Observe the production replay entry points without replacing their effects.
                harmony.Patch(sideStart, prefix: new HarmonyMethod(sideProbe));
                harmony.Patch(playerStart, prefix: new HarmonyMethod(playerProbe));
                next = solver.ReplayMultiplayerForTesting([new(PlanActionKind.EndTurn, root.StartTurnNumber)]);
                Check(_multiplayerTurnStartEvents.SequenceEqual(new[]
                {
                    "side:Enemy", "side:Player",
                }.Concat(combat.Players.Select(player => "player:" + player.NetId))),
                    "multiplayer_cycle_uses_registered_turn_start_facades_in_order");
            }
            finally
            {
                harmony.Unpatch(sideStart, sideProbe);
                harmony.Unpatch(playerStart, playerProbe);
                _multiplayerTurnStartEvents = null;
            }
            expected = ContinuationStamp.CapturePredicted(local, next.Simulator, next.Turn, root.Forecast, root.StartTurnNumber);
            int turn = local.PlayerCombatState!.TurnNumber;
            foreach (Player player in combat.Players) CombatManager.Instance.SetReadyToEndTurn(player, false);
            while (local.PlayerCombatState.TurnNumber == turn || local.PlayerCombatState.Phase != PlayerTurnPhase.Play)
            { EnsureWithinDeadline(); await NextFrameAsync(); }
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
            actual = ContinuationStamp.CaptureLive(combat, multiplayerAdvisor: true);
            Check(expected.StateText == actual.StateText, "poison_full_party_native_cycle_and_draw_equal");
            var cycleRoot = CombatRootSnapshot.Capture(combat, multiplayerAdvisor: true);
            Check(cycleRoot.MultiplayerObservation!.UnattributedDamage - root.MultiplayerObservation!.UnattributedDamage
                == next.AdvisoryUnattributedDamage, "native_cycle_shared_damage_ledger_equal");
            next.ReleaseSimulator();
        }
        finally { harmony.Unpatch(readyMethod, prefix); }
        _writer.WriteGeneratedArtifact("multiplayer-shared-damage.json", new { checks, passed = true,
            nativeFullStateCompared = true, networkValidated = false });
        _completedChecks.Add("MultiplayerSharedDamage:" + checks.Count + ":NativeState:Fork:Ledger:Incremental");
    }
}
