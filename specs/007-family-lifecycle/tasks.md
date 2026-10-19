---

description: "Task list for 007-family-lifecycle（家族生命周期：婚配、生育、疾病、寿命、家主继任、买人口）"
---

# Tasks: 家族生命周期（婚配 · 生育 · 疾病 · 寿命 · 家主继任 · 买人口）

**Feature**: `007-family-lifecycle`（规格书 §16.1 **逻辑轨 ⑦**「婚姻与生育 / 疾病 / 寿命 / 家主继任 / 买人口」）

**Input**: Design documents from `/specs/007-family-lifecycle/`

**Prerequisites**: [plan.md](./plan.md)（技术栈、结构、勘误 E-01~E-06、回写义务清单 10 项、Complexity Tracking）、
[spec.md](./spec.md)（FR-001~FR-051、US1~US5、SC-001~SC-015、Assumptions 与 23 条 §17 默认口径）、
[data-model.md](./data-model.md)（层分布、新增类型、形状变更、状态转移、不变量 13 条、持久化形状与版本、需求→模型映射）、
[contracts/lifecycle.md](./contracts/lifecycle.md)（契约十八：十步与 ⑧ 的八个子步）、
[contracts/marriage-birth.md](./contracts/marriage-birth.md)（契约十九：婚配、生育、买人口）、
[contracts/illness-lifespan.md](./contracts/illness-lifespan.md)（契约二十：成长、疾病、疗程、饥馑、寿数、夭折）、
[contracts/config-registry.md](./contracts/config-registry.md)（契约二十一：数值登记表与字面量守卫）、
[quickstart.md](./quickstart.md)（9 个可执行验证场景）、
[research.md](./research.md)（R-01~R-25，**未决事项 = 无**）、
[.specify/memory/constitution.md](../../.specify/memory/constitution.md)（章程 v1.1.1）

**Tests**: 本特性**含测试任务**——spec FR-049 明令「单测 MUST 覆盖遗传公式、天命寿数到龄死亡、患病概率线性插值、
天赋乘数、`2 + 8s` / `0.05 + 0.45s` 映射、先扣后掷次序、宽限月、疗程短路、免病年龄与三段年龄增益、官员压力与榜下捉婿、
体质与饥馑、夭折、分娩死亡、婚配金额表、买人口递增计价、家主继任四档、外嫁归档、账本核对、存档 Version = 4 往返」，
章程原则 IV 要求「测试与实现**同一提交**给出（先行或至少同批），MUST NOT 留待后补」。各故事按
**对契约签名先写断言（红）→ 实现转绿** 的次序组织。

**Organization**: 任务按 user story 分组，使每个故事可独立实现、独立验收、独立交付。`[Story]` 只出现在用户故事阶段。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 可并行（不同文件、不依赖未完成任务）
- **[Story]**: 该任务所属用户故事（US1 = 婚配 / US2 = 生育与遗传 / US3 = 成长·疾病·寿命 / US4 = 家主继任 / US5 = 买人口）
- 每条任务都带**精确文件路径**（含行号锚点）与**逐字签名 / 数值**

## Path Conventions

单仓库六工程（`KejuFuShengLu.slnx`），分层：`src/KFL.Core`（状态与不变量）← `src/KFL.Infrastructure`（落盘形状）←
`src/KFL.Rules`（规则与编排）← `src/KFL.Presentation` / `src/KFL.App`（界面，**本特性仅 `KFL.App` 组合根一行级改动**）；
测试在 `tests/KFL.Tests/`。依赖方向与工程数 MUST NOT 变化（章程 II、G-02、G-05）。

**受限宿主命令口径**（`AGENTS.md` 坑 1 / 坑 2）：一切 `dotnet build` / `dotnet test` MUST 带
`-m:1 -nodeReuse:false`，测试另加 `-p:_MSTestEnableParentProcessQuery=false`；该属性在
`tests/KFL.Tests/KFL.Tests.csproj:21-25` MUST 以**注释态**提交。

**已登记的口径差异（E-05，供实现裁决）**：`plan.md` 的 Complexity Tracking 写「六个配置类 + 六个 `GameConfig` 转发视图」，
但同文件 Project Structure 与 `data-model.md` §3.7 均为 **5 个新配置类**（`GrowthPolicy` / `IllnessPolicy` /
`MarriagePolicy` / `ChildbirthPolicy` / `PopulationPurchasePolicy`）+ **6 个转发视图**。实现以 **5 个配置类**为准；
第 6 个视图 `GameConfig.Lifecycle` 若确无成员可转发，MUST 在 Phase 9 的「回写范围登记」中记录该差异，
且**任何情况下 MUST NOT 自带数值字面量**（SC-008 / `ConfigLiteralTests`）。
同一勘误（`plan.md` 的 E-05）另涵盖：新增存档 DTO 为 **4 个**（含 `CoupleKeyDto`，非「三个」）、
`quickstart.md` 为 **9 个场景**（0 前置 + 场景 1~9，非「8 个」）。另见 `plan.md` 的 **E-06**：饥馑的
**时间线**门面是 002 的 `GameConfig.Famine`（`:203-220`），而 ⑦ 的饥馑**健康影响**数值留在
`GrowthPolicy` 并经 `GameConfig.Growth` 暴露（`FamineHealth.Run` 属 ⑧-e 健康子步，职责不同，非重复门面）。

**回写前的行号位移提示**：`plan.md` 的「回写义务清单」表中的行号为**回写前原位**（`plan.md:114` 明写）；
本文件与 Phase 9 引用这些行号时一律保留原位口径。

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 建立门禁基线与新目录骨架，确认零新增依赖、六工程不变

- [X] T001 记录门禁基线：在仓库根跑 `dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false`、`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false`、`dotnet sln KejuFuShengLu.slnx list`（期望恰好六个工程）、`Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }`（期望**无输出**），并记录起始测试总数作为回归基线（后续 FR-010 的「既有断言 MUST 继续成立」以此为参照）
- [X] T002 [P] 建立本特性的唯一新目录 `src/KFL.Rules/Family/` 并确认工程形状零改动：`KejuFuShengLu.slnx`、`global.json`、`Directory.Build.props`、各 `*.csproj` 均 MUST NOT 改动；**零新增 NuGet 依赖**；`KFL.Presentation` 一行不动；`KFL.App` 仅允许在 T025 注入 `INameGenerator`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: US1~US5 共同依赖的状态载体（`KFL.Core`）、数值单点（`KFL.Rules/Config`）、
结算骨架与当月事实段（`SettlementResult.Lifecycle` 的**空壳**）、以及既有断言物的同步。
本阶段的 `PopulationAdvance` 只搭**八子步骨架 + 空事实段**，真实内容由各用户故事填入。

**⚠️ CRITICAL**: 本阶段完成前，任何用户故事都无法开始

### 内核状态载体（`KFL.Core`）

- [X] T003 [P] `src/KFL.Core/Enums/DeathCause.cs`（新）：`public enum DeathCause { OldAge = 0, Illness = 1, Famine = 2, Infancy = 3, Childbirth = 4 }`，逐项附 XML 注释标注触发子步（⑧-f / ⑧-c / ⑧-e / ⑧-f / ⑧-b 登记）；**只**出现在当月事实段 `LifecycleSnapshot.Deaths`，MUST NOT 落 `Person` 字段、MUST NOT 落账本条目（`LedgerEntry` 形状为 `(GameDate, PersonId?, LedgerCategory, Money)`，`src/KFL.Core/ValueObjects/LedgerEntry.cs:30`，无 `Note`）；**MUST NOT 为 006 的「处死」路径补取值**（`src/KFL.Rules/Punishment/` 只置 `StatusFlag.Deceased`，见 FR-033）
- [X] T004 [P] `src/KFL.Core/Enums/MarriageClass.cs`（新）：`Commoner = 0`（`Origin.Farming` / `Origin.Artisan`）/ `Scholar = 1`（`Origin.Commerce` / `Origin.Scholar`）/ `Official = 2`（`Family.HasShiStatus == true`，**优先于 `Origin` 判定**）；单一判据见 T038
- [X] T005 [P] `src/KFL.Core/Enums/MarriageProposalKind.cs`（新）：`Matchmaker = 0`（§9.1 随机提亲，⑧-a 第二段）/ `ImperialProposal = 1`（§9.2 榜下捉婿，⑧-a 第一段）
- [X] T006 [P] `src/KFL.Core/ValueObjects/CoupleKey.cs` + `src/KFL.Core/ValueObjects/PendingBirth.cs`（新）：`public readonly record struct CoupleKey(PersonId HusbandId, PersonId WifeId);`（**有序、夫在前**，不校验性别——性别语义属规则层）与 `public readonly record struct PendingBirth(CoupleKey Couple, GameDate AppliedAt);`
- [X] T007 [P] `src/KFL.Core/ValueObjects/MarriageProposal.cs`（新）：`public sealed record MarriageProposal`，**18 个 `required … { get; init; }` 成员**逐字照 `data-model.md` §2.2 —— `Kind`、`OurMemberId`、`CandidateName`、`CandidateGender`、`CandidateAge`、`TalentAgriculture`、`TalentCommerce`、`TalentScholar`、`TalentCraft`、`CandidateStudy`、`CandidateHealth`、`CandidateLifespan`、`CandidateDegree`、`Class`、`BetrothalGift`、`Dowry`、`ProposedAt`，另加 `public ImperialPlacement? CandidatePlacement { get; init; }`（**唯一非 required**）；**不是** `Person`、MUST NOT 进 `Family.Members`（`Components` 见 plan.md Complexity Tracking 第 2 行）；`CandidateLifespan` 只 clamp 下界、无上界
- [X] T008 `src/KFL.Core/Entities/FamilyLifecycleState.cs`（新）：`public sealed class FamilyLifecycleState` —— 只读暴露 `PurchaseCount` / `PurchaseYear` / `PendingBirths`（`IReadOnlyList<PendingBirth>`）/ `AppliedBirthCouples`（`IReadOnlyList<CoupleKey>`）/ `BirthApplicationYear` / `Proposal`（`MarriageProposal?`），写入通道 `int NextPurchaseOrdinal(int year)`（**返回 1 起的名次；跨年自动重置 `PurchaseCount` 并推进 `PurchaseYear`**；**带副作用，MUST 只在扣款成功后调用**）、`int PeekPurchaseOrdinal(int year)`（**纯读、零副作用**：`PurchaseYear == year ? PurchaseCount + 1 : 1`，供 T067 的报价与 T066 的第 ① 步使用）、`void ScheduleBirth(PendingBirth)`、`void ConsumeBirths()`、`bool HasAppliedForBirth(CoupleKey, int year)`、`void RecordBirthApplication(CoupleKey, int year)`、`void SetProposal(MarriageProposal?)`（`null` = 应答或失效后清空），以及 **`internal` 复原通道** `CaptureBookkeeping()` / `RestoreBookkeeping(...)`（`src/KFL.Core/AssemblyInfo.cs:13` 只放行 `KFL.Infrastructure`，与 `Family.CaptureOutsiderBookkeeping`（`src/KFL.Core/Entities/Family.cs:340`）同款）；两个 `Year` 字段**不可省**（跨年重置的唯一载体，MUST NOT 靠 `GameState.CurrentDate` 反推）——依赖 T006、T007
- [X] T009 [P] `src/KFL.Core/ValueObjects/StatusTimers.cs:21-65`：在**尾部**追加两项（**不得插在中间**，否则 004 的 `MessagePack` 位置式 `Skip` 序列错位）——`[4] int? IllnessSeverityBasisPoints`（疾病程度 `s` 的万分之整数，`0~10000`）、`[5] int? TreatmentRemainingMonths`（疗程剩余，`0~3`）；构造函数追加**两个可选尾参**（默认 `null`，既有 16 处 `new StatusTimers(...)` 不必改签名）；`EnsureNonNegative`（`:58-65`）继续只管四个「剩余月数」，新增 `EnsureSeverityInRange`（`0~10000`，抛 `ArgumentOutOfRangeException("疾病程度 MUST 在 0~10000 之间（规格书 §4.3）。")`）；**宽限月不在这里**——`IllnessSeverityBasisPoints == null` 与 `TreatmentRemainingMonths == null` 的组合即「未患病」，二者与 `StatusFlag.Ill` **同生同灭**
- [X] T010 [P] `src/KFL.Core/Entities/Person.cs:400` 附近（可写成员区，`Occupation` 邻近）：追加 `private bool _tutoredByMaster;` + `public bool IsTutoredByMaster { get => _tutoredByMaster; set => _tutoredByMaster = value; }`（纯布尔、无范围校验）+ `internal void RestoreTutoring(bool tutoredByMaster)`；**不新增**任何疾病 / 死亡 / 配偶候选字段（FR-029、FR-033）
- [X] T011 `src/KFL.Core/Entities/Family.cs:55` 附近（`AutoExamApplication` 邻近）：追加 `public FamilyLifecycleState Lifecycle { get; } = new();`；**MUST NOT** 为买人口 / 生育 / 提议新增任何写入方法（全部经 `Lifecycle`），**MUST NOT** 新增辈分写入通道（仍只用 `AddChild`（`:143`）/ `AddOutsider`（`:176`）/ `SetOutsiderGeneration`（`:199`）/ `BoughtCollateralGeneration`（`:89-92`），FR-035）；`SetHead`（`:308-328`）**签名不变**，⑧-h 只是新增一个调用者——依赖 T008
- [X] T012 [P] `src/KFL.Core/Enums/LedgerCategory.cs`：追加 **16** 个取值（在末值 `Fine = 88` 之后，按「支出 → 收入 → 事件」分组）——支出 8：`MatchmakerFee`、`TutorFee`、`BetrothalGiftPaid`、`DowryPaid`、`DegreeMarriageFeePaid`、`ChildbirthExpense`、`MedicineExpense`、`PersonPurchase`；收入 3：`BetrothalGiftReceived`、`DowryReceived`、`DegreeMarriageDowryReceived`；事件 5：`ChildBorn`、`MemberDeceased`、`MarriageFormed`、`MemberMarriedOut`、`PersonBought`；类文档的取值数与分布计数同步（22 → 38）
- [X] T013 [P] `src/KFL.Core/Enums/LedgerCategoryMetadata.cs`：两个 `switch` 各 +16 分支——支出 8 项与收入 3 项 `KindOf = LedgerEntryKind.Treasury`（收入 3 项另 `CreditTargetOf = LedgerTarget.Cash`，支出 8 项 `CreditTargetOf = null`），事件 5 项 `KindOf = LedgerEntryKind.Event` 且 `CreditTargetOf = null`；两处 `default:` 异常文案与 `<exception>` 文档的「共 22 个取值」同步改 **38**（`data-model.md` §2.7 的分布：`Treasury` 17 → 28、`Event` 5 → 10、`Cash` 9 → 12、`Savings` 1 不变、`null` 12 → 25）

### 数值单点（`KFL.Rules/Config`）

- [X] T014 [P] `src/KFL.Rules/Config/GrowthPolicy.cs`（新）：`StudyPerMonth = 1.5m`、`TutorStudyBonusPerMonth = 2.25m`、`TutorFeeGuanPerMonth = 5`、`ScholarStudyMultiplier = 1.10m`、`ForgetPerMonth = 0.5m`（正值，内部取负）、`ForgetAgeThreshold = 16`、`IllnessStudyPenaltyDivisor = 2m`、`HealthRecoverPerMonth = 1`、`FamineHealthLossPerMonth = 10`、`FamineDeathChance = 0.05m`；**口径**：唯一声明处、`AttributeLimits.Max`（100）MUST 复用不得另声明上限、成年边界经 `GameConfig.Ages` 读取（契约二十一 §1）
- [X] T015 [P] `src/KFL.Rules/Config/IllnessPolicy.cs`（新）：`InfectionChanceAtFullHealth = 0.0025m`、`InfectionChanceAtFloorHealth = 0.40m`、`InterpolationSpan = 99`、`HealthFloor = 1` / `HealthCeiling = 100`、四组 `m_p`/`m_q`（`TalentMultiplierFarmer` 0.80m/1.20m、`TalentMultiplierScholar` 0.95m/1.10m、`TalentMultiplierArtisan` 1.10m/1.10m、`TalentMultiplierMerchant` 1.05m/0.95m）、`SeverityLossBase = 2` / `SeverityLossSpan = 8`、`MedicineBaseGuan = 0.05m` / `MedicineSeverityGuan = 0.45m`、`BaseRecoveryCoefficient = 1.00m`、`TreatedRecoveryCoefficient = 1.10m`、`TreatedRecoveryOffset = 0.20m`、`TreatmentMonths = 3`、`ClearBonusChild = 0.60m` / `ClearBonusYouth = 0.30m`、`ClearBonusBoundaryChild = 3` / `Youth = 12` / `Adult = 24`、`IllnessExemptAge = 3`、`SeverityBasisPointsScale = 10000`，并给 `ToSeverity(int)` / `ToBasisPoints(double)` 两个换算单点；**MUST NOT 出现 `NegativeEventFactor` 符号**（Q6 / 契约二十一 §2 口径 2），`min(1, …)` 的钳位 MUST 在掷骰之前完成
- [X] T016 [P] `src/KFL.Rules/Config/MarriagePolicy.cs`（新）：`MatchmakerFeeGuan = 5`、`MatchmakerSuccessChance = 0.50m`、`CandidateAgeSpread = 5`、`ProposalBaseChance = 0.05m`、`ImperialProposalBaseChance = 0.30m`、`ImperialPaymentMinGuan = 500` / `MaxGuan = 1000`、`PressureWeightBottom = 0.02m` / `PressureWeightTop = 1.50m`、`PressureHighestLevel = 1` / `LowestLevel = 18`、`SpouseDegreeScholarThreshold = 1m` / `SpouseDegreeTributeThreshold = 3m`（**MUST NOT 进士档**）、`DegreeMarriageFeeRatio = 1.5m`、`TributeSpouseStudyMean = 60` / `Sigma = 10`、`JuRenSpouseStudyMean = 50` / `Sigma = 10`、`CommonerGiftGuan = 10` / `CommonerDowryGuan = 20`、`ScholarGiftGuan = 20` / `ScholarDowryGuan = 30`、`OfficialGiftGuan = 50` / `OfficialDowryGuan = 100`、`ClassWealthThresholdLow = 100` / `HighGuan = 1000`、三档权重 `ClassWeightsPoor = (0.70, 0.25, 0.05)` / `Middle = (0.45, 0.40, 0.15)` / `Rich = (0.20, 0.50, 0.30)`（顺序固定 平民 → 士人 → 仕族）；`ExpenseFactor` / `NegativeEventFactor` / `SalaryTable` / `TotalAssetsCalculator` 一律**复用**，MUST NOT 复制
- [X] T017 [P] `src/KFL.Rules/Config/ChildbirthPolicy.cs`（新）：`ChildbirthExpenseGuan = 10`、`FertilityAgeMin = 16` / `Max = 45`、`ChildbirthDeathChance = 0.05m`、`InheritanceSigmaBase = 5`、`StudyMeanHalving = 0.5m`、`InheritedHealthFloor = 20`、`InfantDeathChance = 0.10m`、`InfantCheckCount = 3`、`InfantExemptAge = 3`；分难度档一律经 `DifficultyRates` 与 `GameState.Difficulty` 读（简单 0.025 / 地狱 0.07；夭折简单 0.05 / 地狱 0.12）
- [X] T018 [P] `src/KFL.Rules/Config/PopulationPurchasePolicy.cs`（新）：`BasePriceGuan = 10`、`AgeMin = 8` / `AgeMax = 16`；`10n` 的计价 MUST 写成 `PopulationPurchasePolicy.BasePriceGuan * n`，**MUST NOT** 出现 `Money.FromGuan(10)` 这类字面量（契约二十一 §4.3 判据②）
- [X] T019 `src/KFL.Rules/Config/GameConfig.cs`：新增**六个**嵌套 `static class` 转发视图 `GameConfig.Growth` / `Illness` / `Marriage` / `Childbirth` / `Purchase` / `Lifecycle`（形如既有 `GameConfig.Exam`，`:29` 的门面风格），全部**纯转发、零数值字面量**（SC-008）；并修正 `:24-27` 的失真注释「明确不含：买人口递增计价、成长衰老疾病死亡的年龄线与概率」为「已由 ⑦ 交付，见 `Growth`/`Illness`/`Marriage`/`Childbirth`/`Purchase`」（勘误 E-03）——依赖 T014、T015、T016、T017、T018

### 结算骨架与当月事实段

- [X] T020 [P] `src/KFL.Rules/Family/LifecycleSnapshot.cs`（新）：`public sealed record LifecycleSnapshot`，**10 个 `required … { get; init; }`** —— `Births` / `Deaths` / `Marriages` / `MarriedOut` / `Purchases` / `Illnesses` / `Crossings`（均 `IReadOnlyList<…Fact>`）、`PendingProposal`（`MarriageProposal?`，**当月新产出**）、`NewHeadId`（`PersonId?`，未变动为 `null`）、`HeadBecameVacant`（`bool`）；另加 `static LifecycleSnapshot None(GameDate month)`；子记录（均 `public sealed record`、位置参数式，与 `LivingCostLine` 风格一致）：`BirthFact(PersonId, Gender, PersonId FatherId, PersonId MotherId)`、`DeathFact(PersonId, DeathCause)`、`MarriageFact(PersonId, PersonId SpouseId, MarriageClass, bool IsDegreeMarriage, Money BetrothalGift, Money Dowry)`、`MarriedOutFact(PersonId, MarriageClass)`、`PurchaseFact(PersonId, int Ordinal, Money Amount)`、`IllnessFact(PersonId, bool Contracted, bool Recovered)`、`AgeBracketCrossFact(PersonId, AgeBracket From, AgeBracket To)`；口径：**只描述当月**、增量、只读、集合按 `PersonId` 升序、MUST NOT 回写 `GameState`、MUST NOT 承载累计值（契约十八 §4）
- [X] T021 [P] `src/KFL.Rules/Family/DeathRegister.cs`（新）：`internal sealed class DeathRegister`，`public void Add(Person person, DeathCause cause)`、`public IReadOnlyList<(Person Person, DeathCause Cause)> Ordered { get; }`（`PersonId` 升序）、`public bool Contains(Person person)`；**条款**：登记**不改变** `Person.Status`（子步隔离的唯一手段）；同一人在多个子步被登记时**只保留首个**（优先级 ⑧-b 分娩 > ⑧-c 病亡 > ⑧-e 饥馑 > ⑧-f 寿终/夭折，后续登记 MUST 被忽略）
- [X] T022 [P] `src/KFL.Rules/Family/PopulationAdvance.cs`（新，**骨架**）：`public static class PopulationAdvance { public static PopulationAdvanceResult Run(GameState state, IRandomService random, INameGenerator names); }`，固定调用八个子步，**MUST NOT 增删或穿插**（FR-007）；本任务只搭次序与 `internal sealed record PopulationAdvanceResult` 的传递，八个执行器暂以 `LifecycleSnapshot.None(...)` 的空事实供给，**保持构建全绿**（真实内容由 T041 / T047 / T059 / T063 替换）——依赖 T020、T021
- [X] T023 `src/KFL.Rules/Settlement/SettlementResult.cs:48-120`：新增 `public required LifecycleSnapshot Lifecycle { get; init; }`（公开属性 **20 → 21**，尾部追加）；既有不变量（`Entries` 口径、`TreasuryPoolAfter − Before` = 资金类条目之和）不变。**不回改** `LivingCostLine` / `IncomeLine` 的既有位置参数式 `record`（勘误 E-04：新增子段一律 `required`，既有两行属历史形状）——依赖 T020
- [X] T024 `src/KFL.Rules/Settlement/MonthlySettlementEngine.cs`：插第 **⑧** 步并重编号——新序列 ①提升待生效值 ②米价游走 ③惩罚推进 ④官吏推进 ⑤收入 ⑥生活费 ⑦贷款 **⑧人口与健康推进** ⑨科举季 ⑩推进时间（**十步**）；同步 `:22-32` 的步骤清单、`:34-35` 的 MUST NOT 段、`:37-42` 的随机次序注释（逐字照契约十八 §3 的式子：米价(②) → 惩罚推进(③，0 次) → 考课(④-d) → 储蓄利率(⑤，仅 1 月) → 贷款计息(⑦) → **婚配事件(⑧-a) → 分娩(⑧-b) → 患病与病程(⑧-c) → 成长(⑧-d，0 次) → 饥馑死亡判定(⑧-e) → 寿终与夭折(⑧-f)** → 科举判定(⑨) → 及第入仕待阙(⑨)）；`Settle` 仍是**唯一**公开实例方法；**MUST NOT** 改变 ②~⑦ 与 ⑨ 的既有随机次数与次序（FR-010）——依赖 T022、T023
- [X] T025 `src/KFL.Rules/Settlement/MonthlySettlementEngine.cs:63` 的 ctor 追加 `INameGenerator` 形参；`src/KFL.Rules/Save/TurnRunner.cs:54` 的 ctor 同步 +1 参；`src/KFL.App` 组合根注入**同一个** `INameGenerator` 实例（R-02；替代方案「把取名推迟到下一次玩家动作」被否——会让 `SettlementResult.Lifecycle.Births` 的姓名在当月为空，与「事实段只存活于当月」矛盾）——依赖 T024

### 基础断言物同步

- [X] T026 [P] `tests/KFL.Tests/Core/PersonTests.cs:41-53`：`WritableMembers` 可写成员清单 **10 → 11**（加入 `IsTutoredByMaster`）；**只读成员全集仍 19 不变**（MUST NOT 改只读清单，与 006 的 T017 口径相反）
- [X] T027 [P] `tests/KFL.Tests/Core/StatusTests.cs`：`StatusTimers` 由 4 项扩为 6 项的建立/清除次序断言——补「`IllnessSeverityBasisPoints` / `TreatmentRemainingMonths` 与 `StatusFlag.Ill` 位**同生同灭**」、`0~10000` 越界抛 `ArgumentOutOfRangeException`、既有四个剩余月数的 `EnsureNonNegative` 行为逐位不变
- [X] T028 [P] `tests/KFL.Tests/Core/LedgerInvariantTests.cs`：四处清单同步（全名单 `:92-106` 22 → **38**、事件类枚举 `:123-141` 5 → **10**、无 `CreditTarget` `:154-171` 12 → **25**、恒为负 `:274-282` 7 → **15**）；`:108-114` 断言「不存在 Buy / Slave / Bride / Dowry / Betroth 类别」的**负向断言 MUST 被替换**为新增类别的**正向**断言（不得既新增类别又保留「不存在」断言，FR-043、SC-009）
- [X] T029 [P] `tests/KFL.Tests/Architecture/ConfigLiteralTests.cs`：`RegisteredValues` **49 → 去重后的实际新增条数**（契约二十一 §4.1 的**全集**：`GrowthPolicy` 1.5m / 2.25m / 1.10m / 0.5m / 2m；`IllnessPolicy` 0.0025m / 0.40m / 0.80m / 0.95m / 1.10m / 1.05m / 1.20m / 0.95m / 0.05m / 0.45m / 1.00m / 0.20m / 0.60m / 0.30m；`MarriagePolicy` 0.50m / 0.05m / 0.30m / 0.02m / 1.50m / 1.5m / 0.70m / 0.25m / 0.45m / 0.40m / 0.15m / 0.20m / 0.50m / 0.30m；`ChildbirthPolicy` 0.05m / 0.025m / 0.07m / 0.10m / 0.12m / 0.5m）。**条款**：与既有 49 条重叠的值（`0.5m` / `1.10m` / `1.5m` / `0.05m` 等）MUST NOT 出现两次；预计 **49 → 63 上下**，实现时以守卫变红为准确认，**MUST NOT 为了让测试变绿而漏登记**；整数规则值（`10000` / 各级 `Level` / 年龄线 / `3` / `16` / `20` / `45` / `100`）与 `ExpenseFactor` 四值、`NegativeEventFactor` 四值、`IncomeRateTable` 系数**一律不入单**（`ConfigLiteralTests.cs:102-103` 的既有纪律 + 契约二十一 §4.2）
- [X] T030 [P] `tests/KFL.Tests/Rules/SettlementEngineTests.cs`：`:305-307` 的注释与负向断言改为**十步**口径（「随机事件、属性成长与衰老、疾病与死亡、绝嗣判定等越界步骤 MUST NOT 存在」中的两条已由 ⑦ 接管，仅保留绝嗣等仍未交付项）；`:313` 仍断言 `["Settle"]`；`:315-337` 的 `expectedProperties` **20 → 21**（末尾加 `Lifecycle`）
- [X] T031 [P] `tests/KFL.Tests/Rules/DeterminismTests.cs:198-221`：把「1 月 2 次、其余 1 次、全年 13 次」这类**绝对值**断言改为**相对**断言（R-24）——「⑧ 之外各步的相对次数」+「本月总次数 − ⑧ 的消费数 == 旧绝对值」；复用既有计数替身 `CountingRandomService`（`DeterminismTests.cs:529-544`）为 ⑧-d / ⑧-g / ⑧-h 断言**零随机消费**
- [X] T032 [P] 建立本特性的**计数替身**用例基座：在 `tests/KFL.Tests/Rules/FamilyLifecycleTests.cs` 内为 ⑧ 的八个子步各提供「进入前 / 离开后」时间点快照的断言辅助（供 T041 / T047 / T059 / T063 与 T050 使用），并断言 `DeathRegister` 在 ⑧-g 之前**不改变**任何 `Person.Status`（不变量 11）

**Checkpoint**: 基础设施就绪——`dotnet build` 零警告零错误、既有测试（经 T026~T031 同步后）全绿，用户故事可以开始

---

## Phase 3: User Story 1 - 婚配：托媒、娶入、嫁出与提亲提议 (Priority: P1) 🎯 MVP

**Goal**: 交付**结算之内与之外**的完整婚配链：托媒（5 贯 × 支出系数、50%）→ 阶层加权抽对象 → 候选者初始化 → 聘礼/嫁妆双向流动 → `Family.Marry` 入册；反向**嫁出 + 外嫁归档**；**买功名婚姻**（年俸 × 150%，配偶低一级功名）；以及 ⑧-a 的**榜下捉婿**（先，`30% × 系数 ÷ (1 + P)`、对方支付 `Next(500,1001) × (1 + P)` 贯）与**随机提亲**（后，`5% × 系数`，命中不覆盖捉婿提议），两者共用同一**待应答提议**状态并**可被拒绝**。

**Independent Test**: 在只有 001~006 的基线上实现本故事的规则入口与查询面（⑧-a 可先不接结算）：对一名成年未婚男成员托媒至成功 → 断言现银按 `5 贯 × 支出系数` 减少、`Ledger` 恰新增 1 条 `MatchmakerFee`、对象属性落「无父母参照者」分布（天赋 `N(60,20)` / 学业 `N(30,15)` / 体质 `N(85,10)` 下限 20）、`SpouseId` 双向一致、聘礼/嫁妆按对方阶层与支出系数逐格吻合、辈分 = 家族内配偶辈分；再对一名女性成员执行嫁出 → 断言 `StatusFlag.MarriedOut` 置位、从 `RegisteredMembers` 与 `CountedMembers.Counted` 消失、`ArchivedMembers` 仍可 `TryGet`。**提亲/捉婿提议可单独以固定种子在 ⑧-a 上验收，不依赖 US2~US5**（分娩死亡走 `DeathRegister`，由 US3 的 ⑧-g 统一结算）。

### 测试（先写，红色）

- [X] T033 [P] [US1] `tests/KFL.Tests/Rules/MarriageTests.cs`（新）：**24 格金额表逐格** `Assert.Equal`（3 阶层 × 娶入/嫁出 × 支出系数 0.6 / 1.0 / 1.1 / 1.3，SC-007）；托媒扣费恰 1 条 `MatchmakerFee` / 失败**不退费**且 `Lifecycle` 无任何变化 / 资金不足 ⇒ `Accepted == false` + `InsufficientFunds` + **随机 0 次** + 不转贷款 + 不记 0 金额条目 / 同一成员同一月至多一次（`Proposal` 非空且未过期 ⇒ `NotMarriageable`）；提亲四难度概率 2.5 / 5 / 6.5 / 7% 逐点、**每月至多 1 次**、三档总资产下阶层抽样落 70/25/5、45/40/15、20/50/30 区间（SC-013）；捉婿 `w(18) = 0.02` / `w(1) = 1.50` 与线性中间值、`P = 0` 时概率恰 `30% × 系数`、金额恰落 500~1000 且**方向为对方支付给本家族**、**MUST NOT 叠加 §9.2 基础金额**；`P` 增大 ⇒ 概率（`30 ÷ (1 + P)`）**严格下降**、金额**严格上升**；功名分档在 `P = 0.99 / 1.00 / 2.99 / 3.00` 取 白身 / 举人 / 举人 / 贡士 且**任何情况下 MUST NOT 产出进士**；**拒绝提议 ⇒ 资金池、成员集合、婚姻关系与账本条目逐字段不变，且名额已消耗（MUST NOT 次月重掷）**；买功名婚姻的功名降级（进士 → 贡士 → 举人）与白身 / 举人的 `NoLowerDegree` 灰置（不扣费、0 随机）；提亲与捉婿同月都掷而提亲**不覆盖**捉婿（R-25）
- [X] T034 [P] [US1] `tests/KFL.Tests/Rules/LifecycleQueriesTests.cs`（新）：查询面**纯度**断言——`LifecycleQueries` 的每个方法调用前后 `GameState` 逐字段不变、`CountingRandomService` 消费数恰为 0、不建视图（R-20）；`ClassOf` 单一判据（`HasShiStatus` 优先于 `Origin`）；`PendingProposal` 对**过期**（`ProposedAt` 年月 ≠ 当月）提议返回 `null`；`ChildbirthFeasibility`（女方年龄窗口 / 已婚 / 本年是否已申请 / 金额可及）与 `IllnessStatus`（患病剩余月数与病程）的返回值逐项断言（FR-044 明文点名的两项交付面，MUST NOT 缺）

### 实现

- [X] T035 [P] [US1] `src/KFL.Rules/Family/OfficialPressure.cs`（新，**全纯函数**）：`public static decimal Compute(Family family)` = `Σ w(L)`，`L` 取 `CountedMembers.Registered(family)` 中 `Rank != null` 者；`internal static decimal WeightOf(int level)` = `0.02 + 1.48 × (18 − L) / 17`；**官员集合口径**：判定开始时的**在册且 `Rank != null`** 成员——已亡、外嫁归档、无官职者 MUST NOT 计入（Assumption 18）；服刑成员若 `Rank` 已被 006 剥夺则自然不计入，**MUST NOT 另立「是否服刑」判据**；`P` MUST NOT 落字段、MUST NOT 跨月缓存（Assumption 19）
- [X] T036 [P] [US1] `src/KFL.Rules/Family/LifecycleOutcome.cs`（新）：`public enum LifecycleRejectReason { None = 0, NotMarriageable, InsufficientFunds, NoLowerDegree, FertilityWindowClosed, AlreadyAppliedThisYear, HeadVacant, NoPendingProposal, NotHead, NoRegisteredMale }`（`None = 0` 与 006 的 `BribeRejectReason`（`src/KFL.Rules/Bribery/BribeOutcome.cs:12-33`）同风格；`InsufficientFunds` 复用同名但**不跨枚举复用值**）；`public sealed record LifecycleOutcome`：`required bool Accepted`、`required LifecycleRejectReason RejectReason`、`required Money Amount`、`required Money FromCash`、`required Money FromSavings`、`required LedgerEntry? Entry`（被拒时 `null`）、`public MarriageProposal? Proposal { get; init; }`
- [X] T037 [US1] `src/KFL.Rules/Family/LifecycleEntry.cs`（新）：`public static class LifecycleEntry`，本故事交付 `public static LifecycleOutcome TryMatchmake(GameState state, Person member, MarriageDirection direction, IRandomService random)`，**严格按契约十九 §3 的固定次序**（照 `BribeEntry.Bribe`（`src/KFL.Rules/Bribery/BribeEntry.cs:41`）的七段式）：①校验资格（纯，失败即返回 `Accepted = false` + 原因，**0 次随机**；资格 = 双方在册未婚 + `AgeBracketPolicy.IsAdult`（`src/KFL.Rules/Config/AgeBracketPolicy.cs:42-50`，男 12 / 女 14，**MUST NOT 另立成年判据**）+ 对象年龄差 ≤ `CandidateAgeSpread` + 血亲禁婚 + 家族内婚配**只**允许「买来的旁系 ↔ 家族内成员」）→ ②算托媒费 `MatchmakerFeeGuan × DifficultyRates.ExpenseFactor` → ③`PaymentPrimitive.CanCover` → ④`PaymentPrimitive.Pay(economy, LedgerCategory.MatchmakerFee, month, amount, member.Id)`（**现银 + 储蓄全额、恒不转贷款**）→ ⑤掷成功判定（**恰 1 次 `NextDouble`**，50%，失败 ⇒ 返回 `Accepted = false`、**不退费**）→ ⑥抽对方阶层（1 次 `NextDouble` 的分段映射，三档权重，区间内顺序固定 平民 → 士人 → 仕族）→ ⑦初始化候选者（**年龄 1 次 `Next` → 天赋 ×4 → 学业 → 体质 → 寿数 → 姓名**，次序照 `specs/003-opening-assets-officialdom/contracts/new-game-setup.md:59-79`；天赋 `N(60,20)`、学业 `N(30,15)`、体质 `N(85,10)` 下限 20、`Lifespan` 经 `AttributePolicy.NextLifespan` 只 clamp 下界；姓名经 `INameGenerator.NextGivenName(Gender)`（`src/KFL.Infrastructure/Abstractions/INameGenerator.cs:23/:28`），**冠家族姓氏**）→ ⑧婚嫁金额（§5 方向语义）→ `Family.Marry`（`src/KFL.Core/Entities/Family.cs:245`，**MUST NOT 由 007 直写 `SpouseId`**）→ 落资金类条目 + 1 条 `MarriageFormed` 事件条目 → ⑨方向为**嫁出**时归档（置 `StatusFlag.MarriedOut`、进 `ArchivedMembers`、落 `MemberMarriedOut`）；**买功名婚姻额外项**：娶入且 `HasShiStatus` 且成员 `Rank != null` ⇒ 额外支出 `DegreeMarriageFeePaid` = `SalaryTable.AnnualSalaryGuan(level) × DegreeMarriageFeeRatio`，配偶功名降一级并以 `DegreeRecord(cause: Initial)` 追加（`src/KFL.Core/ValueObjects/DegreeRecord.cs` 的四类原因之一）、学业按贡士 `N(60,10)` / 举人 `N(50,10)` 重掷；嫁出 ⇒ 额外收入 `DegreeMarriageDowryReceived` = 其 `AnnualSalaryGuan × 1.50`，**前置条件 MUST 同时满足 `Family.HasShiStatus == true`（仕族）与该外嫁成员 `Rank != null`（现任官员）**（FR-018 的两个条件，MUST NOT 只校验 `Rank`）——依赖 T016、T019、T035、T036
- [X] T038 [US1] `src/KFL.Rules/Family/LifecycleQueries.cs`（新）：`public static class LifecycleQueries` —— `IReadOnlyList<Person> MarriageCandidateMembers(Family family, GameDate date)`、`MarriageFeasibility MatchmakeFeasibility(GameState state, Person member, MarriageDirection direction)`（含金额预览与灰置原因）、`MarriageClass ClassOf(Family family, Person person)`（**单一判据**：`HasShiStatus` ⇒ `Official`；否则 `Origin.Commerce` / `Origin.Scholar` ⇒ `Scholar`；否则 `Commoner`）、`int GenerationOf(Person person)`、`ChildbirthFeasibility ChildbirthFeasibility(GameState state, CoupleKey couple, GameDate date)`、`IllnessStatus IllnessStatus(Person person)`（患病剩余月数与病程）、`MarriageProposal? PendingProposal(Family family, GameDate date)`（过期视为 `null`）；**全部 MUST NOT 写状态、MUST NOT 消费随机、MUST NOT 建视图**（FR-044）——依赖 T036、T037
- [X] T039 [US1] `src/KFL.Rules/Family/MarriageMarket.cs`（新）：`internal static MatchmakerResult Run(GameState state, IRandomService random, INameGenerator names)` —— ⑧-a 的**两段**（R-25/契约十八 §2.1）：**先捉婿**（判定对象 = **上月考季新科一甲 `ImperialPlacement.First` 且在册未婚**者；每名成员**每科只判定一次**、该子步至多产出 1 条提议；1 次概率 + 命中后 1 次 `Next(500, 1001)` 金额；`Kind = ImperialProposal`；配偶自带功名按 `P` 分档并以 `DegreeRecord(cause: Initial)` 追加；学业按同口径初始化；**MUST NOT 叠加 §9.2 的聘礼/嫁妆**）→ **后提亲**（**每月至多 1 次**，1 次概率；名额语义**不得随捉婿结果漂移**；命中后先抽阶层再在该阶层候选里**均匀抽 1**，**不再消耗额外随机**）；**捉婿命中后写 `Lifecycle.SetProposal`；随后提亲若也命中 MUST NOT 覆盖已有捉婿提议**（只掷骰、不改状态）；只写 `Lifecycle.Proposal`，**MUST NOT 改任何 `Person` 状态**——依赖 T035、T037、T038
- [X] T040 [US1] `src/KFL.Rules/Family/LifecycleEntry.cs` 追加 `public static LifecycleOutcome RespondToProposal(GameState state, bool accept, IRandomService random, INameGenerator names)`：`accept = false` ⇒ 清空提议，**0 次随机、0 金额、0 状态变更**；`accept = true` ⇒ 按提议快照的属性把候选者落成一个 `Person`（经 `Family.Marry`）：**娶入**只记 `BetrothalGiftPaid`（支出对方阶层聘礼）+ `DowryReceived`（收入对方阶层嫁妆）；**捉婿**只记 `DowryReceived`（对方支付的捉婿金额）；买功名婚姻另加 `DegreeMarriageFeePaid`——依赖 T037、T039
- [X] T041 [US1] 接线 ⑧-a：在 `src/KFL.Rules/Family/PopulationAdvance.cs` 的 ⑧-a 位调用 `MarriageMarket.Run`，把 `Lifecycle.Proposal` 与当月新产出提议写入 `PopulationAdvanceResult`；情形「捉婿/提亲未命中」MUST 仍按次序消费该消费的随机——依赖 T022、T039

**Checkpoint**: US1 可独立验收（quickstart 场景 1 的 US1 部分、场景 6）；MVP 边界 = Phase 1 + Phase 2 + 本阶段

---

## Phase 4: User Story 2 - 生育与遗传：次月分娩 (Priority: P2)

**Goal**: 交付生育决策（10 贯 × 支出系数、女方 16~45、**每年每对夫妇至多一次**、无怀孕率）→ **次月** ⑧-b 分娩
（母亲 5% 分娩死亡判定、新生儿性别 50/50、姓随家族、名经 `INameGenerator`、遗传公式 `N(父母均值, σ)`、
`σ = |父 − 母| / 2 + 5`、学业 `N(均值 × 0.5, σ)` 共用同一 σ、clamp 0~100、**体质下限 20**、辈分 = 父母辈分 + 1、
`Lifespan` 照常 roll），以及**出生当月 + 满 1 岁 + 满 2 岁共 3 次、每次 10%**（简单 5% / 地狱 12%）的夭折判定登记。

**Why this priority**: 生育是「家族不死」的正反馈来源；**遗传公式是规格书 §16 点名的必测项**（全仓当前**无任何遗传测试**）。

**Independent Test**: 用固定种子断言——申请当月扣 10 贯 × 支出系数并落 `ChildbirthExpense`、**当月不产生**新生儿；
次月诞生 1 名成员、`FatherId`/`MotherId` 双向、`Generation` = 父辈分 + 1、四项天赋各自落在
`N((父+母)/2, |父−母|/2+5)` 的 clamp 区间、体质不低于 20、学业按 `N(父母均值 × 0.5, σ)` 且与天赋取自**同一 σ**、
姓名取自 `INameGenerator`、`AttributePolicy.NextNormal` 每次调用**恰消费 2 次** `NextDouble`。
**只依赖 ⑧-b**，不依赖 ⑧-a / ⑧-c~⑧-h（分娩死亡只向 `DeathRegister` 登记，归档由 US3 的 ⑧-g 完成）。

### 测试（先写，红色）

- [X] T042 [P] [US2] `tests/KFL.Tests/Rules/InheritanceTests.cs`（新）：SC-003 —— 对至少 **20 组**父母值断言 σ 恰为 `|父−母|/2+5`；clamp 上下界 0/100 生效；**体质不低于 20**；学业均值恰为父母均值的 0.5 倍且**共用同一 σ**；四项天赋独立取样；**任一方缺失**（含单亲）⇒ 取无父母参照者分布（天赋 `N(60,20)` / 学业 `N(30,15)` / 体质 `N(85,10)`），**MUST NOT 用单亲值外推**；`AttributePolicy.NextNormal`（`src/KFL.Rules/Config/AttributePolicy.cs:66-82`）**恰 2 次 `NextDouble`**
- [X] T043 [P] [US2] `tests/KFL.Tests/Rules/ChildbirthTests.cs`（新）：生育资格（女方 15 岁或 46 岁、配偶已亡 `SpouseId == null` ⇒ 不可用且**不消费随机**）；本年度已申请 ⇒ `AlreadyAppliedThisYear` + 0 随机 + 状态与现金不变；**跨年（次年 1 月后）可再次申请**；申请当月不产子；次月分娩的⑦步次序与性别 `Next(0,2)`；**分娩死亡命中 ⇒ 母亲置 `Deceased`、进 `ArchivedMembers`、死因「分娩」，且新生儿照常出生**、其配偶 `SpouseId` 置空；夭折三档（10% / 简单 5% / 地狱 12%）在 `AgeAt < 3` 的**生日月**判定、共 3 次、满 3 岁后**零随机消费**、累计 ≈ 27.1%（`1 − 0.9³`）

### 实现

- [X] T044 [P] [US2] `src/KFL.Rules/Family/Inheritance.cs`（新）：`internal static int Attribute(int father, int mother, IRandomService random)` 与 `internal static int Study(int f, int m, IRandomService random)`；公式逐字照契约二十 §6：`mean = (father + mother) / 2`、`sigma = |father − mother| / 2 + ChildbirthPolicy.InheritanceSigmaBase`、`value = clamp(round(NextNormal(mean, sigma)), 0, 100)`、体质另加 `max(value, InheritedHealthFloor)`、学业 `clamp(round(NextNormal(mean × StudyMeanHalving, sigma)), 0, 100)`（**同一 sigma**）；取整 `MidpointRounding.AwayFromZero`；**复用** `AttributePolicy.NextNormal`，MUST NOT 自写 Box–Muller
- [X] T045 [US2] `src/KFL.Rules/Family/Childbirth.cs`（新）：`internal static IReadOnlyList<BirthFact> Run(GameState state, IRandomService random, INameGenerator names, DeathRegister deaths)` —— 消费 `Lifecycle.PendingBirths`（按 `CoupleKey.WifeId`（**母亲** `PersonId`）升序，FR-008 的夫妇遍历口径；实现时 MUST 同步 `contracts/marriage-birth.md` §7 与 `contracts/lifecycle.md` §2 的排序键措辞），**固定次序**：①掷分娩死亡（**1 次 `NextDouble`**，`ChildbirthPolicy.ChildbirthDeathChance` 分难度；命中 ⇒ `deaths.Add(母亲, DeathCause.Childbirth)`，**新生儿照常出生**）→ ②新生儿性别（`Next(0, 2)`，50/50）→ ③遗传（天赋 ×4 → 学业 → 体质，经 T044）→ ④寿数（`NextLifespan`，只 clamp 下界）→ ⑤姓名（`NextGivenName(gender)`，**冠家族姓氏**）→ ⑥`Family.AddChild`（`src/KFL.Core/Entities/Family.cs:143`，辈分 = 父母辈分 + 1；**MUST NOT 直写父/母引用与辈分**）→ ⑦落 1 条 `ChildBorn` **事件类**条目（金额 0，不入资金类）→ ⑧母亲死亡时 MUST 触发其配偶 `SpouseId` 置空；处理完毕后 `Lifecycle.ConsumeBirths()` **清空**；**夭折判定 MUST NOT 在 ⑧-b 内做**（属 ⑧-f 的不同子步）；新生儿 MUST NOT 被额外设年龄条件排除在家主候选之外（FR-036 不设年龄下限，Assumptions 25）——依赖 T044
- [X] T046 [US2] `src/KFL.Rules/Family/LifecycleEntry.cs` 追加 `public static LifecycleOutcome ApplyChildbirth(GameState state, CoupleKey couple)`：资格（双方在册、`SpouseId` 双向一致、`FertilityAgeMin <= AgeAt(date) <= FertilityAgeMax`）、频率（`Lifecycle.HasAppliedForBirth(couple, year)`）、费用（`ChildbirthExpenseGuan × ExpenseFactor`，现银 + 储蓄全额，不足 ⇒ `InsufficientFunds` + **0 随机** + 不转贷款 + 不记 0 金额条目）；拒绝原因 `FertilityWindowClosed` / `AlreadyAppliedThisYear` / `InsufficientFunds` / `NotMarriageable`；成功 ⇒ `RecordBirthApplication(couple, year)` + `ScheduleBirth(new PendingBirth(couple, state.CurrentDate))` + 落 1 条 `ChildbirthExpense`；**申请当月不分娩**——依赖 T016、T017、T037
- [X] T047 [US2] 接线 ⑧-b：在 `src/KFL.Rules/Family/PopulationAdvance.cs` 的 ⑧-b 位调用 `Childbirth.Run`，把 `BirthFact` 与死因「分娩」的登记并入 `PopulationAdvanceResult`——依赖 T022、T045、T046

**Checkpoint**: US1 与 US2 均可独立验收（quickstart 场景 3；场景 1 的 ⑧-a/⑧-b 部分）

---

## Phase 5: User Story 3 - 成长、疾病与天命寿数 (Priority: P3)

**Goal**: 交付 ⑧-c~⑧-g 五个子步与疗程入口：学业成长（读书 +1.5 / 延名师 +2.25 且**先扣 5 贯** / 士出身 ×1.10 / `AgeAt > 16` 未读书 −0.5 / **16 岁以下不遗忘** / 患病期**减半**）；体质四向（+1 / 患病 `−(2 + 8s)` / 饥馑 −10 / 救济期不减）；
患病概率**按体质线性插值 × 主导天赋 `m_p`**、**`AgeAt < 3` 免病且零随机**、清除概率 `min(1, 体质 × 1.00/100 × m_q + b)` 与三段年龄增益 `+0.60 / +0.30 / 0`、**宽限月**由子步次序隐式表达、疗程 `min(1, (体质 × 1.10/100 + 0.20) × m_q + b)` 恰 3 月且 `≥ 1` 短路；
饥馑严重期 −10 与 5% 死亡判定；**天命寿数到龄当月死亡**（服刑 / 患病 / 外嫁均不豁免）；⑧-g 的统一归档与死因。

**Independent Test**: 只实现 ⑧-c~⑧-g（不实现婚育与买人口），用固定种子构造：一名体质 100 的患病成员在**宽限月结束后的第一个月即愈**（且确诊当月 0 扣减、0 药费、0 清除随机）；一名体质 20、`s ≈ 1` 的患病成员在体质归零时病亡；一名 `Lifespan = 45` 的 45 岁成员在生日当月死亡且服刑/患病/外嫁三态皆成立；饥馑严重期每月 −10 且 5% 判定可复现；所有死亡都置 `Deceased`、离开 `RegisteredMembers`、进 `ArchivedMembers`、落 `MemberDeceased`。SC-015 的**存活阈值金色表**（容差 ±0.5）单独可断言。

### 测试（先写，红色）

- [X] T048 [P] [US3] `tests/KFL.Tests/Rules/IllnessTests.cs`（新）：患病概率**体质线性插值**逐点（`100 ⇒ 0.25%`、`1 ⇒ 40%`、中间值按 `0.25 + 39.75 × (100 − c) / 99`）与**四组 `m_p` / `m_q`** 逐点；**无年龄档、无 20% 封顶**；**不乘难度负面事件系数**（同一 seed 下四难度逐字段**完全相同**，SC-014）；`AgeAt < 3` 不掷判定且该成员当月消费**恰 0 次**（同一存档同一种子下把一名 2 岁成员加入家族 MUST NOT 改变当月任何其他随机结果），`AgeAt == 3` 必须参与判定；`s` → 扣减/药费映射（`2 + 8s` / `0.05 + 0.45s`）且**一次 roll 后不再变动**；**「先扣体质、再掷清除」**次序；基础清除 `体质 × 1.00 / 100`（无乘数且 `b = 0` 时 100 ⇒ **必愈且 0 随机**、10 ⇒ 10%）逐点；**宽限月**（确诊当月 0 扣减 / 0 `MedicineExpense` / 0 清除随机）；疗程 `min(1, (体质 × 1.10/100 + 0.20) × m_q + b)` 与 `≥ 1` **短路不消费随机**；疗程恰 3 个月、**未治疗的疾病 MUST NOT 自行过期**、药费**仅在疗程月产生**；年龄增益边界逐点：`2 岁 ⇒ 免病` / `3 岁 ⇒ +0.60` / `11 岁 ⇒ +0.60` / `12 岁 ⇒ +0.30` / `23 岁 ⇒ +0.30` / `24 岁 ⇒ +0`
- [X] T049 [P] [US3] `tests/KFL.Tests/Rules/LifespanTests.cs`（新）：SC-004 到龄当月死亡（`AgeAt(date) >= Lifespan`）在**服刑 / 患病 / 外嫁**三态下概率均为 1；外嫁者死亡 **MUST NOT** 使其回到在册 / 计口集合；夭折只在 `AgeAt < 3` 的生日月判定（`BirthDate.Month == date.Month` 派生判据，**零新增字段**），满 3 岁后**零随机消费**；**SC-015 的存活阈值金色表**（容差 ±0.5）：`AgeAt ≥ 24`（`b = 0`）不治疗 **71.1** / 开疗程 **63.4**；`12 ≤ AgeAt < 24`（+0.30）**62.3** / **59.1**；`3 ≤ AgeAt < 12`（+0.60）**59.1** / **59.1**（疗程同值，因 `min(1, …)` 在 `体质 ≥ 40`（不治疗）/ `≥ 18.2`（疗程）段即为 1）；四天赋端点（不治疗 / 疗程）：`≥ 24` ⇒ 农 62.0/52.3、仕 68.0/59.5、工 71.1/63.4、商 73.2/65.9；`12~24` ⇒ 农 52.4/48.7、仕 59.1/56.9、工 63.1/62.9、商 64.5/61.1；`3~12` ⇒ 农 48.7、仕 56.9、工 62.9、商 61.1（疗程档同值）；**落险比例**（新成员体质 `N(85,10)` 低于阈值）：`≥ 24` 岁无天赋 **8.2%**、农 1.1%、仕 4.4%、工 8.2%、商 11.8%；并断言 `体质 > c*` 时月净变化恒为正、年龄增益把 3~12 岁段清除概率**钳到 1**、**宽限月 MUST NOT 进入阈值公式**（每段病程期望伤害 `6 / q` 与之对账抵消）
- [X] T050 [P] [US3] `tests/KFL.Tests/Rules/FamilyLifecycleTests.cs`（新）：SC-001 十步次序且 ⑧ 严格在 ⑨ **之前**（⑧ 内死亡的成员在 ⑨ 的考生集合里**不存在**）；⑧ 八个子步各执行一次、**子步隔离**（⑧-g 之前死亡登记表不改任何 `Person.Status`，不变量 11）；⑧-d / ⑧-g / ⑧-h **零随机消费**（`CountingRandomService`）；体质 +1 / 患病 2~10 / 饥馑 −10 / **救济期不减（含患病减免）** / 归零死亡；**饥馑严重期每人每月 5% 死亡判定**（四难度同种子可复现；非严重期该判定**零随机消费**）；患病与饥馑**各扣一次不得互替**；学业 `AgeAt == 16` **不遗忘**、`AgeAt == 17` 起 −0.5、儿童不遗忘、名师费付不起 ⇒ 按普通速度且 `IsTutoredByMaster` **保持**、不转贷款；死亡归档（`Deceased` + `ArchivedMembers` + `MemberDeceased`）与外嫁归档口径（不计口、不可指派、不可科举、数据保留供 006 追及）

### 实现

- [X] T051 [US3] `src/KFL.Rules/Family/IllnessAdvance.cs`（新）：`internal static IReadOnlyList<IllnessFact> Run(GameState state, IRandomService random, DeathRegister deaths)` —— **内部两段次序是本契约最容易被写反的一条**：**第一段**对「进入本子步前 `Status` 已含 `Ill`」者按 `PersonId` 升序：①在疗程中（`TreatmentRemainingMonths > 0`）付药费（`CanCover` → `Pay(MedicineExpense)`；付不起 ⇒ 疗程中止置 `null`，本月按未治疗口径）②**先**扣体质 `−(2 + 8s)` ③体质归零 ⇒ `deaths.Add(…, DeathCause.Illness)` 并结束该成员 ④**再**掷清除（见 T052）⑤疗程月数递减；**第二段**对健康成员按 `PersonId` 升序掷感染（`AgeAt < 3` 者**不掷、不消费随机**；`c = clamp(当月初 Health, 1, 100)`；命中 ⇒ `Status |= Ill` + **紧接 1 次 `NextDouble` 定 `s`**（`IllnessSeverityBasisPoints = round(s × 10000)`、`TreatmentRemainingMonths = null`），**不扣体质、不掷清除**）；**宽限月由子步次序隐式表达，MUST NOT 新增字段**；遍历集合 = 在册成员（**含服刑 / 待阙**，**不含**外嫁与已亡）——依赖 T015、T021、T037
- [X] T052 [US3] `src/KFL.Rules/Family/IllnessAdvance.cs` 的清除概率与疗程：`b = 3 ≤ AgeAt < 12 ? ClearBonusChild : 12 ≤ AgeAt < 24 ? ClearBonusYouth : 0`（**加算**在「基础 × 天赋乘数」之上，疗程公式同样叠加）；`q = 未治疗 ? min(1, Health × BaseRecoveryCoefficient / 100 × m_q + b) : 疗程中 ? min(1, (Health × TreatedRecoveryCoefficient / 100 + TreatedRecoveryOffset) × m_q + b)`；`Health` 取**扣减之后**的值；**`min(1, …)` 的钳位 MUST 在掷骰之前完成，钳到 1 ⇒ 必愈且 MUST NOT 消费随机**；清除命中后该月 MUST NOT 再有后续随机消费（Assumption 15）；`m_q` / 主导天赋口径由 T053 单点提供——依赖 T051
- [X] T053 [US3] `src/KFL.Rules/Family/IllnessAdvance.cs` 内的**主导天赋单点**：`(m_p, m_q)` 由该成员**四项 `Talents` 最高的一项**决定（并列按 **农 → 商 → 仕 → 工** 固定顺序取先者，保证确定性），**MUST NOT 四项连乘**、MUST NOT 二次封顶（FR-029②、Assumption 3c，已由 Q6 裁决确认）；该单点 MUST 同时供 ⑧-c 与疗程使用，MUST NOT 复制到 `LifecycleQueries`——依赖 T015、T051
- [X] T054 [US3] `src/KFL.Rules/Family/GrowthAdvance.cs`（新）：`internal static void Run(GameState state, GameDate date)`，**零随机消费**；读书 `Occupation == Occupation.Studying` ⇒ +`StudyPerMonth`；延名师 `IsTutoredByMaster` ⇒ **先**扣 `TutorFeeGuanPerMonth × ExpenseFactor`（`CanCover` → 付得起则 `Pay(TutorFee)` 并给 `TutorStudyBonusPerMonth` 加成；付不起 ⇒ 按普通速度、标志**保持**、**MUST NOT 转贷款**，Assumption 9）再加成；士出身（`Origin == Scholar`）乘 `ScholarStudyMultiplier`；`AgeAt > 16` 且 `Occupation != Studying` ⇒ −`ForgetPerMonth`（`AgeAt == 16` 不遗忘、`17` 起遗忘）；患病期**当月全部学业变化减半**（`IllnessStudyPenaltyDivisor`）；**取整口径**（Assumption 13，封印）：全部加成先用 `decimal` **累加**（含减半），最后一次 `MidpointRounding.AwayFromZero` 取整，再 `Math.Clamp(0, 100)` 后写 `Person.Study`（**setter 是抛异常守卫，不是静默 clamp**）；体质：当月**生活费足额付清**、非饥馑、**非患病** ⇒ `+HealthRecoverPerMonth`（上限 100），写入前 MUST 自行 clamp 到 `0`——依赖 T014、T019
- [X] T055 [US3] `src/KFL.Rules/Family/FamineHealth.cs`（新）：`internal static void Run(GameState state, IRandomService random, DeathRegister deaths)` —— 只**读** `GameEconomy.Famine.Stage`（002 已交付的状态机），`FamineStage.Severe` ⇒ 计口（`CountedMembers.Counted`）每人 −`FamineHealthLossPerMonth` 体质，随后每人 **1 次 `NextDouble`** 与 `FamineDeathChance` 比较，命中 ⇒ `deaths.Add(…, DeathCause.Famine)`；体质因扣减归零 ⇒ 同样登记 `Famine`；`Relief` / 12 个月救济期 ⇒ 体质**不减**、**不掷**死亡判定；**MUST NOT 改写 002 的阶段迁移、时限与「阶段与剩余月数可读」的契约**（FR-031）——依赖 T014、T021、T054
- [X] T056 [US3] `src/KFL.Rules/Family/LifespanAdvance.cs`（新）：`internal static void Run(GameState state, GameDate date, IRandomService random, DeathRegister deaths)` —— 遍历集合 = **在册成员 ∪ 已外嫁归档成员**（即 `RegisteredMembers` ∪ {`ArchivedMembers` 中 `Status` 含 `MarriedOut` 者}；`ArchivedMembers` = 已亡 ∪ 外嫁，**已亡成员 MUST 被显式排除**，否则会被逐月重复判定）（**外嫁只承载本判定**，其余月度判定 MUST NOT 施加）；`AgeAt(date) >= Lifespan` ⇒ `deaths.Add(…, DeathCause.OldAge)`（**服刑 / 患病 / 外嫁均不豁免**）；**夭折**：判据 `AgeAt(date) < 3 && BirthDate.Month == date.Month` ⇒ 每月 **1 次 `NextDouble`** 与 `InfantDeathChance`（简单 5% / 地狱 12%）比较，命中 ⇒ `deaths.Add(…, DeathCause.Infancy)`；`AgeAt >= 3` 后**不再判定、不消费随机**；`Lifespan` MUST NOT 重掷、MUST NOT 设上限（只 clamp 下界）——依赖 T017、T021
- [X] T057 [US3] `src/KFL.Rules/Family/DeathSettlement.cs`（新）：`internal static IReadOnlyList<DeathFact> Run(GameState state, DeathRegister deaths, GameDate date)`，**零随机**——按 `PersonId` 升序对登记表**去重后**的每个人 `Status |= Deceased`、进 `ArchivedMembers`、落 1 条 `MemberDeceased` **事件类**条目；**幂等守卫**：遍历时 MUST 跳过 `Status` 已含 `Deceased` 者（否则已归档成员从次月起会被重复判定并重复落条目，污染「累计死亡」事实）；死亡时间与死因 MUST NOT 落 `Person` 字段；MUST NOT 改动 006 的处死路径行为；MUST NOT 在任何路径判定绝嗣（§15 属 ⑧）——依赖 T003、T021
- [X] T058 [US3] `src/KFL.Rules/Family/LifecycleEntry.cs` 追加 `public static LifecycleOutcome StartTreatment(GameState state, Person patient)`：仅对 `Ill` 置位者可用，置 `TreatmentRemainingMonths = TreatmentMonths`（3）；**宽限月内 MUST NOT 支付药费**（疗程自患病**次月**起算）；疗程结束后若仍未愈可**再次发起**；拒绝原因沿用 T036 的 `LifecycleRejectReason`（非 `Ill` 置位者 ⇒ `Accepted = false` + **0 随机 0 支付**；**MUST NOT 自创新枚举值**，确需新增 MUST 先登记到 T036 与 `data-model.md` §2.5）；抉择 MUST 使 U1 的**快进在患病成员当月暂停**：暂停信号来自**上一月**结算产出的 `LifecycleSnapshot.Illnesses`（`Contracted = true`），暂停点落在该月结算结束之后、下一月结算开始之前（FR-030；**MUST NOT 自建 `IEventBus`**，也 MUST NOT 依赖「当月结算尚未发生」的前视信息）——依赖 T036、T037、T051
- [X] T059 [US3] 接线 ⑧-c~⑧-g：在 `src/KFL.Rules/Family/PopulationAdvance.cs` 依次调用 `IllnessAdvance.Run`（⑧-c）→ `GrowthAdvance.Run`（⑧-d）→ `FamineHealth.Run`（⑧-e）→ `LifespanAdvance.Run`（⑧-f）→ `DeathSettlement.Run`（⑧-g），把 `IllnessFact` / `DeathFact` / `AgeBracketCrossFact`（生日当月跨档：成年 / 青年 / 老人）并入 `PopulationAdvanceResult`；**⑧-g 是当月唯一置 `Deceased` 的地方**——依赖 T022、T051~T057
- [X] T060 [P] [US3] 同步既有断言物：`tests/KFL.Tests/Rules/SettlementEngineTests.cs` 与 `FamilyLifecycleTests.cs` 的 `expectedProperties` 与随机次数断言对齐；`tests/KFL.Tests/Rules/DeterminismTests.cs` 的「连续 120 月逐字段 diff = 0」（SC-002，含成员集合、四项天赋、学业、体质、辈分、`SpouseId`、`HeadId`、现金/储蓄、账本与患病计时）扩展为含 ⑧；存档往返（`MessagePackSaveFormat` → `SaveStateMapper.ToDomain`）后继续 12 个月仍一致

**Checkpoint**: US1~US3 全部可独立验收（quickstart 场景 2、4、5；场景 1 完整）

---

## Phase 6: User Story 4 - 家主继任 (Priority: P4)

**Goal**: 交付 ⑧-h —— 家主亡故或从在册集合消失的**同月**、在 ⑧-g 之后**一次**按 §4.4 直系优先的四档产生新家主；
无在册男性 ⇒ `HeadId = null`（**家主空缺，不构成绝嗣**）；结算结束时 `HeadId` **永不**悬空。

**Independent Test**: 用 `FamilyFixtures.MultiGeneration()`（三代家庭）逐一构造四档候选场景断言选择与 §4.4 完全一致；
清空全部在册男性 ⇒ `HeadId == null` 且 `HeadBecameVacant == true` 且**不**触发绝嗣；任意月份结算结束后
`tests/KFL.Tests/Infrastructure/SaveStateValidatorTests.cs:68-73` 的存档校验全绿。

### 测试（先写，红色）

- [X] T061 [P] [US4] `tests/KFL.Tests/Rules/HeadSuccessionTests.cs`（新）：SC-005 —— 四档各至少 1 条断言（①**在册男性子女**中年龄最大 ②已亡儿子的**在册男性后代**（代际就近优先、同代 `AgeAt` 最大）③**亲兄弟** ④在册男性中 `AgeAt` 最大者、同岁取**子代数更多**者、再同按 `PersonId` 升序）；候选含**服刑 / 待阙**、排除已亡与外嫁（「家族只剩一名服刑男性 + 两名外嫁女性」⇒ 该服刑者当选）；**同月多人死亡 ⇒ 先归档完再继任一次**（MUST NOT 选出已亡者、MUST NOT 逐死继任）；新生儿 MUST NOT 被额外设年龄条件排除（FR-036 不设年龄下限，Assumptions 25）；无在册男性 ⇒ `HeadId == null` + `HeadBecameVacant == true` + **不产生绝嗣判定或结局**；现任家主健在且未外嫁 ⇒ 本子步**不做任何事**（0 随机、`NewHeadId == null`）；`Family.SetHead`（`src/KFL.Core/Entities/Family.cs:308-328`）**签名不变**

### 实现

- [X] T062 [P] [US4] `src/KFL.Rules/Family/HeadSuccession.cs`（新）：`public static IReadOnlyList<Person> Candidates(Family family)` + `internal static PersonId? Select(IReadOnlyList<Person>)`（⑧-h 与查询面共用）；四档顺序逐字照 `contracts/illness-lifespan.md` §8；候选 = **在册男性**（含服刑、待阙）；①②③ 档以 `Person.AgeAt` 决胜（同岁按 `PersonId` 升序）；**④ 档先比 `AgeAt`、同岁再比在册子代数（多者胜）、仍同按 `PersonId` 升序**（FR-036）；候选 **MUST NOT 设年龄下限**；选出后经 `Family.SetHead` 写入；**无候选 ⇒ `SetHead(null)`** 且事实段 `HeadBecameVacant = true`
- [X] T063 [US4] 接线 ⑧-h：在 `src/KFL.Rules/Family/PopulationAdvance.cs` 的 ⑧-h 位**一次**调用 `HeadSuccession`（MUST NOT 每死一个继任一次），结果写 `NewHeadId` / `HeadBecameVacant`；**继任 MUST NOT 经 `Marry` / 归档路径的回调实现**（保留 001 `tasks.md:320-323` 的 2026-10-05 裁决「不增设归档回调或事件通知」），而由月结算步驱动（FR-037）——依赖 T022、T062
- [X] T064 [P] [US4] `src/KFL.Rules/Family/LifecycleQueries.cs` 追加 `public static IReadOnlyList<Person> SuccessionCandidates(Family family)`（家主与继任候选序列的只读投影，供 U2 消费；**纯函数**）

**Checkpoint**: US1~US4 全部可独立验收（quickstart 场景 7）

---

## Phase 7: User Story 5 - 买人口（补充旁系成员） (Priority: P5)

**Goal**: 交付任意月份的买人口动作：本年度第 n 名 `10 × n` 贯（**每年 1 月重置**）、现银 + 储蓄全额、付不起即拒绝且**不转贷款**；
买来者年龄 8~16 整数均匀（`Next(8, 17)`，**含端点**）、性别 50/50、家族内**无父母**、属性按「无父母参照者」口径、
**冠家族姓**、辈分 = 家主辈分 + 1，计入在册存活与计口。

**Independent Test**: 只实现买人口入口与成员初始化（不插入结算步）：连续购买同年第 1/2/3 名断言价格恰为
**10 / 20 / 30 贯**（§9.5 定价**不含支出系数**）；跨年 1 月后回到 10 贯；辈分 = 家主辈分 + 1、`IsOutsider == true`、
`FatherId` / `MotherId` 均 `null`、姓名冠家族姓；`HeadId == null` ⇒ `HeadVacant` 且**零随机**。

### 测试（先写，红色）

- [X] T065 [P] [US5] `tests/KFL.Tests/Rules/PurchaseTests.cs`（新）：SC-006 —— 本年第 1/2/3 名价格恰为 10 / 20 / 30 贯（`Money.FromGuan(PopulationPurchasePolicy.BasePriceGuan * n)`）；跨年 1 月重置回 10 贯；**同月多次购买**价格按「本年度已购数 + 1」递增、计数在同一月内即时更新；付不起 ⇒ `InsufficientFunds` + 随机消费 0 + MUST NOT 转贷款 + MUST NOT 记 0 金额条目；`HeadId == null` ⇒ `HeadVacant` + 零随机；年龄恒落 `[8, 16]`（含端点）、性别两值都可能、`FatherId` / `MotherId` 均 `null`、辈分 = 家主辈分 + 1、`IsOutsider == true`、计入 `CountedMembers.Counted`；各落 1 条 `PersonPurchase`（资金类，负额）与 1 条 `PersonBought`（事件类）；首次与家族内成员成婚 ⇒ 辈分对齐配偶辈分且此后**不再变动**（外来者此生总计变动两次，§4.4）

### 实现

- [X] T066 [US5] `src/KFL.Rules/Family/LifecycleEntry.cs` 追加 `public static LifecycleOutcome BuyPerson(GameState state, IRandomService random, INameGenerator names)`：①校验（`Family.HeadId == null` ⇒ `HeadVacant`，**0 随机**；报价 = `BasePriceGuan × Lifecycle.PeekPurchaseOrdinal(year)`，**纯读、不得推进计数**）→ ②`CanCover` → ③`Pay(PersonPurchase)` → ③b**扣款成功后**才调 `Lifecycle.NextPurchaseOrdinal(year)` 提交本年度名次（**失败路径 MUST NOT 改变 `PurchaseCount` / `PurchaseYear`**——FR-039「不足即拒绝」要求状态不变）→ ④**初始化次序**：年龄 `Next(8, 17)` → 性别 `Next(0, 2)` → 天赋 ×4 → 学业 → 体质 → 寿数 → 姓名（**与 003 开局口径对称**，Assumption 17；无父母参照者分布）→ ⑤`Family.AddOutsider`（`src/KFL.Core/Entities/Family.cs:176`）+ `SetOutsiderGeneration`（`:199`）/`BoughtCollateralGeneration`（`:89-92` = 家主辈分 + 1）→ ⑥落 1 条 `PersonPurchase` + 1 条 `PersonBought`；**不设购买门槛、不可指定性别与年龄**；MUST NOT 另存汇总值（§12.3）——依赖 T016、T018、T036、T037、T008
- [X] T067 [P] [US5] `src/KFL.Rules/Family/LifecycleQueries.cs` 追加 `public static PurchaseQuote PurchaseQuote(GameState state)`（含 `HeadVacant` 拒绝原因与当次报价；**纯函数**，报价 MUST 经 `Lifecycle.PeekPurchaseOrdinal(year)`，MUST NOT 调用带副作用的 `NextPurchaseOrdinal`）
- [X] T068 [P] [US5] 接线与断言：确认 `CountedMembers`（`src/KFL.Rules/Settlement/CountedMembers.cs:36/:47/:58/:69/:83`）的三个口径在买来者入册后即计入，无需新增筛选；`Family.BoughtCollateralGeneration` 在 `HeadId` 为 `null` 时抛异常（001 `data-model.md:269`）——该异常 MUST NOT 被 007 捕获或吞掉，因为 `HeadVacant` 已在 T066 的第 ① 步拦截

**Checkpoint**: 五个用户故事全部可独立验收（quickstart 场景 1~8）

---

## Phase 8: 持久化（跨故事；FR-047 / SC-010）

**Purpose**: 本特性的全部新增持久状态落盘且往返无损；`SaveFileFormat.Version` **3 → 4**；**无迁移路径**（004 已裁 Out of Scope）

- [X] T069 [P] `src/KFL.Infrastructure/Save/SaveFileFormat.cs:31`：`Version` **3 → 4**；`!= 4` 一律 `UnsupportedVersion`（`src/KFL.Infrastructure/Save/Serialization/MessagePackSaveFormat.cs:363-365` 既有路径，MUST NOT 新增迁移分支）
- [X] T070 [P] `src/KFL.Infrastructure/Save/Serialization/SaveStateDto.cs`：`PersonDto`（`:143-316`）**28 → 29 键**（尾部 `[Key(28)] bool isTutoredByMaster`）；`FamilyDto`（`:83-133`）**8 → 9 键**（尾部 `[Key(8)] FamilyLifecycleDto lifecycle`）
- [X] T071 [P] `src/KFL.Infrastructure/Save/Serialization/SaveStateValueDtos.cs`：`StatusTimersDto`（`:258-284`）**4 → 6 键**（尾部 `[Key(4)] int? illnessSeverityBasisPoints`、`[Key(5)] int? treatmentRemainingMonths`）；新增 4 个 DTO —— `FamilyLifecycleDto`（6 键：`purchaseCount` / `purchaseYear` / `pendingBirths` / `appliedBirthCouples` / `birthApplicationYear` / `proposal`）、`PendingBirthDto`（4 键：`husbandId` / `wifeId` / `appliedYear` / `appliedMonth`）、`CoupleKeyDto`（2 键：`husbandId` / `wifeId`）、`MarriageProposalDto`（**19 键**：与 `MarriageProposal` 的 18 个成员逐一对应，`ProposedAt` 拆成 `proposedYear` + `proposedMonth` ⇒ 18 → 19）
- [X] T072 `src/KFL.Infrastructure/Save/Serialization/MessagePackSaveFormat.cs`：成员数常量 `FamilyMemberCount 8 → 9`（`:47`）、`PersonMemberCount 28 → 29`（`:50`）、`StatusTimersMemberCount 4 → 6`（`:56`），新增 `FamilyLifecycleMemberCount = 6` / `PendingBirthMemberCount = 4` / `CoupleKeyMemberCount = 2` / `MarriageProposalMemberCount = 19`；`Skip` 序列：`:237`（Family 0-2 之后）插 lifecycle 分支、`:309`（Person 22-27 之后）插 `Skip(1)`（`isTutoredByMaster`）、`:295` 的 `Skip(4)` 改 `Skip(6)`；`EnsureFamilyShape`（`:234`）/ `EnsurePersonShape`（`:284`）/ 新增 `EnsureLifecycleShape` 逐一校验键数——依赖 T070、T071
- [X] T073 `src/KFL.Infrastructure/Save/Serialization/SaveStateMapper.cs`：`ToDto`（`:38`）补三个新段；`Build`（`:100`）在 `:222`（`RestoreOutsiderBookkeeping`）之后追加 `family.Lifecycle.RestoreBookkeeping(...)`、在成员标量段（`:149-158` 附近）追加 `person.RestoreTutoring(...)`；两侧 MUST 覆盖持久化清单五组（①`Person` 的延名师标志 ②`StatusTimers` 的疾病程度 `s` 与疗程剩余月数 ③买人口计数与所属年份 ④待分娩记录 + 本年度已申请生育的夫妇集合 ⑤待应答提亲提议）——依赖 T070、T071、T072
- [X] T074 `src/KFL.Infrastructure/Save/Serialization/SaveStateValidator.cs`：新增两条结构校验 —— 第 13 条 `EnsureLifecycleProposalReferencesMember`（`Proposal.OurMemberId` 必须在册）、第 14 条 `EnsurePendingBirthsApplied`（不变量 6：每个 `PendingBirth.Couple` 必在 `AppliedBirthCouples` 中且 `PendingBirths.Count <= AppliedBirthCouples.Count`）；既有 `HeadId` 校验（`:68-73`）MUST 允许 `null`（家主空缺合法）——依赖 T071、T073
- [X] T075 [P] 测试：`tests/KFL.Tests/Infrastructure/SaveStateValidatorTests.cs` 补 `HeadId == null`（家主空缺）与 `Lifecycle` 引用完整性的正反用例；新增/扩展存档往返用例断言 `SaveFileFormat.Version == 4`、`Version == 3` 旧档被 `UnsupportedVersion` 拒绝、`PersonDto` 29 键 / `FamilyDto` 9 键 / `StatusTimersDto` 6 键往返无损（SC-010、quickstart 场景 8）

**Checkpoint**: SC-009 / SC-010 可断言；两种格式（MessagePack / JSON）承载同一信息集

---

## Phase 9: 回写义务（同一提交内；`AGENTS.md` 铁律 / FR-050 / SC-011）

**Purpose**: 本特性在公开面上做了七处跨特性形状变更（① 结算九步 → **十步**；② `SettlementResult` **+1 必填子段**；
③ `StatusTimers` **+2 项**（4 → 6）；④ `Person` **+1 可写属性**；⑤ `Family` **+1 聚合属性**；
⑥ `LedgerCategory` **+16 取值**（22 → 38）；⑦ 存档 `Version 3 → 4`）。
下表的行号为**回写前原位**（`plan.md:114`）。

- [X] T076 回写①`科举浮生录规格书.md`：§4.3 的「病程 2~4 月」「固定 −10 体质/月」整体替换为 Q2；「患病概率 16 岁以上 2.5%/月…封顶 20%」替换为 Q4 + Q5 + Q6（含 `< 3` 岁免病、三段年龄增益 `+0.60 / +0.30 / 0`、基础痊愈系数 `1.00`、宽限月、疗程式 `(体质 × 1.10/100 + 0.20) × m_q + b`、患病概率不乘难度系数）；§9.1/§9.2 补提亲 5%、捉婿 `30% ÷ (1 + P)`、金额 `×(1 + P)`、功名分档阈值 1/3、**提议可拒绝**、阶层加权三档（阈值 100 / 1000 贯；权重 70/25/5、45/40/15、20/50/30）；**§9.3** 补买功名婚姻的降级链（进士 → 贡士 → 举人）与 150% 系数；§9.4 补夭折粒度（出生当月 + 1 岁 + 2 岁）；**§4.2** 补夭折三次判定与「寿数只 clamp 下界」；**§4.4** 补继任四档与「无在册男性 ⇒ `HeadId = null`，不构成绝嗣」；**§5.4** 补「救济期体质不减」及于患病的口径注；§17 块标注裁决日期 **2026-10-17**（FR-050 ① 的完整范围 = §4.2 / §4.3 / §4.4 / §5.4 / §9.1~§9.4 / §17）
- [X] T077 回写②`specs/001-core-skeleton/data-model.md`：`:188-225` 不变量 4 追加 ⑦ 引用注（`Ill` 位 ⇔ `IllnessSeverityBasisPoints != null`）；**「无写入通道成员全集 19 项」保持不变**（`StatusTimers` 的两项在 `Timers` 内，不构成 `Person` 成员），要改的是 `:195-196` 的「`Person` 自持可写属性十个」→ **十一个**（追加 `IsTutoredByMaster`，与 T026 同源）；`:308-310` 嫁入配偶的仕身份差异补 ⑦ 落实注；`:244-248` 删除「成长衰老疾病死亡的年龄线与概率不含」的旧口径；`:293-303` 不变量 6 补「由 ⑧-h 月度驱动修正」；`:318-322` 继任判定改为「已由 ⑦ 交付」；`:402-406` 状态转移表补**患病 / 归档 / 外嫁 / 继任**四行；`:227` 补「死因只落当月事实段」（FR-050 ②）
- [X] T078 回写③`specs/001-core-skeleton/contracts/injection-seams.md`：`:91-99` 维持「`IEventBus` / `IAchievementStore` 至今未定义」——本特性**不新增接缝**（快进暂停信号走 `SettlementResult.Lifecycle` + `LifecycleQueries`），只补一段「⑦ 未新增接缝」的确认（FR-045、FR-050 ③）
- [X] T079 回写④`specs/002-monthly-settlement-economy/contracts/monthly-settlement.md`：`:13-23` 步骤表九行 → **十行**（插 ⑧，原 ⑧/⑨ → ⑨/⑩）；`:41-42` 随机次序式补 ⑧-a~⑧-f；`:51-52` 两条从「MUST NOT 存在」改为「已由 ⑦ 交付」；`:85` / `:88-96` 次序式与映射表补本特性条目；`:222` 饥馑 −10 与 5% 判定的归属改为「⑦ 已交付」；`:229-239` 明确不包含节删去已交付项（FR-050 ④）
- [X] T080 回写⑤`specs/003-opening-assets-officialdom/data-model.md:163-164` 与 `specs/003-opening-assets-officialdom/contracts/new-game-setup.md:106-110`：补「无父母参照者分布（天赋 `N(60,20)` / 学业 `N(30,15)` / 体质 `N(85,10)`）被 ⑦ 复用于**买人口与婚配对象**」（FR-050 ⑤）
- [X] T081 回写⑥`specs/004-save-system/contracts/save-format.md`：`:77` FamilyDto 8 → 9 键；`:78` PersonDto 28 → 29 键；`:80` StatusTimersDto 4 → 6 键；`:73-89` 表补 `FamilyLifecycleDto` / `PendingBirthDto` / `CoupleKeyDto` / `MarriageProposalDto`；`:128` / `:137` / `:150` 的 `== 3` 谓词与「MUST NOT 静默误读」段改 `== 4`；`:151-153` 键数常量同步。**顺带**修正 `specs/004-save-system/data-model.md:52/:71/:298/:349` 的存量旧值 `FormatVersion = 2`（006 遗留未回写项，活工件 MUST 与实现一致）（FR-047、FR-050 ⑥）
- [X] T082 回写⑦`specs/005-exam-cycle/contracts/exam-session.md`：`:18` 的八步旧序（006 未回写的存量缺口）改为十步；`:21` 的「⑦ 在科举季之前」补一句「⑦ 已交付为 ⑧」（FR-050 ⑦）
- [X] T083 回写⑧`specs/006-bribery-punishment/`：`contracts/config-registry.md:95-97`、`contracts/punishment.md:279-280`、`spec.md:461-466` 的「属逻辑轨 ⑦」改述为「⑦ 已交付」（FR-050 ⑧）；并顺带订正 `src/KFL.Rules/Exam/ExamSeasonRunner.cs:17` 的自述「本步是 `MonthlySettlementEngine.Settle` 的**第 ⑦ 步**，位于「⑥ 贷款」之后」为**第 ⑨ 步**（勘误 E-02，**仅注释层**；引擎与契约口径均为第 ⑨ 步，实现不读该编号）
- [X] T084 回写⑨`tests/KFL.Tests/Rules/SettlementEngineTests.cs`：`:305-307` 注释改十步口径；`:313` 仍 `["Settle"]`；`:315-337` `expectedProperties` **20 → 21**（末尾加 `Lifecycle`）（FR-010、FR-050 ⑨；与 T030 同一处，MUST 只改一次）
- [X] T085 回写⑩`specs/007-family-lifecycle/tasks.md`（本文件）：登记上述九项的回写范围，并登记「本特性接管 001 `tasks.md:88`、002 `tasks.md:83` / `:279` 的『阶段⑧』残句」（FR-050 ⑩）——见下方「回写范围登记」

### 回写范围登记（FR-050 ⑩ / `AGENTS.md` 铁律第 3 条）

- [X] T086 在 `specs/007-family-lifecycle/tasks.md` 末尾按下表补齐「同步了哪些文件、同步到哪里」的**逐条落点**（回写完成后回填实际行号），并显式声明三条**未回改**（历史记录类工件）：

  | # | 目标工件（活工件） | 同步到 |
  |---|---|---|
  | 1 | `科举浮生录规格书.md` | §4.2 / §4.3 / §4.4 / §5.4 / §9.1 / §9.2 / §9.3 / §9.4 / §17 裁决块（2026-10-17，Q1~Q6）——与 FR-050 ① 逐项一致 |
  | 2 | `specs/001-core-skeleton/data-model.md` | `:188-225`（含 `:195-196` 的可写属性计数与不变量 4 的 ⑦ 引用注）/ `:227` / `:244-248` / `:293-303` / `:308-310` / `:318-322` / `:402-406` |
  | 3 | `specs/001-core-skeleton/contracts/injection-seams.md` | `:91-99`（仅确认未新增接缝） |
  | 4 | `specs/002-monthly-settlement-economy/contracts/monthly-settlement.md` | `:13-23` / `:41-42` / `:51-52` / `:85` / `:88-96` / `:222` / `:229-239` |
  | 5 | `specs/003-opening-assets-officialdom/data-model.md`、`contracts/new-game-setup.md` | `data-model.md:163-164`、`new-game-setup.md:106-110` |
  | 6 | `specs/004-save-system/contracts/save-format.md`、`data-model.md` | `save-format.md:73-89` / `:77` / `:78` / `:80` / `:128` / `:137` / `:150` / `:151-153`；`data-model.md:52` / `:71` / `:298` / `:349` |
  | 7 | `specs/005-exam-cycle/contracts/exam-session.md` | `:18` / `:21` |
  | 8 | `specs/006-bribery-punishment/`、`src/KFL.Rules/Exam/ExamSeasonRunner.cs` | `contracts/config-registry.md:95-97`、`contracts/punishment.md:279-280`、`spec.md:461-466`；`ExamSeasonRunner.cs:17` 的步骤序号注释（勘误 E-02） |
  | 9 | `tests/KFL.Tests/Rules/SettlementEngineTests.cs` | `:305-307` / `:313` / `:315-337` |
  | 10 | `specs/007-family-lifecycle/tasks.md` | 本表 + 下方未回改声明 |

  **未回改（历史记录类工件）**：`specs/001-core-skeleton/tasks.md:88`、`specs/002-monthly-settlement-economy/tasks.md:83` / `:279`
  写「阶段⑧」的条目、各 `implementation-notes.md`、既有 `checklists/` —— MUST NOT 回改，仅在本文件登记「已接管」。

- [X] T087 [P] 回写核对：逐条检索 T076~T086 的落点可命中（SC-011）；确认 001/002 `tasks.md` 的「阶段⑧」残句**未被回改**；确认 `spec.md` 自身的 Assumptions 开放项状态与规格书 §17 块一致（`spec.md` 是 skills 的 **sole source of intent**）

---

## Phase 10: Polish & Cross-Cutting Concerns

- [X] T088 [P] 门禁四条全跑并留档：`dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false`（**0 警告 0 错误**）、`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false`（全绿，含 `tests/KFL.Tests/Architecture/` 的 G-01~G-08）、`dotnet sln KejuFuShengLu.slnx list`（恰六个工程）、`Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }`（**无输出**）（FR-051、SC-008）
- [X] T089 [P] 越界制止自查（SC-012）：仓库中 MUST NOT 出现**绝嗣判定**、**成就阈值**、**界面代码**（`src/KFL.Presentation/` 下的 View / ViewModel / 按钮 / 快捷键，本特性 0 行）、**全局随机源**（`Random` / `DateTime.Now` / `Guid.NewGuid`，G-07，由 `tests/KFL.Tests/Architecture/EnvironmentDependencyTests.cs` 强制）、**重复声明的成年判据**（`12` / `14` 只允许出现在既有 `AgeBracketPolicy` 与 006 登记的复用注记中）、`IEventBus` / `IAchievementStore` 的自建定义
- [X] T090 [P] 数值纪律自查：`src/KFL.Rules/Config/` 与测试之外 MUST NOT 出现本特性数值（FR-002，由 `tests/KFL.Tests/Architecture/ConfigLiteralTests.cs` 强制）；一切金额 MUST 经 `Money.FromGuan(<配置成员>)`；`IllnessPolicy` 内 MUST NOT 出现 `NegativeEventFactor`；`GameConfig` 六个转发视图 MUST 零字面量
- [X] T091 [P] 运行 `quickstart.md` 的 9 个场景（重点是场景 2 的**反例检查**：把 `IllnessSeverityBasisPoints` 换成 `double` 实现后，场景 2 在 JSON 存档上**应**变红——这是 R-03 选整数的原因；验证后 MUST 立即改回整数实现）
- [X] T092 提交纪律：按章程 V 以中文约定式提交拆分（建议 `feat(rules): 家族生命周期…` 主体 + `feat(core): …` 状态载体 + `feat(infra): 存档 Version 3→4` + `docs: 回写 ⑦ 交付口径` + `test: …`），一次提交只做一件事；回写义务（Phase 9）MUST 与实现**同一提交**（`AGENTS.md` 铁律）

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1（Setup）**：无依赖，可立即开始
- **Phase 2（Foundational）**：依赖 Phase 1；**CRITICAL** —— 阻塞 US1~US5 全部
- **Phase 3（US1）**：依赖 Phase 2；是其余故事的**结构前提**（`LifecycleEntry` / `LifecycleQueries` / `LifecycleOutcome` / 金额与账本类别）
- **Phase 4（US2）**：依赖 Phase 2 + ⑧-b 挂点（T022）；**只**依赖 ⑧-b，不依赖 ⑧-a / ⑧-c~⑧-h
- **Phase 5（US3）**：依赖 Phase 2 + ⑧-c~⑧-g 挂点；⑧-g 是**唯一**置 `Deceased` 的地方，US2 的分娩死亡与 US4 的继任均经它
- **Phase 6（US4）**：依赖 Phase 5（⑧-g 先归档，⑧-h 才能取候选集）
- **Phase 7（US5）**：依赖 Phase 2 + Phase 3（`LifecycleEntry` 骨架）+ Phase 6（辈分基准 `BoughtCollateralGeneration` = 家主辈分 + 1）
- **Phase 8（持久化）**：依赖 Phase 2 的形状定案（T007~T013）与 Phase 3~7 的字段落定；可与 US5 并行
- **Phase 9（回写）**：依赖 Phase 3~8 的实现完成（回写内容须与实现一致）
- **Phase 10（Polish）**：依赖全部

### Within Each User Story

1. **测试先写**（对契约签名写断言，红色）
2. **实现**（配置 → 纯函数 → 执行器 → 接线）
3. **接线到 ⑧**（`PopulationAdvance` 的对应子步）
4. 该故事独立验收（quickstart 对应场景）后再进入下一个优先级

### Parallel Opportunities

- Phase 1：T001 与 T002 可并行（但 T001 需先跑基线）
- Phase 2：`KFL.Core` 的 12 个载体任务（T003~T013）中除 T008 / T011 外均可并行；5 个配置类（T014~T018）可全并行；`[P]` 标记的断言物同步（T026~T032）全部可并行
- Phase 3（US1）：T033 / T034（测试）并行 → T035 / T036 并行 → 其余串行
- Phase 4（US2）：T042 / T043（测试）并行 → T044 先行 → T045 / T046 可部分并行
- Phase 5（US3）：T048 / T049 / T050（测试）并行 → T051 → T052 / T053 → T054 / T055 / T056 / T057 / T058
- Phase 6（US4）：T061 先行 → T062 → T063；T064 可与 T062 并行
- Phase 7（US5）：T065 先行 → T066 → T067 / T068 并行
- Phase 8：T069 / T070 / T071 并行 → T072 → T073 → T074 → T075
- Phase 10：T088~T091 可并行

### 建议的并行示例

```text
# Phase 2 的内核载体（同一轮可并行提交）
T003 DeathCause.cs          T004 MarriageClass.cs      T005 MarriageProposalKind.cs
T006 CoupleKey/PendingBirth T007 MarriageProposal.cs   T009 StatusTimers.cs
T010 Person.cs              T012 LedgerCategory.cs     T013 LedgerCategoryMetadata.cs
# → 再串行：T008（依赖 T006/T007）→ T011（依赖 T008）

# Phase 2 的数值单点（同一轮可并行）
T014 GrowthPolicy.cs  T015 IllnessPolicy.cs  T016 MarriagePolicy.cs
T017 ChildbirthPolicy.cs  T018 PopulationPurchasePolicy.cs
# → 再串行：T019 GameConfig.cs（依赖 T014~T018）

# Phase 5（US3）的测试三件套（同一轮可并行）
T048 IllnessTests.cs   T049 LifespanTests.cs   T050 FamilyLifecycleTests.cs
```

---

## Implementation Strategy

### MVP First（User Story 1 Only）

1. Phase 1（Setup）→ 2. Phase 2（Foundational）→ 3. Phase 3（US1 婚配）
4. **STOP and VALIDATE**：跑 quickstart 场景 6（婚配与 24 格金额表）+ 场景 1 的十步骨架
5. 此时家族已能**娶进、嫁出**，`RegisteredMembers` 与族谱随之变化——但成员的成长/疾病/寿命仍全空

### Incremental Delivery

1. Phase 2 → 骨架就绪，既有测试全绿
2. +US1 → 婚配链闭合（MVP）
3. +US2 → 家族能**新生**（`→ 遗传公式`首次有测试）
4. +US3 → 家族开始**变老、生病、死亡**（民生闭环；此时 `HeadId` 已可能悬空，故 US4 必须紧随）
5. +US4 → 死亡不再破坏不变量 6
6. +US5 → 第二条扩员途径
7. +Phase 8 → 全部新状态可持久化（SC-010）
8. +Phase 9 → 回写义务与规格书同步（SC-011）
9. +Phase 10 → 门禁四条（SC-008）

### 交付前的不可省项

- `dotnet build` **0 警告 0 错误**（章程：阶段验收门禁三条同时满足）
- `dotnet sln KejuFuShengLu.slnx list` 恰**六个**工程（`.slnx` 解析器不认识 `<TestProject>` 且不报错，测试工程可能悄悄掉出解决方案）
- 仓库内**无** `*.sln` 与 `.slnx` 共存
- G-01~G-08 架构门禁全过
- `tests/KFL.Tests/KFL.Tests.csproj:21-25` 的 `_MSTestEnableParentProcessQuery` MUST 以**注释态**提交
- FR-050 的 ①~⑩ 回写与实现**同一提交**

---

## Notes

- 本特性的**数值真源**是《科举浮生录规格书》§4.1~§4.4、§5.4、§9.1~§9.5、§12.1、§12.3 与 §17 的**七项裁决（2026-10-17，Q1~Q7）**；`tasks.md` MUST NOT 自创或改写任何数值（规格书 §17）。
- **主导天赋口径**（Assumption 3c，已由 Q6 确认）：取**数值最高的一项**，并列按 **农 → 商 → 仕 → 工** 固定顺序，**四项不叠加**。若日后改为按天赋值缩放乘数或四项连乘，MUST 重算 `m_p` / `m_q` 并回写规格书 §4.3。
- **患病概率 MUST NOT 乘难度负面事件系数**（Q6）：该系数只作用于提亲 `5%` 与榜下捉婿 `30%` 的底数；故**不需要**「患病概率封顶 50%~60%」这类护栏。
- **核心数值的进一步调整仍属 §17**，MUST NOT 由实现自行选择。**开放项（实机观察，非本阶段阻断）**：药费档位（现行 `0.05 + 0.45s`；MAY 上调到 `0.08 + 0.55s` / `0.10 + 0.70s`，但 MUST NOT 与下调痊愈概率**同时**进行）、「饥馑 × 疾病叠加」的手感（见 `spec.md` Edge Cases 的最坏情形条目）。
- **`Person.Study` / `Person.Health` 的 setter 是抛异常守卫而非静默 clamp**（`src/KFL.Core/Entities/Person.cs:157`）⇒ 调用方 MUST 自己先 clamp（`Math.Clamp(0, 100)`）。
- **`KFL.Core/AssemblyInfo.cs:13` 只放行 `KFL.Infrastructure`** ⇒ 新增持久字段的复原通道只能 `internal` + Infrastructure 落盘。
- **`StatusTimers` 新项 MUST 尾插**，否则 004 的 `MessagePack` 位置式 `Skip` 序列错位。
- 本特性**不做**：界面轨 U1~U5、成就解锁与绝嗣判定（逻辑轨 ⑧）、难度启动装配（逻辑轨 ⑨）、族谱排序与绘制（U2）、存档格式迁移、随机事件总表（只实现 §9.1 提亲与 §9.2 榜下捉婿）、灾年米价跳涨与经商亏损、官名表、**家族声望属性**、继承与财产分割 / 诉讼 / 子女抚养、多家族与姻亲网络、`IEventBus` / `IAchievementStore`。
- **短期宿主提示**：一次只改一个文件；`IRandomService` 的每一个新增消费点 MUST **先登记进 `contracts/lifecycle.md` §3 的次序式**再写实现（FR-009）。

---

## 回写范围登记（FR-050 ⑩ / `AGENTS.md` 铁律第 3 条 / T085–T086）

**登记时点**：2026-10-17（Phase 9 完成后**回填实际行号**；下表行号均为**回写后原位**）。
**同一提交声明**：本登记与 T076~T084 的回写内容，按 `AGENTS.md`「变更授权在变更源，回写义务在同一提交」
并入 T033~T075 的实现提交（本地分支 `007-family-lifecycle` 未推送，以 `git commit --amend` 完成）。

| # | 目标工件（活工件） | 实际同步落点（回写后行号） |
|---|---|---|
| 1 | `科举浮生录规格书.md` | `:88`（§4.2 交付与复用）/ `:108-110`（§4.3 整体替换与旧稿作废）/ `:130`（§4.4 继任交付）/ `:212`（§5.4 交付与支付路径）/ `:220-226`（§5.4 的 §17 裁决回写注）/ `:414-452` 段内（§9.1~§9.5 的回写注）/ `:453`（§9 全章交付注）/ `:715-735`（§17 裁决块 Q1~Q7）——与 FR-050 ① 逐项一致 |
| 2 | `specs/001-core-skeleton/data-model.md` | `:195`（可写属性**十一个**）/ `:228`（不变量 4 的 007 引用注）/ `:236`（不变量 6：死因只落当月事实段）/ `:317`（⑧-h 豁免失效）/ `:329`（§17：不按人细分仕身份）/ `:436`（§5 状态转移 007 后记八行） |
| 3 | `specs/001-core-skeleton/contracts/injection-seams.md` | `:101-106`（007 未新增接缝；暂停信号走 `SettlementResult.Lifecycle` + 纯函数查询面） |
| 4 | `specs/002-monthly-settlement-economy/contracts/monthly-settlement.md` | `:45-57`（007 后记：九步→十步、随机次序式、属性 20→21）/ `:59-95`（§1.1 八子步表）/ `:126-129`（§4 次序式与映射表补 007 五行）/ `:286`（§9 明确不含节）/ `:317`（快照面后记） |
| 5 | `specs/003-opening-assets-officialdom/data-model.md`、`contracts/new-game-setup.md` | `data-model.md:166`；`new-game-setup.md:112-119`（无父母参照者分布被 007 复用） |
| 6 | `specs/004-save-system/contracts/save-format.md`、`data-model.md` | `save-format.md:77-81`（三处键数）/ `:136-158`（版本闸门 `== 4`、十四条、常量表）/ `:233-256`（007 后记）；`data-model.md:278` / `:355` / `:359` / `:507-519`（007 后记；**顺带订正 006 遗留的四处 `FormatVersion = 2` 存量旧值**） |
| 7 | `specs/005-exam-cycle/contracts/exam-session.md`、`data-model.md` | `exam-session.md:17-21`（十步口径与「⑦ 已交付为 ⑧」）；`data-model.md:187`（第 ⑨ 步） |
| 8 | `specs/006-bribery-punishment/`、`src/KFL.Rules/Exam/ExamSeasonRunner.cs` | `spec.md:461-466`（T103：逻辑轨 ⑦「已由 007 交付」）/ `contracts/config-registry.md:104-109`；`contracts/punishment.md:189-193`、`data-model.md:386-387`；`ExamSeasonRunner.cs:17` 的步骤序号注释（勘误 E-02，**仅注释层**） |
| 9 | `tests/KFL.Tests/Rules/SettlementEngineTests.cs` | `:308-311`（十步口径注释）/ `:317`（仍断言 `["Settle"]`）/ `:319-342`（`expectedProperties` **20 → 21**，末尾 `Lifecycle`） |
| 10 | `specs/007-family-lifecycle/tasks.md` | 本表 + 下方未回改声明 |

**未回改（历史记录类工件，MUST NOT 回改，仅在此登记「已接管」）**：
`specs/001-core-skeleton/tasks.md:88`（T020 的 `Lifespan` 上界「属阶段⑧」）、
`specs/002-monthly-settlement-economy/tasks.md:83`（T014 的「体质与死亡属阶段⑧」）与 `:279`（T051 的「体质 −10/月、5% 死亡判定属阶段⑧」）、
各 `implementation-notes.md`、既有 `checklists/`。

**本特性接管的残句**：上列三条「阶段⑧」指本特性交付的第 ⑧ 步（人口与健康推进）——由 007 交付，
原条目不回改（它们记录当时的阶段事实）。

**原待裁决项已裁决（2026-10-17，规格书 §17 裁决块 Q7 / §5.4）**：基础聘礼 / 嫁妆在
「可付额不足」时取**备选②整单失败且托媒费不退**。实现落点：`src/KFL.Rules/Family/LifecycleEntry.cs`
的 `TryMatchmake` ⑧a 段与 `RespondToProposal` 接受路径的支付能力校验（`PaymentPrimitive.CanCover`
不通过即整单失败：不成婚、无婚嫁条目、状态零变更、`LifecycleRejectReason.InsufficientFunds`、
托媒费与买功名费不退、MUST NOT 转贷款；接受提议时提议被清空 = 名额已消耗）；
`SettleAmounts` 的文档注释已同步为「调用方 MUST 先校验，故此处 `Pay` 恒不产生贷款」。
断言在 `tests/KFL.Tests/Rules/MarriageTests.cs` 的三例（`聘礼付不起时整单失败且托媒费不退` /
`嫁妆付不起时整单失败且不外嫁归档` / `接受提亲时付不起聘礼则整单失败且提议已消耗`，
另含「恰好付得起照常成婚」的边界断言）。至此本特性**不再有未决口径**。

---

## Phase 11 回写范围登记（T093~T105，FR-050 ⑩ / `AGENTS.md` 铁律第 3 条）

**登记时点**：2026-10-18（Phase 11 的实现与回写并入**同一提交**；下表行号均为**回写后原位**）。
**门禁**：`dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false` 0 警告 0 错误；`dotnet test`
（全量，含 `-p:_MSTestEnableParentProcessQuery=false`）全绿；`dotnet sln list` 恰好 6 个工程；
仓库内除 `.git` 外无 `*.sln`。

| # | 目标工件（活工件） | 实际同步落点（回写后行号） |
|---|---|---|
| 1 | `src/KFL.Core/`、`src/KFL.Rules/Family/`、`src/KFL.Infrastructure/Save/` 实现侧 | 新增 `ValueObjects/MatchmakeApplication.cs`；`Entities/FamilyLifecycleState.cs` 三方法 + 七参簿记；`Family/LifecycleOutcome.cs` 的 `AlreadyAppliedThisMonth`；`Family/LifecycleQueries.cs` 的 `MatchmakeFeasibility` 口径；`Family/LifecycleEntry.cs` ①a/④a；`Family/MarriageMarket.cs` 可支付候选集合 + `Next(0, 候选数)`；`Family/DeathSettlement.cs` 调 `Family.EndMarriage`；`Save/Serialization/` 新增 `MatchmakeApplicationDto`（3 键）、`FamilyLifecycleDto` 7 键、`FamilyLifecycleMemberCount = 7`、JSON 缺键一律拒绝（T098） |
| 2 | `specs/007-family-lifecycle/spec.md` | `:507-509`（FR-013 当月名额持久化）/ `:600-603`（FR-026 四档夭折率）/ `:760-768`（FR-047 六组 + `FamilyLifecycleDto` 7 键 + 常量 7/3） |
| 3 | `specs/007-family-lifecycle/data-model.md` | §2.2（新增 `MatchmakeApplication`）/ §2.3（六组、七元组、`RestoreBookkeeping` 七参）/ §3.3（①a・④a）/ §3.4（`MatchmakeFeasibility.RejectReason`）/ §3.5（枚举）/ §6（不变量 14）/ §7（7 键 + `MatchmakeApplicationDto` + 十五条校验器描述） |
| 4 | `specs/007-family-lifecycle/contracts/lifecycle.md` | `:41`（⑧-a）/ `:47`（⑧-g 丧偶终止）/ `:56-58`（R-25 第 3 条；原「不再消耗额外随机」句删除）/ `:96`（§3 随机消费次序式） |
| 5 | `specs/007-family-lifecycle/contracts/marriage-birth.md` | `:29-31`（§2 第 3 条）/ `:42`（§3 失败行去掉「不落持久字段」）/ `:49-53`（①a・④a）/ `:75`（§4.1 候选集合）/ §7 条款 2（四档夭折率） |
| 6 | `specs/007-family-lifecycle/contracts/illness-lifespan.md`、`contracts/config-registry.md` | `illness-lifespan.md:153`（§7.2 四档与累计口径）/ `config-registry.md:102`（四档同步） |
| 7 | `specs/004-save-system/contracts/save-format.md`、`data-model.md` | `save-format.md:81`（`FamilyLifecycleDto` 7 键 + 新 DTO 行）/ `:124`（条款 3 缺键对称）/ `:137`・`:151`（十四条 → 十五条）/ `:159`（常量 7、3）/ `:234-260`（007 后记 + 缺键对称段）；`data-model.md:124` / `:227-228` / `:278` / `:311` / `:359` / `:520-521`（线上成员计数按 007 基线再 +4） |
| 8 | `specs/001-core-skeleton/data-model.md` | `:120-125`（§1.7 `StatusTimers` 由四个字段补到**六个字段**，含 `Ill` 位配对与来源）/ `:281`（§2.2 `Family` 成员表补 `Lifecycle` 行）——T102 |
| 9 | `specs/006-bribery-punishment/spec.md` | `:461-466`（逻辑轨 ⑦「已由 007 交付」并写明 ⑧ 子步位于第⑧步）——T103；同时补进上方登记表 #8 |
| 10 | `科举浮生录规格书.md` | `:718`（标题 `Q1~Q7`）/ `:720`（七项裁决）——T104；Q3/Q7 数值裁决行（`:439`・`:728`）为真源，未改 |
| 11 | 实现侧注释层 | `src/KFL.Rules/Config/ChildbirthPolicy.cs:51-60`（`InfantDeathFactor` 删除「待规格书 §17 裁决」失真注释，改注 Q3 裁决行）——T105；`src/KFL.Infrastructure/Save/Serialization/JsonSaveFormat.cs:16`（`FormatVersion: 4`）——T104 |
| 12 | 测试 | `tests/KFL.Tests/Rules/MarriageTests.cs`（T093 三例、T097 两例）/ `Rules/ChildbirthTests.cs`（T094 三死因 `[Theory]`）/ `Rules/DeterminismTests.cs`（T095 成员级恒等比较面）/ `Infrastructure/SaveFixtures.cs`・`SaveStateComparer.cs`（T096 五类状态 + `IsTutoredByMaster`）/ `Infrastructure/SaveStateValidatorTests.cs`（T101 十例）/ `Infrastructure/SaveFailureTests.cs`（T099 两格式 `formatVersion = 3` 拒绝）/ `Infrastructure/SaveFormatContractTests.cs`（键表 7 键 + `MatchmakeApplicationDto`） |

**T104 的落点勘误**：任务文字所列 `specs/004-save-system/data-model.md:403` 的「引用行号」在该文件内
并不存在（`:403` 是 FR-002 的 `84 个线上成员` 追溯行，不含源码行号）；真正的陈旧引用在
`specs/007-family-lifecycle/data-model.md:431-433`（原写 `MessagePackSaveFormat.cs:363-365`），已订正为
`MessagePackSaveFormat.EnsureVersion`（`src/KFL.Infrastructure/Save/Serialization/MessagePackSaveFormat.cs:423-430`）
与 `JsonSaveFormat.cs:393-401`。`specs/004-save-system/data-model.md:403` 保持原样。

**未回改（历史记录类工件，MUST NOT 回改）**：本文件 T001~T092 既有条目（含其 `(missing)` /
`(partial)` / `(contradicts)` 标注——它们记录收敛时点的缺口事实）、各 `implementation-notes.md`、
既有 `checklists/`。

---

## Phase 11: Convergence

> 来源：2026-10-17 `$speckit-converge` 对 007 已交付实现（commit `39eccf9`）与 `spec.md` / `plan.md` / `tasks.md` 的逐条比对。
> 门禁现状：`dotnet build` 0 警告 0 错误、`dotnet test` 960 全绿 —— **完成声明不构成证据**，下列条目按当前行为重新核对后仍为缺口。
> 本阶段**只追加**，MUST NOT 回改 T001~T092 既有条目；实现时仍受 `AGENTS.md`「变更授权在变更源，回写义务在同一提交」约束。

- [X] T093 [HIGH] 在 `TryMatchmake` 之前补「同一成员同一月至多一次托媒」守卫：`FamilyLifecycleState` 新增持久记录（成员 × 年月的托媒申请，形状与 `AppliedBirthCouples` 同款），`LifecycleQueries.MatchmakeFeasibility` 暴露该判定，`LifecycleRejectReason` 增对应拒绝值；失败后下月可再试，命中该项时 0 随机、0 支付、状态零变更；同步 `specs/007-family-lifecycle/contracts/marriage-birth.md:42`（删「不落持久字段」）与 `contracts/lifecycle.md` §3 随机次序式、`spec.md` FR-013/FR-047 五项清单、`specs/007-family-lifecycle/data-model.md`、`specs/004-save-system/contracts/save-format.md` 与 `data-model.md` 的 `FamilyLifecycleDto` 键数（6→7）与 validator；补 `tests/KFL.Tests/Rules/MarriageTests.cs` 用例（同月第二次被拒且 `CountingRandom` 无消费、下月可再试）per FR-013 (missing)
- [X] T094 [HIGH] 在 `DeathSettlement.Run` 归档每名亡者后，对其在世配偶调用 `Family.EndMarriage(亡者 PersonId)`，使 `SpouseId` 置空并追加 `FormerSpouseIds`（⑧-g 死亡结算归档路径统一承担，`Childbirth.cs:131` 的分娩死亡路径改为复用或保持等价）；同步 `specs/007-family-lifecycle/contracts/lifecycle.md` ⑧-g 行与 `contracts/marriage-birth.md:29`；补测试断言病亡/寿终/饥馑三种死因下在世配偶「可再婚、`FormerSpouseIds` 追加、再婚不覆盖婚姻历史」per FR-021 (missing)
- [X] T095 [HIGH] 扩展 `tests/KFL.Tests/Rules/DeterminismTests.cs` 的 120 月运行恒等比较面至成员级：成员集合与逐人字段（四项天赋、`Study`、`Health`、`Generation`、`SpouseId`、`FormerSpouseIds`、`Status`、`Timers` 的 `illnessSeverityBasisPoints`/`treatmentRemainingMonths`、`IsTutoredByMaster`）、`HeadId`、`Family.Lifecycle` 全簿记（proposal / pendingBirths / appliedBirthCouples / purchaseCount / purchaseYear / birthApplicationYear）与账本；并断言 ⑧ 步确被驱动（同一次运行内至少发生 1 次患病、分娩与婚配）per SC-002 (partial)
- [X] T096 [HIGH] 补齐 007 五类状态的存档往返实证：`tests/KFL.Tests/Infrastructure/SaveFixtures.cs` 的 `RichState` 置入 `IsTutoredByMaster`、患病 `s` + 疗程、买人口计数与年份、`pendingBirths`/`appliedBirthCouples`/`birthApplicationYear`、`proposal`（含捉婿 `kind`）全部 19 个字段，并让 `SaveStateComparer.cs` 逐字段比较 `Lifecycle` 与 `IsTutoredByMaster`；JSON 与 MessagePack 两条路径都要断言 per FR-047 (partial)
- [X] T097 [MEDIUM] 使 `MarriageMarket.DrawMatchmakerProposal` 与 FR-020 对齐：候选集合只保留「本家可为其支付相应聘礼者」，命中后在候选集合内以 `Next(0, 候选数)` 均匀抽 1 名（新增一次随机消费），阶层分段只用于确定对方阶层与金额；同步 `contracts/lifecycle.md:56-57`（删「不额外消费随机」）、`contracts/marriage-birth.md:71`、`contracts/lifecycle.md` §3 次序式；补测试（付不起聘礼者不入候选集、命中后随机消费次数与次序表一致）per FR-020 (partial)
- [X] T098 [MEDIUM] 消除 JSON 与 MessagePack 的「缺失键」行为不对称：`JsonSaveFormat.cs:230-239` 对无 `[JsonRequired]` 的可缺失属性不再静默 `continue`，使缺 `"proposal"` 键的可选成员被检出（与 `SaveStateValueDtos.cs:860-866` 注释和 `specs/004-save-system/contracts/save-format.md:253-255` 一致）；若判定为「可空成员允许缺键」，则 MUST 反向订正注释与契约并说明理由；两种情况都要补两格式对照用例 per SC-010 (contradicts)
- [X] T099 [MEDIUM] 在 `tests/KFL.Tests/Infrastructure/SaveFailureTests.cs` 补 `formatVersion = 3` 的拒绝用例（JSON 与 MessagePack 各一），与 `specs/004-save-system/contracts/save-format.md:156` 的用例表一致 per SC-010 (missing)
- [X] T100 [MEDIUM] 补齐 `SaveStateValidator` 缺失的不变量校验：`EnsureTimersBackedByStatus` 扩到 6 路（`Ill` 位 ⇔ `illnessSeverityBasisPoints != null`，`treatmentRemainingMonths` 仅在 `Ill` 时非空）；`purchaseCount >= 0`、`purchaseYear >= 1` 且与 `GameDate` 同年或更早、`purchaseCount` 与「本年第 n 名」报价自洽；`birthApplicationYear` 合法；`EnsurePendingBirthsApplied` 增「夫妇 `SpouseId` 双向一致」；新增「无在册男性 ⇒ `HeadId == null`」（004 `data-model.md` §6 不变量 13）per FR-047 (partial)
- [X] T101 [MEDIUM] 在 `tests/KFL.Tests/Infrastructure/SaveStateValidatorTests.cs` 为 validator 第 13 条（提议指向在册未婚成员）与第 14 条（待分娩 ⊆ 已申请、成员在册、夫妇互相指向）补正/反用例，并覆盖 T100 新增的计数与计时项校验 per FR-047 (missing)
- [X] T102 [MEDIUM] 回写上游活工件 `specs/001-core-skeleton/data-model.md`：§1.7 `StatusTimers` 字段表由 4 行补到 6 行（`IllnessSeverityBasisPoints` / `TreatmentRemainingMonths`，含 `:121-122` 不变量文字改为「六个字段」并说明 `Ill` 位配对、来源标 007 FR-047/FR-028），§2.2 `Family` 成员表补 `Lifecycle` 行（`FamilyLifecycleState`，来源 007 §数据模型）；同步在 `tasks.md` 登记本轮回写范围 per FR-050 ② (missing)
- [X] T103 [MEDIUM] 回写 `specs/006-bribery-punishment/spec.md:461-466`：把仍写「属逻辑轨 ⑦」的表述改述为「⑦ 已由 007 交付」，并在 `specs/007-family-lifecycle/tasks.md` 回写登记表 #8 补登该实际落点（现 #8 只写 `contracts/` 与 `data-model.md:386-387`）per FR-050 ⑧ (missing)
- [X] T104 [LOW] 订正四处描述层残句：`科举浮生录规格书.md:718` 标题「Q1~Q6」补为「Q1~Q7」且 `:720` 提及 Q7；`specs/007-family-lifecycle/tasks.md:430` 登记 #1 描述去掉已不存在的「未决项」；`src/KFL.Infrastructure/Save/Serialization/JsonSaveFormat.cs:16` 注释 `FormatVersion:3` 改 4；`specs/004-save-system/data-model.md:403` 的引用行号更新为 `MessagePackSaveFormat.cs:410-418` per FR-050 ⑩ (partial)
- [X] T105 [LOW] 补齐 FR-026 的难度全档：`specs/007-family-lifecycle/spec.md:600-603` 与 `contracts/marriage-birth.md` §3 的夭折率写明「简单 5% / 普通 10% / 困难 11% / 地狱 12%」（与 `科举浮生录规格书.md:439`/`:728` 一致），并订正 `src/KFL.Rules/Config/ChildbirthPolicy.cs` 中「待规格书 §17 裁决」的失真注释 per FR-026 (partial)

## 回写范围登记（2026-10-19，玩家反馈第三轮：族内通婚与离婚）

> 本节由第三轮玩家反馈（反馈 1「允许族内通婚（只能同代），也可以离婚」）**追加登记**，
> 不回改 T001~T105 既有条目，不复用既有编号。
> 授权落点 = 规格书 §9.1 的 2026-10-19 `§17 裁决回写` 块、§17 裁决块 Q4 与 `spec.md` 末尾同名增补。

| 变更 | 同步了哪些文件 |
| --- | --- |
| 族内通婚 `LifecycleEntry.TryMarryWithinFamily(first, second)`（同代、零费用、零随机）；拒绝值 `NotSameGeneration = 11` | `科举浮生录规格书.md` §9.1（裁决块）；[spec.md](./spec.md) 增补 §1；`src/KFL.Rules/Family/{LifecycleEntry,LifecycleOutcome}.cs`；`src/KFL.Presentation/GameSession.cs`（`MarryWithinFamily`）、`ViewModels/MainViewModel.cs`（`endogamy` 动作 + 候选下拉）、`src/KFL.App/MainWindow.xaml` |
| 离婚 `LifecycleEntry.Divorce(member)` → `Family.EndMarriage`（清 `SpouseId`、追加 `FormerSpouseIds`，子女父母引用不变）；拒绝值 `NotSpouse = 12` | 规格书 §9.1（裁决块）；[spec.md](./spec.md) 增补 §2；`LifecycleEntry.cs`、`LifecycleOutcome.cs`；`GameSession.cs`（`Divorce`）、`MainViewModel.cs`（`divorce` 动作）、`MainWindow.xaml` |
| 查询面 `EndogamyCandidates` / `CanMarryWithin` / `CanDivorce`（纯函数） | 规格书 §9.1；[spec.md](./spec.md) 增补 §3；`src/KFL.Rules/Family/LifecycleQueries.cs`；`tests/KFL.Tests/Rules/{LifecycleQueriesTests,MarriageTests}.cs` |
| 界面两入口（族内通婚候选下拉 + 离婚按钮） | 规格书 §12.2；[spec.md](./spec.md) 增补 §4；`src/KFL.App/MainWindow.xaml`、`MainViewModel.cs` |

**门禁实测（2026-10-19）**：0 警告 0 错误 / 1309 通过 0 失败 / 恰六工程 / 无 `*.sln`。
本轮回写**未触碰** `contracts/marriage-birth.md` 与 `contracts/lifecycle.md` 的**随机次序表**
（族内通婚与离婚都是 0 随机、0 支付，不进入既有结算步骤，故次序表不变）。

## 回写范围登记（2026-10-19，玩家反馈第四轮：生命周期簿记的失效清理）

> 本节由第四轮玩家反馈（某档**读不回来**：R-04 第 13 条 / 第 14 条）**追加登记**，
> 不回改 T001~T105 既有条目，不复用既有编号。
> 授权落点 = 规格书 §9.1 的第四轮 `§17 裁决回写` 块、文末裁决块 Q1/Q2 与 `spec.md` 末尾同名增补。

| 变更 | 同步了哪些文件 |
| --- | --- |
| **成婚即清提议**：`Family.Marry` 在写入配偶并 `ApplyAlignment` 后，若 `Lifecycle.Proposal` 指向双方之一则清空 | 规格书 §9.1 第四轮回写块①；[spec.md](./spec.md) 第四轮增补 §1；`src/KFL.Core/Entities/Family.cs`；`tests/KFL.Tests/Rules/MarriageTests.cs` |
| **婚姻终止即清生育簿记**：`Family.EndMarriage` 调 `FamilyLifecycleState.DropBirthCouplesInvolving`（离异/丧偶/难产而亡统一经此）；成婚同样丢弃当事人别处登记的簿记 | 规格书 §9.1 第四轮回写块①；[spec.md](./spec.md) 增补 §1；`src/KFL.Core/Entities/{Family,FamilyLifecycleState}.cs` |
| **月度兜底（⑧-a0）**：`PopulationAdvance` 在既有过期提议清理后追加 `DropNonMutualBirthCouples()` 与 `DropPendingBirthsWithoutApplication()`（0 随机 / 0 金额 / 0 状态变更） | 规格书 §9.1 第四轮回写块②；[spec.md](./spec.md) 增补 §2；`src/KFL.Rules/Family/PopulationAdvance.cs` |
| **读档兜底**：`SaveStateRepair`（004 的 ⑦′ 步）清空历史档的同类不一致 | 规格书 §14 第四轮回写块；004 `spec.md` 第四轮增补；`src/KFL.Infrastructure/Save/Serialization/SaveStateRepair.cs` |

**门禁实测（2026-10-19）**：0 警告 0 错误 / **1314 通过 0 失败** / 恰六工程 / 无 `*.sln`。
本轮回写**未触碰** `contracts/marriage-birth.md` / `contracts/lifecycle.md` 的随机次序表
（⑧-a0 子步 0 随机；写入侧清理发生在既有步骤内部，不新增步骤、不新增随机消费）；
`FamilyLifecycleDto` 键数不变（只**清空**既有字段，不新增字段），故 `SaveFileFormat.Version` 仍为 5。

## 回写范围登记（2026-10-19，玩家反馈第七轮：婚配门槛、婚配方向与成本、教育平衡）

> 本节由第七轮玩家反馈（三条 BUG：无官职不能婚配 / 托媒把全体成员变成婚出 / 无名师学业倒退）**追加登记**，
> 不回改 T001~T105 既有条目，不复用既有编号。
> 授权落点 = 规格书 §4.3、§9.1、§9.2、§9.3、§12.2 的第七轮回写，以及文末新增的
> `### 裁决块：2026-10-19（玩家反馈第七轮：婚配门槛、婚配方向与教育平衡）` 与 `spec.md` 末尾同名增补。

| 变更 | 同步了哪些文件 |
| --- | --- |
| **婚配门槛**：买功名婚姻取消 `HasShiStatus` 与「本人须有功名（`Rank`）」两个前置，仅保留「仅娶入方向」；目标功名 = `SpouseDegreeFor(member)` = `LowerDegreeOf(CurrentDegree) ?? DegreeLevel.JuRen`；价格 = 现任官员 `年俸 × 150%`，无官职者按档位价 `举人 60 贯 / 贡士 180 贯`（再乘支出系数） | 规格书 §9.3 第七轮回写、文末裁决块 Q1/Q2；[spec.md](./spec.md) 第七轮增补 §1；`contracts/marriage-birth.md` §3/§7；`contracts/config-registry.md`（`DegreeMarriageJuRenFeeGuan` / `DegreeMarriageGongShiFeeGuan`）；`data-model.md` 买功名行；`src/KFL.Rules/{Config/MarriagePolicy.cs,Family/LifecycleQueries.cs,Family/LifecycleEntry.cs,Family/LifecycleOutcome.cs}`；`src/KFL.Presentation/Display.cs`；`tests/KFL.Tests/Rules/MarriageTests.DegreePurchaseAndMarriageOut.cs` |
| **拒绝原因改名**：`LifecycleRejectReason.NoLowerDegree` → `DegreeMarriageBrideIntoOnly`（同为 `= 3`，语义从「无更低档功名」变为「仅娶入可买功名」） | `research.md` R-20；`contracts/marriage-birth.md` §8 枚举清单；`src/KFL.Rules/Family/LifecycleOutcome.cs`；`src/KFL.Presentation/Display.cs` |
| **婚配方向与成本**：方向判定仍唯一由性别派生（男 ⇒ 娶入、女 ⇒ 嫁出），但「外嫁」徽记改为按当事人性别取词（男性外来女婿显示「女婿」）；托媒费 5 → **3 贯**（× 支出系数）；聘礼/嫁妆表 ×0.6 = 平民 6/12、士人 12/18、仕族 30/60（均 × 支出系数） | 规格书 §9.1/§9.2 第七轮回写、§12.2 徽记行、文末裁决块 Q3/Q4；[spec.md](./spec.md) 增补 §2/§3；`contracts/marriage-birth.md` §3/§5/§6；`contracts/config-registry.md`；`data-model.md` 徽记与金额行；`plan.md` US1 叙述；`quickstart.md`；`src/KFL.Rules/Config/MarriagePolicy.cs`；`src/KFL.Presentation/{Display.cs,ViewModels/MemberRow.cs,ViewModels/MainViewModel.Detail.cs}`；`tests/KFL.Tests/Rules/{MarriageTests.DegreePurchaseAndMarriageOut,LifecycleQueriesTests}.cs` |
| **教育平衡**：名师费 5 → **2 贯/月**（× 支出系数）；遗忘判定改为「`AgeAt > 16` 且 `Occupation` 为务农/做工/经商（**已指派非读书职业**）」，`Occupation.None`（未指派）与 `Studying` 均不遗忘；读书加成与名师加成不变（+1.5 / +2.25） | 规格书 §4.3 第七轮回写、文末裁决块 Q5/Q6；[spec.md](./spec.md) 增补 §4（FR-027）；`contracts/illness-lifespan.md` 读书加成与遗忘两行；`data-model.md` ⑧-d 行；`plan.md` US3 叙述；`src/KFL.Rules/{Config/GrowthPolicy.cs,Family/GrowthAdvance.cs}`；`tests/KFL.Tests/Rules/FamilyLifecycleTests.cs`（界面侧名师费与月度预览均直接读 `GrowthPolicy.TutorFeeGuanPerMonth`，无硬编码数值） |

**门禁实测（2026-10-19）**：0 警告 0 错误 / **1319 通过 0 失败** / 恰六工程 / 无 `*.sln`。
本轮回写**未触碰** `contracts/marriage-birth.md` 与 `contracts/lifecycle.md` 的随机次序表
（买功名、托媒、成长各步的随机消费次数与次序均不变；仅数值与前置条件变化）；
`FamilyLifecycleDto` 键数不变，`SaveFileFormat.Version` 仍为 5。
