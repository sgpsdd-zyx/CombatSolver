// 研究交付物；未编译、未运行。不要自动复制进生产工程。
// 固定 API 来源：d55fa84ec07dc252ce62248a9e8b2f4af95effd5。
// 加入现有 OfflineSearchHarness 程序集后，由宿主在合法冻结根上创建 solver 并调用。
// 这是“给定合法前缀 -> 生产回放/生产排序”的探针，不是实际搜索生成/首次裁剪证明。
// 不含 C 策略实现，不增加生产开关，不直接读取后续 live CombatState。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.IO;
using CombatSolver;
using HarmonyLib;

namespace OfflineSearchHarness;

internal static class MultiplayerLongTermProductionProbe20260918
{
    private const string ExpectedSha = "d55fa84ec07dc252ce62248a9e8b2f4af95effd5";

    // solver 必须使用同一个已冻结多人根；route 名称仅用于输出，不参与排序。
    // compiledSourceSha 须由宿主真实构建元数据提供；字符串相等本身不验证二进制来源。
    public static void WriteProbe(
        CombatBeamSolver solver,
        SearchPolicySnapshot policy,
        IReadOnlyDictionary<string, IReadOnlyList<PlanAction>> routes,
        int beamWidth,
        string compiledSourceSha,
        string outputPath)
    {
        ArgumentNullException.ThrowIfNull(solver);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(routes);
        if (policy.Multiplayer == null)
            throw new InvalidOperationException("This probe must never enter a solo request.");
        if (!String.Equals(compiledSourceSha, ExpectedSha, StringComparison.Ordinal))
            throw new InvalidOperationException("Source SHA mismatch; rebuild/review the adapter.");
        if (beamWidth != policy.Profile.BeamWidth || beamWidth < 1)
            throw new ArgumentException("Use the current fixture's unchanged BeamWidth.", nameof(beamWidth));
        if (routes.Count < 2 || routes.Count > 8 || routes.Values.Any(x => x.Count == 0 || x.Count > 32))
            throw new ArgumentException("Probe requires 2..8 nonempty routes of at most 32 actions.");
        if (String.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("An explicit research output path is required.", nameof(outputPath));

        var allocated = new List<SearchNode>();
        var prefixes = new List<(string Name, int Depth, SearchNode Node)>();
        var leaves = new List<(string Name, SearchNode Node)>();
        var observations = new List<object>();
        int replayCalls = 0;
        int replayEdges = 0;
        try
        {
            foreach (var route in routes)
            {
                SearchNode? parent = null;
                for (int depth = 0; depth <= route.Value.Count; depth++)
                {
                    PlanAction[] actions = route.Value.Take(depth).ToArray();
                    replayCalls++;
                    replayEdges += actions.Length; // 包括重复前缀的完整回放成本。
                    SimulationSnapshot snapshot = solver.ReplayMultiplayerForTesting(actions);
                    PlanAction? action = depth == 0 ? null : actions[^1];
                    var node = new SearchNode(action, actions.Length, snapshot.PotionUseCount,
                        snapshot.PotionStrategicCost, snapshot.Turn, SearchRouteTraits.None, 0,
                        snapshot.Score, snapshot.StateKey, snapshot.HasRisk, snapshot.BoundaryReason,
                        snapshot.PlayerDead || snapshot.AllEnemiesDead
                            || snapshot.BoundaryReason != SearchBoundaryReason.None,
                        parent, snapshot, CombatProgressState.Capture(snapshot));
                    allocated.Add(node);
                    if (snapshot.AdvisoryHpLossAllowance < 0)
                        throw new InvalidOperationException("Replay did not produce a multiplayer snapshot.");
                    prefixes.Add((route.Key, depth, node));
                    observations.Add(new
                    {
                        route = route.Key, depth, node.ActionCount, node.Score,
                        stateKey = node.StateKey.ToString(),
                        snapshot.PersistentBuffValue, snapshot.FutureResourceValue,
                        snapshot.DelayedDamageValue, snapshot.ReachableHandValue, snapshot.Stars,
                        snapshot.AdvisoryEnemyCycles, snapshot.CumulativePlayerHpLost,
                        snapshot.PlayerHp, snapshot.EnemyHp, snapshot.TeamSurvivors,
                        snapshot.PotionUseCount, snapshot.DeathSaveUseCount,
                        boundary = snapshot.BoundaryReason.ToString(),
                        snapshot.PlayerDead, snapshot.AllEnemiesDead,
                        completePrefix = actions
                    });
                    if (depth < route.Value.Count && node.IsTerminal)
                        throw new InvalidOperationException(
                            $"{route.Key}: proposed suffix crosses terminal/unsupported boundary at {depth}.");
                    parent = node;
                }
                leaves.Add((route.Key, parent!));
            }

            object retention = AccessTools.Property(typeof(CombatBeamSolver), "Retention")
                ?.GetValue(solver) ?? throw new MissingMemberException("Retention");
            MethodInfo rankMethod = AccessTools.Method(retention.GetType(), "RankMultiplayer")
                ?? throw new MissingMethodException("RankMultiplayer");
            var cuts = new List<object>();
            foreach (var layer in prefixes.Where(x => x.Depth > 0).GroupBy(x => x.Depth))
            {
                SearchNode[] pool = layer.Select(x => x.Node).ToArray();
                // 明确为注入前缀池实验，不等于生产 Expand 的全部候选池。
                object? raw = rankMethod.Invoke(retention, new object[] { pool, beamWidth, false });
                var kept = raw as List<SearchNode>
                    ?? throw new InvalidOperationException("Unexpected RankMultiplayer result type.");
                if (kept.Count > beamWidth) throw new InvalidOperationException("Beam bound exceeded.");
                cuts.Add(new
                {
                    depth = layer.Key,
                    entered = layer.Select(x => x.Name).ToArray(),
                    retained = layer.Where(x => kept.Any(n => ReferenceEquals(n, x.Node)))
                        .Select(x => x.Name).ToArray()
                });
            }

            // 走当前完整最终池资格 -> 单一共同周期 -> 截断入口，不自行重写比较器。
            MethodInfo prepare = AccessTools.Method(typeof(CombatBeamSolver), "PrepareMultiplayerFinalCandidates")
                ?? throw new MissingMethodException("PrepareMultiplayerFinalCandidates");
            object batch = prepare.Invoke(solver, new object[] { leaves.Select(x => x.Node).ToArray() })
                ?? throw new InvalidOperationException("Missing final batch.");
            var final = AccessTools.Property(batch.GetType(), "Candidates")?.GetValue(batch) as List<SearchNode>
                ?? throw new InvalidOperationException("Missing batch candidates.");
            object ordering = AccessTools.Property(batch.GetType(), "Ordering")?.GetValue(batch)
                ?? throw new InvalidOperationException("Missing fixed ordering.");
            int common = (int)(AccessTools.Property(ordering.GetType(), "EnemyCycles")?.GetValue(ordering)
                ?? throw new InvalidOperationException("Missing comparison boundary."));
            string? winner = final.Count == 0 ? null
                : leaves.Single(x => ReferenceEquals(x.Node, final[0])).Name;
            string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (directory != null) Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, JsonSerializer.Serialize(new
            {
                sourceShaSuppliedByHost = compiledSourceSha,
                evidence = "HOST_PRODUCTION_FUNCTION_PROBE_ONLY_WHEN_THIS_FILE_ACTUALLY_RUNS",
                scope = "Injected route prefixes; NOT generation, C strategy, native differential, or multiplayer play",
                replayCalls, replayEdges, beamWidth, commonCycle = common, winner,
                noEligibleFinalRoute = final.Count == 0,
                observations, injectedPoolCuts = cuts,
                missing = new[] { "actual search first-loss trace", "native differential", "real multiplayer", "solo RNG differential" }
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            // 本探针拥有新回放快照；不释放调用方传入的 solver/root。
            foreach (SearchNode node in allocated) node.Snapshot.ReleaseSimulator();
        }
    }
}
