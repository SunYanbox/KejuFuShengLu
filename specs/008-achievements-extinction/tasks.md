---
description: "Task list for 008-achievements-extinction（逻辑轨 ⑧：68 条成就定义与判定 · 全局 achievements.json · 绝嗣与编年史结局）"
---

# Tasks: 成就与绝嗣结局（逻辑轨 ⑧）

**Feature**: `008-achievements-extinction`（规格书 §16.1 **逻辑轨 ⑧**「成就与绝嗣结局」；§16.2 旧编号「⑪ 成就/绝嗣结局」）

**Input**: Design documents from `/specs/008-achievements-extinction/`

**Prerequisites**: [plan.md](./plan.md)（技术栈、结构、勘误 E-01~E-05、**回写义务清单 10 项**、Complexity Tracking）、
[spec.md](./spec.md)（FR-001~FR-048、US1~US5、SC-001~SC-015、Assumptions 25 条、成就判据总表 68 行）、
[data-model.md](./data-model.md)（层分布、新增类型、形状变更、状态转移、不变量 16 条、持久化形状与版本）、
[contracts/achievement-catalog.md](./contracts/achievement-catalog.md)（契约二十二：68 行三元组表 + 判据阈值表 + 12 个即时入口 + 待接线接缝 + 测试映射）、
[contracts/achievement-store.md](./contracts/achievement-store.md)（契约二十三：`IAchievementStore` / 全局文件 / 合并规则 / 隔离 / 存档形状与版本）、
[contracts/chronicle-ending.md](./contracts/chronicle-ending.md)（契约二十四：绝嗣判据 / 编年史七项 / 三均值 / 拒绝与冻结 / 判定次序）、
[quickstart.md](./quickstart.md)（0 前置 + 场景 1~10 + 回写核对）、
[research.md](./research.md)（R-01~R-21，**未决事项 = 无**）、
[.specify/memory/constitution.md](../../.specify/memory/constitution.md)（章程 v1.1.1）

**Tests**: 本特性**含测试任务**——`spec.md` FR-045~FR-048 明令：68 条成就**逐条**至少一个触发用例与一个
不触发用例（136 条参数化）、绝嗣四正负例、全局文件去重/最早日期/原子性/隔离/失败重试、同种子逐字段一致、
已结束存档的拒绝推进与往返。章程原则 IV 要求「测试与实现**同一提交**给出」，故各故事按
**先写断言（红）→ 实现转绿**的次序组织。

**Organization**: 任务按 user story 分组，使每个故事可独立实现、独立验收；`[Story]` 只出现在用户故事阶段。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 可并行（不同文件、不依赖未完成任务）
- **[Story]**: 该任务所属用户故事（US1 = 68 条定义/判定/全局持久化、US2 = 瞬时与累计事实记账、
  US3 = 绝嗣与编年史、US4 = 结束态状态机与存档语义、US5 = 待接线成就）
- 每条任务都带**精确文件路径**（含行号锚点）与**逐字签名 / 数值**

## Path Conventions

单仓库六工程（`KejuFuShengLu.slnx`），分层：`src/KFL.Core`（状态与不变量）→ `src/KFL.Infrastructure`（落盘形状）→
`src/KFL.Rules`（规则与编排）；`src/KFL.App` 仅组合根**一行级**改动，`src/KFL.Presentation` **一行不动**（本特性纯逻辑轨）。
测试在 `tests/KFL.Tests/`。依赖方向与工程数 MUST NOT 变化（章程 II、G-01~G-08）。

**受限宿主命令口径**（`AGENTS.md` 坑 1 / 坑 2）：一切 `dotnet build` / `dotnet test` MUST 带
`-m:1 -nodeReuse:false`，测试另加 `-p:_MSTestEnableParentProcessQuery=false`；该属性在
`tests/KFL.Tests/KFL.Tests.csproj:21-25` MUST 以**注释态**提交。

**回写前的行号位移提示**：`plan.md`「回写义务清单」表与契约里的行号是**回写前原位**（`plan.md:114` 明写）；
本文件与 Phase 8 引用这些行号时一律保留原位口径，回写完成后以 `git diff` 复核偏移。

---

## 勘误登记（任务阶段，只登记工件内部矛盾，不新创数值）

> 与 `plan.md` 的 E-01~E-05 同性质。真源顺序：规格书（数值与需求）→ `spec.md`（意图）→
> `data-model.md`（模型形状）→ 契约（接口与键表）→ 代码。**一律以更上游者为准，修正下游工件。**

| # | 位置 | 原文 | 本文件采用的口径 | 依据 |
| --- | --- | --- | --- | --- |
| **E-06** | `plan.md:69-70`（「新增 14 个测试文件（`Rules/` 9 + `Infrastructure/` 4 + `Architecture/` 1）」）与 `data-model.md:19`（「`Rules/` 8 个 + `Infrastructure/` 4 个」） | 两处数字自相矛盾，且**均未给 `AchievementDeterminismTests.cs` 落点** | 实为 **15 个**：`Rules/` **10** + `Infrastructure/` **4** + `Architecture/` **1**。第 10 个 = `tests/KFL.Tests/Rules/AchievementDeterminismTests.cs`（SC-003 与 `data-model.md` §6 不变量 15 明写该文件名） | `data-model.md:613`；契约二十二 §5；`spec.md` SC-003 |
| **E-07** | 契约二十二 §5 的测试文件名表 | 使用 `AchievementJudgmentTests.cs` / `AchievementSaveRoundTripTests.cs` / `EndingRefusalTests.cs` / `AchievementArchiveJsonTests.cs`；`plan.md:225-244` 用另一套名字（含 `AchievementPredicateTests.cs` / `AchievementSaveFormatTests.cs` / `EndedSaveTests.cs` / `AchievementAdjudicatorTests.cs` / `AchievementQueryTests.cs`） | **文件名以 `plan.md` 的 Project Structure 为准**（结构权威），契约 §5 的行**逐行映射**到 T029~T035 / T066~T067：`AchievementJudgmentTests` → `AchievementPredicateTests`；`AchievementArchiveJsonTests`（FR-019 兼容语义）**并入** `AchievementStoreTests`；`AchievementSaveRoundTripTests`（SC-011 往返）**并入** `AchievementSaveFormatTests`；`EndingRefusalTests`（SC-010 三面）**并入** `EndedSaveTests`。`AchievementAdjudicatorTests` / `AchievementQueryTests` 为 `plan.md` 独有，**保留**（不合并） | 契约二十二 §5；`plan.md:225-244` |
| **E-08** | 契约二十四 §2「条款 6」表与 `quickstart.md:32-33`、`:51-52` | 契约二十四写「`CenturyHouse` 首次成立时存续 **102**（`CurrentDate.Year == 103`）」「`MillenniumHouse` **302**（Year 303）」；`quickstart.md` 场景 1 写「**六个**系列」（实为 **七**）、系列计数「宗族血脉 **17** / 覆巢之下 **11**」，场景 2 写 `百年世家` 首达 `Year == 103`、`千秋万代` `Year == 303` | **规格书口径**：`SurvivedYears = CurrentDate.Year − 1`（契约二十四 §2 条款 5 自身如此定义）⇒ `> 100` 首达 `SurvivedYears = 101`、`Year == 102`；`> 300` 首达 `301`、`Year == 302`；`绝处逢生` `>= 100` 首达 `100`、`Year == 101`。系列为**七**个，计数 `Emerging 12 / ExamProgression 8 / OfficialCareer 11 / Lineage 12 / CollectiveRuin 16 / Famine 4 / DebugSecrets 5`（合计 68） | `spec.md` Assumptions 5 与 US3 AS4（「存续年数达到 101（= 当前年 102）」）；契约二十二「条款 2」；`data-model.md` §2.1 计数不变量 |
| **E-09** | 契约二十四 §2「条款 6」表与「条款 25」 | 写 `SurvivedAgainstOdds` / `AcedTheLot` 两个**不存在的枚举名**；写「`烟消云散` ACH-**045**」；「条款 18」写「`TukeyTrimean` 有两个调用点……`AchievementPredicates` 的 ACH-012 需要同口径的存续年数」 | 枚举名 MUST 为 `NarrowEscape`（ACH-012）与 `HellEnding`（ACH-048）；`烟消云散` = `FamilyExtinct` = **ACH-044**；`TukeyTrimean` 的调用点只有**编年史 ⑥**（存续年数是整数差，与三均值无关） | 契约二十二 §1 三元组表；`data-model.md` §2.1；契约二十四 §1 条款 1 |
| **E-10** | 契约二十二 §3 第 4/5 行 | 「`LoanOriginated` → 002 **借款发生入口**成功后」「`LoanRepaid` → 002 **提前清偿入口**成功后」（未给路径） | `src/` 现状**无**这两个独立公开入口：借款本金唯一产生路径 = `src/KFL.Rules/Economy/PaymentPrimitive.cs:108-111`（缺口转贷款）；提前清偿 = `FamilyEconomy.RepayLoan`（`src/KFL.Core/Entities/FamilyEconomy.cs:233`，Core 层）。接线点以 T052 的判定为准，并**同一次提交**回写契约二十二 §3 的该两行 | 仓库 grep（`public static` × `Borrow|TakeLoan|RepayLoan|...` 无命中） |
| **E-11** | 契约二十二 §3 第 8/11 行 | 「`DegreeMarriagePurchased` → 007 买功名婚姻**入口**」「`ExamInquiryAnswered` → 005 **科举询问应答入口**」（未给路径） | `DegreeMarriagePurchased` **无独立入口**：它是 `LifecycleEntry.TryMatchmake`（`src/KFL.Rules/Family/LifecycleEntry.cs:40`）与 `RespondToProposal`（`:198`）的 `buyDegreeMarriage` 参数，成功后才上报；`ExamInquiryAnswered` 的既有落点 = `ExamRegistration.AcceptSpecialTribute`（`src/KFL.Rules/Exam/ExamRegistration.cs:170`） | `LifecycleEntry.cs:22-25`（签名差异登记）；`ExamRegistration.cs:170` |
| **E-12** | `plan.md:16` | 「在既有『结算 → 写档 → 备份』编排**之后**追加『成就与绝嗣判定』与『全局成就落盘』两步」 | ② 成就与绝嗣判定在 ① 结算之后、③ 覆盖写档**之前**；⑤ 全局落盘在 ④ 之后。`plan.md:16` 已就地订正；T043 只覆盖 ⑤，② 由 **T090** 补 | `spec.md` FR-034/FR-007；契约二十三 §5 条款 19/22；`quickstart.md` 场景 4 |
| **E-13** | 本文件 T013（`:127`）、T087（`:324`） | 把配置字面量/环境守卫的锚点写成 `SC-008` | `SC-008` 实为「绝嗣判定四条口径」（由 T059 承担）；配置字面量守卫的依据 = **章程「技术栈与工程约束」**，环境守卫 = **G-07**。按 **T091** 在执行时更正 | `spec.md:592`；章程「技术栈与工程约束」 |
| **E-14** | 本文件 T081（`:318`） | 只给裸行号 `:376-390`/`:429-467`，**缺文件路径** | 目标文件 = `specs/007-family-lifecycle/data-model.md`（**T095**） | `plan.md` 回写义务清单第 ⑦ 项 |
| **E-15** | 本文件 T043 | 只覆盖 `Advance` 的 **⑤** 追加，**未覆盖 ② 的插入与入参** | ② 的落点/次序/入参由 **T090** 补齐（判定收在写档之前；全局面以只读参数 `archiveMarks` 传入） | `spec.md` FR-034/FR-007/FR-032；契约二十三 §5 条款 19/22；Edge Case 1 |
| **E-16** | 本文件 T005（`:116`）、T029（`:167`） | 系列计数写 `12 + 8 + 11 + 12 + 16 + 4 + 5`，列「宗族血脉 12 / 覆巢之下 16」 | 以 **E-08** 为准：`12 + 8 + 11 + 17 + 11 + 4 + 5`（**T092**）；`ACH-044~048` 归 `Lineage`、`ACH-049~059` 归 `CollectiveRuin` | 规格书 §14.1 的七个系列标题；`spec.md` 判据总表 :491/:513；`data-model.md:124-125`/`:131`（已订正） |
| **E-17** | 本文件 T008（`:119`） | 编年史 ④ 写 `DegreeLevel HighestDegree`，易读作属性名 | 属性实际为 `Person.CurrentDegree`（`src/KFL.Core/Entities/Person.cs:173`）；`DegreeLevel` 是类型名（**T093**） | 契约二十四 §1 条款 1（已订正） |
| **E-18** | 本文件 T001（`:93-98`）的陈旧注释清单 | 只列 `SaveFileHeader.cs:21`、`SaveFailureTests.cs:23` 两处 | 追加 `src/KFL.Infrastructure/Abstractions/ISaveService.cs:93`（「九条一致性校验」→ 现为十五条；008 落地后按 22 条口径收口）（**T094**；该文件已就地订正为「十五条」） | `src/KFL.Infrastructure/Abstractions/ISaveService.cs:93` |

---

## 待接线成就清单（FR-043 / SC-002 的唯一登记处）

> 本清单是 `spec.md` FR-043 要求的交付物，也是 `quickstart.md` 场景 10 与
> `tests/KFL.Tests/Architecture/AchievementPendingWiringTests.cs`（T071）的断言依据。
> 全部 6 条 MUST 在实现中**恒不解锁**（契约二十二 §4 条款 12）。

| # | 成就 | 缺失的触发源 | 所属后续阶段 | 外部需要上报的内容 | 接线后需新增的测试 |
| --- | --- | --- | --- | --- | --- |
| 1 | ACH-041 `商海浮沉` | 「经商亏损事件」（损失本金 10~30%） | 随机事件 / 经营事件阶段（规格书 §17 已登记该缺口） | 一次亏损事件事实（本金与损失比例），由事件产出方上报 | 亏损 10% / 30% 边界与 <10% / >30% 反例 |
| 2 | ACH-064 `天机泄露` | 唤醒 `Suntion` 调试控制台 **1** 次 | 界面轨 **U4**（`spec.md` §16.1） | `AchievementCounters.ReportsDebugWake(state, count)`（`count >= 1`） | 上报 1 次解锁、0 次不解锁 |
| 3 | ACH-065 `窥探虚实` | 唤醒 **10** 次 | U4 | 同上 | 9 / 10 次边界 |
| 4 | ACH-066 `篡改命数` | 唤醒 **100** 次 | U4 | 同上 | 99 / 100 次边界 |
| 5 | ACH-067 `虚空主宰` | 唤醒 **1000** 次 | U4 | 同上 | 999 / 1000 次边界 |
| 6 | ACH-068 `代码飞升` | 唤醒 **10000** 次 | U4 | 同上 | 9999 / 10000 次边界；且**超过 10000 不清零、不溢出**（`spec.md` Edge Case 13） |

**收尾任务编号（T082 登记）**：本表六行的「恒不解锁 + 接缝存在性 + 边界」由下列收尾任务承接——

| 本表行 | 收尾任务 | 断言内容 |
| --- | --- | --- |
| 1（ACH-041 `商海浮沉`） | **T073** | 谓词 `IsTradeLoss` 本特性恒 `false`；`TradeLossMinPercent = 10` / `TradeLossMaxPercent = 30` 只是**原文登记**，MUST NOT 被读作概率或幅度 |
| 2~6（ACH-064~068 调试五级） | **T072** | 分档边界（0 / 1 / 10 / 100 / 1000 / 10000、9999 vs 10000）、超 10000 不清零不溢出、计数可随档持久化 |
| 1~6 全体（登记处本身） | **T071** | `Source == Pending` 恰 6 条且与本表双向一致；全仓 grep 证明**没有**自造经商亏损规则、**没有**调试控制台实现（SC-002） |
| 1~6 全体（对外口径） | **T074** | 本清单与契约二十二 §4 条款 12 保持同一登记处；若清单变动，同一提交内回写契约 |

**与契约二十二 §4 的逐行对应（T074 核对结论）**：本清单 6 行 = `AchievementSource.Pending` 的**全部** 6 条
（ACH-041 商海浮沉 + ACH-064~068 调试秘辛），与契约 §4 条款 12 的条数、编号、名目**逐行一致**。
契约 §4 另有两行接缝（ACH-047 `四民皆历` 的 `Origins` 读面、trigger ⑪ `ExamInquiryAnswered` 的可选尾参），
它们**不是** `Pending` 源、本特性**已接**（前者在 `TurnRunner` 传参、后者见 `plan.md` 勘误 E-20），
故 MUST NOT 并入本清单——否则会与 T071 的「`Pending` 恰 6 条」断言冲突。

**MUST NOT**（SC-002 的可 grep 断言面）：本特性交付中**0 处**自造的经商亏损事件概率/幅度/触发频率规则；
**0 处**调试控制台或 `Suntion` 唤醒路径的实现。`AchievementPolicy` 只允许登记
`TradeLossMinPercent = 10` / `TradeLossMaxPercent = 30` 这两个**规格书原文**百分比（`AchievementPolicy.cs`），
MUST NOT 由此推出任何掷骰逻辑。

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 先落**存档格式与 004 契约的同步**（`plan.md:293-298` 与 `checklists/requirements.md:83-84` 的硬性前置），
再建新目录骨架，并就地订正已知的工件内部矛盾。

- [X] T001 存档格式 `Version` 4 → 5 与 004 契约同步（**本文件的第一条任务**，SC-013 的逐条核对在此完成）：
      ① 代码侧 `src/KFL.Infrastructure/Save/SaveFileFormat.cs:32` 的 `Version` 由 `4` 改为 `5`；
      ② 两处陈旧注释「恒为 3」→ **5**：`src/KFL.Infrastructure/Save/SaveFileHeader.cs:21`、`tests/KFL.Tests/Infrastructure/SaveFailureTests.cs:23`；
      ③ 回写 `specs/004-save-system/contracts/save-format.md`：`:77` 一带的 `SaveStateDto` **8 → 10** 键（补 `[8] achievements` / `[9] ending`）、`:73-89` 的表补 **5** 个新 DTO 行、版本闸门（`:128`/`:137`/`:150` 语境，**等值**比较）由 `== 4` 改 **`== 5`**、一致性校验由「十五条」改 **22 条**（新增 7 条，**不夹带可重算派生值**）、`:151-153` 的键数常量补 `AchievementStateMemberCount = 8` / `AchievementMarkMemberCount = 4` / `PendingUnlockMemberCount = 5` / `EndingStateMemberCount = 3` / `ChronicleMemberCount = 7`、**后记**追加 008 行（Version 5 / 键表增量 / 校验条数 / 边界声明「仍不引入迁移路径」）；
      ④ 逐条核对并登记「**0 处需改**」的既有版本断言：`tests/KFL.Tests/Infrastructure/SaveFormatContractTests.cs:37,56,270`、`tests/KFL.Tests/Infrastructure/SaveStateValidatorTests.cs:356`、`tests/KFL.Tests/Infrastructure/SaveFailureTests.cs:156-157,183-184,477,503,585-586`（**全部以 `SaveFileFormat.Version` 常量书写**，MUST NOT 改成字面量，MUST NOT 修改其期望值）；
      ⑤ 记录门禁基线：`dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false`、`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false`、`dotnet sln KejuFuShengLu.slnx list`（期望恰好六个工程）、`Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }`（期望**无输出**）
- [X] T002 [P] 建立本特性的两个唯一新目录并确认工程形状零改动：`src/KFL.Rules/Achievements/`、`src/KFL.Infrastructure/Achievements/`；`KejuFuShengLu.slnx`、`global.json`、`Directory.Build.props`、各 `*.csproj` 均 MUST NOT 改动；**零新增 NuGet 依赖**（FR-005）；`src/KFL.Presentation` 一行不动；`src/KFL.App` 仅允许 T044 注入 `IAchievementStore`
- [X] T003 [P] 就地订正本阶段登记的工件矛盾（**只改口径，不新创数值**）：① 契约二十四 §2「条款 6」表的首次成立年数与 `quickstart.md:32-33`、`:51-52` 同步为 E-08 的规格书口径（`Year == 102` / `302` / `101`），`quickstart.md` 场景 1 的「六个系列」改 **七**、系列计数改 **12/8/11/12/16/4/5**；② 契约二十四「条款 6」「条款 25」的枚举名改 `NarrowEscape` / `HellEnding`、`烟消云散` 的编号改 **ACH-044**（E-09）、「条款 18」删去「ACH-012 需要三均值」的错误表述；③ 把 E-06~E-11 登记进 `plan.md` 的「勘误登记（计划阶段）」表**之后**（不覆盖 E-01~E-05，属**追加**）；④ `plan.md:69-70` 与 `data-model.md:19` 的测试文件数同步为 **15**（`Rules/` 10 + `Infrastructure/` 4 + `Architecture/` 1）

**Checkpoint**: `dotnet build` 在 `Version = 5` 下零警告零错误，既有存档测试仍全绿（0 处期望值被改）

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: US1~US5 共同依赖的三层载体——`KFL.Core` 的状态与值对象、`KFL.Rules/Config` 的数值单点、
`KFL.Infrastructure` 的存档形状与全局文件接缝，以及既有断言物的同步。

**⚠️ CRITICAL**: 本阶段完成前，任何用户故事都无法开始

### 内核状态载体（`KFL.Core`）

- [X] T004 [P] `src/KFL.Core/Enums/AchievementId.cs`（新）：`public enum AchievementId`，**恰 68 个成员**，数值 = `ACH-xxx` 的序号（**从 1 起、连续、无洞**），成员名与中文名注释逐字照 `data-model.md` §2.1 的代码块（`FoundationLaid = 1`（开基立业）…… `TradeLoss = 41`（商海浮沉）、`FamilyExtinct = 44`（烟消云散）、`AllFourOrigins = 47`（四民皆历）、`HellEnding = 48`（大梦一场）、`DebugWakeTenThousand = 68`（代码飞升））；成员名 ↔ `ACH-xxx` ↔ 中文名的三元组唯一字典 = 契约二十二 §1，MUST NOT 另立副本
- [X] T005 [P] `src/KFL.Core/Enums/AchievementSeries.cs`（新）：`public enum AchievementSeries { Emerging = 0, ExamProgression = 1, OfficialCareer = 2, Lineage = 3, CollectiveRuin = 4, Famine = 5, DebugSecrets = 6 }`，XML 注释逐条标注成员数与 `ACH` 区间；计数不变量 `12 + 8 + 11 + 17 + 11 + 4 + 5 == 68` 由 T029 断言（**T092 订正**：宗族血脉 17 / 覆巢之下 11，`ACH-044~048 ∈ Lineage`、`ACH-049~059 ∈ CollectiveRuin`）
- [X] T006 [P] `src/KFL.Core/Enums/OriginMilestone.cs`（新）：`public enum OriginMilestone { Extinction = 0, CenturyFamily = 1 }`；**不是**成就标识（`CenturyHouse` 之外的第二类：与 ACH-044 / ACH-045 同一次触发）；MUST NOT 为「千秋万代」加第三项（契约二十三 §9 条款 38 的排除项）
- [X] T007 [P] `src/KFL.Core/ValueObjects/AchievementMark.cs` + `src/KFL.Core/ValueObjects/PendingUnlock.cs`（新）：`public readonly record struct AchievementMark(AchievementId Achievement, Difficulty Difficulty, GameDate CompletedAt);` 与 `public readonly record struct PendingUnlock(AchievementId Achievement, Difficulty Difficulty, GameDate UnlockedAt, Guid SourceSaveId);`；**最早日期语义在合并时体现，不在类型里**（FR-018）
- [X] T008 [P] `src/KFL.Core/ValueObjects/ChronicleSnapshot.cs`（新）：`public sealed record ChronicleSnapshot`，**7 个 `required … { get; init; }`** 逐字照 `data-model.md` §2.2 —— `int SurvivedYears`（① `CurrentDate.Year − 1`）、`int Generations`（② 最大 `Generation` + 1）、`Money PeakTotalAssets`（③ 读累计量 ④）、`DegreeLevel HighestDegree`（④ 全量成员历史最高，取不到用 `DegreeLevel.None`）、`int TotalJinShi`（⑤ 按人去重）、`decimal AverageLifespan`（⑥ 三均值、3 位小数）、`IReadOnlyList<PersonId> Genealogy`（⑦ **次序敏感**，只冻结 `Id` 序列、**不复制 `Person`**）；MUST NOT 加「结局原因」字段（`data-model.md` §2.7）
- [X] T009 `src/KFL.Core/Entities/AchievementState.cs`（新）：`public sealed class AchievementState`，**持久字段恰 8 项**（SC-014：①`int SuccessfulBribeCount`、②`int BirthCount`、③`IReadOnlyList<int> Lifespans`（**完整序列**，非「和 + 个数」）、④`Money PeakTotalAssets`、⑤`bool EverHadLoan`、⑥`int DebugWakeCount`、⑦`IReadOnlyList<AchievementMark> Completed`、⑧`IReadOnlyList<PendingUnlock> PendingUnlocks`，全部 `{ get; private set; }` + `private readonly List<T>`），写通道逐字照 `data-model.md` §2.3：`int RecordSuccessfulBribes(int count)` / `int RecordBirths(int count)` / `void AppendLifespan(int age)`（`age < 0` ⇒ `ArgumentOutOfRangeException`）/ `void ObserveTotalAssets(Money totalAssets)`（取极大值；**负值按 `Money.Zero` 处理**，与 `src/KFL.Rules/Economy/TotalAssetsCalculator.cs:50` 的 `total.IsPositive ? total : Money.Zero` 同口径）/ `void RecordLoanOriginated()`（幂等置真）/ `int ReportDebugWake(int count = 1)` / `bool MarkCompleted(AchievementMark mark)`（键 `(Achievement, Difficulty)` 已存在 ⇒ 不改动、返回 `false`；`CompletedAt` **保留首次**）/ `bool MarkCompletedAndEnqueue(AchievementMark mark, Guid sourceSaveId)`（**唯一**同时写 ⑦⑧ 的入口，保证 `PendingUnlocks ⊆ Completed`）；复原通道 `internal void Restore(...)` 与 `internal void RestorePendingUnlocks(IReadOnlyList<PendingUnlock>)`——`src/KFL.Core/AssemblyInfo.cs:13` 已有 `[assembly: InternalsVisibleTo("KFL.Infrastructure")]`，故取「`internal` 复原」这一变体（MUST NOT 新增 `public` 面）——依赖 T007
- [X] T010 `src/KFL.Core/Entities/EndingState.cs`（新）：`public sealed class EndingState` —— `bool IsEnded { get; private set; }`、`GameDate? EndedAt { get; private set; }`、`ChronicleSnapshot? Chronicle { get; private set; }`、`bool End(GameDate at, ChronicleSnapshot chronicle)`（**幂等**：已结束 ⇒ 原样返回 `false`）、`internal void Restore(bool isEnded, GameDate? endedAt, ChronicleSnapshot? chronicle)`；不变量 `IsEnded ⇔ EndedAt.HasValue ∧ Chronicle is not null` 由唯一私有构造路径保证，并由 T019 的校验条 5 复核——依赖 T008
- [X] T011 `src/KFL.Core/Entities/GameState.cs`：追加 **2** 个 `public` 只读聚合属性（**内联初始化，不进 ctor 参数表**，ctor 保持 **6 参**）——`public AchievementState Achievements { get; } = new AchievementState();`、`public EndingState Ending { get; } = new EndingState();`（`data-model.md` §2.6；`SaveStateMapper` / `NewGameSetup` / 既有测试的构造点 MUST NOT 被迫改动）；MUST NOT 改动 `Family` / `Person` / `FamilyEconomy` / `Holdings` / `Loan` / `PunishmentJournal` / `FamilyLifecycleState`——依赖 T009、T010

### 数值单点（`KFL.Rules/Config`）

- [X] T012 [P] `src/KFL.Rules/Config/AchievementPolicy.cs`（新）：**68 条阈值的唯一真源**（R-18），常量逐字照 `data-model.md` §3.10 的代码块，**分档符号 MUST 逐条保留**：`CenturyFamilySurvivedYears = 100`（**严格 `>`**）、`MillenniumFamilySurvivedYears = 300`（**严格 `>`**）、`NarrowEscapeSurvivedYears = 100`（**`>=`**）、`FiveGenerationsMinGeneration = 4`、`ClanBirthCount = 50`、`RichAssetsGuan = 10_000`、`WealthyAssetsGuan = 50_000`、`LongevityAge = 80`、`VenerableStudyAge = 50`、`VenerableShengShiFailures = 6`、`GeniusStudy = 80`、`DebtBurdenGuan = 100`（**严格 `>`**）、`ExamFailuresPileUp = 20`、`JuRen{Three,Ten,Hundred,Thousand}Threshold`（3/10/100/1_000）、`JinShi{Three,Ten,Hundred,Thousand}Threshold`、`Bribe{Three,Ten,Hundred,Thousand}Threshold`、`LargeHouseholdMembers = 20`、`OfficeHoldersCount = 5`、`PrisonerCount = 3`、`MeritThreshold = 100`、`PrefectRankMin = 5`/`PrefectRankMax = 8`、`FarmlandHundredMu = 100`、`FarmlandFiveHundredMu = 500`、`DebugWake{One,Ten,Hundred,Thousand,TenThousand}`（1/10/100/1_000/10_000）、`TradeLossMinPercent = 10`/`TradeLossMaxPercent = 30`（**仅登记原文百分比，MUST NOT 由此实现掷骰**）、`InfantDeathMaxAge = 3`、`AverageLifespanDecimals = 3`；金额阈值一律经 `Money.FromGuan(配置成员)` 使用，**MUST NOT 在判据里写字面量金额**（契约二十二 §2 条款 4）
- [X] T013 [P] `src/KFL.Rules/Config/GameConfig.cs`：新增**恰 1** 个嵌套 `static class` 转发视图 `GameConfig.Achievements`，逐成员**纯转发**（**零字面量**），风格照既有 `:837` 的 `Lifecycle` 分节（章程「技术栈与工程约束」 / `ConfigLiteralTests`）——依赖 T012
- [X] T014 [P] `tests/KFL.Tests/Architecture/ConfigLiteralTests.cs`：`RegisteredValues` 登记本特性新增的**非整数**阈值（`ConfigLiteralRules` 只守小数/特殊值）；**整数阈值一律不入单**（`ConfigLiteralTests.cs:102-103` 的既有纪律）；**以守卫变红为准确认**，MUST NOT 为了让测试变绿而漏登记或改守卫——依赖 T012、T013

### 存档形状与全局文件接缝（`KFL.Infrastructure`）

- [X] T015 [P] `src/KFL.Infrastructure/Save/Serialization/SaveStateValueDtos.cs`：追加 **5** 个新 DTO，键表逐字照契约二十三 §6「条款 25」——`AchievementStateDto` **8** 键（`[0] int successfulBribeCount`、`[1] int birthCount`、`[2] IReadOnlyList<int> lifespans`、`[3] Money peakTotalAssets`、`[4] bool everHadLoan`、`[5] int debugWakeCount`、`[6] IReadOnlyList<AchievementMarkDto> completed`、`[7] IReadOnlyList<PendingUnlockDto> pendingUnlocks`）、`AchievementMarkDto` **4** 键（`achievement` / `difficulty` / `completedYear` / `completedMonth`）、`PendingUnlockDto` **5** 键（`achievement` / `difficulty` / `unlockedYear` / `unlockedMonth` / `Guid sourceSaveId`）、`EndingStateDto` **3** 键（`endedYear` / `endedMonth` / `ChronicleDto chronicle`）、`ChronicleDto` **7** 键（`survivedYears` / `generations` / `peakTotalAssets` / `highestDegree` / `totalJinShi` / `averageLifespan` / `IReadOnlyList<Guid> genealogy`）；枚举一律写底层整数、年月用两个 `int` 键（**MUST NOT 引入新的日期表示**，契约二十三「条款 26」「条款 28」）
- [X] T016 `src/KFL.Infrastructure/Save/Serialization/SaveStateDto.cs`：键 **8 → 10**，尾部追加 `[8] AchievementStateDto achievements`、`[9] EndingStateDto? ending`；`ending` 用 `null` 表达「未结束」，**JSON 侧 MUST NOT 标 `[JsonRequired]`**，MUST NOT 改成「`bool` + 两个可空字段」的三键表示（契约二十三「条款 26」）——依赖 T015
- [X] T017 `src/KFL.Infrastructure/Save/Serialization/MessagePackSaveFormat.cs`：① `:44` 的 `SaveStateMemberCount` **8 → 10**；② 新增 5 个常量 `AchievementStateMemberCount = 8` / `AchievementMarkMemberCount = 4` / `PendingUnlockMemberCount = 5` / `EndingStateMemberCount = 3` / `ChronicleMemberCount = 7`（`<inheritdoc cref>` 风格照既有）；③ `:238` 的 `Skip(ref reader, 6)` → **`Skip(ref reader, 8)`** 并补齐「多余成员 / 缺失成员」两分支；④ `[9] ending` 走 **`TryReadNil()`** 分支（照 007 对 `proposal` 的既有处理）；⑤ `EnsureVersion` 的比较值 MUST 引 `SaveFileFormat.Version`（**改常量即生效，不得写字面量 5**）；**与 007 的 `FamilyMemberCount = 9` 是不同常量，MUST NOT 混用**——依赖 T016
- [X] T018 `src/KFL.Infrastructure/Save/Serialization/SaveStateMapper.cs:77` 附近：`MapToDomain` 增加**两段**复原调用——`state.Achievements.Restore(...)`（⑦⑧ **逐元素复原并保持次序**）与 `state.Ending.Restore(...)`；复原次序 MUST NOT 反转（先 `Achievements`，再由 `Ending.Restore` 复原快照，契约二十四「条款 30」）；`MapToDto` 侧同步补 ⑧⑨ 键；**MUST NOT 新增 `public` 面**（验收标准 = 在不新增 `public` 的前提下复原，`plan.md:64-66`）——依赖 T009、T010、T016
- [X] T019 `src/KFL.Infrastructure/Save/Serialization/SaveStateValidator.cs`：新增**恰 7** 条一致性校验（既有 15 条**不动**，合计 **22** 条），逐条照契约二十三 §7「条款 32」的表——① `Completed` 内 `(Achievement, Difficulty)` 唯一；② `PendingUnlocks` 内键唯一**且**每个键都出现在 `Completed` 里；③ `Lifespans.Count == Members.Count(p => p.Status.HasFlag(StatusFlag.Deceased))` 且每项 `>= 0`；④ `SuccessfulBribeCount >= 0` ∧ `BirthCount >= 0` ∧ `DebugWakeCount >= 0` ∧ `PeakTotalAssets >= 0`；⑤ `IsEnded ⇔ (EndedAt.HasValue ∧ Chronicle is not null)`；⑥ `IsEnded ⇒ Family.RegisteredMembers.Count == 0`；⑦ `Chronicle.Genealogy` **次序敏感逐元素** == `Family.Members.Select(p => p.Id)` **且** `Chronicle.SurvivedYears == EndedAt.Year − 1`（**MUST NOT 用集合相等**，契约二十三「条款 34」）；违反一律 `SaveFileException(SaveFileErrorKind.InvalidStructure)`；**并列注释写明「明确不校验的清单」**：`PeakTotalAssets` / `HighestDegree` / `TotalJinShi` / `AverageLifespan` 与任何重算值的一致性 MUST NOT 校验（契约二十三「条款 33」）——依赖 T016、T018
- [X] T020 [P] `tests/KFL.Tests/Infrastructure/SaveStateComparer.cs:21`：扩展为比较 `Achievements` 的 **8** 个字段与 `Ending` 的 **3** 个状态字段（含 `Lifespans` / `Genealogy` / `Completed` / `PendingUnlocks` 的**次序**），使既有往返测试自动覆盖新形状（契约二十三「条款 36」；FR-045 的回写义务第 8 项）
- [X] T021 [P] `src/KFL.Infrastructure/Abstractions/IAchievementStore.cs`（新）：`public interface IAchievementStore` —— `string FilePath { get; }`、`AchievementArchive Read()`、`AchievementFlushResult Flush(IReadOnlyList<PendingUnlock> unlocks, IReadOnlyList<OriginRecord> originAdditions)`（**签名以契约二十三 §3「条款 13」为准**：`Origin` 由调用方构造，`AchievementStore` MUST NOT 自己猜）；XML doc MUST 写明「`Read` / `Flush` MUST NOT 抛」的理由（契约二十三「条款 2」）与「`Version >` 当前版本不失败、按未知键忽略」这条与存档本体**相反**的语义（「条款 7」第 4 点）
- [X] T022 [P] `src/KFL.Infrastructure/Achievements/AchievementArchive.cs`（新）：`public sealed record AchievementArchive`（`Version` / `Achievements` / `Origins` / `Diagnostics`）+ `public readonly record struct ArchiveAchievementRecord(AchievementId Achievement, Difficulty Difficulty, GameDate CompletedAt, Guid SourceSaveId)` + `public readonly record struct OriginRecord(Origin Origin, OriginMilestone Milestone, GameDate GameDate, Guid SourceSaveId)` + `public readonly record struct AchievementArchiveDiagnostics(bool WasQuarantined, string? QuarantinePath, string? Reason)`（契约二十三 §2「条款 8」）
- [X] T023 [P] `src/KFL.Infrastructure/Achievements/AchievementFlushResult.cs`（新）：`public sealed record AchievementFlushResult`（`IsSuccess` / `FailureReason` / `QuarantinePath` + `static Success(string? quarantinePath = null)` / `static Failure(string reason, string? quarantinePath = null)`）（契约二十三「条款 9」）
- [X] T024 [P] `src/KFL.Infrastructure/Achievements/AchievementArchiveJson.cs`（新）：JSON 形状（`Version` / `Achievements[]` / `Origins[]`）+ **自带版本常量 `Version = 1`**（**MUST NOT 复用 `SaveFileFormat.Version`**，契约二十三「条款 10」）；解析策略逐条照「条款 7」：未知属性**忽略**、缺失属性取默认（缺 `Achievements` ⇒ 空数组；缺 `Version` ⇒ 视作 `1`）、`Achievements` / `Origins` 元素 **MUST NOT 标 `[JsonRequired]`**、`Version > 当前` ⇒ **不失败**（XML doc 写明与存档等值闸门相反的理由）
- [X] T025 `src/KFL.Infrastructure/Achievements/AchievementStore.cs`（新）：实现在 **`Read()` / `Flush(...)` 均 MUST NOT 抛**（契约二十三「条款 2」）；① 原子替换 MUST 复用 `src/KFL.Infrastructure/Save/AtomicFileWriter.cs` 的 `Write(string, ReadOnlySpan<byte>)`（临时文件 → `Flush(flushToDisk: true)` → `File.Move(..., overwrite: true)`），**MUST NOT 直接 `File.WriteAllText`**；② 落点恒为 `<ISaveService.RootDirectory>/achievements.json`（根目录默认值来自 `src/KFL.Infrastructure/Save/SaveCatalog.cs:143-144`，**本特性不另设根目录**）；③ 合并键 `(Achievement, Difficulty)`，**仅当** `unlock.UnlockedAt < 既有.CompletedAt` 才替换日期与来源（相等或更晚 ⇒ 不动），`Origins` 键 `(Origin, Milestone)` 同规则；④ ⑤ 的写盘内容 MUST 是**全量合并后**的档案（**既有记录 ∪ 本次增量**，MUST NOT 只写增量）；⑤ 损坏隔离：改名为 `achievements.corrupt-<n>.json`（`n` 为**从 1 起的第一个未占用序号**，**MUST NOT 用时间戳**——`DateTime.Now`/`UtcNow`/`Today`/`DateTimeOffset.Now` 在 G-07 的 14 项禁用 token 清单内且扫描范围含 `src\KFL.Infrastructure\`），以空档案继续，诊断经返回值上报，**MUST NOT 删文件、MUST NOT 边隔离边覆盖**，隔离失败（目录只读）⇒ 不隔离不删除、按空档案继续；⑥ 文件**缺失不是损坏**（空档案、无诊断）；⑦ `Read` MUST NOT 做去重/排序/修正（合并是 `Flush` 的职责）——依赖 T021、T022、T023、T024、T002
- [X] T026 [P] `src/KFL.Rules/Save/TurnOutcome.cs`：新增 ⑤ 的落盘结果承载（如 `AchievementFlushResult? AchievementFlush { get; init; }`），并按其既有 record 风格补可选 init 属性使**既有构造点不受影响**；XML doc MUST 写清 ③ 与 ⑤ 的**刻意对比**（③ `ISaveService.Save` 失败**上抛**且不回滚内存状态；⑤ 全局落盘失败**不上抛**、队列保留待重试），理由照契约二十三「条款 22」
- [X] T027 [P] 新建本特性的存档/接缝替身：`tests/KFL.Tests/Rules/RecordingAchievementStore.cs`（记录 `Read`/`Flush` 调用与入参、可注入必然失败）；**MUST NOT** 改动 `tests/KFL.Tests/Rules/RecordingSaveService.cs`、**MUST NOT** 把「记录调用」混进 `tests/KFL.Tests/Infrastructure/SaveTestSandbox.cs`（它已被定位为「存档测试唯一临时目录助手」，契约二十三「条款 37」）
- [X] T028 [P] `tests/KFL.Tests/Rules/RulesTestHarness.cs`：确认 `TurnRunner` 的新增参数是**可选默认 `null`**，既有构造点**可不改**（契约二十三「条款 20」、契约二十三 §8「条款 35」第 3 行）；若测试面确需注入，只**追加可选参**，MUST NOT 改动既有签名

**Checkpoint**: `dotnet build` 零警告零错误、既有测试全绿；US1~US5 可以开始

---

## Phase 3: User Story 1 - 成就自动解锁与全局持久化 (Priority: P1) 🎯 MVP

**Goal**: 交付 §14.1 的 **68** 条成就定义（`AchievementId` / `AchievementCatalog` / `AchievementPredicates`）、
**唯一判定入口** `AchievementAdjudicator`，以及独立于存档的 `achievements.json`（各难度**独立**标记、
该难度**最早**完成年月、来源存档 UUID、原子替换），并让 `TurnRunner.Advance` 的 **⑤ 全局落盘**跑起来。

**Why this priority**: 这是 §14 与 §14.1 的正面需求，也是 006/007 已预留派生谓词
（`PunishmentQueries.FamilyCumulativeBribes`、`WasBribeFreeAtImperialPass`、`WasExecutedByState`、`IsLevelCapReached`；
`LifecycleSnapshot` 六类当月事实）的**唯一消费者**。

**Independent Test**: 注入固定种子的测试驱动器，逐月推进并在任意一点断言 `AchievementState.Completed`
与 `achievements.json` 的内容即可，**不需要任何界面**；也可「构造一个已满足条件的 `GameState` → 调一次 `Judge`」
直接验证单条成就。

### 测试（先写，红色）

- [X] T029 [P] [US1] `tests/KFL.Tests/Rules/AchievementCatalogTests.cs`（新）：`AchievementId` 恰 **68** 个成员；`AchievementCatalog.All` 恰 **68** 行且按 `Id` 升序；`Code` 集合逐字等于 `ACH-001`~`ACH-068`（**无重号、无缺号**，`Id` 数值 == 序号）；七系列计数 `12 / 8 / 11 / 17 / 11 / 4 / 5`（合计 68；**T092 订正**，`ACH-044~048 ∈ Lineage`、`ACH-049~059 ∈ CollectiveRuin`）；枚举与目录**双向一一对应**；`All` 中 `AchievementSource.Pending` 恰 **6** 条（ACH-041、ACH-064~068）；`Of` 非法值 ⇒ `ArgumentOutOfRangeException`；断言表逐行对应契约二十二 §1（SC-001）
- [X] T030 [P] [US1] `tests/KFL.Tests/Rules/AchievementPredicateTests.cs`（新）：**68 × 2 = 136** 条参数化用例（① 构造满足判据的状态面 + 当月事实 ⇒ `IsSatisfied` 为真；② 构造**恰好差一格**的状态 ⇒ 为假）；边界逐条按契约二十二 §2 的阈值列，**下表是最容易被写错的几处**，MUST 逐点钉死：`五世其昌` `Generation >= 4`（**非 `>= 5`**）、`百年世家` `SurvivedYears > 100`（首达 `CurrentDate.Year == 102`）、`千秋万代` `> 300`（首达 `Year == 302`）、`绝处逢生` `>= 100`（**含 100**，首达 `Year == 101`）、`寿比南山` `AgeAt >= 80`（分界月两侧各一例）、`囹圄之灾` 同月 `PrisonerCount >= 3`、`枯木逢春` 上月 `Relief` ∧ 本月脱离 ∧ 实付 **==** 应付、`沉疴难起` 实付 **<** 应付、`名落孙山` `Level == ShengShi ∧ Passed == false ∧ Study >= 80`、`皓首穷经` `AgeAt >= 50 ∧ ShengShiFailureCount >= 6 ∧ 特奏名授官`、`榜下捉婿` 曾出现 `Kind == ImperialProposal`（**含被拒**，只读 `Kind`）、`襁褓之殇` 用**事实月份**的 `AgeAt(死亡月) < 3`（**全特性唯一**用事实月的年龄判据）；6 条 `Pending` **恒不解锁**（无假数据）；**MUST NOT 为 `Pending` 造任何触发规则**（SC-002）；**T097 补引**：同月既达成成就又绝嗣时的**次序**（先成就、后绝嗣）与绝嗣后编年史**冻结**口径见 `spec.md` Edge Case 1 / Assumption 19，由 T031 / T060 逐条钉死（本条只覆盖单条判据的真假）
- [X] T031 [P] [US1] `tests/KFL.Tests/Rules/AchievementAdjudicatorTests.cs`（新）：**唯一次序**（更新累计量 → 判 68 条 → 判绝嗣 的固定先后）；**幂等**（同一状态重复 `Judge` ⇒ `NewlyUnlocked` 为空、`Completed` 不增）；`MarkCompleted` 第二次返回 `false` 且 `CompletedAt` 保留**首次**；同月两条路径命中同一成就 ⇒ 只落**一条**记录；`AchievementOutcome.AlreadyCompleted` 作为幂等证据被填充
- [X] T032 [P] [US1] `tests/KFL.Tests/Rules/AchievementQueryTests.cs`（新）：`AchievementQuery` 的**纯度**断言——每个方法调用前后 `GameState` 逐字段不变、`CountingRandomService` 消费数恰为 **0**、不建视图；`CompletedCount(state)` / `IsCompleted(state, id)` / `SeriesProgress(state, series)` **全部**由 ⑦ 投影（`data-model.md` §3.9：MUST NOT 持久、MUST NOT 立第二真源）
- [X] T033 [P] [US1] `tests/KFL.Tests/Rules/AchievementDeterminismTests.cs`（新，**E-06 补记的落点**）：SC-003 —— 同一种子两次完整推演（含判定与全局落盘）后 `AchievementState` 的 8 个字段与解锁集合**逐字段相同**（可 diff 的单一断言）；判定路径**零随机消费**（_不_新增 `NextDouble`/`Next` 次数）；`Judge` / `Report` MUST NOT 读墙钟、MUST NOT 依赖区域设置
- [X] T034 [P] [US1] `tests/KFL.Tests/Infrastructure/AchievementStoreTests.cs`（新）：文件恒为 `<RootDirectory>/achievements.json`（JSON、**无** MessagePack 侧）；同 `(Achievement, Difficulty)` 重复 `Flush` 只留**一条**且 `CompletedAt` 取**最早**那次（更晚的日期 MUST NOT 覆盖）（SC-004）；来源 UUID = `PendingUnlock.SourceSaveId`（**非**档名；同一 UUID 换档名不产生第二条）（SC-004 / FR-025）；写入中途中断后文件仍可解析且记录集合属于两次状态之一（**原子性**，SC-005）；损坏 ⇒ 改名 `achievements.corrupt-1.json`（序号递增、**不用时间戳**）、空档案继续、诊断可读、**原文件不被删**（SC-006）；文件不存在 ⇒ 空档案且**无诊断**；`Origins` 段**只**写「绝嗣 / 百年世家」两类（**千秋万代不写**，契约二十三 §9「条款 38」）；④ 兼容语义（契约二十二 §5 的 `AchievementArchiveJsonTests` 行**并入本文件**）：未知属性忽略、缺 `Version` 视作 `1`、`Version` 更高**不失败**（FR-019）
- [X] T035 [P] [US1] `tests/KFL.Tests/Infrastructure/AchievementStoreFailureTests.cs`（新）：目录不可写（只读目录或 `RecordingAchievementStore` 注入必然失败）时——① 月结算**照常完成**；② 存档与备份**照常**（`ISaveService` 不受影响）；③ `Flush` 返回 `IsSuccess == false` 且**不抛**；④ `state.Achievements.PendingUnlocks` **保留**；⑤ 恢复可写后**下一次** `Advance` 的 ⑤ **一次性**写入，且 `CompletedAt` 是**原解锁年月**（不是重试当月的年月）（SC-007 / FR-024 / `spec.md` Edge Case 2）

### 实现

- [X] T036 [US1] `src/KFL.Rules/Achievements/AchievementSource.cs`（新）：`public enum AchievementSource { Creation = 0, Degree = 1, Asset = 2, Household = 3, Career = 4, Exam = 5, Punishment = 6, Famine = 7, Lifecycle = 8, Cumulative = 9, Ending = 10, GlobalArchive = 11, Pending = 12 }`；**`Pending` = 待接线**（`TradeLoss` 与调试秘辛 5 条）；`Source` 是**文档性元数据**，MUST NOT 参与判定路径的分支（`data-model.md` §3.2）
- [X] T037 [US1] `src/KFL.Rules/Achievements/AchievementCatalog.cs`（新）：`public sealed record AchievementDefinition(AchievementId Id, string Code, string Name, AchievementSeries Series, AchievementSource Source)` + `public static class AchievementCatalog { public static IReadOnlyList<AchievementDefinition> All { get; } public static AchievementDefinition Of(AchievementId id) public static int CountFor(AchievementSeries series) }`；68 行与契约二十二 §1 的三元组表**逐字对应**（`Code` 用 `"ACH-001"` 形式）；只读、**无可变状态**——依赖 T036
- [X] T038 [US1] `src/KFL.Rules/Achievements/AchievementContext.cs` + `src/KFL.Rules/Achievements/AchievementOutcome.cs`（新）：`public readonly record struct AchievementContext(GameState State, SettlementResult? Settlement, IReadOnlyList<AchievementMark> ArchiveMarks, IReadOnlyList<OriginRecord> ArchiveOrigins, GameDate Now)`（即时路径 `Settlement == null`）；`public sealed record AchievementOutcome`（`required IReadOnlyList<AchievementId> NewlyUnlocked`、`required IReadOnlyList<AchievementId> AlreadyCompleted`、`bool ExtinctionJustTriggered`，前二者按 `Id` 升序）
- [X] T039 [US1] `src/KFL.Rules/Achievements/AchievementPredicates.cs`（新，**前半 ACH-001~031**）：`public static bool IsSatisfied(AchievementId id, AchievementContext ctx)` 作为**唯一分派点**（一个 `switch` 分派到 34 个 `private static` 纯函数）；谓词**只读**参数（MUST NOT 改 `ctx` 里任何对象）、**不消费随机、不触时钟/文件/网络**（G-07 扫描 `src\KFL.Rules\`）；阈值一律引 `AchievementPolicy`（T012），代码里 MUST NOT 出现裸数值字面量；在册 vs 全量口径逐条照 `spec.md` Assumptions 13~14（`三代同堂` / `五世其昌` / `袍笏登场` / `人丁兴旺` 只数**在册**；`寿比南山` / `最高功名` / `进士总数` 数**全量**）——依赖 T012、T037、T038
- [X] T040 [US1] `src/KFL.Rules/Achievements/AchievementPredicates.cs`（新，**后半 ACH-032~043、049~063**）：其余 27 条纯谓词（ACH-044~048 由 US3 的判绝嗣步骤补判，**不在本任务**）；读向逐条照契约二十二 §2「条款 7」：`ACH-012/033/045/046/050~053` 读 `AchievementState` 的累计量（落 US2），判据 MUST NOT 自行扫描历史事实重算（会形成第二真源，且享年序列的三均值不可重算）；`ACH-055` MUST 读「当月 `Caught == true` 的**殿试**行贿事实 ∧ 该『难度 × 殿试』惩罚格为立即执行」（引 `PunishmentPolicy`），**MUST NOT** 用「成员已亡」这一更宽的谓词冒充；`ACH-056` 的后果限 `CollectiveOutcome.Executed` / `.DegreeDemoted`；`ACH-057` 判「连坐事实的亲属**已外嫁归档**」（`StatusFlag.MarriedOut`）——依赖 T039
- [X] T041 [US1] `src/KFL.Rules/Achievements/AchievementAdjudicator.cs`（新，**第 1、2 步**）：`public static AchievementOutcome Judge(GameState state, SettlementResult settlement, IReadOnlyList<AchievementMark> archiveMarks, IReadOnlyList<OriginRecord> archiveOrigins)` —— 本任务只落「**判 68 条**（**不含** `FamilyExtinct` / `HellEnding`）」这一步：新解锁一律走 `AchievementState.MarkCompletedAndEnqueue(mark, state.Id)`，`mark.CompletedAt = state.CurrentDate`、`Difficulty = state.Difficulty`（**MUST NOT 取 `PendingDifficulty`**，`spec.md` Edge Case 3）；`Judge` MUST NOT 触文件系统、时钟、`Random`、`Guid.NewGuid()`（G-07），`now` 取 `state.CurrentDate`（R-06，**不额外传参**）；`state.Ending.IsEnded` 为真时**立即返回空结果**（冻结，与 T050 同一守卫）——依赖 T009、T011、T037、T038、T039、T040
- [X] T042 [US1] `src/KFL.Rules/Achievements/AchievementQuery.cs`（新）：`public static class AchievementQuery` —— `CompletedCount(GameState)`、`IsCompleted(GameState, AchievementId)`、`SeriesProgress(GameState, AchievementSeries)`；**全部**由 ⑦ 投影，MUST NOT 持久、MUST NOT 建视图（供 U1 徽章与 U2 结局画面直接调用，界面无需自行判定）——依赖 T009
- [X] T043 [US1] `src/KFL.Rules/Save/TurnRunner.cs`：① `:58` 的 ctor 追加 **1 个可选参** `IAchievementStore? achievementStore = null`（**MUST 带默认值**，既有构造点无需改动）；② `:79-109` 的 `Advance` 在既有 ④ 之后追加 **⑤ 全局落盘**：`_achievementStore is not null && state.Achievements.PendingUnlocks.Count > 0` ⇒ 用 `state.Origin` 与 `state.CurrentDate`（已结束则 `state.Ending.EndedAt`）构造 `originAdditions`（仅当本次 `PendingUnlocks` 含 `FamilyExtinct` / `CenturyHouse` 时非空）并调 `Flush`；成功 ⇒ `state.Achievements.RestorePendingUnlocks([])` 清空队列，失败 ⇒ **队列保留 + `TurnOutcome` 携带诊断 + 不抛**；③ `_saveService is null` 的短路分支（`:87-94`）**⑤ 仍执行**且返回值 MUST 携带 ⑤ 的结果（契约二十三「条款 21」）；④ 次序 MUST 由**结构**保证，MUST NOT 靠 try/catch 维持（FR-034）；⑤ 本任务 **MUST NOT** 插入结算步骤表（FR-035）、MUST NOT 改 ③ 的异常策略——依赖 T025、T026、T041
- [X] T044 [US1] `src/KFL.App` 组合根：按现状把 `AchievementStore` 注入 `TurnRunner`（可选参，**可暂缺省 `null`**）；MUST NOT 改动 `src/KFL.Presentation`、MUST NOT 新增工程或 `*.sln`——依赖 T025、T043
- [X] T045 [US1] 接线 trigger ①：`src/KFL.Rules/Start/NewGameSetup.cs:99` 的 `Create` **成功之后、返回之前**调 `AchievementAdjudicator.Report(state, AchievementTrigger.NewGameCreated)`，使 `ACH-001 开基立业` 在创建后入 `PendingUnlocks`（来源 UUID = `GameState.Id`、难度 = 本档难度、日期 = 当前游戏年月）；起始年月校验（`NewGameSetup.cs:106-110` 语境）与随机消费次序 MUST NOT 变更（契约二十二 §6 对契约六 §1 的关系）；被拒/失败路径 MUST NOT 调用（契约二十二 §3「条款 9」）——依赖 T050

**Checkpoint**: US1 可独立验收（quickstart 场景 1、2、6、7，以及场景 9 的确定性部分）；MVP 边界 = Phase 1 + Phase 2 + 本阶段

---

## Phase 4: User Story 2 - 瞬时与累计事实的确定性记账 (Priority: P1)

**Goal**: 交付 **严格 8 项**不可重算累计量（FR-013 ①~⑧）的同月同步更新、**12 个动作入口**的即时判定接线，
以及「同随机种子 ⇒ 逐字段相同」的确定性。

**Why this priority**: 成就若靠「月末扫一遍当前状态」实现，瞬时事实（同月 5 人在职、连坐、被释放）
会在月末前消失；若靠「各动作点自己判定」，同一事实会有 N 个出处且不可复现。必须先定死
「事实从哪来、累计量存在哪、何时判定」这一层，US1 才可能正确。

**Independent Test**: 同一 `GameState`（同种子）跑两次 N 个月推演，断言 `AchievementState` 的计数器与
解锁集合逐字段相同；再用人工构造的 `SettlementResult` 断言每一条瞬时判据都能从报告单独触发。

### 测试（先写，红色）

- [X] T046 [P] [US2] `tests/KFL.Tests/Rules/AchievementCounterTests.cs`（新）：① ①~⑤ 的**同月同步**（事实产出当月即更新，MUST NOT 事后回填、MUST NOT 在判定之外更新）；② 增量语义（`Record*` 返回累计值、`AppendLifespan` 逐个追加、`ObserveTotalAssets` **取极大值且负值按 0**、`RecordLoanOriginated` 幂等）；③ ⑥ 只由 `Report` 路径推进（**月末 MUST NOT 重算**）；④ ③ 的**完整序列**语义（`Lifespans.Count == 历史死亡人数`，序列长度可用于反推分位点）；⑤ 两条时点特例：`ACH-034`（当月 `LifecycleSnapshot.PendingProposal?.Kind` ∪ 状态面当前待应答提议的 `Kind`，**不设即时触发器、不加持久字段**）与 `ACH-042`（状态面 `Loan.Principal` ∪ `LoanOriginated` 上报时刻的本金）；⑥ 不可重算验证：8 项**逐项**有「为什么不能从既有持久数据重算」的断言（`data-model.md` §2.4 的证明表 → 测试）
- [X] T047 [P] [US2] `tests/KFL.Tests/Rules/AchievementTriggerTests.cs`（新）：**12 个入口各触发一次**、每个入口的**直接受益成就**至少一条被解锁；**被拒路径 MUST NOT 触发**（校验失败 / 金额不足 / 窗口未开 / 已结束存档）；即时判定 **MUST NOT 落全局文件、MUST NOT 写档、MUST NOT 抛**（契约二十二 §3「条款 10」）；`AchievementTrigger` 的数值 1~12 与契约二十二 §3 的表逐项一致（含 `0` 值语义 = 「非触发路径」）

### 实现

- [X] T048 [US2] `src/KFL.Rules/Achievements/AchievementCounters.cs`（新）：`public static void AbsorbMonthFacts(GameState state, SettlementResult settlement)` —— 逐条照 `data-model.md` §3.1 第 1 步：① 由 `settlement.Punishment.Bribes` 里 **`!Caught`** 的条数调 `RecordSuccessfulBribes`（006 的 `FamilyCumulativeBribes` 计的是**尝试**次数，**MUST NOT 复用**）；② 由 `settlement.Lifecycle.Births.Count` 调 `RecordBirths`（含夭折）；③ 由 `settlement.Lifecycle.Deaths` 逐条取 `person.AgeAt(factMonth)` 调 `AppendLifespan`；④ `ObserveTotalAssets(TotalAssetsCalculator.Of(state.Family.Economy))`；⑤ 由 `state.Family.Economy.Treasury.Loan` 判 `RecordLoanOriginated`；另 `public static int ReportsDebugWake(GameState state, int count)` = ⑥ 的累加（`count >= 1`，否则 `ArgumentOutOfRangeException`）**并与 ACH-064~068 的即时判定放在同一次调用里**（契约二十二 §4「条款 13」，使 U4 只需一个调用点）；`AbsorbMonthFacts` 只做「事实 → 累计量」，与判据职责分离——依赖 T009、T010、T011
- [X] T049 [US2] `src/KFL.Rules/Achievements/AchievementTrigger.cs`（新）：`public enum AchievementTrigger`，**12 项**照契约二十二 §3 的表：`NewGameCreated = 1`、`AssetPurchased = 2`、`AssetSold = 3`、`LoanOriginated = 4`、`LoanRepaid = 5`、`MatchmakingCommenced = 6`、`ProposalAnswered = 7`、`DegreeMarriagePurchased = 8`、`PersonPurchased = 9`、`BribeSubmitted = 10`、`ExamInquiryAnswered = 11`、`DebugWakeReported = 12`；**`0` 值语义 = 「非触发路径」**故首成员从 1 起（与 `CollectiveOutcome` 的「无零值」决定相反，理由 MUST 写进 XML doc）
- [X] T050 [US2] `src/KFL.Rules/Achievements/AchievementAdjudicator.cs`（追加 **`Report`**）：`public static AchievementOutcome Report(GameState state, AchievementTrigger trigger)` —— ① 先判 `state.Ending.IsEnded` ⇒ 立即返回**空结果**（结束态冻结成就判定，契约二十四 §5「条款 23」）；② 必要时先推进 ⑥（仅 `DebugWakeReported`）；③ 判 68 条中与触发器相关族的条目（实现上可全表判，成本可忽略），新解锁 ⇒ `MarkCompletedAndEnqueue(mark, state.Id)`；④ **不落全局文件、不写档、不抛**（契约二十二 §3「条款 10」）；⑤ `Judge`（T041）与 `Report` 共用同一「已结束 ⇒ 空结果」守卫——依赖 T041、T048、T049
- [X] T051 [US2] 接线 trigger ②③：`src/KFL.Rules/Economy/AssetMarket.cs:40` 的 `Buy(GameState, AssetKind, int)` 与 `:77` 的 `Sell(...)` **成功之后、返回之前**各调一次 `Report(state, AssetPurchased / AssetSold)`（使「同月买入后卖出」不漏判，`spec.md` Edge Case 9）；失败路径 MUST NOT 调用——依赖 T050
- [X] T052 [US2] 接线 trigger ④⑤（**E-10：无独立入口，按现状接线并回写契约二十二 §3 的这两行**）：④ 借款本金唯一产生路径 = `src/KFL.Rules/Economy/PaymentPrimitive.cs:108-111`（`economy.Treasury.Loan.Principal += toLoan`）⇒ 在该路径确认「确有一次贷款发生」后上报 `LoanOriginated`；⑤ 提前清偿 = `src/KFL.Core/Entities/FamilyEconomy.cs:233` 的 `RepayLoan`（**Core 层，非 Rules 入口**）⇒ 在自 `KFL.Rules` 调用它的既有动作/结算路径上于**成功后**上报 `LoanRepaid`（若现状无此类调用，MUST 在本任务内定位并在 `tasks.md` 与契约二十二 §3 登记实际落点）；两处 MUST NOT 自造新的借款/清偿规则——依赖 T050
- [X] T053 [US2] 接线 trigger ⑥⑦⑧⑨（**E-11：`DegreeMarriagePurchased` 无独立入口**）：`src/KFL.Rules/Family/LifecycleEntry.cs:40` 的 `TryMatchmake`（托媒成功后上报 `MatchmakingCommenced`；**且当 `buyDegreeMarriage == true` 且本次确实发生了买功名婚姻时**增报 `DegreeMarriagePurchased`）、`:198` 的 `RespondToProposal`（接受**或**拒绝均算「应答成功」⇒ 上报 `ProposalAnswered`；其中接受路径若发生买功名婚姻则增报 `DegreeMarriagePurchased`）、`:461` 的 `BuyPerson`（成功后上报 `PersonPurchased`）；全部 MUST 在**成功之后、返回之前**，被拒路径 MUST NOT 调用（契约二十二 §3「条款 9」）；MUST NOT 改动这些入口的既有随机次序与返回契约——依赖 T050
- [X] T054 [US2] 接线 trigger ⑩：`src/KFL.Rules/Bribery/BribeEntry.cs:41` 的 `Bribe(GameState, Person, ExamLevel, IRandomService)` 成功提交后上报 `BribeSubmitted`（使 `ACH-049~054` 可在当月即时解锁）；被拒路径 MUST NOT 调用；**MUST NOT 改动 006 的掷骰与惩罚次序**——依赖 T050
- [X] T055 [US2] 接线 trigger ⑪（**E-11 的既有落点**）：`src/KFL.Rules/Exam/ExamRegistration.cs:170` 的 `AcceptSpecialTribute(Person, GameDate, IRandomService)` 成功后上报 `ExamInquiryAnswered`（使 `ACH-027`/`ACH-028` 可在应答当月即时解锁）；MUST NOT 改动 `ExamRegistration` 的既有资格与随机次序——依赖 T050
- [X] T056 [US2] 接线 trigger ⑫：把 `AchievementCounters.ReportsDebugWake(state, count)` 作为 **U4 未来的唯一调用点**交付（本特性**不**实现调试控制台、**不**实现 `Suntion` 唤醒路径，FR-041 / SC-002）；上报 MUST NOT 消费随机、MUST NOT 落账本条目、MUST NOT 改家族经济与成员状态（`spec.md` US5 AS3）；`count` 累加 MUST NOT 溢出、MUST NOT 清零，超过 10000 更高档位不再有成就（`spec.md` Edge Cases 13）——依赖 T048、T050
- [X] T057 [P] [US2] 同步既有断言物（**MUST NOT 改期望值去迁就新行为**，FR-047 / SC-015）：`tests/KFL.Tests/Rules/DeterminismTests.cs` 的随机次数断言若因新增判定路径需调整，MUST 改为**相对**断言（「判定路径消费数 == 旧绝对值」）；`tests/KFL.Tests/Rules/SettlementEngineTests.cs` 的步骤表断言 MUST 继续为**十步**且 MUST 断言 `Settle` 仍是 `["Settle"]` 唯一公开实例方法（FR-035：判定与绝嗣 MUST NOT 插入结算步骤表）

**Checkpoint**: US1 与 US2 均可独立验收（quickstart 场景 2、9）

---

## Phase 5: User Story 3 - 绝嗣判定与编年史结局快照 (Priority: P1)

**Goal**: 家族最后一名**在册**成员死去（**外嫁不计**）时判定绝嗣，记录结局年月，冻结**编年史七项**
（存续年数、传承代数、峰值资产、最高功名、进士总数、平均寿命、族谱），并解锁 `ACH-044 烟消云散`
（地狱难度另加 `ACH-048 大梦一场`）；判定次序为**先成就、后绝嗣**。

**Why this priority**: §15「唯一终点 = 家族绝嗣」是整个游戏的**终止条件**，也是 §16.1「⑧」这一编号里的另一半；
没有它，推演就没有终点。

**Independent Test**: 构造一份只剩 1 名在册成员、且该成员寿命必在本月耗尽的存档，推进一回合，
断言 `EndingState.IsEnded`、结局年月、编年史七个字段的具体取值；再用「只剩外嫁归档者」的存档断言**不**判绝嗣。

### 测试（先写，红色）

- [X] T058 [P] [US3] `tests/KFL.Tests/Rules/TukeyTrimeanTests.cs`（新）：**8 行金色算例逐行** `Assert.Equal`（`[50]`→`50.000`；`[40,60]`→`50.000`；`[10,50,90]`→`50.000`；`[10,40,60,70]`→`47.500`；`[10,20,50,60,70]`→`45.000`；`[10,20,30,40,100]`→**`30.000`**；`[]`→`0.000`（**不抛异常、不返回 `null`**）；`[1..9]`→`5.000`）+ **判别例同时断言 `!= 36.250`**（«奇数样本弃中位»的错误变体；`spec.md` SC-009 与 Assumptions 7 逐字要求）；`n = 1/2/3` 照公式算**不设特例分支**；全程 `decimal`、只在最后 `Math.Round(x, 3, MidpointRounding.AwayFromZero)`；**T097 补引**：⑥ 平均寿命进入编年史后**随绝嗣冻结**（不再随新死亡改变），次序与冻结口径见 `spec.md` Edge Case 1 / Assumption 19
- [X] T059 [P] [US3] `tests/KFL.Tests/Rules/ExtinctionTests.cs`（新，**§16.1 必测单测清单第 9 条「绝嗣判定（外嫁不计）」的落点**，FR-048）：**四条口径面** —— ① 在册成员归零 ⇒ `Ending.IsEnded == true` 且 `EndedAt == CurrentDate`；② 仅剩**外嫁归档**者 ⇒ **绝嗣**（外嫁不计）；③ 仅剩服刑者 ⇒ **不绝嗣**；④ 仅剩待阙者 ⇒ **不绝嗣**；⑤ 仅剩患病者 ⇒ **不绝嗣**；⑥ `HeadBecameVacant`（家主空缺）而仍有在册成员 ⇒ **不绝嗣**（MUST NOT 用家主空悬代替绝嗣判据）；另有：`EndedAt` 一旦成立即**冻结**（再次判定不改写）、`End` 幂等（第二次返回 `false`）、`ACH-044`/`ACH-048` 的**补判**（地狱难度另加）、`MUST NOT` 计入 `Family.SettledOutsiders` / `MarriageAlignedOutsiders`
- [X] T060 [P] [US3] `tests/KFL.Tests/Rules/ChronicleTests.cs`（新）：**七项逐项取值断言** —— ① `SurvivedYears == EndedAt.Year − 1`（整数年差，**MUST NOT 换算成月数再除 12**）；② `Generations == Family.Members.Max(p => p.Generation) + 1`（**零成员时为 0 不抛**）；③ `PeakTotalAssets == AchievementState.PeakTotalAssets`（**构造用例**：只减不增的路径上仍取到中间高点）；④ `HighestDegree` 与 ⑤ `TotalJinShi` **含已亡与外嫁归档**（**按人去重**）；⑥ `AverageLifespan` 走后 5 的算例；⑦ `Genealogy` == 全量 `Family.Members.Select(p => p.Id)` 且**次序敏感**；「同月既满足某成就判据又绝嗣」的**次序用例**（**先成就后绝嗣**，成就在绝嗣之前判定，绝嗣当月成立的其它成就（如 `路有冻骨`）不被漏判）；存续年数门槛首达点：`百年世家` 首达 `Year == 102`、`千秋万代` 首达 `Year == 302`、`绝处逢生` 首达 `Year == 101`（E-08 的规格书口径）

### 实现

- [X] T061 [US3] `src/KFL.Rules/Achievements/TukeyTrimean.cs`（新）：`public static decimal Of(IReadOnlyList<int> values)`（`n == 0` ⇒ `0m`）；算法逐字照契约二十四 §4「条款 13」：升序**副本**（MUST NOT 原地改累计量序列）→ `Q2 = median(s)`；下半 `n` 偶数取 `[0, n/2)`、奇数取 `[0, (n+1)/2)`（**含中位**）；上半 `n` 偶数取 `[n/2, n)`、奇数取 `[n/2, n)`（**含中位**）→ `TM = (Q1 + 2·Q2 + Q3) / 4m` → `Math.Round(x, 3, MidpointRounding.AwayFromZero)`；**奇数样本时中位 MUST 同时计入两半**（R `fivenum` 口径），MUST NOT 采用「弃中位」变体、MUST NOT 用线性插值；全程 `decimal`，**MUST NOT 对 Q1/Q2/Q3 各自取整后再合成**；独立成类（**MUST NOT** 内联进 `ChronicleBuilder`，`plan.md:262`）
- [X] T062 [US3] `src/KFL.Rules/Achievements/ExtinctionJudge.cs`（新）：`public static bool IsExtinct(GameState state)` = `state.Family.RegisteredMembers.Count == 0`（**复用既有派生入口，MUST NOT 另立第二套「在册」定义**，FR-002/FR-026）+ `public static bool TryTrigger(GameState state)`（只在 `!state.Ending.IsEnded && IsExtinct(state)` 时动作：调 `ChronicleBuilder.Build` → `state.Ending.End(state.CurrentDate, chronicle)` → 返回 `true`）；MUST NOT 判定`清算/复兴`等不存在分支——依赖 T010、T063
- [X] T063 [US3] `src/KFL.Rules/Achievements/ChronicleBuilder.cs`（新）：`public static ChronicleSnapshot Build(GameState state)` —— 七项数据来源逐条照契约二十四 §1「条款 1」的表；**③ MUST 读累计量 `AchievementState.PeakTotalAssets`，MUST NOT 在本类内重算「月末总资产」**（总资产算式唯一出处 = `src/KFL.Rules/Economy/TotalAssetsCalculator.cs` 的 `Of`：现金 + 储蓄 + 田宅铺市值 − 贷款）；④⑤ MUST 遍历**全量** `Family.Members`（含 `StatusFlag.Deceased` 与外嫁归档）；⑦ 是 `PersonId` 的**次序敏感**序列、**MUST NOT 复制 `Person`**；⑥ 调 `TukeyTrimean.Of(state.Achievements.Lifespans)`——依赖 T008、T009、T061
- [X] T064 [US3] `src/KFL.Rules/Achievements/AchievementAdjudicator.cs`（追加 **第 3 步**）：`Judge` 在判完 68 条之后调 `ExtinctionJudge.TryTrigger`；**首次成立**时**补判** `FamilyExtinct`（ACH-044）与 `Difficulty == Hell` 时的 `HellEnding`（ACH-048），并置 `AchievementOutcome.ExtinctionJustTriggered = true`；**次序 MUST 为「先成就、后绝嗣」（`spec.md` Assumptions 19 与契约二十四 §6「条款 25」为唯一副本）**且同一次编排内完成（FR-032，MUST NOT 拆成两次 `Advance`）；先跑累计量再判绝嗣（编年史 ③ 依赖 ④，`data-model.md` §3.6）——依赖 T041、T062、T063
- [X] T065 [US3] 复核「编年史 ↔ 累计量」的**单向读取**方向（契约二十四「条款 30」）与读档复原次序：`MapToDomain` MUST **先**复原 `Achievements`、**再**由 `Ending.Restore` 复原快照；MUST NOT 让累计量读编年史（方向反转会在读档时产生旧快照覆盖新累计量的路径）——依赖 T018、T063

**Checkpoint**: US1~US3 全部可独立验收（quickstart 场景 3、4、5）

---

## Phase 6: User Story 4 - 结局后的状态机与存档语义 (Priority: P2)

**Goal**: 结局一旦触发，该存档进入**已结束**状态：`Advance` 顶部拒绝推进（`InvalidOperationException`，
**零状态变更**），但写盘 / 读回 / 改名 / 备份仍可用；读档后结束态逐字段复原；全局成就记录**不受**读档或
回退影响；备份可回退到绝嗣之前的月份继续玩。

**Why this priority**: §15 说结局画面提供「读档 / 回主菜单」而界面属 U 轨；逻辑轨必须先给出**可被 U 轨
直接消费的结束状态**与**不会自相矛盾的状态机**，否则 U1 会在结束态上继续推进月份。

**Independent Test**: 在已结束的存档上调用推进一回合，断言以明确异常拒绝；再把该档写盘、读回，
断言结束状态与编年史快照逐字段一致；最后用备份覆盖主文件后推进，断言可以继续玩。

### 测试（先写，红色）

- [X] T066 [P] [US4] `tests/KFL.Tests/Infrastructure/EndedSaveTests.cs`（新，契约二十二 §5 的 `EndingRefusalTests` 行**并入本文件**）：SC-010 的**三面**断言 —— ① `Advance` 抛 `InvalidOperationException`；② **零状态变更**（随机源未被消费 / 账本无新条目 / `CurrentDate` 未推进）；③ 存档文件 **mtime 不变**；另：写盘 / 读回 / 改名 / 备份**仍可用**（`ISaveService.Save` / `Load` / `Rename` / 备份判定 MUST NOT 检查 `Ending.IsEnded`）；**读档后** `Ending` 逐字段复原且再推进**仍被拒**；用 3 回合前的**备份覆盖主文件**后该档可继续推进，且此前已解锁的成就**保持**解锁（`spec.md` US4 AS3）
- [X] T067 [P] [US4] `tests/KFL.Tests/Infrastructure/AchievementSaveFormatTests.cs`（新，契约二十二 §5 的 `AchievementSaveRoundTripTests` 行**并入本文件**）：`SaveFileFormat.Version == 5`；`SaveStateDto` 键 **8 → 10**（`[8] achievements` / `[9] ending`）；`AchievementStateDto` 恰 **8** 键、`AchievementMarkDto` **4**、`PendingUnlockDto` **5**、`EndingStateDto` **3**、`ChronicleDto` **7**；`ending` 为 `null` 时 JSON 侧**不**要求该键、二进制侧走 `TryReadNil()`；两格式（MessagePack / JSON）**信息集等价**、逐字段往返一致（**含 `Lifespans` / `Completed` / `PendingUnlocks` / `Genealogy` 的次序**，SC-011）；`AchievementState` 的持久字段数 **≤ 8**（SC-014，序列化字段清单断言）

### 实现

- [X] T068 [US4] `src/KFL.Rules/Save/TurnRunner.cs` 的 `Advance`：**第一行**（`ArgumentNullException.ThrowIfNull` 之后、任何结算之前）加结束检查 `if (state.Ending.IsEnded) throw new InvalidOperationException("家族已绝嗣，存档不可继续推进。");`（契约二十四「条款 19」）；异常类型取 `InvalidOperationException`（`spec.md` Assumptions 20），**MUST NOT** 自定义异常类型、**MUST NOT** 返回「无变化的 `TurnOutcome`」（静默无操作会让调用方以为推进成功）；抛出前 MUST 零状态变更——依赖 T041
- [X] T069 [US4] 复核「拒绝只发生在 `Advance`」：`ISaveService.Save` / `Load` / `Rename`（`src/KFL.Infrastructure/Save/SaveService.cs`）与 `SaveCadencePolicy` 的备份判定 MUST NOT 检查 `Ending.IsEnded`；`SaveOnExit`（`TurnRunner.cs:118`）MUST NOT 因结束态被跳过，也 MUST NOT 落全局成就文件（契约二十三「条款 18」）——依赖 T068
- [X] T070 [US4] 结束态冻结的集成落点：`AchievementAdjudicator.Judge` / `Report` 在 `state.Ending.IsEnded` 为真时**立即返回空结果**（MUST NOT 消费累计量、MUST NOT 追加待落盘项，契约二十四「条款 23」）；并断言读档复原为已结束态后该冻结仍生效（`spec.md` Edge Case 14：结束态上的上报 MUST NOT 触发任何新解锁）——依赖 T050、T066、T068

**Checkpoint**: US4 可独立验收（quickstart 场景 8）

---

## Phase 7: User Story 5 - 「触发源未交付」成就的接线与待办登记 (Priority: P3)

**Goal**: 交付 6 条「触发源未交付」成就的**成就定义与计数接缝**（本文件「待接线成就清单」为唯一登记处），
**MUST NOT** 自建调试控制台、**MUST NOT** 自造经商亏损事件的概率与幅度。

**Why this priority**: 6/68 条成就的触发源不在本特性范围内；若为此自造规则，会直接违反 §17「禁止自创影响
平衡的核心数值」，并把 U4 的实现方式提前钉死。

**Independent Test**: 以测试驱动器**直接上报**唤醒计数 1 / 10 / 100 / 1000 / 10000 次，断言
`天机泄露` / `窥探虚实` / `篡改命数` / `虚空主宰` / `代码飞升` 逐档解锁；不触碰任何界面代码。

### 测试（先写，红色）

- [X] T071 [P] [US5] `tests/KFL.Tests/Architecture/AchievementPendingWiringTests.cs`（新）：`AchievementSource.Pending` **恰 6** 条，且逐条出现在本文件「待接线成就清单」中（断言清单与目录表**双向一致**）；grep 断言全仓 **0 处**自造的「经商亏损事件」规则常量或判定代码、**0 处**调试控制台 / `Suntion` 唤醒路径实现（SC-002）；5 条调试秘辛在**未上报**时**恒不解锁**（无假数据，契约二十二 §4「条款 12」）
- [X] T072 [P] [US5] 调试唤醒 5 档的**逐档边界**用例（可落在 `T071` 的同一文件或 `tests/KFL.Tests/Rules/AchievementCounterTests.cs`）：0 次不解锁；1 / 10 / 100 / 1000 / 10000 次逐档解锁；**9999 与 10000 的边界**；超过 10000 **不清零、不溢出**、更高档位不再有成就（`spec.md` Edge Cases 13 / US5 AS1）；计数**随档持久化**（读回后不归零）

### 实现

- [X] T073 [US5] `src/KFL.Rules/Achievements/AchievementPredicates.cs` 的 `IsTradeLoss(ctx)`（ACH-041）：**本次恒 `false`** —— 谓词只读「经商亏损事件」事实，而本特性**无产生者**（规格书 §14.1 列了成就但未定义事件）；`AchievementPolicy.TradeLossMinPercent = 10` / `TradeLossMaxPercent = 30` 只作**原文登记**，MUST NOT 由此推出概率 / 幅度 / 触发频率；缺口已登记在本文件清单与规格书 §17 裁决块（FR-042）
- [X] T074 [US5] 维护「待接线成就清单」的**唯一登记处**：本文件上方清单 MUST 与契约二十二 §4「条款 12」的表逐行一致；若实现中清单有增删，MUST 在**同一提交**内同步本文件、契约二十二 §4 与规格书 §17 裁决块（清单变更属跨特性形状变更，AGENTS.md 铁律）

**Checkpoint**: US5 可独立验收（quickstart 场景 10 的前半）

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: AGENTS.md「铁律」的**回写义务 10 项**、SC-013~SC-015 的逐条核对、以及门禁四条与 quickstart 全场景。

- [X] T075 [P] 回写**规格书**（回写义务第 1 项，FR-039）：`科举浮生录规格书.md` ① §14（`:528-554`）的持久化口径补「全局成就文件自带版本号 + 向后兼容 + 损坏隔离 + 写失败不阻断」；② §14.1 的 **6** 条标注（`:600` 榜下捉婿「含被拒」、`:611`/`:612` 的「超过」逐字从严、`:570` 绝处逢生取 `>=`、`:641` 调试秘辛改述为「待 U4 接线」、`:570` 大梦一场）；③ §15（`:643-646`）编年史七项补逐项取值口径（含三均值）；④ §16.1 必测清单第 9 条补「落点 = 008 的 `ExtinctionTests`」；⑤ §17 **追加裁决块「2026-10-18（逻辑轨 ⑧ 的空白填充，Q1~Q6）」**
- [X] T076 回写**004 契约**的终检（回写义务第 2 项，FR-036/FR-037）：`specs/004-save-system/contracts/save-format.md` 的键表已到 **10** 键、版本闸门已写 **`== 5`**、常量登记已含 5 个新 `*MemberCount`、一致性校验已写 **22** 条（15 + 7）、后记已追加 008 行；以 `Select-String -Path "specs\004-save-system\contracts\save-format.md" -Pattern "FormatVersion|MemberCount|十五条|Version"` 核对
- [X] T077 [P] 回写 004 的 `data-model.md`（回写义务第 3 项）：版本行（`FormatVersion` 4 → 5）与键表引用同步；**MUST NOT** 顺手修正 004 `data-model.md` 的其它存量值（`plan.md:117` 的 E-04：`FormatVersion = 2` 属 007 的范围）
- [X] T078 [P] 回写 001 的接缝契约（回写义务第 4 项，FR-033）：`specs/001-core-skeleton/contracts/injection-seams.md:91-99` 的「`IAchievementStore` / `IEventBus` 至今未定义」⇒ `IAchievementStore` 改述为**「已由 ⑧ 定义」**并指向契约二十三 §1；`IEventBus` **维持未定义**；顺带登记「接缝集合自 004 起首增一项」
- [X] T079 [P] 回写 001 的 `data-model.md`（回写义务第 5 项）：只补「`GameState` +2 聚合属性」与不变量 4 的引用注（`IsEnded ⇔ EndedAt ∧ Chronicle`）；**「无写入通道成员全集」与 `Person` 的可写属性清单 MUST NOT 变**（008 不改 `Person`）
- [X] T080 [P] 回写 006 的契约（回写义务第 6 项）：`specs/006-bribery-punishment/contracts/punishment.md` 与 `contracts/config-registry.md` 补一条**读向备注**——`PunishmentQueries.FamilyCumulativeBribes` 的语义是**尝试**次数，而 `暗度陈仓` / `权钱交易` / `只手遮天` / `买断功名` 要的是**成功**次数 ⇒ 后者读 `AchievementState.SuccessfulBribeCount`，MUST NOT 复用前者
- [X] T081 [P] 回写 007 的 `data-model.md`（回写义务第 7 项，AGENTS.md 铁律）：`:376-390` 的形状变更汇总表与 `:429-467` 的持久化节补两行交叉引用——「⑧ 消费 `LifecycleSnapshot` 的六类当月事实」与「⑧ 新增 `EndingState`，与 `Family.Lifecycle` 正交」
- [X] T082 回写范围登记与边界声明（回写义务第 10 项，FR-038）：在本文件登记「上述九项同步了哪些文件、同步到哪里」；登记「本特性**接管** 001 `tasks.md` 中『成就/绝嗣结局属后续阶段』的残句」（001/002/003 的 `tasks.md` 残句**保持原样**，只在 008 登记「已接管」）；并声明**历史记录类工件 MUST NOT 回改** —— `specs/*/tasks.md` 的既有条目、`specs/001-core-skeleton/implementation-notes.md`、既有 `checklists/`；以 `git diff` 核对这三类不得出现语义改写
- [X] T083 [P] SC-013 的登记落地：逐条列出「既有存档测试的版本断言」核对结果——**0 处需改**（`SaveFormatContractTests.cs:37,56,270`、`SaveStateValidatorTests.cs:356`、`SaveFailureTests.cs:156-157,183-184,477,503,585-586` 全部以 `SaveFileFormat.Version` 常量书写）、**2 处陈旧注释已同步**（`SaveFileHeader.cs:21`、`SaveFailureTests.cs:23`）；**0 条未登记**
- [X] T084 [P] SC-014 的审查落地：`AchievementState` 的持久字段**恰 8** 项（== FR-013 的列项数），**不夹带任何可重算的派生值**（代码审查 + `AchievementStateDto` 成员数常量断言 + 序列化字段清单）；`TradeLoss` 的 `Pending` 语义**不新增第 9 项**
- [X] T085 [P] SC-015 / FR-047 的核对落地：本特性 **0 处**修改既有测试的**期望值**来迁就新行为；若确有改动（如 `DeterminismTests` 的随机次数断言相对化），MUST 在本文件与 `checklists/requirements.md` 逐条记录理由
- [X] T086 门禁四条 + quickstart 全场景（交付前必跑）：`dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false`（**0 警告 0 错误**）、`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false`（**全绿**，含 G-01~G-08 与既有 001~007 全部测试）、`dotnet sln KejuFuShengLu.slnx list`（**恰好六个**工程）、`Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }`（**无输出**）；再跑本特性快速回路 `--filter "FullyQualifiedName~Achievement|FullyQualifiedName~Extinction|FullyQualifiedName~Chronicle|FullyQualifiedName~TukeyTrimean|FullyQualifiedName~EndedSave"`；并逐场景执行 `quickstart.md` 的场景 1~10 与「回写核对」的四条 `Select-String`
- [X] T087 [P] 守卫**变红确认**（反证测试）：临时移除一项配置登记，确认 `tests/KFL.Tests/Architecture/ConfigLiteralTests.cs` 变红（章程「技术栈与工程约束」）；在 `src/KFL.Rules/` 临时引入一次禁用 token（如 `DateTime.Now`），确认 `tests/KFL.Tests/Architecture/EnvironmentDependencyTests.cs` 变红（G-07）；确认后**立即还原**，MUST NOT 留下污染
- [X] T088 `spec.md` 真源核对（FR-040）：逐条核对实现与契约是否偏离 `spec.md`；若偏离且 `spec.md`/`plan.md`/契约均未声明，按 `speckit-converge` **追加收敛任务**（MUST NOT 反向推理「代码已经这么写」= 文档过时）；若 `spec.md` 需修正，MUST 与受影响工件**同一提交**落地

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1（Setup）**: 无依赖，可立即开始；**T001 是全局第一条**（`plan.md:293-298`、`checklists/requirements.md:83-84`）
- **Phase 2（Foundational）**: 依赖 Phase 1 —— **阻塞全部用户故事**
- **Phase 3~7（US1~US5）**: 均依赖 Phase 2；按优先级 P1 → P1 → P1 → P2 → P3 顺序推进
- **Phase 8（Polish & 回写）**: 依赖全部期望的用户故事完成；回写 MUST 与实现**同一提交**

### User Story Dependencies

- **US1（P1）**: 依赖 Phase 2；不依赖其它故事 —— **MVP**
- **US2（P1）**: 依赖 Phase 2 与 US1 的 `AchievementAdjudicator`（T041 提供 `Judge`/`Report` 骨架）；
  T045（trigger ①）依赖 US2 的 T050 ⇒ **US1 与 US2 存在一处互相咬合**：先落 T041（Judge 第 1、2 步）与
  T042，再落 T050（Report），最后补 T045
- **US3（P1）**: 依赖 Phase 2 与 US1（累计量 ④ 由 US2 的 T048 更新，编年史 ③ 读它）⇒ 建议 US2 的 T048 先于 T063
- **US4（P2）**: 依赖 Phase 2、US1（T043 的编排）与 US3（`Ending` 的写入门径）
- **US5（P3）**: 依赖 US2（T050 / T056 的上报接缝）；不阻塞任何其它故事

### Within Each User Story

- 测试（红）先写 → 实现转绿；模型（`KFL.Core`）→ 配置（`KFL.Rules/Config`）→ 服务（`KFL.Rules/Achievements`）→ 编排（`TurnRunner`）→ 接线（12 个动作入口）
- 每个故事在 Checkpoint 处停下来独立验收

---

## Parallel Example: Phase 2 的内核与形状

```powershell
# 可并行：三个枚举 + 三个值对象（T004~T008 全是新文件）
Task: "T004 [P] src/KFL.Core/Enums/AchievementId.cs（68 成员，值 1..68 连续）"
Task: "T005 [P] src/KFL.Core/Enums/AchievementSeries.cs（七系列 12/8/11/12/16/4/5）"
Task: "T006 [P] src/KFL.Core/Enums/OriginMilestone.cs（Extinction / CenturyFamily）"
Task: "T007 [P] src/KFL.Core/ValueObjects/AchievementMark.cs + PendingUnlock.cs"
Task: "T008 [P] src/KFL.Core/ValueObjects/ChronicleSnapshot.cs（7 个 required）"

# 可并行：配置单点与 JSON 侧（不同文件、无相互依赖）
Task: "T012 [P] src/KFL.Rules/Config/AchievementPolicy.cs（68 条阈值唯一真源）"
Task: "T022 [P] src/KFL.Infrastructure/Achievements/AchievementArchive.cs"
Task: "T024 [P] src/KFL.Infrastructure/Achievements/AchievementArchiveJson.cs（自带 Version = 1）"
Task: "T027 [P] tests/KFL.Tests/Rules/RecordingAchievementStore.cs（新替身）"
```

## Parallel Example: US1 的测试波次

```powershell
Task: "T029 [P] [US1] AchievementCatalogTests.cs（68 行 / 七系列计数 / Pending 恰 6）"
Task: "T030 [P] [US1] AchievementPredicateTests.cs（68 × 2 = 136）"
Task: "T032 [P] [US1] AchievementQueryTests.cs（查询面纯度）"
Task: "T033 [P] [US1] AchievementDeterminismTests.cs（SC-003）"
Task: "T034 [P] [US1] AchievementStoreTests.cs（SC-004~006 + FR-019）"
Task: "T035 [P] [US1] AchievementStoreFailureTests.cs（SC-007）"
```

---

## Implementation Strategy

### MVP First（US1 only）

1. Phase 1（T001~T003）→ 2. Phase 2（T004~T028）→ 3. US1（T029~T045）
4. **STOP and VALIDATE**：跑 `quickstart.md` 场景 1、2、6、7 + 场景 9 的确定性部分
5. MVP 的边界 = 「68 条定义齐备 + 唯一判定入口 + `achievements.json` 可落盘可重试」

### Incremental Delivery

1. Setup + Foundational ⇒ 骨架就绪（存档 `Version = 5`、形状与接缝齐备）
2. US1 ⇒ 68 条与全局文件可用（MVP）
3. US2 ⇒ 瞬时与累计事实同月同步、12 个入口接线（确定性 SC-003 可断言）
4. US3 ⇒ 绝嗣与编年史（§15 的终点落地）
5. US4 ⇒ 结束态状态机与存档语义（U1 可直接消费）
6. US5 ⇒ 6 条待接线成就的定义与接缝
7. Polish（T075~T088）⇒ 回写 10 项 + 门禁四条 + quickstart 全场景

### 风险与纪律提示

- **T001 不可后置**：`Version` 自增与 004 契约同步是后续所有存档测试的前提（SC-013）
- **`KFL.Rules` MUST NOT 触文件系统/时钟/随机**（G-07 扫描含 `src\KFL.Rules\` 与 `src\KFL.Infrastructure\`）：
  判定器**不注入** store，全局面以**只读参数**传入（R-01）——这是保 SC-003 与纯函数单测的关键
- **全局文件的唯一写点 = `Advance` 的 ⑤**（R-13）：动作型即时判定只入档内 `PendingUnlocks`
- **既有期望值 NOT 改**（SC-015）：`Version` 4 → 5 的既有断言**全部**用常量书写，**0 处**需改（R-20）

---

## Notes

- `[P]` 任务 = 不同文件、无未完成依赖
- `[Story]` 标签用于把任务追溯回 `spec.md` 的 US1~US5
- 每个用户故事都可独立完成与验收；提交信息用中文 `feat(rules): 成就与绝嗣结局…`（章程 V），
  存档格式变更用 `feat(save)!` + `BREAKING CHANGE:` 脚注
- 全部任务完成后 MUST 通过门禁四条（T086），并确认「历史记录类工件未被回改」（T082）

---

## 收敛补充（`$speckit-analyze` 后追加，2026-10-18）

> 本节为**追加登记**，不回改上文任何既有条目（历史记录类工件，见 AGENTS.md「回写义务」）。
> 凡本节与上文冲突者，以本节为准。来源 = 008 的跨工件一致性分析（C-01 / H-01 / H-02 / M-01~M-06 / N-01~N-05）
> 与随之完成的**就地订正**；勘误口径见上表 E-12~E-18。

### 分析后新增/修订任务

- [X] **T089** [P] `src/KFL.Core/ValueObjects/ChronicleSnapshot.cs` 的 ④ 取值口径：`HighestDegree` = **全量** `Family.Members`（含已亡与外嫁归档）`Person.CurrentDegree` 的最大值——MUST NOT 写 `Person.DegreeLevel`（该属性不存在，`DegreeLevel` 是类型名，见 `src/KFL.Core/Entities/Person.cs:173`）；同步契约二十四 §1 条款 1（已改）与 T008 的文本口径
- [X] **T090** [US1] `src/KFL.Rules/Save/TurnRunner.cs`：在 `Advance` 的 ① `Settle` 之后、③ `Save` 之前**插入 ②**「成就与绝嗣判定」（调 `AchievementAdjudicator`，全局面以只读参数 `archiveMarks`（取自 `IAchievementStore.Read()`）传入），再依 ③ → ④ → ⑤ 执行；② 与 ⑤ 在**同一次** `Advance` 内可见；② 抛异常时 MUST NOT 消费随机、MUST NOT 写档（依赖 T042 / T043 / T050；依据 `spec.md` FR-034 / FR-007 / FR-032、契约二十三 §5 条款 19/22、Edge Case 1）——**原 T043 只覆盖 ⑤，本条补 ② 的落点、次序与入参**
- [X] **T091** [P] T013（`:127`）与 T087（`:324`）的**执行前置更正**：把 `SC-008` 改为章程「技术栈与工程约束」（配置字面量守卫）与 G-07（环境守卫）；T087 的「变红确认」对象 = `ConfigLiteralTests` 与 `EnvironmentDependencyTests`，与 SC-008（绝嗣四条口径，由 T059 承担）无关
- [X] **T092** [P] T005（`:116`）与 T029（`:167`）的**执行前置更正**：采用 E-08/E-16 的计数与归属 `Emerging 12 / ExamProgression 8 / OfficialCareer 11 / Lineage 17 / CollectiveRuin 11 / Famine 4 / DebugSecrets 5`；`ACH-044~048` ∈ `Lineage`，`ACH-049~059` ∈ `CollectiveRuin`
- [X] **T093** [P] T008（`:119`）的**执行前置更正**：`ChronicleSnapshot.HighestDegree` 的类型仍是 `DegreeLevel`，但取值口径按 T089 写（`Person.CurrentDegree` 的最大值）
- [X] **T094** [P] T001（`:93-98`）的陈旧注释清单追加 `src/KFL.Infrastructure/Abstractions/ISaveService.cs:93`（「九条一致性校验」→ 十五条；008 落地后按 22 条口径收口）
- [X] **T095** [P] T081（`:318`）补全目标文件路径 = `specs/007-family-lifecycle/data-model.md`（`:376-390` / `:429-467`）
- [X] **T096** [P] FR-031（不实现界面）的显式守卫：并入 T002 的「工程形状零改动」断言——`KFL.Presentation` / `KFL.App` MUST NOT 新增成就相关 UI/ViewModel 类型；并在 T044 的文本位置记录「MUST NOT 改动 `KFL.Presentation`」
- [X] **T097** [US1] T030 / T058 / T064 的任务文本补引 **Edge Case 1**（同月成就与绝嗣的次序必须被测试钉死）与 **Assumption 19**（次序取「先成就、后绝嗣」，编年史在绝嗣成立时冻结），使 FR-032 的次序义务有逐字落点

### 就地订正清单（本次分析后已写入，供 `git diff` 复核）

| 工件 | 订正内容 |
| --- | --- |
| `data-model.md` | 层分布的「②⑤ 均为新增」；测试文件数 15（`Rules/` 10 + `Infrastructure/` 4 + `Architecture/` 1）；系列四/五的编号区间与计数（`:69`/`:82`/`:124-125`/`:131`）；不变量表第 12 行的锚点（章程）；`:714` 的 SC 括注 |
| `contracts/achievement-catalog.md` | 条款 2 计数（`Lineage 17 / CollectiveRuin 11`）；ACH-044~048 的系列列改 `Lineage`；§3 十二条入口补路径（第 2~11 行）；§5 测试映射表按 `plan.md` 文件名重写与 SC-008 锚点更正；§2 条款 4 的锚点；§6 契约十一行的「插入 ② + 追加 ⑤」 |
| `contracts/chronicle-ending.md` | 条款 6 表首达年数（102 / 302 / 101）与枚举名（`NarrowEscape` / `HellEnding`）；条款 18 的调用点（仅编年史 ⑥）；条款 25 的编号（`烟消云散` = ACH-044）；条款 22 的属性名（`Person.CurrentDegree`） |
| `quickstart.md` | 场景 1 的「七系列」与 `12/8/11/17/11/4/5`；场景 2 的 `Year == 102` / `302` / `101` |
| `spec.md` | US2 AS3 改述为「本金**超过** 100 贯」（`:142`）；FR-028 ④ 改 `Person.CurrentDegree`（`:371`） |
| `plan.md` | `:16` 编排措辞（② 插入 / ⑤ 追加）；测试文件数 15（`:69-70`）；「七系列」（`:176`/`:229`）；勘误表追加 E-06~E-18；Project Structure 补 `Rules/AchievementDeterminismTests.cs` |
| `research.md` | R-18 的两处 `SC-008` 锚点改为章程「技术栈与工程约束」（`:538`/`:549`） |
| `src/KFL.Infrastructure/Abstractions/ISaveService.cs` | `:93` 的 `Load` 注释「九条一致性校验」→「十五条一致性校验」（008 落地后由 T001 收口为 22 条） |

### 终检（本次分析后执行）

- [X] 全仓 `grep` 复查残留：`六系列` / `Year == 103` / `Year == 303` / `SurvivedAgainstOdds` / `AcedTheLot` / 系列计数 `12 + 8 + 11 + 12 + 16` 在 `specs/008-achievements-extinction/` 内 0 命中（`tasks.md` 的勘误/收敛段与 `plan.md` 的勘误表作为**登记文本**除外）
- [X] 门禁四条：`dotnet build` 0 警告 0 错误、`dotnet test` 全绿、`.slnx` 恰六工程、无 `.sln`（本次只改工件与注释，不改行为）

---

## Phase 9: Convergence

> 本节由 `$speckit-converge` **追加登记**：不回改上文任何既有条目，不复用/重排既有编号（既有最大编号 = T097）。
> 来源 = 008 的 `spec.md` / `plan.md` / `tasks.md` 与**当前代码与工件**的收敛比对（CF-01~CF-06）。
> 每条末尾括号为缺口类型；全部**不含新增数值**（FR-004），不新增界面（FR-031）。

- [ ] T098 修复门禁的架构守卫误报：`tests/KFL.Tests/Architecture/RepositoryLocator.cs:17` 的 `IgnoredPathSegments = [".git","bin","obj"]` 增补 `.agents`（或改为从 `.gitignore` / `git rev-parse --show-toplevel` 派生忽略集），使嵌套检出 `.agents/worktrees/009-debug-console/`（自带 `KejuFuShengLu.slnx` 与 `src/*/*.csproj`、`*.cs`）不再被当作仓库本体；**MUST NOT 删除或改动 009 的 worktree**；随后重跑门禁四条（`dotnet build` 0 警告 0 错误、`dotnet test` 全绿、`dotnet sln list` 恰六工程、无 `*.sln`）并留档——当前 `G-01 仓库`、`用例六`、`DependencyDirectionTests` 3 条、`TargetFrameworkTests` 2 条共 **7 条失败 / 1279 通过** per FR-044 + 章程「技术栈与工程约束」+ AGENTS.md 门禁四条 (partial)
- [ ] T099 补齐 007 的回写义务：在 `specs/007-family-lifecycle/data-model.md` 的形状变更汇总表（`:376-390`）与持久化节（`:429-467`）各补一行交叉引用——「⑧ 消费 `LifecycleSnapshot` 的六类当月事实（只读）」与「⑧ 新增 `EndingState`，与 `Family.Lifecycle` 正交」；并订正 `specs/008-achievements-extinction/quickstart.md:180,194` 的「已补两行」措辞（当前全仓 `EndingState` 在 007 目录 0 命中，仅有 `:273-287` 的消费注，未补全即声明完成） per plan.md:178（回写义务第 7 行）+ T095 + FR-038 + AGENTS.md「活工件 MUST 与当前实现一致」 (partial)
- [ ] T100 对齐「逐条双测」的实测口径：把 `tests/KFL.Tests/Rules/AchievementPredicateTests.cs:47`、`specs/008-achievements-extinction/tasks.md:190`（T030）与 `plan.md` Project Structure 的「68 条成就各取『正侧』与『邻位侧』」改为实测表述——矩阵 136 行 = **61 真 / 75 假**，覆盖 **60 个标识为真 / 67 个为假**；同处点名 `ACH-044/048`（`tests/KFL.Tests/Rules/ExtinctionTests.cs:136-150`、`tests/KFL.Tests/Rules/AchievementAdjudicatorTests.cs:145-146`）与 `ACH-064~068`（`tests/KFL.Tests/Architecture/AchievementPendingWiringTests.cs:222-227`、`:267-268`）的实际覆盖位置；并把 **ACH-041 无触发用例**（判据恒假，属 FR-042 / Q1-A 的待接线）与 **ACH-001 无不触发用例**（无条件成立）登记为 FR-045 的两处**逐字例外**，避免「68 条逐条双测」被后续审计读成既成事实 per FR-045 (partial)
- [ ] T101 补 8 号触发点的 ACH-035 断言并删除陈旧备注：`tests/KFL.Tests/Rules/AchievementTriggerTests.cs:34-37` 的「ACH-035 在即时判定里无法解锁」已因 E-24 的状态面兜底（`src/KFL.Rules/Achievements/AchievementPredicates.cs:123-125` 的 `|| HasDegreeMarriageRecord(ctx.State)`）而失真，须删除或改写；在触发器八（`:231-247` 买功名婚姻成功路径）上补断言 `AchievementQuery.IsCompleted(state, AchievementId.DegreeMarriage)` 为真，并保留 `Report` 幂等断言 per FR-009（12 个动作入口之第 8 行）+ 契约二十二 §3 第 8 行 + E-24 (partial)
- [ ] T102 同步 008 自身活工件的判定入口签名：`specs/008-achievements-extinction/data-model.md:335-336` 的 `Judge(...)` 声明与 `:557` 的调用示例都补第 4 个参数 `IReadOnlyList<OriginRecord> archiveOrigins`（实现为 4 参：`src/KFL.Rules/Achievements/AchievementAdjudicator.cs:43-47` 同时校验 `archiveMarks` 与 `archiveOrigins`；`:392-393` 的 `AchievementContext` 已含该面，供 ACH-047 使用） per FR-040 + FR-036 + AGENTS.md「活工件 MUST 与当前实现一致」 (partial)
- [ ] T103 清掉两处陈旧的存档版本文案：`src/KFL.Infrastructure/Save/SaveFileException.cs:21` 的 `FormatVersion != 3` 与 `src/KFL.Infrastructure/Save/Serialization/JsonSaveFormat.cs:16` 的 `{"FormatVersion":4,…` 一律改为当前 `SaveFileFormat.Version`（= 5，参照 `src/KFL.Infrastructure/Save/SaveFileHeader.cs:21`「本版本恒为 5」），不改任何行为 per FR-036 / Q3 裁决 + 契约二十三 §6 (partial)

## 回写范围登记（2026-10-19，玩家反馈批次：隐藏成就 + 控制台强制解锁调试秘辛）

> 本节由玩家反馈修复（反馈 12）**追加登记**，不回改上文既有条目，不复用既有编号。
> 授权落点 = `spec.md` 末尾的同日增补（判定条件、解锁时机与 `SaveFileFormat.Version` 均**未变**）。

| 变更 | 同步了哪些文件 |
| --- | --- |
| 隐藏成就展示口径 `AchievementCatalog.IsHidden` | `specs/008-achievements-extinction/spec.md` 增补 §1；`src/KFL.Rules/Achievements/AchievementCatalog.cs`（`RareIds` / `HiddenSources` / `IsHidden`）、`src/KFL.Presentation/ViewModels/AchievementRow.cs`（`IsHidden` / `IsMasked`）、`src/KFL.Presentation/ViewModels/MainViewModel.cs`（`RefreshAchievements` 传 `hidden:`） |
| 调试秘辛可真实触发（`wake` 是唯一合法路径） | `spec.md` 增补 §2；`src/KFL.Presentation/AchievementDescriptions.cs`（ACH-064~068 由「（本版本恒不解锁）」改为「（隐藏成就）」） |
| 控制台 `ach-unlock <ACH-064\|64\|all>` 强制解锁 | `spec.md` 增补 §3；`src/KFL.Presentation/GameSession.cs`（`DebugUnlockAchievement`，写入路径与正常解锁一致）、`src/KFL.Presentation/ViewModels/MainViewModel.cs`（`ExecuteConsole` / `ConsoleCommands`） |

**门禁实测（2026-10-19）**：0 警告 0 错误 / 1286 通过 0 失败 / 恰六工程 / 无 `*.sln`。
