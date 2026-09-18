# CombatSolver 0.41.2 多人策略优化设计

**固定基线：fork 0.41.2，提交 `2dc5d15b26f16d89436af0fb98650d8b4cf6b411`。研究日期：2026-09-18。**

本设计只针对本机多人建议；官方单人原路径、原参数、原能力承诺与原行为均保持不变。只在用户手动请求时捕获当前全队状态，只生成本机动作，由用户手动执行。队友无需安装 Mod，也不发送计划或保证配合。预算、3 HP 敌方周期目标及最多七周期窗口不扩大。

**最终选择：优先采用“证据一致的保守多人 Beam”；低成本回退为“P1 正确性修复＋现役三通道 Beam”。撤回上一轮把单席位两步挑战作为优先下一步的建议。** 本轮没有实现下述伪代码、修改仓库或发布行为。

## 1. 质量目标：先把“更可靠”写成可以失败的测试

本机建议质量不是搜索层数越大越好，也不是单个加权分越高越好。我们需要在相同冻结根、信息政策、工作与墙钟约束下，减少已经可以定位的错误，并保持七周期铺垫的可发现性。

|质量目标|可观测量与单位|通过标准/反证|
|---|---|---|
|资格与可执行性|本机合法动作/选择、强制及最少显式药水，布尔|无资格路线不伪装成成功；队友历史不能未经授权满足本机要求。|
|真实终局不丢失|真实胜利/死亡标志、终局事实、实际结束回合|旧 CP 不能把终局变回未终局；加入无竞争力浅候选不改变两场真实终局的既定比较事实。|
|证据一致性|比较周期 d、已完成周期数、已知续行风险、未知尾部|同一批次从压缩到预览/最终选择只用一次上下文；不同批次的合法变化有原因。|
|生存与3HP目标|本机死亡，逐敌方周期实际无格挡扣血及超额，HP|先按已声明字典序；根前已付和自损计入，治疗不刷新，不用“七周期21HP总池”。|
|队友保护与资源|存活人数；本机/队友救命事件次数、恢复HP、显式/自动药水次数|归属可追踪，默认风险次序不借记账修正偷换；已模拟伤害不重复奖励。|
|短预算首条建议|首个合法前缀预览与首个完整物化结果各自时间，毫秒|分开记录，不把未完成验证预览算作完整安全路线。|
|长线覆盖|在同样七周期上限内，真实到达CP数、5–7周期铺垫的首动作质量|不能靠二步挑战或近端枚举吞掉远端预算而只改善短样例。|
|真实总成本|父展开数、引擎转移/选择模拟、重放、分配与峰值活状态、端到端墙钟|不增加配置上限，不漏记挑战、场景、旧前缀、结果重放或取消排空。|
|官方单人零变化|多人评分/保路/预算/字段消费者的单人进入次数；同输入动作和状态|进入次数为0；原调用顺序、参数与行为对照必须通过，不能只看最终同分。|

本轮没有真实失败战斗包，因此不能给胜率改善百分比。下面的“预期收益”是由明确反例推出的定性或局部保证，而不是已测线上效益。抽象实验、生产合同和游戏证据的层级见第 11 节。

## 2. 诊断摘要与明确不重做的部分

当前 R1/R2 已把完整最终池资格检查放在固定共同周期和截断之前，`MultiplayerFinalBatch` 贯穿同次预览、最终选择与结果比较深度。可继续展开的未用药前缀仍保路。这些修复应原样保留。[C04][C05][C47]

本轮优先缺口是：晚胜利在旧 CP 分支丢失终局事实；共享的本机零损满血胜利谓词还会停多人搜索；根前全队用药数量会提前放宽本机 Require；已完成池的局部压缩先于后来同一已知时点的联合比较；预算末父节点丢掉已模拟但未接收子状态。资源 owner 不全与共同周期之外动作数惩罚是独立残余。[C01][C07][C09][C11][C06][C15][C16][C03]

比较器在固定 d、不可变有限事实下可以表达为全预序；本轮55,296组三元标量检查没有发现传递性反例。问题不是又回到了两两临时选共同周期的旧错误。全部队友未来不主动行动也不是普遍最坏情况，但没有证据支持先给一套任意权重情景概率。[C02][C03][C31]

## 3. 竞争方案比较：为什么这轮不优先增加搜索模块

|方案|解决的实际缺口|成立前提与成本|主要反对证据|本轮取舍|
|---|---|---|---|---|
|现有 Beam 的正确性修正与有界保留对齐|F01–F04直接改变已有候选的正确解释/交付，不必增加战斗模拟|保持已有节点、引擎、Fork、批次和容量；增加少量过滤/事实读取|仍会丢掉分支上限之外的好路线；不是完全规划器|**优先主方案**。|
|当前回合强搜索＋短尾/估值|可能改善目标、顺序和近端救援|当前回合可穷举部分必须足够小；尾值须识别长期能力和敌方阶段|零CP的自信估值不能冒充受击验证；会牺牲5–7周期铺垫；没有校准尾值|不替换现役七周期引擎后缀。|
|有限两步/宏动作|能救出部分首步弱、第二步强的组合|候选仍可获取；第二步合法且选择有界；需把重放计费|A12同预算退化，A13三步才兑现；父内裁剪可能更早删路；F05末端无法交付已付候选|撤回优先推荐，只保留后续实验触发条件。|
|少量合法队友情景，选共享本机前缀|可以检验被动假设下建议是否脆弱|真实可得信息、合法队友动作、共用可执行本机前缀、场景成本与结果范围可解释|无计划/不保证配合；场景数极少时覆盖很差；最坏场景会过防，等权不是概率|先改善条件范围标注，不进入默认搜索。|
|Beam-Stack/有界回溯|理论上可以回访被 Beam 删除的路径|需要合适成本/界与反复展开；完备性依赖继续给工作|本项目非单调多目标及固定预算没有对应廉价界；旧路线重放已是真成本|不建立额外栈和持久 archive。|
|best-first Beam/当前回合 best-first|可能更早发现好叶子，避免部分广度同步损耗|优先级/上界需适配目标；平局与混合深度证据仍要先修|赢局奖励、铺垫、自损恢复和共同周期变化不满足论文的直接单调性条件|没有同预算数据不换调度器。|
|MCTS/UCT/PUCT、ISMCTS|适合从生成模型反复采样、处理部分不确定性|需可用的标量回报、rollout/先验、足够样本及信息集定义|资源/死亡字典序很难随意加权；稀有救命和延迟组合未必在短样本出现；真人行动不是已校准分布|不作为这轮低成本项。|
|学习模型/云端推理|可能提供尾值、动作先验|需代表性数据、离线评估、部署/漂移控制|当前连生产反例包都没有；不符合首版本地无训练首选|仅远期有条件研究，不列实施批次。|

### 3.1 原始资料能支持什么，不能支持什么

**Beam-Stack Search** 通过回溯恢复 Beam 的部分丢路，以更多工作逐步改善并在论文条件下获得完备/最优性质；不是说固定小预算下免费更强。[P01] **Best-First Beam Search** 的等价/早停分析依赖单调评分或有效未来上界，不能把其文本生成速度结果直接搬到战斗引擎。[P02] 本项目的两步和三步收益示例就是简单的非单调反例；F02更说明“我已经很满意”不等于对所有多人维度有界。

**UCT** 在生成模型和回报采样假设下以置信界安排探索；渐近性质不证明一次桌面短搜避免死亡或发现稀有组合。[P03] **ISMCTS** 对 determinization 的 strategy fusion 提出直接批评：不同隐状态各挑最优动作再聚合，不构成玩家在当前信息下可执行的同一策略。[P04] 本设计只借用这一非预知约束，不声称该游戏恰好满足论文中的对手/信息集模型。

以上是资料结论；“因此本轮保留 Beam”是结合本仓库成本边界作出的设计判断，不是论文替本项目选了赢家。原始资料链接和年份在文末，不引用二手算法百科。

## 4. 主方案与低成本回退

### 4.1 主方案：证据一致的保守多人 Beam

保留现有三通道保路和七周期引擎搜索，先修候选事实及错误提前停止，再让**同一已知发布时点**的局部压缩与最终比较采用一致投影。把本机根前药水资格输入改为本机 owner；根后本机/队友资源先补观察，不在本轮默认调换风险次序。预算末端的已付候选接收修正必须经过成本/所有权合同后才进入第三批。

这不是新 Planner。“发布时点/epoch”仅指当前循环中已经拥有的 `completed/ended/frontier/被启用的回退` 集合的一次一致读取，不是新的版本号系统、长期 archive 或跨请求缓存。它可以由现有 `MultiplayerFinalBatch` 的 Ordering 和少量局部变量表达，不必新增 registry、provider 或全局服务。[C04][C06]

**直接收益。** A02/A17的真实胜利不再被旧 CP 遮蔽；A03的局部停搜谓词不再错误声明多人目标已经达到不能改善的状态；A18的队友药水不再充当本机资格；A04中同一已知联合候选目标不再先被局部裁剪破坏。这些收益不依赖猜测队友以后会做什么，也不需要额外战斗模拟。

**尚未解决。** 主方案不保证 Beam 全局最优、不保证任意未来合池后仍保有所有冠军、不自动找到三步以上组合、不消除被动队友模型偏差、不把所有未知尾部升级为安全。F06 的“到CP操作数”只有在无需额外重放且能准确定位边界时才修；否则保留明确已知限制，不从总动作数猜一个 CP 内计数。

### 4.2 低成本回退：P1修复＋现役三通道

只保留终局事实优先、单人专属早停守卫和本机根前药水归属修正；继续当前 R1/R2 批次与三通道、不改变档案压缩时机、不增加资源事件字段或末端候选接收点。主方案第二/三批一旦引入预算或释放回归，退到此状态，而不是退回 F01–F03。

F03 的追踪起点和根前自动触发语义仍必须明确。首个实现宜保留原追踪起点及“根前本机已有事件”的既有豁免种类，只纠正 owner；若用户想改为“每次手动请求必须再显式喝一瓶”或“根前自动不算”，这是另一项政策，不混进回退。

## 5. 比较规格：事实边界、单位、玩家归属和优先级

### 5.1 三种不同对象

**可执行前缀 p** 是当前冻结根下本机马上能做的卡牌、目标、药水和本机选择序列；遇到队友外部选择、根已过期或支持边界即不再承诺后续可执行。第一动作必须来自合法展开，不从静态卡名猜。

**具体条件后缀 s** 是在未来队友不主动操作的条件下，引擎为该 p 找到的一条续行。它可以包含后续玩家回合和最多七个敌方周期；不是“所有相同首动作后缀都这样”。

**比较视图 v** 包含这条具体路线在共同 CP 或真实终局的事实，加上后来已知的反证。它不是模拟器，也不应成为战斗语义状态。浅前缀缺少未来风险是 unknown，不是 risk=0 的安全证明。

### 5.2 资格先于优化

对于**最终发布/已终止归档**：先检查强制指令，再检查有效最少显式药水/RequireAtLeastOne；显式数继续排除根后自动救命药水。本机禁用/保护的药水仍通过现有合法动作与指令机制处理，不新增旁路。无合格路线返回明确的 `PotionPolicyUnsatisfied`，预览可以暂时不发布，不能静默降为 Smart 或把 EndTurn 当成功。[C04][C20][C48]

对于**中间可展开前缀**：不要求未来最终资格现在就满足。未用药但以后有合法用药机会的路线继续按三通道保留。这是R1/R2修复的边界，不得在新“联合投影”函数里误删。[C19]

根前是否已满足本机 Require 是独立冻结输入；由本机历史 owner 和原追踪窗口计算，既不读 worker 的 live 日志，也不把队友事件减成一个假本机数。[C09][C11][C14]

### 5.3 默认最终比较维度

下面按当前风险政策列出顺序。除 F01 的终局事实修正外，不借研究更改资源/超额/Won 的优先级。

|顺序|维度及方向|单位/玩家归属|读取边界与备注|
|---:|---|---|---|
|0|资格合格|布尔，本机药水指令|完整发布池过滤；不把不合格者当低分备用。|
|1|本机实际死亡更差|布尔，本机|具体路线当前终点，不能藏回较早安全 CP。|
|2|有可比事实优于完全未知|布尔|真实终局或拥有 d 的完整 CP；这是现役策略，不是未知风险概率。|
|3|救命使用较少|事件次数，默认仍全队聚合|共同事实与本具体续行后来总数取较坏者；新增owner先供观测。|
|4|本机超额较少|HP，本机|逐敌方周期 `sum(max(0, loss_i-3))`，加当前未完成周期已付超额；不能跨周期抵扣。|
|5|真实胜利更好|布尔|采用真实终局，不受旧 CP 阻挡；不提前到死亡/资源/超额前。|
|6|队伍存活较多|人数，当前包含本机|共同边界与后来已见存活人数取较坏值；本机已在前面单独判死。|
|7|敌方有效HP较少|HP，所有敌人|非终局取CP；终局取终点。沿用复活/形态感知的 EffectiveEnemyHp，不改成裸HP。[C44]|
|8|本机累计扣血较少|HP，本机，根相对记录|用于目标范围内的同效伤害比较；根前本轮已付仍参与3HP账本。|
|9|本机当前HP较多|HP，本机|共同CP或终局；不能用治疗抵消已付扣血/超额。|
|10|药水次数较少|次，当前观察仍含聚合/自动口径|保留当前比较；根前资格与根后成本不是同一量。|
|11|双方真胜利时结束较早|玩家回合编号|沿用现有 CombatEndedTurn；不把它当敌方周期数。|
|12|操作成本/确定性|动作个数或规范化前缀身份|当前仍用总ActionCount。修正目标见下一节；未经精准边界验证不偷偷换定义。|

当双方都没有可比 CP/终局时，当前走 Score，再比较总 ActionCount；该 Score 是探索/未完成结果估值，不是完成受击的真实损失。源中多人 Score 以敌有效HP、持续增益、手牌可达值等组合，扣血超额另有多人账本修正；不要把通用单人估值前段和多人覆盖后的公式混用。[C27][C29][C40]

### 5.4 动作同分修正的精确约束

目标是：同一个共同 CP、相同已知风险，仅追加 CP 之外安全动作不应让路线因“多看了几步”而劣化。理想末项用 `localActionCountAtCheckpoint`，终局用终局动作数，未知用当前前缀数，再按可执行共同前缀的规范化动作身份稳定处理同分。

**不能靠父链随手取一个 ActionCount。** 一个 EndTurn 展开可能同时包含敌方阶段及下一玩家阶段选择，CP 在两者之间；若用该 SearchNode 的总数，仍可能把 CP 后选择计入。只有在当前已有动作/选择游标能精确标明 CP 时刻时，才在多人检查点加一个动作序号并在该时刻捕获；否则这一项先不变，作为已知限制。绝不为同分再跑一次引擎，也不从 TurnNumber 推算（额外回合不等于敌方周期）。[C30][C31][C59]

同一规范化共同前缀仍可以等价，无需人为惩罚它不同的未知尾部。固定批次要满足传递性，但不能把 `.NET List.Sort` 当稳定排序契约。[P05] 本轮 Python 的 `CP.action_count` 明确是提议字段，不是假装当前C#已有该元数据。

### 5.5 终局与未知的事实提取伪代码

```text
FactsAtMP(node, d):                         # 仅多人入口
    s = node.Snapshot
    if s.AllEnemiesDead or s.PlayerDead:   # 关键修正：先终局整体事实
        return Facts(
            Comparable = true, Won = s.AllEnemiesDead,
            Hp = s.PlayerHp, HpLost = s.CumulativePlayerHpLost,
            DeathSaves = s.DeathSaveUseCount,
            Excess = node.MpBudget.Excess(3),
            EnemyHp = s.EnemyHp, TeamAlive = s.TeamSurvivors,
            Potions = node.PotionCount, Kind = ActualTerminal)

    cp = FindExactCheckpoint(node, d)       # 不用较早 CP 冒充 d
    if d > 0 and cp exists:
        return raw facts of cp,
               Excess = sum(max(0, each_cycle_paid_loss - 3)),
               Comparable = true, Won = false, Kind = CompletedCycle

    return current raw facts,
           Comparable = false, Won = false,
           Kind = PreserveOriginalBoundary(node)  # Unknown/External/Unsupported等

KeyMP(node, frozenOrdering):
    a = FactsAtMP(node, frozenOrdering.d)
    knownSaves = max(a.DeathSaves, node.current.DeathSaveUseCount)
    knownExcess = max(a.Excess, node.MpBudget.Excess(3))
    knownTeam = min(a.TeamAlive, node.current.TeamSurvivors)
    # 资格已过滤；次序按上表。未知分支走现役Score，不附送“安全”标签。
    return lexicographic tuple with node.current.PlayerDead,
           a.Comparable, knownSaves, knownExcess, a.Won,
           (knownTeam, a.EnemyHp, a.HpLost, a.Hp, a.Potions, ...)
```

`Kind` 是伪代码说明字段，可由原 `BoundaryReason/AllEnemiesDead/PlayerDead/CP` 派生；首版不必为它新建公共枚举。当前代码事实结构若不需要存 Kind，就由发布摘要派生。真实终局仍保留原来的风险优先次序；不能只修改 `Won=true` 而保留旧敌HP和旧资源，这正是A17否定的补丁。

## 6. 候选管线与同一已知发布时点

### 6.1 两个池，两个职责，不增加新的永久池

**探索集合**允许仍未满足最终药水条件的可展开前缀，保留现有 MaxCardBranches、三通道与小 Beam 规则。**发布集合**只包含实际可用于预览/最后返回/终止归档的节点，按完整集合过资格，再冻结比较上下文。不能把两者合成一个“凡不合格都删除”的函数。[C19][C28]

原完成池的容量仍至多4×Beam，上一层回退仍有界；不增加“每个首动作一个席位”“每周期一个冠军”或额外挑战队列。多个深度信息不会免费变成多个 Beam。

### 6.2 批次构造与压缩伪代码

```text
EligibleForPublication(node, mpRequest):
    if not ForcedUsesSatisfied(node): return false
    if ExplicitFuturePotionUses(node) < mpRequest.minimum: return false
    if mpRequest.requireOne and ExplicitFuturePotionUses(node) == 0: return false
    return true

BeginKnownPublicationEpoch(completed, ended, frontier, fallbackWhenEnabled):
    # 只用此时真实持有且属于本次发布候选的集合；不读取未来live状态。
    # 可多遍枚举现有集合，避免另复制一棵树或额外全池状态。
    eligibleView = DistinctReferences(
        EnumerateExistingPublicationCandidates(...).filter(EligibleForPublication))
    ordering = CreateMultiplayerOrdering(eligibleView)  # 原规则，一次冻结d
    return ordering

CompressCompletedAtEpoch(completed, ordering):
    eligible = completed.filter(EligibleForPublication)
    # 这些节点不可再展开，所以可在归档压缩前严格过最终资格。
    sort eligible with KeyMP(node, ordering)
    keep at most existing 4 * BeamWidth
    release through existing phase ownership; no extra simulator owner
    return retained

PrepareFinalAtEpoch(existingPublicationPool, ordering):
    eligible = DistinctReferences(pool).filter(EligibleForPublication)
    if empty: return NoEligibleRoute
    sort with the SAME ordering
    take existing final capacity
    return MultiplayerFinalBatch(candidates, ordering)

SelectOrPreview(batch):
    select batch.Candidates[0]
    expose batch.Ordering.d separately from selected.actualCompletedCycles
    do not rebuild ordering from the truncated list
```

这不是把最终 Ordering 永久冻结在请求开始。**后续真的展开出新一层、新候选或新边界时可以建立新 epoch。** 要防的是：某一个已经确定的联合发布时点里，先在 completed 局部定 d 并删路，再在合池时改 d。A04的修复只保证这个局部一致性，不承诺对未来未知集合的集合无关性。

完成池压缩目前发生在下一 frontier 构造之前，接入时需要把本时点将用于发布的已有 ended/frontier 投影视图先确定，或把局部压缩延后到同一现有集合已确定时；这是生命周期小调整，不得改变单人循环。仍可展开的前缀即使没有最终资格，也不能从探索集合被删除；它们仅不参与这一次发布资格池。如果实现要求额外保留整层状态，超过原峰值对象预算，就撤回第二批而不是加内存。

### 6.3 总体搜索、预览、预算与排空伪代码

下列过程描述多人目标接入点，**不是已编译C#**；`Budget` 表示原请求共享的计数和时间约束，不表示新增服务或独立时钟。

```text
Solve(request):
    if request.policy.Multiplayer is null:
        return ORIGINAL_SOLO_ENTRY(request)       # 原实现、参数、路径原样保留

    assert request.wasManuallyRequested
    await existing prior worker/callback drain
    root = CaptureOnMainThreadWithStampBarrier(
        all current players, local identity, cycle HP already paid,
        local prior-potion count in the original tracking window)
    policy = BuildMpPolicyFromFrozenRootOnly(root)
    budget = existing request budget              # 不给辅助工作另开预算
    status = {firstLegalPreviewTime: none, firstMaterializedResultTime: none}

    try:
        frontier = existing root frontier
        completed = existing bounded completed storage
        oldCohort = existing bounded interrupted-layer metadata
        ReplayExistingSeedsIfAllowed(root, 4 routes, 32 actions each, budget)
        # 每一步合法重演、Fork、选择与统计均使用原budget，不能先免费种树。

        while frontier not empty and budget permits existing expansion:
            for parent in existing scheduling order:
                stop dispatching new work when cancelled or budget stops
                expand using the existing engine and legal choice machinery
                commit children in existing deterministic parent order
                keep unsupported/external-choice boundaries explicit
                retain expandable no-potion prefixes by existing three lanes
                if a terminal is found:
                    observe it; DO NOT apply the solo zero-local-loss stop

            epoch = BeginKnownPublicationEpoch(current existing collections)
            completed = CompressCompletedAtEpoch(completed, epoch)
            publishBatch = PrepareFinalAtEpoch(existing preview sources, epoch)
            if publishBatch has an actionable eligible local prefix:
                PublishPreviewWithScope(publishBatch, root.stamp)
                record firstLegalPreviewTime once
                # 这是引擎已生成的前缀预览，非保证完成七周期或最终重放。

            save/reuse only the already-existing bounded interruption cohort
            advance with existing seven-enemy-cycle limit and retention

        stop dispatching; join/observe outstanding worker results
        epoch = BeginKnownPublicationEpoch(actual final sources)
        batch = PrepareFinalAtEpoch(actual final sources, epoch)
        if no eligible batch: return explicit policy/boundary failure
        chosen = SelectOrPreview(batch)
        result = MaterializeByExistingEngine(chosen, root, SAME budget/accounting)
        # released fallback refresh 与注释/校验重放分别记账，不宣称合并成一次。
        if replay/validation is incomplete:
            do not label a preview as a fully validated final route
            return explicit incomplete result/boundary under current API contract
        hand result back to the MAIN-THREAD controller
        if the controller finds root stamp stale or request cancelled:
            suppress adoption/publication; do not auto-recalculate
        # worker不读取后续live状态；新鲜度检查仍由原主线程控制器完成。
        record firstMaterializedResultTime
        return manual advice with actual scope and known risks
    finally:
        stop dispatching, observe worker errors, drain existing callbacks
        release every owned simulator exactly once via existing ownership rules
        release root/request only after workers cannot access them
```

不能吞掉重放异常并返回看似成功，也不能把“无合格路线”降级为“建议随便结束回合”。当前 Materialize 有根上回放和一致性验证，不应为缩短统计时间跳过它。[C21][C22] 取消时排空可能晚于停止派发的时间；日志须分开，不把排空时间从用户等待中抹去。[C36][C37]

现役预算是有检查点的执行约束，不是操作系统硬实时中断：最终物化与排空可能越过最后一次搜索时间检查。本设计不声称已经实现严格截止于某毫秒。任何新增实验都必须在**包含这些尾部成本的相同端到端墙钟**下比较，且不得为补足结果追加第二段搜索预算；无法按现有结果契约在截止后完成验证时，必须清楚标注未完成，不能伪造“已验证”。首版不凭未经测量的百分比预留预算，也不新增一套定时器/超时框架。

### 6.4 末父节点的受控接收设计：第三批条件项

```text
OnMpCardChildrenAlreadyMaterialized(parent, immutableReadyChildren):
    # 此回调只能看到已经经过合法模拟且现役流程将要yield的子节点。
    # 不得调用 MoveNext() 去催生尚未开始的药水、EndTurn或额外选择。
    for child in ready children under the existing bounded admission cap:
        merge its lightweight route+facts into the EXISTING interruption quota
        do not allocate another beam, archive, or long-lived simulator owner
    keep original single-player iterator and stopping semantics untouched
```

仅暴露已物化批次还不够：被接受节点若随后在 active 清空时一并丢失，仍没修F05。实际测试要覆盖直到最终合池，并检验被合并的回退候选若已释放，重新物化成本是否使收益消失。无法在现有回退容量内替换时，应不接入这个项。这里没有隐式两步挑战，新增引擎动作数必须为0；排序、候选复制与最终重放的增量仍要计费。[C15][C16][C17][C18][C21]

## 7. 资源归属：最小字段、捕获、Fork、键与释放

### 7.1 根前与根后必须分账

根前已付药水影响“当前请求还必须显式喝吗”；根后预测消耗影响本条路线的成本和救命使用。根前本机扣血影响本敌方周期剩余额度，根后治疗不能抵消它。不要用一个 `PotionCount` 同时代表这四个不同概念。

|字段/观察|捕获或写入点|拥有者、Fork、释放|是否进入键|
|---|---|---|---|
|`PriorLocalPotionUseCount`（建议名）|主线程冻结 root 时，在与现有 Begin 相同的药水历史区间内按Actor过滤；stamp前后检查保持|不可变根/多人政策标量；worker只读；随请求释放；旧动作种子每次从新根验证|是请求多人政策输入，不是战斗语义；同请求常量无需每节点重复膨胀战斗键。|
|现有 `MultiplayerHpLossBudget`|根前本轮已付＋节点派生＋完整周期点|现有不可变节点字段/CP；Fork共享历史|继续现有多人政策标签，不改变单人。|
|本机/队友救命次数、恢复HP|Fairy/Tail真实事件处，使用已知owner；只在本地多人advisor开启时追加观察|可用一个不可变 `MultiplayerResourceTotals` 值，分本机/其他人；Fork共享值，写入时替换；不需要按职业建字典|只用于说明时不进战斗键；若以后参加比较/支配，必须进多人历史标签。|
|显式/自动药水owner统计|现有 ConsumePotion/RecordPotionUse 具有玩家/槽信息处；不猜日志缺失类型|同一MP观察值或已有记录的MP附属数据；保留原总量|同上；库存本来已进入真实战斗状态，不重复把估值入键。|
|共同周期动作序号（条件项）|精确CP完成瞬间，并区分该次EndTurn内前后选择|只在确认游标可提供准确序号时加到不可变MP CP；不为它重放|用于MP同分，属于政策历史；不进入公共战斗语义指纹。|
|批次 d/事实视图/边界说明|现有集合一次投影后派生|局部短命值/扩展现有FinalBatch；不持有新Simulator；由Phases原所有权释放|不进入状态键，不跨请求保存。|

**根前最小切口。** 现有 `BattleDamageTracker` 的 `_potionHistoryCountAtStart` 是“所有药水条目的起始计数”。可以给多人根捕获提供一个只读基线访问，再从同一历史的药水条目序列先跳过这个基线、后按本机 Actor 过滤；保留现有单人计数和 Begin/Observe 行为不变。访问需验证同一个 combat，不能遇到基线不明就默认0。这样比另建一个跨战斗记账器更小。具体是否存在历史清空/替换、Mod 中途接入语义，必须由生产 fixture 验证。[C10][C11][C14]

**根后最小切口。** 保留原 `RecordDeathSaveUse` 等全局统计；在真实事件已经知道 owner 的位置，附加多人观察，不改谁消费、谁治疗、事件顺序或RNG。公共镜像入口如需多传 owner，只能是观察参数变化，必须单列为公共语义接触面，不能与策略排序变更混成一个“纯小改”。[C24][C25][C26]

不要新增通用事件总线、资源插件注册器或完整玩家资产对象。当前问题只需本机/队友二分及事件类型，不需要为每个职业建立规则。确需逐玩家解释时用现有稳定 NetId/CombatId，不用职业名。[C33][C34]

### 7.2 默认风险语义与改变政策的独立选项

**默认保持。** 新增 owner 观察后仍按原“全队救命次数→本机超额→胜利→队伍存活…”比较。它不会自动改变A08的选择；它让用户能看到究竟是谁付出资源，并使后续政策可被准确实现。

**待用户决定的政策改变。** 可以研究把本机超额置于队友救命消耗之前，但不因此移动本机死亡；本机自己的 Fairy/Tail 是否仍优先于3HP目标又是第二个选择。队友必死与本机轻微超额的交换是第三个选择。三者不可用一个“团队安全权重”暗中合并。没有用户确认的顺序，不进入默认主方案，也不把A08重新标为已修行为。

根前自动用药是否解除 Require，以及同战斗已用与“每次请求必须用”区别同样单列。所有这些选择都应写成比较器条件表和极端fixture，而不是从胜率不明的样本拟合无量纲系数。

## 8. 集火/控制/救援与延迟收益：如何寻找收益而不堆权重

### 8.1 威胁反例先落到第一次裁剪

A10的两只6HP敌人，只有一个威胁4HP队友；两次目标选择在即时聚合指标上相同，完成周期后队友存活不同。现有Score尚未表达逐敌威胁，父节点动作/家族分支上限也可能在全局Beam之前删掉目标。[C27][C28]

验证时同时记录 `legal_generated → choice_expanded → per-parent-kept → beam-kept → cycle-completed → final-eligible → final-selected`。如果正确目标甚至没有生成，是语义/合法性问题；若在 per-parent-kept 消失，是保路问题；若已经完成受击但最终仍选错，才检查比较事实。不同根因不能统一加“集火+100分”。

控制通过少一次敌人行动、降低实际伤害或改变队友存活而获得价值；若这些结果已经模拟并计入HP/人数，再额外加一份“防止的伤害”会重复奖励。救援可达性应先使用已经生成的合法救援动作作为证据，不凭队友少血假定一定能救。敌方有效HP要继续沿用复活/形态语义，不能把召唤后HP增加当成负输出而另造奖励。[C44]

本轮不新增逐敌威胁评分。只有真实fixture反复证明正确目标在完整周期前被聚合估值同分淘汰，才允许另作“用一个现有代表席位保持不同目标/救援动作”的小实验，替换而非增加席位；它不属于本轮三批默认实现。

### 8.2 两步挑战的支持证据与否定证据

|实验|完整计费结果|能支持什么|不能支持什么|
|---|---|---|---|
|A11，首步弱第二步强|两侧均8玩具工作：6搜索边＋2最终重放；基线6，挑战20|存在值得多看一动作的结构|不能证明当前生产父内裁剪会把这个前缀交给挑战，也不能外推胜率。|
|A12，无价值挑战挤压长尾|两侧均6工作；基线三步价值10，挑战后两步价值2；均含完整重放|挑战可损失已见深层证据/路线质量|首动作相同，不是根动作退化的实测例。|
|A13，第三步才兑现|收益序列0、0、30，竞争方案5|两步探针可能稳定错过关键组合|不证明一定需要固定三步；可能更长，更不能预设宏动作库。|
|A09，末父接收边界|3个已模拟卡子仅接收1个|结果派发/接收/释放是挑战可交付的前提|不能靠drain迭代器免费执行剩余药水和EndTurn。|

第二步必须在被挑战节点的实际状态上由原合法展开生成。选牌分支不是传一个卡名就完成；8种选择就是8个合法分支（还可能伴随重放/派发成本），不是“一次探针”。遇外部队友选择、未支持、终局、根过期或预算耗尽立即保留相应边界，不把它算成第二步成功。[C16][C28][C31]

挑战候选若来自旧纯动作路线，应先按新根重新验证最多4×32的现役限制；不能复用旧分数、旧Simulator或旧选择结果。最终所选路径被释放时还要走 RefreshReleasedFallback，后续注释重放也可能再执行一次。A11/A12已把玩具完整最终重放计入，但没有冒充生产所有这些成本均已测过。[C20][C21][C22]

**重新开启研究的门槛。** 先有一个E2/E3可达的延迟组合丢路fixture，且丢失前缀能够在不额外扩容的既有候选边界被获取；加入所有选择/旧前缀/最终校验成本后，在固定工作和固定墙钟两种对照中都优于修正后的Beam，并通过5–7周期哨兵。达不到就停止，不靠加宽Beam或多给一秒挽救论文式正例。

## 9. 真人未来行动的不确定性：先说明条件，再决定是否值得情景

### 9.1 默认保留的条件模拟

本机立即动作来自冻结事实；未来队友主动动作未知，但队友被动、正常阶段和受击不应被删去。本机即使是客户端，也不能把 worker 搜索中的 live 队友新状态掺进旧root。队友行动导致根失效时，沿用现有过期标识和手动重算，不自动接管。[C31][C33][C34][C35][C36]

建议摘要至少区分：“本次根下可执行的当前前缀”“条件后缀到第几个实际完成CP”“后来已知的超额/救命/队友死亡”“在何处外部选择或未支持”。这是范围说明，不是另一个评估器。不要标未经校准的“95%安全”。

### 9.2 少量情景的合法比较方式与反例 A15

若未来有明确证据值得测试，只能使用冻结根下合法、在信息政策允许范围内的队友动作，例如确认队友当前可执行且不依赖隐藏牌序的一段动作；不能凭职业模板假装队友手里一定有牌。行动交错、目标失效和RNG消耗需在每个情景由同一个引擎真实推进，不可把相同seed当作相同随机结果保证。

所有情景必须共用**同一个本机立即前缀**，之后才允许依据真实到达的信息边界重新规划。数学上应比较同一个 p 的情景结果向量 `(V(p,s1), V(p,s2),...)`；不能先算每个情景自己最好的 `p_s` 再平均。

A15 给出X=(10,0)、Y=(0,10)、Z=(6,6)。分别在两个世界挑X/Y可平均得10，但没有一个共同立即动作实现该值；合法共享Z平均6，最小值也6。这里的等权只是算术演示，不是“队友一半概率如此”。它与信息集搜索论文所讨论的 strategy fusion 风险直接相关。[P04]

minimax/min-regret等稳健准则同样是政策：极少数手造情景的最坏值可能强迫过度防守，平均则可能掩盖致死。没有概率校准时，优先显示条件结果范围/敏感性，而不是把权重包装成胜率。每增加一情景都需要新的Fork、合法性、阶段推进和可能的重新规划工作；固定预算会挤掉原七周期搜索。因此**主方案情景数仍为现役一个被动条件，不新增场景模块**。

## 10. 工程切口与生命周期：哪些必须小，哪些暂不碰

|批内切口|最少相关文件|必要性与约束|
|---|---|---|
|终局事实|`CombatBeamSolver.MultiplayerEvaluation.cs`；对应合同|终局分支前移，当前终局事实整体使用；不改共享快照判胜和本机风险优先级。|
|多人早停|`CombatBeamSolver.Phases.cs`；对应合同/两端门禁|只给现有共享零损胜利条件加单人守卫。官方单人语句、参数、其他早停保持。|
|根前本机药水|`CombatRootSnapshot.cs`、`CombatBeamSolver.cs`，必要只读基线在`BattleDamageTracker.cs`；合同|捕获在主线程，构造器显式MP选择local count，单人仍原battleDamage；不另造跟踪器。|
|同一发布时点|`CombatBeamSolver.Multiplayer.cs`、`.Phases.cs`、`MultiplayerFinalSelectionContracts.cs`|复用FinalBatch与已有集合，不新增长寿命archive；保持4×Beam和回退上限。|
|动作边界同分（条件项）|`.MultiplayerEvaluation.cs`、`MultiplayerCycleCheckpoint.cs`及现有CP调用点|仅当精确游标无额外重放可得；否则不进入本批。|
|资源owner观察|`SimulatedCombatState.Multiplayer.cs`/`.LongTermResources.cs`/`.Potions.cs`、现有死亡镜像、CP写入与合同|一个MP不可变统计值足够；原结算与全局统计不变；本轮不变风险顺序。|
|末端已付子接收（条件项）|`.Expansion.cs`、`.Phases.cs`、已有回退/合同|只在MP开启观察已经物化子列表；不得启动后续枚举或加一份持久状态。|

“最少相关文件”不是本轮已修改清单，也不是允许一起大改的白名单。不需要独立Planner、provider/registry、宏动作库、全局多策略版本开关、大面板或学习数据管道。新的命名字段可以放现有MP分片；只有资源owner统计确有一个小值类型的必要，且若第三批不做观察就不新增。

所有候选Simulator仍由原Phases/快照生命周期持有；批次Ordering只是读节点事实，不能取得第二份所有权。旧回退节点允许只剩父链/快照标量，选择它时才按原路径重新物化，成本完整计算。最终 `SelectedSearchPlan` 和摘要保持轻量，不保存SearchNode或Simulator。[C21][C22][C40][C42]

战斗语义键继续描述真实引擎状态；3HP、CP历史、将来用于策略决策的owner历史属于多人政策标签。不能把日志、场景注释、规范化显示文本、评分或批次编号塞进战斗键。跨请求只保留当前已经允许的纯动作种子，根变动后一律重新合法验证。[C20][C38][C43]

## 11. 验证矩阵：共享根因对应最小fixture

### 11.1 本轮证据分层

E0：读到了相关固定源码和数据写入链。E1：实际执行随附Python。E2：生产C#调用链/合同。E3：原生实际/模拟差分。E4：两端真实联机。本轮完成E0相关定点审查和E1共18项；**E2/E3/E4均未重跑**。环境没有dotnet或游戏DLL，随附历史证据不改名成本轮结果。[C48][C49][C50][C51][C52][C58]

### 11.2 关键fixture与第一失败边界

|编号/根因|最小状态或变换|本轮结果|下一层验收与停止条件|
|---|---|---|---|
|T01 R1/R2防倒退|唯一显式用药合格者；增加不合格者；中间前缀尚未用药|A01，840排列/216三元通过|生产Prepare/Select仍同批；无合格明确失败；不得重现先裁后资格。|
|T02 终局遮蔽|CP1敌40后获胜，对CP1敌39未胜|A02当前选未胜，终局优先纠正|通过真实生产Facts/比较；确认真实晚胜可达。|
|T03 仅改Won不足|两场不同时间晚胜＋加入浅非终局|A17 flag-only仍错|使用整个终局事实，不能仅置位。|
|T04 多人早停|本机满血零损赢但队友死/消耗Fairy，后面有更优完成路线|A03停搜谓词未界住后者|调用Phases检查第二路线能继续发现；原生终局队友状态语义另核。|
|T05 根前owner|同追踪窗口队友已用1、本机0；RequireAtLeastOne|A18当前降Smart|准备请求→构造solver→最终资格；不要只测过滤函数。|
|T06 跨批压缩|五条CP2外部边界路线，容量4；合入现存CP1|A04局部压缩选B，完整联合选A|经过真实压缩/合池；新epoch允许改变d，已知同epoch不随意改变。|
|T07 固定顺序/尾动作|24标量节点×d0/1/2/7；只加无风险CP外动作|A06传递通过；A05总动作尾惩罚复现|.NET实际入口做关系/排列；精确CP操作边界不可得时不伪修。|
|T08 坏续行/好首动作|P浅、P_bad、P_good与Q|A07三者关系可复现|不删除later-risk；不把P_bad传播成首动作黑名单；未知不标安全。|
|T09 根后资源|本机3＋队友Fairy，对本机6＋无资源|A08默认选后者|先验证owner统计与原总量一致；改顺序需独立批准。|
|T10 最后父节点|父已有三张卡子列表，仅余1父名额|A09接收1、释放2|实际iterator/active清空/最终物化贯通；未新增药水或EndTurn模拟。|
|T11 非对称威胁|6伤选杀攻击队友的6HP敌/不攻击的6HP敌|A10即时聚合相同，周期后人数不同|真实卡/敌fixture；定位首次裁剪，不能只看最后是否赢。|
|T12 二步收益正例|弱首步B经第二步20，对A最多6|A11含重放同8工作|生产可获取候选＋合法选择＋派发/物化；没有这些不得默认挑战。|
|T13 二步退化哨兵|无收益挑战占两边，基线能见第三步10|A12同6工作，10→2；首动作相同|不要声称已测首动作退化；在生产固定预算对照看远端证据损失。|
|T14 三步以上铺垫|0、0、30，对即时5；另扩展到5–7CP才兑现|A13只做抽象前半|生产加入第5/6/7CP兑现根动作；不能牺牲该哨兵换近端样例。|
|T15 3HP分账|3+3、0+6、根2＋自损2＋治10、敌阶段3/下次开局2|A14通过标量预期|现有native合同与两周期生命周期重跑；额外玩家回合不重置。|
|T16 终止/外部/未知|零CP＋CP1；全受阻CP0＋CP2；全真实终局|A16；全终局上下文7但实际长度可1|UI分别表达d/实完成/终局；不把外部选择当胜利。|
|T17 情景非预知|X=(10,0)、Y=(0,10)、Z=(6,6)|A15共享6，错误全知10|真做场景时先锁共享本机前缀，不能每场景选不同立即动作。|
|T18 边界/生命周期/隔离|同根Fork兄弟、重复职业/本机位置、额外回合、手动取消/过期|源码有相关边界；本轮没有实测|官方单人零进入；两周期owner不串写；取消后排空；2/3/4人少量根因驱动组合，不机械穷举卡名。|

T14后半是新设计的不可退化哨兵，**不是本轮已运行的七周期游戏实验**。同样T18没有伪造测试次数；已有两人host/client离线入口还替换了Godot/网络，不能替代双端联机。[C51][C52]

### 11.3 公平对照与完整成本台账

**固定模拟工作量对照。** 相同root、RNG状态/信息政策、合法动作输入、DOP、Beam、MaxCardBranches、最大父展开数及H=7；除父展开数还记录引擎状态转移、选择枚举、Fork、丢弃子节点、完整周期数、旧种子回放、RefreshReleasedFallback、注释/最终重放。一个父节点的成本可以远大于另一个，不能只报告ExpandedNodes相等就说成本相等。[C15][C16][C20][C21][C22]

**固定端到端墙钟对照。** 使用部署原预算档位，不增加上限；从同一个请求入口开始，到结果物化并完成必要排空为止，区分根捕获、搜索、重放和排空耗时。预热与冷启动分开，相同机器/构建/线程数，交替A/B顺序并固定输入。可补充1/3/10秒研究档位，但只有用户现有配置允许时才采用；这些不是已测最佳参数，也不代替原预算对照。

两种对照都记录首个合法前缀预览时间、首个完整物化结果时间、最终首动作、比较周期与实际完成周期、每周期扣血向量、终局/资源owner/队友存活、未支持比例、失败/取消、峰值活Simulator和分配量。不同队友情景/挑战不能另列“辅助预算”；它们的成本应占同一总额。

**消融顺序。** 修正前0.41.2；仅F01；加F02；加F03；再加同epoch压缩。第三批owner观察与已付子接收分别测试，不把多个改动混成一次收益。两步挑战只能对“上述修正后的基线”比较，不能以已知有F01错误的旧基线制造虚假提升。

**推进/停止阈值。** 正确性fixture必须全部满足，任一官方单人差分、无资格路线被发布、未知冒充已验证、use-after-release/双释放、worker读取live状态均立即停止。质量提升至少要在对应反例上可解释，并且不能使预先固定的生存/资格/5–7CP哨兵退化。性能阈值应在E2/E3基线测量后确定；可把“p95首条完整结果延迟恶化超过10%”作为**待校准实验停止线**，不是本轮测得结论。这条建议线主要用于第二/三批相对第一批正确性基线的新增成本；F02可能使原预算用得更满，延迟取舍单列，不以恢复错误早停来满足指标。即使未超过10%，只要越过原总预算或多保留状态，也不得通过。

没有一组小fixture能证明任意战斗从不退化。通过的意义是：共享根因的局部正确性、单人隔离、成本与生命周期可接受，才足以扩大受控回放范围；不是自动获得发布或普遍胜率保证。

## 12. 最多三批的实施次序

|批次|目的与最小范围|必须验收|回退与不做项|
|---|---|---|---|
|第一批：事实与入口|F01终局整体事实优先；F02共享零损早停单人守卫；F03同追踪窗口本机药水输入。更新对应实际C#合同及两端门禁。|T01–T05；官方单人零进入/原80项对照重新运行并标新结果；没有DLL时不得声称本批已验证。|本轮低成本回退就是这组经验证修复；窗口/自动语义未确定的部分保留明确待决，不自动改政策。无新Beam、owner事件框架、挑战、情景。|
|第二批：同证据面发布|F04同一已知发布时点的上下文贯穿局部压缩、预览和最终选择；说明d/实际CP/未知尾部。F06仅在精确CP动作游标低成本可得时顺带修，不可得就保留。|T06–T08、T16；固定工作/墙钟、峰值状态上限；同批/跨批变换分开。|发生所有权/内存/预算回归则退第一批。不建立跨请求archive、首动作汇总评分器或每周期多个冠军。|
|第三批：归属观察与已付工作回收，均受门控|先补MP本机/队友救命与药水owner观察，默认风险次序不变；F05仅在已物化卡批可零新增引擎动作、现有容量内接入时实现。二者分别消融。|T09/T10/T15/T18，Fork兄弟/两周期；公共镜像结算差分；重放/排空成本完整；5–7CP哨兵。|难以小改或没有同预算实益就停在第二批；不为了“做满三批”硬加模块。不实施两步挑战/稳健情景/新风险排序。|

每批是后续实现建议，不是本次实施授权。本轮未运行任何仓库构建或发布命令。用户未批准的资源顺序、根前自动用药语义、每次请求是否必须再喝、隐藏信息政策继续留在产品决策表，不由Codex自行选择。

### 12.1 可以交给实现者的范围说明

```text
固定 2dc5d15b26f16d89436af0fb98650d8b4cf6b411；先写能失败的生产入口合同。
只做第一批：MultiplayerFactsAt 先返回真实终局整体事实；
Phases 原零本机战损满血胜利早停保持单人原样，多人不进入；
本机 Require 的根前历史按原追踪窗口的本机Actor冻结，单人计数不变。
保留 R1/R2、可展开未用药前缀、3HP逐敌周期、H=7、原所有预算。
不以本报告中的Python PASS作为C#通过，不把历史证据贴成本次证据。
完成后用同root合同验证终局/资格与单人零进入，再考虑第二批。
没有部署/发布授权；不要顺便重构大文件或引入新Planner。
```

## 13. 对上轮结论的修正，以及仍需用户决定的取舍

这轮对“两步挑战”的反对不只是缺乏实测，而是有明确负例与接入成本：父内裁剪可能先删掉来源、卡内选择使第二步并非一个模拟、末父名额已算未收、旧前缀和最终结果仍需真实重放、第三步/第5–7周期才兑现的收益可能被挤掉。A11说明它有适用结构；A12/A13说明它不具有默认优势。因此下一步应先让现有候选被正确解释和交付。

需要产品决定的是：本机超额与队友救命资源的次序、本机救命资源与3HP目标的次序、队友必死与本机轻微超额的交换、根前自动/既付用药豁免、隐藏牌序/RNG公平信息政策。主方案没有替用户批准这些选择。目标仍是“优先避免死亡，在每敌方周期3HP目标范围内争取有价值输出”，不是鼓励掉血或自动接管队友。

在上述正确性缺口修复且同预算哨兵通过之前，最可辩护的策略是保持现有引擎与Beam简单；在真正失败包指出覆盖瓶颈之后，才选择能被同一实验反驳的新搜索覆盖手段。这一结论可以被后续E2/E3证据推翻，而不是为了维护上一轮建议而坚持它。

## 附录 A：本轮实际运行的脚本与输出

完整脚本独立随附：[CombatSolver_0.41.2_abstract_checks.py](CombatSolver_0.41.2_abstract_checks.py)，标准库Python3.10+。结果：[JSON](CombatSolver_0.41.2_abstract_results.json)；完整控制台：[TXT](CombatSolver_0.41.2_abstract_output.txt)。代码、源码读范围和完整性校验也在证据ZIP中；没有游戏DLL或字体等额外依赖。

```bash
python CombatSolver_0.41.2_abstract_checks.py --out ./abstract-results
python CombatSolver_0.41.2_abstract_checks.py \
  --out ./abstract-results --source ./CombatSolver-0.41.2
```

本轮实际运行环境Python3.13.5；18项抽象检查全部通过其预设断言，六个可选源码子串检查匹配当前附件。这不代表18项“生产修复通过”：多数断言专门确认当前反例存在。脚本的 `fix_win` 和 `scoped_tie` 是反事实设计对照，玩具树不是引擎，A12的根动作没有变化。规范脚本、真实输出与全部限制一起交付，便于在有DLL后把失败点移入生产入口。

```text
Baseline: 2dc5d15b26f16d89436af0fb98650d8b4cf6b411
Evidence: ABSTRACT_PYTHON_ONLY
Python: 3.13.5; dotnet found: False
PASS A01_R1_R2_fixed_pool: {"only_explicit_eligible": true, "permutations": 840, "triples": 216}
PASS A02_victory_checkpoint_shadow: {"actual_completed_cycles_of_W": 1, "all_terminal_context": 7, "current_winner": "N_nonterminal", "depth": 1, "fixed_winner": "W_later_victory", "immediate_win_ok": true}
PASS A03_premature_multiplayer_stop: {"abstract_better_suffix_not_ruled_out": "win_peer_alive", "stop_with_peer_dead": true, "stop_with_peer_fairy": true}
PASS A04_cross_batch_loss: {"archive_after_cut": ["E", "D", "C", "B"], "compressed_winner": "B", "full_pool_winner": "A", "new_depth": 1, "old_depth": 2, "same_epoch_repair": "A"}
PASS A05_tail_length_and_ties: {"after": "B", "before": "A", "distinct_routes_may_compare_equal": true, "scoped_tie_winner": "A"}
PASS A06_fixed_context_preorder: {"antisymmetry_pairs": 2304, "depths": [0, 1, 2, 7], "nodes": 24, "triples": 55296}
PASS A07_unknown_vs_bad_suffix: {"ancestor_not_proof_of_future_safety": true, "bad_suffix_loses_to_Q": true, "same_first_action_good_suffix_beats_Q": true, "unknown_beats_Q": true}
PASS A08_resource_ownership_policy: {"current_winner": "local6_peerNoSave", "loser": "local3_peerFairy", "owner_observation_alone_changes_policy": false}
PASS A09_last_parent_admission: {"accepted": 1, "already_simulated": 3, "discarded_paid_children": 2, "events": ["simulate:first", "simulate:second_better", "simulate:third_best", "release:second_better", "release:third_best"], "potion_endturn_not_started": true}
PASS A10_asymmetric_threat: {"after_cycle": {"kill_dangerous_enemy_survivors": 2, "kill_safe_enemy_survivors": 1}, "aggregate_prefix_tie": true, "specific_game_reachability": "NOT_TESTED"}
PASS A11_two_step_can_help: {"baseline_trace": ["search:root->A", "search:root->B", "search:a->a1", "search:a->a2", "search:a->a3", "search:a->a4", "replay:root->A", "replay:a->a1"], "baseline_value": 6, "both_final_replay_work": 2, "both_total_toy_work": 8, "challenge_trace": ["search:root->A", "search:root->B", "search:a->a1", "search:b->b1", "search:b->b2", "search:a->a2", "replay:root->B", "replay:b->b1"], "challenge_value": 20}
PASS A12_two_step_can_hurt: {"baseline_trace": ["search:root->a", "search:a->b", "search:ab->c", "replay:root->a", "replay:a->b", "replay:ab->c"], "baseline_value": 10, "challenge_trace": ["search:root->x", "search:x->y", "search:root->a", "search:a->b", "replay:root->a", "replay:a->b"], "challenge_value": 2, "same_first_action_in_this_fixture": true, "same_total_work": 6}
PASS A13_two_step_scope: {"a_second_action_is_not_one_simulation": true, "delayed_payoff": [0, 0, 30], "example_second_action_choice_fanout": 8, "three_step_best": 30, "two_step_best": 5}
PASS A14_hp_budget: {"end3_next_start2_separate_excess": 0, "root2_self2_heal10_excess": 1, "three_plus_three_excess": 0, "zero_plus_six_excess": 3}
PASS A15_shared_prefix_scenarios: {"best_shared_prefix": "Z", "invalid_clairvoyant_average": 10, "shared_toy_average": 6, "weights_are_not_probabilities": true}
PASS A16_boundary_semantics: {"external_zero_and_two_context": 2, "mixed_zero_and_one_context": 1, "unknown_does_not_become_verified": true}
PASS A17_won_flag_only_insufficient: {"all_terminal_winner": "WA_earlier_win", "flag_only_winner": "WB_later_win", "mixed_pool_winner": "WB_later_win", "terminal_facts_first_winner": "WA_earlier_win"}
PASS A18_root_peer_potion_disables_requirement: {"R1_filter_function_itself_is_not_broken": true, "current_effective_policy": "Smart", "current_winner": "local_no_potion", "global_prior_uses": 1, "local_prior_uses": 0, "local_scoped_winner": "local_use_potion"}
PASS total=18
NOT RUN: production C#; native game differential; real multiplayer
```

## 附录 B：来源与审计边界

附件为主，固定GitHub原始文件只作辅助交叉核对。完整阅读本轮需求，源码使用独立目录 `review_0412_2dc5d15/CombatSolver-0.41.2`；2017个文件的原始与交付前哈希校验见 `CombatSolver_0.41.2_integrity.json`。读过/未读清单详列于配套复审结论第8.2节，本文件不把大型共享文件的定点审查称为全部2017文件逐行审核。

本轮实际审查入口包括当前多人指南/架构/测试矩阵、多人搜索及评分/周期/预算分片、最终合池/保路/展开/终止/转置、资源事件写入、主线程冻结/过期/排空、最终选择和评价合同、其他多人合同的相关区间、当前结构化证据与两端门禁。没有游戏程序集、原生失败包或联机记录，不声称E2–E4已完成。[C55][C56][C57][C58]


## 来源索引与固定行号

所有 C 编号以本轮 ZIP 解压源码的行号为准；链接均锁定 `2dc5d15b26f16d89436af0fb98650d8b4cf6b411`，不指向默认分支。链接用于定位，主要读取渠道是附件。

|编号|文件、方法/用途、行号|
|---|---|
|[C01]|`src/Search/CombatBeamSolver.MultiplayerEvaluation.cs`，MultiplayerFactsAt：检查点与终局分支，49–68 行。|
|[C02]|`src/Search/CombatBeamSolver.MultiplayerEvaluation.cs`，CreateMultiplayerOrdering：批次共同周期，20–43 行。|
|[C03]|`src/Search/CombatBeamSolver.MultiplayerEvaluation.cs`，CompareMultiplayerAtCycle：完整比较顺序，71–117 行。|
|[C04]|`src/Search/CombatBeamSolver.Multiplayer.cs`，MultiplayerFinalBatch、Prepare/Select 最终候选，16–45 行。|
|[C05]|`src/Search/CombatBeamSolver.Phases.cs`，预算回退合池、一次 Prepare、同批 Select，2071–2104 行。|
|[C06]|`src/Search/CombatBeamSolver.Phases.cs`，已完成池独立压缩与后续 frontier 合并，1905–1947 行。|
|[C07]|`src/Search/CombatBeamSolver.Phases.cs`，共享的零本机战损满血胜利提前停止，1995–2009 行。|
|[C09]|`src/Search/CombatBeamSolver.cs`，有效药水政策的构造期降级，83–93 行。|
|[C10]|`src/Runtime/BattleDamageTracker.cs`，Begin/Observe 与多人返回，24–49 行。|
|[C11]|`src/Runtime/BattleDamageTracker.cs`，全队 PotionUsedEntry 计数，95–102 行。|
|[C14]|`src/Runtime/CombatReplayOutcome.cs`，PotionUsedEntry.Actor 的本机归属先例，40–83 行。|
|[C15]|`src/Search/CombatBeamSolver.Phases.cs`，ExpandNextSerially 与最后父节点串并行边界，1648–1716 行。|
|[C16]|`src/Search/CombatBeamSolver.Expansion.cs`，卡候选裁剪、yield/finally、后续药水展开，688–768 行。|
|[C17]|`src/Search/CombatBeamSolver.Phases.cs`，中间保留后节点预算耗尽清空 active，1867–1903 行。|
|[C18]|`src/Search/CombatBeamSolver.Phases.cs`，保存上一层多人有界候选，1380–1404 行。|
|[C19]|`src/Search/CombatBeamSolver.Multiplayer.cs`，中间三通道与小 Beam 规则，50–103 行。|
|[C20]|`src/Search/CombatBeamSolver.Multiplayer.cs`，旧路线种子：四条、32 动作、重新验证，105–161 行。|
|[C21]|`src/Search/CombatBeamSolver.Retention.cs`，RefreshReleasedFallback：根上重放，82–112 行。|
|[C22]|`src/Search/CombatBeamSolver.Phases.cs`，MaterializeResult：回退刷新、注释重放与最终校验，430–555 行。|
|[C24]|`src/Search/SimulatedCombatState.LongTermResources.cs`，无玩家参数的救命资源统计，1–75 行。|
|[C25]|`src/Search/SimulatedCombatState.Potions.cs`，药水所有权库存与 PredictedPotionUse 记录，1–139 行。|
|[C26]|`src/Engine/InCombat/Mirrors/Hooks/Death/DeathPreventerMirrors.cs`，Fairy/Tail：真实所有者结算与全局记账，1–80 行。|
|[C27]|`src/Search/CombatBeamSolver.StateEvaluation.cs`，多人中间 Score，518–531 行。|
|[C28]|`src/Search/CombatBeamSolver.Expansion.cs`，SelectActionCandidates：父节点内动作/家族保留，3529–3668 行。|
|[C29]|`src/Search/MultiplayerSearchPolicy.cs`，MultiplayerHpLossBudget：根前、当前、已完成周期，29–74 行。|
|[C30]|`src/Search/CombatBeamSolver.Expansion.cs`，受击、周期检查点、下一玩家阶段与胜利，3374–3465 行。|
|[C31]|`src/Search/CombatBeamSolver.MultiplayerRound.cs`，多人正常阶段、额外回合、队友选择边界，1–156 行。|
|[C33]|`src/Runtime/CombatRootSnapshot.cs`，主线程冻结、本机定位、本轮扣血、前后 stamp，137–275 行。|
|[C34]|`src/Runtime/ContinuationStamp.Multiplayer.cs`，全队状态与身份的新鲜度签名，1–70 行。|
|[C35]|`src/Runtime/SolverController.Multiplayer.cs`，手动建议完成、过期观察、纯路线缓存，1–44 行。|
|[C36]|`src/Runtime/SolverController.cs`，多人手动入口、取消与前次 worker 排空，846–889 行。|
|[C37]|`src/Runtime/SolverController.cs`，结果/worker/回调完成后释放请求，2060–2133 行。|
|[C38]|`src/Search/CombatBeamSolver.Transpositions.cs`，多人政策历史进入转置标签，1–62 行。|
|[C40]|`src/Search/CombatPlan.cs`，SearchNode：父链与多人扣血账本，1060–1124 行。|
|[C42]|`src/Search/CombatPlan.cs`，释放 Simulator 与轻量 SelectedSearchPlan，1340–1400 行。|
|[C43]|`src/Search/SimulatedCombatState.cs`，Fork 复制/共享现有不可变观察，420–449 行。|
|[C44]|`src/Search/SimulatedCombatState.cs`，EffectiveEnemyHp：复活/形态口径，1429–1468 行。|
|[C47]|`src/Search/CombatBeamSolver.Phases.cs`，PublishRoutePreview：同批 Prepare/Select，868–939 行。|
|[C48]|`tools/OfflineSearchHarness/MultiplayerFinalSelectionContracts.cs`，当前 R1/R2 生产入口合同源码，1–235 行。|
|[C49]|`tools/OfflineSearchHarness/MultiplayerEvaluationContracts.cs`，周期、铺垫、救援、坏续行与立即胜利合同，1–277 行。|
|[C50]|`tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs`，策略合同入口、扣血/重算 native fixture，23–130 行。|
|[C51]|`tools/OfflineSearchHarness/MultiplayerContracts.cs`，七周期、纯路线重放与零完整周期合同，1–128 行。|
|[C52]|`tools/OfflineSearchHarness/MultiplayerStartContracts.cs`，离线 host/client 启动 fixture 的替身范围，1–60 行。|
|[C55]|`docs/multiplayer-advisor.md`，当前多人指南：语义与局限，1–99 行。|
|[C56]|`docs/ARCHITECTURE.md`，当前架构、多人边界、预算/排空，1–80 行。|
|[C57]|`docs/TEST_MATRIX.md`，当前测试矩阵的多人章节，1–77 行。|
|[C58]|`coverage/test-evidence.json`，随附历史结构化证据，非本轮重跑，1–65 行。|
|[C59]|`src/Search/MultiplayerCycleCheckpoint.cs`，现有不可变周期观察字段，1–6 行。|

### 原始资料

检索日期：2026-09-18。论文正文及相关页面截图已查阅；以下用于算法条件对照，不替代本项目同预算实验。

**[P01]** Zhou 与 Hansen，Beam-Stack Search: Integrating Backtracking with Beam Search，ICAPS 2005。

**[P02]** Meister、Vieira 与 Cotterell，Best-First Beam Search，TACL 2020，795–809；正文 §3–4。

**[P03]** Kocsis 与 Szepesvári，Bandit Based Monte-Carlo Planning，ECML 2006。

**[P04]** Cowling、Powley 与 Whitehouse，Information Set Monte Carlo Tree Search，IEEE TCIAIG 4(2)，2012；§III-B。

**[P05]** Microsoft，List<T>.Sort 官方文档，Remarks（不稳定排序）；检索于 2026-09-18。


[C01]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.MultiplayerEvaluation.cs#L49-L68
[C02]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.MultiplayerEvaluation.cs#L20-L43
[C03]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.MultiplayerEvaluation.cs#L71-L117
[C04]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Multiplayer.cs#L16-L45
[C05]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L2071-L2104
[C06]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L1905-L1947
[C07]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L1995-L2009
[C09]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.cs#L83-L93
[C10]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/BattleDamageTracker.cs#L24-L49
[C11]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/BattleDamageTracker.cs#L95-L102
[C14]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/CombatReplayOutcome.cs#L40-L83
[C15]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L1648-L1716
[C16]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Expansion.cs#L688-L768
[C17]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L1867-L1903
[C18]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L1380-L1404
[C19]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Multiplayer.cs#L50-L103
[C20]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Multiplayer.cs#L105-L161
[C21]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Retention.cs#L82-L112
[C22]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L430-L555
[C24]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/SimulatedCombatState.LongTermResources.cs#L1-L75
[C25]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/SimulatedCombatState.Potions.cs#L1-L139
[C26]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Engine/InCombat/Mirrors/Hooks/Death/DeathPreventerMirrors.cs#L1-L80
[C27]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.StateEvaluation.cs#L518-L531
[C28]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Expansion.cs#L3529-L3668
[C29]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/MultiplayerSearchPolicy.cs#L29-L74
[C30]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Expansion.cs#L3374-L3465
[C31]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.MultiplayerRound.cs#L1-L156
[C33]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/CombatRootSnapshot.cs#L137-L275
[C34]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/ContinuationStamp.Multiplayer.cs#L1-L70
[C35]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/SolverController.Multiplayer.cs#L1-L44
[C36]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/SolverController.cs#L846-L889
[C37]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/SolverController.cs#L2060-L2133
[C38]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Transpositions.cs#L1-L62
[C40]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatPlan.cs#L1060-L1124
[C42]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatPlan.cs#L1340-L1400
[C43]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/SimulatedCombatState.cs#L420-L449
[C44]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/SimulatedCombatState.cs#L1429-L1468
[C47]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L868-L939
[C48]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/tools/OfflineSearchHarness/MultiplayerFinalSelectionContracts.cs#L1-L235
[C49]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/tools/OfflineSearchHarness/MultiplayerEvaluationContracts.cs#L1-L277
[C50]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs#L23-L130
[C51]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/tools/OfflineSearchHarness/MultiplayerContracts.cs#L1-L128
[C52]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/tools/OfflineSearchHarness/MultiplayerStartContracts.cs#L1-L60
[C55]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/docs/multiplayer-advisor.md#L1-L99
[C56]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/docs/ARCHITECTURE.md#L1-L80
[C57]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/docs/TEST_MATRIX.md#L1-L77
[C58]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/coverage/test-evidence.json#L1-L65
[C59]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/MultiplayerCycleCheckpoint.cs#L1-L6
[P01]: https://cdn.aaai.org/ICAPS/2005/ICAPS05-010.pdf
[P02]: https://aclanthology.org/2020.tacl-1.51/
[P03]: https://www.lri.fr/~sebag/Examens_2008/UCT_ecml06.pdf
[P04]: https://eprints.whiterose.ac.uk/id/eprint/75048/1/CowlingPowleyWhitehouse2012.pdf
[P05]: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1.sort?view=net-9.0
