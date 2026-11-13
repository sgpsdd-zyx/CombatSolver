# Shared HP-loss incumbent pruning

This single-player policy targets repeated work after an eligible complete victory is known: unavoidable early damage followed by safe turns, and fast victories rediscovered by portfolio members. The current source integrates upstream 06083d36 (0.51.3). Two representative solo roots match the independently built upstream source, and multiplayer isolation checks pass; this merge does not establish boss-route quality across encounters.

The multiplayer fork keeps this policy only on the official single-player path. Multiplayer roots reject healing/growth certificates, and multiplayer policy and solvers do not reuse primary incumbent tables or bounds. Current ownership is in [the architecture map](../../ARCHITECTURE.md); current fork validation is in [the merge record](../../archive/strategy/upstream-0513-merge-20261113.md).

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

Current validation status is recorded in [the test matrix](../../TEST_MATRIX.md). Prior-version results, failures and limits are preserved in [the historical record](../../archive/strategy/single-player-hp-pruning-through-0513.md); they are not evidence for the current merge.

Offline comparisons do not prove native automatic deployment, visible Steam frame time, all growth sources, all positive potion tiers or global optimality. The upstream comparison harness receives the same test-only resource-settings loader; upstream production source is unchanged.

## 首领卖血预算遍历

原版首领房间默认启用，包含第三幕双首领第一场。主搜索及原后处理完成后追加原配置节点和软时间的各1/5；达成既有完整HP目标时可以跳过。额外工作进入请求总账本，取消、接管及内存无进展沿原边界结束。时间是软限额，单次展开和最终回放可能超过截止点。

侦察以追加额度的至多1/3、3次排序偏差枚举首回合边界。当前回合动作和有正净潜力的强能力承诺先行，再按HP归一的中途分排序。动作Top-K和Beam席位由待处理栈替代，选牌枚举受原预算限制；独立转置键包含完整状态和剩余偏差，六维精确支配标签沿原实现。回调只冻结动作、状态键、消灭敌人数、用药量和排名，原模拟器随侦察释放。用药档0/1/2/3及以上各保留至多6个代表，优先已经兑现全部强制用药指令的前缀，再按消灭敌人数、用药量及排名选至多两个前缀，固定回放后由原Beam完成后续；两阶段共用追加余量。有限额度下仍可能漏解，不作全局最优或收敛保证。

生存按真实HP、回血和复活判断。累计损血可能因治疗超过开战HP，固定累计上限会剪掉这些可生存路线；预算界使用已有完整胜利和剩余治疗下界，并按用药、成长、遗物及追回资源分桶。等战损后续候选继续探索。新成员中途排序消除累计损血的重复计价和额外卖血罚分；致死投影的活节点继续参与出牌，实际死亡保留原判定。终局仍使用既有实际政策轴，跨成员的候选池相关Score尾键归零后比较。

原生遍历使用维护的 [卖血手牌](../../../coverage/fixtures/search/boss-tempo-native-cards.json)，由 `GENERATED-NOVELTY-SEARCH` 执行严格增量回放和完整部署。复跑时创建自有EvidenceDirectory，其中 `research-options.json` 写 `{"scheduler":"tempo"}` 并创建 `native.flag`；IRONCLAD / AEONGLASS_BOSS / Act2，seed `BOSSTEMPOBLOOD20261007`，敌HP28、玩家80/80、能量3，ClearPlayerHand/Piles，Beam1、1000节点、5000ms、DOP1、Disabled、NoGC0、Instant/0秒、Fixed、VerifyIncremental、Timeout120、CleanupInstanceOnExit。`tempo-portfolio` 配合 `smart.flag` 验证协调器追加额度，`beam` 配合该标记执行关闭成员的同条件对照。

多人政策显式清空该成员、配置与等损保路开关；只保留官方单人路径。既有批次的对照、失败、原生兑现与研究缺口见[官方历史归档](../../archive/strategy/single-player-hp-pruning-through-0513.md)。本轮游戏 0.113 的编译和验证仍以[合并记录](../../archive/strategy/upstream-0513-merge-20261113.md)为准。
