# 六个社区 PR 与回血剪枝审计（2026-10-03）

基线 main `1a3d1a37`；完整合并分支，没有 cherry-pick。#198 转正式后经用户明确授权纳入。只做构建、结构门禁、最小语义和短搜验证，不提升版本、不发包、不启动可见 Steam。

## 合并范围

| PR | 作者 | 最终审计 head | 完整合并 |
| --- | --- | --- | --- |
| [#194](https://github.com/Torch1230/CombatSolver/pull/194) | s1f102500012 | `d999365e458bf191556079c3f694fb12434dd3a5` | `3635ef5b` |
| [#197](https://github.com/Torch1230/CombatSolver/pull/197) | fengzhenhong（大宏） | `42ccdf8f678a71c1ce36123836af7f392eb6e1f2` | 维护者修正后 `53a79a64` |
| [#190](https://github.com/Torch1230/CombatSolver/pull/190) | ACGNnsj | `2d3c957dc22310cbca2e2697b8d8f402fc814283` | `0d0fa40a` |
| [#199](https://github.com/Torch1230/CombatSolver/pull/199) | ltlly | `06cb60cac63467efb4ee653b34e6e92bfa9ce8b7` | `52b7a8b8` |
| [#200](https://github.com/Torch1230/CombatSolver/pull/200) | ltlly | `4b48b8a977059a2694f62a1166ac7644acb257ed` | 原合并 `2e4113ff`，最新增量完整合并 `254cd96e` |
| [#198](https://github.com/Torch1230/CombatSolver/pull/198) | yM7-1 | `fae9b6ba07eb6fd143b58d9025b011bfcce4bf7f` | 维护者修正后 `7129836a`，Refs #173 |

## 审计修正与冲突

- #194：B015 失效目标前移保留原路线，统计队列故障停止统计并隔离部署；原存储合同和最小目标验证通过。
- #197：集合版本字段缺失原来返回 `-1`，可能制造虚假相等；改为必须取得字段及实际集合，缺失直接抛错。删除没有资源职责的空 Dispose，把版本核对放到根构造末尾，覆盖完整窗口。球数值镜像及临时 Strength/Dexterity/Focus 回收核对 0.111.0 原版监听器顺序。历史现场并发写者、原始球枚举异常未复现；T024 是诊断与身份合同，不声称定位全部首因。
- #190/#199：Retention 冲突保留认证战后回血的边际计数，同时贯穿 `allowTurnTieBound`，保留 relic 目标同血量跨回合路线。Executor 和文档同时保留双方内容。
- #200：原生生成闭包只是职责抽出与复用，牌/Power 集合未扩大；铁甲未知消耗区治疗牌仍拒绝证书。Linux 错误提示覆盖 JSON 语法与数组形状。作者最终铁甲追加七次离线请求及原生目标，没有重跑 29 根；本轮不补做重度矩阵，也不把作者数据算成本轮提速。
- #198：删除动态变量读取委托编译失败后的 `catch (Exception) → null`；仅已定义字段缺失/类型变化使用公开枚举，委托编译异常直接失败。写入桥仅由测试消费，缺字段明确失败。补齐 Windows 禁止 `._vars` 直访的等价门禁，修正测试可空警告。单人跳过生命缩放符合原版 `playerCount == 1` 空操作；场景 Marker 顺序在主线程冻结，未知召唤槽位保持稳定插入顺序。

## issue #135 后续与多成员

原讨论是 [issue #135](https://github.com/Torch1230/CombatSolver/issues/135)，多成员建议见 [后续评论](https://github.com/Torch1230/CombatSolver/issues/135#issuecomment-5826716394)。新策略只给实际已知回血来源留余量，随机炼药尚未生成时不给回血额度；产生实际回血药水后从分支槽计入。原版鲜血药水、龙涎香及恶魔之舌也能回血，不能只保留再生药水。

最终原版来源复核还确认 `BookRepairKnife.AfterDiedToDoom` 在非拥有者死于灾厄时治疗玩家，该遗物也保留完整余量。原版 `RegenPotion.CanBeGeneratedInCombat` 为 false，随机炼药仍可能生成鲜血药水；忽略这种未来随机结果是明确选择的策略，实际持有的回血药水仍计入。

已有再生与持有的再生药水按合并剂量三角数计入；用药额度到达后保留已生效再生。鲜血药水、龙涎香按分支最大生命计算，固定战后回血保留。时候未到、狂宴、可重复遗物回血、果汁与保命资源给完整余量；第三方内容、未决选择保持保守边界。

资格冻结在 `CombatRootSnapshot.UsesKnownNativeHealingPolicy`，与原封闭证书分开，Retention 取二者较紧值。该估计是用户明确选择的搜索策略，不宣称随机生成下的严格可达上界。所有 Beam 消费共用 Retention；宽度组合及能力续搜取得已有无药完整胜利界的门槛也覆盖新策略。只共享纯值界，分支/前沿/转置保持各成员独立，成长、偷窃与强制用药门保持原规则。测试显示名在主线程捕获后交给 worker。

## 本轮直接验证

| 最小验证 | 结果与 run |
| --- | --- |
| KNOWN-HEALING-POLICY | Passed，`dff081fbcbcd48eab24ba49af05170ab`：随机炼药、既有再生/持有药水、NotYet/Feed、用药额度、FrozenRoot/Fork/live/RNG |
| B015-FIXED-PREFIX-TARGETS | Passed，`c8d054486dfe4dbcacdd7c9a92207790`；先误传 EnemyCurrentHp=999 导致一击杀预期失败，按已提交 fixture 改回 1 后通过，没有为错误输入修改源码 |
| B016-T021-ROOT-COLLECTION + 根快照断言 | Passed，`6c2d956ef33f4e1083420018eaf3137f` |
| TURN-END-FOCUS-EVOKE-ORDER | Passed，`3d50ea01eaa24319bb422ac67b8225d3` |
| ORB-VALUE-NATIVE-HOOK-ESCAPE + INFUSED_CORE | Passed，`34bf1834fc2a445ebeedbf966b2d6dc1` |
| OPENING-POWER-BOUNDARY | Passed，`988f79d5127a4ae2a2ccfd278b35eef7` |
| REGENT-POTION-CAP-BOUND | Passed，`a034a786c53e431da2b8f1e6c3fd37fc`：0/1/2/无限额度、自动消费、已有再生、两次用药完整原生状态/Fork/RNG |

RunStatisticsTests 存储合同通过；OfflineSearchHarness `--check-early-turn-continuation-bound` 143 项断言通过。批内复用同一实例，末项由启动器成功清理 `.local/headless-instances/pr-audit-20261003`。贡献者广泛回归保留原文档口径，不混作本轮结果。

最终 Release 构建 0 警告/0 错误，Windows 门禁 `REFACTOR_BOUNDARIES_OK search_files=246`；diff 空白检查通过。

| 最新增量与边界 | 结果与 run |
| --- | --- |
| KNOWN-HEALING-MEMBERS | Passed，`090c88d57b7f4be69bf078599e7b8f04`：旧证书 false、新策略 true，DOP2/800节点/三宽度成员，实际接收外部界、strict incremental、控制质量、成长门/live隔离 |
| IRON-GENERATION-HEALING | Passed，`ce8ad1cbe030416a860eac7195684954`：四个原生动作、生成池、未知消耗牌、再生/Fork/完整状态/RNG |
| DISPLAY-NAME-SUMMON | Passed，`8fed110bf2f847af90a3e92e307d5036` |
| PREDICTED-MONSTER-SCALING | Passed，`028c53879b4c4c00917292cde2d295ee`：ToughEgg HP17，多人缩放调用0 |
| DYNAMIC-VAR-BRIDGE | Passed，`2c80d103e2394ed4955a29bb46cc3039`：快路径与强制公开枚举逐项一致；实际跨版本兼容Mod环境未运行 |
| LAMP-INKY-SHIV-ROUTE-CONTINUATION | Passed，`ca12e6563178410d8a3165416b101528`：原生生成小刀身份与T+1完整续用戳 |
| KNOWN-HEALING-POLICY 返回牌附件复核 | Passed，`5a3d63a682264b72a2fa2d4c1be694ca`：在补齐返回牌附件资格及测试入口后的实际DLL上验证 |
| KNOWN-HEALING-POLICY 最终来源保护 | Passed，`95a89d6861f84a83a62e12b1bea95f01`：新增灾厄击杀治疗遗物保护及旧根隔离断言；启动器成功删除 `.local/headless-instances/pr-audit-20261003-doom-heal` |

进一步审计发现 #197 的墨染小刀边界方法没有接入 Executor；补上入口后本轮真实执行通过，不能把未接入场景的默认流程 Passed 算作该合同。此项验证的是 main 既有 Inky 来源修复，#197 本身主要添加覆盖。

多成员测试首次 Failed 因对照路线零损血；诊断 run `e3793cf5cb5e486db819b576041b8687` 显示 `certified=False/knownPolicy=True/won=True/boundary=None/loss=0`，并非接线失败。给敌人注入原版力量以形成确定有损血两回合边界后通过。期间一次测试辅助 using 导致编译失败，已改正；误启动旧DLL的 run `77a73e7cdc5c42e49c3d6bd834c50771` 重复该零损血失败，不计最终验证。没有为测试输入扩大预算或修改生产剪枝。

最终批次在 `.local/headless-instances/pr-audit-20261003-final` 内复用；末项带 `CleanupInstanceOnExit`，启动器成功清理整个实例。最小证据在忽略目录 `.local/pr-audit-20261003/`。Bash 门禁同步维护，`bash -n` 语法检查通过；本轮仅在 Windows 执行完整结构门禁。

## 社区边界

用户随后要求合并 [PR #201](https://github.com/Torch1230/CombatSolver/pull/201)，head `e11ab12e`。这是同一 T015 修复变基到 `45f87cd3` 后的四提交分支，完整合并，继续保留下述维护者严格费用比较修正。最终行为源码、工具、夹具和构建输入与已验证 `a0765da0` 相同，复用刚完成的验证与构建。0.48.0 玩家日志明确链接 #201。

后续 B014 T015：用户追加要求合并已修复的 T015。#198 的已合并 head 仍为 `fae9b6ba`，作者 fork 分支新增 `0833dd1b`、`cf10d513`、`a53332da`、`a9162efd`；本轮取完整分支合并，保留原 main 的桥接异常修正和 Windows 门禁。Smart 主路线带药且丢失确定性插药标记时，先用原 Disabled 路径重新建立无药基线再审计，保留现有候选并按原终局政策选优。没有修改强制用药或智能用药的收益门槛。

维护者拒绝新增的测试端自动费用剥离：费用子状态在当前无修改牌上也缺失，缺失本身不是旧版标识；直接编译作者 helper 的两个最小字符串反例得到 `CURRENT_UNMODIFIED_VS_MODIFIED_MATCH=True`、`LEGACY_TWO_CARDS_MATCH=False`。完整分支保留在合并历史中，最终源码删除该宽松匹配，并在 `REPLAY-BOUNDARY-CONTRACT` 加入费用与星能漂移反例。`SMART-AUDIT-POTION-BASELINE` 在真实最小场景中，通过实际主结果发布入口清除插药标记，隔离作者报告的审计交接；不重跑 227 万转移的原包。#173 已由用户与贡献者收口关闭，下面“保持开放”的结论是前一阶段状态。

最终维护者小场景 `24b1580a…` Passed：带药交接实际命中，控制/候选 HP 指标、1 瓶药和 T2 胜利相同，live 与根不变，严格费用/星能与原生失败传播合同通过。既有插药执行哨兵 `d4510f7c…` Passed：真实 T2 胜利、HP36、零计划外重算。Release 0 警告/0 错误、Windows 门禁通过，两实例已清理；详见 [测试矩阵](../TEST_MATRIX.md#b014-t015-智能药水审计追加2026-10-03)。最终行为继续记入未发布 0.48.0，中英玩家日志补充智能药水中断修复。

B015 的 T016/T018 已修，T017/T019/T020 按 [维护者明确答复](https://github.com/Torch1230/CombatSolver/issues/174#issuecomment-5955057462) 结束或归档，整批收口。B016 按 #197 整批交付合并，上述历史异常保持未复现。B014 的 T011/T012/T014 已修，T013 为第三方内容排除；T015 仍因旧版材料与续用戳缺失受阻，primary 带药来源未闭环，#173 保持开放。

本轮没有完整长战斗、29 根全量回归、可见 FPS/帧时间或同根性能采样，不报告新的提速倍数。
