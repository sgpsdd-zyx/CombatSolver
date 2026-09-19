# CombatSolver 多人窗口策略设计：固定七周期与同一发布时点的一致比较

**日期：2026-09-19；唯一当前源码 `3ccac172dd55ad4a8d97074b7129cbf4155add65`。这是研究设计，不是已经实施或已获生产验收的代码。**

## 1. 唯一主方案与低成本回退

### 1.1 默认决定

**主方案：固定 H=7，保持三通道 Beam、3 HP 逐敌方周期账本、原请求预算与手动操作；用同一已知发布时点的资格合格候选冻结K，避免先按局部完成池的深K丢掉联合池需要的路线。** 先在现有离线宿主复现，再决定是否启用这一个窄行为修正。窗口本身不增加配置界面或新搜索器。

**回退：`3ccac17`原样固定7，原三通道、原完成池管线和比较器。** 观察器只留在离线合同，运行时关闭。生产窗口修改、信号门槛、队友模型、场景集成、两步挑战全部不启用。

默认7的理由不是“历史上是7”或“没有DLL”：新实验已证明3／5会有独立延迟收益损失，9会带来目标失效投资损失，自适应会出现信息假阴性、空跑和真实预算机会成本。七同样有缺陷，但当前替代方案没有给出足以承担复杂度和回归风险的改进证据。独立第9周期收益例H08明确反对7已最优，这条反对证据保留为未来改变默认值的入口。

### 1.2 本轮不做什么

不改官方单人 `0e6cc2d / 0.41.0` 的路径、参数、预算、缓存、评分、能力承诺或既有输出。不会用多人名义打开 `PowerCommitment`、提高全部能力牌评分、增加Beam或延长时间。不控制真人、不自动重算、不把未兑现长线估值换算成实际防伤，不改变救命资源与3 HP超额的既有相对顺序。

本轮也不承诺修好全部历史比较缺口。F12动作数同分项保留为收益判定禁区；跨未来未知批次的信息损失、末父节点付费子项、原生资源消费者和所有权政策分别列出，不塞进一个“窗口优化”大补丁。

## 2. 可验收的质量定义

设冻结根为s，本次算法选择可执行首动作／当前回合前缀p，队友扰动脚本为ω，统一因果后续控制规则为μ，固定外部评价时点T。评价对象为：

```text
Q_T(p; s, ω, μ) =
    原生/已声明抽象环境在相同s中先执行p，
    后续只由同一个μ依据当前观察继续，
    队友按同一个ω动作，直到T或真实终局。
```

这里不是拿各自H的终点HP比较，也不是让每个ω各自挑选一个最佳p再求平均。μ不得读未来事件、算法名字或将来的队友选择；ω是压力测试，不是概率分布。真人改变战斗时只停止过期建议或等用户手动重算，不能新增自动操作。

### 2.1 决策维度与单位

|维度|单位、边界与归属|本轮处理|
|---|---|---|
|合法性／药水资格|本机合法动作、目标和选择；显式药水次数；冻结药槽指令|先资格再最终批次；未用药可展开前缀不提前删|
|本机死亡|真实模拟终局布尔事实|任何短K都不隐藏本路线后续已知死亡|
|可比较性|是否有恰好K的完整CP，或真实终局|不把未知尾部当0风险；外部选择不是安全证明|
|救命资源|现役累计消耗次数，当前部分观测为全队口径|保留排序；不因窗口设计悄悄重分所有权或优先级|
|本机超额|每周期 `max(0, 本周期扣血−3)`之和|根前扣血、自损、额外回合、治疗不刷新或结转额度|
|胜利|真实全敌死亡；终局事实完整|与早检查点分开，不用预计胜利替代|
|队友存活|相同边界存活人数，后续已知更坏值仍有效|本人风险相同再考虑救援；人数不能表达所有威胁差异|
|有效敌方HP|同一评价时点的HP量|不把目标已失效的投资当已造成伤害|
|扣血／剩余HP|本机已支付损失与边界HP|治疗不注销风险账本|
|显式药水／结束时点|合格路线实际使用；双真胜利比较结束时间|保留现役语义，不把自动救命当本机显式用药|
|动作数／深度／K|结构或成本信息|不进入本轮“当前动作质量提升”的证据|

生产比较器的准确顺序见 [`src/Search/CombatBeamSolver.MultiplayerEvaluation.cs:L71–L115`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.MultiplayerEvaluation.cs#L71-L115)。外部宏模型没有模拟所有资源触发，不能据其零计数证明生产所有权正确。根前与新增损失在生产分开保存；抽象实现合并在同一账本进行逐周期超额计算，该适配差异写在JSON元数据。

### 2.2 “收益”“节省”“无效变化”分别验收

只有p改变，并在相同s／ω／μ／T下高优先级风险不变而真实输出、救援、药水或终局改进，才计为本轮首动作质量收益。同一个p但搜索后缀不同，只算条件后缀变化；p和结果都相同但少用工作，只算算力节省；只有K或总动作数同分项改变，一律不计质量收益。外部选择未解时是删失，不与完整路线算平均。

第12敌方周期是本轮主外部时点，9／16是固定次级时点。真实生产fixture可因合理机制预先规定另一个共同T，但不得根据某策略赢在哪个时点事后换口径。对真人行为未校准时不使用逐回合置信度、概率折扣、期望胜率或“安全概率”。

## 3. 五种窗口候选的实质比较

|候选|可解释的长处|本轮反证|生产选择|
|---|---|---|---|
|固定3|即时控制能更早停止，较少依赖遥远队友静止假设|H06漏第5周期40伤害；H18药水资格无法完成；省下工作未自动用于当前回合|不改默认|
|固定5|覆盖一些中期独立收益，较3少短视|H07漏第7周期40伤害；H12第5周期随机消费者已可能因队友偏移失效|不改默认|
|固定7|保留5–7周期收益可见性，不引入额外门槛|H08漏第9周期；H10/H11被队友动作反驳；小预算H14到不了7|**保留默认，但不声称最优**|
|固定9|H08看见真实第9周期收益|H21目标失效使本机当前动作损失20伤害；远端工作仍可能被较浅K抵消|仅离线对照|
|单次请求的门槛自适应|在即时控制组到3停；有提示可到5／7／9|H19漏信号，H20空跑；预算760点更多工作却少40伤害|不部署|

不能直接改用“当前回合强搜＋尾值”逃避上述问题：目前没有覆盖原生暗球／星能等所有消费者的已验尾值，误差仍会回到未兑现收益上；而短H不会自动转移现役4／8层预算。少量队友情景也不是免费的稳健保证：它们要共享可执行p、都计原预算，且只对列出的ω成立。MCTS／学习模型没有本轮必要性证据，故不引入。

原论文依据用于界定这些取舍，而非提供某个H的数值答案。MBPO讨论学习模型长度与误差；MPC区分当前控制和远端规划；Talvitie研究模型误差的多步组合；Nau提醒更深搜索不保证更好。它们都不证明这个冻结引擎在七周期附近有数学最优点。完整原始出处见报告第6节。

## 4. 对自适应候选给出可执行定义，再明确拒绝默认上线

### 4.1 研究伪代码

以下伪代码是算法设计，**不是声称已经存在或编译通过的C#接口**。实际标准库实现见实验脚本 `solve`、`common_cycle`、`batch`。

```text
H_hard = 9                         # 本次请求不可变
next_gates = [3, 5, 7]
last_gate_winner = None

SearchWithExistingBeam(root, same_request_budget):
    while 原始搜索还可继续:
        按原合法动作、Fork、转置、三通道和预算继续
        在已完成的自然层边界，读取已有候选；不重新模拟整个根
        d = 活跃池最小的已完成敌方周期
        if d不是尚未检查的3/5/7门槛: continue

        charge(资格、原始字段、共同周期、键、排序和遍历成本)
        eligible = 完整已知发布池的现役药水资格过滤
        ordering = 按eligible冻结一次共同K
        current = 该批选中路线的当前本机动作身份
        changed = 上一门槛存在 且 current != last_gate_winner
        delayed_signal = 已保留节点有既有长线提示值 > 0
        unresolved = 当前最终药水资格未满足但仍有可展开前缀
        prefixes = 当前池不同可执行本机首动作身份

        extend = len(prefixes) >= 2 AND (delayed_signal OR changed OR unresolved)
        last_gate_winner = current
        if not extend:
            用本次已知完整事实做正常最终发布；停止该请求
            # 不把深层死亡／超额重置为较浅CP
```

“硬9＋软门槛”只避免运行中改H的键／边界矛盾，不代表可靠。门槛的时间单位必须是已完成敌方周期，额外本机回合不能使门槛前移。自然层可能混合深度，本轮H16出现选中D=4而K=3，因此软停止也不能声称恰好只模拟到3。

提示值不含回本日期或真人存活概率。H19说明零提示不能证明没有延迟收益；H20说明正提示不能证明存在消费者。H06／H07的赢家变化还会在收益已经兑现之后继续多跑。扫描全部计入原预算后，760点自适应花497单位选Hit0，原H7花465单位选Invest；外部T12分别158与118。**本轮自适应被自己的停止证书和成本反例反驳，故只保留为研究对照。**

### 4.2 为什么不能用一次坏后缀否定整个首动作

设同一p有后缀r₁、r₂。r₁在第6周期死亡，是 `(p,r₁)`的反证；它不能证明所有 `(p,r)`都会死。原比较器保留该具体节点完整坏事实是正确的。窗口变短不能把已知r₁改写为“安全p”，但另一个未经展开的p前缀也不能被算成已经安全通过第6周期。

本轮保留“已知风险”和“未知尾部”的区别，不额外保存一个跨所有分支的首动作黑名单。因信息不对称而偏好未知前缀可能仍发生；解决它需要同预算的更好覆盖，而不是取消风险检查或给未知续行凭空发安全分。

## 5. 最小生产行为切口：同一已知发布时点一致比较

### 5.1 只修可限定的命题

当前 `PrepareMultiplayerFinalCandidates`内部先资格、冻结K、排序和截断已正确。剩余反例发生在 `Phases:1909–1947`：完成池按自己的K提前4B压缩，然后才与较浅的非终止池合流。目标命题限定为：

> 在**同一个已知发布时点**，已经生成且通过最终资格、被纳入本次发布输入的候选，不能因为先从completed局部池裁掉而在同一时点的联合比较中失去原赢家。

不是保证任意未来候选加入后历史赢家都还能找回。也不是统一把所有候选强行扩到某个K，更不是把completed容量从4B加到8B。

### 5.2 先得到实际frontier，再冻结本次联合发布池

**本设计只采用一种作用域定义：本轮实际保留的frontier，加尚未局部压缩的completed候选。** 非终止节点仍按原三通道保路，不把全部原始候选或已被frontier保路拒绝的浅前缀混进最终K。最终资格过滤仅作用于发布池；仍可展开但尚未用药的前缀继续活在frontier，不被最终过滤误删。

因此需要在显式多人分支中先执行原frontier保路，再冻结联合K，最后压缩completed子集与发布联合子集。单人整段执行顺序原封不动。多人既有incumbent更新可移到新completed确定之后；源码检索显示`_primaryIncumbent`的剪枝消费者为`ApplyPrimaryIncumbentBound`，该入口已有多人排除，仍须在生产合同里证明调用顺序调整没有其他行为差异。

```text
FinishLayer():
    if policy.Multiplayer is null:
        执行原版单人整段，顺序／参数／调用次数不变
        return

    completed_raw = old_completed + ended_terminal
    nonterminal_raw = ended_nonterminal

    # 唯一一次原有三通道保路。非终止前缀不按最终药水资格删。
    frontier = ExistingPrune(nonterminal_raw)

    scope_eligible = FilterFinalEligibility(
        ReferenceDistinct(completed_raw + frontier))
    ordering = CreateMultiplayerOrdering(scope_eligible)

    completed_eligible = scope_eligible ∩ completed_raw
    completed_next = SortAndTake4B(completed_eligible, ordering)
    ReleaseDroppedSnapshots(completed_raw, completed_next)
    UpdateExistingIncumbentAtSameLayerBoundary(completed_next)

    # 已被裁掉的completed不能让这一批重新计算K。
    publish_eligible = (completed_next + frontier) ∩ scope_eligible
    publish_batch = SortAndTake4B(publish_eligible, ordering)
    PublishAndSelectFromSameBatch(publish_batch)
```

具体复用 `MultiplayerFinalBatch`和`MultiplayerPlanOrdering`，最多拆出“资格过滤”与“使用已冻结ordering对子集排序”两个私有方法；**不用独立Planner、registry或新长期缓存**。保留原 `PrepareMultiplayerFinalCandidates(nodes)`作为独立新批次入口，原R1/R2合同继续调用它。新辅助入口仅供同一作用域内的子集使用，不能拿旧ordering比较后来加入的新节点。

在这一限定命题下有一个简单证明：如果某个completed候选被同K的4B排序删除，至少有4B个同属completed、且不差于它的候选；联合池也不可能需要它进入前4B。M01的A是在**不同K**先被删，故不满足证明前提。固定同K可以修该原因，但不会消除中间Beam本身的信息损失。

**反对证据和成本必须正面处理。** 提前用所有raw非终止节点冻结K更便宜，但M16表明：浅锚C后来被原frontier保路拒绝时，raw作用域仍以K3选A，实际幸存者作用域以K7选B。这是不同策略，不是免费实现替代。因此不采用“省略正常保路结果但仍叫同一发布池”的版本。

推荐顺序会让未压缩终止候选的模拟器稍晚释放，可能增加一次frontier保路期间的峰值存活量；这不是零成本。如果现有内存门禁不能承受，或需要另建多K长期缓存才能实现，不增加预算救方案，直接回退原管线。不要未经验证就提前释放全部terminal再为每次预览重放，那会产生另一个成本／所有权问题。本轮只用候选级M01/M16证明范围内的比较现象，没有在132次宏Beam主运行中模拟这个生产补丁，也没有证明它在真实战斗提高首动作质量。

### 5.3 新批次、预算截止、预览和最终选择

同一发布输入的子集只复用该次ordering；输入中新加入真正新节点、恢复药水边界fallback、预算停止时纳入`advisoryLastCohort`，都形成新作用域。此时对完整新池按R1过滤并冻结新K，不能偷用更深的旧K。该规则仍不能复活更早已被删掉的节点；要保证跨所有未来浅池不变需要额外多K档案，本轮明确拒绝。

`PublishRoutePreview`本身有`HasSimulator`筛选，且旧当前回合预览有独立摘要路径。本轮不把它们当作已与最终批次全面统一。新作用域的路线预览若无法物化所选released候选，应保留已有明确的旧预览／无新预览，不得偷偷用可显示子集重新算K并声称它就是同批选择；也不为每次进度显示免费重放一次。完整最终物化继续走现有释放／重放路径，计原预算总成本。

实际切口：[`src/Search/CombatBeamSolver.Phases.cs:L868–L938`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Phases.cs#L868-L938)；[`src/Search/CombatBeamSolver.Phases.cs:L1909–L1947`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Phases.cs#L1909-L1947)；[`src/Search/CombatBeamSolver.Phases.cs:L2049–L2104`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Phases.cs#L2049-L2104)；[`src/Search/CombatBeamSolver.Multiplayer.cs:L16–L45`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatBeamSolver.Multiplayer.cs#L16-L45)。

### 5.4 风险、取消和预算所有权

所有步骤使用既有请求profile和token；没有“为正确性额外给一份时间”。统计至少分出生成、保路、资格／CP扫描、预览、旧前缀重验、最终重放、排空与墙钟。固定节点数不是固定总工作量，转移数也不是完整墙钟；必须同时报告。

新增作用域的资格与最小K尽可能在一次遍历完成；已完成的纯资格结果在同作用域复用，不跨请求缓存。比较器本身的行为保持现役风险顺序。若一次新扫描不能在原截止策略下完成，不得留下“已释放一半候选但尚未确定K”的中间状态。先构造纯候选引用／整数结果，再提交排序与释放；取消交给原请求取消／排空路径处理，而不是返回一个悄悄忽略死亡的浅建议。

本轮Python的384单位最终预留不进入生产。生产原本是软截止，强制EndTurn排空、恢复released选择和录制重放可能超过该软时点；既不在报告中假装它是硬实时，也不趁本轮修改官方单人预算。生产对照应在请求外层测完整结束，给出超时和尾部开销；更多扫描导致高优先级建议退化或原墙钟档位更常超时就是停止条件。

## 6. 四量统计的最小接入与状态所有权

### 6.1 只增加必要的观测

|字段／对象|来源与归属|Fork／生命周期|是否入状态键|
|---|---|---|---|
|H|冻结 `MultiplayerSearchPolicy.Horizon`，默认7|每请求不可变；实验不同H使用新的solver请求|现役多人键已有H−cycles，不改|
|D_parent／D_generated|现有路径观察的Expanded／Generated事件，新增可选`EnemyCycles`纯整数|只有离线collector聚合；不保存SearchNode引用|不入|
|D_selected|物化后的`SolverResult.AdvisoryEnemyCycles`|结果值，不持有树|不入|
|K及origin|最终`MultiplayerFinalBatch`的ordering，附“普通CP／全边界／全终局哨兵”诊断类别|一次发布作用域；新批次重新冻结|不入|
|scope_eligible|本次已知发布输入的资格合格节点引用|只在本轮提交期间使用；释放沿既有工具，下一轮不长期保留|不入|
|门槛信号／winner|仅Python或离线对照实验|不进入默认生产；不能成为live读取口|不入|

`SimulatedCombatState`已有周期标量和不可变CP链，Fork复制标量、共享链；无需为了统计新增一个可变历史列表。`SearchPathObservation`明确禁止持有节点、snapshot或模拟器，所以只增加纯值字段，不把整棵树送给日志。[`src/Search/SimulatedCombatState.cs:L436–L447`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/SimulatedCombatState.cs#L436-L447)；[`src/Search/MultiplayerCycleCheckpoint.cs:L1–L6`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/MultiplayerCycleCheckpoint.cs#L1-L6)；[`src/Search/SearchDiagnosticsSink.cs:L129–L163`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/SearchDiagnosticsSink.cs#L129-L163)。

`Horizon`运行中不可修改。旧 `PreviousRoutes`最多四条纯当前回合动作，仍由新的冻结根重新验证，不保留旧H下的分数、CP或模拟器；本轮不建立按H保存多份树的缓存。

### 6.2 F12的单独最小探针，不默认部署新比较政策

可以在合同里计算到K的动作前缀长度：沿`SearchNode.Parent`寻找最早已达到K检查点的祖先，取其`ActionCount`；真实终局继续使用完整终局长度。若一个动作跳过多个边界或祖先信息不足，必须显式标记无法得到更细粒度，不伪造数值。父链在`CombatPlan.cs`已有，released simulator并不等于父链消失。[`src/Search/CombatPlan.cs:L1060–L1084`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatPlan.cs#L1060-L1084)；[`src/Search/CombatPlan.cs:L1129–L1159`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/src/Search/CombatPlan.cs#L1129-L1159)。

这个计算可能遍历很多动作，不能在比较器每次调用时无限重复；合同可一次派生并计成本。**本轮不把它加入默认排序**：M03只表明可消除一个同分偏差，不证明改成新的平局选择更好。研究结果中的质量计分已经排除它，所以无需先改生产同分项才能比较H。需要后续行为调整时，另以该探针和F12验证，不借“窗口优化”改动风险目标。

## 7. 与实际宿主连接的最小实验方案

### 7.1 可复用入口及现有证据边界

`MultiplayerLongTermContracts.Run`已能捕获真实双玩家根、构造含能力／Dark／星能的输入，并用相同根比较“启用观察器／不启用观察器”。当前它**硬编码H=3**，因此不是已经比较3／5／7／9的窗口实验。需要参数化的是这个离线测试入口，不是上线一个大配置面板。[`tools/OfflineSearchHarness/MultiplayerLongTermContracts.cs:L45–L97`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/tools/OfflineSearchHarness/MultiplayerLongTermContracts.cs#L45-L97)；[`tools/OfflineSearchHarness/MultiplayerLongTermContracts.cs:L100–L158`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/tools/OfflineSearchHarness/MultiplayerLongTermContracts.cs#L100-L158)。

`MultiplayerContracts.Run`已有默认7、单独5、短预算、额外回合、选择边界和原生六周期状态对账的测试形状。`MultiplayerEvaluationContracts`有当前根、1／2周期铺垫、预算四节点、后续已知风险与即时胜利。最终资格和共同周期沿 `MultiplayerFinalSelectionContracts` 扩展，不另造一套生产比较器。官方单人对照沿现有五卡能力根，要求多人入口零进入、非时间字段和RNG路径不变。[`tools/OfflineSearchHarness/MultiplayerContracts.cs:L35–L123`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/tools/OfflineSearchHarness/MultiplayerContracts.cs#L35-L123)；[`tools/OfflineSearchHarness/MultiplayerEvaluationContracts.cs:L85–L140`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/tools/OfflineSearchHarness/MultiplayerEvaluationContracts.cs#L85-L140)；[`tools/OfflineSearchHarness/MultiplayerEvaluationContracts.cs:L214–L275`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/tools/OfflineSearchHarness/MultiplayerEvaluationContracts.cs#L214-L275)。

仓库文档记载这些历史生产／原生测试曾通过，包括80项单人对照；这不是本轮重跑。原生暗球储值／激发与星能获得／花费消费者差分仍独立待办，不能因为root快照有对应字段就判断全部语义已验证。[`docs/TEST_MATRIX.md:L5–L34`](https://github.com/sgpsdd-zyx/CombatSolver/blob/3ccac172dd55ad4a8d97074b7129cbf4155add65/docs/TEST_MATRIX.md#L5-L34)。

### 7.2 原生差分与质量对照分开

先验证“同一真实合法动作前缀，经原生结算与嵌入引擎的状态一致”；再比较不同算法选出的前缀在同一外部环境中表现。前者不是质量对照，后者也不能替代原生语义差分。

每个原生fixture冻结一次建局定义；各个H从等价根开始，用相同Beam、DOP、RNG、时间／节点上限和旧路线配置。顺序交叉执行，不用同时运行多算法污染墙钟。测固定模拟量时同时记录额外扫描与重放；测固定墙钟时仍保留原节点上限。保留暖机与运行顺序、失败退出、取消排空、最终物化和峰值内存的原始记录。

外部判官只让当前选出的p保持相同，再按同一确定的合法后续脚本执行到T12；对第三方选择或合法性变化没有实现时标为删失。队友动作扰动采用冻结脚本，如在第2周期先击杀目标、补防或不补防、消费某个已证明共享的资源、执行一个确实触发相关RNG流的动作。**在消费者／RNG分流语义未证明之前，用脚本名字替代真实合法性不算原生fixture通过。**

单一“队友不主动操作”依旧不是一般最坏情况。额外提供的ω各有前提，不按0.5／0.5平均，不从少数无头场景外推真人的配合频率。若某前缀在某ω下失去目标或进入外部选择，应正确停止／拒绝，不由判官偷偷替它换目标继续算高分。

## 8. 最多两批实施计划与硬停止条件

### 第一批：补齐可观测窗口与同预算生产实验，不改默认行为

**最少文件：** `SearchDiagnosticsSink.cs`、`CombatBeamSolver.PathDiagnostics.cs`；`MultiplayerLongTermContracts.cs`或同目录一个薄合同文件；仅必要时在`Program.cs`增加离线入口参数。观察器关闭时不遍历父链、不分配新集合；Runtime、Coordinator、单人政策、共享权重均不改。

实现内容：给已有观察事件附敌方周期整数，在现有Expanded事件汇总D_parent；最终输出H、D_generated、D_selected、K及全终局哨兵标记。复用现有真实根构造，按H3／5／7／9各自一个同预算请求；自适应仅为离线挑战，不能每个门槛重启solver重新领预算。跑下表对应最小状态；不按职业×人数×卡名笛卡尔积扩大。

|必需哨兵|最小生产形状|验收／停止|
|---|---|---|
|当前风险|真实本机必需防御／致命；根前已付与额外回合自损|3 HP账本及死亡优先不变；任何静默超额／自动操作即停止|
|独立回本|已验证消费者分别在第3、5、7周期兑现；另设第9周期支持反对意见|比较相同T的首动作，不拿各自终点HP替代|
|目标依赖／RNG|同当前根，队友先杀目标；已证明合法的同流随机消费|可复现输入→事件→轨迹差异；未证明消费者或流归属就标未验证|
|混合深度|节点限制使生成深度大于选中／K；额外回合混层|四量分别准确，不用SearchedTurns|
|终局／外部选择／药水|周期0真胜利；队友选择阻断；中间尚未用药、最终才满足|终局完整事实；删失明确；R1/R2原合同通过|
|机会成本|有改善空间的第5周期根，额外扫描／重复后验吞掉原预算|包括负例；不得增加节点／时间使失败消失|
|官方单人|原五卡能力对照|多人相关入口零进入，既有80项非时间对照无差异|

无观察器和有观察器的固定工作量测试必须给出首动作、节点、转移和状态键差分；墙钟变化不能被“诊断无行为”口号覆盖。若观测本身导致原预算下更多截断，仅用于离线采样，不启用默认日志。**本批结束仍默认H=7。**

### 第二批：仅修同一已知发布输入的局部完成池压缩

**进入条件：** 第一批能在生产比较器入口复现M01结构，区分真实生成池与人工候选池；R1/R2、终局、后续坏事实和单人隔离保持通过。即便人工候选合同通过，没有真实搜索证据时也只能将补丁标为比较一致性修正，不能宣称提高胜率。

**最少文件：** `CombatBeamSolver.Multiplayer.cs`拆资格／冻结／子集排序的私有逻辑；`CombatBeamSolver.Phases.cs`显式多人发布作用域接入；既有最终选择合同增加M01及混合边界用例。尽量不新增类型，复用`MultiplayerPlanOrdering`与`MultiplayerFinalBatch`；不改`FinalPlanOrdering`的单人路径，不改`MultiplayerCycleCheckpoint`战斗事实，不改模拟器。

验收：在同一已保留frontier的联合发布作用域中先资格、固定K、各子集使用同K；M01下恢复A，M16不让已拒绝的浅锚影响新作用域；同一作用域预览／最终选择不重算K。新候选加入形成新批次，不能复用旧K。completed不超过4B、frontier不超过原Beam／既有路由预算；没有额外Fork、额外模拟器长驻或第二条搜索树。每次取消／排空按原所有权释放，后续请求根状态不变。外部首动作质量门禁排除F12；同工作与同墙钟的不可退化哨兵分别报告，不能用较慢或更深掩盖退化。

**硬停止／回退：** 单人任何入口或非时间字段变化；3 HP账本、资源／药水资格／真终局变化；未知被写成安全；任一已有死亡／救援哨兵退化；窗口改动引入自动请求或操作；同预算新增扫描再次造成已测首动作负例且没有预先接受的政策取舍；内存需靠增大上限才能通过。任一满足即回退至原 `3ccac17`多人管线，默认仍7。不得为救补丁扩大Beam或时间。

如果生产入口表明M01只是不可达合成池、或者范围可控的修正必须演变为跨所有历史K存档，则第二批不做。保留第一批合同与离线统计，任务以“替代窗口未获部署支持”结束，而不是继续造模块。

## 9. 可直接交给Codex的任务边界

```text
基线必须为3ccac172dd55ad4a8d97074b7129cbf4155add65。
先执行第一批：只补离线四量观测和同预算H3/5/7/9实验。
默认H保持7；不启用自适应，不实施两步挑战、能力加权或队友情景规划器。
把原生语义差分、生产排序合同、实际搜索质量、真人联机分栏，不混写通过。
复现M01后，才做第二批“同一实际联合发布作用域固定K”的多人小补丁。
保持完整最终资格先行、未用药可展开前缀保留、后续已知坏事实和真终局优先。
F10/F12必须保留，动作数／深度／K提升不计质量收益。
原单人执行顺序、参数、预算、状态键及能力承诺入口不变。
不增加Beam、时间、节点、长期缓存或可配置策略框架；任何新扫描计真实成本。
输出失败基线、补丁后、相同外部T12的当前前缀对照、取消/释放和单人零进入证据。
没有生产／原生证据就写未验证；不把本研究当成发布或推送授权。
```

## 10. 重现、来源与结论被推翻的条件

在Python 3.13.5实际运行；标准库脚本无需联网：

```bash
python CombatSolver_Multiplayer_Horizon_20260919_Experiments.py --out CombatSolver_Multiplayer_Horizon_20260919_Results.json
# 完整ZIP中另附从当前源码归档原样复制的旧Python：
python CombatSolver_Multiplayer_Horizon_20260919_Experiments.py --out CombatSolver_Multiplayer_Horizon_20260919_Results.json \
  --legacy-script CombatSolver_Multiplayer_Horizon_20260919_Legacy_20260918.py
```

新主实验132次、预算网格177次、微检查16项；固定K独立检查720种排列和216组三元关系。旧脚本51＋12次运行与13项检查是另外一层证据。二次新脚本运行的确定性摘要：`715ba48b768548bd329204c0bf9991bc2e4b31b244bc27ae9d30c5dd3ca92128`。这个摘要排除实际计时和旧脚本包装，不排除新主实验、网格、微检查或外部评价内容。

原始算法资料及源码物理行号在报告与读取清单中可定位。本设计没有声称论文证明七最优，也没有将Python变成C#镜像。Python抽象有一次宏动作通常完成一个周期、简化保路与缓存键、固定后续规则、共同最终预留等偏差；真人对局、资源消费者、C#时序与墙钟都仍需原生生产验证。

**改变本轮决策的充分研究方向：** 在预先声明的真实冻结根集合中，某个H或简单门槛能稳定改变当前p，并在相同T、相同合法ω、相同预算下改进真实输出／救援／终局而不增加高优先级风险；负例被解释而非删除；改动不依赖新增不透明权重或更大预算。达到这些条件就应重新评估7。反之，只有更大H、更深D_selected、更小动作数、更多“被保路”的能力牌，均不足以改变默认。
