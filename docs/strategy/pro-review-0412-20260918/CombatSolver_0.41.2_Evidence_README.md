# CombatSolver 0.41.2 复审证据包

固定提交：`2dc5d15b26f16d89436af0fb98650d8b4cf6b411`。日期：2026-09-18。

两个中文 Markdown 是完整复审与设计；`abstract_checks.py` 是本轮实际执行的标准库抽象实验，不是生产C#或游戏模拟。脚本内注释、报告及结果共同界定可达性和证据限制。

## 复现

```bash
python CombatSolver_0.41.2_abstract_checks.py --out ./abstract-results
# 可选：本轮公开源ZIP解压后，补六个源码子串一致性检查。
python CombatSolver_0.41.2_abstract_checks.py --out ./abstract-results --source ./CombatSolver-0.41.2
```

Python3.10+，无第三方依赖；不联网、不修改源码，只在--out指定目录写两份结果文件。

本轮运行Python3.13.5，18项抽象断言通过。840排列与216三元为本轮独立标量实验，另有55,296组三元关系检查；不是源码记录的历史生产C#合同重跑。没有dotnet/游戏DLL，没有本轮原生实际差分或双端联机。

`integrity.json` 和 `original_manifest.json` 证明本次解压的2017文件内容未变；这是文件校验，不是从Git对象库重新计算提交。`read_ranges.jsonl` 记录部分定点行号读取，不代表全部2017文件逐行审计；完整读/未读范围在结论第8.2节。`archive_diff.json` 仅用于0.41.1/0.41.2包间核对，不把旧代码当成本轮基线。

未实施、推送、创建版本或发布。没有把源码ZIP重复装入本证据包。
