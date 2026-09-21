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
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;

namespace OfflineSearchHarness;

internal static class MultiplayerWindowSelectionContracts
{
    private static bool _recording;
    private static CombatBeamSolver? _lastSolver;
    private static SearchNode[] _lastPool = [];
    private static Publication? _lastPublication;
    private static readonly SortedDictionary<int, int> Expanded = [];
    private static int _generatedDepth;
    private static int _maximumPool;

    private sealed record Publication(CombatBeamSolver Solver, SearchNode[] Pool, SearchNode Best, int Depth);
    private sealed record Point(int Cycle, int EnemyHp, int HpLost, int TeamSurvivors);
    private sealed record Evaluation(Point[] Points, int Replays, int ReplayedActions, string[] CurrentTurn);

    private static void Observe(SearchNode __0, SearchPathObservationStage __1)
    {
        if (!_recording) return;
        if (__1 == SearchPathObservationStage.Expanded)
            Expanded[__0.Snapshot.AdvisoryEnemyCycles] = Expanded.GetValueOrDefault(__0.Snapshot.AdvisoryEnemyCycles) + 1;
        if (__1 == SearchPathObservationStage.Generated)
            _generatedDepth = Math.Max(_generatedDepth, __0.Snapshot.AdvisoryEnemyCycles);
    }

    private static void CapturePool(CombatBeamSolver __instance, ref IEnumerable<SearchNode> __0)
    {
        if (!_recording) return;
        _lastSolver = __instance;
        _lastPool = __0.ToArray();
        __0 = _lastPool;
        _maximumPool = Math.Max(_maximumPool, _lastPool.Length);
    }

    private static void CaptureSelection(object __result)
    {
        if (!_recording) return;
        object candidate = Property(__result, "Candidate");
        _lastPublication = new(_lastSolver!, _lastPool,
            (SearchNode)Property(candidate, "Node"), (int)Property(__result, "AdvisoryComparisonCycles"));
    }

    private static object Property(object instance, string name)
        => AccessTools.Property(instance.GetType(), name).GetValue(instance)!;

    private static string Token(PlanAction action)
        => $"{action.Turn}:{action.Kind}:{action.CardId}:{action.CardOccurrence}:{action.TargetCombatId}:{action.PotionId}";

    private static PlanAction[] FirstTurn(SearchNode node, int turn)
        => node.Actions.TakeWhile(action => action.Turn == turn)
            .Where(action => action.Kind != PlanActionKind.EndTurn).ToArray();

    private static object Facts(SearchNode node, int turn) => new
    {
        cycles = node.Snapshot.AdvisoryEnemyCycles,
        node.ActionCount,
        node.Snapshot.EnemyHp,
        node.Snapshot.CumulativePlayerHpLost,
        node.Snapshot.DeathSaveUseCount,
        excess = node.AdvisoryHpLoss.ExcessHpLost(3),
        node.Snapshot.TeamSurvivors,
        node.Snapshot.PlayerDead,
        node.Snapshot.AllEnemiesDead,
        node.PotionCount,
        node.BoundaryReason,
        firstTurn = FirstTurn(node, turn).Select(Token).ToArray(),
        checkpoints = Checkpoints(node.Snapshot.AdvisoryLastEnemyCycle),
    };

    private static Point[] Checkpoints(MultiplayerCycleCheckpoint? last)
    {
        List<Point> points = [];
        for (var checkpoint = last; checkpoint != null; checkpoint = checkpoint.Previous)
            points.Add(new(checkpoint.Cycle, checkpoint.EnemyHp, checkpoint.HpLost, checkpoint.TeamSurvivors));
        points.Reverse();
        return points.ToArray();
    }

    public static string Run(CombatState state, HarnessOptions options, MainLoopContext loop)
    {
        Player local = LocalContext.GetMe(state)
            ?? throw new InvalidOperationException("Window selection requires a local player.");
        if (state.Players.Count != 2 || local == state.Players[0] || state.Enemies.Count != 1
            || options.MaxDegreeOfParallelism != 1 || options.BudgetMilliseconds > 5000)
            throw new ArgumentException("Window selection requires two players, local index 1, one enemy, DOP 1 and at most 5000 ms.");
        void Native(Task task) => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(20), "Window selection setup");
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
        Native(CardPileCmd.AddGeneratedCardToCombat(state.CreateCard(ModelDb.Card<StrikeIronclad>(), local),
            PileType.Hand, local));
        var setup = state.CreateCard(ModelDb.Card<Inflame>(), local);
        setup.AddKeyword(CardKeyword.Ethereal);
        Native(CardPileCmd.AddGeneratedCardToCombat(setup, PileType.Hand, local));
        bool delayedPayback = options.Scenario.MultiplayerReviewStage == "window-selection-payback";
        if (delayedPayback)
            Native(PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), local.Creature, 1,
                local.Creature, null));
        state.Enemies[0].SetMaxHpInternal(500);
        state.Enemies[0].SetCurrentHpInternal(500);
        Native(RunManager.Instance.ActionExecutor.FinishedExecutingActions());

        SearchPolicySnapshot template = new MultiplayerSearchPolicy(Horizon: 14).Apply(
            SolverController.CaptureSearchPolicy(SolverSettings.Capture(), state,
                includeTurnSetup: false, theftPolicy: null)) with
        {
            FixedBudget = true, BudgetOverrideMilliseconds = options.BudgetMilliseconds,
            Profile = ModRuntime.ResolveProfile(options), MaxDegreeOfParallelism = 1,
        };
        MethodInfo observe = AccessTools.Method(typeof(CombatBeamSolver), "ObserveSearchPath");
        MethodInfo prepare = AccessTools.Method(typeof(CombatBeamSolver), "PrepareMultiplayerFinalCandidates");
        MethodInfo select = AccessTools.Method(typeof(CombatBeamSolver), "SelectMultiplayerFinal");
        MethodInfo observePatch = AccessTools.Method(typeof(MultiplayerWindowSelectionContracts), nameof(Observe));
        MethodInfo preparePatch = AccessTools.Method(typeof(MultiplayerWindowSelectionContracts), nameof(CapturePool));
        MethodInfo selectPatch = AccessTools.Method(typeof(MultiplayerWindowSelectionContracts), nameof(CaptureSelection));
        GameBootstrap.Harmony.Patch(observe, prefix: new HarmonyMethod(observePatch));
        GameBootstrap.Harmony.Patch(prepare, prefix: new HarmonyMethod(preparePatch));
        GameBootstrap.Harmony.Patch(select, postfix: new HarmonyMethod(selectPatch));
        List<object> evidence = [];
        try
        {
            foreach (bool withPotion in delayedPayback ? new[] { false } : new[] { false, true })
            {
                if (withPotion)
                    Native(PotionCmd.TryToProcure(ModelDb.Potion<FirePotion>().ToMutable(), local));
                CombatRootSnapshot root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
                var names = SolverDisplayNames.Capture(state);
                var damage = BattleDamageTracker.Observe(state);
                var live = ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true);
                Dictionary<string, Evaluation> evaluations = [];
                foreach (int nodes in delayedPayback ? new[] { 52 }
                    : withPotion ? new[] { 36, 180 } : new[] { 4, 12, 36, 96, 180 })
                {
                    _lastPool = [];
                    _lastPublication = null;
                    _maximumPool = _generatedDepth = 0;
                    Expanded.Clear();
                    var policy = template with { Profile = template.Profile with { MaxExpandedNodes = nodes } };
                    _recording = true;
                    SolverResult result;
                    try
                    {
                        var task = Task.Run(() => CombatSearchCoordinator.Solve(root, names, damage,
                            policy, CancellationToken.None, null));
                        loop.RunUntilCompleted(task, TimeSpan.FromSeconds(20), "Window selection search");
                        result = task.GetAwaiter().GetResult();
                    }
                    finally { _recording = false; }
                    Publication final = _lastPublication
                        ?? throw new InvalidOperationException("Window selection did not observe publication.");
                    if (result.ExpandedNodes > nodes || Expanded.Values.Sum() != result.ExpandedNodes
                        || final.Depth != result.AdvisoryComparisonCycles)
                        throw new InvalidOperationException("Window selection budget or observation mismatch.");
                    if (final.Pool.Any(node => node.Snapshot.HasSimulator))
                        throw new InvalidOperationException("Final candidate observation retained an unreleased simulator.");

                    Evaluation Evaluate(SearchNode node)
                    {
                        PlanAction[] current = FirstTurn(node, root.StartTurnNumber);
                        string key = string.Join('|', current.Select(Token));
                        if (!evaluations.TryGetValue(key, out Evaluation? evaluation))
                        {
                            evaluation = EvaluateCurrentTurn(root, names, damage, template, local,
                                state.Enemies[0].CombatId, current);
                            evaluations.Add(key, evaluation);
                        }
                        return evaluation;
                    }
                    List<object> groups = [];
                    int groupedCandidateScans = 0;
                    foreach (int depth in final.Pool.Select(node => node.Snapshot.AdvisoryEnemyCycles)
                        .Where(cycle => cycle > 0).Distinct().Order())
                    {
                        SearchNode[] pool = final.Pool.Where(node => node.Snapshot.AdvisoryEnemyCycles >= depth
                            || node.Snapshot.AllEnemiesDead || node.Snapshot.PlayerDead).ToArray();
                        groupedCandidateScans += final.Pool.Length;
                        object batch = prepare.Invoke(final.Solver, [pool])!;
                        var candidates = (List<SearchNode>)Property(batch, "Candidates");
                        if (candidates.Count == 0) continue;
                        object selection = select.Invoke(final.Solver, [batch])!;
                        SearchNode best = (SearchNode)Property(Property(selection, "Candidate"), "Node");
                        groups.Add(new { minimumDepth = depth,
                            actualComparisonDepth = (int)Property(selection, "AdvisoryComparisonCycles"),
                            inputCount = pool.Length, retainedCount = candidates.Count,
                            firstTurnFamilies = pool.Select(node => string.Join('|', FirstTurn(node, root.StartTurnNumber)
                                .Select(Token))).Distinct().Count(),
                            selected = Facts(best, root.StartTurnNumber), evaluation = Evaluate(best) });
                    }
                    evidence.Add(new
                    {
                        withPotion, nodes, strength = delayedPayback ? 1 : 0,
                        horizon = 14, beam = policy.Profile.BeamWidth,
                        result.ExpandedNodes, result.TransitionCount, result.Elapsed,
                        expandedCycles = Expanded.ToDictionary(), generatedDepth = _generatedDepth,
                        maximumObservedPool = _maximumPool, result.AdvisoryComparisonCycles,
                        result.BoundaryReason, selected = Facts(final.Best, root.StartTurnNumber),
                        evaluation = Evaluate(final.Best),
                        pool = final.Pool.Distinct(ReferenceEqualityComparer.Instance)
                            .Cast<SearchNode>().Select(node => Facts(node, root.StartTurnNumber)).ToArray(),
                        groupedCandidateScans, groups,
                        distinctExternalEvaluations = evaluations.Count,
                    });
                    if (ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true).StateText != live.StateText)
                        throw new InvalidOperationException("Window selection changed the live root.");
                    File.WriteAllText(Path.Combine(options.OutputDirectory, "window-selection.json"),
                        JsonSerializer.Serialize(new
                        {
                            experimentBaselineCommit = "851c1521f5258ec43dc71c24eeb8d5200fdf612d",
                            loadedSolverAssemblyVersion = typeof(CombatBeamSolver).Assembly.GetName().Version?.ToString(),
                            method = "Production search final pools; offline depth-filtered selection uses the production comparator. No policy replacement.",
                            limitations = "Two players, one enemy, one energy, Ethereal Inflame, 64 Buffer. No native differential or real multiplayer. Observation retains two bounded node pools but never prevents simulator release; timing is not a benchmark.",
                            evaluationProtocol = "Execute the selected first player turn; then play at most one legal Strike per turn, pass otherwise; teammate passes. Reuse only identical pure action prefixes on the same root. All evaluation is outside search and reports replay cost.",
                            evidence,
                        }, UnattendedTestFiles.JsonOptions));
                    Console.WriteLine($"[window-selection] potion={withPotion} nodes={nodes} "
                        + $"common={result.AdvisoryComparisonCycles} selected={result.Snapshot.AdvisoryEnemyCycles} "
                        + $"generated={_generatedDepth} pool={final.Pool.Length} groups={groups.Count}");
                }
            }
        }
        finally
        {
            _recording = false;
            _lastPool = [];
            _lastPublication = null;
            _lastSolver = null;
            GameBootstrap.Harmony.Unpatch(observe, observePatch);
            GameBootstrap.Harmony.Unpatch(prepare, preparePatch);
            GameBootstrap.Harmony.Unpatch(select, selectPatch);
        }
        return $"window_selection_runs={evidence.Count} production_unchanged=true live_root_unchanged=true";
    }

    private static Evaluation EvaluateCurrentTurn(CombatRootSnapshot root, SolverDisplayNames names,
        BattleDamageSnapshot damage, SearchPolicySnapshot policy, Player local, uint? enemyId,
        PlanAction[] current)
    {
        var solver = new CombatBeamSolver(root, names, damage, policy, searchProfile: policy.Profile);
        List<PlanAction> actions = [.. current];
        bool endTurn = true;
        int replays = 0, replayedActions = 0;
        for (int step = 0; step < 36; step++)
        {
            SimulationSnapshot snapshot = solver.ReplayMultiplayerForTesting(actions);
            replays++;
            replayedActions += actions.Count;
            try
            {
                if (snapshot.AllEnemiesDead || snapshot.PlayerDead
                    || snapshot.BoundaryReason == SearchBoundaryReason.AdvisoryHorizon)
                    return new(Checkpoints(snapshot.AdvisoryLastEnemyCycle), replays,
                        replayedActions, current.Select(Token).ToArray());
                if (snapshot.BoundaryReason != SearchBoundaryReason.None)
                    throw new InvalidOperationException($"External evaluation stopped at {snapshot.BoundaryReason}.");
                if (endTurn)
                {
                    actions.Add(new(PlanActionKind.EndTurn, snapshot.Turn));
                    endTurn = false;
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
                    endTurn = true;
                }
                else actions.Add(new(PlanActionKind.EndTurn, snapshot.Turn));
            }
            finally { snapshot.ReleaseSimulator(); }
        }
        throw new InvalidOperationException("External window evaluation exceeded its finite action bound.");
    }
}
