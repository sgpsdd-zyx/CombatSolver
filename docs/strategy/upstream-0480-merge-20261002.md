# 官方 0.48.0 合并与 fork 0.48.1

[策略索引](README.md) · [逐项证据](upstream-0480-merge-20261002-evidence.json) · [中英玩家说明](../releases/0.48.1-RELEASE_NOTES.md) · [发布凭证](../releases/0.48.1-PUBLISH.md)

2026-10-02，按用户「继续合并官方的最新版本，并发布release，按照 neat-freak 归档」请求，在 `codex/multiplayer-advisor` 从 `154e6283` 合入官方 `a789aad2613683438803b11ecddd32aa083e1752 / 0.48.0`。上一个官方基线为 `72363308 / 0.47.2`，上一个公开 fork 为0.47.3，来源 `06018378`。本次只取一次官方最新分支证据；上游提交的中国时区日期可能为10月3日，本记录使用本机10月2日。

## 合并结果

- 单人保留官方0.48.0的原路径、参数和行为：已知回血来源、剩余治疗证书、跨成员胜利界、智能药水判断、前两回合探索与调整路线的目标合法性。没有继续开发上游多人规划或社区待办。
- `CombatRootSnapshot` 的多人根不取得无治疗、剩余治疗及已知原版来源三类资格，初始剩余治疗上界为 `int.MaxValue`。零额度遗物生命界、主结果剪枝及 `SmartPotionBound` 明确排除多人。原有能力／计划、成长、追加探索与开发脚本隔离保留。
- 多人仍按三周期本机贡献、无来源伤害折算和条件长线续行选路，最多十四敌方周期；普通时间／节点预算乘二，固定预算不变。手动请求、原生选择和队友行动后的手动重算边界保持，不预测队友主动行动。
- 费用持续时间与顺序、克隆身份诊断、充能球数值、临时力量／敏捷／集中退役顺序及召唤目标名称属于共享语义。回合末采用官方逐监听器时序，同时保留多人持有者、存活 Hook 和参与者过滤。根捕获的 `LiveCollectionGuard` 保留明确失败，`DynamicVarSetAccess` 独占内部字典读取。
- 继承日志保留开关、跑局统计故障隔离及主动内存释放。macOS 无头入口同步原官方 Windows/Bash 的四项静音设置；没有改变玩家档案。
- 官方0.47.3与已公开 fork0.47.3同号：fork原文保留，官方原文独立存放于[上游日志](../releases/upstream/0.47.3-RELEASE_NOTES.md)。官方0.48.0原文保留，fork用0.48.1；所有旧标签不移动。

## 本轮验证

环境为 macOS arm64、.NET SDK9.0.318、游戏0.111.0、RitsuLib0.6.5。原生请求总超时均不超过120秒，一个进程复用一批请求，实例位于本仓库 `.local/headless-instances/`。没有启动可见Steam。

| 范围 | 实际结果与限度 |
| --- | --- |
| 根与费用身份 | Passed：主线程捕获／worker拒绝、集合写入具名失败、多人治疗资格隔离；11项费用身份比较包含原生费用清理、星能、Fork、选择与续用。 |
| 选择与Hook | Passed：1200次重复选择比较、5069项选择输出；62种Hook／1671模型的过滤、顺序、Fork及布局合同。 |
| 克隆身份诊断 | Passed：根、写时复制、转换、牌堆迁移、六轮roll-out身份归属与逐位置续用差异。该上游诊断刻意证明顺序变化时旧序号匹配会选中另一实例，不代表所有历史同名牌续用问题已修复。 |
| 临时集中与充能球 | 正序、逆序、无临时集中哨兵均Passed；逐阶段原生回合末结算、球队列、敌人HP、RNG、Fork一致。球数值和保路值均为6，预测未进入原生可变Hook。三个变体属于同一根因，不算三场独立质量样本。 |
| 召唤与访问桥 | 地精死亡召唤名称区分、单人怪物不调用多人缩放Hook、动态变量桥均Passed。 |
| 单人治疗与药水 | 已知来源政策Passed，包含已有／未生成回血、保命、遗物、严格较差HP剪枝及等HP跨回合保留；DOP2成员继承与增量一致Passed。Smart重新取得无药基线，仍严格核对药水成本。 |
| 路线目标与死亡 | 原生库存替身、两次实际攻击、直接／增量回放、朱红斗篷死亡前完整状态Passed；调整路线无效后缀及固定前缀合法／缺失目标合同Passed。缺少匹配原包，未声称B015/T016现场回放。 |
| 统计 | 原生队列饱和及worker失败隔离Passed；独立存储合同覆盖连续记录、缺口、弃局、去重、持久化、历史分离与不完整标记恢复。 |
| 多人操作 | 烘焙手套原生暂停中手动计算、选择后全队完整状态Passed；DOP2停止排空、队友防御后建议过期、异选仍归玩家、手动新根Passed。随后同进程单人恢复Passed。 |
| 多人托管合同 | 32项兼容、32项贡献Passed；真实队友死亡／复活、原生回合、Fork及手动重算Passed。托管宿主旁路渲染和网络，不代替真人联机。 |
| 单人官方对照 | 独立构建官方源码与fork，两根各DOP1/DOP2，共4对／344项非时序字段一致，无时间边界。能力根1HP／0药／4回合；药水根4HP／1药／2回合。比较包含路线、根／续用及工作计数，不是跨DOP或可见性能结论。 |
| 提前探索与UI | 提前探索停止边界143项Passed；英文／简中／繁中486项模板及存活控件、选牌、归属、设置结构Passed。未核验可见布局。 |
| 静态 | Release与宿主构建通过；Bash结构门禁258个Search文件通过；CoverageCatalog3035项分类、状态字段和分支读取指定检查通过。生成目录写入隔离证据树，没有重写旧生成结果。 |

24个最终原生请求通过。完整请求、阶段、耗时及断言见[结构化证据](upstream-0480-merge-20261002-evidence.json)，原始材料保存在 `.local/upstream-update-20261002/`。通过后仅调整测试预热入口的触发时点，补跑两个最小请求证明首次请求和进程复用；没有连带重跑生产行为或官方对照。

## 失败与修正

新建克隆身份fixture最初把同名原牌与副本放在手牌，测试所需的「从原牌堆移动到手牌」没有发生，因此失败。改为清空原牌组／牌堆，并将两张牌放入抽牌堆，保留明确不同的牌组归属。没有更改生产匹配逻辑或放宽断言。

第二次身份断言全部完成，但返回菜单写原生回放时，`MaxEnumValueCache` 的非并发字典报告已损坏。现有多人实验已有该缓存预热，移到 `ProtocolHost` 首次接受无人请求、建局之前，一次物化游戏枚举，避免回放和报告线程并发首次写入。普通游戏没有请求时不运行预热，测试不改包内容或战斗状态。修正后身份场景和后续批次通过；最终入口再以两个最小请求验证。相关所有权、两端结构门禁和skill同步。

主动内存释放工具要求先建立实际1GiB No-GC区域，本机在该前置条件失败。此次未验证区域释放后驻留与重入效果，没有降低预算、提高超时或取消断言；上游Windows结果不算本机通过。

四个自有实例均有启动器成功删除凭证：`macos.4LjObz`、`macos.hre0cR`、`macos.kTypzk`、`macos.V0imyj`。日志和失败请求继续保留。Windows/Linux运行、Windows MemoryCleaner、完整第三方栈、原问题包回放、真实网络、可见性能和完整40分钟探索均未执行；本轮不是完整发布门禁或干净安装。

## 知识收尾

采用 `neat-freak` 完整路径，复用已有Git阶段凭证进行等价只读盘点，避免违反仓库「同来源不重复安心检查」规则。初始工作树干净，只有当前主工作树；机械盘点2688项非构建文件、468篇Markdown，无规则软链，唯一项目规则为根 `AGENTS.md`。没有另建工作树或操作其他聊天。

| 事实面 | 状态 | 权威与收尾 |
| --- | --- | --- |
| 源码与配置 | changed-and-verified | 冲突按单人官方／多人隔离解决，版本0.48.1，原生／托管／结构证据见本文。 |
| 当前文档与规则 | changed-and-verified | README中英文、多人指南、架构、外部适配手册、两端结构门禁与相关skill同步。过期英文0.47.2入口纠正，规则顶部移除重复发布流水。 |
| 上游历史 | changed-and-verified | 新官方开发／测试结果标为上游历史；官方多人规划增加醒目参考边界，不覆盖本fork现役手动行为。社区自动认领工作流已有官方仓库身份门，未执行社区外部操作。 |
| Agent记忆 | out-of-scope / generated-read-only | 本机存在宿主生成的Codex记忆；无可写纠正入口授权，未直接编辑。项目现役事实归文档与AGENTS。其他平台／其他项目不在本轮范围。 |
| 部署与渠道 | changed-and-verified | 来源 `d543bea8` 正式构建、四文件部署、2,464,773字节最小ZIP、annotated tag与GitHub最新正式版均完成；直接凭证见[发布记录](../releases/0.48.1-PUBLISH.md)，没有发布后重复核对。 |
| 工作区残留 | verified-current | 构建、发布和原始证据均忽略；四个临时游戏实例已按启动器合同清理。独立官方源码、失败证据和旧发行包保留用于复核。 |

上游社区实验补丁中的空白上下文按原文保留，Git空白检查的三条提示来自该归档补丁；不把它改成不可应用的历史差异。生产源码末尾多余空行已移除，未改变行为。

规则文件当前29,770字节／241行，本机未配置 `project_doc_max_bytes`；仍有体量偏大的维护提醒。没有为压缩而移除本轮用户明确提供的硬约束。手册和架构保留唯一机制入口，新增材料经目录链接检索。

复核现场仍保留，等待用户确认后清场。候选包括本轮独立官方源码快照与隔离Coverage输出，以及 `.local/` 下旧批次证据；均只列候选、不删除。发布ZIP属于交付产物，私有连接配置是后续构建输入，不纳入清理。未创建临时分支、PR、外部服务或数据库。

## 重跑入口

```bash
tools/run-unattended-test-macos.sh \
  coverage/unattended/heal-bound-safe-root.json \
  coverage/unattended/upstream-0480-card-cost.json \
  coverage/unattended/upstream-0480-turn-end-focus.json \
  coverage/unattended/upstream-0480-turn-end-focus-reverse.json \
  coverage/unattended/upstream-0480-turn-end-focus-sentinel.json \
  coverage/unattended/upstream-0480-orb-value.json \
  coverage/unattended/known-healing-policy.json \
  coverage/unattended/known-healing-members.json \
  coverage/unattended/smart-audit-potion-baseline.json \
  coverage/unattended/multiplayer-toasty-choice.json \
  coverage/unattended/upstream-0472-multiplayer-toasty-controls-dop2.json \
  --timeout-seconds 120 --cleanup-instance-on-exit
```

全部24个请求的精确输入由结构化证据给出；不要求每次维护重跑整批。正式构建、部署、最小包与发布均已完成，见[渠道凭证](../releases/0.48.1-PUBLISH.md)。可见游戏和真实联机验收为out-of-scope，复核材料保留不影响此次发布完成。
