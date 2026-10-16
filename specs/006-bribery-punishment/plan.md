# Implementation Plan: 贿赂全链路（行贿、12 格惩罚矩阵、连坐与服刑）

**Branch**: `006-bribery-punishment` | **Date**: 2026-10-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-bribery-punishment/spec.md`

## Summary

交付规格书 §16 **逻辑轨 ⑥「贿赂全链路（12 格矩阵/连坐/服刑/死刑）」**：把「用钱换录取率、可能把自己
送进牢里」这条链落成 `KFL.Rules` 里可纯函数断言、可逐次钉住随机消费的**行贿行动**与**新增月末结算整
步**，并交付供 U1/U2/U4 与 ⑧ 消费的**纯查询面**与**当月事实快照**。规格书 §7 的全部要求在本特性内闭合。

- **US1（P1）行贿**：行贿是**结算之外的玩家动作**（与 005 的报名同类）。窗口 = **考试当月及前 1 个月**
  （解试 8+7 月 / 省试 3+2 月 / 殿试 4+3 月）；计数维度 = **同一考生 × 同一考试级别、跨科年累计**（新
  增 `Person` 三个非负整数）；第 n 次金额 = 基础金额（解试 100 / 省试 500 / 殿试 2000 贯）×
  `(1 + 0.2×(n−1))` × **难度支出系数**；被抓概率 = 基础率（5% / 15% / 35%）× `(1 + 0.2×(n−1))` ×
  **贿赂风险系数**（0.2 / 1.0 / 1.5 / 3.0），clamp 到 1.0。**当刻**经 `PaymentPrimitive.Pay` 扣款
  （现金 → 储蓄 → 余额转贷款）并**当刻**掷**恰好 1 次** `NextDouble()` 判定被抓；**无论被抓与否**该级别
  乘数累计为 **kⁿ**（k = 1.6 / 1.3 / 1.1，殿试作用于**进甲率**；n **含被抓的那些**，Q1 = B）；落榜**不退钱、
  无补偿**；乘数把最终录取率/进甲率推到 `0.899999` ⇒ 该级别行贿入口**置灰**且入口**拒绝**（状态逐字段
  不变、随机消费 0）。
- **US2（P2）12 格惩罚矩阵**：被抓当刻按**当前难度 × 考试级别**命中一格并施加全部后果——服刑（以月计）、
  罚金（**总资产**基数的比例或固定额，比例罚金**全域封顶 50000 贯**，经同一支付原语付，付不起**不阻止
  惩罚、不产生负余额**）、相关亲属禁考与/或禁升 N 月（简单档「无禁考」⇒ 计时 `null`；困难/地狱的解试与
  省试「只禁考」⇒ 禁升计时 `null`）。
- **US3（P3）服刑推进与刑满释放**：服刑/禁考/禁升三种计时**独立并行**按月递减，成为月末结算的**固定新增
  第 ③ 步**（插在「②米价游走」之后、「官吏推进」之前，原 ③~⑧ 顺移为 ④~⑨，**合计九步**；Q2 = A）；
  服刑者**保留在册、不计口**（不耗口粮、不产生收益与功名），**待阙/在职计时与政绩一律暂停**（不推进、
  不重置）；刑满释放清除计时与状态位、**功名保留**、**曾有官职者降 1 级官阶**（L18 维持）、**仍在禁考期
  则继续禁考**；现任官员在禁考期内**正常任职领俸**。
- **US4（P4）地狱殿试连坐**：对当事人「配偶、儿子、女儿、父亲、母亲」中的**在世者**逐人判定三分支——
  **无功名且无官职 ⇒ 处死**（置已亡并归档）、**有功名无官职 ⇒ 降一级功名**（`PunishmentDemotion`）、
  **有官职 ⇒ 全族禁考禁升 48 月**；免死例外（配偶有官职 ∧ 孩子未成年）只对孩子成立；**已出嫁归档的女儿
  被跨族追及**；范围严格限于五类亲属；**死刑立即执行**（MUST NOT 建立服刑计时）；其余 11 格 MUST NOT 连坐。

技术路线（沿用 001~005 的分层口径）：**状态与不变量归 `KFL.Core`，规则判定与编排归 `KFL.Rules`，
落盘形状归 `KFL.Infrastructure`（只随字段同步，不新增依赖）**。

- `KFL.Core`：`Person` **+3 个非负整数**（三级别贿赂计数，只读 + 具名受控写入通道 + 一条 `internal`
  复原通道）；`Family` **+1 个成员**（`PunishmentJournal`——**当月事实缓冲**，第③步读取后清空）；
  `LedgerCategory` **+2 个取值**（`BribeExpense` / `Fine`，皆资金类、恒为负；**20 → 22**）；
  新增 **2 个文件**（`Entities/PunishmentJournal.cs`、`ValueObjects/PunishmentFacts.cs`）。
- `KFL.Rules`：新增 `Bribery/`（`BribeRules` 纯函数、`BribeEntry` 行动与调试重置、`BribeOutcome`）与
  `Punishment/`（`PunishmentImposer` 施加矩阵、`PunishmentAdvance` 结算第③步、`PunishmentSnapshot`
  快照、`PunishmentQueries` 查询面、`PunishmentCommands` 调试入口）共 8 个文件 +
  `Config/BribePolicy.cs`、`Config/PunishmentPolicy.cs` 与 `Economy/TotalAssetsCalculator.cs`；
  `MonthlySettlementEngine` **八步 → 九步**（惩罚推进插在 ② 与「官吏推进」之间）、
  `SettlementResult` 新增 `Punishment` 必填子段、`GameConfig` 加 `Bribe` / `Punishment` 两个转发视图
  与 `Assets.TotalAssets`。
- `KFL.Infrastructure`：**不新增文件、不新增依赖**；只随字段同步两套编解码的键表/成员数常量、映射器、
  校验器，并把 `SaveFileFormat.Version` 由 **2 升为 3**（FR-032：**MUST NOT** 实现旧→新迁移）。

本特性**不做**任何界面（主界面/下月/快进/双视图/统计页/调试面板/设置属 U1~U5）、不做成就解锁与绝嗣
（⑧；只产出事件流与计数）、不做成长衰老疾病死亡（⑦）、不做婚育与买人口（⑦）、不做存档迁移。

## Technical Context

**Language/Version**: C# 14 / .NET 10（沿用 001~005 基线；`global.json` 不动，仍只锁 `10.0` 版本带）。
非 UI 三层 `net10.0`、UI 两层 `net10.0-windows`；**不新增工程**，仍六工程。

**Primary Dependencies**: **零新增 NuGet 依赖**。只使用既有依赖（xUnit 测试面）；`KFL.Core` 与
`KFL.Rules` 不引入任何新包，也不触碰 `CommunityToolkit.Mvvm` / `MessagePack` / `Bogus` / `Serilog` /
`LiveChartsCore` 的既有引用。

**Storage**: 不新增持久化设施，只**扩充既有存档形状**（004 的线上形状是**位置式数组**，键自 0 连续）：
`PersonDto` **25 → 28**（末尾追加 `jieShiBribeCount` / `shengShiBribeCount` / `dianShiBribeCount`，
键 25/26/27）、`FamilyDto` **7 → 8**（末尾追加 `punishmentJournal`，键 7，其内为新增的日志 DTO 行）；
`MessagePackSaveFormat` 的 `PersonMemberCount` **25 → 28**（`MessagePackSaveFormat.cs:50`，尾随
`Skip(ref reader, 3)` `:254` → `Skip(ref reader, 6)`）与 `FamilyMemberCount` **7 → 8**（`:47`，
`:226` 的 `Skip(ref reader, 1)` → `Skip(ref reader, 2)`）；`SaveFileFormat.Version` **2 → 3**
（`src/KFL.Infrastructure/Save/SaveFileFormat.cs:30`）。`KFL.Core` 与 `KFL.Rules` 仍是**零**文件系统
访问（G-07 零违规）。

**Testing**: xUnit v2 + VSTest，`dotnet test KejuFuShengLu.slnx -m:1 -nodeReuse:false
-p:_MSTestEnableParentProcessQuery=false`（受限宿主的两个已知坑见 `AGENTS.md`）。新增
`tests/KFL.Tests/Rules/{BribeEntryTests,PunishmentMatrixTests,PunishmentAdvanceTests,
CollectivePunishmentTests,BribeQueryTests}.cs`；同步 `Rules/{SpecAnchorTests,DeterminismTests,
ExamAdmissionTests,ExamSeasonTests}.cs`、`Core/{PersonTests,StatusTests,LedgerInvariantTests}.cs`
（`PersonTests.cs:16-35` 的「无写入通道成员」全集 **16 → 19**；`LedgerInvariantTests.cs:87` 的全集
**20 → 22**、`:106-112` 的禁用词守卫、`:146-158` 的无入账目标集合 **10 → 12**、`:264-275` 的恒为负
集合 **5 → 7**、`:59-78` 的 `[Theory]` **19 → 21** 行）、`Architecture/ConfigLiteralTests.cs`
（`RegisteredValues` **46 → 49**，见下）、`Infrastructure/{SaveFixtures,SaveStateComparer,
SaveFormatContractTests,SaveStateValidatorTests,SaveFailureTests}.cs`。全部测试**无界面、无文件系统、
无网络**（存档往返沿用 004 的临时目录沙箱），随机一律经可设种子的 `IRandomService`（章程原则 IV、G-07、
FR-033）。

**Target Platform**: Windows（WPF 表现层，本特性不涉及）。规则层不引用任何平台类型。

**Project Type**: 桌面应用（WPF + MVVM）+ 分层类库，单仓库六工程（结构不变）。

**Performance Goals**: 无运行时性能目标（§3：逻辑推进仅由用户输入驱动）。可衡量的只有门禁本身
（构建零警告零错误、测试全绿）与 SC-001~SC-011 的可断言性——其中 SC-001 要求**固定种子跑完一条
「行贿 → 被抓 → 惩罚 → 服刑 → 释放」完整链，两次独立运行逐字段差异项 = 0**。

**Constraints**:
- 章程原则 II：`KFL.Rules` MUST NOT 出现 `DateTime.Now` / `Random` / 文件系统 / 网络（G-07 静态断言）；
  本特性的**全部**随机调用（行贿掷骰**是唯一新增的随机消费点，且发生在结算之外**）必须经注入的
  `IRandomService`——结算内的**惩罚推进步消费随机 0 次**，整月随机次序因此**逐字不变**
  （[契约十六 §1.3](./contracts/punishment.md#13-整月随机消费次序006-后仍逐次断言)）。
- 章程原则 VI：规则层 MUST NOT 感知调用方；封顶与「放宽上限」能力 MUST 经**装配期显式注入的配置或
  策略**提供，MUST NOT 在规则层留按调用方分支的绕过路径（FR-010、FR-031）。
- 规则数值 MUST 集中在 `KFL.Rules/Config/`：本特性新增两个数值文件（`BribePolicy.cs`、
  `PunishmentPolicy.cs`）。`ConfigLiteralTests.RegisteredValues`（`ConfigLiteralTests.cs:54-117`，
  现 **46** 项）**只**新增 **3** 项小数——`1.6m`（解试 k）、`0.15m`（省试被抓基础率 15% 与罚金比例
  15% 同值，只登记一次）、`0.35m`（殿试被抓基础率 35%）；其余数值**已登记或为整数**：
  `0.2m`（累进步长，`:66`）、`0.02m`（简单档殿试罚金比例，`:76`）、`1.1m` / `1.3m`（殿试 / 省试 k，
  `:68`）、`1.5m`（困难风险系数，`:74`）、`0.01m`（解试率与 1% 罚金比例，`:106`）、`0.05m`（`:108`）、
  `0.1m`（`:72`）、`0.899999m`（封顶，`:112`）已存在；`1.0m` / `3.0m` 与 100 / 500 / 2000 / 50000 /
  36 / 24 / 48 等整数规则值**一律不入单**（`ConfigLiteralTests.cs:115-116`）。
- 依赖边不新增：`KFL.Core ← KFL.Infrastructure ← KFL.Rules`（G-05 不变）；MUST NOT 在 `KFL.Rules`
  之外的层定义任何结算接缝接口；**本特性不新增任何接缝接口**（001 `contracts/injection-seams.md` 不动）。
- `.slnx` 内 MUST 仍恰好六个 `<Project Path=… />`（G-02）；`tests/KFL.Tests/KFL.Tests.csproj:21-25`
  的 `_MSTestEnableParentProcessQuery` MUST 保持**注释态**提交。

**Scale/Scope**（预估，实现期按实际现状回写）：

- `KFL.Core`：**增 2** 个文件（`Entities/PunishmentJournal.cs`、`ValueObjects/PunishmentFacts.cs`）、
  **改 4** 个（`Entities/Person.cs`、`Entities/Family.cs`、`Enums/LedgerCategory.cs`、
  `Enums/LedgerCategoryMetadata.cs`）；**3** 个领域字段新增 + **1** 个家族成员新增 +
  **2** 个账本类别新增；**0** 个新工程、**0** 个新接缝接口。
- `KFL.Rules`：**增 11** 个文件（`Bribery/` 3 + `Punishment/` 5 + `Config/` 2 +
  `Economy/TotalAssetsCalculator.cs`）、**改 3** 个（`Settlement/SettlementResult.cs`、
  `Settlement/MonthlySettlementEngine.cs`、`Config/GameConfig.cs`）+ 文档级 **1** 个
  （`Exam/ExamAdmission.cs` 的两处 `<param>`）。
- `KFL.Infrastructure`：**增 0** 个文件（无新依赖）、**改 6** 个
  （`Save/SaveFileFormat.cs`、`Save/Serialization/{SaveStateDto,SaveStateValueDtos,
  MessagePackSaveFormat,SaveStateMapper,SaveStateValidator}.cs`）。
- `KFL.Presentation` / `KFL.App`：**0** 个界面功能、**0** 行界面代码。
- 测试：**增 5** 个文件、**改 ≥10** 个既有文件（断言物同步）。
- **1** 处线上形状版本提升（`SaveFileFormat.Version` 2 → 3），**0** 条迁移路径。

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 章程条款 | 本计划如何满足 | 结论 |
| --- | --- | --- |
| **I. .NET 10 单一运行时基线** | 不新增工程、不改任何 `TargetFramework`；`global.json` 不动；**零新增依赖** | ✅ |
| **II. 分层解耦与单向依赖** | **状态与不变量**归 `KFL.Core`（3 + 1 个字段与具名写入通道、2 个账本类别、1 个当月事实缓冲）；**全部判定与编排**归 `KFL.Rules`（`Bribery/` + `Punishment/` + 两个数值文件）；**落盘形状同步**归 `KFL.Infrastructure`；`Core` 不引用 `Rules`（G-05 不变）；`Core` / `Rules` 零文件系统访问（G-07）；规则逻辑为纯函数或仅依赖可注入的 `IRandomService` | ✅ |
| **III. WPF + MVVM 表现层契约** | 无 ViewModel、无 View、无 code-behind 改动；`KFL.Presentation` 与 `KFL.App` **一行不动**（界面属 U1~U5，spec 的 Out of Scope 已逐项排除）。**不新增任何界面接口**——U1/U2/U4 与 ⑧ 只消费 `PunishmentQueries` / `BribeRules` 的**纯查询**（FR-029） | ✅（N/A 已说明） |
| **IV. 单测优先与结果确定性** | 五个关注点各一个测试文件（行贿行动 / 矩阵逐格 / 服刑推进与释放 / 连坐与免死 / 查询面与封顶）；**全部**经可设种子 `IRandomService`；无界面/文件系统/网络；§16 必测清单的三项（贿赂累进与封顶、12 格矩阵逐格断言、连坐范围）逐条有锚点；测试与实现同批提交；SC-001 要求两次独立运行差异项 = 0 | ✅ |
| **V. 约定式提交与中文提交信息** | `feat(core)`（三计数 + 事实缓冲 + 两个账本类别）、`feat(rules)`（`Bribery/` + `Punishment/` + 数值 + 结算第③步 + 快照 + 查询面）、`feat(infrastructure)`（键表/版本）、`test(...)`、`docs(...)`（上游活工件回写与 §17 裁决回写）各起一次；不混装 | ✅（流程约束） |
| **VI. 调试通道不复制规则** | 本特性不建控制台（U4）；FR-031 的五个调试入口是**普通公开规则入口**（重置某成员贿赂计数（按级别或全部）/ 解除自身禁考禁升 / 解除全族禁考禁升 / 释放囚犯 / 赦免死囚），规则层**无按调用方分支**；封顶常量单点于 `BribePolicy`（MUST NOT 在 U4 或 ViewModel 复制）；「放宽上限」不在规则层新建绕过通道（见 Assumptions 的开放项） | ✅ |
| **技术栈与工程约束** | `.slnx` 唯一、六工程不变；无游戏引擎与外部美术资源/emoji；金额以文为单位的 `decimal`（`Money`）；新增数值集中 `KFL.Rules/Config/`；无日志代码 | ✅ |
| **开发工作流与质量门禁** | 严格对位 §16 逻辑轨 ⑥（本特性即该条，非跨阶段提前动工）；门禁四条可执行；复杂度逐项论证（见 Complexity Tracking） | ✅ |
| **需求真源** | 三项用户裁决（Q1/Q2/Q3，2026-10-12）已按规格书 §17 回写规格书 §7.1 / §7.4 / §7.5 并由本计划继承；`plan.md` / `research.md` / `data-model.md` / `contracts/` 逐条标注规格书章节号；本特性**未自创任何影响平衡的数值**（全部逐字取自 §7.1 / §7.2 / §7.3 / §7.4 / §7.5 / §11，见「勘误登记」） | ✅ |

**设计后复核（Phase 1 之后）**：✅ 无新增违规。`data-model.md` 与三份契约未引入任何需要放宽章程的结构：
仍是六个工程、无新接缝层、无跨层引用、无界面代码；契约十五/十六/十七只描述既有分层内的接口、次序与
数值归属；`SettlementResult` 的新增子段、`PaymentPrimitive.Pay` 的可选形参与存档版本提升都已在
「回写义务清单」登记为**跨特性形状变更**，且**先在变更源 `spec.md` / `plan.md` 声明**（AGENTS.md 铁律）。

### 继承的用户裁决（2026-10-12）

| 编号 | 议题 | 裁决 | 落点 |
| --- | --- | --- | --- |
| **Q1** | §7.1 写「第 n 次行贿**未被抓获后**」乘数累计为 kⁿ，但未写被抓当次是否产生乘数、是否清零、是否退钱 | **B：指数 n = 行贿总次数（含被抓的那些）**——被抓当次照样计入 n 并推进乘数（**k 的指数 = 该级别累计计数**），被抓**不清零**该级别计数与乘数、**贿款不退**、无补偿条目 | FR-008、US1、SC-003；[契约十五 §4](./contracts/bribery.md#4-效果乘数-kⁿ与封顶) |
| **Q2** | §7.5 规定服刑以月计、禁考与服刑独立并行、期满可考、释放降 1 级，但 §3 的步骤序列未写惩罚计时在哪一步递减 | **A：惩罚推进步插在「官吏推进」之前**，成为新的第 **③** 步（原 ③~⑧ 顺移为 ④~⑨，**合计九步**）——递减 → 释放/降级/解除在同月的仕途推进与科举季**之前**生效 | FR-020、FR-035 ②④；[契约十六 §1.1](./contracts/punishment.md#11-在月末结算中的位置) |
| **Q3** | §7.4 写「有官职 → 受全族禁考禁升职 48 月覆盖」，但 §7.5 的禁考/禁升计时是逐人字段（载体未定） | **A：逐人计时**——施加当刻对**每名在世在册成员**各写 48 月禁考 + 48 月禁升（复用 001 既有状态位与 `StatusTimers`，**不新增家族级真源**、**不改 005 的资格判定**）；施加后新出生/娶入者不受该次覆盖；U4 的「解除全族禁考/禁升」= 一次性清除全部在册成员的相应计时 | FR-021；[契约十六 §6](./contracts/punishment.md#6-连坐与免死fr-022fr-026) |

## 勘误登记（计划阶段）

> 本表只登记**计划阶段发现的工件内部矛盾**，逐条按规格书 §17② 取「对玩家最直观的解释」，并**登记为
> 待回写项**；三者都**不新创任何影响平衡的数值**。

| # | 位置 | 原文 | 计划口径 | 依据 |
| --- | --- | --- | --- | --- |
| 1 | [spec.md](./spec.md) US2 AS1（L131） | 「账本恰 1 条罚金条目 + **1 条服刑事件记录**」 | 「服刑事件记录」落在**惩罚快照**（`SettlementResult.Punishment.Sentences`），**不落账本**：FR-027 明令新增账本类别**只有资金类**两个（行贿支出、罚金）；001 的账本二分类里「服刑」不引起资金池变动，且 §14.1 的成就只消费事件流与计数 | FR-027；契约四 §2；[契约十七 §3](./contracts/config-registry.md#3-禁止项本特性不得引入的数值) |
| 2 | [spec.md](./spec.md) FR-032（L380-382） | 新增状态枚举含「**家族级行贿计数**与「从未行贿」事实」 | **两者都不新增持久字段**：①「家族级行贿计数」= 逐人三项计数按 `Family.Members`（含归档）求和（**派生量 MUST NOT 入档**，004 不变量 7）；②「从未行贿」= 由「存在 `ExamPass` 的进士记录」∧「三项计数皆为 0」**派生**（`PunishmentQueries.WasBribeFreeAtImperialPass`）。派生结果在两种格式往返后**逐字段相等**，FR-032 的验收判据仍成立 | 004 data-model §8 不变量 7；章程原则 V；§17② |
| 3 | [spec.md](./spec.md) FR-030 / Key Entities | 「每位进士从出生到及第全程未行贿」 | 该事实为**派生谓词**（见 #2）。已知边界：进士被**连坐降级**后再行贿时，该谓词会给出假阴性——规格书未覆盖此组合，按 §17② 取「以现行计数为准」并**登记为待回写项**；消费者（⑧）MUST 只经该谓词取值 | FR-030；§17②；[契约十五 §6](./contracts/bribery.md#6-派生查询面fr-029-fr-030) |
| 4 | [spec.md](./spec.md) SC-005（L469）与 US2 AS5（L138-139） | 付不起罚金时「账本条目金额合计 = 罚金额」（例：罚金 120 贯 ⇒ 条目合计 120 贯） | **改述**：`Fine` 条目的金额 = **池内实际付出额**（现金 + 储蓄被扣掉的部分）；缺口转贷款**不落资金条目**，故罚金 120 贯 / 池 30 贯 时条目金额 = 30 贯、贷款本金 +90 贯。`PaymentResult.Total`（池内付出 + 转贷款）仍恰等于罚金额，罚金总额不因本口径打折 | 规格书 §12.3 的 §17 裁决回写（**资金类条目金额 = 本次的资金池变动额**）+ §4.3「罚金支付顺序：现金 → 储蓄 → 余额转贷款」；002 契约三 §7 / data-model §4.3（缺口转贷款不改资金池、不落条目）。**2026-10-13 实现期（T029）发现、已按真源修正 spec.md 与本节** |
| 5 | [contracts/punishment.md](./contracts/punishment.md) §1.2 表 6 / §3 条款 1 / §7 ①、[data-model.md](./data-model.md) I-5 | 「置位次序**先计时后状态位**，清除时相反」 | **反了**：001 起 `Person.Status` / `Person.Timers` 的交叉校验 setter 强制「建立 = **先状态位后计时**；清除 = **先清计时（写 `null`）后清状态位**」——反序必抛 `ArgumentException`（`Core/StatusTests.cs` 的待阙/服刑两条用例已如此断言，`RulesHarness.ServingSentence` 等夹具亦然）。裁定：**以既有实现与既有测试为准**，工件按此修正 | 001 `data-model.md` §1.7 不变量 4 与 `Person` 两个 setter 的实现；`Core/StatusTests.cs:110-127`。**2026-10-13 实现期（T037/T042）发现、已按真源修正** |
| 6 | [data-model.md](./data-model.md) §2.2 / §2.3 / §3.2 / §3.9 / §7.1 | `FineFact(…, PaymentResult? Paid)`、`internal void Clear()`、`BribeWindowDays`、`RelativeBanMonthsOf` / `DifficultyExpenseFactor`、事实行 DTO 键数 6 / 7 | ① `PaymentResult` 属 `KFL.Rules`，放不进 `KFL.Core`（G-05）⇒ 改为 `FineFact(Date, Subject, Amount, Money PaidFromTreasury, Money ToLoan)`，与 `FineFactDto` 的 5 键**逐一对应**；② 按「DTO 键与记录成员逐一对应」裁定 `RelativeBanFactDto` = **5**、`CollectivePunishmentFactDto` = **6**；③ `Clear()` 的唯一调用方 `PunishmentAdvance.Run` 在 `KFL.Rules`，而 001 的 `InternalsVisibleTo` **只放行 KFL.Infrastructure**（其注释明令不得增放）⇒ `Clear()` 改为 **public**；④ 成员名以实际落盘的 `WindowPreviousMonths` 为准；⑤ `RelativeBanMonthsOf` / `DifficultyExpenseFactor` **不实装**（职责由 `PunishmentCell` 与 `DifficultyRates.ExpenseFactor` 覆盖） | 章程「需求真源」；`src/KFL.Core/AssemblyInfo.cs`；G-05；契约十七 §1/§2 的登记表。**2026-10-13 实现期（T004/T030/T048/T049）发现、已按真源修正** |
| 7 | [contracts/punishment.md](./contracts/punishment.md) §6.3、[quickstart.md](./quickstart.md) 场景 5 期望 2 | 免死例外后写「该孩子**状态逐字段不变**」，与同句「全族禁考禁升 48 月照常适用」**自相矛盾** | **改述**：免死封印的是**连坐三分支**（不置已亡、不降功名、不产生 `CollectivePunishmentFact`），**不是**第 ⑦ 步的全族禁令——该孩子仍在世在册，`StatusTimers` 的两条禁令计时**照常写入**（与 §6.4「连坐范围 ≠ 全族禁令范围」同一口径） | 规格书 §7.4（全族 48 月对所有在世在册成员适用）；契约十六 §3/§6.4；`CollectivePunishmentTests` 的 `CollectiveFingerprint`（断言口径已排除计时）。**2026-10-13 实现期（T074 核对）发现、已按真源修正 006 自身的契约与 quickstart** |

> 两表勘误**只改口径的解释、不改规格书的任何取值**；#1~#3 已在本计划与 `data-model.md` 中固定，
> 并列入「回写义务清单」第 1 项（规格书 §7 的 `§17 裁决回写` 块）。#4 为实现期（T029）新发现，
> 已按真源（规格书 §12.3）**同批修正** `spec.md` 的 SC-005 与 US2 AS5。#5（置位/清除次序）与
> #6（记录/DTO 形状与可见性）为实现期（T004~T049）新发现，已按「以既有实现与既有测试为准」
> 同批修正 006 自身的 `contracts/punishment.md` 与 `data-model.md`。五条均列入 T073 的回写登记。

## 回写义务清单（AGENTS.md「铁律」+ spec FR-035 的落地）

**先说结论**：本特性在公开面上确实做了跨特性形状变更——① `Person` **+3 字段**、② `Family` **+1 成员**、
③ `LedgerCategory` **+2 取值**（20 → 22）、④ `SettlementResult` **+1 子段**、⑤ `MonthlySettlementEngine`
**八步 → 九步**（插在第 ③ 位）、⑥ `SaveFileFormat.Version` **2 → 3**、⑦ `PaymentPrimitive.Pay`
**+1 个可选形参**（`PersonId? personId = null`）。按 AGENTS.md 的铁律，这些变更**已在变更源
[spec.md](./spec.md)（FR-001/FR-011/FR-020/FR-027/FR-028/FR-031/FR-032）与本计划声明**，并 MUST 在
**同一提交**内完成下表回写。

> 下表的**行号为回写前的原位**，逐条回写后行号会移动，故每行同时给出**可检索的锚点文本**。

| # | 目标工件（活工件） | 回写内容（锚点） | 依据 |
| --- | --- | --- | --- |
| 1 | `科举浮生录规格书.md` §7.1 / §7.4 / §7.5（顺及 §7.2） | 追加「§17 裁决回写」块记入 Q1/Q2/Q3，并登记「勘误登记」#1~#3 的判读口径；原始条目文本按 §16.2 的历史事实口径**保留不改写** | spec `## Clarifications` |
| 2 | `specs/002-monthly-settlement-economy/contracts/monthly-settlement.md` | §1 步骤表 `:17-22` 插入**惩罚推进 = 新 ③**（原 ③~⑧ → ④~⑨，合计**九步**），并在 005 后记 `:29-33` 之后追加 006 后记；行内互引顺移（③ 的「俸禄差异经 ④ 入账」→ ⑤）；§4 次序段 `:76-77` 与表 `:79-86` 补「惩罚推进（③）—— **0 次**」并顺移 `③-d`→`④-d`、`④`→`⑤`、`⑥`→`⑦`、`⑦`→`⑧`；§7 支付原语行 `:185` 补「006 为首个产品调用方，并新增可选 `PersonId?` 形参」；§9 `MUST NOT 存在`清单 `:221-222` **移除**「罚金与 12 格惩罚矩阵」「连坐与服刑计时」并加 006 后记；文件末追加 006 快照面后记（惩罚子段） | FR-020、FR-028、SC-011 |
| 3 | `specs/002-monthly-settlement-economy/spec.md` | Status 行 `:7`（八步 → 九步 + 惩罚子段）；Out of Scope `:394-400`（把「贿赂金额与乘数、罚金矩阵、连坐与服刑/禁考计时推进」移出并加后记）；FR-020 `:306-316`（九步）；FR-017 `:292-293`（「无罚金调用方」作废）；Assumptions `:455-457`（支付原语先于罚金） | FR-028、SC-011 |
| 4 | `specs/002-monthly-settlement-economy/data-model.md` | §2.1 `LedgerCategory` 表 `:76-108`（加 `BribeExpense` / `Fine` 两行、20 → 22、「恒为负的支出类」5 → 7 类、「正额入账目标为 —」的 10 → 12 类）；§4.2 `SettlementResult` `:296-318`（新增 `Punishment` 必填子段）；§4.3 `:324` 与 `:331`（九步；`PaymentPrimitive.Pay` 新签名）；§5 映射 `:348`；§6.3 `:400-401`、§6.4 `:403-408`（移除「罚金与服刑计时（阶段⑥）」） | FR-011、FR-020、FR-028 |
| 5 | `specs/002-monthly-settlement-economy/contracts/config-registry.md` | §7 `:108-111` 把「§7 贿赂的全部数值（阶段⑥）」**移出**并加 006 后记，指向 [契约十七](./contracts/config-registry.md)；§6 加一行「贿赂与惩罚数值只指向不复制」 | FR-001 |
| 6 | `specs/001-core-skeleton/data-model.md` | `Person` 字段表 `:130-158` 末尾追加三项贿赂计数；不变量 2 `:175-192`（只读全集 + 受控写入通道）、不变量 4 `:194-200`；`StatusTimers` §1.7 `:106-124`（`:117` 的「服刑/禁考/禁升属逻辑轨 ⑥」由「明确不含」改为「已由 006 建立与递减」）；`DegreeRecord` §1.5 `:85-89`（006 是 `PunishmentDemotion` 的首个生产者）；`:379`（「服刑计时递减」由 001 的「不实现」列表移出） | FR-004、FR-014~FR-021 |
| 7 | `specs/003-opening-assets-officialdom/contracts/official-career.md` | `:8`（`PromotionBanned` / `ServingSentence` 由 **⑥ 建立**，本契约仍只消费）；§4 条款 8 `:84` 改写；§4 条款 6/7 `:82-83` 的「暂停 vs 跳过」保持并加交叉引用；§10 收尾清单 `:168` 移除「服刑与禁考/禁升计时的建立与递减、释放降级」；**新增**一条「释放降 1 级官阶」条款（**不是**改 `:81` 的考课晋升 `−1`） | FR-020、SC-011 |
| 8 | `specs/003-opening-assets-officialdom/data-model.md` | §1.2 `StatusTimers` 表 `:33`（`PromotionBanRemainingMonths` 由「只消费」改为「由 006 建立与递减」）、`:31-32` 同款；§3.3 明确不含 `:182`；§6.5 `:459-464` 移除已交付项 | FR-021、SC-011 |
| 9 | `specs/004-save-system/contracts/save-format.md` | §4.2 键表 `:77-78`（`FamilyDto` 7 → **8**、`PersonDto` 25 → **28**）与新增日志 DTO 行；版本闸门 `:109-130`（`FormatVersion == 3`，`3`/`2`/`1` 一律拒绝，谓词与条款 1/2 同步）；§7 常量登记 `:136`（**2 → 3**）；末尾追加 006 后记 | FR-032 |
| 10 | `specs/004-save-system/data-model.md` | §3 DTO 表 `:93-221`（`FamilyDto` +1 键、`PersonDto` +3 键、新增日志 DTO 行）；§3.11 复原次序 `:244-263`（新计数并入第 ② 步；日志复原独立一步）；§8 不变量 `:404-424`（新增不变量 18/19，`PersonMemberCount`=28 / `FamilyMemberCount`=8）；后记 `:440-456`（线上成员计数 **80 → 84**） | FR-032 |
| 11 | `specs/005-exam-cycle/contracts/exam-session.md` | §4.3 条款 4 `:155`（`NeutralBribeMultiplier` 占位**作废**——⑥ 已落地、经**同一入口**传实际值）；§9 条款 8 `:255`（「清白传家」计数已由 ⑥ 交付）；§10 表格 `:263` 把「贿赂的累进金额…服刑」移出；`:182` 的连坐降级来源标注 ⑥ | FR-008、FR-030、SC-011 |
| 12 | `specs/005-exam-cycle/spec.md` | FR-027 `:317`（「清白传家」的行贿计数**已由 ⑥ 交付**）；FR-016 `:268-271`（乘数占位作废）；Out of Scope `:354` 相邻条目 | FR-030、SC-011 |
| 13 | `src/KFL.Rules/Settlement/MonthlySettlementEngine.cs` 类文档 | 八步 → **九步**；`<list type="number">` 步骤清单插第 ③ 项；`:16` 的步骤数句与 `:33-37` 的随机次序段顺移序号并补「惩罚推进（③）—— 0 次」；「MUST NOT 存在」段 `:30-31` **不动**（惩罚推进是固定步骤，不是被排除项） | FR-020 |
| 14 | `src/KFL.Core/Enums/LedgerCategory.cs` + `LedgerCategoryMetadata.cs` | 类别计数 **20 → 22**：`:4` 的类文档「20 个取值」→ 22；`:15-16` 的「『贿赂』等阶段⑤⑥⑧ 的类别 MUST NOT 存在」条款改写；`LedgerCategoryMetadata.cs:23` / `:59` 的 `<exception>` 文档与 `:50-51` / `:90-91` 的两处 `default:` 异常文案「20」→「22」；`:33` 的「其余 15 个」→ 17、`:76` 的「恒为负的支出类（5 类）」→ 7 | FR-027、SC-011 |
| 15 | `src/KFL.Rules/Exam/ExamSeasonRunner.cs` + `Config/ExamPolicy.cs` + `Config/GameConfig.cs` + `Exam/ExamAdmission.cs` | 两处调用点 `:297` / `:323` 改传 006 计算的 `kⁿ`；`ExamPolicy.NeutralBribeMultiplier`（`ExamPolicy.cs:63`）与 `GameConfig.Exam.NeutralBribeMultiplier`（`GameConfig.cs:434`）**删除**（占位作废，k⁰ 由乘数入口的恒等元自然给出）；`ExamAdmission.cs:30` / `:57` 的两处 `<param>` 文档改写 | FR-008、SC-003 |
| 16 | 断言物（同提交，**不是**历史工件） | `Rules/SpecAnchorTests.cs:235`（占位锚点改为 `BribePolicy` 的 k⁰ 恒等元）；`Rules/ExamAdmissionTests.cs` 的 9 处与 `Rules/ExamSeasonTests.cs` 的 4 处 `NeutralBribeMultiplier` 引用；`Core/PersonTests.cs:16-35`（只读成员全集 16 → 19）；`Core/LedgerInvariantTests.cs`（`:87` 计数、`:89-104` 名称数组、`:106-112` 的 `Bribe` 禁用词守卫改写为「只允许 `BribeExpense` / `Fine`」、`:146-158`、`:264-275`、`:59-78`）；`Architecture/ConfigLiteralTests.cs:54-117`（`RegisteredValues` 46 → 49）；`Infrastructure/{SaveFixtures,SaveStateComparer,SaveFormatContractTests,SaveStateValidatorTests,SaveFailureTests}.cs`（键表、成员数常量与 walker Skip 计数、版本闸门用例、夹具非默认值、比较器、校验口径） | FR-001、FR-032、SC-002、SC-009 |
| 17 | `specs/006-bribery-punishment/tasks.md` | 登记上述回写范围（同步了哪些文件、同步到哪里），供后续审计核对 | AGENTS.md 铁律第 3 条 |

> **不改的**：`tasks.md`（001~005 的既有条目）、`implementation-notes.md`、既有 `checklists/` 属
> **历史记录类工件**，MUST NOT 回改（规格书 §16.2；AGENTS.md）。
> **001 的 `contracts/{architecture-guard,injection-seams}.md`** 本特性**不改**：不引入新工程、不引入新
> 接缝接口，G-01~G-08 的口径与扫描范围均不变。

## Project Structure

### Documentation (this feature)

```text
specs/006-bribery-punishment/
├── plan.md                        # 本文件
├── spec.md                        # 规格（Q1~Q3 已裁决并回写规格书 §7；含 3 条计划阶段勘误）
├── research.md                    # 阶段 0：17 项抉择（R-01~R-17，全部有结论，未决事项 = 无）
├── data-model.md                  # 阶段 1：层分布 / 字段与签名 / 状态转移 / 不变量 / 键表与版本 / 需求映射
├── quickstart.md                  # 阶段 1：8 个可运行的人工验收场景
├── contracts/
│   ├── bribery.md                 # 契约十五：窗口、金额、概率、乘数 kⁿ、封顶、当月事实缓冲、派生查询面
│   ├── punishment.md              # 契约十六：结算第③步、12 格矩阵、罚金、服刑与禁考禁升、连坐与死刑、快照
│   └── config-registry.md         # 契约十七：贿赂与惩罚数值登记、禁止项、RegisteredValues 对照、回写关系
└── checklists/requirements.md     # 规格校验（$speckit-specify 阶段完成；勘误 #1~#3 见本计划）
```

> 001 的 `contracts/{architecture-guard,injection-seams}.md`、002 的
> `contracts/{monthly-settlement,ledger,config-registry}.md`、003 的
> `contracts/{new-game-setup,official-career,config-registry}.md`、004 的
> `contracts/{save-service,save-format,save-cadence}.md`、005 的
> `contracts/{exam-calendar,exam-session,config-registry}.md` 是**跨阶段契约**，本特性**继承**并按
> 「回写义务清单」做**十一处最小修订**（#2 步骤表与 MUST NOT 清单、#3 spec、#4 data-model、#5 数值归属、
> #6 001 字段与不变量、#7/#8 003 的 `PromotionBanned` 归属、#9/#10 004 键表与版本、#11/#12 005 的占位
> 作废）。**契约四（账本）的判定句不动**，只在其类别表内新增两行。

### Source Code (repository root)

```text
KejuFuShengLu.slnx                       # 不动（仍六工程）
global.json / Directory.Build.props      # 不动
*.csproj                                 # 不动（零新增依赖）

src/
├── KFL.Core/                            # net10.0
│   ├── Entities/
│   │   ├── Person.cs                    # 【改】+3 只读计数、+1 写入通道族、+1 internal 复原
│   │   ├── Family.cs                    # 【改】+PunishmentJournal（只读引用 + internal 复原）
│   │   └── PunishmentJournal.cs         # 【新】当月事实缓冲：追加 / 读取 / 清空 / internal 复原
│   ├── ValueObjects/
│   │   └── PunishmentFacts.cs           # 【新】BribeFact / SentenceFact / FineFact / RelativeBanFact / CollectivePunishmentFact / ReleaseFact
│   └── Enums/
│       ├── LedgerCategory.cs            # 【改】追加第 21/22 个取值 BribeExpense、Fine；类文档 20 → 22
│       └── LedgerCategoryMetadata.cs    # 【改】两个 switch 各 +2 分支；两处异常文案 20 → 22
├── KFL.Infrastructure/                  # net10.0（零新增文件）
│   └── Save/
│       ├── SaveFileFormat.cs            # 【改】Version 2 → 3（:30）
│       └── Serialization/
│           ├── SaveStateDto.cs          # 【改】PersonDto +3 键（25..27）、FamilyDto +1 键（7）
│           ├── SaveStateValueDtos.cs    # 【改】+PunishmentJournalDto 与事实行 DTO
│           ├── MessagePackSaveFormat.cs # 【改】PersonMemberCount 25→28（:50、:254 Skip 3→6）、FamilyMemberCount 7→8（:47、:226 Skip 1→2）
│           ├── SaveStateMapper.cs       # 【改】双向映射与复原次序（日志复原独立一步）
│           └── SaveStateValidator.cs    # 【改】+2 条（计数非负、日志引用完整性）→ 校验十二条
├── KFL.Rules/                           # net10.0
│   ├── Bribery/                         # 【新目录】
│   │   ├── BribeRules.cs                # 【新】窗口 / 应付金额 / 被抓概率 / 乘数 kⁿ / 触顶判定（全纯函数）
│   │   ├── BribeEntry.cs                # 【新】行贿行动（扣款 + 掷骰 + 计数 + 记日志 + 转惩罚入口）、调试重置
│   │   └── BribeOutcome.cs              # 【新】行动返回值 + 可行性查询记录
│   ├── Punishment/                      # 【新目录】
│   │   ├── PunishmentImposer.cs         # 【新】矩阵施加：服刑 / 罚金 / 亲属禁考禁升 / 死刑 / 连坐 / 全族 48 月
│   │   ├── PunishmentAdvance.cs         # 【新】结算第③步：读日志成快照 → 清空 → 三计时递减 → 释放与降级
│   │   ├── PunishmentSnapshot.cs        # 【新】快照主体 + 事实行 + PrisonerCount
│   │   ├── PunishmentQueries.cs         # 【新】FR-029 ①②③④ 纯查询 + FR-030 派生事实
│   │   └── PunishmentCommands.cs        # 【新】FR-031 的解除禁考禁升（自身与全族）/ 释放囚犯 / 赦免死囚
│   ├── Config/
│   │   ├── BribePolicy.cs               # 【新】§7.1/§7.2/§11 的行贿数值单点
│   │   ├── PunishmentPolicy.cs          # 【新】§7.3/§7.4/§7.5 的 12 格矩阵与罚金/服刑数值单点
│   │   └── GameConfig.cs                # 【改】+Bribe / +Punishment 转发视图、+Assets.TotalAssets
│   ├── Economy/
│   │   └── TotalAssetsCalculator.cs     # 【新】总资产口径的单一访问器（FR-011）
│   ├── Exam/ExamAdmission.cs            # 【改】仅两处 <param> 文档（公式与 clamp 一字不动）
│   └── Settlement/
│       ├── MonthlySettlementEngine.cs   # 【改】八步 → 九步（惩罚推进插在 ② 之后、③ 之前）
│       └── SettlementResult.cs          # 【改】+1 个 required init 子段 Punishment
├── KFL.Presentation/                    # 不动（界面轨 U2/U3）
└── KFL.App/                             # 不动（界面轨 U1）

tests/
└── KFL.Tests/                           # net10.0
    ├── Architecture/ConfigLiteralTests.cs        # 【改】RegisteredValues 46 → 49
    ├── Core/{PersonTests,StatusTests,LedgerInvariantTests}.cs  # 【改】只读全集、状态位一致性、类别计数与守卫
    ├── Infrastructure/                 # 【改】SaveFixtures / SaveStateComparer / SaveFormatContractTests / SaveStateValidatorTests / SaveFailureTests
    └── Rules/
        ├── BribeEntryTests.cs          # 【新】FR-003~FR-010、FR-031（重置面）；SC-002/003
        ├── PunishmentMatrixTests.cs    # 【新】FR-011~FR-016、FR-027；SC-004/005（12 格逐格）
        ├── PunishmentAdvanceTests.cs   # 【新】FR-017~FR-021；SC-006（递减、暂停、释放、降级）
        ├── CollectivePunishmentTests.cs # 【新】FR-022~FR-026；SC-007（三分支、免死、外嫁追及、范围封印）
        ├── BribeQueryTests.cs          # 【新】FR-029、FR-030；SC-008
        ├── SpecAnchorTests.cs          # 【改】新增贿赂与惩罚数值锚点节
        └── DeterminismTests.cs         # 【改】新增完整链的两次独立运行差异项 = 0
```

**Structure Decision**: 工程划分与依赖方向**不动**（规格书 §2 六个工程）。新增代码按**关注点**落在既有
目录语义内：`Core/Entities` + `Core/ValueObjects`（三个计数、当月事实缓冲与其行记录）、`Core/Enums`
（两个账本类别）、`Rules/Bribery`（结算之外的玩家行动）、`Rules/Punishment`（结算步 + 矩阵 + 查询面）、
`Rules/Config`（**全部**规则数值）、`Rules/Economy`（总资产单一访问器）、
`Infrastructure/Save/Serialization`（**只**随字段同步键表与映射，不改分层）。

`Rules/Bribery/` 与 `Rules/Punishment/` 是本特性仅有的两个**新目录**：前者是**结算之外**的行为
（与 005 的 `ExamCalendar` / `ExamRegistration` 同类），后者是**结算之内**的整步与矩阵（与
`ExamSeasonRunner` 同类）。把两者分开，使「行贿消耗 1 次随机、惩罚推进消耗 0 次随机」这条**独立性**
在代码结构上可直接指认（契约十六 §1.3）。理由与替代方案见 Complexity Tracking。

## Complexity Tracking

> 本特性无章程违规项。下面登记**十项新增抽象 / 新增面**的论证（章程「复杂度 MUST 被论证」）。

| 新增抽象 | 为什么现在需要 | 更简单的替代为何被否 |
| --- | --- | --- |
| `Person` 三个新计数 + 具名写入通道 + 一条 `internal` 复原通道 | FR-004 明令「同一考生 × 同一考试级别、跨科年累计」且 MUST 跨存档保留；001 不变量 2 禁止无约束公开 setter | ① 三个公开 `set;`——违反 001 不变量 2；② 只给 `internal`——规则层引不到 `KFL.Core` 的 internal 面；③ 放进旁路字典——成为第二真源，且「换考生重置」要靠手工维护 |
| `Family.PunishmentJournal`（新增持久子聚合） | FR-028 明令**行贿与惩罚的当月事实**（行贿/被抓、服刑建立、罚金、亲属禁考禁升、连坐结果）MUST 经结算快照可见，而这些事实按 Q2 的裁决**建立**在结算之外、**递减/释放**在结算之内；没有缓冲无法在同一份快照里表达两者，而缓冲若不落盘，「行贿 → 退出保存 → 重载 → 结算」会**静默丢失**整格惩罚 | ① 把「施加惩罚」挪进结算第③步——被否：Q2 的裁决文本明写「行贿与惩罚的**建立**发生在**结算之外**的月份内，该月结算即递减 1 次」，且 US2 的独立测试要求「直接把一次被抓喂给惩罚入口」；② 纯内存缓冲——被否：004 的存档节奏允许决策事件后退出保存，事实会丢；③ 挂在 `GameState` 上——被否：001 明令 `GameState` 不承载集合，且 `GameStateTests.cs:110` 断言其公开属性恰为七个 |
| `LedgerCategory.BribeExpense` + `Fine`（20 → 22） | FR-027 明令「新增**资金类**账本类别以承载行贿支出与罚金」；契约四把「类别全集」的唯一真源钉在 `LedgerCategoryMetadata` | ① 两类共用一个类别——被否：§12.3 的支出明细与 U3 需要区分「主动买路钱」与「被判罚」；② 用事件类——被否：资金池真的动了，E-08 的二分类会破；③ 第二套账——FR-027 明令禁止 |
| `PunishmentJournal` 的六种事实行记录（`PunishmentFacts.cs`） | 快照的 Key Entities 逐项要求「行贿/被抓、服刑建立、罚金、亲属禁考禁升、连坐结果、释放」六类增量事实，且字段各不相同（月数 / 金额 / 双方 `PersonId` / 功名前后值） | ① 一个带 kind 判别式与可空字段的万能记录——被否：可表达非法态（既有 `DegreeRecord.EnsureCompatible` 的先例反对这种做法）；② 只靠账本条目——被否：行贿的「第几次」、服刑月数、禁考禁升月数都不产生账本条目 |
| `Rules/Bribery/` 新目录（3 文件） | 行贿是**结算之外**的玩家动作（FR-003、FR-007），需要与结算步完全分离的可断言面；U2/U4 只需其中的纯查询 | 全塞进 `MonthlySettlementEngine`——被否：与 FR-007 的「结算之外」直接冲突，且窗口/金额/概率无法脱离结算被单独断言 |
| `Rules/Punishment/` 新目录（5 文件） | 本特性跨越「矩阵施加 / 结算整步 / 快照 / 查询 / 调试入口」五个正交关注点；U2/U4/⑧ 只消费其中的纯查询与快照 | 合进 `Bribery/` 或 `Settlement/`——被否：失败定位退化，且「惩罚推进不消费随机」这条判据会与行贿的掷骰混在一个类里 |
| `Config/BribePolicy.cs` + `Config/PunishmentPolicy.cs` | FR-001 明令 §7.1/§7.2/§7.3/§7.5/§11 的**全部数值**单点声明并经 `GameConfig` 可读；两块数值的**读取时机不同**（每次行贿 vs 每次被抓），且 12 格矩阵是**表** | ① 挤进 `ExamPolicy`——被否：005 的契约十四 §2 明令 `ExamPolicy` MUST NOT 引入贿赂数值，且会平移既有成员；② 合并为一个类——被否：矩阵（12 格 × 多字段）需要独立类型，混在一起会掩盖「矩阵是表」这一事实 |
| `Economy/TotalAssetsCalculator.cs`（总资产单一访问器） | FR-011 明令「总资产口径 MUST 有**单一访问器**」，并让既有「12 月工出身 bonus 的总资产为正」判定复用同一位（消除 `MonthlySettlementEngine.cs:138-140` 的就地计算第二真源） | ① 把表达式复制到惩罚路径——被否：正是 FR-011 要消除的第二真源；② 挂进 `AssetPriceTable`——被否：那是配置表（只有单位价），总资产还要读 `Treasury` 与 `Loan` |
| `SettlementResult.Punishment`（跨特性形状变更） | FR-028 明令「作为 `SettlementResult` 的新增必填子段」，与 003 的 `Career`、005 的 `ExamSeason` 同款；且快照必须表达**不产生账本条目**的事实（服刑建立、禁考禁升、释放、降级） | 只靠账本条目与状态读取——「本月发生了什么」无法增量表达，且 U2 的徽章与 ⑧ 会退化成 diff 与副作用（章程原则 VI） |
| `SaveFileFormat.Version` 2 → 3（唯一线上形状破坏） | FR-032 明令升版；004 的线上形状是**位置式数组**，`PersonDto` 由 25 变 28、`FamilyDto` 由 7 变 8 后旧档必然错位读 | ① 提供旧→新迁移——Out of Scope 且会被 §14 的「拒绝优于误读」否掉；② 把新字段塞进扩展袋——仍是第二真源且迁移问题原样存在 |

## 阶段 0 / 阶段 1 小结

`research.md` 的 **17 项抉择全部有结论，未决事项 = 无**，`data-model.md` 给出不变量清单，三份契约
（十五/十六/十七）把形状、次序与数值归属分别钉死。其中**五项是真问题**，共同特征是「不做就静默失真」
或「做了就静默破形状」：

1. **当月事实必须有落盘的缓冲**（R-06）：Q2 把「建立」钉在结算之外、「递减」钉在结算之内，而 FR-028
   要求两者出现在**同一份** `SettlementResult` 里；缓冲区若不落盘，「行贿 → 退出保存 → 重载 → 结算」
   会丢掉整格惩罚。它不是第二真源——状态的真源仍是 `Person` 的计时/状态位与账本，缓冲只承载
   「已发生但尚未被某次结算报告」的事实，第③步读后即清空。
2. **惩罚推进步不能消费随机**（R-09）：契约三 §4 的整月随机次序被 `DeterminismTests` 的
   `NextDouble` 计数逐月钉死（1 次，1 月 2 次，计息节点再 +1）；新步若消费随机，该测试立刻红。
   本特性因此把「行贿掷骰」留在结算之外，第③步**恰好 0 次**。
3. **`PaymentPrimitive.Pay` 需要一个可选 `PersonId?`**（R-08）：行贿与罚金都是**个人**行为，而现有
   形参把条目落成家族级；契约四 §5 的角色级统计（U3）会因此把「谁把家败了」抹平。加一个**默认值**
   形参是源码兼容的，且不触碰既有的「不做部分支付」「不产生负余额」承诺。
4. **`NeutralBribeMultiplier` 占位必须删除而不是保留**（R-05）：FR-008 写「占位随之作废」，SC-003 写
   「占位值在任何允许多次尝试的路径上都不可达」。两个 005 调用点改传 `kⁿ` 后，`k⁰ = 1.0` 由乘数入口的
   **恒等元**自然给出（循环初值），因此保留一个值为 1.0 的常量只会造出「k⁰ 的第二真源」。
5. **封顶必须判在「最终概率」而不是「乘数」上**（R-04）：规格书把封顶写在最终录取率/进甲率上，而乘数
   本身无上限；判据取 `ExamAdmission` 同一入口的输出，因此**不复制录取率公式**（FR-008）、也不引入
   任何「乘数上限」这一规格书里不存在的概念。

## 下一步

`$speckit-tasks` 依据本计划与 [data-model.md](./data-model.md)、[contracts/](./contracts/)、
[quickstart.md](./quickstart.md) 生成依赖有序的 `tasks.md`；其中 MUST 登记「回写义务清单」的
**17** 项（同步了哪些文件、同步到哪里），供后续审计核对（AGENTS.md「铁律」）。实现阶段 MUST 一并完成：
`BribePolicy` / `PunishmentPolicy` 的 **3** 项新增小数登记（`1.6m` / `0.15m` / `0.35m`）、
`SaveFileFormat.Version` 2 → 3 与两张键表、`MonthlySettlementEngine` 类文档的八步 → 九步、
`NeutralBribeMultiplier` 的删除与两处调用点改写，以及「勘误登记」#1~#3 在 `tasks.md` 中的登记与在
测试锚点中的落实。
