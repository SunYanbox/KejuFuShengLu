# 阶段 1 数据模型：经济状态与月度结算

**Feature**: `002-monthly-settlement-economy` | **Date**: 2026-10-06 | **Spec**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

本文件定义 002 交付的经济状态与结算产物。字段全集来自规格书 §5.1~§5.4、§8.1、§11、§12.3
与 spec 的 FR-001~FR-031；**凡规格书未列举、且本阶段无消费者的一律不建**（章程「复杂度
MUST 被论证」）。存档落盘属阶段④，此时增删字段仍无迁移成本。

层归属的依据见 research R-01：**状态在 `KFL.Core`，依赖数值的推导与编排在 `KFL.Rules`**。

---

## 1. 值类型（`KFL.Core`）

### 1.1 `Money`（readonly record struct）

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `Wen` | `decimal` | 内部唯一存储：以**文**为单位（章程「技术栈与工程约束」） |
| `Zero` | `Money`（静态） | 0 文 |
| `FromWen(decimal)` | `Money`（静态） | 具名入口，禁止裸 `new Money(500)` |
| `FromGuan(decimal)` | `Money`（静态） | `= FromWen(guan × 1000)` |
| `Guan` | `decimal`（派生） | `= Wen / 1000`，仅用于展示与断言 |
| `IsPositive` / `IsNegative` | `bool`（派生） | 结算分支用 |
| `+ - * (decimal) 一元 -` 与 `== < >` | 运算符 | 与 `decimal` 的乘法只在「乘系数」处使用 |

**不变量**：`Wen` ∈ `decimal` 全域；溢出由 `decimal` 运算符抛 `OverflowException`（`FromGuan`
的 ×1000 换算有此风险）。
**明确不含**：舍入（R-02：一律保留全精度，取整属阶段③展示层）；隐式 `double`/`int` 转换
（把「贯」「文」的口径混淆挡在编译期）。

### 1.2 `GrainPriceIndex`（readonly record struct）

| 成员 | 类型 | 说明 | 来源 |
| --- | --- | --- | --- |
| `Value` | `decimal` | **米价系数**（Core **不含初值**：初始 1.0 单点在 `GrainPricePolicy`，SC-008） | §5.1；E-01；§4.1 |

**不变量**：`Value > 0`。
**明确不含**：`0.7~3.0` 的 clamp 区间与 `±10%` 的游走幅度（属 `KFL.Rules/Config/GrainPricePolicy`，
R-01/R-10）；米价派生值 `(系数 − 0.4) / 0.6` 也由 Rules 提供（阶段③ 展示用）。

### 1.3 `LedgerEntry`（readonly record struct）

| 成员 | 类型 | 说明 | 来源 |
| --- | --- | --- | --- |
| `Date` | `GameDate` | 归属年月 | §12.3 |
| `PersonId` | `PersonId?` | 归属角色；`null` = **家族级**（铺面租、储蓄利息、工 bonus、买人口等） | §12.3；001 R-16 |
| `Category` | `LedgerCategory` | 类别 | §12.3 |
| `Amount` | `Money` | 金额：**资金类** = 本次对资金池的变动额（正=流入、负=流出）；**事件类** = 该事件自身的金额语义 | E-08 |

**不变量**：`Category` 的种类（资金类/事件类）由 `LedgerCategoryMetadata` 唯一决定，条目
自身不存第二个副本。
**为什么带 `PersonId` 可空**：§12.3 要求「按角色」与「家族级」两个维度聚合，且买人口、
铺面租这类支出**无法归属到个人**（001 R-16 的三条理由）。

---

## 2. 枚举（`KFL.Core/Enums`）

| 类型 | 取值 | 来源 |
| --- | --- | --- |
| `LivingStandard` | `Frugal`(拮据), `Normal`(普通), `Comfortable`(体面) | §5.1 三档 |
| `AgeBracket` | `Child`(儿童), `Youth`(青年), `Adult`(成人), `Elder`(老人) | §5.1 四档 |
| `FamineStage` | `None`(无), `Famine`(饥馑), `Relief`(救济), `Severe`(第三阶段) | §5.4 四阶段 |
| `AssetKind` | `Farmland`(田), `RuralHouse`(农村宅), `UrbanHouse`(城市宅), `Shop`(铺面) | §5.3 |
| `LedgerEntryKind` | `Treasury`(资金类), `Event`(事件类) | E-08 |
| `LedgerCreditTarget` | `Cash`, `Savings` | §5.4（储蓄利息入储蓄本金） |
| `LedgerCategory` | 见 §2.1 | §12.3；§5.1~§5.4 |

`LedgerCategory` 的 §17 裁决（001 R-04 的同一模式）：`Occupation` 不新增「自耕」「务农」等
取值——收入来源是**账本类别**，不是职业（E-02 的收入触发口径固定后更是如此）。

### 2.1 `LedgerCategory` 全集与 `LedgerCategoryMetadata`

| 类别 | 中文 | 种类 | 正额入账目标 | 来源 |
| --- | --- | --- | --- | --- |
| `LivingCost` | 生活费 | 资金类 | —（恒为负） | §5.1 |
| `SelfFarmingIncome` | 自耕收入 | 资金类 | 现金 | §5.2 |
| `LandRentIncome` | 田租收入 | 资金类 | 现金 | §5.2 |
| `FarmingWageIncome` | 务农收入 | 资金类 | 现金 | §5.2 |
| `CraftingIncome` | 做工收入 | 资金类 | 现金 | §5.2 |
| `TradeIncome` | 经商收益 | 资金类 | 现金 | §5.2 |
| `OfficialSalary` | 官俸 | 资金类 | 现金 | §5.2、§8.1 |
| `ShopRentIncome` | 铺面租 | 资金类 | 现金 | §5.3 |
| `ArtisanBonus` | 工出身 bonus | 资金类 | 现金 | §5.2 |
| `SavingsInterest` | 储蓄利息 | 资金类 | **储蓄** | §5.4 |
| `LoanPrincipalRepaid` | 偿还本金 | 资金类 | —（恒为负） | §5.4 |
| `LoanInterestRepaid` | 偿还欠息 | 资金类 | —（恒为负） | §5.4 |
| `AssetPurchase` | 资产购入 | 资金类 | —（恒为负） | §5.3 |
| `AssetSale` | 资产售出 | 资金类 | 现金 | §5.3 |
| `LoanInterestAccrued` | 贷款计息入欠息 | **事件类** | — | §5.4 |
| `FamineEntered` | 进入饥馑 | **事件类** | — | §5.4；spec US4 AS1 |
| `FamineReliefEntered` | 转入救济模式 | **事件类** | — | §5.4 |
| `FamineSevereEntered` | 进入第三阶段 | **事件类** | — | §5.4 |
| `FamineResolved` | 饥馑全部解除 | **事件类** | — | §5.4 |

**`LedgerCategoryMetadata` 的职责**（结构事实，不是平衡数值，故随枚举留在 Core，R-04）：
`KindOf(category) → LedgerEntryKind`、`CreditTargetOf(category) → LedgerCreditTarget?`——返回
`null` 表示**没有正额入账目标**，即上表「正额入账目标」列为「—」的 **9 个类别**（4 个恒为负的
支出类 + 5 个事件类）。其中**只有 4 个恒为负的支出类**（`LivingCost`、`LoanPrincipalRepaid`、
`LoanInterestRepaid`、`AssetPurchase`）在收到正额时 `FamilyEconomy.Apply` MUST 抛异常；5 个
**事件类**的金额是**事件自身的金额语义**（阶段迁移恒 0，`LoanInterestAccrued` 为利息额、**可正**），
`Apply` 只追加条目不移动资金池（与 `contracts/ledger.md` 的二分类一致）。
**SC-005 的求和口径** = 对 `KindOf == Treasury` 的条目求和。

---

## 3. 实体与聚合（`KFL.Core/Entities`）

### 3.1 `Treasury`（家族资金）

| 成员 | 类型 | 可变 | 说明 | 来源 |
| --- | --- | --- | --- | --- |
| `Cash` | `Money` | 经方法 | 现金 | §5.4 |
| `Savings` | `Money` | 经方法 | 储蓄 | §5.4 |
| `MerchantCapital` | `Money` | 经方法 | 商本池（≥100 贯才产生经商收益，门槛在 Rules） | §5.2 |
| `Loan` | `Loan` | 是 | 单笔贷款 | §5.4；R-08 |
| `SavingsRate` | `decimal?` | 是 | 当年储蓄利率，1 月 roll 出后当年不变 | §5.4 |
| `SavingsRateYear` | `int?` | 是 | `SavingsRate` 所属年份 | §5.4、R-11 |

**不变量**：三个金额 MUST `>= 0`；`SavingsRate` 非空时 MUST 与 `SavingsRateYear` 同时非空
（取值区间 `0.5%~2.4%` 的校验在 Rules——区间是规格书数值，Core 不复制）。
**方法（内部转账，均不落条目、无手续费，R-05）**：`TransferToSavings(Money)`、
`TransferToCash(Money)`、`InjectMerchantCapital(Money)`、`WithdrawMerchantCapital(Money)`；
前两者与后两者在金额超过来源池时 MUST 抛异常（不允许隐式透支）。
**写入通道**：三个金额池对 `KFL.Core` 之外**不可写**（`internal` setter），初值只经公开三参构造
`Treasury(Money cash, Money savings, Money merchantCapital)`（默认 0）给出——开局资产属阶段③
（§10.1），Core 不写数值；「只动资金池而不落条目」因此在类型层面不可表达（FR-021）。

### 3.2 `Loan`（贷款）

| 成员 | 类型 | 说明 | 来源 |
| --- | --- | --- | --- |
| `Principal` | `Money` | 本金，≥ 0 | §5.4 |
| `AccruedInterest` | `Money` | 独立「欠息」字段，≥ 0 | §5.4 |
| `MonthsSinceInterest` | `int` | 距上次计息的**自然月**数，≥ 0 | §5.4；E-07 |
| `IsSettled` | `bool`（派生） | 本金与欠息皆为 0 | §5.4 |
| `Total` | `Money`（派生） | 本金 + 欠息 | §5.4 |

**方法**
- `void AccrueInterest(Money interest)`：`AccruedInterest += interest`（**MUST NOT** 改 `Principal`，§5.4「利息永不滚入本金」）。
- `void ResetInterestClock()`：`MonthsSinceInterest = 0`。
- `LoanRepayment Repay(Money amount)`：**先本后息**，返回 `(PrincipalPart, InterestPart)`；
  `amount` MUST `<= Total`（封顶由调用方按 R-08 计算），且 MUST `>= 0`。

**为什么「先本后息」在实体里**：它是单实体的自身不变量（§5.4），不涉及跨实体编排，
故不违反章程原则 II 的「实体 MUST NOT 承担跨实体结算编排职责」。

**写入通道**：`Principal` 与 `MonthsSinceInterest` 为公开可写属性（带 `>= 0` 校验）——前者由
`PaymentPrimitive` 在现金 + 储蓄不足时加差额，后者由结算每月 +1；`AccruedInterest` **只读**，
唯一写入通道是 `AccrueInterest(Money)`，「利息永不滚入本金」因此无法被绕过。

**`LoanRepayment`**：`readonly record struct (Money PrincipalPart, Money InterestPart)`，
不变量：两部分皆 `>= 0` 且 `PrincipalPart <= 还款前本金`、`InterestPart <= 还款前欠息`。

> **贷款没有「产生」入口的公开构造**？本阶段由 `PaymentPrimitive`（§4.3）在「现金 + 储蓄
> 不足」时把差额计入 `Loan.Principal`；罚金触发入口属阶段⑥（FR-017），本阶段 MUST NOT 实现。

### 3.3 `Holdings`（资产组合）

| 成员 | 类型 | 说明 | 来源 |
| --- | --- | --- | --- |
| `FarmlandMu` | `int` | 田（亩），≥ 0 | §5.3 |
| `RuralHouses` | `int` | 农村宅（座），≥ 0 | §5.3 |
| `UrbanHouses` | `int` | 城市宅（座），≥ 0 | §5.3 |
| `Shops` | `int` | 铺面（间），≥ 0 | §5.3 |

**方法**：`int CountOf(AssetKind)`、`void Add(AssetKind, int count)`、`void Remove(AssetKind, int count)`
（移除至负数 MUST 抛异常）。
**明确不含**：价格、市值、农田上限——单价与「每人 20 亩」都在 `KFL.Rules/Config`（R-01）。

### 3.4 `Ledger`（家族级追加式流水账）

| 成员 | 类型 | 说明 | 来源 |
| --- | --- | --- | --- |
| `Entries` | `IReadOnlyList<LedgerEntry>` | 只读视图，按追加顺序（年月非降序） | §12.3 |
| `Append(LedgerEntry)` | `void` | **唯一**追加入口；既有条目 MUST NOT 被改写或删除 | FR-021 |
| `EntriesIn(GameDate from, GameDate to)` | `IReadOnlyList<LedgerEntry>` | 闭区间筛选 | §12.3 |
| `TreasuryDeltaIn(from, to)` | `Money` | 对**资金类**条目求和（SC-005 的等式左侧） | SC-005 |
| `TotalsByCategory(from, to)` | `IReadOnlyList<(LedgerCategory, Money)>` | 「收益来源明细 / 支出明细」 | §12.3 |
| `TotalsByPerson(PersonId, from, to)` | `Money` | 单个成员的区间累计（**含已归档成员**） | §12.3、FR-022 |
| `MonthlyByPerson(PersonId, from, to)` | `IReadOnlyList<(GameDate, Money)>` | 单个成员的逐月收支 | §12.3 |

**不变量**：MUST NOT 存储任何月度汇总值（FR-021、SC-008）；上述两个维度是**同一批条目的
两个查询方向**（001 R-16、spec US5 AS4）。
**性能说明（如实记录）**：聚合是线性扫描；条目数按月线性增长（001 R-16 估计 100 年约数万条），
本阶段不做索引——预置索引属「为假想需求预置结构」。

### 3.5 `FamineState`（饥馑状态）

| 成员 | 类型 | 说明 | 来源 |
| --- | --- | --- | --- |
| `Stage` | `FamineStage` | 当前阶段 | §5.4；FR-019 |
| `ElapsedMonths` | `int` | **本阶段**已持续月数（转入当月记 1） | §5.4 |

**不变量**：`Stage == None` ⇔ `ElapsedMonths == 0`。
**方法**：`void TransitionTo(FamineStage stage)`（置阶段并把 `ElapsedMonths` 置 1；仅
`TransitionTo(None)` 例外，按 `Clear()` 语义归 0——否则会破坏下面的不变量）、
`void Tick()`（阶段非 `None` 时 +1）、`void Clear()`（归 `None`/0）。
**计时时点（§17 裁决 E-14）**：`Tick()` MUST 在每月第④步的**足额判定之前**被调用一次，
消费方再用**推进后**的 `ElapsedMonths` 比较阈值——转入当月记 1，故「满 3 月」在第 3 个饥馑月
当月成立（`X` 月进入 → `X+2` 月计 3 → 转 `Relief`）。`Tick()` 本身不判阈值、不知道时限
（时限在 `FamineTimeline`）。
**形状**：`class`（非结构体）并提供**公开复制构造**，供 `SettlementResult.FamineBefore/After`
取快照——不为快照开放 setter，避免绕过不变量。
**明确不含**：3 个月与 12 个月的阈值、救济折扣、体质下降与死亡判定——阈值与折扣在
`KFL.Rules/Config/FamineTimeline`；体质与死亡属阶段⑧（FR-019）。

### 3.6 `FamilyEconomy`（经济聚合＝资金流动的唯一入口）

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `Treasury` | `Treasury` | 现金 / 储蓄 / 商本 / 贷款 / 当年储蓄利率 |
| `Holdings` | `Holdings` | 田宅铺数量 |
| `Ledger` | `Ledger` | 流水账 |
| `GrainPriceIndex` | `GrainPriceIndex` | 米价系数（可变，结构体属性） |
| `LivingStandard` | `LivingStandard` | 当前**生效**的生活费档位；**新建存档 = 普通**（2026-10-06 裁决，取值单点在 `LivingCostTable.InitialStandard`） |
| `PendingLivingStandard` | `LivingStandard?` | 待生效档位，结算第①步提升（R-09） |
| `Famine` | `FamineState` | 饥馑阶段 |
| `TreasuryPool` | `Money`（派生） | `现金 + 储蓄 + 商本`；SC-005 的右侧口径（R-05） |
| `AgeBracketCount(AgeBracket)` | — | **不在此处**：年龄档归属依赖 12/14/18/60，属 Rules（R-06） |

**核心方法（唯一写入通道）**

| 方法 | 职责 |
| --- | --- |
| `LedgerEntry Apply(LedgerCategory category, PersonId? personId, Money amount, GameDate date)` | 追加条目**并在同一次调用内**按 `LedgerCategoryMetadata` 改动资金池：正额入 `CreditTargetOf(category)`；负额按「现金 → 储蓄」扣付。资金不足 MUST 抛异常（E-06 要求调用方先算可付额） |
| `LoanRepayment RepayLoan(Money amount, GameDate date)` | 扣付资金池 + `Loan.Repay` + 落 1~2 条资金类条目（`LoanPrincipalRepaid` / `LoanInterestRepaid`，金额为 0 的部分不落条目） |
| `Money AccrueLoanInterest(Money interest, GameDate date)` | `Loan.AccrueInterest` + 落一条**事件类**条目 `LoanInterestAccrued` |
| `void EnterFamineStage(FamineStage stage, Money amount, GameDate date)` | 落对应的事件类条目（`FamineEntered` / `FamineReliefEntered` / `FamineSevereEntered` / `FamineResolved`） |

**不变量**
1. **资金池不为负**：任何 `Apply`/`RepayLoan` 结束后，`Cash`、`Savings`、`MerchantCapital`
   均 `>= 0`（FR/US4「不能默默把钱扣成负数」）。
2. **资金流动必落条目**：资金池的任何变动都只能经 `Apply` / `RepayLoan`，二者都在同一次
   调用里追加条目——「只动资金池而不落条目」在类型层面不可表达（FR-021、SC-005）。
3. `PendingLivingStandard` 仅由切换入口设置，结算第①步提升后置 `null`。
4. **构造要求显式传入当前档位、不设默认值**：`KFL.Infrastructure` 看不到 `KFL.Rules`（G-05），
   新建存档的初值（`LivingCostTable.InitialStandard` = `Normal`）必须由调用方给出，而不是由
   `KFL.Core` 里抄一份「普通」当默认值（那会让同一个规则数值出现第二个出处）。
   构造签名（`Treasury`/`Holdings`/`Ledger` 可省：`null` = 空池 / 零资产 / 空账，均为**零状态**、
   不含任何规则数值）：
   `FamilyEconomy(LivingStandard livingStandard, GrainPriceIndex grainPriceIndex, Treasury? treasury = null, Holdings? holdings = null, Ledger? ledger = null)`
   ——档位与米价系数**同为必需参数**，因为两者的初值都在 `KFL.Rules/Config`（同样的 SC-008 理由）。

### 3.7 `GameState` 的变更

| 变更 | 说明 | 来源 |
| --- | --- | --- |
| 新增 `Economy`（`FamilyEconomy`，只读） | 经济状态在存档级对象上的落点 | R-01、FR-001 |
| 构造函数新增 `economy` 参数（共 6 参） | 调用点：`GameStateFactory` + 3 处测试（R-13） | R-13 |
| 新增 `public void AdvanceMonth()` | 结算后推进一个月（跨年进位）；替代先前的 `internal set` | R-03 |
| 新增 `PendingDifficulty`（`Difficulty?`） | 难度「次月生效」的待生效位 | §11；R-09 |

> `CurrentDate` 仍**没有** public setter：推进只能经 `AdvanceMonth()`（一次一个月、跨年进位），
> 保持 §3「1 回合 = 1 游戏月」的语义不可绕过。

---

## 4. 规则层类型（`KFL.Rules`）

### 4.1 配置类（`KFL.Rules/Config/`）

| 类 | 覆盖的数值 | 规格书章节 |
| --- | --- | --- |
| `GameConfig` | 唯一数值真源的**总入口**（聚合各子表，供核对） | §1、§5 |
| `LivingCostTable` | 三档 × 四年龄档日耗（拮据 20/7/14/5、普通 25/8.5/17.5/5、体面 30/10/21/5）、**新建存档的初始档位**（`InitialStandard` = `Normal`，2026-10-06 裁决）、**农出身独立乘区**（成年 0.90 / 未成年 0.80）、**生活费一般乘区**的修正项列表（未成年 −50%、救济期 −20%，加算） | §5.1、§5.4、§10.1；R-17 |
| `AgeBracketPolicy` | 成年边界（男 12 / 女 14）、青年上界 18、老人下界 60；`Of(性别, 年龄) → AgeBracket` | §5.1、§4.3 |
| `IncomeRateTable` | 自耕 0.5 贯/亩/年、每亩上限 20、田租 0.1 贯/亩/年、务农 2 贯/月、做工 1.5 贯/月、城市宅 +1 贯/月、经商 2% 与门槛 100 贯、商出身 ×1.1、农/工/商 除数 200/400 | §5.2 |
| `AssetPriceTable` | 田 1 / 农村宅 10 / 城市宅 100 / 铺面 300 贯；铺面年租 20% | §5.3 |
| `InterestPolicy` | 储蓄贷款利率区间 0.5%~2.4%、计息周期 12 月 | §5.4 |
| `FamineTimeline` | 饥馑 3 月转救济、救济 12 月、救济折扣 20% | §5.4 |
| `GrainPricePolicy` | 初始 1.0、游走幅度 10%、clamp 0.7~3.0、米价派生值 `米价 = (系数 − 0.4) ÷ 0.6` | §5.1 |
| `DifficultyRates` | 四难度的收益/支出/贿赂风险/负面事件系数 + 查表 | §11 |
| `SalaryTable` | 18 级年俸（L1=5100 … L18=72）、月摊 = ÷12、士出身当官 ×1.05 | §8.1 |
| `LoanPolicy` | 划扣比例 仕 20% / 工农 40% / 商 80% 的判定 | §5.4、§10.2 |

**约束**：每个数值 MUST 只在这些类里出现一次（SC-008）；`contracts/config-registry.md`
是「数值 ↔ 章节 ↔ 成员」的核对表，也是该约束的验证物。

### 4.2 `SettlementResult`（一次结算的可断言快照，`KFL.Rules/Settlement`）

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `Month` | `GameDate` | 本次结算的月份（结算后 `CurrentDate` 已是下月） |
| `GrainPriceIndexBefore` / `After` | `decimal` | 米价系数的游走前后 |
| `LivingCosts` | `IReadOnlyList<LivingCostLine>` | 逐年龄档的日耗、人数与小计（US1 AS1） |
| `LivingCostPayable` / `LivingCostPaid` | `Money` | 应付额（含救济折扣）与实际扣付额（E-06） |
| `Incomes` | `IReadOnlyList<IncomeLine>` | 各来源分项：类别 + 归属成员（可空）+ 金额 |
| `NetProfit` | `Money` | = 收入合计 − 生活费支出（E-04） |
| `LoanInterestAccrued` | `Money` | 本月计息额（含 12 月节点是否命中） |
| `LoanRepayment` | `LoanRepayment` | 本月划扣的本金/欠息拆分 |
| `SavingsInterest` / `ArtisanBonus` | `Money` | 只可能出现在 12 月（工 bonus 需工出身且基数为正） |
| `FamineBefore` / `FamineAfter` | `FamineState` 快照 | 阶段迁移（US4） |
| `Entries` | `IReadOnlyList<LedgerEntry>` | 本次结算产生的**全部**条目（含事件类） |
| `TreasuryPoolBefore` / `After` | `Money` | 资金池前后值，供 SC-005 直接断言 |

**不变量**：`Entries` MUST 与本次结算向 `Ledger` 追加的条目**逐条相同**；
`TreasuryPoolAfter − Before` MUST 等于 `Entries` 中资金类条目之和（**这条不变量就是 SC-005**，
在任何一次结算上都必须成立）。`SettlementResult` MUST NOT 被写回 `GameState`
（FR/US：资金池是唯一余额真源）。

### 4.3 计算器与入口（全部纯函数或只依赖注入的接缝）

| 类型 | 公开形状 | 职责 |
| --- | --- | --- |
| `MonthlySettlementEngine` | `ctor(IRandomService, IGameClock)`；`SettlementResult Settle(GameState)` | 唯一编排入口：按契约「月度结算」§1 的六步顺序执行，就地推进 `GameState` 与 `CurrentDate` |
| `LivingCostCalculator` | `static ... Compute(计口成员, LivingStandard, 米价系数, 难度, 出身, FamineStage)` | §5.1/§5.4 生活费与逐档明细（四个乘区，R-17）；**不碰**资金池 |
| `IncomeCalculator` | `static ... Compute(计口成员, Holdings, Treasury, HasShiStatus, 难度, 出身)` | §5.2 各来源分项与归属成员；**不碰**资金池。成员集合 = **计口 ∧ 成年**（E-16，成年按 §4.3）；城市宅加成按人归属并逐人乘本人 `(1+工/400)`（E-15）；`TradeIncome` 归属被采用的那名成员（E-18） |
| `SavingsSettlement` | `static decimal RollRate(IRandomService)`；`static Money Accrue(Money savings, decimal rate)` | §5.4 的 1 月 roll 与 12 月计息 |
| `LoanSettlement` | `static bool IsInterestDue(Loan)`；`static decimal RollRate(IRandomService)`；`static Money ComputeRepayment(Money netProfit, bool hasShiStatus, Origin origin, Loan)` | §5.4 的计息节点与划扣额（含封顶） |
| `FamineController` | `static FamineDecision Evaluate(FamineState, Money payable, Money pool)` | §5.4 四阶段流转与「解除优先」（R-12）；`pool` 是**可付额**（现金 + 储蓄，不含商本，见 R-05），不是 SC-005 求和的资金池 |
| `AssetMarket` | `static ... Buy/Sell(GameState, AssetKind, int count)` | §5.3 田宅铺买入口（购售同价） |
| `PaymentPrimitive` | `static PaymentResult Pay(FamilyEconomy, Money amount)` | FR-017「现金 → 储蓄 → 余额转贷款」；本阶段无罚金调用方 |

---

## 5. 需求 → 模型映射

| 需求 | 落在哪 | 验证方式 |
| --- | --- | --- |
| FR-001、SC-008 | `Treasury` / `Holdings` / `Loan` / `Money`（文为单位） | 字段边界测试 + 配置登记表核对 |
| FR-002、FR-003、FR-004 | `AgeBracketPolicy` + `LivingCostTable`（四个乘区，R-17）+ `GrainPricePolicy` | 逐年龄档断言、边界日（12 岁男 / 14 岁女）、clamp 0.7/3.0 与连续越界回弹；农出身与一般乘区的四种组合（成年/未成年 × 农/非农）各一条断言；农出身未成年人处于救济期时的一般乘区 = 0.3 |
| FR-005（次月生效） | `PendingDifficulty` / `PendingLivingStandard` / `LivingCostTable.InitialStandard` | 同月切换 → 当月不变、次月变（US1 AS6）；新建存档的初始档位 = `Normal`（一条断言） |
| FR-006、FR-011、FR-012 | `IncomeCalculator` + `SalaryTable` + `DifficultyRates` | 逐来源分项断言；18 级锚点 72/420/5100；储蓄利息**不**乘收益系数 |
| FR-007、FR-008、FR-009 | `IncomeRateTable` + `IncomeCalculator`（E-02 的触发口径） | 20 亩上限与超出转田租；城市宅 +1 贯（**份数 × 按人归属 × 本人乘数**三条各一断言，E-15）；商本 100 贯边界；无田时务农 2 贯且与自耕互斥；做工需指派；**可指派人群 = 计口 ∧ 成年**（青年/老人可、未成年不可，E-16）；**`TradeIncome` 归属被采用者**（`PersonId != null`，E-18） |
| FR-010、FR-013、FR-015 | `AssetPriceTable` / `InterestPolicy` / `LoanPolicy` | 铺面月摊 5 贯（300×20%÷12）；1 月 roll、12 月计息、次年重 roll；20/40/80 各一条断言 |
| FR-014、FR-016 | `Loan` + `LoanSettlement` + `InterestPolicy` | 先本后息；跨 12 月节点按当时本金计息；本金清零后转冲欠息；皆清即结清；计时按自然月（E-07）；任意金额手动提前还款（`FamilyEconomy.RepayLoan` 承载） |
| FR-017 | `PaymentPrimitive` | 「现金 → 储蓄 → 余额转贷款」三步各一断言；本阶段无罚金入口 |
| FR-018、FR-019 | `FamineState` + `FamineController` + `FamineTimeline` | 4 阶段转移 4/4 + 解除后计时归零；**「恰好第 3 月转救济 / 恰好第 12 月转 Severe」两条边界断言**（E-14）；阶段与剩余月数可读 |
| FR-020 | `MonthlySettlementEngine` | 六步顺序契约（E-04）；越界步骤（随机事件/成长/科举/绝嗣）**不存在** |
| FR-021、FR-022、SC-005 | `FamilyEconomy.Apply` / `RepayLoan` + `Ledger` | 资金池变动与条目一一对应；家族/角色两维度聚合同一批条目；归档成员历史可读 |
| FR-023 | `KFL.Rules/Config/` 全部配置类 | 配置登记表 + 「配置类之外无第二份副本」扫描测试 |
| FR-024、FR-025、SC-006、SC-007 | `MonthlySettlementEngine` 的注入与随机消费次序 | 同种子复跑逐位相同；G-07 静态断言（扫描范围含新 `Rules/` 目录） |
| FR-026 | `LoanTests` / `FamineTimelineTests` / `SalaryTableTests` | §16 必测三项逐条 |
| FR-027 | `AssetMarket` | 买入/售出后 `Holdings` 与资金池同价变动 |
| FR-028 | ——（本阶段 MUST NOT 实现） | 「买人口」相关类型在阶段② 不存在，以「无该类型/无该类别」断言 |
| FR-029、FR-030、SC-009 | `LivingCostCalculator` / `IncomeCalculator` 的**计口筛选** | 服刑 + 外嫁同夹具：贡献为 0、档案与历史条目仍可读；待阙**照常**计入 |
| FR-031 | `IncomeRateTable` 的务农系数 + `IncomeCalculator` + `CountedMembers` | `tests/KFL.Tests/Rules/IncomeTests.cs`；**「无田可耕」= `FarmlandMu == 0`** 与「有田即不得发务农」各一断言（E-17） |
| SC-001 | `MonthlySettlementEngine` + `SettlementResult` | `tests/KFL.Tests/Rules/SettlementEngineTests.cs`（quickstart S1~S3/S6） |
| SC-002 | `LoanSettlement` + `LoanPolicy` | `tests/KFL.Tests/Rules/LoanTests.cs` |
| SC-003 | `FamineController` + `FamineTimeline` | `tests/KFL.Tests/Rules/FamineTimelineTests.cs` |
| SC-004 | `SalaryTable` | `tests/KFL.Tests/Rules/SalaryTableTests.cs` |

---

## 6. 状态转移

### 6.1 饥馑（§5.4，R-12）

| 起始阶段 | 条件 | 结果 | 事件类条目 |
| --- | --- | --- | --- |
| `None` | 现金 + 储蓄 < 当月应付额 | `Famine`（计时 1） | `FamineEntered` |
| `Famine` | 推进后计时 ≥ 3（第 3 个饥馑月当月）仍未足额付清 | `Relief`（计时从 1 起重算） | `FamineReliefEntered` |
| `Relief` | 推进后计时 ≥ 12（第 12 个救济月当月）仍未足额付清 | `Severe`（计时从 1 起重算） | `FamineSevereEntered` |
| `Severe` | —— | 保持 `Severe` | — |
| 任一阶段 | 现金 + 储蓄 ≥ 当月应付额 | `None`（计时清零） | `FamineResolved` |

**判定次序**：先判「付得起 → 全部解除」，再判升级（R-12）。救济期内应付额 = 正常档 ×
`(1 − 20%)`；其余阶段为正常档。
**计时（E-14）**：每月在④的足额判定**之前**先 `Tick()`（阶段非 `None` 时 +1），阈值用**推进后**
的 `ElapsedMonths` 比较——`Famine` 计 `≥ 3` 即转 `Relief`（`X` 月进入 → `X+2` 月当即转，
`−20%` 自 `X+3` 月的应付额起生效），`Relief` 计 `≥ 12` 即转 `Severe`。因此
「恰好第 3 月 / 恰好第 12 月」是本阶段 MUST 有的边界断言（第 2 月与第 11 月 MUST NOT 触发）。

### 6.2 贷款（§5.4，R-08）

| 状态 | 触发 | 结果 |
| --- | --- | --- |
| 无贷款 | `PaymentPrimitive` 遇现金 + 储蓄不足 | `Principal += 差额`（本阶段唯一产生路径；罚金属阶段⑥） |
| `MonthsSinceInterest` 0→12 | 每次结算第⑤步（**先计息**） | `AccruedInterest += 当时本金 × 新 roll 利率`，计数归零，落事件类条目 |
| 净利润 > 0 且未结清 | 每次结算第⑤步（**后划扣**） | 划扣 `min(净利润 × 比例, Total)`，先本后息，落 1~2 条资金类条目 |
| `Principal == 0` 且 `AccruedInterest == 0` | 划扣后 | `IsSettled`，此后不再产生划扣条目 |

### 6.3 其他按月转移

| 对象 | 转移 |
| --- | --- |
| `GrainPriceIndex` | 每月第②步游走 ±10% 后 clamp 0.7~3.0（当月生效，US1 AS5） |
| `PendingDifficulty` / `PendingLivingStandard` | 每月第①步提升为生效值并清空待生效位（R-09） |
| `Treasury.SavingsRate` | 1 月重 roll（与上年无关），当年内不变；12 月计息并入储蓄本金（R-11） |
| `FamineState.ElapsedMonths` | 阶段非 `None` 时每月 +1（`TransitionTo` 的当月记为 1）；**调用点在④的足额判定之前**，阈值用推进后的值比较（E-14） |
| `GameState.CurrentDate` | 结算末尾 `AdvanceMonth()`（跨年进位） |

### 6.4 本阶段**不建立**的状态转移

属性成长/衰老/疾病/死亡（阶段⑧）、科举季触发与功名变迁（阶段⑤）、罚金与服刑计时（阶段⑥）、
待阙转授官与考课/致仕（阶段⑧）、婚育与外嫁归档（阶段⑧）、绝嗣判定（阶段⑪）、
随机事件与米价灾年跳涨（阶段⑧）、存档落盘与成就（阶段④/⑪）。上述项在本阶段的模型里
**没有对应的写入通道**，因此不是「暂未实现」，而是「不可达」。
