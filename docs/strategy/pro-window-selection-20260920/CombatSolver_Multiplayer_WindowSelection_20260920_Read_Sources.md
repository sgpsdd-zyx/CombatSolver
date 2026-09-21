# 当前源码实际读取清单

基线：`851c1521f5258ec43dc71c24eeb8d5200fdf612d` /0.43.3。
唯一当前源码目录：`CombatSolver_851c1521_current_review/CombatSolver-851c1521/`。

## 输入读取

`CombatSolver-window-selection-review-request.md`完整阅读。ZIP独立解压，2,150个文件；ZIP注释提交与请求一致，manifest版本0.43.3。逐文件输入SHA-256在 Input_Manifest.json；不把注释当作与远端Git blob逐一校验。未访问旧GitHub树替代本地未推送提交。

## 调用链读取记录

表中“请求输出行段”来自实际阅读辅助脚本的日志；长联合输出曾发生截断，因此与“完整逐行阅读”明确区分。源码引用只用于所核对的方法；未读部分不自动获得审查结论。

| 文件 | 请求输出的物理行段（合并） | 阅读状态／限制 |
|---|---|---|
| `AGENTS.md` | 1–229 | 按段阅读。长联合输出被截断；仅部分阅读，用到的当前官方基线／约束在开头。非全文件审计。 |
| `docs/multiplayer-advisor.md` | 1–238 | 按段阅读。部分阅读；24–170为重点，历史七周期段只作历史，当前参数以代码为准。 |
| `docs/ARCHITECTURE.md` | 1–538 | 按段阅读。长输出被截断；职责定位／检索，不据此声称完整架构审计。 |
| `CombatSolver.json` | 1–17 | 完整阅读。关键调用链／实验证据；不是整个目录审计。 |
| `src/Search/MultiplayerSearchPolicy.cs` | 1–95 | 完整阅读。关键调用链／实验证据；不是整个目录审计。 |
| `src/Search/CombatBeamSolver.MultiplayerEvaluation.cs` | 1–117 | 完整阅读。关键调用链／实验证据；不是整个目录审计。 |
| `src/Search/CombatBeamSolver.Multiplayer.cs` | 1–161 | 完整阅读。关键调用链／实验证据；不是整个目录审计。 |
| `src/Search/MultiplayerCycleCheckpoint.cs` | 1–6 | 完整阅读。关键调用链／实验证据；不是整个目录审计。 |
| `src/Search/CombatBeamSolver.Phases.cs` | 1–465, 490–515, 837–855, 900–996, 1450–1815, 1935–2215 | 按段阅读。多次按调用链分段；联合大输出部分截断，关键1450–1815、1985–2185另有较小段核对。不声称读全2523行。 |
| `src/Search/CombatSearchCoordinator.cs` | 1–52 | 按段阅读。关键调用链／实验证据；不是整个目录审计。 |
| `src/Search/CombatBeamSolver.MultiplayerRound.cs` | 1–156 | 完整阅读。关键调用链／实验证据；不是整个目录审计。 |
| `src/Search/SimulatedCombatState.Multiplayer.cs` | 1–62 | 完整阅读。关键调用链／实验证据；不是整个目录审计。 |
| `tools/OfflineSearchHarness/MultiplayerFinalSelectionContracts.cs` | 1–254 | 完整阅读。关键调用链／实验证据；不是整个目录审计。 |
| `tools/OfflineSearchHarness/MultiplayerHorizonContracts.cs` | 1–529 | 按段阅读。1–384、408–529为重点；385–407原生动作核对段只部分可见。未运行宿主。 |
| `tools/OfflineSearchHarness/MultiplayerReviewContracts.cs` | 1–220 | 完整阅读。关键调用链／实验证据；不是整个目录审计。 |
| `src/Runtime/SolverController.Multiplayer.cs` | 1–44 | 完整阅读。关键调用链／实验证据；不是整个目录审计。 |
| `src/Search/CombatBeamSolver.Expansion.Replay.cs` | 1170–1290 | 按段阅读。关键调用链／实验证据；不是整个目录审计。 |
| `src/Search/SimulatedCombatState.cs` | 1425–1463 | 按段阅读。关键调用链／实验证据；不是整个目录审计。 |
| `src/Search/CombatBeamSolver.cs` | 27–117 | 按段阅读。关键调用链／实验证据；不是整个目录审计。 |
| `src/Search/CombatPlan.cs` | 1063–1187 | 按段阅读。关键调用链／实验证据；不是整个目录审计。 |
| `src/Search/CombatBeamSolver.AdmittedExpansion.cs` | 1–103, 170–232 | 按段阅读。关键调用链／实验证据；不是整个目录审计。 |
| `src/Search/CombatBeamSolver.Expansion.cs` | 1–72, 390–513 | 按段阅读。关键调用链／实验证据；不是整个目录审计。 |
| `src/Search/CombatBeamSolver.Terminal.cs` | 1–100 | 按段阅读。关键调用链／实验证据；不是整个目录审计。 |
| `src/Search/CombatBeamSolver.Retention.cs` | 1–145 | 按段阅读。关键调用链／实验证据；不是整个目录审计。 |
| `src/UI/SolverOverlaySnapshot.cs` | 245–265 | 按段阅读。关键调用链／实验证据；不是整个目录审计。 |
| `docs/strategy/pro-horizon-20260919/CombatSolver_Multiplayer_Horizon_20260919_Experiments.py` | 638–690 | 按段阅读。关键调用链／实验证据；不是整个目录审计。 |

另对当前 `docs/TEST_MATRIX.md`、多人指南、旧长线档案做过定点检索；这些是历史证据导航，不是本轮重新执行。`Source_Excerpts.txt`把报告中最重要的已读源码段带上物理行号，便于离线核对；它不是额外完整源码副本。

## 旧研究如何使用

本次ZIP中的 `pro-horizon-20260919/...Experiments.py:638–690`读取了共同周期压缩、F12、风险尾部和资格的旧反例实现。也定位了 `pro-long-term-20260918` 的F10/F12归档。没有原样重跑旧七周期／旧预算脚本，因为它不是当前0.43.3策略；本轮用M03/M16及W03等新模型分别检验同分假改善与固定资源机会成本，明确不是复跑旧生产或Beam8实证。旧全文报告与引擎全卡目录没有逐行重审。

## 原始算法资料

正文P1–P6列出原始作者／出版方来源。P1只读摘要和作者出版信息；P2–P6读取指定章节／命题及有关截图，不声称完整通读所有PDF。记录见 Web_Sources.json。没有把论文定理前提不足之处用“经典算法保证”代替。

## 没有完成的读取或执行

没有源码文件访问失败；长输出截断已标记为部分读取。没有全面阅读整个2,150文件、全部共享卡牌镜像、完整转置／能力承诺框架或全部单人路径。缺少 `dotnet` 和游戏DLL，生产C#、原生差分与联机均未运行。本轮输出在源码目录之外，没有仓库补丁、推送或发布。
