# 多人箭矢归属修复与0.51.7收尾

日期：2026-11-14。源码基线：多人fork 0.51.6，官方基线 `3677d4ca / 0.51.5`。当前状态：修复与定向验证完成，0.51.7待发布；发布状态由[版本索引](../../releases/README.md)维护。

## 问题与根因

问题包 `db194303` 报告 CombatSolver0.51.6、游戏0.113.0，环境记录加载0.51.6.0和RitsuLib0.6.13。Windows双愚者在墨影幻灵战第6回合反复手动计算，19.082秒内16次失败都来自同一动作：肌肉药水、深度拉伸、防御之后向敌人2打出快速射击+。目标已有队友0施加的 `IMPALED_POWER`，本机为玩家1；异常为 `Arrow power application did not create its target state.`。包内没有DLL或source commit，不能据此证明精确构建身份。

原生0.113的 `ImpaledPower` 为 `InstancedPerApplier`。模拟器此前仅识别 `Instanced`，其余按 `(owner,type)` 取得状态；本机施加新Power时误叠到队友实例，随后按本机施加者查找失败。首次失败基线使用一个普通敌人即可重现，不依赖该首领机制、第三方Mod或整场搜索。

## 实现与边界

`SimulatedCombatState.MultiplayerPowers` 仅在多人军师根启用，按owner/type/applier匹配已有Power；同来源叠加、异来源创建、移除后重新获得新实例。公共入口只增加显式多人分支；单人政策、预算、评分和原路径保持。根仅有一个按施加者实例时也克隆到既有逐实例表，后续变化与原有Fork共享重映射合同；没有新建平行状态表或新增live读取。

箭槽仍由 `SimPlayerCombatState` 拥有，`ImpaledPower` 保存该玩家分支箭槽的有序引用。状态键与完整续用文本沿用逐玩家的箭状态和命中计数。没有移除异常、跳过候选或把队友的施加者改成本机。第三方登记点和公开API未变化。

## 本轮证据

环境：游戏0.113.0、RitsuLib0.6.13、macOS arm64。请求上限120秒，Instant；没有启动可见Steam。

| 阶段 | runId | 结果 |
| --- | --- | --- |
| 未修复生产源码的双愚者基线 | `macos-641960492a1642dabec65922823d8a9b` | 相同ImpaleArrow异常，目标动作之前的根完整状态一致 |
| 修复后多人差分 | `macos-311a04644fb844ae8e0ee4d8be833062` | 44项断言通过；完整全队原生状态、不同来源、同来源叠加、自动/手动拔箭、移除/重建、根/兄弟/后代隔离 |
| 单人箭矢哨兵 | `macos-81b32896f6dd435e8e9e31d869752c13` | 箭身份、命中取回、多标签、Yah/Lullaby、完整续用与Fork隔离通过 |

多人输入在[固定夹具](../../../coverage/fixtures/scenarios/state/multiplayer-arrow-ownership.json)，协议与执行沿现有ScenarioBuilder/Executor。单人请求为 `FOOL-BRANCH-STATE`、FOOL、`FUZZY_WURM_CRAWLER_WEAK`、种子 `MPARROWSOLO20261114`。结构化索引在[测试证据](../../../coverage/evidence/test-evidence.json)。基线实例 `macos.6rKDsK` 与最终实例 `macos.J9gO5R` 均由启动器成功删除。

测试代码初次编译缺少 `ResourceInfo` 命名空间，补齐后零警告/错误；这是夹具编译修正，不是行为失败基线。修复构建17.79秒、零警告/错误。此后只修改文档、版本与发布元数据，不重复行为测试。

定版前L0检查：文档673份/3487链接、coverage布局654数据/602fixture/38组、Bash职责门禁271 Search文件与diff空白检查通过。结构脚本缺可执行位，改用 `bash tools/inspection/verify-refactor-boundaries.sh` 成功，未修改文件权限。

原问题包恢复/录制路线回放、真实联机、可见UI/性能、完整第三方栈、Windows/Linux原生运行、干净安装和完整发布门禁未执行。本轮不承诺整场路线质量或真人联机胜率。上一批0.51.6文档将RitsuLib误记为0.6.12，已依据该批原始启动日志更正为0.6.13，不冒充新测试。

## 知识与工作区

| 事实面 | 状态 | 依据 |
| --- | --- | --- |
| 代码 | changed-and-verified | 最小失败基线、修复后的原生差分与单人哨兵 |
| 运行态 | pending | 待定版构建、本机精确部署与GitHub发布；真实联机仍pending |
| 文档 | changed-and-verified | 架构、多人指南、测试索引、更新日志和此归档已同步，链接与coverage布局检查通过 |
| 规则 | verified-current | 根AGENTS为项目入口，无子目录规则；全局Codex规则为空，无override；只改过时“本轮”措辞 |
| 记忆 | out-of-scope | 宿主生成记忆只读，未写入 |
| 工作区 | verified-current | 初始工作区干净、一个worktree；本次只提交源码/夹具/文档，保留原始问题包与本地证据 |

neat-freak机械盘点671份Markdown，再按本次语义和发布范围阅读现役入口。未删除工作树、分支或证据；原包与约6.9MiB解包数据继续保留。可供后续清场复核的是 `.local/issue-bundles/db194303/baseline/` 与 `final/` 中约224KiB日志，以及临时检查脚本；正式包、发布回执、私有配置和其他批次不在候选中。复核现场仍保留，等待用户确认后清场。

未消除提醒：AGENTS约25KiB，占默认32KiB指令预算约78%；本轮未新增规则段落。当前请求没有workspace级审计、其他项目修改或破坏性清理授权。
