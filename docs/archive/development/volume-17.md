# CombatSolver 开发笔记历史卷 17

[返回归档索引](README.md)

## 0.49.3 框架与局外 Mod 兼容性（2026-10-04）

Mod ID 和程序集名用于来源展示。局外 Mod 使用既有战斗外 Hook 分类；商店删牌价格通过原生商店入口处理。BaseLib、RitsuLib、gameplay-neutral 来源、镜像登记与订阅器沿用 `387863d0` 的实现和准入规则。实际未适配内容继续通过模型、gameplay subscriber、OnPlay 和怪物 AI 的既有语义门禁报告。

核心变化位于 Prediction 的加载身份边界和 Runtime 的根捕获入口。模型数值读取、CardModifier 所有权、Ritsu capability、mirror 的默认分类与已适配 OnPlay 分派保持已有合同。新增回归只证明加载身份与商店修改可以求解，已有战斗内容合同仍可执行。

### 验证与范围

托管 `AdaptedOnPlayChecks` 普通模式 41 项、`--empty` 5 项 Passed。覆盖加载 BetterVanillaSTS2／BaseLib／RitsuLib、gameplay-neutral 补丁的既有准入、未适配 gameplay OnPlay、完整组合、来源、async MoveNext、安装／卸载与冻结分派。

原生 `ROOT-CONTENT-SOURCES` Passed：runId `791612a7355141518541227c3216270b`，25.556 秒。对真实 `MerchantCardRemovalEntry.CalcCost` 安装同类补丁，覆盖四个 Mod 来源名和 true/false 玩法声明；根捕获放行并保持真实战斗状态。怪物、意图缺失来源、回调重入、Owner 绑定 Power 与失败分类通过。

真实 BaseLib 最小准入和 `PR18-FOREIGN-ONPLAY-BOUNDARY` Passed：runId `2781b571a8a04df2841924fba091511e`，23.821 秒。沿原修饰器夹具创建真实 BaseLib CardModifier 子类并附着到根牌，核对根捕获、原状态戳、父子修饰器身份／Owner、子分支金额修改的父／live 隔离；输出 `BaseLibRootAdmission:ActiveModifier:Owner:ForkIsolation`。同进程还通过后装 OnPlay、async MoveNext、neutral／未知来源、卸载和根保持；原版打击的一次搜索部署通过，玩家 HP=80，敌人 HP=0。临时 fixture 只运行原合同的根与 Fork 子集，未更改生产框架代码或原完整合同的断言。

真实 BaseLib 完整 `VerifyBaseLibCardModifierBoundary` 合同 Failed：runId `71984ec7e337460da825c98566898474`，22.033 秒。根捕获与父子预测修饰器／Owner 隔离检查通过，之后在生成牌克隆检查报告“游戏玩法生成的卡牌克隆没有独立复制 BaseLib CardModifier 状态、Owner、listener 或官方生成卡字段”。该失败与准入分别记录，相关克隆实现保持原源码；本轮没有修复或确认其具体字段原因。

原生私有快照包含安装的 BaseLib 3.4.7 DLL、PCK、manifest 和 RitsuLib。构建使用已提交 Executor 的隔离接线，保留工作区原有 Issue 212 临时接线；启动器使用已有映像查询入口，保留精确进程身份与租约检查。请求总超时为 120 秒，均带 `CleanupInstanceOnExit`；临时构建和启动辅助在交付后删除。命令见 [测试历史卷 14](../testing/volume-14.md#0493-框架与局外-mod-兼容性2026-10-04)。

BetterVanillaSTS2 原包及其各项配置、任意第三方内容完整适配、可见 Steam 交互和性能未执行。本轮局外兼容性证据来自真实商店入口的同类补丁；原生框架验证采用安装的 BaseLib 原包。
