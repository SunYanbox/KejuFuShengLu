# 阶段 0 研究：科举周期（逻辑轨 ⑤）的技术抉择

**Feature**: `005-exam-cycle` | **Date**: 2026-10-12 | **Plan**: [plan.md](./plan.md) | **Spec**: [spec.md](./spec.md)

> 前置：`spec.md` 的三项裁决（Q1 殿试必中 / Q2 报名＝考试当月 / Q3 每科年询问一次特奏名，用户
> 2026-10-12 全部取 A 档）已按规格书 §17 回写规格书 §3 / §6 / §7.1。本文件只解决**设计选择**，
> **不改任何数值**：全部数值逐字取自规格书 §6 / §7.1 / §7.2 / §8.2 / §11，登记见
> [契约十四](./contracts/config-registry.md)。

层与代号：`KFL.Core` = 领域层，`KFL.Infrastructure` = 存储层，`KFL.Rules` = 规则层。
「契约N」指 `specs/*/contracts/` 下的既有契约文号（契约一/二 = 001，契约三/四/五 = 002，
契约六/七/八 = 003，契约九/十/十一 = 004，契约十二/十三/十四 = 本特性）。

---

## R-01 考试月派生的落点与形状

**Decision**: 新建 `KFL.Rules/Exam/ExamCalendar.cs`（静态纯函数类），三个成员：

```csharp
public static bool IsKeYear(int year);            // year >= 1 && year % 3 == 1
public static ExamLevel? LevelOf(GameDate date);  // 该月考试级别；非考试月 ⇒ null
public static GameDate NextExamMonth(GameDate date);  // 严格晚于 date 的第一个考试月
```

科年与考试月是**纯派生值，MUST NOT 持久化**（FR-001）。

**Rationale**: 「三年一科」是玩法规则，不是通用时间算术——按 002 R-09 的同一口径（`GameDate` 只
承载年月算术，玩法派生留在 `KFL.Rules`），把它放在规则层才能被单测直接钉住且被 U1/U4 共用。
不持久化避免「时间同步维护的第二真源」（FR-001 末句）。

**Alternatives considered**: ① 做成 `GameDate.NextExamMonth()` 实例方法——被否：`GameDate` 是
001 的通用时间值对象，把 `mod 3` 混进去会让「日期」这个抽象同时承载玩法，后续阶段改周期就要动
`KFL.Core`；② 在每个调用点各写 `% 3`——被否：复制规则（章程原则 VI），且 U1 的暂停点与结算步
会各自演化。

---

## R-02 考试级别枚举的形状与取值

**Decision**: 新建 `KFL.Core/Enums/ExamLevel.cs`：

```csharp
public enum ExamLevel { JieShi = 1, ShengShi = 2, DianShi = 3 }
```

**不设** `None = 0`：「本月无考试」用 `ExamLevel?` 的 `null` 表达。`0` 与 `> 3` 都不是具名成员，
故二进制存档里的越界整数会在复原第 ⑦ 步被 `Enum.IsDefined` 拒绝（004 契约十 §6 条款 6、004
data-model §3.10）。

**Rationale**: 与 `DegreeLevel`（白身 = `BaiShen` = 0 是「无」的**具名**状态）不同，考试级别没有
「无」这个合法取值——报名标记只用 解试/省试 两态，「本月无考试」是**查询结果**而不是状态取值。
`null` 比 `None` 更精确，并让「具名成员集合 = 1..3」成为可检出的结构性质。

**Alternatives considered**: ① `{ None, JieShi, ShengShi, DianShi }`——被否：`None = 0` 会让
`default(ExamLevel)` 变成合法值，掩盖「字段缺失/未初始化」；② 复用 `AppointmentTrack` 或
`ImperialClass`——被否：它们是入仕途径与甲第，语义不同且已各有值域。

**命名口径**：拼音（`JieShi` / `ShengShi` / `DianShi`）与 `DegreeLevel`（`BaiShen` / `JuRen` /
`GongShi` / `JinShi`）一致——这三个词在英文里没有不致歧义的对应（省试的「省」指尚书省，不是
province；解试/殿试亦然）。

---

## R-03 三个新 `Person` 字段的写入通道

**Decision**: `Person` 新增三个**只读属性**（`private set`）：

```csharp
public int JieShiFailureCount { get; private set; }      // >= 0，单调递增
public int ShengShiFailureCount { get; private set; }    // >= 0，单调递增
public ExamLevel? AppliedExam { get; private set; }      // null 或 解试/省试
```

公开写入通道只有三个受控方法：

```csharp
public void ApplyForExam(ExamLevel level);   // 仅 解试/省试；幂等（已报名同一级别 ⇒ no-op）；否则 InvalidOperationException
public void ClearExamApplication();          // null ⇒ no-op（幂等）
public void RecordExamFailure(ExamLevel level);  // 仅 解试/省试；对应计数 +1；无上限
```

复原走 `internal void RestoreExamProgress(int jieShiFailureCount, int shengShiFailureCount, ExamLevel? appliedExam)`
——`KFL.Core/AssemblyInfo.cs` 已开的**唯一**程序集面 `InternalsVisibleTo("KFL.Infrastructure")`
即为此用（004 R-03 的同款通道）。

**Rationale**: FR-023 明令 MUST NOT 新增**无约束**的公开 setter；001 不变量 2 的口径是「有写入
通道的成员必须把通道钉在具体语义上」（`AppendDegree` 是范本）。`private set` 让
`PropertyInfo.GetSetMethod()` 返回 `null`，与 001 既有的「无公开 setter」反射断言同形，测试只需
把新成员加进该集合。

**Alternatives considered**: ① 公开 setter + setter 内校验——被否：`AppliedExam = ExamLevel.DianShi`
这类非法态会变成**可表达**的状态，而它正是 Q2 裁决排除的东西（殿试不需报名）；② 把三者合成一个
值对象 `ExamProgress`（仿 `StatusTimers`）——被否（当下）：`StatusTimers` 之所以是值对象，是因为
四个计时与八个状态位**成对**同生同灭、跨字段才是它的不变量；此处三者的耦合只到「级别取值域」
一层，包成对象只会多一层解包而不增加任何可断言的不变量（章程「复杂度 MUST 被论证」）。

---

## R-04 `Family` 的持续报考开关

**Decision**: `Family` 新增 `public bool AutoExamApplication { get; set; }`（默认 `false`），落
`FamilyDto` 键 6。文档口径写明「持续报考开关（规格书 §3、FR-017）」。

**Rationale**: FR-017 明令是**家族级布尔**，与 `HasShiStatus` 同为可写属性（001 已确立该形状）；
不引入新枚举——两态布尔不需要类型。

**Alternatives considered**: 用 `bool?` 表达「未设置」——被否：规格书的开关只有开/关两态，
`null` 会凭空造出第三态与「未设置如何解释」的新问题。

---

## R-05 资格与报名入口的落点

**Decision**: 新建 `KFL.Rules/Exam/ExamRegistration.cs`（静态类）：

```csharp
public static ExamLevel? TargetLevelOf(Person person);            // 白身→解试、举人→省试、贡士→殿试、进士→null
public static bool IsEligible(Person person, GameDate date);      // FR-002 ∧ FR-003
public static bool CanRegister(Person person, GameDate date);     // 窗口 ∧ 资格 ∧ 级别匹配 ∧ 非殿试
public static void Register(Person person, GameDate date);        // !CanRegister ⇒ InvalidOperationException，MUST NOT 改任何字段
public static IReadOnlyList<Person> EligibleUnapplied(Family family, GameDate date);   // 纯查询
public static bool SpecialTributeEligible(Person person, GameDate date);
public static void AcceptSpecialTribute(Person person, GameDate date, IRandomService random, GameState? state = null);   // state 为 008 追加的可选尾参（T055/E-20），传入时上报成就触发器
```

「在册」复用既有的 `CountedMembers.IsRegistered(Person)`（002 已定义、唯一出处），不新写一份。

**Rationale**: FR-002/003/004/006/012/018 需要**同一份**资格判定；把它钉在一处，U1（快进暂停的
名单）、U4（跳至下一科举）与结算步共用同一实现，MUST NOT 各写一份（章程原则 III、原则 VI）。

**Alternatives considered**: 塞进 `ExamCalendar`——被否：日历是纯时间函数、与成员无关，合在一起
会让每个日期测试都要构造 `Person`；放进结算步——被否：报名发生在**两次结算之间**（Q2 裁决），
它不是结算的一部分。

---

## R-06 录取率/进甲率的纯函数入口与贿赂乘数接缝

**Decision**: 新建 `KFL.Rules/Exam/ExamAdmission.cs`（静态类）：

```csharp
public static decimal AdmissionRate(ExamLevel level, Person person, bool hasShiStatus, decimal bribeMultiplier);
public static decimal ImperialPromotionRate(Person person, decimal bribeMultiplier);
public static ImperialClass ImperialClassFor(Person person, bool promoted);
public static ImperialPlacement RollPlacement(IRandomService random);   // Next(1, 4) → 状元/榜眼/探花
```

`AdmissionRate` 只接受 解试/省试（传入 `DianShi` ⇒ `ArgumentOutOfRangeException`）。005 当时全部调用方
一律传 `ExamPolicy.NeutralBribeMultiplier`（= k⁰ = 1.0）。

> **006 后记（2026-10-13）**：占位 `ExamPolicy.NeutralBribeMultiplier` 已随 006 落地**删除**；
> 两处调用点（`ExamSeasonRunner` 的解试/省试与殿试分支）改为传
> `BribeRules.MultiplierOf(level, person)`（`kⁿ`，`k⁰` 由 `BribePolicy.UnitMultiplier = 1m` 唯一给出）。
> `AdmissionRate` / `ImperialPromotionRate` 的形参签名与 clamp **逐字未变**。

**Rationale**: FR-016 要求逻辑轨 ⑥ 落地时经**同一入口**传入实际乘数——把它做成**显式形参**即可，
公式内部 MUST NOT 读任何全局态。封顶在函数内部 clamp，调用方**没有分支可以绕过**（FR-015、
章程原则 VI）。

**Alternatives considered**: ① 把乘数放进 `ExamPolicy` 的静态可变字段——被否：静态可变状态会破坏
可复现性与并行测试，并违反章程原则 II 的「规则逻辑 MUST 表达为纯函数」；② 让入口自己收
`IRandomService` 并掷骰——被否：概率计算与掷骰分离才能逐乘区断言（SC-002 的「封印」要求）。

---

## R-07 科举季步的形状与位置

**Decision**: 新建 `KFL.Rules/Exam/ExamSeasonRunner.cs`：

```csharp
public static ExamSeasonSnapshot Run(GameState state, IRandomService random);
```

`MonthlySettlementEngine.Settle` 在**第 ⑥ 步（贷款）之后、`GameState.AdvanceMonth()` 之前**调用它，
成为新的**第 ⑦ 步**；`AdvanceMonth` 顺移为第 ⑧ 步。

**Rationale**: FR-019 逐字规定位置与内部次序；与既有 `OfficialCareerAdvance.Run(Family, GameDate,
IRandomService)` 的编排同形（规则层的可变编排 + 随机经参数注入）。取 `GameState` 而非
`(Family, GameDate, Difficulty)`，是因为本步同时需要 `CurrentDate`（月）、`Difficulty`（路费档）、
`Family`（成员与开关）与 `Economy`（现金与账本），拆成四个形参会把「一次结算的上下文」拆散。

**Alternatives considered**: ① 放到 `TurnRunner` 里、结算之外——被否：结算顺序 MUST 是契约三 §1
的一步，否则「同种子 → 逐位相同」无法按**整月**钉死随机次序；② 挂进 `IncomeCalculator` /
`LoanSettlement` 等既有步——被否：会污染既有步的 `MUST NOT 存在` 清单（契约三 §1 明列科举季）。

---

## R-08 科举季的内部次序与整月随机消费次序

**Decision**: 步内固定次序（FR-019）：

```
① 自动报名（仅当持续报考 = 开；仅解试/省试月）→ ② 路费扣除与弃考（仅省试月）→ ③ 免解行使（仅解试月）
→ ④ 录取/进甲判定（按 PersonId 升序逐人）→ ⑤ 及第入仕（按 PersonId 升序）→ ⑥ 特奏名询问（仅省试月）
```

整月随机消费次序固定为：

```
米价（②）→ 考课掷骰（③-d）→ 储蓄利率（仅 1 月，④）→ 贷款计息利率（⑥）
→ 科举判定（逐人：1 次 NextDouble() 录取/进甲；进甲者紧接 1 次 Next(1, 4) 名次）
→ 及第入仕的待阙掷骰（1 次 Next(6, 25)，按 PersonId 升序）
```

**报名阶段 MUST NOT 消耗随机**；被跳过（不合格、现金不足）的成员 MUST NOT 消耗随机。

**Rationale**: FR-019 逐条；**免解必须在掷骰之前**——否则 SC-004 要求的「随机消费次数 = 0」不成立。
「先全部判定、再全部入仕」而不是「逐人判定+入仕」，是为了让随机序列可以按「阶段 → 成员」两段
断言（与 003 把待阙时长随机归入入口、不混进月度步骤的取舍一致）。

**Alternatives considered**: 逐人「判定 → 立刻入仕」——被否：同为升序却把两类随机交错，单测只能
断言总次数而不能逐段定位。

---

## R-09 省试路费的扣付口径

**Decision**: 判定前，对每位复核通过的举人：

- `Treasury.Cash < 路费` ⇒ **弃考**：清报名标记、不掷骰、不计败、不落账本条目、不转贷款，并在
  快照里落一条 `ExamAbsenceReason.InsufficientTravelCost`；
- 否则调用 `economy.Apply(LedgerCategory.ExamTravelCost, person.Id, -cost, month)` 落一条**资金类、
  恒为负**的条目。因 `Cash >= 路费`，`Apply` 的「现金 → 储蓄」扣付**必然只动现金**，MUST NOT 动
  储蓄、MUST NOT 转贷款。

**Rationale**: 规格书 §3 与 FR-008 的门槛字面是「**现金**不足」；而 `FamilyEconomy.Apply` 的既有
实现按「现金 → 储蓄」扣付、现金足够时不碰储蓄（`PayFromCashThenSavings`）。因此「门槛取现金、
扣付走 `Apply`」同时满足字面口径与「复用唯一支付通道」——无需给 `KFL.Core` 增加「只扣现金」的
新 API（章程「复杂度 MUST 被论证」）。落第不退由「判定前扣、事后不补」直接成立。

**Alternatives considered**: 用 `PaymentPrimitive`（现金 → 储蓄 → **余额转贷款**）——被否：FR-008
明令「不转贷款」，且它会把付不起路费变成一笔新负债（规格书未定义这种负债）。

---

## R-10 结算快照子段的形状

**Decision**: 新建 `KFL.Rules/Exam/ExamSeasonSnapshot.cs`，含快照主体与四个行记录 + 一个缺席原因
枚举（形状见 [契约十三 §9](./contracts/exam-session.md#9-快照与断言面)）；`SettlementResult` 新增：

```csharp
public required ExamSeasonSnapshot ExamSeason { get; init; }
```

**Rationale**: FR-022 明令「作为 `SettlementResult` 的新增子段」，与 003 的
`SettlementResult.Career`（`CareerAdvanceResult`）同款：**增量、只读、MUST NOT 写回
`GameState`、MUST NOT 成为第二真源**。快照必须能表达「报名与弃考 / 免解 / 中第落第 / 甲第与名次 /
失败数变化 / 待阙月数 / 特奏名询问 / `HasShiStatus` 置位」八项，因为其中多数**不产生账本条目**。

**Alternatives considered**: 只靠 `SettlementResult.Entries` 表达——被否：报名、免解、特奏名询问、
功名变迁都不产生账本条目，而账本也**不是**功名判定的真源（001 的口径：功名变迁史才是）。

---

## R-11 落盘形状与格式版本提升

**Decision**:

| 位置 | 变更 |
| --- | --- |
| `FamilyDto` | 新增键 **6** `autoExamApplication`(`bool`)；成员数常量 6 → **7** |
| `PersonDto` | 新增键 **22** `jieShiFailureCount`(`int`)、**23** `shengShiFailureCount`(`int`)、**24** `appliedExam`(`ExamLevel?`)；成员数常量 22 → **25** |
| `SaveFileFormat.Version` | **1 → 2**；`< 2` 与 `> 2` 一律 `UnsupportedVersion`（拒绝而非迁移） |
| 同步物 | `MessagePackSaveFormat` 的 `*MemberCount` 与 `EnsurePersonShape`/`EnsureFamilyShape`、`SaveStateMapper` 双向映射与复原次序、`SaveStateComparer.AssertPerson/AssertFamily`、`SaveStateValidator` 新增第 ⑩ 条、`SaveFormatContractTests.BinaryKeyTables`/`ContractKeyTable`、`SaveFailureTests` 的两条版本闸门用例（旧版 **1** 拒绝 / 更新版 **3** 拒绝） |

**Rationale**: FR-024。位置式数组（`[Key]` 从 0 连续）意味着「加字段 = 改线上形状」，而 004 的
版本闸门是「**拒绝**而非迁移」（004 契约十 §6 条款 2），因此必须升版；升版后旧档被明确拒绝，
正是 004 的既定口径。键一律**追加在末尾**（22/23/24、6），既有键的序号与键名一字不动。

**Alternatives considered**: 不升版、把新字段当可选尾随成员——被否：004 契约十 §5 条款 3 明令
「任一字段的缺失 MUST 被检出，MUST NOT 静默默认化」，可选字段会让旧档静默读成默认值（
`appliedExam` = 「没报名」这类**看似合法**的错值）。

---

## R-12 账本类别与元数据

**Decision**: `LedgerCategory` 追加第 **20** 个取值 `ExamTravelCost`（省试路费），声明序置于**末尾**
（`FamineResolved` 之后）；`LedgerCategoryMetadata.KindOf` → `LedgerEntryKind.Treasury`（资金类），
`CreditTargetOf` → `null`（恒为负）；同步两处异常文案与两处 `<exception>` 文档的「19」→「20」，
以及 `LedgerInvariantTests` 的三处穷举断言（19 元 `expected` 数组、`[Theory]` 的 `[InlineData]`
一行、「无正额入账目标」的 9 → 10 元数组）。

**Rationale**: FR-021。「资金类、恒为负」恰好就是既有元数据的既有含义（`KindOf == Treasury` ∧
`CreditTargetOf == null`，002 契约四 §1），不需要任何新概念。声明序末位使既有 19 个取值的二进制
整数不变——虽然 1 → 2 升版已拒绝旧档，但「追加在末尾」的 diff 与审计成本最低。

**Alternatives considered**: 插到 `LivingCost` 附近的支出类分组里——被否：会平移 18 个既有取值的
整数，diff 与审计成本都更大，且没有任何收益。

---

## R-13 数值归属与字面量登记

**Decision**: 新建 `src/KFL.Rules/Config/ExamPolicy.cs`，用 `public const` 单点声明 FR-014 的全部
数值（清单见 [契约十四 §1](./contracts/config-registry.md#1-科举数值登记)）；`GameConfig` 只增一个
`Exam` **转发视图**、MUST NOT 自带数值字面量。`ConfigLiteralTests.RegisteredValues` 增补 **4 项
新小数**：

```csharp
0.01m,      // 解试基础录取率 1%（§6）
0.05m,      // 殿试基础进甲率 5%（§6）
0.012m,     // 学业系数系数项（§6）
0.899999m,  // 需掷骰概率的封顶（§7.1）
```

**MUST NOT 重复登记**（值与既有条目相同）：省试基础率 `0.10m` 与田租 `0.1m`、学业系数基数
`0.4m` 与米价派生项 `0.4m`、仕族加成 `1.1m` 与难度支出系数 `1.1m`。**整数规则值一律不入单**：
`14` / `400` / `50` / `3` / `50` / `6` / `10` / `20` / `40` / `80`。贿赂乘数 `1.0m` 是整数值，不入单。

**Rationale**: SC-008 与契约五 §5 的两条互补判据——「登记表数值只出现在配置类的常量声明处」+
「产品代码的金额字面量只经配置成员取得」。漏登记的后果是**假绿**（003 契约八 §3 条款 3 的实测
口径），故本表必须与 `ExamPolicy` 逐项对照。

**Alternatives considered**: 把日历/概率/路费拆成三个配置类——被否：FR-014 要求「全部考试数值」
单点声明，拆开就会出现「哪一类归哪个文件」的新争议。

---

## R-14 测试组织

**Decision**: `tests/KFL.Tests/Rules/` 新增 5 个文件——

| 文件 | 覆盖 |
| --- | --- |
| `ExamCalendarTests.cs` | FR-001、FR-018 ①②；SC-007 前半（连续 3 个科年含跨年边界） |
| `ExamRegistrationTests.cs` | FR-002~FR-006、FR-013、FR-017、FR-018③、FR-012 的接受面；SC-007 后半 |
| `ExamAdmissionTests.cs` | FR-007~FR-010、FR-015、FR-016；SC-002/003/004 |
| `ExamSeasonTests.cs` | FR-019~FR-022、FR-027；SC-001/005/006 |
| `ExamSpecialTributeTests.cs` | FR-012 的判定与拒绝面；SC-005 |

并同步 `Rules/SpecAnchorTests.cs`（科举数值锚点）、`Core/LedgerInvariantTests.cs`、
`Architecture/ConfigLiteralTests.cs`、`Infrastructure/{SaveFixtures,SaveStateComparer,
SaveFormatContractTests,SaveFailureTests}.cs`。

**Rationale**: 章程原则 IV（规则改动 MUST 同提交给出测试）；硬约束是「全部经 `IRandomService` 注入、
无界面/文件系统/网络、MUST NOT 访问系统时钟或全局随机」（G-07）。`RulesTestHarness.FixedRandomService`
（可设种子/可排队列）是本特性的主随机来源。

**Alternatives considered**: 把五个文件合成一个 `ExamCycleTests.cs`——被否：本特性跨越「日历 /
报名 / 概率 / 结算编排 / 兜底机制」五个正交关注点，合起来会让失败定位退化（002/003 的既有划分
也是按关注点分文件）。

---

## R-15 不引入新工程、新接缝层与界面代码

**Decision**: 六个工程不变（G-02）；新增类型全部落在既有 `KFL.Core`（1 个枚举、3 个 `Person`
成员、1 个 `Family` 成员、1 个账本类别）与 `KFL.Rules`（`Exam/` 5 个文件 + `Config/ExamPolicy.cs`）；
MUST NOT 在 `KFL.Core` / `KFL.Infrastructure` / `KFL.Rules` 之外定义任何结算接缝接口。

**Rationale**: FR-029 把界面、贿赂、成长、成就逐条排除；且架构守卫后续要断言「除 `KFL.Rules`
外无层定义结算接缝接口」——本设计不触碰该约束。

**Alternatives considered**: 为「快进暂停」造一个 `IExamSignal` / `IEventBus` 抽象——被否：U1 只需
`ExamCalendar` 的纯查询（FR-018），为假想的未来需求预置结构违反章程「复杂度 MUST 被论证」，
`IEventBus` 也仍列在 001 契约二的「等各自阶段带着真实需求回来定义」清单里，本特性**没有**该需求。

---

## 未决事项

无。三项口径冲突已由用户在 2026-10-12 裁决（见 `spec.md` 的 `## Clarifications`），其余选择全部
落在既有契约（契约三 §1 的步骤序列例外已由 FR-028 登记回写）与既有实现的约束内；本文件**未引入
任何规格书之外的数值**。
