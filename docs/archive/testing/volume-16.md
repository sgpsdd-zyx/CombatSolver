# CombatSolver 测试入口历史卷 16

[返回归档索引](README.md)

## 额外回合镜像顺序（2026-10-05）

`EXTRA-TURN-MIRROR-ORDER` Passed，runId `36f686e1cf2b442e836d1ab4e5bdda2d`，23.655秒。合成第三方监听者的三个位置均通过原生短路顺序、后置状态读取、完整状态、Fork 与 live 隔离及登记冻结检查；直接证据见 [登记表](../../../coverage/evidence/test-evidence.json)。原生回合推进哨兵 `PAELS-EYE-AUTOPOST-ORDER` Passed，runId `f1d9c6342c4b4e91aaf15bf1af4e4a76`，24.863秒，完整状态与 Fork 一致。实例已删除。

PowerShell：`tools/testing/run-unattended-test.ps1 -ScenarioId EXTRA-TURN-MIRROR-ORDER -EnemyCurrentHp 1000 -TimeoutSeconds 120 -CleanupInstanceOnExit`；Bash：`tools/testing/run-unattended-test.sh --scenario-id EXTRA-TURN-MIRROR-ORDER --enemy-current-hp 1000 --timeout-seconds 120 --cleanup-instance-on-exit`。帕尔之眼哨兵替换同名 ScenarioId。第三方完整 Mod 组合与可见 Steam 性能未复测。

## 同根成长胜利续用（2026-10-05）

`PRIMARY-INCUMBENT-REUSE` Passed，runId `7128ceee4238421bb34376b19e373dec`，22.713 秒。原生储君根连续三次请求均为第 2 回合零损胜利、成长 1 次、零药；逐转移完整前缀回放、完整质量比较、政策变化失效与 live 隔离通过，实例已删除。失败基线为 PR #215 的 `02cf2283`：第一次胜利，第二次返回未完成路线。

PowerShell：`tools/testing/run-unattended-test.ps1 -ScenarioId PRIMARY-INCUMBENT-REUSE -CharacterId REGENT -Seed GROWTHBUCKET20261004 -EnemyCurrentHp 18 -TimeoutSeconds 120 -CleanupInstanceOnExit`。Bash 使用 `--scenario-id PRIMARY-INCUMBENT-REUSE --character-id REGENT --seed GROWTHBUCKET20261004 --enemy-current-hp 18 --timeout-seconds 120 --cleanup-instance-on-exit`。

同根离线复跑使用[固定根](../../../coverage/fixtures/search/shared-growth-incumbent-reuse.json)与[成长额度](../../../coverage/fixtures/search/shared-growth-incumbent-settings.json)，命令见[策略说明](../../strategy/hp-loss-pruning/README.md#官方历史同根成长路线续用)。该合同验证原生建局上的搜索与回放；整场部署及可见 Steam 性能分别验收。

两项 PR 集成后的 `SHARED-GROWTH-AUTO-DEPLOY` Passed，runId `929ad5193d8d47c0b9cdddf14d027e78`，23.095秒。使用上述固定根的牌序，原生全自动在第2回合获胜，玩家75/75、零药，首动ROYALTIES，第2回合续用，计划外重算0。固定 Beam45/20000节点/20000ms/DOP1，严格增量验证，Instant/0秒部署；实例已删除。该模式的耗时用于正确性验收。独立离线回血哨兵本轮仍为先NOT_YET再击杀、战损0，展开243/转移527，单次搜索0.65秒；保持该固定根的质量与工作量。

## 0.49.4 上传引导（2026-10-05）

`CONTENT-MOD-FAILURES` 最小原生合同初轮 Passed，runId `c9d7eff6241f4d89bfbb8a544c483558`，26.066 秒。补齐部署开始、回合准备失配与实机风险复核时的内容观察后，最终行为源码 Passed，runId `73fa6ecba0ff47ee9adf777a84abd640`，25.508 秒。新增 `Contracts/Runtime/UnattendedTestRunner.UploadGuidance.cs`，从已有内容失败合同调用：

- 搜索期间 player 输入触发手操标记；solver / system 输入及上一轮手操保持独立；录制明细不完整时仍识别输入。
- 手操过期免上传，来源不明的过期保留上传，同场已有诊断问题不被后续手操过期清除。
- 使用游戏 `AssemblyInfo.MockTypes` 对实际角色、牌、遗物与怪物逐项提供第三方来源，四类普通异常保留原错误及分类；反馈横幅、全自动战损暂停与实机复核提示均免上传。声明为非 gameplay 的实际内容同样适用。本场未出现的登记模型及怪物不影响原版引导，新战斗重新判断。
- eng / zhs / zht 文案覆盖；既有八类 Hook、计算型变量、包装异常及原版/框架/运行库失败合同继续通过。

```powershell
pwsh -NoProfile -File tools/testing/run-unattended-test.ps1 -ScenarioId CONTENT-MOD-FAILURES -EnemyCurrentHp 1000 -TimeoutSeconds 120 -CleanupInstanceOnExit
```

本次使用 Debug 测试 DLL；保留用户未完成的 `ISSUE-212-RECORDED-BOUNDARY` 测试入口，通过临时 MSBuild 输入改用该文件的已提交版本。启动器临时副本改用已有进程路径查询，保留进程出生时间、租约和清理检查。启动器已报告删除整个私有实例。两份玩家包仅作静态取证，未复跑观者实际 Mod 栈，未验证可见 Steam 排版。
