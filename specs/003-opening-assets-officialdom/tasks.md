---

description: "Task list for 开局资产与官吏体系（逻辑轨 ③）"
---

# Tasks: 开局资产与官吏体系

**Feature**: `003-opening-assets-officialdom` | **Input**: 设计文档来自 `/specs/003-opening-assets-officialdom/`

**Prerequisites**: [plan.md](./plan.md)（必需）、[spec.md](./spec.md)（必需）、
[research.md](./research.md)、[data-model.md](./data-model.md)、[contracts/](./contracts/)、
[quickstart.md](./quickstart.md)（全部存在且无 `NEEDS CLARIFICATION` 残留）

**Tests**: 本特性**包含测试任务**。依据：spec FR-024 明令交付四出身开局（US1）、待阙与授官（US2）、
考课与政绩（US3）、致仕与半俸（US4）的单测，并复核 §16 必测的俸禄锚点 **72 / 420 / 5100**
在半俸与三态下的取值；SC-001~SC-010 的验证物本身就是测试（32 项开局事实、账本为空、同种子复现、
数值唯一出处扫描）；章程原则 IV「单测优先与结果确定性」；quickstart §3 的 S1~S8 逐条对应一个测试文件。
故测试任务是**验收物**而不是可选项。

**Organization**: 按 user story 分组（US1~US4，优先级 P1~P4），使每个故亊可独立实现、独立验证、
独立交付。Foundational 阶段只放**被多个故亊共享**的 `KFL.Core` 状态、姓名来源接缝与共享夹具。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 可并行（不同文件，且不依赖同一阶段内其它未完成任务的产出）
- **[Story]**: 该任务所属 user story（US1 / US2 / US3 / US4）
- 每个任务都给出精确文件路径；data-model 与契约的字段约束**逐字抄入**任务描述

## Path Conventions

仓库根 `E:\ProjectCsharp\Games\KejuFuShengLu`；源码 `src/`、测试 `tests/`（见 [plan.md](./plan.md)
的「Source Code」）。结构**不变**：仍为六个工程、`.slnx` 唯一、非 UI 三层 `net10.0`、
UI 两层 `net10.0-windows`。新增 **1 个** NuGet 依赖：`Bogus 35.6.1`（仅 `KFL.Infrastructure`）。
`KFL.Presentation` 与 `KFL.App` 在本阶段**一行不动**。

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

**Purpose**: 开工前的仓库级准备——分支与门禁基线、需求真源的裁决回写、跨阶段契约的三处最小修订。
**不含任何生产代码**。

- [X] T001 确认工作分支为 `003-opening-assets-officialdom` 并记录四条门禁基线：依次执行 `dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false`、`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false`、`dotnet sln KejuFuShengLu.slnx list`、`Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }`，把「构建 0 警告 0 错误 / 测试全绿 0 skipped / 恰好六个工程 / 无 `.sln`」四条基线值记入提交信息或 `specs/003-opening-assets-officialdom/implementation-notes.md`（若创建该文件）——后续每一阶段与它对比
- [X] T002 [P] 回写规格书裁决（章程「需求真源」）：在 `科举浮生录规格书.md` 新增 §17 裁决回写四条——§3（前史纪年，Q4：年份接受 `int` 全域、月份仍 1~12、`GameState.CurrentDate` 仍自 1 年 1 月起）、§4.1（功名记录增「甲第」= 一甲/二甲/三甲，名次非空 ⟺ 甲第 = 一甲）、§7.5 与 §8.2（服刑期待阙计时/在职计时/政绩一律暂停，Q5）、§8.2（考课计时口径：授官当月记 1、第 36 个在职月判定、判定后重置）
- [X] T003 [P] 同步 001 活工件：`specs/001-core-skeleton/spec.md`（FR-008 附近）与 `specs/001-core-skeleton/data-model.md`（`DegreeRecord` 小节）各补一句「甲第字段由 `003-opening-assets-officialdom` 做**向后兼容扩展**（第 5 个可选成员），见该特性 FR-014」；**MUST NOT** 回改 001 的历史记录类工件（`tasks.md`、`implementation-notes.md`、既有 `checklists/`，规格书 §16.2 的迁移口径）
- [X] T004 [P] 跨阶段契约三处最小修订：① `specs/001-core-skeleton/contracts/architecture-guard.md` 的 **G-07** 行——确认扫描范围（产品源码三根 + `Core`/`Infrastructure`/`Fixtures`/`Rules` 四个测试目录）已覆盖本特性新增的 `src/KFL.Rules/Start`、`src/KFL.Rules/Career` 与 `src/KFL.Infrastructure/Services`，必要时改标；② `specs/001-core-skeleton/contracts/injection-seams.md` 增 `INameGenerator` 一节（位于 `KFL.Infrastructure`，规则层只依赖抽象）并更新 §1 `IRandomService` 的「消费者」格（新增姓名来源，随机仍只来自 `IRandomService`）；③ `specs/002-monthly-settlement-economy/contracts/config-registry.md` 增「003 新增数值」小节，逐行指向本特性 `contracts/config-registry.md`，**MUST NOT** 复制数值本身

**Checkpoint**: 门禁基线已知、规格书与 001 工件已回写、跨阶段契约已同步——可以开始写生产代码。

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 被 US1~US4 **共享**的地基——`KFL.Core` 的状态字段与不变量、姓名来源接缝、
以及共享测试夹具与数值唯一性扫描清单。

**⚠️ CRITICAL**: 本阶段完成前，任何 user story 都 MUST NOT 开工。

- [X] T005 [P] `KFL.Core`：放宽 `GameDate` 年份下界（Q4 裁决）——在 `src/KFL.Core/ValueObjects/GameDate.cs` 删除 `year < 1` 的构造期校验，年份接受 `int` 全域（含 `0` 与负数 = **前史纪年**）；月份仍 MUST 在 1~12；同步 XML 注释（错误信息只描述月份约束）与类摘要（`GameState.CurrentDate` 仍自 1 年 1 月起推演，本类型不强制）；`ElapsedMonths`/`AgeInYearsAt`/比较运算符**语义不变**
- [X] T006 [P] `KFL.Core`：新增三个枚举——`src/KFL.Core/Enums/ImperialClass.cs`（`FirstClass` / `SecondClass` / `ThirdClass`，取值顺序即甲第高低，**不承载任何级数**）、`src/KFL.Core/Enums/SalaryMode.cs`（`None` / `Active` / `AwaitingPost` / `Retired`，**派生**、不落字段）、`src/KFL.Core/Enums/AppointmentTrack.cs`（`FirstClass` / `SecondClass` / `ThirdClass` / `SpecialTribute`）
- [X] T007 `KFL.Core`：`DegreeRecord` 增甲第字段（FR-014）——在 `src/KFL.Core/ValueObjects/DegreeRecord.cs` 增**可选第 5 参** `ImperialClass? imperialClass = null` 与只读属性 `Class`，并施加四条构造期不变量（违反抛 `ArgumentException`）：① `Class is not null` ⇒ `Level == DegreeLevel.JinShi`；② `Placement is not null` ⇒ `Class == ImperialClass.FirstClass`；③ `Class == ImperialClass.FirstClass` ⇒ `Placement is not null`；④ `Class ∈ {SecondClass, ThirdClass}` ⇒ `Placement is null`。「进士但 `Class == null`」**是合法状态**（不抛异常）。（依赖 T006）
- [X] T008 [P] `KFL.Core`：`StatusTimers` 增第 4 个计时——在 `src/KFL.Core/ValueObjects/StatusTimers.cs` 增 `int? AwaitingPostRemainingMonths`（构造参数保持可选、非空时 MUST `>= 0`，与既有三个字段同款校验），对应状态位 `StatusFlag.AwaitingPost`；同步类注释（**明确不含**递减逻辑与 6~24 区间）
- [X] T009 `KFL.Core`：`Person` 增在职月数与待阙交叉校验（FR-013、FR-015）——在 `src/KFL.Core/Entities/Person.cs` 增可写属性 `int MonthsInOffice`（不变量 `>= 0`，赋负值抛 `ArgumentOutOfRangeException`，默认 0），并把 `Status` / `Timers` 的交叉一致性校验扩展到 `StatusFlag.AwaitingPost`：⑦ 清除 `AwaitingPost` 位前 MUST 先把 `Timers.AwaitingPostRemainingMonths` 置 `null`；该字段非空时 `AwaitingPost` 位 MUST 为真；可写属性由八个变九个（`Name`/`Study`/`Health`/`Rank`/`Merit`/`Status`/`Timers`/`Occupation`/`MonthsInOffice`）。（依赖 T008）
- [X] T010 `KFL.Core`：三处注释改标（research R-18#3，纯注释、不触发断言）——`src/KFL.Core/Config/AttributeLimits.cs` 的「政绩上限属阶段⑧」改为「属**逻辑轨 ③** 的 `OfficialCareerPolicy.MeritMaximum`」；`src/KFL.Core/Entities/Person.cs` 的 `Lifespan`/`Merit` 注释中「上界属阶段⑧」分别改为「属**逻辑轨 ⑦**」与「属**逻辑轨 ③**」。（依赖 T009）
- [X] T011 [P] `KFL.Infrastructure`：新增姓名来源接缝（FR-009）——`src/KFL.Infrastructure/Abstractions/INameGenerator.cs`：`string NextSurname()`（姓氏池随机取一，§12.4「随机姓氏」）与 `string NextGivenName(Gender gender)`（**只取名**；姓由家族姓氏决定）
- [X] T012 `KFL.Infrastructure`：新增内置宋风姓名生成器——`src/KFL.Infrastructure/Services/SongStyleNameGenerator.cs`：构造注入 `IRandomService`，内置宋风姓氏/名字库（**产品默认路径**，规格书 §2 的兜底实现），全部随机 MUST 经注入的 `IRandomService`，MUST NOT 触碰任何全局随机源。（依赖 T011）
- [X] T013 [P] `KFL.Infrastructure`：新增 Bogus 适配器——在 `src/KFL.Infrastructure/KFL.Infrastructure.csproj` 增 `<PackageReference Include="Bogus" Version="35.6.1" />`；新增 `src/KFL.Infrastructure/Services/BogusNameGenerator.cs`：以注入随机取一个整数种子 → `new Randomizer(seed)`（**局部** `Randomizer`），只在该局部实例上取名；**MUST NOT** 赋值 `Bogus.Randomizer.Seed`，MUST NOT 使用 `ExtensionsForRandomizer` 的分布方法。（依赖 T011）
- [X] T013a `KFL.Infrastructure`：新增姓名来源单测 `tests/KFL.Tests/Infrastructure/NameGeneratorTests.cs`（quickstart S6；FR-009、FR-011、SC-008）：对**两个实现**（`SongStyleNameGenerator` 与 `BogusNameGenerator`）各断言——① `NextSurname()` 与 `NextGivenName(Gender.Male)` / `NextGivenName(Gender.Female)` 的返回值**非空**且由汉字组成；② 同一 `SeededRandomService(固定种子)` 下**连续两次构造生成器 → 逐项相同序列**（同种子同结果）；③ 同一种子的**两个实例交替取值互不干扰**（侧证实现 MUST NOT 写全局 `Bogus.Randomizer.Seed`）；④ `SongStyleNameGenerator` 的姓氏落在其内置字库内（白名单断言）。两个实现的所有随机 MUST 经注入的 `IRandomService`；本文件 MUST NOT 出现姓名库以外的规则数值（SC-009 的扫描范围含本目录）；**规则层不引用 Bogus 类型**一条由 T041 的架构守卫覆盖，本文件不重复断言。（依赖 T012、T013）
- [X] T014 [P] 更新 001 的核心值类型断言（受控的工件修订，R-02）：`tests/KFL.Tests/Core/ValueObjectTests.cs` 的 `GameDate拒绝非法年份` 改为「年份接受 `int` 全域（含 0 与负数）、仅月份越界被拒」；`DegreeRecord四个成员均为必需` 改为「五个成员、第 5 个可选」，并新增四条相容性不变量的正例/反例断言（`Class` 非进士被拒、名次非空但甲第非一甲被拒、一甲无名次被拒、二甲/三甲带名次被拒、进士 `Class == null` **合法**）；`StatusTimers` 断言补第 4 字段的负值拒绝与空值接受
- [X] T015 [P] 更新状态位交叉校验断言：`tests/KFL.Tests/Core/StatusTests.cs` 增「清除 `AwaitingPost` 位前 MUST 先清空 `AwaitingPostRemainingMonths`」与「计时非空时状态位 MUST 为真」两条断言（与既有服刑/禁考同款口径）
- [X] T016 [P] 更新成员档案断言：`tests/KFL.Tests/Core/PersonTests.cs` 的 `WritableMembers` 由八个变九个（增 `MonthsInOffice`）并同步 `自持可写属性有公开setter` / `无写入通道的成员没有公开setter` / `不含越界字段` 三条断言；新增 `MonthsInOffice` 下界（赋 `-1` 抛）与默认值 0 的断言；既有的一甲功名构造点补传 `ImperialClass.FirstClass`。（依赖 T009）
- [X] T017 [P] 更新家族聚合断言：`tests/KFL.Tests/Core/FamilyTests.cs` 中的一甲功名构造点补传 `ImperialClass.FirstClass`（其余断言语义不变）
- [X] T018 [P] 更新架构数值清单（SC-009，契约八 §3 条款 3）：在 `tests/KFL.Tests/Architecture/ConfigLiteralTests.cs` 的 `RegisteredValues` 增补 **0.25**（基础升级率）、**0.003**（每点政绩 +0.3%）、**60.7** / **62.3**（天命寿数均值）四项小数；**MUST NOT** 重复登记 0.70（封顶，与米价 clamp 上限同值）与 0.50（半俸，与一般乘区 −0.50 同值）；**整数规则值一律不入单**（6 / 24 / 11 / 13 / 15 / 18 / 36 / 70 / 100 / 12 / 14 / 60 / 23~33 / 0~8 / 80~100 / 90~100 / 10~30），其唯一性由 `EvaluateMoneyLiterals` 判据与配置类注释兜底
- [X] T019 `KFL.Rules` 测试夹具扩展（R-17）：在 `tests/KFL.Tests/Rules/RulesTestHarness.cs` 增 `Official(level, monthsInOffice, merit, …)`（在任官员）、`Awaiting(person, remainingMonths)`（位 + 计时**成对**设置）、`PromotionBannedOfficial(remaining)`（禁升期官员，**只消费**计时、不清算）与 `FixedNameGenerator`（返回固定姓名的 `INameGenerator` 替身，与 `FixedRandomService` 同款）；边界测试用 `FixedRandomService`，开局测试 MUST 用 `SeededRandomService(固定种子)`；本文件 MUST NOT 另存规则数值副本（SC-009 扫描范围含本目录）。（依赖 T009、T011）

**Checkpoint**: 地基就绪——四个 user story 可以开始并行（若有足够人力）；MVP 从 US1 开始。

---

## Phase 3: User Story 1 - 四出身开局：一次创建即有人有产 (Priority: P1) 🎯 MVP

**Goal**: 按规格书 §10.1 的四出身一次性落地家主/配偶/孩子（年龄、性别、辈分、父母与婚姻引用、
四项天赋/学业/体质/天命寿数）、初始资产（现金入现金池、商本入商本池、田宅入资产组合）、
生活费档位与米价系数初值；开局资产是**初始余额**，**不落账本条目**。

**Independent Test**: 固定随机种子与固定姓名来源，对四种出身各创建一份存档，逐项断言成员数、
年龄区间、性别、辈分、家主、婚姻与父母引用、四项天赋/学业/体质/寿数口径、初始资产与资金池、
士家主的那条功名变迁记录，且 `Ledger.Entries` 为空；两次相同输入逐字段相同。全程不需要界面、
文件系统或网络（quickstart S1/S2）。

### Implementation for User Story 1

- [X] T020 [P] [US1] 新增开局表 `src/KFL.Rules/Config/OriginStartTable.cs`（§10.1 的**唯一**声明处）：初始现金 **农 80 / 工 80 / 商 500 / 士 200** 贯；初始田 **农 40** 亩、其余 0；初始农村宅 **农/工/士 1** 座（「农舍」与「农村宅」同价，MUST NOT 出现第三种宅类）；初始城市宅 **商 1**、其余 0；初始商本 **商 300** 贯（**入商本池，不入现金**）；`SpouseCount` 恒 1；`ChildCount` **农 2 / 工 1 / 商 2 / 士 1**；`HeadAgeOffset` **农/工/商 28±5、士 30±5**；`SpouseAge` **25±5**；`ChildAgeRange` **0~8**；`HeadStudy` **士 60（常量）/ 其余 10~30**；`HeadHealthRange` **80~100**；`ChildStudy` **0（常量）**；`ChildHealthRange` **90~100**；`ScholarOriginHasJuRenRecord` 士 = 是。全部区间为**整数均匀、含两端点**（实现取 `Next(min, max + 1)`）
- [X] T021 [P] [US1] 新增属性分布 `src/KFL.Rules/Config/AttributePolicy.cs`（§4.1/§4.2 的唯一声明处）：`TalentMean/Sigma = 60/20`（四项独立）、`StudyMean/Sigma = 30/15`、`HealthMean/Sigma = 85/10`、`LifespanMeanMale/SigmaMale = 60.7/8`、`LifespanMeanFemale/SigmaFemale = 62.3/8`；纯函数 `NextNormal(mean, sigma, IRandomService)` 用 **Box–Muller**、每次取样**恰好消耗 2 次 `NextDouble()`**、**不做**静态缓存；`NextTalent/NextStudy/NextHealth` 取样 → **四舍五入取整** → clamp 到 `AttributeLimits.Min~Max`（0~100）；`NextLifespan` **只取整、clamp 下界 0、不设上限**（FR-006：只保证 `>= 0`，MUST NOT 设**上限**）；**MUST NOT** 使用 Bogus 的分布方法
- [X] T022 [US1] `GameConfig` 增两组只转发视图（契约八 §3 条款 6）：在 `src/KFL.Rules/Config/GameConfig.cs` 增 `NewGame`（转发 `OriginStartTable`）与 `Attributes`（转发 `AttributePolicy` 与 `AttributeLimits` 的 0~100 边界）；**本文件 MUST NOT 出现任何数值字面量**。（依赖 T020、T021）
- [X] T023 [US1] 新增新建存档入口 `src/KFL.Rules/Start/NewGameSetup.cs`（FR-001、FR-010、FR-011）：公开 `readonly record struct NewGameRequest(Origin Origin, Difficulty Difficulty, string? Surname, GameDate StartDate)` 与薄视图 `NewGameSetupResult`（**只持有 `State`**，`HeadId`/`StartDate`/`Members` 均为派生只读属性），以及 `static NewGameSetupResult Create(NewGameRequest request, IRandomService random, INameGenerator names)`；编排严格按契约六 §2：① 姓氏（`Surname` 为 `null` 时取 `names.NextSurname()`，非空时原样使用）→ ② `new Family(surname)`，按**家主（`AddFoundingMember`，男，辈分 0）→ 配偶（`AddOutsider`，女，辈分 0）→ 孩子 1..n（`AddChild`，辈分 1、父母引用同指二人、性别 50/50）** 加入 → ③ `Family.Marry` + `Family.SetHead` → ④ 士出身向家主 `DegreeHistory` 追加 `(DegreeLevel.JuRen, placement: null, changedAt: StartDate, cause: DegreeChangeCause.Initial, imperialClass: null)` → ⑤ 资产**只经构造入参**写入 `Treasury(cash, savings: 0, merchantCapital)` 与 `Holdings`，`Ledger` **为空**，`FamilyEconomy` 的档位取 `LivingCostTable.InitialStandard`、米价系数取 `GrainPricePolicy.Initial`（**MUST NOT** 再写一份初值）→ ⑥ `GameStateFactory.Create(StartDate, difficulty, origin, family, economy)` 取唯一标识；随机消费次序 MUST 为契约六 §3 的**①姓氏 →②家主 →③配偶 →④孩子 i →⑤存档标识**，成员内部次序为 **年龄 → 天赋(农/商/仕/工) → 学业 → 体质 → 寿数 → 姓名**（孩子为 **性别 → 年龄 → 天赋×4 → 学业(常量 0，不掷骰) → 体质 → 寿数 → 姓名**）；`StartDate` MUST 为 1 年 1 月、姓氏为空白串时 MUST 抛异常且**不产生任何部分状态**；任何出身下 `Family.HasShiStatus` MUST 为 `false`；**MUST NOT** 经 `FamilyEconomy.Apply` 写资产。（依赖 T022、T011）
- [X] T024 [US1] 新增开局单测 `tests/KFL.Tests/Rules/NewGameSetupTests.cs`（quickstart S1/S2；SC-001、SC-002、SC-008）：四出身各断言 8 类事实（成员数与性别、年龄区间 **家主 28±5 / 士 30±5、配偶 25±5、孩子 0~8**、辈分 家主与配偶 0 / 孩子 1、家主、婚姻与父母引用、四项天赋/学业/体质/寿数口径、初始资产与资金池、士家主的功名记录），共 32 项；逐格断言 **农 = 现金 80 + 田 40 + 农村宅 1**、**工 = 现金 80 + 农村宅 1**、**商 = 现金 500 + 城市宅 1 + 商本 300（商本池）**、**士 = 现金 200 + 农村宅 1 + 1 条「举人 / Initial」记录**，四者 `HasShiStatus` 全为 `false`；商本不错池的两条**反向验证**（300 贯 MUST NOT 进入「付不起生活费」的可付额，也 MUST NOT 进入工出身 bonus 的资产基数）；开局后 `Ledger.Entries` 为空；`Surname == null` 时经 `INameGenerator` 取随机姓氏另有一条用例；**寿数上下界**两条——`NextLifespan` 只 clamp 下界 0（下界断言）与上界**不** clamp（用 `FixedRandomService` 给出使 Box–Muller 产出大于 `AttributeLimits.Max` 的取值，断言结果 MUST 超过 100 且未被截断）
- [X] T025 [US1] 扩展开局确定性断言 `tests/KFL.Tests/Rules/DeterminismTests.cs`（SC-008）：相同出身、姓氏、难度与种子**连续两次**开局，断言成员数、性别、年龄、姓名、四项天赋、学业、体质、天命寿数、辈分、婚姻与父母引用、资产与资金池、功名记录**逐字段完全相同**（按契约六 §3 的消费次序）

**Checkpoint**: US1 独立可验收（MVP）——四出身开局的产品路径与断言齐备。

---

## Phase 4: User Story 2 - 待阙与授官：及第之后不是立刻当官 (Priority: P2)

**Goal**: 「及第入仕」入口把无官职的进士置入**待阙**（随机 **6~24** 月、无俸），待阙期满的当月
按甲第授官（**一甲 L11 / 二甲 L13 / 三甲 L15 / 特奏名 L18**）；该步位于当月**收入之前**，
故「当月授官 → 当月起领俸」。

**Independent Test**: 造一名带进士功名（含甲第）的成员 → 触发入口 → 断言状态为待阙、剩余月数
落在 6~24（含端点）、当月俸禄为 0、待阙期内不出现 `OfficialSalary` 条目；逐月推进到剩余月数为 0
的当月，断言官阶等于按甲第映射的级数、待阙状态清除、当月起出现俸禄条目；重复触发 MUST 被拒
且不重置剩余月数（quickstart S3）。

### Implementation for User Story 2

- [X] T026 [US2] 新增官吏政策 `src/KFL.Rules/Config/OfficialCareerPolicy.cs`（§8.2 的唯一声明处）：`AwaitingPostMinMonths/MaxMonths = 6/24`（整数均匀、含端点）、`InitialRankOf(AppointmentTrack)` = **一甲 → L11 / 二甲 → L13 / 三甲 → L15 / 特奏名 → L18**、`MeritPerMonth = 1`、`MeritMaximum = 100`、`AppraisalPeriodMonths = 36`、`PromotionBaseChance = 0.25`、`PromotionChancePerMerit = 0.003`、`PromotionChanceCap = 0.70`、`RetirementAge = 70`、`RetirementSalaryRatio = 0.50`、纯函数 `PromotionChance(int merit) = min(0.25 + merit × 0.003, 0.70)`；**明确不含**禁升计时的建立与递减（逻辑轨 ⑥）、官名（规格书未定义）
- [X] T027 [US2] `GameConfig` 增 `Career` 转发组：在 `src/KFL.Rules/Config/GameConfig.cs` 转发 `OfficialCareerPolicy` 的每个成员（**零字面量**）。（依赖 T026）
- [X] T028 [US2] 新增三态判定纯函数 `src/KFL.Rules/Career/SalaryModePolicy.cs`（R-05；FR-021）：`static SalaryMode Of(Person person)`，按**优先级从上到下**判定——`Rank != null && Retired → Retired`；`Rank != null → Active`；`Rank == null && AwaitingPost → AwaitingPost`；否则 `None`；判定 MUST 是**只读**纯函数（不改任何状态）。**MUST NOT** 出现 `Money` / `Origin` / `Difficulty` / `GameDate` 入参，MUST NOT 计算金额或乘区（半俸比例、难度收益系数与「士出身 ×1.05」的单点都在 `IncomeCalculator`，见 T033）；**MUST NOT** 读取 `Family.HasShiStatus`
- [X] T029 [US2] 新增「及第入仕」入口 `src/KFL.Rules/Career/AppointmentEntry.cs`（FR-012、FR-015）：`static void BeginForImperialGraduate(Person person, GameDate date, IRandomService random)`（从 `DegreeHistory` 末条**派生** `AppointmentTrack`：`Class == FirstClass → FirstClass`、`SecondClass → SecondClass`、`ThirdClass → ThirdClass`）与 `static void Begin(Person person, AppointmentTrack track, GameDate date, IRandomService random)`；行为 = 校验 → 掷待阙月数 `random.Next(6, 24 + 1)`（**恰好 1 次 `Next`**，整数均匀、含两端点）→ 置 `AwaitingPostRemainingMonths` 与 `AwaitingPost` 位（**赋值次序：先 `Timers`、后 `Status`**，与 `Person` 的交叉校验方向一致）；拒绝矩阵（全部抛 `InvalidOperationException`，且**校验通过前不产生任何写入**）：`Rank != null`、已在待阙、进士入口而末条不是进士、进士但末条 `Class == null`（**不猜等级**）；**MUST NOT** 写官阶、MUST NOT 写 `MonthsInOffice`、MUST NOT 动账本、MUST NOT 消耗除待阙时长以外的随机
- [X] T030 [US2] 新增仕途月度推进 `src/KFL.Rules/Career/OfficialCareerAdvance.cs`（本阶段只落 **③-a**）与 `src/KFL.Rules/Career/CareerAdvanceResult.cs`：`static CareerAdvanceResult Run(Family family, GameDate month, IRandomService random)`，**成员集合 MUST 取 002 的在册口径（`CountedMembers.Registered`：未亡且未外嫁，含服刑与待阙；已亡与外嫁者 MUST NOT 被推进）**，成员一律按 `PersonId` **升序**处理；③-a = 待阙计时递减 1，**递减到 0 的当月授官**（写 `Rank = OfficialCareerPolicy.InitialRankOf(track)`、`MonthsInOffice = 0`、**先清计时、后清 `AwaitingPost` 位**）；`CareerAdvanceResult` 为**增量快照**（`Appointments`、`MeritGains`、`Promotions`、`Retirements`、`AppraisalSkipped`（因禁升跳过、计时已重置）、`AppraisalPaused`（因服刑暂停、计时未重置）、每人的 `SalaryMode`），MUST NOT 成为与 `Person` 并存的第二真源；`StatusFlag.ServingSentence` 为真者 **③-a 一律暂停**（Q5：不递减、不清位，刑满后从暂停处继续）。（依赖 T029、T028）
- [X] T031 [US2] 在月末结算插入第 ③ 步（FR-019）：`src/KFL.Rules/Settlement/MonthlySettlementEngine.cs` 的调用次序改为 **① 提升待生效值 → ② 米价游走 → ③ 官吏推进（新） → ④ 收入 → ⑤ 生活费 → ⑥ 贷款先计息后划扣 → ⑦ `AdvanceMonth`**；随机消费总次序改为 **米价 → ③-d 的考课掷骰（逐人按 `PersonId` 升序）→ 储蓄利率（仅 1 月）→ 贷款计息利率**；**MUST NOT** 把官吏推进放在收入之后，MUST NOT 为授官/致仕新增账本类别。（依赖 T030）
- [X] T032 [US2] `SettlementResult` 增 `Career` 字段：在 `src/KFL.Rules/Settlement/SettlementResult.cs` 增只读属性 `CareerAdvanceResult Career`（本次结算第 ③ 步的增量），其余字段语义不变；由 `MonthlySettlementEngine` 填充。（依赖 T030、T031）
- [X] T033 [US2] 俸禄改按三态计算（FR-021）：`src/KFL.Rules/Settlement/IncomeCalculator.cs` 的 `AddSalaries` 由「`person.Rank` 非空即发全俸」改为先取 `SalaryModePolicy.Of(person)`，再按三态取**乘区系数**——`Active` = 系数 `1`；`Retired` = 系数 `OfficialCareerPolicy.RetirementSalaryRatio`（月俸 = `SalaryTable.MonthlySalaryGuan(level) × originMultiplier × revenue × 系数`，与 002 既有算式同形）；`AwaitingPost` / `None` **不发**（MUST NOT 落 0 金额条目）。**半俸的 50% MUST 只在本方法里乘一次**——`SalaryModePolicy` MUST NOT 返回已打折的金额（否则 50% → 25%，违反契约七 §5 条款 3）；人群口径仍沿用 002 的「计口成员」（服刑与外嫁已排除）；**MUST NOT** 读取 `HasShiStatus` 定俸禄。（依赖 T028）
- [X] T034 [US2] 更新既有俸禄断言为三态口径：`tests/KFL.Tests/Rules/IncomeTests.cs` 中「有官阶即有俸」的断言改为「在任全俸 / 待阙无条目 / 非官员无条目」；条目类别仍为 `LedgerCategory.OfficialSalary`、归属仍为该成员本人（MUST NOT 落成家族级条目）。（依赖 T033）
- [X] T035 [US2] 新增待阙与授官单测 `tests/KFL.Tests/Rules/AppointmentTests.cs`（quickstart S3；SC-003、SC-004）：待阙期长度 100% 落在 **6~24（含端点）**且「恰好 6」「恰好 24」两端各 1 条独立断言；待阙期内 `OfficialSalary` 条目数为 **0**；推进到剩余月数为 0 的当月，官阶按甲第映射 **L11 / L13 / L15 / L18 四档逐档断言（误差为 0）**、待阙状态与计时清除、**当月**起出现俸禄条目（FR-019）；重复触发「及第入仕」被拒且剩余月数不被重置；「名次是状元但甲第写成二甲」在 `DegreeRecord` 构造期被拒、「进士但甲第缺失」在授官入口被拒；官阶 MUST 始终落在 `SalaryTable.HighestLevel ~ LowestLevel`；**待阙期内被置「已亡」或「外嫁」者 MUST NOT 再递减待阙计时、MUST NOT 被授官**（spec Edge Case，1 条断言）。（依赖 T029、T030、T031、T033）

**Checkpoint**: US1 与 US2 均独立可验收——「开局 → 及第 → 待阙 → 授官 → 领俸」闭环成立。

---

## Phase 5: User Story 3 - 政绩与考课：36 个月一次的升迁 (Priority: P3)

**Goal**: 在任官员每月政绩 **+1**（上限 **100**）；每满 **36 个月**考课一次，升级概率
= **min(25% + 政绩 × 0.3%, 70%)**，成功则官阶级数 −1；禁升期内到期的那次判定**跳过**
（不掷骰、不升迁），但计时照常重置。

**Independent Test**: 造一名在任 L15 官员，逐月结算断言政绩每月 +1 且封顶 100；推进到在职满
36 个月（第 35 个月 MUST NOT 判定），用固定取值随机来源断言概率公式逐点、成功 −1、失败不变、
判定后在职计时重置；对禁升期成员断言该次判定被跳过且**不消耗随机**（quickstart S4）。

### Implementation for User Story 3

- [X] T036 [US3] 扩展月度推进的 ③-b 与 ③-d：在 `src/KFL.Rules/Career/OfficialCareerAdvance.cs` 增 ③-b **政绩 `+1`**（钳制 `<= MeritMaximum`，仅在 `SalaryMode == Active` 时）与 ③-d **在职计时 +1**（授官当月记 1）与**考课判定**（`MonthsInOffice >= AppraisalPeriodMonths` **且 `AgeAt(month) < RetirementAge`** 的当月判定一次，`random.NextDouble() < OfficialCareerPolicy.PromotionChance(merit)` ⇒ 官阶级数 **−1**，`Level == SalaryTable.HighestLevel` 时**维持**、MUST NOT 越界；失败不变；判定后无论成功、失败还是**因禁升被跳过**一律把计时**重置为 0**）；**禁升**（`PromotionBanned`）期内 MUST **不掷骰**（MUST NOT 消耗随机）也不升迁，且 MUST NOT 补判；本特性 MUST NOT 递减、新建或清除 `PromotionBanRemainingMonths`（FR-018）；**服刑者 ③-b/③-d 一律暂停**（Q5：不推进、不重置）；同月 ③-c MUST 先于 ③-d；③-d **自带 `AgeAt(month) < RetirementAge` 闸门**，故本任务**不依赖 US4 的 ③-c** 即可对满 70 岁者产生正确行为（③-c 落地后两闸冗余）；同步填充 `CareerAdvanceResult` 的 `MeritGains` / `Promotions` / `AppraisalSkipped`（因禁升**跳过**、计时已重置）与 `AppraisalPaused`（因服刑**暂停**、计时未重置）——**两者 MUST NOT 合并**。（依赖 T030）
- [X] T037 [US3] 新增政绩与考课单测 `tests/KFL.Tests/Rules/CareerAdvanceTests.cs`（quickstart S4；SC-005、SC-006）：政绩「每月 +1」「上限 100」「非官员 / 待阙者 / 已致仕者不增长」三条各至少 1 条断言；在职第 **35** 个月 MUST NOT 判定、第 **36** 个月判定一次，判定后计时重置；概率逐点断言 `min(25% + 政绩 × 0.3%, 70%)`（用 `FixedRandomService` 的固定取值；`Merit = 100` 时概率 **55%**；另加 1 条**封顶截面**断言——`PromotionChance(200)` MUST 返回 `PromotionChanceCap` = **0.70**，证明 `min(…, 0.70)` 的封顶分支被实现，MUST NOT 以「政绩区间不可达」为由省略）；成功时级数 −1、失败不变、**L1 时成功仍维持 L1**；禁升期成员到期时跳过判定（**不消耗随机**、不升迁、MUST NOT 补判）另有 1 条断言；含「本月政绩 +1 计入本月到期的考课概率」与「同种子两次推进的官阶轨迹/待阙月数/政绩序列完全相同」两条断言。（依赖 T036）

**Checkpoint**: US1~US3 全部独立可验收——仕途拥有「授官 → 政绩 → 考课晋升」的长期节奏。

---

## Phase 6: User Story 4 - 致仕与半俸：体面的收场 (Priority: P4)

**Goal**: 在任官员年满 **70 岁**（生日当月即算）自动致仕：状态置 `Retired`、**官阶保留**，
自当月起按 **半俸 = 全俸 × 50%** 发放至寿终，政绩与在职计时停摆；从未有官职的 70 岁成员
MUST NOT 致仕。

**Independent Test**: 造一名 69 岁 11 个月的在任官员，结算一个月后断言年龄 70、状态 `Retired`、
官阶保留；再结算一个月断言俸禄 = 全俸 × 50%（含难度收益系数与「士出身 ×1.05」），政绩与
在职计时不再变化；同一测试内断言 70 岁无官职者不产生 `Retired`、重复致仕幂等（不得 50% → 25%）、
`Retired` 与患病/禁考等状态位并存；并复核锚点 **72 / 420 / 5100** 在在任与致仕两态下的月俸
（quickstart S5）。

### Implementation for User Story 4

- [X] T038 [US4] 扩展月度推进的 ③-c **致仕判定**：在 `src/KFL.Rules/Career/OfficialCareerAdvance.cs` 中，对 `SalaryMode == Active` 且 `person.AgeAt(month) >= OfficialCareerPolicy.RetirementAge`（**70，生日当月即算**）者置 `StatusFlag.Retired`（**官阶保留**，MUST NOT 清 `Rank`）；已致仕者直接跳过（**幂等**，半俸只在计算时乘一次 50%，MUST NOT 累乘成 25%）；`Rank == null` 者**永不**致仕（无官可致仕）；③-c MUST 位于 ③-d **之前**，故满 70 岁当月的成员 MUST NOT 再接受考课（MUST NOT 出现「同月既升一级、又按升级后的级别计半俸」）；「同月待阙期满且刚满 70 岁」⇒ **先授官、后致仕**，当月按**新授官阶**的半俸计（MUST NOT 出现「有官阶却从未授官」或「先致仕、后授官」）；同步填充 `CareerAdvanceResult.Retirements`。（依赖 T030、T036）
- [X] T039 [US4] 新增致仕单测 `tests/KFL.Tests/Rules/RetirementTests.cs`（quickstart S5；SC-007）：69 岁 11 个月的在任官员结算一个月后年龄 70、状态 `Retired`、官阶保留；「满 70 岁当月即按半俸」「官阶保留」「半俸 = 全俸 × 50%（含难度收益系数与士出身加成）」「此后不再增长政绩、不再推进在职计时」四条各至少 1 条断言；70 岁无官职者不产生 `Retired` 另有 1 条断言；已致仕者再判**幂等**（不得 50% → 25%）；`Retired` 与患病/禁考等状态位**并存互不覆盖**；同月边界两条——待阙期满且满 70 岁（先授官、后致仕，按新官阶的半俸计）与考课成功同月满 70 岁（**不晋升**）；服刑者的在任计时与政绩暂停（Q5）；**满 70 岁当月仍有一次政绩 +1、次月起停止**（FR-019 的 ② 先于 ③，1 条断言）。（依赖 T038）
- [X] T040 [US4] 新增三态俸禄与锚点复核单测 `tests/KFL.Tests/Rules/SalaryModeTests.cs`（quickstart S5；SC-004 + FR-024）：`SalaryModePolicy.Of` 的四条判定（含 `Retired` 优先于 `Active`）各 1 条断言；`Active` / `AwaitingPost` / `None` / `Retired` 四态的月俸条目差异（`AwaitingPost` 与 `None` MUST 不落 0 金额条目）；**锚点三态复核**——L18 / L15 / L1 的月俸在**在任**态等于 `72 / 420 / 5100 贯/年 ÷ 12 × 难度收益系数`，**致仕**态等于上式的一半（锚点字面量断言 MUST 加行级豁免注释 `// arch-guard:allow 锚点即被验证对象`）；**累乘回归**：同一名已致仕成员的月俸 MUST 等于其在任态月俸的 `RetirementSalaryRatio` 倍（MUST NOT 为 0.25 倍）——一条独立断言，防止两个位置各乘一次 50%；三态 MUST 都乘难度收益系数、「出身 = 士」再 ×1.05，且 MUST NOT 读取 `HasShiStatus`。（依赖 T028、T033、T038）

**Checkpoint**: 四个 user story 全部独立可验收——§8.2 的吏治全量（待阙/授官/考课/政绩/致仕/半俸/禁升跳过）落地。

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: 跨故事的验收、边界与门禁。

- [X] T041 [P] 架构与数值唯一性验证（quickstart S7；SC-009、SC-010）：跑 `dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~Architecture"`，确认 G-01~G-08 全绿、G-07 禁用 token 在 `src/KFL.Core`/`src/KFL.Infrastructure`/`src/KFL.Rules` 与四个测试目录（含新增的 `Rules/Start`、`Rules/Career` 与 `tests/KFL.Tests/Infrastructure/NameGeneratorTests.cs`）**零命中**、`ConfigLiteralTests` 的清单已含 **0.25 / 0.003 / 60.7 / 62.3** 且清单数值在 `src/KFL.Rules/Config/` 之外零命中、金额字面量判据零命中（依赖 T013a）
- [X] T042 [P] 边界与「不做」验证（quickstart S8；FR-026）：`git status --short src/KFL.Presentation src/KFL.App` 与 `git diff --stat -- src/KFL.Presentation src/KFL.App` **均无输出**；确认无存档落盘读写、无科举、无惩罚矩阵、无婚育/疾病/死亡判定、无界面改动；`grep -n "0\.25\|0\.003\|L11\|5100" src/KFL.Core` 期望仅注释或无命中（`KFL.Core` MUST NOT 出现考课概率、官职映射、开局资产等规则数值）
- [X] T043 回归门禁全跑（四条）：执行 `dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false`（期望 **0 警告 0 错误**）、`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false`（期望**全绿、0 skipped**）、`dotnet sln KejuFuShengLu.slnx list`（期望**恰好六个工程**——`.slnx` 解析器不认识 `<TestProject>` 且不报错，测试工程可能悄悄掉出，这条不能省）、`Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }`（期望**无输出**）；测试后若 `bin\` 下的 DLL 删不掉，按 `AGENTS.md` 坑 3 处置（`Get-Process -Name testhost | Stop-Process -Force`，**不要去改文件 ACL**）；确认 `tests/KFL.Tests/KFL.Tests.csproj` 的 `_MSTestEnableParentProcessQuery` 仍为**注释态**
- [X] T044 复核历史记录类工件未被回改（规格书 §16.2 的迁移口径）：`git diff --stat -- specs/001-core-skeleton/tasks.md specs/001-core-skeleton/implementation-notes.md specs/001-core-skeleton/checklists specs/002-monthly-settlement-economy/tasks.md` 期望**无输出**（旧编号保留，MUST NOT 回改）；确认 T002/T003/T004 的裁决回写已与代码同批提交（章程「需求真源」：规格书变更 MUST 与受影响工件同步提交）
- [X] T045 按提交口径收口（章程原则 V）：`feat(core)`（`GameDate`/`StatusTimers`/`DegreeRecord`/`Person` 与三个枚举）、`feat(rules)`（配置、开局、仕途、三态俸禄）、`feat(infrastructure)`（姓名来源与 Bogus 依赖）、`test(...)`（新增与更新的断言）、`docs(...)`（规格书 §17 与 001 工件回写）各起一次提交，**不混装**；提交信息用中文

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 无依赖——立即可开工
- **Foundational (Phase 2)**: 依赖 Setup 完成——**阻塞全部 user story**
- **User Stories (Phase 3~6)**: 均依赖 Foundational 完成；三者按优先级顺序推进（P1 → P2 → P3 → P4），
  若有足够人力可在 Foundational 后并行，但 US3/US4 会与 US2 触碰同一个
  `src/KFL.Rules/Career/OfficialCareerAdvance.cs`，故**实际 MUST NOT 并行**
- **Polish (Phase 7)**: 依赖所需的全部 user story 完成

### User Story Dependencies

- **US1（P1，MVP）**: Foundational 之后即可开工——不依赖其它故事；交付后即可独立验收
- **US2（P2）**: Foundational 之后即可开工——提供 ③-a 授官与三态俸禄，是 US3/US4 的前置
- **US3（P3）**: **依赖 US2**（无授官则无在任者、无在职计时）；在 US2 的 `OfficialCareerAdvance.cs` 上续写 ③-b/③-d
- **US4（P4）**: **依赖 US2**（③-c 判定 `SalaryMode == Active` 与 `Rank`）；与 US3 共享同一个推进文件，MUST 在 US3 之后执行以保证月内次序（③-c 先于 ③-d）。US3 **不依赖** ③-c 的存在：③-d 自带「未满致仕年龄」闸门（见 T036），故 US3 阶段对 ≥70 岁成员也不产生考课；US4 落地 ③-c 后两道闸门互为冗余——并非「US3 需要 ③-c 才能正确」

### Within Each User Story

- 配置表 → `GameConfig` 转发 → 规则类型 → 结算引擎接线 → 测试
- 测试 MUST 在实现之后同一批提交（章程原则 IV：测试与实现同批）；本条不采用 TDD 先行，因为
  断言依赖数据类型与配置成员的存在（C# 编译期耦合）
- 故事完成后再进入下一个优先级

### Parallel Opportunities

- Setup 的 T002 / T003 / T004 [P] 可同时开工（三个不同工件）
- Foundational 内的纯新增文件任务可并行：T005 / T006 / T008 / T011 / T014 / T015 / T018
  （`GameDate.cs`、三个枚举、`StatusTimers.cs`、`INameGenerator.cs`、三个测试文件、`ConfigLiteralTests.cs`）
- `T013a`（姓名来源单测）依赖同阶段的 T012 / T013，故 MUST 排在两者之后——不参与上面的并行集
- 同文件任务 MUST **串行**：`Person.cs`（T009 → T010）、`RulesTestHarness.cs`（T019）、
  `GameConfig.cs`（T022 → T027）、`OfficialCareerAdvance.cs`（T030 → T036 → T038）、
  `IncomeCalculator.cs`（T033 → T034）
- US1 内的 T020 / T021 [P] 可同时写（两张配置表、不同文件）

---

## Parallel Example: User Story 1

```text
# 两张配置表并行开写（不同文件、无相互依赖）：
Task: "新增 src/KFL.Rules/Config/OriginStartTable.cs（§10.1 四出身表）"
Task: "新增 src/KFL.Rules/Config/AttributePolicy.cs（§4.1/§4.2 分布与 Box–Muller）"

# 两者完成后串行接线：
#   T022 GameConfig 转发 → T023 NewGameSetup → T024 NewGameSetupTests → T025 DeterminismTests
```

## Parallel Example: Foundational

```text
# 纯新增文件并行开写：
Task: "T005 GameDate 年份下界放宽（src/KFL.Core/ValueObjects/GameDate.cs）"
Task: "T006 三个枚举（src/KFL.Core/Enums/ImperialClass.cs / SalaryMode.cs / AppointmentTrack.cs）"
Task: "T008 StatusTimers 第 4 个计时（src/KFL.Core/ValueObjects/StatusTimers.cs）"
Task: "T011 INameGenerator 抽象（src/KFL.Infrastructure/Abstractions/INameGenerator.cs）"

# 依赖它们的三条随后串行：T007 DegreeRecord → T009 Person → T010 注释改标
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. 完成 Phase 1 Setup（含规格书与 001 工件的裁决回写）
2. 完成 Phase 2 Foundational（**CRITICAL**——阻塞全部故事）
3. 完成 Phase 3 US1（四出身开局）
4. **STOP and VALIDATE**：按 quickstart S1/S2 独立验收 US1（32 项开局事实 + 账本为空 + 同种子复现）
5. 此时 002 的结算引擎已拥有「可结算的对象」，可先行提交/演示

### Incremental Delivery

1. Setup + Foundational → 地基就绪（Core 状态、姓名来源、数值清单）
2. US1 → 独立验收 → 提交/演示（**MVP**）
3. US2 → 独立验收（待阙 6~24、四档授官映射、待阙无俸）→ 提交
4. US3 → 独立验收（政绩 +1/上限 100、36 月考课、禁升跳过）→ 提交
5. US4 → 独立验收（70 岁致仕、半俸、锚点 72/420/5100 三态复核）→ 提交
6. 每个故事只增价值，不破坏前序故事

### Parallel Team Strategy

1. 团队合写 Setup + Foundational
2. Foundational 完成后：一人做 US1，另一人做 US2（触文件不重叠）
3. **US3 与 US4 必须排队**（同一个 `OfficialCareerAdvance.cs`，且 ③-c 先于 ③-d 的次序不可拆）
4. 全部完成后由一人执行 Phase 7 的门禁与边界核对

---

## 附：需求 → 任务对照（落地完整性核对）

| 需求 | 任务 |
| --- | --- |
| FR-001 新建存档入口（纯函数/可注入） | T023 |
| FR-002 四出身初始资产（三池不错池） | T020、T023、T024 |
| FR-003 家族成员构成 | T020、T024 |
| FR-004 年龄与性别 | T020、T023、T024 |
| FR-005 属性初始化 | T021、T023、T024 |
| FR-006 天命寿数（出身无关、只 clamp 下界、不设上限） | T021、T024 |
| FR-007 辈分/家主/婚姻/父母引用 | T023（调用 001 已交付的 `Family` 入口）、T024 |
| FR-008 士出身功名记录 + 仕身份为假 | T023、T024 |
| FR-009 姓名来源抽象 + 确定性实现 | T011、T012、T013、T013a、T019、T023、T024 |
| FR-010 开局不落账本条目 | T023、T024 |
| FR-011 开局确定性 | T023、T025 |
| FR-012 及第入仕入口（不做科举判定） | T029 |
| FR-013 待阙 6~24、无俸、0 时清位 | T008、T026、T029、T030、T033、T035 |
| FR-014 初始官阶映射 + 甲第字段 | T006、T007、T026、T029、T035 |
| FR-015 授官自动发生、幂等/互斥 | T009、T029、T030、T035 |
| FR-016 政绩 +1、上限 100、非官员不增长 | T026、T036、T037 |
| FR-017 36 月考课、概率公式、判定后重置 | T026、T036、T037 |
| FR-018 禁升期跳过判定（只消费） | T036、T037 |
| FR-019 推进位于收入之前 + 月内次序 | T031、T035、T036、T038、T039 |
| FR-020 致仕、半俸、政绩/计时停摆、幂等 | T028、T038、T039 |
| FR-021 俸禄三态 × 难度 × 士出身（与仕身份无关） | T028、T033、T034、T040 |
| FR-022 全部随机经可设种子来源 | T021（Box–Muller 2 次）、T029（1 次 `Next`）、T035、T037、T040 |
| FR-023 数值集中 `KFL.Rules/Config/` | T020、T021、T026、T027 | 
| FR-024 §16 必测项 + 俸禄锚点三态复核 | T024、T035、T037、T039、T040 |
| FR-025 非 UI 层无时钟/全局随机/文件/网络 | T013（Bogus 局部种子）、T041（G-07） |
| FR-026 不做界面 | T042 |
| FR-027 吏治取 §8.2 全量 | T026、T029、T030、T036、T038 |
| SC-001 ~ SC-010 | T024、T025、T035、T037、T039、T040、T041、T042、T043 |

---

## Notes

- [P] 任务 = 不同文件且不依赖同一阶段内其它未完成任务的产出
- [Story] 标签把任务映射到具体 user story，便于追溯；Setup / Foundational / Polish 阶段**不带** [Story]
- 每个 user story 都应能独立完成与独立测试；停止在任何 checkpoint 都可用 quickstart 的 S1~S8 单独验收
- 配置数值**只在** `KFL.Rules/Config/` 声明（SC-009）；测试 MUST 经配置成员取期望值，只有俸禄锚点 **72 / 420 / 5100** 允许字面量并加行级豁免注释
- `KFL.Presentation` / `KFL.App` 一行不动；无存档落盘、无科举、无惩罚矩阵、无婚育疾病死亡、无官名
- 避免：含糊任务、同一文件并发改写、破坏故事独立性的跨故事依赖
