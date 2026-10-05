# 内容性 Mod 失败分类验证历史（归档于2026-10-05）

保留此前结果、失败与未验证项，命令从仓库根运行；不代表本轮重新测试。当前入口见[测试矩阵](../../TEST_MATRIX.md)。

## 0.49.2 内容性 Mod 失败分类（2026-10-04）

`CONTENT-MOD-FAILURES` 与 `VerifyPredictionFailureBoundaries` 同进程 Passed，runId `849fb38580474f7881c05d113beef5d3`，24.505 秒。合同直接调用五个回合阶段、三个金币回调与计算型变量的生产拒绝入口，断言确认的第三方来源、原生回调保持未执行、包装异常、eng/zhs/zht 的 Mod 名称与方括号转义、四类失败账本仅记录暂未适配且不触发上传。原版、共享计算框架与运行库失败继续提示诊断上传；平台接口错误保留主失败类别。

```powershell
pwsh -NoProfile -File tools\testing\run-unattended-test.ps1 -ScenarioId CONTENT-MOD-FAILURES -EnemyCurrentHp 1000 -VerifyPredictionFailureBoundaries -TimeoutSeconds 120 -CleanupInstanceOnExit
```

```bash
./tools/testing/run-unattended-test.sh --scenario-id CONTENT-MOD-FAILURES --enemy-current-hp 1000 --verify-prediction-failure-boundaries --timeout-seconds 120 --cleanup-instance-on-exit
```

来源与验证边界见 [开发历史卷 14](../development/volume-14.md)。最终行为源码在版本同步前通过；后续只改版本元数据和文档，采用最终 Release 构建。测试启动器已删除实例，未执行可见 Steam 弹窗排版验收或第三方原包整场回放。
