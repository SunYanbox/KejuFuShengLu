# 阶段 0 研究：贿赂全链路（逻辑轨 ⑥）的技术抉择

**Feature**: `006-bribery-punishment` | **Date**: 2026-10-13 | **Plan**: [plan.md](./plan.md) | **Spec**: [spec.md](./spec.md)

> 前置：`spec.md` 的三项裁决（Q1 乘数指数含被抓的那些 / Q2 惩罚推进＝结算新第③步 / Q3 全族禁考禁升
> 逐人计时，用户 2026-10-12 全部取 A/B 档）已按规格书 §17 回写规格书 §7.1 / §7.4 / §7.5。本文件只解决
> **设计选择**，**不改任何数值**：全部数值逐字取自规格书 §7.1 / §7.2 / §7.3 / §7.4 / §7.5 / §11，登记见
> [契约十七](./contracts/config-registry.md)。

层与代号：`KFL.Core` = 领域层，`KFL.Infrastructure` = 存储层，`KFL.Rules` = 规则层。
「契约N」指 `specs/*/contracts/` 下的既有契约文号（契约一/二 = 001，契约三/四/五 = 002，
契约六/七/八 = 003，契约九/十/十一 = 004，契约十二/十三/十四 = 005，契约十五/十六/十七 = 本特性）。

---

## R-01 行贿行动的落点与形状

**Decision**: 新建 `KFL.Rules/Bribery/BribeEntry.cs`（静态类）：

```csharp
public static BribeOutcome Bribe(GameState state, Person examinee, ExamLevel level, IRandomService random);
public static void ResetBribeCounts(Person examinee, ExamLevel? level);   // FR-031（调试入口，普通公开入口）
```

`Bribe` 的固定次序（契约十五 §2）：① 校验（窗口 / 级别与当前功名匹配 / 在册 / 未触顶）→ ② 自算应付
金额与被抓概率 → ③ `PaymentPrimitive.Pay` 扣款并落 `BribeExpense` 条目 → ④ `random.NextDouble()`
**恰好 1 次**判定被抓 → ⑤ 该级别计数 +1 并推进乘数（**无论是否被抓**）→ ⑥ 记 `BribeFact` 入
`PunishmentJournal` → ⑦ 被抓则经 `PunishmentImposer.Apply` 施加整格惩罚（含连坐），并把后果记入同一
日志。

**Rationale**: 行贿是**结算之外**的玩家动作（FR-003、FR-007 的「当刻」），与 005 的「报名」同类，故
方法与 `ExamCalendar` / `ExamRegistration` 同处「结算外」的可断言面。①在②之前，使被拒绝的调用
**不消费随机、不改动任何状态**（FR-003/FR-007/FR-010 的共同判据）；③在④之前，使「扣款与掷骰同刻」
与「落榜不退、被抓不退」由次序本身成立（FR-007、FR-009）。

**Alternatives considered**: ① 把行贿做成结算的一步——被否：FR-003 明写窗口在**考试当月及前 1 个月**、
动作发生在两次推进之间，做成结算步会让「错过窗口」不可表达，且会破坏契约三 §4 的整月随机次序；
② 让 `Bribe` 自己判断调用方是否调试——被否：章程原则 VI 禁止规则层按调用方分支。

---

## R-02 三个贿赂计数的形状与写入通道

**Decision**: `Person` 新增三个**只读属性**（`private set`）：

```csharp
public int JieShiBribeCount { get; private set; }     // >= 0
public int ShengShiBribeCount { get; private set; }   // >= 0
public int DianShiBribeCount { get; private set; }    // >= 0
```

公开写入通道只有一个受控方法 + 一个调试重置：

```csharp
public void RecordBribe(ExamLevel level);   // 对应计数 +1（三级皆合法，无上限）
public void ResetBribeCounts(ExamLevel? level);   // level = null ⇒ 三级全清；否则只清该级别（FR-031）
```

复原走 `internal void RestoreBribeCounts(int jieShi, int shengShi, int dianShi)`——`KFL.Core/AssemblyInfo.cs`
已开的**唯一**程序集面 `InternalsVisibleTo("KFL.Infrastructure")` 即为此用（004 R-03、005 R-03 的同款通道）。

**Rationale**: FR-004 明令计数维度是「同一考生 × 同一考试级别、跨科年累计」且 MUST 跨存档保留；
001 不变量 2 的口径是「有写入通道的成员必须把通道钉在具体语义上」（`AppendDegree`、`ApplyForExam`
是范本）。`private set` 让 `PropertyInfo.GetSetMethod()` 返回 `null`，与 001 既有的「无公开 setter」
反射断言同形，测试只需把三个新成员加进 `PersonTests.MembersWithoutWriteChannel`（16 → 19）。

**Alternatives considered**: ① 三个公开 `set;`——被否：违反 001 不变量 2；② 把三项合成一个值对象
（仿 `StatusTimers`）——被否：`StatusTimers` 之所以是值对象，是因为四个计时与状态位**成对**同生同灭、
跨字段才是它的不变量；此处三者的耦合只到「都是非负计数」一层（章程「复杂度 MUST 被论证」）；
③ 存在 `Family` 上（家族级）——被否：FR-004 明令**换考生重置**，家族级无法表达。

---

## R-03 行贿窗口的派生与校验落点

**Decision**: 窗口判定放在 `BribeRules`，复用 005 的 `ExamCalendar`：

```csharp
public static bool IsWithinWindow(GameDate date, ExamLevel level);
// 窗口 = 该级别的考试当月及前 1 个月：
//   解试 ⇒ (keYear, 8) 与 (keYear, 7)；省试 ⇒ (keYear+1, 3) 与 (keYear+1, 2)；
//   殿试 ⇒ (keYear+1, 4) 与 (keYear+1, 3)（与省试月重叠，规格书 §7.1 的「前 1 个月」字面口径）
```

窗口是**纯派生值，MUST NOT 持久化**；`BribeRules.WindowOf(ExamLevel, GameDate)` 给出该级别当月的
窗口起点，供 U2 面板显示「窗口还差几个月」。

**Rationale**: 「三年一科」与「考试当月及前 1 个月」都是玩法规则，不是通用时间算术（002 R-09 的同一
口径）；放在规则层才能被单测逐月钉住并被 U2/U4 共用。殿试窗口与省试月重叠是规格书的**字面**结果，
spec 的 Edge Cases 已确认「玩家可以在尚未知道省试结果时就行贿殿试；若省试落第，该次殿试行贿照扣不退」。

**Alternatives considered**: ① 做成 `GameDate` 的实例方法——被否：`GameDate` 是 001 的通用时间值对象，
把 `mod 3` 混进去会让「日期」这个抽象同时承载玩法；② 只在界面层判断——被否：规则层必须有拒绝能力
（FR-003 要求窗口外调用**拒绝且状态不变**），界面判断会形成第二真源。

---

## R-04 应付金额与被抓概率的纯函数形状

**Decision**: `BribeRules` 提供两个**纯函数**（不改状态、不消费随机）：

```csharp
public static Money PayableAmountGuan(ExamLevel level, int nth, Difficulty difficulty);
public static decimal CaughtProbability(ExamLevel level, int nth, Difficulty difficulty);
public static int NthOf(Person examinee, ExamLevel level);   // = 该级别现计数 + 1
```

金额 = `基础金额(level) × (1 + ProgressionStep × (nth − 1)) × ExpenseFactor(difficulty)`；
概率 = `min(基础率(level) × (1 + ProgressionStep × (nth − 1)) × BriberyRiskFactor(difficulty), 1.0)`。

**Rationale**: FR-005/FR-006 要求金额与概率**可经查询面读出**（FR-029 ①），且都被单测逐值断言
（SC-002 的封印）。把两者做成纯函数即同时满足「查询面不改状态、不消费随机」与「界面明示金额与概率」。
clamp 到 **1.0**（而非 `DiceRateCap`）是 FR-006 的字面口径：地狱殿试第 2 次即
`0.35 × 1.2 × 3.0 = 1.26 > 1`，而「被抓概率」与「录取率」是两个量，前者的上界只能是 100%。

**Alternatives considered**: ① 把金额与概率写进 `BribeEntry` 内部——被否：查询面必须能独立调用；
② 对概率不 clamp——被否：会产生「概率 > 100%」的可观察值（spec Edge Cases 明令禁止）。

---

## R-05 乘数 kⁿ 的形状与 `NeutralBribeMultiplier` 的删除

**Decision**: `BribeRules.MultiplierOf(ExamLevel level, Person examinee)` 返回 `kⁿ`，实现为**乘法循环**
（初值 `1m`，重复 `n` 次乘 `k`）——**不引入任何额外常量**：

```csharp
public static decimal MultiplierOf(ExamLevel level, Person examinee);
```

`ExamPolicy.NeutralBribeMultiplier`（`ExamPolicy.cs:63`）与 `GameConfig.Exam.NeutralBribeMultiplier`
（`GameConfig.cs:434`）**删除**；`ExamSeasonRunner.cs:297` 与 `:323` 改传 `MultiplierOf(...)`。

**Rationale**: Q1 = B 把指数钉成「该级别累计计数」，故 `n = 0`（从未行贿）时乘数必然是 `k⁰ = 1.0`——
这由循环初值**自然给出**，保留一个值为 `1.0` 的常量只会造出「k⁰ 的第二真源」，与 FR-008 的
「占位随之作废」和 SC-003 的「占位值在任何允许多次尝试的路径上都不可达」直接冲突。删除后，
`SpecAnchorTests` 的占位锚点改为对恒等元的断言（`MultiplierOf(level, 计数为 0 的成员) == 1.0m`）。

**Alternatives considered**: ① 保留常量并把 `n = 0` 映射到它——被否：同一件事两个名字，且旧测试会
继续「验证」一个已被取代的占位；② 用 `Math.Pow`——被否：`decimal` 没有整指数 `Pow` 的重载，
转 `double` 会破坏「金额与概率以十进制定点计算」的既有口径（章程「技术栈与工程约束」）。

---

## R-06 当月事实缓冲的形状与落点

**Decision**: `Family` 新增一个成员，`KFL.Core` 新增两个文件：

```csharp
// src/KFL.Core/Entities/PunishmentJournal.cs
public sealed class PunishmentJournal
{
    public IReadOnlyList<BribeFact> Bribes { get; }
    public IReadOnlyList<SentenceFact> Sentences { get; }
    public IReadOnlyList<FineFact> Fines { get; }
    public IReadOnlyList<RelativeBanFact> RelativeBans { get; }
    public IReadOnlyList<CollectivePunishmentFact> CollectivePunishments { get; }
    public IReadOnlyList<ReleaseFact> Releases { get; }
    public bool FamilyBanDeclared { get; }          // FR-030「斯文扫地」的事实
    internal void Append(…); internal void Restore(…); internal void Clear();
}

// Family.cs
public PunishmentJournal PunishmentJournal { get; }   // 只读引用；Clear 由结算第③步调用
```

`PunishmentAdvance.Run` 在**第③步开头的固定时刻**（递减之前）把日志投影成 `PunishmentSnapshot`
并**立即清空**。

**Rationale**: 见 plan.md「阶段 0 / 阶段 1 小结」第 1 条——Q2 把「建立」钉在结算之外、FR-028 要求
「当月事实」出现在 `SettlementResult` 里，二者只有缓冲能同时满足；缓冲必须落盘，否则「行贿 → 退出
保存 → 重载 → 结算」会丢失整格惩罚。它**不是**第二真源：状态的真源仍是 `Person` 的计时/状态位与账本，
日志只承载「已发生但尚未被某次结算报告」的事实，第③步读后即清空，且 `SaveStateValidator` 新增一条
「日志行的 `PersonId` 必须存在于家族成员集」的引用完整性校验。

**Alternatives considered**: ① 把「施加惩罚」挪进结算第③步，让日志只承载行贿事实——被否：Q2 的裁决
文本明写「行贿与惩罚的**建立**发生在**结算之外**的月份内」，且 US2 的独立测试要求「直接把一次被抓
喂给惩罚入口」；② 纯内存缓冲——被否：004 的存档节奏允许决策事件后退出保存；③ 挂在 `GameState` 上
——被否：001 明令 `GameState` 不承载集合，`GameStateTests.cs:110` 还断言其公开属性恰为七个；
④ 用 `Person` 上的「待处理」标记代替日志——被否：连坐涉及多名亲属与功名前后值，单标记表达不了快照
要求的六类事实。

---

## R-07 12 格惩罚矩阵的配置形状

**Decision**: `Config/PunishmentPolicy.cs` 用**一个只读表**单点声明 12 格，并提供查表函数：

```csharp
public readonly record struct PunishmentCell(
    int SentenceMonths,            // 0 = 不建立服刑（死刑格）
    ExecutionKind Execution,       // None / ImmediateSentence（当事人死刑）
    bool CollectivePunishment,     // 仅地狱殿试为真
    FineKind Fine,                 // None / Ratio / Fixed
    decimal FineRatio,             // Fine == Ratio 时有效
    Money FineFixedGuan,           // Fine == Fixed 时有效
    int RelativeBanMonths,         // 0 = 不建立禁考（简单档「无禁考」）
    bool RelativePromotionBan,     // false = 该格「只禁考」（困难 / 地狱的解试与省试）
    bool FamilyWideBan);           // 仅地狱殿试为真（全族 48 月）

public static PunishmentCell CellOf(Difficulty difficulty, ExamLevel level);
public const int RatioFineCapGuan = 50000;
public const int FamilyWideBanMonths = 48;
public const int ReleaseDemotionLevels = 1;
```

**Rationale**: FR-001 要求矩阵数值**单点声明**；矩阵是**表**，用一个不可变记录类型逐格列出，使
SC-004 的「12 格逐格断言」在测试里能直接 `[Theory]` 覆盖 `(Difficulty, ExamLevel)` 的 12 个组合，
且「简单档禁考 = 0 月」「困难解试禁升 = false」这类**结构性**差异由字段而非分支表达。
数值逐字取自规格书 §7.3：简单 5 年 / 2%（无禁考）、3 年 / 1%（无禁考）、1 年（无罚金）；普通
15 年 / 15% / 36 月、5 年 / 200 贯 / 24 月、3 年 / 100 贯（无亲属禁考）；困难 死刑 / 20% / 48 月、
20 年 / 10% / 36 月（只禁考）、10 年 / 5% / 36 月（只禁考）；地狱 死刑 + 连坐 / 20% / 全族 48 月、
20 年 / 10% / 36 月、10 年 / 5% / 36 月。

**Alternatives considered**: ① 用 `switch` 表达式就地返回元组——被否：12 格 × 9 字段的元组不可读，
且「只禁考 / 无禁考」这类标记会退化成隐式约定；② 每格一个 `const` 常量组——被否：36 个常量无法表达
「哪一格是哪一格」，查表逻辑会与声明分离。

---

## R-08 总资产访问器与支付原语的可选 `PersonId`

**Decision**:

```csharp
// src/KFL.Rules/Economy/TotalAssetsCalculator.cs（新）
public static Money Of(FamilyEconomy economy);
public static Money Of(Treasury treasury, Holdings holdings);
```

`MonthlySettlementEngine.cs:138-140` 的就地表达式改为调用它（消除第二真源）；`GameConfig.Assets`
新增转发视图 `TotalAssets`。同时给 `PaymentPrimitive.Pay` **追加一个可选形参**：

```csharp
public static PaymentResult Pay(
    FamilyEconomy economy, LedgerCategory category, GameDate date, Money amount,
    PersonId? personId = null);          // 006 新增：默认 null ⇒ 行为与既有完全一致
```

条目经 `economy.Apply(category, personId, -fromTreasury, date)` 落盘。

**Rationale**: FR-011 明令总资产口径 MUST 有**单一访问器**，并让既有「12 月工出身 bonus 的总资产为正」
判定复用同一位——这正是把 `MonthlySettlementEngine` 的就地计算提取出来的理由。行贿与罚金都是**个人**
行为，而 `Pay` 目前把条目落成家族级（`personId: null`）；契约四 §5 的角色级统计（「按角色的每月与累计
收支」，U3 会消费）会因此把「谁把家败了」抹平。加**默认值**形参是源码兼容的，既有 `LoanTests` 的三步
语义断言一字不动，也不触碰「不做部分支付」「不产生负余额」的既有承诺。

**Alternatives considered**: ① 不改 `Pay`，让罚金与行贿落成家族级——被否：U3 的支出明细会丢失归属，
与契约四 §5 的角色级维度相悖；② 在 `KFL.Core` 加一个「只扣现金」的新 API——被否：违反契约四 §3
「唯一写入通道」，且会造出绕过账本的资金流动（章程「复杂度 MUST 被论证」）。

---

## R-09 惩罚推进步的形状与位置

**Decision**: 新建 `KFL.Rules/Punishment/PunishmentAdvance.cs`：

```csharp
public static PunishmentSnapshot Run(GameState state);
```

`MonthlySettlementEngine.Settle` 在**第 ② 步（米价游走）之后、原第 ③ 步（官吏推进）之前**调用它，
成为新的**第 ③ 步**；原 ③~⑧ 顺移为 ④~⑨，全月结算由八步变**九步**。步内固定次序（Q2 = A）：

```
① 投影并清空日志（行贿 / 被抓 / 服刑建立 / 罚金 / 亲属禁考禁升 / 连坐 / 死刑 / 全族禁考禁升）
→ ② 按 PersonId 升序逐人：服刑 / 禁考 / 禁升剩余月数各递减 1（仅当计时非空）
→ ③ 递减到 0：服刑 ⇒ 释放（清计时与状态位、Rank 非 null 则 Level + 1、L18 维持、仍在禁考期则继续禁考）；
   禁考 / 禁升 ⇒ 清对应计时与状态位
→ ④ 统计当月在册服刑人数（供 FR-029④ 与「囹圄之灾」）
```

**本步消费随机恰好 0 次**；`MUST NOT` 调用 `AdvanceMonth`、`MUST NOT` 修改 `CurrentDate`。

**Rationale**: FR-020 逐字规定位置与「按 `PersonId` 升序逐人处理」；与 `OfficialCareerAdvance.Run` 的
编排同形。位置的理由是 Q2：递减 → 释放/降级/解除在同月的官吏推进与科举季**之前**生效，故「禁考 N 月」
恰封锁 N 个考试月、**期满当月即可报名**，停职计时也自释放当月起恢复推进。0 次数随机使
`DeterminismTests.随机消费次序固定为米价储蓄利率再计息()` 的逐月 `NextDouble` 计数**保持全绿**。

**Alternatives considered**: ① 挂进 `OfficialCareerAdvance`——被否：003 的类文档明写「本特性只消费
`PromotionBanned` 与 `ServingSentence`，MUST NOT 递减/新建/清除」；② 排在科举季之后——被否：会让
「禁考 N 月」实际封锁 N+1 个考试月（Q2 的裁决理由）；③ 由 `TurnRunner` 在结算之外调用——被否：
「同种子 → 逐位相同」的 `SettlementResult` 序列要求它是**结算内**的一步。

---

## R-10 快照子段的形状

**Decision**: 新建 `KFL.Rules/Punishment/PunishmentSnapshot.cs`，`SettlementResult` 新增：

```csharp
public required PunishmentSnapshot Punishment { get; init; }
```

快照含 `Month`、日志投影出的六类行记录（`IReadOnlyList`，**全部按 `PersonId` 升序**）、
`bool FamilyBanDeclared`、`int PrisonerCount`；并提供 `public static PunishmentSnapshot None(GameDate month)`
的静态空实例（与 `ExamSeasonSnapshot.None` 同款）。

**Rationale**: FR-028 明令「作为 `SettlementResult` 的新增必填子段」，与 003 的 `Career`、005 的
`ExamSeason` 同款：**增量、只读、MUST NOT 写回 `GameState`**、MUST NOT 成为第二真源。快照必须能表达
**不产生账本条目**的事实（服刑建立、亲属禁考禁升、连坐结果、释放、降级），因为账本不是状态的真源。

**Alternatives considered**: 只靠 `SettlementResult.Entries` 与状态 diff——被否：行贿的「第几次 / 是否
被抓」、服刑与禁考禁升的月数都不产生账本条目，且 diff 会把「本月发生了什么」变成副作用推断。

---

## R-11 查询面的形状（FR-029 + FR-030 派生事实）

**Decision**: 新建 `KFL.Rules/Punishment/PunishmentQueries.cs`（**全纯函数**，不改状态、不消费随机）：

```csharp
public static bool IsWithinBribeWindow(GameDate date, ExamLevel level);              // ①③
public static BribeFeasibility Of(GameState state, Person examinee, ExamLevel level); // ① 金额/概率/是否触顶
public static PunishmentStatus StatusOf(Person person);                              // ② 三计时剩余月数 + 是否有官职
public static int PrisonerCount(Family family, GameDate date);                       // ④ 某月服刑人数
public static int FamilyCumulativeBribes(Family family);                             // FR-030
public static bool IsLevelCapReached(GameState state, Person examinee, ExamLevel level);  // FR-030「无法无天」
public static bool WasBribeFreeAtImperialPass(Person person);                        // FR-030「清白传家」
```

**Rationale**: FR-029 明令 U1/U2/U4 与 ⑧ MUST 只经该查询面取值，且查询面 MUST 是纯函数（SC-008 要求
「调用前后 `GameState` 逐字段差异项 = 0 且随机消费 0」）。触顶判据取**最终概率**（`BribeRules` 的输出
经 005 的同一入口 `ExamAdmission.AdmissionRate` / `ImperialPromotionRate` 计算）达到 `DiceRateCap`，
因此本特性 MUST NOT 复制录取率公式，也不引入「乘数上限」这一规格书里不存在的概念。
`WasBribeFreeAtImperialPass` 是 FR-030「清白传家」事实的**派生**实现（见 plan.md 勘误 #2/#3）。

**Alternatives considered**: ① 让每个查询各自读 `GameState` 并就地算——被否：会复制窗口/金额/概率公式
（章程原则 VI）；② 把「触顶」实现为「乘数 ≥ 某上限」——被否：规格书从未给乘数设上限。

---

## R-12 连坐判定与降功名的落点

**Decision**: `PunishmentImposer` 内提供三分支判定，并把降功名经**既有**通道追加记录：

```csharp
public static void Apply(GameState state, Person subject, ExamLevel level, IRandomService random);   // 当事人当场
private static IReadOnlyList<Person> RelativesOf(Family family, Person subject);   // 五类在世亲属（含归档的外嫁女儿）
private static void DemoteDegree(Person person, GameDate month);
```

`RelativesOf` = `SpouseOf` + `ChildrenOf`（按 `Gender` 过滤儿子/女儿）+ `TryGet(FatherId)` +
`TryGet(MotherId)`，逐项过滤「在世」（`!(Status & Deceased)`）；**父/母/配偶/子女的集合取
`Family.Members`（含归档成员）**，以便「已出嫁归档的女儿被跨族追及」（FR-025）自然成立。
`DemoteDegree` 追加一条 `DegreeRecord`（`Cause = DegreeChangeCause.PunishmentDemotion`，`Level` 降一级，
白身无降），**清空 `Placement` / `ImperialClass`** 以满足 `DegreeRecord.EnsureCompatible` 的四条兼容性；
`Person.AppendDegree` 的**降级 MUST NOT 清零解试/省试失败数**约束（`Person.cs:181-182`）已由既有实现保证。

**Rationale**: FR-023/FR-025 要求三分支互斥、范围严格限于五类、外嫁女儿被追及。001 已备好
`DegreeChangeCause.PunishmentDemotion`（`DegreeChangeCause.cs:16`，文档逐字写明「连坐降级」），
且 `DegreeRecord.cs:9-13` 已预告连坐会产生降级记录——本特性只是它的**首个产品生产者**。

**Alternatives considered**: ① 从 `Family.RegisteredMembers` 取亲属——被否：会漏掉归档的外嫁女儿，
直接违反 FR-025；② 自创一个功名变化原因——被否：004/005 同款约束（MUST NOT 自创变化原因）；
③ 用 `Person.CurrentDegree` 直接改写——被否：功名变迁史是功名的**唯一**真源，改写会让历史与当前值分叉。

---

## R-13 释放降级的纯函数

**Decision**: `PunishmentAdvance` 内提供纯函数，降级只作用于**官阶级数**：

```csharp
public static bool DemoteOnRelease(Person person);
// Rank is null            ⇒ false（无官职者不降，规格书 §7.5）
// Rank.Level >= LowestLevel(18) ⇒ false（已是 L18 则维持）
// 否则 person.Rank = new OfficialRank(person.Rank.Level + 1); return true;
```

**Rationale**: FR-020 与规格书 §7.5「曾有官职者释放时降 1 级官阶（已是 L18 则维持）」。判据取
「释放时 `Rank` 非 null」，**不新增「曾为官」标记**——新增标记会与 `Rank` 构成第二真源（001 的口径）。
L18 是 `SalaryTable.LowestLevel`（`SalaryTable.cs:23`），本特性 MUST NOT 自造 18 这个字面量。
003 的 `OfficialCareerAdvance.TryPromote`（`:198-220`）是**私有**的晋升函数（`Level − 1`），故降级必须
在 006 侧新增，并回写 003 的契约（回写清单 #7）声明「释放降级由 ⑥ 落地」。

**Alternatives considered**: ① 复用 003 的私有 `TryPromote`——被否：语义相反（升 vs 降）且不可见；
② 把降级并进「递减到 0」的分支里写死——被否：纯函数可被单测直接钉住（SC-006 要求「降级恰 1 次」）。

---

## R-14 调试入口与「放宽上限」的接缝取舍

**Decision**: FR-031 的五个入口落成**普通公开规则入口**，**不新增任何接缝接口**：

| 入口 | 落点 | 语义 |
| --- | --- | --- |
| 重置某成员的贿赂计数（按级别或全部） | `BribeEntry.ResetBribeCounts(Person, ExamLevel?)` | 计数回落 ⇒ 金额与概率相应回落 |
| 解除自身禁考 / 禁升 | `PunishmentCommands.ClearBans(Person, GameDate)` | 一次清个人两个计时与状态位 |
| 解除全族禁考 / 禁升 | `PunishmentCommands.ClearFamilyBans(Family, GameDate)` | 一次清**全部在册成员**的相应计时（FR-021） |
| 释放囚犯 | `PunishmentCommands.ReleasePrisoner(Person, GameDate)` | 走与第③步**同一**的释放路径（含降级） |
| 赦免死囚 | `PunishmentCommands.PardonDeathRow(Person)` | 对**已亡者无效**（§7.5 死刑立即执行 ⇒ 无「已判未决」状态） |

「放宽上限」（突破 `0.899999`）**不在本特性新建接缝**，登记为 U4 规格化的开放项。

**Rationale**: 章程原则 VI 要求「越限能力经装配期显式注入的配置或策略提供」。封顶的实际执行点是
**两处**：`BribeRules.IsLevelCapReached`（置灰判据）与 005 的 `ExamAdmission.Clamp`（`ExamAdmission.cs:118`
的 `private static decimal Clamp`，内部 `Math.Clamp(rate, 0m, ExamPolicy.DiceRateCap)`）。要真正放宽，
必须同时改写 005 的**内部** clamp——那是对上游录取率入口的形状变更，且唯一消费者 U4 尚未规格化；
现在预置一个只用一次的接口违反章程「MUST NOT 为假想的未来需求预置结构」。本特性因此只保证
「规则层**没有**按调用方分支的绕过路径」，并把该接缝登记为 U4 的开放项（同 `赦免死囚` 的口径）。
**001 的 `contracts/injection-seams.md` 因此不改动**。

**Alternatives considered**: ① 现在就定义 `IAdmissionCapPolicy` 并让 `ExamAdmission` 接收它——被否：
跨特性形状变更 + 无消费者 + 预置结构；② 在规则层提供 `bool allowOverCap` 形参——被否：正是章程
原则 VI 禁止的「按调用方分支放宽上限」。

---

## R-15 账本类别与元数据

**Decision**: `LedgerCategory` 追加第 **21/22** 个取值 `BribeExpense`（行贿支出）与 `Fine`（罚金），
声明序置于**末尾**（`ExamTravelCost` 之后）；两者在 `LedgerCategoryMetadata` 中均为
`KindOf → LedgerEntryKind.Treasury`（资金类）、`CreditTargetOf → null`（恒为负）。同步：
`LedgerCategory.cs:4` 的「20 个取值」→ 22、`:15-16` 的「『贿赂』…MUST NOT 存在」条款改写、
`LedgerCategoryMetadata.cs:23` / `:59` 的 `<exception>` 文档与 `:50-51` / `:90-91` 的两处 `default:`
异常文案、`:33` 的「其余 15 个」→ 17、`:76` 的「恒为负的支出类（5 类）」→ 7，以及
`LedgerInvariantTests.cs` 的六处穷举断言。

**Rationale**: FR-027 明令「新增**资金类**账本类别以承载行贿支出与罚金」。两个类别而非一个：契约四 §5
的家族级「支出明细」与 U3 的统计页需要区分「主动买路钱」与「被判罚」；合并会让两类语义不同的支出
在同一类别里互相淹没。声明序末位使既有 20 个取值的二进制整数不变（升版已拒绝旧档，但「追加在末尾」
的 diff 与审计成本最低）。**注意**：`LedgerInvariantTests.cs:106-112` 的禁用词守卫含 `"Bribe"`，
故该守卫 MUST 改写为「只允许 `BribeExpense` / `Fine` 两个新增类别名」——这是**有意**的形状变更，
不是绕过守卫。

**Alternatives considered**: ① 只加一个 `BribeAndFine`——被否：见上；② 用**事件类**承载罚金——被否：
罚金真的动了资金池，E-08 的二分类会破；③ 插到支出类分组中（`LivingCost` 附近）——被否：会平移既有
取值的整数，diff 与审计成本更大且无收益。

---

## R-16 落盘形状与版本提升

**Decision**:

| 位置 | 变更 |
| --- | --- |
| `PersonDto` | 追加键 **25** `jieShiBribeCount`(`int`)、**26** `shengShiBribeCount`(`int`)、**27** `dianShiBribeCount`(`int`)；成员数常量 25 → **28** |
| `FamilyDto` | 追加键 **7** `punishmentJournal`(`PunishmentJournalDto`)；成员数常量 7 → **8** |
| `SaveStateValueDtos` | 新增 `PunishmentJournalDto`（6 个列表成员）与六个事实行 DTO |
| `SaveFileFormat.Version` | **2 → 3**；`!= 3` 一律 `UnsupportedVersion`（拒绝而非迁移） |
| 同步物 | `MessagePackSaveFormat` 的 `PersonMemberCount` / `FamilyMemberCount` 与 `EnsurePersonShape` / `EnsureFamilyShape`（`:254` 的尾随 `Skip(3)` → `Skip(6)`；`:226` 的 `Skip(1)` → `Skip(2)`）、`SaveStateMapper` 双向映射与复原次序（新计数并入第 ② 步、日志复原独立一步）、`SaveStateComparer.AssertPerson`、`SaveStateValidator` 新增第 ⑪/⑫ 条、`SaveFormatContractTests` 的 `BinaryKeyTables`（`:388-397`）/ `ContractKeyTable`（`:488-571`）、`SaveFixtures.RichState()` 的非默认值、`SaveFailureTests` 的版本闸门用例（旧 **2** 拒绝 / 更新 **4** 拒绝） |

**Rationale**: FR-032。004 的线上形状是**位置式数组**（`[Key]` 自 0 连续），「加字段 = 改线上形状」，
而 004 的版本闸门是「**拒绝**而非迁移」（`SaveFileFormat.cs:24-29`），因此必须升版。键一律**追加在
末尾**（25/26/27、7），既有键的序号与键名一字不动。`SaveFixtures.RichState()` MUST 把三个新计数与
日志设成**非默认值**（否则往返测试会静默通过，`SaveFixtures.cs:19-25` 的既有口径）。

**Alternatives considered**: ① 不升版、把新字段当可选尾随成员——被否：004 契约十 §5 条款 3 明令
「任一字段的缺失 MUST 被检出，MUST NOT 静默默认化」，且 walker 的位置式 Skip 会把旧档静默错位读成
别的字段；② 把日志拆成多个 `FamilyDto` 键——被否：日后新增事实类型会再破一次线上形状。

---

## R-17 测试组织与「不引入新工程 / 新接缝 / 界面代码」

**Decision**: `tests/KFL.Tests/Rules/` 新增 5 个文件——

| 文件 | 覆盖 |
| --- | --- |
| `BribeEntryTests.cs` | FR-003~FR-010、FR-031（重置面）；SC-002、SC-003 |
| `PunishmentMatrixTests.cs` | FR-011~FR-016、FR-027；SC-004（12 格逐格）、SC-005 |
| `PunishmentAdvanceTests.cs` | FR-017~FR-021；SC-006（递减、暂停、释放、降级、L18 维持） |
| `CollectivePunishmentTests.cs` | FR-022~FR-026；SC-007（三分支、免死、外嫁追及、范围封印） |
| `BribeQueryTests.cs` | FR-029、FR-030；SC-008（纯函数性） |

并同步 `Rules/{SpecAnchorTests,DeterminismTests,ExamAdmissionTests,ExamSeasonTests}.cs`、
`Core/{PersonTests,StatusTests,LedgerInvariantTests}.cs`、`Architecture/ConfigLiteralTests.cs`、
`Infrastructure/{SaveFixtures,SaveStateComparer,SaveFormatContractTests,SaveStateValidatorTests,
SaveFailureTests}.cs`。六个工程不变（G-02）；新增类型全部落在既有 `KFL.Core` 与 `KFL.Rules`；
**MUST NOT** 在 `KFL.Rules` 之外定义任何结算接缝接口；`KFL.Presentation` 与 `KFL.App` 一行不动。

**Rationale**: 章程原则 IV（规则改动 MUST 同提交给出测试）；硬约束是「全部经 `IRandomService` 注入、
无界面/文件系统/网络、MUST NOT 访问系统时钟或全局随机」（G-07）。
`RulesTestHarness.FixedRandomService`（可设种子/可排队列）是本特性的主随机来源。

**Alternatives considered**: ① 把五个文件合成一个 `BriberyTests.cs`——被否：本特性跨越「行贿行动 /
矩阵 / 结算编排 / 连坐 / 查询面」五个正交关注点，合起来会让失败定位退化（002/003/005 的既有划分
也是按关注点分文件）；② 为「快进在贿赂入口置灰」造一个信号抽象——被否：U1 只需纯查询，为假想的
未来需求预置结构违反章程「复杂度 MUST 被论证」。

---

## 未决事项

无。三项口径冲突已由用户在 2026-10-12 裁决（见 `spec.md` 的 `## Clarifications`），三条工件内部
矛盾已在 [plan.md](./plan.md) 的「勘误登记」中按 §17② 定案并登记为待回写项，其余选择全部落在既有契约
与既有实现的约束内；本文件**未引入任何规格书之外的数值**。
