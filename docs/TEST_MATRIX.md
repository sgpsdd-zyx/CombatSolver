# CombatSolver 测试入口

当前源码基于官方 `0d290fbe / 0.50.0`，fork 0.50.1 定版中。发布状态见 [版本索引](releases/README.md)，测试分层见 [无人测试](HEADLESS_TESTING.md)，职责见 [Testing](../src/Testing/README.md)。历史结果不替代本轮验证。

## 2026-10-05 本轮结果

| 范围 | 实际结果与边界 |
| --- | --- |
| 官方单人对照 | 两根各DOP1/DOP2，四对346项非时序字段一致，含路线、根/续用和工作计数；多人入口均零调用。不是可见性能结论。 |
| 多人兼容 | 60项通过，新增单人组件回复/成长证书、共享胜利表和Smart剪枝隔离；含历史、金币死亡复活、金纸归属、完整原生全队状态和Fork。 |
| 多人贡献与死亡 | 32项阶段/归属/预算/增量合同；救命、死亡、复活、下一回合和手动重算通过。托管宿主旁路渲染与网络。 |
| 原生无头 | 23个最终请求通过，覆盖回合末/额外回合、虚空形态选择/短搜部署、群体杀敌、金币/能量/手牌、反伤格挡、Smart开局、内容来源和中英反馈。 |
| 多人输入与生命周期 | 额外回合来源/归属、烘焙手套选择/DOP2取消/队友行动、4次手动重算和同进程单人恢复通过。原生队友脚本属于测试，不进入产品搜索。 |
| 轻量合同 | GC能力降级5项，胜利界20项，组合成员105项，OnPlay41+5项通过。GC注入检查不代表移动运行库验收。 |
| 结构与覆盖 | Bash263个Search文件，CoverageCatalog3035项，选牌85/自动出牌19/阵容51个来源；coverage布局565数据/515fixture通过。 |

[合并记录](archive/strategy/upstream-0500-merge-20261005.md)、[逐项证据](archive/strategy/upstream-0500-merge-20261005-evidence.json)和[复跑输入](archive/strategy/upstream-0500-merge-20261005-inputs.json)保留runId、检查细项、首次失败、预算与输入。五个owned headless实例由启动器清理成功，没有启动可见Steam。

## 失败与平台条件

- 群体减益首次种子未触发要求的首敌死亡；使用官方默认 `COMBATSOLVER` 种子后通过。生产源码与断言未改。
- `COMPONENT-HEALING-BOUND` 返回 `native-version`：官方组件证书不认证本机macOS程序集。相同前提的 `COMPONENT-SMART-BOUND`、`POTION-COST-INCUMBENT` 未运行；保留官方回退与门禁，不能报告这些启用分支已通过。
- 反馈引导的一次请求误用了不存在的场景名，主动终止并清理；真实入口 `CONTENT-MOD-FAILURES` 后续通过。
- 工具盘点320文件/38项目仅剩PowerShell7缺失，`.ps1`运行/语法未验证；两端结构规则同步维护，Bash实际通过。

## 可重跑入口

Windows / Linux 分别使用 `tools/testing/run-unattended-test.ps1` 和 `.sh`，从配套输入取场景ID、角色、遭遇和注入。每项上限120秒，最后一项带实例清理。macOS将配套输入 `nativeRequests` 写为临时JSON，保留实验路径相对输入归档的位置，或直接使用对应已提交fixture：

```bash
./tools/testing/run-unattended-test-macos.sh <request.json> ... --timeout-seconds 120 --cleanup-instance-on-exit
dotnet build tools/search/OfflineSearchHarness/OfflineSearchHarness.csproj -c Release
dotnet .local/tool-build/OfflineSearchHarness/bin/Release/net9.0/OfflineSearchHarness.dll --multiplayer-review-contracts upstream-compatibility --encounter FUZZY_WURM_CRAWLER_WEAK --beam 4 --nodes 100 --budget-ms 1000 --dop 1 --out .local/review/upstream-compatibility
```

同一宿主的 `quota-contracts` 使用Beam8/100节点/3000ms；`dead-teammate` 使用NECROBINDER、Beam4/100节点/1000ms。它们包含原生模型调用与完整状态比较；普通离线搜索指标本身不构成语义验收。

```bash
./tools/inspection/verify-refactor-boundaries.sh
python3 tools/inspection/verify-documentation.py
python3 tools/inspection/verify-tools.py
python3 tools/inspection/verify-coverage.py
dotnet run --project tools/inspection/CoverageCatalog/CoverageCatalog.csproj -c Release -- . --verify-state-fields --verify-branch-state-reads --verify-combat-choices --verify-autoplay-sources --verify-roster-sources
dotnet run --project tools/testing/checks/CombatSolver.GcPolicyChecks/CombatSolver.GcPolicyChecks.csproj -c Release -- portable-runtime
```

## 未验证与历史

真实联机、可见UI/帧时间、Windows/Linux/移动端运行、Windows MemoryCleaner、完整第三方栈、全部问题ZIP、干净安装及完整发布门禁未执行。缩小甲虫特定原始问题仍未验证。独立官方对照仅覆盖本机能启用的路径；上游证书MVID及性能数据不因此扩大适用范围。

[上一轮fork测试入口](archive/testing/multiplayer-0492-20261003.md)、[官方0.50.0入口](archive/testing/upstream-0500.md)和[官方历史分卷](archive/testing/README.md)保留原始日期和证据等级。当前架构由 [架构地图](ARCHITECTURE.md)维护。
