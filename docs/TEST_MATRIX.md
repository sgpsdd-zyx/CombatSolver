# CombatSolver 测试入口

当前源码基于官方 `5c773caa / 0.50.1 后续主线`，fork 0.50.2 本轮验证如下。发布状态见 [版本索引](releases/README.md)，测试分层见 [无人测试](HEADLESS_TESTING.md)，职责见 [Testing](../src/Testing/README.md)。历史结果不替代本轮验证。

## 2026-10-09 本轮结果

| 范围 | 实际结果与边界 |
| --- | --- |
| 官方单人对照 | 能力牌与强制用药两根各DOP1/DOP2，四对346项非时序字段一致，含路线、根/续用与工作计数；多人入口均零调用。不是可见性能结论。 |
| 多人兼容 | 69项通过，含本机/队友各自手牌上限、旧根隔离、三代Fork、生产状态键/续用签名，以及单人估值、能力/计划/成长/药水证书隔离和完整原生全队状态。 |
| 多人贡献与死亡 | 32项阶段/归属/预算/增量合同；救命、死亡、复活、下一回合和3次手动重算通过。托管宿主旁路渲染与网络。 |
| 原生无头 | 11个请求通过：手牌上限根、Ctrl+F9生命周期、检查点profile、旧报告边界、选牌组合、HP修饰集合、固定前缀、Smart追加审计、Smart开局、进攻保路哨兵、多人烘焙手套DOP2操作。 |
| 完整部署哨兵 | CALCULATED-ATTACK-ROUTING-SENTINEL，Instant/0秒，第一回合胜利、HP60、计划外重算0。仅此固定哨兵结论。 |
| 轻量合同 | 组合搜索118项、能力估值104项通过。GC portable-runtime 在实际NoGC区域准入前提处失败，不能报告该组通过。 |
| 结构与覆盖 | Bash264个Search文件；CoverageCatalog3035项，状态字段/分支读门禁通过，生成目录含85选牌/19自动出牌/51阵容来源；coverage布局577数据/527fixture/37组通过。 |

[合并记录](archive/strategy/upstream-0501-merge-20261009.md)、[逐项证据](archive/strategy/upstream-0501-merge-20261009-evidence.json)和[复跑输入](archive/strategy/upstream-0501-merge-20261009-inputs.json)保留本轮runId、检查细项、输入和失败。11项原生请求共用一个隔离实例，启动器已成功删除整个实例目录；没有启动可见Steam。

## 失败与平台条件

- GC portable-runtime 第五项要求实际建立NoGC区域，本机CLR未能满足，进程返回134；未降低断言或反复重试，后续同组检查未执行。NoGC重启能力仍未验证。
- 官方组件回复/Smart证书的原生MVID门禁不认证本机macOS程序集，相关启用分支本轮未运行。保留官方回退，不声称获得贡献者Linux性能收益。
- 工具盘点321文件/38项目仅剩PowerShell7缺失，`.ps1`运行/语法未验证；对应Bash实际通过，双端结构规则同步维护。

## 可重跑入口

Windows / Linux 使用 `tools/testing/run-unattended-test.ps1` 和 `.sh`。macOS将配套输入 `nativeRequests` 写为JSON后交给下列原生入口；每项上限120秒，最后一项清理实例。纯一步合同不添加增量搜索验证。

```bash
./tools/testing/run-unattended-test-macos.sh <request.json> ... --timeout-seconds 120 --cleanup-instance-on-exit
dotnet build tools/search/OfflineSearchHarness/OfflineSearchHarness.csproj -c Release
dotnet .local/tool-build/OfflineSearchHarness/bin/Release/net9.0/OfflineSearchHarness.dll --multiplayer-review-contracts upstream-compatibility --encounter FUZZY_WURM_CRAWLER_WEAK --beam 4 --nodes 100 --budget-ms 1000 --dop 1 --out .local/review/upstream-compatibility
```

同宿主 `quota-contracts` 使用Beam8/100节点/3000ms；`dead-teammate` 使用NECROBINDER、Beam4/100节点/1000ms。它们含原生调用与完整状态比较；普通离线搜索指标本身不是语义验收。

```bash
./tools/inspection/verify-refactor-boundaries.sh
python3 tools/inspection/verify-documentation.py
python3 tools/inspection/verify-tools.py
python3 tools/inspection/verify-coverage.py
dotnet run --project tools/inspection/CoverageCatalog/CoverageCatalog.csproj -c Release -- . --verify-state-fields --verify-branch-state-reads
python3 tools/testing/checks/BeamWidthPortfolioChecks/run.py
dotnet run --project tools/testing/checks/PowerCardValuationChecks/PowerCardValuationChecks.csproj -c Release
dotnet run --project tools/testing/checks/CombatSolver.GcPolicyChecks/CombatSolver.GcPolicyChecks.csproj -c Release -- portable-runtime
```

## 未验证与历史

真实联机、可见UI/帧时间、Windows/Linux/移动端运行、Windows MemoryCleaner、完整第三方栈、全部问题ZIP、干净安装及完整发布门禁未执行。旧版缩小甲虫原始问题未追加验证。独立官方对照仅覆盖本机能启用的路径。

[上一轮fork测试](archive/testing/multiplayer-0501-20261005.md)、[官方本轮来源](archive/testing/upstream-0501-20261009.md)和[历史分卷](archive/testing/README.md)保留原始日期和证据等级。
