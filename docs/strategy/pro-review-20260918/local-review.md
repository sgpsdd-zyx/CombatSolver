# 0.41.1 Pro 复审本地核对

[返回本轮归档](README.md)

日期：2026-09-18。外部研究固定源码为 `5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77`，本地接续起点为 `9c15855`；其间只有版本说明后的研究文档提交，行为实现仍为 `6e28485`。两份 Pro 原文保留下载内容，本文件独立记录本地判断，不改写外部作者的结论。

后续采用：用户在研究归档后授权实施第一批，R1/R2 已进入下一版本开发记录，生产函数反例与回归见[测试矩阵](../../TEST_MATRIX.md#多人最终候选管线第一批2026-09-18)。以下保留研究收尾时的静态判断；R3 资源归属优先级和两步挑战仍未实施。

## 核对结论

R1/R2 的调用顺序与源码相符，应优先进入下一轮最小生产函数验证。R3 的全队资源口径也可确认，但改变资源与本机超额扣血的优先级属于单独的政策决定。两步组合搜索仍是待实验方案。本轮只完成研究归档和静态核对，没有实施这些建议。

| 外部发现 | 本地判断 | 证据边界与下一步 |
|---|---|---|
| R1：先截断，再检查用药资格 | 源码确认该顺序；可以丢弃原池中的唯一合格路线 | Pro 的五候选算术反例与所读分支相符；尚未运行生产候选管线或构造游戏失败包 |
| R2：裁剪后重建共同周期 | 源码确认两次创建排序上下文，子集可能使用更深周期 | 五/六候选反例成立于其限定域；不是固定比较器不传递，也不证明实际触发频率 |
| R3：救命资源缺少玩家归属 | 源码确认计数、使用记录与检查点保存全队汇总 | 库存仍按玩家与槽位隔离；不是药水库存串人或治疗结算错误。调整风险优先级需另定政策 |
| R4：延迟组合可能提前丢失 | 保留为搜索覆盖假设 | 三通道保证代表席位，不保证所有首动作或低即时收益组合；需要首个裁剪点与同预算收益证据 |
| R5：汇总生命/存活人数压缩威胁信息 | 确认摘要的信息范围；收益判断仍待验证 | 不等于引擎没有目标语义；较深周期可能自然区分，应先定位短预算下的错误边界 |
| R6：验证覆盖不足 | 接受为后续测试清单 | 既有比较器合同不覆盖 R1/R2 整条发布管线；多人交错、资源归属与真实联机仍待验证 |

## R1：资格检查晚于最终截断

调用证据：

- [最终池构造与截断](../../../src/Search/CombatBeamSolver.Phases.cs#L2045)先补边界回退，再于第 2073 行调用 `Retention.RankFinal`。
- [多人 RankFinal 入口](../../../src/Search/CombatBeamSolver.BeamRetentionPolicy.cs#L479)直接分派到 [RankMultiplayerFinal](../../../src/Search/CombatBeamSolver.Multiplayer.cs#L18)，排序后只留 `4 * BeamWidth`。
- [多人最终选择](../../../src/Search/CombatBeamSolver.FinalPlanOrdering.cs#L37)随后才检查 `enforcePotionDirectives`、`minimumPotionUses` 和 `RequireAtLeastOne`；筛空会明确抛出 `PotionPolicyUnsatisfiedException`。
- [路线预览](../../../src/Search/CombatBeamSolver.Phases.cs#L868)采用同样顺序，并在资格异常时跳过该次预览。
- [多人政策 Apply](../../../src/Search/MultiplayerSearchPolicy.cs#L8)没有清除 `RequireAtLeastOne`，不能用“多人不走单人药水审计”排除这个入口。

Pro 给出的宽度 1 反例中，四条零药路线在输出上优于第五条用药路线；先取四条再过滤，结果为空。逐槽强制指令的优先比较不能保护一个没有逐槽 Force、仅要求至少用一瓶的候选。补回药水回退发生在截断前，也不能保证其存活。

后续应把资格判断放到完整的可发布候选池上，并与预览/最终返回共用；仍可继续展开的中间前缀不能因为尚未用药而被一刀切。原有明确失败语义必须保留。

## R2：同次选路改变比较目标

[RankMultiplayerFinal](../../../src/Search/CombatBeamSolver.Multiplayer.cs#L20)先对未截断池创建 `MultiplayerPlanOrdering`；[FinalPlanOrdering.Select](../../../src/Search/CombatBeamSolver.FinalPlanOrdering.cs#L47)对截断及资格过滤后的池再次创建。共同周期来自 [CreateMultiplayerOrdering](../../../src/Search/CombatBeamSolver.MultiplayerEvaluation.cs#L20)对候选检查点深度的扫描。

当唯一浅候选被删掉，共同周期可以由 1 变成 2。旧周期下被裁掉的另一条路线，却没有机会参加新周期比较。Pro 的 A/B/C/D/S 例子展示排名翻转，加入 X 后展示被删除候选在新目标下优于最终选中者。该例所有候选均存活、非胜利且没有外部选择边界，未依赖特殊终局语义。

[预算中断时加入上一层候选](../../../src/Search/CombatBeamSolver.Phases.cs#L2067)允许混合深度输入，因此不能假定最终池总是同深度。已有[共同周期合同](../../../tools/OfflineSearchHarness/MultiplayerEvaluationContracts.cs#L45)验证 `RankFinal` 和固定比较器，未贯穿这两次排序及中间过滤。

后续应在资格池上创建一次上下文，贯通本次截断、选择和结果周期元数据；下一次候选批次仍可以选择新的共同周期。不能把整场搜索永久锁在第 1 周期。

## R3：全队资源观察与本机风险

[死亡阻止镜像](../../../src/Engine/InCombat/Mirrors/Hooks/Death/DeathPreventerMirrors.cs#L13)知道实际受益者，但写入 [RecordDeathSaveRelicHpRestored / RecordDeathSavePotionHpRestored](../../../src/Search/SimulatedCombatState.LongTermResources.cs#L60)时只传入回复量，累加同一个 `DeathSaveUseCount`。

[药水消费](../../../src/Search/SimulatedCombatState.Potions.cs#L50)知道玩家，库存键为 `(Player, Slot)`；`PredictedPotionUse` 只保留槽位、药水 ID、代价和 automatic 标记。[快照](../../../src/Search/CombatBeamSolver.StateEvaluation.cs#L507)与[周期观察](../../../src/Search/CombatBeamSolver.MultiplayerEvaluation.cs#L12)使用全队计数。[比较器](../../../src/Search/CombatBeamSolver.MultiplayerEvaluation.cs#L84)先比较救命次数，再比较本机逐周期超额。

因此“本机 0 损、队友用一次救命资源”可能输给“本机扣 4 HP、全队未用救命资源”。这个排序后果有源码依据；哪种更符合用户偏好需要单独确定。自动消费仍从显式用药数量中扣除，不能把队友自动用药一概说成满足本机的显式用药要求。

若后续补玩家归属，公共镜像仅能增加显式多人观察接入，保留现有战斗结算与单人计数。根后预测使用次数、根前已发生事件和当前库存必须分开，Fork 与多人政策标签另做最小生命周期验证。

## 设计采用前的约束

1. 第一批只验证和修正 R1/R2，复用现有资格条件，包括 `enforcePotionDirectives`。`RankMultiplayerFinal` 还有中间 `finalQualityFirst` 调用，不能把所有调用都改成最终资格过滤。
2. 设计第 6.3 节新增的“规范化动作序列”同分规则不是现有行为；当前比较器最终只比较 `ActionCount`。第一批保持原同分语义，若要增加稳定规则应单独说明并测试。
3. 资源归属涉及公共观察入口，列为独立依赖；第一批不顺带改本机/队友风险优先级。
4. 两步挑战的 15% 配额、24 个 fixture、60% 改善线和 110% 开销线都是外部实验建议，不是当前生产参数或已达成指标。实施前还需核对“根动作层 1→2”窗口与“先已有一条可解释建议”的时点能否兼容，以及最终重放预留的实际计费入口。
5. 保持手动操作、每敌方周期 3 HP、七周期上限、原请求预算和官方单人路径。不能把无头/抽象结果写成真实联机质量或可见性能结论。

## 本轮证据

本轮执行了原文接收、上述源码与合同的定点静态阅读、文档/固定提交引用检查及归档盘点。没有运行 Pro 附录脚本、生产 C# 候选合同、编译、Godot、Steam 或游戏场景。Pro 自述执行的 720 种候选排列和 216 组三元关系检查保留为外部抽象证据，不登记为本地游戏测试通过。

论文和外部实现的引用保留在原文，本地本轮未重新进行文献调研。现有发布、构建与历史行为验证复用前一阶段凭证；没有重建安装包、重新发布或更改冻结标签。后续源码实施需要新的开发任务，不由原文末尾的 Codex 任务段自动启动。
