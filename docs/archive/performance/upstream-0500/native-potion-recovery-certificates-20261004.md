# 官方 0.50.0 合并时保留的研究记录

来源：官方 `0d290fbe`，2026-10-05 归档。以下为上游阶段记录，保留原版本、失败、未达目标和原始意见；不是本 fork 本轮测试或部署凭证。当前实现见 [架构地图](../../../ARCHITECTURE.md)，本轮验证见 [合并记录](../../strategy/upstream-0500-merge-20261005.md)。

# 全药水回复证书：当前原生版本的统一清单

接续[原生回复审计](native-healing-component-audit-20261003.md)，按用户要求先完整审查当前 DLL 的药水集合，再组合搜索根；不再按单个场景的持有药水临时补表。[结构化证据](native-potion-recovery-certificates-20261004.json)保存每个类型的作用对象、使用时点、来源哈希、效果 Power、条件与未验证项。

## 范围与分类

实际安装游戏 v0.111.0 / 41cef1ea，DLL SHA-256 `2b40d2df538db1ceb5fa48d958c80ab730ada1e07db88a870aff01a661768b9f`，MVID `8a76776c-0ce1-4d4f-90bd-8cce653dad8e`。64 个原版药水模型及 DeprecatedPotion、MockDiscardAndAddShivsPotion 两个辅助模型全部入账。58 个具有条件零玩家回复证明的组件，占64个原版类型的90.625%；这不是完整根认证覆盖率，也不表示已执行全部药水的原生动作。

审查包括 OnUse、继承的 OnUseWrapper、351 个原生虚方法入口、25 个关联 Power 的源码与115个已声明虚方法、TemporaryDexterity/Strength 与 PowerModel 基类、药水生成工厂、获得/丢弃/使用及手空回调。直接生命调用仅是定位入口；认证仍逐项检查生成、重复、取回、召唤、回合、死亡和完整回调依赖。

## 组合条件

零回复组件只在现有 StrategicHpRecoveryBound 根闭包认证通过后使用。永久牌组、全部牌堆（包括消耗）、待返回牌、卡牌附着、Power、遗物、敌人及扩展回调均需已审计；未知类型仍拒绝。按精确 Type 与锁定 MVID 组合，缓存只有不可变类型元数据，分支剩余药水、额度和已生效再生从拥有的状态读取。原版程序集身份本身不作为证明。

自动牌、重复执行、升级、回收或取回不能掩盖初始回血牌。普通生成继续使用原生角色/无色池、解锁、人数及 CanBeGeneratedInCombat 过滤；Feed、NotYet、Alchemize 不属于普通可生成集合，但初始持有、复制和回收独立处理。EntropicBrew 在战斗中也调用 OutOfCombat 工厂，可能得到回血、涨上限、Fairy 或再次 EntropicBrew；当前候选继续拒绝该机制。

再生保留已有层数与所有合法剩余剂量的叠加上界，禁药/额度只排除无法合法使用的剩余药水。Blood、Ambergris 的有限量仍须证明 MaxHP 及回复修正回调闭包，Fruit 保留成长目标，Fairy 保留自动触发及资源消耗评价。上述特殊模型尚未因本清单放开严格根认证。评分、预算、Smart/禁药/强制用药/反事实及成长、战后回复、保命目标均不改变。

## 最小原生合同与失败

隔离候选 DLL `a470322915f0a4bedf285cfb6e0df94aa57e99b8e69d65f27b4c21faaf50d046`，从已验证 fe200 基底构建，14.73秒、零警告/错误。`COMPONENT-SILENT-BOSS` / `14eef45cfeb04fc7bb137f9cec2a8c08` Passed，120秒上限，私有实例已删除。实际 ModelDb 持有根覆盖58个零回复模型及5个特殊来源拒绝；另覆盖禁药/额度、未知消耗堆 Feed 拒绝及错误 Sandpit owner 拒绝。

实际 Fortifier（现有格挡变为三倍）和 LuckyTonic 的原生完整状态差分通过；8张初始牌的实际出牌、固定 Shiv 生成、MasterPlanner/Sly、Reflex 抽牌、Power 实例、两回合 TheInsatiable/Sandpit/FranticEscape、毒与巨石及 Sparkling 回调通过。16个并行独占 Fork 的完整状态、父/live 和 RNG 隔离通过。完整合同标记保存于 JSON；没有把58个持有根测试写成58次实际原生用药。

失败保留：首次构建有两个测试 helper 编译错误；首次启动把怪物 ID 当遭遇 ID，未进入测试；第二次根包含 TheHunt，被既有成长守卫拒绝。后者证明成长门仍生效；独立药水夹具移除 TheHunt 后通过，原 dev-06-silent-boss 仍拒绝，没有放宽成长守卫来取得速度。

## 当前限制

运行时清单已纳入拟提交候选，0.49.1 魂枢的最小原生用药／完整回调与最终 Smart 合同通过，整请求串行对照见[魂枢证据](soul-nexus-0491-research-20261004.md)。尚未执行64种药水的全部实际使用及每种扩展 Power 的最终完整回调闭包；其他27根固定回归通过；独立诊断完整根严格组件覆盖3/27、包括既有认证7/27，暖态单次中位0.0003～0.1002毫秒，详见魂枢报告；这不代表全部原版根或每节点认证开销。不能用整个无人合同耗时代替认证开销，也不能把58种模型的最小认证合同当作全部64种实际使用通过。

## 逐类型清单

| 原生类型 | 分类 | 机制与约束 |
| --- | --- | --- |
| `Ambergris` | 直接回复＋额外回合 | 一次回复实际 MaxHp × HealPercent / 100，原版50%；AmbergrisPower 额外回合可能触发其他已生效回复 |
| `Ashwater` | 条件零回复 | 选择并消耗手牌；消耗回调、全部牌堆与回收闭包 |
| `AttackPotion` | 条件零回复 | 原生角色攻击池筛选、解锁及人数约束、CanBeGeneratedInCombat、递归生成 |
| `BeetleJuice` | 条件零回复 | 敌方 Shrink；攻击倍率及施加者死亡移除，不改生命 |
| `BlessingOfTheForge` | 条件零回复 | 升级全部手牌；升级、卡牌变更与附着回调 |
| `BlockPotion` | 条件零回复 | 目标玩家获得格挡；格挡回调 |
| `BloodPotion` | 有限直接回复 | 一次使用回复实际 MaxHp × 实际 HealPercent / 100；原版值20%；需证明未来 MaxHp 上界及治疗修正闭包 |
| `BoneBrew` | 条件零回复 | Osty 召唤／宠物生命；不是玩家回复；召唤回调 |
| `BottledPotential` | 条件零回复 | 手牌移入抽牌堆、洗牌、抽牌；牌堆与抽牌回调 |
| `Clarity` | 条件零回复 | 抽一张牌并施加 ClarityPower；后续回合抽牌量加一并递减次数；抽牌与自动执行回调 |
| `ColorlessPotion` | 条件零回复 | 原生无色生成池与递归生成；选择、临时费用 |
| `CosmicConcoction` | 条件零回复 | 原生无色池生成三张并升级；人数／解锁／可生成过滤及升级回调 |
| `CunningPotion` | 条件零回复 | 显式生成升级 Shiv；固定令牌与升级回调 |
| `CureAll` | 条件零回复 | 获得能量并抽牌；名称不表示治疗 |
| `DeprecatedPotion` | 停用模型，拒绝 | 废弃的历史占位模型，继承基础 OnUse；不纳入可用药水安全集合 |
| `DexterityPotion` | 条件零回复 | DexterityPower；格挡加成 |
| `DistilledChaos` | 条件零回复 | 自动执行抽牌堆顶多张牌；全部牌堆、重复执行及生成闭包 |
| `DropletOfPrecognition` | 条件零回复 | 选择抽牌堆的牌移入手牌；牌堆回调 |
| `Duplicator` | 条件零回复 | DuplicationPower；增加出牌执行次数，不能忽略初始回复牌 |
| `EnergyPotion` | 条件零回复 | 获得能量 |
| `EntropicBrew` | 随机药水来源，暂不认证 | 战内调用 CreateRandomPotionOutOfCombat，能产生回复、加生命上限及复活药水；可能继续产生 EntropicBrew；无限显式额度无法证明有限上限，保持保守 |
| `EssenceOfDarkness` | 条件零回复 | 按实际球槽数量引导 DarkOrb；引导／激发回调 |
| `ExplosiveAmpoule` | 条件零回复 | 敌方伤害及死亡回调 |
| `FairyInABottle` | 自动保命 | ShouldDie/AfterPreventingDeath 自动消耗并回复 max(实际 MaxHp×30%,1)；不是显式用药，保留复活资源比较；当前运行时保持保守 |
| `FirePotion` | 条件零回复 | 敌方伤害及死亡回调 |
| `FlexPotion` | 条件零回复 | FlexPotionPower；临时力量与到期移除 |
| `FocusPotion` | 条件零回复 | FocusPower；球效果及激发回调 |
| `Fortifier` | 条件零回复 | 目标玩家获得其当前格挡的两倍；增加格挡，不增加生命 |
| `FoulPotion` | 条件零回复 | 战内伤害全部非宠物，包括玩家；商店金币／事件效果属于搜索边界外 |
| `FruitJuice` | 生命上限与成长 | GainMaxHp 同时增加当前生命，原版5；有限回复与生命上限成长目标均需建模，当前运行时保持保守 |
| `FyshOil` | 条件零回复 | StrengthPower、DexterityPower |
| `GamblersBrew` | 条件零回复 | 选择弃牌并抽牌；Sly 自动打出与全部牌堆闭包 |
| `GhostInAJar` | 条件零回复 | IntangiblePower；减伤及到期移除 |
| `GigantificationPotion` | 条件零回复 | GigantificationPower；攻击倍率、攻击对象内部状态及用后递减 |
| `GlowwaterPotion` | 条件零回复 | 消耗当前手牌后抽牌；消耗、抽牌、自动执行回调 |
| `HeartOfIron` | 条件零回复 | PlatingPower；格挡及受伤递减 |
| `KingsCourage` | 条件零回复 | Forge；SovereignBlade 生成、成长目标及升级回调保持原规则 |
| `LiquidBronze` | 条件零回复 | ThornsPower；敌方反伤及死亡回调 |
| `LiquidMemories` | 条件零回复 | 从弃牌堆回收并临时免费；初始／消耗／待返回牌不能失去认证检查 |
| `LuckyTonic` | 条件零回复 | BufferPower；减少一次战损，不是治疗 |
| `MazalethsGift` | 条件零回复 | RitualPower；按回合获得力量 |
| `OrobicAcid` | 条件零回复 | 角色攻击、技能、能力各一张；原生解锁／人数／可生成过滤与递归闭包 |
| `PoisonPotion` | 条件零回复 | PoisonPower；敌方持续伤害与死亡回调 |
| `PotionOfBinding` | 条件零回复 | 敌方 WeakPower、VulnerablePower |
| `PotionOfCapacity` | 条件零回复 | 增加球槽；球槽改变回调 |
| `PotionOfDoom` | 条件零回复 | 敌方 DoomPower；死亡阈值与死亡回调 |
| `PotionShapedRock` | 条件零回复 | Token 药水；敌方伤害，初始实际持有与普通生成分开处理 |
| `PotOfGhouls` | 条件零回复 | 显式生成 Soul；抽牌与递归执行闭包 |
| `PowderedDemise` | 条件零回复 | 敌方 DemisePower；回合末伤害与死亡回调 |
| `PowerPotion` | 条件零回复 | 原生角色能力池筛选；人数／解锁／可生成及递归生成 |
| `RadiantTincture` | 条件零回复 | 即时能量与 RadiancePower 的后续能量 |
| `RegenPotion` | 有限再生 | 玩家再生；已有再生及所有合法剩余剂量累加后用三角和保守上界；禁药／额度只排除未使用剂量 |
| `ShacklingPotion` | 条件零回复 | 敌方临时力量降低及到期恢复；不是生命重设 |
| `ShipInABottle` | 条件零回复 | 当前格挡及 BlockNextTurnPower |
| `SkillPotion` | 条件零回复 | 原生角色技能池筛选；人数／解锁／可生成及递归生成 |
| `SneckoOil` | 条件零回复 | 抽牌并随机化手牌费用；费用变化与抽牌回调 |
| `SoldiersStew` | 条件零回复 | 所有实际 Strike 标签牌 BaseReplayCount 增加；重复执行与初始来源闭包 |
| `SpeedPotion` | 条件零回复 | SpeedPotionPower；临时敏捷及到期恢复 |
| `StableSerum` | 条件零回复 | RetainHandPower；保留手牌 |
| `StarPotion` | 条件零回复 | 获得星星；资源目标保持原规则 |
| `StrengthPotion` | 条件零回复 | StrengthPower |
| `SwiftPotion` | 条件零回复 | 抽牌；全部牌堆和抽牌回调 |
| `TouchOfInsanity` | 条件零回复 | 选牌永久战内免费；不变牌；费用变化回调 |
| `VulnerablePotion` | 条件零回复 | 敌方 VulnerablePower |
| `WeakPotion` | 条件零回复 | 敌方 WeakPower |
| `MockDiscardAndAddShivsPotion` | 测试模型，拒绝 | 测试模型；不因属于原版 DLL 就认证为原版药水 |
