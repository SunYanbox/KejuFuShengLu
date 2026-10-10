# 阶段 0 研究：开局资产与官吏体系

**Feature**: `003-opening-assets-officialdom` | **Date**: 2026-10-08

本文件记录**动工前的抉择**：每一条给出「问题 → 结论 → 依据 → 被否的替代」。全部结论都已
落进 [data-model.md](./data-model.md) 与 [contracts/](./contracts/)；**无 `NEEDS CLARIFICATION` 残留**。
数值一律引规格书章节号，MUST NOT 在此另立数值（章程「需求真源」）。

标记约定：**【裁决 Q4/Q5】**＝用户本次逐项裁决；**【真源冲突】**＝规格书或既有工件自相矛盾，
按章程「需求真源」修正工件；**【§17①/②】**＝按规格书 §17 的默认规则推导并登记，可被逐项否决。

---

## R-01 开局年月与出生年月：`GameDate` 允许前史纪年【裁决 Q4】

**问题**：规格书 §10.1 要求开局家主 28±5 岁（士 30±5）、配偶 25±5、孩子 0~8 岁；
§3 规定推演从「1 年 1 月」开始。而 001 交付的 `GameDate` 在构造期强制 `year >= 1`，
`Person.AgeAt` 又**只**由 `BirthDate` 派生（001 research R-07：年龄不落裸字段）。
若开局年月恒为 1 年 1 月，则每位开局成员的出生年必然落在 `1 − 年龄 ≤ 0`——**无法表示**。
三者不可能同时成立。

**结论**：**放宽 `GameDate` 的年份下界**——年份接受 `int` 全域（含 `0` 与负数），月份仍 MUST 在
1~12；错误信息改为只描述月份约束。`GameState.CurrentDate` 仍从 **1 年 1 月**起推演。
负年份/0 年是**前史纪年**（存档开始之前的世界历史），只出现在出生年月与功名记录的年月上。

**依据**：用户裁决 A（2026-10-08）；规格书 §10.1 与 §3 的原文本条不动；001 的
`GameDate` 不变量本身**不是**需求，而是当时的防御性校验（其 XML 注释写的是「架空纪年从 1 起」，
并未论证「出生不得早于元年」）。

**被否的替代**：① **保留 `year >= 1`、把开局年月改成足够晚的年份**（如 40 年 1 月）——
要修订规格书 §3「从 1 年 1 月开始推演」，且存档纪年从 40 起、玩家看到的第一年是 40 年，
与「架空纪年从 1 起」的世界观冲突；② **开局成员年龄不落 `BirthDate`**（`Person` 增 `Age` 裸字段）——
直接推翻 001 research R-07 与 `AgeAt` 的派生契约，生日当月转档（§4.3「生日当月生效」）会失去落点。

**边界**：出生年的实际下界由 `OriginStartTable`（最短 23 岁 ⇒ 前推约 33 年）与
`NewGameSetup` 的入参共同约束，故本特性不会构造 `int.MinValue` 一级的年份；
`GameDate.ElapsedMonths` 的加法溢出行为**沿用 001 现状**（不新增校验，也不在本特性引入新的溢出路径）。

---

## R-02 甲第字段的形状：`DegreeRecord` 的可选第 5 参与三条相容性不变量

**问题**：FR-014 要求 `一甲 → L11 / 二甲 → L13 / 三甲 → L15`，而 001 的 `DegreeRecord`
只有「功名 + 一甲名次」，二甲与三甲**不可区分**。同时 `ValueObjectTests.DegreeRecord四个成员均为必需`
断言该记录**恰好 4 个构造参数且无可选参数**。

**结论**：新增**可选第 5 参** `ImperialClass? imperialClass = null` 与只读属性 `Class`，
并施加三条构造期不变量：

| 序号 | 不变量 | 违背示例 |
| --- | --- | --- |
| ① | `Class is not null` ⇒ `Level == JinShi` | 举人带「三甲」 |
| ② | `Placement is not null` ⇒ `Class == FirstClass` | 状元却写「二甲」 |
| ③ | `Class == FirstClass` ⇒ `Placement is not null` | 一甲却没有名次 |
| ④ | `Class ∈ {SecondClass, ThirdClass}` ⇒ `Placement is null` | 二甲带「榜眼」 |

（②③ 合起来即 FR-014 的「名次非空 ⟺ 甲第 = 一甲」；④ 是 ② 的逆否补充。）

**「甲第缺失」不是构造期错误**：`Level == JinShi` 且 `Class is null` **允许存在**
（001 时代的记录、或科举尚未产出甲第的中间态），但**授官入口 MUST 拒绝**它，
而不是猜一个等级——这正是 spec Edge Case「甲第缺失或与一甲名次矛盾 → 授官 MUST 拒绝」的字面要求。

**依据**：FR-014；规格书 §6（「进甲者在该条记录中额外带名次」，二甲/三甲由学业决定）、§8.2。

**被否的替代**：① **另开一张「甲第」旁挂表**（`Person` → `ImperialClass`）：功名变迁历史是
追加式的（§4.1），甲第属于**某一次**变化的属性；旁挂表无法表达「第二次进士记录换了甲第」，
也会让「当前甲第」与「当前功名」两个派生源分离；② **把二甲/三甲编成名次枚举的扩展**
（`ImperialPlacement.SecondClass` 之类）：名次是「1~3 名」（状元/榜眼/探花），与甲第是
**两个不同的维度**，混进同一枚举会让 §6 的「进甲率」与 §8.2 的「授官映射」共用一个类型，
任意一处扩展都会污染另一处。

**受控的工件修订**：`ValueObjectTests.DegreeRecord四个成员均为必需` 改为「五个成员、第 5 个可选」，
并新增四条不变量的正例/反例断言；001 既有的一甲构造点（`PersonTests`、`FamilyTests`）
补传 `ImperialClass.FirstClass`。这属于**版本内可见的工件修订**，在 `tasks.md` 中单列任务，
不做静默兼容。

---

## R-03 待阙计时的落点：`StatusTimers` 的第 4 个字段

**结论**：`StatusTimers` 新增 `int? AwaitingPostRemainingMonths`（`>= 0` 校验与既有三个字段同款），
并把 `Person.Status` / `Person.Timers` 的**交叉一致性校验**扩展到 `StatusFlag.AwaitingPost`：

- 清除 `AwaitingPost` 位前 MUST 先把该字段置 `null`（否则 `Person.Status` 抛异常）；
- 该字段非空时 `AwaitingPost` 位 MUST 为真（否则 `Person.Timers` 抛异常）；
- 剩余月数为 **0** 时 MUST NOT 保留待阙状态（FR-013）——即「递减到 0 的当月」的次序是
  **先授官、后清位与清计时**，绝不出现「状态位为真而剩余月数为 0」的中间态。

**依据**：FR-013（「待阙 MUST 以『状态位 + 剩余月数』表达——剩余月数为 0 时 MUST NOT 保留待阙状态」）；
001 的 data-model §2.1 不变量 4（计时字段仅在对应状态位为真时可非空）。

**被否的替代**：把待阙剩余月数放进 `Person` 的独立 `int?` 属性（如 `AwaitingPostMonths`）——
`StatusTimers` 的存在理由正是「状态位与其计时的**成对**不变量」（`Person` 的两个 setter 互相校验）；
另开属性会让「位为真而计时为空」「计时为负」两类非法态重新变得可表达。

---

## R-04 在职计时与政绩的落点

**结论**：`Person` 新增 `int MonthsInOffice`（自授官起算，**不变量** `>= 0`，可写）；
**政绩上限 100 不进 `KFL.Core`**——`Person.Merit` 仍只保证 `>= 0`，上限由
`OfficialCareerPolicy.MeritMaximum`（Rules）在推进时钳制。

**依据**：`Person.Merit` 的既有注释已写明「上限 100 属规格书 §8.2 规则」；
`AttributeLimits` 的注释同样声明「MUST NOT 把政绩上限 100 放进来」。两条都指向 Rules。
`MonthsInOffice` 则是**中立的状态量**（没有数值语义），与 `Merit`/`Rank` 同类。

**被否的替代**：把在职月数并入 `StatusTimers`——它不是「由某个状态位支撑的剩余计时」，
而是一个**累计**计数（不会因状态位清除而失效），塞进 `StatusTimers` 会破坏该类
「仅在对应状态位为真时可非空」的语义。

---

## R-05 俸禄三态的判定：`SalaryMode` 派生 + `SalaryModePolicy` 纯函数

**结论**：`KFL.Core/Enums/SalaryMode.cs` 定义四个取值 `None / Active / AwaitingPost / Retired`；
判定规则（**优先级从上到下**）由 `KFL.Rules/Career/SalaryModePolicy` 的纯函数给出：

| 条件 | 判定 | 月俸 |
| --- | --- | --- |
| `Rank != null && Retired` | `Retired` | 全俸 × 50% |
| `Rank != null && !Retired` | `Active` | 全俸 |
| `Rank == null && AwaitingPost` | `AwaitingPost` | 0（不落条目） |
| 其余 | `None` | 0（不落条目） |

**依据**：FR-013（待阙无俸）、FR-020（致仕半俸、官阶保留）、FR-021（三态口径）；
spec Key Entities「俸禄三态（SalaryMode）……属**派生**而非独立存储的状态」。

**为何是四个取值而不是三个**：非官员（白身/举人/贡士）既不「在任」也不「待阙」也不「致仕」。
用一个可空的 `SalaryMode?` 表达「非官员」会让 `IncomeCalculator` 的 `switch` 出现 `null` 分支，
而枚举多一个 `None` 取值可以把「无俸」的两种**不同来由**（无官 vs 待阙）在断言里分开。
`Retired` 的判定**优先于** `Active`，保证「致仕后仍持官阶」不会退回全俸。

**被否的替代**：在 `Person` 上派生 `SalaryMode` 属性——`SalaryMode` 的优先级编码的是
**仕途规则**（§8.2），把它写进 `KFL.Core` 等于让最底层持有规则判定；
`KFL.Core` 只承载枚举取值（结构事实，同 `AgeBracket`），判定归 `Rules`（同 `AgeBracketPolicy`）。

---

## R-06 开局编排的归属与复用（不开新接缝）

**结论**：新建存档入口 `KFL.Rules/Start/NewGameSetup`，形状：

```
NewGameSetupResult Create(NewGameRequest request, IRandomService random, INameGenerator names)
```

- `NewGameRequest`：`Origin Origin`、`Difficulty Difficulty`、`string? Surname`（`null` = 随机姓氏）、
  `GameDate StartDate`（新建存档恒为 1 年 1 月）。
- `NewGameSetupResult`：**只持有 `GameState`**（唯一真源），另以派生只读属性转发
  `HeadId`/`Members`/`StartDate` 供断言——MUST NOT 另存成员集合或余额副本
  （spec Key Entities 明令「MUST NOT 成为与家族/资金池并存的第二份真源」）。
- `GameState` 的唯一标识**复用 `KFL.Infrastructure/Services/GameStateFactory`**
  （Rules → Infrastructure 是允许边），不在本特性重写一遍「16 字节 → `Guid`」；
  MUST NOT 使用 `Guid.NewGuid()`（G-07 禁用 token，章程原则 IV）。
- 初值来源**不复制**：生活费档位取 `LivingCostTable.InitialStandard`、米价系数取
  `GrainPricePolicy.Initial`（两者正是 `GameState`/`FamilyEconomy` 构造的必需入参，
  001/002 已把「不设默认值」的理由写在类型注释里）。
- 初始资产直接写 `Treasury`/`Holdings` 的构造入参——**不经 `FamilyEconomy.Apply`**，
  故**不落账本条目**（FR-010：初始余额口径，不是一笔收入）。

**依据**：FR-001（纯函数式或仅依赖可注入服务）、FR-010、001 契约二 §2「接缝 MUST 通过构造函数注入」。

**被否的替代**：① **把开局放进 `KFL.Infrastructure`**（与 `GameStateFactory` 同处）：
开局需要读 `OriginStartTable`/`LivingCostTable`/`GrainPricePolicy` 等 Rules 数值，
而 Infrastructure **看不到** Rules（G-05），只能把数值抄一份 ⇒ SC-009 直接失败；
② **新增第三个接缝（如 `INewGameService`）**：本特性没有跨实现替换的需求，
按章程「复杂度 MUST 被论证」不预置。

---

## R-07 开局的随机消费次序（同种子 → 逐字段同结果）

**结论**：次序**固定**如下，并写进契约六 §3 逐条断言：

1. **姓氏**（仅当 `request.Surname == null`）：从内置姓氏池取一个（消耗注入随机）。
2. **成员**，按固定构造序 **家主 → 配偶 → 孩子 1..n**；每名成员内部按
   **性别（如需随机）→ 年龄 → 四项天赋（农/商/仕/工）→ 学业 → 体质 → 天命寿数 → 姓名**。
3. **存档唯一标识**：`NextBytes(16)` → `Guid`（复用 `GameStateFactory`）。

全部随机只来自注入的 `IRandomService`；MUST NOT 读取全局随机、MUST NOT 依赖
Bogus 的全局 `Randomizer.Seed`（见 R-09）。

**依据**：FR-011、FR-022（章程原则 IV 的落点）。002 的契约三 §4 已建立同一手法
（把随机消费次序写成契约并逐条断言），本特性沿用。

**被否的替代**：不规定次序、只断言「同种子同结果」——那么任何**新增一次随机调用**
（例如给「士出身的孩子天赋」加一项）都会静默改变整条时间线，测试无法指出是哪一步漂移了。

---

## R-08 正态分布的实现与取整【§17②】

**结论**：`KFL.Rules/Config/AttributePolicy` 提供纯函数取样
`NextNormal(mean, sigma, IRandomService)`，实现取 **Box–Muller**（每次取样**恰好消耗 2 次
`NextDouble()`，**不做**静态缓存以避免跨调用耦合）；调用方按 spec Assumptions 的规定
**四舍五入取整后 clamp 到 `AttributeLimits.Min~Max`（0~100）**。
分布参数单点声明在该配置类：

| 用途 | 分布 | 出处 |
| --- | --- | --- |
| 天赋（四项独立） | `N(60, 20)` | §4.2「无父母参照者 / 开局成员」 |
| 学业（配偶、买来的旁系） | `N(30, 15)` | §4.2 |
| 体质（配偶、买来的旁系） | `N(85, 10)` | §4.2 |
| 天命寿数 男 / 女 | `N(60.7, 8)` / `N(62.3, 8)` | §4.1 |

**MUST NOT 使用 Bogus 的 `GaussianDouble`**：那会把随机来源从注入的 `IRandomService`
挪到 Bogus 内部，破坏「随机消费次序可断言」与确定性口径。
`KFL.Core` 侧的 `AttributeLimits` 仍只承载**值域**（0~100），分布参数属 Rules（SC-009）。

**依据**：FR-005/FR-006、FR-022；spec Assumptions「全部正态分布按取整（四舍五入）后 clamp 到 0~100」。

**被否的替代**：① 用 **Bogus 分布**（`ExtensionsForRandomizer.GaussianDouble`）——
同上，且会给 `KFL.Rules` 引入 Bogus 的包依赖；② **Maraglia 的极坐标法**——需要拒绝采样
（随机消费次数不定），与 R-07 的「次序可断言」冲突。

---

## R-09 姓名来源的形状与两个实现

**结论**：`KFL.Infrastructure/Abstractions/INameGenerator`：

```csharp
public interface INameGenerator
{
    string NextSurname();                  // 姓氏池随机取一（§12.4「随机姓氏」）
    string NextGivenName(Gender gender);   // 只取名；姓氏由家族姓氏决定（§12.4）
}
```

- **`SongStyleNameGenerator(IRandomService)`**：内置宋风姓氏/名字库，**产品默认路径**，
  确定性由注入的 `IRandomService` 保证（规格书 §2 的「内置宋风姓氏/名字字库兜底」）。
- **`BogusNameGenerator(IRandomService)`**：Bogus `zh_CN` 适配器（规格书 §2 的「Bogus 为主」）。
  确定性口径：以注入随机取一个整数种子 → `new Randomizer(seed)`（Bogus 的
  `Randomizer(int)` **完全忽略**全局静态 `Randomizer.Seed`）→ 只在该局部 `Randomizer` 上取名。
  **MUST NOT** 赋值 `Bogus.Randomizer.Seed`（全局可变状态 = 隐式全局随机源，章程原则 IV）。
- 姓名装配：`Person.Name = 家族姓氏 + GivenName`（001 的 `Person.Name` 是**全名**，
  夹具中即「陈祖」这一形状）。
- `KFL.Rules` MUST 只依赖 `INameGenerator` 抽象（规则层不引用 Bogus 类型）。

**新增依赖**：`KFL.Infrastructure.csproj` 增 `PackageReference Bogus 35.6.1`（章程依赖基线的既有项、
仍在维护、含 `net6.0` 资产 → 兼容 `net10.0`；本机 NuGet 缓存中已存在该版本，离线可还原）。
已核对 `Bogus.data.zh_CN.locale.bson` **含 `name` / `first_name` / `last_name` 数据**，
故 zh_CN 取名可用。

**依据**：FR-009、规格书 §2、章程「技术栈与工程约束」（Bogus 属依赖基线，新增依赖须说明必要性）。

**已登记的取舍**：Bogus 的 `Randomizer` 内部使用 `System.Random`，其生成算法**不保证跨 .NET
版本稳定**——与 001 `SeededRandomService` 的已知取舍同款（单次运行内确定性成立）。
所以**产品默认走内置生成器**，Bogus 适配器作为规格书 §2「为主」的实现并在测试中同样断言确定性；
若实现期发现 `DataSet.Randomizer` 的赋值通路不可用，**回退方案**是只交付内置生成器
（FR-009 的 MUST 已满足）并在 `tasks.md` 追记「Bogus 适配器延后」——**MUST NOT** 改为设全局种子。

---

## R-10 官吏月度推进的时点与月内次序（FR-019）

**结论**：`MonthlySettlementEngine.Settle` 在 **① 提升待生效值** 与 **② 米价系数游走** 之后、
**收入**之前插入新的一步；该步内部次序**固定**：

```
① 提升待生效的难度与生活费档位（002 原有）
② 米价系数游走并 clamp（002 原有）
③ 官吏推进（本特性新增；**对在册成员 `CountedMembers.Registered`（未亡且未外嫁，含服刑与待阙）**按 PersonId 升序处理）：
   ③-a 待阙计时递减；递减到 0 即【授官】（写官阶、清待阙位与计时、在职月数置 0）
   ③-b 政绩 +1（在任者；钳制 ≤ 100）
   ③-c 致仕判定（在任 ∧ 年龄 ≥ 70 ⇒ 置 Retired；幂等）
   ③-d 在职计时 +1 与考课判定（在任且未致仕、**未满致仕年龄**、未禁升；命中周期才掷骰，掷骰消耗 1 次 NextDouble）
④ 收入（002 原有；俸禄按三态计）
⑤ 生活费（002 原有）
⑥ 贷款先计息、后划扣（002 原有）
⑦ AdvanceMonth（002 原有）
```

**随机消费次序**（契约七 §4 逐条断言）：**米价 →（③-d 的考课掷骰，逐人按 `PersonId` 升序）
→ 储蓄利率（仅 1 月）→ 贷款计息利率**。待阙时长的随机**不在月度步骤内**（它属于及第入仕入口，R-11）。

**「无官可致仕」与「释褐即致仕」**：③-a 先授官、③-c 后致仕，故同月待阙期满且刚满 70 岁者
**当月即成为致仕官员并按新授官阶的半俸计**；从未有官职的 70 岁者不经 ③-c（`Rank == null`）。
③-c 在 ③-d 之前，故「考课成功同月满 70 岁」**不会晋升**（③-d 被致仕挡住）。

**服刑者**（Q5 裁决）：`StatusFlag.ServingSentence` 为真者，其 ③-a/③-b/③-d **一律暂停**
（**不推进，也不重置**——与禁升的「跳过并重置计时」是两种语义），③-c 也不适用（服刑期不减寿、不改状态）；俸禄本就为 0（服刑者不计口）。

**依据**：FR-019；spec Assumptions「本月政绩 +1 计入本月到期的考课」「致仕判定先于考课」；
002 契约三 §1（该契约被本特性插入一步，已在契约七 §2 说明）。

**被否的替代**：把官吏推进放在**生活费之后**——「当月授官当月起领俸」与「满 70 岁当月即按半俸计」
都会晚一个月成立，直接违反 FR-019。

---

## R-11 考课计时的口径与及第入仕的随机

**结论**：

- **及第入仕入口**（`AppointmentEntry.Begin`）：校验 → 掷**待阙月数** = `Next(6, 24 + 1)`
  （**整数均匀、含两端点**，消耗 1 次 `Next`）→ 置 `AwaitingPost` 位与
  `AwaitingPostRemainingMonths`。**不写官阶**（授官才写）。
- **待阙递减**：002 的饥馑/贷款计时记的是**已持续**月数，故用「先 +1、再比阈值」；
  待阙记的是**剩余**月数，方向相反，实现时 MUST NOT 混用——
  「及第入仕」时把剩余月数置为掷出的 `n`，此后**每月递减 1**，**递减到 0 的当月授官**
  （授官当月计入，故待阙期总长恰为 `n` 个月）。
- **在职计时**：授官当月记 **1**（在 ③-a 完成后的同一次结算里由 ③-d 递增），
  此后每月 +1；`MonthsInOffice >= 36` 的当月开展考课判定；判定后（无论成功、失败，
  或**因禁升被跳过**）一律**重置为 0**，即「重新开始 36 个月计时」。
  故「第 35 个在职月 MUST NOT 判定、第 36 个在职月判定」——与 SC-005 的字面一致。
- **禁升**（FR-018）：到期当月若 `PromotionBanned` 为真，则**不掷骰、不升迁**
  （MUST NOT 消耗随机），但仍重置计时；本特性**只消费** `PromotionBanned` 与其计时值，
  MUST NOT 递减或新建该计时（逻辑轨 ⑥）。

**依据**：FR-013、FR-014、FR-015、FR-017、FR-018；spec Assumptions（禁止补判）。

**被否的替代**：**暂存判定**（禁升期满后补判一次）——§8.2 只写「禁升期内跳过判定」，
未授权补判；且补判需要一个新的「待补判」状态位，属 §17③ 范畴而规格书未覆盖。

---

## R-12 授官映射与「甲第缺失」的拒绝

**结论**：`AppointmentEntry` 提供两个入口：

| 入口 | 入参 | 官阶 | 前置校验 |
| --- | --- | --- | --- |
| `BeginForImperialGraduate(person, date, random)` | 从 `person.DegreeHistory` 末条**派生** `AppointmentTrack` | 按甲第映射 | 末条 MUST 是进士且 `Class` 非空；名次与甲第的矛盾由 R-02 的构造期不变量挡住 |
| `Begin(person, track, date, random)` | 显式给出 `AppointmentTrack` | 按 track 映射 | 供逻辑轨 ⑤ 的**特奏名**（`SpecialTribute` → L18）与将来复用 |

**幂等/互斥拒绝矩阵**（全部抛异常，且 MUST NOT 有任何部分写入）：

| 情形 | 结果 |
| --- | --- |
| 已有官阶（`Rank != null`） | 拒绝 |
| 已在待阙（`AwaitingPost` 位为真） | 拒绝（MUST NOT 重置剩余月数） |
| 已是致仕（`Retired`） | 拒绝（题中之义：致仕者必有官阶，已由第一条覆盖） |
| 非进士而走进士入口 | 拒绝 |
| 进士但 `Class is null` | 拒绝（**不猜等级**） |

映射：`FirstClass → L11`、`SecondClass → L13`、`ThirdClass → L15`、`SpecialTribute → L18`
（规格书 §8.2，单点声明在 `OfficialCareerPolicy`）。

**依据**：FR-012、FR-014、FR-015；spec Edge Cases（重复触发 MUST 被拒绝、甲第缺失 MUST 被拒绝）。

**被否的替代**：把「特奏名 → L18」也做成一条 `DegreeRecord`——特奏名**不是功名**
（§6 的功名链只有白身→举人→贡士→进士），把它写成功名记录会让 `CurrentDegree` 撒谎。

---

## R-13 致仕的幂等与半俸口径

**结论**：致仕判定（③-c）在两处幂等：① 已是 `Retired` 者直接跳过；
② 半俸只在**计算时**乘一次 50%，**MUST NOT** 把官阶或任何状态改成「半俸档」，
故不可能累乘（不会 50% → 25%）。半俸 = `年俸 ÷ 12 × 难度收益系数 ×（士出身 ×1.05）× 50%`；
「士出身 ×1.05」与 §10.2 的「仕身份」**无关**（FR-021 明令不得混为一谈）。

**依据**：FR-020、FR-021；spec Edge Case「已致仕成员被再次判定致仕 MUST 幂等」。

**被否的替代**：新增「半俸」官阶/薪酬档位字段——那会造出一个可与 `Retired` 状态位
互相矛盾的第三处真源。

---

## R-14 仕身份（`Family.HasShiStatus`）不由本特性设置【§17①】

**结论**：本特性**只读** `HasShiStatus`（002 的贷款划扣比例），**MUST NOT** 在任何路径写入它——
包括「及第入仕」与「授官」。§10.2 的触发点是「家族出首个**进士**」（科举中式时点，
属逻辑轨 ⑤），与「授官」是两件事。

**依据**：FR-008（任何出身下开局仕身份均 MUST 为假）、FR-021（不得与士出身混淆）；
spec Out of Scope（科举本身属逻辑轨 ⑤）。

**登记的后果**：在逻辑轨 ⑤ 交付之前，家族不会通过本特性获得仕身份；002 的划扣比例
（仕 20% / 工农 40%）在本特性下沿用既有取值，无回归风险。

---

## R-15 结算快照的扩展：`SettlementResult.Career`

**结论**：`SettlementResult` 新增一个 `Career` 字段，承载本次结算的**逐人事件**
（授官 / 政绩增量 / 晋升 / 致仕 / 跳过考课的成员与原因），供 US2~US4 的断言**不必读回
`Person` 的中间态**即可逐步验证 FR-019 的月内次序。它是**可断言快照**，
MUST NOT 成为第二真源（同 002 `SettlementResult` 的既有口径：不存副本、只存本次增量）。

**依据**：FR-019（「该次序 MUST 可被单测逐步断言」）、002 data-model §4.2。

**被否的替代**：为每次授官/致仕额外落一条**账本条目**——账本类别是 §12.3 的**资金与事件**台账，
「授官」不涉资金也不属既有的四类饥馑事件；新增类别会扩大 002 契约四的类别全集，
而本特性没有任何 SC 要求它们出现在账本里（俸禄条目已由既有的 `OfficialSalary` 承载）。

---

## R-16 数值登记与 SC-009 的扫描口径

**结论**：本特性新增的**小数**数值一律进 `Architecture/ConfigLiteralTests.RegisteredValues`：

`0.25`（25% 基础升级率）、`0.003`（每点政绩 +0.3%）、`0.70`（封顶 70%，**与米价 clamp 上限同值，
已在清单中**）、`0.50`（半俸比例，**与一般乘区 −0.50 同值，已在清单中**）；
`60.7` / `62.3`（天命寿数均值）。**整数**数值（6 / 24 / 11 / 13 / 15 / 18 / 36 / 100 / 70 /
23~33 / 0~8 / 80~100 / 90~100 / 10~30 / 12 / 14 / 60）**不入清单**——与 002 已裁决的口径一致：
它们与成员编号、年龄、世代号、夹具金额同值，入单会产出数百处无关命中。

**依据**：002 契约五 §5；`ConfigLiteralRules` 的两条互补判据（数值清单 + 金额构造点）。

**注意**：`0.25` 与 `0.003` 是**新增**小数，落地时必须同时更新
`ConfigLiteralTests.RegisteredValues` 与 [contracts/config-registry.md](./contracts/config-registry.md)，
否则「配置类之外零副本」这条 SC 会出现**假绿**（清单里没有它 → 扫描不到）。

---

## R-17 测试组织与夹具扩展

**结论**：沿用 002 research R-16 的目录语义——`tests/KFL.Tests/Rules/` 放**规则断言**、
`Core/` 放**值类型与聚合不变量**、`Infrastructure/` 放**接缝实现**（三个目录都在 G-07 与
SC-009 的扫描范围内，故其中的数值一律经配置成员取得，只有锚点允许字面量并加行级豁免）。
新增夹具放进既有的 `Rules/RulesTestHarness.cs`：

- `Official(level, monthsInOffice, merit, …)`：在任官员；
- `Awaiting(person, remainingMonths)`：待阙者（位 + 计时成对设置）；
- `PromotionBannedOfficial(remaining)`：禁升期官员（**只消费**，不清算计时）；
- `FixedNameGenerator`：返回固定姓名的 `INameGenerator` 替身（与 `FixedRandomService` 同款）。

开局测试 MUST 使用 `SeededRandomService(固定种子)`（不是 `FixedRandomService`），
因为它的目的正是验证「同种子 → 同结果」；边界测试（待阙恰好 6/24、考课概率两端）
用 `FixedRandomService` 与 `Next` 的替身取值。

**依据**：章程原则 IV；001 契约一 §2.1 的 G-07 扫描范围；002 research R-16。

---

## R-18 文档迁移的剩余项（章程「需求真源」）

**结论**：001/002 的活工件已在 2026-10-08 按规格书 §16.2 迁移完毕
（见本特性 `checklists/requirements.md` 的追记），本特性**只剩三项**：

1. `科举浮生录规格书.md` 新增 §17 裁决回写：§3（前史纪年，Q4）、§4.1（功名记录增「甲第」）、
   §7.5 与 §8.2（服刑期仕途计时暂停，Q5）、§8.2（考课计时口径：授官当月记 1、第 36 个在职月判定）。
2. 001 的 `spec.md`（FR-008 附近）与 `data-model.md`（`DegreeRecord` 小节）各补一句：
   甲第字段由 `003-opening-assets-officialdom` 向后兼容扩展（指向本特性 FR-014）。
3. `KFL.Core` 内的三处注释改标（`AttributeLimits` 的「政绩上限属阶段⑧」、
   `Person.Lifespan`/`Merit` 的「上界属阶段⑧」→ **逻辑轨 ③/⑦**）——它们是被本特性直接触碰的
   产品文件，顺手改标避免误导（纯注释，不触发任何断言）。

**历史记录类工件**（`tasks.md`、`implementation-notes.md`、既有 `checklists/`）**保留旧编号，
MUST NOT 回改**（规格书 §16.2 的迁移口径）。

**依据**：章程「需求真源」（规格书变更 MUST 与受影响工件同步提交）；本特性 spec 的
Assumptions 末两条。

---

## R-19 开局属性口径的残留子口径（【§17①/②】推导，逐条登记但**不阻塞**）

spec 的 Assumptions 已把主要口径裁决完，以下是实现时必须**逐条落到代码**、但规格书未逐字写明的子口径：

1. **孩子天赋**取 §4.2「开局成员」口径 `N(60,20)` ×4（**不是**从父母按遗传公式推导）——
   spec 已裁决，此处只登记落地位置（`NewGameSetup` 对孩子一律调「无父母参照者」的取样路径）。
2. **孩子的体质** `90~100` 区间内**整数均匀**（§10.1 表内「孩子学业 0、体质 90~100」），
   **不**走 `N(85,10)`（那是配偶/买来旁系的口径）。
3. **家主的学业**：士 = 60（**常量，不掷骰**，但**仍消耗姓名与其它随机**）；
   其余出身 = `10~30` 整数均匀。
4. **家长/配偶/孩子的性别**：家主恒男、配偶恒女（§10.1 的「夫妇」）；孩子 50/50——
   实现取 `Next(0, 2) == 0 ? Male : Female`（消耗 1 次）。
5. **士出身的「1 子」**按 1 名孩子处理（性别 50/50），MUST NOT 解读为「必须男孩」（spec 已裁决）。
6. **年龄区间**（`±5`、`0~8`、`80~100`、`90~100`、`10~30`）一律**整数均匀、含两端点**——
   实现用 `IRandomService.Next(min, max + 1)`；`Next` 的上界是**开区间**，
   故 `+1` 的写法 MUST NOT 被漏掉（这是最容易出错的边界）。
7. **天命寿数**只取整、**只 clamp 下界 0、不设上限**（FR-006）；「低于开局年龄」如实记录（spec Edge Case）。
8. **开局档位与米价系数**：取 002 已交付的单点常量，MUST NOT 在本特性再写一遍
   （`LivingCostTable.InitialStandard` 与 `GrainPricePolicy.Initial`）。

**依据**：spec Assumptions；§10.1；§4.2。

---

## 未决事项（不阻塞本阶段，已登记）

| 事项 | 为何不阻塞 | 归属 |
| --- | --- | --- |
| Bogus 适配器的 `DataSet.Randomizer` 赋值通路未能离线核实 | 产品默认走内置生成器；FR-009 的 MUST 已由内置实现满足。回退方案见 R-09 | 实现期自证 |
| `GameDate` 允许负年份后，卡片/筛选器如何显示前史年月 | 显示属界面轨 U2；规则层只回答「何时」 | 界面轨 U2 |
| 科举如何产出「甲第」与特奏名 | FR-012 明令本特性 MUST NOT 自行判定科举细节；本特性只提供入口与校验 | 逻辑轨 ⑤ |
| 禁升计时的建立与逐月递减 | FR-018 明令本特性只**消费** `PromotionBanned` 状态位与计时值 | 逻辑轨 ⑥ |
| 服刑/外嫁的建立（本特性只读其状态位） | 同上；且 002 已交付「计口 = 在册且未服刑」的判定 | 逻辑轨 ⑥/⑦ |
| 存档落盘后如何保证跨 .NET 版本的随机重放 | 与 001 `SeededRandomService` 的既有取舍同款，规格书 §14 的落盘属逻辑轨 ④ | 逻辑轨 ④ |
