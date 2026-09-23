using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private sealed partial class MultiplayerExperimentDriver
    {
        private static object ActionView(PlanAction action) => new
        {
            kind = action.Kind.ToString(), action.Turn, action.CardId, action.CardOccurrence,
            action.CardStateKey, action.CardStateOccurrence, action.TargetCombatId,
            action.PotionId, action.PotionSlot,
            choices = action.GetActionChoicesInExecutionOrder(), action.TurnStartChoices,
        };

        private static JsonObject PolicyView(SearchPolicySnapshot policy)
        {
            var result = new JsonObject();
            HashSet<string> runtimeServices = [nameof(policy.Diagnostics), nameof(policy.FramePressureSignal),
                nameof(policy.MemoryPressureSignal), nameof(policy.Interaction), nameof(policy.RequestWorkTotals),
                nameof(policy.PortfolioTelemetry)];
            foreach (PropertyInfo property in typeof(SearchPolicySnapshot).GetProperties(
                BindingFlags.Instance | BindingFlags.Public))
                if (!runtimeServices.Contains(property.Name))
                    result[property.Name] = JsonSerializer.SerializeToNode(property.GetValue(policy), UnattendedTestFiles.JsonOptions);
            return result;
        }

        private void Record(string kind, object? detail = null)
        {
            _events.Add(new { sequence = ++_sequence, kind, detail, round = _combat.RoundNumber,
                side = _combat.CurrentSide.ToString(),
                actors = _combat.Players.Select(player => new
                {
                    player.NetId, turn = player.PlayerCombatState?.TurnNumber,
                    phase = player.PlayerCombatState?.Phase.ToString(),
                    ready = CombatManager.Instance.IsPlayerReadyToEndTurn(player),
                    hp = player.Creature.CurrentHp, block = player.Creature.Block,
                    energy = player.PlayerCombatState?.Energy,
                }).ToArray(),
                state = CombatManager.Instance.IsInProgress
                    ? ContinuationStamp.CaptureLive(_combat, multiplayerAdvisor: true).StateText : null,
            });
            // Retain the last completed boundary even if the game process stops responding.
            Save();
        }

        private void Save()
        {
            _runner._writer.WriteGeneratedArtifact("multiplayer-experiment.json", new
            {
                schemaVersion = 1, spec = _spec, outcome = _outcome, nativeEnded = _nativeEnded, nativeWon = _nativeWon,
                round = _combat.RoundNumber, queries = _queries, events = _events,
                health = _health.Select(pair => pair.Value.Snapshot()).ToArray(),
                invalidAdvice = _invalidAdvice, localActions = _localActions, peerActions = _peerActions, decisions = _decisions,
                strictActionChecks = _strictActionChecks, frozenRootChecks = _frozenRootChecks,
                singleAdvisorUser = true, peerConsultedAdvisor = false,
                networkValidated = false, visiblePerformanceValidated = false,
                transport = "native single-process transport; test-scoped advisor gate and all-player ready check; native single-process enemy-start barrier, no remote acknowledgements",
                renderingBypasses = new[] { "combat-rules tutorial (existing unattended patch)",
                    "first-shuffle tutorial", "remote-card queue visual (absent remote intent widget)" },
                availableMetrics = new { actualScoreFallbackCount = false, selectedComparable = true },
            });
        }

        private sealed class PlayerObservation : IDisposable
        {
            private readonly Player _player;
            private readonly int _initialHp;
            private int _loss;
            private int _healing;
            private int _maxHpLoss;
            private int _deaths;
            private int _revivals;
            private int _combatHp;
            private int _combatLoss;
            private int _combatHealing;
            private int _combatRevivals;
            private readonly List<object> _transitions = [];

            internal PlayerObservation(Player player)
            {
                _player = player; _initialHp = _combatHp = player.Creature.CurrentHp;
                player.Creature.CurrentHpChanged += HpChanged;
                player.Creature.MaxHpChanged += MaxChanged;
                player.Creature.Died += Died;
            }

            private void HpChanged(int before, int after)
            {
                _loss += Math.Max(0, before - after);
                _healing += Math.Max(0, after - before);
                if (before <= 0 && after > 0) _revivals++;
                bool inCombat = CombatManager.Instance.IsInProgress;
                if (inCombat)
                {
                    _combatHp = after;
                    _combatLoss += Math.Max(0, before - after);
                    _combatHealing += Math.Max(0, after - before);
                    if (before <= 0 && after > 0) _combatRevivals++;
                }
                _transitions.Add(new { kind = "hp", before, after, inCombat,
                    round = _player.Creature.CombatState?.RoundNumber });
            }
            private void MaxChanged(int before, int after) => _maxHpLoss += Math.Max(0, before - after);
            private void Died(Creature creature)
            {
                _deaths++;
                _transitions.Add(new { kind = "death", round = creature.CombatState?.RoundNumber });
            }
            internal object Snapshot() => new { actor = _player.NetId, initialHp = _initialHp,
                finalHp = _player.Creature.CurrentHp, hpLost = _loss, hpHealed = _healing,
                combatHp = _combatHp, combatHpLost = _combatLoss, combatHpHealed = _combatHealing,
                combatRevivals = _combatRevivals,
                maxHpLost = _maxHpLoss, deaths = _deaths, revivals = _revivals, transitions = _transitions };
            public void Dispose()
            {
                _player.Creature.CurrentHpChanged -= HpChanged;
                _player.Creature.MaxHpChanged -= MaxChanged;
                _player.Creature.Died -= Died;
            }
        }
    }
}
