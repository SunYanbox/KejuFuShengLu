---

description: "Task list for 006-bribery-punishment（贿赂全链路：行贿、12 格惩罚矩阵、连坐与服刑）"
---

# Tasks: 贿赂全链路（行贿、被抓惩罚与服刑）

**Feature**: `006-bribery-punishment`（规格书 §16.1 **逻辑轨 ⑥**「贿赂全链路（12 格矩阵/连坐/服刑/死刑）」）

**Input**: Design documents from `/specs/006-bribery-punishment/`

**Prerequisites**: [plan.md](./plan.md)（技术栈、结构、回写义务清单）、[spec.md](./spec.md)（FR-001~FR-035、US1~US4、SC-001~SC-011）、
[data-model.md](./data-model.md)（层分布、字段与签名、I-1~I-19 不变量、键表与版本）、
[contracts/bribery.md](./contracts/bribery.md)（契约十五）、[contracts/punishment.md](./contracts/punishment.md)（契约十六）、
[contracts/config-registry.md](./contracts/config-registry.md)（契约十七）、[quickstart.md](./quickstart.md)（8 个手工验收场景）、
[research.md](./research.md)（R-01~R-17，未决事项 = 0）

**Tests**: 本特性**含测试任务**——spec FR-034 明令「MUST 有覆盖 US1~US4 与规格书 §16「终验收」所需路径的规则层单测」，
章程原则 IV 要求「测试与实现同一提交给出」。测试文件先写（对契约签名写断言），实现后转绿。

**Organization**: 任务按 user story 分组，使每个故事可独立实现、独立验收、独立交付。`[Story]` 只出现在用户故事阶段。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 可并行（不同文件、不依赖未完成任务）
- **[Story]**: 该任务所属用户故事（US1 = 行贿 / US2 = 12 格惩罚矩阵 / US3 = 服刑推进与释放 / US4 = 地狱殿试连坐）
- 每条任务都带**精确文件路径**

## Path Conventions

单仓库六工程（`KejuFuShengLu.slnx`），分层：`src/KFL.Core`（状态与不变量）← `src/KFL.Infrastructure`（落盘形状）←
`src/KFL.Rules`（规则与编排）← `src/KFL.Presentation` / `src/KFL.App`（界面，**本特性 0 行**）；测试在 `tests/KFL.Tests/`。
依赖方向与工程数 MUST NOT 变化（章程 II、G-02、G-05）。

**受限宿主命令口径**（`AGENTS.md` 坑 1 / 坑 2）：一切 `dotnet build` / `dotnet test` MUST 带
`-m:1 -nodeReuse:false`，测试另加 `-p:_MSTestEnableParentProcessQuery=false`；该属性在
`tests/KFL.Tests/KFL.Tests.csproj:21-25` MUST 以**注释态**提交。

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 建立基线与目录骨架，确认零新增依赖、六工程不变

- [X] T001 记录门禁基线：在仓库根跑 `dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false`、`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false`、`dotnet sln KejuFuShengLu.slnx list`（期望恰好六个工程）、`Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }`（期望无输出），并记录起始测试总数作为回归基线
- [X] T002 [P] 建立本特性的两个新目录并确认工程形状零改动：`src/KFL.Rules/Bribery/`、`src/KFL.Rules/Punishment/`（`KejuFuShengLu.slnx`、`global.json`、`Directory.Build.props`、各 `*.csproj` 均 MUST NOT 改动；零新增 NuGet 依赖；`KFL.Presentation` / `KFL.App` 一行不动）

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: US1~US4 共同依赖的状态载体、账本类别、数值单点、总资产访问器、快照类型与支付原语形参

**⚠️ CRITICAL**: 本阶段完成前，任何用户故事都无法开始

### 状态载体（`KFL.Core`）

- [X] T003 [P] `src/KFL.Core/Entities/Person.cs`：新增三个只读计数 `JieShiBribeCount` / `ShengShiBribeCount` / `DianShiBribeCount`（`private set`，**`>= 0`**，语义 = 同一考生 × 同一考试级别、**跨科年累计**），并给出唯一写入通道 `public void RecordBribe(ExamLevel level)`（对应计数 +1，三级别皆合法，**无上限、无清零**）、`public void ResetBribeCounts(ExamLevel? level)`（`null` ⇒ 三级全清；MUST NOT 降到 0 以下、MUST NOT 触碰其他字段）、`internal void RestoreBribeCounts(int jieShiBribeCount, int shengShiBribeCount, int dianShiBribeCount)`；连坐降级（`PunishmentDemotion`）MUST NOT 清零三者（`Person.cs:181-182` 既有约束）
- [X] T004 [P] `src/KFL.Core/ValueObjects/PunishmentFacts.cs`（新）：六个 `readonly record struct`，字段逐一照 `data-model.md` §2.2 —— `BribeFact(GameDate Date, PersonId Examinee, ExamLevel Level, int Nth, Money Amount, bool Caught, bool CapReached)`、`SentenceFact(GameDate Date, PersonId Subject, int Months)`、`FineFact(GameDate Date, PersonId Subject, Money Amount, PaymentResult? Paid)`、`RelativeBanFact(GameDate Date, PersonId Subject, PersonId Relative, int ExamBanMonths, int? PromotionBanMonths)`、`CollectivePunishmentFact(GameDate Date, PersonId Subject, CollectiveOutcome Outcome, PersonId Relative, DegreeLevel? DegreeBefore, DegreeLevel? DegreeAfter)`、`ReleaseFact(GameDate Date, PersonId Subject, int? NewRankLevel)`
- [X] T005 [P] `src/KFL.Core/Enums/CollectiveOutcome.cs`（新）：枚举 `Executed`（处死）/ `DegreeDemoted`（降功名）/ `FamilyWideBan`（受全族禁考禁升）
- [X] T006 [P] `src/KFL.Core/Entities/PunishmentJournal.cs`（新）：**当月事实缓冲**（`IsEmpty`、`FamilyBanDeclared`、六集合 `Bribes`/`Sentences`/`Fines`/`RelativeBans`/`CollectivePunishments`/`Releases`、`Append` ×6 重载、`MarkFamilyBanDeclared()`、`internal Clear()`（**仅结算第③步调用**）、`internal Restore(…)`（**仅 `SaveStateMapper` 调用**））；口径：MUST **只追加**（MUST NOT 改写或删除既有行）、集合**按 `PersonId` 升序**（并列时按 `Level`/`Relative` 再排序）、成员集**含归档成员**的 `PersonId`、MUST NOT 成为与 `Person` 并存的第二真源
- [X] T007 `src/KFL.Core/Entities/Family.cs`：新增 `public PunishmentJournal PunishmentJournal { get; }`（只读引用 + `internal` 复原通道）——依赖 T006
- [X] T008 [P] `src/KFL.Core/Enums/LedgerCategory.cs`：追加第 **21/22** 个取值 `BribeExpense`、`Fine`（声明序在 `ExamTravelCost` 之后）；类文档「20 个取值」→ **22**，并改写「『贿赂』等阶段⑤⑥⑧ 的类别 MUST NOT 存在」条款
- [X] T009 [P] `src/KFL.Core/Enums/LedgerCategoryMetadata.cs`：两个 `switch` 各 +2 分支（`BribeExpense`/`Fine` 均 `KindOf = LedgerEntryKind.Treasury`、`CreditTargetOf = null`）；两处 `default:` 异常文案与 `<exception>` 文档「20」→「22」（`:23`/`:50-51`/`:59`/`:90-91`）；「其余 15 个」→ **17**、「恒为负的支出类（5 类）」→ **7**

### 数值单点与访问器（`KFL.Rules`）

- [X] T010 [P] `src/KFL.Rules/Config/BribePolicy.cs`（新）：`WindowPreviousMonths = 1`、`ProgressionStep = 0.2m`、`BaseAmountGuan(ExamLevel)`（解试 `100` / 省试 `500` / 殿试 `2000`）、`BaseCaughtRate(ExamLevel)`（`0.05m` / `0.15m` / `0.35m`）、`EffectMultiplierK(ExamLevel)`（`1.6m` / `1.3m` / `1.1m`）、`CaughtProbabilityCap = 1.0m`、`UnitMultiplier = 1m`（`k⁰` 的**唯一**来源）；数值逐字取自规格书 §7.1/§7.2，MUST NOT 内联到规则代码
- [X] T011 [P] `src/KFL.Rules/Config/PunishmentPolicy.cs`（新）：`PunishmentCell`（`readonly record struct`：`SentenceMonths`、`Execution`、`CollectivePunishment`、`Fine`（`None`/`Ratio`/`Fixed`）、`FineRatio`、`FineFixedGuan`、`RelativeBanMonths`、`RelativePromotionBan`、`FamilyWideBan`）+ `CellOf(Difficulty, ExamLevel)` —— **12 格逐格列明**（简单 60/36/12 月、比例 `0.02m`/`0.01m`/无罚金、禁考 `0`；普通 180/60/36 月、`0.15m`/固定 `200`/固定 `100`、禁考 `36`/`24`/`0`；困难 死刑/240/120 月、`0.20m`/`0.10m`/`0.05m`、禁考 `48`/`36`/`36`、禁升仅殿试 `true`；地狱 死刑+连坐/240/120 月、`0.20m`/`0.10m`/`0.05m`、全族 `48`/禁考 `36`/`36`、禁升仅殿试 `true`），另加 `RatioFineCapGuan = 50000`、`FamilyWideBanMonths = 48`、`ReleaseDemotionLevels = 1`；约束：死刑格 `SentenceMonths == 0 ∧ Execution != None`、简单三格 `RelativeBanMonths == 0 ∧ RelativePromotionBan == false`、困难/地狱解试与省试 `RelativePromotionBan == false`；`ExpenseFactor`/`SalaryTable.LowestLevel`/`IncomeRateTable.MonthsPerYear`/`AgeBracketPolicy` 一律**复用**，MUST NOT 复制
- [X] T012 `src/KFL.Rules/Config/GameConfig.cs`：新增两个**转发视图** `GameConfig.Bribe` / `GameConfig.Punishment`（形如既有 `GameConfig.Exam`）与 `GameConfig.Assets.TotalAssets`；转发视图 MUST NOT 自带任何数值字面量（SC-002/SC-008）——依赖 T010、T011、T013
- [X] T013 [P] `src/KFL.Rules/Economy/TotalAssetsCalculator.cs`（新）：`public static Money Of(FamilyEconomy economy)` 与 `public static Money Of(Treasury treasury, Holdings holdings)` = **现金 + 储蓄 + `AssetPriceTable.MarketValue(holdings)` − `Loan.Principal` − `Loan.AccruedInterest`**；为负或 0 ⇒ 返回 `Money.Zero`；**不含商本池**（FR-011 的单一访问器）
- [X] T014 [P] `src/KFL.Rules/Economy/PaymentPrimitive.cs`：`Pay` 追加**可选形参** `PersonId? personId = null`（默认 `null` ⇒ 既有行为**逐位不变**）；三段次序（现金 → 储蓄 → 余额转贷款）、不做部分支付、`amount <= 0` 的拒绝语义**一字不动**

### 快照与结算子段

- [X] T015 [P] `src/KFL.Rules/Punishment/PunishmentSnapshot.cs`（新）：`sealed record PunishmentSnapshot`，全部 `required … { get; init; }` —— `Month`、六类事实行集合、`FamilyBanDeclared`、`PrisonerCount`，另加 `static PunishmentSnapshot None(GameDate month)`；口径：增量、只读、集合按 `PersonId` 升序（并列按 `Level`/`Relative`）、MUST NOT 回写 `GameState`
- [X] T016 `src/KFL.Rules/Settlement/SettlementResult.cs` + `src/KFL.Rules/Settlement/MonthlySettlementEngine.cs`：新增 `public required PunishmentSnapshot Punishment { get; init; }`；引擎暂以 `PunishmentSnapshot.None(state.CurrentDate)` 供给，**保持构建全绿**（真实内容由 T037 替换）；既有两条不变量（`Entries` 口径、`TreasuryPoolAfter − Before` = 资金类条目之和）不变——依赖 T015

### 基础断言物同步

- [X] T017 [P] `tests/KFL.Tests/Core/PersonTests.cs:16-35`：只读成员全集 **16 → 19**（加入三个贿赂计数）；`tests/KFL.Tests/Core/FamilyTests.cs`：断言 `Family.PunishmentJournal` 存在且默认 `IsEmpty`、六集合为空
- [X] T018 [P] `tests/KFL.Tests/Architecture/ConfigLiteralTests.cs:54-117`：`RegisteredValues` **46 → 49**，只新增 `1.6m`（解试 k）、`0.15m`（省试被抓率 15% 与 15% 罚金比例同值，**只登记一次**）、`0.35m`（殿试被抓率 35%）；同值的 `1.3m`/`1.1m`/`0.2m`/`0.05m`/`0.02m`/`0.01m`/`0.10m`/`0.20m`/`1.5m`/`0.899999m` MUST NOT 重复登记，整数规则值（`100`/`500`/`2000`/`50000`/`36`/`24`/`48` 等）与 `1.0m`/`3.0m` **一律不入单**
- [X] T019 [P] `tests/KFL.Tests/Rules/SpecAnchorTests.cs`：新增「贿赂与惩罚数值锚点」节，逐项对照 [契约十七 §1/§2](./contracts/config-registry.md)（窗口/步长/基础金额/被抓率/k 值/clamp/12 格矩阵/50000 封顶/48 月/降 1 级），断言值一律**经配置成员取得**（MUST NOT 写裸字面量）

**Checkpoint**: 基础设施就绪——`dotnet build` 零警告零错误、既有测试全绿，用户故事可以开始

---

## Phase 3: User Story 1 - 行贿：累进金额、被抓判定与录取率乘数 (Priority: P1) 🎯 MVP

**Goal**: 交付**结算之外**的行贿行动：窗口判定 → 累进金额与被抓概率 → 当刻扣款 → 恰 1 次掷骰 → 计数与 `kⁿ` 乘数 → 当月事实 → 触顶拒绝；并删除 005 的 `NeutralBribeMultiplier` 占位。

**Independent Test**: 固定种子与年月，构造含合格考生的家族（解试报名月），连续行贿 `n = 1/2/3`：断言金额 = `100 × (1 + 0.2×(n−1)) × 支出系数`、概率 = `5% × (1 + 0.2×(n−1)) × 风险系数`（clamp 1.0）、扣款与账本条目各 1 条、`NextDouble()` 恰 1 次、乘数 = `1.6ⁿ`（**n 含被抓的那些**）、`ExamAdmission.AdmissionRate(…)` 收到的形参等于该值；窗口外/已触顶调用 ⇒ 状态逐字段不变且随机消费 = 0。**无需 US2~US4 的任何能力**（被抓分支的施加见 T023/T029）。

### 测试（先写，红色）

- [X] T020 [P] [US1] `tests/KFL.Tests/Rules/BribeEntryTests.cs`（新）：SC-002 逐步断言（步长 `0.2`、基础金额、被抓率、k 值、clamp）、窗口边界（6 月拒绝且资金/计数/乘数/账本/随机全不变；7 月与 8 月受理；殿试窗口覆盖省试月本身）、同月多次行贿各自累进且各消费 1 次随机、被抓**不清零**（第 2 次被抓后计数 = 2、乘数 = `k²`、无退款条目）、触顶后直接调用入口被拒且随机 0、`ResetBribeCounts` 回落与「重置不是越限」、`ExamPolicy.NeutralBribeMultiplier` **不存在**（`k⁰ = 1.0` 只由 `BribePolicy.UnitMultiplier`/`MultiplierOf` 给出）

### 实现

- [X] T021 [US1] `src/KFL.Rules/Bribery/BribeRules.cs`（新，**全纯函数**）：`IsWithinWindow(GameDate, ExamLevel)`、`WindowStartOf(GameDate, ExamLevel)`、`NthOf(Person, ExamLevel)`（= 现计数 + 1）、`PayableAmount(ExamLevel, int nth, Difficulty)` = `BaseAmountGuan × (1 + 0.2×(n−1)) × DifficultyRates.ExpenseFactor`、`CaughtProbability(ExamLevel, int nth, Difficulty)` = `min(BaseCaughtRate × (1 + 0.2×(n−1)) × DifficultyRates.BriberyRiskFactor, CaughtProbabilityCap)`、`MultiplierOf(ExamLevel, Person)` = `kⁿ`（n 含被抓的那些，k⁰ = `UnitMultiplier`）、`AdmissionRateWith(GameState, Person, ExamLevel)`（**只转调** 005 的 `ExamAdmission.AdmissionRate` / `ImperialPromotionRate`，MUST NOT 复制公式）、`IsLevelCapReached(GameState, Person, ExamLevel)`（最终录取率/进甲率命中 `ExamPolicy.DiceRateCap = 0.899999m`）、`CanBribe(GameState, Person, ExamLevel, out BribeFeasibility)`；窗口是**纯派生值**，MUST NOT 持久化；级别与功名匹配**沿用** `ExamRegistration.TargetLevelOf`，MUST NOT 另写一份
- [X] T022 [P] [US1] `src/KFL.Rules/Bribery/BribeOutcome.cs`（新）：`BribeOutcome`（`Accepted`、`RejectReason`、`Amount`、`FromCash`/`FromSavings`/`ToLoan`、`Caught`、`Nth`、`MultiplierAfter`、`CapReached`、`PunishmentSnapshot? Punishment`）与 `BribeFeasibility`（`Amount`、`Probability`、`IsCapReached`、`RejectReason` 枚举）
- [X] T023 [US1] `src/KFL.Rules/Bribery/BribeEntry.cs`（新）：`public static BribeOutcome Bribe(GameState state, Person examinee, ExamLevel level, IRandomService random)` 严格按 [契约十五 §2](./contracts/bribery.md) 的固定次序执行 —— ①校验（窗口 ∧ 在册 ∧ 级别与当前功名匹配 ∧ 未触顶；拒绝 ⇒ 不改任何字段、**不消费随机**）→ ②自算 `nth = NthOf` / 应付金额 / 被抓概率 → ③`PaymentPrimitive.Pay(economy, LedgerCategory.BribeExpense, month, amount, examinee.Id)`（**恰 1 条**资金类条目，恒为负）→ ④`random.NextDouble()` **恰 1 次**（`<` 被抓概率 ⇒ 被抓；本特性**唯一**新增随机消费点，且发生在结算之外）→ ⑤`examinee.RecordBribe(level)`（**无论是否被抓都执行**，Q1 = B）→ ⑥`family.PunishmentJournal.Append(new BribeFact(...))`（**每次恰 1 行**）；被抓 MUST NOT 退贿款、MUST NOT 落任何补偿条目；无次数上限、无拒收机制（唯一「上限」是触顶拒绝）；另加 `public static void ResetBribeCounts(Person examinee, ExamLevel? level)`（FR-031 的行贿侧）
- [X] T024 [US1] `src/KFL.Rules/Bribery/BribeEntry.cs` 第 **⑦** 段：被抓则委托 `PunishmentImposer.Apply(state, examinee, level, random)`（PunishmentImposer 主体在 T029 落地；本任务的接线 MUST 不引入按调用方分支）；US1 的非被抓/拒绝分支不依赖它
- [X] T025 [US1] 作废并**删除** 005 的占位：`src/KFL.Rules/Config/ExamPolicy.cs:63` 的 `NeutralBribeMultiplier` 与 `src/KFL.Rules/Config/GameConfig.cs:434` 的 `GameConfig.Exam.NeutralBribeMultiplier`（`k⁰` 由 `BribePolicy.UnitMultiplier` 的恒等初值唯一给出，MUST NOT 留下第二真源）
- [X] T026 [US1] `src/KFL.Rules/Exam/ExamSeasonRunner.cs:297`/`:323` 两处调用点改传 `BribeRules.MultiplierOf(level, person)`（殿试作用于**进甲率**）；`src/KFL.Rules/Exam/ExamAdmission.cs:30`/`:57` 两处 `<param>` 文档改写（**公式与 clamp 一字不动**）
- [X] T027 [US1] `src/KFL.Rules/Punishment/PunishmentQueries.cs`（新，US1 侧，**全纯函数**）：`IsWithinBribeWindow(GameDate, ExamLevel)`（FR-029③）、`Of(GameState, Person, ExamLevel)`（FR-029①：当前应付金额 / 当前被抓概率 / 是否已触顶）、`IsLevelCapReached(...)`、`FamilyCumulativeBribes(Family)`（全部成员含归档的三项计数之和）、`WasBribeFreeAtImperialPass(Person)`（存在 `ExamPass` 的进士记录 ∧ 三项计数皆为 0；**派生谓词，MUST NOT 入档**——见 plan.md 勘误 #2/#3）；MUST NOT 改状态、MUST NOT 消费随机

- [X] T028 [P] [US1] 同步断言物：`tests/KFL.Tests/Rules/ExamAdmissionTests.cs` 的 9 处与 `tests/KFL.Tests/Rules/ExamSeasonTests.cs` 的 4 处 `NeutralBribeMultiplier` 引用改为传入实际乘数；`tests/KFL.Tests/Rules/SpecAnchorTests.cs:235` 的占位锚点改为「`BribePolicy` 的 k⁰ 恒等元」

**Checkpoint**: US1 可独立验收（quickstart 场景 1、2）；MVP 边界 = Phase 1 + Phase 2 + 本阶段

---

## Phase 4: User Story 2 - 被抓惩罚：12 格矩阵、服刑、罚金与亲属禁考禁升 (Priority: P2)

**Goal**: 被抓当刻按**当前难度 × 考试级别**命中 12 格中的一格并施加全部后果：服刑（以月计）、罚金（总资产基数的比例或固定额，全域封顶 50000 贯）、相关亲属禁考与/或禁升 N 月。

**Independent Test**: 以夹具直接把「一次被抓」喂给惩罚入口（或固定种子反复行贿直到被抓），逐格断言 12 格的服刑月数、罚金金额与禁考/禁升计时是否建立（简单格禁考计时 = `null`、困难解试禁升计时 = `null`），再断言罚金条目数与余额转贷款金额。**不依赖 US1 的随机结果**。

### 测试（先写，红色）

- [X] T029 [P] [US2] `tests/KFL.Tests/Rules/PunishmentMatrixTests.cs`（新）：SC-004 的 **12 格逐格**断言（服刑月数 = 年数 × 12、罚金比例或固定额、「只禁考 / 无禁考」标记各一条；简单档三格两种计时均为 `null`、困难与地狱解试/省试三格禁升计时为 `null`，违例各 0 条；普通解试无亲属禁考、简单解试无罚金）；SC-005 的罚金口径（`现金 + 储蓄 = 30 贯` ⇒ 双清零、`Loan.Principal` +90 贯、条目金额合计 = 120 贯、各池 ≥ 0；超大总资产 ⇒ 比例罚金恰 `50000` 贯；总资产为负 ⇒ 金额 0 且条目数 0；每次罚金恰 1 条 `LedgerCategory.Fine` 资金类条目）；**服刑事件不落账本**（落在快照 `Sentences`）

### 实现

- [X] T030 [US2] `src/KFL.Rules/Punishment/PunishmentImposer.cs`（新）：`Apply(GameState, Person subject, ExamLevel level, IRandomService random)` 按 ①取该格 → ②服刑（`SentenceMonths > 0` 时建立：`StatusTimers.SentenceRemainingMonths` 与 `StatusFlag.ServingSentence`，**先计时后状态位**）→ ③罚金 → ④相关亲属禁考与/或禁升（按格标记）→ ⑦全族禁考禁升 48 月（连坐分支的 ⑤⑥ 见 T043）的次序执行，全部后果**同刻**记入 `PunishmentJournal` 并返回同一份 `PunishmentSnapshot`；另含 `FineBase(FamilyEconomy)`（转调 `TotalAssetsCalculator.Of`）、`DemoteDegree(Person, GameDate month)`、`RelativesOf(Family, Person subject)` = 当事人的「妻子/丈夫、儿子、女儿、父亲、母亲」中的**在世者**（取 `Family.Members`，**含归档**），兄弟、孙辈、姻亲 MUST NOT 被波及；**同档取替换不叠加**（MUST NOT 使剩余月数增加）
- [X] T031 [US2] `src/KFL.Rules/Punishment/PunishmentImposer.cs` 的罚金段：`ComputeFine(PunishmentCell, FamilyEconomy, Difficulty)` = `min(比例 × TotalAssetsCalculator.Of × DifficultyRates.ExpenseFactor, PunishmentPolicy.RatioFineCapGuan)`（比例格）或 `固定额 × ExpenseFactor`（固定格）；经既有 `PaymentPrimitive.Pay(economy, LedgerCategory.Fine, month, amount, subject.Id)` 支付（现金 → 储蓄 → **余额转贷款**，本特性是其**首个产品调用方**）；金额 `0` ⇒ MUST NOT 落条目；付不起 MUST NOT 阻止惩罚、MUST NOT 产生负余额
- [X] T032 [US2] `src/KFL.Rules/Settlement/MonthlySettlementEngine.cs:138-140`：「12 月工出身 bonus 的总资产为正」的就地计算改为经 `TotalAssetsCalculator.Of`（消除 FR-011 要消除的第二真源）
- [X] T033 [US2] `src/KFL.Rules/Punishment/PunishmentCommands.cs`（新，FR-031）：`ClearBans(Person)`（一次性解除自身禁考与禁升）、`PardonDeathRow(Person)`（对已亡者无效——§7.5 死刑立即执行，规则层**不存在**「死囚待决」持久状态）
- [X] T034 [P] [US2] `tests/KFL.Tests/Core/LedgerInvariantTests.cs`：`:87` 类别全集 **20 → 22**；`:89-104` 名称数组同步；`:106-112` 的「`Bribe`」禁用词守卫改写为「只允许 `BribeExpense` / `Fine`」；`:146-158` 无入账目标集合 **10 → 12**；`:264-275` 恒为负集合 **5 → 7**；`:59-78` 的 `[Theory]` **19 → 21** 行
- [X] T035 [P] [US2] `tests/KFL.Tests/Rules/SettlementEngineTests.cs`：工 bonus 锚点改为经 `TotalAssetsCalculator.Of` 判定（与 T032 同批，差异项 = 0）

**Checkpoint**: US1 与 US2 均可独立验收（quickstart 场景 1、2、3）

---

## Phase 5: User Story 3 - 服刑推进与刑满释放 (Priority: P3)

**Goal**: 把服刑/禁考/禁升三计时的递减、释放、降级与解除做成月末结算的**固定新增第 ③ 步**（插在「②米价游走」之后、「官吏推进」之前；原 ③~⑧ 顺移为 ④~⑨，**合计九步**），并保证服刑期间「暂停」与禁升期内「跳过」不混用。

**Independent Test**: 把某成员置为「服刑 5 年」并跑 **60 次**月末结算，断言每次递减 1、第 60 次后状态位与计时均空、降级恰 1 次、功名记录条数不变、其间待阙/在职计时与政绩**逐月不变**；释放次月仕途计时恢复推进。**无需 US1/US2 的随机路径**。

### 测试（先写，红色）

- [X] T036 [P] [US3] `tests/KFL.Tests/Rules/PunishmentAdvanceTests.cs`（新）：60 次结算递减与释放（`ServingSentence` = 假、`SentenceRemainingMonths` = `null`、L13 → L14、降级恰 1 次、功名史条数不变）；L18 释放后**维持 L18**；服刑期间待阙（剩余 20 月者判 12 月）与在职计时、政绩**逐月不变**、释放后从暂停处继续；服刑者**不计口**（不耗生活费、不产生收益与功名）但仍在家族列表；禁考与服刑**独立并行**（服刑 60 月 ∧ 禁考 48 月：释放后仍在禁考期、释放当月不得报名、期满可考）；在任官员禁考期内**照常在职领俸**；某月恰 3 人服刑 ⇒ 查询面与快照 `PrisonerCount` = 3 且本特性 MUST NOT 解锁「囹圄之灾」

### 实现

- [X] T037 [US3] `src/KFL.Rules/Punishment/PunishmentAdvance.cs`（新，结算第③步）：`Run(GameState)` 严格按 [契约十六 §1.2](./contracts/punishment.md) 的次序 —— ①**投影并清空**日志（`PunishmentJournal` → `PunishmentSnapshot`，随后 `Clear()`，**恰好一遍**；`IsEmpty` 时投影为空集合）→ ②按 `PersonId.Value` **升序**逐人递减（仅当计时非空：服刑 −1、禁考 −1、禁升 −1，三者互相独立、并行；**只**作用于 `StatusTimers` 的三个字段，MUST NOT 触碰 `AwaitingPostRemainingMonths`）→ ③归零处理（服刑 ⇒ 释放：**先清状态位后清计时** → `DemoteOnRelease` → 仍在禁考期则继续禁考；禁考/禁升 ⇒ 清对应计时与状态位，MUST NOT 因释放而提前清零）→ ④统计当月在册服刑人数 ⇒ `PrisonerCount`；`DemoteOnRelease(Person)`：释放时 `Rank is not null` ⇒ `new OfficialRank(Rank.Level + 1)`，已是 **L18**（`SalaryTable.LowestLevel`）则维持（判据 MUST NOT 新增「曾为官」标记）；本步 MUST NOT 调用 `AdvanceMonth`、MUST NOT 修改 `CurrentDate`、**消费随机恰好 0 次**
- [X] T038 [US3] `src/KFL.Rules/Settlement/MonthlySettlementEngine.cs`：把 T016 的 `PunishmentSnapshot.None(...)` 占位替换为 `PunishmentAdvance.Run(state)`，位置钉死在 **② 米价之后、官吏推进之前**（新的第 **③** 步；原 ③~⑧ 顺移为 ④~⑨）；类文档「八步 → **九步**」、`<list type="number">` 插第 ③ 项、`:16` 步骤数句与 `:33-37` 随机次序段顺移序号并补「惩罚推进（③）—— **0 次**」；「MUST NOT 存在」段 `:30-31` **不动**（惩罚推进是固定步骤）
- [X] T039 [US3] `src/KFL.Rules/Punishment/PunishmentQueries.cs`：追加 US3 侧纯查询 —— `StatusOf(Person)`（服刑 / 禁考 / 禁升**剩余月数** + 「是否有官职」，供 U2 的「服刑中（剩 X 年 X 月）」徽章，FR-029②）与 `PrisonerCount(Family, GameDate)`（某月在册服刑人数，FR-029④；构造「同月 3 人服刑」时返回 3）
- [X] T040 [US3] `src/KFL.Rules/Punishment/PunishmentCommands.cs`：追加 `ReleasePrisoner(Person)`（**复用**第③步的释放路径，含 `DemoteOnRelease`）
- [X] T041 [P] [US3] `tests/KFL.Tests/Rules/DeterminismTests.cs`：整月随机消费次序保持逐次可断言（米价② → 考课掷骰④-d → 储蓄利率仅 1 月⑤ → 贷款计息利率⑦ → 科举判定⑧ → 及第入仕待阙⑧），**惩罚推进③恰好 0 次**；新增 SC-001：固定种子跑两遍完整「行贿 → 被抓 → 惩罚 → 服刑 → 释放」链（含同月多人与亲属禁考），逐字段差异项 = **0**
- [X] T042 [P] [US3] `tests/KFL.Tests/Core/StatusTests.cs`：新增服刑/禁考/禁升的建立与清除断言——「计时非空 ⟺ 状态位为真」（0 也算非空）、建立**先计时后状态位**、清除**先状态位后计时**

**Checkpoint**: US1~US3 全部可独立验收（quickstart 场景 1~4、8）

---

## Phase 6: User Story 4 - 地狱殿试连坐：处死、降功名、免死与外嫁追及 (Priority: P4)

**Goal**: 只在地狱难度 × 殿试 × 被抓时触发连坐，对当事人的五类在世亲属（含**已出嫁归档的女儿**）逐人判定三分支，并落实免死例外与范围封印。

**Independent Test**: 用夹具构造「地狱难度 + 当事人处于殿试月 + 已被抓获」，家族含五类亲属各一人（其中一名无官职的孩子、一名已出嫁归档的女儿、一名不在范围内的兄弟、一名孙辈），断言三分支各自结果、免死例外、外嫁女儿被追及、兄弟与孙辈**逐字段不变**、降级记录恰 1 条且功名非升。**无需 US1 的随机路径**。

### 测试（先写，红色）

- [X] T043 [P] [US4] `tests/KFL.Tests/Rules/CollectivePunishmentTests.cs`（新）：三分支各命中 1 条（父亲处死并置已亡/归档/进族谱/计入死亡结算；儿子举人 → 白身并**恰 1 条** `PunishmentDemotion`；有官职者受全族 48 月覆盖）；免死例外（配偶有官职 ∧ 孩子未成年 ⇒ 该孩子状态**逐字段不变**且全族 48 月照常适用；四种组合各一断言；当事人本人与配偶以外的亲属不适用）；已出嫁归档女儿被跨族**追及**；**范围封印**（兄弟、孙辈、姻亲状态逐字段不变，差异项 = 0）；当事人本人死刑**立即执行**且 MUST NOT 建立服刑计时；**其余 11 格连坐触发数 = 0**；`ClearFamilyBans` 一次清除全部在册成员的相应计时、快照 `FamilyBanDeclared = 真`，且本特性 MUST NOT 自行解锁任何成就
- [X] T044 [P] [US4] `tests/KFL.Tests/Rules/BribeQueryTests.cs`（新）：SC-008 —— FR-029 ①②③④ 各调用一次，调用前后 `GameState` 逐字段差异项 = 0 且随机消费 = 0；④ 在「同月 3 人服刑」场景返回值 = 3；⑧ 所需事实（行贿/被抓、家族累计行贿、某级别触顶、清白传家、死刑、连坐致亲属死亡或降级、外嫁追及、全族禁考禁升触发、某月服刑 ≥ 3、服刑期满释放）齐备且**不解锁任何成就**

### 实现

- [X] T045 [US4] `src/KFL.Rules/Punishment/PunishmentImposer.cs`：补齐连坐段（**仅**地狱难度 × 殿试 × 被抓；其余 11 格 MUST NOT 触发）—— ⑤死刑**立即执行**（置 `StatusFlag.Deceased`、归档、进族谱、计入死亡结算，MUST NOT 建立服刑计时）；⑥对每名在世相关亲属**逐人**判定**互斥**三分支：①无功名 ∧ 无官职 ⇒ 处死（`CollectivePunishmentFact(Executed)`）；②有功名 ∧ 无官职 ⇒ 降一级功名（进士→贡士→举人→白身，白身无降）并以 `PunishmentDemotion` 追加**恰 1 条** `DegreeRecord`（复用 001 既有原因值域，MUST NOT 自创）+ `CollectivePunishmentFact(DegreeDemoted, before, after)`；③有官职 ⇒ 受全族禁考禁升 48 月覆盖（MUST NOT 另立「禁升 60 月」）；**免死例外**（配偶有官职 ∧ 该孩子未成年，「未成年」沿用 002 的 `AgeBracketPolicy`，MUST NOT 另立判据）；⑦**全族禁考禁升 48 月**逐人落地 —— 施加当刻对**每名在世在册成员**各写 48 月禁考 + 48 月禁升（复用 001 既有状态位与 `StatusTimers`，MUST NOT 新增家族级字段），施加后新出生/娶入者**不受**覆盖，并 `MarkFamilyBanDeclared()`；连坐范围严格限于五类在世亲属，已亡者跳过（不得复活、不得追加记录），已在服刑者照常被判
- [X] T046 [US4] `src/KFL.Rules/Punishment/PunishmentCommands.cs`：追加 `ClearFamilyBans(Family)`（一次性清除**全部在册成员**的相应计时，与 `StatusOf` 的「全族禁考/禁升是否生效」**走同一入口**，MUST NOT 与个人禁考/禁升形成两个互不相认的真源）

**Checkpoint**: 四个用户故事全部可独立验收（quickstart 场景 1~8）

---

## Phase 7: 持久化（跨故事；FR-032 / SC-009）

**Purpose**: 把三个计数与整本 `PunishmentJournal` 落进既有存档形状：`PersonDto` 25 → 28、`FamilyDto` 7 → 8、新增日志 DTO，
`SaveFileFormat.Version` 2 → 3，**MUST NOT** 实现旧→新迁移（004 的 Out of Scope）。`KFL.Infrastructure` **零新增文件、零新增依赖**。

- [X] T047 `src/KFL.Infrastructure/Save/Serialization/SaveStateDto.cs`：`PersonDto` 末尾追加键 **25/26/27** `jieShiBribeCount` / `shengShiBribeCount` / `dianShiBribeCount`（均 `int`），`FamilyDto` 末尾追加键 **7** `punishmentJournal`（`PunishmentJournalDto`）；既有键的序号与键名**一字不动**
- [X] T048 [P] `src/KFL.Infrastructure/Save/Serialization/SaveStateValueDtos.cs`：新增 `PunishmentJournalDto`（7 键：`familyBanDeclared`(0)、`bribes`(1)、`sentences`(2)、`fines`(3)、`relativeBans`(4)、`collectivePunishments`(5)、`releases`(6)）与六类事实行 DTO（键均自 0 连续、与 `KFL.Core` 的记录成员**逐一对应**）：`BribeFactDto` 7 / `SentenceFactDto` 3 / `FineFactDto` 5 / `RelativeBanFactDto` 6 / `CollectivePunishmentFactDto` 7 / `ReleaseFactDto` 3
- [X] T049 `src/KFL.Infrastructure/Save/Serialization/MessagePackSaveFormat.cs`：`PersonMemberCount` **25 → 28**（`:50`；`:254` 的尾随 `Skip(ref reader, 3)` → `Skip(ref reader, 6)`）、`FamilyMemberCount` **7 → 8**（`:47`；`:226` 的 `Skip(ref reader, 1)` → `Skip(ref reader, 2)`），新增 `PunishmentJournalMemberCount = 7`；其余常量不改
- [X] T050 `src/KFL.Infrastructure/Save/Serialization/SaveStateMapper.cs`：双向映射 —— 三个新计数并入既有**第 ② 步**（标量 + `RestoreExamProgress`，走 `RestoreBribeCounts`）；`PunishmentJournal.Restore(…)` 在**第 ⑦ 步之后、第 ⑧ 步之前**独立一步（不与 `Rank` / `Status` / `Timers` 的顺序耦合）
- [X] T051 `src/KFL.Infrastructure/Save/Serialization/SaveStateValidator.cs`：新增第 **⑪** 条（三个贿赂计数 `>= 0`）与第 **⑫** 条（日志内每一行 `PersonId` MUST 存在于 `Family.Members`，含归档）⇒ 校验共十二条
- [X] T052 `src/KFL.Infrastructure/Save/SaveFileFormat.cs:30`：`Version` 由 **2 升为 3**；唯一接受谓词为 `FormatVersion == 3` —— `> 3`（更新版本）与 `< 3`（含旧版 2 与 1）**一律** `UnsupportedVersion`，**MUST NOT** 提供任何迁移/兼容路径
- [X] T053 [P] `tests/KFL.Tests/Infrastructure/SaveFixtures.cs`：`RichState()` 必须扩展 —— 把三个新计数与整本 `PunishmentJournal`（含六类事实行与 `FamilyBanDeclared`）都设成**非默认值**，否则往返测试会**静默通过**
- [X] T054 [P] `tests/KFL.Tests/Infrastructure/SaveStateComparer.cs`：新增三个计数与日志全部事实行的逐字段比对（两种格式往返差异项 = 0）
- [X] T055 [P] `tests/KFL.Tests/Infrastructure/SaveFormatContractTests.cs`：键表与成员数常量断言（`PersonDto` 28 / `FamilyDto` 8 / `PunishmentJournalDto` 7 / 六类事实行 DTO 键数 / walker 的成员数常量与 DTO 键数**逐一相等**，含 `Skip` 代数）
- [X] T056 [P] `tests/KFL.Tests/Infrastructure/SaveStateValidatorTests.cs`：第 ⑪（计数为负）与 ⑫（日志行 `PersonId` 不在 `Family.Members`）各一条反例断言
- [X] T057 [P] `tests/KFL.Tests/Infrastructure/SaveFailureTests.cs`：版本闸门用例改为「**旧 2 拒绝 / 更新 4 拒绝**」，并断言拒绝**早于**主体解析（MUST NOT 出现误读）

**Checkpoint**: SC-009 可断言；两种格式承载同一信息集

---

## Phase 8: 回写义务（同一提交内；AGENTS.md 铁律 / FR-035）

**Purpose**: 本特性在公开面上做了跨特性形状变更（`Person` +3 字段、`Family` +1 成员、`LedgerCategory` 20 → 22、
`SettlementResult` +1 子段、`MonthlySettlementEngine` 八步 → 九步、`SaveFileFormat.Version` 2 → 3、
`PaymentPrimitive.Pay` +1 可选形参）。这些变更**已在变更源 [spec.md](./spec.md) 与 [plan.md](./plan.md) 声明**，
回写 MUST 与实现**同一提交**。下表 17 项与 plan.md「回写义务清单」一一对应。

| # | 目标工件 | 回写任务 |
| --- | --- | --- |
| 1 | 规格书 §7.1 / §7.4 / §7.5（顺及 §7.2） | T058 |
| 2 | `specs/002-monthly-settlement-economy/contracts/monthly-settlement.md` | T059 |
| 3 | `specs/002-monthly-settlement-economy/spec.md` | T060 |
| 4 | `specs/002-monthly-settlement-economy/data-model.md` | T061 |
| 5 | `specs/002-monthly-settlement-economy/contracts/config-registry.md` | T062 |
| 6 | `specs/001-core-skeleton/data-model.md` | T063 |
| 7 | `specs/003-opening-assets-officialdom/contracts/official-career.md` | T064 |
| 8 | `specs/003-opening-assets-officialdom/data-model.md` | T065 |
| 9 | `specs/004-save-system/contracts/save-format.md` | T066 |
| 10 | `specs/004-save-system/data-model.md` | T067 |
| 11 | `specs/005-exam-cycle/contracts/exam-session.md` | T068 |
| 12 | `specs/005-exam-cycle/spec.md` | T069 |
| 13 | `MonthlySettlementEngine` 类文档（八步 → 九步） | T038 |
| 14 | `LedgerCategory.cs` + `LedgerCategoryMetadata.cs`（20 → 22） | T008、T009 |
| 15 | `ExamSeasonRunner` + `ExamPolicy` + `GameConfig` + `ExamAdmission` | T025、T026 |
| 16 | 断言物（`Core/`、`Architecture/`、`Rules/`、`Infrastructure/`） | T017~T019、T020、T028、T029、T034、T035、T036、T041、T042、T043、T044、T053~T057 |
| 17 | `specs/006-bribery-punishment/tasks.md` 登记回写范围 | T073 |

> 另有 T070（003 契约八 §5）与 T071（005 契约十四 §1/§2）两项，出自 [契约十七 §5](./contracts/config-registry.md)
> 的「与 002 / 003 / 005 数值契约的回写关系」，与上表第 5/11 项同属数值归属回写，一并纳入本阶段。

- [X] T058 回写《科举浮生录规格书》§7.1 / §7.4 / §7.5（顺及 §7.2）：追加「§17 裁决回写」块记入 Q1（指数 n = 行贿总次数、含被抓；不清零、不退贿款）、Q2（惩罚推进步插在「官吏推进」之前 = 新第 ③ 步、八步 → 九步）、Q3（全族禁考禁升逐人计时、无家族级真源），并登记 [plan.md](./plan.md)「勘误登记」#1~#3 的判读口径；原始条目文本按 §16.2 的历史事实口径**保留不改写**
- [X] T059 回写 `specs/002-monthly-settlement-economy/contracts/monthly-settlement.md`：§1 步骤表 `:17-22` 插入**惩罚推进 = 新 ③**（原 ③~⑧ → ④~⑨，合计**九步**）并在 005 后记 `:29-33` 之后追加 006 后记；行内互引顺移（③ 的「俸禄差异经 ④ 入账」→ ⑤）；§4 次序段 `:76-77` 与表 `:79-86` 补「惩罚推进（③）—— **0 次**」并顺移 `③-d`→`④-d`、`④`→`⑤`、`⑥`→`⑦`、`⑦`→`⑧`；§7 支付原语行 `:185` 补「006 为首个产品调用方，并新增可选 `PersonId?` 形参」；§9 `MUST NOT 存在`清单 `:221-222` **移除**「罚金与 12 格惩罚矩阵」「连坐与服刑计时」并加 006 后记；文件末追加 006 快照面后记（惩罚子段）
- [X] T060 回写 `specs/002-monthly-settlement-economy/spec.md`：Status 行 `:7`（八步 → 九步 + 惩罚子段）；Out of Scope `:394-400`（把「贿赂金额与乘数、罚金矩阵、连坐与服刑/禁考计时推进」移出并加后记）；FR-020 `:306-316`（九步）；FR-017 `:292-293`（「无罚金调用方」作废）；Assumptions `:455-457`（支付原语先于罚金）
- [X] T061 回写 `specs/002-monthly-settlement-economy/data-model.md`：§2.1 `LedgerCategory` 表 `:76-108`（加 `BribeExpense` / `Fine` 两行、20 → 22、「恒为负的支出类」5 → 7、「正额入账目标为 —」10 → 12）；§4.2 `SettlementResult` `:296-318`（新增 `Punishment` 必填子段）；§4.3 `:324` 与 `:331`（九步；`PaymentPrimitive.Pay` 新签名）；§5 映射 `:348`；§6.3 `:400-401`、§6.4 `:403-408`（移除「罚金与服刑计时（阶段⑥）」）
- [X] T062 回写 `specs/002-monthly-settlement-economy/contracts/config-registry.md`：§7 `:108-111` 把「§7 贿赂的全部数值（阶段⑥）」**移出**并加 006 后记，指向 [契约十七](./contracts/config-registry.md)；§6 加一行「贿赂与惩罚数值只指向不复制」
- [X] T063 回写 `specs/001-core-skeleton/data-model.md`：`Person` 字段表 `:130-158` 末尾追加三项贿赂计数；不变量 2 `:175-192`（只读全集 + 受控写入通道）与不变量 4 `:194-200`；`StatusTimers` §1.7 `:106-124`（`:117` 的「服刑/禁考/禁升属逻辑轨 ⑥」由「明确不含」改为「已由 006 建立与递减」）；`DegreeRecord` §1.5 `:85-89`（006 是 `PunishmentDemotion` 的首个生产者）；`:379`（「服刑计时递减」由 001 的「不实现」列表移出）
- [X] T064 回写 `specs/003-opening-assets-officialdom/contracts/official-career.md`：`:8`（`PromotionBanned` / `ServingSentence` 由 **⑥ 建立**，本契约仍只消费）；§4 条款 8 `:84` 改写；§4 条款 6/7 `:82-83` 的「暂停 vs 跳过」保持并加交叉引用；§10 收尾清单 `:168` 移除「服刑与禁考/禁升计时的建立与递减、释放降级」；**新增**一条「释放降 1 级官阶」条款（**不是**改 `:81` 的考课晋升 `−1`）
- [X] T065 回写 `specs/003-opening-assets-officialdom/data-model.md`：§1.2 `StatusTimers` 表 `:33`（`PromotionBanRemainingMonths` 由「只消费」改为「由 006 建立与递减」）、`:31-32` 同款；§3.3 明确不含 `:182`；§6.5 `:459-464` 移除已交付项
- [X] T066 回写 `specs/004-save-system/contracts/save-format.md`：§4.2 键表 `:77-78`（`FamilyDto` 7 → **8**、`PersonDto` 25 → **28**）与新增日志 DTO 行；版本闸门 `:109-130`（`FormatVersion == 3`，`3`/`2`/`1` 一律拒绝，谓词与条款 1/2 同步）；§7 常量登记 `:136`（**2 → 3**）；末尾追加 006 后记
- [X] T067 回写 `specs/004-save-system/data-model.md`：§3 DTO 表 `:93-221`（`FamilyDto` +1 键、`PersonDto` +3 键、新增日志 DTO 行）；§3.11 复原次序 `:244-263`（新计数并入第 ② 步；日志复原独立一步）；§8 不变量 `:404-424`（新增不变量 18/19，`PersonMemberCount` = 28 / `FamilyMemberCount` = 8）；后记 `:440-456`（线上成员计数 **80 → 84**）
- [X] T068 回写 `specs/005-exam-cycle/contracts/exam-session.md`：§4.3 条款 4 `:155`（`NeutralBribeMultiplier` 占位**作废**——⑥ 已落地、经**同一入口**传实际值）；§9 条款 8 `:255`（「清白传家」计数已由 ⑥ 交付）；§10 表格 `:263` 把「贿赂的累进金额…服刑」移出；`:182` 的连坐降级来源标注 ⑥
- [X] T069 回写 `specs/005-exam-cycle/spec.md`：FR-027 `:317`（「清白传家」的行贿计数**已由 ⑥ 交付**）；FR-016 `:268-271`（乘数占位作废）；Out of Scope `:354` 相邻条目
- [X] T070 回写 `specs/003-opening-assets-officialdom/contracts/config-registry.md`：§5 把「贿赂与惩罚相关数值属阶段 ⑥」的措辞改为后记指向 [契约十七](./contracts/config-registry.md)（[契约十七 §5](./contracts/config-registry.md)）
- [X] T071 回写 `specs/005-exam-cycle/contracts/config-registry.md`：§2 条款 2 把「MUST NOT 引入贿赂的累进金额、被抓概率、惩罚矩阵、连坐系数（逻辑轨 ⑥）」改为「已由 ⑥ 在 `BribePolicy` / `PunishmentPolicy` 落地」；§1 **删除** `NeutralBribeMultiplier` 一行（占位作废，`k⁰` 由 `BribePolicy.UnitMultiplier` 的恒等初元给出）

> **不改的**：001~005 的既有 `tasks.md` 条目、`implementation-notes.md`、既有 `checklists/` 属**历史记录类工件**，
> MUST NOT 回改（规格书 §16.2；AGENTS.md）。001 的 `contracts/{architecture-guard,injection-seams}.md` 本特性**不改**
> （不引入新工程、不引入新接缝接口，G-01~G-08 口径与扫描范围均不变）。

### 回写范围登记（FR-035 / AGENTS.md 铁律第 3 条）

> 本表登记「同步了哪些文件、同步到哪里」，供后续审计核对。**行号为回写后的现位**；表内每一行都
> 与之对应的落点测试或守卫在同一提交内，`git status --porcelain` 里可逐一核对。

| # | 目标工件（活工件） | 同步内容（回写后锚点） | 依据 | 任务 |
| --- | --- | --- | --- | --- |
| 1 | `科举浮生录规格书.md` §7.1 / §7.2 / §7.4 / §7.5 | 四处追加「**§17 裁决回写**」块：Q1（指数 n = 行贿总次数、含被抓；不清零、不退贿款；乘数是**派生量**不入档）、Q2（惩罚推进 = 新第 ③ 步、八步 → 九步、随机 **0 次**）、Q3（全族禁考禁升**逐人计时**、**无**家族级真源）；并登记 [plan.md](./plan.md)「勘误登记」#1~#7 的判读口径。**原始条目文本按 §16.2 的历史事实口径保留不改写** | spec `## Clarifications`；struct 真源 | T058 |
| 2 | `specs/002-monthly-settlement-economy/contracts/monthly-settlement.md`（237 → 264 行） | §1 步骤表 `:17` 插入**惩罚推进 = 新 ③**（原 ③~⑧ → ④~⑨，合计**九步**）；`:36-42` 追加 006 后记（位置钉死、随机 0 次、MUST NOT 调 `AdvanceMonth`/改 `CurrentDate`、新随机次序）；`:85`/`:91` 补「惩罚推进（③）—— **0 次**」并顺移；§7 借贷与储蓄行、`:195` 支付原语行（新签名 + 006 为首个产品调用方）、`:204`、`:237`/`:241` §9 清单（移除两条）；`:256-264` 快照面后记（`Punishment` 子段） | FR-020、FR-028、SC-011 | T059 |
| 3 | `specs/002-monthly-settlement-economy/spec.md` | Status 行 `:7`（八步 → 九步 + 惩罚子段）；FR-017 `:292-299`（可选 `PersonId?`、「无罚金调用方」作废 + 同步注）；FR-020 `:306-320`（九步）；Out of Scope `:402-416`；Assumptions `:460-465`/`:474-477` | FR-028、SC-011 | T060 |
| 4 | `specs/002-monthly-settlement-economy/data-model.md` | §2.1 `LedgerCategory` 表 `:100-112`（加 `BribeExpense` / `Fine` 两行，20 → 22、「恒为负的支出类」5 → 7、「正额入账目标为 —」10 → 12、其余 15 → 17）；§4.2 `:303-322`（新增 `Punishment` 必填子段）；§4.3 `:332`/`:339`（九步；`PaymentPrimitive.Pay` 新签名 + 金额语义）；§5 `:354`/`:356`；§6.2~§6.4 `:400-410`/`:422-440` | FR-011、FR-020、FR-028 | T061 |
| 5 | `specs/002-monthly-settlement-economy/contracts/config-registry.md` | §6 `:108-117` 加「贿赂与惩罚数值**只指向不复制**」；§7 `:119` 把「§7 贿赂的全部数值（阶段⑥）」**移出**；`:134-138` 后记指向 [契约十七](./contracts/config-registry.md) | FR-001 | T062 |
| 6 | `specs/001-core-skeleton/data-model.md` | `Person` 字段表 `:169-171` 追加三项贿赂计数；不变量 2 `:192-193`/`:207-212`（只读全集 + **受控写入通道**）；不变量 4 `:221-225`；`StatusTimers` §1.7 `:125-128`（「服刑/禁考/禁升属逻辑轨 ⑥」由「明确不含」改为「已由 006 建立与递减」）；`DegreeRecord` §1.5 `:85`（006 是 `PunishmentDemotion` 的首个生产者）；`:405-410` §5（「服刑计时递减」移出不实现清单 + 006 后记） | FR-004、FR-014~FR-021 | T063 |
| 7 | `specs/003-opening-assets-officialdom/contracts/official-career.md` | `:8-9` 总纲（`PromotionBanned`/`ServingSentence` 由 ⑥ 建立、本契约只消费）；§4 条款 6/7 `:83-84` 加交叉引用并保留「暂停 vs 跳过」；条款 8 `:85` 改写；`:87` **新增条款 10「释放降 1 级官阶」**（不动 `:82` 的考课晋升 `−1`）；§10 `:170` 移除已交付项；`:176-187` 006 后记 | FR-020、SC-011 | T064 |
| 8 | `specs/003-opening-assets-officialdom/data-model.md` | §1.2 `StatusTimers` 三行 `:31-33` 改「由 006 建立与递减」；§3.3 明确不含 `:182`；§6.5 `:466-468` + 006 后记 | FR-021、SC-011 | T065 |
| 9 | `specs/004-save-system/contracts/save-format.md` | §4.2 键表 `:77-78`（`FamilyDto` 7 → **8**、`PersonDto` 25 → **28**）与 8 个新增日志/事实 DTO 行 + 「事实行 DTO 键数 MUST 等于对应 `KFL.Core` 记录成员数」；版本闸门 `:128`/`:137`（`FormatVersion == 3`，`3`/`2`/`1` 一律拒绝、无迁移）；§7 常量登记 `:149-152`（**2 → 3** 与九个 member count）；`:204-215` 006 后记 | FR-032 | T066 |
| 10 | `specs/004-save-system/data-model.md` | §3.2 `:123`（`FamilyDto` +1）；§3.8 `:217-219`/`:226-233`（`PersonDto` +3 与 8 个新增子 DTO 行）；§3.11 `:263`/`:269`（新计数并入第 ② 步；日志复原为独立第 **⑦′** 步）；`:272`（十条 → **十二条**）；§7 `:396`（线上成员计数 80 → **84**）；§8 `:442-444`（不变量 17 更新 + 新增 18/19）；`:480-493` 006 后记 | FR-032 | T067 |
| 11 | `specs/005-exam-cycle/contracts/exam-session.md` | §4.3 条款 4 `:155`（`NeutralBribeMultiplier` 占位**作废**——⑥ 已落地、经**同一入口**传实际值，签名/公式/clamp 一字未动）；§5 条款 8 `:182`（连坐降级施加者标注 ⑥）；§9 条款 8 `:255`（「清白传家」计数已由 ⑥ 交付）；§10 `:261`/`:269-272` | FR-008、FR-030、SC-011 | T068 |
| 12 | `specs/005-exam-cycle/spec.md` | FR-016 `:268-273`（乘数占位作废）；FR-027 `:319-321`（「清白传家」行贿计数已由 ⑥ 交付）；Out of Scope `:358-360` | FR-030、SC-011 | T069 |
| 13 | `specs/003-opening-assets-officialdom/contracts/config-registry.md` | §5 `:102-110` 把「贿赂与惩罚矩阵（§7）的全部数值（逻辑轨 ⑥）」**移出**并加 006 后记，指向 [契约十七 §5](./contracts/config-registry.md) | FR-001 | T070 |
| 14 | `specs/005-exam-cycle/contracts/config-registry.md` | §1 `:32` **删除** `NeutralBribeMultiplier` 一行；§2 条款 2 `:57` 改为「已由 ⑥ 在 `BribePolicy` / `PunishmentPolicy` 落地」；§3.3 `:107` 消除对被删占位的悬空引用 | FR-001、FR-008 | T071 |
| 15 | `specs/006-bribery-punishment/` 自身活工件 | `contracts/punishment.md`（§6.3 免死口径勘误 #7、§1.2/§3/§7 置位次序勘误 #5、§4.2 罚金金额口径勘误 #4）；`contracts/config-registry.md`、`contracts/bribery.md`（同批核对）；`data-model.md`（§2.2 `FineFact` 形状、§2.3 `Clear()` 可见性、§3.2/§3.9 成员名、§7.1 DTO 键数、I-5 次序 —— 勘误 #6）；`plan.md`「勘误登记」#1~#7 与「回写义务清单」；`quickstart.md` 场景 3 期望 6、场景 5 期望 2；`spec.md` SC-005 与 US2 AS5（勘误 #4）；**T077**：`spec.md` FR-014 置位/清除次序改为「建立 = 先置状态位、后写计时；清除 = 先清计时、后清状态位」；**T079**：`spec.md` Assumptions「放宽上限」接缝缺口开放项 + `plan.md:143` 引用改指该条；**T080**：`contracts/punishment.md` §8/§9 与 `research.md` R-09/R-10 明确「当事人本人的死刑 = `Person.Status` 状态派生，**不经**快照六类事实行」 | FR-035；spec `## Clarifications` | T029、T037、T042、T048、T074、T077、T079、T080 |
| 16 | 源码类文档（同提交） | `src/KFL.Rules/Settlement/MonthlySettlementEngine.cs` 类文档八步 → **九步** + 第 ③ 项 + 随机次序段；`src/KFL.Core/Enums/LedgerCategory.cs` + `LedgerCategoryMetadata.cs`（20 → 22、其余 15 → 17、恒为负 5 → 7、两处 `<exception>` 与两处 `default:` 文案）；`src/KFL.Rules/Config/ExamPolicy.cs` + `Config/GameConfig.cs`（`NeutralBribeMultiplier` 删除、`GameConfig.Bribe` / `GameConfig.Punishment` / `Assets.TotalAssets` 转发视图）；`src/KFL.Rules/Exam/ExamAdmission.cs` `:30`/`:57` 两处 `<param>`；`src/KFL.Rules/Economy/PaymentPrimitive.cs`（`PersonId?` 形参与调用方说明）；**T078**：`src/KFL.Infrastructure/Save/SaveFileHeader.cs:21`（「本版本恒为 2」）、`src/KFL.Infrastructure/Save/SaveFileException.cs:21`（`FormatVersion != 2`）、`src/KFL.Infrastructure/Save/Serialization/JsonSaveFormat.cs:16`（样例 `"FormatVersion":2`）三处文档注释统一改为 **3** | FR-008、FR-020、FR-027、SC-003、SC-011 | T009、T012、T014、T025、T026、T038、T078 |
| 17 | 断言物（同提交，**不是**历史工件） | `Rules/SpecAnchorTests.cs`（12 格逐格锚点 + `BribePolicy` 的 k⁰ 恒等元）；`Rules/ExamAdmissionTests.cs` 9 处与 `Rules/ExamSeasonTests.cs` 4 处引用；`Core/PersonTests.cs`（只读成员全集 16 → 19）；`Core/FamilyTests.cs`；`Core/StatusTests.cs`（建立/清除次序两条）；`Core/LedgerInvariantTests.cs`（`:87` 计数、名称数组、`Bribe` 禁用词守卫改写、无入账目标 10 → 12、恒为负 5 → 7、Theory 19 → 21 行）；`Architecture/ConfigLiteralTests.cs`（`RegisteredValues` 46 → 49）；`Infrastructure/{SaveFixtures,SaveStateComparer,SaveFormatContractTests,SaveStateValidatorTests,SaveFailureTests}.cs`；**T080**：`tests/KFL.Tests/Rules/BribeQueryTests.cs`（查询面**八个**入口的纯函数不变性断言 + FR-030 事实五断言改为 `PunishmentQueries.WasExecutedByState`） | FR-001、FR-032、SC-002、SC-009 | T017~T019、T028、T034~T036、T041~T044、T053~T057、T080 |
| 18 | **超出 plan.md「回写义务清单」的一致性补做**（同批登记，供审计） | ① `specs/002-monthly-settlement-economy/quickstart.md` `:48`（八步 → 九步，含第 ③ 步）；② 同目录 `research.md` R-16（八步 → 九步）；③ `specs/001-core-skeleton/spec.md` `:207`（八步 → 九步，注明第 ③/④/⑧ 步的插入者）；④ `specs/005-exam-cycle/data-model.md` `:247`/`:433`/`:453`（`NeutralBribeMultiplier` 清单项与接缝表 + 006 后记）；⑤ 同目录 `research.md` `:152`（占位删除后记）。五处均为「被触碰的字段表/步骤序列仍是旧值」的活工件，按 AGENTS.md 铁律第 1 条一并同步 | AGENTS.md 铁律第 1 条；真源一致性 | T059~T071 同批（父代理补做） |
| 19 | `specs/006-bribery-punishment/tasks.md` | 本表：登记上述回写范围（同步了哪些文件、同步到哪里），供后续审计核对 | AGENTS.md 铁律第 3 条 | T073 |
| 20 | **收敛批次 T077~T080 的同步核算**（同批登记，供审计） | **T077** → 第 15 行（`spec.md` FR-014 置位/清除次序）；**T078** → 第 16 行（`SaveFileHeader` / `SaveFileException` / `JsonSaveFormat` 三处「版本 2」注释）；**T079** → 第 15 行（`spec.md` Assumptions FR-031 开放项 + `plan.md:143` 引用）；**T080** → 第 15 行（`contracts/punishment.md` §8/§9、`research.md` R-09/R-10）与第 17 行（`Rules/BribeQueryTests.cs`）与源码（`KFL.Rules/Punishment/PunishmentQueries.cs` 新增纯函数 `WasExecutedByState(Person)`，**不动** `SaveFileFormat.Version`、不动存档形状）。四条均属「活工件与实现不一致时以规格书为准修正工件」的同批收敛，无跨特性形状变更 | AGENTS.md 铁律第 1/3 条；FR-029、FR-030、FR-031；SC-010 | T077~T080 |
| 21 | **PR #13 评审收敛批次 T082~T087 的同步核算**（同批登记，供审计） | **T082** → 源码（`src/KFL.Rules/Punishment/PunishmentImposer.cs` 的 `IsExemptFromExecution` + 新增 `IsChildOf`；真源 = 规格书 §7.4 与 §17 ③「只适用于孩子」）与断言物（`tests/KFL.Tests/Rules/CollectivePunishmentTests.cs` 新增「免死例外不扩展到未成年非子女亲属」一例）；**T083** → 源码（同文件 `Collective` 的有官职分支回收 `Ban` 的返回值）与断言物（同文件 `连坐三分支各命中一条且后果与格一致` 追加 `RelativeBans` 断言）；**T084** → 源码（`src/KFL.Rules/Bribery/BribeRules.cs` 改读 `BribePolicy.WindowPreviousMonths` 单一真源 + 新增私有 `SubtractMonths`）；**T085** → 源码（`src/KFL.Core/ValueObjects/PunishmentFacts.cs:84-85`/`:109`/`:111` 的 `Subject`/`Relative` 注释改为与 `PunishmentImposer.cs:22-26` 的口径同向）；**T086** → 第 9/10/15/16 行（`save-format.md` 的 `:132` 十条→十二条、`:149` 断言锚点、§6 条款表新增第 8 条；`save-service.md:61`/`:62`；`specs/004-save-system/plan.md:231`/`:283`；`SaveService.cs:200`、`ISaveFormat.cs:20`、`SaveStateValidator.cs:267`/`:316`；`specs/006-bribery-punishment/plan.md:68-70`、`research.md:445`、`quickstart.md:209`）；**T087** → 第 15 行（`spec.md` Assumptions 新增「零资金池行贿的账本条目」开放项，**不改**代码与契约措辞，等规格书 §17 裁决）。全批次**不改任何数值、不动存档形状、不动跨特性契约的形状** | AGENTS.md 铁律第 1/3 条；真源一致性；FR-009、FR-032 | T082~T087 |
| 22 | **§17 裁决收敛批次 T089~T093 的同步核算**（同批登记，供审计） | **T089** → 规格书 `科举浮生录规格书.md` §7.1 新增 `§17 裁决回写（2026-10-17，行贿的资金来源）` 块；**T090** → 源码（`src/KFL.Rules/Economy/PaymentPrimitive.cs` 新增 `CanCover` 与类文档；`src/KFL.Rules/Bribery/BribeOutcome.cs` 的 `BribeRejectReason.InsufficientFunds` / `BribeFeasibility.IsAffordable` / 删 `BribeOutcome.ToLoan`；`src/KFL.Rules/Bribery/BribeRules.cs` 的 `CanBribe` 判定次序末尾追加可付性；`src/KFL.Rules/Bribery/BribeEntry.cs` 删转贷款自守与类文档）；**T091** → 断言物（`tests/KFL.Tests/Rules/BribeEntryTests.cs` 三例新增 + 一例改述）；**T092** → 第 15 行（`specs/006-bribery-punishment/` 的 `spec.md` / `contracts/bribery.md` / `data-model.md` / `plan.md` 四件自身活工件）。罚金侧（`contracts/punishment.md`、`FineFact.ToLoan`）与存档形状**不动**，`PaymentPrimitive.Pay` 的三段语义逐位不变 | AGENTS.md 铁律第 1/2/3 条；规格书 §5.4、§7.1；FR-007 | T089~T093 |

**回写核算结论（T073）**：[plan.md](./plan.md)「回写义务清单」**17 项**逐条对应到上表第 1~17 行，
**无遗留项**；另有 tasks.md 第 T070/T071 点名的 2 处（003 / 005 的 `contracts/config-registry.md`）落在上表第 13/14 行，
以及实现期发现的 5 处「旧值活工件」落在第 18 行——三者合计 19 行，全部在**同一工作树内**完成。
第 20 行登记收敛批次 T077~T080 的落点（其条目已并入第 15/16/17 行，此处只做批次级核算）。

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: 门禁、结果确定性复核、人工验收与提交切分

- [X] T072 跑门禁四条并复核 SC-010：`dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false`（**0 警告 0 错误**）、`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false`（**全绿、0 skipped**）、`dotnet sln KejuFuShengLu.slnx list`（**恰好六个工程**）、`Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }`（**期望无输出**）；随后 `git status --porcelain` 期望**变更数 = 0**
- [X] T073 在本 `tasks.md` 的「回写义务」节登记回写范围（**同步了哪些文件、同步到哪里**，17 项逐条对应任务号；AGENTS.md 铁律第 3 条 / 章程「需求真源」）；核对 [plan.md](./plan.md)「回写义务清单」无遗留项
- [X] T074 依 [quickstart.md](./quickstart.md) 逐条人工核对 **8 个场景**（场景 1~8）的期望项与落点测试一一对应；重点复核：被删占位的不可达性（`NeutralBribeMultiplier` 在**任何**允许多次尝试的路径上都不可达）、12 格逐格、连坐范围封印、查询面纯函数性、`git status` 无残留
- [X] T075 仓库形状与门禁注释态核验：`KejuFuShengLu.slnx` 仍恰好六个 `<Project Path=… />`（G-02）；`tests/KFL.Tests/KFL.Tests.csproj:21-25` 的 `_MSTestEnableParentProcessQuery` 保持**注释态**；`KFL.Core` / `KFL.Rules` 仍是**零**文件系统访问（G-07 零违规）；`KFL.Presentation` / `KFL.App` **一行未动**；零新增 NuGet 依赖
- [X] T076 按章程原则 V 切分并提交（中文提交信息、一次提交只做一件事）：`feat(core)`（T003~T009）、`feat(rules)`（T010~T016、T021~T027、T030~T033、T037~T040、T045~T046）、`feat(infrastructure)`（T047~T052）、`test(...)`（T017~T020、T028、T029、T034~T036、T041~T044、T053~T057）、`docs(...)`（T058~T071 的上游活工件回写与 §17 裁决回写）；**破坏性变更**（存档 `Version` 2 → 3）在正文以 `BREAKING CHANGE:` 说明「拒绝旧档、无迁移路径」（004 的 Out of Scope）

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup（Phase 1）**: 无依赖，可立即开始
- **Foundational（Phase 2）**: 依赖 Setup 完成 —— **阻塞全部用户故事**
- **User Stories（Phase 3~6）**: 全部依赖 Foundational 完成
  - US1（P1）→ US2（P2）→ US3（P3）→ US4（P4）可顺序推进；除下条注明的一处接线外，四者可并行（若人力允许）
  - **跨故事接线（唯一一处）**: T024（US1 的第 ⑦ 段）依赖 T030（US2 的 `PunishmentImposer.Apply`）；US1 的窗口/金额/概率/乘数/触顶/拒绝路径不依赖 US2
  - T032（US2）与 T035（US2 测试）同批；T038（US3）依赖 T037；T039/T040 依赖 T037 的释放路径
- **持久化（Phase 7）**: 依赖 Foundational（T003、T006）完成；与 US1~US4 无代码耦合，可并行
- **回写义务（Phase 8）**: 与实现**同一提交** —— 需实现定型后回写（建议在 US1~US4 与 Phase 7 之后执行；T062/T066/T068/T070/T071 只需形状定型）
- **Polish（Phase 9）**: 依赖全部前置阶段完成

### Within Each User Story

- 测试先写、**跑红**再实现（FR-034；章程原则 IV：测试与实现同批提交）
- 类型 → 纯函数规则 → 行动/施加 → 查询面 → 断言物同步
- 每个故事在自己的 Checkpoint 处可独立验收，故事完成后再推进下一个优先级

### Parallel Opportunities

- Phase 2 的 `[P]` 任务（T003~T006、T008~T011、T013~T015、T017~T019）可并行：分别落在 `Person.cs` / `PunishmentFacts.cs` / `CollectiveOutcome.cs` / `PunishmentJournal.cs` / `LedgerCategory.cs` / `LedgerCategoryMetadata.cs` / 两个配置类 / `TotalAssetsCalculator.cs` / `PaymentPrimitive.cs` / `PunishmentSnapshot.cs` 与四个测试文件，不同文件无依赖
- 每个故事的**测试文件**（T020/T029/T036/T043/T044）互不冲突，可并行编写
- Phase 7 的五个测试文件（T053~T057）按不同文件并行
- Phase 8 的十四项回写分属不同工件（规格书 / 001~005 各自文档），可并行

### 建议的并行示例

```text
# Foundational 批次（不同文件）：
T003 Person.cs                      T004 PunishmentFacts.cs
T008 LedgerCategory.cs              T010 BribePolicy.cs
T011 PunishmentPolicy.cs            T013 TotalAssetsCalculator.cs

# US1 测试与 US2 测试同时开写：
T020 Rules/BribeEntryTests.cs       T029 Rules/PunishmentMatrixTests.cs

# Phase 7 存档断言物并行：
T053 SaveFixtures.cs   T054 SaveStateComparer.cs   T055 SaveFormatContractTests.cs
```

---

## Implementation Strategy

### MVP First（User Story 1 Only）

1. 完成 Phase 1：Setup
2. 完成 Phase 2：Foundational（**CRITICAL** —— 阻塞全部故事）
3. 完成 Phase 3：US1（含 T024 的最小编排接线，可在 T030 之后合入）
4. **STOP and VALIDATE**：按 quickstart 场景 1、2 独立验收 US1（行贿窗口 / 金额 / 概率 / 乘数 / 触顶 / 占位删除）
5. 交付演示（界面属 U1~U5，本特性只交付规则入口与查询面）

### Incremental Delivery

1. Setup + Foundational ⇒ 基础设施就绪（构建全绿、既有测试全绿）
2. **+ US1** ⇒ 行贿链路可断言（MVP）
3. **+ US2** ⇒ 12 格惩罚矩阵与罚金口径可逐格断言
4. **+ US3** ⇒ 服刑递减、释放降级、结算九步
5. **+ US4** ⇒ 地狱殿试连坐、免死例外与范围封印
6. **+ Phase 7** ⇒ 存档往返与版本闸门
7. **+ Phase 8** ⇒ 上游活工件与规格书 §7 回写（同一提交）
8. **+ Phase 9** ⇒ 门禁四条全过、`git status` 无残留

### 交付前的不可省项

- 门禁第四条（`dotnet sln list` = 六工程）与「无 `*.sln`」检查不能省（`.slnx` 解析器不认识 `<TestProject>` 且不报错）
- `ConfigLiteralTests` 断言配置字面量违规数 = **0**（SC-002）
- `DeterminismTests` 的逐月 `NextDouble` 计数（1 次；1 月 2 次；计息节点再 +1）必须保持全绿
- 回写 17 项必须在**同一提交**内完成，且在本 `tasks.md` 登记（SC-011）

---

## Notes

- `[P]` = 不同文件、无依赖；`[Story]` 只用于用户故事阶段，Setup / Foundational / 持久化 / 回写 / Polish 阶段 MUST NOT 带故事标签
- 全部测试**无界面、无文件系统、无网络**（唯一例外是 Phase 7 的存档往返，沿用 004 的临时目录沙箱）；随机一律经可设种子的 `IRandomService`
- **随机消费的两条铁律**：行贿结算外消费 **恰好 1 次**（T023 ④）；惩罚推进步（③）消费 **恰好 0 次**（T037）
- **数值真源**：一切数值逐字取自规格书 §7.1 / §7.2 / §7.3 / §7.4 / §7.5 / §11，单点声明在 `BribePolicy` / `PunishmentPolicy`，MUST NOT 自创、改写或简化
- **不变量**（`data-model.md` I-1~I-19）与 **SC-001~SC-011** 是每个 Checkpoint 的验收判据
- 提交粒度：每条任务或逻辑任务组一次提交；章程原则 V 的中文提交信息
- 严禁：自建视图/按钮（章程 III）、按调用方分支的绕过通道（章程 VI）、旧档迁移（004 Out of Scope）、成就解锁与绝嗣判定（逻辑轨 ⑧）

---

## Phase 10: Convergence（2026-10-16）

**Purpose**: 收口 006 已交付实现与「规格书 / 本特性活工件」之间的**四处一致性缺口**。实现侧门禁已全绿
（`dotnet build` 0 警告 0 错误；`dotnet test` 失败 0 / 通过 714 / 跳过 0；`dotnet sln list` 恰六工程；
递归无 `*.sln`；工作区干净，HEAD `baaa733c`），故本阶段**不改行为、不新增数值**，只把真源与工件对齐。

**收敛判据**：源需求（`spec.md` + 规格书 §7）逐条对照实现的结论见下表；「已满足」项不再追加任务。

| # | 严重度 | 类别 | 缺口（锚点） | 结论 |
| --- | --- | --- | --- | --- |
| C-1 | High | contradicts | `spec.md:306-307`（FR-014）仍写「置位次序 MUST 为**先计时后状态位**（清除时相反）」，而实现与自身契约是**先状态位后计时** | 未回写：`plan.md:172` 勘误 #5 已裁定「以既有实现与既有测试为准」，`contracts/punishment.md:52` 条款 6 与 `data-model.md` I-5 均已修正，**唯独 `spec.md` FR-014 漏改** |
| C-2 | Low | partial | 三处源码文档注释仍写「存档格式版本 2」 | `src/KFL.Infrastructure/Save/SaveFileHeader.cs:21`「本版本恒为 2」、`src/KFL.Infrastructure/Save/SaveFileException.cs:21`「`FormatVersion != 2`」、`src/KFL.Infrastructure/Save/Serialization/JsonSaveFormat.cs:16` 的 JSON 形状 `"FormatVersion":2`——与 `src/KFL.Infrastructure/Save/SaveFileFormat.cs:31` 的 `Version = 3`（及 `:28-29` 的 2 → 3 沿革）矛盾 |
| C-3 | Medium | partial | FR-031 的「放宽上限 MUST 经装配期注入的策略提供」既无实现、也未在本特性 `spec.md` 登记为开放项 | `spec.md:375-378` 仍是 MUST；`contracts/bribery.md:106` 条款 4 与 `research.md` R-14（`:381-405`）都写「本特性不预置、接缝由 U4 规格化时定义」；`plan.md:143` 又声称「见 Assumptions 的开放项」，而 `spec.md` 的 Assumptions（`:492-571`）只登记了「赦免死囚」（`:565-567`） |
| C-4 | Medium | partial | FR-030 的「死刑（身首异处）」事实**不在 §9 快照**里，⑧ 只能读 `Person.Status` | `contracts/punishment.md:234-236` 写「⑧ 要求的其余事实（死刑、连坐致亲属死亡或降级、…）经 §9 的当月快照消费」，`research.md:265`（R-09 的 ①）也把「死刑」列为投影项；但 `PunishmentSnapshot` 只有六类事实行（`contracts/punishment.md:248-249`、`src/KFL.Rules/Punishment/PunishmentSnapshot.cs`），`src/KFL.Core/ValueObjects/PunishmentFacts.cs` 的六条记录里**没有**「当事人本人被处死」这一条（`CollectivePunishmentFact.Outcome == Executed` 只覆盖被连坐的亲属）；`tests/KFL.Tests/Rules/BribeQueryTests.cs:146-149` 因此改为断言 `subject.Status.HasFlag(StatusFlag.Deceased)`（状态而非事实） |

### 收敛任务

- [X] T077 [P] 回写 `specs/006-bribery-punishment/spec.md` FR-014（`:306-307`，C-1）：把「置位次序 MUST 为**先计时后状态位**（清除时相反）」改为与实现一致的口径——**建立 = 先置状态位、后写计时；清除 = 先清计时（写 `null`）、后清状态位**，并补一句「（原写「先计时后状态位」，已按 001 既有实现与既有测试修正，见 `plan.md` 勘误 #5）」。依据：`plan.md:172`（勘误 #5）、`src/KFL.Core/Entities/Person.cs:370-397`（`Status` / `Timers` 两个交叉校验 setter 强制该次序）、`src/KFL.Rules/Punishment/PunishmentImposer.cs:282`（先置 `StatusFlag.ServingSentence` 再写 `StatusTimers`）、`src/KFL.Rules/Punishment/PunishmentAdvance.cs:167-179`（清除时先清计时后清状态位）、`tests/KFL.Tests/Core/StatusTests.cs` 的建立/清除次序两条；AGENTS.md 铁律第 3 条要求同时把该回写登记进本文件的「回写范围登记」表（第 15 行 `spec.md` 一栏补「FR-014 置位/清除次序」）。验收：`spec.md` 全文不再出现「先计时后状态位」，且与 `contracts/punishment.md:52` 逐字同向。
- [X] T078 [P] 修正三处仍写「版本 2」的源码文档注释（C-2）：`src/KFL.Infrastructure/Save/SaveFileHeader.cs:21`（`<param name="FormatVersion">` 的「本版本恒为 2」）、`src/KFL.Infrastructure/Save/SaveFileException.cs:21`（`FormatVersion != 2`）、`src/KFL.Infrastructure/Save/Serialization/JsonSaveFormat.cs:16`（文件形状样例 `"FormatVersion":2`）一律改为 **3**，与 `src/KFL.Infrastructure/Save/SaveFileFormat.cs:31`（`public const int Version = 3;`）及 `:28-29` 的版本沿革、`specs/004-save-system/contracts/save-format.md:128`/`:137`/`:211`（接受谓词 `FormatVersion == 3`）一致；并把这三处补进本文件「回写范围登记」表第 16 行（源码类文档）。依据：AGENTS.md 铁律第 1 条；`specs/006-bribery-punishment/contracts/config-registry.md` 与 004 契约十 §7 的常量登记。验收：`src/` 下不再有「恒为 2」「`!= 2`」「`FormatVersion":2`」字样，`GitHub` 差异仅注释。
- [X] T079 结清 FR-031「放宽上限」的接缝口径（C-3，**默认取 ①**）：①在 `specs/006-bribery-punishment/spec.md` 的 Assumptions（`:492-571`，紧邻「赦免死囚」条 `:565-567`）登记开放项——「「放宽上限」（如突破 `0.899999`）MUST 以**装配期显式注入的策略**提供，其接缝由 U4 规格化时定义；本特性 MUST NOT 预置该接缝、MUST NOT 出现按调用方分支的绕过路径」，并改掉 `plan.md:143` 对「Assumptions 的开放项」的悬空引用，使其指向该条；②（若判定本特性就落地，则改为在 `KFL.Rules/Config/` 定义注入式封顶策略，并把 `src/KFL.Rules/Bribery/BribeRules.cs:177-179` 直读 `ExamPolicy.DiceRateCap` 的触顶判据改为经该策略取值）。依据：章程原则 VI；`spec.md:375-378`；`contracts/bribery.md:106` 条款 4；`research.md` R-14（`:381-405`）。验收：`spec.md` FR-031 与 Assumptions 不再自相矛盾，`plan.md` 的引用可解析到具体条目。
- [X] T080 结清 FR-030「死刑（身首异处）」事实的取值来源（C-4，**默认取 ①**）：①按「不加字段、不动存档版本」的最省改法，把工件口径改为事实取自**状态派生**——在 `contracts/punishment.md` §8 的条款句（`:234-236`）与 §9 的清单（`:248-249`）明确「当事人本人的死刑由 `Person.Status` 的已亡位与归档派生值表达，**不经** §9 快照的六类事实行」，同步 `research.md:265`（R-09 的 ① 投影清单）与 R-10（`:294-296`）措辞，并在 `PunishmentQueries` 补一个纯函数（如 `bool WasExecutedByState(Person)`）供 ⑧ 与 U2 走同一入口（FR-029），`tests/KFL.Tests/Rules/BribeQueryTests.cs:146-152` 的注释与断言随之对齐（断言口径 = 状态派生）；②（若判定必须由快照承载，则新增第 7 类事实行 `ExecutionFact(Date, Subject)`，同步 `src/KFL.Core/ValueObjects/PunishmentFacts.cs`、`PunishmentJournal`、`SaveStateMapper` / `SaveStateValueDtos` / `SaveStateValidator` 第 ⑫ 条与 004 键表，并把 `SaveFileFormat.Version` 由 3 提升——该路线属跨特性形状变更，须先在 `spec.md` / `plan.md` 声明）。依据：`spec.md:368-374`（FR-030）、`:363-367`（FR-029）；`specs/004-save-system/data-model.md:487-493`。验收：⑧ 解锁「身首异处」所需事实的来源在 `spec.md` / 契约 / `research.md` / 断言物四处**同向**，且本特性仍 MUST NOT 自行解锁任何成就。
- [X] T081 门禁复跑与收口（与 T077~T080 同一提交或紧随其后）：`dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false`（0 警告 0 错误）、`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false`（全绿、0 skipped）、`dotnet sln KejuFuShengLu.slnx list`（恰六工程）、递归无 `*.sln`、`git status --porcelain` 与提交前一致（SC-010）；并在本文件「回写范围登记」表补登 T077~T080 的同步范围。依据：AGENTS.md 门禁四条；SC-010、SC-011。

---

## Phase 11: 评审收敛（2026-10-16，PR #13 评审）

**Purpose**: 收口 PR #13 评审（`gh pr view 13`，实现侧门禁在评审时已全绿）发现的 **4 处代码/注释偏差**
与 **5 类活工件回写缺口**。本阶段**不改任何数值**、不动存档形状、不动跨特性契约的形状；唯一的判据
收紧是 T082，其真源（规格书 §7.4 与 §17 ③「免死例外只适用于孩子」）**已在规格书中裁定**，故属
「按真源修正代码」而非「先行变更」。

**收敛判据**：评审逐条对照真源的结论见下表；「已满足」项不再追加任务。

| # | 严重度 | 类别 | 缺口（锚点） | 结论 |
| --- | --- | --- | --- | --- |
| R-1 | Medium | contradicts | 免死例外**比真源宽**：`src/KFL.Rules/Punishment/PunishmentImposer.cs:387-396` 只判「当事人的配偶有官职 ∧ 该成员未成年」，**未**判「该成员是当事人的孩子」。真源：`科举浮生录规格书.md:283`、`:297-298`（§17 ③「免死例外的判据 = 当事人配偶有官职 ∧ **该孩子**未成年 … **只适用于孩子**、不适用于当事人本人、不扩展到配偶以外的亲属」）、`contracts/punishment.md:181-185` | **改代码**（规格书是唯一真源；反向推理禁止：不能因「代码已经这么写」而认定规格书过时）。暴露面为潜在（父亲/母亲通常成年），但无测试覆盖该判据 |
| R-2 | Low | partial | `PunishmentImposer.Collective` 的**有官职**分支（`:352`）丢弃 `Ban(...)` 返回的 `RelativeBanFact`（同文件 `:92`/`:131` 都接得住），使 `Apply` 返回的 `PunishmentSnapshot.RelativeBans` **漏报**该亲属一条事实；而 `contracts/punishment.md:177` 要求 `CollectivePunishmentFact(FamilyWideBan)` **+ 逐人** `RelativeBanFact(48, 48)`，`PunishmentImposer.cs:18-19` 也写「投影为**同一份**快照」。日志侧正确，仅即时快照少报 | **改代码**（不动结算路径的投影——它本就读日志，是正确的） |
| R-3 | Low | 契约内部矛盾 | 零资金池行贿落 **0** 条 `BribeExpense`：`src/KFL.Rules/Economy/PaymentPrimitive.cs:97-101` 仅在 `fromTreasury` 为正时记账，而 `contracts/bribery.md:37`/`:142` 要求「**恰好 1 条**」，`:145` 又写「MUST NOT 记 0 金额条目」。行贿金额全部由借贷补足时两条 MUST 互斥 | **不擅改**：须先经规格书 **§17 裁决**。本阶段只登记开放项（T087），MUST NOT 动代码或契约措辞 |
| R-4 | Low | partial | 注册表常量 `BribePolicy.WindowPreviousMonths`（`src/KFL.Rules/Config/BribePolicy.cs:30`）已注册（`contracts/config-registry.md:25`）、已转发（`GameConfig.cs:464`）、已被锚点钉住（`SpecAnchorTests.cs:266`），却**无人读取**——窗口宽度硬编码在 `BribeRules.cs:35-36`，改常量不生效 | **改代码**（恢复「数值单点、只指向不复制」的既有纪律；值为 1 时行为逐字不变） |
| R-5 | Low | partial | 公开 Core 类型的 XML 注释把 `Subject`/`Relative` **写反**：`src/KFL.Core/ValueObjects/PunishmentFacts.cs:84-85`（`RelativeBanFact`）与 `:109`/`:111`（`CollectivePunishmentFact`）与实现口径（`PunishmentImposer.cs:22-26`）及断言物（`CollectivePunishmentTests.cs:125`）矛盾 | **改注释**（公开类型的文档失真；无行为变更） |
| R-6 | Low | partial | 五类活工件回写缺口：① `specs/004-save-system/contracts/save-format.md:149` 的断言锚点写「版本闸门的用例（**3 及以上拒绝**、2 拒绝、1 拒绝）」，与同文件 `:128` 的谓词 `FormatVersion == 3` 直接矛盾（照此写法等于要求闸门拒绝**当前**版本）；② `:132`「一致性校验（十条）」与 `save-service.md:61`「`FormatVersion != 2`」/`:62`「十条」、`specs/004-save-system/plan.md:231`（80 个线上成员）/`:283`（十条）、`SaveService.cs:200`、`ISaveFormat.cs:20`（九条）全部陈旧（真值：**十二条**、**84**、**3**）；③ `save-format.md` §6 条款表只登记 005 的 ⑩，缺 006 的 ⑪/⑫；④ `specs/006-bribery-punishment/plan.md:68-70` 与 `research.md:445` 把家谱 walker 描述为 `Skip(1)` → `Skip(2)`，实现是 `MessagePackSaveFormat.cs:247` 保留 `Skip(1)` 后在 `:249` 追加 `EnsureJournalShape`（盲目 `Skip(2)` 会吞掉嵌套数组头）；⑤ `quickstart.md:209` 锚 `:254`（应为 `:309`）、`SaveStateValidator.cs:267`「覆盖七个位置」（实为**八个**）、`:316` 尾随空格 | **以实现为准修正活工件**（AGENTS.md 铁律第 1 条）。历史记录类工件（既有 `tasks.md` 条目、`implementation-notes.md`、既有 `checklists/`）**未回改** |

### 收敛任务

- [X] T082 [P] 收紧免死例外（R-1）：`src/KFL.Rules/Punishment/PunishmentImposer.cs` 的 `IsExemptFromExecution` 在「配偶有官职」之后**追加**「该成员是当事人的子女」判据（新增私有 `IsChildOf`，与 `RelativesOf` 同口径——经 `Family.ChildrenOf`，含已出嫁归档的女儿），并在断言物 `tests/KFL.Tests/Rules/CollectivePunishmentTests.cs` 新增「免死例外不扩展到未成年非子女亲属」（母亲的年龄经 `Kin.Build` 新增的 `motherAge` 形参置为 `AgeBracketPolicy.FemaleAdulthoodAge - 1`；真源 = 规格书 §17 ③）。验收：未成年**非子女**的五类亲属不再免死，既有四条「免死例外只认配偶有官职且孩子未成年」Theory 全绿。
- [X] T083 [P] 回收被丢弃的事实（R-2）：同文件 `Collective` 的有官职分支改为接住 `Ban(...)` 的返回值并加入 `relativeBans`（与 `:92`/`:131` 同形），使即时快照与日志投影同集；断言物在同文件的「连坐三分支各命中一条且后果与格一致」追加一条 `RelativeBans` 断言（有官职亲属 MUST 落 `ExamBanMonths = PromotionBanMonths = FamilyWideBanMonths`）。验收：该断言在修复前必然失败，修复后全绿。
- [X] T084 [P] 恢复窗口宽度的单一真源（R-4）：`src/KFL.Rules/Bribery/BribeRules.cs` 的 `IsWithinWindow` / `WindowStartOf` 改为由 `BribePolicy.WindowPreviousMonths` 驱动（新增私有 `SubtractMonths`，MUST NOT 内联字面量），类文档同步。验收：`WindowPreviousMonths = 1` 时行为与既有 12 格锚点/窗口断言逐字一致，且改该常量即改变窗口宽度。
- [X] T085 [P] 修正公开注释（R-5）：`src/KFL.Core/ValueObjects/PunishmentFacts.cs` 的 `RelativeBanFact` / `CollectivePunishmentFact` 两处 `<param>` 改为与 `PunishmentImposer.cs:22-26` 定稿口径同向（`Subject` = 触发惩罚的当事人；`Relative` = 被施加/被波及的成员，全族禁令时可为当事人本人）。验收：与 `data-model.md` §2.2、`contracts/punishment.md` 与断言物不再互相矛盾。
- [X] T086 修正五类活工件回写缺口（R-6）：`specs/004-save-system/contracts/save-format.md`（`:132` 十条→**十二条**；`:149` 断言锚点改为「`≠ 3` 一律拒绝：`4` 拒绝、`2` 拒绝」，与 §6 ④ 同口径；§6 条款表新增**第 8 条**登记 006 的 ⑪/⑫ 两条校验）；`specs/004-save-system/contracts/save-service.md`（`:61` `FormatVersion != 2` → `!= 3`；`:62` 十条→十二条）；`specs/004-save-system/plan.md`（`:231` 80 → **84**；`:283` 十条→十二条）；源码文档 `src/KFL.Infrastructure/Save/SaveService.cs:200`、`Serialization/ISaveFormat.cs:20`（九条→十二条）、`Serialization/SaveStateValidator.cs:267`（七个→**八个**）与 `:316`（尾随空格）；`specs/006-bribery-punishment/plan.md:68-70`、`research.md:445`（walker 描述改为 `:247` 保留 `Skip(1)` + `:249` 追加 `EnsureJournalShape`）、`quickstart.md:209`（`:254` → `:309`）。验收：`specs/` 与 `src/` 不再出现「十条/九条」一致性校验、「80 个线上成员」、「`FormatVersion != 2`」、或把家谱 walker 写成 `Skip(1)` → `Skip(2)` 的字样。
- [X] T087 登记 §17 待裁决项（R-3，**不擅改**）：在 `specs/006-bribery-punishment/spec.md` 的 Assumptions 新增开放项——「零资金池行贿的账本条目」：`contracts/bribery.md:37`/`:142`（恰好 1 条）与 `:145`（MUST NOT 记 0 金额条目）在「现银 + 储蓄 = 0」时互斥，MUST 先经规格书 §17 裁决；裁决前本特性 MUST NOT 自选一侧、MUST NOT 动 `PaymentPrimitive` 或契约措辞。依据：AGENTS.md 铁律（涉契约语义的裁决先在规格书 §17 回写，再改工件）；章程「需求真源」。验收：`spec.md` 与 `tasks.md` 各有一处登记，且 `src/` 与 `contracts/` 无任何改动。
- [X] T088 门禁复跑与收口（与 T082~T087 同一提交）：`dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false`（0 警告 0 错误）、`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false`（全绿、0 skipped）、`dotnet sln KejuFuShengLu.slnx list`（恰六工程）、递归无 `*.sln`；并把本批复写并入「回写范围登记」表第 **21** 行。依据：AGENTS.md 门禁四条；SC-010、SC-011。

---

## Phase 12: §17 裁决收敛（2026-10-17，行贿支付的资金来源）

**Purpose**: 消解 Phase 11 登记的 §17 开放项（R-3 / T087）。用户裁定：行贿是玩家**主动的可选支出**，
应付金额 MUST 由**现银 + 储蓄全额覆盖**，不足即**拒绝**，**MUST NOT 转贷款**。真源 = 规格书 §5.4
（贷款因**罚金 / 固定罚金**付不起而产生，**「不存在主动借贷」**）与 §5.4 买人口（玩家主动可选支出
⇒「付不起即无法购买」）。原实现沿用 §4.3 的**罚金**支付顺序（现金 → 储蓄 → 余额转贷款），属
**未经规格书授权的自造**——按 AGENTS.md 铁律「按真源修正代码/工件」，不属「先改代码再补文档」。
裁决已回写规格书 §7.1 的 `§17 裁决回写（2026-10-17）` 块。**罚金侧不动**（`contracts/punishment.md`、
`FineFact.ToLoan`、`PaymentPrimitive.Pay` 的三段语义一字不动）。

**收敛判据**：

| # | 严重度 | 类别 | 缺口（锚点） | 结论 |
| --- | --- | --- | --- | --- |
| J-1 | Medium | contradicts | 行贿支付沿用罚金口径「现金 → 储蓄 → **余额转贷款**」：`contracts/bribery.md:37`（原写「付不起**不阻止行贿**」）、`spec.md:275`、`plan.md:18`、`data-model.md:291`，与规格书 §5.4 的**贷款封闭枚举**矛盾；`BribeOutcome.ToLoan` 亦为自造字段 | **改代码 + 同批回写工件**：新增可付性判据与拒绝原因 `InsufficientFunds`，`BribeExpense` 由「可能 0 条」收敛为「恒恰好 1 条」——同时消解 R-3 的契约内部矛盾 |

### 收敛任务

- [X] T089 回写真源（J-1）：在 `科举浮生录规格书.md` §7.1 追加 `§17 裁决回写（2026-10-17，行贿的资金来源）` 块——行贿 MUST 全额可付、不足即拒绝、MUST NOT 转贷款；贷款唯一来源仍是罚金 / 固定罚金；原文按 §16.2 历史事实口径保留不改写。依据：AGENTS.md 铁律（裁决先落规格书）；规格书 §5.4、§9.5。
- [X] T090 修正源码（J-1）：`src/KFL.Rules/Economy/PaymentPrimitive.cs` 新增 `public static bool CanCover(FamilyEconomy economy, Money amount)`（`!(现银 + 储蓄 < amount)`）并在类文档说明行贿调用恒不走第三段；`src/KFL.Rules/Bribery/BribeOutcome.cs` 增 `BribeRejectReason.InsufficientFunds`、`BribeFeasibility.IsAffordable`，**删除** `BribeOutcome.ToLoan`；`src/KFL.Rules/Bribery/BribeRules.cs` 的 `CanBribe` 在判定次序**末尾**追加可付性（「窗口 → 在册 → 级别匹配 → 触顶 → 可付性」）；`src/KFL.Rules/Bribery/BribeEntry.cs` 删除转贷款自守并同步类文档。验收：`src/KFL.Rules/Bribery/` 不再出现「余额转贷款」字样；`Pay` 的三段语义与 `Fine` 路径逐位不变。
- [X] T091 补断言物（J-1）：`tests/KFL.Tests/Rules/BribeEntryTests.cs` 新增三例——差 1 文 ⇒ 拒绝（逐字段不变、随机 0、无账本条目、**贷款本金不变**）、恰好等于 ⇒ 可行且不转贷款、储蓄补足 ⇒ 可行且不转贷款；既有「窗口外」一例的 `ToLoan` 断言改述为 `Amount` 断言（`BribeOutcome` 已无该字段）。依据：`data-model.md` I-20；SC-008。
- [X] T092 回写工件（J-1）：`specs/006-bribery-punishment/spec.md`（Clarifications 新增 `Session 2026-10-17` Q4；US1 摘要与 FR-005 / FR-007 改述；FR-035 ① 扩为「四项裁决」；Assumptions 的 R-3 开放项改为「已由 §17 裁决消解」）；`contracts/bribery.md`（§1 条款 7 增「可付性」与判定次序、新增 §17 裁决注；§2 ① 增判据、③ 改「现金 → 储蓄全额付清」；§2 条款 1/5；§6 可行性行增 `IsAffordable`；§7 注「恒恰好 1 条」）；`data-model.md`（§3.2 字段面、§4.3、§5.1 转移表、新增不变量 I-20）；`plan.md`（US1 支付口径）。依据：AGENTS.md 铁律第 1/2/3 条。
- [X] T093 门禁复跑与收口（与 T089~T092 同一提交）：`dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false`（0 警告 0 错误）、`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false`（全绿、0 skipped）、`dotnet sln KejuFuShengLu.slnx list`（恰六工程）、递归无 `*.sln`；并把本批复写并入「回写范围登记」表第 **22** 行。依据：AGENTS.md 门禁四条；SC-010、SC-011。

---

## 回写范围登记（2026-10-19，玩家反馈第三轮：服刑事件条目）

> 本节由第三轮玩家反馈（反馈 10「人口统计图需入狱数据」）**追加登记**，不回改 T001~T093 既有条目，
> 不复用既有编号。授权落点 = 规格书 §17 裁决块（2026-10-19）Q8 与 `spec.md` 末尾同名增补。

| 变更 | 同步了哪些文件 |
| --- | --- |
| 服刑落 1 条事件类条目 `MemberImprisoned`（金额 `Money.Zero`，不涉资金，仅供人口统计的「入狱」系列） | `科举浮生录规格书.md` §17 裁决块 Q8 与 §12.3；`specs/006-bribery-punishment/spec.md` 增补 §1~§3；`specs/002-monthly-settlement-economy/data-model.md` §2.1（新行 + 计数 39 → 40）；`src/KFL.Core/Enums/{LedgerCategory,LedgerCategoryMetadata}.cs`、`src/KFL.Rules/Punishment/PunishmentImposer.cs`（`cell.SentenceMonths > 0` 分支内 `state.Economy.Apply(...)`）、`src/KFL.Presentation/Display.cs`（`"入狱服刑"`）；`tests/KFL.Tests/Core/LedgerInvariantTests.cs`（40 个取值、事件类 11 个）、`tests/KFL.Tests/Rules/PunishmentMatrixTests.cs`（改 `Fine` 定向断言，不再用全量计数） |

**门禁实测（2026-10-19）**：0 警告 0 错误 / 1309 通过 0 失败 / 恰六工程 / 无 `*.sln`。
本条**不改**惩罚矩阵的任何数值与结算次序（③ 步随机消费仍恒为 0 次），仅在已判服刑时多追加一条
不涉资金的账本事件条目；`contracts/punishment.md` 的 12 格矩阵与 I-1~I-20 不变量**未改**。
