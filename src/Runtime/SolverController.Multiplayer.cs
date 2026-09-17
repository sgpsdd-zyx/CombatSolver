using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace CombatSolver;

internal static partial class SolverController
{
    private static void ObserveMultiplayerAdvice(CombatState state)
    {
        if (System.Environment.TickCount64 - _combat.AdvisoryCheckedAt < 500) return;
        _combat.AdvisoryCheckedAt = System.Environment.TickCount64;
        var executor = RunManager.Instance.ActionExecutor;
        if (!executor.FinishedExecutingActions().IsCompleted
            || executor.CurrentlyRunningAction is { CompletionTask.IsCompleted: false }) return;
        LiveCombatStamp? expected = _search?.Stamp ?? _combat.LatestStamp;
        if (expected == null) return;
        bool stale = LiveCombatStamp.Capture(state) != expected;
        if (_combat.AdvisoryStale == stale) return;
        _combat.AdvisoryStale = stale;
        SolverOverlay.ShowMultiplayerCondition(stale, IsSearching);
    }

    private static void CompleteMultiplayerAdvice(NGame host, SolverSearchSession search, SolverResult result)
    {
        _combat.LatestResult = result;
        _combat.LatestStamp = search.Stamp;
        _combat.AdvisoryStale = LiveCombatStamp.Capture(search.State) != search.Stamp;
        _combat.PendingCompleteProjectionBaseline = null;
        _combat.PendingManualProjectionBaseline = null;
        _combat.FullAutoEnabled = false;
        _combat.AdvisoryRoutes.Insert(0, result.BestNode.Actions.ToArray());
        if (_combat.AdvisoryRoutes.Count > 4) _combat.AdvisoryRoutes.RemoveAt(4);
        RecordReviewedWorldlines(result);
        ApplySearchFrameMetrics(result, search);
        SolverOverlay.ShowResult(host, SolverOverlaySnapshot.CaptureWithReviewedWorldlines(
            result, unexpectedReplan: false, _combat.ReviewedWorldlinesTotal));
        SolverOverlay.ShowMultiplayerCondition(_combat.AdvisoryStale, searching: false);
        LastCompletedResultForTesting = result;
        if (search.Interaction.StopRequested) SolverOverlay.ShowSearchStopped(host);
        Entry.Logger.Info(SolverDiagnostics.DescribeResult(result));
    }
}
