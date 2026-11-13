# CombatSolver 测试入口

当前源码基于官方 `06083d36 / 0.51.3`，多人 fork 0.51.4 已完成定向验证。[本轮记录](archive/strategy/upstream-0513-merge-20261113.md)维护证据与限制；发布状态见[版本索引](releases/README.md)，[无人测试](HEADLESS_TESTING.md)维护分层口径。

## 2026-11-13 本轮状态

游戏0.113.0 / RitsuLib0.6.12 / macOS arm64。主项目和离线宿主Release零警告、零错误；Bash职责门禁通过（270 Search文件），coverage布局通过（650数据、598fixture、38组），CoverageCatalog3437项无未分析及必需证据缺口。PowerShell门禁完成状态见本轮记录。

10个原生请求通过；82项多人兼容、32项贡献合同、死亡/复活与手动重算通过。两个单人固定根与独立构建官方比较，177个非计时字段和完整路线一致。原生输入、runId、失败与清理见[证据](archive/strategy/upstream-0513-merge-20261113-evidence.json)和[请求](archive/strategy/upstream-0513-merge-20261113-inputs.json)。旧测试与上游测试不是本轮证据。

## 本轮覆盖

- L0：Release构建、结构规则、文档/工具/coverage布局检查，CoverageCatalog状态与分支读取验证。
- L2：多人逐玩家新增状态的根/Fork/生产键/完整续用对账、额外回合来源、多目标模式结算；覆盖死亡队友监听隔离。
- 搜索：单人官方同根短搜对照、多人兼容合同与贡献政策哨兵，验证首领追加成员被多人政策清空。
- 原生无头：最小请求按批复用隔离实例；扩展注册测试必须位于新进程首项。单项不超过120秒，所有实例已由启动器清理。

## 可重跑入口

```bash
dotnet build CombatSolver.csproj -c Release -p:CopyModOnBuild=false
dotnet build tools/search/OfflineSearchHarness/OfflineSearchHarness.csproj -c Release
./tools/testing/run-unattended-test-macos.sh <request.json> --timeout-seconds 120 --cleanup-instance-on-exit
./tools/inspection/verify-refactor-boundaries.sh
python3 tools/inspection/verify-documentation.py
python3 tools/inspection/verify-tools.py
python3 tools/inspection/verify-coverage.py
```

离线宿主仍依赖对应游戏和 RitsuLib 程序集，只产搜索指标或限定的托管合同；不替代原生运行时差分。详细命令见[离线搜索宿主](OFFLINE_SEARCH_HARNESS.md)。Windows/Linux 原生入口分别为 `tools/testing/run-unattended-test.ps1` / `.sh`，必须带实例清理开关。

## 历史与边界

[上一公开 fork 验证](archive/testing/multiplayer-0502-20261009.md)与[官方 0.51.3 测试入口](archive/testing/upstream-0513-20261113.md)分别归档。真实联机、可见 UI/帧时间、Windows/Linux 实机、完整第三方栈、干净安装和完整发布门禁不在本轮范围。
