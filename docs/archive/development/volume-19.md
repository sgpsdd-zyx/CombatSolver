# CombatSolver 开发笔记历史卷 19

[返回归档索引](README.md)

## 0.49.4 发布定稿（2026-10-05）

版本为 0.49.4，玩家说明采用 [0.49.4 更新日志](../../releases/0.49.4-RELEASE_NOTES.md) 的完整中英文正文。渠道发布结果以 `releases/CombatSolver-0.49.4.publish-state.json` 的统一发布记录为准。

### 问题上传引导（2026-10-05）

- 搜索期间的原生输入按已有 player / solver / system 来源记录。每次搜索单独保存玩家输入标记；录制明细不完整时仍更新该标记。手操导致的结果过期记录为 `ManualSearchResultStale`，显示重新计算提示；来源不明的过期继续保留反馈提示，同场其他诊断问题保持原分类。
- Runtime 根据本场实际角色、战斗牌堆及牌组、遗物、怪物、Power、药水、附魔、灾厄与球的模型来源保存上传引导资格。发现第三方内容后，本场搜索初始化、计算、部署和回合准备的普通异常保留真实错误与类别，统一省去上传提示；反馈横幅、路线详情和全自动暂停提示使用同一资格。新战斗重新判断。
- 上传策略独立于模拟准入；仅安装框架、局外 Mod 或本场未出现的新怪物不影响原版问题的反馈。设置中的主动上传入口继续可用。
- 日志站只读查询确认：观者 `4e74450ff819415bb96c14b9211aa6c7` 在第三方姿态镜像中发生普通计算异常；过期报告 `71d726926a2249efad12730b4cc05fa8` 的 generation 46 搜索期间有玩家出牌及结束回合输入。本轮调整上传引导，未复跑观者战斗或修改姿态语义；账号只读权限未回写线上报告。
- 最终行为源码的最小原生合同 `CONTENT-MOD-FAILURES` Passed，runId `73fa6ecba0ff47ee9adf777a84abd640`，25.508 秒；验证明细见 [测试入口](../../TEST_MATRIX.md)。玩家说明见 [0.49.4 更新日志](../../releases/0.49.4-RELEASE_NOTES.md)。

### 第三方额外回合来源登记

`ExtraTurnMirrors` 为 `ShouldTakeExtraTurn` / `AfterTakingExtraTurn` 开放第三方登记（泛型与按 `Type` 两种）。
原版龙涎香、帕尔之眼与第三方来源按原生监听顺序统一派发；`HookMirrors.ShouldTakeExtraTurn` 在首个 true 处结束判断，
`AfterTakingExtraTurn` 固定成员后依次结算并处理选择暂停。搜索回放与 `LiveEndTurnRiskEvaluator` 共用入口。重写了却没登记的
第三方类型按回合阶段表的口径停止搜索。监听者掩码用最后一位 `ExtraTurnCallbacks`，两个方法共用。
帕尔之眼的后置回调在所属玩家获得额外回合时标记已使用，与原生方法一致；分支计数仍由 `SimulatedCombatState` 持有。

### 第三方规范 Power 预热登记

0.49.2 起规范 Power 预热只访问原版来源，第三方 Power 在搜索里第一次被施加时会撞上
`PowerDynamicVarMaterializationGuardPatch`。`PowerDynamicVarWarmup.RegisterAdaptedCanonicalPower` 让适配层
显式担保某个第三方 Power 的规范实例可以在主线程物化，建根时随原版一起物化，每局一次；物化失败照常抛出。
默认范围不变，没有登记时行为与此前一致。

### 原版怪物出招表补丁的适配声明

`PredictionModPatchAudit.RegisterAdaptedMonsterMachine(Type, string modId)` 让适配层逐个声明某 mod 对某原版怪物
`GenerateMoveStateMachine` 的补丁已适配。`RejectForeignPatches` 只对出招表方法、且仅对登记的
（出招表的声明类型，mod id）组合放行，声明类型可以是被多个怪物继承的抽象基类；其他 mod、其他审计方法及第三方怪物的整体门禁不变。
没有登记时行为与此前一致。动机：平衡尖塔改写了 45 个原版怪物的出招表，0.49.2 起几乎每场都停在
「求解器暂未适配此内容性 Mod」，而其适配层已逐条核对招式效果与条件分支。

### 单人共享损血剪枝（2026-10-05）

完整合规胜利在串行提交处立即发布，各组合成员共享按失窃量、药水量、成长来源次数及遗物目标分桶的见证。同根同政策的会话仅携带完整零药路线，根戳、损血账本或政策变化时失效。未知回血、开放药水档及未知成长上界保留展开。

可执行零药胜利使用独立于纯 HP 上界的准入条件；成长和遗物路线同样参与下一次同根请求的完整路线选优。纯 HP 界继续使用原有资格限制。原生 `PRIMARY-INCUMBENT-REUSE` 连续三次搜索通过完整路线质量、成长次数、两回合增量回放、政策失效和 live 隔离检查。

未终局无风险节点可以等值截断，可能放弃同收益同损血但更早获胜的路线；不承诺原完整排序或所有有限 Beam 根均不退化。实现及复跑见[策略说明](../../strategy/hp-loss-pruning/README.md)，新基线结果见[测试入口](../../TEST_MATRIX.md)。
