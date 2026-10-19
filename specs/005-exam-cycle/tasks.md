---

description: "Task list for 科举周期（逻辑轨 ⑤）"

---

# Tasks: 科举周期

**Feature**: `005-exam-cycle` | **Input**: 设计文档来自 `/specs/005-exam-cycle/`

**Prerequisites**: [plan.md](./plan.md)（必需）、[spec.md](./spec.md)（必需）、
[research.md](./research.md)、[data-model.md](./data-model.md)、[contracts/](./contracts/)、
[quickstart.md](./quickstart.md)（全部存在且无 `NEEDS CLARIFICATION` 残留）

**Tests**: 本特性**包含测试任务**。依据：spec FR-025 明令交付 US1~US4 与规格书 §16 必测清单的
单测，FR-026 要求同种子逐字段可复现，SC-001~SC-011 的验证物本身就是测试（一个完整科年、
逐乘区封印、免解随机消费 0 次、特奏名问询次数、路费四档、查询面纯函数、存档往返与版本闸门、
配置字面量违规数 = 0）；章程原则 IV「单测优先与结果确定性」；quickstart 的 8 个场景逐条对应
测试文件。故测试任务是**验收物**而不是可选项。

**Organization**: 按 user story 分组（US1~US4，优先级 P1~P4），使每个故事可独立实现、独立验证、
独立交付。Foundational 阶段只放**被多个故事共享**的 `KFL.Core` 状态、共享资格判定、配置数值、
快照形状与日历纯查询。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 可并行（不同文件，且不依赖同一阶段内其它未完成任务的产出）
- **[Story]**: 该任务所属 user story（US1 / US2 / US3 / US4）
- 每个任务都给出精确文件路径；data-model 与契约的字段约束**逐字抄入**任务描述

## Path Conventions

仓库根 `E:\ProjectCsharp\Games\KejuFuShengLu`；源码 `src/`、测试 `tests/`（见 [plan.md](./plan.md)
的「Source Code」）。结构**不变**：仍为六个工程、`.slnx` 唯一、非 UI 三层 `net10.0`、
UI 两层 `net10.0-windows`。**零新增 NuGet 依赖**。`KFL.Presentation` 与 `KFL.App` 在本特性
**一行不动**（FR-029）。

> **门禁命令**（本阶段每一次验证都用它们；受限宿主的两个坑见 `AGENTS.md`）：
> ```powershell
> dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false     # 0 警告 0 错误
> dotnet test  KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false
> dotnet sln   KejuFuShengLu.slnx list                      # 恰好六个工程
> Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }   # 期望无输出
> ```
> `tests/KFL.Tests/KFL.Tests.csproj` 的 `_MSTestEnableParentProcessQuery` MUST 保持**注释态**提交。

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 锁定改动前基线，登记本特性要接入的既有接缝事实（本特性**不新增工程、不新增目录以外
的结构**，Setup 阶段全部为只读核对）

- [X] T001 跑一次门禁基线并记录：`dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false`（0 警告 0 错误）、`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false`（全绿）、`dotnet sln KejuFuShengLu.slnx list`（恰好六个工程）、无 `*.sln`。**不修改任何文件**，把结果登记为本特性改动前的对照基线
- [X] T002 [P] 只读登记结算接缝事实：读 `src/KFL.Rules/Settlement/MonthlySettlementEngine.cs` 的**七步**序列与既有随机消费次序（米价 → 考课 → 储蓄利率（仅 1 月）→ 贷款计息）、`src/KFL.Rules/Settlement/SettlementResult.cs` 的既有子段（尤其 `Career` 的 `required init` 形状）、`src/KFL.Rules/Career/AppointmentEntry.cs` 的 `BeginForImperialGraduate(person, date, random)` 与 `Begin(person, AppointmentTrack, date, random)` 签名与既有校验。**不修改任何文件**
- [X] T003 [P] 只读登记形状范式：`src/KFL.Core/Entities/Person.cs` 的 `AppendDegree` 受控写入通道与「无公开 setter」范式、`src/KFL.Core/Entities/Family.cs` 的 `HasShiStatus` 可写布尔属性范式、`src/KFL.Core/Enums/LedgerCategory.cs` 与 `src/KFL.Core/Enums/LedgerCategoryMetadata.cs` 的既有 **19** 个取值、声明序与两处 `default:` 异常文案。**不修改任何文件**
- [X] T004 [P] 只读登记测试设施：`tests/KFL.Tests/Rules/RulesTestHarness.cs` 的 `FixedRandomService`（可设种子 / 可排队列）能力、`tests/KFL.Tests/Infrastructure/SaveFixtures.cs` 与 `tests/KFL.Tests/Infrastructure/SaveStateComparer.cs` 的夹具与逐字段比较器形状、`tests/KFL.Tests/Architecture/ConfigLiteralRules.cs` 的两条判据与扫描根。**不修改任何文件**

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 所有 user story 都依赖的领域状态、共享资格判定、配置数值单点、快照形状与日历纯查询

**⚠️ CRITICAL**: 本阶段完成前，任何 user story 都不得开工

### 领域状态（`KFL.Core`）

- [X] T005 [P] 新建 `src/KFL.Core/Enums/ExamLevel.cs`：`public enum ExamLevel { JieShi = 1, ShengShi = 2, DianShi = 3 }`。**MUST NOT 设 `None = 0`**（「本月无考试」用 `ExamLevel?` 的 `null` 表达，`0` 与 `> 3` 不是具名成员）；类文档 MUST 引用规格书 §6 并写明「科年与考试月是派生值，MUST NOT 持久化」、报名标记只允许 `JieShi` / `ShengShi`（FR-001、FR-023；契约十二 §2）
- [X] T006 改 `src/KFL.Core/Entities/Person.cs`：新增三个**只读**成员 `public int JieShiFailureCount { get; private set; }` / `public int ShengShiFailureCount { get; private set; }` / `public ExamLevel? AppliedExam { get; private set; }`（均为 `>= 0` / `null ∨ ∈ {JieShi, ShengShi}`）；新增**唯一三条公开写入通道** `public void ApplyForExam(ExamLevel level)`（`DianShi` ⇒ `InvalidOperationException`；已有标记且 `== level` ⇒ no-op 且不抛；已有标记且 `!= level` ⇒ `InvalidOperationException`）、`public void ClearExamApplication()`（`null` ⇒ no-op）、`public void RecordExamFailure(ExamLevel level)`（`DianShi` ⇒ `InvalidOperationException`，否则对应计数 +1、无上限）；新增复原通道 `internal void RestoreExamProgress(int jieShiFailureCount, int shengShiFailureCount, ExamLevel? appliedExam)`（经既有 `src/KFL.Core/AssemblyInfo.cs` 的 `InternalsVisibleTo("KFL.Infrastructure")`）。**MUST NOT 新增任何公开 setter**（`GetSetMethod() == null`）（FR-005、FR-023；data-model §2.2；001 不变量 2）
- [X] T007 [P] 改 `src/KFL.Core/Entities/Family.cs`：新增 `public bool AutoExamApplication { get; set; }`，默认 `false`，形状与既有 `HasShiStatus` 同款；文档口径写明「持续报考开关（规格书 §3、FR-017）」（data-model §2.3）
- [X] T008 [P] 改 `src/KFL.Core/Enums/LedgerCategory.cs` + `src/KFL.Core/Enums/LedgerCategoryMetadata.cs`：追加第 **20** 个取值 `ExamTravelCost`（**声明序置于末尾**，`FamineResolved` 之后），`KindOf(ExamTravelCost) => LedgerEntryKind.Treasury`（资金类）、`CreditTargetOf(ExamTravelCost) => null`（恒为负）；同步两处 `default:` 异常文案与两处 `<exception>` 文档的「共 19 个取值」→「共 20 个取值」，以及 `LedgerCategory` 类文档 19 → 20。**MUST NOT 引入**「买人口」「聘礼/嫁妆」「贿赂」等规格书未定义类别（FR-021；data-model §2.4）

### 配置数值单点（`KFL.Rules/Config`）

- [X] T009 [P] 新建 `src/KFL.Rules/Config/ExamPolicy.cs`，用 `public const` 单点声明全部考试数值（**逐字取自规格书，MUST NOT 自创/改写**）：`MinimumAge = 14`、`JieShiBaseRate = 0.01m`、`ShengShiBaseRate = 0.10m`、`ImperialPromotionBaseRate = 0.05m`、`StudyFactorBase = 0.4m`、`StudyFactorPerPoint = 0.012m`、`OfficialdomDivisor = 400`、`ShiStatusAdmissionBonus = 1.1m`、`SecondClassStudyThreshold = 50`、`DiceRateCap = 0.899999m`、`NeutralBribeMultiplier = 1.0m`、`JieShiExemptionFailureThreshold = 3`、`SpecialTributeMinimumAge = 50`、`SpecialTributeFailureThreshold = 6`，以及 `public static int ExamTravelCostGuan(Difficulty difficulty)`（简单 `10` / 普通 `20` / 困难 `40` / 地狱 `80`，**不乘**难度支出系数）；**MUST NOT** 复制 003 的 `OfficialCareerPolicy` 待阙月数与初始官阶（FR-014、FR-016；契约十四 §1）
- [X] T010 改 `src/KFL.Rules/Config/GameConfig.cs`：新增一个 `Exam` **转发视图**（形如既有 `GameConfig.Career`），**MUST NOT 自带任何数值字面量**（契约十四 §1 条款 2）— 依赖 T009
- [X] T011 [P] 改 `tests/KFL.Tests/Architecture/ConfigLiteralTests.cs`：`RegisteredValues` 增补 **恰好 4 项新小数** `0.01m`（解试基础率）、`0.05m`（进甲基础率）、`0.012m`（学业系数每点增量）、`0.899999m`（封顶），各带规格书出处注释；**MUST NOT 重复登记**同值既有条目 `0.10m`（田租）、`0.4m`（米价派生）、`1.1m`（难度支出系数）；**MUST NOT** 登记任何整数规则值（`14` / `400` / `50` / `3` / `50` / `6` / `10` / `20` / `40` / `80`）与整数值小数 `1.0m`（FR-014、SC-008；契约十四 §3.1~§3.3）— 依赖 T009
- [X] T012 [P] 改 `tests/KFL.Tests/Rules/SpecAnchorTests.cs`：新增「科举数值锚点」节，逐项对照契约十四 §1 的登记表，并写入 spec US1 AS1 的**勘误后**示例值 `0.01 × (0.4 + 0.012 × 60) × (1 + 60/400) × 1.0 × 1.0 = 1.288%`（≈`1.29%`；**不是** 1.42%），锚点 MUST 经 `ExamPolicy` 成员取得（含路费 `Money.FromGuan(ExamPolicy.ExamTravelCostGuan(difficulty))`，MUST NOT 写字面量）（FR-014、SC-008；plan.md「勘误登记」）— 依赖 T009

### 日历、资格判定与快照形状

- [X] T013 新建 `src/KFL.Rules/Exam/ExamCalendar.cs`（静态纯函数类，无字段无状态）：`public static bool IsKeYear(int year)`（`year >= 1 && year % 3 == 1`）、`public static ExamLevel? LevelOf(GameDate date)`（科年 `N` 的 `(N,8)` ⇒ `JieShi`、`(N+1,3)` ⇒ `ShengShi`、`(N+1,4)` ⇒ `DianShi`，其余月份 ⇒ `null`）、`public static GameDate NextExamMonth(GameDate date)`（**严格晚于** `date` 的第一个考试月，含跨年与跨科年边界）；三者 MUST NOT 读 `GameState`、MUST NOT 改状态、MUST NOT 消耗随机、MUST NOT 依赖系统时钟（G-07）；返回类型 MUST NOT 是 `IReadOnlyList` 或任何可变对象（FR-001、FR-018①②；契约十二 §1）— 依赖 T005
- [X] T014 新建 `src/KFL.Rules/Exam/ExamRegistration.cs`，本任务只实现**共享资格判定**两项：`public static ExamLevel? TargetLevelOf(Person person)`（`BaiShen → JieShi`、`JuRen → ShengShi`、`GongShi → DianShi`、`JinShi → null`、其它 ⇒ `ArgumentOutOfRangeException`；「当前功名」= 功名变迁史末条，史为空 ⇒ 白身）与 `public static bool IsEligible(Person person, GameDate date)`（七项全真：`CountedMembers.IsRegistered(person)` 复用既有唯一出处 **MUST NOT 另写一份** ∧ `StatusFlag.ServingSentence` 为假 ∧ `StatusFlag.ExamBanned` 为假（**只消费**，MUST NOT 递减或新建该计时）∧ `Person.Rank is null` ∧ `StatusFlag.AwaitingPost` 为假 ∧ `person.AgeAt(date) >= ExamPolicy.MinimumAge` ∧ `TargetLevelOf(person) is not null`；性别不构成限制、年龄无上限）（FR-002、FR-003；契约十二 §3.1~§3.2）— 依赖 T005、T009、T013
- [X] T015 [P] 新建 `src/KFL.Rules/Exam/ExamSeasonSnapshot.cs`：`public enum ExamAbsenceReason { IneligibleBeforeJudgement, InsufficientTravelCost }`；四个行记录 `ExamApplicationLine(PersonId, ExamLevel)`、`ExamAbsenceLine(PersonId, ExamLevel, ExamAbsenceReason)`、`ExamOutcomeLine(PersonId, ExamLevel, bool Passed, bool Exempted, ImperialClass?, ImperialPlacement?, int FailureCountAfter, DegreeLevel DegreeAfter)`、`ExamAppointmentLine(PersonId, AppointmentTrack, int AwaitingPostMonths)`；快照主体 `ExamSeasonSnapshot` 含 `required` 的 `Month` / `Level`(`ExamLevel?`) / `AutoApplications` / `Absences` / `Exemptions`(`IReadOnlyList<PersonId>`) / `Outcomes` / `Appointments` / `SpecialTributeInquiries`(`IReadOnlyList<PersonId>`) / `HasShiStatusBecomesTrue`(`bool`)，以及 `public static ExamSeasonSnapshot None(GameDate month)`（`Level = null`、全空）；全部集合按 `PersonId` 升序、只读、**增量**、MUST NOT 回写 `GameState`、MUST NOT 成为第二真源；路费金额**不进快照**（已在 `Entries` 的 `ExamTravelCost` 条目里，避免第二个金额真源）（FR-022；data-model §3.5；契约十三 §9）— 依赖 T005
- [X] T016 改 `src/KFL.Rules/Settlement/SettlementResult.cs`：新增 `public required ExamSeasonSnapshot ExamSeason { get; init; }`；既有成员（`Month`、`GrainPrice**`、`LivingCosts`、`Incomes`、`NetProfit`、`Loan**`、`SavingsInterest`、`ArtisanBonus`、`Famine**`、`Entries`、`TreasuryPool**`、`Career`）**一字不改**；同步全部构造点（含测试）使 `required` 成员被满足（FR-022；data-model §4.1）— 依赖 T015

### Foundational 断言物同步

- [X] T017 [P] 改 `tests/KFL.Tests/Core/PersonTests.cs`：把三个新成员并入既有「**无公开 setter**」反射断言集合（`GetSetMethod() == null`），并补 `ApplyForExam` 幂等（同级别重复报名不抛不改字段）、`ApplyForExam(DianShi)` / `RecordExamFailure(DianShi)` 抛 `InvalidOperationException`、`ClearExamApplication()` 对 `null` 为 no-op、`RecordExamFailure` 只 +1 对应计数，覆盖不变量 I-1/I-2/I-3（FR-023；data-model §6）— 依赖 T006
- [X] T018 [P] 改 `tests/KFL.Tests/Core/FamilyTests.cs`：断言 `AutoExamApplication` 默认 `false`、可写、与 `HasShiStatus` 同形（data-model §2.3）— 依赖 T007
- [X] T019 [P] 改 `tests/KFL.Tests/Core/LedgerInvariantTests.cs`：类别计数 **19 → 20**（三处穷举断言：`expected` 数组、`[Theory]` 的 `[InlineData]` 一行、「无正额入账目标」的 9 → 10 元数组），并断言 `ExamTravelCost` 为 `Treasury` 且 `CreditTargetOf == null`（不变量 I-5；FR-021；R-12）— 依赖 T008

**Checkpoint**: 领域状态、配置数值、日历与共享资格判定就绪 — user story 可以开工

---

## Phase 3: User Story 1 - 一届科年走完：解试、省试、殿试 (Priority: P1) 🎯 MVP

**Goal**: 科年 `N` 的 `(N,8)` 解试（录取率 1%）→ `(N+1,3)` 省试（10%，判定前扣路费）→
`(N+1,4)` 殿试（**不掷录取骰**、100% 进士，进甲率 5% 决定甲第）；每次中第追加**恰好一条**
`DegreeRecord(…, DegreeChangeCause.ExamPass, …)`，及第当月经 003 的 `AppointmentEntry`
置入待阙（6~24 月）

**Independent Test**: 固定随机种子，构造一个含适龄白身的家族，按「报名 → 结算考试月」推进一个
完整科年，逐项断言：解试/省试的录取判定与概率公式各乘区、落第计数、功名变迁记录（等级 / 年月 /
`ExamPass` / 甲第 / 一甲名次）、当前功名派生、殿试 100% 中第与甲第判定、及第入仕后的待阙状态位与
`EntryTrack`、待阙月数落在 6~24。全程不需要界面、文件系统或网络（报名标记可在夹具里直接用
`Person.ApplyForExam` 设置，不依赖 US2）

- [X] T020 [US1] 新建 `src/KFL.Rules/Exam/ExamAdmission.cs`（**全为纯函数**）：`public static decimal AdmissionRate(ExamLevel level, Person person, bool hasShiStatus, decimal bribeMultiplier)`（只接受 `JieShi` / `ShengShi`，`DianShi` ⇒ `ArgumentOutOfRangeException`；`= 基础率 × (StudyFactorBase + StudyFactorPerPoint × person.Study) × (1 + person.Talents.Officialdom / OfficialdomDivisor) × bribeMultiplier × (hasShiStatus ? ShiStatusAdmissionBonus : 1.0m)`，**函数内部** clamp 到 `[0, DiceRateCap]`，调用方无绕过分支）、`public static decimal ImperialPromotionRate(Person person, decimal bribeMultiplier)`（`= ImperialPromotionBaseRate × 学业系数 × (1 + 仕天赋/400) × bribeMultiplier`，**MUST NOT 乘仕族加成**，同样内部 clamp）、`public static ImperialClass ImperialClassFor(Person person, bool promoted)`（`promoted ⇒ FirstClass`，否则 `person.Study >= SecondClassStudyThreshold ? SecondClass : ThirdClass`）、`public static ImperialPlacement RollPlacement(IRandomService random)`（`random.Next(1, 4)` **恰好 1 次**，`1 → ZhuangYuan`、`2 → BangYan`、`3 → TanHua`）。四者 MUST NOT 读全局态、MUST NOT 落账本、MUST NOT 改 `Person`（FR-007~FR-010、FR-015、FR-016；契约十三 §4.1~§4.4）
- [X] T021 [US1] 新建 `src/KFL.Rules/Exam/ExamSeasonRunner.cs`，本任务实现**编排骨架 + 判定前复核 + ④ 录取/进甲判定 + ⑤ 及第入仕 + 报名标记清除**：`public static ExamSeasonSnapshot Run(GameState state, IRandomService random)`；非考试月 ⇒ **不消费任何随机、不产生任何变更**、返回 `ExamSeasonSnapshot.None(month)`；考试月按契约十三 §1.2 的六段骨架组织（① 自动报名 / ② 路费与弃考 / ③ 免解留作 US2/US3 的扩展点）；候选集 = 持有本月级别报名标记者，按 `PersonId` **升序**；判定前**重新**复核 `TargetLevelOf(person) == 本月级别 ∧ IsEligible(person, 考试月)`（复用 T014，MUST NOT 另写一份），不合格者**跳过**（不掷骰、不计败、不收路费、不追加功名）并**清除报名标记**、落 `ExamAbsenceLine(…, IneligibleBeforeJudgement)`；④ 按 $1.3 的随机次序逐人 **1 次 `NextDouble()`** 判录取（解试/省试）或进甲（殿试）；**殿试必中不掷录取骰**，进甲者**紧接 1 次** `RollPlacement`；中第经既有 `Person.AppendDegree` 追加**恰好一条**记录（`ChangedAt` = 考试月、`Cause = DegreeChangeCause.ExamPass`、一甲 `FirstClass` 带名次 / 二甲三甲 `ImperialClass` 非空且名次为空），落第 `RecordExamFailure(级别)`；⑤ 在 ④ **全部完成后**按 `PersonId` 升序对殿试及第者调用 `AppointmentEntry.BeginForImperialGraduate(person, 考试月, random)`（**MUST NOT** 自行写 `Person.Rank` / `MonthsInOffice` / 账本，MUST NOT 参与待阙递减与授官）；④⑤ 之后按 FR-020 置 `Family.HasShiStatus = true`（幂等，快照 `HasShiStatusBecomesTrue` 仅首次为真）；无论中第/落第/免解/弃考/资格不符，本月结算结束后 `AppliedExam` 必为 `null`。`Run` 的可变范围严格限定为 `Person`（`ApplyForExam` / `ClearExamApplication` / `RecordExamFailure` / `AppendDegree`）、`Family.HasShiStatus`、`FamilyEconomy.Apply(LedgerCategory.ExamTravelCost, …)`、及 `AppointmentEntry` 入口（FR-006~FR-011、FR-019~FR-022；契约十三 §1~§2、§6、§7）
- [X] T022 [US1] 改 `src/KFL.Rules/Settlement/MonthlySettlementEngine.cs`：把科举季插为**第 ⑦ 步**（位于「⑥ 贷款」之后、「⑧ 推进时间（`GameState.AdvanceMonth`）」之前，原第 ⑦ 步顺移为第 ⑧ 步），在 `Settle` 内调用 `ExamSeasonRunner.Run(state, random)` 并把返回快照填入 `SettlementResult.ExamSeason`；类文档**七步 → 八步**，`MUST NOT 存在` 清单**移除**「科举季触发（阶段 ⑤）」，随机消费次序补入「科举判定（逐人：1 次录取/进甲，进甲者紧接 1 次名次）→ 及第入仕待阙（1 次 `Next(6, 25)`，按 `PersonId` 升序）」；本步 MUST NOT 调用 `AdvanceMonth`、MUST NOT 修改 `CurrentDate`（FR-019、FR-028③；契约十三 §1.1）— 依赖 T021
- [X] T023 [P] [US1] 新建 `tests/KFL.Tests/Rules/ExamAdmissionTests.cs`（本任务只建**录取率/进甲率/甲第**分区，免解分区由 T036 追加）：逐乘区封印 SC-002 —— 基础率 `0.01`/`0.10`/`0.05`、学业系数 `0.4 + 0.012 × 学业`、仕天赋项 `1 + 仕/400`、仕族加成 `1.1` **只作用于录取率**（进甲率在 `HasShiStatus` 真/假时结果**完全相同**，差异项 = 0）、封顶 `0.899999` 在解试/省试录取率与殿试进甲率**三处**生效且**殿试录取率不受封顶影响**（100% 必中）、贿赂乘数经**同一入口的形参**接收（本轮恒 `ExamPolicy.NeutralBribeMultiplier`）、US1 AS1 的示例率 `1.288%`（含 `hasShiStatus = false`）、`RollPlacement` 映射与均匀性；另断言功名链 SC-003（白身→举人→贡士→进士恰好 3 条 `ExamPass`、当前功名 = 末条、一甲「名次非空 ⟺ `ImperialClass == FirstClass`」违例数 = 0、二甲/三甲名次全空）；随机一律经 `RulesTestHarness.FixedRandomService`（FR-007~FR-010、FR-015、FR-016、FR-025、FR-026；SC-002/SC-003）
- [X] T024 [P] [US1] 新建 `tests/KFL.Tests/Rules/ExamSeasonTests.cs`（本任务只建**完整科年 + 步骤位置与随机次序 + `HasShiStatus`** 分区，路费/弃考分区由 T031 追加）：跑完 `(N,8) → (N+1,3) → (N+1,4)` 并断言 SC-001 同种子两次独立运行**逐字段差异项 = 0**（含录取/进甲结果、一甲名次、两个累计失败数、功名变迁记录、待阙月数）；断言科举季位于「⑥ 贷款之后、`AdvanceMonth` 之前」（判定用的是考试当月日期与当月值）、非考试月随机消费 **0** 次且状态变更 **0** 处、④⑤ 的随机消费次序可**逐次**断言（先全部判定、再全部入仕，MUST NOT 逐人交错）、同一结算内 `AppendDegree` 追加条数 = 中第人数、及第当月 `StatusFlag.AwaitingPost` 为真 / `EntryTrack` 对应一甲/二甲/三甲 / 待阙月数落在 `[6, 24]` 且**当月不递减**、本特性 MUST NOT 写 `Person.Rank`；断言 `HasShiStatus` 首个生产者语义（当月置真、幂等、置位当月 `HasShiStatusBecomesTrue == true` 仅首次、对贷款月划扣比例的影响自**次月**生效）；3 月省试与 4 月殿试必须各自产生**独立**快照 MUST NOT 合并（FR-010、FR-019~FR-022、FR-026、FR-027；SC-001）
- [X] T025 [P] [US1] 改 `tests/KFL.Tests/Rules/DeterminismTests.cs`：新增科举季一节 —— 相同种子 + 相同操作序列跑两遍完整科年，逐字段差异项 = 0（章程原则 IV；FR-026；SC-001）

**Checkpoint**: US1 独立可用 —— 一个完整科年可从规则层跑通并被纯函数断言

---

## Phase 4: User Story 2 - 报名、路费与持续报考 (Priority: P2)

**Goal**: 报名窗口 = **考试当月**（解试仅科年 8 月、省试仅次年 3 月）、幂等、不消耗随机、不读写
账本；省试路费按难度直接取档（10/20/40/80 贯，**不乘**难度支出系数）、**判定前扣**、**落第不退**，
现金不足 ⇒ **弃考并提示**（不计败、不落条目、不转贷款）；`Family.AutoExamApplication` 开启时在
报名月**自动报名**并在**同一结算内**完成判定

**Independent Test**: 固定种子与年月，断言报名窗口的边界（科年 8 月、次年 3 月有效；7 月/9 月、
4 月无效）、报名幂等（重复报名不改字段、不重复扣费、不消耗随机）、持续报考开启时 8 月与 3 月的
自动报名与自动判定、路费按难度取值且不乘支出系数、现金不足 ⇒ 弃考（不计败、无账本条目）

- [X] T026 [US2] 改 `src/KFL.Rules/Exam/ExamRegistration.cs`（在 T014 建立的同一文件内追加）：`public static bool CanRegister(Person person, GameDate date)`（`ExamCalendar.LevelOf(date)` 为 `JieShi` / `ShengShi` **∧** `IsEligible(person, date)` **∧** `TargetLevelOf(person) == LevelOf(date)`；对殿试月**恒 `false`**）与 `public static void Register(Person person, GameDate date)`（`!CanRegister` ⇒ `InvalidOperationException` 且 **MUST NOT 改任何字段**（含失败数、功名史、账本、随机状态））；同一人同一级别重复 `Register` MUST **幂等**（不抛、不改字段、不重复消费）；报名 MUST NOT 消耗随机、MUST NOT 读写账本、MUST NOT 追加功名记录（FR-004、FR-005；契约十二 §4）
- [X] T027 [US2] 改 `src/KFL.Rules/Exam/ExamSeasonRunner.cs`（在 T021 建立的同一文件内追加，**不并行**）：实现 ① **自动报名**（仅当 `Family.AutoExamApplication` 为真；名单 = `ExamRegistration.EligibleUnapplied(family, date)` 按 `PersonId` 升序；**MUST NOT 消耗随机**、MUST NOT 读写账本、MUST NOT 覆盖已有标记）与 ② **路费扣除与弃考**（仅省试月；门槛 = **现金**：`state.Economy.Treasury.Cash < ExamPolicy.ExamTravelCostGuan(state.Difficulty)` ⇒ 弃考：清除报名标记、不掷骰、**失败数不变**、**不落账本条目**、**不转贷款**，快照落 `ExamAbsenceReason.InsufficientTravelCost`；付得起时调用 `state.Economy.Apply(LedgerCategory.ExamTravelCost, person.Id, -cost, 考试月)` —— 因门槛保证了 `Cash >= 路费`，扣付**必然只动现金**，MUST NOT 动储蓄、**MUST NOT 使用 `PaymentPrimitive`**、MUST NOT 部分支付）；解试与殿试 MUST NOT 收任何路费；`SettlementResult.Entries` 中 `ExamTravelCost` 条目数 MUST 等于实际收费人数（FR-008、FR-017、FR-021；契约十三 §1.2、§3.1~§3.2）
- [X] T028 [P] [US2] 新建 `tests/KFL.Tests/Rules/ExamRegistrationTests.cs`（本任务只建**资格/级别/窗口/幂等**分区，特奏名接受面由 T034 追加、查询面纯函数分区由 T038 追加）：断言 `TargetLevelOf` 四级映射与「进士 ⇒ `null`」、`IsEligible` 七项各自为假时不合格（`CountedMembers.IsRegistered` 复用、服刑、`ExamBanned`、有官阶、待阙、`AgeAt(date) < 14`、无可报级别）、性别不构成限制且年龄无上限、窗口边界（科年 8 月与次年 3 月有效；7 月 / 9 月 / 4 月与殿试月无效）、窗口外 `Register` 抛且**逐字段不变**、`Register` 幂等（不抛、字段不变、随机消费 **0** 次、账本 **0** 条）、`Person.ApplyForExam(DianShi)` 抛；随机一律经 `FixedRandomService`（FR-002~FR-005；I-7；SC-007 后半）
- [X] T029 [P] [US2] 改 `tests/KFL.Tests/Rules/ExamSeasonTests.cs`（在 T024 建立的同一文件内追加）：路费与弃考分区 SC-006 —— 四种难度下省试路费分别为 **10 / 20 / 40 / 80 贯**（期望金额 MUST 经 `ExamPolicy.ExamTravelCostGuan(difficulty)` 取得，MUST NOT 写 `Money.FromGuan(20)` 字面量）、与**难度支出系数无关**（切换难度前后金额只随难度档取值变化）、**落第不退**（中第与落第的账本条目数各为 1）、`ExamTravelCost` 条目为资金类且金额为负、`PersonId` = 考生本人、账本条目年月 = 考试月、现金不足 ⇒ 条目数 **0** 且失败数不变且**不转贷款**、快照落 `InsufficientTravelCost`；持续报考分区 —— 开启后科年 8 月与次年 3 月**自动报名并在同一结算内完成判定**（报名阶段随机消费 0 次）、关闭后 MUST NOT 自动报名、错过某科年不计败且不影响后续科年（FR-005、FR-008、FR-017、FR-021；I-9/I-10/I-15；SC-006）

**Checkpoint**: US1 与 US2 均独立可用 —— 报名闸门、路费与弃考全部可断言

---

## Phase 5: User Story 3 - 免解与特奏名：老考生的两条出路 (Priority: P3)

**Goal**: **免解** —— 解试累计失败数 ≥ 3 者此后每次解试**不掷录取骰**直接按中第处理（随机消费
**0** 次），且解试失败数不再增长；**特奏名** —— 满 50 岁 ∧ 省试累计失败数 ≥ 6 ∧ 无官阶 ∧ 非待阙者，
在**省试落第当月**产出**恰好一次**询问（不改任何状态）；接受 ⇒ 经
`AppointmentEntry.Begin(person, AppointmentTrack.SpecialTribute, date, random)` 入待阙（最终 L18）、
**功名史不新增任何记录**、此后退出科举；拒绝/未应答 ⇒ 逐字段不变、下次省试落第后**再次**询问

**Independent Test**: 断言解试失败数到 3 后下一次解试**不掷骰**直接中第（随机消耗次数 = 0）、
其后解试失败数不再增长；断言 50 岁 + 省试 6 败者在省试落第当月产出特奏名询问事件，接受后
`EntryTrack` = `SpecialTribute`、**功名史没有新增任何记录**、待阙与后续授官为 L18；拒绝后状态与
功名史逐字段不变，且下次省试落第后再次询问

- [X] T030 [US3] 改 `src/KFL.Rules/Exam/ExamRegistration.cs`（在 T014/T026 建立的同一文件内追加）：`public static bool SpecialTributeEligible(Person person, GameDate date)`（`person.AgeAt(date) >= ExamPolicy.SpecialTributeMinimumAge`（50）∧ `person.ShengShiFailureCount >= ExamPolicy.SpecialTributeFailureThreshold`（6）∧ `person.Rank is null` ∧ `StatusFlag.AwaitingPost` 位为假）与 `public static void AcceptSpecialTribute(Person person, GameDate date, IRandomService random)`（`!SpecialTributeEligible` ⇒ `InvalidOperationException` 且 **MUST NOT 改任何字段**；否则 `AppointmentEntry.Begin(person, AppointmentTrack.SpecialTribute, date, random)`；**MUST NOT 追加任何功名记录、MUST NOT 伪造进士记录**）；**MUST NOT 提供「记录拒绝」的持久字段**（会成为第二真源，拒绝 = **不调用**）（FR-012；契约十二 §6.2；data-model §5.4）
- [X] T031 [US3] 改 `src/KFL.Rules/Exam/ExamSeasonRunner.cs`（在 T021/T027 建立的同一文件内追加，**不并行**）：实现 ③ **免解行使**（仅解试月；门槛用**进入本步之前**的失败数 `person.JieShiFailureCount >= ExamPolicy.JieShiExemptionFailureThreshold`（3）；该类成员 MUST NOT 掷录取骰、直接按**中第**处理、随机消费 **0** 次；「中第」若当前功名**低于举人** ⇒ 追加一条 `JuRen / ExamPass` 记录，**已达举人或更高** ⇒ MUST NOT 追加重复记录（仍算中第、仍清除标记）；行使记入快照 `Exemptions` 与该行 `Exempted = true`；本段 MUST 在 ④ 的掷骰**之前**判定，否则会多消耗一次随机）与 ⑥ **特奏名询问**（仅省试月；落第者满足 `SpecialTributeEligible` 时在**落第当月**产出**一次**询问，**MUST NOT 改任何状态**——不改功名、不改状态位、不落条目、不掷骰，写入 `SpecialTributeInquiries` 按 `PersonId` 升序；解试月与殿试月 MUST NOT 产生任何询问）（FR-012、FR-013、FR-019；契约十三 §1.2 条款 4/6、§5、§8）
- [X] T032 [P] [US3] 新建 `tests/KFL.Tests/Rules/ExamSpecialTributeTests.cs`：SC-005 —— 满 50 岁 + 省试 6 败者的落第当月产出**恰好 1 次**询问且询问本身**不改任何状态**（功名史、状态位、账本条目全部不变，不掷骰）；**拒绝**（= 不调用 `AcceptSpecialTribute`）后状态与功名史**逐字段不变**、下次省试落第后**再次**询问（次数 = 1）；**接受**后 `EntryTrack == AppointmentTrack.SpecialTribute`、置入待阙（6~24 月）、最终授官 **L18**（经 `OfficialCareerPolicy.InitialRankOf` 单点取得，MUST NOT 在本特性另立第二处）、**功名变迁记录条数不变**、接受后该成员**退出科举**（不再被自动报名、不再进入判定，由既有资格条款结构性保证）；`AcceptSpecialTribute` 不满足条件时抛且不改任何字段；解试月与殿试月不产生询问（FR-012、FR-025；SC-005；I-14）
- [X] T033 [P] [US3] 改 `tests/KFL.Tests/Rules/ExamAdmissionTests.cs`（在 T023 建立的同一文件内追加）免解分区 + 改 `tests/KFL.Tests/Rules/ExamSeasonTests.cs`（在 T024/T029 建立的同一文件内追加）跨科年免解分区：SC-004 —— 解试累计失败数恰为 **2** 时**仍掷骰**，恰为 **3** 时**随机消费次数 = 0**、直接中第、记录被追加；末条功名已达举人或更高时 MUST NOT 追加重复记录（仍算中第、仍清除标记）；取得资格后解试失败数**不再增长**（此后每跑一个科年差异项 = 0，含中间若干科年未报名与跨科年）；免解**不跨级**（该成员省试仍须在 3 月报名并付路费）；门槛判定用进入本步**之前**的失败数（免解当月不因免解而 +1）；连坐降级且失败数仍 ≥ 3 时再次解试**仍然**免掷骰（计数不因降级或调试改写清零）（FR-013、FR-025；I-8、SC-004；契约十三 §5）

**Checkpoint**: US1、US2、US3 均独立可用 —— 两条兜底机制与事件流就绪

---

## Phase 6: User Story 4 - 快进在考试月停住 (Priority: P4)

**Goal**: 交付三个**纯查询**（① 给定年月 → 该月考试级别；② 下一考试月（含跨年）；③ 该考试月
合格且未报名者名单），供界面轨 U1 的「快进在考试月暂停」与 U4 的「跳至下一科举」消费；
**MUST NOT** 实现快进循环本身，界面 MUST NOT 自行复制资格判定或日历推导

**Independent Test**: 对任意年月断言「该月考试级别」与「下一考试月」的三年周期正确性（含跨年
边界：科年 8 月 → 次年 3 月 → 同年 4 月 → 3 年后的 8 月）；断言「本考试月合格且未报名者名单」
查询与报名后的名单变化一致，且该查询是**纯函数**（不改变任何状态、不消耗随机）

- [X] T034 [US4] 改 `src/KFL.Rules/Exam/ExamRegistration.cs`（在 T014/T026/T030 建立的同一文件内追加）：`public static IReadOnlyList<Person> EligibleUnapplied(Family family, GameDate date)`（`person.AppliedExam is null ∧ TargetLevelOf(person) == ExamCalendar.LevelOf(date) ∧ IsEligible(person, date)`，按 `PersonId` **升序**；非解试/省试月（含殿试月）或无人合格 ⇒ **空表**；**纯查询**：调用前后 `GameState`/`Family`/`Person`/账本/随机状态**逐字段相同**、MUST NOT 消耗随机；MUST NOT 返回可变集合）（FR-018③；契约十二 §5）
- [X] T035 [P] [US4] 新建 `tests/KFL.Tests/Rules/ExamCalendarTests.cs`：SC-007 前半 —— 对连续 **3 个科年**的**每一个月**调用 `LevelOf` 与 `NextExamMonth`，与契约十二 §1 的手算期望逐月比对（`(1,8) 解试 → (1,9)~(2,2) 无 → (2,3) 省试 → (2,4) 殿试 → (2,5)~(4,7) 无 → (4,8) 解试 → (5,3) 省试 → (5,4) 殿试`，差异项 = 0）；`IsKeYear` 的 `year >= 1 && year % 3 == 1` 边界（含 `year <= 0`）；`NextExamMonth` 返回**严格晚于**入参的第一个考试月（传入考试月本人时返回下一个，而非自身）；三者**不消耗随机**（`FixedRandomService` 调用计数 = 0）、MUST NOT 依赖系统时钟或全局状态；断言科年与考试月**不出现在任何存档键表中**（派生值，MUST NOT 持久化）（FR-001、FR-018①②、FR-025；SC-007）
- [X] T036 [P] [US4] 改 `tests/KFL.Tests/Rules/ExamRegistrationTests.cs`（在 T028 建立的同一文件内追加）：SC-007 后半 —— 「合格未报名者名单」在报名**前**列出全部合格未报名成员、报名**后**把该成员**移出**；非解试/省试月（含殿试月）恒为空表；输出按 `PersonId` 升序；该查询是**纯函数** —— 调用前后状态逐字段相同、随机消费 **0** 次、账本 **0** 条（FR-018③、FR-025；SC-007）

**Checkpoint**: 全部四个 user story 独立可用 —— U1/U4 的查询面就绪

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: FR-024 的落盘形状与版本提升、FR-028 的七项活工件回写、门禁与回写核对

### 落盘形状与格式版本（FR-024、SC-009）

- [X] T037 改 `src/KFL.Infrastructure/Save/Serialization/SaveStateValueDtos.cs`：`FamilyDto` 追加键 **6** `autoExamApplication`（`bool`，成员数 6 → 7）；`PersonDto` 追加键 **22** `jieShiFailureCount`（`int`）、**23** `shengShiFailureCount`（`int`）、**24** `appliedExam`（`ExamLevel?`，成员数 22 → **25**）；每项同步 `[Key]` + `[JsonPropertyName]` + `[JsonRequired]`；既有键的序号与键名**一字不动**，新增键**一律追加在末尾**（FR-024；data-model §7.1；R-11）
- [X] T038 改 `src/KFL.Infrastructure/Save/Serialization/MessagePackSaveFormat.cs`：`PersonMemberCount` 22 → **25**、`FamilyMemberCount` 6 → **7**，并同步 `EnsurePersonShape` / `EnsureFamilyShape` 的 **Skip 计数**，使「二进制 walker 的成员数常量与 DTO 键数逐一相等」（FR-024；SC-009）— 依赖 T037
- [X] T039 改 `src/KFL.Infrastructure/Save/Serialization/SaveStateMapper.cs`：`ToDto(Person)` / `Build` / `CreatePerson` 的双向映射补齐三个新 `Person` 成员与 `Family.AutoExamApplication`；复原次序沿用 004 data-model §3.11，`appliedExam` 经 `Person.RestoreExamProgress` 复原（**唯一** internal 通道）（FR-024）— 依赖 T037、T006
- [X] T040 改 `src/KFL.Infrastructure/Save/Serialization/SaveStateValidator.cs`：新增第 **⑩** 条校验 —— `appliedExam` 非空 ⇒ `∈ { JieShi, ShengShi }`（**殿试不是合法报名态**，`Enum.IsDefined` 拒绝 `0` 与 `> 3`），且两个失败数均 `>= 0`；越界整数 ⇒ `InvalidStructure`、认不得的 JSON 枚举名 ⇒ `Malformed`（FR-024；契约十二 §2 条款 3；004 契约十 §6）— 依赖 T037
- [X] T041 改 `src/KFL.Infrastructure/Save/SaveFileFormat.cs`：`Version` 由 **1 提升为 2**；版本闸门口径改为 `FormatVersion == 2`（既有 `== 1` 口径），`< 2` 与 `> 2` 一律 `UnsupportedVersion` 且**早于主体解析**；**MUST NOT 提供任何迁移 / 兼容路径**（FR-024；data-model §7.2）
- [X] T042 改 `tests/KFL.Tests/Infrastructure/SaveFixtures.cs` + `tests/KFL.Tests/Infrastructure/SaveStateComparer.cs`：`RichState()` 把四个新成员设成**非默认值**（`jieShiFailureCount` / `shengShiFailureCount` 取非 0、`appliedExam` 取 `JieShi` 或 `ShengShi`、`autoExamApplication = true`），否则往返测试会**静默通过**；`AssertPerson` / `AssertFamily` 补齐四个新成员的逐字段比较（FR-024；SC-009；data-model §7.3）
- [X] T043 改 `tests/KFL.Tests/Infrastructure/SaveFormatContractTests.cs`：`BinaryKeyTables` 的 `PersonDto` 数组 **22 列 → 25 列**、`FamilyDto` **6 列 → 7 列**；同步 `mapping` 常量表与 `ContractKeyTable` 的路径条目；保持「二进制 walker 的成员数常量与 DTO 键数逐一相等」全绿（FR-024；SC-009）
- [X] T044 改 `tests/KFL.Tests/Infrastructure/SaveFailureTests.cs`：两条版本闸门用例改为**旧版 `1` 拒绝** / **更新版 `3` 拒绝**，并断言拒绝**早于主体解析**；补一条 `appliedExam = DianShi` 的非法态被 `SaveStateValidator` 拒绝的用例（FR-024；SC-009）

### 跨特性活工件回写（FR-028、AGENTS.md「铁律」）

> **只回写活工件**。`tasks.md`（001~004 的既有条目）、`implementation-notes.md`、既有
> `checklists/` 属**历史记录类工件，MUST NOT 回改**；001 的
> `contracts/{architecture-guard,injection-seams}.md` 本特性**不改**（不引入新工程、不引入新接缝）。

- [X] T045 [P] 回写 `specs/001-core-skeleton/data-model.md`：`Person` 字段表（`:120-143`）增三行（解试/省试累计失败数、报名标记）；`Family` 表（`:196-206`）增一行（持续报考开关）；不变量 2（`:160-165`，无公开 setter 的集合扩充）、不变量 4（`:170-172`）、不变量 7（`:175-177`）；`StatusTimers`（`:100-112`）与 `DegreeRecord`（`:66-92`）加**引用注**（一甲名次 ⟺ 一甲由本特性首次生产）；`HasShiStatus`（`:233`、`:249-251`）落实为**首个生产者**（FR-023、FR-028⑤）
- [X] T046 [P] 回写 `specs/002-monthly-settlement-economy/`：`contracts/monthly-settlement.md` §1 步骤表（`:13-21`）**七步 → 八步**、科举季为新 ⑦；`MUST NOT 存在` 清单（`:35-36`）**移除**「科举季触发（阶段 ⑤）」并加 005 后记；§4 随机消费次序（`:62-79`）补「科举判定（逐人：录取/进甲，进甲者紧接名次）→ 及第入仕待阙」；§9 快照面（`:205-212`）增 `ExamSeason` 子段。`spec.md` 的 Status 行（`:9`）、Out of Scope（`:379-402`，尤其 `:388-389`）、FR-020（`:306-310`）、Assumptions（`:432`）。`data-model.md` §4.2（`:297-311`）、§4.3（`:322`）、§6.3/§6.4（`:390-412`）（FR-019、FR-022、FR-028②；SC-011）
- [X] T047 [P] 回写 `specs/002-monthly-settlement-economy/contracts/config-registry.md`：§7（`:102-109`）把「科举录取率」从「002 阶段 MUST NOT 存在」清单**移出并加后记**（指向契约十四）；§6 加一行「科举数值只指向不复制」（FR-014、FR-028④）
- [X] T048 [P] 回写 `specs/003-opening-assets-officialdom/`：`contracts/official-career.md` §3 条款 6（`:61`）注明 `EntryTrack` 由 005 的「及第入仕」入口写入；§10（`:145-149`）把「科举周期与功名推进属逻辑轨 ⑤」**移出**；§8（`:134`）注明待阙时长随机属 005 的入口。`contracts/config-registry.md` §5（`:91-95`）把科举周期相关数值从「阶段 ⑤ 才有的数值」清单**移出并加后记**（FR-011、FR-014、FR-028）
- [X] T049 [P] 回写 `specs/004-save-system/`：`contracts/save-format.md` §4.2/§4.3 键表（`:73-92`）`PersonDto` 22 → **25**、`FamilyDto` 6 → **7**；版本闸门（`:109-129`、`:133-135`）`Version` 1 → 2 且拒绝口径改为 `!= 2`；§6 失败分类口径。`data-model.md` §3（`:93-217`）两张 DTO 表、§3.11 复原次序（`:240-255`）新增三个成员的读回位置、§8 不变量（`:396-417`）（FR-024、FR-028⑥）
- [X] T050 回写**断言物**（同提交，**不是**历史工件）：`tests/KFL.Tests/Core/LedgerInvariantTests.cs` 的类别计数断言 19 → 20（与 T019 同一处，若已随 T019 完成则在此处只做复核）；`tests/KFL.Tests/Architecture/ConfigLiteralTests.cs` 的 `RegisteredValues` +4 项小数（与 T011 同一处，若已随 T011 完成则只做复核）。本任务只做**逐项复核**并在 `tasks.md` 的「回写范围登记」表上打勾（FR-014、FR-024、SC-008；plan.md 回写清单第 13 行）

### 门禁与终验

- [X] T051 [P] 跑 `quickstart.md` 的 8 个场景人工核对，逐条确认「怎么跑 → 看什么 → 期望什么」与测试落点一致（含场景 1 的 `1.288%`、场景 2 的 `10/20/40/80`、场景 4 的 L18、场景 5 的 `(1,8)→(4,8)` 逐月命中、场景 7 的版本闸门）
- [X] T052 终验：跑门禁四条（`dotnet build` 0 警告 0 错误、`dotnet test` 全绿 0 skipped、`dotnet sln list` 恰好六个工程、无 `*.sln`），确认 `git status --porcelain` 跑完后**无残留**（SC-010）；核对 SC-011 —— `specs/002-*/contracts/monthly-settlement.md` 的 `MUST NOT 存在` 清单中**不再包含**「科举季触发」，且 T045~T050 的七项回写目标与下表**同一提交内**全部更新、`tasks.md` 已登记「同步了哪些文件、同步到哪里」；确认 `tests/KFL.Tests/KFL.Tests.csproj` 的 `_MSTestEnableParentProcessQuery` 仍为**注释态**；确认 `KFL.Presentation` / `KFL.App` **一行未改**（FR-029）

---

## 回写范围登记（FR-028 / AGENTS.md 铁律第 3 条）

> 本表登记「同步了哪些文件、同步到哪里」，供后续审计核对。**行号为回写前的原位**，逐条回写后
> 行号会移动，故每行同时给出**可检索的锚点文本**。

| # | 目标工件（活工件） | 回写内容（锚点） | 依据 | 任务 |
| --- | --- | --- | --- | --- |
| 1 | `科举浮生录规格书.md` §3 / §6 / §7.1（顺及 §7.2） | **已完成（2026-10-12）**：已追加「§17 裁决回写」块记入 Q1/Q2/Q3；原始条目文本按 §16.2 的历史事实口径**保留不改写** | spec `## Clarifications` | — |
| 2 | `specs/001-core-skeleton/data-model.md` | `Person` 字段表 `:120-143` 增三行；`Family` 表 `:196-206` 增一行；不变量 2 `:160-165`、4 `:170-172`、7 `:175-177`；`StatusTimers` `:100-112` 与 `DegreeRecord` `:66-92` 的引用注；`HasShiStatus` `:233` / `:249-251` | FR-023、FR-028⑤ | T045 |
| 3 | `specs/002-monthly-settlement-economy/contracts/monthly-settlement.md` | §1 步骤表 `:13-21`（七步 → 八步，科举季 = 新 ⑦）；`MUST NOT 存在` 清单 `:35-36` **移除**「科举季触发（阶段 ⑤）」并加 005 后记；§4 随机消费次序 `:62-79`；§9 快照面 `:205-212` 增 `ExamSeason` 子段 | FR-019、FR-022、FR-028② | T046 |
| 4 | `specs/002-monthly-settlement-economy/spec.md` | Status 行 `:9`、Out of Scope `:379-402`（尤其 `:388-389`）、FR-020 `:306-310`、Assumptions `:432` 的同步注 | FR-028② | T046 |
| 5 | `specs/002-monthly-settlement-economy/data-model.md` | §4.2 `SettlementResult` `:297-311`、§4.3 七步 `:322`、§6.3/§6.4 `:390-412` | FR-022、FR-028② | T046 |
| 6 | `specs/002-monthly-settlement-economy/contracts/config-registry.md` | §7 `:102-109` 移出「科举录取率」并加后记；§6 加一行「科举数值只指向不复制」 | FR-014、FR-028④ | T047 |
| 7 | `specs/003-opening-assets-officialdom/contracts/official-career.md` | §3 条款 6 `:61`、§10 `:145-149`、§8 `:134` | FR-011、FR-028 | T048 |
| 8 | `specs/003-opening-assets-officialdom/contracts/config-registry.md` | §5 `:91-95` 移出科举周期相关数值并加后记 | FR-014 | T048 |
| 9 | `specs/004-save-system/contracts/save-format.md` | §4.2/§4.3 键表 `:73-92`（`PersonDto` 22 → 25、`FamilyDto` 6 → 7）；版本闸门 `:109-129` / `:133-135`（`Version` 1 → 2，拒绝口径改为 `!= 2`）；§6 失败分类口径 | FR-024、FR-028⑥ | T049 |
| 10 | `specs/004-save-system/data-model.md` | §3 `:93-217`、§3.11 复原次序 `:240-255`、§8 不变量 `:396-417` | FR-024、FR-028⑥ | T049 |
| 11 | `src/KFL.Rules/Settlement/MonthlySettlementEngine.cs` 类文档 | 七步 → 八步；`MUST NOT 存在` 清单**移除**「科举季触发（阶段 ⑤）」；随机消费次序补入「科举判定 → 及第入仕待阙」 | FR-019、FR-028③ | T022 |
| 12 | `src/KFL.Core/Enums/LedgerCategory.cs` + `LedgerCategoryMetadata.cs` | 类别计数 **19 → 20**：枚举类文档与两处 `default:` 异常文案（「共 19 个取值」→「共 20 个取值」） | FR-021、FR-028⑦ | T008 |
| 13 | 断言物（同提交，**不是**历史工件） | `tests/KFL.Tests/Architecture/ConfigLiteralTests.cs` 的 `RegisteredValues` **+4 项小数**；`tests/KFL.Tests/Core/LedgerInvariantTests.cs` 的类别计数断言；`tests/KFL.Tests/Infrastructure/{SaveFixtures,SaveStateComparer,SaveFormatContractTests,SaveFailureTests}.cs` | FR-014、FR-024、SC-008、SC-009 | T011、T019、T042~T044、T050 |
| 14 | `src/KFL.Infrastructure/Save/SaveFileFormat.cs` + `Save/Serialization/{MessagePackSaveFormat,SaveStateMapper,SaveStateValidator,SaveStateValueDtos}.cs` | `Version` 1 → 2；`PersonMemberCount` 22 → 25 及 walker 的 Skip 计数；`FamilyMemberCount` 6 → 7；双向映射；第 ⑩ 条校验 | FR-024 | T037~T041 |
| 15 | `specs/005-exam-cycle/tasks.md` | 本表：登记上述回写范围，供后续审计核对 | AGENTS.md 铁律第 3 条 | T050、T052 |

### 回写复核结论（T050 / T052，2026-10-13）

上表 15 行**逐项复核完毕，全部落入同一工作树**；下方括号内为复核到的实际落点（行号为回写**后**的现位）：

1. ✅ `科举浮生录规格书.md` §17 裁决块已在（2026-10-12），本特性未再动。
2. ✅ `specs/001-core-skeleton/data-model.md` —— `Person` 三行 `:156-158`、`Family.AutoExamApplication` `:238`、不变量 2 `:175-192` / 4 `:197-200` / 7 `:206-208`、`DegreeRecord` `:79-83`、`StatusTimers` `:120-124`、`HasShiStatus` `:265-267` + `:287-291`。
3. ✅ `specs/002-monthly-settlement-economy/contracts/monthly-settlement.md` —— §1 步骤表新 ⑦ 科举季（原 ⑦ 顺移 ⑧）、`MUST NOT 存在` 清单**已移除**「科举季触发（阶段⑤）」、§4 随机消费次序、§9 `ExamSeason` 子段。
4. ✅ `specs/002-monthly-settlement-economy/spec.md` —— Status 八步、FR-020 `:311-316`、Out of Scope、Assumptions。
5. ✅ `specs/002-monthly-settlement-economy/data-model.md` —— §4.2 / §4.3 / §5 / §6.3 / §6.4。
6. ✅ `specs/002-monthly-settlement-economy/contracts/config-registry.md` —— §7 移出「科举录取率」并加 005 后记、§6 「科举数值只指向不复制」。
7. ✅ `specs/003-opening-assets-officialdom/contracts/official-career.md` —— §3 条款 6 与表后 005 后记 `:65-71`、§8 后记 `:138`、§10 `:150` 移出。
8. ✅ `specs/003-opening-assets-officialdom/contracts/config-registry.md` —— §5 `:93-100` 移出并加 005 后记。
9. ✅ `specs/004-save-system/contracts/save-format.md` —— §2 版本 2、§4.2 键表（22→25 / 6→7）、§6 闸门 `== 2` + 新增第 ⑩ 条、§7 常量表（`3` 拒绝 / `1` 拒绝）。
10. ✅ `specs/004-save-system/data-model.md` —— §2.2/§2.3、§3.2、§3.8、§3.11、§4、§6.2、§7（FR-002 线上成员数 76 → 80）、§8 不变量 15/16/17。
11. ✅ `src/KFL.Rules/Settlement/MonthlySettlementEngine.cs` 类文档 —— 八步、清单移除、随机消费次序补入「科举判定 → 及第入仕待阙」。
12. ✅ `src/KFL.Core/Enums/LedgerCategory.cs` + `LedgerCategoryMetadata.cs` —— 类别计数 19 → 20 与两处 `default:` 文案。
13. ✅ 断言物 —— `tests/KFL.Tests/Architecture/ConfigLiteralTests.cs` 的 `RegisteredValues` **+4 项小数**（`0.01m` / `0.05m` / `0.012m` / `0.899999m`，注释块 `:104-114`）；`tests/KFL.Tests/Core/LedgerInvariantTests.cs` 的 `ExamTravelCost`（`[InlineData]` 与穷举数组，`:78` / `:152`）；存档四文件（`SaveFixtures` / `SaveStateComparer` / `SaveFormatContractTests` / `SaveFailureTests`）。
14. ✅ 存档源码 —— `SaveFileFormat.Version = 2`、`MessagePackSaveFormat` 的 `PersonMemberCount` 25 / `FamilyMemberCount` 7 与两处 `Skip`、`SaveStateMapper` 双向映射、`SaveStateValidator.EnsureExamProgressInRange`（第 ⑩ 条）。
15. ✅ 本表。

**顺带收敛的活工件（越界残留，非上表条目）**：`specs/004-save-system/{research.md,plan.md,contracts/save-service.md,quickstart.md}` 与 `specs/002-monthly-settlement-economy/{research.md,plan.md,quickstart.md,data-model.md,contracts/ledger.md}` 中的旧形状字样（版本 `1`、九条、七步、`76 个线上成员`、quickstart 的「改成 2」）已同批同步为当前形状；`tasks.md` / `implementation-notes.md` / 既有 `checklists/` 一律**未回改**（历史记录类工件）。

**FR-029 核对**：`git status --porcelain -- src/KFL.Presentation src/KFL.App` **无输出** —— UI 两层一行未改。

> **不改的**：`tasks.md`（001~004 的既有条目）、`implementation-notes.md`、既有 `checklists/` 属
> **历史记录类工件**，MUST NOT 回改（规格书 §16.2；AGENTS.md）。
> **001 的 `contracts/{architecture-guard,injection-seams}.md`** 本特性**不改**：不引入新工程、
> 不引入新接缝接口，G-01~G-08 的口径与扫描范围均不变（R-15）。

**本特性与 002 契约三 §1 步骤表的兼容性**：`MUST NOT 存在` 清单原先列有「科举季触发（阶段 ⑤）」，
本特性把它**落地为固定第 ⑦ 步**并从该清单移除（T046 / T022，同一提交内完成）。

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 无依赖，可立即开始（全部为只读核对，不改文件）
- **Foundational (Phase 2)**: 依赖 Setup 完成 —— **BLOCKS 全部 user story**
- **User Stories (Phase 3~6)**: 全部依赖 Foundational 完成
  - US1（P1）→ US2（P2）→ US3（P3）→ US4（P4）按优先级顺序推进
  - 本特性内 US2/US3/US4 各有一段**追加进 US1 已建文件**的实现（`ExamSeasonRunner.cs`、
    `ExamRegistration.cs`），故**顺序执行**；但各故事的**测试文件**可并行编写与独立验证
- **Polish (Phase 7)**: 依赖全部期望的 user story 完成

### 任务级依赖（关键路径）

```text
T001 ─ T002 ─ T003 ─ T004            （Setup，只读）
    └─ T005 ExamLevel
        ├─ T006 Person ────────┬─ T017 PersonTests
        ├─ T013 ExamCalendar ──┤
        ├─ T011 Snapshot ─ T016 SettlementResult ─ T021 Runner ─ T022 Engine
        │                                              └ T027 Runner ①②
        │                                                 └ T031 Runner ③⑥
        └─ T009 ExamPolicy ──┬─ T010 GameConfig
                             ├─ T011 ConfigLiteralTests
                             ├─ T012 SpecAnchorTests
                             └─ T014 ExamRegistration ─ T026 ─ T030 ─ T034
T007 Family ─ T018 FamilyTests
T008 LedgerCategory ─ T019 LedgerInvariantTests
T020 ExamAdmission ─ T023 / T033
T037~T041 存档形状 ─ T042~T044 存档断言物
T045~T050 回写 ─ T052 终验
```

**同文件顺序约束（MUST NOT 并行）**：

- `src/KFL.Rules/Exam/ExamSeasonRunner.cs`：T021（US1）→ T027（US2）→ T031（US3）
- `src/KFL.Rules/Exam/ExamRegistration.cs`：T014（Foundational）→ T026（US2）→ T030（US3）→ T034（US4）
- `tests/KFL.Tests/Rules/ExamSeasonTests.cs`：T024 → T029 → T033
- `tests/KFL.Tests/Rules/ExamAdmissionTests.cs`：T023 → T033
- `tests/KFL.Tests/Rules/ExamRegistrationTests.cs`：T028 → T036

### Within Each User Story

- 实现任务先于对应测试任务的**断言绿**（测试可先写、先失败，再被实现转绿）
- 存储/形状层（`KFL.Core`、`KFL.Infrastructure`）先于规则判定与编排
- 判定与编排先于集成（`MonthlySettlementEngine` 的步骤插入）
- 故事完成后再进入下一优先级

### Parallel Opportunities

- Setup 阶段：T002 / T003 / T004 三个只读任务可并行
- Foundational 阶段：T005、T007、T008、T009、T013（不同文件、无互相依赖）可并行；T011 与
  T012 在 T009 之后可并行；T017 / T018 / T019 三个断言物同步任务可并行
- US1~US4 阶段：各故事的**测试文件**互不冲突，可并行编写（如 T023 ∥ T024 ∥ T025）
- Polish 阶段：T045 / T046 / T047 / T048 / T049 / T051 六个**不同工件**的回写与核对任务可并行

---

## Parallel Example: User Story 1

```text
# 并行启动 US1 的三个测试文件（不同文件、无互相依赖）：
Task: "T023 [P] [US1] 新建 tests/KFL.Tests/Rules/ExamAdmissionTests.cs（录取率/进甲率/甲第分区）"
Task: "T024 [P] [US1] 新建 tests/KFL.Tests/Rules/ExamSeasonTests.cs（完整科年/随机次序分区）"
Task: "T025 [P] [US1] 改 tests/KFL.Tests/Rules/DeterminismTests.cs（科举季两次运行差异项 = 0）"

# 实现任务的顺序（同文件与依赖约束）：
Task: "T020 [US1] 新建 src/KFL.Rules/Exam/ExamAdmission.cs"
Task: "T021 [US1] 新建 src/KFL.Rules/Exam/ExamSeasonRunner.cs（骨架 + 复核 + ④⑤）"
Task: "T022 [US1] 改 src/KFL.Rules/Settlement/MonthlySettlementEngine.cs（七步 → 八步）"
```

## Parallel Example: Foundational

```text
# 五个不同文件、无互相依赖的任务一起启动：
Task: "T005 [P] 新建 src/KFL.Core/Enums/ExamLevel.cs"
Task: "T007 [P] 改 src/KFL.Core/Entities/Family.cs（AutoExamApplication）"
Task: "T008 [P] 改 src/KFL.Core/Enums/LedgerCategory.cs + LedgerCategoryMetadata.cs（20）"
Task: "T009 [P] 新建 src/KFL.Rules/Config/ExamPolicy.cs（全部考试数值单点）"
Task: "T013 新建 src/KFL.Rules/Exam/ExamCalendar.cs（依赖 T005）"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. 完成 Phase 1：Setup（只读核对 + 门禁基线）
2. 完成 Phase 2：Foundational（**关键** —— 阻塞全部故事：领域状态、配置数值、日历、共享资格判定、快照形状）
3. 完成 Phase 3：User Story 1
4. **STOP and VALIDATE**：固定种子跑完一个完整科年，逐字段断言解试 → 省试 → 殿试、3 条 `ExamPass`、
   待阙 6~24 月、`HasShiStatus` 首次置位
5. 若需要可先行交付 —— 003 的「及第入仕」入口至此有了唯一调用者

> **注意**：MVP 不含 FR-024 的存档升版。真实提交 MUST 是在**同一提交**内一并完成 `PersonDto`
> 22 → 25、`FamilyDto` 6 → 7 与 `SaveFileFormat.Version` 1 → 2（FR-024：线上形状变化不允许
> 分次落地，否则旧档会静默错位读）。

### Incremental Delivery

1. Setup + Foundational → 地基就绪
2. 加 US1 → 独立验证 → 可演示（MVP！）
3. 加 US2 → 独立验证（报名闸门、路费四档、弃考、持续报考）
4. 加 US3 → 独立验证（免解 0 次随机、特奏名询问/拒绝/接受、L18、退出科举）
5. 加 US4 → 独立验证（三个纯查询 + 跨年边界逐月命中）
6. Polish → 存档形状与版本、七项活工件回写、门禁四条与回写核对

### Story 独立性说明（诚实登记）

- US1 可用 `Person.ApplyForExam` 直接设置报名标记，**不依赖 US2** 的 `Register`。
- US2/US3/US4 共用 US1 建立的 `ExamSeasonRunner.cs` 编排骨架与 `ExamRegistration.cs` 资格判定 ——
  这是 FR-018 与章程原则 VI 明令的「**同一份资格判定**」，MUST NOT 为独立性而复制实现；
  代价是这三个故事对 `ExamSeasonRunner.cs` / `ExamRegistration.cs` 的编辑 MUST 顺序进行。
- US4 的查询面复用 US2 的 `IsEligible` / `TargetLevelOf`，故 US4 的集成验证应在 US2 之后。

---

## Notes

- [P] 任务 = 不同文件、且不依赖同一阶段内其它未完成任务的产出
- [Story] 标签把任务映射到具体 user story，便于追溯
- 每个 user story 都应能独立完成与独立验证
- 测试**先写先失败**再被实现转绿（章程原则 IV）
- 每完成一个任务或一组逻辑任务就提交一次；提交信息用中文、遵循约定式提交
  （`feat(core)` / `feat(rules)` / `feat(infrastructure)` / `test(...)` / `docs(...)` 不混装）
- 随机 **MUST** 一律经可设种子的 `IRandomService`（`RulesTestHarness.FixedRandomService`）；
  `KFL.Core` / `KFL.Rules` 零文件系统访问、零 `DateTime.Now`、零 `System.Random`（G-07）
- 避免：含糊任务、同文件冲突、破坏独立性的跨故事依赖

### 需求覆盖对照（FR-001~FR-030）

| 需求 | 任务 |
| --- | --- |
| FR-001 考试月纯派生 | T013、T035 |
| FR-002 报考资格 | T014、T028 |
| FR-003 报考级别 | T014、T028 |
| FR-004 报名窗口/幂等 | T026、T028 |
| FR-005 报名标记 | T006、T021、T027、T029 |
| FR-006 判定前复核 | T021 |
| FR-007 解试录取率 | T020、T023 |
| FR-008 省试录取率 + 路费 | T020、T027、T029 |
| FR-009 殿试两步 | T020、T021、T023 |
| FR-010 功名记录追加 | T021、T023 |
| FR-011 及第入仕 | T021、T024 |
| FR-012 特奏名 | T030、T031、T032、T033 |
| FR-013 免解 | T031、T033 |
| FR-014 数值集中 + 登记 | T009、T010、T011、T012 |
| FR-015 封顶口径 | T020、T023 |
| FR-016 贿赂乘数接缝 | T009、T020、T023 |
| FR-017 持续报考 | T007、T027、T029 |
| FR-018 查询面 | T013、T034、T035、T036 |
| FR-019 步骤位置与次序 | T022、T024、T027、T031 |
| FR-020 `HasShiStatus` | T021、T024 |
| FR-021 路费账本类别 | T008、T019、T027、T029 |
| FR-022 增量快照 | T015、T016、T021、T024 |
| FR-023 新增持久状态 | T005、T006、T007、T017、T018 |
| FR-024 落盘与版本 | T037~T044 |
| FR-025 测试覆盖 | T023、T024、T025、T028、T029、T032、T033、T035、T036 |
| FR-026 可复现 | T024、T025 |
| FR-027 事件流与计数 | T015、T021、T031 |
| FR-028 回写义务 | T022、T045~T050 |
| FR-029 范围限定 | T052（`KFL.Presentation` / `KFL.App` 一行未改） |
| FR-030 不引入未登记名称 | T008、T020、T021、T030 |

### 成功判据覆盖（SC-001~SC-011）

| 判据 | 任务 |
| --- | --- |
| SC-001 同种子两跑差异项 = 0 | T024、T025 |
| SC-002 逐乘区封印 | T023、T033 |
| SC-003 功名链 3 条 `ExamPass` | T023 |
| SC-004 免解随机消费 0 次 | T033 |
| SC-005 特奏名恰好 1 次询问、L18 | T032 |
| SC-006 路费 10/20/40/80、不退、不足 0 条 | T029 |
| SC-007 查询面纯函数与跨年边界 | T035、T036 |
| SC-008 配置字面量违规数 = 0 | T011、T012、T050 |
| SC-009 双格式往返 + 版本闸门 | T037~T044 |
| SC-010 门禁四条 + 变更数 = 0 | T001、T052 |
| SC-011 七项回写同提交 + 清单核对 | T045~T050、T052 |

---

## Phase 8: Convergence

> 由 `$speckit-converge`（2026-10-13）追加：**只追加本节，未改动任何既有条目、任何 `spec.md` / `plan.md` 与任何应用代码**。分级依据见本次会话内的收敛findings表（6 条：HIGH×3 / MEDIUM×1 / LOW×2，无 CRITICAL）。

- [X] T053 回写 `specs/002-monthly-settlement-economy/data-model.md` §2.1（`LedgerCategory` 全集的**文档形式真源**，与 `src/KFL.Core/Enums/LedgerCategoryMetadata.cs` 的可执行形式同源，见 `specs/002-monthly-settlement-economy/contracts/ledger.md:18`）：把 `ExamTravelCost`（省试路费，**资金类**，`正额入账目标` = 「—（恒为负）」，来源 规格书 §6/§12.3）作为**第 20 行**追加到类别表末尾；同步表下计数与名单——「**9** 个类别（**4** 个恒为负的支出类 + 5 个事件类）」→「**10** 个类别（**5** 个恒为负的支出类 + 5 个事件类）」，且「**只有 4 个恒为负的支出类**（`LivingCost`、`LoanPrincipalRepaid`、`LoanInterestRepaid`、`AssetPurchase`）」补入 `ExamTravelCost`，使该表的行数与名单同 `LedgerCategoryMetadata`（20 个取值）逐行一致 per FR-021/FR-028② (partial)

- [X] T054 在 `tests/KFL.Tests/Core/PersonTests.cs` 补齐既有任务 T017 承诺、当前缺失的行为断言（该文件现仅有「无公开 setter」反射集合）：`ApplyForExam` 同级别重复报名幂等（不抛、逐字段不变）、`ApplyForExam(ExamLevel.DianShi)` 抛 `InvalidOperationException`、`RecordExamFailure(ExamLevel.DianShi)` 抛 `InvalidOperationException`、`ClearExamApplication()` 对 `AppliedExam == null` 为 no-op、`RecordExamFailure` 只 +1 对应计数且不触碰另一计数，逐条覆盖 `specs/005-exam-cycle/data-model.md` §6 的不变量 I-1/I-2/I-3（`RecordExamFailure(DianShi)` 的拒绝与 `ClearExamApplication()` 的 null no-op 目前**在任何测试文件中都无断言**）per FR-023/FR-025 (partial)

- [X] T055 在测试中落实计划阶段勘误（`plan.md:309`「spec 的 1 处算式勘误在测试锚点中的落实」，T012/T023 承诺）：于 `tests/KFL.Tests/Rules/SpecAnchorTests.cs` 的「科举数值锚点」节与/或 `tests/KFL.Tests/Rules/ExamAdmissionTests.cs` 写入 spec US1 AS1 勘误后的示例值 `0.01 × (0.4 + 0.012 × 60) × (1 + 60/400) × 1.0 × 1.0 = 0.01288`（即 `1.288%`，`hasShiStatus = false`；**不是** `1.42%`），断言值 MUST 经 `ExamPolicy` 的数值成员取得（该测试现经其转发视图 `GameConfig.Exam.*`，二者同源；若保留转发视图，须在锚点注释中注明唯一数值来源仍是 `ExamPolicy`），MUST NOT 在测试中书写规则字面量 per FR-014/FR-025 (partial)

- [X] T056 补 `tests/KFL.Tests/Core/LedgerInvariantTests.cs` 的 `无正额入账目标的资金类类别以正额入账被拒`（`:259-281`）：把 `LedgerCategory.ExamTravelCost` 加入 `alwaysNegative` 数组（4 → 5 项），使「`KindOf == Treasury` ∧ `CreditTargetOf == null`」的类别全集无遗漏、正额入账对该类别同样抛 `InvalidOperationException`（现在该类别未被覆盖）per FR-021、`specs/005-exam-cycle/data-model.md` 不变量 I-5 (partial)

- [X] T057 更正 005 活工件中的落盘 DTO 文件指针：`specs/005-exam-cycle/plan.md:98`、`specs/005-exam-cycle/plan.md:168`（回写范围表第 14 行）、`specs/005-exam-cycle/plan.md:222`（文件树）与 `specs/005-exam-cycle/data-model.md:396` 把 `FamilyDto`/`PersonDto` 的所在文件由 `SaveStateValueDtos.cs` 改为 `SaveStateDto.cs`（实际位置：`src/KFL.Infrastructure/Save/Serialization/SaveStateDto.cs:84` 的 `FamilyDto` 与 `SaveStateDto.cs:138` 的 `PersonDto`；`SaveStateValueDtos.cs` 只承载 `TalentDto`/`StatusTimersDto`/`DegreeRecordDto`/`TreasuryDto`/`LoanDto`/`HoldingsDto`/`LedgerEntryDto`/`FamineDto`/`FamilyEconomyDto` 等值类型 DTO），并在本次收敛任务中登记该更正（既有 `tasks.md` 的历史条目 T037 与回写范围登记第 14 行**一律不回改**）per FR-028、plan.md「同提交必须同步的实现与断言物」(contradicts)

- [X] T058 消除三处残留的旧形状字样「七步」（月度结算在 005 之后为**八步**）：`specs/004-save-system/contracts/save-cadence.md:8`、`specs/004-save-system/research.md:405`、`specs/001-core-skeleton/spec.md:207`；其中 `specs/004-save-system/research.md:405` 被本 `tasks.md` 的「顺带收敛的活工件」段落声明已同步，该声明与事实不符——差异由本条收敛任务承接登记，**既有段落（历史记录）不得回改** per FR-028、AGENTS.md「活工件 MUST 与当前实现一致」 (partial)

### Phase 8 回写登记（T053~T058 完成时同步的范围）

| # | 同步的文件 | 同步到哪里 | 依据 |
| --- | --- | --- | --- |
| T053 | `specs/002-monthly-settlement-economy/data-model.md` | §2.1 类别表末行（追加 `ExamTravelCost`，**第 20 行**）与表下计数段（9→10 个类别、4→5 个恒为负的支出类） | FR-021、FR-028② |
| T054 | `tests/KFL.Tests/Core/PersonTests.cs` | 新增分区「科举进度（T054，I-1/I-2/I-3）」4 个 `[Fact]` | FR-005、FR-023、FR-025 |
| T055 | `tests/KFL.Tests/Rules/ExamAdmissionTests.cs` | 新增 `科举示例录取率等于勘误后的百分之一二八八` | `plan.md:309`、FR-014、SC-002 |
| T056 | `tests/KFL.Tests/Core/LedgerInvariantTests.cs` | `无正额入账目标的资金类类别以正额入账被拒` 的 `alwaysNegative` 数组（4 → 5 项） | FR-021、data-model §6 不变量 I-5 |
| T057 | `specs/005-exam-cycle/plan.md`（`:98` 实现面、`:168` 回写范围表第 14 行、`:222` 文件树）与 `specs/005-exam-cycle/data-model.md`（`:396` §7.3） | `SaveStateValueDtos.cs` → `SaveStateDto.cs` | FR-028、plan「同提交必须同步的实现与断言物」 |
| T058 | `specs/004-save-system/contracts/save-cadence.md:8`、`specs/004-save-system/research.md:405`、`specs/001-core-skeleton/spec.md:207` | 七步 → 八步（001 处并注明「第③步为 003 插入、第⑦步为 005 插入」） | FR-028、AGENTS.md「活工件 MUST 与当前实现一致」 |

**两条偏差登记**（供后续审计核对）：

1. T053 的类别中文名取 `src/KFL.Core/Enums/LedgerCategory.cs:78` 枚举自身的注释「**省试路费**」
   （converge 文本写作「赶考路费」）：§2.1 表的「中文名」列 MUST 与可执行形式同源，故以枚举为准。
2. T058 **额外**收敛了第 4 处旧形状字样 `src/KFL.Rules/Save/TurnRunner.cs:14` 的类文档——原文
   「契约三钉死的「月度结算七步」序列一个字都没动——存档不是第八步」，在 005 追加第 ⑦ 步后八步
   已被占满，该措辞双重失真；改为「004 对契约三钉死的结算步骤序列一个字都没动（005 后该序列为
   八步）——存档不是序列里的任何一步，而是套在结算外面的一层编排（R-17）」。该文件不在 T058 原列
   的三处之内，按 AGENTS.md 铁律第 3 条在此承接登记。

## 回写范围登记（2026-10-19，玩家反馈批次：基础率、报名窗口、举人授官）

> 本节由玩家反馈修复（反馈 4/5/8/9/21/22）**追加登记**，不回改上文既有条目，不复用既有编号。
> 授权落点 = 规格书 §5 / §6 的 2026-10-19 `§17 裁决回写` 块与 `spec.md` 末尾同名增补。

| 变更 | 同步了哪些文件 |
| --- | --- |
| 基础录取率 0.01/0.10/0.05 → 0.25/0.30/0.15 | `科举浮生录规格书.md` §6；`specs/005-exam-cycle/spec.md` 增补 §1；`src/KFL.Rules/Config/ExamPolicy.cs`；`tests/KFL.Tests/Rules/{SpecAnchorTests,ExamAdmissionTests}.cs` |
| 报名窗口 = 考试当月 ∪ 前一月（`RegisterLevelOf`） | `科举浮生录规格书.md` §5；`specs/005-exam-cycle/spec.md` 增补 §2；`src/KFL.Rules/Exam/ExamRegistration.cs`、`src/KFL.Presentation/GameSession.cs`（`HasPendingDecision` / `AutoRegisterForUpcomingExam`）、`src/KFL.Presentation/ViewModels/MainViewModel.cs`（`RefreshAll` / `RefreshDetail`）；`tests/KFL.Tests/Rules/ExamRegistrationTests.cs` |
| 举人即授官引发 `IsEligible` 去掉 `Rank is null` | `specs/003-opening-assets-officialdom/spec.md` 增补 §4（交叉引用本节增补 §3）；`src/KFL.Rules/Exam/ExamRegistration.cs`、`src/KFL.Rules/Exam/ExamSeasonRunner.cs`；`tests/KFL.Tests/Rules/ExamSeasonTests.cs` |

**门禁实测（2026-10-19）**：`dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false` → 0 警告 0 错误；
`dotnet test … -p:_MSTestEnableParentProcessQuery=false` → **1286 通过 / 0 失败 / 0 skipped**；
`dotnet sln KejuFuShengLu.slnx list` → 恰好六个工程；`*.sln` 全树扫描 → 无输出。
