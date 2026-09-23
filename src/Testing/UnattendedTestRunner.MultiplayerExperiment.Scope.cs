using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private sealed partial class ProtocolHost
    {
        private Harmony? _multiplayerExperimentPatches;
        internal MultiplayerExperimentSpec? MultiplayerExperiment { get; private set; }
        internal SearchPolicySnapshot? LastExperimentPolicy { get; private set; }
        internal MultiplayerPlanValue? SelectedExperimentFacts { get; private set; }
        internal long MaximumObservedUnattributedDamage { get; private set; }
        private ManualResetEventSlim? _experimentWorkerGate;
        private ManualResetEventSlim? _experimentWorkerEntered;
        internal bool ExperimentWorkerEntered => _experimentWorkerEntered?.IsSet == true;

        private void ConfigureMultiplayerExperiment(UnattendedTestRequest request)
        {
            if (request.MultiplayerExperimentPath == null) return;
            if (!IsActive || request.ScenarioId != MultiplayerExperimentSpec.ScenarioId
                || !string.IsNullOrWhiteSpace(request.GeneratedScenarioPath)
                || !string.IsNullOrWhiteSpace(request.RunSnapshotPath)
                || !string.IsNullOrWhiteSpace(request.ReplayStatePath)
                || !string.IsNullOrWhiteSpace(request.CheckpointArchivePath)
                || !string.IsNullOrWhiteSpace(request.NativeStatePath)
                || !string.IsNullOrWhiteSpace(request.ShowcaseBundlePath)
                || !string.IsNullOrWhiteSpace(request.ShowcaseRoutePath)
                || !string.IsNullOrWhiteSpace(request.ReplayPolicyOverridePath)
                || request.ModifierIds.Length != 0 || request.Relics.Length != 0
                || request.RunCards.Length != 0 || request.Potions.Length != 0
                || request.PreCombatSimulationSeed != null || request.PreCombatPlayerCurrentHpOverride != null
                || request.TargetActFloor != null || request.MarkEncounterAsSecondBossForTest
                || request.TargetMapPointType != MegaCrit.Sts2.Core.Map.MapPointType.Unassigned
                || request.TargetRoomType != MegaCrit.Sts2.Core.Rooms.RoomType.Monster)
                throw new InvalidDataException("Multiplayer experiment requires its dedicated test request.");
            ResetMultiplayerExperiment();
            MultiplayerExperiment = MultiplayerExperimentSpec.Load(request.MultiplayerExperimentPath);
            _multiplayerExperimentPatches = new Harmony("CombatSolver.Tests.MultiplayerManualLoop");
            _multiplayerExperimentPatches.Patch(
                AccessTools.PropertyGetter(typeof(SolverController), nameof(SolverController.IsMultiplayerSession)),
                prefix: new HarmonyMethod(typeof(ProtocolHost), nameof(ExperimentMultiplayerPrefix)));
            _multiplayerExperimentPatches.Patch(
                AccessTools.Method(typeof(CombatManager), "AllPlayersReadyToEndTurn", [typeof(CombatTurnState)]),
                prefix: new HarmonyMethod(typeof(UnattendedTestRunner), nameof(MultiplayerRelicReadyPrefix)));
            _multiplayerExperimentPatches.Patch(
                AccessTools.Method(typeof(MultiplayerSearchPolicy), nameof(MultiplayerSearchPolicy.Apply)),
                postfix: new HarmonyMethod(typeof(ProtocolHost), nameof(ExperimentPolicyPostfix)));
            _multiplayerExperimentPatches.Patch(
                AccessTools.Method(typeof(CombatBeamSolver), "SelectMultiplayerFinal"),
                postfix: new HarmonyMethod(typeof(ProtocolHost), nameof(ExperimentSelectedPostfix)));
            _multiplayerExperimentPatches.Patch(
                AccessTools.Method(typeof(CombatBeamSolver), "MultiplayerFactsAt"),
                postfix: new HarmonyMethod(typeof(ProtocolHost), nameof(ExperimentFactsPostfix)));
            _multiplayerExperimentPatches.Patch(
                AccessTools.Method(typeof(CombatBeamSolver), nameof(CombatBeamSolver.Solve)),
                prefix: new HarmonyMethod(typeof(ProtocolHost), nameof(ExperimentWorkerPrefix)));
            _multiplayerExperimentPatches.Patch(
                AccessTools.Method(typeof(CardPileCmd), "ShuffleFtueCheck"),
                prefix: new HarmonyMethod(typeof(ProtocolHost), nameof(ExperimentShuffleTutorialPrefix)));
            _multiplayerExperimentPatches.Patch(
                AccessTools.Method(typeof(NCardPlayQueue), "OnActionEnqueued"),
                prefix: new HarmonyMethod(typeof(ProtocolHost), nameof(ExperimentRemoteCardVisualPrefix)));
        }

        private static bool ExperimentShuffleTutorialPrefix(ref Task __result)
        {
            // First-shuffle teaching requires a human confirmation in a fresh isolated profile.
            __result = Task.CompletedTask;
            return false;
        }

        private static bool ExperimentRemoteCardVisualPrefix(GameAction __0)
            => __0 is not PlayCardAction play || LocalContext.IsMe(play.Player);

        internal void HoldExperimentWorker()
        {
            if (MultiplayerExperiment?.Options.Mode != "RootIsolation")
                throw new InvalidOperationException("Worker gating belongs only to the isolation contract.");
            _experimentWorkerGate = new(false);
            _experimentWorkerEntered = new(false);
        }

        internal void ReleaseExperimentWorker() => _experimentWorkerGate?.Set();

        private static void ExperimentWorkerPrefix()
        {
            if (Host._experimentWorkerGate is not { IsSet: false } gate) return;
            Host._experimentWorkerEntered!.Set();
            if (!gate.Wait(TimeSpan.FromSeconds(12)))
                throw new TimeoutException("Isolation contract did not release the worker.");
        }

        internal static bool PublishedAdviceIsStale()
            => ((SolverCombatSession)AccessTools.Field(typeof(SolverController), "_combat")
                .GetValue(null)!).AdvisoryStale;

        internal void BeginExperimentObservation()
        {
            SelectedExperimentFacts = null;
            MaximumObservedUnattributedDamage = 0;
        }

        private static class ExperimentSelectionAccessor
        {
            internal static readonly AccessTools.FieldRef<CombatBeamSolver, MultiplayerPlanValue> SelectedFacts =
                AccessTools.FieldRefAccess<CombatBeamSolver, MultiplayerPlanValue>("_selectedContribution");
        }

        private static void ExperimentSelectedPostfix(CombatBeamSolver __instance)
            => Host.SelectedExperimentFacts = ExperimentSelectionAccessor.SelectedFacts(__instance);

        private static void ExperimentFactsPostfix(MultiplayerPlanValue __result)
            => Host.MaximumObservedUnattributedDamage = Math.Max(
                Host.MaximumObservedUnattributedDamage, __result.UnattributedDamage);

        private static bool ExperimentMultiplayerPrefix(ref bool __result)
        {
            // Only the advisor gate is emulated; the declared transport remains single-process.
            __result = RunManager.Instance.IsInProgress
                && RunManager.Instance.DebugOnlyGetState()?.Players.Count > 1;
            return false;
        }

        private static void ExperimentPolicyPostfix(ref SearchPolicySnapshot __result)
        {
            MultiplayerExperimentSpec spec = Host.MultiplayerExperiment
                ?? throw new InvalidOperationException("Experiment policy hook escaped its request.");
            __result = __result with
            {
                Multiplayer = __result.Multiplayer! with { CreditSharedDamage = spec.Options.CreditSharedDamage },
                Profile = spec.SearchProfile, FixedBudget = true,
                BudgetOverrideMilliseconds = spec.SearchProfile.SoftTimeBudgetMilliseconds,
                MaxDegreeOfParallelism = 1,
            };
            Host.LastExperimentPolicy = __result;
        }

        private void ResetMultiplayerExperiment()
        {
            ReleaseExperimentWorker();
            _experimentWorkerGate?.Dispose();
            _experimentWorkerEntered?.Dispose();
            _experimentWorkerGate = null;
            _experimentWorkerEntered = null;
            _multiplayerExperimentPatches?.UnpatchAll(_multiplayerExperimentPatches.Id);
            _multiplayerExperimentPatches = null;
            MultiplayerExperiment = null;
            LastExperimentPolicy = null;
            BeginExperimentObservation();
        }

        internal void AssertMultiplayerExperimentReset()
        {
            if (MultiplayerExperiment != null || _multiplayerExperimentPatches != null
                || LastExperimentPolicy != null || SelectedExperimentFacts != null
                || _experimentWorkerGate != null || _experimentWorkerEntered != null
                || MaximumObservedUnattributedDamage != 0
                || Harmony.GetAllPatchedMethods().Any(method => Harmony.GetPatchInfo(method)!.Owners
                    .Contains("CombatSolver.Tests.MultiplayerManualLoop")))
                throw new InvalidOperationException("Multiplayer experiment scope leaked into a later request.");
        }
    }
}
