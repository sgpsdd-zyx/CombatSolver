# 多人未击杀路线的首回合覆盖与共同层发布设计

**唯一行为基线：`851c1521f5258ec43dc71c24eeb8d5200fdf612d` / 0.43.3。2026-09-20。研究设计，未实施、未编译生产C#、未发布。**

## 1. 决策

优先推进 **C：以完整当前本机回合为代表、按实证共同层发布**。生产首版采用一个明确受限的入口：只有现役批次确被较浅候选压低，所有在册代表又各有更深的完整检查点时，才尝试用更深的共同层排序。给它的名称是“首回合覆盖的共同层发布”，不是新搜索器。**低成本回退为A：本次14周期现役规则**，不是七周期，也不是B。

不默认添加主动队友情景、不安装价值网络、不替换Beam、不新增两步挑战。D的便宜、正确续行有正例，但还缺本地生产成本与基策略可靠性；E引入时间偏好且会抢额度，先留在宿主外评。B仅消除某些同分不对称，不能让第一周期少伤害的铺垫得到第7周期的收益；只在C的同层比较中作为口径对齐使用。

这个选择包含**目标时点政策变化**：过去只承认c=1的输出，现在在完整代表覆盖成立时承认c>1的已实现输出。它不是单纯修错，更不是全面不退化声明。W07/W14说明近端可少打18、后来多打66；W39说明队友提前击杀目标可损失18；W38说明扫描也可抢走当前72伤害。下面用范围、成本和停机条件约束代价，不隐瞒反例。

**生产首版的门禁比本轮通用C_cover原型更窄。** 原型对每层付费扫描，并在阻断根上也尝试覆盖；生产设计仅在已有发布点且有明确混深症状时激活，遇外部阻断等情况走A。这个收窄有源码切口理由，但**还没有生产或额外Python验证它能消除所有负例**；不得把原型正例自动算成收窄版全部验收通过。

## 2. 不变边界与优化目标

H始终为14个**敌方周期**；普通多人预算使用现役解析后的双份额度，不再乘二。固定预算／显式覆盖遵守 `MultiplayerSearchPolicy.ResolveSearchProfile` 的原分支。只有本机手动请求冻结根，建议由玩家手动执行；不自动出牌、重算、结束回合或控制队友。

当下决策单位是完整本机当前回合：牌序、目标、牌内选择、用药及使本机当前回合结束的合法边界。未来条件后缀帮助选择这个单位，但不是队友必须配合或玩家必须照单执行14周期的承诺。

### 2.1 原风险／收益次序原样保留

| 顺序 | 维度与单位 | 事实边界／所有权 |
|---|---|---|
| 资格，先于池截断 | 强制药水指令、本机显式用药最小数量／至少一瓶 | 完整候选先调用现役资格；仍可展开但未满足要求的前缀继续原中间保路，不用最终过滤剪掉。 |
| 1 | 本机死亡，布尔 | 完整具体续行／真实终局；不能用旧安全检查点遮掉。 |
| 2 | 完整可比事实，布尔 | 非终局必须有本次固定d的精确敌方检查点；真胜／死直接取终局。 |
| 3 | 救命消耗，次数，越少越先 | 沿用现役检查点与尾部的较大值；当前观察是全队事件计数，不在本次改成个人归属或改优先级。 |
| 4 | 累计超出每周期3 HP的部分，HP | 沿用本机账本及完整尾部反证；根前本轮已支付、自损算入，治疗／额外玩家回合／手动重算不重置，未用额度不结转。 |
| 5 | 胜利，布尔 | 真实提前终局；不为了窗口对齐把胜利改为旧时点未胜利。 |
| 6 | 队伍存活人数，人 | 共同点与现役尾部反证；不添加预测救援概率、逐人加权或隐藏队友已知死亡。 |
| 7 | 敌方有效剩余HP，HP，越少越先 | 非终局在固定d读取；阶段／复活口径仍由当前唯一引擎决定，不换成名义伤害或正HP下降总和。 |
| 8–10 | 本机累计扣血、当前HP、药水观察次数 | 同一比较边界；本机HP和全队药水观察不混成一个“团队得分”。 |
| 11 | 双方胜利时结束回合 | 沿用原终局同分规则。 |
| 12 | 动作成本，动作数 | A回退仍为完整路线；C对非终局只用精确d边界前动作数，真实终局用完整数，无法定位时不伪造成本。 |

3 HP不是硬过滤。若所有候选都超额，仍由原比较器按已知风险排序并标注；本轮不把它改成“总计42 HP随意花”，也不悄悄加入硬门槛、Pareto标签或权重抵扣。救援与本机风险次序、队友Fairy与本机自损优先级等属于独立产品问题，不借C实施。

额外发布护栏只比较**已经观察到的本机死亡／救命次数／累计超额**按原字典序是否比A选中候选更差。若更差，不以较深高伤害强行替换A。它只是“新增策略不提升已知坏事实等级”的保护，不证明短A未来安全；所有未知仍显示未知。队友与胜利的次序不被该护栏悄悄改成新硬约束。

### 2.2 什么才记质量改善

每次记录整个当前回合动作的规范签名。签名不变，后来展示更长、d更大、ActionCount更少或排序分更高，最多记覆盖／显示变化。签名变化后，还需在相同外部3／7／14周期、相同后续协议下比较本机风险和敌方剩余HP，才可说某个时点输出改善。

本机实际有效伤害和队友伤害作为解释量；两者分开记录，不能把共同伤害都归因本机。击杀、威胁解除和资源消耗不直接与HP重复相加。队友提前结束整场战斗时，双方团队结果相同，本机贡献差不等于团队胜率差。3／7／14均报告而不事后只挑有利时点。

## 3. 为什么选择C，而不是其余候选

| 方案 | 有证据的好处 | 实际反对证据 | 本轮位置 |
|---|---|---|---|
| A 现役共同最浅＋完整动作数 | 资格、终局、尾部反证已成熟；低附加开销；不声称未见未来安全。 | W07/W14的已有深收益被类别浅兜底压住；同分尾部动作不对称。 | 明确低成本回退。 |
| B 仅共同周期动作成本 | M01可消除同分偏差；某些状态也可能换完整当前回合。 | W03/W07当前计划不变；M02可换成更差首回合；长路线重放付费。 | C内配套口径，不能单独充当长线主方案。 |
| C_count 候选数够即选深层 | 简单，容易提升报告深度。 | W26/W38低分支多后缀压过尚未完成的强首回合；预算变大仍可错误。 | 否决。 |
| C_cover 全在册首回合覆盖 | 利用已付费检查点；W03/07/14有统一时点实际输出收益；不需新增尾部策略。 | W28外部选择拖慢；W38断点扫描抢额度；W39目标被杀退化。 | 优先，但使用受限生产入口并接受回退。 |
| D 代表付费补齐 | W40廉价正确尾部使7/14各多打102；有可能解决主搜分支贵而覆盖不足。 | W40错误基策略同到14仍失去收益；W24/38抢掉主搜；两个代表不等于所有当前方案。 | 宿主对照，不进默认首版。 |
| E 多时点曲线 | 明确展示何时亏／赚；能揭示只看末点的取舍。 | W35等权早期更好、14更差47；权重变化反选；W24预算520扫描造成实际退化。 | 默认外部诊断，不加到生产总分。 |

C不是解决所有组合发现问题。W21给了完整第三步组合目录，原型能选对，不证明生产中该目录可生成。若首次丢失发生在第一／第二卡片分支，改最终窗口不解决它；仍不恢复固定两步挑战或加一个能力席位。

## 4. C的最小数据结构

复用 `SearchNode`、parent动作链、现役不可变 `MultiplayerCycleCheckpoint` 和 `MultiplayerFinalBatch`。只在多人分片内增加两个私有小记录／等价局部结构即可；不建立Planner、provider、registry或长期搜索树。

```text
FirstTurnSignature
    rootEpoch                         // 一次冻结请求的身份；不跨请求缓存分数
    normalizedActions[]              // 当前完整本机回合，含card/potion/choice/target和次序
    endBoundaryKind                  // EndTurn、卡牌强制结束、提前胜利等
    hash + exactEquality             // hash只加速，冲突必须精确比较

CoverageStamp
    rootEpoch, candidateGeneration
    representativeSignatures[]       // 本次比较声明的范围
    completedDepth                   // 本批固定收益时点d，不是探索深度
    coverageCount, requiredCount
    terminalRepresentatives[]
    suppressedReason                 // 无升级、缺代表、外部阻断、预算、资格等
```

节点到签名、到d动作游标的缓存归**本次多人solver协调线程**拥有。建议用请求本地表或短局部表，键为节点引用／不可变身份；不改战斗指纹，不塞到 `SimulatedCombatState` 或转置标签中。每次请求结束清空；只保存纯动作与纯观察，绝不为了一个评分索引持有旧Simulator。

### 4.1 当前完整回合如何识别

不能只取第一张牌，也不能简单以第一个字符串 `EndTurn` 截断。沿当前 `FindCurrentTurnBoundary` 的真实逻辑确认根本机回合已结束，再从parent链提取该回合的合法动作；卡牌导致额外回合、强制结束、提前胜利必须走实际边界。`PlanAction.Turn`、根本机回合号和Outcome可辅助定位，不能用敌方周期代替本机回合号。

签名区分同名卡实例、目标身份、选择结果、药水槽位／实例与次序；重复职业／两只同类怪不能只用显示名称。牌内选择未解决、外部玩家选择阻断当前回合或只到额外玩家回合尚未验证首个敌方阶段，不当作完整可发布的覆盖证据。

未来某个周期用了药并不会改变已经完成的当前回合签名，但会影响该**具体后缀**的资格、资源和风险。不能因签名相同就合并未来药水账本或丢掉已知消耗；覆盖关系只是“这个当前计划至少有一条这样的已验证本机续行”。

### 4.2 代表集合不许凭结果缩小

R取本次待发布候选中的完整当前回合代表，并纳入当前已保留、同一根的首回合代表清单。它是既有Beam保留下来的有限范围，**不是所有合法回合方案**。如果新计划进入这一可见范围，要更新本批代次并重新判断覆盖，不能沿用旧“全部已覆盖”。尚未产生完整合法首回合的活动前缀要标未完成，不能拿其第一张牌匹配现有代表后宣称已覆盖。

首版范围门禁：至少2个不同完整当前回合，否则当前决策没有可比较的变化；在册代表超过现有 `BeamWidth` 时暂走A，**不偷偷取前Beam个把其余删掉**。这个限制是控制首版元数据与未覆盖情况的保守工程范围，不是测得的最优代表数，也不改变生产Beam宽度。

最终发布资格在R的可发布集合构造前检查。仍可扩展但尚未完成强制用药的前缀继续原搜索；它不因没有最终资格就被证明无前途，也不能被冒充已经覆盖。报告须同时记录 `pendingEligibilityRepresentatives`，防止把“合格代表已覆盖”误写成“所有前缀都探索完”。无合格路线沿用原显式返回／错误处理，不伪造候选或降低用药政策。

## 5. 共同层与风险证据

对每个在册合格完整首回合r，在**仍可见、实际已生成的具体续行**中，找最大的完整敌方检查点深度；终局用完整事实，视为不再需要向后延展。非终局尚未完成受击的尾部不增加深度。

若每个r均有某条续行覆盖d，则d可作为共同层。选所有可共同覆盖深度中的最大者，且d≤14。这里“最大”只用于决定**全代表相同的证据边界**，不作为候选排名维度。低分支方案到14、另一代表只到1时，d仍是1；不同后缀数量不改变覆盖计数。

```text
For each representative r:
    E[r] = eligible maximal concrete continuations of r in this publication generation
    depth[r] = max completed checkpoint among E[r]
               or H for a real terminal witness
If any required r has no completed witness: no C batch
d = min_r depth[r]
```

`maximal concrete continuations`只抑制当前池内被**同一具体路径的已知延长**取代的祖先版本，不是按首回合把所有分支压成一个节点。好坏两个后缀必须都可参与；不能把坏后缀升级成根计划黑名单。

比较时不构造删去尾部的新SearchNode。直接对原具体节点读取d检查点，并继续把完整尾部的死亡、救命消耗、超额和队友反证交给原比较器。这样“延长后发现坏事”不会仅因把可见收益移回d而消失。

**不新增旧安全节点复活机制。** 首版在每个现有发布点由当前可见池重算C；CoverageStamp是诊断，不是一棵可随时恢复的旧树。若上一发布胜出具体路径已有更长反证，不能把保存的纯旧分数当最新安全结论。预算失败回到现役A的可见池处理，并保留“未完成／未知”的标签；不声称已经修好A所有跨代祖先证据问题。

### 5.1 首版激活条件

下列条件同时成立才做C的固定d比较，否则直接用本次A批次：

1. 显式多人入口，冻结根相同，当前没有自动操作；候选资格仍由现役方法控制。
2. R内有2至BeamWidth个完整当前回合，且没有尚未解析的外部玩家选择、未支持语义或首个敌方周期未完成的必需代表。
3. 仅使用现有已经付费的检查点，能得到共同d，且 **d > A本批共同周期**。若d相同，不为了末级动作数再做一遍C。
4. 元数据处理和可能变长的最终重放有同一请求的剩余额度。已分派作业、待排空工作也属于这份额度。
5. C候选没有比A新引入更差的已知本机死亡／救命次数／累计超额等级；不隐藏其他已知反证。

门禁2会绕开W28的“外部阻断代表拖住所有人”；门禁3避免在正常同深池每层付费重排。但本轮没有用新参数补跑一个号称“消灭所有负例”的生产替代结果。W38揭示任何正开销都可能在准入断点改变搜索，不能用门禁文字消除这条反证。

当无升级时不把d写成0覆盖，也不抹去A中已有正检查点；返回原A结果及明确 `CNotAppliedReason`。终局全池直接走A，避免把H哨兵误当实证覆盖。

## 6. 从请求到最终发布的完整伪代码

以下是设计伪代码，不是声称已运行的C#。所有新观察与计算都只在多人分派后发生。

```text
Solve(request):
    if request.Policy.Multiplayer is null:
        return ExistingOfficialSoloSolveExactly(request)

    root = request.AlreadyFrozenRoot
    profile = ResolveMultiplayerProfileExactlyOnce(request)
    work = request-owned counters + deadline + admitted-work/drain accounting
    signatures = request-local bounded metadata cache
    lastPublication = none                    // 纯结果，不拥有新模拟器

    try:
        run the existing three-lane Beam without new seats:
            obey existing legality, choices, TT and phase semantics
            keep incomplete forced-potion prefixes in intermediate search
            account actual parent jobs, child simulations and replays
            record expanded-parent enemy cycle AND generated enemy cycle

            at an EXISTING drained publication boundary:
                raw = all candidates already known for THIS publication
                // 包括现在已经决定合入的fallback/cohort；不是未来候选
                lastPublication = TryPublishWindow(raw, request, work)

        finalRaw = ExistingFinalRawUnionWithExactFallbackConditions()
        return TryPublishWindow(finalRaw, request, work, final=true)
    finally:
        stop further admission
        drain every admitted worker in the existing order
        release each simulator/batch lease exactly once
        discard request-local signatures and coverage metadata
```

```text
TryPublishWindow(raw, request, work, final=false):
    // 不在一个已经截到4B的小池上重新推断原始代表范围
    eligible = ExistingFullFinalEligibility(raw)
    Acontext = ExistingCreateMultiplayerOrdering(eligible)
    Abatch = ExistingSortAndTake4B(eligible, Acontext)
    Awinner = ExistingSelectFromSameBatch(Abatch)
    if no eligible result: return ExistingNoEligibleResult()

    // A上下文与C候选上下文是显式的不同政策试算，费用分别计入；
    // 最终只选择其中一个批次。后面的预览/最终消费不得再自行换d。
    if not CanPayBoundedMetadataAndPotentialFinalReplay(work):
        return MaterializeExistingBatchWithinBudget(Abatch)

    R, pending, reasons = BuildExactCurrentTurnRepresentatives(eligible, request)
    if not ScopeGate(R, pending, reasons):
        return MaterializeExistingBatchWithinBudget(Abatch)

    witnesses = CollectAlreadyPaidMaximalConcreteWitnesses(eligible, R)
    d = GreatestCommonCompletedBoundary(witnesses, R, H=14)
    if d <= Acontext.CompletedEnemyCycles:
        return MaterializeExistingBatchWithinBudget(Abatch)

    Cpool = concrete witnesses that cover d + eligible true terminals
    if not EveryRequiredRepresentativeHasWitness(Cpool, R, d):
        return MaterializeExistingBatchWithinBudget(Abatch)

    Ccontext = FreezeOrderingAtExplicitBoundary(d)
    // 相同风险/终局/收益次序；最后的动作成本按精确d定位。
    Csorted = SortUsingOriginalFactsAndFullTailCounterEvidence(Cpool, Ccontext)
    Cwinner = Csorted.first
    if KnownRiskLex(Cwinner) > KnownRiskLex(Awinner):
        record known-risk-regression reason
        return MaterializeExistingBatchWithinBudget(Abatch)

    Cbatch = ExistingFinalBatch(Csorted.take(4*BeamWidth), Ccontext)
    stamp = immutable CoverageStamp(R, generation, d, allRequiredCovered=true)
    // 剪到4B前已完成全R证据比较；stamp不持有被释放的simulator。
    return MaterializeAndPublishSameBatch(Cbatch, stamp, work)
```

```text
ActionsThroughBoundary(node, d):
    if node is real win/death: return node.ActionCount
    if d <= 0 or node lacks an EXACT d checkpoint:
        fail C applicability; do not return zero
    cursor = node
    while cursor.Parent exists and cursor.Parent.CompletedEnemyCycles >= d:
        cursor = cursor.Parent
    require cursor.CompletedEnemyCycles == d
    require cursor.Parent.CompletedEnemyCycles == d-1
    return cursor.ActionCount
```

实际C#可从 `MultiplayerHorizonContracts.ExperimentalActionCountAt:517–528` 迁移这个已存在的离线算法，但需要验证本次分支中额外玩家回合、终局、动作隐式结束及root已有周期的条件。若该算法不能定位，关闭此次C而不是吞异常后用完整动作数或0让排序悄悄变政策。

```text
MaterializeAndPublishSameBatch(batch, stamp, work):
    winner = SelectFromBatch(batch)                // 不重建排序上下文
    if winner's simulator has been released:
        require admission for Replay(winner.Actions) + mandatory drain
        refresh by existing Replay/RefreshReleasedFallback
        verify root identity, legality and checkpoint/result consistency
    if canceled, stale, unsupported, or replay cannot be admitted:
        use only an already validated legal fallback / explicit incomplete status
        never present an unvalidated C route as valid advice
    record selected_cycles separately from stamp.depth
    emit preview/final only from this batch and materialized facts
    retain original manual execution and stale-result rules
```

### 6.1 预览、最终池与跨批次压缩

当前 route preview 排除了释放了simulator的节点；因此历史合同只证明“相同候选池、同批次上下文”一致，不证明每次预览和最终总选同一条。C也不能伪造这项保证。源池不同，就记录新的generation和共同周期；同一批次的预览／最终必须共用上下文。

`Phases:2005–2016`的更早completed局部压缩可能已经丢失路线。首版不保存所有历史候选；若在同一次发布前已经知道必须与frontier联合，代表／排序应从这次已知union取，再做原4B限制。若未知的新浅路线是以后才生成，不能承诺现有固定内存恢复过去丢失候选。把这个边界写进测试，不用“最终批次已统一”遮盖。

## 7. D与E的备选伪代码：足够具体，但本轮不默认实施

### 7.1 D：代表付费补齐

```text
PaidContinuationPilot(representatives, existingBudget):
    include the current A complete first-turn plan
    select a small set by explicit current-turn identity, not only first card
    do NOT allocate a fresh solver budget per representative
    reserve final validation + admitted drain from the same request

    for each common target boundary in ascending order up to 14:
        begin tentative round for ALL selected representatives
        for each representative in deterministic interleaved order:
            pay to replay exact prefix if its state lease was released
            apply ONE SAME legal base continuation policy
            pay choices, forks, action dispatch, enemy transitions, scans
            stop on real terminal / external choice / unsupported / budget
        if every representative completed this boundary:
            publish a complete comparable pilot batch
        else:
            discard the partial pilot as a basis for promotion
            keep baseline, but DO NOT refund spent work
            stop pilot
```

若只抽2个代表，必须明确它们不是全Beam范围；不得用这2个算到14去宣称尚未试过的第3个高分支强当前计划更差。生产若接入D，代表选择本身也需对照。目前补充W40给D一个相当有利的假设：减少未选分支的枚举，只验证一个可执行动作；它仍支付操作和回放，所以有正例。但这个单支合法策略在真实卡池能否便宜、不会提前浪费资源，还未证明。

外部选择、强制药水、额外玩家回合都沿原引擎推进；不是用一个EndTurn当一个敌方周期。最后一个父节点名额不能通过“把yield读完”补模拟，`Expansion.cs:409–410`以后仍可能开始新的结束回合工作。显式准入兄弟分支，或回到原行为；不能改节点计数让成本看起来没增加。

### 7.2 E：已覆盖的多时点事实

E的实验分数在原风险／胜利／队友层级之后，对共同已经覆盖的锚点3／7／14的敌方剩余HP做加权平均。例如等权表示每个命名检查时点同等重要，不表示每个敌方周期等权，也不表示到该时点的概率。未覆盖时点不可补0、凭空估值或按末值延续；只有真实终局可吸收。

E若未来生产化，必须固定所使用的锚点集合、权重语义、无完整锚点时的回退，并把曲线读取计入预算；其目标是新的时间偏好，不能叫纯correctness fix。W35已证明不同合法权重会给不同当前计划。当前把曲线留作诊断与外评，少一套生产目标更可维护。

## 8. 预算、所有权、缓存与取消

### 8.1 不用ExpandedNodes冒充完整成本

现役节点额度以父展开准入为主，工作包含多卡选择、回合分支、重放与worker合并。生产探针同时记录：`ExpandedNodes`、真实模拟动作／敌方阶段分派、Replay调用及重放动作数量、资格／候选扫描、比较次数、查找动作游标步数、最终重放、排空等待、峰值simulator租约和元数据字节。

固定逻辑工作对照和固定墙钟对照都要做。抽象的256最终化保留和382断点**不进生产常量**；真实预留由当前重放长度与实际工作统计确定，或者保持当前软预算语义并如实报告尾部成本。若无法证明新增C最终化与取消排空落在用户现有总预算内，就不准入C升级，而非静默超时后称预算没变。

同预算也可能有浪费：已有元数据扫描无法退款；预算停止前未提交的兄弟分支已经计算过则必须计入。不得把被拒绝／丢弃候选的模拟从工作账中删掉。对仍未开始的巨大作业，可以不准入并返回已验证建议，剩余闲置量单列。

### 8.2 最小所有权表

| 对象 | 捕获／变化 | 所有者与释放 | 是否进战斗键 |
|---|---|---|---|
| 冻结根与本机身份 | 原Runtime手动捕获 | 原request与solver；不读后台live补值 | 原语义不改。 |
| 分支状态与原检查点 | 原Fork／Replay与敌方周期捕获 | 原lease，checkpoint不可变共享 | 原键／多人账本标签不改。 |
| FirstTurnSignature缓存 | 协调线程发布点按纯动作推导 | 请求内纯元数据；结束清空 | 否；哈希冲突用精确动作相等校验。 |
| d动作游标缓存 | 精确parent边界首次查询 | 请求内整数缓存；不保留额外simulator | 否。 |
| CoverageStamp | 每个发布代次构造，不跨根使用 | 纯DTO，诊断／结果附属 | 否。 |
| C候选排序表 | 原可见节点引用的暂存数组 | 不拥有新模拟器；待旧批次处理完成统一释放 | 否。 |
| D试算状态（仅宿主） | 付费重放／同策略续行 | 单独租约，取消必须排空；上限内占用 | 不允许复用为跨根树。 |

重复职业、host/client、目标死亡和新怪物出现，都要求身份来自冻结根／分支真实对象映射，不拿显示名称当缓存键。一次手动重算产生新rootEpoch，旧CoverageStamp失效；旧路线只有现役≤4×32动作重验能力，不能新加跨请求分数缓存。

### 8.3 取消顺序

只在现有worker已排空的发布点做C元数据。取消后停止新准入，等待已经准入的工作按原顺序回收，再释放parent／probe／aggregate，最后销毁C元数据。源码 `AdmittedExpansion.cs:214–232` 的顺序不能反过来。若最后可显示的是旧请求结果，沿原过期提示，不将它标成新根的安全建议；不得为了获取“第一条建议”在后台继续模拟并超出请求生命周期。

## 9. 最小生产探针与证伪矩阵

先复用 `MultiplayerHorizonContracts`、`MultiplayerFinalSelectionContracts`、`MultiplayerReviewContracts`，不另造引擎。现有Horizon宿主已经能观察ExpandedCycles、提取当前回合动作、独立EvaluateCurrentTurn；但它的后续协议主要是一张指定Strike，原生设置以单敌为主，**不可原样声称覆盖任意牌序、多敌和资源兑现**。

需要一项局部stage或等价参数，冻结根一次，逐臂从该根新建solver。不要改生产策略之后还拿不同根做A。A/C/B比较应通过真实最终入口，而非只用反射手造快照。

```text
RunWindowSelectionProbe(rootFixture):
    root, liveStamp = capture once
    resolved = resolve current multiplayer request budget once
    for policyArm in [A, B_offline, C_prototype]:
        solve from root with SAME resolved nodes/time/DOP/seed
        hook actual final raw union: record each candidate source and eligibility
        hook first loss: card/choice prune, retention, completed compression, final batch
        export exact complete current local turn, not merely first action
        record actual work + selected/display/common/depth distributions
        evaluate selected complete current turn from root separately:
            passive-peer protocol to cycles 3/7/14
            legal target-kill / defense / phase perturbation variants
        require liveStamp unchanged
    run official-solo entry sentinel with C counters strictly zero
```

| 最小反例／哨兵 | 必须看见的事实 | 不能冒称通过的替代 |
|---|---|---|
| P1 混深高HP与独立铺垫 | 实际fallback条件触发；两种完整当前回合都真实回放到3或7；A c1、C更深且同外部时点变化 | 只在最后池手工塞一个浅节点。 |
| P2 共同时点动作游标 | 同首回合不同后缀、不同首回合同c1；额外回合不跨敌方周期 | 只比较动作字符串长度／只改显示。 |
| P3 高分支未完成代表 | 记录不同首回合数、未完成身份；C_count可选反，C覆盖不谎报 | 同一首回合8个后缀算8份覆盖。 |
| P4 好坏续行 | 同首回合具体好后缀继续可选；坏后缀完整死亡／救命／超额保留 | 全首动作黑名单、删除尾部反证后声称安全。 |
| P5 资格与边界 | 强制药水未完成仍可展开；无合格路线显式返回；额外玩家回合、外部选择、提前终局 | 把自动救命计入本机显式用药；把队友选择当pass。 |
| P6 工作断点 | 准入、未接收兄弟、最终Replay、元数据、取消排空全计；同预算首回合可退化也要留结果 | 扩大Beam／时间、只记保留候选耗时。 |
| P7 队友与阶段 | 同一个当前前缀用于全部扰动；提前补刀、目标失效、补防、阶段和RNG | 每个情景分别全知重选当前回合，再平均成“胜率”。 |
| P8 官方单人隔离 | 新签名／覆盖／d成本计数器均为0；原入口、参数、能力承诺与结果对照 | 只看一条路线碰巧一样。 |

**不可退化哨兵：** 选择一条本机本周期防死／满足药水要求的合法完整当前回合。任何C变更若允许不合格候选、遮掉已知本机死亡／救命消耗／超额，或绕进单人路径，应立即停止，不以更高伤害抵扣。该哨兵不等于总体胜率门禁，也不要求牺牲所有风险目标追求零损。

## 10. 最多两批实施

### 第一批：真实入口对照与最小多人覆盖原型，生产默认仍A

**目的不是再写一轮泛泛诊断，而是让同一个冻结根实际走A/B/C三个最终入口，并得到可归因的完整当前回合对照。** 最少改 `tools/OfflineSearchHarness/MultiplayerHorizonContracts.cs`，复用FinalSelection/Review契约；在多人分片增加私有／测试可达的签名、共同层选择助手。只能多人入口访问，不改官方单人方法、能力策略和默认参数。

先做P1、P2、P4及一个工作断点P6；外部评价统一3／7／14并标明协议。新增C在单个真实根确实改变完整当前回合且有长线实测收益后，才把它带入下一批。若真实入口根本没有浅兜底问题，或强计划更早在卡内保路丢失，停止这条实现方向，保留A并把准确首次丢失交给下一项局部研究。

第一批出口必须同时有：真实原始池与浅节点来源、资格／风险正确、全代表覆盖而非数量、当前完整回合变化、正反例及实付预算。没有DLL的本文Python不能替代这些出口。

### 第二批：在现有发布点接入受限C，验收后才决定默认开启

最少生产切口为：

- `CombatBeamSolver.Multiplayer.cs`：在完整资格池上构造有限代表／显式d，复用FinalBatch；增加一次性的C适用门禁与A回退，保持原4B。
- `CombatBeamSolver.MultiplayerEvaluation.cs`：支持显式已验证d上下文，非终局的精确d动作游标；原风险／终局层级不变，A路径保留原完整动作数。
- `CombatBeamSolver.Phases.cs`：只在多人现有发布／最终原始合池位置调用；保持先排空后处理、原兜底条件和释放；必要时对本次已知joint pool统一裁剪，不保存历史树。
- 宿主合同与最多一处纯多人结果元数据；UI只说明真实比较范围／不适用原因，不增加自动按钮或大配置面板。

只需一个可撤回的内部多人实验开关或等价受控发布机制，不建多策略生产框架。通过固定工作量与固定墙钟的同根对照，加入P3/P5/P7/P8和原生动作差分，再决定默认打开。任何现实预算退化集中在常用请求档、强首回合被遗漏、风险账本变化、资源资格变化或solo计数器非零，关闭C回A；**花掉的本次预算不能因回退而假装退款**。

## 11. 收益主张、反对证据与停止条件

**能支持的预期收益：** 当两个不同完整当前回合已经都有较深可比证据，浅兜底只因候选年龄／类别把共同收益压低时，C允许本机独立能力成长、延迟攻击、资源成熟与多动作次序收益进入当下选择；无需猜队友会配合。这个收益可以在源代码最终入口被证伪。

**不能保证的东西：** C不会自动找回被Beam裁掉的组合，不会把未展开高分支变成已覆盖，不保证第3／7／14同时更好，不保证对目标失效稳健，也不保证元数据开销始终值得。W28、W38、W39分别反驳覆盖保守性、预算免费性与未来目标可靠性。D_fast_hold正例进一步反驳“永远只做已有证据比较足够”；只有真实尾部便宜且一致时，才值得重新考虑D。

**明确停止／撤回：**

1. 任何资格、终局、已知风险、单人零进入、线程／租约释放错误：立即回A，不接受统计平均抵扣。
2. P1不能在实际最终入口复现，或C只改变后缀／显示而不改善同外评时点的完整当前回合：不默认启用，停止把此项当长线收益修复宣传。
3. 同预算固定根中出现可解释的强首回合丢失且与常用预算相关，不能靠更窄触发或少一次重复扫描消除：回A；不加总预算掩盖。
4. 同时点收益仅来自队友被假定配合、真实目标事件使收益系统失效、或为了通过而改权重／把有效伤害改名义伤害：不接受该收益。

这里不凭24个人工根给一个“提升率达到X%就发布”的假门槛。最有区分力的是P1真实候选来源＋同根A/B/C完整首回合外评，以及P6真实工作准入断点；它们直接决定本设计是否击中用户的问题。

## 12. 交付与复现边界

配套Review给出全部源码事实、方法／物理行号与数值解释；Python为标准库缩减模型，JSON和stdout为本环境实际输出；Read_Sources与证据ZIP保留一次输入凭证、源码读取范围和研究过程修正记录。

这轮没有编译生产C#、没有游戏DLL、没有原生实际／模拟差分或真人联机。本文伪代码与宿主stage是**待本地实现／验证的最小接入设计**，不是伪造的C#通过结果。所有产物写在解压源码之外；未修改、推送或发布仓库。

## 原始资料与适用前提

以下为本轮实际检索的作者／出版方资料；不是借用算法名称作项目性能证明。PDF 页码同时给出印刷页与 PDF 零基索引，后者用于定位。

| 编号 | 原始来源、读取位置 | 支持的具体取舍与不适用处 |
|---|---|---|
| P1 | Korf，1985，*Depth-first iterative-deepening: An optimal admissible tree search*，Artificial Intelligence 27:97–109，[出版方摘要](https://www.sciencedirect.com/science/article/pii/0004370285900840)，DOI `10.1016/0004-3702(85)90084-0`。本轮只读取摘要／作者出版信息，未通读证明。 | “完成一轮再替换结果”可作设计启发；其树搜索／可采纳性结论不能给截断 Beam、混深池或代表覆盖率背书。 |
| P2 | Hansen、Zhou，2007，*Anytime Heuristic Search*，JAIR 28:267–297，[作者论文 PDF](https://arxiv.org/pdf/1110.2737)，Algorithm 1 与 incumbent／界讨论，印刷 pp.271–272，PDF 索引4–5；已查看算法页截图。 | 可保留已完成 incumbent；这里没有可采纳尾值或最优误差界，不能由 anytime 推出当前动作越来越好。 |
| P3 | Bertsekas、Tsitsiklis、Wu，1997，*Rollout Algorithms for Combinatorial Optimization*，Journal of Heuristics 3:245–262，[作者 PDF](https://web.mit.edu/dimitrib/www/rollout.pdf)，顺序一致性、Proposition 1 与两步变体，pp.249–253，PDF 索引4–8；已查看命题页截图。 | 不劣于基策略的论证依赖终止、同一目标与基策略一致性等条件；付费截断、换评价时点或错续行都不能直接套保证。 |
| P4 | Bertsekas，2020，*Constrained Multiagent Rollout and Multidimensional Assignment with Auction Algorithm*，[作者论文 PDF](https://arxiv.org/pdf/2002.07407)，v2，fortified rollout pp.9–10，PDF 索引9截图。 | 保留完整可行轨迹能保护特定模型目标；一条只到周期1的 CombatSolver 前缀不是周期14完整可行解，真人队友也不是共同受控的决策变量。 |
| P5 | Kaelbling、Littman、Cassandra，1998，*Planning and acting in partially observable stochastic domains*，AI 101:99–134，[作者 PDF](https://people.csail.mit.edu/lpk/papers/aij98-pomdp.pdf)，模型与 belief state，pp.101–106，PDF 索引2、7截图。 | 概率式 belief 更新需要转移／观察模型。本项目没有校准的真人队友策略分布，扰动只能标为条件假设，不能捏造胜率或置信度。 |
| P6 | Rawlings、Mayne、Diehl，2017，*Model Predictive Control: Theory, Computation, and Design*，2版首次印刷，[作者 PDF](https://sites.engineering.ucsb.edu/~jbraw/mpc/MPC-book-2nd-edition-1st-printing.pdf)，§2.10，印刷p.164，PDF索引211；已查看截图。 | 有限视界／滚动执行本身不提供一般稳定或最优保证；延迟收益与终端价值问题不能靠“每次只执行当前回合”自动消失。 |

**本报告的推断而非论文结论：** 本机先执行一个完整当前回合、未来按同一条件协议评价，是本次实验控制变量的办法；人工重算没有被改成自动 MPC。C 的代表覆盖是有限集合内的证据条件，既不是全状态最优性，也不是对未知队友的安全证书。
