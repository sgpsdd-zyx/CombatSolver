# 官方 0.43.2 合并记录

日期：2026-09-20（America/Los_Angeles）。用户要求检查并尝试合入 fork 原仓库的新更新。本轮从 `7b52ac8c / fork 0.43.4` 合并官方 `3f4002bd / 0.43.2`，共同基线为 `cccc270 / 0.43.0`，包含官方此后的 27 个提交。远端为 `Torch1230/CombatSolver`。

当前行为见[多人指南](../multiplayer-advisor.md)，面向玩家的变化见[fork 0.43.5](../releases/0.43.5-RELEASE_NOTES.md)。本轮只准备本地补丁和提交；已知 GitHub 发布版仍为 fork 0.43.1，不创建新标签或上传渠道。官方新标签抓取到 `refs/upstream-tags/*`，已有 fork 标签不移动。

## 合并取舍

| 来源 | 当前接入与边界 |
|---|---|
| 官方 0.43.1 | 英文面板 Collapse 键；疯狂科学能力／改进变体的独立成长额度、根目标和可升级容量。多人不采用单人成长目标或战损信用，实际改进 Power 仍正常结算 |
| 官方 0.43.2 用药修复 | 强制药与额外 Smart 药按边际收益比较，原版单人路径完整保留；多人协调器仍在此路径之前返回 |
| #105 与上游集成修正 | 根预审已登记的生成牌、冻结已补丁 OnPlay 方法集合，worker 不读取 Harmony 活表；未知组合显式拒绝 |
| #117 与上游集成修正 | 缓存／录像仅捕获文件访问、权限和已定义格式故障；取消、程序错误和模拟错误继续传播；录像排在交付路线之后 |
| #118 | 单人六计数随历史事件增量维护，三类 Fork 按值继承。多人保持原完整历史扫描及各效果原有持有者范围 |
| #119 | 合入转置触顶诊断、检查与研究，默认百万条上限及候选规则不变 |
| fork 自有行为 | H14、普通请求双倍额度、每周期 3 HP 账本、佩尔之眼归属、完整回合覆盖 C／A 回退、单人能力隔离保持 |
| 文档冲突 | fork 与官方的 0.43.1／0.43.2 含义不同；fork 历史文件保留，官方原文另存 `docs/releases/upstream/` 并独立索引。导入的上游旧测试注明来源，不写成本轮通过 |

## 两处真实兼容失败

1. 自动合并后，普通多人策略请求在 `GrowthOpportunityPolicy.Capture` 调用新增的单人成长容量函数时抛出 `Sequence contains more than one element`。Runtime 多人捕获现在直接使用空成长目标，影子根多人容量固定为零，单人继续官方原函数。不能仅等到后续 `IgnoreLongTermRewards` 才过滤，因为根捕获会先失败。
2. 处理上一项后，多人历史读者牌的生产构键入口又抛出 `History counters require the captured single-player owner`。上游累计容器只有一个单人 owner；多人继续用 `CombatHistoryCounters.Scan`，不伪造 owner、不借用列表第一位玩家、也不引入新的多人累计缓存。

这两项位于 Runtime 输入捕获、Search 影子根和历史键的显式多人分派；没有修改 Engine 的单人累计语义。两端结构门禁均增加对应约束。新增 `MultiplayerUpstreamContracts` 使用双玩家、本机索引 1，验证两位玩家的独立历史范围、真实生产状态键、金斧输出、父子／根隔离，以及疯狂科学的模拟和原生全队状态。

## 本轮证据

现场根为 `.local/upstream-merge-20260920/`，输入记录在 `inputs.json`。以下是当前行为源码上的直接证据；行为构建版本仍为 0.43.4，随后只同步版本、文档和最终发布构建。

| 检查 | 结果与产物 |
|---|---|
| 多人兼容 | `mp-compatibility-clock/upstream-compatibility.json`：13 项通过。两位玩家历史计数分别为 `(2,0,0,1,1,1)` 与 `(2,1,0,1,1,1)`；金斧增加 2；改进只施加本机、成长信用为零；原生与预测完整全队 `ContinuationStamp` 相同 |
| 官方单人对照 | 独立导出／编译 `3f4002bd`。同一 `solo-power-compat.json`、Coordinator／Beam12／350 节点每成员／12000ms／DOP1；80 项及完整 route JSON 相同，1820 展开／4581 转移／2 HP；四个多人入口零进入。`solo-comparison.json` |
| 缓存故障 | 同一次单人求解后注入坏 JSON、占用、只读目录、目标占用等 18 项，结果序列化不变，取消和程序错误向上传播。`solo-merged/ancillary-checks/checks.json` |
| 多人风险与资格 | `mp-strategy/`：九项额度／避死／治疗／原生下一回合／增量／Fork／能力隔离，既有 840 资格排列、216 三元关系与同池预览／最终一致通过；`mp-facts/review-facts.json`：10 项真实终局和本机用药范围通过 |
| 长线完整回放 | `mp-window-incremental/window-selection.json`：52 展开／83 转移，逐转移完整重放及真实预览通过，三个代表完整覆盖，比较周期 4→5 |
| 缺代表哨兵 | `mp-window-sentinel-traced/window-selection.json`：A/C 均 180／301，同池同完整路线，仍比较周期 1，`coverage_not_deeper` |
| 带来伤哨兵 | `mp-window-defense/window-selection.json`：A/C 均 180／364，比较周期 6→7，首动作、已知 3 HP 损失／超额零不变；统一外评第十四周期均损失 37 HP，不是十四周期安全证明 |
| 历史累计生命周期 | `build-history-verification.log` 使用 `VerifyHistoryCounters=true`，0 警告／错误；`history-verified/history-checks.json` 21 项通过，包含三类 Fork、未完成事件／恢复和独立扫描对账。计时不外推整搜性能 |
| 转置与 OnPlay | `frontier-checks.log` 1,024,013 项；`adapted-onplay.log` 40 项与 `adapted-empty.log` 2 项通过 |
| 原生无头 UI | `native/01-result.json`，`macos-b6faaf6ad08349f880873587f605addb` Passed：eng/zhs/zht 各 449 文案、真实策略面板构造／收起、遗物持有者与 JSON／切换语言 |
| 原生无头用药 | `native/02-result.json`，`macos-67e1bb55d1ed4e019ad805e0176e0979` Passed：强制药基准、额外 Smart、保留药和救援，以及原战损／成长早停 |
| 原生无头成长 | `native/03-result.json`，`macos-098e27737e56478f8a4e0b7759754544` Passed：疯狂科学容量 3、改进／非改进、独立额度、Fork 与原生状态对照 |

## 未完成与中间失败

- `ADAPTED-ONPLAY-INTEGRATION-CARD` 没有返回协议结果：同进程第 4 项和两个独立尝试均未完成，不能记为原生适配通过。`native-adapted-protocol/` 保存一次进程采样，尚未定位具体原因；补写显式 schemaVersion 也未改变结果，因此不能归因为协议缺省。独立 OnPlay 40+2 项通过只能覆盖其声明范围。没有延长 120 秒超时。
- 多人新夹具最初未配对完成抽牌／生成历史，Fork 正确拒绝；随后原生手动出牌读取 Godot 时钟使普通 .NET 宿主退出。配齐 Resolved、沿用既有原生合同的局部时钟替身后，13 项全部完成。时钟替身仅影响动作时间戳，不替换效果结算；这两类是夹具错误，不当作产品失败或普遍原生支持。
- 首次缺代表哨兵在多任务并行负载下撞到每层 250ms 时间边界：日志记 `elapsed_ms=323`，首层只展开一个节点，候选池已改变，不能作固定节点对照。补充失败现场输出后重跑通过，未加任何时间／节点预算或改排序。固定预算包含时间边界，不能把所有退出都当作节点确定性。
- 当前成功行为构建 `build-compatibility.log` 和宿主 `build-upstream-contract-clock.log` 均 0 警告／错误；早期一次 `_players.Length` 编译失败已改为原集合接口 `Count`。
- 原生测试启动器已报告删除全部三处隔离实例 `macos.hS7XM2`、`macos.3qVxNd`、`macos.YFYECn`；未保留游戏快照。原生退出日志仍有渲染资源清理告警，不据此声称可见界面或性能验收。
- 未启动可见 Steam，未测试真实网络、三／四人、全角色全遭遇或整个适配 Mod 栈；未执行完整发布门禁、干净安装或外部发布。

## 知识收尾

完整盘点记录在 `inventory-before-closeout.txt`：368 份 Markdown、一个工作树；当前 Codex 规则真身为根 `AGENTS.md`，全局同名规则为空，无项目 override 或 CLAUDE 副本。全局 Claude 文件属其他平台，仅列存在。合并涉及的当前入口、架构、测试矩阵、技能、玩家说明和索引同步到官方新基线；历史研究和已发布 fork 日志保留原语境。

| 事实面 | 状态 |
|---|---|
| 代码 | `changed-and-verified`：上述针对性检查通过；第三方原生集成项单列 `pending` |
| 运行态 | `pending`：只准备本地版本；真实网络／可见 Steam 未验收 |
| 文档／规则 | `changed-and-verified`：以多人指南为现役机制，官方日志独立归档；本轮测试与上游历史证据分开 |
| 记忆 | `out-of-scope / generated-read-only`：未修改全局设置或生成记忆 |
| 工作区 | `verified-current`：当前分支完成合并；官方参考导出、构建、日志及发布包在忽略目录，保留诊断现场，不清除原有资料 |

结构门禁为 `REFACTOR_BOUNDARIES_OK search_files=212`；文档检查覆盖 23 份变更 Markdown、692 个本地链接和 57 个锚点，JSON 与 0.43.5 版本对应通过。三个变更 skill 的官方快速校验通过，复用本仓库已安装的验证依赖。凭证为 `boundaries.log`、`doc-validation.json`、`skill-validation.json`；PowerShell 门禁未执行。根规则仍超过默认 32KiB 预算的 70%，因此只维护必要约束，实验流水账归本文。

最终提交后的普通 Release 构建与最小 ZIP 来源保存为 `package-receipt.json`；不使用带历史断言的 DLL 发包，不重复已通过的行为场景，也不在 ZIP 成功后重开核对。
