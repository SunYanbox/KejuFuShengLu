---

description: "Task list for 解决方案骨架与家族领域模型"
---

# Tasks: 解决方案骨架与家族领域模型

**Feature**: `001-core-skeleton` | **Branch**: `001-core-skeleton` | **Date**: 2026-10-05

**Input**: 设计文档来自 `/specs/001-core-skeleton/`

**Prerequisites**: [plan.md](./plan.md)、[spec.md](./spec.md)、[research.md](./research.md)、
[data-model.md](./data-model.md)、[contracts/](./contracts/)、[quickstart.md](./quickstart.md)

**Tests**: 本阶段**包含测试任务**。依据：spec FR-014 明令交付自动化架构守卫；
user story 的 Independent Test 与 §16「必测单测清单」中属本阶段的三项（实体不变量、
亲属引用一致性、解耦守卫）均以测试为验收物；quickstart §4/§5 的演练靠 `--filter` 分步执行。

**Organization**: 按 user story 分组，使每个故事可独立实现、独立验证、独立交付。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 可并行（不同文件，无未完成依赖）
- **[Story]**: 该任务所属 user story（US1 / US2 / US3）
- 每个任务都给出精确文件路径

## Path Conventions

仓库根 `E:\ProjectCsharp\Games\KejuFuShengLu`；源码 `src/`、测试 `tests/`（见 plan.md
「Source Code」）。非 UI 三层 TFM 一律 `net10.0`，UI 两层一律 `net10.0-windows`。

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 仓库级构建基线。本阶段所有 TFM 由各 `.csproj` 显式声明。

- [ ] T001 在仓库根 `.gitignore` 追加 `bin/`、`obj/`、`TestResults/`（若已存在同义条目则去重，不重复追加）
- [ ] T002 [P] 创建仓库根 `global.json`：`{"sdk":{"version":"10.0.100","rollForward":"latestFeature"}}`。**MUST NOT** 写具体补丁号，**MUST NOT** 用 `rollForward: disable`（research R-01；章程原则 I v1.1.1）
- [ ] T003 [P] 创建仓库根 `Directory.Build.props`：`TreatWarningsAsErrors=true`、`Nullable=enable`、`ImplicitUsings=enable`、`EnforceCodeStyleInBuild=true`、`AnalysisLevel=latest-Recommended`。**MUST NOT** 在其中设置 `TargetFramework` 或 `TargetFrameworks`（research R-01/R-10：给了默认值会让写错的 TFM 被静默补上，守卫 G-03 即失效）

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 六个工程与唯一解决方案文件。FR-001/FR-002/SC-001 的物理载体，
US1/US2/US3 全部依赖它。

**⚠️ CRITICAL**: 本阶段完成前，任何 user story 都无法开始。

- [ ] T004 [P] 创建 `src/KFL.Core/KFL.Core.csproj`：`TargetFramework=net10.0`，无任何 `ProjectReference`（G-05 的允许边集里 Core 无出边）
- [ ] T005 [P] 创建 `src/KFL.Infrastructure/KFL.Infrastructure.csproj`：`TargetFramework=net10.0`，`ProjectReference` 仅指向 `KFL.Core`
- [ ] T006 [P] 创建 `src/KFL.Rules/KFL.Rules.csproj`：`TargetFramework=net10.0`，`ProjectReference` 指向 `KFL.Core` 与 `KFL.Infrastructure`；并创建空壳 `src/KFL.Rules/Config/GameConfig.cs`（本阶段只建位置与类声明，**MUST NOT** 写入任何规则数值；承载后续平衡常量，见 research R-06）
- [ ] T007 [P] 创建 `src/KFL.Presentation/KFL.Presentation.csproj`：`TargetFramework=net10.0-windows`、`UseWPF=true`、`OutputType=Library`，`ProjectReference` 指向 `KFL.Core`、`KFL.Infrastructure`、`KFL.Rules`；本阶段**不建任何窗口或 ViewModel**（research R-11）
- [ ] T008 [P] 创建 `src/KFL.App/KFL.App.csproj`：`TargetFramework=net10.0-windows`、`UseWPF=true`、`OutputType=WinExe`，`ProjectReference` 指向 `KFL.Core`、`KFL.Infrastructure`、`KFL.Rules`、`KFL.Presentation`；并创建 `src/KFL.App/App.xaml`、`src/KFL.App/App.xaml.cs`、`src/KFL.App/MainWindow.xaml`、`src/KFL.App/MainWindow.xaml.cs`（`StartupUri` 指向 `MainWindow`；空窗口，**MUST NOT** 含任何规则数值或业务逻辑）
- [ ] T009 [P] 创建 `tests/KFL.Tests/KFL.Tests.csproj`：`TargetFramework=net10.0`；`PackageReference` = `xunit` 2.9.x、`xunit.runner.visualstudio` 3.1.x、`Microsoft.NET.Test.Sdk` 17.14.x；`ProjectReference` 指向上述五个工程（G-05 中 `KFL.Tests` 豁免层序，可引用任意工程）
- [ ] T010 创建仓库根 `KejuFuShengLu.slnx`，用**恰好六个** `<Project Path="..." />` 扁平声明（反斜杠路径）：`src\KFL.Core\KFL.Core.csproj`、`src\KFL.Infrastructure\KFL.Infrastructure.csproj`、`src\KFL.Rules\KFL.Rules.csproj`、`src\KFL.Presentation\KFL.Presentation.csproj`、`src\KFL.App\KFL.App.csproj`、`tests\KFL.Tests\KFL.Tests.csproj`。**MUST NOT** 使用 `<TestProject>`——该元素被 `.slnx` 解析器静默忽略（research R-02 实测），会让测试工程悄悄掉出解决方案
- [ ] T011 门禁验证（三项全部通过才算完成本阶段，把实际输出贴进提交信息或回复）：① `dotnet build KejuFuShengLu.slnx` 输出 **0 个警告、0 个错误**；② `dotnet sln KejuFuShengLu.slnx list` **恰好列出六个工程**（含 `tests\KFL.Tests`）；③ `Get-ChildItem -Recurse -Filter *.sln`（排除 `.git`）**无输出**

**Checkpoint**: 解决方案可构建、六工程齐全，user story 实现可以开始

---

## Phase 3: User Story 1 - 每一位家族成员都有完整且可校验的档案 (Priority: P1) 🎯 MVP

**Goal**: 交付规格书 §4.1 / §4.4 的字段全集载体——`KFL.Core` 的枚举、值类型与
`Person`/`Family` 聚合，使档案字段可读可写、取值范围与枚举合法、功名以变迁历史呈现、
辈分与家主可校验、亲属引用双向一致且可遍历。

**Independent Test**: `dotnet test KejuFuShengLu.slnx --filter "FullyQualifiedName~Core"`
全绿——用五类夹具（多代同堂 ≥3 代、有配偶、有子女、娶入配偶、买来的旁系）断言字段全集、
值域边界、功名变迁历史（含降级）、辈分与家主、九状态并存与亲属引用一致性，全程不需要
界面、文件系统或网络。

### Implementation for User Story 1

- [ ] T012 [P] [US1] 在 `src/KFL.Core/Enums/` 创建八个枚举，取值**逐字**取自 data-model §1.3：`Gender.cs`（`Male`, `Female`）；`DegreeLevel.cs`（`BaiShen`, `JuRen`, `GongShi`, `JinShi`）；`ImperialPlacement.cs`（`ZhuangYuan`, `BangYan`, `TanHua`）；`DegreeChangeCause.cs`（`Initial`=开局或买功名婚姻带入, `ExamPass`=科举中式, `PunishmentDemotion`=§7.4 连坐降级, `DebugEdit`=§13.2 控制台改写）；`Occupation.cs`（`None`, `Studying`, `Farming`, `Crafting`, `Trading`）；`Origin.cs`（`Farmer`, `Artisan`, `Merchant`, `Scholar`）；`Difficulty.cs`（`Easy`, `Normal`, `Hard`, `Hell`，次序 简单 < 普通 < 困难 < 地狱）；`StatusFlag.cs`（**必须**带 `[Flags]`，九位：`Ill`, `Famine`, `ServingSentence`, `AwaitingPost`, `ExamBanned`, `PromotionBanned`, `MarriedOut`, `Retired`, `Deceased`）
- [ ] T013 [P] [US1] 创建 `src/KFL.Core/ValueObjects/GameDate.cs`（`readonly record struct`）：`Year`（int）、`Month`（int）。不变量 `Year >= 1`、`1 <= Month <= 12`，**构造即校验**，越界抛 `ArgumentOutOfRangeException`。行为：`CompareTo` 与比较运算符、`ElapsedMonths(GameDate other)` 返回月差、`AgeInYearsAt(GameDate at)` 返回「已满几周岁」（生日当月即计入，§4.3）。**MUST NOT** 含 12/14 等成年年龄或任何规则数值（成年判定属阶段②，research R-07）
- [ ] T014 [P] [US1] 创建 `src/KFL.Core/ValueObjects/PersonId.cs`（`readonly record struct`）：包装 `Guid`，**构造即校验** `Guid.Empty` 不合法（抛 `ArgumentException`）。`Person` 之间的全部引用都用它，杜绝裸传 `Guid`
- [ ] T015 [P] [US1] 创建 `src/KFL.Core/ValueObjects/TalentSet.cs`（`readonly record struct`）：`Agriculture`、`Commerce`、`Officialdom`、`Craft`。不变量：四项各自 `0 <= v <= 100`，构造即校验；整组不可变，**MUST NOT** 提供任何常规写入通道（§4.1「出生即定，终生不可升降」；「仅调试控制台可修改」的落地属阶段⑦）
- [ ] T016 [P] [US1] 创建 `src/KFL.Core/ValueObjects/DegreeRecord.cs`（`readonly record struct`）——**一条功名变迁记录**，四个成员：`Level`（`DegreeLevel`）、`Placement`（`ImperialPlacement?`）、`ChangedAt`（`GameDate`，本次变化的年月）、`Cause`（`DegreeChangeCause`）。不变量：`Placement is not null` ⇒ `Level == DegreeLevel.JinShi`，构造即校验。**MUST NOT** 把 `ChangedAt` 或 `Cause` 设为可选或省略——§4.1 的「变迁历史」要求每一次变化都能被定位（何时）与解释（何故），且 §7.4 的降级必须与中式升迁可区分（research R-14）
- [ ] T017 [P] [US1] 创建 `src/KFL.Core/ValueObjects/OfficialRank.cs`（`readonly record struct`）：`Level`（int），不变量 `1 <= Level <= 18`，构造即校验。**不表示「无官职」**——无官职由 `Person.Rank is null` 表达（FR-009）。**MUST NOT** 写入年俸数值表（72~5100 贯属阶段⑧）
- [ ] T018 [P] [US1] 创建 `src/KFL.Core/ValueObjects/StatusTimers.cs`（`readonly record struct`）：`SentenceRemainingMonths`、`ExamBanRemainingMonths`、`PromotionBanRemainingMonths`，均为 `int?`。不变量：非空值 MUST `>= 0`；三个字段仅在对应状态位为真时可非空（一致性校验由 `Person` 承担，见 T020）。**MUST NOT** 含计时递减逻辑（阶段⑥）
- [ ] T019 [P] [US1] 创建 `src/KFL.Core/Config/AttributeLimits.cs`：实体自不变量的值域常量（天赋/学业/体质下界 0、上界 100）。归 `KFL.Core` 的理由：依赖方向 `Core ← Infrastructure ← Rules` 使 Core **不可能**引用 `KFL.Rules`，放进 `GameConfig` 会让 FR-005/FR-006 退化成只在测试里口头成立（research R-06，所有者已确认）。**MUST NOT** 把政绩上限 100 放进来（§8.2 属阶段⑧）
- [ ] T020 [US1] 创建 `src/KFL.Core/Entities/Person.cs`，字段**逐字**取自 data-model §2.1：`Id`(`PersonId`,不可变)、`Name`(`string`,可变)、`Gender`(不可变)、`Generation`(`int`,>= 0；**开局成员初值 0**（§4.4「辈分自创始者为 0」，据此「买来的旁系 = 家主辈分 + 1」在开局家主治下取 1）；血亲不可变，外来者可由 `Family` 变更一次)、`BirthDate`(`GameDate`,不可变)、`Talents`(`TalentSet`,不可变)、`Study`(`int`,0~100,可变)、`Health`(`int`,0~100,可变)、`Lifespan`(`int`,>= 0 年,**无上界**,不可变——上界来自 §4.2 的天命寿数分布，属阶段⑧，本阶段 MUST NOT 自设上限)、`DegreeHistory`(`IReadOnlyList<DegreeRecord>`,追加式)、`CurrentDegree`/`CurrentPlacement`(派生只读)、`Rank`(`OfficialRank?`,可变)、`Merit`(`int`,>= 0,可变)、`Status`(`StatusFlag`,可变)、`Timers`(`StatusTimers`,可变)、`Occupation`(`Occupation`,可变)、`FatherId`/`MotherId`(`PersonId?`,不可变)、`SpouseId`(`PersonId?`,可变)、`FormerSpouseIds`(`IReadOnlyList<PersonId>`,可变)。实现不变量：① `Talents`/`Study`/`Health` 落在 0~100；② **无写入通道的成员（全集，T024 须逐个反射断言）**：`Id`、`Gender`、`BirthDate`、`Talents`、`Lifespan`、`Generation`、`FatherId`、`MotherId`、`SpouseId`、`FormerSpouseIds`、`DegreeHistory`、`CurrentDegree`、`CurrentPlacement`——只读属性、**无公开 setter**（data-model §2.1 不变量 2）；`Person` 自持的**可写**属性只有八个：`Name`、`Study`、`Health`、`Rank`、`Merit`、`Status`、`Timers`、`Occupation`；③ `FatherId != Id`、`MotherId != Id`、`SpouseId != Id`，`FormerSpouseIds` 不含 `SpouseId` 且不重复；④ `Timers` 与 `Status` 一致（对应状态位为假时该计时字段 MUST 为 `null`）；⑤ 年龄**不落裸字段**，`AgeAt(GameDate)` = `BirthDate.AgeInYearsAt(at)`；⑥ `DegreeHistory` 按 `ChangedAt` **非降序**，既有记录 MUST NOT 被改写或删除，且 **MUST NOT 存在独立的当前功名字段**——`CurrentDegree` = 末条 `Level`（空则 `BaiShen`），`CurrentPlacement` = 末条 `Placement`（空则 `null`）；⑦ `Generation` **不提供公开 setter**：血亲（`FatherId` 或 `MotherId` 至少一方为本家族成员）的辈分一经确定不可变更，外来者（两者皆 `null`）的辈分只能经 `Family` 指定，且 MUST 在其尚无子女时落定。**MUST NOT** 含 `ChildIds`、`IsShiIdentity`、`Origin`（分别由 `Family` 派生、家族级、存档级承载）；**MUST NOT** 含出生时辰、「已亡」时间字段，或任何逐月收支字段（后者归 `GameState` 账本，research R-16）
- [ ] T021 [US1] 创建 `src/KFL.Core/Entities/Family.cs`，成员取自 data-model §2.2：`Name`(`string`,家族姓氏)、`HasShiStatus`(`bool`,「仕身份」= 进士直系血统，与出身独立可并存)、`HeadId`(`PersonId?`,**家主**；`null` = 家族内无在册男性成员)、`Members`(`IReadOnlyCollection<Person>`,含已归档)、`TryGet(PersonId)`、`ChildrenOf(PersonId)`（派生子女引用）、`SpouseOf(PersonId)`、`RegisteredMembers`（**在册** = 非「已亡」且非「外嫁」）、`ArchivedMembers`（已归档 = 已亡 或 外嫁）。实现不变量：① 成员标识唯一，`PersonId` 引用要么指向本家族成员要么为 `null`（**无悬挂引用**，构造与变更时校验）；② 配偶关系双向一致 `a.SpouseId == b.Id` ⇔ `b.SpouseId == a.Id`，且**至多一人**——已有配偶者被指定第二个配偶时 MUST 被拒（§9.1 一夫一妻）；丧偶再婚 MUST 先置空 `SpouseId` 并把前任追加进 `FormerSpouseIds`，既往配偶记录 MUST NOT 被覆盖，且 MUST NOT 改动任何子女的 `FatherId`/`MotherId`；③ 父母引用为向无环，沿父系主轴向上遍历必然终止；④ **只有 `Family` 能改配偶、父母引用、辈分与家主**，`Person` 不提供公开写入通道（章程原则 II：实体不承担跨实体编排）；⑤ `HasShiStatus` 与任何 `Origin` 可任意组合（§10.2）；⑥ `HeadId` 为 `null` 或指向本家族的**在册**成员，**MUST NOT** 指向已亡/外嫁的已归档成员，也 MUST NOT 指向非本家族成员；⑦ 辈分的指定与变更入口唯一在 `Family`（同 ④）——血亲成员的辈分一经确定 MUST NOT 变更，外来者（娶入配偶、买来的旁系）的辈分 MUST 在其尚无子女时落定（§4.4）。`HasShiStatus` 建在家族级而非成员级是 §17 裁决；`HeadId` 进 001 但其**继任判定不进**（由死亡推进触发，属阶段⑧）——两者均 MUST NOT 被改为按人标记或在本阶段实现继任
- [ ] T022 [P] [US1] 创建 `tests/KFL.Tests/Fixtures/FamilyFixtures.cs`：**五类**夹具构造器——多代同堂（≥3 代，含祖辈→父辈→子辈，可向上向下遍历）、有配偶（含一对双向配偶）、有子女（子女的 `FatherId`/`MotherId` 指向正确）、**娶入配偶**（家族内无父母、辈分 = 其配偶辈分）、**买来的旁系**（家族内无父母、辈分 = 家主辈分 + 1、`FatherId` 与 `MotherId` 皆 `null`）。夹具 MUST 只用 `Person`/`Family` 公开 API 构造，**MUST NOT** 走反射绕过不变量校验（否则测的不是真实约束）

### Tests for User Story 1

> 依赖顺序说明：C# 中测试无法在类型不存在时通过编译，故本阶段测试任务排在对应类型之后，
> 而非字面的「先写测试」。红-绿由分步执行体现：先跑 `dotnet test` 看到断言失败/缺失，
> 再补齐实现使其通过。

- [ ] T023 [US1] `tests/KFL.Tests/Core/ValueObjectTests.cs`：值类型与枚举边界——`GameDate` 接受 `Year=1`/`Month=1`/`Month=12`，拒绝 `Year=0` 与 `Month=0`/`Month=13`；`ElapsedMonths` 与 `AgeInYearsAt`（含**生日当月即计入**的边界，例如生于 1 年 1 月者在其 12 岁生日当月 `AgeInYearsAt == 12`）；`PersonId` 拒绝 `Guid.Empty`；`TalentSet` 四项接受 0 与 100、拒绝 `-1` 与 `101`；`DegreeRecord` 允许白身/举人/贡士且 `Placement` 为 `null`、允许进士带名次、拒绝非进士带名次，且四个成员（`Level`/`Placement`/`ChangedAt`/`Cause`）均为必需；`OfficialRank` 接受 L1 与 L18、拒绝 L0 与 L19
- [ ] T024 [US1] `tests/KFL.Tests/Core/PersonTests.cs`：覆盖 US1 AS1 与 FR-004/FR-008/SC-005——用夹具构造成员，逐个断言性别、辈分、四项天赋、学业、体质、天命寿数、功名、官阶、政绩、状态、职业指派**均可读取**；断言四项天赋与学业、体质落在 0~100；断言 `AgeAt` 由出生年月派生（改变 `GameDate` 参数即改变结果，无裸年龄字段）；用反射断言 `Talents`/`Lifespan`/`Gender`/`BirthDate`/`Generation`/`SpouseId`/`FatherId`/`MotherId`/`FormerSpouseIds`/`DegreeHistory`/`CurrentDegree`/`CurrentPlacement`/`Id` **全部不存在公开 setter**（这是 data-model §2.1 不变量 2 的完整清单，不是抽样）；断言 `FatherId`/`MotherId`/`SpouseId` 等于自身 `Id` 时构造被拒。**功名变迁历史专项**（research R-14）：空历史时 `CurrentDegree == BaiShen` 且 `CurrentPlacement is null`；依次追加「举人→贡士→进士·状元」后 `CurrentDegree`/`CurrentPlacement` 严格等于末条；再追加一条 `PunishmentDemotion` 的贡士记录后，`CurrentDegree` 降为贡士且 `CurrentPlacement` 变回 `null`；断言历史按 `ChangedAt` 非降序且既有记录不可改写；断言「空历史」与「举人被降为白身后追加了一条 `BaiShen` 记录」是**两种不同状态**；用反射断言**不存在**独立的当前功名字段
- [ ] T025 [US1] `tests/KFL.Tests/Core/StatusTests.cs`：覆盖 US1 AS3 与 FR-010——九种状态标记可任意组合并存（含「服刑 + 禁考」同时为真且互不覆盖）；`[Flags]` 组合的位运算正确；`Timers` 一致性——状态位为假时对应计时字段为 `null`，为真时可非空且非空值 `>= 0`，负值被拒
- [ ] T026 [US1] `tests/KFL.Tests/Core/FamilyTests.cs`：覆盖 US1 AS2/AS4 与 FR-011/SC-006，以及 spec Edge Cases 中属本阶段的三条（**婚姻一方死亡后存活方再婚**、**开局士出身不计仕身份**、**买来的旁系终身未婚仍可在家族树中定位**）——配偶引用双向一致；子女的 `FatherId`/`MotherId` 指向正确；从任一成员**一次查询**即可定位父母、配偶与子女（含 3 代以上多代同堂夹具，向上可达祖辈、向下可达孙辈）；父母引用无环、无自环；已出嫁（外嫁）与已亡成员的档案**仍存在于 `Members` 且可读**，只是不在 `RegisteredMembers`；`RegisteredMembers` + `ArchivedMembers` 的并集等于 `Members`；`PersonId` 引用指向非本家族成员时构造被拒；`Family` 是配偶变更的唯一入口。**婚姻专项**（FR-011）：为**已有配偶**的成员再指定配偶时被拒（§9.1 一夫一妻）；构造「丧偶 → 再婚」序列——`SpouseId` 置空后前任出现在 `FormerSpouseIds` 中、`FormerSpouseIds` 不含当前 `SpouseId` 且无重复项；再婚后子女的 `FatherId`/`MotherId` **不变**。**出身 × 仕身份专项**（§10.2）：四出身（农/工/商/士）× `HasShiStatus` 两值共八种组合均可构造且互不约束，含「士出身 + `HasShiStatus == false`」（开局士出身不计仕身份）与「任意出身 + `HasShiStatus == true`」。**辈分与家主专项**（research R-15）：**开局成员的辈分 = 0**；血亲成员的辈分 = 父母辈分 + 1 且无写入通道；`HeadId` 可为 `null`；`HeadId` 指向已亡或外嫁的已归档成员时被拒；`HeadId` 指向非本家族成员时被拒；娶入配偶的辈分等于其配偶辈分；买来的旁系辈分 = 家主辈分 + 1（开局家主治下为 1），且其终身未婚时仍可按辈分在家族树中定位；外来者辈分在其**已有子女后**再改被拒

**Checkpoint**: US1 可独立验证——`--filter "FullyQualifiedName~Core"` 全绿，档案可读可校验

---

## Phase 4: User Story 2 - 一条命令即可验证构建、测试与解耦 (Priority: P2)

**Goal**: 交付契约一的可执行形式——`ArchitectureRules` 纯函数 + 文件读取外壳 + 八条守卫
断言（G-01~G-08），并用合成违规输入自证守卫真的会红。

**Independent Test**: `dotnet test KejuFuShengLu.slnx` 全绿（真实仓库零违规）；
`GuardSelfTests` 用六组合成违规输入断言逐条被抓——**反向依赖与平台泄漏的验证物就是这组
合成输入**（真实改写仓库必然连带编译失败，见 T035）；随后按 quickstart §2 手工删掉一行
`<Project>` 验证 G-02，断言守卫失败并报出缺失路径，再还原。

> **注**：本故事的 US2 AS1/AS2（构建零警告零错误、六工程齐全、测试全绿）由 Foundational
> T011 与本节测试共同确立；T011 已完成后，AS1/AS2 的形状不再变化。

### Implementation for User Story 2

- [ ] T027 [P] [US2] 创建 `tests/KFL.Tests/Architecture/ArchitectureRules.cs`：**纯函数**判定逻辑，输入「`.slnx` 文本 + 各 `.csproj` 文本字典 + 待扫描源码文本字典（非 UI 产品工程 + `tests/KFL.Tests` 的领域/接缝/夹具目录，范围见契约一 G-07）」，输出违规列表（含违规编号、工程名、依赖边或文件:行）。**MUST NOT** 触碰文件系统——判定与 IO 分离是 SC-003 可自证的前提（契约一 §1）。逐条实现 G-01~G-08：**G-01** 仓库恰有一个 `KejuFuShengLu.slnx` 且递归无 `*.sln`（排除 `.git`、`bin`、`obj`）；**G-02** `.slnx` 解析出恰好六个 `<Project>`，路径集合等于六个约定路径——判定 MUST 基于**可解析的工程集合**，MUST NOT 按文本中 `Path=` 出现次数计数；**G-03** 非 UI = `net10.0`、UI = `net10.0-windows`，且任何工程不得出现 `TargetFrameworks`；**G-04** 非 UI 工程的 csproj 不得出现 `UseWPF`/`UseWindowsForms` 的真值；**G-05** `ProjectReference` 边集 ⊂ 允许边集且无环（Core(0) ← Infrastructure(1) ← Rules(2) ← Presentation(3)/App(4)；`KFL.Tests` 豁免层序），允许边集逐字取自契约一 §3；**G-06**（**源码级**，由 `ArchitectureRules` 承担）`KFL.Core`/`KFL.Infrastructure`/`KFL.Rules` 的源码文本不出现界面平台命名空间 `System.Windows`、`System.Drawing`、`PresentationCore`、`PresentationFramework`、`WindowsBase`（`using` 指令与全限定名两种形式都算）；**G-06b 不在 `ArchitectureRules` 内**——程序集级证据由 T032 的外壳直接断言，因为纯函数的输入里没有程序集元数据（契约一 §4 末注）；**G-07** 扫描范围按契约一 G-07，禁用 token 清单**逐字取自契约一 §2.1（14 个 token，MUST NOT 在本文件另存副本）**；**G-08** 仓库根存在 `global.json` 且 `sdk.version` 主次版本为 `10.0`、`rollForward` ∈ {`latestFeature`,`latestMinor`,`latestMajor`}
- [ ] T028 [P] [US2] 创建 `tests/KFL.Tests/Architecture/RepositoryLocator.cs`：从 `AppContext.BaseDirectory` 向上逐级查找 `*.slnx` 定位仓库根，**找不到即失败**（不静默返回 null）。读取仓库文件时排除 `.git`、`bin`、`obj` 目录

### Tests for User Story 2

- [ ] T029 [US2] `tests/KFL.Tests/Architecture/SolutionShapeTests.cs`：把真实仓库的 `.slnx` 文本喂给 `ArchitectureRules`，断言 **G-01 / G-02 / G-08** 零违规；用 `dotnet sln list` 的等价语义断言六个工程均在解决方案内（`KFL.Tests` 尤其不能缺席——这是 R-02 的真实陷阱）
- [ ] T030 [US2] `tests/KFL.Tests/Architecture/TargetFrameworkTests.cs`：读六个 csproj 文本，断言 **G-03 / G-04** 零违规——非 UI 三层为 `net10.0`、UI 两层为 `net10.0-windows`、无任何 `TargetFrameworks`、非 UI 工程无 `UseWPF`/`UseWindowsForms` 真值
- [ ] T031 [US2] `tests/KFL.Tests/Architecture/DependencyDirectionTests.cs`：读六个 csproj 的 `ProjectReference`，断言 **G-05** 零违规——边集 ⊂ 允许边集、图中无环、`KFL.Core` 无出边
- [ ] T032 [US2] `tests/KFL.Tests/Architecture/PlatformLeakageTests.cs`：平台泄漏的**两层证据**。① **G-06b（程序集级）**：对 `KFL.Core`/`KFL.Infrastructure`/`KFL.Rules` 各取一个锚点类型，用 `Assembly.GetReferencedAssemblies()` 断言不含 `PresentationCore`、`PresentationFramework`、`WindowsBase`、`System.Drawing*`、`System.Windows.Forms`（比文本扫描更硬：抓的是真实类型引用，含源码里看不见的传递引用）；程序集级证据**不经 `ArchitectureRules`**（纯函数的输入里没有程序集元数据，见契约一 §4 末注）。② **G-06（源码级）**：把真实仓库三工程的源码文本喂给 `ArchitectureRules`，断言零违规——G-06 与 G-06b 是同一违规的两种证据层级，**编号 MUST NOT 混用**（T034 用例③的期望编号固定为 G-06）
- [ ] T033 [US2] `tests/KFL.Tests/Architecture/EnvironmentDependencyTests.cs`：源码级证据，断言 **G-07** 零违规。扫描范围按契约一 G-07 = 非 UI 产品工程（`KFL.Core`/`KFL.Infrastructure`/`KFL.Rules`）**加上** `tests/KFL.Tests/Core`、`tests/KFL.Tests/Infrastructure`、`tests/KFL.Tests/Fixtures`；`tests/KFL.Tests/Architecture/` **显式豁免**（该目录的职责就是读仓库文件，R-13 的收敛在此物化——不把测试侧纳入扫描，FR-015/SC-002 的「领域与规则测试无环境依赖」就没有验证物，`dotnet test` 跑绿并不等于测试自身不碰文件系统）。禁用 token 清单**逐字取自契约一 §2.1（14 个 token）**，本任务 **MUST NOT** 另存清单副本。实现为**词法级子串匹配**，并支持行级豁免注释 `// arch-guard:allow`（豁免行 MUST 在同一行说明理由）。**本任务同时是 US3 AS2 的唯一验证物**，见「User Story Dependencies」
- [ ] T034 [US2] `tests/KFL.Tests/Architecture/GuardSelfTests.cs`：守卫自证，覆盖契约一 §4 全部六用例——① 反向依赖（`KFL.Core` 的 csproj 文本含指向 `KFL.Rules` 的 `ProjectReference`）→ 报 G-05 且信息里出现两个工程名；② 工程级平台泄漏（`KFL.Rules` 带 `<UseWPF>true</UseWPF>`）→ 报 G-04；③ 源码级平台泄漏（`using System.Windows.Media;`）→ 报 **G-06**（该 token 不在契约一 §2.1 的清单里，故 MUST NOT 报成 G-07；期望编号是固定的，不写「或」，以便回归时一眼看出证据层级被弄错）；④ 环境依赖（`var now = DateTime.Now;`）→ 报 G-07；⑤ 测试工程漏登记（`.slnx` 文本只列五个 `<Project>`）→ 报 G-02；⑥ 合法输入（真实仓库内容）→ 零违规。**MUST NOT** 通过真实改写仓库文件来验证
- [ ] T035 [US2] 手工演练验收并记录结果。**唯一可执行的手工演练是 G-02**：临时从 `KejuFuShengLu.slnx` 删掉一行 `<Project>`（例如 `src\KFL.Presentation\KFL.Presentation.csproj`），用 `dotnet test tests\KFL.Tests\KFL.Tests.csproj`（**不经 `.slnx`**，避免被删工程影响测试发现）运行守卫，确认失败信息报出缺失的工程路径；还原后重跑确认全绿，`git status --short` 干净。**反向依赖（G-05）与源码级平台泄漏（G-06）不做手工演练**——本方案的允许边集是全序（Core ← Infrastructure ← Rules ← Presentation / App），任何反向边都闭合成环；`using System.Windows.Media;` 在 `net10.0` 工程里也必然编译失败。二者都会让「守卫失败」与「编译失败」不可区分，所以只能由 T034 的合成输入用例①②③验证。验收记录里 MUST 写明这一点，MUST NOT 把编译失败当作守卫生效的证据

**Checkpoint**: US1 与 US2 均可独立验证——解耦不再靠人工评审，而是构建期硬门禁

---

## Phase 5: User Story 3 - 时间与随机性可注入，建模结果可复现 (Priority: P3)

**Goal**: 交付契约二的两组接缝（`IRandomService`、`IGameClock`）及其实现，以及存档级状态
`GameState`；使「同种子 → 同 UUID、同游戏时间 → 同起始年月」在 001 内即有真实断言对象。

**Independent Test**: `dotnet test KejuFuShengLu.slnx --filter "FullyQualifiedName~Determinism"`
全绿——以固定种子随机来源 + 固定游戏时间基准两次构造同一存档级状态，断言唯一标识与
起始年月完全一致（**AS1，不依赖其他故事**）；**AS2（非 UI 层无系统时钟/全局随机/
文件系统/网络）的验证物是 US2 的 T033（G-07），不在本故事内**——因此 US3 的完整验收
须在 US2 之后，若需在 US2 之前交付 US3，只能先验收 AS1（见「User Story Dependencies」）。

> **分层说明（已裁决，2026-10-05）**：`IRandomService` 定义在 `KFL.Infrastructure`，而
> `KFL.Core` **不能**反向引用它（G-05），因此 `GameState` 的构造函数只接受已生成的
> `Guid`，「16 字节 → UUID」的生成动作必须落在 Core 之外。为使 FR-012 的「唯一标识由
> 注入的 `IRandomService` 生成」存在**产品代码路径**（而不是只有测试在拼字节），T041 在
> `KFL.Infrastructure/Services/` 建 `GameStateFactory`，并已同步进 plan.md 的源码树。
> **MUST NOT** 把该转换逻辑挪进 `KFL.Core`（Core 看不到接缝），也 **MUST NOT** 改用
> `Guid.NewGuid()`（G-07 会拦）。

### Implementation for User Story 3

- [ ] T036 [P] [US3] 创建 `src/KFL.Infrastructure/Abstractions/IRandomService.cs`，签名**逐字**取自契约二 §1：`double NextDouble()`（`[0.0, 1.0)`）、`int Next(int minInclusive, int maxExclusive)`（`[min, max)`）、`void NextBytes(Span<byte> destination)`（填满 `destination`）
- [ ] T037 [P] [US3] 创建 `src/KFL.Infrastructure/Abstractions/IGameClock.cs`，签名**逐字**取自契约二 §2：`GameDate Current { get; }`，语义为**推演出的游戏年月**、不是墙钟时间。**MUST NOT** 同时引入系统时钟抽象（`IClock`/墙钟）——001 无消费者（research R-04）
- [ ] T038 [US3] 创建 `src/KFL.Infrastructure/Services/SeededRandomService.cs`（`SeededRandomService(int seed)`）：契约二 §1 全部条款——同种子逐位相同的调用序列得逐位相同结果；`Next` 在 `maxExclusive <= minInclusive` 时抛 `ArgumentOutOfRangeException` 且返回值落在 `[min, max)`；`NextBytes` 写满整个 `destination` 不得部分填充。**MUST NOT** 使用全局 `Random`、`Random.Shared`、`Guid.NewGuid()` 作为隐式源（G-07 会拦）
- [ ] T039 [US3] 创建 `src/KFL.Core/Entities/GameState.cs`，字段取自 data-model §2.3：`Id`(`Guid`)、`CurrentDate`(`GameDate`)、`Difficulty`(`Difficulty`)、`Origin`(`Origin`)、`Family`(`Family`)。不变量：`Id != Guid.Empty`；`CurrentDate.Month` 合法（由 `GameDate` 保证）；起始值为 1 年 1 月。构造函数**只接受已生成的 `Guid`**，**MUST NOT** 引用 `IRandomService` 或任何 `KFL.Infrastructure` 类型。**MUST NOT** 含资产池、商本、现金/储蓄/贷款、统计容器字段（阶段②及以后，spec Out of Scope）
- [ ] T040 [US3] 创建 `src/KFL.Infrastructure/Services/GameStateClock.cs`：`IGameClock` 的实现，以 `GameState.CurrentDate` 为后端，构造注入 `GameState`。**MUST NOT** 读取 `DateTime.Now` 或任何系统时钟
- [ ] T041 [US3] 创建 `src/KFL.Infrastructure/Services/GameStateFactory.cs`：构造注入 `IRandomService`，暴露 `GameState Create(GameDate date, Difficulty difficulty, Origin origin, Family family)`——从 `NextBytes` 取 16 字节转 `Guid` 作为 `GameState.Id`。**MUST NOT** 使用 `Guid.NewGuid()`。这是本阶段唯一把「随机来源」与「存档级状态」接起来的**产品代码路径**（见上文设计补充）

### Tests for User Story 3

- [ ] T042 [US3] `tests/KFL.Tests/Infrastructure/RandomServiceTests.cs`：接缝契约测试——同一 `seed` 构造两次，连续调用 `NextDouble`/`Next`/`NextBytes` 的序列**逐位相同**；不同种子立即产生不同序列（证明确定性来自注入而非巧合）；`Next` 返回值落在 `[min, max)` 且 `maxExclusive <= minInclusive` 抛 `ArgumentOutOfRangeException`；`NextBytes` 写满整个 `Span<byte>`；`NextDouble` 落在 `[0.0, 1.0)`
- [ ] T043 [US3] `tests/KFL.Tests/Infrastructure/DeterminismTests.cs`（**类名必须含 `Determinism`**，quickstart §4 用 `--filter "FullyQualifiedName~Determinism"` 定位）：覆盖 US3 AS1——注入固定种子随机来源与固定游戏时间基准，通过 `GameStateFactory` **两次创建同一存档级状态**，断言两次的 `Id` 完全一致且均为非空 `Guid`，两次的 `CurrentDate` 均为 1 年 1 月；再以不同种子断言 `Id` 不同。另断言 `GameStateClock.Current` 返回的是 `GameState.CurrentDate`（同输入 → 同结果），并用反射断言 `GameState.Id` **无公开 setter**——FR-012 的「标识 MUST NOT 随存档改名而变化」在 001 内**无可验证对象**（档名与落盘属阶段④），本阶段只能验证「标识不由外部改写」这一形状，MUST NOT 用「同一实例复用即不变」充当该子句的证据（那是同义反复，不可反驳）
- [ ] T044 [US3] `tests/KFL.Tests/Infrastructure/GameStateTests.cs`：`GameState` 字段与边界——`Id` 非空（`Guid.Empty` 被拒）、起始年月为 1 年 1 月、`Difficulty` 四档可读可写（同存档内可随时切换）、`Origin` 四出身可读、`Family` 可读；用反射断言**不存在**资产池/现金/储蓄/贷款/统计容器字段（阶段边界守卫）

**Checkpoint**: 三个 user story 全部可独立验证——档案可校验、解耦被守卫、建模可复现

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: 阶段门禁的最终执行与阶段边界复核。

- [ ] T045 [P] 执行 quickstart §1 的四条门禁命令并归档实际输出：`dotnet build KejuFuShengLu.slnx`（0 警告 0 错误）、`dotnet test KejuFuShengLu.slnx`（全部通过、**0 skipped**）、`dotnet sln KejuFuShengLu.slnx list`（恰好六工程）、递归无 `*.sln`。任一项不达标即回到对应任务修复，**不得**用放宽警告级别或跳过测试来凑门禁
- [ ] T046 [P] 执行 [quickstart.md](./quickstart.md) §4/§5/§6 演练：`dotnet test KejuFuShengLu.slnx --filter "FullyQualifiedName~Determinism"`、`--filter "FullyQualifiedName~Core"` 全绿；`dotnet run --project src\KFL.App\KFL.App.csproj` 打开**空窗口**（这是预期结果而非缺陷——规格书 §16 把主界面排在阶段③）
- [ ] T047 阶段边界复核（逐条对照，发现越界即删）：`src/KFL.Rules/Config/GameConfig.cs` 是空壳且不含任何规则数值；`KFL.Presentation` 无窗口/无 ViewModel；`KFL.App` 无业务逻辑；`KFL.Core` 无资产池字段；`Directory.Build.props` 无 `TargetFramework`；仓库无 `.sln`；`global.json` 无补丁号与 `rollForward: disable`
- [ ] T048 对照 [spec.md](./spec.md) 的 FR-001~FR-015 / SC-001~SC-006 与 [data-model.md](./data-model.md) §4 映射表逐条核验「每条需求均有落地物与验证方式」，把覆盖结论写进提交信息
- [ ] T049 按 Conventional Commits + 中文描述分批提交（`build`：`KejuFuShengLu.slnx`/`global.json`/`Directory.Build.props` 与六工程骨架；`feat`：`src/KFL.Core` 实体与 `src/KFL.Infrastructure` 注入接缝；`test`：`tests/KFL.Tests` 的领域、接缝与架构守卫）。提交前确认 `git status --short` 无残留脏改动

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 无依赖，可立即开始
- **Foundational (Phase 2)**: 依赖 Setup 完成——**阻塞全部 user story**
- **User Stories (Phase 3~5)**: 均依赖 Foundational 完成；此后可并行（若人力允许），
  或按优先级 P1 → P2 → P3 顺序推进
- **Polish (Phase 6)**: 依赖所有目标 user story 完成

### User Story Dependencies

- **US1 (P1)**: Foundational 之后即可开始，不依赖其他故事
- **US2 (P2)**: Foundational 之后即可开始。守卫只读仓库工程文件与源码，**不依赖 US1/US3
  的类型存在**（G-06 的锚点类型取自 `KFL.Core`/`KFL.Infrastructure`/`KFL.Rules`，
  T004~T006 建好工程后其程序集已存在）
- **US3 (P3)**: Foundational 之后即可开始。`GameState`（Core）不依赖 US1 的 `Person`，
  只依赖 `Family` 类型存在——若在 US1 之前执行 T039，需先有一个最小的 `Family` 声明，
  **推荐仍按 P1 → P2 → P3 顺序推进**以避免该耦合。
  **跨故事依赖（已登记）**：`US3 → T033`——US3 的 AS2（非 UI 层无系统时钟/全局随机/
  文件系统/网络）的验证物是 US2 的 G-07 守卫，US3 自身不新建扫描。这是本阶段**唯一**
  的跨故事依赖，登记于此，以免 US3 的「独立验收」被误读为可在 US2 之前**完整**通过
  （AS1 可，AS2 不可）

### Within Each User Story

- 值类型与枚举先于聚合（T012~T019 → T020/T021）
- 夹具先于使用夹具的测试（T022 → T024/T026）
- `ArchitectureRules` 纯函数先于守卫测试外壳（T027 → T029~T033 → T034）
- 接缝接口先于实现（T036/T037 → T038；T039 → T040/T041）
- 每个 user story 完成并通过其 Checkpoint 后，再进入下一个优先级

### Parallel Opportunities

- Setup：T002、T003 可并行（T001 触碰 `.gitignore`，与其他无冲突）
- Foundational：T004~T009 六个工程文件互不重叠，**可全部并行**；T010 依赖它们（slnx 需
  六个路径确定）；T011 依赖 T010
- US1：T012~T019（枚举、六个值类型、常量类）**八个任务可并行**；T020/T021 依赖 T012~T019；
  T022 依赖 T020/T021；T023 依赖 T013~T019，T024/T026 依赖 T020~T022，T025 依赖 T020
- US2：T027、T028 可并行；T029~T034 依赖 T027/T028
- US3：T036、T037 可并行；T038 依赖 T036，T039 依赖 T021，T040/T041 依赖 T037/T036/T039；
  T042 依赖 T038，T043 依赖 T038+T040+T041，T044 依赖 T039
- Polish：T045、T046 可并行

---

## Parallel Example: User Story 1

```text
# 一次性并行启动 US1 的全部值类型与枚举（八个互不重叠的文件）：
Task: "T012 八个枚举 in src/KFL.Core/Enums/"
Task: "T013 GameDate in src/KFL.Core/ValueObjects/GameDate.cs"
Task: "T014 PersonId in src/KFL.Core/ValueObjects/PersonId.cs"
Task: "T015 TalentSet in src/KFL.Core/ValueObjects/TalentSet.cs"
Task: "T016 DegreeRecord in src/KFL.Core/ValueObjects/DegreeRecord.cs"
Task: "T017 OfficialRank in src/KFL.Core/ValueObjects/OfficialRank.cs"
Task: "T018 StatusTimers in src/KFL.Core/ValueObjects/StatusTimers.cs"
Task: "T019 AttributeLimits in src/KFL.Core/Config/AttributeLimits.cs"
```

## Parallel Example: User Story 2

```text
# 守卫的判定逻辑与仓库定位互不依赖，可并行：
Task: "T027 ArchitectureRules 纯函数（G-01~G-08）in tests/KFL.Tests/Architecture/ArchitectureRules.cs"
Task: "T028 RepositoryLocator in tests/KFL.Tests/Architecture/RepositoryLocator.cs"

# 随后六个守卫测试类按断言分组并行：
Task: "T029 G-01/G-02/G-08 in tests/KFL.Tests/Architecture/SolutionShapeTests.cs"
Task: "T030 G-03/G-04 in tests/KFL.Tests/Architecture/TargetFrameworkTests.cs"
Task: "T031 G-05 in tests/KFL.Tests/Architecture/DependencyDirectionTests.cs"
Task: "T032 G-06 in tests/KFL.Tests/Architecture/PlatformLeakageTests.cs"
Task: "T033 G-07 in tests/KFL.Tests/Architecture/EnvironmentDependencyTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. 完成 Phase 1: Setup
2. 完成 Phase 2: Foundational（**关键**——阻塞全部故事；T011 三项门禁必须实测通过）
3. 完成 Phase 3: User Story 1
4. **停下验证**：`dotnet test --filter "FullyQualifiedName~Core"` 全绿，档案可读可校验
5. 此时已可用于演示；US2/US3 完成前**不宣称**解耦与确定性已达标

### Incremental Delivery

1. Setup + Foundational → 骨架可构建（FR-001/FR-002/SC-001 达成）
2. ＋US1 → 档案可校验（MVP，FR-004~FR-011 达成）
3. ＋US2 → 解耦被硬门禁守住（FR-014/SC-003/SC-004 达成）
4. ＋US3 → 建模可复现（FR-012/FR-013/SC-002 达成）
5. ＋Polish → §16 阶段① 验收门禁完整执行，可提交

### 顺序执行的注意点

- 本阶段刻意**不做任何数值**：不写生活费、收入、贷款、科举、存档落盘。任何「顺手把
  §5.1 的档位公式也写了吧」的冲动都越界到阶段②。
- `TreatWarningsAsErrors` 下，任何一次提交都必须保持 0 警告；不要用 `#pragma` 压制，
  用 `// arch-guard:allow` 只豁免守卫扫描，不豁免编译器警告。

---

## Notes

- [P] 任务 = 不同文件、无依赖，可并行
- [Story] 标签用于追溯到 spec.md 的 user story
- 每个 user story 都应可独立完成与验证；完成一个 Checkpoint 后再进入下一个
- 提交粒度：按逻辑组（骨架 / 实体 / 守卫），提交信息用 Conventional Commits + 中文描述
- 避免：模糊任务、同文件冲突、**未登记的**跨故事依赖（本阶段已登记的跨故事依赖只有
  一条：`US3 → T033`，见「User Story Dependencies」）
