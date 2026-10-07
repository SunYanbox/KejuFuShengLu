---

description: "Task list for 月度结算与经济引擎"
---

# Tasks: 月度结算与经济引擎

**Feature**: `002-monthly-settlement-economy` | **Input**: 设计文档来自 `/specs/002-monthly-settlement-economy/`

**Prerequisites**: [plan.md](./plan.md)（必需）、[spec.md](./spec.md)（必需）、
[research.md](./research.md)、[data-model.md](./data-model.md)、[contracts/](./contracts/)、
[quickstart.md](./quickstart.md)（全部存在且无 `NEEDS CLARIFICATION` 残留）

**Tests**: 本阶段**包含测试任务**。依据：spec FR-026 明令交付规格书 §16 必测单测清单中属
本阶段的三项（贷款先本后息 / 月划扣 20/40/80 / 12 月计息不滚本金、饥馑时间线 4 阶段流转、
俸禄表 18 级锚点 72/420/5100）；SC-002/SC-003/SC-004 要求这些规则**各自有独立断言**；
SC-005/SC-006/SC-008 的验证物本身是测试（账本等式、同种子复跑、数值唯一出处扫描）；
章程原则 IV「单测优先与结果确定性」；quickstart §3 的 S1~S8 逐条对应一个测试文件。
故测试任务是**验收物**而不是可选项。

**Organization**: 按 user story 分组（US1~US5，优先级 P1~P5），使每个故事可独立实现、
独立验证、独立交付。foundational 阶段只放**被全部故事共享**的 Core 经济状态与结算快照形状。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 可并行（不同文件，且**不依赖同一阶段内其他任务的产出**——只依赖前一阶段或本阶段
  更早的独立数据表）。C# 中类型互相引用不影响「可同时动笔」（本阶段结束时统一编译一次），
  但**消费某个计算器/引擎产出的测试任务一律不带 [P]**（与 001 的标记口径一致）
- **[Story]**: 该任务所属 user story（US1 / US2 / US3 / US4 / US5）
- 每个任务都给出精确文件路径；data-model 的字段约束**逐字抄入**任务描述

## Path Conventions

仓库根 `E:\ProjectCsharp\Games\KejuFuShengLu`；源码 `src/`、测试 `tests/`（见
[plan.md](./plan.md) 的「Source Code」）。结构**不变**：仍为六个工程、`.slnx` 唯一、
非 UI 三层 `net10.0`、UI 两层 `net10.0-windows`、零新增 NuGet 依赖。
`KFL.Presentation` 与 `KFL.App` 在本阶段**一行不动**。

> **门禁命令**（本阶段每一次验证都用它们，受限宿主的两个坑见 `AGENTS.md`）：
> ```powershell
> dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false     # 0 警告 0 错误
> dotnet test  KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false
> dotnet sln   KejuFuShengLu.slnx list                      # 恰好六个工程
> Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }   # 期望无输出
> ```

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 开工前的仓库级准备——分支口径对齐（已执行）、继承契约的两处最小修订、守卫扫描
范围扩容、开局档位裁决的回写（已执行）、基线实测。**不含任何生产代码**。

- [X] T001 [P] 对齐分支口径 → **已执行（2026-10-06）**：按 plan.md 的登记建 `002-monthly-settlement-economy` 分支并切过去（`git checkout -b`；基线提交仍为 `83c215a`）。plan.md 的 `Branch` 字段与实际分支现已一致，无需改文档。
- [X] T002 [P] 扩容 G-07 扫描范围 → **已完成**：`ArchitectureRules.ScannedTestRoots` 追加 `tests\KFL.Tests\Rules\`；同步修订契约一 `architecture-guard.md` 的 **G-07** 行（扫描范围真源）与 001 `research.md` **R-13**（登记扩至 `Rules`，指回 002 R-14）。**未**另存禁用 token 清单——唯一真源仍是契约一 §2.1（14 个 token）。理由：新目录若不纳入扫描，「规则测试无环境依赖」这半条约束对它**静默失效**
- [X] T003 为新增扫描目录补「扫描非空洞」证据 → **已完成**：既有 `[Theory] 测试目录内的禁用token报G07` 增加一行 `InlineData(@"tests\KFL.Tests\Rules\EnvironmentLeak.cs")`（该 Theory 本就是「每个被扫描测试目录各注入一次」的形状，故按扩行而非新建 Theory 处理）；对照面 `架构目录内的同一注入不报G07` 已存在，二者合起来证明该目录边界是被**判定**的而非恒不命中（依赖 T002）
- [X] T004 [P] 继承契约的第二处最小修订 → **已完成**：`injection-seams.md` §2 `IGameClock` 的「注入」条款已补「月度结算引擎（002）按构造函数注入 `IGameClock` 与 `IRandomService`；MUST NOT 把 `GameState.CurrentDate` 直接当作『现在』（该值由 `IGameClock` 承载），也 MUST NOT 用静态全局」（002 plan.md「继承契约的两处最小修订」之一）
- [X] T005 [P] 开局生活费档位初值（真源缺口）→ **已裁决并回写（2026-10-06）**：规格书 §5.1 与 spec.md 原先均**未规定**该初值（§5.1 只写「家族级随时切换，次月生效」）。用户裁决为**普通**档（`Normal`）。回写位置：`科举浮生录规格书.md` §5.1（新增「开局档位」条 + §17 裁决回写第 5 条）、[spec.md](./spec.md) FR-005 与 Assumptions、[data-model.md](./data-model.md) §3.6/§4.1/§5、[research.md](./research.md) R-09、[plan.md](./plan.md) 的 **E-13** 行。实现约束：取值**单点**声明在 `LivingCostTable.InitialStandard`；`FamilyEconomy` 构造仍要求**显式传入**档位、不设默认值——`KFL.Infrastructure` 看不到 `KFL.Rules`（G-05），在 `KFL.Core` 里抄一份「普通」会造出第二个出处（SC-008）
- [X] T006 阶段② 起点基线 → **已实测（2026-10-06，四条门禁全过）**：① `dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false` → **0 警告 0 错误**；② `dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false` → **通过 171 / 失败 0 / 跳过 0 / 总计 171**（001 交付基线 170，本阶段 Setup 的 T003 新增 1 行 `[Theory]` 数据 → 171）；③ `dotnet sln list` → **恰六个工程**；④ 递归扫描 `*.sln`（排除 `.git`）→ **无输出**。后续按「171 + 新增测试」对比（依赖 T002、T003）

**Checkpoint**: 守卫扫描范围已扩容（含 `Rules\` 的非空洞负向对照）、继承契约已同步、开局档位裁决
已回写（T001/T005 已执行）、基线已实测（171 项全绿）——可以开始写生产代码

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 被**全部五个 user story 共享**的经济状态与结算快照形状——`KFL.Core` 的三个值类型、
七个枚举、六个实体与聚合，以及 `GameState` 的构造变更、共享配置表与测试夹具。

**⚠️ CRITICAL**: 本阶段完成前，任何 user story 都无法开始。理由是结构性的：FR-021 要求
「一切资金流动 MUST 落条目」，所以每个故事的每一笔钱都必须经 `FamilyEconomy` 的同一入口；
US1 的收入/生活费、US2 的划扣、US3 的储蓄利息、US4 的阶段迁移事件、US5 的聚合查询，全部
建立在本阶段的类型之上。

- [X] T007 [P] 创建 `src/KFL.Core/ValueObjects/Money.cs`（`readonly record struct`，data-model §1.1）：内部**唯一**存储 `decimal Wen`（以**文**为单位，章程「技术栈与工程约束」）；静态 `Zero`、具名入口 `FromWen(decimal)` / `FromGuan(decimal)`（= `FromWen(guan × 1000)`）、派生 `Guan`（= `Wen / 1000`，仅用于展示与断言）、派生 `IsPositive` / `IsNegative`；运算 `+ - * (decimal) 一元 -` 与 `== < >`。**不变量**：`Wen` 为有限 `decimal`（`NaN` / `Infinity` 被拒）。**明确不含**：任何舍入（R-02：一律保留 `decimal` 全精度，取整属阶段③展示层）、`double`/`int` 隐式转换（把「贯」「文」的口径混淆挡在编译期）。**MUST NOT** 提供裸 `new Money(500)` 式入口
- [X] T008 [P] 创建 `src/KFL.Core/ValueObjects/GrainPriceIndex.cs`（`readonly record struct`，data-model §1.2）：唯一成员 `decimal Value` = **米价系数**（初值 **1.0 不在 Core**——单点在 `KFL.Rules/Config/GrainPricePolicy`，理由同 T005 的 SC-008 单点，见 data-model §1.2/§4.1）。**不变量**：`Value > 0`。**明确不含**：初始值 1.0、clamp 区间 `0.7~3.0`、游走幅度 `±10%`、米价派生系数 `0.4`/`0.6`——全部属 `KFL.Rules/Config/GrainPricePolicy`（R-01/R-10；SC-008 要求数值只有一处出处）
- [X] T009 [P] 在 `src/KFL.Core/Enums/` 创建六个枚举，取值**逐字**取自 data-model §2：`LivingStandard.cs`（`Frugal` 拮据 / `Normal` 普通 / `Comfortable` 体面）、`AgeBracket.cs`（`Child` 儿童 / `Youth` 青年 / `Adult` 成人 / `Elder` 老人）、`FamineStage.cs`（`None` 无 / `Famine` 饥馑 / `Relief` 救济 / `Severe` 第三阶段）、`AssetKind.cs`（`Farmland` 田 / `RuralHouse` 农村宅 / `UrbanHouse` 城市宅 / `Shop` 铺面）、`LedgerEntryKind.cs`（`Treasury` 资金类 / `Event` 事件类，E-08）、`LedgerCreditTarget.cs`（`Cash` / `Savings`，§5.4）
- [X] T010 [P] 创建 `src/KFL.Core/Enums/LedgerCategory.cs` + `src/KFL.Core/Enums/LedgerCategoryMetadata.cs`（data-model §2.1）：`LedgerCategory` = 19 个取值，逐行取自 data-model §2.1 的表——`LivingCost`（生活费，资金类，恒为负）、`SelfFarmingIncome`、`LandRentIncome`、`FarmingWageIncome`、`CraftingIncome`、`TradeIncome`、`OfficialSalary`、`ShopRentIncome`、`ArtisanBonus`、`SavingsInterest`（资金类，正额入**储蓄**）、`LoanPrincipalRepaid`、`LoanInterestRepaid`、`AssetPurchase`、`AssetSale`、`LoanInterestAccrued`（**事件类**）、`FamineEntered` / `FamineReliefEntered` / `FamineSevereEntered` / `FamineResolved`（**事件类**）。`LedgerCategoryMetadata` 提供 `KindOf(category) → LedgerEntryKind` 与 `CreditTargetOf(category) → LedgerCreditTarget?`——返回 `null` 表示**没有正额入账目标**（data-model §2.1 里「正额入账目标」列为「—」的 9 个类别：4 个恒为负的支出类 + 5 个事件类；`Apply` 收到这 9 类之一的正额时抛异常）（**结构事实**，不是平衡数值，故随枚举留在 Core，R-04）；SC-005 的求和口径 = 对 `KindOf == Treasury` 的条目求和。**MUST NOT** 给 `Occupation` 新增「自耕」「务农」等取值——收入来源是**账本类别**不是职业（E-02）；**MUST NOT** 出现买人口、聘礼/嫁妆、贿赂等阶段⑤⑥⑧ 的类别（FR-028；data-model §2 的 §17 裁决）
- [X] T011 [P] 创建 `src/KFL.Core/ValueObjects/LedgerEntry.cs`（`readonly record struct`，data-model §1.3）：`GameDate Date`、`PersonId? PersonId`（`null` = **家族级**：铺面租、储蓄利息、工 bonus、田租）、`LedgerCategory Category`、`Money Amount`。**不变量**：**资金类**条目的 `Amount` MUST `!= 0`；`Amount` 是**变动量**（正 = 流入、负 = 流出），MUST NOT 被解释成余额；条目不存「资金类/事件类」的第二个副本——种类由 `LedgerCategoryMetadata` 唯一决定
- [X] T012 [P] 创建 `src/KFL.Core/Entities/Loan.cs` + `LoanRepayment`（data-model §3.2）：成员 `Principal`（≥ 0）、`AccruedInterest`（独立「欠息」字段，≥ 0）、`MonthsSinceInterest`（距上次计息的**自然月**数，≥ 0，E-07）、派生 `IsSettled`（本金与欠息皆 0）、派生 `Total`（本金 + 欠息）。方法：`AccrueInterest(Money)` = `AccruedInterest += interest` 且 **MUST NOT** 改 `Principal`（§5.4「利息永不滚入本金」）、`ResetInterestClock()`、`LoanRepayment Repay(Money amount)`（**先本后息**，返回 `(PrincipalPart, InterestPart)`；`amount` MUST `<= Total` 且 `>= 0`）。`LoanRepayment` 为 `readonly record struct (Money PrincipalPart, Money InterestPart)`，不变量：两部分皆 `>= 0` 且分别不超过还款前的本金与欠息。「先本后息」留在实体里是因为它是**单实体的自身不变量**，不涉及跨实体编排（章程原则 II）
- [X] T013 [P] 创建 `src/KFL.Core/Entities/Holdings.cs`（data-model §3.3）：`int FarmlandMu`（田，≥ 0）、`int RuralHouses`（农村宅，≥ 0）、`int UrbanHouses`（城市宅，≥ 0）、`int Shops`（铺面，≥ 0）；方法 `int CountOf(AssetKind)`、`void Add(AssetKind, int count)`、`void Remove(AssetKind, int count)`（移除至负数 MUST 抛异常）。**明确不含**：价格、市值、农田上限——单价与「每成人 20 亩」都在 `KFL.Rules/Config`（R-01）
- [X] T014 [P] 创建 `src/KFL.Core/Entities/FamineState.cs`（data-model §3.5）：`FamineStage Stage`、`int ElapsedMonths`（**本阶段**已持续月数，转入当月记 1）。**不变量**：`Stage == None` ⇔ `ElapsedMonths == 0`。方法：`TransitionTo(FamineStage)`（置阶段并把计时置 1）、`Tick()`（阶段非 `None` 时 +1）、`Clear()`（归 `None`/0）。**明确不含**：3 个月与 12 个月的阈值、救济折扣 20%、体质下降与死亡判定（阈值与折扣在 `KFL.Rules/Config/FamineTimeline`；体质与死亡属阶段⑧，FR-019）
- [X] T015 [P] 创建 `src/KFL.Core/Entities/Treasury.cs`（data-model §3.1）：`Cash`、`Savings`、`MerchantCapital`（三者均**经方法**变动）、`Loan`、`decimal? SavingsRate`（当年储蓄利率）、`int? SavingsRateYear`。**不变量**：三个金额 MUST `>= 0`；`SavingsRate` 非空时 MUST 与 `SavingsRateYear` 同时非空。方法（**内部转账，均不落条目、无手续费**，R-05）：`TransferToSavings(Money)`、`TransferToCash(Money)`、`InjectMerchantCapital(Money)`、`WithdrawMerchantCapital(Money)`——金额超过来源池时 MUST 抛异常（不允许隐式透支）。利率取值区间 `0.5%~2.4%` 的校验**在 Rules**（区间是规格书数值，Core 不复制）
- [X] T016 [P] 创建 `src/KFL.Core/Entities/Ledger.cs`——**本任务只交付存储与追加入口**（聚合查询属 US5，见 T054）：`IReadOnlyList<LedgerEntry> Entries`（只读视图，按追加顺序、年月非降序）、`void Append(LedgerEntry)`（**唯一**追加入口，既有条目 MUST NOT 被改写或删除）、`IReadOnlyList<LedgerEntry> EntriesIn(GameDate from, GameDate to)`（闭区间）。**不变量**：MUST NOT 存储任何月度汇总值（FR-021、SC-008）；聚合每次从条目算
- [X] T017 创建 `src/KFL.Core/Entities/FamilyEconomy.cs`（data-model §3.6）——**资金流动的唯一入口**。构造签名：`FamilyEconomy(LivingStandard livingStandard, GrainPriceIndex grainPriceIndex, Treasury? treasury = null, Holdings? holdings = null, Ledger? ledger = null)`——`LivingStandard`（当前**生效**档位）与 `GrainPriceIndex`（米价系数，可变结构体属性）**同为构造必需参数、无默认值**（见 T005/T008：两者的初值单点都在 `KFL.Rules/Config`），`Treasury` / `Holdings` / `Ledger` 可省（`null` = 空池 / 零资产 / 空账，均不含规则数值）。其余成员：`LivingStandard? PendingLivingStandard`、`FamineState`、派生 `Money TreasuryPool`（= 现金 + 储蓄 + 商本；SC-005 的右侧口径，R-05）。方法：`LedgerEntry Apply(LedgerCategory, PersonId?, Money amount, GameDate)`（追加条目**并在同一次调用内**按 `LedgerCategoryMetadata` 改动资金池：正额入 `CreditTargetOf(category)`；负额按「现金 → 储蓄」扣付；资金不足 MUST 抛异常——E-06 要求调用方先算可付额）、`LoanRepayment RepayLoan(Money, GameDate)`（扣付资金池 + `Loan.Repay` + 落 1~2 条资金类条目，金额为 0 的部分不落条目）、`Money AccrueLoanInterest(Money interest, GameDate)`（`Loan.AccrueInterest` + 落一条**事件类** `LoanInterestAccrued`）、`void EnterFamineStage(FamineStage, Money, GameDate)`（落对应的饥馑阶段迁移事件条目）。**不变量**：① 资金池不为负（`Cash`/`Savings`/`MerchantCapital` 任一时点 `>= 0`）；② **资金流动必落条目**——「只动资金池而不落条目」在类型层面不可表达；③ `PendingLivingStandard` 仅由切换入口设置，结算第①步提升后置 `null`。**明确不含**：年龄档归属（依赖 12/14/18/60，属 Rules，R-06）（依赖 T010~T016）
- [X] T018 修改 `src/KFL.Core/Entities/GameState.cs`（data-model §3.7）：新增只读 `Economy`（`FamilyEconomy`）、新增 `Difficulty? PendingDifficulty`（难度「次月生效」的待生效位，§11/R-09）、新增 `public void AdvanceMonth()`（推进一个月、跨年进位）、构造函数新增 `economy` 参数（共 6 参）。`CurrentDate` **仍无 public setter**——推进只能经 `AdvanceMonth()`，保持 §3「1 回合 = 1 游戏月」的语义不可绕过（R-03）（依赖 T017）
- [X] T019 创建 `tests/KFL.Tests/Fixtures/EconomyFixtures.cs`（R-16）：可复现的经济夹具——多代同堂（含儿童/青年/成人/老人四档）+ 田 + 农村宅 + 城市宅 + 铺面 + 一名在职位成员 + 可注入的贷款（本金/欠息/距上次计息月数）、储蓄（含储蓄利率）、商本、米价系数、饥荒阶段、生活费档位；**全部接受显式初值或固定种子**，MUST NOT 依赖环境。夹具 MUST 只用公开 API 构造（MUST NOT 走反射绕过不变量校验）（依赖 T017、T018）
- [X] T020 更新既有调用点以适配 6 参构造（R-13）：`src/KFL.Infrastructure/Services/GameStateFactory.cs:45`（`Create` 新增 `FamilyEconomy economy` 参数并透传；`Create` **不自行决定**新建存档的初始档位——该值单点在 `KFL.Rules/Config/LivingCostTable.InitialStandard`，而 `KFL.Infrastructure` 看不到 `KFL.Rules`（G-05），故由调用方传入，见 T005）、`tests/KFL.Tests/Infrastructure/GameStateTests.cs:18`、`:60`、`:101`、`tests/KFL.Tests/Infrastructure/DeterminismTests.cs:100-101`。验收：`dotnet build` 后**无一处遗留编译错误**（依赖 T018、T019）
- [X] T021 修订 `tests/KFL.Tests/Infrastructure/GameStateTests.cs` 中两条**阶段① 的边界守卫**，使其在阶段② 有依据地放宽：① `字段全集恰为五个`（当前第 86 行）→ 改为新的公开属性全集（`Id`、`CurrentDate`、`Difficulty`、`Origin`、`Family`、`Economy`、`PendingDifficulty`）；② `不存在资产现金储蓄贷款与统计容器字段`（当前第 65 行）→ 改为「`GameState` **自身**不直接暴露 `Cash`/`Savings`/`Loan`/`Ledger`/`Money`/`Capital`/`Pool`/`Asset`/`Statistics`，这些一律只在 `Economy` 聚合之下；同时断言 `Economy` 存在且非空」。**MUST NOT** 直接删除这两条而不留替代断言——阶段① 的边界是被**修订**不是被遗忘，修订理由要写进提交信息（依赖 T018、T020）
- [X] T022 [P] 创建 `src/KFL.Rules/Config/InterestPolicy.cs`（data-model §4.1）：储蓄与贷款的利率区间 **0.5%~2.4%**、计息周期 **12 月**（§5.4）；区间端点供断言（`NextDouble()` 取 0 与极大值）。放共享阶段的理由：US2（贷款计息）与 US3（储蓄利率）**共用**同一张利率表，任一故事独占都会让另一故事出现跨故事结论依赖
- [X] T023 创建 `src/KFL.Rules/Settlement/SettlementResult.cs` + `LivingCostLine` + `IncomeLine`（data-model §4.2，**纯数据容器、无逻辑**）：`Month`（结算后 `CurrentDate` 已是下月）、`GrainPriceIndexBefore`/`After`、`LivingCosts`（逐年龄档的日耗、人数与小计，US1 AS1）、`LivingCostPayable`/`LivingCostPaid`（E-06）、`Incomes`（类别 + 归属成员（可空）+ 金额）、`NetProfit`（= 收入合计 − 生活费支出，E-04）、`LoanInterestAccrued`、`LoanRepayment`、`SavingsInterest`/`ArtisanBonus`、`FamineBefore`/`After`、`Entries`（本次结算产生的**全部**条目，含事件类）、`TreasuryPoolBefore`/`After`。**不变量**：`Entries` MUST 与本次向 `Ledger` 追加的条目**逐条相同**；`TreasuryPoolAfter − Before` MUST 等于 `Entries` 中**资金类**条目之和（**这条不变量就是 SC-005**）；MUST NOT 被写回 `GameState`（资金池是唯一余额真源）
- [X] T024 `tests/KFL.Tests/Core/MoneyTests.cs`：`Money` 的边界与口径——`FromWen`/`FromGuan` 换算（1 贯 = 1000 文）、`Guan` 派生、`Zero`、四则与比较运算、`NaN`/`Infinity` 被拒、**不存在 `double`/`int` 隐式转换**（编译期口径守卫，用反射断言无 `op_Implicit`）、**不内部舍入**（如 `FromGuan(1) * (1m/3m)` 保留全精度，与「先取整再比较」的期望不同）
- [X] T025 `tests/KFL.Tests/Core/LedgerInvariantTests.cs`：`LedgerEntry`（资金类 `Amount != 0` 被强制）、`LedgerCategoryMetadata`（19 个类别的种类与入账目标逐行断言；`KindOf(category)` 全覆盖无遗漏；**无**买人口/聘礼/贿赂类别）、`Ledger.Append` 追加式（既有条目与顺序不变）、`FamilyEconomy.Apply` 的三条不变量——正额入对池（含 `SavingsInterest` 入**储蓄**）、负额按「现金 → 储蓄」扣付、资金不足抛异常、`RepayLoan` 先本后息且 0 金额不落条目、`AccrueLoanInterest` 落**事件类**条目且不动本金、`TreasuryPool` = 现金 + 储蓄 + 商本（贷款不在池内）
- [X] T026 `tests/KFL.Tests/Core/EconomyStateTests.cs`：`Treasury`（三金额 `>= 0`、内部转账不落条目且超支抛异常、无手续费）、`Loan`（先本后息、`AccrueInterest` 不改本金、`MonthsSinceInterest` 自然月语义）、`Holdings`（移除至负数抛异常、`CountOf`）、`FamineState`（`None` ⇔ 0 的不变量、`TransitionTo` 当月记 1、`Tick`、`Clear`）、`GameState` 的变更（`Economy` 只读、`PendingDifficulty`、`AdvanceMonth()` 跨年进位且 12 月 → 次年 1 月、`CurrentDate` **无公开 setter**）

**Checkpoint**: Core 经济状态与结算快照形状就位，`dotnet build` 与 `dotnet test` 全绿——五个 user story 可以开始

> **本阶段完成登记（2026-10-06，T007~T026 全部执行）**：实测 `dotnet build` **0 警告 0 错误**、
> `dotnet test` **229 通过 / 0 失败 / 0 跳过**（阶段② 起点 171 → 本阶段新增 58 项：T024 9 + T025 33 +
> T026 16）。落地文件 24 个：`KFL.Core` 16（3 值类型 + 8 枚举 + `Loan` / `Holdings` / `FamineState` /
> `Treasury` / `Ledger` / `FamilyEconomy`）、`KFL.Rules` 2、`KFL.Infrastructure` 1 处调用点、
> `tests/KFL.Tests` 6（新夹具 1 + 新测试 3 + 两条阶段① 守卫修订 + 调用点适配）。
>
> **实现期发现并已回写 data-model 的五处口径**（都不是新数值，只是把原先「无处安放」的地方说清楚）：
> ① `CreditTargetOf` 返回 `LedgerCreditTarget?`（`null` = 无正额入账目标，正好对应 §2.1 表里
> 「—」的 9 类）；② `GrainPriceIndex` 的初值 1.0 移出 Core，单点归 `GrainPricePolicy`（与 T005 同
> 一理由：SC-008）；③ `Treasury` 三池对 Core 之外 `internal` 不可写、初值只经公开三参构造；
> ④ `Loan.Principal` / `MonthsSinceInterest` 公开可写（带校验），`AccruedInterest` 只读；
> ⑤ `FamineState` 为 `class` + 公开复制构造（供 `SettlementResult` 取快照，不为快照开放 setter），
> 且 `TransitionTo(None)` 按 `Clear()` 语义归 0（否则与「`None` ⇔ 计时 0」的不变量冲突）。
>
> **两条阶段① 守卫是「修订」而非「删除」**：`字段全集恰为五个` → 公开属性全集改为七个；
> `不存在资产现金储蓄贷款与统计容器字段` → 改为「`GameState` 自身不暴露资金容器，且 `Economy`
> 只读存在」，意图（资金不得绕过聚合，FR-021/SC-005）保留。修订理由已进提交信息（T021）。

---

## Phase 3: User Story 1 - 推演一个月，家里的钱按规格书的公式进出 (Priority: P1) 🎯 MVP

**Goal**: 交付规格书 §3（E-04 裁决后）的五步中属 US1 的部分：提升待生效值 → 米价系数游走并
clamp → 各来源收入入账 → 生活费（四乘区）扣付 → 推进一个月；并交付生活费与全部收入来源的
**逐项可读**计算器（§5.1、§5.2、§5.3、§8.1、§11）。

**Independent Test**: `dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~LivingCostTests|FullyQualifiedName~IncomeTests|FullyQualifiedName~SalaryTableTests|FullyQualifiedName~SettlementEngineTests"`
全绿——用多代同堂夹具（含田宅铺与在职位成员）在固定种子下推进一个月，逐项断言生活费金额与
年龄档明细、各来源收入分项金额、资金池变动额、六步顺序、次月生效与米价 clamp；全程不需要界面、
文件系统或网络。

**Acceptance Scenarios 归属**: US1 AS1（逐年龄档生活费）、AS2（自耕 20 亩上限与 E-11 分配）、
AS3（做工 + 城市宅 + 铺面）、AS4（经商门槛与无负值）、AS5（米价游走与 clamp）、AS6（档位次月
生效）、AS7（官俸 + 士 ×1.05）、AS8（每笔账一条条目且合计 = 池变动）、AS9（服刑/外嫁不计口但
档案与历史条目可读）。

### Implementation for User Story 1

- [ ] T027 [P] [US1] 创建 `src/KFL.Rules/Config/LivingCostTable.cs`：三档 × 四年龄档日耗**逐格**声明为 `DailyCost[档位][年龄档]`，**顺序为 成人/青年/老人/儿童**：拮据 `20 / 7 / 14 / 5`、普通 `25 / 8.5 / 17.5 / 5`、体面 `30 / 10 / 21 / 5`（§5.1；本类是这 12 个值的**唯一出处**，`KFL.Core` 与测试 MUST NOT 另存副本）；`DaysPerMonth = 30`；**新建存档的初始档位** `InitialStandard = LivingStandard.Normal`（2026-10-06 裁决 E-13；本类是它的**唯一出处**——`KFL.Core`/`KFL.Infrastructure` 与测试 MUST NOT 另存默认值）；**农出身独立乘区** `FarmerMultiplier`（仅 `Origin.Farmer`，**乘算**）：已成年（青年/成人/老人）`0.90`、未成年（儿童）`0.80`，非农恒 `1.00`；**生活费一般乘区**修正项列表 `GeneralZoneModifiers`（`1 + Σ同区百分比修正`，**加算**）：未成年 `−0.50`（**所有出身共有**）、救济期 `−0.20`（§5.4）。两条派生关系 MUST 与表值一致并由断言守卫：老人档 = 成人档 ×`0.7`；儿童档**固定 5 文、不随档位缩放**（E-10；R-17）
- [ ] T028 [P] [US1] 创建 `src/KFL.Rules/Config/AgeBracketPolicy.cs`：`Of(Gender, int age) → AgeBracket` 与 `Of(Person, GameDate) → AgeBracket`。**逐字**口径：未成年（男 <**12** / 女 <**14**）一律**儿童档**；已成年且 ≤**18** 岁**青年档**；≥**60** 岁**老人档**；其余**成人档**；生日当月即转档（`GameDate.AgeInYearsAt` 已按此实现）。边界 MUST 可断言：12 岁男生日、14 岁女生日、恰 18 岁、恰 60 岁（§5.1、§4.3；R-06）
- [ ] T029 [P] [US1] 创建 `src/KFL.Rules/Config/GrainPricePolicy.cs`：初始 `1.0`、游走 `±10%`（映射 `系数 ← clamp(系数 × (1 + (r × 0.2 − 0.1)), 0.7, 3.0)`，`r ∈ [0,1)`）、`Min = 0.7` / `Max = 3.0`；并提供**派生展示值** `米价 = (系数 − 0.4) ÷ 0.6`（阶段③ 使用，**不参与本阶段结算**）。**MUST NOT** 把 `0.4`/`0.6` 写进 `KFL.Core` 的 `GrainPriceIndex`（SC-008）（§5.1；E-01/R-10）
- [ ] T030 [P] [US1] 创建 `src/KFL.Rules/Config/DifficultyRates.cs`：四难度（简单/普通/困难/地狱）的**收益系数** `1.4 / 1.0 / 0.9 / 0.8`、**支出系数** `0.6 / 1.0 / 1.1 / 1.3`（§11），以及 §11 的贿赂风险与负面事件系数（**登记以便 §11 数值单点**，但**本阶段 MUST NOT 被任何计算路径读取**——它们属阶段⑥/⑧）；提供 `Difficulty` → 系数的查表
- [ ] T031 [P] [US1] 创建 `src/KFL.Rules/Config/IncomeRateTable.cs`：自耕 `0.5` 贯/亩/年、每成人上限 **20 亩**；田租 `0.1` 贯/亩/年；务农 **2 贯/月**（家族无田可耕时，**每名计口成人**）；做工 **1.5 贯/月**（每名被指派者）、城市宅加成 **+1 贯/月**（份数 = `min(做工人数, 城市宅数)`）；经商 `2%` + 门槛 **100 贯**（**含**边界）+ 商出身 `×1.1`；天赋除数 农 **200** / 工 **400** / 商 **200**；工出身 bonus **6%**（消费在 US3）（§5.2、§5.3；E-02/E-12/R-18/R-19）
- [ ] T032 [P] [US1] 创建 `src/KFL.Rules/Config/AssetPriceTable.cs`：田 **1** 贯/亩、农村宅 **10** 贯/座、城市宅 **100** 贯/座、铺面 **300** 贯/间（**购售同价**）；铺面年租 **20%**（月摊 `300 × 20% ÷ 12 = 5` 贯）；并提供市值函数 `MarketValue(Holdings)` = 田×1 + 农村宅×10 + 城市宅×100 + 铺面×300 贯，**不含商本池**（§5.3；R-15 与工 bonus 基数口径）
- [ ] T033 [P] [US1] 创建 `src/KFL.Rules/Config/SalaryTable.cs`：18 级年俸**逐级**声明 L1→L18 = `5100 / 4250 / 3560 / 2980 / 2490 / 2090 / 1750 / 1460 / 1220 / 1020 / 860 / 720 / 600 / 500 / 420 / 235 / 130 / 72`；月摊 = `÷12`；士出身成员当官后 `×1.05`（§8.1、§5.2）。锚点 `L18 = 72` / `L15 = 420` / `L1 = 5100` MUST 逐级可断言（SC-004）
- [ ] T034 [P] [US1] 创建 `src/KFL.Rules/Settlement/CountedMembers.cs`（**纯函数**，FR-029/FR-030）——生活费与收入的成员集合：**计口** = **在册**（未亡且未外嫁）**且未服刑**。`RegisteredMembers`（在册）与 `CountedMembers`（计口）MUST **各自独立**给出且 MUST NOT 互相顶替：**待阙**照常计入计口（§8.2 无俸、靠积蓄；2026-10-06 用户确认待阙包在计口内）；**外嫁**与**已亡**本就不在在册内；**服刑**在册但**不计口**。两口径都必须可读，供界面与后续阶段消费（§7.5、§12.1、§5.1）
- [ ] T035 [US1] 创建 `src/KFL.Rules/Settlement/LivingCostCalculator.cs`（**纯函数，不碰资金池**）：按**四个乘区**算月应付生活费——`30 × 米价系数 × 难度支出系数 × Σ(各计口成员日耗) × 农出身独立乘区 × 生活费一般乘区`；输出**逐年龄档明细**（日耗、人数、小计）与应付额。救济期的 `×0.8` 由传入的 `FamineStage` 决定（US4 传入，US1 传 `None`）；米价系数与难度支出系数**各自独立相乘**、**不进**一般乘区（R-17）。判据示例 MUST 可复现：普通档、非农、无救济的儿童 = `5 × 30 × 米价系数 × 难度支出系数 × 1.00 × 0.50`；同条件农出身儿童再 `× 0.80`；农出身且处于救济期的儿童，一般乘区 = `1 − 0.5 − 0.2 = 0.3`（依赖 T027、T028、T029）
- [ ] T036 [US1] 创建 `src/KFL.Rules/Settlement/IncomeCalculator.cs`（**纯函数，不碰资金池**）：按契约「月度结算」§6 的表格算**全部**收入来源分项与归属成员——**自耕**（亩数按 `(1+农/200)×(1+工/400)` **降序**依次填满每成人 **20 亩**，并列时按年龄降序、再按 `PersonId`（E-11）；每人 `亩数 × 0.5 ÷ 12 贯` 乘**本人**农/工乘数；**第 21 亩起转田租**）、**田租**（`非自耕亩数 × 0.1 ÷ 12 贯` × **家主**的农/工乘数；`HeadId == null` 或家主非计口时乘数取 `1.0`）、**务农**（仅在家族**无田可耕**时，**每名计口成人** `2 贯/月` × 本人工农乘数，与自耕互斥）、**做工**（需 ≥1 名计口成人被指派「做工」；**每名被指派者** `1.5 贯/月` × 本人工乘数；城市宅再 `+1 贯/月 × min(做工人数, 城市宅数)`）、**经商**（商本 ≥ **100 贯**（含）**且** ≥1 名计口成人指派经商；**家族级一份** `商本 × 2% × (1+商/200) × (1+工/400)`，商出身再 `×1.1`；多名指派者取乘数**最大者**，并列时年龄降序再按 `PersonId`）、**官俸**（`SalaryTable[级] ÷ 12 × 难度收益系数`，士出身当官再 `×1.05`）、**铺面租**（`间数 × 300 贯 × 20% ÷ 12`）、**工出身 bonus** 与**储蓄利息**（两者的年度时点与系数口径分别属 US3 与 T048）。**难度收益系数除储蓄利息外全部收入均乘**（FR-012）；收入来源**彼此叠加**（一名成员可同时贡献自耕与做工）；各来源只对**计口成员**计算（依赖 T031、T033、T030、T032、T034）
- [ ] T037 [US1] 创建 `src/KFL.Rules/Settlement/MonthlySettlementEngine.cs`——**唯一编排入口**：`ctor(IRandomService, IGameClock)`（章程原则 II：接缝必须构造注入，R-03）、唯一公开方法 `SettlementResult Settle(GameState)`，**就地**推进传入的 `GameState` 与 `CurrentDate`。步骤序列**严格**按契约「月度结算」§1：①提升 `PendingDifficulty`/`PendingLivingStandard` 并清空待生效位 → ②米价系数游走并 clamp（**消耗 1 次 `NextDouble()`**）→ ③收入（**本任务不含 1 月/12 月的年度项**，属 US3）→ ④生活费（按 T035 的应付额支付：`资金池 >= 应付额` 足额支付；否则**部分支付** `min(应付额, 资金池)` + 资金池清零 + 差额**不入账、不转贷款**，E-06；**本任务不含饥馑阶段迁移**，属 US4）→ ⑤贷款（**本任务只保留步骤位**：`LoanPolicy` 与 `LoanSettlement` 属 US2，MUST NOT 在此自行发明利息或划扣规则；无贷款时该步无动作）→ ⑥`AdvanceMonth()` 并返回快照。**MUST NOT** 出现随机事件、属性成长/衰老/疾病/死亡、科举季触发、绝嗣判定、存档写入（契约「月度结算」§1 的「MUST NOT 存在」清单）（依赖 T023、T029、T035、T036）

### Tests for User Story 1

> **依赖顺序说明**（与 001 同一处置）：C# 中测试无法在类型不存在时通过编译，故测试任务排在对
> 应实现**之后**，而不是字面的「先写测试」。红-绿由分步执行体现：先跑 `dotnet test` 看到断言
> 失败或测试缺失，再补齐实现使其通过。

- [ ] T038 [US1] `tests/KFL.Tests/Rules/LivingCostTests.cs`（quickstart S1、S8）：逐年龄档日耗与人数 → 四个乘区**各自可分别读出**；生日当月转档（12 岁男 / 14 岁女生日）；老人 = 成人 ×0.7、儿童固定 5 文不随档位缩放；**农出身/非农 × 成年/未成年四种组合各一条**；救济期一般乘区 = `1 − 0.5 − 0.2 = 0.3`（农出身未成年 + 救济）；**计口断言（FR-029/FR-030、SC-009）**——同夹具加入一名**服刑**成员与一名**外嫁**成员，两人对生活费合计的贡献为 0，而两人的档案与历史账目仍 100% 可读；**待阙**成员照常计入生活费；并断言 `LivingCostTable.InitialStandard == LivingStandard.Normal`（FR-005 的开局初值，E-13）
- [ ] T039 [US1] `tests/KFL.Tests/Rules/IncomeTests.cs`（quickstart S2、S8）：自耕 40 亩 = `40 × 0.5 ÷ 12 = 1.667` 贯/月（**不是** 2 贯）；每成人 20 亩上限与第 21 亩转田租；E-11 的降序分配（含并列时年龄降序、再按 `PersonId`）；无田时务农 2 贯/月/计口成人且与自耕互斥；做工需指派、多名被指派者各得一份、城市宅加成 = `min(做工人数, 城市宅数)`；商本 **99 贯无收益 / 100 贯有收益**（边界各一条）、商出身 ×1.1；铺面月摊 5 贯；田租在家主为 `null` 或非计口时乘数取 `1.0`；收入来源**叠加**（同一成员同时自耕与做工）；**储蓄利息不乘难度收益系数**（负例断言）；服刑/外嫁成员对**各收入分项**的贡献为 0、待阙成员照常可产生收入（但无俸禄）
- [ ] T040 [US1] `tests/KFL.Tests/Rules/SalaryTableTests.cs`（quickstart S2、SC-004、§16 必测三项之一）：18 级**逐级**断言 + 锚点 `L18 = 72` / `L15 = 420` / `L1 = 5100` 误差为 **0**；月摊 = ÷12；士出身当官 ×1.05 与「士出身但无官职 → 不产生官俸」对照；官俸乘难度收益系数、储蓄利息不乘（与 T039 的负例互补）
- [ ] T041 [US1] `tests/KFL.Tests/Rules/SettlementEngineTests.cs`（quickstart S1、SC-001）：①**六步顺序**逐条断言（含「提升在米价游走之前」「收入在生活费之前」——用「本月有俸禄可领」的家庭证明不会在发放前被判断炊，E-04）；②米价系数游走区间与 clamp `0.7`/`3.0`、连续越界回弹以边界为起点、当月值当月生效（US1 AS5）；③生活费档位与难度**次月生效**：切换当月不变、次月变（US1 AS6、FR-005）；④`NetProfit` = 本月全部收入 − 本月**实付**生活费（Assumptions 的口径）；⑤`AdvanceMonth` 跨年进位（12 月 → 次年 1 月）；⑥一次结算产生的条目与 `SettlementResult.Entries` **逐条相同**、`TreasuryPoolAfter − Before` = 资金类条目之和（US1 AS8、SC-005 的首次落地）；⑦付不起时部分支付、资金池清零、**不出现负余额**、缺口不入账不转贷款（E-06）

**Checkpoint**: US1 可独立验证——生活费与全部收入来源逐项可断言、六步顺序被钉住，MVP 达成

---

## Phase 4: User Story 2 - 欠了钱就按先本后息往下还，利息永不滚进本金 (Priority: P2)

**Goal**: 交付规格书 §5.4 的负债机制——月划扣比例（仕 20% / 工·农 40% / 商 80%）、先本后息、
每满 12 个月按**当时剩余本金**计息一次且利息永不滚入本金、净利润 ≤ 0 不划扣不罚但**计息计时
照常按自然月推进**，以及 FR-017 的支付原语。

**Independent Test**: `--filter "FullyQualifiedName~LoanTests"` 全绿——构造「本金 + 欠息」并存
且属于商（80%）的贷款，在若干个月内推进结算，断言划扣额 = `min(净利润 × 比例, 本金 + 欠息)`、
本金优先下降、跨 12 个月节点时按**当时剩余本金**独立 roll 一次且**只增加欠息**、本金清零后
划扣转向欠息、两者皆清后不再划扣、先计息后划扣；全程无界面、无文件系统。

**Acceptance Scenarios 归属**: US2 AS1（划扣与先本后息）、AS2（20/40/80 三档）、AS3（净利润
≤ 0 不划扣不罚）、AS4（12 月按当时本金计息、本金不增）、AS5（本金清零转欠息、皆清结清）、
AS6（同种子两轮一致）。

### Implementation for User Story 2

- [ ] T042 [P] [US2] 创建 `src/KFL.Rules/Config/LoanPolicy.cs`：划扣比例——`HasShiStatus == true`（家族级「仕」身份，§10.2）→ **20%**；否则 `Origin == Merchant` → **80%**；其余（士/工/农）→ **40%**（§5.4、§10.2；R-08 第 4 条）
- [ ] T043 [P] [US2] 创建 `src/KFL.Rules/Settlement/LoanSettlement.cs`（**纯函数**）：`IsInterestDue(Loan)`（`MonthsSinceInterest` 达 **12**，**只由该计数决定**，MUST NOT 依赖任何随机结果或当月净利润——E-07）、`RollRate(IRandomService)`（`0.005 + r × 0.019`，区间 `0.5%~2.4%`）、`ComputeRepayment(Money netProfit, bool hasShiStatus, Origin origin, Loan)`（= `min(净利润 × 比例, 本金 + 欠息)`，**封顶**——不封顶会引入规格书没有的「退款」资金流）
- [ ] T044 [P] [US2] 创建 `src/KFL.Rules/Economy/PaymentPrimitive.cs`（**纯函数**，FR-017）：`Pay(FamilyEconomy, Money amount)` 按「**现金 → 储蓄 → 余额转贷款**」三步执行，返回三段的拆分结果。本阶段**没有**罚金调用方（FR-017、阶段⑥），这是 `Loan.Principal` 在本阶段**唯一**的产生路径（data-model §3.2 的注）。资产买卖与生活费**MUST NOT** 走这条路径（§5.4「不存在主动借贷」）
- [ ] T045 [US2] 在 `MonthlySettlementEngine`（T037 建立的文件）的第⑤步接入贷款结算，**顺序 MUST 为先计息、后划扣**（E-05）：①若 `IsInterestDue` → 消耗 **1 次** `NextDouble()` roll 利率，`欠息 += 当时本金 × 新利率`，计数归零，落一条**事件类** `LoanInterestAccrued` 条目（本金 MUST NOT 增加）；②`净利润 ≤ 0` → **不划扣、不产生罚则、不重置也不跳过计息计时**（E-07）；③否则 `RepayLoan(min(净利润 × 比例, 本金 + 欠息))`，**先本后息**、落 1~2 条**资金类**条目（金额为 0 的部分不落条目，净利润恰为 0 时**不得**产生 0 元划扣条目，spec Edge Cases）；④本金清零后划扣全部转向欠息，两者皆清即结清且此后不再产生划扣条目。`MonthsSinceInterest` 每个结算月 **+1**（自然月，与净利润无关）（依赖 T043、T042、T037）
### Tests for User Story 2

- [ ] T046 [US2] `tests/KFL.Tests/Rules/LoanTests.cs`（quickstart S3、SC-002、§16 必测三项之一）：①划扣 = `min(净利润 × 比例, 本金 + 欠息)` 且**优先冲本金**、不足部分转冲欠息（先本后息）；②三档比例**各一条**（仕 20% / 工农 40% / 商 80%）；③净利润 ≤ 0 → 不划扣、无罚则、余额与阶段不变，且**不产生 0 元划扣条目**，同时断言 `MonthsSinceInterest` **照常 +1**（E-07 的关键断言）；④第 12 个月按**当时剩余本金**计息一次并**只增加欠息**（本金 MUST NOT 因计息而增加），第 13 个月不重复计息；⑤**先计息后划扣**：同月既命中计息节点又产生划扣时，计息本金 = **月初**结余本金（E-05）；⑥本金清零后划扣全部转向欠息；皆清即结清且此后无划扣条目；⑦本金为 0 而欠息 > 0 时 12 个月计息额为 **0**（不出现「无本金却持续生息」）；⑧固定种子重复两次同样的贷款结算序列 → 每次计息结果与余额轨迹**完全一致**（SC-002/SC-006）；⑨`PaymentPrimitive` 的「现金 → 储蓄 → 余额转贷款」三步各一条断言；⑩本金封顶（多还部分不产生退款资金流）

**Checkpoint**: US1 与 US2 均可独立验证——现金流与偿债机制都已被钉住

---

## Phase 5: User Story 3 - 储蓄生息与年末增益 (Priority: P3)

**Goal**: 交付规格书 §5.4 的年度节奏——每年 1 月为当年 roll 一个 `0.5%~2.4%` 的储蓄利率并
当年不变、12 月末按该利率计息并**并入储蓄本金（复利）**；工出身家族在 12 月末按总资产拿
**6%** 现金增益。

**Independent Test**: `--filter "FullyQualifiedName~SavingsTests"` 全绿——在一个 1 月结算中读出
当年储蓄利率（落在 `0.5%~2.4%`），持续结算到 12 月末断言利息 = 储蓄本金 × 当年利率且并入本金，
次年 1 月重新 roll 出独立利率；对工出身家族在同一月断言 bonus = `(现金 + 储蓄 + 田宅铺市值 − 本金 − 欠息) × 6%`
且仅当基数为正时发放；全程固定随机种子。

**Acceptance Scenarios 归属**: US3 AS1（1 月 roll 当年利率且当年不变）、AS2（12 月计息并入
本金、次年重 roll）、AS3（储蓄利息 MUST NOT 被乘收益系数）、AS4（工 bonus 三分支）。

### Implementation for User Story 3

- [ ] T047 [P] [US3] 创建 `src/KFL.Rules/Settlement/SavingsSettlement.cs`（**纯函数**）：`RollRate(IRandomService)`（`0.005 + r × 0.019`）、`Accrue(Money savings, decimal rate)`（利息 = 储蓄本金 × 当年利率，**并入储蓄本金**）。与 `LoanSettlement` **共用** `InterestPolicy` 的区间与映射（T022），MUST NOT 复制字面量
- [ ] T048 [US3] 在 `MonthlySettlementEngine` 第③步接入年度项：①**每年 1 月**结算第③步**无条件** roll 一次当年储蓄利率（`0.5%~2.4%`）并与年份一起记入 `Treasury`，**当年内不变**，跨年 1 月重新 roll（与旧值无关）；②**每年 12 月**结算第③步：`利息 = 当时储蓄本金 × 当年利率`，**并入储蓄本金**，落 `SavingsInterest` 条目（入账目标 = **储蓄**，见 `LedgerCategoryMetadata`）；③**12 月末**工出身 bonus：基数 = `现金 + 储蓄 + 田宅铺市值 − 贷款本金 − 欠息`（**不含商本池**），基数为**正**才发 `基数 × 6%` 现金并落 `ArtisanBonus` 条目，且该 bonus **乘难度收益系数**（§5.2「储蓄利息除外」）；基数为 0 或负 → 本年不发；非工出身 → 任何情形都不发。**储蓄利息 MUST NOT 乘难度收益系数**（FR-012）。随机消费次序：米价 → 储蓄利率（仅 1 月）→ 贷款计息利率（契约「月度结算」§4）（依赖 T047、T037、T045、T031、T032、T030）
### Tests for User Story 3

- [ ] T049 [US3] `tests/KFL.Tests/Rules/SavingsTests.cs`（quickstart S4）：①1 月 roll 出的利率落在 `0.5%~2.4%`（**区间两端**：`NextDouble()` 取 0 与极大值）且当年内不变；②12 月末利息 = 储蓄本金 × 当年利率并**并入本金**（复利：次年以并入后的本金为基数）；③次年 1 月**重新 roll** 一个独立利率；④工 bonus 三分支——基数为正发放 `×6%`、基数为 0 不发、基数为负不发；非工出身任何情形都不发；⑤**不含商本池**（把商本加大而资产不变 → bonus 不变）；⑥储蓄利息**不**乘难度收益系数（四难度下利息相同）+ 工 bonus **乘**收益系数（四难度下不同）——一对正负例；⑦bonus 与利息的条目归属为**家族级**（`PersonId == null`）

**Checkpoint**: US1~US3 均可独立验证——年度节奏与工出身特性落地

---

## Phase 6: User Story 4 - 断炊不静默：饥馑四阶段如实推进 (Priority: P4)

**Goal**: 交付规格书 §5.4 的饥馑时间线——`None → Famine`（满 3 月）`→ Relief`（满 12 月）
`→ Severe`，救济期全体支出 `−20%`，任一月份付得起即**全部解除且计时归零**，判定次序为
**先解除、后升级**（R-12），每次阶段迁移落一条**事件类**条目。

**Independent Test**: `--filter "FullyQualifiedName~FamineTimelineTests"` 全绿——构造一个现金与
储蓄都不足的家庭，逐月推进并断言阶段迁移（阶段一 → 满 3 月 → 阶段二 → 满 12 月仍不足 →
阶段三）、救济期生活费按 `−20%` 计（体现在**条目金额**上而不是事后修正）、以及任一月资金
补足后阶段一次性归零；全程无界面。

**Acceptance Scenarios 归属**: US4 AS1（进入饥馑 + 部分支付 + 事件条目）、AS2（满 3 月转救济
且期限 12 个月）、AS3（救济期满仍付不起**正常档**支出则升级）、AS4（付得起即全部解除且无计时
残留）、AS5（救济减免进**一般乘区**：未成年人在救济期 = `1 − 0.5 − 0.2 = 0.3`）。

### Implementation for User Story 4

- [ ] T050 [P] [US4] 创建 `src/KFL.Rules/Config/FamineTimeline.cs`：饥馑满 **3** 个月转救济、救济期 **12** 个月、救济期支出折扣 **20%**（§5.4）。剩余月数 = 时限 − 已持续月数（FR-019 的可读口径）
- [ ] T051 [P] [US4] 创建 `src/KFL.Rules/Settlement/FamineController.cs`（**纯函数**）：`FamineDecision Evaluate(FamineState, Money payable, Money pool)`。判定次序**固定**：①先判「资金池 ≥ 当月应付额 → 足额支付、阶段归 `None`、计时清零（此前非 `None` 时落 `FamineResolved`），当月结束」；②否则先部分支付，再按转移表推进——`None` + 未能足额 → `Famine`（计时 1，落 `FamineEntered`）；`Famine` 累计满 **3** 月仍未足额 → `Relief`（计时**重算为 1**，落 `FamineReliefEntered`）；`Relief` 累计满 **12** 月仍未足额 → `Severe`（计时重算为 1，落 `FamineSevereEntered`）；`Severe` 保持。**每个月的阶段迁移 MUST 至多触发一次**（同月 MUST NOT 既转阶段又重复计时，spec Edge Cases）。救济期的**应付额** = 正常档 × `(1 − 20%)`（`LivingCostCalculator` 已按传入阶段处理）；`Severe` 阶段不再减免，应付额 = 正常档。**MUST NOT** 实现体质 −10/月、每人每月 5% 死亡判定与「体质不清零」（属阶段⑧，FR-019）
- [ ] T052 [US4] 在 `MonthlySettlementEngine` 第④步接入饥馑状态机：用**当月应付额**（`Relief` 期即减免后的值）判定 → 足额则解除并清零 → 否则部分支付（E-06 的 `min(应付额, 资金池)` + 资金池清零 + 差额不入账不转贷款）+ 按 T051 的转移表推进并落对应的**事件类**条目。步骤序列与随机消费次序**不得改变**（US2/US3 的既有断言必须继续全绿）（依赖 T051、T037、T035）
### Tests for User Story 4

- [ ] T053 [US4] `tests/KFL.Tests/Rules/FamineTimelineTests.cs`（quickstart S5、SC-003、§16 必测三项之一）：①4 个阶段转移 **4/4** 各一条独立断言（`None → Famine` → 满 3 月 `→ Relief` → 满 12 月 `→ Severe`）；②「任一月付得起即**全部解除**且计时归零、MUST NOT 保留任何阶段计时残留」1 条；③付不起时生活费条目 = `min(应付额, 资金池)`、资金池清零、**不出现负余额**、缺口不入帐不转贷款；④救济期的 `−20%` 体现在**条目金额**上，未成年人处于救济期时一般乘区 = `0.3`；⑤同月 MUST NOT 既转阶段又重复计时（「饥馑与救济的关键月份同月发生」边界）；⑥每次迁移都留下对应的事件类条目且**不参与 SC-005 求和**；⑦阶段与剩余月数可读（FR-019）

**Checkpoint**: US1~US4 均可独立验证——失败反馈闭环成立

---

## Phase 7: User Story 5 - 每一文钱都有出处：一次结算一条账 (Priority: P5)

**Goal**: 把流水账的两个聚合维度交付完整——**家族级**（总收支、收益来源明细、支出明细）与
**角色级**（按角色的每月与累计收支，**含已归档成员**），二者基于**同一批条目**且不依赖任何
预先存储的月度汇总值。

**Independent Test**: `--filter "FullyQualifiedName~LedgerTests"` 全绿——推进若干个月，断言每条
都带年月、类别与金额；对任意单月与任意连续区间断言 `Σ(资金类条目) == 资金池期末 − 期初`
（差额为 0）；家族维度与角色维度聚合出同一批条目；已归档成员的历史条目仍可读；事件类条目
不参与求和但 MUST 存在且可读；不存在「只动资金池而不落条目」的路径。

**Acceptance Scenarios 归属**: US5 AS1（资金类 vs 事件类条目各归其位）、AS2（家族级条目
MUST NOT 被塞给某个成员或丢失）、AS3（已归档成员历史条目可读）、AS4（两维度同批条目、不依赖
预存汇总）。

### Implementation for User Story 5

- [ ] T054 [US5] 在 `src/KFL.Core/Entities/Ledger.cs`（T016 建立的文件）补上四个**聚合查询**（data-model §3.4）：`Money TreasuryDeltaIn(from, to)`（对**资金类**条目求和，SC-005 的等式左侧）、`IReadOnlyList<(LedgerCategory, Money)> TotalsByCategory(from, to)`（收益来源明细 / 支出明细）、`Money TotalsByPerson(PersonId, from, to)`（单个成员的区间累计，**含已归档成员**）、`IReadOnlyList<(GameDate, Money)> MonthlyByPerson(PersonId, from, to)`（单个成员的逐月收支）。**实现方式如实记录**：线性扫描，本阶段**不建索引、不分块**（R-16/契约四 §6）；**MUST NOT** 存任何月度汇总值（FR-021、SC-008）——上述两个维度是**同一批条目的两个查询方向**
### Tests for User Story 5

- [ ] T055 [US5] `tests/KFL.Tests/Rules/LedgerTests.cs`（quickstart S6、SC-005）：①对**任意单月与任意连续区间**断言 `Ledger.TreasuryDeltaIn(from, to) == TreasuryPoolAfter − TreasuryPoolBefore`（差额为 **0**）；②`SettlementResult.Entries` 与账本新增条目**逐条相同**；③家族维度与角色维度聚合出的是**同一批条目**（US5 AS4）；④**已归档**（死亡/外嫁）成员的历史条目仍可按角色读出（US5 AS3、SC-009）；⑤铺面租、储蓄利息、工 bonus 为**家族级**条目（`PersonId == null`），MUST NOT 被塞给某个成员或丢失；⑥事件类条目（饥馑四类迁移、贷款计息入欠息）**不参与求和**但 MUST 存在且金额可读；⑦资金类条目 `Amount` 非 0；⑧条目数与结算次数、发生的资金事件数一一对应——不存在「只动资金池而不落条目」的路径；⑨用反射断言**不存在**任何月度汇总类型/字段（契约四 §4 不变量 6）

**Checkpoint**: 五个 user story 全部可独立验证——统计页（阶段⑩）将来要消费的数据形状已定型

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: 跨故事的收尾件、SC-008 的扫描验证物、确定性复跑、文档与实现的同步、门禁终检与提交。

- [ ] T056 [P] 创建 `src/KFL.Rules/Economy/AssetMarket.cs`（FR-027、R-15）：`Buy(GameState, AssetKind, int count)` 与 `Sell(GameState, AssetKind, int count)`——改 `Holdings` 数量与资金池，落**资金类**条目（类别 `AssetPurchase` / `AssetSale`，金额 = ±数量 × 单价）；田宅铺**购售同价**；现金不足即**拒绝**（MUST NOT 转贷款——§9.5/§5.4 同一口径）。界面入口属阶段③
- [ ] T057 `tests/KFL.Tests/Rules/AssetMarketTests.cs`：买/卖各一条（资金池与 `Holdings` 同价变动、条目类别与金额正确）；卖出后对应收入来源退化到 0（田租、做工加成、铺面租、工 bonus 基数）；资产被卖光或从未持有时结算**不中断**（spec Edge Cases）；现金不足时拒绝且**不产生贷款**
- [ ] T058 [P] 把 `src/KFL.Rules/Config/GameConfig.cs` 由空壳改为**数值总表入口**（data-model §4.1）：以只读视图聚合各子表（`LivingCostTable` / `AgeBracketPolicy` / `IncomeRateTable` / `AssetPriceTable` / `InterestPolicy` / `FamineTimeline` / `GrainPricePolicy` / `DifficultyRates` / `SalaryTable` / `LoanPolicy`），供核对与后续阶段的单点引用。**MUST NOT** 在本文件里复制任何数值字面量（SC-008）；并同步 `contracts/config-registry.md` 的「配置成员」列使其与该入口一致
- [ ] T059 [P] 创建 **SC-008 的验证物**：`tests/KFL.Tests/Architecture/ConfigLiteralRules.cs`（纯函数：输入源码字典 + 数值清单，输出违规「文件:行 + 数值」）+ `tests/KFL.Tests/Architecture/ConfigLiteralTests.cs`（外壳：用 `RepositoryLocator` 读 `src/KFL.Core`/`KFL.Infrastructure`/`KFL.Rules` 与 `tests/KFL.Tests/{Core,Infrastructure,Fixtures,Rules}` 的 `.cs`，断言违规为空）。范围与 G-07 相同、支持行级 `// arch-guard:allow`（须在同一行说明理由）。清单 = `contracts/config-registry.md` §1~§4 逐行的数值（逐字抄入测试；该清单是 SC-008 的验证物清单，不是生产数值的第二出处——冲突时以源码为准）。条款：① 上述数值 MUST 只出现在 `src/KFL.Rules/Config/` 的常量声明处；② 测试期望值 MUST 通过**配置成员**取得，MUST NOT 复制字面量——**唯二例外**是锚点断言（`72`/`420`/`5100`，因为它们本身就是被验证对象）与本扫描清单。**放在 `Architecture/` 的理由**：它必须读文件系统，而该目录是 G-07 的显式豁免区（契约一 §2 的 G-07 行）
- [ ] T060 [P] 创建 `tests/KFL.Tests/Rules/DeterminismTests.cs`（quickstart S7、SC-006）：同一种子 + 同一初始 `GameState` + 同一月份序列 → 两次运行的 `SettlementResult` 序列、资金池、米价系数与账本条目序列**逐位相同**；并断言随机消费次序——**未命中的计息节点不消耗随机数**，且「是否命中」只由 `Loan.MonthsSinceInterest` 决定（契约「月度结算」§4 条款 2）。类名 MUST 含 `Determinism` 以便 `--filter "FullyQualifiedName~DeterminismTests"` 同时命中 001 的 `tests/KFL.Tests/Infrastructure/DeterminismTests.cs`（quickstart S7 的预期）
- [ ] T061 [P] 对齐文档命令与文件划分：[quickstart.md](./quickstart.md) §3 的 S2 目前只写 `--filter "FullyQualifiedName~IncomeTests"`，而 R-16 把俸禄断言单独放在 `tests/KFL.Tests/Rules/SalaryTableTests.cs`——把 S2 的 `--filter` 改为 `"FullyQualifiedName~IncomeTests|FullyQualifiedName~SalaryTableTests"`，并补上 US1 的新增文件 `SettlementEngineTests`（S1 或新增一条 S9，二选一，以能一条命令跑出为准）。**MUST NOT** 只改文档不改文件划分或反之——两者必须能互相验证
- [ ] T062 对照 [spec.md](./spec.md) 的 FR-001~FR-031（31 条）与 SC-001~SC-009 以及 [data-model.md](./data-model.md) §5 的映射表**逐条**核验「每条需求都有落地物与验证方式」，把覆盖结论（含未覆盖项与理由）写进提交信息。**MUST NOT** 用「大致覆盖」了事——映射表的每一行都要指到任务号
- [ ] T063 阶段边界复核（逐条对照 spec 的 Out of Scope，发现越界即删）：`KFL.Presentation` 与 `KFL.App` **一行未动**；无存档落盘读写；无随机事件（含灾年米价跳涨与经商亏损）；无属性成长/衰老/疾病/死亡判定与饥荒体质减免；无科举周期与省试路费；无罚金金额、惩罚矩阵、连坐与服刑计时；无买人口入口与递增计价（FR-028）；无待阙/授官/考课/政绩/致仕；无开局初始资产发放（阶段③）；`KFL.Core` **不含**任何规则数值（12/14/18/60、日耗表、利率、门槛、系数）；仓库**零新增 NuGet 依赖**、仍六工程、`Directory.Build.props` 与 `global.json` 未改；`tests/KFL.Tests/KFL.Tests.csproj` 的 `_MSTestEnableParentProcessQuery` **仍为注释态**
- [ ] T064 执行 [quickstart.md](./quickstart.md) §2 的三条门禁与 §3 的 S1~S8（逐条 `--filter` 跑一次）并归档**实际输出**：构建 0 警告 0 错误；测试全绿且 **0 skipped**（并与 T006 记录的基线对比新增项数）；`dotnet sln list` 恰六工程；递归无 `*.sln`。任一项不达标即回到对应任务修复，**不得**用放宽警告级别或跳过测试来凑门禁
- [ ] T065 按 Conventional Commits + 中文描述**分批**提交（涉及 `specs/`、`src/`、`tests/` 三处；每批提交前确认 `git status --short` 无残留、四条门禁全过）：`docs`（契约一 G-07 扩容与契约二注入条款、001 research R-13 登记、T005 的缺口登记、quickstart 的 filter 对齐）；`feat(core)`（`Money`/枚举/条目/实体的聚合/`GameState` 变更与调用点）；`feat(rules)`（`Config/` 各表）；`feat(rules)`（`Settlement/` 计算器与 `MonthlySettlementEngine`、`Economy/` 资产与支付原语）；`test`（`Core`/`Rules`/`Fixtures`/`Architecture` 的测试与守卫）。提交信息里 MUST 说明本批改了什么、为什么，以及 T002/T021 这类**既有守卫被修订**的依据

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 无依赖，可立即开始（T001 是流程项，可与其余并行）
- **Foundational (Phase 2)**: 依赖 Setup 完成（尤其 T002 的扫描范围与 T005 的档位初值缺口登记）
  ——**阻塞全部 user story**
- **User Stories (Phase 3~7)**: 均依赖 Foundational 完成；此后可按优先级顺序推进，
  或在人力允许时部分并行（见「User Story Dependencies」的跨故事登记）
- **Polish (Phase 8)**: 依赖全部目标 user story 完成；T056/T058/T059 可与 US5 并行

### User Story Dependencies

- **US1 (P1)**: Foundational 之后即可开始，不依赖其他故事（MVP）
- **US2 (P2)**: 除 Foundational 外，`LoanSettlement`/`LoanPolicy`/`PaymentPrimitive`（T042~T044）
  与 `LoanTests` 的**纯函数部分**完全独立；但 T045 的引擎第⑤步接入与 `LoanTests` 的端到端
  断言需要 US1 建立的 `MonthlySettlementEngine`（T037）
- **US3 (P3)**: 同上——T047 与 T049 的纯函数/单项断言独立；T048 的年度项接入需要 T037，
  且 12 月的储蓄利息与工 bonus 落在**收入**步，依赖 US1 的 `IncomeCalculator`
- **US4 (P4)**: T050/T051 独立；T052 的接入需要 T037 与 T035（生活费应付额）
- **US5 (P5)**: T054 只改 `Ledger.cs`，可在 Foundational 之后任何时候做；T055 的端到端断言
  需要 US1~US4 都已能产生条目（尤其事件类条目来自 US2/US4）
- **已登记的跨故事依赖（1 条主干）**：`US2 / US3 / US4 / US5 → US1 的 T037`——
  FR-020 规定月末结算**只能有一个编排入口**，因此后四个故事都落在 US1 建立的同一个
  `MonthlySettlementEngine` 上（每次只**插入**自己那一步的内容，MUST NOT 改动既有步骤的
  相对次序与语义）。它们的「独立验收」指的是：**各自的故事断言可单独跑绿**，而不是
  「US1 不存在也能跑」。这与 001 登记 `US3 → T033` 的同一方式登记于此，以免被误读
- **每个故事的纯函数部分可在 US1 之前完成**：`LoanSettlement`（T043）、`SavingsSettlement`
  （T047）、`FamineController`（T051）、`LoanPolicy`/`FamineTimeline`（T042/T050）、
  `AssetMarket`（T056）都不引用引擎，是真正的独立单元

### Within Each User Story

- 值类型与枚举先于实体与聚合（T007~T016 → T017）
- 配置表先于计算器（US1 的 T027~T033 → T035/T036；US2 的 T042 → T043）
- 计算器先于引擎接入（T035/T036 → T037；T043 → T045；T047 → T048；T051 → T052）
- 夹具先于使用夹具的测试（T019 → T024~T026 与 US1~US5 的测试）
- 每个 user story 完成并通过其 Checkpoint 后，再进入下一个优先级

### Parallel Opportunities

- Setup：T001、T002、T004、T005 可并行；T003 依赖 T002，T006 依赖 T002/T003
- Foundational：T007~T016 **十个独立文件可同时动笔**（C# 的类型互相引用不影响「可同时动笔」）；
  T017 依赖它们全部，T018 依赖 T017，T019 依赖 T017/T018，T020/T021 依赖 T018 与 T019；
  T022 与 `KFL.Core` 那批**完全无交集**，可与 T007~T018 并行；T023 依赖 T007/T010/T011/T012/T014
  的类型；T024~T026 各自依赖对应的实现文件
- US1：T027~T034 **八个独立文件**（7 张配置表 + 计口筛选）可并行；T035 依赖 T027~T029；
  T036 依赖 T030~T034；T037 依赖 T023/T029/T035/T036；T038~T041 四个测试文件互不依赖，
  在各自依赖的实现完成后可并行执行
- US2：T042、T043、T044 **三个文件**可并行；T045 依赖 T043/T042；T046 依赖 T045
- US3：T047 可与 US2 的任何任务并行；T048 依赖 T047 与 T037；T049 依赖 T048
- US4：T050、T051 可并行（也可与 US2/US3 并行）；T052 依赖 T051/T037；T053 依赖 T052
- US5：T054 可与 US2/US3/US4 的实现并行；T055 依赖 US1~US4 的条目来源齐备
- Polish：T056、T058、T059、T060、T061 五个任务互不重叠，**可全部并行**；T057 依赖 T056，
  T062~T065 依次进行

---

## Parallel Example: Foundational 阶段（十个独立文件）

```text
# KFL.Core 的十个文件互不依赖，可同时动笔（本阶段结束时统一编译一次）：
Task: "T007 Money in src/KFL.Core/ValueObjects/Money.cs"
Task: "T008 GrainPriceIndex in src/KFL.Core/ValueObjects/GrainPriceIndex.cs"
Task: "T009 六个枚举 in src/KFL.Core/Enums/"
Task: "T010 LedgerCategory + LedgerCategoryMetadata in src/KFL.Core/Enums/"
Task: "T011 LedgerEntry in src/KFL.Core/ValueObjects/LedgerEntry.cs"
Task: "T012 Loan + LoanRepayment in src/KFL.Core/Entities/Loan.cs"
Task: "T013 Holdings in src/KFL.Core/Entities/Holdings.cs"
Task: "T014 FamineState in src/KFL.Core/Entities/FamineState.cs"
Task: "T015 Treasury in src/KFL.Core/Entities/Treasury.cs"
Task: "T016 Ledger（存储与追加入口）in src/KFL.Core/Entities/Ledger.cs"
```

## Parallel Example: User Story 1 的八张表与筛选器

```text
# 八个文件无依赖、口径各自独立，可同时动笔：
Task: "T027 LivingCostTable in src/KFL.Rules/Config/LivingCostTable.cs"
Task: "T028 AgeBracketPolicy in src/KFL.Rules/Config/AgeBracketPolicy.cs"
Task: "T029 GrainPricePolicy in src/KFL.Rules/Config/GrainPricePolicy.cs"
Task: "T030 DifficultyRates in src/KFL.Rules/Config/DifficultyRates.cs"
Task: "T031 IncomeRateTable in src/KFL.Rules/Config/IncomeRateTable.cs"
Task: "T032 AssetPriceTable in src/KFL.Rules/Config/AssetPriceTable.cs"
Task: "T033 SalaryTable in src/KFL.Rules/Config/SalaryTable.cs"
Task: "T034 CountedMembers in src/KFL.Rules/Settlement/CountedMembers.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. 完成 Phase 1: Setup
2. 完成 Phase 2: Foundational（**关键**——阻塞全部故事；T017 的「唯一写入通道」是后面
   一切资金流动的物理落点）
3. 完成 Phase 3: User Story 1
4. **停下验证**：`--filter "FullyQualifiedName~LivingCostTests|FullyQualifiedName~IncomeTests|FullyQualifiedName~SalaryTableTests|FullyQualifiedName~SettlementEngineTests"`
   全绿；此时已可演示「推演一个月，钱按规格书的公式进出」
5. US2~US5 完成前**不宣称**负债机制、年度节奏、饥馑与账本聚合已达标

### Incremental Delivery

1. Setup + Foundational → 经济状态与资金流动通道就位（FR-001/FR-021 的形状达成）
2. ＋US1 → 生活费与收入可逐项断言（MVP，FR-002~FR-012 达成）
3. ＋US2 → 先本后息、20/40/80、12 月计息不滚本金（§16 必测 1/3，SC-002 达成）
4. ＋US3 → 储蓄复利与工 bonus（FR-013 达成）
5. ＋US4 → 饥馑四阶段（§16 必测 2/3，SC-003 达成）
6. ＋US5 → 两个聚合维度（SC-005 达成）
7. ＋Polish → SC-004/SC-006/SC-007/SC-008 与四条门禁完整执行，可提交

### 顺序执行的注意点

- **本阶段是「数值首次真正落地」的一步**：任何一处把 §5.1/§5.2/§5.3/§5.4/§8.1/§11 的数值
  写进 `KFL.Core`、测试正文或计算器，都会让 SC-008 的扫描测试变红——数值**只能**住在
  `KFL.Rules/Config/`，测试期望**只能**经配置成员取得（例外只有锚点 `72`/`420`/`5100`）。
- **既有守卫是被修订、不是被绕过**：T021 修订 `GameStateTests` 的两条阶段① 边界断言，
  T002 扩容 G-07 的扫描范围——两处都 MUST 在提交信息里写明依据，MUST NOT 直接删掉断言
  或把目录从扫描范围里摘出去。
- **`_MSTestEnableParentProcessQuery` 仍为注释态**：受限宿主里的开关只经命令行传入
  （`AGENTS.md` 坑 2），MUST NOT 以启用状态提交。
- **孤儿 `testhost` 会锁死 `bin\`**：`dotnet test` 被打断后若 `bin\` 删不掉，先
  `Get-Process -Name testhost | Stop-Process -Force`，**不要**去改文件 ACL（`AGENTS.md` 坑 3）。
- **一次结算只推进一个月**：任何「顺手把『快进一年』也实现了」的冲动都越界（界面与快进属阶段③）。

---

## Notes

- [P] 任务 = 独立文件、无需等待另一任务的结论，可并行
- [Story] 标签用于追溯到 [spec.md](./spec.md) 的 user story
- 每个 user story 都应可独立完成与验证；完成一个 Checkpoint 后再进入下一个
- **开局档位（已裁决，见 T005）**：规格书 §5.1 原先未规定初值 → 2026-10-06 裁决为**普通**
  档（`Normal`），已回写规格书 §5.1、spec.md FR-005/Assumptions、data-model §3.6/§4.1/§5、
  research R-09 与 plan E-13；取值单点在 `LivingCostTable.InitialStandard`，`FamilyEconomy`
  构造仍显式传入、不设默认值（T017/T027/T038）
- **跨故事依赖（已登记）**：US2/US3/US4/US5 → `MonthlySettlementEngine`（T037），理由与
  登记方式见「User Story Dependencies」
- 提交粒度：按逻辑组（契约与文档 / Core 状态 / 配置表 / 计算器与引擎 / 测试），
  提交信息用 Conventional Commits + 中文描述
- 避免：模糊任务、同文件冲突（引擎是唯一被多个故事先后修改的文件，故 T037/T045/T048/T052
  之间 MUST 顺序执行、MUST NOT 并行）、未登记的跨故事依赖
