using System.Text.Json;
using CombatSolver;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Runs;

namespace OfflineSearchHarness;

/// <summary>
/// Runs the first, diagnostic-only long-term experiment. It records where existing routes
/// disappear; it never changes scoring, retention, state keys, or final ordering.
/// </summary>
internal static class MultiplayerLongTermContracts
{
    private const int MaximumObservedEvents = 32_768;

    private sealed record EventSample(
        string Stage,
        string Reason,
        int BoundaryId,
        string StateKey,
        string? ParentStateKey,
        int Turn,
        int ActionCount,
        string[] Actions,
        int PlayerHp,
        int EnemyHp,
        int CumulativeEnemyHpLost,
        int? Stars,
        int? FutureResourceValue,
        int? PersistentBuffValue,
        int? StrategicRetentionValue,
        int? DelayedDamageValue,
        int? LongTermResourceValue);

    public static string Run(CombatState state, HarnessOptions options, MainLoopContext loop)
    {
        Player local = LocalContext.GetMe(state)
            ?? throw new InvalidOperationException("Long-term fixture has no local player.");
        if (state.Players.Count != 2 || ReferenceEquals(local, state.Players[0]) || state.Enemies.Count != 1)
            throw new InvalidOperationException("Long-term fixture requires two players, local index 1 and one enemy.");

        void Native(Task task) => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(20), "Long-term fixture setup");
        foreach (Player player in state.Players)
        {
            foreach (var potion in player.PotionSlots.ToArray()) potion?.Discard();
            Native(CardPileCmd.RemoveFromCombat(player.PlayerCombatState!.AllCards.ToArray(), skipVisuals: true));
            player.Creature.SetMaxHpInternal(500);
            player.Creature.SetCurrentHpInternal(500);
        }

        Player peer = state.Players[0];
        var enemy = state.Enemies[0];
        enemy.SetMaxHpInternal(500);
        enemy.SetCurrentHpInternal(500);
        foreach (CardModel model in new CardModel[]
        {
            ModelDb.Card<StrikeIronclad>(),
            ModelDb.Card<DefendIronclad>(),
            ModelDb.Card<Inflame>(),
        })
        {
            Native(CardPileCmd.AddGeneratedCardToCombat(state.CreateCard(model, local), PileType.Hand, local));
        }

        Native(OrbCmd.Channel<DarkOrb>(new ThrowingPlayerChoiceContext(), local));
        DarkOrb dark = local.PlayerCombatState!.OrbQueue.Orbs.OfType<DarkOrb>().Single();
        Native(OrbCmd.Passive(new ThrowingPlayerChoiceContext(), dark, target: null));

        // Stars are captured as a real root fact so the diagnostic can show whether they survive
        // the existing path boundaries, without inventing a new scoring rule for them.
        Native(PlayerCmd.GainStars(3, local));
        Native(RunManager.Instance.ActionExecutor.FinishedExecutingActions());

        ContinuationStamp liveBefore = ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true);
        CombatRootSnapshot root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        SearchPolicySnapshot policy = new MultiplayerSearchPolicy(Horizon: 3).Apply(
            SolverController.CaptureSearchPolicy(SolverSettings.Capture(), state,
                includeTurnSetup: false, theftPolicy: null)) with
        {
            FixedBudget = true,
            BudgetOverrideMilliseconds = Math.Min(options.BudgetMilliseconds, 1_000),
            Profile = new SolverSearchProfile(
                BeamWidth: Math.Clamp(options.Beam ?? 8, 2, 16),
                MaxExpandedNodes: Math.Clamp(options.Nodes ?? 80, 20, 200),
                MaxCardBranchesPerNode: 16,
                MaxPileChoiceBranchesPerAction: 4,
                MaxHandChoiceBranchesPerAction: 4,
                SoftTimeBudgetMilliseconds: Math.Min(options.BudgetMilliseconds, 1_000)),
            MaxDegreeOfParallelism = 1,
        };

        List<SearchPathObservation> observations = [];
        object observationGate = new();
        int dropped = 0;
        SearchPathObserver observer = new(
            _ => true,
            observation =>
            {
                lock (observationGate)
                {
                    if (observations.Count >= MaximumObservedEvents)
                    {
                        dropped++;
                        return;
                    }
                    observations.Add(observation);
                }
            },
            _ => true);
        policy = policy with
        {
            Diagnostics = new SearchDiagnosticsSink(
                policy.Diagnostics.Info, policy.Diagnostics.Debug, observer),
        };

        SolverDisplayNames names = SolverDisplayNames.Capture(state);
        BattleDamageSnapshot damage = BattleDamageTracker.Observe(state);
        CombatBeamSolver solver = new(root, names, damage, policy, searchProfile: policy.Profile);
        Task<SolverResult> search = Task.Run(solver.Solve);
        loop.RunUntilCompleted(search, TimeSpan.FromSeconds(60), "Multiplayer long-term diagnostics");
        SolverResult result = search.GetAwaiter().GetResult();

        SearchPolicySnapshot controlPolicy = policy with
        {
            Diagnostics = new SearchDiagnosticsSink(policy.Diagnostics.Info, policy.Diagnostics.Debug),
        };
        CombatRootSnapshot controlRoot = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        CombatBeamSolver controlSolver = new(controlRoot, names, damage, controlPolicy, searchProfile: controlPolicy.Profile);
        Task<SolverResult> controlSearch = Task.Run(controlSolver.Solve);
        loop.RunUntilCompleted(controlSearch, TimeSpan.FromSeconds(60), "Multiplayer long-term control search");
        SolverResult control = controlSearch.GetAwaiter().GetResult();
        bool sameActions = result.BestNode.Actions.Select(ActionIdentity).SequenceEqual(
            control.BestNode.Actions.Select(ActionIdentity), StringComparer.Ordinal);
        SimulationSnapshot observedReplay = solver.ReplayMultiplayerForTesting(result.BestNode.Actions);
        SimulationSnapshot controlReplay = controlSolver.ReplayMultiplayerForTesting(control.BestNode.Actions);
        bool stateKeyUnchanged = observedReplay.StateKey == controlReplay.StateKey;
        bool rankingUnchanged = sameActions && stateKeyUnchanged
            && result.Snapshot.PlayerHp == control.Snapshot.PlayerHp
            && result.Snapshot.EnemyHp == control.Snapshot.EnemyHp
            && result.Snapshot.CumulativePlayerHpLost == control.Snapshot.CumulativePlayerHpLost
            && result.BoundaryReason == control.BoundaryReason
            && result.ExpandedNodes == control.ExpandedNodes
            && result.TransitionCount == control.TransitionCount;
        observedReplay.ReleaseSimulator();
        controlReplay.ReleaseSimulator();
        if (!result.IsMultiplayerAdvice || !control.IsMultiplayerAdvice || !rankingUnchanged)
            throw new InvalidOperationException("Path diagnostics changed the multiplayer search result.");

        ContinuationStamp liveAfter = ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true);
        if (liveBefore.StateText != liveAfter.StateText)
            throw new InvalidOperationException("Long-term diagnostics changed the live combat state.");
        if (dropped != 0)
            throw new InvalidOperationException($"Long-term path diagnostics exceeded the {MaximumObservedEvents} event limit.");
        if (observations.Count == 0)
            throw new InvalidOperationException("Long-term path diagnostics observed no search paths.");
        if (!observations.Any(item => item.Stage == SearchPathObservationStage.PruneInput)
            || !observations.Any(item => item.Stage == SearchPathObservationStage.PruneFinal))
            throw new InvalidOperationException("Long-term path diagnostics did not reach a prune boundary.");

        var stageCounts = observations.GroupBy(item => item.Stage.ToString())
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var firstLoss = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["prune_input_to_final"] = CountBoundaryDrops(
                observations, SearchPathObservationStage.PruneInput, SearchPathObservationStage.PruneFinal),
            ["retention_pool_input_to_final"] = CountBoundaryDrops(
                observations, SearchPathObservationStage.RetentionPoolInput,
                SearchPathObservationStage.RetentionPoolFinal),
        };
        SearchPathEvaluationValues[] evaluations = observations
            .Where(item => item.Retention?.Evaluation is not null)
            .Select(item => item.Retention!.Evaluation!.Value)
            .ToArray();
        var facts = new
        {
            evaluatedStates = evaluations.Length,
            nonZeroFutureResourceStates = evaluations.Count(item => item.FutureResourceValue != 0),
            nonZeroStrategicRetentionStates = evaluations.Count(item => item.StrategicRetentionValue != 0),
            nonZeroPersistentBuffStates = evaluations.Count(item => item.PersistentBuffValue != 0),
            nonZeroDelayedDamageStates = evaluations.Count(item => item.DelayedDamageValue != 0),
            starsObserved = evaluations.Length == 0 ? 0 : evaluations.Max(item => item.Stars),
            futureResourceMaximum = evaluations.Length == 0 ? 0 : evaluations.Max(item => item.FutureResourceValue),
            strategicRetentionMaximum = evaluations.Length == 0 ? 0 : evaluations.Max(item => item.StrategicRetentionValue),
        };
        EventSample[] samples = observations
            .Where(item => item.Stage is SearchPathObservationStage.PruneInput
                or SearchPathObservationStage.PruneFinal
                or SearchPathObservationStage.GlobalRetention
                or SearchPathObservationStage.RetentionPoolFinal)
            .Take(32)
            .Select(Project)
            .ToArray();

        File.WriteAllText(
            Path.Combine(options.OutputDirectory, "long-term-path-diagnostics.json"),
            JsonSerializer.Serialize(new
            {
                contract = "MULTIPLAYER-LONG-TERM-PATH-DIAGNOSTICS",
                rankingUnchanged,
                stateKeyUnchanged,
                fixture = new
                {
                    players = state.Players.Count,
                    localPlayerIndex = Array.IndexOf(state.Players.ToArray(), local),
                    peerHp = peer.Creature.CurrentHp,
                    enemyHp = enemy.CurrentHp,
                    cards = new[] { "STRIKE_IRONCLAD", "DEFEND_IRONCLAD", "INFLAME" },
                    localStars = local.PlayerCombatState!.Stars,
                    darkOrbEvokeValue = dark.EvokeVal,
                },
                solver = new
                {
                    result.IsMultiplayerAdvice,
                    result.ExpandedNodes,
                    result.TransitionCount,
                    result.AdvisoryComparisonCycles,
                    result.BoundaryReason,
                    policy.Profile.BeamWidth,
                    policy.Profile.MaxExpandedNodes,
                    policy.BudgetOverrideMilliseconds,
                },
                observationCount = observations.Count,
                stageCounts,
                firstLoss,
                facts,
                samples,
            }, UnattendedTestFiles.JsonOptions));

        return $"events={observations.Count} stages={stageCounts.Count} pruneDrops={firstLoss["prune_input_to_final"]} "
            + $"retentionDrops={firstLoss["retention_pool_input_to_final"]} expanded={result.ExpandedNodes} "
            + "ranking_unchanged=true state_key_unchanged=true";
    }

    private static int CountBoundaryDrops(
        IReadOnlyList<SearchPathObservation> observations,
        SearchPathObservationStage inputStage,
        SearchPathObservationStage outputStage)
    {
        var input = observations.Where(item => item.Stage == inputStage)
            .Select(item => (item.BoundaryId, RouteKey(item), item.StateKey))
            .ToHashSet();
        var output = observations.Where(item => item.Stage == outputStage)
            .Select(item => (item.BoundaryId, RouteKey(item), item.StateKey))
            .ToHashSet();
        return input.Count(key => !output.Contains(key));
    }

    private static string RouteKey(SearchPathObservation observation)
        => string.Join(",", observation.Actions.Select(action =>
            $"{action.Turn}:{action.Kind}:{action.CardId ?? action.PotionId ?? "-"}:{action.CardStateKey}"));

    private static string ActionIdentity(PlanAction action)
        => $"{action.Turn}:{action.Kind}:{action.CardId ?? action.PotionId ?? "-"}:"
            + $"{action.TargetCombatId}:{action.CardStateKey}:{action.PotionSlot}";

    private static EventSample Project(SearchPathObservation observation)
    {
        SearchPathEvaluationValues? evaluation = observation.Retention?.Evaluation;
        return new(
            observation.Stage.ToString(), observation.Reason, observation.BoundaryId,
            observation.StateKey.ToString(), observation.ParentStateKey?.ToString(), observation.Turn,
            observation.ActionCount, observation.Actions.Select(action =>
                $"{action.Turn}:{action.Kind}:{action.CardId ?? action.PotionId ?? "-"}").ToArray(),
            observation.PlayerHp, observation.EnemyHp, observation.CumulativeEnemyHpLost,
            evaluation?.Stars, evaluation?.FutureResourceValue, evaluation?.PersistentBuffValue,
            evaluation?.StrategicRetentionValue, evaluation?.DelayedDamageValue,
            evaluation?.LongTermResourceValue);
    }
}
