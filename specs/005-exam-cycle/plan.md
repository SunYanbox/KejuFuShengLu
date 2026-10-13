# Implementation Plan: 科举周期

**Branch**: `005-exam-cycle` | **Date**: 2026-10-12 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-exam-cycle/spec.md`

## Summary

交付规格书 §16 **逻辑轨 ⑤「科举周期」**：把「三年一科」的**解试 → 省试 → 殿试**整条链条落成
`KFL.Rules` 里可纯函数断言、可逐次钉住随机消费的**月末结算新增整步**，并交付功名变迁、省试路费、
免解、特奏名与供界面轨消费的**纯查询面**。规格书 §6 的全部要求在本特性内闭合。

- **US1（P1）一届科年走完**：科年 `N` 的 `(N,8)` 解试 → `(N+1,3)` 省试（判定前扣路费）→
  `(N+1,4)` 殿试（**不掷录取骰**、100% 进士）；甲第与一甲名次按 §6 落进既有 `DegreeRecord`；
  每次中第追加一条 `ExamPass` 记录；及第当月经 **003 的既有入口** `AppointmentEntry` 置入待阙。
- **US2（P2）报名 / 路费 / 持续报考**：报名窗口 = 考试当月、幂等、不消耗随机、不读写账本；
  省试路费按难度直接取档（四档，**不乘**支出系数）、**落第不退**、现金不足即弃考（不计败、不落条目、
  不转贷款）；持续报考为家族级布尔开关，开时在报名月自动报名并在**同一结算内**完成判定。
- **US3（P3）免解与特奏名**：解试累计 **3** 败后此后免掷骰直接中第且计数不再增长；
  满 **50** 岁 + 省试 **6** 败者落第当月产出**一次询问**，接受后经既有入口以
  `AppointmentTrack.SpecialTribute` 入待阙（**不追加进士功名**）并退出科举，拒绝则状态不变、下次再问。
- **US4（P4）快进在考试月停住**：交付三个**纯查询**（该月考试级别 / 下一考试月 / 该考试月合格且
  未报名者名单），供 U1 的快进暂停与 U4 的「跳至下一科举」消费；**MUST NOT** 实现快进循环本身。

技术路线（沿用 001~004 的分层口径）：**状态与不变量归 `KFL.Core`，规则判定与编排归 `KFL.Rules`，
落盘形状归 `KFL.Infrastructure`（只随字段同步，不新增依赖）**。

- `KFL.Core`：**新增 1 个枚举**（`ExamLevel`）、**`Person` 新增 3 个成员**（解试/省试累计失败数 +
  报名标记）、**`Family` 新增 1 个成员**（持续报考开关）、**`LedgerCategory` 新增 1 个类别**
  （`ExamTravelCost`）。三个新成员**只有三条公开写入通道**（`ApplyForExam` / `ClearExamApplication` /
  `RecordExamFailure`）+ **一条 `internal` 复原通道**（`RestoreExamProgress`），**MUST NOT 新增公开
  setter**（001 不变量 2、FR-023）。
- `KFL.Rules`：新增 `Exam/`（`ExamCalendar` 纯查询、`ExamRegistration` 资格与报名、`ExamAdmission`
  概率与名次、`ExamSeasonRunner` 结算步、`ExamSeasonSnapshot` 增量快照）与
  `Config/ExamPolicy.cs`（§6/§7.1/§7.2 的**全部**数值单点）；`MonthlySettlementEngine` **七步 → 八步**
  （科举季插在 ⑥ 贷款之后、⑦ 推进时间之前）、`SettlementResult` 新增 `ExamSeason` 子段、
  `GameConfig` 加一个转发视图。
- `KFL.Infrastructure`：**不新增文件、不新增依赖**；只随字段同步两套编解码的键表/成员数常量、
  映射器、校验器，并把 `SaveFileFormat.Version` 由 **1 升为 2**（FR-024：**MUST NOT** 实现旧→新迁移）。

本特性**不做**任何界面（主界面 / 下月 / 快进循环 / 调试控制台 / 卡片统计属 U1~U5）、不做贿赂全链路
（⑥；本轮只经**同一入口的形参**消费乘数且恒 `1.0`）、不做成长衰老疾病死亡（⑦）、不做成就与绝嗣
（⑧；只产出事件流与计数）、不做存档迁移（Out of Scope）。

## Technical Context

**Language/Version**: C# 14 / .NET 10（沿用 001~004 基线；`global.json` 不动，仍只锁 `10.0` 版本带）。
非 UI 三层 `net10.0`、UI 两层 `net10.0-windows`；**不新增工程**，仍六工程。

**Primary Dependencies**: **零新增 NuGet 依赖**。本特性只使用既有依赖（xUnit 测试面）；`KFL.Core`
与 `KFL.Rules` 不引入任何新包，也不触碰 `CommunityToolkit.Mvvm` / `MessagePack` / `Bogus` /
`Serilog` / `LiveChartsCore` 的既有引用（后四者在本特性仍无新消费者）。

**Storage**: 本特性**不新增持久化设施**，只**扩充既有存档形状**：`PersonDto` 22 → **25** 个成员
（+`jieShiFailureCount` / `shengShiFailureCount` / `appliedExam`）、`FamilyDto` 6 → **7** 个成员
（+`autoExamApplication`）；`SaveFileFormat.Version` **1 → 2**。`KFL.Core` 与 `KFL.Rules` 仍是
**零**文件系统访问（G-07 零违规）。

**Testing**: xUnit v2 + VSTest，`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false
-p:_MSTestEnableParentProcessQuery=false`（受限宿主的两个已知坑见 `AGENTS.md`）。
新增 `tests/KFL.Tests/Rules/{ExamCalendarTests,ExamRegistrationTests,ExamAdmissionTests,
ExamSeasonTests,ExamSpecialTributeTests}.cs`（5 个），并同步 `Rules/SpecAnchorTests.cs`、
`Rules/DeterminismTests.cs`、`tests/KFL.Tests/Core/` 的类别计数断言、`Architecture/ConfigLiteralTests.cs`
与 004 的四个存档断言文件。全部测试**无界面、无文件系统、无网络**（存档往返沿用 004 的临时目录
沙箱），随机一律经可设种子的 `IRandomService`（章程原则 IV、G-07、FR-025）。

**Target Platform**: Windows（WPF 表现层，本特性不涉及）。规则层不引用任何平台类型。

**Project Type**: 桌面应用（WPF + MVVM）+ 分层类库，单仓库六工程（结构不变）。

**Performance Goals**: 无运行时性能目标（§3：逻辑推进仅由用户输入驱动）。可衡量的只有门禁本身
（构建零警告零错误、测试全绿）与 SC-001~SC-011 的可断言性——其中 SC-001 要求**固定种子跑完一个
完整科年，两次独立运行逐字段差异项 = 0**。

**Constraints**:
- 章程原则 II：`KFL.Rules` MUST NOT 出现 `DateTime.Now` / `Random` / 文件系统 / 网络（G-07 静态断言）；
  本特性的**全部**随机调用必须经注入的 `IRandomService`——随机消费次序由
  [契约十三 §1.3](./contracts/exam-session.md#13-整月随机消费次序fr-019-钉死单测逐次断言) 逐次钉死。
- 章程原则 VI：规则层 MUST NOT 感知调用方；封顶与「无限制」能力 MUST 经**装配期显式注入的配置或
  策略**提供，MUST NOT 在规则层留按调用方分支的绕过路径（FR-015）。
- 规则数值 MUST 集中在 `KFL.Rules/Config/`：本特性新增的唯一数值文件是 `ExamPolicy.cs`，且新增的
  **4** 项小数 MUST 同步登记进 `ConfigLiteralTests.RegisteredValues`（整数一律不登记）（FR-014）。
- 依赖边不新增：`KFL.Core ← KFL.Infrastructure ← KFL.Rules`（G-05 不变）；MUST NOT 在
  `KFL.Rules` 之外的层定义任何结算接缝接口（FR-029 与后续守卫项）。
- `.slnx` 内 MUST 仍恰好六个 `<Project>`（G-02）；`tests/KFL.Tests/KFL.Tests.csproj` 的
  `_MSTestEnableParentProcessQuery` MUST 保持**注释态**提交。

**Scale/Scope**（预估，实现期按实际现状回写）：

- `KFL.Core`：**增 1** 个文件（`Enums/ExamLevel.cs`）、**改 4** 个（`Entities/Person.cs`、
  `Entities/Family.cs`、`Enums/LedgerCategory.cs`、`Enums/LedgerCategoryMetadata.cs`）；
  **3** 个领域字段新增 + **1** 个家族字段新增；**0** 个新工程、**0** 个新层间接缝接口。
- `KFL.Rules`：**增 6** 个文件（`Exam/ExamCalendar.cs`、`Exam/ExamRegistration.cs`、
  `Exam/ExamAdmission.cs`、`Exam/ExamSeasonRunner.cs`、`Exam/ExamSeasonSnapshot.cs`、
  `Config/ExamPolicy.cs`）、**改 3** 个（`Settlement/SettlementResult.cs`、
  `Settlement/MonthlySettlementEngine.cs`、`Config/GameConfig.cs`）。
- `KFL.Infrastructure`：**增 0** 个文件（无新依赖）、**改 5** 个
  （`Save/Serialization/{SaveStateDto,MessagePackSaveFormat,SaveStateMapper,SaveStateValidator}.cs`、
  `Save/SaveFileFormat.cs`）。
- `KFL.Presentation` / `KFL.App`：**0** 个界面功能、**0** 行界面代码。
- 测试：**增 5** 个文件、**改 ≥6** 个既有文件（断言物同步）。
- **1** 处线上形状版本提升（`SaveFileFormat.Version` 1 → 2），**0** 条迁移路径。

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 章程条款 | 本计划如何满足 | 结论 |
| --- | --- | --- |
| **I. .NET 10 单一运行时基线** | 不新增工程、不改任何 `TargetFramework`；`global.json` 不动；**零新增依赖** | ✅ |
| **II. 分层解耦与单向依赖** | **状态与不变量**归 `KFL.Core`（3 + 1 个字段与三条写入通道）；**全部判定与编排**归 `KFL.Rules`（`Exam/` + `ExamPolicy`）；**落盘形状同步**归 `KFL.Infrastructure`；`Core` 不引用 `Rules`、`Infrastructure` 不引用 `Rules`（G-05 不变）；`Core`/`Rules` 零文件系统访问（G-07）；规则逻辑为纯函数或仅依赖可注入的 `IRandomService` | ✅ |
| **III. WPF + MVVM 表现层契约** | 无 ViewModel、无 View、无 code-behind 改动；`KFL.Presentation` 与 `KFL.App` **一行不动**（界面属 U1~U5，spec 的 Out of Scope 已逐项排除）。**不新增任何界面接口**——U1/U4 只消费 `ExamCalendar` / `ExamRegistration` 的**纯查询**（FR-018） | ✅（N/A 已说明） |
| **IV. 单测优先与结果确定性** | 五个关注点各一个测试文件（日历 / 报名 / 概率 / 结算编排 / 特奏名）；**全部**经可设种子 `IRandomService`；无界面/文件系统/网络；§16 必测清单的本范围项逐条有锚点；测试与实现同批提交；SC-001 要求两次独立运行差异项 = 0 | ✅ |
| **V. 约定式提交与中文提交信息** | `feat(core)`（`ExamLevel` + 三字段 + 账本类别）、`feat(rules)`（`Exam/` + `ExamPolicy` + 结算步 + 快照）、`feat(infrastructure)`（键表/版本）、`test(...)`、`docs(...)`（上游活工件回写与 §17 裁决回写）各起一次；不混装 | ✅（流程约束） |
| **VI. 调试通道不复制规则** | 本特性不建控制台（U4）；**录取率/进甲率是唯一入口**，概率公式经形参接收贿赂乘数，规则层无按调用方分支的绕过路径（FR-015/FR-016）；`ExamPolicy` 是 §6/§7.1/§7.2 数值的**单点**，`Infrastructure` 与界面层 MUST NOT 出现副本 | ✅ |
| **技术栈与工程约束** | `.slnx` 唯一、六工程不变；无游戏引擎与外部美术资源/emoji；金额以文为单位的 `decimal`；新增数值集中 `KFL.Rules/Config/`；无日志代码 | ✅ |
| **开发工作流与质量门禁** | 严格对位 §16 逻辑轨 ⑤（本特性即该条，非跨阶段提前动工）；门禁四条可执行；复杂度逐项论证（见 Complexity Tracking） | ✅ |
| **需求真源** | 三项用户裁决（Q1/Q2/Q3，2026-10-12，全 A）已按规格书 §17 回写规格书 §3 / §6 / §7.1 并由本计划继承；`plan.md`/`research.md`/`data-model.md`/`contracts/` 逐条标注规格书章节号；本特性**未自创任何影响平衡的数值**（唯一新增数值全部逐字取自 §6/§7.1/§7.2，且**没有**新增任何数值——见「勘误登记」） | ✅ |

**设计后复核（Phase 1 之后）**：✅ 无新增违规。`data-model.md` 与三份契约未引入任何需要放宽章程的
结构：仍是六个工程、无新接缝层、无跨层引用、无界面代码；契约十二/十三/十四只描述既有分层内的
接口、次序与数值归属；`SettlementResult` 的新增子段与存档版本提升都已在「回写义务清单」登记为
**跨特性形状变更**，且**先在变更源 `spec.md` / `plan.md` 声明**（AGENTS.md 铁律）。

### 继承的用户裁决（2026-10-12，全 A）

| 编号 | 议题 | 裁决 | 落点 |
| --- | --- | --- | --- |
| **Q1** | §6「殿试不淘汰」与 §7.1「封顶 89.9999%」冲突 | 殿试录取**不掷骰**、入殿试者 100% 成为进士；封顶只作用于**需掷骰的概率**（解试/省试录取率 + 殿试**进甲率**） | FR-008/FR-009/FR-015；[契约十三 §4.2/§4.3](./contracts/exam-session.md#42-殿试进甲率不掷录取骰) |
| **Q2** | 报名发生在哪个月 & 什么算「一次败」 | 报名 = **考试当月**（解试 = 科年 8 月，省试 = 次年 3 月；殿试不需报名）；只有「报名并**实际应考**而落第」记 1 败——未报名、弃考（路费不足）**都不计败** | FR-004/FR-005/FR-006/FR-008/FR-023；[契约十二 §4](./contracts/exam-calendar.md#4-报名窗口幂等与标记) |
| **Q3** | 特奏名可拒绝如何落地 | 每个科年**询问一次**；接受 ⇒ 经 003 通用入口 `AppointmentEntry.Begin(…, AppointmentTrack.SpecialTribute, …)` 入待阙、**不追加进士功名**、此后退出科举；拒绝/未应答 ⇒ 不改任何状态，可继续考，下次省试落第后**再次**询问 | FR-012；[契约十三 §8](./contracts/exam-session.md#8-特奏名询问判定面入口面见契约十二-62) |

## 勘误登记（计划阶段）

| # | 位置 | 原文 | 更正 | 依据 |
| --- | --- | --- | --- | --- |
| 1 | [spec.md](./spec.md) US1 AS1（L74-75） | 解试录取率算式 `0.01 × (0.4 + 0.012 × 60) × (1 + 60/400) × 1.0 × 1.0 = **1.42%**` | 同一算式的值 = `0.01 × 1.12 × 1.15 = 0.01288` ⇒ **`1.288%`**（≈`1.29%`） | 规格书 §6（`:198-199`）**只给公式、未给示例值**；「1.42%」是 spec 阶段自算的示例值且**与同行的公式不符**（章程「需求真源」：不得自创/改写数值） |

> 该勘误**只修正算式、未改任何数值**：规格书的 1% / 10% / 5% / 0.4 / 0.012 / 400 / 1.1 / 50 /
> 89.9999% 与四档路费一字未动。更正后的示例值已写回 [spec.md](./spec.md)，并必须被
> `ExamAdmissionTests` 的录取率锚点逐项引用（否则同一处会二次分叉）。

## 回写义务清单（AGENTS.md「铁律」+ spec FR-028 的落地）

**先说结论**：本特性**在公开面上确实做了跨特性形状变更**——① `Person` **+3 字段**、
② `Family` **+1 字段**、③ `LedgerCategory` **+1 取值**（19 → 20）、④ `SettlementResult` **+1 子段**、
⑤ `MonthlySettlementEngine` **七步 → 八步**（插在第 ⑦ 位）、⑥ `SaveFileFormat.Version` **1 → 2**。
按 AGENTS.md 的铁律，这些变更**已在变更源 [spec.md](./spec.md)（FR-023/FR-024/FR-028）与本计划声明**，
并 MUST 在**同一提交**内完成下表回写。

> 下表的**行号为回写前的原位**，逐条回写后行号会移动，故每行同时给出**可检索的锚点文本**。

| # | 目标工件（活工件） | 回写内容（锚点） | 依据 |
| --- | --- | --- | --- |
| 1 | `科举浮生录规格书.md` §3 / §6 / §7.1（顺及 §7.2） | **已完成（2026-10-12）**：已追加「§17 裁决回写」块记入 Q1/Q2/Q3；原始条目文本按 §16.2 的历史事实口径**保留不改写** | spec `## Clarifications` |
| 2 | `specs/001-core-skeleton/data-model.md` | `Person` 字段表 `:120-143` 增三行（解试/省试累计失败数、报名标记）；`Family` 表 `:196-206` 增一行（持续报考开关）；不变量 2 `:160-165`（无公开 setter 的集合扩充）、不变量 4 `:170-172`、不变量 7 `:175-177`；`StatusTimers` `:100-112` 与 `DegreeRecord` `:66-92` 的**引用注**（一甲名次 ⟺ 一甲由本特性首次生产）；`HasShiStatus` `:233` / `:249-251`（**首个生产者**落实） | FR-023、FR-028⑤ |
| 3 | `specs/002-monthly-settlement-economy/contracts/monthly-settlement.md` | §1 步骤表 `:13-21`（七步 → 八步，科举季 = 新 ⑦）；**`MUST NOT 存在` 清单 `:35-36` 移除「科举季触发（阶段 ⑤）」**并加 005 后记；§4 随机消费次序 `:62-79` 补「科举判定（逐人：录取/进甲，进甲者紧接名次）→ 及第入仕待阙」；§9 快照面 `:205-212` 增 `ExamSeason` 子段 | FR-019、FR-022、FR-028② |
| 4 | `specs/002-monthly-settlement-economy/spec.md` | Status 行 `:9`、Out of Scope `:379-402`（尤其 `:388-389` 的「科举季触发属阶段⑤」）、FR-020 `:306-310`、Assumptions `:432` 的同步注 | FR-028② |
| 5 | `specs/002-monthly-settlement-economy/data-model.md` | §4.2 `SettlementResult` `:297-311`（新增 `ExamSeason` 子段）；§4.3 七步 `:322`（→ 八步）；§6.3/§6.4 `:390-412` | FR-022、FR-028② |
| 6 | `specs/002-monthly-settlement-economy/contracts/config-registry.md` | §7 `:102-109`：把「科举录取率」从「002 阶段 MUST NOT 存在」清单**移出并加后记**；§6 加一行「科举数值只指向不复制」 | FR-014、FR-028④ |
| 7 | `specs/003-opening-assets-officialdom/contracts/official-career.md` | §3 条款 6 `:61`（`EntryTrack` 由 005 的「及第入仕」入口写入）；§10 `:145-149`（把「科举周期与功名推进属逻辑轨 ⑤」**移出**）；§8 `:134`（待阙时长随机属 005 的入口） | FR-011、FR-028 |
| 8 | `specs/003-opening-assets-officialdom/contracts/config-registry.md` | §5 `:91-95`：把科举周期相关数值从「阶段 ⑤ 才有的数值」清单移出并加后记 | FR-014 |
| 9 | `specs/004-save-system/contracts/save-format.md` | §4.2/§4.3 键表 `:73-92`（`PersonDto` 22 → 25、`FamilyDto` 6 → 7）；版本闸门 `:109-129` 与 `:133-135`（`Version` 1 → 2，拒绝口径改为 `!= 2`）；§6 失败分类口径 | FR-024、FR-028⑥ |
| 10 | `specs/004-save-system/data-model.md` | §3 `:93-217`（两张 DTO 表）、§3.11 复原次序 `:240-255`（新增 3 个成员的读回位置）、§8 不变量 `:396-417` | FR-024、FR-028⑥ |
| 11 | `src/KFL.Rules/Settlement/MonthlySettlementEngine.cs` 类文档 | 七步 → 八步；`MUST NOT 存在` 清单**移除**「科举季触发（阶段 ⑤）」；随机消费次序补入「科举判定 → 及第入仕待阙」 | FR-019、FR-028③ |
| 12 | `src/KFL.Core/Enums/LedgerCategory.cs` + `LedgerCategoryMetadata.cs` | 类别计数 **19 → 20**：枚举类文档与两处 `default:` 异常文案（「共 19 个取值」→「共 20 个取值」） | FR-021、FR-028⑦ |
| 13 | 断言物（同提交，**不是**历史工件） | `tests/KFL.Tests/Architecture/ConfigLiteralTests.cs` 的 `RegisteredValues` **+4 项小数**（`0.01m` / `0.05m` / `0.012m` / `0.899999m`）；`Core/` 的类别计数断言（19 → 20）；`Infrastructure/{SaveFixtures,SaveStateComparer,SaveFormatContractTests,SaveFailureTests}.cs`（键表、成员数常量、版本闸门用例、夹具非默认值、比较器、校验口径） | FR-014、FR-024、SC-008、SC-009 |
| 14 | `src/KFL.Infrastructure/Save/SaveFileFormat.cs` + `Save/Serialization/{MessagePackSaveFormat,SaveStateMapper,SaveStateValidator,SaveStateDto}.cs` | `Version` 1 → 2；`PersonMemberCount` 22 → 25 及 walker 的 Skip 计数；`FamilyMemberCount` 6 → 7；双向映射；第 ⑩ 条校验（`appliedExam ∈ { JieShi, ShengShi }` 且两个失败数 `>= 0`） | FR-024 |
| 15 | `specs/005-exam-cycle/tasks.md` | 登记上述回写范围（同步了哪些文件、同步到哪里），供后续审计核对 | AGENTS.md 铁律第 3 条 |

> **不改的**：`tasks.md`（001~004 的既有条目）、`implementation-notes.md`、既有 `checklists/` 属
> **历史记录类工件**，MUST NOT 回改（规格书 §16.2；AGENTS.md）。
> **001 的 `contracts/{architecture-guard,injection-seams}.md`** 本特性**不改**：不引入新工程、
> 不引入新接缝接口，G-01~G-08 的口径与扫描范围均不变（R-15）。

## Project Structure

### Documentation (this feature)

```text
specs/005-exam-cycle/
├── plan.md                        # 本文件
├── spec.md                        # 规格（Q1~Q3 已裁决并回写规格书 §3/§6/§7.1；含 1 处算式勘误）
├── research.md                    # 阶段 0：15 项抉择（R-01~R-15，全部有结论，未决事项 = 无）
├── data-model.md                  # 阶段 1：层分布 / 字段与签名 / 状态转移 / 15 条不变量 / 键表与版本 / 需求映射
├── quickstart.md                  # 阶段 1：8 个可运行的人工验收场景
├── contracts/
│   ├── exam-calendar.md           # 契约十二：考试日历派生、资格与报名、查询面、标记生命周期
│   ├── exam-session.md            # 契约十三：结算步位置与内部次序、整月随机消费次序、路费、概率、免解、入仕、快照
│   └── config-registry.md         # 契约十四：科举数值登记、禁止项、RegisteredValues 对照、回写关系
└── checklists/requirements.md     # 规格校验（$speckit-specify 阶段完成）
```

> 001 的 `contracts/{architecture-guard,injection-seams}.md`、002 的
> `contracts/{monthly-settlement,ledger,config-registry}.md`、003 的
> `contracts/{new-game-setup,official-career,config-registry}.md`、004 的
> `contracts/{save-service,save-format,save-cadence}.md` 是**跨阶段契约**，本特性**继承**并按
> 「回写义务清单」做**五处最小修订**（#3 步骤表与 MUST NOT 清单、#6 / #8 数值归属、#7 入仕入口归属、
> #9 键表与版本）。**契约四（账本）的判定句不动**，只在其类别表内新增一行 `ExamTravelCost`。

### Source Code (repository root)

```text
KejuFuShengLu.slnx                       # 不动（仍六工程）
global.json / Directory.Build.props      # 不动
*.csproj                                 # 不动（零新增依赖）

src/
├── KFL.Core/                            # net10.0
│   ├── Enums/
│   │   ├── ExamLevel.cs                 # 【新】ExamLevel { JieShi=1, ShengShi=2, DianShi=3 }（不设 None）
│   │   ├── LedgerCategory.cs            # 【改】追加第 20 个取值 ExamTravelCost；类文档 19 → 20
│   │   └── LedgerCategoryMetadata.cs    # 【改】两个 switch 各 +1 分支；两处异常文案 19 → 20
│   ├── Entities/
│   │   ├── Person.cs                    # 【改】+3 成员、+3 写入通道、+1 internal RestoreExamProgress
│   │   └── Family.cs                    # 【改】+AutoExamApplication（与 HasShiStatus 同款）
│   └── ...                              # 其余一行不动
├── KFL.Infrastructure/                  # net10.0（零新增文件）
│   └── Save/
│       ├── SaveFileFormat.cs            # 【改】Version 1 → 2
│       └── Serialization/
│           ├── SaveStateDto.cs          # 【改】PersonDto +3 键（22..24）、FamilyDto +1 键（6）
│           ├── MessagePackSaveFormat.cs # 【改】PersonMemberCount 22→25、FamilyMemberCount 6→7 与 walker
│           ├── SaveStateMapper.cs       # 【改】双向映射（复原次序沿用 004 data-model §3.11）
│           └── SaveStateValidator.cs    # 【改】+1 条（appliedExam 取值域、两个失败数非负）
├── KFL.Rules/                           # net10.0
│   ├── Exam/                            # 【新目录】
│   │   ├── ExamCalendar.cs              # 【新】IsKeYear / LevelOf / NextExamMonth（纯查询，FR-001/018）
│   │   ├── ExamRegistration.cs          # 【新】TargetLevelOf / IsEligible / CanRegister / Register / EligibleUnapplied / SpecialTributeEligible / AcceptSpecialTribute
│   │   ├── ExamAdmission.cs             # 【新】AdmissionRate / ImperialPromotionRate / ImperialClassFor / RollPlacement（纯函数）
│   │   ├── ExamSeasonRunner.cs          # 【新】Run(GameState, IRandomService) —— 契约十三 §1.2 的六段
│   │   └── ExamSeasonSnapshot.cs        # 【新】快照主体 + 4 个行记录 + ExamAbsenceReason
│   ├── Config/
│   │   ├── ExamPolicy.cs                # 【新】§6/§7.1/§7.2 全部数值的单点声明处
│   │   └── GameConfig.cs                # 【改】+1 个 Exam 转发视图（仍零字面量）
│   └── Settlement/
│       ├── MonthlySettlementEngine.cs   # 【改】七步 → 八步（科举季插在 ⑥ 之后、⑦ 之前）
│       └── SettlementResult.cs          # 【改】+1 个 required init 子段 ExamSeason
├── KFL.Presentation/                    # 不动（界面轨 U2/U3）
└── KFL.App/                             # 不动（界面轨 U1）

tests/
└── KFL.Tests/                           # net10.0
    ├── Architecture/
    │   └── ConfigLiteralTests.cs        # 【改】RegisteredValues +4 小数（0.01 / 0.05 / 0.012 / 0.899999）
    ├── Core/                            # 【改】类别计数断言 19 → 20
    ├── Infrastructure/                  # 【改】SaveFixtures / SaveStateComparer / SaveFormatContractTests / SaveFailureTests
    └── Rules/
        ├── ExamCalendarTests.cs         # 【新】FR-001、FR-018①②；SC-007 前半
        ├── ExamRegistrationTests.cs     # 【新】FR-002~FR-006、FR-012（接受面）、FR-013、FR-017、FR-018③
        ├── ExamAdmissionTests.cs        # 【新】FR-007~FR-010、FR-015、FR-016；SC-002/003/004
        ├── ExamSeasonTests.cs           # 【新】FR-019~FR-022、FR-027；SC-001/005/006
        ├── ExamSpecialTributeTests.cs   # 【新】FR-012 的判定与拒绝面；SC-005
        ├── SpecAnchorTests.cs           # 【改】新增科举数值锚点节
        └── DeterminismTests.cs          # 【改】新增科举季的两次独立运行差异项 = 0
```

**Structure Decision**: 工程划分与依赖方向**不动**（规格书 §2 六个工程）。新增代码按**关注点**落在
既有目录语义内：`Core/Enums`（一个新维度枚举）、`Core/Entities`（三个字段与其唯一写入通道）、
`Rules/Exam`（日历 / 报名 / 概率 / 结算步 / 快照五个正交关注点）、`Rules/Config`（**全部**规则数值）、
`Infrastructure/Save/Serialization`（**只**随字段同步键表与映射，不改分层）。

`Rules/Exam/` 是本特性唯一的**新目录**，理由与替代方案见 Complexity Tracking。五个文件按关注点拆分，
与 R-14 的测试划分一一对应（失败定位不退化，且契约十三的「六段次序」在代码里有可指认的对象）。

## Complexity Tracking

> 本特性无章程违规项。下面登记**七项新增抽象 / 新增面**的论证（章程「复杂度 MUST 被论证」）。

| 新增抽象 | 为什么现在需要 | 更简单的替代为何被否 |
| --- | --- | --- |
| `KFL.Core/Enums/ExamLevel`（新枚举） | FR-023 明令新增「考试级别」枚举；它同时是**报名标记的取值域**与 **⑥ 贿赂的计数维度**（规格书 §7） | ① 复用 `DegreeLevel`——语义不同（级别 ≠ 功名，白身与进士都可报考）；② 用 `int` / `string`——失去 `Enum.IsDefined` 的存档校验（004 契约十 §6）与编译期穷尽 |
| `Person` 三个新成员 + 三条写入通道 + 一条 `internal` 复原通道 | FR-023 要求「解试累计失败数 / 省试累计失败数 / 报名标记」持久；001 不变量 2 禁止无约束公开 setter，故必须给**具名通道**（幂等、定域、可校验） | ① 三个公开 `set;`——违反 001 不变量 2；② 只给 `internal` 通道——规则层（`KFL.Rules`）引不到 `KFL.Core` 的 internal 面，实现不了 FR-007/008/013；③ 把失败数放进 `GameState` 的旁路字典——成为**第二真源**且存档要额外同步 |
| `Family.AutoExamApplication` | FR-017 明令「持续报考 MUST 为**家族级**布尔字段」 | 存在 `GameState` 上——跨特性形状变更更大；存在装配期注入的策略里——则「开关」不可存档、读档后静默丢失（FR-024 要求完整落盘） |
| `LedgerCategory.ExamTravelCost` | FR-021 明令「新增账本类别」且 MUST NOT 用事件类或第二套账 | ① 复用 `LivingCost`——会污染生活费统计与快照断言；② 事件类——`Swap` 会破坏既有 SC-005 口径（资金类求和）；③ 第二套账——FR-021 明令禁止 |
| `KFL.Rules/Exam/` 新目录（5 文件） | 本特性跨越「日历 / 报名 / 概率 / 结算编排 / 兜底机制」五个正交关注点，且 U1/U4 只消费其中两个**纯查询**类 | 全塞进 `MonthlySettlementEngine`——契约三 §1 的步骤表会变成一坨，且日历与资格判定无法脱离结算被单独断言（SC-007 的纯函数要求） |
| `SettlementResult.ExamSeason`（跨特性形状变更） | FR-022 明令「作为 `SettlementResult` 的新增子段」，与 003 的 `Career` 同款；且快照必须表达**不产生账本条目**的事件（弃考、免解、询问、`HasShiStatus` 置位） | 只靠账本条目与状态读取——「本月发生了什么」无法增量表达，且 U1/U2 会退化成 diff 与副作用（章程原则 VI） |
| `SaveFileFormat.Version` 1 → 2（唯一线上形状破坏） | FR-024 明令升版；MessagePack 是**位置式数组**，`PersonDto` 由 22 变 25 后旧档必然错位读 | ① 提供旧→新迁移——Out of Scope 且会被 §14 的「拒绝优于误读」否掉；② 把新字段塞进 `GameState` 的扩展袋——仍是第二真源且迁移问题原样存在 |

## 阶段 0 / 阶段 1 小结

`research.md` 的 **15 项抉择全部有结论，未决事项 = 无**，`data-model.md` 给出 15 条不变量，
三份契约（十二/十三/十四）把形状、次序与数值归属分别钉死。其中**五项是真问题**，共同特征是
「不做就静默失真」或「做了就静默破形状」：

1. **报名标记是必要的持久状态**（R-03/R-04）：没有它，自动报名与手动报名会在**读档后**分叉
   （U1 的「本月报名」按钮与 FR-017 的自动报名必须走同一段规则）。故它不是第二真源，而是
   **唯一**的「本人已报名」真源，且必须在考试月的结算里被消费并清除。
2. **科举季不能挂在既有步里**（R-07）：FR-019 明令插在 ⑥ 与 ⑦ 之间，且 `MonthlySettlementEngine`
   类文档的「`MUST NOT 存在`」清单**当前含**「科举季触发」——落地时必须同提交移除（回写 #11）。
3. **路费只能走 `FamilyEconomy.Apply`，且必须先自算现金**（R-09）：`Apply` 对负额走
   「现金 → 储蓄」扣付、**不做部分支付**、也不自动转贷款。FR-008 要求「现金不足即弃考且**不落条目、
   不转贷款**」，因此调用方 MUST **先**判 `Treasury.Cash >= 路费`，**再**调 `Apply`——否则会静默
   吃储蓄，SC-006 直接不成立。
4. **殿试必须两段判定**（R-06）：Q1 裁决把「必中」与「进甲」拆成两件事——必中**不掷骰**、
   进甲掷一次且**不乘仕族加成**、封顶只作用于后者。写成一段会让 SC-002 的封印项无法断言。
5. **存档版本必须提升**（R-11）：`PersonDto` 是位置式数组，22 → 25 会让旧档**静默错位**读成
   别的字段；升版 + 拒绝（不迁移）是唯一与 §14「拒绝优于误读」一致的选择。

另有**一项计划阶段的勘误**（见「勘误登记」）：spec 的 US1 AS1 把算式写成了 `1.42%`，而同行公式
逐项相乘是 `1.288%`；规格书 §6 只给公式、未给示例值，故按公式更正，且**不涉及任何数值改动**。

## 下一步

`$speckit-tasks` 依据本计划与 [data-model.md](./data-model.md)、[contracts/](./contracts/)、
[quickstart.md](./quickstart.md) 生成依赖有序的 `tasks.md`；其中 MUST 登记「回写义务清单」的
**15** 项（同步了哪些文件、同步到哪里），供后续审计核对（AGENTS.md「铁律」）。实现阶段 MUST 一并完成：
`ExamPolicy` 的 4 项小数登记、`SaveFileFormat.Version` 1 → 2 与两张键表、`MonthlySettlementEngine`
类文档的七步 → 八步与 `MUST NOT 存在` 清单移除，以及 spec 的 1 处算式勘误在测试锚点中的落实。
