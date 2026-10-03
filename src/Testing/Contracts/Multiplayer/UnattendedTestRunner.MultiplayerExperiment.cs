using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private async Task RunMultiplayerExperimentAsync(CombatState combat)
    {
        var spec = _protocolHost.MultiplayerExperiment
            ?? throw new InvalidOperationException("Missing multiplayer experiment request.");
        using var driver = new MultiplayerExperimentDriver(this, combat, spec);
        await driver.RunAsync();
        _completedChecks.Add($"MultiplayerManualLoop:{driver.Outcome}:queries={driver.Queries}:" +
            $"local_actions={driver.LocalActions}:peer_actions={driver.PeerActions}");
    }

    private sealed partial class MultiplayerExperimentDriver : IDisposable
    {
        private readonly UnattendedTestRunner _runner;
        private readonly CombatState _combat;
        private readonly MultiplayerExperimentSpec _spec;
        private readonly Player _local;
        private readonly Player _peer;
        private readonly int _startRound;
        private readonly int _startTurn;
        private readonly List<object> _events = [];
        private readonly List<object> _queries = [];
        private readonly List<PlanAction> _remaining = [];
        private readonly Dictionary<Player, PlayerObservation> _health = [];
        private readonly MultiplayerContributionSession _expectedLedger = new();
        private readonly SolverSettingsData _originalSettings;
        private bool _nativeEnded;
        private bool _nativeWon;
        private bool _peerChanged;
        private int _lastQueryTurn = -1;
        private int _decisions;
        private int _invalidAdvice;
        private int _localActions;
        private int _peerActions;
        private int _expectedSearches;
        private int _sequence;
        private int _strictActionChecks;
        private int _frozenRootChecks;
        private string _outcome = "execution_error";

        internal MultiplayerExperimentDriver(UnattendedTestRunner runner, CombatState combat,
            MultiplayerExperimentSpec spec)
        {
            _runner = runner; _combat = combat; _spec = spec;
            _local = LocalContext.GetMe(combat) ?? throw new InvalidOperationException("Missing local actor.");
            _peer = combat.Players.Single(player => player != _local);
            _startRound = combat.RoundNumber;
            _startTurn = _local.PlayerCombatState!.TurnNumber;
            _originalSettings = SolverSettings.Current;
            SolverSettings.ApplyForTesting(_originalSettings with
            {
                AutomaticCalculationEnabled = false, AutoEnableFullAuto = false,
                EnableNoGcRegion = false, OnlineStatisticsEnabled = false,
                SearchCompletionNotificationsEnabled = false, PotionPolicy = SolverPotionPolicy.Disabled,
                DeploymentFastMode = SolverDeploymentFastMode.Instant, DeploymentInterActionDelaySeconds = 0,
            });
            foreach (Player player in combat.Players) _health.Add(player, new PlayerObservation(player));
            CombatManager.Instance.CombatEnded += OnEnded;
            CombatManager.Instance.CombatWon += OnWon;
            _expectedSearches = SolverController.SearchesStartedForTesting;
        }

        internal string Outcome => _outcome;
        internal int Queries => _queries.Count;
        internal int LocalActions => _localActions;
        internal int PeerActions => _peerActions;

        internal async Task RunAsync()
        {
            try
            {
                _runner.SetStage("multiplayer_opening");
                if (_local.NetId != 1 || _combat.Enemies.Count < _spec.Root.MinimumActiveEnemies
                    || !SolverController.IsMultiplayerSession || SolverController.FullAutoEnabled)
                    throw new InvalidOperationException("Invalid native multiplayer experiment opening.");
                string opening = ContinuationStamp.CaptureLive(_combat, multiplayerAdvisor: true).StateText;
                string nativeOpening = Convert.ToBase64String(CombatShowcaseNativeState.CaptureNormalized(_combat));
                _runner._writer.WriteGeneratedArtifact("multiplayer-opening.json", new
                {
                    state = opening,
                    nativeState = nativeOpening,
                    _spec.Root.Id, _spec.SourceCluster,
                    party = _combat.Players.Select(player => new
                    {
                        player.NetId, character = player.Character.Id.Entry,
                        deck = player.Deck.Cards.Select(card => new { id = card.Id.Entry, key = CardChoiceSupport.ChoiceCardKey(card) }),
                        relics = player.Relics.Select(relic => relic.Id.Entry),
                        player.Creature.CurrentHp, player.Creature.MaxHp,
                    }),
                });
                if (_spec.Options.ExpectedOpeningPath is { } expectedPath)
                {
                    using JsonDocument expected = JsonDocument.Parse(File.ReadAllText(expectedPath));
                    if (expected.RootElement.GetProperty("state").GetString() != opening
                        || expected.RootElement.GetProperty("nativeState").GetString() != nativeOpening)
                        throw new MultiplayerExperimentBoundaryException("root_mismatch",
                            "Full party opening differs from its paired root.");
                }
                Record("opening");
                if (_spec.Options.Mode == "Contract")
                {
                    CombatManager.Instance.SetReadyToEndTurn(_peer, true);
                    if (!CombatManager.Instance.IsPlayerReadyToEndTurn(_peer))
                        throw new InvalidOperationException("Native peer ready was not observed.");
                    CombatManager.Instance.UndoReadyToEndTurn(_peer);
                    if (CombatManager.Instance.IsPlayerReadyToEndTurn(_peer))
                        throw new InvalidOperationException("Native peer ready withdrawal failed.");
                    Record("ready_withdrawal_contract");
                }
                _runner.SetStage("multiplayer_manual_loop");
                if (_spec.Options.Mode == "RootIsolation")
                {
                    if (!await Query("inflight_peer_action") || !await Query("after_inflight_peer_action"))
                        throw new InvalidOperationException("Isolation contract did not complete both manual queries.");
                    _outcome = "isolation_complete";
                    return;
                }
                while (!_nativeEnded)
                {
                    _runner.EnsureWithinDeadline();
                    AssertSingleUserControl();
                    if (CombatManager.Instance.IsOverOrEnding)
                    {
                        await _runner.NextFrameAsync();
                        continue;
                    }
                    if (_spec.Options.Mode == "Contract" && Queries >= 2 && _lastQueryTurn > _startTurn)
                    {
                        if (_localActions == 0 || _peerActions == 0 || _strictActionChecks == 0 || _frozenRootChecks == 0)
                            throw new InvalidOperationException("Contract did not exercise both native actors.");
                        _outcome = "contract_complete";
                        return;
                    }
                    if (_combat.RoundNumber - _startRound >= _spec.Options.EnemyCycleLimit)
                    { _outcome = "cycle_limit"; return; }
                    if (_decisions >= _spec.Options.DecisionLimit)
                    { _outcome = "decision_limit"; return; }
                    if (_combat.CurrentSide != CombatSide.Player)
                    { await _runner.NextFrameAsync(); continue; }

                    bool acted;
                    if (_spec.Options.Schedule == "alternate_one_action")
                    {
                        acted = await ActLocal();
                        if (_outcome != "execution_error") return;
                        if (!CombatManager.Instance.IsOverOrEnding) acted |= await ActPeer();
                    }
                    else
                    {
                        bool peerFirst = _spec.Options.Schedule == "peer_batch_first";
                        Player first = peerFirst ? _peer : _local;
                        acted = CanAct(first)
                            ? await (peerFirst ? ActPeer() : ActLocal())
                            : await (peerFirst ? ActLocal() : ActPeer());
                        if (_outcome != "execution_error") return;
                    }
                    if (!acted) await _runner.NextFrameAsync();
                }
                _outcome = _nativeWon ? "team_win" : "team_loss";
            }
            catch (MultiplayerExperimentBoundaryException error)
            {
                _outcome = error.Outcome;
                Record("declared_boundary", new { error.Message });
                throw;
            }
            catch (NativeChoicePlanMismatchException)
            {
                _outcome = "choice_adapter_missing";
                throw;
            }
            catch (TimeoutException)
            {
                _outcome = "request_timeout";
                throw;
            }
            finally { Save(); }
        }

        private bool CanAct(Player player) => player.Creature.IsAlive
            && _combat.CurrentSide == CombatSide.Player
            && player.PlayerCombatState?.Phase == PlayerTurnPhase.Play
            && !CombatManager.Instance.IsPlayerReadyToEndTurn(player);

        private async Task<bool> ActLocal()
        {
            if (!CanAct(_local)) return false;
            int turn = _local.PlayerCombatState!.TurnNumber;
            if (_lastQueryTurn != turn || _peerChanged && _spec.Options.RequestCadence == "after_peer_batch")
            {
                if (!await Query(_lastQueryTurn != turn ? "turn_start" : "after_peer_batch")) return false;
                if (_spec.Options.Mode == "FirstRequest") { _outcome = "first_request_complete"; return false; }
            }
            if (_remaining.Count == 0)
            { _outcome = "no_advice"; return false; }
            PlanAction next = _remaining[0];
            if (!TryResolve(next, out CardModel? card, out var target))
            {
                _invalidAdvice++;
                Record("advice_invalid", next);
                _remaining.Clear();
                if (!await Query("advice_invalid")) return false;
                return true;
            }
            _remaining.RemoveAt(0);
            await Execute(_local, next, card, target);
            if (next.IsExecutable) _localActions++;
            return true;
        }

        private async Task<bool> ActPeer()
        {
            if (!CanAct(_peer)) return false;
            var chosen = ChoosePeerAction();
            await Execute(_peer, chosen.Action, chosen.Card, chosen.Target);
            if (chosen.Action.IsExecutable) _peerActions++;
            _peerChanged = true;
            return true;
        }

        private async Task<bool> Query(string reason)
        {
            if (Queries >= _spec.Options.QueryLimit) { _outcome = "query_limit"; return false; }
            Record("manual_query_start", new { reason });
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
            var root = CombatRootSnapshot.Capture(_combat, multiplayerAdvisor: true);
            string frozen = root.ContinuationStamp.StateText;
            var expected = _expectedLedger.Observe(root.MultiplayerObservation!);
            _runner._protocolHost.BeginExperimentObservation();
            bool inflightPeer = reason == "inflight_peer_action";
            if (inflightPeer) _runner._protocolHost.HoldExperimentWorker();
            try
            {
                SolverController.RequestSearch(_runner._host, _combat, SearchReason.Manual);
                _expectedSearches++;
                if (inflightPeer)
                {
                    while (!_runner._protocolHost.ExperimentWorkerEntered)
                    {
                        _runner.EnsureWithinDeadline();
                        if (!SolverController.IsSearching)
                            throw new InvalidOperationException("Isolation search never entered its worker.");
                        await _runner.NextFrameAsync();
                    }
                    if (!await ActPeer() || !SolverController.IsSearching)
                        throw new InvalidOperationException("Peer action did not occur during the held search.");
                    _runner._protocolHost.ReleaseExperimentWorker();
                }
                while (SolverController.IsSearching)
                { _runner.EnsureWithinDeadline(); await _runner.NextFrameAsync(); }
            }
            finally { if (inflightPeer) _runner._protocolHost.ReleaseExperimentWorker(); }
            AssertSingleUserControl();
            if (SolverController.LastSearchFailureForTesting is { } failure)
                throw new InvalidOperationException("Manual experiment search failed.", failure);
            var result = SolverController.CurrentResultForBugReport;
            if (result?.IsMultiplayerAdvice != true)
            { _outcome = "no_advice"; return false; }
            var policy = _runner._protocolHost.LastExperimentPolicy
                ?? throw new InvalidOperationException("Missing effective experiment policy.");
            var facts = _runner._protocolHost.SelectedExperimentFacts
                ?? throw new InvalidOperationException("Missing selected candidate facts.");
            if (result.AdvisoryObjective != expected || policy.Multiplayer!.CreditSharedDamage != _spec.Options.CreditSharedDamage)
                throw new InvalidOperationException("Manual request lost the contribution session or treatment binding.");
            bool worldChanged = ContinuationStamp.CaptureLive(_combat, multiplayerAdvisor: true).StateText != frozen;
            if (worldChanged != inflightPeer || ProtocolHost.PublishedAdviceIsStale() != inflightPeer)
                throw new InvalidOperationException("Paused experiment world changed while searching.");
            var shadow = root.ForkSimulator();
            if (ContinuationStamp.CapturePredicted(_local, shadow, root.StartTurnNumber, root.Forecast,
                root.StartTurnNumber).StateText != frozen)
                throw new InvalidOperationException("Search mutated its frozen full-party root.");
            _remaining.Clear();
            _remaining.AddRange(result.BestNode.Actions.Where(action => action.Turn == result.StartTurnNumber
                && (action.IsExecutable || action.Kind == PlanActionKind.EndTurn)));
            if (Queries == 0 && _spec.Options.ExpectedFirstCardId is { } expectedFirst
                && _remaining.FirstOrDefault()?.CardId != expectedFirst)
                throw new InvalidOperationException("Frozen positive-control first action was not reproduced.");
            _lastQueryTurn = _local.PlayerCombatState!.TurnNumber;
            _peerChanged = false;
            _queries.Add(new
            {
                sequence = Queries + 1, reason, round = _combat.RoundNumber, turn = _lastQueryTurn,
                publishedStale = ProtocolHost.PublishedAdviceIsStale(),
                root = frozen, objective = result.AdvisoryObjective, result.AdvisoryComparisonCycles,
                result.AdvisorySearchedEnemyCycles, result.AdvisoryNodeBudget, result.AdvisoryTimeBudgetMilliseconds,
                result.ExpandedNodes, result.TransitionCount, elapsedMs = result.TotalSearchElapsed.TotalMilliseconds,
                result.TurnLayerTimeBudgetStops, result.TurnLayerNodeBudgetStops,
                boundary = result.BoundaryReason.ToString(), result.AdvisorySharedDamageCredit,
                selectedFacts = facts,
                maximumObservedUnattributedDamage = _runner._protocolHost.MaximumObservedUnattributedDamage,
                stageCovered = facts.Comparable && (facts.Won || facts.Dead
                    || result.AdvisoryComparisonCycles >= expected.RemainingCycles),
                advice = _remaining.Select(ActionView).ToArray(), policy = PolicyView(policy),
            });
            Record("manual_query", new { reason, query = Queries });
            return true;
        }

        private void AssertSingleUserControl()
        {
            if (LocalContext.NetId != _local.NetId || SolverController.IsDeploying || SolverController.FullAutoEnabled
                || SolverController.SearchesStartedForTesting != _expectedSearches)
                throw new InvalidOperationException("Experiment interfered with actor identity or automatic advisor control.");
        }

        private void OnEnded(CombatRoom room) { _nativeEnded = true; }
        private void OnWon(CombatRoom room) { _nativeWon = true; }

        public void Dispose()
        {
            CombatManager.Instance.CombatEnded -= OnEnded;
            CombatManager.Instance.CombatWon -= OnWon;
            foreach (var health in _health.Values) health.Dispose();
            SolverSettings.ApplyForTesting(_originalSettings);
        }
    }
}
