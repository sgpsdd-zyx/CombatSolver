using System.Reflection;
using System.Text.Json;
using CombatSolver;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;

namespace OfflineSearchHarness;

internal static class MultiplayerSharedRankingContracts
{
    public static string Run(CombatState state, HarnessOptions options, MainLoopContext loop)
    {
        GameBootstrap.Harmony.Patch(AccessTools.Method(typeof(Godot.Time), nameof(Godot.Time.GetTicksMsec)),
            prefix: new HarmonyMethod(typeof(MultiplayerReviewContracts), "ClockPrefix"));
        var local = LocalContext.GetMe(state)!;
        local.Creature.SetMaxHpInternal(80);
        local.Creature.SetCurrentHpInternal(80);
        var enemy = state.Enemies.Single();
        enemy.SetMaxHpInternal(100);
        enemy.SetCurrentHpInternal(100);
        var root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        var names = SolverDisplayNames.Capture(state);
        var damage = BattleDamageTracker.Observe(state);
        var objective = MultiplayerContributionObjective.Start(root.MultiplayerObservation!, 3);
        var templatePolicy = SolverController.CaptureSearchPolicy(SolverSettings.Capture(), state, false, null) with
        {
            FixedBudget = true, Profile = new SolverSearchProfile(2, 40, 8, 2, 2, 1000),
            MaxDegreeOfParallelism = 1, PotionPolicy = SolverPotionPolicy.Disabled,
            PotionStrategy = new(SolverPotionPolicy.Disabled, []),
        };
        CombatBeamSolver Solver(bool shared) => new(root, names, damage,
            new MultiplayerSearchPolicy(Horizon: 3) { Objective = objective, CreditSharedDamage = shared }.Apply(templatePolicy),
            searchProfile: templatePolicy.Profile);
        var solver = Solver(true);
        var baseline = Solver(false);
        var template = solver.ReplayMultiplayerForTesting([]);
        List<string> checks = [];
        void Check(bool passed, string name)
        {
            if (!passed) throw new InvalidOperationException("Shared-damage ranking failed: " + name);
            checks.Add(name);
        }
        try
        {
            // Synthetic ranking facts isolate pruning from the native poison/Fork fixture.
            SearchNode Node(long localDamage, long unattributed, bool checkpoint = true)
            {
                var snapshot = template.DetachForMultiplayerWitness();
                void Set(string name, object? value) => AccessTools.Field(typeof(SimulationSnapshot),
                    $"<{name}>k__BackingField").SetValue(snapshot, value);
                Set(nameof(SimulationSnapshot.AdvisoryEnemyCycles), checkpoint ? 3 : 0);
                Set(nameof(SimulationSnapshot.AdvisoryLocalDamage), localDamage);
                Set(nameof(SimulationSnapshot.AdvisoryTotalDamage), 10L);
                Set(nameof(SimulationSnapshot.AdvisoryUnattributedDamage), unattributed);
                Set(nameof(SimulationSnapshot.AdvisoryContribution), (int)localDamage);
                Set(nameof(SimulationSnapshot.EnemyHp), 90);
                Set(nameof(SimulationSnapshot.AdvisoryLastEnemyCycle), checkpoint
                    ? new MultiplayerCycleCheckpoint(3, 80, 0, 0, 90, 2, 0, null)
                    { MaxHp = 80, AliveEnemies = 1, LocalDamage = localDamage, TotalDamage = 10, UnattributedDamage = unattributed }
                    : null);
                return new(new(PlanActionKind.EndTurn, root.StartTurnNumber), 1, 0, 0,
                    snapshot.Turn, SearchRouteTraits.None, 0, 0, snapshot.StateKey, false,
                    SearchBoundaryReason.None, false, null, snapshot, CombatProgressState.Capture(snapshot));
            }
            MultiplayerPlanValue Facts(SearchNode node) => (MultiplayerPlanValue)AccessTools
                .Method(typeof(CombatBeamSolver), "MultiplayerFactsAt").Invoke(solver, [node, 3])!;
            List<SearchNode> Retained(CombatBeamSolver target, SearchNode[] pool)
            {
                object batch = AccessTools.Method(typeof(CombatBeamSolver), "PrepareMultiplayerFinalCandidates")
                    .Invoke(target, [pool])!;
                return (List<SearchNode>)AccessTools.Property(batch.GetType(), "Candidates").GetValue(batch)!;
            }
            var direct = Node(2, 0);
            var shared = Node(0, 10);
            Check(MultiplayerQuotaSelection.Dominates(Facts(direct), Facts(shared), 80, 80),
                "counterexample_old_personal_axes_dominate_shared_route");
            foreach (var pool in new[] { new[] { direct, shared }, new[] { shared, direct } })
            {
                Check(ReferenceEquals(Retained(solver, pool)[0], shared), "shared_route_survives_frontier_and_wins");
                Check(ReferenceEquals(Retained(baseline, pool)[0], direct), "disabled_policy_preserves_personal_ranking");
            }

            var knownPeer = Node(0, 0, checkpoint: false);
            var unknown = Node(0, 10, checkpoint: false);
            object retention = AccessTools.Property(typeof(CombatBeamSolver), "Retention").GetValue(solver)!;
            var ranked = (List<SearchNode>)AccessTools.Method(retention.GetType(), "RankMultiplayer")
                .Invoke(retention, [new[] { knownPeer, unknown }, 4, false])!;
            Check(ranked.Count == 2, "same_combat_state_keeps_distinct_damage_attribution");

            Type labelType = typeof(CombatBeamSolver).GetNestedType("TranspositionLabel", BindingFlags.NonPublic)!;
            Type frontierType = typeof(CombatBeamSolver).GetNestedType("TranspositionFrontier", BindingFlags.NonPublic)!;
            object Label(long unattributed) => Activator.CreateInstance(labelType,
                [0, 0, 0, 0, 1, 0d, 0L, 10L, null, unattributed])!;
            object frontier = Activator.CreateInstance(frontierType, [Label(0)])!;
            var accept = AccessTools.Method(frontierType, "TryAccept");
            Check((bool)accept.Invoke(frontier, [Label(10)])!, "transposition_keeps_new_attribution");
            Check(!(bool)accept.Invoke(frontier, [Label(10)])!, "transposition_still_removes_exact_duplicate");

            object run = AccessTools.Field(typeof(CombatBeamSolver), "_run").GetValue(solver)!;
            AccessTools.Method(run.GetType(), "ResetRebuildableCaches").Invoke(run, [new[] { unknown }]);
            var rebuilt = (System.Collections.IDictionary)AccessTools.Field(run.GetType(), "Transpositions").GetValue(run)!;
            object rebuiltFrontier = rebuilt.Values.Cast<object>().Single();
            Check(!(bool)accept.Invoke(rebuiltFrontier, [Label(10)])!
                && (bool)accept.Invoke(rebuiltFrontier, [Label(0)])!, "cache_rebuild_preserves_attribution");

            var task = Task.Run(solver.Solve);
            loop.RunUntilCompleted(task, TimeSpan.FromSeconds(20), "Shared ranking UI fixture");
            var result = task.GetAwaiter().GetResult();
            int personal = result.AdvisoryPlannedContribution;
            bool witness = result.AdvisoryObjectiveWitness;
            foreach (string language in new[] { "eng", "zhs", "zht" })
            {
                OfflineLocalization.Install(language);
                result.AdvisorySharedDamageCredit = 5;
                string expected = SolverText.Format($"无明确来源的伤害按人数折算 {5d:F1} 点参与选路，不计入本机贡献。");
                Check(SolverOverlaySnapshot.Capture(result, false).SummaryText.Contains(expected), "shared_explanation_" + language);
                result.AdvisorySharedDamageCredit = 0;
                Check(!SolverOverlaySnapshot.Capture(result, false).SummaryText.Contains(expected), "zero_shared_hidden_" + language);
            }
            Check(result.AdvisoryPlannedContribution == personal && result.AdvisoryObjectiveWitness == witness,
                "shared_explanation_does_not_change_personal_witness");
            File.WriteAllText(Path.Combine(options.OutputDirectory, "shared-ranking-contracts.json"),
                JsonSerializer.Serialize(new { checks, passed = true, syntheticRankingFacts = true,
                    nativeDamageValidation = "coverage/unattended/multiplayer-shared-damage.json" },
                    new JsonSerializerOptions { WriteIndented = true }));
            return $"shared_ranking_contracts={checks.Count}";
        }
        finally
        {
            template.ReleaseSimulator();
            OfflineLocalization.Install(options.Language);
        }
    }
}
