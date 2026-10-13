# 契约十：存档文件格式（二进制与 JSON）

**Feature**: `004-save-system` | **Date**: 2026-10-11

本契约钉住**线上形状**：头记录、主体的 DTO 键表、两种格式的编解码选项、版本闸门与失败分类的判据。
它是 FR-004（两种格式）、FR-005（默认二进制）、FR-015（MUST NOT 静默误读）与 FR-021（单一文件、
全量、不分块、不压缩）的可执行形式。

## 1. 文件布局

```text
[头记录][主体]          ← 两种格式同形（R-05）；单文件、全量、不分块、不压缩（FR-021）
```

| 条款 | 要求 |
| --- | --- |
| 1 | 一份存档 MUST 是**恰好一个文件**；MUST NOT 分块、MUST NOT 压缩、MUST NOT 有旁挂索引或侧车文件 |
| 2 | 头记录 MUST 在主体**之前**，且 MUST 可**独立读出**（不解析主体即可得到版本、长度、标识） |
| 3 | `PayloadLength` MUST 等于主体字节数；写入时同源产生 |
| 4 | 文件扩展名：二进制 `.dat`（**默认**）、JSON `.json`（FR-004/FR-005） |
| 5 | 同一状态以两种格式存读 MUST 得到**信息集等价**的结果（§5；FR-004/FR-005） |

## 2. 头记录

| 位置 | 成员 | 类型 | 二进制（`.dat`） | JSON（`.json`） |
| --- | --- | --- | --- | --- |
| 1 | `FormatVersion` | `int` | `MessagePackWriter.Write(int)` | `"FormatVersion"` 数字成员 |
| 2 | `PayloadLength` | `int` | 同上 | `"PayloadLength"` 数字成员 |
| 3 | `SaveId` | `Guid` | `Write(string)`（`D` 格式，36 字符） | `"SaveId"` 字符串成员 |

二进制头的完整形状 = `fixarray(3)` 起手（`0x93`）后跟三个成员；JSON 头的完整形状 =
`{"FormatVersion":2,"PayloadLength":N,"SaveId":"…","State":<主体>}`（`State` 之前恰有这三个成员，
次序固定）。

**不变量**
1. 头 `SaveId` MUST 等于主体 `Id`；不等 ⇒ `InvalidStructure`（[契约九 §2](./save-service.md)）。
2. 头 MUST NOT 参与「拒绝手改」的校验和语义：`PayloadLength` **只**用于判截断（R-05 的备选 ② 已被否，
   以保住 §14 的「JSON 可读、可手改」）。

## 3. 编解码选项

| 格式 | 选项（**唯一**出处：`SaveFileFormat`） | 依据 |
| --- | --- | --- |
| 二进制 | `MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData)`；压缩 = `None` | R-09；FR-021 |
| JSON | `WriteIndented = true`；`Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`；`Converters = { new JsonStringEnumConverter() }`；DTO 每成员显式 `[JsonPropertyName]` + `[JsonRequired]` | R-08；R-07；§14 的「可读、可手改、便于排查」 |
| 两者 | 枚举：二进制写**原值整数**、JSON 写**枚举名**；`decimal` 一律**原值**（含 scale）；`Guid` 写字符串 | R-08；R-10 |

| 条款 | 要求 |
| --- | --- |
| 1 | 读入 MUST 以「不受信任的外部输入」为前提（玩家可手改、可损坏）：二进制 MUST 启用 `UntrustedData` |
| 2 | MUST NOT 启用压缩（FR-021）、MUST NOT 启用「容忍注释/尾随逗号」（会放宽「结构合法」的判据，R-08 的备选 ③） |
| 3 | 编码选项 MUST 集中声明、MUST NOT 散落在各调用点（同一个选项在两个地方构造就会出现两份真相） |
| 4 | 线上键名 MUST 显式固定；C# 侧的属性改名 MUST NOT 改变线上键名（R-08） |

## 4. 主体 DTO 键表

**本节是主体线上形状的唯一真源**；`MessagePack` 的 `[Key]` 从 `0` **连续**编号（⇒ **数组格式**，
位置式：成员缺失即结构错误），JSON 的 `[JsonPropertyName]` 逐项如下。

### 4.1 `SaveStateDto`

| Key | 键名 | 类型 |
| --- | --- | --- |
| 0 | `id` | `Guid` |
| 1 | `currentYear` | `int` |
| 2 | `currentMonth` | `int` |
| 3 | `difficulty` | `Difficulty` |
| 4 | `pendingDifficulty` | `Difficulty?` |
| 5 | `origin` | `Origin` |
| 6 | `family` | `FamilyDto` |
| 7 | `economy` | `FamilyEconomyDto` |

### 4.2 `FamilyDto` / `PersonDto` 及其子 DTO

| DTO | Key → 键名 |
| --- | --- |
| `FamilyDto` | 0 `name`(string)、1 `hasShiStatus`(bool)、2 `headId`(Guid?)、3 `members`(PersonDto[])、4 `settledOutsiders`(Guid[])、5 `marriageAlignedOutsiders`(Guid[])、6 `autoExamApplication`(bool) |
| `PersonDto` | 0 `id`(Guid)、1 `name`(string)、2 `gender`、3 `birthYear`(int)、4 `birthMonth`(int)、5 `generation`(int)、6 `talents`(TalentDto)、7 `study`(int)、8 `health`(int)、9 `lifespan`(int)、10 `rankLevel`(int?)、11 `merit`(int)、12 `status`(StatusFlag)、13 `timers`(StatusTimersDto)、14 `occupation`、15 `monthsInOffice`(int)、16 `entryTrack`(AppointmentTrack?)、17 `fatherId`(Guid?)、18 `motherId`(Guid?)、19 `spouseId`(Guid?)、20 `formerSpouseIds`(Guid[])、21 `degreeHistory`(DegreeRecordDto[])、22 `jieShiFailureCount`(int)、23 `shengShiFailureCount`(int)、24 `appliedExam`(`ExamLevel?`) |
| `TalentDto` | 0 `agriculture`、1 `commerce`、2 `officialdom`、3 `craft`（皆 `int`） |
| `StatusTimersDto` | 0 `sentenceRemainingMonths`(int?)、1 `examBanRemainingMonths`(int?)、2 `promotionBanRemainingMonths`(int?)、3 `awaitingPostRemainingMonths`(int?) |
| `DegreeRecordDto` | 0 `level`、1 `placement`、2 `changedYear`(int)、3 `changedMonth`(int)、4 `cause`、5 `imperialClass` |

### 4.3 `FamilyEconomyDto` 及其子 DTO

| DTO | Key → 键名 |
| --- | --- |
| `FamilyEconomyDto` | 0 `livingStandard`、1 `pendingLivingStandard`、2 `grainPriceIndex`(decimal)、3 `treasury`(TreasuryDto)、4 `holdings`(HoldingsDto)、5 `ledger`(LedgerEntryDto[])、6 `famine`(FamineDto) |
| `TreasuryDto` | 0 `cash`(decimal)、1 `savings`(decimal)、2 `merchantCapital`(decimal)、3 `loan`(LoanDto)、4 `savingsRate`(decimal?)、5 `savingsRateYear`(int?) |
| `LoanDto` | 0 `principal`(decimal)、1 `accruedInterest`(decimal)、2 `monthsSinceInterest`(int) |
| `HoldingsDto` | 0 `farmlandMu`、1 `ruralHouses`、2 `urbanHouses`、3 `shops`（皆 `int`） |
| `LedgerEntryDto` | 0 `year`(int)、1 `month`(int)、2 `personId`(Guid?)、3 `category`、4 `amountWen`(decimal) |
| `FamineDto` | 0 `stage`、1 `elapsedMonths`(int) |

**金额一律以「文」为单位的 `decimal`**（章程「技术栈与工程约束」）；**`SalaryMode`、`TreasuryPool`
等派生量 MUST NOT 出现在任一键表里**。

**条款**：字段的语义、来源与复原入口逐条见 [data-model §3](../data-model.md)——
本契约只固定**键与类型**，MUST NOT 另行维护语义副本。

## 5. 信息集等价条款

| 条款 | 要求 |
| --- | --- |
| 1 | 两种格式 MUST 承载**同一信息集**：§4 的键表是两种格式共用的唯一字段清单（MUST NOT 出现「某种格式少存一块」） |
| 2 | 同一状态经任一种格式存读后 MUST **逐字段相等**（含 `decimal` 的 scale、归档成员、簿记、计时与全量账本） |
| 3 | 任一字段的缺失 MUST 被检出（JSON：`[JsonRequired]`；二进制：位置式数组长度）——MUST NOT 静默默认化 |
| 4 | 等价性 MUST 由**键表断言** + **逐字段往返断言**共同覆盖（只做往返会漏掉「两格式同缺一个字段」，R-22） |

## 6. 版本闸门与失败分类

```text
① 文件存在？           否 ⇒ Missing
② 头记录可读？         否 ⇒ Malformed
③ 剩余字节 ≥ PayloadLength？ 否 ⇒ Truncated
④ FormatVersion == 2？ 否 ⇒ UnsupportedVersion（> 2 = 更新版本写出；< 2 = 旧版本，无迁移路径）
⑤ 主体可解析？         否 ⇒ Malformed
⑥ 必需成员齐备？       否 ⇒ InvalidStructure
⑦ 逐字段复原（Core 校验）？ 否 ⇒ InvalidStructure
⑧ 一致性校验（十条）？ 否 ⇒ InvalidStructure
```

| 条款 | 要求 |
| --- | --- |
| 1 | 版本闸门（④）MUST 在任何主体解析之前；接受的**唯一**谓词是 `FormatVersion == 2`——`> 2` 与 `< 2` MUST 一律拒绝（FR-015 的正面用例），MUST NOT 静默误读 |
| 2 | 本版本 MUST NOT 提供任何迁移/兼容路径（Out of Scope）；`< 2`（含旧版 `1`）同样拒绝 |
| 3 | 失败分类的判据 MUST 与「SC-007 的四类输入」一一对应，MUST NOT 依赖异常消息文本（R-07 的备选 ② 已被否） |
| 4 | 全部失败路径 MUST NOT 修改原文件、MUST NOT 返回默认或部分状态（FR-014；[契约九 §2](./save-service.md)） |
| 5 | 读取 MUST 只读该文件本身；MUST NOT 依赖任何外部状态（无时钟、无环境变量、无网络） |
| 6 | **枚举的失败落点由线上形态决定**：JSON 写枚举**名**，不认得的名字在 ⑤ 就失败 ⇒ `Malformed`；二进制写**原值整数**，越界整数与未登记状态位都读得出来、但在 ⑦ 被拒 ⇒ `InvalidStructure`（data-model §3.9、§3.10） |
| 7 | **第 ⑩ 条校验（005 新增）**：`appliedExam` 非空 ⇒ MUST ∈ { `JieShi`, `ShengShi` }（`Enum.IsDefined` 拒绝 `0` 与 `> 3`；殿试 `DianShi` 不是合法报名态）；`jieShiFailureCount` 与 `shengShiFailureCount` MUST `>= 0`。二进制写原值整数 ⇒ 越界整数在 ⑦ 被拒 ⇒ `InvalidStructure`；JSON 写枚举名 ⇒ 不认得的名字在 ⑤ 就读不出 ⇒ `Malformed`（与条款 6 同口径） |

## 7. 常量登记、依赖与验证物

| 常量 | 声明处 | 依据 | 断言锚点 |
| --- | --- | --- | --- |
| 文件格式版本 **2** | `SaveFileFormat.Version` | §14（未规定版本字段，按 §17② 取「拒绝优于误读」）；005 由 `1` 升为 `2` | 版本闸门的两条用例（`3` 拒绝、`1` 拒绝） |
| 扩展名 `.dat` / `.json` | `SaveFileFormat` | §14 | 未指定格式 ⇒ `.dat`；指定 JSON ⇒ `.json`（FR-005） |
| 存档根默认 `saves/` | `SaveOptions`（`null` ⇒ `AppContext.BaseDirectory`） | §14；章程「运行时路径 MUST 可配置」 | 自定义根生效 / 默认根生效（各一条） |
| 备份目录 `backups/` | `SaveFileFormat` | §14 的 Q1 裁决 | [契约十一 §4](./save-cadence.md) |

**这不是数值登记表**：本契约**不新增、不改动任何影响平衡的数值**（上表全是文件格式常量与路径，
不属 `KFL.Rules/Config/`）。因此
`tests/KFL.Tests/Architecture/ConfigLiteralTests.RegisteredValues` **无需增补任何小数**
（本特性没有新增小数规则值；规格书 §14 的「3 回合」是整数，按 002 契约五 §5 的既有口径不入清单）。
**唯一**新增的规则数值 `BackupIntervalTurns = 3` 的登记见 [契约十一 §4](./save-cadence.md)。

| 依赖 | 版本 | 必要性（FR-024） |
| --- | --- | --- |
| `MessagePack` | `2.5.305` | 规格书 §1/§14 指定的二进制存档实现，且在章程的依赖基线内。已实测：`net10.0` 构建 **0 警告 0 错误**；连续 `[Key]` ⇒ **数组格式**；`decimal` 的 scale 逐位保留；`MessagePackSecurity.UntrustedData` 可用；手写头 + `Consumed` 切片反序列化可用（R-24） |
| `System.Text.Json` | BCL（无新增包） | JSON 格式（§14）；`[JsonRequired]` 给出「缺必需成员」的明确失败（R-07），`JsonStringEnumConverter` 给出可读枚举（R-08） |

**MUST NOT** 引入 `MessagePackAnalyzer`、`MessagePack.ReactiveProperty` 或任何第三方文件系统/JSON 抽象。

| 验证物（测试） | 覆盖 |
| --- | --- |
| `SaveFormatContractTests` | §4 的键表逐项断言；§3 的枚举写法（名 vs 整数）；§2 的头三成员与次序；§5 的信息集等价；FR-021 的 1200 回合体量 |
| `SaveServiceTests` | 两种格式的往返等价（US1 AS1/AS2/AS3/AS4/AS6） |
| `SaveFailureTests` | §6 的八步判定逐类注入（SC-007） |

## 8. 本契约明确不包含

- **服务入口、命名、原子性、唯一性、改名、备份**：[契约九](./save-service.md)。
- **节奏与编排**（何时保存、何时备份、回合数定义）：[契约十一](./save-cadence.md)。
- **字段语义与复原入口**：[data-model §3](../data-model.md)（本契约只固定键与类型）。
- **旧格式迁移 / 向下兼容 / 多版本共存**：Out of Scope。
- **压缩、分块、按年索引、增量/差分存档**：FR-021 明文排除（Q2 裁决：单一文件、全量、不分块、不压缩）。
- **写入时间戳与任何时钟信息**：R-23（不引入系统时钟）。
- **界面**（格式设置项、存档列表、覆盖确认弹窗）：界面轨 U1/U5。

---

## 005 后记（`005-exam-cycle` 同步注）

本契约随 005 的**跨特性形状变更**同步（变更授权在 `specs/005-exam-cycle/plan.md` 与
`specs/005-exam-cycle/contracts/exam-calendar.md`）：

| 同步点 | 变更 |
| --- | --- |
| §2、§4.2、§7 | 头记录 `FormatVersion` 与 `SaveFileFormat.Version` 由 `1` 升为 `2`；版本闸门的接受谓词改为 `FormatVersion == 2`（`< 2` 与 `> 2` 一律 `UnsupportedVersion`） |
| §4.2 | `FamilyDto` 6 → **7** 键（末尾追加 `6 autoExamApplication`(bool)）；`PersonDto` 22 → **25** 键（末尾追加 `22 jieShiFailureCount`(int)、`23 shengShiFailureCount`(int)、`24 appliedExam`(`ExamLevel?`)）；既有键的 `Key` 序号与键名**不变**，新键**只追加** |
| §6 | 一致性校验由「九条」变「**十条**」，新增第 ⑩ 条（`appliedExam` 合法值 + 两个失败数非负）及其 `InvalidStructure` / `Malformed` 落点（条款 7） |

三个新键的**语义、来源与复原入口**（`Person.RestoreExamProgress`）见 [data-model §3.2、§3.8、§3.11](../data-model.md)——
本契约只固定**键与类型**，MUST NOT 另行维护语义副本。

