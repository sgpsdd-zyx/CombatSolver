# 尖塔军师 v0.1.3

- 修复加入别人房间后，拿牌推荐和卡组诊断误用房主或其他队友卡组的问题。
- 推荐使用的卡组、血量和遗物统一按本机玩家读取，调整房间座位不影响识别。
- 本机没有卡牌或遗物时不再借用队友数据；重新加入跑局后重新读取当前玩家。

社区统计的样本口径仍为单人模式，推荐可作为联机拿牌时的参考。

## English

- Fix card recommendations and deck diagnosis using the host's or another teammate's deck when joining a multiplayer lobby.
- Read the deck, health, and relics from the local player regardless of roster order.
- Keep an empty local deck or relic collection empty, and refresh player data after rejoining a run.

Community statistics remain based on single-player runs and serve as reference information.

## 验证与来源

已用游戏 0.111.0 的托管对象验证本机数据读取，包括单人及二至四人、不同座位顺序、空卡组、身份缺失、重新加入和遗物变化。尚未验证真实网络联机与可见界面。

本包来自用户提供的 v0.1.2 ZIP；原包 manifest 和说明实际写作 v0.1.0，原 DLL 程序集版本为 0.0.0.0。本包统一标为 v0.1.3。原有评分逻辑及内置数据保持不变。

随包 LICENSE 与 THIRD_PARTY_NOTICES.md 保留本仓库的许可及来源说明；原模组的数据来源说明见「功能说明.txt」。
