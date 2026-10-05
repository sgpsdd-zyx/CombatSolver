# Shared HP-loss incumbent pruning

This single-player change targets repeated work after an eligible complete victory is known: unavoidable early damage followed by safe turns, and fast victories rediscovered by portfolio members. Baseline: upstream 5d28a1cfa (0.49.3).

The multiplayer fork keeps this policy only on the official single-player path. Multiplayer roots reject healing/growth certificates, and multiplayer policy and solvers do not reuse primary incumbent tables or bounds. Current ownership is in [the architecture map](../../ARCHITECTURE.md); current fork validation is in [the merge record](../../archive/strategy/upstream-0500-merge-20261005.md).

## Scope and safety

PrimaryIncumbentTable stores completed, hard-policy-compliant victories by outstanding stolen resources, explicit potion uses, per-source growth count vector, and satisfied relic target mask. Different growth sources and relic combinations remain separate; scalar HP witnesses cannot replace resource-target witnesses.

Coordinator members share pure-value bounds through frozen policy, retaining independent simulators, frontiers and transpositions. Live combat sessions carry one executable potion-free witness only when the full root continuation stamp, damage ledger and policy match. Changed input invalidates it. Victory publication occurs at the serial commit boundary.

Potion buckets are consumed only when explicit use is closed by member policy or the maximum-use limit. HP lower bounds retain future healing, death protection, post-combat healing and boss HP relief. Unknown sources keep the upstream conservative allowance. Stolen healing cards remain possible recovery sources.

PreserveResources looks up a lower bound on final unrecovered resources, subtracting resources recoverable from living enemies. It does not use the current missing-card count directly. Missing compatible witnesses preserve expansion.

Growth caps require a separate narrow original-content closure: exhausting growth sources, audited basic cards/statuses/curses, selected relics and powers. Generation, exhaust recovery, unknown callbacks and live growth-copying opportunities reject certification. Opportunity metadata alone is not a cap.

For pure growth targets, each possible final source-count vector consumes its own witnessed victory. Shared expansion stops only when every possible vector is bounded. Missing witnesses or more than 256 combinations preserve expansion. Growth/relic mixtures retain the optimistic final-bucket path, without claiming complete independent enumeration of relic outcomes.

DualWield remains uncertified while a growth object can be copied. Once no usable growth object remains, copying ordinary cards cannot reopen growth, and branch certification can resume. Fork isolation is tested.

## Equality tradeoff

Eligible unfinished, risk-free branches may stop when their optimistic strategic HP deficit equals a compatible completed victory. This prioritizes reduced search work over finding an earlier victory with identical resource outcome and HP loss. It is not a proof that the original complete ordering or every finite-Beam result is preserved. New equality pruning leaves completed candidates intact.

No action commutativity prediction, pile-order masking or multiplayer pruning is included.

## Reproduction

Build CombatSolver.csproj and tools/search/OfflineSearchHarness/OfflineSearchHarness.csproj in Release. Installed game/RitsuLib paths come from local.props; personal paths do not belong in committed requests.

Run the harness with --check-primary-incumbents for shared table contracts, or --check-early-turn-continuation-bound for existing continuation contracts.

Set OFFLINE_HARNESS_RESOURCE_BUCKET_CHECKS=1 and run --request coverage/fixtures/scenarios/state/royalties-resource-0170.json --milestone M1 for resource contracts. Set OFFLINE_HARNESS_THEFT_BUCKET_CHECKS=1 on an Ironclad combat for theft contracts. These are shadow-state assertions, not native actual/simulated acceptance.

OFFLINE_HARNESS_RESOURCE_SETTINGS reads a test-only JSON containing GrowthBudgets, RelicStrategyEnabled and RelicCounterRules, for example {"growthBudgets":{"royalties":5}}. Other settings stay CLI-controlled; player settings are not modified.

--disable-shared-incumbents retains new member-local equality behavior, so it is not the entire upstream baseline. --verify-shared-incumbent-reuse checks same-root reuse and policy invalidation on small Coordinator requests. --verify-incremental performs complete prefix replay and is excluded from performance samples.

## Evidence and limits

Rebased validation is recorded in [the test matrix](../../TEST_MATRIX.md). Historical 0.49.1 results included expanded nodes 28,956 to 17,802 on the restored Infested Prisms root and 5,669 to 3,964 on a growth-copy fixture. These are prior-version evidence, not new 0.49.3 measurements.

Offline comparisons do not prove native automatic deployment, visible Steam frame time, all growth sources, all positive potion tiers or global optimality. The upstream comparison harness receives the same test-only resource-settings loader; upstream production source is unchanged.

## 官方历史：同根成长路线续用

完整零药胜利以完整结果参与下一次同根请求的选优，成长与遗物收益沿原政策比较；纯 HP 剪枝资格单独判断。固定成长根的敌方生命为 18，需要跨回合获胜，用于覆盖携带成长见证后的再次搜索。

```powershell
$env:OFFLINE_HARNESS_RESOURCE_SETTINGS = 'coverage/fixtures/search/shared-growth-incumbent-settings.json'
dotnet .local/tool-build/OfflineSearchHarness/bin/Release/net9.0/OfflineSearchHarness.dll --request coverage/fixtures/search/shared-growth-incumbent-reuse.json --label growth-reuse --out .local/growth-reuse --beam 45 --nodes 20000 --budget-ms 20000 --dop 1 --potion-policy Disabled --search-mode Coordinator --use-portfolio --verify-shared-incumbent-reuse
```

`02cf2283` 的该根第一次为第 2 回合零损胜利，第二次为未完成路线。修复后同根续用和政策变化失效通过。原生 `PRIMARY-INCUMBENT-REUSE` 连续三次请求、完整质量、严格增量回放和 live 隔离通过，直接证据见[测试矩阵](../../archive/testing/volume-16.md#同根成长胜利续用2026-10-05)。续用模式累计多次请求的执行时间，只用于正确性检查。

## 官方历史：单人共享损血剪枝（2026-10-05）

基线 `5d28a1cfa`（0.49.3），游戏 0.111.0 / RitsuLib 0.6.5。候选主项目及离线宿主 Release 构建均 0 警告、0 错误。初次构建缺 net48 引用程序集，使用本机已有 NuGet 引用包的 FrameworkPathOverride 后构建成功；未修改上游构建配置。

本轮共享表20项、既有早回合界143项、成长资源35项、偷窃边界10项全部通过。成长小根的增量完整前缀回放及同根共享见证续用、政策变化失效检查通过。这些不是原生 actual/simulated 整场验收。

独立 .NET 进程、Coordinator及组合开启、Disabled、DOP1、牌堆掩码0、No-GC关闭；两侧均 Boundary=None。上游仅在测试宿主移入相同 resource-settings 读取方法，生产源码保持基线；没有用关闭共享表代替整个上游基线。

| 固定根 | 相同终局 | 展开：上游 → 候选 | 转移：上游 → 候选 | 单次搜索秒：上游 → 候选 |
| --- | --- | --- | --- | --- |
| Royalties 成长 | 胜利、收益1次/额度5、战损0、零药、第1回合 | 7572 → 11 | 20336 → 43 | 5.66 → 0.60 |
| NotYet 回血哨兵 | 胜利、先回血再击杀、战损0、零药、第1回合 | 243 → 243 | 527 → 527 | 0.62 → 0.72 |

成长根：REGENT / FUZZY_WURM_CRAWLER_WEAK / GROWTHBUCKET20261004，飞升0、敌HP6、玩家75/75、能量3、原生遗物。清空牌组与牌堆，手牌永久Royalties、2张StrikeRegent、4张DefendRegent；抽牌堆5张StrikeRegent。测试设置 `{"growthBudgets":{"royalties":5}}`。Beam45、20000节点、20000ms。回血根使用 `coverage/fixtures/scenarios/state/not-yet-heal-resource-0170.json`，Beam20、12000节点、20000ms。完整命令与边界入口见[复跑说明](#reproduction)。

耗时是单次离线观察，回血哨兵本次多0.10秒，不能称为所有场景提速。未执行原问题包恢复、完整原生自动部署、可见Steam性能、全部成长来源及正数药水档整场验证。本机产物保存在忽略目录 `.local/pr-validation/`。

## 官方历史：与PR #207的整合边界

当前分支复用此共享表。组件证书根的等HP（含更晚回合）比较还须核对实际已用药水成本；缺失见证成本保留分支。无遗物目标时还可计入剩余必须显式用药次数乘以根冻结的最低药水成本；只有组件生成闭包排除新增药水来源才消费此界，未知分支保留。组件证书根的新共享消费严格分支回复证明，未知分支不借启发式重新认证；旧遭遇认证和其他已知原版根继续上游既有政策，不能写成新增严格证明。零HP额度遗物目标保留原有本地严格HP界，同HP目标路线继续搜索；正额度、成长和追回仍有原边界。原生证据及当前上游对照见[0.49.4整合记录](../../archive/performance/upstream-0500/pr207-upstream-0494-integration-20261005.md)。
