# Specification Quality Checklist: 月度结算与经济引擎

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-06
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

- **校验轮次**：第 1 轮写出 2 条 `[NEEDS CLARIFICATION]`；用户裁决后第 2 轮全部通过
  （0 条残留、16/16 项通过）。此后用户追加裁决 Q3（成员计口口径），已并入正文并复跑校验，
  仍为 16/16 通过。
- **Q1（范围，已裁决 2026-10-06）**：§9.5「买人口」的购买入口与递增计价归**阶段⑧**。
  理由：§16 阶段② 的括注只列生活费/收入/贷款/储蓄/饥馑，且 §9.5 位于对应阶段⑧ 的
  「婚姻与生育」章——按章程「工件与规格书冲突时以规格书为准」处理。
  已落到：`spec.md` FR-028 与 Out of Scope；**并同步修正 001 工件**的旧措辞
  （`specs/001-core-skeleton/spec.md`、`specs/001-core-skeleton/research.md` 的「阶段②经济」
  → 阶段⑧）；规格书 §9.5 新增 §17 裁决回写。
- **Q2（数值口径，已裁决 2026-10-06）**：§5.2「工出身 bonus = 总资产 × 6%（为正才发）」的
  **总资产 = 现金 + 储蓄 + 田宅铺市值 − 贷款本金 − 欠息**（与 §7.5 罚金基数同口径），
  商本池不计入；为正才发放。
  已落到：`spec.md` FR-006、US3 验收场景 4 与 Assumptions；规格书 §5.2 新增 §17 裁决回写。
- **Q3（成员口径，用户于校验后提出并裁决 2026-10-06）**：**服刑**与**外嫁**成员同为「本人不在
  家族内」：显示在界面与族谱中，但**不消耗生活费、不产生任何收益或人力**。据此新增
  `spec.md` FR-029（计口 = 在册且未服刑）与 FR-030（在册 ≠ 计口；**待阙包在计口内**——人仍在
  家中吃饭、只是无俸，该细化经用户 2026-10-06 确认），改写 Edge Cases 的两条成员口径，并给
  US1 增加验收场景 9、新增 SC-009。
  规格书同步：§5.1 新增「计口范围」§17 裁决回写（含待阙确认）；§12.1 补外嫁与服刑同口径的回写。
- 同批额外回写：规格书 §16 补注「阶段② 的『收入』含俸禄表取值与月摊发放、『饥馑』指四阶段
  状态机」——这两条边界原是本规格的假设（见 `spec.md` Assumptions），按章程「需求真源」
  回写后不再是工件单方面推断。
- 本规格的技术性引用（工程名、层名、配置归属、金额以「文」为单位的定点小数）来自规格书
  §1/§2 与项目章程的强制约束，是项目的需求与验收判据，不属于本特性的实现选择。
- 下一轮：`$speckit-clarify` 已无待澄清项，可直接进入 `$speckit-plan`。
