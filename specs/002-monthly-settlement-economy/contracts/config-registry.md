# 契约五：配置数值登记表（SC-008 的验证物）

**Feature**: `002-monthly-settlement-economy` | **Date**: 2026-10-06

SC-008 要求「§5.1、§5.2、§5.3、§5.4、§8.1、§11 中与本阶段相关的每一项公式与数值，
都能在配置类中定位到唯一出处，且配置类之外不存在同一数值的第二份副本」。
本表是**核对表**：每一行给出「数值 → 规格书章节 → 配置成员 → 断言锚点」。
数值的唯一出处是 `KFL.Rules/Config/` 下的常量；本表与源码 MUST 同步，MUST NOT 成为第二真源
（表中数值仅为可读性副本，冲突时以源码为准，并由同步测试兜底）。

「配置成员」列统一写成**数值总表入口** `GameConfig` 的路径（T058，data-model §4.1）：
`GameConfig` 只做逐成员转发，本身不含任何数值字面量，故 KFL.Rules/Config/ 内的常量仍是唯一出处。

## 1. 生活费（§5.1、§5.4、§10.1；R-17）

| 数值 | 配置成员 | 断言锚点 |
| --- | --- | --- |
| 三档日耗 成人/青年/老人/儿童：拮据 20/7/14/5、普通 25/8.5/17.5/5、体面 30/10/21/5 | `GameConfig.LivingCost.DailyCost[档位][年龄档]` | 12 条逐格断言 |
| 月 = 日耗 × **30** | `GameConfig.LivingCost.DaysPerMonth` | 单成员单档断言 |
| 成年边界 男 **12** / 女 **14**；青年上界 **18**；老人下界 **60** | `GameConfig.Ages`（`MaleAdulthoodAge` / `FemaleAdulthoodAge` / `YouthUpperAge` / `ElderLowerAge` / `Of`） | 边界日断言（12 岁男生日、14 岁女生日、18 与 60 岁） |
| 农出身独立乘区 成年 **0.90** / 未成年 **0.80** | `GameConfig.LivingCost.FarmerMultiplierOf` | 成年/未成年 × 农/非农 四种组合 |
| 生活费一般乘区修正项 未成年 **−0.50**、救济期 **−0.20**（加算） | `GameConfig.LivingCost.GeneralZoneModifiers` / `GeneralZoneFactor` | `1 − 0.5 − 0.2 = 0.3`（农出身未成年 + 救济）与 `0.5`（非农未成年） |
| 米价系数 初始 **1.0**、游走 **±10%**、clamp **0.7~3.0**、米价派生值 `米价 = (系数 − 0.4) ÷ 0.6` | `GameConfig.GrainPrice`（`Initial` / `WalkAmplitude` / `Min` / `Max` / `PriceOffset` / `PriceScale` / `Walk` / `MarketPrice`） | 边界 clamp 与连续越界回弹；派生值换算 |
| 难度支出系数 简单/普通/困难/地狱 **0.6/1.0/1.1/1.3** | `GameConfig.DifficultyRate.ExpenseFactor` | 四难度各一条 |

## 2. 收入（§5.2、§5.3、§8.1、§11；R-18/R-19）

| 数值 | 配置成员 | 断言锚点 |
| --- | --- | --- |
| 自耕 亩产率 **0.5 贯/亩/年**、每人上限 **20 亩** | `GameConfig.Income.SelfFarmingGuanPerMuPerYear` / `FarmlandPerCapitaMu` | 40 亩 = 1.667 贯/月；第 21 亩转田租 |
| 田租 亩产率 **0.1 贯/亩/年** | `GameConfig.Income.LandRentGuanPerMuPerYear` | 与自耕同夹具对照 |
| 务农 **2 贯/月**（家族**无田可耕 = `FarmlandMu == 0`** 时，每名**计口成年成员**） | `GameConfig.Income.FarmingWageGuanPerMonth` | 有田/无田两分支；与自耕互斥；**有田未占满容量不发务农**（E-17） |
| 做工 **1.5 贯/月**、城市宅 **+1 贯/月** | `GameConfig.Income.CraftingGuanPerMonth` / `UrbanHouseCraftingBonusGuanPerMonth` | 做工人数 × 1.5；城市宅加成 = `min(做工人数, 城市宅数)` **份，按本人工乘数降序逐人归属并各乘本人乘数**（E-15） |
| 经商 **2%**、门槛 **100 贯**、商出身 **×1.1** | `GameConfig.Income.TradeProfitRate` / `TradeCapitalThresholdGuan` / `MerchantOriginMultiplier` | 99/100 贯两边界；商出身加成 |
| 天赋除数 农 **200**、工 **400**、商 **200** | `GameConfig.Income.AgricultureTalentDivisor` / `CraftTalentDivisor` / `CommerceTalentDivisor` | 各来源一条 |
| 铺面年租 **20%** | `GameConfig.Assets.ShopRentRate` / `ShopMonthlyRent` | 300 × 20% ÷ 12 = 5 贯/月 |
| 工出身 bonus **6%** | `GameConfig.Income.ArtisanBonusRate` | 基数为正/为 0/为负三分支 |
| 官俸 18 级 **5100/4250/3560/2980/2490/2090/1750/1460/1220/1020/860/720/600/500/420/235/130/72** | `GameConfig.Salary.AnnualSalaryGuan` | 逐级断言 + 锚点 72/420/5100（SC-004） |
| 俸禄月摊 **÷12**、士出身当官 **×1.05** | `GameConfig.Income.MonthsPerYear` / `GameConfig.Salary.ScholarOriginMultiplier` | 士/非士对照 |
| 难度收益系数 **1.4/1.0/0.9/0.8** | `GameConfig.DifficultyRate.RevenueFactor` | 四难度各一条；储蓄利息**不**乘（负例断言） |

## 3. 资产（§5.3）

| 数值 | 配置成员 | 断言锚点 |
| --- | --- | --- |
| 田 **1 贯/亩**、农村宅 **10 贯**、城市宅 **100 贯**、铺面 **300 贯**（购售同价） | `GameConfig.Assets.UnitPrice` | 买/卖各一条，且卖出不再产生对应收入 |

## 4. 现金、储蓄、贷款（§5.4、§10.2；R-08/R-11）

| 数值 | 配置成员 | 断言锚点 |
| --- | --- | --- |
| 储蓄利率区间 **0.5%~2.4%**、1 月 roll、当年不变 | `GameConfig.Interest.SavingsRate` | 区间两端（`NextDouble()` 取 0 与极大） |
| 贷款利率区间 **0.5%~2.4%**、计息周期 **12 月** | `GameConfig.Interest.LoanRate` / `InterestPeriodMonths` | 12 月节点命中一次、第 13 月不重复 |
| 划扣比例 仕 **20%** / 工农 **40%** / 商 **80%** | `GameConfig.Loan.RepaymentRatio` | 三档各一条（SC-002） |
| 饥馑 **3 月** 转救济、救济 **12 月**、救济折扣 **20%** | `GameConfig.Famine`（`MonthsUntilRelief` / `ReliefMonthsUntilSevere` / `ReliefExpenseDiscount`） | 4/4 阶段转移（SC-003） |
| 支付原语顺序 现金 → 储蓄 → 余额转贷款 | `PaymentPrimitive`（无魔数，结构即契约） | 三步各一条 |

## 5. 数值唯一性条款

1. 上述数值 MUST 只出现在 `KFL.Rules/Config/` 内的常量声明处；**MUST NOT** 出现在
   `KFL.Core`（实体只存状态与自身值域）、ViewModel、测试期望之外的任何位置。
2. 测试 MUST 通过配置成员取期望值，**MUST NOT** 复制字面量——只有锚点断言（如 72/420/5100）
   允许写字面量，因为「锚点」本身就是被验证对象。
3. 同步测试：扫描 `src/` 与 `tests/KFL.Tests/{Core,Infrastructure,Fixtures,Rules}/` 的源码，
   按**两条互补判据**断言数值的唯一出处（实现：`tests/KFL.Tests/Architecture/ConfigLiteralRules.cs`）：
   - **数值清单判据**：清单内的数值**只**在 `src/KFL.Rules/Config/` 的 `.cs` 里以字面量出现。
     清单＝小数全收 + 4 位及以上官俸 + 实测零撞车的低品官俸（860/720/235/130/72）；
     宅价 1/10/100/300、门槛 100、亩数 20 等整数**不入清单**——它们与成员编号/年龄/夹具金额同值，
     入单会产出数百处无关命中（详见 `ConfigLiteralTests` 类注释与 tasks.md 的 T059 追记）。
   - **金额条款判据**：产品源码（`src/KFL.Core`、`KFL.Infrastructure`、`KFL.Rules`，`Config/` 除外）
     MUST NOT 把裸数值字面量喂给 `Money.FromGuan` / `Money.FromWen`——该判据按**金额构造点**
     而非按数值识别，故恰好覆盖上一条收不进的整数规则值（如 `Money.FromGuan(300m)`）。
     测试目录不在其范围内（夹具金额属条款 ⑤）。
4. 米价派生值的系数（**0.4** 与 **0.6**）同属本表，MUST NOT 写进 `GrainPriceIndex`。
5. `GameConfig`（数值总表入口）MUST 只转发既有成员，MUST NOT 声明自己的数值字面量：
   它是**视图**，不是第二出处。

## 6. 003 新增数值（只指向，不复制）

本节由 `003-opening-assets-officialdom` 追加：本表 §7「本契约明确不包含」中的**开局资产业务**
（§10.1，逻辑轨 ③）与**官吏体系数值**（§8.2，逻辑轨 ③）已由 003 落地。按契约分工，本表
**只登记名称与指向**，**MUST NOT 复制数值本身**——数值的唯一出处仍是 003 契约八
（[`specs/003-opening-assets-officialdom/contracts/config-registry.md`](../../003-opening-assets-officialdom/contracts/config-registry.md)）
所指向的 `KFL.Rules/Config/` 常量，冲突时以源码为准。

| 新增项 | 003 契约八的位置与配置成员 |
| --- | --- |
| 开局 §10.1 四出身资产（初始现金 / 初始田 / 农村宅 / 城市宅 / 商本） | 该契约 **§1 开局**：`GameConfig.NewGame.InitialCashGuan` / `InitialFarmlandMu` / `InitialRuralHouses` / `InitialUrbanHouses` / `InitialMerchantCapitalGuan` |
| 开局成员口径（配偶人数、孩子人数按出身） | 该契约 **§1 开局**：`GameConfig.NewGame.SpouseCount` / `ChildCount` |
| 开局年龄口径（家主、士出身家主、配偶、孩子） | 该契约 **§1 开局**：`GameConfig.NewGame.HeadAge` / `SpouseAge` / `ChildAge`（区间常量在 `OriginStartTable.HeadAgeMean` 一族 / `SpouseAgeMean` / `ChildAgeMin` / `ChildAgeMax`） |
| 天赋与寿数分布（天赋、无父母参照的学业/体质、天命寿数按性别） | 该契约 **§1 开局**：`GameConfig.Attributes.TalentMean` / `TalentSigma` / `StudyMean` / `StudySigma` / `HealthMean` / `HealthSigma` / `LifespanMean(gender)` / `LifespanSigma(gender)` 与取样 `NextTalent` / `NextStudy` / `NextHealth` / `NextLifespan` |
| 官吏体系：待阙期区间 | 该契约 **§2 官吏体系**：`GameConfig.Career.AwaitingPostMinMonths` / `AwaitingPostMaxMonths` |
| 官吏体系：初始官阶按甲第/特奏名映射 | 该契约 **§2 官吏体系**：`GameConfig.Career.InitialRankOf` |
| 官吏体系：政绩月增与上限 | 该契约 **§2 官吏体系**：`GameConfig.Career.MeritPerMonth` / `MeritMaximum` |
| 官吏体系：考课周期 | 该契约 **§2 官吏体系**：`GameConfig.Career.AppraisalPeriodMonths` |
| 官吏体系：升级率基础值、政绩加成与封顶 | 该契约 **§2 官吏体系**：`GameConfig.Career.PromotionBaseChance` / `PromotionChancePerMerit` / `PromotionChanceCap` / `PromotionChance` |
| 官吏体系：致仕年龄与半俸比例 | 该契约 **§2 官吏体系**：`GameConfig.Career.RetirementAge` / `RetirementSalaryRatio` |

**沿用项**（俸禄 18 级与月摊、士出身 ×1.05、难度收益系数、开局生活费档位、开局米价系数）
002 已在本表登记，003 **不复制、不重登记**，只在 003 契约八 **§2 官吏体系** 中复核锚点。

## 7. 本契约明确不包含

§6 科举与 §7 贿赂的全部数值（阶段⑤/⑥）、§9 婚育与买人口数值（逻辑轨 ⑦）、
§13 调试控制台上限（界面轨 U4）、§14 成就与存档（阶段④/逻辑轨 ⑧）。

> 后记（2026-10-08）：本表**原有**的两项排除内容——§8.2 的考课/政绩/致仕数值与 §10.1 的
> 开局资产发放（逻辑轨 ③）——**已由 `003-opening-assets-officialdom` 交付**，故自本节移出，
> 改由 §6 登记指向（只指向，不复制）。本节余下的排除项不变。
