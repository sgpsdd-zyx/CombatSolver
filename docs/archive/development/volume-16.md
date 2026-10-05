# CombatSolver 开发笔记历史卷 16

[返回归档索引](README.md)

本卷记录源码 `6a073d9e` 的历史验证。当前 0.49.3 的实现与验证见 [历史卷 17](volume-17.md)。

## 0.49.3 战斗兼容性边界（2026-10-04）

兼容性按本场战斗的可达模型、订阅器和补丁目标判断。商店删牌价格、地图、事件、休息处等战斗外修改放行；实际参与战斗的未适配模型、效果、怪物、意图、回调与原版数值修改保留明确边界。Mod ID、程序集名和玩法声明用于报告来源。

根因是根捕获与卡牌审计按加载身份拒绝 BetterVanillaSTS2；未改行为源码的托管合同在仅加载该 Mod 名称、没有战斗补丁时复现 `IncompatibleGameplayModException`。原生复现使用同一来源名对 `MerchantCardRemovalEntry.CalcCost` 安装补丁，覆盖 true/false 两种玩法声明和四个已有来源名，实际根捕获放行；对原版卡牌 CanonicalVars 的修改被拒绝，继承来的战斗 Hook 被拒绝，安装与卸载期间真实战斗状态戳一致。

职责位于 Prediction 的来源审计与订阅器分类、Engine 的标准镜像登记表，以及 Runtime 的根捕获接线。共享战斗外 Hook 分类；镜像覆写通过明确登记或推断合同选择支持状态。纯表现覆写使用 `RegisterIgnored`。卡牌 OnPlay 完整组合、异步 MoveNext、未知来源和卸载后重捕获沿既有合同处理。

Ritsu 目标、星能、可打性和升级桥接只在根牌的 capability 集为空时接受精确桥接方法。BaseLib 升级桥接检查实际修饰器集合，变量升级桥接在数值 getter 审计后检查每个变量的升级额度。仅覆写战斗外 Hook 的遗物、Modifier，其商店元数据按战斗外内容处理。

### 验证与范围

- `AdaptedOnPlayChecks --empty` 2 项 Passed；普通模式最终 49 项 Passed，含真实 Harmony 安装／卸载、完整组合、数值拒绝、商店 Hook 和遗物元数据、玩法声明、空框架桥接以及实际升级额度／修饰器贡献拒绝。BaseLib 采用框架接口外壳，不代替真实框架完整栈验收。
- `ROOT-CONTENT-SOURCES` Passed：runId `3cd7ec96ae094e63b66cfe68dc1c55b8`，24.913 秒。覆盖实际商店价格与战斗数值入口、订阅器继承、怪物、意图缺失来源、回调重入、Owner 绑定 Power 预热与分类。此原生输入采用 RitsuLib，后续 BaseLib 空桥接扩展由托管合同验证。
- `CONTENT-MOD-FAILURES` 加 `VerifyPredictionFailureBoundaries` Passed：runId `20e28be96c83465789cc9266fd5b5c42`，24.396 秒。声明为非 gameplay 的八个未适配回调仍拒绝；计算变量、包装异常、eng/zhs/zht 名称与上传分类通过。
- `PR18-FOREIGN-ONPLAY-BOUNDARY` Passed：runId `453fc5c44e2b4a5483c553ab543557e6`，23.548 秒。包含后装补丁、async MoveNext、玩法声明、未知 owner、卸载与根状态保持；一张原版打击完成真实部署，玩家 HP=80，敌人 HP=0。

原生合同均使用 `-TimeoutSeconds 120 -CleanupInstanceOnExit`，启动器确认删除各次实例。历史入口与命令见 [该源码的测试矩阵](https://github.com/Torch1230/CombatSolver/blob/6a073d9e/docs/TEST_MATRIX.md#0493-战斗兼容性边界2026-10-04)。

本轮工作区已有 Executor 调查接线指向缺失的 `ProbeIssue212RecordedBoundaryAsync`。测试与定版构建通过忽略目录中的临时 MSBuild target 选择该文件的已提交版本，保留原有工作区改动。原生启动器的 MainModule 映像路径检查拒绝了启动进程；临时启动器改用现有 `Get-ProcessExecutablePath` 的映像查询入口，保留精确进程路径、出生时间、租约及退出清理检查。临时构建和启动辅助在交付后删除，主项目启动工具未修改。

BetterVanillaSTS2 原包及其各项配置、真实 BaseLib 完整栈、可见 Steam 排版和任意全局 detour 未执行。局外放行证据来自真实商店入口的同类补丁；不把此证据扩写成特定第三方版本的完整兼容认证。初始化后直接改写字段及尚未进入根的生成模型仍需各自的语义合同，详见 [适配手册](../../third-party/README.md)。
