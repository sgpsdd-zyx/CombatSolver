# 原始文献与适用边界

本地于 2026-09-22 通过作者、机构、会议或 arXiv 原始页面核对。检索接口返回 HTTP 404 后，改用浏览器检索和原站读取；没有把检索摘要或其他模型回答当成原文。完整论文临时保存在 `.local/mp-coop-strategy-20260922/papers/`，不把第三方全文复制进源码仓库。

下列“本项目可用”均是应用推论，不是论文已经验证 CombatSolver。

| 文献 | 本次实际阅读 | 本项目可用的思想 | 不能直接继承的结论 |
|---|---|---|---|
| Mavrotas, 2009, [Effective implementation of the epsilon-constraint method in Multi-Objective Mathematical Programming problems](https://dspace.lib.ntua.gr/xmlui/handle/123456789/19533?show=full)；DOI `10.1016/j.amc.2009.03.037` | 作者机构元数据、摘要；该条目没有全文附件 | 将伤害要求作为约束、以战损为优化目标；保留不同要求下的权衡解 | 本次未阅读全文，不声称移植了 AUGMECON；Beam 的不完备候选集不等于数学规划的完整 Pareto 集 |
| Bertsekas, Tsitsiklis & Wu, 1997, [Rollout Algorithms for Combinatorial Optimization](https://web.mit.edu/dimitrib/www/rollout.pdf) | 动态规划建模、顺序一致性定义、Proposition 1 与终止条件 | 枚举当前选择，再由可执行基策略续行，用完整结果比较当前选择；保留基策略见证 | 理论改善要求相应一致性、终止与评价假设；有限 Beam、截断值和错误队友模型没有自动“不比原版差”保证 |
| Bertsekas, 2020, [Constrained Multiagent Rollout and Multidimensional Assignment with the Auction Algorithm](https://arxiv.org/abs/2002.07407) | 摘要、约束轨迹模型、rollout 改善性质、结论 | 约束可以作用于整条轨迹；按多行动分量改善，降低联合动作爆炸 | 论文控制或协调多个行动分量。真人队友不执行选定分量时，不能套用其确定性改善保证 |
| Stone, Kaminka, Kraus & Rosenschein, 2010, [Ad Hoc Autonomous Agent Teams: Collaboration without Pre-Coordination](https://www.cs.utexas.edu/~pstone/Papers/bib2html-links/AAAI10-adhoc.pdf) | 问题定义、能力/知识差异、配对评估方案 | 不预先约定策略的组队；按队友能力调整分工，跨不同队友比较同一个顾问 | 这是问题框架与研究议程，不提供解决本游戏的成品算法 |
| Carroll et al., 2019, [On the Utility of Learning about Humans for Human-AI Coordination](https://arxiv.org/abs/1910.05789) | 摘要、人类数据与模型拆分、联合规划、不同伙伴评估（第 4、5 节） | 单独留出队友风格；对自己最优不代表和人配合好；观察后重新规划 | Overcooked 中的经验结果不是卡牌游戏胜率；已知精确人类模型的规划优势尤其不能当作未知真人保证 |
| Lerer, Hu, Foerster & Brown, AAAI 2020, [Improving Policies via Search in Cooperative Partially Observable Games](https://arxiv.org/abs/1912.02318) | 摘要、single-agent search、共同 blueprint 假设、multiple-agent search | 一名搜索者把其它人的策略视为环境的一部分；用短根搜索改善既有可执行策略 | SPARTA 要求共同知识的策略/搜索约定。真实队友不遵循已知 blueprint，且独立偏离时，理论改善保证失效 |
| Silver & Veness, 2010, [Monte-Carlo Planning in Large POMDPs](https://papers.neurips.cc/paper_files/paper/2010/file/edfbe1afcf9246bb0d40eb4d8027d90f-Paper.pdf) | 生成模型、根信念采样、历史策略与有限时域收敛条件 | 可以枚举可见当前行动，通过采样处理未知后续；决策依赖观察历史，不能让路线预知隐藏未来 | 收敛依赖正确的信念、模型、访问与渐近算力；几条手写队友情景不等于已校准的信念分布 |
| Cowling, Powley & Whitehouse, 2012, [Information Set Monte Carlo Tree Search](https://eprints.whiterose.ac.uk/id/eprint/75048/1/CowlingPowleyWhitehouse2012.pdf) | 引言、determinization 与 strategy fusion 问题、信息集树的动机 | 在不同隐藏未来中，同一可见历史必须共享决策；不能各自挑最优后续再当作真人可执行策略 | ISMCTS 不是消除全部模型误差的保证，需真实信息集和合法动作定义；当前队友情景设计仍需独立验证 |
| Churchill & Buro, CIG 2013, [Portfolio Greedy Search and Simulation for Large-Scale Combat in StarCraft](https://davechurchill.ca/publications/pdf/combat13.pdf) | 模拟器/行动表示、PGS 算法、脚本组合与实验定义 | 对昂贵未来使用少量风格明确的基策略；当前关键动作仍可精细枚举，控制分支爆炸 | 脚本集合不能产生未表达的组合；文中的同时行动战斗和性能数字不能直接套入本游戏 |
| Karnin, Koren & Somekh, ICML 2013, [Almost Optimal Exploration in Multi-Armed Bandits](https://proceedings.mlr.press/v28/karnin13.html) | 固定预算问题、Algorithm 2 Sequential Halving、Theorem 4.1 | 先让候选有共同的初始评价，再把后续样本集中给有希望的当前方案；目标是选对最终动作 | 自适应深度、相关情景、不断生成的新候选与不同模拟成本不满足原始简单模型，不能照搬置信界 |
| Chow, Tamar, Mannor & Pavone, 2015, [Risk-Sensitive and Robust Decision-Making: a CVaR Optimization Approach](https://arxiv.org/abs/1506.02188) | 摘要、CVaR 定义与分布扰动的等价解释、MDP 设定 | 在平均代价之外控制较坏尾部；明确风险程度如何改变方案 | 需要声明概率模型与误差预算；三到五条未经校准的队友情景不能给真人死亡概率，更不能把经验最坏样本称为精确 CVaR 风险保证 |
| Rawlings, Mayne & Diehl, [Model Predictive Control: Theory, Computation, and Design](https://sites.engineering.ucsb.edu/~jbraw/mpc/MPC-book-2nd-edition-1st-printing.pdf) | 第 2 章时域控制、终端成本、确定性与不确定性边界 | 有限未来的真实结算加截断后价值；执行当前部分，获得新状态后重新规划 | 连续控制稳定性、递归可行性并非卡牌规划保证；模型误差会破坏这些性质，本项目只采用思想 |

## 对技术选型的含义

贡献配额与情景 rollout 可以组合：前者定义要完成什么，后者评估队友会怎样使这个目标更容易或失效。当前行动束适合细粒度枚举，远期分支适合较便宜的合法基策略，最终选路必须使用同一信息条件与外部评价标准。

MCTS/PUCT 是预算分配和探索方式，不能自动修正错误的奖励、未来队友模型或卡牌语义。当前应先把这些问题做成可比较接口；是否替换 Beam，要让相同模型、相同目标、相同总预算的实验决定。

没有一篇上述论文证明本 Mod 能超过人类平均水平。合理的目标是：在限定场景下，以真实合法枚举减少牌序/费用/目标组合遗漏，再通过独立队友与真人评价检验整体策略质量。
