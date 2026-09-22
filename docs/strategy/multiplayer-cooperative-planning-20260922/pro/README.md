# 6 Pro 原始交付与本地提取

本目录保留 2026-09-22 两轮 6 Pro 的报告、设计、脚本、资料读取范围、派生表及两份选型冻结记录。原文中的推荐是当次研究判断，最终采用取舍由[本轮结论](../README.md)统一说明；不据原文操作指令实施或发布。

完整原始交付位于 `.local/mp-coop-strategy-20260922/CombatSolver_CooperativePlanning_20260922_Evidence.zip`，一次下载后解压至同目录 `pro-evidence/`：107 项、解压后 38,979,156 字节。完整 JSON、分批 stdout、修正前运行、读取日志和原始源码摘录留在该现场，不把大型原始日志或重复数据加入源码提交。

原文逐字保留，包括补充报告第 3、4 行的 Markdown 双空格硬换行；暂存空白检查只报告这两处，不为消除告警而改写来源。

## 原文

- [源码复审与结果](CombatSolver_CooperativePlanning_20260922_Review.md)。
- [首轮算法设计](CombatSolver_CooperativePlanning_20260922_Design.md)。
- [配额专项最终补充](CombatSolver_CooperativePlanning_20260922_Quota_Addendum.md)：首轮完成后独立提交，界面记录 `Worked for 18m 50s`；删除软 3 HP，配额/前沿优先，复杂队友规划后移。没有新增算法实验，原始下载同名文件留在研究现场。
- [实际实验脚本](CombatSolver_CooperativePlanning_20260922_Experiments.py)。本地未执行该外部脚本。
- [源码读取范围](CombatSolver_CooperativePlanning_20260922_Read_Sources.md)。
- [原始论文读取范围](CombatSolver_CooperativePlanning_20260922_Papers.md)。
- [首轮实验派生表](CombatSolver_CooperativePlanning_20260922_Experiment_Tables.md)。
- [Q 集之前的选型](Main_Selection_Before_FinalTest.json)与 [R 集之前的选型](Main_Selection_Before_Lockbox.json)。

## 本地提取及口径修正

[`results-summary.json`](results-summary.json) 从原始 17.6 MB JSON 提取，保留全部 909 次搜索对照的当前动作、工作量、完成深度和外部第 1/3/7/14 周期结果，同时保留配置、60 个根、27 项检查及分组摘要。提取不是实验重跑。原始探索候选、逐步轨迹、费用分类和分批回执留在完整 ZIP。

本地对原始结果核对了 909 个唯一运行键，各行费用分类之和等于 `used` 且未超过声明上限。该上限是玩具逻辑工作，不是完整物理运行计费，也不是 C# 节点或墙钟保证。

R 的八个根中，`D_actor_tail` 与 `F_cheap` 在第 3/7/14 周期的双方 HP、**敌人总 HP**、累计扣血、药水、救命、存活与清场指标逐根相同。平均逻辑工作为 5,998.75 与 3,124.25。需要特别保留以下不相同的事实：

| R02 时点 | D_actor_tail 各敌人 HP | F_cheap 各敌人 HP |
|---|---|---|
| 3 | 23、179 | 45、157 |
| 7 / 14 | 23、81 | 45、59 |

初次本地提取错误地断言敌人 HP 向量也相同，R02 使该断言失败；之后读取原始向量，修正提取范围，没有重新运行实验。首轮与配额补充的“HP、敌方 HP 等结果相同”只能按列明的聚合指标理解，不能外推完整状态等价或所有后续政策等价。R02 的既定外部推进下两方案最终均全队死亡；其它未来队友策略可能使分配差异变得重要。

原文 S1 的方法简称 `CompareMultiplayer` 不是实际方法名；正确入口为 `CompareMultiplayerQualityAtCycle`，源码第 77 行。超额节点分 `ApplyScore` 属于同文件中的 `MultiplayerHpLossBudget`，不是 `MultiplayerSearchPolicy` record。原始摘录与核心政策结论相符，本地准确入口见[源码复核](../local-source-review.md)。

## 实验不能回答的事项

首轮 780 次主对照没有配额/前沿算法。因此 R 的结果支持“先检验便宜方案”的工程顺序，**不证明尚未运行的配额方案已胜过任何算法**。D 是开发、H/Q 后来参与选型，只有 R 是 actor 尾值冻结后未再调参的八个程序化测试根；仍不是独立真人样本。

原型在各根只选一次当前完整动作，外部随后使用统一脚本；并未在每轮重新调用求解器。它不能证明连续手动重算后的整场策略质量或消除滚动目标拖延。还有明确简化：两人、公开根第一抽、至多一个当前交错点、固定风格、玩具牌组和伤害规则、有限动作深度、尾值中的平均容量及通用格挡。生产 actor/choice、ready 根、未知信息和资源账本都没有在此实现。

原型的当前致死硬过滤只覆盖部分本机动作前缀自杀，低预算初始建议和原生敌方结算没有完整覆盖；有限死亡罚不保证本机始终存活。首轮设计提出的自损储备门禁亦未实际运行。不能把这些研究原型称为已可直接上线。
