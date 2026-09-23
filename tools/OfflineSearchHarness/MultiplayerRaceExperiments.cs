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
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;

namespace OfflineSearchHarness;

internal static class MultiplayerRaceExperiments
{
    private sealed record Fixture(string Name, int Hp, int PeerHp, int EnemyHp, int Energy,
        string[] Cards, int EnemyRitual = 0, bool Protected = false, bool SecondEnemy = false);
    private sealed record Outcome(int Cycles, int LocalHp, int LocalLoss, int PeerHp, int EnemyHp,
        bool Won, string Boundary, int Actions);
    private static Player? _externalPeer;
    private static string? _externalMode;

    public static string Run(CombatState state, HarnessOptions options, MainLoopContext loop)
    {
        if (options.MaxDegreeOfParallelism != 1 || options.BudgetMilliseconds > 5000)
            throw new ArgumentException("Race experiments require DOP 1 and a budget at most 5000 ms.");
        GameBootstrap.Harmony.Patch(AccessTools.Method(typeof(Godot.Time), nameof(Godot.Time.GetTicksMsec)),
            prefix: new HarmonyMethod(typeof(MultiplayerReviewContracts), "ClockPrefix"));
        var end = AccessTools.Method(typeof(CombatBeamSolver), "EndMultiplayerPlayerTurn");
        var peerPrefix = AccessTools.Method(typeof(MultiplayerRaceExperiments), nameof(PlayExternalPeer));
        GameBootstrap.Harmony.Patch(end, prefix: new HarmonyMethod(peerPrefix));
        Player local = LocalContext.GetMe(state)!, peer = state.Players.Single(player => player != local);
        void Native(Task task) => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(15), "Race fixture setup");
        bool holdout = options.Scenario.MultiplayerReviewStage == "race-holdout";
        Fixture[] fixtures = holdout
            ? [new("poison_holdout", 55, 55, 110, 1, ["POISONED_STAB", "DEADLY_POISON", "DEFEND_IRONCLAD"]),
               new("fumes_holdout", 65, 65, 160, 1, ["STRIKE_IRONCLAD", "NOXIOUS_FUMES", "DEFEND_IRONCLAD"]),
               new("peer_guard_holdout", 70, 11, 150, 2, ["STRIKE_IRONCLAD", "RALLY", "DEFEND_IRONCLAD"], 1),
               new("protect_holdout", 45, 2, 100, 1, ["STRIKE_IRONCLAD", "INTERCEPT", "DEFEND_IRONCLAD"]),
               new("burst_holdout", 65, 65, 180, 3, ["BLUDGEON", "INFLAME", "STRIKE_IRONCLAD", "DEFEND_IRONCLAD"]),
               new("threat_holdout", 75, 75, 35, 1, ["STRIKE_IRONCLAD", "POISONED_STAB", "DEFEND_IRONCLAD"], 2, SecondEnemy: true)]
            : [new("poison", 80, 80, 120, 1, ["STRIKE_IRONCLAD", "DEADLY_POISON", "DEFEND_IRONCLAD"]),
               new("fumes", 80, 80, 160, 1, ["STRIKE_IRONCLAD", "NOXIOUS_FUMES", "DEFEND_IRONCLAD"]),
               new("mixed_poison", 70, 70, 90, 2, ["POISONED_STAB", "DEADLY_POISON", "DEFEND_IRONCLAD"]),
               new("peer_guard", 80, 9, 160, 2, ["STRIKE_IRONCLAD", "RALLY", "DEFEND_IRONCLAD"], 2),
               new("immediate_rescue", 50, 2, 120, 1, ["STRIKE_IRONCLAD", "INTERCEPT", "DEFEND_IRONCLAD"]),
               new("lift_rescue", 50, 2, 120, 2, ["STRIKE_IRONCLAD", "LIFT", "DEFEND_IRONCLAD"]),
               new("defense", 6, 80, 30, 1, ["STRIKE_IRONCLAD", "DEFEND_IRONCLAD"], Protected: true),
               new("investment", 80, 80, 160, 2, ["STRIKE_IRONCLAD", "DEFEND_IRONCLAD", "INFLAME", "BASH"], 2, true),
               new("burst", 80, 80, 220, 3, ["BLUDGEON", "INFLAME", "STRIKE_IRONCLAD", "DEFEND_IRONCLAD"]),
               new("threat", 80, 80, 24, 1, ["STRIKE_IRONCLAD", "POISONED_STAB", "DEFEND_IRONCLAD"], 3, SecondEnemy: true)];
        string? only = Environment.GetEnvironmentVariable("RACE_FIXTURE");
        if (only != null && !fixtures.Any(fixture => fixture.Name == only))
            throw new ArgumentException("Unknown race fixture: " + only);
        string? variant = Environment.GetEnvironmentVariable("RACE_VARIANT");
        int[] variants = variant == null ? [0, 1] : [int.Parse(variant)];
        List<object> rows = [];
        try
        {
            foreach (Fixture fixture in fixtures.Where(fixture => only == null || fixture.Name == only))
            {
                foreach (Player player in state.Players)
                {
                    foreach (var potion in player.PotionSlots.ToArray()) potion?.Discard();
                    foreach (var relic in player.Relics.ToArray()) Native(RelicCmd.Remove(relic));
                    foreach (var power in player.Creature.Powers.ToArray()) Native(PowerCmd.Remove(power));
                    Native(CardPileCmd.RemoveFromCombat(player.PlayerCombatState!.AllCards.ToArray(), skipVisuals: true));
                    player.Creature.SetMaxHpInternal(80);
                    player.Creature.SetCurrentHpInternal(player == local ? fixture.Hp : fixture.PeerHp);
                }
                foreach (Creature enemy in state.Enemies)
                    foreach (var power in enemy.Powers.ToArray()) Native(PowerCmd.Remove(power));
                if (fixture.SecondEnemy && state.Enemies.Count == 1)
                    Native(CreatureCmd.Add<FuzzyWurmCrawler>(state));
                for (int index = 0; index < state.Enemies.Count; index++)
                {
                    var enemy = state.Enemies[index];
                    enemy.SetMaxHpInternal(index == 0 ? fixture.EnemyHp : fixture.EnemyHp * 4);
                    enemy.SetCurrentHpInternal(enemy.MaxHp);
                }
                Native(PlayerCmd.LoseEnergy(local.PlayerCombatState!.Energy - fixture.Energy, local));
                if (fixture.Protected)
                    Native(PowerCmd.Apply<BufferPower>(new ThrowingPlayerChoiceContext(), peer.Creature, 40, peer.Creature, null));
                if (fixture.EnemyRitual > 0)
                    Native(PowerCmd.Apply<RitualPower>(new ThrowingPlayerChoiceContext(), state.Enemies[0],
                        fixture.EnemyRitual, state.Enemies[0], null));
                if (Environment.GetEnvironmentVariable("RACE_ENEMY_HP") is { } enemyHp)
                {
                    state.Enemies[0].SetMaxHpInternal(int.Parse(enemyHp));
                    state.Enemies[0].SetCurrentHpInternal(int.Parse(enemyHp));
                }
                if (Environment.GetEnvironmentVariable("RACE_LOCAL_HP") is { } localHp)
                    local.Creature.SetCurrentHpInternal(int.Parse(localHp));
                if (fixture.Name == "mixed_poison")
                    Native(PowerCmd.Apply<PoisonPower>(new ThrowingPlayerChoiceContext(), state.Enemies[0], 3, peer.Creature, null));
                foreach (string cardId in fixture.Cards) AddCard(local, cardId, Native, state);
                AddCard(peer, "DEFEND_IRONCLAD", Native, state);
                AddCard(peer, "STRIKE_IRONCLAD", Native, state);
                Native(RunManager.Instance.ActionExecutor.FinishedExecutingActions());
                var root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
                var names = SolverDisplayNames.Capture(state);
                var damage = BattleDamageTracker.Observe(state);
                var template = SolverController.CaptureSearchPolicy(SolverSettings.Capture(), state, false, null) with
                {
                    FixedBudget = true, BudgetOverrideMilliseconds = options.BudgetMilliseconds,
                    Profile = ModRuntime.ResolveProfile(options), MaxDegreeOfParallelism = 1,
                    PotionPolicy = SolverPotionPolicy.Disabled, PotionStrategy = new(SolverPotionPolicy.Disabled, []),
                    VerifyIncrementalSearch = options.VerifyIncremental,
                };
                List<object> reference = [];
                if (fixture.Name.Contains("rescue") || fixture.Name.Contains("guard") || fixture.Name.Contains("protect"))
                {
                    var driver = new CombatBeamSolver(root, names, damage, new MultiplayerSearchPolicy().Apply(template),
                        searchProfile: template.Profile);
                    foreach (string id in fixture.Cards)
                    {
                        var model = ModelDb.AllCards.Single(card => card.Id.Entry == id);
                        var action = new PlanAction(PlanActionKind.PlayCard, root.StartTurnNumber, CardId: id,
                            TargetCombatId: model.TargetType is TargetType.AnyAlly or TargetType.AnyPlayer ? peer.Creature.CombatId
                                : model.TargetType == TargetType.AnyEnemy ? state.Enemies[0].CombatId : null);
                        var snapshot = driver.ReplayMultiplayerForTesting([action, new(PlanActionKind.EndTurn, root.StartTurnNumber)]);
                        try { reference.Add(new { id, snapshot.PlayerHp, snapshot.TeamSurvivors,
                            peerHp = snapshot.Simulator.State.GetCreature(peer.Creature).CurrentHp,
                            boundary = snapshot.BoundaryReason.ToString(), snapshot.PredictionGaps }); }
                        finally { snapshot.ReleaseSimulator(); }
                    }
                }
                foreach (var features in variants)
                {
                    var multiplayer = new MultiplayerSearchPolicy();
                    if (features is not (0 or 1)) throw new ArgumentException("Only baseline 0 and shared-damage 1 remain supported.");
                    var evaluation = typeof(MultiplayerSearchPolicy).GetProperty("CreditSharedDamage");
                    if (evaluation != null) evaluation.SetValue(multiplayer, features == 1);
                    else if (features != 0) throw new InvalidOperationException("This DLL has no experimental evaluation policy.");
                    var policy = multiplayer.Apply(template);
                    var search = Task.Run(() => CombatSearchCoordinator.Solve(root, names, damage, policy, CancellationToken.None, null));
                    loop.RunUntilCompleted(search, TimeSpan.FromSeconds(20), "Race fixed-budget search");
                    var result = search.GetAwaiter().GetResult();
                    PlanAction[] current = result.BestNode.Actions.TakeWhile(action => action.Turn == root.StartTurnNumber)
                        .Where(action => action.Kind != PlanActionKind.EndTurn).ToArray();
                    var outcomes = new Dictionary<string, Outcome>();
                    foreach (string peerMode in new[] { "idle", "guard", "attack" })
                        outcomes[peerMode] = Evaluate(root, names, damage, template, peer, current, peerMode);
                    rows.Add(new { fixture = fixture.Name, features = (int)features, result.ExpandedNodes,
                        result.TransitionCount, result.Elapsed, result.AdvisoryComparisonCycles,
                        result.AdvisorySearchedEnemyCycles, result.AdvisoryPlannedContribution,
                        result.AdvisoryObjectiveWitness, result.Snapshot.PlayerHp, result.Snapshot.EnemyHp,
                        rootEnemyHp = root.MultiplayerObservation!.EnemyHp, root.InitialPlayerHp,
                        firstTurn = current, outcomes, reference });
                    File.WriteAllText(Path.Combine(options.OutputDirectory, "race-experiments.json"),
                        JsonSerializer.Serialize(new { holdout, fixedPeerScripts = true, humanPolicyValidated = false, rows },
                            UnattendedTestFiles.JsonOptions));
                    Console.WriteLine($"[race] {fixture.Name} v={(int)features} first={string.Join(',', current.Select(a => a.CardId))} "
                        + $"hp={outcomes["guard"].LocalHp} enemy={outcomes["guard"].EnemyHp} peer={outcomes["guard"].PeerHp}");
                    if (result.ExpandedNodes > policy.Profile.MaxExpandedNodes
                        || root.ContinuationStamp.StateText != ContinuationStamp.CaptureLive(state, multiplayerAdvisor: true).StateText)
                        throw new InvalidOperationException("Race experiment exceeded its budget or changed the live root.");
                }
            }
        }
        finally { _externalPeer = null; _externalMode = null; GameBootstrap.Harmony.Unpatch(end, peerPrefix); }
        return $"race_runs={rows.Count}";
    }

    private static void AddCard(Player player, string id, Action<Task> native, CombatState state)
        => native(CardPileCmd.AddGeneratedCardToCombat(state.CreateCard(ModelDb.AllCards.Single(card => card.Id.Entry == id), player),
            PileType.Hand, player));

    private static Outcome Evaluate(CombatRootSnapshot root, SolverDisplayNames names, BattleDamageSnapshot damage,
        SearchPolicySnapshot template, Player peer, IReadOnlyList<PlanAction> current, string mode)
    {
        _externalPeer = peer;
        _externalMode = mode;
        try
        {
            var solver = new CombatBeamSolver(root, names, damage, new MultiplayerSearchPolicy(Horizon: 5).Apply(template),
                searchProfile: template.Profile);
            List<PlanAction> actions = [.. current, new(PlanActionKind.EndTurn, root.StartTurnNumber)];
            for (int step = 0; step < 48; step++)
            {
                var snapshot = solver.ReplayMultiplayerForTesting(actions);
                try
                {
                    var simulator = snapshot.Simulator;
                    var combat = (SimulatedCombatState)simulator.State.CombatState;
                    if (snapshot.PlayerDead || snapshot.AllEnemiesDead || snapshot.AdvisoryEnemyCycles >= 5
                        || snapshot.BoundaryReason != SearchBoundaryReason.None)
                        return new(snapshot.AdvisoryEnemyCycles, snapshot.PlayerHp, snapshot.CumulativePlayerHpLost,
                            simulator.State.GetCreature(peer.Creature).CurrentHp, snapshot.EnemyHp, snapshot.AllEnemiesDead,
                            snapshot.BoundaryReason.ToString(), actions.Count);
                    var hand = simulator.State.GetPlayerCombatState(root.PlayerIdentity).Hand.Cards;
                    var target = combat.Enemies.FirstOrDefault(enemy => simulator.State.GetCreature(enemy).IsAlive);
                    // The continuation is a fixed evaluator, independent of all candidate scores.
                    var card = hand.Where(card => combat.CanPlayCard(simulator, card))
                        .Where(card => card.Preview.TargetType is not TargetType.AnyAlly and not TargetType.AnyPlayer)
                        .OrderBy(card => card.Preview is DefendIronclad ? 0 : card.Preview is Inflame or NoxiousFumes ? 1 : 2)
                        .ThenBy(card => card.Preview.Id.Entry, StringComparer.Ordinal).FirstOrDefault();
                    actions.Add(card == null ? new(PlanActionKind.EndTurn, snapshot.Turn)
                        : new(PlanActionKind.PlayCard, snapshot.Turn, CardId: card.Preview.Id.Entry,
                            TargetCombatId: card.Preview.TargetType == TargetType.AnyEnemy ? target?.CombatId : null));
                }
                finally { snapshot.ReleaseSimulator(); }
            }
            throw new InvalidOperationException("Race evaluator exhausted its fixed action limit.");
        }
        finally { _externalPeer = null; _externalMode = null; }
    }

    private static bool PlayExternalPeer(CombatPredictionSimulator __0, SimulatedCombatState __1,
        ISet<uint> __2, ref bool __3, ref bool __result)
    {
        if (_externalPeer == null || _externalMode == "idle" || !__0.State.GetCreature(_externalPeer.Creature).IsAlive)
            return true;
        var hand = __0.State.GetPlayerCombatState(_externalPeer).Hand.Cards;
        var card = hand.FirstOrDefault(card => (_externalMode == "guard" ? card.Preview is DefendIronclad
            : card.Preview is StrikeIronclad) && __1.CanPlayCard(__0, card));
        var target = __1.Enemies.FirstOrDefault(enemy => __0.State.GetCreature(enemy).IsAlive);
        if (card == null || target == null) return true;
        using (__1.BeginCardExecutionScope(__2))
            if (!__0.ManualPlay(card, card.Preview is StrikeIronclad ? target : null, out _))
                throw new InvalidOperationException("External peer script unexpectedly requested a choice.");
        if (!CorePowerSupport.ApplyEnemyDeathPowers(__0, __1, __1.KnownEnemies, __2)
            || !CombatBeamSolver.SettleReplayActionBoundary(__0, __1))
            throw new InvalidOperationException("External peer script failed to settle.");
        if (__0.IsInProgress) return true;
        __3 = false;
        __result = true;
        return false;
    }
}
