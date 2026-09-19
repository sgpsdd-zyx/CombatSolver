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
using MegaCrit.Sts2.Core.ValueProps;

namespace OfflineSearchHarness;

internal static class MultiplayerEvaluationContracts
{
    public static void Run(CombatState state, SearchPolicySnapshot policy, HarnessOptions options, MainLoopContext loop)
    {
        var local = LocalContext.GetMe(state)!;
        var root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        policy = new MultiplayerSearchPolicy(Horizon: 3).Apply(policy);
        var solver = new CombatBeamSolver(root, SolverDisplayNames.Capture(state), BattleDamageTracker.Observe(state),
            policy, searchProfile: policy.Profile);
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
        SearchNode initial = Replay(null, null);
        SearchNode attack = Replay(initial, new(PlanActionKind.PlayCard, root.StartTurnNumber,
            CardId: "STRIKE_IRONCLAD", TargetCombatId: state.Enemies[0].CombatId));
        SearchNode shallow = Replay(attack, new(PlanActionKind.EndTurn, attack.Turn));
        SearchNode pass = Replay(initial, new(PlanActionKind.EndTurn, initial.Turn));
        SearchNode deep = Replay(pass, new(PlanActionKind.EndTurn, pass.Turn));
        SearchNode extendedAttack = Replay(shallow, new(PlanActionKind.EndTurn, shallow.Turn));
        object retention = AccessTools.Property(typeof(CombatBeamSolver), "Retention").GetValue(solver)!;
        List<SearchNode> Rank(params SearchNode[] pool) => (List<SearchNode>)AccessTools.Method(retention.GetType(), "RankFinal")
            .Invoke(retention, [pool])!;
        var ranked = Rank(deep, shallow);
        if (!ReferenceEquals(Rank(attack with { Score = 1e50 }, shallow)[0], shallow))
            throw new InvalidOperationException("An unmeasured action was treated as having no incoming damage.");
        SearchNode[] cohort = [shallow, deep, extendedAttack];
        object ordering = AccessTools.Method(typeof(CombatBeamSolver), "CreateMultiplayerOrdering")
            .Invoke(solver, [cohort])!;
        int depth = (int)AccessTools.Property(ordering.GetType(), "EnemyCycles").GetValue(ordering)!;
        var compare = (Comparison<SearchNode>)AccessTools.Property(ordering.GetType(), "Compare").GetValue(ordering)!;
        foreach (SearchNode a in cohort)
        foreach (SearchNode b in cohort)
        foreach (SearchNode c in cohort)
        {
            if (Math.Sign(compare(a, b)) != -Math.Sign(compare(b, a))
                || compare(a, b) <= 0 && compare(b, c) <= 0 && compare(a, c) > 0)
                throw new InvalidOperationException("Common-cycle ordering is not a consistent total preorder.");
        }
        int suffixComparison = compare(extendedAttack with { Score = 1e50 }, shallow);
        if (depth != 1 || suffixComparison != 0)
            throw new InvalidOperationException("Later work or an unearned estimate changed common-cycle quality.");
        File.WriteAllText(Path.Combine(options.OutputDirectory, "common-boundary.json"), JsonSerializer.Serialize(new
        {
            shallowCycles = shallow.Snapshot.AdvisoryEnemyCycles,
            deepCycles = deep.Snapshot.AdvisoryEnemyCycles,
            shallowEnemyHp = shallow.Snapshot.EnemyHp,
            deepEnemyHp = deep.Snapshot.EnemyHp,
            selected = ReferenceEquals(ranked[0], shallow) ? "shallow_attack" : "deep_pass",
            commonDepth = depth,
            suffixComparison,
            tripleComparisons = 27,
        }, new JsonSerializerOptions { WriteIndented = true }));
        foreach (SearchNode node in nodes) node.Snapshot.ReleaseSimulator();
        if (shallow.Snapshot.AdvisoryEnemyCycles != 1 || deep.Snapshot.AdvisoryEnemyCycles != 2
            || !ReferenceEquals(ranked[0], shallow))
            throw new InvalidOperationException("A deeper pass route displaced the better first-cycle attack.");
        VerifySetup(state, policy, options, loop, pass.Snapshot.CumulativePlayerHpLost);
        VerifyTactics(state, policy, options, loop, pass.Snapshot.CumulativePlayerHpLost);
        VerifyLaterRiskAndVictory(state, policy, options, loop, pass.Snapshot.CumulativePlayerHpLost);
    }

    private static void VerifySetup(CombatState state, SearchPolicySnapshot policy, HarnessOptions options,
        MainLoopContext loop, int incoming)
    {
        var local = LocalContext.GetMe(state)!;
        void Native(Task task) => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(20), "Setup fixture");
        int energy = local.PlayerCombatState!.Energy;
        int block = local.Creature.Block;
        List<CardModel> added = [];
        foreach (CardModel model in new CardModel[] { ModelDb.Card<Inflame>(), ModelDb.Card<StrikeIronclad>(), ModelDb.Card<StrikeIronclad>() })
        {
            CardModel card = state.CreateCard(model, local);
            if (card is Inflame)
            {
                card.UpgradeInternal();
                card.FinalizeUpgradeInternal();
            }
            added.Add(card);
            Native(CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, local));
        }
        Native(PlayerCmd.LoseEnergy(energy - 1, local));
        Native(CreatureCmd.GainBlock(local.Creature, incoming, ValueProp.Unpowered, null, fast: true));
        var root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        var names = SolverDisplayNames.Capture(state);
        var damage = BattleDamageTracker.Observe(state);
        List<object> evidence = [];
        foreach (int horizon in new[] { 1, 2 })
        {
            SearchPolicySnapshot candidate = new MultiplayerSearchPolicy(Horizon: horizon).Apply(policy);
            SolverResult Search()
            {
                var search = Task.Run(new CombatBeamSolver(root, names, damage, candidate,
                    progressCallback: horizon == 1 ? _ => { } : null, searchProfile: candidate.Profile).Solve);
                loop.RunUntilCompleted(search, TimeSpan.FromSeconds(30), "Realized setup search");
                return search.GetAwaiter().GetResult();
            }
            SolverResult result = horizon == 1
                ? MultiplayerFinalSelectionContracts.VerifyPreviewAndFinal(Search, options) : Search();
            string first = result.BestNode.Actions.First().CardId;
            int dealt = 500 - result.Snapshot.EnemyHp;
            evidence.Add(new { horizon, first, dealt, result.ExpandedNodes, result.AdvisoryComparisonCycles });
            File.WriteAllText(Path.Combine(options.OutputDirectory, "realized-setup.json"),
                JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
            if (first != (horizon == 1 ? "STRIKE_IRONCLAD" : "INFLAME")
                || dealt != (horizon == 1 ? 6 : 27)
                || result.ExpandedNodes > candidate.Profile.MaxExpandedNodes)
                throw new InvalidOperationException($"Setup was not valued by realized damage: h={horizon}, first={first}, damage={dealt}.");
        }
        SearchPolicySnapshot limitedPolicy = new MultiplayerSearchPolicy(Horizon: 7).Apply(policy) with
        { Profile = policy.Profile with { MaxExpandedNodes = 4 } };
        var limitedSearch = Task.Run(new CombatBeamSolver(root, names, damage, limitedPolicy,
            searchProfile: limitedPolicy.Profile).Solve);
        loop.RunUntilCompleted(limitedSearch, TimeSpan.FromSeconds(30), "Partial comparison layer");
        SolverResult limited = limitedSearch.GetAwaiter().GetResult();
        if (limited.ExpandedNodes > 4 || limited.AdvisoryComparisonCycles != 1
            || limited.BestNode.Actions.First().CardId != "STRIKE_IRONCLAD")
            throw new InvalidOperationException("Partial deeper work replaced the last completed first-cycle comparison.");
        File.WriteAllText(Path.Combine(options.OutputDirectory, "partial-cohort.json"), JsonSerializer.Serialize(new
        { limited.ExpandedNodes, limited.AdvisoryComparisonCycles, limited.Snapshot.AdvisoryEnemyCycles }));
        Native(CardPileCmd.RemoveFromCombat(added, skipVisuals: true));
        Native(PlayerCmd.GainEnergy(energy - 1, local));
        Native(CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), local.Creature, local.Creature.Block - block, null));
    }

    private static void VerifyTactics(CombatState state, SearchPolicySnapshot policy, HarnessOptions options,
        MainLoopContext loop, int incoming)
    {
        var local = LocalContext.GetMe(state)!;
        var peer = state.Players.First(player => player != local);
        void Native(Task task) => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(20), "Tactical fixture");
        int energy = local.PlayerCombatState!.Energy;
        int block = local.Creature.Block;
        int peerHp = peer.Creature.CurrentHp;
        int peerBlock = peer.Creature.Block;
        CardModel lift = state.CreateCard(ModelDb.Card<Lift>(), local);
        Native(CardPileCmd.AddGeneratedCardToCombat(lift, PileType.Hand, local));
        Native(PlayerCmd.LoseEnergy(energy - 1, local));
        Native(CreatureCmd.GainBlock(local.Creature, incoming, ValueProp.Unpowered, null, fast: true));
        int peerPadding = Math.Max(0, incoming - lift.DynamicVars.Block.IntValue);
        Native(CreatureCmd.GainBlock(peer.Creature, peerPadding, ValueProp.Unpowered, null, fast: true));
        peer.Creature.SetCurrentHpInternal(1);
        SearchPolicySnapshot candidate = new MultiplayerSearchPolicy(Horizon: 1).Apply(policy) with
        { Profile = policy.Profile with { BeamWidth = 4 } };
        var root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        var solver = new CombatBeamSolver(root, SolverDisplayNames.Capture(state), BattleDamageTracker.Observe(state),
            candidate, searchProfile: candidate.Profile);
        SimulationSnapshot pass = solver.ReplayMultiplayerForTesting([new(PlanActionKind.EndTurn, root.StartTurnNumber)]);
        var search = Task.Run(solver.Solve);
        loop.RunUntilCompleted(search, TimeSpan.FromSeconds(30), "Rescue teammate");
        SolverResult result = search.GetAwaiter().GetResult();
        bool rescue = result.BestNode.Actions.Any(action => action.CardId == "LIFT"
            && action.TargetCombatId == peer.Creature.CombatId);
        SimulationSnapshot rescued = solver.ReplayMultiplayerForTesting(result.BestNode.Actions);
        bool rescuedAtBoundary = rescued.AdvisoryLastEnemyCycle is { Cycle: 1, TeamSurvivors: 2 };
        File.WriteAllText(Path.Combine(options.OutputDirectory, "effective-rescue.json"), JsonSerializer.Serialize(new
        {
            unprotectedSurvivors = pass.TeamSurvivors,
            rescuedSurvivors = rescued.TeamSurvivors,
            rescue, result.Snapshot.CumulativePlayerHpLost,
            actions = result.BestNode.Actions,
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (pass.TeamSurvivors != 1 || result.Snapshot.CumulativePlayerHpLost != 0)
            throw new InvalidOperationException("Rescue fixture did not isolate the teammate's lethal incoming damage.");
        pass.ReleaseSimulator();
        rescued.ReleaseSimulator();
        peer.Creature.SetCurrentHpInternal(peerHp);
        Native(CardPileCmd.RemoveFromCombat([lift], skipVisuals: true));
        Native(PlayerCmd.GainEnergy(energy - 1, local));
        Native(CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), local.Creature, local.Creature.Block - block, null));
        Native(CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), peer.Creature, peer.Creature.Block - peerBlock, null));

        Native(PowerCmd.Apply<BufferPower>(new ThrowingPlayerChoiceContext(), local.Creature, 1, local.Creature, null));
        var bufferedRoot = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        var buffered = new CombatBeamSolver(bufferedRoot, SolverDisplayNames.Capture(state), BattleDamageTracker.Observe(state),
            candidate, searchProfile: candidate.Profile);
        SimulationSnapshot before = buffered.ReplayMultiplayerForTesting([]);
        SimulationSnapshot defend = buffered.ReplayMultiplayerForTesting([new(PlanActionKind.PlayCard,
            bufferedRoot.StartTurnNumber, CardId: "DEFEND_IRONCLAD", TargetCombatId: local.Creature.CombatId)]);
        SimulationSnapshot noBlock = buffered.ReplayMultiplayerForTesting([new(PlanActionKind.EndTurn, bufferedRoot.StartTurnNumber)]);
        File.WriteAllText(Path.Combine(options.OutputDirectory, "ineffective-block.json"), JsonSerializer.Serialize(new
        { before = before.Score, defend = defend.Score, noBlock.CumulativePlayerHpLost }));
        if (noBlock.CumulativePlayerHpLost != 0)
            throw new InvalidOperationException("Buffer fixture did not prevent the opening damage.");
        bool ineffectiveBlockReward = defend.Score > before.Score;
        foreach (SimulationSnapshot snapshot in new[] { before, defend, noBlock }) snapshot.ReleaseSimulator();
        Native(PowerCmd.Remove(local.Creature.GetPower<BufferPower>()!));
        if (!rescue || !rescuedAtBoundary || ineffectiveBlockReward)
            throw new InvalidOperationException($"Tactical evaluation failed: rescue={rescue}, ineffective_block_reward={ineffectiveBlockReward}.");
    }

    private static void VerifyLaterRiskAndVictory(CombatState state, SearchPolicySnapshot policy, HarnessOptions options,
        MainLoopContext loop, int incoming)
    {
        var local = LocalContext.GetMe(state)!;
        void Native(Task task) => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(20), "Known risk setup");
        int hp = local.Creature.CurrentHp, block = local.Creature.Block, enemyHp = state.Enemies[0].CurrentHp;
        CardModel[] blood = Enumerable.Range(0, 2).Select(_ => state.CreateCard(ModelDb.Card<Bloodletting>(), local)).ToArray();
        foreach (CardModel card in blood) Native(CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, local));
        Native(CreatureCmd.GainBlock(local.Creature, incoming, ValueProp.Unpowered, null, fast: true));
        List<object> evidence = [];
        foreach (int health in new[] { 2, 40 })
        {
            local.Creature.SetCurrentHpInternal(health);
            var root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
            var solver = new CombatBeamSolver(root, SolverDisplayNames.Capture(state), BattleDamageTracker.Observe(state),
                policy, searchProfile: policy.Profile);
            List<SearchNode> nodes = [];
            SearchNode Next(SearchNode? parent, PlanAction? action)
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
            SearchNode initial = Next(null, null);
            SearchNode attack = Next(initial, new(PlanActionKind.PlayCard, initial.Turn,
                CardId: "STRIKE_IRONCLAD", TargetCombatId: state.Enemies[0].CombatId));
            SearchNode safe = Next(attack, new(PlanActionKind.EndTurn, attack.Turn));
            SearchNode risky = Next(safe, new(PlanActionKind.PlayCard, safe.Turn,
                CardId: "BLOODLETTING", TargetCombatId: local.Creature.CombatId));
            if (health == 40) risky = Next(risky, new(PlanActionKind.PlayCard, risky.Turn,
                CardId: "BLOODLETTING", TargetCombatId: local.Creature.CombatId));
            object ordering = AccessTools.Method(typeof(CombatBeamSolver), "CreateMultiplayerOrdering")
                .Invoke(solver, [new[] { safe, risky }])!;
            var compare = (Comparison<SearchNode>)AccessTools.Property(ordering.GetType(), "Compare").GetValue(ordering)!;
            bool detected = health == 2 ? risky.Snapshot.PlayerDead : risky.AdvisoryHpLoss.ExcessHpLost(3) == 3;
            if (!detected || compare(safe, risky with { Score = 1e50 }) >= 0)
                throw new InvalidOperationException("Known later death or excess HP loss was hidden by an earlier checkpoint.");
            evidence.Add(new { health, risky.Snapshot.PlayerDead, excess = risky.AdvisoryHpLoss.ExcessHpLost(3), sameFirstAction = true });
            foreach (SearchNode node in nodes) node.Snapshot.ReleaseSimulator();
        }
        local.Creature.SetCurrentHpInternal(hp);
        state.Enemies[0].SetCurrentHpInternal(6);
        var lethalRoot = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        var lethalSolver = new CombatBeamSolver(lethalRoot, SolverDisplayNames.Capture(state), BattleDamageTracker.Observe(state),
            policy, searchProfile: policy.Profile);
        SimulationSnapshot victory = lethalSolver.ReplayMultiplayerForTesting([new(PlanActionKind.PlayCard,
            lethalRoot.StartTurnNumber, CardId: "STRIKE_IRONCLAD", TargetCombatId: state.Enemies[0].CombatId)]);
        if (!victory.AllEnemiesDead || victory.AdvisoryEnemyCycles != 0 || victory.AdvisoryLastEnemyCycle != null)
            throw new InvalidOperationException("Immediate native victory gained fictitious enemy-cycle observations.");
        evidence.Add(new { immediateVictory = true, victory.AdvisoryEnemyCycles });
        victory.ReleaseSimulator();
        state.Enemies[0].SetCurrentHpInternal(enemyHp);
        Native(CardPileCmd.RemoveFromCombat(blood, skipVisuals: true));
        Native(CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), local.Creature, local.Creature.Block - block, null));
        File.WriteAllText(Path.Combine(options.OutputDirectory, "known-risk-and-victory.json"),
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
    }
}
