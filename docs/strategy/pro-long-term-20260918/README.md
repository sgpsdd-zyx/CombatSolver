# 多人长线收益复审与首批诊断归档

日期：2026-09-18。用户要求继续 Fork 任务，先让 ChatGPT 6 Pro 设计能力牌、黑暗球和星能的长线实验，再执行并归档。本目录保留 6 Pro 的原始交付物和本地执行边界；原始 Markdown、Python、JSON 与 C# 探针不改写。

## 固定输入与研究结论

- 行为基线为 `d55fa84`，游戏托管模型为 `0.111.0`；官方单人 `0e6cc2d / 0.41.0` 继续隔离。
- 6 Pro 交付源码复审、策略设计、抽象实验脚本/结果和未编译生产探针五份材料。抽象实验已由原脚本重跑：17 个 fixture、3 个变体、51 次运行、13 项检查，另有 12 次 Beam 8 控制运行；报告的 F10 席位替换退化仍阻止默认 C。
- 采用首批设计的低成本边界：新增有界多人路径观察合同，消费已有 `SearchPathObserver` 快照；不改多人评分、Beam 席位、最终排序、状态键、预算或生产策略开关。
- 未把 Pro 的生产探针复制进运行代码；它仍是未编译、未运行的参考材料。

## 本地执行

新增 `OfflineSearchHarness --multiplayer-long-term-contracts`，固定双玩家、本机索引 1、Ironclad、`FUZZY_WURM_CRAWLER_WEAK`，加入 `Strike`、`Defend`、`Inflame`、真实 Dark 球和 3 点星能，使用 Beam 8、120 节点、500ms、DOP 1。合同只记录路径在已有边界上的出现/消失，并在 RetentionPool 诊断副本中记录已有星能、持续效果、战略保留、延迟伤害和未来资源字段；它明确报告 `ranking_unchanged=true`、`state_key_unchanged=true`，搜索前后 `ContinuationStamp` 相等。

最终生产合同结果：1,383 个观察事件、12 个阶段、91 个展开节点、202 次转移；51 条路线在 `PruneInput -> PruneFinal` 消失，51 条在对应外层保路池消失。231 个快照携带评估值，星能最大值 3，持续效果非零 231 个，战略保留非零 171 个；本夹具的未来资源与延迟伤害为零，因此不能据此证明所有长线消费者已经覆盖。结构化结果见 [production-long-term-path-diagnostics.json](production-long-term-path-diagnostics.json)，最终现场输出保留在 `.local/mp-longterm-production-final2/`。

同一宿主的既有 `--multiplayer-strategy-contracts` 通过 9 个扣血/能力隔离案例；官方单人五卡能力哨兵通过 Coordinator / Beam 12 / 350 节点 / 12 秒 / DOP 1，1,820 总展开、4,583 次转移、2 HP 投影战损，与独立官方基线保持一致。单人哨兵现场为 `.local/mp-longterm-solo/`，多人合同现场为 `.local/mp-longterm-production-strategy/`。

第一次尝试用 80 节点启动被既有设置校验拒绝（最小值 100），记录在 `.local/mp-longterm-production-r1/`；没有把参数失败计为行为失败。随后 120 节点运行通过。主项目与离线宿主 Release 构建均为 0 警告、0 错误。

## 状态矩阵

| 事实面 | 状态 | 当前权威入口 |
|---|---|---|
| 研究材料 | `changed-and-verified` | 本目录五份原始 Pro 文件与本 README |
| 诊断代码 | `changed-and-verified` | `tools/OfflineSearchHarness/MultiplayerLongTermContracts.cs` 与 `src/Search/CombatBeamSolver.PathDiagnostics.cs` |
| 默认多人策略 | `verified-current` | 保持现役三通道 Beam；没有启用 C 或新的长线评分 |
| 单人路径 | `verified-current` | 官方 `0e6cc2d / 0.41.0` 哨兵与入口隔离 |
| 原生黑暗/星能差分 | `pending` | 当前只证明真实根与搜索快照进入诊断；完整事件/花费/激发差分仍需专门 fixture |
| 真实联机、可见 Steam、普遍胜率/性能 | `pending` | 本批明确未执行；无头结果不外推 |
| 生成记忆与工作区清场 | `out-of-scope` / `pending` | 未改平台记忆；`.local` 现场保留，未删除 |

这批结果证明了诊断入口和现有快照事实可用，不证明能力牌、Dark 储值或储君星能在所有真实路线中都被正确保留。下一批若要改变策略，必须先以同根生产失败 fixture 通过 F10 不可退化门禁，并单独验证合法消费者；研究结果本身不扩大默认策略或发布范围。
