# CombatSolver 多人长线收益源码复审与实验结论

**研究日期：2026-09-18**  
**唯一行为基线：`d55fa84ec07dc252ce62248a9e8b2f4af95effd5`（简称 d55fa84）**  
**游戏语义目标：0.111.0；本轮仅研究，不实施、不推送、不发布。**

## 1. 结论先行：不要打开一个“长线总开关”，也不要立即部署保护席位

本轮完整阅读了新请求 [S00]，按当前任务提交而不是旧 `v0.41.2` Release 阅读源码。最重要的结论是：

|优先级|发现及证据强度|立即可做什么；不能声称什么|
|---|---|---|
|P1|**长线事实的消费是不均匀的，不是整体关闭。** 战斗内通用能力向量仍进入多人评分；未来资源与星能已经参与父内动作族分类，却没有同等直接进入多人总榜／铺垫通道。[S01][S02][S03][S11]|先记录第一次丢失阶段；不能把 `IgnoreLongTermRewards=false` 当修复，也不能称“星能完全没被读取”。|
|P1|**黑暗球储存的激发值在球保路特征中不可见。** `OrbRetentionValue` 读被动值；Dark 的 `_evokeVal` 增长和实际激发由引擎正常建模。[S02][S16][S17]|这是局部估值信息缺口；不是已经证明引擎算错激发，更不是已证明战斗状态键漏掉 Dark。|
|P1|**星能事件、存量、花费量与消费者可达性不能互相代替。** 通用能力估值有覆盖，但几个储君能力只落到泛化分支；BlackHole 的获得事件镜像明确有正数和本机拥有者条件。[S05][S18][S24]|补可解释事实与合法窗口；禁止星能平方、队友星能算本机资源、名义触发次数当已造成伤害。|
|P1，反对证据|**一个条件席位也可能比现役三通道更差。** 本轮 F10 在相同深度、expanded、转移和回放数下，A 最终敌方 HP=20，B/C=50。|本轮 C 不通过默认上线门槛；“只替换铺垫席位所以不退化”已被反驳。|
|P2|**固定预算下多读事实也有覆盖成本。** F12 中 A 到达同周期后的额外攻击，B/C 没有；共同周期相同，短路线仅靠动作数同分项占优。|不把排序位置当质量；没有证据支持无条件追加事实扫描、短 rollout 或第二套搜索器。|
|P2|**第一次丢失可能发生在父内容量，甚至早于 Beam。** 当前父内按动作族保留，C 只接 Beam 救不了已不存在的前缀。[S11]|F11 是抽象边界反例；不能用它声称已经找到某张真实卡在生产父内丢失。|

**优先主方案：证据门控的多人单席位兑现实验——先接入有界“首次丢失”观察，再在离线宿主中验证一个仅替换既有铺垫席位的 C；当前不启用生产选路差异。**

**低成本回退：保持 d55fa84 现役三通道 Beam 和最终比较，移除实验消费者；可保留关闭时零工作量的多人诊断。** B 只是消融对照，不是比 C 更安全的默认修复。没有依据恢复上轮“两步挑战优先”，也没有依据借用官方单人 PowerCommitment。

## 2. 基线、真实读取渠道与证据等级

### 2.1 这不是再次审查旧发布包

用户给出的新 SHA 比上一轮 `2dc5d15…` 发布提交更具体，本轮以 d55fa84 固定文件为权威。新请求明确要求当前分支行为和游戏 0.111.0；旧附件仅用于检索文件名和寻找入口，**未把旧 ZIP 的行为当当前实现，也没有把旧目录冒充本轮解压目录**。[S00]

本轮通过固定提交的 GitHub/raw 页面读取代码。容器网络下载、完整归档入口和若干 blob 请求受限；没有取得 d55fa84 完整源码 ZIP，也没有逐文件完整性校验。因此这里不能重复上一轮“已解压并核对 2,017 个文件”的说法。研究生成文件单独放在 `/mnt/data`；没有修改任何上传的 ZIP 或生产文件。

全文使用 S 编号给出固定源码链接、方法和 **W 行号**。W 是网页提取文本零起始行号，空行处理与物理源文件不同。没有原始字节就不能诚信地给出物理 `#L` 锚点。拿到真实 checkout 后可运行以下**未在本轮执行的定位命令**：

```bash
BASE=d55fa84ec07dc252ce62248a9e8b2f4af95effd5
git rev-parse HEAD
git show "$BASE:src/Search/CombatBeamSolver.StateEvaluation.cs" | nl -ba | \
  grep -E 'OrbRetentionValue|futureResourceValue|IsMultiplayer|StrategicEffectModel'
git show "$BASE:src/Search/CombatBeamSolver.Multiplayer.cs" | nl -ba | \
  grep -E 'RankMultiplayer|PrepareMultiplayerFinalCandidates|PersistentBuffValue'
# 命令仅定位；不能用 grep 命中代替读取上下文。
```

### 2.2 五层证据不能混写

|标签|本轮完成情况|允许的结论|
|---|---|---|
|S：源码事实|当前固定链接与调用链已读取，范围见来源表|某字段被谁读取、某分派是否存在、比较顺序是什么|
|A：抽象验证|**已运行**标准库 Python：17 个主 fixture × A/B/C；另 12 个 Beam=8 控制运行；13 项性质检查|所给有限模型下的可复现反例、因果隔离和算法限制|
|C：生产 C#|**未编译、未运行**；环境无 `dotnet` 和游戏 DLL|不能声称生产入口通过；另附完整待编译函数探针供接入|
|N：原生实际／模拟差分|**未运行**|不能确认 0.111.0 游戏对象时序、牌/球/星能的完整差分|
|M：真实联机|**未运行**|不能确认房主/客机、2–4 人交错、可见性能、胜率或建议采纳收益|

仓库中的历史 27 组三元比较、两周期 Inflame、单人哨兵、七周期对账等是仓库记录，不是本轮重跑的结果。[S19][S27][S28] 本轮 Python 的 720 排列、216 三元检查与历史生产合同是不同对象、不同证明范围。

## 3. 实际调用链：哪些长线信息还在，在哪里被压缩

```text
主线程冻结全队根 / 本机身份 / 本轮已付扣血
  -> MultiplayerSearchPolicy.Apply
  -> CombatSearchCoordinator 多人单会话分派
  -> CombatBeamSolver（_hasRegisteredPowerCards 对多人为 false）
  -> 合法动作、目标和本机选择生成 / 引擎真实模拟
  -> Snapshot：通用能力向量、球、牌流、未来资源、当前星能等
  -> 父内动作族容量 / 支配 / 转置
  -> RankMultiplayer：防御、进攻、铺垫三个有限代表
  -> 继续模拟；敌方周期结算后写不可变检查点
  -> 最终资格过滤 -> 同一共同周期批次 -> 截断 -> 预览/选中
  -> 物化和最终回放 -> 仅手动建议
```

[S01] `Apply` W6–23 明确关掉局外奖励、若干单人目标、宽度组合和 novelty 组合；[S08] W14–23 的多人提前返回使单人能力路线组合不进入该请求；[S07] W52–54 的 `policy.Multiplayer == null` 是单人能力登记/承诺的另一个必要门禁。**这三者不是同一个开关，也不意味着能力牌失去合法动作或模拟效果。**

`PowerCommitment` 与 `StrategicEffectModel` 是不同机制。当前没有依据把用户简写中的两者拼成一个实际的 `PowerCommitmentStrategicEffectModel` 类型；也不能因为前者被隔离，就推断后者不被多人读取。

### 3.1 已算出、已使用、只在部分阶段使用

|事实/特征|当前产生与消费者|本轮判定|
|---|---|---|
|`LongTermResourceValue`、成长/战后奖励|[S02] W192–197 被 `IgnoreLongTermRewards` 控制；另有单人相关目标|局外收益隔离合理，不能为 ROYALTIES 改回|
|`StrategicEffectModel.Evaluate` 的向量|[S02] W242–371 调用并汇入 `PersistentBuffValue`；多人重置总分后仍给 persistent 权重|**多人确实消费通用模型**，但不是完整逐卡承诺系统|
|`PersistentBuffValue`|[S02] W512–519 中间分；[S03] W79–81 铺垫首排序|会保护强泛化特征，却不保证每种延迟机制都有恰当表示|
|`FutureResourceValue`|[S02] W407–413 及快照字段；[S11] W3524–3589 资源动作族；[S15] `CombatProgressState` 进展记录|不是“死字段”；缺口是没有直接进入多人中间总分与铺垫主键|
|当前 `Stars`|[S02] 快照 W577，手牌可达性 W681–709；[S11] 动作族 ResourceAndCycle|已影响合法性/牌流/父内多样性；并非完全忽略星能|
|`DelayedDamageValue`、`OffensiveProgressValue`|[S02] W423–447；父内持续铺垫分类等|已建模部分延迟收益；不等于最终可兑现输出|
|球被动值|[S02] `OrbRetentionValue` W1071–1087|Dark 存储激发值不在该估值的输入中|
|已完整周期的实际 HP、救命次数、敌人有效 HP、存活人数|[S04] W7–15、44–110|最终排序的权威事实；没有额外名义能力奖金|
|零完整周期的候选|[S04] `CompareMultiplayerAtCycle` 的不可比较分支|会回退到中间 Score；所以不能声称“最终选择在任何边界都不读估值”|

B 的实验只模拟一个小变化：再把已有式 `FutureResourceValue` 消费到总分/同一铺垫槽。它没有把所有长线事实恢复完毕，更没有打开局外目标。F17 特别区分“持续能量引擎”与 `EnergyNextTurnPower` 一类下一回合事实：前者可能只有通用层数，B 未必有字段可加。

### 3.2 最终比较不是本轮长线缺口的默认罪魁

当前 `MultiplayerFactsAt` 先识别真实终局，再决定是否使用旧检查点；真实胜利不再被早检查点的 `Won=false` 覆盖。[S04] W44–63。完整发布候选先按用药资格过滤，再创建共同周期上下文，截断和最终选择消费同一批次。[S03] W16–41。

只要某路线在同一已完成周期实际少留敌方生命、没有更高优先级风险，最终比较可以识别其收益。生产合同源码中 `VerifySetup` 已有一周期选 Strike、两周期选升级 Inflame 的具体检查；它反驳了“现役永远不可能选能力牌”。但是本轮没有重跑该合同。[S28] W82–143。

## 4. 按问题逐项复审

### LT-01：已有事实没有贯穿保路，但不能靠全局加权修复

**类型/严重性：S 已证实的消费差异；P1 优化切口，不是已证实实战 bug。**

**触发前提。** 合法能力/资源前缀已经生成；它的当前输出低于攻击，当前防御低于防御代表，`PersistentBuffValue` 低于另一个铺垫代表；优势主要在下一次资源可用或之后的消费者。小 Beam 容量使它在兑现前被删除。

**首个静态切口。** [S11] `SelectActionCandidates` W3390–3506 在父内先限流；通过后，[S03] `RankMultiplayer` W51–95 以三个通道裁剪。[S02] W512–519 的多人中间分不直接加 `FutureResourceValue`。因此需要观察先后：丢在父内的路线不能归咎于后面的 Beam；活到 Beam 才丢的路线也不能通过改最终比较救回。

**最小反例与实际运行。** F01：根有攻击、防御、原有铺垫和目标能量引擎四条动作。A_py 三个代表没有目标引擎；B_py 与 C_py 留住它，三周期敌方 HP 从 66 变 46。F17 将目标的 `future` 改为 0，其后实际资源仍照常产生；B 无法修复，C 可以。这说明“某个字段消费不足”与“当前字段没有表达这类事实”是两种问题。

**反驳/限制。** A_py 没有复制完整父内分类、转置、各类牌流和 87/104 目录；原生某张牌可能通过其他通道留下。这里未证明 Pyre、Genesis 在当前真实输入下一定被剪。F03 与 F10 同时证明加分可能保护无消费者路线或挤掉更好的既有铺垫。

**最小改动与验收。** 首批只把已有候选的阶段、动作前缀、现有字段、保留/删除原因记录到请求内有界多人诊断；同一状态不多做模拟。取得真实目标路线先丢于 Beam 的证据后，才比较保路实验。不得先把所有未来资源塞到分数里，再凭新结果解释旧问题。

### LT-02：黑暗球储存值在局部球估值中失真

**类型/严重性：S 局部观测缺口 + A 最小反例；P1。**

[S16] `DarkOrbMirrors` W16–28 实际维护 `_evokeVal` 的增长，激发读取该值并选择可命中的最低当前 HP 敌人。[S17] `GetPassiveValue` W63–70 对 Dark 给出被动量，`GetEvokeValue` W72–79 有激发入口。[S02] `OrbRetentionValue` W1071–1087 却只消费被动值。

**最小状态对。** 其他缩减特征相同，两状态 Dark 储值分别 12 与 36，被动值都是 6；本轮性质检查显示 A_py 估值相同（-7880）。这证明该特征不能区分储值，不证明其他真实字段、球顺序或引擎状态键也相同。

**路线反例。** F07 的目标球带有合成的已有储值 24，经过一次被动到 30，再合法激发；A 在第一个 Beam 裁剪丢掉它，C 留住，三周期敌方 HP 66→60。**24 不是任何真实新生成黑暗球的卡牌数值**，该小树把“可达根上已有储球”抽象成一个分支；必须在生产宿主构造真正合法的初态和前缀，才能确认现实可达性。

**反对过度修补。** 储值越大不等于本机收益越大：推球可能提前激发，扩容可能反而推迟激发，目标可能被队友先杀，低 HP 目标会产生过量伤害，集中会变动，某些效果会在结束时自动激发。不能把 `EvokeVal` 全额加进最终敌方伤害，不能用库存价值替代一次真实合法激发。

**最小修正候选。** 多人诊断只读自身球序、储值和已经生成的激发/推球动作；保路实验只在存在可解释兑现机会时使用这一原始事实。最终收益只来自引擎已结算后的敌方有效 HP。队友球不能变成本机可控制资源；读取必须来自当前分支影子状态，而非 worker 后读 live 球对象。

**验收。** 两个只差储值的合法状态，验证字段变化、Fork 独立、实际激发对象/伤害/RNG；再做有/无消费者、提前推球、扩容延迟和队友先杀目标的差分。前两项属于 C/N 待验证，不在本轮“通过”清单。

### LT-03：星能库存、事件次数与消费量需要分开

**类型/严重性：S 数据表达限制 + A 事件反例；P1。**

[S05] `Evaluate` W462–533 存在多种专门能力处理，也有默认 `Scaling(amount)`；本轮查到 BlackHole、ChildOfTheStars、Genesis、Pyre 不在该通用 switch 的专门分支中。不能把泛化正分说成零分，也不能把逐卡目录的设计文字当成已被多人完整消费的实现。[S22] W101 明确区分某些逐卡估值合同与生产标量消费者。

[S18] 获得星能镜像有 `Amount>0`、`Gainer == Owner.Player` 条件；这是真实代码证据。**花费侧镜像的当前固定文件本轮读取失败**；其按事件触发 BlackHole、按数量触发 Child 的口径来自当前仓库语义资料 [S24] 和实现讨论 [S22]，还需游戏 DLL/原生差分确认。不能借一个成功的获得 hook 宣称全部花费路径已核实。

**可证伪的量纲反例。** BlackHole 每次正获得事件触发一次：一次获得 4 与两次各获得 2，最终库存相同，触发次数不同。脚本使用合成每事件 3 伤害，得到 3 对 6。Child 的格挡按实际花费数量：库存 8 或 80、当前合法消费者最多花 4、每星 2 格挡，可兑现上限均为 8，不是 16 或 160，更不是平方。

**实际路线实验。** F05 先获得再花费；F06 把正获得事件、花费事件和花费量分账，C 在未来真实消费之后胜出；F15 明确区分 `peer_stars` 和本机 `stars`，不允许把队友库存拿来解锁本机动作。两种资源不是“全队总资源可以等价花”。

**不能由本轮推出。** C_py 没有模拟真实储君所有出牌 hook、封印王座自身触发先后、耗星费用修正、任意费用牌、临时增益或额外回合。以正式卡名装饰玩具节点不会增加证据等级。

**最小设计。** 在多人观察中保留 owner、当前存量、已经发生的正事件计数、已经发生的花费量、已知本机合法消费者。消费者来自动作系统，不从“牌型看起来相关”推断可用。预估只决定探索机会，不替代药水指令、风险顺序或终局实绩。

### LT-04：有限席位的机会成本推翻“保护就能更可靠”

**类型/严重性：A 已运行反例；P1 上线阻断。**

C_py 不增加 Beam，只把原有铺垫通道的一个代表换成窗口候选；防御和攻击代表保持。窗口不读未来真实伤害或奖励。但 **只要多条有价值的延迟路线争一个槽，就可能选错**。

F10 的原有铺垫路线在两次后续动作各造成 35，三周期敌方 HP=20；目标引擎每次 20，敌方 HP=50。A 保留前者，B/C 保留后者。三者均 expanded=16、transitions=19、replay=6、完整三周期，本机累计扣血 6、逐周期无超额。没有“C 搜得更浅”“预算更少”可替这个反例开脱。

因此不推荐：按卡名永久护航、任何“有窗口”就抢席、宣称保住 D/O 代表即可保证不退化、把保护释放条件写成“直到已证明能力自身贡献了净收益”。最后一种还与仓库单人历史经验冲突：[S22] W109–124 记录了过严因果兑现条件使承诺黏滞并导致哨兵退化；这是**历史单人证据，不是本轮多人测量**，但足以反驳无证据照搬。

**处理。** 本轮 C 是可撤回的竞争方案，不是已验收策略。F10 必须作为不可退化哨兵；不能按 `fixture_id`、卡名或特定数值绕过，不能用扩大 Beam 消除后就宣布原方案正确。若不存在成本内可审查的一般条件排除这种替换，保持 A 是有效结论。

### LT-05：小预算会让“排序更好”与“实际工作覆盖更好”分离

**类型/严重性：A 反例 + S 已知残余同分限制；P2。**

F12 同样总上限 31 合成 work，且最终回放都收费。A 的 selected 为 `attack,end,act`，完成周期 1、当前敌方 HP=74；B/C 为 `attack,end`，完成周期 1、当前敌方 HP=82。回看周期 1 时两者敌方 HP 都是 82。现役式比较的主要维度相等，动作少的 B/C 在最后同分项占优。

**这不是首动作质量提升。** 三者首动作全是 attack；A 多完成一次当前周期后的攻击验证。报告和 JSON 专门标记 `comparison_differs_only_in_final_tiebreak=true`，不得把它纳入“C 改善战斗质量”的计数。该例也不是可以随意取消共同周期比较的理由：当前周期的额外攻击不包含之后完整受击验证。

当前指南已承认共同周期外动作数同分问题以及完成池压缩等残余 [S19] W68；本轮没有完整复读所有后半轮转代码，故不新增未经核实的“生产一定丢失已付结果”结论。最小措施是同时报告共同周期实绩、当前前缀实绩、动作截面和阶段成本，不在长线实验中顺手重写终局政策。

### LT-06：父内容量、后缀风险与人类不确定性不能被长线分数抹平

**父内容量。** [S11] `SelectActionCandidates` W3390–3506 的当前动作族次序和容量发生在 Beam 之前；某些已有选择/救命语义有明确例外，不能把上限描述成无例外绝对硬裁。F11 将玩具父内容量缩为 2，目标引擎先被删除，A/B/C 一样失败。真实父内具体路线仍需生产 trace。

**坏后缀。** F08 从同一个目标首动作派生好/坏 `use`，坏分支额外自伤 8；最终可以选择好分支。风险应归属于具体后缀，不能建立“开这个能力就不许再搜”的首动作黑名单。F04 的晚期自损被真实模拟到后，A/B/C 都回到攻击对照；窗口只证明某个资源机会，不证明未来免伤。

**真人未来。** 当前冻结根之后队友不主动动作只是一个条件模型，不是最坏情况。[S19] W19–21。获得星能的队友、被队友杀掉的激发目标、额外抽牌消耗的 RNG 都能改变长线后缀。脚本的共前缀反例：`hold` 两情景收益 (4,4)，`hit` 为 (10,-8)，稳健共前缀最优 4；各情景先选动作再平均得到 7，是不可执行的先知解，不是概率收益。

**RNG。** 固定根 RNG、同 DOP 是对照的必要条件，不是“每条路线永远消费同一个随机事件”。脚本显示增加一个队友随机消费后，后继结果不同；不能把同种子外推成真人先后动作无关。不要在本轮修改公共公平信息屏障或猜测队友未知牌序。

## 5. 0.111.0 语义核对：用于构造合同，不把卡目录当实测

以下名称/数值是当前仓库语义资料中的记载，**不是本轮 DLL 反编译或游戏验证**。数值只服务测试输入核对，不作为玩具树的“真实卡数值”。[S24][S25][S26]

|机制|本轮采用的当前资料口径|需要防止的错误外推|
|---|---|---|
|BlackHole / 黑洞|获得或花费星能的正事件触发伤害；事件与数量分开|不把最终星能余额当触发次数|
|ChildOfTheStars / 群星之子|按实际花费星能量给格挡；资料为每星 2/3|不平方、不超过消费者能花掉的量、不把无效格挡当收益|
|Genesis / 创世纪|下一次本机回合开始产生星能；资料为 2/3|不把“第七敌方周期结束后的第八玩家开始”算进七周期收益|
|TheSealedThrone / 封印王座|启动还要星能，之后出牌获得星能；资料费用为 1→0 能量加 3 星能|不能忽略启动门槛；自身事件先后须原生差分|
|Pyre / 薪火之源|铁甲能力，持续能量；资料为 2 费、以后每回合 1/2 能量|不是机器人卡；持续机制不等于快照一定已有 NextTurn 字段|
|DemonForm / 恶魔形态|持续力量、先付 3 能量；资料为每开始 3/4 力量|开能力时先亏动作/费，只有未来攻击实际兑现才有输出|
|CrimsonMantle / 绯红披风|回合开始自损并得格挡，资料为 -1 HP、7/10 格挡|开始阶段自损属于新周期，不能抵消或刷新已付损失|
|BiasedCognition / 偏差认知|当前资料为 5/6 集中，然后逐回合衰减，可到负值|不要使用一代的 4/5 数值或只看初始名义集中|
|ConsumingShadow / 吞噬暗影|资料有生 Dark 与结束时激发最左球|不能当永不强制激发的纯蓄球；玩家评审并不要求所有此类牌专搜|
|Loop / 循环、Capacitor / 扩容|资料为最右球被动；扩容改变球位，不一定形成 Power 实例|不能套用“最左激发”或“一定能从能力层数捕捉扩容”|
|BulkUp / 暴涨、WhiteNoise / 白噪声|前者丢球位且加属性；后者是技能|不能用 CardType.Power 唯一识别所有球路线或把反协同当纯正分|
|Royalties / 王国资产、ForbiddenGrimoire / 禁忌魔典|纯战后金币/移牌收益|不引入战斗内保护，不能为了局外收益增加本机战损|

不能把玩家评审中的“必剪”“一定强”当概率为 1 的结论。它是需求/经验来源，不是固定代码路径的测量；本轮即时能力控制组和席位负例专门反驳无条件偏爱能力牌。

## 6. 实验实现与真实运行

### 6.1 可执行命令、环境和产物

已实际执行：

```bash
python -m py_compile CombatSolver_Multiplayer_LongTerm_Experiments_20260918.py
python CombatSolver_Multiplayer_LongTerm_Experiments_20260918.py \
  --out CombatSolver_Multiplayer_LongTerm_Results_20260918.json
```

环境 Python 3.13.5；脚本要求 Python ≥3.10，全部标准库，不联网、不读取或修改仓库。JSON 包含完整 fixture 参数、每次生成/保留 trace、最终动作、检查点、成本、性质结果和明确的 `unrun` 清单。输出名可用 `--out` 改；异常/断言失败不会被吞掉。

### 6.2 A/B/C 的精确定义与限制

**A_py** 是缩减适配器，不是调用当前 C#：保留多人相关的即时评分结构、三通道、按周期损失、共同检查点、已知坏后缀风险和终局优先取事实。它不模拟所有牌、真实洗牌、完整转置/支配或生产最终压缩。报告里 A/C 的比较只在这个模型内成立。

**B_py** 在 A 上给 `FutureResourceValue` 乘 20 进入中间分，并让同一个铺垫通道按 persistent+future 排序。20 是复用量级的消融选择，**不是已校准权重或建议上线值**。它不新增席位、不改最终共同周期政策，也不打开局外奖励。

**C_py** 从 A 分支，**不是 A+B**。窗口只读当前玩具资源、owner、已声明消费者、ready_cycle、剩余窗口、已经发生风险；不读目标将来造成多少伤害、原铺垫未来多强、以后会不会自损。C 只替换原有铺垫代表，成功使用或无窗口即释放。没有隐藏第四个席位。

窗口对玩具动作语法是合法性判定，但对生产不是免费证书：F13 的 `ready_cycle=5` 是输入中声明的可见规则；它不证明真实牌会留在手里五周期、抽牌一定如愿、玩家可平安活到那时。因此 C_py 的结果是“保护窗口这个概念能否被反驳”，不是 native 实现已经就绪。

默认 Beam=3、父内上限=8、最多 expanded=80、work=240、DOP=1、根 seed=20260918。F12 work=31；F13/F14 horizon=7、work=340。每个 fixture 的 A/B/C 配置完全一致。Beam=8 只用于额外机制控制，不是生产扩宽提案。

### 6.3 工作量完整口径

合成 work = 根捕获 1 + 每展开父节点 1 + 每条模拟边 1 + 额外选择分支 1 + B/C 每条生成边的事实读取 1 + 最终回放每边 1。预留回放上限 `2*horizon+3`，未使用不计消耗。所有变体共用同一预留规则。

这是一种公开、可复现的有限操作预算，**不是 C# expanded 的单位换算、不是测得的 CPU 成本、也不是墙钟公平测试**。B/C 的额外特征单位不等价于真实扫描耗时；窗口检测的常数工作归入该模型单位，未精细模拟排序 CPU。各运行有 5 秒安全截止，但本轮运行远低于它；它不是“5 秒固定墙钟策略质量实验”。

`transpositions_pruned=0`、`dominance_pruned=0` 等明确表示模型没有这些生产阶段，不能解释为它们通过了零缺陷检验。主树中的 RNG 只是按动作推进的确定性状态计数，随机结果不影响这些树的战斗收益；另有独立的随机消费差异性质检查，不能据此声称做过随机战斗质量测试。`team_alive=2` 和 `potions=0` 是本组树的固定背景，记录了要求的维度，却没有测试真实救援或药水引擎。峰值 frontier 是候选数，不是原生峰值内存。JSON 的比较字段特意区分 `C_vs_A_comparator_order` 与 `C_vs_A_combat_interpretation`；F12 的前者为 better、后者为 tiebreak_only_NOT_combat_gain。

### 6.4 完整主结果

下表 enemy HP 是所选路线当前已模拟状态；共同周期可不同，F12 必须结合专门解释。`engine` 是目标延迟路线，`decoy` 是旧铺垫代表的代码标签，**不意味着它价值低**。

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

**不能报告“C 9/17 获胜”。** 在这组特意构造的抽象树中，8 个有已模拟战斗实绩改善，1 个明确退化（F10），1 个只在同分项占优但证据覆盖退化（F12），其余 7 个不变。它们不是随机抽样战斗，不能换算成胜率、总体提升比例或置信区间。

### 6.5 每个机制的动作、首次丢失与验收含义

|fixture|最小动作/机制|第一次丢失或关键边界|应如何解释|
|---|---|---|---|
|F01|engine → end → use → end → use → end；下一阶段资源转攻击|A 在 d1 Beam 丢目标；B/C 进入最终并被选|已有 future 消费可救回一种情况|
|F02|先上成长，第二周期还不回本，第三周期多次攻击兑现|A/B 在 d1 丢；C 留下|两步/两周期探针不必足够；第三周期有价值|
|F03|有名义未来资源，没有任何消费者|B 留进池后最终输；C 不保护|资源量不等于输出，避免盲目加分|
|F04|延迟引擎后续有自损，攻击对照安全|真实坏后缀在最终输；不抹风险|窗口不是七周期生存证书|
|F05|先获 2 星能，再得 2，然后花 4|A/B 在首裁丢；C 等到可花才得伤害|获得与花费阶段分开|
|F06|正获得事件、正花费事件、花费 4 生成格挡|收益只在 transition 后进入 HP/敌方 HP|事件次数与花费量独立|
|F07|已有 Dark 储值，先被动增长再激发|A/B 首裁丢；C 留下|球 reservoir 不是当回合伤害|
|F08|同一个 engine 首动作分出 use 与 bad_use|坏分支不能封禁好分支|不要建立根动作黑名单|
|F09|即时能力已有输出，后续自然继续|所有变体通过进攻等原通道保留|不为能力名额泛化抢席|
|F10|目标与旧铺垫争唯一 setup；旧铺垫未来更好|C 把好旧铺垫删除|不可退化哨兵，阻断默认部署|
|F11|父内只接 2 个动作|首丢 parent_cap；后面评分无权恢复|先修正确阶段，不能硬塞 Beam|
|F12|总 work=31，包含事实读取和回放|预算停止；共同周期相同、动作截面不同|不把少做工作导致的同分项优势算质量|
|F13|已声明第 5 周期后消费者，6 周期真实胜利|A 7 周期未赢，B/C 6 周期终局|终局事实优先；生产窗口可得性仍待证|
|F14|消费者在第 7 敌方周期后的玩家开始才可用|C guard 拒绝超窗保护|七周期末不是赠送一个玩家回合|
|F15|资源属于队友，没有本机消费者可花|owner guard，不给 C 席位|本机/队友资源不能合并|
|F16|未来需要队友选择|ExternalPlayerChoice 明确停止|不能猜选项或伪造完成受击|
|F17|持续引擎只有 generic persistent，future=0|B 无字段可救；C 可救抽象路线|不等于所有持续能力有 NextTurn 字段|

F02 的手算检查：对照每轮 8，三周期共 24；成长路线第一轮投资不打，第二轮 12、第三轮 15，共 27。第二轮结束时投资线仍落后（12<16），第三轮才反超。这是反驳“只到两个周期的局部差分足以处理所有长线”的最小例，不是需要无限树的论证。

F06 在所选 C 路线中，JSON 分别记录 3 次正获得、1 次正花费、实际花费 4；BlackHole 样式事件伤害与消费动作伤害分开结算，格挡只抵御相应敌方来伤。没有把名义格挡重复加到最终得分。

### 6.6 所有主运行的成本与首次丢失表

|fixture / 变体|expanded|transitions|choice|父裁 / Beam 裁|特征读取|最终回放边|work / 上限|首次丢失|
|---|---:|---:|---:|---|---:|---:|---|---|
|F01 / A|16|19|0|0 / 1|0|6|42 / 240|beam/d1|
|F01 / B|16|19|0|0 / 1|19|6|61 / 240|未丢失，最终选中|
|F01 / C|16|19|0|0 / 1|19|6|61 / 240|未丢失，最终选中|
|F02 / A|16|19|0|0 / 1|0|6|42 / 240|beam/d1|
|F02 / B|16|19|0|0 / 1|19|6|61 / 240|beam/d1|
|F02 / C|16|19|0|0 / 1|19|6|61 / 240|未丢失，最终选中|
|F03 / A|16|19|0|0 / 1|0|6|42 / 240|beam/d1|
|F03 / B|16|19|0|0 / 1|19|6|61 / 240|final_comparison|
|F03 / C|16|19|0|0 / 1|19|6|61 / 240|beam/d1|
|F04 / A|16|19|0|0 / 1|0|6|42 / 240|beam/d1|
|F04 / B|16|19|0|0 / 1|19|6|61 / 240|final_comparison|
|F04 / C|16|19|0|0 / 1|19|6|61 / 240|final_comparison|
|F05 / A|16|19|0|0 / 1|0|6|42 / 240|beam/d1|
|F05 / B|16|19|0|0 / 1|19|6|61 / 240|beam/d1|
|F05 / C|16|19|0|0 / 1|19|6|61 / 240|未丢失，最终选中|
|F06 / A|16|19|0|0 / 1|0|6|42 / 240|beam/d1|
|F06 / B|16|19|0|0 / 1|19|6|61 / 240|beam/d1|
|F06 / C|16|19|0|0 / 1|19|6|61 / 240|未丢失，最终选中|
|F07 / A|16|19|0|0 / 1|0|6|42 / 240|beam/d1|
|F07 / B|16|19|0|0 / 1|19|6|61 / 240|beam/d1|
|F07 / C|16|19|0|0 / 1|19|6|61 / 240|未丢失，最终选中|
|F08 / A|16|19|0|0 / 1|0|6|42 / 240|beam/d1|
|F08 / B|16|20|1|0 / 2|20|6|64 / 240|未丢失，最终选中|
|F08 / C|16|20|1|0 / 2|20|6|64 / 240|未丢失，最终选中|
|F09 / A|16|19|0|0 / 1|0|6|42 / 240|未丢失，最终选中|
|F09 / B|16|19|0|0 / 1|19|6|61 / 240|未丢失，最终选中|
|F09 / C|16|19|0|0 / 1|19|6|61 / 240|未丢失，最终选中|
|F10 / A|16|19|0|0 / 1|0|6|42 / 240|beam/d1|
|F10 / B|16|19|0|0 / 1|19|6|61 / 240|未丢失，最终选中|
|F10 / C|16|19|0|0 / 1|19|6|61 / 240|未丢失，最终选中|
|F11 / A|11|14|0|2 / 0|0|6|32 / 240|parent_cap|
|F11 / B|11|14|0|2 / 0|14|6|46 / 240|parent_cap|
|F11 / C|11|14|0|2 / 0|14|6|46 / 240|parent_cap|
|F12 / A|9|12|0|0 / 1|0|3|25 / 31|beam/d1|
|F12 / B|5|8|0|0 / 1|8|2|24 / 31|final_comparison|
|F12 / C|5|8|0|0 / 1|8|2|24 / 31|final_comparison|
|F13 / A|40|43|0|0 / 1|0|14|98 / 340|beam/d1|
|F13 / B|39|42|0|0 / 1|42|13|137 / 340|未丢失，最终选中|
|F13 / C|39|42|0|0 / 1|42|13|137 / 340|未丢失，最终选中|
|F14 / A|40|43|0|0 / 1|0|14|98 / 340|beam/d1|
|F14 / B|40|43|0|0 / 1|43|14|141 / 340|final_comparison|
|F14 / C|40|43|0|0 / 1|43|14|141 / 340|beam/d1|
|F15 / A|16|19|0|0 / 1|0|6|42 / 240|beam/d1|
|F15 / B|16|19|0|0 / 1|19|6|61 / 240|beam/d1|
|F15 / C|16|19|0|0 / 1|19|6|61 / 240|beam/d1|
|F16 / A|16|19|0|0 / 1|0|6|42 / 240|beam/d1|
|F16 / B|13|16|0|0 / 1|16|6|52 / 240|final_comparison|
|F16 / C|16|19|0|0 / 1|19|6|61 / 240|beam/d1|
|F17 / A|16|19|0|0 / 1|0|6|42 / 240|beam/d1|
|F17 / B|16|19|0|0 / 1|19|6|61 / 240|beam/d1|
|F17 / C|16|19|0|0 / 1|19|6|61 / 240|未丢失，最终选中|

同一个 fixed budget 意味着上限一致，不要求每次恰好烧完预算；提前终局或显式边界可以用更少工作。某个方案若占用事实读取工作而少扩展父节点，本身就是需要记录的代价，不能把其额外扫描排除成本来“配平”。

### 6.7 Beam=8 控制与 13 项性质

额外对 F01/F07/F10/F17 各跑 A/B/C 的 Beam=8，总计 12 次。四种树中 A/B/C 分别都选 engine(46)、engine(60)、旧铺垫 decoy(20)、engine(46)。这支持“这些玩具正/负例的区别确实来自小 Beam 竞争”，但不支持违反用户预算去扩宽实际搜索。

性质检查实际通过：同根同预算；Dark 储值的局部不可见；3+3 与 0+6 不能共用额度；星能正事件次数；消费格挡线性上限；终局优先于早检查点；完整周期不兑现 future 10000；固定候选池全预序（720 排列、216 三元）；窗口不读取未来奖励或风险；无消费者不保护；共享前缀非先知；同 seed 不同随机消耗；非时间输出/重放确定性。

这些是脚本中的独立可检查断言。它们不证明当前 C# List.Sort 的所有同分排列完全稳定；玩具额外用动作序列做确定性 tie-break，不能悄悄归功于生产。

## 7. 当前修复复核：保留，不重新开旧工单

|上一轮问题|d55fa84 当前证据|本轮处理|
|---|---|---|
|最终先截断后药水过滤|[S03] W16–31 已先资格再建批次/截断|不重复报旧 R1|
|同次选择反复换共同周期|[S03] `MultiplayerFinalBatch` W14、33–41；[S04] W17–40|不重复报旧 R2；跨池压缩仍是不同问题|
|胜利旧检查点遮蔽|[S04] W47–63 终局分支已保留完整事实|本轮终局性质检查使用已修口径|
|本机无损满血胜利导致多人提前停止|[S20] 实施记录、[S19] W67 记载已取消|本轮未逐行重审后半 Phases，不能冒充重跑通过|
|根前队友用药放宽本机要求|[S20]、[S19] W37、67 记载本机 Actor 口径修正|不重报旧输入 bug；根后救命归属仍另列|
|坏后缀污染根动作|当前比较按具体节点晚期风险；本轮 F08|保留风险证据、不加根动作黑名单|
|单人能力策略误入多人|[S07] W52–54、[S08] W14–23；[S27] W183–226 有隔离合同|保留隔离；不把缺少专搜当成应撤销门禁|

全队救命次数仍在本机超额之前、跨池局部压缩与混合深度、最后已付父候选接收边界、旧预览和动作数截面等残余，仓库已有明确条件记录。[S19] W68、[S20] W28–32、[S21]。它们可能干扰长线实验，应被 trace 标注；**本轮没有新生产证据就不把它们包装成新修复列表，也不在长线补丁中顺带变更风险政策。**

## 8. 生产验证交付及未验证清单

另附 `CombatSolver_Multiplayer_LongTerm_ProductionProbe_20260918.cs`。它使用当前合同里的真实 `ReplayMultiplayerForTesting`、`SearchNode` 构造、`RankMultiplayer`、`PrepareMultiplayerFinalCandidates` 接口形状；只接受宿主给出的同根合法动作前缀，输出真实快照特征、注入候选的保留结果、最终共同周期和回放成本。

**它未编译、未运行，不是已经验证可运行的生产合同。** 需要加入已有 OfflineSearchHarness 程序集并由有 DLL 的宿主调用；不应自动复制到生产。它不能证明动作生成器确实产生了某路线，也不能替代实际首次丢失 trace；更不含 C 的原生合法窗口实现。反射 API 不匹配会明确抛错，不静默当成相等/通过。SHA 参数由宿主传入也不等于自动验明二进制，仍要记录真实构建来源。

当前指南给出以下可复跑入口，[S19] W120–125、169–176；**以下命令本轮均未执行**：

```bash
# 在已确认固定 SHA、已配置 0.111.0 本地引用的研究 checkout 执行；禁止自动部署。
dotnet build CombatSolver.csproj -c Release -p:CopyModOnBuild=false
dotnet build tools/OfflineSearchHarness/OfflineSearchHarness.csproj -c Release
dotnet tools/OfflineSearchHarness/bin/Release/net9.0/OfflineSearchHarness.dll \
  --multiplayer-strategy-contracts --encounter FUZZY_WURM_CRAWLER_WEAK \
  --beam 2 --nodes 100 --budget-ms 1000 --dop 1 --out .local/mp-longterm-baseline
```

这是已有合同的基线复跑，不会自动覆盖新增 Dark/星能场景。新增探针没有虚构的 CLI 开关；应从既有多人合同内部调用，并将同根合法路线作为参数。若没有 DLL/冻结根，它就保持待验证，不能生成带“PASS”的假 JSON。

仍缺：真实三机制合法路线与选择生成、球位/集中/激发时序、获得与花费所有 hook、native RNG 全文对账；完整父内/转置/完成池首丢 trace；生产 C 同预算质量；取消排空/释放计数；固定墙钟质量和内存；官方单人完整哨兵差分；真实房主/客机及 2–4 人交错。读取失败或未系统复读的当前文件包括花费侧 `CardPowerOnPlaySupport.cs` / `AfterCardPlayedMirrors.cs`、Terminal 后处理完整链、HarnessOptions/Program 全部参数、全部 Runtime 捕获实现、完整状态字段门禁与全目录 coverage。**不以旧文件或历史结果补空。**

## 9. 最终行动判断

预期收益最明确的是“让真实长线机会被看见并保留到已模拟兑现”，而不是更高评分或更深节点数。本轮证明了当前局部观察确有盲区，也证明了最简单的 B/C 都有失败方式。

因此，本轮只能支持：保留现有主搜索器与终局政策，新增最小多人丢失证据；用同一生产根验证一个可撤回 C；只在收益与机会成本门禁都通过后启用一个多人保路消费点。**当前 C 的 F10 门禁未通过，默认选路继续 A。** 这既不是放弃长线研究，也不是把玩具优点当作已批准上线。


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

固定输入为 `d55fa84ec07dc252ce62248a9e8b2f4af95effd5`、游戏 0.111.0、本轮脚本/JSON/报告。首批只允许一个最小生产改动：在 `src/Search/CombatBeamSolver.Multiplayer.cs` 的多人 `RankMultiplayer` 入口/出口记录有界“候选首次丢失”诊断，输出已有动作前缀、现有快照字段、保留原因和阶段；诊断必须只由多人请求创建、关闭时不分配，不新增候选、模拟、预算、缓存、状态键或行为开关。优先复用现有诊断输出能力，无法满足原总预算时只放宿主探针，不做生产接线。

可回滚实验只在 `tools/OfflineSearchHarness/MultiplayerLongTermContracts.cs`（新文件）和现有 `MultiplayerStrategyContracts.cs` 的多人测试调用处实现 A/B/C 同根对照；可先接入附带的待编译函数探针，但不得把它的注入候选当实际生成证据。至少构造 Dark 储值/合法激发、储君获得后实际花费、普通持续能力三类原生根；在父内输出与 Beam 输出之间确认第一丢失点。C 只替换既有铺垫代表，无消费者/队友资源/外部选择/超窗不保护；还未证明合法的未来窗口必须标 Unknown。首批不要启用生产 C。

禁止修改单人 `StrategicEffectModel`、PowerCommitment/PowerRoutes、共享评分/Beam 参数、药水政策、终局顺序、转置、续用或自动执行；不得为通过实验增大 Beam、节点、时间、内存，不得更新单人哨兵期望值。不得实施上轮两步挑战、rollout、主动队友情景或第二个 Planner。

验收先运行本文已执行的 Python 命令，再在具备 DLL 的环境运行上节现有构建/多人合同命令，并追加新合同；报告分别给出 C#/native/联机证据级别，缺哪个就写哪个。F10 旧铺垫机会成本、F12 小预算覆盖、单人入口零进入和完整动作/选择/状态/RNG 哨兵是停止条件：任一退化即停用/回滚本批多人差异，不改期望、不按卡名或 fixture ID 特判。没有真实同预算收益或窗口证书时保留 A。此次任务文字不是当前会话实施授权；本研究不修改仓库、不推送、不发布。
