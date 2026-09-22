# 本地最小探针

这些结果由本任务在 2026-09-22 实际运行一次，独立于 6 Pro 的外部抽象实验。它们定位目标函数及设计必要条件，不证明真实多人收益。

## 生产比较函数

[`OrderingProbe.cs.txt`](OrderingProbe.cs.txt) 通过现役 `0.43.6` DLL 调用 `CompareMultiplayerQualityAtCycle` / `CompareMultiplayerAtCycle`；源码基线 `95fd98572884c1f2da97ffd5120167815fca6403`，DLL 复用已发布构建。只构造比较器读取的冻结候选字段，没有构造游戏根、模拟牌序、搜索候选池或启动 Godot。

[`ordering-results.json`](ordering-results.json) 记录 10 项实际结果，断言均通过：

| 合成对照 | 当前比较结果 |
|---|---|
| 活着获胜但扣 4 HP；未胜扣 3 HP | 选未胜路线 |
| 扣 4 HP 保住两人；扣 3 HP 只剩本机 | 选只剩本机 |
| 敌人余 10 HP、扣 4 HP；敌人余 100 HP、扣 3 HP | 选敌人余 100 HP |
| 共同周期事实相同，探索估值不同 | 质量比较相等 |
| 共同周期事实相同，高估值多一动作 | A 的完整比较选动作更少者；C 的共同周期动作成本不由此项验证 |
| 胜利且没有超额；未胜且没有超额 | 选胜利，控制项 |
| 多打 90、扣 3 HP；少打、扣 0 HP | 选多打，控制项 |
| 本机死亡且敌人全死；本机存活未胜 | 选本机存活，控制项 |
| 用过一次救命效果后胜利；不用且未胜 | 选未胜路线 |
| 首周期相同，深后缀已见超额；浅后缀尚未知 | 选浅路线；这是现役政策观察，不证明深后缀模拟错误 |

比较前的强制用药约束、C 代表生成与覆盖、终止池截断没有在本探针中执行。不能把上述合成结果说成玩家本局复现；局面有多常见仍需真实根样本。

复跑时将两份 `.txt` 复制为 `.local/mp-coop-strategy-20260922/ordering-probe/Program.cs` 与 `OrderingProbe.csproj`，使用已配置的 `local.props` 和当前已构建 DLL：

```bash
.local/dotnet/dotnet run --project .local/mp-coop-strategy-20260922/ordering-probe/OrderingProbe.csproj -c Release
```

项目只构建探针，不重建产品。其 friend assembly 名称与既有离线宿主相同，不能从这个设置推导产品公开 API。

构建有一条 `CS8625` 警告：合成节点为本比较器不读取的字段传入空值；程序成功结束，10 项断言通过。归档 JSON 去掉该条编译输出前缀，原始 stdout 留在 `.local/mp-coop-strategy-20260922/ordering-probe-results.json`。没有为消除探针警告而更改输入或重复运行。

## 贡献配额的精确小例子

[`quota-frontier-probe.py`](quota-frontier-probe.py) 使用标准库，输出 [`quota-frontier-results.json`](quota-frontier-results.json)。两项为单回合卡牌排列的完整枚举；另外两项只是有限集合数学例子。无随机性、真人分布、性能对照或生产引擎。

- 本机 6 HP、敌人 20 HP、来伤 8、两能量、一张 10 伤害和一张 5 格挡：要求打 10 时，保留真实敌人应打攻防两张，剩 3 HP。把敌人 HP 改成 10 会只打攻击并错误地认为无损胜利；真实原局同样动作在敌方阶段死亡。
- 一能量在 8 直接伤害和全目标 1.5 倍增伤中选一；队友随后两次各打 10：直接攻击使团队输出 28，增伤使团队输出 30。只要求自己打够 8 会排除增伤；队友已结束时则攻击 8 优于增伤 0。
- 对相同有限候选集合，一次得到伤害/损失 Pareto 前沿即可查询多个配额；这不证明前沿已经穷尽、续行等价或生产去重安全。
- 构造一个在阈值 50 返回 Unknown 的不完备 oracle，将其误当不可行的二分会返回 49，即使存在 90 的见证。例子只反驳该判断规则，没有模拟真实 Beam 出现该缺口的频率。

```bash
python3 docs/strategy/multiplayer-cooperative-planning-20260922/local-probes/quota-frontier-probe.py
```

上述来源和输入不变时无需重复执行；本次没有实机、原生差分、全角色或联机胜率验收。

## 重算不断推迟目标的反例

[`deadline-probe.py`](deadline-probe.py) 对一个有限行动表完整枚举：攻击造成 10 点有效伤害、付 3 HP；防守零伤害、零损失。目标是在三个周期内打 20，最小总损失相同则先选眼前少损失。每次重算都把期限恢复为未来三个周期，会反复计划“防、攻、攻”而只执行“防”；三次执行总伤害为 0。冻结同一个阶段截止并扣除实际进度，则执行“防、攻、攻”，总伤害 20。

[`deadline-results.json`](deadline-results.json) 为本轮一次运行结果。它只证明滚动配额存在拖延反例，不声称所有 MPC 都有此问题，也不是游戏引擎测试。后续方案可以通过阶段截止、进度条件或合理末端价值解决，但须独立验证。
