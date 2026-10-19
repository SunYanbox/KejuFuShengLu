# 阶段 1 设计：科举周期（逻辑轨 ⑤）数据模型

**Feature**: `005-exam-cycle` | **Date**: 2026-10-12 | **Spec**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

> 本文件是**形状的真源**：字段、键、签名、次序。**数值一律不在此声明**（真源 = 规格书 §6 / §7.1 /
> §7.2 / §8.2 / §11，登记见 [契约十四](./contracts/config-registry.md)）。层与序号口径同
> [research.md](./research.md) 抬头。

---

## 1. 层分布总览

| 层 | 新增 | 变更 |
| --- | --- | --- |
| `KFL.Core` | 枚举 `ExamLevel` | `Person` +3 成员（+3 个写入通道 +1 个 restore 通道）；`Family` +1 成员；`LedgerCategory` +1 取值；`LedgerCategoryMetadata` 两个 switch +1 分支 |
| `KFL.Infrastructure` | — | `FamilyDto` +1 键；`PersonDto` +3 键；`MessagePackSaveFormat` 两个成员数常量 + walker；`SaveStateMapper` 双向映射；`SaveStateValidator` +1 条；`SaveFileFormat.Version` 1 → 2 |
| `KFL.Rules` | `Exam/ExamCalendar.cs`、`Exam/ExamRegistration.cs`、`Exam/ExamAdmission.cs`、`Exam/ExamSeasonRunner.cs`、`Exam/ExamSeasonSnapshot.cs`、`Config/ExamPolicy.cs` | `SettlementResult` +1 子段；`MonthlySettlementEngine` 七步 → 八步；`GameConfig` +1 转发视图 |
| `KFL.Presentation` / `KFL.App` | — | **无**（FR-029：本特性只交付规则层与查询面） |

工程数不变（恰六个，G-02）；依赖边集不变（G-05）。

---

## 2. `KFL.Core` 的新增与变更

### 2.1 枚举 `ExamLevel`（新文件 `src/KFL.Core/Enums/ExamLevel.cs`）

```csharp
public enum ExamLevel
{
    JieShi   = 1,   // 解试：科年 8 月（规格书 §6）
    ShengShi = 2,   // 省试：科年次年 3 月（规格书 §6）
    DianShi  = 3,   // 殿试：科年次年 4 月（规格书 §6）
}
```

- **不设 `None = 0`**：「无考试」用 `ExamLevel?` 的 `null` 表达（research.md R-02）。
- `0` 与 `> 3` **不是具名成员** ⇒ 二进制存档里的越界整数在复原第 ⑦ 步被 `Enum.IsDefined` 拒绝
  （`InvalidStructure`，004 契约十 §6 条款 6）。
- 文档口径：考试级别；报名标记只允许 `JieShi` / `ShengShi`（殿试不需报名）。
- 类文档 MUST 引用规格书 §6，并写明「派生值（科年与考试月）MUST NOT 持久化」。

### 2.2 `Person` 新增三成员（`src/KFL.Core/Entities/Person.Exam.cs`）

| 成员 | 类型 | 访问 | 含义 | 来源 |
| --- | --- | --- | --- | --- |
| `JieShiFailureCount` | `int` | `get; private set;` | 解试累计失败数（`>= 0`，单调递增） | FR-023 |
| `ShengShiFailureCount` | `int` | `get; private set;` | 省试累计失败数（`>= 0`，单调递增） | FR-023 |
| `AppliedExam` | `ExamLevel?` | `get; private set;` | 报名标记：`null` / `JieShi` / `ShengShi` | FR-005、FR-023 |

**公开写入通道（唯一）**：

```csharp
public void ApplyForExam(ExamLevel level);       // 仅 JieShi / ShengShi；幂等；否则 InvalidOperationException
public void ClearExamApplication();              // AppliedExam == null ⇒ no-op（幂等）
public void RecordExamFailure(ExamLevel level);  // 仅 JieShi / ShengShi；对应计数 +1
```

**复原通道**（与既有 `SetGeneration` / `SetSpouse` / `AddFormerSpouse` 同款，唯一程序集面
`InternalsVisibleTo("KFL.Infrastructure")`）：

```csharp
internal void RestoreExamProgress(int jieShiFailureCount, int shengShiFailureCount, ExamLevel? appliedExam);
```

**行为规约**：

1. `ApplyForExam(level)`：`level == DianShi` ⇒ `InvalidOperationException`（殿试不需报名，Q2 裁决）；
   已有标记且 `== level` ⇒ **无操作且不抛**（FR-004 幂等）；已有标记且 `!= level` ⇒
   `InvalidOperationException`（调用方错误）；`JieShiFailureCount` / `ShengShiFailureCount` **不受影响**。
2. `ClearExamApplication()`：`null` ⇒ no-op；否则置 `null`。MUST NOT 动任何其它字段。
3. `RecordExamFailure(level)`：`level == DianShi` ⇒ `InvalidOperationException`（殿试不淘汰，Q1 裁决）；
   否则对应计数 +1（**无上限**；免解后不再增长由规则层保证，见 §5.3）。
4. **MUST NOT 新增公开 setter**（001 不变量 2、FR-023）：三个属性在反射下 `GetSetMethod() == null`，
   与既有「无公开 setter」断言集合同形。

### 2.3 `Family` 新增一成员（`src/KFL.Core/Entities/Family.cs`）

| 成员 | 类型 | 访问 | 含义 | 来源 |
| --- | --- | --- | --- | --- |
| `AutoExamApplication` | `bool` | `get; set;`（默认 `false`） | 持续报考开关 | FR-017、FR-023 |

- 形状与既有 `HasShiStatus` 同款（`bool` 可写）；不引入新枚举（两态不需要类型）。
- 落盘键 `FamilyDto[6]`（§7）。

### 2.4 `LedgerCategory.ExamTravelCost` + 元数据

```csharp
// src/KFL.Core/Enums/LedgerCategory.cs —— 追加为第 20 个取值（声明序末位）
ExamTravelCost,   // 省试路费：资金类、恒为负（规格书 §6）

// src/KFL.Core/Enums/LedgerCategoryMetadata.cs
KindOf(ExamTravelCost)        => LedgerEntryKind.Treasury
CreditTargetOf(ExamTravelCost) => null            // 恒为负 ⇒ 无正额入账目标
```

- 两处 `default:` 异常文案与两处 `<exception>` 文档的「共 19 个取值」→「共 20 个取值」。
- `LedgerCategory` 的类文档同步（19 → 20；且仍 MUST NOT 出现「买人口」「聘礼/嫁妆」「贿赂」等类别）。
- 资金类 + `CreditTargetOf == null` 恰好就是既有「恒为负支出类」的既有含义，**不引入新概念**。

---

## 3. `KFL.Rules` 新增

### 3.1 `ExamCalendar`（`src/KFL.Rules/Exam/ExamCalendar.cs`）

```csharp
public static class ExamCalendar
{
    public static bool IsKeYear(int year);                 // year >= 1 && year % 3 == 1
    public static ExamLevel? LevelOf(GameDate date);        // 非考试月 ⇒ null
    public static GameDate NextExamMonth(GameDate date);    // 严格晚于 date 的第一个考试月
}
```

派生规则（FR-001）：`year % 3 == 1` 的年份 `N` 是科年 ⇒ `(N, 8)` 解试、`(N+1, 3)` 省试、
`(N+1, 4)` 殿试；其余月份无考试。

**纯函数约束**：三者 MUST NOT 读 `GameState`、MUST NOT 改任何状态、MUST NOT 消耗随机、
MUST NOT 依赖系统时钟（G-07）。

### 3.2 `ExamRegistration`（`src/KFL.Rules/Exam/ExamRegistration.cs`）

```csharp
public static class ExamRegistration
{
    public static ExamLevel? TargetLevelOf(Person person);
    public static bool IsEligible(Person person, GameDate date);
    public static bool CanRegister(Person person, GameDate date);
    public static void Register(Person person, GameDate date);
    public static IReadOnlyList<Person> EligibleUnapplied(Family family, GameDate date);
    public static bool SpecialTributeEligible(Person person, GameDate date);
    public static void AcceptSpecialTribute(
        Person person, GameDate date, IRandomService random, GameState? state = null);
}
```

- `TargetLevelOf`：`BaiShen → JieShi`、`JuRen → ShengShi`、`GongShi → DianShi`、`JinShi → null`；
  其它取值 ⇒ `ArgumentOutOfRangeException`。
- `IsEligible`：`CountedMembers.IsRegistered(person)`（**复用**既有定义，不新写）∧ 未服刑 ∧ 未禁考
  ∧ `Rank == null` ∧ `AwaitingPost` 位为假 ∧ `AgeAt(date) >= ExamPolicy.MinimumAge` ∧
  `TargetLevelOf(person) is not null`。
- `CanRegister`：`LevelOf(date)` 为 `JieShi` / `ShengShi` ∧ `IsEligible` ∧
  `TargetLevelOf(person) == LevelOf(date)`。
- `Register`：`!CanRegister` ⇒ `InvalidOperationException`，且 MUST NOT 改任何字段（FR-004）；
  否则 `person.ApplyForExam(LevelOf(date)!.Value)`。MUST NOT 读写账本、MUST NOT 消耗随机。
- `EligibleUnapplied`：`AppliedExam is null ∧ TargetLevelOf(p) == LevelOf(date) ∧ IsEligible(p, date)`，
  按 `PersonId` 升序；非解试/省试月或无人合格 ⇒ 空表。**纯函数**（FR-018③）。
- `SpecialTributeEligible`：`AgeAt(date) >= ExamPolicy.SpecialTributeMinimumAge` ∧
  `ShengShiFailureCount >= ExamPolicy.SpecialTributeFailureThreshold` ∧ `Rank == null` ∧
  `AwaitingPost` 位为假（FR-012）。
- `AcceptSpecialTribute`：`!SpecialTributeEligible` ⇒ `InvalidOperationException`，MUST NOT 改任何字段；
  否则 `AppointmentEntry.Begin(person, AppointmentTrack.SpecialTribute, date, random)`。
  **MUST NOT 追加任何功名记录**（FR-012、Q3 裁决）。
  008 起追加**可选尾参** `GameState? state = null`（形状变更授权：008 `tasks.md` T055，登记 E-20）：
  `state is not null` 时成功后上报 `AchievementTrigger.ExamInquiryAnswered`（契约二十二 §3 第 11 行），
  `null` 时与 005 原签名逐字段相同；资格判定与 `random` 消耗次序 MUST NOT 受影响。

### 3.3 `ExamAdmission`（`src/KFL.Rules/Exam/ExamAdmission.cs`）

```csharp
public static class ExamAdmission
{
    public static decimal AdmissionRate(ExamLevel level, Person person, bool hasShiStatus, decimal bribeMultiplier);
    public static decimal ImperialPromotionRate(Person person, decimal bribeMultiplier);
    public static ImperialClass ImperialClassFor(Person person, bool promoted);
    public static ImperialPlacement RollPlacement(IRandomService random);
}
```

- `AdmissionRate`：只接受 `JieShi` / `ShengShi`（`DianShi` ⇒ `ArgumentOutOfRangeException`）；
  公式与乘区见 [契约十三 §4](./contracts/exam-session.md#4-录取率与进甲率)；
  返回值在 `[0, ExamPolicy.DiceRateCap]`（**内部 clamp**，调用方无绕过分支）。
- `ImperialPromotionRate`：**MUST NOT 乘仕族加成**（FR-009、SC-002 的封印项）；同样内部 clamp。
- `ImperialClassFor(person, promoted)`：`promoted` ⇒ `FirstClass`；
  否则 `person.Study >= ExamPolicy.SecondClassStudyThreshold ? SecondClass : ThirdClass`。
- `RollPlacement(random)`：`random.Next(1, 4)` 恰好 **1 次**，映射 `1 → ZhuangYuan`、
  `2 → BangYan`、`3 → TanHua`（均匀、含端点、等概率）。
- 四个方法**全部为纯函数**（`RollPlacement` 只经 `IRandomService`），MUST NOT 读全局态、
  MUST NOT 落账本、MUST NOT 改 `Person`。

### 3.4 `ExamSeasonRunner`（`src/KFL.Rules/Exam/ExamSeasonRunner.cs`）

```csharp
public static class ExamSeasonRunner
{
    public static ExamSeasonSnapshot Run(GameState state, IRandomService random);
}
```

调用点：`MonthlySettlementEngine.Settle` 的**第 ⑨ 步**（§4.2；**007 后记**：原第 ⑦ 步，因 006 插入
③ 惩罚推进、007 插入 ⑧ 人口与健康推进而两次顺移——本步位置始终钉在「贷款之后、推进时间之前」）。
内部次序与随机消费次序逐字见
[契约十三 §1~§2](./contracts/exam-session.md#1-步骤位置与内部次序)。`Run` 的**可变**范围严格限定为：

- `Person`：`ApplyForExam` / `ClearExamApplication` / `RecordExamFailure` / `AppendDegree`；
- `Family`：`AutoExamApplication`（只读）、`HasShiStatus`（置真）；
- `FamilyEconomy`：`Apply(LedgerCategory.ExamTravelCost, …)`；
- 及第入仕经 `AppointmentEntry.BeginForImperialGraduate`（003 的入口，**MUST NOT 自行写
  `Person.Rank` / `MonthsInOffice` / 账本**）。

非考试月：**不消费任何随机、不产生任何变更、返回 `Level == null` 的空快照**。

### 3.5 `ExamSeasonSnapshot`（`src/KFL.Rules/Exam/ExamSeasonSnapshot.cs`）

```csharp
public enum ExamAbsenceReason { IneligibleBeforeJudgement, InsufficientTravelCost }

public sealed record ExamApplicationLine(PersonId PersonId, ExamLevel Level);

public sealed record ExamAbsenceLine(PersonId PersonId, ExamLevel Level, ExamAbsenceReason Reason);

public sealed record ExamOutcomeLine(
    PersonId PersonId,
    ExamLevel Level,
    bool Passed,
    bool Exempted,                       // 免解行使（仅解试月可能为真）
    ImperialClass? ImperialClass,
    ImperialPlacement? Placement,        // 非空 ⟺ ImperialClass == FirstClass
    int FailureCountAfter,               // 该级别累计失败数的**变更后**值
    DegreeLevel DegreeAfter);

public sealed record ExamAppointmentLine(PersonId PersonId, AppointmentTrack Track, int AwaitingPostMonths);

public sealed record ExamSeasonSnapshot
{
    public required GameDate Month { get; init; }
    public required ExamLevel? Level { get; init; }
    public required IReadOnlyList<ExamApplicationLine> AutoApplications { get; init; }
    public required IReadOnlyList<ExamAbsenceLine> Absences { get; init; }
    public required IReadOnlyList<PersonId> Exemptions { get; init; }
    public required IReadOnlyList<ExamOutcomeLine> Outcomes { get; init; }
    public required IReadOnlyList<ExamAppointmentLine> Appointments { get; init; }
    public required IReadOnlyList<PersonId> SpecialTributeInquiries { get; init; }
    public required bool HasShiStatusBecomesTrue { get; init; }
    public static ExamSeasonSnapshot None(GameDate month);   // Level = null，全空
}
```

- 全部集合按 `PersonId` 升序；同一结算内**不跨月合并**（3 月省试与 4 月殿试各一条快照）。
- 增量、只读、MUST NOT 回写 `GameState`、MUST NOT 成为第二真源（FR-022）。
- 省试路费的金额**不进快照**——它已是 `SettlementResult.Entries` 里的
  `LedgerCategory.ExamTravelCost` 条目（避免第二个金额真源）；弃考**没有**条目，故以 `Absences` 表达。

### 3.6 `ExamPolicy`（`src/KFL.Rules/Config/ExamPolicy.cs`）

FR-014 全部数值的单点声明处，逐项见 [契约十四 §1](./contracts/config-registry.md#1-科举数值登记)：
`MinimumAge`、`JieShiBaseRate`、`ShengShiBaseRate`、`ImperialPromotionBaseRate`、`StudyFactorBase`、
`StudyFactorPerPoint`、`OfficialdomDivisor`、`ShiStatusAdmissionBonus`、`SecondClassStudyThreshold`、
`DiceRateCap`、`JieShiExemptionFailureThreshold`、
`SpecialTributeMinimumAge`、`SpecialTributeFailureThreshold`、`ExamTravelCostGuan(Difficulty)`。

> **006 后记（2026-10-13）**：本清单原有 `NeutralBribeMultiplier` 一项，已在
> `006-bribery-punishment` 落地时**删除**（占位作废）——行贿乘数改由
> `BribeRules.MultiplierOf(ExamLevel, Person)` 派生（`kⁿ`，`k⁰` 由 `BribePolicy.UnitMultiplier = 1m`
> 唯一给出）。`ExamAdmission.AdmissionRate` / `ImperialPromotionRate` 的
> `bribeMultiplier` 形参**逐字未变**，只是一律接收实际乘数而非占位常量。

`GameConfig` 只增一个转发视图（形如既有的 `GameConfig.Career`），**MUST NOT 自带任何数值字面量**。

---

## 4. 既有类型的形状变更

### 4.1 `SettlementResult`（`src/KFL.Rules/Settlement/SettlementResult.cs`）

新增一个 `required init` 子段：

```csharp
public required ExamSeasonSnapshot ExamSeason { get; init; }
```

既有成员（`Month`、`GrainPrice**`、`LivingCosts`、`Incomes`、`NetProfit`、`Loan**`、`SavingsInterest`、
`ArtisanBonus`、`Famine**`、`Entries`、`TreasuryPool**`、`Career`）**一字不改**。
新增不变式：`Entries` 中 `ExamTravelCost` 条目数 = 该结算实际收费人数；`Absences` 中
`InsufficientTravelCost` 的行对一个 `ExamTravelCost` 条目都不对应。

### 4.2 `MonthlySettlementEngine` 步骤序列（`src/KFL.Rules/Settlement/MonthlySettlementEngine.cs`）

| # | 步骤 | 变更 |
| --- | --- | --- |
| ① | 提升待生效值 | 不变 |
| ② | 米价系数游走（1 次 `NextDouble`） | 不变 |
| ③ | 官吏推进（考课掷骰） | 不变 |
| ④ | 收入（1 月掷储蓄利率；12 月结息） | 不变 |
| ⑤ | 生活费（含饥馑） | 不变 |
| ⑥ | 贷款（计息 → 划扣） | 不变 |
| **⑦** | **科举季**（`ExamSeasonRunner.Run`） | **新增** |
| **⑧** | 推进时间（`GameState.AdvanceMonth`） | 原 ⑦ 顺移 |

类文档：七步 → 八步；`MUST NOT 存在` 清单**移除**「科举季触发（阶段 ⑤）」；随机消费次序补入
「科举判定 → 及第入仕待阙」（契约三 §4 同提交回写）。

---

## 5. 状态转移

### 5.1 功名链（FR-010，既有 `AppendDegree` 通道）

```text
白身 --解试中第/免解--> 举人 --省试中第--> 贡士 --殿试（必中）--> 进士
                                                              ├─ 一甲（Placement 非空）
                                                              ├─ 二甲（学业 >= 50）
                                                              └─ 三甲（学业 <  50）
```

每次转移追加**恰好一条** `DegreeRecord(level, placement, month, DegreeChangeCause.ExamPass, imperialClass)`；
`ChangedAt` 非递减（`AppendDegree` 既有校验）。一甲/二甲/三甲与名次的兼容性四不变量由
`DegreeRecord` 构造期保证：**名次非空 ⟺ `ImperialClass == FirstClass`**。

### 5.2 报名标记生命周期（FR-005）

```text
(null) --Register / 自动报名--> JieShi | ShengShi        （仅考试当月；窗口外 Register 被拒）
JieShi | ShengShi --本月结算的科举季步（消费后清除）--> (null)
```

- 报名窗口 = 考试当月（解试仅科年 8 月，省试仅次年 3 月）；殿试**不需**报名。
- 标记在考试月月末结算中被**消费并清除**（无论中第、落第、免解、弃考、资格不符）。
- 「非考试月非空」**不可达**：入口只在窗口内写入，当月的结算步必然清除。该性质由规则层保证
  （实体无时钟），并由测试断言（[契约十二 §4](./contracts/exam-calendar.md#4-报名窗口幂等与标记)）。

### 5.3 累计失败数（FR-007、FR-008、FR-013）

| 级别 | 何时 +1 | 何时不再增长 |
| --- | --- | --- |
| 解试 | **实际报名并应考**而落第 | 达到 `JieShiExemptionFailureThreshold` 后（此后每次解试都免掷骰中第） |
| 省试 | **实际报名并应考**而落第 | 无上限（特奏名门槛为 `>= SpecialTributeFailureThreshold`） |

**不计败**的四种情形：未报名；弃考（现金不足）；判定前资格不符被跳过；错过科年。

### 5.4 特奏名（FR-012，Q3 裁决）

```text
省试落第 ∧ 满 50 岁 ∧ 省试累计失败数 >= 6 ∧ 无官阶 ∧ 非待阙
   ⇒ 本步产出「一次询问」（快照 SpecialTributeInquiries，不改任何状态）
       ├─ 接受 ⇒ AppointmentEntry.Begin(person, SpecialTribute, month, random)
       │           ⇒ 待阙 6~24 月、最终授官 L18；**功名史不新增记录**；此后退出科举
       └─ 拒绝 / 未应答 ⇒ 状态逐字段不变；下次省试落第后**再次**询问
```

### 5.5 `HasShiStatus`（FR-020）

```text
家族产生首个进士的当月： HasShiStatus == false ⇒ 置 true（幂等；已为 true 时 no-op）
之后： 解试/省试录取率 × ShiStatusAdmissionBonus；殿试进甲率 **不受影响**
```

- 这是 `Family.HasShiStatus` 在仓库中的**首个生产者**。
- 对贷款月划扣比例（仕 20%）的影响**自次月**生效（当月结算的 ⑥ 贷款步已在本步之前执行）。

---

## 6. 不变量清单

**实体级（`KFL.Core`，构造器 / setter 强制）**

| # | 不变量 | 来源 |
| --- | --- | --- |
| I-1 | `JieShiFailureCount >= 0` ∧ `ShengShiFailureCount >= 0` | FR-023 |
| I-2 | `AppliedExam is null` ∨ `AppliedExam ∈ { JieShi, ShengShi }` | FR-005、Q2 |
| I-3 | `Person` 的上述三成员**无公开 setter** | 001 不变量 2、FR-023 |
| I-4 | `DegreeRecord.Placement is not null ⟺ DegreeRecord.Class == FirstClass` | FR-009（既有） |
| I-5 | `LedgerCategory.ExamTravelCost` 的 `KindOf == Treasury` ∧ `CreditTargetOf == null` | FR-021 |

**规则级（`KFL.Rules`，测试断言）**

| # | 不变量 | 来源 |
| --- | --- | --- |
| I-6 | 非考试月：随机消费 0 次、状态变更 0 处 | FR-019 |
| I-7 | 报名（含自动报名）：随机消费 0 次、账本条目 0 条 | FR-004、FR-017 |
| I-8 | 免解（解试达阈值的那一次）：随机消费 0 次 | FR-013、SC-004 |
| I-9 | 弃考：随机消费 0 次、账本条目 0 条、失败数 +0 | FR-008、SC-006 |
| I-10 | `AppliedExam` 在本月结算结束后必为 `null` | FR-005 |
| I-11 | 同一结算内 `AppendDegree` 追加的条数 = 中第人数（1 人 1 条） | FR-010 |
| I-12 | 殿试录取率与封顶无关（100%）；封顶只作用于解试/省试录取率与殿试进甲率 | FR-009、FR-015、SC-002 |
| I-13 | 进甲率与 `HasShiStatus` 无关（真/假结果完全相同） | FR-009、SC-002 |
| I-14 | 特奏名接受后：功名记录条数不变 | FR-012、SC-005 |
| I-15 | `ExamTravelCost` 条目的金额 = `ExamTravelCostGuan(Difficulty)`，**不乘难度支出系数** | FR-008、FR-021 |

---

## 7. 持久化形状与版本

### 7.1 键表变更（活工件 = `specs/004-save-system/contracts/save-format.md` §4.2/§4.3）

| DTO | 新增键 | 类型 | 成员数 |
| --- | --- | --- | --- |
| `FamilyDto` | **6** `autoExamApplication` | `bool` | 6 → **7** |
| `PersonDto` | **22** `jieShiFailureCount` | `int` | 22 → **25** |
| `PersonDto` | **23** `shengShiFailureCount` | `int` | ↑ |
| `PersonDto` | **24** `appliedExam` | `ExamLevel?` | ↑ |

其余 DTO 键表**一字不动**。`SavedStateDto`（0..7）不变。金额一律以「文」为单位的 `decimal`
（本特性无新增金额字段；路费走既有 `LedgerEntryDto.amountWen`）。

### 7.2 版本闸门

| 常量 | 旧 | 新 |
| --- | --- | --- |
| `SaveFileFormat.Version` | `1` | **`2`** |

闸门口径（004 契约十 §6 第 ④ 步）由 `FormatVersion == 1` 改为 `FormatVersion == 2`；
`< 2` 与 `> 2` 一律 `UnsupportedVersion`。**MUST NOT 提供任何迁移/兼容路径**（FR-024）。

### 7.3 同提交必须同步的实现与断言物

`SaveStateDto.cs`（两个 DTO 的 `[Key]` + `[JsonPropertyName]` + `[JsonRequired]`）→
`MessagePackSaveFormat`（`PersonMemberCount` 22 → 25、`FamilyMemberCount` 6 → 7、
`EnsurePersonShape` / `EnsureFamilyShape` 的 Skip 计数）→ `SaveStateMapper`
（`ToDto(Person)` / `Build` / `CreatePerson`；复原次序见 004 data-model §3.11）→
`SaveStateValidator`（第 ⑩ 条：`appliedExam` 非空 ⇒ `∈ { JieShi, ShengShi }`；两个失败数 `>= 0`）→
`SaveStateComparer.AssertPerson/AssertFamily` → `SaveFormatContractTests.BinaryKeyTables`
（PersonDto 数组 22 列 → 25 列）+ `mapping` 常量表 + `ContractKeyTable`（路径条目）→
`SaveFailureTests` 的两条版本闸门用例（旧版 **1** 拒绝 / 更新版 **3** 拒绝）→ `SaveFixtures.RichState()`
（新字段带**非默认**值，否则往返测试静默通过）。

---

## 8. 需求 → 模型映射

| 需求 | 落地 |
| --- | --- |
| FR-001 考试月纯派生 | `ExamCalendar.LevelOf` / `NextExamMonth` / `IsKeYear` |
| FR-002 报考资格 | `ExamRegistration.IsEligible` |
| FR-003 报考级别 | `ExamRegistration.TargetLevelOf` |
| FR-004 报名窗口/幂等 | `ExamRegistration.CanRegister` / `Register`；`Person.ApplyForExam` |
| FR-005 报名标记 | `Person.AppliedExam` + `ApplyForExam` / `ClearExamApplication` |
| FR-006 判定前复核 | `ExamSeasonRunner` ② 的复核分支 + `ExamAbsenceLine` |
| FR-007 解试录取率 | `ExamAdmission.AdmissionRate` + `ExamPolicy` |
| FR-008 省试录取率 + 路费 | 同上 + `ExamSeasonRunner` ② + `LedgerCategory.ExamTravelCost` |
| FR-009 殿试两步 | `ExamAdmission.ImperialPromotionRate` / `ImperialClassFor` / `RollPlacement` |
| FR-010 功名记录追加 | `Person.AppendDegree`（既有） |
| FR-011 及第入仕 | `AppointmentEntry.BeginForImperialGraduate`（003 入口） |
| FR-012 特奏名 | `ExamRegistration.SpecialTributeEligible` / `AcceptSpecialTribute` + 快照 `SpecialTributeInquiries` |
| FR-013 免解 | `ExamSeasonRunner` ③ + `ExamPolicy.JieShiExemptionFailureThreshold` |
| FR-014 数值集中 | `ExamPolicy` + `GameConfig.Exam` |
| FR-015 封顶口径 | `ExamPolicy.DiceRateCap` + 两个 rate 方法内部 clamp |
| FR-016 贿赂乘数接缝 | `AdmissionRate` / `ImperialPromotionRate` 的 `bribeMultiplier` 形参（006 起实际值由 `BribeRules.MultiplierOf` 给出，占位常量已删除） |
| FR-017 持续报考 | `Family.AutoExamApplication` + `ExamSeasonRunner` ① |
| FR-018 查询面 | `ExamCalendar.LevelOf` / `NextExamMonth` + `ExamRegistration.EligibleUnapplied` |
| FR-019 步骤位置与次序 | §4.2 + [契约十三 §1~§2](./contracts/exam-session.md#1-步骤位置与内部次序) |
| FR-020 `HasShiStatus` | `ExamSeasonRunner` ④ + `ExamSeasonSnapshot.HasShiStatusBecomesTrue` |
| FR-021 路费账本类别 | §2.4 |
| FR-022 增量快照 | `ExamSeasonSnapshot` + `SettlementResult.ExamSeason` |
| FR-023 新增持久状态 | §2.2、§2.3 |
| FR-024 落盘与版本 | §7 |
| FR-025~FR-026 测试与可复现 | [quickstart.md](./quickstart.md) 的场景 + `IRandomService` 注入 |
| FR-027 事件流与计数 | `ExamSeasonSnapshot.Outcomes`（含 `FailureCountAfter`）+ `Entries` |
| FR-028 回写义务 | [plan.md](./plan.md) 的「回写义务清单」 |
| FR-029 范围限定 | §1「无」行 + §9 |
| FR-030 不引入未登记名称 | 只用 `ExamPass` / `FirstClass` / `SecondClass` / `ThirdClass` / `SpecialTribute` |

---

## 9. 明确不包含

- 贿赂的累进金额、被抓概率、惩罚矩阵、连坐、服刑（逻辑轨 ⑥；005 当时只经 `bribeMultiplier` 形参
  预留同入口，占位常量 `ExamPolicy.NeutralBribeMultiplier` 现已被 006 删除、改传实际乘数）。
- 成员成长 / 衰老 / 疾病 / 死亡（逻辑轨 ⑦；将来插在本步**之前**）。
- 成就解锁与绝嗣判定（逻辑轨 ⑧；本特性只产出事件流与计数）。
- 买功名婚姻、婚育、买人口（逻辑轨 ⑦）。
- 界面（主界面 / 下月 / 快进循环 / 调试控制台 / 卡片统计，U1~U5）。
- 存档迁移与向下兼容；官名表；跨 .NET 版本的随机数稳定性。
- 任何新的 `*.sln`、新工程、新层间接缝接口（G-01/G-02/G-05）。
