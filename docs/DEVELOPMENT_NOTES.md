# CombatSolver 开发笔记

当前公开版为 [0.51.4](releases/0.51.4-PUBLISH.md)，基于官方 `06083d36 / 0.51.3`。本轮变化、失败与证据已进入[开发归档](archive/development/multiplayer-0514-20261113.md)和[合并记录](archive/strategy/upstream-0513-merge-20261113.md)。

## 0.51.6（定版中）

- 合入官方 `3677d4ca / 0.51.5`：死亡转阶段、首个箭槽、随机流、选牌Fork历史、回合能力顺序、原生选择续接和便携GC修正。保留显式多人政策与手动入口。
- 13项原生请求、82项多人兼容和32项贡献合同通过。GC实际NoGC区域前提失败，后续检查未执行。详情与限制见[本轮合并记录](archive/strategy/upstream-0515-merge-20261114.md)；官方原始开发记录见[卷27](archive/development/volume-27.md)与[卷28](archive/development/volume-28.md)。
