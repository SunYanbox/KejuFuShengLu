# Implementation Plan: 存档体系

**Branch**: `004-save-system` | **Date**: 2026-10-11 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-save-system/spec.md`

## Summary

交付规格书 §16 **逻辑轨 ④「存档体系（MessagePack/JSON/备份）」**：把 001~003 的全部推演状态**逐字段**
落到磁盘、再逐字段读回，并补齐「什么时候存」与「存在哪里」两件事。

- **US1（能存能读）**：整份状态 ⇄ 磁盘的完整往返——默认二进制 MessagePack（`.dat`）、可选
  System.Text.Json（`.json`），两种格式承载**同一信息集**；读回后继续推演 MUST 与「从未落盘」逐字段一致。
- **US2（什么时候存）**：每回合结算**完整成功后覆盖**当前档、每 **3** 回合在其后留一份覆盖式备份、
  退出时再存一次；写入具**原子性**（要么旧档、要么新档）。
- **US3（玩家的东西）**：存档名支持中文、受 Windows 实际非法字符集与保留名约束；同名未确认不覆盖；
  改档名**不**变存档标识（UUID）与游戏内容；存档根可配置（默认可执行文件旁 `saves/`）。

技术路线（沿用 001/002/003 的分层口径）：**状态与不变量归 `KFL.Core`，落盘实现与格式归
`KFL.Infrastructure`，节奏决策与「结算 → 保存 → 备份」的编排归 `KFL.Rules`**。

- `KFL.Core`：**不新增任何领域字段**（本特性零字段增删），只加最小 `internal` 复原面
  （`Family` 的外来者簿记进出两法、`FamilyEconomy.RestoreFamine`）与 `InternalsVisibleTo("KFL.Infrastructure")`。
  > **回写补注（T048）**：原文此处写「两处最小 `internal` 复原面」并声称「零形状变更」，实现期
  > 出现**一处**例外——`FamineState.Stage`/`ElapsedMonths` 的访问器由 `private set` 放宽为
  > `internal set`。理由与边界见 `tasks.md` 「回写义务登记」第 12 行：`TransitionTo` 把计时钉死为
  > 1、`Tick` 只能逐月步进，二者都无法表达存档里任意的「(阶段, 已历时月数)」组合。公开面**未变**
  > （对外仍是只读），不新增不变量。
- `KFL.Infrastructure`：`ISaveService` 首次落地（001 契约二 §5 的「等各自阶段带着真实需求回来定义」
  即此处），新增 `Save/` 目录承载命名校验、原子写、目录索引、两套编解码、瞬态 DTO 与逐字段映射器。
- `KFL.Rules`：新增 `SaveCadencePolicy`（`3` 与纪元 `(1,1)` 的单点）与 `TurnRunner`（每回合
  「结算 → 覆盖保存 → 命中 3 的倍数则备份」）——**不动** `MonthlySettlementEngine`（契约三 §1 的
  「MUST NOT 存在」清单明列「存档写入」）。

本特性**不做**任何界面（新建/读档界面、覆盖确认弹窗、设置面板属**界面轨 U1/U5**）、不做成就持久化
（`achievements.json` 与 UUID 归属属**逻辑轨 ⑧**）、不做旧格式迁移（Out of Scope）、**不引入系统时钟**
（无写入时间戳；001 R-04 的待决项由此结清）。

## Technical Context

**Language/Version**: C# 14 / .NET 10（沿用 001~003 基线；`global.json` 仍只锁 .NET 10 版本带）。
非 UI 三层 `net10.0`、UI 两层 `net10.0-windows`；**不新增工程**，仍六工程。

**Primary Dependencies**: **新增 1 个 NuGet 依赖：`MessagePack 2.5.305`**（仅
`KFL.Infrastructure`；对应规格书 §1/§14 指定的「二进制 = MessagePack」与章程依赖基线内的既有包）。
**已实测**（本机缓存含 `messagepack 2.5.305` 与 `2.5.192`，资产含 `net6.0`，兼容 `net10.0`，离线可还原）：

| 实测项 | 结果 |
| --- | --- |
| 探针工程（`net10.0` + `TreatWarningsAsErrors`）引用 `MessagePack 2.5.305` | **0 警告 0 错误**（分析器/源生成器在 .NET 10 SDK 上正常加载，无 CS8032） |
| 连续 `[Key(0..n)]` 的线上形状 | **array 格式**（首字节 `0x94`）⇒ 位置式，成员缺失即结构错误 |
| `decimal`（`1.000m`）往返 | `decimal.GetBits(x)[3]` 前后相同 ⇒ **scale 逐位保留** |
| 手写头（`MessagePackWriter.WriteArrayHeader(3)` + `Write(int)` + `Write(string)`）与 `MessagePackReader.Consumed` 切片反序列化 | 可用（实测 `headerLen == offset == 41`，切片后主体还原正确） |
| 截断（头完好、主体不足） | `剩余字节(4) < 声明长度(12)` ⇒ **可确定为截断** |
| `MessagePackSecurity.UntrustedData` | 可用 |
| JSON `[JsonRequired]` 缺成员 / 非法枚举名 / 截断 | `JsonException`（消息含 `missing required properties`）/ `JsonException` / `JsonException` |
| JSON `JsonStringEnumConverter` + `UnsafeRelaxedJsonEscaping` | 枚举落成 `"Hard"`；中文按原样落盘（不转义成 `\uXXXX`） |
| JSON `decimal`（`1.000m`）往返 | scale 逐位保留（同二进制） |

JSON 走 BCL 的 `System.Text.Json`（**无**新增依赖）。其余基线包（CommunityToolkit.Mvvm / Serilog /
LiveChartsCore）在本特性仍无消费者；Bogus 既有引用不动。

**Storage**: 文件系统（唯一的数据存储）。**单文件、全量、不分块、不压缩**（§14 的 Q2 裁决；FR-021）。
默认根 = 可执行文件旁 `saves/`（`AppContext.BaseDirectory`），备份 = `<根>/backups/`（Q1 裁决）。
`KFL.Core` 与 `KFL.Rules` **零**文件系统访问（G-07 零违规）。

**Testing**: xUnit v2 + VSTest，`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false
-p:_MSTestEnableParentProcessQuery=false`（受限宿主的两个已知坑见 `AGENTS.md`）。
新增 `tests/KFL.Tests/Infrastructure/{SaveTestSandbox,SaveServiceTests,SaveFailureTests,
AtomicWriteTests,SaveFormatContractTests}.cs` 与 `tests/KFL.Tests/Rules/SaveCadenceTests.cs`；
扩写 `tests/KFL.Tests/Fixtures/`（存档夹具：全字段类别的构造点）。全部测试**使用临时目录**，
**0** 处依赖 `KFL.App`（其 TFM 为 `net10.0-windows`，物理上不可引用）。

**Target Platform**: Windows（WPF 表现层，本特性不涉及）。存档实现不引用任何平台类型。

**Project Type**: 桌面应用（WPF + MVVM）+ 分层类库，单仓库六工程（结构不变）。

**Performance Goals**: 无运行时性能目标（§3：逻辑推进仅由用户输入驱动）。可衡量的只有门禁本身
（构建零警告零错误、测试全绿）与 SC-001~SC-010 的可断言性——其中 SC-010 要求**100 年
（1200 回合）**的存档完整可写可读、账本条目丢失数 = 0（预计数万条、单一文件下 < 2 MB）。

**Constraints**:
- 章程原则 II：`KFL.Core`/`KFL.Rules`/`KFL.Infrastructure` 内 MUST NOT 出现 `DateTime.Now`、`Random`、
  文件系统、网络（G-07 静态断言，14 个禁用 token；扫描范围含 `tests\KFL.Tests\Infrastructure\` 与
  `Rules\`）——故存档实现的文件系统行 MUST 逐行 `// arch-guard:allow <理由>`（FR-019；SC-008）。
- 章程「技术栈与工程约束」：金额一律以**文**为单位的 `decimal`，1 贯 = 1000 文；运行时路径 MUST 可配置，
  存档默认 `saves/`。
- 规则数值 MUST 集中在 `KFL.Rules/Config/`：本特性新增的唯一数值是 `BackupIntervalTurns = 3`
  与纪元 `(1, 1)`（逐条出自 §14 与 §3）。
- 存档实现 MUST NOT 读墙钟、MUST NOT 落盘写入时间戳（FR-018；R-23）。
- 依赖边不新增：`KFL.Core ← KFL.Infrastructure ← KFL.Rules`（G-05 不变）。
- `.slnx` 内 MUST 仍恰好六个 `<Project>`（G-02）；`tests/KFL.Tests/KFL.Tests.csproj` 的
  `_MSTestEnableParentProcessQuery` MUST 保持**注释态**提交。

**Scale/Scope**（预估，实测后按实现现状回写）：

> **回写补注（T048，实测值）**：以下计数按**实现现状**修正，原预估数字保留在括注里。
> `KFL.Core` 改 **3** 个文件（`Family`、`FamilyEconomy`、`FamineState`〔原预估 2 个，漏了
> `FamineState` 的访问器放宽〕）、增 **2** 个（`AssemblyInfo.cs`、`Entities/OutsiderBookkeeping.cs`），
> **0** 个领域字段增删；`KFL.Infrastructure` 增 **18** 个文件（`Abstractions/` 3 个：
> `ISaveService`、`SaveFormat`、`SaveSlot`；`Save/` 8 个：`SaveOptions`、`SaveFileException`、
> `SaveFileHeader`、`SaveFileFormat`、`SaveName`、`AtomicFileWriter`、`SaveCatalog`、`SaveService`；
> `Save/Serialization/` 7 个：`ISaveFormat`、`MessagePackSaveFormat`、`JsonSaveFormat`、
> `SaveStateDto`、`SaveStateValueDtos`、`SaveStateMapper`、`SaveStateValidator`）并改 csproj **1** 处
> （原预估约 17 个）；`KFL.Rules` 增 **3** 个（`Config/SaveCadencePolicy.cs`、`Save/TurnRunner.cs`、
> `Save/TurnOutcome.cs`）、改 **1** 个（`Config/GameConfig.cs` 的转发组）；测试增 **9** 个文件
> （`Infrastructure/` 7 个：`SaveFixtures`、`SaveTestSandbox`、`SaveStateComparer`、`SaveServiceTests`、
> `SaveFormatContractTests`、`SaveFailureTests`、`AtomicWriteTests`；`Rules/` 2 个：`RecordingSaveService`、
> `SaveCadenceTests`）、改 **0** 个既有夹具（原预估「增 6 个、改 2 个」；实测把新夹具独立成
> `SaveFixtures`，`FamilyFixtures`/`EconomyFixtures` 一行未动）；`.gitignore` 改 1 处；
> **0** 个界面功能、**0** 行界面代码。测试总数 421 → **489**（新增 68 条，0 skipped）。

原预估（保留为当时事实）：`KFL.Core` 改 **2** 个文件（`Family`、`FamilyEconomy`）、增 **2** 个（`AssemblyInfo.cs`、
`Entities/OutsiderBookkeeping.cs`），**0** 个领域字段增删；`KFL.Infrastructure` 增 **约 17** 个文件
（`ISaveService` 与存储侧值类型 5 个、`Save/` 12 个含 4 个 DTO 文件）并改 csproj **1** 处；
`KFL.Rules` 增 **3** 个（`Config/SaveCadencePolicy.cs`、`Save/TurnRunner.cs`、`Save/TurnOutcome.cs`）、
改 **1** 个（`Config/GameConfig.cs` 的转发组）；测试增 **6** 个文件、改 **2** 个（存档夹具）；
`.gitignore` 改 1 处；**0** 个界面功能、**0** 行界面代码。

> **无跨特性形状变更**：本特性**不**增删 `KFL.Core` 领域字段、**不**改任何上游步骤序列
> （见「回写义务清单」的说明）。故 AGENTS.md「铁律」要登记的是**声明与验证义务**的
> 回写，而非字段级回写。
>
> **回写补注（T048）**：上句原文为「**不**改任何已有签名」。实现期有一处例外——
> `KFL.Core/Entities/FamineState.cs` 的 `Stage`/`ElapsedMonths` 由 `private set` 放宽为
> `internal set`（对外只读性不变，不新增不变量）。该处已在 `tasks.md`「回写义务登记」
> 第 12 行登记，并已在本节「技术路线」的 `KFL.Core` 一条下补注。

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 章程条款 | 本计划如何满足 | 结论 |
| --- | --- | --- |
| **I. .NET 10 单一运行时基线** | 不新增工程、不改任何 `TargetFramework`；`global.json` 不动；新增依赖 `MessagePack 2.5.305` 含 `net6.0` 资产（兼容 `net10.0`）、仍在维护、且是章程依赖基线与规格书 §1/§14 的既有指定项；JSON 走 BCL 的 `System.Text.Json`（零新依赖） | ✅ |
| **II. 分层解耦与单向依赖** | **状态与不变量**归 `KFL.Core`（本特性零字段变更，只加两处最小 `internal` 复原面）；**落盘实现、格式、命名、原子写**归 `KFL.Infrastructure`（规格书 §2 指定的层）；**节奏数值与编排**归 `KFL.Rules`；`Core` 不引用 `Rules`，`Infrastructure` 不引用 `Rules`（G-05 不变）；`Core`/`Rules` 零文件系统访问（G-07） | ✅ |
| **III. WPF + MVVM 表现层契约** | 无 ViewModel、无 View、无 code-behind 改动；`KFL.Presentation` 与 `KFL.App` **一行不动**（界面属界面轨 U1/U5，spec 的 Out of Scope 已逐项排除） | ✅（N/A 已说明） |
| **IV. 单测优先与结果确定性** | 两种格式的逐字段往返、≥14 类字段类别、错位/缺字段/截断/版本四类失败、原子性注入、12 回合节奏、含归档成员的集合、`decimal` 逐位（scale）、1200 回合体量——逐条独立断言；无墙钟、无全局随机（`SaveCadenceTests` 用内存替身，`SaveServiceTests` 用临时目录）；测试与实现同批提交 | ✅ |
| **V. 约定式提交与中文提交信息** | `feat(core)`（两处 `internal` 复原面与 `InternalsVisibleTo`）、`build(infrastructure)`（MessagePack 依赖）、`feat(infrastructure)`（`ISaveService` + `Save/`）、`feat(rules)`（节奏策略与 `TurnRunner`）、`test(...)`、`docs(...)`（上游活工件的回写与规格书 §14 已完成的三条裁决）各起一次；不混装 | ✅（流程约束） |
| **VI. 调试通道不复制规则** | 本特性不建控制台（界面轨 U4）；`ISaveService` 是**唯一**的存档入口，`TurnRunner.SaveOnExit` 与自动保存复用**同一个** `Save` 调用（FR-012），将来 U1/U4 只能调它；`3` 与纪元只在 `KFL.Rules/Config/` 声明，`Infrastructure` 内 MUST NOT 出现副本 | ✅ |
| **技术栈与工程约束** | `.slnx` 唯一、六工程不变；无游戏引擎；金额以文为单位的 `decimal` 且**原值原样**入档；新增数值集中 `KFL.Rules/Config/`；存档与备份路径可配置且默认 `saves/`；无日志代码 | ✅ |
| **开发工作流与质量门禁** | 严格对位 §16 逻辑轨 ④（本特性即该条，非跨阶段提前动工）；三项门禁可执行；复杂度逐项论证（见 Complexity Tracking） | ✅ |
| **需求真源** | 三项用户裁决（Q1/Q2/Q3，2026-10-11）已回写规格书 §14 并由本计划继承；`plan.md`/`research.md`/`data-model.md`/`contracts/` 逐条标注规格书章节号；本特性**未自创任何影响平衡的数值**（唯一新增数值 `3` 与纪元 `(1,1)` 逐条出自 §14/§3） | ✅ |

**设计后复核（Phase 1 之后）**：✅ 无新增违规。`data-model.md` 与三份契约未引入任何需要放宽章程的
结构：仍是六个工程、无新接缝层、无跨层引用、无界面代码；契约九/十/十一只描述既有分层内的接口、
字节形状与调用次序；新增依赖已在 Phase 0 用**实测**证据论证（R-24）。

### 继承的用户裁决（2026-10-11）

**Q1~Q3**（`$speckit-specify` 阶段裁决，规格书 §14 已回写）：

| 编号 | 议题 | 裁决 | 落点 |
| --- | --- | --- | --- |
| **Q1** | 备份落点与内容时点 | 落点 = 可配置根下 `backups/`，文件名 = 存档名 + **同扩展名**；时点 = 该回合自动保存**完成之后**复制刚写入的内容；覆盖式、每档一份 | FR-011；[契约十一 §2](./contracts/save-cadence.md)；[契约九 §7](./contracts/save-service.md) |
| **Q2** | 账本的落盘组织 | **单文件、全量、不分块、不压缩**；统计一律由账本聚合、不另存汇总值 | FR-021；[契约十 §1](./contracts/save-format.md)；[data-model 不变量 8](./data-model.md) |
| **Q3** | 存档名过滤字符集 | 按 Windows **实际**非法集 `\ / : * ? " < > \|`（9 个）过滤，`-` 与中文放行（§14 原文的 `-` 认定为 `*` 的笔误） | FR-007；[契约九 §4](./contracts/save-service.md)；R-13 |

**Q4~Q8**（`$speckit-clarify` 复扫阶段裁决，本轮新增；已回写 [spec.md](./spec.md) 的 FR / Edge Cases / SC / Assumptions）：

| 编号 | 议题 | 裁决 | 落点 |
| --- | --- | --- | --- |
| **Q4** | 「当前档」的来源（R-17 的 `Advance(GameState)` 与步骤 ③ 的 `slot` 不自洽） | 当前槽位（档名 + 格式）由**装配期注入**并由 `TurnRunner` 构造参数或存档服务持有；`Advance`/`SaveOnExit` 不逐次接收；MUST NOT 进 `GameState`、MUST NOT 落盘 | FR-010/FR-012；[契约九 §1 条款 7](./contracts/save-service.md)；[契约十一 §2 条款 6](./contracts/save-cadence.md) |
| **Q5** | 改名 / 换格式后旧名或旧扩展名备份的处置 | **同步处置**：改名时备份一并改名；换格式时新当前档写成功后再移除旧格式备份；「`backups/` 下同一存档标识恒 1 份且与主档同名同扩展名」在操作完成时即成立 | FR-009/FR-011；SC-003；[契约九 §5~§7](./contracts/save-service.md)；[data-model §6.3](./data-model.md) |
| **Q6** | 结算成功后写盘失败时的内存语义 | **保留**已推进的内存状态并把失败向上传播；MUST NOT 回滚（回滚需结算前快照 = FR-020 禁止的第二份真源）、MUST NOT 静默吞；「已推进未落盘」为允许的中间态，下次保存合并写入 | FR-010；Edge Cases；[契约十一 §2 条款 7](./contracts/save-cadence.md)；[data-model §6.4](./data-model.md) |
| **Q7** | 同名判定的大小写口径 | **大小写不敏感**（与 Windows 文件系统语义一致，也沿用保留设备名 `CON`/`con` 的既有口径）；保存保留输入写法；不做 Unicode NFC 规范化 | FR-008；SC-005；[契约九 §4 条款 1.1](./contracts/save-service.md) |
| **Q8** | 回合计数与「任意年月」的适用面 | 推演进程的 `CurrentDate` **恒自 1 年 1 月起算**（0/负数年份只在角色的**出生年月**等成员字段）；回合数恒为正、无需为负年份设计备份节奏；Edge Case 的「任意年月」收窄为成员年月字段。**不改任何数值** | FR-011；Edge Cases；[契约十一 §1 条款 1](./contracts/save-cadence.md)；R-16 |

> Q4~Q8 属**声明与验证义务**类回写（零 `KFL.Core` 字段增删、零签名变更）；本轮已同步
> `research.md`（R-16/R-17/R-18/R-19/R-25 + 追加节）、`data-model.md`（§6.1/§6.3/§6.4）、
> `contracts/save-service.md`（§1/§4/§5/§6/§7）、`contracts/save-cadence.md`（§1/§2/§5），
> 并 MUST 在 `tasks.md` 登记同步范围（AGENTS.md 的「铁律」；spec 的 `Assumptions` Q4~Q8 段
> 已声明同一义务）。

## 回写义务清单（AGENTS.md「铁律」+ spec Assumptions 的落地）

**先说结论**：本特性**没有**对任何上游 `KFL.Core` 类型或既有契约做**形状变更**——零字段增删、
零签名变更、零步骤序列插入（落盘被刻意放在结算**之外**的一层，R-17）。因此本清单登记的是
**声明与验证义务**的回写：哪些「待阶段④ 处理」的登记被**结清**、哪一个「接口待定义」被**落实**、
哪一处「无验证对象」的断言**首次获得可验证对象**。

| # | 目标工件（活工件） | 回写内容 | 依据 |
| --- | --- | --- | --- |
| 1 | `科举浮生录规格书.md` §14 | **已完成（2026-10-11）**：该节末尾已追加「§17 裁决回写」段记入 Q1/Q2/Q3；原始条目文本按 §16.2 的历史事实口径保留不改写 | spec Assumptions 1 |
| 2 | `specs/001-core-skeleton/spec.md` FR-012（L157–160） | 「改档名不变性**属阶段④**、001 内无可验证对象」→ 标注为**已由 004 交付并验证**（指向 [契约九 §6](./contracts/save-service.md) 与 SC-004 的断言） | spec Assumptions 2 |
| 3 | `specs/001-core-skeleton/data-model.md` §2.3 的 `Id` 行（L261）与 FR 对照表（L313） | 同 #2 的两处「改档名不变性属阶段④验证」措辞 → 标注「已由 004 交付并验证」 | spec Assumptions 2 |
| 4 | `specs/001-core-skeleton/contracts/injection-seams.md` §5（L80–82）与 §2 的末句（L47–48） | §5：`ISaveService` 由「等各自阶段带着真实需求回来定义」→ 改为**已由 004 定义**并指向 [契约九](./contracts/save-service.md)；§2：把「存档时间戳属阶段④」标注为**已由 004 复核为「无该需求」**（R-23） | spec Assumptions 3；R-23 |
| 5 | `specs/001-core-skeleton/research.md` R-03（UUID 碰撞，L72–74 与未决事项 L383） | 标注为**已由 004 结清**：本特性以 `SaveIdentityConflictException` 检测并拒绝同根同 UUID 并存（[契约九 §5](./contracts/save-service.md)）；「调试控制台重置种子」这一原始触发路径仍属 U4，但存档侧的**检测点已存在** | spec Assumptions 4；001 R-03 |
| 6 | `specs/001-core-skeleton/research.md` R-16（账本落盘体量与分块，L363–364 与未决事项 L389） | 标注为**已由 004 结清**：结论 = 单文件全量、不分块、不压缩（Q2 裁决），并以 1200 回合（SC-010）实测复核 | spec Assumptions 4；001 R-16 |
| 7 | `specs/001-core-skeleton/research.md` R-04 的未决事项行（L393）与 `plan.md`（L39） | 「系统时钟抽象：阶段④（存档写入时间戳）首次需要时引入」→ **结清为「不需要」**：本特性不落盘时间戳，故 MUST NOT 引入墙钟接缝（R-23） | spec Assumptions（「不落盘写入时间戳」）；R-23 |
| 8 | `specs/002-monthly-settlement-economy/research.md` 未决事项表（L441） | 「流水账落盘体量与按年分块：阶段④ 实测后决定」→ 标注**已由 004 结清**（同 #6） | spec Assumptions 4 |
| 9 | `specs/002-monthly-settlement-economy/contracts/ledger.md` §6（L73–74）与 §7（L78–79） | **只补注**：把「阶段④ 落盘时实测后再决定（001 R-16 已登记该未决事项）」标注为**已由 004 按 Q2 裁决结清**；§7 的「存档序列化格式（阶段④）」改为**已由 004 交付**并指向 [契约十](./contracts/save-format.md)。**不改**判定句本身（它记录的是当时的取舍） | spec Assumptions 4/5；AGENTS.md 的「活工件只在含义已改变处补注」 |
| 10 | `specs/002-monthly-settlement-economy/data-model.md`（L7）、`plan.md`（L43–44）、`research.md`（L283–285） | 「存档落盘属阶段④，**此时增删字段无迁移成本**」属**当时事实**：按 §16.2 的迁移口径**保留原文、MUST NOT 回改**；只在「含义已改变」处补注一句「004 之后，落盘形态已固定为 [契约十](./contracts/save-format.md) 的版本 1，跨版本变更须走 §14 的版本闸门（拒绝而非迁移）」 | spec Assumptions 5 |
| 11 | `specs/004-save-system/tasks.md` | 登记上述回写范围（同步了哪些文件、同步到哪里），供后续审计核对 | spec Assumptions 6；AGENTS.md 的「铁律」 |

> **不改的**：`tasks.md`（001~003 的既有条目）、`implementation-notes.md`、既有 `checklists/` 属
> **历史记录类工件**，MUST NOT 回改（规格书 §16.2；AGENTS.md）。
> **003 的 Out of Scope 行**（`spec.md`/`plan.md`/`data-model.md`/`contracts/*`/`quickstart.md` 里
> 「存档落盘属逻辑轨 ④」）语义**未变**（004 正是逻辑轨 ④），故无需改动。

## Project Structure

### Documentation (this feature)

```text
specs/004-save-system/
├── plan.md                        # 本文件
├── spec.md                        # 规格（Q1~Q3 已裁决并回写规格书 §14）
├── research.md                    # 阶段 0：25 项抉择（R-01~R-25，全部有结论；含依赖实测表）
├── data-model.md                  # 阶段 1：文件布局 / 76 个线上成员 / 存储侧与规则侧类型 / 状态转移 / 需求映射
├── quickstart.md                  # 阶段 1：8 个可运行的人工验收场景
├── contracts/
│   ├── save-service.md            # 契约九：ISaveService 入口、失败矩阵、原子性、命名、唯一性、改名、备份
│   ├── save-format.md             # 契约十：头记录、DTO 键表、编解码选项、版本闸门、依赖与验证物
│   └── save-cadence.md            # 契约十一：回合数定义、TurnRunner 编排次序、数值登记、SC-003 锚点
└── checklists/requirements.md     # 规格校验（$speckit-specify 阶段完成）
```

> 001 的 `contracts/architecture-guard.md`、`contracts/injection-seams.md`、002 的
> `contracts/{monthly-settlement,ledger,config-registry}.md`、003 的
> `contracts/{new-game-setup,official-career,config-registry}.md` 是**跨阶段契约**，本特性**继承**并
> 做**三处最小修订**：契约二 §5（`ISaveService` 已定义）与 §2 末句的时钟注（R-23）、
> 契约四 §6/§7（R-16 未决项结清与存档格式已交付）。**契约三（月度结算的七步序列）一字不动**。

### Source Code (repository root)

```text
KejuFuShengLu.slnx                       # 不动（仍六工程）
global.json / Directory.Build.props      # 不动
.gitignore                               # 【改】新增 saves/（FR-023；R-20）

src/
├── KFL.Core/                            # net10.0
│   ├── AssemblyInfo.cs                  # 【新】[assembly: InternalsVisibleTo("KFL.Infrastructure")]（R-03）
│   ├── Entities/
│   │   ├── Family.cs                    # 【改】新增 internal 簿记捕获/复原（R-03）
│   │   ├── FamilyEconomy.cs             # 【改】新增 internal RestoreFamine（R-03）
│   │   ├── FamineState.cs               # 【改】Stage/ElapsedMonths 由 private set 放宽为 internal set（T045 登记第 12 行）
│   │   └── OutsiderBookkeeping.cs       # 【新】internal sealed record（两个 PersonId 集合）
│   └── ...                              # 其余一行不动（零领域字段变更）
├── KFL.Infrastructure/                  # net10.0
│   ├── Abstractions/
│   │   ├── ISaveService.cs              # 【新】契约九 §1（001 契约二 §5 的「待定义」即此处）
│   │   ├── SaveSlot.cs                  # 【新】readonly record struct（Name + Format，默认 Binary）
│   │   └── SaveFormat.cs                # 【新】enum { Binary = 0, Json = 1 }
│   ├── Save/
│   │   ├── SaveOptions.cs               # 【新】RootDirectory（null ⇒ 默认 saves/）+ DefaultFormat（命名空间按契约九 §1 的代码围栏写作 KFL.Infrastructure.Abstractions）
│   │   ├── SaveFileException.cs         # 【新】Kind 五值 + 三个专用冲突/访问异常（R-07）
│   │   ├── SaveFileHeader.cs            # 【新】readonly record struct（版本 / 长度 / 标识）
│   │   ├── SaveFileFormat.cs            # 【新】Version = 1、扩展名、备份目录名、编解码选项单点
│   │   ├── SaveName.cs                  # 【新】名称口径唯一真源（R-13）
│   │   ├── AtomicFileWriter.cs          # 【新】临时文件 + 同卷替换 + 测试用写入钩子（R-14）
│   │   ├── SaveCatalog.cs               # 【新】目录枚举 / 存在性 / 只读头的 SaveId 探针（R-18）
│   │   ├── SaveService.cs               # 【新】ISaveService 的实现（装配以上四者与两个格式）
│   │   └── Serialization/
│   │       ├── ISaveFormat.cs           # 【新】Write(Guid saveId, SaveStateDto) / ReadHeader(bytes) / Read(bytes)
│   │       ├── MessagePackSaveFormat.cs # 【新】R-05/R-09（手写头 + 数组格式主体）
│   │       ├── JsonSaveFormat.cs        # 【新】R-05/R-08（手写头 + 缩进 JSON 主体）
│   │       ├── SaveStateDto.cs          # 【新】主体根 DTO + 家族/人物/经济子 DTO（契约十 §4）
│   │       ├── SaveStateValueDtos.cs    # 【新】天赋 / 计时 / 功名记录 / 资金 / 贷款 / 资产 / 账本 / 饥馑 DTO
│   │       ├── SaveStateMapper.cs       # 【新】逐字段双向映射（R-02、R-12；data-model §3.11 的次序）
│   │       └── SaveStateValidator.cs    # 【新】九条一致性校验（R-04）
│   └── KFL.Infrastructure.csproj        # 【改】PackageReference MessagePack 2.5.305
├── KFL.Rules/                           # net10.0
│   ├── Config/
│   │   ├── SaveCadencePolicy.cs         # 【新】BackupIntervalTurns = 3、纪元 (1,1)、CompletedTurnsAt、ShouldBackup
│   │   └── GameConfig.cs                # 【改】新增 Save 组转发（仍零字面量）
│   ├── Save/
│   │   ├── TurnRunner.cs                # 【新】结算 → 覆盖保存 → 命中 3 的倍数则备份（R-17）
│   │   └── TurnOutcome.cs               # 【新】一次回合的增量快照（MUST NOT 写回 GameState）
│   └── Settlement/                      # 一行不动（契约三 §1 的「MUST NOT 存在」清单含存档写入）
├── KFL.Presentation/                    # 不动（界面轨 U2/U3）
└── KFL.App/                             # 不动（界面轨 U1）

tests/
└── KFL.Tests/                           # net10.0
    ├── Architecture/                    # 不改（G-07 扫描范围与豁免清单均不变）
    ├── Fixtures/                        # 不改（FamilyFixtures / EconomyFixtures 一行未动）
    ├── Infrastructure/
    │   ├── SaveFixtures.cs              # 【新】全字段类别的存档夹具（在册/已亡/外嫁/待阙/致仕/患病/饥馑/贷款…）
    │   ├── SaveStateComparer.cs         # 【新】逐字段等价断言（≥14 类；含 decimal 逐位）
    │   ├── SaveTestSandbox.cs           # 【新】唯一的临时目录/文件操作辅助类（豁免行收敛于此；R-21）
    │   ├── SaveServiceTests.cs          # 【新】US1/US2/US3：两格式往返、命名、覆盖、UUID 唯一性、格式切换、改名、真文件节奏
    │   ├── SaveFailureTests.cs          # 【新】US3 AS7 / SC-007：五类失败各自可识别
    │   ├── AtomicWriteTests.cs          # 【新】SC-006：写入中途失败注入 + 备份中断
    │   └── SaveFormatContractTests.cs   # 【新】契约十：头三成员、键表、枚举写法、信息集等价、1200 回合
    └── Rules/
        ├── RecordingSaveService.cs      # 【新】内存替身（不碰文件系统），供节奏测试记录调用
        └── SaveCadenceTests.cs          # 【新】US2：12 回合节奏、开关、退出保存复用入口、回合数递增
```

> **结构树补注（T048，按实现现状修正）**：原树把 `SaveFixtures.cs` 列在 `Fixtures/` 下，
> 实测落在 `Infrastructure/`（与 `SaveTestSandbox`、`SaveStateComparer` 同处，便于共用沙箱）；
> 另补上原树漏列的 `SaveStateComparer.cs`、`Rules/RecordingSaveService.cs` 与
> `Core/Entities/FamineState.cs`（【改】）；`ISaveFormat` 的方法签名按实现修正为
> `Write(Guid saveId, SaveStateDto payload)`——`PayloadLength` 恰是主体字节数，故**不能**由调用方
> 预先拼头（否则会引入「声明长度 ≠ 实际字节」，而截断判定正建立在二者的差异上）。

**Structure Decision**: 工程划分与依赖方向**不动**（规格书 §2 六个工程）。新增代码按**关注点**落在
既有目录语义内：`Core/Entities`（两处最小复原面与其簿记类型）、`Infrastructure/Abstractions`
（跨层接口与存储侧值类型，规格书 §2 指定）、`Infrastructure/Save/**`（文件系统与格式的全部实现）、
`Rules/Config`（**全部**规则数值）、`Rules/Save`（回合编排）。

`Infrastructure/Save/` 与 `Rules/Save/` 是两个新目录，理由与替代方案见 Complexity Tracking。
DTO 与映射器放在 `Save/Serialization/` 之下，是为了让「契约十的键表 ←→ 代码」在目录结构上就一一对应
（键表断言的对象就是这几个文件）。

## Complexity Tracking

> 本特性无章程违规项。下面登记**六项新增抽象/新增面**的论证（章程「复杂度 MUST 被论证」）。

| 新增抽象 | 为什么现在需要 | 更简单的替代为何被否 |
| --- | --- | --- |
| `KFL.Core` 的 `InternalsVisibleTo("KFL.Infrastructure")` + 两处 `internal` 复原面 | FR-003/FR-017 要求「逐字段相等」与「落盘不改推演结果」，而 `Family._settledOutsiders`/`_marriageAlignedOutsiders` 与 `FamineState.ElapsedMonths` 决定**后续**推演，不落盘即静默失真 | ① 把它们改成公开只读属性——扩大公开 API 面，且簿记本是实现细节；② 用 `internal void Set...` 逐字段开洞——`Person.SetGeneration`/`SetSpouse`/`AddFormerSpouse` **已经**是 `internal`，`InternalsVisibleTo` 让它们可直接复用，新增面因此只有两处 |
| `KFL.Infrastructure/Save/Serialization/` 的**瞬态 DTO + 显式映射器** | FR-020 禁止 `KFL.Core` 依赖具体序列化框架，而框架也无法穿过 `Core` 的不变量校验复原不可变字段（R-02） | ① `Core` 类型加序列化注解（违反 FR-020）；② DTO 放 `Core` + 无注解契约序列化（线上形状退化成「成员顺序」的隐式契约，改个成员次序就静默改存档格式）；③ 为每个 Core 类型写自定义 formatter（每格式每类型一份，漂移面翻倍） |
| `ISaveFormat` + 两个实现 | FR-004/FR-005 明令两种格式且**信息集必须等价**；把两套编解码放在一个类里会让「两格式同形」不可断言 | 单类内 `switch (format)`——两种格式的差异（头写法、选项、枚举写法、失败分类）会被织进同一批方法，契约十的「信息集等价」失去可指认的对象 |
| `Infrastructure/Save/` 独立目录（`AtomicFileWriter`/`SaveCatalog`/`SaveName`/`SaveService`） | FR-013 的原子性、FR-019 的「文件系统访问收敛在少数明确行」、R-18 的 UUID 探针各自是独立的可测关注点；存档是本项目**唯一**的持久化设施 | 全塞进 `SaveService`——文件系统行会散落在含业务判断的方法里，SC-008 的「豁免行数可被逐一列举」将无法核对；且 `Services/` 目录语义是「接缝实现」（`GameStateClock`/`SeededRandomService` 等），存档是一整块子系统 |
| `Rules/Save/TurnRunner` | FR-010/FR-011 要求「每回合结算成功后覆盖保存、每 3 回合备份」**可脱离界面被断言**；契约三 §1 的「MUST NOT 存在」清单明列「存档写入」，故落盘只能落在结算之外的一层 | ① 塞进 `MonthlySettlementEngine`——改 002 的七步序列（跨特性形状变更）且让纯规则动作带上 I/O；② 交给界面轨 U1 自己「结算后保存」——US2 的三条 AS 会全部落到界面轨，本特性内无可断言对象 |
| `tests/KFL.Tests/Infrastructure/SaveTestSandbox.cs` 单一沙箱辅助类 | 存档测试**本质**需要文件系统，而 G-07 与 SC-008 的扫描范围含 `tests/KFL.Tests/Infrastructure/`——豁免行必须能集中列举 | ① 新建 `tests/KFL.Tests/Save/`（不在扫描根内）——等于给新代码开守卫盲区；② 各测试类各自碰文件系统——豁免行散落，无法逐一核对 |

## 阶段 0 / 阶段 1 小结

`research.md` 的 **25 项抉择全部有结论，无 `NEEDS CLARIFICATION` 残留**。其中三项是**真问题**，
不是纸上推演——它们的共同特征是「不做就静默失真」：

1. **可派生的状态不能丢**（R-03）：`Family` 的两个私有簿记与 `FamineState.ElapsedMonths` **没有**
   公开读出、也**没有**公开写入；不落盘时，读回后的同一名外来者「辈分还能不能变」会与关闭前不同，
   直接违反 FR-003 与 FR-017。这是唯一必须新增 `Core` 面的地方。
2. **「每 3 回合」的回合数从哪来**（R-16）：`GameState` **没有**回合计数器。新增一个字段就是给
   时间加一条需同步维护的第二真源并触发跨特性形状变更；而 §3「1 回合 = 1 游戏月、自 1 年 1 月起」
   使回合数**可派生**。取派生，并把 `3` 与纪元钉在 `KFL.Rules/Config/`——`Infrastructure` 看不到
   `Rules`（G-05），这条错路从依赖边上就被堵住。
3. **落盘不能挂在结算里**（R-17）：契约三 §1 的「MUST NOT 存在」清单**明列**「存档写入」，
   `MonthlySettlementEngine` 的类文档亦自述「唯一编排入口、每月一次」。若把保存塞进它，就是一次
   跨特性步骤序列变更；`TurnRunner` 让「结算与落盘分离」成为**结构性**事实。

另有三项是把「规格书的模糊处」落成**可执行判据**：**截断 vs 结构不符**（R-05/R-07：头记录声明
`PayloadLength`，`剩余字节 < 声明长度 ⇒ Truncated`，而「文本合法但缺必需成员」由两段解析分开）、
**改名不变性**（R-25：`Rename` 是纯文件移动，故「内容不变」结构性成立）、**JSON 的可读承诺**
（R-08：中文不转义 + 枚举写名字 + 键名显式固定）。

**依赖侧**已用实测消除唯一的实质风险（R-24）：`MessagePack 2.5.305` 在 `net10.0` 上
**0 警告 0 错误**，`decimal` 的 scale 逐位保留，连续 `[Key]` 落成**位置式数组**（成员缺失即结构错误），
手写头 + `Consumed` 切片反序列化与 `UntrustedData` 均可用了。

## 下一步

`$speckit-tasks` 依据本计划与 [data-model.md](./data-model.md)、[contracts/](./contracts/) 生成依赖
有序的 `tasks.md`；其中 MUST 登记「回写义务清单」的 11 项（同步了哪些文件、同步到哪里），供后续审计
核对（AGENTS.md「铁律」）。实现阶段 MUST 一并完成：`.gitignore` 的 `saves/`、csproj 的
`MessagePack 2.5.305`、两处 `KFL.Core` 的 `internal` 复原面与 `InternalsVisibleTo`、以及
`SaveCadencePolicy` 在 `GameConfig` 中的转发。
