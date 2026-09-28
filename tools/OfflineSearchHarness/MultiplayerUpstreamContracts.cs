using System.Reflection;
using System.Text.Json;
using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Simulation;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;

namespace OfflineSearchHarness;

internal static class MultiplayerUpstreamContracts
{
    private static bool ClockPrefix(ref ulong __result) { __result = 0; return false; }

    internal static string Run(CombatState state, HarnessOptions options, MainLoopContext loop)
    {
        Player local = LocalContext.GetMe(state)!;
        if (state.Players.Count != 2 || ReferenceEquals(local, state.Players[0]))
            throw new InvalidOperationException("Upstream compatibility needs two players and local index 1.");
        Player peer = state.Players[0];
        void Native(Task task) => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(20), "Upstream compatibility");
        foreach (Player player in state.Players)
            Native(CardPileCmd.RemoveFromCombat(player.PlayerCombatState!.AllCards.ToArray(), skipVisuals: true));
        void Add(CardModel card, Player owner)
            => Native(CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, owner));
        Add(state.CreateCard(ModelDb.Card<GoldAxe>(), local), local);
        Add(state.CreateCard(ModelDb.Card<DefendIronclad>(), local), local);
        Add(state.CreateCard(ModelDb.Card<DefendIronclad>(), peer), peer);
        var improvement = (MadScience)state.CreateCard(ModelDb.Card<MadScience>(), local);
        improvement.TinkerTimeType = CardType.Power;
        improvement.TinkerTimeRider = TinkerTime.RiderEffect.Improvement;
        Add(improvement, local);

        List<string> checks = [];
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks.Add(name);
            Console.WriteLine($"UPSTREAM_COMPAT Passed {name}");
        }
        SearchPolicySnapshot captured = SolverController.CaptureSearchPolicy(SolverSettings.Capture(), state, false, null);
        Check(!captured.GrowthOpportunityTargets.HasTargets, "multiplayer_skips_single_player_growth_targets");
        SearchPolicySnapshot policy = new MultiplayerSearchPolicy().Apply(captured with
        {
            EarlyTurnExplorationDepth = 2,
            EarlyTurnExplorationBudgetMilliseconds = 2_400_000,
            DevelopmentStrategy = new(new RejectSoloStrategy(), new Dictionary<string, double>()),
            UseBeamWidthPortfolio = true,
            UseNoveltyPortfolio = true,
        });
        Check(policy.EarlyTurnExplorationDepth == 0
            && policy.EarlyTurnExplorationBudgetMilliseconds == 0
            && policy.DevelopmentStrategy == null
            && !policy.UseBeamWidthPortfolio && !policy.UseNoveltyPortfolio,
            "multiplayer_discards_solo_exploration_and_script_policy");
        var root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        var names = SolverDisplayNames.Capture(state);
        var damage = BattleDamageTracker.Observe(state);
        var soloPlan = new PlanCommitment(PlanCommitmentKind.CopyPower, [], root.StartTurnNumber,
            new(PlanPayoffEvidenceKind.RegisteredPowerBenefit, "IMPROVEMENT_POWER", root.StartTurnNumber),
            UsesPotion: false, Priority: 1);
        foreach (bool multiplayer in new[] { false, true })
        {
            var configured = new CombatBeamSolver(root, names, damage,
                policy with { Multiplayer = multiplayer ? policy.Multiplayer : null },
                searchProfile: policy.Profile, planCommitment: soloPlan);
            object? retained = typeof(CombatBeamSolver)
                .GetField("_planCommitment", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(configured);
            Check(multiplayer ? retained == null : ReferenceEquals(retained, soloPlan),
                multiplayer ? "multiplayer_rejects_solo_plan_commitment" : "solo_retains_official_plan_commitment");
        }
        var solver = new CombatBeamSolver(root, names, damage, policy, searchProfile: policy.Profile);
        var parent = root.ForkSimulator();
        var child = parent.Fork();
        var parentState = (SimulatedCombatState)parent.State.CombatState;
        Check(parentState.MadScienceUpgradeCapacity == 0, "multiplayer_has_no_upgrade_credit_capacity");

        StateFingerprint HistoryKey(CombatPredictionSimulator simulator, Player owner)
        {
            StateFingerprintBuilder key = new();
            CombatHistoryCounterKey.Append(ref key, simulator, owner);
            return key.Finish();
        }
        StateFingerprint initialHistory = HistoryKey(parent, local);
        void Record(Player owner, bool ethereal)
        {
            PredictedCard card = child.State.GetPlayerCombatState(owner).Hand.Cards
                .Single(value => value.Preview is DefendIronclad);
            using (child.PushActionSource(card.Original, PredictionActionKind.CardPlay))
            {
                CardPlay play = new() { Card = card.MutablePreview, Player = owner, Target = null,
                    ResultPile = PileType.Discard, Resources = default, IsAutoPlay = false, PlayIndex = 0, PlayCount = 1 };
                child.History.CardPlayStarted(card, play);
                child.History.CardPlayFinished(card, play, ethereal);
                var draw = child.History.CardDrawn(card, fromHandDraw: true);
                child.History.CardDrawResolved(draw, card);
                var generated = child.History.CardGenerated(card, owner, CardGenerationResultKind.Fixed);
                child.History.CardGenerationResolved(generated, card);
                child.History.DamageReceived(owner.Creature, null,
                    new DamageResult(owner.Creature, default) { UnblockedDamage = 1 }, null, CombatDamageSource.Unknown);
            }
        }
        Record(peer, true);
        Record(local, false);
        var localCounts = CombatHistoryCounters.Scan(child.History, local);
        var peerCounts = CombatHistoryCounters.Scan(child.History, peer);
        Check(localCounts == new CombatHistoryCounters(2, 0, 0, 1, 1, 1)
            && peerCounts == new CombatHistoryCounters(2, 1, 0, 1, 1, 1), "history_keeps_per_effect_owner_scope");
        foreach (Player owner in state.Players)
        {
            StateFingerprintBuilder scan = new();
            CombatHistoryCounterKey.AppendCounters(ref scan, CombatHistoryCounters.Scan(child.History, owner));
            Check(HistoryKey(child, owner) == scan.Finish(), "multiplayer_history_key_matches_scan_" + owner.Creature.CombatId);
        }
        Check(HistoryKey(child, local) != initialHistory && HistoryKey(parent, local) == initialHistory,
            "history_key_changes_only_in_child");
        Check(HistoryKey(child.Fork(), local) == HistoryKey(child, local), "history_key_survives_fork");
        decimal Axe(CombatPredictionSimulator simulator)
        {
            PredictedCard card = simulator.State.GetPlayerCombatState(local).Hand.Cards
                .Single(value => value.Preview is GoldAxe);
            if (!CalculatedVarSpecRegistry.TryCalculate((CalculatedVar)card.Preview.DynamicVars.CalculatedDamage,
                    simulator, card, null, out decimal value))
                throw new InvalidOperationException("Gold Axe calculation missing.");
            return value;
        }
        Check(Axe(child) == Axe(parent) + 2, "gold_axe_observes_both_finished_plays");
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
        using (SimulationNotificationIsolation.Enter())
        {
            StateFingerprint parentKey = Key(parent), childKey = Key(child);
            Check(parentKey != childKey && childKey == Key(child.Fork()) && parentKey == Key(parent),
                "production_snapshot_distinguishes_history_and_preserves_forks");
        }
        ContinuationStamp frozen = ContinuationStamp.CapturePredicted(local, root.ForkSimulator(),
            root.StartTurnNumber, root.Forecast, root.StartTurnNumber);
        SimulationSnapshot predicted = solver.ReplayMultiplayerForTesting(
            [new(PlanActionKind.PlayCard, root.StartTurnNumber, CardId: "MAD_SCIENCE")]);
        try
        {
            var combat = (SimulatedCombatState)predicted.Simulator.State.CombatState;
            Check(combat.GetAmount<ImprovementPower>(local.Creature) == 1
                && combat.GetAmount<ImprovementPower>(peer.Creature) == 0
                && combat.GrowthRewards.MadScience == 0, "improvement_applies_to_owner_without_advisor_growth_credit");
            var sibling = (SimulatedCombatState)predicted.Simulator.Fork().State.CombatState;
            sibling.RecordMadScienceGrowthReward();
            Check(sibling.GrowthRewards.MadScience == 0 && combat.GrowthRewards.MadScience == 0,
                "fork_cannot_create_multiplayer_upgrade_credit");
            ContinuationStamp expected = ContinuationStamp.CapturePredicted(local, predicted.Simulator,
                predicted.Turn, root.Forecast, root.StartTurnNumber);
            // Native manual actions stamp the Godot clock; the offline process has no engine.
            var clock = AccessTools.Method(typeof(Godot.Time), nameof(Godot.Time.GetTicksMsec));
            var clockPrefix = AccessTools.Method(typeof(MultiplayerUpstreamContracts), nameof(ClockPrefix));
            GameBootstrap.Harmony.Patch(clock, prefix: new HarmonyMethod(clockPrefix));
            try
            {
                if (!improvement.TryManualPlay(null)) throw new InvalidOperationException("Native Mad Science refused play.");
                Native(RunManager.Instance.ActionExecutor.FinishedExecutingActions());
            }
            finally { GameBootstrap.Harmony.Unpatch(clock, clockPrefix); }
            ContinuationStamp actual = ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true);
            if (expected.StateText != actual.StateText)
                throw new InvalidOperationException("Native multiplayer Mad Science differs: "
                    + string.Join("; ", expected.DescribeDifferences(actual, maximumDifferences: 12)));
            checks.Add("mad_science_native_full_party_state_equal");
            Check(ContinuationStamp.CapturePredicted(local, root.ForkSimulator(), root.StartTurnNumber,
                root.Forecast, root.StartTurnNumber).StateText == frozen.StateText, "root_unchanged_after_native_play");
        }
        finally { predicted.ReleaseSimulator(); }
        VerifyEntropicCosts(state, local, peer, Check);
        File.WriteAllText(Path.Combine(options.OutputDirectory, "upstream-compatibility.json"),
            JsonSerializer.Serialize(new { status = "Passed", checks, localCounts, peerCounts },
                new JsonSerializerOptions { WriteIndented = true }));
        return $"upstream_compatibility_checks={checks.Count} native_full_party_state_equal=true";
    }

    private sealed class RejectSoloStrategy : IDevelopmentSearchStrategy
    {
        public double Prioritize(StrategyActionFeatures action, double builtInPriority,
            IReadOnlyDictionary<string, double> parameters)
            => throw new InvalidOperationException("Solo development strategy reached multiplayer search.");
    }

    private static void VerifyEntropicCosts(CombatState state, Player local, Player peer,
        Action<bool, string> check)
    {
        string live = ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true).StateText;
        var parent = new CombatPredictionSimulator(new SimulatedCombatState(state));
        foreach (Player recipient in new[] { local, peer })
        {
            var branch = parent.Fork();
            var combat = (SimulatedCombatState)branch.State.CombatState;
            check(combat.TryProcurePotion(local, ModelDb.Potion<EntropicBrew>()),
                "entropic_source_procured_" + recipient.Creature.CombatId);
            int sourceSlot = Enumerable.Range(0, combat.PotionSlotCount(local))
                .Single(slot => combat.GetPotionAtSlot(local, slot) is EntropicBrew);
            PotionModel source = combat.GetPotionAtSlot(local, sourceSlot)!;
            combat.ConsumePotion(local, sourceSlot);
            combat.BeforePotionUsed(branch, source, recipient.Creature);
            check(PotionOnUseSupport.Use(branch, combat, source, recipient.Creature),
                "entropic_generated_without_pending_choice_" + recipient.Creature.CombatId);
            combat.AfterPotionUsed(branch, source, recipient.Creature);
            check(combat.PotionUses.Single().StrategicHpCost
                    == PotionUsePolicy.StrategicHpCost(source, renewablePotionShapedRock: false),
                "entropic_source_keeps_own_cost_" + recipient.Creature.CombatId);

            int[] generatedSlots = Enumerable.Range(0, combat.PotionSlotCount(recipient))
                .Where(slot => combat.GetPotionAtSlot(recipient, slot) != null).ToArray();
            check(generatedSlots.Length > 0, "entropic_recipient_inventory_filled_" + recipient.Creature.CombatId);
            var child = branch.Fork();
            var childState = (SimulatedCombatState)child.State.CombatState;
            foreach (int slot in generatedSlots) childState.ConsumePotion(recipient, slot);
            check(childState.PotionUses.Skip(1).All(use => use.StrategicHpCost == 0)
                    && combat.PotionUses.Count == 1
                    && generatedSlots.All(slot => combat.GetPotionAtSlot(recipient, slot) != null),
                "entropic_free_cost_and_inventory_are_fork_local_" + recipient.Creature.CombatId);
            check(childState.TryProcurePotion(recipient, ModelDb.Potion<StrengthPotion>()),
                "ordinary_replacement_procured_" + recipient.Creature.CombatId);
            int replacementSlot = Enumerable.Range(0, childState.PotionSlotCount(recipient))
                .Single(slot => childState.GetPotionAtSlot(recipient, slot) is StrengthPotion);
            childState.ConsumePotion(recipient, replacementSlot);
            check(childState.PotionUses[^1].StrategicHpCost > 0,
                "ordinary_replacement_does_not_inherit_free_cost_" + recipient.Creature.CombatId);
        }
        check(((SimulatedCombatState)parent.State.CombatState).PotionUses.Count == 0
                && ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true).StateText == live,
            "entropic_branches_preserve_root_and_live");
    }
}
