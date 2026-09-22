# 当前基线源码读取范围

`95fd98572884c1f2da97ffd5120167815fca6403` / fork `0.43.6`。唯一解压目录：`CombatSolver_95fd9857_cooperative_source`。输入文件由本轮提供；未以旧发布包或 GitHub 默认分支替代。

完整阅读 `research-request.md`。2192 个解压文件进行了内容校验，不等于逐个阅读2192个文件。下面区间使用当前 ZIP 的物理行号；`read_log.jsonl` 是请求输出范围日志，不是声称终端每个长输出都完整可见。

特别说明：Ranking 的1–908和 Candidates 的1–1050初次整文件输出被工具截断，只按后续定向范围支持结论；没有把这两次调用算作全文阅读。其他较长合并输出也可能截断，报告引用以清晰可见的定向读取为准。

|文件|请求读取的物理区间|总行数|范围解释|
|---|---|---:|---|
|`docs/multiplayer-advisor.md`|1–253, 135–185|253|定向源码/文档阅读；不覆盖未列部分|
|`src/Search/MultiplayerSearchPolicy.cs`|1–98|98|定向源码/文档阅读；不覆盖未列部分|
|`src/Search/CombatBeamSolver.MultiplayerEvaluation.cs`|1–120|120|定向源码/文档阅读；不覆盖未列部分|
|`src/Search/CombatBeamSolver.MultiplayerWindow.cs`|1–220, 34–66, 220–298|298|定向源码/文档阅读；不覆盖未列部分|
|`src/Search/CombatBeamSolver.Multiplayer.cs`|1–170|171|定向源码/文档阅读；不覆盖未列部分|
|`src/Search/CombatBeamSolver.MultiplayerRound.cs`|1–156|156|定向源码/文档阅读；不覆盖未列部分|
|`src/Search/SimulatedCombatState.Multiplayer.cs`|1–116|116|定向源码/文档阅读；不覆盖未列部分|
|`src/Search/CombatBeamSolver.BeamRetentionPolicy.Ranking.cs`|1–908, 838–908, 25–65|908|初次全文输出截断；确认25–65、838–908|
|`src/Search/CombatBeamSolver.Expansion.Candidates.cs`|1–1050, 1–205, 225–291, 411–486|1050|初次全文输出截断；确认1–205、225–291、411–486|
|`src/Search/CombatBeamSolver.StateEvaluation.cs`|80–170, 245–285, 338–375, 445–525|1901|定向源码/文档阅读；不覆盖未列部分|
|`src/Runtime/CombatRootSnapshot.cs`|1–190, 190–319|319|定向源码/文档阅读；不覆盖未列部分|
|`src/Search/CombatSearchCoordinator.cs`|1–82|2178|定向源码/文档阅读；不覆盖未列部分|
|`src/Search/CombatBeamSolver.Phases.cs`|1440–1520, 2090–2145, 2150–2230, 2154–2226|2532|定向源码/文档阅读；不覆盖未列部分|
|`src/Search/CombatBeamSolver.RoundTransition.cs`|1–135|362|定向源码/文档阅读；不覆盖未列部分|
|`src/Search/SimulatedCombatState.Potions.cs`|1–100|139|定向源码/文档阅读；不覆盖未列部分|
|`src/Search/CombatBeamSolver.cs`|30–85|180|定向源码/文档阅读；不覆盖未列部分|
|`src/Runtime/SolverController.Multiplayer.cs`|1–44|44|定向源码/文档阅读；不覆盖未列部分|
|`src/Search/SimulatedCombatState.LongTermResources.cs`|1–84|84|定向源码/文档阅读；不覆盖未列部分|
|`src/Engine/InCombat/Mirrors/Hooks/Death/DeathPreventerMirrors.cs`|1–80|80|定向源码/文档阅读；不覆盖未列部分|
|`src/Search/CombatBeamSolver.Expansion.Replay.cs`|490–590, 1220–1290|1291|定向源码/文档阅读；不覆盖未列部分|
|`tools/OfflineSearchHarness/MultiplayerWindowSelectionContracts.cs`|20–106|413|定向源码/文档阅读；不覆盖未列部分|
|`tools/OfflineSearchHarness/MultiplayerCoveredWindowContracts.cs`|1–105|274|只读fixture建立与选择入口；未运行|
|`tools/OfflineSearchHarness/MultiplayerDeadTeammateContracts.cs`|1–95|212|只读原生对账的调用入口；未运行|

补充只读：`CombatSolver.csproj` 前38行用于版本/目标框架；目录名与 grep 结果只作索引，不作整文件已读证明。

## 未读或未验证的主要范围

没有通读全部引擎镜像、所有卡牌模型、全部1901行 StateEvaluation 或整个2532行 Phases；没有验证所有原生消费者。没有读原版 DLL、私人实战包或凭据。旧研究文件仅用作请求中已指出反例的历史背景，未当成当前行为源码。

没有执行 OfflineSearchHarness、原生差分、单人完整回归或联机。CoveredWindow 的 detached 排序 fixture 与 DeadTeammate 的原生调用是两种不同证据入口；阅读它们不等于运行任何一个。Python 的 F10/F12 小检查只保留逻辑反例，旧脚本没有重跑；本轮另外实际运行D12及新Beam1/2/3对照。

## 完整性与复核入口

ZIP SHA-256：`c0a0a82ac497ae7a4b8a95f59cee70d1b6de529a3e8eb33f40b8f59c16659d3a`。文件逐项 SHA 在 `input_manifest.json`；提交身份来自本轮请求与附件，ZIP 没有可供独立重建 Git commit 对象的完整 `.git`，不声称密码学验证了提交树归属。

论文来源及实际阅读部分单列于 `Papers.md`。报告 `[S#]` 指向其源码证据表，均为这个固定源码，不是可变网页行号。