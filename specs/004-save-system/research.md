# 阶段 0 研究：存档体系的技术抉择

**Feature**: `004-save-system` | **Date**: 2026-10-11 | **Spec**: [spec.md](./spec.md)

本阶段 25 项抉择**全部有结论，无 `NEEDS CLARIFICATION` 残留**。规格书 §14 的**三项用户裁决
（Q1~Q3）**在 `$speckit-specify` 阶段已完成并回写规格书 §14，本文件直接继承，只展开**技术侧**
的抉择；凡涉数值者一律标注「不新增、不改动任何数值」。

一件必须如实记录的前置事实：本特性在**公开面**上没有对任何上游类型做**形状变更**（零领域字段
增删、零**公开**签名变更、无步骤序列插入），但在 `internal` 面上有 6 处放宽 / 新增——
`FamineState.Stage` / `ElapsedMonths` 放宽为 `internal set`、`Family.CaptureOutsiderBookkeeping()`、
`Family.RestoreOutsiderBookkeeping(...)`、新增 `OutsiderBookkeeping` 类型、新增
`AssemblyInfo.cs` 的 `InternalsVisibleTo`、`FamilyEconomy.RestoreFamine(...)`。故 `tasks.md` 要登记的是**声明与验证义务**的回写
（哪些「待阶段④ 处理」的登记被结清、哪一个上游契约的「接口待定义」被落实），外加这 6 处的
**字段 / 签名级落点登记**。逐项清单见 [plan.md](./plan.md) 的「回写义务清单」，6 处的条目见
[spec.md](./spec.md) 的「本特性对上游 `KFL.Core` 的形状变更」。

---

## R-01 存档能力的层分布

**Decision**: 分三层落位，严格遵守 `Core ← Infrastructure ← Rules`（G-05）：

| 关注点 | 落点 | 依据 |
| --- | --- | --- |
| 跨层接口与存储侧值类型（`ISaveService`、`SaveSlot`、`SaveFormat`、`SaveOptions`、`SaveFileException`） | `KFL.Infrastructure/Abstractions/` 与 `KFL.Infrastructure/Save/` | 规格书 §2 把 `ISaveService` 列在 `KFL.Infrastructure`；FR-001 |
| 文件系统、原子写、命名校验、编解码、DTO 与映射 | `KFL.Infrastructure/Save/` | 章程原则 II（`Core`/`Rules` 不得直接碰文件系统） |
| 「存档节奏」的决策（每回合保存、每 3 回合备份）与「结算 → 保存 → 备份」的编排 | `KFL.Rules/Config/SaveCadencePolicy`（数值）与 `KFL.Rules/Save/TurnRunner`（编排） | 章程「技术栈与工程约束」（规则数值集中在 `KFL.Rules/Config/`）；`Rules` 是唯一同时看得见结算引擎与 `ISaveService` 的层 |

**Rationale**: `KFL.Rules` 引用了 `KFL.Infrastructure`（G-05 允许），故「结算 + 落盘」的编排只能落在
`Rules`；反过来 `Infrastructure` **看不到** `Rules`，故「每 3 回合」这一数值与回合数推算不可能住在
存档服务里——若硬塞进去，`3` 会变成 `KFL.Infrastructure` 里的规则数值副本（SC-008 违规）。

**Alternatives considered**: ① 把 `ISaveService` 放 `KFL.Core`（最下层）——规格书 §2 已把它指定在
`Infrastructure`，且会让 `Core` 持有环境边界（001 契约二 §4 条款 3 明令禁止）；② 把节奏判定放进
存档服务的构造函数参数、由装配期喂入 `3`——`3` 的出处仍是 `KFL.Rules/Config`，但「该不该备份」的
判定逻辑会沉到 `Infrastructure`，与规格书 §2 的「规则引擎全在 `KFL.Rules`」冲突。两者皆否。

---

## R-02 持久化模型（DTO）与映射：不碰 `KFL.Core` 的注解

**Decision**: 在 `KFL.Infrastructure/Save/Serialization/` 下定义**一套专用 DTO**（`SaveStateDto`
及其子 DTO）与**显式映射器**（`SaveStateMapper`），逐一字段地把 `GameState` 映射进 DTO、再从 DTO
复原。`KFL.Core` 的类型上**不出现任何序列化框架的注解**。

DTO 是**瞬态的**：只在一次 `Save`/`Load` 调用的栈内存活，MUST NOT 被 `GameState`、任何规则类型
或服务实例长期持有——故它**不是**与领域聚合并存的第二份内存真源（FR-020 的第二子句）。

**Rationale**: FR-020 明文「MUST NOT 让 `KFL.Core` 依赖具体序列化框架」。若把 `[MessagePackObject]`
写在 `KFL.Core/Entities/*.cs` 上，`Core` 就与 MessagePack 的具体版本绑死；此外 `Person.Generation`、
`SpouseId`、`FormerSpouseIds`、`Family.HeadId` 这些**不可变且有私有写通道**的成员，用「框架反射式
序列化」也复原不回去（框架无法穿过不变量校验）。显式映射器则天然是「逐字段、可逐行审阅、可逐条
断言」的，正好给 SC-001 的「≥14 类字段逐类断言」一个可指认的落点。

**Alternatives considered**: ① 在 `Core` 类型上加序列化注解（违反 FR-020，且注解会横穿 XML 文档注释
与不变量说明）；② DTO 放 `KFL.Core`、靠「无注解契约」（MessagePack 的 contractless resolver）落地
——线上形状会变成「按成员顺序的隐式契约」，C# 侧一改成员次序就静默改变存档格式，且 `Core` 仍需
为序列化而调整成员形状；③ 不设 DTO、为每个 Core 类型写自定义 formatter——每格式每类型一份，
代码量与漂移面都翻倍。三者皆否。

---

## R-03 复原通道：`InternalsVisibleTo` + 两处 `internal` 成员

**Decision**: `KFL.Core` 增 `AssemblyInfo.cs`：

```csharp
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("KFL.Infrastructure")]
```

并**只新增两处** `internal` 成员：

| 类型 | 新增成员 | 为什么必须新增 |
| --- | --- | --- |
| `Family` | `internal OutsiderBookkeeping CaptureOutsiderBookkeeping()` 与 `internal void RestoreOutsiderBookkeeping(OutsiderBookkeeping)`（配套 `internal sealed record OutsiderBookkeeping(IReadOnlyCollection<PersonId> Settled, IReadOnlyCollection<PersonId> MarriageAligned)`，落在 `KFL.Core/Entities/`） | `_settledOutsiders` / `_marriageAlignedOutsiders` 是**私有、无任何公开读出**的簿记，却决定「外来者的辈分还能不能再变」（规格书 §4.4）。不落盘 ⇒ 读回后同一名外来者的辈分再从婚一次就与「从未关闭」不同，直接违反 FR-003 与 FR-017 |
| `FamilyEconomy` | `internal void RestoreFamine(FamineStage stage, int elapsedMonths)` | `Famine` 是只读引用、其 `Stage`/`ElapsedMonths` 都是 `private set`；`Tick()`/`TransitionTo()` 组合无法表达任意「阶段 + 已持续月数」（且 `TransitionTo` 会把计时钉成 1） |

**除这两处外，复原全部经既有成员完成**（这是本抉择的关键佐证——不必为序列化开新公开面）：

| 需要复原的私有状态 | 既有通道 |
| --- | --- |
| `Person.Generation` / `SpouseId` / `FormerSpouseIds` | `Person.SetGeneration` / `SetSpouse` / `AddFormerSpouse`——001 起就是 `internal`（为 `Family` 而设），`InternalsVisibleTo` 后 `Infrastructure` 可直接调用 |
| `GameState.Id` / `CurrentDate` / `Origin` / `Family` / `Economy` | 公开构造函数**已接受任意 `currentDate`**（001 注释即写明「加载存档时须能表达任意年月」） |
| `Family.HeadId` / `HasShiStatus` | `Family.SetHead`（含「MUST 在册」校验）/ 公开 setter |
| `Person.Status` / `Timers` / `EntryTrack` / `Rank` / `Merit` / `Study` / `Health` / `Occupation` / `MonthsInOffice` / `Name` | 公开 setter（含交叉校验） |
| `Person.DegreeHistory` | `Person.AppendDegree`（非降序校验与原始不变量一致） |
| `Treasury.Cash` / `Savings` / `MerchantCapital` / `Loan` / `SavingsRate` / `SavingsRateYear` | 公开构造函数 + `Loan` 公开 setter + 利率两属性的「先年份后利率」赋值次序 |
| `Loan.Principal` / `AccruedInterest` / `MonthsSinceInterest` | `Principal` 公开 setter；`AccruedInterest` 经 `AccrueInterest(原值)`；计时公开 setter |
| `Holdings` 四数量、`Ledger.Entries` | 四个公开 setter；`Ledger.Append`（逐条按序重放） |
| `FamilyEconomy.LivingStandard` / `PendingLivingStandard` / `GrainPriceIndex` / `Treasury` / `Holdings` / `Ledger` | 公开 setter + 公开构造函数 |

**Rationale**: FR-020 只禁止「为序列化给不可变字段新增**公开**写入通道」——`internal` 面加上
`InternalsVisibleTo("KFL.Infrastructure")` 把复原能力限制在**唯一**需要它的程序集里，既不扩大公开
API 面，也不让 `Core` 依赖任何序列化框架（R-02）。两处新增各有**前置的失败证据**：不落
`_settledOutsiders` 会改变**后续推演**（FR-017 可断言），不落 `Famine.ElapsedMonths` 会丢失
「本阶段已持续几月」这一规格书 §5.4 的计时要件（FR-002 可逐字段断言）。

**Alternatives considered**: ① 把 `Family` 的两个 `HashSet` 改成公开只读属性——扩大了公开 API 面，
且它们本是实现细节（对外只暴露「辈分可否再变」的**结果**，不暴露簿记本身）；② 用「重放历史」复原
（先 `AddOutsider` 再按当年次序调用 `Family.Marry`/`SetOutsiderGeneration`）——`Marry` 会**消耗**
「首次家族内成婚」的对齐机会并写簿记，重放等于把历史再演一遍，稍有不一致就会得到与原件不同的簿记；
③ 复原时把 `Famine.ElapsedMonths` 丢掉——直接违反 FR-002/SC-001。

---

## R-04 复原的校验：走同一条不变量路径，失败即拒绝

**Decision**: `Load` 的复原 MUST 逐层走 Core 的既有校验路径（构造函数与既有方法的自校验），并在复原
完成后追加一次**存档级一致性校验**（`SaveStateValidator`）：

| # | 校验 | 不成立时的后果 |
| --- | --- | --- |
| 1 | 成员标识唯一 | 拒绝 |
| 2 | 配偶关系双向一致（`a.SpouseId == b` ⇔ `b.SpouseId == a`） | 拒绝 |
| 3 | `FormerSpouseIds` 每一项都指向本家族成员、不含自身、不含当前配偶、无重复 | 拒绝 |
| 4 | `HeadId` 为 `null` 或指向**在册**成员 | 拒绝 |
| 5 | 每个计时的非空都对应其状态位为真（沿 `Person.Timers`/`Status` 的既有交叉校验） | 拒绝 |
| 6 | `EntryTrack` 非空 ⇒ `AwaitingPost` 为真 | 拒绝 |
| 7 | 饥馑：「阶段为零 ⇔ 计时为零」，且阶段非零时计时 ≥ 1 | 拒绝 |
| 8 | 头记录的 `SaveId` 与主体的 `GameState.Id` 相等 | 拒绝 |
| 9 | 账本每一条资金类条目金额非零（沿 `LedgerEntry` 的既有校验） | 拒绝 |
| 10 | `appliedExam` 合法值（`null` ∨ ∈ { `JieShi`, `ShengShi` }）+ 两个考试失败计数 `>= 0`（005 追加） | 拒绝 |

任一不成立 ⇒ `SaveFileException(Kind = InvalidStructure)`，且 **MUST NOT 返回半成品状态、MUST NOT
修改原文件、MUST NOT 污染调用方已有的内存状态**（FR-014；`Load` 只**新建**一个 `GameState`，不改动
任何调用方对象）。

**Rationale**: 校验的**唯一真源**是 `KFL.Core` 自身的不变量——在存档层复制一份校验规则会让两份规则
各自演化（章程原则 VI 的同款病灶）。第 8 条不是「第二真源」而是**同一份**文件内的自一致性检查
（头是主体标识的 O(1) 探针副本，写入时同源，读出时比对）。

**Alternatives considered**: 在存档层自带一整套字段级校验（与 Core 并行维护两份规则）——否；
`Load` 失败时返回 `null` 或默认状态——被 FR-014 明文禁止（会让玩家以为读档成功）。

---

## R-05 文件布局：「手写头记录 + 主体」

**Decision**: 两种格式共用**同一形状**：`[头记录][主体]`。

| 部分 | 成员 | 语义 |
| --- | --- | --- |
| 头记录 | `FormatVersion`（int）、`PayloadLength`（int）、`SaveId`（Guid） | 版本闸门、截断判据、标识探针——三者都**只需读头** |
| 主体 | `SaveStateDto` 的完整编码 | 全部推演状态（规格书 §14、§12.3） |

- **二进制（`.dat`）**：头 = MessagePack `fixarray(3)`，`SaveId` 以字符串写入（36 字符）；随后**直接**
  拼接主体的 MessagePack 字节流。
- **JSON（`.json`）**：头 = `{"FormatVersion":2,"PayloadLength":N,"SaveId":"…"}`，随后是
  `"State":` + 主体 JSON。

写入次序固定为「先算出主体字节 → 再写头（此时 `PayloadLength` 已知）→ 拼接」，**不需两趟写盘**。

**Rationale**: 只读头带来三项能力（缺一不可）：① `FindBySaveId` 的 UUID 探针不必读上百 KB 的主体
（R-18、Assumptions 的重复检测）；② 版本闸门能在解析主体**之前**拒绝（FR-015）；③ `PayloadLength`
给出「**截断** vs **结构不符**」的**确定**判据——实测：`文件剩余字节 < 声明的 PayloadLength` 即截断。
若把头并入单个大 DTO，这三项都要先解析整个主体。

**Alternatives considered**: ① 单 DTO（头即主体的前几个成员）——无法只读头，且 `PayloadLength`
要写进自身就成了「先知道长度才能写长度」的两趟；② 全文件校验和/摘要——能一并覆盖截断与手改，
但会让 §14 明示「可读、可手改」的 JSON 变成**改一个字就拒读**，还引入规格书未要求的失败模式；
③ 在尾部记录长度或结束标记——截断恰好会**截掉**尾部标记，判据失效。

---

## R-06 版本闸门

**Decision**: `SaveFileFormat.Version = 2`（常量住在 `KFL.Infrastructure/Save/SaveFileFormat.cs`）。
读取时 `FormatVersion != 2` 一律 `SaveFileException(Kind = UnsupportedVersion)`：
`> 2` = 由更新版本写出（FR-015 的正面用例）；`< 2` = 无迁移路径（Out of Scope 明示不实现迁移）。

**Rationale**: 本常量是**文件格式版本**，不是游戏规则数值，故不属 `KFL.Rules/Config/`。
写入时头部先写版本，读取时在任何字段级解析之前先比对，使「更新版本写出的存档」**不可能**被静默
误读成「字段缺失但看起来正常」（Edge Case 与 FR-015 的共同要求）。

**Alternatives considered**: 不设版本字段、靠「必填字段缺失」兜底——更新版本**新增**字段时字段并不
缺失，会静默通过；且无法表达「我读得懂 1，读不懂 2」。否。

---

## R-07 失败分类与判据

**Decision**: `SaveFileException.Kind` 取五个值，判据与次序如下（**先分类、后解析**）：

| 次序 | 判据 | Kind |
| --- | --- | --- |
| 1 | 目标文件不存在 | `Missing` |
| 2 | 头记录可读，且「文件剩余字节 < 头声明的 `PayloadLength`」 | `Truncated` |
| 3 | 头记录本身读不出（字节损坏、魔数/形状不符） | `Malformed` |
| 4 | 头可读且长度足够，但 `FormatVersion != 2` | `UnsupportedVersion` |
| 5 | 文本结构可解析（JSON 的 `JsonDocument.Parse` 成功 / MessagePack 的数组头成功），但**缺本版本必需成员**或**字段值违反 R-04 的校验** | `InvalidStructure` |
| 6 | 其余一切解析失败（字节损坏、类型不符） | `Malformed` |

**JSON 的「结构不符」为何能与「字节损坏」区分**：R-08 的 DTO 每个成员都标 `[JsonRequired]`，读取分
**两段**——① `JsonDocument.Parse`（只看文本是否合法）→ ② 把 `State` 元素映射到 DTO（此时才判
「必需成员是否齐备」）。第 ① 段失败 ⇒ 文本层面损坏；第 ② 段失败 ⇒ 结构可解析但字段缺失 ⇒
`InvalidStructure`。实测：删掉一个 `[JsonRequired]` 成员后，`JsonSerializer` 抛出
`JsonException: ... was missing required properties including: 'diff'`。

**Rationale**: SC-007 要求「缺失 / 截断 / 结构不符 / 版本不符**四类各自**产生可识别的失败信号」，
上表给出四个**互不相同**的 Kind；`Malformed` 是规格书未列举的第五类（字节损坏）的兜底，多出来的
区分度不会削弱四类之间的可识别性。若截断恰好切进头记录本身（头都读不出），只能落到 `Malformed`
——两者都仍是「可识别的方式失败」，MUST NOT 返回默认状态或部分状态。

**Alternatives considered**: 只抛一个笼统的「读档失败」——无法逐类断言 SC-007；按异常消息文本判类
（如匹配 `"unexpected end"`）——依赖框架的消息文案，升版即碎。

---

## R-08 JSON 编码口径（可读性优先）

**Decision**: `JsonSerializerOptions` 固定为
`WriteIndented = true` + `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`
+ `Converters = { new JsonStringEnumConverter() }`，且 DTO 的**每个成员**都显式标注
`[JsonPropertyName("…")]`（ASCII、短、稳定）。

**Rationale**: 四项都已实测：① 缩进 ⇒ 可读；② `UnsafeRelaxedJsonEscaping` ⇒ 中文与「」按原样落盘
（默认会写成 `\u5B8B\u6587`，本仓库的产品是中文游戏，转义会让「可读、便于排查」落空——它只是
**转义**取舍，不等于放宽安全，见下）；③ `JsonStringEnumConverter` ⇒ 枚举写**名字**（`"Hard"`）
而不是 `1`，手改与排查都可读，且非法枚举名会明确失败；④ 显式 `[JsonPropertyName]` 把**线上键名**
钉死——否则 C# 侧改个属性名就会静默改掉存档格式（本仓库的存档要长期可读）。

**Alternatives considered**: ① 依赖 `PropertyNamingPolicy = CamelCase` 自动派生键名——属性改名即
改格式，隐患同上；② 保持默认转义——中文存档变机器码，US3 的「可读、可手改、便于排查」形同虚设；
③ 允许注释与尾随逗号（`JsonCommentHandling`/`AllowTrailingCommas`）——会放宽「结构合法」的判据，
让 R-07 的分类模糊，且规格书无此要求。

---

## R-09 二进制读选项与安全

**Decision**: MessagePack 读写一律经 `MessagePackSerializerOptions`：
`MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData)`，压缩恒为
`None`（FR-021 的「不压缩」）。

**Rationale**: 存档文件是**玩家可编辑/可损坏的外部输入**（FR-014/FR-015 的全部用例都以此为前提），
读入不受信任数据 MUST NOT 让恶意构造的哈希碰撞或超深嵌套拖垮进程；`UntrustedData` 正是为该场景
提供的既有选项（已实测可用）。`None` 直接落实 FR-021 与 §14 的「单文件全量、不分块、不压缩」。

**Alternatives considered**: 默认（`TrustedData`）——对玩家可改的文件不设防；启用压缩——违反
FR-021；自写 formatter 逐字节读写——代码量与漂移面远超收益（且 R-05 只需要头记录手写）。

---

## R-10 金额精度：原值原样、逐位断言

**Decision**: DTO 的金额成员一律是 `decimal`（以文为单位），MUST NOT 经 `double`、MUST NOT 量化、
MUST NOT 施加任何统一舍入；对应的测试断言 MUST 用 `decimal.GetBits` 逐位比较（或「原值 == 读回值
**且** scale 相同」），MUST NOT 只用 `==`。

**Rationale**: FR-016 要求「按原值持久化」；而 `decimal` 的 `==` **忽略 scale**（`1.0m == 1.00m` 为真），
只用 `==` 无法证明「原值」没被量化。实测两种格式都能保住 scale：`1.000m` 往返后
`decimal.GetBits(x)[3]` 仍为 `196608`（= 3 位小数）。这一条把 002 登记的 1 ulp / 精度容差原封不动
地保留下来（FR-016 的第二句）。

**Alternatives considered**: 统一量化到「文」的整数（`long`）——精度更高但会改变 002 的既有口径
（002 契约四 §4 不变量 1 的 1 ulp 取舍），被 FR-016 明文禁止；把金额写成字符串以便人读——JSON 里
`decimal` 本来就是可读数字，加字符串反而破坏 R-08 的手改体验。

---

## R-11 `CurrentDate` 的线上形态

**Decision**: 存 `(CurrentYear, CurrentMonth)` 两整数，复原时经**公开构造函数**
`new GameState(id, new GameDate(year, month), …)` 直接落地；MUST NOT 用 `AdvanceMonth()` 循环逼近。

**Rationale**: `GameState` 的公开构造函数**本来就接受任意 `currentDate`**（001 的注释即写明「本类型
不强制，加载存档时须能表达任意年月」），因此这条复原**不需要任何新通道**。循环逼近既做不到
（`AdvanceMonth` 只能前进、且对负年份无意义），也是 O(月数) 的无谓开销。月份 1~12 由 `GameDate`
自校验（越界 ⇒ 沿 R-04 落到 `InvalidStructure`），从而覆盖 Edge Case 的「任意年月（含前史纪年）」。

**Alternatives considered**: 存「起始年月 + 已推进月数」——新增第二真源，且与 §3「1 回合 = 1 游戏月」
的推导关系互为冗余；存 `DateTime`——把架空纪年硬塞进公历（负年份与 `0` 年不可表达）。否。

---

## R-12 成员的复原次序

**Decision**: 按 DTO 中 `Members` 的**原始加入顺序**逐个注册：无父母者经 `Family.AddOutsider`，
有父母者经 `Family.AddChild`；全部成员注册完毕后，再统一写配偶、前任配偶与辈分簿记（R-03）。

**Rationale**: ① 原始状态必然满足「父母先于子女加入」（`AddChild` 要求父母已在册），故按序重放在
**同一条**校验路径上必然成功——若失败，说明文件本身不自洽（R-04 的 `InvalidStructure`）；
② `AddOutsider` 对辈分**无约束**（只要求无父母参照），故经它复原任何无父母成员（原开局成员、娶入
配偶、买来的旁系）都合法，不需要区分「当初走的是 `AddFoundingMember` 还是 `AddOutsider`」——
这两个入口在**状态**上没有任何可观察差异（都只做 `Register`）。

**配偶 MUST NOT 经 `Family.Marry` 复原**：`Marry` 会消耗外来者「首次家族内成婚」的对齐机会并把
两个标识写进簿记（规格书 §4.4）——复原要复现的是**当时**的簿记，而不是把历史再演一遍（R-03 的
备选 ② 已否）。故配偶/前任/簿记一律在成员注册完成后经既有 `internal` 成员写入。

**Alternatives considered**: 先按「拓扑序」排（父母 → 子女 → 配偶）再注册——与「原始加入顺序」
在合法状态上等价，但需要额外重建拓扑关系且会掩盖文件不自洽的情形；逐字段直接改私有字段——
绕过了全部校验，等于把 R-04 作废。

---

## R-13 存档名的口径

**Decision**: `SaveName`（`KFL.Infrastructure/Save/SaveName.cs`）是名称校验的**唯一真源**：

| 输入 | 处置 | 依据 |
| --- | --- | --- |
| 中文、`-`、内部空格 | **放行** | FR-007（裁决 Q3：`-` 是 §14 原文的笔误，`*` 才是非法字符） |
| `\` `/` `:` `*` `?` `"` `<` `>` `\|`（9 个） | **拒绝** | FR-007（裁决 Q3） |
| 空串、纯空白 | **拒绝** | Edge Case（MUST NOT 落到文件系统再报错） |
| 长度 > **255** 字符 | **拒绝** | Edge Case「超长」；255 = **NTFS 文件名分量上限**（文件系统事实，不是平衡数值，故不进 `KFL.Rules/Config/`） |
| Windows 保留设备名 `CON`/`PRN`/`AUX`/`NUL`/`COM1~9`/`LPT1~9`（大小写不敏感，**带扩展名与否都算**） | **拒绝** | Edge Case |
| 以 `.` 结尾、首尾空白 | **拒绝** | Edge Case（Windows 会静默截断，MUST NOT 依赖） |
| ASCII 控制字符 `0x00~0x1F` | **拒绝** | Edge Case；同属「Windows 会静默处置」的一类，是 FR-007 那 9 个的**同源**补充（不新增任何数值） |

一律取**拒绝**而非静默规范化：拒绝没有「同名却指向不同文件」的歧义，且 SC-005 允许「拒绝率 100%」
或「100% 规范化」二选一。

**Rationale**: 名称是**文件名**层面的量，属存储侧，故住在 `Infrastructure`（R-01）；把它放进
`KFL.Rules/Config` 会让「文件名合法性」变成游戏规则（规格书未如此定义）。

**Alternatives considered**: 静默规范化（去空白、替换非法字符、补 `_`）——玩家看到的档名与实际文件
名不一致，且「改名」语义会变得依赖实现细节；只拦 FR-007 的 9 个、其余交给文件系统——违反
Edge Case 的「MUST NOT 依赖文件系统的静默行为」。

---

## R-14 原子写：临时文件 + 同卷替换

**Decision**: `AtomicFileWriter` 是**唯一**写盘路径：

1. 在**目标同目录**建临时文件（`<target>.tmp`，同卷 ⇒ 替换是原子的），写入全部字节；
2. `Flush(flushToDisk: true)`；
3. `File.Move(tmp, target, overwrite: true)` 一步替换（目标不存在时同一条调用即首次落盘）。

失败路径 MUST 尽力删除临时文件，且 MUST NOT 触碰目标文件。**备份也走同一条路径**（先写临时再替换）。

**Rationale**: FR-013 要求「任何中断 MUST NOT 在目标路径留下半截存档、也 MUST NOT 损坏原有存档」；
SC-006 把判据写成「目标路径上的文件要么是写入前的完整存档、要么是写入后的完整存档」。备份走同一条
路径是 Edge Case「备份被中断 MUST NOT 影响当前档、且降级 MUST 以可识别的方式暴露」的直接落实——
半截的**备份**同样是不可接受的（它会被当成一份可读的档）。

**Alternatives considered**: ① `File.Replace`——要求目标已存在，得为「首次写」另开分支，`File.Move`
一条调用覆盖两种情形；② 先删目标再写——中断即丢档，正是 FR-013 要禁的形态；③ 内存里拼好再一次性
`File.WriteAllBytes`——`WriteAllBytes` 本身不是原子的，写到一半中断仍会留下半截文件。

---

## R-15 目录与路径

**Decision**: 默认存档根 = `AppContext.BaseDirectory` + `saves`（FR-006；`AppContext` 不是 G-07 的
禁用 token）；备份目录 = `<根>/backups`（FR-011）。根目录经 `SaveOptions.RootDirectory` 配置
（`null` ⇒ 默认）。目录不存在时**自动创建**；创建失败或无写权限 ⇒ 抛出可识别的
`SaveFileException(Kind = Malformed)` 之外的一类失败语义（`SaveAccessException`，见契约九）且
MUST NOT 静默吞（Edge Case）。

**Rationale**: 章程「技术栈与工程约束」要求「存档、日志等运行时路径 MUST 可配置；默认取可执行文件
相对路径 `saves/`」——`AppContext.BaseDirectory` 正是「可执行文件旁」，且它**不读墙钟、不读环境
变量以外的外部状态**。所有文件系统调用收敛在 `AtomicFileWriter` / `SaveCatalog` / `SaveName`
三个类型内，逐行 `// arch-guard:allow`（FR-019、SC-008）。

**Alternatives considered**: `Environment.CurrentDirectory`——随启动方式漂移，不是「可执行文件旁」；
`Environment.GetFolderPath(SpecialFolder.ApplicationData)`——与规格书 §14 明示的 `saves/` 冲突。

---

## R-16 回合数与节奏数值

**Decision**: `KFL.Rules/Config/SaveCadencePolicy`：

| 成员 | 值 | 依据 |
| --- | --- | --- |
| `EpochYear` / `EpochMonth` | `1` / `1` | 规格书 §3「从 1 年 1 月开始推演」 |
| `BackupIntervalTurns` | `3` | 规格书 §14「每 3 回合写 `backups/存档名.dat`」 |
| `CompletedTurnsAt(GameDate current)` | `(current.Year − EpochYear) × 12 + (current.Month − EpochMonth)` | 派生量：1 回合 = 1 游戏月（§3） |
| `ShouldBackup(int turns)` | `turns > 0 && turns % BackupIntervalTurns == 0` | §14；`turns > 0` 排除「Episode 起点」被算成 3 的倍数 |

**回合数从 `CurrentDate` 派生，MUST NOT 新增持久化计数器**：`MonthlySettlementEngine` 的第 ⑦ 步已
把时间推进一个月，故「一次成功结算后的 `CurrentDate`」恰好等于「已结算回合数」的位移。新增计数器会
给 `GameState` 加一条需要与时间**同步维护**的第二真源（且属跨特性形状变更），而 §12.3 的统计学
口径（「一律由账本聚合、不另存汇总值」）已经确立了「可派生的量不落字段」的取向。

**自动保存的开关 = 是否注入 `ISaveService`**（装配期决定，不新增持久化设置项）：US2 AS4 的
「关闭自动保存」即不注入该服务；规格书 §11 的设置面板属界面轨 U5，本特性 MUST NOT 预置设置存储。

**Rationale**: 章程要求规则数值集中在 `KFL.Rules/Config/`；`3` 与 `(1, 1)` 都是规格书数值/事实，
故它们的唯一出处只能在这里（存档服务与 `Infrastructure` 一律**不得**出现 `3`）。

**Alternatives considered**: ① 由调用方把回合数当参数传给存档服务——`3` 与「回合数的定义」就会被
推到装配点甚至界面轨，本特性内无法断言 SC-003；② 存一个 `TurnCount` 字段——违反「不另存可派生的
量」并触发跨特性形状变更；③ 用 `CurrentDate` 从某个「存档创建年月」起算——`GameState` 没有创建年月
字段，引入它同样是新增状态。

---

## R-17 自动保存的落点：新增 `TurnRunner`，不动结算引擎

**Decision**: 新增 `KFL.Rules/Save/TurnRunner`：

| 步 | 动作 | 失败时的后果 |
| --- | --- | --- |
| ① | `new MonthlySettlementEngine(random, new GameStateClock(state)).Settle(state)` | 抛出 ⇒ **MUST NOT 保存**（FR-010 的第二句） |
| ② | 若未注入 `ISaveService` ⇒ 到此为止（自动保存关闭） | — |
| ③ | `saveService.Save(state, slot, overwrite: true)`（**覆盖写入当前档**，FR-010） | 抛出 ⇒ **MUST NOT 备份**（FR-011 的「自动保存完成之后」） |
| ④ | `SaveCadencePolicy.CompletedTurnsAt(state.CurrentDate)`，命中 `ShouldBackup` ⇒ `saveService.Backup(slot)`（FR-011） | 抛出 ⇒ 传播（备份失败 MUST NOT 影响已写好的当前档） |
| — | `SaveOnExit(GameState)`：**复用同一个保存入口**（FR-012），MUST NOT 另写一套 | — |

**Rationale**: 契约三 §1 的「**MUST NOT 存在**的步骤」清单**明列「存档写入」**，`MonthlySettlementEngine`
的类文档亦自述「唯一编排入口、每月一次」——把落盘塞进它等于改 002 的七步序列（跨特性形状变更，
需先回写契约三）。`TurnRunner` 把「结算」与「落盘」显式分成两步，「写入 MUST NOT 发生在结算中途」
因此是**结构性**成立的（不是靠约定）。每回合在 `Rules` 内新建一次结算引擎是既有惯例：全部既有测试
都写作 `new MonthlySettlementEngine(random, new GameStateClock(state))`，因为 `GameStateClock` 绑定
到具体的 `GameState`。

**Alternatives considered**: ① 给 `MonthlySettlementEngine` 注入可空的 `ISaveService`——改 002 的
步骤序列与类契约（且会让「结算」这个纯规则动作带上 I/O）；② 在 `Rules/Settlement` 下再开一个
「存档步」——仍是对契约三的改动；③ 交给未来的游戏循环（U1）自己「结算后保存」——则 FR-010/FR-011
在本特性内**没有任何可断言的对象**，US2 的三条 AS 会全部落到界面轨。

---

## R-18 保存、覆盖与唯一性

**Decision**:

| 情形 | 处置 |
| --- | --- |
| 目标名已有存档（**任一格式**）而 `overwrite == false` | 拒绝：`SaveNameConflictException`（FR-008） |
| `overwrite == true`，同名存档存在 | 写出新文件；随后移除**同名**的异格式文件（「该名字归调用方」） |
| 同根下**另一个文件名**携带同一 UUID | 拒绝：`SaveIdentityConflictException`（Assumptions 的 001 R-03 收口口径） |
| 同**名**但异格式、UUID 相同（格式切换） | 先成功写出新格式文件，**再**删除旧格式文件；新文件写出失败 ⇒ MUST NOT 删除旧文件 |
| `Exists(slot)` | 回答「该名字在该目录下是否已有存档」，**与格式无关** |

UUID 探针（`FindBySaveId`）只读**头记录**（R-05），MUST NOT 读主体。

**Rationale**: 规格书 §14 只说「同名弹窗确认是否覆盖」，未定义「覆盖一个**别的**存档」时的语义；
Assumptions 已裁决「同一 UUID 在同一目录下至多只能有一个存档文件」与「UUID 重复 ⇒ 检测并拒绝」。
把 `overwrite` 定义成「该名字归调用方」使格式切换与「同名另存」两条路径都只有一个处置，且
「同一 UUID 两个文件」这一**成就归属**上的危险状态在任何路径下都不可达。

**Alternatives considered**: 允许同 UUID 并存（靠调用方自律）——成就会按 UUID 记到两份档上；
`Exists` 只按格式回答——会出现「新建 `宋家.json` 时看不到已有的 `宋家.dat`」，把歧义留给玩家。

---

## R-19 备份口径

**Decision**: `Backup(slot)` = 把**当前档的字节**逐字节复制为 `<根>/backups/<存档名>.<与当前档相同的扩展名>`
（覆盖式，每档恒 1 份），经 `AtomicFileWriter`（R-14）。备份目录不存在即创建。
「回退」= 把备份复制回主文件（界面轨 U1 的操作）。

**Rationale**: 这是 §14 的 2026-10-11 裁决 Q1 的直接实现：落点 = 可配置根下的 `backups/`、文件名 =
存档名 + **相同扩展名**、时点 = 该回合自动保存**完成之后**、覆盖式每档一份。同格式的备份可直接被
`Load` 读回（§14 裁决原文的「回退 = 把备份复制回主文件」）；本特性以测试断言
「把备份复制回主文件后 `Load` 成功且内容一致」。

**Alternatives considered**: 备份到另一个目录（如 `%TEMP%`）——与 Q1 裁决冲突；按时间戳留多份——
§14 明示「仅保留最新」；备份存「覆盖前的旧档」——Q1 已裁决取「写入后的检查点」。

---

## R-20 仓库忽略运行时产物

**Decision**: `.gitignore` 增一行 `saves/`（FR-023）。

**Rationale**: 默认存档根就在**可执行文件旁**（`bin/.../saves/`），而 `bin/` 已被忽略；但开发期若
从仓库根运行或把根配置成仓库内，产物就会落进工作区——SC-009 要求「一次完整门禁跑完后仓库的
存档相关变更数 = 0」。测试一律使用**临时目录**（FR-022），不依赖这一行来保证洁净；它防的是
**手工试跑**留下的产物。

**Alternatives considered**: 把 `backups/`、`*.dat`、`*.json` 一并加入忽略——`*.json`/`*.dat` 会被
误伤（本仓库已有非存档的 JSON/数据文件与将来的成就文件），故只忽略 `saves/` 这一个目录。

---

## R-21 测试组织与行级豁免的收敛

**Decision**:

| 测试落点 | 内容 | 是否触碰文件系统 |
| --- | --- | --- |
| `tests/KFL.Tests/Infrastructure/SaveTestSandbox.cs` | **唯一**的临时目录/文件操作辅助类（建临时根、读字节、列文件、复制备份回主文件、清理） | 是——**每个文件系统调用一行**，逐行 `// arch-guard:allow <理由>` |
| `tests/KFL.Tests/Infrastructure/SaveServiceTests.cs` | US1/US3：两种格式的存读等价、命名、覆盖、UUID 唯一性、格式切换（用 `SaveTestSandbox`） | 否（经沙箱） |
| `tests/KFL.Tests/Infrastructure/SaveFailureTests.cs` | US3 AS7 / SC-007：缺失、截断、结构不符、版本不符、字节损坏五类失败 | 否（经沙箱） |
| `tests/KFL.Tests/Infrastructure/AtomicWriteTests.cs` | SC-006：写入中途失败注入 + 备份中断 | 否（经沙箱） |
| `tests/KFL.Tests/Infrastructure/SaveFormatContractTests.cs` | 契约十的线上形状（头三成员、键名集合、枚举写名、两格式信息集等价、100 年体量） | 否（经沙箱） |
| `tests/KFL.Tests/Rules/SaveCadenceTests.cs` | US2：12 回合的保存/备份节奏、开关、退出保存复用入口 | 否（`RecordingSaveService` 内存替身） |

**MUST NOT** 新建一个不在 G-07 扫描根内的测试目录来「省掉注释」——那会让新代码悄悄脱离守卫。

**Rationale**: G-07 与 SC-008 的扫描范围含 `tests/KFL.Tests/Infrastructure/` 与 `Rules/`
（001 契约一 §2.1、§2 的 G-07 行），而存档测试**本质**需要文件系统；把调用收敛到一个辅助类里，
豁免行数就能被**逐一列举**（SC-008 的原文要求）。节奏判定不需要任何文件系统（`ISaveService` 是
接口），故它用内存替身，落 `Rules/`。

**Alternatives considered**: ① 新增 `tests/KFL.Tests/Save/`（不在扫描根内）——等于给新代码开一个
守卫盲区；② 把文件系统调用散落在各测试类里——豁免行数无法集中核对；③ 用 `dotnet` 的
`TestContext`/内存文件系统——.NET 无内置内存文件系统，引第三方包违反「复杂度 MUST 被论证」。

---

## R-22 格式契约的验证物

**Decision**: `SaveFormatContractTests` 断言：① 头记录恰有三个成员且次序为
`FormatVersion, PayloadLength, SaveId`；② DTO 的**键名集合**与契约十 §4 的键表逐项一致（多一个、
少一个都失败）；③ 枚举落成**名字**（JSON）与**整数**（二进制），且非法枚举名/越界整数被拒；
④ 同一状态以两种格式存读得到的**信息集等价**（逐字段断言，SC-001）；⑤ 100 年（1200 回合）存档的
账本条目数**丢失数 = 0**（SC-010）。

**Rationale**: FR-004/FR-005 要求两格式承载**同一信息集**；「同一信息集」若不落成可执行断言，
就只能靠人眼比对两份映射代码。键表断言同时钉住了 R-08 的「线上键名不改」这一承诺。

**Alternatives considered**: 只做往返测试（存 → 读 → 比）——往返测试**无法**发现「两种格式都漏掉
同一个字段」这类系统性缺失；键表/字段类别的显式断言（SC-001 要求 ≥14 类）才能。

---

## R-23 不引入系统时钟

**Decision**: 本特性**不落盘任何写入时间戳**，因此**不引入**墙钟接缝（无 `ISystemClock`、无
`DateTime.Now`）。001 research R-04 与未决事项表中「系统时钟抽象：阶段④（存档写入时间戳）首次需要
时引入」一行，按 §17②「对玩家最直观 / 无该需求」复核后**结清为「不需要」**并回写。

**Rationale**: 规格书 §14 **未**要求存档时间戳；FR-018 明文要求存档实现的时点「完全由回合数与显式
入参决定」。章程「复杂度 MUST 被论证」禁止为假想需求预置结构；若界面轨将来要显示「最近修改时间」，
MUST 走注入的时钟接缝，MUST NOT 在存档实现里读墙钟。

**Alternatives considered**: 顺手把 `File.GetLastWriteTime` 写进存档——`DateTime.Now`/`DateTime.Today`
会被 G-07 拦，且时间戳会让**同一状态的两份存档字节不同**，「同状态 → 同字节」这一最有用的确定性
断言随之失效。

---

## R-24 依赖：MessagePack `2.5.305`（唯一新增包）

**Decision**: `KFL.Infrastructure.csproj` 增 `<PackageReference Include="MessagePack" Version="2.5.305" />`
（仅此一个包）。JSON 走 BCL 的 `System.Text.Json`（**无**新增依赖）。

**已实测的事实**（本机缓存 `/root/.nuget/packages` 内有 `messagepack 2.5.305` 与 `2.5.192`，
资产含 `net6.0`，兼容 `net10.0`）：

| 实测项 | 结果 |
| --- | --- |
| 探针工程（`net10.0` + `TreatWarningsAsErrors`）引用 `MessagePack 2.5.305` | **0 警告 0 错误**（分析器/源生成器在 .NET 10 SDK 上正常加载，不产生 CS8032） |
| `[MessagePackObject]` + 连续 `[Key(0..n)]` 的线上形状 | **array 格式**（首字节 `0x94` = `fixarray(4)`）⇒ **位置式**：成员缺失即结构错误 |
| `decimal` 往返（`1.000m`） | `decimal.GetBits(x)[3]` 前后相同 ⇒ **scale 逐位保留** |
| `MessagePackSecurity.UntrustedData` | 可用 |
| 手写头（`MessagePackWriter.WriteArrayHeader(3)` + `Write(int)` + `Write(string)`）与 `MessagePackReader.Consumed` 切片反序列化 | 可用（实测 `headerLen == offset == 41`，切片后主体还原正确） |
| 截断（头完好、主体不足） | `剩余字节(4) < 声明长度(12)` ⇒ **可确定为截断** |
| JSON `[JsonRequired]` 缺成员 | `JsonException: ... was missing required properties including: 'diff'` |
| JSON `JsonStringEnumConverter` / 非法枚举名 | `"Hard"` 可读 / 抛 `JsonException` |
| JSON `decimal` 往返 | scale 逐位保留（同二进制） |

**Rationale**: FR-024 要求新增依赖「支持 `net10.0` 并在本规格或计划文档中说明必要性」；章程的依赖
基线**本就包含** MessagePack-CSharp（「二进制存档」），规格书 §1/§14 亦把它指定为二进制存档的实现。
取 `2.5.305`（缓存内较新且已实测）；MUST NOT 引入 `MessagePackAnalyzer`、`MessagePack.ReactiveProperty`
等附加包，也 MUST NOT 引入第三方文件系统抽象。

**Alternatives considered**: ① 二进制也走 `System.Text.Json`（UTF-8 文本）——违反 §14 明文指定的
MessagePack；② 自写二进制格式——规格书已指定实现，自写属「自创」（§17 禁止）；③ 等 MessagePack 3.x
——本机缓存无该版本、离线不可还原，且 2.x 已实测全绿。

---

## R-25 存档名与家族姓氏的解耦

**Decision**: 存档名只存在于**文件名与显示**层面；`Family.Name` 只存在于主体 DTO 的
`family.name` 成员里。改名路径 MUST NOT 触碰任何主体字节（`Rename` 只做文件移动），改家族姓氏
MUST NOT 触碰文件名。

**Rationale**: 规格书 §12.4 与 §14 是两个独立概念；FR-009 与 SC-004 要求「改名前后存档标识与游戏
内容差异 = 0，家族姓氏亦未被改动」。由于 `Rename` 是**纯文件移动**，「内容不变」是**结构性**成立的
（不是靠断言维持）——测试只需断言「移动前后文件字节逐字节相同」与「读回后 `Family.Name` 不变」。

**Alternatives considered**: 改档名时顺带把家族姓氏改成档名（把两者当同一个概念）——被 §12.4/§14
明文排除，且会让「改档名」意外改变新生儿姓氏（§12.4：姓氏只影响此后新生儿）。

---

## 澄清会话追加（2026-10-11，Q4~Q8）

`$speckit-clarify` 复扫在本文件与契约之外又发现五处**未定义的口径**，用户全部取「A」档，结论
已回写 [spec.md](./spec.md) 的 FR / Edge Cases / SC 与 `Assumptions`：

| 编号 | 缺口 | 裁决对既有抉择的影响 |
| --- | --- | --- |
| **Q4** | 「当前档」从未定义来源；R-17 的 `Advance(GameState state)` 却在步骤 ③ 使用 `slot`（签名与步骤不自洽） | **修正 R-17**：`slot` = **装配期注入的当前槽位**（`TurnRunner` 构造参数或持有它的存档服务），`Advance`/`SaveOnExit` 不逐次接收；槽位 MUST NOT 进 `GameState`（否则就是 R-16 否决过的第二真源） |
| **Q5** | R-19 只定义「覆盖式每档一份」，未覆盖改名/换格式后旧名或旧扩展名备份的处置 | **扩写 R-18 的格式切换、R-19 的备份口径、R-25 的改名路径**：改名时备份一并改名；格式切换时新当前档写成功后再移除旧格式备份；「`backups/` 下同一存档标识恒 1 份且与主档同名同扩展名」在操作完成时即成立 |
| **Q6** | R-17 只写「③ 抛出 ⇒ 不备份」，未定义**已结算的内存状态**如何处置 | **补充 R-17**：保留已推进的内存状态并把失败向上传播；MUST NOT 回滚（回滚需结算前快照 = FR-020 禁止的第二份真源）、MUST NOT 静默吞；「已推进未落盘」为允许的中间态，下次保存合并写入 |
| **Q7** | R-13/R-18 的名称口径未定义**大小写敏感性** | **补充 R-13/R-18**：判重（`Exists`/覆盖冲突/改名目标）**大小写不敏感**——Windows 文件系统本就不敏感，敏感判重会造出「系统说没有、随后静默覆盖」的空洞；不加 Unicode NFC 规范化（避免第二套比较口径） |
| **Q8** | spec 的 Edge Case（任意年月含 0 与负数）与 FR-011（每 3 回合）在负年份区间冲突；R-16 用 `turns > 0` 回避而非**界定** | **界定 R-16 的适用面**（用户裁定）：推演进程的 `CurrentDate` **恒自 1 年 1 月起算**，0/负数年份只出现在角色的**出生年月**等成员字段；回合数恒为正，「3 的倍数」无负数区间歧义；Edge Case 的「任意年月」收窄为成员年月字段。**不改任何数值** |

> 本表属**声明与验证义务**类回写（这五条裁决本身未增删 `KFL.Core` 字段、未改任何**公开**签名；
> 本特性整体另有 6 处 `internal` 面的放宽 / 新增，见本文件开篇的前置事实与
> [spec.md](./spec.md) 的「本特性对上游 `KFL.Core` 的形状变更」条目）；`tasks.md` 生成时
> MUST 登记上述同步范围（AGENTS.md 的「铁律」）。

---

## 未决事项

无。规格书 §14 的三项裁决（Q1/Q2/Q3）已在 `$speckit-specify` 阶段由用户裁决并回写规格书；
`$speckit-clarify` 复扫的五项口径（Q4~Q8）亦已裁决并回写 spec 与活工件。本文件另 25 项抉择
全部由规格书条款、章程与实测证据推出，**不新增、不改动任何影响平衡的数值**（唯一进入代码的
数值仍是规格书 §14 的「每 3 回合」与 §3 的「自 1 年 1 月起」）。
