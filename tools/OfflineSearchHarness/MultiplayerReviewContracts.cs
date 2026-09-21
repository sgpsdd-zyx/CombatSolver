using System.Text.Json;
using CombatSolver;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.ValueProps;

namespace OfflineSearchHarness;

internal static class MultiplayerReviewContracts
{
    private static int _lastExpandedTurn;
    private static void ObserveExpandedTurn(SearchNode __0) => _lastExpandedTurn = Math.Max(_lastExpandedTurn, __0.Turn);

    public static string Run(CombatState state, HarnessOptions options, MainLoopContext loop)
    {
        if (options.Scenario.MultiplayerReviewStage == "dead-teammate")
            return MultiplayerDeadTeammateContracts.Run(state, options, loop);
        if (options.Scenario.MultiplayerReviewStage == "upstream-compatibility")
            return MultiplayerUpstreamContracts.Run(state, options, loop);
        if (options.Scenario.MultiplayerReviewStage == "window-covered-contracts")
            return MultiplayerCoveredWindowContracts.Run(state, options);
        if (options.Scenario.MultiplayerReviewStage is "window-selection" or "window-selection-payback"
            or "window-covered-payback" or "window-covered-sentinel" or "window-covered-incremental" or "window-covered-defense")
            return MultiplayerWindowSelectionContracts.Run(state, options, loop);
        if (options.Scenario.MultiplayerReviewStage is "horizon" or "horizon-fourteen" or "horizon-budget" or "horizon-native" or "horizon-ordering")
            return MultiplayerHorizonContracts.Run(state, options, loop);
        GameBootstrap.Harmony.Patch(AccessTools.Method(typeof(Godot.Time), nameof(Godot.Time.GetTicksMsec)),
            prefix: new HarmonyMethod(typeof(MultiplayerReviewContracts), nameof(ClockPrefix)));
        Player local = LocalContext.GetMe(state)!;
        if (state.Players.Count != 2 || ReferenceEquals(local, state.Players[0]) || state.Enemies.Count != 1)
            throw new InvalidOperationException("Review fixture requires two players, local index 1 and one enemy.");
        void Native(Task task) => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(20), "Review fixture setup");
        foreach (Player player in state.Players)
        {
            foreach (PotionModel? potion in player.PotionSlots.ToArray()) potion?.Discard();
            Native(CardPileCmd.RemoveFromCombat(player.PlayerCombatState!.AllCards.ToArray(), skipVisuals: true));
            player.Creature.SetCurrentHpInternal(player.Creature.MaxHp);
            Native(CreatureCmd.GainBlock(player.Creature, 1000, ValueProp.Unpowered, null, fast: true));
        }
        var enemy = state.Enemies[0];
        enemy.SetCurrentHpInternal(10);
        for (int i = 0; i < 2; i++)
            Native(CardPileCmd.AddGeneratedCardToCombat(state.CreateCard(ModelDb.Card<StrikeIronclad>(), local),
                PileType.Hand, local));
        SearchPolicySnapshot policy = new MultiplayerSearchPolicy(Horizon: 3).Apply(
            SolverController.CaptureSearchPolicy(SolverSettings.Capture(), state, includeTurnSetup: false, theftPolicy: null));
        policy = policy with
        {
            Profile = new SolverSearchProfile(4, 120, 12, 4, 4, 1000), MaxDegreeOfParallelism = 1,
            PotionPolicy = SolverPotionPolicy.Smart, PotionStrategy = new(SolverPotionPolicy.Smart, []),
        };
        List<object> evidence = [];
        List<string> failures = [];
        void Check(string name, bool passed, object facts)
        {
            evidence.Add(new { name, passed, facts });
            if (!passed) failures.Add(name);
        }
        SolverResult Search(CombatBeamSolver solver)
        {
            Task<SolverResult> task = Task.Run(solver.Solve);
            loop.RunUntilCompleted(task, TimeSpan.FromSeconds(30), "Review regression search");
            return task.GetAwaiter().GetResult();
        }
        var names = SolverDisplayNames.Capture(state);
        var root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        var solver = new CombatBeamSolver(root, names, BattleDamageTracker.Observe(state), policy,
            searchProfile: policy.Profile);
        void VerifyStopping()
        {
            var expand = AccessTools.Method(typeof(CombatBeamSolver), "Expand");
            var observe = AccessTools.Method(typeof(MultiplayerReviewContracts), nameof(ObserveExpandedTurn));
            _lastExpandedTurn = 0;
            GameBootstrap.Harmony.Patch(expand, prefix: new HarmonyMethod(observe));
            try
            {
                SolverResult result = Search(solver);
                Check("multiplayer_continues_after_local_perfect_win",
                    _lastExpandedTurn > root.StartTurnNumber && result.ExpandedNodes <= policy.Profile.MaxExpandedNodes,
                    new { lastExpandedTurn = _lastExpandedTurn, result.ExpandedNodes,
                        result.Snapshot.AllEnemiesDead, result.Elapsed });
            }
            finally { GameBootstrap.Harmony.Unpatch(expand, observe); }
        }
        string Finish()
        {
            File.WriteAllText(Path.Combine(options.OutputDirectory, "review-" + options.Scenario.MultiplayerReviewStage + ".json"),
                JsonSerializer.Serialize(new { stage = options.Scenario.MultiplayerReviewStage, evidence, failures },
                    new JsonSerializerOptions { WriteIndented = true }));
            if (failures.Count > 0)
                throw new InvalidOperationException("Multiplayer review regressions: " + string.Join(", ", failures));
            return $"{evidence.Count} review contracts passed";
        }
        if (options.Scenario.MultiplayerReviewStage == "stopping")
        {
            VerifyStopping();
            return Finish();
        }
        List<SearchNode> nodes = [];
        SearchNode Replay(SearchNode? parent, PlanAction? action)
        {
            PlanAction[] actions = action == null ? [] : [.. parent!.Actions, action];
            SimulationSnapshot snapshot = solver.ReplayMultiplayerForTesting(actions);
            var node = new SearchNode(action, actions.Length, snapshot.PotionUseCount, snapshot.PotionStrategicCost,
                snapshot.Turn, SearchRouteTraits.None, 0, snapshot.Score, snapshot.StateKey, snapshot.HasRisk,
                snapshot.BoundaryReason, snapshot.PlayerDead || snapshot.AllEnemiesDead
                    || snapshot.BoundaryReason != SearchBoundaryReason.None,
                parent, snapshot, CombatProgressState.Capture(snapshot));
            nodes.Add(node);
            return node;
        }
        SearchNode Select(params SearchNode[] pool)
        {
            object batch = AccessTools.Method(typeof(CombatBeamSolver), "PrepareMultiplayerFinalCandidates")
                .Invoke(solver, [pool])!;
            return ((List<SearchNode>)AccessTools.Property(batch.GetType(), "Candidates").GetValue(batch)!)[0];
        }

        SearchNode initial = Replay(null, null);
        SearchNode pass = Replay(initial, new(PlanActionKind.EndTurn, initial.Turn));
        SearchNode attackAfterPass = Replay(pass, new(PlanActionKind.PlayCard, pass.Turn,
            CardId: "STRIKE_IRONCLAD", TargetCombatId: enemy.CombatId));
        SearchNode win = Replay(attackAfterPass, new(PlanActionKind.PlayCard, attackAfterPass.Turn,
            CardId: "STRIKE_IRONCLAD", TargetCombatId: enemy.CombatId));
        SearchNode attack = Replay(initial, new(PlanActionKind.PlayCard, initial.Turn,
            CardId: "STRIKE_IRONCLAD", TargetCombatId: enemy.CombatId));
        SearchNode unfinished = Replay(attack, new(PlanActionKind.EndTurn, attack.Turn));
        if (!win.Snapshot.AllEnemiesDead || unfinished.Snapshot.AllEnemiesDead
            || win.Snapshot.AdvisoryLastEnemyCycle?.Cycle != 1
            || unfinished.Snapshot.AdvisoryLastEnemyCycle?.Cycle != 1)
            throw new InvalidOperationException("The real replay did not produce the mixed terminal/checkpoint fixture.");
        SearchNode mixedWinner = Select(win, unfinished);
        Check("terminal_facts_precede_checkpoint", ReferenceEquals(mixedWinner, win), new
        {
            winner = ReferenceEquals(mixedWinner, win) ? "win" : "unfinished",
            terminalEnemyHp = win.Snapshot.EnemyHp,
            terminalCheckpointEnemyHp = win.Snapshot.AdvisoryLastEnemyCycle.EnemyHp,
            unfinishedEnemyHp = unfinished.Snapshot.EnemyHp,
        });
        object terminalFacts = AccessTools.Method(typeof(CombatBeamSolver), "MultiplayerFactsAt").Invoke(solver, [win, 1])!;
        int Fact(string property) => (int)AccessTools.Property(terminalFacts.GetType(), property).GetValue(terminalFacts)!;
        Check("terminal_projection_uses_entire_snapshot",
            Fact("EnemyHp") == win.Snapshot.EnemyHp && Fact("Hp") == win.Snapshot.PlayerHp
            && Fact("HpLost") == win.Snapshot.CumulativePlayerHpLost
            && Fact("DeathSaves") == win.Snapshot.DeathSaveUseCount
            && Fact("TeamSurvivors") == win.Snapshot.TeamSurvivors && Fact("Potions") == win.PotionCount,
            new { projectedEnemyHp = Fact("EnemyHp"), actualEnemyHp = win.Snapshot.EnemyHp });
        Check("all_terminal_control", ReferenceEquals(Select(win), win), new { win.Snapshot.CombatEndedTurn });
        foreach (SearchNode node in nodes) node.Snapshot.ReleaseSimulator();

        VerifyStopping();

        PotionModel Procure(Player owner)
        {
            PotionModel potion = ModelDb.Potion<BlockPotion>().ToMutable();
            Native(PotionCmd.TryToProcure(potion, owner));
            return potion;
        }
        Native(Procure(local).OnUseWrapper(new ThrowingPlayerChoiceContext(), local.Creature));
        BattleDamageTracker.Begin(state);
        BattleDamageSnapshot before = BattleDamageTracker.Observe(state);
        Player peer = state.Players[0];
        Native(Procure(peer).OnUseWrapper(new ThrowingPlayerChoiceContext(), local.Creature));
        PotionUsedEntry peerEntry = CombatManager.Instance.History.Entries.OfType<PotionUsedEntry>().Last();
        if (!ReferenceEquals(peerEntry.Actor, peer.Creature))
            throw new InvalidOperationException("Native potion history did not record the actual owner.");
        BattleDamageSnapshot peerPaid = BattleDamageTracker.Observe(state);
        PotionModel held = Procure(local);
        var potionRoot = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true, predictPotionReward: true);
        Check("single_player_reward_forecast_excluded",
            potionRoot.PotionRewardOutlook == PotionRewardOutlook.None
            && !new MultiplayerSearchPolicy().Apply(policy with { PredictPotionReward = true }).PredictPotionReward,
            new { potionRoot.PotionRewardOutlook.ForecastPotionId, potionRoot.PotionRewardOutlook.ReplacementHpCredit });
        SearchPolicySnapshot required = policy with
        { PotionPolicy = SolverPotionPolicy.RequireAtLeastOne, PotionStrategy = new(SolverPotionPolicy.RequireAtLeastOne, []) };
        CombatBeamSolver PotionSolver(BattleDamageSnapshot damage, PotionStrategySnapshot? strategy = null)
            => new(potionRoot, names, damage, required with { PotionStrategy = strategy ?? required.PotionStrategy },
                searchProfile: required.Profile);
        SolverPotionPolicy Effective(CombatBeamSolver target)
            => (SolverPotionPolicy)AccessTools.Field(typeof(CombatBeamSolver), "_potionPolicy").GetValue(target)!;
        var requiredSolver = PotionSolver(peerPaid);
        SolverResult requiredResult = Search(requiredSolver);
        int explicitUses = requiredResult.BestNode.Actions.Count(action => action.Kind == PlanActionKind.UsePotion);
        Check("peer_potion_does_not_satisfy_local_requirement", peerPaid.PotionsUsedSoFar == 0
            && peerPaid.PotionIdsUsedSoFar.Length == 0
            && Effective(requiredSolver) == SolverPotionPolicy.RequireAtLeastOne && explicitUses > 0,
            new { before.PotionsUsedSoFar, afterPeer = peerPaid.PotionsUsedSoFar,
                effective = Effective(requiredSolver).ToString(), explicitUses, requiredResult.ExpandedNodes });
        int slot = local.GetPotionSlotIndex(held);
        var forced = new PotionStrategySnapshot(SolverPotionPolicy.RequireAtLeastOne,
            [new(slot, held.Id.Entry, SolverPotionDirective.Force)]);
        SolverResult forcedResult = Search(PotionSolver(peerPaid, forced));
        Check("local_force_still_applies", forced.EvaluateForcedUses(forcedResult.BestNode.Actions,
            potionRoot.HasRenewablePotionShapedRock).AllForcedUsesSatisfied,
            new { forcedResult.ExpandedNodes,
                explicitUses = forcedResult.BestNode.Actions.Count(action => action.Kind == PlanActionKind.UsePotion) });
        var protectedPotion = new PotionStrategySnapshot(SolverPotionPolicy.RequireAtLeastOne,
            [new(slot, held.Id.Entry, SolverPotionDirective.Disabled)]);
        Check("protected_potion_is_not_generated", PotionSolver(peerPaid, protectedPotion).BuildOpeningPotionActions().Count == 0,
            new { slot, held.Id.Entry });
        Native(held.OnUseWrapper(new ThrowingPlayerChoiceContext(), peer.Creature));
        BattleDamageSnapshot localPaid = BattleDamageTracker.Observe(state);
        Check("local_prior_use_satisfies_requirement", localPaid.PotionsUsedSoFar == 1
            && localPaid.PotionIdsUsedSoFar.SequenceEqual(new[] { held.Id.Entry })
            && Effective(PotionSolver(localPaid)) == SolverPotionPolicy.Smart,
            new { localPaid.PotionsUsedSoFar, localPaid.PotionIdsUsedSoFar,
                effective = Effective(PotionSolver(localPaid)).ToString() });
        Check("tracking_window_and_frozen_request", before.PotionsUsedSoFar == 0 && peerPaid.PotionsUsedSoFar == 0
            && before.PotionIdsUsedSoFar.Length == 0 && peerPaid.PotionIdsUsedSoFar.Length == 0,
            new { before.PotionsUsedSoFar, frozenPeerRequest = peerPaid.PotionsUsedSoFar });

        return Finish();
    }

    private static bool ClockPrefix(ref ulong __result)
    {
        __result = (ulong)Environment.TickCount64;
        return false;
    }
}
