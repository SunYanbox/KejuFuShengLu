# Specification Quality Checklist: 贿赂全链路（行贿、被抓惩罚与服刑）

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

**校验轮次**：第 1 轮写出 3 条 `[NEEDS CLARIFICATION]`（Q1~Q3，**全部为口径空白类，不涉及自创
数值**，已到规格书允许的 3 条上限）→ 用户于 2026-10-12 裁决 → 已写入 `## Clarifications` 并
同步 FR-008 / FR-020 / FR-021 及其余落点 → **第 2 轮校验：16/16 通过、`[NEEDS CLARIFICATION]`
残留 0 条**。

**关于「No implementation details」两行**：本仓库的 `spec.md` 体例（001~005 一致）把**工程名、
层名、类型名、字段名、配置归属与「金额以文为单位的定点小数」**写进规格，作为项目的需求与验收
判据（见 `## Assumptions` 的同名条目）——它们来自规格书 §1/§2 与章程的强制约束，不是本特性的
实现选择；判据本身逐条可测，故两项判为通过。

**关于「Success criteria are technology-agnostic」**：SC-002 的「配置字面量违规数 = 0」与
SC-010 的「门禁四条」是章程规定的**项目级质量门禁**（001~005 同款写法），属验收判据而非实现
细节；用户可感指标（可复现性、矩阵逐格后果、封顶置灰、余额不为负、释放降级）均已单独列出，
故判为通过。

### 三项澄清的裁决（2026-10-12，用户裁定，已写入 `## Clarifications` 与 FR）

| 编号 | 落点 | 规格书事实 | 裁决 | 落地 |
| ---- | ---- | ---------- | ---- | ---- |
| **Q1** | FR-008 | §7.1 写「第 n 次行贿**未被抓获后**，录取率乘数累计为 kⁿ」，且 §7.1 明写「无次数上限、无拒收机制」「落榜则钱不退、无补偿（仅累计乘数保留）」 | **B：乘数指数 = 行贿总次数 kⁿ（含被抓的那些）**——被抓当次**照样计入 n 并推进乘数**，被抓**不清零**该级别计数与乘数，**贿款不退**、无补偿条目 | FR-008、US1 描述/场景 6/独立测试、SC-003、Key Entities、Edge Cases「被抓当次的乘数」 |
| **Q2** | FR-020 | §7.5 写「服刑以月为单位（5 年 = 60 月）」「刑满释放后保留功名可继续考试」「曾有官职者释放时降 1 级官阶」，但 §3 的月末结算步骤序列**未写**惩罚计时的递减与释放落在哪一步 | **A：惩罚推进步插在「③官吏推进」之前**，成为新的第 **③** 步（原 ③~⑧ 顺移为 ④~⑨，**合计九步**）——递减 → 释放/降级/解除在同月的仕途推进与科举季**之前**生效 | FR-020、FR-035 ②④、Edge Cases「释放当月恰逢考试/考课」 |
| **Q3** | FR-021 | §7.4 写「有官职 → 受『全族禁考禁升职 48 月』覆盖」，§13.2 有「解除**全族**禁考/禁升」入口、§14.1 有「斯文扫地」，但 §7.5 的禁考/禁升计时是**逐人**字段 | **A：逐人计时**——施加当刻对**每名在世在册成员**各写 48 月禁考 + 48 月禁升（复用 001 既有状态位与计时，**不新增家族级真源**、**不改 005 的资格判定**）；施加后新出生/娶入的成员**不受**该次覆盖 | FR-021、FR-032、Key Entities、Edge Cases「全族禁考禁升期内出生的新成员」 |

三项裁决均须按规格书 §17 回写规格书 §7（`§17 裁决回写`块，2026-10-12；**原始条目文本保留
不改写**），回写清单见 FR-035 ① 与下文第 1 条。

### 未裁决但按规格书 §17② 取默认的子口径（逐条可单独否决，已写入 Assumptions）
- **罚金是否再乘难度支出系数**：按 §11 的字面口径**照乘**（§7.3 的难度差异内嵌在格子里但未被
  列入「不乘」例外，而 §7.2 明写「贿赂金额乘难度支出系数」）——登记为待回写的 §17 项。
- **被抓概率的 clamp**：累进到 `≥ 1.0` 时按 **100%（必定被抓）**处理（地狱殿试第 2 次即达
  `0.35 × 1.2 × 3.0 = 1.26`）。
- **「乘数达到封顶值」的执行口径**：以**最终录取率/进甲率**命中 `89.9999%` 为触顶判据（乘数
  本身无上限，只有概率有）。
- **被抓判定时点**：与**行贿动作同一时刻**（结算之外），扣款与掷骰同时发生。
- **同一档刑期/禁考月数不叠加**：同一成员被告知两次时取**本次施加值**（服刑取替换），规格书
  无「累加刑期」表述。
- **释放降级的判据**：「释放时 `Rank` 非 null」，不新增「曾为官」标记（避免与 `Rank` 构成
  第二真源）。
- **「全族」的时间边界**：施加当刻的**在世在册**成员（与 §7.4 的「在世者」同向）——Q3 = A
  的逐人计时下，施加后新出生/娶入的成员**不受**该次覆盖。
- **赦免死囚的缺口**：§7.5 明写死刑**立即执行**，故「可赦免对象从哪里来」留给 U4 规格化时按
  §17 裁决；本特性不引入「已判未决」状态。

### 数值出处

本规格出现的**全部**数值（累进步长 `0.2`；基础金额 100 / 500 / 2000 贯；基础被抓率 5% / 15% /
35%；效果乘数 k = 1.6 / 1.3 / 1.1；风险系数 0.2 / 1.0 / 1.5 / 3.0；封顶 `0.899999`；12 格矩阵的
服刑年数 5 / 15 / 20 / 10 / 3 / 1 与罚金比例 2% / 1% / 15% / 20% / 10% / 5% 与固定额 100 / 200
贯；亲属禁考禁升 36 / 24 / 48 月；比例罚金封顶 50000 贯；释放降 1 级；全族禁考禁升 48 月）
**均出自规格书 §7.1、§7.2、§7.3、§7.4、§7.5、§11**，逐条标注章节号，**未自创任何影响平衡的
核心数值**；本特性对 001~005 的既有数值（待阙区间、初始官阶映射、录取率乘区、支付原语口径）
只**消费**、不改动。

### 范围边界（已在 Out of Scope 逐条排除）

界面（U1 主界面与下月/快进循环、U2 双视图与卡片、U3 统计页、U4 调试面板、U5 设置）、成员成长/
衰老/疾病/寿命死亡（逻辑轨 ⑦）、成就解锁与绝嗣结局（⑧）、婚育/买人口/买功名婚姻（⑦）、族谱排序
与死亡结算的实现、存档迁移（004 的 Out of Scope）、官名表、跨 .NET 随机稳定性、以及「死囚待决」
持久状态（§7.5 明写死刑立即执行）。

### 需要同步修正的既有工件（落地时一并提交，章程「需求真源」）

1. 规格书 §7 的 `§17 裁决回写`块（三项裁决，2026-10-12）——**待裁决后写入**，原始条目文本保留
   不改写。
2. `specs/002-monthly-settlement-economy/contracts/monthly-settlement.md` —— §1 步骤表在「官吏
   推进」之前插入「惩罚推进」步为新的第 **③** 步（原 ③~⑧ 顺移为 ④~⑨）、§4 随机消费次序补入
   说明（**惩罚推进步不消费随机**；行贿的 `NextDouble` 发生在**结算之外**的行贿动作里）、
   `MUST NOT 存在`清单**移除**「罚金与 12 格惩罚矩阵」「连坐与服刑计时」、快照面新增惩罚子段。
3. `specs/002-*/spec.md` 与 `specs/002-*/data-model.md`（罚金与服刑计时改由 006 交付）。
4. `src/KFL.Rules/Settlement/MonthlySettlementEngine.cs` 的类文档（八步 → 九步、随机次序）。
5. `specs/001-core-skeleton/data-model.md` 的 `Person` 字段表与不变量（新增贿赂计数三项）。
6. `specs/003-opening-assets-officialdom/contracts/official-career.md` 与 `data-model.md`
   （`PromotionBanned` 从「只消费」改为「由⑥建立与递减」；释放降 1 级官阶由 006 落地）。
7. `specs/004-save-system/contracts/save-format.md`（§4 键表、§7 版本常量 **2 → 3**）与
   `specs/004-save-system/data-model.md`。
8. `specs/005-exam-cycle/contracts/exam-session.md` §4.3 条款 4（`NeutralBribeMultiplier` 占位
   作废、经同一入口传实际值）与 FR-027 的「清白传家计数由 ⑥ 维护」。
9. `LedgerCategory` 的类别计数与「无该类别」断言（**20 → 22**）；新枚举/新类别自加计数断言。
10. 断言锚点同步：`tests/KFL.Tests/Rules/SettlementEngineTests.cs` 的属性面、
    `Infrastructure/SaveFixtures.RichState()`、`SaveStateComparer.AssertPerson`、
    `SaveStateValidator`（新增贿赂计数与计时一致性口径）、`SaveFailureTests` 的版本闸门用例、
    `Architecture/ConfigLiteralTests.RegisteredValues`（新增小数 `0.2m`/`1.6m`/`1.3m`/`1.1m`/
    `0.05m`/`0.15m`/`0.35m`/`0.899999m` 等未登记者）。
11. `tasks.md` / `implementation-notes.md` / 既有 `checklists/` 属**历史记录类工件**，
    `MUST NOT 回改`（规格书 §16.2 迁移口径）；本次回写范围 MUST 登记进 006 的 `tasks.md`。

### 下一轮

第 2 轮校验完成（16/16、0 条 `[NEEDS CLARIFICATION]` 残留）→ `$speckit-plan`（四项待设计：
①惩罚推进步在契约三 §1 中的形状与回写；②贿赂计数与全族禁考逐人计时的落盘形状与格式版本
2 → 3 的提升；③`KFL.Rules/Config/` 惩罚矩阵配置类的形状与字面量登记；④行贿入口与查询面的
纯函数签名、以及 `bribeMultiplier` 接缝的实际注入方式）。
