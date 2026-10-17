# 契约二十一：家族生命周期数值登记表（007 · 逻辑轨 ⑦）

**Feature** | **Date**: 2026-10-17 | **Spec**: [../spec.md](../spec.md) | **Plan**: [../plan.md](../plan.md)

> 本契约是「数值在哪、由谁签发、如何被守卫」的登记表，**不是**数值真源。
> 数值真源 = 《科举浮生录规格书》§4.1~§4.4、§5.4、§9.1~§9.5 与 §17 的六项裁决（2026-10-17）。
> 上游契约：[契约十七：惩罚数值登记表](../../006-bribery-punishment/contracts/config-registry.md)。

## 1. 成长与体质登记（`src/KFL.Rules/Config/GrowthPolicy.cs`）

**声明处**：`src/KFL.Rules/Config/GrowthPolicy.cs`。`GameConfig` 只增一个转发视图 `GameConfig.Growth`，
**MUST NOT** 自带任何数值字面量。

| 成员 | 值 | 类型 | 出处 | 用途 |
| --- | --- | --- | --- | --- |
| `StudyPerMonth` | 1.5m | decimal | §4.3 | 读书成员每月学业 |
| `TutorStudyBonusPerMonth` | 2.25m | decimal | §4.3 | 延名师额外学业 |
| `TutorFeeGuanPerMonth` | 5 | int | §4.3 | 名师月费（再乘支出系数） |
| `ScholarStudyMultiplier` | 1.10m | decimal | §4.3 | 士出身藏书加成 |
| `ForgetPerMonth` | 0.5m | decimal | §4.3 | 遗忘扣减（正值，内部取负） |
| `ForgetAgeThreshold` | 16 | int | §4.3 | 「16 岁以上」严格大于 |
| `IllnessStudyPenaltyDivisor` | 2m | decimal | §4.3 | 患病期学业减半 |
| `HealthRecoverPerMonth` | 1 | int | §5.4 | 健康月体质 +1 |
| `FamineHealthLossPerMonth` | 10 | int | §5.4 | 饥馑严重期体质 −10 |
| `FamineDeathChance` | 0.05m | decimal | §5.4 | 严重期每人每月死亡判定 |

**口径**

1. 唯一声明处：上表成员 MUST 只出现在本文件。
2. 门面只转发：`GameConfig.Growth.Xxx` 一律 `=> GrowthPolicy.Xxx`。
3. 复用不另立：`AttributeLimits.Max`（100）MUST 复用，本表 MUST NOT 再声明上限。
4. 同源常量不复制：`AgeBracketPolicy` 的成年边界（男 12 / 女 14）MUST 经 `GameConfig.Ages` 读取。
5. 规格书变更须同步四件：本表、`GrowthPolicy`、`data-model.md` 的映射行、`tasks.md` 的回写登记。

## 2. 疾病登记（`src/KFL.Rules/Config/IllnessPolicy.cs`）

| 成员 | 值 | 类型 | 出处 | 用途 |
| --- | --- | --- | --- | --- |
| `InfectionChanceAtFullHealth` | 0.0025m | decimal | §4.3 Q4 | 体质 100 时月患病率 0.25% |
| `InfectionChanceAtFloorHealth` | 0.40m | decimal | §4.3 Q4 | 体质 1 时月患病率 40% |
| `InterpolationSpan` | 99 | int | §4.3 Q4 | 插值分母 `100 − 1` |
| `HealthFloor` / `HealthCeiling` | 1 / 100 | int | §4.3 Q4 | 概率入参 clamp |
| `TalentMultiplierFarmer`（`m_p`/`m_q`） | 0.80m / 1.20m | decimal | §4.3 Q4 | 农 |
| `TalentMultiplierScholar` | 0.95m / 1.10m | decimal | §4.3 Q4 | 仕 |
| `TalentMultiplierArtisan` | 1.10m / 1.10m | decimal | §4.3 Q4 | 工 |
| `TalentMultiplierMerchant` | 1.05m / 0.95m | decimal | §4.3 Q4 | 商 |
| `SeverityLossBase` / `SeverityLossSpan` | 2 / 8 | int | §4.3 Q2 | 每月扣减 `2 + 8s` |
| `MedicineBaseGuan` / `MedicineSeverityGuan` | 0.05m / 0.45m | decimal | §4.3 Q2 | 药费 `(0.05 + 0.45s) 贯` |
| `BaseRecoveryCoefficient` | 1.00m | decimal | §4.3 Q6 | 未治疗清除 `体质 × 1.00 / 100` |
| `TreatedRecoveryCoefficient` | 1.10m | decimal | §4.3 Q6 | 疗程式系数 |
| `TreatedRecoveryOffset` | 0.20m | decimal | §4.3 Q6 | 疗程式加项 |
| `TreatmentMonths` | 3 | int | §4.3 Q2 | 疗程月数 |
| `ClearBonusChild` | 0.60m | decimal | §4.3 Q5 | `3 <= AgeAt < 12` |
| `ClearBonusYouth` | 0.30m | decimal | §4.3 Q5 | `12 <= AgeAt < 24` |
| `ClearBonusBoundaryChild` / `Youth` / `Adult` | 3 / 12 / 24 | int | §4.3 Q5 | 三段边界（满岁当月切换） |
| `IllnessExemptAge` | 3 | int | §4.3 Q5 | `AgeAt < 3` 免病且零随机 |
| `SeverityBasisPointsScale` | 10000 | int | 本特性 R-03 | `s` → 万分之整数的换算单点 |

**口径**

1. `m_p`/`m_q` **乘算**且 MUST NOT 二次封顶；主导天赋 = 四项最高，并列按 **农 → 商 → 仕 → 工**。
2. 患病概率 MUST NOT 引用 `DifficultyRates.NegativeEventFactor`——本类 MUST NOT 出现该符号。
3. `min(1, …)` 的钳位 MUST 在**掷骰之前**完成，钳到 1 ⇒ 直接必愈、不消费随机。
4. `SeverityBasisPointsScale` 是 `s` 与存档字段 `IllnessSeverityBasisPoints` 的唯一换算点。

## 3. 婚配与生育登记（`MarriagePolicy.cs` / `ChildbirthPolicy.cs` / `PopulationPurchasePolicy.cs`）

### 3.1 `src/KFL.Rules/Config/MarriagePolicy.cs`

| 成员 | 值 | 类型 | 出处 | 用途 |
| --- | --- | --- | --- | --- |
| `MatchmakerFeeGuan` | 5 | int | §9.1 | 托媒费（再乘支出系数） |
| `MatchmakerSuccessChance` | 0.50m | decimal | §9.1 | 单次成功判定 |
| `CandidateAgeSpread` | 5 | int | §9.1 | 对象年龄差上限 |
| `ProposalBaseChance` | 0.05m | decimal | §9.1 Q1 | 随机提亲 base（再乘负面事件系数） |
| `ImperialProposalBaseChance` | 0.30m | decimal | §9.2 Q1 | 捉婿 base（再乘系数、除 `1+P`） |
| `ImperialPaymentMinGuan` / `MaxGuan` | 500 / 1000 | int | §9.2 Q5 | `Next(500, 1001)` |
| `PressureWeightBottom` | 0.02m | decimal | §9.2 Q5 | `w(18)` |
| `PressureWeightTop` | 1.50m | decimal | §9.2 Q5 | `w(1)` |
| `PressureHighestLevel` / `LowestLevel` | 1 / 18 | int | §9.2 Q5 | `OfficialRank.Level` 边界（L1 最高） |
| `SpouseDegreeScholarThreshold` | 1m | decimal | §9.2 Q5 | `P >= 1` ⇒ 举人 |
| `SpouseDegreeTributeThreshold` | 3m | decimal | §9.2 Q5 | `P >= 3` ⇒ 贡士（**MUST NOT 进士**） |
| `DegreeMarriageFeeRatio` | 1.5m | decimal | §9.3 | 买功名 = 年俸 × 150% |
| `TributeSpouseStudyMean/Sigma` | 60 / 10 | int | §9.3 | 贡士配偶学业重掷 |
| `JuRenSpouseStudyMean/Sigma` | 50 / 10 | int | §9.3 | 举人配偶学业重掷 |
| `CommonerGiftGuan` / `CommonerDowryGuan` | 10 / 20 | int | §9.1 | 平民档 |
| `ScholarGiftGuan` / `ScholarDowryGuan` | 20 / 30 | int | §9.1 | 士人档 |
| `OfficialGiftGuan` / `OfficialDowryGuan` | 50 / 100 | int | §9.1 | 仕族档 |
| `ClassWealthThresholdLow` / `HighGuan` | 100 / 1000 | int | §9.1 Q1 | 阶层加权分档（总资产） |
| `ClassWeightsPoor` / `Middle` / `Rich` | 0.70/0.25/0.05、0.45/0.40/0.15、0.20/0.50/0.30 | decimal | §9.1 Q1 | 三档权重（平民/士人/仕族） |

### 3.2 `src/KFL.Rules/Config/ChildbirthPolicy.cs`

| 成员 | 值 | 类型 | 出处 | 用途 |
| --- | --- | --- | --- | --- |
| `ChildbirthExpenseGuan` | 10 | int | §9.4 | 生育申请费（再乘支出系数） |
| `FertilityAgeMin` / `Max` | 16 / 45 | int | §9.4 | 女方窗口（含端点） |
| `ChildbirthDeathChance` | 0.05m | decimal | §9.4 | 分娩死亡（简单 0.025 / 地狱 0.07） |
| `InheritanceSigmaBase` | 5 | int | §4.2 | `σ = abs(父 − 母) / 2 + 5` |
| `StudyMeanHalving` | 0.5m | decimal | §4.2 | 学业均值折半 |
| `InheritedHealthFloor` | 20 | int | §4.2 | 遗传体质下限 |
| `InfantDeathChance` | 0.10m | decimal | §9.4 Q3 | 单次夭折（简单 0.05 / 地狱 0.12） |
| `InfantCheckCount` | 3 | int | §9.4 Q3 | 出生当月 + 1 岁 + 2 岁 |
| `InfantExemptAge` | 3 | int | §9.4 Q3 | 满 3 岁不再判定 |

### 3.3 `src/KFL.Rules/Config/PopulationPurchasePolicy.cs`

| 成员 | 值 | 类型 | 出处 | 用途 |
| --- | --- | --- | --- | --- |
| `BasePriceGuan` | 10 | int | §9.5 | 本年第 n 名 = `10 × n` |
| `AgeMin` / `AgeMax` | 8 / 16 | int | §9.5 | `Next(8, 17)`（含端点） |

### 3.4 复用项（MUST NOT 重复声明）

- `DifficultyRates.ExpenseFactor(Difficulty)`（`src/KFL.Rules/Config/DifficultyRates.cs:36`）——全部「× 支出系数」。
- `DifficultyRates.NegativeEventFactor(Difficulty)`（`:62`）——提亲/捉婿的难度乘数（**患病概率不用它**）。
- `AttributePolicy.NextTalent/NextStudy/NextHealth/NextLifespan/NextNormal`（`:66-104`）——候选者/新生儿初始化。
- `AgeBracketPolicy.IsAdult`（`AgeBracketPolicy.cs:42`）——婚配资格（男 12 / 女 14）。
- `SalaryTable.AnnualSalaryGuan(int)`（`SalaryTable.cs`）——买功名费率基数。
- `TotalAssetsCalculator.Of(...)`（`src/KFL.Rules/Economy/TotalAssetsCalculator.cs`）——阶层加权分档基数。
- `AttributeLimits.Min/Max`（`src/KFL.Core/Config/AttributeLimits.cs:21/:24`）——属性 clamp 边界。

## 4. 与 `ConfigLiteralTests.RegisteredValues` 的对照

机制（`tests/KFL.Tests/Architecture/ConfigLiteralRules.cs`）：
判据① `Evaluate`（`:112`）扫描**非 Config 处**的清单元旦数值——`ConfigRoot`（`:53`）与
`ArchitectureRoot`（`:56`）被排除，故**漏登记 = 假绿**；
判据② `EvaluateMoneyLiterals`（`:179`）禁止 `Money.FromGuan(<字面量>)`。

### 4.1 本特性**必须新增**入单的小数

```text
GrowthPolicy  : 1.5m  2.25m  1.10m  0.5m  2m
IllnessPolicy : 0.0025m  0.40m  0.80m  0.95m  1.10m  1.05m  1.20m  0.95m
                0.05m  0.45m  1.00m  0.20m  0.60m  0.30m
MarriagePolicy: 0.50m  0.05m  0.30m  0.02m  1.50m  1.5m
                0.70m  0.25m  0.45m  0.40m  0.15m  0.20m  0.50m  0.30m
ChildbirthPolicy: 0.05m  0.025m  0.07m  0.10m  0.12m  0.5m
```

**条款**：上表是**全集**，但去重后与既有 49 条重叠的值（如 `0.5m`、`1.10m`、`1.5m`、`0.05m`）
MUST NOT 在 `RegisteredValues` 里出现两次；清单长度以去重后的实际新增条数为准
（**预计 49 → 63 上下**），实现时以守卫变红为准确认，MUST NOT 为了让测试变绿而漏登记。
`illness s` 的 `10000`、各级 `Level`、年龄线、次数（`3`/`16`/`20`/`45`/`100`）**整数规则值一律不入单**
（`ConfigLiteralTests.cs:102-103` 的既有纪律）。

### 4.2 MUST NOT 重复登记

| 本表成员 | 值 | 既有同值条目（`RegisteredValues` 内） |
| --- | --- | --- |
| `ExpenseFactor` 四值 | 0.6m / 1.0m / 1.1m / 1.3m | 已在 003/002 段登记 |
| `NegativeEventFactor` 四值 | 0.5m / 1.0m / 1.3m / 1.4m | 已在 006 段登记 |
| `IncomeRateTable` 各系数 | — | 已在 002 段登记 |

### 4.3 金额纪律（判据②）

一切金额 MUST 经 `Money.FromGuan(<配置成员>)`，**MUST NOT** 写 `Money.FromGuan(10)` 这类字面量；
`10n` 的买人口价 MUST 写成 `Money.FromGuan(PopulationPurchasePolicy.BasePriceGuan * n)`。

## 5. 与上游数值契约的回写关系

| 上游 | 回写动作 |
| --- | --- |
| `specs/002-monthly-settlement-economy/contracts/monthly-settlement.md:222` | 饥馑「−10 体质 / 每人每月 5% 死亡」的归属改为「⑦ 已交付」 |
| `specs/003-opening-assets-officialdom/contracts/new-game-setup.md:106-110` | 补「无父母参照者分布被 ⑦ 复用于买人口与婚配对象」 |
| `specs/006-bribery-punishment/contracts/config-registry.md:95-97` | 「属逻辑轨 ⑦」改述为「⑦ 已交付」 |
| `specs/001-core-skeleton/data-model.md:244-248` | 删除「成长衰老疾病死亡的年龄线与概率不含」的旧口径 |
| `src/KFL.Rules/Config/GameConfig.cs:24-27` | 「明确不含」注释改为指向本特性的六个视图 |

## 6. 验证物

| 断言 | 落点 |
| --- | --- |
| 登记表数值只出现在配置类 | `ConfigLiteralTests.登记表数值只出现在配置类的常量声明处()`（`:134`） |
| 金额字面量只经配置成员 | 同文件 `:150` |
| 扫描范围覆盖七个根目录 | 同文件 `:194` |
| 本项目数值不出现在 `src/**` 其它位置 | `ConfigLiteralRules.Evaluate`（`:112`） |
| 患病概率不引用 `NegativeEventFactor` | `IllnessTests.cs` 的四难度同种子同结果（SC-014） |
| `GameConfig` 六个视图零字面量 | SC-008，`ConfigLiteralTests` 全绿 |
