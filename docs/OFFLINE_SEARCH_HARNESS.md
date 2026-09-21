# 离线搜索宿主

`tools/OfflineSearchHarness/` 是一个普通的 .NET 9 控制台程序：它加载 `sts2.dll` 但**不启动 Godot
引擎**，用游戏自己的核心层建出一场战斗、推进到玩家第一回合，再在同一个进程里调
`CombatRootSnapshot.Capture` 与 `CombatSearchCoordinator.Solve`（或单次 `CombatBeamSolver`）跑一次
固定预算搜索，把指标、选中路线和搜索策略写成 JSON。

它解决的是批量测量的成本问题：游戏内无人测试每一根都要起一次 Godot 进程，宿主不用，几十根到上百根
的宽度/预算/保留规则对照可以在一台机器上连着跑。**它不是 `docs/HEADLESS_TESTING.md` 的替代**——
正确性验收仍然走无人测试，宿主只覆盖「同一份 DLL、同一个根、只换搜索参数」这一类测量。

开发分支另有显式 `--multiplayer-contracts`，运行原生托管结算与模拟状态的局部合同，随后退出。它增加双玩家、动画时钟绕过和多人准备人数条件；不建立网络，也不验证可见 UI。其证据范围、输入与命令见 [多人军师](multiplayer-advisor.md)。普通性能模式的用途不变。

`--multiplayer-start-contracts` 单独验证按钮处理函数到 Runtime 的手动启动链路：双玩家 Host/Client 身份、本机记录归属、等待原生动作、启动失败反馈及重试。它用隔离宿主替代 Godot 渲染/分发和完整录像负载采集，不建立真实网络；不能与 `--request` 或 `--multiplayer-contracts` 合用。最小输入为 `--encounter FOGMOG_NORMAL --beam 12 --nodes 100 --budget-ms 1000 --dop 1`，产物另含 `manual-startup-events.txt`。这不是可见 UI 或多人问题包回放验收。

`--multiplayer-strategy-contracts` 验证每回合最多 3 HP 换输出的多人目标，最小输入为 `--encounter FUZZY_WURM_CRAWLER_WEAK --beam 2 --nodes 100 --budget-ms 1000 --dop 1`。双玩家且本机索引为 1，九个案例覆盖 1/2/3 HP 换输出、超额防御、低血量避死、无来伤、相同输出少扣血、立即击杀和重算前已付扣血。双卡案例固定 Beam 2，已付扣血的四卡案例用 Beam 12，其他预算不变；3 HP 案例启用增量等价。另对账原生下一回合状态，检查回合开始自损的周期归属、Fork 隔离、治疗不恢复额度及不跨周期结转。产物另含 `strategy-results.json` 与 `native-boundary.txt`；不能和 `--request` 或其他多人合同开关合用，不建立网络、不验证可见 UI。普通离线性能模式仍只产指标。

`--multiplayer-long-term-contracts` 是首批多人长线收益诊断入口：固定双玩家、本机索引 1，用真实 Dark 球、星能和能力牌根捕获路径观察。它只消费已有 `SearchPathObserver` 的有界副本，记录路径在父内和保路边界的首次消失及已有评估字段；不改评分、Beam 席位、最终排序、状态键或预算。不能和 `--request` 或其他多人合同开关合用，不建立网络、不验证可见 UI；结果与限制见[长线收益归档](strategy/pro-long-term-20260918/README.md)。

同一策略入口还调用 `MultiplayerFinalSelectionContracts`：用无模拟器的指定排名事实调用生产候选准备/选择函数，检查 `4B` 前用药资格、五/六候选的 840 种排列、固定比较器、空集与可展开前缀，输出 `final-selection.json`。这些纯政策反例不代表游戏触发频率；一周期真实托管短搜另以 `preview-final-selection.json` 核对相同候选池的预览、最终选择和结果深度。普通单人搜索在宿主中观察当前 DLL 的多人候选入口，输出 `single-player-isolation.json` 并断言零进入；官方旧 DLL 缺少这些方法时记录空观察列表。完整路线质量仍按固定场景单独比较。

完整回合覆盖选路使用 `--multiplayer-review-contracts window-covered-*`，固定 `--encounter FUZZY_WURM_CRAWLER_WEAK --beam 8 --budget-ms 3000 --dop 1`。`window-covered-contracts --nodes 100` 用纯排名事实检查完整身份、覆盖、祖先/好坏后缀、`4B` 前集合、固定比较器、额外回合与费用门禁；不冒充战斗语义验收。`window-covered-payback --nodes 100` 在内部 52 节点分别关闭/开启 C，断言真实池/工作量相同且共同周期 4→5；`window-covered-sentinel --nodes 180` 检查缺少独立代表后续时维持周期 1 与原整条路线；`window-covered-defense --nodes 180` 用本机无缓冲、50 HP、15 敏捷与一张防御的固定来伤局检查 6→7 周期、3 HP 损失和首动作不变。外部统一续行单独记账，不算额外免费搜索；其第十四周期 37 HP 损失不被前七周期的 3 HP 遮盖。`window-covered-incremental --nodes 100` 在晚回本同根启用逐转移完整回放，仅用于正确性。原 `window-selection*` 阶段显式关闭 C，继续作为 0.43.3 的 A/离线深组观察入口。详见[实施与证据](strategy/pro-window-selection-20260920/implementation.md)。

`--multiplayer-review-contracts facts` 使用双玩家、本机索引 1、双打击与敌方 10 HP 的固定小局，验证真实终局与旧检查点混合、实际搜索继续展开、原生本机/队友药水历史和资格。内部固定 Beam 4 / 120 节点 / 1 秒 / DOP 1 / 三周期，输出 `review-facts.json`；单独复核提前停止可用 `--multiplayer-review-contracts stopping`，输出 `review-stopping.json`，只观察搜索实际展开到的回合。该入口不能与 `--request` 或其他多人合同合用。`SearchedTurns` 是最终选中路线的长度，不是所有搜索层的覆盖量。终局被截断、两条真胜利的结束回合和本机死亡优先控制另由现有 `final-selection.json` 覆盖。普通离线指标、原生托管合同、真实联机验收的边界不变。

`--multiplayer-review-contracts upstream-compatibility --encounter FUZZY_WURM_CRAWLER_WEAK --beam 4 --nodes 100 --budget-ms 1000 --dop 1` 检查官方 0.43.2 的多人接入：双玩家／本机索引 1，空局外成长目标、疯狂科学改进正常施加但不产生信用，两位玩家历史／金斧计算、生产状态键、Fork 与冻结根；最后逐字段比较原生出牌和预测的全队状态。13 项结果写入 `upstream-compatibility.json`。原生动作时间戳局部替换 Godot 时钟，牌效果不替换；历史事件部分是人工构造，不等同全部卡牌或真实联机验证。见[本轮合并](strategy/upstream-0432-merge-20260920.md)。

窗口专项复用该入口的三个阶段：`horizon` 对六个固定根运行 3/5/7/9 上限，输出 `horizon-comparison.json`；`horizon-native` 只核对本机铺垫与队友攻击两个原生手动动作的完整状态；`horizon-ordering` 分别记录现役完整动作数比较和仅在宿主中计算的共同周期成本提案，不改变生产比较器。固定输入为 `--encounter FUZZY_WURM_CRAWLER_WEAK --beam 8 --nodes 350 --budget-ms 3000 --dop 1`，要求单 worker、时间不超过 5000ms。`horizon` 把相同当前回合动作交给统一九周期评价器，队友攻击仅是测试侧的固定扰动；输出区分搜索工作与评价回放。Ethereal 铺垫牌是明确的实验改造，不是原版默认效果。窗口数字与首动作结果见[研究归档](strategy/pro-horizon-20260919/README.md)，不能把这些条件世界线当作真人策略或胜率验收。

`horizon-fourteen` 复用窗口根，比较 7/14 周期并加入第十四周期才回本的输入；当前回合动作统一交给十四周期外部评价器。`horizon-budget` 用十张原版卡牌、抽牌与生成牌的固定长战斗，对照 H7/原额度、H14/原额度、H14/双倍额度，记录实际展开分布、选中周期、共同周期和动作；第三级显式倍增测试上限，不触发生产默认倍增。可用 Beam24/1200 节点/3000ms 或 Beam96/7000 节点/5000ms，单搜索最多等待 20 秒。十四周期语义与有效请求额度由 `--multiplayer-contracts --encounter FOGMOG_NORMAL --beam 12 --nodes 700 --budget-ms 12000 --dop 2` 验证，包含十三次原生完整状态对账、旧七周期控制、额外回合、边界及 UI 投影。结果和限制见[十四周期记录](strategy/multiplayer-fourteen-cycles-20260920.md)。

未击杀长线选路诊断：`--multiplayer-review-contracts window-selection --encounter FUZZY_WURM_CRAWLER_WEAK --beam 8 --nodes 180 --budget-ms 3000 --dop 1` 运行 7 个内部固定预算点，记录真实最终输入池后，仅在离线阶段按深度筛选并调用原比较器；不更改生产搜索。`window-selection-payback` 使用 1 点力量和内部 52 节点的晚回本输入，外部 `--nodes` 仍需至少 100 以满足设置入口。两者输出 `window-selection.json`，把实际展开、观察生成、选中、共同周期、首回合动作与统一 3/7/14 周期外部评价分开。外部后续仅每回合至多一次攻击，不使用保留药水；药水时机差异不能当作资源匹配收益。输入和本轮结果见[选路研究](strategy/pro-window-selection-20260920/local-review.md)。

## 构建

```
dotnet build CombatSolver.csproj -c Release -p:CopyModOnBuild=false
dotnet build tools/OfflineSearchHarness/OfflineSearchHarness.csproj -c Release
```

路径解析与 `CombatSolver.csproj` 同一套：先 `Import` 仓库根的 `local.props`，再按操作系统给
`SteamRoot` / `Sts2Dir` / `Sts2DataDir` / `RitsuWorkshopRoot` / `RitsuLibDir` 默认值；RitsuLib 优先走
`RitsuLib.References.props`，没有它才回退 `RitsuLibDir`。模组 DLL 默认取
`.godot/mono/temp/bin/Release/CombatSolver.dll`，可以用 `-p:CombatSolverDll=<path>` 覆盖；运行期还可以
用环境变量 `OFFLINE_HARNESS_COMBATSOLVER_DLL` 换一份（批量对照不同 DLL 时用）。

宿主对 `sts2` 沿用模组自己的公开化（`Publicize`），对模组本体不做公开化——`CombatSolver.csproj` 里加了
`<InternalsVisibleTo Include="OfflineSearchHarness" />`，宿主只经 `internal` 入口进来。

## 单根用法

```
dotnet tools/OfflineSearchHarness/bin/Release/net9.0/OfflineSearchHarness.dll \
    --request <无人测试请求.json> --label R1 --out <产物目录> \
    --profile VeryHigh --beam 135 --nodes 100000 --dop 1 --budget-ms 600000
```

常用选项：

| 选项 | 含义 |
|---|---|
| `--request <path>` | 无人测试请求 JSON（含 `generatedScenarioPath`），走生成场景开局 |
| `--character/--encounter/--seed/--ascension/--act-index` | 不给 `--request` 时直接指定一个新跑局 |
| `--profile` | `Low\|Medium\|High\|VeryHigh\|Custom`（默认 `Custom`），预设值问模组自己要 |
| `--beam/--nodes/--card-branches/--pile-branches/--hand-branches` | 覆盖预设的宽度、节点上限与分支上限 |
| `--dop <int>` | 搜索并行度（默认 1） |
| `--budget-ms <int>` | 搜索软时间预算毫秒（默认 600000） |
| `--potion-policy <p>` | 药水政策（默认 `Smart`） |
| `--search-mode <m>` | `Evaluate`（默认）或 `Coordinator` |
| `--use-portfolio` | 开宽度组合，只对 `Coordinator` 有效 |
| `--out/--label/--language/--verbose-game-log/--milestone` | 产物目录、标签、本地化语言码、是否打游戏日志、跑到 M1 还是 M2 |
| `--measure-phases` | 在运行日志里输出 `SEARCH_PHASE` 逐阶段排他耗时/分配表 |
| `--memory-no-progress-limit <int>` | 实验：连续多少次无进展内存回收后提前收手；0=关闭（生产默认） |
| `--transposition-entry-limit <int>` | 实验：转置支配表合并条目上限；0=不设上限，缺省=生产默认 1000000 |
| `--enable-no-gc-region` | 让 Runtime 的 No-GC / 回收生命周期真正生效；默认关闭 |
| `--no-gc-region-budget-gigabytes <double>` | No-GC 区域预算（十进制 GB，1..256）；只在开启上一项时生效 |
| `--signal-ballast-mb <int>` | 进 No-GC scope 后先持有 N MiB 活对象；只用于制造受控内存压力，0=关闭 |

## 批量用法

`tools/OfflineSearchHarness/run_plan.py` 吃一份 plan JSON（数组），起 N 个宿主进程并行消费：

```
python3 tools/OfflineSearchHarness/run_plan.py --plan <plan.json> --workspace <dir> --workers 3
```

plan 每项的字段：`label`（必填，简单目录名）、`request`（必填）、`profile`、`beam`、`nodes`、
`maxCardBranchesPerNode`、`maxPileChoiceBranchesPerAction`、`maxHandChoiceBranchesPerAction`、
`maxDegreeOfParallelism`、`searchBudgetMilliseconds`、`potionPolicy`、`searchMode`、`usePortfolio`、
`dll`（换掉这一根运行时加载的 `CombatSolver.dll`）。

产物在 `<workspace>/runs/<label>/`，另有 `<workspace>/runs.jsonl` 与 `plan-summary.json`。

`tools/OfflineSearchHarness/compare_results.py` 把两份 `runs/` 目录逐字段比较（两侧都提供时还比较选中路径的续用戳；`solverMetrics` 里
与时间/内存/GC 无关的字段、选中路线每个动作的 `turn/kind/cardId/potionId/targetCombatId/cardStateKey`、
根 `ContinuationStamp`、生成场景目录指纹），全等返回 0，有差异返回 1 并把明细写进 `--out`。

官方 0.41.0 新增的首条路线耗时、峰值堆大小，以及 `portfolioMembers` / `powerRouteMembers` 内的实际耗时、分配和堆大小同样排除；成员顺序、配置的节点/时间预算、策略开关、实际展开/转移和战斗结果仍参与比较。需要验证完整选择链时额外对照完整 `route.json`，不能把上述六个动作字段的相等扩大为所有计划字段相等。

## 产物

每根一个目录：

- `result.json`：`label` / `status` / `profile` / `searchMode` / `budget` / `solverMetrics`（与游戏内
  无人测试 `result.json` 同名同形，由游戏自己的 Writer 构造）/ `pruneCounters`（宿主从 `SolverResult`
  读的剪枝与复用计数，游戏内那份没有）/ `timeBoundaryObserved` / `wallSeconds` / 峰值托管堆、峰值
  工作集、总分配字节 / `rootContinuationStamp` / `catalogFingerprint`。
- `route.json`：选中路线的动作序列。
- `root-diagnostics.txt`：`SolverDiagnostics.DescribeStart` 的根局面描述。
- `search-policy.json`：这一次求解实际用的 `SearchPolicySnapshot`。
- `harness-result.json`：上面全部加上分步时间线、绕过清单、补丁装载记录。

## 口径

**`Evaluate` 与 `Coordinator` 的区别。** `Evaluate` 是单次求解：直接建一个 `CombatBeamSolver` 跑，不经
协调器，也就没有组合成员和审计通道，`totalExpanded` 等于这一棵树自己的展开量。`Coordinator` 走生产
路径 `CombatSearchCoordinator.Solve`，`solverMetrics` 里的 `total*` 字段是**协调器把各条通道（含审计
通道与组合成员）加总**后的值，所以同一个根同样的宽度，`Coordinator` 的 `totalExpanded` 会明显大于
`Evaluate`。要量「一个宽度值到底搜了多少」用 `Evaluate`；要量「玩家实际会等多久、实际选哪条路线」
用 `Coordinator`。

**固定预算口径。** 宿主总是以 `fixedSearchBudget=true` 起一段离线会话
（`UnattendedTestRunner.BeginOfflineSession`），`--budget-ms` 落在 `searchBudgetOverrideMilliseconds`
上，`--dop` 落在 `searchMaxDegreeOfParallelismForTest` 上——与游戏内无人测试请求里的同名字段走同一段
代码（`ProtocolHost.ConfigureSearchOverrides`）。默认 `EnableNoGcRegion` 关闭；显式传
`--enable-no-gc-region` 且目标是验证 Runtime 的内存回收/截断路径时，才由 `SearchGcPolicy` 管理模式
并将回收回调注入搜索。该模式只用于诊断，不替代游戏内无人测试的正确性断言。

**宽度组合。** `--use-portfolio` 把 `useBeamWidthPortfolioForTest` 打开，与无人测试请求里那个开关同义；
成员宽度不指定时用协调器自己的默认成员集，成员明细在 `solverMetrics.portfolioMembers`。

## Godot 绕过

宿主不启动引擎，凡是会打到 Godot 原生层的入口都要绕开。绕过点全部集中在
`tools/OfflineSearchHarness/GameBootstrap.cs` 一个类里，类头有完整的表（目标、为什么必须绕、绕过后
返回什么、对搜索结果有没有影响），结果 JSON 的 `bypasses` 字段列出实际装上的那些。摘要：

| 目标 | 绕过后 | 对搜索结果 |
|---|---|---|
| `Logger.GetIsRunningFromGodotEditor`、`ConsoleLogPrinter.Print` | 不判编辑器、打到 `System.Console` | 无，只决定日志去向 |
| `LocString.GetRawText/GetFormattedText/Exists` | 返回本地化键名 / `true` | 无，搜索不读文案（见下方限制） |
| `PreloadManager.Load{Run,Act,RoomCombat}Assets` | `Task.CompletedTask` | 无，战斗建立不需要立绘与节点 |
| `NCombatRulesFtue.Create` | `null` | 无，与游戏内无人测试同语义 |
| `MigrationRegistry.RegisterAllMigrations` | 跳过注册 | 无，离线不读存档 |
| `NGame.GetGameVersion`、`PlatformUtil.GetPlatformBranch/GetPlayerNameRaw` | 固定值 | 无，只进联机握手信息与显示名 |
| `Godot.Node` 及其 304 个子类的静态构造 | 跳过 | 无，离线不建节点树 |

还有一处不是补丁：`SolverController.DisplayServerNameProvider` 被设成固定返回 `"headless"`，与游戏内
`--headless` 取到的值一致，帧压力恢复照样关闭。

## 已知限制

- **本地化返回键名**：`LocManager.Initialize` 要用 `Godot.FileAccess` 读 `res://localization`，离线装的是
  一张空表，所有文案取到的是键名。显示字段（`cardTitle` / `targetName` / …）因此不能与游戏内直接比，
  `compare_results.py` 已把它们排除。搜索本身不读文案。
- **只覆盖生成场景与新跑局**：`runSnapshotPath`、`replayStatePath`、`checkpointArchivePath` 这几条
  「从存档/回放恢复战斗态」的入口没有接，宿主只能从新跑局或生成场景开局。
- **RitsuLib 未初始化**：宿主装的是模组里与搜索正确性相关的那 13 个 Harmony 补丁，RitsuLib 自己的运行期
  初始化没跑。实测 30 根里有 2 根的探索量与游戏内不同，结论字段（选中路线、`score`、
  `projectedBattleHpLost`）相同。
- **不做正确性验收**：宿主没有无人测试的断言体系，它只产指标。行为改动仍然要过
  `docs/HEADLESS_TESTING.md` 的流程。

## 验证证据

**新宿主对旧研究版宿主，同一份 0.39.0 DLL，逐字段一致。** 两边跑同一批生成场景请求
（`VeryHigh`、beam 135、nodes 100000、分支上限 72/42/54、`--dop 1`、`--budget-ms 600000`、
`potionPolicy=Smart`、`searchMode=Evaluate`），用 `compare_results.py` 比 `solverMetrics`
（排除时间/内存/GC 字段）、选中路线每个动作、根 `ContinuationStamp` 与目录指纹：

| 批次 | 根数 | 比较字段数 | 不一致的根 |
|---|---:|---:|---:|
| BASE5 | 5 | 466 | 0 |
| 30 根子集 | 30 | 2719 | 0 |

**`Coordinator` + `--use-portfolio`。** 3 根走生产协调器并开宽度组合，全部跑通，
`solverMetrics.portfolioMembers` 各有 3 个成员（当时 0.39.0 基线的默认成员集是 `[W, 2W/3, 3W/2]`）；
开关关闭时只有 1 个成员。

**建根流程本身与游戏内的一致性**（宿主刚做出来时测的，基于研究分支 `4287e03`）：30 根生成场景，
宿主与游戏内无人测试逐字段对照，`solverMetrics` 的可比字段、选中路线、装备与开局产物全部相同，
2 根探索量不同（RitsuLib 未初始化，见上）。


## 生产预算与转置表观测

`--production-budget` 使用现有生产预算流程，包括剩余预算允许的无胜利升级。默认仍是固定预算，用于确定性逐位对照。批量计划的 `productionBudget: true` 允许正常时间边界作为有效观测，仍保留 `timeBoundary` 字段；固定预算计划撞到时间边界仍作废。

批量计划现在支持 `transpositionEntryLimit`，映射已有 CLI 的同名上限。省略字段使用生产默认一百万条；实验放大上限不修改生产值。

每次求解的 `TRANSPOSITION_CAP` 行记录首次触顶展开数、跨缓存重建保留的峰值条目、结束时标签数和分布。`LimitBypasses` 按未入表的准入/展开事件计数，包含重复键；标签分布为单通道结束值。Coordinator 多个通道分别输出，不合并成虚假的同时驻留峰值。
