# 巨斧机器人报告排查（2026-10-03）

用户要求从后台取包并修复重算或计算失败。主仓库为 `CombatSolver`，开工分支 `main`，工作区干净；基线源码 `7df1f0480a49723a89962fef765664f5d984f942`，版本 0.48.0。后台 monster 稳定 ID 为 `AXEBOT`，查询得到29份未解决报告。两份最新静默猎手 SearchFailure 属于同一 session，只下载一份；本次固定六份代表。包内环境可证明 Mod 版本，未提供可比源码提交时不根据文件名推断源码身份。

| 报告 ID | 包内版本 | 首个证据与处理 |
|---|---|---|
| `cffe550de0534dfb9c8248cee7398615` | 0.47.3 | T3 续用戳 `mirrorRelics` 金纸预测2、实机3。音乐盒在异界嚎叫的回合末自动出牌中新增虚无复制牌，旧手牌预统计漏算。已用同机制最小原生差分验证修复。 |
| `a3418d28e4ad4493afa5c1cd012b85fb` | 0.47.3 | T7 `C[5]` 预测打击、实机燃料+。压缩+整批变牌被逐张插入模拟，污染有序牌堆并在后续洗牌/抽牌导致重算。已用间隔两张状态牌的原生差分验证。 |
| `b6869f7eae154465b39fbdeee53299d5` | 0.47.3 | T2 消耗堆 FOLLY 预测残留临时星能0层、实机已清除。回合末清理资格仅查能量费用与临时标记，漏掉只有星能修改层的牌。已原生差分验证。 |
| `f71b6897fa204801a7a7b4a820fe00ce` | 0.47.3 | T4 小刀目标 CombatId2，`CardPlay has no target creature`。与主线已合并 B015 T016 的重生目标路径一致，本轮未重复同源码既有合同，也未回放此原包；不把日志机制归并当作原包已验证。 |
| `d9c106140d77420bb34815d1ae6471c1` | 0.48.0 | T6 倾泻计划 TrueGrit 消耗 BASH+1，原生页面唯一候选 STRIKE_IRONCLAD。后续最小场景复现同一异常，修复内层自动牌提前触发无尽陀螺的效果边界，原生严格差分通过。原包整场未回放。 |
| `5cbc6bb5c9a845b39c10dd6005e21064` | 0.47.3 | TurnSetup 的分支 Fork 抛 OutOfMemoryException，失败候选已到T10/127动作。日志证明内存耗尽，分配/容量首因未复现，未修改预算、GC或异常处理。 |

## 最终逻辑

- 金纸的已计数与延迟虚无消耗分别由 `JossPaperPredictionState` 保存。主线程从原版根读取 `_etherealCount`，Fork按值复制；延迟计数进入指纹和续用文本。`AfterCardExhausted` 逐次记账，`AfterSideTurnEnd` 消费所属分支的延迟计数；回合末新增或被其他效果消耗的牌以实际事件为准。
- 压缩使用 `TransformCardBatch`：生成替换牌，顺序移除原牌并记录当时缩短牌堆中的索引，然后按原版牌堆/索引排序插入。原力、SEANCE、Charge等原版逐张调用继续使用 `TransformCards`；共同复用单张移除和生成尾部。首次扩大共享入口后，原力哨兵在H[1]出现偏差，最终据原版调用方式缩回专用整批入口。
- `NeedsEndOfTurnCleanup` 纳入 `ClearsWhenTurnEnds` 的临时星能层，仍使用原版 `CardModel.EndOfTurnCleanup` 清理完整状态。没有修改显示比较或吞掉续用差异。

## 本轮证据

| 夹具 | 失败基线 | 最终 |
|---|---|---|
| 金纸/音乐盒回合末生成牌 | `94ec8b7610e34da1b963faae75a41186`，实际1/模拟0 | `26b7755643d241f091c62f57c9f75a3a` Passed |
| 压缩+两张间隔状态牌 | `3195ac535ce0499d8f4e02b24707124e`，Hand[1] 打击/燃料+ | `b45e21f166d042f1ae59079d001e375d` Passed |
| 子弹时间/FOLLY清理 | `02192ab7e98d410291b5d7f6b7dea043`，消耗堆残留星能层 | `896d94be19b9473e988aa997fc30c2c7` Passed |
| 原力/SEANCE哨兵 | 原力初稿 `2224c8a468a44f90bb98e79247f7af63` 失败 | `8512e36aecd74d93a15f3adb34502190` 两项Passed |
| 金纸延迟计数根/Fork | 首次 `e3faadd893c74974af6060628bbf13ec` 为测试把根捕获放在隔离域内，触发Power显示变量保护 | `e7c0363924e74211b043ebfaca50ac0c` Passed |

前五项为原生动作完整状态/RNG严格差分，Fork合同验证根冻结、live推进后独立、父子/兄弟、指纹/续用文本和每分支消费一次；不把后者称为原生整场对照。纯差分不运行搜索。Release构建零警告/错误，结构门禁 `REFACTOR_BOUNDARIES_OK search_files=246`。

CoverageCatalog 验证未跑通：当前 RitsuLib 改为 compat/shared 分拆包，临时编译引用补齐后，既有 `test-evidence.json` 的两种扩展status未被工具枚举接受；临时解析补齐后又在 `InfusedCore` 重复原版模型构造处崩溃。相关临时工具修改和中途生成目录已撤回，本轮不声明覆盖目录验证通过。

## 倾泻材料边界

以下三次失败属于首轮复现工具障碍。首轮没有继续缩小夹具，倾泻修复因此遗漏；后续修复证据见下一节。

原生录制路径在第26个输入停滞，runId `750de6555b7841528e7f0887f6fea6c1`，未恢复到T6。旧中途导入首先缺当前重生个体ID2（`39d53d5b581040c9b5ecf33ddecc6084`）；临时测试对齐该ID后仍缺历史Power施加者ID1（`d7cb88a79ade42fe82c66b3c2de8a627`）。临时身份测试入口已撤回，没有把历史施加者改指当前怪物。按包内牌堆/Power重建的场景 `f7abdd59eaf14d1596ffcdd3f0c866c8` 达到120秒上限，没有继续扩展时间帽，未取得该错误的实际/模拟对照。这些是恢复/夹具失败，不是修复基线或修复成功。

本轮没有整场部署、可见Steam或性能结论，不声称全部巨斧机器人报告已修复。后台凭据的API scope是read，本次没有回写报告状态。无头实例已清理，源码保留可复跑的最小夹具；验证摘要保存在忽略目录 `.local/verification/axebot-20261003/summary.json`。临时问题包的整目录清理和逐文件清理都被自动审批拒绝，工具仅返回 `blocked by policy`，未给出具体原因；包和临时诊断仍保留在 `.local/issue-bundles/axebot-20261003`。0.48.0已发布冻结，本轮记入下一版本开发记录，未提升版本或发布渠道。

源码修复提交为 `21a5abdf`；复用与最终行为源码一致且已通过测试的Release构建，将manifest、CombatSolver.dll、MemoryCleaner、LICENSE和THIRD_PARTY_NOTICES.md五个文件覆盖到已确认的本地游戏 `mods/CombatSolver`。复制成功，未重复构建、检查版本或启动游戏。

## 倾泻后续修复

按报告第6回合头槌结束后的牌堆顺序及 Shuffle 的计数器/四段状态建立 `CASCADE-EMPTY-HAND-NATIVE`，只有倾泻+在手、抽牌堆一张闪电霹雳、弃牌堆17张和无尽陀螺。使用普通遭遇隔离手空、洗牌及嵌套坚毅，未恢复原包的历史、巨斧机器人个体及整场路线。首轮最小基线 `cc37414e00274b6baaf0d877a60e3ac9` 的完整状态对照显示模拟多抽一张全身撞击；改为优先消耗痛击的失败基线 `e3bac4a083574686b1e9d018ccc23f80` 精确复现报告中的原生页面失配。

原版 `CombatManager.CheckForEmptyHand` 要求该玩家的卡牌/药水效果深度为0。倾泻的外层效果覆盖内层闪电霹雳、耸肩无视、重振精神、坚毅和剑柄打击；旧模拟每张内层牌收尾都检查手空，提前由无尽陀螺抽到痛击。修复后在最外层效果结束后检查，坚毅候选与原生一致。引擎拥有临时效果身份栈，普通 Fork 要求为空；执行检查点逐帧按值冻结身份栈，恢复独占列表。来源牌结果移动与费用清理继续在原顺序完成。没有改选牌匹配、吞掉异常或放宽状态比较。

目标修复证据 `53ba69afa54544f3a1322b42367d5e90` Passed：原生嵌套选择完成，完整 continuation/RNG一致，完整回放/检查点续执行/完成后 Fork一致，预测不改变live。两条目标请求均为120秒上限、Instant、动作间隔0；测试后实例由启动器删除。日志保存在 `.local/verification/cascade-bash-baseline.log` 与 `cascade-empty-hand-fixed.log`。本次不声明原问题包整场重放、全场零重算或可见游戏通过。

相邻合同 `260511f79ef34d9393ef2d5f6b07f672` Passed（37.4秒），涵盖三项手空边界、五种嵌套卡牌执行、手动自身选牌及九种药水的原生完整状态/RNG与Fork/续执行验证，另有DOP2、兄弟隔离和取消/异常再访问。完整日志为 `.local/verification/effect-scope-adjacent.log`；实例已由启动器删除。最终行为源码的Release构建和结构门禁通过，覆盖目录沿用首轮已记录的外部限制，本次未重复执行失败工具。

复用最终行为源码对应的成功Release构建，将manifest、DLL、Windows MemoryCleaner及两份许可共五文件覆盖到已确认的 `D:\Steam\steamapps\common\Slay the Spire 2\mods\CombatSolver`，复制成功。未提升版本、发包、推送或启动可见游戏。旧问题包按先前自动审批拒绝的边界继续保留，本次无头实例均已清理。
