# CombatSolver 0.41.2 深入源码复审结论

**固定提交：`2dc5d15b26f16d89436af0fb98650d8b4cf6b411`。研究日期：2026-09-18。**

本报告接续 0.41.1，但所有当前行为结论均来自 **0.41.2 新目录的附件源码**。R1/R2 的主要修复确已在当前调用链中覆盖，不再把它们原样列为未修问题。本轮最值得先做的不是增加一个搜索器，而是修正终局事实、多人提前停止、根前用药归属等仍会改变答案的入口。上一轮“单席位两步挑战优先”的建议在本轮被撤回。

本报告是研究交付，不是实施授权；没有修改、编译、推送或发布仓库。配套设计见 `CombatSolver_Multiplayer_Strategy_Optimization_0.41.2.md`，可运行抽象实验见随附 `CombatSolver_0.41.2_abstract_checks.py`。

## 1. 按严重程度排列的新增/残余发现

P1 表示应先处理的建议正确性缺口，不表示已经证明普遍实战失败；P2 是具有具体触发边界的覆盖/排序损失；P3 是低优先级的确定性或政策口径。表中“源码＋抽象”绝不等于生产 C#、原生实际/模拟差分或真实联机验证。

|编号|优先级与性质|本轮结论|证据与首个错误位置|
|---|---|---|---|
|F01|P1，事实提取正确性|已获胜路线遇到较早共同检查点，被读取为 `Won=false`；只翻转 Won 仍不够，终局收益字段也会被旧检查点替代。|源码＋A02/A17；`MultiplayerFactsAt` 54–68 行。[C01]|
|F02|P1，共享提前停止|多人关闭了“可接受战损早停”，但仍命中另一条“本机零扣血、满血、零显式用药胜利”停搜条件；该条件没有界住队友存活和全队救命资源。|源码＋A03；`Phases` 1995–2009 行。[C07]|
|F03|P1，条件性输入归属|根前队友用药可能使本机 `RequireAtLeastOne` 被降为 `Smart`；随后 R1 正确过滤的是已经放宽的政策。|源码＋A18；全队历史计数→构造期政策降级。[C09][C10][C11]|
|F04|P2，跨批次保留|已完成池先按自身共同周期压缩，再与较浅的现存候选合并，可能丢失合并目标下最好的路线。不是 R2 的同批上下文重建。|源码＋A04；`Phases` 1909–1920、2071–2093 行。[C06][C05]|
|F05|P2，预算末端接收损耗|最后一个父节点名额可已模拟多个卡牌子候选，却只接收第一个；其余已付成本候选释放。直接把迭代器读到底又会启动尚未付费的药水/结束回合工作。|源码＋A09；串行/并行边界与 `yield/finally`。[C15][C16]|
|F06|P2/P3，证据边界与同分|共同周期之后的额外动作数仍作为最终惩罚；不同动作路径也可能完全同分，不保证跨候选输入排列的唯一结果。|源码＋A05/A06；比较器末尾 114–115 行。[C03]|
|F07|P2，已知资源口径残余|根后救命事件和部分药水观察是全队总量，无法区分本机与队友；可以优先省队友 Fairy 而让本机超额。归属补齐不等于风险政策已获批准变更。|源码＋A08；事件镜像→全局计数→检查点/比较器。[C23][C24][C25][C26]|

没有把“未知续行可能后来坏”“短预算看不到集火收益”“两步未覆盖三步组合”等直接升级为生产 bug。它们在第 4–6 节作为有反例支撑、但仍需可达性和效果验证的搜索局限讨论。

### F01：胜利被较早检查点遮蔽；仅修 Won 标志不充分

**触发前提。** 路线 W 已完成至少一个敌方周期，随后在玩家阶段获胜；候选批次中另一条未终局路线使共同周期 d 落在 W 的历史检查点。`MultiplayerFactsAt` 先找检查点，命中后立即返回 `Comparable=true, Won=false`，来不及进入下方终局分支。实际终局由 `Snapshot.AllEnemiesDead` 标识，比较器却在后面使用被遮蔽的 `Facts.Won`。[C01][C02][C03]

**最小反例 A02。** W 的第 1 周期敌方有效 HP 为 40，之后获胜、当前敌方 HP 为 0；N 未获胜，第 1 周期有效 HP 为 39。两者本机存活、救命次数、超额、队伍存活等相同。合池 d=1 后，当前事实提取把 W 视为未获胜，N 以 39<40 胜出。把真实终局事实放在检查点分支之前，W 胜出。脚本实际得到了 `current_winner=N_nonterminal`、`fixed_winner=W_later_victory`。

**进一步反例 A17。** 两条胜利路线 WA/WB 都有第 1 周期检查点：WA 在回合 2 获胜、旧敌 HP40；WB 在回合 3 获胜、旧敌 HP30。全终局池 d=7 时可比较真实终局，WA 胜出；加入一个无竞争力但只有 CP1 的未终局节点后，当前实现选 WB。即使只把旧检查点分支的 `Won` 改成 true，仍因旧敌 HP30<40 选 WB。只有终局整体事实优先才恢复 WA。这说明修复不是一行布尔替换。

**影响与反驳。** 这不是“立即击杀也一律识别不了”：无旧检查点的立即胜利走终局分支，现有合同覆盖了该情况。也不是“任何胜利都必须无条件胜过未终局”：现有排序把实际死亡、救命资源、超额放在 Won 之前，本轮不替用户改动这些政策。两条示例路线的具体原生可达性仍需 DLL fixture 验证，当前已证实的是源代码分支与其标量后果。[C49]

**最小改动与验收。** 只在多人 `MultiplayerFactsAt` 先处理真实胜利/死亡，使用当前终局 HP、战损、资源、队伍存活和敌方状态；非终局再投影共同检查点。继续保留本具体续行后来已知的坏风险。增加“晚胜利带 CP”“两场晚胜利＋无关浅候选”“立即胜利”“终局有复活/超额”四组实际生产入口合同。不得顺手把 Won 提到风险之前。

### F02：仍有不适用于多人目标的零本机战损胜利早停

**调用链。** `MultiplayerSearchPolicy.Apply` 已将 `StopAtAcceptableBattleHpLoss=false`，相应 `MeetsHpTarget/GrowthTargetSatisfied` 路径不会按用户 3 HP 目标提前结束，这部分是正确的。[C08][C46][C62] 但 `Phases` 另有一条独立条件：无增长目标，完成池里出现获胜、零显式药水、零卖血、本机累计扣血为零、最大 HP 不减且当前满血的路线，就释放 frontier 并停止。这里没有 `!IsMultiplayerAdvice` 守卫。[C07]

**最小反例 A03。** 已发现路线 A 让本机满血无扣血并获胜，但队友死亡；同一搜索尚可发现路线 B，本机条件相同而队友活着。当前比较器在这些本机条件相同时会更喜欢 B，但 A 已足以触发早停。另一种不依赖“胜利时队友尸体是否保留”的抽象变体是：A 消耗队友 Fairy、B 不消耗，本机都没有显式用药和扣血。A 仍满足共享停搜谓词，而全局救命次数使 B 在当前比较器下更优。[C03][C26]

**性质与证据边界。** 已证明早停谓词不是当前多人排序的最优性证书；没有证明某个实际战斗一定存在 B，更没有测量发生频率。原生游戏是否在获胜后统一恢复队友状态，会影响第一个具体 fixture，不能以此忽略第二个资源变体。节点预算耗尽而停止当然不是本问题。

**最小改动。** 保留共享原条件及其单人语句顺序，在入口加显式单人守卫；多人先不增加另一套“差不多够好”的早停。保留正常时间/节点停止与可用预览。这样可能花完原本未用完的预算，属于实际运行时间变化，必须测首条有效建议时间，不能宣传为免费改进。只有取得覆盖所有更高优先级多人维度的可证明界，才能另加多人终止条件。

**验收。** 生产合同使 A 被发现后仍允许 B 到达；A/B 的当前排序关系要来自同一生产比较器。官方单人同一输入的进入次数、展开顺序、动作和结果维持原样。现有两端结构门禁主要检查指定文本/入口存在；“禁用可接受战损早停”这个字符串不能证明其他共享提前停止也隔离了。[C53][C54]

### F03：根前队友药水历史可提前解除本机“至少一瓶”

**数据链。** `BattleDamageTracker.Begin` 记录历史用药总数，`Observe` 在多人模式没有 single player 时仍返回 `PotionsUsedSoFar`；计数是所有 `PotionUsedEntry`，没有 Actor 条件。控制器把这个快照传入请求，多人协调器直接创建普通 `CombatBeamSolver`；其 `_potionPolicy` 初始化见到“配置 RequireAtLeastOne 且 PotionsUsedSoFar>0”就改为 Smart。[C10][C11][C12][C13][C09]

**最小反例 A18。** 在同一追踪区间内，根前只有队友喝了一瓶，本机没有；本机配置 RequireAtLeastOne。根后发布池有干路线 D（显式用药0，敌有效 HP10）和用药路线 W（显式用药1，敌有效 HP30）。全队历史计数1使有效政策成为 Smart，于是正确的 R1 过滤保留 D，D 被选中。按本机历史计数0则仍需用药，只能选 W。脚本显示的差别发生在 R1 **输入之前**，不是“最终先截断后过滤”旧问题复发。

**影响边界与可能反驳。** 若产品明确规定“整个队伍在这段战斗里有人喝过就满足本机开关”，现有结果可符合另一种定义；但当前本机建议与本机药水控制目标没有提供这项团队授权。本报告将它列为应修的本机归属缺口，同时把该语义前提写明。这个例子不声称强制具体药水指令也失效：显式 forced 指令有自己的资格检查。它也不证明自动 Fairy 是否应解除根前 Require；这是另一项尚未明确的政策。

**最小修正。** 在主线程冻结多人请求时捕获本机 Actor 对应的已用数量，供多人政策对象消费；不要修改单人 `BattleDamageTracker` 的既有口径。`CombatReplayOutcome` 已有按 `PotionUsedEntry.Actor == local Creature` 过滤的源内用法，可作为身份定位依据，但不能把“没有动作回执”直接当作“自动用药”。[C14]

必须先固定“从何时算已用”：当前计数有 `Begin` 的历史基线，而不是任意累加整个日志。修复要保留同一战斗/追踪起点的语义，在多人对象捕获该起点的本机计数，或明确采用当前战斗起点并把“Mod 中途接入”的差别作为单列产品变化。不能为修 owner 悄悄重置或扩大统计窗口。

**验收。** 本机端原生 History 是否确实记录队友的该次用药、事件何时入账，仍需 E3/E4 验证；本项的触发前提是该事件已经出现在统计区间。

**验收续。** 同一追踪区间的“队友1/本机0”“队友0/本机1”“强制指定药水仍未完成”“本机自动触发”“手动重算”和“中途开始追踪”分别测有效政策及最终资格。自动触发和中途接入的期望值未定时，必须保留明确待决状态，不用自定默认伪装修复完成。

### F04：已完成池独立压缩导致跨批次目标反转后丢路

**调用位置。** 完成池在 `Phases` 1909–1920 行调用 `PrepareMultiplayerFinalCandidates(completedCandidates).Candidates` 做有界压缩；它只看该池，返回后丢弃这次 Ordering。之后预览/最后发布会与 frontier、预算回退候选合并，按新集合构造新批次。这次新建本身是合理的，但先前删掉的节点已不能复活。[C06][C05][C47]

**最小反例 A04（Beam=1，发布上限4）。** 五条停在外部选择边界的候选都已完成2周期，资源、风险及同分字段相同。其敌方有效 HP 如下。这个标量例使用外部边界避免触发 F01，与胜利遮蔽无关。

|路线|CP1 敌 HP|CP2 敌 HP|
|---|---:|---:|
|A|10|9|
|B|20|8|
|C|30|7|
|D|40|6|
|E|50|5|

完成池独立取 d=2，裁成 E/D/C/B，删除 A。随后加入一个已经存在、CP1 敌 HP99 的普通 frontier S，合池 d=1，压缩池选择 B；完整 A…E＋S 在同一 d=1 则应该选 A。脚本复现了 A 的消失，也显示在**已知同一发布时点**先确定联合上下文再压缩，可保留 A。

**不能过度承诺。** 这不是固定比较器不传递，不是每次添加新候选都不该改名次。新候选提供更浅/更深证据时，比较目标可以合法改变。有界 Beam 不可能为所有尚未出现的目标周期保住所有未来冠军；要求任意增删候选都保持赢家不变也不合理。修复只应保证：一个发布时点中已经可见的候选，不因人为拆成两个局部批次而被不必要地错删。

**最小改动与验收。** 由现有多人批次对象表达“这一时点已知的发布投影视图”：完整可发布集合先过资格，冻结 d；完成池有界压缩使用这一 d，而不是自己重新定 d。可展开前缀仍按原规则保路，不因为未来药水尚未喝而删除。不要建立跨请求 archive、多周期冠军表或扩大 4×Beam 上限。实际 C# 合同需要穿过 `Phases` 压缩→合池，而不只是调用一次 Prepare；新周期产生新批次的合法名次变化另测。

### F05：末父节点“已算未接收”，但不能靠读完迭代器修复

**源内控制流。** `Expand` 的节点计数按父节点增加；一批卡牌动作可以先完成子状态模拟/选择再逐个 `yield`。串行调度接收第一个 child 后检查最大展开数并 break；并行剩余父名额≤1时明确走同样规则。迭代器 `finally` 释放尚未发出的子状态；药水及后续阶段在这些卡候选之后才继续生成。[C15][C16]

**最小反例 A09。** 父节点已模拟 3 个卡候选，拿出第1个后预算检查中断，2个已付成本候选释放。这是接收损耗，不是假定只有“一个孩子就消耗一次父节点预算”。脚本的三子例只复现该控制流；没有声称真实 C# 第3子必然拥有最高最终排名——卡子列表已按中间估值筛选，错失最终更好路线还需要具体 fixture。

**另一处边界。** 节点额度耗尽后 `active` 被释放清空，最后主要靠此前的有界回退与已结束池返回。即使让所有已付成本子候选进入 `active`，也不自动保证它们进入最终候选池。[C17][C18][C05] 这也是本轮反驳“只插入一次二步挑战就好”的工程证据：末端候选是否能被交付，并不等于已经模拟了它。

**最小可研究修复。** 仅在多人路径给**已经完全物化的卡子批次**一个只读的有界接收点，在释放前把必要轻量候选并入现有回退名额；不得启动药水迭代、EndTurn、额外选择展开或新的父节点。与旧回退共用现有容量，不能另建一个额外 Beam。公共 iterator 的单人行为保持原样。

**反对证据与停止条件。** 暴露这个批次若需要侵入式重构、延长分支存活、破坏释放顺序，或只增加排序/重放而没有同预算收益，就不实施。不可简单删除 break 或“全部 drain”；那会把尚未完成的药水和阶段模拟当成免费工作。验收必须观察 `Expand`、接收、finally、active 清空和最终选择的完整链；脚本只证明其中可见的接收缺口，不证明补丁已经可用。

### F06：尾部动作数与同分确定性

**最小反例 A05。** 两条路线在共同 CP1 的所有比较事实相同，A 到 CP1 有2动作、B有3动作；A胜。仅将 A 延长到共同周期之后、未发现新增坏风险且 CP1 不变的4动作尾部，当前最后一项使用总 ActionCount，B 反而胜。注释“不奖励共同深度之外的工作”成立，但实际仍**惩罚**了这类工作。[C03]

“所有安全延长都不能改变任何结果”不是必要公理：有真实资源消耗、超额、队友死亡的新尾部应改变结果。但在已对齐的事实和已知风险都完全相同的前提下，仅因观察了更长安全尾部而落后，是与证据边界不一致的低收益偏置。现有合同故意偏好某些相同 CP 事实下总动作更短者，因此改变它要更新明确的同分语义，不应称为不影响行为的整理。[C49]

**顺序性质。** 固定 d、不可变观测及有限标量下，可以将比较器写成一个按节点计算的字典序键；不存在对每一对另算 d 的问题。本轮24个标量节点、4种 d 的2,304对方向检查与55,296组三元检查都通过。它定义的是允许并列的全预序，而不是每个不同动作路径都有不同名次的严格全序。`List<T>.Sort` 官方文档并不保证相等元素顺序稳定。[P05] 本轮没有模拟 .NET 内部排序，也不宣称当前一定在实际线程交错下产生不同结果。

**最小建议。** 非终局已完成 CP 的最后动作成本用到该 CP 为止的本机动作数；真实终局用到终局，未知前缀用当前前缀。再用现有稳定动作身份（卡实例/选择/目标/药水槽/回合及玩家ID）规范化共同前缀作为确定性兜底；不要用对象地址、日志序号或线程到达顺序。完全相同的共同前缀仍可等价，无需为未知尾部强行定优劣。该项优先级低于 F01–F03，若计算需要额外重放则先不做。

### F07：事件结算所有者正确，不代表多人评分观察有所有者

**完整链。** 药水库存是按玩家和槽隔离的，死亡阻止镜像也检查 Fairy/Tail 的实际 owner，消费与治疗作用于正确玩家。问题在向外观察：`RecordDeathSaveUse` 类记录没有玩家参数，`PredictedPotionUse` 也没有明确 owner；周期检查点写入的是全局 `DeathSaveUseCount` 与 `PotionUses.Count`，最终比较器把全局救命次数放在本机超额之前。[C24][C25][C26][C23][C03]

因此，当前公开证据不支持“喝了队友的库存”“把复活治疗结算给错人”这类严重结算指控。它支持的是**策略所需的归属信息丢失**。通用状态估值中虽有恢复 HP 相关扣项，但多人末段覆盖了中间 Score；不能把此前通用公式直接当成现役多人最终公式。多人中间分仍直接用总药水次数等量，最终观察也仍有总量。[C27]

**A08。** A 本机扣血3、无本机救命消耗、队友 Fairy1；B 本机扣血6、无任何救命消耗，其余相同。当前全队救命次数优先于本机超额，B 胜出。这证明现行字典序表达了一种具体取舍，不证明本机6HP对用户一定不可接受：3HP是目标，死亡避免及资源顺序本来需要明确。

**两项必须分开。** 观察修复：新增多人不可变本机/队友救命次数及恢复 HP，记录显式/自动药水与 owner，保留原全局总量供既有单人和默认策略使用。政策改变：是否让本机超额优先于队友救命消耗、是否仍把本机 Fairy 放在超额前、队友死亡与本机轻微超额谁先，均需要用户决定。主方案只补归属和可解释性，默认排序不借此自动改变。

**验收。** 根前既付不重复计为根后消耗；根后本机/队友 Fairy、Tail、显式本机对队友用药分别记归属；Fork 兄弟不得互相污染；两周期快照不重记同一事件；聚合新计数应与原总数一致。若自动使用历史缺少可靠类型，就保留 unknown，不从药水名或缺少回执猜分类。

## 2. R1/R2 修复覆盖复核：已经修复的部分

`PrepareMultiplayerFinalCandidates` 当前顺序是：按引用去重、检查强制用药（按调用政策）、检查最少显式用药和 RequireAtLeastOne、构造一次 `MultiplayerPlanOrdering`、排序、按 4×Beam 截断，返回包含候选和 Ordering 的 `MultiplayerFinalBatch`；`SelectMultiplayerFinal` 消费这个批次。[C04]

|入口|复核结果|不应混淆的边界|
|---|---|---|
|正常最终选择|最后完整发布池过资格后再固定 d；选择沿用批次。[C05]|F03 是有效政策已在构造时被改写，不是这里过滤漏做。|
|同一次预览|准备批次与选候选沿用同对象。[C47]|不同时间的预览与最终池不相同，不承诺永远同路线。|
|已终止候选压缩|压缩前确实先过滤资格。[C06]|F04 是独立压缩采用局部目标后不可逆丢路。|
|仍可展开的未用药前缀|保留现有中间三通道，没有用最终 Require 将其一律删光。[C19]|以后可能喝药，当前未满足最终资格不等于不许探索。|
|强制药水与自动药水|显式数量与自动触发区分，强制用途有单独判定。[C04][C48]|根前本机/队友和显式/自动的历史口径仍是独立问题。|
|结果深度|最终选择沿用批次上下文，不在 Select 偷换成子集 d。[C04][C05]|“比较上下文是7”不是“这条提前胜利路线已经实挨7周期”。|

现有 `MultiplayerFinalSelectionContracts` 通过实际生产类型/入口构造候选，覆盖排列、三元比较、资格、无合格、强制、自动排除和中间前缀保留；这是有针对性的测试设计。随附资料记录的 **840排列、216三元组**是历史运行事实声明，不是本轮执行结果。[C48][C58] 本轮脚本也运行了独立的840/216标量检查，数字相同并不使它成为那套 C# 测试的重跑。

0.41.1→0.41.2 ZIP 对照为2010→2017文件，新增7、删除0、修改23；行为相关修改集中于最终批次和分派链。这个对照只用于确认修复落点，0.41.1 不是当前缺陷判断依据。

## 3. 共同周期、终局及未知边界：哪些不变性应该成立

### 3.1 当前每个分支的实际含义

|候选状态|CreateOrdering 对共同 d 的作用|FactsAt 当前读取|本轮判断|
|---|---|---|---|
|普通非终局，有 CP|参与普通候选最近 CP 的最小值|有恰好 d 的 CP 则读它|合理的同周期比较。|
|普通非终局，零 CP|没有 CP，不把 d 拉成0|没有 d 证据，Comparable=false|并非已经验证0伤；在全部风险之前比较“是否有可比证据”是现行政策。|
|真正立即胜利/死亡，无 CP|不压低普通 d；全池终局时 d=H|终局当前事实|已覆盖的合理路径。|
|真正晚胜利/死亡，有旧 CP|同上|命中旧 CP 时先读旧事实|F01；死亡还被最前面的 PlayerDead 识别，但终局收益混合仍不应保留。|
|外部选择、未支持等非普通边界|有普通 CP 候选时不拖慢它们；全池受阻则取正 CP 最小值|能读共同 CP 则描述过去；否则未知|不等于以后安全，也不是同一种结局。|
|AdvisoryHorizon|参与普通 d|完成 CP 后可以比较|达到请求上限，不等于获胜。|
|全池真实终局|取 Horizon，默认7|没有匹配 CP7 则读终局；若标量节点自带匹配CP，仍走旧分支，其实际可达性未验证|d 是比较哨兵/上下文，不是实测长度证明。|

A16 复现了“零CP＋CP1”取 d=1、“外部CP0＋CP2”取 d=2，未知节点仍不变成 verified。用户界面和诊断应分开输出 `comparisonCycle`、`actuallyCompletedCycles`、`terminalKind`，不能只显示一个“深度”。[C01][C02][C51]

### 3.2 可以要求与不应要求的性质

同一资格集合、同一事实、同一 d 下的反身、方向一致和传递，是必要的；同一最终批次预览和 Select 使用同一个目标，是必要的；真正终局不被旧 CP 伪装成未终局，也是必要的。添加完全重复引用不应改变结果，删除不合格节点不应先改变目标再返回未合格路线。

但不能要求“增加真实新证据从不改答案”“深入发现本路线致死也不能让它降级”“任意未来候选出现后，有界池必然还能找到全历史最优”。也不能把未知当风险0而声称稳健保证。A04、A07和A17各自对应不同性质，不能合并成一句“比较不稳定”而给同一个粗暴补丁。

## 4. 深搜坏续行与浅前缀：该反驳什么，不该取消什么

A07 构造同首动作 P 的三种状态：浅 P 只见 CP1；P_bad 的续行后来超额2；P_good 的另一续行无该风险。竞争路线 Q 已知超额1。当前浅 P 可以胜 Q，P_bad 输 Q，而 P_good 又能胜 Q。这个结果一部分是获得新证据后的合理修正（并未使用概率模型）：获得坏证据应降低对**这个续行**的评价；另一续行不是同一结论。

真正缺口是输出是否清楚说明浅 P 的**条件性未知尾部**、以及有限搜索是否反复浪费在等价浅路径，而不是比较器应该给所有浅节点加虚构死亡。当前转置键包含多人周期扣血与检查点历史，并不把某一坏续行的事实直接广播给所有相同首动作；跨不同状态也拒绝粗略支配。[C38][C39] 因而没有证据支持新增“坏首动作黑名单”。

本轮优先方案不会添加 `max/mean(all suffixes of first action)` 评分器。`max` 会偏爱尚未查出风险的尾部，`min` 会让可避免的坏选择污染好首动作，未经校准的 mean 也没有概率含义。先明确候选代表“一个具体条件路线＋已验证边界”，记录未展开，不取消现有 later-risk 检查。扩展祖先是否与子路线重复可在诊断记录，不先建长期搜索树。

## 5. 集火、控制、救援与延迟组合的第一处损失

### 5.1 非对称威胁的最小例子 A10

两只敌人各6HP，本机有一次合法6伤攻击。敌人 D 将对4HP队友造成5伤，敌人 Q 本周期不攻击；本机均不受伤。击杀 D 与击杀 Q 后剩余敌方有效 HP 总和都为6、存活玩家数都为2、本机扣血均0。在敌方周期结算前，这些聚合事实不能区分；结算后，杀 D 保留两人，杀 Q 只剩一人。

这是合法动作原语层面的非对称威胁例，不是已经用真实怪物/卡牌名在原生游戏实现的场景。当前源中有目标身份及逐敌信息，也有合法目标展开，不能说引擎不知道目标是谁；问题在 `StateEvaluation` 多人 Score 只使用敌有效 HP 总量、持续增益、手牌可达值、能量、药水和动作成本等，尚未完成周期时可能同分。[C27][C41] 父节点内部 `SelectActionCandidates` 的上限和家族代表可在全局 Beam 之前先删掉正确目标；随后小 Beam 又可能只留一个聚合相同状态。[C28][C19]

因此第一个待测错误点应是**两条合法目标分支是否在完整受击前就被父节点裁剪或 Beam 同分裁掉**，而不是只测最终总分。完成受击后队友存活项本来会区分它们，重新叠加“避免这次死亡”的威胁奖励会重复计分。

### 5.2 是否要加逐敌威胁、队友安全余量或救援可达性

先记录现有数据是否足够解释失败，不先加无量纲权重。可以在测试报告中展示每个玩家实际 HP、预计本已模拟周期的扣血和是否还活着；不能把血量减意图图标当作经过所有状态效果的真实结果。控制对下个敌方阶段的影响应由现有引擎结算；一回合易伤、虚弱、换目标、召唤/复活和阶段切换都可能使简单威胁公式错。

只有在同预算 fixture 证明“正确目标普遍在完整周期之前死于保路”，才考虑用**一个现有席位的替换**保留不同目标/救援前缀，且不加新的搜索宽度。本轮主方案不启用这个改动。救援可达性最少应是“存在一个已生成且合法的本机救援动作”，不是“队友少血所以假定能救”；完整枚举其未来效果仍是模拟成本。已有零效格挡不奖励、相同本机风险先救队友和实际两周期铺垫的合同应继续保留。[C49]

### 5.3 延迟收益不止两步，且卡内选择不是一步免费动作

三通道改善的是已有候选的多样性，不保证首步弱的前缀已经通过父节点分支上限。两步挑战只能挑战“还能拿到、拥有合法状态”的候选，不能复活在 `SelectActionCandidates` 内部就丢失的目标或选择。第二次出牌可能触发多个选择、额外动作或外部玩家选择；旧路线种子也最多复核当前回合4条×32无选择动作，不是完整状态缓存。[C28][C20][C31]

A11 给出了挑战的正例：相同8单位玩具工作（6次搜索边＋2次最终重放），普通 width1 得6，给被弱估的 B 一次第二步得到20。A12 给出了反例：相同6单位工作（包含完整最终重放），普通搜索能看到三步价值10，挑战耗费两个无收益边后只剩两步价值2。**A12 的最终首动作相同**，证明的是深层证据/已找到路线价值退化，不是假装已经观测到首动作胜率下降。A13 的 `[0,0,30]` 还直接反驳“两步足够表达关键铺垫”。

结论不是“两步挑战永远不好”，而是它没有资格在未修 F01–F05、未有真实延迟组合失败包之前占用下一批的默认预算。

## 6. 真人队友、信息与时序边界

当前冻结根包含全队已发生动作与状态，搜索只为本机生成主动动作；未来队友不主动出牌/用药，正常被动、抽牌、受击、敌方阶段及必要的外部选择边界仍发生。这是一个**条件模拟**，不是最坏情况保证。[C31][C32][C33][C55]

|维度|源码支持的结论|不能据此宣称的结论|
|---|---|---|
|本机位置/重复职业|本机由 `LocalContext.GetMe` 与玩家/生物身份定位，stamp 带玩家ID及位置相关状态；不是默认第一个职业。[C33][C34]|不能由一个两人客机 fixture 证明全部2/3/4人、相同职业和所有召唤顺序都正确。|
|队友已经行动|冻结根反映已结算事实；手动重算会捕获新根。|不能把之后 live 队友动作喂回正在搜索的 worker。|
|队友未来主动行动|基线不建模；选择依赖由明确边界停止。[C31][C32]|不能声称是被动模型下最差情况：队友也能消耗共享目标、引发自损/反击或改变 RNG。|
|额外玩家回合|区分参与玩家与真实敌方周期，CP 在敌方阶段完成时才推进。[C30][C31]|不能用玩家回合数字直接重置3HP或把回合开始自损计回上一周期。|
|外部选择/未支持|是明确条件边界，不是无风险终局。|不能把拒绝支持的情况统一叫作“悄悄算错”，也不能忽略已经支持路径中的事实缺口。|
|目标失效/RNG|本机计划依赖当时对象和冻结序列；队友主动动作可能改变后缀合法性或随机消耗。|原随机种子相同不等于行动交错后的随机事件耦合相同。|
|取消/过期|多人入口只允许 Manual、不自动部署；前 worker/回调排空后释放，完成建议可被标记过期。[C35][C36][C37]|离线 NetType.Host/Client 替身不是两台机器实测。[C52]|

被动假设可使本机过度防守、过早救援、保留无需保留的药水，也可能低估与真人配合的长期铺垫；主动队友又可能破坏原目标和组合，所以不能只沿“队友一定会帮忙”作单向乐观修正。最便宜的改进是明确建议适用范围与当前可执行前缀，并沿用手动重算/过期提示，而不是默认添加不可信情景权重。

隐藏牌序/RNG 公平信息政策属于另一个产品与引擎信息屏障问题。本轮没有据此重写引擎，也没有利用未给出的游戏 DLL 推断未知信息支持。

## 7. 保持原样的合理设计

现有唯一内嵌引擎、主线程冻结根、Fork、合法动作/选择展开和统一预算是正确起点；另起“轻战斗预测器”会制造双重语义。官方能力政策入口显式要求 `policy.Multiplayer == null`，协调器多人分派先返回；必须继续维持零进入，不能用最终动作恰好相同替代隔离证明。[C13][C45]

每敌方周期的扣血账本保留根前已付、自损和跨周期分账，治疗不抵消已支付额度；周期完成点早于下一玩家阶段的新增损失。A14 独立检查3+3、0+6、根前2＋自损2＋治疗以及前周期3/下一阶段2的差别，不构成原生语义全覆盖。[C29][C30][C33]

小 Beam 三通道、不同宽度规则、零效格挡不凭名义分获奖、已知坏续行不藏回安全 CP，以及纯动作旧路线按新根重放，均值得保留。Fork 共享不可变 CP、多人历史加入政策标签而非把派生估值塞进战斗键，也是应延续的方向。[C19][C20][C38][C43][C59]

## 8. 本轮证据清单与未验证范围

### 8.1 实际完成的层级

|层级|本轮状态|精确定义|
|---|---|---|
|E0 源码/归档事实|完成相应调用链阅读|来自固定 ZIP；不代表2017个文件逐行审计。|
|E1 抽象 Python|实际运行18项；输出随附|部分函数是标量分支移植，部分是独立玩具树/控制流。不会执行引擎。|
|E2 生产 C#|未运行|环境没有 `dotnet`，没有游戏 DLL；不能把 Python 数字叫生产合同通过。|
|E3 原生实际/模拟差分|未运行|没原生程序集和本次失败战斗包；历史 fixture 源码/证据只作已存在覆盖参考。|
|E4 真实联机|未运行|无两端实机、真人行动交错和网络数据。|
|H 优化收益|仍为待验证假设|没有声称胜率、延迟、内存或建议质量已经实测改善。|

源码中的历史证据包括最终管线、多人战损/铺垫、七周期对账、单人对照等。既有覆盖对两人场景和受控宿主很有价值，但不能外推成完整战斗或所有队伍排列；测试里专门提高队友/敌人HP、替换 Godot/网络入口，正说明其证据边界。[C48][C49][C50][C51][C52][C58]

### 8.2 实际读取与未读取清单

以下“完整”指完整阅读列出的短文件/相应专用文件，不是声称所有依赖均已逐行读完。`CombatSolver_0.41.2_read_ranges.jsonl` 记录后续行号展开，早期整文件读取不全在此日志；日志也不等于覆盖率工具。

|范围|实际读取|
|---|---|
|需求与文档|完整读取本轮 request；完整当前 `docs/multiplayer-advisor.md`；`ARCHITECTURE.md` 1–80及其中多人章节；`TEST_MATRIX.md` 当前多人1–77行；结构化证据的当前多人/单人对照条目；`coverage/multiplayer/solo-power-compat.json`。|
|完整多人搜索分片|`CombatBeamSolver.Multiplayer.cs`、`.MultiplayerEvaluation.cs`、`.MultiplayerRound.cs`、`MultiplayerCycleCheckpoint.cs`、`MultiplayerSearchPolicy.cs`、`.Transpositions.cs`。|
|共享搜索的定点读取|`Phases.cs` 的根/计时、资格、incumbent、串并行展开、保留、完成池压缩、提前终止、预览、最终合池和重放；`Expansion.cs` 的父计数、合法/选择展开、子批迭代、终局/CP、父内裁剪与支配；`Retention.cs` 1–115的刷新回退；`Terminal.cs` 25–215；`StateEvaluation.cs` 1–235、479–535、597–634、1558–1615；`BeamRetentionPolicy.cs` 多人分派470–489、2580–2610、7557–7582；`FinalPlanOrdering.cs` 1–120；`CombatBeamSolver.cs` 1–142；`SearchPolicySnapshot.cs` 30–66。|
|节点/协调器|`CombatPlan.cs` SearchNode、SimulationSnapshot、释放与 SelectedSearchPlan（1060–1124、1210–1320、1340–1400）；`CombatSearchCoordinator.cs` 1–87多人分派。未把其余单人策略当成研究修改范围。|
|完整数据写入短文件|`SimulatedCombatState.Multiplayer.cs`、`.LongTermResources.cs`、`.Potions.cs`、`DeathPreventerMirrors.cs`；`SimulatedCombatState.cs` 329–355、420–449、1424–1468等库存/Fork/有效HP相关位置。|
|运行时|完整 `SolverController.Multiplayer.cs`、`ContinuationStamp.Multiplayer.cs`、`BattleDamageTracker.cs`；`CombatRootSnapshot.cs` 137–275冻结；`SolverController.cs` 846–889、990–1015、1110–1168、2060–2133；`CombatReplayOutcome.cs` 40–83。|
|测试和边界|完整 `MultiplayerFinalSelectionContracts.cs`、`MultiplayerEvaluationContracts.cs`；`MultiplayerStrategyContracts.cs` 1–130；`MultiplayerContracts.cs` 1–128；`MultiplayerStartContracts.cs` 1–60；两端结构门禁当前多人段。[C48]–[C54]|
|Prediction 短接入|完整 `MonsterMoveEffects.Multiplayer.cs`、`CardOnPlaySupport.Multiplayer.cs`，用于核对多人语义接入，不据此声称所有镜像内容已验证。[C60][C61]|

**未逐行审查：** 大型共享文件其余单人分支、全部卡牌/怪物/遗物镜像、其余约千级覆盖输入与历史归档、UI/日志/发布脚本全文、网络/Godot原生实现。**根本未提供：** 游戏程序集、反编译游戏、运行配置、存档、本轮失败战斗包、完整玩家日志与双端实测记录。没有把公开源码缺少这些材料写成无法继续研究的理由，也没有补造其内容。

### 8.3 归档完整性

输入 ZIP：`CombatSolver-0.41.2-source.zip`，SHA-256：

```text
86bf0749dfc144ee9f7b3de0aa0d34a18e1824308a68a8e4527200b244c520b4
```

解压目录：`review_0412_2dc5d15/CombatSolver-0.41.2`。2017个普通文件逐项保存初始SHA-256，交付前复核原树文件数量、内容和增删；详细结果在 `CombatSolver_0.41.2_integrity.json`。所有脚本、报告和运行输出均在该源码目录之外。ZIP与用户声明固定提交绑定；没有 `.git` 对象库时，不能把内容哈希校验夸大为独立重新计算该 Git 提交。

## 9. 建议优先级与上轮建议的正式修订

**一个主方案：证据一致的保守多人 Beam。** 先修 F01/F02/F03，再将已知同一时点的发布投影与有界压缩对齐、澄清共同边界动作同分与未知尾部；资源 owner 先补观察，预算末端已付候选接收只作受控后续项。不加新搜索器，不让“模型更深”替代事实正确，不改3HP含义与7周期上限。

**一个低成本回退：仅保留 P1 正确性修复＋现役三通道 Beam。** 在本机根前药水窗口语义未确定时，F03先补可验证归属/诊断，不能假装完成政策纠正；不为其他改进扩大预算。跨批次压缩与深层组合仍有局限，明确保留。

两步挑战不是主方案的隐藏第四批。恢复考虑它的前提是：先得到一个实际生产入口可达的延迟组合丢路 fixture，并证明在包含选择、重放、最终验证的固定工作和墙钟预算中，比上述修正后的基线更好，且不破坏5–7周期哨兵。否则保持简单。原始算法资料的比较和三批计划详见配套设计；Beam-Stack、best-first、UCT和信息集方法的论文并没有为当前非单调多人分数与固定桌面预算提供免费保证。[P01][P02][P03][P04]

## 附录 A：可运行脚本、真实输出与复现限制

完整标准库脚本独立随附：[CombatSolver_0.41.2_abstract_checks.py](CombatSolver_0.41.2_abstract_checks.py)。机器可读结果：[CombatSolver_0.41.2_abstract_results.json](CombatSolver_0.41.2_abstract_results.json)。不需要游戏、不联网、不写源码；Python 3.10以上。

```bash
python CombatSolver_0.41.2_abstract_checks.py --out ./abstract-results
# 可选：增加六个只读源码文本一致性检查；不是 C# 构建/结构门禁。
python CombatSolver_0.41.2_abstract_checks.py \
  --out ./abstract-results --source ./CombatSolver-0.41.2
```

本轮实际运行 Python 3.13.5，带 `--source` 指向本轮独立解压目录。24节点×4深度的关系检查不是合法战斗枚举；A11/A12的单位是玩具边模拟，不是游戏 ExpandedNodes；`CP.action_count` 是用于检验设计的提议字段，现役 `MultiplayerCycleCheckpoint` 没有它。脚本不模拟 `.NET List.Sort` 的相等元素稳定性。六个源码子串检查只用于防误接基线，不代替语法或行为验证。

下面为实际输出原文：

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
|[C08]|`src/Search/MultiplayerSearchPolicy.cs`，Apply：多人禁用单人增长/可接受战损早停，8–24 行。|
|[C09]|`src/Search/CombatBeamSolver.cs`，有效药水政策的构造期降级，83–93 行。|
|[C10]|`src/Runtime/BattleDamageTracker.cs`，Begin/Observe 与多人返回，24–49 行。|
|[C11]|`src/Runtime/BattleDamageTracker.cs`，全队 PotionUsedEntry 计数，95–102 行。|
|[C12]|`src/Runtime/SolverController.cs`，PrepareSolveRequest：BattleDamageTracker.Observe，990–1015 行。|
|[C13]|`src/Search/CombatSearchCoordinator.cs`，多人先分派到唯一 CombatBeamSolver，1–42 行。|
|[C14]|`src/Runtime/CombatReplayOutcome.cs`，PotionUsedEntry.Actor 的本机归属先例，40–83 行。|
|[C15]|`src/Search/CombatBeamSolver.Phases.cs`，ExpandNextSerially 与最后父节点串并行边界，1648–1716 行。|
|[C16]|`src/Search/CombatBeamSolver.Expansion.cs`，卡候选裁剪、yield/finally、后续药水展开，688–768 行。|
|[C17]|`src/Search/CombatBeamSolver.Phases.cs`，中间保留后节点预算耗尽清空 active，1867–1903 行。|
|[C18]|`src/Search/CombatBeamSolver.Phases.cs`，保存上一层多人有界候选，1380–1404 行。|
|[C19]|`src/Search/CombatBeamSolver.Multiplayer.cs`，中间三通道与小 Beam 规则，50–103 行。|
|[C20]|`src/Search/CombatBeamSolver.Multiplayer.cs`，旧路线种子：四条、32 动作、重新验证，105–161 行。|
|[C23]|`src/Search/CombatBeamSolver.MultiplayerEvaluation.cs`，CaptureMultiplayerCycle：全局资源计数快照，9–18 行。|
|[C24]|`src/Search/SimulatedCombatState.LongTermResources.cs`，无玩家参数的救命资源统计，1–75 行。|
|[C25]|`src/Search/SimulatedCombatState.Potions.cs`，药水所有权库存与 PredictedPotionUse 记录，1–139 行。|
|[C26]|`src/Engine/InCombat/Mirrors/Hooks/Death/DeathPreventerMirrors.cs`，Fairy/Tail：真实所有者结算与全局记账，1–80 行。|
|[C27]|`src/Search/CombatBeamSolver.StateEvaluation.cs`，多人中间 Score，518–531 行。|
|[C28]|`src/Search/CombatBeamSolver.Expansion.cs`，SelectActionCandidates：父节点内动作/家族保留，3529–3668 行。|
|[C29]|`src/Search/MultiplayerSearchPolicy.cs`，MultiplayerHpLossBudget：根前、当前、已完成周期，29–74 行。|
|[C30]|`src/Search/CombatBeamSolver.Expansion.cs`，受击、周期检查点、下一玩家阶段与胜利，3374–3465 行。|
|[C31]|`src/Search/CombatBeamSolver.MultiplayerRound.cs`，多人正常阶段、额外回合、队友选择边界，1–156 行。|
|[C32]|`src/Search/SimulatedCombatState.Multiplayer.cs`，本机标识与 RequireLocalChoice，1–62 行。|
|[C33]|`src/Runtime/CombatRootSnapshot.cs`，主线程冻结、本机定位、本轮扣血、前后 stamp，137–275 行。|
|[C34]|`src/Runtime/ContinuationStamp.Multiplayer.cs`，全队状态与身份的新鲜度签名，1–70 行。|
|[C35]|`src/Runtime/SolverController.Multiplayer.cs`，手动建议完成、过期观察、纯路线缓存，1–44 行。|
|[C36]|`src/Runtime/SolverController.cs`，多人手动入口、取消与前次 worker 排空，846–889 行。|
|[C37]|`src/Runtime/SolverController.cs`，结果/worker/回调完成后释放请求，2060–2133 行。|
|[C38]|`src/Search/CombatBeamSolver.Transpositions.cs`，多人政策历史进入转置标签，1–62 行。|
|[C39]|`src/Search/CombatBeamSolver.Expansion.cs`，多人跨不同战斗状态不作粗略支配，3864–3893 行。|
|[C41]|`src/Search/CombatPlan.cs`，SimulationSnapshot 的现有观测，1210–1320 行。|
|[C43]|`src/Search/SimulatedCombatState.cs`，Fork 复制/共享现有不可变观察，420–449 行。|
|[C45]|`src/Search/CombatBeamSolver.cs`，IsMultiplayerAdvice 与官方能力政策零进入守卫，44–67 行。|
|[C46]|`src/Search/SearchPolicySnapshot.cs`，可接受战损早停与最少用药语义，30–66 行。|
|[C47]|`src/Search/CombatBeamSolver.Phases.cs`，PublishRoutePreview：同批 Prepare/Select，868–939 行。|
|[C48]|`tools/OfflineSearchHarness/MultiplayerFinalSelectionContracts.cs`，当前 R1/R2 生产入口合同源码，1–235 行。|
|[C49]|`tools/OfflineSearchHarness/MultiplayerEvaluationContracts.cs`，周期、铺垫、救援、坏续行与立即胜利合同，1–277 行。|
|[C50]|`tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs`，策略合同入口、扣血/重算 native fixture，23–130 行。|
|[C51]|`tools/OfflineSearchHarness/MultiplayerContracts.cs`，七周期、纯路线重放与零完整周期合同，1–128 行。|
|[C52]|`tools/OfflineSearchHarness/MultiplayerStartContracts.cs`，离线 host/client 启动 fixture 的替身范围，1–60 行。|
|[C53]|`tools/verify-refactor-boundaries.sh`，shell 多人结构门禁片段，1254–1292 行。|
|[C54]|`tools/verify-refactor-boundaries.ps1`，PowerShell 多人结构门禁片段，1570–1606 行。|
|[C55]|`docs/multiplayer-advisor.md`，当前多人指南：语义与局限，1–99 行。|
|[C58]|`coverage/test-evidence.json`，随附历史结构化证据，非本轮重跑，1–65 行。|
|[C59]|`src/Search/MultiplayerCycleCheckpoint.cs`，现有不可变周期观察字段，1–6 行。|
|[C60]|`src/Prediction/MonsterMoveEffects.Multiplayer.cs`，多人怪物效果接入片段，1–43 行。|
|[C61]|`src/Prediction/CardOnPlaySupport.Multiplayer.cs`，多人卡牌效果接入片段，1–30 行。|
|[C62]|`src/Search/CombatBeamSolver.Phases.cs`，用药资格、MeetsHpTarget 与完成路线 incumbent，289–345 行。|

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
[C08]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/MultiplayerSearchPolicy.cs#L8-L24
[C09]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.cs#L83-L93
[C10]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/BattleDamageTracker.cs#L24-L49
[C11]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/BattleDamageTracker.cs#L95-L102
[C12]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/SolverController.cs#L990-L1015
[C13]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatSearchCoordinator.cs#L1-L42
[C14]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/CombatReplayOutcome.cs#L40-L83
[C15]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L1648-L1716
[C16]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Expansion.cs#L688-L768
[C17]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L1867-L1903
[C18]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L1380-L1404
[C19]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Multiplayer.cs#L50-L103
[C20]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Multiplayer.cs#L105-L161
[C23]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.MultiplayerEvaluation.cs#L9-L18
[C24]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/SimulatedCombatState.LongTermResources.cs#L1-L75
[C25]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/SimulatedCombatState.Potions.cs#L1-L139
[C26]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Engine/InCombat/Mirrors/Hooks/Death/DeathPreventerMirrors.cs#L1-L80
[C27]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.StateEvaluation.cs#L518-L531
[C28]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Expansion.cs#L3529-L3668
[C29]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/MultiplayerSearchPolicy.cs#L29-L74
[C30]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Expansion.cs#L3374-L3465
[C31]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.MultiplayerRound.cs#L1-L156
[C32]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/SimulatedCombatState.Multiplayer.cs#L1-L62
[C33]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/CombatRootSnapshot.cs#L137-L275
[C34]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/ContinuationStamp.Multiplayer.cs#L1-L70
[C35]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/SolverController.Multiplayer.cs#L1-L44
[C36]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/SolverController.cs#L846-L889
[C37]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Runtime/SolverController.cs#L2060-L2133
[C38]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Transpositions.cs#L1-L62
[C39]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Expansion.cs#L3864-L3893
[C41]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatPlan.cs#L1210-L1320
[C43]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/SimulatedCombatState.cs#L420-L449
[C45]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.cs#L44-L67
[C46]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/SearchPolicySnapshot.cs#L30-L66
[C47]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L868-L939
[C48]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/tools/OfflineSearchHarness/MultiplayerFinalSelectionContracts.cs#L1-L235
[C49]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/tools/OfflineSearchHarness/MultiplayerEvaluationContracts.cs#L1-L277
[C50]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs#L23-L130
[C51]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/tools/OfflineSearchHarness/MultiplayerContracts.cs#L1-L128
[C52]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/tools/OfflineSearchHarness/MultiplayerStartContracts.cs#L1-L60
[C53]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/tools/verify-refactor-boundaries.sh#L1254-L1292
[C54]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/tools/verify-refactor-boundaries.ps1#L1570-L1606
[C55]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/docs/multiplayer-advisor.md#L1-L99
[C58]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/coverage/test-evidence.json#L1-L65
[C59]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/MultiplayerCycleCheckpoint.cs#L1-L6
[C60]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Prediction/MonsterMoveEffects.Multiplayer.cs#L1-L43
[C61]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Prediction/CardOnPlaySupport.Multiplayer.cs#L1-L30
[C62]: https://github.com/sgpsdd-zyx/CombatSolver/blob/2dc5d15b26f16d89436af0fb98650d8b4cf6b411/src/Search/CombatBeamSolver.Phases.cs#L289-L345
[P01]: https://cdn.aaai.org/ICAPS/2005/ICAPS05-010.pdf
[P02]: https://aclanthology.org/2020.tacl-1.51/
[P03]: https://www.lri.fr/~sebag/Examens_2008/UCT_ecml06.pdf
[P04]: https://eprints.whiterose.ac.uk/id/eprint/75048/1/CowlingPowleyWhitehouse2012.pdf
[P05]: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1.sort?view=net-9.0
