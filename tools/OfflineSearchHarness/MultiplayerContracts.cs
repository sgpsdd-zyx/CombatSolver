using CombatSolver;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace OfflineSearchHarness;

// Uses native managed combat commands; the harness still bypasses Godot and networking.
internal static class MultiplayerContracts
{
    public static string Run(CombatState state, HarnessOptions options, MainLoopContext loop)
    {
        GameBootstrap.Harmony.Patch(HarmonyLib.AccessTools.Method(typeof(Godot.Time), nameof(Godot.Time.GetTicksMsec)),
            prefix: new HarmonyLib.HarmonyMethod(typeof(MultiplayerContracts), nameof(ClockPrefix)));
        // The offline host uses a singleplayer transport. Match the native multiplayer
        // ready-count condition so an idle peer cannot end the local extra turn.
        GameBootstrap.Harmony.Patch(HarmonyLib.AccessTools.Method(typeof(CombatManager),
                "AllPlayersReadyToEndTurn", [typeof(CombatTurnState)]),
            prefix: new HarmonyLib.HarmonyMethod(typeof(MultiplayerContracts), nameof(ReadyPrefix)));
        Player local = LocalContext.GetMe(state)!;
        Require(state.Players.Count == 2, "two players");
        Require(!ReferenceEquals(local, state.Players[0]), "local player is not the first player");
        // Keep an idle party alive long enough to check the entire forecast window.
        foreach (Player player in state.Players)
        {
            player.Creature.SetMaxHpInternal(500);
            player.Creature.SetCurrentHpInternal(500);
        }
        CombatRootSnapshot root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        SolverDisplayNames names = SolverDisplayNames.Capture(state);
        BattleDamageSnapshot damage = BattleDamageTracker.Observe(state);
        SearchPolicySnapshot policy = new MultiplayerSearchPolicy().Apply(SolverController.CaptureSearchPolicy(
            SolverSettings.Capture(), state, includeTurnSetup: false, theftPolicy: null));
        Require(policy.Multiplayer!.Horizon == 7, "default seven-enemy-turn horizon");
        int horizon = policy.Multiplayer.Horizon;
        var solver = new CombatBeamSolver(root, names, damage, policy, searchProfile: policy.Profile);
        var predictions = new List<SimulationSnapshot>();
        for (int rounds = 1; rounds <= horizon; rounds++)
        {
            var actions = Enumerable.Range(root.StartTurnNumber, rounds)
                .Select(turn => new PlanAction(PlanActionKind.EndTurn, turn)).ToArray();
            predictions.Add(solver.ReplayMultiplayerForTesting(actions));
        }
        Require(predictions[^1].BoundaryReason == SearchBoundaryReason.AdvisoryHorizon
            && predictions[^1].AdvisoryEnemyCycles == horizon, "seventh enemy cycle reaches the horizon");
        Require(predictions.Take(horizon - 1).All(prediction => prediction.BoundaryReason == SearchBoundaryReason.None),
            "earlier enemy cycles remain searchable");
        SearchPolicySnapshot fiveTurnPolicy = new MultiplayerSearchPolicy(Horizon: 5).Apply(policy);
        var fiveTurnSolver = new CombatBeamSolver(root, names, damage, fiveTurnPolicy, searchProfile: policy.Profile);
        var fiveTurnPrediction = fiveTurnSolver.ReplayMultiplayerForTesting(
            Enumerable.Range(root.StartTurnNumber, 5).Select(turn => new PlanAction(PlanActionKind.EndTurn, turn)).ToArray());
        Require(fiveTurnPrediction.BoundaryReason == SearchBoundaryReason.AdvisoryHorizon
            && fiveTurnPrediction.AdvisoryEnemyCycles == 5, "five-turn policy has its own horizon");
        fiveTurnPrediction.ReleaseSimulator();
        for (int round = 0; round < horizon - 1; round++)
        {
            int turn = local.PlayerCombatState!.TurnNumber;
            foreach (Player player in state.Players)
                CombatManager.Instance.SetReadyToEndTurn(player, canBackOut: false);
            DateTime deadline = DateTime.UtcNow.AddSeconds(20);
            while (local.PlayerCombatState.TurnNumber == turn
                || local.PlayerCombatState.Phase != PlayerTurnPhase.Play)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Native multiplayer turn did not finish.");
                loop.Pump(TimeSpan.FromMilliseconds(10));
            }
            ContinuationStamp actual = ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true);
            SimulationSnapshot predicted = predictions[round];
            ContinuationStamp expected = ContinuationStamp.CapturePredicted(local, predicted.Simulator,
                predicted.Turn, root.Forecast, root.StartTurnNumber);
            if (actual.StateText != expected.StateText)
                throw new InvalidOperationException("Native multiplayer turn differs: "
                    + string.Join("; ", expected.DescribeDifferences(actual, maximumDifferences: 12)));
        }
        // Native advancement must not alter either the frozen root or an independent fork.
        var again = solver.ReplayMultiplayerForTesting([new(PlanActionKind.EndTurn, root.StartTurnNumber)]);
        Require(again.StateKey == predictions[0].StateKey, "frozen root survives native advancement");
        again.ReleaseSimulator();
        foreach (var prediction in predictions) prediction.ReleaseSimulator();
        CombatRootSnapshot current = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        var currentSolver = new CombatBeamSolver(current, SolverDisplayNames.Capture(state),
            BattleDamageTracker.Observe(state), policy, searchProfile: policy.Profile);
        Task<SolverResult> task = Task.Run(currentSolver.Solve);
        loop.RunUntilCompleted(task, TimeSpan.FromSeconds(90), "Multiplayer search");
        SolverResult result = task.GetAwaiter().GetResult();
        Require(result.IsMultiplayerAdvice && result.AdvisoryHorizon == horizon, "advisory result metadata");
        Require(result.BestNode.Actions.All(action => action.Turn >= current.StartTurnNumber), "local action turns");
        Require(result.ExpandedNodes <= policy.Profile.MaxExpandedNodes, "longer horizon keeps the node budget");
        SolverResult limited = Solve(current, policy with
        {
            Profile = policy.Profile with { MaxExpandedNodes = 1 },
        }, loop);
        Require(limited.ExpandedNodes <= 1 && limited.BoundaryReason == SearchBoundaryReason.NodeLimit
            && limited.Snapshot.AdvisoryEnemyCycles < horizon, "node limit can return a partial forecast");
        SolverOverlaySnapshot overlay = SolverOverlaySnapshot.Capture(limited, unexpectedReplan: false);
        Require(overlay.SummaryText.Contains(SolverText.Format(
            $"敌方回合：已推演 {limited.Snapshot.AdvisoryEnemyCycles} / 上限 {horizon}")),
            "advice displays achieved depth separately from the horizon");
        Require(limited.AdvisoryComparisonCycles == 0 && overlay.SummaryText.Contains(
            SolverText.Get("尚未完成首个敌方周期，当前建议缺少完整受击评估。")),
            "unfinished first-cycle advice does not imply zero incoming damage");
        File.WriteAllText(Path.Combine(options.OutputDirectory, "multiplayer-route.json"),
            System.Text.Json.JsonSerializer.Serialize(result.BestNode.Actions, UnattendedTestFiles.JsonOptions));
        var reusedPolicy = new MultiplayerSearchPolicy(PreviousRoutes: [result.BestNode.Actions]).Apply(policy);
        SolverResult reused = Solve(current, reusedPolicy, loop);
        Require(reused.ReplayedAdviceActions > 0, "previous route is revalidated");
        Require(reused.Snapshot.CumulativePlayerHpLost <= result.Snapshot.CumulativePlayerHpLost,
            "replay seed preserves defense");
        var invalid = result.BestNode.Actions.Select(action => action with { CardStateKey = "missing-card" }).ToArray();
        SolverResult rejected = Solve(current,
            new MultiplayerSearchPolicy(PreviousRoutes: [invalid]).Apply(policy), loop);
        Require(rejected.ReplayedAdviceActions == 0, "invalid seed is rejected");
        VerifyActions(state, local, options, loop, policy);
        VerifyExtraTurn(state, local, loop, policy);
        VerifyChoicesAndPotions(state, local, loop, policy);
        VerifyRuntimeIsolation(state);
        return $"native_turns={horizon - 1}:equal horizons=5,7 frozen_root=equal local_index=1 boundary={result.BoundaryReason} "
            + $"actions={result.BestNode.ActionCount} turns={result.SearchedTurns} reuse={reused.ReplayedAdviceActions} "
            + "cards=4 peer_potion=equal extra_turn=equal peer_choice=boundary potion_choice=native_equal "
            + "potion_directives=passed native_controls=blocked partial_depth_ui=passed";
    }

    private static void VerifyActions(CombatState state, Player local, HarnessOptions options,
        MainLoopContext loop, SearchPolicySnapshot policy)
    {
        Player peer = state.Players.First(player => player != local);
        void Native(Task task) => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(20), "Native action");
        foreach (CardModel model in new CardModel[] { ModelDb.Card<Lift>(), ModelDb.Card<BelieveInYou>(), ModelDb.Card<TagTeam>(), ModelDb.Card<Flanking>() })
        {
            CardModel card = state.CreateCard(model, local);
            Native(CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, local));
            Native(PlayerCmd.GainEnergy(3, local));
            var target = card.TargetType == TargetType.AnyAlly ? peer.Creature : state.Enemies[0];
            var actionRoot = CombatRootSnapshot.Capture(state, true);
            var branch = actionRoot.ForkSimulator();
            var combat = (SimulatedCombatState)branch.State.CombatState;
            combat.BeginActionChoices((IReadOnlyList<PlanCardChoice>?)null);
            using (SimulationNotificationIsolation.Enter())
                Require(branch.AutoPlay(branch.State.FindCard(card)!, target), "auto play " + card.Id.Entry);
            combat.EndActionChoices();
            Native(CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), card, target, skipCardPileVisuals: true));
            AssertNativeState(state, actionRoot, branch, "card " + card.Id.Entry);
        }
        foreach (var potion in local.PotionSlots.ToArray()) potion?.Discard();
        PotionModel block = ModelDb.Potion<BlockPotion>().ToMutable();
        Native(PotionCmd.TryToProcure(block, local));
        var potionRoot = CombatRootSnapshot.Capture(state, true);
        int slot = local.GetPotionSlotIndex(block);
        var potionSolver = new CombatBeamSolver(potionRoot, SolverDisplayNames.Capture(state),
            BattleDamageTracker.Observe(state), policy, searchProfile: policy.Profile);
        var potions = potionSolver.BuildOpeningPotionActions();
        Require(potions.Any(action => action.TargetCombatId == peer.Creature.CombatId), "potion teammate target");
        Require(potions.Any(action => action.TargetCombatId == local.Creature.CombatId), "potion self target");
        var predicted = potionSolver.ReplayMultiplayerForTesting([potions.First(action => action.TargetCombatId == peer.Creature.CombatId)]);
        Native(block.OnUseWrapper(new ThrowingPlayerChoiceContext(), peer.Creature));
        AssertNativeState(state, potionRoot, predicted.Simulator, "potion thrown at peer");
        predicted.ReleaseSimulator();

        var choiceRoot = CombatRootSnapshot.Capture(state, true);
        var choiceBranch = choiceRoot.ForkSimulator();
        var choiceCombat = (SimulatedCombatState)choiceBranch.State.CombatState;
        bool refused = false;
        try { choiceCombat.RequireLocalChoice(peer); }
        catch (ExternalPlayerChoiceException) { refused = true; }
        Require(refused, "teammate choice is explicit");

        // A fresh root after teammate RNG consumption must differ, while the earlier root stays frozen.
        string frozen = choiceRoot.ContinuationStamp.StateText;
        state.RunState.Rng.CombatCardGeneration.NextInt(100);
        Require(ContinuationStamp.CaptureLive(state, true).StateText != frozen, "teammate RNG invalidates advice");
        Require(ContinuationStamp.CapturePredicted(local, choiceRoot.ForkSimulator(), choiceRoot.StartTurnNumber,
            choiceRoot.Forecast, choiceRoot.StartTurnNumber).StateText == frozen, "RNG root remains frozen");
    }

    private static SolverResult Solve(CombatRootSnapshot root, SearchPolicySnapshot policy, MainLoopContext loop)
    {
        var names = SolverDisplayNames.Capture(CombatManager.Instance.DebugOnlyGetState()!);
        var damage = BattleDamageTracker.Observe(CombatManager.Instance.DebugOnlyGetState()!);
        Task<SolverResult> task = Task.Run(() => CombatSearchCoordinator.Solve(
            root, names, damage, policy, CancellationToken.None, null));
        loop.RunUntilCompleted(task, TimeSpan.FromSeconds(90), "Advisory contract search");
        return task.GetAwaiter().GetResult();
    }

    private static void VerifyExtraTurn(CombatState state, Player local, MainLoopContext loop,
        SearchPolicySnapshot policy)
    {
        loop.RunUntilCompleted(PowerCmd.Apply<AmbergrisPower>(new ThrowingPlayerChoiceContext(),
            local.Creature, 1, local.Creature, null), TimeSpan.FromSeconds(20), "Extra turn setup");
        var root = CombatRootSnapshot.Capture(state, true);
        var solver = new CombatBeamSolver(root, SolverDisplayNames.Capture(state),
            BattleDamageTracker.Observe(state), policy, searchProfile: policy.Profile);
        var predicted = solver.ReplayMultiplayerForTesting([new(PlanActionKind.EndTurn, root.StartTurnNumber)]);
        Require(((SimulatedCombatState)predicted.Simulator.State.CombatState).AdvisorEnemyCycles == 0,
            "extra turn does not consume enemy horizon");
        Require(predicted.AdvisoryLastEnemyCycle == null, "extra turn does not create an enemy-cycle observation");
        int oldTurn = local.PlayerCombatState!.TurnNumber;
        foreach (Player player in state.Players) CombatManager.Instance.SetReadyToEndTurn(player, false);
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (local.PlayerCombatState.TurnNumber == oldTurn || local.PlayerCombatState.Phase != PlayerTurnPhase.Play)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Extra turn did not start.");
            loop.Pump(TimeSpan.FromMilliseconds(10));
        }
        AssertNativeState(state, root, predicted.Simulator, "local extra turn");
        predicted.ReleaseSimulator();
    }

    private static void VerifyChoicesAndPotions(CombatState state, Player local, MainLoopContext loop,
        SearchPolicySnapshot policy)
    {
        void Native(Task task) => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(20), "Potion/choice setup");
        Player peer = state.Players.First(player => player != local);
        Native(PowerCmd.Apply<ToolsOfTheTradePower>(new ThrowingPlayerChoiceContext(), peer.Creature, 1, peer.Creature, null));
        var choiceRoot = CombatRootSnapshot.Capture(state, true);
        var solver = new CombatBeamSolver(choiceRoot, SolverDisplayNames.Capture(state),
            BattleDamageTracker.Observe(state), policy, searchProfile: policy.Profile);
        var choice = solver.ReplayMultiplayerForTesting([new(PlanActionKind.EndTurn, choiceRoot.StartTurnNumber)]);
        Require(choice.BoundaryReason == SearchBoundaryReason.ExternalPlayerChoice, "native peer choice boundary");
        choice.ReleaseSimulator();
        Native(PowerCmd.Remove(peer.Creature.GetPower<ToolsOfTheTradePower>()!));
        Native(PowerCmd.Apply<ToolsOfTheTradePower>(new ThrowingPlayerChoiceContext(), local.Creature, 1, local.Creature, null));
        SolverResult localChoice = Solve(CombatRootSnapshot.Capture(state, true),
            policy with { VerifyIncrementalSearch = true }, loop);
        Require(localChoice.BestNode.Actions.Any(action => action.TurnStartChoices is { Count: > 0 }),
            "local turn-start choices remain searchable");
        Native(PowerCmd.Remove(local.Creature.GetPower<ToolsOfTheTradePower>()!));

        foreach (PotionModel? held in local.PotionSlots.ToArray()) held?.Discard();
        PotionModel attack = ModelDb.Potion<AttackPotion>().ToMutable();
        Native(PotionCmd.TryToProcure(attack, local));
        var generationRoot = CombatRootSnapshot.Capture(state, true);
        solver = new CombatBeamSolver(generationRoot, SolverDisplayNames.Capture(state),
            BattleDamageTracker.Observe(state), policy, searchProfile: policy.Profile);
        var options = solver.BuildOpeningPotionActions();
        Require(options.Any(action => action.TargetCombatId == local.Creature.CombatId
            && action.Choice?.Cards.Count == 1), "generated potion choices are searched");
        var peerOption = options.First(action => action.TargetCombatId == peer.Creature.CombatId);
        var peerChoice = solver.ReplayMultiplayerForTesting([peerOption]);
        Require(peerChoice.BoundaryReason == SearchBoundaryReason.ExternalPlayerChoice,
            "potion recipient owns their choice");
        peerChoice.ReleaseSimulator();
        var localOption = options.First(action => action.TargetCombatId == local.Creature.CombatId
            && action.Choice?.Cards.Count == 1);
        var generated = solver.ReplayMultiplayerForTesting([localOption]);
        using (CardSelectCmd.PushSelector(new PlannedCardSelector(localOption.Choice!)))
            Native(attack.OnUseWrapper(new ThrowingPlayerChoiceContext(), local.Creature));
        AssertNativeState(state, generationRoot, generated.Simulator, "native generated potion choice");
        generated.ReleaseSimulator();

        foreach (CardModel card in local.PlayerCombatState!.Hand.Cards.ToArray())
            local.PlayerCombatState.Hand.RemoveInternal(card);
        foreach (var enemy in state.Enemies) enemy.SetCurrentHpInternal(10);
        PotionModel fire = ModelDb.Potion<FirePotion>().ToMutable();
        Native(PotionCmd.TryToProcure(fire, local));
        var root = CombatRootSnapshot.Capture(state, true);
        int slot = local.GetPotionSlotIndex(fire);
        SolverResult lethal = Solve(root, policy, loop);
        Require(lethal.Snapshot.AllEnemiesDead && lethal.CombatEndedTurn == root.StartTurnNumber
            && lethal.BestNode.Actions.Any(action => action.PotionId == fire.Id.Entry), "immediate potion lethal wins");
        foreach (SolverPotionDirective directive in new[] { SolverPotionDirective.Disabled, SolverPotionDirective.Force })
        {
            var directed = policy with { PotionStrategy = new(policy.PotionPolicy, [new(slot, fire.Id.Entry, directive)]) };
            SolverResult result = Solve(root, directed, loop);
            bool used = result.BestNode.Actions.Any(action => action.PotionId == fire.Id.Entry);
            Require(used == (directive == SolverPotionDirective.Force), "potion directive " + directive);
        }
    }

    private static void AssertNativeState(CombatState state, CombatRootSnapshot root,
        CombatSolver.Engine.InCombat.Simulation.CombatPredictionSimulator predicted, string label)
    {
        var expected = ContinuationStamp.CapturePredicted(root.PlayerIdentity, predicted,
            root.PlayerIdentity.PlayerCombatState!.TurnNumber, root.Forecast, root.StartTurnNumber);
        var actual = ContinuationStamp.CaptureLive(state, true);
        if (actual.StateText != expected.StateText)
            throw new InvalidOperationException(label + ": " + string.Join("; ", expected.DescribeDifferences(actual, 12)));
    }

    private static void VerifyRuntimeIsolation(CombatState state)
    {
        GameBootstrap.Harmony.Patch(HarmonyLib.AccessTools.PropertyGetter(typeof(SolverController),
                nameof(SolverController.IsMultiplayerSession)),
            prefix: new HarmonyLib.HarmonyMethod(typeof(MultiplayerContracts), nameof(MultiplayerPrefix)));
        Require(!SolverController.AutomaticCalculationEnabled && !SolverController.CanExecuteCurrentTurn,
            "multiplayer disables automatic calculation and execution");
        // Null hosts are deliberate: these entry guards must return before UI/native work.
        SolverController.RequestDeploy(null!, state);
        SolverController.SetFullAuto(null!, state, true);
        SolverController.ApplyCurrentTurn();
        SolverController.RequestSearch(null!, state, SearchReason.AutoTurnStart);
        Require(!SolverController.IsDeploying && !SolverController.IsSearching && !SolverController.FullAutoEnabled,
            "multiplayer rejects native control and automatic search");
    }

    private static bool MultiplayerPrefix(ref bool __result)
    {
        __result = true;
        return false;
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("Multiplayer contract failed: " + description);
    }

    private static bool ClockPrefix(ref ulong __result)
    {
        __result = (ulong)Environment.TickCount64;
        return false;
    }

    private static bool ReadyPrefix(CombatTurnState __0, ref bool __result)
    {
        __result = __0.PlayersReadyToEndTurn.Count == __0.State.Players.Count
            && __0.State.CurrentSide == MegaCrit.Sts2.Core.Combat.CombatSide.Player;
        return false;
    }
}
