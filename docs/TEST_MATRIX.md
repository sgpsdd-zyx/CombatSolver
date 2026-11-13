# CombatSolver 测试入口

当前正在合入官方 `06083d36 / 0.51.3`，多人 fork 0.51.4 尚未定版。公开版仍为 [0.50.2](releases/0.50.2-PUBLISH.md)。[本轮记录](archive/strategy/upstream-0513-merge-20261113.md)维护实际验证与未完成项；[无人测试](HEADLESS_TESTING.md)维护分层口径。

## 2026-11-13 本轮状态

源码冲突标记已清除，Bash 职责门禁通过（270 Search 文件），coverage 布局通过（649 数据、597 fixture、38 组），文档门禁通过。工具盘点因缺 PowerShell 7 未通过；`.ps1` 解析未验证。

初始本机游戏缺少 0.113 API，RitsuLib 0.6.10 缺少 `compat/0.113.0`。Release 构建在引用验证阶段失败（2.48 秒，尚未进入 C# 编译）；多人原生差分、离线搜索对照及覆盖目录再生成均 pending。旧测试与上游测试不是本轮证据。

## 计划验证

- L0：Release 构建、双端结构规则、文档/工具/coverage 布局检查，CoverageCatalog 状态与分支读取验证。
- L2：多人逐玩家新增状态的根/Fork/生产键/完整续用对账、额外回合来源、多目标模式结算；覆盖死亡队友监听隔离。
- 搜索：单人官方同根短搜对照、多人兼容合同与贡献政策哨兵，验证首领追加成员被多人政策清空。
- 原生无头：选择能穿过改动边界的最小请求，在同一隔离实例执行；单项不超过120秒，结束后由启动器清理。

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
