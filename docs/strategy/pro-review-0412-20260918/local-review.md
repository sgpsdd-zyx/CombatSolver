# 0.41.2 Pro 第二轮复审本地核对

[返回本轮归档](README.md)

日期：2026-09-18。研究固定源码为 `2dc5d15b26f16d89436af0fb98650d8b4cf6b411 / 0.41.2`，多人行为提交为 `b29d6fc`，本地起点为 `9885a75`。本轮只研究和归档，没有实施或发布新行为。

本文保留上述研究时点的源码结论和验证边界。用户后续授权实施的 F01/F02/F03 已完成，新的生产合同与条件项取舍见[实施与归档记录](implementation.md)；不把后续通过结果回写成研究期证据。

F06 随后由[2026-09-19 窗口研究](../pro-horizon-20260919/README.md)取得真实父链实验；0.41.3 定版时撤出生产，保留原动作数排序。下文仍保留当时的静态判断，当前行为以多人指南为准。

## 结论与建议次序

6 Pro 已交付[复审结论](CombatSolver_0.41.2_Review_Conclusions.md)与[策略设计](CombatSolver_Multiplayer_Strategy_Optimization_0.41.2.md)。本地支持其修订后的主方向：先让现有候选的事实、资格和交付正确，再讨论增加搜索覆盖。上一轮 R1/R2 已修；本轮没有将它们重新列为当前缺陷。

| Pro 编号 | 优先级与结论 | 本地证据与采用边界 |
|---|---|---|
| F01 | P1：较早检查点遮蔽胜利 | [L1](#l1较早检查点遮住已知胜利) 的生产函数探针复现，含截断删除胜利候选；建议整体使用终局事实，不能只改胜利标志 |
| F02 | P1：本机无损满血胜利仍提前停止多人搜索 | [L5](#l5本机无损胜利提前停止没有覆盖多人目标) 静态确认；先给原条件加多人排除，后续须运行穿过真实搜索循环的反例 |
| F03 | P1，有触发前提：根前队友用药解除本机 Require | [L7](#l7队友在请求前用药可能提前解除本机用药要求) 确认归属缺失及构造期降级；真实客户端是否记录相应历史事件未验证 |
| F04 | P2：完成池局部压缩与已知联合池目标不一致 | [L8](#l8已完成池局部压缩早于联合比较) 有源码链和 Pro 抽象反例；限定同一个已知发布时点，不承诺任意未来集合的冠军都保留 |
| F05 | P2：末父节点已生成卡子候选未完整接收 | [L4](#l4最后一个父节点只提交首个子候选) 静态确认；还须覆盖随后 active 清空及最终物化，不以读完整个迭代器修复 |
| F06 | P2/P3：共同周期之外动作数影响同分 | [L3](#l3已知坏后缀与未知后缀比较不对称) 探针确认尾部动作惩罚；没有证明实际排序不确定，也没有现成的精确 CP 动作游标 |
| F07 | P2：根后资源观察没有玩家归属 | [L2](#l2全队救命资源先于本机-3-hp-目标) 确认观察缺口与现行顺序；补 owner 与改变风险顺序分开，不按研究文字擅自调换 |

保留[旧预览回退路径](#l6旧当前回合预览仍有另一条比较路径)为本地额外待测项。它没有进入 Pro 最终的七项主要发现，也未复现用户可见影响，不与主预览已经通过的合同混写。

建议后续最多三批：第一批只处理 F01/F02/F03；第二批对齐 F04 的已知联合比较，F06 只有准确边界可低成本取得时再做；第三批先补资源归属观察，F05 只有能在原容量内接入且不新增引擎动作时再试。每批先取得最小生产失败证据，复杂度或成本失控就停在上一批。低成本回退为经验证的第一批加现役三通道 Beam。

两步挑战不再是优先下一步。Pro 的 A11 表明它能救回特定二步组合，A12 表明同预算含重放时也会损失深层证据，A13 表明三步才兑现的收益仍会漏掉；A12 的首动作没有变化，不能冒充首动作胜率下降。集火、控制、救援和更长组合仍值得研究，但须先用真实 fixture 找到正确分支首次被裁掉的位置。暂不新增主动队友情景、威胁权重、第二个 Planner 或固定预算切块。

## 本地独立证据

在接收第二轮最终回文前，本地已完成下列检查；外部研究与本地证据分别记录。

- 确认本地起点相对固定发布提交的生产源码、项目配置、覆盖输入、离线宿主与结构门禁没有变化；独立「尖塔军师」工具不属于本次输入。
- 定点阅读共同周期、最终资格、预览/最终候选池、预算末尾展开、资源记录和多人生命周期入口。
- 编译并运行一个独立探针，直接调用当前发布 DLL 的比较入口，得到 [5 组真实输出](local-ordering-probe/output.json)；[源码、命令与证据范围](local-ordering-probe/README.md)一并归档。没有重新构建生产 DLL。

探针使用人工构造的冻结候选事实，没有捕获真实战斗根或运行模拟器。下述“生产函数确认”仅指排序函数在这些输入上的真实结果，不能升级为原生差分、整场搜索复现或真实联机结论。

## L1：较早检查点遮住已知胜利

优先级：P1，建议作为下一批最小正确性修复的首项。

`MultiplayerFactsAt` 先寻找共同周期检查点，在命中时返回 `Won=false`，随后才处理 `snapshot.AllEnemiesDead`。`CreateMultiplayerOrdering` 在确定共同周期时跳过胜利候选，但投影候选事实时没有先保留它的胜利状态。

固定源码入口：

- [检查点投影先于终局](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.MultiplayerEvaluation.cs#L48)。
- [完整资格池排序和截断](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Multiplayer.cs#L17)。
- [预览合并候选](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L868)与[最终池加入较早一层](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L2074)。
- [零损获胜提前停止条件](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L1995)还要求回满最大 HP；不能用这条捷径排除所有混合终局池。

最小生产函数反例：双方本机存活、0 扣血、0 救命消耗、2 名队员存活、动作数相同。W 已经获胜，当前敌方 HP 为 0，但第 1 周期检查点仍有 100 HP；U 尚未结束，第 1 周期及当前敌方 HP 为 10。完整池的共同深度为 1，W 的投影变成 `Won=false, EnemyHp=100`，U 排第一。再放入 3 条同深度、敌方 HP 为 20/30/40 的未完成候选，`BeamWidth=1` 时 W 被 `4B` 截断删除。

控制结果：只放 W 时，全部终局的共同深度为 7，W 被正确识别为胜利；没有较早检查点的立即胜利也能击败 U；本机死亡候选仍输给存活候选。问题集中在终局与较早检查点同时存在的分支，不是所有胜利/死亡排序都失效。

这与 0.41.2 已修 R1/R2 不同：资格过滤与单次共同深度均正确，丢失发生在固定深度下的事实投影。建议先修事实表达，保留既有本机死亡和已知风险顺序；不借机决定“任何胜利都必须压过任何资源/扣血代价”。验收应覆盖混合终局池、截断、全终局控制和官方单人零进入，之后再用一个真实短搜证明搜索入口可达。

Pro 的 A17 补充了重要约束：两条真正胜利在终点事实相同、结束回合不同，但旧检查点敌方 HP 不同时，仅设置 `Won=true` 仍会按旧 HP 选错。应把终局的 HP、累计损失、敌方 HP、资源和队伍事实整体前置；保留原来优先于胜利的风险项。A17 是外部标量对照，本地没有再次运行生产入口的“仅改 Won”版本。

## L2：全队救命资源先于本机 3 HP 目标

优先级：P2；资源归属缺失是数据口径事实，具体优先级是独立政策选择。

[死亡阻止镜像](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Engine/InCombat/Mirrors/Hooks/Death/DeathPreventerMirrors.cs#L13)知道实际受益者，写入[共享计数](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/SimulatedCombatState.LongTermResources.cs#L60)时却只传入回复量。[药水库存](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/SimulatedCombatState.Potions.cs#L18)按 `(Player, Slot)` 隔离；使用记录 `PredictedPotionUse` 不含玩家。[快照汇总](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.StateEvaluation.cs#L507)与[周期检查点](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.MultiplayerEvaluation.cs#L9)沿用全队总数。

生产排序探针确认：同深度、同敌方 HP 和存活人数时，`本机扣 4 HP / 救命次数 0` 胜过 `本机扣 0 HP / 救命次数 1`。比较器没有归属字段，不能区分后者是本机还是队友的资源。该探针没有运行真实队友救命事件，实际来源由静态调用链支持。

建议先在显式多人观察中区分本机/队友、根前已付/根后预测、显式药水/自动救命；保留公共原结算和单人计数。是否将“为了省队友救命资源而让本机超出 3 HP”改掉，需要单独确认产品优先级。不能把这个问题描述为药水库存串人或治疗结算错误，也不能让自动救命满足本机显式用药资格。

## L3：已知坏后缀与未知后缀比较不对称

优先级：P2 搜索覆盖/政策问题；当前结果本身不等于语义错误。

[比较器](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.MultiplayerEvaluation.cs#L78)用较晚观察到的救命次数和超额否定具体续行；这能防止已知风险被较早检查点藏住。生产探针中，第 1 周期事实相同，深路线第 2 周期发现 1 HP 超额时，尚未查到该后果的浅路线获选。这个设计没有证明浅路线安全，也没有证明相同首动作的所有后缀都坏。

另一组观察：深路线第 2 周期把敌方 HP 从 50 降到 10，且没有额外风险，仍会在共同第 1 周期投影后因总动作数 6 大于浅路线的 3 而输掉同分。共同周期之外的正收益被忽略，负代价和动作长度却仍可进入比较。这值得完善“已验证前缀”和“条件后缀”的表达，但不能通过删掉已知风险检查来换取看似更好的排序。

建议先保留每条候选的已验证深度与具体风险来源，并确认预算末尾的有效候选没有丢失。只有取得真实失败对照后，才评估少量同首动作的替代后缀或有限补齐；不由这两组标量反例直接推出增加独立搜索器、情景树或两步挑战。

Pro 建议的 `CP.action_count` 是抽象脚本新增字段，现役检查点没有它。结束回合展开可能在 CP 之后继续处理下一玩家阶段选择，不能直接把整个子节点动作数当作 CP 内成本。若现有游标无法准确定位，就保留限制；不为末级同分新增重放。同分返回 0 也不等于已经证明 `.NET List.Sort` 在真实输入中产生不确定结果。

## L4：最后一个父节点只提交首个子候选

当前证据：固定源码调用链确认，尚未运行生产展开入口或真实战斗反例。

[Expand](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Expansion.cs#L489)在展开父节点时递增 `_run.Expanded`；普通卡牌候选在[首次 yield 前已生成并完成准入](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Expansion.cs#L688)。[串行消费者](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L1658)接收首个子节点后按父节点总数判断截止，导致其余已生成卡牌候选释放；普通药水和回合结束后继排在后面，也可能尚未生成。

[并行入口](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L1688)明确保留了这个 legacy 行为，将最后一个预算槽切回串行。因此不能擅自当作多人之外也获准修改的公共 bug。

这给“两步挑战优先”提供了反对证据：已有计算结果可能还未公平进入下一层，先增加额外展开未必划算。最小候选方向是仅多人明确最后一个已准入父节点的提交边界，同时保持总预算、取消、快照释放与官方单人路径。排空已生成卡牌与继续生成药水/回合尾的成本不同，不能统一声称免费；需要生产入口反例和完整成本对照后才能实施。

后续还有[预算耗尽时清空 active](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L1893)。仅多接收几个子节点，随后全部释放，仍不能保证它们参与最终比较。若试验，只允许在现有有界回退中替换候选，并把恢复、排序、复制及最终重放计入成本；“不新增引擎动作”不等于零开销。

## L5：本机无损胜利提前停止没有覆盖多人目标

来源：6 Pro 在研究进度中提出，本地随后独立沿条件与控制流核对。当前证据为源码事实，尚未构造整条搜索的失败输入。

[回合层末尾](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L1995)只要已完成池中存在“获胜、无显式用药、无卖血、根后累计扣血为 0、最大 HP 未下降、当前满血”的路线，就释放 frontier 并停止。这里没有 `IsMultiplayerAdvice` 排除，也没有检查队友存活或 `DeathSaveUseCount`。多人政策关闭局外成长目标，使 `_hasGrowthTargets` 不能阻止该分支。

因此本机满血获胜，但队友已经死亡或消耗自动救命资源时，该条件仍可能成立。自动药水从显式用药数中扣除，不能用“无显式用药”推导“没有全队资源代价”。现行多人比较器仍可能更偏好另一条队友存活或不花救命资源的胜利路线，停止条件却使剩余候选没有继续证明它的机会。

这与常规 HP 目标提前停止不同：[`MeetsHpTarget`](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L313)经 `GrowthTargetSatisfied/CanStopAtHpTarget` 被多人 `StopAtAcceptableBattleHpLoss=false` 关闭；[单人 HP 下界剪枝](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Retention.cs#L287)也有显式多人排除。不能笼统声称所有单人捷径均泄漏进多人。

最小候选修复是让这一条单人提前停止条件不适用于多人，或另行证明涵盖完整多人目标的充分条件；不扩大原请求预算。后一方案必须同时考虑队友资源、存活、根前已经支付的本周期扣血和终局条件，未证明前优先选择简单的多人排除。建议与 L1 一起作为最先验证的控制流修正，不等待两步挑战或新估值框架。

取消错误早停可能让搜索更充分地使用原预算，因此不能承诺首条完整结果更快；后续应记录实际等待时间，而不是为维持旧耗时恢复不充分的停止条件。

## L6：旧当前回合预览仍有另一条比较路径

来源：6 Pro 在研究进度中提出预览旁路审查，本地核对了生产者与 UI 消费者。当前为源码确认，未复现用户可见触发过程。

[`ConsiderCurrentTurnCandidate`](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L348)仍使用 `SolverInterimResultOrdering.IsBetter` 维护当前回合候选，没有分派到多人共同周期比较。`RefreshCurrentTurnPreview` 据此生成 `CurrentTurnPreview`。[运行时](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/SolverController.cs#L2236)优先显示已经修正的 `SpeculativeRoutePreview`，只有它为空时才回退到 `CurrentTurnPreview`；两者都进入搜索进度数据。

因此不能声称当前所有预览都仍错误，也不能仅凭主预览的合同断言已经覆盖所有 UI 路径。后续最小验证应固定“多人、主预览为空、旧预览可用”的场景，检查资格、排序、显示与最终返回是否一致。按用户要求保持简单，可以先让多人只展示已通过多人政策的预览；若确有首条提示延迟问题，再统一该生产入口，不先重构整个 UI。

## L7：队友在请求前用药可能提前解除本机用药要求

优先级：P1 的上游资格问题。来源为 6 Pro 的后续研究进度，本地独立核对源码链；没有运行真实历史注入或生产构造函数反例。

[`BattleDamageTracker.CountPotionHistoryEntries`](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/BattleDamageTracker.cs#L95)统计所有 `PotionUsedEntry`，不区分玩家。多人时 `Observe` 虽然不跟踪单人 HP，仍返回这个全队历史计数差。主线程[创建搜索请求](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/SolverController.cs#L1005)传入该 `BattleDamageSnapshot`；[`CombatBeamSolver` 构造](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.cs#L87)只要看到 `PotionsUsedSoFar > 0`，就把全局 `RequireAtLeastOne` 转成 `Smart`。

最小触发前提是：战斗跟踪已开始、队友已手动用过药、本机尚未用药、用户选择“至少用一瓶”，随后本机手动发起多人搜索。资格在进入最终候选管线前已经改变，0.41.2 的正确资格过滤自然无法恢复它。逐槽 `Force` 仍有独立检查，不能声称所有强制用药设置都失效。本地未核对原生自动救命是否记录为同类历史，不将该结论扩大到所有自动药水。

建议在多人请求边界提供本机真实已用药口径，保留官方单人原路径；与 L2 的根后救命观察分开验证，但可以共享明确的玩家归属规则。验收使用“只有队友在根前用药”的一条原生历史反例，并分别覆盖本机已用药、逐槽 Force 和保护药水。它不是再次修“先截断再过滤”，也不能只在 `PrepareMultiplayerFinalCandidates` 里追加条件掩盖上游错误。

[`CombatReplayOutcome.Capture`](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/CombatReplayOutcome.cs#L65)已有按 `PotionUsedEntry.Actor` 与本机 Creature 身份匹配的先例。最小改动应保持原 `Begin` 追踪起点，只纠正同一窗口的玩家归属；不要顺便改成“每次请求必须再喝一瓶”，也不要假定所有自动救命都会写入相同历史。追踪中途接入、历史重置和自动事件分类需在后续生产合同中明确，不能遇到未知基线默认为 0。

## L8：已完成池局部压缩早于联合比较

优先级：P2。来源为 Pro 最终 F04/A04；本地读取当前压缩与合池调用链，未运行此场景的生产候选管线。

[`Phases`](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L1909)先单独压缩 `completed + ended 中已终止者`，之后才形成 frontier；最终又合入现存 frontier 与有界旧层。每次准备都正确地先过滤并固定一个深度，但已删除节点不能随之后的联合目标恢复。

Pro 的标量反例：五条外部选择边界路线 A/B/C/D/E 都完成 CP2，CP1 敌方 HP 为 10/20/30/40/50，CP2 为 9/8/7/6/5。宽度 1 的完成池按 CP2 留 E/D/C/B，删除 A。合入已经存在的 CP1 浅路线 S（敌方 HP 99）后，联合深度为 1，保留池选 B，原完整池却应选 A。按同一已知联合集合先冻结深度 1 再压缩，可以保住 A。

建议复用 `MultiplayerFinalBatch` 与当前集合，在一个已知发布时点让完成池压缩和联合选择使用同一上下文；没有新状态时不要在局部池先定另一个目标。之后出现真正新候选时仍可更新深度。这不等于要求有界存储在任意未来集合出现后保留所有历史冠军，也不支持每个周期另建冠军池。若实现需要超出原峰值容量保留整层，应暂缓，先交付第一批。

## 控制实施复杂度的约束

1. 优先修好已有事实的使用：终局投影、提前停止、本机用药输入，再处理联合比较和已准入候选的提交。先取得生产失败输入，再讨论新搜索覆盖；不因为外部设计列了模块就全部照建。
2. 资源归属只需保留现有消费入口已经知道的拥有者。根前已耗资源已体现在冻结库存/遗物状态中，不为比较同一根的路线重建一套完整历史事件系统；根后预测观察单独记账。本机已付扣血继续使用现有周期账本。
3. 新的资源计数属于多人观察，随分支复制并随周期检查点固定。它改变政策比较时就检查多人去重标签，不能只补一个 UI 字段，也不把估值或日志塞进公共战斗状态键。
4. “真人队友以后不主动操作”是一个条件情景。即使增加两个情景，也不能声称覆盖最坏情况；必须先固定同一个当前可执行本机前缀，再比较不同后续。不能分别选全知动作后平均，制造实际无法执行的好路线。
5. 当前首条建议、旧前缀重放、展开、选择分支、尾部验证和最终重放都消耗真实工作。扩大节点上限或只报告主循环耗时不能作为收益证据。预算未查完时保留未验证标记。
6. 七周期上限保留，固定共同周期仍有价值。问题不支持把整个搜索永久降为一周期，也不支持先建立第二个 Planner、MCTS 树、队友行为概率模型或策略版本开关。

## 对“不退化保证”的原始资料核对

本地定向读取了 Bertsekas 的 [Constrained Multiagent Rollout and Multidimensional Assignment with the Auction Algorithm，v2](https://arxiv.org/html/2002.07407v2) 第 2 节及第 3 节 fortified rollout 相关部分。文中改进性质依赖相应假设；强化版本要求从初始状态得到一条完整可行基线，并保留与当前前缀一致的完整候选轨迹。它不是“任意增加两步展开都不会变差”的定理，也不直接扩展成未知队友动作下的最坏情况保证。

对本项目的推论：可以借鉴保留可用基线、只按同一目标接受已证明更优结果的纪律；仅有浅检查点、变化中的比较深度或尚未验证尾部时，不能援引该论文宣称整个多人策略保证改善。固定总预算下，挑战还会挤占其他展开机会，必须用完整工作量与固定墙钟对照检验收益。这里是适用前提核对，不是新算法实施或数学保证证明。

## 外部证据的实际范围

Pro 原始[脚本](CombatSolver_0.41.2_abstract_checks.py)、[结构化结果](CombatSolver_0.41.2_abstract_results.json)与[控制台输出](CombatSolver_0.41.2_abstract_output.txt)已收取，报告为 Python 3.13.5 下 18 项预设断言通过，另含 6 个源码子串检查。840 种排列、216 组三元比较，以及 24 个标量节点在 4 个固定深度下的 55,296 组三元检查，均是本轮外部抽象运行；不是仓库历史 C# 合同重跑。本地阅读了脚本和输出，没有重复执行这些已有成功证据。

这 18 项不是 18 个已修生产问题。A01 中“同批消费”的一个断言直接比较对象与自身，可展开前缀也只检查原始列表仍含元素；两者不能验证生产 Preview/Select 接线与真实保路。A09 是显式关闭 Python 生成器的控制流示例，A10/A13/A15 是给定数值的反例，A11/A12 是人工安排的玩具工作调度。它们说明可能性或设计约束，不能外推搜索可达性、.NET 调度、实际模拟工作量或胜率。生产 R1/R2 的当前证据仍是源码与既有合同；新的终局投影证据则来自本轮独立 DLL 探针。

原始回文、脚本和外部结果保持原样；本地意见不回写原文。源码读取范围只证明所列调用链被读过，不是 2,017 文件逐行审计；Pro 的完整性报告是外部执行声明，本地没有再次解包或计算源码哈希。完整证据 ZIP 与全量文件哈希清单保留在 `.local/mp-pro-review-0412-20260918/`，不进入源码提交。原生差分、真实搜索全链和双端联机均未在本轮执行。

## 已排除的笼统怀疑

- 不能继续报上轮“最终先截断再过滤、同次截断后重新建深度”：当前 `MultiplayerFinalBatch` 的顺序和贯通已经修正。本轮 L1 发生在固定比较器内部。
- 不能声称所有单人 HP 捷径都进入多人：普通 HP 目标停止与主结果 HP 下界剪枝有政策或显式多人隔离，L5 指向另一条具体分支。
- 不能把队友不确定性说成只捕获本机或漏掉 RNG：[`LiveCombatStamp`](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/LiveCombatStamp.cs#L10)委托完整 `ContinuationStamp`；[九条战斗 RNG](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/ContinuationStamp.cs#L106)和[逐队友牌堆、资源与回合状态](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/ContinuationStamp.Multiplayer.cs#L10)均在文本中。根捕获有[前后稳定性核对](https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/CombatRootSnapshot.cs#L215)。这只是所读字段的源码确认，不等于全部第三方隐藏状态或所有联机场景已验收。
