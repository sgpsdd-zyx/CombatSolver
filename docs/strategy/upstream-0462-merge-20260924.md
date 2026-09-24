# 官方 0.46.2 合并与知识收尾

## 当前结论

本分支 `codex/multiplayer-advisor` 从 `fdbe4e6c` 合入官方 `upstream/main` 的 `4bfb4407 / v0.46.2`。官方单人代码、可选 ServerGC 启动配置、在线连接元数据门禁、策略侧栏布局、UI 本地化和对应工具已进入当前源码；多人手动军师、三周期本机贡献目标、无来源伤害折算、最长十四周期和实验边界继续只在 fork 专属路径生效。合并版完成 Release 构建、最小原生验证和本地 Mod 部署；[结构化证据](upstream-0462-merge-20260924-evidence.json)保留运行 ID、输入和范围。

项目版本已同步为 `0.46.2`。这表示源码版本与官方基线同步，不表示 fork 已发布。fork 最近已发布版本仍是 `0.45.1`；本轮未创建 fork 标签、GitHub Release 或 ZIP，也未推送。上一任务的官方 fetch、合并父提交和只读盘点直接复用；中断发生在冲突处理后、构建前，没有重复拉取或改写历史提交。

## 合并取舍

| 文件面 | 处理 | 原因 |
| --- | --- | --- |
| `AGENTS.md`、多人指南、策略与发布规则 | 保留 fork 规则并把官方基线更新为 `4bfb4407 / 0.46.2` | 多人边界、发布授权和交接规则属于本分支现役约束 |
| `CombatSolver.csproj`、`CombatSolver.json` | 吸收官方 0.46.2 配置，并保留既有 `Compile Remove="docs/**/*.cs"` | 归档探针和原型不是生产源；本次没有新增探针排除机制 |
| Runtime/UI/Testing 与 ServerGC 工具 | 吸收官方实现，四个交叠 UI/测试文件保留既有多人接入 | 上游 20 个源码文件中 16 个与官方完全相同；其余为多人显示和测试扩展 |
| `docs/DEVELOPMENT_NOTES.md`、`docs/TEST_MATRIX.md`、索引和发布目录 | 合并两侧现役事实；官方证据单列为上游证据，fork 证据保持独立 | 避免把官方通过项误写成 fork 本轮行为通过，也避免丢失多人证据 |
| `src/UI/English.json` | 保留多人文案并补入官方英文词条，去掉上游四个同值重复键 | 实际解析后的 465 项映射不变；唯一键 JSON 检查不能靠解析器覆盖重复定义蒙混通过 |

本轮 `src/Search/`、`src/Engine/`、`src/Prediction/` 相对 fork 父提交没有变化；两版上游之间这些目录也未变。这里是源码边界证据，不把之前 0.45.0 的单人 92 项对照改称本轮测试。没有修改第三方登记点或引入队友未来动作。

## 直接验证

原始输出根为 `.local/upstream-0462-merge-20260924/`。首份 Release DLL 完成六请求普通批次和两请求 ServerGC 批次；最终 JSON 唯一键检查指出官方词典存在四个同值重复定义，去重后解析映射不变。内嵌资源发生变化，因此最终重新构建、仅复跑受影响的本地化场景并更新本地部署；未重复不受影响的行为检查。实际执行的 UI 输入保存为[本地化夹具](../../coverage/unattended/ui-localization.json)，其余输入沿用现有 coverage 文件，路径记入结构化证据。

| 检查 | 本轮结果 |
| --- | --- |
| Release / 职责边界 | macOS arm64 构建 0 警告、0 错误；Bash `REFACTOR_BOUNDARIES_OK search_files=218` |
| 运行库配置 | `RuntimeGcProfileChecks` 25 项通过；普通原生进程 `Default / serverGc=false / savedNoGc=true / effectiveNoGc=true`；显式环境进程 `Active / serverGc=true / savedNoGc=true / effectiveNoGc=false`，均为 CLR 9.0.7 |
| UI 本地化与路线 | `UI-LOCALIZATION` 465 模板、eng/zhs/zht、遗物归属、名称往返、循环展开/收拢及高亮通过；`ROUTE-ROW-REUSE`、`UI-COMPACT-QOL` 通过 |
| 会话与侧栏 | 普通、ServerGC 两种进程各一次 `UPSTREAM-0450-UI-STATE` 通过，含四项侧栏坐标、窗口保存/恢复、设置页和控制器生命周期 |
| 多人手动边界 | `MULTIPLAYER-MANUAL-LOOP` 四次手动查询，本机/队友各三动作，进入第二回合；首动作完整全队状态相等、0 差异，六次冻结根检查通过，队友未读取建议 |
| 请求隔离与输出 | 多人后同进程 `MULTIPLAYER-EXPERIMENT-INACTIVE` 通过；显式 ServerGC 中同一单人一牌胜利请求通过，完整搜索输出和运行库事件均成功落盘 |
| 发布配置负例 | 直接调用 `ValidatePublicationConnections`，缺 `PresenceEndpoint` 按预期拒绝；没有尝试连接线上服务或进行发布构建 |
| 工具与知识静态检查 | 四个新增/变更 Bash 入口及一个 Python 比较器语法通过；431 份 Markdown 盘点、18 份受影响文档/884 个本地文件链接、11 份 JSON 通过；两侧词条解析值与三份官方发布正文完整保留 |
| 无头实例 | 六请求普通批次、两请求 ServerGC 批次及最终本地化复跑全部 Passed；三个仓库内实例均由启动器成功删除，目录及凭证见结构化证据 |

本机已确认的加载位置为游戏应用包内 `SlayTheSpire2.app/Contents/MacOS/mods/CombatSolver`，来源为原生加载日志和当前目录。最终复制当前 DLL、manifest、`LICENSE`、`THIRD_PARTY_NOTICES.md`，未覆盖其他 Mod；Windows MemoryCleaner 在 macOS 不适用。本地缺少在线服务私有配置，因此这是开发部署，未验证在线端点，也不作为发布包。未启动可见游戏。

## 知识收尾

补回冲突处理中漏掉的五节官方 0.46.x 开发历史，并显式标为上游历史；测试矩阵将官方检查与 fork 本轮结果分开。修正策略索引仍指向 0.44.0 的现役基线，README、多人指南、架构、版本/策略索引统一到本次合并。官方 0.46.x 玩家日志保持原文，版本索引明确它们的来源，未伪装为 fork 发布说明。

| 事实面 | 状态 | 证据 |
| --- | --- | --- |
| 代码 | `changed-and-verified` | 构建、结构边界、八个初始原生请求、去重后的本地化复跑和配置合同；仅限上述覆盖范围 |
| 运行态 | `changed-and-verified` | 同源码 DLL 与三份配套文件已本地部署；可见加载/性能及真实网络仍未验证 |
| 文档 | `changed-and-verified` | 当前入口、上游历史和结构化证据分开；本地链接、JSON、源文件/词条保留核对 |
| 规则 | `changed-and-verified` | 唯一项目规则为根 `AGENTS.md`；全局 Codex 规则为空、无上级或子目录覆盖，保留多人规则并补齐官方版本术语 |
| 记忆 | `generated-read-only` | Codex 生成记忆只读，本轮不写入 |
| 工作区 | `verified-current` | 仅一个工作树、当前多人分支和 `main`；合并及知识改动纳入本任务提交，忽略产物保留；清场单列待决 |

原任务只读盘点枚举 430 份 Markdown，报告在 `/tmp/combatsolver-neat-inventory.txt`；本轮新增归档后的 431 份清单位于原始证据根。规则文件约 29 KB，没有新增第二份架构或记忆。未变化的历史文档不逐篇复审。

## 残留候选

复核现场仍保留，清场为 `pending`，等待用户确认后执行。依据 [neat-freak](../../.agents/skills/neat-freak/SKILL.md) 的“先完成知识收尾和只读清场预览”，本次不删除历史证据：

- `.local/upstream-0462-merge-20260924/`：本轮构建、原生结果及部署凭证，保留给用户复核；可复跑输入与摘要已归档。
- `.local/upstream-sync-20260923/` 及其他历史实验目录：旧官方对照、失败输入和联机实验仍被文档引用，不按目录年龄删除。
- `.local/dotnet-sdk.tar.gz`、`.local/sdk-chunks/`、`.local/sdk-parts/`、`.local/test-sdk-part`：历史 SDK 下载残留，可在另行确认后处理；`.local/dotnet/` 是本轮仍在使用的 SDK，需保留。

根目录尖塔军师独立 ZIP、`releases/` 的已有发布物及归档探针属于 `out-of-scope` 或仍有用途的材料，不列为本轮删除项。三个本轮临时无头实例已按测试入口的强制清理合同删除，不属于待清理历史证据。

Windows/PowerShell、Linux 原生启动器、可见 Steam 排版/鼠标/性能、真实联机、在线端点、完整发布门禁和外部发布为 `out-of-scope`。没有新的策略质量实验，不把无头结果或官方原始测量外推为联机胜率、可见性能或普遍收益。
