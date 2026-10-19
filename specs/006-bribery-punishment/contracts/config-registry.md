# 契约十七：贿赂与惩罚数值登记（贿赂全链路 · 逻辑轨 ⑥）

**Feature**: `006-bribery-punishment` | **Date**: 2026-10-13 | **Spec**: [../spec.md](../spec.md) | **Plan**: [../plan.md](../plan.md)

> 本契约是**「数值在哪、由谁签发、如何被守卫」**的登记表，**不是**数值真源。数值真源 = 《科举浮生录
> 规格书》§7.1 / §7.2 / §7.3 / §7.4 / §7.5 / §11（章程「需求真源」、规格书 §16.2）。本表每一项都注明
> 出处章节号，**MUST NOT 自创、改写或简化**任何数值。
>
> 上游契约：[契约五 §7](../../002-monthly-settlement-economy/contracts/config-registry.md)（002 的数值
> `MUST NOT 存在` 清单中「§7 贿赂的全部数值（阶段⑥）」在本特性落地后**移出并加后记**）；
> [契约十四 §2 条款 2](../../005-exam-cycle/contracts/config-registry.md)（005 的 `ExamPolicy`
> **MUST NOT** 引入贿赂数值——本契约是它们的唯一声明处）。同提交回写义务见
> [../plan.md](../plan.md) 的「回写义务清单」与 FR-035。

---

## 1. 行贿数值登记

**声明处**：`src/KFL.Rules/Config/BribePolicy.cs`（`public const` / `public static` 方法）。
`GameConfig` 只增一个转发视图 `GameConfig.Bribe`（形如既有 `GameConfig.Exam`），**MUST NOT 自带任何
数值字面量**。

| 成员 | 值 | 类型 | 出处 | 用途 |
| --- | --- | --- | --- | --- |
| `WindowPreviousMonths` | `1` | `int` | 规格书 §7.1 | 行贿窗口 = 考试当月及**前 1 个月** |
| `ProgressionStep` | `0.2m` | `decimal` | 规格书 §7.1 | 第 n 次的累进步长 `(1 + 0.2×(n−1))`（金额与概率共用） |
| `BaseAmountGuan(ExamLevel)` | 解试 `100` / 省试 `500` / 殿试 `2000` | `int`（贯） | 规格书 §7.2 | 基础金额；**再乘**难度支出系数 |
| `BaseCaughtRate(ExamLevel)` | 解试 `0.05m` / 省试 `0.15m` / 殿试 `0.35m` | `decimal` | 规格书 §7.2 | 基础被抓率 5% / 15% / 35% |
| `EffectMultiplierK(ExamLevel)` | 解试 `1.6m` / 省试 `1.3m` / 殿试 `1.1m` | `decimal` | 规格书 §7.2 | 效果乘数 k（殿试作用于**进甲率**） |
| `CaughtProbabilityCap` | `1.0m` | `decimal` | 规格书 §7.1（§17② 的字面落法） | 被抓概率的上界（≥ 100% 即必定被抓） |
| `UnitMultiplier` | `1m` | `decimal` | 规格书 §7.1（`k⁰`） | 乘法循环的恒等初值——`k⁰ = 1.0` 的**唯一**来源（`ExamPolicy.NeutralBribeMultiplier` 已删除） |
| （复用）`DifficultyRates.ExpenseFactor(Difficulty)` | 0.6 / 1.0 / 1.1 / 1.3 | `decimal` | 规格书 §11（002 已登记） | 贿赂金额乘难度支出系数；**MUST NOT 在本表重复声明** |
| （复用）`DifficultyRates.BriberyRiskFactor(Difficulty)` | 0.2 / 1.0 / 1.5 / 3.0 | `decimal` | 规格书 §11（002 已登记） | 被抓概率的难度风险系数；**MUST NOT 在本表重复声明** |
| （复用）`ExamPolicy.DiceRateCap` | `0.899999m` | `decimal` | 规格书 §7.1（005 已登记） | 触顶（入口置灰）判据的封顶值；**MUST NOT 在本表重复声明** |

**口径**：

| 条款 | 要求 |
| --- | --- |
| 1 | 「§7.1 / §7.2 的全部数值 MUST 只声明在 `KFL.Rules/Config/`」（FR-001）；`BribePolicy` 是这批数值的**唯一**声明处 |
| 2 | `GameConfig.Bribe` 只作转发，MUST NOT 自带数值字面量 |
| 3 | 难度支出系数与贿赂风险系数由 002 的 `DifficultyRates` **单点**决定，本特性 MUST NOT 另立第二处 |
| 4 | 封顶值与 005 的 `ExamPolicy.DiceRateCap` 是**同一个**常量（触顶判据与 clamp 同源），MUST NOT 复制 |
| 5 | 规格书变更数值 ⇒ 必须同时改本表、`BribePolicy`、`RegisteredValues`（如为小数）与受影响测试 |

---

## 2. 惩罚数值登记

**声明处**：`src/KFL.Rules/Config/PunishmentPolicy.cs`。`GameConfig` 只增转发视图
`GameConfig.Punishment`。

### 2.1 12 格矩阵（规格书 §7.3）

`PunishmentCell`（`readonly record struct`）：`SentenceMonths`、`Execution`、`CollectivePunishment`、
`Fine`（`None` / `Ratio` / `Fixed`）、`FineRatio`、`FineFixedGuan`、`RelativeBanMonths`、
`RelativePromotionBan`、`FamilyWideBan`。查表函数：`CellOf(Difficulty, ExamLevel)`。

| 难度 | 殿试 | 省试 | 解试 |
| --- | --- | --- | --- |
| 简单 | 60 月 / 比例 `0.02m` / 禁考 `0` / 禁升 `false` | 36 月 / 比例 `0.01m` / 禁考 `0` / 禁升 `false` | 12 月 / **无罚金** / 禁考 `0` / 禁升 `false` |
| 普通 | 180 月 / 比例 `0.15m` / 禁考 `36` / 禁升 `true` | 60 月 / 固定 `200` 贯 / 禁考 `24` / 禁升 `true` | 36 月 / 固定 `100` 贯 / 禁考 `0` / 禁升 `false` |
| 困难 | **死刑** / 比例 `0.20m` / 禁考 `48` / 禁升 `true` | 240 月 / 比例 `0.10m` / 禁考 `36` / 禁升 **`false`** | 120 月 / 比例 `0.05m` / 禁考 `36` / 禁升 **`false`** |
| 地狱 | **死刑 + 连坐** / 比例 `0.20m` / **全族禁考禁升 `48`** | 240 月 / 比例 `0.10m` / 禁考 `36` / 禁升 **`false`** | 120 月 / 比例 `0.05m` / 禁考 `36` / 禁升 **`false`** |

### 2.2 通则与阈值（规格书 §7.4、§7.5、§11）

| 成员 | 值 | 类型 | 出处 | 用途 |
| --- | --- | --- | --- | --- |
| `RatioFineCapGuan` | `50000` | `int`（贯） | 规格书 §7.5 | 比例罚金**全域封顶**（对最终金额生效） |
| `FamilyWideBanMonths` | `48` | `int` | 规格书 §7.4 | 全族禁考禁升 = **逐人**各 48 月禁考 + 48 月禁升（Q3 = A） |
| `ReleaseDemotionLevels` | `1` | `int` | 规格书 §7.5 | 曾有官职者释放时降 1 级官阶（L18 维持） |
| `MonthsPerYear` | `12` | `int` | 规格书 §7.5 | 服刑年数 → 月数（5 年 = 60 月）；**复用** `IncomeRateTable.MonthsPerYear`，MUST NOT 复制 |
| `ImmediateExecution` | —— | `ExecutionKind` 取值 | 规格书 §7.5 | 死刑立即执行（MUST NOT 建立服刑计时） |
| （复用）`DifficultyRates.ExpenseFactor(Difficulty)` | 0.6 / 1.0 / 1.1 / 1.3 | `decimal` | 规格书 §11 | 罚金乘难度支出系数；**MUST NOT 重复声明** |
| （复用）`SalaryTable.LowestLevel` | `18` | `int` | 规格书 §8（003 已登记） | 释放降级的下界（L18 维持）；**MUST NOT 在本表重复声明 `18`** |
| （复用）`AgeBracketPolicy` | 男 `12` / 女 `14` | `int` | 规格书 §4.3（002 已登记） | 连坐免死例外的「孩子未成年」判据；**MUST NOT 在本表重复声明** |

**口径**：

| 条款 | 要求 |
| --- | --- |
| 1 | 矩阵 MUST 是**表**（12 格逐格列明），MUST NOT 用分支表达式合出「只禁考 / 无禁考」的隐式约定 |
| 2 | 「异地同值」必须**逐格列明**而不是互相引用——例如困难与地狱的三格数值完全相同，但仍各占一行（审计时需要逐格核对 §7.3） |
| 3 | 死刑格的 `SentenceMonths` MUST 为 `0` 且 `Execution != None`（两者不可同时非空） |
| 4 | 简单档三格的 `RelativeBanMonths == 0` ∧ `RelativePromotionBan == false`（「无禁考」） |
| 5 | 困难 / 地狱的解试与省试三格的 `RelativePromotionBan == false`（「只禁考」） |

---

## 3. 禁止项（本特性**不得**引入的数值）

| 条款 | 要求 |
| --- | --- |
| 1 | MUST NOT 引入成长 / 衰老 / 疾病 / 死亡的任何概率或年龄线（逻辑轨 ⑦） |
| 2 | MUST NOT 引入成就 / 绝嗣判定的阈值（逻辑轨 ⑧） |
| 3 | MUST NOT 引入婚育 / 收养 / 买功名婚姻的数值（逻辑轨 ⑦） |
| 4 | MUST NOT 为「无限制模式」在规则层引入任何按调用方分支的数值或开关（章程原则 VI；越限接缝属 U4） |
| 5 | MUST NOT 引入跨 .NET 版本的随机数稳定性相关常量 |
| 6 | MUST NOT 为矩阵引入**累加刑期**的表达式（规格书从未出现「累加」表述；同档取替换） |
| 7 | MUST NOT 引入「乘数上限」或「乘数封顶」——规格书把封顶写在**最终概率**上（FR-010） |
| 8 | MUST NOT 新增「死囚待决」相关的状态或时长常量（§7.5 明写死刑立即执行） |

> **007 后记（2026-10-17）**：上表第 1 条与第 3 条是 **006 的越界约束**（006 MUST NOT 引入这些数值），
> 该约束**继续成立且未被放宽**；但两条所指向的**数值归属**已由 007 交付——第 1 条的概率与年龄线
> 落在 `KFL.Rules/Config/{GrowthPolicy, IllnessPolicy, ChildbirthPolicy}.cs`，第 3 条的婚育与收养
> 数值落在 `KFL.Rules/Config/{MarriagePolicy, ChildbirthPolicy, PopulationPurchasePolicy}.cs`
> （「买功名婚姻」的数值复用 `MarriagePolicy.DegreeMarriageFeeRatio` 与 `SalaryTable`），
> 登记见 [007 contracts/config-registry.md](../../007-family-lifecycle/contracts/config-registry.md)。
> 故 006 的 `RegisteredValues` 对照表（§4）**无需增补**，也不得因此放宽本节的禁止项。

> **008 读向备注（2026-10-18，逻辑轨 ⑧）**：`PunishmentQueries.FamilyCumulativeBribes(Family)`
> （`src/KFL.Rules/Punishment/PunishmentQueries.cs:54`）的语义是家族**尝试**行贿的次数（**含被抓**），
> 而成就「暗度陈仓 / 权钱交易 / 只手遮天 / 买断功名」（ACH-050~053）要的是**成功**次数 ⇒
> 它们读 `AchievementState.SuccessfulBribeCount`（由当月 `PunishmentSnapshot.Bribes` 中
> `Caught == false` 的条数累加），**MUST NOT** 复用 `FamilyCumulativeBribes`。
> 两者口径相近但**不等价**，混用会把「行贿被抓」也算作成就进度。成就侧新增的数值只有
> `Bribe{Three,Ten,Hundred,Thousand}Threshold = 3/10/100/1_000`（`KFL.Rules/Config/AchievementPolicy.cs`），
> 它们是**整数规则值**，按 §4.3 的纪律**不入** `ConfigLiteralTests.RegisteredValues`；
> 本节的禁止项（尤其第 8 条）继续成立且未被放宽。真源见
> [008 `achievement-catalog.md` §2/§3](../../008-achievements-extinction/contracts/achievement-catalog.md)。
---

## 4. 与 `ConfigLiteralTests.RegisteredValues` 的对照

**机制**（真源 = [002 契约五 §5](../../002-monthly-settlement-economy/contracts/config-registry.md)；
守卫实现 = `tests/KFL.Tests/Architecture/ConfigLiteralRules.cs`）：

- **判据①**（`Evaluate`）：**清单内的数值**出现在**非 `src/KFL.Rules/Config/`** 处即违规；
  `ConfigRoot` 与 `ArchitectureRoot` 在 `EnumerateSources` 被整体排除 ⇒ **Config 内的未登记小数不会被报**
  （所以漏登记 = **假绿**）。
- **判据②**（`EvaluateMoneyLiterals`）：产品代码里的 `Money.FromGuan(<字面量>)` / `Money.FromWen(<字面量>)`
  一律违规，金额 MUST 经配置成员取得。
- 清单 = `tests/KFL.Tests/Architecture/ConfigLiteralTests.cs:54-117`，**按数值相等**匹配
  （`0.15m == 0.150m`；`HashSet<decimal>` 会去重）。评审口径：注释与字符串会被 `CodeView` 抹成空格。

### 4.1 本特性**必须新增**入单的小数（**3** 项）

```csharp
// ---- 006（逻辑轨 ⑥）新增：契约十七 §1/§2 的三项小数 ----
// 解试效果乘数 k = 1.6（规格书 §7.2）
1.6m,
// 省试基础被抓率 15% 与中档罚金比例 15%（规格书 §7.2、§7.3；同值只登记一次）
0.15m,
// 殿试基础被抓率 35%（规格书 §7.2）
0.35m,
```

清单长度 **46 → 49**。

### 4.2 **MUST NOT 重复登记**（与既有条目同值）

| 本表成员 | 值 | 既有同值条目（`RegisteredValues` 行号） |
| --- | --- | --- |
| `EffectMultiplierK(ShengShi)` | `1.3m` | 难度支出系数 困难 `1.3m`（`:68`） |
| `EffectMultiplierK(DianShi)` | `1.1m` | 难度支出系数 困难 `1.1m`（`:68`） |
| `ProgressionStep` | `0.2m` | 米价游走幅度项 `0.2m`（`:66`） |
| `BaseCaughtRate(JieShi)` | `0.05m` | 殿试基础进甲率 `0.05m`（`:108`） |
| `BaseCaughtRate(ShengShi)` | `0.15m` | 与本表新增项同值（只登记一次，见 §4.1） |
| `FineRatio(简单殿试)` | `0.02m` | 商税 / 铺面租相关 `0.02m`（`:76`） |
| `FineRatio(简单省试)` | `0.01m` | 解试基础录取率 `0.01m`（`:106`） |
| `FineRatio(困难/地狱解试)` | `0.05m` | 殿试基础进甲率 `0.05m`（`:108`） |
| `FineRatio(困难/地狱省试)` | `0.10m` | 田租 `0.1m`（`:72`） |
| `FineRatio(困难/地狱殿试)` | `0.20m` | 米价游走幅度项 `0.2m`（`:66`） |
| `BriberyRiskFactor(困难)` | `1.5m` | 商铺租率相关 `1.5m`（`:74`） |
| `CaughtProbabilityCap` | `1.0m` | **与整数同值**（第二判据），不入单（`ConfigLiteralTests.cs:115`） |
| `DiceRateCap` | `0.899999m` | 005 已登记（`:112`） |

### 4.3 **整数规则值一律不入单**

`100` / `500` / `2000` / `50000` / `36` / `24` / `48` / `60` / `180` / `240` / `120` / `12` / `18` /
`200` / `1`（以及难度风险系数里的 `1.0` / `3.0`）**MUST NOT** 出现在 `RegisteredValues` 中
（实测会与成员编号、夹具金额等产生大量撞车，口径见 `ConfigLiteralTests` 类注释与 `:116`）。

### 4.4 金额的取值纪律（判据②）

矩阵的固定罚金（`200` / `100` 贯）与金额期望值 MUST 经配置成员取得：
`Money.FromGuan(PunishmentPolicy.CellOf(difficulty, level).FineFixedGuan)`，
MUST NOT 写 `Money.FromGuan(200)` 这样的字面量。

---

## 5. 与 002 / 003 / 005 数值契约的回写关系

| 上游 | 回写动作 |
| --- | --- |
| [契约五 §7](../../002-monthly-settlement-economy/contracts/config-registry.md) | 把「§7 贿赂的全部数值（阶段⑥）」从「002 阶段 MUST NOT 存在」清单**移出**，并加**后记**指向本契约 |
| [契约五 §6](../../002-monthly-settlement-economy/contracts/config-registry.md) | 加一行「贿赂与惩罚数值只指向不复制」 |
| [契约十四 §2 条款 2](../../005-exam-cycle/contracts/config-registry.md) | 把「MUST NOT 引入贿赂的累进金额、被抓概率、惩罚矩阵、连坐系数（逻辑轨 ⑥）」改为「已由 ⑥ 在 `BribePolicy` / `PunishmentPolicy` 落地」 |
| [契约十四 §1](../../005-exam-cycle/contracts/config-registry.md) | `NeutralBribeMultiplier` 一行删除（占位作废，`k⁰` 由 `UnitMultiplier` 的恒等初值给出） |
| [003 契约八 §5](../../003-opening-assets-officialdom/contracts/config-registry.md) | 把「贿赂与惩罚相关数值属阶段 ⑥」的措辞改为后记指向本契约 |
| [契约十一 §7](../../004-save-system/contracts/save-format.md) | `SaveFileFormat.Version` 由 `2` 升为 `3`（键表与版本闸门同提交回写） |

---

## 6. 验证物

| 断言 | 落点 |
| --- | --- |
| 清单内数值只出现在配置类常量声明处 | `ConfigLiteralTests.登记表数值只出现在配置类的常量声明处` |
| 产品代码的金额字面量只经配置成员取得 | `ConfigLiteralTests.产品代码的金额字面量只经配置成员取得` |
| 扫描范围覆盖全部七个根目录 | `ConfigLiteralTests.扫描范围覆盖全部七个根目录` |
| 贿赂与惩罚数值锚点（逐项对照本契约 §1/§2） | `tests/KFL.Tests/Rules/SpecAnchorTests.cs` 新增贿赂与惩罚节 |
| 累进步长 `0.2` 对金额与概率共用；基础金额 100/500/2000；被抓率 5%/15%/35%；k = 1.6/1.3/1.1；概率 clamp 到 1.0 | `tests/KFL.Tests/Rules/BribeEntryTests.cs`（SC-002） |
| 风险系数 0.2/1.0/1.5/3.0 经 `DifficultyRates.BriberyRiskFactor` 生效 | `tests/KFL.Tests/Rules/BribeEntryTests.cs` |
| 12 格矩阵逐格（服刑月数 / 罚金比例或固定额 / 「只禁考」「无禁考」标记） | `tests/KFL.Tests/Rules/PunishmentMatrixTests.cs`（SC-004） |
| 比例罚金封顶 `50000` 与基数为负 ⇒ 金额 0、条目 0 | `tests/KFL.Tests/Rules/PunishmentMatrixTests.cs`（SC-005） |
| 全族 48 月逐人计时 | `tests/KFL.Tests/Rules/PunishmentAdvanceTests.cs`、`CollectivePunishmentTests.cs` |
| 释放降 1 级且 L18 维持 | `tests/KFL.Tests/Rules/PunishmentAdvanceTests.cs`（SC-006） |

**SC-002 的门禁口径**：架构守卫的配置字面量违规数 = **0**（新增 3 项小数已入单、整数未入单、
`GameConfig.Bribe` / `GameConfig.Punishment` 无自带数值、矩阵固定罚金期望值经配置成员取得）。
