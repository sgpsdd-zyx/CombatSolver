# CombatSolver 测试历史卷 14

归档日期：2026-10-05。保留 0.49.3 当时的验证、失败和未验证项。

## 0.49.3 框架与局外 Mod 兼容性（2026-10-04）

托管 `AdaptedOnPlayChecks` 普通模式 41 项、`--empty` 5 项 Passed。覆盖 BetterVanillaSTS2、BaseLib、RitsuLib 加载身份、gameplay-neutral 框架准入、真实 Harmony 完整组合和未适配 gameplay OnPlay 拒绝。

```powershell
dotnet run --project tools/testing/checks/AdaptedOnPlayChecks/AdaptedOnPlayChecks.csproj -c Release -p:Sts2DataDir=<游戏数据目录>
dotnet run --project tools/testing/checks/AdaptedOnPlayChecks/AdaptedOnPlayChecks.csproj -c Release -p:Sts2DataDir=<游戏数据目录> -- --empty
```

原生验证使用安装的 BaseLib 3.4.7 DLL/PCK/manifest 与 RitsuLib，进入同一私有游戏快照：

| 场景 | runId | 秒 | 直接证据 |
| --- | --- | --- | --- |
| `ROOT-CONTENT-SOURCES` | `791612a7355141518541227c3216270b` | 25.556 | 真实商店删牌入口放行，怪物和意图来源拒绝，根与真实战斗状态保持 |
| BaseLib 最小准入 + `PR18-FOREIGN-ONPLAY-BOUNDARY` | `2781b571a8a04df2841924fba091511e` | 23.821 | Passed；真实修饰器根、Owner、Fork 隔离、原有 OnPlay 门禁与一次原版求解部署 |
| BaseLib 完整合同 + `PR18-FOREIGN-ONPLAY-BOUNDARY` | `646fb5504eb445efbaa14fe8068f84b0` | 23.892 | Passed；原生生成牌状态键、五种牌堆及离堆的监听生命周期，原有完整修饰器合同、OnPlay 边界和一次原版求解部署 |

原生入口为 `tools/testing/run-unattended-test.ps1 -ScenarioId <表内场景> -TimeoutSeconds 120 -CleanupInstanceOnExit`。ROOT 使用 `-EnemyCurrentHp 1000`；PR18 使用 `-EnemyCurrentHp 1 -VerifyBaseLibCardModifierBoundary`。早先的最小准入样本通过临时 fixture 限定根、Owner 与 Fork，输出独立的 `BaseLibRootAdmission` 标记；当前完整合同输出 `BaseLibCardModifierBoundary` 与 `BaseLibGeneratedClone:NativeState:Created:Hand:Draw:Discard:Exhaust:Play:Removed`。

此前完整 BaseLib 修饰器合同 Failed，runId `71984ec7e337460da825c98566898474`，22.033 秒，原文保留在 [历史卷 17](../development/volume-17.md#验证与范围)。本轮拆分断言的失败基线 runId `ef33630d6b52447b9279fefce87117e2`，21.952 秒，定位为入堆前的 listener 预期；生成牌修饰器和重置字段通过。当前合同按真实 BaseLib 的牌堆生命周期对照，完整通过；根因和范围见 [历史卷 18](../development/volume-18.md#baselib-生成牌回归合同2026-10-04)。

Bash 使用同名 scenario 和对应长参数。本轮使用已提交 Executor 的隔离构建；临时启动器将 BaseLib 原包加入私有快照，并使用现有映像查询入口。进程身份、租约与退出清理检查保持，各次实例已删除。验证来源与未验证项见 [开发历史卷 17](../development/volume-17.md#验证与范围)，源码 `6a073d9e` 的历史验证见 [历史卷 16](../development/volume-16.md#验证与范围)。
