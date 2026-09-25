using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Runs;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private sealed partial class MultiplayerExperimentDriver
    {
        private bool TryResolve(PlanAction action, out CardModel? card, out Creature? target)
        {
            card = null;
            target = action.TargetCombatId is { } id ? _combat.GetCreature(id) : null;
            if (action.Kind == PlanActionKind.EndTurn) return true;
            if (action.Kind != PlanActionKind.PlayCard)
                throw new InvalidDataException("The registered potion-free pilot received a non-card action.");
            if (action.TargetCombatId != null && target == null) return false;
            var hand = _local.PlayerCombatState!.Hand.Cards.ToList();
            bool found = string.IsNullOrEmpty(action.CardStateKey)
                ? hand.Count(candidate => candidate.Id.Entry == action.CardId) > action.CardOccurrence
                : hand.Count(candidate => CardChoiceSupport.ChoiceCardKey(candidate) == action.CardStateKey)
                    > action.CardStateOccurrence;
            if (!found) return false;
            card = SolverController.FindCardForDeployment(hand, action);
            return card.CanPlayTargeting(target);
        }

        private (PlanAction Action, CardModel? Card, Creature? Target) ChoosePeerAction()
        {
            // This policy sees only its own hand/resources and public intent/HP. No solver calls or plans.
            CardModel[] hand = _peer.PlayerCombatState!.Hand.Cards.ToArray();
            foreach (CardModel card in hand)
                if (!_spec.PeerAllowedCardIds.Contains(card.Id.Entry, StringComparer.Ordinal)
                    && (card.Type is not (CardType.Status or CardType.Curse)
                        || card.CanPlayTargeting(null)
                        || _combat.Enemies.Any(card.CanPlayTargeting)))
                    throw new MultiplayerExperimentBoundaryException("choice_adapter_missing",
                        "Unregistered peer card " + card.Id.Entry);
            int incoming = _combat.Enemies.Where(enemy => enemy.IsAlive).Sum(enemy =>
                enemy.Monster!.NextMove.Intents.OfType<AttackIntent>().Sum(intent =>
                    Math.Max(0, intent.GetSingleDamage([_peer.Creature], enemy)) * Math.Max(1, intent.Repeats)));
            bool defend = _spec.Options.PartnerPolicy == "survival_first" && incoming > _peer.Creature.Block;
            var options = hand.Where(card => _spec.PeerAllowedCardIds.Contains(card.Id.Entry, StringComparer.Ordinal))
                .SelectMany(card => (card.TargetType == TargetType.AnyEnemy
                    ? _combat.Enemies.Where(enemy => enemy.IsAlive).Cast<Creature?>()
                    : new Creature?[] { null }).Where(card.CanPlayTargeting).Select(target =>
                    (Card: card, Target: target, Priority: PeerPriority(card, target, defend))))
                .OrderBy(option => option.Priority)
                .ThenBy(option => option.Target?.CurrentHp ?? 0)
                .ThenBy(option => option.Card.Id.Entry, StringComparer.Ordinal)
                .ThenBy(option => Array.IndexOf(hand, option.Card)).ToArray();
            if (options.Length == 0)
                return (new(PlanActionKind.EndTurn, _peer.PlayerCombatState.TurnNumber), null, null);
            var selected = options[0];
            string key = CardChoiceSupport.ChoiceCardKey(selected.Card);
            int occurrence = hand.Take(Array.IndexOf(hand, selected.Card))
                .Count(card => CardChoiceSupport.ChoiceCardKey(card) == key);
            return (new(PlanActionKind.PlayCard, _peer.PlayerCombatState.TurnNumber,
                CardId: selected.Card.Id.Entry, TargetCombatId: selected.Target?.CombatId,
                CardStateKey: key, CardStateOccurrence: occurrence), selected.Card, selected.Target);
        }

        private static int PeerPriority(CardModel card, Creature? target, bool defend)
        {
            // Listed base damage is an explicit simple-policy estimate, not a hidden branch simulation.
            int baseDamage = card.Id.Entry switch
            {
                "STRIKE_IRONCLAD" or "STRIKE_SILENT" or "POISONED_STAB" => 6,
                "BASH" => 8,
                _ => 0,
            };
            if (target != null && baseDamage >= target.CurrentHp + target.Block) return 0;
            if (defend && card.Id.Entry.StartsWith("DEFEND_", StringComparison.Ordinal)) return 1;
            return card.Id.Entry switch
            {
                "BASH" => 2,
                "POISONED_STAB" => 3,
                "STRIKE_IRONCLAD" or "STRIKE_SILENT" => 4,
                "DEADLY_POISON" => 5,
                "INFLAME" => 6,
                "DEFEND_IRONCLAD" or "DEFEND_SILENT" => 7,
                _ => throw new InvalidDataException("Unregistered peer priority."),
            };
        }

        private async Task Execute(Player actor, PlanAction planned, CardModel? card, Creature? target)
        {
            if (!CanAct(actor)) throw new InvalidOperationException("Scheduled actor is not eligible.");
            CombatRootSnapshot? contractRoot = null;
            ContinuationStamp? predicted = null;
            if (_spec.Options.Mode == "Contract" && planned.IsExecutable)
            {
                contractRoot = CombatRootSnapshot.Capture(_combat, multiplayerAdvisor: true);
                if (contractRoot.HasOnlyPostCombatHealing)
                    throw new InvalidOperationException("Multiplayer root used a single-player healing bound.");
                if (actor == _local && _strictActionChecks == 0)
                {
                    var policy = _runner._protocolHost.LastExperimentPolicy
                        ?? throw new InvalidOperationException("Action contract needs a completed manual request.");
                    var solver = new CombatBeamSolver(contractRoot, SolverDisplayNames.Capture(_combat),
                        BattleDamageTracker.Observe(_combat), policy, searchProfile: policy.Profile);
                    var snapshot = solver.ReplayMultiplayerForTesting([planned]);
                    try
                    {
                        if (snapshot.FutureHealPotential != int.MaxValue)
                            throw new InvalidOperationException("Multiplayer snapshot restricted future healing.");
                        if (!snapshot.AllEnemiesDead)
                            predicted = ContinuationStamp.CapturePredicted(_local, snapshot.Simulator, snapshot.Turn,
                                contractRoot.Forecast, contractRoot.StartTurnNumber);
                    }
                    finally { snapshot.ReleaseSimulator(); }
                }
            }
            _decisions++;
            Record("action_before", new { actor = actor.NetId, action = ActionView(planned),
                instance = card == null ? null : (object)NetCombatCard.FromModel(card) });
            using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(12));
            if (planned.Kind == PlanActionKind.EndTurn)
            {
                if (actor == _local) CombatManager.Instance.OnEndedTurnLocally();
                GameAction end = new EndPlayerTurnAction(actor, actor.PlayerCombatState!.TurnNumber);
                await Enqueue(end, deadline.Token);
            }
            else
            {
                if (card == null || card.Owner != actor || !card.CanPlayTargeting(target))
                    throw new InvalidOperationException("Native action lost its registered actor or target.");
                using NativeChoiceSession choices = NativeChoiceRuntime.Begin(_combat, actor,
                    $"multiplayer_experiment:{actor.NetId}:{_decisions}");
                choices.SetPlanAndStartDriving(_runner._host, planned.GetActionChoicesInExecutionOrder(), deadline.Token);
                GameAction played = await SolverController.EnqueueAndCaptureActionAsync(
                    candidate => candidate is PlayCardAction action && action.Player == actor
                        && ReferenceEquals(action.NetCombatCard.ToCardModelOrNull(), card),
                    () =>
                    {
                        if (!card.TryManualPlay(target)) throw new InvalidOperationException("Native card enqueue failed.");
                    }, deadline.Token);
                if (played.OwnerId != actor.NetId) throw new InvalidOperationException("Native queue actor mismatch.");
                await choices.AwaitProducerAndCompleteAsync(played.CompletionTask).WaitAsync(deadline.Token);
            }
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions().WaitAsync(deadline.Token);
            if (predicted != null)
            {
                var actual = ContinuationStamp.CaptureLive(_combat, multiplayerAdvisor: true);
                _runner._writer.WriteGeneratedArtifact("multiplayer-native-action-diff.json", new
                {
                    expected = predicted.StateText, actual = actual.StateText,
                    differences = predicted.DescribeDifferences(actual),
                });
                if (actual.StateText != predicted.StateText)
                    throw new InvalidOperationException("Native action differs: " + predicted.DescribeFirstDifference(actual));
                _strictActionChecks++;
            }
            if (contractRoot != null)
            {
                string retained = ContinuationStamp.CapturePredicted(_local, contractRoot.ForkSimulator(),
                    contractRoot.StartTurnNumber, contractRoot.Forecast, contractRoot.StartTurnNumber).StateText;
                if (retained != contractRoot.ContinuationStamp.StateText)
                    throw new InvalidOperationException("Native actor mutated a retained prediction root.");
                _frozenRootChecks++;
            }
            Record("action_after", new { actor = actor.NetId, action = ActionView(planned) });
        }

        private static async Task Enqueue(GameAction action, CancellationToken token)
        {
            GameAction captured = await SolverController.EnqueueAndCaptureActionAsync(
                candidate => ReferenceEquals(candidate, action),
                () => RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action), token);
            await captured.CompletionTask.WaitAsync(token);
        }
    }
}
