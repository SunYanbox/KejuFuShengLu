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

**不变量**：`Placement is not null` ⇒ `Level == DegreeLevel.JinShi`（§6：进甲者才是状元/
榜眼/探花）。

**为什么必须带日期与原因**：§4.1 要求功名以「变迁历史」呈现，而 §7.4 的连坐会
**降一级功名**（进士→贡士→举人→白身）。若只记录「考试考成功的功名」，被降级者的历史
末条会一直写着「进士」，与其真实功名矛盾——历史本身就成了假的。带 `Cause` 后，一次中式
与一次降级都能被如实读出。举人被降为白身会追加一条 `Level = BaiShen` 的记录，这是
**合法的历史条目**，与「尚无记录（出生即白身）」是两回事。

**向后兼容扩展（2026-10-08，003）**：`DegreeRecord` 新增第 5 个**可选**成员
`ImperialClass? imperialClass = null`（甲第：一甲 / 二甲 / 三甲），由
`003-opening-assets-officialdom` 引入，见该特性 FR-014。既有**四参调用保持合法**；
上表前四项的语义与不变量不变（一甲名次非空 ⇒ 甲第 = 一甲）。

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

**不变量**：四个字段各自仅在对应状态位为真时可非空；非空值 MUST `>= 0`。
待阙项与 `Person.EntryTrack` **同生同灭**（见 §2.1 不变量 4），且 MUST NOT 以「0 + 状态位为真」的形态存续。
**明确不含**：计时递减逻辑——服刑/禁考/禁升属逻辑轨 ⑥，待阙的递减与「递减到 0 的当月授官」
属逻辑轨 ③（`OfficialCareerAdvance`），其 6~24 区间属 `OfficialCareerPolicy`。

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
   `FormerSpouseIds`、`DegreeHistory`、`CurrentDegree`、`CurrentPlacement`——均为只读
   属性，**不存在公开 setter**（FR-005、FR-007、FR-008、§4.4）。
   **`Person` 自持的可写属性**只有十个：`Name`、`Study`、`Health`、`Rank`、`Merit`、
   `Status`、`Timers`、`Occupation`、`MonthsInOffice`（003 新增）、`EntryTrack`（003 新增）。
   分界依据：凡「出生即定」或「跨实体一致性」的字段一律只读——前者如 `Gender` /
   `Talents` / `Lifespan`，后者如 `SpouseId` / `FatherId` / `MotherId` / `Generation`
   （唯一入口在 `Family`，见 §2.2 不变量 4）。
3. `FatherId != Id`、`MotherId != Id`、`SpouseId != Id`；`FormerSpouseIds` 不含 `SpouseId` 且不重复。
4. `Timers` 与 `Status` 一致（见 1.7）；003 新增的 `EntryTrack` 同向一致
   （非空 ⇒ `AwaitingPost` 位为真；清除该位前 MUST 先置 `null`——`Timers` 的
   `AwaitingPostRemainingMonths` 与它 MUST 同生同灭）。
5. **年龄不落裸字段**：`AgeAt(GameDate)` = `BirthDate.AgeInYearsAt(at)`（R-07）。
6. 出生时辰与「已亡」时间不落字段：§4.1 未列，且 §12.1 的归档排序属界面轨 U2。
7. `DegreeHistory` 按 `ChangedAt` **非降序**（追加式）；既有记录 MUST NOT 被改写或删除。
   `CurrentDegree` / `CurrentPlacement` MUST 由末条派生，MUST NOT 另存独立字段——否则
   §7.4 的连坐降级会让两处数据失配（见 1.5）。
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
`Lifespan` **只约束下界 `>= 0`，没有上界**——天命寿数的分布（§4.2）属逻辑轨 ⑦，001 MUST NOT
自设上限；「无上界」是有意留白，不是遗漏（曾因只写下界而被读成设计缺失）。

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

**变更入口（`Family` 是跨实体唯一写入方，§4.4 辈分与 §9.1 一夫一妻的强制点）**

| 成员 | 签名 | 规则 | 来源 |
| --- | --- | --- | --- |
| `BoughtCollateralGeneration` | `int`（派生） | = 家主辈分 + 1；`HeadId` 为 `null` 时抛异常（无在册男性成员则无从推导） | §4.4；§9.5 |
| `AddFoundingMember(Person)` | `Person` | 开局成员：辈分 MUST 为 0、父母引用 MUST 为 `null` | §4.4 |
| `AddChild(Person)` | `Person` | 血亲成员：父母至少一方为本家族成员，辈分 MUST = 该父母辈分 + 1 | §4.4 |
| `AddOutsider(Person)` | `Person` | 外来者：父母引用 MUST 为 `null`，辈分由 `Family` 指定 | §4.4；§9.5 |
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
7. 辈分的指定与变更入口唯一在 `Family`（同第 4 条）：血亲成员的辈分一经确定 MUST NOT
   变更；外来者（娶入配偶、§9.5 买来的旁系）的辈分 MUST 在其尚无子女时落定（§4.4），
   且**总共只变动两次**——落定一次 + 首次家族内成婚时对齐配偶辈分一次，此后 MUST NOT 再变更。

**§17 裁决**: `HasShiStatus` 建在家族级而非成员级——§10.2 的效果（录取率 ×1.1、婚嫁规格、
贷款划扣 20%）在本项目「一存档一家族」的模型下由家族统一承载；嫁入配偶的差异在逻辑轨 ⑦
真正实现该效果时再细分，本阶段不预置按人标记。

**家主的分期**: `Family.HeadId` 进 001——它在 001 内**有真实消费者**：§4.4 的辈分规则
要求「买来的旁系辈分 = 家主辈分 + 1」。但**继任判定不进 001**：它由死亡推进触发，属
逻辑轨 ⑦。001 只保证该字段存在、且「为 `null` 或指向在册成员」这一不变量可被校验。
经核验，§4.4 的继任规则所需数据 001 已**全部提供**，无需为此再加字段：`Gender`、
`BirthDate`（年龄）、`ChildrenOf`（子代数量与排序）、`Generation`、`Status`。

### 2.3 `GameState`（存档级状态）

| 字段 | 类型 | 说明 | 来源 |
| --- | --- | --- | --- |
| `Id` | `Guid` | 创建存档时初始化，**改档名不影响**（该子句属阶段④ 验证——001 无落盘，只校验非空与无公开 setter，见 FR-012）；由注入的 `IRandomService` 生成（16 字节 → Guid 的转换落在 `KFL.Infrastructure/Services/GameStateFactory.cs`——`KFL.Core` 看不到接缝，见 R-03） | §14；FR-012；R-03 |
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
| FR-012 | `GameState` | 标识非空（`Guid.Empty` 被拒）、标识无公开 setter、起始年月；**改档名不变性属阶段④**（001 无落盘，无可验证对象） |
| FR-013 / SC-004 | 三组接缝（`INameGenerator` 由 003 追加）+ 静态扫描 | 源码级守卫 |
| FR-015 / SC-002 | `tests/KFL.Tests` 的领域与接缝目录 | `dotnet test`（领域/规则测试无环境依赖）；G-07 的扫描范围已按契约一扩至 `tests/KFL.Tests/Core`、`Infrastructure`、`Fixtures`，`Architecture/` 显式豁免 |

---

## 5. 状态转移

**本阶段不实现任何状态转移**。001 只保证档案能**表达**状态，不推进状态：服刑计时递减
（阶段⑥）、患病与饥馑流转（阶段②/逻辑轨 ⑦）、待阙转授官（逻辑轨 ③）、致仕（逻辑轨 ③）、
已亡归档判定（阶段②/逻辑轨 ⑧）、外嫁归档（界面轨 U2/逻辑轨 ⑦）全部属后续阶段，且各自带自己的验收门禁。

唯一在本阶段成立的「转移」是构造期校验失败即拒绝——不产生非法档案，而不是先建再修。
