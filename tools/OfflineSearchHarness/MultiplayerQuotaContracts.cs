using System.Text.Json;
using CombatSolver;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace OfflineSearchHarness;

internal static class MultiplayerQuotaContracts
{
    public static string Run(CombatState state, HarnessOptions options, MainLoopContext loop)
    {
        List<string> checks = [];
        void Check(bool passed, string name)
        {
            if (!passed) throw new InvalidOperationException("Quota contract: " + name);
            checks.Add(name);
        }
        VerifyLedger(Check);
        GameBootstrap.Harmony.Patch(AccessTools.Method(typeof(Godot.Time), nameof(Godot.Time.GetTicksMsec)),
            prefix: new HarmonyMethod(typeof(MultiplayerReviewContracts), "ClockPrefix"));
        GameBootstrap.Harmony.Patch(AccessTools.Method(typeof(CombatManager), "AllPlayersReadyToEndTurn",
                [typeof(CombatTurnState)]), prefix: new HarmonyMethod(typeof(MultiplayerQuotaContracts), nameof(ReadyPrefix)));
        Player local = LocalContext.GetMe(state)!, peer = state.Players[0];
        Check(state.Players.Count == 2 && local != peer, "local_index_one");
        void Native(Task task) => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(15), "Quota native contract");
        foreach (var player in state.Players)
        {
            foreach (var potion in player.PotionSlots.ToArray()) potion?.Discard();
            Native(CardPileCmd.RemoveFromCombat(player.PlayerCombatState!.AllCards.ToArray(), skipVisuals: true));
            player.Creature.SetMaxHpInternal(80);
            player.Creature.SetCurrentHpInternal(80);
        }
        Native(PowerCmd.Apply<BufferPower>(new ThrowingPlayerChoiceContext(), peer.Creature, 40, peer.Creature, null));
        var enemy = state.Enemies.Single();
        enemy.SetMaxHpInternal(160);
        enemy.SetCurrentHpInternal(3);
        var overkill = enemy.LoseHpInternal(10, ValueProp.Unpowered);
        Check(overkill.UnblockedDamage == 3 && overkill.OverkillDamage == 7
            && MultiplayerDamageAttribution.HpDamage(overkill) == 3, "native_overkill_counts_only_actual_hp");
        enemy.SetCurrentHpInternal(160);
        CardModel Add<T>(Player player) where T : CardModel
        {
            var card = state.CreateCard(ModelDb.Card<T>(), player);
            Native(CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player));
            return card;
        }
        var strike = Add<StrikeIronclad>(local);
        var peerStrike = Add<StrikeIronclad>(peer);
        var root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        var session = new MultiplayerContributionSession();
        var objective = session.Observe(root.MultiplayerObservation!);
        SearchPolicySnapshot Policy(MultiplayerContributionObjective? goal = null, int horizon = 14, int nodes = 100)
            => new MultiplayerSearchPolicy(horizon) { Objective = goal }.Apply(SolverController.CaptureSearchPolicy(
                SolverSettings.Capture(), state, false, null)) with
            {
                Profile = new(8, nodes, 16, 8, 8, 3000), MaxDegreeOfParallelism = 1, FixedBudget = true,
                PotionPolicy = SolverPotionPolicy.Disabled, PotionStrategy = new(SolverPotionPolicy.Disabled, []),
            };
        CombatBeamSolver Solver(CombatRootSnapshot captured, SearchPolicySnapshot policy)
            => new(captured, SolverDisplayNames.Capture(state), BattleDamageTracker.Observe(state), policy,
                searchProfile: policy.Profile);
        var solver = Solver(root, Policy(objective));
        PlanAction attack = new(PlanActionKind.PlayCard, root.StartTurnNumber, CardId: "STRIKE_IRONCLAD",
            TargetCombatId: enemy.CombatId);
        var predicted = solver.ReplayMultiplayerForTesting([attack]);
        var predictedCombat = (SimulatedCombatState)predicted.Simulator.State.CombatState;
        Check(predicted.AdvisoryLocalDamage == 6 && predicted.AdvisoryTotalDamage == 6
            && predicted.AdvisoryContribution == 6, "local_damage_credited");
        var sibling = predicted.Simulator.Fork();
        var siblingCombat = (SimulatedCombatState)sibling.State.CombatState;
        Check(siblingCombat.AdvisorLocalDamage == 6 && siblingCombat.AdvisorTotalDamage == 6
            && ((SimulatedCombatState)root.ForkSimulator().State.CombatState).AdvisorLocalDamage == 0,
            "damage_ledger_fork_and_root_isolation");
        siblingCombat.RecordDamageReceived(enemy, local.Creature, new DamageResult(enemy, ValueProp.Unpowered)
            { UnblockedDamage = 1 });
        siblingCombat.RecordDamageReceived(enemy, peer.Creature, new DamageResult(enemy, ValueProp.Unpowered)
            { UnblockedDamage = 2 });
        siblingCombat.RecordDamageReceived(enemy, null, new DamageResult(enemy, ValueProp.Unpowered)
            { UnblockedDamage = 3 });
        Check(siblingCombat.AdvisorLocalDamage == 7 && siblingCombat.AdvisorTotalDamage == 12
            && predictedCombat.AdvisorLocalDamage == 6 && predictedCombat.AdvisorTotalDamage == 6,
            "child_foreign_and_unknown_damage_stays_in_child");
        var expected = ContinuationStamp.CapturePredicted(local, predicted.Simulator, predicted.Turn,
            root.Forecast, root.StartTurnNumber);
        Check(strike.TryManualPlay(enemy), "native_local_play_accepted");
        Native(RunManager.Instance.ActionExecutor.FinishedExecutingActions());
        var actual = ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true);
        Check(expected.StateText == actual.StateText, "local_play_full_party_native_equal");
        var localRoot = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        Check(session.Observe(localRoot.MultiplayerObservation!).PaidDamage == 6, "native_recalculation_keeps_paid_damage");
        Check(peerStrike.TryManualPlay(enemy), "native_peer_play_accepted");
        Native(RunManager.Instance.ActionExecutor.FinishedExecutingActions());
        var peerRoot = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        var afterPeer = session.Observe(peerRoot.MultiplayerObservation!);
        Check(afterPeer.PaidDamage == 6 && afterPeer.DeadlineRound == objective.DeadlineRound
            && peerRoot.MultiplayerObservation!.TotalDamage - root.MultiplayerObservation!.TotalDamage == 12,
            "native_peer_damage_does_not_pay_or_postpone_local_quota");
        Native(CreatureCmd.Heal(enemy, 8));
        var healedRoot = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        Check(session.Observe(healedRoot.MultiplayerObservation!).PaidDamage == -2,
            "enemy_healing_reopens_progress");
        Check(predictedCombat.AdvisorLocalDamage == 6 && predicted.AdvisoryContribution == 6
            && solver.ReplayMultiplayerForTesting([attack]).StateKey == predicted.StateKey,
            "native_changes_do_not_mutate_frozen_prediction");
        predicted.ReleaseSimulator();

        foreach (var player in state.Players)
            Native(CardPileCmd.RemoveFromCombat(player.PlayerCombatState!.AllCards.ToArray(), skipVisuals: true));
        local.Creature.SetCurrentHpInternal(3);
        enemy.SetCurrentHpInternal(12);
        Native(PlayerCmd.LoseEnergy(local.PlayerCombatState!.Energy - 2, local));
        Add<StrikeIronclad>(local);
        Add<DefendIronclad>(local);
        root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        var retaliationPolicy = Policy(horizon: 1) with { VerifyIncrementalSearch = true };
        solver = Solver(root, retaliationPolicy);
        SolverResult Search(CombatBeamSolver target)
        {
            var task = Task.Run(target.Solve);
            loop.RunUntilCompleted(task, TimeSpan.FromSeconds(25), "Quota fixed-budget search");
            return task.GetAwaiter().GetResult();
        }
        var result = Search(solver);
        Check(result.AdvisoryObjectiveWitness && !result.Snapshot.AllEnemiesDead && !result.Snapshot.PlayerDead
            && result.AdvisoryPlannedContribution == 6 && result.Snapshot.EnemyHp == 6
            && result.BestNode.Actions.Any(action => action.CardId == "DEFEND_IRONCLAD")
            && result.BestNode.Actions.Any(action => action.CardId == "STRIKE_IRONCLAD"),
            "quota_does_not_skip_real_retaliation_incremental_equal");
        Check(result.ExpandedNodes <= retaliationPolicy.Profile.MaxExpandedNodes, "fixed_node_budget_respected");
        VerifyRanking(root, solver, Check);
        var limited = Search(Solver(root, Policy(nodes: 1)));
        Check(limited.ExpandedNodes <= 1 && limited.AdvisoryComparisonCycles == 0
            && !limited.AdvisoryObjectiveWitness && limited.BoundaryReason == SearchBoundaryReason.NodeLimit,
            "unassessed_budget_fallback_remains_unknown");
        foreach (string language in new[] { "eng", "zhs", "zht" })
        {
            OfflineLocalization.Install(language);
            var overlay = SolverOverlaySnapshot.Capture(limited, unexpectedReplan: false);
            Check(overlay.SummaryText.Contains(SolverText.Get("尚未完成首个敌方周期，当前建议缺少完整受击评估。"))
                && overlay.SummaryText.Contains(SolverText.Get("队友未来操作未知；队友行动后请手动重新计算。")),
                "quota_and_unknown_ui_" + language);
        }
        OfflineLocalization.Install(options.Language);
        var replay = solver.ReplayMultiplayerForTesting(result.BestNode.Actions);
        expected = ContinuationStamp.CapturePredicted(local, replay.Simulator, replay.Turn, root.Forecast, root.StartTurnNumber);
        foreach (var action in result.BestNode.Actions.Where(action => action.Kind == PlanActionKind.PlayCard))
        {
            var card = local.PlayerCombatState.Hand.Cards.Single(card => card.Id.Entry == action.CardId);
            Check(card.TryManualPlay(card is StrikeIronclad ? enemy : null), "native_retaliation_play_" + action.CardId);
            Native(RunManager.Instance.ActionExecutor.FinishedExecutingActions());
        }
        // The horizon stops before the next player setup. A two-cycle replay includes that setup.
        var nextSolver = Solver(root, Policy(horizon: 2));
        var next = nextSolver.ReplayMultiplayerForTesting(result.BestNode.Actions);
        expected = ContinuationStamp.CapturePredicted(local, next.Simulator, next.Turn, root.Forecast, root.StartTurnNumber);
        int turn = local.PlayerCombatState.TurnNumber;
        foreach (var player in state.Players) CombatManager.Instance.SetReadyToEndTurn(player, canBackOut: false);
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (local.PlayerCombatState.TurnNumber == turn || local.PlayerCombatState.Phase != PlayerTurnPhase.Play)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Quota native cycle did not complete.");
            loop.Pump(TimeSpan.FromMilliseconds(10));
        }
        actual = ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true);
        Check(expected.StateText == actual.StateText, "retaliation_full_party_native_cycle_equal");
        replay.ReleaseSimulator();
        next.ReleaseSimulator();
        File.WriteAllText(Path.Combine(options.OutputDirectory, "quota-contracts.json"),
            JsonSerializer.Serialize(new { checks, result.ExpandedNodes, result.TransitionCount,
                result.AdvisoryObjective, result.AdvisoryPlannedContribution, result.AdvisoryObjectiveWitness,
                result.BestNode.Actions }, UnattendedTestFiles.JsonOptions));
        return $"quota_contracts={checks.Count} native_actions_and_cycle=equal incremental=equal";
    }

    private static void VerifyLedger(Action<bool, string> check)
    {
        MultiplayerRootObservation Root(int round, int hp, long local = 0, long total = 0, params ulong[] players)
            => new(round, hp, 80, 80, players.Length == 0 ? new ulong[] { 1, 2 } : players, local, total);
        var session = new MultiplayerContributionSession();
        var start = session.Observe(Root(4, 100));
        check(start.TargetDamage == 50 && start.DeadlineRound == 6, "stage_share_and_absolute_deadline");
        var paid = session.Observe(Root(4, 82, 6, 18));
        check(paid.PaidDamage == 6 && paid.RemainingCycles == 3 && paid.Stage == start.Stage,
            "same_round_recalculation_does_not_renew");
        var healing = session.Observe(Root(5, 94, 6, 18));
        check(healing.PaidDamage == -6 && healing.RemainingCycles == 2 && healing.Progress(88, 6, 6) == 0,
            "healing_and_continued_progress_share_stage_origin");
        var roster = session.Observe(Root(5, 94, 6, 18, 1));
        check(roster.Stage == 2 && roster.DeadlineRound == 6 && roster.TargetDamage == 94
            && roster.PreviousTarget == 50 && roster.PreviousProgress == -6,
            "roster_change_preserves_deadline_and_previous_result");
        var renewed = session.Observe(Root(7, 70, 30, 42, 1));
        check(renewed.Stage == 3 && renewed.DeadlineRound == 9 && renewed.PreviousProgress == 24,
            "elapsed_stage_renews_explicitly");
        var capped = new MultiplayerContributionSession();
        capped.Observe(Root(1, 100));
        var unknownDamage = capped.Observe(Root(1, 50));
        check(unknownDamage.PaidDamage == 0 && unknownDamage.Progress(60, 6, 6) == 6,
            "recalculation_does_not_add_nonadditive_capped_progress");
        var quota = start with { TargetDamage = 20 };
        MultiplayerPlanValue Value(int hp, int progress) => new(true, false, false, hp, 80, 80 - hp,
            0, 100 - progress, 1, 2, 0, 0, progress, 3);
        check(MultiplayerQuotaSelection.Cost(Value(80, 19), quota, 80, 80)
            < MultiplayerQuotaSelection.Cost(Value(70, 20), quota, 80, 80), "no_last_damage_point_health_cliff");
    }

    private static void VerifyRanking(CombatRootSnapshot root, CombatBeamSolver solver, Action<bool, string> check)
    {
        var template = solver.ReplayMultiplayerForTesting([]);
        SearchNode Node(int hp, int damage, bool risk = false, bool won = false, int cycles = 1)
        {
            var snapshot = template.DetachForMultiplayerWitness();
            void Set(string property, object value) => AccessTools.Field(typeof(SimulationSnapshot),
                $"<{property}>k__BackingField").SetValue(snapshot, value);
            Set(nameof(SimulationSnapshot.PlayerHp), hp);
            Set(nameof(SimulationSnapshot.PlayerDead), hp <= 0);
            Set(nameof(SimulationSnapshot.AllEnemiesDead), won);
            Set(nameof(SimulationSnapshot.AdvisoryEnemyCycles), cycles);
            Set(nameof(SimulationSnapshot.AdvisoryContribution), damage);
            Set(nameof(SimulationSnapshot.EnemyHp), won ? 0 : root.MultiplayerObservation!.EnemyHp - damage);
            Set(nameof(SimulationSnapshot.AdvisoryLastEnemyCycle), new MultiplayerCycleCheckpoint(cycles,
                hp, Math.Max(0, root.InitialPlayerHp - hp), 0, root.MultiplayerObservation!.EnemyHp - damage,
                2, 0, null) { MaxHp = 80, LocalDamage = damage, TotalDamage = damage, AliveEnemies = 1 });
            return new(null, 0, 0, 0, root.StartTurnNumber, SearchRouteTraits.None, 0, 0, template.StateKey,
                risk, SearchBoundaryReason.None, won || hp <= 0, null, snapshot, CombatProgressState.Capture(snapshot));
        }
        var reliable = Node(3, 6);
        var risky = Node(3, 12, risk: true);
        var win = Node(3, 12, won: true, cycles: 0);
        var prepare = AccessTools.Method(typeof(CombatBeamSolver), "PrepareMultiplayerFinalCandidates");
        List<SearchNode> Rank(params SearchNode[] nodes)
        {
            var batch = prepare.Invoke(solver, [nodes])!;
            return (List<SearchNode>)AccessTools.Property(batch.GetType(), "Candidates").GetValue(batch)!;
        }
        check(ReferenceEquals(Rank(risky, reliable)[0], reliable), "risk_cannot_dominate_reliable_witness");
        check(ReferenceEquals(Rank(reliable, win)[0], win), "real_victory_overrides_stale_cycle");
        var lateWin = Node(3, 6, won: true);
        var facts = AccessTools.Method(typeof(CombatBeamSolver), "MultiplayerFactsAt").Invoke(solver, [lateWin, 1])!;
        check(!(bool)AccessTools.Property(facts.GetType(), "Won").GetValue(facts)!,
            "victory_after_deadline_does_not_backdate_stage");
        var nodes = new[] { reliable, risky, win, Node(0, 6), Node(2, 5) };
        var ordering = AccessTools.Method(typeof(CombatBeamSolver), "CreateMultiplayerOrdering").Invoke(solver, [nodes])!;
        var compare = (Comparison<SearchNode>)AccessTools.Property(ordering.GetType(), "Compare").GetValue(ordering)!;
        foreach (var a in nodes)
        foreach (var b in nodes)
        foreach (var c in nodes)
            if (Math.Sign(compare(a, b)) != -Math.Sign(compare(b, a))
                || compare(a, b) <= 0 && compare(b, c) <= 0 && compare(a, c) > 0)
                throw new InvalidOperationException("Quota ordering is inconsistent.");
        check(true, "fixed_batch_ordering_125_triples");
        template.ReleaseSimulator();
    }

    private static bool ReadyPrefix(CombatTurnState __0, ref bool __result)
    {
        __result = __0.PlayersReadyToEndTurn.Count == __0.State.Players.Count && __0.State.CurrentSide == CombatSide.Player;
        return false;
    }
}
