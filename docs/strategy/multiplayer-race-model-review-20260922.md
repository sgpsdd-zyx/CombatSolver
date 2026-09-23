# 多人策略审视：机制事实、现有实现与竞速模型建议

日期：2026-09-22。[返回策略索引](README.md) · [现役多人指南](../multiplayer-advisor.md) · [贡献策略实施记录](multiplayer-cooperative-planning-20260922/implementation.md)

## 0. 范围与性质

- 审视对象：fork `89eaeb1c / 0.44.1` 的多人军师层，即本机贡献目标、闲置队友推演、十四周期窗口、伤害归属与最终比较。官方单人路径不在范围内。
- 输入：仓库源码（`src/Search/*Multiplayer*`、`src/Runtime/*Multiplayer*`、`src/Prediction/MonsterMoveSemantics.cs`、`src/Engine/InCombat/Simulation/*`）；游戏 `0.111.0` 的只读反编译子集 `.local/decompiled/sts2-v0.111.0/`；本目录既往研究与 6 Pro 归档。
- 反编译子集只有 31 个文件加类型索引。`Player`、`PlayerCombatState`、`CombatState`、`Rng`/`RunRngSet`、`PoisonPower`、`MultiplayerScalingModel` 正文缺失，涉及处在第 7 节标为未核实。
- 本文是静态阅读与推导。没有新增运行、原生差分、离线对照或联机测试。所有建议是候选设计，不是实施授权；行号以本次读取版本为准。
- 下文卡牌以模型 ID 标识（`Lift`、`Rally`、`BelieveInYou`、`Flanking`、`TagTeam`），不代替官方中文译名。

## 1. 三个改变问题性质的机制事实

### 1.1 怪物攻击命中所有存活玩家，没有目标选择

- `DamageCmd.Attack(x).FromMonster(monster)` 返回 `TargetingAllOpponents`（`AttackCommand.cs:259-267`）；怪物来源的多目标攻击直接取 `_combatState.PlayerCreatures`（`:163-175`）。127 个怪物文件中没有任何选目标逻辑。
- 怪物行动在玩家回合开始时掷定：`enemy.PrepareForNextTurn(PlayerCreatures)`（`CombatManager.cs:739-745`），内部 `MoveStateMachine.RollMove(targets, Creature, RunRng.MonsterAi)`（`MonsterModel.cs:416-419`）。额外回合不重掷。
- 玩家卡牌的随机目标走 `card.Owner.RunState.Rng.CombatTargets`（`CardCmd.cs:77, 90`），不消耗 `MonsterAi`。

推论：本机这个周期要吃多少伤害是确定的，与队友怎么防守无关。队友能影响本机的只有三条渠道：把敌人打残或打死；给敌人上虚弱之类的减益；直接给本机格挡或能量（`Lift`、`Rally`、`BelieveInYou`、投掷药水）。

### 1.2 敌人血量按人数缩放，死亡不结束战斗

- `ScaleHpForMultiplayer = hp × playerCount × GetMultiplayerScaling(encounter, actIndex)`（`Creature.cs:749-756`）。伤害数值没有找到按人数缩放的代码，总受伤通过全体受击隐式放大。
- 只有全部玩家死亡才 `LoseCombat`（`Commands.CreatureCmd.cs:476-489`）。单个玩家死亡：清球、杀 Osty、`DeactivateHooks`、`HandlePlayerDeath` 移除全部卡牌并清零能量与星能（`:577-590`；`CombatManager.cs:1220-1256`）。战斗结束前 `ReviveBeforeCombatEnd` 对每名玩家执行（`:1316-1319`），复活血量在子集内不可见。

推论：多人战斗是 N 个并行的单人局共享一个 N 倍血池，每人都吃满全部敌方输出。它是一场竞速，不是坦克加输出的分工。玩家死亡的代价是团队输出下降与复活血量，不是跑局结束。

### 1.3 RNG 流是跑局级共享的

- 开战时 `player2.PopulateCombatState(player2.RunState.Rng.Shuffle, state)` 对每名玩家取同一个 `RunState`（`CombatManager.cs:460`）；卡牌随机目标经 `card.Owner.RunState.Rng`（`CardCmd.cs:77`）。类型索引中 `RunRngSet` 与 `PlayerRngSet` 并存（`types.txt:245, 293`），后者用于奖励等玩家私有随机（fork 内 `src/Search/PotionRewardOutlook.cs:102`）。离线宿主也把两名玩家放进同一个 `RunState`（`tools/OfflineSearchHarness/OfflineCombat.cs:45-50`）。
- fork 只捕获一套九条流（`src/Runtime/ContinuationStamp.cs:105-114`；`src/Engine/InCombat/Simulation/CombatPredictionSimulator.cs:120`），与共享模型一致。

推论：队友任何触发洗牌、随机目标或随机生成的动作，都会改变共享流的消耗顺序，使本机此后的预测抽牌与实际分叉。单人求解器最大的优势是未来抽牌精确；这个优势在多人里只在本回合成立，最多撑到任何人下一次洗牌之前。`MonsterAi` 流只由怪物消耗，敌人行动预测不受队友出牌影响，只受敌人血量阈值与死亡影响。

## 2. 现有实现与机制的核对

| 机制 | 实现位置 | 结论 |
|---|---|---|
| 全体受击 | `MonsterMoveSemantics.DamagePlayers` 对全部存活玩家结算（`src/Prediction/MonsterMoveSemantics.cs:157-177`）；攻击命令镜像 `TargetingAllOpponents`（`CombatPredictionSimulator.Attack.cs:18`） | 一致 |
| 全体减益与状态牌 | `MonsterMoveEffects.Multiplayer.cs:11-42` 等 | 一致 |
| 意图掷定与流 | 根行动注册为分支 AI 当前状态；后续用 `Rng.MonsterAi` 推进（`SimulatedCombatState.MonsterAi.cs:94-139`） | 一致 |
| 额外回合冻结敌人、只有参与者行动 | `Expansion.Replay.cs:1277-1282`；`MultiplayerRound.cs:12-47` | 一致 |
| 死亡玩家挂钩停用、复活恢复 | `SimulatedCombatState.Multiplayer.cs:20-45` | 一致 |
| 血量缩放 | `SimulatedCombatState.DeathLifecycle.cs:87, 225` | 一致 |
| 队友 ready 标志 | 根与 stamp 只记 `Phase`（`ContinuationStamp.Multiplayer.cs:20`），未读 `IsPlayerReadyToEndTurn`（`CombatManager.cs:1035-1044`） | 缺失 |

模拟语义这一层是资产。问题集中在"队友闲置"这个参照和建在它上面的目标函数。

## 3. 审视：目标函数建在错误参照上

### 3.1 闲置参照对两件事同时错

`EndMultiplayerPlayerTurn` 一步结束全部玩家回合并清手牌（`MultiplayerRound.cs:16-40`）；队友每回合抽牌、不出牌、弃牌。这意味着推演里的队友既不输出也不格挡。

- 不输出：敌人存活时间被拉长到 N 倍以上，本机长期受击被高估，搜索天然偏向防守。
- 不格挡：队友每周期吃满未格挡伤害，稍长的战斗里几个周期内必死。终局代价里每少一名存活者扣 R（`MultiplayerPlanValue.cs:13-18`），尾值里同样计队友死亡（`MultiplayerEvaluation.cs:75-92`），这些项被虚构的死亡触发。`Lift`、`Rally` 给队友格挡的价值因此被系统性高估。

### 3.2 配额只补了一半

`D = ceil(H/n)`、三周期截止和二次缺口代价（`MultiplayerContributionObjective.cs:16-19, 41-45`）是对"不输出"那一半的补偿，本身隐含"每名队友也各打 1/n"。"不格挡"那一半没有任何补偿。两个方向相反的假设靠参数互相抵消，是当前方案脆弱的根源。

配额的隐含换算率由 `DeficitCost` 推出，取本机根 HP 70、双人：

| 局面 | D | 第一点伤害值多少 HP | 达标后每点伤害值多少 HP |
|---|---|---|---|
| 小怪总 60 HP | 30 | 4.7 | 0.29 |
| 首领 300 HP | 150 | 0.93 | 0.06 |

这个比例来自 D 的分母，不来自哪个敌人更危险。实施记录里"18 伤害换 12 损失"被选中是它的直接结果。

### 3.3 十四周期的两重失真

第 1.3 节的共享 RNG 与第 3.1 节的闲置参照叠加：第三周期以后的推演既不反映真实敌人血线，也不反映本机真实手牌。默认上限 14 与时间/节点翻倍（`MultiplayerSearchPolicy.cs:4, 11-24`）把预算花在最不可信的部分。尾值 0.95 的逐周期折价和 0.5 权重都很温和。

### 3.4 归属机制的代价大于收益

- 毒在模拟里以空 dealer 结算（`src/Prediction/CorePowerSupport.cs:465-469`），`NetProgress` 只认本机或本机宠物为 dealer 的伤害（`MultiplayerContributionObjective.cs:31-39`；`MultiplayerDamageAttribution.cs:8-13`），因此走 DoT 流的角色在配额下永远无法达标。原版历史中毒的 dealer 归属未能从子集核实。
- 整套 local/total/宠物归属/过杀排除/见证/Progress 只为配额服务。目标一旦换掉，它们可以整体删除。

### 3.5 未捕获的确定信息

已按结束回合的队友本回合不可能再行动：`OnEndedTurnLocally` 置 `PlayerActionsDisabled`（`CombatManager.cs:987-998`），`SetReadyToEndTurn` 维护 `PlayersReadyToEndTurn`（`:935-966`）。这是主线程能读到的确定事实，当前根与 stamp 没有它。

### 3.6 已核对不成立的疑点

- 中间分把 `(loss + DeficitCost) × 100` 与 `enemyHp` 同列（`StateEvaluation.cs:514-526`）。通过 deficit 项，中间分与终局代价在配额未满时的换算率近似一致，不存在量级冲突。
- 实现审计曾把"全体受击"列为与原版不一致的风险；经反编译源码核对，它正是原版规则，不是实现偏差。

## 4. 建议：守如单人，攻如团队

### 4.1 为什么可以分开

第 1.1 节说明本机受伤只取决于敌人血线、敌人减益和直接给本机的支援，与队友的防守无关。第 1.2 节说明每人都吃全部输出，因此"只算本机自己的未来受伤"已经与团队利益对齐。防守侧和进攻侧受队友影响的方式不同，可以分别选择参照。

### 4.2 防守侧保持现状

本周期来伤确定。按"敌人不会被队友打死"的保守假设精确结算第一个敌方周期，就是现有闲置推演。它对防守是正确的下界，永远不要指望队友的击杀来救命。第二周期起因共享 RNG 分叉，精确性下降，只保留近似价值。

### 4.3 进攻侧：竞速代价替代配额

在阶段边界（现为三周期）对每个候选评估剩余战斗代价：

```text
r_e      = 团队对敌人 e 的输出速率（每敌方周期）
T_e      = 敌人 e 的剩余有效 HP ÷ r_e
V_race   = Σ_e  每周期伤害_e × T_e            本机未来还要吃的伤害
候选代价 = 已结算本机 HP 损失 + 最大生命损失 + 药水战略代价 + V_race
```

- 本机伤害的价值等于它缩短了多少 T_e。击杀让 T_e 归零，虚弱降低每周期伤害_e，力量与能力牌通过提升本机速率进入 r_e。这些自然出现，不需要击杀奖励、配额、见证、Progress、TailValue 或伤害归属。
- 每周期伤害_e 可以复用单人 `ThreatProjection` 与 `IntentForecaster` 的怪物行动表投影；多人当前把它置零（`StateEvaluation.cs:146-151`）。
- 最简版本把团队速率均分到存活敌人：`r_e = r_team / 存活敌人数`。阶段内的击杀顺序已由精确模拟处理，被打死的敌人没有伤害项。按威胁加权分配是可调项，不是首版必需。
- 本机死亡、队友死亡与救命次数保持现有词典序位置，不进入 V_race。

### 4.4 团队速率是测量，不是预测

`MultiplayerContributionCapture` 已从原生历史读出本场总伤害与本机伤害（`src/Runtime/MultiplayerContributionCapture.cs:11-28`）。

```text
r_peer  = (ObservedTotalDamage − ObservedLocalDamage) ÷ 已过敌方周期数
r_team  = 本机计划在阶段内的实际输出速率 + r_peer
```

- 头一两回合没有数据时退化到"队友与本机同速"，即现在隐含的 1/n。
- 队友水平差，实测速率自动变小；队友已死，速率归零。这与用户"预测了队友不按预期打怎么办"的顾虑一致：这里没有对具体行为的预测，只有对已发生输出的度量。
- 它对个体混沌是鲁棒的，因为取的是均值；它不对易伤、`Flanking`、`TagTeam` 等需要队友主动消费的增益作任何估值，这一限制与现役相同。

### 4.5 落点

| 层 | 改动 |
|---|---|
| Search 评分 | `StateEvaluation` 中间分与 `MultiplayerQuotaSelection.Cost` 终局代价用 V_race 替换 `DeficitCost`、`TailValue` 与 `0.25 × R × 剩余敌 HP` 项 |
| Search 窗口 | 默认上限从 14 收到 3 或 4；取消或改用时间/节点翻倍，把预算还给 Beam 宽度 |
| Runtime 会话 | `MultiplayerContributionSession` 保留按周期的观测总/本机伤害，用于 r_peer；删除 D、截止、阶段重建 |
| 删除 | `NetProgress`、达标见证、`Progress` 去重标签、尾值、配额 UI 字段与对应合同 |
| 保留 | 4B 前沿可简化为按标量代价排序加三通道保路；药水资格、风险词典序、外部选择边界不变 |

实现量小于现在的配额体系，符合最简实现要求。

### 4.6 验证方式

- 同根离线对照：现有 `quota-pressure`、`quota-defense`、`quota-investment` 三个夹具，以及 `horizon-comparison` 中 `late_setup`、`payback_seven/nine/fourteen` 根，旧 DLL 与候选 DLL 各跑一次，沿用"只执行选中首动作，每周期一次合法攻击"的外评协议。
- 负哨兵：本机 6 HP / 敌 30 HP 的防守根必须继续首动防守；"能力投资"根的 57/17 不应退化。
- 单人哨兵：官方 0.44.0 80 项非时序比较继续一致，确认多人改动未泄漏。
- 不可离线验证的部分：真人队友速率分布、可见性能、联机胜率。

## 5. 三项独立的低成本改动

这三项互不依赖，也不依赖第 4 节。

1. **捕获 ready 标志。** 主线程在根捕获时读 `IsPlayerReadyToEndTurn(player)`，写入根与 `ContinuationStamp`；UI 显示哪些队友已结束回合。全员 ready 时首周期可以标为精确。
2. **可见手牌上界与等待提示。** 用队友当前手牌与能量算出对每个敌人的最大直接伤害，这是界不是预测。用途是提示"队友 1 本回合可击杀 X，建议等其行动后重算"。在同时行动的回合里，最后出牌的人信息最多，等待本身是一种策略。已 ready 的队友上界为零。
3. **核实原版毒的 dealer 归属。** 若采用竞速模型，归属机制随之删除，此项自动消失；若继续用配额，需要让 DoT 伤害能支付本机份额。

## 6. 不建议的方向

- MCTS 重写：不修目标函数，只改预算分配方式。
- 更深视界：在闲置参照与共享 RNG 两重失真下，越深越假。
- 向模拟器注入虚拟队友伤害：会伪造死亡时点与触发。竞速模型只在评估层使用速率，模拟层保持真实。
- 预测具体队友出牌：违背用户约束，且实测速率已经覆盖它能带来的大部分修正。

## 7. 未核实项与边界

| 项 | 状态 |
|---|---|
| `Player.RunState` 是否所有玩家同一对象 | 由 `CombatManager.cs:460` 与离线宿主建局推断为共享；`Player.cs` 不在子集 |
| `GetMultiplayerScaling` 系数 | 子集内为 ILSpy 版本警告桩 |
| `ReviveBeforeCombatEnd` 复活血量 | 不在子集 |
| `Taunt`、`PullAggro`、`Intercept`、`Bodyguard`、`Protector` | 只有类名（`types.txt`），可能经 `ModifyUnblockedDamageTarget` 重定向伤害；语义未知 |
| 原版毒的 dealer | `PoisonPower.cs` 不在子集 |
| `IntentForecaster` 对多名玩家调用 `GetSingleDamage(state.PlayerCreatures, ...)` 取哪名玩家 | 攻方基础伤害与目标侧修正分开结算，未逐项核对 |
| 队友手牌对本机是否可见 | 只有 `NPeekButton` 等类名 |

本文未运行任何测试。第 3 节的换算率是从源码公式直接推导，第 4 节的所有参数是待对照的起点。

## 8. 与既往研究的关系

- 与[配额补充报告](multiplayer-cooperative-planning-20260922/pro/CombatSolver_CooperativePlanning_20260922_Quota_Addendum.md)一致：不缩血、不伪造 `AllEnemiesDead`、未找到见证不等于不可达、阶段截止固定。
- 不同：配额不再作为主目标，归属机制可删，视界收短而不是加深。ready 标志与实测团队速率是既往研究没有使用的输入。
- 与用户约束一致：不预测具体队友行为。实测速率是对已发生事实的度量，不是对未来出牌的预测。
- 既往两轮 6 Pro 的玩具实验与本地探针不受本文影响，仍是研究材料。本文的推论需要第 4.6 节的同根对照才能成为实施依据。
