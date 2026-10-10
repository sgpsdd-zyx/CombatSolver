# 重构路线状态

官方策略与搜索重构于 2026-09-28 收尾：P0～P6 已按固定根与对应边界收口。P7 已有权重探针和免费生成药链，默认权重未校准；目标代表试验因哨兵退化撤回。P8 只有局部归因与诊断工具，尚未达到退出标准。P7/P8 的新增工作已停止，不沿旧计划继续推进。

职责与当前实现见 [架构地图](../ARCHITECTURE.md)。阶段验收、源码基线和剩余局限见 [实施总结](../archive/refactoring/strategy-search-refactor-summary-20260928.md)；旧路线全文见 [历史路线](../archive/refactoring/refactor-roadmap-before-20261003.md)。后续整合证据见 [2026-10-03 合并审计](../archive/refactoring/merge-audit-20261003.md)。

本 fork 已跟随官方 0.49.0 完成目录迁移，多人合同归 `src/Testing/Contracts/Multiplayer`，现役指南保留多人边界；历史报告进入 archive。此后合入官方 0.50.1 后续主线（`5c773caa`），玩家 Hook 资格与逐成员手牌上限归根/分支状态，单人估值、保路与计划证书仍与多人隔离；证据见 [本 fork 合并记录](../archive/strategy/upstream-0501-merge-20261009.md)。

新重构只围绕当前任务的明确边界展开，取得对应最小证据后替换本状态，不将阶段流水账追加到路线首页。

coverage 与 Testing 已按当前职责入口精简：历史批次材料由固定提交保存，公共测试框架与有效回归继续维护，一次性调查代码退出正式源码树。维护证据见 [测试矩阵](../TEST_MATRIX.md)，目录边界见 [Testing 入口](../../src/Testing/README.md)。
