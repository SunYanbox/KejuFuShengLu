# 阶段 1 数据模型：存档体系的存储侧模型

**Feature**: `004-save-system` | **Date**: 2026-10-11 | **Spec**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

本文件定义**存储侧**的形状：文件布局、持久化字段表、存储侧类型、规则侧类型、状态转移与
需求映射。**它不定义任何新的领域语义**——领域类型与数值的真源仍是规格书与 001/002/003 的
`data-model.md`；本文件只回答「这些状态如何**一对一地**变成字节、又如何**逐字段**变回来」。

> **术语**：`Core` = `KFL.Core`、`Infra` = `KFL.Infrastructure`、`Rules` = `KFL.Rules`。
> 全部类型名按声明处首次出现时标注层。R-xx 引用 [research.md](./research.md)；契约九/十/十一
> 分别指 [contracts/save-service.md](./contracts/save-service.md)、
> [contracts/save-format.md](./contracts/save-format.md)、
> [contracts/save-cadence.md](./contracts/save-cadence.md)。

## 1. 层分布与「瞬态 DTO」的定位

```text
Core    GameState / Family / Person / FamilyEconomy / Treasury / Loan / Holdings / Ledger / FamineState
          ▲ 逐字段映射（Infra 侧经 InternalsVisibleTo 调用 Core 的既有成员）
Infra   SaveStateDto 及其子 DTO（[MessagePackObject] + [JsonPropertyName]，瞬态）
        SaveStateMapper（Core ⇄ DTO）· SaveStateValidator（R-04）
        SaveFileFormat（版本常量）· MessagePackSaveFormat / JsonSaveFormat（R-05/R-08/R-09）
        AtomicFileWriter（R-14）· SaveCatalog（R-18）· SaveName（R-13）· SaveService
Rules   SaveCadencePolicy（数值）· TurnRunner（结算 → 保存 → 备份的编排）
```

**不变量 1（瞬态 DTO）**：`SaveStateDto` 及其子 DTO MUST 只在**一次** `Save`/`Load` 调用的栈内存活，
MUST NOT 被 `GameState`、任何 `Core`/`Rules` 类型或 `SaveService` 实例长期持有。
**DTO 不是第二份内存真源**——它是同一批状态在**跨进程边界的那一刻**的形状（FR-020）。

**不变量 2（单向映射）**：`SaveStateMapper` 的每一个字段映射 MUST 是**显式**的（点对点赋值），
MUST NOT 使用反射、MUST NOT 使用「无注解契约」序列化（R-02）。

---

## 2. 文件级模型

### 2.1 `SaveFile`（一份存档的落盘形态）

| 属性 | 值 | 来源 |
| --- | --- | --- |
| 定位 | `存档根` + `存档名` + `.` + `扩展名` | §14；FR-006/FR-007 |
| 存档根 | 可配置；默认 `AppContext.BaseDirectory` + `saves` | §14；FR-006；R-15 |
| 扩展名 | 二进制 `.dat`（**默认**）/ JSON `.json` | §14；FR-004/FR-005 |
| 备份落点 | `存档根` + `backups` + `存档名` + `.` + **同扩展名** | §14 的 Q1 裁决；FR-011；R-19 |
| 形状 | `[头记录][主体]`，两种格式同形 | R-05 |

### 2.2 头记录（手写，**不是** DTO）

| 位置 | 成员 | 类型 | 语义 | 读取时机 |
| --- | --- | --- | --- | --- |
| 1 | `FormatVersion` | `int` | 文件格式版本，本版本 = `2` | **任何正文解析之前**（FR-015 的闸门，R-06） |
| 2 | `PayloadLength` | `int` | 主体的**字节数** | 截断判据（R-05/R-07） |
| 3 | `SaveId` | `Guid` | 存档标识的探针副本（`D` 格式字符串） | UUID 唯一性探针（R-18） |

**不变量 3**：头记录的 `SaveId` MUST 等于主体 `SaveStateDto.Id`；两者不等 ⇒ `InvalidStructure`（R-04 第 8 条）。
**不变量 4**：`PayloadLength` MUST 等于主体的字节数；写入时同源产生，读取时**只用于判截断**，MUST NOT
被用作「拒绝手改」的校验和（R-05 的备选 ②）。

### 2.3 两种格式的字节布局

```text
.dat（MessagePack，R-09 选项）
  0x93                                 ← fixarray(3) 头记录
  int      FormatVersion
  int      PayloadLength
  str      SaveId（36 字符）
  <PayloadLength 字节的主体 MessagePack 数组>

.json（System.Text.Json，R-08 选项）
  {"FormatVersion":2,"PayloadLength":N,"SaveId":"…","State":
  <主体 JSON>}
```

### 2.4 目录布局

```text
<存档根>/                       默认 = 可执行文件旁 saves/
├── 宋家三十年.dat              ← 当前档（二进制，默认格式）
├── 李家.json                   ← 当前档（JSON）
└── backups/
    ├── 宋家三十年.dat          ← 覆盖式，每档恒 1 份（R-19）
    └── 李家.json
```

**不变量 5（同标识唯一）**：同一存档根下 MUST NOT 存在两个携带**同一 `SaveId`** 的存档文件
（Assumptions；001 R-03 的收口）。违反即拒绝写入（R-18）。
**不变量 6（同根同名单文件）**：同一存档根下，同一个 `存档名` MUST NOT 同时存在两种格式的候选文件
（Edge Case 的「两个候选」歧义）——格式切换与覆盖写都以此为准（R-18）。

---

## 3. 持久化字段表（逐实体）

「线上形状」列的**二进制**写法指 MessagePack 的值类型（`int` = 变长整数、`str` = UTF-8 字符串、
`nil` = MessagePack nil）；**JSON** 写法指 R-08 的选项下的字面量。
所有 DTO 成员都是 `[JsonRequired]` + `[JsonPropertyName]`（R-07/R-08）。

### 3.1 `SaveStateDto`（`Core.GameState`）

| # | DTO 成员 | 线上键名 | 线上形状（二进制 / JSON） | 源（`Core`） | 复原入口 |
| --- | --- | --- | --- | --- | --- |
| 0 | `Id` | `id` | `str` / 字符串 | `GameState.Id` | 构造函数第 1 参（`Guid.Empty` 被拒） |
| 1 | `CurrentYear` | `currentYear` | `int` / 数字 | `GameState.CurrentDate.Year` | 构造函数第 2 参（`new GameDate(y, m)`；R-11） |
| 2 | `CurrentMonth` | `currentMonth` | `int` / 数字 | `GameState.CurrentDate.Month` | 同上（1~12 由 `GameDate` 自校验） |
| 3 | `Difficulty` | `difficulty` | `int` / 枚举名 | `GameState.Difficulty` | 公开 setter |
| 4 | `PendingDifficulty` | `pendingDifficulty` | `int` 或 `nil` / 名字或 `null` | `GameState.PendingDifficulty` | 公开 setter（`null` ⇒ 无待生效） |
| 5 | `Origin` | `origin` | `int` / 枚举名 | `GameState.Origin` | 构造函数第 4 参 |
| 6 | `Family` | `family` | 数组 / 对象 | `GameState.Family` | 见 §3.2 |
| 7 | `Economy` | `economy` | 数组 / 对象 | `GameState.Economy` | 见 §3.3 |

### 3.2 `FamilyDto`（`Core.Family`，§4.4、§12.1、§12.4）

| # | DTO 成员 | 线上键名 | 线上形状 | 源 | 复原入口 |
| --- | --- | --- | --- | --- | --- |
| 0 | `Name` | `name` | `str` / 字符串 | `Family.Name` | 构造函数（空白被拒；**与存档名无关**，R-25） |
| 1 | `HasShiStatus` | `hasShiStatus` | `bool` / 布尔 | `Family.HasShiStatus` | 公开 setter |
| 2 | `HeadId` | `headId` | `str` 或 `nil` / 字符串或 `null` | `Family.HeadId` | `Family.SetHead`（**在册性**由它校验） |
| 3 | `Members` | `members` | 数组 / 数组 | `Family.Members`（**含归档成员，按加入顺序**） | 按同一顺序经 `AddOutsider`/`AddChild` 注册（R-12） |
| 4 | `SettledOutsiders` | `settledOutsiders` | 数组 / 数组 | `Family` 的私有簿记（R-03） | `Family.RestoreOutsiderBookkeeping`（**新增 `internal`**） |
| 5 | `MarriageAlignedOutsiders` | `marriageAlignedOutsiders` | 数组 / 数组 | 同上 | 同上 |
| 6 | `AutoExamApplication` | `autoExamApplication` | `bool` / 布尔 | `Family.AutoExamApplication`（**005 新增**，持续报考开关） | 公开 setter（默认 `false`，与 `HasShiStatus` 同款） |

> 归档**不是删除**（§12.1）：`Members` MUST 涵盖「在册」与「已归档（已亡 / 外嫁）」两类，
> 故 `Members.Count == 在册数 + 归档数`；`Person` 的 `status` 里保留 `Deceased` / `MarriedOut` 位。

### 3.3 `FamilyEconomyDto`（`Core.FamilyEconomy`，§5.1~§5.4）

| # | DTO 成员 | 线上键名 | 线上形状 | 源 | 复原入口 |
| --- | --- | --- | --- | --- | --- |
| 0 | `LivingStandard` | `livingStandard` | `int` / 枚举名 | `FamilyEconomy.LivingStandard` | 构造函数（**无默认值**，由本成员给出） |
| 1 | `PendingLivingStandard` | `pendingLivingStandard` | `int` 或 `nil` / 名字或 `null` | `FamilyEconomy.PendingLivingStandard` | 公开 setter |
| 2 | `GrainPriceIndex` | `grainPriceIndex` | `decimal` / 数字 | `FamilyEconomy.GrainPriceIndex.Value` | 公开 setter（`new GrainPriceIndex(v)`，`v > 0` 由它校验） |
| 3 | `Treasury` | `treasury` | 数组 / 对象 | `FamilyEconomy.Treasury` | 见 §3.4 |
| 4 | `Holdings` | `holdings` | 数组 / 对象 | `FamilyEconomy.Holdings` | 见 §3.5 |
| 5 | `Ledger` | `ledger` | 数组 / 数组 | `FamilyEconomy.Ledger.Entries`（**全量、按追加顺序**） | 构造函数 + `Ledger.Append` 逐条重放（FR-021） |
| 6 | `Famine` | `famine` | 数组 / 对象 | `FamilyEconomy.Famine` | `FamilyEconomy.RestoreFamine`（**新增 `internal`**） |

### 3.4 `TreasuryDto` / `LoanDto`（§5.4）

| DTO | # | 成员 | 线上键名 | 线上形状 | 源 | 复原入口 |
| --- | --- | --- | --- | --- | --- | --- |
| `TreasuryDto` | 0 | `Cash` | `cash` | `decimal` / 数字 | `Treasury.Cash.Wen` | 构造函数（负值被拒） |
| | 1 | `Savings` | `savings` | `decimal` / 数字 | `Treasury.Savings.Wen` | 同上 |
| | 2 | `MerchantCapital` | `merchantCapital` | `decimal` / 数字 | `Treasury.MerchantCapital.Wen` | 同上 |
| | 3 | `Loan` | `loan` | 数组 / 对象 | `Treasury.Loan` | 见本表 `LoanDto` |
| | 4 | `SavingsRate` | `savingsRate` | `decimal` 或 `nil` / 数字或 `null` | `Treasury.SavingsRate` | 公开 setter（**先年份、后利率**） |
| | 5 | `SavingsRateYear` | `savingsRateYear` | `int` 或 `nil` / 数字或 `null` | `Treasury.SavingsRateYear` | 公开 setter（同上次序） |
| `LoanDto` | 0 | `Principal` | `principal` | `decimal` / 数字 | `Loan.Principal.Wen` | 公开 setter |
| | 1 | `AccruedInterest` | `accruedInterest` | `decimal` / 数字 | `Loan.AccruedInterest.Wen` | `Loan.AccrueInterest(Money.FromWen(v))`（**只增**，与「先本后息」自洽） |
| | 2 | `MonthsSinceInterest` | `monthsSinceInterest` | `int` / 数字 | `Loan.MonthsSinceInterest` | 公开 setter |

**不变量 7（负债不并池）**：`TreasuryDto` MUST 只含三个资金池 + 贷款 + 当年利率；
`TreasuryPool`（现金 + 储蓄 + 商本）是**派生量**，MUST NOT 入档（R-05 口径；002 契约四 §1）。

### 3.5 `HoldingsDto`（§5.3）

| # | DTO 成员 | 线上键名 | 线上形状 | 源 | 复原入口 |
| --- | --- | --- | --- | --- | --- |
| 0 | `FarmlandMu` | `farmlandMu` | `int` / 数字 | `Holdings.FarmlandMu` | 公开 setter（负值被拒） |
| 1 | `RuralHouses` | `ruralHouses` | `int` / 数字 | `Holdings.RuralHouses` | 同上 |
| 2 | `UrbanHouses` | `urbanHouses` | `int` / 数字 | `Holdings.UrbanHouses` | 同上 |
| 3 | `Shops` | `shops` | `int` / 数字 | `Holdings.Shops` | 同上 |

### 3.6 `LedgerEntryDto`（§12.3；002 契约四）

| # | DTO 成员 | 线上键名 | 线上形状 | 源 | 复原入口 |
| --- | --- | --- | --- | --- | --- |
| 0 | `Year` | `year` | `int` / 数字 | `LedgerEntry.Date.Year` | `new GameDate(y, m)` |
| 1 | `Month` | `month` | `int` / 数字 | `LedgerEntry.Date.Month` | 同上 |
| 2 | `PersonId` | `personId` | `str` 或 `nil` / 字符串或 `null` | `LedgerEntry.PersonId`（`null` = 家族级） | `new PersonId(g)` |
| 3 | `Category` | `category` | `int` / 枚举名 | `LedgerEntry.Category` | `Ledger.Append(new LedgerEntry(...))` |
| 4 | `AmountWen` | `amountWen` | `decimal` / 数字 | `LedgerEntry.Amount.Wen`（**变动量**，不是余额） | 同上（资金类非 0 由 `LedgerEntry` 校验） |

**不变量 8（全量、顺序一致、无冗余汇总）**：`Ledger` MUST 全量入档，条目**顺序**与 `Ledger.Entries`
逐条一致，`Count` 相等；MUST NOT 另存任何每月汇总值（FR-021；§12.3 的 §17 裁决回写）。
**不变量 9（金额原值）**：`AmountWen` 与三个资金池 MUST 按 `decimal` **原值**入档（含 scale），
断言 MUST 用 `decimal.GetBits` 逐位比较（R-10；FR-016）。

### 3.7 `FamineDto`（§5.4）

| # | DTO 成员 | 线上键名 | 线上形状 | 源 | 复原入口 |
| --- | --- | --- | --- | --- | --- |
| 0 | `Stage` | `stage` | `int` / 枚举名 | `FamineState.Stage` | `FamilyEconomy.RestoreFamine`（**新增 `internal`**） |
| 1 | `ElapsedMonths` | `elapsedMonths` | `int` / 数字 | `FamineState.ElapsedMonths` | 同上（「阶段零 ⇔ 计时零」由 R-04 第 7 条校验） |

### 3.8 `PersonDto`（§4.1、§5.4、§7.5、§8.2、§9.1）

| # | DTO 成员 | 线上键名 | 线上形状 | 源 | 复原入口 |
| --- | --- | --- | --- | --- | --- |
| 0 | `Id` | `id` | `str` / 字符串 | `Person.Id` | 构造函数（`Guid.Empty` 被拒） |
| 1 | `Name` | `name` | `str` / 字符串 | `Person.Name` | 构造函数（空白被拒） |
| 2 | `Gender` | `gender` | `int` / 枚举名 | `Person.Gender` | 构造函数 |
| 3 | `BirthYear` | `birthYear` | `int` / 数字 | `Person.BirthDate.Year`（**含前史纪年**，负年份合法） | 构造函数（`new GameDate(y, m)`） |
| 4 | `BirthMonth` | `birthMonth` | `int` / 数字 | `Person.BirthDate.Month` | 同上 |
| 5 | `Generation` | `generation` | `int` / 数字 | `Person.Generation` | 构造函数（`>= 0` 被校验；R-12 的按序注册会再校验一次） |
| 6 | `Talents` | `talents` | 数组 / 对象 | `Person.Talents` | 见本表 `TalentDto` |
| 7 | `Study` | `study` | `int` / 数字 | `Person.Study` | 公开 setter（0~100 被校验） |
| 8 | `Health` | `health` | `int` / 数字 | `Person.Health` | 同上 |
| 9 | `Lifespan` | `lifespan` | `int` / 数字 | `Person.Lifespan` | 构造函数（`>= 0` 被校验） |
| 10 | `RankLevel` | `rankLevel` | `int` 或 `nil` / 数字或 `null` | `Person.Rank?.Level` | 公开 setter（`new OfficialRank(l)`，1~18 被校验） |
| 11 | `Merit` | `merit` | `int` / 数字 | `Person.Merit` | 公开 setter（`>= 0` 被校验） |
| 12 | `Status` | `status` | `int` / **枚举名的数组** | `Person.Status`（九位可并存） | 公开 setter（**先于 `Timers`/`EntryTrack` 赋值**，见 §3.11） |
| 13 | `Timers` | `timers` | 数组 / 对象 | `Person.Timers` | 见 `StatusTimersDto` |
| 14 | `Occupation` | `occupation` | `int` / 枚举名 | `Person.Occupation` | 公开 setter |
| 15 | `MonthsInOffice` | `monthsInOffice` | `int` / 数字 | `Person.MonthsInOffice` | 公开 setter（`>= 0` 被校验） |
| 16 | `EntryTrack` | `entryTrack` | `int` 或 `nil` / 名字或 `null` | `Person.EntryTrack` | 公开 setter（非空需 `AwaitingPost`） |
| 17 | `FatherId` | `fatherId` | `str` 或 `nil` / 字符串或 `null` | `Person.FatherId` | 构造函数（**不可变**） |
| 18 | `MotherId` | `motherId` | `str` 或 `nil` / 字符串或 `null` | `Person.MotherId` | 同上 |
| 19 | `SpouseId` | `spouseId` | `str` 或 `nil` / 字符串或 `null` | `Person.SpouseId` | `Person.SetSpouse`（**既有 `internal`**，注册完成后统一写入；R-12） |
| 20 | `FormerSpouseIds` | `formerSpouseIds` | 数组 / 数组 | `Person.FormerSpouseIds` | `Person.AddFormerSpouse`（**既有 `internal`**，逐条按序） |
| 21 | `DegreeHistory` | `degreeHistory` | 数组 / 数组 | `Person.DegreeHistory`（**全量、按序**） | `Person.AppendDegree` 逐条（非降序与原件一致） |
| 22 | `JieShiFailureCount` | `jieShiFailureCount` | `int` / 数字 | `Person.JieShiFailureCount`（**005 新增**，解试累计失败数） | `Person.RestoreExamProgress(…)`（**唯一 `internal` 复原通道**，一次写回 22~24，见 §3.11） |
| 23 | `ShengShiFailureCount` | `shengShiFailureCount` | `int` / 数字 | `Person.ShengShiFailureCount`（**005 新增**，省试累计失败数） | 同上 |
| 24 | `AppliedExam` | `appliedExam` | `int` 或 `nil` / 名字或 `null` | `Person.AppliedExam`（**005 新增**，报名标记：`null` / `JieShi` / `ShengShi`） | 同上（`Person.RestoreExamProgress(jieShiFailureCount, shengShiFailureCount, appliedExam)`；殿试不是合法报名态） |

| 子 DTO | # | 成员 | 线上键名 | 源 / 复原 |
| --- | --- | --- | --- | --- |
| `TalentDto` | 0~3 | `Agriculture` / `Commerce` / `Officialdom` / `Craft` | `agriculture` / `commerce` / `officialdom` / `craft` | `TalentSet` 四成员；`new TalentSet(...)`（0~100 被校验） |
| `StatusTimersDto` | 0~3 | `SentenceRemainingMonths` / `ExamBanRemainingMonths` / `PromotionBanRemainingMonths` / `AwaitingPostRemainingMonths` | `sentenceRemainingMonths` / … | `StatusTimers` 四成员；`new StatusTimers(...)`（非负被校验） |
| `DegreeRecordDto` | 0~5 | `Level` / `Placement` / `ChangedYear` / `ChangedMonth` / `Cause` / `ImperialClass` | `level` / `placement` / `changedYear` / `changedMonth` / `cause` / `imperialClass` | `DegreeRecord` 五成员（年月拆两整数）；`new DegreeRecord(...)`（四条甲第相容性不变量被校验） |

### 3.9 `StatusFlag` 的线上形状

九位并存 ⇒ JSON 写法为**枚举名的数组**（`["AwaitingPost","Retired"]`，可读且可手改），
二进制写法为**位掩码整数**（`int`，`Person.Status` 的原值）。`None`（= 0）在 JSON 中写作**空数组**
`[]`（MUST NOT 写作 `["None"]`，避免与「无名成员」混淆）；读取时数组为空 ⇒ `StatusFlag.None`。
未登记的位（`1 << 9` 及以上）⇒ `InvalidStructure`：二进制形态写的是整数，未登记位读得出来，
但在第 ⑦ 步还原时被位集判据拒绝。JSON 形态写的是**名字**，不认得的名字根本读不出来 ⇒
第 ⑤ 步 ⇒ `Malformed`（两种形态的落点见契约十 §6 条款 6）。

### 3.10 值类型与枚举的线上表示（总表）

| 领域类型 | 线上形态 | 断言要点 |
| --- | --- | --- |
| `Money` | `decimal`（**文**） | 章程「内部一律以文为单位的 `decimal`」；`decimal.GetBits` 逐位（R-10） |
| `GameDate` | 两个 `int`（`year`, `month`） | 年份 `int` 全域（含 `0` 与负数），月份 1~12（R-11） |
| `PersonId` | `Guid` | `Guid.Empty` 被拒 |
| `OfficialRank` | `int`（1~18），`null` = 无官职 | `Person.Rank is null` 表达无官（001 FR-009） |
| 全部 `enum` | 二进制 = 原值整数；JSON = **枚举名** | R-08；非法名 ⇒ 第 ⑤ 步 `Malformed`；越界整数 ⇒ 第 ⑦ 步 `InvalidStructure`（契约十 §6、§3.9） |
| `TalentSet` / `StatusTimers` / `DegreeRecord` | 数组（二进制）/ 对象（JSON） | 成员次序 = R-05 的连续 `[Key]` 次序 |
| `SalaryMode` | **不入档** | 派生量（`Core.Enums.SalaryMode` 的文档即写明），MUST NOT 落字段 |

### 3.11 复原的赋值次序（`SaveStateMapper.ToDomain`）

次序**必须**如此，否则 `Core` 的交叉校验会拒绝（R-04）：

| 步 | 动作 | 为什么 |
| --- | --- | --- |
| ① | 建 `Family`（名 / 仕身份）、按序注册全部 `Person`（配偶留空） | `AddChild` 要求父母已在册（R-12） |
| ② | 写 `Generation`（经构造函数携带）、`Study`/`Health`/`Lifespan`/`Talents`/`Rank`/`Merit`/`Occupation`/`MonthsInOffice`；并写回 005 新增的三个成员——`Person.RestoreExamProgress(jieShiFailureCount, shengShiFailureCount, appliedExam)` | 无相互依赖；三个新成员**只**经这条 `internal` 通道复原，MUST NOT 走公开 setter |
| ③ | 写 `Status`（此时 `Timers`/`EntryTrack` 仍为空 ⇒ 清位的校验恒成立） | `Person.Status` 的 setter 会拒绝「清位但计时非空」 |
| ④ | 写 `Timers`、随后写 `EntryTrack` | 二者的 setter 要求对应状态位**已为真** |
| ⑤ | 逐条 `AppendDegree`（按序） | 非降序校验与原件一致 |
| ⑥ | 写 `SpouseId`（双向）、`FormerSpouseIds` | 全部成员已在册（R-12） |
| ⑦ | `SetHead`、`RestoreOutsiderBookkeeping` | `SetHead` 需在册性；簿记需成员标识（R-03） |
| ⑧ | 建 `Treasury`（三池 + 贷款 + 利率，**先年份后利率**）、`Holdings`、`Ledger`（逐条 `Append`）、`Famine` | `Treasury` 的利率交叉校验 |
| ⑨ | 建 `FamilyEconomy`（生活费档位 + 米价系数 + 三个子聚合）→ 建 `GameState`（identifier / 年月 / 难度 / 出身） | `LivingStandard` 与 `GrainPriceIndex` 是构造必需参数 |
| ⑩ | 跑 `SaveStateValidator`（R-04 的十条） | 不自洽 ⇒ `InvalidStructure` |

> **005 注**：`PersonDto` 的 22~24 三个键（`jieShiFailureCount` / `shengShiFailureCount` / `appliedExam`）
> 由 ② 步的 `Person.RestoreExamProgress` **一次性**写回——它是唯一的复原入口
> （`InternalsVisibleTo("KFL.Infrastructure")`），`Person` MUST NOT 为此新增公开 setter（001 不变量 2）。

---

## 4. 存储侧类型（`KFL.Infrastructure`）

| 类型 | 位置 | 形状与职责 |
| --- | --- | --- |
| `ISaveService` | `Abstractions/ISaveService.cs` | 见契约九 §1：`Exists` / `FindBySaveId` / `Save` / `Load` / `Rename` / `Backup` / `FilePathOf` |
| `SaveSlot` | `Abstractions/SaveSlot.cs` | `readonly record struct SaveSlot(string Name, SaveFormat Format = SaveFormat.Binary)`（FR-005 的「未指定 ⇒ 二进制」由默认参数表达） |
| `SaveFormat` | `Abstractions/SaveFormat.cs` | `enum { Binary = 0, Json = 1 }`（**零值 = 默认格式**） |
| `SaveOptions` | `SaveOptions.cs` | `RootDirectory`（`null` ⇒ 默认 `saves/`）、`DefaultFormat`（默认 `Binary`） |
| `SaveFileException` | `SaveFileException.cs` | `Kind ∈ { Missing, Truncated, Malformed, UnsupportedVersion, InvalidStructure }`（R-07） |
| `SaveNameConflictException` | 同上文件族 | 同名未确认覆盖（FR-008；契约九 §2） |
| `SaveIdentityConflictException` | 同上文件族 | 同根下同 UUID 的另一个文件（R-18） |
| `SaveAccessException` | 同上文件族 | 目录创建失败 / 无写权限（Edge Case；R-15） |
| `SaveFileHeader` | `Save/SaveFileHeader.cs` | `readonly record struct (int FormatVersion, int PayloadLength, Guid SaveId)` |
| `SaveFileFormat` | `Save/SaveFileFormat.cs` | `Version = 2`、扩展名常量、两种格式的**唯一**常量出处（R-06） |
| `SaveName` | `Save/SaveName.cs` | `Validate`/`Normalize`：名称口径的唯一真源（R-13） |
| `AtomicFileWriter` | `Save/AtomicFileWriter.cs` | 临时文件 + 同卷替换；**唯一**写盘路径（R-14） |
| `SaveCatalog` | `Save/SaveCatalog.cs` | 目录枚举、存在性、`SaveId` 探针（只读头）、格式切换时的旧文件清理（R-18） |
| `ISaveFormat` + `MessagePackSaveFormat` / `JsonSaveFormat` | `Save/Serialization/` | `Write(header, dto) → byte[]` / `Read(byte[]) → (header, dto)`；编解码选项的唯一出处（R-08/R-09） |
| `SaveStateDto` 及其子 DTO | `Save/Serialization/*Dto.cs` | 线上形状的唯一真源（§3；R-02） |
| `SaveStateMapper` | `Save/Serialization/SaveStateMapper.cs` | `ToDto(GameState)` / `ToDomain(SaveStateDto)`，逐字段显式（§3；R-02/R-12） |
| `SaveStateValidator` | `Save/Serialization/SaveStateValidator.cs` | R-04 的十条一致性校验 |
| `SaveService` | `Save/SaveService.cs` | `ISaveService` 的实现：装配 `SaveName`/`SaveCatalog`/`AtomicFileWriter`/`ISaveFormat` |

---

## 5. 规则侧类型（`KFL.Rules`）

| 类型 | 位置 | 形状与职责 |
| --- | --- | --- |
| `SaveCadencePolicy` | `Config/SaveCadencePolicy.cs` | 静态类：`EpochYear = 1`、`EpochMonth = 1`、`BackupIntervalTurns = 3`、`CompletedTurnsAt(GameDate)`、`ShouldBackup(int)`（R-16） |
| `TurnRunner` | `Save/TurnRunner.cs` | `TurnRunner(IRandomService randomService, ISaveService? saveService, SaveSlot slot)`；`TurnOutcome Advance(GameState state)`；`void SaveOnExit(GameState state)`（R-17；契约十一 §2） |
| `TurnOutcome` | `Save/TurnOutcome.cs` | `readonly record struct (SettlementResult Settlement, int CompletedTurns, bool Saved, bool BackedUp)`——一次回合的**增量**快照，MUST NOT 被写回 `GameState` |
| `GameConfig` | `Config/GameConfig.cs` | **改**：新增 `Save` 组转发（`SaveCadencePolicy` 的三个数值），保持「零字面量」的既有口径 |

`SaveCadencePolicy.CompletedTurnsAt` 的语义（**唯一**的回合数定义）：

```text
CompletedTurnsAt(current) = (current.Year − EpochYear) × 12 + (current.Month − EpochMonth)
```

其中 `current` = **一次成功结算之后**的 `GameState.CurrentDate`（⑦ 步已推进）。故第 1 次结算后
`CompletedTurnsAt == 1`，第 3、6、9、12 次后分别为 3、6、9、12 ⇒ 备份（SC-003）。

---

## 6. 状态转移

### 6.1 写入（`Save`）

| 起始 | 触发 | 结果 | 失败路径 |
| --- | --- | --- | --- |
| 任意状态 + 槽位 | `Save(state, slot, overwrite)` | 校验名称 → 校验冲突 → 序列化主体 → 写头 → **原子替换** → 清理同名的异格式文件 | 名称非法 ⇒ `ArgumentException`；同名未确认 ⇒ `SaveNameConflictException`；同 UUID 异名 ⇒ `SaveIdentityConflictException`；目录不可写 ⇒ `SaveAccessException` |
| 同上，且发生在**不可逆点之后** | — | 目标文件已是新内容（原子替换成功即完成） | 替换失败 ⇒ 目标仍是**上一份完整存档**（FR-013/SC-006） |

**不变量 10（原子性）**：任意时刻目标路径上的文件 MUST 要么是写入前的完整存档、要么是写入后的完整
存档；MUST NOT 出现半截文件（FR-013）。

### 6.2 读取（`Load`）

| 步 | 判定 | 不通过时 |
| --- | --- | --- |
| ① | 目标文件存在 | `Missing` |
| ② | 头记录可读（形状 + 三成员） | `Malformed` |
| ③ | `文件剩余字节 ≥ PayloadLength` | `Truncated` |
| ④ | `FormatVersion == 2` | `UnsupportedVersion` |
| ⑤ | 主体文本/字节可解析 | `Malformed` |
| ⑥ | 必需成员齐备（`[JsonRequired]` / MessagePack 数组长度） | `InvalidStructure` |
| ⑦ | 沿 `Core` 校验路径逐字段复原（§3.11） | `InvalidStructure` |
| ⑧ | `SaveStateValidator` 十条 | `InvalidStructure` |

**不变量 11（失败无副作用）**：`Load` 失败 MUST NOT 修改存档文件、MUST NOT 返回默认状态或部分状态、
MUST NOT 污染调用方已有的内存状态（FR-014；SC-007）。

### 6.3 改名、备份与格式切换

| 操作 | 起始 | 结果 | 依据 |
| --- | --- | --- | --- |
| `Rename(slot, newName)` | 存在 `name.ext` | 校验新名 → 目标不冲突 → **主档**文件移动 → **备份**（若有）同时改名；**字节逐字节不变** ⇒ `SaveId` 与游戏内容不变 | FR-009；SC-004；R-18/R-25；**spec 裁决 Q5** |
| `Backup(slot)` | 存在 `name.ext` | 原子复制为 `backups/name.ext`（覆盖式，恒 1 份，**改名/换格式后亦然**） | FR-011；Q1/Q5 裁决；R-19 |
| 格式切换 | `name.dat` 存在、请求写 `name.json` | 先成功写出 `name.json`，**再**删除 UUID 相同的 `name.dat`，**并移除旧格式备份** | Assumptions；R-18；**spec 裁决 Q5** |
| 同名覆盖（异格式、异 UUID） | `name.dat` 存在、`overwrite: true`、写 `name.json` | 写出 `name.json` 后移除同名 `name.dat`（「该名字归调用方」） | R-18 |
| 同名判定（`Exists` / 覆盖冲突 / 改名目标） | 任意 | **大小写不敏感**（`SongJia` ≡ `songjia`）；保存时保留输入写法 | FR-008；**spec 裁决 Q7** |

### 6.4 自动保存与备份节奏（`TurnRunner.Advance`）

| 步 | 动作 | 中断/失败的后果 |
| --- | --- | --- |
| ① | 结算（`MonthlySettlementEngine.Settle`） | 抛出 ⇒ **不保存**（FR-010） |
| ② | 未注入 `ISaveService` ⇒ 直接返回（自动保存关闭） | — |
| ③ | `Save(state, slot, overwrite: true)`（`slot` = 装配期注入的当前槽位，**spec 裁决 Q4**：MUST NOT 进入 `GameState`） | 抛出 ⇒ **保留已结算的内存状态**并向上传播、不备份（MUST NOT 回滚 / 静默吞；**spec 裁决 Q6**） |
| ④ | `CompletedTurnsAt` 命中 `ShouldBackup` ⇒ `Backup(slot)`（回合数由 `CurrentDate` 相对纪元 `1 年 1 月` 派生，恒为正——**spec 裁决 Q8**） | 抛出 ⇒ 传播（当前档已写好，不受影响） |

`SaveOnExit(GameState)` = **同一个** `Save` 入口 + **同一个当前槽位**（FR-012），MUST NOT 另写一套。

### 6.5 本特性**不建立**的状态转移

| 不建立的转移 | 归属 |
| --- | --- |
| 存档格式的**迁移**（旧版本 → 新版本） | Out of Scope（规格书 §14 未定义任何迁移规则） |
| 成就的解锁与按 UUID 归属 | 逻辑轨 ⑧（本特性只保证 UUID 被如实持久化且改名不动它） |
| 进程退出钩子 / 主菜单事件 / 覆盖确认弹窗 / 设置面板的「存档格式」项 | 界面轨 U1/U5（本特性只交付被它们调用的能力） |
| 多进程 / 并发写协调、文件锁 | 规格书未要求；单机单进程游戏（Assumptions） |
| 存档写入时间戳、CSV 导出、统计页 | 界面轨 U3 / 无该需求（R-23） |

---

## 7. 需求 → 模型映射

| 需求 | 落点 | 验证物 |
| --- | --- | --- |
| FR-001 | `ISaveService`（`Infra/Abstractions`）+ `SaveService` | `SaveServiceTests`：全程无 UI、无 `KFL.App` 引用 |
| FR-002 | §3 的逐字段表（80 个线上成员） | `SaveFormatContractTests` 的字段类别断言（≥14 类） |
| FR-003 | §3.11 的复原次序 + R-04 | US1 的逐字段等价断言（两种格式各一轮） |
| FR-004 | `ISaveFormat` 的两个实现 + §2.3 的两种布局 | 两格式各自往返 + `SaveFormatContractTests` 的键表断言 |
| FR-005 | `SaveSlot.Format` 默认 `SaveFormat.Binary` | 未指定格式 ⇒ `.dat`；两格式结果等价 |
| FR-006 | `SaveOptions.RootDirectory` + R-15 | 自定义根生效 / 未配置 ⇒ 可执行文件旁 `saves/` |
| FR-007 | `SaveName`（R-13） | 中文档名成功；9 个非法字符逐条拒绝；`-` 放行 |
| FR-008 | `Exists` + `overwrite` 参数 + 契约九 §2 的矩阵 | 同名未确认 ⇒ 拒绝；`Exists` 与格式无关 |
| FR-009 | `Rename` = 纯文件移动（R-25） | 改名前后**字节逐字节相同**；`Family.Name` 不变 |
| FR-010 | `TurnRunner` 步骤 ①③（R-17） | 12 回合覆盖写 12 次；结算抛出时当前档不变 |
| FR-011 | `TurnRunner` 步骤 ④ + `Backup`（R-16/R-19） | 第 3/6/9/12 回合各备份一次，恒 1 份 |
| FR-012 | `TurnRunner.SaveOnExit`（复用同一入口） | 替身断言两次调用命中同一 `Save` 重载与实参 |
| FR-013 | `AtomicFileWriter`（R-14）+ 不变量 10 | `AtomicWriteTests`：中途失败注入 + 备份中断 |
| FR-014 | §6.2 的八步判定 + 不变量 11 | `SaveFailureTests`：五类失败各自可识别，原文件与内存状态未变 |
| FR-015 | 版本闸门（R-06）+ `[JsonRequired]`（R-07） | 版本不符 / 缺成员的**明确**失败 |
| FR-016 | R-10 的 `decimal` 原值 | `decimal.GetBits` 逐位相同（含 scale） |
| FR-017 | §3.11 的全量复原（含簿记）+ 无时钟（R-23） | 读回后继续推进 12 个月 vs 「从未落盘」基线逐字段相同 |
| FR-018 | 存档实现**不含**任何时钟 / 全局随机（G-07 零违规） | `EnvironmentDependencyTests` 与 `SaveCadenceTests` 的固定回合数驱动 |
| FR-019 | 三处文件系统收敛点（R-15）+ 逐行 `// arch-guard:allow` | `EnvironmentDependencyTests` + 可列举的豁免行 |
| FR-020 | R-02（瞬态 DTO）+ R-03（`internal` 最小面） | `KFL.Core` 无序列化注解；无公开写通道被新增 |
| FR-021 | 不变量 8（单一文件、全量、不分块、不压缩） | `SaveFormatContractTests`：1200 回合后条目丢失数 = 0 |
| FR-022 | R-21 的测试组织（临时目录 + 内存替身） | 门禁跑完仓库存档相关变更数 = 0（SC-009） |
| FR-023 | `.gitignore` 增 `saves/`（R-20） | 试跑后 `git status` 洁净 |
| FR-024 | MessagePack `2.5.305`（R-24，含实测表） | 构建 0 警告 0 错误；`dotnet list package` 只有这一个新包 |

---

## 8. 不变量清单（存储侧）

| # | 不变量 | 验证方式 |
| --- | --- | --- |
| 1 | DTO 是瞬态的，MUST NOT 被领域/服务长期持有 | `DTO` 不出现在任何 `Core`/`Rules` 的字段或签名里（人工核验 + `Rules` 不引用 `Infra.Save.Serialization`） |
| 2 | 字段映射逐字段显式，无反射、无无注解契约 | 代码审查 + `SaveFormatContractTests` 的键表断言 |
| 3 | 头 `SaveId` == 主体 `Id` | R-04 第 8 条；篡改头即 `InvalidStructure` |
| 4 | `PayloadLength` == 主体字节数 | 只用于判截断（R-05/R-07） |
| 5 | 同根下同 `SaveId` 至多一个文件 | `SaveIdentityConflictException`；`SaveServiceTests` |
| 6 | 同根下同名至多一个文件（跨格式） | `Exists`（与格式无关）+ 覆盖/切换的清理（R-18） |
| 7 | `TreasuryPool` 等派生量不入档 | 线上键表里不存在这些成员 |
| 8 | 账本全量、顺序一致、无冗余汇总 | `Entries.Count` 与逐条比对；SC-010 的 1200 回合 |
| 9 | 金额按 `decimal` 原值（含 scale） | `decimal.GetBits` 逐位（R-10） |
| 10 | 写入原子（要么旧、要么新） | SC-006 的失败注入；`AtomicWriteTests` |
| 11 | 读取失败无副作用 | SC-007：原文件与内存状态差异 = 0 |
| 12 | 落盘不改推演结果 | US1 AS5：读回后 12 个月 vs 基线逐字段相同 |
| 13 | 存档名与家族姓氏解耦 | SC-004：改名前后 `Family.Name` 不变 |
| 14 | 无墙钟、无全局随机、无网络（G-07 零违规） | `EnvironmentDependencyTests` |
| 15 | `JieShiFailureCount >= 0` ∧ `ShengShiFailureCount >= 0` | `SaveStateValidator` 第 ⑩ 条；负值 ⇒ `InvalidStructure`（005） |
| 16 | `AppliedExam ∈ { null, JieShi, ShengShi }`（殿试不是合法报名态） | `SaveStateValidator` 第 ⑩ 条（`Enum.IsDefined` 拒绝 `0` 与 `> 3`）；JSON 坏名 ⇒ `Malformed`、二进制越界整数 ⇒ `InvalidStructure`（005） |
| 17 | 二进制 walker 的成员数常量 == DTO 键数（`PersonMemberCount` = 25、`FamilyMemberCount` = 7，连同对应 Skip 计数） | `SaveFormatContractTests` 按 005 的键数断言（005） |

---

## 9. 本数据模型明确不包含

- **成就持久化**（`achievements.json`、按难度记录、UUID 归属判定与提示）：逻辑轨 ⑧。
- **界面状态与交互**（存档列表、覆盖确认弹窗、设置面板）：界面轨 U1/U5。
- **进度条 / 统计页 / CSV 导出**：界面轨 U3。
- **旧格式迁移、向下兼容、多版本共存**：Out of Scope（R-06 的闸门只**拒绝**，不**转换**）。
- **多进程/并发协调、文件锁、写入时间戳**：规格书未要求（Assumptions；R-23）。
- **任何新的领域字段或规则数值**：本特性只把既有状态搬上磁盘；新增的唯一数值是
  `BackupIntervalTurns = 3` 与纪元 `(1, 1)`，二者**逐条出自**规格书 §14 与 §3（R-16）。

---

## 10. 005 后记（`005-exam-cycle` 同步注）

本文件的存储侧形状随 005 的**跨特性形状变更**同步（变更授权：
`specs/005-exam-cycle/plan.md` §「跨特性形状变更」表第 14 行、
`specs/005-exam-cycle/contracts/exam-calendar.md`）：

| 同步点 | 变更 |
| --- | --- |
| §2.2、§2.3、§4 | `SaveFileFormat.Version` 由 `1` 升为 `2`；头记录与 JSON 布局里的 `FormatVersion` 字样同步；版本闸门的接受谓词为 `FormatVersion == 2`（`< 2` 与 `> 2` 一律 `UnsupportedVersion`，且判定在**任何正文解析之前**） |
| §3.2 | `FamilyDto` 6 → **7** 成员（末尾追加 `AutoExamApplication` / `autoExamApplication`(bool)） |
| §3.8 | `PersonDto` 22 → **25** 成员（末尾追加 `JieShiFailureCount` / `ShengShiFailureCount` / `AppliedExam`，线上键 `jieShiFailureCount` / `shengShiFailureCount` / `appliedExam`）；既有成员的序号与键名不变 |
| §3.11 | 复原次序第 ② 步并入 005 的三个新成员——`Person.RestoreExamProgress(jieShiFailureCount, shengShiFailureCount, appliedExam)` 是唯一复原通道（`internal`，`InternalsVisibleTo("KFL.Infrastructure")`），MUST NOT 借公开 setter 复原 |
| §4、§6.2、§3.11 | `SaveStateValidator` 由「九条」变「**十条**」；第 ⑩ 条 = `appliedExam` 合法值（`null` ∨ ∈ { `JieShi`, `ShengShi` }）+ 两个失败数 `>= 0` |
| §7、§8 | `FR-002` 的线上成员计数 76 → **80**；新增不变量 15/16/17 |

**边界**：005 只**加宽** DTO 并**升版本号**，不引入任何迁移/兼容路径（`< 2` 的旧档一律 `UnsupportedVersion`）；
`Person` / `Family` 的领域语义真源仍是 [005 data-model](../../005-exam-cycle/data-model.md)，本文件只记录它们如何变成字节。

