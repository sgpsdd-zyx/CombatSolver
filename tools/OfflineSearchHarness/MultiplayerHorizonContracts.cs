using System.Text.Json;
using CombatSolver;
using CombatSolver.Engine.InCombat.Simulation;
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

namespace OfflineSearchHarness;

internal static class MultiplayerHorizonContracts
{
    private static readonly SortedDictionary<int, int> ExpandedCycles = [];
    private static bool _collecting;
    private static Player? _scriptedPeer;
    private static int _peerReplayActions;

    private static void ObserveExpansion(SearchNode __0, SearchPathObservationStage __1)
    {
        if (!_collecting || __1 != SearchPathObservationStage.Expanded) return;
        int cycle = __0.Snapshot.AdvisoryEnemyCycles;
        ExpandedCycles[cycle] = ExpandedCycles.GetValueOrDefault(cycle) + 1;
    }

    private sealed record Evaluation(int Cycles, int EnemyHp, int HpLost, int TeamSurvivors,
        bool Won, int? EndedTurn, int ReplayCalls, int PeerReplayActions, IReadOnlyList<PlanAction> Actions);

    // A falsification scenario in the evaluator only; the production search still assumes idle peers.
    private static bool PlayPeerBeforeEndTurn(CombatPredictionSimulator __0, SimulatedCombatState __1,
        ISet<uint> __2, ref bool __3, ref bool __result)
    {
        if (_scriptedPeer == null || __1.AdvisorEnemyCycles != 0) return true;
        var card = __0.State.GetPlayerCombatState(_scriptedPeer).Hand.Cards
            .Single(candidate => candidate.Preview is Bludgeon);
        if (!__1.CanPlayCard(__0, card))
            throw new InvalidOperationException("Scripted teammate cannot legally play Bludgeon.");
        _peerReplayActions++;
        using (__1.BeginCardExecutionScope(__2))
            if (!__0.ManualPlay(card, __1.Enemies.Single(), out _))
                throw new InvalidOperationException("Scripted teammate needs an unexpected choice.");
        if (!CorePowerSupport.ApplyEnemyDeathPowers(__0, __1, __1.KnownEnemies, __2)
            || !CombatBeamSolver.SettleReplayActionBoundary(__0, __1))
            throw new InvalidOperationException("Scripted teammate did not reach a stable action boundary.");
        if (__0.IsInProgress) return true;
        __3 = false;
        __result = true;
        return false;
    }

    public static string Run(CombatState state, HarnessOptions options, MainLoopContext loop)
    {
        Player local = LocalContext.GetMe(state)
            ?? throw new InvalidOperationException("Horizon fixture has no local player.");
        if (state.Players.Count != 2 || ReferenceEquals(local, state.Players[0]) || state.Enemies.Count != 1)
            throw new InvalidOperationException("Horizon fixture requires two players, local index 1 and one enemy.");
        if (options.BudgetMilliseconds > 5_000 || options.MaxDegreeOfParallelism != 1)
            throw new ArgumentException("Horizon fixture requires a budget at most 5000 ms and DOP 1.");
        void Native(Task task) => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(20), "Horizon fixture setup");
        foreach (Player player in state.Players)
        {
            foreach (var potion in player.PotionSlots.ToArray()) potion?.Discard();
            Native(CardPileCmd.RemoveFromCombat(player.PlayerCombatState!.AllCards.ToArray(), skipVisuals: true));
            player.Creature.SetMaxHpInternal(500);
            player.Creature.SetCurrentHpInternal(500);
            Native(PowerCmd.Apply<BufferPower>(new ThrowingPlayerChoiceContext(), player.Creature, 64,
                player.Creature, null));
        }
        Native(PlayerCmd.LoseEnergy(local.PlayerCombatState!.Energy - 1, local));
        CardModel strike = state.CreateCard(ModelDb.Card<StrikeIronclad>(), local);
        CardModel setup = state.CreateCard(ModelDb.Card<Inflame>(), local);
        setup.AddKeyword(CardKeyword.Ethereal);
        Native(CardPileCmd.AddGeneratedCardToCombat(strike, PileType.Hand, local));
        Native(CardPileCmd.AddGeneratedCardToCombat(setup, PileType.Hand, local));
        Player peer = state.Players.Single(player => player != local);
        CardModel finisher = state.CreateCard(ModelDb.Card<Bludgeon>(), peer);
        Native(CardPileCmd.AddGeneratedCardToCombat(finisher, PileType.Hand, peer));
        Native(RunManager.Instance.ActionExecutor.FinishedExecutingActions());
        if (options.Scenario.MultiplayerReviewStage == "horizon-native")
        {
            state.Enemies[0].SetMaxHpInternal(500);
            state.Enemies[0].SetCurrentHpInternal(500);
            var clock = AccessTools.Method(typeof(Godot.Time), nameof(Godot.Time.GetTicksMsec));
            var clockPrefix = AccessTools.Method(typeof(MultiplayerHorizonContracts), nameof(ClockPrefix));
            GameBootstrap.Harmony.Patch(clock, prefix: new HarmonyMethod(clockPrefix));
            try { VerifyNativeActions(state, local, setup, finisher, Native); }
            finally { GameBootstrap.Harmony.Unpatch(clock, clockPrefix); }
            return "horizon_native_actions=2 full_continuation_state=equal local_index=1 godot_clock=bypassed";
        }

        SearchPolicySnapshot template = new MultiplayerSearchPolicy().Apply(
            SolverController.CaptureSearchPolicy(SolverSettings.Capture(), state,
                includeTurnSetup: false, theftPolicy: null)) with
        {
            FixedBudget = true,
            BudgetOverrideMilliseconds = options.BudgetMilliseconds,
            Profile = ModRuntime.ResolveProfile(options),
            MaxDegreeOfParallelism = 1,
        };
        if (options.Scenario.MultiplayerReviewStage == "horizon-ordering")
        {
            Native(CardPileCmd.AddGeneratedCardToCombat(state.CreateCard(ModelDb.Card<Deflect>(), local),
                PileType.Hand, local));
            VerifyCommonCycleActionCost(state, template, options, Native);
            return "common_cycle_action_cost=equal suffix_card_and_cycle=ignored current_prefix_cost=ordered triples=64 extra_player_turn=equal";
        }
        var observation = AccessTools.Method(typeof(CombatBeamSolver), "ObserveSearchPath");
        var prefix = AccessTools.Method(typeof(MultiplayerHorizonContracts), nameof(ObserveExpansion));
        GameBootstrap.Harmony.Patch(observation, prefix: new HarmonyMethod(prefix));
        var turnEnd = AccessTools.Method(typeof(CombatBeamSolver), "EndMultiplayerPlayerTurn");
        var peerPrefix = AccessTools.Method(typeof(MultiplayerHorizonContracts), nameof(PlayPeerBeforeEndTurn));
        GameBootstrap.Harmony.Patch(turnEnd, prefix: new HarmonyMethod(peerPrefix));
        List<object> evidence = [];
        try
        {
            foreach ((string name, int enemyHp, int nodes, int strength) in new[]
            {
                ("late_setup", 500, template.Profile.MaxExpandedNodes, 0),
                ("early_finish", 12, template.Profile.MaxExpandedNodes, 0),
                ("budget_four", 500, 4, 0),
                ("peer_finisher", 38, template.Profile.MaxExpandedNodes, 0),
                ("payback_seven", 500, template.Profile.MaxExpandedNodes, 1),
                ("payback_nine", 500, template.Profile.MaxExpandedNodes, 5),
            })
            {
                if (strength > 0)
                {
                    if (!strike.IsUpgraded)
                    {
                        strike.UpgradeInternal();
                        strike.FinalizeUpgradeInternal();
                    }
                    int current = (int)(local.Creature.GetPower<StrengthPower>()?.Amount ?? 0);
                    Native(PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), local.Creature,
                        strength - current, local.Creature, null));
                }
                state.Enemies[0].SetMaxHpInternal(enemyHp);
                state.Enemies[0].SetCurrentHpInternal(enemyHp);
                CombatRootSnapshot root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
                ContinuationStamp live = ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true);
                SolverDisplayNames names = SolverDisplayNames.Capture(state);
                BattleDamageSnapshot damage = BattleDamageTracker.Observe(state);
                foreach (int horizon in new[] { 3, 5, 7, 9 })
                {
                    SearchPolicySnapshot policy = new MultiplayerSearchPolicy(Horizon: horizon).Apply(template) with
                    {
                        Profile = template.Profile with { MaxExpandedNodes = nodes },
                    };
                    ExpandedCycles.Clear();
                    SolverResult result;
                    _collecting = true;
                    try
                    {
                        Task<SolverResult> search = Task.Run(() => CombatSearchCoordinator.Solve(root, names,
                            damage, policy, CancellationToken.None, null));
                        loop.RunUntilCompleted(search, TimeSpan.FromSeconds(20), $"{name} horizon {horizon}");
                        result = search.GetAwaiter().GetResult();
                    }
                    finally { _collecting = false; }
                    if (!result.IsMultiplayerAdvice || result.ExpandedNodes > nodes
                        || ExpandedCycles.Values.Sum() != result.ExpandedNodes
                        || result.Snapshot.AdvisoryEnemyCycles > horizon)
                        throw new InvalidOperationException("Horizon search violated its budget or observation contract.");
                    PlanAction[] currentTurn = result.BestNode.Actions
                        .TakeWhile(action => action.Turn == root.StartTurnNumber)
                        .Where(action => action.Kind != PlanActionKind.EndTurn).ToArray();
                    Evaluation evaluated = EvaluateCurrentTurn(root, names, damage, template, local,
                        state.Enemies[0].CombatId, currentTurn);
                    Evaluation? peerEvaluation = name == "peer_finisher"
                        ? EvaluateCurrentTurn(root, names, damage, template, local,
                            state.Enemies[0].CombatId, currentTurn, peer) : null;
                    evidence.Add(new
                    {
                        name, rootEnemyHp = enemyHp, strength, strike.IsUpgraded, horizon, nodeLimit = nodes,
                        result.ExpandedNodes, result.TransitionCount, result.Elapsed,
                        expandedCycles = ExpandedCycles.ToDictionary(),
                        result.AdvisoryComparisonCycles,
                        selectedCycles = result.Snapshot.AdvisoryEnemyCycles,
                        result.BoundaryReason,
                        endEnemyHp = result.Snapshot.EnemyHp,
                        result.Snapshot.CumulativePlayerHpLost,
                        currentTurn,
                        evaluation = evaluated,
                        peerEvaluation,
                    });
                    WriteEvidence();
                    Console.WriteLine($"[horizon] {name} h={horizon} expanded={result.ExpandedNodes} "
                        + $"common={result.AdvisoryComparisonCycles} selected={result.Snapshot.AdvisoryEnemyCycles} "
                        + $"first={currentTurn.FirstOrDefault()?.CardId} evaluated_enemy_hp={evaluated.EnemyHp}");
                    if (ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true).StateText != live.StateText)
                        throw new InvalidOperationException("Horizon experiment mutated the live root.");
                }
            }
        }
        finally
        {
            _collecting = false;
            _scriptedPeer = null;
            GameBootstrap.Harmony.Unpatch(observation, prefix);
            GameBootstrap.Harmony.Unpatch(turnEnd, peerPrefix);
        }
        return $"horizon_runs={evidence.Count} same_budget=true common_evaluation_cycles=9 live_root_unchanged=true";

        void WriteEvidence() => File.WriteAllText(Path.Combine(options.OutputDirectory, "horizon-comparison.json"),
            JsonSerializer.Serialize(new
            {
                fixture = "native_models_ethereal_inflame_one_strike",
                localPlayerIndex = 1,
                evaluationProtocol = "Selected current-turn cards, then one legal Strike per turn through cycle 9; teammates pass. Evaluation work is separate from search.",
                peerProtocol = "Only peer_finisher: the teammate legally plays one Bludgeon before the first enemy cycle, then passes. This is a fixed perturbation, not a human probability model.",
                humanPolicyValidated = false,
                evidence,
            }, UnattendedTestFiles.JsonOptions));
    }

    private static Evaluation EvaluateCurrentTurn(CombatRootSnapshot root, SolverDisplayNames names,
        BattleDamageSnapshot damage, SearchPolicySnapshot template, Player local, uint? enemyId,
        IReadOnlyList<PlanAction> currentTurn, Player? peer = null)
    {
        if (_scriptedPeer != null) throw new InvalidOperationException("Nested horizon evaluation.");
        _scriptedPeer = peer;
        _peerReplayActions = 0;
        try
        {
            SearchPolicySnapshot policy = new MultiplayerSearchPolicy(Horizon: 9).Apply(template);
            var solver = new CombatBeamSolver(root, names, damage, policy, searchProfile: policy.Profile);
            List<PlanAction> actions = [.. currentTurn];
            bool needsEndTurn = true;
            int replays = 0;
            for (int step = 0; step < 32; step++)
            {
                SimulationSnapshot snapshot = solver.ReplayMultiplayerForTesting(actions);
                replays++;
                try
                {
                    if (snapshot.PlayerDead || snapshot.AllEnemiesDead
                        || snapshot.BoundaryReason == SearchBoundaryReason.AdvisoryHorizon)
                        return new(snapshot.AdvisoryEnemyCycles, snapshot.EnemyHp, snapshot.CumulativePlayerHpLost,
                            snapshot.TeamSurvivors, snapshot.AllEnemiesDead, snapshot.CombatEndedTurn, replays,
                            _peerReplayActions, actions.ToArray());
                    if (snapshot.BoundaryReason != SearchBoundaryReason.None)
                        throw new InvalidOperationException($"Fixed horizon evaluator stopped at {snapshot.BoundaryReason}.");
                    // Check victory after each card before scheduling the next turn boundary.
                    if (needsEndTurn)
                    {
                        actions.Add(new(PlanActionKind.EndTurn, snapshot.Turn));
                        needsEndTurn = false;
                        continue;
                    }
                    var simulator = (CombatPredictionSimulator)snapshot.Simulator;
                    var combat = (SimulatedCombatState)simulator.State.CombatState;
                    var strike = simulator.State.GetPlayerCombatState(local).Hand.Cards
                        .SingleOrDefault(card => card.Preview is StrikeIronclad);
                    if (strike != null && combat.CanPlayCard(simulator, strike))
                    {
                        actions.Add(new(PlanActionKind.PlayCard, snapshot.Turn,
                            CardId: "STRIKE_IRONCLAD", TargetCombatId: enemyId));
                        needsEndTurn = true;
                    }
                    else
                    {
                        actions.Add(new(PlanActionKind.EndTurn, snapshot.Turn));
                    }
                }
                finally { snapshot.ReleaseSimulator(); }
            }
            throw new InvalidOperationException("Fixed horizon evaluator exceeded its action bound.");
        }
        finally { _scriptedPeer = null; }
    }

    private static void VerifyNativeActions(CombatState state, Player local, CardModel setup,
        CardModel finisher, Action<Task> native)
    {
        CombatRootSnapshot root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        var simulator = root.ForkSimulator();
        var combat = (SimulatedCombatState)simulator.State.CombatState;
        foreach (CardModel card in new[] { setup, finisher })
        {
            Console.WriteLine("[horizon-native] predicted " + card.Id.Entry);
            Creature? target = card == setup ? null : state.Enemies.Single();
            var predicted = simulator.State.FindCard(card)
                ?? throw new InvalidOperationException("Native action card missing from the frozen root.");
            if (!combat.CanPlayCard(simulator, predicted))
                throw new InvalidOperationException("Native action card is not playable in prediction.");
            combat.BeginActionChoices((IReadOnlyList<PlanCardChoice>?)null);
            try
            {
                using (SimulationNotificationIsolation.Enter())
                using (combat.BeginCardExecutionScope())
                    if (!simulator.ManualPlay(predicted, target, out _)
                        || !CombatBeamSolver.SettleReplayActionBoundary(simulator, combat))
                        throw new InvalidOperationException("Native action prediction did not complete.");
            }
            finally { combat.EndActionChoices(); }
            Console.WriteLine("[horizon-native] native " + card.Id.Entry);
            if (!card.TryManualPlay(target))
                throw new InvalidOperationException("Native manual play was refused: " + card.Id.Entry);
            native(RunManager.Instance.ActionExecutor.FinishedExecutingActions());
            ContinuationStamp expected = ContinuationStamp.CapturePredicted(local, simulator,
                root.StartTurnNumber, root.Forecast, root.StartTurnNumber);
            ContinuationStamp actual = ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true);
            if (expected.StateText != actual.StateText)
                throw new InvalidOperationException("Horizon fixture native action differs: "
                    + string.Join("; ", expected.DescribeDifferences(actual, 12)));
        }
    }

    private static bool ClockPrefix(ref ulong __result)
    {
        __result = (ulong)Environment.TickCount64;
        return false;
    }

    private static void VerifyCommonCycleActionCost(CombatState state, SearchPolicySnapshot policy,
        HarnessOptions options, Action<Task> native)
    {
        CombatRootSnapshot root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        policy = new MultiplayerSearchPolicy(Horizon: 3).Apply(policy);
        var solver = new CombatBeamSolver(root, SolverDisplayNames.Capture(state),
            BattleDamageTracker.Observe(state), policy, searchProfile: policy.Profile);
        List<SearchNode> owned = [];
        SearchNode Replay(SearchNode? parent, PlanAction? action)
        {
            PlanAction[] actions = action == null ? [] : [.. parent!.Actions, action];
            SimulationSnapshot snapshot = solver.ReplayMultiplayerForTesting(actions);
            var node = new SearchNode(action, actions.Length, snapshot.PotionUseCount,
                snapshot.PotionStrategicCost, snapshot.Turn, SearchRouteTraits.None, 0,
                snapshot.Score, snapshot.StateKey, snapshot.HasRisk, snapshot.BoundaryReason,
                snapshot.PlayerDead || snapshot.AllEnemiesDead || snapshot.BoundaryReason != SearchBoundaryReason.None,
                parent, snapshot, CombatProgressState.Capture(snapshot));
            owned.Add(node);
            return node;
        }
        try
        {
            SearchNode seed = Replay(null, null);
            SearchNode attack = Replay(seed, new(PlanActionKind.PlayCard, seed.Turn,
                CardId: "STRIKE_IRONCLAD", TargetCombatId: state.Enemies[0].CombatId));
            SearchNode plain = Replay(attack, new(PlanActionKind.EndTurn, attack.Turn));
            SearchNode futureCard = Replay(plain, new(PlanActionKind.PlayCard, plain.Turn, CardId: "DEFLECT"));
            SearchNode futureCycle = Replay(plain, new(PlanActionKind.EndTurn, plain.Turn));
            SearchNode extra = Replay(seed, new(PlanActionKind.PlayCard, seed.Turn, CardId: "DEFLECT"));
            SearchNode extraAttack = Replay(extra, new(PlanActionKind.PlayCard, extra.Turn,
                CardId: "STRIKE_IRONCLAD", TargetCombatId: state.Enemies[0].CombatId));
            SearchNode costlyPrefix = Replay(extraAttack, new(PlanActionKind.EndTurn, extraAttack.Turn));
            SearchNode[] cohort = [plain, futureCard, futureCycle, costlyPrefix];
            foreach (SearchNode node in owned) node.Snapshot.ReleaseSimulator();
            object ordering = AccessTools.Method(typeof(CombatBeamSolver), "CreateMultiplayerOrdering")
                .Invoke(solver, [cohort])!;
            int depth = (int)AccessTools.Property(ordering.GetType(), "EnemyCycles").GetValue(ordering)!;
            var compare = (Comparison<SearchNode>)AccessTools.Property(ordering.GetType(), "Compare")
                .GetValue(ordering)!;
            int suffixCard = compare(plain, futureCard);
            int suffixCycle = compare(plain, futureCycle);
            int prefixCost = compare(plain, costlyPrefix);
            File.WriteAllText(Path.Combine(options.OutputDirectory, "horizon-ordering.json"),
                JsonSerializer.Serialize(new { depth, suffixCard, suffixCycle, prefixCost,
                    simulatorsReleased = owned.All(node => !node.Snapshot.HasSimulator),
                    actionCounts = cohort.Select(node => node.ActionCount),
                    cycles = cohort.Select(node => node.Snapshot.AdvisoryEnemyCycles),
                    checkpointEnemyHp = cohort.Select(node =>
                    {
                        MultiplayerCycleCheckpoint checkpoint = node.Snapshot.AdvisoryLastEnemyCycle!;
                        while (checkpoint.Cycle > depth) checkpoint = checkpoint.Previous!;
                        return checkpoint.EnemyHp;
                    }) }, UnattendedTestFiles.JsonOptions));
            if (depth != 1 || suffixCard != 0 || suffixCycle != 0 || prefixCost >= 0)
                throw new InvalidOperationException("Actions outside the common cycle changed a tied comparison.");
            foreach (SearchNode a in cohort)
            foreach (SearchNode b in cohort)
            foreach (SearchNode c in cohort)
                if (Math.Sign(compare(a, b)) != -Math.Sign(compare(b, a))
                    || compare(a, b) <= 0 && compare(b, c) <= 0 && compare(a, c) > 0)
                    throw new InvalidOperationException("Horizon ordering is not a consistent total preorder.");

            native(PowerCmd.Apply<AmbergrisPower>(new ThrowingPlayerChoiceContext(),
                root.PlayerIdentity.Creature, 1, root.PlayerIdentity.Creature, null));
            root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
            solver = new CombatBeamSolver(root, SolverDisplayNames.Capture(state),
                BattleDamageTracker.Observe(state), policy, searchProfile: policy.Profile);
            SearchNode extraSeed = Replay(null, null);
            SearchNode extraTurn = Replay(extraSeed, new(PlanActionKind.EndTurn, extraSeed.Turn));
            SearchNode clean = Replay(extraTurn, new(PlanActionKind.EndTurn, extraTurn.Turn));
            SearchNode extraDefense = Replay(extraTurn,
                new(PlanActionKind.PlayCard, extraTurn.Turn, CardId: "DEFLECT"));
            SearchNode costly = Replay(extraDefense, new(PlanActionKind.EndTurn, extraDefense.Turn));
            SearchNode tail = Replay(clean, new(PlanActionKind.PlayCard, clean.Turn, CardId: "DEFLECT"));
            object extraOrdering = AccessTools.Method(typeof(CombatBeamSolver), "CreateMultiplayerOrdering")
                .Invoke(solver, [new[] { clean, costly, tail }])!;
            var extraCompare = (Comparison<SearchNode>)AccessTools.Property(extraOrdering.GetType(), "Compare")
                .GetValue(extraOrdering)!;
            int extraPrefix = extraCompare(clean, costly);
            int extraSuffix = extraCompare(clean, tail);
            File.WriteAllText(Path.Combine(options.OutputDirectory, "extra-turn-action-cost.json"),
                JsonSerializer.Serialize(new { extraTurnCycles = extraTurn.Snapshot.AdvisoryEnemyCycles,
                    firstCycle = clean.Snapshot.AdvisoryEnemyCycles, extraPrefix, extraSuffix,
                    clean.ActionCount, clean.Turn }, UnattendedTestFiles.JsonOptions));
            if (extraTurn.Snapshot.AdvisoryEnemyCycles != 0 || clean.Snapshot.AdvisoryEnemyCycles != 1
                || extraPrefix >= 0 || extraSuffix != 0)
                throw new InvalidOperationException("Extra player turns changed the enemy-cycle action cursor.");
        }
        finally { foreach (SearchNode node in owned) node.Snapshot.ReleaseSimulator(); }
    }
}
