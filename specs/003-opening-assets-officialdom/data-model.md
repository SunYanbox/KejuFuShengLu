# 阶段 1 数据模型：开局资产与官吏体系

**Feature**: `003-opening-assets-officialdom` | **Date**: 2026-10-08

本文件是**落地蓝图**：新增/变更的类型、字段、不变量与状态转移。数值一律引
`KFL.Rules/Config/` 的配置成员与规格书章节号，本文件 MUST NOT 另立数值。
抉择的依据见 [research.md](./research.md)（R-01~R-19），接口次序见 [contracts/](./contracts/)。

**分层口径不变**：`KFL.Core`（状态与自身不变量）← `KFL.Infrastructure`（接缝与实现）←
`KFL.Rules`（编排与全部数值）。本特性在三个工程内均**不新增引用边**（G-05 不变）。

---

## 1. 值类型与枚举（`KFL.Core`）

### 1.1 `GameDate`（readonly record struct）——【改】年份下界放宽

| 成员 | 变更 | 说明 |
| --- | --- | --- |
| `Year` | 语义扩展 | 接受 `int` 全域（含 `0` 与负数）= **前史纪年**；`GameState.CurrentDate` 仍从 1 年 1 月起 |
| `Month` | 不变 | MUST 在 1~12（唯一的构造期校验之一，规格书 §3「无闰月」） |
| `ElapsedMonths` / `AgeInYearsAt` / 比较运算符 | 不变 | 均为纯算术；`AgeInYearsAt` 仍拒绝「出生之前」的查询 |

**不变量**：① `Month ∈ [1, 12]`；② 年份**无**构造期约束（R-01、Q4 裁决）。
**明确不含**：出生不得早于元年（该约束已被裁决删除）、任何日历换算（无闰月、无星期）。

### 1.2 `StatusTimers`（readonly record struct）——【改】第 4 个计时

| 字段 | 类型 | 对应的状态位 | 说明 |
| --- | --- | --- | --- |
| `SentenceRemainingMonths` | `int?` | `ServingSentence` | 既有 |
| `ExamBanRemainingMonths` | `int?` | `ExamBanned` | 既有 |
| `PromotionBanRemainingMonths` | `int?` | `PromotionBanned` | 既有；本特性**只消费**，不递减（逻辑轨 ⑥） |
| `AwaitingPostRemainingMonths` | `int?` | `AwaitingPost` | **【新】** 待阙剩余月数；递减到 **0** 的当月授官，且 MUST NOT 以「0 + 状态位为真」的形态存续（FR-013） |

**不变量**：① 四个字段非空时 MUST `>= 0`；② 仅在对应状态位为真时可非空
（由 `Person.Timers` / `Person.Status` 交叉校验，见 §2.1）。
**明确不含**：递减逻辑（归 `OfficialCareerAdvance`）、6~24 的区间（归 `OfficialCareerPolicy`）。

### 1.3 `DegreeRecord`（readonly record struct）——【改】甲第字段

```csharp
public DegreeRecord(
    DegreeLevel level,
    ImperialPlacement? placement,
    GameDate changedAt,
    DegreeChangeCause cause,
    ImperialClass? imperialClass = null)   // 【新】第 5 个成员，可选
```

| 成员 | 说明 |
| --- | --- |
| `Level` / `Placement` / `ChangedAt` / `Cause` | 既有，语义不变 |
| `Class`（`ImperialClass?`） | **【新】** 甲第；`null` = 该记录**未表达**甲第（001 时代的记录、或科举尚未产出的中间态）。二甲/三甲的**唯一判据** |

**构造期不变量**（违反即抛 `ArgumentException`，R-02）：

| № | 不变量 | 依据 |
| --- | --- | --- |
| ① | `Class is not null` ⇒ `Level == DegreeLevel.JinShi` | §6：甲第只属于进士 |
| ② | `Placement is not null` ⇒ `Class == ImperialClass.FirstClass` | FR-014：名次非空 ⟹ 甲第 = 一甲 |
| ③ | `Class == ImperialClass.FirstClass` ⇒ `Placement is not null` | FR-014：名次非空 ⟺ 甲第 = 一甲 |
| ④ | `Class ∈ {SecondClass, ThirdClass}` ⇒ `Placement is null` | §6：未进甲者无名次 |

**「进士但 `Class == null`」是合法状态**（不违反 ①~④），但**授官入口拒绝**它（R-12）。
**明确不含**：当前甲第的独立存储（派生自 `DegreeHistory` 末条，与 `CurrentPlacement` 同款）。

### 1.4 `ImperialClass`（enum，`KFL.Core/Enums`）——【新】

`FirstClass`（一甲）/ `SecondClass`（二甲）/ `ThirdClass`（三甲）。
**取值顺序即甲第高低**（§6：一甲 = 进甲者，其余按学业分二甲/三甲），但不承载**任何级数**——
`一甲 → L11` 之类的映射是规则数值，单点在 `OfficialCareerPolicy`（SC-009）。

### 1.5 `SalaryMode`（enum，`KFL.Core/Enums`）——【新】

`None` / `Active` / `AwaitingPost` / `Retired`。**派生**，不落字段（FR-021）。
判定与优先级见 §4.1；`None` 与 `AwaitingPost` 都无俸，分开是为了让断言能区分**无官**与**待阙**。

### 1.6 `AppointmentTrack`（enum，`KFL.Core/Enums`）——【新】

`FirstClass` / `SecondClass` / `ThirdClass` / `SpecialTribute`（特奏名）。
它是**授官入口的入参**：把「怎么入仕」与「功名记录的形状」解耦，
使逻辑轨 ⑤ 的**特奏名**（§6：50 岁 + 省试 6 败 → 授 L18，可拒绝）无需伪造一条进士记录。
入口把它**记入 `Person.EntryTrack`**（与「待阙」同生命周期，见 §2.1），③-a 授官**只读该字段**——
故 `Begin` 的 `track` 的语义是「授官时初始官阶的唯一依据」，不再于授官时从功名记录重新派生。

---

## 2. 实体与聚合（`KFL.Core/Entities`）

### 2.1 `Person`——【改】新增在职月数 + 待阙计时与入仕途径的交叉校验

| 成员 | 变更 | 不变量 |
| --- | --- | --- |
| `MonthsInOffice` | **【新】** `int`，可写（在任月数，自授官起算） | `>= 0`；赋负值抛 `ArgumentOutOfRangeException` |
| `EntryTrack` | **【新】** `AppointmentTrack?`，可写（待阙期记录的入仕途径） | 非空 ⇒ `AwaitingPost` 位为真；清位前 MUST 先置 `null`；授官时与计时一并清空 |
| `Status` | 语义扩展 | ⑦ 清除 `AwaitingPost` 位前 MUST 先把 `Timers.AwaitingPostRemainingMonths` **与 `EntryTrack`** 置 `null` |
| `Timers` | 语义扩展 | ⑦ `AwaitingPostRemainingMonths` 非空时 `AwaitingPost` 位 MUST 为真 |
| `Rank` / `Merit` / `Status`（其余位）/ `DegreeHistory` / `AppendDegree` | 不变 | `Merit` 仍只保证 `>= 0`（上限 100 在 Rules，R-04） |

**可写属性由八个变十个**：`Name`、`Study`、`Health`、`Rank`、`Merit`、`Status`、`Timers`、
`Occupation`、`MonthsInOffice`、**`EntryTrack`**。001 的「无写入通道」反射断言（`PersonTests`）随之更新。
**`EntryTrack` 为什么落成状态、而不是每次从功名记录派生**：途径经入口确定后 MUST NOT 随功名记录变化
——逻辑轨 ⑥ 的连坐降级会在待阙期内向历史追加一条降级记录（§7.4），若授官时重新派生，
「一甲进士」会被改判成 `SpecialTribute`（授 L18 而非 L11）；若 ⑤ 补记一条「进士但 `Class == null`」
的记录，派生还会在 ③-a 内部抛异常，破坏契约七 §7 的失败原子性。派生只发生在**入口**。
**明确不含**：待阙月数（在 `Timers` 里）、甲第（在功名记录里）、`SalaryMode`（派生）、
官名（规格书未定义，§17 禁止自创）。

### 2.2 `Family` / `GameState` / `FamilyEconomy` / `Treasury` / `Holdings` / `Ledger`

**本特性一行不改**。开局的资产与资金**只经构造函数初值**写入
（`Treasury(cash, savings, merchantCapital)` 与 `Holdings` 的属性 setter），
**MUST NOT 经 `FamilyEconomy.Apply`**——故开局**不落账本条目**（FR-010、R-06）。

---

## 3. 规则层类型（`KFL.Rules`）

### 3.1 `Config/OriginStartTable`——【新】§10.1 的唯一声明处

| 成员 | 内容（四个出身各一条） | 出处 |
| --- | --- | --- |
| `InitialCashGuan` | 农 80 / 工 80 / 商 500 / 士 200 | §10.1 |
| `InitialFarmlandMu` | 农 40 / 工 0 / 商 0 / 士 0 | §10.1 |
| `InitialRuralHouses` | 农 1（农舍同价）/ 工 1 / 商 0 / 士 1（含藏书） | §10.1、§5.3 |
| `InitialUrbanHouses` | 商 1 / 其余 0 | §10.1、§5.3 |
| `InitialMerchantCapitalGuan` | 商 300 / 其余 0（**入商本池，不入现金**） | §10.1、§5.2 |
| `SpouseCount` | 恒 1 | §10.1（「夫妇」） |
| `ChildCount` | 农 2 / 工 1 / 商 2 / 士 1 | §10.1、FR-003 |
| `AgeSpread` / `HeadAgeMean`（内部） | ±5；农/工/商 28、**士 30** | §10.1 |
| `HeadAgeMin` / `HeadAgeMax` | `HeadAgeMean ∓ AgeSpread`（农/工/商 23~33、士 25~35） | §10.1 |
| `SpouseAgeMean` | 25 | §10.1 |
| `ChildAgeMin` / `ChildAgeMax` | 0 / 8 | §10.1 |
| `ScholarHeadStudy` | 士 **60**（常量） | §10.1 |
| `CommonerHeadStudyMin` / `CommonerHeadStudyMax` | 10 / 30（其余出身） | §10.1 |
| `HeadHealthMin` / `HeadHealthMax` | 80 / 100 | §10.1 |
| `ChildStudyValue` | 0（常量） | §10.1 |
| `ChildHealthMin` / `ChildHealthMax` | 90 / 100 | §10.1 |
| `ScholarOriginHasJuRenRecord` | 士 = 是（开局带入一条举人记录，`Cause = Initial`） | §10.1、FR-008 |

**取样入口**（`GameConfig.NewGame` 的转发名）：`NextHeadAge` / `NextSpouseAge` / `NextChildAge` /
`NextHeadStudy` / `NextHeadHealth` / `NextChildHealth`（对外分别叫 `HeadAge` / `SpouseAge` /
`ChildAge` / `HeadStudy` / `HeadHealth` / `ChildHealth`）；常量以 `ChildStudy` 转发。

**不变量/说明**：所有区间一律「整数均匀、**含端点**」；`ScholarOriginHasJuRenRecord` 为真时
MUST NOT 同时置 `Family.HasShiStatus`（§10.2、FR-008）。
**明确不含**：任何界面文案、官名、资产的**单价**（单价在 `AssetPriceTable`，002 已交付）。

### 3.2 `Config/AttributePolicy`——【新】§4.1/§4.2 的分布参数与取样

| 成员 | 内容 | 出处 |
| --- | --- | --- |
| `TalentMean` / `TalentSigma` | 60 / 20（四項独立） | §4.2 |
| `StudyMean` / `StudySigma` | 30 / 15（无父母参照者） | §4.2 |
| `HealthMean` / `HealthSigma` | 85 / 10（无父母参照者） | §4.2 |
| `LifespanMeanMale` / `LifespanSigmaMale` | 60.7 / 8 | §4.1 |
| `LifespanMeanFemale` / `LifespanSigmaFemale` | 62.3 / 8 | §4.1 |
| `NextNormal(mean, sigma, IRandomService)` | 纯函数；Box–Muller，**恰好消耗 2 次** `NextDouble()` | R-08 |
| `NextTalent/NextStudy/NextHealth` | 取样 → **四舍五入取整** → clamp 到 `AttributeLimits.Min~Max` | spec Assumptions |
| `NextLifespan` | 取样 → **四舍五入取整** → **只 clamp 下界 `AttributeLimits.Min`（0）**、**不设上限**（FR-006） | spec Assumptions |

**明确不含**：`AttributeLimits` 的 0~100 值域（属 `KFL.Core`）、遗传公式（§4.2 的新生儿口径
属逻辑轨 ⑦）。

### 3.3 `Config/OfficialCareerPolicy`——【新】§8.2 的单一出处

| 成员 | 取值 | 出处 |
| --- | --- | --- |
| `AwaitingPostMinMonths` / `AwaitingPostMaxMonths` | 6 / 24（整数均匀、含端点） | §8.2、FR-013 |
| `InitialRankOf(AppointmentTrack)` | `FirstClass→11`、`SecondClass→13`、`ThirdClass→15`、`SpecialTribute→18` | §8.2、FR-014 |
| `MeritPerMonth` | 1 | §8.2 |
| `MeritMaximum` | 100 | §8.2 |
| `AppraisalPeriodMonths` | 36 | §8.2、FR-017 |
| `PromotionBaseChance` | 0.25 | §8.2 |
| `PromotionChancePerMerit` | 0.003（= 0.3%） | §8.2 |
| `PromotionChanceCap` | 0.70 | §8.2 |
| `RetirementAge` | 70 | §8.2、FR-020 |
| `RetirementSalaryRatio` | 0.50 | §8.2、FR-020 |
| `PromotionChance(int merit)` | `min(0.25 + merit × 0.003, 0.70)` 的纯函数 | §8.2 |

**明确不含**：禁升计时的递减与建立（逻辑轨 ⑥）、官名、考课的事件文本。

### 3.4 `Config/GameConfig`——【改】新增三组**只转发**的视图

新增 `GameConfig.NewGame`（转发 `OriginStartTable`）、`GameConfig.Attributes`（转发
`AttributePolicy` 与 `AttributeLimits` 的边界）、`GameConfig.Career`（转发
`OfficialCareerPolicy`）。**本文件仍 MUST NOT 出现任何数值字面量**（002 契约五 §5 条款 5），
否则 `ConfigLiteralTests` 会抓到「配置类之外的第二出处」。

### 3.5 `Start/NewGameSetup`——【新】新建存档入口

```csharp
public readonly record struct NewGameRequest(
    Origin Origin, Difficulty Difficulty, string? Surname, GameDate StartDate);

public sealed class NewGameSetupResult     // 薄视图：只持有 State，其余全部派生
{
    public GameState State { get; }
    public PersonId? HeadId { get; }            // => State.Family.HeadId
    public GameDate StartDate { get; }          // => State.CurrentDate
    public IReadOnlyCollection<Person> Members { get; }   // => State.Family.Members
}

public static NewGameSetupResult Create(
    NewGameRequest request, IRandomService random, INameGenerator names);
```

**编排步骤**（顺序即契约六 §2 的断言顺序）：
1. 姓氏：`request.Surname` 非空则用它；否则取 `names.NextSurname()`。
2. 构造家族 `Family(surname)` → 按 `OriginStartTable` 生成**家主 → 配偶 → 孩子**，
   家主任 `AddFoundingMember`（男、辈分 0、无父母）、配偶任 `AddOutsider`（女、辈分 0、无父母）、
   孩子任 `AddChild`（辈分 1、父母引用同指二人）——配偶按**外来者**加入（`Family.AddOutsider`
   的注释即把「娶入配偶」列为适用者），MUST NOT 改走 `AddFoundingMember`。
3. `Family.Marry(head, spouse)`；孩子同时引用二人；`Family.SetHead(家主)`。
4. 士出身：向家主的 `DegreeHistory` 追加一条
   `(JuRen, null, StartDate, Initial, null)`。
5. 组装 `Treasury`/`Holdings`/`Ledger`（**Ledger 为空**）与 `FamilyEconomy`
   （档位 = `LivingCostTable.InitialStandard`、米价系数 = `GrainPricePolicy.Initial`）。
6. 经 `GameStateFactory.Create` 取唯一标识 → `GameState`。

**不变量**：① 开局结果**不落任何账本条目**（`Ledger.Entries` MUST 为空）；
② 现金入现金池、商本入商本池、田宅入 `Holdings`，**MUST NOT 错池**；
③ 同 `request` + 同随机种子 ⇒ 逐字段相同；④ `Family.HasShiStatus == false`（任何出身）。
**明确不含**：界面、存档落盘、「仕」身份、孩子的天赋遗传（R-19#1）。

### 3.6 `Career/SalaryModePolicy`——【新】三态判定（纯函数）

```csharp
public static SalaryMode Of(Person person);   // 只判态：无 Money / Origin / Difficulty / GameDate 入参
```

判定优先级（R-05）：`Rank != null && Retired → Retired`；`Rank != null → Active`；
`Rank == null && AwaitingPost → AwaitingPost`；否则 `None`。
**幂等性**：判定是**只读**的纯函数，不改任何状态；判定只读 `Rank` 与 `Status`，与年月无关——
故**不收** `GameDate`：入参只保留它真正读取的东西，以免调用方以为「三态随时点变化」。
**这不是为了躲编译警告**（该理由曾写在本节，是错的）：仓库没有 `.editorconfig`，
未使用形参（`IDE0060`）默认不是警告级，`Directory.Build.props` 的
`TreatWarningsAsErrors` + `EnforceCodeStyleInBuild` 不会因未使用形参报错——
反例是 `AppointmentEntry.Begin` 的 `track` 曾为未使用形参而构建仍 0 警告
（该形参已按 §3.7 改为记入 `Person.EntryTrack`）。
**明确不含**：金额与一切乘区。**半俸比例、难度收益系数与「士出身 ×1.05」的单点都在
`IncomeCalculator`**（§3.11）——本类型 MUST NOT 出现 `Money` 参数、MUST NOT 返回已打折的金额
（FR-021；契约七 §5 条款 3 的「不得累乘」）。

### 3.7 `Career/AppointmentEntry`——【新】及第入仕入口

```csharp
public static void BeginForImperialGraduate(Person person, GameDate date, IRandomService random);
public static void Begin(Person person, AppointmentTrack track, GameDate date, IRandomService random);
```

行为：校验（§4.3 的拒绝矩阵）→ `Next(6, 25)` 掷待阙月数 → 置 `AwaitingPost` 位、待阙计时
**与 `EntryTrack`**（**赋值次序**：先 `Status`、后 `Timers`、最后 `EntryTrack`，与 `Person` 的
交叉校验方向一致：计时/途径非空 ⇒ 位为真，见 `implementation-notes.md` §2）。
`Begin` 的 `track` 因而**必然被读取**：它是 ③-a 授官时初始官阶的唯一依据。
**MUST NOT**：写官阶、写 `MonthsInOffice`、动账本、消耗除「待阙时长」以外的随机。
**明确不含**：科举细节（解试/省试/殿试、免解、特奏名的触发与拒绝，逻辑轨 ⑤）。

### 3.8 `Career/OfficialCareerAdvance`——【新】月度推进

```csharp
public static CareerAdvanceResult Run(Family family, GameDate month, IRandomService random);
```

内部次序（FR-019，逐条可断言）。
**成员集合** MUST 取 002 的**在册**口径（`CountedMembers.Registered` = 未亡且未外嫁，**含服刑与待阙**），
一律按 `PersonId` **升序**处理以保证确定性——因此**已亡与外嫁者 MUST NOT 被推进**（spec Edge Case
「待阙期内死亡或外嫁 ⇒ 仕途停止」），服刑者仍在集合内、由 ③-a/③-b/③-d 的**显式暂停**处理。

| 序 | 步骤 | 条件 | 随机 |
| --- | --- | --- | --- |
| ③-a | 待阙递减 → 递减到 0 的当月**授官**（读 `Person.EntryTrack` 定初始官阶；写 `Rank`、`MonthsInOffice = 0`、清计时、途径与状态位） | `AwaitingPost` ∧ 计口（未服刑） | 无 |
| ③-b | `Merit += 1`（钳制 `<= MeritMaximum`） | `SalaryMode == Active` | 无 |
| ③-c | **致仕**：置 `Retired` 位 | `SalaryMode == Active` ∧ `AgeAt(month) >= RetirementAge` ∧ 未致仕 | 无 |
| ③-d | `MonthsInOffice += 1`；命中 `>= 36` ⇒ 考课判定；判定后**重置为 0** | `SalaryMode == Active` ∧ `AgeAt(month) < RetirementAge` ∧ 未禁升 | 命中时 **1 次** `NextDouble()` |

**Q5 裁决**：`StatusFlag.ServingSentence` 为真者，③-a/③-b/③-d **一律暂停**——**不推进，也不重置**；
③-c 对服刑者无意义（必有官阶者才可能 `Active`，而服刑者已由计口排除在俸禄之外）。
**两种「不判定」MUST 在代码与快照里区分**：**禁升** = *跳过*到期判定并**重置**计时（§4.4 第二行）；
**服刑** = *暂停*（既不掷骰也不重置，刑满后从暂停处继续）。
**③-d 的年龄闸门与 ③-c 的关系**：③-d 自带 `AgeAt(month) < RetirementAge`，故「满 70 岁当月的成员
不参与考课」在 ③-c（US4）落地**之前**也成立，US3 因而不依赖 US4 的 ③-c；③-c 落地后两道闸门互为冗余。
**不变量**：① 每月每名成员至多一次授官、至多一次晋升、至多一次致仕；
② MUST NOT 出现「有官阶却从未授官」或「先致仕、后授官」；
③ 官阶级数 MUST 落在 `SalaryTable.HighestLevel~LowestLevel`（成功升一级时 `Level - 1`，
`Level == HighestLevel` 时**维持**）。

### 3.9 `Career/CareerAdvanceResult`——【新】一次推进的可断言增量

字段：`Appointments`（成员 → 新官阶）、`MeritGains`、`Promotions`（成员 → 旧级/新级）、
`Retirements`、**`AppraisalSkipped`（因禁升**跳过**到期判定，计时已重置为 0）**、
**`AppraisalPaused`（因服刑**暂停**，计时未重置）**——两者是**不同的语义**，MUST NOT 合并为
同一字段或同一「原因」枚举值；以及**每人的三态**（`IReadOnlyDictionary<PersonId, SalaryMode>`）。
它是本次结算的**增量快照**，MUST NOT 成为第二真源（R-15）。

### 3.10 `Settlement/MonthlySettlementEngine.Settle`——【改】插入官吏推进步

调用次序变为：① 提升待生效值 → ② 米价游走 → **③ 官吏推进（新）** → ④ 收入
（含 12 月年度项）→ ⑤ 生活费 → ⑥ 贷款先计息后划扣 → ⑦ `AdvanceMonth`。
**随机消费总次序**改为：**米价 →（③-d 的考课掷骰，逐人升序）→ 储蓄利率（仅 1 月）
→ 贷款计息利率**。
**MUST NOT**：把官吏推进放在收入之后；**MUST NOT** 为「授官/致仕」新增账本类别（R-15）。

### 3.11 `Settlement/IncomeCalculator`——【改】俸禄三态

`AddSalaries` 由「`person.Rank` 非空即发全俸」改为先取 `SalaryModePolicy.Of(person)`，再按三态取
**乘区系数**（这是**唯一**施加半俸的位置）：

| 三态 | 乘区系数 | 月俸条目 |
| --- | --- | --- |
| `Active` | `1` | 年俸月摊额 × 难度收益系数 ×（士出身 ×1.05） |
| `Retired` | `OfficialCareerPolicy.RetirementSalaryRatio` | 上式 **× 该系数**（全流程只乘一次，MUST NOT 再乘第二次） |
| `AwaitingPost` / `None` | — | **不发**（MUST NOT 落 0 金额条目——资金类条目金额 MUST 非 0） |

人群口径**不变**：仍遍历「计口成员」（002 的 `CountedMembers`，服刑与外嫁已排除）。
**明确不含**：仕身份的加成（仕身份只影响录取率/婚嫁/划扣，与俸禄无关，FR-021）。

### 3.12 `Settlement/SettlementResult`——【改】新增 `Career`

新增只读属性 `CareerAdvanceResult Career`（本次结算的③步增量）。其余字段语义不变。

### 3.13 `KFL.Infrastructure`——【新】姓名来源

```csharp
public interface INameGenerator                       // Abstractions/
{
    string NextSurname();                             // §12.4 的「随机姓氏」
    string NextGivenName(Gender gender);              // 名；姓由家族姓氏决定
}
```

| 实现 | 用途 | 确定性口径 |
| --- | --- | --- |
| `SongStyleNameGenerator(IRandomService)` | **产品默认**；内置宋风姓氏/名字库 | 全部随机经注入的 `IRandomService` |
| `BogusNameGenerator(IRandomService)` | 规格书 §2 的「Bogus 为主」 | 以注入随机取整数种子 → `new Randomizer(seed)`（**局部**），MUST NOT 触碰全局 `Randomizer.Seed` |

`KFL.Infrastructure.csproj` 增 `PackageReference Bogus 35.6.1`。
**明确不含**：Bogus 的分布取样（正态在 `AttributePolicy` 里自实现，R-08）、
亲属称谓/字辈（规格书未定义）。

---

## 4. 判定与拒绝矩阵

### 4.1 三态判定（R-05）

| 输入 | `SalaryMode` |
| --- | --- |
| `Rank != null` ∧ `Retired` | `Retired`（半俸） |
| `Rank != null` ∧ ¬`Retired` | `Active`（全俸） |
| `Rank == null` ∧ `AwaitingPost` | `AwaitingPost`（无俸） |
| 其余 | `None`（无俸禄条目） |

### 4.2 致仕判定

`SalaryMode == Active` ∧ `person.AgeAt(month) >= OfficialCareerPolicy.RetirementAge` ⇒ 置位一次；
已是 `Retired` 者跳过（幂等，MUST NOT 累乘半俸）。**`Rank == null` 者永不致仕**（无官可致仕）。

### 4.3 「及第入仕」的拒绝矩阵（R-12；全部抛异常且无部分写入）

| 情形 | 结果 |
| --- | --- |
| `Rank != null`（已有官阶） | 拒绝 |
| `AwaitingPost` 位为真 | 拒绝，且 MUST NOT 重置剩余月数 |
| 进士入口而末条记录不是进士 | 拒绝 |
| 进士但末条 `Class == null`（甲第缺失） | 拒绝（**不猜等级**） |
| 待阙者再次「授官」 | 不可表达（授官只在 ③-a 内部发生） |

### 4.4 考课判定（R-11）

| 条件 | 行为 |
| --- | --- |
| `MonthsInOffice < 36` | 无判定 |
| `AgeAt(month) >= RetirementAge`（满 70 岁） | 无判定（③-c 已置 `Retired`；或由 ③-d 自带的年龄闸门挡住） |
| 服刑（`ServingSentence`） | **暂停**：不 +1、不判定、**不重置**计时（Q5）——与下一行的禁升**不同** |
| `MonthsInOffice >= 36` ∧ `PromotionBanned` | **跳过**：不掷骰、不升迁；计时**重置为 0** |
| `MonthsInOffice >= 36` ∧ ¬`PromotionBanned` | 掷 1 次 `NextDouble()`；`< min(0.25 + merit × 0.003, 0.70)` ⇒ 级数 −1（到 L1 维持）；计时重置为 0 |

---

## 5. 需求 → 模型映射

| 需求 | 落点 |
| --- | --- |
| FR-001 新建存档入口（纯函数/可注入） | `Start/NewGameSetup` + `NewGameRequest`/`NewGameSetupResult` |
| FR-002 四出身初始资产（三池不错池） | `OriginStartTable`（现金/商本/田宅）；§3.5 步骤 5 |
| FR-003 家族成员构成 | `OriginStartTable.ChildCount` / `SpouseCount` |
| FR-004 年龄与性别 | `OriginStartTable.HeadAgeMin`/`HeadAgeMax`/`SpouseAgeMean`/`ChildAgeMin`/`ChildAgeMax`（取样 `NextHeadAge`/`NextSpouseAge`/`NextChildAge`）+ `NewGameSetup` |
| FR-005 属性初始化 | `AttributePolicy` + `OriginStartTable.{Head,Child}Study/Health` |
| FR-006 天命寿数（出身无关、只 clamp 下界） | `AttributePolicy.NextLifespan`（取整 → clamp 下界 0，**不设上限**） |
| FR-007 辈分/家主/婚姻/父母引用 | `Family.AddFoundingMember`/`AddChild`/`Marry`/`SetHead`（001 已交付，本特性只调用） |
| FR-008 士出身功名记录 + 仕身份为假 | §3.5 步骤 4；`Family.HasShiStatus` 保持 `false`（R-14） |
| FR-009 姓名来源抽象 + 确定性实现 | `INameGenerator` + `SongStyleNameGenerator`/`BogusNameGenerator` |
| FR-010 开局不落账本条目 | §3.5 不变量 ①（资产只经构造入参） |
| FR-011 开局确定性 | §3.5 不变量 ③；契约六 §3 的随机消费次序 |
| FR-012 及第入仕入口（不做科举判定） | `AppointmentEntry` |
| FR-013 待阙 6~24、无俸、0 时清位 | `OfficialCareerPolicy.AwaitingPost*`；`StatusTimers.AwaitingPostRemainingMonths` |
| FR-014 初始官阶映射 + 甲第字段 | `OfficialCareerPolicy.InitialRankOf`；`DegreeRecord.Class` + `ImperialClass` |
| FR-015 授官自动发生、幂等/互斥 | `OfficialCareerAdvance` ③-a；§4.3 |
| FR-016 政绩 +1、上限 100、非官员不增长 | `OfficialCareerAdvance` ③-b；`OfficialCareerPolicy.MeritMaximum` |
| FR-017 36 月考课、概率公式、判定后重置 | `OfficialCareerAdvance` ③-d；`OfficialCareerPolicy.PromotionChance` |
| FR-018 禁升期跳过判定（只消费） | §4.4 第二行；MUST NOT 改 `PromotionBanRemainingMonths` |
| FR-019 推进位于收入之前 + 月内次序 | `MonthlySettlementEngine`（§3.10）；③-a~③-d |
| FR-020 致仕、半俸、政绩/计时停摆、幂等 | `OfficialCareerAdvance` ③-c；`SalaryModePolicy`；`RetirementSalaryRatio` |
| FR-021 俸禄三态 × 难度 × 士出身（与仕身份无关） | §3.11；`SalaryMode` |
| FR-022 全部随机经可设种子来源 | `IRandomService` 唯一入口（含 Bogus 适配器的**局部**种子） |
| FR-023 数值集中 `KFL.Rules/Config/` | §3.1~§3.4 + 契约八 |
| FR-024 §16 必测项（含俸禄锚点三态复核） | `tests/KFL.Tests/Rules/{NewGameSetupTests,AppointmentTests,CareerAdvanceTests,RetirementTests,SalaryModeTests}` |
| FR-025 非 UI 层无时钟/全局随机/文件/网络 | G-07（扫描范围已含本特性新增目录） |
| FR-026 不做界面 | `KFL.Presentation`/`KFL.App` 零改动 |
| FR-027 吏治取 §8.2 全量 | §3.3 + §3.8 覆盖待阙/授官/考课/政绩/致仕/禁升跳过 |

## 6. 状态转移

### 6.1 仕途主干（§8.2）

```text
（无官职，进士）
   │ AppointmentEntry.Begin*（掷 6~24；消耗 1 次 Next；把 track 记入 Person.EntryTrack）
   ▼
待阙 AwaitingPost（无俸）── 逐月剩余月数 −1 ──► 递减到 0 的当月
   │                                              │ ③-a 授官（读 EntryTrack；清计时、途径与位）
   │                                              ▼
   │                                    在任 Active（全俸，MonthsInOffice 从 0 起算）
   │                                              │
   │                          ③-b Merit+1 ├──► ③-d MonthsInOffice+1
   │                                              │      └─ 满 36 → 考课（成功 → 级数 −1）
   │                                              │ ③-c 年龄 ≥ 70
   │                                              ▼
   └─────────────────────────────────────► 致仕 Retired（半俸，官阶保留）
```

### 6.2 待阙计时与入仕途径（逐月，R-11）

授官当月剩余月数 = 掷出的 `n`；此后每月 ③-a 递减 1；**递减到 0 的当月**授官
（即「在任月数从 0 起算」与「待阙计时与途径清空」在同一次结算里相邻发生）。
途径自入口写入起**只读**：待阙期内功名记录的任何变动（逻辑轨 ⑤/⑥）MUST NOT 改变已记录的途径。
金额后果：授官当月起，当月**收入步**即按新官阶计全俸（FR-019）。

### 6.3 考课与在职计时（逐月，R-11）

| 在职月数 | 判定 |
| --- | --- |
| 1 ~ 35 | 无 |
| **36** | 判定一次（禁升则跳过），随后重置为 0 |
| 37 ~ 71 | 无 |
| **72** | 第二次判定 |

### 6.4 其他按月转移（本特性的**全部**月度行为）

- `Merit`：在任且未致仕时 +1/月（≤ 100）；**满 70 岁当月仍 +1**（③-b 先于 ③-c），次月起停止。
- `MonthsInOffice`：在任且未致仕时 +1/月。
- 待阙剩余月数：待阙且未服刑时 −1/月。
- `Retired`：在任且年龄 ≥ 70 时置位（幂等）。
- 服刑者：以上四项**全部暂停**（Q5）。

### 6.5 本特性**不建立**的状态转移

随机事件、属性成长/衰老/疾病/死亡（逻辑轨 ⑦）、科举季与功名推进（逻辑轨 ⑤）、
贿赂与惩罚矩阵、服刑与禁考的建立与递减、释放降级、连坐（逻辑轨 ⑥）、婚育与买人口、
家主继任（逻辑轨 ⑦）、存档读写与成就（逻辑轨 ④/⑧）、任何界面状态（界面轨 U1~U5）、
`Family.HasShiStatus` 的任何写入（R-14）。
