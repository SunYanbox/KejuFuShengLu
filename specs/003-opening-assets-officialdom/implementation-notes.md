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

### 3.2 入仕途径的落点（入口写入、③-a 只读）

**初版设计**：`Person` **不存**入仕途径，③-a 授官时从功名记录末条派生（`AppointmentEntry.TrackOf`）。
**复审确认该口径有缺陷**：`Begin(person, track, …)` 的 `track` 形参从未被读取，真正生效的是
「授官那一刻功名记录末条」，于是

- 显式 `FirstClass` 而该成员无功名记录时，授的是 **L18**（派生落到 `SpecialTribute`）而非 L11；
- 待阙期内被逻辑轨 ⑥ 的连坐降级追加一条「举人」记录后，**一甲进士会被改判成 L18**；
- 若逻辑轨 ⑤ 在待阙期内补记一条「进士但 `Class == null`」的记录，`TrackOf` 会在 **③-a 内部**
  抛 `InvalidOperationException`——此时同月其他成员可能已被改写，与契约七 §7 的**失败原子性**不符。

**现行实现（已按复审首选方案修正）**：`Person.EntryTrack`（`AppointmentTrack?`，与「待阙」**同生命周期**）
由入口写入，③-a **只读**它、并在授官时与待阙计时一并清空；`TrackOf` 只在**入口**
（`BeginForImperialGraduate`）与夹具里使用，MUST NOT 出现在授官路径上。
回归测试：`AppointmentTests.显式途径就是授官依据而不是功名记录的末条` /
`待阙期内功名被降级不改判已记录的途径` / `待阙期内末条进士甲第缺失也不会让月度推进抛异常`。

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

---

## 6. 复审响应（2026-10-10，PR #4 的复审意见）

复审结论为「门禁实测通过、主体设计建议合入」，但列出 1 处行为缺陷、1 类证据强度问题与若干文档问题。
逐条处置如下（**未在评论里争论、全部落到工件**）：

| 复审条目 | 处置 | 落地物 |
| --- | --- | --- |
| §1 `Begin` 的 `track` 被静默丢弃 | **修**（取复审的首选方案：把途径落进状态） | `Person.EntryTrack`、`AppointmentEntry.Begin`、`OfficialCareerAdvance.TryAppoint`；契约七 §1/§3；data-model §1.6/§2.1/§3.7/§3.8/§6.1；三条回归测试 |
| §1 附注：data-model §3.6 用「未使用形参会顶到 0 警告门禁」解释 `SalaryModePolicy.Of` 不收 `GameDate` | **改述**（该理由不成立：仓库无 `.editorconfig`，`IDE0060` 非警告级） | data-model §3.6 |
| §2 SC-002/003/004/005/007 的**数值**只有结构锚定、没有字面量锚定 | **补锚点**（新增 `Rules/SpecAnchorTests.cs`） | §10.1/§4.1/§8.2 的资产、成员构成、年龄/学业/体质区间、分布参数、寿数、待阙、官阶、政绩、考期、概率与公式、致仕、半俸、×1.05 逐项字面量；契约八 §1/§2 的「断言锚点」列改为真实测试名 |
| §3 契约八（003 与 002）的「配置成员」列有不存在的符号 | **改为真实成员名** | 003 契约八 §1/§2；002 契约五 §6 |
| §4 `GameDate.ElapsedMonths` 极端年份静默回绕 | **写明行为**（spec Assumptions 已裁决「沿用 001 现状」，故不改算术，只补注记） | `GameDate.ElapsedMonths` 的 `<remarks>` |
| §4 `BogusNameGenerator` 构造期消耗一次 `Next` 会挪动契约六 §3 的槽位 | **写进契约与代码**（不改 API：产品默认实现不消耗，改签名会波及已交付接缝） | 契约六 §3 的注记；`BogusNameGenerator` 的 `<remarks>` |
| §4 002 契约五 §7 与新增 §6 措辞自相矛盾 | **改写 §7**（把已交付的两项移出排除列表） | 002 契约五 §7 |

**本条响应后的门禁**：`dotnet build` **0 警告 0 错误**；`dotnet test` **421 通过 / 0 失败 / 0 skipped**
（409 → 421：`SpecAnchorTests` 6 项、`AppointmentTests` 3 项、`PersonTests` 1 项，另 2 项为既有
Theory 的新用例）；`dotnet sln list` 恰好**六个**工程；递归 `*.sln` **无输出**。

**验证物**：`SpecAnchorTests` 的判据不是「有断言」而是「改坏配置必须变红」——实测把
`OriginStartTable.InitialCashGuan(Merchant)` 由 `500m` 改成 `5m` 后，行为测试 41 项全绿
（与复审的判断一致），而 `SpecAnchorTests.四出身的初始资产逐格等于规格书` 失败。

**顺带同步**（复审未提、但同属「活工件」口径）：001 的 `data-model.md` §2.1 仍写「可写属性八个」，
本次随 `Person` 的字段变更一并更新为**十个**（`MonthsInOffice`、`EntryTrack`），并补上两条字段行与
「计时/途径与状态位一致」的不变量。
