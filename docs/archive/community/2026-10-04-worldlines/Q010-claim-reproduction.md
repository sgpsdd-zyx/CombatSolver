# 官方 Q010 认领者复现记录归档

来源：官方 `0d290fbe / 0.50.0`，本 fork 于 2026-10-05 按知识收尾归档。下文的维护者裁定、分支、授权、结果及未验证项属于上游历史；不构成本 fork 的操作授权或本轮测试结果。当前入口见 [测试矩阵](../../../TEST_MATRIX.md)。

# Q010 认领者复现记录（fengzhenhong，2026-10-04）

对应批次：[Q010.md](Q010.md)（维护者审核稿，发布准备已移入 archive；本文件不修改它）。分支 `perf/batch-q010`，基点 `4533f6bb`。三轮审计的纠偏过程（`hp_deficit=11` 跨口径推翻、「入选零药原始战损=26」归属撤回、判据链两轮修正、O042 整场等价=1、O043 的 12 录于 `:7`/`:9`）保留在 #210 评论与本 PR 正文；本文件只写终态事实与原始行。**归档口径（2026-10-05 维护者裁定：本批不归档，保持现状）**：本文件已越 AGENTS.md 第 9 节活动记录的 32 KiB 阈值（本行写于 commit b27bd28f，当时 45.4 KiB / 181 行；行数仍在 200 内）。按 §9 本应把 t1–t12 已完成部分压成短入口或移入 `docs/archive/testing/`，但归档会牵动文档索引、相对链接、skill 与结构化证据的路径同步，把修复轮变成结构搬迁。维护者裁定本批不搬，该项不再是开放问题。

## 复现方式与口径

五主题统一从 `combat_start` 同根起搜：`tools/replay/run-checkpoint-batch.ps1 -ReplayMode SearchOnly -CheckpointSelector start`。三个耗时/规模口径不混用：「本轮实测」取入选结果的 `comparisonQuality.projectedBattleHpLost`（整场累计口径，含本请求起算前已发生的战损；口径定义 `src/Search/CombatBeamSolver.Phases.cs:838`，`futureHpLost` 取自 `:541`）；「搜索耗时」取 `RESULT` 的 `total_elapsed_ms`（本请求所有 solver 会话之和）；「入选解展开」取 `RESULT` 的 `expanded`（产出该实测值的那个 solver）；「墙钟」取 `timings.json` 各阶段之和（含约 19 秒回放启动）。

| 主题 | 遭遇 | 审核稿原值 | 改善值 | 本轮实测 | boundary | 搜索耗时 | 入选解展开 | 墙钟 | 性质 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| O041 | HUNTER_KILLER_NORMAL | 17 | 12 | **17** | None | 20923ms | 36177 | 40799.1ms | 已收敛；12 与本行不同根 |
| O042 | THIEVING_HOPPER_WEAK | 5 | 1 | **5** | None | 14703ms | 6261 | 34766.7ms | 已收敛，策略缺口 |
| O043 | SPINY_TOAD_NORMAL | 12 | 8 | **18** | TimeLimit | 180170ms | 130228 | 199836.9ms | 未收敛，预算受限 |
| O044 | SLUMBERING_BEETLE_NORMAL | 8 | 5 | **5** | None | 12079ms | 4760 | 31358.5ms | 已达目标（跨内存档稳定） |
| O045 | LOUSE_PROGENITOR_NORMAL | 13 | 1 | **1** | None | 9743ms | 5142 | 30124.6ms | 已达目标（内存档条件性） |

O041 旧版记录的 5.3s / 3062 是零药基线成员（`BEAM_WIDTH_PORTFOLIO_MEMBER index=0`、`elapsed_ms=5272`、`expanded=3062`）的值，不是产出实测 17 的那个 solver；该请求 `total_expanded=70196`。

## 三类结论（处置方式不同）

- **A 类（目标已达成）**：O044、O045。O044 达成 5 且跨内存档稳定。**O045 的达成具有内存档条件性**：窄档（No-GC 弃区 24%）产 1 战损 / 第 5 回合 / 1 瓶 `ENERGY_POTION`，宽档（弃区 44%）产 14 / 第 7 回合 / 0 瓶。宽档劣于窄档正是「strategic 净差 8 < 门槛 9 → 整层药水搜索被跳过」判据缺口的直接后果（证据见「同根夹具首次执行结果」）。夹具保持现状（锁认领者档实测值 1），宽档 Failed 保留、不改断言，`description` 已标注条件性。
- **B 类（已收敛但次优）**：O041、O042。搜索跑完（`boundary=None`）仍得不到改善值，且两个改善值本身录于中途残局、不与 `combat_start` 同根可比，因此按同根非退化夹具交付；不能按「同根跑输 12 / 跑输 1」表述。
- **C 类（未收敛）**：O043。搜索把 profile 软预算 180000ms 耗尽仍未收敛，当前预算下无法判定是否存在更优路线。

## A 类：O044、O045

O044 实测 projHP=5、0 瓶、endTurn=6、`unavoidable_hp_lost=5`、`score=10001829972`，与审核稿改善值一致，并在宽内存档重跑中保持全同（同为 `ran=1 compared=1`）。O045 实测（认领者档）projHP=1、endTurn=5、1 瓶省血 19、`score=10001989977`、`won=True`，与包内改善记录同根可比（`:3`，`battleHpLostSoFar=0`、`startTurnNumber=1`）。O045 实测首段 `POMMEL_STRIKE / BURNING_PACT / IMPERVIOUS / BASH`，审核稿改善路线记 `IMPERVIOUS / BURNING_PACT / POMMEL_STRIKE / STOKE`：两者**不是同一组卡的重排**（实测 T1 第 4 张 `BASH`、包内 `STOKE`，卡集不同），能对照的只有终值。O045 达成的条件是零药最优停在 20（strategic 14 ≥ 门槛 9）使药水层被搜索；零药被覆盖度提升改善到 14 后整层药水搜索被跳过，终值退到 14。两项都不含生产补丁。

## B 类之一：O041

运行时序（相对首事件毫秒，`.local/checkpoint-batch/Q010-O041-so/.../runtime-events.json`）：

```
 7296  POLICY_BASELINE          kind=potion_free won=True  hp_deficit=17 enemy_hp=0 boundary=None   (入选零药 primary)
 7384  SEARCH_INTERIM_RESULT    potions=0  projected_battle_hp_lost=17
 7600  POLICY_BASELINE          kind=potion_free won=False hp_deficit=12 enemy_hp=70 boundary=None (portfolio index=4 半成品)
16318  POLICY_BASELINE          kind=potion_free won=True  hp_deficit=26 enemy_hp=0                 (带药层自身候选里的另一条零药)
16318  POLICY_BASELINE_OVERRIDE kind=potion_free won=True  hp_deficit=11
16341  SEARCH_INTERIM_RESULT    potions=1  projected_battle_hp_lost=17
16341  SMART_POTION_GRADIENT layer=1 won=True hp_deficit=-5 saved=16 required=9 acceptable=True selected=True expanded=36177
16343  SMART_POTION_GRADIENT result stop=threshold_met maximum=1 selected_potions=1
```

终态事实（口径与归属；纠偏过程见 #210）：

- 同名字段 `hp_deficit` 有两个口径。`POLICY_BASELINE` 打 `features.CumulativePlayerHpLost`（原始战损，`src/Search/CombatBeamSolver.FinalPlanOrdering.cs:66,199`）；`POLICY_BASELINE_OVERRIDE` 打 `PotionFreePolicyBaseline.HpDeficit`，构造时传入 `StrategicHpDeficit(...)`（`src/Search/CombatSearchCoordinator.Audits.cs:867-871`，构造点另见 `:567-569`）。11 是扣治疗后的 strategic 净值，不能与审核稿 12（原始口径）比较，「已找到零药 11 HP、不劣于 12」不成立。
- 入选零药 primary 双口径闭合：原始 17（`rel 7296`，即 `BEAM_WIDTH_PORTFOLIO_MEMBER index=0`、`expanded=3062`、`battle_hp_lost=17`，动作前缀 `UPPERCUT / SHRUG_IT_OFF / INFERNAL_BLADE`），strategic 11（原始 17 扣治疗后 6）。`rel 16318` 的 26 前缀是 `UPPERCUT / STRIKE_IRONCLAD / SECOND_WIND`，属带药层**自身候选集**里的另一条零药，因 `minimumPotionUses=1` 被 `FinalPlanOrdering` 过滤，与同毫秒的 11 不同源。incumbent 链 `13 → 11(source=no_explicit_potion, turn=6) → 9 → 0 → -5` 印证 11 归属入选 primary。
- 带药层原始战损同样是 17：`saved=16` 全部来自 `BLOOD_POTION` 的一次治疗（`ROUTE_HEALTH ActionIndex=0 Kind=heal Requested=16 Before=61 After=77`），没有减少任何一次受击。原始战损轴 17 = 17，既无收益也无退化。
- 放行带药层的判据链：`Audits.cs:969-974` 的 `IsSmartPotionGradientCandidateAcceptable`（定义 `:1014-1021`，条件 `candidateWon && (!potionFreeWon || hpSaved >= hpRequired || protectsLoot)`）加 `IsBetterPotionPolicyResult`（经 `RouteQualityProjection.PotionPolicy` → `src/Search/RouteQualityPolicy.cs:78-95`，`ComparePrimary` 第 3 键即 `StrategicHpDeficit`，`-5 < 11` 直接判优，战损轴从未被读取）。`src/Search/PotionUsePolicy.cs:111-114` 的 Smart 资格是 solver 内部候选过滤，不在这条跨结果选择链上。`required=9` 因 `BLOOD_POTION` 不在 `src/Search/PotionValuationRegistry.cs:19-34` 表内、取 `src/Search/SolverWeights.cs:99` 默认值。`stop=threshold_met` 取 `acceptablePotionLayerFound`（`:1008`），与战损早停无关（本包 `acceptableBattleHpLoss=0`）。
- **基准本身不同根**：`preflight.json` 的 `index.searchResults` 显示 17 录于 `checkpointId=…:1`（`startTurnNumber=1`、`battleHpLostSoFar=0`、`potionCount=1`），12 录于 `…:4`（`startTurnNumber=3`、`battleHpLostSoFar=6`、`potionCount=0`）；`report.manualProjectionComparison` 的 `stateDifference="field=hp expected={77} actual={61}"` 说明玩家在 T1 没喝计划中的 `BLOOD_POTION`、已偏离路线。12 是从第三回合残局重算的整场预测，不是从 `combat_start` 同根可比的更优解。
- 本轮从 `combat_start` 同根跑出的 30 个动作与该包 `:1` 记录的 17 路线逐 token 相同（仅两枚 `ANGER` 的 occurrence / CardOccurrence 0/1 互换），`restoration`/`nativeState`/`continuationVerified` 均 true：当前源码未复现退化，也没有同根可达的改善目标可判失败。判据链与两种口径取舍的完整推导见 `.local/tool-tasks/q010/O041-analysis.md`。

## B 类之二：O042

`boundary=None`、墙钟 34766.7ms（profile 软预算 300000ms）、请求累计展开 50512（上限 500000，入选解自身 6261）：搜索已收敛但未找到优于 5 战损的路线。portfolio 6 成员中 4 个以 `MemoryHeadroomInsufficient` 跳过，实际参与比较的是 index=0（beam=135）与 index=5（beam=54）；O041 侧为 `members=5 / ran=2 / compared=1`（index=1/2/3 同因跳过，index=4 跑 382 节点后 `NodeLimitNotTerminal` 不参与比较）。

关键反证：O042 的 5 **不出自 portfolio**。时序 `9295 POLICY_BASELINE 16`（index=0）→ `9563 portfolio selected_index=0` → 四个 `POWER_ROUTE` 成员全为 19、`14191 changed=False` → `15085 POLICY_BASELINE 5`（前缀 `1:E,2:C:FEEL_NO_PAIN`）→ `DEFERRED_OPENING_POWER power=FEEL_NO_PAIN hp_lost=5`，即出自不经该门限的 deferred 补查（`src/Search/CombatSearchCoordinator.cs:564-601`）。实测首段 `EndTurn / FEEL_NO_PAIN / SHRUG_IT_OFF / ALCHEMIZE / TAUNT / EndTurn / EndTurn / BRAND`，审核稿改善首段 `EndTurn / TAUNT / SHRUG_IT_OFF / DEFEND_IRONCLAD`（endTurn=6）；改善值 1 录于 `:5`（T3、已损 1），整场等价即 1（其「后续」为 0），不与 `combat_start` 同根。完整推导见 `.local/tool-tasks/q010/O042-analysis.md`。

## C 类：O043

对照实验（同根、同政策，只改外层超时）：

| 运行 | TimeoutSeconds | 入选解搜索耗时 | 请求搜索总量 | 墙钟 | expanded | boundary | projHP |
| --- | --- | --- | --- | --- | --- | --- | --- |
| run 1（`Q010-O043-so2`） | 300 | 180109ms | 180170ms | 199836.9ms | 130228 | TimeLimit | 18 |
| run 2（`Q010-O043-big`） | 900 | 180080ms | 180142ms | 200597.5ms | 127399 | TimeLimit | 18 |

旧版对照表把 run 1 的搜索耗时（180109）与 run 2 的墙钟（200597）并排，混了两个口径；两行搜索耗时其实几乎相同。外层超时放大到 900 秒不改变结果，瓶颈是包内 profile 自身的 `softTimeBudgetMilliseconds=180000`（beam 90、`maxExpandedNodes` 250000、preset High）；节点远未触顶（约 13 万 / 25 万），先耗尽的是时间预算。两运行 `rootContinuationStamp`/`actions`/`snapshot` 逐字节相同、`score` 同为 10001389981，两次运行各自的 `recordedPolicy` 与 `executedPolicy` 也逐字节相同（run1==run2），其中 `profile` 段 18 个字段（beam 90、`softTimeBudgetMilliseconds` 180000、`maxExpandedNodes` 250000 等）recorded 与 executed 逐位相等：同根同政策成立。订正一处旧表述——原写「recorded 与 executed 完全相等」，实际展平后有 24 处叶路径差异（1 项真实值变化：`performancePreset` High→Custom；23 项 recorded 侧未写出的字段在 executed 侧显式默认化）；`profile` 段本身相等，C 类结论不受影响。已排除两处嫌疑（负结果）：1）循环 region 计数——`BEAM_WIDTH_PORTFOLIO result members=6 ran=1 compared=1`，本轮只有 index=0 运行，计数不跨成员累加，`CycleRegionGlobalAdmissionBudget` 覆盖停滞上限与 512 硬上限、`CycleRegionRetentionTransaction` 按 turn 复制预算字典，计数高只因该遭遇的 region 数量本身大；2）portfolio 跳过 5 个成员——`src/Search/BeamWidthPortfolioGate.cs` 的 `FrontierExhausted` 口径为「基线没有被任何上限截断（`SearchBoundaryReason.None`）」，O043 基线以 `TimeLimit` 收束，跳过精炼成员、把预算留给基线是设计行为。结论：18 是时间预算耗尽导致的未收敛，不是候选被错误丢弃；要论证能否恢复 8 需在同根条件下提高该包 profile 软预算对照，但那会使质量对照失去可比性。**2026-10-05 维护者裁定：按「预算边界」结案**，不放宽软预算、不另开特例；同根实测亦证明本批无减弱（合并后 O042 等三项 score 逐位不变，见后节）。O043 维持 C 类边界记录，不设修复也不设达标夹具。

## 五包改善值录制检查点与同根可比性

逐包读 `preflight.json` 的 `index.searchResults`（原报告录制时的求解产物，含 `startTurnNumber`、`battleHpLostSoFar`、`projectedBattleHpLost`、`boundaryReason`）：

| 主题 | 原值录制点 | 改善值录制点 | 起算回合 / 已损 | 同根可比 |
| --- | --- | --- | --- | --- |
| O041 | 17 @ `…:1` | 12 @ `…:4` | T3 / 已损 6 | **否**，中途残局 |
| O042 | 5 @ `…:1` | 1 @ `…:5` | T3 / 已损 1 | **否**，中途残局（整场等价即 1，其「后续」为 0） |
| O043 | 12 @ `…:7`、`…:9`（`boundary=NodeLimit`） | 8 @ `…:9`、`…:11` | T2 / 已损 0 | **否**，第一回合之后的状态 |
| O044 | 8 @ `…:1` | 5 @ `…:3` | T2 / 已损 5 | **否**，中途残局 |
| O045 | 13 @ `…:1` | 1 @ `…:3` | T1 / 已损 0 | **是**，同根可比 |

五包的**原值**一律录于 `startTurnNumber=1`、`battleHpLostSoFar=0`，与 `-CheckpointSelector start` 同根，可直接对照；**改善值**只有 O045 同根。O041/O042 另有玩家偏离的直接证据：`stateDifference` 分别为 `field=hp expected={77} actual={61}`（计划 T1 喝 `BLOOD_POTION` 到 77、实机只到 61）与 `field=hp expected={55} actual={56}`（实机比计划多 1 点血）。也就是说除 O045 外的四个「改善值」描述的是已经打成那样的局面之后还能省多少血，不是从开战起可达的更优世界线。据此夹具目标：A 类锁同根实测终值（5 与 1，只断言战损、结束回合、边界与用药数，不断言路线同构）；B 类改为同根非退化（O041 锁 17 / 1 瓶 / `None`，O042 锁 5 / 0 瓶 / `None` / 主动卖血 4，不断言 12 或 1；两者同根产出与包内 `:1` 原路线逐 token 相同或 26 个动作多重集完全相同（仅相邻顺序差异），含义是「未复现退化」而非「未达目标」）；C 类不设夹具。夹具输入为四个同根夹具 `Q010-O041/O042/O044/O045-SAME-ROOT-*`（`coverage/fixtures/regressions/community/q010-o04{1,2,4,5}-same-root-*.json`），复跑入口见 [测试矩阵](../../../TEST_MATRIX.md)。

## 同根夹具首次执行结果（2026-10-04）

四条夹具串行执行（单实例锁，逐条完成再下一条），均带 `-CleanupInstanceOnExit`、`-CheckpointSelector start`、`-ReplayMode SearchOnly`、`Instant`；断言参数逐项取自夹具 JSON。四份夹具的 `timeoutSeconds=300` 超出 AGENTS.md 第 8 节「单个 unattended 请求默认不超过 120 秒」的口径，登记理由：该值**跟随各包内录制政策的软预算**（`policy.json` 的 `recordedPolicy.profile.softTimeBudgetMilliseconds` 与 `executedPolicy` 相等，五包分别 120000/300000/180000/300000/180000ms，最长 300000ms），不是为本轮排障临时放大——首次执行四次的 launcher 墙钟 28.7–40.0 秒全部远低于 300 秒上限，无一次接近超时；若压到 120 秒，O042/O044（包内软预算 300000ms）就不再是同政策同预算对照。

| 主题 | runId | 判定 | 战损 | endTurn | boundary | 瓶 | 省血/卖血 | score | 墙钟 ms | total_expanded / total_transitions / total_elapsed_ms |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| O041 | `e1ef0c5f460d4128a824ba43adf2ce8f` | Passed | 17 | 6 | None | 1 | saved 16 / required 9 | 10001064970 | 39990.4 | 77995 / 322339 / 20496 |
| O042 | `430a16e96916498b9908ef5af1fd67ff` | Passed | 5 | 7 | None | 0 | sold 4、unavoidable 1 | 10001354974 | 33805.4 | 51366 / 211256 / 13866 |
| O044 | `c1c11f6de94b41808b0f1cfbddbc5be5` | Passed | 5 | 6 | None | 0 | unavoidable 5、sold 0 | 10001829972 | 30367.9 | 11851 / 79116 / 11296 |
| O045 | `d67f8317705a4a9b9d355b8ce3996756`（同参数复跑 `e3632e589f2b4ef4939c49a661a2c931`） | **Failed** | 14 | 7 | None | 0 | saved 0 / required 9 | 10001209968 | 28663.1 | 10109 / 46116 / 9342 |

三条 Passed 的 `score` 与认领者 `SearchOnly` 同根产物（runId `64c2f0401542492ab2ae739ee0b0da5f`、`0202e4fdcb1349a0b870a33fca69a983`、`cf00c6f5a6f745e0a9142a1577e3ddca`、`4d21b9feaf90488aa05ab1a019b86173`）逐位相同，计划路线也**逐 token 相同**（按 `turn:kind:cardId+升级位`、药水取 `potionId`，序列与多重集比较均相等：O041 30/30、O042 26/26、O044 28/28）。O044 另构成跨环境对照：分配墙 2325295000（认领者 1283048684，No-GC 弃区 44% vs 24%），成员覆盖同为 `ran=1 compared=1`，终值、`score`、路线全同，展开 11851 vs 11913、搜索耗时 11296ms vs 12079ms（−6.5%）：质量无退化、耗时无明显增加，符合 AGENTS.md 第 1 节对同条件哨兵的口径。

### O045 失败：判据缺口的可复现实测，不是退化

失败原文「首轮路线使用药水 0 瓶，预期为 1 瓶」，同参数复跑结论一致（同 `score`、同终值）。两侧可证同输入同政策：`rootContinuationStamp` 长 3776 字符、SHA256 均为 `F95E3DA5A9F50626FE74A5FF1302BB12B97690FD4924BBD3E31722E9252AD006`；两次运行的 `policy.json.executedPolicy` 递归展平后逐字段比较，差异数为 0（`request.json` 的 seed/ascension/encounter/act/`enemyCurrentHp`/`checkpointSelector`/`replayMode`/`headlessFastModeForTest`/`stopFlag` 亦全同）。注意区分：同一次运行内 recorded→executed 存在规范化差异（五包 `performancePreset` 一律 →Custom；O044 `includeTurnSetup` true→false、O045 `useNoveltyPortfolio` true→false），但两侧运行的 executed 完全相同，故不影响同政策判定。二进制等价经哈希对账成立：12 次运行（本轮六次：三条 Passed 各一次、O045 两次、O044 复跑一次；认领者六次：O041/O042/O044/O045 各一次加 O043 两次）的 `result.json.mainAssemblyHash` 全部为 `7E306914E10A62FAF737D9B412EEE88773AE0E02DFEF1282A4528FDA1660A610`（字段定义 `src/Testing/Host/UnattendedTestProtocol.cs:530-531`，加载程序集的 SHA256），与 `mods/CombatSolver` 部署件及当前 Release 产物逐位相同；基点 `4533f6bb` 为 2026-10-03T17:34:53Z，认领者六次运行的起点（`result.json.startedAtUtc`）在 2026-10-04T00:56:41Z–01:30:13Z，即均晚于基点 7.4–7.9 小时（按 `startedAtUtc`；含结束时刻则 7.4–8.0）。下面这条因果链取自本轮日志：

1. 本轮墙 2319212440（认领者 1296864492），index=1 的 **beam 60 普通宽度成员**（`bounded_refinement`/`offensive_refinement`/`power_commitment`/`second_rank_band`/`base_score_only` 全 False）不再被 `SkippedMemoryHeadroom` 拦下，于是真的跑了：`ran=3 compared=2`（认领者 `ran=1 compared=1`）；真正的有界精炼成员是 index=5（beam 36、`bounded_refinement=True`），它跑了但以 `NodeLimitNotTerminal` 结束、`compared=False` 未参与比较。
2. 多跑成员把**零药最优**从 20 改善到 14（`POLICY_BASELINE` 本轮 `20, 14, wonFalse:0`，认领者 `20, 0`），选中成员由 index 0（beam 90、战损 20）换成 index 1（beam 60、战损 14）。
3. 战损 14 的 strategic 净差为 8，低于门槛 `firstPaidPotionHpRequired=9`（本轮 `potion_reward=Unknown/-/credit=0`，无替代治疗抵免）→ `MaximumSmartPotionUses`（`src/Search/CombatSearchCoordinator.cs:1009-1052`）返回 0 → Smart 梯度在 `src/Search/CombatSearchCoordinator.Audits.cs:859-865` 以 `stop=no_potion_acceptable maximum=0` 直接返回，**整层药水搜索被跳过** → 终值停在零药 14。
4. 认领者侧零药只有 20，门槛通过，梯度 layer=1 搜出 1 瓶省血 19、整场战损 1 的路线并选中。

即同一输入、同一政策、同一预算下，更宽的内存覆盖反而产出更差结果（14 vs 1）。缺口出在「零药更优 → 判定药水不可能划算 → 不搜药水层」这一步：门槛只看 strategic 净差，被更好的零药结果压低后就否证了整层，而该层里存在终值更优的路线。这是判据口径问题，不属于「源码相对包内路线退化」；按 AGENTS.md 第 1 节本批不改这段判据（影响所有 Smart 政策场景，需独立的「同根质量无退化 + 耗时无明显增加」哨兵），与门限口径一起交维护者定口径。**该缺口已于同日按 W2 修复**（本文件末尾「W2 修复与复跑结果」一节）：修复后本夹具转 Passed。

### O042 顺带闭合两项

同一次执行给出 `unavoidable_hp_lost=1` 与 `sold_hp=4`，相加即断言的整场战损 5，t3 留待对账的「被迫受击 = 1」分量在本次得到 diagnostics 层面确认；该量现已成为协议断言（O042 夹具已带 `expectedInitialUnavoidableHpLost=1`，见「W1/W2 修复与复跑结果」），工作项 3 已销。本轮 O042 也是天然覆盖率 A/B：分配墙 2298331032（认领者 1283575704），成员覆盖从 `ran=2` 升到 `ran=3 compared=3`（多跑 beam 90 与 beam 54，均得 16，beam 54 因 `NodeLimit` 结束、expanded=1485），`POWER_ROUTE_PORTFOLIO result selected_hp_lost=16 changed=False`，`DEFERRED_OPENING_POWER power=FEEL_NO_PAIN hp_lost=5` 照旧触发，终值、`score`、路线都不变 ⇒ 覆盖度提高后零药层没有搜出优于 16 的结果，5 仍来自不经门限的 deferred 补查，t3 未验证项 1 就此闭合，门限口径不是 O042 终值的原因。

### 退出码注意

带 `-CleanupInstanceOnExit` 时实例目录 `.local/headless-instances/<实例>` 偶尔删不掉（`game\data_sts2_windows_x86_64\0Harmony.dll` 句柄未释放，`tools/testing/headless-runtime.ps1:271` 在 `run-unattended-test.ps1:1458` 的 `exit 0` 之后的 finally 抛出），启动器因此返回退出码 1；这不影响 `result.json` 的 `Passed` 判定。本次四条退出码均为 1（O045 同时是断言失败）。残留实例目录需手动删除后再跑下一条，本轮结束时 `headless-instances` 已确认清空。

## 门限口径偏差：工作项登记（2026-10-05 维护者裁定：本批不扩范围，W1 已修，其余保持登记）

O041 与 O042 的共同线索是 beam 宽度组合的精炼成员几乎全部被内存门限拦下（O041 `members=5 ran=2 compared=1`，O042 `members=6 ran=2 compared=2`）。逐位复算后 `src/Search/BeamWidthPortfolioGate.cs:86-118` 的算术与它拿到的输入一致，**判定为环境保守而非逻辑过严**：六条判据按序 ProvenZeroDamage → FrontierExhausted → 时间份额 → 节点余量 → 时间余量 → 内存余量（`:98-116`，第 6 条在 `:111-116`），成本按 `ceil(base×w×3/(baseW×2))` 外推（`:63-71`），墙由 `src/Runtime/SearchGcPolicy.cs:2098-2101` 的 `min(sob/5*4, region/4*3)` 算得 O042 1283575704、O041 1241302252，与日志逐位吻合。输入侧三处值得维护者决定是否单独立项：

1. **成本侧与余量侧用了不同起点的分配量。** 门控用基线成员**自己那一段**的分配增量外推每个成员的成本（`src/Search/CombatSearchCoordinator.BeamPortfolio.cs:120,139-140`），却拿 `SearchMemoryPressureSignal.RemainingBytes`（`src/Runtime/SearchMemoryPressureSignal.cs:113-117,157-177`，**自区域配置时刻起**的累计分配）当余量。O042 基线自报 `allocated_delta=1063386616`，是整面分配墙 1283575704 的 82.9%，而反推出的判拒时刻余量只有约 0.64–1.06 GB（`remaining ∈ [638031970, 1063386616)`：54 宽放行给下界、90 宽拒绝给上界）⇒ 凡宽度 ≥ 基线宽度的成员必然被拒，这是结构性而非偶发内存紧张。O041 同构：60 宽成员外推成本 1229477958 为其墙 1241302252 的 99.05%，差 11824294 字节被拒（`remaining ∈ [491791184, 1229477958)`）。
2. **有界精炼成员被显著高估。** `src/Search/BeamWidthPortfolio.cs:284-285` 把它的节点预算压到 `totalExpanded / 8`（O042 为 7026/8 = 878，与日志 `nodes=878` 一致），实际只分配 113047992 字节，而门控按宽度线性外推给它的预算是 638031970 字节，高估约 5.6 倍（638031970/113047992=5.64）；第 6 条实际只在「宽度 ≥ 基线」上生效。
3. ~~**`unavoidable_hp_lost` 缺协议断言字段。**~~ 已修（W1/W2 轮次，见下节）；以下保留修复前的登记原文。无人测试的 `ExpectedInitial*` 只有 `soldHp`/`soldHpAtMost`/`hpLostAtMost` 一类，被迫受击分量只出现在 `RESULT` 诊断行（`src/Runtime/SolverDiagnostics.cs:209`），「战损构成」这类断言只能靠人工对账；若要机器化，需要新增 `ExpectedInitialUnavoidableHpLost`，属 `src/Testing` 生产协议改动，不在本批范围（该字段已于同日 W1/W2 轮次落地）。

4. **未映射键在请求反序列化中被静默忽略（登记为仓库级工作项，2026-10-05 维护者裁定本批不动）。** `UnattendedTestRequest`（`src/Testing/Host/UnattendedTestProtocol.cs:17`）没有设 `JsonUnmappedMemberHandling.Disallow`——全仓只有 `src/Testing/Host/GeneratedCombatScenario.cs:19,27` 设了。后果：任何夹具里写了、而该二进制协议中不存在的 `ExpectedInitial*` 键，会被反序列化直接丢弃、不报错，于是旧二进制对 `expectedInitialUnavoidableHpLost` 的 `Passed` 属**空断言通过**（prefix 类构建根本没有该属性），读作「断言生效」就是错的。不加 `Disallow` 是因为它全局影响 476 份存量夹具——其中 `description` 本就是有意不映射的键。若要机器化，需要的是给特定字段族加显式校验，而不是打开全局严格模式。

两者的直接环境诱因是 NoGC 区域被系统内存压力拒开：`GC_NO_GC_REGION_DECLINED percent_of_configured=24`，配置 16 GB 只能保留 3.95 GB → `IsNoGcRegionBudgetWorthEntering` 主动弃区（`src/Runtime/SearchGcPolicy.cs:520-545`）→ 中途区域重建后墙降到 1.28 GB（`physical_load` 8.6→10.6 GB、`system_limit` 12.56 GB）。**同一诱因在本轮以反方向出现**：弃区比例 44%、墙升到 2.3 GB，于是 O045 变差而 O041/O042 不变，说明门限松紧在当前判据下都不单调。本轮**不改生产代码**，理由：AGENTS.md 第 1 节禁止用扩大 Beam、节点、时间或 No-GC 预算掩盖问题，而门限校准属口径改动、需要独立的「同根质量无退化 + 耗时无明显增加」哨兵；且 O042 的最终结果并不出自被拦的精炼成员，而出自不经该门限的 `DEFERRED_OPENING_POWER` 补查（`src/Search/CombatSearchCoordinator.cs:564-601`），放宽门限对本批两个主题的收益未经证实。

## 本机 headless 环境记录

1. 工坊默认路径不在 `run-checkpoint-batch.ps1`：该脚本第 11 行的 `-RitsuWorkshopRoot` **没有默认值**，只在非空时才拼进 `--ritsu-root`。真正的默认值在 `tools/replay/CheckpointTool/BatchRunner.cs:324`，拼成 `<game-root>/../../workshop/content/2868840/3747602295`。本机 RitsuLib 实际在 `E:\Slay the Spire 2\mods\STS2-RitsuLib`，省略 `-RitsuWorkshopRoot` 会得到 `invalid_archive`。（`tools/testing/run-unattended-test.ps1:9` 另有硬编码 `D:\Steam` 默认，是第二处入口。）本机实际加载的 RitsuLib 程序集版本是 **0.6.2.0**（`mod_manifest.json` 的 `version: 0.6.2`）；此前写的「RitsuLib 0.111.0」是 `lib` 目录按游戏版本匹配的路径名，游戏本体为 v0.111.0。该目录与包内记录的 RitsuLib 各程序集版本一致，但 `moduleId` 全部不同（`policy.json` 的 `modEnvironmentComparison` 因此报 `build_changed`）。
2. 三处首跑 `process_crash` 的原因**不同**，不都是 MemoryCleaner：`Q010-O043-so` 与 `Q010-O044-so` 的 `launcher-error.log` 指向 `run-unattended-test.ps1:466`，即 launcher.lock 争用（同一 headless 实例被并发占用），与 MemoryCleaner 无关；只有 `Q010-O045-searchonly` 首跑是 `run-unattended-test.ps1:422` 硬校验 CombatSolver.dll / manifest / MemoryCleaner.exe 三件产物失败。该 exe 是 `net48` 项目，需 .NET Framework 4.8 引用程序集；本机无 VS / SDK / winget，改用 NuGet `Microsoft.NETFramework.ReferenceAssemblies.net48` 解出引用程序集安装后构建通过（0 警告 0 错误），未改动仓库任何文件。
3. 工具链：游戏 v0.111.0、RitsuLib 程序集 0.6.2.0、.NET SDK 10.0.400，headless 全程无需打开游戏窗口。

## W1/W2 修复与复跑结果（2026-10-04，Refs #210）

修复一处：`src/Search/CombatSearchCoordinator.Audits.cs` 梯度入口在 Smart 政策下另按必然受击轴（`SolverResult.UnavoidableHpLost`，口径 `src/Search/CombatBeamSolver.Phases.cs:541-545,833`）求一份 `MaximumSmartPotionUses` 配额并取两者最大值，另输出 `SMART_POTION_GRADIENT axis_widened` 诊断。单调性由构造直接成立：新上界取两轴各自配额的最大值，`max(·) ≥ 旧上界`，故只会放宽搜索面、旧行为是被包含子集；两轴之间没有大小关系：净差由 `ActEndingBossPolicy.StrategicHpDeficit`（`src/Search/ActEndingBossPolicy.cs:139-148`，调用点 `CombatSearchCoordinator.cs:966-984`）算作 `cumulativeHpLost + maxHpDeficit − 持久化回收治疗 + DeathSavePremium − StrategicHpCredit`，比必然受击多出 `maxHpDeficit` 与 `DeathSavePremium` 两个加项，故可大可小（反例：O042 的 `potionFree` 净差 5 > 必然受击 1，runId `430a16e9…`、`0202e4fd…` 与 `b973275b…` 的 `POLICY_BASELINE kind=potion_free hp_deficit=5` 对上同一次 `RESULT … unavoidable_hp_lost=1`），所以不能拿「净差 ≤ 必然受击」当前提；层内是否采纳仍由 `RouteQualityPolicy`/`IsBetterPotionPolicyResult` 裁决，beam/节点/时间/No-GC 预算与比较规则一律未动。完整调用链与新旧行为差异推导在 `.local/tool-tasks/q010/w2-fix-rationale.md`。

四条同根夹具复跑（约定：`score` 取 `comparisonQuality.score`；「墙钟」取 `timings.json` 各阶段之和；`limit` 取 `GC_SEARCH_ALLOCATION_LIMIT`）：

| 主题 | 修复前 runId / 判定 / score | 修复后 runId / 判定 / score | 修复后终值 | 墙钟 ms | 本轮 limit | axis_widened |
| --- | --- | --- | --- | --- | --- | --- |
| O041 | `e1ef0c5f…` Passed / 10001064970 | `4e7f8e7e284a447195163b3741dc3f02` Passed / 10001064970 | 17 / T6 / 1 瓶 saved 16 | 40901.3 | 1183750724 | 未触发 |
| O042 | `430a16e9…` Passed / 10001354974 | `5743f2ccc3de429584df1d0e92a538fd` Passed / 10001354974 | 5 / T7 / 0 瓶、sold 4、unavoidable 1 | 34975.6 | 1189524716 | 未触发 |
| O044 | `c1c11f6d…` Passed / 10001829972 | `f6633b40430b49bd9d0c3e159d4e23ab` Passed / 10001829972 | 5 / T6 / 0 瓶 | 32780.8 | 1276120984 | 未触发 |
| O045 | `d67f8317…` **Failed** / 10001209968（projHP 14） | `095e69ee275b4acca62f35e9e735c869` **Passed** / 10001989977 | 1 / T5 / 1 瓶 saved 19 | 31295.2 | 1307413060 | hp_deficit=14 unavoidable=19 maximum 1→2 |

四条 `score` 与修复前逐位相同，O045 的 `score` 现与认领者窄档产物 `4d21b9fe…` 逐位相同。O045 第 2 层被真实搜索（`saved=20 required=27 selected=False`）后仍选第 1 层，说明轴放宽只扩大搜索面、不改变采纳判定。O041/O042/O044 的 `axis_widened` 未触发，即修复前行为确为被包含的子集。构建 0 警告 0 错误。另有三次 O045 复跑记录在 `.local/tool-tasks/q010/w2-fix-rationale.md`（`57b3c995…`、`dfc63c07…`、`567b87d9…`，全 Passed）：这三次**产物未单独留存**，属二手登记、无产物可复核，不作为独立证据计入。

W1（同一轮次）：`BeamWidthPortfolioGate.cs` 的内存判据改为成本侧与余量侧同一起点——基线新增 `AllocatedBytesFromOrigin`（与 `memberAllocated` 同一时刻采 `SearchMemoryPressureSignal.AllocatedBytes`），基数取两者较小值；`RejectRefinement` 追加可选参数 `memberMemoryCostBytes`（默认 `null` 即旧口径，`EstimateMemberCost` 原语义与时间轴未动），有界精炼成员改按 `totalExpanded / 8` 的实际配额用新 `EstimateQuotaCost` 投影。那一次「覆盖不变」的观测（index=0/1/5 `ran=True`、index=2/3/4 `skipped=MemoryHeadroomInsufficient`）后来被证明是诊断未开 + 该档余量太小所致，**W1 的决策翻转已在 t14 attempt 2/3 取到**，见下一节「W1 决策翻转现场与界断言落盘」。门控检查工具未改任何断言即绿：隔离副本（同 `Program.cs`、同生产源文件）输出 `BEAM_WIDTH_PORTFOLIO_OK checks=108`；顺带查明该工具的 `run.py` 本机早就构建失败——它用花括号配对抽取 `IsBetterPotionPolicyResult`，而该重载现为表达式体（`; < 0;` 结尾），抽取越界吞掉下一个方法，且拷贝清单缺 `RouteQuality*`；`git stash` 反证与本次改动无关，本批未修改 `run.py`（不在许可范围）。

unavoidable 断言已全链接通并用正反对照验证：断 `expectedInitialUnavoidableHpLost=1` 时 Passed（`af3fdabed95b4ac6afa3c36162f66550`，`solverMetrics.unavoidableHpLost=1`），断 9 时 Failed（`1c332f4748be4c86a2cb104b0cbd4ba3`，失败信息「首轮路线必然受击为 1，预期为 9。」），证明确实执行到断言体。两处需更正我先前的错误陈述：①「`.sh` 连同族 `expected_initial_sold_hp` 都缺」不成立，`.sh` 一直有 `expected-initial-sold-hp`（kebab 命名，`run-unattended-test.sh:169-170`），我当时按下划线拼写去 grep 才误判，本轮只新增了两侧都缺的 `expected-initial-unavoidable-hp-lost`；②`.sh` 的透传与语法未经执行验证——本机无 bash（`Get-Command bash` 为空），只静态核对了 `option_to_field` 的 kebab→camel 规则会把该选项映射成协议属性名 `expectedInitialUnavoidableHpLost`。

## W1 决策翻转现场与界断言落盘（2026-10-04，t14 attempt 2/3，Refs #210）

约定：`limit` 一律给字节原值并附 MiB（=字节/1048576）；`cov` = `BEAM_WIDTH_PORTFOLIO result` 的 `members/ran/compared/selected_index/selected_beam`（按成员计数）；耗时三口径分列——`searchMs`=`solverMetrics.elapsedMilliseconds`（原始搜索）、`totalMs`=`totalElapsedMilliseconds`（同请求累计）、`wallMs`=launcher 墙钟（含启动/建局/清理），互不混用。对照二进制用 `git archive` 导出到 `.local` 后同 `local.props` 独立构建（各 0 警告 0 错误），经 `-CombatSolverBuildDir` 注入；`mainAssemblyHash` 只作「该 run 加载了哪个件」的身份记录：无修复 `8184A000439814AD…3F46E2`、修复后 `BD84AE5C69DFC45E…105C24`。同根权威证据用 `search-result.json.rootContinuationStamp`（O042 对两侧 3121 字节、FNV 一致；O045 对两侧 3776 字节、FNV 一致）。

翻转现场（GATE 行原值代入；宽度规则 `ceil(base×w×3/(baseW×2))`，配额规则 `ceil(base×quota×3/(baselineNodes×2))`，`base = min(baseline_allocated, baseline_from_origin)`）：

| 成对运行 | GATE 原值 | 无修复侧（宽度规则） | 修复后侧（配额投影） | `remaining_bytes` | 判定 | 成员实测 |
| --- | --- | --- | --- | --- | --- | --- |
| `0945a847`（head，limit 1904955288=1817 MiB）/ `0265fa83`（prefix，1876538604=1790 MiB），O042 | alloc 1074372800、origin 641651968、baseline_nodes 7026、baseBeam 135、决策时累计 expandedByMembers=11886 | ceil(1074372800×54×3/(135×2))=**644623680** | quota=floor(11886/8)=**1485** → ceil(641651968×1485×3/(7026×2))=**203427236** | 555298448 | **翻转**（成本降 3.17×） | head beam54 `ran=True nodes=1485`；prefix 同成员 `skipped=MemoryHeadroomInsufficient nodes=0`；`cov` head 6/3/3 vs prefix 6/2/2，`score` 两侧同为 10001354974 |
| `8215083c`（head，1996151364=1904 MiB）/ `b3b6d729`（prefix，1979522968=1888 MiB），O045 | alloc 1155971760、origin 756677064、baseline_nodes 5714、baseBeam 90、决策时累计 expandedByMembers=8986 | ceil(1155971760×36×3/(90×2))=**693583056** | quota=floor(8986/8)=**1123** → ceil(756677064×1123×3/(5714×2))=**223070094** | 612649268 | **翻转**（3.11×） | head beam36 `ran=True nodes=1123`（以 `NodeLimitNotTerminal` 结束、`compared=False`）；prefix `skipped=MemoryHeadroomInsufficient` |

剩余被拦成员（`203`、`135+band`、`135+base`，O045 侧为 `135`、`90+band`、`90+base`）都不是有界精炼成员、本就不走配额投影；换成同一起点基数后成本虽降（O042 `203` 由 2423307538 降到 1447281662），仍高于 `remaining_bytes` ⇒ 属真实余量不足，不在 W1 射程内。O042 那轮的有界成员（`54+offense+bounded`）`memory_cost=208599929` 仍 > 当时 `remaining=205241764`；该实测成本是累计配额口径的旁证——它只由 `quota=floor(11886/8)=1485` 复现（`ceil(657967720×1485×3/(7026×2))=208599929`），按「只算基线节点」的 878 会得 123333830、与实际不符：能否翻转取决于同档余量与配额的比值，不是恒定放行——这正是上一轮「明细逐位相同」的成因。

**目标有效性（同机可达最宽档成对 A/B，1904 vs 1888 MiB、同根 stamp 一致）**：无修复件 `b3b6d729` 复现 `#210` 的形状——`proj=14 / endTurn=7 / 0 瓶`、`score=10001209968`、诊断行 `stop=no_potion_acceptable hp_deficit=8 maximum=0`（整层药水未搜）；修复后 `8215083c` 同一根给 `proj=0 / 1 瓶 / score=10002069975`（更高）、`axis_widened hp_deficit=8 unavoidable_hp_lost=13 maximum_from_deficit=0 maximum_from_unavoidable=1`。另一条无修复件同形状样本 `ea117112`（limit 2169395780=2069 MiB、弃区 41%）当时按原样保留的 `expectedInitialPotionCount=1` 当场抛「首轮路线使用药水 0 瓶，预期为 1 瓶」而 Failed ⇒ 用药轴未被放松。更宽内存档未取到的原因见「未验证项」（其中也说明原先设想的「靠设置分配上限环境变量复跑更宽档」这一前提不成立，不得据此复跑）。**「无修复件的 Passed」不可读成界断言生效**：见工作项 4（未映射键静默忽略）。

界断言落盘后的首跑（参数由夹具文件逐字段映射进 CLI，argv 留存在 `.local/tool-tasks/q010/fixture-effect/*.argv.txt`）：O044 `4e17b33a1367462c906cca1a83fc4aae` Passed，limit 1232616004=1176 MiB、弃区 23%，`proj=3`（≤5）、0 瓶、`endTurn=7`、`score=10001949972`；O045 `7d8a2799942d459390c12a1b87142a3d` Passed，limit 1235238808=1178 MiB、弃区 23%，`proj=1`（≤1）、1 瓶、`endTurn=5`、`score=10001989977`（与认领者窄档 `4d21b9fe…` 逐位相同）、`axis_widened hp_deficit=14 unavoidable_hp_lost=19 maximum_from_deficit=1 maximum_from_unavoidable=2`。两次 `mainAssemblyHash` 均为 `BD84AE5C69DFC45E…105C24`。

耗时成对（同机、同并行度、同 Instant 预热与同诊断模式）：`0945a847` head searchMs=986/totalMs=15364/wallMs=60728、exp=51366/trans=211256；`0265fa83` prefix 946/14166/58849、exp=49881/trans=205389 ⇒ 多放行一个成员带来工作量 +2.9%、搜索耗时 +4.2%，在同主题跨轮波动（966/974/986/1242ms）内。O045 侧 head `8215083c` 1539/10648/54641 vs prefix `b3b6d729` 991/9554/56869：prefix 更「快」的成因是整层被否证且产出 `proj=14` 劣化，不作为性能优势读取；撞上限/被截断样本一律同时核 `exp`/`trans`/`cov`/`limit`。

## 合并 origin/main 后的复跑（2026-10-05，Refs #210）

上游推进到 `5d28a1cf`（0.49.2/0.49.3 发版、Mono 内存回收、BaseLib 生成牌回归、世界线批次规模调整、审核稿移入 `docs/archive/`）。合并提交 `7cd38b55`：仅 `DEVELOPMENT_NOTES.md` 一处占位文案冲突（取本批章节）；`Audits.cs` 自动合并，W2 修复点 `:100-105`/`:864-877` 完好，上游改动在 `:1116` 之后与 W2 无重叠。构建 0 警告 0 错误；相对上游 head 的本批生产 diff 仍为 8 文件 +121/−9。

四份夹具同根重跑全部 Passed（新二进制 `A29416DE3903…`，哈希仅作 run 身份）：O041 `b48801e8…`（score 10001064970 逐位同、17/6/1 瓶）、O042 `09320b03…`（10001354974 逐位同、5/7/0 瓶、`unavoidable=1` 由新协议字段机器断言）、O044 `d1ec15e5…`（proj=3 ≤5、10001949972）、O045 `8aaa687f…`（**零药轴自身 proj=0 ≤1**、score 10002069976 高于带药路线、第 6 回合）。

两处合并后变化：① O045 的 `expectedInitialPotionCount=1` 等值断言被移除——零药达 proj=0 后，等值会把更好的路线判 Failed，正是质量界政策禁止的「锁环境快照」；判别力由 `projAtMost=1` 承担（缺陷形貌 14 仍会踩破）。该次 `axis_widened` 未触发：`no_potion_acceptable` 成立是因零药已最优而非药水层被否证，与 W2 互为印证。② O042 本轮 `ran=2`、无 GATE 行，翻转现场证据仍取 2026-10-04 的 `0945a847`/`0265fa83` 成对记录，未重复采集。

门禁与文档：`TEST_MATRIX.md` 上游自身已占 200 行阈值，本批 Q010 节（46 行）整体移入 [历史卷 15](../../testing/volume-15.md)，本页与 `TEST_MATRIX` 留短入口；审核稿链接随上游迁移更正；`verify-documentation` 由 errors=3 降为 **errors=0**（同批草稿入 archive 后豁免规则生效，非本批放宽）。

## 二次合并（`c4ae4367`）发现的 W1 引发退化与修复（2026-10-05，Refs #210）

上游再次推进（`a6c241b2`→`c4ae4367`，0.49.4 发版、额外回合与共享损血剪枝、第三方适配登记），merge 提交 `2be865e0`。冲突四处（`DEVELOPMENT_NOTES` / `TEST_MATRIX` / `archive/testing/README` / `volume-15` 编号撞车），处理：上游已占 14/15/16 卷，本批 Q010 卷由 14 顺延为 **17**，上游各卷原样保留；两侧新增章节全部保留。

**合并后重跑四份夹具，O045 由 Passed 变 Failed（proj=12），定位为本批 W1 引进的退化，已修：**

- 上游单独行为（`a6c241b2` 构建）：成员 `ran=2 compared=1`，选中 index=0（beam=90，净差 22）⇒ 门槛通过 ⇒ 梯度搜到 `hp_deficit=-6 / saved=22` ⇒ **proj=0, 1 瓶, Passed**。
- 本批 W1 放行 beam=60 成员后：`ran=3 compared=2`，选中 index=1（净差 12、整场战损 12）⇒ `MaximumSmartPotionUses` 以**净差 6**（< 门槛 9）与**必然受击 0** 两轴都算出 0 ⇒ `stop=no_potion_acceptable maximum=0` ⇒ 整层药水搜索被跳过 ⇒ **proj=12, 0 瓶, Failed**。
- 根因：门槛的两条轴在「更好的零药解」上会一起塌到门槛以下——净差被既有治疗扣低、必然受击在已减伤路线为 0——而这场仗仍有 12 点战损可省。W2 当初只补了必然受击轴，兜不住本例。
- 修复（`Audits.cs` +20/−14）：门槛取三轴更宽者，新增 **`ProjectedBattleHpLost`（整场预计战损）** 轴，梯度入口与停止点两处同改；预算（beam / 节点 / 时间 / No-GC）零改动，比较规则未动，仍只放宽「跑不跑药水层」。
- 修复后实测：O045 **Passed，proj=0、0 瓶、score=10002069967**（比上游自己的 `proj=0, 1 瓶` 更省一瓶药）。

## 未验证项

- ~~**W1 的覆盖效果未被观测到变化**~~ **已被 attempt 3 的 GATE 诊断闭合（见「W1 决策翻转现场」节）**：先前那次 O042 运行的成员明细与修复前逐位相同（index=0/1/5 `ran=True`、index=2/3/4 `skipped=MemoryHeadroomInsufficient`、`ran=3 compared=3 selected_index=0`），当时被拦的三个成员都不是有界精炼成员、本就不走配额投影，而 `BEAM_WIDTH_PORTFOLIO_GATE` 诊断需 `MeasurePhasePerformance` 开启才输出、那轮未开，所以其缺席不构成证据——那一轮确实无法判 W1 有效或无效。后续按该开关重跑并取得翻转现场，该缺口已闭合；W1/W2 代码均已实现（见末节），因此「本批无生产代码改动」这句只对 t1–t12 成立。另记两条门禁事实：`tools/testing/checks/BeamWidthPortfolioChecks/run.py` 在本机现在就构建失败（生成的 `Ordering.cs(96-99)` 缺 `CombatRootSnapshot`/`SearchPolicySnapshot`/`SolverResult`，CS0246 ×4），已用 `git stash` 反证为既有状态、与 W2 无关（该脚本只从 `CombatSearchCoordinator.cs` 与 `SolverProgress.cs` 抽取，不含 `Audits.cs`）；`verify-refactor-boundaries.ps1` 依赖 `rg`，需把 `rg` 放进 PATH 才能跑，本批实测 `REFACTOR_BOUNDARIES_OK search_files=247`。
- 「未改目标哨兵」这条已在本批补做（t14）：`tools/search/GeneratedCombatScenarios/specified.json` 解析为 SILENT / BYRDONIS_ELITE / seed=SPECIFIED-COMBAT-001 / A10（本批完全未涉及的遭遇），同机同条件串行 7 次（修复后 4 次 `aab329e1`、`5084dc22`、`8903e4e6`、`beee8551`，无修复件 3 次 `44116b07`、`82dce5e4`、`bdf5aee8`），`score=10001019984`、`projHP=16`、`endTurn=4`、`pot=0` 七次逐位相同 ⇒ 未改目标的路线质量零差异。该哨兵的 status 层 6 Failed / 1 Passed，且两个二进制各有 Failed 与 Passed，失败原因全部是同一条环境断言「No-GC 搜索没有按配置建立并保留战斗级区域」（Failed 样本 `noGcRegionBudgetBytes=0`、唯一 Passed 的 `5084dc22` 为 noGcRegionBudgetBytes=2601324390 字节（2481 MiB）/ limit=1734216260 字节（1654 MiB）），与修复无关，属本机 No-GC 建立不稳定 ⇒ 状态不能当通过依据，已另列为未验证项。「严格关闭内存门限」的同根对照仍未单独构建，由同根成对 A/B 替代（head 与无修复件同 stamp、同政策，见「W1 决策翻转现场」节）。成对耗时只到 SearchOnly 原始搜索（`elapsedMilliseconds`）、同请求累计（`totalElapsedMilliseconds`）与 launcher 墙钟三个口径，未做同一进程内的严格交替计时；可见 FPS/帧时间结论需要可见 Steam 会话，本批未运行。
- O045 的宽档失败只在这台机器复现（分配墙约为认领者的 1.8 倍）。未在认领者那档余量下重跑以证明「余量窄即 Passed」——该侧证据来自认领者产物，非本轮执行。**原先设想「通过设置分配上限环境变量复跑更宽内存档」这一前提不成立，已撤回，不得据此复跑**：`GC_SEARCH_ALLOCATION_LIMIT` 不是可调环境变量，全仓只有 `src/Runtime/SearchGcPolicy.cs:2130` 与 `src/Runtime/CombatDiagnosticJournal.cs:132` 两处命中，都是日志前缀，没有任何 `GetEnvironmentVariable` 读它；档位由 `limit = min(configured, headroom + reusableHeap)`（`SearchGcPolicy.cs:2639-2647`）与系统上限 `HighMemoryLoadThresholdBytes/100×95`（`:2649-2657`）推导，日志里 `limit` 恒为 `region_budget` 的 2/3。能抬这一侧的唯一测试参数是 `-NoGcRegionBudgetGigabytesForTest`（默认 16 GB），那是扩大预算、本批硬约束禁止，未使用。改为**同机可达最宽档成对 A/B**并已取到（1904 vs 1888 MiB、同根 stamp 一致，见「W1 决策翻转现场」节）：无修复件 `b3b6d729…` 在 `limit=1979522968` 字节（1888 MiB、弃区 37%）复现 `proj=14 / endTurn=7 / 0 瓶`、`stop=no_potion_acceptable hp_deficit=8 maximum=0`；修复后 `8215083c…` 在 `limit=1996151364` 字节（1904 MiB、弃区 38%）同一根下 `proj=0 / 1 瓶 / score=10002069975`。本机物理内存 13.7 GB、可用 7.5–8.6 GB，三次尝试内弃区最高 41%，故更宽档仍未取到，如实保留为未验证。修复前宽档退化为 14 是可复现事实（`d67f8317…`、复跑 `e3632e58…`、`ea117112…`）。
- 修复前的二进制等价已由那 12 次运行（t5 六次 + 认领者六次）的 `mainAssemblyHash` 对账确认，同一哈希 `7E306914E10A62FA…60A610`；该结论只覆盖这 12 次。W2 修复后另七次运行的说法按产物校正为：**四次有产物**（`.local/tool-tasks/q010/w2-runs/` 下 O041-after `4e7f8e7e`、O042-after `5743f2cc`、O044-after `f6633b40`、O045-after `095e69ee`，各自 `result.json.mainAssemblyHash` 前缀均为 `1F130987AC1827DD…`）+ 三次仅登记无产物（见上一节那条），故哈希对账的可复核范围是这四次，正好证明修复前后加载的是不同二进制、行为差异不是同码抖动；对账明细在 `.local/tool-tasks/q010/w2-fix-rationale.md`。同一份源码在 18:41 重建后哈希变为 `AE310F74…`，说明本仓库 Release 构建非字节可复现，因此不能拿哈希当「同源」证据，只能拿各 run 自带的 `mainAssemblyHash` 与其对应源码状态。
- O042 的「被迫受击 = 1」已成为机器断言：`ExpectedInitialUnavoidableHpLost` 全链接通（协议 → `UnattendedSolverMetrics.UnavoidableHpLost` → `Writer` → ps1/sh 参数 → `SolverPolicy` 断言体），正反对照为断 1 Passed（`af3fdabed95b4ac6afa3c36162f66550`）、断 9 Failed（`1c332f4748be4c86a2cb104b0cbd4ba3`）。**更正我此前的一条错误陈述**：本文件与 DEVELOPMENT_NOTES 曾写「`.sh` 连同族 `expected_initial_sold_hp` 都缺」，实测不成立——`.sh` 一直有 `expected-initial-sold-hp`（kebab 命名，见 `run-unattended-test.sh:169-170`），我当时按下划线拼写去 grep 才误判；两平台真正的差异只是本轮新加的 `expected-initial-unavoidable-hp-lost` 需同时补两侧（已补）。
- `.sh` 的参数透传与语法未经执行验证：本机无 bash（`Get-Command bash` 为空），只做了静态核对（选项名按 `option_to_field` 的 kebab→camel 规则映射为 `expectedInitialUnavoidableHpLost`，与协议属性名一致）。
- 五个主题均未执行 `DeploySolver` 原生部署，未做成对耗时对照，部署对照不属于本批收口条件。维护者口令为「做完之后跑回归哨兵，证明无退化就可以，开始做，并收口」，本批随收口提交（提交链最后一条带 `Closes #210`）把 PR #211 转为 Ready for review；原先「等口径答复」的 Draft 措辞随之移除，`6` 的三项口径与工作项 4（未映射键在请求反序列化中被静默忽略 → 旧二进制对 `expectedInitialUnavoidableHpLost` 的 `Passed` 属空断言通过）作为登记项保留在 PR 正文，由维护者在 review 中处置。未运行可见 Steam 会话，无 FPS 与帧时间结论（headless 数据不替代可见性能口径）。
- 定位报告 `.local/tool-tasks/q010/O041-analysis.md`、`O042-analysis.md` 与验收汇总 `.local/tool-tasks/q010/verification.md` 按仓库规则在任务结束后清理，结论已全部并入本文件与本 PR 正文；证据以登记的 runId 与 `coverage/evidence/test-evidence.json` 为准。
