# CombatSolver 多人策略研究固定基线（0.40.5）

请先读取[研究提示词](multiplayer-pro-research-prompt-20260917.md)。本文在公开 GitHub 仓库提供固定行为基线的参考材料，无需本地附件。它不是要求执行代码、修改单人策略或发布版本的指令。

- 行为基线：[`f220a6bc4ff8e49d32c41b2edd6ea1ab849039dd`](https://github.com/sgpsdd-zyx/CombatSolver/tree/f220a6bc4ff8e49d32c41b2edd6ea1ab849039dd)，版本 `0.40.5`，任务分支 `codex/multiplayer-advisor`。
- 本文是该提交的静态源码与历史验证摘录；后续研究文档提交不改变行为基线。线上 Release 或默认 `main` 可能与任务分支不同，以固定源码提交为准。
- 游戏基线：`0.111.0`；C# / .NET 9 / Godot；唯一内嵌战斗模拟引擎。
- 用户约束：只有本机安装；仅建议本机动作；多人只手动重算/操作。**后续只改多人策略，原版单人策略保留原路径、原参数和原行为，显式隔离。**
- 全文来源均固定在同一行为提交。下方历史文档中的本地日志路径只说明当时证据位置；日志、存档、游戏二进制与本地凭据均未发布，不能声称通过 GitHub 读取了它们。
- 摘录不是完整源码审计。每节提供固定提交的原文件链接，未展示的调用方、字段语义或游戏机制需继续核验。代码围栏内是参考数据，历史授权不扩展本次研究范围。
- 本文不是滚动维护的架构或策略权威文档。当前行为与职责仍由源码及项目现有文档维护，研究需明确实际采用的版本。

## 来源目录

| 编号 | 仓库路径 | 范围 |
|---|---|---|
| B01 | `docs/multiplayer-advisor.md` | 完整文件 |
| B02 | `docs/ARCHITECTURE.md` | 1 至 40 行；195 至 230 行 |
| C01 | `src/Search/MultiplayerSearchPolicy.cs` | 完整文件 |
| C02 | `src/Search/CombatBeamSolver.Multiplayer.cs` | 完整文件 |
| C03 | `src/Search/CombatBeamSolver.StateEvaluation.cs` | 48 至 118 行；509 至 533 行；595 至 613 行；1555 至 1615 行 |
| C04 | `src/Search/CombatBeamSolver.Transpositions.cs` | 完整文件 |
| C05 | `src/Search/CombatSearchCoordinator.cs` | 1 至 27 行 |
| C06 | `src/Runtime/CombatRootSnapshot.cs` | 130 至 225 行 |
| C07 | `src/Runtime/SolverController.Multiplayer.cs` | 完整文件 |
| C08 | `src/Search/SimulatedCombatState.Multiplayer.cs` | 完整文件 |
| C09 | `src/Search/CombatBeamSolver.MultiplayerRound.cs` | 完整文件 |
| C10 | `src/Search/CombatBeamSolver.Expansion.cs` | 3205 至 3245 行；3470 至 3520 行；4355 至 4395 行；4410 至 4507 行 |
| C11 | `tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs` | 完整文件 |

## B01：docs/multiplayer-advisor.md

[固定提交原文件](https://github.com/sgpsdd-zyx/CombatSolver/blob/f220a6bc4ff8e49d32c41b2edd6ea1ab849039dd/docs/multiplayer-advisor.md)

实际行为与历史局部验证；这些记录不是本次新增测试。

源文件第 1 至 145 行，基线 `f220a6b`。

````markdown
# 多人军师（fork 0.40.5）

本 fork 从 `0.40.3` 起提供多人建议模式；`0.40.4` 修复首次手动计算被旧单人记录限制阻断的问题；`0.40.5` 改为在单回合 3 HP 扣血目标内优先输出。单人仍使用原有搜索政策和自动执行入口。本次准备本地最小安装包，安装与玩家说明见 [版本日志](releases/0.40.5-RELEASE_NOTES.md)；此前公开版本位于 `sgpsdd-zyx/CombatSolver` 的 GitHub Releases。最初的本地玩家规划文档属于设计输入；本文描述实际实现和验证范围，多人功能尚未完成真实联机验收。

## 使用方式

- 主机或客户端均以本机玩家为决策者，不假定玩家列表第一位就是自己。
- 发牌和回合开始选择完成后，按「重新计算」捕获当时的整个战场。只有这个按钮发起重算；新回合、自己或队友的操作不会自动重算。
- 尚有动作/选牌在结算时显示等待提示，完成后继续这次手动请求；启动失败会显示原因。`0.40.3` 出现点击完全没有反馈时，升级到 `0.40.4` 并重启游戏。
- 多人面板隐藏执行、全自动、自动计算和采纳入口；停止按钮保留当前候选，尚无新候选时保留上次完成的建议。后台只模拟，不替任何玩家出牌、用药、结束回合或选牌。
- 队友行动后旧建议仍保留，显示「战场已变化」。再次按重新计算会取消并等待旧搜索退出，再捕获稳定的新局面。结束战斗会清理搜索和建议。
- 药水加入每个合法动作节点的搜索，包含使用时机、玩家/敌方目标和本人的选牌；保护及强制用药设置继续生效。多人不套用单人额外的省血门槛审计。

## 推演假设与取舍

搜索默认最多覆盖七次敌方行动周期；额外玩家回合不消耗这个窗口。节点/时间预算不因窗口扩大而增加，预算不足时可以提前返回。面板同时显示实际推演的敌方回合数、上限和相应边界提示，不保证每次达到七回合。Beam 是有界启发式搜索，不承诺全局最优。

按用户确认的多人打法，每个敌方周期以本机扣血不超过 3 HP 为目标，在此范围内优先击杀和压低敌人剩余生命。伤害相同时再选择扣血更少的路线，仍优先避免本人死亡及消耗复活效果。各周期超过 3 HP 的部分优先压低，因此没有满足额度的路线时仍会给出超额较少的建议；不承诺能把每场战斗都控制在额度内。已经覆盖完整窗口的存活路线优先于尚未验证后续安全的短路线，相同收益时少用药。

3 HP 按每个敌方周期分别计算，不是七回合共用的 21 HP。使用本机实际未格挡扣血，包含自损以及手动计算前本轮已经支付的部分；治疗不抵扣已用额度，重新计算和额外玩家回合也不刷新。下一玩家回合开始的自损属于新的周期。面板会显示扣血目标与当前预测中最高的单周期扣血，供玩家决定是否采用。

所有玩家的血量、格挡、资源、牌堆、药水、能力、遗物、球、怪物状态及 RNG 从当前战场冻结。未来假设队友不再主动出牌/用药，但照常处理其回合阶段、被动效果、抽牌和受击。因此这是一条有条件的建议，不是队友未来行为的预测，也不把未知的队友输出算作已造成伤害。

需要队友选择时明确停止在「等待队友选择」边界，包括向队友投掷需要选牌的药水；不替队友决定。未支持效果形成明确边界。玩家列表中的目标带编号，重复职业也能区分。

## 复用与单人隔离

每次搜索继续使用现有分支 Fork、完整状态去重、转置表、选牌前缀、并行展开和预算控制。多人状态键补充所有队友的可变状态；只描述单人效果的动作支配/小型 Pareto 不能据此淘汰多人分支。

多人保路在既有 Beam 内预留防御、进攻和铺垫候选，避免进攻路线在中间阶段全部被淘汰；没有扩大搜索预算。单周期扣血账本归属搜索节点，状态相同但当前已用额度/此前超额不同的路线由去重和转置标签区分，不把目标额度写入战斗状态键。

跨次最多保留四条路线的纯动作数据，每条最多重放当前回合的 32 个无选择动作。旧动作在新根上重新检查牌实例、费用、目标、药水指令并重新模拟，之后作为候选起点；不保留旧树、旧分数、旧影子对象，也不强制采用旧路线。非法首步不复用，遇到选择或回合结束便停止前缀复用。复用工作计入当前请求节点及转移统计。

这能让已找到的合法动作组合较早参加比较，但没有证明总耗时一定下降；这里不报告提速倍数。本人已经执行旧路线前缀时暂不自动匹配剩余后缀，也没有跨不同状态直接返回缓存结果。

多人策略由 `MultiplayerSearchPolicy` 注入，Runtime 不修改持久化的单人自动计算设置。多人绕开单人磁盘路线恢复、反事实用药审计、局外收益目标和自动部署。既有单人分支与比较器保持原路径。

## 代码职责

| 位置 | 职责 |
|---|---|
| `Runtime/SolverController.Multiplayer.cs`、会话字段 | 过期提示、建议结果和最多四条路线；重新计算的取消/排空沿用 Controller |
| `Runtime/CombatRootSnapshot.cs`、`ContinuationStamp.Multiplayer.cs` | 主线程冻结全队状态及本机本轮已付扣血、完整多人状态对账 |
| `Search/MultiplayerSearchPolicy.cs`、`CombatBeamSolver.Multiplayer.cs` | 多人目标、节点扣血账本、结果比较、多人 Beam 保路和受限旧路线重放 |
| `Search/CombatBeamSolver.MultiplayerRound.cs` | 全队回合阶段、额外回合、敌方周期及本人的选择展开 |
| `Search/SimulatedCombatState.Multiplayer.cs` | 本人身份、额外回合参与者、窗口计数、原始周期末扣血检查点、队友选择边界、历史窗口 |
| `Prediction/*Multiplayer.cs` 与相关语义入口 | 多目标怪物效果、多人专属卡牌补偿；不执行真实动作 |
| `UI/SolverOverlaySnapshot.cs`、`SolverActionBar.cs` | 建议范围、边界、中英文提示和多人按钮可见性 |

## 验证记录（2026-09-17）

### 0.40.5 单周期扣血目标

`c172621`（0.40.4）基线将 1 HP 损失在中间评分中等同于 1000 点敌人伤害，终局又先最小化整个窗口扣血。`FUZZY_WURM_CRAWLER_WEAK` 的双卡短搜复现：只有 1 点来伤时仍选择防御、打 0 伤害。相同输入在新策略下选择攻击，损失 1 HP、造成 6 伤害。

`--multiplayer-strategy-contracts` 使用游戏 `0.111.0` 的真实托管模型，双玩家、本机索引 1。固定 100 节点、1 秒预算、DOP 1；前八个双卡案例为 Beam 2，第九个四卡案例为 Beam 12。每次搜索等待上限 30 秒，原生步骤上限 20 秒；最终整个合同步骤 0.862 秒。

| 场景 | 期望和实际结果 |
|---|---|
| 仅剩 1 / 2 / 3 HP 来伤，攻击或防御二选一 | 分别损失 1 / 2 / 3 HP，均造成 6 伤害 |
| 同根剩余来伤 4 HP | 防御，损失 0 HP、造成 0 伤害 |
| 来伤 3 HP，本人只剩 3 HP | 防御避死，损失 0 HP |
| 无来伤 | 攻击，造成 6 伤害 |
| 能量允许相同输出下补防御 | 造成 6 伤害、损失 0 HP |
| 敌人只剩 6 HP | 立即击杀，损失 0 HP |
| 本轮已付扣血后治疗并重算，可再次自损换额外攻击 | 采用无额外扣血的 6 伤害路线，不重新消费额度 |

3 HP 案例开启增量/完整重放等价。另以真实回合开始自损区分旧周期和新周期，预测与原生下一回合的完整 `ContinuationStamp` 全文一致；Fork 子分支修改周期末检查点不影响父分支或旧根。账本断言覆盖分散的 3+3 HP 不超额、0+6 HP 不能挪用上一周期额度、同周期两次 2 HP 合计超额 1 HP；主线程历史捕获确认治疗不抹去已付扣血。九个结果均检查摘要投影的额度/最高值。

最终七周期哨兵 `FOGMOG_NORMAL`（Beam 12、350 节点、12 秒、DOP 2）通过六个完整原生回合的全文对账、五/七周期上限、额外回合、旧根稳定性、四张多人卡、队友药水、本人/队友选牌、药水指令及禁止自动操作。搜索返回 23 动作、七回合建议，有效旧前缀重验 1 动作，步骤耗时 5.382 秒。该耗时含建局后的多次合同操作，不用于性能比较。

本轮单人哨兵与独立编译的 `8411164` 比较：61 项非时间指标、9 动作、完整根和目录共 72 项一致、0 差异，126 节点、376 转移、T3 胜利、4 HP 扣血。只证明该哨兵未退化。主项目和宿主行为构建均 0 警告/0 错误；Bash 门禁通过，三条变更声明已同步 Windows 入口但未执行 PowerShell；443 项中英模板占位符一致。CoverageCatalog 状态字段/分支读取检查通过，3035 项无未分类字段或 live 分支读取；工具隔离引用配置构建有 2 条依赖路径/解析警告、0 错误。

基线 `.local/mp-strategy/baseline/`，最终策略 `.local/mp-strategy/fixed/`（含 `strategy-results.json`、`native-boundary.txt`），七周期 `.local/mp-strategy/window/`，单人 `.local/mp-strategy/solo/` 和 `.local/mp-strategy/solo-comparison.json`，目录检查 `.local/mp-strategy-coverage.log`。结构化条目为 `MULTIPLAYER-ADVISOR-HP-ALLOWANCE`。未启动 Godot 或 Steam，未验证真实网络交错、可见布局及全角色/全遭遇质量，不把离线局部合同作为联机验收。

策略合同复跑入口（先按文末构建主项目与宿主）：

```bash
dotnet tools/OfflineSearchHarness/bin/Release/net9.0/OfflineSearchHarness.dll \
  --multiplayer-strategy-contracts --encounter FUZZY_WURM_CRAWLER_WEAK \
  --beam 2 --nodes 100 --budget-ms 1000 --dop 1 --out .local/mp-strategy-check
```

### 0.40.4 手动启动修复

`a56a84f` 的按钮入口基线在 `CombatReplayOutcome` 构造时因 `state.Players.Single()` 抛错；调用链为 `OnRecalculatePressed -> RequestSearch -> RecordCheckpoint -> EnsureSession -> BeginCombat`。这一步发生在原初始化异常边界之前，解释了按钮和面板完全不变。此前的多人模拟合同直接构造搜索器，Runtime 部分只验证自动/执行入口被拒绝，遗漏了手动启动链路。

`--multiplayer-start-contracts` 使用游戏 `0.111.0`、双玩家、`FOGMOG_NORMAL`、Beam 12、100 节点、1 秒预算、DOP 1。它先分别绑定 Host/本机索引 0 与 Client/索引 1，创建真实战斗记录并调用原按钮处理函数；随后覆盖原生动作等待、完成后继续、记录初始化失败、同步/异步动作失败和重试。本人损失 3 HP、回复 2 HP 与队友损失 7 HP 正确分开，四次搜索均返回多人建议，无部署。最终步骤耗时 0.768 秒，单次等待上限 30 秒。

证据为 `.local/mp-start/baseline.log`、`.local/mp-start/fixed/harness-result.json`、`.local/mp-start/fixed/manual-startup-events.txt` 和 `.local/mp-start/fixed.log`，结构化条目 `MULTIPLAYER-ADVISOR-MANUAL-STARTUP`。宿主替代 Godot 渲染/分发与计时、模拟网络类型；完整录像负载采集需要游戏环境，未在本合同中执行。会话创建、本机结果观察、Runtime 启动和搜索均保留真实实现。真实网络连接、可见按钮点击、多人问题包回放尚未验证；原七回合语义和单人哨兵沿用历史证据，本轮未重跑。

### 0.40.3 实现验证

基础版本 `8411164`（0.40.2），游戏 0.111.0，macOS ARM64，.NET 9。未启动 Godot 或可见 Steam。

新增 `OfflineSearchHarness --multiplayer-contracts` 使用游戏真实托管对象和结算命令，建立双玩家且本机位于列表第二位。宿主原有图形绕过之外，仅为该测试绕过动画计时，并把单机传输的「任意一人准备即可结束」改为原生多人准备人数条件。没有模拟网络连接。

### 三回合实现阶段

前一任务固定 Beam 12、350 节点、12 秒软预算，单请求最多等待 90 秒，实际各轮均在数秒内结束。两个遭遇 `FUZZY_WURM_CRAWLER_WEAK`、`FOGMOG_NORMAL` 覆盖：

- 两次原生完整回合与预测 `ContinuationStamp` 全文一致，包括双方牌堆、历史、能力、怪物行动、生命、资源和九条 RNG；第三次敌方周期触发窗口边界。
- 实机推进后旧根重放结果不变；实机消耗生成 RNG 后，新根状态不同，旧根仍保持原值。
- `Lift`、`BelieveInYou`、`TagTeam`、`Flanking` 的单动作原生对账，以及向队友使用格挡药水的对账。这里使用模型 ID 标识测试输入，不代替游戏中文译名。
- 额外回合原生对账；FOGMOG 场景暴露并修复了额外回合期间未行动队友的历史计数未失效问题。
- 队友回合开始需要选牌时形成边界；本人同类选牌继续展开，并开启增量/完整重放等价检查。
- 生成卡牌药水对本人展开选项，选中结果与原生用药全文对账；对队友则形成外部选择边界。
- 立即用药击杀、禁止用药、强制用药；有效旧路线重新验证 3 个动作，非法牌实例的旧路线验证数为 0。
- 强制多人 Runtime 条件下，自动计算/执行能力为 false；执行、全自动、接管和自动回合搜索入口均在触碰 UI/真实操作之前返回。

单人哨兵与独立编译的未修改基线对比：61 项非时间/分配指标、9 个路线动作、根状态与目录字段共 72 项一致；126 节点、376 转移、第 3 回合胜利、累计损失 4 HP。它证明该哨兵没有退化，不等同于所有单人内容的完整回归。

验证产物保留在被忽略的 `.local/mp-runtime/`、`.local/mp-final/`、`.local/mp-fogmog/`、`.local/solo-comparison.json`。失败基线及原因也在对应日志中；不把修正前失败算作通过。

### 七回合扩展与收尾

前一任务在最终提交前中断，最后的 5～7 回合要求尚未落地。本次将默认上限改为七个敌方回合，并补齐已完成推演次数到只读结果及 UI 的投影。旧路线重放计数移入本次 `SearchRunContext`，不保留在 solver 接线层。

`FOGMOG_NORMAL` 使用同样的 Beam 12、350 节点、12 秒预算、DOP 2；为让无主动操作的双方活到窗口末尾，合同在建局后把双方生命及上限设为 500。全部预测先从同一个冻结根生成，再推进原生战斗：

- 前六个完整回合的双方有序牌堆、能力、历史、怪物状态、生命、资源和九条 RNG 与原生全文对账一致；第七个敌方回合在窗口边界停止。
- 同根的五回合政策在第五次停止，七回合政策此前继续；额外玩家回合仍不增加敌方周期数。
- 正式搜索在原预算内返回 23 个动作、七回合建议；有效旧路线重新验证 1 个动作，非法实例前缀拒绝。此输入与前一阶段生命值不同，不比较耗时或路线优劣。
- 仅允许 1 个展开节点的请求按节点边界提前返回；界面投影显示实际推演次数与七回合上限，英文模板正常解析。
- 四张多人卡牌、队友药水、生成药水原生结算、本人选牌增量等价、队友选择边界、强制/禁用药水与禁止自动操作合同均通过。

此次产物为 `.local/mp-seven/` 与 `.local/mp-seven.log`，合同步骤耗时 5.112 秒；其中含原生对照及多次搜索，不作为单次搜索性能数字。

最终单人哨兵沿用独立编译的 `8411164` 基线，只重跑当前实现：61 项非时间/分配指标、9 个动作、根状态与目录字段共 72 项再次全部一致，仍为 126 节点、376 转移、T3 胜利、4 HP 累计损失。产物 `.local/solo-seven/`、`.local/solo-seven-comparison.json`。结构化证据 `MULTIPLAYER-ADVISOR-MANAGED-CONTRACTS` 和 `MULTIPLAYER-ADVISOR-SOLO-SENTINEL` 记录在 `coverage/test-evidence.json`，没有把局部合同登记成所有多人内容已验证。

主项目与离线宿主 Release 编译均为 0 警告/0 错误；Bash 结构门禁通过，`search_files=118`。补齐了前一任务漏登记的两个多人搜索分片，两端文件清单与 7 条多人规则一致。440 项本地化模板的占位符相同。本机未安装 PowerShell，未执行 Windows 入口。

`CoverageCatalog --verify-effective --verify-state-fields --verify-branch-state-reads` 通过：现有 3035 项目录无待实现、live 分支读取或未分类状态字段。工具原默认 RitsuLib 路径不适配本机多程序集安装，首次编译失败；仅在被忽略的本地检查配置中导入已安装的 0.111.0 引用表后完成验证。输入/生成目录隔离于 `.local/mp-coverage/`，日志 `.local/mp-coverage-configured.log`。该静态目录检查不扩大多人运行时验证范围。

未验证：真实主机/客户端连接、网络操作交错、断线重连、可见 UI 布局、三/四人原生全量对照及所有角色/遗物/第三方 Mod 的组合。离线合同是局部语义证据，不替代游戏内联机验收，不宣称 FPS 或实机提速。

复跑入口：

```bash
dotnet build CombatSolver.csproj -c Release -p:CopyModOnBuild=false
dotnet build tools/OfflineSearchHarness/OfflineSearchHarness.csproj -c Release
dotnet tools/OfflineSearchHarness/bin/Release/net9.0/OfflineSearchHarness.dll \
  --multiplayer-contracts --encounter FOGMOG_NORMAL \
  --beam 12 --nodes 350 --budget-ms 12000 --dop 2 --out .local/mp-contracts
```
````

## B02：docs/ARCHITECTURE.md

[固定提交原文件](https://github.com/sgpsdd-zyx/CombatSolver/blob/f220a6bc4ff8e49d32c41b2edd6ea1ab849039dd/docs/ARCHITECTURE.md)

架构摘录；其余职责请参考仓库完整文档。

源文件第 1 至 40 行，基线 `f220a6b`。

````markdown
# CombatSolver 架构与职责地图

本文描述当前源码的所有权边界。它面向维护者和 coding agent；玩家功能说明见根目录 `README.md`，历史重构证据见 `docs/refactoring/`。

职责迁移时优先更新本文，并同步更新 Windows 的 `tools/verify-refactor-boundaries.ps1` 与 Linux 的 `tools/verify-refactor-boundaries.sh`。历史审计记录保留当时结论，不承担当前导航职责。

## 当前精简分支的局部合同

普通 Power 的 Target 保留原生 null；显式定向入口继续保存传入目标。临时力量复用上游施加入口，首次回调仍先于计数加入，封顶后按修正请求量触发数量回调。Weak/Vulnerable/Frail 的跳过首次持续时间扣减只由各自 Power 保存，影响状态指纹与 ContinuationStamp；其他能力的无效 Skip 元数据不参与该等价性判断。未新增战斗后端或状态存储副本。

## 1. 运行链

### 多人手动建议分支

`SolverController` 在网络多人中只接受 Manual 请求，排空旧 worker 后主线程捕获整个战场。`SolverController.Multiplayer` 持有过期标记和有界纯动作路线，不做自动续用或部署；`ContinuationStamp.Multiplayer` 对账全队状态。`MultiplayerSearchPolicy` 注入最多七次敌方周期与独立建议排序，`CombatBeamSolver.Multiplayer` 重放旧前缀，重放计数属于 `SearchRunContext`；`MultiplayerRound` 编排全队回合，不接管实机。已推演的敌方周期从分支按值复制至 `SimulationSnapshot`、`SolverSnapshot` 和 UI，与窗口上限分开显示，不增加原节点/时间预算。`SimulatedCombatState.Multiplayer` 的身份、窗口和额外回合参与者随 Fork 复制，其他队员数值继续属于既有影子状态。队友选择通过显式边界退出，本人选择继续搜索。UI 仍只渲染 snapshot，单人调用原有路径。详见 [行为与验证](multiplayer-advisor.md)。

多人默认以每个敌方周期 3 HP 扣血为输出目标范围。`CombatRootSnapshot` 在主线程冻结本机本轮已经发生的未格挡伤害；敌方周期结束、下一玩家回合准备之前，`Expansion` 将累计扣血检查点写入 `SimulatedCombatState.AdvisorLastEnemyCycleHpLost`，随 Fork 按值复制。该字段只记录原始观察，不决定政策；`SearchNode` 持有不可变的 `MultiplayerHpLossBudget`，区分已结束周期的超额扣血与当前周期的已用额度。治疗、重算和额外玩家回合不清除已用额度，下一回合准备的自损计入新周期。政策账本通过转置标签和去重标签保留，不进入战斗状态键或 `ContinuationStamp`。

`CombatBeamSolver.Multiplayer` 中的 `BeamRetentionPolicy` 分片拥有多人进攻、防御、铺垫候选的有界保留，所有通道共用既有 Beam、节点和时间预算；同文件的多人终局比较器先检查生存与窗口完成度，再比较超额扣血、击杀、敌人剩余生命和累计扣血。`StateEvaluation` 只计算中间特征，UI 通过只读结果显示额度和当前路线的最高单周期扣血。

### 单一搜索预算与兼容边界

遗物计数策略由 `RelicCounterCatalog` 声明已核对的跨战斗计数，Runtime 过滤总/单项开关与当前持有对象，冻结到 `SearchPolicySnapshot.RelicTargets`。`SimulatedCombatState.RelicCounters` 只投影既有分支状态，`RelicCounterPolicy` 生成范围达标掩码和一次性 HP 额度。快照的 `StrategicHpCredit` 汇总成长与计数额度，终局/中间排序、用药审计及保路共享；真实战损早停仍须同时满足成长、药水、偷窃和已启用的遗物目标。`SolverRelicStrategyPanel` 拥有 UI 输入，Overlay 只接线，Controller 使续用失效并按原自动计算偏好重算。设置导出、归档恢复与磁盘路线键携带同一策略，旧包默认关闭。见 [完整计数清单](relic-counters.md)。

`RelicCounterEvaluation.CounterValues` 按内置遗物顺序保存结束计数的四位数字槽，随结果快照和续用按值保留；`SolverStrategyOutcomeText` 在主线程投影已卡/未达标及成长次数，Overlay 只读取摘要文本。计数显示不新增搜索目标或评分维度。

遗物目标包含优先级，达标优先值用于原 HP 轴之后的路线比较，掩码仍负责 Pareto 和早停。MeatOnTheBone 使用一个半血布尔目标；完整获胜且用户启用时，StateEvaluation 仅补入 HealFor 与 MonotoneHealFor 的差值，沿首领战略价值折算，不在模拟器重复治疗。

开局后续动作探针通过 `ApplyFixedPrefix(seed, prefix)` 构造真实父链，保留前置资源/药水/准备动作的动作数与状态；不得用已经回放前缀的快照伪装成 action_count=0 的根。

`SolverSettings` 将四档或自定义配置解析为一个 `Profile`，主线程冻结到 `SearchPolicySnapshot`。`CombatSearchCoordinator` 的主搜索、药水审计与恢复使用同一套预算维度；`FixedBudget` 只限制无胜利后的预算扩展，测试/API 可显式覆盖时间。Search 不再包含 Short/Deep 配置、枚举、检查点或分段累计统计；两端结构门禁禁止这些符号回流。`SearchRequestWorkTotals` 按请求累计唯一 elapsed 和工作计数。

旧设置的 deep 字段仅在反序列化边界迁移至 search 字段，保存只写新字段；旧归档读取 deepProfile，新的回放政策覆盖文件使用 profile/fixedBudget。旧无人请求的 forceShortSearchOnly 与短/深时间字段在 ProtocolHost 入口转成固定预算；已退休的阶段断言参数不再接受。UI 设置只渲染单列预算。

`CombatDiagnosticJournal` 仍按战斗保留详细诊断，额外向有界进程日志复制 GC/分配预算/主线程帧摘要，供跨战斗关联。高频显示与节点日志不复制。`SearchGcPolicy` 分别记录收集完成后的堆状态和重启 NoGC 后的状态，诊断采样不改变收集策略。

`SmartLayerMemoryForecast` 只决定有证据能改善容量的可选层间回收：完整层超过新区域容量、缺少预测或当前区域新分配低于 max(64 MiB, 区域限额/4)时沿用每批准入。`SearchGcPolicy` 的自动回收请求已确认完成的后台收集；搜索中和搜索后保持同一路径，手动回收继续强制压缩。最新实机 trace 已证明按碎片比例自动压缩会造成数秒停顿，因此碎片比例不再决定自动压缩。算法层不调用 GC。

`NodePoolSignalLifetimePatch` 补齐原版 `NodePool<T>` 递归信号清理的包装所有权：返回的 typed array 通过底层 Array 释放；字典、Variant、从原生转换得到的新 StringName 在作用域退出时释放。节点与 Callable 的目标不属于此作用域，保留原解绑条件；原版 Free 的对象池账本和 OnFreedToPool 保持原调用链。NCard 与 NGridCardHolder 的共享泛型方法分别由真实方法合同覆盖。

````

源文件第 195 至 230 行，基线 `f220a6b`。

````markdown
| 文件 | 权威职责 |
|---|---|
| `CombatBeamSolver.cs` | 构造参数、不可变根配置、`SearchRunContext` 与两个策略对象接线 |
| `CombatPlan.cs` | `SearchNode`、`SimulationSnapshot`、动作与最终计划数据 |
| `CombatBeamSolver.Models.cs` | `SearchFeatures`、单次运行 `SearchRunContext` |
| `CombatBeamSolver.Transpositions.cs` | 转置标签与支配前沿；单标签内联，多标签保持原序List，缩回单标签即释放额外容器 |
| `CombatBeamSolver.Phases.cs` | `Solve`、阶段循环、总预算与回合层预算保留、当前回合预览、约 `200 ms` 刷新的动态推演路线，以及玩家采用路线/执行当前回合的收束检查点；动态路线显式携带战斗是否结束，未完成路线不产生整场战损数值 |
| `CombatBeamSolver.Expansion.cs` | 可执行卡牌/药水/结束回合候选展开和动作回放入口；识别选牌后手中实际可支付的能力。三层首领的首个搜索回合由Phases在普通父节点提交完成后提前展开这些中间态，复用Expand的去重/节点计数，不注入固定答案或终局奖励 |
| `CombatBeamSolver.ParallelExpansion.cs` | 固定 worker lane、卡牌/药水动作准备与原始候选物化、按输入顺序串行提交 |
| `CombatBeamSolver.AdmittedExpansion.cs` | 已准入父节点的准备、动作探测、选择准备/回放/续接、药水/目标与回合尾部作业；有界派发、快照移交、取消/异常排空 |
| `CombatBeamSolver.PrimaryChoiceReplay.cs` | 原预算保证必经的首层回放、唯一快照暂存与原序消费；动态预算和实例补充仍由一个续接作业独占 |
| `CombatBeamSolver.EndTurnChoiceReplay.cs` | EndTurn初始回放与首层挂起选择准备；复用必经回放槽位，原序解析嵌套/实体补充，独占返回候选和待命基线 |
| `CombatBeamSolver.CardChoiceContinuation.cs` | 手动自身选牌检查点的同父/同动作匹配、串行选择链与并行frontier所有权、尝试/捕获/复用/回退计数；不改变预算或候选 |
| `CombatBeamSolver.PotionChoiceContinuation.cs` | 9种手动药水的公共候选准备、同父同动作检查点、消耗/Use前缀与后置钩子之间的稳定复制；普通Fork、frontier所有权及物理工作计数 |
| `CombatBeamSolver.ExecutionChoiceContinuation.cs` | 挂起选择层的seed/数据帧所有权、精确父动作与已消费选择前缀匹配、锁内复制、搜索计数及动作最终结算；逐层再次捕获，不改变预算 |
| `CombatBeamSolver.TurnExecutionContinuation.cs` | 首回合与后续回合共用玩家准备阶段机；保存来源循环、抽牌补偿、提前SideTurnStart、自动牌及共享死亡集合进度 |
| `CombatBeamSolver.ExecutionChoiceContinuation.Testing.cs` | Search内部合同入口，完整回放基线与生产选择层续跑逐分支对账；不依赖无人测试runner |
| `CombatBeamSolver.RoundTransition.cs` | 玩家回合开始推进；在抽牌准备完成但尚未Draw或抽牌/历史补偿完成两个稳定点保存同父EndTurn前缀；frontier独占、同父gate复制、生产者排空后释放；不缓存挂起事务或改变候选预算 |
| `CombatBeamSolver.StandPatJobs.cs` | 对原保路规则必经的 EndTurn 探针批量求值，复用固定 lane、回传标量，缓存和选择仍由 coordinator 原序完成 |
| `CombatBeamSolver.RetentionJobs.cs` | 剪枝只读索引作业；复用空闲固定 lane，按原索引收集输出，排空后统一记账并传播取消/错误 |
| `ParallelExpansionWorkProfile.cs` | coordinator 所有的作业经过时间分布与 wave/等待/提交计时；不代表 CPU 时间 |
| `CombatBeamSolver.PathDiagnostics.cs` | 可选路径观察的值复制与边界配对；分别记录生成、两类转置、实际展开、动作准入、完整保留及回合注释，不写搜索策略或账本 |
| `CombatBeamSolver.Retention.cs` | prune/retention 调用边界与相关小型辅助 |
| `CombatBeamSolver.BeamRetentionPolicy.cs` | 状态去重、中间分数排序、多样性通道、动作/回合开始选牌保路、药水配额和小型 Pareto |
| `CombatBeamSolver.CyclePlanning.cs` | 精确动作周期、通用收益与出口探针；按周期族和回合记账的有限观察与成长预算 |
| `CombatBeamSolver.CycleRegionRetention.cs` | 合并同回合、同控制形状的动作排列；对最终存活候选事务式提交区域保留预算与进展证据 |
| `CombatBeamSolver.OrderedMutationRetention.cs` | 有序操作碰撞的谱系、租约、成对激活和预算账本；统一处理续接、到期与普通通道回退 |
| `CombatBeamSolver.FinalPlanOrdering.cs` | 终局胜负、偷窃、战损、药水、卖血和搜索边界排序 |
| `CombatBeamSolver.StateEvaluation.cs` | 搜索快照、评分、威胁、stand-pat 和状态特征；手牌可达价值的纯背包计算委托 `ReachableHandValue` |
| `CombatBeamSolver.NoveltySearch.cs` | 有界新颖性队列与影子特征提取；复用既有展开、终局与 Phases 注入边界 |
| `CombatBeamSolver.Terminal.cs` | 终局精确回放、逐回合结果、击杀与遗物标注 |
| `StrategicEffectModel.cs` | 把 Power 的实际触发语义投影为伤害、防伤、资源、牌访问和成长效果；不决定终局胜负 |

`SearchRunContext` 只活于一次 solver：计数器、性能指标、节流器、转置表、stand-pat/威胁/coverage/路由缓存和 `OwnedExpansionBatch` 容器池均在这里。每个 lane 最多保留两个已清空 storage，单容器容量上限 4096；批次 lease 独立且 Dispose 幂等，检查点丢弃空闲池，不池化 simulator/model。根配置留在 solver，不把可变运行状态退回入口文件。 `SnapshotListBuffer<PredictedCard>` 也归各自 `_run` 所有，只缓存一个已清空、实际容量不超过 4096 的临时列表；快照内用栈上 lease，嵌套租用取独立 storage，generation 防止旧 lease 触碰新租户。牌序与 Shuffle RNG 克隆照旧，列表不得逃出 Snapshot，worker 排空后的缓存检查点丢弃空闲 storage。

`PotionStrategicCostLookup` 同样归单次 `SearchRunContext` 所有，中间保路与终局排序共用规范药水 ID/可再生条件对应的只读代价值；未命中仍调用原目录的 `Single` 查询，保留缺失/重复 ID 的失败行为。每个 worker 有独立表，不存药水实例或分支值，也不跨并发 solver 共享修改。快照内 Power 是否贡献战略估值只判定一次并暂存在当前调用的栈/数组中，需求收集与评分复用同一判定，不跨快照缓存。
````

## C01：src/Search/MultiplayerSearchPolicy.cs

[固定提交原文件](https://github.com/sgpsdd-zyx/CombatSolver/blob/f220a6bc4ff8e49d32c41b2edd6ea1ab849039dd/src/Search/MultiplayerSearchPolicy.cs)

当前多人政策、扣血账本及超额评分。

源文件第 1 至 74 行，基线 `f220a6b`。

````csharp
namespace CombatSolver;

internal sealed record MultiplayerSearchPolicy(
    int Horizon = 7,
    IReadOnlyList<IReadOnlyList<PlanAction>>? PreviousRoutes = null,
    int AcceptableHpLossPerTurn = 3)
{
    public SearchPolicySnapshot Apply(SearchPolicySnapshot policy)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(AcceptableHpLossPerTurn);
        ArgumentOutOfRangeException.ThrowIfLessThan(Horizon, 1);
        return policy with
        {
            Multiplayer = this,
            IncludeTurnSetup = false,
            IgnoreLongTermRewards = true,
            RelicTargets = [],
            TheftPolicy = null,
            Act3BossStrategy = false,
            StopAtAcceptableBattleHpLoss = false,
            UseNoveltyPortfolio = false,
            UseBeamWidthPortfolio = false,
            NoveltySearch = null,
        };
    }
}

// Search history, not combat state: unused allowance never carries into another enemy cycle.
internal readonly record struct MultiplayerHpLossBudget(
    int CompletedExcessHpLost,
    int CurrentCycleHpLost,
    int MaximumCycleHpLost)
{
    public int ExcessHpLost(int allowance)
        => checked(CompletedExcessHpLost + Math.Max(0, CurrentCycleHpLost - allowance));

    public static MultiplayerHpLossBudget Capture(SearchNode? parent, SimulationSnapshot snapshot)
    {
        if (snapshot.AdvisoryHpLossAllowance < 0) return default;
        if (parent == null)
        {
            int initial = checked(snapshot.AdvisoryRootHpLost + snapshot.CumulativePlayerHpLost);
            return new(0, initial, initial);
        }
        MultiplayerHpLossBudget prior = parent.AdvisoryHpLoss;
        int loss = snapshot.CumulativePlayerHpLost - parent.Snapshot.CumulativePlayerHpLost;
        if (loss < 0 || snapshot.AdvisoryEnemyCycles < parent.Snapshot.AdvisoryEnemyCycles)
            throw new InvalidOperationException("Multiplayer damage history moved backwards.");
        int allowance = snapshot.AdvisoryHpLossAllowance;
        if (snapshot.AdvisoryEnemyCycles == parent.Snapshot.AdvisoryEnemyCycles)
            return prior.Advance(loss, enemyCycleEnded: false, allowance);
        int cycleEndLoss = snapshot.AdvisoryLastEnemyCycleHpLost - parent.Snapshot.CumulativePlayerHpLost;
        if (snapshot.AdvisoryEnemyCycles != parent.Snapshot.AdvisoryEnemyCycles + 1
            || cycleEndLoss < 0 || cycleEndLoss > loss)
            throw new InvalidOperationException("Multiplayer route skipped an enemy-cycle damage checkpoint.");
        return prior.Advance(cycleEndLoss, enemyCycleEnded: true, allowance)
            .Advance(loss - cycleEndLoss, enemyCycleEnded: false, allowance);
    }

    internal MultiplayerHpLossBudget Advance(int hpLost, bool enemyCycleEnded, int allowance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hpLost);
        ArgumentOutOfRangeException.ThrowIfNegative(allowance);
        int current = checked(CurrentCycleHpLost + hpLost);
        int maximum = Math.Max(MaximumCycleHpLost, current);
        return enemyCycleEnded
            ? new(checked(CompletedExcessHpLost + Math.Max(0, current - allowance)), 0, maximum)
            : new(CompletedExcessHpLost, current, maximum);
    }

    public static double ApplyScore(double score, SearchNode? parent, SimulationSnapshot snapshot)
        => snapshot.AdvisoryHpLossAllowance < 0 ? score
            : score - Capture(parent, snapshot).ExcessHpLost(snapshot.AdvisoryHpLossAllowance) * 100000d;
}
````

## C02：src/Search/CombatBeamSolver.Multiplayer.cs

[固定提交原文件](https://github.com/sgpsdd-zyx/CombatSolver/blob/f220a6bc4ff8e49d32c41b2edd6ea1ab849039dd/src/Search/CombatBeamSolver.Multiplayer.cs)

终局比较、多人 Beam 保留与旧路线重放。

源文件第 1 至 160 行，基线 `f220a6b`。

````csharp
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace CombatSolver;

internal sealed partial class CombatBeamSolver
{
    internal SimulationSnapshot ReplayMultiplayerForTesting(IReadOnlyList<PlanAction> actions)
        => IsMultiplayerAdvice ? Replay(actions)
            : throw new InvalidOperationException("Multiplayer contract requires an advisory policy.");

    private int CompareMultiplayerPlans(SearchNode left, SearchNode right)
    {
        SimulationSnapshot a = left.Snapshot, b = right.Snapshot;
        bool forcedA = _potionStrategy.EvaluateForcedUses(left.Actions, root.HasRenewablePotionShapedRock).AllForcedUsesSatisfied;
        bool forcedB = _potionStrategy.EvaluateForcedUses(right.Actions, root.HasRenewablePotionShapedRock).AllForcedUsesSatisfied;
        int comparison = forcedB.CompareTo(forcedA);
        if (comparison != 0) return comparison;
        comparison = a.PlayerDead.CompareTo(b.PlayerDead);
        if (comparison != 0) return comparison;
        // A short unfinished route has not established safety through the same window.
        bool completeA = a.AllEnemiesDead || a.BoundaryReason == SearchBoundaryReason.AdvisoryHorizon;
        bool completeB = b.AllEnemiesDead || b.BoundaryReason == SearchBoundaryReason.AdvisoryHorizon;
        comparison = completeB.CompareTo(completeA);
        if (comparison != 0) return comparison;
        if (!completeA)
        {
            comparison = b.AdvisoryEnemyCycles.CompareTo(a.AdvisoryEnemyCycles);
            if (comparison != 0) return comparison;
        }
        comparison = a.DeathSaveUseCount.CompareTo(b.DeathSaveUseCount);
        if (comparison != 0) return comparison;
        int allowance = policy.Multiplayer!.AcceptableHpLossPerTurn;
        comparison = left.AdvisoryHpLoss.ExcessHpLost(allowance)
            .CompareTo(right.AdvisoryHpLoss.ExcessHpLost(allowance));
        if (comparison != 0) return comparison;
        comparison = b.AllEnemiesDead.CompareTo(a.AllEnemiesDead);
        if (comparison != 0) return comparison;
        comparison = a.EnemyHp.CompareTo(b.EnemyHp);
        if (comparison != 0) return comparison;
        comparison = a.CumulativePlayerHpLost.CompareTo(b.CumulativePlayerHpLost);
        if (comparison != 0) return comparison;
        comparison = b.TeamSurvivors.CompareTo(a.TeamSurvivors);
        if (comparison != 0) return comparison;
        if (a.AllEnemiesDead && b.AllEnemiesDead)
        {
            comparison = Nullable.Compare(a.CombatEndedTurn, b.CombatEndedTurn);
            if (comparison != 0) return comparison;
        }
        comparison = left.PotionCount.CompareTo(right.PotionCount);
        if (comparison != 0) return comparison;
        comparison = right.Score.CompareTo(left.Score);
        return comparison != 0 ? comparison : left.ActionCount.CompareTo(right.ActionCount);
    }

    private sealed partial class BeamRetentionPolicy
    {
        private List<SearchNode> RankMultiplayer(IEnumerable<SearchNode> nodes, int limit, bool finalQualityFirst)
        {
            if (finalQualityFirst)
            {
                List<SearchNode> final = nodes.Order(Comparer<SearchNode>.Create(_advisoryComparison!)).Take(limit).ToList();
                for (int index = 0; index < final.Count; index++) final[index].RetentionRank = index;
                return final;
            }
            var ranked = nodes.GroupBy(node => (node.StateKey,
                    node.AdvisoryHpLoss.CompletedExcessHpLost, node.AdvisoryHpLoss.CurrentCycleHpLost))
                .Select(group => group.OrderByDescending(node => node.Score)
                    .ThenBy(node => node.Snapshot.CumulativePlayerHpLost).ThenBy(node => node.ActionCount).First())
                .ToList();
            SortByBeamRank(ranked);
            List<SearchNode> retained = [];
            HashSet<SearchNode> selected = new(ReferenceEqualityComparer.Instance);
            void Take(IEnumerable<SearchNode> lane, int count)
            {
                foreach (SearchNode node in lane)
                {
                    if (count == 0 || retained.Count == limit) break;
                    if (!selected.Add(node)) continue;
                    retained.Add(node);
                    count--;
                }
            }
            Take(ranked, 1);
            // Reserve bounded alternatives before filling ordinary score slots. All lanes
            // share the same beam width and request budget, and retain complete party states.
            IEnumerable<SearchNode> living = ranked.Where(node => !node.Snapshot.PlayerDead);
            int quota = Math.Max(1, limit / 4);
            Take(living.OrderBy(node => node.AdvisoryHpLoss.ExcessHpLost(node.Snapshot.AdvisoryHpLossAllowance))
                .ThenByDescending(node => node.Snapshot.PlayerHp).ThenByDescending(node => node.Snapshot.PlayerBlock)
                .ThenByDescending(node => node.Score), quota);
            Take(living.OrderBy(node => node.Snapshot.EnemyHp).ThenByDescending(node => node.Score), quota);
            Take(living.OrderByDescending(node => node.Snapshot.PersistentBuffValue)
                .ThenByDescending(node => node.Snapshot.LatentSetupValue)
                .ThenByDescending(node => node.Snapshot.ReachableHandValue).ThenByDescending(node => node.Score), quota);
            Take(ranked, limit);
            SortByBeamRank(retained);
            for (int index = 0; index < retained.Count; index++) retained[index].RetentionRank = index;
            return retained;
        }
    }

    private void SeedMultiplayerRoutes(List<SearchNode> frontier)
    {
        if (policy.Multiplayer?.PreviousRoutes is not { Count: > 0 } routes) return;
        SearchNode rootNode = frontier[0];
        long started = System.Environment.TickCount64;
        // Keep only pure action data across requests. Every reused edge pays for a new
        // simulation and contributes to this request's normal transition accounting.
        foreach (IReadOnlyList<PlanAction> route in routes.Take(4))
        {
            SearchNode node = rootNode;
            foreach (PlanAction action in route.Where(action => action.Turn >= _startTurnNumber).Take(32))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_run.Expanded >= _profile.MaxExpandedNodes
                    || System.Environment.TickCount64 - started >= _profile.SoftTimeBudgetMilliseconds)
                    break;
                if (action.Turn != _startTurnNumber || action.Kind == PlanActionKind.EndTurn
                    || action.EndsPlayerTurn || action.Choice != null
                    || action.NestedChoices is { Count: > 0 } || action.TurnStartChoices is { Count: > 0 })
                    break;
                if (!CanReplayMultiplayerAction(node, action)) break;
                _run.Expanded++;
                SimulationSnapshot snapshot = ReplayAction(node, action);
                var next = new SearchNode(action, node.ActionCount + 1, snapshot.PotionUseCount,
                    snapshot.PotionStrategicCost, snapshot.Turn, node.Traits, 0, snapshot.Score,
                    snapshot.StateKey, snapshot.HasRisk, snapshot.BoundaryReason,
                    snapshot.PlayerDead || snapshot.AllEnemiesDead || snapshot.BoundaryReason != SearchBoundaryReason.None,
                    node, snapshot, CombatProgressState.Capture(snapshot))
                { CumulativeEnemyHpLost = AccumulateEnemyHpLost(node, snapshot) };
                _run.ReplayedAdviceActions++;
                if (!ReferenceEquals(node, rootNode)) node.Snapshot.ReleaseSimulator();
                node = next;
                if (node.IsTerminal) break;
            }
            if (!ReferenceEquals(node, rootNode)) frontier.Add(node);
        }
    }

    private bool CanReplayMultiplayerAction(SearchNode node, PlanAction action)
    {
        var simulator = node.Snapshot.Simulator;
        var combat = (SimulatedCombatState)simulator.State.CombatState;
        Creature? target = combat.GetCreature(action.TargetCombatId);
        if (action.TargetCombatId != null && (target == null || !simulator.State.IsHittable(target)))
            return false;
        if (action.Kind == PlanActionKind.UsePotion)
        {
            var potion = combat.GetPotionAtSlot(_player, action.PotionSlot);
            return potion != null && potion.Id.Entry == action.PotionId
                && AllowsPotionUse(action.PotionSlot, potion.Id.Entry)
                && TargetsForPotion(potion, simulator).Any(item => ReferenceEquals(item.Target, target));
        }
        var card = FindCardForReplay(simulator.State.GetPlayerCombatState(_player).Hand.Cards, action);
        return card != null && combat.CanPlayCard(simulator, card)
            && TargetsFor(card, simulator).Any(item => ReferenceEquals(item.Target, target));
    }
}
````

## C03：src/Search/CombatBeamSolver.StateEvaluation.cs

[固定提交原文件](https://github.com/sgpsdd-zyx/CombatSolver/blob/f220a6bc4ff8e49d32c41b2edd6ea1ab849039dd/src/Search/CombatBeamSolver.StateEvaluation.cs)

有效敌人生命、终局/风险判定、中间评分、多人结果字段及状态键摘录。

源文件第 48 至 118 行，基线 `f220a6b`。

````csharp
        CombatPredictionSimulator simulator,
        int turn,
        int actionCount,
        int shufflesCrossed,
        SearchBoundaryReason boundary,
        IReadOnlySet<uint> processedEnemyDeaths)
    {
        SimCreatureState player = simulator.State.GetCreature(_player.Creature);
        SimulatedCombatState combat = (SimulatedCombatState)simulator.State.CombatState;
        int enemyHp = 0;
        int enemyBlock = 0;
        int rawEnemyHp = 0;
        int maxCurrentEnemyHp = 0;
        int revivingEnemyCount = 0;
        int aliveEnemyCount = 0;
        ulong aliveEnemyMask = 0;
        EnemyDurabilityVectorBuilder enemyDurabilityBuilder =
            new(combat.KnownEnemies.Count);
        StateFingerprintBuilder enemyCombatDistribution = new();
        for (int index = 0; index < combat.KnownEnemies.Count; index++)
        {
            Creature creature = combat.KnownEnemies[index];
            SimCreatureState enemy = simulator.State.GetCreature(creature);
            int effectiveHp = combat.EffectiveEnemyHp(creature, enemy);
            enemyCombatDistribution.Add(creature.CombatId ?? uint.MaxValue);
            enemyCombatDistribution.Add(enemy.CurrentHp);
            enemyCombatDistribution.Add(enemy.Block);
            enemyCombatDistribution.Add(effectiveHp);
            enemyCombatDistribution.Add(combat.ContainsCreature(creature));
            enemyDurabilityBuilder.Set(index, new EnemyDurabilityEntry(
                creature.CombatId ?? uint.MaxValue,
                Math.Max(0, effectiveHp) + Math.Max(0, enemy.Block)));
            enemyHp += effectiveHp;
            if (effectiveHp > 0 && combat.ContainsCreature(creature))
                enemyBlock += Math.Max(0, enemy.Block);
            rawEnemyHp += Math.Max(0, enemy.CurrentHp);
            maxCurrentEnemyHp = Math.Max(maxCurrentEnemyHp, Math.Max(0, enemy.CurrentHp));
            if (enemy.CurrentHp <= 0 && effectiveHp > 0)
                revivingEnemyCount++;
            if (effectiveHp > 0 && combat.ContainsCreature(creature))
            {
                aliveEnemyCount++;
                aliveEnemyMask |= 1UL << index;
            }
        }
        StateFingerprint enemyCombatDistributionKey = enemyCombatDistribution.Finish();
        if (boundary == SearchBoundaryReason.None && combat.HasPendingChoice)
            boundary = SearchBoundaryReason.PendingChoice;
        // Later effects may restore HP after native pending loss has already been committed.
        bool dead = player.IsDead || simulator.TerminalStamp is { Outcome: CombatTerminalOutcome.Defeat };
        bool won = boundary != SearchBoundaryReason.EventDefeat
            && !dead
            && !combat.HasPendingChoice
            && simulator.TerminalStamp is { Outcome: CombatTerminalOutcome.Victory };
        CoverageSummary coverage = GetCoverageSummary(simulator);
        IReadOnlyList<PredictionGap> predictionGaps = coverage.Gaps;
        bool risk = coverage.HasUncompensatedRisk;
        if (IsMultiplayerAdvice && risk)
        {
            won = false;
            if (boundary is SearchBoundaryReason.None or SearchBoundaryReason.AdvisoryHorizon)
                boundary = SearchBoundaryReason.UnsupportedEffect;
        }
        bool uncertainVictory = won && HasUncompensatedDeathGap(predictionGaps);
        if (uncertainVictory)
            boundary = SearchBoundaryReason.UnsupportedEffect;

        SimPlayerCombatState playerState = simulator.State.GetPlayerCombatState(_player);
        SearchMeasurement fingerprintMeasurement = _run.Performance.Begin();
        StateFingerprint key = BuildStateKey(
            turn,
````

源文件第 509 至 533 行，基线 `f220a6b`。

````csharp
        int automaticPotionUseCount = combat.PotionUses.Count(use => use.Automatic);
        (int reachableHandValue, int zeroCostPlayableCount) =
            CalculateReachableHandPotential(simulator, combat, playerState);
        StateFingerprint potionInventoryKey = BuildPotionInventoryKey(combat);
        StateFingerprint cycleShapeKey = BuildCycleShapeKey(
            cyclePileShapeKey,
            aliveEnemyMask,
            potionInventoryKey,
            boundary);
        if (IsMultiplayerAdvice)
        {
            // Nodes charge only per-cycle losses above the allowance. These terms guide
            // unfinished actions; safety is checked using actual simulated enemy cycles.
            score = (dead ? -1e12 : 0) + (won ? 1e11 : 0)
                - enemyHp * 100d
                + Math.Min(player.Block, combat.CurrentMonsterMoves().Sum(move => move.AttackHits.Sum(hit => hit.Damage))) * 5d
                + persistentBuffValue * 20d + reachableHandValue + playerState.Energy * 2d
                - potionUseCount * 0.1d - actionCount * 0.001d;
        }
        return new SimulationSnapshot(
            score,
            key,
            unorderedPileKey,
            cycleShapeKey,
            projectedShuffleOrderKey,
````

源文件第 595 至 613 行，基线 `f220a6b`。

````csharp
            automaticPotionUseCount,
            turn,
            shufflesCrossed,
            processedEnemyDeaths,
            boundary,
            predictionGaps,
            simulator,
            simulator.TerminalStamp)
        {
            TeamSurvivors = IsMultiplayerAdvice
                ? combat.Players.Count(peer => simulator.State.GetCreature(peer.Creature).IsAlive) : 0,
            AdvisoryEnemyCycles = IsMultiplayerAdvice ? combat.AdvisorEnemyCycles : 0,
            AdvisoryHpLossAllowance = policy.Multiplayer?.AcceptableHpLossPerTurn ?? -1,
            AdvisoryRootHpLost = IsMultiplayerAdvice ? root.InitialPlayerRoundHpLost : 0,
            AdvisoryLastEnemyCycleHpLost = IsMultiplayerAdvice ? combat.AdvisorLastEnemyCycleHpLost : 0,
            GrowthHpCredit = growthHpCredit,
            RelicCounters = relicCounters,
            GrowthRewards = growthRewards,
            BrightestFlameMaxHpSpent = combat.BrightestFlameMaxHpSpent,
````

源文件第 1555 至 1615 行，基线 `f220a6b`。

````csharp
    private StateFingerprint BuildStateKey(
        int turn,
        SimCreatureState player,
        SimPlayerCombatState playerState,
        SimulatedCombatState simulatedCombat,
        CombatPredictionSimulator simulator,
        int shufflesCrossed,
        IReadOnlySet<uint> processedEnemyDeaths)
    {
        StateFingerprintBuilder key = new();
        key.Add(turn);
        key.Add(player.CurrentHp);
        key.Add(player.MaxHp);
        key.Add(player.Block);
        key.Add(playerState.Energy);
        key.Add((int)playerState.Phase);
        key.Add(playerState.Stars);
        key.Add(shufflesCrossed);
        if (IsMultiplayerAdvice)
        {
            key.Add(_player.NetId);
            key.Add(simulatedCombat.RoundNumber);
            key.Add((int)simulatedCombat.CurrentSide);
            key.Add(policy.Multiplayer!.Horizon - simulatedCombat.AdvisorEnemyCycles);
            key.Add(simulatedCombat.AdvisorExtraTurnPlayers.Count);
            foreach (Player extra in simulatedCombat.AdvisorExtraTurnPlayers) key.Add(extra.NetId);
            foreach (Player peer in simulatedCombat.Players)
            {
                var creature = simulator.State.GetCreature(peer.Creature);
                var peerState = simulator.State.GetPlayerCombatState(peer);
                key.Add(peer.NetId);
                key.Add(peer.Creature.CombatId ?? uint.MaxValue);
                key.Add(creature.CurrentHp);
                key.Add(creature.MaxHp);
                key.Add(creature.Block);
                key.Add(peerState.Energy);
                key.Add(peerState.Stars);
                key.Add((int)peerState.Phase);
                key.Add(simulatedCombat.GetPlayerTurnNumber(peer));
                key.Add(simulatedCombat.GetPlayerGold(peer));
                AppendPile(ref key, peerState.Hand, 'H');
                AppendPile(ref key, peerState.DrawPile, 'D');
                AppendPile(ref key, peerState.DiscardPile, 'C');
                AppendPile(ref key, peerState.ExhaustPile, 'X');
                AppendPile(ref key, peerState.PlayPile, 'P');
                AppendOrbs(ref key, simulator, peerState.OrbQueue);
                if (simulatedCombat.GetOsty(peer) is { } peerOsty)
                {
                    key.Add(simulator.State.GetCreature(peerOsty).CurrentHp);
                    key.Add(simulator.State.GetCreature(peerOsty).Block);
                    key.Add(simulatedCombat.GetOstyMaxHp(simulator, peer));
                }
            }
        }
        Player owner = _player;
        if (simulatedCombat.GetOsty(owner) is { } osty)
        {
            key.Add(simulator.State.GetCreature(osty).CurrentHp);
            key.Add(simulatedCombat.GetOstyMaxHp(simulator, owner));
            key.Add(simulatedCombat.IsOstyHittable(simulator, owner));
        }
````

## C04：src/Search/CombatBeamSolver.Transpositions.cs

[固定提交原文件](https://github.com/sgpsdd-zyx/CombatSolver/blob/f220a6bc4ff8e49d32c41b2edd6ea1ab849039dd/src/Search/CombatBeamSolver.Transpositions.cs)

当前转置标签和支配比较。

源文件第 1 至 60 行，基线 `f220a6b`。

````csharp
namespace CombatSolver;

internal sealed partial class CombatBeamSolver
{
    private readonly record struct TranspositionLabel(
        int PotionCount,
        int PotionStrategicCost,
        int FutureSoldHp,
        int CumulativePlayerHpLost,
        int ActionCount,
        double Score,
        int AdvisoryCompletedExcessHpLost = 0,
        int AdvisoryCurrentCycleHpLost = 0);

    private sealed class TranspositionFrontier(TranspositionLabel first)
    {
        // Most retained states have one nondominated label. Keep it in this object:
        // neither an empty tail nor a one-element List/array needs to survive a GC.
        private TranspositionLabel _single = first;
        private List<TranspositionLabel>? _labels;

        public bool TryAccept(TranspositionLabel next)
        {
            if (_labels == null)
            {
                if (Dominates(_single, next))
                    return false;
                if (Dominates(next, _single))
                    _single = next;
                else
                    _labels = [_single, next];
                return true;
            }
            foreach (TranspositionLabel current in _labels)
            {
                if (Dominates(current, next))
                    return false;
            }
            _labels.RemoveAll(current => Dominates(next, current));
            _labels.Add(next);
            if (_labels.Count == 1)
            {
                _single = next;
                _labels = null;
            }
            return true;
        }

        private static bool Dominates(TranspositionLabel left, TranspositionLabel right)
            => left.PotionCount <= right.PotionCount
                && left.PotionStrategicCost <= right.PotionStrategicCost
                && left.FutureSoldHp <= right.FutureSoldHp
                && left.CumulativePlayerHpLost <= right.CumulativePlayerHpLost
                && left.AdvisoryCompletedExcessHpLost <= right.AdvisoryCompletedExcessHpLost
                && left.AdvisoryCurrentCycleHpLost <= right.AdvisoryCurrentCycleHpLost
                && left.ActionCount <= right.ActionCount
                && left.Score >= right.Score;
    }

}
````

## C05：src/Search/CombatSearchCoordinator.cs

[固定提交原文件](https://github.com/sgpsdd-zyx/CombatSolver/blob/f220a6bc4ff8e49d32c41b2edd6ea1ab849039dd/src/Search/CombatSearchCoordinator.cs)

多人请求绕开单人组合与药水审计的入口。

源文件第 1 至 27 行，基线 `f220a6b`。

````csharp
using System.Diagnostics;
using MegaCrit.Sts2.Core.Combat;

namespace CombatSolver;

internal static partial class CombatSearchCoordinator
{
    public static SolverResult Solve(
        CombatRootSnapshot root,
        SolverDisplayNames displayNames,
        BattleDamageSnapshot battleDamage,
        SearchPolicySnapshot policy,
        CancellationToken cancellationToken,
        Action<SolverProgress>? progressCallback)
    {
        if (policy.Multiplayer != null)
        {
            SolverSearchProfile advisoryProfile = policy.BudgetOverrideMilliseconds is { } budget
                ? policy.Profile with { SoftTimeBudgetMilliseconds = budget } : policy.Profile;
            SolverResult advice = new CombatBeamSolver(root, displayNames, battleDamage, policy,
                cancellationToken, progressCallback, advisoryProfile).Solve();
            advice.TotalSearchElapsed = advice.Elapsed;
            advice.SingleSessionSearch = true;
            return advice;
        }
        SearchRequestWorkTotals requestWorkTotals = new();
        BeamWidthPortfolioTelemetry portfolioTelemetry = new();
````

## C06：src/Runtime/CombatRootSnapshot.cs

[固定提交原文件](https://github.com/sgpsdd-zyx/CombatSolver/blob/f220a6bc4ff8e49d32c41b2edd6ea1ab849039dd/src/Runtime/CombatRootSnapshot.cs)

主线程根捕获、本机身份、本周期已付扣血和完整状态核对。

源文件第 130 至 225 行，基线 `f220a6b`。

````csharp
    public static CombatRootSnapshot Capture(CombatState state)
        => Capture(state, SolverController.IsMultiplayerSession);

    internal static CombatRootSnapshot Capture(CombatState state, bool multiplayerAdvisor)
    {
        if (!NGame.IsMainThread())
            throw new InvalidOperationException("Combat root snapshot must be captured on the main thread.");
        Engine.InCombat.Mirrors.Hooks.TurnEnd.AfterSideTurnEndLateMirrors.Seal();
        Stopwatch stopwatch = Stopwatch.StartNew();

        PowerDynamicVarWarmup.EnsureMaterialized(state);
        CardDynamicVarWarmup.EnsureMaterialized(state);

        // Listener enumeration and third-party owner discovery are part of root capture.
        // Take the baseline first so any semantic mutation in those callbacks is rejected by
        // the existing after-capture stamp without paying for another full serialization.
        bool advisor = multiplayerAdvisor;
        ContinuationStamp continuationBefore = ContinuationStamp.CaptureLive(state, advisor);
        LiveCombatStamp liveBefore = LiveCombatStamp.FromContinuation(continuationBefore);

        Player player = LocalContext.GetMe(state)
            ?? throw new InvalidOperationException("找不到本地玩家。");
        PlayerCombatState playerState = player.PlayerCombatState
            ?? throw new InvalidOperationException("玩家没有战斗状态。");
        int initialPlayerRoundHpLost = advisor
            ? CombatManager.Instance.History.Entries.OfType<DamageReceivedEntry>()
                .Where(entry => entry.Receiver == player.Creature && entry.RoundNumber == state.RoundNumber)
                .Sum(entry => entry.Result.UnblockedDamage)
            : 0;
        AbstractModel[] liveCombatHookListeners = state.IterateHookListeners().ToArray();
        if (liveCombatHookListeners.Any(PredictionModModelSupport.IsBaseLibCardModifier))
        {
            PredictionModModelSupport.RegisterBaseLibCardModifierOwners(
                state.Players
                    .Where(candidate => candidate.PlayerCombatState != null)
                    .SelectMany(candidate => candidate.PlayerCombatState!.AllCards));
        }
        IntentForecast forecast = IntentForecaster.Build(state, SolverWeights.SetupValueHorizonTurns);

        SimulatedCombatState simulatedCombat = new(state, liveCombatHookListeners);
        simulatedCombat.AdvisorPlayer = advisor ? player : null;
        if (advisor)
            simulatedCombat.AdvisorExtraTurnPlayers = CombatManager.Instance.PlayersTakingExtraTurn.ToArray();
        CombatPredictionSimulator simulator = new(simulatedCombat);
        ContinuationStamp projected = ContinuationStamp.CapturePredicted(
            player,
            simulator,
            playerState.TurnNumber,
            forecast,
            playerState.TurnNumber);
        bool hasUnusedCardReplayAllocator = simulatedCombat.RelicsOf(player)
            .OfType<ThrowingAxe>()
            .Any(relic => !relic.IsMelted && !relic._usedThisCombat);
        bool hasRenewablePotionShapedRock = simulatedCombat.RelicsOf(player)
            .OfType<PetrifiedToad>()
            .Any(relic => !relic.IsMelted);
        PostCombatRelicHealProfile postCombatRelicHeal = CapturePostCombatRelicHeal(
            simulatedCombat.RelicsOf(player));
        SearchablePotionSlotSnapshot[] searchablePotions = player.PotionSlots
            .Select((potion, slot) => (Potion: potion, Slot: slot))
            .Where(item => item.Potion != null && PotionOnUseSupport.CanSearch(item.Potion))
            .Select(item => new SearchablePotionSlotSnapshot(
                item.Slot,
                item.Potion!.Id.Entry,
                PotionUsePolicy.StrategicHpCost(
                    item.Potion,
                    hasRenewablePotionShapedRock)))
            .ToArray();
        if (!string.Equals(
                continuationBefore.StateText,
                projected.StateText,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Combat root projection differs from the captured live state: " +
                continuationBefore.DescribeFirstDifference(projected));
        }

        ContinuationStamp continuationAfter = ContinuationStamp.CaptureLive(state, advisor);
        LiveCombatStamp liveAfter = LiveCombatStamp.FromContinuation(continuationAfter);
        if (!string.Equals(liveBefore.StateText, liveAfter.StateText, StringComparison.Ordinal)
            || !string.Equals(
                continuationBefore.StateText,
                continuationAfter.StateText,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Combat state changed while the root snapshot was being captured.");
        }

        ulong aliveEnemyMask = 0;
        for (int index = 0; index < state.Enemies.Count; index++)
        {
            if (state.Enemies[index].IsAlive)
                aliveEnemyMask |= 1UL << index;
        }
        int cardCount = state.Players
````

## C07：src/Runtime/SolverController.Multiplayer.cs

[固定提交原文件](https://github.com/sgpsdd-zyx/CombatSolver/blob/f220a6bc4ff8e49d32c41b2edd6ea1ab849039dd/src/Runtime/SolverController.Multiplayer.cs)

建议过期观察、结果发布和有界动作路线保存。

源文件第 1 至 44 行，基线 `f220a6b`。

````csharp
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
````

## C08：src/Search/SimulatedCombatState.Multiplayer.cs

[固定提交原文件](https://github.com/sgpsdd-zyx/CombatSolver/blob/f220a6bc4ff8e49d32c41b2edd6ea1ab849039dd/src/Search/SimulatedCombatState.Multiplayer.cs)

多人分支字段、历史窗口与外部选择边界。

源文件第 1 至 61 行，基线 `f220a6b`。

````csharp
using MegaCrit.Sts2.Core.Entities.Players;

namespace CombatSolver;

internal sealed partial class SimulatedCombatState
{
    // Stable identity only. Every mutable peer value belongs to the existing branch stores.
    internal Player? AdvisorPlayer { get; set; }
    internal IReadOnlyList<Player> AdvisorExtraTurnPlayers { get; set; } = [];
    internal int AdvisorEnemyCycles { get; set; }
    // Raw observation for Search's per-cycle allowance, before next-turn setup can cost HP.
    internal int AdvisorLastEnemyCycleHpLost { get; set; }
    internal IReadOnlyDictionary<Player, int>? AdvisorEtherealCounts { get; set; }
    internal bool ExternalChoiceReached { get; private set; }

    internal void ResetMultiplayerHistoryWindow()
    {
        // Native HappenedThisTurn compares every player's turn number, including peers
        // sitting out an extra turn. Power lifecycle counters remain participant-owned.
        foreach (var creature in Creatures)
        {
            ResetTurnCounter(ref _cardsPlayedThisTurn, creature);
            ResetTurnCounter(ref _manualCardsPlayedThisTurn, creature);
            ResetTurnCounter(ref _attacksPlayedThisTurn, creature);
            ResetTurnCounter(ref _shivsPlayedThisTurn, creature);
            ResetTurnCounter(ref _blockCardsPlayedThisTurn, creature);
            ResetTurnCounter(ref _skillCardsPlayedThisTurn, creature);
            ResetTurnCounter(ref _cardsExhaustedThisTurn, creature);
            ResetTurnCounter(ref _cardsDiscardedThisTurn, creature);
            ResetTurnCounter(ref _creatureAttacksThisTurn, creature);
            ResetTurnCounter(ref _cardPlaySeriesStartedThisTurn, creature);
            ResetTurnCounter(ref _zeroCostAttackStartsThisTurn, creature);
            ResetTurnCounter(ref _attackPlayStartsThisTurn, creature);
            ResetTurnCounter(ref _cardPlayStartsThisTurn, creature);
            ResetTurnCounter(ref _attackSkillStartsThisTurn, creature);
        }
        foreach (Player player in Players)
        {
            ResetTurnCounter(ref _energySpentThisTurn, player);
            ResetTurnCounter(ref _starsGainedThisTurn, player);
            ResetTurnCounter(ref _nonHandDrawsThisTurn, player);
            ResetTurnCounter(ref _statusCardsDrawnThisTurn, player);
        }
        _fetchCardsPlayedThisTurn?.Clear();
        _unblockedDamageThisTurn = null;
        _poweredAttackHitsThisTurn?.Clear();
        _doomAppliersThisTurn?.Clear();
    }

    internal void RequireLocalChoice(Player player)
    {
        if (AdvisorPlayer != null && !ReferenceEquals(AdvisorPlayer, player))
        {
            ExternalChoiceReached = true;
            throw new ExternalPlayerChoiceException(player.NetId);
        }
    }
}

internal sealed class ExternalPlayerChoiceException(ulong playerId)
    : InvalidOperationException($"Prediction reached a choice owned by teammate {playerId}.");
````

## C09：src/Search/CombatBeamSolver.MultiplayerRound.cs

[固定提交原文件](https://github.com/sgpsdd-zyx/CombatSolver/blob/f220a6bc4ff8e49d32c41b2edd6ea1ab849039dd/src/Search/CombatBeamSolver.MultiplayerRound.cs)

完整多人玩家阶段调度。

源文件第 1 至 156 行，基线 `f220a6b`。

````csharp
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CombatSolver;

internal sealed partial class CombatBeamSolver
{
    private bool EndMultiplayerPlayerTurn(CombatPredictionSimulator simulator,
        SimulatedCombatState combat, ISet<uint> deaths, out bool extraTurn)
    {
        extraTurn = false;
        IReadOnlyList<Player> players = combat.AdvisorExtraTurnPlayers.Count > 0
            ? combat.AdvisorExtraTurnPlayers : combat.Players;
        Creature[] participants = players.Select(player => player.Creature).ToArray();
        List<Player> extra = [];
        foreach (Player player in players)
        {
            if (!combat.TryPrepareExtraPlayerTurn(simulator, player, out bool takingExtra, out _))
                return false;
            if (takingExtra) extra.Add(player);
        }
        combat.AdvisorEtherealCounts = players.ToDictionary(player => player,
            player => combat.CountEtherealCardsInHand(simulator, player));
        if (!PlayerTurnEndLifecycle.RunPhaseOne(simulator, combat, _player, participants)) return false;
        foreach (Player player in players) combat.CommitHistoryCourseTurn(player);
        combat.NormalizeAeonglassWithers(simulator);
        combat.NormalizeCardAfflictions(simulator);
        if (!CorePowerSupport.ApplyEnemyDeathPowers(simulator, combat, combat.KnownEnemies, deaths)) return false;
        if (!simulator.IsInProgress) return true;
        foreach (Player player in players)
        {
            if (simulator.State.GetCreature(player.Creature).IsDead) continue;
            CorePowerSupport.FlushPlayerHandAtTurnEnd(simulator, combat, player);
            if (combat.HasPendingChoice) return false;
        }
        if (!PlayerTurnEndLifecycle.RunPhaseTwo(simulator, combat, participants)) return false;
        combat.AdvisorEtherealCounts = null;
        if (!CorePowerSupport.ApplyEnemyDeathPowers(simulator, combat, combat.KnownEnemies, deaths)) return false;
        combat.AdvisorExtraTurnPlayers = extra.ToArray();
        foreach (Player player in extra) combat.ConsumeExtraTurnSources(player);
        extraTurn = extra.Count > 0;
        return true;
    }

    private SearchBoundaryReason StartMultiplayerPlayerTurn(CombatPredictionSimulator simulator,
        SimulatedCombatState combat, ISet<uint> deaths, ref int shufflesCrossed,
        TurnStartChoiceCursor choices, bool extraTurn)
    {
        combat.SetActionChoiceTiming(PlanChoiceTiming.PlayerTurnStart);
        combat.CurrentSide = CombatSide.Player;
        if (!extraTurn) combat.RoundNumber++;
        IReadOnlyList<Player> players = extraTurn ? combat.AdvisorExtraTurnPlayers : combat.Players;
        Creature[] participants = extraTurn
            ? players.Select(player => player.Creature).ToArray() : combat.Allies.ToArray();
        foreach (Player player in combat.Players)
            simulator.State.GetPlayerCombatState(player).Phase = PlayerTurnPhase.None;
        foreach (Player player in players) combat.AdvancePlayerTurn(player);
        combat.ResetMultiplayerHistoryWindow();
        foreach (Creature creature in participants) combat.BeginSideTurn(creature);
        combat.SnapshotPowerAmountsAtTurnStart(participants);
        if (!TurnStartRelicSupport.TriggerBeforeSideTurnStart(simulator, combat, participants)
            || TurnStartPowerSupport.TriggerBeforeSideTurnStart(simulator, combat, participants))
            return SearchBoundaryReason.PendingChoice;
        foreach (Player player in combat.Players)
            simulator.State.GetPlayerCombatState(player).Phase = PlayerTurnPhase.Start;
        foreach (Creature creature in participants)
        {
            var state = simulator.State.GetCreature(creature);
            if (state.Block > 0)
            {
                if (combat.ShouldClearBlock(creature, out AbstractModel? preventer))
                    state.DamageBlock(state.Block, ValueProp.Move);
                else PersistentRelicSupport.TriggerAfterPreventingBlockClear(simulator, preventer, creature);
            }
        }
        foreach (Creature creature in participants)
            if (!CorePowerSupport.TriggerAfterBlockCleared(simulator, combat, creature))
                return SearchBoundaryReason.PendingChoice;
        int beforeShuffles = simulator.ShuffleEventCount;
        bool PrepareHand(Player player)
        {
            if (simulator.State.GetCreature(player.Creature).IsDead) return true;
            var state = simulator.State.GetPlayerCombatState(player);
            if (PersistentRelicSupport.ShouldPlayerResetEnergy(combat, player)) state.LoseEnergy(state.Energy);
            state.GainEnergy(PersistentPowerSupport.GetModifiedMaxEnergy(combat, player)
                + combat.ConsumeEnergyNextTurn(player));
            if (!PersistentPowerSupport.TriggerAfterEnergyReset(simulator, combat, player))
                return false;
            TurnStartRelicSupport.TriggerAfterEnergyReset(simulator, combat, player);
            if (combat.HasPendingChoice) return false;
            TurnStartRelicSupport.TriggerAfterEnergyResetLate(simulator, combat, player);
            if (combat.HasPendingChoice || combat.PrepareBeforeHandDraw(simulator, player, choices))
                return false;
            int draw = PersistentPowerSupport.ConsumeModifiedHandDraw(combat, player, CombatManager.baseHandDrawCount);
            int history = simulator.History.Entries.Count;
            simulator.Draw(player, draw, fromHandDraw: true);
            if (combat.HasPendingChoice) return false;
            TriggeredPowerSupport.CompensateHistorySince(simulator, combat, history);
            if (combat.HasPendingChoice || combat.TriggerAfterPlayerTurnStart(simulator, player.Creature, choices))
                return false;
            return true;
        }
        int nextPlayer = 0;
        bool sideStarted = false;
        HashSet<Player> autoStarted = [];
        bool StartAuto(Player player)
        {
            if (!autoStarted.Add(player) || simulator.State.GetCreature(player.Creature).IsDead) return true;
            combat.TriggerAutoPrePlayEarly(simulator, player, combat.GetPlayerTurnNumber(player), choices, deaths);
            return !combat.HasPendingChoice;
        }
        bool CompleteSideStart(bool localChoicePaused)
        {
            if (sideStarted) return true;
            while (nextPlayer < players.Count)
                if (!PrepareHand(players[nextPlayer++])) return false;
            sideStarted = true;
            if (!combat.TriggerSideTurnStart(simulator, CombatSide.Player, participants,
                decrementPlating: combat.RoundNumber != 1, extraTurn)
                || !CorePowerSupport.ApplyEnemyDeathPowers(simulator, combat, combat.KnownEnemies, deaths))
                return false;
            foreach (Player player in players)
            {
                EnchantmentLifecycleSupport.TriggerAfterTurnStartOrbs(simulator, player);
                if (combat.HasPendingChoice) return false;
            }
            if (localChoicePaused)
                foreach (Player player in players)
                    if (player != _player && !StartAuto(player)) return false;
            return true;
        }
        while (nextPlayer < players.Count)
        {
            Player player = players[nextPlayer++];
            // Native setup pauses for local choices while other players, side hooks and
            // orbs finish starting. Resume that choice only after the same shared work.
            using var beforeChoice = player == _player && !sideStarted
                ? choices.BeforeNextTake(() => CompleteSideStart(localChoicePaused: true)) : null;
            if (!PrepareHand(player)) return SearchBoundaryReason.PendingChoice;
        }
        if (!CompleteSideStart(localChoicePaused: false)) return SearchBoundaryReason.PendingChoice;
        foreach (Player player in players)
            if (!StartAuto(player)) return SearchBoundaryReason.PendingChoice;
        shufflesCrossed += simulator.ShuffleEventCount - beforeShuffles;
        combat.NormalizeAeonglassWithers(simulator);
        combat.NormalizeCardAfflictions(simulator);
        combat.SetPredictedEnemyIntents(combat.CurrentMonsterMoves()
            .Where(move => move.AttackHits.Count > 0).Select(move => move.Owner));
        simulator.CheckWinCondition(combat.GetPlayerTurnNumber(_player));
        return SearchBoundaryReason.None;
    }
}
````

## C10：src/Search/CombatBeamSolver.Expansion.cs

[固定提交原文件](https://github.com/sgpsdd-zyx/CombatSolver/blob/f220a6bc4ff8e49d32c41b2edd6ea1ab849039dd/src/Search/CombatBeamSolver.Expansion.cs)

多人阶段、周期结束、转置政策标签及本机动作目标摘录。

源文件第 3205 至 3245 行，基线 `f220a6b`。

````csharp
        if (!simulator.IsInProgress)
            return SearchBoundaryReason.None;
        SimPlayerCombatState playerState = simulator.State.GetPlayerCombatState(_player);
        IReadOnlyList<PlanCardChoice>? roundChoicePlans = turnStartChoices?
            .Where(choice => choice.Effect != PlanChoiceEffect.ApplyKnowledgeCurse)
            .ToArray();
        TurnStartChoiceCursor roundChoices = new(roundChoicePlans);
        simulatedCombat.BeginActionChoices(roundChoices);
        simulatedCombat.SetActionChoiceTiming(PlanChoiceTiming.PlayerTurnEnd);
        try
        {
        while (true)
        {
        cancellationToken.ThrowIfCancellationRequested();
        int roundHistoryEntryStart = simulator.History.Entries.Count;
        bool takingExtraTurn;
        bool hasActiveEmotionChip = false;
        if (IsMultiplayerAdvice)
        {
            if (!EndMultiplayerPlayerTurn(simulator, simulatedCombat, processedEnemyDeaths,
                    out takingExtraTurn))
                return SearchBoundaryReason.PendingChoice;
            if (!simulator.IsInProgress)
                return SearchBoundaryReason.None;
        }
        else
        {
        if (!simulatedCombat.TryPrepareExtraPlayerTurn(
                simulator,
                _player,
                out takingExtraTurn,
                out hasActiveEmotionChip))
        {
            return SearchBoundaryReason.PendingChoice;
        }
        int etherealExhaustCount = simulatedCombat.CountEtherealCardsInHand(simulator, _player);
        {
            using SearchMeasurementScope _ = _run.Performance.Measure(SearchMetricPhase.RoundPlayerEnd);
            bool playerTurnEndCompleted;
            using (_run.Performance.Measure(SearchMetricPhase.RoundEndSimulation))
                playerTurnEndCompleted = PlayerTurnEndLifecycle.RunPhaseOne(
````

源文件第 3470 至 3520 行，基线 `f220a6b`。

````csharp
                {
                    return SearchBoundaryReason.PendingChoice;
                }
                TriggeredPowerSupport.CompensateHistorySince(
                    simulator,
                    simulatedCombat,
                    playerPoisonHistoryStart);
                if (simulatedCombat.HasPendingChoice)
                    return SearchBoundaryReason.PendingChoice;
                foreach (var participant in IsMultiplayerAdvice ? simulatedCombat.Players : [_player])
                {
                    simulatedCombat.ClearNoDraw(participant.Creature);
                    simulatedCombat.RecordRelicRoundDamage(simulator, participant, roundHistoryEntryStart);
                }
            }
            if (simulator.CheckWinCondition(simulatedCombat.GetPlayerTurnNumber(_player)))
                return SearchBoundaryReason.None;
            simulatedCombat.PrepareMonsterMovesForNextRound(simulator, performedMoves);
            if (IsMultiplayerAdvice)
            {
                simulatedCombat.AdvisorLastEnemyCycleHpLost = simulatedCombat.GetCumulativeHpLost(_player.Creature);
                if (++simulatedCombat.AdvisorEnemyCycles >= policy.Multiplayer!.Horizon)
                    return SearchBoundaryReason.AdvisoryHorizon;
            }
        }
        else if (!IsMultiplayerAdvice)
        {
            // An extra turn advances the player's turn number too, so damage from the
            // just-finished turn becomes Emotion Chip's "previous turn" window.
            if (hasActiveEmotionChip)
                simulatedCombat.RecordRelicRoundDamage(simulator, _player, roundHistoryEntryStart);
            simulatedCombat.ConsumeExtraTurnSources(_player);
        }

        if (IsMultiplayerAdvice)
        {
            SearchBoundaryReason started = StartMultiplayerPlayerTurn(simulator, simulatedCombat,
                processedEnemyDeaths, ref shufflesCrossed, roundChoices, takingExtraTurn);
            if (started == SearchBoundaryReason.None && simulator.IsInProgress && takingExtraTurn
                && !simulatedCombat.AdvisorExtraTurnPlayers.Contains(_player))
            {
                simulatedCombat.SetActionChoiceTiming(PlanChoiceTiming.PlayerTurnEnd);
                continue;
            }
            return started;
        }
        return AdvanceRoundPlayerStart(simulator, simulatedCombat, playerState, simulatedPlayer,
            roundIndex, processedEnemyDeaths, ref shufflesCrossed, roundChoices, takingExtraTurn,
            turnStartChoices is not { Count: > 0 } ? roundCheckpointCapture : null);
        }
        }
````

源文件第 4355 至 4395 行，基线 `f220a6b`。

````csharp
            throw new InvalidOperationException("循环出口测试票据无法签发。");
        candidate.CycleExitProbe = candidate.CycleExitProbe with { LeaseIssued = true };
        if (!HasCycleExpansionTranspositionLease(candidate))
            throw new InvalidOperationException("已签发的循环出口票据无法继续推进。");
    }

    private bool TryAcceptTransposition(SearchNode candidate)
    {
        // Scheduling obligations are deliberately bounded elsewhere. A normal route at the
        // same simulator state cannot inherit their exact pattern/envelope history, so it must
        // not erase the probe before the obligation reaches the frontier.
        if (HasCycleAdmissionTranspositionLease(candidate)
            || CanRetainOrderedMutationLease(_run, candidate))
        {
            ObserveSearchPath(candidate, SearchPathObservationStage.AdmissionTransposition, "bypass_cycle_or_ordered_lease");
            return true;
        }
        TranspositionLabel next = new(
            candidate.PotionCount,
            candidate.PotionStrategicCost,
            candidate.FutureSoldHp,
            candidate.Snapshot.CumulativePlayerHpLost,
            candidate.ActionCount,
            candidate.Score,
            candidate.AdvisoryHpLoss.CompletedExcessHpLost,
            candidate.AdvisoryHpLoss.CurrentCycleHpLost);
        if (!_run.Transpositions.TryGetValue(candidate.StateKey, out TranspositionFrontier? frontier))
        {
            _run.Transpositions.Add(candidate.StateKey, new TranspositionFrontier(next));
            ObserveSearchPath(candidate, SearchPathObservationStage.AdmissionTransposition, "accepted_new_state");
            return true;
        }
        if (frontier.TryAccept(next))
        {
            ObserveSearchPath(candidate, SearchPathObservationStage.AdmissionTransposition, "accepted_label");
            return true;
        }
        _run.TranspositionBranchesPruned++;
        ObserveSearchPath(candidate, SearchPathObservationStage.AdmissionTransposition, "rejected_dominated");
        if (_detailedDiagnostics && candidate.ActionCount <= 2)
        {
````

源文件第 4410 至 4507 行，基线 `f220a6b`。

````csharp
        {
            SearchReplayEvidence.PublishCandidateFailure(policy.Diagnostics, node, "expansion_admission_frontier");
            throw new InvalidOperationException(
                "临时循环出口 observation 越过了 action admission frontier。");
        }
        if (HasCycleExpansionTranspositionLease(node)
            || CanRetainOrderedMutationLease(_run, node))
        {
            ObserveSearchPath(node, SearchPathObservationStage.ExpansionTransposition, "bypass_cycle_or_ordered_lease");
            return true;
        }
        TranspositionLabel next = new(
            node.PotionCount,
            node.PotionStrategicCost,
            node.FutureSoldHp,
            node.Snapshot.CumulativePlayerHpLost,
            node.ActionCount,
            node.Score,
            node.AdvisoryHpLoss.CompletedExcessHpLost,
            node.AdvisoryHpLoss.CurrentCycleHpLost);
        if (!_run.ExpandedTranspositions.TryGetValue(node.StateKey, out TranspositionFrontier? frontier))
        {
            _run.ExpandedTranspositions.Add(node.StateKey, new TranspositionFrontier(next));
            ObserveSearchPath(node, SearchPathObservationStage.ExpansionTransposition, "accepted_new_state");
            return true;
        }
        if (frontier.TryAccept(next))
        {
            ObserveSearchPath(node, SearchPathObservationStage.ExpansionTransposition, "accepted_label");
            return true;
        }
        _run.TranspositionBranchesPruned++;
        ObserveSearchPath(node, SearchPathObservationStage.ExpansionTransposition, "rejected_dominated");
        return false;
    }

    private IEnumerable<(int Index, Creature? Target)> TargetsFor(
        PredictedCard card,
        CombatPredictionSimulator simulator)
    {
        if (IsMultiplayerAdvice && simulator.GetTargetType(card) is TargetType.AnyAlly or TargetType.AnyPlayer)
        {
            foreach (var peer in simulator.State.CombatState.Players)
                if (simulator.State.GetCreature(peer.Creature).IsAlive
                    && (simulator.GetTargetType(card) != TargetType.AnyAlly || peer != _player))
                    yield return (-1, peer.Creature);
            yield break;
        }
        if (simulator.GetTargetType(card) == TargetType.AnyEnemy)
        {
            IReadOnlyList<Creature> enemies = simulator.State.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Creature target = enemies[i];
                if (simulator.State.IsHittable(target))
                    yield return (i, target);
            }
            yield break;
        }

        yield return (-1, null);
    }

    private IEnumerable<(int Index, Creature? Target)> TargetsForPotion(
        PotionModel potion,
        CombatPredictionSimulator simulator)
    {
        if (IsMultiplayerAdvice && potion.TargetType == TargetType.AnyPlayer)
        {
            foreach (var peer in simulator.State.CombatState.Players)
                if (simulator.State.GetCreature(peer.Creature).IsAlive)
                    yield return (-1, peer.Creature);
            yield break;
        }
        if (potion.TargetType == TargetType.AnyEnemy)
        {
            IReadOnlyList<Creature> enemies = simulator.State.Enemies;
            for (int index = 0; index < enemies.Count; index++)
            {
                Creature enemy = enemies[index];
                if (simulator.State.IsHittable(enemy))
                    yield return (index, enemy);
            }
            yield break;
        }

        if (potion.TargetType is TargetType.AnyPlayer or TargetType.Self)
        {
            if (simulator.State.GetCreature(_player.Creature).IsAlive)
                yield return (-1, null);
            yield break;
        }

        if (potion.TargetType is TargetType.AllEnemies or TargetType.TargetedNoCreature)
            yield return (-1, null);
    }

    private static IReadOnlyList<PlanCardChoice>? ActionChoicesForReplay(PlanAction action)
````

## C11：tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs

[固定提交原文件](https://github.com/sgpsdd-zyx/CombatSolver/blob/f220a6bc4ff8e49d32c41b2edd6ea1ab849039dd/tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs)

局部策略测试的实际构型和验证范围。

源文件第 1 至 209 行，基线 `f220a6b`。

````csharp
using System.Text.Json;
using CombatSolver;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace OfflineSearchHarness;

internal static class MultiplayerStrategyContracts
{
    private sealed record Outcome(string Name, int Hp, int ExpectedLoss, int ExpectedDamage,
        int ActualLoss, int ActualDamage, string[] Cards, int Expanded, string Boundary);

    public static string Run(CombatState state, HarnessOptions options, MainLoopContext loop)
    {
        GameBootstrap.Harmony.Patch(AccessTools.Method(typeof(Godot.Time), nameof(Godot.Time.GetTicksMsec)),
            prefix: new HarmonyMethod(typeof(MultiplayerStrategyContracts), nameof(ClockPrefix)));
        Player local = LocalContext.GetMe(state)!;
        if (state.Players.Count != 2 || ReferenceEquals(local, state.Players[0]) || state.Enemies.Count != 1)
            throw new InvalidOperationException("Strategy fixture requires two players, local index 1 and one enemy.");
        void Native(Task task) => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(20), "Strategy fixture setup");
        foreach (Player player in state.Players)
        {
            foreach (var potion in player.PotionSlots.ToArray()) potion?.Discard();
            Native(CardPileCmd.RemoveFromCombat(player.PlayerCombatState!.AllCards.ToArray(), skipVisuals: true));
        }
        Player peer = state.Players[0];
        peer.Creature.SetMaxHpInternal(500);
        peer.Creature.SetCurrentHpInternal(500);
        var enemy = state.Enemies[0];
        enemy.SetMaxHpInternal(500);
        enemy.SetCurrentHpInternal(500);
        foreach (CardModel model in new CardModel[] { ModelDb.Card<StrikeIronclad>(), ModelDb.Card<DefendIronclad>() })
            Native(CardPileCmd.AddGeneratedCardToCombat(state.CreateCard(model, local), PileType.Hand, local));

        SearchPolicySnapshot policy = new MultiplayerSearchPolicy(Horizon: 1).Apply(
            SolverController.CaptureSearchPolicy(SolverSettings.Capture(), state, includeTurnSetup: false, theftPolicy: null));
        policy = policy with { Profile = policy.Profile with { BeamWidth = 2 }, MaxDegreeOfParallelism = 1 };
        CombatBeamSolver Solver(CombatRootSnapshot root, bool verify = false) => new(root, SolverDisplayNames.Capture(state),
            BattleDamageTracker.Observe(state), policy with { VerifyIncrementalSearch = verify }, searchProfile: policy.Profile);
        var initialRoot = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
        var probe = Solver(initialRoot).ReplayMultiplayerForTesting([new(PlanActionKind.EndTurn, initialRoot.StartTurnNumber)]);
        int incoming = probe.CumulativePlayerHpLost;
        probe.ReleaseSimulator();
        if (incoming < 4 || incoming > 30)
            throw new InvalidOperationException($"Strategy fixture needs an opening attack of 4..30 HP, got {incoming}.");

        List<Outcome> outcomes = [];
        void Check(string name, int remainingIncoming, int hp, int energy, int expectedLoss, int expectedDamage,
            int enemyHp = 500)
        {
            local.Creature.SetCurrentHpInternal(hp);
            enemy.SetCurrentHpInternal(enemyHp);
            int block = incoming - remainingIncoming;
            if (local.Creature.Block > block)
                Native(CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), local.Creature,
                    local.Creature.Block - block, null));
            else if (local.Creature.Block < block)
                Native(CreatureCmd.GainBlock(local.Creature, block - local.Creature.Block,
                    ValueProp.Unpowered, null, fast: true));
            var playerState = local.PlayerCombatState!;
            if (playerState.Energy > energy) Native(PlayerCmd.LoseEnergy(playerState.Energy - energy, local));
            else if (playerState.Energy < energy) Native(PlayerCmd.GainEnergy(energy - playerState.Energy, local));
            Native(RunManager.Instance.ActionExecutor.FinishedExecutingActions());
            var root = CombatRootSnapshot.Capture(state, multiplayerAdvisor: true);
            Task<SolverResult> search = Task.Run(Solver(root, verify: name == "allow_3").Solve);
            loop.RunUntilCompleted(search, TimeSpan.FromSeconds(30), name);
            SolverResult result = search.GetAwaiter().GetResult();
            outcomes.Add(new Outcome(name, hp, expectedLoss, expectedDamage,
                result.Snapshot.CumulativePlayerHpLost, enemyHp - result.Snapshot.EnemyHp,
                result.BestNode.Actions.Where(action => action.Kind == PlanActionKind.PlayCard)
                    .Select(action => action.CardId).ToArray(), result.ExpandedNodes, result.BoundaryReason.ToString()));
            HarnessLog.Trace($"{name}: hp_lost={result.Snapshot.CumulativePlayerHpLost} enemy_damage={enemyHp - result.Snapshot.EnemyHp}");
            if (result.ExpandedNodes > policy.Profile.MaxExpandedNodes)
                throw new InvalidOperationException("Strategy lanes exceeded the node budget.");
            if (result.AdvisoryHpLossAllowance != 3 || !SolverOverlaySnapshot.Capture(result, unexpectedReplan: false)
                    .SummaryText.Contains(SolverText.Format($"输出优先：单回合扣血目标不超过 {3} 点；当前预测最高 {result.AdvisoryMaximumCycleHpLost} 点。")))
                throw new InvalidOperationException("Advisory UI does not describe the selected HP allowance.");
        }

        Check("allow_1", 1, 40, 1, 1, 6);
        Check("allow_2", 2, 40, 1, 2, 6);
        Check("allow_3", 3, 40, 1, 3, 6);
        Check("defend_above_3", 4, 40, 1, 0, 0);
        Check("avoid_lethal_3", 3, 3, 1, 0, 0);
        Check("no_need_to_defend", 0, 40, 1, 0, 6);
        Check("equal_damage_save_hp", 3, 40, 2, 0, 6);
        Check("take_lethal", 3, 40, 1, 0, 6, enemyHp: 6);
        VerifyBudgetBookkeeping();
        enemy.SetCurrentHpInternal(500);
        VerifyNativeBoundary(state, local, policy, loop, options);

        // Damage already paid before a manual recalculation must not grant a fresh allowance.
        CardModel bloodletting = state.CreateCard(ModelDb.Card<Bloodletting>(), local);
        Native(CardPileCmd.AddGeneratedCardToCombat(bloodletting, PileType.Hand, local));
        int priorLoss = CombatRootSnapshot.Capture(state, true).InitialPlayerRoundHpLost;
        Native(CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), bloodletting, null, skipCardPileVisuals: true));
        Native(CardPileCmd.RemoveFromCombat([bloodletting], skipVisuals: true));
        Native(CreatureCmd.Heal(local.Creature, 5));
        var paidRoot = CombatRootSnapshot.Capture(state, true);
        if (paidRoot.InitialPlayerRoundHpLost != priorLoss + 3)
            throw new InvalidOperationException("Recalculation/healing reset damage already paid this round.");
        if (local.Creature.Block > 0)
            Native(CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), local.Creature, local.Creature.Block, null));
        paidRoot = CombatRootSnapshot.Capture(state, true);
        var paidProbe = Solver(paidRoot).ReplayMultiplayerForTesting([new(PlanActionKind.EndTurn, paidRoot.StartTurnNumber)]);
        incoming = paidProbe.CumulativePlayerHpLost;
        paidProbe.ReleaseSimulator();
        if (incoming != 0)
            throw new InvalidOperationException("Recalculation fixture requires the enemy's setup turn.");
        foreach (CardModel model in new CardModel[] { ModelDb.Card<Bloodletting>(), ModelDb.Card<StrikeIronclad>() })
            Native(CardPileCmd.AddGeneratedCardToCombat(state.CreateCard(model, local), PileType.Hand, local));
        policy = policy with { Profile = policy.Profile with { BeamWidth = 12 } };
        Check("already_paid_before_recalculation", 0, 40, 1, 0, 6);
        File.WriteAllText(Path.Combine(options.OutputDirectory, "strategy-results.json"),
            JsonSerializer.Serialize(outcomes, UnattendedTestFiles.JsonOptions));
        foreach (Outcome outcome in outcomes)
        {
            if (outcome.ActualLoss != outcome.ExpectedLoss || outcome.ActualDamage != outcome.ExpectedDamage)
                throw new InvalidOperationException($"{outcome.Name}: expected loss/damage {outcome.ExpectedLoss}/{outcome.ExpectedDamage}, "
                    + $"got {outcome.ActualLoss}/{outcome.ActualDamage}; cards={string.Join(',', outcome.Cards)}.");
        }
        return $"cases={outcomes.Count} allowance=1,2,3 above_limit=defend lethal=avoid equal_damage=save_hp "
            + "incremental=equal native_next_turn=equal healing=no_reset allowance=no_carry fork=isolated; "
            + "beam=2 for two-card cases, beam=12 for paid-HP setup; shared node/time limits; no networking";
    }

    private static void VerifyBudgetBookkeeping()
    {
        MultiplayerHpLossBudget empty = default;
        var distributed = empty.Advance(3, true, 3).Advance(3, true, 3);
        var concentrated = empty.Advance(0, true, 3).Advance(6, true, 3);
        var splitActions = empty.Advance(2, false, 3).Advance(2, false, 3);
        if (distributed.ExcessHpLost(3) != 0 || concentrated.ExcessHpLost(3) != 3
            || splitActions.ExcessHpLost(3) != 1 || concentrated.MaximumCycleHpLost != 6)
            throw new InvalidOperationException("Damage allowance carries across rounds or resets between actions.");
    }

    private static void VerifyNativeBoundary(CombatState state, Player local, SearchPolicySnapshot policy,
        MainLoopContext loop, HarnessOptions options)
    {
        GameBootstrap.Harmony.Patch(AccessTools.Method(typeof(CombatManager), "AllPlayersReadyToEndTurn", [typeof(CombatTurnState)]),
            prefix: new HarmonyMethod(typeof(MultiplayerStrategyContracts), nameof(ReadyPrefix)));
        loop.RunUntilCompleted(PowerCmd.Apply<CrimsonMantlePower>(new ThrowingPlayerChoiceContext(), local.Creature, 1,
            local.Creature, null), TimeSpan.FromSeconds(20), "Add next-turn HP cost");
        int startCost = ModelDb.Power<CrimsonMantlePower>().DynamicVars["SelfDamage"].IntValue;
        var root = CombatRootSnapshot.Capture(state, true);
        SearchPolicySnapshot boundaryPolicy = new MultiplayerSearchPolicy(Horizon: 2).Apply(policy);
        var solver = new CombatBeamSolver(root, SolverDisplayNames.Capture(state), BattleDamageTracker.Observe(state),
            boundaryPolicy, searchProfile: boundaryPolicy.Profile);
        SimulationSnapshot before = solver.ReplayMultiplayerForTesting([]);
        SimulationSnapshot after = solver.ReplayMultiplayerForTesting([new(PlanActionKind.EndTurn, root.StartTurnNumber)]);
        SearchNode Node(SimulationSnapshot snapshot, SearchNode? parent) => new(
            parent == null ? null : new(PlanActionKind.EndTurn, root.StartTurnNumber), parent == null ? 0 : 1,
            snapshot.PotionUseCount, snapshot.PotionStrategicCost, snapshot.Turn, SearchRouteTraits.None, 0,
            snapshot.Score, snapshot.StateKey, snapshot.HasRisk, snapshot.BoundaryReason, false, parent, snapshot,
            CombatProgressState.Capture(snapshot));
        SearchNode next = Node(after, Node(before, null));
        if (after.AdvisoryLastEnemyCycleHpLost != 3 || after.CumulativePlayerHpLost != 3 + startCost
            || next.AdvisoryHpLoss.CompletedExcessHpLost != 0 || next.AdvisoryHpLoss.CurrentCycleHpLost != startCost
            || next.AdvisoryHpLoss.MaximumCycleHpLost != Math.Max(3, startCost))
            throw new InvalidOperationException("Next-turn HP cost was charged to the previous enemy cycle.");
        var fork = after.Simulator.Fork();
        var forkCombat = (SimulatedCombatState)fork.State.CombatState;
        if (forkCombat.AdvisorLastEnemyCycleHpLost != 3)
            throw new InvalidOperationException("Cycle checkpoint was not copied into the fork.");
        forkCombat.AdvisorLastEnemyCycleHpLost = 99;
        if (((SimulatedCombatState)after.Simulator.State.CombatState).AdvisorLastEnemyCycleHpLost != 3
            || ((SimulatedCombatState)before.Simulator.State.CombatState).AdvisorLastEnemyCycleHpLost != 0)
            throw new InvalidOperationException("Cycle checkpoint mutations escaped a branch.");
        ContinuationStamp expected = ContinuationStamp.CapturePredicted(local, after.Simulator,
            after.Turn, root.Forecast, root.StartTurnNumber);
        int turn = local.PlayerCombatState!.TurnNumber;
        foreach (Player player in state.Players) CombatManager.Instance.SetReadyToEndTurn(player, canBackOut: false);
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (local.PlayerCombatState.TurnNumber == turn || local.PlayerCombatState.Phase != PlayerTurnPhase.Play)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Strategy native boundary did not finish.");
            loop.Pump(TimeSpan.FromMilliseconds(10));
        }
        ContinuationStamp actual = ContinuationStamp.CaptureLive(state, true);
        if (expected.StateText != actual.StateText)
            throw new InvalidOperationException("Native cycle differs: " + string.Join("; ", expected.DescribeDifferences(actual, 12)));
        File.WriteAllText(Path.Combine(options.OutputDirectory, "native-boundary.txt"), actual.StateText);
        before.ReleaseSimulator();
        after.ReleaseSimulator();
    }

    private static bool ReadyPrefix(CombatTurnState __0, ref bool __result)
    {
        __result = __0.PlayersReadyToEndTurn.Count == __0.State.Players.Count && __0.State.CurrentSide == CombatSide.Player;
        return false;
    }

    private static bool ClockPrefix(ref ulong __result)
    {
        __result = (ulong)Environment.TickCount64;
        return false;
    }
}
````

