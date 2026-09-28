# 官方 0.47.2 合并与 fork 0.47.3

[策略索引](README.md) · [结构化证据](upstream-0472-merge-20260928-evidence.json) · [玩家说明](../releases/0.47.3-RELEASE_NOTES.md) · [发布凭证](../releases/0.47.3-PUBLISH.md)

2026-09-28，按用户「继续合并官方的最新版本，并发布release」请求，从已发布 fork 0.47.2 的归档提交 `3570b9d6` 合入官方 `72363308c83d7543ddb139ad62ec2161d013d469`。官方版本号为 0.47.2，包含该正式版之后合入的 PR #144；上一官方基线为 `7d9b4bed / 0.47.1`。fork 使用新版本 0.47.3，官方同号日志移入 `releases/upstream/`，既有 fork 0.47.2 日志和所有旧标签不改。

## 合并边界

- 单人采用官方搜索阶段、请求账本、路线质量比较和固定前缀调度，以及已启用的能力／生成药水计划。没有继续开发上游未完成的 P7/P8 实验。
- 官方将串行和并行候选生成集中到 `ExpansionPlan`；原两处多人首动作目标门禁迁到该入口，仍只在单人读取原生 live 目标条件，多人按冻结根和分支状态判定。`AdmittedParent`、作业调度和快照归属沿用官方结构。
- 多人协调器在单人请求账本、组合及计划发现前返回。新增 `_planCommitment` 在多人构造时清空，计划专属保路和延长无进展窗口不进入多人。原能力信用、成长目标、追加探索和开发脚本隔离保留；三周期贡献、普通双倍／固定原值预算、十四敌方周期及手动操作不改。
- 注能核心、开局弃牌、回合结算与续用属于共享语义，和单人策略改进分开验证。第三方回合阶段的按类型登记、原子忽略登记和模型 ID 缓存采用官方实现；新增登记仍设置外部扩展标记，不能绕过多人烘焙手套暂停入口的拒绝边界。
- PR #144 的恢复退避和永久退出分类采用官方代码；内存条沿用官方物理容量与不可用值显示，没有调整生产内存预算或虚构 macOS 物理采样。

## 最小验证

全部请求上限为 120 秒，无可见 Steam。原始结果和启动器输出位于 `.local/upstream-update-20260928/`；这是本轮执行证据，上游文档中的 Windows 历史结果不算本轮通过。

| 检查 | 本轮结果与范围 |
| --- | --- |
| 固定前缀跨回合 | Passed：三次原生回合推进与完整续用戳、七张回合统计表、缺失缓存拒绝；N4/N8/N17 路线与独立逐前缀 oracle 一致。 |
| 开局弃牌 | Passed：新抽牌、暂时免费牌、重复牌数值、严格选择上下文、DOP1/DOP2、原生完整状态和兄弟隔离。 |
| 注能核心／工具箱 | Passed：原生准备后 `TURN_SETUP_STATE_MATCH validation=exact_state_text`、选择顺序与增量验证；另有 14 项生产 Hook 离线检查，覆盖三球、4/9 数值、历史、后续不重复及 Fork。 |
| 准备选牌后固定前缀 | Passed：5 秒固定预算、Beam24、节点6000、DOP2、增量验证、首个准备结果停止。最终输入不声明 No-GC 验收。 |
| 多人烘焙手套 | Passed：选牌暂停中手动计算，完整全队根不变，实际手动选择后完整状态一致。 |
| 多人手动控制 | Passed：DOP2 输入，停止排空、实际队友防御后建议过期、手动取得新根、计算中异选仍归玩家，零自动操作。 |
| 同进程恢复单人 | Passed：多人请求后无残留实验 Hook，单人搜索和准备继续正常。 |
| UI／内存显示 | Passed：英／简中／繁中 483 项模板、存活控件语言切换、显示身份与内存格式；macOS 系统物理采样不可用时为未知，不证明可见布局或 Windows 采样。 |
| 多人托管合同 | 31 项兼容、32 项贡献通过；新增单人承诺保留／多人承诺清空，贡献合同含原生动作、周期和增量一致。 |
| 独立官方对照 | 两个战斗根各以 DOP1、DOP2 对照，共 4 对／340 项非时序字段一致，均无时间边界；比较所选路线、根／续用及工作计数。能力根 1 HP／0 药，药水根 4 HP／1 药。能力根的两种 DOP 分别展开1165／1167，官方与 fork 各自一致，不宣称跨 DOP 等价。 |
| 第三方登记与预报 | 模型缓存7项；回合阶段六组28／3／27／2／52／5项；预报请求11项通过。使用生产代码与工具替身，不替代真实第三方组合或 worker 双进程验收。 |
| GC | 恢复分类／退避11项通过；真实区域生命周期和检查点未通过准入，原因见下一节。 |
| 静态 | Release 构建与宿主构建通过；Bash 250 文件边界通过；CoverageCatalog 3035项分类、状态字段和分支读取检查通过。输出在隔离目录，未重写历史生成目录。 |

## 失败与修正

首个固定前缀请求卡在新建档案的首次洗牌教程，120 秒后未返回。测试在实际三回合推进前标记该教程已完成，不修改战斗模型、动作或生产搜索；修正后的完整场景通过。没有提高超时。

官方注能核心 fixture 同时声明 `holdAfterInitialSearch` 和准备断言后停止。语义断言及完整状态匹配已通过，但协议拒绝复用该矛盾生命周期并退出；移除无效 hold 后，在最终批次中完成准备断言及后续 UI／内存请求。失败阶段保留，未重跑已经通过且输入不变的前两个场景。

准备选牌的原输入带 Low 预设和关闭 No-GC 字段，实际准备搜索先于普通 Executor 配置。两次请求在 No-GC 断言失败，实际仍为默认16 GB配置，区域未建立。最终独立 fixture 移除两项 GC 验收触发字段，缩短为5秒并启用增量验证，只证明选牌和搜索语义；未放宽生产断言或声称配置问题解决。

`recovery-lifecycle` 和 `checkpoint` 的1,000,000,000字节区域均在第一个实际区域断言失败。单独链接相同生产源码的诊断明确返回 `region_size_unsupported`；降低到最小512 MiB的工具输入仍不支持。真实区域恢复、主动退出和取消收尾在本机未验证，不拿上游 Windows 成功记录代替，也不修改生产区域下限。

CoverageCatalog 首次在生成未覆盖项目时重复构造已登记的注能核心，引发 `DuplicateModelException`。改为按原版 `ModelDb` 读取已有规范实例。第二次能够完成目录，但上游注能核心分类仍指向 Pending 历史证据；保留历史记录，另建本轮原生／离线成功证据并更新该 Hook 引用，最终指定检查通过。

五个自有实例均由带清理参数的启动器报告完整删除：`macos.iOAoaJ`、`macos.hF723o`、`macos.phXi84`、`macos.7Efp9N`、`macos.lih5dx`。失败日志和诊断保留在忽略的 `.local/` 中。

## 重跑入口

```bash
tools/run-unattended-test-macos.sh \
  coverage/unattended/fixed-prefix-turn-outcomes.json \
  coverage/unattended/opening-discard-choice-value.json \
  coverage/unattended/upstream-0472-turn-setup-stampede.json \
  coverage/unattended/multiplayer-toasty-choice.json \
  coverage/unattended/upstream-0472-multiplayer-toasty-controls-dop2.json \
  coverage/unattended/multiplayer-experiment-inactive.json \
  coverage/unattended/initial-toolbox-infused-core.json \
  coverage/unattended/ui-localization.json \
  coverage/unattended/memory-display-contract.json \
  --timeout-seconds 120 --cleanup-instance-on-exit
```

普通发布只在已验证行为之上同步版本、提交、正式 Release 构建、精确本地四文件部署、最小 ZIP、当前分支／annotated tag 和 GitHub Release。没有运行全量门禁、可见性能、真实联机、Windows/Linux 运行、完整第三方 Mod 栈或40分钟探索质量验收。发布完成记录另见[渠道凭证](../releases/0.47.3-PUBLISH.md)。
