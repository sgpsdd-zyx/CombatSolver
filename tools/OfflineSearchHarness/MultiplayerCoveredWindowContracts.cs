using System.Reflection;
using System.Text.Json;
using CombatSolver;
using CombatSolver.Engine.InCombat.Simulation;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace OfflineSearchHarness;

internal static class MultiplayerCoveredWindowContracts
{
    private sealed record Pick(SearchNode Best, SearchNode[] Candidates, int Depth, string Reason, object Decision);

    public static string Run(CombatState state, HarnessOptions options)
    {
        var root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        var names = SolverDisplayNames.Capture(state);
        var damage = BattleDamageTracker.Observe(state);
        SearchPolicySnapshot policy = new MultiplayerSearchPolicy(Horizon: 3).Apply(
            SolverController.CaptureSearchPolicy(SolverSettings.Capture(), state, false, null)) with
        { FixedBudget = true, Profile = new(8, 100, 8, 4, 4, 3000), MaxDegreeOfParallelism = 1 };
        CombatBeamSolver Solver(SearchPolicySnapshot? selected = null, CancellationToken cancellation = default)
            => new(root, names, damage, selected ?? policy, cancellationToken: cancellation,
                searchProfile: (selected ?? policy).Profile);
        var solver = Solver();
        SimulationSnapshot template = solver.ReplayMultiplayerForTesting([]);
        Dictionary<SearchNode, string> labels = new(ReferenceEqualityComparer.Instance);
        List<object> evidence = [];
        List<string> failures = [];
        int turn = root.StartTurnNumber;
        void Set(SimulationSnapshot snapshot, string field, object? value)
            => AccessTools.Field(typeof(SimulationSnapshot), $"<{field}>k__BackingField").SetValue(snapshot, value);
        SimulationSnapshot Copy(SimulationSnapshot source)
        {
            var snapshot = (SimulationSnapshot)AccessTools.Method(typeof(object), "MemberwiseClone").Invoke(source, null)!;
            AccessTools.Field(typeof(SimulationSnapshot), "_simulator").SetValue(snapshot, null);
            return snapshot;
        }
        SimulationSnapshot initial = Copy(template);
        Set(initial, nameof(SimulationSnapshot.PlayerHp), 80);
        Set(initial, nameof(SimulationSnapshot.ProjectedPlayerHp), 80);
        Set(initial, nameof(SimulationSnapshot.CumulativePlayerHpLost), 0);
        Set(initial, nameof(SimulationSnapshot.AdvisoryRootHpLost), 0);
        Set(initial, nameof(SimulationSnapshot.AdvisoryHpLossAllowance), 3);
        Set(initial, nameof(SimulationSnapshot.EnemyHp), 100);
        Set(initial, nameof(SimulationSnapshot.TeamSurvivors), 2);
        SearchNode rootNode = new(null, 0, 0, 0, turn, SearchRouteTraits.None, 0, 0, initial.StateKey,
            false, SearchBoundaryReason.None, false, null, initial, CombatProgressState.Capture(initial));

        // Pure, detached ranking facts exercise policies only; native execution is tested separately.
        SearchNode Step(SearchNode parent, PlanAction action, int cycle, int enemyHp,
            int hpLost = 0, int saves = 0, int survivors = 2, bool dead = false, bool won = false,
            SearchBoundaryReason boundary = SearchBoundaryReason.None, bool extra = false)
        {
            SimulationSnapshot snapshot = Copy(parent.Snapshot);
            bool ended = action.Kind == PlanActionKind.EndTurn || action.EndsPlayerTurn;
            int nextTurn = ended ? action.Turn + 1 : action.Turn;
            Set(snapshot, nameof(SimulationSnapshot.Turn), nextTurn);
            Set(snapshot, nameof(SimulationSnapshot.PlayerHp), dead ? 0 : 80 - hpLost);
            Set(snapshot, nameof(SimulationSnapshot.ProjectedPlayerHp), dead ? 0 : 80 - hpLost);
            Set(snapshot, nameof(SimulationSnapshot.CumulativePlayerHpLost), hpLost);
            Set(snapshot, nameof(SimulationSnapshot.DeathSaveRelicHpRestored), saves * 10);
            Set(snapshot, nameof(SimulationSnapshot.DeathSaveUseCount), saves);
            Set(snapshot, nameof(SimulationSnapshot.PlayerDead), dead);
            Set(snapshot, nameof(SimulationSnapshot.AllEnemiesDead), won);
            Set(snapshot, nameof(SimulationSnapshot.EnemyHp), won ? 0 : enemyHp);
            Set(snapshot, nameof(SimulationSnapshot.TeamSurvivors), survivors);
            Set(snapshot, nameof(SimulationSnapshot.BoundaryReason), boundary);
            Set(snapshot, nameof(SimulationSnapshot.AdvisoryEnemyCycles), cycle);
            int potions = parent.PotionCount + (action.Kind == PlanActionKind.UsePotion ? 1 : 0);
            Set(snapshot, nameof(SimulationSnapshot.PotionUseCount), potions);
            if (cycle > parent.Snapshot.AdvisoryEnemyCycles)
            {
                Set(snapshot, nameof(SimulationSnapshot.AdvisoryLastEnemyCycleHpLost), hpLost);
                Set(snapshot, nameof(SimulationSnapshot.AdvisoryLastEnemyCycle), new MultiplayerCycleCheckpoint(
                    cycle, dead ? 0 : 80 - hpLost, hpLost, saves, won ? 0 : enemyHp, survivors, potions,
                    parent.Snapshot.AdvisoryLastEnemyCycle));
            }
            return new(action, parent.ActionCount + 1, potions, 0, nextTurn, SearchRouteTraits.None,
                0, 0, snapshot.StateKey, false, boundary, dead || won || boundary != SearchBoundaryReason.None,
                parent, snapshot, CombatProgressState.Capture(snapshot),
                ended || extra || won ? new(action.Turn, hpLost - parent.Snapshot.CumulativePlayerHpLost,
                    0, parent.Snapshot.EnemyHp - enemyHp, 0, 0, 0, 0) : null);
        }
        SearchNode Label(string label, SearchNode node) { labels[node] = label; return node; }
        PlanAction Play(string id, int at = 0) => new(PlanActionKind.PlayCard, at == 0 ? turn : at, CardId: id);
        SearchNode First(string name, PlanAction[] actions, int enemyHp)
        {
            SearchNode node = rootNode;
            foreach (PlanAction action in actions) node = Step(node, action, 0, enemyHp);
            return Label(name, Step(node, new(PlanActionKind.EndTurn, turn), 1, enemyHp));
        }
        SearchNode Later(string name, SearchNode parent, int enemyHp, int hpLost = 0,
            int saves = 0, int survivors = 2, bool dead = false, bool won = false)
            => Label(name, Step(parent, new(PlanActionKind.EndTurn, parent.Turn),
                parent.Snapshot.AdvisoryEnemyCycles + 1, enemyHp, hpLost, saves, survivors, dead, won));

        Pick Select(SearchNode[] pool, SearchNode[]? scope = null, long elapsed = 0, CombatBeamSolver? target = null)
        {
            object batch = AccessTools.Method(typeof(CombatBeamSolver), "PrepareMultiplayerPublicationCandidates")
                .Invoke(target ?? solver, [pool, scope, elapsed])!;
            object selection = AccessTools.Method(typeof(CombatBeamSolver), "SelectMultiplayerFinal").Invoke(target ?? solver, [batch])!;
            object candidate = Property(selection, "Candidate");
            object decision = Property(batch, "Window");
            return new((SearchNode)Property(candidate, "Node"), ((List<SearchNode>)Property(batch, "Candidates")).ToArray(),
                (int)Property(selection, "AdvisoryComparisonCycles"), (string)Property(decision, "Reason"), decision);
        }
        void Check(string name, Pick picked, SearchNode expected, int depth, string reason)
        {
            bool passed = ReferenceEquals(picked.Best, expected) && picked.Depth == depth && picked.Reason == reason
                && picked.Candidates.All(node => !node.Snapshot.HasSimulator);
            evidence.Add(new { name, passed, selected = labels.GetValueOrDefault(picked.Best, "unlabelled"),
                picked.Depth, picked.Decision });
            if (!passed) failures.Add(name);
        }
        try
        {
            SearchNode attack = First("attack1", [Play("ATTACK")], 80);
            SearchNode setup = First("setup1", [Play("SETUP")], 95);
            SearchNode attack2 = Later("attack2", attack, 70);
            SearchNode setup2 = Later("setup2", setup, 55);
            SearchNode[] pool = [attack, setup, attack2, setup2];
            Check("covered_delayed_payback", Select(pool), setup2, 2, "covered");
            Check("missing_own_continuation", Select([attack, setup, setup2]), attack, 1, "coverage_not_deeper");
            Check("retained_missing_representative", Select(pool, [First("third", [Play("THIRD")], 99)]),
                attack, 1, "missing_representative");
            Check("incomplete_current_turn", Select(pool, [Step(rootNode, Play("PENDING"), 0, 99)]),
                attack, 1, "first_turn_incomplete");
            foreach (SearchBoundaryReason boundary in new[] { SearchBoundaryReason.ExternalPlayerChoice,
                SearchBoundaryReason.PendingChoice, SearchBoundaryReason.UnsupportedEffect })
            {
                SearchNode blocked = Step(setup2, Play("BLOCKED", setup2.Turn), 2, 50, boundary: boundary);
                Check($"blocked_{boundary}", Select(pool, [blocked]), attack, 1, "blocked_continuation");
            }
            Check("expired_time", Select(pool, elapsed: 3000), attack, 1, "metadata_budget");
            Check("materialization_reserve", Select(pool, elapsed: 2990), attack, 1, "replay_budget");
            Check("representative_cap", Select(pool, target: Solver(policy with
                { Profile = policy.Profile with { BeamWidth = 1 } })), attack, 1, "representative_limit");
            SearchNode oversized = First("large_choice", [Play(new string('x', 70_000))], 100);
            Check("metadata_ceiling", Select(pool, [oversized]), attack, 1, "metadata_budget");
            var require = policy with { PotionPolicy = SolverPotionPolicy.RequireAtLeastOne,
                PotionStrategy = new(SolverPotionPolicy.RequireAtLeastOne, []) };
            PlanAction potion = new(PlanActionKind.UsePotion, turn, PotionSlot: 0, PotionId: "FIRE_POTION");
            SearchNode usedA = First("used_attack1", [potion, Play("ATTACK")], 80);
            SearchNode usedB = First("used_setup1", [potion, Play("SETUP")], 95);
            SearchNode usedA2 = Later("used_attack2", usedA, 70), usedB2 = Later("used_setup2", usedB, 55);
            Check("eligibility_before_coverage", Select([.. pool, usedA, usedB, usedA2, usedB2], target: Solver(require)),
                usedA, 1, "pending_eligibility");

            // A later failure refutes this concrete path, not every continuation of its first turn.
            SearchNode death = Later("setup_dead", setup2, 10, hpLost: 80, dead: true, survivors: 1);
            Check("dead_suffix_cannot_hide", Select([.. pool, death]), attack2, 2, "covered");
            SearchNode save = Later("setup_save", setup2, 10, saves: 1);
            Check("death_save_cannot_hide", Select([.. pool, save]), attack2, 2, "covered");
            SearchNode excess = Later("setup_excess", setup2, 10, hpLost: 4);
            Check("excess_loss_cannot_hide", Select([.. pool, excess]), attack2, 2, "covered");
            SearchNode teammate = Later("setup_teammate_dead", setup2, 10, survivors: 1);
            Check("teammate_risk_cannot_hide", Select([.. pool, teammate]), attack2, 2, "covered");
            SearchNode alternate = Step(setup, Play("ALTERNATE", setup.Turn), 1, 90);
            SearchNode good = Later("setup_good_branch", alternate, 50);
            Check("good_sibling_survives_bad_suffix", Select([.. pool, death, good]), good, 2, "covered");
            SearchNode costlyAttack = Later("attack_costly", attack2, 60, hpLost: 4);
            Check("known_risk_incumbent_guard", Select([.. pool, excess, costlyAttack]), attack, 1, "known_risk_regression");
            SearchNode won = Later("won", setup, 0, won: true);
            Check("true_victory_keeps_priority", Select([.. pool, won]), won, 2, "covered");
            Check("all_terminal_keeps_baseline", Select([won, death]), won, 3, "all_terminal");

            SearchNode tiedA2 = Later("tie_attack2", attack, 60);
            SearchNode tiedB2 = Later("tie_setup2", setup, 60);
            SearchNode suffix = Label("tie_attack_suffix", Step(tiedA2, Play("UNPAID_SUFFIX", tiedA2.Turn), 2, 1));
            Pick tied = Select([attack, setup, suffix, tiedB2]);
            object tiedBatch = AccessTools.Method(typeof(CombatBeamSolver), "PrepareMultiplayerPublicationCandidates")
                .Invoke(solver, [new[] { attack, setup, suffix, tiedB2 }, null, 0L])!;
            var compare = (Comparison<SearchNode>)Property(Property(tiedBatch, "Ordering"), "Compare");
            bool suffixIgnored = tied.Depth == 2 && compare(suffix, tiedB2) == 0 && compare(tiedB2, suffix) == 0;
            evidence.Add(new { name = "post_checkpoint_damage_and_actions_not_rewarded", passed = suffixIgnored });
            if (!suffixIgnored) failures.Add("post_checkpoint_damage_and_actions_not_rewarded");

            PlanCardToken token = new("PICK", 1, "state;with:delimiters", 0, 0, "title");
            PlanCardChoice choice = new(PlanChoiceEffect.Exhaust, PileType.Hand, [token], "source", "context");
            PlanAction identity = Play("SAME") with { TargetCombatId = 2, Choice = choice };
            PlanAction[] different = [
                identity with { CardOccurrence = 1 }, identity with { CardStateOccurrence = 1 },
                identity with { TargetCombatId = 3 }, identity with { CardStateKey = "another" },
                identity with { CardUpgradeLevel = 1 }, identity with { CardEnchantmentId = "ENCHANTMENT" },
                identity with { Choice = choice with { Cards = [token with { SourceOccurrence = 1 }] } },
                identity with { Choice = choice with { Cards = [token with { OptionOccurrence = 1 }] } },
                identity with { Choice = choice with { ContextId = "other" } },
                identity with { NestedChoices = [choice], NestedChoicesBeforePrimary = 1 },
                identity with { TurnStartChoices = [choice] },
            ];
            foreach ((PlanAction variant, int index) in different.Select((action, index) => (action, index)))
            {
                SearchNode shallow = First($"identity_{index}", [identity], 80);
                SearchNode other = First($"variant_{index}", [variant], 95);
                Check($"complete_identity_{index}", Select([shallow, Later($"variant_deep_{index}", other, 30)]),
                    shallow, 1, "coverage_not_deeper");
            }
            SearchNode orderAB = First("AB", [Play("A"), Play("B")], 80);
            SearchNode orderBA = First("BA", [Play("B"), Play("A")], 95);
            Check("entire_card_order", Select([orderAB, Later("BA2", orderBA, 30)]), orderAB, 1, "coverage_not_deeper");
            SearchNode slot0 = First("slot0", [potion], 80), slot1 = First("slot1", [potion with { PotionSlot = 1 }], 95);
            Check("potion_slot_identity", Select([slot0, Later("slot1_deep", slot1, 30)]), slot0, 1, "coverage_not_deeper");
            SearchNode duplicateRoot = rootNode with { Snapshot = Copy(initial) };
            SearchNode foreign = Step(duplicateRoot, new(PlanActionKind.EndTurn, turn), 1, 95);
            Check("different_root_rejected", Select(pool, [foreign]), attack, 1, "root_or_path_mismatch");

            SearchNode extraA = Step(rootNode, Play("EXTRA") with { EndsPlayerTurn = true }, 0, 99, extra: true);
            SearchNode extraB = Step(rootNode, Play("OTHER_EXTRA") with { EndsPlayerTurn = true }, 0, 99, extra: true);
            Check("extra_turn_is_not_enemy_cycle", Select(pool, [extraA]), attack, 1, "first_cycle_incomplete");
            SearchNode extraA1 = Later("extra_a1", extraA, 80), extraB1 = Later("extra_b1", extraB, 95);
            SearchNode extraA2 = Later("extra_a2", extraA1, 70), extraB2 = Later("extra_b2", extraB1, 50);
            Check("forced_end_and_extra_turn_coverage", Select([extraA1, extraB1, extraA2, extraB2]), extraB2, 2, "covered");
            SearchNode cloneSetup = setup with { Outcome = setup.Outcome! with { MaxBlock = 42 } };
            SearchNode clonedDeep = Later("cloned_setup_deep", cloneSetup, 50);
            Check("annotation_clone_is_same_path", Select([attack, setup, attack2, clonedDeep]), clonedDeep, 2, "covered");
            SearchNode[] crowded = [.. Enumerable.Range(0, 12).Select(_ => attack with { }), setup, attack2, setup2];
            Check("coverage_precedes_4b_cut", Select(crowded, target: Solver(policy with
                { Profile = policy.Profile with { BeamWidth = 2 } })), setup2, 2, "covered");
            Pick samePublication = Select(pool);
            Check("same_pool_preview_final", samePublication, setup2, 2, "covered");
            SearchNode suffixVariant = Label("setup_choice_suffix", Step(setup2, Play("SUFFIX", setup2.Turn), 2, 25));
            object comparisonBatch = AccessTools.Method(typeof(CombatBeamSolver), "PrepareMultiplayerPublicationCandidates")
                .Invoke(solver, [new[] { attack, setup, attack2, suffixVariant, good }, null, 0L])!;
            var compareCovered = (Comparison<SearchNode>)Property(Property(comparisonBatch, "Ordering"), "Compare");
            var comparisonPool = (List<SearchNode>)Property(comparisonBatch, "Candidates");
            int triples = 0;
            bool transitive = true;
            foreach (SearchNode a in comparisonPool)
            foreach (SearchNode b in comparisonPool)
            foreach (SearchNode c in comparisonPool)
            {
                transitive &= Math.Sign(compareCovered(a, b)) == -Math.Sign(compareCovered(b, a))
                    && !(compareCovered(a, b) <= 0 && compareCovered(b, c) <= 0 && compareCovered(a, c) > 0);
                triples++;
            }
            evidence.Add(new { name = "covered_order_transitive", triples, passed = transitive });
            if (!transitive) failures.Add("covered_order_transitive");

            int permutations = 0;
            foreach (SearchNode[] permutation in Permute(pool))
            {
                Pick picked = Select(permutation);
                if (!ReferenceEquals(picked.Best, setup2) || picked.Depth != 2) failures.Add("permutation");
                permutations++;
            }
            evidence.Add(new { name = "input_permutations", permutations, passed = !failures.Contains("permutation") });
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            bool propagated = false;
            try { Select(pool, target: Solver(cancellation: cancelled.Token)); }
            catch (TargetInvocationException error) when (error.InnerException is OperationCanceledException) { propagated = true; }
            evidence.Add(new { name = "cancellation_propagates", passed = propagated });
            if (!propagated) failures.Add("cancellation_propagates");
            File.WriteAllText(Path.Combine(options.OutputDirectory, "covered-window-contracts.json"),
                JsonSerializer.Serialize(new { syntheticRankingFacts = true, nativeSimulation = false,
                    cases = evidence.Count, evidence, failures }, UnattendedTestFiles.JsonOptions));
            if (failures.Count > 0) throw new InvalidOperationException("Covered window contracts: " + string.Join(", ", failures));
            return $"covered_window_contracts={evidence.Count} permutations={permutations} detached_snapshots=true";
        }
        finally { template.ReleaseSimulator(); }
    }

    private static object Property(object instance, string name) => AccessTools.Property(instance.GetType(), name).GetValue(instance)!;

    private static IEnumerable<SearchNode[]> Permute(SearchNode[] nodes)
    {
        if (nodes.Length == 0) { yield return []; yield break; }
        for (int index = 0; index < nodes.Length; index++)
            foreach (SearchNode[] tail in Permute(nodes.Where((_, position) => position != index).ToArray()))
                yield return [nodes[index], .. tail];
    }
}
