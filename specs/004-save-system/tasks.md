---

description: "Task list for 存档体系（逻辑轨 ④）"
---

# Tasks: 存档体系

**Feature**: `004-save-system` | **Input**: 设计文档来自 `/specs/004-save-system/`

**Prerequisites**: [plan.md](./plan.md)（必需）、[spec.md](./spec.md)（必需）、
[research.md](./research.md)（R-01~R-25 全部有结论）、[data-model.md](./data-model.md)（§1~§9）、
[contracts/](./contracts/)（[契约九 save-service](./contracts/save-service.md) /
[契约十 save-format](./contracts/save-format.md) /
[契约十一 save-cadence](./contracts/save-cadence.md)）、
[quickstart.md](./quickstart.md)（8 个可运行验收场景）；规格书 §14、§12.3、§3、§4.1、§5.4、§7.5、§8.2

**Tests**: 本特性**包含测试任务且测试是验收物**（不可省略）。依据：spec **FR-022** 明令交付
两种格式的逐字段往返、自动保存与备份节奏与中断安全、命名与非法字符、同名未确认不覆盖、
改名后的标识不变性、路径可配置、缺失与损坏文件的失败形态各点的单测，且测试 MUST 用**临时目录**；
SC-001~SC-010 的验证物本身就是测试；章程原则 IV「单测优先与结果确定性」；
quickstart 的场景 1~8 逐条对应一个测试文件或一次过滤跑。
故本特性**不采用** TDD 先行，而按仓库既有口径「测试与实现在同一批提交」（章程原则 IV）。

**Organization**: 按 user story 分组（US1~US3，优先级 P1~P3），使每个故事可独立实现、独立验证、
独立交付。Foundational 阶段只放**被多个故事共享**的 `KFL.Core` 最小复原面、存储侧值类型与
文件系统收敛点（`AtomicFileWriter`/`SaveName`/`SaveCatalog`）与共享测试夹具/沙箱。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 可并行（不同文件，且不依赖同一阶段内其它未完成任务的产出）
- **[Story]**: 该任务所属 user story（US1 / US2 / US3）；Setup / Foundational / Polish **不带** [Story]
- 每个任务都给出精确文件路径；data-model 与契约的字段约束、键名、失败类别**逐字抄入**任务描述

## Path Conventions

仓库根 `E:\ProjectCsharp\Games\KejuFuShengLu`；源码 `src/`、测试 `tests/`（见 [plan.md](./plan.md)
的「Source Code」）。结构**不变**：仍为六个工程、`.slnx` 唯一、非 UI 三层 `net10.0`、
UI 两层 `net10.0-windows`。新增 **1 个** NuGet 依赖：`MessagePack 2.5.305`（仅 `KFL.Infrastructure`；
JSON 走 BCL 的 `System.Text.Json`，零新依赖）。`KFL.Presentation` 与 `KFL.App` 在本特性
**一行不动**（界面属界面轨 U1/U5）。

> **门禁命令**（本阶段每一次验证都用它们；受限宿主的两个坑见 `AGENTS.md`）：
> ```powershell
> dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false     # 0 警告 0 错误
> dotnet test  KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false
> dotnet sln   KejuFuShengLu.slnx list                      # 恰好六个工程
> Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }   # 期望无输出
> ```
> 第三条不能省：`.slnx` 解析器不认识 `<TestProject>` 且不报错，测试工程可能悄悄掉出解决方案。
> `tests/KFL.Tests/KFL.Tests.csproj` 的 `_MSTestEnableParentProcessQuery` MUST 保持**注释态**提交。

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 开工前的仓库级准备——分支与门禁基线、需求真源的裁决核对、**上游活工件**的回写
（AGENTS.md「铁律」：变更授权在变更源，回写义务在同一提交）。**不含任何生产代码**。

- [X] T001 确认工作分支为 `004-save-system` 并记录四条门禁基线：依次执行 `dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false`、`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false`、`dotnet sln KejuFuShengLu.slnx list`、`Get-ChildItem -Recurse -Filter *.sln | Where-Object { $_.FullName -notmatch '\\\.git\\' }`，把「构建 0 警告 0 错误 / 测试全绿（当前基线 421 通过 0 失败 0 skipped）/ 恰好六个工程 / 无 `.sln`」四条基线值记入提交信息或 `specs/004-save-system/implementation-notes.md`（若创建该文件）——后续每一阶段与它对比
- [X] T002 [P] 在 `.gitignore` 增一行 `saves/`（FR-023；R-20）——**只**忽略这一个目录，MUST NOT 把 `backups/`、`*.dat`、`*.json` 一并加入（会误伤仓库既有的非存档 JSON/数据文件与将来的成就文件，R-20 的否决策略）
- [X] T003 [P] 回写 001 活工件（AGENTS.md「铁律」；spec Assumptions 1/2）：`specs/001-core-skeleton/spec.md` FR-012 段（L157–160）与 `specs/001-core-skeleton/data-model.md` §2.3 的 `Id` 行（L261）、FR 对照表（L313）中「改档名不变性属阶段④、001 内无可验证对象」的措辞 → 标注为**已由 004 交付并验证**，指向 `specs/004-save-system/contracts/save-service.md` §6 与 SC-004；**MUST NOT** 回改 001 的 `tasks.md`、`implementation-notes.md`、既有 `checklists/`（规格书 §16.2 的历史记录口径）
- [X] T004 [P] 回写 001 跨阶段契约（spec Assumptions 3；R-23）：`specs/001-core-skeleton/contracts/injection-seams.md` §5（L80–82）把 `ISaveService` 的「等各自阶段带着真实需求回来定义」改为**已由 004 定义**并指向 `specs/004-save-system/contracts/save-service.md`；§2 末句（L47–48）把「存档时间戳属阶段④」标注为**已由 004 复核为「无该需求」**（本特性不落盘时间戳）
- [X] T005 [P] 结清既有未决事项登记（spec Assumptions 4/5；plan 回写清单 #5~#10）：① `specs/001-core-skeleton/research.md` R-03（L72–74 与未决事项 L383）标注为**已由 004 结清**（`SaveIdentityConflictException` 检测并拒绝同根同 UUID 并存）；② 同文件 R-16（L363–364、L389）标注为**已由 004 结清**（结论 = 单文件全量、不分块、不压缩）；③ 同文件 R-04 未决事项行（L393）与 `specs/001-core-skeleton/plan.md`（L39）的「系统时钟抽象首次需要时引入」标注为**结清为不需要**；④ `specs/002-monthly-settlement-economy/research.md` 未决事项表（L441）标注**已由 004 结清**；⑤ `specs/002-monthly-settlement-economy/contracts/ledger.md` §6（L73–74）与 §7（L78–79）**只补注**（判定句本身 MUST NOT 改写）；⑥ `specs/002-monthly-settlement-economy/data-model.md`（L7）、`plan.md`（L43–44）、`research.md`（L283–285）的「此时增删字段无迁移成本」属**当时事实**：保留原文，只在「含义已改变」处补注一句「004 之后落盘形态已固定为契约十的版本 1，跨版本变更须走 §14 的版本闸门（拒绝而非迁移）」
- [X] T006 [P] 核对需求真源的裁决回写（plan 回写清单 #1，标注为**已完成**）：确认 `科举浮生录规格书.md` §14 末尾的「§17 裁决回写」段已记入 Q1（备份落点与内容时点）、Q2（账本单文件全量、不分块、不压缩）、Q3（过滤字符集取 Windows 实际非法集 `\ / : * ? " < > |`、`-` 放行）；若缺失则补齐；§14 的**原始条目文本**按 §16.2 的历史事实口径**保留不改写**

**Checkpoint**: 门禁基线已知、上游活工件已按「含义已改变处补注」口径回写——可以开始写生产代码。

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 被 US1~US3 **共享**的地基——`KFL.Core` 的两处最小 `internal` 复原面与
`InternalsVisibleTo`、存储侧值类型与异常族、三个文件系统收敛点（`AtomicFileWriter`/`SaveName`/
`SaveCatalog`）、MessagePack 依赖、共享存档夹具与测试沙箱。

**⚠️ CRITICAL**: 本阶段完成前，任何 user story 都 MUST NOT 开工。

- [X] T007 [P] `KFL.Core`：新增 `src/KFL.Core/AssemblyInfo.cs`，内容为 `[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("KFL.Infrastructure")]`（R-03）——这是本特性给 `Core` 开的**唯一**程序集级面；MUST NOT 同时放行任何其它程序集
- [X] T008 [P] `KFL.Core`：复原家族外来者簿记（R-03）——新增 `src/KFL.Core/Entities/OutsiderBookkeeping.cs`：`internal sealed record OutsiderBookkeeping(IReadOnlyCollection<PersonId> Settled, IReadOnlyCollection<PersonId> MarriageAligned)`；在 `src/KFL.Core/Entities/Family.cs` 增 `internal OutsiderBookkeeping CaptureOutsiderBookkeeping()` 与 `internal void RestoreOutsiderBookkeeping(OutsiderBookkeeping bookkeeping)`（读写既有私有簿记 `_settledOutsiders` / `_marriageAlignedOutsiders`；它们是「外来者辈分还能不能再变」的判据，规格书 §4.4）。**除这两处外 MUST NOT 新增任何 `Core` 成员**；`Person.SetGeneration`/`SetSpouse`/`AddFormerSpouse` 已是 `internal`，经 T007 直接复用
- [X] T009 [P] `KFL.Core`：复原饥馑计时（R-03）——在 `src/KFL.Core/Entities/FamilyEconomy.cs` 增 `internal void RestoreFamine(FamineStage stage, int elapsedMonths)`：直接落 `Famine.Stage` 与 `Famine.ElapsedMonths`（二者均为 `private set`），**MUST NOT** 用 `FamineState.Tick()`/`TransitionTo()` 组合逼近（`TransitionTo` 会把计时钉成 1，无法表达任意「阶段 + 已持续月数」）；「阶段为零 ⇔ 计时为零」的校验归 T024 的 `SaveStateValidator` 第 7 条，本方法只落值
- [X] T010 [P] `KFL.Infrastructure`：新增存储侧值类型（契约九 §1）——`src/KFL.Infrastructure/Abstractions/SaveFormat.cs`：`public enum SaveFormat { Binary = 0, Json = 1 }`（**零值 = 默认格式**，FR-005）；`src/KFL.Infrastructure/Abstractions/SaveSlot.cs`：`public readonly record struct SaveSlot(string Name, SaveFormat Format = SaveFormat.Binary)`
- [X] T011 [P] `KFL.Infrastructure`：新增跨层入口 `src/KFL.Infrastructure/Abstractions/ISaveService.cs`（契约九 §1；001 契约二 §5 的「待定义」即此处）——精确形状：`string RootDirectory { get; }`、`bool Exists(SaveSlot slot)`、`SaveSlot? FindBySaveId(Guid saveId)`、`string FilePathOf(SaveSlot slot)`、`void Save(GameState state, SaveSlot slot, bool overwrite)`、`GameState Load(SaveSlot slot)`、`SaveSlot Rename(SaveSlot slot, string newName)`、`void Backup(SaveSlot slot)`；注入 MUST 经构造函数（MUST NOT 静态单例 / `ServiceLocator` / 属性注入）；MUST NOT 引用任何 WPF/Windows 类型
- [X] T012 [P] `KFL.Infrastructure`：新增失败族 `src/KFL.Infrastructure/Save/SaveFileException.cs`（契约九 §2；R-07）——`SaveFileException` 携带 `Kind ∈ { Missing, Truncated, Malformed, UnsupportedVersion, InvalidStructure }`（**五个互不相同的值**，SC-007 的可识别信号不得依赖异常消息文本）；同文件族另三个：`SaveNameConflictException`（同名未确认覆盖）、`SaveIdentityConflictException`（同根下同 UUID 的另一个文件名）、`SaveAccessException`（目录创建失败 / 无写权限）
- [X] T013 [P] `KFL.Infrastructure`：新增 `src/KFL.Infrastructure/Save/SaveFileHeader.cs`——`public readonly record struct SaveFileHeader(int FormatVersion, int PayloadLength, Guid SaveId)`（data-model §2.2 的头三成员）；类注释写明「不变量 3：头 `SaveId` MUST 等于主体 `Id`」「不变量 4：`PayloadLength` **只**用于判截断，MUST NOT 被当作拒绝手改的校验和」
- [X] T014 [P] `KFL.Infrastructure`：新增常量与编解码选项单点 `src/KFL.Infrastructure/Save/SaveFileFormat.cs`（契约十 §3、§7）——`Version = 1`、扩展名 `.dat`（二进制，默认）/ `.json`（JSON）、备份目录名 `backups`、两种格式编解码选项的**唯一**出处：二进制 `MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData)` 且压缩 = `None`；JSON `WriteIndented = true` + `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping` + `Converters = { new JsonStringEnumConverter() }`，且 MUST NOT 容忍注释/尾随逗号。**MUST NOT** 出现「每 3 回合」或纪元 `(1, 1)`（那是 `KFL.Rules/Config/` 的单点；`Infrastructure` 看不到 `Rules`，G-05）
- [X] T015 [P] `KFL.Infrastructure`：新增名称口径唯一真源 `src/KFL.Infrastructure/Save/SaveName.cs`（契约九 §4；R-13）——`Validate`/判定接口一律**拒绝、MUST NOT 静默规范化**（避免「同名却指向不同文件」的歧义），逐条：**放行**中文、`-`、内部空格；**拒绝** `\` `/` `:` `*` `?` `"` `<` `>` `|`（9 个，FR-007 的 Q3 裁决）、空串、纯空白、长度 > **255** 字符（NTFS 文件名分量上限，属文件系统事实，**不进** `KFL.Rules/Config/`）、Windows 保留设备名 `CON`/`PRN`/`AUX`/`NUL`/`COM1~COM9`/`LPT1~LPT9`（**大小写不敏感**，**带扩展名与否都算**）、首尾空白、以 `.` 结尾、ASCII 控制字符 `0x00~0x1F`。**同名判定（含 `Exists`、覆盖冲突、改名目标）MUST 大小写不敏感**（Q7：`SongJia` ≡ `songjia`），保存时**原样保留**玩家输入的大小写写法；不做 Unicode NFC 规范化。本类型**零**文件系统调用（契约九 §4.1）
- [X] T016 [P] `KFL.Infrastructure`：新增唯一写盘路径 `src/KFL.Infrastructure/Save/AtomicFileWriter.cs`（契约九 §3；R-14）——顺序 MUST 为：① 在**目标同目录**建临时文件（MUST NOT 落在系统临时目录，同卷 ⇒ 替换原子）→ ② `Flush(flushToDisk: true)` → ③ `File.Move(tmp, target, overwrite: true)`（目标不存在时同一条调用即首次落盘）；失败路径 MUST 尽力删除临时文件且 **MUST NOT 触碰目标文件**；MUST 暴露**仅供测试**的写入钩子 `Action<int>? OnChunkWritten`（SC-006 的可测性，不必真的杀进程）。每一行文件系统访问 MUST 在**同一行**带 `// arch-guard:allow <理由>`（FR-019；SC-008；本类型 ≤ 10 行）
- [X] T017 [P] `KFL.Infrastructure`：新增目录与探针 `src/KFL.Infrastructure/Save/SaveCatalog.cs`（契约九 §4.1、§5；R-18）——根目录与 `backups/` 的枚举、**大小写不敏感**的存在性判定、目录不存在时自动创建（创建失败/无写权限 ⇒ `SaveAccessException`，MUST NOT 静默吞）、**只读头记录**的 `SaveId` 探针（`FindBySaveId` MUST NOT 读主体）、异格式同名文件与旧标识备份的清理原语。每一行文件系统访问同一行带 `// arch-guard:allow <理由>`（≤ 10 行）；`backups/` 下的文件 MUST NOT 参与唯一性判定与 `Exists`（它是副本，不是候选档）
- [X] T018 [P] `KFL.Infrastructure`：新增 `src/KFL.Infrastructure/Save/SaveOptions.cs`（契约九 §1）——`public sealed record SaveOptions { public string? RootDirectory { get; init; } public SaveFormat DefaultFormat { get; init; } = SaveFormat.Binary; }`；`RootDirectory == null` ⇒ `AppContext.BaseDirectory` + `saves`（FR-006；章程「存档、日志等运行时路径 MUST 可配置」）；已配置 ⇒ 原样使用
- [X] T019 [P] `KFL.Infrastructure`：在 `src/KFL.Infrastructure/KFL.Infrastructure.csproj` 增 `<PackageReference Include="MessagePack" Version="2.5.305" />`（**唯一**新增包；FR-024；R-24 已实测 `net10.0` 0 警告 0 错误、`decimal` scale 逐位保留、连续 `[Key]` ⇒ 数组格式）；MUST NOT 引入 `MessagePackAnalyzer`、`MessagePack.ReactiveProperty` 或任何第三方文件系统/JSON 抽象
- [X] T020 [P] 新增共享存档夹具 `tests/KFL.Tests/Fixtures/SaveFixtures.cs`：构造一份覆盖**≥14 类字段类别**的完整推演状态（在册 / 已亡 / 外嫁归档 / 待阙 / 致仕 / 服刑 / 患病 / 饥馑 / 有贷款本息 / 功名变迁史非空 / 非默认难度 / 非默认出身 / 三池金额含非整数 scale / 计时器），全程只用 `Core` 的公开 API（需要时用 T007/T008/T009 的 `internal` 面），**MUST NOT** 走反射绕过不变量校验；夹具全部取值显式，MUST NOT 读环境或时钟
- [X] T021 新增测试沙箱 `tests/KFL.Tests/Infrastructure/SaveTestSandbox.cs`（R-21）——**唯一**的临时目录/文件操作辅助类（建临时根、读字节、列文件、复制备份回主文件、清理），每一行文件系统访问同一行带 `// arch-guard:allow <理由>`（≤ 15 行）；**MUST NOT** 新建不在 G-07 扫描根内的测试目录（如 `tests/KFL.Tests/Save/`）来省掉注释。其余测试类 MUST 一律经本沙箱访问文件系统。（依赖 T010、T018）

**Checkpoint**: 地基就绪（Core 复原面、存储侧类型、三个文件系统收敛点、共享夹具与沙箱）——
三个 user story 可以开始；MVP 从 US1 开始。

---

## Phase 3: User Story 1 - 关掉再打开，家族还在原地 (Priority: P1) 🎯 MVP

**Goal**: 把**整份推演状态**逐字段落到磁盘、再逐字段读回——默认二进制 MessagePack（`.dat`）、
可选 `System.Text.Json`（`.json`），两种格式承载**同一信息集**；读回后继续推演 MUST 与
「从未落盘」逐字段一致（FR-001~FR-005、FR-014~FR-017、FR-020、FR-021）。

**Independent Test**: 在**临时目录**中用 [SaveFixtures](#phase-2-foundational-blocking-prerequisites)
的全字段类别状态，分别以二进制与 JSON 各写入一次再读回，逐字段断言读回值等于写入前的值
（≥14 类，SC-001）；再对读回的状态继续结算若干个月，断言与「同一状态从未落盘」的基线逐字段相同
（US1 AS5；FR-017）。全程不启动界面、不启动游戏循环（quickstart 场景 1/6）。

### Implementation for User Story 1

- [X] T022 [P] [US1] 新增主体根 DTO `src/KFL.Infrastructure/Save/Serialization/SaveStateDto.cs`（契约十 §4.1/§4.2/§4.3；**线上形状的唯一真源**）——`SaveStateDto`：`[Key(0)] id(Guid)`、`[Key(1)] currentYear(int)`、`[Key(2)] currentMonth(int)`、`[Key(3)] difficulty(Difficulty)`、`[Key(4)] pendingDifficulty(Difficulty?)`、`[Key(5)] origin(Origin)`、`[Key(6)] family(FamilyDto)`、`[Key(7)] economy(FamilyEconomyDto)`；`FamilyDto`：0 `name`(string)、1 `hasShiStatus`(bool)、2 `headId`(Guid?)、3 `members`(PersonDto[]，**含归档成员、按加入顺序**)、4 `settledOutsiders`(Guid[])、5 `marriageAlignedOutsiders`(Guid[])；`PersonDto` 22 个成员**逐字**按契约十 §4.2 的 Key→键名（0 `id` … 21 `degreeHistory`）；`FamilyEconomyDto`：0 `livingStandard`、1 `pendingLivingStandard`、2 `grainPriceIndex`(decimal)、3 `treasury`、4 `holdings`、5 `ledger`(LedgerEntryDto[]，**全量、按追加顺序**)、6 `famine`。每个成员 MUST 同时标 `[MessagePackObject]`/`[Key(n)]`（从 0 **连续**，⇒ 数组格式、位置式）与 `[JsonPropertyName("…")]` + `[JsonRequired]`；属性改名 MUST NOT 改变线上键名
- [X] T023 [P] [US1] 新增值类型 DTO `src/KFL.Infrastructure/Save/Serialization/SaveStateValueDtos.cs`（契约十 §4.2/§4.3）——`TalentDto`（0 `agriculture`、1 `commerce`、2 `officialdom`、3 `craft`，皆 `int`）；`StatusTimersDto`（0 `sentenceRemainingMonths`、1 `examBanRemainingMonths`、2 `promotionBanRemainingMonths`、3 `awaitingPostRemainingMonths`，皆 `int?`，**四成员**）；`DegreeRecordDto`（0 `level`、1 `placement`、2 `changedYear`、3 `changedMonth`、4 `cause`、5 `imperialClass`）；`TreasuryDto`（0 `cash`(decimal)、1 `savings`(decimal)、2 `merchantCapital`(decimal)、3 `loan`、4 `savingsRate`(decimal?)、5 `savingsRateYear`(int?)）；`LoanDto`（0 `principal`(decimal)、1 `accruedInterest`(decimal)、2 `monthsSinceInterest`(int)）；`HoldingsDto`（0 `farmlandMu`、1 `ruralHouses`、2 `urbanHouses`、3 `shops`，皆 `int`）；`LedgerEntryDto`（0 `year`、1 `month`、2 `personId`(Guid?，`null` = 家族级)、3 `category`、4 `amountWen`(decimal，**变动量不是余额**)）；`FamineDto`（0 `stage`、1 `elapsedMonths`）。**`StatusFlag` 的线上形状**：二进制 = **位掩码整数**（原值），JSON = **枚举名的数组**，`None`（= 0）写作**空数组** `[]`（MUST NOT 写 `["None"]`）；未登记的位（`1 << 9` 及以上）⇒ `InvalidStructure`。**`SalaryMode`、`TreasuryPool` 等派生量 MUST NOT 出现在任一键表里**（不变量 7）
- [X] T024 [P] [US1] 新增一致性校验 `src/KFL.Infrastructure/Save/Serialization/SaveStateValidator.cs`（data-model §3.11 第 ⑩ 步；R-04 九条）——逐条校验，任一不成立 ⇒ `SaveFileException(Kind = InvalidStructure)`：① 成员标识唯一；② 配偶关系**双向一致**（`a.SpouseId == b` ⇔ `b.SpouseId == a`）；③ `FormerSpouseIds` 每项都指向本家族成员、不含自身、不含当前配偶、无重复；④ `HeadId` 为 `null` 或指向**在册**成员；⑤ 每个计时的非空都对应其状态位为真；⑥ `EntryTrack` 非空 ⇒ `AwaitingPost` 为真；⑦ 饥馑「阶段为零 ⇔ 计时为零」且非零阶段计时 ≥ 1；⑧ 头 `SaveId` == 主体 `GameState.Id`；⑨ 账本每条资金类条目金额非零。校验的**唯一真源**是 `Core` 自身的不变量，MUST NOT 在存档层复制一套字段级规则
- [X] T025 [US1] 新增二进制格式实现 `src/KFL.Infrastructure/Save/Serialization/ISaveFormat.cs` 与 `MessagePackSaveFormat.cs`（契约十 §1/§2/§3/§6；R-05/R-09）——接口形状 `byte[] Write(SaveFileHeader header, SaveStateDto dto)` / `(SaveFileHeader Header, SaveStateDto Dto) Read(byte[] bytes)`；二进制布局：`0x93`（`fixarray(3)`）起手 → `Write(int) FormatVersion` → `Write(int) PayloadLength` → `Write(string) SaveId`（`D` 格式，恰 36 字符）→ 随后**直接拼接**主体的 MessagePack 数组字节；读取用 `MessagePackReader` 读头并以 `Consumed` 切片反序列化主体；选项取自 T014（`UntrustedData`、压缩 `None`）；截断判据 = 「文件剩余字节 < 头声明的 `PayloadLength`」⇒ `Truncated`；写入次序固定为「先算主体字节 → 再写头（此时长度已知）→ 拼接」，**不需两趟写盘**。（依赖 T013、T014、T022、T023）
- [X] T026 [US1] 新增 JSON 格式实现 `src/KFL.Infrastructure/Save/Serialization/JsonSaveFormat.cs`（契约十 §2/§3/§6；R-07/R-08）——布局：`{"FormatVersion":1,"PayloadLength":N,"SaveId":"…","State":<主体>}`（`State` 之前**恰有**这三个成员，次序固定）；选项取自 T014（缩进、`UnsafeRelaxedJsonEscaping` ⇒ 中文与「」按原样落盘不许转义成 `\uXXXX`、`JsonStringEnumConverter` ⇒ 枚举写**名字**）；解析分**两段**——① `JsonDocument.Parse` 只看文本是否合法（失败 ⇒ `Malformed`）→ ② 把 `State` 映射到 DTO，此时才判「必需成员是否齐备」（缺成员 ⇒ `InvalidStructure`）；MUST NOT 启用注释容忍或尾随逗号。（依赖 T013、T014、T022、T023）
- [X] T027 [US1] 新增逐字段映射器 `src/KFL.Infrastructure/Save/Serialization/SaveStateMapper.cs`（data-model §3.1~§3.10；R-02/R-12）——`SaveStateDto ToDto(GameState state)` 与 `GameState ToDomain(SaveStateDto dto)`；每个字段映射 MUST 是**显式点对点赋值**，MUST NOT 使用反射、MUST NOT 使用「无注解契约」序列化。复原**必须**严格按 data-model §3.11 的次序：① 建 `Family`（名 / 仕身份）、按**原始加入顺序**注册全部成员（无父母者经 `AddOutsider`、有父母者经 `AddChild`，配偶先留空——`AddChild` 要求父母已在册）→ ② 写 `Study`/`Health`/`Lifespan`/`Talents`/`Rank`/`Merit`/`Occupation`/`MonthsInOffice` → ③ 写 `Status`（此时 `Timers`/`EntryTrack` 仍为空，清位的校验恒成立）→ ④ 写 `Timers`、随后 `EntryTrack`（二者要求状态位已为真）→ ⑤ 按序逐条 `AppendDegree` → ⑥ 写 `SpouseId`（双向）、`FormerSpouseIds`（经既有 `internal` 的 `SetSpouse`/`AddFormerSpouse`，**MUST NOT** 经 `Family.Marry`——那会消耗「首次家族内成婚」的对齐机会、把历史再演一遍）→ ⑦ `SetHead`、`RestoreOutsiderBookkeeping` → ⑧ 建 `Treasury`（三池 + 贷款 + 利率，**先年份后利率**）、`Holdings`、`Ledger`（逐条 `Append`）、`Famine`（经 `RestoreFamine`）→ ⑨ 建 `FamilyEconomy` → 建 `GameState`（标识 / 年月 / 难度 / 出身，**经公开构造函数**，MUST NOT 用 `AdvanceMonth()` 循环逼近）。金额一律 `decimal`（**文**）原值，MUST NOT 经 `double`、MUST NOT 量化。（依赖 T008、T009、T022、T023）
- [X] T028 [US1] 新增服务实现（US1 范围）`src/KFL.Infrastructure/Save/SaveService.cs`（契约九 §1/§2/§3；data-model §6.1/§6.2）——装配 `SaveName`/`SaveCatalog`/`AtomicFileWriter` 与两个 `ISaveFormat` 实现；实现 `Save(state, slot, overwrite)`：**写任何字节之前**完成全部校验（名称 → 冲突 → 同 UUID 探针）→ 序列化主体 → 写头 → 经 `AtomicFileWriter` 原子替换；实现 `Load(slot)`：按契约十 §6 的八步判定（存在 → 头可读 → 剩余字节 ≥ `PayloadLength` → `FormatVersion == 1` → 主体可解析 → 必需成员齐备 → 沿 `Core` 校验路径逐字段复原 → `SaveStateValidator` 九条），失败按 §6 的分类抛 `SaveFileException(Kind = …)`。**失败原子性**：`Save` 的全部拒绝路径 MUST NOT 产生任何文件写入；`Load` 的全部拒绝路径 MUST NOT 修改原文件、MUST NOT 返回默认状态或部分状态、MUST NOT 污染调用方已有的内存状态（`Load` 只**新建**一个 `GameState`）。本任务只落 US1 需要的 `Save`/`Load`/`RootDirectory` 骨架，`Exists`/`FilePathOf`/`FindBySaveId`/`Rename`/`Backup` 由 US3 的 T039~T042 补齐。（依赖 T011、T012、T015、T016、T017、T018、T025、T026、T027）

### Tests for User Story 1

- [X] T029 [US1] 新增往返单测 `tests/KFL.Tests/Infrastructure/SaveServiceTests.cs`（US1 部分；quickstart 场景 1；SC-001/SC-002；FR-003/FR-016/FR-017）——经 `SaveTestSandbox` 的临时目录：① 两种格式各一轮**逐字段等价**，逐类断言 **≥14 类**（存档标识 / 年月 / 难度与待生效难度 / 出身 / 家族姓氏与仕身份与家主与成员数含归档 / 成员全部字段 / 功名变迁史 / 账本 / 三池金额 / 资产 / 贷款本金与欠息（**分别**断言）/ 米价系数 / 生活费档位与待生效档位 / 饥馑阶段与计时），差异项数 = 0；② AS4：刚读回再写再读 MUST 与第一次读回**相同**（无漂移）；③ AS5：读回后继续结算一个月，与「同一状态从未落盘」基线**逐字段相同**（FR-017）；④ AS6：已亡 / 外嫁归档成员仍在家族中且可按标识取回（`Members.Count == 在册数 + 归档数`）；⑤ 金额 MUST 用 `decimal.GetBits` **逐位**比较（含 scale），MUST NOT 只用 `==`（R-10；FR-016）。（依赖 T020、T021、T028）
- [X] T030 [US1] 新增格式契约单测 `tests/KFL.Tests/Infrastructure/SaveFormatContractTests.cs`（quickstart 场景 6；契约十 §4/§5/§7；SC-001/SC-010）——① 头记录**恰有三个成员**且次序为 `FormatVersion, PayloadLength, SaveId`（二进制首字节 `0x93`）；② DTO 的**键名集合**与契约十 §4 的键表逐项一致——多一个、少一个都失败（键表是两格式共用的唯一字段清单，防「两格式同缺一个字段」）；③ 枚举在 JSON 落成**名字**（如 `"Hard"`）、在二进制落成**整数**，非法枚举名/越界整数被拒；`StatusFlag.None` 在 JSON 中写作 `[]`；④ 同一状态以两种格式存读得到的**信息集等价**（逐字段断言）；⑤ 构造一份 **100 年（1200 回合）** 的存档，写入并读回：完整可写可读、账本条目**丢失数 = 0**、条目顺序一致、落盘形态是**单一文件**（不分块、不压缩）（FR-021；SC-010）。（依赖 T020、T021、T025、T026、T027）
- [X] T031 [P] [US1] 按需扩写既有夹具 `tests/KFL.Tests/Fixtures/FamilyFixtures.cs` 与 `tests/KFL.Tests/Fixtures/EconomyFixtures.cs`——**仅当**现有夹具缺少构造「全字段类别存档状态」所需构造点时（如已亡 / 外嫁归档成员、服务中的计时器、非整数 scale 金额的成形点）；MUST NOT 改动既有夹具的既有语义与取值，MUST NOT 把规则数值抄成第二处出处（SC-008）

**Checkpoint**: US1 独立可验收（**MVP**）——两种格式的完整往返与「落盘不改推演结果」可断言；
US2 / US3 拥有可信的存档形态。

---

## Phase 4: User Story 2 - 自动保存与备份：忘记保存的代价是零 (Priority: P2)

**Goal**: 每回合（= 1 游戏月）结算**完整成功后覆盖**当前档；每 **3** 回合在其后留一份**覆盖式**
备份；退出时复用**同一个**保存入口再存一次；写入具**原子性**（要么旧档、要么新档）
（FR-010~FR-013、FR-018；SC-003/SC-006）。

**Independent Test**: 用可注入的存档服务替身与固定种子，从同一初始状态连续推进 12 个月，
断言 `Save` 被调用 12 次、`Backup` 在 3/6/9/12 回合各一次（共 4 次）且备份恒 1 份；结算抛出时
不保存、保存抛出时不备份且内存状态保留；不注入服务时零次保存；再断言一次被**中断的写入**
不会损坏原有存档（SC-006）。全程不启动界面（quickstart 场景 2/3）。

### Implementation for User Story 2

- [X] T032 [P] [US2] 新增节奏数值单点 `src/KFL.Rules/Config/SaveCadencePolicy.cs`（契约十一 §1/§4；R-16）——`EpochYear = 1`、`EpochMonth = 1`（规格书 §3「从 1 年 1 月开始推演」）、`BackupIntervalTurns = 3`（规格书 §14「每 3 回合」）、`CompletedTurnsAt(GameDate current) => (current.Year - EpochYear) * 12 + (current.Month - EpochMonth)`、`ShouldBackup(int turns) => turns > 0 && turns % BackupIntervalTurns == 0`（`turns > 0` 排除「尚未结算任何回合」被算成 3 的倍数）。**MUST NOT 新增持久化的回合计数器/时间戳字段**（那是与时间同步维护的第二真源）；**MUST NOT** 读墙钟、MUST NOT 用 `File.GetLastWriteTime` 之类的文件系统时间（FR-018）
- [X] T033 [US2] 在 `src/KFL.Rules/Config/GameConfig.cs` 新增 `Save` 组只转发视图（同时转发 `SaveCadencePolicy` 的 `BackupIntervalTurns` / `EpochYear` / `EpochMonth`），保持「**零字面量**」的既有口径（SC-008 的扫描对象）；`3` 是整数，按契约五 §5 的既有口径**不进** `ConfigLiteralTests.RegisteredValues`（该清单只收小数与 4 位及以上官俸）。（依赖 T032）
- [X] T034 [P] [US2] 新增回合增量快照 `src/KFL.Rules/Save/TurnOutcome.cs`（data-model §5；契约十一 §2）——`public readonly record struct TurnOutcome(SettlementResult Settlement, int CompletedTurns, bool Saved, bool BackedUp)`；类注释写明它是**增量**快照，**MUST NOT 被写回 `GameState`**
- [X] T035 [US2] 新增回合编排 `src/KFL.Rules/Save/TurnRunner.cs`（契约十一 §2；R-17）——`public TurnRunner(IRandomService randomService, ISaveService? saveService, SaveSlot slot)`（**当前槽位由构造参数在装配期注入**，`Advance`/`SaveOnExit` **不逐次接收**；槽位是**会话**信息，MUST NOT 进 `GameState`、MUST NOT 落盘，spec 裁决 Q4）；`TurnOutcome Advance(GameState state)` MUST 严格按序：① `new MonthlySettlementEngine(randomService, new GameStateClock(state)).Settle(state)` → ② `saveService is null` ⇒ 直接返回（**自动保存关闭**，MUST NOT 新增持久化设置项）→ ③ `saveService.Save(state, slot, overwrite: true)`（FR-010）→ ④ `SaveCadencePolicy.ShouldBackup(SaveCadencePolicy.CompletedTurnsAt(state.CurrentDate))` 为真 ⇒ `saveService.Backup(slot)`（FR-011）→ 返回 `TurnOutcome`。条款：③ MUST 在①**正常返回之后**（结算抛出 ⇒ 不 `Save`、不 `Backup`，这是**结构性**成立，MUST NOT 靠 `try/catch` 约定）；④ MUST 在③**成功之后**；③ **抛出时**（磁盘满 / 无写权限 / 目标盘不可用）MUST **保留已推进的内存状态**并把失败向上传播——MUST NOT 回滚（回滚需结算前快照 = FR-020 禁止的第二份真源）、MUST NOT 静默吞，且 MUST NOT 备份，「已推进但未落盘」是**允许的中间态**（spec 裁决 Q6）；`void SaveOnExit(GameState state)` MUST 复用③的**同一个** `Save` 入口与**同一个**槽位（FR-012；MUST NOT 另写一套参数/序列化/路径）；MUST NOT 读时钟、MUST NOT 自增计数器。（依赖 T011、T033、T034）

### Tests for User Story 2

- [X] T036 [US2] 新增节奏单测 `tests/KFL.Tests/Rules/SaveCadenceTests.cs`（quickstart 场景 2；契约十一 §5；SC-003；FR-010~FR-012、FR-018）——用**内存替身** `RecordingSaveService`（记录调用与实参，**不碰文件系统**）与固定种子：① 连续 `Advance` 12 回合 ⇒ `Save` 调用 **12** 次、`Backup` 调用 **4** 次且调用序号为第 **3、6、9、12** 次；② `CompletedTurnsAt` 初始为 **0**、第 1~12 次结算后依次为 **1~12**；③ **结算抛出** ⇒ 该回合 MUST NOT 调 `Save`、MUST NOT 调 `Backup`；④ **保存抛出** ⇒ MUST NOT 调 `Backup`，且内存状态保留（`state` 仍处于已结算的当前年月）、失败向上传播；⑤ 保存失败后继续推进 ⇒ 下一次 `Advance` 写入的是**当前**完整状态（含上次未落盘的那一个月），账本条目全量与推进次数一致、**无缺口**（Q6）；⑥ **不注入** `ISaveService` ⇒ 零次 `Save`，且与「开启自动保存推进 N 回合」的**存档内容逐字段相同**（US2 AS4：自动保存 MUST NOT 改变推演结果）；⑦ `SaveOnExit` 与自动保存命中**同一个** `Save(state, slot, overwrite: true)` 调用（替身记录实参比对）。（依赖 T020、T035）
- [X] T037 [P] [US2] 新增原子性单测 `tests/KFL.Tests/Infrastructure/AtomicWriteTests.cs`（quickstart 场景 3；契约九 §3 条款 6；SC-006；FR-013）——经 `AtomicFileWriter` 的测试钩子 `OnChunkWritten` 对一份**已存在**的当前档注入一次「写入中途失败」，再读该存档：目标路径上存在的文件 MUST **要么**是写入前的完整存档、**要么**是写入后的完整存档（「两边都不是」出现 **0** 次）且**始终可被成功读回**；对**备份**注入一次失败 ⇒ 当前档 MUST NOT 受任何影响；失败后无残留临时文件（或残留临时文件 MUST NOT 参与任何读取路径）。（依赖 T016、T021、T028）
- [X] T038 [US2] 在 `tests/KFL.Tests/Infrastructure/SaveServiceTests.cs` 增**真实文件版节奏**断言（quickstart 场景 2 末段；SC-003）——临时目录中经 `TurnRunner` 推进 12 回合后：当前档内容 == 第 12 回合结束时的状态；`backups/` 下该存档恒定 **1** 个文件（覆盖式，覆盖率 100%）。（依赖 T029、T035）

**Checkpoint**: US1 与 US2 各自独立可用——「能不能存」与「什么时候存」都已可断言。

---

## Phase 5: User Story 3 - 档名、目录与格式：存档是玩家的东西 (Priority: P3)

**Goal**: 存档名支持中文、受 Windows 实际非法字符集与保留名约束；同名未经显式确认不覆盖；
改档名**不**变存档标识（UUID）与游戏内容（备份同步改名）；存档根可配置（默认可执行文件旁
`saves/`）；缺失 / 截断 / 结构不符 / 版本不符各自可识别地失败
（FR-006~FR-009、FR-014、FR-015；SC-004/SC-005/SC-007）。

**Independent Test**: 在临时目录中逐条断言：中文档名可用、9 个非法字符与保留名被拒、`Exists`
与格式无关且大小写不敏感、同名未经确认不覆盖、改名后 UUID 与内容不变且备份随改名、
两种格式扩展名正确、自定义根生效、同 UUID 另一文件名被拒、格式切换后旧档与旧备份被移除、
缺失 / 截断 / 结构不符 / 版本不符各自产生可识别 `Kind`。全程不需要真实用户界面
（quickstart 场景 4/5）。

### Implementation for User Story 3

- [X] T039 [US3] `SaveService`：落实查询面（契约九 §1 条款 5、§5 条款 1/3）——`Exists(slot)` MUST 回答「该**名字**在存档根下是否已有存档」**与格式无关**、比较**大小写不敏感**；`FilePathOf(slot)` MUST 返回槽位在**当前根**下的绝对路径且 MUST NOT 越出根；`FindBySaveId(saveId)` MUST **只读头记录**（未命中返回 `null`，MUST NOT 读主体）。（依赖 T028）
- [X] T040 [US3] `SaveService`：落实命名与覆盖（契约九 §2/§4/§5；FR-007/FR-008；Q7）——`Save` 接入 T015 的 `SaveName`：名称不合法 ⇒ `ArgumentException` 且 **MUST NOT 落到文件系统**；同一根下该名字已有存档（**任一格式**）而 `overwrite: false` ⇒ `SaveNameConflictException`；`overwrite: true` ⇒ 覆盖写入后移除同名的**异格式**文件（「该名字归调用方」）；同根下**另一个文件名**携带同一 `SaveId` ⇒ `SaveIdentityConflictException` 且 **MUST NOT 写任何文件**（FR-009 与成就 UUID 归属的收口）；根目录不可创建/不可写 ⇒ `SaveAccessException`，MUST NOT 静默吞。（依赖 T039）
- [X] T041 [US3] `SaveService`：落实改名不变性（契约九 §6；FR-009；SC-004；Q5）——`Rename(slot, newName)`：校验新名 → 目标不冲突 → **主档只做文件移动**（`File.Move`，MUST NOT 触碰任何主体字节 ⇒ `SaveId` 与全部游戏内容**结构性不变**、`Family.Name` 不变）→ **同时**把该存档在 `backups/` 下的备份（若有）**改名**为同名同扩展名（使「主档名 = 备份名」在改名后仍成立）；备份改名失败 MUST NOT 损坏主档，且 MUST 以 `SaveAccessException` 家族的可识别方式暴露、MUST NOT 静默吞；返回新槽位，旧槽位文件 MUST 已不存在。（依赖 T040）
- [X] T042 [US3] `SaveService`：落实备份原语与格式切换（契约九 §5/§7；FR-011；Q1/Q5）——`Backup(slot)` = 把当前档字节**原子复制**为 `<根>/backups/<存档名>.<与当前档相同的扩展名>`（经 `AtomicFileWriter`，目录不存在即创建；**覆盖式**、每个存档恒 1 份、MUST NOT 按时间戳追加）；备份失败 MUST NOT 影响已写好的当前档、MUST NOT 静默吞。格式切换（`name.dat` 存在、请求写 `name.json`）：**先成功写出**新格式主档，**再**移除 UUID 相同的旧格式主档**与**该存档的旧格式备份；新文件写出失败 ⇒ MUST NOT 删除旧文件；发现同标识的**其他**备份文件（如遗留的旧名副本）时 MUST 在本次备份/切换中清理，使「`backups/` 下同一存档标识恒 1 份且与主档同名同扩展名」在操作完成时即成立。（依赖 T041）

### Tests for User Story 3

- [X] T043 [US3] 在 `tests/KFL.Tests/Infrastructure/SaveServiceTests.cs` 增 US3 用例（quickstart 场景 5；FR-005~FR-009；SC-004/SC-005）——中文档名「宋家三十年」新建成功且磁盘上出现对应扩展名文件；9 个 Windows 非法字符（`\ / : * ? " < > |`）逐条**拒绝率 100%** 且文件系统上无非法名；`-`（连字符）**放行**；空 / 纯空白 / 超长（> 255）/ 保留设备名（`CON`、`NUL`、`COM1`…，大小写不敏感、带扩展名与否都算）/ 首尾空白 / 以 `.` 结尾逐条拒绝且 MUST NOT 落到文件系统再报错；同名已存在而未确认 ⇒ `Save` 拒绝且 `Exists` 回答 `true`；同名但**另一种格式**已存在 ⇒ `Exists` 同样回答 `true`；**仅大小写不同**的两个名字视为同名、未确认 MUST NOT 覆盖；改名后 `SaveId`、文件字节与游戏内容**逐字节不变**、`Family.Name` 未变、备份随主档同名且无旧名副本（SC-004；Q5）；未指定格式 ⇒ 落 `.dat`、指定 JSON ⇒ `.json`；自定义根 ⇒ 文件落在该目录下、未配置 ⇒ 可执行文件旁 `saves/`；同 UUID 的另一个文件名 ⇒ `SaveIdentityConflictException`；格式切换 ⇒ 先写出 `.json`、随后 `.dat` 被移除，写出失败时 `.dat` 必须仍在；把 `backups/<名>.dat` 复制回主文件后 `Load` 成功且内容一致。（依赖 T020、T042）
- [X] T044 [P] [US3] 新增失败形态单测 `tests/KFL.Tests/Infrastructure/SaveFailureTests.cs`（quickstart 场景 4；契约九 §2、契约十 §6；FR-014/FR-015；SC-007）——对同一份好档派生四种输入逐条 `Load` 并断言各自**可识别**的 `Kind`：缺失（不存在的档名）⇒ `Missing`；截断（只保留前 **60%** 字节）⇒ `Truncated`；结构不符（合法 JSON 但删掉一个必需成员）⇒ `InvalidStructure`；版本不符（把头 `FormatVersion` 改成 **2**）⇒ `UnsupportedVersion`；（附加）字节损坏（改掉头之后的若干字节）⇒ `Malformed`。四类共同：MUST NOT 返回「看起来能玩」的默认状态或部分状态；失败后**原文件内容**与**内存中已有的状态**均未被修改（差异 = 0）；版本闸门 MUST 在任何主体解析**之前**生效。（依赖 T020、T021、T028）

**Checkpoint**: 三个故事全部独立可用——存档是可被玩家命名、迁移与选择的**玩家的东西**。

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: 回写登记、门禁、人工验收与活工件一致性收口。

- [X] T045 在 `specs/004-save-system/tasks.md` 追加「回写范围登记表」（AGENTS.md「铁律」第 3 条；spec Assumptions 6；plan 回写义务清单 #11）——逐条写清「同步了哪些文件、同步到哪里」，覆盖 T003~T006 的上游活工件回写与 T007~T009 的 `KFL.Core` 新面；**MUST NOT** 回改既有条目文字（追加登记除外），MUST NOT 把 `tasks.md` 的既有条目、`implementation-notes.md`、既有 `checklists/` 当活工件回改
- [X] T046 执行四条门禁并记录实测值（AGENTS.md）：`dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false`（0 警告 0 错误）、`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false`（全绿 0 skipped）、`dotnet sln KejuFuShengLu.slnx list`（恰好六个工程）、`Get-ChildItem -Recurse -Filter *.sln | …`（无输出）；另核对 **SC-009**（`git status --porcelain` 无存档相关变更）与 **SC-008**（`KFL.Core`/`KFL.Rules` 的 G-07 违规数 = 0；存档实现的每一处文件系统访问都在同一行带 `// arch-guard:allow` 且豁免行可按契约九 §4.1 的清单**逐一列举**）；确认 `tests/KFL.Tests/KFL.Tests.csproj` 的 `_MSTestEnableParentProcessQuery` 仍为**注释态**
- [X] T047 [P] 走查 `specs/004-save-system/quickstart.md` 的场景 1~8 逐条人工验收（含场景 7 的两条过滤跑：`FullyQualifiedName~Architecture.EnvironmentDependencyTests` 与 `~Architecture.ConfigLiteralTests`；场景 8 的 `grep -rn "DateTime\.\|Environment\.TickCount" src/KFL.Infrastructure src/KFL.Rules` 无命中，且同一状态的两次存档字节**完全相同**）；把每条结果记入提交信息或 `implementation-notes.md`
- [X] T048 [P] 按实现现状回写活工件（规格书 §16.2 的活工件口径）——`plan.md` 的 Scale/Scope 计数与结构树按实际文件数修正；`spec.md` 的 **Status** `Draft` → `Completed`；复核 `contracts/` 与代码一致（键表、步骤次序、失败类别）；`research.md` 的「未决事项」保持「无」。**MUST NOT** 回改历史记录类工件（001~003 的既有 `tasks.md` 条目、`implementation-notes.md`、既有 `checklists/`）

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup（Phase 1）**：无依赖，可立即开始
- **Foundational（Phase 2）**：依赖 Setup 完成——**阻塞全部 user story**
- **US1（Phase 3）**：依赖 Foundational 完成（T007~T021）
- **US2（Phase 4）**：依赖 Foundational 完成（T011 的接口面 + T016 的原子写 + T020 的夹具）
- **US3（Phase 5）**：依赖 Foundational 完成，且**复用 US1 的 `Save`/`Load`**（T028）与 US2 的 T035（真实文件版节奏）——故实际执行顺序为 US1 → US2 → US3
- **Polish（Phase 6）**：依赖全部所需故事完成

### User Story Dependencies

- **US1（P1）**：Foundational 之后即可开工，**不依赖其它故事**——本特性的 MVP
- **US2（P2）**：Foundational 之后即可开工；只依赖 `ISaveService` **接口**（T011）与内存替身，与 US1 的实现**无编译期耦合**（可在 US1 之前独立开发）
- **US3（P3）**：其实现（T039~T042）与测试（T043/T044）落在 **US1 的 `SaveService.cs` 与 `SaveServiceTests.cs`** 上，故 MUST 排在 US1（及 T038）之后；这是**同一文件**的串行约束，不是语义耦合

### Within Each User Story

- 值类型 / 常量 → DTO → 编解码 → 映射器 → 服务 → 测试
- 测试 MUST 与实现在同一批提交（章程原则 IV）；本特性**不采用** TDD 先行，因为断言依赖类型与配置成员的存在（C# 编译期耦合）
- 故事完成后再进入下一个优先级

### Same-File Serialization（MUST 串行，不可并行）

- `src/KFL.Core/Entities/Family.cs`：T008 之后不再有任务触碰
- `src/KFL.Infrastructure/Save/SaveService.cs`：T028 → T039 → T040 → T041 → T042
- `tests/KFL.Tests/Infrastructure/SaveServiceTests.cs`：T029 → T038 → T043
- `src/KFL.Rules/Config/GameConfig.cs`：T033（本特性只加 `Save` 组）
- 三个文件系统收敛点 `AtomicFileWriter.cs` / `SaveName.cs` / `SaveCatalog.cs` 各自只由一个任务创建（T016/T015/T017），其后由 T028 装配

### Parallel Opportunities

- Setup 的 T002~T006 [P] 可同时开工（五个不同工件）
- Foundational 的 T007~T020 [P]（`AssemblyInfo.cs`、`OutsiderBookkeeping.cs`+`Family.cs`、`FamilyEconomy.cs`、`SaveFormat.cs`+`SaveSlot.cs`、`ISaveService.cs`、`SaveFileException.cs`、`SaveFileHeader.cs`、`SaveFileFormat.cs`、`SaveName.cs`、`AtomicFileWriter.cs`、`SaveCatalog.cs`、`SaveOptions.cs`、csproj、`SaveFixtures.cs`）可并行；**T021 不参与**（依赖 T010/T018 的类型）
- US1 内 T022~T024 [P]（根 DTO / 值 DTO / 校验器，三个不同文件）可并行；T031 [P] 独立于全部（改的是既有夹具文件）
- US2 内 T032 [P] 与 T034 [P] 可并行；T037 [P] 与 T036/T038 无文件冲突
- US3 内 T044 [P] 与 T043 无文件冲突
- Polish 的 T047/T048 [P] 可并行

---

## Parallel Example: Foundational

```text
# 纯新增文件并行开写（互不依赖）：
Task: "T007 KFL.Core/AssemblyInfo.cs（InternalsVisibleTo）"
Task: "T008 KFL.Core/Entities/OutsiderBookkeeping.cs + Family.cs 捕获/复原"
Task: "T009 KFL.Core/Entities/FamilyEconomy.cs 的 RestoreFamine"
Task: "T010 KFL.Infrastructure/Abstractions/{SaveFormat,SaveSlot}.cs"
Task: "T011 KFL.Infrastructure/Abstractions/ISaveService.cs"
Task: "T014 KFL.Infrastructure/Save/SaveFileFormat.cs"
Task: "T015 KFL.Infrastructure/Save/SaveName.cs"
Task: "T016 KFL.Infrastructure/Save/AtomicFileWriter.cs"
Task: "T017 KFL.Infrastructure/Save/SaveCatalog.cs"
Task: "T019 KFL.Infrastructure/KFL.Infrastructure.csproj（MessagePack 2.5.305）"
Task: "T020 tests/KFL.Tests/Fixtures/SaveFixtures.cs"

# 依赖类型面的一条随后串行：
#   T013 SaveFileHeader → T021 SaveTestSandbox
```

## Parallel Example: User Story 1

```text
# 三个 DTO/校验文件并行开写（不同文件）：
Task: "T022 Serialization/SaveStateDto.cs（根/家族/人物/经济子 DTO）"
Task: "T023 Serialization/SaveStateValueDtos.cs（值类型键表）"
Task: "T024 Serialization/SaveStateValidator.cs（R-04 九条）"

# 三者齐备后串行接线：
#   T025 MessagePackSaveFormat + T026 JsonSaveFormat → T027 SaveStateMapper → T028 SaveService
#   → T029 SaveServiceTests（US1）→ T030 SaveFormatContractTests
```

## Parallel Example: User Story 2

```text
Task: "T032 KFL.Rules/Config/SaveCadencePolicy.cs（3 与纪元 (1,1) 的单点）"
Task: "T034 KFL.Rules/Save/TurnOutcome.cs（增量快照）"

# 两者齐备后串行：T033 GameConfig.Save 转发 → T035 TurnRunner → T036 SaveCadenceTests
# 与 T036 并行：T037 AtomicWriteTests（不同文件）
```

---

## 附：需求 → 任务对照（落地完整性核对）

| 需求 | 任务 |
| --- | --- |
| FR-001 可脱离界面的存取能力（`ISaveService` 属 `KFL.Infrastructure`） | T011、T028、T029 |
| FR-002 覆盖全部推演状态（含归档成员、功名史、账本全量、贷款本息分列） | T020、T022、T023、T027、T029、T030 |
| FR-003 逐字段相等、无丢弃/默认化/近似化 | T024、T027、T029、T030 |
| FR-004 两种格式承载同一信息集 | T025、T026、T030 |
| FR-005 未指定 ⇒ 二进制、格式不影响游戏语义 | T010、T018、T029、T030 |
| FR-006 存档根可配置、默认 `saves/` | T017、T018、T043 |
| FR-007 中文放行 + 9 个非法字符拒绝、`-` 放行（Q3） | T015、T043 |
| FR-008 存在性查询 + 显式覆盖开关、同名判定大小写不敏感（Q7） | T015、T039、T040、T043 |
| FR-009 改名不变 UUID/内容/姓氏，备份随改名（Q5） | T041、T043 |
| FR-010 每回合结算成功后覆盖当前档（槽位注入 Q4；失败不回滚 Q6） | T032、T035、T036、T038 |
| FR-011 每 3 回合覆盖式备份（落点 `backups/`、同扩展名；Q1/Q5） | T032、T035、T036、T038、T042 |
| FR-012 退出保存复用同一入口与同一槽位 | T035、T036 |
| FR-013 写入原子性 | T016、T037 |
| FR-014 读档失败可识别、无副作用（不改原文件/不返回部分状态） | T044 |
| FR-015 版本闸门、禁止静默误读 | T014、T025、T026、T044 |
| FR-016 金额按原值持久化（含 scale） | T022、T023、T027、T029、T030 |
| FR-017 落盘不改推演结果、反复存读不漂移 | T029、T030 |
| FR-018 不读墙钟、时点由回合数与显式入参决定 | T032、T035、T046 |
| FR-019 文件系统收敛 + 行级 `// arch-guard:allow` | T016、T017、T021、T046 |
| FR-020 不放宽既有不变量、不引入第二真源（瞬态 DTO + 两处 `internal`） | T007、T008、T009、T022、T027 |
| FR-021 账本全量、单文件、不分块不压缩（Q2） | T023、T027、T030 |
| FR-022 单测覆盖 + 临时目录、不污染仓库 | T020、T021、T029、T036、T037、T038、T043、T044 |
| FR-023 忽略 `saves/` | T002 |
| FR-024 新增依赖支持 `net10.0` 并说明必要性 | T019（R-24 的实测表已在 `plan.md`/`research.md`） |
| SC-001 两格式各一轮 ≥14 类字段、差异项数 = 0 | T029、T030 |
| SC-002 连续落盘 24 个月后一致 + 继续推进 12 个月差异 = 0 | T029、T030 |
| SC-003 12 回合覆盖 12 次、备份 4 次、备份恒 1 份（改名/换格式后仍成立） | T036、T038、T042、T043 |
| SC-004 改名前后差别 = 0、家族姓氏未改 | T041、T043 |
| SC-005 六类非法名的拒绝率 100% | T015、T043 |
| SC-006 中断写入后「要么旧、要么新」且始终可读回 | T016、T037 |
| SC-007 四类失败各自可识别、原文件与内存差异 = 0 | T044 |
| SC-008 G-07 零违规 + 豁免行逐一列举 | T016、T017、T021、T046 |
| SC-009 门禁跑完仓库存档相关变更数 = 0 | T002、T021、T046 |
| SC-010 1200 回合完整可写可读、账本丢失数 = 0、单一文件 | T030 |

---

## 附：回写义务登记（AGENTS.md「铁律」；spec Assumptions 的落地）

> 本特性**没有**对任何上游 `KFL.Core` 类型或既有契约做**形状变更**（零字段增删、零签名变更、
> 零步骤序列插入——落盘被刻意放在结算**之外**的一层）。下表登记的是**声明与验证义务**的回写：
> 哪些「待阶段④ 处理」的登记被结清、哪一个「接口待定义」被落实。
> 逐条内容与依据见 [plan.md](./plan.md) 的「回写义务清单」。
>
> **T045 收口补注（实现期新增，原表未涵盖）**：上句「零签名变更」在**实现**阶段出现了一处
> 例外——`KFL.Core/Entities/FamineState.cs` 的 `Stage` 与 `ElapsedMonths` 由 `private set`
> 放宽为 `internal set`（见第 12 行）。这是**已登记的授权内实现细节**（004 变更源 `plan.md`
> 「核心层改动」一节的「两处最小 `internal` 复原面」），但**不止两处**：除 `Family` 的
> `CaptureOutsiderBookkeeping`/`RestoreOutsiderBookkeeping` 与 `FamilyEconomy.RestoreFamine`
> 之外，`FamineState` 的两个访问器也必须放宽，否则 `RestoreFamine` 写不进「任意的
> (阶段, 计时) 组合」。第 12 行即为该处的事实登记。

| # | 目标工件（活工件） | 回写内容 | 任务 |
| --- | --- | --- | --- |
| 1 | `科举浮生录规格书.md` §14 | 该节末尾的「§17 裁决回写」段记入 Q1/Q2/Q3（2026-10-11 已完成，本特性只**核对其在位**；原始条目文本保留不改写） | T006 |
| 2 | `specs/001-core-skeleton/spec.md` FR-012（L157–160） | 「改档名不变性属阶段④」→ 标注**已由 004 交付并验证** | T003 |
| 3 | `specs/001-core-skeleton/data-model.md` §2.3 `Id` 行（L261）与 FR 对照表（L313） | 同上两处措辞 → 标注**已由 004 交付并验证** | T003 |
| 4 | `specs/001-core-skeleton/contracts/injection-seams.md` §5（L80–82）与 §2 末句（L47–48） | `ISaveService` → **已由 004 定义**（指向契约九）；「存档时间戳属阶段④」→ **已由 004 复核为「无该需求」** | T004 |
| 5 | `specs/001-core-skeleton/research.md` R-03（L72–74、L383） | UUID 碰撞 → **已由 004 结清**（`SaveIdentityConflictException`） | T005 |
| 6 | `specs/001-core-skeleton/research.md` R-16（L363–364、L389） | 账本落盘体量与分块 → **已由 004 结清**（单文件全量、不分块、不压缩） | T005 |
| 7 | `specs/001-core-skeleton/research.md` R-04 未决事项（L393）、`specs/001-core-skeleton/plan.md`（L39） | 系统时钟抽象 → **结清为「不需要」**（本特性不落盘时间戳） | T005 |
| 8 | `specs/002-monthly-settlement-economy/research.md` 未决事项表（L441） | 流水账落盘体量与按年分块 → **已由 004 结清** | T005 |
| 9 | `specs/002-monthly-settlement-economy/contracts/ledger.md` §6（L73–74）、§7（L78–79） | **只补注**（判定句本身 MUST NOT 改写） | T005 |
| 10 | `specs/002-monthly-settlement-economy/data-model.md`（L7）、`plan.md`（L43–44）、`research.md`（L283–285） | 保留原文，仅在「含义已改变」处补注版本闸门口径 | T005 |
| 11 | `specs/004-save-system/tasks.md` | 登记上述回写范围（本表即该登记，由 T045 收口核对） | T045 |
| 12 | `src/KFL.Core/Entities/FamineState.cs`（**上游类型，唯一的一处签名放宽**） | `Stage` 与 `ElapsedMonths`：`{ get; private set; }` → `{ get; internal set; }`。**理由**：`TransitionTo` 把计时钉死为 1、`Tick` 只能逐月步进，二者都无法表达存档里任意的「(饥馑阶段, 已历时月数)」组合，而 `FamilyEconomy.RestoreFamine` 按 data-model §3.11 步骤⑧ 必须精确复原该组合。**边界**：`internal set` 只对 `KFL.Infrastructure`（`InternalsVisibleTo`）可见，公开面从「只读」变为「对外只读」；「阶段为 None ⇔ 计时为 0」的一致性判定仍归存档层 `SaveStateValidator` 第⑦条，Core 不新增不变量。**声明位置**：004 `plan.md`「核心层改动」一节与「Scale/Scope」计数（2 文件 → 3 文件），T048 回写 | T009 · T045 |

> **不改的**：001~003 的既有 `tasks.md` 条目、`implementation-notes.md`、既有 `checklists/` 属
> **历史记录类工件**，MUST NOT 回改（规格书 §16.2；AGENTS.md）。003 的 Out of Scope 行
> （「存档落盘属逻辑轨 ④」）语义**未变**，无需改动。

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. 完成 Phase 1 Setup（含上游活工件的回写）
2. 完成 Phase 2 Foundational（**CRITICAL**——阻塞全部故事）
3. 完成 Phase 3 US1（两格式的完整往返）
4. **STOP and VALIDATE**：按 quickstart 场景 1/6 独立验收 US1（≥14 类字段差异项数 = 0、
   金额 `decimal.GetBits` 逐位、读回后继续推演与基线逐字段相同、1200 回合单文件可读回）
5. 此时 001~003 的全部推演成果首次具备「跨会话」能力，可先行提交/演示

### Incremental Delivery

1. Setup + Foundational → 地基就绪（Core 最小复原面、存储侧类型、三个文件系统收敛点）
2. US1 → 独立验收 → 提交/演示（**MVP**）
3. US2 → 独立验收（12 回合节奏、结算/保存失败语义、中断安全）→ 提交
4. US3 → 独立验收（命名、目录、格式、改名不变性、失败形态）→ 提交
5. 每个故事只增价值，不破坏前序故事；**不做**界面、**不做**成就持久化、**不做**旧格式迁移

### Parallel Team Strategy

1. 团队合写 Setup + Foundational
2. Foundational 完成后：一人做 US1（`Serialization/` 全套），另一人做 US2（`KFL.Rules` 的节奏与编排，
   只依赖 `ISaveService` 接口 + 内存替身，两边**触文件不重叠**）
3. **US3 必须排队**：T039~T042 与 T043 都落在 US1 的 `SaveService.cs` / `SaveServiceTests.cs` 上
4. 全部完成后由一人执行 Phase 6 的门禁、quickstart 走查与活工件一致性收口

---

## Notes

- [P] 任务 = 不同文件且不依赖同一阶段内其它未完成任务的产出
- [Story] 标签把任务映射到具体 user story，便于追溯；Setup / Foundational / Polish **不带** [Story]
- 每个 user story 都应能独立完成与独立测试；停止在任何 checkpoint 都可用 quickstart 的场景单独验收
- 规则数值**只在** `KFL.Rules/Config/` 声明（本特性只新增 `BackupIntervalTurns = 3` 与纪元 `(1, 1)`，
  逐条出自规格书 §14/§3；`3` 是整数，**不进** `ConfigLiteralTests.RegisteredValues`）；
  `KFL.Infrastructure` 的存档实现 MUST NOT 出现这两者的副本
- 文件系统访问 MUST 收敛在 `AtomicFileWriter` / `SaveCatalog` / `SaveName`（零）与测试的
  `SaveTestSandbox` 四处以内，逐行 `// arch-guard:allow <理由>`；`KFL.Core` 与 `KFL.Rules` 的
  G-07 违规数 MUST 为 0
- 全部测试 MUST 使用临时目录（`SaveTestSandbox`），MUST NOT 在仓库工作区留下存档产物
- `KFL.Presentation` / `KFL.App` 一行不动；无界面、无成就持久化、无旧格式迁移、无并发/文件锁、
  无写入时间戳
- 避免：含糊任务、同一文件并发改写、破坏故事独立性的跨故事依赖

---

## Phase 7: Convergence（缺口收敛；`/speckit-converge` 审计于 2026-10-12）

> **追加式阶段**：本节由 `/speckit-converge` 对基线 `ef93ed3`（分支 `004-save-system`，审计时工作树干净）
> 的只读审计后追加，MUST NOT 回改 Phase 1~6 的任何既有条目（AGENTS.md：历史记录类工件不回改）。
> 审计对象 = `spec.md` / `plan.md` / `contracts/`（九/十/十一）/ `data-model.md` / `quickstart.md`
> 的全部需求、验收锚点与不变量 × 现有 489 个测试与实现代码。
> 结论：**无 CRITICAL**；17 条有证据的缺口，逐条一个任务（T049~T065）。
> 「已核对且判定无缺口」的关键面登记在文末「审计确证清单」，不产生任务。

**严重度口径**：CRITICAL = 违反章程 MUST 或阻塞 P1 验收；HIGH = 核心 FR / 验收场景缺失或部分未落地；
MEDIUM = 契约细则或次要验收锚点的行为/断言缺口；LOW = 边界断言与活工件登记缺口。
**判定口径**：`missing` = 审计提纲要求的项完全不存在；`partial` = 部分存在但不满足该条；
`contradicts` = 实现/工件与规格书或契约的规定冲突；`needs-confirm` = 需先实测/裁决才能定论。

### 发现登记表

| ID | 判定 | 严重度 | 锚点（需求/契约/SC） | 证据（文件:行） | 任务 |
| --- | --- | --- | --- | --- | --- |
| C-01 | `contradicts` | HIGH | 契约十 §6 第③步（`contracts/save-format.md:111-120`，③在④之前）、FR-014、SC-007 | `src/KFL.Infrastructure/Save/JsonSaveFormat.cs:89-121`（`EnsureVersion` 在 `:94`；`remaining` 在 `:101` 仅被 `:108` 的 `catch (JsonException ex) when (remaining < header.PayloadLength)` 使用）；对照 `src/KFL.Infrastructure/Save/MessagePackSaveFormat.cs:73-78` | T049 |
| C-02 | `missing` | HIGH | SC-002（`spec.md:317-318`）、US1 Independent Test（`spec.md:52-53`）、T029③（`tasks.md:122`）、`tasks.md:315` 已把 SC-002 标给 T029/T030 | 004 特性测试内 grep「SC-002」/「24 个月」/「读回后继续」零命中；`tests/KFL.Tests/Infrastructure/SaveServiceTests.cs:178-180` 的 US1/AS5 用例只接内存 `TurnRunner`、不经磁盘 `Load` | T050 |
| C-03 | `contradicts` | HIGH | FR-022（测试 MUST 用临时目录）、`tasks.md:403` Notes | 实测门禁失败 1/489：`KFL.Tests.Architecture.SolutionShapeTests.globalJson只锁十点零版本带` → `System.UnauthorizedAccessException : Access to the path '…\tests\KFL.Tests\bin\Debug\net10.0\save-tests\6d013381f5794feea970b97cab6e9666' is denied.`（`tests/KFL.Tests/Architecture/RepositoryLocator.cs:35` ← `SolutionShapeTests.cs:26`）；`tests/KFL.Tests/Infrastructure/SaveTestSandbox.cs:52-54` 把沙箱建在 `AppContext.BaseDirectory` 下 | T051 |
| C-04 | `partial` | MEDIUM | 契约九 §7 条款 7（「本次备份/切换中清理」）、FR-011 | `SaveService.Backup` 不经 `RemoveStaleSiblings`（同 SaveId 旧备份残留）；`tests/KFL.Tests/Infrastructure/SaveServiceTests.cs:102-107` 固化「改名时补写旧名备份」 | T052 |
| C-05 | `partial` | MEDIUM | 契约十 §5 条款 3（缺失 MUST 被检出，MUST NOT 静默默认化）、FR-002 | `src/KFL.Infrastructure/Save/MessagePackSaveFormat.cs:142-158` 只校验**顶层**数组 == `SaveStateMemberCount(8)`；`MessagePackSaveFormat.cs:35` 注释宣称位置式数组长度即成员齐备判据，而 Person 22 / StatusTimers 4 / Treasury 6 / Loan 3 / Holdings 4 / LedgerEntry 5 / Famine 2 / Family 6 / FamilyEconomy 7 的长度在 Format / `SaveStateMapper` / `SaveStateValidator` 三层均无检查 | T053 |
| C-06 | `partial` | MEDIUM | SC-010（`spec.md:337-338`）、T030⑤（`tasks.md:123`） | `tests/KFL.Tests/Infrastructure/SaveFormatContractTests.cs:161-174` 只断 `Count == 1200` + 单文件 + 无备份；夹具 `LongLedgerState`（`:179-207`）的 1200 条金额互异、年月递增却未逐条断言；`:157` 注释误写 SC-006 | T054 |
| C-07 | `partial` | MEDIUM | 契约十 §4 键表 / §5 条款 4 / §7「§4 键表逐项」、FR-002 | 键表断言只比对 JSON 属性路径集；tests 全目录 grep「KeyAttribute / GetCustomAttribute / MessagePackObject」零命中 ⇒ 重排 `PersonDto` 的 `[Key(0..21)]` 既不失败也不改变往返 | T055 |
| C-08 | `missing` | MEDIUM | T030③（`tasks.md:123`）、契约十 §3 条款 1、data-model §3.10 | 全仓 grep「非法枚举 / 越界」零命中；`SaveFormatContractTests.cs` 只断言枚举在 JSON 落成名字、二进制落成整数 | T056 |
| C-09 | `contradicts` | MEDIUM | data-model §3.9（未登记状态位 ⇒ `InvalidStructure`） | 二进制：`src/KFL.Infrastructure/Save/SaveStateMapper.cs:141-145` 检 `(status & ~KnownStatusFlags) != None` ⇒ `InvalidStructure`；JSON：`SaveFileFormat.cs` 的 `StatusFlagJsonConverter.Read` 抛 `JsonException`，被 `JsonSaveFormat.cs:154-160` 统一包成 `Malformed` | T057 |
| C-10 | `partial` | MEDIUM | SC-006（`spec.md:330-331`）、SC-003、T037/T038（`tasks.md:153-158`） | `tests/KFL.Tests/Infrastructure/AtomicWriteTests.cs` 注入中断后从不 `Load` 真实存档；T038 的 12 回合用例只比内存状态、不比磁盘内容；「换格式后旧格式备份移除」「写失败保留旧格式」无覆盖 | T058 |
| C-11 | `partial` | MEDIUM | SC-007（`spec.md:331-332`）、契约十 §6 条款 4、T044（`tasks.md:182`） | `tests/KFL.Tests/Infrastructure/SaveFailureTests.cs` 只断言 `Kind`，不比对失败前后的原文件字节与已持有 `GameState`；截断（`:39`）、版本不符（`:97`）、版本闸门（`:116`）、字节损坏（`:136`）四条全部只跑 `SaveFormat.Binary` | T059 |
| C-12 | `missing` | MEDIUM | FR-009、契约九 §6 条款 5 | `SaveService.Rename` 的「备份改名失败」暴露路径无任何用例（tests 内无该注入点） | T060 |
| C-13 | `needs-confirm` | MEDIUM | FR-008（同名判定大小写不敏感）、FR-009 | 仅大小写差异的改名目标（如 `甲档` → `甲档B` 以外的 `A` → `a`）走 `File.Move` 不带 overwrite，NTFS 上是否自冲突未实测、无用例 | T061 |
| C-14 | `missing` | LOW | FR-007/FR-008、T043（`tasks.md:185`） | 大小写在磁盘上未被改写（`SaveServiceTests.cs:158` 只断内存 `slot.Name`）、档名内部空格放行、只有 `backups/` 无主档时 `Exists == false`、JSON `FindBySaveId` 只读头——四小项均无断言 | T062 |
| C-15 | `needs-confirm` | LOW | FR-007（放行集为 255 字符名） | `src/KFL.Infrastructure/Save/SaveName.cs` 无长度上界；临时文件名约 +37 字符，255 字符名在 Windows `MAX_PATH = 260` 下是否超限无实测 | T063 |
| C-16 | `missing` | LOW | spec Edge Cases（`spec.md:168`）、Q8（`spec.md:26`）、FR-003 | 夹具出生年月与功名年份全为 ≥1（`SaveFixtures`/`PlainState` 的 `changedYear` 为 1）；`GameDate`/`GameState` 支持 0 与负年份但无断言 | T064 |
| C-17 | `partial` | LOW | AGENTS.md 铁律第 1 条、`plan.md` 回写义务清单 | (a) FR-022 的「临时目录」落点（`SaveTestSandbox` 建在 `AppContext.BaseDirectory` 下，见 C-03）未在活工件登记；(b) T020/T021 的 `tests/KFL.Tests/Fixtures/SaveFixtures.cs` 与 `SaveTestSandbox.cs` 文件落点未登记进 `plan.md` | T065 |

### HIGH

- [X] T049 [US1] 补齐 JSON 读档的契约十 §6 **第③步独立截断判定**并修正 ③/④ 次序：在 `src/KFL.Infrastructure/Save/JsonSaveFormat.cs` 的 `Read` 中于 `JsonDocument.Parse` **之前**加无条件 `remaining < header.PayloadLength ⇒ Truncated`（现在的 `remaining` 只在 `:108` 的 `catch (JsonException ex) when (…)` 过滤器里生效，合法文本的截断/头 `PayloadLength` 被改大都会**静默读档成功**），并使版本闸门退回第④位（对照 `src/KFL.Infrastructure/Save/MessagePackSaveFormat.cs:73-78`：二进制是「截断检查 → 版本闸门」）——依据 `specs/004-save-system/contracts/save-format.md:111-120`（③`剩余字节 ≥ PayloadLength` 在④`FormatVersion == 1` 之前）与 `:126`（判据 MUST 与 SC-007 四类输入一一对应）。**验收**：把 `tests/KFL.Tests/Infrastructure/SaveFailureTests.cs` 的截断、版本不符、版本闸门三条用例由 `SaveFormat.Binary` 参数化到两种格式后全绿，且新增一条「JSON 正文合法但头 `PayloadLength` 大于实际字节数 ⇒ `Truncated`」用例
- [X] T050 [US1] 补齐 **SC-002** 的落地测试（`spec.md:317-318`）并修正套错的标注：在 `tests/KFL.Tests/Infrastructure/SaveServiceTests.cs` 新增「连续落盘 **24 个月**（每回合结算成功后 `Save` 到同一槽位）→ `Load` 读回 → 读回态与不落盘基线逐字段差异 = 0 → 两路各继续推进 **12 个月**后再次逐字段差异 = 0（用 `SaveStateComparer.AssertEquivalent`）」；同时把 `:178-180` 那条实际只接内存 `TurnRunner` 的用例的「US1 / AS5」注释改正（它验证的是 US2 的「开启自动存档不改变模拟结果」，不是 US1 AS5 的「读回后继续结算」）。**验收**：新增用例在两种 `SaveFormat` 下通过；`tasks.md:315` 的 SC-002 行此后有真实断言支撑（本任务**不回改**该历史表格，仅在提交信息/`implementation-notes.md` 记明）
- [X] T051 修复门禁第四条的非确定性失败（本轮审计实测 488/489，唯一失败即此项）：(a) 在 `tests/KFL.Tests/Architecture/RepositoryLocator.cs:35` 之前排除 `bin`/`obj`（枚举时用 `EnumerationOptions` + 过滤，或先按目录段剪枝，MUST NOT 先枚举后过滤，否则仍会走进 `tests/KFL.Tests/bin/Debug/net10.0/save-tests/<guid>` 并抛 `UnauthorizedAccessException`）；(b) 让 `tests/KFL.Tests/Infrastructure/SaveTestSandbox.cs:52-54` 的沙箱改为 `Path.GetTempPath()` 下的一次性目录（FR-022「临时目录」；现落在 `AppContext.BaseDirectory` 下，且残留目录的 ACL 带 `Everyone Deny DeleteSubdirectoriesAndFiles`、只剩 GUID 空壳，删残留后即 489/489 连续绿 11 次）。**验收**：`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false` 连续 **5 次** 489/489 全绿，且 `tests/KFL.Tests/bin/**` 下不再出现 `save-tests/` 残留；仓库工作区无存档产物（SC-009）

### MEDIUM

- [X] T052 [US3] 收口 `Backup()` 的同 SaveId 清理口径：`src/KFL.Infrastructure/Save/SaveService.cs` 的 `Backup` 目前不经同名的 `RemoveStaleSiblings` 清理点，与契约九 §7 条款 7「本次备份/切换中清理」不一致；先按 `specs/004-save-system/contracts/save-service.md` 该条原文确认「清理是 `Save` 的专属义务还是两入口共有」，再让 `Backup` 复用同一清理点（`ConcurrentDictionary` 内的槽位/活文件登记 MUST NOT 被绕过）。**验收**：`tests/KFL.Tests/Infrastructure/SaveServiceTests.cs:102-107`（改名补写旧名备份那条）按新口径调整并全绿，新增「同 SaveId 连做两次 `Backup` 后 `backups/` 恒 1 份」用例（SC-003）
- [X] T053 [US1] 补二进制嵌套位置式数组长度的缺失检出：`src/KFL.Infrastructure/Save/MessagePackSaveFormat.cs:142-158` 的 `EnsurePayloadShape` 只校验顶层 8 成员，须按契约十 §5 条款 3 延伸到嵌套（Person 22 / StatusTimers 4 / Treasury 6 / Loan 3 / Holdings 4 / LedgerEntry 5 / Famine 2 / Family 6 / FamilyEconomy 7），或在 `:35` 注释处改口径并给出等价检出——**先实测** MessagePack-CSharp `2.5.305` 对短于 maxKey 的位置式数组是否抛异常（不抛即静默默认化，属 MUST NOT）。**验收**：新增「手工截短的嵌套数组」用例断言 `InvalidStructure`；若库本身抛异常，则该用例把库行为钉死并同步 `:35` 注释
- [X] T054 [US1] 补 **SC-010** 的账本逐条断言：`tests/KFL.Tests/Infrastructure/SaveFormatContractTests.cs:161-174` 现只断 `Count == 1200`；按 T030⑤「条目顺序一致」（`tasks.md:123`）对返回的 `loaded.Economy.Ledger.Entries` 逐条比对 `(date, category, personId, amountWen)`（夹具 `LongLedgerState`（`:179-207`）的 1200 条金额互异、年月递增，已具备逐条断言条件），并把 `:157` 注释误写的 SC-006 改为 SC-010。**验收**：两种格式各一条用例全绿，且「顺序被反转/条目被替换」时断言必然失败
- [X] T055 [US1] 补二进制 `[Key(n)]` 键序的反射断言（契约十 §4 键表 + §5 条款 4）：新增用例反射遍历每个 DTO 的 `[Key]`，断言其集合恰为 `0..N-1` 且与 `specs/004-save-system/contracts/save-format.md` §4 的编号一一对应（现有 JSON 键名断言**漏掉**这一层：重排 `[Key]` 既不改变 JSON 键名、也不改变读写对称的往返结果）。**验收**：故意交换 `PersonDto` 两个 `[Key]` 后该断言失败
- [X] T056 [US1] 补 T030③「非法枚举名 / 越界整数被拒」的两条用例：JSON 侧非法枚举**名**（`JsonStringEnumConverter` 默认也接受数字，须确认 Core 校验是否兜住）与二进制侧越界整数，断言 `Kind`（契约十 §3 条款 1、data-model §3.10、`tasks.md:123`）。**验收**：两条用例均断言失败形态为 `InvalidStructure`（若实测为 `Malformed`，则先按 C-09 的口径裁决再落断言）
- [X] T057 [US1] 统一**未登记状态位名**在 JSON 路径的失败分类：data-model §3.9 要求未登记位 ⇒ `InvalidStructure`（二进制的 `src/KFL.Infrastructure/Save/SaveStateMapper.cs:141-145` 已如此），而 JSON 由 `SaveFileFormat.cs` 的 `StatusFlagJsonConverter.Read` 抛 `JsonException`、被 `JsonSaveFormat.cs:154-160` 包成 `Malformed`。二选一并同步：让 converter 抛可被识别为 `InvalidStructure` 的异常（改代码），或先在 `specs/004-save-system/data-model.md` §3.9 / `contracts/save-format.md` 明确「未登记**名字** ⇒ `Malformed`、未登记**位** ⇒ `InvalidStructure`」（改工件，涉数值/口径者按规格书 §17 裁决），两种格式的对照断言随后补齐。**验收**：同一输入在两格式下的 `Kind` 有明文口径且被用例固定
- [X] T058 [US2] 收口 US2 的三处测试空白：(a) `tests/KFL.Tests/Infrastructure/AtomicWriteTests.cs` 注入中断后 MUST 用真实 `Load` 读回存档并断言「要么旧、要么新」且始终可读（SC-006 现只验字节形态）；(b) T038 的 12 回合用例 MUST 同时比对磁盘内容与内存状态（现只比内存）；(c) 新增「换格式后旧格式备份被移除」「写失败时旧格式归档保留」两条（SC-003/FR-011 末句）。**验收**：三条全绿，且 (a) 在「注入后把文件截断」的变异下失败
- [X] T059 [US1][US3] 补 **SC-007** 的「原文件与内存差异 = 0」断言并参数化失败输入：`tests/KFL.Tests/Infrastructure/SaveFailureTests.cs` 的每条失败用例加「调用前后**原文件字节完全相同**」与「调用方已持有的 `GameState` 逐字段未被污染」；把截断（`:39`）、版本不符（`:97`）、版本闸门（`:116`）、字节损坏（`:136`）四条参数化到两种 `SaveFormat`（契约十 §6 条款 4）。**验收**：四类输入 × 两格式全绿；任一失败路径被改为「先删原文件再报错」或用例可被断言捕获
- [X] T060 [US3] 补 Rename 的备份改名失败暴露路径：按 FR-009 与契约九 §6 条款 5 注入「备份改名失败」（`src/KFL.Infrastructure/Save/SaveService.cs` 的 `Rename`），断言失败可识别（`SaveNameConflictException` 或契约九该条规定的形态）、原档与旧备份均未被破坏。**验收**：注入点可用且有断言；无注入点时先补测试接缝（属测试侧夹具，不改生产签名）
- [X] T061 [US3] 确认并固定「仅大小写差异的改名目标」行为：实测 `src/KFL.Infrastructure/Save/SaveService.cs` 的 `Rename` 在 NTFS 上把 `A` 改名为 `a` 时 `File.Move`（不带 overwrite）是否自冲突；据结果补用例并在实现里走大小写不敏感的同一槽位判定（FR-008 的口径）。**验收**：该场景有确定行为与用例；若无歧义则记录实测结论到 `implementation-notes.md`

### LOW

- [X] T062 [US3] 补 US3 的零散断言（四小项，均落在 `tests/KFL.Tests/Infrastructure/SaveServiceTests.cs` 或同目录新测试类）：(a) 档名大小写在**磁盘上**未被改写（现 `:158` 只断内存 `slot.Name`）；(b) 档名**内部空格**放行；(c) 只有 `backups/` 而无主档时 `Exists == false`；(d) JSON 档的 `FindBySaveId` 只读头、不解析正文。**验收**：四条全绿
- [X] T063 [US3] 确认并收口档名长度上界：`src/KFL.Infrastructure/Save/SaveName.cs` 允许 255 字符名，而原子写的临时文件名在档名基础上再增约 37 字符（`MAX_PATH = 260` 风险）。**验收**：先实测 255 字符名在原子写路径下是否可用；若超限，则在 `SaveName` 落下与实测一致的长度上界（涉数值者经规格书 §17 裁决后回写 `spec.md`/契约十）或改为不依赖档名长度构造临时名，并补边界用例
- [X] T064 [US1] 补 Edge Case「成员年月含 0 / 负年份」的往返覆盖（`spec.md:168`、Q8 `spec.md:26`）：夹具（`tests/KFL.Tests/Infrastructure/SaveFixtures.cs`）加一名 `birthYear ≤ 0` 的成员或一条 `changedYear ≤ 0` 的功名记录，纳入两格式逐字段往返（FR-003）。**验收**：`GameDate` 的 0 与负年份在两格式下逐字段相等
- [X] T065 补两处活工件登记（AGENTS.md 铁律第 1 条）：在 `specs/004-save-system/plan.md` 的回写义务清单中登记 (a) FR-022「临时目录」的实际落点（与 T051 的 `SaveTestSandbox` 改法同一提交）与 (b) `tests/KFL.Tests/Fixtures/SaveFixtures.cs`（T020）与 `tests/KFL.Tests/Infrastructure/SaveTestSandbox.cs`（T021）两处文件落点。**MUST NOT** 回改 `tasks.md` 既有条目与 `implementation-notes.md`。**验收**：`plan.md` 的清单能逐条对上现实现文件

### Phase 7 执行约束

- **同文件串行（不可并行）**：`src/KFL.Infrastructure/Save/JsonSaveFormat.cs`（T049）；`src/KFL.Infrastructure/Save/SaveService.cs`（T052 → T060 → T061）；`tests/KFL.Tests/Infrastructure/SaveServiceTests.cs`（T050 → T052 → T060 → T061 → T062）；`tests/KFL.Tests/Infrastructure/SaveFailureTests.cs`（T049 → T056 → T057 → T059）；`tests/KFL.Tests/Infrastructure/SaveFormatContractTests.cs`（T053 → T054 → T055 → T056）
- **[P] 可用**：T051（架构/沙箱）与 T058、T064 触文件不重叠，可与上列任一线并行
- **顺序**：HIGH（T049~T051）→ MEDIUM（T052~T061）→ LOW（T062~T065）；每条完成即跑四条门禁（`tasks.md:42-51`），T051 未完成前门禁结果不可信
- **回写**：T057、T063、T065 若判定为「口径需变更」，MUST 先经规格书 §17 裁决并**在同一提交**内回写受影响活工件（`spec.md`/`plan.md`/`contracts/`）；T057 的代码侧改动须与 `data-model.md` §3.9 的措辞一致

### 审计确证清单（已核对，无缺口，不产生任务）

- 12 个 DTO 的 JSON 键表与契约十 §4 逐字段一致（76 个线上成员；`[Key]` 自 0 连续、`[JsonPropertyName]` + `[JsonRequired]` 齐全；属性改名不改键名）；`SalaryMode` / `TreasuryPool` 未泄入任何键表
- 复原次序符合 data-model §3.11；五类 `Kind` 互不相同且各有触发路径；两种格式的版本闸门都在**主体解析之前**（次序问题仅 C-01 的 ③/④）
- `SaveOptions.DefaultFormat` 被 pin 属契约十 §4 的明文要求（缩面 R-14⑤），判为**合规**而非死形
- 回写义务登记表 12 行、架构门禁 G-01~G-08、`.slnx` 恰好六工程、`ConfigLiteralTests.RegisteredValues` 未增补小数、`// arch-guard:allow` 豁免行可逐一列举——均已落地
- 四类失败分类（Missing/Malformed/Truncated/UnsupportedVersion）在**二进制**侧逐条可识别；`SaveName` 非法字符集与 `-` 放行、同名判定大小写不敏感在 `Exists`/覆盖冲突/改名目标三处口径一致

---

## Phase 7 回写范围登记（实现期追加；AGENTS.md 铁律第 3 条）

> 本节只登记「同步了哪些文件、同步到哪里」，供后续审计核对；**MUST NOT** 回改本文件 Phase 1~6 的既有条目、
> `implementation-notes.md` 与既有 `checklists/`（历史记录类工件）。T050 原计划把 SC-002 的标注更正记入
> 提交信息或 `implementation-notes.md`，实现期改记于本节（该文件属历史记录类工件，不得回改）。

| 任务 | 活工件同步 | 同步到哪里 |
| --- | --- | --- |
| T049 | 无（契约十 §6 第③/④步原文未变，是**实现向契约对齐**，非契约变更） | `src/KFL.Infrastructure/Save/JsonSaveFormat.cs`（③无条件截断判定 ⇒ ④版本闸门）；`tests/KFL.Tests/Infrastructure/SaveFailureTests.cs` |
| T050 | 无（SC-002 的锚点文本本身未变，缺的是断言） | `tests/KFL.Tests/Infrastructure/SaveServiceTests.cs`（新增 24+12 个月长跑；`:178` 注释 US1/AS5 → US2/AS5） |
| T051 | `plan.md` 回写义务清单**第 13 行**（FR-022「临时目录」实际落点） | `specs/004-save-system/plan.md` 的「回写义务清单（AGENTS.md「铁律」+ spec Assumptions 的落地）」表 |
| T056 | 无（`EnsureEnumsDefined` 是**补足** data-model §3.10 已有的「越界整数 ⇒ 明确失败」义务，非口径变更） | `src/KFL.Infrastructure/Save/Serialization/SaveStateMapper.cs`（`Build` 的 ⓪ 步）；`tests/KFL.Tests/Infrastructure/SaveFailureTests.cs` |
| T057 | `data-model.md` §3.9（L224）、§3.10 枚举行（L234）；`contracts/save-format.md` §6 **新条款 6** | 「JSON 非法枚举**名** ⇒ 第⑤步 ⇒ `Malformed`；二进制越界整数 / 未登记状态位 ⇒ 第⑦步 ⇒ `InvalidStructure`」。**未改任何数值或步骤序列**，故不经规格书 §17 裁决（只把契约十 §6 的步号映射写到明文） |
| T063 | 无（不把实测可写长度写进 `SaveName`——它是**根路径**的函数，随部署路径变化） | `src/KFL.Infrastructure/Save/AtomicFileWriter.cs`（临时名长度恒定：档名前 16 字符 + 随机后缀）；`tests/KFL.Tests/Infrastructure/SaveServiceTests.cs` 边界用例 |
| T064 | 无（`SaveFixtures` 为测试夹具，非需求工件） | `tests/KFL.Tests/Infrastructure/SaveFixtures.cs`（新增前史纪年成员 + 两条 `changedYear ≤ 0` 的功名记录） |
| T045 遗留 | `plan.md` 回写义务清单**第 12 行**（结清结构树 L238 引用的「第 12 行」） | `specs/004-save-system/plan.md`：`src/KFL.Core/Entities/FamineState.cs` 的 `Stage`/`ElapsedMonths` 由 `private set` 放宽为 `internal set` |
| T065 | `plan.md` 回写义务清单第 13 行（(a) FR-022 落点、(b) `SaveFixtures.cs`（T020）与 `SaveTestSandbox.cs`（T021）的文件落点） | `specs/004-save-system/plan.md`；两处测试文件实际都在 `tests/KFL.Tests/Infrastructure/`（见 plan.md「结构树补注（T048）」） |

**实测结论留档（T061 / T063 的 needs-confirm 项）**：

- T061：NTFS + .NET 10 下，同目录「不带 overwrite 的移动」对**仅大小写差异**的改名（`甲A档` → `甲a档`）**能成功**
  （Windows 视作同一对象的改名，不报 already exists）⇒ `SaveService.Rename` 无需特判；结论落在
  `tests/KFL.Tests/Infrastructure/SaveServiceTests.cs` 该用例的 doc 注释（`implementation-notes.md` 属历史记录类工件，不回改）。
- T063：`LongPathsEnabled = 1`，但 testhost 不受长路径政策保护；255 字符档名在原子写路径上**撞 `MAX_PATH`**
  （主档全路径 372 字符 ⇒ 原子改名处抛 `IOException`）⇒ 255 只是**校验层**上界，「写得下多少」由**根路径**长度决定；
  收口办法 = 临时名长度恒定（+54 字符，与档名长度无关），边界用例按 `259 - 根长 - 扩展名` 的动态预算取名。

---

## 审核轮回写范围登记（实现期追加；AGENTS.md 铁律第 3 条）

> 本节登记 **PR #10 首轮审核（对象 head `df92ce0`）** 之后的回写范围，同样**只登记**「同步了哪些文件、同步到哪里」；
> **MUST NOT** 回改本文件 Phase 1~6 与 Phase 7 的既有条目、`implementation-notes.md` 与既有 `checklists/`。

**审核项「一」——跨特性形状变更的同步（6 处 `internal` 面，一份清单）**：

| 目标工件（活工件） | 同步到哪里 |
| --- | --- |
| `specs/001-core-skeleton/data-model.md` | §2.2 `Family` 变更入口表新增 `CaptureOutsiderBookkeeping()` / `RestoreOutsiderBookkeeping(...)` 两行（:216-217）；新增 §2.4 `OutsiderBookkeeping` 小节（:282-291） |
| `specs/001-core-skeleton/contracts/injection-seams.md` | §4 新增第 4 条「程序集级接缝（004 追加）」（:78-89）：仅 `KFL.Infrastructure`、MUST NOT 扩大（尤其 `tests/KFL.Tests`）、MUST NOT 因此新增公开写入通道 |
| `specs/002-monthly-settlement-economy/data-model.md` | §3.5 `FamineState.Stage` / `ElapsedMonths` 标 `{ get; internal set; }` 与不变量「强制点」两条（:196-204）、「写入通道（004 补充）」（:214-217）；§3.6 新增 `RestoreFamine(FamineStage, int)` 行（:244） |
| `specs/004-save-system/spec.md` | 新增顶层条目「本特性对上游 `KFL.Core` 的形状变更（6 处，全部是 `internal` 面）」（:385-401）与「除上述 6 处外零形状变更」（:402-403）；Q4~Q8 段括注改为「这五条裁决本身未增删 `KFL.Core` 成员」（:379-383）；Assumptions 前言改事实口径并追加第 7 项回写落点（:451-455、:472-477） |
| `specs/004-save-system/plan.md` | 「无跨特性形状变更」→「（公开面）」+ 新增「回写补注（T065，收敛轮）」块列 6 处（:120、:129-138）；Q4~Q8 小结句与「先说结论」精确化（:180-183、:190-199）；回写义务清单第 12 行扩为 `KFL.Core` 的 `internal` 面 6 处（:214） |
| `specs/004-save-system/research.md` | 开篇「前置事实」改为「公开面零形状变更 + `internal` 面 6 处」（:9-16）；Q4~Q8 表下补注同步（:588-590） |

**审核项「2.x」——本轮修复的实现与验证落点（无工件口径变更）**：`src/KFL.Infrastructure/Save/Serialization/{MessagePackSaveFormat.cs,JsonSaveFormat.cs,SaveStateMapper.cs}`、`src/KFL.Infrastructure/Save/{AtomicFileWriter.cs,SaveCatalog.cs,SaveService.cs}`、`tests/KFL.Tests/Infrastructure/{SaveFailureTests.cs,SaveStateValidatorTests.cs（新增）,SaveFormatContractTests.cs,AtomicWriteTests.cs,SaveServiceTests.cs,SaveStateComparer.cs,SaveTestSandbox.cs}`；`contracts/save-format.md` §2（JSON 头次序与重复成员）与 `save-service.md` §4.1（豁免行预算复核）**文本未改**——本轮只是把实现拉回契约既有要求。

**有意不回改**（历史记录类工件）：本文件 Phase 1~6 / Phase 7 既有条目、`specs/002-monthly-settlement-economy/implementation-notes.md`、各 `checklists/`、`specs/004-save-system/contracts/save-cadence.md:10`（未插入任何上游步骤）、`specs/001-core-skeleton/spec.md:7`（前瞻性授权句），以及描述**被否决替代方案**的「跨特性形状变更」字样（`research.md:375/386/404`、`plan.md:337/349`、002 `data-model.md` §3.6 不变量段）。
