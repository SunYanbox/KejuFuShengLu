# 阶段 0 研究：成就与绝嗣结局

**特性**：`008-achievements-extinction`（逻辑轨 ⑧）
**输入**：`specs/008-achievements-extinction/spec.md`（sole source of intent）、《科举浮生录规格书》§14 / §14.1 / §15 / §16.1 / §17、
既有代码面（`src/` + `tests/`）、001~007 的活工件与契约。

**本阶段要消解的不是「需求」而是「落点」**：需求已由 spec.md 定稿（含 Q1~Q3 与次级口径 A~D 裁决），
规格书未给实现的空白产出了一条 §17 裁决块需求（见 R-21）。本文件只回答「用哪个既有面承载、为什么不用另一个」。

---

## 采纳状况总览

| 编号 | 议题 | 结论 | 影响工件 |
| --- | --- | --- | --- |
| R-01 | 判定器落层与唯一入口 | 采纳：`KFL.Rules/Achievements/AchievementAdjudicator` 单入口 | data-model §3.1 |
| R-02 | 68 条标识的表示 | 采纳：`AchievementId` 枚举（68 成员）+ 目录表校验 | 契约二十二 §1 |
| R-03 | 判据表组织 | 采纳：静态目录 `AchievementCatalog` + 纯谓词 `AchievementPredicates` | 契约二十二 §2 |
| R-04 | 累计量最小集 | 采纳：严格 8 项，ACH-034/042 化解为「即时触发」与「状态面」 | data-model §2.4 |
| R-05 | 三均值算法 | 采纳：`TukeyTrimean` 内的 Tukey hinges，`decimal` 3 位 `AwayFromZero` | 契约二十四 §4 |
| R-06 | 「当前月」时点口径 | 采纳：判定在 ⑩ 之后，`存续年数 = CurrentDate.Year − 1` | 契约二十四 §2 |
| R-07 | 绝嗣判据的实现面 | 采纳：`Family.RegisteredMembers.Count == 0` | 契约二十四 §3 |
| R-08 | 结局状态载体 | 采纳：`GameState.Ending` 新聚合属性（非 `Family` 内嵌） | data-model §2.5 |
| R-09 | 编年史族谱字段 | 采纳：只冻结 `PersonId` 序列，不复制 `Person` 字段 | data-model §2.3 |
| R-10 | 累计器挂载方式 | 采纳：`GameState` 内联初始化 + `internal` 复原通道（**不改 ctor 形状**） | data-model §2.6 |
| R-11 | 全局文件格式 | 采纳：恒 JSON，**不做** MessagePack 侧 | 契约二十三 §2 |
| R-12 | 损坏隔离命名 | 采纳：`achievements.corrupt-<n>.json` 递增序号（**不用时钟**） | 契约二十三 §4 |
| R-13 | 全局落盘点唯一性 | 采纳：唯一落盘点 = `TurnRunner.Advance` ⑤ | 契约二十三 §5 |
| R-14 | 存档版本与键表扩展 | 采纳：`Version = 4 → 5`，`SaveStateDto` 8 → 10 键 | 契约二十三 §6 |
| R-15 | 一致性校验新增条目 | 采纳：新增 7 条（不夹带可重算派生值） | 契约二十三 §7 |
| R-16 | 已结束存档的拒绝 | 采纳：`Advance` 顶部抛 `InvalidOperationException`，零状态变更 | 契约二十四 §5 |
| R-17 | 即时判定的触发器清单 | 采纳：12 个 `AchievementTrigger`，登记在契约二十二 §3 | 契约二十二 §3 |
| R-18 | 阈值常量落点 | 采纳：`KFL.Rules/Config/AchievementPolicy.cs` + `GameConfig.Achievements` 转发 | data-model §3.10 |
| R-19 | 待接线成就的接缝形状 | 采纳：3 个接缝（亏损事件上报、调试唤醒上报、`IAchievementStore` 读面） | 契约二十二 §4 |
| R-20 | 与既有测试的冲突面 | 采纳：以常量驱动断言，新增字段不产生字面量断言改动 | 契约二十三 §8 |
| R-21 | 规格书 §17 裁决块 | 采纳：追加「2026-10-18（逻辑轨 ⑧ 的空白填充）」块 | plan.md 回写义务清单 |

---

## R-01 判定器的落层与唯一入口

**决定**：在 `src/KFL.Rules/Achievements/AchievementAdjudicator.cs` 建立**唯一**判定入口，公开两个方法：

```csharp
public static AchievementOutcome Judge(GameState state, SettlementResult settlement,
                                       IReadOnlyList<AchievementMark> archiveMarks, GameDate now);
public static AchievementOutcome Report(GameState state, AchievementTrigger trigger, GameDate now);
```

- `Judge` 是月末判定（消费六类当月事实快照 + 状态面 + 累计量 + 全局已达成面），FR-034 的 ② 调它一次。
- `Report` 是动作型即时判定（FR-009 的 12 个入口各调一次）。

**理由**：

1. 章程 II 要求规则层不碰文件系统。判定器需要读「全局出身达成段」（ACH-047），因此**不注入 store**，
   由调用方把已读到的全局面**以只读参数**传入（`archiveMarks`）。这样判定器保持纯函数、可单测、
   不产生层序倒置（`KFL.Rules` 已引用 `KFL.Infrastructure`，但纯函数面比注入面更易钉死 SC-003 的确定性）。
2. 唯一入口是收敛不变量：`spec.md` FR-006 要求「全部 68 条的判定 MUST 经由同一入口」，一个类的两个公开方法
   比「分散在 6 个执行器里各判各的」更可测。`AchievementCatalog` 的 68 条定义与 `AchievementPredicates` 的
   68 个纯谓词都只被这一个类调用。
3. 不把判定器塞进 `MonthlySettlementEngine`：FR-035 明令「成就判定与绝嗣判定 MUST NOT 插入结算步骤表内」，
   且 007 的结算步骤表（①~⑩）已被 `specs/007-family-lifecycle/contracts/lifecycle.md` 钉死并由测试断言次序。

**替代方案与否决原因**：

- *方案 B：判定器作为 `MonthlySettlementEngine` 的第 ⑪ 步* —— 否决。会让「结算」这一概念同时负责规则推进与
  元进度记录，且 007 的步骤表测试会失败；FR-035 直接禁止。
- *方案 C：把 `IAchievementStore` 注入判定器（`Judge` 内部读文件）* —— 否决。判定器将无法在纯规则单测里
  运行（`RulesHarness` 目前不装配 Infrastructure 的存档面，`tests/KFL.Tests/Rules/RulesTestHarness.cs:18`），
  且 G-07 的扫描范围包含 `src\KFL.Rules\`。读文件留在调用方（`TurnRunner`）里。

---

## R-02 68 条标识的表示

**决定**：`src/KFL.Core/Enums/AchievementId.cs`，`public enum AchievementId`，68 个语义化成员，
`ACH-001`~`ACH-068` 作为**稳定外部标识**记入 XML doc 与 `AchievementCatalog` 的目录表。

**理由**：

- 成就完成集合（`AchievementState.Completed`）需要去重与幂等（FR-011），枚举天然提供键语义；
  字符串标识则要靠 `StringComparer.Ordinal` 约定，容易在 JSON 面写出大小写不一致的键。
- 仓库既有风格就是语义枚举：`DeathCause.OldAge`（`src/KFL.Core/Enums/DeathCause.cs:18`）、
  `DegreeLevel.JuRen`（`src/KFL.Core/Enums/DegreeLevel.cs:10`）、`AppointmentTrack.SpecialTribute`
  （`src/KFL.Core/Enums/AppointmentTrack.cs:16`）。`ACH-001…` 作为枚举成员名会得到 `ACH001` 这种
  无意义名字，违背既有风格。
- 68 条的完整性靠测试钉死（`AchievementCatalogTests` 断言目录表恰好 68 项、`ACH-00n` 连续无洞、
  枚举每个成员都在目录表里且只出现一次），这比「枚举成员名就等于标识」更严格。

**替代方案与否决原因**：

- *方案 B：`readonly record struct AchievementId(string Value)` 强类型字符串* —— 否决。
  连续两个 `default` 会得到 `null` 值，破坏「Core 值合法性由构造守卫保证」的既有模式；
  且 JSON 侧的未知标识向后兼容（FR-019）需要「未知值」表示，枚举的 `(AchievementId)int` 转换即可承载。
- *方案 C：只在目录表里用字符串，集合用 `HashSet<string>`* —— 否决。见上「键语义」。

**已知代价（登记于 plan.md Complexity Tracking）**：枚举成员名是**新引入的命名面**，规格书只给了中文名。
本计划在契约二十二 §1 逐条钉死「枚举成员名 ↔ ACH-xxx ↔ 中文名」三元组，作为后续实现的唯一字典。

---

## R-03 判据表的组织

**决定**：

- `AchievementCatalog`：`IReadOnlyList<AchievementDefinition>`，68 项，每项
  `AchievementDefinition(AchievementId Id, string Code, string Name, AchievementSeries Series, AchievementTriggerFamily Family)`。
  其中 `Family` 声明该条的**事实来源族**（创建 / 功名 / 资产 / 户籍 / 生涯 / 科举 / 惩罚 / 饥馑 / 生命周期 / 累计 / 结局 / 全局 / 待接线）。
- `AchievementPredicates`：68 个 `public static bool` 纯函数，签名统一为
  `bool IsXxx(AchievementContext ctx)`，`AchievementContext` 是只读聚合（state、settlement、archiveMarks、now、counters 视图）。

**理由**：

1. 「一条成就 = 一条定义 + 一个谓词」让「目录表完备性」与「谓词可触发」成为两个独立可测的命题：
   前者是静态断言（68 项齐全），后者是参数化测试（68 × 触发/不触发）。
2. `AchievementContext` 的存在避免 68 个签名各自拉参数（否则一半谓词要 8 个参数）。
3. 判据**不读** `SettlementResult` 之外的历史：凡需要历史的判据一律落到 R-04 的 8 项累计量或既有持久数据上。

**替代方案与否决原因**：

- *方案 B：`AchievementPredicates` 用 `switch (id)` 单方法* —— 否决。67 个 `case` 无法用「目录表 68 项齐全」
  的静态断言覆盖，且编译器不会提示漏项。
- *方案 C：把判据写成数据（JSON/DSL）* —— 否决。判据里有跨成员去重（ACH-013 的「按人计数」）、
  月度前后比较（ACH-061/062）与派生谓词（ACH-010），不是可声明的表达式；且会引入新依赖（FR-005 禁止）。

---

## R-04 累计量最小集：ACH-034 与 ACH-042 的化解

**决定**：`AchievementState` 的持久字段**严格等于** FR-013 列的 8 项（① 累计行贿成功次数、② 累计出生人数、
③ 已故成员享年完整序列、④ 月末总资产历史峰值、⑤ 是否曾产生贷款、⑥ 调试唤醒次数、⑦ 本档已完成集合、
⑧ 待落盘解锁队列）。**不新增第九项**。

两条看起来需要「历史」的判据这样化解：

- **ACH-034 榜下捉婿**（判据：「历史上出现过 `Kind == ImperialProposal` 的提议，**含被拒**」）：
  捉婿提议由 ⑧-a 产出，落在 `LifecycleSnapshot.PendingProposal`（`src/KFL.Rules/Family/LifecycleSnapshot.cs:44`，
  类型 `MarriageProposal?`）与状态面 `Family.Lifecycle` 的待应答提议上。产出月 M 的月末判定**当场**就能看到它
  （同一份快照），解锁与「玩家在月 M 之后拒绝」无关 —— 时间次序保证了「含被拒」成立。
  玩家动作发生在 `Advance` 返回之后，而 M 的判定在同一 `Advance` 内已跑完。
  ⇒ **无需持久标志**，判据读「当月快照的提议」∪「状态面当前待应答提议」。
  （若提议在 M 月被玩家拒绝、M+1 月 ⑧-a 覆写为新提议，M 的解锁早已落进 ⑦，不会被撤销。）
- **ACH-042 债台高筑**（判据：「历史任一时点贷款本金 > 100 贯」）：
  `Loan.Principal` 只在两个动作上**增大**（借款发生）与**减小**（还款，先本后息），而 `Loan` 本身是状态面
  持久对象（`src/KFL.Core/Entities/Loan.cs:36-44`）。因此「任一时点 > 100」等价于
  「(a) 月末状态面 `Loan.Principal > 100`」∪「(b) 某个 `AchievementTrigger.LoanOriginated` 上报时本金 > 100」。
  两者都不需要新字段：月末判定读状态面，即时判定由 R-17 的触发器提供。
- **ACH-043 无债一身轻**（判据：「曾产生贷款 ∧ 其后 `IsSettled`」）：直接由 ⑤ 累计标志 + 状态面
  `Loan.IsSettled`（`src/KFL.Core/Entities/Loan.cs:73`，**派生不落盘**）合成。

**理由**：FR-013 的 8 项是**闭集**，SC-014 断言「`AchievementState` 的持久字段数 ≤ FR-013 的列项数」
并由「序列化字段清单断言 + 代码审查」双钉。任何「顺手多存一个方便」的字段都会让 SC-014 失败，
所以必须先把两条看似需要历史的判据证明为可重算，再定形状。

**替代方案与否决原因**：

- *方案 B：为 ACH-034 加 `bool EverProducedImperialProposal`* —— 否决。违反 SC-014；
  且上文的时点论证表明它多余。
- *方案 C：为 ACH-042 加 `Money HistoricalMaxLoanPrincipal`* —— 否决。同上；`Loan.Principal` 单调分段，
  状态面 + 即时触发已完备。

---

## R-05 三均值（Tukey trimean）与平均寿命

**决定**：`src/KFL.Rules/Achievements/TukeyTrimean.cs`：

```csharp
public static class TukeyTrimean
{
    public static decimal Of(IReadOnlyList<int> values);   // n == 0 ⇒ 0m；否则 (Q1 + 2*Q2 + Q3) / 4
}
```

- `Q1`/`Q2`/`Q3` = Tukey hinges（中位数把**升序**样本分成两半，奇数个时**中位数本身同时属于两半**）。
- 结果四舍五入到 **3 位小数**，`MidpointRounding.AwayFromZero`。
- 编年史 ⑥「平均寿命」= `TukeyTrimean.Of(AchievementState.Lifespans)`。

**理由**：spec.md 的 Clarifications Q1 与「金色算例表」已把算法与舍入钉死到可逐字断言的程度；
其中 `[10,20,30,40,100] → 30.000` 正是「奇数个时中位数同时属于两半」的**判别例**
（若把中位数排除，会得到 36.250，与规格书 §15:639 冲突）。实现必须让这个判别例成为一条测试。

**与 ③ 的关系**：`Lifespans` 是「已故成员享年**完整序列**」（FR-013 ③），不是「和 + 个数」——
因为三均值不是线性统计量，无法由和与个数重算。序列里的每个值 = 该成员死亡当月的 `Person.AgeAt(月份)`
（`src/KFL.Core/Entities/Person.Family.cs:25`，实现在 `src/KFL.Core/ValueObjects/GameDate.cs:60-78`）。
「完整序列」的**上界**可由不变量校验：`Lifespans.Count <= Family.Members.Count(p => p.Status.HasFlag(StatusFlag.Deceased))`
（见契约二十三 §7；**E-21 修订**：处决与株连在 `DeathRegister` 之外置 `Deceased`、不记享年 ⇒ 只能取上界）。

**替代方案与否决原因**：

- *方案 B：换算术平均* —— 否决。规格书 §15:639 与 spec.md Q1 都已裁决为三均值（抗极端值）。
- *方案 C：中位数排除自己，取真二分位* —— 否决。判别例 `[10,20,30,40,100]` 会得到 36.250。
- *方案 D：用 `double` 计算* —— 否决。金额/统计一律 `decimal`（章程「数值与货币约束」）；
  且 3 位小数的舍入在 `double` 上不稳定，会破坏 SC-003 的逐字段确定性。

---

## R-06 「当前月」时点口径

**决定**：判定发生在 `MonthlySettlementEngine.Settle` **返回之后**（⑩ `GameState.AdvanceMonth()` 已执行），
故判定里的「当前月」= `state.CurrentDate`（**新月**）。由此：

- 存续年数 = `state.CurrentDate.Year − 1`；
- ACH-009 的 `AgeAt(当前月)` 取新月；ACH-037 的「亡者 `AgeAt(死亡月)`」取**事实里的死亡月**（不取新月）；
- 结局年月 = `state.CurrentDate`。

**理由**：spec.md Assumption 5 用「首次成立 = 当前年 102」钉死了「百年世家首次在 `CurrentDate.Year == 102`
时成立」（存续年数 101 > 100）。若把「当前月」解释为已结算月份（旧月），则首次成立会变成 103 ——
与 Assumption 5 冲突。这条口径 MUST 由一条测试直接钉住（`ChronicleTests` 断言
`SurvivedYears == EndYear - 1`，并有一条「年 102 首次成立」的用例）。

**替代方案与否决原因**：

- *方案 B：在 ⑩ 之前判定，使「当前月 = 已结算月」* —— 否决。除了破坏 Assumption 5，
  还要求判定器在结算器内部被调用（FR-035 禁止），且 FR-034 明确把判定排在「结算完成之后」。

**推论（写入契约）**：ACH-037 是全特性**唯一**一条使用「事实月份」而非「当前月」的年龄判据，
因为 `DeathFact` 携带死亡月、而 `AgeAt` 需要一个月份参数；用新月会算多一个月。

---

## R-07 绝嗣判据的实现面

**决定**：绝嗣 ⟺ `state.Family.RegisteredMembers.Count == 0`。
`Family.RegisteredMembers`（`src/KFL.Core/Entities/Family.cs:82-83`，谓词 `IsRegistered` `:385-386`）已按
007 的定义**排除已亡与外嫁归档者**，正是规格书 §15:630 与 spec.md FR-026 的「在册成员」口径。

**理由**：口径已经在 007 建立并被 `FamilyTests` 覆盖，008 **不得**再写一套「存活 + 未外嫁 + 未亡」的复合谓词 ——
那会形成第二个真源。`Family.HeadId` 的空悬**不是**判据（`LifecycleSnapshot.HeadBecameVacant`
在 `src/KFL.Rules/Family/LifecycleSnapshot.cs:50`，其注释 `:49` 逐字写着「**不等于绝嗣**」）。

**替代方案与否决原因**：

- *方案 B：`RegisteredMembers.Count == 0 ∧ Head == null`* —— 否决。007 的 `HeadId` 不变式是
  「无在册男性 ⇒ `HeadId == null`」而非反方向；零在册时 `HeadId` 必为 `null`，该合取项恒真，属噪声。
- *方案 C：另写「存活成员数 == 0」* —— 否决。`StatusFlag.Deceased` 与 `MarriedOut` 的归档时机由 007 的
  ⑧-f/h 决定，重写会引入第二个口径并可能把「已外嫁但仍在族谱」的人算成还在册。

---

## R-08 结局状态的载体

**决定**：新增 `src/KFL.Core/Entities/EndingState.cs`，挂为 `GameState.Ending` 属性（**不**挂到 `Family`）。

```csharp
public sealed class EndingState
{
    public bool IsEnded { get; }
    public GameDate? EndedAt { get; }
    public ChronicleSnapshot? Chronicle { get; }
    public void End(GameDate at, ChronicleSnapshot chronicle);   // 幂等：已结束即原样返回
    internal void Restore(bool isEnded, GameDate? endedAt, ChronicleSnapshot? chronicle);
}
```

**理由**：

1. 「族亡」是**存档级终局**，不是家族内部状态机的一格。挂 `Family` 会让 `Family` 同时承载
   「族的构成」与「族的历史终局」，且 `Family` 的既有不变量（辈分、家主、在册口径）会受到污染。
2. FR-036 把「结束状态」列为**存档形状变更**的独立项（与成就累计器并列），暗示它与累计器同级，
   而非 `FamilyLifecycleState` 的扩展。
3. 幂等 `End` 是关键：FR-029/FR-032 要求绝嗣只在**首次**成立时冻结编年史；若重复 `End` 允许覆盖，
   读档再推进一次会改写编年史，破坏 SC-011 的往返一致。

**替代方案与否决原因**：

- *方案 B：挂 `Family.Lifecycle`（007 的 `FamilyLifecycleState`）* —— 否决。
  007 的 `specs/007-family-lifecycle/data-model.md` §9「明确不包含」把绝嗣判定排除在外，
  且该聚合已有「不急」的簿记语义（`NextPurchaseOrdinal` 等），塞入终局会混淆两种生命周期。
- *方案 C：作为 `GameState` 上的三个标量字段（bool + GameDate + Chronicle）* —— 否决。
  三者的**联动不变量**（`IsEnded ⇔ EndedAt.HasValue ∧ Chronicle != null`）会退化为校验器职责，
  而 Core 的既有模式是「值合法性与联动由类型保证，校验器只做跨对象一致性」。

---

## R-09 编年史快照的族谱字段

**决定**：`ChronicleSnapshot.Genealogy` 只冻结**全量成员的 `PersonId` 序列**（含外嫁/已亡归档者，
按入册次序），**不复制** `Person` 的任何字段。

**理由**：

1. FR-028 ⑦ 的表述是「族谱（**数据面**）」——数据面已由 `Family.Members` 持久承载且含归档者；
   再复制一份 `Person` 快照会形成「两份可分歧的族谱」，违反章程「单一真源」与 AGENTS.md 的禁令。
2. 冻结的是**次序与成员集合**：`Family.Members` 在绝嗣后不再增长（终局后拒绝推进，R-16），
   因此 `Genealogy` 序列作为「顺序证物」就足以让 U2 复现族谱面板；
   绘制（按代分层、连线）是界面轨的事，不需要数据层预先算好。
3. 它也提供一条可校验的不变量：`Chronicle.Genealogy` MUST 等于
   `Family.Members.Select(p => p.Id)` —— 校验器可逐元素比对（契约二十三 §7）。

**替代方案与否决原因**：

- *方案 B：`Genealogy` 是 `IReadOnlyList<Person>` 深拷贝* —— 否决。Core 无深拷贝设施，
  且会让存档体积翻倍、并创造出「哪个 Person 才是真身」的问题。
- *方案 C：干脆不存 `Genealogy`，只存计数* —— 否决。FR-028 把族谱列为七项之一，
  且「哪些人算族谱」在归档语义下有歧义（`Members` 与 `RegisteredMembers` 不同），必须显式冻结。

---

## R-10 累计器的挂载方式（不改 `GameState` 构造形状）

**决定**：`GameState` 新增两个**内联初始化**的聚合属性，`ctor` 签名**不变**：

```csharp
public AchievementState Achievements { get; } = new AchievementState();
public EndingState Ending { get; } = new EndingState();
```

复原走 `internal` 通道（`AchievementState.Restore(...)` / `EndingState.Restore(...)`），
由 `SaveStateMapper` 在 `MapToDomain` 阶段调用 —— 与 007 的 `Family.Lifecycle.RestoreBookkeeping(...)` 同款。

**理由**：

1. `GameState` 的 ctor 当前是 6 参（`src/KFL.Core/Entities/GameState.cs:29`，`Id`/`CurrentDate`/`Difficulty`/
   `PendingDifficulty`/`Origin`/`Family`/`Economy` 由 `:71`~`:101` 的字段与 `GameStateTests`、
   `SaveStateMapper`、`NewGameSetup` 三处共同依赖）。加两个 ctor 参数会**强制**改动这三处以及所有
   直接构造 `GameState` 的测试；而 008 的真实耦合面只是「存档形状」。
2. 「新增聚合的值对象 + internal 复原通道」是 007 已验证的模式，
   `src/KFL.Infrastructure/Save/Serialization/SaveStateMapper.cs:38` / `:79` 就是这条通道的现存实例。
3. `IL` 侧不会看到 `null`：`Achievements`/`Ending` 恒非空，避免 67 条判据到处写 `?.`。

**替代方案与否决原因**：

- *方案 B：加两个 ctor 参数* —— 否决。见上；收益（构造期显式）小于改动面（三处生产代码 + 多处测试），
  且与 007 的既有模式不一致。
- *方案 C：`Achievements` 可空，`null` 表示「旧档无此段」* —— 否决。`Version` 闸门（R-14）已保证
  进入领域的存档**必然**带有该段（旧版一律拒收），可空性只会把「不可能态」变成「到处判空」。

---

## R-11 全局成就文件的格式

**决定**：`achievements.json` **恒为 JSON**，位于 `ISaveService.RootDirectory`（FR-017）。
Infrastructure 侧只实现 JSON 读写与解析，**不实现** MessagePack 侧。

**理由**：

1. 规格书 §14:542 把 `achievements.json` 定义为**跨存档、人类可读**的聚合文件（要跨存档合并与追加），
   与「存档本体」的双格式诉求不同：存档要紧凑与版本闸门，成就文件要可读与可合并。
2. 它**不受存档版本闸门管辖**，因此也不进 `SaveFileFormat`/`MessagePackSaveFormat` 的键表 ——
   这正是 R-14 里「版本自增」只覆盖存档本体的原因。
3. FR-019 要求「未知字段忽略、缺失取默认」，JSON 实现天然满足；MessagePack 侧需要额外的
   `TryReadNil` 与成员数容忍机制（004 契约里为此付出了不少条款），收益为零。

**替代方案与否决原因**：

- *方案 B：双格式，跟随当前 `SaveSlot.Format`* —— 否决。会让同一个玩家在不同格式下的成就文件
  不可互认，直接违反「任何难度可解锁」与跨存档合并的诉求（§14:534）。
- *方案 C：复用 004 的头记录（`FormatVersion` + `PayloadLength` + `SaveId`）* —— 否决。
  成就文件不是单存档，没有 `SaveId` 概念；套用头记录会引入一个恒假的版本闸门。

---

## R-12 损坏成就文件的隔离命名

**决定**：损坏的 `achievements.json` 改名为 `achievements.corrupt-<n>.json`（`n` 从 1 起，
取第一个未被占用的序号），随后以**空表**继续（并让调用方拿到诊断结果）。

**理由**：

1. G-07 的禁用 token 清单（`tests/KFL.Tests/Architecture/ArchitectureRules.cs:72-88`）里有
   `DateTime.Now` / `DateTime.UtcNow` / `DateTime.Today` / `DateTimeOffset.Now`，扫描范围包含
   `src\KFL.Infrastructure\`（`ArchitectureRules.cs:117-118`）。用时间戳命名会**直接触发守卫失败**。
2. 「递增序号」同时是无时钟依赖的、确定性的、可断言的：测试可以精确断言隔离后的文件名。
3. 不与既有隔离/备份命名冲突：004 的备份是 `backups/<存档名>.dat`（`ISaveService.Backup`），
   不在同一目录前缀下。

**替代方案与否决原因**：

- *方案 B：`achievements.corrupt.json`（固定名，边隔离边覆盖）* —— 否决。第二次损坏会覆盖第一份证物；
  且 SC-006 的测试无法区分「隔离成功」与「本来就存在」。
- *方案 C：直接删掉损坏文件* —— 否决。销毁用户数据；且掩盖了「曾经损坏」这一事实。

---

## R-13 全局落盘点的唯一性

**决定**：全局成就文件的**唯一写点**是 `TurnRunner.Advance` 的第 ⑤ 步（FR-034 的次序末位）。
动作型即时判定（R-17）**只**把解锁写入档内的 ⑦ 已完成集合与 ⑧ 待落盘队列，**不**触发全局落盘。

**理由**：

1. FR-034 的次序是「① 结算 → ② 成就判定与绝嗣判定 → ③ 覆盖写档 → ④ 备份 → ⑤ 全局成就落盘」。
   判定（含即时判定产生的队列增量）**先**写进档、**后**落全局 —— 这样即使 ⑤ 失败，
   队列已随档持久（③ 已完成），下次判定能重试（FR-024「待落盘队列保留在档内」）。
2. 若即时判定也落全局，就会出现「同一回合内两次写同一文件」的路径，
   使 SC-005 的原子性断言复杂化，并把失败语义分裂成两套（回合末失败 vs 动作失败）。
   `TurnRunner` 的既有 remarks（`src/KFL.Rules/Save/TurnRunner.cs:19-29`、`:77-78`）
   刻意不让 try/catch 维持次序，第二个写点会破坏「结构本身给出语义」的设计。
3. 已知的后果：玩家在「新局创建 → 立刻退出」的极端情况下，`achievements.json` 里还没有 ACH-001。
   这**不**违反任何 FR/SC（退出保存会写下含 ⑧ 的档，下次 `Advance` 的 ⑤ 补落）。
   该取舍登记于 plan.md Complexity Tracking。

**替代方案与否决原因**：

- *方案 B：即时判定后立即落全局* —— 否决。见上「两个写点」。
- *方案 C：`SaveOnExit` 也落全局* —— 否决。`SaveOnExit`（`src/KFL.Rules/Save/TurnRunner.cs:118`）
  当前只做 `_saveService?.Save(...)`；给它加全局写会引入第二个落盘点，
  且 `SaveOnExit` 在 G-07 意义下没有「回合」上下文，失败处理的语义无处安放。

---

## R-14 存档版本与键表扩展

**决定**：

- `src/KFL.Infrastructure/Save/SaveFileFormat.cs:32`：`public const int Version = 4;` → **`5`**。
- `SaveStateDto`：8 键 → **10 键**（`[Key(8)]` 成就累计器、`[Key(9)]` 结束状态，后者在未结束时为 `null`）。
- 新增 DTO：`AchievementStateDto`（8 键）、`AchievementMarkDto`（4 键）、
  `PendingUnlockDto`（5 键）、`EndingStateDto`（3 键）、`ChronicleDto`（7 键）。
- `MessagePackSaveFormat`：`SaveStateMemberCount` 8 → 10；新增 5 个成员数常量；
  `EnsureSaveStateShape` 的跳过序列与分支同步（现存 `Skip(ref reader, 6)` 处 → `Skip(ref reader, 8)`）。

**理由**：

1. 004 的版本闸门语义是「**恰好等于**当前版本才受理」（`SaveFailureTests.cs:156-157`、
   `:183-184` 以 `SaveFileFormat.Version + 1` 造拒绝用例）。因此新增持久字段**必须**自增版本，
   否则旧档会被误认为「可能有字段」而静默缺省。
2. 全部既有版本断言都以**常量**书写（`SaveFormatContractTests.cs:37`、`:56`、`:270`；
   `SaveStateValidatorTests.cs:356`；`SaveFailureTests.cs:156-157`、`:183-184`、`:477`、`:503`、`:585-586`），
   所以自增只改一处常量即可；**唯一**需要人工核对的是两处陈旧注释
   （`src/KFL.Infrastructure/Save/SaveFileHeader.cs:21`、`tests/KFL.Tests/Infrastructure/SaveFailureTests.cs:23`
   逐字写着「恒为 3」），它们 MUST 一并改成 5。
3. 结束状态用 `null` 表达「未结束」而不是 `[Key(9)] bool + 两个可空字段`：
   `MessagePackSaveFormat` 的既有形状里有「可空引用类型成员」的处理惯例（JSON 侧 MUST NOT 标
   `[JsonRequired]`，二进制侧用 `TryReadNil()`），复用它可以避免新增一种「半空」表示。

**替代方案与否决原因**：

- *方案 B：把结束状态塞进 `AchievementStateDto`* —— 否决。FR-036 把「结束状态」与「成就累计器」
  并列为两个形状变更项；塞一起会让「成就段恒存在」与「结束段可缺失」两种生命周期共用一张表。
- *方案 C：不动 `Version`，靠「字段缺失即默认」容忍旧档* —— 否决。004 的闸门是等值比较，
  改动它会推翻 004 的核心安全属性（旧档不得被静默升级），属跨特性回退。

---

## R-15 一致性校验新增条目的最小集

**决定**：`SaveStateValidator` 新增 **7** 条，全部是「跨对象/跨字段」的一致性，不含可重算派生值：

| # | 条目 | 依据 |
| --- | --- | --- |
| 1 | `Completed` 中 `(Id, Difficulty)` 唯一 | FR-011 幂等 |
| 2 | `PendingUnlocks ⊆ Completed`（按 `(Id, Difficulty)`） | FR-013 ⑦⑧ 的集合关系 |
| 3 | `Lifespans.Count <= Members.Count(Deceased)` 且每项 `>= 0`（**E-21**：上界，处决不记享年） | FR-013 ③ 的「完整序列」 |
| 4 | `SuccessfulBribeCount >= 0 ∧ BirthCount >= 0 ∧ DebugWakeCount >= 0 ∧ PeakTotalAssets >= 0` | 域合法性 |
| 5 | `IsEnded ⇔ EndedAt.HasValue ∧ Chronicle.HasValue` | R-08 的联动不变量 |
| 6 | `IsEnded` ⇒ `Family.RegisteredMembers.Count == 0` | FR-026/R-07 |
| 7 | `Chronicle.Genealogy` 逐元素等于 `Family.Members.Select(p => p.Id)` 且 `Chronicle.SurvivedYears == EndedAt.Year − 1` | R-09 / R-06 |

**理由**：

1. 既有校验器共 15 条（004 契约），其组织原则是「对象内部由类型保证、跨对象由校验器保证」。
   上表 7 条全部落在跨对象侧（唯一例外是第 4 条的域下界，与既有 `EnsureHighDegreeCap` 等同类）。
2. **刻意不校验**的项：`PeakTotalAssets` 是否等于某种重算值、`HighestDegree` 是否等于 `DegreeHistory` 的极大值、
   `TotalJinShi` 是否等于重数 —— 这些是**冻结的历史**，绝嗣后不可能重算
   （成员已被归档、功名记录仍在但「首达时点」不可复原）。把它们写成校验会在**合法档**上误报。
   这条「不校验什么」的清单 MUST 写进契约，防止后续实现「顺手补校验」。
3. 校验失败一律沿用既有表达：`SaveFileException(SaveFileErrorKind.InvalidStructure)`。

**替代方案与否决原因**：

- *方案 B：校验编年史每个派生值* —— 否决。见上第 2 点；且会与 R-09「族谱只是次序证物」的定位矛盾。
- *方案 C：不校验 `PendingUnlocks ⊆ Completed`* —— 否决。这是 ⑦⑧ 之间唯一的内部关系，
  一旦破坏，`Flush` 会把「未完成的成就」写进全局文件（SC-004 的最早日期会因此被污染）。

---

## R-16 已结束存档的拒绝路径

**决定**：`TurnRunner.Advance` 的**第一步**（早于 `ArgumentNullException.ThrowIfNull` 之后的任何随机消费与状态变更）
检查 `state.Ending.IsEnded`，成立则 `throw new InvalidOperationException(...)`。
`MonthlySettlementEngine.Settle`、`Judge`、`Save`、`Backup`、`Flush` 全不执行。

**理由**：

1. FR-029 要求「拒绝路径 MUST NOT 改变任何状态（不消费随机、不落账本条目、不写盘）」。
   唯一能保证这一点的位置是 `Advance` 的顶部 —— 一旦进入 ① `Settle`，随机已被消费。
2. 异常类型必须是**编排层**的 `InvalidOperationException`，不是 `SaveFileException` 或
   `ArgumentException`：这不是数据非法（状态合法，就是已结束），而是**调用顺序非法**。
3. SC-010 的测试写法是「`Advance` 抛 + `GameState` 逐字段快照比对 + 存档目录文件清单与时间戳不变」，
   三者都要求检查点在最前面。

**替代方案与否决原因**：

- *方案 B：返回一个 `TurnOutcome` 里的 `Refused` 标志而不抛* —— 否决。FR-029 明写「拒绝」
  （⇒ 异常），且静默返回会让调用方以为回合推进了。
- *方案 C：在 `GameState.AdvanceMonth` 里拦* —— 否决。⑩ 在 `Settle` 内部，随机已被消费；位置太晚。
- *方案 D：让 `Judge` 返回「已结束」并在 `TurnRunner` 里提前返回* —— 否决。位置太晚（① 已跑），
  且会让 ② 之后的 ③④⑤ 也跑不完，`TurnOutcome` 的语义变得依赖上下文。

---

## R-17 动作型即时判定的触发器清单

**决定**：`src/KFL.Rules/Achievements/AchievementTrigger.cs` 的 `public enum AchievementTrigger`，**12 项**。
调用点与顺序登记于契约二十二 §3，并作为 `FR-009` 清单的唯一副本（其他文档只引用不复制）。

| 触发器 | 调用点（既有面） |
| --- | --- |
| `NewGameCreated` | `src/KFL.Rules/Start/NewGameSetup.cs:99` `Create` 成功后 |
| `AssetPurchased` / `AssetSold` | 003 的资产买入/卖出入口 |
| `LoanOriginated` / `LoanRepaid` | 002 的借款发生与提前清偿入口 |
| `MatchmakingCommenced` | 007 的托媒入口（`LifecycleEntry`） |
| `ProposalAnswered` | 007 的提亲应答入口 |
| `DegreeMarriagePurchased` | 007 的买功名婚姻入口 |
| `PersonPurchased` | 007 的收养入口 |
| `BribeSubmitted` | 006 的行贿提交入口（`BribeEntry`） |
| `ExamInquiryAnswered` | 005 的科举询问应答入口 |
| `DebugWakeReported` | FR-041 ⑥ 的上报接缝（U4 将来调；本次只提供入口） |

**理由**：

1. FR-009 的清单是「至少覆盖」的**下限**，且明写「此清单 MUST 由契约登记，新增动作入口时必须同步」——
   因此它必须是枚举 + 一张表，而不是散落在各执行器里的注释。
2. 「捉婿提议产出」**不**列入触发器：R-04 已证明它由月末快照承载，
   把它做成触发器会要求 `MonthlySettlementEngine` 反向调用判定器（FR-035 禁止）。
   这条「为什么少一项」的说明 MUST 写进契约，否则后续读者会以为漏了。
3. `DebugWakeReported` 的调用点是**新增**的接缝（FR-041 ⑥），本次只定义入口与计数语义（⑥ 累计量），
   真实上报方是 U4 的调试控制台（Out of Scope）。

**替代方案与否决原因**：

- *方案 B：不做即时判定，全部等月末* —— 否决。FR-009 要求动作成功后立即触发；
  ACH-001（新局创建）若等月末，则「创建后立刻查看成就」永远为空。
- *方案 C：用 `string` 触发器名* —— 否决。见 R-02 的键语义理由。

---

## R-18 阈值常量的落点

**决定**：新增 `src/KFL.Rules/Config/AchievementPolicy.cs`（阈值唯一真源），
并在 `src/KFL.Rules/Config/GameConfig.System.cs` 新增 `Achievements` 分节**纯转发**。

**理由**：

1. `GameConfig` 的既有 remark（`src/KFL.Rules/Config/GameConfig.cs:14-18`）逐字规定
   「本文件只有转发，没有数值（章程「技术栈与工程约束」）」，并且 `:24-28` 的「明确不含」里**点名**了
   「尚未落地的阶段的数值（**成就与绝嗣阈值等**）」—— 即 008 的阈值被显式留给了本计划。
2. `ConfigLiteralRules.ScannedRoots` 覆盖 `src\KFL.Rules\` 全量，数值字面量只允许出现在
   `src\KFL.Rules\Config\`（`ConfigLiteralRules.cs:53`）内。因此 68 条判据里的所有数字（10000/50000/80/100/300/50/20/3/10/100/1000/10000 …）
   MUST 是 `AchievementPolicy` 的常量，判据与编年史代码里 MUST NOT 出现裸字面量。
3. 「配置单点」在代码里的实际机制**不是**注册表类型，而是「每张表一个 static 策略类 + `GameConfig` 转发」
   （仓库内 `grep ConfigRegistry` 零命中）—— 本计划沿用该机制，不发明注册表。

**替代方案与否决原因**：

- *方案 B：把阈值写在 `AchievementCatalog` 的定义表里* —— 否决。定义表在
  `src\KFL.Rules\Achievements\`，会被配置字面量守卫（章程「技术栈与工程约束」）扫描并判违规。
- *方案 C：新建配置注册表类型* —— 否决。仓库无此机制，且会与 007 的
  `contracts/config-registry.md`（契约二十一，逐成员转发）冲突。

---

## R-19 待接线成就的接缝形状

**决定**：三条待接线/待上报项（ACH-041 商海浮沉、ACH-064~068 调试秘辛系列，以及 ACH-047 需要的全局读面）
在本次只落**接缝**，不落真实触发者：

| 成就 | 接缝 |
| --- | --- |
| ACH-041 商海浮沉 | 谓词 `IsBusinessLoss(context)` 读「经商亏损事件」事实；本次无生产者（规格书未定义该事件），故恒 false 且有一条测试断言「未接线 ⇒ 不解锁」 |
| ACH-064~068 调试秘辛 | `AchievementTrigger.DebugWakeReported` + ⑥ 累计量 + `AchievementAdjudicator.Report` 的公开入口；真实上报方为 U4 |
| ACH-047 四民皆历 | 读取方由 `IAchievementStore.Read` 提供出身达成段；本次实现读面与判据，不做界面 |

**理由**：FR-041~FR-043 要求「留接缝 + 不得靠假数据解锁」。恒 false 的谓词 + 明确的入口
是最诚实的形状：接缝存在、可测、且在没有生产者时不会误解锁。

**替代方案与否决原因**：

- *方案 B：直接删掉 ACH-041，等规格书补事件后再加* —— 否决。会破坏「68 条齐全」的目录表断言
  （R-02 的完整性测试），且规格书 §14.1 已列出该成就。
- *方案 C：为 ACH-041 创建一个假的「亏损事件」事实类型* —— 否决。
  那是在规则层发明规格书没有的事件来源（§17 禁止自创影响平衡的规则），
  且 violates FR-042 的「不得靠假数据解锁」精神。

---

## R-20 与既有测试的冲突面

**决定**：本次改动触碰的既有测试面**只有四处**，且全部以常量或新增用例的形式吸收：

| 冲突面 | 处理 |
| --- | --- |
| `SaveFileFormat.Version` 4 → 5 | 无测试改动（断言已用常量）；改两处陈旧注释 |
| `SaveStateDto` 8 → 10 键 | 无既有断言绑定键数（成员数常量由 `MessagePackSaveFormat` 内部使用），只需同步常量 |
| `TurnRunner` ctor 增一个可选参 `IAchievementStore?` | `RulesTestHarness` / `SaveCadenceTests` 的既有构造点若用位置参数需补 `null`；以可选参数默认 `null` 使调用点**不必**改动 |
| 新增运行期依赖 `IAchievementStore` 为 `null` 时 | 与 `ISaveService?` 同款：`null` ⇒ **不落全局文件**，判定仍照跑（内存内），故既有「无存档服务」的回合测试行为不变 |

**理由**：008 是**最后一条逻辑轨**，其价值有一半在于「不改坏前七条」。把冲突面钉成一张表
（而非事后发现）是 007 plan.md 已验证的做法。

**替代方案与否决原因**：

- *方案 B：`IAchievementStore` 必填* —— 否决。会让所有既有 `TurnRunner` 构造点与
  「无存档服务」路径的测试全部需要改动，收益仅是「不可能为 null」，
  而该不可能性无法在 `KFL.Rules` 内保证（Infrastructure 的 store 需要目录）。

---

## R-21 规格书 §17 裁决块

**决定**：本次执行 MUST 在《科举浮生录规格书》§17 追加一个裁决块
「**裁决块：2026-10-18（逻辑轨 ⑧「成就与绝嗣结局」的空白填充）**」，
承载 spec.md 的 Q1~Q3 与次级口径 A~D 中**规格书原文有歧义或未定义**的部分。

**待写入裁决块的条目**（逐条已在 spec.md 定稿，此处只登记「哪几条属于规格书空白」）：

1. **平均寿命的统计量**：规格书 §15:639 只写「平均寿命」，未定义统计量与舍入 → 裁决为三均值 + 3 位小数。
2. **累计量的持久集**：规格书未定义跨月累计的持久集 → 裁决为 8 项闭集（FR-013）。
3. **「存续年数」的起算**：规格书 §14.1 用「超过 100 年 / 300 年」但未定义起算点 →
   裁决为 `CurrentDate.Year − 1`，并给出首次成立年（102 / 302）。
4. **ACH-041 的事件缺口**：规格书 §14.1:604 列了「商海浮沉」，但经商亏损事件在规格书内**未定义** →
   裁决为「留接缝、本次不解锁」。

**理由**：规格书 §17 的规则是「① 同节类似规则 → ② 最直观 → ③ 暂停提问」，且
「禁止自创影响平衡核心数值」。上表 4 条中没有任何一条引入新的**平衡数值**（三均值、年数起算、
闭集大小都不是玩法数值），因此它们可以自行裁决并回写；而 ACH-041 涉及一个**尚未定义的事件**，
按 §17 ③ 本应「暂停提问」—— 但 spec.md 的 Q1 已经代为用户裁决，
故裁决块里以「接缝化」表达，并保留 U 系列日后补齐的入口。

**替代方案与否决原因**：

- *方案 B：不追加裁决块，只改 §14/§15 正文* —— 否决。§17 的既有形态是「追加裁决块」，
  且 AGENTS.md 要求「规格书变更 MUST 与受影响的 spec/plan/tasks 同步提交」；
  裁决块是变更的**授权凭据**，正文改写是**结果**，两者都要有。
- *方案 C：把 4 条全写进正文，不留裁决块* —— 否决。会丢失「当时为什么这么裁」的记录，
  后续读者无法区分「规格书原文」与「009+ 的填充」。

## 未决事项

**无。**

- `R-01` ~ `R-21` 每项都给出了**唯一决策**与**被否替代**，没有「待定 / 二选一 / 需要实测」的条目。
- `spec.md` 的 `[NEEDS CLARIFICATION]` 计数为 0（`checklists/requirements.md` 16/16 通过），
  Q1~Q3 与次级口径 A~D 均已由用户裁决并回填 `spec.md`。
- R-21 登记的 4 条「规格书空白」**不是**未决的设计问题：它们的**设计**已由 `spec.md` 定稿，
  悬空的只是《科举浮生录规格书》§17 的**回写动作**（FR-039）。该动作属文档义务，
  由 `tasks.md` 按 AGENTS.md 铁律登记回写范围后在实施阶段完成，**不阻塞**本计划。
- 未引入任何需要实测才能定的技术选型：本特性零新增依赖（FR-005），
  只复用既有的 `MessagePack` 2.5.305 与 BCL `System.Text.Json`。
