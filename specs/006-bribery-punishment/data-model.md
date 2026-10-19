# 阶段 1 设计：贿赂全链路（逻辑轨 ⑥）数据模型

**Feature**: `006-bribery-punishment` | **Date**: 2026-10-13 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Research**: [research.md](./research.md)

本文件回答三件事：**新状态放在哪一层**、**每个成员与函数的形状**、**哪些不变量必须可断言**。
形状与次序的完整条款在 [contracts/](./contracts/)（[契约十五：行贿](./contracts/bribery.md) /
[契约十六：惩罚](./contracts/punishment.md) / [契约十七：数值登记](./contracts/config-registry.md)）。

---

## 1. 层分布总览

| 层 | 新增 | 变更 |
| --- | --- | --- |
| `KFL.Core`（net10.0） | `Entities/PunishmentJournal.cs`（当月事实缓冲）、`ValueObjects/PunishmentFacts.cs`（六个事实行记录） | `Entities/Person.cs` 与 `Entities/Person.Exam.cs`（+3 计数与写入通道）、`Entities/Family.cs`（+`PunishmentJournal`）、`Enums/LedgerCategory.cs` 与 `Enums/LedgerCategoryMetadata.cs`（+2 取值） |
| `KFL.Rules`（net10.0） | `Bribery/`（3）、`Punishment/`（5）、`Config/{BribePolicy,PunishmentPolicy}.cs`、`Economy/TotalAssetsCalculator.cs` | `Settlement/{MonthlySettlementEngine,SettlementResult}.cs`、`Config/GameConfig.Officialdom.cs`、`Exam/ExamAdmission.cs`（仅文档） |
| `KFL.Infrastructure`（net10.0） | 无（零新增文件、零新增依赖） | `Save/SaveFileFormat.cs`、`Save/Serialization/{SaveStateDto,SaveStateValueDtos.Justice,MessagePackSaveFormat,SaveStateMapper.Justice,SaveStateValidator.Justice}.cs` |
| `KFL.Presentation` / `KFL.App` | —— | **0 行**（界面属 U1~U5） |

**依赖方向不变**：`KFL.Core ← KFL.Infrastructure ← KFL.Rules ← KFL.Presentation / KFL.App`
（G-05）；`KFL.Core` 不认识 `KFL.Rules`，故**当月事实的持久化类型在 `KFL.Core`**，`KFL.Rules` 只
把它们**投影**进快照。

---

## 2. `KFL.Core` 的新增与变更

### 2.1 `Person` 新增三成员（`src/KFL.Core/Entities/Person.Exam.cs`）

| 成员 | 类型 | 可变性 | 语义 | 依据 |
| --- | --- | --- | --- | --- |
| `JieShiBribeCount` | `int` | 只读（`private set`） | 解试行贿累计次数，`>= 0` | §7.1「同一考生 × 同一考试级别，跨科累计」 |
| `ShengShiBribeCount` | `int` | 只读 | 省试行贿累计次数，`>= 0` | 同上 |
| `DianShiBribeCount` | `int` | 只读 | 殿试行贿累计次数，`>= 0` | 同上 |

**写入通道（唯一）**：

```csharp
public void RecordBribe(ExamLevel level);          // 对应计数 +1；三级别皆合法；无上限、无清零
public void ResetBribeCounts(ExamLevel? level);    // FR-031 调试入口：级别为 null ⇒ 三级全清
internal void RestoreBribeCounts(int jieShiBribeCount, int shengShiBribeCount, int dianShiBribeCount);
```

**口径**：
1. 三者的取值域是 `ExamLevel` 的三个具名成员——**不存在**「贿赂级别 = 殿试但对解试计数」这类错配，
   `RecordBribe` 的形参即维度。
2. `ResetBribeCounts` **MUST NOT** 把计数降到 0 以下、**MUST NOT** 触碰乘数以外的任何字段；
   乘数是**派生值**（§7.1 的 kⁿ），重置计数即重置乘数——这正是 U4「同行贿叠加」的既有口径。
3. 连坐降级（`PunishmentDemotion`）**MUST NOT** 清零三者（`Person.Exam.cs:134` 的既有约束）。

### 2.2 六个事实行记录（`src/KFL.Core/ValueObjects/PunishmentFacts.cs`）

全部为 `readonly record struct`，字段皆为值类型或 `PersonId` / `GameDate` / `Money`：

```csharp
public readonly record struct BribeFact(
    GameDate Date, PersonId Examinee, ExamLevel Level, int Nth, Money Amount, bool Caught, bool CapReached);

public readonly record struct SentenceFact(GameDate Date, PersonId Subject, int Months);

public readonly record struct FineFact(
    GameDate Date, PersonId Subject, Money Amount, Money PaidFromTreasury, Money ToLoan);

public readonly record struct RelativeBanFact(
    GameDate Date, PersonId Subject, PersonId Relative, int ExamBanMonths, int? PromotionBanMonths);

public readonly record struct CollectivePunishmentFact(
    GameDate Date, PersonId Subject, CollectiveOutcome Outcome, PersonId Relative,
    DegreeLevel? DegreeBefore, DegreeLevel? DegreeAfter);

public readonly record struct ReleaseFact(GameDate Date, PersonId Subject, int? NewRankLevel);
```

`CollectiveOutcome` 是新增枚举（`KFL.Core/Enums/`）：`Executed`（处死）、`DegreeDemoted`（降功名）、
`FamilyWideBan`（受全族禁考禁升）。

### 2.3 `Family` 新增一成员 + `PunishmentJournal`（`src/KFL.Core/Entities/PunishmentJournal.cs`）

```csharp
public sealed class Family { … public PunishmentJournal PunishmentJournal { get; } … }

public sealed class PunishmentJournal
{
    public bool IsEmpty { get; }
    public bool FamilyBanDeclared { get; }
    public IReadOnlyList<BribeFact> Bribes { get; }
    public IReadOnlyList<SentenceFact> Sentences { get; }
    public IReadOnlyList<FineFact> Fines { get; }
    public IReadOnlyList<RelativeBanFact> RelativeBans { get; }
    public IReadOnlyList<CollectivePunishmentFact> CollectivePunishments { get; }
    public IReadOnlyList<ReleaseFact> Releases { get; }

    public void Append(BribeFact fact);             // 与其余五个同款重载
    public void MarkFamilyBanDeclared();
    public void Clear();                            // 仅结算第③步调用（PunishmentAdvance.Run，位于 KFL.Rules）
    internal void Restore(…);                       // 仅 SaveStateMapper 调用
}
```

**口径**：
1. 它是**当月事实缓冲**，不是任何状态的第二真源——状态的真源仍是 `Person` 的计时/状态位与账本；
   日志只承载「已发生但尚未被某次结算报告」的事实（见 [契约十五 §5](./contracts/bribery.md#5-当月事实缓冲fr-028)）。
2. 集合**按 `PersonId` 升序**（并列时按 `Level` / `Relative` 再排序），与快照的断言面一致。
3. **只追加**：MUST NOT 改写或删除既有行；唯一清除路径是第③步的 `Clear()`。
4. 成员集**含归档成员**的 `PersonId`（连坐会处死/外嫁追及，被追及者可能已不在在册集内）。

### 2.4 `LedgerCategory` 的两个新取值（`src/KFL.Core/Enums/`）

| 取值 | 声明序 | `KindOf` | `CreditTargetOf` | 依据 |
| --- | --- | --- | --- | --- |
| `BribeExpense` | 第 **21** 个（`ExamTravelCost` 之后） | `LedgerEntryKind.Treasury`（资金类） | `null`（恒为负） | §7.1、§7.2 |
| `Fine` | 第 **22** 个 | `LedgerEntryKind.Treasury` | `null`（恒为负） | §7.3、§7.5 |

类别全集 **20 → 22**；恒为负的支出类 **5 → 7**；「无入账目标」的类别 **10 → 12**。

---

## 3. `KFL.Rules` 新增

### 3.1 `BribeRules`（`src/KFL.Rules/Bribery/BribeRules.cs`）——全纯函数

```csharp
public static bool IsWithinWindow(GameDate date, ExamLevel level);
public static GameDate WindowStartOf(GameDate date, ExamLevel level);
public static int NthOf(Person examinee, ExamLevel level);                       // = 现计数 + 1
public static Money PayableAmount(ExamLevel level, int nth, Difficulty difficulty);
public static decimal CaughtProbability(ExamLevel level, int nth, Difficulty difficulty);
public static decimal MultiplierOf(ExamLevel level, Person examinee);            // kⁿ（n 含被抓的）
public static decimal AdmissionRateWith(GameState state, Person examinee, ExamLevel level);
public static bool IsLevelCapReached(GameState state, Person examinee, ExamLevel level);
public static bool CanBribe(GameState state, Person examinee, ExamLevel level, out BribeFeasibility feasibility);
```

`AdmissionRateWith` **只调用** 005 的 `ExamAdmission.AdmissionRate` / `ImperialPromotionRate`
（殿试进甲率），把 `MultiplierOf` 作为 `bribeMultiplier` 形参传入——本类 MUST NOT 复制录取率公式。
`CanBribe` 是**纯查询**（不改状态、不消费随机），返回的 `BribeFeasibility` 含
`Amount` / `Probability` / `IsCapReached` / `IsAffordable` / `RejectReason`（枚举）。

### 3.2 `BribeEntry`（`src/KFL.Rules/Bribery/BribeEntry.cs`）——玩家行动

```csharp
public static BribeOutcome Bribe(GameState state, Person examinee, ExamLevel level, IRandomService random);
public static void ResetBribeCounts(Person examinee, ExamLevel? level);
```

固定次序见 [契约十五 §2](./contracts/bribery.md#2-行贿行动的固定次序fr-003fr-010)。`BribeOutcome`
（`Bribery/BribeOutcome.cs`）含：`Accepted`、`RejectReason`、`Amount`、`FromCash` / `FromSavings`
（`PaymentResult` 的前两段；**无 `ToLoan`**——行贿 MUST 全额可付、MUST NOT 转贷款，见 §17 裁决
2026-10-17）、`Caught`、`Nth`、`MultiplierAfter`、`CapReached`，以及
`PunishmentSnapshot? Punishment`（被抓时由 `PunishmentImposer.Apply` 生成，供 U2/U4 当刻反馈）。

### 3.3 `PunishmentImposer`（`src/KFL.Rules/Punishment/PunishmentImposer.cs`）

```csharp
public static PunishmentSnapshot Apply(GameState state, Person subject, ExamLevel level, IRandomService random);
public static Money FineBase(FamilyEconomy economy);        // 转调 TotalAssetsCalculator.Of
public static Money ComputeFine(PunishmentCell cell, FamilyEconomy economy, Difficulty difficulty);
public static void DemoteDegree(Person person, GameDate month);
public static IReadOnlyList<Person> RelativesOf(Family family, Person subject);
```

`Apply` 的次序：① 取该格 → ② 服刑（若 `SentenceMonths > 0`）→ ③ 罚金（`None` 时**不落条目**）→
④ 亲属禁考与/或禁升（按格标记）→ ⑤ 死刑（立即执行、置已亡、归档、进族谱、**MUST NOT 建立服刑计时**）
→ ⑥ 地狱殿试连坐（三分支 + 免死例外）→ ⑦ 全族禁考禁升 48 月（逐人写入）。全部后果**同刻**记入
`PunishmentJournal` 并返回同一份快照。

### 3.4 `PunishmentAdvance`（`src/KFL.Rules/Punishment/PunishmentAdvance.cs`）——结算第③步

```csharp
public static PunishmentSnapshot Run(GameState state);      // 消费随机 0 次
public static bool DemoteOnRelease(Person person);
```

次序见 [契约十六 §1.2](./contracts/punishment.md#12-步内固定次序)。**MUST NOT** 调用 `AdvanceMonth`、
**MUST NOT** 修改 `CurrentDate`、**MUST NOT** 消费随机。

### 3.5 `PunishmentSnapshot`（`src/KFL.Rules/Punishment/PunishmentSnapshot.cs`）

```csharp
public sealed record PunishmentSnapshot
{
    public required GameDate Month { get; init; }
    public required IReadOnlyList<BribeFact> Bribes { get; init; }
    public required IReadOnlyList<SentenceFact> Sentences { get; init; }
    public required IReadOnlyList<FineFact> Fines { get; init; }
    public required IReadOnlyList<RelativeBanFact> RelativeBans { get; init; }
    public required IReadOnlyList<CollectivePunishmentFact> CollectivePunishments { get; init; }
    public required IReadOnlyList<ReleaseFact> Releases { get; init; }
    public required bool FamilyBanDeclared { get; init; }
    public required int PrisonerCount { get; init; }

    public static PunishmentSnapshot None(GameDate month);
}
```

**增量、只读、按 `PersonId` 升序、MUST NOT 回写 `GameState`、MUST NOT 成为第二真源**；它是
**日志的投影**（第③步在清空前一次性构造）。

### 3.6 `PunishmentQueries`（`src/KFL.Rules/Punishment/PunishmentQueries.cs`）——全纯函数

见 [契约十六 §8](./contracts/punishment.md#8-查询面fr-029fr-030)：`IsWithinBribeWindow` /
`Of`（金额 / 概率 / 是否触顶）、`StatusOf`（三计时剩余月数 + 是否有官职）、`PrisonerCount`、
`FamilyCumulativeBribes`、`IsLevelCapReached`、`WasBribeFreeAtImperialPass`。

### 3.7 `PunishmentCommands`（`src/KFL.Rules/Punishment/PunishmentCommands.cs`）——FR-031

```csharp
public static void ClearBans(Person person);
public static void ClearFamilyBans(Family family);
public static void ReleasePrisoner(Person person);
public static void PardonDeathRow(Person person);    // 对已亡者无效（§7.5 死刑立即执行）
```

`ClearFamilyBans` 与 `StatusOf`（「全族禁考/禁升是否生效」）**走同一入口**，MUST NOT 与「个人禁考/禁升」
形成两个互不相认的真源（FR-021）；`ReleasePrisoner` **复用** 第③步的释放路径（含 `DemoteOnRelease`）。

### 3.8 `TotalAssetsCalculator`（`src/KFL.Rules/Economy/TotalAssetsCalculator.cs`）

```csharp
public static Money Of(FamilyEconomy economy);
public static Money Of(Treasury treasury, Holdings holdings);
// = 现金 + 储蓄 + AssetPriceTable.MarketValue(holdings) − Loan.Principal − Loan.AccruedInterest
// 为负或 0 ⇒ 返回 Money.Zero（比例罚金按 0 处理）
```

### 3.9 两个数值类（`src/KFL.Rules/Config/`）

`BribePolicy`：`WindowPreviousMonths`（int，= 1）、`ProgressionStep = 0.2m`、
`BaseAmountGuan(ExamLevel)`（100 / 500 / 2000）、`BaseCaughtRate(ExamLevel)`（`0.05m` / `0.15m` /
`0.35m`）、`EffectMultiplierK(ExamLevel)`（`1.6m` / `1.3m` / `1.1m`）、`CaughtProbabilityCap = 1.0m`、
`UnitMultiplier = 1m`（乘法循环的恒等初值——**k⁰ 的唯一来源**）。

`PunishmentPolicy`：`CellOf(Difficulty, ExamLevel)`（12 格表，`PunishmentCell` 读出全部九个成员）、
`RatioFineCapGuan = 50000`、`FamilyWideBanMonths = 48`、`ReleaseDemotionLevels = 1`、
`MonthsPerYear`（**复用** `IncomeRateTable.MonthsPerYear`）。**注意**：`PunishmentCell` 已逐格承载
「亲属禁考月数（36 / 24 / 48）」与「支出系数（**复用** `DifficultyRates.ExpenseFactor`，MUST NOT 复制）」，
故本特性 **MUST NOT** 另建 `RelativeBanMonthsOf` / `DifficultyExpenseFactor` 之类的第二访问器
（登记为「计划阶段勘误 #5」，见 [plan.md](./plan.md)「勘误登记」）。

`GameConfig` 只加两个**转发视图**（`GameConfig.Bribe` / `GameConfig.Punishment`）与
`GameConfig.Assets.TotalAssets`，**MUST NOT 自带数值字面量**（SC-008）。

---

## 4. 既有类型的形状变更

### 4.1 `SettlementResult`（`src/KFL.Rules/Settlement/SettlementResult.cs`）

```csharp
public required PunishmentSnapshot Punishment { get; init; }    // 006 追加，由第③步产生
```

现有的两条不变量**不变**：`Entries` 逐条等于本次结算追加的条目；`TreasuryPoolAfter − Before` 等于
资金类条目之和（= SC-005）。行贿与罚金的扣款发生在**结算之外**，故它们**不进** `Entries`，
但已体现在 `TreasuryPoolBefore` 的取值里——两条不变量因此仍然成立。

### 4.2 `MonthlySettlementEngine` 步骤序列（九步）

| 步 | 动作 | 消耗随机 | 落条目 |
| --- | --- | --- | --- |
| ① | 提升待生效值 | — | — |
| ② | 米价系数游走 | 1 次 | — |
| **③** | **惩罚推进**（**006 追加**；`PunishmentAdvance.Run`：投影并清空日志 → 逐人递减 → 释放/降级/解除 → 统计服刑人数） | — | —（罚金条目在结算之外已落） |
| ④ | 官吏推进（原 ③） | 命中考课周期者各 1 次 | — |
| ⑤ | 收入（原 ④） | 1 月时 1 次 | 资金类若干 |
| ⑥ | 生活费（原 ⑤） | — | `LivingCost` + 事件类 |
| ⑦ | 贷款（原 ⑥） | 命中节点时 1 次 | 事件类 + 资金类 |
| ⑧ | 科举季（原 ⑦） | 见契约十三 §1.3 | `ExamTravelCost` |
| ⑨ | 推进时间（原 ⑧） | — | — |

**整月随机次序**：`米价（②）→ 考课掷骰（④-d）→ 储蓄利率（仅 1 月，⑤）→ 贷款计息利率（⑦）→
科举判定（⑧）→ 及第入仕待阙（⑧）`——**③ 恰好 0 次**，与 005 的序列逐项同构，只是序号顺移。

### 4.3 `PaymentPrimitive.Pay`（`src/KFL.Rules/Economy/PaymentPrimitive.cs`）

```csharp
public static PaymentResult Pay(
    FamilyEconomy economy, LedgerCategory category, GameDate date, Money amount,
    PersonId? personId = null);        // 006 追加的可选形参；默认 null ⇒ 既有行为逐位不变
```

三段次序（现金 → 储蓄 → 余额转贷款）、不做部分支付、`amount <= 0` 的拒绝语义**一字不动**。
**行贿调用恒不走第三段**（`CanBribe` 的可付性判据已保证现银 + 储蓄 ≥ 应付额，见 §5.1）；
罚金调用仍按三段次序（规格书 §4.3）。

---

## 5. 状态转移

### 5.1 行贿（FR-003~FR-010）

| 起点 | 触发 | 结果 | 日志 / 条目 |
| --- | --- | --- | --- |
| 窗口内、未触顶、**现银 + 储蓄 ≥ 应付额** | `Bribe(state, examinee, level, random)` | 扣款（现金 → 储蓄，**全额付清，MUST NOT 转贷款**）；计数 +1；乘数 = kⁿ（**含被抓当次**）；掷 1 次骰 | 1 条 `BribeExpense`（资金类、负、归属本人）+ 1 行 `BribeFact` |
| 同上 ∧ 命中抓获 | 同一次调用 | 追加第 3.3 节的七段施加 | 1 条 `Fine`（若有）；`SentenceFact` / `RelativeBanFact` / `CollectivePunishmentFact` 若干行 |
| 窗口外 / 未在册 / 级别与功名不符 / 已触顶 / **现银 + 储蓄不足** | `Bribe(...)` | **拒绝**：状态逐字段不变、随机消费 **0**、不落账本条目、**贷款本金不变** | 无 |
| 落榜（考试判定） | `ExamSeasonRunner` | **不退钱、无补偿**，乘数保留 | 无退款条目 |

### 5.2 服刑 / 禁考 / 禁升（FR-014、FR-020）

| 项 | 建立 | 递减 | 归零 |
| --- | --- | --- | --- |
| 服刑 | 被抓当刻（先设计时、后置 `ServingSentence`） | 每结算第③步 −1 | 释放：清计时（先清位、后清计时）→ `DemoteOnRelease` → 仍在禁考期则保留禁考 |
| 禁考 | 被抓当刻（个人档或全族档） | 每结算第③步 −1，**与服刑独立并行** | 清对应计时与状态位 |
| 禁升 | 同上（**只有**含禁升的格子才建） | 同上 | 同上 |

**暂停 vs 跳过**：服刑者（`ServingSentence`）的待阙/在职计时与政绩**暂停**（不推进、不重置）；
禁升期内到期的考课**跳过**（不掷骰、不补判、计时重置）。两者 MUST NOT 混用（003 契约七 §4 条款 6/7）。

### 5.3 连坐（FR-022~FR-026，仅地狱殿试）

| 亲属状态 | 结果 | 记录 |
| --- | --- | --- |
| 无功名 ∧ 无官职 | 处死（置已亡、归档、进族谱） | `CollectivePunishmentFact(Executed)` |
| 有功名 ∧ 无官职 | 降一级功名（进士→贡士→举人→白身；白身无降） | `CollectivePunishmentFact(DegreeDemoted, before, after)` + 1 条 `DegreeRecord(PunishmentDemotion)` |
| 有官职 | 受全族禁考禁升 48 月覆盖（**不存在独立的禁升 60 月**） | `CollectivePunishmentFact(FamilyWideBan)` + 逐人 `RelativeBanFact(48, 48)` |
| 免死例外：配偶有官职 ∧ 孩子未成年 | 该孩子**免死**、状态逐字段不变 | 无 `CollectivePunishmentFact`；全族 48 月照常适用 |

**范围封印**：只取「配偶、儿子、女儿、父亲、母亲」中的**在世者**；兄弟、孙辈、姻亲 MUST NOT 被波及；
**已出嫁归档的女儿被跨族追及**（故亲属集合取 `Family.Members` 而非 `RegisteredMembers`）。

---

## 6. 不变量清单

| # | 不变量 | 验证方式 |
| --- | --- | --- |
| I-1 | 三个贿赂计数 `>= 0`，且只经 `RecordBribe` / `ResetBribeCounts` / `RestoreBribeCounts` 改变 | 反射断言「无公开 setter」；`PersonTests.MembersWithoutWriteChannel` 含三者 |
| I-2 | 乘数是**派生值**：任何时刻 `MultiplierOf(level, p) == k^(计数)`，且 `计数 == 0 ⇒ 乘数 == 1.0` | 逐级别断言；存档往返后差异项 = 0 |
| I-3 | 行贿窗口外、已触顶、级别与功名不符、非在册的调用 **MUST NOT** 改动任何字段、**MUST NOT** 消费随机 | 调用前后逐字段比对 + `FixedRandomService` 调用计数 = 0 |
| I-4 | 被抓**不清零**计数与乘数、**不落**退款或补偿条目 | 第 2 次被抓后 `计数 == 2`、`乘数 == k²`、无退款条目 |
| I-5 | 「计时非空 ⟺ 状态位为真」（001 的既有不变量）：置位次序**先状态位后计时**，清除时**先清计时后清状态位**（001 既有交叉校验 setter 强制；契约十六与此处原写作「先计时后状态位」，已按实现修正，登记为计划勘误 #5） | `Status` / `Timers` 的交叉校验 setter；`SaveStateValidator` 第 ⑤ 条 |
| I-6 | 服刑 / 禁考 / 禁升**三计时各自独立、并行递减**；禁考或禁升 MUST NOT 使现任官员失去官职或俸禄 | 同月跑 60 次：三条计时各自的归零月份互不影响 |
| I-7 | 服刑期间待阙/在职计时与政绩**逐月不变**（不推进、不重置）；释放次月起恢复 | 60 次结算全程逐月比对差异项 = 0 |
| I-8 | 释放降级恰发生 **1 次**，且 `Level >= 18` 时**维持不变**；功名记录条数不变 | `ReleaseFact.NewRankLevel`；`DegreeHistory` 条数比对 |
| I-9 | 罚金金额 `>= 0`；`== 0`（基数为负）时 **MUST NOT** 落条目；资金类条目恒不为 0 | `LedgerInvariantTests` 的条目口径 + 边界夹具 |
| I-10 | 比例罚金 `<= 50000` 贯（**对最终金额**生效）；付不起时 `现金 == 储蓄 == 0 ∧ 贷款本金增加额 == 缺口 ∧ 各池 >= 0` | 超大总资产夹具 + 30 贯夹具 |
| I-11 | 连坐三分支**互斥**、范围严格限于五类在世亲属；其余 11 格连坐触发数 = **0** | 含兄弟/孙辈/姻亲的夹具逐字段比对 |
| I-12 | 免死例外只对「当事人的孩子」成立，且要求「配偶有官职」∧「孩子未成年」**同时**满足 | 四种组合各一断言 |
| I-13 | 降功名 MUST 追加**恰好 1 条** `PunishmentDemotion` 记录，且功名**非升**；白身无降 | `DegreeHistory` 末条比对 |
| I-14 | 当月事实缓冲**只追加**、每月被第③步精确读取一次并清空；缓冲为空时 `SettlementResult.Punishment` 的各集合为空 | 连续两月结算：第二月快照仅含第二月的事实 |
| I-15 | 缓冲内的每一行 `PersonId` MUST 存在于 `Family.Members`（含归档） | `SaveStateValidator` 第 ⑫ 条 |
| I-16 | 查询面①②③④**调用前后 `GameState` 逐字段差异项 = 0 且随机消费 = 0** | SC-008 的核心断言 |
| I-17 | 结算第③步消费随机 **0** 次；整月随机次序与 005 逐项同构（仅序号顺移） | `DeterminismTests` 的逐月 `NextDouble` 计数 |
| I-18 | `SettlementResult.Punishment` MUST NOT 被写回 `GameState`；MUST NOT 成为与 `Person` 并存的第二真源 | 只读 `required init` + 无 setter；调用前后状态比对 |
| I-19 | 新增的三个计数与日志在二进制与 JSON 两种格式下往返后**逐字段相等** | `SaveStateComparer` + `SaveFormatContractTests` |
| I-20 | 行贿 MUST **全额可付**：`现金 + 储蓄 < 应付额` ⇒ 拒绝（状态逐字段不变、随机 0、无账本条目、**贷款本金不变**）；可付时 `BribeExpense` **恰好 1 条**且 `ToLoan` 不存在（MUST NOT 转贷款） | 边界三例（差 1 文 / 恰好相等 / 储蓄补足）各一断言 |

---

## 7. 持久化形状与版本

### 7.1 键表变更（活工件 = `specs/004-save-system/contracts/save-format.md` §4.2）

| DTO | 现成员数 | 新成员数 | 追加的键 |
| --- | --- | --- | --- |
| `PersonDto` | 25（0~24） | **28**（0~27） | 25 `jieShiBribeCount`(`int`)、26 `shengShiBribeCount`(`int`)、27 `dianShiBribeCount`(`int`) |
| `FamilyDto` | 7（0~6） | **8**（0~7） | 7 `punishmentJournal`(`PunishmentJournalDto`) |
| `PunishmentJournalDto`（新） | — | **7**（0~6） | 0 `familyBanDeclared`(`bool`)、1 `bribes`(`BribeFactDto[]`)、2 `sentences`、3 `fines`、4 `relativeBans`、5 `collectivePunishments`、6 `releases` |

事实行 DTO 的键（均自 0 连续，与 `KFL.Core` 的记录成员**逐一对应**）：
`GameDateDto` 2、`BribeFactDto` 7、`SentenceFactDto` 3、`FineFactDto` 5、`RelativeBanFactDto` 5、
`CollectivePunishmentFactDto` 6、`ReleaseFactDto` 3。
（本段原写 `FineFactDto` 5 / `RelativeBanFactDto` 6 / `CollectivePunishmentFactDto` 7，与上表「逐一对应」
自相矛盾；按「逐一对应」裁定为 5 / 5 / 6，并把 `FineFact` 的 `PaymentResult? Paid` 换成
`Money PaidFromTreasury, Money ToLoan`——`PaymentResult` 属 `KFL.Rules`，`KFL.Core` 引用它违反 G-05。
登记为计划勘误 #5。）

**键一律追加在末尾**，既有键的序号与键名一字不动。`MessagePackSaveFormat` 的成员数常量与 walker 的
Skip 代数同步（`PersonMemberCount` 25 → 28、`FamilyMemberCount` 7 → 8、
`PunishmentJournalMemberCount` 新增为 7、六类事实行各新增常量
`BribeFactMemberCount` 7 / `SentenceFactMemberCount` 3 / `FineFactMemberCount` 5 /
`RelativeBanFactMemberCount` 5 / `CollectivePunishmentFactMemberCount` 6 / `ReleaseFactMemberCount` 3）。

### 7.2 版本闸门

`SaveFileFormat.Version` **2 → 3**；接受的唯一谓词是 `FormatVersion == 3`——`> 3`（更新版本写出）与
`< 3`（含旧版 `2` 与 `1`）**一律** `UnsupportedVersion`，**MUST NOT** 提供任何迁移/兼容路径
（004 的 Out of Scope）。

### 7.3 复原次序（`SaveStateMapper.ToDomain`）

- 新的三个计数并入既有**第 ② 步**（标量 + `RestoreExamProgress`），走 `RestoreBribeCounts`；
- `PunishmentJournal` 的复原在**第 ⑦ 步之后、第 ⑧ 步之前**独立一步（`Restore(…)`），不与 `Rank` /
  `Status` / `Timers` 的顺序耦合（缓冲区不参与交叉校验，只做 I-15 的引用完整性校验）。
  > **007 后记（2026-10-17）**：007 在同一位置追加了**第 ⑦″ 步**（`FamilyLifecycleState.RestoreBookkeeping`，
  > 见 [004 data-model §3.11](../../004-save-system/data-model.md) 的 007 后记）；两步次序为
  > ⑦′ `PunishmentJournal.Restore` → ⑦″ 生命周期簿记 → ⑧ `SaveStateValidator`，互不依赖。

### 7.4 同提交必须同步的实现与断言物

见 [plan.md](./plan.md) 的「回写义务清单」第 9/10/16 项——共 **6** 个产品文件、
**10** 个测试文件（含 `SaveFixtures.RichState()` 的非默认值与 `SaveFailureTests` 的版本闸门用例
「旧 **2** 拒绝 / 更新 **4** 拒绝」）。

---

## 8. 需求 → 模型映射

| 需求 | 模型 | 落点 |
| --- | --- | --- |
| FR-001、FR-002、SC-002 | `BribePolicy` + `PunishmentPolicy` + `GameConfig.Bribe/Punishment` | `Contracts/BribeQueryTests`/`PunishmentMatrixTests`；`Architecture/ConfigLiteralTests` 的 `RegisteredValues` 46 → 49（`1.6m` / `0.15m` / `0.35m`） |
| FR-003~FR-007 | `BribeRules.IsWithinWindow` / `PayableAmount` / `CaughtProbability` + `BribeEntry.Bribe` | `Rules/BribeEntryTests.cs`（SC-002/003） |
| FR-008、SC-003 | `BribeRules.MultiplierOf` + `ExamAdmission` 的同一入口；`NeutralBribeMultiplier` 删除 | `BribeEntryTests`、`ExamAdmissionTests`、`SpecAnchorTests` |
| FR-009、FR-010 | 无退款条目；`IsLevelCapReached` + `CanBribe` 的拒绝面 | `BribeEntryTests`、`BribeQueryTests`（SC-008） |
| FR-011、SC-005 | `TotalAssetsCalculator.Of`（单一访问器）+ 工 bonus 复用 | `PunishmentMatrixTests`（`SettlementEngineTests` 的工 bonus 锚点同步） |
| FR-012、FR-013 | `PunishmentImposer.ComputeFine` + `PaymentPrimitive.Pay`（新可选形参） | `PunishmentMatrixTests`（SC-005） |
| FR-014~FR-016、SC-004 | `PunishmentPolicy.CellOf` + `PunishmentImposer.Apply` | `PunishmentMatrixTests`（12 格逐格） |
| FR-017~FR-019、SC-006 | `PunishmentAdvance.Run` + `DemoteOnRelease` | `PunishmentAdvanceTests` |
| FR-020、FR-021 | 第③步位置；`PunishmentCommands.ClearBans/ClearFamilyBans` | `PunishmentAdvanceTests`、`BribeQueryTests` |
| FR-022~FR-026、SC-007 | `PunishmentImposer.RelativesOf` + 三分支 + 免死 | `CollectivePunishmentTests` |
| FR-027 | `LedgerCategory.BribeExpense` / `Fine` + 元数据 | `Core/LedgerInvariantTests`（20 → 22；守卫改写） |
| FR-028、FR-030 | `PunishmentJournal` → `SettlementResult.Punishment` | `BribeQueryTests`、`DeterminismTests` |
| FR-029、SC-008 | `PunishmentQueries` 的七个成员 | `BribeQueryTests` |
| FR-031 | `BribeEntry.ResetBribeCounts` + `PunishmentCommands` | `BribeEntryTests`、`BribeQueryTests` |
| FR-032、SC-009 | 键表 + 版本 2 → 3 + `SaveStateValidator` 第 ⑪/⑫ 条 | `Infrastructure/*`（5 个文件） |
| FR-033、SC-001 | 全部随机经 `IRandomService`；行贿 1 次、第③步 0 次 | `DeterminismTests` |
| FR-034、FR-035、SC-010、SC-011 | 5 个新测试文件 + 回写清单 | 门禁四条 + `tasks.md` 的回写登记 |

---

## 9. 明确不包含

界面（U1 主界面与下月/快进、U2 双视图与卡片、U3 统计页、U4 调试面板、U5 设置）、成就解锁与绝嗣判定
（逻辑轨 ⑧）、成员成长/衰老/疾病/寿命死亡（⑦）、婚育/买人口/买功名婚姻（⑦）、族谱排序与死亡结算的
实现、存档迁移与向下兼容、官名表、跨 .NET 版本的随机数稳定性、以及「死囚待决」的持久状态
（§7.5 明写死刑**立即执行**，故规则层不存在「已判未决」的对象）。上述各项在本特性**没有写入通道**
（不可达），而非「暂未实现」。
