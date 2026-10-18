# 契约十二：考试日历与报名（科举周期 · 逻辑轨 ⑤）

**Feature**: `005-exam-cycle` | **Date**: 2026-10-12 | **Spec**: [../spec.md](../spec.md) | **Data model**: [../data-model.md](../data-model.md)

> **数值一律不在此声明**。本契约只固定**形状、次序与判定口径**；全部数值的真源是《科举浮生录
> 规格书》§3 / §6 / §7.1 / §7.2 / §8.2 / §11，登记见 [契约十四](./config-registry.md)。
> 「契约N」文号口径见 [../research.md](../research.md) 抬头。
>
> 本契约的消费方：逻辑轨 ⑤ 自身、界面轨 U1（快进暂停与「下月」按钮）、U4（调试控制台
> 「跳至下一科举」）。消费方 MUST 经本节入口，**MUST NOT 自行复制**日历推导或资格判定（FR-018）。

---

## 1. 考试日历的纯派生

**入口**：`KFL.Rules/Exam/ExamCalendar.cs`（静态纯函数类，无字段、无状态）。

| 条款 | 要求 |
| --- | --- |
| 1 | 科年判定：年份 `N >= 1` 且 `N % 3 == 1` ⇒ `N` 为**科年**（`ExamCalendar.IsKeYear(N)`） |
| 2 | 考试月映射：科年 `N` 的 **(N, 8)** = 解试；(N+1, 3) = 省试；(N+1, 4) = 殿试 |
| 3 | 其余月份**无考试**：`ExamCalendar.LevelOf(date)` 返回 `null` |
| 4 | `ExamCalendar.NextExamMonth(date)` 返回**严格晚于** `date` 的第一个考试月，含跨年与跨科年边界 |
| 5 | 三者 MUST 为**纯函数**：MUST NOT 读 `GameState`、MUST NOT 改任何状态、MUST NOT 消耗随机、MUST NOT 依赖系统时钟或全局状态（G-07、FR-001、FR-018） |
| 6 | 科年与考试月是**派生值，MUST NOT 持久化**；MUST NOT 出现在任何存档键表中 |
| 7 | 三者的返回类型为 `bool` / `ExamLevel?` / `GameDate`；**MUST NOT** 返回 `IReadOnlyList` 或任何可变对象 |

**跨年边界（3 年周期，逐月断言口径）**：`(1,8) 解试 → (1,9..12) 无 → (2,1..2) 无 → (2,3) 省试 →
(2,4) 殿试 → (2,5..(3,12)) 无 → (4,8) 解试 → (5,3) 省试 → (5,4) 殿试 …`

---

## 2. 考试级别枚举

**类型**：`KFL.Core/Enums/ExamLevel.cs`

```csharp
public enum ExamLevel { JieShi = 1, ShengShi = 2, DianShi = 3 }
```

| 条款 | 要求 |
| --- | --- |
| 1 | **MUST NOT** 设 `None = 0`：「无考试」一律用 `ExamLevel?` 的 `null` 表达 |
| 2 | 合法取值恰为 `1..3`；`0` 与 `> 3` 不是具名成员 |
| 3 | 二进制存档中的越界整数 MUST 在复原第 ⑦ 步被拒（`InvalidStructure`）；JSON 写枚举名，认不得的名字在解析步失败（`Malformed`）（004 契约十 §6 条款 6） |
| 4 | `ExamLevel` MUST NOT 承载任何数值（基础率、路费、年龄线等一律在 [契约十四](./config-registry.md)） |
| 5 | 报名标记的合法取值只有 `JieShi` / `ShengShi`（殿试不需报名，见 §4） |

---

## 3. 报考资格与报考级别

**入口**：`KFL.Rules/Exam/ExamRegistration.cs`。

### 3.1 `TargetLevelOf(Person) -> ExamLevel?`

| 当前功名（`Person.CurrentDegree`） | 报考级别 |
| --- | --- |
| `BaiShen` | `JieShi` |
| `JuRen` | `ShengShi` |
| `GongShi` | `DianShi` |
| `JinShi` | `null`（无更高级别，MUST NOT 报名） |
| 其它 | `ArgumentOutOfRangeException` |

「当前功名」= 功名变迁史**末条**记录；史为空 ⇒ 白身（FR-010）。

### 3.2 `IsEligible(Person, GameDate) -> bool`

| 条款 | 条件 | 来源 |
| --- | --- | --- |
| 1 | 年满 **14 周岁**（`Person.AgeAt(date) >= ExamPolicy.MinimumAge`；**生日当月生效**，周岁口径 = `GameDate.AgeInYearsAt`） | FR-002 |
| 2 | **在册**：复用 `CountedMembers.IsRegistered(person)`（未亡 ∧ 未外嫁），MUST NOT 另写一份判据 | FR-002 |
| 3 | **未服刑**：`StatusFlag.ServingSentence` 位为假 | FR-002 |
| 4 | **不在禁考期**：`StatusFlag.ExamBanned` 位为假（该位与其计时由逻辑轨 ⑥ 维护；本特性**只消费**，MUST NOT 递减或新建） | FR-002、edge case |
| 5 | **无官阶**：`Person.Rank is null` | FR-002 |
| 6 | **非待阙**：`StatusFlag.AwaitingPost` 位为假 | FR-002 |
| 7 | 有可报考级别：`TargetLevelOf(person) is not null` | FR-003 |
| 8 | 性别不构成限制；年龄无上限 | FR-002 |

七项条件全为真 ⇒ 合格。**"已入仕者退出科举"的口径 = 条款 5 ∨ 条款 6 不满足**（有官阶或已在待阙
者不得报名）。

### 3.3 `CanRegister(Person, GameDate) -> bool`

`ExamCalendar.LevelOf(date)` 为 `JieShi` 或 `ShengShi` **∧** `IsEligible(person, date)`
**∧** `TargetLevelOf(person) == LevelOf(date)`。

---

## 4. 报名窗口、幂等与标记

| 条款 | 要求 |
| --- | --- |
| 1 | 报名窗口 = **考试当月**：解试仅科年 8 月，省试仅次年 3 月 |
| 2 | **窗口外**（非考试月，或考试月但不是本人级别）的报名 MUST 被拒：`Register` 抛 `InvalidOperationException`，且 **MUST NOT 改变任何字段**（含失败数、功名史、账本、随机状态） |
| 3 | 殿试**不需报名**：由省试中第者与既有贡士在 (N+1, 4) 自动参加；`CanRegister` 对殿试月恒为 `false` |
| 4 | **幂等**：同一人同一级别重复 `Register` MUST 无副作用（不抛、不改字段、不重复消费任何东西） |
| 5 | 报名 MUST NOT 消耗随机；MUST NOT 读写账本；MUST NOT 追加功名记录 |
| 6 | 报名的持久表达 = `Person.AppliedExam`（考试级别，仅 `JieShi` / `ShengShi` 两态）；MUST NOT 成为第二真源；MUST NOT 在非考试月非空 |
| 7 | `Person.ApplyForExam(DianShi)` ⇒ `InvalidOperationException` |
| 8 | 标记的清除由 `ExamSeasonRunner` 在**考试月月末结算**中完成（无论中第 / 落第 / 免解 / 弃考 / 资格不符），使条款 6 的「非考试月非空」**不可达**；`Person.ClearExamApplication()` 对 `null` 为 no-op |
| 9 | 本特性 MUST NOT 自行改写功名：功名只经 `Person.AppendDegree` 由中第路径追加（契约十三 §6） |

---

## 5. 持续报考（家族级开关）

| 条款 | 要求 |
| --- | --- |
| 1 | 开关 = `Family.AutoExamApplication`（`bool`，默认 `false`） |
| 2 | 开时：每逢报名月（科年 8 月、次年 3 月）由该月月末结算的科举季步**自动报名**，并在**同一结算内**完成判定（FR-017） |
| 3 | 关时：玩家显式发起（`ExamRegistration.Register`） |
| 4 | 自动报名 MUST NOT 消耗随机；MUST NOT 读写账本 |
| 5 | 自动报名的名单 = `EligibleUnapplied(family, date)`：合格 ∧ 未持标记者，按 `PersonId` **升序** |
| 6 | 自动报名 MUST NOT 覆盖已有标记（幂等，同 §4 条款 4） |
| 7 | 连续多科年：开关保持开启期间每个报名月各报一次；错过某科年**不计败**、不影响后续科年 |

**`EligibleUnapplied(Family, GameDate) -> IReadOnlyList<Person>`（FR-018③）**

- 纯查询：调用前后 `GameState` / `Family` / `Person` / 账本 / 随机状态逐字段相同。
- 非解试/省试月 ⇒ 空表；殿试月 ⇒ 空表（无人需要报名）。
- 输出按 `PersonId` 升序；MUST NOT 返回可变集合。

---

## 6. 免解与特奏名的**入口面**（判定面见契约十三 §5 / §7）

### 6.1 免解

免解不是报名入口，而是**判定期**的免掷骰规则（契约十三 §5）。本契约只固定：
免解者的**解试**报名仍走 §4（必须报名才会被判定），且免解**不跨级**——取得资格后解试累计
失败数 MUST NOT 继续增长，但**省试仍须报名（§4）并付路费**（契约十三 §3）。

### 6.2 特奏名

| 条款 | 要求 |
| --- | --- |
| 1 | `SpecialTributeEligible(Person, GameDate)`：满 **50** 岁 ∧ `ShengShiFailureCount >= 6` ∧ `Rank is null` ∧ `AwaitingPost` 位为假 |
| 2 | **询问**由科举季步在**省试落第当月**产出，写入快照（契约十三 §9）；询问本身 **MUST NOT 改任何状态** |
| 3 | 每个科年**询问一次**；拒绝或未应答 ⇒ 状态逐字段不变，可在**下次省试落第后再次询问** |
| 4 | `AcceptSpecialTribute(person, date, random, state = null)`：不满足 §6.2 条款 1 ⇒ `InvalidOperationException` 且 MUST NOT 改任何字段；否则 `AppointmentEntry.Begin(person, AppointmentTrack.SpecialTribute, date, random)`（003 的通用入口）。<br>**008 起追加可选尾参** `GameState? state = null`（形状变更授权见 008 `tasks.md` T055、登记 E-20）：`state is not null` 时在成功后上报 `AchievementTrigger.ExamInquiryAnswered`（契约二十二 §3 第 11 行）；为 `null` 时行为与 005 原签名**逐字段相同**。MUST NOT 影响本条款既有资格判定与 `random` 消耗次序 |
| 5 | 接受后：进入待阙（6~24 月，最终授官 **L18**，由 `OfficialCareerPolicy.InitialRankOf` 单点决定）；**功名变迁史不新增任何记录**，MUST NOT 伪造进士记录 |
| 6 | 接受后 MUST 退出科举：此后不再被自动报名、不再进入判定（由 §3.2 条款 6 在待阙期为假、条款 5 在授官后为假**结构性地**保证） |
| 7 | 拒绝 = **不调用** `AcceptSpecialTribute`；MUST NOT 提供「记录拒绝」的持久字段（那会成为第二真源） |

---

## 7. 查询面（供 U1 / U4）

三个查询 MUST 为**纯函数**：MUST NOT 改状态、MUST NOT 消耗随机（FR-018）。

| # | 查询 | 入口 | 消费方 |
| --- | --- | --- | --- |
| 1 | 某年月 → 该月考试级别（无 / 解试 / 省试 / 殿试） | `ExamCalendar.LevelOf(GameDate)` | U1 快进暂停、U4 跳至下一科举 |
| 2 | 下一考试月（含跨年边界） | `ExamCalendar.NextExamMonth(GameDate)` | U1「跳至下一科举」 |
| 3 | 该考试月合格且未报名者名单 | `ExamRegistration.EligibleUnapplied(Family, GameDate)` | U1 报名提示、U4 批量报名 |

界面 MUST NOT 自行复制资格判定（§3.2）或日历推导（§1）；U1 的「快进在考试月停住」只需查询 1
返回非 `null`。本特性 MUST NOT 实现快进循环本身（FR-029）。

---

## 8. 明确不包含

- 快进循环、「下月」按钮、任何界面的实现（U1；本契约只交付查询面）。
- 调试控制台与「无限制模式」的绕过路径（U4；FR-015 明令规则层 MUST NOT 提供按调用方分支的绕行）。
- 贿赂的触发、金额、被抓概率与惩罚（逻辑轨 ⑥；本契约不含**任何**贿赂字段与入口）。
- `StatusFlag.ExamBanned` 的产生与计时递减（逻辑轨 ⑥；本特性**只消费**）。
- 存档迁移与向下兼容（契约十 §8）。
