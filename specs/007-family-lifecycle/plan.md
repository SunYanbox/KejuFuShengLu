# Implementation Plan: 家族生命周期（婚配 · 生育 · 疾病 · 寿命 · 家主继任 · 收养）

**Branch**: `007-family-lifecycle` | **Date**: 2026-10-17 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/007-family-lifecycle/spec.md`

**Note**: 本文件由 `/speckit-plan` 生成。数值真源是《科举浮生录规格书》§4.1~§4.4、§5.4、§9.1~§9.5、§12.1、§12.3
与 §17 的六项裁决（2026-10-17，Q1~Q6）；本计划只负责「怎么落地」，MUST NOT 新创数值（FR-004）。

## Summary

交付规格书 **逻辑轨 ⑦**：把「家族」从一张静态名册变成有生老病死的活体。月度结算由**九步扩为十步**，
在 ⑦ 贷款与 ⑨ 科举季之间插入 **⑧ 人口与健康推进**（八个子步 ⑧-a~⑧-h），使 ⑦ 的所有判定都发生在
科举季**之前**（`specs/005-exam-cycle/contracts/exam-session.md:21` 的硬约束）。

- **US1 婚配**：托媒（5 贯 × 支出系数、50%）、随机提亲（每月至多 1 次、base 5% × 负面事件系数）、
  榜下捉婿（新科一甲次月、`30% ÷ (1 + P)`、对方支付 `Next(500,1001) × (1+P)` 贯）、
  聘礼/嫁妆双向流动（三阶层 × 支出系数）、嫁出外嫁归档、买功名婚姻（年俸 × 150%）。
- **US2 生育**：申请 10 贯（女方 16~45、每对夫妇每年一次）、次月 ⑧-b 分娩、分娩死亡 5%、
  遗传（`σ = |a−b|/2 + 5`）、新生儿三档夭折判定（累计 ≈ 27.1%）。
- **US3 成长·疾病·寿命**：学业 ±（读书 / 延名师 / 遗忘）、体质四向变化（+1 / 患病 / 饥馑 / 救济期）、
  患病概率按体质线性插值 × 主导天赋、`s` 决定扣减与药费、宽限月、疗程与 `min(1,…)` 短路、
  天命寿数到龄当月死亡。
- **US4 家主继任**：⑧-h 四档直系优先，无在册男性 ⇒ `HeadId = null`（**不构成绝嗣**）。
- **US5 收养**：本年第 n 名 `10n` 贯、1 月重置、年龄 8~16、性别 50/50、辈分 = 家主辈分 + 1。

技术路线（沿用 001~006 的分层口径）：状态与不变量归 `KFL.Core`，规则判定与编排归 `KFL.Rules`，
落盘形状归 `KFL.Infrastructure`（随字段同步，不新增依赖）。

- `KFL.Core`：`StatusTimers` 尾部 +2 项（疾病程度、疗程剩余月数）、`Person.IsTutoredByMaster`、
  `Family.Lifecycle`（新聚合）、`DeathCause` / `LedgerCategory` 16 个新取值、`MarriageProposal` 等值对象。
- `KFL.Rules`：十步引擎插 ⑧、`PopulationAdvance` + 八个子步执行器、`LifecycleEntry`（玩家动作）、
  `LifecycleQueries`（纯查询面）、五个新配置类 + `GameConfig` 六个转发视图（配置类计数见勘误 E-05）。
- `KFL.Infrastructure`：`SaveFileFormat.Version` 3 → 4、`PersonDto` 28 → 29、`FamilyDto` 8 → 9、
  `StatusTimersDto` 4 → 6、新增 `FamilyLifecycleDto`/`PendingBirthDto`/`CoupleKeyDto`/`MarriageProposalDto`（四个）、
  `MessagePackSaveFormat` 的成员数常量与 `Skip` 序列、`SaveStateValidator` 增两条结构校验。

本特性**不做**：界面轨 U1~U5、成就与绝嗣判定（⑧）、难度启动装配（⑨）、族谱排序绘制（U2）、
存档格式迁移、随机事件总表（只实现 §9.1 提亲与 §9.2 榜下捉婿）、家族声望属性。

## Technical Context

**Language/Version**：C# 14 / .NET 10（非 UI 工程 `net10.0`，UI 工程 `net10.0-windows`；章程 I）。
**Primary Dependencies**：无新增外部依赖。`KFL.Core` 零依赖；`KFL.Rules` 只依赖 `KFL.Core`；
`KFL.Infrastructure` 用既有的 MessagePack / System.Text.Json / Bogus / Serilog。
**Storage**：现有存档通道（`SaveFileFormat` + `MessagePackSaveFormat` + `JsonSaveFormat`），
文件格式版本 **3 → 4**，旧版本一律 `UnsupportedVersion` 拒绝（复用 004 既定策略，无迁移路径）。
**Testing**：xUnit（`tests/KFL.Tests/`），架构守卫 G-01~G-08 在构建期强制；数值守卫
`ConfigLiteralRules`/`ConfigLiteralTests`（新增入单小数见 `contracts/config-registry.md` §4.1，
清单长度以守卫去重后的实际条数为准）。
**Target Platform**：Windows 桌面（WPF），但本特性全部落点在三层非 UI 工程内，可无头运行。
**Project Type**：桌面应用 + 分层类库（单一解决方案 `KejuFuShengLu.slnx`，恰好六个工程）。
**Performance Goals**：单月 `Settle` 在 200 人规模家族上 < 5 ms；`LifecycleQueries` 全部纯函数、
无分配放大（不建视图、不缓存派生量）；连续 120 月结算（SC-002）< 1 s。
**Constraints**：
- 规则层 MUST NOT 触 `DateTime.Now` / `Random` / 文件系统（G-07 的十四条禁用 token）。
- 一切随机 MUST 经 `IRandomService`；正态 MUST 复用 `AttributePolicy.NextNormal`（恰 2 次 `NextDouble`）。
- 数值 MUST NOT 出现在 `src/KFL.Rules/Config/` 与测试之外（FR-002）。
- 新步 MUST NOT 改变 ②~⑦ 与 ⑨ 的既有随机次数与次序（FR-010）。
- `Person.Study`/`Person.Health` 的 setter 是**抛异常守卫**而非静默 clamp ⇒ 调用方 MUST 自己先 clamp。
- `KFL.Core/AssemblyInfo.cs:13` 只放行 `KFL.Infrastructure` ⇒ 新增持久字段的复原通道只能用 internal + Infrastructure 落盘。
**Scale/Scope**：单家族；成员数典型 3~200；新增 ~14 个产品文件、~8 个测试文件、16 个 `LedgerCategory` 取值、
5 个配置类、4 个新存档 DTO。

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 章程条款 | 本计划如何满足 | 结论 |
| --- | --- | --- |
| I .NET 10 单一基线 | 只改三类非 UI 工程，TFM 全保持；不新增工程、不新增 `*.sln` | ✅ |
| II 分层解耦与单向依赖 | 新增文件分别落 `KFL.Core`（状态/值对象）、`KFL.Rules/Family`（判定）、`KFL.Infrastructure/Save`（形状）；规则层零环境依赖 | ✅ |
| II 规则层禁 `DateTime.Now`/`Random`/文件系统 | 全部时间取 `IGameClock`/`GameDate`，全部随机取 `IRandomService`；G-07 扫描覆盖新增目录 | ✅ |
| III WPF + MVVM、表现层不复制规则 | 本特性不触表现层；查询面 `LifecycleQueries` 供 U1 直接调用，U1 无需自行算概率 | ✅ |
| IV 单测优先 + 可复现种子随机 | FR-049 的 30+ 条清单在**同一提交**内落地，含 SC-015 存活阈值金色表；不留后补 | ✅ |
| V 约定式提交（中文） | 提交信息用中文 `feat(rules): 家族生命周期…` | ✅ |
| VI 调试通道不复制规则 | 若要「立即生子/立即病死」调试入口，MUST 转调 `LifecycleEntry`，不另写判定 | ✅ |
| 技术栈与工程约束 | 无新依赖；六工程不变；金额内部以「文」为 `decimal`（`Money.FromGuan`） | ✅ |
| 开发工作流与质量门禁 | 门禁四条（build 0 警告 / test 全绿 / `dotnet sln list` 六工程 / 无 `*.sln`）+ G-01~G-08 | ✅ |
| 需求真源 | 数值全部来自规格书 §4/§5/§9/§17，FR-050 的十项回写在**同一提交**内完成 | ✅ |

**设计后复核（Phase 1 之后）**：✅ 无新增违规。`Complexity Tracking` 里四项新增抽象均已论证，
其中「新聚合 `FamilyLifecycleState`」是本特性唯一可以争议的复杂度，理由见该节第 1 行。

### 继承的用户裁决（2026-10-17，规格书 §17）

| 编号 | 议题 | 裁决 | 落点 |
| --- | --- | --- | --- |
| Q1 | 随机提亲频率与阶层加权 | 每月至多 1 次；base `5% × 负面事件系数`；命中后候选集合均匀抽 1；阶层加权只按总资产三档 | `MarriagePolicy`、⑧-a、`contracts/marriage.md` §1 |
| Q1 | 榜下捉婿 | 新科一甲及第**次月**；对方支付 `500~1000 贯`；base `30% × 系数`；MUST NOT 叠加 §9.2 基础金额 | `MarriagePolicy.ImperialProposalChance`、⑧-a |
| Q2 | 疾病程度与代价 | `s∈[0,1]` 均匀；扣减 `2+8s`；药费 `(0.05+0.45s) 贯 × 支出系数`；疗程 3 月；未治疗不过期 | `IllnessPolicy`、⑧-c |
| Q3 | 夭折粒度 | 出生当月 + 满 1 岁 + 满 2 岁共 3 次、每次 10%（简单 5 / 地狱 12） | `ChildbirthPolicy`、⑧-f |
| Q4 | 患病概率函数 | `p = [0.25 + 39.75 × (100 − c)/99] % × m_p`，`c` 取当月初值 clamp `[1,100]`；年龄档/封顶全删 | `IllnessPolicy.InfectionChance` |
| Q5 | 免病年龄与年龄增益 | `<3` 免病且**零随机消费**；`3~12` +0.60、`12~24` +0.30、`≥24` +0 | `IllnessPolicy.ClearBonus` |
| Q5 | 官员压力 P 与捉婿 | `P = Σ w(L)`，`w(L)=0.02+1.48×(18−L)/17`；金额 `×(1+P)`；概率 `÷(1+P)`；功名分档 `P<1/1≤P<3/P≥3` | `OfficialPressure`、`MarriagePolicy` |
| Q6 | 痊愈系数与宽限月 | 基础 `体质 × 1.00/100`；新确诊当月 0 扣减 0 药费 0 清除；疗程式 `min(1, (体质×1.10/100+0.20)×m_q+b)` | `IllnessPolicy`、⑧-c |
| Q6 | 患病概率不乘难度系数 | `p` 只乘天赋 `m_p`；四难度同种子同结果 | `IllnessPolicy`（不引用 `DifficultyRates.NegativeEventFactor`） |

## 勘误登记（计划阶段）

只登记**工件内部矛盾**，不新创数值。

| # | 位置 | 原文 | 计划口径 | 依据 |
| --- | --- | --- | --- | --- |
| E-01 | `specs/002-monthly-settlement-economy/contracts/monthly-settlement.md:51-52` | 把「随机事件」「属性成长/衰老/疾病/死亡判定」列在 **MUST NOT 存在** 的步骤里 | 二者由本特性**正式接管**为 ⑧ 的子步；该条 MUST 改写为「已由 ⑦ 交付」 | `spec.md` FR-006/FR-007；`specs/005-exam-cycle/contracts/exam-session.md:21` |
| E-02 | `src/KFL.Rules/Exam/ExamSeasonRunner.cs:17` | 自述「本步是第 ⑦ 步」 | 引擎与契约口径均为第 ⑨ 步（⑦ 贷款、⑧ 人口健康）；实现不读该编号，只在注释层订正 | `MonthlySettlementEngine.cs:22-32` |
| E-03 | `src/KFL.Rules/Config/GameConfig.cs:24-27` | 「明确不含：收养递增计价、成长衰老疾病死亡的年龄线与概率」 | 本特性交付后该注释**失真**，MUST 改为「已由 ⑦ 交付，见 `Growth`/`Illness`/`Marriage`/`Childbirth`/`Purchase`」 | FR-001 |
| E-04 | `src/KFL.Rules/Settlement/SettlementResult.cs:15-19` | `LivingCostLine`/`IncomeLine` 用位置参数式 `record`（无 `required`），与其余子段风格不一 | 本特性新增子段一律 `public required … { get; init; }`；**不回改**既有两行（历史形状，改则扰动 002 契约） | 006 风格；`SettlementResult.cs:48` |
| E-05 | 本文件自身 | Complexity Tracking 写「六配置类 + 六 `GameConfig` 转发视图」、`:62` 写「6 个配置类、3 个新存档 DTO」、`:142` 写 quickstart「8 个场景」 | 实为 **5 个配置类 + 6 个转发视图**（`data-model.md` §3.7）、**4 个新存档 DTO**（含 `CoupleKeyDto`）、**9 个场景**；`GameConfig.Lifecycle` 若确无成员可转发，MUST 在 `tasks.md` 的回写范围登记中记录 | 本文件 Project Structure、`data-model.md` §3.7、`quickstart.md` |
| E-06 | `src/KFL.Rules/Config/GameConfig.Price.cs:9-25` | 002 已为饥馑**时间线**建了门面 `GameConfig.Famine`（`MonthsUntilRelief` / `ReliefMonthsUntilSevere` / `ReliefExpenseDiscount` / `LimitMonths` / `RemainingMonths`） | ⑦ 的饥馑**健康影响**数值（`FamineHealthLossPerMonth = 10` / `FamineDeathChance = 0.05m`）**留在 `GrowthPolicy` 并经 `GameConfig.Growth` 暴露**——`FamineHealth.Run` 属 ⑧-e 健康子步，与成长/恢复同域；两个门面职责不同（时间线 vs 健康影响），MUST NOT 视为同一领域的重复门面 | FR-031；`contracts/config-registry.md` §1 |

## 回写义务清单（AGENTS.md「铁律」+ FR-050 的落地）

先说结论：本特性在公开面上确实做了跨特性形状变更——① 月度结算 **九步 → 十步**；② `SettlementResult`
**+1 必填子段**；③ `StatusTimers` **+2 项**（4 → 6）；④ `Person` **+1 可写属性**；⑤ `Family` **+1 聚合属性**；
⑥ `LedgerCategory` **+16 取值**（22 → 38）；⑦ 存档 `Version 3 → 4`（三个 DTO 扩键 + 四个新 DTO）。
下表的**行号为回写前的原位**。

| # | 目标工件（活工件） | 回写内容（锚点） | 依据 |
| --- | --- | --- | --- |
| 1 | `科举浮生录规格书.md` | §4.3 的「病程 2~4 月」「固定 −10 体质/月」整体替换为 Q2；「患病概率 16 岁以上 2.5%/月…封顶 20%」替换为 Q4 + Q5 + Q6；§9.1/§9.2 补提亲 5%、捉婿 `30%÷(1+P)`、金额 `×(1+P)`、功名分档阈值 1/3、可拒绝、阶层加权三档；§9.4 补夭折粒度；§17 块标注裁决日期 2026-10-17 | FR-050 ① |
| 2 | `specs/001-core-skeleton/data-model.md` | `:188-225` 不变量 4 追加 ⑦ 引用注（`Ill` 位 ⇔ `IllnessSeverityBasisPoints != null`）；**「无写入通道成员全集 19 项」保持不变**，改的是 `:195-196` 的「`Person` 自持可写属性十个」→ **十一个**（追加 `IsTutoredByMaster`；`StatusTimers` 的两项在 `Timers` 内，不构成 `Person` 成员）；`:244-248` 删除「成长衰老疾病死亡的年龄线与概率不含」的口径；`:293-303` 不变量 6 补「由 ⑧-h 月度驱动修正」；`:308-310` 嫁入配偶的仕身份差异补 ⑦ 落实注；`:318-322` 继任判定改为「已由 ⑦ 交付」；`:402-406` 状态转移表补患病/归档/外嫁/继任四行；`:227` 补「死因只落当月事实段」 | FR-050 ② |
| 3 | `specs/001-core-skeleton/contracts/injection-seams.md` | `:91-99` 维持「`IEventBus`/`IAchievementStore` 至今未定义」——本特性**不新增接缝**（快进暂停信号走 `SettlementResult.Lifecycle` + `LifecycleQueries`），只补一段「⑦ 未新增接缝」的确认 | FR-045、FR-050 ③ |
| 4 | `specs/002-monthly-settlement-economy/contracts/monthly-settlement.md` | `:13-23` 步骤表九行 → 十行（插 ⑧，原 ⑧/⑨ → ⑨/⑩）；`:41-42` 随机次序式补 ⑧-a~⑧-f；`:51-52` 两条从「MUST NOT 存在」改为「已由 ⑦ 交付」；`:85`/`:88-96` 次序式与映射表补本特性条目；`:222` 饥馑 −10 与 5% 判定的归属改为「⑦ 已交付」；`:229-239` 明确不包含节删去已交付项 | FR-050 ④ |
| 5 | `specs/003-opening-assets-officialdom/data-model.md` + `contracts/new-game-setup.md` | `data-model.md:163-164` 与 `new-game-setup.md:106-110` 补「无父母参照者分布（`N(60,20)`/`N(30,15)`/`N(85,10)`）被 ⑦ 复用于收养与婚配对象」 | FR-050 ⑤ |
| 6 | `specs/004-save-system/contracts/save-format.md` | `:77` FamilyDto 8 → 9 键；`:78` PersonDto 28 → 29 键；`:80` StatusTimersDto 4 → 6 键；`:73-89` 表补 `FamilyLifecycleDto`/`PendingBirthDto`/`MarriageProposalDto`；`:128`/`:137`/`:150` 的 `== 3` 谓词与「MUST NOT 静默误读」段改 `== 4`；`:151-153` 键数常量同步。**顺带**修正 `specs/004-save-system/data-model.md:52/:71/:298/:349` 的存量旧值 `FormatVersion = 2`（006 遗留未回写项） | FR-047、FR-050 ⑥ |
| 7 | `specs/005-exam-cycle/contracts/exam-session.md` | `:18` 的八步旧序（006 未回写的存量缺口）改为十步；`:21` 的「⑦ 在科举季之前」补一句「⑦ 已交付为 ⑧」 | FR-050 ⑦ |
| 8 | `specs/006-bribery-punishment/` | `contracts/config-registry.md:95-97`、`contracts/punishment.md:279-280`、`spec.md:461-466` 的「属逻辑轨 ⑦」改述为「⑦ 已交付」 | FR-050 ⑧ |
| 9 | `tests/KFL.Tests/Rules/SettlementEngineTests.cs` | `:305-307` 注释改十步口径；`:313` 仍 `["Settle"]`；`:315-337` `expectedProperties` 20 → 21（末尾加 `Lifecycle`） | FR-010、FR-050 ⑨ |
| 10 | `specs/007-family-lifecycle/tasks.md` | 登记上述九项的回写范围（同步了哪些文件、同步到哪里），并登记「本特性接管 001 `tasks.md:88`、002 `tasks.md:83/:279` 的『阶段⑧』残句」 | FR-050 ⑩ |

> **不改的**：`tasks.md` 既有条目、`implementation-notes.md`、既有 `checklists/` 属**历史记录类工件**，
> MUST NOT 回改。001 `tasks.md:88`、002 `tasks.md:83/:279` 的「阶段⑧」残句保持原样，只在 007 的
> `tasks.md` 里登记「已接管」。

## Project Structure

### Documentation (this feature)

```text
specs/007-family-lifecycle/
├── plan.md              # 本文件
├── research.md          # 阶段 0：R-01~R-25 技术抉择（25 项全部有结论，未决事项 = 无）
├── data-model.md        # 阶段 1：层分布 / 新增类型 / 形状变更 / 状态转移 / 不变量 / 持久化
├── quickstart.md        # 阶段 1：9 个可执行验证场景（0 前置 + 场景 1~9）
├── contracts/
│   ├── lifecycle.md     # 契约十八：十步结算与 ⑧ 的八个子步、随机次序、事实段形状
│   ├── marriage-birth.md# 契约十九：婚配、生育、收养的资格/金额/随机次序
│   ├── illness-lifespan.md # 契约二十：患病/痊愈/疗程/饥馑/寿数/夭折的公式与短路
│   └── config-registry.md  # 契约二十一：数值登记表（五配置类 + 门面转发 + 字面量守卫）
├── spec.md              # 已存在（1018 行，sole source of intent）
└── checklists/
    └── requirements.md  # 已存在（16/16，历史记录类）
```

> `tasks.md` 由 `$speckit-tasks` 生成，不是本命令的交付物。

### Source Code (repository root)

```text
src/KFL.Core/
├── Enums/
│   ├── LedgerCategory.cs            # 【改】+16 取值（22 → 38）
│   ├── LedgerCategoryMetadata.cs    # 【改】KindOf/CreditTargetOf 登记 +16 行
│   ├── DeathCause.cs                # 【新】OldAge / Illness / Famine / Infancy / Childbirth
│   ├── MarriageClass.cs             # 【新】Commoner / Scholar / Official（三阶层）
│   └── MarriageProposalKind.cs      # 【新】Matchmaker / ImperialProposal
├── ValueObjects/
│   ├── StatusTimers.cs              # 【改】尾部 +2 项：IllnessSeverityBasisPoints / TreatmentRemainingMonths
│   ├── MarriageProposal.cs          # 【新】待应答提议的完整快照（含候选属性 + 金额 + 提出年月）
│   ├── PendingBirth.cs              # 【新】待分娩记录（夫妇 + 申请年月）
│   └── CoupleKey.cs                 # 【新】夫妇键（夫 PersonId + 妻 PersonId，有序）
├── Entities/
│   ├── Person.cs + Person.Officialdom.cs   # 【改】+1 可写属性 IsTutoredByMaster（+ internal 复原通道）
│   ├── Family.cs                    # 【改】+1 聚合属性 Lifecycle
│   └── FamilyLifecycleState.cs      # 【新】收养计数/待分娩/生育申请/待应答提议
└── (AssemblyInfo.cs)                # 【不动】只放行 KFL.Infrastructure

src/KFL.Rules/
├── Settlement/
│   ├── MonthlySettlementEngine.cs   # 【改】九步 → 十步；插 ⑧；ctor + INameGenerator；随机次序注释 38-41 重写
│   └── SettlementResult.cs          # 【改】+1 必填属性 Lifecycle
├── Family/                          # 【新目录】
│   ├── PopulationAdvance.cs         # ⑧ 编排（八子步，不许穿插）
│   ├── MarriageMarket.cs            # ⑧-a 捉婿 → 提亲（R-25）
│   ├── Childbirth.cs                # ⑧-b 分娩 + 新生儿初始化 + 遗传
│   ├── IllnessAdvance.cs            # ⑧-c 先病程后感染（宽限月隐式表达）
│   ├── GrowthAdvance.cs             # ⑧-d 学业/体质
│   ├── FamineHealth.cs              # ⑧-e 饥馑 −10 与 5% 死亡
│   ├── LifespanAdvance.cs           # ⑧-f 寿终 + 三档夭折（含外嫁成员）
│   ├── DeathSettlement.cs           # ⑧-g 死亡登记表统一归档
│   ├── HeadSuccession.cs            # ⑧-h 四档候选 + 决胜
│   ├── OfficialPressure.cs          # P = Σ w(L)
│   ├── Inheritance.cs               # 遗传公式（复用 AttributePolicy.NextNormal）
│   ├── LifecycleEntry.cs + LifecycleEntry.{Childbirth,Family}.cs   # 玩家动作入口（托媒/生育/收养/应答提议/发起疗程）
│   ├── LifecycleOutcome.cs          # 结果 record + LifecycleRejectReason
│   ├── LifecycleQueries.cs          # 纯查询面（FR-044）
│   └── LifecycleSnapshot.cs         # 当月事实段（含 DeathCause）
├── Config/
│   ├── GrowthPolicy.cs              # 【新】读书/名师/遗忘/体质四向
│   ├── IllnessPolicy.cs             # 【新】概率/乘数/寿命增益/疗程/宽限月/免病年龄
│   ├── MarriagePolicy.cs            # 【新】托媒/提亲/捉婿/阶层加权/婚嫁金额表/买功名
│   ├── ChildbirthPolicy.cs          # 【新】遗传 σ / 生育窗口 / 分娩死亡 / 夭折
│   ├── PopulationPurchasePolicy.cs  # 【新】10n 计价 / 年龄 8~16
│   └── GameConfig.Family.cs          # 【改】+6 个转发视图（Growth/Illness/Marriage/Childbirth/Purchase/Lifecycle）+ 修正 24-27 注释
└── Save/TurnRunner.cs               # 【改】ctor + INameGenerator（R-02）

src/KFL.Infrastructure/
└── Save/
    ├── SaveFileFormat.cs            # 【改】Version 3 → 4
    └── Serialization/
        ├── SaveStateDto.cs          # 【改】PersonDto 28 → 29、FamilyDto 8 → 9
        ├── SaveStateValueDtos.cs + SaveStateValueDtos.{Person,Family}.cs    # 【改】StatusTimersDto 4 → 6；+FamilyLifecycleDto/PendingBirthDto/MarriageProposalDto
        ├── MessagePackSaveFormat.cs # 【改】成员数常量 + Skip 序列（Family 0-2 后插、Person 20 后插、Timers 4 → 6）
        ├── SaveStateMapper.cs + SaveStateMapper.{Person,Family}.cs       # 【改】ToDto/ToDomain 两侧补生命周期 + 恢复 internal 通道
        └── SaveStateValidator.cs + SaveStateValidator.Lifecycle.cs    # 【改】+2 条：HeadId 缺失与 Lifecycle 引用完整性

src/KFL.App/                         # 【改】组合根：TurnRunner 注入 INameGenerator
src/KFL.Presentation/                # 不动（U1 才接查询面）

tests/KFL.Tests/
├── Core/
│   ├── LedgerInvariantTests.cs      # 【改】全名单 22 → 38、事件类 5 → 10、无 CreditTarget 12 → 25、恒为负 7 → 15；:108-114 负向断言替换为正向
│   ├── PersonTests.cs               # 【改】可写成员清单 10 → 11（IsTutoredByMaster）；只读清单 19 不变
│   └── StatusTests.cs               # 【改】StatusTimers 4 项 → 6 项的构造与断言
├── Rules/
│   ├── SettlementEngineTests.cs     # 【改】:305-307 改十步、:315-337 expectedProperties 20 → 21
│   ├── DeterminismTests.cs          # 【改】:198-221 绝对值断言改相对断言（R-24）
│   ├── FamilyLifecycleTests.cs      # 【新】⑧ 子步次序与隔离
│   ├── InheritanceTests.cs          # 【新】遗传公式逐点
│   ├── IllnessTests.cs              # 【新】概率/乘数/宽限月/疗程/短路
│   ├── MarriageTests.cs             # 【新】托媒/提亲/捉婿/金额表 24 格
│   ├── ChildbirthTests.cs           # 【新】生育/分娩死亡/夭折
│   ├── LifespanTests.cs             # 【新】到龄当月死亡 + 三态不豁免
│   ├── HeadSuccessionTests.cs       # 【新】四档 + 空缺
│   ├── PurchaseTests.cs             # 【新】10/20/30 与跨年重置
│   └── LifecycleQueriesTests.cs     # 【新】查询面纯度（无状态写入、无随机消费）
├── Architecture/
│   └── ConfigLiteralTests.cs        # 【改】RegisteredValues 新增入单小数（清单 49 → 去重后实际条数）
└── Infrastructure/
    └── SaveStateValidatorTests.cs   # 【改】HeadId 断言补空缺与引用完整性
```

**Structure Decision**：`KFL.Rules/Family/` 是本特性唯一的规则层新目录——把「人口与健康推进」整体收在
一个目录下，避免 ⑧ 的八个子步散落到 `Settlement/` 里与 002 的收支语义混读。`LifecycleEntry`
（玩家动作）与 `PopulationAdvance`（月度自动）**分开**：前者由 U1 直接调用、可拒绝并返回原因，
后者不可拒绝、只在 `Settle` 内被调用一次。`LifecycleQueries` 与二者都**不共享可变状态**，
只读 `GameState`，符合 FR-044「不建视图、不写状态」。

## Complexity Tracking

| 新增抽象 | 为什么现在需要 | 更简单的替代为何被否 |
| --- | --- | --- |
| `FamilyLifecycleState` 聚合（挂在 `Family.Lifecycle`） | 五组新增状态（收养计数+年份、待分娩列表、生育申请集合、待应答提议、提议所属年月）在生命周期上互相关联，且都必须整组落盘/整组复原 | 直接摊成 `Family` 上的五个属性 ⇒ `FamilyDto` 要 +5 键、`MessagePackSaveFormat` 要散插五个 `Skip`、`SaveStateMapper` 要散写五段恢复；且「复原时先后顺序」这类约束无处声明 |
| `MarriageProposal` 快照 record（不复用 `Person`） | 提议在玩家应答前**不是家族成员**（FR-044 要求字段表在决定前可读）；造一个未入册的 `Person` 会污染 `Family.Members` 与 `MembersWithoutWriteChannel` 的封闭性 | 造「幽灵 Person」再在成婚时改名 ⇒ 违反 001 不变量（`Members` 只含已入册者），且 `PersonId` 分配次序会与成婚次序耦合，破坏 FR-048 复现性 |
| `IllnessSeverityBasisPoints`（`int`，万分之一）而非 `double s` | 存档要求逐字段可复现（FR-048），浮点往返不可靠；且 `MessagePack` 对 `double` 的往返在 JSON 侧会产生 `0.1` ≠ `0.1` 的文本差异 | 存 `double` ⇒ SC-002 的「连续 120 月逐字段 diff = 0」在 JSON 存档上会假红；存 `decimal` ⇒ 与「`s` 均匀分布」的随机源（`NextDouble`）之间要额外定标度，且 `decimal` 的 28 位精度对 4 位需求是浪费 |
| 六个配置类 + 六个 `GameConfig` 转发视图 | FR-001 要求数值单点声明；`GameConfig` 现有 16 个嵌套类已按领域切分（`LivingCost`/`Income`/`Exam`/`Punishment`…），本特性六个领域界限同样清晰 | 塞进一个 `FamilyLifecyclePolicy` ⇒ 60+ 成员的单类，且与既有「一领域一文件」的 21 个 `Config/*.cs` 惯例冲突 |

## 阶段 0 / 阶段 1 小结

`research.md` 的 25 项抉择全部有结论，未决事项 = **无**（`spec.md` 的 16 条需求检查项已 16/16 通过，
六项 §17 裁决已把数值层面全部封死）。其中**五项是真问题**，逐条摘出：

1. **R-02 名字从哪来**：`TurnRunner`（`src/KFL.Rules/Save/TurnRunner.cs:54`）目前只持
   `IRandomService` + `ISaveService`，`MonthlySettlementEngine` 的 ctor（`:63`）只有前两者，
   而 ⑧-b 要在结算内造新生儿与被收养者，二者都必须经 `INameGenerator`（FR-023/FR-040）。
   抉择：**两处 ctor 各 +1 参数**，组合根（`KFL.App`）注入同一个 `INameGenerator` 实例。
   替代方案「把取名推迟到下一次玩家动作」被否——会让 `SettlementResult.Lifecycle.Births` 里的
   姓名在当月为空，与「事实段只存活于当月」矛盾。
2. **R-03 疾病程度怎么存**：`s ∈ [0,1]` 均匀，但存档要逐字段可复现 ⇒ 用
   `IllnessSeverityBasisPoints`（`int`，万分之 0~10000）落 `StatusTimers` 尾部，
   换算单点在 `IllnessPolicy`（`ToSeverity(int)` / `ToBasisPoints(double)`）。
3. **R-05 宽限月不许加字段**：`spec.md` 的 Edge Cases 明确「宽限月由子步次序**隐式**表达」。
   实现靠 ⑧-c 内部的两段循环顺序：**先**处理「进入本子步前已患病」者（扣体质 → 掷清除），
   **再**对健康成员掷感染并置 `Ill`（不扣体质、不掷清除）⇒ 新确诊者当月自然处于宽限状态。
4. **R-09 死亡与继任的次序**：同月可能有多人死于不同子步。抉择：⑧-b~⑧-f 只往
   「当月死亡登记表」里追加（含 `DeathCause` 与发现子步），⑧-g 统一置位 + 归档 + 落
   `MemberDeceased` 事件条目（按 `PersonId` 升序），⑧-h 在**死亡集合处理完毕后**才取候选集。
5. **R-24 确定性测试的绝对值断言**：`DeterminismTests.cs:198-221` 用「1 月 2 次、其余 1 次、
   全年 13 次」的绝对值断言锁随机消费。插入 ⑧ 后这些绝对值**必然变化**，但 FR-010 只要求
   ②~⑦ 与 ⑨ 的次数与次序不变 ⇒ 断言改为「⑧ 之外各步的相对次数」+「本月总次数 − ⑧ 的消费数
   == 旧绝对值」。这样既守住 FR-010，又允许 ⑧ 自己消费随机。

## 下一步

`$speckit-tasks` 依据本计划与 `data-model.md` / `contracts/` / `quickstart.md` 生成 `tasks.md`，
并 MUST 登记「回写义务清单」的 10 项范围（含历史记录类工件的**未回改**声明）。
