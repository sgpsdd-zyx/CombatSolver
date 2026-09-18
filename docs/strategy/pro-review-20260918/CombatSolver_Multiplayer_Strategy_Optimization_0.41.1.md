# CombatSolver 0.41.1 多人策略优化设计

**研究基线：** fork `0.41.1`，固定提交 `5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77`。  
**日期：** 2026-09-18。  
**配套复审：** `CombatSolver_0.41.1_Review_Conclusions.md`。  
**性质：** 待实施、可证伪的研究设计；本次未改仓库、未推送、未发布。文中的代码是伪代码，不表示现有仓库已经实现。

## 1. 基线、证据边界与唯一取舍

### 1.1 主方案

选择 **“批次一致的 Beam + 单席位两步前缀挑战，远端继续现行七周期搜索”**。

它包含三个相互依赖但可独立验收的变化：先贯通发布候选的资格过滤和共同周期上下文；再澄清本机／队友救命资源观测；最后在原 Beam 的首次关键截断处，为极少数被淘汰的不同首动作尝试第二个合法动作，把成功的两步节点送回原搜索。**不另建 Planner、不增加保留 Beam 宽度、不另给一份时间或节点预算、不引入训练或运行时云端模型。**

远期仍由唯一战斗引擎和现有多人跨周期搜索到最多 7 个敌方周期。近端的有限两步展开增强组合可达性；远端仍是有限宽度的近似策略搜索，而不是近似战斗物理。这个选择比额外造一套尾值模型或整棵 MCTS 树更小，且直接针对本轮可定位的“先筛掉什么”问题。

### 1.2 低成本回退

**只保留最终候选管线修复，继续使用 0.41.1 的三通道 Beam、现行七周期推进与现行请求预算；不执行两步挑战。** 已确认且通过隔离测试的资源归属修复可以保留；尚未决定的资源优先级不强行更改。

回退采用独立提交撤回／不合入可选挑战代码，不建立 `Baseline/Lite/V2` 等长期多策略版本开关，也不回退到官方单人策略。R1/R2 的正确性修复不应随搜索质量实验一起撤掉。

### 1.3 为什么不是现在改换主算法

当前没有新失败战斗包，没有本机 Fork、阶段推进、选牌组合及完整重放的实测成本。已经发现的 R1/R2 位于算法名称之外：同样的资格过滤和评估上下文错误会污染 Beam、best-first 或 MCTS。现有小 Beam、共同检查点和单人能力隔离已落地；重做旧问题或换名建框架都不是本轮价值。[^C01][^C02][^C04][^C05][^C12]

本轮 S/A 证据支持先修管线；两步挑战是否改善实际根动作仍是 H。没有证据证明它是所有实战问题的最佳算法，也没有证据证明 MCTS 必然更差。算法切换条件见第 11 节。

### 1.4 不变的产品合同

只有本机安装 Mod，其他真人既不安装也不提交计划；建议只针对本机。用户手动请求、手动执行、手动选牌／用药、手动重算和手动结束回合。队友已经做过的动作是新根事实，其未来动作是条件不确定性。

每敌方周期 **3 HP 是本机毛扣血目标**，包含本周期根前已付扣血与自损；治疗、额外玩家回合、重新计算都不重置；未使用额度不结转。致命风险优先于有价值输出，但不是要求每周期主动损失 3 HP。窗口最多七个敌方周期，预算不足可返回较短证据，不得把估值称为完整受击验证。

仅多人策略可变。原单人调用路径、参数、能力政策、药水策略、预算和行为保留；公共文件只做必要显式分派。任何风险默认变化都需要单列，不用“优化”一词掩盖。

## 2. 当前实现诊断与工程入口

### 2.1 当前真实调用链

```text
手动 RequestSearch
  → 多人政策 Apply（关闭单人专属策略入口）
  → 主线程 CombatRootSnapshot.Capture
     ├─ LocalContext.GetMe：本机身份
     ├─ 已付毛扣血、全队冻结状态
     └─ 捕获前后 stamp 一致性
  → CombatSearchCoordinator 的多人早分派
  → CombatBeamSolver
     ├─ 最多四条旧动作路线：在新根验证／重放
     ├─ 本机合法动作、目标、选牌、药水与 Fork
     ├─ 三通道 Beam / 多人状态标签 / 并行展开
     ├─ 全队被动阶段与本机下一回合
     ├─ 敌方周期不可变检查点，最多七周期
     └─ 预览、预算中断、上一层候选、最终重放
  → 多人结果管理：手动建议、过期标记、禁止自动部署
```

对应入口和定位见源码资料 [^C01][^C02][^C10][^C12][^C13][^C15][^C16][^C18][^C21][^C22]。这不是要求把所有入口都修改。

### 2.2 本轮诊断的三层分离

| 层次 | 本轮判断 | 应采取的动作 |
|---|---|---|
| 比较管线 | 有资格候选可能先被截掉；同一选路过程可能重建共同周期 | 先用生产函数最小合同修复，R1/R2 为最高确定性工作 |
| 风险观测 | 全队救命次数和药水使用缺少受益／消费玩家归属 | 补多人观察，不改公共战斗结算；明确资源优先级 |
| 搜索覆盖 | 已有三通道仍可能看不到两步后才出现的价值，或不同首动作被同族候选挤掉 | 用受限挑战探查；是否合入由固定工作量消融决定 |

有效敌方 HP 汇总不能完整表达按受害者区分的威胁，但原引擎并非没有目标语义。被动队友模型可能过度防守，也可能因队友触发有害联动而乐观，暂时不能将其视为已证实唯一根因。两者先进入测量与最小 fixture，不在第一批追加新高权重评分。[^C05][^C09][^C11]

## 3. 算法比较与原始资料

以下成本是对本项目的**结构分析**，不是本机测量。令 `B` 为原 Beam 宽度，`b` 为包含目标和选牌的平均合法分支数，`D` 为动作／阶段搜索深度，`c` 为一次真实引擎转移成本；它们在不同牌组和多人局面间可能差异很大。实际成本还包括 Fork、历史键、选择展开、重放和释放。

| 方法 | 信息要求、适用条件 | 主要失败模式 | 本项目模拟、内存与维护代价 | 本轮取舍 |
|---|---|---|---|---|
| 现有 Beam 的增量改进 | 冻结根、合法动作和可比较评价；适合已有稳定引擎与严格预算 | 不可逆剪枝丢失延迟组合；宽度很小时不能保全所有首动作 | 大致 `O(B·b·D·c)`，并非每一步同成本；复用现有有界状态与回放设施 | **主方案基础** |
| 当前回合强搜索＋独立尾值 | 需要可信长线价值或有限续行政策 | 当前回合枚举仍指数；尾值外推错、把估计当安全；尾部模型可能支配首动作 | 若枚举深度 `d`，最坏 `b^d`；每叶做 rollout 又乘尾长，校准维护中高 | 暂不新增独立尾值模型；保留现行七周期搜索 |
| 手动滚动规划 | 新根能反映队友已发生动作，用户主动重新请求 | 手动重算之间环境已变；单个未来脚本不等于反馈策略 | 单次成本仍是所用求解器成本；跨次只留少量动作 | **现有交互继续保留**，不是另一个新求解器 |
| 有限合法队友情景＋鲁棒评估 | 可得信息和真实合法的 peer actor 执行入口；情景数受限 | 未覆盖人类行动；非法假设／隐牌泄漏；不同世界选不同首动作产生全知偏差 | `S` 个情景近似乘以 `S` 次重放与续行；选牌／交错进一步放大；所有权和键维护中高 | 不进入首版生产；只定义可实施的影子实验门槛 |
| best-first／局部精确枚举 | 有效优先级；若声称最优还需适当界和终止条件 | 非采纳启发式不保证最优；低即时价值分支仍饿死；零费循环 | 局部两步最坏 `O(b²·c)`；一般 open 集可指数增长 | 只借用有界两步枚举，不换全局队列 |
| MCTS / UCT | 可重复合法模拟、有效 rollout、奖励定义 | 有限预算漏见罕见致死；多选择分支稀释访问；错误人类模型不会自行消失 | `N` 次迭代至少含路径重放＋rollout；树通常随访问增长，完整 children 初始化还依赖 `b`；接入维护高 | 达到量化门槛后才做离线原型 |
| PUCT | 还需要有意义的动作 prior；可以是规则或均匀先验，不必是神经网络 | 先验错误可持续压制好动作；未经训练的 prior 不产生胜率含义 | 在 MCTS 成本外增加 prior 求值；需要同预算消融 | 不因名称更先进就替代 Beam |
| 宏动作／有界组合 | 合法动作序列、可中断点与实际状态变化 | 固定脚本遇到目标改变／选择失效；组合覆盖仍有限 | 两步临时展开可小；通用技能／选项体系维护高 | **只用两个原子动作的临时挑战**，不建宏动作注册表 |
| 学习排序／价值 | 版本匹配、合法且公平的数据、足够失败根、离线训练和漂移测试 | 数据偏差、隐藏信息、版本漂移、不可校准风险 | 采集、训练、部署、校验均有持续成本 | 仅长期有条件选项，无运行时云端 LLM |

### 3.1 文献实际支持的内容

**Beam-Stack Search，Zhou 与 Hansen，ICAPS 2005。** 原文讨论 Beam 的不可逆裁剪，并通过受控回溯恢复完整性；其完备／最优结论依赖搜索与启发式等假设及充分工作量。本项目有限预算下不能直接继承这些保证。本文只借其问题定位，不照搬 beam-stack 数据结构。[^R01]

**UCT，Kocsis 与 Szepesvári，ECML 2006。** 原文基于可生成状态转移的规划模型，给出 bandit 树选择及相应收敛分析。对本项目的推断是：有引擎仍不等于有人类未来策略模型；有限次 rollout 的均值也不是“不会死亡”的证书。[^R02]

**ISMCTS，Cowling、Powley 与 Whitehouse，2012。** 原文讨论不完全信息决策以及 determinization 中的 strategy fusion。直接相关的约束是：在获得不同观察之前，各情景不能各自采用不同的本机首动作，再把收益合并。完整 ISMCTS 不是因此就适合当前首版。[^R03]

**多智能体 rollout，Bertsekas，2020，arXiv v2。** 阅读了 sequential consistency／improvement、fortified rollout 的可行 incumbent 思路。本文借鉴“保存可重放的完成方案、不要用未完成推测替换它”；论文的改进保证不自动适用于改变比较周期、受预算截断和不受控真人队友的本项目。[^R04]

**MPC，Rawlings、Mayne 与 Diehl，第二版第一印次。** 阅读版本为 2017 首印、2018 电子下载修订标注的作者公开 PDF，核对了 §2.10 和 §7.4 中在线决策与取控制序列首项的说明。这里仅借作手动滚动规划的类比，不声称游戏建议具备控制系统的稳定性保证。[^R05]

**时间抽象，Sutton、Precup 与 Singh，1999。** options 明确区分启动、内部策略和终止。本文只采用其“长动作必须有有效起止条件”的工程启发，不实现 option 学习或通用框架。[^R06]

**OpenSpiel 实际实现，tag `v1.6.15`。** 阅读 `mcts.py` 的 rollout、`uct_value`、`puct_value`、树策略和主迭代。该实现的默认选择函数是 UCT；PUCT 使用 prior 项，随机 rollout 可以用均匀先验。其顺序博弈／终局奖励与信息处理前提需要适配，不能直接把每位人类队友接成可控树节点。[^R07]

以上资料是算法结构依据。**本项目是否获益由本项目同预算实验决定**，不使用其他游戏或论文的收益数字替代验证。

## 4. 决策对象：立即动作、前缀与条件续行

### 4.1 本次真正交付什么

建议区分三个层次：

| 对象 | 含义 | 对用户的承诺边界 |
|---|---|---|
| 立即动作 | 一次本机合法出牌／用药／当前已知选择，带精确目标与卡牌实例 | 仅对冻结根及动作执行前仍匹配的状态有效 |
| 可执行前缀 | 一个或少数本机动作，执行中没有尚未建模的外部选择和状态变化 | 队友或 RNG 相关状态变化后，后续部分可能失效，需用户手动重算 |
| 条件续行 | 搜索中假定队友未来不主动出牌时，本机后续若干回合的路线 | 用于解释和比较，不是用户必须照做的七周期脚本，也不是控制队友的计划 |

首动作身份不能只有 CardId：必须包含卡牌实例／token、动作种类、目标 combat identity、药水槽位和已有明确的选择内容。重复职业、重复卡牌和同槽位药水不能混成同一个动作。跨次保存仍是纯 `PlanAction` 路线，后续在新根验证 token、费用、目标和可用性。[^C02][^C11]

### 4.2 同首动作的坏后续不能污染所有好后续

设 A 是本机立即动作，存在两个后续 `u`、`v`：`A→u` 以后死亡，`A→v` 在共同周期安全且有效。应将反证记录在 `A→u` 这条路径；最终可以选择 `A→v`，不能将 A 加入全局黑名单。

反过来，若 A 一旦执行就必然触发致死自动结算，且在致死前用户没有另一个决策机会，那么不能用截短的 A 前缀隐藏这个事实。截断到早期安全检查点，仅能表示“改变了后续政策且尚未重新验证”，不能表示已经找到完整安全替代路线。保留旧 checkpoint 和保留旧结论是两件事。[^C05]

两步挑战只增加可被发现的后续，不保证在任何宽度／预算下找到所有安全替代。宽度 1 尤其不能同时保存多种未来；该限制必须写进测试预期。

## 5. 目标、单位与原始观察

### 5.1 保留现行毛扣血账本

令 `R` 为冻结根时本敌方周期已经支付的本机毛扣血，`L_i` 为之后在第 i 个敌方周期归属内新增毛扣血。周期 1 的总损失为 `R + L_1`，随后周期为 `L_i`。每个周期超额为：

```text
excess_i = max(0, cycle_loss_i - 3)
total_excess = sum(excess_i)
```

未完成的当前周期也按已经发生的损失计入风险，不用“还没结束”抹掉。治疗不从 `cycle_loss` 中减去；本机额外玩家回合仍属于当前敌方周期；新玩家回合开始自损按实际敌方周期边界分账。现有 `MultiplayerHpLossBudget` 已实现这些关键分账，不重新发明另一个风险引擎。[^C13][^C15][^C16]

3 HP 是偏好目标，不是无法满足时禁止显示任何建议的硬可行性限制。全路线都超额时，应显示所发现的较低风险路线及超额事实，而不是称其“安全达标”。选药要求等用户明确硬指令另作为资格条件。

### 5.2 观测表与使用规则

| 量 | 单位、归属与观察边界 | 中间探索用途 | 最终路线用途与防重复规则 |
|---|---|---|---|
| 本机实际死亡 | 布尔，当前续行已知事实 | 淘汰无意义继续扩展，保留失败解释 | 高风险优先层；不能被未来输出抵消 |
| 本机救命消耗 | 按玩家、资源类型计数，从冻结根之后开始累计 | 风险分层 | 在获得归属并确认优先级后，独立于 peer 消耗；不能靠治疗值刷奖励 |
| 逐周期本机损失／超额 | HP，含根前本周期已付损失 | 使用现行惩罚与防御通道 | 共同检查点事实，加该具体后续已经出现的超额反证 |
| 队友存活 | 按稳定玩家身份的 alive 事实／人数 | 救援候选与后续风险探查 | 本机风险相同时优先队友存活；“未受击”不是已保住 |
| 队友生命裕量 | 每位队友 HP；最好连同实际下一周期损失 | 诊断／针对性探针 | 首版不加任意高权重，等非对称威胁消融后再决定 |
| 威胁 | 按受害者的原生条件受击 HP／致死／救命触发 | 识别该保留的目标、救援和控制前缀 | 实际结算已经反映的减伤不再次奖励；无完整结算时标估计 |
| 敌方有效生命 | 原引擎 `EffectiveEnemyHp` 单位，逐敌人原始值与总和 | 进攻通道 | 共同边界剩余有效生命；不另累加击杀次数或所有历史伤害 |
| 控制 | 原生 debuff／意图／阶段事实 | 只帮助候选生成或探针选择 | 靠后续真实减伤、存活或输出兑现；不和实际减伤重复计分 |
| 铺垫 | 持续效果、手牌／资源状态；启发值没有 HP 单位 | 保留现行铺垫通道，两步挑战跨越首次低值节点 | 最终不直接加“每层能力分”，靠未来共同边界收益兑现 |
| 本机显式用药 | 从本机动作及原生消费记录核对的次数／种类 | 资格、合法性、资源候选 | 必须用药是资格；可选药是资源代价，不借单人战略 HP 常数 |
| 自动药／队友救命资源 | 按玩家、类型计数，根后事件；根库存是剩余资源事实 | 风险解释 | Fairy 既是自动药又是救命事件，但只能作为一次资源事件解释，不把治疗再算正收益 |
| 长线资源 | 剩余可用药、一次性救命资源、可验证的局内持久状态 | 已有可得信息下的有限探查 | 不把金币、永久收益、名义格挡等擅自换算成 HP；不启用单人局外政策 |

### 5.3 召唤、变身、复活与奖励边界

沿用现有有效敌方生命口径和实际终局判定，不用“累计击杀次数”“累计打掉的血”作为新的正奖励。召唤后敌方总 HP 可以增加，变身／复活也可能改变有效生命；这时终点比较与真实胜利比无界的累计伤害奖励更不易被刷分。本文没有验证所有怪物辅助函数，新的疑似变身误差必须先做原生对账，不能在策略层加补丁公式。[^C09][^C10]

治疗提高实际剩余 HP，但不返还本周期 3 HP 额度。Fairy 恢复的 HP 不算“主动救援收益”；其自动消费和死亡阻止属于同一复合事件的不同视图。若要显示“本场已用多少资源”，还需要真实历史；从当前库存无法反推全部过去消费。首版新计数明确为**根后预测使用**，不伪装成整场总量。

## 6. 批次一致的最终比较

### 6.1 修复发生在“发布候选”入口，而不是所有中间剪枝

建立一个小的不可变批次对象，复用已有 `MultiplayerPlanOrdering`，只增加持有候选和上下文的必要封装：

```text
MultiplayerFinalBatch
    EligibleCandidates       // 完整发布池过滤后的有界候选引用
    RetainedCandidates       // 同一排序器截断后的引用
    Ordering                 // 现有共同周期与 Compare 的不可变绑定
    RootIdentity             // 现有冻结根／solver 实例身份；不新增运行时代际通道
```

可以把它写成现有 partial 内的私有 record，不要求新建公共文件。临时完整池本来就由 `Phases` 构建，不复制模拟器，不保存长期树。

**资格逻辑只有一个权威入口。** 使用 `FinalPlanOrdering` 当前已有政策与原生显式用药计数，避免复制两份强制药水语义。`RequireAtLeastOne`、minimum uses、逐槽强制指令均在发布前的完整池上统一检查。对中间可继续展开的前缀，“现在尚未用药”不等于“最终不可能满足指令”。终局胜利且已无用药机会的路线，则按真实终局和用户指令处理，不假称还能补药。

### 6.2 共同周期保持现行定义，贯通一次决策

第一批不重新设计 `CreateMultiplayerOrdering` 的周期定义，而是正确传递它的结果：正常／horizon 候选的已完成检查点确定共同边界；真实终局和明确外部边界按已有规则处理。固定的比较对象、上下文和比较函数构成一个发布事务。[^C05]

```text
完整发布池
  → 精确资格过滤
  → CreateMultiplayerOrdering(eligible) 一次
  → 使用这个 Ordering 排序并取前 4B
  → 使用这个 Ordering 选中第一名
  → 将同一个 EnemyCycles 放进结果元数据
```

同一事务内，不因为某个候选被裁掉、被过滤或排序改变就再次创建上下文。下一批搜索获得新检查点后可以建立新上下文；这必须是**先确定该批完整入选池和共同目标，再裁剪**，不能追溯性地说上批被裁掉的路线也在新目标下接受了比较。

### 6.3 传递的比较规则

第一批最终比较保留现有维度顺序，只修复上下文生命周期。令 `Facts(n,d)` 是现有函数在固定共同周期 d 的事实；后续已知风险仍取该具体节点的反证。可写成以下字典序，越小越优：

```text
资格：在排序之前过滤，不混成可用小权重抵消的评分

K(n | d) = (
  实际本机死亡,
  不可在 d 比较,
  已知救命消耗,
  已知逐周期超额,
  非真实提前胜利,
  若可比较：
      -队友存活反证值,
       共同边界有效敌方HP,
       共同边界本机毛扣血,
      -共同边界本机HP,
       用药计数,
       双方都胜利时的结束时间,
       动作数,
       规范化动作序列
  否则：
      -现有中间分,
       动作数,
       规范化动作序列
)
```

这里“已知”只来自实际模拟到的事件，不是死亡概率；“救命消耗”第一批仍是当前全局口径，R3 获确认后才替换为本机口径。规范化动作序列只解决相同数值下的稳定重现，包含目标和明确选择；哈希相同还需比较规范内容，不能把哈希碰撞当相等。完全相同的动作和事实允许相等，严格全序建立在等价路线的商集上，不为不同对象地址制造虚假优劣。

固定 d、固定事实、固定分支条件下，这是字典序／全预序；不执行“这一对用 d1、下一对用 d2”的比较。不得把日志、排序名次或派生分塞进战斗状态键。

### 6.4 R3 经确认后的最小优先级变化

明确授权“本机风险先于队友资源节约”后，使用本机死亡、**本机救命消耗**、本机逐周期超额作为风险层；本机风险相同，先比较队友是否存活。队友救命资源消费只在此后作为资源成本，不再自动压过本机超额。

其余输出、毛扣血、剩余 HP 等次序尽量保持，避免一次修改多种偏好。Fairy 自动消费在救命事件层已经表达风险／资源使用时，不再把同一事件当成另一项额外加权惩罚；最终资源 tie-break 应明确互斥分类。资源具体类型可展示，但是否“一个 Lizard Tail 等价于一瓶 Fairy”等换算不在首版设定。

没有身份数据时，不把本机计数默认填成零。保留当前已知的全队字段并标明口径，或阻止发布声称本机归属已验证的新风险说明；不得继续跑 owner-aware 排名再假装数据完整。

### 6.5 各种终止／截止的处理

| 情况 | 可用事实 | 排名与展示 |
|---|---|---|
| 完成普通敌方周期 | 不可变原始 checkpoint | 在本批 d 使用实际事实；多完成周期本身不是收益 |
| 当前回合真实提前胜利 | 原生终局事实，不再需要假造敌方受击 | 按现有终局例外与 `Facts` 判定；不得强行模拟七次空敌方阶段 |
| 胜利发生在共同边界以后 | 较早 checkpoint 和后续真实胜利 | 第一批沿用现行规则，不把共同边界以后的好消息提前奖励；结果可说明条件续行确实获胜 |
| 本机后来死亡／超额／消耗救命 | 具体完整或部分后续中的真实事件 | 保留反证，不退回较早安全点伪装这条路线安全 |
| 等待队友选择 | 边界前的合法状态与已有 checkpoint | 明确为外部条件；不猜选择，不宣称后续验证完成 |
| 未支持语义 | 失败位置与最后可证明事实 | 明确停止；不以“默认成功”替代，不从统计中无声删除 |
| 预算截止 | 已完成上一层与当前已有检查点 | 返回最后可验证建议；证据不足时标条件或未完成，不把探索深度当安全深度 |
| 新根或结果过期 | 当前 stamp 与原 root stamp 不同 | 保留手动交互；不自动重算或执行；旧建议显示过期 |

“精确”仅指已支持路径的原引擎转移，不表示对所有合法策略进行了精确穷举，也不表示当前引擎所有镜像已经通过游戏 DLL 对账。

## 7. 单席位两步前缀挑战

### 7.1 要修的是一处具体断点

原 Beam 可能在动作 A 之后看到很差的中间状态，将它裁掉；但 A 后面合法动作 B 会立即完成一个铺垫组合，使现有持续效果／手牌／输出特征或下一阶段结算显值。挑战让少量这样的 `A→B` 节点越过第一次截断，再交回原搜索，而不是把 A 的虚构“未来潜力”直接加到最终分。

首版只在本次请求的当前本机回合的根动作层 1→2 启用；本次请求最多准入一次挑战，最多登记四个纯动作前缀候选，且**同时最多执行一项挑战、额外保留一个挑战端点**。动作执行所需的瞬时父／子 Fork 仍属于真实成本，计入内存与释放统计，不能据此声称整个进程只多一个状态对象。登记对象不是旧分支引用。挑战从原始合法动作展开中选出尚未被保留首动作覆盖的候选，优先从现有防御／进攻／铺垫通道的被裁代表选取，稳定轮换，不能因为日志中写了“setup”就假定必有收益。

### 7.2 与现有小宽度规则的关系

原 `RankMultiplayer` 的席位数、宽度 1/2/3/4 退化、三通道顺序及重合代表规则保持。挑战不在前沿永久增加第 `B+1` 个保留节点，也不为每个首动作分配 B 个节点。

动作深度 1 被裁掉的前缀，可以在单独的临时席位中做第二个动作。在正常前沿进入动作深度 2 前完成这个有界挑战，再将完成两步的挑战节点加入该层候选，然后**再走原保路规则，仍只保留 B 个**。不得把只走一半的挑战标成完成下一敌方周期；不得为了插入它而覆盖不同阶段的父节点状态。正常前沿已经越过动作层 2 时，关闭挑战窗口并清空尚未启动的纯前缀，不回插旧层，也不重启一次搜索。

宽度 1 时，这相当于对一次原本不可逆剪枝进行少量顺序挑战，不保证同时保有攻击、防御和铺垫三族。宽度 3 的七组既有配额合同仍应通过，因为基础分配算法没有重写；另加一组“同层新增合法双步候选”的合同，说明候选集改变后的行为。

### 7.3 哪些动作可以组成挑战

只用现有合法动作枚举、目标解析、`Replay`／展开与引擎结算。两步都必须是本机动作，费用、目标存活、卡牌实例、选择边界及药水可用性在执行时重新检查，不用代数估算伤害或费用。

首版挑战不跨 `EndTurn`、不跨队友选择、不跨未支持边界；需要本机选牌但不能在这个局部入口复用现有选择展开时，也停止为明确边界，交给普通 Beam 处理，不自造选择器。因此出牌、选牌和目标总体仍由原展开保证，挑战只增强它已有能力中的一小部分。不得把不支持挑战解释为游戏动作非法。

第二步可以枚举多个合法备选，但受同一挑战工作额度限制，逐个释放；不声称对全部两步组合穷举。优先用现有通道次序枚举而不是新建组合模板库。若确实只有少量合法动作且在额度内全部枚举，可将该**局部域**标为完整枚举，不能外推到整个回合。

### 7.4 如何保留 5–7 周期价值

挑战后的节点回到原 Beam，由现有多人阶段推进和 horizon=7 继续扩展。长线能力、抽牌、控制、救援资源仍有机会在第 5–7 个检查点兑现。当前两步只是在提高到达这些状态的概率，不缩短窗口至两回合。

本轮不新增“每点力量等价于若干 HP”的最终尾值，也不在近端失败后用估计宣布七周期达标。远端近似来自有限 Beam 的动作选择；凡真正通过原引擎完成的周期，其 checkpoint 仍是条件下的原始受击事实。没完成的周期保持未知。

如果五／七周期铺垫需要前三四步持续低值，两步挑战可能仍然失败。这是明确的能力上限，不通过扩 Beam 或无限加深挑战来掩盖；收集第一处裁剪边界后，才判断是否有必要做另一个离线搜索原型。可用深度不足时，先返回上一层完整候选，用户手动重算。

### 7.5 攻击目标、救援和用药的实际收益入口

R1 首先保证符合用药要求的路线有资格参与比较。目标和救援仍通过现有合法目标展开，在统一周期下兑现敌方有效生命、队友存活与本机风险。两步挑战可覆盖“先给队友效果、再攻击”“先改变目标耐久、再击杀”“先用药、再出牌”等合法序列，但每种组合都必须用原引擎验证，不承诺所有药水／角色均受该局部入口支持。

按受害者记录的威胁信息先作为诊断：比较“下一周期哪位玩家实际受击／触发救命”。只有固定工作量消融显示原通道选择无法保住相关候选时，才讨论新的中间 tie-break；本轮不直接加“未使用格挡奖励”，也不为等待队友行动提供虚构本机贡献。

## 8. 搜索、预算、取消和返回伪代码

下列名称是对现有 partial 方法的接入草案，不是要求创建同名框架。实际实现优先把小 helper 留在 `CombatBeamSolver.Multiplayer.cs` / `.MultiplayerEvaluation.cs`。

### 8.1 一次手动请求

```text
SolveRequest(request):
    if request.Policy.Multiplayer == null:
        return OriginalSinglePlayerPath(request)  // 原参数、原行为，不经新预算器

    assert request.Trigger == Manual
    assert request.Deploy == false
    root = ExistingMainThreadCaptureWithBeforeAfterStamp()
    session = ExistingGenerationAndCancellationSession(root)
    work = MultiplayerViewOfExistingRequestBudget(session)  // 不是新的一份预算
    incumbent = none
    pendingPrefixes = []  // <=4，只有不可变动作
    challengeStarted = false  // 本次根的动作层 1→2 最多启动一次挑战

    try:
        state = ExistingFrozenRootAndForkInfrastructure(root)
        ReplayExistingPreviousRoutesAtNewRoot(maxRoutes=4, maxNoChoiceActions=32,
                                             work=work)
        while ExistingSearchHasWork():
            CheckCancellationAndGlobalDeadline(work)
            generated = ExistingLocalLegalExpansion(work)

            baseline = ExistingMultiplayerRetention(generated, width=B)
            if (RootFirstActionLayerAndChallengeWindowOpen() and not challengeStarted
                    and TwoStepChallengeIsAdmitted(work)):
                RegisterBoundedDistinctDroppedPrefixes(generated, baseline,
                                                       pendingPrefixes, limit=4)

            // 普通搜索保留 B 个节点；挑战同时最多持有一个额外临时分支
            challenger = None
            if not challengeStarted and ChallengeWindowStillOpen():
                admitted, challenger = TryOneTwoStepPrefix(pendingPrefixes, work)
                challengeStarted = admitted
            if challenger != none and challenger.HasSupportedSecondAction:
                QueueOnlyForMatchingActionDepthAndPhase(challenger)
            elif challenger != none:
                PreserveItsExplicitBoundaryOrFailureEvidence()

            if MatchingNormalLayerReady():
                merged = NormalLayerCandidates + ReadyChallengerAtThisLayer
                frontier = ExistingMultiplayerRetention(merged, width=B)
                ExistingReleaseDroppedSnapshots(merged, frontier)

            ExistingMultiplayerRoundAdvanceAndCheckpoint(work, horizon=7)
            ExistingPreservePreviousCompletedCohortWhenInterrupted()

            if PreviewDueAndEnoughReplayReserve(work):
                batch = PrepareMultiplayerFinalBatch(CurrentPublishablePool())
                if batch.HasEligibleCandidate:
                    candidate = SelectUsingBatchOrdering(batch)
                    verified = ExistingMaterializeAndValidate(candidate, work)
                    if verified.MatchesRootAndSimulatorFacts:
                        incumbent = ReplaceBoundedVerifiedAdvice(verified)
                        PublishManualAdviceOnly(incumbent)

        batch = PrepareMultiplayerFinalBatch(ExistingFinalPoolIncludingFallbacks())
        if not batch.HasEligibleCandidate:
            if incumbent != none and incumbent.IsVerifiedAndEligibleForThisUnchangedRoot:
                return incumbent
            RaiseExplicitPotionPolicyUnsatisfied()  // 不伪造满足要求的候选
        selected = SelectUsingBatchOrdering(batch)
        if CanFinishRequiredValidation(selected, work):
            return ExistingMaterializeAndValidate(selected, work)
        return LastVerifiedAdviceOrExplicitIncomplete(incumbent)
    finally:
        ReleaseChallengeBranchAndPendingPrefixes()
        ExistingReleaseSimulatorOwnershipExactlyOnce()
        ExistingFinishCallbackAndDrainSessionReferences()
```

实际预览若原来只建可重放描述而尚未做完整最终 materialize，不得把它直接改名为 `verified`。只有现有校验确实执行过的对象才可作“已验证 incumbent”；没有足够预算时沿用已有可兑现的回退路径或明确未完成。不能额外偷偷做一次完整重放来获取这个标签。[^C01][^C22]

### 8.2 最终批次准备与选择

```text
PrepareMultiplayerFinalBatch(pool):
    assert IsMultiplayerAdvice
    distinct = ReferenceDistinct(pool)
    eligible = [n for n in distinct if ExistingFinalPotionEligibility(n)]
    if eligible is empty:
        return ExplicitUnsatisfiedBatch()

    ordering = CreateMultiplayerOrdering(eligible)  // 本批唯一一次
    ranked = Sort(eligible, ordering.CompareWithStableEquivalentRouteTie)
    retained = Take(ranked, 4 * B)
    return Batch(retained, ordering, rootIdentity)

SelectUsingBatchOrdering(batch):
    assert batch.RootIdentity is currentFrozenRoot
    assert batch.OrderingWasBuiltBeforeTruncation
    // 不能在这里 CreateMultiplayerOrdering(batch.Retained)！
    return Minimum(batch.Retained, batch.Ordering)
```

预览和最终返回共用这个入口；纯中间保路不要错误调用发布资格过滤。最终批次对被删除模拟器的释放保持原责任，排序 helper 不悄悄取得释放所有权。

### 8.3 两步挑战的局部执行

```text
TryOneTwoStepPrefix(prefixes, work):
    if prefixes empty or ChallengeWindowClosed() or not work.CanAdmitChallengeAfterFinalReplayReserve:
        return (false, none)
    prefix = PopStableNextPrefix(prefixes)
    assert prefix.IsPureActionData and prefix.Actor == LocalPlayer

    branch = none
    bestSecond = none
    try:
        branch = ExistingReplayAtThisRoot(prefix, work)  // 每次重新验证
        if branch.ExternalChoice or branch.Unsupported or branch.IsTerminal:
            return (true, DetachExplicitBoundaryCandidate(branch))

        for action in ExistingOrderedLegalLocalActions(branch):
            if action.EndTurn or action.RequiresUnreusableChoice:
                continue_as_not_admitted_for_this_probe  // 普通 Beam 仍可处理
            if not work.CanAdmitOneMoreRealTransition:
                break
            child = ExistingForkAndApply(branch, action, work)
            bestSecond = KeepBestSupportedSecondOrExplicitBoundary(bestSecond, child)
            ReleaseTheUnselectedEndpointWithoutReleasing(bestSecond)
        return (true, DetachSelectedCandidateWithMatchingDepthMetadata(bestSecond))
    finally:
        ReleaseAllNotDetachedTemporarySimulators()
```

上面的“不纳入这个探针”必须区别于“模拟失败”。不符合探针范围的合法动作不在探针域中；已经尝试却遇到未知语义／异常的候选不能悄悄删除后宣称域已穷举。原异常处理与失败证据继续生效，不添加 catch-all 并返回默认安全值。

### 8.4 同一个预算，不能在 helper 中再启动完整额度

保留原 `Profile.BeamWidth`、`MaxExpandedNodes` 和请求时间参数。多人新增工作只消耗这些约束内的余额。引入的是对既有预算的请求级视图／计数，不是独立 solver profile。

| 工作 | 计费要求 |
|---|---|
| 普通展开、选择、跨阶段推进 | 原展开计数继续；另记录引擎转移／选择／阶段计数以公平实验 |
| 旧路线种子重放 | 维持 4×32 上限；检查全局剩余时间与工作量，不只看局部计时 |
| 两步挑战和 Fork | 从多人请求余额扣除；与普通搜索共用节点上限；记录额外 Fork／释放 |
| 未来任何情景或威胁探针 | 同一计费规则，不允许“只作评估所以免费” |
| 预览、回退刷新、最终路线重放／校验 | 计入墙钟与实际工作计数；开始新挑战前先保留必要校验余量 |

实验起点可将挑战占用设为**剩余可搜索工作量的不超过 15%**，只作多人私有派生上限，不新增用户配置；这是待测工程起点，不是已确认最佳参数。必须在先得到至少一条可解释建议、并保留最终校验余量后才启动挑战。小预算自动不准入挑战，使用回退。

不能把同一次原展开在 `_run.Expanded` 和新计费中重复扣两次；实现应明确哪些引擎调用原来已计数，新增调用在相同边界补计一次。总墙钟从同一个请求起点计算，重放和不可分割阶段并不免费。若原时间预算是软截止，单个引擎调用仍可能越界；不承诺硬实时。截止后只能完成必要的清理／已准入校验，不继续新探索。

### 8.5 取消和 anytime 的明确语义

用户取消时遵循现有 session 的取消和采纳逻辑；不得因新探针完成就回写已经过期的 session。分支执行检查同一 token，释放等待原 worker 与 callback 完成；不在后台留下持续求解任务。新手动请求必须由现有排空／代际机制接管，不复用上一请求活状态。[^C21][^C22]

“anytime”表示有可追溯的最后有效建议，而不是任何半成品都能声称通过七周期验证。建议至少携带共同已完成周期、可执行前缀、根身份、已知超额／救命事实、未完成／外部边界及过期状态。已经完成但明确有风险的候选可以显示风险；没有合法可发布结果就明确未完成，不把零字段凑成安全。

## 9. 有限队友情景：完整研究约束，但不进入本轮默认生产

### 9.1 为什么现在不接入

现有主动动作展开和重放围绕本机 `_player`。能够枚举“给队友一个目标”不等于已经有任意队友 actor 的通用出牌入口。擅自把 `_player` 换成另一个玩家，可能破坏卡牌拥有者、选择、能量、事件锚点和主机身份语义。合法 peer 执行适配及公平信息边界是独立依赖，不能以策略改动名义顺手重写公共引擎。[^C11][^C16][^C18][^C19]

### 9.2 可得信息和合法性

| 信息 | 首版用途 | 情景实验中的约束 |
|---|---|---|
| 队友当前 HP、block、公开 power、阶段、已发生动作 | 当前事实与被动结算 | 可用；动作已发生就应进入新根，不再当随机情景 |
| 当前公开且可确认的手牌／费用／药水 | 仅以实际捕获能力为准 | 只有证明可得且 actor 合法时才生成对应动作 |
| 引擎内部能读到但未证明对本机公开的队友手牌／牌库顺序 | 不声称天然公平 | 不以此默认推演全知最优未来；完整公平屏障另列依赖 |
| 未知牌、未知选择、真人偏好 | 未知 | 不伪造牌、不采样虚构选择、不把不可得内容默认为空或最优 |
| 队友未来是否会行动 | 条件不确定性 | 不要求配合，不把其当可控 planner |

### 9.3 最多三个联合情景，而不是按人数指数展开

影子实验最多使用三个**联合**情景：S0 为当前被动参考；S1 为一个能从可得信息证明合法的代表性队友动作；S2 为一个合法的替代目标／控制／行动次序。每个只插入少量原子动作，不对 2/3/4 人分别做完整动作笛卡尔积。无合适可证明动作时只有 S0，明确“未进行主动情景敏感性验证”。

插入时点必须是原生动作完整结束后的合法边界。至少区分“本机前缀先发生”与“队友在后续动作之间插入”的时序；若队友在第一个本机动作之前已经行动，那是根已过期，不应该还假称旧前缀可执行。

每个情景从相同冻结根 Fork，并让原引擎自然消耗 RNG。行动顺序变化导致 RNG 消耗和目标选择变化时，不重置 RNG 强行配对；相同根种子只说明共同起点，不说明每次随机事件仍是一一对应。

### 9.4 必须共享同一个本机前缀

正确评估对象为：

```text
同一本机前缀 π
    → 情景 s 导致的可观察状态 o_s
    → 只在观察确实不同之后，允许不同条件续行 κ(o_s)
```

不能计算 `平均_s 最优动作_a V(a,s)` 然后称其为一个可执行建议。抽象例子：A 在两个情景价值为 `(10,0)`，B 为 `(0,10)`，C 为 `(6,6)`；逐情景全知最大平均是 10，但同一前缀的最好平均是 C 的 6。这个反例本次已用 Python 执行，数值不是游戏收益或队友概率。[^R03]

实现上还要防止“首动作相同，第二动作在尚未观察到情景差异时已偷偷不同”的更隐蔽融合。共享的是完整承诺前缀，而不只是 CardId。

### 9.5 风险聚合与退化

未经校准的权重不是胜率／死亡概率。影子报告先给逐情景原始事实，以及构造集合内的最坏本机风险；必要时使用固定的 worst-case 字典序汇总，不能将死亡、HP、药水混为无单位均值。构造集合最坏情况也不是全体真人未来的通用最坏情况保证。

默认 S0 的本机 3 HP 风险约束不因某个乐观队友情景而放松。主动情景未完成、合法性不足或成本耗尽时，退化为 S0 并清楚标注缺口，不能静默去掉坏／未完成情景后说“鲁棒”。全部分支、重放、选择和校验计入同一预算。

若将来进入生产，情景 ID、插入时序和可观察信息集应进入多人政策／情景标签及请求缓存键，不塞进纯战斗语义指纹；两个物理相同但承诺前缀／可观察条件不同的样本不能随意合并。当前主方案没有这些字段，避免为尚未启用的情景先建框架。

## 10. 所有权、Fork、键和最小源码切口

### 10.1 新增数据责任表

| 数据 | 产生时点和所有者 | Fork／可变性 | 键与释放 |
|---|---|---|---|
| 最终批次上下文 | 每次预览／最终候选资格过滤后，solver 请求拥有 | 不进入模拟状态；只读 | 不进战斗键或转置；选路完成即丢弃，模拟器释放仍归原调用者 |
| 待挑战纯前缀，≤4 | 当前本机动作截断时，请求内列表 | 复制不可变 `PlanAction` 数据；不保存 simulator／旧 score | 规范首动作只作本请求去重；不新增通用 transposition tag；请求结束清空 |
| 单项挑战及其端点 | 根动作层 1→2 真正准入时，以原引擎 Fork／重放产生 | 与普通候选同样拥有分支状态 | 至多保留一个挑战端点；瞬时父／子 Fork 单独计费，明确转交前沿才 detach；其余 finally 释放 |
| 多人资源观察 | 多人根初始化玩家身份映射，根后实际资源事件派生 | 小规模不可变值／copy-on-write；Fork 共享只读值，写入产生新值 | 观测历史进入多人政策／检查点标签；不重复塞进战斗键；随分支释放 |
| 请求工作计费视图 | 多人请求启动 | 线程安全地使用现有工作计数与全局截止 | 不进任何状态键；不跨次复用，不为每个 probe 重新计时 |
| 已验证建议 incumbent | 现有 materializer 校验成功后 | 纯动作、原始摘要、root stamp；不拥有旧搜索树 | 使用现有结果生命周期；新根仅作为待重放动作，不复用旧评分 |

同首动作识别并不必然要求给每个 SearchNode 添加字段。首版可在极小纯动作列表中计算规范键；本机动作没有改变战斗语义时，现有完整状态／政策标签转置仍然有效。只有未来真要按首动作族独立保留同状态候选，才需要多人专属的探索标签，并单独证明其必要性。

### 10.2 资源观察的最小合法实现

建议在 `SimulatedCombatState.Multiplayer.cs` 中持有一个仅多人创建的 `MultiplayerResourceObservation`。玩家映射使用根捕获的稳定玩家身份，最多四人，不按职业名或列表第一位归属。每位玩家保存根后 Fairy／Lizard Tail 等救命次数和实际药水消费分类；明确 root 时计数从零开始表示“未来预测”，剩余资源直接来自冻结库存。

公共死亡镜像原有逻辑完全保留，仅增加可证明不进入单人的观察分派，例如：

```text
原生镜像完成原治疗／资源消耗
combat.RecordDeathSaveUse(restoredHp)       // 原调用保持原样
if combat.AdvisorPlayer != null:
    combat.RecordMultiplayerDeathSave(actualBeneficiary, actualResourceType)
```

`ConsumePotion` 在其已知 `Player` 的位置追加同类多人观察，而不改原库存索引与消费逻辑。具体方法名以现有两个 `Record...` 入口为准，伪代码不要求合并它们。

计数写入必须值语义、不可变或正确 copy-on-write。**不要把数组放进 record 后误以为默认相等就是逐项相等**；用于政策标签的表示要显式按玩家映射和计数值比较。检查点只存不可变快照。纯资源使用历史不改变战斗效果，不应复制到 `StateFingerprint`；原库存、relic 使用状态等真实战斗语义仍沿现有键处理。[^C06][^C07][^C08][^C20]

### 10.3 预计最小修改面

| 变化 | 优先源码切口 | 真正必要的新类型／复杂度估计 |
|---|---|---|
| R1/R2 发布批次修复 | `.MultiplayerEvaluation.cs`、`.Multiplayer.cs`、`.FinalPlanOrdering.cs`；`Phases.cs` 预览／最终两处显式分派 | 一个私有批次 record 或复用返回 tuple；约 100–200 行生产代码量级，非承诺 |
| 多人资源归属 | `SimulatedCombatState.Multiplayer.cs`、Fork 复制位置、checkpoint／evaluation、两个死亡镜像调用点与 `ConsumePotion` | 一个小的多人观察值对象；约 150–300 行及专门合同；跨公共观测边界，必须单列 |
| 两步挑战 | `.Multiplayer.cs` 的局部 helper；`Phases.cs` 或原展开循环的最少多人 hook；必要时复用 `Expansion.cs` 已有合法动作入口 | ≤4 纯前缀与一个临时 branch，无新 Planner；约 150–300 行量级，视复用入口可达性而定 |
| 测试与证据 | 现有 `MultiplayerEvaluationContracts`、`MultiplayerStrategyContracts`，资源／生命周期测试及转置合同 | 优先扩原合同；必要时一个职责清晰的小测试文件，不建新测试框架 |

行数是审阅尺度的估计，不是已经编译的 patch。若两步挑战需要大范围改写公共展开器才能获得 actor 正确的本机动作，应停止该批并采用回退，不用“代码已经写了一半”作为扩范围理由。

### 10.4 单人严格隔离验收

新预算、新观察、新挑战、新排序批次均只在 `policy.Multiplayer != null` 的显式分派后创建／执行。已有 `policy.Multiplayer == null` 的单人能力入口原条件保持，不把“没有最终改变动作”作为入口隔离证明。[^C12][^C14]

需同时检查源代码差异与运行计数：多人时单人能力／portfolio 进入计数为零；单人时多人 batch／resource observation／probe 进入计数为零，原 profile 参数逐项一致。官方单人 80 项原合同应在相同版本环境重跑，并比对动作、目标、药水、策略路径与关键计数；本次没有独立原版和 DLL，不声称完成了该验收。

## 11. 最小反例、测试矩阵与公平实验

### 11.1 受控规模：24 个核心 fixture，不做全组合爆炸

以下是建议新增／强化的核心集合。已有 fixture 可以复用；同一 fixture 先定位第一处错误边界，再按需要做一到两回合原生对账。只有长线价值项需要完整推进到 5／7 周期。

| ID | 最小输入与第一处错误边界 | 核心断言与验证层 |
|---|---|---|
| F01 | R1：B=1，四条不用药优于一条合格用药路线 | 生产发布管线不能误报空；纯候选合同 |
| F02 | 最少用药数=2，唯一两次显式用药候选排在 4B 之后 | 所有资格谓词一致；纯候选合同 |
| F03 | R2 五候选 `(40,35)…(43,5),(99)` | 截断前后保持本批 d=1，选 A；纯候选合同 |
| F04 | R2 加 X=`(44,1)`，枚举排列 | 同一批不得改 d；独立新批允许另设 d；纯候选合同 |
| F05 | 预览池与最终池包含同样候选和资格 | 两入口同一协议；预览不因 R1 无声丢失 |
| F06 | 本机 0 损／peer Fairy，对照本机 4 损／无 Fairy | owner 记录准确，旧／提案排序差异可见；先合同，后原生 |
| F07 | 两玩家同职业、同槽位同药，交换 local index | 不串玩家、不把自动药当本机显式药；原生最小事件 |
| F08 | 三人：本机、消耗救命的队友、另一濒死队友 | 本机风险与队友存活／资源次序明确；原生 |
| F09 | 根已付 2，旧周期再付 3，新开始自损 4 | 总超额 3、新周期损失 4、最大 5；账本＋两回合事件 |
| F10 | 同周期治疗、手动重算、额外玩家回合 | 不返还／重置 3 HP；原生／根捕获 |
| F11 | 同首动作：一条完整安全续行、一条完整致死续行 | 只淘汰坏续行，不给首动作永久黑标；搜索合同 |
| F12 | 当前前缀已触发必然自损致死，没有中途决策点 | 截短前缀不能冒充安全；原生镜像／显示 |
| F13 | 两步组合第一步三通道均劣、第二步显值 | 第一次动作截断可被挑战跨越；小抽象域＋实际合法卡映射 |
| F14 | 宽度 1/2/3/4 与重合代表，挑战关闭／开启 | 既有七组配额不回归；最终保留不超过 B |
| F15 | 一种两周期显值能力、一种第 5 周期显值、一种第 7 周期显值 | 既有能力不退化；长线完整 checkpoint 才能称验证；窗口不缩短 |
| F16 | 非对称双敌威胁、脆弱队友，当前周期两路同风险 | 找到第一个错误目标／受击边界；原生条件下一周期 |
| F17 | 救援与无效额外格挡各一局 | 同本机风险先保队友；不恢复名义格挡奖励；复用原合同 |
| F18 | 本机选牌、药水给队友触发选择、未支持效果 | 合法局部选择可展开；外部／未知明确停止，不能默认成功 |
| F19 | 4 人含重复职业、不同 local index，含角色特有伙伴／球等已支持状态 | 全队捕获、身份、Fork、阶段正确；按支持能力分项，不假定所有组合已覆盖 |
| F20 | 相同本机前缀，队友动作两种合法插入次序／RNG 消耗不同 | 原生顺序与重放一致；目前可先验证过期，不要求启用主动情景 |
| F21 | 已支持的召唤／变身／死亡阻止各最小一例 | 无累计击杀刷奖励；实际有效 HP／资源归属正确；需 DLL |
| F22 | 4 节点截止、阶段中途截止、挑战开始前／后截止 | 回退上一层；不伪造完成周期，预留校验成本；合同＋计费 |
| F23 | 用户取消／新请求、队友在捕获或计算中行动 | 代际／stamp 检查、无自动重算，worker＋callback 排空，无模拟器残留 |
| F24 | 单人官方基线和 MP 分派计数，加资源账本 Fork／转置双分支 | 单人严格隔离；同物理状态不同资源历史不错误合并；两回合生命周期 |

F21 若需要真正“死后复活”的队友历史，而当前受支持域没有这种路径，应记录不适用／未支持，不能用 Fairy 阻止死亡替代证明。

F19–F21 不用覆盖所有角色 × 所有卡 × 所有人数的笛卡尔积。用正交选择覆盖 2/3/4 人、local 首位／非首位、重复职业、伙伴或球、一次选择／一次自动资源触发；发现失败再沿相关机制扩展。只有一个角色通过不等于全部角色通过。

### 11.2 已有证据与本次新证据的分界

历史七组配额、27 组三候选、九个账本合同、七周期六次原生对账、单人 80 项和 512008 项转置均只按仓库记录引用，没有在本次运行。新执行的是附录 Python 抽象脚本：R1、R2、720 种排列、216 组三元关系、资源排序例子、现行账本算术和共享前缀例子。没有游戏实测、真实胜率或性能数字。[^T01][^T02][^T03][^T04][^T05]

### 11.3 固定模拟工作量与固定墙钟分别比较

**固定工作量组。** 在 harness 对所有算法使用同一截止门，计数实际原引擎动作转移、阶段推进、选择解析、路线重放；至少同时报告这个向量和 Fork 数。不能只看 `ExpandedNodes`，因为某个方法可能额外重放大量节点。可先用一个透明的工作单位 `W = 动作转移 + 阶段推进 + 选择解析`，并列报告各分量；这不是声称三者 CPU 成本完全相等。对极不均匀的局面必须依靠第二组墙钟结果交叉判断。

**固定墙钟组。** 从同一个手动请求／冻结根定义的计时点开始，到首个有效建议及最后可返回结果；所有种子、探针、校验都包含。统一硬件、游戏 DLL、配置、线程设置和 warm-up，记录而不隐藏 GC、重放和取消尾部。可试 **1／3／10 秒**三档，但这些只是实验档位，不是用户已确认的延迟承诺，也不是本次测得性能。

固定工作量回答“同样模拟投入有没有搜到更好的动作”；固定墙钟回答“真实等待是否值得”。不能用多 50% 模拟或多一份 probe 时间后改进的结果宣布策略更优。

### 11.4 指标、消融与质量参照

| 指标 | 定义／注意事项 |
|---|---|
| 首条有效建议时间 | 具备合法动作、根身份、明确证据深度且可重放的第一条建议；不是第一次打印日志 |
| 根动作质量 | 小域用更充分的原引擎枚举作参考，比较同根、同周期的风险／输出字典序；大域无真值时只报告配对结果 |
| 共同周期战损 | 本机各周期毛损失／超额、队友存活、救命资源，不能把不同完成深度直接相减 |
| 威胁／目标质量 | 逐受害者下一周期实际条件受击；不把未校准威胁分当 HP 真值 |
| 药水与资源 | 本机显式、自动救命、队友自动分开；药水约束满足率必须为 100% 或明确无解 |
| 长线价值 | 5／7 周期特定收益是否保路和兑现；未完成明确记作未覆盖，不记作成功 |
| 尾部风险 | 在已测根和情景中出现的死亡、超额、资源耗尽次数及最差值；不外推真实概率 |
| 成本 | 实际转移／重放／Fork、峰值内存、首条建议和总墙钟、取消释放尾部 |
| 回归 | 单人入口／参数／动作、原生状态对账、已知边界、稳定排序和内存释放 |

消融至少分为：固定基线；仅 R1/R2；加归属观察但不改风险序；确认后改本机资源序；最后加两步挑战。其余参数不变。单独记录“好首动作是否生成、是否被保留、最终是否排对、重放是否一致”，避免把镜像错误、剪枝错误和评分错误混成一个胜率指标。

### 11.5 推进门槛与停止门槛

以下为**待实施的工程门槛建议**，不是已测结果：

- 正确性合同、资格满足与单人隔离必须全部通过；任何已知镜像／重放／Fork／取消违例立即停止，不能被平均收益抵消。
- 先收集至少 24 个核心 fixture 和一组约 20–30 个版本匹配的真实冻结根；后者尚缺，不能用人工挑选的组合例子替代自然分布。
- 两步挑战在针对“第一次截断”的失败子集中应确实提高好前缀存活率；同工作量的配对非平局根，建议以改善占比至少 60% 作为初始推进线，同时列出全部风险退步。样本很少时不用这个比例声称统计显著。
- 普通非组合根上首条建议 P95 和峰值内存建议不超过修复后 Beam 的 110%；一旦超出，先降低或关闭挑战准入，不提高总预算。缺少可靠计时环境时不通过性能验收。
- 受控安全 fixture 中不允许增加本机致死／已知超额，也不允许隐藏坏后续。真实样本出现任何新增风险须逐个解释，不能宣称“零观察失败＝零概率”。

**改换算法的触发条件。** 只有 R1/R2 修复、镜像和资格问题排除后，在至少 20 个可重复失败根中，约 30% 以上仍能定位为“好动作始终未通过局部组合裁剪，而不是评分／队友模型错误”，才值得做一个离线 root-action UCT 或更强局部 best-first 对照。新原型必须在固定工作量和墙钟两组中都达到上述质量／回归门槛，并维持原单人隔离，才讨论替换候选生成器。该门槛不是自动合入授权。

**主动情景的触发条件。** 只有已记录错误中约 30% 以上可归因于冻结根后的真人未来行动、且有可证明合法并遵守信息边界的 actor 入口，才启动第 9 节的影子比较；否则不先建情景系统。

## 12. 三批以内实施计划

### 第一批：只修发布候选的资格和共同上下文

**目的：** 解决 R1/R2，不改变风险含义、不重做已有三通道。

**最小源码切口：** `MultiplayerEvaluation.cs`／`Multiplayer.cs` 内批次封装；`FinalPlanOrdering.cs` 的多人资格与选择；`Phases.cs` 预览及最终两处显式多人分派。若保留 `Retention.RankFinal` 的其他调用，明确它不是“已完成发布资格”的证明。

**反例与验收：** F01–F05、固定组传递性、所有排列、空合格池明确失败；加原来的四节点中断与立即胜利例。记录每个批次只创建一次 ordering，过滤前后输入可追踪。单人分支差异审查和入口计数零新增。

**回退：** 单个修复实现有问题时撤回该提交重新实现；不接受恢复错误顺序作为长期方案。后续搜索实验回退时保留本批。

**依赖／不做项：** 纯候选合同不需要游戏语义新接口；实际 harness 仍需现有环境。不改能力政策、威胁权重、队友情景、自动操作、时间或 Beam。

### 第二批：资源归属与风险解释，先数据后排序

**目的：** 解决 R3 的数据缺口，消除“全队资源＝本机风险”的不透明性。

**最小源码切口：** 多人状态观察、Fork／checkpoint／转置标签、死亡镜像与用药调用点的必要多人观察分派；比较器只在优先级确认后改变相应维度。

**反例与验收：** F06–F12、F18、F21、F24 的相关资源项；至少两玩家同槽位、local index 交换、三玩家救援、一条使用资源与一条未使用的 Fork、两回合标签生命周期。单人没有创建或更新多人观察对象，原全局资源调用及参数不动。

**回退：** 若公共观测入口不能在不改战斗语义的前提下提供归属，保留现有全队字段并明确限制，不启用 owner-aware 排名；不通过猜测补零。

**依赖／不做项：** 需要原生资源事件验证和产品优先级确认；本批不改 Fairy/Lizard Tail 治疗、死亡规则、不建资源事件总线，不用学习模型换算资源价格。

### 第三批：单席位两步挑战与同预算消融

**目的：** 改善可定位的低即时收益组合；继续保留七周期原搜索，不增加第二套生产策略。

**最小源码切口：** `Multiplayer.cs` 小 helper、原动作层中的必要多人 hook、现有预算／释放接入。最多四条纯前缀、一个临时分支，完成两步后回到相同动作深度前沿，由原三通道裁到 B。

**反例与验收：** F13–F17、F19–F20、F22–F24；优先第一次裁剪的二步组合、第五／第七周期兑现、普通局面开销、强制药水与选择边界。执行固定工作量／墙钟两组、逐项消融，达到第 11 节门槛才合入。

**回退：** 撤回挑战提交，使用第一批修复后的原三通道 Beam；不留下长期“策略版本选择器”。

**依赖／不做项：** 依赖第一批；第二批未确认的偏好不能偷偷启用。主动队友情景仅保留设计与未来影子实验条件，不在本批建 actor 适配器；不做全局 MCTS、独立尾值模型、宏动作插件系统、全引擎公平信息屏障或单人重构。

## 13. 未决产品选择与越界依赖

| 事项 | 本文建议 | 未获确认前行为 |
|---|---|---|
| 全队救命资源是否可优先于本机 3 HP 超额 | 建议本机死亡／本机救命／本机超额在前，队友存活和资源在后 | 保留当前排序并明确全队口径；先补归属测试，不冒充纯 bugfix |
| 能否为更高胜利概率主动超出 3 HP | 本轮不改变目标 | 沿用逐周期 3 HP；没有概率校准，不新增风险旋钮 |
| 被动参考与少量主动情景发生冲突 | 先作敏感性报告，不让乐观情景放松本机风险 | 只用现有被动参考，不声称最坏情况保证 |
| 第 5–7 周期未完成时如何显示 | 明确已验证共同周期与未验证尾部 | 不显示七周期安全；不自动延长预算 |
| 队友隐藏手牌／牌库的公平信息边界 | 另立公共语义／信息屏障议题 | 主方案不靠新增隐藏信息推演，情景缺信息就不生成 |
| 长期金币、永久成长与药水资源的价值换算 | 暂不引入 | 不借单人政策参数，不创建 HP 等价价格表 |
| 公共镜像若出现原生对账失败 | 先形成独立引擎缺陷证据 | 不在多人评分里补偿错误，不将公共语义改动混入策略批次 |

这些待决事项不阻止第一批完成，也不要求先收集所有游戏资料才能继续源码工作；它们限制的是哪些行为变更可以被宣称已验证／已授权。

## 14. 可直接交给 Codex 的任务

> 以 `5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77` / fork 0.41.1 为唯一行为基线，只实施本设计第一批。先读 `CombatSolver_0.41.1_Review_Conclusions.md` 的 R1/R2，沿 `Phases.PublishRoutePreview`／最终返回 → `Retention.RankFinal` → `RankMultiplayerFinal` → `FinalPlanOrdering.Select` 核对真实调用。补五候选用药空集反例、五／六候选共同周期重建反例及排列测试，使其调用生产多人候选准备与最终选择入口。将发布资格过滤前移到完整发布池，并把一次创建的 `MultiplayerPlanOrdering` 从截断前传到预览／最终选择／结果深度元数据；禁止在子集上重建上下文。中间可继续展开的未用药前缀不得按最终资格一刀切。保留空合格池的明确失败、既有三通道规则、3 HP 分账、七周期、已知坏后续反证及上一层中断保留。公共文件只做多人显式分派，原单人条件、profile 和能力入口不改。同步检查释放所有权与 session 代际，不增加自动操作、独立 Planner、策略版本开关或新预算。提供生产函数合同结果、源代码差异、单人零进入断言和可取得的原合同回归；缺 DLL 的项目标“未运行”，不得捏造。第一批验收通过后，第二批资源归属和第三批两步挑战另提交评审；未确认资源优先级不改默认排序。不要推送、打 tag、发布或修改无关目录。

## 15. 资料版本与实际阅读说明

源码引用均固定到本次提交；本地行号包含空行。ZIP 是主源码渠道，Release/raw 仅作交叉核对。原始论文／实现均实际浏览了与比较结论相关的正文、算法或代码；没有声称完整读完 819 页 MPC 教材。论文 PDF 中涉及算法、图表和公式的关键页使用了页面图像核对。外部资料访问日期均为 2026-09-18；OpenSpiel 固定 tag 不表示“当日最新”。

完整执行边界、归档哈希、实际读取清单与可运行抽象脚本在配套复审文档中。没有游戏 DLL、真实失败包和本地 .NET 环境，本次结果不是编译通过、原生对账通过或胜率改进证明。


### 引用索引

[^C01]: [src/Search/CombatBeamSolver.Phases.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.Phases.cs)，重点：预览 868–934、完成池 1902–1938、最终池与选择 2045–2097、materialize 430–553。
[^C02]: [src/Search/CombatBeamSolver.Multiplayer.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.Multiplayer.cs#L13-L127)，最终排名、三通道与旧前缀重放。
[^C04]: [src/Search/CombatBeamSolver.FinalPlanOrdering.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.FinalPlanOrdering.cs#L32-L54)，多人发布资格过滤、重建排序器及结果深度。
[^C05]: [src/Search/CombatBeamSolver.MultiplayerEvaluation.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.MultiplayerEvaluation.cs#L7-L115)，检查点、共同周期、Facts 和最终比较。
[^C12]: [src/Search/CombatSearchCoordinator.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatSearchCoordinator.cs#L16-L25)；[src/Search/CombatBeamSolver.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.cs#L44-L58)；[src/Search/SearchPolicySnapshot.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/SearchPolicySnapshot.cs)。
[^C10]: [src/Search/SimulatedCombatState.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/SimulatedCombatState.cs)，重点：全队根／Fork 420–444、有效敌方生命 1429–1437；另见 [src/Search/SimulatedCombatState.Fork.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/SimulatedCombatState.Fork.cs#L52-L105)。
[^C13]: [src/Search/MultiplayerSearchPolicy.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/MultiplayerSearchPolicy.cs#L3-L74)，政策 Apply、七周期与 3 HP 账本。
[^C15]: [src/Runtime/CombatRootSnapshot.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Runtime/CombatRootSnapshot.cs#L137-L225)；毛扣血事件另见 [src/Search/SimulatedCombatState.CardEventHistory.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/SimulatedCombatState.CardEventHistory.cs#L180-L210)。
[^C16]: [src/Search/CombatBeamSolver.MultiplayerRound.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.MultiplayerRound.cs#L9-L155)；真实周期递增和检查点调用见 [src/Search/CombatBeamSolver.Expansion.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.Expansion.cs#L3423-L3455)。
[^C18]: [src/Search/SimulatedCombatState.Multiplayer.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/SimulatedCombatState.Multiplayer.cs#L9-L62)，本机身份、额外回合、外部选择边界。
[^C21]: [src/Runtime/SolverController.Multiplayer.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Runtime/SolverController.Multiplayer.cs)；[src/Runtime/ContinuationStamp.Multiplayer.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Runtime/ContinuationStamp.Multiplayer.cs)，过期检查、纯路线保存与全队 stamp。
[^C22]: [src/Runtime/SolverController.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Runtime/SolverController.cs)，重点：手动请求 852–875、多人政策／捕获 1145–1179、取消 1601–1635、部署禁止 1641 起、排空释放 2090–2121。
[^C09]: [src/Search/CombatBeamSolver.StateEvaluation.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.StateEvaluation.cs)，重点：65–113、150–155、507–525、603–618、1573–1607。
[^C11]: [src/Search/CombatBeamSolver.Expansion.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.Expansion.cs)，重点：Replay 2670 起、外部选择 2762–2765、周期推进 3140 起／3423–3455、目标枚举 4392–4425。
[^R01]: Rong Zhou, Eric A. Hansen. **Beam-Stack Search: Integrating Backtracking with Beam Search**. ICAPS 2005. [AAAI 原论文 PDF](https://cdn.aaai.org/ICAPS/2005/ICAPS05-010.pdf)。阅读算法、回溯与保证条件相关段落，并核对关键页面；访问 2026-09-18。
[^R02]: Levente Kocsis, Csaba Szepesvári. **Bandit based Monte-Carlo Planning**. ECML 2006. [大学托管的原论文 PDF](https://www.lri.fr/~sebag/Examens_2008/UCT_ecml06.pdf)。阅读 UCT 算法、生成模型与定理前提；访问 2026-09-18。
[^R03]: Peter I. Cowling, Edward J. Powley, Daniel Whitehouse. **Information Set Monte Carlo Tree Search**. IEEE Transactions on Computational Intelligence and AI in Games 4(2), 120–143, 2012. [作者机构仓储 PDF](https://eprints.whiterose.ac.uk/id/eprint/75048/1/CowlingPowleyWhitehouse2012.pdf)；[作者机构书目记录](https://pure.york.ac.uk/portal/en/publications/information-set-monte-carlo-tree-search/)。阅读信息集与 strategy fusion 相关正文，核对印刷页 123；仓储标注提交版本，访问 2026-09-18。
[^R04]: Dimitri P. Bertsekas. **Constrained Multiagent Rollout and Multidimensional Assignment with the Auction Algorithm**. arXiv:2002.07407, v2, 2020-04-27. [元数据](https://arxiv.org/abs/2002.07407)；[所读 v2 正文](https://arxiv.org/html/2002.07407v2)。阅读 sequential consistency／improvement、fortified rollout 与相关命题；访问 2026-09-18。
[^R05]: James B. Rawlings, David Q. Mayne, Moritz M. Diehl. **Model Predictive Control: Theory, Computation, and Design**, 2nd edition, first printing October 2017；所读电子 PDF 标注 October 2018 下载修订。[作者公开 PDF](https://sites.engineering.ucsb.edu/~jbraw/mpc/MPC-book-2nd-edition-1st-printing.pdf)。仅阅读相关章节／页，不是全文：§2.10、§7.4，印刷页 163–164、468 等；访问 2026-09-18。
[^R06]: Richard S. Sutton, Doina Precup, Satinder Singh. **Between MDPs and semi-MDPs: A framework for temporal abstraction in reinforcement learning**. Artificial Intelligence 112, 181–211, 1999. [大学托管原论文 PDF](https://www-anw.cs.umass.edu/~barto/courses/cs687/Sutton-Precup-Singh-AIJ99.pdf)。阅读 §2 的 option 定义与起止／策略条件，核对印刷页 186；访问 2026-09-18。
[^R07]: Google DeepMind **OpenSpiel**, 固定 tag `v1.6.15`，`open_spiel/python/algorithms/mcts.py`。[实际读取源码](https://raw.githubusercontent.com/google-deepmind/open_spiel/v1.6.15/open_spiel/python/algorithms/mcts.py)。重点：40–78、117–134、194–234、288–432；代码 tag 不表示访问日的最新版，访问 2026-09-18。
[^C19]: [src/Prediction/CardOnPlaySupport.Multiplayer.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Prediction/CardOnPlaySupport.Multiplayer.cs)；[src/Prediction/MonsterMoveEffects.Multiplayer.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Prediction/MonsterMoveEffects.Multiplayer.cs)；[src/Prediction/PotionExecutionSupport.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Prediction/PotionExecutionSupport.cs)。此列表不代表全部多人效果只有这些文件。
[^C06]: [src/Search/SimulatedCombatState.LongTermResources.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/SimulatedCombatState.LongTermResources.cs#L48-L74)，不带玩家归属的死亡阻止资源观察。
[^C07]: [src/Engine/InCombat/Mirrors/Hooks/Death/DeathPreventerMirrors.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Engine/InCombat/Mirrors/Hooks/Death/DeathPreventerMirrors.cs#L8-L69)，实际受益玩家的 Fairy／Lizard Tail 调用。
[^C08]: [src/Search/SimulatedCombatState.Potions.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/SimulatedCombatState.Potions.cs#L10-L138)，使用记录、按玩家库存、消费与补药。
[^C20]: [src/Search/CombatBeamSolver.Transpositions.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.Transpositions.cs)；[src/Search/CombatPlan.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatPlan.cs#L1060-L1100)，节点账本与政策标签。
[^C14]: [tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs#L192-L235)，多人单人能力入口隔离检查。
[^T01]: [tools/OfflineSearchHarness/MultiplayerEvaluationContracts.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/tools/OfflineSearchHarness/MultiplayerEvaluationContracts.cs)，已读共同周期、救援／格挡、铺垫、中断及坏续行相关合同；本次未运行。
[^T02]: [tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs)；[coverage/test-evidence.json](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/coverage/test-evidence.json)，既有策略／账本及结构化历史记录，本次未运行。
[^T03]: [tools/OfflineSearchHarness/MultiplayerContracts.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/tools/OfflineSearchHarness/MultiplayerContracts.cs)；[docs/TEST_MATRIX.md](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/docs/TEST_MATRIX.md)，有限七周期对账和测试范围记录，本次未运行。
[^T04]: [coverage/multiplayer/solo-power-compat.json](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/coverage/multiplayer/solo-power-compat.json) 是 fixture 配置，不是完整原始运行日志；80 项比较来自 [coverage/test-evidence.json](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/coverage/test-evidence.json) 与当前测试说明的历史记录。
[^T05]: [tools/TranspositionFrontierChecks/Program.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/tools/TranspositionFrontierChecks/Program.cs)；[tools/TranspositionFrontierChecks/TranspositionFrontierChecks.csproj](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/tools/TranspositionFrontierChecks/TranspositionFrontierChecks.csproj)；[tools/TranspositionFrontierChecks/README.md](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/tools/TranspositionFrontierChecks/README.md)。512008 为历史检查总数，本次未运行。
