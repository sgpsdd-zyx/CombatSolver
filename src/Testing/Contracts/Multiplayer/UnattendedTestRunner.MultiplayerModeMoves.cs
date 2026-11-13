using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private async Task AssertMultiplayerModeMovesAsync(CombatState combat)
    {
        Player local = LocalContext.GetMe(combat) ?? throw new InvalidOperationException("Missing local player.");
        if (combat.Players.Count != 2) throw new InvalidOperationException("Expected two players.");
        List<object> results = [];
        foreach (var (monsterId, moveId) in new[]
        {
            ("SoulFysh", "BECKON_MOVE"),
            ("Aeonglass", "INCREASING_INTENSITY_MOVE"),
            ("LagavulinMatriarch", "SOUL_SIPHON_MOVE"),
        })
        {
            await EnsureMonsterExistsAsync(combat, monsterId, null);
            foreach (Player player in combat.Players)
            {
                foreach (RelicModel relic in player.Relics.ToArray()) await RelicCmd.Remove(relic);
                await ClearPlayerPilesAsync(player);
                await InjectCardAsync(combat, player, new() { CardId = "DEFEND_IRONCLAD", Pile = "Hand" });
                await InjectCardAsync(combat, player, new() { CardId = "STRIKE_IRONCLAD", Pile = "Draw", Count = 2 });
                await InjectCardAsync(combat, player, new() { CardId = "WITHER", Pile = "Discard" });
            }
            foreach (PowerModel power in combat.Creatures.SelectMany(creature => creature.Powers).ToArray())
                await PowerCmd.Remove(power);
            var check = new UnattendedMonsterMoveCheck { MonsterId = monsterId, MoveId = moveId };
            var enemy = ResolveMonsterMoveTarget(combat, check);
            MonsterModel monster = ConfigureMonsterMove(enemy, check);
            CombatRootSnapshot root = CombatRootSnapshot.Capture(combat, multiplayerAdvisor: true);
            var predicted = root.ForkSimulator();
            var shadow = (SimulatedCombatState)predicted.State.CombatState;
            var move = shadow.CurrentMonsterMoves().Single(value => value.Owner == enemy);
            _ = MonsterMoveSemantics.ApplyForecastMove(predicted, shadow, move, local.Creature, new HashSet<uint>());
            var expected = ContinuationStamp.CapturePredicted(local, predicted, root.StartTurnNumber,
                root.Forecast, root.StartTurnNumber);
            await monster.PerformMove();
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
            var actual = ContinuationStamp.CaptureLive(combat, multiplayerAdvisor: true);
            bool equal = expected.StateText == actual.StateText;
            bool rootFrozen = ContinuationStamp.CapturePredicted(local, root.ForkSimulator(), root.StartTurnNumber,
                root.Forecast, root.StartTurnNumber).StateText == root.ContinuationStamp.StateText;
            bool forkEqual = ContinuationStamp.CapturePredicted(local, predicted.Fork(), root.StartTurnNumber,
                root.Forecast, root.StartTurnNumber).StateText == expected.StateText;
            results.Add(new { monsterId, moveId, equal, rootFrozen, forkEqual,
                differences = expected.DescribeDifferences(actual, maximumDifferences: 16) });
            _writer.WriteGeneratedArtifact("multiplayer-mode-moves.json", new { mode = _request.GameMode, results });
            if (!equal || !rootFrozen || !forkEqual)
                throw new InvalidOperationException($"Multiplayer {monsterId}.{moveId}: "
                    + string.Join("; ", expected.DescribeDifferences(actual, maximumDifferences: 16)));
            _completedChecks.Add($"MultiplayerModeMove:{_request.GameMode}:{monsterId}:FullParty:Fork:Root");
        }
    }
}
