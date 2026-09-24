# 官方 0.46.3 合并、fork 0.46.4 与知识收尾

日期：2026-09-24。用户要求合并官方新更新、执行 neat-freak 归档并发布 fork Release。发布终态统一见[版本索引](../releases/README.md)，玩家变化见[0.46.4 中英说明](../releases/0.46.4-RELEASE_NOTES.md)。[结构化证据](upstream-0463-merge-20260924-evidence.json)只记录本轮实际结果；下述官方历史不冒充本机通过项。

## 来源与合并取舍

- 从已发布 fork 0.46.3 后的文档提交 `1532fd5056953f61bbd798f87a61f8ad6ea4488b`，合入官方 `72295691457ec71eef9bf2d53c27f6b25d2c9f90 / 0.46.3`。上一次官方基线是 `4bfb4407 / 0.46.2`；本轮上游变更 31 个文件。
- fork 的 `v0.46.3` 已冻结于 `02d1523c`，因此本轮使用 0.46.4，不移动现有标签。官方同号说明完整保存到 `docs/releases/upstream/0.46.3-RELEASE_NOTES.md`；已发布 fork 的同号说明保持原文。
- 采用官方 PR #133 的默认自动启动配置、设置状态与恢复逻辑，PR #134 的单人新鲜资源待命探针上限，以及搜索中／完成后的速度显示和重复提示精简。
- 八个合并冲突分别涉及开发笔记、文档导航、测试矩阵、启动配置指南、同号发布说明、发布索引、英文词典和 UI snapshot。保留全部多人阶段／共享伤害／过期状态，官方新增词条与速度投影合入；历史开发和测试记录按来源分别标识。
- `RankBest` 在进入单人保路前返回多人专属排名。新鲜资源探针沿官方原顺序取前 64 个；未改变多人评分、前沿、预算、阶段账本、手动操作或队友行为假设。Engine、Prediction 与第三方登记点无改动。

## 本机发现与修正

官方启动路径检查要求游戏程序集位于可执行目录内，不能识别 macOS 的 `.app/Contents/MacOS` 与 `Contents/Resources` 布局。纯配置层新增安装路径解析，只对 macOS 允许同一 app 的 Resources；其他应用、同名前缀、越级目录、空路径和相对路径拒绝写入。Windows/Linux 原路径仍有效。

启动层先冻结当前 CLR 模式，再准备下一次配置；配置文件层不依赖 Godot、设置或搜索。双平台结构门禁同步这一职责。设置说明改为硬件／战斗／Mod 条件下的内存压力改善，不保留固定“降低约 70%”和可见流畅度承诺，中英文一致。

macOS 无头入口增加三次独立进程的准备／启用／恢复模式，正式安装配置不变。Windows 上游三启动入口保留，补齐 Linux Bash 对应入口；Linux 停止实例路径补上已证明归属后的最终目录清理，并扩充有主／无进程／损坏标记的停止合同。这些 Linux/PowerShell 原生检查未在本机运行。

## 本轮验证

| 层面 | 本轮直接证据 | 范围 |
| --- | --- | --- |
| 开发构建 | fork、独立官方源码及各自离线宿主均 Release 成功，0 警告／0 错误 | 对照 DLL 分目录构建；不是安装包验证 |
| 配置合同 | `RuntimeGcProfileChecks` 60 项 Passed，含三个真实 CLR 子进程 | 原字段恢复、幂等、外部变更拒绝、六组安装路径合同 |
| 原生 UI | `UI-LOCALIZATION` 与 `UPSTREAM-0450-UI-STATE` Passed | 476 模板，eng/zhs/zht，搜索速度、设置与会话生命周期；不代表可见布局 |
| 原生多人 | `MULTIPLAYER-MANUAL-LOOP` Passed | 4 次手动查询、本机／队友各 3 动作、到第二回合、6 次冻结根检查；首动作完整全队状态相等，0 差异 |
| 后续单人 | `MULTIPLAYER-EXPERIMENT-INACTIVE` Passed | 多人测试后无遗留 Hook，单人根和首结果正确 |
| 原生三启动 | 三次最短单人请求全部 Passed | CLR 9.0.7：Default/false → Active/true → Default/false；有效 NoGC true → false → true，保存值始终 true；下一次配置恢复全部原字段 |
| 单人官方对照 | 两根、231 个非时间／非内存字段 IDENTICAL，无缺根 | 包含完整根、路线、续行及工作量；Evaluate、DOP1 固定预算 |
| 增量回放 | 旗舰根 1000 节点／10 秒上限、`--verify-incremental` Passed | 1000 展开、5561 转移、NodeLimit；仅正确性，不引用耗时作性能结论 |
| 职责门禁 | Bash `REFACTOR_BOUNDARIES_OK search_files=218` | PowerShell 规则同步但未执行 |

单人同根三臂为旧 fork 0.46.3、合并 fork、独立官方 0.46.3，各执行一次；VeryHigh / Beam 135 / 60000 节点 / DOP1 / 60000 ms / Smart / Evaluate。六次均未触发时间边界。旧 DLL 从此前已知发布构建保存，不从工坊替换本轮产物。

| 固定根 | 旧 fork → 新 fork 战损 | 旧 → 新探针 | 新 fork 与官方 |
| --- | --- | --- | --- |
| `EQ-IRONCLAD-ELITE-00` | 52 → 52 HP | 6269 → 3687 | 根、路线、续行、计数一致 |
| `FULL-REGENT-BOSS-00` | 55 → 55 HP | 10493 → 7272 | 根、路线、续行、计数一致 |

本轮没有作普遍提速、零质量代价或联机胜率结论。官方 60 根历史对照有一根战损 0→2、两根改善、15 根路线／续行变化；上游 DOP1/DOP2 StandPat 计数合同在 PR 与旧基线上均失败，仍是未通过项，见[官方专项报告](../performance/fresh-resource-standpat-probe-cap-20260924.md)。没有为此次发布重跑该失败合同或整批历史实验。

原始证据位于 `.local/upstream-update-20260924/`。macOS 两个实例 `macos.ozLsuZ` 与 `macos.cHlDQ6` 已由启动器删除，三启动检查证明正式游戏 runtimeconfig 未变。发布源 `441fad36`、一次正式构建、本地部署、最小 ZIP 与 GitHub 交付已完成，见[0.46.4 发布凭证](../releases/0.46.4-PUBLISH.md)。

## neat-freak 真相矩阵

| 事实面 | 状态 | 本次处置 |
| --- | --- | --- |
| 当前源码与运行机制 | verified-current | 合并取舍、60 项配置合同、七个原生请求与单人官方对照；可见游戏和真实网络 unverified |
| 文档 | changed | 统一当前基线、版本／策略导航；保留官方历史，纠正“原生多人实验只有草案”的陈旧说明与无头文档代码围栏 |
| 规则与 skills | verified-current | AGENTS 当前指针、搜索／UI skills、职责地图和双平台结构门禁同步；不新增历史流水账到主规则 |
| 第三方适配与语义目录 | not-applicable | 没有登记点、公共战斗语义或支持面变化，不制造虚假的覆盖验收 |
| 记忆与全局配置 | generated-read-only | 只读规则／记忆入口，不写平台生成记忆，不修改其他项目或全局规则 |
| 工作区与历史现场 | pending cleanup approval | 开始时完整枚举项目 Markdown 和工作树；对照源码、旧 DLL、原始证据及历史包保留供复核 |
| 发布与本地部署 | verified-current | 源码 `441fad36` 构建并部署、最小 ZIP 创建、分支与标签原子推送、GitHub Release 创建均成功；不追加页面、下载或 ZIP 复核 |

主规则开始时 28,951 字节，更新后 28,881 字节，占 Codex 默认 32 KiB 预算约 88.1%，已超过 70% 提示线但未超预算；本次缩短版本历史指针，未扩大规则。仓库无其他更具体 AGENTS；全局 Codex 规则为空，其他平台文件只读。项目 Markdown 在开始时机械枚举 433 份，受影响文件另按变更清单定向审阅。

## 保留项与限制

- pending：真实联机、可见 Steam 交互／动画／性能、Windows 与 Linux 原生执行、DOP>1 质量、完整回归／干净安装、在线端点可用性。新增 Linux 停止／清理合同保留为可重跑输入，本机不把语法检查写成运行通过。
- out-of-scope：官方 main、创意工坊／夸克渠道、在线后台版本提示、其他项目、全局配置和平台生成记忆。
- warning：主规则约 88.1% 预算；官方 DOP 对照与 BeamRankSortChecks 的既有失败仍按官方原始记录保留，不回写成修复。
- 清场候选：本轮 `.local/upstream-update-20260924/official-source/`、`official-source.tar`、`fork-0463.dll`、比较与原始测试证据，以及前轮归档已列出的历史现场。未删除这些材料，也未删除分支或 worktree；发布包和受限私有配置不是自动删除对象。

复核现场仍保留，等待用户确认后清场。依据 `.agents/skills/neat-freak/SKILL.md` 第 6 节“停下来等待用户在汇报后明确确认可以清场”；该要求只约束证据清场，不阻止已授权的合并、发布与本地部署。仓库规定的一次成功阶段证据优先，因此不为 neat-freak 追加发布后复测或远端页面核验。
