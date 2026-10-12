# Specification Quality Checklist: 科举周期

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-12
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

**校验轮次**：第 1 轮写出 3 条 `[NEEDS CLARIFICATION]`（Q1~Q3，**全部为口径冲突/空白类，
不涉及自创数值**，已到规格书允许的 3 条上限）；用户于 **2026-10-12 全部裁决**（三条均取
拟推荐的 A 档）后，第 2 轮 **16/16 项通过、0 条残留**。

**关于「No implementation details」两行**：本仓库的 `spec.md` 体例（001~004 一致）把**工程名、
层名、类型名、字段名、配置归属与「金额以文为单位的定点小数」**写进规格，作为项目的需求与验收
判据（见 `## Assumptions` 的同名条目）——它们来自规格书 §1/§2 与章程的强制约束，不是本特性的
实现选择；判据本身逐条可测，故两项判为通过。

### 三项裁决（2026-10-12）

| 编号 | 落点 | 事实 | 裁决 |
| ---- | ---- | ---- | ---- |
| **Q1** | FR-009、FR-015 | 规格书 §6 写「殿试不淘汰（100% 成为进士）」，§7.1 又写「最终录取率/进甲率封顶 89.9999%」；按 §6 的乘法公式，殿试录取率会被封顶压到 89.9999%——**两条硬冲突**（§17 未覆盖） | **A**：**殿试录取不掷骰**，入殿试者 100% 成为进士；89.9999% 封顶**只作用于「需掷骰的概率」**＝解试/省试录取率与殿试**进甲率** |
| **Q2** | FR-004、FR-005、FR-008 | §3 只写「关闭则**报名月**暂停询问」、§6 只写考试在 8 月/次年 3 月，**未写明报考发生在哪个月**；这直接决定免解 3 败与特奏名 6 败的达成速度，以及快进的暂停点 | **A**：**报名＝考试当月**（解试＝科年 8 月、省试＝次年 3 月）；**殿试不需报名**（省试中第者与既有贡士自动参加）；**只有「报名并实际应考而落第」才计 1 败**，未报名与弃考（路费付不出）均不计败 |
| **Q3** | FR-012 | §6 只写特奏名「可拒绝」，**未写拒绝的后果、也未写询问频率与接受后的状态** | **A**：**每科年询问一次**；接受 → 经 003 的通用入口按 `AppointmentTrack.SpecialTribute` 入待阙（**不追加进士功名**），此后**退出科举**；拒绝或未应答 → 不改状态、下次省试落第后**再次询问** |

三条均**不涉及自创数值**（章程原则与规格书 §17 的红线未触碰）：Q1 是两条既有条款的冲突消解、
Q2/Q3 是空白处的落地口径。规格书已按 §17 **裁决回写**三处（§3 追加「报名窗口与『报名月』」、
§6 追加「殿试与 89.9999% 封顶」「免解与特奏名的落地口径」、§7.1 追加「封顶的作用范围」，
均为 2026-10-12），**原始条目文本保留不改写**。

### 未裁决但按规格书 §17② 取默认的子口径（逐条可单独否决）

- **免解的落地方式**：取「免的是解试这一关的录取率」——该次解试不掷骰按中第处理，随后照常
  报名省试。理由写在 Assumptions：若让白身跳过解试直接考省试，其中第会出现「白身 → 贡士」的
  跨级，而 §4.1 的四种变化原因里没有可用的跨级原因。
- **已入仕者退出科举**：有官阶或已在待阙者不得报名（正好实现 Q3 的「接受即退出」，且无需额外
  持久标记）。
- **省试路费的扣除时点**：判定**前**扣除（而非报名时扣）——使报名天然幂等，且「弃考」不必改动
  资金状态。
- **科举季步的位置**：月末结算的「⑥ 贷款」之后、「⑦ 推进时间」之前（满足「在属性成长/死亡之后、
  绝嗣判定之前」）。
- **随机消费次序**：米价 → 考课 → 储蓄利率（仅 1 月）→ 贷款计息 → 科举判定（逐人按 `PersonId`
  升序：1 次录取/进甲掷骰，进甲者紧接 1 次名次掷骰）→ 及第入仕的待阙掷骰；报名不消耗随机。
- **`Family.HasShiStatus` 的首个生产者**：家族出首个进士的当月置真、幂等（§10.2 的「直系血统」
  在 001 的模型里是**家族级**布尔字段）。
- **待阙月数的消耗时点**：及第入仕发生在 4 月的科举季步（晚于同月官吏推进步），新入待阙者当月
  剩余月数不被递减，自**次月**起算。
- **失败计数不可清零**：本特性不提供解试/省试累计失败数的清零通道（调试台的越限能力属 U4）。
- **报名标记的形状**：逐成员持久字段（考试级别，仅解试/省试），考试月结算消费后清除。
- **一甲名次的分布**：1~3 均匀整数 roll（含端点、等概率），与学业/天赋/仕族/贿赂无关。
- **年龄口径**：满 14 岁 / 满 50 岁按**周岁**且**生日当月生效**（与 §5.1 的年龄档归属一致）。
- **错过科年不计败**：举人某科年未报名即不考、不计败，3 年后仍可再报；贡士错过 4 月殿试也
  无需重考省试。

### 数值出处

本规格出现的**全部**数值（解试 1% / 省试 10% / 殿试不淘汰、进甲 5%、学业系数 `0.4 + 0.012 ×
学业`、仕天赋除数 `400`、仕族加成 `1.1`、二甲学业线 `50`、封顶 `89.9999%`、报考 14 岁、
免解 3 败、特奏名 50 岁 + 6 败、路费 10 / 20 / 40 / 80 贯、待阙 6~24 月、初始官阶 L11 / L13 /
L15 / L18）**均出自规格书 §6、§7.1、§7.2、§8.2、§11**，逐条标注章节号，**未自创任何影响平衡的
核心数值**；本特性对 003 的既有数值（待阙区间、初始官阶映射）只**消费**、不改动。

### 范围边界（已在 Out of Scope 逐条排除）

贿赂全链路与 12 格惩罚矩阵、连坐、服刑与禁考/禁升计时的建立与递减（**逻辑轨 ⑥**）、成员成长/
衰老/疾病/死亡（**⑦**）、成就解锁与绝嗣结局（**⑧**）、婚育与买功名婚姻（**⑦**）、快进循环与
「下月」按钮（**U1**）、调试控制台面板（**U4**）、卡片与统计（U2/U3）、设置面板（U5）、存档迁移
（004 的 Out of Scope）、官名表（规格书未定义）。

### 需要同步修正的既有工件（落地时一并提交，章程「需求真源」）

1. **规格书 §3 / §6 / §7.1** —— **已完成（2026-10-12）**，见上文三项裁决。
2. `specs/002-monthly-settlement-engine/contracts/monthly-settlement.md` —— §1 步骤表插入「科举季」
   （序号顺移）、`MUST NOT 存在`清单**移除**「科举季触发（阶段⑤）」、快照面新增科举子段、
   §4 随机消费次序补入科举判定。
3. `specs/002-*/spec.md` 的 Out of Scope（科举周期改由 005 交付）与 `specs/002-*/data-model.md`。
4. `src/KFL.Rules/Settlement/MonthlySettlementEngine.cs` 的类文档（七步 → 八步、随机次序）。
5. `specs/002-*/contracts/config-registry.md` §6/§7 的科举数值归属。
6. `specs/001-core-skeleton/data-model.md` 的 `Person` / `Family` 字段表与不变量（新增三个 `Person`
   字段、一个 `Family` 字段、一个新枚举）。
7. `specs/004-save-system/contracts/save-format.md`（§4 键表、§7 版本常量 **1 → 2**）与
   `specs/004-save-system/data-model.md`。
8. `LedgerCategory` 的类别计数与「无该类别」断言（**19 → 20**）；新枚举/新类别**自加**计数断言
   （仓库现无 DTO 枚举完备性测试）。
9. 断言锚点同步：`tests/KFL.Tests/Rules/SettlementEngineTests.cs`（`:302-343` 属性面）、
   `Infrastructure/SaveFixtures.RichState()`、`SaveStateComparer.AssertPerson`、
   `SaveStateValidator`（新增报名标记的一致性口径）、`SaveFailureTests` 的版本闸门用例、
   `Architecture/ConfigLiteralTests.RegisteredValues`（新增小数 `0.01m`/`0.012m`/`0.10m`/`0.4` 中
   未登记者）。
10. `tasks.md` / `implementation-notes.md` / 既有 `checklists/` 属**历史记录类工件**，
    `MUST NOT 回改`（规格书 §16.2 迁移口径）；本次回写范围 MUST 登记进 005 的 `tasks.md`。

### 下一轮

无待澄清项，可直接进入 `$speckit-plan`（四项待设计：①科举季步在契约三 §1 中的形状与回写；
②三个新字段 + 一个新枚举的落盘形状与格式版本 1 → 2 的提升；③`KFL.Rules/Config/` 科举配置类的
形状与字面量登记；④录取率/进甲率的纯函数入口与「贿赂乘数」接缝的注入方式）。
