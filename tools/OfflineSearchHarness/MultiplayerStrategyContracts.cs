using System.Text.Json;
using CombatSolver;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace OfflineSearchHarness;

internal static class MultiplayerStrategyContracts
{
    private sealed record Outcome(string Name, int Hp, int ExpectedLoss, int ExpectedDamage,
        int ActualLoss, int ActualDamage, string[] Cards, int Expanded, string Boundary);

    public static string Run(CombatState state, HarnessOptions options, MainLoopContext loop)
    {
        GameBootstrap.Harmony.Patch(AccessTools.Method(typeof(Godot.Time), nameof(Godot.Time.GetTicksMsec)),
            prefix: new HarmonyMethod(typeof(MultiplayerStrategyContracts), nameof(ClockPrefix)));
        Player local = LocalContext.GetMe(state)!;
        if (state.Players.Count != 2 || ReferenceEquals(local, state.Players[0]) || state.Enemies.Count != 1)
            throw new InvalidOperationException("Strategy fixture requires two players, local index 1 and one enemy.");
        void Native(Task task) => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(20), "Strategy fixture setup");
        foreach (Player player in state.Players)
        {
            foreach (var potion in player.PotionSlots.ToArray()) potion?.Discard();
            Native(CardPileCmd.RemoveFromCombat(player.PlayerCombatState!.AllCards.ToArray(), skipVisuals: true));
        }
        Player peer = state.Players[0];
        peer.Creature.SetMaxHpInternal(500);
        peer.Creature.SetCurrentHpInternal(500);
        var enemy = state.Enemies[0];
        enemy.SetMaxHpInternal(500);
        enemy.SetCurrentHpInternal(500);
        foreach (CardModel model in new CardModel[] { ModelDb.Card<StrikeIronclad>(), ModelDb.Card<DefendIronclad>() })
            Native(CardPileCmd.AddGeneratedCardToCombat(state.CreateCard(model, local), PileType.Hand, local));

        SearchPolicySnapshot policy = new MultiplayerSearchPolicy(Horizon: 1).Apply(
            SolverController.CaptureSearchPolicy(SolverSettings.Capture(), state, includeTurnSetup: false, theftPolicy: null));
        policy = policy with { Profile = policy.Profile with { BeamWidth = 2 }, MaxDegreeOfParallelism = 1 };
        CombatBeamSolver Solver(CombatRootSnapshot root, bool verify = false) => new(root, SolverDisplayNames.Capture(state),
            BattleDamageTracker.Observe(state), policy with { VerifyIncrementalSearch = verify }, searchProfile: policy.Profile);
        var initialRoot = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        var probe = Solver(initialRoot).ReplayMultiplayerForTesting([new(PlanActionKind.EndTurn, initialRoot.StartTurnNumber)]);
        int incoming = probe.CumulativePlayerHpLost;
        probe.ReleaseSimulator();
        if (incoming < 4 || incoming > 30)
            throw new InvalidOperationException($"Strategy fixture needs an opening attack of 4..30 HP, got {incoming}.");

        VerifyPowerStrategyIsolation(state, local, policy, incoming, loop, options);

        List<Outcome> outcomes = [];
        void Check(string name, int remainingIncoming, int hp, int energy, int expectedLoss, int expectedDamage,
            int enemyHp = 500)
        {
            local.Creature.SetCurrentHpInternal(hp);
            enemy.SetCurrentHpInternal(enemyHp);
            int block = incoming - remainingIncoming;
            if (local.Creature.Block > block)
                Native(CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), local.Creature,
                    local.Creature.Block - block, null));
            else if (local.Creature.Block < block)
                Native(CreatureCmd.GainBlock(local.Creature, block - local.Creature.Block,
                    ValueProp.Unpowered, null, fast: true));
            var playerState = local.PlayerCombatState!;
            if (playerState.Energy > energy) Native(PlayerCmd.LoseEnergy(playerState.Energy - energy, local));
            else if (playerState.Energy < energy) Native(PlayerCmd.GainEnergy(energy - playerState.Energy, local));
            Native(RunManager.Instance.ActionExecutor.FinishedExecutingActions());
            var root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
            Task<SolverResult> search = Task.Run(Solver(root, verify: name == "allow_3").Solve);
            loop.RunUntilCompleted(search, TimeSpan.FromSeconds(30), name);
            SolverResult result = search.GetAwaiter().GetResult();
            outcomes.Add(new Outcome(name, hp, expectedLoss, expectedDamage,
                result.Snapshot.CumulativePlayerHpLost, enemyHp - result.Snapshot.EnemyHp,
                result.BestNode.Actions.Where(action => action.Kind == PlanActionKind.PlayCard)
                    .Select(action => action.CardId).ToArray(), result.ExpandedNodes, result.BoundaryReason.ToString()));
            HarnessLog.Trace($"{name}: hp_lost={result.Snapshot.CumulativePlayerHpLost} enemy_damage={enemyHp - result.Snapshot.EnemyHp}");
            if (result.ExpandedNodes > policy.Profile.MaxExpandedNodes)
                throw new InvalidOperationException("Strategy lanes exceeded the node budget.");
            if (result.AdvisoryHpLossAllowance != 3 || !SolverOverlaySnapshot.Capture(result, unexpectedReplan: false)
                    .SummaryText.Contains(SolverText.Format($"输出优先：单回合扣血目标不超过 {3} 点；当前预测最高 {result.AdvisoryMaximumCycleHpLost} 点。")))
                throw new InvalidOperationException("Advisory UI does not describe the selected HP allowance.");
        }

        Check("allow_1", 1, 40, 1, 1, 6);
        Check("allow_2", 2, 40, 1, 2, 6);
        Check("allow_3", 3, 40, 1, 3, 6);
        Check("defend_above_3", 4, 40, 1, 0, 0);
        Check("avoid_lethal_3", 3, 3, 1, 0, 0);
        Check("no_need_to_defend", 0, 40, 1, 0, 6);
        Check("equal_damage_save_hp", 3, 40, 2, 0, 6);
        Check("take_lethal", 3, 40, 1, 0, 6, enemyHp: 6);
        VerifyBudgetBookkeeping();
        enemy.SetCurrentHpInternal(500);
        VerifyNativeBoundary(state, local, policy, loop, options);

        // Damage already paid before a manual recalculation must not grant a fresh allowance.
        CardModel bloodletting = state.CreateCard(ModelDb.Card<Bloodletting>(), local);
        Native(CardPileCmd.AddGeneratedCardToCombat(bloodletting, PileType.Hand, local));
        int priorLoss = CombatRootSnapshot.Capture(state, true).InitialPlayerRoundHpLost;
        Native(CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), bloodletting, null, skipCardPileVisuals: true));
        Native(CardPileCmd.RemoveFromCombat([bloodletting], skipVisuals: true));
        Native(CreatureCmd.Heal(local.Creature, 5));
        var paidRoot = CombatRootSnapshot.Capture(state, true);
        if (paidRoot.InitialPlayerRoundHpLost != priorLoss + 3)
            throw new InvalidOperationException("Recalculation/healing reset damage already paid this round.");
        if (local.Creature.Block > 0)
            Native(CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), local.Creature, local.Creature.Block, null));
        paidRoot = CombatRootSnapshot.Capture(state, true);
        var paidProbe = Solver(paidRoot).ReplayMultiplayerForTesting([new(PlanActionKind.EndTurn, paidRoot.StartTurnNumber)]);
        incoming = paidProbe.CumulativePlayerHpLost;
        paidProbe.ReleaseSimulator();
        if (incoming != 0)
            throw new InvalidOperationException("Recalculation fixture requires the enemy's setup turn.");
        foreach (CardModel model in new CardModel[] { ModelDb.Card<Bloodletting>(), ModelDb.Card<StrikeIronclad>() })
            Native(CardPileCmd.AddGeneratedCardToCombat(state.CreateCard(model, local), PileType.Hand, local));
        policy = policy with { Profile = policy.Profile with { BeamWidth = 12 } };
        Check("already_paid_before_recalculation", 0, 40, 1, 0, 6);
        File.WriteAllText(Path.Combine(options.OutputDirectory, "strategy-results.json"),
            JsonSerializer.Serialize(outcomes, UnattendedTestFiles.JsonOptions));
        foreach (Outcome outcome in outcomes)
        {
            if (outcome.ActualLoss != outcome.ExpectedLoss || outcome.ActualDamage != outcome.ExpectedDamage)
                throw new InvalidOperationException($"{outcome.Name}: expected loss/damage {outcome.ExpectedLoss}/{outcome.ExpectedDamage}, "
                    + $"got {outcome.ActualLoss}/{outcome.ActualDamage}; cards={string.Join(',', outcome.Cards)}.");
        }
        return $"cases={outcomes.Count} allowance=1,2,3 above_limit=defend lethal=avoid equal_damage=save_hp "
            + "incremental=equal native_next_turn=equal healing=no_reset allowance=no_carry fork=isolated; "
            + "single_player_power_strategy=isolated; "
            + "beam=2 for two-card cases, beam=12 for paid-HP setup; shared node/time limits; no networking";
    }

    private static void VerifyPowerStrategyIsolation(CombatState state, Player local, SearchPolicySnapshot policy,
        int incoming, MainLoopContext loop, HarnessOptions options)
    {
        void Native(Task task) => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(20), "Power isolation setup");
        CardModel power = state.CreateCard(ModelDb.Card<Inflame>(), local);
        CardModel attack = state.CreateCard(ModelDb.Card<StrikeIronclad>(), local);
        Native(CardPileCmd.AddGeneratedCardToCombat(power, PileType.Hand, local));
        Native(CardPileCmd.AddGeneratedCardToCombat(attack, PileType.Hand, local));
        Native(CreatureCmd.GainBlock(local.Creature, incoming, ValueProp.Unpowered, null, fast: true));
        Native(RunManager.Instance.ActionExecutor.FinishedExecutingActions());
        var diagnostics = new System.Collections.Concurrent.ConcurrentQueue<string>();
        SearchPolicySnapshot advisory = new MultiplayerSearchPolicy(Horizon: 1).Apply(policy with
        {
            UseBeamWidthPortfolio = true,
            UseNoveltyPortfolio = true,
            Profile = policy.Profile with { BeamWidth = 12, AggressivePowerCommitment = true },
            Diagnostics = new SearchDiagnosticsSink(message =>
            {
                if (message.Contains("POWER_COMMITMENTS scope=solver", StringComparison.Ordinal))
                    diagnostics.Enqueue(message);
            }, _ => { }),
        });
        CombatRootSnapshot root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        SolverDisplayNames names = SolverDisplayNames.Capture(state);
        BattleDamageSnapshot damage = BattleDamageTracker.Observe(state);
        Task<SolverResult> search = Task.Run(() => CombatSearchCoordinator.Solve(
            root, names, damage, advisory, CancellationToken.None, progressCallback: null));
        loop.RunUntilCompleted(search, TimeSpan.FromSeconds(30), "Multiplayer power isolation");
        SolverResult result = search.GetAwaiter().GetResult();
        string summary = diagnostics.Single();
        File.WriteAllText(Path.Combine(options.OutputDirectory, "power-strategy-isolation.txt"), summary + "\n"
            + $"single_session={result.SingleSessionSearch} expanded={result.ExpandedNodes} "
            + $"cards={string.Join(',', result.BestNode.Actions.Select(action => action.CardId))}\n");
        if (!result.SingleSessionSearch || result.ExpandedNodes > advisory.Profile.MaxExpandedNodes
            || !result.BestNode.Actions.Any(action => action.CardId == power.Id.Entry))
            throw new InvalidOperationException("Power isolation fixture did not produce a bounded advisory power route.");
        foreach (string counter in new[] { "candidates", "frontier_evaluations", "created", "admitted",
                     "expired", "realized", "seats_peak" })
        {
            if (!summary.Split(' ').Contains($"{counter}=0", StringComparer.Ordinal))
                throw new InvalidOperationException($"Single-player power strategy entered multiplayer advice: {summary}");
        }
        Native(CardPileCmd.RemoveFromCombat([power, attack], skipVisuals: true));
    }

    private static void VerifyBudgetBookkeeping()
    {
        MultiplayerHpLossBudget empty = default;
        var distributed = empty.Advance(3, true, 3).Advance(3, true, 3);
        var concentrated = empty.Advance(0, true, 3).Advance(6, true, 3);
        var splitActions = empty.Advance(2, false, 3).Advance(2, false, 3);
        if (distributed.ExcessHpLost(3) != 0 || concentrated.ExcessHpLost(3) != 3
            || splitActions.ExcessHpLost(3) != 1 || concentrated.MaximumCycleHpLost != 6)
            throw new InvalidOperationException("Damage allowance carries across rounds or resets between actions.");
    }

    private static void VerifyNativeBoundary(CombatState state, Player local, SearchPolicySnapshot policy,
        MainLoopContext loop, HarnessOptions options)
    {
        GameBootstrap.Harmony.Patch(AccessTools.Method(typeof(CombatManager), "AllPlayersReadyToEndTurn", [typeof(CombatTurnState)]),
            prefix: new HarmonyMethod(typeof(MultiplayerStrategyContracts), nameof(ReadyPrefix)));
        loop.RunUntilCompleted(PowerCmd.Apply<CrimsonMantlePower>(new ThrowingPlayerChoiceContext(), local.Creature, 1,
            local.Creature, null), TimeSpan.FromSeconds(20), "Add next-turn HP cost");
        int startCost = ModelDb.Power<CrimsonMantlePower>().DynamicVars["SelfDamage"].IntValue;
        var root = CombatRootSnapshot.Capture(state, true);
        SearchPolicySnapshot boundaryPolicy = new MultiplayerSearchPolicy(Horizon: 2).Apply(policy);
        var solver = new CombatBeamSolver(root, SolverDisplayNames.Capture(state), BattleDamageTracker.Observe(state),
            boundaryPolicy, searchProfile: boundaryPolicy.Profile);
        SimulationSnapshot before = solver.ReplayMultiplayerForTesting([]);
        SimulationSnapshot after = solver.ReplayMultiplayerForTesting([new(PlanActionKind.EndTurn, root.StartTurnNumber)]);
        SearchNode Node(SimulationSnapshot snapshot, SearchNode? parent) => new(
            parent == null ? null : new(PlanActionKind.EndTurn, root.StartTurnNumber), parent == null ? 0 : 1,
            snapshot.PotionUseCount, snapshot.PotionStrategicCost, snapshot.Turn, SearchRouteTraits.None, 0,
            snapshot.Score, snapshot.StateKey, snapshot.HasRisk, snapshot.BoundaryReason, false, parent, snapshot,
            CombatProgressState.Capture(snapshot));
        SearchNode next = Node(after, Node(before, null));
        if (after.AdvisoryLastEnemyCycleHpLost != 3 || after.CumulativePlayerHpLost != 3 + startCost
            || next.AdvisoryHpLoss.CompletedExcessHpLost != 0 || next.AdvisoryHpLoss.CurrentCycleHpLost != startCost
            || next.AdvisoryHpLoss.MaximumCycleHpLost != Math.Max(3, startCost))
            throw new InvalidOperationException("Next-turn HP cost was charged to the previous enemy cycle.");
        var fork = after.Simulator.Fork();
        var forkCombat = (SimulatedCombatState)fork.State.CombatState;
        if (forkCombat.AdvisorLastEnemyCycleHpLost != 3)
            throw new InvalidOperationException("Cycle checkpoint was not copied into the fork.");
        forkCombat.AdvisorLastEnemyCycleHpLost = 99;
        if (((SimulatedCombatState)after.Simulator.State.CombatState).AdvisorLastEnemyCycleHpLost != 3
            || ((SimulatedCombatState)before.Simulator.State.CombatState).AdvisorLastEnemyCycleHpLost != 0)
            throw new InvalidOperationException("Cycle checkpoint mutations escaped a branch.");
        ContinuationStamp expected = ContinuationStamp.CapturePredicted(local, after.Simulator,
            after.Turn, root.Forecast, root.StartTurnNumber);
        int turn = local.PlayerCombatState!.TurnNumber;
        foreach (Player player in state.Players) CombatManager.Instance.SetReadyToEndTurn(player, canBackOut: false);
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (local.PlayerCombatState.TurnNumber == turn || local.PlayerCombatState.Phase != PlayerTurnPhase.Play)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Strategy native boundary did not finish.");
            loop.Pump(TimeSpan.FromMilliseconds(10));
        }
        ContinuationStamp actual = ContinuationStamp.CaptureLive(state, true);
        if (expected.StateText != actual.StateText)
            throw new InvalidOperationException("Native cycle differs: " + string.Join("; ", expected.DescribeDifferences(actual, 12)));
        File.WriteAllText(Path.Combine(options.OutputDirectory, "native-boundary.txt"), actual.StateText);
        before.ReleaseSimulator();
        after.ReleaseSimulator();
    }

    private static bool ReadyPrefix(CombatTurnState __0, ref bool __result)
    {
        __result = __0.PlayersReadyToEndTurn.Count == __0.State.Players.Count && __0.State.CurrentSide == CombatSide.Player;
        return false;
    }

    private static bool ClockPrefix(ref ulong __result)
    {
        __result = (ulong)Environment.TickCount64;
        return false;
    }
}
