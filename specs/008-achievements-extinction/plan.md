# Implementation Plan: 成就与绝嗣结局（68 条成就 · 全局持久化 · 编年史终局）

**Branch**: `008-achievements-extinction` | **Date**: 2026-10-18 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/008-achievements-extinction/spec.md`

**Note**: 本文件由 `/speckit-plan` 生成。数值真源是《科举浮生录规格书》§14（:528-554）、§14.1（:555-641）、
§15（:643-646）与 §17 的裁决（2026-10-17 块 + 本特性新增的 2026-10-18 块）。
本计划只负责「怎么落地」，**MUST NOT 新创任何数值**；`spec.md` 是 sole source of intent，
工件与规格书冲突时以规格书为准**修正工件**。

## Summary

交付**逻辑轨 ⑧「成就与绝嗣结局」**（规格书 §16.1 第 ⑧ 项；§16.2 的旧编号「⑪ 成就/绝嗣结局」）。
本特性是**纯逻辑轨**，全部断言 MUST 可完全脱离界面验收，MUST NOT 被 U 轨阻塞（`spec.md` 范围来源）。

技术路线一句话：**把既有「结算 → 写档 → 备份」编排扩展为「结算 → 成就与绝嗣判定 → 覆盖写档 → 备份
→ 全局成就落盘」**——在既有第 ① 步「结算」**之后**插入判定 ②（FR-007：判定在结算完成后、写盘之前），
在既有第 ④ 步「备份」之后追加全局落盘 ⑤，
用 `KFL.Core` 的两个新聚合（随档 `AchievementState` / `EndingState`）承载不可重算的累计量与终局，
用 `KFL.Infrastructure` 的新接缝 `IAchievementStore` 承载跨存档的 `achievements.json`，
判定器作为 `KFL.Rules` 的**唯一纯函数入口**消费既有六类当月事实快照。

- **US1 成就自动解锁与全局持久化（P1）**：68 条定义（`AchievementId` / `AchievementCatalog` /
  `AchievementPredicates`）+ `achievements.json`（各难度独立标记、该难度**最早**完成年月、来源存档 UUID）。
- **US2 瞬时与累计事实的确定性记账（P1）**：严格 **8** 项不可重算累计量（FR-013 ①~⑧）；
  ACH-034 / ACH-042 经「即时触发 + 状态面」化解，**不**新增第 9 项。
- **US3 绝嗣判定与编年史结局快照（P1）**：判据 `Family.RegisteredMembers.Count == 0`（**外嫁不计**）；
  七项编年史一次算定并冻结；平均寿命取 Tukey 三均值。
- **US4 结局后的状态机与存档语义（P2）**：`Advance` 顶部拒绝推进（`InvalidOperationException`，零状态变更）；
  写盘 / 读回 / 改名 / 备份仍可用；读档后结束态逐字段复原。
- **US5「触发源未交付」成就的接线与待办登记（P3）**：恰 **6** 条（ACH-041、ACH-064~068）走 `Pending`
  来源 + 两个上报接缝，逐条登记在 `tasks.md`，**MUST NOT** 自造经商亏损 / 调试唤醒规则。

**本特性不做**（`spec.md` Out of Scope）：界面轨全部（U1~U5）、调试控制台的任何实现、
经商亏损事件规则、随机事件总表、逻辑轨 ⑨、既有存档迁移（Version 4 档一律拒绝）、
新增声望/名望属性、跨进程并发写全局文件。

## Technical Context

**Language/Version**：C# 14 / .NET 10（非 UI 工程 `net10.0`，UI 工程 `net10.0-windows`；章程 I）。
**Primary Dependencies**：**无新增外部依赖**（FR-005）。`KFL.Core` 零依赖；`KFL.Rules` 只依赖
`KFL.Core` + `KFL.Infrastructure.Abstractions`；`KFL.Infrastructure` 复用既有的
`MessagePack` 2.5.305 / `System.Text.Json`（全局文件恒 JSON，**不**引第三方文件系统或 JSON 抽象）。
**Storage**：**两条通道** —— ① 随档（`SaveFileFormat.Version` **4 → 5**，`SaveStateDto` **8 → 10** 键，
旧版本一律 `UnsupportedVersion` 拒绝，复用 004 既定策略、**无迁移路径**）；② 跨档全局
`<ISaveService.RootDirectory>/achievements.json`（自身 `Version = 1`，恒 JSON，原子替换，
损坏时改名隔离后按空表继续）。
**Testing**：xUnit（`tests/KFL.Tests/`），架构守卫 G-01~G-08 在构建期强制；数值守卫
`ConfigLiteralRules`/`ConfigLiteralTests`（本特性阈值落在 `AchievementPolicy`，`GameConfig.Achievements`
是**纯转发**，零字面量）。
**Target Platform**：Windows 桌面（WPF），但本特性全部落点在三层非 UI 工程内，可无头运行。
**Project Type**：桌面应用 + 分层类库（单一解决方案 `KejuFuShengLu.slnx`，恰好六个工程）。
**Performance Goals**：单次 `Advance` 的判定开销在 200 人规模家族上 < 1 ms（68 条谓词 + 八项累计量
更新均为 O(成员数) 的纯内存运算）；全局文件一次 `Read` + 一次 `Flush`，文件体量在
「68 条 × 4 难度」上限下 < 64 KB；连续 120 月结算（含判定与落盘）< 2 s。
**Constraints**：
- 规则层 MUST NOT 触 `DateTime.Now` / `Random` / 文件系统（G-07 的十四条禁用 token，扫描范围含
  `src\KFL.Rules\` 与 `src\KFL.Infrastructure\`）。时间一律取 `GameDate`，随机一律不消费。
- 判定 MUST 有**唯一入口**（FR-006）；判定 MUST NOT 插入结算步骤表（FR-035）；
  派生谓词 MUST NOT 落盘（FR-003，唯一例外 = FR-013 的八项累计量）。
- 全局文件读写 MUST 经 `KFL.Infrastructure.Abstractions` 的接缝注入（FR-033），
  MUST NOT 静态单例 / ServiceLocator / 属性注入。
- 判定器 MUST NOT 注入 store：全局面以**只读参数**传入（`research.md` R-01），
  以保住纯函数单测与 SC-003 的确定性。
- 全局文件带**自己的**版本号，MUST NOT 复用 `SaveFileFormat.Version`。
- `KFL.Core/AssemblyInfo.cs` 只放行 `KFL.Infrastructure` ⇒ 新增持久字段的复原通道
  MUST 走 `internal` + Infrastructure 落盘（与 007 同款；若现状未开放则按 `data-model.md` §2.6 的
  既有变体二选一，验收标准是「`SaveStateMapper` 能在不新增 `public` 面的前提下复原」）。
**Scale/Scope**：单家族；成员数典型 3~200；新增 **8** 个 `KFL.Core` 类型（3 枚举 + 3 值对象
+ 2 聚合）与 `GameState` 的一处变更、**11** 个 `KFL.Rules/Achievements/` 文件、1 个配置类、
**5** 个新存档 DTO、**5** 个 Infrastructure 文件；新增 **15** 个测试文件（`Rules/` 10 + `Infrastructure/` 4
+ `Architecture/` 1）。

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 章程条款 | 本计划如何满足 | 结论 |
| --- | --- | --- |
| I .NET 10 单一基线 | 只改三类非 UI 工程，TFM 全保持；不新增工程、不新增 `*.sln` | ✅ |
| II 分层解耦与单向依赖 | 新增类型分别落 `KFL.Core`（状态/值对象）、`KFL.Rules/Achievements`（判定）、`KFL.Infrastructure`（形状与文件）；`Core` 仍零出边 | ✅ |
| II 规则层禁 `DateTime.Now`/`Random`/文件系统 | 全部时间取 `GameDate`；判定器**不消费随机**、**不触文件**（全局面以只读参数传入）；文件面全部在 Infrastructure | ✅ |
| II 跨层协作经接口 | 新增 `IAchievementStore`（正是章程 II 与 001 `injection-seams.md` §5 点名的接缝）；构造注入，`TurnRunner` ctor 的该参数为**可选**默认 `null` | ✅ |
| III WPF + MVVM、表现层不复制规则 | 本特性不触表现层；只读查询面 `AchievementQuery` 供 U1/U2 直接调用，界面无需自行判定 | ✅ |
| IV 单测优先 + 可复现种子随机 | FR-045/FR-048 的用例在**同一提交**内落地，含 136 条参数化判据用例与三均值金色表；判定路径零随机消费 | ✅ |
| V 约定式提交（中文） | 提交信息用中文 `feat(rules): 成就与绝嗣结局…`；存档格式变更用 `feat(save)!` + `BREAKING CHANGE:` 脚注 | ✅ |
| VI 调试通道不复制规则 | ACH-064~068 只留**上报接缝** `ReportsDebugWake(count)`；U4 的调试控制台将来必须调它，不得另写计数 | ✅ |
| 技术栈与工程约束 | 无新依赖；六工程不变；金额内部以「文」为 `decimal`（阈值经 `Money.FromGuan(配置成员)`） | ✅ |
| 开发工作流与质量门禁 | 门禁四条（build 0 警告 / test 全绿 / `dotnet sln list` 六工程 / 无 `*.sln`）+ G-01~G-08 | ✅ |
| 需求真源 | 数值全部来自规格书 §14/§14.1/§15/§17；§17 新裁决块与十项回写在**同一提交**内完成 | ✅ |

**设计后复核（Phase 1 之后）**：✅ 无新增违规。`Complexity Tracking` 里四项新增抽象均已论证，
其中「新增 `IAchievementStore` 接缝」与「全局文件恒 JSON」是本特性最可争议的两处复杂度，
理由见该节第 1、3 行。

### 继承的用户裁决（2026-10-18，规格书 §17）

| 编号 | 议题 | 裁决 | 落点 |
| --- | --- | --- | --- |
| Q1 | 交付范围 | 交付全部 **68** 条定义 + 计数接缝 + 待接线清单；**MUST NOT** 实现调试控制台（U4）；**MUST NOT** 自造经商亏损事件规则 | `AchievementCatalog`、`AchievementSource.Pending`、`contracts/achievement-catalog.md` §4 |
| Q2 | 判定时点 | 月末结算**完成后、写盘之前**统一判定一次；FR-009 的动作型事实**即时**判定；解锁不可撤销 | `TurnRunner.Advance` ②、`AchievementAdjudicator.Judge`/`Report` |
| Q3 | 存档语义 | 随档新增累计器与结束状态，`SaveFileFormat.Version` **4 → 5**；旧档按既有版本闸门**拒绝读取**，**不做**就地迁移 | `contracts/achievement-store.md` §6 |
| A | 存续年数门槛 | **逐字从严**：`百年世家` `> 100`（首次成立 = 当前年 102）、`千秋万代` `> 300`（当前年 302）、`绝处逢生` **`>= 100`**（地狱，当前年 101；§14.1:570 无「超过」字样） | `AchievementPolicy`、`contracts/chronicle-ending.md` §2 |
| B | 传承代数 | = 族谱最大 `Person.Generation` **+ 1**（辈分 0 基准，§4.4:115 与 `KFL.Core/Entities/Family.cs:138`） ⇒ `五世其昌` = `Generation >= 4`（起草稿误写 `>= 5`，已更正） | `ChronicleBuilder`、`contracts/achievement-catalog.md` §2 |
| C | 平均寿命 | 已故成员**享年分布的三均值**（Tukey trimean：`TM = (Q1 + 2·Q2 + Q3) / 4`，Q1/Q2/Q3 取 **Tukey hinges**、**奇数样本时中位计入两半** = R `fivenum` 口径）；`decimal`、**3** 位小数 `AwayFromZero`；`n <= 3` 照公式，`n = 0` 取 `0` 且不抛 | `TukeyTrimean`、`contracts/chronicle-ending.md` §4 |
| D | 榜下捉婿 | 历史上**出现过** `Kind == ImperialProposal` 的提议即解锁（**含被拒**）；判据只读 `Kind`，**不依赖** `CandidatePlacement` | `AchievementPredicates`、`contracts/achievement-catalog.md` §2 |

**次级口径复核 A~D（2026-10-18）已裁决完毕**，`spec.md` 无未获复核的假设条目（Assumptions 25 条）。

## 勘误登记（计划阶段）

只登记**工件内部矛盾**，不新创数值。

| # | 位置 | 原文 | 计划口径 | 依据 |
| --- | --- | --- | --- | --- |
| E-01 | `research.md:33`（R-18 的「影响工件」列）；同性质的一处：`research.md:26`（R-11） | R-18 指向「data-model §3.7」、R-11 指向「契约二十三 §1」 | 实为 **`data-model.md` §3.10**（`Config/AchievementPolicy.cs`）与 **`contracts/achievement-store.md` §2**（文件位置与格式；§1 是 `IAchievementStore` 接口）。**已在本次计划阶段就地订正 `research.md`**，其余指向（R-01 的 §3.1、R-05~R-07 的契约二十四 §2/§3/§4 等）核对无误 | `data-model.md:470`；`contracts/achievement-store.md` §2 |
| E-02 | `data-model.md:19`（§1 层分布总览） | `tests/KFL.Tests` 新增「`Rules/` 8 个 + `Infrastructure/` 4 个测试文件」 | 实为 **9 + 4 + `Architecture/` 1 = 14**：漏记 `Rules/AchievementQueryTests.cs`（`data-model.md` §3.9 已把 `AchievementQuery` 列为交付物，其纯度断言 MUST 有落点）与 `Architecture/AchievementPendingWiringTests.cs`（本计划与 `quickstart.md` 场景 10 都依赖它） | `data-model.md:468`；`quickstart.md` 场景 10；`spec.md` SC-002 |
| E-03 | `contracts/achievement-store.md:5/:211`、`contracts/chronicle-ending.md:5`、`data-model.md:673`、`quickstart.md:185-186` | 引用 004 的目录名为 `specs/004-persistence-recovery/` | 实为 **`specs/004-save-system/`**（已在本次计划阶段就地订正，含 `quickstart.md` 的两条 `Select-String` 路径） | 仓库实际目录（`Get-ChildItem specs -Directory`） |
| E-04 | `specs/007-family-lifecycle/plan.md:125` | 引 004 为 `specs/004-save-system/...`（正确），并「顺带修正 004 `data-model.md` 的存量旧值 `FormatVersion = 2`」 | **不在本特性范围内**（007 已登记）；本特性只改 004 契约的键表/版本/常量/校验条数，MUST NOT 顺手改 004 `data-model.md` 的其它存量值 | AGENTS.md「一次提交只做一件事」；章程 V |
| E-05 | 本文件自身 | 初稿曾在 `Summary` 写「`SaveStateDto` 8 → 10 键」而 `Scale/Scope` 写「**5** 个新存档 DTO」 | 两者一致：DTO 为 `AchievementStateDto` / `AchievementMarkDto` / `PendingUnlockDto` / `EndingStateDto` / `ChronicleDto` = **5** 个，键表见 `contracts/achievement-store.md` §6 | 本文件、`data-model.md` §7.1 |

### 勘误登记（任务阶段追加，承接 `tasks.md` 的 E-06~E-11）

> 本节为 `$speckit-analyze` 后的**追加**登记（不覆盖 E-01~E-05）。E-06~E-11 的完整口径见
> `tasks.md`「勘误登记（任务阶段）」表，此处只留指针与影响面。

| # | 位置 | 原文 | 采用口径 | 依据 |
| --- | --- | --- | --- | --- |
| E-06 | 本文件 `:69-70`（新增 14 个测试文件：`Rules/` 9 + `Infrastructure/` 4 + `Architecture/` 1）与 `data-model.md:19` | 两处数字矛盾，且**均漏 `Rules/AchievementDeterminismTests.cs`** | 实为 **15 个**：`Rules/` **10** + `Infrastructure/` **4** + `Architecture/` **1**（**已就地订正**本文件与 `data-model.md`） | `data-model.md` §6 不变量 15；`spec.md` SC-003；`tasks.md` E-06 |
| E-07 | 契约二十二 §5 的测试文件名表 | 与 `plan.md:225-244` 的 Project Structure 不一致 | **以 Project Structure 为准**：`AchievementJudgmentTests` → `AchievementPredicateTests`；`AchievementArchiveJsonTests` **并入** `AchievementStoreTests`；`AchievementSaveRoundTripTests` **并入** `AchievementSaveFormatTests`；`EndingRefusalTests` **并入** `EndedSaveTests`（**已就地订正**契约 §5） | `tasks.md` E-07 |
| E-08 | 契约二十四 §2「条款 6」表与 `quickstart.md:32-33`、`:51-52` | 首达年数写 `Year == 103`/`303`；`quickstart.md` 写「**六个**系列」与「宗族血脉 17 / 覆巢之下 11」（与契约二十四 §1 的系列列自相矛盾） | **规格书口径**：`SurvivedYears = CurrentDate.Year − 1` ⇒ `> 100` 首达 `Year == 102`、`> 300` 首达 `302`、`绝处逢生` `>= 100` 首达 `101`；系列为**七**个，计数 `12/8/11/17/11/4/5`（**已就地订正**契约二十四与 `quickstart.md`） | `tasks.md` E-08；`spec.md` Assumptions 5 / US3 AS4 |
| E-09 | 契约二十四「条款 6」「条款 25」「条款 18」 | 枚举名 `SurvivedAgainstOdds`/`AcedTheLot` 在 `AchievementId` 中**不存在**；`烟消云散` 写成 ACH-**045**；称 `TukeyTrimean` 有两个调用点（其中 ACH-012「需要同口径存续年数」） | `NarrowEscape`（ACH-012）/`HellEnding`（ACH-048）；`烟消云散` = `FamilyExtinct` = **ACH-044**；`TukeyTrimean` 的调用点只有**编年史 ⑥**（**已就地订正**契约二十四） | `tasks.md` E-09；契约二十二 §1 |
| E-10 | 契约二十二 §3 第 4/5 行 | 「002 借款发生入口」「002 提前清偿入口」**未给路径** | 借款本金唯一来源 = `src/KFL.Rules/Economy/PaymentPrimitive.cs:108-111`（缺口转贷款）；提前清偿 = `FamilyEconomy.RepayLoan`（`src/KFL.Core/Entities/FamilyEconomy.cs:233`）（**已就地订正**契约二十二 §3） | `tasks.md` E-10 |
| E-11 | 契约二十二 §3 第 8/11 行 | 「007 买功名婚姻入口」「005 科举询问应答入口」**未给路径** | `DegreeMarriagePurchased` 是 `LifecycleEntry.TryMatchmake`（`src/KFL.Rules/Family/LifecycleEntry.cs:40`）/`RespondToProposal`（`:198`）的 `buyDegreeMarriage` 参数；`ExamInquiryAnswered` = `ExamRegistration.AcceptSpecialTribute`（`src/KFL.Rules/Exam/ExamRegistration.cs:170`）（**已就地订正**契约二十二 §3） | `tasks.md` E-11 |
| E-12 | 本文件 `:176`（`AchievementSeries.cs  # 【新】六系列`）与 `:229`（「七系列计数」段落中的「六系列」） | 本文件自身写「**六系列**」，与规格书 §14.1 的**七**个 `####` 系列标题矛盾 | 一律改 **七系列**（`AchievementSeries` 恰 7 个成员）（**已就地订正**本文件） | 规格书 §14.1 :557/:572/:582/:596/:616/:630/:636；`spec.md` 判据总表 :445/:462/:475/:491/:513/:529/:538；契约二十二 条款 2 |
| E-13 | 本文件 `:16`（「在既有『结算 → 写档 → 备份』编排**之后**追加『成就与绝嗣判定』与『全局成就落盘』两步」） | 读作两步都排在 ④ 之后，与 `contracts/achievement-store.md` §5、`spec.md` FR-034/FR-007（判定在**写档之前**）、`quickstart.md` 场景 4 矛盾 | 表述为「在 ① 结算之后、③ 覆盖写档之前**插入** ② 成就与绝嗣判定；在 ④ 备份之后**追加** ⑤ 全局成就落盘」（**已就地订正**本文件） | `spec.md` FR-034/FR-007；契约二十三 §5 条款 19/22；`quickstart.md` 场景 4 |
| E-14 | `research.md:538`/`:549`、`data-model.md:610`/`:714`、契约二十二 §2 条款 4 与 §5 表行 | 把「配置字面量守卫/环境守卫」的锚点写成 **SC-008**，而 `spec.md:592` 的 SC-008 实为「绝嗣判定四条口径」 | 配置字面量守卫的真实依据 = **章程「技术栈与工程约束」（规则数值集中配置）**；SC-008 只留绝嗣口径（**已就地订正** `research.md`/`data-model.md`/契约二十二） | `spec.md:592`；章程「技术栈与工程约束」 |
| E-15 | `tasks.md:127`（T013）、`:324`（T087） | 同 E-14 的锚点错指（T087 还把「变红确认」的对象写成 `SC-008`） | `tasks.md` 既有条目属**历史记录类**，MUST NOT 回改 ⇒ 在 `tasks.md`「收敛补充」段做**追加式更正** | `tasks.md` 收敛补充；AGENTS.md「历史记录类工件 MUST NOT 回改」 |
| E-16 | FR-031（不实现界面）与 Assumption 19 / Edge Case 1 | 25 条 Assumptions 中 21 条、16 条 Edge Cases 中 12 条无任务文本引用；**FR-031 无显式实现/守卫任务**（仅 `tasks.md:35-39` 范围声明与 T044 的「MUST NOT 改动 `KFL.Presentation`」隐式承担）；**Assumption 19 / Edge Case 1**（FR-032 要求「次序必须被测试钉死」）无任务点名 | FR-031 的守卫并入 T002；次序义务在 T064 文本显式引用 EC1/Assumption 19 ⇒ 在 `tasks.md`「收敛补充」登记追加任务 | `spec.md` :607-694（Assumptions）/ :235-268（Edge Cases）；`tasks.md` :35-39 |
| E-17 | 契约二十四 §1 条款 1 行 ④「最高 `DegreeLevel`」与 `tasks.md` T008 | `Person` **无** `DegreeLevel` 属性；实际成员是 `Person.CurrentDegree`（`src/KFL.Core/Entities/Person.cs:173`），`DegreeLevel` 是类型名 | 口径写成「`Person.CurrentDegree` 的最大值（含已亡与外嫁归档）」（**已就地订正**契约二十四；`tasks.md` 按追加式更正） | `src/KFL.Core/Entities/Person.cs:173`；`tasks.md` 收敛补充 |
| E-18 | `src/KFL.Infrastructure/Abstractions/ISaveService.cs:93` | `Load` 的 XML doc 仍写「结束于**九条**一致性校验」，实际 `SaveStateValidator` 现为 15 条（14 + `ValidateIdentity`），**不在** T001 的 2 处注释清单内 | 该注释并入 T001 的注释订正清单（**已就地订正**为「十五条」；008 落地后由 T001 一并改为 22 条口径） | `tasks.md` 收敛补充 |
| E-19 | `tasks.md:191`（T043 ②）、`data-model.md:224`/`:561`、契约二十三 §5 `:184` | 三处都要求 **`KFL.Rules`** 侧在 ⑤ 落盘成功后调 `state.Achievements.RestorePendingUnlocks([])`，但 `data-model.md:224` 与 T009 `:127` 又把该方法声明为 **`internal`**，且 `src/KFL.Core/AssemblyInfo.cs:13` 只放行 `KFL.Infrastructure`、MUST NOT 增放行程序集 | 取**最小改动**变体：`RestorePendingUnlocks` 改为 **`public`** 写通道（`Restore(...)` 仍 `internal`），使三处调用逐字成立；调用方义务（队列 ⊆ `Completed`）由存档期第 17 条强制（**已就地订正** `data-model.md` §2.3 的签名行） | `src/KFL.Core/AssemblyInfo.cs:10-13`；`data-model.md` §3.6；契约二十三 条款 21/22 |
| E-20 | `tasks.md:225`（T055）与契约二十二 §3 第 11 行 | 两处都要求 `ExamRegistration.AcceptSpecialTribute` 成功后上报 `AchievementTrigger.ExamInquiryAnswered`，但该入口签名为 `(Person, GameDate, IRandomService)`——**没有 `GameState`**，且全仓**无生产调用方**（真实应答方为 U2 界面，由 `ExamSeasonRunner` 的 `SpecialTributeInquiries` 驱动） | 取**加性**变体：`AcceptSpecialTribute` 追加**可选尾参** `GameState? state = null`，`state is not null` 时上报；既有 3 参调用逐字不变，资格判定与随机次序不受影响。属**跨特性（005）形状变更** ⇒ 同提交回写：`specs/005-exam-cycle/contracts/exam-calendar.md:141`、`specs/005-exam-cycle/data-model.md:133`/`:151`、`specs/005-exam-cycle/research.md:124`；005 的 `tasks.md` 属历史记录类，MUST NOT 回改 | `tasks.md` T055/E-11；契约二十二 §3 条款 8~10；`src/KFL.Rules/Exam/ExamRegistration.cs:170` |
| E-21 | `data-model.md:606`（§6 不变量 5）、`research.md:186`/`:445`、契约二十三 §7「条款 32」表第 3 行、`tasks.md:143`（T019 ③） | 四处都把第 18 条写成**相等**：`Lifespans.Count == Members.Count(p => p.Status.HasFlag(StatusFlag.Deceased))`。但 006 的 `PunishmentImposer.Execute`（`src/KFL.Rules/Punishment/PunishmentImposer.cs:440`）在 `DeathRegister` **之外**置 `StatusFlag.Deceased`（处决与株连家属），既不产出 `DeathFact`（`DeathCause` 明确不补「处决」取值）也不记享年 ⇒ 相等在**产品路径上不可达**，会让合法的旧档（如 `SaveFixtures.RichState()` 的处决场景）读档即拒 | 第 18 条改为**上界**：`Lifespans.Count <= Members.Count(p => p.Status.HasFlag(StatusFlag.Deceased))`，并保留「每项 `>= 0`」；`tasks.md:143` 属历史记录类，MUST NOT 回改 | `src/KFL.Rules/Punishment/PunishmentImposer.cs:440`；`src/KFL.Core/Enums/DeathCause.cs`；`tests\KFL.Tests\Infrastructure\SaveFixtures.cs` |
| E-22 | `tests\KFL.Tests\Rules\BribeQueryTests.cs`（006 的 FR-030 守卫，原断言在 `:178` 一带） | 原断言「**程序集内**不存在任何成就类型」用来钉死 006「不自行解锁成就」。008 把 `KFL.Rules.Achievements` 落在**同一程序集**（`KFL.Rules`）⇒ 该断言被 008 的实现正当地证伪 | 改为断言「**惩罚命名空间**（`KFL.Rules.Punishment`）内没有任何成就类型」，006 的职责边界与守卫强度不变（成就的判定与落盘仍在 008 侧） | `tasks.md` T057（同步既有断言，MUST NOT 放宽容许值）；契约二十二 §3 第 4 行 |
| E-23 | `tests\KFL.Tests\Infrastructure\SaveServiceTests.cs`（SC-002 长跑）、`tests\KFL.Tests\Infrastructure\SaveStateComparer.cs:54-58`（`AssertBits`） | ① 长跑夹具 `SaveFixtures.PlainState()` 的家主生于 1 年 1 月，第 13 月因 **`DeathCause.Infancy`** 身亡 ⇒ 家族绝嗣，008 的结束闸门（契约二十四「条款 19」）此后拒绝推进，SC-002 的「24 + 12 月」场景不再可运行（诊断输出：第 13 月绝嗣，在册 0，死因 `Infancy`）。② 换用能存活的夹具后，`AssertTreasury` 在 `Treasury.Savings.Wen` 上亮出位级差异：内存侧为**负号零**（bits 尾词 `0x801A0000`，标度 26），往返后为同标度的正零——`decimal` 的文本形态不表达负零（`(-0.000m).ToString()` 得 `"0.000"`），JSON 与二进制两条路径实测一致丢失该位 | ① 新增本地夹具 `LongRunState()`（家主生于**前史纪年** `(-19, 1)`、体质取 `GameConfig.Attributes.HealthMean`，沿用同一 `PlainStateId`、薄经济与起始年月 1 年 1 月），断言本身**未放宽**；② `AssertBits` 改为「零一律按正零比较，其余逐位比较」——FR-021 要求位级保留的是**标度**（尾随零），不是零的符号，`SaveStateComparer` 顶部注释的立论即在此。两处**均不触碰生产代码** | `src/KFL.Core/ValueObjects/GameDate.cs:53-65`（前史纪年合法）；`src/KFL.Core/ValueObjects/Money.cs:24-96`（唯一构造入口）；`spec.md` FR-021/SC-002 |

| E-24 | 契约二十二 §2 的 ACH-035 行（`秦晋之好`）、`tasks.md` T039/T040（判据实现）与触发器 8 | 判据表写「当月 `MarriageFact.IsDegreeMarriage == true`」，但 007 的 ⑧-a（`src/KFL.Rules/Family/PopulationAdvance.cs:43-45`）只把 `PendingProposal` 写进 `LifecycleSnapshot`，**从不填 `Marriages`**（`populationAdvance` 只 `with` 了 PendingProposal/Births/Illnesses/Crossings/Deaths）⇒ 当月面**恒空**，ACH-035 按原口径**永不可达**；动作型即时判定（触发器 8）的 `Settlement` 又是 `null`，同样不可达 | 按 `spec.md` FR-008「判定 MUST 同时消费**状态面**与当月报告面」补**状态面兜底**：家族台账出现过 `LedgerCategory.DegreeMarriageFeePaid`（娶入）或 `DegreeMarriageDowryReceived`（嫁出）即算达成（与规格书 §14.1「完成一次『买功名婚姻』（娶入或嫁出）」的历史口径一致）。**不新增数值、不放宽既有期望**；契约二十二 §2 该行与 §5 均已同提交回写；「恢复当月面严格口径」登记为**待接线候选**（需 007 在 ⑧-a 补记婚配事实，属 007 的形状变更） | 规格书 §14.1「秦晋之好」；`spec.md` FR-008/FR-003；`src/KFL.Rules/Achievements/AchievementPredicates.cs`（`HasDegreeMarriageRecord`）；`src/KFL.Core/Enums/LedgerCategory.cs:103`/`:121` |

| E-25 | `tasks.md` T087（`:346`）的「变红确认」机制描述 | 原文要求「**临时移除一项配置登记**」使 `ConfigLiteralTests` 变红 | 实测**不成立**：`ConfigLiteralRules.Evaluate` 是**单向**判据——只报「登记表里的数值出现在 `src\KFL.Rules\Config\` 之外」的行，**删除登记项只会缩小检查面，永远不会变红**（实测：删 `0.1m` 后 7/7 仍绿）。**正确的变红对象**：① 摘掉一条行级豁免注释（`// arch-guard:allow`）让既有撞车数值复现为违规 ⇒ `ConfigLiteralTests` 红（实测 1/7 红）；② 在 `src\KFL.Rules\` 引入禁用 token ⇒ `EnvironmentDependencyTests` 红（实测 2/3 红，G-07 报文逐字命中）。两条探针均**逐字节还原**（SHA256 前后一致）。执行记录见 `checklists/implementation.md` 的「T086~T088」节 | `tests\KFL.Tests\Architecture\ConfigLiteralRules.cs:112-154`（单向判据）；`checklists/implementation.md` |

> **编号对照（本表 ↔ `tasks.md` 的勘误表）**：两表的 **E-06~E-11 与 E-17~E-18 同号同义**；
> 其余各行是**各自文件内部的登记**，编号在 `tasks.md` 于 38e85ba 就地订正后出现偏移，逐条对照如下
> （**MUST NOT** 跨文件按号取义，须按内容对照）：
> `tasks.md` **E-12**（编排次序）↔ 本表 **E-13**；`tasks.md` **E-13**（SC-008 锚点）↔ 本表 **E-14** + **E-15**；
> `tasks.md` **E-14**（T081 缺文件路径）与 **E-15**（T043 只覆盖 ⑤）是本表**未另立行**的 `tasks.md` 内部文本问题
> （其更正对象分别是 T095 与 T090）；`tasks.md` **E-16**（系列计数）↔ 本表 **E-12**（「六系列」表述）与
> **E-08**（首达年数与七系列）；本表的 **E-16**（FR-031 无显式守卫 / 次序义务无逐字落点）在
> `tasks.md` 中对应**收敛补充**的 T096 / T097。本表 **E-19~E-23** 为本表独有（`tasks.md` 无同号条目）。

## 回写义务清单（AGENTS.md「铁律」+ FR-036~FR-040 的落地）

先说结论：本特性在公开面上确实做了跨特性形状变更——① 存档 `Version` **4 → 5**、`SaveStateDto`
**8 → 10 键**、**5** 个新 DTO、校验 **15 → 22** 条；② 新增跨档**全局文件** `achievements.json`
（新接缝 `IAchievementStore`）；③ `GameState` **+2 聚合属性**；④ `TurnRunner.Advance` 编排 **③/④ → ①~⑤**
（追加 ② 判定与 ⑤ 全局落盘），ctor **+1 可选参**；⑤ `KFL.Rules/Config/` **+1 配置类** + `GameConfig`
**+1 转发视图**；⑥ 规格书 §17 **+1 裁决块**；⑦ 006 的 `FamilyCumulativeBribes` 语义需要一条**读向备注**
（尝试次数 vs 成功次数）。下表的**行号为回写前的原位**。

| # | 目标工件（活工件） | 回写内容（锚点） | 依据 |
| --- | --- | --- | --- |
| 1 | `科举浮生录规格书.md` | §14（`:528-554`）的持久化口径补「全局成就文件自带版本号 + 向后兼容 + 损坏隔离 + 写失败不阻断」；§14.1 的 **6** 条标注（`:600` 榜下捉婿「含被拒」、`:611/:612` 的「超过」逐字从严、`:570` 绝处逢生取 `>=`、`:641` 调试秘辛改述为「待 U4 接线」、`:570` 大梦一场）；§15（`:643-646`）编年史七项补逐项取值口径（含三均值）；§16.1 必测清单第 9 条补「落点 = 008 的 `ExtinctionTests`」；§17 **追加裁决块「2026-10-18（逻辑轨 ⑧ 的空白填充，Q1~Q6）」** | FR-039 |
| 2 | `specs/004-save-system/contracts/save-format.md` | `:77` 一带的 `SaveStateDto` **8 → 10** 键（补 `[8] achievements` / `[9] ending`）；`:73-89` 表补 **5** 个新 DTO 行；版本闸门（`:128`/`:137`/`:150` 语境）`== 4` → **`== 5`**；一致性校验 **十五条 → 22 条**（新增 7 条，不夹带可重算派生值）；`:151-153` 的键数常量补 `AchievementStateMemberCount = 8` 等 5 项；**后记**追加 008 行（Version 5 / 键表增量 / 校验条数 / 边界声明「仍不引入迁移路径」） | FR-036、FR-037 |
| 3 | `specs/004-save-system/data-model.md` | 版本行（`FormatVersion` 4 → 5）与键表引用同步；**不**改其它存量值（见 E-04） | FR-036 |
| 4 | `specs/001-core-skeleton/contracts/injection-seams.md` | `:91-99` 的「`IAchievementStore` / `IEventBus` 至今未定义」⇒ `IAchievementStore` 改述为**「已由 ⑧ 定义」**并指向本特性契约二十三 §1；`IEventBus` **维持未定义**；顺带登记「接缝集合自 004 起首增一项」 | FR-033 |
| 5 | `specs/001-core-skeleton/data-model.md` | 只补「`GameState` +2 聚合属性」与不变量 4 的引用注（`IsEnded ⇔ EndedAt ∧ Chronicle`）；**「无写入通道成员全集」与 `Person` 的可写属性清单 MUST NOT 变**（008 不改 `Person`） | FR-036 |
| 6 | `specs/006-bribery-punishment/` | `contracts/punishment.md` / `contracts/config-registry.md` 补一条**读向备注**：`PunishmentQueries.FamilyCumulativeBribes` 的语义是**尝试**次数，而 `暗度陈仓`/`权钱交易`/`只手遮天`/`买断功名` 要的是**成功**次数 ⇒ 后者读 `AchievementState.SuccessfulBribeCount`，MUST NOT 复用前者 | spec `Notes` ①в |
| 7 | `specs/007-family-lifecycle/data-model.md` | `:376-390` 的形状变更汇总表与 `:429-467` 持久化节补两行交叉引用：「⑧ 消费 `LifecycleSnapshot` 的六类当月事实」与「⑧ 新增 `EndingState`，与 `Family.Lifecycle` 正交」 | FR-036、AGENTS.md 铁律 |
| 8 | `tests/KFL.Tests/Infrastructure/SaveStateComparer.cs:21` | 扩展为比较 `Achievements` 的 8 个字段与 `Ending` 的三个状态字段（含 `Lifespans` / `Genealogy` 的**次序**），使既有往返测试自动覆盖新形状 | FR-045 |
| 9 | `src/KFL.Infrastructure/Save/SaveFileHeader.cs:21`、`tests/KFL.Tests/Infrastructure/SaveFailureTests.cs:23` | 陈旧注释「恒为 3」→ **5**；**其余**版本断言全部已用 `SaveFileFormat.Version` 常量，0 处需改（`SaveFormatContractTests.cs:37,56,270`、`SaveStateValidatorTests.cs:356`、`SaveFailureTests.cs:156-157,183-184,477,503,585-586`） | SC-013、SC-015 |
| 10 | `specs/008-achievements-extinction/tasks.md` | 登记上述九项的回写范围（同步了哪些文件、同步到哪里），并登记「本特性**接管** 001 `tasks.md` 中『成就/绝嗣结局属后续阶段』的残句」与 SC-013 的逐条版本断言核对结果 | FR-038 |

> **不改的**：`tasks.md` 既有条目、`implementation-notes.md`、既有 `checklists/` 属**历史记录类工件**，
> MUST NOT 回改。001/002/003 的 `tasks.md` 里「阶段⑧」「旧⑪ 成就/绝嗣结局」之类的残句保持原样，
> 只在 008 的 `tasks.md` 里登记「已接管」。既有测试的**期望值** MUST NOT 被改来迁就新行为（SC-015），
> 确有必要者 MUST 在 `tasks.md` 逐条记录理由。

## Project Structure

### Documentation (this feature)

```text
specs/008-achievements-extinction/
├── plan.md              # 本文件
├── research.md          # 阶段 0：R-01~R-21 技术抉择（21 项全部有结论，未决事项 = 无）
├── data-model.md        # 阶段 1：层分布 / 新增类型 / 形状变更 / 状态转移 / 不变量 / 持久化
├── quickstart.md        # 阶段 1：10 个可执行验证场景（0 前置 + 场景 1~10 + 回写核对）
├── contracts/
│   ├── achievement-catalog.md # 契约二十二：68 条三元组表 + 判据阈值表 + 12 个即时判定入口 + 待接线接缝 + 测试映射
│   ├── achievement-store.md   # 契约二十三：IAchievementStore / 全局文件 / 合并规则 / 隔离 / 存档形状与版本
│   └── chronicle-ending.md    # 契约二十四：绝嗣判据 / 编年史七项 / 三均值 / 拒绝与冻结 / 判定次序
├── spec.md              # 已存在（694 行，sole source of intent）
└── checklists/
    └── requirements.md  # 已存在（16/16，历史记录类）
```

> `tasks.md` 由 `$speckit-tasks` 生成，不是本命令的交付物。

### Source Code (repository root)

```text
src/KFL.Core/
├── Enums/
│   ├── AchievementId.cs             # 【新】ACH-001~068，68 成员（含中文名注释）
│   ├── AchievementSeries.cs         # 【新】七系列
│   └── OriginMilestone.cs           # 【新】Extinction / CenturyFamily（出身达成段的两类）
├── ValueObjects/
│   ├── AchievementMark.cs           # 【新】档内完成标记（标识 + 难度 + 完成年月）
│   ├── PendingUnlock.cs             # 【新】待落盘解锁（含解锁年月 + 来源存档 UUID）
│   └── ChronicleSnapshot.cs         # 【新】编年史七项（含 Genealogy 的 PersonId 次序序列）
├── Entities/
│   ├── AchievementState.cs          # 【新】累计量 ①~⑧ + 完成集合 + 待落盘队列（+ internal 复原通道）
│   ├── EndingState.cs               # 【新】IsEnded / EndedAt / Chronicle（End 幂等 + internal 复原）
│   └── GameState.cs                 # 【改】+2 只读聚合属性（内联初始化，ctor 形状不变）
└── (AssemblyInfo.cs)                # 【不动】只放行 KFL.Infrastructure

src/KFL.Rules/
├── Achievements/                    # 【新目录】
│   ├── AchievementAdjudicator.cs    # 唯一判定入口：Judge(月末) / Report(动作型即时)
│   ├── AchievementCatalog.cs        # 68 条静态目录（只读、无可变状态）
│   ├── AchievementPredicates.cs     # 68 个纯谓词 + 分派
│   ├── AchievementCounters.cs       # 累计量 ①~⑧ 的更新（唯一写入点）
│   ├── AchievementOutcome.cs        # 判定结果 record + 待落盘增量
│   ├── AchievementTrigger.cs        # FR-009 的 12 个动作入口枚举
│   ├── AchievementSource.cs         # Built / Pending（6 条待接线）
│   ├── ExtinctionJudge.cs           # 绝嗣判定（在册口径 + 冻结）
│   ├── ChronicleBuilder.cs          # 编年史七项一次算定
│   ├── TukeyTrimean.cs              # 三均值（decimal / 3 位 AwayFromZero）
│   └── AchievementQuery.cs          # 只读查询面（供 U1/U2，MUST NOT 持久）
├── Config/
│   ├── AchievementPolicy.cs         # 【新】68 条阈值的唯一真源
│   └── GameConfig.cs                # 【改】+1 转发视图 Achievements（零字面量）
└── Save/TurnRunner.cs               # 【改】①~⑤ 编排；顶部结束拒绝；ctor +1 可选参 IAchievementStore?

src/KFL.Infrastructure/
├── Abstractions/
│   └── IAchievementStore.cs         # 【新】Read() / Flush(增量, 出身达成段)；MUST NOT 抛
├── Achievements/                    # 【新目录】
│   ├── AchievementStore.cs          # 【新】实现；复用 AtomicFileWriter；损坏改名隔离
│   ├── AchievementArchive.cs        # 【新】只读全局面 + 诊断
│   ├── AchievementFlushResult.cs    # 【新】落盘结果（成功 / 原因 / 隔离路径）
│   └── AchievementArchiveJson.cs    # 【新】JSON 形状 + 自带 Version = 1
└── Save/
    ├── SaveFileFormat.cs            # 【改】Version 4 → 5
    ├── SaveFileHeader.cs            # 【改】陈旧注释「恒为 3」→ 5
    └── Serialization/
        ├── SaveStateDto.cs          # 【改】8 → 10 键（[8] achievements / [9] ending）
        ├── SaveStateValueDtos.cs    # 【改】+5 DTO：AchievementStateDto / AchievementMarkDto / PendingUnlockDto / EndingStateDto / ChronicleDto
        ├── MessagePackSaveFormat.cs # 【改】SaveStateMemberCount 8 → 10 + 5 个新成员数常量 + Skip 序列 6 → 8
        ├── SaveStateMapper.cs       # 【改】两侧补状态 + internal 复原通道（保持元素次序）
        └── SaveStateValidator.cs    # 【改】+7 条（不夹带可重算派生值）

src/KFL.App/                         # 【改】组合根：TurnRunner 注入 AchievementStore（可选参，可暂缺省）
src/KFL.Presentation/                # 【不动】U1/U2 才接 AchievementQuery

tests/KFL.Tests/
├── Rules/
│   ├── AchievementCatalogTests.cs    # 【新】68 行 / 三元组 / 七系列计数 / Pending 恰 6
│   ├── AchievementPredicateTests.cs  # 【新】68×2 = 136 条参数化（触发 + 恰好差一格）
│   ├── AchievementAdjudicatorTests.cs# 【新】唯一次序、幂等、不解锁重复项、结束态冻结
│   ├── AchievementTriggerTests.cs    # 【新】12 个入口成功后才上报、被拒不上报、不落盘
│   ├── AchievementCounterTests.cs    # 【新】累计量 ①~⑧ 的同月同步与不可重算验证
│   ├── ExtinctionTests.cs            # 【新】绝嗣四正负例（外嫁不计）+ 必测清单第 9 条
│   ├── ChronicleTests.cs             # 【新】七项逐项取值（含跨月冻结）
│   ├── TukeyTrimeanTests.cs          # 【新】八行金色算例 + 判别例（≠ 36.250）
│   ├── AchievementQueryTests.cs      # 【新】查询面纯度（不写状态、不消费随机）
│   └── AchievementDeterminismTests.cs# 【新】同种子逐字段一致（SC-003）
├── Infrastructure/
│   ├── AchievementStoreTests.cs      # 【新】落点/格式/去重/最早日期/兼容/隔离/Origins 排除项
│   ├── AchievementStoreFailureTests.cs # 【新】写失败不阻断 + 待落盘保留 + 恢复后原年月
│   ├── AchievementSaveFormatTests.cs # 【新】键表/两格式等价/往返/Version 5
│   ├── EndedSaveTests.cs             # 【新】拒绝推进 + 零状态变更 + 仍可写读改备份 + 往返
│   └── SaveStateComparer.cs          # 【改】+比较 Achievements 与 Ending（含次序）
└── Architecture/
    └── AchievementPendingWiringTests.cs # 【新】6 条待接线登记 + 0 处自造规则（SC-002）
```

**Structure Decision**：`KFL.Rules/Achievements/` 是本特性唯一的规则层新目录——把「判定 + 目录 +
累计量 + 绝嗣 + 编年史」整体收在一处，避免 68 条谓词散落到 `Settlement/` 里与 002 的收支语义混读。
`KFL.Infrastructure/Achievements/` 与 `Abstractions/IAchievementStore.cs` 分开，是照 `ISaveService`
（接缝在 `Abstractions`、实现在 `Save/`）的既有先例**成对**落地：全局面是**跨档**的元进度，
与 `Save/` 的「当前档」语义不同域，混在一个目录下会让「谁拥有版本号」变得含糊（本特性的文件带
**自己的** `Version = 1`）。判定器**不**注入 store 而只吃只读参数，是本计划最重要的一条结构决策
（`research.md` R-01）：它让 68 条判据能在不装配 Infrastructure 的纯规则单测里跑，也让
SC-003 的「同种子逐字段相同」不被文件面污染。

## Complexity Tracking

| 新增抽象 | 为什么现在需要 | 更简单的替代为何被否 |
| --- | --- | --- |
| 接缝 `IAchievementStore`（+ 实现在 `KFL.Infrastructure`） | 成就是**跨存档**的全局面（FR-017 落在 `ISaveService.RootDirectory`），而规则层 MUST NOT 触文件系统（G-07）；章程 II 与 001 `injection-seams.md` §5 早已点名该接缝「等各自阶段带着真实需求回来定义」 | 直接把 `File.ReadAllText` 写在 `TurnRunner`（`KFL.Rules`）里 ⇒ G-07 扫描变红，且单测无法在无文件系统的环境跑；或塞进 `ISaveService` ⇒ 会让「当前档」的接缝同时承担跨档元进度，`RootDirectory` 之外的版本语义无处声明 |
| 全局文件**恒 JSON**（不做 MessagePack 侧） | 全局面独立于存档格式；同一玩家的 `achievements.json` MUST 在 `.dat` 与 `.json` 两种存档下**可互认**（FR-017 逐字「格式恒为 JSON」） | 「跟随当前存档格式」⇒ 同一个玩家换格式后成就面分裂成两份，FR-018 的「最早日期」被撕成两套；且要在全局面复用 `MessagePackSaveFormat` 的头记录与版本闸门，抽象成本高于收益 |
| `AchievementState` **恰 8 项**累计量（而非常见的「全量事实日志」） | SC-014 的价值就是**压住**持久面的宽度；FR-013 逐条论证了每一项都**不可重算**（`data-model.md §2.4` 的证明表），而 ACH-034/042 经「即时触发 + 状态面」化解，不必加第 9 项 | 落一份「全量事实日志」（每次死亡/出生/行贿/贷款都追加一条） ⇒ 存档体量随回合数线性膨胀，且与既有 `PunishmentJournal` / `Ledger` 的事实行重复，违反 004 契约「事实行 DTO 的键数 MUST == 对应记录成员数」的纪律 |
| `TukeyTrimean` 独立成类（而非内联进 `ChronicleBuilder`） | 三均值是本特性**唯一**一处非线性统计，且被 SC-009 的八行金色算例逐行钉死（含 `[10,20,30,40,100] → 30.000` 的**判别例**）；它与编年史的组织职责完全不同 | 内联 ⇒ 判别例只在 `ChronicleTests` 的间接断言里，改动时不易定位；且 ACH-012 的存续年数口径将来若需要同族统计，会诱发第二份实现 |
| 配置类 `AchievementPolicy` + `GameConfig.Achievements` 纯转发 | FR-001 要求数值单点声明；`GameConfig` 的既有 21 个 `Config/*.cs` 已按领域切分，`GameConfig` 只做转发是既有约定（`ConfigLiteralTests` 会守） | 把 68 条阈值写成判据里的字面量 ⇒ `ConfigLiteralRules.Evaluate` 扫描 `src/**` 时变红；塞进 `GameConfig` 本身 ⇒ 违反「`GameConfig` 零字面量」的既有纪律 |

## 阶段 0 / 阶段 1 小结

`research.md` 的 21 项抉择全部有结论，未决事项 = **无**（`spec.md` 的 16 条需求检查项已 16/16 通过，
Q1~Q3 与次级口径 A~D 已把数值与语义层面全部封死）。其中**五项是真问题**，逐条摘出：

1. **R-01 判定器不许读文件**：ACH-047（`四民皆历`）需要读**全局**出身达成段，而
   `RulesTestHarness`（`tests/KFL.Tests/Rules/RulesTestHarness.cs:18`）不装配 Infrastructure 的存档面，
   且 G-07 扫描 `src\KFL.Rules\`。抉择：判定器**不注入** `IAchievementStore`，
   由 `TurnRunner`（唯一有文件面的层）读好之后以**只读参数** `archiveMarks` 传入。
   替代方案「往判定器里注入 store」被否——它会让 68 条判据无法在纯规则单测里运行。
2. **R-04 累计量为什么恰好 8 项**：`FR-013 ①~⑧` 逐条都有「不可重算」的证明（`data-model.md §2.4`），
   两条曾被怀疑需要第九项的判据（`ImperialGroomPick` ACH-034、`DebtBurden` ACH-042）
   分别化解为「当月事实 ∪ 状态面」与「状态面 ∪ 即时上报」。抉择：**严格 8 项**，SC-014 以此为准。
   被否的替代：加第九项「历史最大贷款本金」——`Principal` 只在借款时**增大**，故状态面已足够。
3. **R-05 三均值的奇数口径**：`Assumptions 7` 与规格书都没写「奇数样本时中位是否计入两半」，
   而两种口径在 `[10,20,30,40,100]` 上分别得 **30.000** 与 **36.250**。
   抉择：取 R `fivenum` 的 Tukey hinges 口径（中位计入两半），并让测试**同时**断言
   `30.000` 与 `!= 36.250`，使这条口径无法被「顺手简化」悄悄改掉。
4. **R-13 全局落盘点的唯一性**：动作型即时判定（FR-009 的 12 个入口）会产生解锁，
   但若每个动作都写全局文件，就与 FR-007「月末一次判定」的确定性叙事冲突，且写入次数不可控。
   抉择：**唯一**写入点 = `TurnRunner.Advance` ⑤；动作型解锁只入档内 `PendingUnlocks`，
   由**下一次**回合末统一落盘（失败则保留并在更下一次重试，但仍写**原解锁年月**）。
5. **R-20 与既有测试的冲突面**：`Version` 4 → 5 看似会波及大量断言。
   核查结论：既有断言**全部**用 `SaveFileFormat.Version` **常量**写法
   （`SaveFormatContractTests.cs:37,56,270`、`SaveStateValidatorTests.cs:356`、
   `SaveFailureTests.cs:156-157,183-184,477,503,585-586`）⇒ **0 处需改**，只有 2 处陈旧注释需同步。
   `TurnRunner` 的新参数取**可选**默认 `null`，使既有构造点不必改动。

## 下一步

`$speckit-tasks` 依据本计划与 `data-model.md` / `contracts/` / `quickstart.md` 生成 `tasks.md`，
并 MUST 登记「回写义务清单」的 10 项范围（含历史记录类工件的**未回改**声明与 SC-013 的逐条核对结果）。
**任务排序前置**：按 `checklists/requirements.md` 的第 ⑦ 条，若 Q3 的 `Version` 自增落地，
`tasks.md` 的第一个任务 MUST 是**存档格式与 004 契约的同步**（含全部存量版本断言的更新，SC-013）。
