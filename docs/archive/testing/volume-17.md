# 测试历史卷 17：社区批次 Q010 同根夹具与质量界政策（2026-10-04）

归档日期：2026-10-05。本节记录断言集在归档当日的形态；2026-10-05 合并 origin/main 后 O045 的 `expectedInitialPotionCount=1` 等值键已按质量界原则移除（零药轴自身达 proj=0 时等值会把更好路线判 Failed），现行断言集以夹具文件与复现记录末节为准。归档不表示问题已经修复；下列结果是本批认领者侧的实际执行记录，后续复测请以 [测试入口](../../TEST_MATRIX.md) 与 [Q010 复现记录](../community/2026-10-04-worldlines/Q010-claim-reproduction.md) 为准。

## 社区批次 Q010 同根夹具（2026-10-04，Refs #210）

四份输入在 `coverage/fixtures/regressions/community/q010-o04{1,2,4,5}-same-root-*.json`，
从各主题原包的 `combat_start` 同根起搜，只锁终值，不断言路线同构。审核稿改善值 12/1/8/5
（O041/O042/O043/O044）录于中途检查点，不同根，因此不作为断言目标；
可比性与录制检查点见 [Q010 复现记录](../community/2026-10-04-worldlines/Q010-claim-reproduction.md)。

复跑入口是 `tools/testing/run-unattended-test.ps1`（Bash：`tools/testing/run-unattended-test.sh`），
公共参数 `-CheckpointSelector start -ReplayMode SearchOnly -HeadlessFastModeForTest Instant
-StopAfterInitialSolverResultAssertion -TimeoutSeconds 300 -CleanupInstanceOnExit`，再按夹具传
`-ScenarioId`、`-CheckpointArchivePath` 与各主题的断言参数（逐项取自夹具 JSON）。这些命令依赖
不入库的原包 ZIP 与本机 RitsuLib 路径，因此不写进下方矩阵启动器清单。2026-10-04 已串行首次执行
（`-CleanupInstanceOnExit`，逐条完成再下一条）：O041、O042、O044 Passed，O045 Failed，结论与
逐字段数值见 [Q010 复现记录](../community/2026-10-04-worldlines/Q010-claim-reproduction.md) 的
「同根夹具首次执行结果」一节；`coverage/evidence/test-evidence.json` 已按实际状态登记（三条 Passed、O045 Failed）。

复跑必须声明内存档（`GC_SEARCH_ALLOCATION_LIMIT` 与 `GC_NO_GC_REGION_DECLINED percent_of_configured`）：
同一夹具在窄档（弃区 24%）与宽档（44%）会给出不同终值，O045 实测窄档 1 战损 / 1 瓶、宽档 14 战损 / 0 瓶，
O041/O042/O044 跨档不变。W2 修复（Smart 药水层不再被 strategic 净差整层否证）后四条复跑均 Passed，score 与修复前逐位相同（O045 由 Failed 转 Passed，runId 095e69ee275b4acca62f35e9e735c869）。

夹具锁定质量界（本批新增口径，captain 对 t14 的裁决①）：A 类同根夹具的断言只锁质量界——战损轴用
`expectedInitialProjectedBattleHpLostAtMost`（上界）、用药轴用 `expectedInitialPotionCount`（等值，比下限更严）、
胜负轴用 `expectedInitialFinalEnemyHpAtMost=0` + `expectedInitialOnlyDeathRoutesFound=false`；单机快照值
（`combatEndedTurn`、`potionHpSavedAtLeast`、`boundaryReason`、`score`）只作观测记录写进 description 与证据文件，
不入断言。理由是录制三元组是录制机内存档形态的函数：同机同根下无修复二进制与修复后二进制给出同一偏移量
（O044 两侧各两次都稳定 proj=3、endTurn=7、score=10001949972，而录制值是 proj=5、endTurn=6），把快照值当等值断言
就会把环境差异误报成退化。误用警示：`expectedInitialHpLostAtMost` 断的是 `HpLostByTurn[startedTurn]`（首回合掉血），
**不是**整场战损，proj 轴一律用 `ExpectedInitialProjectedBattleHpLostAtMost`（断言体
`src/Testing/Contracts/Search/UnattendedTestRunner.SolverPolicy.cs:417-422`）。按此口径，O044/O045 落盘后各重跑一次
均 Passed：`4e17b33a1367462c906cca1a83fc4aae`（limit=1232616004 字节 = 1176 MiB，proj=3 ≤ 5）、
`7d8a2799942d459390c12a1b87142a3d`（limit=1235238808 字节 = 1178 MiB，proj=1 ≤ 1、1 瓶）。

四份夹具的 `timeoutSeconds=300` 高于 AGENTS.md 第 8 节的 120 秒默认口径：该值跟随各包内录制的
`softTimeBudgetMilliseconds`（五包分别 120000/300000/180000/300000/180000ms，最长 300000ms），属
同政策同预算对照而非放大预算；首次执行四次的 launcher 墙钟 28.7–40.0 秒均远低于该上限，无一次接近超时。
压到 120 秒会使包内软预算 300000ms 的 O042/O044 不再是同预算对照。

O042 的「被迫受击 = 1」分量现为机器断言：夹具带 `expectedInitialUnavoidableHpLost=1`，值取自
`RESULT … unavoidable_hp_lost=`（`src/Runtime/SolverDiagnostics.cs:209`）与新增的
`UnattendedSolverMetrics.UnavoidableHpLost`；入口参数 `-ExpectedInitialUnavoidableHpLost`（ps1）/
`--expected-initial-unavoidable-hp-lost`（sh）。修复前该分量只能按 diagnostics 对账（1 + sold 4 = 5）。

复跑注意：带 `-CleanupInstanceOnExit` 时，`.local/headless-instances` 的实例目录偶尔删不掉
（`game\data_sts2_windows_x86_64\0Harmony.dll` 句柄未释放），启动器因此返回退出码 1；这不改变
`result.json` 的 `Passed` 判定，残留实例目录需手动删除后重试。
