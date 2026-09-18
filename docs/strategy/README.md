# 策略与搜索研究

[返回文档导航](../README.md)

当前搜索职责见 [架构地图](../ARCHITECTURE.md)，实际测试与未验证范围见 [测试矩阵](../TEST_MATRIX.md)。

- [多人当前行为与实施取舍](../multiplayer-advisor.md)：多人功能的现役入口。后续优化从兼容合并 `7160d2f` 出发，单人基线为官方 `0e6cc2d / 0.41.0`；研究中的旧版本、类型草案与测试数字不覆盖当前实现。
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

## 多人研究归档

以下原始材料保留原文与固定路径，由上方多人指南统一说明采用情况，不再各自维护现役状态。

| 材料 | 历史输入与用途 |
|---|---|
| [最初的本地玩家方案](../CombatSolver_Multiplayer_Local_Player_Design.md) | 官方 0.40.2 静态研究；双模式、自动刷新等建议不等于后来批准的手动单目标实现 |
| [ChatGPT 6 Pro 研究提示词](multiplayer-pro-research-prompt-20260917.md) | 当时交给外部研究的任务和 GitHub 入口；操作授权以当前用户请求为准 |
| [固定源码上下文](multiplayer-research-context-f220a6b.md) | `f220a6b / 0.40.5` 快照与历史证据，不随源码滚动更新 |
| [策略研究回文](../CombatSolver_Multiplayer_Strategy_Research_and_Design.md) | 算法比较、候选方案和抽象验证；首批只采纳现有 Beam 上的三步改进 |

2026-09-17 已盘点 306 份项目 Markdown、规则链和唯一工作树。项目规则以根 `AGENTS.md` 为真身，无子目录 override；已结束批次从规则顶部归回既有开发/版本记录。代码状态为已合并、已本地验证，部署与真实联机为 `pending`，本轮知识整理不重新运行既有成功测试。文档/规则为 `changed-and-verified`；生成记忆为 `out-of-scope`、只读；构建、发布、外部项目为 `out-of-scope`。本地历史测试与官方对照副本作为复核证据保留，没有执行清场。官方能力估值目录原有 40 处末尾空行警告仍按原样保留，见测试矩阵。
