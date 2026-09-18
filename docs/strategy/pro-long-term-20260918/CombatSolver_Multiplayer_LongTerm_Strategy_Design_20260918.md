# CombatSolver 多人长线策略优化设计

**日期：2026-09-18**  
**唯一行为基线：`d55fa84ec07dc252ce62248a9e8b2f4af95effd5`；游戏语义目标 0.111.0。**  
**状态：研究设计。已运行 Python 抽象实验；未运行生产 C#、原生差分或真实联机；本轮不修改仓库、不发布。**

## 1. 决策：先证明丢在哪里，再让一个窗口候选竞争既有席位

**优先主方案：证据门控的多人单席位兑现实验。** 不建立新搜索器，不复制单人能力承诺；先在既有多人 Beam 裁剪点观察真实首次丢失，再在宿主中比较一个有明确兑现窗口的候选与现有铺垫代表。最多替换一个已有席位，总容量和请求预算不变。当前实验 C 有明确机会成本退化，**本轮不支持默认部署 C**。

首批最多一个最小生产改动：有界多人裁剪诊断；一个可撤回实验：宿主内的 C。没有生产输入证明首次丢失位于 Beam 时，不接生产保路消费者。缺乏合法窗口的证据时标 Unknown，不伪造计划，不增加尾值奖励。

**低成本回退：d55fa84 的现役三通道 Beam、原共同周期最终比较与原预算原样运行。** 实验移除后不需要切换玩家设置、不保留新缓存或策略版本。B 只是已有字段消费的消融，不作为无风险回退。

### 1.1 这个决定解决什么，暂时解决不了什么

|问题|主方案的作用|不能承诺|
|---|---|---|
|合法长线前缀到 Beam 才被丢|观察定位后，让已证实有机会的候选竞争既有 setup 槽|不能保证这个候选优于被替换的旧铺垫；F10 已反驳|
|Dark 储值未进入局部估值|采集已有影子球事实并记录消费者窗口|不额外结算一次虚拟激发，不修改引擎|
|星能事实只进部分阶段|区分所有者、库存、已发生事件、实际可花量|不把星能变成通用 HP 分数，不预测真人帮忙|
|父内/选择容量更早剪枝|trace 指明责任阶段，停止错误接线|不在 Beam 点复活已删除路线|
|共同周期/完成池/零完整周期残余|将比较边界和首丢阶段记录为实验背景|本批不改终局政策，不解决全部混合深度问题|
|队友未来主动操作、RNG 变化|维持冻结根条件建议，说明适用边界|不提供最坏情况安全保证或实际联机胜率|

这是一个有部署门槛的具体方案，不是把负结果隐藏后继续推销原方案。若只有 A 稳定通过不可退化哨兵，就保留 A。

## 2. 不变量与可检验的质量目标

### 2.1 产品与技术不变量

本机手动请求时冻结当前全队事实；工作线程只用冻结根与自己的 Fork；本人手动执行。当真人队友已经动作，它属于当前事实；冻结后的未来主动出牌、用药、选牌都不确定。主机/客机和本机在玩家列表中的位置不改变资源归属规则。[S00][S19]

保留每敌方周期本机扣血 3 HP 的目标与七周期窗口。它是风险排序目标，不是“所有超过 3 的路线不可行”。根前本轮已付扣血、自损计入；治疗不退还额度，额外玩家回合不刷新，未用额度不结转。已模拟的死亡、救命消耗、超额和队友死亡不能被早检查点藏掉。[S01][S04]

不改变官方单人路径、参数、评分、战略效果模型、PowerCommitment、保路、转置、能力组合、药水、续用、缓存或自动执行；不取消当前多人门禁。[S07][S08][S10] 不以增加 Beam、节点、时间或内存掩盖机制错误。

### 2.2 质量不是“能力选得更多”

主要质量目标为：**在同一合法冻结根、同预算、同最终政策下，改善可执行本机首动作/前缀对应的已验证共同边界实绩，且没有更高优先级风险退化。**

|维度|记录单位/边界|判定用途|
|---|---|---|
|首动作/可执行前缀|真实 PlanAction、目标、卡实例、选择、当前请求根|区分改了当前建议还是只改变远端条件后缀|
|本机死亡、队友存活|布尔/人数；真实终局或共同检查点，同时保留后续坏证据|安全与救援；不拿存活人数猜具体受击对象|
|本机 HP 损失|HP，逐敌方周期、根已付+根后新增|3 HP 目标、超额与治疗分账|
|全队救命次数|次数，当前实现全队汇总|保留现役优先级；改变 owner 口径/排序另立任务|
|已造成输出|相同边界有效敌方 HP；必要时逐敌 CombatId 辅助解释|确认长线实际兑现，不用名义攻击/储值替代|
|本机显式/自动用药|分开记录次数、归属与指令满足；当前最终政策不改|不为多开能力隐瞒药水成本|
|实际完成周期/比较周期|两个整数，不混用|较深不是自动更好；较早真实胜利可成立|
|搜索成本|expanded、每类 transition、选择分支、回放、扫描/排序、首建议时间、总墙钟、峰值内存|覆盖公平性；不以更快替代质量|
|首次丢失|阶段、父前缀、深度、保留理由、同池替代路线|把修复放在真实责任边界|

F12 必须同时给出“共同检查点相同、当前端点不同、首动作相同”。单独用总动作数或所谓排名改善会把少做工作错算成成功。F13 的真实提前胜利则可以用终局事实比较，不应因为完成周期不同就一律排除。

## 3. 当前证据与本轮 A/B/C 的边界

源码表明：`IgnoreLongTermRewards=true` 没有关闭所有战斗内能力特征；`StrategicEffectModel` 仍被多人快照消费；`FutureResourceValue`/Stars 已进入动作族或牌流，但不是多人 setup 主键；Dark 局部保路只取被动值。[S01][S02][S03][S05][S11][S16][S17]

因此三个对照设计如下：

|变体|本轮实际 Python 实现|生产含义与限制|
|---|---|---|
|A_py|当前多人相关规则的缩减三通道/共同周期适配器|不是 C# 基线实际执行；完整生产 A 仍待宿主运行|
|B_py|A 加已有式 future 字段消费，不加席位|只检测“有事实却未进关键排序”的一部分；系数 20 未校准|
|C_py|A 加一个依当前玩具条件判断的 setup 替换；不叠加 B|合法窗口对玩具语法成立，不能视为原生未来行动证书|
|D|**未实现、未运行、不是本轮提案**|短差分/rollout 仅在算法比较中论证，不据此推荐上线|

完整结果与成本见复审文档及 JSON。本组 17 个主树有 8 个 C 的抽象实绩改善、1 个机会成本退化、1 个同分项假改善/覆盖退化、7 个不变；这是定向反例，不是战斗抽样，更不是胜率。

|fixture|A_py 首动作／敌方 HP／已完周期|B_py|C_py|C 相对 A 的实质结论|
|---|---|---|---|---|
|F01_energy_cycle2|`attack` / 66 / 3|`engine` / 46 / 3|`engine` / 46 / 3|较好（抽象树）|
|F02_scaling_cycle3|`attack` / 66 / 3|`attack` / 66 / 3|`engine` / 63 / 3|较好（抽象树）|
|F03_no_consumer|`attack` / 66 / 3|`attack` / 66 / 3|`attack` / 66 / 3|相同|
|F04_late_self_risk|`attack` / 66 / 3|`attack` / 66 / 3|`attack` / 66 / 3|相同|
|F05_gain_then_spend|`attack` / 66 / 3|`attack` / 66 / 3|`engine` / 62 / 3|较好（抽象树）|
|F06_star_events_block|`attack` / 66 / 3|`attack` / 66 / 3|`engine` / 54 / 3|较好（抽象树）|
|F07_dark_growth_evoke|`attack` / 66 / 3|`attack` / 66 / 3|`engine` / 60 / 3|较好（抽象树）|
|F08_good_bad_suffix|`attack` / 66 / 3|`engine` / 46 / 3|`engine` / 46 / 3|较好（抽象树）|
|F09_immediate_control|`engine` / 30 / 3|`engine` / 30 / 3|`engine` / 30 / 3|相同|
|F10_seat_opportunity_cost|`decoy` / 20 / 3|`engine` / 50 / 3|`engine` / 50 / 3|**退化**|
|F11_parent_cap|`attack` / 66 / 3|`attack` / 66 / 3|`attack` / 66 / 3|相同|
|F12_budget_cutoff|`attack` / 74 / 1|`attack` / 82 / 1|`attack` / 82 / 1|**仅同分项占优；覆盖退化，不能算收益**|
|F13_payoff_cycle6|`attack` / 34 / 7|`engine` / 0 / 6|`engine` / 0 / 6|较好（抽象树）|
|F14_outside_cycle7|`attack` / 34 / 7|`attack` / 34 / 7|`attack` / 34 / 7|相同|
|F15_peer_only_resource|`attack` / 66 / 3|`attack` / 66 / 3|`attack` / 66 / 3|相同|
|F16_external_choice|`attack` / 66 / 3|`attack` / 66 / 3|`attack` / 66 / 3|相同|
|F17_recurring_power_no_future_field|`attack` / 66 / 3|`attack` / 66 / 3|`engine` / 46 / 3|较好（抽象树）|

### 3.1 必须接受的反对证据

**F10：合法兑现机会不足以证明值得抢槽。** 原有 setup 分支同风险实际打得更多，C 抢槽导致敌方 HP=50 而非 20；工作深度与关键模拟数一样。单席位保护不满足“不退化定理”。

**F03：已有 future 不等于有消费者。** B 留住不可能兑现的资源线；分数增加可能只是挤占探索机会。不能把 `FutureResourceValue` 直加数值解释成逻辑修复。

**F12：读事实也占预算。** 小预算下 B/C 少完成一个后续实际攻击；被总动作数同分项奖励，不等于真实建议更好。

**F02/F13：固定两步或两周期并不覆盖所有投资。** 第三周期才回本、第五/第六周期才兑现的树存在。增加短挑战不能证明保住远端，而且挑战/回放挤占主搜索。

**仓库历史反证。** 单人 all-pools 实施记录中，更严格“能力自身因果贡献”的承诺释放曾黏住路线并损害哨兵，后来回退。[S22] W109–124。它不是多人实验，却直接告诫：不要再造一个“直到能力净赚才释放”的长期承诺框架。

## 4. 竞争算法：只选一条近期路线

|方案|可以修复的首次丢失|修不了什么/失败模式|额外成本、内存与取消|本轮结论|
|---|---|---|---|---|
|现有 Beam + 长线事实补齐（B 类）|字段存在但关键中间排序没消费|生成/父内容量、根本没有消费者、多个好铺垫争槽；F03/F10|每候选观察与排序；不能假装免费；无需新树|用作消融，不直接全局加权|
|有界资源窗口替换槽（C 类）|已合法到达 Beam 的延迟候选饥饿|会误删更好旧铺垫；窗口可达性难；F10|同容量，不新增 simulator；需要类型化窗口观察与释放|**唯一近期竞争方案；先宿主实验，当前禁止默认启用**|
|当前回合强搜 + 下一个/两个周期短差分|短期组合在裁剪前有可验证收益|F02/F13 深延迟；完整基线和重放成本；相同首动作坏后缀|额外 Fork/模拟/选择/回放，可能减少主线覆盖|不推荐本轮实现 D；需新的同预算退化试验|
|固定首动作的延迟兑现后验|若已经自然搜索到多个后缀，可汇总哪些首动作得到证据|后缀没生成无从汇总；坏后缀不能否定所有；best-of 样本不等于成功概率|复用已付事实可便宜，但专门补搜等于另分预算|首批只用作诊断分组，不进入最终数值|
|best-first / width novelty|可能穿过启发式平台、保留稀有状态|未知谓词选择、内存边界、旧比较与人类不确定性仍在|开放表与重复处理；取消/排空/回放要重验|没有当前同预算优越证据，不换搜索器|
|UCT / PUCT|可逐步分配模拟给不确定动作|廉价可靠 rollout、先验/值函数缺失；真实队友未知不是采样次数问题|大量重访、树和 rollout 成本；严格风险向量不天然匹配标量|不推荐；本轮没有 MCTS 基准结果|
|信息集搜索 / 合法队友情景|可以研究不同未来信息/行为下同一前缀的稳定性|没有校准队友策略或概率；易 strategy fusion；同 seed 不等于同随机事件|多情景各自模拟，动作/选择合法性与共前缀成本高|保留条件模拟；先用真实失败例证明需要|
|纯加权 / 扩 Beam、节点、时间|可减少一些小宽度损失|不能解决语义/归属、误奖励、未知未来；违反固定预算要求|容量与成本增加；并非等价公平比较|仅 Beam=8 作为抽象机制控制，不用于实施|

### 4.1 原始资料支持什么，不支持什么

[P1] Ng–Harada–Russell 的 reward shaping 工作讨论 MDP 下何种奖励变换保持最优策略；这不意味着给有限 Beam 的某个字段加分仍保留相同候选，更不证明 C 不退化。本项目中间排序不是最终游戏回报，F10 已能打破简单外推。

[P2][P3] 宽度/新颖性搜索研究说明探索与利用、少见状态组合可以在规划中有价值；它支持“分析保路信息丢失”的方向，不给出适用于本项目的 Beam 宽度或自动新谓词，也不能跨过真实合法动作生成问题。

[P4] UCT 的理论环境和渐近性质依赖可进行模拟的决策模型；本机当前有限预算、词典序风险、昂贵选择和真人未知行为没有自动满足全部前提。PUCT 还需要有意义的先验，不能用未校准的能力目录代替。

[P5] ISMCTS 将搜索组织在信息集上，有助于区分隐藏信息与确定状态；它不凭空产生可信的人类行动分布。先分别为每个队友情景选最优本机首动作再平均，会得到不可执行的先知路线；本轮共前缀反例已经检验这一点。

[P6] fortified rollout 的不退化论证要维护一条与当前部分轨迹一致的**完整可行基准轨迹**，并用统一目标替换它；论文也指出其确定性前提。当前被裁剪的短前缀、未完整受击的路线或不同共同周期的候选，不是这种免费基准。两步挑战没有因此获得不退化保证。

这些资料只是限定设计前提；没有一篇论文替代当前项目固定工作量、固定墙钟和单人差分实验。

## 5. 主方案的最小语义模型

### 5.1 不造综合长线分：把“事实”“机会”“结果”分成三层

**事实层**：当前分支已经存在的本机资源、已发生事件、球顺序/储值、合法动作及其已模拟效果。它们有实际单位和 owner。

**机会层**：有限窗口内是否存在一个可说明的本机消费者。它只影响一个既有探索位置，不增加真实输出，不抵消 HP 风险，不改变药水资格。

**结果层**：引擎已经结算的共同周期/终局事实。只由现役最终比较器决定排序；不得叠加储值、未来能量或“能力已开”奖金。

|观察|单位与拥有者|时间截面|允许用途|禁止用途|
|---|---|---|---|---|
|当前能量、星能|整数；本机 PlayerId|当前冻结分支|合法性、消费者缺口|队友资源替本机付费|
|已发生星能 gain/spend|正事件数与实际花费量分开；事件 actor|根后当前事件历史|核对真实触发、诊断|把最终库存平方或重复计算已结算伤害|
|Dark 储值、球位/球序|伤害潜量与队列位置；本机球 owner|当前影子球|记录合法激发/推球机会|直接当敌方 HP 已下降|
|下一阶段自动资源|原始资源单位；事件时点与 owner|尚未结算，必须标条件|窄窗口机会判断|当作已可支付费用或已完成受击|
|能力 generic vector|无量纲启发式；现有模型|当前快照|保留现役通道、诊断差异|跨玩家、跨周期直接折算风险预算|
|敌方有效 HP / 逐敌状态|HP、CombatId|已完成检查点或终局|最终输出/解释集火、过杀|把召唤/复活当额外免费输出奖励|
|HP loss / block|HP、归属于受击玩家|已发生扣血或当前格挡|原风险、实际结算|治疗退还额度；名义格挡重复奖励|

对 RootAlreadyPaid 与 PredictedAfterRoot 分开记载。对于全队救命次数，首批只使用当前权威字段，**不同时改变排序语义**；本机/队友细分观察与产品风险政策应拆开审核。首批并没有得到用户授权让节省队友资源高于/低于本机超额的新规则。

### 5.2 窗口证据只有四种状态

`ReadyObserved`：动作生成器已给出当前可执行本机消费者，且相关动作/选择已按普通搜索计费模拟；这能证明该节点存在一种兑现动作，不证明未来整个战斗安全。

`ScheduledConditional`：当前影子状态包含明确即将发生的自动阶段事件，并有已知可持有/获得的消费者；仍依赖冻结根条件与事件先后。生产首个 C 只考虑能用现有事实严格解释的窄情形，不能对牌堆统计、未知抽牌、保留可能性做乐观补全。

`Unknown`：消费者未生成、未来牌是否存在不能证明、需要额外推演才能判断、事件或版本语义未支持。**Unknown 不是 false，也不是安全；它仍走 A 原通道，不拿 C 特权。**

`Invalid`：本机无法付费、资源属于队友、只有外部选择、已越过七周期窗口、具体路线已经终局或这个兑现机会已用完。移除 C 特权，不按能力名禁止普通搜索。

C_py 有玩具语法提供的 ready_cycle 条件；严格原生窗口观察尚未实现。若收紧到上述证据级别，部分玩具正例可能在生产仍是 Unknown。**本轮未测试这一收紧版本，不把 C_py 的 8 个正例归到它头上。**

### 5.3 不永久承诺、不用净收益归因解锁

首个实验不持久化一个跨七周期的能力承诺对象。当前 live 分支上重算一个小的值观察，当前机会已执行、目标消失、资源不再够、到了截止/外部边界时自然失效。不给“开了能力”发永久保护票，不因为能力还没净赚而续期。

如果生产证据最终要求区分两条战斗状态相同、但保护使用历史不同的路线，必须显式将该**多人政策历史**按值加入多人去重/转置标签，不能塞进战斗语义键。这个需要跨周期 lease 的版本不属于首批。没有证据时，保持当前状态派生观察更简单。[S13][S15]

## 6. 插入点、候选身份与首次丢失诊断

### 6.1 第一批只改一个多人生产位置

允许位置：`CombatBeamSolver.Multiplayer.cs` 中 `RankMultiplayer` 的输入、完成当前去重、最终选择代表之后。[S03] W51–95。复用现有诊断对象/输出功能；每请求有界条数，关闭时不创建，不保留 simulator，不复制所有 Actions 数组，不永久持有被裁剪节点。

需要的诊断值是：请求根标识、当前阶段、节点已有 StateKey、短动作标识/父链序号、动作深度/周期、当前 Score/Persistent/Future/Stars、当次通道角色、是否输入/去重/保留、比较周期。**首批只记录已有字段**；不能为日志先加一次原生反射扫描或额外 Fork。

父内 `ACTION_ADMISSION` 等现有诊断与此 trace 在宿主按前缀关联。若现有父内日志不足以确定第一丢失，报告为“不知道在 Beam 前是否已丢”，不要通过增加第五处生产钩子把首批扩大成框架。

有界日志示意（设计 schema，不是本轮生产输出）：

```json
{
  "request": "frozen-root-id",
  "stage": "multiplayer_beam",
  "node": "existing-node-id",
  "parent": "existing-parent-id",
  "depth": 3,
  "completedEnemyCycles": 1,
  "decision": "pruned",
  "reason": "setup_representative_lost",
  "stateKey": "existing-fingerprint",
  "observation": {"persistent": 1, "future": 16, "stars": 0},
  "evidence": "production_trace_only_after_actual_run"
}
```

### 6.2 同首动作不同后缀不合并成政策标签

根动作身份应包括种类、卡实例/费用状态、目标 CombatId、顺序选择等，不用中文卡名或职业名。诊断按根动作分组有助于展示“同首动作还有好续行”，但策略保留、风险与资格都仍在具体 SearchNode 上判断。

支配和转置保持现役：多人的不同状态键不能套用单人粗摘要支配；HP 账本与检查点仍参与多人政策标签。[S11] W3706–3710、[S13]。不能为保存某个能力前缀禁用转置，或让记录了一个诊断标签就永远无法合并。

### 6.3 C 的竞争顺序：只使用有限代表

仅在宿主实验/后续明确验收后：先按 A 原规则完成资格于对应阶段的处理和去重，再取得 A 的防御、进攻、铺垫代表。C 的候选必须已在这个实际候选池中；不重新生成已被父内裁掉的动作。

默认宽度 1/2 按 A；宽度至少 3 时才研究替换 existing setup 槽，保持 A 的防御/进攻代表与重合不重复占位规则。多个候选竞争时，先证据等级、再最早可解释窗口、最后用当前稳定次序；**不要用尚未发生的伤害作排序**。

这只是实验定义，不是不退化证明。原 setup 可能比窗口候选更好；F10 要求一般性机会成本门禁，否则 C 不能上线。不能为了避开 F10，把它的旧铺垫事后加入单独永久保护席位，也不能暗增第四个槽。

## 7. 具体伪代码

以下是设计伪代码，不是假装可编译的补丁。现役比较、动作生成和战斗结算仍只有一套。

### 7.1 手动请求、冻结和生产隔离

```text
OnManualMultiplayerRequest(liveCombat):              # 主线程
    assert multiplayer and user_initiated
    cancel_previous_request_and_join_existing_workers()
    root = ExistingCaptureAllPlayersAndRng(liveCombat)
    policy = ExistingMultiplayerPolicy(root)         # 3 HP / <=7 周期 / 原预算
    diagnostics = ExistingDiagnostics.IfEnabledForMultiplayerOnly()
    LaunchExistingSolver(root, policy, diagnostics)  # worker 不再读 liveCombat

OnSoloRequest(...):
    return ExistingSoloRequestExactly(...)           # 不创建/读取上面的对象
```

### 7.2 一次普通展开与第一次丢失

```text
ExpandOneParent(parent, request):
    CheckExistingCancellationAndBudget()
    candidates = ExistingLegalActionTargetChoiceExpansion(parent)
    # 每张卡、目标、选择和回合推进继续由唯一引擎模拟、记账。
    paid = ExistingReceiveOnlyAlreadyDispatchedResults(candidates)
    admitted = ExistingParentAdmission(paid)         # 首批不改变
    # 不为多拿“已付结果”读穿 lazy iterator，避免启动未付药水/结束回合。
    return admitted

RankMultiplayerObserved(nodes, limit):
    if diagnostics.Enabled:
        CaptureBoundedExistingFields(nodes, stage="beam_input")
    retained = ExistingRankMultiplayer(nodes, limit, finalQualityFirst=false)
    if diagnostics.Enabled:
        CaptureBoundedDecisionDifference(nodes, retained)
    return retained                                 # 首批输出必须与 A 一致
```

关闭诊断下候选数、排序、节点工作量、任何 RNG、完整输出都应不变。开启日志仍耗 CPU，必须纳入墙钟验证；不能以“只读所以免费”免测。

### 7.3 窄窗口观察与可撤回 C

```text
ObservePayoffWindow(node, existingGeneratedActions, horizon):
    if node is terminal or external-choice or unsupported:
        return Invalid("explicit_boundary")
    if no multiplayer policy:
        fail "must not enter solo"                  # 测试中应为零调用
    facts = current branch-owned immutable values    # 非 live；不额外模拟
    if actor != local_player or no identified consumer:
        return Invalid_or_Unknown_with_reason()
    if candidate has known excess/death/resource adverse evidence:
        return NoSpecialPriority                    # 仍可走原 A，非全局硬禁
    if already simulated legal consumer exists:
        return ReadyObserved(event_boundary, consumer_identity)
    if exact next automatic event + preserved known consumer is proven:
        return ScheduledConditional(event_boundary, dependencies)
    return Unknown                                  # 不能凭卡名/库存猜窗口

RankMultiplayer_C_Experiment(actualPool, limit):
    baseline = ExistingRankMultiplayer(actualPool, limit, false)
    if limit < 3:
        return baseline
    roles = ExistingDefenseOffenseSetupRepresentatives(actualPool)
    eligible = ObserveOnlyAlreadyAdmittedCandidates(actualPool)
    chosen = SelectAtMostOneByEvidenceThenWindowThenStableOrder(eligible)
    if chosen is absent:
        return baseline
    replacement = ReplaceOnlyExistingSetupSlot(roles, chosen)
    FillRemainingSlotsExactlyAsBaseline(replacement, actualPool, limit)
    assert Count(replacement) <= limit
    # F10 表明此处仍可能退化；未通过机会成本门禁只允许宿主运行。
    return replacement
```

观察新增成本无法在现有总预算内完成时，返回基线，不借用新的隐形预算。不能将 Unknown 当“无风险”以给它更大优先级，也不能把一个坏后缀的风险附加给所有同首动作节点。

### 7.4 最终资格、比较、预算停止与返回

```text
FinalizeMultiplayer(all_current_publishable_candidates):
    eligible = ExistingFinalPotionEligibility(all_current_publishable_candidates)
    if eligible is empty:
        return ExplicitPotionPolicyUnsatisfied      # 不偷偷放宽指令
    batch = ExistingPrepareMultiplayerFinalCandidates(eligible)
    # 同一批次固定共同周期、排序、截断、预览、选择；不重建子集上下文。
    chosen = ExistingSelectMultiplayerFinal(batch)
    replayed = ExistingMaterializeAndFinalReplay(chosen)   # 成本计入原请求
    VerifyExistingReplayStateAndBoundary(replayed)
    return ManualAdvice(prefix_now, conditional_suffix, observed_boundary)

OnBudgetOrCancellation:
    StopDispatchOfNewWork()
    ReceiveOrDrainExistingWorkersUnderExistingOwnership()
    # previous 是原实现已有的有界候选组，不新存一棵搜索树。
    pool = ExistingCompletedAndLastBoundedFallback()
    if cancelled:
        ReleaseOwnedSimulatorsAndDropStaleResult()
    else:
        FinalizeMultiplayer(pool)                   # 重放同样收费
```

候选只完成零敌方周期时，应显示缺少完整受击评估，不能把 future 或名义格挡当安全事实。在全池终局时用完整终局事实；外部选择不是胜利；第七敌方周期后不额外开启新玩家回合。[S04][S11]

### 7.5 首条有效建议不以“搜满七周期”为条件

原短搜/有界回退仍负责尽早产生合法建议。新观察不阻塞等待所有长线条件，不为确认一张能力连续占据预算到第七周期。选中的可执行前缀与远端计划分开：当前行动按冻结根合法；后续是队友无主动动作、根状态未失效时的条件路线。过期仍由现有机制处理，用户重新手动请求，不自动重算。

## 8. 最少文件、所有权、Fork、键与释放

### 8.1 分批允许文件，而不是一次全改

|阶段|最少源码切口|不需要做的事|
|---|---|---|
|首批生产观察|`CombatBeamSolver.Multiplayer.cs` 一个多人裁剪观察点；优先复用已有诊断能力|不改 `StateEvaluation`、共享节点构造、政策字段、runtime、状态键|
|首批宿主实验|新 `tools/OfflineSearchHarness/MultiplayerLongTermContracts.cs`，由现有多人合同调用；附带探针供参考|不创建新 CLI 框架或玩家策略版本开关|
|第二批，只有门禁通过|可增加 `CombatBeamSolver.MultiplayerLongTerm.cs` 一个多人分片；若需原始球/事件小事实，`CombatPlan.cs` 添加可空值字段、`StateEvaluation` 仅在明确多人分支赋值|不修改 `StrategicEffectModel` 泛化模型，不调用 solo PowerCommitment；不复制结算|
|第三批验证/收尾|相关多人合同与文档/门禁清单的最小更新|不扩展公共引擎为所有 Mod 提供新注册表|

原始语义若发现真实错误，必须另立公共战斗语义任务，单列单人影响和原生对账，不伪装成多人策略小补丁。

### 8.2 数据对象最小化

首批新增长期对象数量目标为 **0**：诊断是请求内已有 sink 的有界值记录；宿主 C 可消费已知快照和注入的实验数据。第二批确有必要时，最多一个不可变 `MultiplayerLongTermFacts` 值记录，包含本机 owner、当前球/资源摘要、已证实窗口类别和 boundary；不持有玩家/卡对象、simulator、父树或 live 引用。

|数据|产生时点|Fork/共享|键|释放|
|---|---|---|---|---|
|真实球、星能、事件状态|原引擎按普通合法动作结算|原 SimulatedCombatState/Fork 负责；本批不复制另一个权威账本|原战斗语义键，不能把估值混入|现有 simulator 生命周期|
|多人小事实（第二批可选）|snapshot 尚持有自己 simulator 时|不可变值可共享；变更由子快照生成|派生事实不进战斗键、ContinuationStamp、跨请求缓存|随 SearchNode/snapshot 回收|
|当前窗口观察|当前批次现有候选上|首版无跨周期 lease；当前状态派生|无新键；若未来加入历史特权必须单列多人政策标签|本次裁剪后释放|
|首次丢失诊断|输入/输出边界|只复制短值/ID，不抓整条节点对象图|不进入任何策略键|每请求有界，取消完成释放|
|旧路线复用|原纯动作数据，最多既有四路线/32 当前回合动作限制|新根重新验证与模拟|保持原规则，不复用分数或窗口证书|原请求/会话所有权|

[S15] `SimulationSnapshot` 会释放 simulator，[S14] 最终物化/回放仍可能发生。因此不能在最后排序时突然读取已释放的球状态；要么已有事实在快照中，要么只作宿主 live 阶段诊断。不能为了补事实无限保留所有 simulator。

## 9. 预算、并行和取消的完整记账

### 9.1 同预算不是只比 expanded

生产至少分开记：根捕获；普通卡/目标转移；选择链；回合/敌方推进；额外探针（本方案首批没有）；旧路线重放；最终 fallback 物化；最终注释/核验回放；原始事实采集；排序/去重；worker 派发/等待/排空；分配与峰值活跃 simulator。

最终回放在当前 [S14] W405、477–542 是真实路径的一部分。新方案不可把重放移出总计时，不可只在漂亮案例使用更多 wall time。已有正在执行的原子步可能越过软截止，必须报告相同规则下的实际消耗，不能设计新的预算豁免。

### 9.2 固定工作量与固定墙钟两套生产比较（均待执行）

**固定工作量对照**：同冻结根、相同牌/球/事件/RNG、同 DOP、同 MaxExpanded/同其他上限、同最终政策。额外事实读取单列，不把它直接折算成一个 C# expanded。报告相同原预算下第一条建议、首动作、共同周期风险/输出、每阶段成本。不得把更多 completed cycle 当唯一质量。

**固定墙钟对照**：使用用户当前请求预算档，不额外增大。相同构建、运行时、CPU 设置、预热；A/B/C 成对交错顺序，诊断开/关状态一致；保留软截止后 drain 和 replay 全耗时。多次重复报告分布，而非挑最好一次。不要把脚本 5 秒安全上限当这项已经完成。

调试诊断/增量对账可能使当前代码 DOP 变为 1，[S14] W141–146；不能一边 A 并行、一边 C 因校验串行后称其公平。先 DOP=1 定位，再同样 DOP 做正式实验。

**停止/回退阈值。** 单人语义差分、额外自动操作、资源归属错误、未支持选择被猜测、预算上限不再一致，任一出现即停止。F10 同风险更差输出、F12 只有 tie-break 优势却损失验证覆盖，不得拿其他正例抵销。耗时/分配容忍阈值应在实际机器上事先登记；例如首建议 P95 增加 5% 可作待校准研究警戒，不是本轮测得或用户已批准参数。

## 10. 不确定真人队友下的建议语义

当前策略的输出应理解为：“根据此时冻结的全队事实，若之后队友不再主动操作、已有被动和阶段照常结算，这个本机前缀与后续路线的观测如下。”这不是队友会配合、不会抢目标或不会消耗 RNG 的保证。[S19]

Dark 激发目标由结算时状态决定；固定序号敌人的原 HP 不应被永久当成消费者目标。星能和球收益必须按本机 owner；队友提供的格挡/输出只有已经发生，或属于当前模型明确处理的被动阶段事实，才能进入该条件模拟。未观察到的主动救援不能降低本机风险估计，也不应假设队友一定什么都不做而把条件模拟称为最坏情况。

未来若研究情景，必须固定同一个当前可执行本机前缀，并保持每个情景动作合法。无校准权重只能叫压力测试权重，不能叫死亡概率或胜率；不能先在各情景选最优首动作再平均。新增情景不属于这三批实施范围，因为尚无证据证明它比修正保路更值得占预算。

## 11. 最小反例与生产验收矩阵

下表的 A 标签是本轮已运行抽象实验；C/N/M 状态均为待验证。生产 fixture 名称按机制，而不是按每个角色/卡名机械排列。

|fixture / 根因|当前抽象证据|必须追加的生产观测|停止条件|
|---|---|---|---|
|F01 能量引擎第二周期兑现|A/B/C 已运行；A 首 Beam 丢|真实本机合法能力与消费者；保持费/牌/目标；首次丢失 trace|窗口只有名义 future，没有合法消费者|
|F02 成长第三周期回本|已运行；两周期尚落后|相同 3 周期冻结根；逐次伤害与资源、开始阶段时序|用两周期分差错误否定真实第三周期路线|
|F03 永不兑现|已运行；C 无特权|未来能量但无手牌/不能花星能；C 不能因卡名留槽|无限续租或变相多席|
|F04 风险能力|已运行；晚期超额后安全对照胜|CrimsonMantle/集中衰减/结束回合任选一共享根因；逐周期 HP|按名义正收益忽略真实后缀风险|
|F05 星能先获再花|已运行|本机 owner、正事件顺序、支付门槛/费用修正、消费者可达|把队友库存算本机或提前支付|
|F06 事件和花费量|已运行 + 线性上限性质|BlackHole 与 Child 两维事件差分；有效格挡不重复记账|库存平方、事件按星数乘、无效格挡奖励|
|F07 Dark 储值与激发|已运行 + 12/36 状态对|合法已有储球根、队列/集中/推球、实际目标/伤害/RNG|把储值直接放入最终输出或原生语义差分|
|F08 同首动作两个后缀|已运行|同卡实例/目标首动作，安全/危险本机选择分别保留风险|坏续行变成首动作黑名单|
|F09 即时控制组|已运行|真实即时能力自然经现役通道保留，C 特权计数零|为了“长线率”使立即救命/击杀退化|
|F10 席位机会成本|**已运行且 C 退化**|旧 setup 与目标 setup 都合法；完整同预算共同边界|**本轮已经触发；不可默认部署 C**|
|F11 父内容量早丢|已运行|在真实 SelectActionCandidates 前后标记路线；与 Beam trace 关联|首丢父内却只补 Beam 并宣称已修|
|F12 极小预算|已运行；tie-break 假改善|派发/接收/最终回放/旧前缀成本，CP 与动作截面|只靠少动作同分更好就报质量提升|
|F13 5–7 周期铺垫|已运行合成语法；生产窗口未知|真实牌存在性、合法消费者与六周期胜利；终局完整事实|把 toy ready_cycle 当免费 native 证书|
|F14 第七周期之外|已运行|第七次敌方结束不进入下一玩家开始；额外回合计数|偷偷增加第八窗口或额度刷新|
|F15 所有权|已运行合成 peer_stars|2/3/4 人、重复职业、本机非首位；按稳定 ID 查 owner|按列表第一人/职业名归属|
|F16 外部选择|已运行|队友选择/不支持真实语义明确边界；不可默认选项|伪造终局或后台 live 读取|
|F17 generic 与 NextTurn 分别缺口|已运行；B 失败、C 成功|持续能力快照实际有哪些字段，不按目录假设|为了 B 生效把普通能力伪装成另一 Power|
|S0 单人隔离|仅当前源代码/历史合同；未重跑|完整动作、目标、选择、终局、资源、RNG/边界对照，MP入口计数零|任何非预期差异；不得改 expected|
|L0 生命周期|未运行|Fork 值共享/修改、取消、排空、过期丢弃、最终回放释放|泄漏 simulator、读已释放或 live、自动重算|

脚本完整 JSON 已包含所需每次动作和成本。生产三机制最小合同优先分别选 Dark、星能、普通持续能力；不要把 17 个玩具一比一翻译成 17 套昂贵战斗才允许查第一个 bug。共享根因用同一小根的变换覆盖。

### 11.1 单人硬验收

需要同时验证**结构零进入**和**结果零差异**。结构侧在多人事实创建、窗口观察、C 排序、诊断消费者处有测试计数，单人请求均为 0；不能只比较最后 winner 一样。行为侧与固定官方兼容基线/现役独立构建同一输入，比较完整动作、卡实例与目标、选择链、终局状态、资源与 RNG。所有非预期差异立即停止，不能更新期望值接受退化。[S27] 已有隔离合同提供接入范式，但本轮未运行。

## 12. 最多三批实施次序

### 第一批：观察与宿主消融，不改默认建议

**目标。** 把用户感受到的短视映射到真实 first-loss，并验证本轮抽象机制能否在 0.111.0 原生根上出现。

**最小切口。** 一个多人 `RankMultiplayer` 有界观察点；一个宿主文件实现 A/B/C 对照/生产函数探针。所有生产选路仍 A，新增诊断可关闭；没有第二 Planner。

**验收。** 同根三机制实际合法生成/回放；记录父内/Beam 前后、现有字段、最终实绩、完整成本；本轮 Python 全通过；现有多人合同重跑；单人零进入/完整哨兵。F10/F12 专门保留为负例。

**停止与回退。** 不能建立合法原生根、首丢不在计划位置、诊断导致不可接受预算/分配、单人变化，立即停在 A。回退只删除多人诊断/宿主实验，不触碰引擎。

### 第二批：一个多人保路消费点，只有通过门槛才接入

**前提。** 第一批真实失败输入与同预算收益成立，并且 C 在事先登记的旧 setup 不可退化哨兵上不退化；**当前 F10 未满足，因此现在不能进入第二批默认启用**。

**最小切口。** 多人分片内一个窗口观察函数和 existing setup 替换；需要的新事实只由明确 multiplayer 分支生成。宽度/总预算、最终比较、药水、旧路线重放全部保持。没有新的租约/跨请求缓存。

**验收。** 首动作实际改善而不是只改远端；完整共同周期风险/输出；无消费者、风险、即时控制、所有权、外部选择全部稳定。新的事实应能由原生引擎证明，不以 heuristics 掩盖未知。

**停止。** 需要再造尾值、主动队友情景、独立预算、永久能力承诺或按卡名特判才能通过时，停止这个实现，回到 A；不要把它们塞成“第二批小修”。

### 第三批：边界、成本、说明与独立复核

**目标。** 对已通过的唯一多人实现补 5–7 周期、2–4 人身份与 Fork/取消/过期、固定墙钟/工作量两类验证，完成原生差分和有条件的联机试验；不是在本轮研究会话直接实施或发布。

**验收。** 原生状态/选择/RNG 全文一致，实际同预算改进与反例控制都有证据；单人结果不改；所有研究性数字仍有来源标签。联机未完成就继续标未验证，不为了交付进度升格。

**回退。** 保留实验数据和失败条件，撤销多人消费者，保持 A。可关闭或移除诊断，无历史窗口/缓存迁移。

## 13. 当前已运行命令、生产待执行命令与交付文件

已实际执行（Python 3.13.5，标准库）：

```bash
python -m py_compile CombatSolver_Multiplayer_LongTerm_Experiments_20260918.py
python CombatSolver_Multiplayer_LongTerm_Experiments_20260918.py \
  --out CombatSolver_Multiplayer_LongTerm_Results_20260918.json
```

结果首行为 `ABSTRACT_CHECKS_OK fixtures=17 variants=3 runs=51 properties=13`，另有 `BEAM8_CONTROL_RUNS 12`；F12 在 stdout 明确标为 `tiebreak_only_NOT_combat_gain`。完整输出随证据包交付。

生产环境**未运行**。当前仓库指南 [S19] 提供的既有构建和合同入口如下；只可在本地 DLL/引用配置齐备、SHA 确认后执行，不自动安装 Mod：

```bash
git rev-parse HEAD  # 必须等于本文完整固定 SHA
dotnet build CombatSolver.csproj -c Release -p:CopyModOnBuild=false
dotnet build tools/OfflineSearchHarness/OfflineSearchHarness.csproj -c Release
dotnet tools/OfflineSearchHarness/bin/Release/net9.0/OfflineSearchHarness.dll \
  --multiplayer-strategy-contracts --encounter FUZZY_WURM_CRAWLER_WEAK \
  --beam 2 --nodes 100 --budget-ms 1000 --dop 1 --out .local/mp-longterm-baseline
```

另附 `CombatSolver_Multiplayer_LongTerm_ProductionProbe_20260918.cs` 是**待编译宿主探针**。它用当前真实回放/保路/最终批次函数处理宿主给定的合法前缀，含释放和回放成本记录；未运行就没有 C# PASS 结果。它没有伪造新的命令行开关，也没有构造玩家、卡牌和 DLL；已有宿主需提供这些输入。注入前缀池与实际动作生成池必须在结果中分开。

交付还含复审 Markdown、完整 Python、机器可读 JSON、真实 stdout 和文件校验清单。JSON 里的 0 次药水/全队存活人数 2 是本组树背景，剪枝 0 中有未建模项；不得用它们替代药水/联机测试。

## 14. 应保持未决的产品选择

本轮不改变：全队救命次数与本机超额的现役顺序；隐藏信息/RNG 公平政策；是否对队友主动动作做情景；是否显示更多条件说明；任何自动重算或执行。没有校准证据，不给未来收益附胜率，不给各情景伪概率。

只要将来政策选择改变风险容忍或最终比较，就应重新设计 A/B/C 的共同目标，并明确不是本次“多人长线保路修复”。这能防止某次实验看起来更会开能力，其实只是偷偷放宽了本机风险。


## 来源与读取定位

所有仓库链接固定到完整提交，不指向可变分支。**W 表示本次网页原始文本提取器的零起始行号，不是 GitHub 物理源码行号。**提取器会合并部分空行；本轮未取得该提交完整原始字节，因此不伪造 `#L` 锚点。方法名、范围和固定文件链接可共同定位；物理行号的本地核对命令见本文。

|编号|固定文件|实际读取范围|
|---|---|---|
|S00|[docs/strategy/multiplayer-long-term-review-request-20260918.md](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/strategy/multiplayer-long-term-review-request-20260918.md)|本轮完整需求；W0–134；全文|
|S01|[src/Search/MultiplayerSearchPolicy.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/MultiplayerSearchPolicy.cs)|多人政策与逐周期账本；W0–68；全文|
|S02|[src/Search/CombatBeamSolver.StateEvaluation.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatBeamSolver.StateEvaluation.cs)|快照、中间评分与球估值；重点 W145–197、242–579、681–709、895–1472、1517–1544；非全文|
|S03|[src/Search/CombatBeamSolver.Multiplayer.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatBeamSolver.Multiplayer.cs)|多人保路与最终批次；W0–153；全文|
|S04|[src/Search/CombatBeamSolver.MultiplayerEvaluation.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatBeamSolver.MultiplayerEvaluation.cs)|共同周期与最终事实；W0–111；全文|
|S05|[src/Search/StrategicEffectModel.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/StrategicEffectModel.cs)|通用战略效果模型；W0–562；全文|
|S06|[src/Search/StrategicEffectMirrors.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/StrategicEffectMirrors.cs)|战略效果注册入口；W0–116；全文|
|S07|[src/Search/CombatBeamSolver.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatBeamSolver.cs)|搜索器构造与单人能力隔离；重点 W0–97；其余未系统复读|
|S08|[src/Search/CombatSearchCoordinator.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatSearchCoordinator.cs)|协调器多人提前分派；W0–110；其余未系统复读|
|S09|[src/Search/CombatSearchCoordinator.PowerRoutes.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatSearchCoordinator.PowerRoutes.cs)|单人能力路线组合；重点 W0–63；非全文|
|S10|[src/Search/PowerCommitmentPortfolioGate.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/PowerCommitmentPortfolioGate.cs)|单人能力组合入口门禁；W0–12；全文|
|S11|[src/Search/CombatBeamSolver.Expansion.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatBeamSolver.Expansion.cs)|父内候选、动作族与支配；重点 W575–700、1365–1372、3226–3818；非全文|
|S12|[src/Search/CombatBeamSolver.BeamRetentionPolicy.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatBeamSolver.BeamRetentionPolicy.cs)|共享 Beam 的多人分派；重点 W25–39、458–462、2483–2492、3438附近、7271–7274；非全文|
|S13|[src/Search/CombatBeamSolver.Transpositions.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatBeamSolver.Transpositions.cs)|转置与多人历史标签；W0–57；全文|
|S14|[src/Search/CombatBeamSolver.Phases.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatBeamSolver.Phases.cs)|搜索过程、最终物化与回放；重点 W0–583；未完整复读后半轮转循环|
|S15|[src/Search/CombatPlan.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatPlan.cs)|节点、快照与释放；重点 W0–413、415–567、1001–1370；非全文|
|S16|[src/Engine/InCombat/Mirrors/Orbs/DarkOrbMirrors.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Engine/InCombat/Mirrors/Orbs/DarkOrbMirrors.cs)|黑暗球结算镜像；W0–31；全文|
|S17|[src/Engine/InCombat/Mirrors/Orbs/OrbMirrors.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Engine/InCombat/Mirrors/Orbs/OrbMirrors.cs)|球被动与激发值入口；W0–115；全文|
|S18|[src/Engine/InCombat/Mirrors/Hooks/Resources/AfterStarsGainedMirrors.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Engine/InCombat/Mirrors/Hooks/Resources/AfterStarsGainedMirrors.cs)|获得星能事件镜像；W0–49；全文|
|S19|[docs/multiplayer-advisor.md](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/multiplayer-advisor.md)|当前多人指南；W0–176；全文|
|S20|[docs/strategy/pro-review-0412-20260918/implementation.md](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/strategy/pro-review-0412-20260918/implementation.md)|上一轮修复实施记录；W0–48；全文|
|S21|[docs/strategy/pro-review-0412-20260918/local-review.md](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/strategy/pro-review-0412-20260918/local-review.md)|上一轮本地复核与保留条件；W0–127；重点条件项；非逐字全文审计|
|S22|[docs/strategy/power-card-valuation/all-pools-implementation-20260917.md](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/strategy/power-card-valuation/all-pools-implementation-20260917.md)|全部能力池实施与回退记录；W0–138；全文|
|S23|[docs/strategy/power-card-valuation/player-review-20260917.md](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/strategy/power-card-valuation/player-review-20260917.md)|玩家逐卡评审；重点三职业及相关反例；未逐条审计所有角色|
|S24|[docs/strategy/power-card-valuation/regent.md](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/strategy/power-card-valuation/regent.md)|储君能力语义资料；W0–81；全文|
|S25|[docs/strategy/power-card-valuation/defect.md](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/strategy/power-card-valuation/defect.md)|机器人能力语义资料；W0–87；全文|
|S26|[docs/strategy/power-card-valuation/ironclad.md](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/strategy/power-card-valuation/ironclad.md)|铁甲能力语义资料；W0–84；全文|
|S27|[tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs)|生产多人策略合同；W0–307；全文；未运行|
|S28|[tools/OfflineSearchHarness/MultiplayerEvaluationContracts.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/tools/OfflineSearchHarness/MultiplayerEvaluationContracts.cs)|生产多人比较与铺垫合同；W0–271；全文；未运行|
|S29|[tools/OfflineSearchHarness/OfflineSearchHarness.csproj](https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/tools/OfflineSearchHarness/OfflineSearchHarness.csproj)|离线宿主项目；W0–65；读取；未构建|

### 原始算法资料

- **P1**：[Ng、Harada、Russell，1999，Policy invariance under reward transformations](https://people.eecs.berkeley.edu/~pabbeel/cs287-fa09/readings/NgHaradaRussell-shaping-ICML1999.pdf)。论文 PDF，已实际看第 1、3 页截图；不把 MDP 定理外推到有限 Beam。访问日期：2026-09-18。
- **P2**：[Lipovetzky、Geffner，2012，Width and Serialization of Classical Planning Problems](https://nirlipo.github.io/publication/lipovetzky-2012-width/)。作者原始出版页/摘要。访问日期：2026-09-18。
- **P3**：[Lipovetzky、Geffner，2017，Best-First Width Search: Exploration and Exploitation in Classical Planning](https://ojs.aaai.org/index.php/AAAI/article/view/11027)。AAAI 原始论文页/摘要。访问日期：2026-09-18。
- **P4**：[Kocsis、Szepesvári，2006，Bandit Based Monte-Carlo Planning](https://cris.technion.ac.il/en/publications/bandit-based-monte-carlo-planning/)。作者机构的原始出版记录/摘要。访问日期：2026-09-18。
- **P5**：[Cowling、Powley、Whitehouse，2012，Information Set Monte Carlo Tree Search](https://eprints.whiterose.ac.uk/id/eprint/75048/)。作者机构论文仓储/摘要。访问日期：2026-09-18。
- **P6**：[Bertsekas，2020，Constrained Multiagent Rollout and Policy Iteration for Combinatorial Optimization](https://arxiv.org/html/2002.07407v2)。原文 §2–3，尤其 W187–229 的完整可行基准路线与 fortified rollout 前提。访问日期：2026-09-18。

[S00]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/strategy/multiplayer-long-term-review-request-20260918.md
[S01]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/MultiplayerSearchPolicy.cs
[S02]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatBeamSolver.StateEvaluation.cs
[S03]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatBeamSolver.Multiplayer.cs
[S04]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatBeamSolver.MultiplayerEvaluation.cs
[S05]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/StrategicEffectModel.cs
[S06]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/StrategicEffectMirrors.cs
[S07]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatBeamSolver.cs
[S08]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatSearchCoordinator.cs
[S09]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatSearchCoordinator.PowerRoutes.cs
[S10]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/PowerCommitmentPortfolioGate.cs
[S11]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatBeamSolver.Expansion.cs
[S12]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatBeamSolver.BeamRetentionPolicy.cs
[S13]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatBeamSolver.Transpositions.cs
[S14]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatBeamSolver.Phases.cs
[S15]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Search/CombatPlan.cs
[S16]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Engine/InCombat/Mirrors/Orbs/DarkOrbMirrors.cs
[S17]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Engine/InCombat/Mirrors/Orbs/OrbMirrors.cs
[S18]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/src/Engine/InCombat/Mirrors/Hooks/Resources/AfterStarsGainedMirrors.cs
[S19]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/multiplayer-advisor.md
[S20]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/strategy/pro-review-0412-20260918/implementation.md
[S21]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/strategy/pro-review-0412-20260918/local-review.md
[S22]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/strategy/power-card-valuation/all-pools-implementation-20260917.md
[S23]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/strategy/power-card-valuation/player-review-20260917.md
[S24]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/strategy/power-card-valuation/regent.md
[S25]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/strategy/power-card-valuation/defect.md
[S26]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/docs/strategy/power-card-valuation/ironclad.md
[S27]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs
[S28]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/tools/OfflineSearchHarness/MultiplayerEvaluationContracts.cs
[S29]: https://github.com/sgpsdd-zyx/CombatSolver/blob/d55fa84ec07dc252ce62248a9e8b2f4af95effd5/tools/OfflineSearchHarness/OfflineSearchHarness.csproj
[P1]: https://people.eecs.berkeley.edu/~pabbeel/cs287-fa09/readings/NgHaradaRussell-shaping-ICML1999.pdf
[P2]: https://nirlipo.github.io/publication/lipovetzky-2012-width/
[P3]: https://ojs.aaai.org/index.php/AAAI/article/view/11027
[P4]: https://cris.technion.ac.il/en/publications/bandit-based-monte-carlo-planning/
[P5]: https://eprints.whiterose.ac.uk/id/eprint/75048/
[P6]: https://arxiv.org/html/2002.07407v2

## 可直接交给 Codex 的首批任务

只改多人长线策略，原版单人策略保持原样并明确隔离。

以 `d55fa84ec07dc252ce62248a9e8b2f4af95effd5` 和游戏 0.111.0 为唯一输入；本轮交付的 Python/JSON 是抽象证据，不是生产通过。首批仅允许 `src/Search/CombatBeamSolver.Multiplayer.cs` 中 `RankMultiplayer` 的有界多人诊断，以及新 `tools/OfflineSearchHarness/MultiplayerLongTermContracts.cs` 和现有多人合同的一处调用。先复用已有诊断，不为日志新增 simulator、候选、长期树、缓存或状态键；关闭时不创建/读取多人实验对象，生产默认选路保持 A。

在宿主构造 Dark/星能/普通持续能力三种合法冻结根，比较 A/B/C 的同根、同 RNG、同 DOP、同 Beam/节点/时间/最终政策，记录每个阶段第一次丢失与包括选择、扫描、最终回放的总成本。C 只作为可撤回宿主实验：一条已证实有合法兑现窗口的本机候选竞争既有 setup 席位；不叠加单人 PowerCommitment，不把 future/Dark 储值加入最终收益，不为未知窗口作默认值。无消费者、队友所有权、外部选择、超七周期均不享受特权；仍保留原 A 的合法候选与超过 3 HP 时的原回退语义。

禁止修改单人 `StrategicEffectModel`、能力组合/承诺、评分、保路、预算、转置、终局、药水、续用、缓存和自动执行。任何共享文件若在后批确有必要，先提交唯一 multiplayer 显式分派与单人零调用证明；本首批不编辑共享评分。不得增大 Beam/预算、不得建立 Planner/provider/registry、不得重新启用两步挑战或主动队友情景。

验收命令使用上节已确认的 Python、build 和已有多人合同入口；新增宿主探针没有预设新 CLI，须从现有合同调用并记录真实编译来源。补单人完整动作/目标/选择/终局/资源/RNG 哨兵、多人入口零进入。F10 同深度机会成本退化和 F12 小预算证据覆盖是不可退化门槛；当前 C 已触发 F10，因此不得默认接入。任一单人/语义/归属/预算/边界异常，立即停用并回滚本批多人差异，不改 expected、不按 fixture 或卡名特判。所有缺少的 C#/native/真实联机证据保持“未验证”；本任务段不是当前会话修改、推送或发布授权。
