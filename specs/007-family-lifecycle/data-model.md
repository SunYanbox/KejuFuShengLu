# 阶段 1 设计：家族生命周期的数据模型

**Feature**: `007-family-lifecycle` | **Date**: 2026-10-17 | **Plan**: [plan.md](./plan.md)
**Spec**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

> 本文件是**形状**的权威：新增类型落在哪一层、既有类型的哪个位置长出新成员、哪些不变量被加强。
> 数值一律不在此处定义（真源 = 规格书 §4/§5/§9/§17），只引用 `src/KFL.Rules/Config/` 的成员名。

## 1. 层分布总览

| 层 | 新增 | 变更 | 约束 |
| --- | --- | --- | --- |
| `KFL.Core` | `DeathCause`、`MarriageClass`、`MarriageProposalKind`、`MarriageProposal`、`PendingBirth`、`CoupleKey`、`FamilyLifecycleState` | `StatusTimers` +2 项、`Person` +1 可写属性、`Family` +1 聚合属性、`LedgerCategory` +16 取值、`LedgerCategoryMetadata` +16 登记 | 零依赖；不校验业务语义，只保证「值合法 + 通道唯一」 |
| `KFL.Rules` | `Family/` 目录 15 个文件、`Config/` 5 个新配置类 | `MonthlySettlementEngine`（九步 → 十步、ctor +1 参）、`SettlementResult` +1 必填属性、`GameConfig` +6 转发视图、`Save/TurnRunner` ctor +1 参 | 只依赖 `KFL.Core`；零环境依赖（G-07）；一切随机经 `IRandomService` |
| `KFL.Infrastructure` | `FamilyLifecycleDto`、`PendingBirthDto`、`MarriageProposalDto` | `SaveFileFormat.Version 3 → 4`、`PersonDto 28 → 29`、`FamilyDto 8 → 9`、`StatusTimersDto 4 → 6`、`MessagePackSaveFormat` 常量与 `Skip` 序列、`SaveStateMapper` 两侧、`SaveStateValidator` +2 条 | 只随字段同步，不新增依赖 |

## 2. KFL.Core 的新增与变更

### 2.1 新增枚举

**`src/KFL.Core/Enums/DeathCause.cs`**

```csharp
public enum DeathCause
{
    OldAge = 0,      // 寿终：AgeAt(date) 达到 Lifespan 的当月（⑧-f）
    Illness = 1,     // 病亡：体质归零且患病中（⑧-c）
    Famine = 2,      // 饥馑：严重期 5% 判定命中，或饥馑扣减致体质归零（⑧-e）
    Infancy = 3,     // 夭折：三档夭折判定命中（⑧-f）
    Childbirth = 4,  // 分娩：FR-025 的 5% 命中（⑧-b 登记、⑧-g 结算）
}
```

> **只**出现在当月事实段（`LifecycleSnapshot.Deaths`）。**不落 `Person` 字段、不落账本条目**：
> `LedgerEntry` 的形状是 `(GameDate, PersonId?, LedgerCategory, Money)`（`src/KFL.Core/ValueObjects/LedgerEntry.cs:30`，
> 无 `Note`），故 `MemberDeceased` 事件条目只记「谁在何月去世」。
> 006 的「处死」路径只置 `StatusFlag.Deceased`，**不产生死因**，本枚举不为它补取值（FR-033）。

**`src/KFL.Core/Enums/MarriageClass.cs`**

```csharp
public enum MarriageClass
{
    Commoner = 0,  // 平民：Origin.Farming / Origin.Artisan
    Scholar = 1,   // 士人：Origin.Commerce / Origin.Scholar
    Official = 2,  // 仕族：Family.HasShiStatus == true（优先于 Origin 判定）
}
```

**`src/KFL.Core/Enums/MarriageProposalKind.cs`**

```csharp
public enum MarriageProposalKind
{
    Matchmaker = 0,       // §9.1 随机提亲（⑧-a 第二段）
    ImperialProposal = 1, // §9.2 榜下捉婿（⑧-a 第一段）
}
```

### 2.2 新增值对象

**`src/KFL.Core/ValueObjects/CoupleKey.cs`** —— 夫妇键，用于「本年度已申请生育的夫妇集合」。
`public readonly record struct CoupleKey(PersonId HusbandId, PersonId WifeId);`
有序（夫在前），不校验性别（性别语义属规则层）。

**`src/KFL.Core/ValueObjects/PendingBirth.cs`**

```csharp
public readonly record struct PendingBirth(CoupleKey Couple, GameDate AppliedAt);
```

**`src/KFL.Core/ValueObjects/MatchmakeApplication.cs`** —— 一条**当月已用名额**的托媒申请记录
（FR-013 的「同一成员同一月至多一次」载体，**落持久字段**）。与 `PendingBirth` 同款：只承载「谁、哪个月」，
成败与金额属规则层。

```csharp
public readonly record struct MatchmakeApplication(PersonId MemberId, GameDate AppliedAt);
```

**`src/KFL.Core/ValueObjects/MarriageProposal.cs`** —— 待应答提议的**完整快照**（FR-044 要求
「配偶字段表 + 阶层 + 金额 + 提出年月」在玩家决定前可读）。**不是** `Person`，不入 `Family.Members`。

```csharp
public sealed record MarriageProposal
{
    public required MarriageProposalKind Kind { get; init; }
    public required PersonId OurMemberId { get; init; }   // 我方在册未婚成员
    public required string CandidateName { get; init; }   // 冠家族姓氏后的全名
    public required Gender CandidateGender { get; init; }
    public required int CandidateAge { get; init; }
    public required int TalentAgriculture { get; init; }
    public required int TalentCommerce { get; init; }
    public required int TalentScholar { get; init; }
    public required int TalentCraft { get; init; }
    public required int CandidateStudy { get; init; }
    public required int CandidateHealth { get; init; }
    public required int CandidateLifespan { get; init; }
    public required DegreeLevel CandidateDegree { get; init; }
    public required MarriageClass Class { get; init; }
    public required Money BetrothalGift { get; init; }    // 我方支出（娶入）
    public required Money Dowry { get; init; }            // 我方收入（娶入）
    public required GameDate ProposedAt { get; init; }
    public ImperialPlacement? CandidatePlacement { get; init; }
}
```

> `CandidateLifespan` 只在下界 clamp（`>= AttributeLimits.Min`），无上界（001 `data-model.md:244-248`）。
> 提议**当月未应答即失效**（Assumption 11）——失效不是字段而是查询判据
> （`ProposedAt` 的年月 ≠ 当月 ⇒ `LifecycleQueries` 视为不可应答），故无需额外状态位。

### 2.3 新增聚合 `FamilyLifecycleState`

**`src/KFL.Core/Entities/FamilyLifecycleState.cs`** —— 六组新增状态的唯一载体。

```csharp
public sealed class FamilyLifecycleState
{
    public int PurchaseCount { get; }        // 本年度已买人数（1 月重置 ⇒ 0）
    public int PurchaseYear { get; }         // 该计数所属自然年
    public IReadOnlyList<PendingBirth> PendingBirths { get; }     // 待分娩记录（次月 ⑧-b 消费）
    public IReadOnlyList<CoupleKey> AppliedBirthCouples { get; }  // 本年度已申请生育的夫妇
    public int BirthApplicationYear { get; }
    public MarriageProposal? Proposal { get; }                    // 至多一条待应答提议
    public IReadOnlyList<MatchmakeApplication> MatchmakeApplications { get; }  // 当月已托媒的成员（跨月清理）

    // 写入通道（规则层唯一入口，均内部校验后落值）
    public int NextPurchaseOrdinal(int year);                     // 返回本年度第 n 名（1 起）；跨年自动重置
    public void ScheduleBirth(PendingBirth birth);
    public void ConsumeBirths();                                  // ⑧-b 分娩后清空
    public bool HasAppliedForBirth(CoupleKey couple, int year);
    public void RecordBirthApplication(CoupleKey couple, int year); // 跨年自动清空
    public void SetProposal(MarriageProposal? proposal);           // null = 应答/失效后清空
    public bool HasAppliedForMatchmaking(PersonId member, GameDate month);   // FR-013 判定
    public void RecordMatchmakeApplication(PersonId member, GameDate month); // 扣款成功后记账（幂等）
    public void ConsumeMatchmakeApplications(GameDate month);      // ⑧-a 每月初清理过期记录

    internal (int, int, IReadOnlyList<PendingBirth>, IReadOnlyList<CoupleKey>, int, MarriageProposal?, IReadOnlyList<MatchmakeApplication>) CaptureBookkeeping();
    internal void RestoreBookkeeping(int purchaseCount, int purchaseYear, IEnumerable<PendingBirth> pendingBirths, IEnumerable<CoupleKey> appliedBirthCouples, int birthApplicationYear, MarriageProposal? proposal, IEnumerable<MatchmakeApplication> matchmakeApplications);
}
```

> `CaptureBookkeeping`/`RestoreBookkeeping` 是 **internal**（`KFL.Core/AssemblyInfo.cs:13` 只放行
> `KFL.Infrastructure`），与 `Family.CaptureOutsiderBookkeeping`（`Family.cs:340`）同款；
> 捕获面是**七项元组**（`MarriageProposal?` 不可空展开，故不用快照对象）。
> 两个 `Year` 字段是「重置」的唯一实现手段——MUST NOT 依赖 `GameState.CurrentDate` 反推，
> 否则存档读回时会把旧年度当成新年度。**这两个字段不可省**，它们是 FR-039/FR-022 的跨年重置载体。
> `MatchmakeApplications` 只在**扣款成功**后追加（`LifecycleEntry.TryMatchmake` ④a），
> 跨月由 ⑧-a 入口的 `ConsumeMatchmakeApplications` 清理；判定本身按月精确比对，不依赖清理时机（FR-013）。

### 2.4 `StatusTimers` 的形状变更（4 → 6）

`src/KFL.Core/ValueObjects/StatusTimers.cs`：在**尾部**追加两项（不得插在中间，否则 004 的
`MessagePack` 位置式 `Skip` 序列会错位）。

| 序号 | 成员 | 类型 | 语义 | 落位 |
| --- | --- | --- | --- | --- |
| 0 | `SentenceRemainingMonths` | `int?` | 服刑剩余（006） | 既有 |
| 1 | `ExamBanRemainingMonths` | `int?` | 禁考剩余（006） | 既有 |
| 2 | `PromotionBanRemainingMonths` | `int?` | 禁升剩余（006） | 既有 |
| 3 | `AwaitingPostRemainingMonths` | `int?` | 待阙剩余（003） | 既有 |
| **4** | `IllnessSeverityBasisPoints` | `int?` | 疾病程度 `s`，取值范围 `0~10000`（万分之） | **新** |
| **5** | `TreatmentRemainingMonths` | `int?` | 疗程剩余月数，`0~3` | **新** |

构造函数追加两个可选尾参（默认 `null`），既有 16 处 `new StatusTimers(...)` 调用点因此**不必改签名**，
但 `tests/KFL.Tests/Core/StatusTests.cs` 的建立/清除次序断言要补「新两项与 `Ill` 位同步」用例。
`EnsureNonNegative`（`:58-65`）继续只管四个「剩余月数」；`IllnessSeverityBasisPoints` 的取值范围
由新增的 `EnsureSeverityInRange`（`0~10000`）单独守，抛
`ArgumentOutOfRangeException("疾病程度 MUST 在 0~10000 之间（规格书 §4.3）。")`。

> **宽限月不在这里**：FR-029 ④ 明确「宽限月由子步次序**隐式**表达、MUST NOT 新增字段」。
> `TreatmentRemainingMonths == 0` 与 `IllnessSeverityBasisPoints == null` 的组合即「未患病」，
> 二者 MUST 同生同灭（`Ill` 位置位 ⇔ `IllnessSeverityBasisPoints != null`）。

### 2.5 `Person` 的形状变更（+1 可写属性）

`src/KFL.Core/Entities/Person.cs` 在可写成员区（`Occupation`（`:400`）附近）追加：

```csharp
private bool _tutoredByMaster;
public bool IsTutoredByMaster
{
    get => _tutoredByMaster;
    set => _tutoredByMaster = value;   // 纯布尔，无范围校验
}
internal void RestoreTutoring(bool tutoredByMaster) => _tutoredByMaster = tutoredByMaster;
```

- 可写属性 **10 → 11**；只读成员全集**不变**（仍 19 项）⇒ `tests/KFL.Tests/Core/PersonTests.cs`
  只改 `WritableMembers`（`:41-53`，10 → 11 条）。
- 复原通道必须是 internal（`AssemblyInfo.cs:13` 只放行 `KFL.Infrastructure`）。
- **不新增**任何疾病/死亡/配偶候选字段（FR-029/FR-033）。

### 2.6 `Family` 的形状变更（+1 聚合属性）

`src/KFL.Core/Entities/Family.cs` 在 `AutoExamApplication`（`:55`）附近追加：

```csharp
public FamilyLifecycleState Lifecycle { get; } = new();
```

- **不新增**任何写入方法：买人口/生育/提议全部经 `Lifecycle` 的方法（这是「新增聚合」的收益——
  `Family` 的公开面只多一个属性，不留五个散装 setter）。
- 辈分 MUST NOT 新增写入通道（FR-035）：仍只用 `AddChild`（`:143`）/`AddOutsider`（`:176`）/
  `SetOutsiderGeneration`（`:199`）/`BoughtCollateralGeneration`（`:89-92`）。
- `Family.SetHead`（`:308-328`）**不改签名**；⑧-h 只是新增一个调用者。

### 2.7 `LedgerCategory` 与元数据（22 → 38）

新增 16 个取值（在 `Fine = 88` 之后按「支出 → 收入 → 事件」分组追加）：

| 组 | 取值 | `KindOf` | `CreditTargetOf` |
| --- | --- | --- | --- |
| 支出 8 | `MatchmakerFee`、`TutorFee`、`BetrothalGiftPaid`、`DowryPaid`、`DegreeMarriageFeePaid`、`ChildbirthExpense`、`MedicineExpense`、`PersonPurchase` | `Treasury` | `null`（恒为负） |
| 收入 3 | `BetrothalGiftReceived`、`DowryReceived`、`DegreeMarriageDowryReceived` | `Treasury` | `Cash` |
| 事件 5 | `ChildBorn`、`MemberDeceased`、`MarriageFormed`、`MemberMarriedOut`、`PersonBought` | `Event` | `null` |

分布变化：`Treasury` 17 → 28、`Event` 5 → 10；`Cash` 9 → 12、`Savings` 1（不变）、
`null` 12 → 25（15 个恒负支出 + 10 个事件）。`LedgerCategoryMetadata.KindOf`（`:24-54`）与
`CreditTargetOf`（`:62-96`）的 `default:` 文案里的「共 22 个取值」同步改 38。

## 3. KFL.Rules 新增

### 3.1 `Family/PopulationAdvance.cs` —— ⑧ 的唯一编排入口

```csharp
public static class PopulationAdvance
{
    public static PopulationAdvanceResult Run(GameState state, IRandomService random, INameGenerator names);
}
```

固定八个子步，**MUST NOT 增删或穿插**（FR-007）。每个子步都是 `internal static` 的独立文件，
`Run` 只做次序与当月死亡登记表的传递：

| 子步 | 实现文件 | 读 | 写 | 随机消费（每次 = 1 次 `NextDouble`，除注明） |
| --- | --- | --- | --- | --- |
| ⑧-a | `MarriageMarket.cs` | 待应答提议、上月考季及第名单 | `Lifecycle.Proposal` | 捉婿先、提亲后（R-25）；两者都掷，后者不覆盖前者 |
| ⑧-b | `Childbirth.cs` | `Lifecycle.PendingBirths` | `Family.AddChild`、母婴状态 | 每对：分娩死亡 1 次 → 新生儿性别 1 次 `Next` → 天赋 ×4 → 学业 → 体质 → 寿数 → 姓名 |
| ⑧-c | `IllnessAdvance.cs` | 在册成员 `Status`/`Timers` | `Status`/`Timers`/`Health` | 旧病：清除 1 次（`min(1,…)` 命中时 0 次）；新感染：感染 1 次 → 命中则程度 1 次 |
| ⑧-d | `GrowthAdvance.cs` | `Occupation`/`IsTutoredByMaster`/`Origin` | `Study`/`Health` | **0 次** |
| ⑧-e | `FamineHealth.cs` | `Economy.Famine` 阶段 | `Health`、死亡登记表 | 严重期每人 1 次（5% 判定） |
| ⑧-f | `LifespanAdvance.cs` | `Lifespan`/`BirthDate`（含外嫁） | 死亡登记表 | 每人 1 次（夭折 10%）——`AgeAt >= 3` 者不消费 |
| ⑧-g | `DeathSettlement.cs` | 死亡登记表 | `Status`/`ArchivedMembers`/账本 | **0 次** |
| ⑧-h | `HeadSuccession.cs` | 在册男性 | `Family.SetHead` | **0 次** |

`PopulationAdvanceResult`（`internal sealed record`）→ 展开为 `SettlementResult.Lifecycle` 的子字段。

### 3.2 `Family/LifecycleSnapshot.cs` —— 当月事实段（`SettlementResult` 的子段）

```csharp
public sealed record LifecycleSnapshot
{
    public required IReadOnlyList<BirthFact> Births { get; init; }
    public required IReadOnlyList<DeathFact> Deaths { get; init; }
    public required IReadOnlyList<MarriageFact> Marriages { get; init; }
    public required IReadOnlyList<MarriedOutFact> MarriedOut { get; init; }
    public required IReadOnlyList<PurchaseFact> Purchases { get; init; }
    public required IReadOnlyList<IllnessFact> Illnesses { get; init; }
    public required IReadOnlyList<AgeBracketCrossFact> Crossings { get; init; }
    public required MarriageProposal? PendingProposal { get; init; }   // 当月产出的待应答提议
    public required PersonId? NewHeadId { get; init; }                 // ⑧-h 的结果；未变动为 null
    public required bool HeadBecameVacant { get; init; }               // 无在册男性 ⇒ true（≠ 绝嗣）
}
```

子记录（均 `public sealed record`，位置参数式，与 `LivingCostLine` 风格一致）：
`BirthFact(PersonId, Gender, PersonId FatherId, PersonId MotherId)`、
`DeathFact(PersonId, DeathCause)`、`MarriageFact(PersonId, PersonId SpouseId, MarriageClass, bool IsDegreeMarriage, Money BetrothalGift, Money Dowry)`、
`MarriedOutFact(PersonId, MarriageClass)`、`PurchaseFact(PersonId, int Ordinal, Money Amount)`、
`IllnessFact(PersonId, bool Contracted, bool Recovered)`、
`AgeBracketCrossFact(PersonId, AgeBracket From, AgeBracket To)`。

> **008 消费注（2026-10-18，逻辑轨 ⑧）**：成就面把本快照当作「当月报告」**只读**消费——
> ① `Births` 的**条数**累加进 `AchievementState.BirthCount`（**含**当月出生即夭折者：出生与死亡是两件独立事实）；
> ② 逐条 `Deaths` 取「该成员在**事实月**的周岁」（`Person.AgeAt(snapshot.Month)`）追加进享年序列，
> 并用 `Cause` 判定「红颜薄命（Childbirth）」「襁褓之殇（事实月年龄 < 3）」「路有冻死骨（Famine）」；
> ③ `Marriages` 中 `IsDegreeMarriage == true` 触发「秦晋之好」——**⚠ 但本特性（007）实际从不填 `Marriages`**：
> `PopulationAdvance` 只 `with` 了 `PendingProposal` / `Births` / `Illnesses` / `Crossings` / `Deaths`，
> `Marriages` 与 `Purchases` **恒为空快照成员**。故 ⑧ 按 FR-008 补了**状态面兜底**（家族台账出现
> `DegreeMarriageFeePaid` / `DegreeMarriageDowryReceived` 即算达成，见 008 勘误 **E-24**）；
> **恢复「当月面」严格口径**须由 007 在 ⑧-a 补记婚配事实（属 007 的形状变更，登记为 008 的待接线候选）；
> ④ `PendingProposal?.Kind == ImperialProposal` 触发「榜下捉婿」（**含被拒**：提议一产出即算见过；
> 当月状态侧还未应答的那条经 `LifecycleQueries.PendingProposal` 同口径读取，故**不需要**新增持久字段）；
> ⑤ `MarriedOut` / `Crossings` / `Illnesses` / `Purchases` / `NewHeadId` / `HeadBecameVacant` 与成就面**无关**。
> 本消费 MUST NOT 回写快照或生命周期的任何状态（快照是「增量只读投影」，001/002 既有口径不变），
> 也 MUST NOT 让 ⑧ 去调用结算或成就判定（FR-035：单月只结算一次；判定只发生在 `TurnRunner.Advance` 的 ② / 动作返回之前）。
> 契约真源见 [008 `achievement-catalog.md` §2/§3](../../008-achievements-extinction/contracts/achievement-catalog.md)。

### 3.3 `Family/LifecycleEntry.cs` —— 玩家动作（可拒绝）

```csharp
public static class LifecycleEntry
{
    public static LifecycleOutcome TryMatchmake(GameState state, Person member, MarriageDirection direction, IRandomService random);
    public static LifecycleOutcome ApplyChildbirth(GameState state, CoupleKey couple);
    public static LifecycleOutcome BuyPerson(GameState state, IRandomService random, INameGenerator names);
    public static LifecycleOutcome RespondToProposal(GameState state, bool accept, IRandomService random, INameGenerator names);
    public static LifecycleOutcome StartTreatment(GameState state, Person patient);
}
```

固定次序（照 `BribeEntry.Bribe`（`src/KFL.Rules/Bribery/BribeEntry.cs:41`）的七段式）：
①校验（纯，失败即返回 `Accepted = false` + 原因，**随机消费 0 次**）→ ②算金额（读配置）→
③`PaymentPrimitive.CanCover` → ④支付（`PaymentPrimitive.Pay`，恒不转贷款）→ ⑤掷随机 →
⑥写状态/账本 → ⑦返回结果。

**托媒的当月名额（2026-10-17 追加，FR-013 / T093）**：`TryMatchmake` 在 ①校验之后插入 **①a**：
`Family.Lifecycle.HasAppliedForMatchmaking(member.Id, month)` 命中 ⇒ 立即
`Rejected(LifecycleRejectReason.AlreadyAppliedThisMonth)`（0 次随机、0 次支付、状态零变更，下月可再试）；
支付成功（④）之后插入 **④a**：`RecordMatchmakeApplication(member.Id, month)`。
成功判定失败（⑤未命中）**也**占用名额——「同一成员同一月至多一次」对成败一视同仁（Assumption 1），
故名额记录只在**扣款成功后**落，付款被拒（②/③失败）时不占名额。同款判据经
`LifecycleQueries.MatchmakeFeasibility` 暴露给灰置按钮（FR-045）。

**婚嫁金额的支付能力校验（2026-10-17 追加，规格书 §17 裁决 Q7 / FR-052）**：
`TryMatchmake` 在 ⑧「婚嫁金额」算出后追加**⑧a**：`CanCover`（娶入 = 对方阶层聘礼、嫁出 = 本家阶层嫁妆）
不通过 ⇒ 整单失败（`InsufficientFunds`、不成婚、无婚嫁条目、状态零变更、**已付费用不退**、不转贷款）；
`RespondToProposal` 的接受路径在支付买功名费之后、`SettleAmounts` 之前做同款校验，
失败时**清空提议**（名额已消耗）。`SettleAmounts` 因此恒不产生贷款。

### 3.4 `Family/LifecycleQueries.cs` —— 纯查询面（FR-044/FR-045）

```csharp
public static class LifecycleQueries
{
    public static IReadOnlyList<Person> MarriageCandidateMembers(Family family, GameDate date);
    public static MarriageFeasibility MatchmakeFeasibility(GameState state, Person member, MarriageDirection direction);
    public static MarriageClass ClassOf(Family family, Person person);
    public static bool CanApplyForChildbirth(Family family, CoupleKey couple, GameDate date);
    public static Money ChildbirthCost(GameState state);
    public static PurchaseQuote PurchaseQuote(GameState state);          // 含 HeadVacant
    public static IllnessStatus IllnessOf(Person person);               // 剩余月数 + 病程
    public static int GenerationOf(Person person);
    public static IReadOnlyList<Person> SuccessionCandidates(Family family);
    public static MarriageProposal? PendingProposal(Family family, GameDate date);  // 过期视为 null
    public static SettlementResult? ...  // 不提供：事实段只经 Settle 返回
}
```

`MatchmakeFeasibility.RejectReason` 覆盖 `NotMarriageable` 与 `AlreadyAppliedThisMonth`（FR-013 的
当月名额），`Available` 恒等于「无拒因」；`ChildbirthFeasibility` / `PurchaseQuote` 同款。

全部 **MUST NOT** 写状态、消费随机、建视图（R-20 已登记纯度断言用例）。

### 3.5 `Family/LifecycleOutcome.cs` + 拒绝原因

```csharp
public enum LifecycleRejectReason
{
    None = 0, NotMarriageable, InsufficientFunds, NoLowerDegree,
    FertilityWindowClosed, AlreadyAppliedThisYear, HeadVacant,
    NoPendingProposal, NotHead, NoRegisteredMale, AlreadyAppliedThisMonth,
}
```

`LifecycleRejectReason.None = 0` 与 006 的 `BribeRejectReason`（`BribeOutcome.cs:12-33`）同风格；
`InsufficientFunds` 复用同名（不同枚举，不跨枚举复用值）。

`LifecycleOutcome` 为 `public sealed record`：`required bool Accepted`、`required LifecycleRejectReason RejectReason`、
`required Money Amount`、`required Money FromCash`、`required Money FromSavings`、
`required LedgerEntry? Entry`（被拒时 `null`）、`public MarriageProposal? Proposal { get; init; }`。

### 3.6 其余执行器与工具

| 文件 | 职责 | 关键签名 |
| --- | --- | --- |
| `MarriageMarket.cs` | ⑧-a：捉婿 → 提亲 | `internal static MatchmakerResult Run(GameState, IRandomService, INameGenerator)` |
| `Childbirth.cs` | ⑧-b：分娩 + 新生儿 + 遗传 | `internal static IReadOnlyList<BirthFact> Run(GameState, IRandomService, INameGenerator, DeathRegister)` |
| `Inheritance.cs` | 遗传公式 | `internal static int Attribute(int father, int mother, IRandomService random)` / `internal static int Study(int f, int m, IRandomService random)` |
| `IllnessAdvance.cs` | ⑧-c | `internal static IReadOnlyList<IllnessFact> Run(GameState, IRandomService, DeathRegister)` |
| `GrowthAdvance.cs` | ⑧-d | `internal static void Run(GameState, GameDate)` |
| `FamineHealth.cs` | ⑧-e | `internal static void Run(GameState, IRandomService, DeathRegister)` |
| `LifespanAdvance.cs` | ⑧-f | `internal static void Run(GameState, GameDate, IRandomService, DeathRegister)` |
| `DeathSettlement.cs` | ⑧-g | `internal static IReadOnlyList<DeathFact> Run(GameState, DeathRegister, GameDate)` |
| `HeadSuccession.cs` | ⑧-h + 查询共用 | `public static IReadOnlyList<Person> Candidates(Family)` / `internal static PersonId? Select(IReadOnlyList<Person>)` |
| `OfficialPressure.cs` | `P = Σ w(L)` | `public static decimal Compute(Family)` / `internal static decimal WeightOf(int level)` |
| `DeathRegister.cs` | 当月死亡登记表 | `internal sealed class DeathRegister`：`Add(Person, DeathCause)`、`IReadOnlyList<(Person, DeathCause)> Ordered`（按 `PersonId` 升序） |

### 3.7 配置类（5 个新文件 + `GameConfig` 6 个转发视图）

| 文件 | 承载数值（真源 §） |
| --- | --- |
| `Config/GrowthPolicy.cs` | 读书 +1.5/月、名师 +2.25/月 与 5 贯/月、士出身 +10%、遗忘 −0.5/月、遗忘门槛 16、体质 +1/月、上限 100（§4.3） |
| `Config/IllnessPolicy.cs` | 概率锚点 0.25%/40%、插值分母 99、体质 clamp 1~100、四组 `m_p`/`m_q`、`2+8s`、`0.05+0.45s`、基础痊愈 `×1.00`、疗程式 `×1.10 + 0.20`、疗程 3 月、免病年龄 3、年龄增益 0.60/0.30/0 与边界 3/12/24（§4.3 + Q2/Q4/Q5/Q6） |
| `Config/MarriagePolicy.cs` | 托媒 5 贯 / 50%、对象年龄差 5、提亲 base 5%、捉婿 base 30%、捉婿金额 500~1000、`w(L)` 锚点 0.02/1.50 与边界 1/18、功名分档阈值 1/3、阶层加权三档（100/1000 贯；70/25/5、45/40/15、20/50/30）、婚嫁金额表（10/20、20/30、50/100）、买功名 150%（§9.1/§9.2 + Q1 + Q5） |
| `Config/ChildbirthPolicy.cs` | 生育 10 贯、女方 16~45、每年一次、分娩死亡 5%/2.5/7、遗传 σ 常数 5 与学业折半 0.5、体质下限 20、夭折 10%/5/12 与 3 次（§4.2/§9.4 + Q3） |
| `Config/PopulationPurchasePolicy.cs` | 递增基数 10 贯、年龄 8~16、性别 50/50（§9.5） |

`GameConfig` 新增六个嵌套 `static class`（`Growth` / `Illness` / `Marriage` / `Childbirth` / `Purchase` / `Lifecycle`），
**全部纯转发、零字面量**（SC-008），并修正 `:24-27` 的「明确不含」注释（E-03）。

## 4. 既有类型的形状变更（汇总表）

| 类型 | 位置 | 变更 | 波及的守卫/测试 |
| --- | --- | --- | --- |
| `StatusTimers` | `KFL.Core/ValueObjects/StatusTimers.cs:21-65` | +2 项（尾插） | `StatusTests.cs`（16 处构造）、`StatusTimersDto` 4 → 6 |
| `Person` | `KFL.Core/Entities/Person.cs:400` 附近 | +1 可写属性 | `PersonTests.cs` 可写清单 10 → 11 |
| `Family` | `KFL.Core/Entities/Family.cs:55` 附近 | +1 聚合属性 | `FamilyDto` 8 → 9 |
| `LedgerCategory` | `KFL.Core/Enums/LedgerCategory.cs:22-88` | +16 取值 | `LedgerInvariantTests.cs` 四处清单 |
| `LedgerCategoryMetadata` | `KFL.Core/Enums/LedgerCategoryMetadata.cs:24-96` | +16 登记、`default:` 文案改 38 | 同上 |
| `MonthlySettlementEngine` | `KFL.Rules/Settlement/MonthlySettlementEngine.cs` | 九步 → 十步；`:22-32` 步骤清单、`:34-35` MUST NOT 段、`:37-42` 随机次序注释、ctor `:63` +1 参、`Settle` 内插 ⑧ | `SettlementEngineTests.cs:305-345`、`DeterminismTests.cs` |
| `SettlementResult` | `KFL.Rules/Settlement/SettlementResult.cs:48-120` | +1 `required` 属性 `Lifecycle`（20 → 21） | `SettlementEngineTests.cs:315-337` |
| `GameConfig` | `KFL.Rules/Config/GameConfig.cs` | +6 转发视图、修正 `:24-27` | `ConfigLiteralTests.cs` |
| `TurnRunner` | `KFL.Rules/Save/TurnRunner.cs:54` | ctor +1 参 `INameGenerator` | 组合根 `KFL.App` |
| `SaveFileFormat` | `KFL.Infrastructure/Save/SaveFileFormat.cs:31` | `Version = 3` → `4` | 004 契约、`SaveFormatContractTests` |

## 5. 状态转移

| 转移 | 触发 | 载体变化 | 子步 |
| --- | --- | --- | --- |
| 健康 → 患病 | 感染判定命中（`AgeAt >= 3`） | `Status \|= Ill`、`Timers.IllnessSeverityBasisPoints = s` | ⑧-c（第二段） |
| 患病 → 健康 | 清除判定命中，或疗程概率 `>= 1` 短路 | `Status &= ~Ill`、`Timers` 两项置 `null` | ⑧-c（第一段） |
| 健康 → 患病（疗程） | `LifecycleEntry.StartTreatment` | `Timers.TreatmentRemainingMonths = 3`，`Ill` 保持 | 玩家动作 |
| 疗程推进 | 每月扣药费 | `TreatmentRemainingMonths--`，归零后仍患则停付 | ⑧-c（第一段） |
| 在册 → 已亡 | 寿终 / 病亡 / 饥馑 / 夭折 / 分娩 | `Status \|= Deceased`、进 `ArchivedMembers`、落 `MemberDeceased` | ⑧-g |
| 在册 → 外嫁 | 嫁出成婚 | `Status \|= MarriedOut`、进 `ArchivedMembers`、落 `MemberMarriedOut` | `LifecycleEntry`（动作） |
| `HeadId` → 他人 | 现家主已亡或被嫁出 | `Family.SetHead` | ⑧-h |
| `HeadId` → `null` | 无在册男性 | `SetHead(null)` | ⑧-h |

**不实现**的状态转移：绝嗣判定（⑧）、患病 → 死亡的直接转移（必须先体质归零）、婚姻 → 离婚（规格书未定义）。

## 6. 不变量清单

1. `Status.HasFlag(Ill)` ⇔ `Timers.IllnessSeverityBasisPoints != null`（同生同灭）。
2. `Timers.TreatmentRemainingMonths` 为 `null` 表示未在疗程中；非 `null` 时 `0 <= 值 <= 疗程月数`，
   且**只**在 `Ill` 置位时存在。
3. `Timers.IllnessSeverityBasisPoints` 落在 `0~10000`（`illness s = 磅值 / 10000`）。
4. `Study`/`Health` 永远落在 `0~100`：**写入方先 clamp**（setter 抛异常，不静默）。
5. `Family.Lifecycle.Proposal` 至多一条，且其 `OurMemberId` 必为在册、非已亡、非外嫁、未婚者。
6. `Family.Lifecycle.PendingBirths` 的每个 `Couple` 必在 `AppliedBirthCouples` 中，且
   `PendingBirths.Count <= AppliedBirthCouples.Count`。
7. `PurchaseYear` 与 `BirthApplicationYear` 单调不减，且 `>= 1`（开局 `1`）；当
   `PurchaseYear != 当年` ⇒ `PurchaseCount == 0`（**读时**判定，重置发生在 `NextPurchaseOrdinal` 内）。
8. `HeadId` 为 `null` 或指向 `RegisteredMembers`（001 不变量 6 加强：⑧-h 在死亡/外嫁后**当月经当日**
   修正）。
9. 一切成婚必经 `Family.Marry`（`:245`），一切丧偶必经 `Family.EndMarriage`（`:280`）。
10. 辈分只经 `AddChild`/`AddOutsider`/`SetOutsiderGeneration` 变化；外来者总共只变动两次。
11. 死亡登记表内的条目在 ⑧-g 之前**不**改变任何 `Person.Status`（子步隔离）。
12. `DeathCause` 不出现在 `Person` 与 `LedgerEntry` 上（只在事实段）。
13. 无在册男性 ⇒ `HeadId == null`，但**不**产生任何「绝嗣」状态（⑧ 的职责）。
14. `Family.Lifecycle.MatchmakeApplications` 内同一 `(MemberId, AppliedAt)` **至多一条**
    （`RecordMatchmakeApplication` 幂等），且只在扣款成功后出现；同月同成员的第二次托媒必被
    `AlreadyAppliedThisMonth` 拒绝（FR-013）。

## 7. 持久化形状与版本

`SaveFileFormat.Version` **3 → 4**（`src/KFL.Infrastructure/Save/SaveFileFormat.cs:31`）。无迁移路径
（004 `spec.md:306-307`）；`!= 4` 一律 `UnsupportedVersion`（`MessagePackSaveFormat.EnsureVersion`，
`src/KFL.Infrastructure/Save/Serialization/MessagePackSaveFormat.cs:423-430`；JSON 侧同款在
`JsonSaveFormat.cs:393-401`）。

| DTO | 现状 | 变更 |
| --- | --- | --- |
| `PersonDto`（`SaveStateDto.cs:143-316`） | 28 键 | **29 键**：尾部 `[Key(28)] bool isTutoredByMaster` |
| `FamilyDto`（`SaveStateDto.cs:83-133`） | 8 键 | **9 键**：尾部 `[Key(8)] FamilyLifecycleDto lifecycle` |
| `StatusTimersDto`（`SaveStateValueDtos.cs:258-284`） | 4 键 | **6 键**：尾部 `[Key(4)] int? illnessSeverityBasisPoints`、`[Key(5)] int? treatmentRemainingMonths` |
| `FamilyLifecycleDto` | — | **新**，7 键：`[Key(0)] int purchaseCount`、`[Key(1)] int purchaseYear`、`[Key(2)] IReadOnlyList<PendingBirthDto> pendingBirths`、`[Key(3)] IReadOnlyList<CoupleKeyDto> appliedBirthCouples`、`[Key(4)] int birthApplicationYear`、`[Key(5)] MarriageProposalDto? proposal`、`[Key(6)] List<MatchmakeApplicationDto> matchmakeApplications`（FR-013 的当月名额，T093 追加） |
| `PendingBirthDto` | — | **新**，4 键：`husbandId`、`wifeId`、`appliedYear`、`appliedMonth` |
| `MatchmakeApplicationDto` | — | **新**，3 键：`memberId`、`appliedYear`、`appliedMonth`（T093 追加） |
| `CoupleKeyDto` | — | **新**，2 键：`husbandId`、`wifeId` |
| `MarriageProposalDto` | — | **新**，19 键：与 `MarriageProposal` 的 18 个成员逐一对应（`ProposedAt` 拆成 `proposedYear` + `proposedMonth`，故 18 → 19 键） |

`MessagePackSaveFormat` 同步：`FamilyMemberCount 8 → 9`（`:47`）、`PersonMemberCount 28 → 29`（`:50`）、
`StatusTimersMemberCount 4 → 6`（`:56`）、新增 `FamilyLifecycleMemberCount = 7`（T093 由 6 追加）
/`PendingBirthMemberCount = 4`/`MatchmakeApplicationMemberCount = 3`/`CoupleKeyMemberCount = 2`
/`MarriageProposalMemberCount = 19`；`Skip` 序列在 `:237`（Family 0-2 之后）插 `memberCount` 消费后的
lifecycle 分支、在 `:309`（Person 22-27 之后）插 `Skip(1)`（`isTutoredByMaster`）、在 `:295` 把
`Skip(4)` 改 `Skip(6)`。`EnsureFamilyShape`（`:234`）/`EnsurePersonShape`（`:284`）/`EnsureLifecycleShape` 逐一校验键数。

`SaveStateMapper`：`ToDto`（`:38`）补三个新段；`Build`（`:100`）在 `:222`（`RestoreOutsiderBookkeeping`）
之后追加 `family.Lifecycle.RestoreBookkeeping(...)`（**七参**，末参为托媒申请）、在成员标量段
（`:149-158` 附近）追加 `person.RestoreTutoring(...)`。

`SaveStateValidator` +3 条（第 13~15 条）并对既有 2 条加严：
- 第 4 条 `EnsureHeadIsRegistered` 追加：**有在册男性而 `HeadId == null` ⇒ 拒**（不变量 13）。
- 第 5 条 `EnsureTimersBackedByStatus` 由 4 路扩到 6 路：`Ill` 位 ⇔ `IllnessSeverityBasisPoints != null`
  （不变量 1）、`TreatmentRemainingMonths` 仅在 `Ill` 置位时非空（不变量 2）。
- 第 13 条 `EnsureLifecycleProposalReferencesMember`：`Proposal.OurMemberId` 必须在册。
- 第 14 条 `EnsurePendingBirthsApplied`：不变量 6，并追加「夫妇双方 `SpouseId` 互指」。
- 第 15 条 `EnsureLifecycleCountersLegal`：`PurchaseCount >= 0`、`PurchaseYear`/`BirthApplicationYear`
  落在 `1..当年`，且当年 `PersonPurchase` 条目数与本年度 `PurchaseCount` 一致、同年度单价严格递增
  （不变量 7 的可检部分；`KFL.Infrastructure` 不能引用 `KFL.Rules`，故不直接比对配置基准价）。

## 8. 需求 → 模型映射

| FR | 落点 |
| --- | --- |
| FR-001~FR-005 | 5 个 `Config/*Policy.cs` + `GameConfig` 六视图 + `Inheritance`/`IllnessPolicy` 复用 `AttributePolicy.NextNormal` |
| FR-006~FR-010 | `MonthlySettlementEngine` 十步、`PopulationAdvance` 八子步、`LifecycleSnapshot` |
| FR-011 | `SettlementResult.Lifecycle`（21 个属性） |
| FR-012~FR-021 | `MarriageMarket`、`LifecycleEntry.TryMatchmake`、`MarriageClass`、`MarriageProposal` |
| FR-022~FR-026 | `LifecycleEntry.ApplyChildbirth`、`Childbirth`、`Inheritance`、`PendingBirth`、`CoupleKey` |
| FR-027~FR-030 | `GrowthAdvance`、`IllnessAdvance`、`IllnessPolicy`、`Person.IsTutoredByMaster`、`StatusTimers` +2 |
| FR-031 | `FamineHealth`（只读 002 的 `Economy.Famine`，不改其状态机） |
| FR-032~FR-034 | `LifespanAdvance`、`DeathSettlement`、`DeathRegister`、`DeathCause` |
| FR-035~FR-038 | `HeadSuccession`、`Family.SetHead`、`FamilyLifecycleState`（无辈分通道） |
| FR-039~FR-041 | `PopulationPurchasePolicy`、`FamilyLifecycleState.NextPurchaseOrdinal`、`LifecycleEntry.BuyPerson` |
| FR-042~FR-043 | `LedgerCategory` +16、`LedgerCategoryMetadata` +16 |
| FR-044~FR-045 | `LifecycleQueries`、`LifecycleSnapshot`（快进暂停信号 = 事实段 + `PendingProposal`） |
| FR-046 | 见 §9 |
| FR-047 | §7 的四个 DTO + 六个成员数常量 |
| FR-048 | 全部随机经 `IRandomService`；`IllnessSeverityBasisPoints` 为整数 |
| FR-049 | `tests/KFL.Tests/Rules/` 八个新测试文件 |
| FR-050 | `plan.md` 的「回写义务清单」10 项 |

## 9. 明确不包含

- **成就解锁、绝嗣判定、结局画面、统计页、族谱绘制**（⑧ 与 U2 的职责，FR-046）。
- **家族声望属性**（Q1 明确 MUST NOT 新增；阶层只按总资产三档）。
- **怀孕率 / 抗病属性 / 预设病程**（FR-030、FR-029 ③）。
- **随机事件总表**：只实现 §9.1 提亲与 §9.2 榜下捉婿；灾年米价跳涨、经商亏损不在此。
- **继承与财产分割、诉讼、子女抚养、多家族与姻亲网络**。
- **存档格式迁移**（旧版本一律拒绝）。
- **`IEventBus` / `IAchievementStore`**：`specs/001-core-skeleton/contracts/injection-seams.md:91-99`
  至今未定义，本特性 MUST NOT 自行引入（FR-045）。
