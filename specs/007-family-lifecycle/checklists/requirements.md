# Specification Quality Checklist: 家族生命周期（婚姻与生育、疾病、寿命、家主继任、买人口）

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-17
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) — **按本仓库房内约定执行**：章程「需求真源」
      与 AGENTS.md 要求工件 MUST 引用规格书章节号与既有类型/契约锚点（001~006 的 `spec.md` 均如此），
      故本文引用 `Family.Marry`、`IRandomService`、`CountedMembers` 等既有锚点属**义务**而非泄漏；
      本特性 MUST NOT 引入新的技术栈、框架或第三方依赖。
- [x] Focused on user value and business needs（五个 User Story 均含 Why this priority 与 Independent Test）
- [x] Written for non-technical stakeholders — 同上房内约定
- [x] All mandatory sections completed（User Scenarios & Testing / Requirements / Success Criteria，
      并按 004~006 先例补 Clarifications、Out of Scope、Assumptions）

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain — **6 项已裁决并写回**（2026-10-17）：
      Q1 取 §17② 直观默认（提亲 5% × 负面事件系数、榜下捉婿 30% × 系数且对方付 500~1000 贯、
      阶层加权只按总资产三档）；Q2 改为「疾病程度 `s` 决定扣减 2~10 与药费 0.05~0.50 +
      体质自愈概率 + 可选季度疗程」并**取代** §4.3 的病程 2~4 月；**Q4** 患病概率改按体质线性插值
      （`100 ⇒ 0.25%` / `1 ⇒ 40%`）× 主导天赋乘数（农 0.80/1.20、仕 0.95/1.10、工 1.10/1.10、
      商 1.05/0.95），删除年龄档 / ×2 / 20% 封顶；**Q5** 追加「`< 3` 岁免病 + 清除概率三段年龄增益
      （`3~12` ⇒ +0.60 / `12~24` ⇒ +0.30 / `≥ 24` ⇒ 0）」与「榜下捉婿按**官员压力**
      `P = Σ(0.02 + 1.48 × (18 − L) / 17)` 折减概率 `÷ (1 + P)`、放大金额 `× (1 + P)`、
      按 `P` 分档配偶功名（白身 / 举人 / 贡士，无进士档）、提议可拒绝」；Q3 每年一次、
      出生当月即第一次（0/1/2 岁共 3 次）；**Q6**（采纳 Qwen3.8-Max 平衡意见）基础痊愈系数
      `0.75 → 1.00`、新确诊当月为**宽限月**（0 扣体质 / 0 药费 / 0 清除）、疗程公式改为
      `(体质 × 1.10/100 + 0.20) × m_q + b`、患病概率 MUST NOT 乘难度负面事件系数。
      裁决内容已落入 FR-001 / FR-014 / FR-019 / FR-020 / FR-026 / FR-029 / FR-030 / FR-044 / FR-045
- [x] Requirements are testable and unambiguous（除上述 6 项外，FR-001~FR-051 均可断言）
- [x] Success criteria are measurable（SC-001~SC-015 均为可计数/可 diff/可枚举的门禁式断言）
- [x] Success criteria are technology-agnostic（以玩家可感结果与门禁条数表述；引用既有测试文件属房内约定）
- [x] All acceptance scenarios are defined（US1~US5 共 35 条 Given/When/Then）
- [x] Edge cases are identified（24 条，含同月多人死亡、继任与买人口同月、外嫁者寿数、救济期叠加、
      年龄档切换当月、捉婿提议被拒、`P = 0` 与 `P` 极大、宽限月与寿终同月、体质归零与清除判定同月）
- [x] Scope is clearly bounded（Out of Scope 逐条排除 U1~U5、⑧、⑨、迁移、事件总表、声望属性）
- [x] Dependencies and assumptions identified（Assumptions 含 23 条 §17② 默认口径 + 开放项清单）

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria（FR ↔ US 场景 ↔ SC 三层可追溯，
      FR-050 的回写清单逐条对应 SC-011）
- [x] User scenarios cover primary flows（托媒娶入、嫁出外嫁、买功名婚姻、生育遗传、疾病寿命、
      家主继任、买人口）
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification（房内约定，见上）

## Notes

- **Requirements are testable**：16/16 通过（Clarifications 已闭合，无待裁决项）。
- 六处裁决对**既有规格书**构成实质修改，`tasks.md` MUST 把它们作为变更源：
  ① §4.3 的「病程 2~4 月 + 固定 −10 体质/月」被整体替换（疾病程度 + 自愈概率 + 疗程）；
  ② §4.3 的「患病概率：16 岁以上 2.5%/月，40 岁以上 5%，体质<50 再 ×2，封顶 20%」被整体替换为
  「按体质线性插值 × 主导天赋乘数」（Q4）；③ §4.3 再追加「`< 3` 岁免病 + 清除概率三段年龄增益」
  （Q5）；④ §9.1 / §9.2 补入提亲 5%、榜下捉婿 `30% ÷ (1 + P)`、金额 `× (1 + P)`、配偶功名分档
  （`P` 阈值 1 / 3）、提议可拒绝、阶层加权三档（不新增「声望」）；⑤ §9.4 补入夭折判定粒度
  （出生当月 + 1 岁 + 2 岁）；⑥ §4.3 的基础痊愈系数 `0.75 → 1.00`、新确诊当月为**宽限月**、
  疗程公式改为 `(体质 × 1.10/100 + 0.20) × m_q + b`、患病概率 MUST NOT 乘难度负面事件系数（Q6）。
  回写清单见 FR-050 ①。
- 下一阶段的**任务前置**：本特性必然引起跨特性形状变更（结算九步 → 十步、`LedgerCategory` 类别扩充、
  `SaveFileFormat.Version` 3 → 4、`StatusTimers` 新增「疾病程度 + 疗程剩余月数」、`Person` 新增名师标志、
  新增「清除概率年龄增益」与「榜下捉婿官员压力」配置项），
  **Q6 的宽限月由结算子步次序隐式表达，MUST NOT 为它新增 `Person` / `StatusTimers` 字段**；
  `tasks.md` MUST 按 AGENTS.md 铁律登记回写范围；`specs/002` / `004` / `006` 的活工件回写
  与存量不一致项（004 `data-model.md` 的 `FormatVersion = 2`、005 `exam-session.md:18` 的八步旧序）
  一并在同一提交内完成。
- **Q4 已取代**上一版清单里的待办「天赋对疾病机制的影响须先出方案再问」：用户在 Q4（m00232）
  直接给出了四项天赋的患病 / 痊愈乘数，故该机制**已授权实现**，不再是开放项。
- **Q6 已闭合**先前两条待裁决项：**天赋乘数的取用口径**确定为「主导天赋 = 数值最高者、并列按
  农 → 商 → 仕 → 工」（Assumptions 3c，Qwen3.8-Max 独立给出同一口径）；**平衡参数**采纳
  Qwen3.8-Max 的建议（基础痊愈系数 `1.00`，成年段阈值由 `76.7` 降到 `71.1`、初始落险比例由
  `20.4%` 降到 `8.2%`），并附加宽限月与疗程新式。
- **仍留待实机观察或用户复核**：① 药费档位（现行 `0.05 + 0.45s`，若治疗显得过便宜 MAY 上调到
  `0.08 + 0.55s` / `0.10 + 0.70s`，MUST NOT 与下调痊愈概率同时进行）；② 「饥馑 × 疾病叠加」的手感；
  ③ **Assumptions 3d**（清除概率的年龄增益取「加点的绝对百分点」而非相对乘算）仍是 §17② 默认——
  Qwen3.8-Max 的平衡意见针对 Q5 之前的数值系统，未覆盖该点。以上任一项调整均属 §17 范畴。
