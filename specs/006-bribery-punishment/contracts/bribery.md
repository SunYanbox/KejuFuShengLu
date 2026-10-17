# 契约十五：行贿（贿赂全链路 · 逻辑轨 ⑥）

**Feature**: `006-bribery-punishment` | **Date**: 2026-10-13 | **Spec**: [../spec.md](../spec.md) | **Plan**: [../plan.md](../plan.md)

本契约把「行贿」钉成**结算之外**的、可逐字段断言的一次玩家动作：**窗口 → 金额 → 扣款 → 掷一次骰 →
计数与乘数 → 当月事实 → 施加惩罚**。它同时是 U2 的行贿面板与 U4 的调试入口的**唯一**规则来源。
数值真源 = 《科举浮生录规格书》§7.1 / §7.2 / §11（章程「需求真源」），登记见 [契约十七](./config-registry.md)。

---

## 1. 行贿窗口与计数维度（FR-003、FR-004）

| 条款 | 要求 |
| --- | --- |
| 1 | **窗口 = 该级别考试当月及前 1 个月**：解试 `(N, 8)` 与 `(N, 7)`；省试 `(N+1, 3)` 与 `(N+1, 2)`；殿试 `(N+1, 4)` 与 `(N+1, 3)`（`N` = 科年，`ExamCalendar.IsKeYear(N)` 为真） |
| 2 | 殿试窗口与**省试月重叠**——玩家可在尚不知省试结果时就殿试行贿；若省试落第，该次行贿**照扣不退**（乘数只在真正参加殿试时生效）（§7.1；spec Edge Cases） |
| 3 | 窗口是**纯派生值，MUST NOT 持久化**；`BribeRules.IsWithinWindow(date, level)` / `WindowStartOf(date, level)` 是唯一判据 |
| 4 | **计数维度 = 同一考生 × 同一考试级别、跨科年累计**：换级别不重置、换考生重置（新成员天然从零开始） |
| 5 | 计数 MUST 跨「保存/退出/重载」保留（`Person` 的三个非负整数，键 25/26/27） |
| 6 | 行贿是**结算之外**的动作，与 005 的「报名」同类：发生在两次「推进」之间，其状态变化由下一次自动保存或退出保存落盘（004 的存档节奏不变） |
| 7 | 窗口外、非在册成员、级别与考生当前功名不符（如白身行贿省试）、已触顶、**现银 + 储蓄不足以全额覆盖应付金额**的调用 MUST **拒绝**，且资金、计数、乘数、账本、随机消费次数**全部不变**。判定按「窗口 → 在册 → 级别匹配 → 触顶 → **可付性**」的固定次序，同时违反多项时返回**最先命中的那一条** |

> **可付性（§17 裁决回写 2026-10-17，规格书 §7.1）**：行贿是玩家**主动发起的可选支出**，与
> 「买人口」「资产买卖」同类，**MUST NOT 转贷款**——贷款的唯一来源仍是规格书 §5.4 的
> **罚金 / 固定罚金**（「不存在主动借贷」）。故 ③ 的支付段恒为「现金 → 储蓄」两段全额付清，
> 付不起即拒绝，`BribeOutcome` 不设 `ToLoan`。罚金侧（[契约十六 §5](./punishment.md)）不受本裁决影响。

> **当前功名与级别的匹配**沿用 005 的 `ExamRegistration.TargetLevelOf`（白身→解试、举人→省试、
> 贡士→殿试、进士→`null`），本契约 MUST NOT 另写一份。进士无法再行贿任何级别；被连坐降级回到
> 贡士/举人者可再次行贿对应级别。

---

## 2. 行贿行动的固定次序（FR-003~FR-010）

`BribeEntry.Bribe(GameState state, Person examinee, ExamLevel level, IRandomService random)`：

| 段 | 动作 | 失败/拒绝时的行为 |
| --- | --- | --- |
| ① | **校验**：窗口 ∧ 在册 ∧ 级别与当前功名匹配 ∧ **未触顶** ∧ **现银 + 储蓄 ≥ 应付金额** | 返回 `Accepted = false` + `RejectReason`；**不改任何字段、不消费随机** |
| ② | **自算**：`nth = NthOf(examinee, level)`、应付金额、被抓概率 | 纯计算，无副作用 |
| ③ | **扣款**：`PaymentPrimitive.Pay(economy, LedgerCategory.BribeExpense, month, amount, examinee.Id)` | **现金 → 储蓄，两段全额付清**（① 已保证可付，**MUST NOT 转贷款**）；落**恰好 1 条**资金类条目（恒为负，金额 = 应付额 > 0） |
| ④ | **掷骰**：`random.NextDouble()` **恰好 1 次**，`< 被抓概率` ⇒ 被抓 | 这是本特性**唯一**的新增随机消费点，且发生在结算之外 |
| ⑤ | **计数与乘数**：`examinee.RecordBribe(level)`（计数 +1），乘数随之变为 `k^计数` | **无论是否被抓都执行**（Q1 = B） |
| ⑥ | **记当月事实**：`family.PunishmentJournal.Append(new BribeFact(...))` | 追加，MUST NOT 改写既有行 |
| ⑦ | **被抓则施加惩罚**：`PunishmentImposer.Apply(state, examinee, level, random)` | 见 [契约十六 §2~§6](./punishment.md) |

**条款**：

1. ③ **恒成功**：① 的可付性判据已保证现银 + 储蓄 ≥ 应付额，故此后的池内付出**恒等于**应付额、
   `BribeExpense` 恒为**恰好 1 条**且金额 > 0（与 §7 的「MUST NOT 记 0 金额条目」不再冲突）。
   ④ MUST NOT 因③的结果而短路：金额为 0 的情形不存在（基础金额 ≥ 100 贯）。
2. ⑤ 与 ④ 的先后不可交换：被抓**当次照样计入 n**，被抓 **MUST NOT** 清零该级别的计数与乘数。
3. 被抓 **MUST NOT** 退还贿款、MUST NOT 落任何补偿条目。
4. **落榜不退钱、无补偿**（FR-009）：行贿与考试判定是两次独立动作，退款通道**不存在**。
5. **无次数上限、无拒收机制**（§7.1）——拒绝仅来自 FR-010 的**触顶**与 §2 ① 的**可付性**两项。

---

## 3. 金额与被抓概率（FR-005、FR-006）

```text
应付金额(n) = 基础金额(level) × (1 + 0.2 × (n − 1)) × DifficultyRates.ExpenseFactor(difficulty)
被抓概率(n) = min( 基础被抓率(level) × (1 + 0.2 × (n − 1)) × DifficultyRates.BriberyRiskFactor(difficulty),
                  1.0 )
```

| 级别 | 基础金额（贯） | 基础被抓率 | 效果乘数 k | 出处 |
| --- | --- | --- | --- | --- |
| 解试 | `100` | `0.05m` | `1.6m` | 规格书 §7.2 |
| 省试 | `500` | `0.15m` | `1.3m` | 规格书 §7.2 |
| 殿试 | `2000` | `0.35m` | `1.1m`（作用于**进甲率**） | 规格书 §7.2 |

**条款**：

1. **累进步长 `0.2` 对金额与概率共用**（§7.1 的同一 `(1 + 0.2×(n−1))` 因子）。
2. 金额 **MUST** 乘**难度支出系数**（§7.2 末行「贿赂金额乘难度支出系数」）；系数取自
   `DifficultyRates.ExpenseFactor`（MUST NOT 复制）。
3. 概率 **MUST** clamp 到 **`1.0`**：`≥ 100%` 即**必定被抓**，MUST NOT 出现「概率 > 100%」的可观察值
   （spec Edge Cases）。clamp 的上界是 `1.0` 而**不是** `DiceRateCap`——「被抓概率」与「录取率」是两个量。
   地狱殿试第 2 次即达 `0.35 × 1.2 × 3.0 = 1.26` 而被 clamp 为 1.0。
4. 金额与被抓概率 MUST 经查询面读出（[§6](#6-派生查询面fr-029-fr-030)），使 U2 的面板能直接明示
   「当前应付金额与当前被抓概率」（§7.1）。
5. 概率的「下一次」值 = 以 `n + 1` 代入上式的值（US1 AS1 的「该考生解试被抓概率下一次为
   `5% × 1.2 × 1.0 = 6%`」）。

---

## 4. 效果乘数 kⁿ 与封顶

### 4.1 乘数（FR-008，Q1 = B）

```text
乘数 = kⁿ，其中 n = 该考生该级别的**累计行贿次数**（含被抓的那些）
k    = 解试 1.6 / 省试 1.3 / 殿试 1.1（殿试作用于**进甲率**）
```

| 条款 | 要求 |
| --- | --- |
| 1 | **被抓当次照样计入 n 并推进乘数**；被抓 MUST NOT 清零计数与乘数、MUST NOT 退还贿款（Q1 = B） |
| 2 | 乘数是**派生值，MUST NOT 持久化**（它是计数与 `k` 的纯函数）——将计数持久化即为持久化乘数 |
| 3 | `n = 0`（从未行贿）⇒ 乘数 = `1.0`（`k⁰`），由乘法循环的**恒等初值**给出；`ExamPolicy.NeutralBribeMultiplier` 的占位**随之作废并删除** |
| 4 | 乘数 MUST 经 005 **同一入口的形参**传入（`ExamAdmission.AdmissionRate(level, person, hasShiStatus, bribeMultiplier)` / `ImperialPromotionRate(person, bribeMultiplier)`）；本契约 MUST NOT 复制录取率/进甲率公式 |
| 5 | 殿试的**录取本身不掷骰**（005 Q1 裁决：入殿试者 100% 成为进士），乘数只作用于**进甲率** |

### 4.2 封顶与置灰（FR-010，SC-003）

| 条款 | 要求 |
| --- | --- |
| 1 | 判据取**最终录取率/进甲率**（解试/省试取录取率，殿试取进甲率）达到 `DiceRateCap`（`0.899999m`）——乘数本身无上限，只有概率有 |
| 2 | 触顶 ⇒ 该考生**该级别**的行贿入口 MUST 被查询面报为「已封顶（置灰）」，且 `BribeEntry.Bribe` 对后续行贿 MUST **拒绝**（状态逐字段不变、随机消费 **0**） |
| 3 | 触顶判据 MUST 经**同一**录取率入口计算（`BribeRules.AdmissionRateWith` 转调 `ExamAdmission`） |
| 4 | 规则层 MUST NOT 提供按调用方分支的绕过通道（章程原则 VI）；U4 的「越限能力」经**装配期策略注入**，其接缝由 U4 规格化时定义（本特性不预置） |

---

## 5. 当月事实缓冲（FR-028）

| 条款 | 要求 |
| --- | --- |
| 1 | 行贿与惩罚的**当月事实**产生在结算之外、**报告在结算之内**：`BribeEntry` 与 `PunishmentImposer` 把事实追加进 `Family.PunishmentJournal`，结算第③步（[契约十六 §1](./punishment.md#1-结算第③步fr-020)）把它们投影成 `SettlementResult.Punishment` 并**立即清空** |
| 2 | 日志是**缓冲**，不是第二真源：状态的真源仍是 `Person` 的计时/状态位与账本；日志只承载「已发生但尚未被某次结算报告」的事实 |
| 3 | 日志 MUST 落盘（否则「行贿 → 退出保存 → 重载 → 结算」会丢失整格惩罚），且每一行只读、**只追加** |
| 4 | 日志的集合按 `PersonId` 升序（并列时按 `Level` / `Relative` 再排序），成员集**含归档成员**的 `PersonId` |
| 5 | 每一次行贿 MUST 追加**恰好 1 行** `BribeFact`（含 `Level` / `Nth` / `Amount` / `Caught` / `CapReached`），供 ⑧ 消费「投石问路 / 暗度陈仓 / 权钱交易 / 只手遮天 / 买断功名」 |
| 6 | 日志行的 `PersonId` MUST 存在于 `Family.Members`（`SaveStateValidator` 第 ⑫ 条） |

---

## 6. 派生查询面（FR-029 ①③、FR-030）

| 成员 | 签名 | 语义 |
| --- | --- | --- |
| 窗口 | `bool IsWithinBribeWindow(GameDate date, ExamLevel level)` | 该月是否处于该级别的行贿窗口 |
| 可行性 | `BribeFeasibility Of(GameState state, Person examinee, ExamLevel level)` | **当前应付金额**、**当前被抓概率**、**是否已触顶（入口置灰）**、**现银 + 储蓄是否够付（`IsAffordable`，不足亦置灰）**、拒绝原因 |
| 触顶 | `bool IsLevelCapReached(GameState state, Person examinee, ExamLevel level)` | 最终概率达到 `0.899999m` |
| 家族累计 | `int FamilyCumulativeBribes(Family family)` | 全部成员（含归档）三项计数之和——供「暗度陈仓 3 / 权钱交易 10 / 只手遮天 100 / 买断功名 1000」 |
| 清白传家 | `bool WasBribeFreeAtImperialPass(Person person)` | 存在 `ExamPass` 的进士记录 ∧ 三项计数皆为 0（见 plan.md 勘误 #2/#3） |

**条款**：上表全部为**纯函数**——MUST NOT 改状态、MUST NOT 消费随机（SC-008）；
U1/U2/U4 与 ⑧ MUST **只**经本查询面取值，本特性 MUST NOT 自建视图或按钮。

---

## 7. 账本类别（FR-027）

| 类别 | 种类 | 入账目标 | 记法 |
| --- | --- | --- | --- |
| `LedgerCategory.BribeExpense` | 资金类 | `null`（恒为负） | 每次行贿**恰好 1 条**（③ 全额付清 ⇒ 金额恒 > 0），`PersonId` = 行贿者 |
| `LedgerCategory.Fine` | 资金类 | `null`（恒为负） | 每次罚金**恰好 1 条**，`PersonId` = 当事人（金额为 0 时**不落条目**） |

**条款**：MUST NOT 记 0 金额条目；MUST NOT 让账本出现「贿赂」以外的第二套计数；MUST NOT 用事件类承载
行贿支出与罚金（资金池真的动了，E-08 的二分类会破）。判据落点见
[契约四 §5](../../002-monthly-settlement-economy/contracts/ledger.md) 的角色级聚合。

---

## 8. 调试入口（FR-031 的行贿侧）

```csharp
public static void BribeEntry.ResetBribeCounts(Person examinee, ExamLevel? level);
```

| 条款 | 要求 |
| --- | --- |
| 1 | `level == null` ⇒ 三级计数全清；否则只清该级别；计数回落 ⇒ 金额与概率相应回落、乘数随之回落 |
| 2 | **MUST NOT** 让最终录取率突破 `0.899999`（重置不是「越限」，越限通道由 U4 的装配期策略另议） |
| 3 | 它是**普通公开规则入口**：规则层 MUST NOT 感知调用方；U4 的日志与时间戳属界面轨（章程原则 VI） |

---

## 9. 明确不包含

界面（主界面与下月/快进、双视图与卡片、统计页、调试面板、设置——U1~U5）、成就解锁与绝嗣判定（⑧）、
成长/衰老/疾病/寿命（⑦）、婚育/买人口（⑦）、存档迁移（004 的 Out of Scope）、越限能力的装配期接缝
（登记为 U4 的开放项）、以及**任何绕过 `ExamAdmission` 的录取率复制**。
