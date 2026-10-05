# 性能研究分支阶段验收历史（归档于2026-10-05）

本卷原样保留此前合同、失败、性能与未验证范围，不代表最新PR #211整合源码已经复测。当前证据见[测试入口](../../TEST_MATRIX.md)及[PR #207追加验收](../performance/upstream-0500/pr207-upstream-0494-integration-20261005.md)。

## 性能研究分支验收记录（2026-10-04）

组件回复界及 Smart 原生合同通过，固定29根回归仍保留女王质量阻断；用户暂停丢路调查，未称全回归通过。完整实测、所有失败及未验证项见[组合上界](../performance/upstream-0500/component-healing-bound-20261003.md)、[原生组件审计](../performance/upstream-0500/native-healing-component-audit-20261003.md)和[组合研究](../performance/upstream-0500/heavy-scene-compositions-20261004.md)。合并前各合同、runId、平台清理与初筛范围完整保留于[固定提交的测试记录](https://github.com/ltlly/CombatSolver/blob/1bea4f8a/docs/TEST_MATRIX.md)。 同一记录补59张手牌阶段合同、Slither根认证、晚回合药水成本及未知消耗堆Feed的原生边界；组合性能仍有质量失败。

回复证明扩展研究：有限再生旧门禁预期失败3f3afd5a…，候选2e84b06a…Passed（精确7／后续2次剪枝）；5bf5eec3…保留可回复分支及未知消耗堆Feed。沙漏10c7a7e1…Passed（六次出牌、三种敌人行动、九份完整状态、16 Fork、四堆未知拒绝）；PR215储君组合8faef194…成本与未知边界、0875367f…实际无药先导／账本／DOP2严格隔离Passed。普通完整请求初筛仍未达到性能或资源质量门槛，未作最终ABBA、固定回归或部署；失败夹具、构建、逐次结果及范围见[补充研究](../performance/upstream-0500/pr215-recovery-proof-research-20261005.md)。 后续代价前沿旧表a9a36690…预期失败、7de37864…Passed（144组合／实际完整见证）；亡灵组合acef5a7c…Passed（16Fork／DOP2／共享消费／未知消耗堆），整请求战损退化而拒绝。

储君成本研究：Fork缓存 `2898db5d6e904adb8a406ed414c4a664` / `0240805e1a3048df8a82f0dc3c4b8936` Passed；零成长HP计价强制回合 `472a181e6cf24dd48605b6bc098c99bc` / `c8f30d1aca664f0788df5e3f67ad6a83` Passed，包含16实际工作分支、原生完整差分与前向结果单次消费。参与掩码布局 `fdfebc03d724493897c66f47704d30ed` / `36b2a663a51347bdb3b05656ea09d794` Passed，初筛16.12秒、工作/质量不变、分配−5.21%；同容量槽混合 `9e3882a4ced048b79489cc6c1dd56ea1` Passed，16.05秒、工作/质量相同、对原始1.251倍。候选整请求仍未达两倍：Fork缓存ACCA无收益/内存超门槛，强制回合单次16.00秒，未作新的固定回归或部署。完整采样、失败与逐次记录见[成本研究](../performance/upstream-0500/regent-search-cost-research-20261005.md)。

[全药水审计](../performance/upstream-0500/native-potion-recovery-certificates-20261004.md)记录64种有效原版药水的分类及58种有条件零回复准入；`14eef45cfeb04fc7bb137f9cec2a8c08` 最小原生合同Passed，全部64种实际用药及最终闭包未完成。[魂枢证据](../performance/upstream-0500/soul-nexus-0491-research-20261004.md)记录严格Continuation恢复、拟提交版本完整请求ABBA：上游311.39／315.50秒，候选96.02／96.66秒，中位数3.254倍，最差峰值降低50.75%，战损19→6、药水0→1、回合8→5，遗物计数目标满足数2→1；完整原生二进制仍未验证。四次搜索合同Passed，最后上游启动器退出1的身份确认异常及进程/实例清理单列。完整原生部署 `85a6c2eaf9ff495fafba556392362d86` Passed，第5回合63/70生命、StrengthPotion、非预期重算0。其他27个固定根同版本串行回归全部通过：完整根/预算/政策相等，24胜/原有3 NoWin保持，战损、保命、追回及同战损次级目标无退化，峰值最大+7.503%。女王两根按用户暂停要求排除，不能称原29根全量通过。

魂枢机制新增合同：`373890e75fa0483db84b5ffd9a8121d0` Passed，实际用药、两回合完整状态、16 Fork/RNG/父分支/live隔离、禁药与额度仍保留已有再生、未知源拒绝、生成过滤及DOP2严格增量/完整重播。`b9a5e01156fb476d8dfcfb9ece2c0e6b` Passed，8遗物/68抽牌查询及冻结隔离，启动器退出1异常单列；查询不代替完整生命周期。拟提交版本 `965c558fc8d341a9afffb821c6d61468` Smart合同Passed，完整无药胜利、精确7次/后续2次剪枝及原政策门禁。

新增监听者分配原型的首次合同 `2283f3757afc4676b9102fd732393e4d` 因既有GoldCallbacks反射oracle错误失败；更正后 `f99dd720ef0b4b49831d75dcc41d071e` Passed，1685模型/63监听位/完整顺序/Fork/失效/补丁合同通过。整请求317.18秒、0.984倍，未提速，未纳入拟提交版本。其余失败构建、内部短搜取消及未提速候选均在魂枢报告保留。

上游合并后的合同原文归位：组件回复界和Smart资格移入 `Contracts/Search`；双平台门禁由两条根目录违规修正为Passed、search_files=248，Release构建0警告错误。原生 `01d1255ecab441e0825ac28d14135597` Passed：完整无药胜利见证，精确层7次/开局后续2次实际剪枝、药水/成长/遗物/追回门禁、DOP2严格增量及完整协调器/live隔离；实例清理。代表合同不代替合并后固定29根回归。

PR #215原生缺陷对照：`0a69bcdd3c2644a6b9e73638fc153829`预期失败，实际成本14/9的同HP/同回合/同药水数完整胜利。独立修正`805b083ba9b14df89d0fb85f71a16436`及扩展合同`0bfdd959a37f49cda8f24f3adcf1a39c` Passed：内部/共享保留较便宜分支、同成本剪枝、缺失成本保留、零药胜利界、实际药水完整Continuation、父分支/live/RNG隔离。20项胜利界及143项续搜纯合同通过，普通120秒上限及实例清理。 长期入口`POTION-COST-INCUMBENT`原生`8cdb3b3ff63540c3ae04b52760d33065` Passed；结构/工具/覆盖/文档检查通过。未纳入本分支，不作新整请求性能或RSS结论，原始失败与未验证项见[组合研究](../performance/upstream-0500/heavy-scene-compositions-20261004.md)。

TheHunt／零额度成长研究：`88f095e8686344f797bef57f7e9d9048` Passed（致死／非致死、未生成战后奖励、金币、第三回合、16 Fork、完整状态／RNG隔离）；`5712177f46554f3bb26229f0de3413d2` Passed（实际成长1一药完整胜利、14／9成本门禁、严格HP、未知来源及DOP2同预算质量）。初始完整请求50.93秒未达标；闭合层失败由独立CLR dump证实原型Godot日志错误，修正注入诊断后51.88秒、战损47资源相同，复用未变的原生成功证据。空见证表扫描 `0ce1537149d1477b8e78b1777225211f` Passed，但完整请求48.67秒战损升至62，拒绝纳入。没有最终交错／固定回归或部署。失败、范围与依赖见[研究记录](../performance/upstream-0500/hunt-zero-growth-proof-research-20261005.md)。

容器拥有者标记研究：c7b320d6…Passed，覆盖列表／字典／集合三代隔离、有序旧枚举、比较器、空容器、16并行子分支，以及实际完整P0先导／请求账本／DOP2严格重播与live/RNG。dev08串行A→C仅16.06→15.83秒、分配−0.149%，未达原始两倍目标；未做最终交错、固定回归或部署，见[搜索成本研究](../performance/upstream-0500/regent-search-cost-research-20261005.md)。
