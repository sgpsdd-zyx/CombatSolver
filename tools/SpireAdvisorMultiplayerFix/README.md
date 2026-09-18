# 尖塔军师：本机玩家修复工具

此目录维护用户提供的独立「尖塔军师」拿牌推荐模组的二进制修复。它不进入 CombatSolver 的编译或运行路径，也不修改 CombatSolver 的 0.41.2 版本。原 ZIP 与派生 DLL 保存在被忽略的本地目录，只有修复工具、验证和说明进入源码提交。

## 输入与根因

输入文件名为 `尖塔军师(自用拿牌推荐)-v0.1.2.zip`，只有 DLL、manifest 和两份说明，没有源码。包内 manifest 和两份说明为 v0.1.0，程序集版本为 0.0.0.0；本工具不以文件名冒充可证明的版本。安全解包目录为 `.local/issue-bundles/spire-advisor-v0.1.2-multiplayer/raw/`。

`RealtimeAdvice.GameAccess.ReadDeckCards` 原先递归遍历跑局对象，在 `Players` 中取得第一副非空卡组便返回，未使用本机网络身份。客机位于索引 1 时可以直接复现返回索引 0 的卡组。奖励、商店、删牌及卡组诊断都使用该入口。血量和遗物读取也依赖猜测字段，遗物缓存没有玩家所有者。

## 修复边界

`Patcher.cs` 使用 Mono.Cecil 0.11.6 保留原程序集，只替换三个数据入口，并增加一个本机玩家解析函数：

- `ResolveLocalPlayer` 调用当前游戏的公开 `RunManager.Instance.DebugOnlyGetState()` 与 `LocalContext.GetMe(IPlayerCollection)`。网络 ID 的解释、单人身份和缺失玩家错误仍由原版负责。
- `ReadDeckCards` 只复制该玩家的 `Deck.Cards`，保留实例身份、重复项与顺序。不会把真实牌堆的可变列表交给调用方，空卡组也不会继续遍历其他玩家。
- `ReadPlayerHp` 读取该玩家的 `Creature.CurrentHp` 与 `Creature.MaxHp`。无跑局或本机身份尚未设置时保留原有 `(0, 0)` 不可用表示；ID 已设置但不在玩家列表时沿用原版异常，不回退到房主。
- `ReadOwnedRelics` 复用原有遗物 ID 提取及去重，但输入严格限定为本机玩家，不复用没有玩家归属的五秒缓存。

没有改动评分、职业知识、内置数据、推荐 UI 或已经停用的地图功能。修复工具只在构建时使用 Mono.Cecil，成品不需要额外 DLL。程序集及包版本统一为 0.1.3，安装目录和模组 ID 保持原值。

## 重跑

需要 .NET 9 SDK 和游戏 0.111.0 的托管程序集目录。`local.props` 存在时自动读取本机路径，也可使用 `-p:Sts2DataDir="<game-managed-directory>"`。Mono.Cecil 默认从 NuGet 获取；已有副本可通过 `-p:CecilPath="<Mono.Cecil.dll>"` 指定。

```text
dotnet run --project tools/SpireAdvisorMultiplayerFix -p:Sts2DataDir="<game-managed-directory>" -- "<original.dll>" "<game-managed-directory>" --self-test
dotnet build tools/SpireAdvisorMultiplayerFix -c Release -p:Sts2DataDir="<game-managed-directory>"
dotnet tools/SpireAdvisorMultiplayerFix/bin/Release/net9.0/SpireAdvisorMultiplayerFix.dll "<original.dll>" "<game-managed-directory>" "<new-output.dll>"
python3 tools/SpireAdvisorMultiplayerFix/package.py "<extracted-original-directory>" "<new-output.dll>" "<new-output.zip>"
```

测试只调用已审查的数据访问方法，不运行包内的 Mod 初始化入口。测试进程使用真实托管 `RunState`、`Player`、`CardPile`、`Creature` 和 `LocalContext`，直接注入最小状态，不启动游戏、Steam 或网络，不接触存档。

本轮直接证据：旧逻辑返回房主卡组的失败基线已复现；单人至四人共 20 种座位／顺序组合通过。另覆盖同类卡牌不同实例、顺序与重复项、空卡组、拿牌后更新、返回列表隔离、遗物变化、残留缓存、零血量、空身份、身份不在列表、零 ID、相同 ID 重新入局以及退出跑局。全部 1013 项断言通过，其中包含原 DLL 的 913 个无关方法和 8 份内嵌资源保持一致的结构断言。

真实网络联机、Godot 可见界面、所有场景下的推荐结果和 Windows 实机未验证。通过的数据归属检查不等于多人推荐质量或胜率验收。最小包一次性创建在 `releases/`；成功后不重新解包或重复验证。没有标签、远端推送或外部发布。

玩家说明见 [RELEASE_NOTES.md](RELEASE_NOTES.md)，本轮记录见 [测试矩阵](../../docs/TEST_MATRIX.md#独立模组尖塔军师本机玩家修复2026-09-18)。
