# Specification Quality Checklist: 存档体系

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-11
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

**校验轮次**：第 1 轮写出 3 条 `[NEEDS CLARIFICATION]`（Q1~Q3，**全部为范围/口径类，
不涉及自创数值**）；用户于 **2026-10-11 全部裁决**（三条均取拟推荐的 A 档）并已回写规格书
§14 后，第 2 轮 **16/16 项通过、0 条残留**。

### 三项裁决（2026-10-11）

| 编号 | 落点 | 事实 | 裁决 |
| ---- | ---- | ---- | ---- |
| **Q1** | FR-011（备份） | 规格书 §14 只写「每 3 回合写 `backups/存档名.dat`」，未说明相对基址、备份内容与扩展名 | **A**：备份落在**可配置存档根目录下的 `backups/`**（默认 `saves/backups/存档名.<与存档相同的扩展名>`）；时点 = 该回合自动保存**完成之后**把刚写入的内容复制一份；覆盖式、每档一份 |
| **Q2** | FR-021（账本） | 001 R-16 与 002 research 显式登记「100 年存档（预计数万条）账本是否按年分块」为「逻辑轨 ④ 实测后决定」 | **A**：**单一存档文件、全量、不分块、不压缩**；该待决项至此结清 |
| **Q3** | FR-007（档名过滤） | §14 原文列出 `\ / : - ? " < > \|`，但 `-` 在 Windows 上合法、真正的非法字符 `*` 未列出——**原文事实性缺陷** | **A**：按 **Windows 实际非法集 `\ / : * ? " < > \|`**（9 个）过滤，`-` 与中文放行；认定原文的 `-` 为 `*` 的笔误 |

三条均**不涉及自创数值**（章程原则与规格书 §17 的红线未触碰）；规格书 §14 已按 §17
裁决回写（追加「§17 裁决回写（2026-10-11，逻辑轨 ④ 存档体系）」段，原始条目文本保留不改写）。

### 未裁决但按规格书 §17② 取默认的子口径（逐条可单独否决）

写入原子性（先临时文件后原子替换）、失败一律取「明确报错」而非「尽力恢复」、
无法完整还原即拒绝（版本标识为最直观落地）、格式切换后同一 UUID 至多一个存档文件、
UUID 重复检测并拒绝（001 R-03）、Windows 保留名与首尾空白/结尾点的拒绝或规范化、
金额不做量化（保持 002 的 1 ulp 容差）、不做并发/多进程互斥、**不落盘写入时间戳因而
不引入系统时钟抽象**（001 R-04 的「阶段④ 首次需要」经复核在本特性无需求）、
统计一律由账本聚合（§12.3 的 §17 裁决回写）。

### 数值出处

本规格出现的全部节奏与取值（**每回合结算后覆盖保存**、**每 3 回合一份备份**、
**默认二进制 `.dat`**、**默认目录 `saves/`**、**扩展名 `.dat` / `.json`**）**均出自规格书
§14**，逐条标注章节号，未自创任何影响平衡的核心数值。本特性**不新增、不改动任何规则数值**。

### 范围边界（已在 Out of Scope 逐条排除）

成就持久化（逻辑轨 ⑧）、主菜单/读档列表/覆盖确认弹窗/设置面板（界面轨 U1/U5）、
进程退出钩子（U1 接线）、调试控制台（U4）、统计页与 CSV 导出（U3）、绝嗣结局画面（⑧）、
跨 .NET 版本的随机重放（本特性存的是状态、不重放随机序列）、旧格式迁移。

### 需要同步修正的既有工件（落地时一并提交，章程「需求真源」）

1. 规格书 §14 —— **已完成（2026-10-11）**，见上文三项裁决。
2. 001 `spec.md` FR-012、`data-model.md` §2.3 与 FR 对照表 —— 「改档名不变性属阶段④ 验证」
   → 落地时标注**已由 004 交付并验证**。
3. 001 `contracts/injection-seams.md` —— `ISaveService` 由「待各自阶段定义」→「已由 004 定义」。
4. 001 `research.md` R-03 / R-16、002 `research.md` 同一登记项 —— 落地时标注**已由 004 结清**
   （R-16 的结论即 Q2 裁决）。
5. `tasks.md` / `implementation-notes.md` / 既有 `checklists/` 属**历史记录类工件**，
   `MUST NOT 回改`（规格书 §16.2 迁移口径）。

### 下一轮

无待澄清项，可直接进入 `$speckit-plan`（含「`ISaveService` 首次定义」「两种格式的
round-trip 形状」「自动保存与备份节奏的落点」三项设计的落地）。
