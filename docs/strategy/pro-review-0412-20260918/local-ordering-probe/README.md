# 0.41.2 排序入口最小反例

此目录保存本轮独立本地实验的原始源码、真实输出与执行记录。它直接调用已发布构建中的 `PrepareMultiplayerFinalCandidates`、`MultiplayerFactsAt`，没有重新实现比较算法。完整解读见[本地复核](../local-review.md)。

| 材料 | 用途 |
|---|---|
| [Program.cs.txt](Program.cs.txt) | 原始探针源码；保留 `.txt` 后缀，避免被主项目的默认 C# 编译扫描纳入 |
| [OrderingProbe.csproj.txt](OrderingProbe.csproj.txt) | 探针项目；引用仓库当前已构建的 DLL，复用本地依赖路径 |
| [output.json](output.json) | 一次成功执行的标准输出，5 组观察及控制 |
| [execution-record.json](execution-record.json) | 固定基线、构建警告、命令与证据范围 |

## 证据边界

生产 DLL 来自 `2dc5d15 / 0.41.2` 已有成功发布构建；本轮确认 `9885a75` 相对发布提交的核心源码与输入没有变化。探针只创建冻结的候选标量，通过反射接入现有私有入口；快照没有模拟器，动作链用于满足排序入口的动作数据读取，不是一条在游戏中合法执行过的路线。

因此它证明这些输入在生产比较器中的排序结果，不能证明实际游戏可达性、搜索出现频率、多人胜率、原生/模拟状态等价或修复后的收益。5 组结果是问题观察与控制，不是 5 项已修复功能。探针构建有 1 条 `CS8625` 警告：合成节点未提供比较器不读取的 `CombatProgressState`；执行退出码为 0。

## 复跑条件

在当前仓库将两个 `.txt` 文件按原名复制到 `.local/mp-pro-review-0412-20260918/ordering-probe/`，移除末尾 `.txt`。要求 .NET 9、有效的仓库 `local.props` 和与待核验源码对应的生产 DLL；不需要启动游戏。项目使用已存在的 `OfflineSearchHarness` 友元程序集名称访问内部类型，不等于执行了离线搜索宿主。

```bash
dotnet build .local/mp-pro-review-0412-20260918/ordering-probe/OrderingProbe.csproj -c Release --nologo
dotnet .local/mp-pro-review-0412-20260918/ordering-probe/bin/Release/net9.0/OfflineSearchHarness.dll
```

本轮使用仓库内 `.local/dotnet/dotnet`。探针以当前错误/政策结果为断言；将来行为修复后失败是预期信号，须改成相应生产回归测试，不能改期望来继续声称本轮反例成立。原始记录保持不动。
