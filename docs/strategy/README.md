# 策略与搜索研究

[返回文档导航](../README.md)

当前搜索职责见 [架构地图](../ARCHITECTURE.md)，实际测试与未验证范围见 [测试矩阵](../TEST_MATRIX.md)。

- [多人当前行为与实施取舍](../multiplayer-advisor.md)：多人功能的现役入口。首批三步优化基于兼容合并 `7160d2f` 完成，单人基线为官方 `0e6cc2d / 0.41.0`；研究中的旧版本、类型草案与测试数字不覆盖当前实现。
- [策略优化日志](STRATEGY_OPTIMIZATION_LOG.md)：样例、策略认识与数值记录。
- [当前搜索逻辑详解](search-logic-explained-20260912.md)：2026-09-12 开发快照，解释评分、保路、剪枝、预算与最终排序，并区分未提交实验。
- [有界新颖性与 Beam 组合](bounded-novelty-search-20260916.md)：默认关闭的实验开关、紧凑增量新颖性、共享预算、选型反例和本轮验证。
- [能力牌逐卡估值与搜索优化计划](power-card-valuation-plan-20260917.md)：用户逐卡定义、统一奖励/惩罚接口、六卡池目录、后续独立搜索成员和清理规定。
- [Beam 宽度组合](beam-width-portfolio.md)：共享节点预算的多宽度选优、精炼门控四条、开关与请求字段、成员默认值与数据来源。
- [玩家世界线研究](player-worldlines-20260905.md)：2026-09-05 批次。
- [有界搜索恢复研究](SEARCH_RECOVERY_RESEARCH.md)：已否决并撤回的 v54/v55 原型，保留研究证据。
- [0.17.0 原始需求](0.17.0-raw-requirements.md)。
- [0.17.0 优化规格](0.17.0-optimization-plan.md)。

原始需求和历史候选设计保留其当时语境，采用情况应结合开发笔记与源码判断。

## 0.41.1 外部复审

本轮以发布提交 `5ad98a9` 固定输入，向用户指定的 ChatGPT 6 Pro 提供完整源码和[复审请求](multiplayer-pro-review-request-20260918.md)。发布凭证、回文接收状态与本地核对统一维护在[本轮归档](pro-review-20260918/README.md)，后续建议不自动成为已批准实施的功能。

## 多人研究归档

以下原始材料保留原文与固定路径，由上方多人指南统一说明采用情况，不再各自维护现役状态。

| 材料 | 历史输入与用途 |
|---|---|
| [最初的本地玩家方案](../CombatSolver_Multiplayer_Local_Player_Design.md) | 官方 0.40.2 静态研究；双模式、自动刷新等建议不等于后来批准的手动单目标实现 |
| [ChatGPT 6 Pro 研究提示词](multiplayer-pro-research-prompt-20260917.md) | 当时交给外部研究的任务和 GitHub 入口；操作授权以当前用户请求为准 |
| [固定源码上下文](multiplayer-research-context-f220a6b.md) | `f220a6b / 0.40.5` 快照与历史证据，不随源码滚动更新 |
| [策略研究回文](../CombatSolver_Multiplayer_Strategy_Research_and_Design.md) | 算法比较、候选方案和抽象验证；首批只采纳现有 Beam 上的三步改进 |

## 本次收尾（2026-09-18）

本节保留 `1ba21fe` 对应的源码归档记录。随后用户另行要求发布当前 fork，定版范围见 [0.41.1 开发记录](../DEVELOPMENT_NOTES.md#0411fork多人策略与官方-0410-兼容2026-09-18)；本节的“不创建 Release”仅描述此前源码同步批次。

先行归档为 `41d4d4e`，三步实现为 `6e28485`，实际行为与未验证项见[测试矩阵](../TEST_MATRIX.md#多人策略首批实施2026-09-17)。本轮重新盘点 306 份 Markdown、规则来源和唯一工作树；仅更新交接与同步状态，上一轮行为源码、依赖和测试输入未变化，不重跑已通过测试。

用户本次授权同步至 [fork 任务分支](https://github.com/sgpsdd-zyx/CombatSolver/tree/codex/multiplayer-advisor)。已有 `v0.41.0` 是官方 `0e6cc2d` 的标签，保持原指向；最新多人源码与本文应从任务分支读取。推送以本任务的一次成功命令为完成凭证，不创建新的 fork 版本或 Release。

| 事实面 | 状态与依据 |
|---|---|
| 代码 | `verified-current`：`6e28485` 的多人实现和既有验证保持原样，单人隔离合同继续有效 |
| 文档与规则 | `changed-and-verified`：本文、多人指南与开发笔记同步当前范围；根 `AGENTS.md` 为 Codex 规则真身，无子目录 override |
| 运行态 | `pending`：真实主机/客户端联机与可见布局尚未验收，源码同步不代表安装部署 |
| 记忆 | `out-of-scope`：生成记忆只读，没有写入 |
| 工作区 | `verified-current`：唯一工作树，归档前无未提交改动；原始研究、失败基线、官方对照副本及本地证据保留 |
| 发布与清场 | `out-of-scope`：安装包定版、渠道上传、分支/worktree/证据删除均不在本次源码同步范围 |

历史警告保留：官方能力估值目录的 40 处末尾空行、原始本地玩家设计第 3 行的 Markdown 双空格硬换行，以及上一轮覆盖工具的 2 条依赖引用警告。本地复核现场仍保留，未执行清场。

根指令现为 26,174 字节，占默认 32 KiB 预算的 79.9%；其他平台的规则文件不计入本次 Codex 指令链。
