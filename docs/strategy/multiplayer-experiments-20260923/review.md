# 多人实验方案与 0.44.2 交付汇报审查

日期：2026-09-23。[实验设计](README.md) · [协议草案](protocol.json) · [0.44.2 实施记录](../multiplayer-shared-damage-20260923/implementation.md) · [知识归档](../multiplayer-shared-damage-20260923/closeout.md) · [上一轮静态审视](../multiplayer-race-model-review-20260922.md) · [策略索引](../README.md)

## 0. 范围与性质

- 审查对象：`556d629d` 提交的交错行动与手动重算实验设计及协议草案；`fd976376` 的 0.44.2 共享伤害实验与实现；`be773943` 的知识归档；以及另一位 agent 对后两个提交的完成汇报。
- 方法：静态阅读源码、文档与 `.local` 留存证据，核对数字、行号与文件存在性。没有运行搜索、构建、打包或游戏测试；没有重新解包或校验已有产物。
- 性质：审查意见，不是实施授权。行号以本次读取版本为准；卡牌以模型 ID 标识。

## 1. 实验方案：总评

方案的方法论骨架是对的，也是既往研究没有做到的：响应式队友而非动作带、同根配对单因素消融、显式截断分类、按原始跑局聚类、先试跑再定样本量、队友策略不进生产（`README.md` 第 2、3、5、6 节）。主要问题不在统计设计，而在用这么大的装置去测一个几乎不会动的因子，以及几处会让试跑测不到目标的落地细节。

## 2. 实验方案：主要问题

### 2.1 处理因子太小，四分之三的配对两臂必然相同

协议把处理固定为共享伤害信用开/关（`protocol.json:29`）。0.44.2 的 71 次搜索已经给出它的作用范围：

| 项 | 数值 |
|---|---|
| 已运行的开发/留出/最终根 | 16 |
| 首动因开关改变的根 | 3，全部为毒根 |
| 试跑四个主题中不含毒的主题 | 3 |

数据来自 `implementation-evidence.json` 的 `experiments[*].rows`，逐行比较 `features=0` 与 `features=1` 的 `firstTurn`。防御、投资、多敌三个主题的配对将走出完全相同的轨迹。

处理建议：

- 相同臂的配对改登记为确定性负对照，用于验证复位合同、RNG 隔离与进程复用，轨迹不一致即报错。协议目前没有这类检查。
- 试跑的主处理换成已知大效应的因子作正对照，例如 H3 对 H14，或配额开/关。它们在每个根上都会改变搜索，效应也直接回答预算该往哪投。共享信用留作后续单因素实验。
- `arms` 改为政策快照差分，而不是写死两个 id。

### 2.2 350 节点预算与整副牌不匹配

现有 71 次实验的根只有 3 到 4 张合成牌。在这个规模下 Beam12 / 350 节点探索到 5 至 10 个敌方周期，比较深度全部为 3（`implementation-evidence.json` 各行 `AdvisorySearchedEnemyCycles` 与 `AdvisoryComparisonCycles`）。方案要求整副牌、允许洗牌与资源耗尽（`README.md` 第 3 节），同样预算大概率到不了第三周期。到不了时 `Comparable=false`，最终比较退回原始 Score（`src/Search/CombatBeamSolver.MultiplayerEvaluation.cs`，`CompareMultiplayerQualityAtCycle` 中 `if (!a.Comparable) return right.Score.CompareTo(left.Score)`），实验测的就是另一个比较器。

处理建议：B 批次登记"到达比较深度的请求占比"作为通过门槛；至少留一格接近生产的预算，确认结论方向不随预算翻转。`protocol.json:75-78` 目前只有一个固定预算。

### 2.3 缺少 local_first 调度

`README.md:68-69` 只有 `peer_batch_first` 与 `alternate_one_action`。前者是军师信息最全的情形，后者居中。本机先出、队友随后跟进在真实对局里常见，而且正是闲置参照最容易出错的场景：本机按"队友不打"防守，队友随后打了，本机是否过度防御。这是方案要回答的核心问题，却没有对应条件。建议加入 `local_first`。

### 2.4 建议执行不要重写，复用单人部署映射器

`README.md:129` 写"不能复用单人自动部署作为多人行为"。我认为方向反了。`DeployCurrentTurn`（`src/Runtime/SolverController.cs:2843`）加 `NativeChoiceRuntime`（`:2904`）已经是 PlanAction 到原生牌实例、目标与选牌页的完整映射；多人被拒只是 `StartDeployment` 里一处产品规则（`:2801-2804`）。测试侧另写一套映射，建议语义与执行语义迟早漂移，`advice_invalid` 的统计也会掺进映射器差异。建议用仅测试可见的旁路让部署走原路径，生产守门不动。

队友驾驶同理：遗物归属测试已经用切换 `LocalContext.NetId` 的方式以队友身份执行原生命令（`src/Testing/UnattendedTestRunner.MultiplayerRelicOwnership.cs:107`），队友策略沿用这条路即可。

### 2.5 原根来源仍是空的

`protocol.json:52` 的 `rootManifest` 为 null，通用生成器明确拒绝多人内容（`docs/GENERATED_COMBAT_SCENARIOS.md`）。最直接的办法：用现有单人生成器生成两个独立的角色/牌组/遗物配置，合并进一个双玩家 `RunState`。离线宿主现在就是这样建局的（`tools/OfflineSearchHarness/OfflineCombat.cs:45-50`），再由原生 `ScaleHpForMultiplayer` 缩放。两名不同角色能覆盖"跨角色合成手牌"以外的真实组合。

### 2.6 队友策略首版限定在无选择牌池

协议用 `choice_adapter_missing` 兜底是对的（`README.md:101`）。但六个策略族若随手打出需要选牌的牌，它会成为主要停止原因而不是罕见事件。建议每个策略登记允许的牌 ID 集合，首版只含 `STRIKE_*`、`DEFEND_*`、`BASH`、`INFLAME`、毒类等无选择牌，适配器缺口留到 C 批次。

### 2.7 补一项最直接量化闲置误差的指标

`turn_start` 节奏下旧建议仍合法时继续执行。在被队友打断的位置额外跑一次不执行的搜索，记录首动是否改变。这是"重算的价值"的直接测量，成本是一次搜索，比整场 HP 差更早、更少噪声地告诉你闲置参照错在哪些局面。

### 2.8 建议的执行顺序

A 批次先做确定性负对照和部署映射器的测试旁路。B 批次换主处理为 H3 对 H14，加 `local_first`，登记到达比较深度占比。共享信用与其余因子在 B 通过后逐个单因素补。

## 3. 对 0.44.2 实验本身

- **共享信用把本机自己的毒也按 1/n 折算。** `SharedProgress` 中 `credit = local + shared / ParticipantCount`（`src/Search/MultiplayerContributionObjective.cs:48-56`）。双人局里本机 `DEADLY_POISON` 的评分价值只有直接伤害的一半。分支内 `PoisonPower` 的 applier 是已知的，模拟中的毒伤可以按 applier 归属而不虚构；根历史仍是空 dealer，跨重算的实绩会因此不一致，这大概是选均分的原因。均分保守可行，但应在文档里写明它对 DoT 主力角色仍有系统性低估，试跑的毒根要覆盖"本机是唯一毒源"的情形。
- **对上一轮静态审视七条反驳的态度**（`implementation.md:19-25`）。接受五条：总减本机含无来源伤害；ready 可撤销；Intercept 与 Covered 会耦合防守；九条流各自独立；十四周期在晚回本根上有用。保留意见两条。其一，"历史速率延续到未来是预测"成立，但 H/n 配额同样假设每名队友未来各打 1/n，两者都是对未来的假设，一个用常数、一个用数据。这条线怎么划由用户定，但文档不应把配额写成无假设。其二，ready 可撤销不构成不捕获的理由：它是当前时刻的事实，作为展示字段和实验观察字段都有价值；方案自己要测"ready 后撤销"（`README.md` 第 4 节），没有捕获就测不了。只是不能把它当合法性保证。

## 4. 对完成汇报的核对

汇报原文：已完成 `be773943` 归档与 `556d629d` 新方案；新方案核心是"队友行动、局面变化、玩家手动重算、按新建议继续"；首批 48 条轨迹、24 对、至多 4 个独立原局面；文档与协议检查通过；新实验尚未运行；原始证据与本地包保留，生成记忆未修改，未推送。

### 4.1 事实核对

| 汇报说法 | 核对结果 |
|---|---|
| `be773943` 归档、补齐 0.44.2 本地包凭证 | 属实。`docs/releases/0.44.2-PREPARE.md` 新增；`releases/CombatSolver-0.44.2.zip`、`.local/multiplayer-race-20260923/build-0442.log` 与 `local-package-receipt.json` 存在，收据内容与文档一致 |
| 旧实验只能证明指定脚本下的局部收益 | 属实，并写入 `coverage/test-evidence.json` 的 `scope` 与 `localDelivery` 字段，是诚实的收窄 |
| 48 条轨迹 / 24 对 / 至多 4 个原根 | 属实。`protocol.json` 与 `.local/neat-multiplayer-20260923/experiment-design-l0.json` 计数一致 |
| 文档与协议检查通过 | 属实。L0 记录 8 个文件、611 个链接、57 个锚点、0 问题 |
| 新实验尚未运行 | 属实。`readyToExecute=false`，已执行轨迹 0 |
| 未推送、本地包保留 | 属实。分支领先 `origin` 4 个提交，其中一个是 `cad3ba35` 的静态审视 |
| `AGENTS.md` 压缩到 27,850 字节 | 属实 |

### 4.2 汇报省略的内容

- **规则文件与三个 skill 被修改。** `fd976376` 改了 `AGENTS.md` 与 combat-semantic-change、search-performance-optimization、ui-localization 三个 skill；`be773943` 压缩 `AGENTS.md` 顶部并新增一条测试选择规则。规则改动约束后续所有 agent，应单独列出，不应归入"归档"。新增那条规则本身是好的。
- **有玩家可见改动。** `src/UI/English.json` 新增共享伤害折算提示，`AdvisorySharedDamageCredit` 进入 UI 投影。
- **玩家日志在打包之后被改写。** ZIP 于 00:37 从 `fd976376` 构建；`be773943`（01:07）把日志措辞从"改善判断"改成"增加选路价值"。符合仓库规则，也是往保守方向改，但汇报应说明包与日志的先后关系。

### 4.3 需要商榷的两处

- **一条评分政策被写成语义规则。** `.agents/skills/combat-semantic-change/SKILL.md:10` 新增"不能把 Power applier 或最后施加者伪造为扣血归属"。原生历史 dealer 为空是事实；模拟分支里毒的 applier 已知，按 applier 归属不是伪造，是一个可选的评分口径。写进语义 skill 等于用规则封死一个合理方案。建议改写为"原生历史 dealer 为空；若按 applier 归属，根观察与分支必须同一口径"，把决定权留给策略层。
- **"补读原生源码"没有留存证据。** `implementation.md:19-25` 称补读了 `PoisonPower.Trigger`、`RunRngSet`、`Intercept`、`CoveredPower`、`InterceptPower`。`.local/decompiled/sts2-v0.111.0/` 仍为 31 个文件，`.local` 下深度 4 以内也没有这些源文件或摘录。其中毒的空 dealer 有原生夹具 `poison_stays_unattributed` 与 `mixed_poison_full_native_state` 的实证支撑；九条流分立可由 fork 的 `CombatPredictionRngSet` 推出。这两条接受。Intercept 与 Covered 会耦合防守这一条，仓库里只剩 `types.txt` 中的类名，没有可复核依据。按 `AGENTS.md` 第 10 节，反编译参考应落在该目录；建议补进这几个文件，否则该反驳只能算口头说法。

## 5. 未核实项

- 没有运行任何新实验、构建或测试；第 2 节的数字全部来自已归档 JSON。
- 没有重新解包或校验 0.44.2 的 ZIP；只确认文件存在与收据一致。
- 汇报中"生成记忆未修改"指另一平台的生成记忆，本审查不涉及。
- 第 2.4 节复用部署映射器的可行性来自静态阅读，未验证仅测试旁路在原生无头宿主中的实际行为。
