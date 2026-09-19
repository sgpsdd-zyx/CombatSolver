# CombatSolver_Multiplayer_Horizon_20260919：源码阅读与证据定位

唯一当前提交：`3ccac172dd55ad4a8d97074b7129cbf4155add65`。材料渠道为本次完整源码ZIP，而非旧发布包或GitHub默认分支。

本轮有 59 条带物理行号的读取记录，涉及 29 个不同文件。下表是读取请求范围的合并清单；不等于宣称每次大输出均完整显示，更不表示通读全部2056文件。关键报告引用段已定向复核。

|当前附件内路径|物理行数|定向读取范围|
|---|---:|---|
|`docs/ARCHITECTURE.md`|507|130–174|
|`docs/TEST_MATRIX.md`|3777|1–145|
|`docs/multiplayer-advisor.md`|215|1–100|
|`docs/strategy/pro-long-term-20260918/CombatSolver_Multiplayer_LongTerm_Experiments_20260918.py`|618|1–115, 261–334, 439–477, 554–618|
|`src/Runtime/CombatRootSnapshot.cs`|311|130–206|
|`src/Runtime/SolverController.Multiplayer.cs`|44|1–44|
|`src/Runtime/SolverController.cs`|3539|1120–1250|
|`src/Search/CombatBeamSolver.Expansion.cs`|4464|1–155, 460–710, 3134–3245, 3390–3460|
|`src/Search/CombatBeamSolver.Multiplayer.cs`|161|1–161|
|`src/Search/CombatBeamSolver.MultiplayerEvaluation.cs`|117|1–117|
|`src/Search/CombatBeamSolver.MultiplayerRound.cs`|156|1–156|
|`src/Search/CombatBeamSolver.PathDiagnostics.cs`|414|1–155|
|`src/Search/CombatBeamSolver.Phases.cs`|2427|146–240, 345–535, 790–940, 1030–1080, 1300–1468, 1524–1704, 1870–2008, 2028–2135|
|`src/Search/CombatBeamSolver.Retention.cs`|2039|82–116, 273–330, 370–572|
|`src/Search/CombatBeamSolver.RoundTransition.cs`|364|1–364|
|`src/Search/CombatBeamSolver.StateEvaluation.cs`|1875|1550–1605|
|`src/Search/CombatBeamSolver.cs`|179|40–60|
|`src/Search/CombatPlan.cs`|1890|1060–1165|
|`src/Search/CombatSearchCoordinator.cs`|2044|1–100|
|`src/Search/MultiplayerCycleCheckpoint.cs`|6|1–6|
|`src/Search/MultiplayerSearchPolicy.cs`|74|1–74|
|`src/Search/SearchDiagnosticsSink.cs`|163|1–163|
|`src/Search/SimulatedCombatState.Multiplayer.cs`|62|1–62|
|`src/Search/SimulatedCombatState.cs`|2936|426–452|
|`src/Search/SolverWeights.cs`|137|77–89|
|`tools/OfflineSearchHarness/MultiplayerContracts.cs`|326|1–140|
|`tools/OfflineSearchHarness/MultiplayerEvaluationContracts.cs`|277|1–277|
|`tools/OfflineSearchHarness/MultiplayerLongTermContracts.cs`|277|1–180|
|`tools/OfflineSearchHarness/Program.cs`|492|118–160|

## 补充阅读、边界与失败

另有指南、架构前部、研究归档README及版本元数据的直接读取；JSON给出其范围。本文并不声称重新运行仓库中的历史C#合同。

必要当前源码均由附件可读。`SearchNode`等最初按独立文件猜测的路径已解析到实际定义文件；这是检索修正，不是源码缺失。没有穷尽全部卡牌／网络／转置／单人代码。

没有.NET或游戏DLL。生产C#、原生差分、真实联机和真实硬件预算收益均未验证。

## 原始资料的实际阅读范围

**Janner et al. 2019 MBPO** — §4–5；PDF第4、6页截图，非逐页全文审计。[原始资料](https://proceedings.neurips.cc/paper/2019/file/5faf461eff3099671ad63c6f3f094f7f-Paper.pdf)

**Talvitie 2017 Self-Correcting Models** — 摘要、引言与组合误差讨论；PDF第1页截图。[原始资料](https://ojs.aaai.org/index.php/AAAI/article/view/10850/10709)

**Rawlings/Mayne/Diehl MPC textbook** — 印刷页90、164，PDF索引137、211截图；不是整本通读。[原始资料](https://sites.engineering.ucsb.edu/~jbraw/mpc/MPC-book-2nd-edition-3rd-printing.pdf)

**Nau 1982 Investigation of Pathology** — 摘要、引言；PDF第1页截图。[原始资料](https://www.cs.umd.edu/~nau/papers/nau1982investigation.pdf)

L’Ecuyer随机流PDF尝试失败，未使用其内容；Nau初始猜测路径失败后，作者正确原文可读。论文只提供测量／失效机制的依据，不证明H=7最优。

## 本轮Python的函数与反例定位

|类型|名称|脚本物理行号|
|---|---|---|
|ClassDef|`Action`|35–46|
|ClassDef|`Fixture`|49–69|
|ClassDef|`CP`|72–80|
|ClassDef|`State`|83–104|
|ClassDef|`Exhausted`|106–107|
|ClassDef|`Work`|109–131|
|FunctionDef|`__init__`|110–116|
|FunctionDef|`spend`|118–125|
|FunctionDef|`sort`|127–131|
|FunctionDef|`compare`|128–130|
|FunctionDef|`root`|134–136|
|FunctionDef|`action_by_name`|139–140|
|FunctionDef|`loss`|143–144|
|FunctionDef|`excess`|147–148|
|FunctionDef|`team`|151–152|
|FunctionDef|`damage`|155–164|
|FunctionDef|`legal`|167–179|
|FunctionDef|`transition`|182–295|
|FunctionDef|`common_cycle`|298–316|
|FunctionDef|`final_key`|319–343|
|FunctionDef|`eligible`|346–352|
|FunctionDef|`batch`|355–366|
|FunctionDef|`retain`|369–408|
|FunctionDef|`snapshot`|411–420|
|FunctionDef|`solve`|423–543|
|FunctionDef|`causal_tail`|546–559|
|FunctionDef|`external`|562–579|
|FunctionDef|`attach_evaluation`|582–588|
|FunctionDef|`fixtures`|591–638|
|FunctionDef|`micro_checks`|641–741|
|FunctionDef|`add`|643–646|
|FunctionDef|`node`|647–654|
|FunctionDef|`strip_timing`|744–749|
|FunctionDef|`main`|752–843|

运行输出和每个fixture完整trace见Results.json；微型反例见`micro_checks`，完整旧脚本结果见Legacy_Rerun.json。源文件哈希见Source_Integrity.json。
