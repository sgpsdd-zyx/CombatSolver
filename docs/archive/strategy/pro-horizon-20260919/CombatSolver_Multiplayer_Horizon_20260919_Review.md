# CombatSolver 多人搜索窗口复审：保留默认七周期，但不把它当作已证最优

**研究日期：2026-09-19。唯一当前源码：`3ccac172dd55ad4a8d97074b7129cbf4155add65`。本轮仅研究，没有实施、推送或发布。**

## 结论与决定

**优先主方案：继续默认最多七个敌方周期；保留现役三通道 Beam 和原请求预算；优先验证、随后仅在多人入口修正“同一发布时点已知联合池”的共同周期／完成池压缩不一致。** 不上线本轮自适应候选，不把所有候选重新搜索至共同深度，不增加队友情景搜索器，也不恢复两步挑战或能力牌统一加权。

**低成本回退：完全维持 `3ccac17` 的默认 H=7、三通道、最终比较及保路行为；新增观察仅在离线宿主启用。** 如果窄补丁无法在同预算生产入口复现收益，第二批不进入默认行为。这里的“保留7”是有反证支持的保守部署决策，不是七周期的最优性定理。

本轮确实找到支持缩短、支持加深和反对自适应的证据。固定3会漏掉第5周期收益；固定5会漏掉第7周期收益；固定9在第9周期独立收益例中优于7，但在队友提前击杀目标的同根扰动例中损失20点实际抽象输出。自适应候选既有控制组节省工作，也会因信号假阴性漏掉40点收益，并在扫描竞争例中耗费更多工作却选错当前动作。**没有一种候选在全部已列情景中支配其余方案，不能用人为 fixture 的多数票选择默认值。**

### 本次实际交付与运行

|层级|本次执行|可支持的结论|
|---|---|---|
|当前源码|新目录解压并沿实际调用链阅读；2056个公开文件逐项校验不变|下文路径、物理行号和执行顺序事实|
|新 Python 抽象实验|22个机制 fixture × 6策略＝132次主运行；59个预算点 × 3对照＝177次预算运行；16项微型检查|列明的有限模型中正例、退化、预算竞争和比较反例|
|新实验重现|完整再运行一次；去掉时间及外部旧脚本包装后的结果哈希一致|本脚本确定性，不是游戏可重复性|
|旧 Python 原样复跑|从当前 ZIP 归档读取原脚本，执行51次A/B/C、12次Beam8控制和13项检查|F10/F12 原抽象反例仍成立|
|生产 C#／原生差分／真实联机|**本轮均未执行**；没有 `dotnet` 或游戏 DLL|不支持胜率、可见性能、真实联机稳定性改善的声称|

完整机器数据在 `CombatSolver_Multiplayer_Horizon_20260919_Results.json`；旧复跑在 `CombatSolver_Multiplayer_Horizon_20260919_Legacy_Rerun.json`。新实验是**宏周期缩减模型，不是生产 C# 基线的移植**。132次主运行的H7没有加入拟议联合池补丁；该补丁仅有候选级M01/M16检查，不把它写成已经测得的端到端质量改进。源码中的既有测试记录另列，绝不记成本轮重跑。

## 1. 输入、范围和对假设的处理

本轮首先完整阅读了 `CombatSolver-horizon-review-request.md`。从本次 ZIP 解压到 `/mnt/data/horizon_review_3ccac17`；旧发布附件不参与任何当前行为判定。提交绑定来自用户提供的冻结需求；ZIP 的 SHA-256 是 `a2bb861ef17234a2d77517580e3f34cad39b94d9b242377ee3655392d7c93fe3`。逐文件不变校验证明本轮未改附件，不等于独立验证远端 Git 签名。

本机已经发生的动作、当前合法目标和资源是冻结事实；队友未来手动操作不是已知输入。研究范围是让本机现在的动作更好，不是让一个假定队友不操作的完整后缀获得更低终点 HP。七周期可以改，3 HP账本、手动请求与操作、官方单人隔离和原总预算不能顺带改变。

需求中的疑问均作为待反驳假设处理。例如，“长窗口一定更聪明”“短窗口一定更稳健”“提示仍为正就值得继续搜”“不增加Beam席位就无机会成本”“真实展开更深就有更好建议”均被单独检验，而非作为前提。

## 2. 源码调用链与四种深度

### 2.1 七周期从哪里来，在哪里生效

`MultiplayerSearchPolicy` 默认 `Horizon=7`、`AcceptableHpLossPerTurn=3`；`Apply`只要求H至少为1，并没有把7写成不可改的引擎常数。它关闭多人不应进入的增长目标、自动早停和相关单人策略组合，但没有重设Beam、节点或时间预算。[`src/Search/MultiplayerSearchPolicy.cs:L3–L24`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/MultiplayerSearchPolicy.cs#L3-L24)

Runtime 在手动请求时构造多人政策、捕获根，并交给后台请求；`CombatSearchCoordinator.Run` 遇到多人政策立即走单个 `CombatBeamSolver`，不再借单人多求解器组合额外分配预算。根在主线程冻结本机身份、全队当前状态、已付扣血和本机药槽。[`src/Runtime/SolverController.cs:L1140–L1162`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Runtime/SolverController.cs#L1140-L1162)；[`src/Runtime/SolverController.cs:L1210–L1246`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Runtime/SolverController.cs#L1210-L1246)；[`src/Search/CombatSearchCoordinator.cs:L13–L26`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatSearchCoordinator.cs#L13-L26)；[`src/Runtime/CombatRootSnapshot.cs:L137–L204`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Runtime/CombatRootSnapshot.cs#L137-L204)。

真实敌方周期的自增在 `AdvanceRound` 的敌方结束阶段之后：保存敌方周期损失、`AdvisorEnemyCycles++`、`CaptureMultiplayerCycle`，达到上限就返回 `AdvisoryHorizon`，**此时还没有执行下一玩家开始阶段**。额外玩家回合走另一个分支，不消耗敌方周期；若额外回合只属于队友，还可能在一个本机推进动作内继续经过队友阶段。因此本机动作层、玩家回合、敌方周期与转移工作不能互换。[`src/Search/CombatBeamSolver.Expansion.cs:L3154–L3169`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Expansion.cs#L3154-L3169)；[`src/Search/CombatBeamSolver.Expansion.cs:L3232–L3245`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Expansion.cs#L3232-L3245)；[`src/Search/CombatBeamSolver.Expansion.cs:L3423–L3457`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Expansion.cs#L3423-L3457)；[`src/Search/CombatBeamSolver.MultiplayerRound.cs:L12–L65`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.MultiplayerRound.cs#L12-L65)。

真实终局可能在本次周期检查点建立前返回。`AdvisorEnemyCycles`因而更准确地表示**已记录的完整检查点数量**，不是所有已执行敌方事件的数量。`RequireLocalChoice`明确拒绝代替队友决策，形成外部选择边界；不应把这种边界计为完整安全周期。[`src/Search/SimulatedCombatState.Multiplayer.cs:L51–L60`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/SimulatedCombatState.Multiplayer.cs#L51-L60)。

### 2.2 必须分别报告的量

|量|定义与推荐采集点|不可替代它的字段|
|---|---|---|
|H，窗口上限|本次冻结政策的 `Horizon`|任何实际覆盖统计|
|D_parent，最深已展开父节点|实际通过入口并增加 `Expanded` 的父节点，其 `Snapshot.AdvisoryEnemyCycles` 最大值|生成过但未展开的最深节点；搜过的玩家回合层|
|D_selected，选中路线完整周期|最终物化后选中快照的 `AdvisoryEnemyCycles`|被丢弃路线最深周期；完整未来战斗长度|
|K，最终共同评价周期|本次 `MultiplayerFinalBatch.Ordering.EnemyCycles`|H、D_parent、D_selected|

另记录 D_generated 作为解释性辅助量。`Expand`在节点被实际接纳后才增加 `_run.Expanded` 并发出 `Expanded`观察，适合采集D_parent。现有 `SearchPathObservation`只有玩家 `Turn`，没有敌方周期字段；应增加可选纯值观察，而不是由 `Turn`猜测。[`src/Search/CombatBeamSolver.Expansion.cs:L469–L491`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Expansion.cs#L469-L491)；[`src/Search/SearchDiagnosticsSink.cs:L131–L163`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/SearchDiagnosticsSink.cs#L131-L163)。

`MaterializeSelectedRoute` 的 `SearchedTurns`从**被选中的动作序列**计算，不能用来证明整个搜索展开到多深。源码测试文档也已经记录过一次因此误判的历史断言。[`src/Search/CombatBeamSolver.Phases.cs:L478–L496`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Phases.cs#L478-L496)；[`docs/TEST_MATRIX.md:L18–L20`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/docs/TEST_MATRIX.md#L18-L20)。

|输入／策略|上限 H|最深已展开父节点|最深已生成检查点|选中完整周期|最终 K|已展开节点|总工作单位|
|---|---:|---:|---:|---:|---:|---:|---:|
|H07／H3|3|2|3|3|3|5|73|
|H07／H7|7|6|7|7|7|13|161|
|H07／H9|9|8|9|9|9|17|205|
|H14／H9|9|2|3|2|2|10|231|
|H15／H7|7|1|1|1|1|3|51|
|H16／Adaptive|9|3|4|4|3|7|111|
|H17／H7|7|0|0|0|7|1|16|

H17的K=7是**全终局池哨兵**，实际没有完整敌方检查点；不是“七周期全覆盖”。H14已经生成第3周期，却最终在第2周期比较；H16自适应在混合玩家回合层结束时选中4周期路线，共同K仍是3。这些结果直接说明不能从H或结果后缀长度替代其余三个量。

### 2.3 与H独立的4／8预算分层

`Phases`使用普通战斗4层、Boss 8层的 `reservedTurnLayers`，来自 `StandardEnemyStrengthSuppressionHorizon`／`BossEnemyStrengthSuppressionHorizon`；余下时间和节点按这些**玩家回合层**分摊，不按多人 `Horizon` 分摊。[`src/Search/CombatBeamSolver.Phases.cs:L1326–L1328`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Phases.cs#L1326-L1328)；[`src/Search/SolverWeights.cs:L82–L89`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/SolverWeights.cs#L82-L89)；[`src/Search/CombatBeamSolver.Phases.cs:L1397–L1419`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Phases.cs#L1397-L1419)。

因此只把7改成3或5，通常是在原调度后更早退出，**并不自动把剩余工作投入当前回合强搜索**；只把7改成9，也未必获得两个完整额外周期。将4／8直接替换成H可能改变当前回合深度与额外回合分配，是另一项调度策略，不是免费修正。本轮不建议顺带实施，也没有在 Python 中伪造该C#调度器。

## 3. 当前已修覆盖与仍存在的独立缺口

### 3.1 已成立的修复，不重复报旧结论

完整最终候选池先按强制指令、最少显式用药和 `RequireAtLeastOne`过滤，再固定共同K、排序和4B截断；`SelectMultiplayerFinal`直接消费同一个批次。普通中间保路不要求尚可展开前缀已经用药。[`src/Search/CombatBeamSolver.Multiplayer.cs:L19–L45`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Multiplayer.cs#L19-L45)；[`src/Search/CombatBeamSolver.Multiplayer.cs:L57–L102`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Multiplayer.cs#L57-L102)。M06/M07仅验证相应抽象性质，不声称重跑生产R1/R2合同。

真实胜利／死亡使用完整终局事实，不再被旧CP的`Won=false`覆盖。具体路线后续已经发生的死亡、救命资源、超额和队友死亡仍能反驳旧检查点；这些风险不会自动污染相同第一张牌的所有其他续行。[`src/Search/CombatBeamSolver.MultiplayerEvaluation.cs:L49–L100`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.MultiplayerEvaluation.cs#L49-L100)。H03、M04/M05覆盖抽象控制。

先前“本机无伤满血无药胜利就停止整个多人搜索”的早停现有明确 `!IsMultiplayerAdvice`保护；单人能力注册也要求 `policy.Multiplayer==null`。另核查了 `ApplyPrimaryIncumbentBound`：它同样先排除多人，故不能仅看仍调用了incumbent更新就重报“单人HP界正在剪多人”。[`src/Search/CombatBeamSolver.Phases.cs:L1995–L2004`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Phases.cs#L1995-L2004)；[`src/Search/CombatBeamSolver.cs:L44–L57`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.cs#L44-L57)；[`src/Search/CombatBeamSolver.Retention.cs:L287–L299`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Retention.cs#L287-L299)。

### 3.2 H-R1：同一已知发布时点，完成池先按自己的K压缩

**重要性：高于调窗口参数；源码顺序已确认，候选级反例已运行，实际战斗可达性仍待生产宿主复现。**

`Phases:1909–1920`先把旧completed与新终止节点合并，用其自身K压缩至4B；随后才保留非终止frontier并在1939、最终2051／2076合流。它与已修R2不同：每一次单独排序的K没有乱变，但**上一个局部池提前删除了下一次联合池所需的候选**。[`src/Search/CombatBeamSolver.Phases.cs:L1909–L1947`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Phases.cs#L1909-L1947)；[`src/Search/CombatBeamSolver.Phases.cs:L2049–L2090`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Phases.cs#L2049-L2090)。

最小结构反例M01取Beam8、完成池容量32，所有风险／资源／队友存活相同：

|候选|第3周期敌方HP|第7周期敌方HP|状态|
|---|---:|---:|---|
|A|70|60|已完成到7|
|B₁…B₃₂|85|20|32条不同路线，已完成到7|
|C|95|无|已经存在的较浅frontier，到3|

先以完成池K7排序，A被32条B删除。再与C合流，K3选择B，敌方HP85；如果同一已知发布时点先冻结联合K3，则A以70胜出。**这不是深度奖励问题，也不是比较器不传递，更不是最终资格过滤旧问题。**

最小修正目标是让同一轮已经确定将参与发布的候选共享K，再压缩completed子集；不额外保留第33个长期席位。不得宣称能找回任何更早轮次已经删除的候选：未来尚未出现的浅池仍可能改变K，固定小内存不能凭空恢复信息。完整方案及释放顺序见设计文档第5节。

反对证据：M01人工构造了不同路线的检查点，没有证明实际牌局中32条B都同时存在；联合K3还会牺牲一些深层区别。停止条件是生产入口不复现、改动需要多套长期候选档案、或同预算本机首动作的高优先级哨兵退化。

### 3.3 H-R2：动作数同分项仍能把远端差异伪装成“改善”

**重要性：评价实验的高优先级防伪项；不在本轮默认部署中借机改风险或终局次序。**

相同K下风险、敌方HP等事实相同后，比较器仍用整条 `ActionCount`，而非到K的动作数。尾部少一个动作会胜出，即使没有任何当前动作质量改进。[`src/Search/CombatBeamSolver.MultiplayerEvaluation.cs:L101–L115`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.MultiplayerEvaluation.cs#L101-L115)。

本轮M03构造相同首动作、相同第3周期事实，长度3与8的路线，旧比较器偏好长度3；按共同边界动作数比较则相同。这个微型修正只消除一种排序偏差，**不能把二者相等写成战斗收益**。原样复跑F12也得到相同首动作`attack`、同共同周期1、比较器“更好”却属于`tiebreak_only_NOT_combat_gain`。候选当前终点敌方HP从A的74变为B/C的82，反而更差，不能选择有利的口径宣称改善。

本轮对H的所有质量比较都删除深度与动作数同分项，使用同一外部时点。共同边界动作数的生产修正需要额外父链扫描／派生字段，并可能改变相同首动作之外的平局选择，故不凭M03自动部署。它可以单独接入既有最终排序合同，不能作为上线自适应的收益证明。

### 3.4 H-R3：全终局池的K使用H作哨兵，不能当覆盖

**重要性：研究结论和显示准确性；没有证据说明这本身使终局选错。**

当全池都是真胜利或本机死亡，`CreateMultiplayerOrdering`将K设为 `policy.Multiplayer.Horizon`。终局事实不依赖该K，原意可成立，但H17显示H3／5／7／9的K不同而所有选中周期均0。新统计必须标注`terminal_only_sentinel`，不需要为了统计去改终局策略。[`src/Search/CombatBeamSolver.MultiplayerEvaluation.cs:L32–L43`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.MultiplayerEvaluation.cs#L32-L43)。

更细的两个现役规则也不能简化错：普通候选没有CP时不会用“0”拉低其他CP的K；较早外部边界不锚定仍可展开路线的K，若其没有恰好K的CP则不可比较。M14/M15明确检验这些分支，不能把“取所有候选深度最小值”当成生产算法。

### 3.5 H-R4：加深／扫描的成本不是仅由Expanded计数解释

**重要性：预算公平性的强约束；源码确认，具体实机成本未测。**

旧路线最多4条、每条最多重验当前回合32个无选择且非结束回合动作，按当前请求合法性重放，不复用旧分数。新增窗口探测不能另领预算。[`src/Search/CombatBeamSolver.Multiplayer.cs:L105–L159`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Multiplayer.cs#L105-L159)。

软超时可能继续排空／强制结束回合；选中的已释放快照会完整重放，而最终物化还可能进行录制重放。只记录 `_run.Expanded`漏掉这些成本；单次较深 `AdvanceRound`还包含全队被动、额外回合与选择成本。[`src/Search/CombatBeamSolver.Phases.cs:L1526–L1548`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Phases.cs#L1526-L1548)；[`src/Search/CombatBeamSolver.Retention.cs:L82–L112`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Retention.cs#L82-L112)；[`src/Search/CombatBeamSolver.Phases.cs:L430–L515`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Phases.cs#L430-L515)。

此外，串行最后父节点到节点上限时可能只接收第一个yield，而一些卡牌子节点早已计算完。这是独立的已付工作接收边界，不能通过无条件读完整个惰性迭代器修复，否则又会启动尚未付费的药水或EndTurn分支。本轮不将它包装成“把H改小就解决”。[`src/Search/CombatBeamSolver.Expansion.cs:L509–L570`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Expansion.cs#L509-L570)；[`src/Search/CombatBeamSolver.Expansion.cs:L620–L697`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Expansion.cs#L620-L697)；[`src/Search/CombatBeamSolver.Phases.cs:L1648–L1694`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Phases.cs#L1648-L1694)。

### 3.6 H-R5：运行中直接改H会改变状态键与终止语义

`StateEvaluation`的多人状态键包含 `Horizon - AdvisorEnemyCycles`。到H返回时尚未开始下一玩家阶段；已有H边界节点也已被归为终止候选。因此“先以3跑，原对象改成7继续跑”不是一行政策更新：会影响键、转置、已终止节点和重放一致性。[`src/Search/CombatBeamSolver.StateEvaluation.cs:L1570–L1585`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.StateEvaluation.cs#L1570-L1585)；[`src/Search/CombatBeamSolver.Expansion.cs:L3428–L3432`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Expansion.cs#L3428-L3432)。

本轮自适应缩减模型始终使用不可变硬上限9，仅在3／5／7门槛主动结束请求，不修改H、不重新跑一份预算。生产若要实现必须另行定义这类软停止，而不能恢复一个尚未进入下一开始阶段的边界快照。该复杂度也是暂不选择自适应的理由。

## 4. 实验方法：评价的是现在的动作，不是不同长度的结尾

### 4.1 冻结输入与成本

全部主对照使用Beam8、最多100个宏父节点、同一根和RNG seed=1、总工作上限1000；H14单独使用10节点。每个fixture内部六种策略全部相同预算。`Fallback7`是H7完全相同的调用路径，脚本断言除了名字和计时外所有数据相同。

每个合法动作检查、宏Fork／转移、父节点接纳、保路键、排序比较、资格扫描、共同周期扫描、CP读取、自适应扫描、重复后验和最终重放都计数。一个宏转移记5个操作单位，**不是5毫秒，也不是生产节点价格**。这些是显式可复现的实验计量；Python键按批预计算，C#比较器可能每次比较重新检查动作与CP，因此不能用两者工作数直接换算。计量不是逐条Python指令或内存分配的硬件成本；轨迹序列化和实验设施开销不按生产工作标价，另有真实Python墙钟字段，但不用于声称实机性能。

为保证实验本身不在停止后无限追加验证，全部策略共用384单位的最终发布预留，未用部分不计为已花工作；59点扫描中实际最终开销最大166。**这项保守预留是实验设施，不是建议向生产增加384预算或引入新配置。** 它也可能让搜索较早停止，预算反例只证明这种共享有限算力下有机会成本；必须在真实软截止和墙钟中再验证。结果给出发布前／发布后分别计数，不藏未用预留。

四次重复根后验只用于负对照，确实执行四次宏模拟再丢弃结果。它**不仿冒生产 `PreviousRoutes`**；后者不重放EndTurn，主实验的PreviousRoutes为空。保路本身是固定既有式三通道，始终不多给席位。

### 4.2 共同外部评价协议

先完成某个H的搜索，取得它建议的第一本机动作（含目标／选项的fixture身份），**只承诺这个动作**；随后用同一冻结的因果后续规则μ推进到第12个敌方周期。μ依据当下合法性执行普通后续伤害、规定的用药，后缀分支总选`cash`，不会看到将来的队友扰动或该路线来自哪个H。它不是每种H自己的后缀，更不是每种情景各自重新挑一个全知首动作。

每个扰动情景下成对执行相同首动作评价协议。第9／16周期作为固定次级观测一并输出，不根据结果临时换终点。真实提前胜利／死亡保留完整事实并停止；外部选择不能解决时标为**不可评价／删失**，不计成0HP损失。药水资格不满足单列“无合格路线”，不伪装成一个分数很差但仍可执行的建议。

外部质量采用词典序：本机死亡、救命消耗、逐周期超额、真实胜利、队伍存活、同一时点敌方HP、累计扣血、剩余HP、显式药水、双胜利结束时点；根可行性先行。**没有H、实际深度、K或总动作数。** 其资源字段在本轮宏模型中大多为零，不表示已经验证了所有权语义。根前已付在玩具损失总量中合并保存，生产代码则分别保存根前和新增损失；两者账本对应但不是逐字段镜像。

外部判官及根动作枚举花费另列 `offline_judge_*`，不反馈给搜索或自适应门槛。离线判官本来就用于比较算法，不能把它偷偷当作免费的线上队友情景模块。

## 5. 实际结果、最小反例与反对证据

### 5.1 四种固定上限与自适应的当前动作

|Fixture|机制|H3|H5|H7|H9|自适应|
|---|---|---|---|---|---|---|
|H01|即时输出；2 HP 在目标内但仍按已支付扣血比较|Hit|Hit|Hit|Hit|Hit|
|H02|本机4点入伤必须先防；队友补防只作扰动|Guard|Guard|Guard|Guard|Guard|
|H03|致命控制；不得用远端收益抵扣本机死亡|Guard|Guard|Guard|Guard|Guard|
|H04|本机风险相同先救队友|Rescue|Rescue|Rescue|Rescue|Rescue|
|H05|本机独立延迟收益在周期3兑现|Invest|Invest|Invest|Invest|Invest|
|H06|本机独立延迟收益在周期5兑现|Hit|Invest|Invest|Invest|Invest|
|H07|本机独立延迟收益在周期7兑现|Hit|Hit|Invest|Invest|Invest|
|H08|本机独立延迟收益在周期9兑现|Hit|Hit|Hit|Invest|Invest|
|H09|本机独立延迟收益在周期11兑现|Hit|Hit|Hit|Hit|Hit|
|H10|周期7依赖目标存活；队友提前击杀或改写敌方资源|Hit|Hit|Mark|Mark|Mark|
|H11|共享敌方标记被队友消费；不是队友拿走本机星能|Hit|Hit|Mark|Mark|Mark|
|H12|同seed、不同事件消耗；周期5本机随机收益失效|Hit|RandomInvest|RandomInvest|RandomInvest|RandomInvest|
|H13|同一个首动作，不同条件后缀；外部首动作质量相同|OnlyRoot|OnlyRoot|OnlyRoot|OnlyRoot|OnlyRoot|
|H14|小节点预算到不了5/7/9，不把cap当coverage|Hit0|Hit0|Hit0|Hit0|Hit0|
|H15|外部选择阻断；不得填零损失或假设队友做出最佳选择|Hit|Hit|Hit|Hit|Hit|
|H16|额外本机回合不计周期；两次2HP自损共用3HP账本|Hit|Hit|Hit|Hit|Hit|
|H17|真实胜利早于第一检查点；全终局K是哨兵|Win|Win|Win|Win|Win|
|H18|完整用药资格在周期4才可满足；中间前缀不能提前过滤|无合格路线|Hit|Hit|Hit|Hit|
|H19|观测提示零但周期5有真实收益；自适应假阴性|Hit|HiddenInvest|HiddenInvest|HiddenInvest|Hit|
|H20|持续提示为正但无可兑现收益；自适应假阳性|Hit|Hit|Hit|Hit|Hit|
|H21|周期9依赖目标；加深到9可能使当前投资在扰动下更差|Hit|Hit|Hit|LateMark|LateMark|
|H22|根前已付2HP，治疗/新请求不能刷新额度|Guard|Guard|Guard|Guard|Guard|

表只列选择；真实质量仍以下表同一外部T=12为准。H18没有合格路线与H15不可评价不是随机丢失数据。没有把这22个特意构造的例子当成真人分布，不发布平均胜率或“某策略胜率最高”。

|反例与外部情景|较短策略：当前动作 → 第12周期敌方HP|较长／自适应：当前动作 → 第12周期敌方HP|判定|
|---|---|---|---|
|H06／passive|H3: Hit → 158|H5: Invest → 118|不同首动作，真实抽象收益 +40伤害|
|H07／passive|H5: Hit → 158|H7: Invest → 118|不同首动作，真实抽象收益 +40伤害|
|H08／passive|H7: Hit → 158|H9: Invest → 118|反对保留7的正例：9优于7|
|H10／kill_target_2|H5: Hit → 78|H7: Mark → 98|反对长窗口：当前投资损失20伤害|
|H11／consume_marker_2|H5: Hit → 158|H7: Mark → 178|共享标记失效，损失20伤害|
|H12／rng_shift_2|H3: Hit → 158|H5: RandomInvest → 178|同seed不同事件，损失20伤害|
|H19／passive|H7: HiddenInvest → 118|Adaptive: Hit → 158|自适应漏掉真实收益，损失40伤害|
|H21／kill_target_2|H7: Hit → 78|H9: LateMark → 98|反对默认9：当前投资损失20伤害|
|H13／passive|H3: OnlyRoot → 160|H9: OnlyRoot → 160|首动作完全相同；不是当前动作质量改善|

### 5.2 延迟收益：固定短窗口确实存在下限代价

最简机制是现在`Hit`造成20伤害，或现在`Invest`放弃攻击、在指定第d周期兑现60伤害；第2周期起同一后续规则每周期造成2伤害。无队友干预、无超额，外部第12周期Hit敌方HP158，Invest为118。

当d=5，H3选Hit，而H5／7／9选Invest；当d=7，H3／5选Hit，H7／9选Invest。这是**不同首动作、同一评价时点的40伤害差**，不是深度奖励。它支持当前保留对5–7周期收益的可见性。

反过来，d=9时H9优于H7；d=11时所有四种固定窗口及本轮自适应均选Hit，而离线统一判官知道Invest在T12更好。**七不是天然分界；任何固定有限上限都能构造窗口外回本例。** 是否值得付费看第9周期需要真实样本与预算证据，不能仅凭构造一个d=9案例默认扩窗。

### 5.3 队友扰动：长窗口有可能把当前投资变坏

H10有A=80、B=120两个敌人。短窗当前打B20，长窗给A铺设第7周期60伤害的投资。被动世界中投资好40；队友在第2周期击杀A后，铺垫不再兑现，T12短窗敌方HP78，长窗98。`retarget_2`再触发B的10HP资源变化，两者分别88与108，结论不是因为选择了不同评价时间。

H11使用**共享敌方标记**被队友提前消费的抽象机制；它不表示队友可消耗本机库存。被动世界标记路线T12为118；队友消费标记后为178，立即攻击路线为158。该机制的具体合法游戏实现仍需原生fixture，不能把脚本设定写成已证游戏规则。

H12的seed=1产生首个随机数约0.134364，第二个约0.847434。被动模型给第5周期本机随机消费者前者，得到60伤害；队友第2周期先消耗一次随机事件后，本机拿到后者，没有奖励。同seed是相同随机流起点，**不是相同事件—抽样对应关系**。H5／7／9在扰动后T12为178，H3当前攻击为158。本例只证明同一随机流的消费位移，不证明游戏所有RNG子流都共享或队友某个具体动作一定消耗同一流。

H21把目标依赖回本移到第9周期：H7仍立即攻击、H9投资。于是既有H08反对“7足够”的正例，又有H21反对“9默认更好”的负例。没有校准真人行为的频率，无法合理地对这两个例子算期望差或每周期置信度折扣。

### 5.4 自适应候选的精确定义与失败

硬上限固定9；在现有frontier完整达到3／5／7时检查一次。只有存在至少两个不同当前首动作，且“至少一个既有延迟提示为正，或相比上一个门槛的首动作赢家改变，或最终用药资格仍未满足”时继续。判断、资格、K和排序扫描全部收费。**提示不是回本日期，更不是概率。** 提示消失不代表未来没有尚未观察到的收益。

正例：H01／H02等即时控制可在3停止，同样首动作而少花工作；H06观察到提示并看到第5周期收益；H08可继续到9看见收益。负例：H19提示一直为0但第5周期真实兑现，H7选HiddenInvest、T12为118，自适应在3停且选Hit、T12为158。H20提示一直为正却没有可用消费者，自适应花238单位跑到9，与H7的161单位选择相同Hit。M13构造在第3周期可见观察完全相同、后面真实收益不同的两个世界，说明仅由这些观察不能可靠识别尾部价值。

另一个代价是“首动作改变”触发会滞后：H06第5周期已兑现后，因赢家变化仍多跑到7；H07则多跑到9。当前提示字段和赢家变化不是免费、可靠的停止证书。本轮不通过添加第三个阈值、卡牌目录或未校准概率来事后救回自适应。

### 5.5 真正的预算竞争：付了更多工作却更差

8个根动作中一条投资在第5周期兑现，其余是即时攻击。预算网格固定为510到1960、步长25，共59点，**全部保留**；以下是760点，未为赢家另发预算。

|同一预算760、Beam8|H7|自适应|重复根后验4次＋H7|
|---|---:|---:|---:|
|最深已展开父节点|5|4|4|
|最深已生成周期|6|5|5|
|选中路线周期|5|4|4|
|最终共同周期|5|4|4|
|展开节点|35|31|33|
|搜索／探测转移|41|37|44|
|发布前工作|373|373|376|
|最终发布与重放工作|92|124|154|
|实际总工作|465|497|530|
|当前动作|Invest|Hit0|Hit0|
|第12周期敌方HP|118|158|158|

H7花465单位选择Invest，T12敌方HP118；自适应花497单位却因门槛成本使最终公共K仍为4，选择Hit0，敌方HP158。它已经生成第5周期节点，但没有得到同样充分的联合证据，故不能以“产生了第5周期”声称覆盖相等。四次无收益重复后验也把H7的当前建议退回Hit0，花530单位。

该网格中自适应劣于H7的已测点为760／785／810／835；重复后验退化见760。它们只是本计量下的反例位置，不是实机临界预算或可推广的发生率。部分更大预算点自适应追平，故不能把负例夸成“扫描永远不好”。同样，节省几十单位但首动作不变只算工作节省，不算出牌更聪明。

### 5.6 F10/F12 原样复跑与边界控制

F10原脚本Beam3、不改席位：A首动`decoy`、三周期敌方HP20；B/C首动`engine`、三周期HP50；三者都16展开、19转移、6重放转移，但特征成本不同。这个反例否定“替换一个铺垫席位不会伤害更优铺垫”。本轮主窗口对照用Beam8，**不能把旧Beam3反例冒充已证Beam8也退化**；旧脚本的12个Beam8控制另有完整结果：F10在Beam8下A/B/C均选择`decoy`、敌方HP20，原退化消失；其工作分别为52／76／76。这是必须保留的反对证据，不能把Beam3的损失外推到本轮Beam8。F12是前述同分项假改善，不能用于支持缩短窗口或自适应。

H02在可能得到队友补防时仍按不补防的条件根建议防御；有补防时它可能较保守，但不能据此提前透支防御。H03死亡优先不变；H04本人风险相同先救队友；H16两次2HP自损发生在同敌方周期，超额1而不是各自免额；H22根前已付2HP继续计入。H18到第4周期才有显式药水，H3无合格最终路线，较长策略有，说明更短窗口还可能减少政策可满足性。

H13只有同一个首动作，较短后缀选cash、较长选charge；外部统一μ下T12都为160。后缀质量可能不同，但**本轮目标“当前动作更好”没有变化**。H15外部选择全部标为删失；不能对其宣称到12仍安全。

## 6. 原始资料：用于选择测量方法，不用于代替本项目实验

|原始资料|本轮实际采用的结论|不能直接迁移的部分|
|---|---|---|
|Janner、Fu、Zhang、Levine，NeurIPS 2019，*When to Trust Your Model: Model-Based Policy Optimization*，§4–5，[官方论文](https://proceedings.neurips.cc/paper/2019/file/5faf461eff3099671ad63c6f3f094f7f-Paper.pdf)|模型使用长度有误差／收益权衡；悲观界本身不足以证明应多用模型，需要实测泛化|其学习模型、SAC、真实数据分支短rollout不是CombatSolver的单次Beam；不能把其k取值套成3或7|
|Talvitie，AAAI 2017，*Self-Correcting Models for Model-Based Reinforcement Learning*，[AAAI原文](https://ojs.aaai.org/index.php/AAAI/article/view/10850/10709)|一步预测准确不能代替组合后续的质量验证|本机引擎的条件结算正确与遗漏真人动作不同；没有训练数据，不建议加自纠错学习器|
|Rawlings、Mayne、Diehl，*Model Predictive Control: Theory, Computation, and Design*，二版第三次印刷，印刷页90、164，[作者官网PDF](https://sites.engineering.ucsb.edu/~jbraw/mpc/MPC-book-2nd-edition-3rd-printing.pdf)|区分长后缀计算与当前控制动作；有限窗口本身不保证稳定或最优|用户只有手动重算，不是自动每步反馈；本游戏不满足其连续控制稳定性条件|
|Nau，Artificial Intelligence 19，1982，*An Investigation of the Causes of Pathology in Games*，摘要与引言，[作者原文](https://www.cs.umd.edu/~nau/papers/nau1982investigation.pdf)|“更深搜索不必改善决策”需要作为可检验命题|论文的game-tree pathology不是本项目Beam退化的直接证明；本轮结论靠自己的成本及扰动反例|

以上均实际读取相应原文／PDF页面，不是只看算法名称。向本项目的推断是：把当前前缀置于统一外部情景评价，分别记录搜索覆盖、模型条件和预算；没有依据给第t周期凭空赋一个“可信概率”。

## 7. 为什么仍选7，而不是只说“缺数据”

固定3／5已被独立5／7周期收益例和H18资格例反驳为无条件替代；固定9被目标失效机会成本反驳为无条件改进，同时它在独立第9周期收益上确有价值。自适应既漏报又空跑，还在同预算网格出现**工作更多、当前动作更差**，其字段不提供可校准的可靠停止证书。当前更小、可维护的政策因此是保留7，而不是部署额外门槛。

**反对本主方案的证据必须保留：** H08表明7漏掉第9周期收益；H10／H11表明7也会受真人操作扰动；H09表明所有已测上限都可能短视；H14表明多数加深承诺在小预算下没有实际覆盖。保留7仅保留现有能力与复杂度，不承诺稳健、最坏情况或最优。

“应该让多深的证据影响当前动作”的回答是：本轮仍允许真实展开、在共同边界可比较的至多7周期条件证据影响当前动作；风险以本路线完整已知事实约束。**不是固定只信前三周期，也不是凡第7周期有好分就当真人一定配合。** 更深影响必须能指出是哪个首动作在同外部评价协议下改善，不能只展示远端后缀／动作数／K变化。

具体生产路径只有两批，详见设计：第一批补齐可观测周期与同根外部判官；第二批仅在实际入口复现后合并同一已知发布时点的比较上下文。F12继续作为不计收益的门禁，资源所有权／原生消费者仍独立处理。生产数据若显示固定5在明确场景组中风险不增、延迟损失可接受且节约的真实工作有用，或9在独立长线组中改善而扰动不退化，再单独提交更改默认值的证据；不把本报告变成永久禁止改H的规则。

## 8. 不可验证、未读与读取失败

没有 `dotnet`、游戏DLL、失败战斗包、真实主机／客机或可见UI。本轮没有编译C#、没有运行生产合同、没有原生实际／模拟差分，更没有真实联机实测。22个宏fixture不证明游戏可达性；资源、暗球、星能只作为机制占位，现役原生消费者缺口没有因本研究自动解决。

实际读取范围见 `CombatSolver_Multiplayer_Horizon_20260919_Source_Reads.md`与JSON清单。读的是需要的调用链与上下文，不是宣称通读2056个文件或整个8万行搜索器。未全读全部卡牌镜像、第三方mod、所有同步网络实现、完整转置机制、全部单人策略和庞大测试矩阵。当前附件的必要文件均可读，**没有必须依赖GitHub正文才能补齐的当前源码**。

路径检索曾假设独立存在`SearchNode.cs`、`SearchPathObservation.cs`、`CombatBeamSolver.Diagnostics.cs`、`HarnessOptions.cs`；实际分别在`CombatPlan.cs`、`SearchDiagnosticsSink.cs`、`CombatBeamSolver.PathDiagnostics.cs`、宿主`Program.cs`，已纠正，不能称为源码缺失。外部L’Ecuyer随机流PDF尝试被拒绝；未将其内容当作来源。Nau第一次猜测的PDF路径失败，随后作者正确路径可读。论文只读取了本题相关段落与页面，没有冒称全文逐页复核。

### 可证伪交付

修改fixture的回本周期、提示、目标依赖或预算即可推翻对应局部结果；完整59点网格和所有负例均在JSON中。生产若发现相同阶段根本无法产生M01池结构、K未变、或建议首动作差异只来自F12同分项，则本轮拟议行为补丁不以“理论更好”为理由上线。所有界限与回退属于设计而非本轮已实施事实。
