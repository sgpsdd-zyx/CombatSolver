# 官方 0.50.0 合并时保留的研究记录

来源：官方 `0d290fbe`，2026-10-05 归档。以下为上游阶段记录，保留原版本、失败、未达目标和原始意见；不是本 fork 本轮测试或部署凭证。当前实现见 [架构地图](../../../ARCHITECTURE.md)，本轮验证见 [合并记录](../../strategy/upstream-0500-merge-20261005.md)。

# 原生回复来源的组件审计：间接来源与回调

接续[第一阶段入口审计](native-health-source-audit-20261003.md)，复用合入上游 `56b6d6ee` 后已验证并部署的 `2c5234ee` 工作。本阶段只补充来源证明，没有修改运行时资格、评分、预算或搜索顺序。通用组件证书尚未接入生产，也没有新增性能验收结论。

## 版本与证据范围

游戏仍为 `v0.111.0` / `41cef1ea`，DLL SHA-256 为 `2b40d2df538db1ceb5fa48d958c80ab730ada1e07db88a870aff01a661768b9f`，MVID 为 `8a76776c-0ce1-4d4f-90bd-8cce653dad8e`。使用现有 IL 清单的 8,340 个模型虚方法、静态调用、委托和异步映射，定向读取实际安装 DLL 的源码；不以治疗文案或 `Heal` 字段作为判据。

[配套 JSON](native-healing-component-audit-20261003.json) 保存 80 项来源类型的提取哈希、44 项具体机制的目标、时点、条件、次数、重复性、来源和剩余证明义务，以及回调和工厂调用清单。原版源码与失败提取日志只留在忽略目录。一次把 SpoilsMap 误写在 Relics 命名空间的提取失败保留；成功来源是 `Models.Cards.SpoilsMap`，没有从失败推导安全结论。

“具体效果已审查”只覆盖注明的方法。直接玩家回复量为零，仍可能产生可回血的牌、药水、遗物或触发其他监听者；只有这些依赖全部闭合，才能把它组合成严格上界。静态无路径、原版程序集身份和已经取得源码均不表示认证通过。

## 固定选牌与普通生成的区别

知识恶魔的三个选择波次为 `[Disintegration, MindRot]`、`[Disintegration, Sloth]`、`[Disintegration, WasteAway]`。对存活玩家明确创建临时牌后调用 `IChoosable.OnChosen`，不走普通出牌或随机生成池。四张牌的 `CanBeGeneratedInCombat` 均为 false，因此不能借八池随机过滤将其排除。

| 选择及后续 Power | 原生结算 | 剩余证明义务 |
| --- | --- | --- |
| Disintegration | 给持有玩家施加 6/7/8 层；所属侧回合晚期造成自身非受增伤影响伤害 | 伤害、死亡、保命和受伤回复监听者必须组合 |
| MindRot | 1 层；减少持有者手牌抽取量，最低为零 | 抽牌及进场回调、初始与取回来源 |
| Sloth | 3 层；按开始出牌计数限制，持有者侧回合开始归零 | 自动牌与重放同样计数，Fork/历史不能换成手动完成次数 |
| WasteAway | 1 层；降低持有者最大能量 | 其他能量结算回调 |

四条选择本身不增加玩家生命；知识恶魔的 Ponder 仍治疗敌人自身 30×玩家人数并加力量。以上方法阅读没有替代完整敌方选择原生/模拟差分，也没有认证整只敌人。

FranticEscape 同样不能普通随机生成，但 TheInsatiable 会明确生成它。出牌找到指向持有玩家的 SandpitPower，加一层并增加自身本场费用。Sandpit 的移除会强制击杀目标玩家及 Osty，必须保持胜负和保命目标，不能因为没有回复就跳过死亡语义。

## 宠物生命、复活与玩家回调

当前原版没有 `AfterSummon` 或 `ModifySummonAmount` 覆盖；`AfterOstyRevived` 唯一覆盖为 SandpitPower，其该方法只更新 UI 位置。该事实不排除其他阶段或扩展监听者。

`AfterCurrentHpChanged` 的五个原版覆盖是 MeatOnTheBone、RedSkull、NecroMasteryPower、Crusher、Rocket。前两项沿用已审查的门槛状态和力量变更；后两项只对自身负变化播放受伤动画。NecroMastery 只在自己玩家的 Osty 生命变化为负时，对可命中的敌人造成 `-delta×Amount` 伤害。因此宠物回复本身不是玩家回复，但宠物受伤导致敌人死亡，仍能触发其他已持有的回复资源。

所有判断需使用正确的 Owner、PetOwner、Target 和分支状态。任意 Power 恰好具有原版类型，不能覆盖错误的玩家持有目标。既有再生仍按玩家有效层数计算；药水禁用、额度耗尽不删除已生效再生。

## 战斗结束、永久牌组与奖励

定位了 45 个原版 `AfterCombatEnd` 覆盖。限定的静态调用图中，只有 ChosenCheese 通向生命写入口；六项经过其他派发或反射入口。该图用于找依赖，零路径不是零回复证明，未逐项审查的正文仍未认证。

| 来源 | 当前原生行为和次数 | 与回复及搜索边界的关系 |
| --- | --- | --- |
| ChosenCheese | 每次结束回调增加玩家生命上限 1，并同步治疗实际增量 | 已有修复保持未知总闭包的保守上界；没有忽略加上限 |
| RoyaltiesPower / HeistPower | 分别在结束或拥有者死亡时增加金币奖励 | 添加奖励不等于 `GainGold`；奖励领取在当前战斗搜索外。资源追回目标仍保留 |
| ForbiddenGrimoirePower | 结束时增加 Amount 份移除卡牌奖励 | 未立即执行永久牌组移除；奖励领取另审 |
| ImprovementPower | 用 CombatCardSelection RNG 随机选取至多 Amount 张可升级的永久牌组卡 | 无直接回复；升级方法、附着效果和完整 RNG 状态仍需闭合 |
| FishingRod | 普通怪物房间每第三次结束，使用 Niche RNG 升级一张永久牌组卡 | 需保留计数、升级与附着链，不能凭静态无生命路径自动放行 |
| PaelsTooth | 玩家存活且保存列表非空时，用 Rewards RNG 返回、升级并加入一张保存牌，随后从列表移除 | `Add Deck` 能触发 BookOfFiveRings、DarkstonePeriapt；需要牌组、反序列化和回调来源证明 |
| ToyBox | 每第三次有效结束熔化第一件未熔化蜡遗物，直到用尽 | `Melt` 调用 `AfterRemoved`；当前原版遗物没有该方法覆盖，基类为空实现，熔化链本身不回复。获得时的五份随机遗物奖励仍需单独证明 |
| Guilty | 只有实际位于永久牌组时累计战斗数，达到五次移除自身 | 战斗临时副本不执行此效果；原生成证书的前提不能直接外推到初始永久牌组 |

当前 `BeforeCardRemoved` 的唯一原版覆盖是 SpoilsMap：只在移除自己、章节与坐标匹配时删除地图任务标记，不调用金币命令。其独立的 `OnQuestComplete` 才会获得金币，因此 Guilty 的移除回调不会隐式完成这个任务。章节任务不在当前战斗搜索边界内；外部回调仍须拒绝或适配。

当前 `AfterCardChangedPiles` 有八个覆盖，其中包括 BookOfFiveRings 和 DarkstonePeriapt；加入永久牌组和加入战斗牌堆不能混为同一入口。YummyCookie 在获得时升级四张永久牌组卡，不调用休息治疗；仍须审核获得和升级可达性。

## 敌方新增来源的递归边界

直接调用清单定位到 18 类怪物的造牌来源：明确状态牌为 Wither、Dazed、Slimed、Burn、Toxic、Infection、Beckon、FranticEscape、Wound，另有上述知识恶魔固定选牌。敌人动作调用之外，PersonalHivePower 明确生成 Dazed，PainfulStabsPower 明确生成 Wound。这些入口同样要经过进场、牌堆及附着回调。

敌人类型中的直接泛型施加引用覆盖 62 种 Power。10 种可复用相同 DLL 的 C58 来源哈希，其余复用已有原生提取或新增读取；这只表示取得来源证据。尚未复核的模型、间接生成和未知目标继续拒绝，不把“62 个直接类型”当成全部可达集合。

| Power | 直接结算 | 闭包要求 |
| --- | --- | --- |
| Infested / Stock / Surprise | 拥有者死亡后分别新增 Wriggler、Axebot、SneakyGremlin/FatGremlin；Stock 子实例层数减一 | 新个体初始化、动作、Power、生成牌和新增回调递归审查 |
| Adaptable / SteamEruption | 进入 TestSubject / WaterfallGiant 的后续状态 | 后续动作和生命重设对象另审，不从当前方法零回复推导整个阶段安全 |
| Illusion / Reattach | 分别补满敌人自身或回复拥有者敌段 | 玩家错误持有未知；死亡、复活和其他玩家持有的回复监听者仍组合 |
| Suck | 根据攻击结果给自身加力量 | 该方法不吸血；Power 施加和其他受伤监听者仍需审查 |
| ChainsOfBinding / Galvanic / Hex / Ringing / Smoggy / Tangled / VitalSpark | 分别附加 Bound / Galvanized / Hexed / Ringing / Smog / Entangled / Tainted | 附着效果是独立组件，不能只认证生成它的 Power |

药水工厂有 14 条定位引用，`TryToProcure` 有 12 条；没有直接怪物调用。此事实不排除经卡牌、遗物或回调间接获取。EntropicBrew 使用战外池的已有结论保持，不能套用战斗生成过滤。RelicCmd.Obtain/Replace 执行实际 `AfterObtained`，获取链没有形成通用证书时保留完整余量。

## 固定根的实际覆盖与首批组件目标

追加[29根来源诊断](../../../performance/native-healing-root-coverage-20261003.json)，使用已验证 DLL `89a8e2c2…` 的独立离线根诊断进程；不运行 Solve，不把诊断耗时或内存作为性能验收。先取得的两个目标根直接复用，另外27根只捕获一次。29个根文本全部与既有最终回归相等，live状态均未变化。

严格剩余回复环境资格为 **6/29**，六根都取得有限的根上界；已知原生来源策略资格为 **29/29**。旧的固定战后回复资格为0/29，其首个拒绝理由分布为药水13、角色8、卡牌7、玩家Power1。该旧理由不是严格证书逐组件的拒绝原因，后者目前没有完整诊断入口。

复用原29根回归的选定结果计数：21根记录了累计5,922次主胜利界剪枝。它包含已知来源策略，不是严格证书独占计数，也不是整个协调器所有成员的总计；原始DLL与逐根路径单列。根捕获耗时含克隆和JIT，不能当作认证本身开销。此批没有重新跑性能对照。

| 目标根 | 诊断发现 | 下一步边界 |
| --- | --- | --- |
| `dev-10-ironclad-monster` | Nibbit；全部初始牌在C58生成集合或已有AscendersBane证明内，无初始成长牌；持有BurningBlood/EternalFeather/Cauldron/Brimstone及ColorlessPotion/BottledPotential | 原生Nibbit全类、Brimstone、Cauldron和两药水已定向审查，仍需组合全部监听来源、生成与政策证明，不能仅凭这些目标都不直接治疗就认证 |
| `holdout-04-necrobinder` | 原生千足虫；THE_SCYTHE成长目标；HiddenGem和AscendersBane不在普通可生成集合；SpeedPotion/BottledPotential | 现有策略明确保留成长评价，纯战损主界与提前计划都受成长门禁；不能通过删除目标获得提速。SpeedPotionPower未在C58 Power集合中，其依赖尚未审查 |

本次额外读取八个目标来源的哈希单列在覆盖JSON：Nibbit只攻击、格挡及施加力量；Brimstone每个玩家侧回合施加力量；Cauldron获取时提供药水奖励，普通模式与TestMode的奖励生成不同。BottledPotential是洗回手牌、洗牌并抽五张，**不生成牌**；ColorlessPotion按原生解锁、玩家数与战斗过滤取得三个不同候选并选择或跳过。两者仍须保留进场/牌堆/抽牌回调、RNG和原始未知来源。

## 九项审计的状态与下一步

直接牌/药水、持续和触发回复、战前战后、上限增长、复活重设均有[第一阶段](native-health-source-audit-20261003.md)及[金币与上限修复](../../../performance/gold-max-hp-healing-20261003.md)的当前版本证据。本轮推进间接生成、敌人/宠物、战外边界和回调；全部九类均已有调查入口与明确未审范围，**完整可达来源审计尚未完成**。

下一批组件认证优先组合已有八池和已审查敌人动作、遗物、药水、Power、附着证明，逐来源取零、有限值或未知。未知初始来源即使暂时离场或在消耗堆仍永久拒绝。多份再生、药水额度、成长、条件回复及复活资源沿用既有政策；无法证明其他目标安全时继续原搜索。

## 储君首领候选来源（2026-10-04）

为尚未达到两倍的 `dev-08-regent-boss` 定向读取当前安装DLL的TheInsatiable、VexingPuzzlebox、MercuryHourglass、MembershipCard、SwiftPotion及SpeedPotionPower六个完整类型，并复用同哈希的SpeedPotion、TemporaryDexterityPower、SandpitPower和FranticEscape源码。提取时游戏SHA与上述固定版本相同；各项源码哈希、对象、时点、条件、次数、依赖和未完成证明保存于配套JSON的 `regentBossCandidateSourceReview`，不扩大运行时资格。

TheInsatiable的初始行动明确给玩家生成六张FranticEscape，三张进抽牌堆、三张进弃牌堆；不能用 `CanBeGeneratedInCombat=false` 排除它。Sandpit在敌方侧开始递减，移除时可强制击杀玩家及Osty；其目标与死亡/保命回调仍须完整差分。VexingPuzzlebox在拥有者第一回合开始，从原生角色解锁池经GetDistinctForCombat生成一张本回合免费牌；MercuryHourglass每个拥有者回合开始伤害可命中敌人，MembershipCard只影响商店价格。SpeedPotionPower沿TemporaryDexterityPower的施加、数量变动和回合结束移除链调整敏捷；SwiftPotion给目标玩家抽三张牌。上述命名效果直接玩家回复量为零，生成、伤害、死亡、抽牌及Power监听闭包不能因此省略。

该冻结根十八类普通初始牌及AscendersBane的初始/永久生命周期必须独立准入，不能直接把C58随机生成集合变成默认初始牌白名单。尚未完成这些组合证明、新原生合同或候选性能筛查；零回复认证能否带来收益仍是待测假设。

本阶段新增运行时证书为 0。现有环境的根资格覆盖已诊断，旧主界剪枝计数已按其原版本复用；逐组件拒绝计数、严格证书独占剪枝和认证开销尚未测量。新审查机制也尚未新增原生/模拟合同或整请求性能测试。只做根来源、文档结构和数据口径检查，复用未变化源码的已验证构建及五文件部署，不重复跑既有成功场景。下一候选须先完成相关最小原生差分，再进行无插桩固定根性能对照。

追加隔离组合证据：上述初始来源已逐项复核完整声明效果及永久生命周期，并在独立原型中显式准入十八类牌，另处理敌人明确生成的FranticEscape。新`COMPONENT-REGENT-BOSS`合同通过原生药水/敏捷移除、七项出牌、遗物过滤生成/伤害、Liquify、Frantic及Sandpit强制死亡的完整状态/Fork/RNG差分，并拒绝错误Power对象及未知消耗堆来源。它复用已有同版本八池/回调前提，未给其他未知原始来源默认认证。完整请求只获得1.20倍原始提速，故不扩大生产资格；先前“待组合”的记录是该来源审查阶段的历史状态，当前新证据与适用范围见配套JSON的`regentBossIsolatedCompositionContract`及[性能报告](component-healing-bound-20261003.md)。最终交错、全固定回归和原生目标整场仍未执行。


## 亡灵首领的隔离组合证据（2026-10-04）

同一DLL的十八项初始卡牌、Osty生命与召唤回调、四遗物、Shackling/CureAll药水和TheInsatiable动作形成显式组合。GainMaxHp/SetMaxHp/Heal的对象为Osty，已有玩家受伤/死亡/击杀及生成链前提仍需全部满足；Transfigure只修改持有手牌的费用/重放次数，CureAll只加能量和抽牌。未审来源保持拒绝，没有按原版程序集自动放行。

`COMPONENT-NECRO-BOSS` / `c8a5cb87dfca4d52ab527d77f496a877`，29.18秒Passed，覆盖药水原生结算、临时力量恢复、召唤物增血、灵魂与遗物回调、选择/重复执行、明确状态生成与流沙玩家/Osty死亡，以及完整状态/Fork/父/live/RNG；两次夹具合法参数失败分别保留，实例均清理。源码审查和完整合同只用于隔离证明，完整请求42.37秒未达原始两倍，未推广到生产。具体来源哈希、七类机制条件和未验证项见配套JSON及[性能记录](component-healing-bound-20261003.md#亡灵首领组件闭包2026-10-04)。

## 提前胜利见证的分支合同（2026-10-04）

隔离DLL`6aad0784…`复用上述亡灵来源组合，未增加组件默认准入。`EARLY-HP-BOUND` / `b6d8528c2acb4fc396cc058c3ecf7feb`在27.79秒Passed：实际完整胜利保留并更新见证，DOP1/DOP16严格结果，较差生命分支实际剪枝、同战损和未知消耗来源保留，禁药下已有再生及父/live/Fork/RNG隔离，原生Offering完整状态相等；成长、遗物、追回资源与强制用药拒绝特化。该最小合同不代表所有完整搜索的质量或性能通过；同根完整初筛仅1.112倍，且此前零战损原型结果变为4，未推广生产。原生结果、失败构建和未验证范围见配套JSON的`earlyVictoryHpBoundContract`及[性能记录](component-healing-bound-20261003.md)。

## Smart 开局的完整胜利见证传递（2026-10-04）

复用同一亡灵来源组合，不新增默认认证。隔离`SMART-OPENING-WITNESS` / `1336ac4aa23f4274be8a832dd37ac99e`在27.44秒Passed：真实有界无药搜索未获胜与真实完整用药胜利分别产生，保留获胜路线并只传标量上界，严格DOP1/DOP16、实际2次剪枝、原政策门禁及CureAll原生完整状态/父/live/Fork/RNG。两次此前夹具未显式传搜索配置而Failed，修正后显式Beam24/1200节点/10000毫秒，无药控制仅1节点；120秒启动器帽，三个实例均清理。既有提前胜利夹具同样实际使用Default，行为证据保留但不声称1200节点。正式性能宿主显式传参。整请求42.86秒、0战损/1瓶/13回合，仅原始1.063倍，未纳入生产；完整来源、失败和限制见配套JSON与[性能记录](component-healing-bound-20261003.md)。

## 女王实际冻结根与前置计划合同（2026-10-04）

隔离DLL`90c907a7…`复用女王组件原型的Components/CombatRootSnapshot同哈希源码，改动仅为既有前置计划采用当前严格回复证书。`QUEEN-OPENING-SCHEDULE` / `959cc284419f453eba8a119d10fb2752`，43.25秒Passed：真实Coordinator新认证根、前置/计划各一次、首个成员胜利界、DOP1/DOP16严格质量/父/live/RNG，以及SpectrumShift原生完整状态/Fork；实例删除。独立无Solve诊断首次确认该原型的实际冻结女王根证书成立、回复界0、完整根匹配，不把生产f266的BundleOfJoy拒绝沿用为此原型的拒绝，也不外推全语料覆盖。完整请求405.60秒NoWin，速度及质量失败，没有推广运行时资格；具体范围见配套JSON及[性能记录](component-healing-bound-20261003.md)。

## 猎手无药完整见证的计划传递（2026-10-04）

复用同一版本已通过的StableSerum留牌与Forge升级零回复闭包，不放宽未知初始来源/附件/生成资格。隔离DLL`c6bef5ca…`的`SILENT-EARLY-HP` / `4fc9d3a11a1348dcb0069f4871171533`在28.94秒Passed：真实完整无药胜利通过已有战略资格助手形成标量，严格DOP1/DOP16、实际31次回合内剪枝，真实Coordinator跨计划传递1次，并验证两种药水顺序/两项完整原生状态/Fork/父/live/RNG。直接搜索显式Beam24/1200节点/10000毫秒，Coordinator20000毫秒及宽度12/8；共享30秒取消、120秒启动器帽，实例删除。此前updates1/pruned0的27.01秒Failed保留，未执行v2不记为成功合同。整请求20.22秒仅原始1.837倍，未纳入生产或扩大7/29严格覆盖；逐次记录见配套JSON与[性能记录](component-healing-bound-20261003.md)。

后续主/精炼及能力成员的隔离传递合同：DLL`fe20001e…`，`SILENT-EARLY-HP` / `35d32dea0b714e2182b8e118cfa6aa50`，30.99秒Passed。新增已审Mayhem以实际运行能力路线，DOP1/DOP16严格搜索29次剪枝，真实Coordinator计划/能力传递3/8次，原生两药水完整状态/Fork/父/live/RNG。复用同版本早期界政策/未知/同战损证据，不重复执行来源未变的成功合同。实际冻结整请求20.04秒仅原始1.853倍，节点和转移与上版一致，仍未纳入生产或扩张覆盖；结果及适用前提见配套JSON的`silentMemberHpSeedContract`与[性能记录](component-healing-bound-20261003.md)。

## 快照填充及复用前的诊断范围（2026-10-04）

隔离快照布局DLL`72376d66…`没有新增回复来源准入；`SILENT-EARLY-HP` / `78168715494e435fa9e3cd13c0908902`在30.09秒Passed，DOP1旧List填充对DOP16同步Span填充，非空四牌堆、严格结果/真实35次剪枝、计划/能力接线、原生两药水顺序/完整状态/Fork/父/live/RNG，30秒取消/启动器120秒/实例删除。整请求22.48秒仅原始1.652倍，未纳入生产。

新增重复转移抽样比对2011次跨成员的记录父/结果状态、完整规范化history/trace及快照公开属性，未发现差异；该抽样步骤仅诊断、没有缓存，也不是原生缓存差分或全部隐藏状态完整性证明；后续隔离合同见下一节。插桩改变成员工作数，观察纯度和原因仍待核对，不把汇总未超时冒充所有预算派发不变。具体前提、拒绝/跳过、未知项及CPU采样限制见配套JSON与[性能记录](component-healing-bound-20261003.md)。


## 读取纯度与释放态缓存的有限原生合同（2026-10-04）

当前原生DLL版本/MVID及生产组件资格不变，认证覆盖仍7/29。既有状态诊断少掉两个精炼成员，已从原结果定位为插桩堆占用触发`MemoryHeadroomInsufficient`；首个能力成员81节点/374转移的增量仍未解释，不把选中质量相等当整次纯度证明。

`TRANSITION-CAPTURE-PURITY` / `ab368a6c0cea437ca416f2b96c644398`、DLL`cfdb2689…`、27.33秒Passed：完整history/trace、续用戳及快照公开属性反复读取后未来动作状态不变，16份独立Fork/改血，Adrenaline/Defend/Mayhem三个实际原生完整状态、父/live/RNG隔离。此前构建未执行的通知作用域审查与最终13.70秒零警告/错误构建分开保留。

独立请求缓存采用稳定快照释放后的所有权移交，锁内Fork，不直接共享可变模板。`TRANSITION-DONATION-MEMO` / `08730d3bad504b0f8083496fc4a5986e`、DLL`8191ade5…`、27.37秒Passed：51个真实命中、16路独立HP修改、完整history/快照/状态、FIFO、不同根/setup/政策、取消及Dispose/晚到移交；三个原生状态相等。增加Coordinator后的DLL`ee447e01…`，run`5285e6187c8948cbba341dbfc6bb6ad7`、31.44秒Passed，显式Beam24/1200/10000毫秒、Coordinator20000毫秒宽12/8、DOP16严格增量，记录1899真实hit、计划/能力传递3/8、32次剪枝、同战损完整无药胜利及两药水顺序/完整原生状态/父/live/RNG。原生阶段取消20秒、共享搜索取消30秒、启动器120秒，实例均删除。构建/准备失败及来源哈希单独保留，不复跑输入未变的成功合同。

缓存根/动作前缀/政策键、移交和有界数量只属研究证明；回复组件证书本身不能证明所有未来机制的转移同余。全部允许来源的确定性闭包、实际对象图字节上界仍未完成。无插桩整请求22.99秒仅原始1.615倍、Win40/0瓶/4回合；另单独流式诊断12921命中/193555次尝试、大量FIFO淘汰，不作验收时间。未纳入生产或部署，未新增回复机制认证。详细输入、三份原生结果和性能/内存限制见配套JSON及[性能记录](component-healing-bound-20261003.md)。


## EndTurn 初始 capture 与长历史复用的有限合同（2026-10-04）

复用前述原生DLL版本及猎手来源证明，不改变生产组件准入或7/29覆盖。三版隔离合同 DLL/run 分别 `ff6c8f44…`/`a44b0ff7cc3f438490b0cd3f3e7d6262`、`89120730…`/`e11c31daf629480ba8f489b674107cfe`、`a4b81358…`/`f55af80fdb6649aeaaf36eb799728f5b`，33.56/33.12/35.31秒Passed。前两版各34个直接命中/两项完整原生EndTurn，末版85个命中/五项完整原生EndTurn、history83条；覆盖Mayhem自动牌/抽牌洗牌/敌方/RNG、完整history与快照、16路独立改血Fork/父live模板、容量/根setup政策取消Dispose。实际严格DOP16 Coordinator分别1079/1132/1204命中、3/8计划/能力传递及43/40/43次生命剪枝，完整胜利和两项原生药水状态不变。

初始EndTurn capture扩大仅限稳定None且实际消费者无选择层；挂起checkpoint/capture续接仍原路。普通Fork延迟到miss，严格完整回放仍执行。最初长历史夹具四回合仅64条，`6a5f5e9870e54d85877b40822bb340bf`在覆盖断言Failed，未算通过；第五回合83条通过，实例全部删除、120秒帽。各源码/build/hash/失败和小预算见配套JSON新增三项合同。

完整冻结请求三版22.89/21.62/21.09秒、原始仅1.622/1.717/1.760倍，均未纳入生产。独立诊断最后337hit/114897聚合拒绝，非认证覆盖；构键累计elapsed约2.25秒含锁等待及并行重叠，不代表完整请求开销。固定前缀reset经源码核对保留完整动作父链；全允许来源键闭包、移交前所有调用者不推进状态及模板字节界仍未完成，没有以回复证书代替缓存确定性证明。具体结果及未验证项见[性能报告](component-healing-bound-20261003.md)与配套JSON的`roundDonationMemoIsolatedContract`、`stableRoundCaptureMemoIsolatedContract`、`longHistoryRoundMemoIsolatedContract`、`transitionMemoCostIsolatedDiagnostic`。


## 全部原版 AfterCombatEnd 正文与通知边界（2026-10-04）

按当前安装DLL的45个实际覆盖补齐正文审查，新增36个模型、11个命令/状态/通知依赖的成功提取，复用9份既有正文。重新读取的原生DLL仍为SHA-256 `2b40d2df538db1ceb5fa48d958c80ab730ada1e07db88a870aff01a661768b9f`、MVID `8a76776c-0ce1-4d4f-90bd-8cce653dad8e`；36份正文提取23.64秒、0失败。逐项记录对象、时点、条件、次数、重复性、可达来源、实际源码/正文哈希和方法行号，保存在配套JSON `afterCombatEndBodyReview`。这补齐了45项**正文审查**，没有新增运行时证书，也不代表全部回复来源审计完成。

| 当前原生正文分组 | 数量 | 直接行为与仍需组合的来源 |
|---|---:|---|
| 自身状态/计数/引用及UI复位 | 35 | 固定正文及已读原生通知处理本身无玩家生命写入；其他模型方法、任意订阅者、Harmony及完整根仍须分别认证 |
| `WongosMysteryTicket` | 1 | 推进CombatsFinished/RemainingCombats；另一个奖励回调在同玩家/战斗房间/未用尽且>=5场时添加Repeat（默认3）份随机遗物奖励。领取可获得加生命遗物，在当前战斗动作搜索之外，不能称为永不产生回复来源 |
| `ChosenCheese` | 1 | 结束回调GainMaxHp（默认1）并回复实际上限增量；先于条件胜利回复，可能改变MeatOnTheBone门槛，保持未知组合的保守处理 |
| `FishingRod` / `ImprovementPower` / `PaelsTooth` | 3 | 永久牌组升级；后者还反序列化并返还保存牌、加入Deck。分别保持Niche/CombatCardSelection/Rewards RNG。OnUpgrade、附着、动态变量、Upgraded及Deck添加回调不能略过 |
| `ToyBox` | 1 | 每个符合计数的结束事件至多熔化一件蜡遗物；原生Melt通向AfterRemoved（已有原版无覆盖/基类空方法证据）。完整模型的取得奖励及扩展仍另审 |
| `ForbiddenGrimoirePower` / `RoyaltiesPower` | 2 | 只向房间列表增加移除牌/金币奖励，不立刻移除或调用GainGold；后续领取与原有成长/资源目标另审 |
| `Guilty` / `IllusionPower` | 2 | 前者只在永久Deck计数并移除自己，临时战斗副本不进入分支；后者仅对已死拥有者播放动画，本方法不执行复活或Heal。完整来源和其他回调继续保守 |

原生命令链实际顺序为：`EndCombatInternal`设置IsInProgress=false、清额外回合及阶段；逐玩家`ReviveBeforeCombatEnd`对已死者调用Heal(Creature,1)；`Hook.AfterCombatEnd`逐监听者等待并调用`InvokeExecutionFinished`；清历史/房间收尾/玩家Power清除；`AfterCombatVictoryEarly`全部监听者，再`AfterCombatVictory`；之后房间/存档与奖励领取。正常胜利检查用IsInProgress防止再次结束，任意直接调用/重入不在本证明中。战后Heal(1)不能把当前死亡搜索分支重新认作完整胜利，尤其多人边界要单独审查。

`CardCmd.Upgrade`虽有IsEnding门，但此时IsInProgress已false，`IsCombatEnding`返回false，因此不能利用该门把战后升级误认为不可达。`UpgradeInternal`实际调用虚方法OnUpgrade、DynamicVars.RecalculateForUpgradeOrEnchant、Upgraded。CardPileCmd.Add至永久Deck还经过ShouldAddToDeck、ModifyCardBeingAddedToDeck及AfterCardChangedPiles；MeatOnTheBone等回调不是唯一需考虑的生命路径。

没有把字段赋值当作无回调：RelicModel.Status调用StatusChanged，显示/Flash各自调用委托。实际IL中的四条原生订阅位于Player.AddRelicInternal和NRelicInventoryHolder.OnModelChanged；已读处理器改音效/视觉、颜色/着色器、计数文本及粒子，相关属性和UpdateDisplay正文一起核对。Hook结束后的ExecutionFinished原生订阅恢复手牌选牌UI；Upgraded另被CombatStateTracker和手牌UI订阅。前者延迟执行RecalculateCardValues并派发CombatStateChanged，这个后续派发边界仍未闭合；原生订阅清单不能排除任意委托、反射、外部Mod或补丁。

本轮仅记录实际正文和明确命令/通知链，不新增默认安全名单或生产覆盖。全部45项分别保留“正文已审/完整组件尚未新认证”的状态；其他战斗开始/胜利/受伤/死亡/生成回调、永久牌组与获得来源、有限重复界、扩展和新原生差分仍待完成。生产严格覆盖仍7/29，性能收益没有从静态阅读中推导。

## FIFO 回合尾复用的隔离合同（2026-10-04）

仅把上一长历史缓存的首批保留改为FIFO，4096模板、65536前缀、256条history及8Mi字符界不变。DLL`acd543c1…`构建27.93秒零警告/错误；`ROUND-FIFO-DONATION-MEMO`/`cf35dfddee9d4a7d82cb7d657895b3c4`在34.60秒Passed：五项原生Mayhem EndTurn/完整history81、85直接命中/16路独立改血Fork、五槽FIFO最老模板淘汰及重放miss状态一致、根/setup/政策/取消/Dispose/父/live/RNG。实际严格DOP16 Coordinator777命中、3/8计划/能力见证传递、35次生命剪枝、完整无药胜利和原生两瓶药水保持。小预算/取消/120秒帽沿用，私有实例删除、启动器退出0。完整冻结请求21.1466秒、仅原始1.756倍，未纳入生产；单独诊断耗尽前缀键上限，不能以命中增加替代完整速度验收，详见[性能记录](component-healing-bound-20261003.md)。


女王历史路线另作两个模拟器版本的只回放诊断，复用85项结构化动作与同一冻结根；原基线与f266均Win67/0瓶、13/80生命，最终continuation文本/全部公开快照属性相同，父/live不变。没有原生游戏执行、没有向正常搜索注入路线或新增来源证书；原档回合12与两次直接回放13的报告差异保留。首宿主在根之前因旧DLL setter不兼容Failed，使用已兼容stage1宿主后两次成功；不能把初始化失败写成路线失败，也不能以回放成功解除正常NoWin阻断。具体范围见配套JSON `queenRecordedRouteReplayDiagnostic`与[性能记录](component-healing-bound-20261003.md)。


## 后续组合研究（2026-10-04）

继续以约1.853倍的猎手精英基底组合小措施，最终仍要求整请求至少2倍。七项普通请求初筛20.04～22.87秒均未达标；原生合同、实际剪枝计数及失败记录见[重场景组合研究](heavy-scene-compositions-20261004.md)和配套JSON。最早胜利界正常请求触发31次，但耗时22.23秒，完整阶段回调闭包未完成，不能扩大生产证书。所有原型保持隔离，严格认证7/29、本机合格前版及女王质量阻断不变；按用户要求暂停女王丢路调查。
