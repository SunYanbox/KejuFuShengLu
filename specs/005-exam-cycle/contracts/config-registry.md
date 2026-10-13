# 契约十四：科举数值登记（科举周期 · 逻辑轨 ⑤）

**Feature**: `005-exam-cycle` | **Date**: 2026-10-12 | **Spec**: [../spec.md](../spec.md)

> 本契约是**「数值在哪、由谁签发、如何被守卫」**的登记表，**不是**数值真源。数值真源 = 《科举浮生录
> 规格书》对应章节（章程「需求真源」、规格书 §16.2）。本表每一项都注明出处章节号，**MUST NOT
> 自创、改写或简化**任何数值。
>
> 上游契约：[契约五 §7](./../002-monthly-settlement-economy/contracts/config-registry.md)（002 的数值
> `MUST NOT 存在` 清单第 ⑤ 项「科举录取率」在本特性落地后**移出并加后记**）；同提交回写义务见
> [../plan.md](../plan.md) 的「回写义务清单」与 FR-028。

---

## 1. 科举数值登记

**声明处**：`src/KFL.Rules/Config/ExamPolicy.cs`（`public const` / `public static` 方法）。
`GameConfig` 只增一个转发视图（形如既有 `GameConfig.Career`），**MUST NOT 自带任何数值字面量**。

| 成员 | 值 | 类型 | 出处 | 用途 |
| --- | --- | --- | --- | --- |
| `MinimumAge` | `14` | `int` | 规格书 §6（报考资格） | 报考年龄下限（周岁，生日当月生效） |
| `JieShiBaseRate` | `0.01m` | `decimal` | 规格书 §6 | 解试基础录取率 1% |
| `ShengShiBaseRate` | `0.10m` | `decimal` | 规格书 §6 | 省试基础录取率 10% |
| `ImperialPromotionBaseRate` | `0.05m` | `decimal` | 规格书 §6 | 殿试基础进甲率 5% |
| `StudyFactorBase` | `0.4m` | `decimal` | 规格书 §6 | 学业系数基数 |
| `StudyFactorPerPoint` | `0.012m` | `decimal` | 规格书 §6 | 学业系数每点增量 |
| `OfficialdomDivisor` | `400` | `int` | 规格书 §6 | 仕天赋除数 `(1 + 仕天赋 / 400)` |
| `ShiStatusAdmissionBonus` | `1.1m` | `decimal` | 规格书 §6 | 仕族（`Family.HasShiStatus`）录取率加成 ×1.1 |
| `SecondClassStudyThreshold` | `50` | `int` | 规格书 §6 | 未进甲者：学业 >= 50 ⇒ 二甲，否则三甲 |
| `DiceRateCap` | `0.899999m` | `decimal` | 规格书 §7.1 | 需掷骰概率的封顶 89.9999% |
| `NeutralBribeMultiplier` | `1.0m` | `decimal` | 规格书 §7.1（k⁰） | 本轮贿赂乘数；逻辑轨 ⑥ 落地后经同一入口传实际值 |
| `JieShiExemptionFailureThreshold` | `3` | `int` | 规格书 §6（免解） | 解试累计失败数达 3 ⇒ 此后免掷骰中第 |
| `SpecialTributeMinimumAge` | `50` | `int` | 规格书 §7.2（特奏名） | 特奏名年龄下限 |
| `SpecialTributeFailureThreshold` | `6` | `int` | 规格书 §7.2（特奏名） | 特奏名省试累计失败数阈值 |
| `ExamTravelCostGuan(Difficulty)` | 简单 `10` / 普通 `20` / 困难 `40` / 地狱 `80` | `int`（贯） | 规格书 §6（省试路费） | 省试路费；**不乘**难度支出系数 |
| （复用）`OfficialCareerPolicy.AwaitingPostMinMonths` / `MaxMonths` | `6` / `24` | `int` | 规格书 §8.2（003 已登记） | 及第入仕待阙月数；**MUST NOT 在本表重复声明** |
| （复用）`OfficialCareerPolicy.InitialRankOf` | 一甲 `11` / 二甲 `13` / 三甲 `15` / 特奏名 `18` | `int` | 规格书 §8.2（003 已登记） | 初始官阶；**MUST NOT 在本表重复声明** |
| （复用）`OfficialCareerPolicy.MeritPerMonth` 等 | — | — | 规格书 §8.2（003） | 官俸与考课数值；本特性不触碰 |

**口径**：

| 条款 | 要求 |
| --- | --- |
| 1 | 「全部考试数值 MUST 只声明在 `KFL.Rules/Config/`」（FR-014）；`ExamPolicy` 是这批数值的**唯一**声明处 |
| 2 | `GameConfig` 只作转发，MUST NOT 自带数值字面量 |
| 3 | 待阙月数与初始官阶由 003 的 `OfficialCareerPolicy` **单点**决定，本特性 MUST NOT 另立第二处 |
| 4 | 上表的「（复用）」两行 MUST NOT 被复制到 `ExamPolicy`（否则出现第二真源） |
| 5 | 规格书变更数值 ⇒ 必须同时改本表、`ExamPolicy`、`RegisteredValues`（如为小数）与受影响测试 |

---

## 2. 禁止项（本特性**不得**引入的数值）

| 条款 | 要求 |
| --- | --- |
| 1 | MUST NOT 引入规格书未定义的官名或官阶数 |
| 2 | MUST NOT 引入贿赂的累进金额、被抓概率、惩罚矩阵、连坐系数（逻辑轨 ⑥） |
| 3 | MUST NOT 引入成长/衰老/疾病/死亡的任何概率或年龄线（逻辑轨 ⑦） |
| 4 | MUST NOT 引入成就/绝嗣判定的阈值（逻辑轨 ⑧） |
| 5 | MUST NOT 为「无限制模式」在规则层引入任何按调用方分支的数值或开关（章程原则 VI） |
| 6 | MUST NOT 引入跨 .NET 版本的随机数稳定性相关常量 |

---

## 3. 与 `ConfigLiteralTests.RegisteredValues` 的对照

**机制**（真源 = `specs/002-monthly-settlement-economy/contracts/config-registry.md` §5、§7，
守卫实现 = `tests/KFL.Tests/Architecture/ConfigLiteralRules.cs`）：

- 判据①（`Evaluate`，`:107-117`）：**清单内的数值**出现在**非 `src\KFL.Rules\Config\`** 处即违规；
  `ConfigRoot` 与 `ArchitectureRoot` 在 `EnumerateSources`（`:455-458`）被整体排除 ⇒
  **Config 内的未登记小数不会被报**（所以漏登记 = **假绿**）。
- 判据②（`EvaluateMoneyLiterals`，`:123-133`）：产品代码里的 `Money.FromGuan(<字面量>)` /
  `Money.FromWen(<字面量>)` 一律违规，金额 MUST 经配置成员取得。
- 清单 = `tests/KFL.Tests/Architecture/ConfigLiteralTests.cs:54-104` 的 `RegisteredValues`，
  按 `HashSet<decimal>` **数值相等**匹配（`0.1m == 0.10m`）。

### 3.1 本特性**必须新增**入单的小数（4 项）

```csharp
// ---- 005（逻辑轨 ⑤）新增：契约十四 §1 的四项小数 ----
// 解试基础录取率 1%（规格书 §6）
0.01m,
// 殿试基础进甲率 5%（规格书 §6）
0.05m,
// 学业系数每点增量（规格书 §6）
0.012m,
// 需掷骰概率的封顶 89.9999%（规格书 §7.1）
0.899999m,
```

### 3.2 **MUST NOT 重复登记**（与既有条目同值）

| 本表成员 | 值 | 既有同值条目（`RegisteredValues` 注释） |
| --- | --- | --- |
| `ShengShiBaseRate` | `0.10m` | §2 田租 `0.1m`（decimal 数值相等，`HashSet` 会去重） |
| `StudyFactorBase` | `0.4m` | §1 米价派生值 `0.4m` |
| `ShiStatusAdmissionBonus` | `1.1m` | §1 难度支出系数 `1.1m` |

口径同 003 的「封顶 `0.70` 与米价 clamp 上限同值 ⇒ MUST NOT 重复登记」与
「半俸 `0.50` 与生活费一般乘区修正 `−0.50` 同值 ⇒ MUST NOT 重复登记」。

### 3.3 **整数规则值一律不入单**

`14` / `400` / `50` / `3` / `50` / `6` / `10` / `20` / `40` / `80`（年龄线、除数、阈值、路费档）
**MUST NOT** 出现在 `RegisteredValues` 中（实测会与成员编号、夹具金额等产生大量撞车，
口径见 `ConfigLiteralTests` 类注释 `:5-45`）。贿赂乘数 `1.0m` 是**整数值**小数，同样不入单。

### 3.4 路费金额的取值纪律（判据②）

路费 MUST 经 `ExamPolicy.ExamTravelCostGuan(difficulty)` 取得，再由
`Money.FromGuan(ExamPolicy.ExamTravelCostGuan(difficulty))` 构造 —— 测试里的期望路费
**MUST 经配置成员取得**，MUST NOT 写 `Money.FromGuan(20)` 这样的字面量（否则判据②违规）。

---

## 4. 与 002 / 003 数值契约的回写关系

| 上游 | 回写动作 |
| --- | --- |
| [契约五 §7](./../002-monthly-settlement-economy/contracts/config-registry.md) | 把「科举录取率」从「002 阶段 MUST NOT 存在」清单**移出**，并加**后记**指向本契约 |
| [契约五 §6](./../002-monthly-settlement-economy/contracts/config-registry.md) | 加一行「科举数值只指向不复制」 |
| [003 契约八 §5](./../003-opening-assets-officialdom/contracts/config-registry.md) | 把科举周期相关数值从「阶段 ⑤ 才有的数值」清单移出并加后记 |
| [契约十一 §4](./../004-save-system/contracts/save-format.md) | `SaveFileFormat.Version` 由 `1` 升为 `2`（键表与版本闸门同提交回写） |

---

## 5. 验证物

| 断言 | 落点 |
| --- | --- |
| 清单内数值只出现在配置类常量声明处 | `ConfigLiteralTests.登记表数值只出现在配置类的常量声明处` |
| 产品代码的金额字面量只经配置成员取得 | `ConfigLiteralTests.产品代码的金额字面量只经配置成员取得` |
| 扫描范围覆盖全部七个根目录 | `ConfigLiteralTests.扫描范围覆盖全部七个根目录` |
| 注入数值在配置类内与注释内不报违规 | `ConfigLiteralTests.注入的数值在配置类内与注释内不报违规` |
| 科举数值锚点（逐项对照本契约 §1） | `tests/KFL.Tests/Rules/SpecAnchorTests.cs` 新增科举节 |
| 路费四档与不乘支出系数 | `tests/KFL.Tests/Rules/ExamSeasonTests.cs`（SC-006） |
| 封顶在三处生效、殿试必中不受影响、进甲率与仕族无关 | `tests/KFL.Tests/Rules/ExamAdmissionTests.cs`（SC-002） |

**SC-008 的门禁口径**：架构守卫的配置字面量违规数 = **0**（新增 4 项小数已入单、整数未入单、
`GameConfig` 无自带数值、路费期望值经配置成员取得）。
