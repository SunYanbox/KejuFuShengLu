# Specification Quality Checklist: 开局资产与官吏体系

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-08
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

**校验轮次**：第 1 轮写出 3 条 `[NEEDS CLARIFICATION]`（Q1~Q3，全部为**范围/口径**类，
不涉及自创数值）；用户于 2026-10-08 全部裁决后，第 2 轮 **16/16 项通过、0 条残留**。

### 三项裁决（2026-10-08）

- **Q1（范围，现已落到 FR-026）** — 用户答 **A + 调整规格书顺序**：
  「UI 相关不应该这么早做；UI 和逻辑的 Feature 应当分开」。
  据此**规格书 §16 被改写为双轨**：
  - **逻辑轨**：① slnx + Core ✅ → ② 月结算引擎 ✅ → **③ 开局资产与官吏体系（本特性）** →
    ④ 存档体系 → ⑤ 科举周期 → ⑥ 贿赂全链路 → ⑦ 婚姻与生育/疾病/寿命/家主继任/买人口 →
    ⑧ 成就与绝嗣结局 → ⑨ 难度装配与打磨。
  - **界面轨**：U1 主界面 + 下月/快进 → U2 双视图 + 卡片 + 筛选器 → U3 统计页与图表 →
    U4 调试控制台 GUI → U5 设置面板（难度/姓氏/存档格式）；U 轨 MUST 只消费逻辑轨已交付的
    规则，MUST NOT 复制规则或在界面内自造未交付的逻辑入口。
  - 规格书 §16 新增 §16.1（双轨与约束）、§16.2（**旧编号 → 新编号对照表**）与迁移口径
    （历史记录类工件保留旧编号、活工件按表更新）；§4.4 与 §9.5 的旧「阶段⑧」措辞已同步
    改标为「逻辑轨 ⑦」。
  - 本特性因此**不再构成跨阶段提前动工**——它正是新的逻辑轨 ③（旧编号下「开局资产属
    阶段③」+「官职属阶段⑧」的合并）。
- **Q2（范围，现已落到 FR-027）** — 用户答 **A：§8.2 全量**。待阙、授官、考课晋升、政绩、
  致仕半俸、禁升期内跳过判定**全部纳入**本特性，不留半个仕途。
- **Q3（口径，现已落到 FR-019）** — 用户答 **A：收入之前**。当月授官当月起领俸、满 70 岁
  当月即按半俸计；同月内次序固定为「待阙递减与授官 → 政绩 +1 → 致仕判定 → 在职计时与考课」。
  由此推出两条已写进 Assumptions 的推论：本月政绩 +1 计入本月到期的考课；致仕判定先于考课
  （故不会出现「同月既升一级、又按升级后的级别计半俸」）。Edge Cases 中三条相关边界
  （70 岁仍在待阙、同月待阙期满且满 70、考课成功同月满 70）已按该次序改写为自洽表述。

### 未裁决但已按规格书 §17② 取默认的子口径

逐条写在 `spec.md` 的 Assumptions，可单独否决而不牵连其余条目：家主/配偶/孩子的年龄与体质
区间（±5 与 80~100/90~100 取区间内整数均匀）、孩子年龄 0~8 均匀、孩子性别 50/50、
「士的 1 子」= 1 名孩子、「孩子天赋」取 §4.2「开局成员」的 N(60,20) 而非遗传公式、
待阙时长整数均匀 6~24、「正态分布取整后 clamp 0~100」、禁升期内到期的考课「跳过并重新计时」、
服刑期间待阙计时暂停、致仕半俸 = 全俸 × 50% 且官阶保留、开局不落账本条目（初始余额）。

### 需要同步修正的既有工件（落地时一并提交，章程「需求真源」）

1. 规格书 §16 — **已完成**（§16.1/§16.2 + §4.4、§9.5 的改标）。
2. `specs/002-monthly-settlement-economy/` 的活工件中「官职……属阶段⑧」「界面属阶段③」
   「统计页属阶段⑩」「成就/绝嗣属阶段⑪」「调试控制台属阶段⑦」「双视图属阶段⑨」等措辞
   → 按规格书 §16.2 对照表改为逻辑轨/界面轨编号（另见本特性 spec 的 Assumptions 末两条）。
   **已完成（2026-10-08）**：`spec.md`、`plan.md`、`research.md`、`data-model.md`、
   `quickstart.md`、`contracts/{config-registry,ledger,monthly-settlement}.md` 共 8 个活工件，
   裸旧编号 0 残留（`tasks.md` 与 `checklists/` 按迁移口径保留）。
3. `specs/001-core-skeleton/` 的活工件中同类旧编号 → 同上。
   **已完成（2026-10-08）**：`spec.md`、`plan.md`、`research.md`、`data-model.md`、
   `quickstart.md`、`contracts/injection-seams.md` 共 6 个活工件，裸旧编号 0 残留
   （`tasks.md`、`implementation-notes.md`、`checklists/` 按迁移口径保留）。
   - 旧编号 ⑧ 为**二义编号**（同时指「官职」与「婚育/疾病/寿命」），迁移时按上下文拆分：
     官职语境 → 逻辑轨 ③，婚育/疾病/寿命/继任/买人口语境 → 逻辑轨 ⑦；同句混装处
     （001 `spec.md`、002 `spec.md`、002 `data-model.md`）拆成两个标签而非只换一个。
   - 旧编号 ⑫ 同样二义（「难度装配与打磨」→ 逻辑轨 ⑨；「难度切换面板」→ 界面轨 U5），
     按原文写的是「逻辑」还是「面板」分派；这一对举已在规格书 §16.1 的 U5 定义中隐含。
4. 001 的 `DegreeRecord`（功名记录）需**向后兼容地**扩展「甲第」（一甲/二甲/三甲）字段，
   001 的 `spec.md` / `data-model.md` 应补一句指向本特性（FR-014）。
5. `tasks.md`、`implementation-notes.md` 等**历史记录类工件保留旧编号，MUST NOT 回改**。

### 数值出处

本规格出现的全部数值（四出身初始资产与成员构成、家主/配偶/孩子的年龄·学业·体质区间、
待阙 6~24、初始官阶 L11/L13/L15/L18、考课 36 月与 25% + 政绩 × 0.3% 与封顶 70%、
政绩上限 100、致仕 70 岁、半俸 50%）**均出自规格书 §10.1、§8.1、§8.2**，逐条标注章节号，
未自创任何影响平衡的核心数值。

### 下一轮

无待澄清项，可直接进入 `$speckit-plan`（含「甲第字段扩展」「编号迁移」两项设计的落地）。
