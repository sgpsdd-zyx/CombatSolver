# 多人未击杀长线选路研究

日期：2026-09-20。用户要求继续借助此前的 ChatGPT 6 Pro，研究如何在队友动作未知的多人战斗中兼顾战损与长线输出，避免未击杀候选只按第一周期收益排序。固定当前行为为 `851c1521f5258ec43dc71c24eeb8d5200fdf612d / 0.43.3`，本轮仅研究和离线实验。

- [完整研究请求](../multiplayer-window-selection-review-request-20260920.md)：当前源码、五个候选方向、反例、成本与交付要求。
- [本地生产候选观察](local-review.md)：实际搜索生成池中的浅比较、晚回本、药水时机与局限。
- 外部入口：[源码复审与策略优化](https://chatgpt.com/c/6aacf71e-165c-83ec-896a-a77f789ad559)，沿用 Hubstudio 环境 8；2026-09-20 20:40 America/Los_Angeles 提交，界面显示 `6 Pro`、当前源码 ZIP、专项请求和 `Pro thinking`。

## 6 Pro 原始交付

6 Pro 已完成本轮研究，界面记录工作 `40m 12s`，完整证据包下载成功。原始[复审报告](CombatSolver_Multiplayer_WindowSelection_20260920_Review.md)、[策略设计](CombatSolver_Multiplayer_WindowSelection_20260920_Design.md)、[实验脚本](CombatSolver_Multiplayer_WindowSelection_20260920_Experiments.py)、[读取范围](CombatSolver_Multiplayer_WindowSelection_20260920_Read_Sources.md)、[执行凭证](CombatSolver_Multiplayer_WindowSelection_20260920_Execution_Receipt.md)和[交付验证](CombatSolver_Multiplayer_WindowSelection_20260920_Delivery_Validation.json)原样归档。先前研究的最终回文不计为本轮结果。收取记录见 [receipt.json](receipt.json)。

外部实际运行 269 次抽象搜索、24 个不同抽象根、18 项微型检查；[关键结果摘录](pro-results-summary.json)直接从原 JSON 提取，本地未重复运行。完整原始 JSON、stdout、源码摘录、模型修正前结果和 ZIP 均保存在 `.local/mp-window-selection-20260920/pro-delivery/` 及其上级目录，不进入源码提交。Pro 环境没有生产 DLL 或 .NET，其数字不能计为本地游戏验证。

外部主方案 C 要求按完整当前回合建立代表集合，使用所有在册代表都具备的最深共同检查点比较；保护资格、终局和已知具体后缀风险，先复用已有证据。低成本回退仍为现役 A。只对齐动作成本的 B、按候选数量选深层的 C_count、主动补齐 D、多时点加权 E 均没有成为默认发布建议。C 本身也有扫描预算、队友目标失效和覆盖过度保守的反例。

## 本地判断与采用状态

本地执行了八次真实 C# 搜索候选池观察，独立于外部抽象模型。[本地原始数据摘要](production-pool-summary.json)与[代表覆盖条件](local-coverage-gate.json)分开保存，没有实施 C 的完整比较器或请求调度。

晚回本输入中，现役比较第 4 周期，同池三个当前回合代表都各有第 5 周期证据。离线第五周期重选把当前攻击改为铺垫，统一十四周期评价多 19 点伤害、前三周期少 3 点。它符合 C 的一个必要深度条件，支持尝试受限原型；缓冲保护、续行机会不齐和单敌输入使其不足以证明一般保战损或全面不退化。

药水 180 节点输入更限制方案范围：当前池共同周期为 1，但那个浅无药攻击代表没有深后缀，全代表覆盖仍只能是 1。单纯筛掉它虽能升至 9/10，却违反 C 的覆盖要求，不能算作 C 成功。这也说明 C 只处理“已有的深证据被遮蔽”，无法给尚未计算的方案补出未来。

本轮建议是进入 C 的小范围离线原型，优先复用已经付费的后续，保留明确 A 回退；补齐续行 D 是第二条需要单独验证成本与质量的候选。下一项区分性实验应保留药水浅代表这一负例，加入真实防守及同预算开销，不把本轮按深度重选冒充完整 C 验收。结论与实现细节见[本地复核](local-review.md)。

当前生产仍使用原共同周期、完整动作数同分和十四周期政策；不提升版本、不创建新包、不推送或发布。
