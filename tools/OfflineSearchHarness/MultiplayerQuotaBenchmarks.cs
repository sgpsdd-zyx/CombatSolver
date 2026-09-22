using System.Text.Json;
using CombatSolver;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;

namespace OfflineSearchHarness;

internal static class MultiplayerQuotaBenchmarks
{
    public static string Run(CombatState state, HarnessOptions options, MainLoopContext loop)
    {
        GameBootstrap.Harmony.Patch(AccessTools.Method(typeof(Godot.Time), nameof(Godot.Time.GetTicksMsec)),
            prefix: new HarmonyMethod(typeof(MultiplayerReviewContracts), "ClockPrefix"));
        var local = LocalContext.GetMe(state)!;
        string scenario = options.Scenario.MultiplayerReviewStage!;
        bool defense = scenario == "quota-defense";
        bool investment = scenario == "quota-investment";
        void Native(Task task) => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(15), "Quota benchmark setup");
        foreach (var player in state.Players)
        {
            foreach (var potion in player.PotionSlots.ToArray()) potion?.Discard();
            Native(CardPileCmd.RemoveFromCombat(player.PlayerCombatState!.AllCards.ToArray(), skipVisuals: true));
            player.Creature.SetMaxHpInternal(80);
            player.Creature.SetCurrentHpInternal(player == local && defense ? 6 : 80);
            if (player != local)
                Native(PowerCmd.Apply<BufferPower>(new ThrowingPlayerChoiceContext(), player.Creature, 40,
                    player.Creature, null));
        }
        Native(PlayerCmd.LoseEnergy(local.PlayerCombatState!.Energy - (investment ? 2 : 1), local));
        void Card<T>() where T : CardModel
            => Native(CardPileCmd.AddGeneratedCardToCombat(state.CreateCard(ModelDb.Card<T>(), local), PileType.Hand, local));
        Card<StrikeIronclad>();
        Card<DefendIronclad>();
        if (investment) { Card<Inflame>(); Card<Bash>(); }
        state.Enemies[0].SetMaxHpInternal(defense ? 30 : 160);
        state.Enemies[0].SetCurrentHpInternal(defense ? 30 : 160);
        if (!defense)
            Native(PowerCmd.Apply<RitualPower>(new ThrowingPlayerChoiceContext(), state.Enemies[0], 2,
                state.Enemies[0], null));
        Native(RunManager.Instance.ActionExecutor.FinishedExecutingActions());
        var root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        var before = ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true);
        var policy = new MultiplayerSearchPolicy().Apply(SolverController.CaptureSearchPolicy(
            SolverSettings.Capture(), state, false, null)) with
        {
            FixedBudget = true, BudgetOverrideMilliseconds = options.BudgetMilliseconds,
            Profile = ModRuntime.ResolveProfile(options), MaxDegreeOfParallelism = 1,
            PotionPolicy = SolverPotionPolicy.Disabled,
            PotionStrategy = new(SolverPotionPolicy.Disabled, []),
        };
        var names = SolverDisplayNames.Capture(state);
        var damage = BattleDamageTracker.Observe(state);
        var search = Task.Run(() => CombatSearchCoordinator.Solve(root, names,
            damage, policy, CancellationToken.None, null));
        loop.RunUntilCompleted(search, TimeSpan.FromSeconds(25), "Quota benchmark search");
        var result = search.GetAwaiter().GetResult();
        if (before.StateText != ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true).StateText
            || result.ExpandedNodes > policy.Profile.MaxExpandedNodes)
            throw new InvalidOperationException("Quota benchmark changed the root or exceeded its node budget.");
        var evidence = new { scenario, root.InitialPlayerHp, policy.Profile,
            result.ExpandedNodes, result.TransitionCount, result.Elapsed,
            result.AdvisoryComparisonCycles, result.Snapshot.AdvisoryEnemyCycles,
            result.AdvisoryObjective, result.AdvisoryPlannedContribution, result.AdvisoryObjectiveWitness,
            result.AdvisoryQuotaFrontierCount, result.AdvisorySearchedEnemyCycles,
            result.Snapshot.PlayerHp, result.Snapshot.PlayerDead, result.Snapshot.AllEnemiesDead,
            result.Snapshot.EnemyHp, result.Snapshot.CumulativePlayerHpLost,
            boundary = result.BoundaryReason.ToString(), result.EnemyHpLostByTurn, result.HpLostByTurn,
            firstTurn = result.BestNode.Actions.TakeWhile(action => action.Turn == root.StartTurnNumber).ToArray(),
            actions = result.BestNode.Actions };
        File.WriteAllText(Path.Combine(options.OutputDirectory, scenario + ".json"),
            JsonSerializer.Serialize(evidence, UnattendedTestFiles.JsonOptions));
        return JsonSerializer.Serialize(new { scenario, result.ExpandedNodes, result.Snapshot.EnemyHp,
            result.Snapshot.PlayerHp, result.AdvisoryComparisonCycles });
    }
}
