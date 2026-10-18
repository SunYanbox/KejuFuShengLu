# Specification Quality Checklist: 成就与绝嗣结局（逻辑轨 ⑧）

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-18
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) — **按本仓库房内约定执行**：章程「需求真源」
      与 AGENTS.md 要求工件 MUST 引用规格书章节号与既有类型/契约锚点（001~007 的 `spec.md` 均如此），
      故本文引用 `PunishmentQueries`、`LifecycleSnapshot`、`TotalAssetsCalculator`、`ISaveService`
      等既有锚点属**义务**而非泄漏；本特性 MUST NOT 引入新的技术栈、框架或第三方依赖（FR-005）。
- [x] Focused on user value and business needs（五个 User Story 均含 Why this priority 与 Independent Test）
- [x] Written for non-technical stakeholders — 同上房内约定
- [x] All mandatory sections completed（User Scenarios & Testing / Requirements / Success Criteria，
      并按 004~007 先例补 Clarifications、范围来源、Out of Scope、Assumptions）

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain — **3 项已裁决并回填（2026-10-18）**：
      **Q1** 取「交付定义 + 计数接缝 + 待接线清单」（68 条全定义；MUST NOT 实现调试控制台、MUST NOT 自造
      亏损事件规则）；**Q2** 取「月末结算后统一判定 + 动作型事实即时判定，解锁不可撤销」；
      **Q3** 取「随档新增累计器与结束状态，`SaveFileFormat.Version` 4 → 5，旧档按既有版本闸门拒绝」。
      裁决内容已落入 Clarifications（Session 2026-10-18）与 FR-041 / FR-042 / FR-043 / Assumptions 1~4、24。
      **4 条次级口径 ⚠ 亦已复核完毕（同日 A~D）**：存续年数门槛=逐字从严分档（`百年世家` > 100 /
      `千秋万代` > 300 / `绝处逢生` ≥ 100）、传承代数=最大 `Person.Generation` + 1（辈分 0 基准）、
      平均寿命=已故成员**享年分布的三均值**（Tukey trimean：Q1/Q2/Q3 取 Tukey hinges、保留 3 位小数，
      含夭折；金色算例 8 行见 Assumptions 7）、`榜下捉婿`=历史上出现过 `ImperialProposal` 提议即解锁（含被拒）。
      已落入「Clarifications / Session 2026-10-18（次级口径复核 A~D，已裁决）」、Assumptions 5~7、12、25。
      **本规格书已无 ⚠ 待复核项**。
- [x] Requirements are testable and unambiguous（FR-001~FR-048 均可断言；68 条成就逐条给出判据与来源分类）
- [x] Success criteria are measurable（SC-001~SC-015 均为可计数/可 diff/可枚举的门禁式断言）
- [x] Success criteria are technology-agnostic（以玩家可感结果与门禁条数表述；引用既有测试文件属房内约定）
- [x] All acceptance scenarios are defined（US1~US5 共 19 条 Given/When/Then）
- [x] Edge cases are identified（15 条，含同月绝嗣与解锁、写盘失败重试取原年月、待生效难度、
      同月多路径命中、存续年数边界、买人口辈分、外嫁者计入/排除两个方向、清白传家读档不变、
      同月买卖漏判、全局文件损坏、UUID 冲突、家主空悬≠绝嗣、计数溢出、结束态冻结、文件版本演进）
- [x] Scope is clearly bounded（Out of Scope 逐条排除 U1~U5、调试控制台实现、经商亏损事件规则、
      随机事件总表、逻辑轨 ⑨、旧档迁移、新增声望属性、跨进程并发）
- [x] Dependencies and assumptions identified（Assumptions 25 条，其中 4 条次级口径 ⚠ 已于 2026-10-18
      复核为正式口径；第 24、25 条汇总全部裁决与「已无待复核项」的结论）

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria — FR-001~FR-048 全部就绪：
      FR-041~FR-043 经 Q1 裁决后已定死为「交付定义 + 外部上报接缝 + 待接线清单」，
      其「MUST NOT 实现调试控制台 / MUST NOT 自造亏损事件规则」两半均可断言。
- [x] User scenarios cover primary flows（全局解锁与持久化、确定性记账、绝嗣与编年史、结束态状态机、
      待接线成就的接缝）
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification（房内约定，见上）

## Notes

- **Requirements are testable**：**16/16 通过**（Clarifications 已闭合，无待裁决项；
  Q1~Q3 于 2026-10-18 裁决并回填 spec.md 与 FR-041~FR-043）。
- **本特性必然引起的跨特性形状变更**（`tasks.md` MUST 按 AGENTS.md 铁律登记回写范围）：
  ① `SaveFileFormat.Version`（007 为 4）→ 5，`SaveStateDto` / `SaveStateMapper` / `SaveStateValidator`
  与 004 的存档契约、`data-model.md` 同步（FR-036）；② 新增「家族累计器 + 结束状态 + 编年史快照」
  三个形状，回写 007 的 `data-model.md` 事实表与 004 的字段表；③ 新增「成就判定器」对 006 预留
  派生谓词的消费关系，回写 006 的惩罚查询契约（至少注明 `FamilyCumulativeBribes` 的语义是
  **尝试次数**，而 `暗度陈仓` 系列需要的是**成功次数**）；④ 规格书 §14 补判定时点与最早日期口径、
  §14.1 标注 6 条待接线、§15 补编年史七项口径、§17 追加 2026-10-18 裁决块（Q1~Q3 + 次级口径 A~D）（FR-039）。
- **已知的形状变更对既有测试的影响**：`SaveFileFormat.Version` 自增会使既有存档测试中的版本断言
  全部需要同步（逐条在 `tasks.md` 登记，SC-013）；本特性 MUST NOT 修改既有测试期望值来迁就新行为
  （FR-047 / SC-015），确有必要者逐条记录理由。
- **本特性必须交付的确定性口径**（实现前 MUST 先在契约里钉死，否则不可复现）：
  判定入口唯一（FR-006）、动作型事实的立即判定入口清单（FR-009）、不可重算的累计量清单（FR-013）、
  在册 vs 全量成员两类计数口径（Assumptions 13~14）。
- **§14.2 / §16.1 的必测单测清单第 9 条**「绝嗣判定（外嫁不计）」由本特性交付，测试落点 MUST 在
  `tasks.md` 登记（FR-048）。
- **次级口径复核（2026-10-18，A~D + 三均值细化）**：起草稿曾有 4 条 ⚠ 待复核项，已逐条裁决并回填
  （存续年数门槛=逐字从严；传承代数=最大 `Generation` + 1；平均寿命=**享年分布的三均值**
  `(Q1 + 2·Q2 + Q3)/4`、Tukey hinges、3 位小数；`榜下捉婿`=提议产出即解锁）。
  **复核同时修正了起草稿的两处理解错误**：① 原稿误记「开基家主辈分为 1」，实为 **辈分 0 基准**
  （规格书 §4.4:115 与 `src/KFL.Core/Entities/Family.cs:138`），故 `五世其昌` 由 `Generation >= 5`
  更正为 `>= 4`、传承代数由「最深辈分」改为「最大辈分 + 1」；② 首轮把「三均值」误读为算术平均，
  经用户给出公式后更正为 Tukey trimean。
- **本项修正对 FR 的影响**：`Person.AgeAt`（死亡当月享年）确认可用；因三均值需要 Q1/Q2/Q3 三个分位点，
  FR-013 ③ 的持久化形状由「累计死亡人数 + 累计享年（和）」升级为**逐个追加的享年完整序列**
  ——只存「和 + 个数」无法反推分位点。成员无死亡时间字段，历史享年本就不可事后重算，故该序列必须持久化。
- **下一阶段的任务前置**：Q1~Q3 与次级口径 A~D 均已裁决，`$speckit-plan` 可直接开始；
  若 Q3 的「Version 自增」落地，计划的第一个任务 MUST 是存档格式与 004 契约的同步
  （含全部存量版本断言的更新，SC-013）。
