using System.Reflection;
using System.Text.Json;
using CombatSolver;
using CombatSolver.Engine.InCombat.Simulation;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

namespace OfflineSearchHarness;

internal static class MultiplayerDeadTeammateContracts
{
    private static bool ClockPrefix(ref ulong __result) { __result = 0; return false; }

    private static bool ReadyPrefix(CombatTurnState __0, ref bool __result)
    {
        __result = __0.PlayersReadyToEndTurn.Count == __0.State.Players.Count
            && __0.State.CurrentSide == CombatSide.Player;
        return false;
    }

    internal static string Run(CombatState state, HarnessOptions options, MainLoopContext loop)
    {
        Player local = LocalContext.GetMe(state)!;
        if (state.Players.Count != 2 || ReferenceEquals(local, state.Players[0]))
            throw new InvalidOperationException("Dead teammate fixture needs two players and local index 1.");
        Player peer = state.Players[0];
        void Native(Task task) => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(20), "Dead teammate native action");
        var clock = AccessTools.Method(typeof(Godot.Time), nameof(Godot.Time.GetTicksMsec));
        var prefix = AccessTools.Method(typeof(MultiplayerDeadTeammateContracts), nameof(ClockPrefix));
        GameBootstrap.Harmony.Patch(clock, prefix: new HarmonyMethod(prefix));
        // Match multiplayer ready counts while the harness uses an offline transport.
        var ready = AccessTools.Method(typeof(CombatManager), "AllPlayersReadyToEndTurn", [typeof(CombatTurnState)]);
        var readyPrefix = AccessTools.Method(typeof(MultiplayerDeadTeammateContracts), nameof(ReadyPrefix));
        GameBootstrap.Harmony.Patch(ready, prefix: new HarmonyMethod(readyPrefix));
        try
        {
            foreach (Player player in state.Players)
                Native(CardPileCmd.RemoveFromCombat(player.PlayerCombatState!.AllCards.ToArray(), skipVisuals: true));
            Native(CardPileCmd.AddGeneratedCardToCombat(state.CreateCard(ModelDb.Card<StrikeIronclad>(), local),
                PileType.Hand, local));
            local.Creature.SetMaxHpInternal(500);
            local.Creature.SetCurrentHpInternal(500);
            peer.AddRelicInternal(ModelDb.Relic<PaelsEye>().ToMutable());
            peer.AddRelicInternal(ModelDb.Relic<Pocketwatch>().ToMutable());
            Native(PowerCmd.Apply<IllusionPower>(new ThrowingPlayerChoiceContext(), state.Enemies[0], 1, null, null));
            Native(PowerCmd.Remove(state.Enemies[0].GetPower<MinionPower>()!));
            Native(PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), peer.Creature, 2, peer.Creature, null));
            Native(PowerCmd.Apply<PlatingPower>(new ThrowingPlayerChoiceContext(), peer.Creature, 7, peer.Creature, null));
            Native(PowerCmd.Apply<PanachePower>(new ThrowingPlayerChoiceContext(), peer.Creature, 10, peer.Creature, null));
            Native(PowerCmd.Apply<SelfFormingClayPower>(new ThrowingPlayerChoiceContext(), peer.Creature, 3, peer.Creature, null));
            Native(PowerCmd.Apply<FlexPotionPower>(new ThrowingPlayerChoiceContext(), peer.Creature, 4, peer.Creature, null));
            Native(PowerCmd.Apply<SpeedPotionPower>(new ThrowingPlayerChoiceContext(), peer.Creature, 3, peer.Creature, null));
            Native(PowerCmd.Apply<HotfixPower>(new ThrowingPlayerChoiceContext(), peer.Creature, 2, peer.Creature, null));
            Native(PowerCmd.Apply<DemonFormPower>(new ThrowingPlayerChoiceContext(), peer.Creature, 1, peer.Creature, null));
            Native(PowerCmd.Apply<RitualPower>(new ThrowingPlayerChoiceContext(), peer.Creature, 1, peer.Creature, null));
            Native(PowerCmd.Apply<IntangiblePower>(new ThrowingPlayerChoiceContext(), peer.Creature, 2, peer.Creature, null));
            Native(PowerCmd.Apply<FlameBarrierPower>(new ThrowingPlayerChoiceContext(), peer.Creature, 4, peer.Creature, null));
            Native(PotionCmd.TryToProcure(ModelDb.Potion<FairyInABottle>().ToMutable(), peer));
            AssertDeathPrevention(state, peer, Native);
            Native(PotionCmd.TryToProcure(ModelDb.Potion<FairyInABottle>().ToMutable(), peer));
            CombatRootSnapshot before = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
            CombatPredictionSimulator predictedDeath = before.ForkSimulator();
            using (SimulationNotificationIsolation.Enter())
                if (!predictedDeath.Kill(peer.Creature, force: true))
                    throw new InvalidOperationException("Simulated teammate death did not complete.");
            string manual = MultiplayerStartContracts.Run(state, options, loop, () =>
            {
                Native(CreatureCmd.Kill(peer.Creature, force: true));
                Native(RunManager.Instance.ActionExecutor.FinishedExecutingActions());
                AssertNative(state, before, predictedDeath, before.StartTurnNumber, "teammate death");
            });
            Console.WriteLine($"DEAD_TEAMMATE native_dead={peer.Creature.IsDead} players={state.Players.Count} "
                + $"in_roster={state.Creatures.Contains(peer.Creature)} combat_state={peer.PlayerCombatState != null}");
            CombatRootSnapshot root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
            AssertFrozen(before, "alive root after native death");
            AssertRevival(state, root, peer, Native);
            SearchPolicySnapshot policy = new MultiplayerSearchPolicy(Horizon: 2).Apply(
                SolverController.CaptureSearchPolicy(SolverSettings.Capture(), state, false, null));
            var solver = new CombatBeamSolver(root, SolverDisplayNames.Capture(state), BattleDamageTracker.Observe(state),
                policy, searchProfile: policy.Profile);
            AssertActivationFingerprint(root, solver, peer);
            SolverResult result = SolverController.CurrentResultForBugReport!;
            if (!result.IsMultiplayerAdvice || !peer.Creature.IsDead || !local.Creature.IsAlive)
                throw new InvalidOperationException("Dead teammate search changed live state or lost advisory mode.");
            SimulationSnapshot predicted = solver.ReplayMultiplayerForTesting(
                [new(PlanActionKind.EndTurn, root.StartTurnNumber)]);
            int turn = local.PlayerCombatState!.TurnNumber;
            CombatManager.Instance.SetReadyToEndTurn(local, canBackOut: false);
            DateTime deadline = DateTime.UtcNow.AddSeconds(20);
            while (local.PlayerCombatState.TurnNumber == turn || local.PlayerCombatState.Phase != PlayerTurnPhase.Play)
            {
                if (DateTime.UtcNow >= deadline) throw new TimeoutException("Dead teammate native turn did not finish.");
                loop.Pump(TimeSpan.FromMilliseconds(10));
            }
            ContinuationStamp actual = ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true);
            ContinuationStamp expected = ContinuationStamp.CapturePredicted(local, predicted.Simulator,
                predicted.Turn, root.Forecast, root.StartTurnNumber);
            CombatRootSnapshot nextRoot = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
            var nextSolver = new CombatBeamSolver(nextRoot, SolverDisplayNames.Capture(state),
                BattleDamageTracker.Observe(state), policy, searchProfile: policy.Profile);
            Task<SolverResult> search = Task.Run(nextSolver.Solve);
            loop.RunUntilCompleted(search, TimeSpan.FromSeconds(20), "Dead teammate next turn search");
            _ = search.GetAwaiter().GetResult();
            if (actual.StateText != expected.StateText)
                throw new InvalidOperationException("Dead teammate native turn differs: "
                    + string.Join("; ", expected.DescribeDifferences(actual, maximumDifferences: 12)));
            predicted.ReleaseSimulator();
            AssertFrozen(root, "dead root after native turn");
            File.WriteAllText(Path.Combine(options.OutputDirectory, "dead-teammate.json"),
                JsonSerializer.Serialize(new { status = "Passed", result.ExpandedNodes, result.SearchedTurns,
                    root.PlayerCount, localIndex = 1, deathPrevention = "equal", deathAndRevival = "equal",
                    nextPlayerTurn = "equal", frozenRoots = true, activationFingerprint = true },
                    new JsonSerializerOptions { WriteIndented = true }));
            return "native_teammate_death=root_capture_and_search_passed native_turn=equal "
                + "death_prevention=equal revival=equal fork_and_fingerprint=passed local_index=1 " + manual;
        }
        finally
        {
            GameBootstrap.Harmony.Unpatch(clock, prefix);
            GameBootstrap.Harmony.Unpatch(ready, readyPrefix);
        }
    }

    private static void AssertNative(CombatState state, CombatRootSnapshot root,
        CombatPredictionSimulator predicted, int turn, string context)
    {
        ContinuationStamp expected = ContinuationStamp.CapturePredicted(root.PlayerIdentity, predicted,
            turn, root.Forecast, root.StartTurnNumber);
        ContinuationStamp actual = ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true);
        if (actual.StateText != expected.StateText)
            throw new InvalidOperationException(context + ": "
                + string.Join("; ", expected.DescribeDifferences(actual, maximumDifferences: 12)));
    }

    private static void AssertFrozen(CombatRootSnapshot root, string context)
    {
        ContinuationStamp frozen = ContinuationStamp.CapturePredicted(root.PlayerIdentity, root.ForkSimulator(),
            root.StartTurnNumber, root.Forecast, root.StartTurnNumber);
        if (frozen.StateText != root.ContinuationStamp.StateText)
            throw new InvalidOperationException(context + " was changed by live advancement.");
    }

    private static void AssertDeathPrevention(CombatState state, Player peer, Action<Task> native)
    {
        CombatRootSnapshot root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        CombatPredictionSimulator predicted = root.ForkSimulator();
        using (SimulationNotificationIsolation.Enter())
            if (!predicted.Kill(peer.Creature)) throw new InvalidOperationException("Death prevention was interrupted.");
        native(CreatureCmd.Kill(peer.Creature));
        AssertNative(state, root, predicted, root.StartTurnNumber, "teammate death prevention");
        if (!peer.Creature.IsAlive || !((SimulatedCombatState)predicted.State.CombatState).IsPlayerActiveForHooks(peer)
            || peer.Potions.Any(potion => potion is FairyInABottle))
            throw new InvalidOperationException("Fairy must save its owner before hooks deactivate and be consumed.");
        AssertFrozen(root, "alive root after native death prevention");
    }

    private static void AssertActivationFingerprint(CombatRootSnapshot root, CombatBeamSolver solver, Player peer)
    {
        var snapshot = typeof(CombatBeamSolver).GetMethod("Snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<CombatPredictionSimulator, int, int, int, SearchBoundaryReason,
                IReadOnlySet<uint>, SimulationSnapshot>>(solver);
        StateFingerprint Key(CombatPredictionSimulator simulator)
        {
            SimulationSnapshot result = snapshot(simulator, root.StartTurnNumber, 1, 0, SearchBoundaryReason.None,
                new HashSet<uint>());
            result.ReleaseSimulator();
            return result.StateKey;
        }
        CombatPredictionSimulator parent = root.ForkSimulator();
        CombatPredictionSimulator child = parent.Fork();
        ((SimulatedCombatState)child.State.CombatState).SetPlayerActiveForHooks(peer, true);
        using (SimulationNotificationIsolation.Enter())
        {
            StateFingerprint parentKey = Key(parent), childKey = Key(child);
            if (parentKey == childKey || childKey != Key(child.Fork()) || parentKey != Key(parent))
                throw new InvalidOperationException("Production state key must distinguish activation and isolate forks.");
        }
    }

    private static void AssertRevival(CombatState state, CombatRootSnapshot root, Player peer, Action<Task> native)
    {
        CombatPredictionSimulator parent = root.ForkSimulator();
        CombatPredictionSimulator revived = parent.Fork();
        using (SimulationNotificationIsolation.Enter()) revived.Heal(peer.Creature, 20);
        native(CreatureCmd.Heal(peer.Creature, 20));
        AssertNative(state, root, revived, root.StartTurnNumber, "teammate revival");
        var combat = (SimulatedCombatState)revived.State.CombatState;
        if (!combat.IsPlayerActiveForHooks(peer)
            || !combat.IterateHookListeners().OfType<RelicModel>().Any(relic => relic.Owner == peer)
            || !combat.IterateHookListeners().OfType<PowerModel>().Any(power => power.Owner == peer.Creature)
            || !combat.IterateHookListeners().OfType<PotionModel>().Any(potion => potion.Owner == peer))
            throw new InvalidOperationException("Revival did not restore the peer's captured listeners.");
        if (((SimulatedCombatState)parent.State.CombatState).IsPlayerActiveForHooks(peer))
            throw new InvalidOperationException("Revival leaked into the parent branch.");
        native(CreatureCmd.Kill(peer.Creature, force: true));
        using (SimulationNotificationIsolation.Enter())
            if (!revived.Kill(peer.Creature, force: true)) throw new InvalidOperationException("Second death was interrupted.");
        AssertNative(state, root, revived, root.StartTurnNumber, "teammate death after revival");
        AssertFrozen(root, "dead root after native revival");
    }
}
