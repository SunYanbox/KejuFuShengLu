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
| `Year` | `int` | 架空纪年，从 1 起 | §3「从 1 年 1 月开始推演」 |
| `Month` | `int` | 1~12，无闰月 | §3「1 回合 = 1 游戏月，1 年 = 12 月」 |

**不变量**：`Year >= 1`；`1 <= Month <= 12`。构造即校验，越界抛 `ArgumentOutOfRangeException`。
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
「仅调试控制台可修改」的落地方式属阶段⑦；本阶段只保证**不存在**常规写入通道。

### 1.5 `DegreeRecord`（readonly record struct）

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `Level` | `DegreeLevel` | 白身 / 举人 / 贡士 / 进士 |
| `Placement` | `ImperialPlacement?` | 一甲名次，仅进士可有 |

**不变量**：`Placement is not null` ⇒ `Level == DegreeLevel.JinShi`（§6：进甲者才是状元/
榜眼/探花）。

### 1.6 `OfficialRank`（readonly record struct）

包装 1~18 的级别（`Level`），**不表示「无官职」**——无官职由 `Person.Rank is null` 表达
（FR-009）。**不变量**：`1 <= Level <= 18`（§8.1 十八级）。
年俸数值表（72~5100 贯）属阶段⑧，本阶段不落任何俸禄常量。

### 1.7 `StatusTimers`（readonly record struct）

| 成员 | 类型 | 说明 | 来源 |
| --- | --- | --- | --- |
| `SentenceRemainingMonths` | `int?` | 服刑剩余月数 | §7.5「服刑以月为单位（5 年 = 60 月）」 |
| `ExamBanRemainingMonths` | `int?` | 禁考剩余月数 | §7.5「禁考与服刑独立并行计时」 |
| `PromotionBanRemainingMonths` | `int?` | 禁升剩余月数 | §7.5、§8.2「禁升来源…独立计时器」 |

**不变量**：三个字段仅在对应状态位为真时可非空；非空值 MUST `>= 0`。
**明确不含**：计时递减逻辑（阶段⑥）。

---

## 2. 聚合与实体（`KFL.Core`）

### 2.1 `Person`（成员档案）

| 字段 | 类型 | 可变 | 来源 |
| --- | --- | --- | --- |
| `Id` | `PersonId` | 否 | 引用完整性所需 |
| `Name` | `string` | 是 | §4.1「姓名」（§12.4 姓氏仅影响 Last Name，生成属后续阶段） |
| `Gender` | `Gender` | 否 | §4.1 |
| `Generation` | `int`（>= 0） | 否 | §4.1「辈分」；§12.1 家族树「一层一代」 |
| `BirthDate` | `GameDate` | 否 | §4.3「生日当月转成年」；年龄由它派生（R-07） |
| `Talents` | `TalentSet` | 否 | §4.1 天赋四项 |
| `Study` | `int`（0~100） | 是 | §4.1「学业」 |
| `Health` | `int`（0~100） | 是 | §4.1「体质」 |
| `Lifespan` | `int`（>= 0，年） | 否 | §4.1「天命寿数：出生瞬间 roll 出…卡片上显示」 |
| `Degree` | `DegreeRecord` | 是 | §6 功名链 |
| `Rank` | `OfficialRank?` | 是 | §8 官阶；`null` = 无官职 |
| `Merit` | `int`（>= 0） | 是 | §4.1「政绩」；§8.2 上限 100 属规则（阶段⑧） |
| `Status` | `StatusFlag` | 是 | §4.1 九种状态，可并存 |
| `Timers` | `StatusTimers` | 是 | §7.5 |
| `Occupation` | `Occupation` | 是 | §4.1「职业指派」 |
| `FatherId` / `MotherId` | `PersonId?` | 否 | §4.1 父母引用 |
| `SpouseId` | `PersonId?` | 是 | §4.1 婚姻关系；一夫一妻（§9.1） |
| `FormerSpouseIds` | `IReadOnlyList<PersonId>` | 是 | §9.1「丧偶可再婚」+ spec Edge Case（须能表达既往婚姻） |

**不变量**
1. `Talents`、`Study`、`Health` 落在 0~100（FR-005、FR-006）。
2. `Study`/`Health` 可写，`Talents`/`Lifespan`/`Gender`/`Generation`/`BirthDate` 无写入通道
   （FR-005、FR-007：「出生即定、终身不再改变」）。
3. `FatherId != Id`、`MotherId != Id`、`SpouseId != Id`；`FormerSpouseIds` 不含 `SpouseId` 且不重复。
4. `Timers` 与 `Status` 一致（见 1.7）。
5. **年龄不落裸字段**：`AgeAt(GameDate)` = `BirthDate.AgeInYearsAt(at)`（R-07）。
6. 出生时辰与「已亡」时间不落字段：§4.1 未列，且 §12.1 的归档排序属阶段⑨。

**明确不含**：`ChildIds`（由 `Family` 派生，见 R-08）；`IsShiIdentity`（仕身份为家族级，
见 2.2）；`Origin`（出身为存档级，见 2.3）。

### 2.2 `Family`（家族）

| 成员 | 类型 | 说明 | 来源 |
| --- | --- | --- | --- |
| `Name` | `string` | 家族姓氏（§12.4） | §12.4 |
| `HasShiStatus` | `bool` | 「仕身份」= 进士直系血统，**与出身独立可并存** | §10.2；spec Edge Case |
| `Members` | `IReadOnlyCollection<Person>` | 全部成员（含已归档），按 `PersonId` 索引 | §12.1「死亡成员自动归档」 |
| `TryGet(PersonId)` | 方法 | 按标识取成员 | FR-011 |
| `ChildrenOf(PersonId)` | 方法 | 派生子女引用 | FR-011、SC-006 |
| `SpouseOf(PersonId)` | 方法 | 配偶 | FR-011 |
| `RegisteredMembers` | 属性 | **在册** = 非「已亡」且非「外嫁」 | §12.1、§15 |
| `ArchivedMembers` | 属性 | 已归档 = 已亡 或 外嫁 | §12.1 |

**不变量**
1. 成员标识唯一；`PersonId` 引用要么指向本家族成员，要么为 `null`（**无悬挂引用**）。
2. 配偶关系双向一致：`a.SpouseId == b.Id` ⇔ `b.SpouseId == a.Id`（US1 AS2）。
3. 父母引用为向无环：沿父系主轴向上遍历必然终止（SC-006、Edge Case 无自环）。
4. 只有 `Family` 能改配偶与父母引用；`Person` 不提供公开写入通道（章程原则 II：
   实体不承担跨实体编排）。
5. `HasShiStatus` 与任何 `Origin` 可任意组合（§10.2：出身终身特性与仕身份叠加并存）。

**§17 裁决**: `HasShiStatus` 建在家族级而非成员级——§10.2 的效果（录取率 ×1.1、婚嫁规格、
贷款划扣 20%）在本项目「一存档一家族」的模型下由家族统一承载；嫁入配偶的差异在阶段④/
⑧ 真正实现该效果时再细分，本阶段不预置按人标记。

### 2.3 `GameState`（存档级状态）

| 字段 | 类型 | 说明 | 来源 |
| --- | --- | --- | --- |
| `Id` | `Guid` | 创建存档时初始化，**改档名不影响**；由注入的 `IRandomService` 生成 | §14；FR-012；R-03 |
| `CurrentDate` | `GameDate` | 起始 = 1 年 1 月 | §3 |
| `Difficulty` | `Difficulty` | 同一存档内可随时切换（切换逻辑属阶段⑫） | §11 |
| `Origin` | `Origin` | 创建存档时选择 | §10.1 |
| `Family` | `Family` | 当前家族 | §2 |

**不变量**：`Id != Guid.Empty`；`CurrentDate.Month` 合法。
**明确不含**：资产池、商本、现金/储蓄/贷款、统计容器——均属阶段②及以后（spec Out of Scope）。

---

## 3. 注入接缝（`KFL.Infrastructure`）

| 接口 | 成员 | 001 内的实现 | 来源 |
| --- | --- | --- | --- |
| `IRandomService` | `double NextDouble()`；`int Next(int minInclusive, int maxExclusive)`；`void NextBytes(Span<byte> destination)` | `SeededRandomService(int seed)`：给定种子，序列完全可复现 | §1「可注入 IRandomService（可设种子、确定性）」；章程原则 IV |
| `IGameClock` | `GameDate Current { get; }` | 以 `GameState.CurrentDate` 为后端；**不接触系统时钟** | §3；FR-013；R-04 |

**关键约束**：非 UI 层 MUST NOT 出现 `DateTime.Now`、`Random`、文件系统、网络
（章程原则 II；FR-013）。守卫测试静态断言（SC-004）。

**本阶段不引入**：`ISaveService`、`IAchievementStore`、`IEventBus`——规格书 §2 虽列于
`KFL.Infrastructure`，但 001 无消费者（阶段④/⑪）。这是章程「复杂度 MUST 被论证」的
直接推论，也是已提交 spec Out of Scope 的最后一条。

---

## 4. 需求 → 模型映射

| 需求 | 落在哪 | 验证方式 |
| --- | --- | --- |
| FR-001 / FR-002 / SC-001 | `KejuFuShengLu.slnx` + 六个 `.csproj` + `global.json` | 工程级守卫 + `dotnet build`（0 警告 0 错误） |
| FR-003 / FR-014 / SC-003 | 各 `.csproj` 的 `ProjectReference` 与 `UseWPF` | 工程级守卫（含反向依赖与平台泄漏两次故意违规） |
| FR-004 / SC-005 | `Person` 字段表（2.1） | 字段读写夹具测试 |
| FR-005 / FR-006 | `TalentSet` / `Study` / `Health` 不变量 | 边界值 0 与 100、越界拒绝、天赋无写入通道 |
| FR-007 | `Person.Lifespan` 只读 | 无 setter 断言 + 取值范围 |
| FR-008 | `DegreeRecord` | 四级链 + `Placement` 仅进士 |
| FR-009 | `OfficialRank?` | `null` 与 L1~L18 两端、L0/L19 拒绝 |
| FR-010 | `StatusFlag` + `StatusTimers` | 九位可任意并存 + 计时一致性 |
| FR-011 / SC-006 | `Family` 的索引与派生查询 | 三类夹具：多代同堂、有配偶、有子女；双向一致与无环 |
| FR-012 | `GameState` | UUID 非空、改档名不影响（同名对象复用）、起始年月 |
| FR-013 / SC-004 | 两组接缝 + 静态扫描 | 源码级守卫 |
| FR-015 / SC-002 | `tests/KFL.Tests` | `dotnet test`（领域/规则测试无环境依赖） |

---

## 5. 状态转移

**本阶段不实现任何状态转移**。001 只保证档案能**表达**状态，不推进状态：服刑计时递减
（阶段⑥）、患病与饥馑流转（阶段②/⑧）、待阙转授官（阶段⑧）、致仕（阶段⑧）、
已亡归档判定（阶段②/⑪）、外嫁归档（阶段⑨/⑧）全部属后续阶段，且各自带自己的验收门禁。

唯一在本阶段成立的「转移」是构造期校验失败即拒绝——不产生非法档案，而不是先建再修。
