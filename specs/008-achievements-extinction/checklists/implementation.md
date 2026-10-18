# 实现期核验清单（008-achievements-extinction）

> **工件类别**：**活工件**（实现期核验结果，随 008 的实现产生）。
> 与 `checklists/requirements.md`（需求期清单，**历史记录类工件，MUST NOT 回改**）分开存放：
> 本文件记录 T083 / T084 / T085 三项「登记落地」的核对结果，供审计对照。
>
> 结论一律以**实现后的仓库状态**为准；每条都给出可复跑的核对方式。

---

## T083 — SC-013：既有「版本断言」的登记落地

**要求**：逐条列出「既有存档测试的版本断言」的核对结果——**0 处需改**。

**核对结论：0 处需改。** 既有版本断言一律经常量取得，未写死数值，故 `SaveFileFormat.Version`
由 `4` 升为 `5` 后无需改动任何期望值：

- [x] `tests/KFL.Tests/Infrastructure/SaveFailureTests.cs:23` 的注释由「版本不是 4」同步为
      「版本不是 5」（**注释**同步，非期望值）；其用例的期望版本仍取自 `SaveFileFormat.Version`。
- [x] `tests/KFL.Tests/Infrastructure/SaveFormatContractTests.cs` 的版本闸门用例（`Asserts` 中的
      `FormatVersion` 取值）取自常量；本次**只**同步了**键表**（`SaveStateDto` 8 → 10）与
      **成员数常量登记**（新增 5 条），未改任何版本期望值。
- [x] `src/KFL.Infrastructure/Save/SaveFileHeader.cs:21` 的文档注释由「本版本恒为 3」同步为
      「本版本恒为 5」（文档，非断言）。
- [x] `grep "头记录恒为 3 个成员"` 的唯一命中是**头记录成员数**（恒 3），与文件格式版本无关，
      故未改动。

**可复跑**：`Select-String -Path tests\KFL.Tests\Infrastructure\*.cs -Pattern "FormatVersion|Version"`
应只见到常量引用与注释，见不到裸数值 `4`（`SaveFileFormat.Version` 除外）。

---

## T084 — SC-014：持久字段**恰 8** 项的审查落地

**要求**：`AchievementState` 的持久字段**恰 8** 项（== FR-013 的列项数），且 MUST NOT 出现第九项。

**核对结论：恰 8 项，0 处多余。** 公开实例属性全集（按名排序）：

| # | 字段 | 类型 | 写通道 |
| --- | --- | --- | --- |
| ① | `SuccessfulBribeCount` | `int` | `RecordSuccessfulBribes(int)` |
| ② | `BirthCount` | `int` | `RecordBirths(int)` |
| ③ | `Lifespans` | `IReadOnlyList<int>` | `AppendLifespan(int)` |
| ④ | `PeakTotalAssets` | `Money` | `ObserveTotalAssets(Money)` |
| ⑤ | `EverHadLoan` | `bool` | `RecordLoanOriginated()` |
| ⑥ | `DebugWakeCount` | `int` | `ReportDebugWake(int)` |
| ⑦ | `Completed` | `IReadOnlyList<AchievementMark>` | `MarkCompleted` / `MarkCompletedAndEnqueue` |
| ⑧ | `PendingUnlocks` | `IReadOnlyList<PendingUnlock>` | `MarkCompletedAndEnqueue` / `RestorePendingUnlocks` |

- [x] 8 项与 `spec.md` FR-013 的列项**逐项对应**，无第九项（`specs/008-achievements-extinction/data-model.md` §2.4）。
- [x] 断言落地：
      `tests/KFL.Tests/Infrastructure/AchievementSaveFormatTests.cs` 的
      `档内成就持久字段不超过八个`（反射取公开属性，排序后逐名比对）。
- [x] 「别处没有第二真源」的断言落地：
      `tests/KFL.Tests/Rules/AchievementCounterTests.cs` 的 `六项累计量只由成就状态持有`
      （反射断言 `Person` / `Family` 上没有同名成员）。
- [x] 存档侧：`AchievementStateDto` **恰 8 键**（`MessagePackSaveFormat.AchievementStateMemberCount = 8`、
      JSON 侧键表同长），与领域侧 8 项一一对应。
- [x] 派生量（`TotalJinShi`、`HighestDegree`、`AverageLifespan`、`Generations` 等）**不**落档，
      只存在于 `ChronicleSnapshot`（绝嗣时冻结），故不构成第九项。

**可复跑**：`dotnet test … --filter "FullyQualifiedName~档内成就持久字段不超过八个|FullyQualifiedName~六项累计量只由成就状态持有"`。

---

## T085 — SC-015 / FR-047：「0 处放宽既有期望值」的核对落地

**要求**：本特性 **0 处**修改既有测试的**期望值**来迁就新行为。

**核对结论：0 处放宽。** 对既有测试的改动**全部**属于下列三类之一，逐条登记：

| # | 文件 | 改动 | 类别 | 为什么不违反 SC-015 |
| --- | --- | --- | --- | --- |
| 1 | `tests/KFL.Tests/Infrastructure/GameStateTests.cs:110` | `公开属性全集恰为七个` → `恰为九个`，期望数组补 `Achievements` / `Ending` | **形状同步**（T057 明令） | `GameState` 按 008 的授权形状**确实**新增了两个聚合属性；断言的是「全集」，不是某个行为值。若不放宽则该断言恒红，且它不构成对任何既有行为的期望 |
| 2 | `tests/KFL.Tests/Infrastructure/SaveFormatContractTests.cs` | 主体数组头 `8` → `10`；`SaveStateDto` 键表补 `achievements` / `ending`；登记 5 条成员数常量 | **键表同步**（T057 明令） | 同上：承载的是**线上形状**，形状变更**有授权**（004 契约已同提交回写） |
| 3 | `tests/KFL.Tests/Rules/DeterminismTests.cs:251` | 随机消费次数断言改为**相对**口径（两遍互比） | **口径改写法**（T057 明令） | 008 在动作路径上**新增**了即时判定（不消费随机），绝对次数不再稳定；相对口径**更严**（要求两遍逐月一致），不是放宽 |
| 4 | `tests/KFL.Tests/Rules/BribeQueryTests.cs` | 「全程序集无 `Achievement*` 类型」的探针收窄为「`KFL.Rules.Punishment` 命名空间内无成就类型」 | **代理断言收窄**（`plan.md` 勘误 **E-22**） | 006 的该断言是「006 不解锁成就」的**代理**；008 的**合法**新类型使代理失真。收窄后仍守卫原意图（`Punishment` 命名空间不自造成就逻辑），且 006 的**行为**期望值一处未动 |
| 5 | `tests/KFL.Tests/Infrastructure/SaveStateComparer.cs` | 追加 `AssertAchievements` / `AssertEnding` | **纯增量** | 只新增断言，未改既有比较逻辑与期望 |

- [x] **MUST NOT** 出现「把某条既有断言的值改小/改松以使红灯转绿」的改动：上述 5 条**逐条**可核。
- [x] 既有断言中与 008 无关者（004/005/006/007 的行为期望值）**一处未动**，全量测试回归证明见 T086。
- [x] 新增断言覆盖率：008 的 15 个新测试文件（`Rules/` 10 + `Infrastructure/` 4 + `Architecture/` 1）。

**可复跑**：`git diff 38e85ba -- tests/KFL.Tests` 应只含上述 5 类改动 + 15 个新文件。

---

## 收敛补充 T089~T097 — 落地核对

**要求**：收敛任务（`tasks.md` 的「收敛补充」小节）逐条落地，且**不得**回改历史条目。
逐条结论与锚点如下（均以**实现后的仓库状态**为准）。

| 任务 | 落地结论 | 锚点 |
| --- | --- | --- |
| **T089** | 编年史 ④ 取 **全量** `Family.Members`（含已亡与外嫁归档）`person.CurrentDegree` 的最大值，空族谱取 `DegreeLevel.BaiShen`；全仓**无** `Person.DegreeLevel`（该属性不存在） | `src/KFL.Rules/Achievements/ChronicleBuilder.cs:34`（`state.Family.Members`）/`:51-53`（`person.CurrentDegree` 比较）/`:71`（赋值） |
| **T090** | `Advance` 的 ② 已插入：`Settle` → ② `AchievementAdjudicator.Judge(state, settlement, archiveMarks, archive.Origins)`（全局面以**只读参数**取自 `IAchievementStore.Read()`）→ ③ `Save` → ④ `Backup` → ⑤ 全局落盘；② 与 ⑤ 在**同一次** `Advance` 内可见 | `src/KFL.Rules/Save/TurnRunner.cs:125-132`（读档案 + ② 判定）/`:156`（⑤ 结果挂到 `TurnOutcome`） |
| **T091** | T087 的**变红对象**（执行时更正）＝ `ConfigLiteralTests`（依据**章程「技术栈与工程约束」**）与 `EnvironmentDependencyTests`（**G-07**）；与 **SC-008**（绝嗣四条口径，由 T059 承担）**无关**。见下方「T087 变红确认」节 | `tests/KFL.Tests/Architecture/{ConfigLiteralTests,EnvironmentDependencyTests}.cs` |
| **T092** | 系列计数取 `12/8/11/17/11/4/5`（Emerging/ExamProgression/OfficialCareer/Lineage/CollectiveRuin/Famine/DebugSecrets）；`ACH-044~048 ∈ Lineage`、`ACH-049~059 ∈ CollectiveRuin`；T092 授权的**执行前置更正**已就地落地：T005 的计数不变量与 T029 的系列计数改作规范值并就地标注「T092 订正」，E-16 勘误行保留为登记文本 | `src/KFL.Rules/Achievements/AchievementCatalog.cs:85-100`；`tests/KFL.Tests/Rules/AchievementCatalogTests.cs` 的系列计数用例 |
| **T093** | `ChronicleSnapshot.HighestDegree` 的类型仍是 `DegreeLevel`，取值口径按 T089（属性名 `Person.CurrentDegree`，非 `DegreeLevel`） | `src/KFL.Core/ValueObjects/ChronicleSnapshot.cs`（成员类型）+ `ChronicleBuilder.cs:51-53` |
| **T094** | `ISaveService.Load` 的次序注释由「十五条一致性校验」收口为「**二十二条**」，并列出 008 追加的第 16~22 条 | `src/KFL.Infrastructure/Abstractions/ISaveService.cs:92-97` |
| **T095** | T081 的目标文件路径补全为 `specs/007-family-lifecycle/data-model.md`（`:376-390` / `:429-467`）；回写见 `plan.md` 回写义务清单第 ⑦ 项 | `specs/007-family-lifecycle/data-model.md`；`specs/008-achievements-extinction/plan.md` |
| **T096** | FR-031 的显式守卫落地为**可执行断言**：`KFL.Presentation` 与 `KFL.App` 两层源码 MUST NOT 出现成就相关标识符；「**MUST NOT 改动 `KFL.Presentation`**」一项因历史条目不可回改，改在本清单登记（T044 的文本位置 = 其工程形状零改动义务） | `tests/KFL.Tests/Architecture/AchievementPendingWiringTests.cs` 的 `界面两层源码不得出现成就相关标识符` |
| **T097** | **Edge Case 1**（同月成就与绝嗣的次序）与 **Assumption 19**（先成就、后绝嗣；编年史在绝嗣成立时冻结）的次序义务有**逐字落点**：同月同一 `Judge` 调用内断言「绝嗣补判发生在 68 条判定**之后**」；任务文本的补引因历史条目不可回改，改在本清单登记 | `tests/KFL.Tests/Rules/AchievementAdjudicatorTests.cs:125`（`绝嗣在同月的成就判定之后补判`）；`tests/KFL.Tests/Rules/ExtinctionTests.cs:128/141`（普通/地狱难度的补判用例） |

**可复跑**：`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false --filter "FullyQualifiedName~界面两层源码不得出现成就相关标识符|FullyQualifiedName~绝嗣在同月的成就判定之后补判"`。

---

## T086~T088 — 四门禁、守卫变红确认、`spec.md` 真源核对

### T086 门禁四条（2026-10-18 实跑）

| # | 命令 | 结果 |
| --- | --- | --- |
| 1 | `dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false` | **已成功生成，0 个警告，0 个错误**（六个工程） |
| 2 | `dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false` | 总计 **1286**，通过 **1279**，失败 **7** |
| 3 | `dotnet sln KejuFuShengLu.slnx list` | 恰好**六个**工程（App/Core/Infrastructure/Presentation/Rules/Tests） |
| 4 | `Get-ChildItem -Recurse -Filter *.sln \| Where-Object { $_.FullName -notmatch '\\\.git\\' }` | **无输出** |

**关于失败的 7 条（属仓库既有状态，非本特性引入）**：`TargetFrameworkTests` ×2、`DependencyDirectionTests` ×3、
`SolutionShapeTests.仓库恰有一个slnx且无sln共存`、`GuardSelfTests.用例六真实仓库零违规`。根因是仓库里存在
**另一个特性的嵌套工作树** `.agents\worktrees\009-debug-console`（`git worktree list` 可见，`status --short`
为 clean、经 `.gitignore:10` 的 `.agents/*` 忽略；其中含另一份 `KejuFuShengLu.slnx`，故 G-01 报
「实际发现：`agents\worktrees\009-debug-console\KejuFuShengLu.slnx`、`KejuFuShengLu.slnx`」，
`TargetFrameworkTests.cs:61` 的 `ProjectText` 亦随之抛
`InvalidOperationException : Sequence contains more than one matching element`）。
**本特性交付前的基线同样是 7 条失败**（基线：982 总计 / 975 通过 / 7 失败）⇒ 008 **零新增失败**。
按「MUST NOT 动别特性的工作树」的约定，未做任何清理。

### T087 守卫变红确认（反证测试）

- **先按原文试**「临时移除一项配置登记」（删 `RegisteredValues` 的 `0.1m,`）⇒ **仍 7/7 全绿**：
  `ConfigLiteralRules.Evaluate` 是**单向**判据（只报登记值出现在 `Config\` 之外的行），
  删登记项只缩小检查面。已登记勘误 **E-25**（`plan.md`）。
- **正确的探针一**：临时摘掉 `tests\KFL.Tests\Rules\AchievementTriggerTests.cs:179` 的行级豁免注释
  （让既有的 `0.1` 撞车复现）⇒ `ConfigLiteralTests` **红**（失败 1 / 通过 6，报文
  `SC-008 违规 2 处（清单 58 项，扫描 249 个文件）`，命中 `:179` 与 `:365`）。
- **探针二**：在 `src\KFL.Rules\Config\AchievementPolicy.cs` 末尾临时引入 `DateTime.Now` ⇒
  `EnvironmentDependencyTests` **红**（失败 2 / 通过 1，报文逐字为
  `[G-07] src\KFL.Rules\Config\AchievementPolicy.cs:168: 禁用 token「DateTime.Now」…`）。
- 两条探针**均逐字节还原**（还原后 SHA256 与探针前一致），仓库**零污染**。

### T088 `spec.md` 真源核对（FR-040）

- 逐条核对后**仅一处**实现与真源偏离：ACH-035 `秦晋之好` 的判据面。007 的 ⑧-a
  （`src/KFL.Rules/Family/PopulationAdvance.cs:43-45`）只 `with` 了
  `PendingProposal` / `Births` / `Illnesses` / `Crossings` / `Deaths`，**从不填 `LifecycleSnapshot.Marriages`**
  ⇒ 原口径「当月 `MarriageFact.IsDegreeMarriage`」**永不可达**（触发器 8 的即时判定 `Settlement == null`，同样不可达）。
- 处置：按 FR-008「判定 MUST 同时消费**状态面**与当月报告面」补**状态面兜底**
  （台账出现 `DegreeMarriageFeePaid`/`DegreeMarriageDowryReceived` 即算达成），**同一提交**回写
  `spec.md:500` 判据总表行、契约二十二 §2 该行 + §5、007 `data-model.md:277` 消费注，
  并在 `plan.md` 登记勘误 **E-24**；「恢复当月面严格口径」登记为**待接线候选**（须 007 补记婚配事实）。
- 其余核对项（系列计数、存续年数严格/含等口径、`SC-008` 的正确用法在 `spec.md:592` 指「绝嗣四条口径」、
  `DegreeLevel` 无 `None` 等）**均与实现一致**，无需回写。
- **未发现**「只存在于代码而任何 `spec.md`/`plan.md`/契约都未声明」的偏离 ⇒ 无需追加收敛任务。
