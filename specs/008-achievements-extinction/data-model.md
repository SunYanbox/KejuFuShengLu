# 阶段 1 设计：成就与绝嗣结局的数据模型

**特性**：`008-achievements-extinction`
**输入**：`spec.md`、`research.md`、契约二十二/二十三/二十四、《科举浮生录规格书》§14 / §14.1 / §15 / §17。

**形状权威顺序**：规格书（数值与需求）→ `spec.md`（意图）→ 本文件（模型形状）→ 契约（接口与键表）→ 代码。
本文件与契约描述的是**本特性交付时的形状**；`src/` 现状见各条「现状」标注。

---

## 1. 层分布总览

| 层 | 新增 | 变更 |
| --- | --- | --- |
| `KFL.Core` | `AchievementId`、`AchievementSeries`、`OriginMilestone`（枚举）；`AchievementMark`、`PendingUnlock`、`ChronicleSnapshot`（值对象）；`AchievementState`、`EndingState`（聚合） | `GameState`：**+2 聚合属性**（内联初始化，ctor 形状不变） |
| `KFL.Infrastructure` | `IAchievementStore`、`AchievementArchive`、`AchievementFlushResult`（接缝）；`AchievementStore`（实现）；`AchievementArchiveJson`（序列化）；5 个新 DTO | `SaveFileFormat.Version` 4→5；`SaveStateDto` 8→10 键；`SaveStateMapper`；`MessagePackSaveFormat`；`SaveStateValidator` +7 条 |
| `KFL.Rules` | `AchievementPolicy`（配置）；`Achievements/` 目录 11 个文件 | `TurnRunner`：①~⑤ 编排（**第 ② 步与第 ⑤ 步新增**，ctor +1 可选参）；FR-009 的 12 个动作入口各 +1 次上报调用 |
| `KFL.Presentation` / `KFL.App` | 无 | 无（界面轨 U1~U5 全部 Out of Scope） |
| `tests/KFL.Tests` | `Rules/` 10 个 + `Infrastructure/` 4 个 + `Architecture/` 1 个测试文件（合计 **15**） | `RulesTestHarness` / `SaveCadenceTests` 的 `TurnRunner` 构造点（可选参，可不改） |

**方向约束复核**：`Core` 仍零出边；判定器（`Rules`）通过**只读参数**接收全局面，不注入 store（R-01）；
文件系统接触面全部在 `Infrastructure`（章程 II 与 G-07）。

---

## 2. `KFL.Core` 的新增与变更

### 2.1 新增枚举

#### `src/KFL.Core/Enums/AchievementId.cs`

```csharp
public enum AchievementId
{
    // 系列一：初露锋芒（ACH-001~012）
    FoundationLaid = 1,        // ACH-001 开基立业
    FirstJuRen = 2,            // ACH-002 首个举人
    FirstJinShi = 3,           // ACH-003 首个进士
    GoldenList = 4,            // ACH-004 金榜题名
    ThreeGenerations = 5,      // ACH-005 三代同堂
    FiveGenerations = 6,       // ACH-006 五世其昌
    TenThousandGuan = 7,       // ACH-007 家资万贯
    WealthOfTheRegion = 8,     // ACH-008 富甲一方
    Longevity = 9,             // ACH-009 寿比南山
    CleanRecord = 10,          // ACH-010 清白传家
    PrisonRelease = 11,        // ACH-011 铁窗生涯
    NarrowEscape = 12,         // ACH-012 绝处逢生
    // 系列二：科名累进（ACH-013~020）
    JuRenThree = 13,           // ACH-013 杏林初茂
    JuRenTen = 14,             // ACH-014 桂子飘香
    JuRenHundred = 15,         // ACH-015 满门朱紫
    JuRenThousand = 16,        // ACH-016 文运昌隆
    JinShiThree = 17,          // ACH-017 雁塔题名
    JinShiTen = 18,            // ACH-018 簪缨世族
    JinShiHundred = 19,        // ACH-019 朝堂半壁
    JinShiThousand = 20,       // ACH-020 天下文宗
    // 系列三：宦海沉浮（ACH-021~031）
    FirstAppointment = 21,     // ACH-021 释褐登仕
    Prefect = 22,              // ACH-022 牧守一方
    Chancellor = 23,           // ACH-023 位极人臣
    Retirement = 24,           // ACH-024 悬车致仕
    OfficeHolders = 25,        // ACH-025 袍笏登场
    Merit = 26,                // ACH-026 政通人和
    JieShiFailuresThree = 27,  // ACH-027 屡挫屡奋
    VenerableScholar = 28,     // ACH-028 皓首穷经
    ImperialSweep = 29,        // ACH-029 鼎甲包揽
    ExamFailuresTwenty = 30,   // ACH-030 科场遗珠
    FallenAtShengShi = 31,     // ACH-031 名落孙山
    // 系列四：宗族血脉（ACH-032~048）
    LargeHousehold = 32,       // ACH-032 人丁兴旺
    FiftyBirths = 33,          // ACH-033 百子千孙
    ImperialGroomPick = 34,    // ACH-034 榜下捉婿
    DegreeMarriage = 35,       // ACH-035 秦晋之好
    DeathInChildbirth = 36,    // ACH-036 红颜薄命
    InfantDeath = 37,          // ACH-037 襁褓之殇
    FarmlandHundred = 38,      // ACH-038 阡陌交通
    FarmlandFiveHundred = 39,  // ACH-039 良田万顷
    FirstShop = 40,            // ACH-040 旺铺之主
    TradeLoss = 41,            // ACH-041 商海浮沉（待接线，恒 false）
    DebtBurden = 42,           // ACH-042 债台高筑
    DebtFree = 43,             // ACH-043 无债一身轻
    // 系列五：覆巢之下（ACH-049~059）
    FamilyExtinct = 44,        // ACH-044 烟消云散
    CenturyHouse = 45,         // ACH-045 百年世家
    MillenniumHouse = 46,      // ACH-046 千秋万代
    AllFourOrigins = 47,       // ACH-047 四民皆历
    HellEnding = 48,           // ACH-048 大梦一场
    FirstSuccessfulBribe = 49, // ACH-049 投石问路
    BribeThree = 50,           // ACH-050 暗度陈仓
    BribeTen = 51,             // ACH-051 权钱交易
    BribeHundred = 52,         // ACH-052 只手遮天
    BribeThousand = 53,        // ACH-053 买断功名
    BribeCapReached = 54,      // ACH-054 无法无天
    ExecutedForBribe = 55,     // ACH-055 身首异处
    CollectivePunishment = 56, // ACH-056 覆巢之下
    MarriedOutPunished = 57,   // ACH-057 殃及池鱼
    FamilyWideBan = 58,        // ACH-058 斯文扫地
    ThreePrisoners = 59,       // ACH-059 囹圄之灾
    // 系列六：饥馑求生（ACH-060~063）
    FirstRelief = 60,          // ACH-060 嗟来之食
    ReliefRecoveredPaidInFull = 61,   // ACH-061 枯木逢春
    ReliefRecoveredShortfall = 62,    // ACH-062 沉疴难起
    FamineDeath = 63,          // ACH-063 路有冻骨
    // 系列七：调试秘辛（ACH-064~068，待接线）
    DebugWakeOne = 64,         // ACH-064 天机泄露
    DebugWakeTen = 65,         // ACH-065 窥探虚实
    DebugWakeHundred = 66,     // ACH-066 篡改命数
    DebugWakeThousand = 67,    // ACH-067 虚空主宰
    DebugWakeTenThousand = 68, // ACH-068 代码飞升
}
```

**值约定**：数值 = `ACH-xxx` 的 `xxx`（十进制、从 1 起、连续无洞）。目录表用它做「齐全性」断言。
成员名 ↔ `ACH-xxx` ↔ 中文名的三元组以本表（与契约二十二 §1）为唯一字典。

#### `src/KFL.Core/Enums/AchievementSeries.cs`

```csharp
public enum AchievementSeries
{
    Emerging = 0,       // 初露锋芒（ACH-001~012，12 条）
    ExamProgression = 1,// 科名累进（ACH-013~020，8 条）
    OfficialCareer = 2, // 宦海沉浮（ACH-021~031，11 条）
    Lineage = 3,        // 宗族血脉（ACH-032~048，17 条）
    CollectiveRuin = 4, // 覆巢之下（ACH-049~059，11 条）
    Famine = 5,         // 饥馑求生（ACH-060~063，4 条）
    DebugSecrets = 6,   // 调试秘辛（ACH-064~068，5 条）
}
```

**计数不变量**：`12 + 8 + 11 + 17 + 11 + 4 + 5 == 68`，由 `AchievementCatalogTests` 逐系列断言。

#### `src/KFL.Core/Enums/OriginMilestone.cs`

```csharp
public enum OriginMilestone
{
    Extinction = 0,     // 绝嗣（与 ACH-044 同一次触发）
    CenturyFamily = 1,  // 百年世家（与 ACH-045 同一次触发）
}
```

用于全局文件的「出身达成」段的**达成类别**（FR-020）。它不是成就标识：
一段记录 = `(Origin, OriginMilestone, GameDate, SourceSaveId)`。

### 2.2 新增值对象

#### `src/KFL.Core/ValueObjects/AchievementMark.cs`

```csharp
public readonly record struct AchievementMark(AchievementId Achievement, Difficulty Difficulty, GameDate CompletedAt);
```

- 用途：档内 ⑦「本档已完成集合」的元素，也是全局文件的合并键。
- `Difficulty` 参与键：规格书 §14:538「每条成就的各难度**独立**标记完成状态」。
- 最早日期语义（FR-018）在**合并**时体现，不在本类型里：合并取 `min(CompletedAt)`。

#### `src/KFL.Core/ValueObjects/PendingUnlock.cs`

```csharp
public readonly record struct PendingUnlock(
    AchievementId Achievement, Difficulty Difficulty, GameDate UnlockedAt, Guid SourceSaveId);
```

- 用途：档内 ⑧「待落盘解锁队列」的元素。比 `AchievementMark` 多 `SourceSaveId`（FR-013 ⑧ 逐字要求
  「含解锁年月与来源 UUID」），供全局记录写入 `SourceSaveId`。
- 不变量：每个 `PendingUnlock` 的 `(Achievement, Difficulty)` MUST 也在 `Completed` 里（契约二十三 §7 第 2 条）。

#### `src/KFL.Core/ValueObjects/ChronicleSnapshot.cs`

```csharp
public sealed record ChronicleSnapshot
{
    public required int SurvivedYears { get; init; }          // ① CurrentDate.Year − 1
    public required int Generations { get; init; }            // ② 最大 Generation + 1
    public required Money PeakTotalAssets { get; init; }      // ③ = AchievementState.PeakTotalAssets
    public required DegreeLevel HighestDegree { get; init; }  // ④ 全量成员 DegreeHistory 的最高功名
    public required int TotalJinShi { get; init; }            // ⑤ 进士按人计数（去重）
    public required decimal AverageLifespan { get; init; }    // ⑥ 三均值，3 位小数
    public required IReadOnlyList<PersonId> Genealogy { get; init; }  // ⑦ Family.Members 的 Id 序列（含归档）
}
```

七项与规格书 §15:632-641 一一对应（顺序也按规格书）。
`Genealogy` 的语义见 R-09：只冻结**次序与成员集合**，不复制 `Person`。
`HighestDegree` 取不到任何功名时用 `DegreeLevel.BaiShen`——既有 `DegreeLevel` 枚举的零值
（**不存在** `DegreeLevel.None`，见 `plan.md` E-13；`Person.CurrentDegree` 对空历史本来就返回白身）；
`Generations` 在零成员时为 `0`（不可能，绝嗣前至少有过开局成员；仍由类型给值避免 `Nullable`）。

### 2.3 新增聚合 `AchievementState`

#### `src/KFL.Core/Entities/AchievementState.cs`

```csharp
public sealed class AchievementState
{
    private readonly List<int> _lifespans = [];
    private readonly List<AchievementMark> _completed = [];
    private readonly List<PendingUnlock> _pendingUnlocks = [];

    public int SuccessfulBribeCount { get; private set; }        // ①
    public int BirthCount { get; private set; }                  // ②
    public IReadOnlyList<int> Lifespans => _lifespans;           // ③ 完整序列（非「和 + 个数」）
    public Money PeakTotalAssets { get; private set; }           // ④
    public bool EverHadLoan { get; private set; }                // ⑤
    public int DebugWakeCount { get; private set; }              // ⑥
    public IReadOnlyList<AchievementMark> Completed => _completed;          // ⑦
    public IReadOnlyList<PendingUnlock> PendingUnlocks => _pendingUnlocks;  // ⑧

    // ── 规则层（KFL.Rules）写通道：只在事实发生时被调用 ──
    public int RecordSuccessfulBribes(int count);                 // ① 增量累加，返回累计值
    public int RecordBirths(int count);                           // ② 增量累加（含夭折），返回累计值
    public void AppendLifespan(int age);                          // ③ 逐个追加（年龄 MUST >= 0）
    public void ObserveTotalAssets(Money totalAssets);            // ④ 取极大值（负值按 0 处理，口径同 TotalAssetsCalculator）
    public void RecordLoanOriginated();                           // ⑤ 置 true（幂等）
    public int ReportDebugWake(int count = 1);                    // ⑥ 增量累加，返回累计值（FR-041 ⑥ 的接缝）
    public bool MarkCompleted(AchievementMark mark);              // ⑦ 幂等：键 (Achievement, Difficulty) 已存在 ⇒ 不改动、返回 false
    public bool MarkCompletedAndEnqueue(AchievementMark mark, Guid sourceSaveId);  // ⑦+⑧ 的原子组合，返回是否新解锁

    // ── Infrastructure 复原通道（internal）──
    internal void Restore(int successfulBribeCount, int birthCount, IReadOnlyList<int> lifespans,
                          Money peakTotalAssets, bool everHadLoan, int debugWakeCount,
                          IReadOnlyList<AchievementMark> completed, IReadOnlyList<PendingUnlock> pendingUnlocks);
    // ⑧ 的单独写通道：**public**（勘误 E-19）——唯一常规调用方是 KFL.Rules 的 ⑤ 落盘成功分支，
    // 而 AssemblyInfo 只对 KFL.Infrastructure 放行 internal；Restore 仍为 internal。
    public void RestorePendingUnlocks(IReadOnlyList<PendingUnlock> pendingUnlocks);  // 落盘成功后清空队列
}
```

**设计要点**：

1. **列表只增不改**：`Completed` 与 `PendingUnlocks` 用 `private readonly List<T>` + 只读视图，
   与 `PunishmentJournal` / `FamilyLifecycleState` 的既有模式一致。
2. **`MarkCompleted` 幂等**（FR-011）：同 `(Achievement, Difficulty)` 第二次调用返回 `false`。
   `CompletedAt` **保留首次**的月份（档内语义），跨档的最早日期由全局文件的合并规则负责（FR-018）。
3. **`MarkCompletedAndEnqueue` 是唯一会同时写 ⑦ 与 ⑧ 的入口**：
   保证 R-15 第 2 条不变量（`PendingUnlocks ⊆ Completed`）在类型层就成立 —— 无法只写队列不写已完成。
4. **`ObserveTotalAssets` 取极大值**（FR-013 ④）。传入负值按 `Money.Zero` 处理，
   使 ④ 与 `TotalAssetsCalculator.Of`（`src/KFL.Rules/Economy/TotalAssetsCalculator.cs:50` 的
   `total.IsPositive ? total : Money.Zero`）口径一致，避免 ④ 记录负数而编年史 ③ 给负数。
5. **不存 `Money` 的可空版本**：初值为 `Money.Zero`（既有零值）。

**不变量（类型保证）**：
- `SuccessfulBribeCount >= 0 ∧ BirthCount >= 0 ∧ DebugWakeCount >= 0`（私有无参构造 + 只增入口）。
- `Lifespans` 每项 `>= 0`（`AppendLifespan` 抛 `ArgumentOutOfRangeException`）。
- `Completed` 内 `(Achievement, Difficulty)` 唯一（`MarkCompleted` 的幂等 + 哈希集加速）。
- `PendingUnlocks` 内 `(Achievement, Difficulty)` 唯一。
- `PendingUnlocks` 内每个键都能在 `Completed` 里找到（由 `MarkCompletedAndEnqueue` 保证）。

### 2.4 累计量与「不可重算」的证明表

8 项**严格**等于 `FR-013` 的列项（SC-014 禁止第九项）。每项的「为什么不能从既有数据重算」：

| # | 字段 | 为什么必须持久 |
| --- | --- | --- |
| ① | `SuccessfulBribeCount` | `BribeFact` 只在当月快照里存在（`PunishmentSnapshot.Bribes` 不落盘为历史行）；`Person` 上的 `JieShi/ShengShi/DianShiBribeCount` 把成功与被抓混计 |
| ② | `BirthCount` | 出生事实只在当月快照里；`Family.Members` 含开局成员与买来的人口，无法区分「出生」 |
| ③ | `Lifespans` | 三均值非线性，无法由 `Members` 的当前状态重算；已故成员的死亡年龄不可从 `Person` 复原（`AgeAt` 是「现在几岁」） |
| ④ | `PeakTotalAssets` | 「历史峰值」不可由当前资产推出（只减不增的路径上会漏掉中间高点） |
| ⑤ | `EverHadLoan` | `Loan` 可被完全清偿，清偿后 `Principal == 0` 不再能证明「曾经有过」（`IsSettled` 是派生 `:73`，且新借款会重置） |
| ⑥ | `DebugWakeCount` | 调试唤醒不产生任何落盘事实（FR-041 ⑥ 明写「可被外部上报的累计接缝」） |
| ⑦ | `Completed` | 全局文件是**跨档**面；档内必须有自己的记录，否则读档后无法判断「本档是否已解锁」（也支撑幂等） |
| ⑧ | `PendingUnlocks` | 「已判定但尚未落全局」这一状态无法从其他数据推出（FR-024 的重试依赖它） |

**R-04 的化解复核**（两条曾被怀疑需要第九项的判据）：

- `ImperialGroomPick`（ACH-034）读「当月 `LifecycleSnapshot.PendingProposal` 的 `Kind`」∪
  「状态面当前待应答提议的 `Kind`」；产出月的判定在同一 `Advance` 内完成，玩家动作在其后 ⇒「含被拒」天然成立。
- `DebtBurden`（ACH-042）读状态面 `Loan.Principal` ∪ `AchievementTrigger.LoanOriginated` 上报时刻的本金；
  `Principle` 只在借款时增大，故不被漏判。

### 2.5 新增聚合 `EndingState`

#### `src/KFL.Core/Entities/EndingState.cs`

```csharp
public sealed class EndingState
{
    public bool IsEnded { get; private set; }
    public GameDate? EndedAt { get; private set; }
    public ChronicleSnapshot? Chronicle { get; private set; }

    public bool End(GameDate at, ChronicleSnapshot chronicle);   // 幂等：已结束 ⇒ 原样返回 false
    internal void Restore(bool isEnded, GameDate? endedAt, ChronicleSnapshot? chronicle);
}
```

**不变量**：`IsEnded ⇔ EndedAt.HasValue ∧ Chronicle is not null`（由类型 + 一个私有构造路径保证）。
`End` 幂等是 SC-011 的前提：读档后再推进不会改写编年史。

### 2.6 `GameState` 的形状变更（+2 聚合属性，ctor 不变）

`src/KFL.Core/Entities/GameState.cs`（现状：`:29` class、`:71` `Id`、`:77` `CurrentDate`、`:80` `Difficulty`、
`:89` `PendingDifficulty`、`:95` `Family`、`:101` `Economy`）：

```csharp
// 新增（内联初始化，不进 ctor 参数表）
public AchievementState Achievements { get; } = new AchievementState();
public EndingState Ending { get; } = new EndingState();
```

**为什么正交**：
- `Achievements` 是**元进度**（跨档聚合的档内投影），`Ending` 是**终局**，两者都不属于
  `Family`（族的构成）或 `FamilyEconomy`（账本）。
- 内联初始化让 `GameState` 的 ctor 保持 6 参，`SaveStateMapper` / `NewGameSetup` / 既有测试的构造点不必改动（R-10）。
- 复原走 `internal Restore(...)`，与 007 的 `Family.Lifecycle.RestoreBookkeeping(...)` 同款。
  这条通道 MUST NOT 被 `KFL.Rules` 使用（`internal` 只在 `KFL.Infrastructure` 的同程序集内可见 —— 
  `Core` 与 `Infrastructure` 是**两个**程序集，故复原通道需 `InternalsVisibleTo`，
  与 007 现有做法一致，见 004/007 契约的「复原通道」条款）。

> **实施注意**：若 `KFL.Core` 当前未对 `KFL.Infrastructure` 开放 `InternalsVisibleTo`，
> 则改为「`public Restore` + 契约声明『仅 Infrastructure 可调』」的既有变体；
> 该二选一 MUST 在实现第一个任务时以 `src/KFL.Core/KFL.Core.csproj` 的现状决定，并以
> `SaveStateMapper` 能在不新增 `public` 面的前提下复原为验收标准。

### 2.7 明确不新增的 Core 类型

- **不新增** `EndingReason` 枚举：终局只有「绝嗣」一种（规格书 §15:628「本作无胜利条件，唯一的终止条件是
  **家族绝嗣**」），加枚举等于为不存在的分支留位置。
- **不新增** `AchievementCounter` / `SeriesProgress` 之类的派生聚合：SC-014 禁止夹带可重算值。
- **不新增** `Difficulty` 以外的成就维度：§14:536 只有「每难度独立标记」。
- **不改** `Family`、`Person`、`FamilyEconomy`、`Holdings`、`Loan`、`PunishmentJournal`、`FamilyLifecycleState`：
  008 的全部输入都已由既有面提供（见 §8 映射表）。

---

## 3. `KFL.Rules` 新增

### 3.1 `Achievements/AchievementAdjudicator.cs` —— ⑧ 的唯一判定入口

```csharp
public static class AchievementAdjudicator
{
    // 月末判定（FR-034 的 ②）：先更新累计量，再判 68 条，再判绝嗣
    public static AchievementOutcome Judge(GameState state, SettlementResult settlement,
                                          IReadOnlyList<AchievementMark> archiveMarks);

    // 动作型即时判定（FR-009）：12 个入口各调一次
    public static AchievementOutcome Report(GameState state, AchievementTrigger trigger);
}
```

**内部次序（MUST 由测试钉死）**：

1. **更新累计量**（FR-014「与事实同月同步」）：① 由 `settlement.Punishment.Bribes` 里 `!Caught` 的条数追加；
   ② 由 `settlement.Lifecycle.Births.Count` 追加；③ 由 `settlement.Lifecycle.Deaths` 逐条取
   `person.AgeAt(factMonth)` 追加；④ `ObserveTotalAssets(TotalAssetsCalculator.Of(state.Family.Economy))`；
   ⑤ 由 `state.Family.Economy.Treasury.Loan` 判；⑥ 只由 `Report` 路径推进（不在月末重算）。
2. **判 68 条**（**不含** `FamilyExtinct` / `HellEnding`，这两条由第 3 步负责）；
   新解锁走 `MarkCompletedAndEnqueue(mark, state.Id)`。
3. **判绝嗣**：见 §3.6。若首次成立，冻结编年史并补判 `FamilyExtinct` / `HellEnding`。
4. 返回 `AchievementOutcome`。

**纯函数性**：`Judge` / `Report` MUST NOT 碰文件系统、时钟、`Random`、`Guid.NewGuid()`（G-07）；
全部输入来自参数与 `GameState`；`now` 取 `state.CurrentDate`（R-06），**不**额外传参。

### 3.2 `Achievements/AchievementCatalog.cs` —— 68 条静态目录

```csharp
public sealed record AchievementDefinition(
    AchievementId Id, string Code, string Name, AchievementSeries Series, AchievementSource Source);

public enum AchievementSource
{
    Creation = 0, Degree = 1, Asset = 2, Household = 3, Career = 4, Exam = 5,
    Punishment = 6, Famine = 7, Lifecycle = 8, Cumulative = 9, Ending = 10,
    GlobalArchive = 11, Pending = 12,   // Pending = 待接线（ACH-041 / 064~068）
}

public static class AchievementCatalog
{
    public static IReadOnlyList<AchievementDefinition> All { get; }        // 恰 68 项，按 Id 升序
    public static AchievementDefinition Of(AchievementId id);               // 非法值 ⇒ ArgumentOutOfRangeException
    public static int CountFor(AchievementSeries series);
}
```

目录表的 68 行与契约二十二 §1 的三元组表**逐字对应**（`Code` 用 `"ACH-001"` 形式）。
`Source` 是文档性元数据（便于按族组织测试），**不**参与判定路径的分支。

### 3.3 `Achievements/AchievementPredicates.cs` —— 68 条纯谓词

```csharp
public static class AchievementPredicates
{
    public static bool IsSatisfied(AchievementId id, AchievementContext ctx);   // 唯一分派点
}

public readonly record struct AchievementContext(
    GameState State,
    SettlementResult? Settlement,                 // 即时路径为 null
    IReadOnlyList<AchievementMark> ArchiveMarks,  // 全局「已完成」面（ACH-047 用）
    IReadOnlyList<OriginRecord> ArchiveOrigins,   // 全局「出身达成」面（ACH-047 用）
    GameDate Now);
```

`IsSatisfied` 用一个 `switch` 分派到 68 个私有纯函数；谓词**只读**参数（MUST NOT 改 `ctx` 里的任何对象）。
阈值一律引 `AchievementPolicy`（R-18）。逐条判据与阈值的唯一副本在契约二十二 §2。

### 3.4 `Achievements/AchievementCounters.cs` —— 累计量更新

```csharp
public static class AchievementCounters
{
    public static void AbsorbMonthFacts(GameState state, SettlementResult settlement);  // ①②③④⑤
    public static int ReportsDebugWake(GameState state, int count);                     // ⑥ + 即时判定
}
```

抽出来的理由：`AbsorbMonthFacts` 只做「事实 → 累计量」，与「判据」是两种职责；
测试可以只钉累计量而不牵动 68 条判据。

### 3.5 `Achievements/AchievementOutcome.cs`

```csharp
public sealed record AchievementOutcome
{
    public required IReadOnlyList<AchievementId> NewlyUnlocked { get; init; }   // 本次新解锁（按 Id 升序）
    public required IReadOnlyList<AchievementId> AlreadyCompleted { get; init; }// 本次判真但已解锁（幂等证据）
    public bool ExtinctionJustTriggered { get; init; }                          // 本次判定首次成立绝嗣
}
```

`TurnRunner` 用它决定 ⑤ 的落盘内容（只写 `NewlyUnlocked` 对应的待落盘队列增量；实际写的是
`state.Achievements.PendingUnlocks` 全量，见契约二十三 §5）。

### 3.6 `Achievements/ExtinctionJudge.cs` + `ChronicleBuilder.cs`

```csharp
public static class ExtinctionJudge
{
    public static bool IsExtinct(GameState state);      // Family.RegisteredMembers.Count == 0
    public static bool TryTrigger(GameState state);     // 首次成立 ⇒ 冻结编年史 + End() + 返回 true
}

public static class ChronicleBuilder
{
    public static ChronicleSnapshot Build(GameState state);   // 七项；⑥ 调 TukeyTrimean
}
```

- `TryTrigger` 只在 `!state.Ending.IsEnded && IsExtinct(state)` 时动作；内部调 `ChronicleBuilder.Build`。
- 编年史七项的数据来源逐条见契约二十四 §1（含「③ 取 `AchievementState.PeakTotalAssets`」这一交叉引用，
  它要求**先**跑 §3.1 第 1 步再判绝嗣）。

### 3.7 `Achievements/TukeyTrimean.cs`

```csharp
public static class TukeyTrimean
{
    public static decimal Of(IReadOnlyList<int> values);   // n == 0 ⇒ 0m
}
```

算法：升序副本 → Tukey hinges（奇数个时中位数**同时**属于两半）→ `(Q1 + 2*Q2 + Q3) / 4m` →
`Math.Round(x, 3, MidpointRounding.AwayFromZero)`。金色算例 8 行 + 判别例 `[10,20,30,40,100] → 30.000`
逐条进测试（契约二十四 §4）。

### 3.8 `Achievements/AchievementTrigger.cs`

`public enum AchievementTrigger`，12 项，见契约二十二 §3 的表（含调用点）。
枚举成员无显式数值（默认 0 起）；`0` 值语义为「非触发路径」，故第一个成员
`NewGameCreated = 1` 以保留「未指定」的零值（与 `CollectiveOutcome` 的「无零值」决定相反 —— 
这里需要一个「无触发」的可表示值，因为 `Report` 的日志/测试会用到）。

### 3.9 其余文件

| 文件 | 职责 |
| --- | --- |
| `Achievements/AchievementFlightLog.cs` | **不新建**。落盘队列的生命周期由 `AchievementState` 承载，不引入第二个簿记对象 |
| `Achievements/AchievementQuery.cs` | 只读查询面（供 U1 徽章与 U2 结局画面）：`CompletedCount(state)`、`IsCompleted(state, id)`、`SeriesProgress(state, series)`。**全部**由 ⑦ 投影，MUST NOT 持久 |

### 3.10 `Config/AchievementPolicy.cs`

阈值唯一真源（R-18）。逐条取值与来源行进契约二十二 §2 的「阈值」列。示例：

```csharp
public static class AchievementPolicy
{
    public const int CenturyFamilySurvivedYears = 100;      // 严格 >（ACH-045）
    public const int MillenniumFamilySurvivedYears = 300;   // 严格 >（ACH-046）
    public const int NarrowEscapeSurvivedYears = 100;       // >=（ACH-012）
    public const int FiveGenerationsMinGeneration = 4;      // >=（ACH-006）
    public const int ClanBirthCount = 50;                   // >=（ACH-033）
    public const int RichAssetsGuan = 10_000;               // >=（ACH-007）
    public const int WealthyAssetsGuan = 50_000;            // >=（ACH-008）
    public const int LongevityAge = 80;                     // >=（ACH-009）
    public const int VenerableStudyAge = 50;                // >=（ACH-028）
    public const int VenerableShengShiFailures = 6;         // >=（ACH-028）
    public const int GeniusStudy = 80;                      // >=（ACH-031）
    public const int DebtBurdenGuan = 100;                  // 严格 >（ACH-042）
    public const int ExamFailuresPileUp = 20;               // >=（ACH-030）
    public const int JuRenThreeThreshold = 3;               // 科名累进
    public const int JuRenTenThreshold = 10;
    public const int JuRenHundredThreshold = 100;
    public const int JuRenThousandThreshold = 1_000;
    public const int JinShiThreeThreshold = 3;
    public const int JinShiTenThreshold = 10;
    public const int JinShiHundredThreshold = 100;
    public const int JinShiThousandThreshold = 1_000;
    public const int BribeThreeThreshold = 3;
    public const int BribeTenThreshold = 10;
    public const int BribeHundredThreshold = 100;
    public const int BribeThousandThreshold = 1_000;
    public const int LargeHouseholdMembers = 20;            // >=（ACH-032）
    public const int OfficeHoldersCount = 5;                // >=（ACH-025）
    public const int PrisonerCount = 3;                     // >=（ACH-059）
    public const int MeritThreshold = 100;                  // >=（ACH-026）
    public const int PrefectRankMin = 5;                    // ACH-022 的 [5, 8]
    public const int PrefectRankMax = 8;
    public const int FarmlandHundredMu = 100;               // >=（ACH-038）
    public const int FarmlandFiveHundredMu = 500;           // >=（ACH-039）
    public const int DebugWakeOne = 1;
    public const int DebugWakeTen = 10;
    public const int DebugWakeHundred = 100;
    public const int DebugWakeThousand = 1_000;
    public const int DebugWakeTenThousand = 10_000;
    public const int TradeLossMinPercent = 10;              // ACH-041 的 10~30%（待接线）
    public const int TradeLossMaxPercent = 30;
    public const int InfantDeathMaxAge = 3;                 // ACH-037 的 < 3
    public const int AverageLifespanDecimals = 3;           // 舍入位数（Q1）
}
```

`GameConfig` 新增 `Achievements` 分节，逐成员**转发**（无字面量），风格照
`src/KFL.Rules/Config/GameConfig.cs` 既有分节（如 `:837` 的 Lifecycle）。

---

## 4. 既有类型的形状变更（汇总表）

| 类型 | 变更 | 迁移影响 |
| --- | --- | --- |
| `GameState` | +2 只读聚合属性（内联初始化） | ctor 不变；`SaveStateMapper` 增加一段复原调用 |
| `SaveFileFormat` | `Version` 4 → 5 | 全部既有断言用常量，无需改；2 处陈旧注释须改 |
| `SaveStateDto` | 8 → 10 键 | 二进制成员数常量同步；JSON 侧无结构约束 |
| `MessagePackSaveFormat` | `SaveStateMemberCount` 8 → 10；+5 常量；跳过序列与分支 | 形状校验的「多余成员」分支同步 |
| `SaveStateValidator` | +7 条一致性校验 | 既有 15 条不动 |
| `TurnRunner` | ctor +1 可选参（`IAchievementStore?`）；`Advance` 顶部加结束拒绝；① 之后插入 ②，⑤ 新增 | 既有构造点用可选参默认值，可不改 |
| 12 个动作入口（`NewGameSetup.Create`、003/002 的资产与借款入口、007 的 `LifecycleEntry` 5 个方法、006 的 `BribeEntry`、005 的询问应答入口） | 各 +1 次 `AchievementAdjudicator.Report(...)` 调用 | 排序在「动作成功之后、返回之前」；失败路径 MUST 不调 |
| `FamilyLifecycleState` | **不变** | 007 的 `docs/data-model.md` §9 的「不含」措辞需回写 |

**MUST NOT 变更**：`SettlementResult` 及其六类快照（FR-035）、`Family`、`Person`、`Loan`、`Holdings`、
`PunishmentJournal`、`FamilyEconomy`、`MonthlySettlementEngine` 的 ①~⑩ 步骤表、`ISaveService`。

---

## 5. 状态转移

### 5.1 回合末（`TurnRunner.Advance`）

```
Advance(state)
  └─ state.Ending.IsEnded ? ── 是 ──▶ throw InvalidOperationException（零变更，R-16）
                            └─ 否
     ① settlement = MonthlySettlementEngine.Settle(state)      // 既有，⑩ 已把 CurrentDate 推进
     ② outcome    = AchievementAdjudicator.Judge(state, settlement, ReadArchiveMarks())
     ③ 若 _saveService is null ──▶ 返回 TurnOutcome(Saved: false, BackedUp: false)   // ⑤ 跳过
        否则 _saveService.Save(state, _slot, overwrite: true)
     ④ 备份（SaveCadencePolicy 判定，既有）
     ⑤ 若 _achievementStore is not null 且 state.Achievements.PendingUnlocks 非空
            result = _achievementStore.Flush(state.Achievements.PendingUnlocks, state.Id)
            成功 ⇒ state.Achievements.RestorePendingUnlocks([])      // 清空队列
            失败 ⇒ 队列保留（FR-024），TurnOutcome.AchievementFlush 携带失败诊断，不抛
     返回 TurnOutcome(...)
```

**要点**：
- ② 在 ③ 之前 ⇒ 队列**随档持久**（R-13 的核心理由）。
- ③ 短路（无存档服务）时 ⑤ 仍执行：成就是独立于存档的能力（规格书 §14:534 反复强调）。
- ⑤ 失败**不抛**（FR-024）：与 ③ 的「失败上抛」形成对比，因为 ⑤ 落的是元进度而非用户数据，
  且已有档内队列承担重试。这条对比 MUST 写进 `TurnOutcome` 的 XML doc。
- ④ 在 ⑤ 之前：备份的是「已含 ⑦⑧ 增量」的档；若备份后 ⑤ 失败，恢复该备份仍能重试。

### 5.2 动作型即时判定

```
<动作入口>::<动作执行成功>
  └─ AchievementAdjudicator.Report(state, <trigger>)
       ├─ 必要时先推进 ⑥（仅 DebugWakeReported）
       ├─ 判 68 条中「源族 ∈ 该触发器相关族」的条目（实现上可全表判，成本可忽略）
       └─ 新解锁 ⇒ MarkCompletedAndEnqueue(mark, state.Id)
      （不落全局文件，不写档，不抛：动作入口的契约不变）
```

### 5.3 读档

```
SaveStateMapper.MapToDomain(dto)
  └─ state.Achievements.Restore(...)   // ⑦⑧ 逐元素复原，保持次序
  └─ state.Ending.Restore(...)         // IsEnded ⇒ EndedAt/Chronicle 非空（校验器已保证）
```

复原 MUST 保持 `Completed` / `PendingUnlocks` 的**元素次序**（SC-011 逐字段比较依赖它）。

---

## 6. 不变量清单

| # | 不变量 | 守卫 |
| --- | --- | --- |
| 1 | `AchievementState` 的持久字段恰为 FR-013 的 8 项 | `AchievementStateDto` 成员数常量断言 + 代码审查（SC-014） |
| 2 | `Completed` 内 `(Achievement, Difficulty)` 唯一 | 类型（`MarkCompleted` 幂等） |
| 3 | `PendingUnlocks` 内 `(Achievement, Difficulty)` 唯一 | 类型 |
| 4 | `PendingUnlocks ⊆ Completed` | 类型（唯一入口 `MarkCompletedAndEnqueue`） |
| 5 | `Lifespans.Count <= Members.Count(p => p.Status.HasFlag(StatusFlag.Deceased))`（**E-21 修订**：上界而非相等——处决/株连在 `DeathRegister` 之外置 `Deceased`、不记享年） | `SaveStateValidator` 新条 3 |
| 6 | `IsEnded ⇔ EndedAt.HasValue ∧ Chronicle is not null` | 类型 + 校验器新条 5 |
| 7 | `IsEnded ⇒ RegisteredMembers.Count == 0` | 校验器新条 6 |
| 8 | `Chronicle.Genealogy` 逐元素 = `Family.Members.Select(p => p.Id)` | 校验器新条 7 |
| 9 | `Chronicle.SurvivedYears == EndedAt.Year − 1` | 校验器新条 7 |
| 10 | 68 条目录表齐全且 `Id` 与枚举一一对应 | `AchievementCatalogTests` |
| 11 | 判定器无环境依赖（无时钟/随机/文件/网络/GUID 生成） | G-07（`src\KFL.Rules\` 在扫描范围） |
| 12 | 008 的阈值只出现在 `src\KFL.Rules\Config\` | 章程「技术栈与工程约束」（`ConfigLiteralRules`） |
| 13 | `EndingState.End` 幂等（第二次不改编年史） | `EndingStateTests` + SC-011 |
| 14 | 已结束存档的推进零变更 | SC-010（三面：异常、状态快照、目录清单） |
| 15 | 同种子同输入 ⇒ 判定结果逐字段一致 | SC-003（`AchievementDeterminismTests`） |
| 16 | `SaveFileFormat.Version == 5` 且 `SaveStateDto` 恰 10 键 | `SaveFormatContractTests` |

**不校验（明确排除集市）**：`PeakTotalAssets` / `HighestDegree` / `TotalJinShi` / `AverageLifespan`
与任何「重算值」的一致性 —— 它们是**冻结的历史**，绝嗣后不可重算（R-15 第 2 点）。
把它们写成校验会在合法档上误报。

---

## 7. 持久化形状与版本

### 7.1 存档本体（MessagePack / JSON 双格式）

| DTO | 键数 | 键表 |
| --- | --- | --- |
| `SaveStateDto` | **10**（原 8） | `[8] achievements`、`[9] ending?` |
| `AchievementStateDto` | 8 | `[0] successfulBribeCount`、`[1] birthCount`、`[2] lifespans`、`[3] peakTotalAssets`、`[4] everHadLoan`、`[5] debugWakeCount`、`[6] completed`、`[7] pendingUnlocks` |
| `AchievementMarkDto` | 4 | `[0] achievement`、`[1] difficulty`、`[2] completedYear`、`[3] completedMonth` |
| `PendingUnlockDto` | 5 | `[0] achievement`、`[1] difficulty`、`[2] unlockedYear`、`[3] unlockedMonth`、`[4] sourceSaveId` |
| `EndingStateDto` | 3 | `[0] endedYear`、`[1] endedMonth`、`[2] chronicle` |
| `ChronicleDto` | 7 | `[0] survivedYears`、`[1] generations`、`[2] peakTotalAssets`、`[3] highestDegree`、`[4] totalJinShi`、`[5] averageLifespan`、`[6] genealogy` |

- 年月用两个 `int` 键（照 004 的 `SaveStateDto[1]/[2]` 既有风格），不引入新的日期表示。
- `Genealogy` 用 `IReadOnlyList<Guid>`（`PersonId` 的底层类型见既有 DTO 的处理方式）。
- 枚举一律写底层整数（004 既有约定）。

`MessagePackSaveFormat` 新增常量：`AchievementStateMemberCount = 8`、`AchievementMarkMemberCount = 4`、
`PendingUnlockMemberCount = 5`、`EndingStateMemberCount = 3`、`ChronicleMemberCount = 7`；
`SaveStateMemberCount` 8 → 10；`EnsureSaveStateShape` 的 `Skip(ref reader, 6)` → `Skip(ref reader, 8)`
并补两个分支（`achievements` 必在、`ending` 可 `nil`）。

### 7.2 全局成就文件（`achievements.json`）

**不受存档版本闸门管辖**，自带 `Version`（首版 = 1）：

```json
{
  "Version": 1,
  "Achievements": [
    { "Achievement": 44, "Difficulty": 2, "CompletedAt": { "Year": 17, "Month": 8 }, "SourceSaveId": "…" }
  ],
  "Origins": [
    { "Origin": 0, "Milestone": 0, "GameDate": { "Year": 17, "Month": 8 }, "SourceSaveId": "…" }
  ]
}
```

- 合并键 `(Achievement, Difficulty)`；已存在则取**较早**的 `CompletedAt`（FR-018）；`SourceSaveId` 取
  **最早那条**的来源（FR-025「模型与界面 MUST NOT 另行保存、推算或推断来源 UUID」）。
- `Origins` 追加去重键 `(Origin, Milestone)`，同样保留最早。
- 未知键忽略、缺失取默认（FR-019）。**MUST NOT** 对 `Origins` 数组元素标 `[JsonRequired]`。

### 7.3 版本同步清单（SC-013）

| 位置 | 动作 |
| --- | --- |
| `src/KFL.Infrastructure/Save/SaveFileFormat.cs:32` | `4` → `5` |
| `src/KFL.Infrastructure/Save/SaveFileHeader.cs:21` | 注释「恒为 3」→ 5 |
| `tests/KFL.Tests/Infrastructure/SaveFailureTests.cs:23` | 注释「恒为 3」→ 5 |
| `SaveFormatContractTests.cs:37,56,270`、`SaveStateValidatorTests.cs:356`、`SaveFailureTests.cs:156-157,183-184,477,503,585-586` | **无改动**（全部以常量书写，已核对） |
| `specs/004-save-system/contracts/save-format.md` | 回写键表、版本闸门、常量登记、校验条数 |

---

## 8. 需求 → 模型映射

| 需求 | 模型落点 |
| --- | --- |
| FR-001 稳定标识 | `AchievementId`（值 = ACH 序号）+ `AchievementCatalog` |
| FR-002/003/004 判定输入面 | `AchievementContext`（`State` / `Settlement` / `ArchiveMarks` / `ArchiveOrigins` / `Now`） |
| FR-005 无新依赖 | 仅用 `System.*` 与既有类型；无新 NuGet |
| FR-006 唯一入口 | `AchievementAdjudicator.Judge` / `.Report` |
| FR-007 判定时点（结算后） | §5.1 的 ② |
| FR-008 消费六类快照 | `AchievementContext.Settlement` 的 `Punishment` / `Career` / `ExamSeason` / `Lifecycle` / `FamineBefore` / `FamineAfter` |
| FR-009 动作型即时判定 | `AchievementTrigger` + §5.2 + 契约二十二 §3 |
| FR-010 全局持久化 | `IAchievementStore` + `achievements.json` |
| FR-011 幂等 | `AchievementState.MarkCompleted` 返回 bool；`AchievementOutcome.AlreadyCompleted` |
| FR-012 无环境依赖 | §3.1 纯函数性 + G-07 |
| FR-013 累计量 8 项 | `AchievementState` 的 8 个持久字段（§2.4 的证明表） |
| FR-014 与事实同月同步 | `AchievementCounters.AbsorbMonthFacts`（§3.1 第 1 步） |
| FR-015/016 各难度独立 | `AchievementMark.Difficulty` 参与键 |
| FR-017 落 `RootDirectory` | 契约二十三 §1 |
| FR-018 最早日期 | 契约二十三 §3 的合并规则 |
| FR-019 向后兼容 | 契约二十三 §2 |
| FR-020 出身达成段 | `OriginMilestone` + `AchievementArchive.Origins` |
| FR-021/022/023 出身语义 | 复用 `GameState.Origin`（既有）；`OriginMilestone` 只标达成 |
| FR-024 失败不阻断 | §5.1 的 ⑤ 与 `AchievementFlushResult` |
| FR-025 来源 UUID | `PendingUnlock.SourceSaveId` + 全局记录的 `SourceSaveId` |
| FR-026 绝嗣判据 | `ExtinctionJudge.IsExtinct`（R-07） |
| FR-027 …(略，见 spec) | 见下方未完部分对应的 § |
| FR-028 编年史七项 | `ChronicleSnapshot`（§2.2）+ `ChronicleBuilder` |
| FR-029 拒绝推进 | §5.1 顶部 + R-16 |
| FR-030 往返一致 | §5.3 + SC-011 |
| FR-032 同一次编排 | §3.1 的第 2、3 步（同一 `Judge` 调用内） |
| FR-033 层落点 | §1 的层分布表 |
| FR-034 TurnRunner ①~⑤ | §5.1 |
| FR-035 不插入结算步骤表 | §4 的 MUST NOT 清单 |
| FR-036 存档形状变更 | §7.1 + §4 |
| FR-037 契约自二十二 | 本特性 `contracts/` 三个文件 |
| FR-041~043 待接线 | `TradeLoss` 恒 false + `AchievementTrigger.DebugWakeReported` + `IAchievementStore.Read` 读面（R-19） |
| FR-048 测试落点 | 契约二十二 §5 的测试映射表 |
| SC-001~015 | 见各条括注；SC-014 由序列化清单断言；配置字面量守卫引章程「技术栈与工程约束」 |

---

## 9. 明确不包含

1. **界面轨全部**（U1~U5）：徽章墙、tooltip、结局画面、编年史绘制、成就列表排序与筛选。
   本特性只交付 `AchievementQuery` 这一只读查询面。
2. **调试控制台本体**（规格书 §13）：只提供 ⑥ 的上报入口与 5 条成就判据。
3. **ACH-041 的「经商亏损事件」规则**：规格书 §14.1:604 列了成就但未定义事件；本次恒 false（R-19）。
4. **随机事件总表**（规格书 §16.1 里未列入逻辑轨 ⑧ 的部分）。
5. **旧档迁移**：`Version` 等值闸门，旧档一律拒收（004 既有语义，不新增迁移器）。
6. **声望属性**：规格书未定义；008 不引入。
7. **多进程/并发写 `achievements.json`**：单进程假设（与 004 的存档写同款）。
8. **成就的排序/展示口径**（按完成时间、按系列、按难度）：属 U1 的界面决定。
9. **`AchievementState` 的第 9 项累计量**：SC-014 明令 ≤ 8 项（R-04 已证明不需要）。
10. **`SettlementResult` 的新子段**：FR-035 明令不插入结算步骤表；判定消费既有六类快照即可。
