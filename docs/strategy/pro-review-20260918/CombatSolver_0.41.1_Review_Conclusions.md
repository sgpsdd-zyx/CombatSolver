# CombatSolver 0.41.1 源码复审结论

**固定基线：** fork `0.41.1`，提交 `5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77`。  
**复审日期：** 2026-09-18。  
**配套设计：** `CombatSolver_Multiplayer_Strategy_Optimization_0.41.1.md`。  
**执行范围：** 阅读需求、解压公开源码、沿调用链复审、查阅原始资料、运行独立 Python 抽象检查；未修改仓库、未运行发布操作。

## 1. 问题与优先级

**最值得先修的不是“把 Beam 换掉”，而是最终候选管线的资格过滤和共同评估上下文。** 当前共同周期比较器在固定候选组内的设计已经明显好于旧版；新发现发生在它的上游截断与下游再次创建上下文之间。另一个重要问题是“全队救命资源消耗”与“本机风险”的观测口径没有分开。这些发现不能证明当前实战不佳的唯一根因。

本文使用四种证据等级：**S：源码直接确认；A：受限抽象模型已复现；G：需要真实引擎／游戏验证；H：优化假设**。S/A 可以定位逻辑问题，但不自动证明真实战斗中的可达频率、损失幅度或胜率变化。严重程度表示处理优先级，不是已经发生事故的证明。

| 编号 | 优先级 | 发现 | 证据与结论边界 |
|---|---|---|---|
| R1 | P1，高 | 多人最终候选先取 `4 × BeamWidth`，随后才过滤“至少用一瓶／最少显式用药”；可能丢掉原候选组中唯一合格路线 | S+A；完整 `Solve` 可达夹具尚需 G |
| R2 | P1，高 | 截断／资格过滤后重新计算共同周期，可能用新的排序目标重排旧目标留下的子集 | S+A；不是固定比较器不传递，完整搜索触发频率需 G |
| R3 | P1，口径优先核实 | 救命次数与药水观察是全队汇总；全队救命次数在本机超额扣血之前参与最终比较 | 数据所有权丢失为 S；对“本机风险优先”的反例为 A；优先级改动需明确确认，实际触发需 G |
| R4 | P2，中 | 修复三通道代表饥饿后，仍没有跨首动作的覆盖保证；两步以后才显现的铺垫仍可能提前消失 | S 描述机制，H 描述收益瓶颈；不是旧配额 bug 回归 |
| R5 | P2，中 | 最终有效敌方 HP 汇总、队友存活人数不能完整表达集火、控制和非对称队友危险 | S 描述评分信息，H 描述改进价值；并不等于引擎丢失目标语义 |
| R6 | P2，验证债务 | 缺少最终筛选管线、同首动作的完整好／坏续行、多人资源归属及实际交错操作的充分回归证据 | 已读测试与产物边界为 S；未覆盖不等于已算错 |

### 1.1 R1：先截断后检查用药资格，可以制造“无合格路线”

**触发条件。** 多人模式，最终池中存在符合 `RequireAtLeastOne` 或 `minimumPotionUses` 的路线，但它在当前质量排序中位于前 `4B` 之外；至少 `4B` 条尚不满足这个全局用药要求的路线排在前面。这里不需要逐槽位 `Force` 指令，因此比较器先比较逐槽强制指令，并不能排除这个例子。

**实际调用链与行号。**

`Phases.cs:2045–2059` 建最终池、补用药／不用药边界回退；`2073` 调 `Retention.RankFinal`；`BeamRetentionPolicy.cs:479–483` 对多人直接取 `RankMultiplayerFinal(candidates, BeamWidth * 4)`；`Multiplayer.cs:18–22` 创建上下文、排序、截断。随后 `Phases.cs:2084–2087` 调 `FinalOrdering.Select`，其多人分支在 `FinalPlanOrdering.cs:40–46` 才执行最少显式用药与 `RequireAtLeastOne` 过滤，空集抛出 `PotionPolicyUnsatisfiedException`。预览也经过 `Phases.cs:893–913` 同类顺序，异常会使该次预览不发布。[^C01][^C02][^C03][^C04]

**最小候选级反例。** `B=1`；五条路线都活着、都实际完成一个敌方周期、没有强制槽位指令、本机扣血为零、其他风险相同。A/B/C/D 不用药，周期末有效敌方 HP 分别为 10/11/12/13；P 显式用药一次，周期末有效敌方 HP 为 14。策略要求至少用一瓶。

```text
原池                 A B C D P
按多人最终质量取前4   A B C D
随后过滤必须用药     空集 → 报无满足指令的路线
先过滤再评估         P → 有合格路线
```

**影响。** 不只是 P 排名偏低，而是“原池里确实有满足用户要求的候选”被错误呈现为无满足要求的路线。补回用药 fallback 在截断前发生，不能保证其在截断后还存在。尤其值得注意：同一 `RankFinal` 的单人分支在 `489–493` 行以后专门处理资格维度的候选保留，而多人在 `483` 行已经早返回；修复应是多人自己的发布资格管线，不是把整套单人药水评分借回来。

**已做验证。** 独立 Python 检查复现上述结果。移植的是这个受限候选域中的实际排序维度与实际操作顺序，不是生产 C# 的执行，也没有构造出可发布的游戏失败包。

**最小修正。** 在“准备发布／最终返回”的多人入口，统一执行：**完整候选池 → 资格过滤 → 冻结共同周期 → 排序／截断 → 使用同一上下文选中结果**。不要简单地在所有中间保路点过滤“尚未用药”的非终局前缀：它可能在随后合法用药。合格池确实为空时仍然明确失败，不能伪造“不用药也满足要求”。首个 C# fixture 应直接覆盖生产候选准备函数与最终选择函数，而不是只再测一次比较器。

### 1.2 R2：固定组比较器没有失去传递性，但选路管线改变了比较目标

**已修好的部分。** `CreateMultiplayerOrdering` 为一组候选冻结一个共同周期，而不是每次两两比较取较浅周期；这是正确的重要修复。[^C05]

**剩余问题。** `Multiplayer.cs:20` 用截断前的池创建排序器，`FinalPlanOrdering.cs:47` 又对截断且过滤后的池创建排序器。如果唯一较浅候选被裁掉，共同周期可能加深。被裁掉的其他候选却没有重新参与新目标的比较。[^C02][^C04]

**五候选最小翻转。** `B=1`；全部为存活、非胜利、正常或 horizon 边界候选；风险、队友人数、用药和本机 HP 相同。括号内是按周期保存的有效敌方 HP：

| 路线 | 第 1 周期 | 第 2 周期 |
|---|---:|---:|
| A | 40 | 35 |
| B | 41 | 25 |
| C | 42 | 15 |
| D | 43 | 5 |
| S | 99 | 未完成 |

完整池共同周期是 1，A 最好；截断保留 A/B/C/D，S 被删除；重新创建上下文变为周期 2，D 最好。**允许新一批搜索使用更深证据，并不是问题；在同一次候选选择中改目标，却沿用旧目标删过的集合，才是问题。**

再加入 X，其两个检查点为 `(44, 1)`，就得到六候选的更强反例：按周期 1 仍然只留下 A/B/C/D；在新周期 2 上，未获重审的 X 比所选 D 更好。所有敌方 HP 都正数并且单调下降，不依赖死亡／复活特殊规则。

**已做验证。** 五候选翻转、六候选丢失最优新目标候选均已通过 Python 抽象执行复现；六候选全部 **720 种输入排列**结果相同。同时检查了固定周期 1 上六候选的 **216 组三元关系**，没有出现所检查域内的反对称／传递性违反。后者不是对完整 C# 比较器的全域形式化证明，也不是仓库历史“27 组三候选测试”的重跑。

**修正。** 返回值携带本批的不可变 `MultiplayerPlanOrdering`；预览、截断、最终选择使用同一个对象。新一轮统一深化可以建立新对象，但必须在该轮的有界完整入选池上先建立目标再剪枝。不能将全请求的共同深度永久锁在 1；也不能通过排除所有浅候选、只留下深路线来掩盖该问题。

**预算关联。** `Phases.cs:2070–2072` 在预算停止时加入上一层候选，这本身是合理的 anytime 保底；它同时意味着混合深度是合法输入。因此“大家总会一样深”不能作为免测理由。[^C01]

### 1.3 R3：全队救命观察不能直接当作本机救命风险

**源码事实。** `SimulatedCombatState.LongTermResources.cs:48,60–74` 使用一个不带玩家身份的 `DeathSaveUseCount`。死亡阻止镜像在实际受益者可能是任一玩家时调用该全局记录：`DeathPreventerMirrors.cs:13–29` 的 Fairy 和 `39–60` 的 Lizard Tail。`CaptureMultiplayerCycle` 在 `MultiplayerEvaluation.cs:12–17` 将全局次数放进检查点，`CompareMultiplayerAtCycle:84–90` 先比较这个次数，后比较本机 3 HP 超额。[^C05][^C06][^C07]

药水也存在观察归属缺口：`SimulatedCombatState.Potions.cs:10–14` 的 `PredictedPotionUse` 只有槽位、药水 ID、战略代价与 automatic；`ConsumePotion:50–63` 明明知道 `Player`，但写入使用记录时未保存。库存字典本身按 `(Player, Slot)` 管理，并不是库存把同槽位队友混在一起。[^C08]

**最小排序反例。** 本机与队友各一人、两条路线都没有本机死亡／本机复活，结束时两人都活着，敌方有效 HP 相同：

| 路线 | 本机本周期扣血 | 本机救命消耗 | 队友救命消耗 |
|---|---:|---:|---:|
| A | 0 | 0 | 1 次 Fairy |
| B | 4 | 0 | 0 |

当前比较器优先选 B：全队救命次数 `0 < 1`，在本机超额判断之前已经结束比较。按“本机风险优先；本人风险相同再比较队友救援”的解释，A 应优于 B。

**应如何定性。** “无法区分谁消耗救命资源”是源码确认的问题；“全队一次 Fairy 是否一定应排在本机 1 HP 超额之后”包含产品取舍。不能把未明确授权的偏好变更冒充纯实现纠错。因此先补身份观察和反例，明确这个优先级，再改变生产排序。没有观察到真实 Fairy 触发的错误镜像结算；本文不指控 Fairy 的治疗数值算错。

**不能采用的捷径。** 不能用槽位猜玩家，不能把“最终库存少一瓶”视为完整使用历史；`ProcurePotion` 支持补药，重复职业和相同药水槽位也合法。不能把 peer automatic use 当作本机显式用药；在当前队友不主动用药的模型中，`ExplicitPotionUseCount` 的主要问题并不是把所有队友自动 Fairy 直接算作用户手动用药。[^C08]

**工程切口。** 保留原镜像所有战斗效果、原全局记录调用及参数；仅在多人观察启用时，将受益玩家身份和资源事件写入多人专属、可 Fork 的小账本。`ConsumePotion` 的多人观测分派也同理。它跨到了公共镜像的观测调用点，必须单列依赖并做单人零进入断言，不混入第一批纯 Search 修复。禁止另造一套死亡或治疗结算。

### 1.4 R4：代表配额正确，不等于所有有价值前缀都活下来

`Multiplayer.cs:46–69` 的三条通道已明确安排小宽度退化和重合代表填充；宽度 3 不再先用总分占一席，宽度 2 有轮换，重合代表不会不必要地挤掉其他通道。应保留这些改进。[^C02]

但三条通道选择的是**节点特征**，不是不同首动作的策略族。多个通道的不同节点仍可能来自同一首动作；铺垫通道先比较 `PersistentBuffValue`，再 `LatentSetupValue`、`ReachableHandValue`。多人中间分仍为存活／胜利项、`-EnemyHp × 100`、`PersistentBuffValue × 20`、手牌值、能量和小额资源／步数项，再扣逐周期超额惩罚。其结构没有保证“第二步才显现的组合”一定存活。[^C02][^C09]

**首个可证伪 fixture。** 三条现有通道各有一个立即显值候选；另一合法前缀先花能量做准备，第一步在三种特征上都较差，第二步才使第 5 周期收益兑现。把奖励实现为测试域中的合法状态转移，不要凭空指定一张未验证的游戏卡。断言当前选择在“第一次动作截断”处丢掉准备分支；再在相同总工作量下测试有界双步挑战是否找回。真实卡牌映射是后续 G，不是本文已运行的实验。

**低复杂度方向。** 维持原 Beam 和小宽度配额；在现有截断前保存极少数未覆盖首动作的纯动作前缀，用一个临时工作席位重新模拟至可比较边界。不是每个首动作再开一个 Beam，不建长期树。挑战失败只否定那条具体续行，不应标记“这个首动作永远不行”。方案细节见配套设计。

### 1.5 R5：目标和队友威胁存在信息压缩，不是目标模拟缺失

`StateEvaluation.cs:65–91` 按敌人读取有效生命和更丰富的分布信息；`SimulatedCombatState.cs:1429–1437` 的 `EffectiveEnemyHp` 还考虑部分变身／复活语义，不能说它只加原始当前 HP。合法攻击、队友和药水目标在 `Expansion.cs:4392–4425` 依身份枚举。[^C09][^C10][^C11]

然而多人最终检查点只有 `EnemyHp` 汇总与 `TeamSurvivors` 人数；排序在本机风险及存活层之后使用这些量。队友 1 HP 与 20 HP、两个同血量但威胁不同的敌人，可能在最终摘要中没有充分区分。多人还明确跳过了原单人 StandPat 威胁预测，不能把该占位值当作已完成全队受击验证。[^C05][^C09]

**最小威胁例子。** 敌人 H 有效 HP 为 6、下一周期对脆弱队友产生 20 HP 量级的攻击；敌人 L 有效 HP 为 10、下一周期攻击量为 2。当前周期两条路线都能完全防住：A 击杀 H，留下 L 的 10；B 击杀 L，留下 H 的 6。仅看当前共同边界汇总，B 更优；继续通过真实引擎走完下一周期，结果可能反转。数字是抽象机制示例，不是游戏敌人数据。

**不能过度推断。** 当下一周期已经搜到并正确比较，现有算法可能自然解决此例；首要假设是短预算下的中间保路／证据深度不够，不是必须添加一个“威胁分”就能提高胜率。控制、防守价值最好由实际受击和存活兑现；不得既加“减少预计伤害”的奖励，又给同一减伤在实际扣血上第二次奖励。首版先将按受害者区分的原始事实用于诊断和探针，未经消融不加入新的高优先级最终启发式。

### 1.6 R6：需要补的是证据，不是把所有边界都改成猜测

当前有限测试不足以回答：R1/R2 在生产最终管线是否触发；同一个首动作下两条**都已完成**的好／坏续行是否同时可用；2/3/4 人、相同职业和相同槽位是否保持救命归属；队友手动动作插入后旧建议是否及时失效；药水和选牌动作交错是否保留明确边界。这些应补成小 fixture，而不是先跑大量完整战斗才定位第一处错误。

预算方面，种子重放本身有数量限制和工作计数；仍需把新探针、续行、预览及最终重放纳入同一个请求总成本。`SeedMultiplayerRoutes` 的局部计时与 `Phases` 的总计时不能被新功能用来各自获得一份完整时间额度。最终重放和单个不可中断引擎阶段还可能造成软截止后的开销。**本文没有测量墙钟超时，也不将这种软预算性质宣布为新的高严重度故障。**[^C01][^C02]

另外，当前队友存活反证取共同检查点与当前端点的最小值，未直接保存“所有中间周期的最低队友存活人数”。假如存在“中途实际死亡、稍后真正复活”的受支持路线，早期队友死亡是否被后续状态遮蔽值得测；但 Fairy 阻止死亡不等于先死后复活，不能拿 Fairy 自动证明这个假设成立。[^C05]

## 2. 应保持原样的合理设计与已落地改进

### 2.1 正确的单人／多人分派

`CombatSearchCoordinator.cs:16–25` 对多人早返回单次求解入口；`MultiplayerSearchPolicy.Apply:8–24` 禁用与当前多人不相容的单人收益、组合搜索与能力相关政策入口。`CombatBeamSolver.cs:56–57` 明确在 `policy.Multiplayer == null` 时才检测注册能力牌，不只是事后把权重设为零。相关单人能力合同还检查进入计数为零。[^C12][^C13][^C14]

本次已读调用点支持“多人不应借用官方单人能力政策”的实现方向。没有官方基线的独立完整源码与可运行 DLL，本次不能重新证明全部单人行为逐项相同。后续每个公共文件修改都应证明 solo 条件、参数和原分支不变，而不是仅比较一个最终动作。

### 2.2 3 HP 账本并没有发现旧式重置错误

根捕获在主线程按本机身份从当轮伤害历史累计 `UnblockedDamage`，不是通过“初始 HP − 当前 HP”推算，因此治疗不会抵消已经支付的伤害。`MultiplayerHpLossBudget.Capture` 按 `AdvisoryLastEnemyCycleHpLost` 拆开敌方周期结束前的损失与新玩家回合开始自损；一次推进跨过多个敌方周期会抛错，不会假装平均分摊。[^C13][^C15]

手动重算重新捕获当前事实，历史中的本周期扣血仍计入；同敌方周期里的额外玩家回合不会因 `StartPlayerTurn` 就自动获得新的 3 HP。原始事件历史与当前回合阶段是其依据，3 HP 是逐周期毛损失目标而非净治疗后损失。[^C15][^C16]

本次独立执行的算术例子为：根已付 2 HP；之后旧周期再付 3 HP，旧周期共 5、超额 2；下一回合开始自损 4 属于新周期，超额 1；累计超额 3、最大单周期损失 5。该结果与现行 `Advance` 分账吻合。它不代替原生事件阶段对账，但足以否定“看到新回合开始就统一清零”的臆测。

### 2.3 周期检查点和后续反证的方向正确

`MultiplayerCycleCheckpoint` 是不可变值记录链；捕获在敌方阶段后、新玩家回合开始前进行。共同边界的未使用格挡、裸铺垫分、仅仅多搜了几层均不直接奖励。后续这条续行实际出现的本机死亡、救命消耗、超额或更少队友存活仍参与比较。[^C05][^C16][^C17]

没有发现源码实现了“某条坏续行使所有相同首动作永久失格”的全局黑名单。风险在于保路和可用候选不足，而不是已经证实这种过度污染。测试应保留同首动作的安全完整分支与危险完整分支，验证只打击后者。需要特别区分：坏的**可选后续动作**可以换掉；当前前缀已经触发的**必然自动结算**不能靠截短显示隐藏。

### 2.4 人类队友的未来是条件，不是自动接管

多人回合分片正常执行全队被动阶段、额外回合参与者、抽牌、回合起止效果与受击，不主动替真人出牌或用药。`RequireLocalChoice` 对非本机选择建立明确边界；相关药水、卡牌和回合开始选择入口调用它。合法目标包含队友并不意味着该队友需要安装 Mod。[^C16][^C18][^C19]

“队友不主动行动”不是通用最坏情况：真实队友既可能杀怪减伤，也可能改变目标、触发被动、消耗 RNG 或改变阶段。结论应标成条件建议。没有资料支持默认假设队友最优合作，也没有理由把每个未支持边界强行改成随机猜测。

### 2.5 状态所有权、手动和过期管理已有实质实现

主线程捕获使用前后 continuation stamp 检查根是否稳定，本机由 `LocalContext.GetMe` 定位，不按玩家数组首位猜测。多人分支 Fork 复制额外回合信息和可变映射，检查点只共享不可变历史。状态键覆盖全队相关战斗事实，政策账本／检查点另走多人转置标签。[^C10][^C15][^C18][^C20]

多人请求入口拒绝非手动来源，禁止部署和自动执行；旧工作取消后等待工作及回调释放，过期观察只标记旧建议，不自动重算。`CompleteSearchCore` 先检查是否仍是当前 session；结果在同场战斗仍可能保留显示，但多人完成路径会重新判断是否过期。这些机制不是只写在 README 中。[^C21][^C22][^C23]

仍然保留“最多四条纯动作旧路线、每条当前回合最多 32 个无选择动作”的重放边界。只在新根合法校验后模拟，不复用旧树、旧分数或旧模拟器。取消／过期／两回合生命周期仍需原生和 UI 夹具验证，不能仅凭静态阅读宣布没有竞争条件。[^C02][^C21]

## 3. 实际使用渠道、读取清单与证据来源

### 3.1 渠道与完整性

已完整阅读本次需求附件 122 行。主源码渠道为用户上传 ZIP，解压后按真实路径与方法检索；GitHub 固定 tree 页面读取受限，但 Release 页及固定提交的 `MultiplayerEvaluation.cs`、`FinalPlanOrdering.cs`、`Multiplayer.cs` raw 内容可读，并与本地审阅内容交叉核对。未使用默认 `main` 替代基线。[^D01][^D02]

归档含 **2,010 个非目录条目**，其中 `src` 下 **679 个 C# 文件**。ZIP 注释是固定 SHA；本次计算的 ZIP SHA-256 为：

```text
2094b0558aa4614baa7167860772c7e579d1faa255cf0b0cb2a075a277e76935
```

这说明本次分析绑定的是哪一个附件，并不等于已经从远端 Git 对象逐文件证明整个提交树哈希。没有 `.git` 历史供独立审查累计 diff；版本身份主要依据用户指定、归档注释及可读取固定提交文件交叉核对。

### 3.2 读取范围，避免将“完整附件”写成“整仓逐行审完”

下表的“定点”指实际读了相关方法与邻接分支，不代表该大文件全文均审计。所有正文源码行号采用附件解压后保留空行的本地行号，不是网页去空行后的行号。

| 范围 | 实际阅读深度与用途 |
|---|---|
| `AGENTS.md`、`docs/multiplayer-advisor.md`、`docs/strategy/README.md` | 项目约束、现役行为和研究采用状态；不执行其中历史发布命令 |
| `docs/ARCHITECTURE.md` | 开头与多人／请求／搜索职责段；非整篇架构形式验证 |
| `docs/DEVELOPMENT_NOTES.md`、`docs/TEST_MATRIX.md`、`coverage/test-evidence.json` | 当前版本记录、相关多人和单人隔离条目、证据路径；非全部历史逐行阅读 |
| 上轮 `CombatSolver_Multiplayer_Strategy_Research_and_Design.md` | 阅读基线、主建议和采用关系；它绑定 0.40.5，不作为当前代码事实；其策略版本开关等建议不沿用 |
| `MultiplayerSearchPolicy.cs`、`CombatBeamSolver.Multiplayer.cs`、`MultiplayerEvaluation.cs`、`MultiplayerRound.cs`、`MultiplayerCycleCheckpoint.cs` | 核心多人分片全文，政策、保路、共同比较与阶段 |
| `CombatBeamSolver.cs`、`CombatSearchCoordinator.cs`、`SearchPolicySnapshot.cs`、`Transpositions.cs` | 构造、分派、政策快照与多人标签 |
| `FinalPlanOrdering.cs`、`BeamRetentionPolicy.cs` | 多人入口及上下游相邻逻辑；大型单人分支定点读取，不声称全文审完 |
| `Phases.cs` | 根／种子、预算循环、预览、完成池、上一层保留、最终筛选、重放和释放等调用段 |
| `Retention.cs`、`Expansion.cs`、`Terminal.cs`、`StateEvaluation.cs`、`CombatPlan.cs` | 保路调用链、真实阶段推进、动作／选择／目标、终局状态、分数、状态键、节点定义等定点读取 |
| `SimulatedCombatState.cs`、`.Multiplayer.cs`、`.Fork.cs`、`.LongTermResources.cs`、`.Potions.cs`、`.CardEventHistory.cs` | 全队快照／Fork、有效敌方 HP、扣血事件、资源使用与归属；大主文件非全文 |
| `SolverController.cs`、`.Multiplayer.cs`、`CombatRootSnapshot.cs`、`ContinuationStamp.Multiplayer.cs` | 手动请求、主线程冻结、过期、完成、取消／排空与释放调用链；不是 UI／线程完整证明 |
| `CardOnPlaySupport.Multiplayer.cs`、`MonsterMoveEffects.Multiplayer.cs`、`PotionExecutionSupport.cs`、相关选择调用点 | 多人目标与被动／选择边界；未逐一审完所有卡、敌人和 relic 镜像 |
| `DeathPreventerMirrors.cs` | Fairy／Lizard Tail 与全局资源观察全文 |
| `Multiplayer*Contracts.cs`、`coverage/multiplayer/`、`TranspositionFrontierChecks` | 已读多人主要合同和关键断言、测试启动边界、转置项目依赖与历史说明；未运行 C# |

### 3.3 历史证据能支持什么

仓库记录了小 Beam 七组配额、固定候选组 27 组三元比较、救援与无效格挡、两周期铺垫、四节点中断、后来致死／超额续行、原九个扣血合同、七周期六次原生状态对账、官方单人 80 项比较与 512008 项转置检查。它们属于**已有记录，不是本次执行结果**。[^T01][^T02][^T03][^T04][^T05]

27 组三元比较支持固定候选组比较器的局部一致性，不支持“过滤→截断→重建上下文”的端到端稳定性。两周期 Inflame 例子支持某种显式能力收益能兑现，不支持所有 5–7 周期组合都能保路。已知坏续行测试不自动覆盖“两条都完成的同首动作不同续行”。七周期对账的有限两人 fixture 不能外推到所有角色、三／四人、重复职业或真人行动交错。

`coverage/multiplayer/` 在附件中仅见 `solo-power-compat.json`；多个历史 `.local/...` 运行产物未上传。启动合同使用 headless/Harmony 替身，覆盖模拟房主／客户端身份和启动重试，但绕过部分真实网络与渲染流程。Release 成功、文档检查、静态 coverage 分类和有限 fixture 都不是普遍胜率证据。[^T03][^T06]

### 3.4 本次运行与不可运行范围

**实际运行：** 独立 Python 候选级反例、固定候选比较关系、账本算术和情景共享前缀例子；归档统计和哈希。

**没有运行：** 项目编译、OfflineSearchHarness、TranspositionFrontierChecks、原生游戏模拟对账、官方单人 80 项对比、安装包验证或任何实战。容器没有 `dotnet`；附件不含游戏 DLL，也没有失败战斗包。转置工具虽然具备独立于游戏 DLL 的部分条件，在本环境仍没有运行它所需的 .NET 工具链。

**尚不可验证：** 每一种镜像是否与当前游戏 DLL 一致、所有动态目标规则、真实联机时序与 RNG、UI 选择和取消竞争、第三方 Mod 交互、不同角色的 3／4 人组合、真实建议质量和性能。遇到这些缺口仍能完成源代码管线诊断，但不能把它们转换为猜测性的“已确认游戏 bug”。

## 4. 本次抽象实验结果与可复现范围

| 检查 | 实际结果 | 能证明／不能证明 |
|---|---|---|
| R1，五候选、B=1、必须用药 | 原顺序无合格结果；先过滤顺序选 P | 证明受限候选域中的管线反例；未执行生产 `Solve` |
| R2，五候选 | 截断前共同周期 1 选 A；截断后共同周期 2 选 D | 证明目标变更，不证明深度 2 本身错误 |
| R2，六候选与全排列 | 720 种排列均选 D；X 被删除但在重建目标下比 D 好；冻结目标版本选 A | 排除该反例只是输入顺序偶然；不证明所有类型节点均可达 |
| 固定周期、六候选三元关系 | 216 组检查通过 | 反驳“该例是固定组比较器不传递”；非全域形式证明 |
| R3，资源归属 | 全队次数优先选本机扣 4 的 B；本机归属优先选 A | 排序后果明确；新排序偏好需要确认 |
| 现行账本算术 | 旧周期超额 2、新周期超额 1，总 3 | 支持现有分账算术；不是原生事件阶段测试 |
| 情景前缀融合 | 逐情景全知最大平均为 10；同一可执行前缀最优为 C 的 6 | 说明不允许把不同首动作的全知收益合并；不是队友概率模型 |

附录脚本可在普通 Python 3 环境运行，无第三方依赖。其域刻意限制为：活着、非胜利、无外部／未支持边界、有共同周期检查点、无逐槽强制指令。它没有实现全部 `SearchNode`、全部风险字段或游戏战斗；不应被当成新的权威战斗引擎。

## 5. 优先行动、可证伪假设与停止条件

**先做候选管线。** 将 R1/R2 的抽象表格变为生产函数合同，覆盖预览和最终返回；使资格池与评估上下文从形成到选中结果都可追溯。在任何搜索质量调整前，要求这组测试通过。

**其次核对身份和风险口径。** 用两玩家同槽位 Fairy、交换本机索引、三玩家一人濒死的原生最小 fixture，确认资源事件归属。产品层明确是否“本机救命／超额优先，队友救命成本在后”。没有这个确认，不悄悄更改默认风险序。

**然后才测搜索覆盖。** 主方案保留 Beam，以单个有界挑战席位重放被提前淘汰的两步前缀，使用同一引擎续行到可比较周期，保留 5–7 周期价值。是否有效由相同模拟工作量与相同墙钟预算两组实验决定，不靠增加 Beam、时间或节点数得到改善。

| 假设 | 最先观察的错误边界 | 会推翻该假设的证据 |
|---|---|---|
| 最终筛选管线影响实际建议 | 合格池存在到最终准备批次之间 | 真实域的完整约束证明这些反例不可达，且生产函数合同验证该约束；仅“暂时没碰到”不够 |
| 延迟组合被中间截断 | 第一次失去好前缀的动作层 | 好前缀仍存活，真正错误在叶子评分或镜像；应停止加保路 |
| 有效 HP 汇总低估威胁差异 | 第一个共同周期的目标排序 | 真实下一周期对账显示两个目标同风险，或当前策略已有足够深度自然选对 |
| 被动队友假设造成过防／等待 | 冻结根之后首个真人动作插入点 | 去掉已发生动作的过期误差后，质量问题主要仍是本机组合搜索；不该先扩情景 |
| 本机／队友资源混合导致偏好倒置 | 第一次 peer death-save 记录 | 需求明确授权全队资源先于本机超额；则需文档化而非称 bug |

任何单人新入口、账本逆行、跨 Fork 污染、已知坏后续被隐藏、重放状态不一致、吞掉未知语义、取消后残留模拟器，均是停止推进的硬条件。没有同预算收益证据就回退新增挑战逻辑，保留 R1/R2 的正确性修复。完整三批方案及可交付 Codex 的任务见配套设计。

## 附录 A：本次执行的抽象检查脚本


```python
"""Abstract candidate-level checks; NOT CombatSolver C# execution or game tests."""
from dataclasses import dataclass, replace
from itertools import permutations, product
import json

@dataclass(frozen=True)
class Candidate:
    name: str
    enemy_hp: tuple[int, ...]
    explicit_potions: int = 0
    automatic_potions: int = 0
    local_loss: int = 0
    global_saves: int = 0
    local_saves: int = 0
    team_alive: int = 2
    hp: int = 20
    actions: int = 4

def depth(pool):
    # Restricted domain: all alive, non-winning, comparable checkpoints;
    # no unsupported/choice boundaries and no per-slot forced directives.
    assert pool and all(n.enemy_hp for n in pool)
    return min(len(n.enemy_hp) for n in pool)

def old_key(n, d):
    # Exact varying fields of CompareMultiplayerAtCycle on this domain.
    # All loss occurs in cycle 1, saves/potions occur by cycle 1.
    return (n.global_saves, max(0, n.local_loss - 3),
            -n.team_alive, n.enemy_hp[d - 1], n.local_loss,
            -n.hp, n.explicit_potions + n.automatic_potions, n.actions)

def old_pipeline(pool, require_one=False, beam=1):
    d0 = depth(pool)
    retained = sorted(pool, key=lambda n: old_key(n, d0))[:4 * beam]
    eligible = [n for n in retained if not require_one or n.explicit_potions > 0]
    if not eligible:
        return None, d0, None, [n.name for n in retained]
    d1 = depth(eligible)
    best = min(eligible, key=lambda n: old_key(n, d1))
    return best.name, d0, d1, [n.name for n in retained]

def fixed_pipeline(pool, require_one=False, beam=1):
    eligible = [n for n in pool if not require_one or n.explicit_potions > 0]
    if not eligible:
        return None
    frozen = depth(eligible)
    retained = sorted(eligible, key=lambda n: old_key(n, frozen))[:4 * beam]
    return min(retained, key=lambda n: old_key(n, frozen)).name

def run():
    results = {}
    p = [Candidate(chr(65+i), (10+i,)) for i in range(4)]
    p.append(Candidate('P', (14,), explicit_potions=1))
    assert old_pipeline(p, True)[0] is None
    assert fixed_pipeline(p, True) == 'P'
    results['R1_potion_eligibility'] = {'old': old_pipeline(p, True), 'fixed': 'P'}

    q = [Candidate('A', (40,35)), Candidate('B', (41,25)),
         Candidate('C', (42,15)), Candidate('D', (43,5)), Candidate('S', (99,))]
    assert min(q, key=lambda n: old_key(n, depth(q))).name == 'A'
    assert old_pipeline(q)[0] == 'D'
    assert fixed_pipeline(q) == 'A'
    results['R2_five_candidate_flip'] = {'old': old_pipeline(q), 'fixed': 'A'}
    q.insert(4, Candidate('X', (44,1)))
    assert old_pipeline(q)[0] == 'D'
    assert old_key(q[4], 2) < old_key(q[3], 2)
    checked = 0
    for order in permutations(q):
        assert old_pipeline(list(order))[0] == 'D'
        assert fixed_pipeline(list(order)) == 'A'
        checked += 1
    results['R2_six_candidate_discard'] = {
        'old': old_pipeline(q), 'discarded_better_at_rebuilt_depth': 'X',
        'fixed': 'A', 'permutations_checked': checked}
    triples = 0
    for a,b,c in product(q, repeat=3):
        ka,kb,kc = (old_key(n,1) for n in (a,b,c))
        assert not (ka < kb and kb < ka)
        if ka <= kb and kb <= kc:
            assert ka <= kc
        triples += 1
    results['fixed_cohort_comparison'] = {'triple_checks': triples, 'passed': True}

    a = Candidate('A_peer_fairy', (10,), global_saves=1, local_saves=0, automatic_potions=1, hp=20)
    b = Candidate('B_local_loss4', (10,), local_loss=4, hp=16)
    assert min([a,b], key=lambda n: old_key(n,1)).name == b.name
    def owner_aware_key(n):
        return (n.local_saves, max(0,n.local_loss-3), -n.team_alive,
                n.enemy_hp[0], n.local_loss, -n.hp, n.global_saves-n.local_saves)
    assert min([a,b], key=owner_aware_key).name == a.name
    results['R3_owner_priority'] = {
        'current_global_priority': b.name, 'proposed_local_priority': a.name,
        'note': 'owner-aware priority is proposed policy, not a claim of native-game execution'}

    # Exact Advance arithmetic, with root-paid 2, old-cycle future 3,
    # then next-cycle start self-damage 4. Healing adds zero gross loss.
    def advance(budget, loss, ended):
        completed,current,maximum = budget
        current += loss
        maximum = max(maximum,current)
        return ((completed + max(0,current-3),0,maximum) if ended
                else (completed,current,maximum))
    ledger = (0,2,2)
    ledger = advance(ledger,3,True)
    assert ledger == (2,0,5)
    ledger = advance(ledger,4,False)
    assert ledger == (2,4,5)
    assert ledger[0] + max(0,ledger[1]-3) == 3
    assert advance(ledger,0,False) == ledger
    results['existing_HP_ledger'] = {'completed_excess':2, 'current_cycle_loss':4,
                                   'maximum_cycle_loss':5, 'total_excess':3}

    utilities = {'A':(10,0), 'B':(0,10), 'C':(6,6)}
    clairvoyant = sum(max(v[s] for v in utilities.values()) for s in range(2))/2
    shared = {a:sum(v)/2 for a,v in utilities.items()}
    assert clairvoyant == 10 and max(shared,key=shared.get) == 'C'
    results['scenario_prefix_fusion'] = {'clairvoyant_invalid':clairvoyant,
                                       'shared_prefix_values':shared, 'best_shared':'C'}
    return results

if __name__ == '__main__':
    print(json.dumps(run(), ensure_ascii=False, indent=2))
```

## 附录 B：固定提交源码与资料索引

[^C01]: [src/Search/CombatBeamSolver.Phases.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.Phases.cs)，重点：预览 868–934、完成池 1902–1938、最终池与选择 2045–2097、materialize 430–553。
[^C02]: [src/Search/CombatBeamSolver.Multiplayer.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.Multiplayer.cs#L13-L127)，最终排名、三通道与旧前缀重放。
[^C03]: [src/Search/CombatBeamSolver.BeamRetentionPolicy.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.BeamRetentionPolicy.cs#L479-L495)；另核对多人 RankBest／评分分派，而非此大文件的全部单人策略。
[^C04]: [src/Search/CombatBeamSolver.FinalPlanOrdering.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.FinalPlanOrdering.cs#L32-L54)，多人发布资格过滤、重建排序器及结果深度。
[^C05]: [src/Search/CombatBeamSolver.MultiplayerEvaluation.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.MultiplayerEvaluation.cs#L7-L115)，检查点、共同周期、Facts 和最终比较。
[^C06]: [src/Search/SimulatedCombatState.LongTermResources.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/SimulatedCombatState.LongTermResources.cs#L48-L74)，不带玩家归属的死亡阻止资源观察。
[^C07]: [src/Engine/InCombat/Mirrors/Hooks/Death/DeathPreventerMirrors.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Engine/InCombat/Mirrors/Hooks/Death/DeathPreventerMirrors.cs#L8-L69)，实际受益玩家的 Fairy／Lizard Tail 调用。
[^C08]: [src/Search/SimulatedCombatState.Potions.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/SimulatedCombatState.Potions.cs#L10-L138)，使用记录、按玩家库存、消费与补药。
[^C09]: [src/Search/CombatBeamSolver.StateEvaluation.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.StateEvaluation.cs)，重点：65–113、150–155、507–525、603–618、1573–1607。
[^C10]: [src/Search/SimulatedCombatState.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/SimulatedCombatState.cs)，重点：全队根／Fork 420–444、有效敌方生命 1429–1437；另见 [src/Search/SimulatedCombatState.Fork.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/SimulatedCombatState.Fork.cs#L52-L105)。
[^C11]: [src/Search/CombatBeamSolver.Expansion.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.Expansion.cs)，重点：Replay 2670 起、外部选择 2762–2765、周期推进 3140 起／3423–3455、目标枚举 4392–4425。
[^C12]: [src/Search/CombatSearchCoordinator.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatSearchCoordinator.cs#L16-L25)；[src/Search/CombatBeamSolver.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.cs#L44-L58)；[src/Search/SearchPolicySnapshot.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/SearchPolicySnapshot.cs)。
[^C13]: [src/Search/MultiplayerSearchPolicy.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/MultiplayerSearchPolicy.cs#L3-L74)，政策 Apply、七周期与 3 HP 账本。
[^C14]: [tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs#L192-L235)，多人单人能力入口隔离检查。
[^C15]: [src/Runtime/CombatRootSnapshot.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Runtime/CombatRootSnapshot.cs#L137-L225)；毛扣血事件另见 [src/Search/SimulatedCombatState.CardEventHistory.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/SimulatedCombatState.CardEventHistory.cs#L180-L210)。
[^C16]: [src/Search/CombatBeamSolver.MultiplayerRound.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.MultiplayerRound.cs#L9-L155)；真实周期递增和检查点调用见 [src/Search/CombatBeamSolver.Expansion.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.Expansion.cs#L3423-L3455)。
[^C17]: [src/Search/MultiplayerCycleCheckpoint.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/MultiplayerCycleCheckpoint.cs)，不可变原始检查点。
[^C18]: [src/Search/SimulatedCombatState.Multiplayer.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/SimulatedCombatState.Multiplayer.cs#L9-L62)，本机身份、额外回合、外部选择边界。
[^C19]: [src/Prediction/CardOnPlaySupport.Multiplayer.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Prediction/CardOnPlaySupport.Multiplayer.cs)；[src/Prediction/MonsterMoveEffects.Multiplayer.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Prediction/MonsterMoveEffects.Multiplayer.cs)；[src/Prediction/PotionExecutionSupport.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Prediction/PotionExecutionSupport.cs)。此列表不代表全部多人效果只有这些文件。
[^C20]: [src/Search/CombatBeamSolver.Transpositions.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.Transpositions.cs)；[src/Search/CombatPlan.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatPlan.cs#L1060-L1100)，节点账本与政策标签。
[^C21]: [src/Runtime/SolverController.Multiplayer.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Runtime/SolverController.Multiplayer.cs)；[src/Runtime/ContinuationStamp.Multiplayer.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Runtime/ContinuationStamp.Multiplayer.cs)，过期检查、纯路线保存与全队 stamp。
[^C22]: [src/Runtime/SolverController.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Runtime/SolverController.cs)，重点：手动请求 852–875、多人政策／捕获 1145–1179、取消 1601–1635、部署禁止 1641 起、排空释放 2090–2121。
[^C23]: [src/Runtime/SolverController.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Runtime/SolverController.cs#L2332-L2410)，回调完成、当前 session 身份与多人完成分派。
[^D01]: [v0.41.1 Release](https://github.com/sgpsdd-zyx/CombatSolver/releases/tag/v0.41.1)，本次浏览，访问日期 2026-09-18；发布说明不替代运行证据。
[^D02]: [固定提交 MultiplayerEvaluation 原始文件](https://raw.githubusercontent.com/sgpsdd-zyx/CombatSolver/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/src/Search/CombatBeamSolver.MultiplayerEvaluation.cs)，本次与附件本地内容交叉核对。
[^T01]: [tools/OfflineSearchHarness/MultiplayerEvaluationContracts.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/tools/OfflineSearchHarness/MultiplayerEvaluationContracts.cs)，已读共同周期、救援／格挡、铺垫、中断及坏续行相关合同；本次未运行。
[^T02]: [tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/tools/OfflineSearchHarness/MultiplayerStrategyContracts.cs)；[coverage/test-evidence.json](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/coverage/test-evidence.json)，既有策略／账本及结构化历史记录，本次未运行。
[^T03]: [tools/OfflineSearchHarness/MultiplayerContracts.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/tools/OfflineSearchHarness/MultiplayerContracts.cs)；[docs/TEST_MATRIX.md](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/docs/TEST_MATRIX.md)，有限七周期对账和测试范围记录，本次未运行。
[^T04]: [coverage/multiplayer/solo-power-compat.json](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/coverage/multiplayer/solo-power-compat.json) 是 fixture 配置，不是完整原始运行日志；80 项比较来自 [coverage/test-evidence.json](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/coverage/test-evidence.json) 与当前测试说明的历史记录。
[^T05]: [tools/TranspositionFrontierChecks/Program.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/tools/TranspositionFrontierChecks/Program.cs)；[tools/TranspositionFrontierChecks/TranspositionFrontierChecks.csproj](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/tools/TranspositionFrontierChecks/TranspositionFrontierChecks.csproj)；[tools/TranspositionFrontierChecks/README.md](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/tools/TranspositionFrontierChecks/README.md)。512008 为历史检查总数，本次未运行。
[^T06]: [tools/OfflineSearchHarness/MultiplayerStartContracts.cs](https://github.com/sgpsdd-zyx/CombatSolver/blob/5ad98a9b0f8ac50ea9594be877f0fe6d5ea3da77/tools/OfflineSearchHarness/MultiplayerStartContracts.cs#L1-L95)，headless 启动／身份测试的替身与边界，不等于真实网络实测。
