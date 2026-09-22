# 多人协作策略研究：原始文献与实际读取范围

研究日期：2026-09-22。下面只列作者、出版社、会议或作者上传的原文来源。论文支持方法的条件与限制，不替 CombatSolver 提供实测结果。没有把网页搜索摘要当作已经读过整篇论文；没有将原论文 PDF 重新打包分发。

## P1｜Rollout 与保留可执行基策略

Dimitri P. Bertsekas, John N. Tsitsiklis, Cynara Wu，1997，**Rollout Algorithms for Combinatorial Optimization**，Journal of Heuristics 3，245–262。

[作者 MIT 原文 PDF](https://web.mit.edu/dimitrib/www/rollout.pdf)。实际阅读摘要、算法定义及 sequential consistency / sequential improvement、fortified rollout 的相关段落，重点为 PDF 第5–12页；另查看首页截图。没有逐页审阅实验表。

可借用的是：在已定义问题、合法可终止基策略与相应一致性条件下，用续行评价候选；保留已有可行完整路线而不是预算耗尽时临时拼接。不能继承的是：有限样本、错误队友模型、近似终端值、被剪掉的候选以及同预算重复回放条件下的无退化保证。本报告的滚动规划是工程迁移，不是该论文定理的直接实例。

## P2｜滚动时域与终端价值不是安全证书

James B. Rawlings, David Q. Mayne, Moritz M. Diehl，2017，**Model Predictive Control: Theory, Computation, and Design**，第二版第一次印刷。

[作者教材页面](https://sites.engineering.ucsb.edu/~jbraw/mpc/)；[固定第一次印刷 PDF](https://sites.engineering.ucsb.edu/~jbraw/mpc/MPC-book-2nd-edition-1st-printing.pdf)。实际读取 receding-horizon 与动态规划关系段落，以及 §2.10 附近；查看 PDF 第212页、书页164截图。未通读819页教材。

可借用的是：当前状态下求一个有限时域计划，只执行近端，再在新观察时规划；终端值为截断后的未来补充估计。不能继承的是：本项目没有满足控制系统的终端集合、Lyapunov、模型与反馈频率条件；玩家手动请求也不是固定频率反馈，故不能声称 MPC 稳定性、递归可行性或长期安全。

## P3｜临时组队不等于联合最优控制

Peter Stone, Gal A. Kaminka, Sarit Kraus, Jeffrey S. Rosenschein，2010，**Ad Hoc Autonomous Agent Teams: Collaboration without Pre-Coordination**，AAAI。

[作者条目](https://www.cs.utexas.edu/~pstone/Papers/bib2html/b2hd-AAAI10-adhoc.html)；[作者原文 PDF](https://www.cs.utexas.edu/~pstone/Papers/bib2html-links/AAAI10-adhoc.pdf)。读取问题定义、评价讨论，查看 PDF 第2页截图。

对本项目的直接启发是：评估一个助手时，让它面对同一组未知队友，而不是为每条自身路线换一个最配合的队友。论文提出问题与评估方向，并没有提供无需数据即可校准真人行为概率的方法。

## P4｜信息集搜索及 strategy fusion

Peter I. Cowling, Edward J. Powley, Daniel Whitehouse，2012，**Information Set Monte Carlo Tree Search**，IEEE TCIAIG，DOI [10.1109/TCIAIG.2012.2200894](https://doi.org/10.1109/TCIAIG.2012.2200894)。

[大学存档原文 PDF](https://eprints.whiterose.ac.uk/id/eprint/75048/1/CowlingPowleyWhitehouse2012.pdf)。实际读取开篇确定化与 strategy fusion 讨论、相关算法说明与末尾局限；查看 PDF 第8页图3及周边说明。

本报告采用其反对证据：不能在不同隐藏世界分别选择互不相容的当前动作，再把各自最优值平均。共享当前动作束、未来仅依已观察信息响应是必要边界。ISMCTS 不是“加几个随机种子”的同义词；本轮 E 只实现动作前缀 UCT 提案器，不是 ISMCTS，不能把 E 的负结果推广成对所有信息集搜索的否定。

## P5｜UCT 的保证有模型前提

Levente Kocsis, Csaba Szepesvári，2006，**Bandit Based Monte-Carlo Planning**，ECML，282–293，DOI [10.1007/11871842_29](https://doi.org/10.1007/11871842_29)。

[出版社原始条目和摘要](https://link.springer.com/chapter/10.1007/11871842_29)。本轮该来源只读到摘要与书目信息；作者旧 PDF 地址读取失败，未声称读过其完整证明。

原始摘要明确以有限时域或折扣 MDP 为一致性及误差界讨论的条件。真人队友非平稳、未知策略、代理终值和截断生成器不自动满足这些条件。E 用有限前缀树、随机完成和边界启发式，是实际运行的有限替代，而非原论文全部条件的复刻。

## P6｜PUCT 需要有意义的先验与价值

David Silver 等，2017，**Mastering the game of Go without human knowledge**，Nature 550，354–359，DOI [10.1038/nature24270](https://doi.org/10.1038/nature24270)。

[出版社原文页面](https://www.nature.com/articles/nature24270)。实际可读范围为摘要、图题和参考文献；正文订阅受限。本报告仅据摘要确认训练后的策略/价值网络与搜索互相改进这一结构，没有转述未读的超参数或训练成本。

本项目没有这样的训练先验与校准价值，不应把均匀先验加一个 PUCT 公式当作已获得 AlphaZero 的能力。以后有高质量原生离线数据时可以比较，不是本轮首选。

## P7｜脚本组合的压缩收益与搜索盲点

David Churchill, Michael Buro，2013，**Portfolio Greedy Search and Simulation for Large-Scale Combat in StarCraft**，CIG，DOI [10.1109/CIG.2013.6633643](https://doi.org/10.1109/CIG.2013.6633643)。

[作者发表清单](https://davechurchill.ca/publications/)；[作者上传的论文正文](https://www.researchgate.net/publication/261527070_Portfolio_greedy_search_and_simulation_for_large-scale_combat_in_starcraft)。作者网站 PDF 链接读取失败；通过作者上传正文阅读摘要、§V 的 portfolio / hill-climbing 与 playout 方法说明及结论段。没有据未查看的表格引用性能数字。

适合借用的层：队友条件脚本、远端续行和一个付费基准建议。不能据此把本机所有当前回合只限制为脚本产生的序列；牌序、选牌、资源生成的组合可能不在脚本集合中。

进一步原始反证：Rubens Moraes, Julian Mariño, Levi Lelis，2018，**Nested-Greedy Search for Adversarial Real-Time Games**。[会议原始条目与摘要](https://ojs.aaai.org/index.php/AIIDE/article/view/13017)，DOI [10.1609/aiide.v14i1.13017](https://doi.org/10.1609/aiide.v14i1.13017)。本轮只读摘要：即使给 PGS 很强的可评价动作假设，它的搜索组织仍可能错失最好动作。本报告不将该结论外推为本文 Beam 的具体退化证明；具体退化来自 D12 等抽象运行。

## P8｜根动作分配、简单遗憾与 sequential halving

Zohar Karnin, Tomer Koren, Ohad Somekh，2013，**Almost Optimal Exploration in Multi-Armed Bandits**，ICML。

[会议原文页面](https://proceedings.mlr.press/v28/karnin13.html)；[原文 PDF](https://proceedings.mlr.press/v28/karnin13.pdf)。实际阅读问题设置、固定预算算法说明；查看 PDF 第6页 Algorithm 2。

根决策的目标应是停止时选到好的动作，而非累计“探索收益”。但论文的臂及抽样假设不等于不同深度、不同成本且带偏差尾值的续行。因此首版使用同候选、同情景、同完成层的小矩阵；后续 racing 只在配对同层样本上考虑。本文没有实际运行 sequential halving；脚本里的 racing 反例是明确标出的逻辑例子，不是带统计保证的实现。

## P9｜风险尾部不等于场景最坏值

R. Tyrrell Rockafellar, Stanislav Uryasev，2000，**Optimization of Conditional Value-at-Risk**，Journal of Risk，DOI [10.21314/JOR.2000.038](https://doi.org/10.21314/JOR.2000.038)。

[作者原文 PDF](https://sites.math.washington.edu/~rtr/papers/rtr179-CVaR1.pdf)。实际阅读定义部分，查看 PDF 第3、4页；未利用投资组合实验表来推导游戏收益。

CVaR 是针对给定分布的尾部风险量。本文三个人工情景不是估计出的真人分布；采用“设计均值减去小幅下偏差”的透明偏好，而不把它命名为 CVaR、死亡概率或安全置信度。纯最坏值可能再次选择龟缩，本文有独立数值反例及 D_worst 消融。

## P10｜离线价值函数需要数据支持域

Scott Fujimoto, David Meger, Doina Precup，2019，**Off-Policy Deep Reinforcement Learning without Exploration**，ICML。

[会议原始页面与摘要](https://proceedings.mlr.press/v97/fujimoto19a.html)。本轮实际读取摘要，不声称读过完整推导。

该研究提出离线数据分布外动作的估值问题。迁移到本项目，先收集原生同根、多动作、真实成本与实际后续的离线数据，再考虑价值学习；不能把少量手工正例拟合出的分数当作可泛化队友模型。首版无需训练，更不需要运行时语言模型猜牌。

## 读取失败与使用原则

作者 UCT 旧链接、Churchill 作者站的若干 PDF 链接、Nature 正文均未完整取得；这些缺口没有用二手算法博客补成“已读原文”。成功的 PDF 仅查阅上述指定部分，不声称全书/全文全部读完。网页日期、论文年份与游戏源码基线互不替代。所有新策略默认值、尾部估计、情景权重和部署门槛均为本文设计或抽象实验结果，不是这些论文为本项目证明的结论。
