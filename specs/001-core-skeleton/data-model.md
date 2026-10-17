# 阶段 1 数据模型：家族领域档案

**Feature**: `001-core-skeleton` | **Date**: 2026-10-05 | **Spec**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

本文件定义 001 交付的领域档案。字段全集来自规格书 §4.1 与 §2 的 `KFL.Core` 实体清单；
**凡规格书未列举、且 001 无消费者的字段一律不建**（章程「复杂度 MUST 被论证」）。
由于存档落盘属阶段④，此时增删字段无迁移成本，因此宁可后置，不可预置。

---

## 1. 值类型（`KFL.Core`）

### 1.1 `GameDate`（readonly record struct）

| 成员 | 类型 | 说明 | 来源 |
| --- | --- | --- | --- |
| `Year` | `int` | 架空纪年；接受 `int` 全域，`<= 0` 为**前史纪年**（存档开始之前的世界历史） | §3「从 1 年 1 月开始推演」；§17 裁决回写（2026-10-08，Q4）|
| `Month` | `int` | 1~12，无闰月 | §3「1 回合 = 1 游戏月，1 年 = 12 月」 |

**不变量**：`1 <= Month <= 12`；**年份无构造期约束**（接受 `int` 全域，含 `0` 与负数）。
构造即校验，**只有月份越界**抛 `ArgumentOutOfRangeException`。

**为什么年份可以早于 1 年**（§17 裁决回写，2026-10-08，Q4）：§10.1 要求开局家主 28±5 岁、
配偶 25±5 岁、孩子 0~8 岁，而年龄只能由 `BirthDate` 派生（不落裸年龄字段，见 §2.1 不变量 5），
从「1 年 1 月」起推演时开局家人的**父母辈必然出生于 1 年 1 月之前**。故年份放宽为前史纪年，
月份仍 MUST 为 1~12；`GameState.CurrentDate` 仍从 **1 年 1 月**起推演（见 §2.3），
`ElapsedMonths` / `AgeInYearsAt` / 比较运算符的语义不变。
**行为**：`CompareTo` / 比较运算符；`ElapsedMonths(GameDate other)` 返回月差；
`AgeInYearsAt(GameDate at)` 返回「已满几周岁」（生日当月即计入，§4.3）。
**明确不含**：12/14 等成年年龄、生日月份之外的任何规则数值（成年判定属阶段②）。

### 1.2 `PersonId`（readonly record struct）

包装 `Guid` 的强类型标识。`Person` 之间的全部引用都用它，杜绝 `Guid` 裸传。
`Guid.Empty` 不合法（构造即校验）。

### 1.3 枚举

| 类型 | 取值 | 来源 |
| --- | --- | --- |
| `Gender` | `Male`, `Female` | §4.1「性别」；§4.2「性别 50/50」 |
| `DegreeLevel` | `BaiShen`(白身), `JuRen`(举人), `GongShi`(贡士), `JinShi`(进士) | §6 功名链 |
| `ImperialPlacement` | `ZhuangYuan`(状元), `BangYan`(榜眼), `TanHua`(探花) | §6 进甲名次；§12.2 名次着色 |
| `DegreeChangeCause` | `Initial`(开局或买功名婚姻带入), `ExamPass`(科举中式), `PunishmentDemotion`(§7.4 连坐降级), `DebugEdit`(§13.2 控制台改写) | §4.1 功名变迁历史 |
| `Occupation` | `None`, `Studying`(读书), `Farming`(务农), `Crafting`(做工), `Trading`(经商) | §4.1「职业指派」；§4.3 读书指派；§5.2 收入来源 |
| `Origin` | `Farmer`(农), `Artisan`(工), `Merchant`(商), `Scholar`(士) | §10.1 四出身 |
| `Difficulty` | `Easy`(简单), `Normal`(普通), `Hard`(困难), `Hell`(地狱) | §11；序：简单 < 普通 < 困难 < 地狱 |
| `StatusFlag` | `[Flags]`：`Ill`(患病), `Famine`(饥馑), `ServingSentence`(服刑), `AwaitingPost`(待阙), `ExamBanned`(禁考), `PromotionBanned`(禁升), `MarriedOut`(外嫁), `Retired`(致仕), `Deceased`(已亡) | §4.1 九种状态；§7.5 可并存 |

`Occupation` 的 §17 裁决：§5.2 的「自耕」与「务农（无田雇工）」不拆成两个枚举值——二者的
区别由田地持有量派生，属阶段②；本阶段只保留一个 `Farming`，不新增规格书未列出的职业。

### 1.4 `TalentSet`（readonly record struct）

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `Agriculture` | `int` | 农 |
| `Commerce` | `int` | 商 |
| `Officialdom` | `int` | 仕 |
| `Craft` | `int` | 工 |

**不变量**：四项各自 `0 <= v <= 100`（§4.1「0~100」；FR-005）。整组不可变——「出生即定，
终生不可通过游戏行为升降，仅调试控制台可修改」（§4.1）。
「仅调试控制台可修改」的落地方式属界面轨 U4；本阶段只保证**不存在**常规写入通道。

### 1.5 `DegreeRecord`（readonly record struct）—— 一条功名变迁记录

| 成员 | 类型 | 说明 | 来源 |
| --- | --- | --- | --- |
| `Level` | `DegreeLevel` | 变化后的功名：白身 / 举人 / 贡士 / 进士 | §6 |
| `Placement` | `ImperialPlacement?` | 一甲名次，仅进士可有 | §6 |
| `ChangedAt` | `GameDate` | 本次变化的年月（变迁历史的时间轴） | §4.1 |
| `Cause` | `DegreeChangeCause` | 变化原因 | §4.1 |
| `Class` | `ImperialClass?` | **003 新增**：甲第（一甲 / 二甲 / 三甲）；`null` = 该记录**未表达**甲第。**构造参数名是 `imperialClass`，读取口径是属性 `Class`**——二者是同一成员的两个名字，`ImperialClass` 只是枚举类型名 | 003 FR-014 |

**不变量**：`Placement is not null` ⇒ `Level == DegreeLevel.JinShi`（§6：进甲者才是状元/
榜眼/探花）。

> **引用注（005 科举周期）**：一甲名次 `Placement` 非空 ⟺ 甲第 `Class = ImperialClass.FirstClass`。
> 005 是**该配对的首个生产者**——中第经既有 `Person.AppendDegree` 追加，`Cause = ExamPass`；
> 一甲 `FirstClass` 且带名次，二甲/三甲 `Class` 非空而名次为空（005 契约十三 §6 条款 2/7，
> 005 `data-model.md` §2.2）。005 之前的产品写入路径（开局 `Initial` 记录、存档复原）
> 从未同时置起这两个成员。

> **引用注（006 惩罚）**：006 是 `DegreeChangeCause.PunishmentDemotion` 的**首个产品生产者**——
> §7.4 的连坐降级经既有 `Person.AppendDegree`（`Cause = PunishmentDemotion`）追加一条降一级的
> 功名记录（进士→贡士→举人→白身；由举人再降则追加一条 `Level = BaiShen` 的合法历史条目），
> **MUST NOT 改写或删除既有记录**（不变量 7）。006 之前该原因值只由枚举声明与存档复原触及，
> 无任何产品路径产生它（006 FR-020；[契约十六 §1](../006-bribery-punishment/contracts/punishment.md)）。

**为什么必须带日期与原因**：§4.1 要求功名以「变迁历史」呈现，而 §7.4 的连坐会
**降一级功名**（进士→贡士→举人→白身）。若只记录「考试考成功的功名」，被降级者的历史
末条会一直写着「进士」，与其真实功名矛盾——历史本身就成了假的。带 `Cause` 后，一次中式
与一次降级都能被如实读出。举人被降为白身会追加一条 `Level = BaiShen` 的记录，这是
**合法的历史条目**，与「尚无记录（出生即白身）」是两回事。

**向后兼容扩展（2026-10-08，003；构造参数/属性名口径 2026-10-11 澄清）**：`DegreeRecord`
新增第 5 个**可选**成员 `ImperialClass? imperialClass = null`（甲第：一甲 / 二甲 / 三甲），
由 `003-opening-assets-officialdom` 引入，见该特性 FR-014 与该特性 `data-model.md` §1.3。
**名分说明**：`imperialClass` 是**构造参数名**，公开读取通道是**只读属性 `Class`**；
`ImperialClass` 仅为 `KFL.Core/Enums/ImperialClass.cs` 的**枚举类型名**，
MUST NOT 被当作属性名引用。既有**四参调用保持合法**；上表前四项的语义与不变量不变
（一甲名次非空 ⇒ 甲第 = 一甲）。第 5 项的完整不变式（①~④）归 003 `data-model.md` §1.3，
本表不复制副本。

### 1.6 `OfficialRank`（readonly record struct）

包装 1~18 的级别（`Level`），**不表示「无官职」**——无官职由 `Person.Rank is null` 表达
（FR-009）。**不变量**：`1 <= Level <= 18`（§8.1 十八级）。
年俸数值表（72~5100 贯）属逻辑轨 ③，本阶段不落任何俸禄常量。

### 1.7 `StatusTimers`（readonly record struct）

| 成员 | 类型 | 说明 | 来源 |
| --- | --- | --- | --- |
| `SentenceRemainingMonths` | `int?` | 服刑剩余月数 | §7.5「服刑以月为单位（5 年 = 60 月）」 |
| `ExamBanRemainingMonths` | `int?` | 禁考剩余月数 | §7.5「禁考与服刑独立并行计时」 |
| `PromotionBanRemainingMonths` | `int?` | 禁升剩余月数 | §7.5、§8.2「禁升来源…独立计时器」 |
| `AwaitingPostRemainingMonths` | `int?` | 待阙剩余月数（**003 新增**，逻辑轨 ③） | §8.2 待阙 6~24 月；003 FR-013 |
| `IllnessSeverityBasisPoints` | `int?` | 疾病程度 `s` 的磅值（`0~10000`，**007 新增**，逻辑轨 ⑦） | §4.3 疾病；007 FR-028 / FR-047 |
| `TreatmentRemainingMonths` | `int?` | 疗程剩余月数（`0~3`，**007 新增**，逻辑轨 ⑦） | §4.3 药费与疗程；007 FR-029 / FR-047 |

**不变量**：六个字段各自仅在对应状态位为真时可非空；非空值 MUST `>= 0`；
`IllnessSeverityBasisPoints` 与 `Ill` 位**同生同灭**（`StatusFlag.Ill` ⇔ 程度非空），
`TreatmentRemainingMonths` **只**在 `Ill` 置位时非空（`null` = 未在疗程）。
待阙项与 `Person.EntryTrack` **同生同灭**（见 §2.1 不变量 4），且 MUST NOT 以「0 + 状态位为真」的形态存续。
**明确不含**：待阙的递减逻辑——「递减到 0 的当月授官」属逻辑轨 ③
（`OfficialCareerAdvance`），其 6~24 区间属 `OfficialCareerPolicy`。
**服刑/禁考/禁升三项已由 006（逻辑轨 ⑥）建立与递减**（原列于本「明确不含」）——计时非空 ⟺
对应状态位（`ServingSentence` / `ExamBanned` / `PromotionBanned`）为真；递减发生在月末结算**第③步**
`PunishmentAdvance.Run(state)`，服刑递减到 0 即释放（清状态位与计时，`Rank is not null` 则官阶 +1）；
服刑期间待阙计时/在职计时/政绩**暂停**（006 FR-020；[契约十六 §1](../006-bribery-punishment/contracts/punishment.md)）。

> **引用注（005 科举周期）**：005 是**待阙计时与其状态位配对的首个产品生产者**——
> 殿试及第当月经 003 的 `AppointmentEntry.BeginForImperialGraduate` 置入
> `AwaitingPostRemainingMonths` 与 `StatusFlag.AwaitingPost`（**当月不递减**，次月起走；
> 005 契约十三 §7.1）。另：本结构在 005 的资格判定中**只被读取**（`ServingSentence` /
> `ExamBanned` / `AwaitingPost`），MUST NOT 递减或新建（005 契约十二 §3.2）。

---

## 2. 聚合与实体（`KFL.Core`）

### 2.1 `Person`（成员档案）

| 字段 | 类型 | 可变 | 来源 |
| --- | --- | --- | --- |
| `Id` | `PersonId` | 否 | 引用完整性所需 |
| `Name` | `string` | 是 | §4.1「姓名」（§12.4 姓氏仅影响 Last Name，生成属后续阶段） |
| `Gender` | `Gender` | 否 | §4.1 |
| `Generation` | `int`（>= 0） | 血亲：否；外来者：落定一次 + 首次家族内成婚时额外变动一次 | §4.1「辈分」；§4.4；§12.1「一层一代」 |
| `BirthDate` | `GameDate` | 否 | §4.3「生日当月转成年」；年龄由它派生（R-07） |
| `Talents` | `TalentSet` | 否 | §4.1 天赋四项 |
| `Study` | `int`（0~100） | 是 | §4.1「学业」 |
| `Health` | `int`（0~100） | 是 | §4.1「体质」 |
| `Lifespan` | `int`（>= 0，年） | 否 | §4.1「天命寿数：出生瞬间 roll 出…卡片上显示」 |
| `DegreeHistory` | `IReadOnlyList<DegreeRecord>` | 追加式 | §4.1 功名变迁历史；§6 |
| `CurrentDegree` | `DegreeLevel`（派生，只读） | 否 | = `DegreeHistory` 末条 `Level`；空则白身 |
| `CurrentPlacement` | `ImperialPlacement?`（派生，只读） | 否 | = `DegreeHistory` 末条 `Placement`；空则 `null` |
| `Rank` | `OfficialRank?` | 是 | §8 官阶；`null` = 无官职 |
| `Merit` | `int`（>= 0） | 是 | §4.1「政绩」；§8.2 上限 100 属规则（逻辑轨 ③） |
| `Status` | `StatusFlag` | 是 | §4.1 九种状态，可并存 |
| `Timers` | `StatusTimers` | 是 | §7.5 |
| `Occupation` | `Occupation` | 是 | §4.1「职业指派」 |
| `MonthsInOffice` | `int`（>= 0） | 是 | **003 新增**（逻辑轨 ③）：在任月数，自授官起算；考课周期 36 属 `OfficialCareerPolicy` |
| `EntryTrack` | `AppointmentTrack?` | 是 | **003 新增**（逻辑轨 ③）：待阙期记录的入仕途径，与「待阙」同生命周期；授官时读它定初始官阶 |
| `FatherId` / `MotherId` | `PersonId?` | 否 | §4.1 父母引用 |
| `SpouseId` | `PersonId?` | 是 | §4.1 婚姻关系；一夫一妻（§9.1） |
| `FormerSpouseIds` | `IReadOnlyList<PersonId>` | 是 | §9.1「丧偶可再婚」+ spec Edge Case（须能表达既往婚姻） |
| `JieShiFailureCount` | `int`（>= 0） | 是 | **005 新增**：解试累计失败数（单调递增）；**只读、无公开 setter**（写入通道见不变量 2）——005 FR-023；契约十二 §2 |
| `ShengShiFailureCount` | `int`（>= 0） | 是 | **005 新增**：省试累计失败数（单调递增）；**只读、无公开 setter**（写入通道见不变量 2）——005 FR-023；契约十二 §2 |
| `AppliedExam` | `ExamLevel?` | 是 | **005 新增**：报名标记，`null` 或 ∈ {`JieShi`, `ShengShi`}；**殿试不是合法报名态**；**当月态**，结算结束后必为 `null`；**只读、无公开 setter**（写入通道见不变量 2）——005 FR-005、FR-023；契约十二 §2 |
| `JieShiBribeCount` | `int`（>= 0） | 是 | **006 新增**（逻辑轨 ⑥）：解试行贿累计次数；**跨科年累积、单调递增**；**只读、无公开 setter**（写入通道见不变量 2）；**连坐降级 MUST NOT 清空**——见 [契约十六](../006-bribery-punishment/contracts/punishment.md) |
| `ShengShiBribeCount` | `int`（>= 0） | 是 | **006 新增**（逻辑轨 ⑥）：省试行贿累计次数；**跨科年累积、单调递增**；**只读、无公开 setter**（写入通道见不变量 2）；**连坐降级 MUST NOT 清空**——见 [契约十六](../006-bribery-punishment/contracts/punishment.md) |
| `DianShiBribeCount` | `int`（>= 0） | 是 | **006 新增**（逻辑轨 ⑥）：殿试行贿累计次数；**跨科年累积、单调递增**；**只读、无公开 setter**（写入通道见不变量 2）；**连坐降级 MUST NOT 清空**——见 [契约十六](../006-bribery-punishment/contracts/punishment.md) |

> **「可变」列的含义**：指该字段的**值**会随推演时间改变，**不表示 `Person` 暴露公开
> setter**。写入通道只有两类，见不变量 2 与 §2.2 不变量 4：跨实体引用（配偶、父母、
> 辈分）的唯一入口是 `Family`；`Person` 自持的字段由 `Person` 自己暴露可写属性。
> 不变量 2 列出了**全部**无写入通道的成员，因此「谁有 setter」无需从本列推断。

**公开行为（非字段，T020 一并实现）**

| 成员 | 签名 | 作用 | 来源 |
| --- | --- | --- | --- |
| `IsOutsider` | `bool`（只读） | `FatherId` 与 `MotherId` 皆 `null`，即 §4.4 定义的外来者（开局成员、娶入配偶、买来的旁系） | §4.4；§9.5 |
| `AgeAt(GameDate)` | `int` | = `BirthDate.AgeInYearsAt(at)`；年龄不落裸字段（不变量 5、R-07） | §4.3；R-07 |
| `AppendDegree(DegreeRecord)` | `void` | `DegreeHistory` 的**唯一**追加入口，强制 `ChangedAt` 非降序（不变量 7）；`Person` 不暴露可写的历史列表 | FR-008；§6 |

**不变量**
1. `Talents`、`Study`、`Health` 落在 0~100（FR-005、FR-006）。
2. **无写入通道的成员（全集，测试须逐个反射断言）**：`Id`、`Gender`、`BirthDate`、
   `Talents`、`Lifespan`、`Generation`、`FatherId`、`MotherId`、`SpouseId`、
   `FormerSpouseIds`、`DegreeHistory`、`CurrentDegree`、`CurrentPlacement`、
   `JieShiFailureCount`（005 新增）、`ShengShiFailureCount`（005 新增）、
   `AppliedExam`（005 新增）、`JieShiBribeCount`（006 新增）、`ShengShiBribeCount`（006 新增）、
   `DianShiBribeCount`（006 新增）——均为只读属性，**不存在公开 setter**
   （FR-005、FR-007、FR-008、§4.4；005 FR-023；006 契约十六）。
   **`Person` 自持的可写属性**只有十一个：`Name`、`Study`、`Health`、`Rank`、`Merit`、
   `Status`、`Timers`、`Occupation`、`MonthsInOffice`（003 新增）、`EntryTrack`（003 新增）、
   `IsTutoredByMaster`（007 新增，布尔标志「是否延请名师」，与 `TutorFee` 支出同源；复原走
   `internal RestoreTutoring(bool)`，唯一程序集面为 `InternalsVisibleTo("KFL.Infrastructure")`）。
   分界依据：凡「出生即定」或「跨实体一致性」的字段一律只读——前者如 `Gender` /
   `Talents` / `Lifespan`，后者如 `SpouseId` / `FatherId` / `MotherId` / `Generation`
   （唯一入口在 `Family`，见 §2.2 不变量 4）。
   **005 的三个只读成员另有受控写入通道（公开面唯一）**：`ApplyForExam(ExamLevel)`
   （拒绝 `DianShi`；同级别幂等；冲突级别抛 `InvalidOperationException`）、
   `ClearExamApplication()`（`null` 时 no-op）、`RecordExamFailure(ExamLevel)`
   （对应计数 +1；`DianShi` 抛 `InvalidOperationException`，殿试不掷录取骰）；复原走
   `internal RestoreExamProgress(int, int, ExamLevel?)`——与 `SetGeneration` / `SetSpouse` /
   `AddFormerSpouse` 同款，唯一程序集面为 `InternalsVisibleTo("KFL.Infrastructure")`。
   除这三者与复原通道外 MUST NOT 新增公开 setter（005 FR-023；契约十二 §2）。
   **006 的三个只读成员另有受控写入通道（公开面唯一）**：`RecordBribe(ExamLevel)`
   （对应计数 +1；非法级别抛异常）、`ResetBribeCounts(ExamLevel?)`（**科年**边界重置；传 `null` 即三项全清）；
   复原走 `internal RestoreBribeCounts(int, int, int)`——与 005 的 `RestoreExamProgress` 同款，
   唯一程序集面为 `InternalsVisibleTo("KFL.Infrastructure")`。**连坐降级（`PunishmentDemotion`）
   MUST NOT 清空**这三个计数（它们记录的是行贿**历史次数**，而非当科状态）
   （006 [契约十六](../006-bribery-punishment/contracts/punishment.md)）。
3. `FatherId != Id`、`MotherId != Id`、`SpouseId != Id`；`FormerSpouseIds` 不含 `SpouseId` 且不重复。
4. `Timers` 与 `Status` 一致（见 1.7）；003 新增的 `EntryTrack` 同向一致
   （非空 ⇒ `AwaitingPost` 位为真；清除该位前 MUST 先置 `null`——`Timers` 的
   `AwaitingPostRemainingMonths` 与它 MUST 同生同灭）。
   **005 引用注**：科举季的资格判定**只消费** `ServingSentence` / `ExamBanned` /
   `AwaitingPost` 三个状态位，MUST NOT 递减或新建本结构的任何计时（005 契约十二 §3.2）；
   005 新增的 `AppliedExam` 是**当月态**、与状态位无关，本月结算结束后必为 `null`
   （005 FR-005；契约十三 §1.2）。
   **006 引用注**：006 是**服刑/禁考/禁升三项计时与其状态位的首个产品生产者与递减者**——
   `PunishmentAdvance.Run(state)`（月末结算第③步）逐人递减三计时，并在计时归零时清掉对应状态位
   （服刑释放另按 `Rank is not null` 官阶 +1）；「计时非空 ⟺ 状态位为真」这一一致式由 006
   首次在产品路径上双向兑现；服刑期间待阙计时/在职计时/政绩**暂停**（006 FR-020；
   [契约十六 §1](../006-bribery-punishment/contracts/punishment.md)）。
   **007 引用注**：007 新增的两项计时（`IllnessSeverityBasisPoints` 0~10000、`TreatmentRemainingMonths`
   0~3）同款遵守「计时非空 ⟺ 状态位为真」：`Ill` 位 ⇔ `IllnessSeverityBasisPoints != null`；
   `TreatmentRemainingMonths` 仅在患病期内非空、且**宽限月不落字段**（确诊当月不付药费）。
   `Person.Status` / `Person.Timers` 的交叉校验已按此双向落地（「先置位、后写计时；先清计时、后清位」）。
   患病与饥馑的体质扣减、成长与遗忘、寿终与夭折由 007 的 ⑧-c~⑧-f 月度驱动，007 是这些字段的
   首个产品生产者与消费者（007 FR-041~FR-044）。
5. **年龄不落裸字段**：`AgeAt(GameDate)` = `BirthDate.AgeInYearsAt(at)`（R-07）。
6. 出生时辰与「已亡」时间不落字段：§4.1 未列，且 §12.1 的归档排序属界面轨 U2。
   **007 引用注**：死因（`DeathCause`：寿终/病亡/饥馑/夭折/分娩）同样**不落 `Person` 字段、不落账本**，
   只承载于**当月**的 `SettlementResult.Lifecycle.Deaths` 事实段（007 FR-043、[data-model §3.1](../007-family-lifecycle/data-model.md)）。
7. `DegreeHistory` 按 `ChangedAt` **非降序**（追加式）；既有记录 MUST NOT 被改写或删除。
   `CurrentDegree` / `CurrentPlacement` MUST 由末条派生，MUST NOT 另存独立字段——否则
   §7.4 的连坐降级会让两处数据失配（见 1.5）。
   **005 引用注**：005 是本仓库**首个经 `Person.AppendDegree` 追加 `ExamPass` 记录的产品路径**
   （一次完整链条白身 → 举人 → 贡士 → 进士恰好追加 3 条），一甲名次与甲第的配对由此**首次**
   被生产（005 契约十三 §6）。
8. `Generation` 的分层规则（§4.4）：血亲成员（`FatherId` 或 `MotherId` 至少一方为本家族
   成员）的辈分 = 父母辈分 + 1，出生即定、**终身不可变更**；外来者（`FatherId` 与
   `MotherId` 皆 `null`——开局成员、娶入配偶、§9.5 买来的旁系）的辈分由 `Family` 指定，
   且 MUST 在其**尚无子女**时落定。外来者的辈分**总共只变动两次**：`Family` 指定（落定）一次，
   其**首次家族内成婚**时对齐配偶辈分一次；此后 MUST NOT 再变更（2026-10-05 裁决）。
   `Person` 不提供辈分的公开写入通道。
   **开局成员的辈分初值 = 0**（§4.4「辈分自创始者为 0」），因此「买来的旁系 = 家主辈分
   + 1」在开局家主治下取 **1**，娶入配偶取其家族内配偶的辈分。夹具与断言据此推导绝对值；
   001 MUST NOT 另行规定其他初值。

**明确不含**：`ChildIds`（由 `Family` 派生，见 R-08）；`IsShiIdentity`（仕身份为家族级，
见 2.2）；`Origin`（出身为存档级，见 2.3）；逐月收支记录（R-16：账本归 `GameState`，
属阶段②，挂到实体上会让永久归档的成员无界增长）。
`Lifespan` **只约束下界 `>= 0`，没有上界**——天命寿数的分布（§4.2）**已由 007（⑦）交付**
（`AttributePolicy.NextLifespan`，均值 60.7/62.3、σ = 8，只 clamp 下界），001 MUST NOT
自设上限；「无上界」是有意留白，不是遗漏（曾因只写下界而被读成设计缺失）。
007 交付后，本节的「成长、衰老、疾病、死亡的年龄线与概率不含」旧口径一并作废：
年龄档跨越（⑧-d）、患病与病程（⑧-c）、饥馑体质损失（⑧-e）、寿终与夭折（⑧-f）均为产品路径。

### 2.2 `Family`（家族）

| 成员 | 类型 | 说明 | 来源 |
| --- | --- | --- | --- |
| `Name` | `string` | 家族姓氏（§12.4） | §12.4 |
| `HasShiStatus` | `bool` | 「仕身份」= 进士直系血统，**与出身独立可并存** | §10.2；spec Edge Case |
| `HeadId` | `PersonId?` | **家主**（§4.4）；`null` = 家族内无在册男性成员 | §4.4 |
| `Members` | `IReadOnlyCollection<Person>` | 全部成员（含已归档），按 `PersonId` 索引 | §12.1「死亡成员自动归档」 |
| `TryGet(PersonId)` | 方法 | 按标识取成员 | FR-011 |
| `ChildrenOf(PersonId)` | 方法 | 派生子女引用 | FR-011、SC-006 |
| `SpouseOf(PersonId)` | 方法 | 配偶 | FR-011 |
| `RegisteredMembers` | 属性 | **在册** = 非「已亡」且非「外嫁」 | §12.1、§15 |
| `ArchivedMembers` | 属性 | 已归档 = 已亡 或 外嫁 | §12.1 |
| `AutoExamApplication` | `bool` | **005 新增**：持续报考开关，默认 `false`，**可写**（形状与 `HasShiStatus` 同款）；为真时科举季步按资格自动报名，MUST NOT 覆盖已有报名标记 | 规格书 §3；005 FR-017 |
| `Lifecycle` | `FamilyLifecycleState` | **007 新增**：家族生命周期簿记（本年度买人口计数与年份 / 待分娩记录 / 本年度已申请生育夫妇 / 待应答提亲提议 / 当月托媒申请），只读聚合属性（`=> _lifecycle`），写入一律经其自身方法与 `internal` 落盘通道 | 规格书 §9.1~§9.5；007 FR-047、007 `data-model.md` §2.3 |

**变更入口（`Family` 是跨实体唯一写入方，§4.4 辈分与 §9.1 一夫一妻的强制点）**

| 成员 | 签名 | 规则 | 来源 |
| --- | --- | --- | --- |
| `BoughtCollateralGeneration` | `int`（派生） | = 家主辈分 + 1；`HeadId` 为 `null` 时抛异常（无在册男性成员则无从推导） | §4.4；§9.5 |
| `AddFoundingMember(Person)` | `Person` | 开局成员：辈分 MUST 为 0、父母引用 MUST 为 `null` | §4.4 |
| `AddChild(Person)` | `Person` | 血亲成员：父母至少一方为本家族成员，辈分 MUST = 该父母辈分 + 1 | §4.4 |
| `AddOutsider(Person)` | `Person` | 外来者：父母引用 MUST 为 `null`，辈分由 `Family` 指定 | §4.4；§9.5 |
| `CaptureOutsiderBookkeeping()` | `internal OutsiderBookkeeping` | **004 新增**（内部面）：返回两处外来者簿记（`_settledOutsiders` / `_marriageAlignedOutsiders`）的瞬时快照，供存档落盘；返回顺序**按标识排序**（非哈希遍历顺序），以保证「同一状态两次落盘字节一致」 | 004 `research.md` R-03；004 FR-003/FR-017 |
| `RestoreOutsiderBookkeeping(OutsiderBookkeeping)` | `internal void` | **004 新增**（内部面）：把快照落回两处簿记（存档复原路径）；只在全部成员已注册后调用，簿记标识 MUST 指向本家族成员，否则抛 `ArgumentException`；只落值、**不**校验「簿记与辈分是否自洽」（该一致性归存档层 `SaveStateValidator`） | 004 `research.md` R-03；004 FR-003/FR-017 |
| `SetOutsiderGeneration(PersonId, int)` | `void` | 外来者辈分的**落定**入口；拒绝血亲成员、拒绝**已有子女**者、拒绝**已落定**者（每名外来者至多落定一次） | §4.4 |
| `Marry(PersonId, PersonId)` | `void` | 一夫一妻（不变量 2）；外来者一方在其**首次**家族内成婚时对齐配偶辈分（这是落定之外的额外那一次），非首次 MUST NOT 再变动 | §9.1；§4.4 |
| `EndMarriage(PersonId)` | `void` | 丧偶/离异两步走的第一步：置空 `SpouseId` 并追加进 `FormerSpouseIds` | §9.1 |
| `SetHead(PersonId?)` | `void` | `null` 或本家族**在册**成员（不变量 6）；**001 不校验性别**——继任判定属逻辑轨 ⑦ | §4.4 |

**不变量**
1. 成员标识唯一；`PersonId` 引用要么指向本家族成员，要么为 `null`（**无悬挂引用**）。
2. 配偶关系双向一致：`a.SpouseId == b.Id` ⇔ `b.SpouseId == a.Id`（US1 AS2）；且
   **至多一人**——已有配偶（`SpouseId is not null`）的成员被指定第二个配偶时 MUST 被拒
   （§9.1 一夫一妻）。丧偶再婚 MUST 走「先把 `SpouseId` 置 `null` 并追加进
   `FormerSpouseIds`，再指定新配偶」两步，既往配偶的记录 MUST NOT 被覆盖（§9.1「丧偶后
   可再婚」+ spec Edge Case）；再婚 MUST NOT 改动任何子女的 `FatherId`/`MotherId`。
3. 父母引用为向无环：沿父系主轴向上遍历必然终止（SC-006、Edge Case 无自环）。
4. 只有 `Family` 能改配偶、父母引用、辈分与家主；`Person` 不提供公开写入通道（章程原则 II：
   实体不承担跨实体编排）。
5. `HasShiStatus` 与任何 `Origin` 可任意组合（§10.2：出身终身特性与仕身份叠加并存）。
   005 起该字段有了**首个生产者**：家族产生首个进士的当月由科举季步置 `true`（幂等，
   仅首次记入快照 `HasShiStatusBecomesTrue`）——见 005 契约十三 §7.2、005 FR-020。
6. `HeadId` 要么为 `null`（家族内无在册男性成员——该情形**不构成绝嗣**，§15 判定与性别
   无关），要么指向本家族的**在册**成员；MUST NOT 指向已亡或外嫁的已归档成员（§4.4）。

   **强制点与豁免（2026-10-05 裁决）**：该不变量在 001 内**只在 `SetHead` 当时强制**。
   「成员在任内被归档（`Deceased` / `MarriedOut`）后如何修正 `HeadId`」属**逻辑轨 ⑦的继任判定**
   （家主亡故后按 §4.4 的继任顺序产生新家主，届时会有相应的事件通知）。001 **不**为此增设
   归档回调或事件机制：001 的产品代码没有任何归档推进路径，为假想的未来需求预置结构违反
   章程「复杂度 MUST 被论证」。因此 001 只保证「任命那一刻合法」，**不保证**「任内被归档后
   立刻合法」；任何经 `Person.Status` 的公开写入通道自行归档在任家主的调用方，会让该不变量
   暂时失效——这是**已知且已登记**的阶段边界，不是隐藏缺陷。
   `BoughtCollateralGeneration` 在被调用时以 `HeadId` 的当前指向为准（不另行判活在册性）。
   **007 落实（豁免失效）**：007（⑦）交付 `HeadSuccession` 并把继任判定接入月度结算的 **⑧-h**——
   每月最多一次，在 ⑧-g 把死亡落到 `Status`/归档**之后**取候选并 `SetHead`；无候选时置 `null`
   并标记「家主空缺」。故自 007 起「任内被归档后月底前修正」已是产品路径：
   ⑦ **仍未**引入归档回调或事件机制，靠月度驱动的幂等式修正兑现（007 FR-041、FR-046；
   [007 data-model §3.5](../007-family-lifecycle/data-model.md)）。
7. 辈分的指定与变更入口唯一在 `Family`（同第 4 条）：血亲成员的辈分一经确定 MUST NOT
   变更；外来者（娶入配偶、§9.5 买来的旁系）的辈分 MUST 在其尚无子女时落定（§4.4），
   且**总共只变动两次**——落定一次 + 首次家族内成婚时对齐配偶辈分一次，此后 MUST NOT 再变更。

**§17 裁决**: `HasShiStatus` 建在家族级而非成员级——§10.2 的效果（录取率 ×1.1、婚嫁规格、
贷款划扣 20%）在本项目「一存档一家族」的模型下由家族统一承载；嫁入配偶的差异在逻辑轨 ⑦
真正实现该效果时再细分，本阶段不预置按人标记。
**007 落实**：⑦（007）**刻意未**按人细分仕身份——婚嫁规格、买功名婚姻与榜下捉婿的压力权重
一律只读家族级 `HasShiStatus` 与成员级 `Rank`（[007 contracts/marriage-birth.md §1](../007-family-lifecycle/contracts/marriage-birth.md)），
「嫁入配偶的仕身份差异」由「配偶自身的 `Rank` / 功名快照」表达，MUST NOT 新增家族声望或按人仕标记。

**005 落实（首个生产者）**: `Family.HasShiStatus` 由科举季步（`ExamSeasonRunner`）在家族
产生**首个进士**的当月置 `true`——幂等，已为 `true` 时 no-op，快照仅以
`HasShiStatusBecomesTrue` 记录**首次**为真（005 契约十三 §7.2；005 FR-020）。
这是该字段在本仓库中的**首个写入者**；003~004 只有读取者（贷款划扣、录取率加成）与
存档复原路径。

**家主的分期**: `Family.HeadId` 进 001——它在 001 内**有真实消费者**：§4.4 的辈分规则
要求「买来的旁系辈分 = 家主辈分 + 1」。**继任判定不进 001**：它由死亡推进触发，属
逻辑轨 ⑦——**已由 007 交付**（`HeadSuccession` + 月度结算 ⑧-h；四档候选与排序见
[007 contracts/illness-lifespan.md §7.4](../007-family-lifecycle/contracts/illness-lifespan.md)）。
001 只保证该字段存在、且「为 `null` 或指向在册成员」这一不变量可被校验。
经核验，§4.4 的继任规则所需数据 001 已**全部提供**，无需为此再加字段：`Gender`、
`BirthDate`（年龄）、`ChildrenOf`（子代数量与排序）、`Generation`、`Status`。

### 2.3 `GameState`（存档级状态）

| 字段 | 类型 | 说明 | 来源 |
| --- | --- | --- | --- |
| `Id` | `Guid` | 创建存档时初始化，**改档名不影响**（该子句属阶段④ 验证——001 无落盘，只校验非空与无公开 setter，见 FR-012；**已交付并验证（004）**，见 [004 `contracts/save-service.md` §6](../004-save-system/contracts/save-service.md) 与 004 SC-004）；由注入的 `IRandomService` 生成（16 字节 → Guid 的转换落在 `KFL.Infrastructure/Services/GameStateFactory.cs`——`KFL.Core` 看不到接缝，见 R-03） | §14；FR-012；R-03 |
| `CurrentDate` | `GameDate` | 起始 = 1 年 1 月 | §3 |
| `Difficulty` | `Difficulty` | 同一存档内可随时切换（切换逻辑属逻辑轨 ⑨） | §11 |
| `Origin` | `Origin` | 创建存档时选择 | §10.1 |
| `Family` | `Family` | 当前家族 | §2 |
| `Economy` | `FamilyEconomy` | **002 新增**（逻辑轨 ②）：资金池、账本、持有物与饥馑状态。只读属性——一切资金流动经它的聚合方法落条目（SC-005） | 002 `data-model.md` §3.7；R-13 |
| `PendingDifficulty` | `Difficulty?` | **002 新增**（逻辑轨 ②）：难度「次月生效」的待生效位，结算开头提升并清空 | 002 `data-model.md` §3.7；R-13 |

**不变量**：`Id != Guid.Empty`；`CurrentDate.Month` 合法。

> **写入通道**：`CurrentDate` **只读**——唯一推进通道是 `AdvanceMonth()`（一次一个月、跨年进位）。
> 001 内除构造参数外没有第二个写入者，002 起由 `MonthlySettlementEngine` 在结算末尾调用；
> 因此**任何**跨月推进都 MUST 走 `AdvanceMonth()`，MUST NOT 重新引入公开 setter 或 `internal set`
> （002 R-03/R-13 的边界守卫会拒绝）。`Economy` 只读；`Difficulty` 与 `PendingDifficulty` 可写
> （切换逻辑属逻辑轨 ⑨）；其余字段只读。

**明确不含**：`GameState` **自身**不直接暴露资产池、商本、现金/储蓄/贷款、账本与统计容器
——这些一律只在 `Economy`（`FamilyEconomy`）聚合之下（002 已交付；spec Out of Scope 的对应条目随之失效）。

### 2.4 `OutsiderBookkeeping`（家族簿记快照，**004 新增、`internal`**）

| 成员 | 类型 | 说明 | 来源 |
| --- | --- | --- | --- |
| `Settled` | `IReadOnlyCollection<PersonId>` | 辈分**已落定**的外来者——一经落定 MUST NOT 重复落定（`Family.SetOutsiderGeneration` 的判据） | §4.4；004 R-03 |
| `MarriageAligned` | `IReadOnlyCollection<PersonId>` | **首次家族内成婚的对齐已用掉**的外来者——此后其辈分只还能落定一次（`Family.Marry` 的判据） | §9.1；§4.4；004 R-03 |

**形状**：`internal sealed record`，位于 `KFL.Core/Entities/OutsiderBookkeeping.cs`；两个集合是
`Family` 私有簿记的**只读快照**，进出经 §2.2 变更入口表的
`CaptureOutsiderBookkeeping()` / `RestoreOutsiderBookkeeping(...)`。
**不承载校验**：本记录只承载数据——复原路径直接落值，一致性由存档层 `SaveStateValidator` 负责。
**可见性**：仅 `KFL.Infrastructure`（`InternalsVisibleTo`，见
[契约二 §4](./contracts/injection-seams.md)）；**公开面不含**本类型。
**存在理由**：两处簿记决定**后续**推演（外来者的辈分还能不能再变），却没有任何公开读取通道
——不落盘即静默失真，违反 004 FR-003/FR-017（004 `research.md` R-03）。

---

## 3. 注入接缝（`KFL.Infrastructure`）

| 接口 | 成员 | 001 内的实现 | 来源 |
| --- | --- | --- | --- |
| `IRandomService` | `double NextDouble()`；`int Next(int minInclusive, int maxExclusive)`；`void NextBytes(Span<byte> destination)` | `SeededRandomService(int seed)`：给定种子，序列完全可复现 | §1「可注入 IRandomService（可设种子、确定性）」；章程原则 IV |
| `IGameClock` | `GameDate Current { get; }` | 以 `GameState.CurrentDate` 为后端；**不接触系统时钟** | §3；FR-013；R-04 |
| `INameGenerator` | `string NextSurname()`；`string NextGivenName(Gender gender)` | **003 追加**（逻辑轨 ③ 首次消费，FR-009）：`SongStyleNameGenerator`（产品默认，内置宋风字库）与 `BogusNameGenerator`（Bogus `zh_CN` 适配器）。001 只交付前两组 | 003 FR-009；契约二 §3 |

**关键约束**：非 UI 层 MUST NOT 出现系统时钟、全局随机、文件系统、网络（章程原则 II；
FR-013）。逐字 token 清单的**唯一真源**是契约一 §2.1（14 个 token），本节**不复制副本**；
扫描范围与 `tests/KFL.Tests/Architecture/` 的豁免见契约一 G-07。守卫测试静态断言（SC-004）。

**本阶段不引入**：`ISaveService`、`IAchievementStore`、`IEventBus`——规格书 §2 虽列于
`KFL.Infrastructure`，但 001 无消费者（阶段④/逻辑轨 ⑧）。这是章程「复杂度 MUST 被论证」的
直接推论，也是已提交 spec Out of Scope 的最后一条。

---

## 4. 需求 → 模型映射

| 需求 | 落在哪 | 验证方式 |
| --- | --- | --- |
| FR-001 / FR-002 / SC-001 | `KejuFuShengLu.slnx` + 六个 `.csproj` + `global.json` | 工程级守卫 + `dotnet build`（0 警告 0 错误） |
| FR-003 / FR-014 / SC-003 | 各 `.csproj` 的 `ProjectReference` 与 `UseWPF` | 工程级守卫（含反向依赖与平台泄漏两次故意违规） |
| FR-004 / SC-005 | `Person` 字段表（2.1） | 字段读写夹具测试 |
| FR-005 / FR-006 | `TalentSet` / `Study` / `Health` 不变量 | 边界值 0 与 100、越界拒绝、天赋无写入通道 |
| FR-007 | `Person.Lifespan` 只读 | 无 setter 断言 + `>= 0`；**上界属逻辑轨 ⑦，本阶段不断言上限** |
| FR-008 | `DegreeRecord` + `DegreeChangeCause` + `Person.DegreeHistory` | 历史按 `ChangedAt` 升序追加；§7.4 降级也入史；`CurrentDegree`/`CurrentPlacement` 由末条派生；`Placement` 仅进士 |
| FR-009 | `OfficialRank?` | `null` 与 L1~L18 两端、L0/L19 拒绝 |
| FR-010 | `StatusFlag` + `StatusTimers` | 九位可任意并存 + 计时一致性 |
| FR-011 / SC-006 | `Family` 的索引与派生查询 + `HeadId` + 辈分规则 | 五类夹具：多代同堂、有配偶、有子女、娶入配偶、买来的旁系；双向一致与无环；**一夫一妻拒绝用例 + 丧偶再婚（`FormerSpouseIds` 承接前任、子女父母引用不断裂）**；家主为 `null` 或指向在册成员；血亲辈分不可变、外来者辈分落定规则；**开局成员辈分 = 0** |
| FR-012 | `GameState` | 标识非空（`Guid.Empty` 被拒）、标识无公开 setter、起始年月；**改档名不变性属阶段④**（001 无落盘，无可验证对象）——**已交付并验证（004）**，见 [004 `contracts/save-service.md` §6](../004-save-system/contracts/save-service.md) 与 004 SC-004 |
| FR-013 / SC-004 | 三组接缝（`INameGenerator` 由 003 追加）+ 静态扫描 | 源码级守卫 |
| FR-015 / SC-002 | `tests/KFL.Tests` 的领域与接缝目录 | `dotnet test`（领域/规则测试无环境依赖）；G-07 的扫描范围**逐字对齐契约一 G-07**：非 UI 三产品工程（`KFL.Core`/`KFL.Infrastructure`/`KFL.Rules`）**加** `tests/KFL.Tests/Core`、`Infrastructure`、`Fixtures`、`Rules`（`Rules` 于 002 追加，见 002 `research.md` **R-14**），`Architecture/` 显式豁免；扫描按目录前缀匹配（含子目录），实现出处 `ArchitectureRules.ScannedTestRoots` |

---

## 5. 状态转移

**本阶段不实现任何状态转移**。001 只保证档案能**表达**状态，不推进状态：患病与饥馑流转
（阶段②/逻辑轨 ⑦）、待阙转授官（逻辑轨 ③）、致仕（逻辑轨 ③）、
已亡归档判定（阶段②/逻辑轨 ⑧）、外嫁归档（界面轨 U2/逻辑轨 ⑦）全部属后续阶段，且各自带自己的验收门禁。

> **006 后记（2026-10-13）**：原列于本句的**「服刑计时递减」（阶段⑥ / 逻辑轨 ⑥）已由 006 交付**——
> `PunishmentAdvance.Run(state)` 在月末结算**第③步**逐月递减服刑/禁考/禁升计时，并在计时归零时
> 清掉对应状态位（服刑释放另按 `Rank is not null` 官阶 +1）；006 是这些计时的首个产品生产者与
> 递减者，见 [契约十六 §1](../006-bribery-punishment/contracts/punishment.md) 与 006 FR-020。

> **007 后记（2026-10-17）**：原列于本句的**「患病与饥馑流转」「已亡归档判定」「外嫁归档」三项已由 007 交付**，
> 全部落在月末结算的**第 ⑧ 步（人口与健康推进）**，且**接在 ⑦ 之后、⑨ 科举季之前**：
>
> | 状态转移 | 落点 | 触发与口径 |
> | --- | --- | --- |
> | 患病（`Ill` 位 + 程度/疗程两项计时） | ⑧-c `IllnessAdvance.Run` | 按体质线性插值的感染率逐人判定；命中当月经 ⑧-g 之外的路径直接置位；清除后同月不再有后续随机消费 |
> | 分娩（新生儿注册）/ 分娩死亡 | ⑧-b `Childbirth.Run` | 消费上一月排入的待产记录；母亲死亡只登记死因，`Status` 由 ⑧-g 统一落 |
> | 饥馑体质损失与饥馑死亡 | ⑧-e `FamineHealth.Run` | 只读 `FamineStage.Severe`；救济期与未进入饥馑时不减不掷 |
> | 成长与遗忘 | ⑧-d `GrowthAdvance.Run` | 零随机：学业增减、体质自然恢复 |
> | 寿终与夭折 | ⑧-f `LifespanAdvance.Run` | `AgeAt >= Lifespan` ⇒ 寿终；`AgeAt < 3` 的生日月掷夭折 |
> | 已亡归档 | ⑧-g `DeathSettlement.Run` | 当月**唯一**置 `Deceased` 的地方；幂等，死因不落 `Person` 字段 |
> | 外嫁归档 | `LifecycleEntry.TryMatchmake`（⑨ 之外的动作入口） | 置 `MarriedOut`、进 `ArchivedMembers`、不计口、不可科举/指派读书；数据保留供 006 连坐追及 |
> | 家主继任 | ⑧-h `HeadSuccession` | 每月最多一次，在 ⑧-g 之后取四档候选并 `SetHead`；无候选 ⇒ `null`（不构成绝嗣） |
>
> 死因（`DeathCause`）只承载于**当月**的 `SettlementResult.Lifecycle.Deaths`；007 已在本文件的
> 第 2 节不变量 4/6 分别追加了「`Ill` 位 ⇔ 程度计时」与「任内归档后由 ⑧-h 月度修正」两处引用注。

唯一在本阶段成立的「转移」是构造期校验失败即拒绝——不产生非法档案，而不是先建再修。
