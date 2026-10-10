# 003 实施记录（归档）

**来源**：本文件由 `$speckit-implement` 执行 `specs/003-opening-assets-officialdom/tasks.md` 时创建。
实施日期：2026-10-08。**本文件不承载任何需求**：需求的唯一真源仍是
[spec.md](./spec.md) / [plan.md](./plan.md) / [data-model.md](./data-model.md) / [contracts/](./contracts/)；
下面记的是「实现出来的东西与规格之间的差额」、四条门禁的实测输出，以及契约条文与代码的一处
**不可能成立的写法**（必须在下一阶段开工前知道）。

---

## 1. 四条门禁的实测输出

| 门禁 | 开工基线（T001） | 完工实测（T043） |
| --- | --- | --- |
| `dotnet build KejuFuShengLu.slnx -m:1 -nodeReuse:false` | **0 警告 0 错误** | **0 警告 0 错误** |
| `dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false -p:_MSTestEnableParentProcessQuery=false` | **320 项全部通过、0 skipped** | **409 项全部通过、0 skipped** |
| `dotnet sln KejuFuShengLu.slnx list` | 恰好**六个**工程 | 恰好**六个**工程 |
| 递归 `*.sln`（排除 `.git`） | **无输出** | **无输出** |

补充证据：

| 项 | 实测输出 |
| --- | --- |
| `--filter "FullyQualifiedName~Architecture"` | **38 项全绿**（G-01~G-08 + SC-008 的数值清单与金额字面量判据） |
| `git status --short src/KFL.Presentation src/KFL.App` | **无输出**（FR-026：界面两层一行不动） |
| `grep "0\.25\|0\.003\|L11\|5100" src/KFL.Core` | 仅 3 处**注释/XML 文档**命中（`AppointmentTrack.cs` 的「一甲（授 L11）」、`ImperialClass.cs` 的映射归属说明、`OfficialRank.cs` 的「72~5100 属阶段⑧」），**无一处是可执行代码** |
| `tests/KFL.Tests/KFL.Tests.csproj` 的 `_MSTestEnableParentProcessQuery` | 仍为**注释态**（未被本次改动触碰） |
| `git diff --stat -- specs/001-core-skeleton/{tasks.md,implementation-notes.md,checklists} specs/002-monthly-settlement-economy/tasks.md` | **无输出**（历史记录类工件的旧编号未被回改，规格书 §16.2 的迁移口径） |

## 2. 契约条文与代码之间的**一处不可能成立的写法**（重要）

**位置**：[contracts/official-career.md](./contracts/official-career.md) §3 条款 1 / tasks T029 的
「置 `AwaitingPost` 位与计时（**赋值次序：先 `Timers`、后 `Status`**，与 `Person` 的交叉校验方向一致）」。

**实测**：`Person` 的交叉校验是「**计时字段非空 ⇒ 对应状态位 MUST 为真**」
（`Person.Timers` 的 setter，001 已交付并有 `StatusTests` 覆盖）。因此**先写 `Timers` 必抛
`ArgumentException`**——契约的这一句与 001 的既有不变量方向相反，**按字面无法实现**。

**实现取值**（`AppointmentEntry.Begin` / `OfficialCareerAdvance.TryAppoint`）：

- **置位**：先 `Person.Status |= StatusFlag.AwaitingPost`，再写 `Person.Timers`；
- **清位**：先写 `Person.Timers`（把待阙计时置 `null`），再 `Person.Status &= ~StatusFlag.AwaitingPost`
  ——**与契约七 §3 条款 4「先清计时、后清位」一致**；
- 两处都**重建** `StatusTimers` 并保留其余三个计时字段（MUST NOT 整体覆盖为 `default`，
  否则会静默丢掉服刑/禁考/禁升的剩余月数）。

即：**只有「置位」这一步的次序与契约七 §3 条款 1 的字面相反**，其余全按契约。
若后续阶段要在文档里统一口径，改契约（或给 `Person` 加一个成对写入的入口）即可，
**MUST NOT** 反向放宽 `Person` 的不变量——那会让「计时与状态位一致」在类型层失效。

## 3. 实现登记的三处**补充口径**（契约未写，但实现必须选一个）

### 3.1 成员标识不消耗随机

契约六 §3 的随机消费次序是「①姓氏 →②家主 →③配偶 →④孩子 i →⑤存档标识（`NextBytes(16)`）」，
**不含成员标识**。故 `NewGameSetup` 的 `PersonId` 由（出身、姓氏、加入序号）**确定性派生**
（FNV-1a 混合式 → 16 字节 → `Guid`），**不消耗注入的随机**；若改从随机取，会挤掉后续成员的
取值槽位，使契约六 §3 的次序不可断言。存档标识仍走 `GameStateFactory`（与契约六 §2 步骤 6 一致）。

### 3.2 入仕途径的派生（③-a 授官时）

`AwaitingPostRemainingMonths` 归零时授官需要知道「按哪一甲授官」，而 `Person` **不存**入仕途径
（data-model §2.1 的「明确不含」）。实现的派生规则（`AppointmentEntry.TrackOf`）：
功名记录末条是**进士** ⇒ 按 `Class` 映射一甲/二甲/三甲（`Class == null` 时**抛异常**，不猜等级）；
末条不是进士或**无功名记录** ⇒ `AppointmentTrack.SpecialTribute`（特奏名本就没有进士记录，
逻辑轨 ⑤ 的入口只经 `AppointmentEntry.Begin(person, track, …)` 写入待阙）。

### 3.3 `AppraisalPaused` 的登记时机

「因服刑暂停」只在**该服刑者已达考课周期**（`MonthsInOffice >= AppraisalPeriodMonths`）时登记：
未到期者本月本就没有判定可暂停，登记它会把「暂停」与「本月无事发生」混为一谈。
「禁升跳过」与「服刑暂停」是两个字段、两种语义，契约七 §4 条款 7 的口径未变。

## 4. 任务清单里的一处**无需改动**（避免后续误判为遗漏）

**T017** 要求「`tests/KFL.Tests/Core/FamilyTests.cs` 中的一甲功名构造点补传 `ImperialClass.FirstClass`」。
实测该文件**没有一甲功名构造点**：唯一的 `DegreeRecord` 构造点是
`(DegreeLevel.JuRen, null, …, Initial)`（举人、无名次）。给举人记录补传甲第会**违反**
`DegreeRecord` 的不变量 ①（甲第只属于进士），故本任务**按无操作处理**，文件未改。

**T010 的第三处注释改标**（research R-18#3）只列了 `AttributeLimits` 与 `Person`（`Lifespan` / `Merit`）
三处，均已改标为对应的逻辑轨；`OfficialRank` 的「年俸数值表属阶段⑧」一句**不在清单内**，
为避免顺手扩大改动范围而**保留原样**（它是说明性注释，不承载规则数值）。

## 5. 落地清单（按章程原则 V 的提交口径）

| 提交 | 范围 |
| --- | --- |
| `feat(core)` | `GameDate`（前史纪年）、`StatusTimers`（第 4 计时）、`DegreeRecord`（甲第 + 四条不变量）、`Person`（`MonthsInOffice` + 待阙交叉校验）、三个枚举、`AttributeLimits` 注释改标 |
| `feat(infrastructure)` | `INameGenerator`、`SongStyleNameGenerator`、`BogusNameGenerator`、`Bogus 35.6.1` 依赖 |
| `feat(rules)` | `OriginStartTable`、`AttributePolicy`、`OfficialCareerPolicy`、`GameConfig` 三组转发、`Start/NewGameSetup`、`Career/*`、`Settlement` 三处接线（官吏推进步、三态俸禄、`SettlementResult.Career`） |
| `test(...)` | 001 既有断言的受控修订（T014~T018）+ T013a/T019/T024 与 US2~US4 的四组新测试 + 架构数值清单增补 |
| `docs(...)` | 规格书 §3/§4.1/§7.5/§8.2 的 §17 裁决回写、001 的 `spec.md`/`data-model.md` 与两处契约、002 的数值登记表指向、003 的 `tasks.md` 勾选 |

```
320 → 409 项测试（新增 89 项，0 skipped）；六工程不变、无 .sln 共存；
KFL.Presentation / KFL.App 零改动。
```
