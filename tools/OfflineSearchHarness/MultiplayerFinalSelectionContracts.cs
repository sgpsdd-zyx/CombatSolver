using System.Reflection;
using System.Text.Json;
using CombatSolver;
using CombatSolver.Engine.InCombat.Simulation;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;

namespace OfflineSearchHarness;

internal static class MultiplayerFinalSelectionContracts
{
    private static int _orderingCalls;
    private static void CountOrdering() => _orderingCalls++;
    private static int _singlePlayerEntries;
    private static void CountSinglePlayerEntry() => Interlocked.Increment(ref _singlePlayerEntries);
    private sealed record Selection(SearchNode[] Candidates, SearchNode Best, int Depth);
    private static readonly List<Selection> Selections = [];

    private static void RecordSelection(object __0, object __result)
    {
        var candidates = (List<SearchNode>)AccessTools.Property(__0.GetType(), "Candidates").GetValue(__0)!;
        object candidate = AccessTools.Property(__result.GetType(), "Candidate").GetValue(__result)!;
        Selections.Add(new(candidates.ToArray(),
            (SearchNode)AccessTools.Property(candidate.GetType(), "Node").GetValue(candidate)!,
            (int)AccessTools.Property(__result.GetType(), "AdvisoryComparisonCycles").GetValue(__result)!));
    }

    public static SolverResult VerifyPreviewAndFinal(Func<SolverResult> search, HarnessOptions options)
    {
        Selections.Clear();
        MethodInfo select = AccessTools.Method(typeof(CombatBeamSolver), "SelectMultiplayerFinal");
        MethodInfo record = AccessTools.Method(typeof(MultiplayerFinalSelectionContracts), nameof(RecordSelection));
        GameBootstrap.Harmony.Patch(select, postfix: new HarmonyMethod(record));
        try
        {
            SolverResult result = search();
            Selection final = Selections.Last();
            Selection? preview = Selections.SkipLast(1).LastOrDefault(item => item.Candidates
                .SequenceEqual(final.Candidates, ReferenceEqualityComparer.Instance));
            if (preview == null || !ReferenceEquals(preview.Best, final.Best) || preview.Depth != final.Depth
                || final.Depth != result.AdvisoryComparisonCycles || final.Depth != 1)
                throw new InvalidOperationException("Preview and final selection disagree for the same candidate pool.");
            File.WriteAllText(Path.Combine(options.OutputDirectory, "preview-final-selection.json"),
                JsonSerializer.Serialize(new { matchingCandidates = final.Candidates.Length, previewDepth = preview.Depth,
                    finalDepth = final.Depth, result.AdvisoryComparisonCycles, sameSelectedNode = true,
                    result.ExpandedNodes }, new JsonSerializerOptions { WriteIndented = true }));
            return result;
        }
        finally { GameBootstrap.Harmony.Unpatch(select, record); Selections.Clear(); }
    }

    public static void VerifySinglePlayerIsolation(Action search, HarnessOptions options)
    {
        string[] names = ["PrepareMultiplayerFinalCandidates", "SelectMultiplayerFinal", "CreateMultiplayerOrdering",
            "PrepareMultiplayerPublicationCandidates"];
        MethodInfo[] methods = typeof(CombatBeamSolver).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(method => names.Contains(method.Name)).ToArray();
        MethodInfo count = AccessTools.Method(typeof(MultiplayerFinalSelectionContracts), nameof(CountSinglePlayerEntry));
        _singlePlayerEntries = 0;
        foreach (MethodInfo method in methods) GameBootstrap.Harmony.Patch(method, prefix: new HarmonyMethod(count));
        try
        {
            search();
            if (_singlePlayerEntries != 0)
                throw new InvalidOperationException("Single-player search entered multiplayer final ordering.");
            File.WriteAllText(Path.Combine(options.OutputDirectory, "single-player-isolation.json"),
                JsonSerializer.Serialize(new { observedMethods = methods.Select(method => method.Name),
                    multiplayerEntries = _singlePlayerEntries }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { foreach (MethodInfo method in methods) GameBootstrap.Harmony.Unpatch(method, count); }
    }

    public static void Run(CombatState state, SearchPolicySnapshot policy, HarnessOptions options)
    {
        var root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        var names = SolverDisplayNames.Capture(state);
        var damage = BattleDamageTracker.Observe(state);
        policy = new MultiplayerSearchPolicy(Horizon: 3).Apply(policy) with
        { Profile = policy.Profile with { BeamWidth = 1 } };
        CombatBeamSolver Solver(SolverPotionPolicy potionPolicy = SolverPotionPolicy.Smart,
            int minimum = 0, PotionStrategySnapshot? directives = null) => new(root, names, damage,
                policy with { PotionPolicy = potionPolicy, PotionStrategy = directives ?? new(potionPolicy, []) },
                searchProfile: policy.Profile, minimumPotionUses: minimum);
        var solver = Solver();
        SimulationSnapshot template = solver.ReplayMultiplayerForTesting([]);
        Dictionary<SearchNode, string> labels = new(ReferenceEqualityComparer.Instance);
        List<object> evidence = [];
        List<string> failures = [];
        MethodInfo createOrdering = AccessTools.Method(typeof(CombatBeamSolver), "CreateMultiplayerOrdering");
        MethodInfo countOrdering = AccessTools.Method(typeof(MultiplayerFinalSelectionContracts), nameof(CountOrdering));
        GameBootstrap.Harmony.Patch(createOrdering, prefix: new HarmonyMethod(countOrdering));
        try
        {
            // Only ranking facts are synthetic. Detached copies never own or replay a simulator.
            SearchNode Node(string name, int[] enemyHp, int potions = 0, int automatic = 0, int firstSlot = 0)
            {
                var snapshot = (SimulationSnapshot)AccessTools.Method(typeof(object), "MemberwiseClone")
                    .Invoke(template, null)!;
                AccessTools.Field(typeof(SimulationSnapshot), "_simulator").SetValue(snapshot, null);
                void Set(string property, object value) => AccessTools.Field(typeof(SimulationSnapshot),
                    $"<{property}>k__BackingField").SetValue(snapshot, value);
                MultiplayerCycleCheckpoint? checkpoint = null;
                for (int index = 0; index < enemyHp.Length; index++)
                    checkpoint = new(index + 1, 80, 0, 0, enemyHp[index], 2, potions, checkpoint);
                Set(nameof(SimulationSnapshot.AdvisoryLastEnemyCycle), checkpoint!);
                Set(nameof(SimulationSnapshot.AdvisoryEnemyCycles), enemyHp.Length);
                Set(nameof(SimulationSnapshot.AdvisoryHpLossAllowance), 3);
                Set(nameof(SimulationSnapshot.AdvisoryRootHpLost), 0);
                Set(nameof(SimulationSnapshot.CumulativePlayerHpLost), 0);
                Set(nameof(SimulationSnapshot.PlayerHp), 80);
                Set(nameof(SimulationSnapshot.ProjectedPlayerHp), 80);
                Set(nameof(SimulationSnapshot.EnemyHp), enemyHp[^1]);
                Set(nameof(SimulationSnapshot.TeamSurvivors), 2);
                Set(nameof(SimulationSnapshot.PotionUseCount), potions);
                Set(nameof(SimulationSnapshot.AutomaticPotionUseCount), automatic);
                SearchNode? node = null;
                PlanAction[] actions = [.. Enumerable.Range(firstSlot, potions - automatic)
                    .Select(slot => new PlanAction(PlanActionKind.UsePotion, root.StartTurnNumber,
                        PotionSlot: slot, PotionId: "BLOCK_POTION")),
                    new(PlanActionKind.EndTurn, root.StartTurnNumber)];
                foreach (PlanAction action in actions)
                    node = new(action, (node?.ActionCount ?? 0) + 1, potions, 0, snapshot.Turn,
                        SearchRouteTraits.None, 0, 0, snapshot.StateKey, false, snapshot.BoundaryReason,
                        false, node, snapshot, CombatProgressState.Capture(snapshot));
                labels.Add(node!, name);
                return node!;
            }

            (SearchNode? Best, int Depth, int Count, string? Error) Select(CombatBeamSolver target, SearchNode[] pool)
            {
                _orderingCalls = 0;
                object batch = AccessTools.Method(typeof(CombatBeamSolver), "PrepareMultiplayerFinalCandidates")
                    .Invoke(target, [pool])!;
                var retained = (List<SearchNode>)AccessTools.Property(batch.GetType(), "Candidates").GetValue(batch)!;
                try
                {
                    object selected = AccessTools.Method(typeof(CombatBeamSolver), "SelectMultiplayerFinal")
                        .Invoke(target, [batch])!;
                    object candidate = AccessTools.Property(selected.GetType(), "Candidate").GetValue(selected)!;
                    return ((SearchNode)AccessTools.Property(candidate.GetType(), "Node").GetValue(candidate)!,
                        (int)AccessTools.Property(selected.GetType(), "AdvisoryComparisonCycles").GetValue(selected)!,
                        retained.Count, null);
                }
                catch (TargetInvocationException error) when (error.InnerException is PotionPolicyUnsatisfiedException)
                {
                    return (null, 0, retained.Count, nameof(PotionPolicyUnsatisfiedException));
                }
            }

            void Check(string name, CombatBeamSolver target, SearchNode[] pool, SearchNode? expected, int depth)
            {
                var result = Select(target, pool);
                bool passed = ReferenceEquals(result.Best, expected) && result.Depth == depth
                    && (expected == null ? result.Error != null : result.Error == null && _orderingCalls == 1)
                    && result.Count <= 4 && pool.All(node => !node.Snapshot.HasSimulator);
                evidence.Add(new { name, passed, input = pool.Select(node => labels[node]),
                    selected = result.Best == null ? null : labels[result.Best], depth = result.Depth,
                    retained = result.Count, orderingCalls = _orderingCalls, result.Error });
                if (!passed) failures.Add(name);
            }

            SearchNode[] dry = Enumerable.Range(0, 4).Select(index => Node($"dry{index}", [40 + index])).ToArray();
            SearchNode used = Node("used", [99], potions: 1);
            var require = Solver(SolverPotionPolicy.RequireAtLeastOne);
            Check("F01_require_potion_before_truncation", require, [.. dry, used], used, 1);
            SearchNode twice = Node("twice", [100], potions: 2);
            Check("F02_minimum_two_before_truncation", Solver(minimum: 2), [.. dry, used, twice], twice, 1);
            Check("empty_eligible_pool", require, dry, null, 0);
            Check("automatic_potion_is_not_explicit", require, [Node("automatic", [30], 1, automatic: 1)], null, 0);
            SearchNode deepUse = Node("deep_use", [99, 90], 1);
            Check("eligibility_precedes_common_cycle", require, [.. dry, deepUse], deepUse, 2);
            var forced = Solver(directives: new(SolverPotionPolicy.Smart,
                [new PotionSlotDirective(1, "BLOCK_POTION", SolverPotionDirective.Force)]));
            SearchNode forcedUse = Node("forced_slot1", [100], 1, firstSlot: 1);
            Check("forced_slot_directive", forced, [.. dry, used, forcedUse], forcedUse, 1);
            object retention = AccessTools.Property(typeof(CombatBeamSolver), "Retention").GetValue(require)!;
            foreach (bool finalQualityFirst in new[] { false, true })
            {
                var prefix = (List<SearchNode>)AccessTools.Method(retention.GetType(), "RankMultiplayer")
                    .Invoke(retention, [new[] { dry[0] }, 1, finalQualityFirst])!;
                if (prefix.Count != 1 || !ReferenceEquals(prefix[0], dry[0]))
                    throw new InvalidOperationException("Final potion eligibility removed an expandable prefix.");
            }
            evidence.Add(new { name = "expandable_prefix_kept", finalQualityFirst = "both", passed = true });

            SearchNode Victory(string name, int checkpointEnemyHp, int endedTurn)
            {
                SearchNode node = Node(name, [checkpointEnemyHp]);
                void Set(string property, object value) => AccessTools.Field(typeof(SimulationSnapshot),
                    $"<{property}>k__BackingField").SetValue(node.Snapshot, value);
                Set(nameof(SimulationSnapshot.AllEnemiesDead), true);
                Set(nameof(SimulationSnapshot.EnemyHp), 0);
                Set(nameof(SimulationSnapshot.TerminalStamp), new CombatTerminalStamp(endedTurn, CombatTerminalOutcome.Victory));
                return node;
            }
            SearchNode earlyWin = Victory("early_win", 99, 2), lateWin = Victory("late_win", 10, 3);
            Check("terminal_survives_common_cycle_cut", solver, [.. dry, earlyWin], earlyWin, 1);
            Check("terminal_end_facts_before_old_checkpoint", solver, [earlyWin, lateWin, dry[0]], earlyWin, 1);
            Check("all_terminal_end_turn_control", solver, [lateWin, earlyWin], earlyWin, 3);
            SearchNode deadWin = Victory("dead_win", 1, 1);
            AccessTools.Field(typeof(SimulationSnapshot), "<PlayerDead>k__BackingField").SetValue(deadWin.Snapshot, true);
            Check("local_death_still_precedes_victory", solver, [deadWin, earlyWin, dry[0]], earlyWin, 1);

            SearchNode a = Node("A", [40, 35]), b = Node("B", [41, 25]), c = Node("C", [42, 15]);
            SearchNode d = Node("D", [43, 5]), shallow = Node("S", [99]), x = Node("X", [44, 1]);
            SearchNode[] five = [a, b, c, d, shallow], six = [a, b, c, d, shallow, x];
            Check("F03_five_candidate_frozen_depth", solver, five, a, 1);
            Check("F04_six_candidate_frozen_depth", solver, six, a, 1);
            Check("new_batch_can_advance_depth", solver, [a, b, c, d, x], x, 2);
            Check("reference_duplicates_do_not_fill_limit", solver, [a, a, b, c, d, shallow], a, 1);
            foreach (SearchNode[] pool in new[] { five, six })
            {
                int permutations = 0, wrong = 0;
                foreach (SearchNode[] permutation in Permutations(pool))
                {
                    var result = Select(solver, permutation);
                    permutations++;
                    if (!ReferenceEquals(result.Best, a) || result.Depth != 1 || _orderingCalls != 1) wrong++;
                }
                evidence.Add(new { name = $"permutations_{pool.Length}", permutations, wrong });
                if (wrong != 0) failures.Add($"permutations_{pool.Length}");
            }
            object ordering = createOrdering.Invoke(solver, [six])!;
            var compare = (Comparison<SearchNode>)AccessTools.Property(ordering.GetType(), "Compare").GetValue(ordering)!;
            int triples = 0;
            foreach (SearchNode left in six)
            foreach (SearchNode middle in six)
            foreach (SearchNode right in six)
            {
                triples++;
                if (Math.Sign(compare(left, middle)) != -Math.Sign(compare(middle, left))
                    || compare(left, middle) <= 0 && compare(middle, right) <= 0 && compare(left, right) > 0)
                    throw new InvalidOperationException("Fixed-cycle ordering lost antisymmetry or transitivity.");
            }
            evidence.Add(new { name = "fixed_cycle_comparator", triples });
            File.WriteAllText(Path.Combine(options.OutputDirectory, "final-selection.json"),
                JsonSerializer.Serialize(new { evidence, failures }, new JsonSerializerOptions { WriteIndented = true }));
            if (failures.Count != 0)
                throw new InvalidOperationException($"Multiplayer final selection failed: {string.Join(", ", failures)}.");
        }
        finally
        {
            GameBootstrap.Harmony.Unpatch(createOrdering, countOrdering);
            template.ReleaseSimulator();
        }
    }

    private static IEnumerable<SearchNode[]> Permutations(SearchNode[] pool)
    {
        if (pool.Length == 0) { yield return []; yield break; }
        for (int index = 0; index < pool.Length; index++)
            foreach (SearchNode[] rest in Permutations([.. pool.Take(index), .. pool.Skip(index + 1)]))
                yield return [pool[index], .. rest];
    }
}
