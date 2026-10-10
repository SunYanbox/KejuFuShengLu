# Implementation Plan: 开局资产与官吏体系

**Branch**: `003-opening-assets-officialdom` | **Date**: 2026-10-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-opening-assets-officialdom/spec.md`

## Summary

交付规格书 §16 **逻辑轨 ③**：把 001 的「空家族档案」与 002 的「会算账的家族」补上头尾。

- **头（US1）**：规格书 §10.1 的四出身开局——按农/工/商/士一次性落地家主、配偶、孩子（年龄、性别、
  辈分、父母与婚姻引用、四项天赋/学业/体质/天命寿数）、初始资产（现金入现金池、商本入商本池、
  田宅入资产组合）、生活费档位与米价系数初值；开局资产是**初始余额**，**不落账本条目**。
- **尾（US2~US4）**：规格书 §8 的官吏生涯——及第入仕 → **待阙**（随机 6~24 月、无俸）→ **授官**
  （一甲 L11 / 二甲 L13 / 三甲 L15 / 特奏名 L18）→ 政绩 +1/月（上限 100）→ 每满 **36 个月**考课
  一次（`min(25% + 政绩 × 0.3%, 70%)`，成功升 1 级）→ **70 岁致仕**并领**半俸**至寿终，
  禁升期内跳过到期判定。

技术路线（沿用 001/002 的分层口径）：**状态归 `KFL.Core`，编排与数值归 `KFL.Rules`，
姓名来源的实现在 `KFL.Infrastructure`**。

- `KFL.Core` 扩展三处：`GameDate` 允许**前史纪年**（开局家人必然生于 1 年 1 月之前，
  见「本次裁决 Q4」）、`StatusTimers` 增第 4 个计时（待阙剩余月数）、`DegreeRecord` 增
  「甲第」字段（`ImperialClass?`，向后兼容的可选第 5 参）。新增三个枚举（`ImperialClass`、
  `SalaryMode`、`AppointmentTrack`）与 `Person.MonthsInOffice`。
- `KFL.Rules` 新增 `Config/`（开局表、属性分布、官吏政策）、`Start/`（新建存档入口）、
  `Career/`（三态判定、及第入仕入口、月度推进），并把 `IncomeCalculator` 的俸禄
  从「有官阶就发全俸」细化为**在任/待阙/致仕三态**；`MonthlySettlementEngine` 在
  **收入之前**插入官吏推进步（FR-019）。
- `KFL.Infrastructure` 交付 `INameGenerator` 与**两个确定性实现**（内置宋风字库为主路径、
  Bogus `zh_CN` 适配器），首次消费章程依赖基线里的 Bogus。

本特性**不做**任何界面（FR-026 已裁决：界面属**界面轨 U1~U5**）、不做科举周期（逻辑轨 ⑤）、
不做惩罚矩阵/服刑与禁升计时的建立与递减（逻辑轨 ⑥）、不做婚育/疾病/寿命/继任（逻辑轨 ⑦）、
不做存档落盘（逻辑轨 ④）。

## Technical Context

**Language/Version**: C# 14 / .NET 10（沿用 001/002 基线；`global.json` 仍只锁 .NET 10 版本带）。
非 UI 三层 `net10.0`、UI 两层 `net10.0-windows`；**不新增工程**，仍六工程。

**Primary Dependencies**: **新增 1 个 NuGet 依赖：Bogus `35.6.1`**（仅 `KFL.Infrastructure`，
对应规格书 §2 的「姓名生成（Bogus zh_CN，内置宋风姓氏/名字字库兜底）」与章程依赖基线内的既有包；
本机 NuGet 缓存中已存在 `bogus/35.6.1`，含 `net6.0` 资产 → 兼容 `net10.0`，离线可还原）。
**MUST NOT 接第三个随机源**：Bogus 的 `Randomizer` 只允许以局部种子构造
（`new Randomizer(int)`），MUST NOT 设置全局静态 `Randomizer.Seed`（R-09）。
其余基线包（CommunityToolkit.Mvvm / MessagePack / Serilog / LiveChartsCore）在本特性仍无消费者。

**Storage**: N/A。存档落盘（MessagePack/JSON/备份）属逻辑轨 ④；本特性只保证开局与仕途状态
可被完整表达、可被确定性重放。开局结果**不写账本条目**（初始余额口径，FR-010）。

**Testing**: xUnit v2 + VSTest，`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false
-p:_MSTestEnableParentProcessQuery=false`（受限宿主的两个已知坑见 `AGENTS.md`）。
新增 `tests/KFL.Tests/Rules/{NewGameSetupTests,AppointmentTests,CareerAdvanceTests,RetirementTests,SalaryModeTests}.cs`
与 `tests/KFL.Tests/Infrastructure/NameGeneratorTests.cs`；扩写 `Rules/{IncomeTests,DeterminismTests,RulesTestHarness}.cs`
与 `Core/{ValueObjectTests,PersonTests,StatusTests,FamilyTests}.cs`；更新
`Architecture/ConfigLiteralTests.cs` 的数值清单。全部无 UI、无文件系统、无网络。

**Target Platform**: Windows（WPF 表现层，本特性不涉及）。规则层不引用任何平台类型。

**Project Type**: 桌面应用（WPF + MVVM）+ 分层类库，单仓库六工程（结构不变）。

**Performance Goals**: 无运行时性能目标（§3：逻辑推进仅由用户输入驱动）。
本特性可衡量的只有门禁本身（构建零警告零错误、测试全绿）与 SC-001~SC-010 的可断言性。

**Constraints**:
- 章程原则 II：`KFL.Rules` 内 MUST NOT 出现 `DateTime.Now`、`Random`、文件系统、网络
  （G-07 静态断言，扫描范围已含 `tests\KFL.Tests\Rules\` 与 `tests\KFL.Tests\Infrastructure\`）。
- 章程「技术栈与工程约束」：金额一律以**文**为单位的 `decimal` 计算，1 贯 = 1000 文；
  本特性全部数值 MUST 集中在 `KFL.Rules/Config/`（SC-009；扫描测试见契约八 §4）。
- 随机性只能来自注入的 `IRandomService`；「现在」只能来自注入的 `IGameClock`（契约二）。
- 依赖边不新增：`KFL.Core ← KFL.Infrastructure ← KFL.Rules`（G-05 不变）。
- `.slnx` 内 MUST 仍恰好六个 `<Project>`（G-02）；`tests/KFL.Tests/KFL.Tests.csproj` 的
  `_MSTestEnableParentProcessQuery` MUST 保持**注释态**提交。

**Scale/Scope**（结构预估，落地以本计划的结构树为准）：
`KFL.Core` 改 4 个文件、增 3 个枚举；`KFL.Rules` 增 6 个类型（3 配置 + 3 生涯/开局），
改 4 个（`GameConfig`、`IncomeCalculator`、`SettlementResult`、`MonthlySettlementEngine`）；
`KFL.Infrastructure` 增 3 个类型 + 1 处 csproj；测试增 6 个文件、改 8 个文件；
规格书与 001 工件的裁决回写 3 处；0 个界面功能、0 行持久化代码。

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 章程条款 | 本计划如何满足 | 结论 |
| --- | --- | --- |
| **I. .NET 10 单一运行时基线** | 不新增工程、不改任何 `TargetFramework`；`global.json` 不动；新增依赖 Bogus `35.6.1` 含 `net6.0` 资产（兼容 `net10.0`）、仍在维护，且是章程依赖基线的既有项 | ✅ |
| **II. 分层解耦与单向依赖** | 开局与仕途的**状态**（`MonthsInOffice`、待阙计时、甲第、`SalaryMode`）归 `KFL.Core`；**编排与数值**（开局表、分布、政策、及第入仕、月度推进、三态俸禄）归 `KFL.Rules`；姓名来源的**实现**归 `KFL.Infrastructure`（§2 的接口清单所在层）；`Core` 不引用 `Rules`（G-05 不变）；全部新增逻辑为纯函数或只依赖注入的 `IRandomService`；`Person`/`Family` 仍只承载数据与自身不变量，跨实体推进只在 `OfficialCareerAdvance` | ✅ |
| **III. WPF + MVVM 表现层契约** | 无 ViewModel、无 View、无 code-behind 改动；`KFL.Presentation` 与 `KFL.App` 一行不动（界面属界面轨 U1/U2/U3）；FR-026 已把界面逐项排除 | ✅（N/A 已说明） |
| **IV. 单测优先与结果确定性** | 四出身的开局逐项断言、待阙/授官/考课/政绩/致仕各条规则独立断言、§16 必测「俸禄表 18 级锚点 72/420/5100」在三态与半俸下复核；随机消费次序写进契约六/七并逐条断言；测试与实现同批提交 | ✅ |
| **V. 约定式提交与中文提交信息** | 规格书与 001 工件的裁决回写单独成 `docs` 提交；`feat(core)`（`GameDate`/`StatusTimers`/`DegreeRecord`/`Person` 与三个枚举）、`feat(rules)`（配置、开局、仕途、三态俸禄）、`feat(infrastructure)`（姓名来源）、`test(...)` 各起一次；不混装 | ✅（流程约束） |
| **VI. 调试通道不复制规则** | 本特性不建控制台（界面轨 U4）；政绩上限、考课概率与封顶、半俸比例等全部只声明在 `KFL.Rules/Config/`，将来的控制台复用同一入口 | ✅ |
| **技术栈与工程约束** | `.slnx` 唯一、六工程不变；无游戏引擎；金额 `decimal` 以文为单位；新增数值全部集中 `KFL.Rules/Config/`（SC-009 由契约八 §4 的扫描测试验证）；无日志/存档代码 | ✅ |
| **开发工作流与质量门禁** | 严格对位 §16 逻辑轨 ③（本特性即该条，非跨阶段提前动工）；三项门禁可执行；复杂度逐项论证（见 Complexity Tracking） | ✅ |
| **需求真源** | 本次 2 项用户裁决（Q4 前史纪年、Q5 服刑期仕途计时）与 4 项由真源推出的口径全部**回写规格书**（§3、§4.1、§7.5、§8.2 的 §17 裁决回写），并同步 001 的 `spec.md`/`data-model.md`；`plan.md`/`research.md`/`data-model.md` 逐条标注规格书章节号 | ✅ |

**设计后复核（Phase 1 之后）**：✅ 无新增违规。`data-model.md` 未引入任何需要放宽章程的结构
（无第 7 工程、无新接缝、无跨层引用）；契约六/七/八只描述既有分层内的接口与次序。

### 本次裁决（2026-10-08，用户逐项裁决；MUST 回写规格书）

| 编号 | 议题 | 裁决 | 回写位置 |
| --- | --- | --- | --- |
| **Q4** | 规格书 §10.1 要求开局家主 28±5 岁、配偶 25±5、孩子 0~8 岁，而 §3 规定从「1 年 1 月」开始推演；001 的 `GameDate` 又强制 `year >= 1`，且年龄**只能**由 `BirthDate` 派生（不落裸年龄字段）。三者不可能同时成立——开局家人的父母辈必然出生于 1 年 1 月之前 | **放宽 `GameDate` 的年份下界**：年份接受 `int` 全域（含 `0` 与负数，即**前史纪年**），月份仍 MUST 在 1~12；`GameState.CurrentDate` 仍从 **1 年 1 月**起推演，**规格书 §3「从 1 年 1 月开始推演」原文不变** | 规格书 §3（新增 §17 裁决回写）；`spec.md` FR-001/FR-004 措辞不动（起始年月仍是 1 年 1 月） |
| **Q5** | `spec.md` 的 Edge Case 只写明「待阙期内服刑 → 待阙计时暂停」，对**在任**官员服刑时政绩是否 +1、在职计时是否推进没有规定（§7.5 只说服刑者「不产生任何收益/人力」，而禁考则「现任官员正常任职领俸」） | **全部暂停**：服刑者的待阙计时、在职计时与政绩一律暂停（与其「本人不在家族内」及待阙暂停同口径）；俸禄本就为 0（服刑者不计口）。刑满后从暂停处继续 | 规格书 §7.5 / §8.2（新增 §17 裁决回写）；`spec.md` 的 Edge Case 与 FR-016/FR-018 同步补一句 |

> 规格书 §16 的双轨拆分、§16.2 的编号对照，以及 Q1~Q3（范围 = 规则层 / 吏治 = §8.2 全量 /
> 推进位于收入之前）已在 `$speckit-specify` 阶段裁决并回写，本计划直接继承。

## Project Structure

### Documentation (this feature)

```text
specs/003-opening-assets-officialdom/
├── plan.md                        # 本文件
├── spec.md                        # 规格（Q1~Q3 已裁决并回写）
├── research.md                    # 阶段 0：19 项抉择（R-01~R-19，全部有结论）
├── data-model.md                  # 阶段 1：值类型 / 枚举 / 实体 / 规则类型 / 需求映射 / 状态转移
├── quickstart.md                  # 阶段 1：可运行的人工验收流程
├── contracts/
│   ├── new-game-setup.md          # 契约六：开局入口、四出身表、随机消费次序、不落条目
│   ├── official-career.md         # 契约七：仕途状态机、月内次序、三态俸禄、考课、致仕、拒绝矩阵
│   └── config-registry.md         # 契约八：新增数值登记表 + SC-009 扫描口径（数值唯一性验证物）
├── checklists/requirements.md     # 规格校验（已完成，16/16）
└── tasks.md                       # 阶段 2 输出（$speckit-tasks 生成，非本命令产物）
```

> 001 的 `contracts/architecture-guard.md`、`contracts/injection-seams.md` 与 002 的
> `contracts/{monthly-settlement,ledger,config-registry}.md` 是**跨阶段契约**，本特性**继承**并做
> 三处最小修订：契约一 §2 的 G-07 行（扫描范围已在 002 扩至 `Rules\`）、
> 契约二 §1 的「消费者」格（新增姓名来源：实现仍在 `KFL.Infrastructure`、随机仍只来自
> `IRandomService`）、契约五/契约八的数值登记表（新增本特性数值，见契约八）。
> 契约三（月度结算）§1 的六步顺序被本特性**插入一步**（FR-019），已在契约七 §2 逐序号写明。

### Source Code (repository root)

```text
KejuFuShengLu.slnx                      # 不动（仍六工程）
global.json / Directory.Build.props     # 不动

src/
├── KFL.Core/                           # net10.0
│   ├── ValueObjects/
│   │   ├── GameDate.cs                 # 【改】年份下界放宽（前史纪年；月份仍 1~12）——Q4
│   │   ├── StatusTimers.cs             # 【改】新增第 4 个计时：待阙剩余月数
│   │   ├── DegreeRecord.cs             # 【改】新增可选第 5 参「甲第」+ 三条相容性不变量（FR-014）
│   │   └── ...
│   ├── Enums/
│   │   ├── ImperialClass.cs            # 【新】一甲 / 二甲 / 三甲（授官映射的唯一判据）
│   │   ├── SalaryMode.cs               # 【新】无 / 在任（全俸）/ 待阙（无俸）/ 致仕（半俸）
│   │   ├── AppointmentTrack.cs         # 【新】一甲 / 二甲 / 三甲 / 特奏名（授官入口的入参）
│   │   └── ...
│   ├── Entities/
│   │   ├── Person.cs                   # 【改】新增 MonthsInOffice；Status/Timers 交叉校验加 AwaitingPost
│   │   └── ...
│   └── Config/AttributeLimits.cs       # 【改】注释改标：政绩上限 100 归逻辑轨 ③ 的 OfficialCareerPolicy
├── KFL.Infrastructure/                 # net10.0
│   ├── Abstractions/
│   │   ├── INameGenerator.cs           # 【新】姓名来源接缝（名；姓氏由家族/玩家给定）
│   │   └── ...
│   ├── Services/
│   │   ├── SongStyleNameGenerator.cs   # 【新】内置宋风姓氏/名字库，确定性（规格书 §2 的兜底，产品默认）
│   │   ├── BogusNameGenerator.cs       # 【新】Bogus zh_CN 适配器，局部种子（规格书 §2 的主路径）
│   │   └── ...
│   └── KFL.Infrastructure.csproj       # 【改】PackageReference Bogus 35.6.1
├── KFL.Rules/                          # net10.0
│   ├── Config/
│   │   ├── OriginStartTable.cs         # 【新】§10.1 四出身表：初始资产、成员构成、年龄/学业/体质口径
│   │   ├── AttributePolicy.cs          # 【新】§4.1/§4.2 分布参数（N(60,20)/N(30,15)/N(85,10)/寿数）与正态取样
│   │   ├── OfficialCareerPolicy.cs     # 【新】§8.2 政策值（待阙 6~24、官阶映射、36 月、25%+0.3%、封顶 70%、上限 100、70 岁、半俸 50%）
│   │   ├── GameConfig.cs               # 【改】新增 NewGame / Attributes / Career 三组转发（仍零字面量）
│   │   └── ...
│   ├── Start/
│   │   └── NewGameSetup.cs             # 【新】新建存档入口 + NewGameRequest + NewGameSetupResult（薄视图）
│   ├── Career/
│   │   ├── SalaryModePolicy.cs         # 【新】三态判定的纯函数（None/AwaitingPost/Active/Retired）
│   │   ├── AppointmentEntry.cs         # 【新】「及第入仕」入口 + 初始官阶映射 + 幂等/互斥拒绝
│   │   └── OfficialCareerAdvance.cs    # 【新】月度推进（待阙→政绩→致仕→在职计时与考课）+ 事件快照
│   ├── Settlement/
│   │   ├── MonthlySettlementEngine.cs  # 【改】在「收入」之前插入官吏推进步（FR-019）
│   │   ├── IncomeCalculator.cs         # 【改】俸禄按三态计算（在任全俸 / 待阙 0 / 致仕半俸）
│   │   └── SettlementResult.cs         # 【改】新增 Career 事件快照字段
│   └── ...
├── KFL.Presentation/                   # 不动（界面轨 U1/U2/U3）
└── KFL.App/                            # 不动（界面轨 U1）

tests/
└── KFL.Tests/                          # net10.0
    ├── Architecture/ConfigLiteralTests.cs   # 【改】数值清单增补本特性的规则数值（SC-009 的验证物）
    ├── Core/
    │   ├── ValueObjectTests.cs         # 【改】GameDate 前史纪年 / DegreeRecord 甲第不变量 / StatusTimers 第 4 字段
    │   ├── PersonTests.cs              # 【改】一甲记录构造点补甲第 + MonthsInOffice 与待阙计时不变量
    │   ├── StatusTests.cs              # 【改】AwaitingPost 与计时的交叉校验
    │   └── FamilyTests.cs              # 【改】DegreeRecord 构造点补甲第
    ├── Infrastructure/
    │   └── NameGeneratorTests.cs       # 【新】姓名来源的确定性与「同种子同结果」
    ├── Fixtures/                       # 【扩】开局/官员夹具（沿用既有夹具风格）
    └── Rules/
        ├── RulesTestHarness.cs         # 【改】官员/待阙夹具 + 固定姓名来源替身
        ├── NewGameSetupTests.cs        # 【新】US1：四出身逐项、复现性、账本为空
        ├── AppointmentTests.cs         # 【新】US2：待阙 6~24、无俸、授官映射四档、幂等
        ├── CareerAdvanceTests.cs       # 【新】US3：政绩 +1/上限、36 月考课、概率、禁升跳过
        ├── RetirementTests.cs          # 【新】US4：70 岁致仕、半俸、政绩/计时停摆、幂等
        ├── SalaryModeTests.cs          # 【新】三态俸禄与锚点 72/420/5100 在半俸下的复核
        ├── IncomeTests.cs              # 【改】既有俸禄断言改为三态口径
        └── DeterminismTests.cs         # 【改】开局与仕途推进序列的同种子逐字段复现

科举浮生录规格书.md                     # 【改】§3 / §4.1 / §7.5 / §8.2 增补 §17 裁决回写（Q4、Q5、甲第、考课计时）
specs/001-core-skeleton/
├── spec.md                             # 【改】FR-008 补一句：甲第字段由 003 向后兼容扩展
└── data-model.md                       # 【改】DegreeRecord 小节同款补注
```

**Structure Decision**: 工程划分与依赖方向**不动**（规格书 §2 六个工程）。新增代码按
**关注点**落在既有目录语义内：`Core/ValueObjects`（值类型与其自身不变量）、`Core/Enums`
（结构事实）、`Core/Entities`（状态与自身不变量）、`Rules/Config`（**全部**规则数值）、
`Rules/Start`（新建存档编排）、`Rules/Career`（仕途编排）、`Rules/Settlement`（月度结算编排），
姓名来源的实现落在 `Infrastructure/Services`（§2 的接口清单所在层）。
`Rules/Start` 与 `Rules/Career` 是新目录：前者承载「一次性的开局编排」，后者承载「逐月的仕途推进」，
两者的**消费者**与**可验证物**不同（开局只需建一次，仕途是月度循环），合进 `Settlement/`
会让 `MonthlySettlementEngine` 的「唯一编排入口」注释与 002 契约三 §1 失去「单月推进」的可验收边界——
该拆分在 Complexity Tracking 中登记论证。

## Complexity Tracking

> 本特性无章程违规项。下面登记三项**新增抽象**的论证（章程「复杂度 MUST 被论证」）。

| 新增抽象 | 为什么现在需要 | 更简单的替代为何被否 |
| --- | --- | --- |
| `INameGenerator` + 两个实现（`KFL.Infrastructure`） | FR-009 明确要求本特性交付该抽象与至少一个确定性实现，且姓名是**开局随机结果的一部分**（FR-011 要求姓名逐字段可复现）。规则层 MUST 只依赖抽象 | 直接在 `NewGameSetup` 里 new 一个具体姓名生成器：规则层随即被钉死在 Bogus 的具体类型上，测试无法注入固定姓名，且「同种子 → 同姓名」只能靠 Bogus 的全局 `Randomizer.Seed`（隐式全局随机源，章程原则 IV 禁止） |
| `Rules/Start/` 独立目录 | 开局是**一次性编排**（生成成员、发放初始资产、落定家主），与「逐月推进」的结算语义不同；它需要 `IRandomService`、`INameGenerator` 与 `LivingCostTable`/`GrainPricePolicy` 的初值三组输入 | 塞进 `Settlement/MonthlySettlementEngine.cs`：该文件自述「唯一编排入口、每月一次」，混入一次性开局会让「单月推进」的验收边界消失，且开局并不需要 `IGameClock` |
| `Rules/Career/` 独立目录（三态判定 / 及第入仕 / 月度推进） | FR-019 要求仕途推进的**月内次序**可被单测逐步断言，且「及第入仕」是**科举/玩家触发的入口**而非月度步骤；三态判定还要被 `IncomeCalculator` 复用 | 全部写进 `MonthlySettlementEngine` 的私有方法：三态判定会被 `IncomeCalculator` 复制一份，及第入仕入口也无法被逻辑轨 ⑤ 复用（章程原则 VI 的同类问题） |

## 阶段 0 / 阶段 1 小结

`research.md` 的 19 项抉择全部有结论，**无 `NEEDS CLARIFICATION` 残留**。三项是从真源里读出的
**真问题**，而非纸上推演：

1. **开局年月与出生年月的矛盾**（Q4）：§10.1 的年龄区间与 §3 的「1 年 1 月起」不可能同时成立，
   除非允许**前史纪年**。这是 001 的 `GameDate` 不变量与 003 的数值要求之间的真实冲突，
   已按用户裁决放宽年份下界并回写规格书 §3。
2. **在任官员服刑时的仕途计时**（Q5）：§7.5 的「服刑不产生任何收益」与「禁考不禁在职」
   给出了两个相反的可类推模式，已按用户裁决取「全部暂停」并回写 §7.5/§8.2。
3. **甲第字段会撞上 001 的既有断言**：`ValueObjectTests.DegreeRecord四个成员均为必需` 断言
   构造函数**恰好 4 个参数且无可选参数**。加入向后兼容的第 5 参必然要改这条断言——
   本计划把它登记为**受控的工件修订**（R-02），而不是绕过它（例如用旁挂表或可空包装类型）。

另有 **Q5 派生的三条口径**与其余由 §17①/② 推导的子口径（考课计时先 +1 再比阈值、
「释褐即致仕」的次序、仕身份不由本特性设置、开局随机消费次序）**逐条登记在 `research.md`**，
供实现与复核时逐项否决。

## 下一步

`$speckit-tasks` 依据本计划与 [data-model.md](./data-model.md)、[contracts/](./contracts/) 生成
依赖有序的 `tasks.md`；随后 `$speckit-implement` 执行。落地时 MUST 一并提交本计划列出的
**规格书与 001 工件的裁决回写**（章程「需求真源」与「规格书变更 MUST 与受影响工件同步提交」）。
