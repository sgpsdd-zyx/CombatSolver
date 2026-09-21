using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using CombatSolver;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace OfflineSearchHarness;

internal static class MultiplayerStartContracts
{
    private static NGame _host = null!;
    private static bool _provideEntryHost;
    private static string _userDataDirectory = string.Empty;
    private static readonly List<string> Messages = [];
    private static readonly ConcurrentQueue<string> RuntimeEvents = new();
    private static int _searchingShown;
    private static NetGameType _netType = NetGameType.Host;
    private static Exception? _checkpointFailure;

    internal static string Run(CombatState state, HarnessOptions options, MainLoopContext loop,
        Action? beforeRecalculate = null)
    {
        _host = (NGame)RuntimeHelpers.GetUninitializedObject(typeof(NGame));
        _userDataDirectory = options.OutputDirectory;
        Patch(AccessTools.Method(typeof(CombatSolverLog), "Write"), nameof(LogPrefix));
        Patch(AccessTools.PropertyGetter(typeof(NGame), nameof(NGame.Instance)), nameof(HostPrefix));
        // Exercise the real host/client guard, while keeping the offline transport.
        Patch(AccessTools.PropertyGetter(RunManager.Instance.NetService.GetType(), "Type"), nameof(NetTypePrefix));
        Patch(AccessTools.Method(typeof(SolverDispatcher), nameof(SolverDispatcher.Ensure)), nameof(SkipPrefix));
        Patch(AccessTools.Method(typeof(OS), nameof(OS.GetUserDataDir)), nameof(UserDataPrefix));
        Patch(AccessTools.Method(typeof(Engine), nameof(Engine.GetProcessFrames)), nameof(ClockPrefix));
        Patch(AccessTools.Method(typeof(Time), nameof(Time.GetTicksMsec)), nameof(ClockPrefix));
        Patch(AccessTools.Method(typeof(SolverOverlay), nameof(SolverOverlay.Show)), nameof(ShowPrefix));
        Patch(AccessTools.Method(typeof(SolverOverlay), nameof(SolverOverlay.ShowSearching)), nameof(SearchingPrefix));
        Patch(AccessTools.Method(typeof(SolverOverlay), nameof(SolverOverlay.ShowResult)), nameof(SkipPrefix));
        Patch(AccessTools.Method(typeof(SolverOverlay), nameof(SolverOverlay.ShowMultiplayerCondition)), nameof(SkipPrefix));
        Patch(AccessTools.Method(typeof(SolverOverlay), nameof(SolverOverlay.RefreshControls)), nameof(SkipPrefix));
        Patch(AccessTools.Method(typeof(SolverOverlay), nameof(SolverOverlay.ShowSearchStopped)), nameof(SkipPrefix));
        Patch(AccessTools.Method(typeof(CombatBugReportExporter), nameof(CombatBugReportExporter.RecordCheckpoint)),
            nameof(CheckpointPrefix));
        // Full replay payload capture requires Godot. Keep forensic session creation,
        // the outcome ledger, and the actual RequestSearch path active in this fixture.
        GameBootstrap.Harmony.Patch(AccessTools.Method(typeof(CombatBugReportExporter), "RecordCheckpointCore"),
            transpiler: new HarmonyMethod(typeof(MultiplayerStartContracts), nameof(SkipReplayPayload)));

        MethodInfo click = AccessTools.Method(typeof(SolverOverlay), "OnRecalculatePressed");
        HarnessLog.Trace("manual_startup_bindings_ready");
        ulong? originalLocalId = LocalContext.NetId;
        try
        {
            foreach (var (type, index) in new[] { (NetGameType.Host, 0), (NetGameType.Client, 1) })
            {
                _netType = type;
                LocalContext.NetId = state.Players[index].NetId;
                HarnessLog.Trace($"outcome_ownership net_type={type} local_index={index}");
                VerifyOutcomeOwnership(state, state.Players[index]);
                HarnessLog.Trace("manual_begin_combat");
                SolverController.BeginCombat(state);
                HarnessLog.Trace($"manual_click_ready net_type={type} local_index={index}");
                Click(click);
                WaitForSearch(loop);
                RequireAdvice();
            }

            if (beforeRecalculate != null)
            {
                beforeRecalculate();
                Click(click);
                WaitForSearch(loop);
                RequireAdvice();
                File.WriteAllLines(Path.Combine(options.OutputDirectory, "manual-recalculation-events.txt"), RuntimeEvents);
                return $"manual_recalculation=passed searches={_searchingShown}; "
                    + "Godot rendering, replay payload capture, and network transport are bypassed";
            }

            VerifyPendingAction(click, loop);
            VerifyStartupFailures(click, loop);
            Click(click);
            WaitForSearch(loop);
            RequireAdvice();
            File.WriteAllLines(Path.Combine(options.OutputDirectory, "manual-startup-events.txt"), RuntimeEvents);
            return $"host_local_index=0 client_local_index=1 outcome_ownership=passed "
                + $"pending_resume=passed startup_failures=3 retry=passed searches={_searchingShown}; "
                + "Godot rendering, replay payload capture, and network transport are bypassed";
        }
        finally
        {
            LocalContext.NetId = originalLocalId;
            GameBootstrap.Harmony.Unpatch(
                AccessTools.PropertyGetter(RunManager.Instance.NetService.GetType(), "Type"),
                AccessTools.Method(typeof(MultiplayerStartContracts), nameof(NetTypePrefix)));
        }
    }

    private static void VerifyOutcomeOwnership(CombatState state, Player local)
    {
        Player peer = state.Players.Single(player => !ReferenceEquals(player, local));
        int localHp = local.Creature.CurrentHp;
        int peerHp = peer.Creature.CurrentHp;
        try
        {
            using CombatReplayOutcome outcome = new(state);
            peer.Creature.SetCurrentHpInternal(peerHp - 7);
            local.Creature.SetCurrentHpInternal(localHp - 3);
            local.Creature.SetCurrentHpInternal(localHp - 1);
            CombatReplayOutcomeSnapshot observed = outcome.Capture(state, ended: false);
            if (observed.InitialHp != localHp || observed.FinalHp != localHp - 1
                || observed.HpLost != 3 || observed.HpHealed != 2)
                throw new InvalidOperationException("Outcome did not isolate local HP loss and healing from the peer.");
        }
        finally
        {
            peer.Creature.SetCurrentHpInternal(peerHp);
            local.Creature.SetCurrentHpInternal(localHp);
        }
    }

    private static void VerifyPendingAction(MethodInfo click, MainLoopContext loop)
    {
        int before = Messages.Count;
        int searchesBefore = _searchingShown;
        TaskCompletionSource<bool> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ActionExecutor executor = RunManager.Instance.ActionExecutor;
        FieldInfo queueTask = AccessTools.Field(typeof(ActionExecutor), "_queueTaskCompletionSource");
        object? originalQueue = queueTask.GetValue(executor);
        queueTask.SetValue(executor, pending);
        try
        {
            HarnessLog.Trace("manual_click_pending_action");
            Click(click);
            if (!SolverController.IsSearching || _searchingShown != searchesBefore)
                throw new InvalidOperationException("Pending action did not defer root capture.");
            if (Messages.Count == before)
                throw new InvalidOperationException("Pending native action leaves the manual click without UI feedback.");
        }
        finally
        {
            pending.TrySetResult(true);
            queueTask.SetValue(executor, originalQueue);
        }
        WaitForSearch(loop);
        RequireAdvice();
        if (_searchingShown != searchesBefore + 1)
            throw new InvalidOperationException("Native action completion did not resume exactly one search.");
    }

    private static void VerifyStartupFailures(MethodInfo click, MainLoopContext loop)
    {
        Exception checkpointFailure = new InvalidOperationException("injected checkpoint failure");
        _checkpointFailure = checkpointFailure;
        try
        {
            int before = Messages.Count;
            Click(click);
            RequireFailure(checkpointFailure, before);
        }
        finally { _checkpointFailure = null; }

        ActionExecutor executor = RunManager.Instance.ActionExecutor;
        FieldInfo queueTask = AccessTools.Field(typeof(ActionExecutor), "_queueTaskCompletionSource");
        object? originalQueue = queueTask.GetValue(executor);
        try
        {
            foreach (bool deferred in new[] { false, true })
            {
                Exception failure = new InvalidOperationException($"injected action failure deferred={deferred}");
                TaskCompletionSource<bool> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
                queueTask.SetValue(executor, pending);
                if (!deferred) pending.SetException(failure);
                int before = Messages.Count;
                Click(click);
                if (deferred) pending.SetException(failure);
                WaitForSearch(loop);
                RequireFailure(failure, before);
            }
        }
        finally { queueTask.SetValue(executor, originalQueue); }
    }

    private static void RequireFailure(Exception expected, int messagesBefore)
    {
        if (SolverController.IsSearching || SolverController.LastSearchFailureForTesting != expected
            || !Messages.Skip(messagesBefore).Any(message => message.Contains(expected.Message, StringComparison.Ordinal)))
            throw new InvalidOperationException("Startup failure did not stop and report: " + expected.Message);
    }

    private static void RequireAdvice()
    {
        if (SolverController.LastSearchFailureForTesting is { } failure)
            throw new InvalidOperationException("Manual search failed: " + failure, failure);
        if (SolverController.CurrentResultForBugReport?.IsMultiplayerAdvice != true
            || SolverController.IsDeploying || SolverController.FullAutoEnabled)
            throw new InvalidOperationException("Manual search did not produce advice without execution.");
    }

    private static void WaitForSearch(MainLoopContext loop)
    {
        var queue = (ConcurrentQueue<Action>)AccessTools.Field(typeof(SolverDispatcher), "Queue").GetValue(null)!;
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (SolverController.IsSearching)
        {
            if (DateTime.UtcNow >= deadline) throw new TimeoutException("Manual startup did not finish within 30 seconds.");
            loop.Pump(TimeSpan.FromMilliseconds(10));
            while (queue.TryDequeue(out Action? action)) action();
        }
    }

    private static void Click(MethodInfo handler)
    {
        _provideEntryHost = true;
        try { handler.Invoke(null, null); }
        finally { _provideEntryHost = false; }
    }

    private static void Patch(MethodInfo method, string prefix)
    {
        HarnessLog.Trace($"manual_startup_bind {method.DeclaringType?.Name}.{method.Name}");
        GameBootstrap.Harmony.Patch(method, prefix: new HarmonyMethod(typeof(MultiplayerStartContracts), prefix));
    }

    private static bool SkipPrefix() => false;
    private static void LogPrefix(string __0, string __1)
    {
        RuntimeEvents.Enqueue(__1);
        if (__0 == "error") HarnessLog.Trace(__1);
    }
    private static IEnumerable<CodeInstruction> SkipReplayPayload(IEnumerable<CodeInstruction> instructions)
        => [new(OpCodes.Ret)];
    private static bool HostPrefix(ref NGame? __result)
    {
        // Only the click handler needs a host; native model code still has no scene tree.
        __result = _provideEntryHost ? _host : null;
        _provideEntryHost = false;
        return false;
    }
    private static bool NetTypePrefix(ref NetGameType __result) { __result = _netType; return false; }
    private static bool CheckpointPrefix()
    {
        if (_checkpointFailure != null) throw _checkpointFailure;
        return true;
    }
    private static bool UserDataPrefix(ref string __result) { __result = _userDataDirectory; return false; }
    private static bool ClockPrefix(ref ulong __result) { __result = (ulong)System.Environment.TickCount64; return false; }
    private static bool ShowPrefix(string __1)
    {
        Messages.Add(__1);
        HarnessLog.Trace("manual_ui_message " + __1);
        return false;
    }
    private static bool SearchingPrefix()
    {
        _searchingShown++;
        HarnessLog.Trace("manual_ui_searching");
        return false;
    }
}
